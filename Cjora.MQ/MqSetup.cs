using Cjora.MQ.Enums;
using Cjora.MQ.Interfaces;
using Cjora.MQ.Options;
using Cjora.MQ.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cjora.MQ
{
    /// <summary>
    /// 消息队列服务注册扩展
    /// </summary>
    public static class MqSetup
    {
        /// <summary>
        /// 注册 MQ 服务到依赖注入容器
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">应用程序配置</param>
        public static void AddMq(this IServiceCollection services, MqOptions mqOptions)
        {
            if (mqOptions == null)
                throw new ArgumentNullException("MqOptions 配置不能为空");

            // 根据 MQ 类型注册具体实现
            switch (mqOptions.MqType)
            {
                case MqTypeEnum.Mqtt:
                    services.AddSingleton<IMq, MqMqtt>();
                    break;
                case MqTypeEnum.Kafka:
                    services.AddSingleton<IMq, MqKafka>();
                    break;
                default:
                    throw new NotSupportedException($"不支持的 MQ 类型：{mqOptions.MqType}");
            }
        }
    }
}
