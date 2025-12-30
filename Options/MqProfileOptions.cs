using Cjora.MQ.Enums;

namespace Cjora.MQ.Options;

/// <summary>
/// 单个 MQ 实例配置
/// </summary>
public sealed class MqProfileOptions
{
    /// <summary>
    /// 消息队列类型
    /// </summary>
    public string MqType { get; set; } = MqTypeEnum.Unknown.ToString();

    /// <summary>
    /// 当前 Profile 的角色
    /// </summary>
    public string Role { get; set; } = MqRoleEnum.Unknown.ToString();

    /// <summary>
    /// 内存通道长度（所有 MQ 共用默认值）
    /// </summary>
    public int ChannelLength { get; set; } = 5000;

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
    /// Mqtt 专用配置
    /// </summary>
    public MqttOptions Mqtt { get; set; } = new MqttOptions();

    /// <summary>
    /// Kafka 专用配置
    /// </summary>
    public KafkaOptions Kafka { get; set; } = new KafkaOptions();
}
