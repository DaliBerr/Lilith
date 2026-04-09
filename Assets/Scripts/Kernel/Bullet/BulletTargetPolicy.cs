namespace Kernel.Bullet
{
    /// <summary>
    /// 决定一发文字子弹允许对哪些阵营结算命中。
    /// </summary>
    public enum BulletTargetPolicy
    {
        EnemiesOnly = 0,
        PlayerOnly = 1,
        Both = 2,
    }
}
