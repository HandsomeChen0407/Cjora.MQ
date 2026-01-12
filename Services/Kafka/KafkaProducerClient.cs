using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Cjora.MQ.Services
{
    /// <summary>
    /// Kafka 消息队列实现发布功能。
    /// 支持 string、byte[] 或任意对象（通过 Newtonsoft.Json 序列化）发布消息。
    /// </summary>
    public sealed class KafkaProducerClient : IMqProducer
    {
        /// <summary>
        /// 实例名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Kafka 生产者
        /// </summary>
        private IProducer<string, byte[]> _producer;

        /// <summary>
        /// MQ 配置
        /// </summary>
        private readonly MqProfileOptions _profile;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<KafkaProducerClient> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">实例名称</param>
        /// <param name="profile">配置文件</param>
        /// <param name="logger">ILogger 实例</param>
        public KafkaProducerClient(string name, MqProfileOptions profile, ILogger<KafkaProducerClient> logger)
        {
            Name = name;
            _profile = profile;
            _logger = logger;
        }

        /// <summary>
        /// 统一启动入口（由 MQ Runtime 调用）
        /// </summary>
        public async Task StartAsync(CancellationToken ct)
        {
            await ConnectInternalAsync(ct);
        }

        private Task ConnectInternalAsync(CancellationToken ct)
        {
            // --- 初始化 Kafka 生产者 ---
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _profile.ServiceIP,
                EnableIdempotence = _profile.Kafka.EnableIdempotence, // 保证幂等
                LingerMs = _profile.Kafka.LingerMs,
                BatchNumMessages = _profile.Kafka.BatchNumMessages,
                Acks = Acks.All
            };

            _producer = new ProducerBuilder<string, byte[]>(producerConfig)
                .SetErrorHandler((_, e) => _logger.LogError($"[Kafka][Producer] {e.Reason}"))
                .Build();

            _logger.LogInformation($"[Kafka][Producer] 已连接 | BootstrapServers={_profile.ServiceIP}");

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _producer?.Dispose();
            return ValueTask.CompletedTask;
        }

        #region IMqPublisher 接口实现

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

            byte[] payload = MqSerializer.ToBytes(data);

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
    }
}
