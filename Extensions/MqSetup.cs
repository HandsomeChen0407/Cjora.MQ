using Cjora.MQ.Enums;
using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Cjora.MQ.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cjora.MQ;

/// <summary>
/// MQ 服务注册扩展
///
/// 设计说明：
/// - 一个 Profile = 一个 MQ Client 实例
/// - 支持 Kafka / MQTT 混合部署
/// - 所有 Client 由 MqRuntime 统一托管生命周期
/// </summary>
public static class MqSetup
{
    public static void AddMq(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("MqOptions");
        var options = section.Get<MqOptions>()
            ?? throw new ArgumentNullException("MqOptions 配置不能为空");

        services.Configure<MqOptions>(section);

        // 注册每一个 MQ Profile
        foreach (var profile in options.Profiles)
        {
            var name = profile.Key;
            var profileOptions = profile.Value;

            // 字符串 -> 枚举转换
            if (!Enum.TryParse<MqTypeEnum>(profileOptions.MqType, true, out var mqType))
                mqType = MqTypeEnum.Unknown;

            if (!Enum.TryParse<MqRoleEnum>(profileOptions.Role, true, out var role))
                role = MqRoleEnum.Unknown;

            switch (mqType)
            {
                case MqTypeEnum.Kafka when role == MqRoleEnum.Consumer:
                    services.AddSingleton<IMqClient>(sp =>
                        new KafkaConsumerClient(name, profileOptions,
                            sp.GetRequiredService<ILogger<KafkaConsumerClient>>()));
                    break;

                case MqTypeEnum.Kafka when role == MqRoleEnum.Producer:
                    services.AddSingleton<IMqClient>(sp =>
                        new KafkaProducerClient(name, profileOptions,
                            sp.GetRequiredService<ILogger<KafkaProducerClient>>()));
                    break;

                case MqTypeEnum.Mqtt when role == MqRoleEnum.Consumer:
                    services.AddSingleton<IMqClient>(sp =>
                        new MqttConsumerClient(name, profileOptions,
                            sp.GetRequiredService<ILogger<MqttConsumerClient>>()));
                    break;

                case MqTypeEnum.Mqtt when role == MqRoleEnum.Producer:
                    services.AddSingleton<IMqClient>(sp =>
                        new MqttProducerClient(name, profileOptions,
                            sp.GetRequiredService<ILogger<MqttProducerClient>>()));
                    break;

                default:
                    throw new NotSupportedException(
                        $"MQ Profile [{name}] 使用了不支持的类型：{profileOptions.MqType}，角色：{profileOptions.Role}");
            }
        }

        // 注册 Runtime 本体（给业务用）
        services.AddSingleton<MqRuntime>();

        // 再把同一个实例注册为 HostedService（给 Host 用）
        services.AddSingleton<IHostedService>(sp =>
            sp.GetRequiredService<MqRuntime>());

    }
}
