# Cjora.MQ

Cjora.MQ 是一个高性能、可扩展的 .NET 消息队列封装库，支持 **MQTT 和 Kafka**，提供统一接口 **IMq**，可轻松集成到 ASP.NET Core 或任意 .NET 项目中。

---

## 功能

- 支持 **MQTT / Kafka** 的统一操作接口  
- 内置异步消息通道，支持批量消费  
- 支持动态并发调整，队列积压自适应  
- JSON 消息序列化保持原始大小写  
- 与 **依赖注入** 无缝集成  

---

## 安装

```bash
dotnet add package Cjora.MQ
```

---

## 配置实例

```json
{
  "MqOptions": {
    "MqType": "1",
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
```

---

## Program.cs 示例

```csharp

using Cjora.MQ;

var builder = WebApplication.CreateBuilder(args);

// 注册 MQ 服务
builder.Services.AddMq(builder.Configuration);
builder.Services.AddHostedService<MyMqHostedService>();

var app = builder.Build();

app.MapGet("/send", async (MyService service) =>
{
    await service.SendMessageAsync();
    return Results.Ok("消息已发送");
});

app.Run();

```

---

## 创建后台服务示例

```csharp

using Cjora.MQ.Services;
using System.Threading;
using System.Threading.Tasks;

public class MyMqHostedService : MqHostedService
{
    public MyMqHostedService(IMq mq, ILogger<MyMqHostedService> logger, IOptions<MqOptions> mqOptions)
        : base(mq, logger, mqOptions)
    {
    }

    protected override async Task ProcessMessage(string topic, string msg, CancellationToken stoppingToken)
    {
        Console.WriteLine($"收到主题 {topic} 消息: {msg}");
        await Task.CompletedTask;
    }
}

```

---

## 发布消息示例

```csharp

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

```