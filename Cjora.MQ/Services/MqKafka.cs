using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Channels;

namespace Cjora.MQ.Services;

/// <summary>
/// Kafka 队列实现
/// </summary>
public sealed class MqKafka : IMq
{
    private Channel<(string Topic, byte[] Payload)> _channel;
    private int _queueCount;

    private IConsumer<string, byte[]> _consumer;
    private IProducer<string, byte[]> _producer;

    private CancellationTokenSource _cts;
    private Task _consumeLoop;

    private MqOptions _mqOptions;
    private readonly ILogger<MqKafka> _logger;

    public MqKafka(ILogger<MqKafka> logger)
    {
        _logger = logger;
    }

    #region IMq 接口

    public Task ConnectAsync(MqOptions mqOptions)
    {
        _mqOptions = mqOptions ?? throw new ArgumentNullException(nameof(mqOptions));

        // --- Channel 初始化 ---
        _channel = Channel.CreateBounded<(string, byte[])>(
            new BoundedChannelOptions(_mqOptions.ChannelLength)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        // --- Consumer 初始化 ---
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _mqOptions.ServiceIP,
            GroupId = _mqOptions.GroupId,
            EnableAutoCommit = false,
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

        // --- Producer 初始化 ---
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _mqOptions.ServiceIP,
            EnableIdempotence = _mqOptions.EnableIdempotence,
            LingerMs = _mqOptions.LingerMs,
            BatchNumMessages = _mqOptions.BatchNumMessages,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, byte[]>(producerConfig)
            .SetErrorHandler((_, e) => _logger.LogError($"[Kafka][Producer] {e.Reason}"))
            .Build();

        // --- 启动消费循环 ---
        _cts = new CancellationTokenSource();
        _consumeLoop = Task.Run(ConsumeLoop, _cts.Token);

        _logger.LogInformation($"[Kafka] 已连接 | Group={_mqOptions.GroupId} | Topics={_mqOptions.SubTopic}");

        return Task.CompletedTask;
    }

    public Task<(string Topic, byte[] Payload)?> TryDequeueAsync(CancellationToken token = default)
    {
        if (_channel.Reader.TryRead(out var item))
        {
            Interlocked.Decrement(ref _queueCount);
            return Task.FromResult<(string, byte[])?>(item);
        }
        return Task.FromResult<(string, byte[])?>(null);
    }

    public int QueuesCount() => Volatile.Read(ref _queueCount);

    public async Task PublishAsync(string topic, object data)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentNullException(nameof(topic));

        byte[] payload = data switch
        {
            byte[] bytes => bytes,
            string str => Encoding.UTF8.GetBytes(str),
            _ => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data))
        };

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

                    if (!_channel.Writer.TryWrite((topic, payload)))
                    {
                        await _channel.Writer.WriteAsync((topic, payload), _cts.Token);
                    }

                    Interlocked.Increment(ref _queueCount);

                    // 立即提交 Offset
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
            // 正常退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Kafka][ConsumeLoop] 循环异常");
        }
    }

    #endregion
}
