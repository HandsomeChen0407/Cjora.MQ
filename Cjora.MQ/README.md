Cjora.MQ

高性能、可扩展的 .NET 消息队列封装库，支持 MQTT 和 Kafka，提供统一接口 IMq，可轻松集成到 ASP.NET Core 或任意 .NET 项目中。
特点：
1. 支持 MQTT / Kafka 的统一操作接口
2. 内置异步消息通道，支持批量消费
3. 支持动态并发调整，队列积压自适应
4. JSON 消息序列化保持原始大小写
5. 可与依赖注入无缝集成

🚀 功能
连接 MQ：通过 MqOptions 配置连接参数
发布消息：支持 string、byte[] 或任意对象（自动 JSON 序列化）
批量消费：内置后台服务 MqHostedService，支持并发消费和队列积压调节
多环境配置：支持不同环境 appsettings.json 配置
扩展性强：可轻松添加新的 MQ 类型（如 RabbitMQ）

📦 安装
# 使用 NuGet 安装（示例）
dotnet add package Cjora.MQ
或将源码直接引入你的解决方案。

⚙️ 配置
在 appsettings.json 中添加 MQ 配置：
{
  "MqOptions": {
    "MqType": "1", // 1 mqtt
    "ServiceIP": "127.0.0.1",
    "ServicePort": 1883,
    "Username": "user",
    "Password": "pass",
    "SubTopic": "topic1,topic2",
    "ChannelLength": 5000,
    "Mqtt": {
      "KeepAliveSeconds": 90
    }
  }
}

🛠️ 使用示例
1. 注册服务
   
在 Startup.cs 或 Program.cs 中注册 MQ 服务：

using Cjora.MQ;

public void ConfigureServices(IServiceCollection services)
{
    services.AddMq(Configuration); // 自动读取 appsettings.json 配置
    services.AddHostedService<MyMqHostedService>(); // 继承 MqHostedService 的后台服务
}

2. 创建后台服务

继承 MqHostedService 并实现消息处理逻辑：

using Cjora.MQ.Services;
using System.Threading;
using System.Threading.Tasks;

public class MyMqHostedService : MqHostedService
{
    public MyMqHostedService(IMq mq, ILogger<MyMqHostedService> logger, MqOptions mqOptions)
        : base(mq, logger, mqOptions)
    {
    }

    protected override async Task ProcessMessage(string topic, string msg, CancellationToken stoppingToken)
    {
        // 处理消息
        Console.WriteLine($"收到主题 {topic} 消息: {msg}");
        await Task.CompletedTask;
    }
}

3. 发布消息
public class MyService
{
    private readonly IMq _mq;

    public MyService(IMq mq)
    {
        _mq = mq;
    }

    public async Task SendMessageAsync()
    {
        await _mq.PublishAsync("topic1", new { Name = "Test", Value = 123 });
        await _mq.PublishAsync("topic2", "简单文本消息");
    }
}

🔧 高级特性
动态并发调整：根据队列积压自动调整并发量，提升吞吐量
批量消费：一次性处理多条消息，减少上下文切换
保持 JSON 原始大小写：内部序列化使用 DefaultContractResolver

💡 扩展 MQ 类型，只需：
增加 MqTypeEnum 新类型
实现 IMq 接口的类
在 MqSetup.AddMq 中注册
case MqTypeEnum.RabbitMq:
    services.AddSingleton<IMq, MqRabbit>();
    break;
    
⚡ 依赖注入
MqOptions 和 IMq 已经注册到 DI 容器中，可以直接注入：
public class DemoService
{
    private readonly IMq _mq;
    private readonly MqOptions _options;

    public DemoService(IMq mq, MqOptions options)
    {
        _mq = mq;
        _options = options;
    }
}

📄 License
MIT / Apache-2.0 双许可证，详见源码 LICENSE 文件。
