/// <summary>
/// 表示一次 Boss 阶段切换已经发生，UI 可据此给出阶段提示。
/// </summary>
public readonly struct BossPhaseChangedEvent
{
    public readonly Enemy boss;
    public readonly int phaseIndex;
    public readonly string phaseDisplayName;

    /// <summary>
    /// 创建一次 Boss 阶段切换事件。
    /// </summary>
    /// <param name="boss">触发切换的 Boss 实例。</param>
    /// <param name="phaseIndex">当前切换后的阶段索引。</param>
    /// <param name="phaseDisplayName">切换后建议展示的阶段名称。</param>
    public BossPhaseChangedEvent(Enemy boss, int phaseIndex, string phaseDisplayName)
    {
        this.boss = boss;
        this.phaseIndex = phaseIndex;
        this.phaseDisplayName = phaseDisplayName;
    }
}
