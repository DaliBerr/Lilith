/// <summary>
/// 表示一次对局胜利结果，供结算流程统一消费。
/// </summary>
public readonly struct CombatVictoryEvent
{
    /// <summary>
    /// 触发本次胜利的波次管理器。
    /// </summary>
    public readonly WaveManager source;

    /// <summary>
    /// 当前对局已完成的波次数。
    /// </summary>
    public readonly int completedWaveCount;

    /// <summary>
    /// 创建一次对局胜利事件。
    /// </summary>
    /// <param name="source">触发本次胜利的波次管理器。</param>
    /// <param name="completedWaveCount">当前对局已完成的波次数。</param>
    public CombatVictoryEvent(WaveManager source, int completedWaveCount)
    {
        this.source = source;
        this.completedWaveCount = completedWaveCount < 0 ? 0 : completedWaveCount;
    }
}
