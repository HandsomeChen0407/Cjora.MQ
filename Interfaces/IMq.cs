using Cjora.MQ.Options;

namespace Cjora.MQ.Interfaces;

/// <summary>
/// MQ（消息队列）服务接口。
/// 提供基础的 MQ 连接、消息发布与消费能力，支持异步操作和队列状态查询。
/// </summary>
public interface IMq
{
    /// <summary>
    /// 异步连接 MQ 服务。
    /// </summary>
    /// <param name="mqOptions">MQ 配置信息，包括服务地址、端口、认证信息、订阅主题等。</param>
    /// <returns>异步任务，连接完成后返回。</returns>
    Task ConnectAsync(MqOptions mqOptions);

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

    /// <summary>
    /// 异步发布消息到指定主题。
    /// 支持多种数据类型：
    /// - string 类型，直接作为文本消息发布；
    /// - byte[] 类型，直接作为二进制消息发布；
    /// - 其他对象类型，将通过 Newtonsoft.Json 序列化为 JSON 字符串后发布。
    /// </summary>
    /// <param name="topic">目标主题（Topic），用于订阅者识别消息类型。</param>
    /// <param name="data">消息内容，可以是 string、byte[] 或其他对象。</param>
    /// <returns>异步任务，消息发布完成后返回。</returns>
    Task PublishAsync(string topic, object data);
}
