/// <summary>
/// 表示一次 Boss 遭遇已结束，Boss UI 可以据此隐藏。
/// </summary>
public readonly struct BossEncounterEndedEvent
{
    public readonly Enemy boss;
    public readonly string displayName;
    public readonly bool endedByDeath;

    /// <summary>
    /// 创建一次 Boss 遭遇结束事件。
    /// </summary>
    /// <param name="boss">本次结束的 Boss 实例。</param>
    /// <param name="displayName">Boss UI 当前展示的名称。</param>
    /// <param name="endedByDeath">是否由 Boss 死亡触发结束。</param>
    public BossEncounterEndedEvent(Enemy boss, string displayName, bool endedByDeath)
    {
        this.boss = boss;
        this.displayName = displayName;
        this.endedByDeath = endedByDeath;
    }
}
