using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace Cjora.MQ.Services
{
    /// <summary>
    /// MQTT 消息队列实现
    /// 使用内存 Channel 缓存消息，实现异步消费和发布。
    /// 支持 string、byte[] 或任意对象（JSON 序列化）发布消息。
    /// </summary>
    public class MqMqtt : IMq
    {
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
        /// MQTT 客户端工厂
        /// </summary>
        private MqttClientFactory _mqttFactory;

        /// <summary>
        /// MQTT 客户端连接选项
        /// </summary>
        private MqttClientOptions _mqttClientOptions;

        /// <summary>
        /// MQ 配置选项
        /// </summary>
        private MqOptions _mqOptions;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<MqMqtt> _logger;

        /// <summary>
        /// 构造函数，注入日志
        /// </summary>
        /// <param name="logger">ILogger 实例</param>
        public MqMqtt(ILogger<MqMqtt> logger)
        {
            _logger = logger;
        }

        #region IMq 接口实现

        /// <summary>
        /// 连接 MQTT 服务并初始化通道和事件
        /// </summary>
        /// <param name="mqOptions">MQ 配置，包括服务地址、端口、用户名、密码和订阅主题等</param>
        public async Task ConnectAsync(MqOptions mqOptions)
        {
            _mqOptions = mqOptions;

            _mqttFactory = new MqttClientFactory();
            _mqttClient = _mqttFactory.CreateMqttClient();

            // 初始化内存通道
            _channel = Channel.CreateBounded<(string Topic, byte[] Payload)>(
                new BoundedChannelOptions(mqOptions.ChannelLength)
                {
                    SingleReader = true,    // HostedService 单线程消费
                    SingleWriter = false,   // MQTT 多线程回调
                    FullMode = BoundedChannelFullMode.Wait // 队列满时等待，不丢消息
                });

            // 初始化 MQTT 连接选项
            _mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqOptions.ServiceIP, _mqOptions.ServicePort)
                .WithCredentials(_mqOptions.Username, _mqOptions.Password)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_mqOptions.Mqtt.KeepAliveSeconds))
                .Build();

            // 注册 MQTT 事件
            _mqttClient.ConnectedAsync += MqttClient_ConnectedAsync;
            _mqttClient.DisconnectedAsync += MqttClient_DisconnectedAsync;
            _mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;

            // 首次连接
            await _mqttClient.ConnectAsync(_mqttClientOptions);
        }

        #endregion

        #region MQTT 事件

        /// <summary>
        /// MQTT 客户端连接成功后触发，订阅配置的主题
        /// </summary>
        private async Task MqttClient_ConnectedAsync(MqttClientConnectedEventArgs args)
        {
            try
            {
                var subscribeOptions = new MqttClientSubscribeOptionsBuilder();

                foreach (var topic in _mqOptions.SubTopic.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    subscribeOptions.WithTopicFilter(f => f.WithTopic(topic.Trim()));
                }

                await _mqttClient.SubscribeAsync(subscribeOptions.Build());
                _logger.LogInformation($"已订阅主题: {_mqOptions.SubTopic}");
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

        #region 消息队列操作

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

        /// <summary>
        /// 异步发布消息到指定主题
        /// 支持 string、byte[] 或任意对象（JSON 序列化）
        /// </summary>
        /// <param name="topic">消息主题</param>
        /// <param name="data">消息内容</param>
        public async Task PublishAsync(string topic, object data)
        {
            try
            {
                byte[] payload = IMq.SerializeToBytes(data);

                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithRetainFlag(false)
                    .Build();

                var result = await _mqttClient.PublishAsync(msg);

                if (result != null && result.IsSuccess)
                {
                    _logger.LogInformation($"消息发送成功 【Topic】{topic} 【Payload】{Encoding.UTF8.GetString(payload)}");
                }
                else
                {
                    _logger.LogError($"消息发送失败 【Topic】{topic} 【Payload】{Encoding.UTF8.GetString(payload)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"MQTT 发布消息异常");
            }
        }

        #endregion
    }
}