using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Microsoft.Extensions.Logging;
using MQTTnet;
using System.Buffers;
using System.Threading.Channels;

namespace Cjora.MQ.Services
{
    /// <summary>
    /// MQTT 消息队列实现消费功能
    /// 使用内存 Channel 缓存消息，实现异步消费
    /// </summary>
    public class MqttConsumerClient : IMqConsumer
    {
        /// <summary>
        /// 实例名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// MQ 配置选项
        /// </summary>
        private readonly MqProfileOptions _profile;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<MqttConsumerClient> _logger;

        /// <summary>
        /// 内存消息通道，用于缓存 MQTT 消费到的消息
        /// </summary>
        private Channel<(string Topic, byte[] Payload)> _channel;

        /// <summary>
        /// 当前队列长度（Channel 无 Count，需要手动维护）
        /// </summary>
        private int _queueCount = 0;

        /// <summary>
        /// MQTT 客户端实例
        /// </summary>
        private IMqttClient _mqttClient;

        /// <summary>
        /// MQTT 客户端连接选项
        /// </summary>
        private MqttClientOptions _mqttClientOptions;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">实例名称</param>
        /// <param name="profile">配置文件</param>
        /// <param name="logger">ILogger 实例</param>
        public MqttConsumerClient(string name, MqProfileOptions profile, ILogger<MqttConsumerClient> logger)
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
            // 初始化内存通道
            _channel = Channel.CreateBounded<(string Topic, byte[] Payload)>(
                new BoundedChannelOptions(_profile.ChannelLength)
                {
                    SingleReader = true,    // HostedService 单线程消费
                    SingleWriter = false,   // MQTT 多线程回调
                    FullMode = BoundedChannelFullMode.Wait // 队列满时等待，不丢消息
                });

            await ConnectInternalAsync(ct);
        }

        private async Task ConnectInternalAsync(CancellationToken ct)
        {
            var mqttFactory = new MqttClientFactory();
            _mqttClient = mqttFactory.CreateMqttClient();
            // 初始化 MQTT 连接选项
            _mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_profile.ServiceIP, _profile.ServicePort)
                .WithCredentials(_profile.Username, _profile.Password)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_profile.Mqtt.KeepAliveSeconds))
                .Build();

            // 注册 MQTT 事件
            _mqttClient.ConnectedAsync += MqttClient_ConnectedAsync;
            _mqttClient.DisconnectedAsync += MqttClient_DisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;

            // 首次连接
            await _mqttClient.ConnectAsync(_mqttClientOptions);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_mqttClient != null)
                {
                    if (_mqttClient.IsConnected)
                    {
                        _logger.LogInformation("[MQTT] 正在断开连接...");
                        await _mqttClient.DisconnectAsync();
                    }

                    _mqttClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MQTT] Dispose 释放异常");
            }
        }

        #region MQTT 事件

        /// <summary>
        /// MQTT 客户端连接成功后触发，订阅配置的主题
        /// </summary>
        private async Task MqttClient_ConnectedAsync(MqttClientConnectedEventArgs args)
        {
            try
            {
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder();

                foreach (var topic in _profile.SubTopic.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    subscribeOptions.WithTopicFilter(f => f.WithTopic(topic.Trim()));
                }

                await _mqttClient.SubscribeAsync(subscribeOptions.Build());
                _logger.LogInformation($"已订阅主题: {_profile.SubTopic}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅主题失败");
            }
        }

        /// <summary>
        /// MQTT 客户端断开连接时触发，自动尝试重连
        /// </summary>
        private Task MqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs args)
        {
            _logger.LogWarning("MQTT 断开连接，尝试重连...");
            _ = ReconnectAsync();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 重连逻辑，失败时持续尝试
        /// </summary>
        private async Task ReconnectAsync()
        {
            try
            {
                await Task.Delay(2000);

                if (_mqttClient.IsConnected) return;

                await _mqttClient.ConnectAsync(_mqttClientOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT 重连失败，继续尝试");
                _ = ReconnectAsync();
            }
        }

        /// <summary>
        /// MQTT 收到消息时触发，将消息写入内存通道
        /// </summary>
        private async Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            try
            {
                var payload = arg.ApplicationMessage.Payload.ToArray();
                var topic = arg.ApplicationMessage.Topic;

                // 尝试写入通道，如果满则阻塞
                if (!_channel.Writer.TryWrite((topic, payload)))
                {
                    await _channel.Writer.WriteAsync((topic, payload));
                }

                Interlocked.Increment(ref _queueCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT 接收消息异常");
            }
        }

        #endregion

        #region IMqConsumer 接口实现

        /// <summary>
        /// 尝试从内存队列中取出一条消息（非阻塞）
        /// </summary>
        /// <param name="token">可选取消令牌</param>
        /// <returns>返回元组 (Topic, Payload)，队列为空时返回 null</returns>
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
        /// 获取当前内存队列长度（估算值）
        /// </summary>
        /// <returns>队列长度</returns>
        public int QueuesCount() => Volatile.Read(ref _queueCount);

        #endregion
    }
}