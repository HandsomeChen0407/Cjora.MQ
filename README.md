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
        "MqType": "Kafka",
        "Role": "Consumer",
        "ServiceIP": "127.0.0.1:9092",
        "SubTopic": "order.created,order.updated",
        "Kafka": {
          "GroupId": "order-service"
        }
      },
      "mqtt-producer": {
        "MqType": "Mqtt",
        "Role": "Producer",
        "ServiceIP": "127.0.0.1",
        "ServicePort": 1883,
        "Username": "user",
        "Password": "pass",
        "Mqtt": {
          "KeepAliveSeconds": 90,
          "ProtocolVersion": "V500",
          "TraceIdPropertyName": "$__messageId"
        }
      },
      "mqtt-consumer": {
        "MqType": "Mqtt",
        "Role": "Consumer",
        "ServiceIP": "127.0.0.1",
        "ServicePort": 1883,
        "Username": "user",
        "Password": "pass",
        "SubTopic": "device/status",
        "Mqtt": {
          "KeepAliveSeconds": 90,
          "ProtocolVersion": "V500"
        }
      }
    }
  }
}
```

---

## 配置说明

### MQTT 配置项

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `KeepAliveSeconds` | `int` | `90` | MQTT KeepAlive 时间（秒），客户端每隔此时间向 Broker 发送心跳 |
| `ProtocolVersion` | `string` | `"V500"` | MQTT 协议版本，可选值：`V310`、`V311`、`V500` |
| `TraceIdPropertyName` | `string?` | `null` | TraceId 属性名称，用于从 MQTT 发布结果的 UserProperties 中提取 TraceId。例如：腾讯云 MQTT 使用 `"$__messageId"`。如果为空或 null，则不提取 TraceId |

**示例：**
- 腾讯云 MQTT：`"TraceIdPropertyName": "$__messageId"`
- 标准 MQTT：不配置此字段或设置为 `null`

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