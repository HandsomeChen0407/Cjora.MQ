using Confluent.Kafka;

namespace Cjora.MQ.Options;

/// <summary>
/// Kafka 专用配置
/// </summary>
public sealed class KafkaOptions
{
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
}