namespace Cjora.MQ.Enums;

/// <summary>
/// 消息队列角色枚举
/// </summary>
public enum MqRoleEnum
{
    /// <summary>
    /// 未知角色，通常抛出异常或提示用户配置错误
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 消费者角色
    /// </summary>
    Consumer = 1,

    /// <summary>
    /// 发布者角色
    /// </summary>
    Producer = 2
}