/// <summary>
/// 表示一次会进入结算 Harvest 的长期收益拾取结果。
/// </summary>
public readonly struct RunRewardCollectedEvent
{
    /// <summary>
    /// 当前结算应展示的收益名称。
    /// </summary>
    public readonly string displayName;

    /// <summary>
    /// 本次拾取带来的数量。
    /// </summary>
    public readonly int count;

    /// <summary>
    /// 创建一次对局收益拾取事件。
    /// </summary>
    /// <param name="displayName">当前结算应展示的收益名称。</param>
    /// <param name="count">本次拾取带来的数量。</param>
    public RunRewardCollectedEvent(string displayName, int count)
    {
        this.displayName = displayName ?? string.Empty;
        this.count = count < 0 ? 0 : count;
    }
}
