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

    /// <summary>
    /// MQTT 协议版本
    /// 可选值：V310，V311, V500
    /// 默认值：V500
    /// </summary>
    public string ProtocolVersion { get; set; } = "V500";

    /// <summary>
    /// TraceId 属性名称（用于从 MQTT 发布结果的 UserProperties 中提取 TraceId）
    /// 例如：腾讯云 MQTT 使用 "$__messageId"
    /// 如果为空或 null，则不提取 TraceId
    /// </summary>
    public string? TraceIdPropertyName { get; set; }
}