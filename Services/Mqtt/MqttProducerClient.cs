using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using System.Buffers;
using System.Text;

namespace Cjora.MQ.Services
{
    /// <summary>
    /// MQTT 消息队列实现发布功能
    /// 支持 string、byte[] 或任意对象（JSON 序列化）发布消息。
    /// </summary>
    public class MqttProducerClient : IMqProducer
    {
        /// <summary>
        /// 实例名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// MQTT 客户端实例
        /// </summary>
        private IMqttClient _mqttClient;

        /// <summary>
        /// MQTT 客户端连接选项
        /// </summary>
        private MqttClientOptions _mqttClientOptions;

        /// <summary>
        /// MQ 配置选项
        /// </summary>
        private readonly MqProfileOptions _profile;

        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<MqttProducerClient> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">实例名称</param>
        /// <param name="profile">配置文件</param>
        /// <param name="logger">ILogger 实例</param>
        public MqttProducerClient(string name, MqProfileOptions profile, ILogger<MqttProducerClient> logger)
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
            _mqttClient.DisconnectedAsync += MqttClient_DisconnectedAsync;

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

        #endregion

        #region IMqProducer 实现

        /// <summary>
        /// 异步发布消息到指定主题
        /// 支持 string、byte[] 或任意对象（JSON 序列化）
        /// </summary>
        /// <param name="topic">消息主题</param>
        /// <param name="data">消息内容</param>
        /// <param name="token">可选取消令牌</param>
        public async Task PublishAsync(string topic, object data)
        {
            try
            {
                byte[] payload = MqSerializer.ToBytes(data);

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