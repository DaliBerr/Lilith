/// <summary>
/// 表示一次敌人死亡结果，供结算统计与战斗日志聚合。
/// </summary>
public readonly struct EnemyDiedEvent
{
    /// <summary>
    /// 触发本次死亡的敌人实例。
    /// </summary>
    public readonly Enemy enemy;

    /// <summary>
    /// 当前结算应展示的敌人名称。
    /// </summary>
    public readonly string displayName;

    /// <summary>
    /// 创建一次敌人死亡事件。
    /// </summary>
    /// <param name="enemy">触发本次死亡的敌人实例。</param>
    /// <param name="displayName">当前结算应展示的敌人名称。</param>
    public EnemyDiedEvent(Enemy enemy, string displayName)
    {
        this.enemy = enemy;
        this.displayName = displayName ?? string.Empty;
    }
}
