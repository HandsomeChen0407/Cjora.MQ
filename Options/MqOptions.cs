using Cjora.MQ.Enums;

namespace Cjora.MQ.Options;

/// <summary>
/// MQ 配置选项
/// 支持 MQTT 和 Kafka 的统一配置，适用于 .NET Options 模式绑定
/// </summary>
public sealed class MqOptions
{
    /// <summary>
    /// 消息队列类型
    /// </summary>
    public MqTypeEnum MqType { get; set; } = MqTypeEnum.Unknown;

    /// <summary>
    /// 服务地址
    /// - MQTT: Broker IP 地址
    /// - Kafka: 多个 broker 地址用逗号分隔 (broker1:9092,broker2:9092)
    /// </summary>
    public string ServiceIP { get; set; } = string.Empty;

    /// <summary>
    /// 服务端口
    /// - MQTT 使用
    /// - Kafka 可忽略
    /// </summary>
    public int ServicePort { get; set; } = 0;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 订阅主题，可用逗号分隔多个主题
    /// </summary>
    public string SubTopic { get; set; } = string.Empty;

    /// <summary>
    /// 内存队列长度（通道容量）
    /// 建议根据消息量和消费者能力配置
    /// </summary>
    public int ChannelLength { get; set; } = 5000;

    /// <summary>
    /// Mqtt 专用配置
    /// </summary>
    public MqttOptions Mqtt { get; set; } = new MqttOptions();

    /// <summary>
    /// Kafka 专用配置
    /// </summary>
    public KafkaOptions Kafka { get; set; } = new KafkaOptions();
}