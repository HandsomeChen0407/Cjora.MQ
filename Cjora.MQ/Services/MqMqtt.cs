using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System.Buffers;
using System.Text;
using System.Threading.Channels;

namespace Cjora.MQ.Services;

/// <summary>
/// MQTT 队列实现
/// </summary>
public class MqMqtt : IMq
{
    /// <summary>
    /// 异步消息通道，支持批量消费
    /// </summary>
    private Channel<(string Topic, byte[] Payload)> _channel;

    /// <summary>
    /// 当前队列长度（官方 Channel 无 Count，自行维护）
    /// </summary>
    private int _queueCount = 0;

    /// <summary>
    /// MQTT 客户端
    /// </summary>
    private IMqttClient _mqttClient;

    private MqttClientFactory _mqttFactory;
    private MqttClientOptions _mqttClientOptions;
    private MqOptions _mqOptions;
    private readonly ILogger<MqMqtt> _logger;

    public MqMqtt(ILogger<MqMqtt> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 连接 MQTT
    /// </summary>
    public async Task ConnectAsync(MqOptions mqOptions)
    {
        _mqOptions = mqOptions;

        _mqttFactory = new MqttClientFactory();
        _mqttClient = _mqttFactory.CreateMqttClient();

        // 使用有界通道
        _channel = Channel.CreateBounded<(string Topic, byte[] Payload)>(
            new BoundedChannelOptions(mqOptions.ChannelLength)
            {
                SingleReader = true,    // HostedService 单线程消费
                SingleWriter = false,   // MQTTnet 多线程回调
                FullMode = BoundedChannelFullMode.Wait // 背压，不丢消息
            });

        _mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqOptions.ServiceIP, _mqOptions.ServicePort)
            .WithCredentials(_mqOptions.Username, _mqOptions.Password)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_mqOptions.KeepAliveSeconds))
            .Build();



        // 注册事件
        _mqttClient.ConnectedAsync += MqttClient_ConnectedAsync;
        _mqttClient.DisconnectedAsync += MqttClient_DisconnectedAsync;
        _mqttClient.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;

        await _mqttClient.ConnectAsync(_mqttClientOptions);
    }

    #region MQTT事件

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

    private Task MqttClient_DisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("MQTT 断开连接，尝试重连...");
        _ = ReconnectAsync();
        return Task.CompletedTask;
    }

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

    private async Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        try
        {
            var payload = arg.ApplicationMessage.Payload.ToArray();
            var topic = arg.ApplicationMessage.Topic;

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

    #region IMq接口

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
        try
        {
            byte[] payload = data switch
            {
                string s => Encoding.UTF8.GetBytes(s),
                byte[] b => b,
                _ => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data))
            };

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
