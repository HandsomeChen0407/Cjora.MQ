using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Channels;

namespace Cjora.MQ.Services
{
    /// <summary>
    /// Kafka 消息队列实现
    /// 使用内存 Channel 作为缓冲区，实现异步消费和发布功能。
    /// 支持 string、byte[] 或任意对象（通过 Newtonsoft.Json 序列化）发布消息。
    /// </summary>
    public sealed class MqKafka : IMq
    {
        /// <summary>
        /// 内存消息通道，用于缓存 Kafka 消费到的消息
        /// </summary>
        private Channel<(string Topic, byte[] Payload)> _channel;

        /// <summary>
        /// 当前内存队列长度（Channel 无 Count，需要手动维护）
        /// </summary>
        private int _queueCount;

        /// <summary>
        /// Kafka 消费者
        /// </summary>
        private IConsumer<string, byte[]> _consumer;

        /// <summary>
        /// Kafka 生产者
        /// </summary>
        private IProducer<string, byte[]> _producer;

        /// <summary>
        /// 后台消费循环的取消令牌
        /// </summary>
        private CancellationTokenSource _cts;

        /// <summary>
        /// 后台消费任务
        /// </summary>
        private Task _consumeLoop;

        /// <summary>
        /// MQ 配置
        /// </summary>
        private MqOptions _mqOptions;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<MqKafka> _logger;

        /// <summary>
        /// 构造函数，注入日志
        /// </summary>
        /// <param name="logger">ILogger 实例</param>
        public MqKafka(ILogger<MqKafka> logger)
        {
            _logger = logger;
        }

        #region IMq 接口实现

        /// <summary>
        /// 连接 Kafka 服务，并启动后台消费循环
        /// </summary>
        /// <param name="mqOptions">MQ 配置信息，包括服务地址、订阅主题、分组 ID 等</param>
        /// <returns>已完成的任务</returns>
        public Task ConnectAsync(MqOptions mqOptions)
        {
            _mqOptions = mqOptions ?? throw new ArgumentNullException(nameof(mqOptions));

            // --- 初始化内存 Channel ---
            _channel = Channel.CreateBounded<(string, byte[])>(
                new BoundedChannelOptions(_mqOptions.ChannelLength)
                {
                    SingleReader = true,    // HostedService 单线程消费
                    SingleWriter = false,   // Kafka Poll 单线程，但预留扩展
                    FullMode = BoundedChannelFullMode.Wait // 队列满时等待，不丢消息
                });

            // --- 初始化 Kafka 消费者 ---
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _mqOptions.ServiceIP,
                GroupId = _mqOptions.GroupId,
                EnableAutoCommit = false,                     // 手动提交 Offset
                AutoOffsetReset = _mqOptions.AutoOffsetReset,
                SessionTimeoutMs = _mqOptions.SessionTimeoutMs,
                MaxPollIntervalMs = _mqOptions.MaxPollIntervalMs
            };

            _consumer = new ConsumerBuilder<string, byte[]>(consumerConfig)
                .SetErrorHandler((_, e) => _logger.LogError($"[Kafka][Consumer] {e.Reason}"))
                .Build();

            var topics = _mqOptions.SubTopic
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _consumer.Subscribe(topics);

            // --- 初始化 Kafka 生产者 ---
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _mqOptions.ServiceIP,
                EnableIdempotence = _mqOptions.EnableIdempotence, // 保证幂等
                LingerMs = _mqOptions.LingerMs,
                BatchNumMessages = _mqOptions.BatchNumMessages,
                Acks = Acks.All
            };

            _producer = new ProducerBuilder<string, byte[]>(producerConfig)
                .SetErrorHandler((_, e) => _logger.LogError($"[Kafka][Producer] {e.Reason}"))
                .Build();

            // --- 启动后台消费循环 ---
            _cts = new CancellationTokenSource();
            _consumeLoop = Task.Run(ConsumeLoop, _cts.Token);

            _logger.LogInformation($"[Kafka] 已连接 | Group={_mqOptions.GroupId} | Topics={_mqOptions.SubTopic}");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 尝试从内存队列中异步取出一条消息（非阻塞）
        /// </summary>
        /// <param name="token">可选的取消令牌</param>
        /// <returns>消息元组 (Topic, Payload)，队列为空时返回 null</returns>
        public Task<(string Topic, byte[] Payload)?> TryDequeueAsync(CancellationToken token = default)
        {
            if (_channel.Reader.TryRead(out var item))
            {
                Interlocked.Decrement(ref _queueCount);
                return Task.FromResult<(string, byte[])?>(item);
            }
            return Task.FromResult<(string, byte[])?>(null);
        }

        /// <summary>
        /// 获取内存队列中当前消息数量（估算值）
        /// </summary>
        /// <returns>队列长度</returns>
        public int QueuesCount() => Volatile.Read(ref _queueCount);

        /// <summary>
        /// 异步发布消息到指定 Kafka Topic
        /// 支持 string、byte[] 或任意对象（JSON 序列化）
        /// </summary>
        /// <param name="topic">目标 Topic</param>
        /// <param name="data">消息内容</param>
        public async Task PublishAsync(string topic, object data)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentNullException(nameof(topic));

            byte[] payload = IMq.SerializeToBytes(data);

            try
            {
                var result = await _producer.ProduceAsync(topic, new Message<string, byte[]>
                {
                    Key = null,
                    Value = payload
                });

                _logger.LogInformation($"[Kafka][Publish] Topic={topic}, Offset={result.Offset}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Kafka][Publish] Topic={topic} 消息发送失败");
            }
        }

        #endregion

        #region 消费循环

        /// <summary>
        /// Kafka 消费后台循环，将消息写入内存 Channel
        /// </summary>
        private async Task ConsumeLoop()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var record = _consumer.Consume(_cts.Token);
                        if (record == null) continue;

                        var topic = record.Topic;
                        var payload = record.Message.Value;

                        // 写入 Channel，如果满则等待
                        if (!_channel.Writer.TryWrite((topic, payload)))
                        {
                            await _channel.Writer.WriteAsync((topic, payload), _cts.Token);
                        }

                        Interlocked.Increment(ref _queueCount);

                        // 提交 Offset，保证至少一次语义
                        _consumer.Commit(record);
                    }
                    catch (ConsumeException ce)
                    {
                        _logger.LogError(ce, "[Kafka][ConsumeLoop] 消费异常");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消退出
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Kafka][ConsumeLoop] 循环异常");
            }
        }

        #endregion
    }
}
