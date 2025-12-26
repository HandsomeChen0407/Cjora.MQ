namespace Cjora.MQ.Interfaces;

/// <summary>
/// MQ 生产者能力接口
///
/// 语义：
/// - 发布消息
/// - 不关心消息来源
/// </summary>
public interface IMqProducer : IMqClient
{
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
