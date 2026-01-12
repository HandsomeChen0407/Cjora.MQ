using Cjora.MQ.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cjora.MQ.Services;

/// <summary>
/// MQ 运行时容器
///
/// 职责：
/// 1. 统一管理所有 MQ Client 生命周期
/// 2. 提供按 Name 获取 Consumer / Producer
/// 3. 保证 MQ 在 HostedService 之前完成初始化
///
/// 这是整个 MQ 基础设施的“心脏”
/// </summary>
public sealed class MqRuntime : IHostedService, IAsyncDisposable
{
    private readonly IEnumerable<IMqClient> _clients;
    private readonly ILogger<MqRuntime> _logger;

    private Dictionary<string, IMqClient> _clientMap;

    public MqRuntime(
        IEnumerable<IMqClient> clients,
        ILogger<MqRuntime> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    /// <summary>
    /// Host 启动时调用
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _clientMap = new Dictionary<string, IMqClient>(StringComparer.OrdinalIgnoreCase);

        foreach (var client in _clients)
        {
            if (_clientMap.ContainsKey(client.Name))
                throw new InvalidOperationException($"重复的 MQ Client Name：{client.Name}");

            _logger.LogInformation($"启动 MQ Client：{client.Name}");
            await client.StartAsync(cancellationToken);

            _clientMap[client.Name] = client;
        }

        _logger.LogInformation("所有 MQ Client 启动完成");
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// 获取 MQ Consumer
    /// </summary>
    public IMqConsumer GetConsumer(string name)
        => Get<IMqConsumer>(name);

    /// <summary>
    /// 获取 MQ Producer
    /// </summary>
    public IMqProducer GetProducer(string name)
        => Get<IMqProducer>(name);

    private T Get<T>(string name) where T : class, IMqClient
    {
        if (_clientMap == null)
            throw new InvalidOperationException("MQ Runtime 尚未启动，无法获取 Client");

        if (!_clientMap.TryGetValue(name, out var client))
            throw new KeyNotFoundException($"MQ Client 未找到：{name}");

        if (client is not T typed)
            throw new InvalidOperationException($"{name} 不是 {typeof(T).Name}");

        return typed;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }
    }
}
