namespace Cjora.MQ.Interfaces;

/// <summary>
/// MQ 客户端基础接口（生命周期级别）
/// 
/// 设计目标：
/// 1. 所有 MQ（Kafka / MQTT / Rabbit）
///    都必须具备统一的启动与释放能力
/// 2. 生命周期由 Host 管理，而不是业务代码
/// </summary>
public interface IMqClient : IAsyncDisposable
{
    /// <summary>
    /// 客户端唯一名称（用于运行时索引）
    ///
    /// 例如：
    /// kafka
    /// mqtt
    /// rabbit
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 启动 MQ 客户端（建立连接、订阅、后台线程等）
    ///
    /// ⚠ 注意：
    /// - 不允许在构造函数中连接 MQ
    /// - 所有 IO 初始化必须在这里完成
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);
}
