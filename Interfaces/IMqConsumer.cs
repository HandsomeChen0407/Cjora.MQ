namespace Cjora.MQ.Interfaces;

/// <summary>
/// MQ 消费者能力接口
///
/// 语义：
/// - 拉取消息
/// - 管理内存队列
/// </summary>
public interface IMqConsumer : IMqClient
{
    /// <summary>
    /// 尝试从队列中异步取出一条消息。
    /// </summary>
    /// <param name="cancellationToken">可选的取消令牌，用于取消等待队列消息的操作。</param>
    /// <returns>
    /// 异步任务，返回一个元组 (Topic, Payload)：  
    /// - Topic: 消息所属主题。  
    /// - Payload: 消息内容的字节数组。  
    /// 如果队列为空，则返回 null。
    /// </returns>
    Task<(string Topic, byte[] Payload)?> TryDequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取队列中当前缓冲的消息数量。
    /// </summary>
    /// <returns>当前队列中未处理消息的数量（估算值，非实时精确）。</returns>
    int QueuesCount();
}