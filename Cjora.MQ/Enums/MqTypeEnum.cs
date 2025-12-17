namespace Cjora.MQ.Enums;

/// <summary>
/// 消息队列类型枚举
/// </summary>
public enum MqTypeEnum
{
    /// <summary>
    /// 未知类型，通常抛出异常或提示用户配置错误
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// MQTT 协议消息队列
    /// </summary>
    Mqtt = 1,

    /// <summary>
    /// Kafka 消息队列
    /// </summary>
    Kafka = 2
}