using Cjora.MQ.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Cjora.MQ.Services;

/// <summary>
/// MQ 后台服务基类
/// 提供批量消费、动态并发控制、队列积压自适应
/// </summary>
public abstract class MqHostedService : BackgroundService
{
    private IMqConsumer _consumer = null!;
    private readonly MqRuntime _runtime;
    private readonly string _consumerName;
    private readonly ILogger<MqHostedService> _logger;

    // 队列空时休眠毫秒
    private const int _minIntervalMs = 10;

    // 动态可变参数
    private int _batchCount;      // 每批取多少条
    private SemaphoreSlim _semaphore; // 控制并发数量

    // CPU 核心数
    private readonly int _cpuCount;

    protected MqHostedService(MqRuntime runtime, string consumerName, ILogger<MqHostedService> logger)
    {
        _runtime = runtime;
        _consumerName = consumerName;
        _logger = logger;

        _cpuCount = Environment.ProcessorCount;
        InitPerformanceSettings();
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer = _runtime.GetConsumer(_consumerName);

        _logger.LogInformation(
            "MQ Consumer 已绑定：{ConsumerName}",
            _consumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = new List<(string Topic, byte[] Payload)>();
                int count = 0;

                // 批量取消息
                while (count < _batchCount && !stoppingToken.IsCancellationRequested)
                {
                    var msg = await _consumer.TryDequeueAsync(stoppingToken);
                    if (msg == null) break;
                    messages.Add(msg.Value);
                    count++;
                }

                // 队列为空
                if (messages.Count == 0)
                {
                    await Task.Delay(_minIntervalMs, stoppingToken);
                    continue;
                }

                // 动态调节性能
                AdjustPerformanceByQueueLoad();

                int qCount = QueuesCount();
                if (qCount > 10)
                    LogWarning($"开始消费 {messages.Count} 条消息，队列积压 {qCount} 条");

                // 并发处理消息（不等待整批完成，避免单条阻塞拖慢整体）
                foreach (var message in messages)
                {
                    // 直接调度异步方法，由 SemaphoreSlim 控制最大并发
                    _ = ProcessMessageInternalAsync(message, stoppingToken);
                }

                // 轻微让出时间片，避免空转影响其它业务
                await Task.Delay(1, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQ service loop error");
                await Task.Delay(500, stoppingToken);
            }
        }

        _logger.LogInformation("MQ Hosted Service 停止");
    }

    protected abstract Task ProcessMessage(string topic, string msg, CancellationToken stoppingToken);

    #region 辅助方法

    private void InitPerformanceSettings()
    {
        // === 并发控制 ===
        // 初始并发：CPU * 1（保守起步）
        // 最大并发：CPU * 4（只用于动态扩容上限）
        int initialConcurrency = Math.Clamp(_cpuCount, 2, 8);
        int maxConcurrency = Math.Clamp(_cpuCount * 4, 8, 128);

        _semaphore = new SemaphoreSlim(initialConcurrency, maxConcurrency);

        // === 批量控制 ===
        // 初始批量：CPU * 100
        // 最大批量：5000
        _batchCount = Math.Clamp(_cpuCount * 100, 200, 2000);

        _logger.LogInformation(
            "MQ 性能初始化：CPU={Cpu}, 初始并发={InitConcurrency}, 最大并发={MaxConcurrency}, 批次={Batch}",
            _cpuCount,
            initialConcurrency,
            maxConcurrency,
            _batchCount);
    }
    /// <summary>
    /// 带并发控制的消息处理封装，避免重复样板代码 & 不再额外使用 Task.Run
    /// </summary>
    private async Task ProcessMessageInternalAsync((string Topic, byte[] Payload) message, CancellationToken stoppingToken)
    {
        await _semaphore.WaitAsync(stoppingToken);
        try
        {
            var msgString = Encoding.UTF8.GetString(message.Payload);
            await ProcessMessage(message.Topic, msgString, stoppingToken);
        }
        catch (Exception ex)
        {
            LogError(ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }
    private void AdjustPerformanceByQueueLoad()
    {
        int q = QueuesCount();

        if (q > 20000)
            IncreaseConcurrency(32);
        else if (q > 5000)
            IncreaseConcurrency(16);

        // ⚠ 不再支持动态降低并发
        // 降并发会导致 SemaphoreSlim 状态不可控
    }
    private void IncreaseConcurrency(int step)
    {
        int cur = _semaphore.CurrentCount;
        int max = Math.Clamp(cur + step, 8, 128);

        if (max > cur)
        {
            _semaphore.Release(max - cur);
            _logger.LogInformation($"动态提升并发：{cur} → {max}");
        }
    }
    public int QueuesCount() => _consumer.QueuesCount();
    public void LogDebug(string msg) => _logger.LogDebug($"MQ 消息队列：{msg}");
    public void LogInformation(string msg) => _logger.LogInformation($"MQ 消息队列：{msg}");
    public void LogWarning(string msg) => _logger.LogWarning($"MQ 消息队列预警：{msg}");
    public void LogError(Exception ex) => _logger.LogError(ex, "MQ 消息队列异常");
    public void LogError(string msg) => _logger.LogError($"MQ 消息队列异常：{msg}");

    #endregion
}
