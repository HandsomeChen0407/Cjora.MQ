using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Cjora.MQ.Services;

/// <summary>
/// MQ 后台服务基类
/// 提供批量消费、动态并发控制、队列积压自适应
/// </summary>
public abstract class MqHostedService : BackgroundService
{
    private readonly IMq _mq;
    private readonly ILogger<MqHostedService> _logger;
    private readonly MqOptions _mqOptions;

    // 队列空时休眠毫秒
    private const int _minIntervalMs = 10;

    // 动态可变参数
    private int _batchCount;      // 每批取多少条
    private SemaphoreSlim _semaphore; // 控制并发数量

    // CPU 核心数
    private readonly int _cpuCount;

    protected MqHostedService(IMq mq, ILogger<MqHostedService> logger, MqOptions mqOptions)
    {
        _mq = mq;
        _logger = logger;
        _mqOptions = mqOptions;

        _cpuCount = Environment.ProcessorCount;

        InitPerformanceSettings();
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // 原生方式：直接连接 MQ
        await _mq.ConnectAsync(_mqOptions);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = new List<(string Topic, byte[] Payload)>();
                int count = 0;

                // 批量取消息
                while (count < _batchCount && !stoppingToken.IsCancellationRequested)
                {
                    var msg = await _mq.TryDequeueAsync(stoppingToken);
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

                // 并发处理消息
                var tasks = messages.Select(async message =>
                {
                    await _semaphore.WaitAsync(stoppingToken);
                    try
                    {
                        var msgString = Encoding.UTF8.GetString(message.Payload);
                        LogInformation($"消费内容：{msgString}");
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
                });

                await Task.WhenAll(tasks);
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
        _batchCount = Math.Clamp(_cpuCount * 400, 500, 5000);
        int maxConcurrency = Math.Clamp(_cpuCount * 4, 8, 64);
        _semaphore = new SemaphoreSlim(maxConcurrency);

        _logger.LogInformation($"MQ 服务初始化性能：CPU={_cpuCount}, 批次={_batchCount}, 最大并发={maxConcurrency}");
    }
    private void AdjustPerformanceByQueueLoad()
    {
        int q = QueuesCount();

        if (q > 20000)
            IncreaseConcurrency(50);
        else if (q > 5000)
            IncreaseConcurrency(20);
        else if (q < 500)
            DecreaseConcurrency(10);
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
    private void DecreaseConcurrency(int step)
    {
        int cur = _semaphore.CurrentCount;
        int newVal = Math.Clamp(cur - step, 4, 128);

        if (newVal < cur)
        {
            _semaphore = new SemaphoreSlim(newVal);
            _logger.LogInformation($"动态降低并发：{cur} → {newVal}");
        }
    }
    public int QueuesCount() => _mq.QueuesCount();
    public Task PublishAsync(string topic, object msg) => _mq.PublishAsync(topic, msg);
    public void LogDebug(string msg) => _logger.LogDebug($"MQ 消息队列：{msg}");
    public void LogInformation(string msg) => _logger.LogInformation($"MQ 消息队列：{msg}");
    public void LogWarning(string msg) => _logger.LogWarning($"MQ 消息队列预警：{msg}");
    public void LogError(Exception ex) => _logger.LogError(ex, "MQ 消息队列异常");
    public void LogError(string msg) => _logger.LogError($"MQ 消息队列异常：{msg}");

    #endregion
}
