using MQTTnet;
using MQTTnet.Formatter;

namespace Cjora.MQ.Services.Mqtt;

/// <summary>
/// MQTT 辅助工具类
/// 提供协议版本转换、TraceId 提取等工具方法
/// </summary>
public static class MqttHelper
{
    /// <summary>
    /// 将字符串协议版本转换为 MqttProtocolVersion 枚举
    /// </summary>
    /// <param name="protocolVersion">协议版本字符串（如 "V500", "V311"）</param>
    /// <returns>MqttProtocolVersion 枚举值，如果无法识别则返回 V500</returns>
    public static MqttProtocolVersion ParseProtocolVersion(string? protocolVersion)
    {
        if (string.IsNullOrWhiteSpace(protocolVersion))
        {
            return MqttProtocolVersion.V500;
        }

        return protocolVersion.ToUpperInvariant() switch
        {
            "V500" => MqttProtocolVersion.V500,
            "V311" => MqttProtocolVersion.V311,
            "V310" => MqttProtocolVersion.V310,
            _ => MqttProtocolVersion.V500
        };
    }

    /// <summary>
    /// 从 MQTT 发布结果中提取 TraceId（消息 ID）
    /// </summary>
    /// <param name="publishResult">MQTT 发布结果</param>
    /// <param name="traceIdPropertyName">TraceId 属性名称，如果为空或 null 则不提取</param>
    /// <returns>TraceId，如果不存在或未配置则返回 null</returns>
    public static string? ExtractTraceId(MqttClientPublishResult? publishResult, string? traceIdPropertyName)
    {
        if (string.IsNullOrWhiteSpace(traceIdPropertyName))
        {
            return null;
        }

        if (publishResult?.UserProperties == null)
        {
            return null;
        }

        return publishResult.UserProperties
            .FirstOrDefault(p => p.Name == traceIdPropertyName)?
            .Value;
    }
}
