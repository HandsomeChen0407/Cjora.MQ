namespace Cjora.MQ.Options;

/// <summary>
/// MQTT 专用配置
/// </summary>
public sealed class MqttOptions
{
    /// <summary>
    /// MQTT KeepAlive 时间（秒）
    /// 客户端每隔此时间向 Broker 发送心跳
    /// </summary>
    public int KeepAliveSeconds { get; set; } = 90;
}