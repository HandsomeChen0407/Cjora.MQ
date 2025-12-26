using Cjora.MQ.Enums;
using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Cjora.MQ.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            switch (profileOptions.MqType)
            {
                case MqTypeEnum.Kafka:
                    services.AddSingleton<IMqClient>(sp =>
                        new MqKafka(
                            name,
                            profileOptions,
                            sp.GetRequiredService<ILogger<MqKafka>>()));
                    break;

                case MqTypeEnum.Mqtt:
                    services.AddSingleton<IMqClient>(sp =>
                        new MqMqtt(
                            name,
                            profileOptions,
                            sp.GetRequiredService<ILogger<MqMqtt>>()));
                    break;

                default:
                    throw new NotSupportedException(
                        $"MQ Profile [{name}] 使用了不支持的类型：{profileOptions.MqType}");
            }
        }

        // 注册 MQ Runtime（必须是 HostedService）
        services.AddHostedService<MqRuntime>();
    }
}
