using Cjora.MQ.Enums;
using Confluent.Kafka;

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
    /// 订阅主题，可用逗号分隔多个主题
    /// </summary>
    public string SubTopic { get; set; } = string.Empty;

    /// <summary>
    /// 内存队列长度（通道容量）
    /// 建议根据消息量和消费者能力配置
    /// </summary>
    public int ChannelLength { get; set; } = 5000;

    #region MQTT 专用配置

    /// <summary>
    /// MQTT 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// MQTT 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// MQTT KeepAlive 时间（秒）
    /// 客户端每隔此时间向 Broker 发送心跳
    /// </summary>
    public int KeepAliveSeconds { get; set; } = 90;

    #endregion

    #region Kafka 专用配置

    /// <summary>
    /// Kafka Consumer GroupId
    /// 不同消费者组可以并行消费相同 Topic
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// 是否自动提交 Offset
    /// 强烈建议生产环境使用 false 手动提交以保证消息准确性
    /// </summary>
    public bool EnableAutoCommit { get; set; } = false;

    /// <summary>
    /// Offset 重置策略
    /// Earliest: 从最早的消息开始消费
    /// Latest: 从最新消息开始消费
    /// </summary>
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;

    /// <summary>
    /// Kafka 会话超时（毫秒）
    /// 超过此时间未发送心跳则认为消费者下线
    /// </summary>
    public int SessionTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 最大 Poll 间隔（毫秒）
    /// 单次 Poll 最大允许间隔，超过则触发重平衡
    /// </summary>
    public int MaxPollIntervalMs { get; set; } = 300000;

    /// <summary>
    /// Producer 批量发送延迟（毫秒）
    /// 用于控制发送批量消息的延迟时间，提高吞吐量
    /// </summary>
    public int LingerMs { get; set; } = 5;

    /// <summary>
    /// Producer 单批次最大消息数
    /// 用于控制批量发送数量，提高吞吐量
    /// </summary>
    public int BatchNumMessages { get; set; } = 10000;

    /// <summary>
    /// 是否开启 Kafka 幂等性
    /// 生产环境建议开启，确保消息不重复写入
    /// </summary>
    public bool EnableIdempotence { get; set; } = true;

    #endregion
}