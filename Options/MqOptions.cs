namespace Cjora.MQ.Options;

/// <summary>
/// MQ 总配置
///
/// 设计说明：
/// - Profiles 用于支持多 MQ 并存
/// - 单 MQ 场景仍可使用原字段
/// </summary>
public sealed class MqOptions
{
    /// <summary>
    /// 多 MQ 配置集合
    /// Key = MQ 名称（mqtt / kafka）
    /// </summary>
    public Dictionary<string, MqProfileOptions> Profiles { get; set; }
        = new();
}