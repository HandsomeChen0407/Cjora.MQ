# 📦 Cjora.MQ

Cjora.MQ 是一个高性能、可扩展的 .NET 消息队列基础设施库，  
统一封装 Kafka / MQTT，提供标准化的 Consumer / Producer / Runtime 生命周期管理。

适用于：
- ASP.NET Core 后台服务
- 微服务消息通信
- MQTT ⇄ Kafka 消息桥接
- 高并发消息消费场景

---

## ✨ 特性

- 支持 Kafka / MQTT
- Consumer / Producer 职责完全拆分
- 多 Profile、多实例并存
- 内置 Channel 高性能缓冲
- 批量消费 + 动态并发
- 与 IHostedService 深度集成
- 统一 Runtime 管理生命周期

---

## 架构说明

```text

┌────────────┐
│ Host       │
└─────┬──────┘
      │
┌─────▼──────────┐
│ MqRuntime      │
│ 生命周期管理   │
└─────┬──────────┘
      │
 ┌────▼────┐   ┌────▼────┐
 │Consumer │   │Producer │
 └────┬────┘   └─────────┘
      │
 ┌────▼────────┐
 │ Channel     │
 └─────────────┘

```

---

## 📦 安装

```bash
dotnet add package Cjora.MQ
```

---

## 配置示例（多 Profile）

```json
{
  "MqOptions": {
    "Profiles": {
      "kafka-consumer": {
        "MqType": 2,
        "Role": 1,
        "ServiceIP": "127.0.0.1:9092",
        "SubTopic": "order.created,order.updated",
        "Kafka": {
          "GroupId": "order-service"
        }
      },
      "mqtt-producer": {
        "MqType": 1,
        "Role": 2,
        "ServiceIP": "127.0.0.1",
        "ServicePort": 1883,
        "Username": "user",
        "Password": "pass"
      }
    }
  }
}
```

---

## Program.cs 注册

```csharp

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMq(builder.Configuration);

// 注册你的业务消费 HostedService
builder.Services.AddHostedService<OrderConsumerService>();

var app = builder.Build();
app.Run();

```

---

## 创建消费后台服务

```csharp
using Cjora.MQ.Services;

public class OrderConsumerService : MqHostedService
{
    public OrderConsumerService(
        MqRuntime runtime,
        ILogger<OrderConsumerService> logger)
        : base(runtime, "kafka-consumer", logger)
    {
    }

    protected override Task ProcessMessage(
        string topic,
        string msg,
        CancellationToken stoppingToken)
    {
        Console.WriteLine($"[{topic}] {msg}");
        return Task.CompletedTask;
    }
}

```

---

## 发布消息示例

```csharp

using Cjora.MQ.Interfaces;

public class MessagePublisher
{
    private readonly IMqProducer _producer;

    public MessagePublisher(MqRuntime runtime)
    {
        _producer = runtime.GetProducer("mqtt-producer");
    }

    public Task SendAsync()
    {
        return _producer.PublishAsync(
            "device/status",
            new
            {
                DeviceId = "D001",
                Online = true
            });
    }
}

```