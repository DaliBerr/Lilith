using UnityEngine;

/// <summary>
/// 暂存一次击杀来源给予敌人的掉落概率倍率，供死亡掉落监听同步读取。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyDropBonusController : MonoBehaviour
{
    private float pendingDropChanceMultiplier = 1f;

    public float PendingDropChanceMultiplier => pendingDropChanceMultiplier;

    public static void RegisterDropChanceMultiplier(Enemy enemy, float multiplier)
    {
        if (enemy == null || multiplier <= 1f)
        {
            return;
        }

        EnemyDropBonusController controller = enemy.GetComponent<EnemyDropBonusController>();
        if (controller == null)
        {
            controller = enemy.gameObject.AddComponent<EnemyDropBonusController>();
        }

        controller.pendingDropChanceMultiplier = Mathf.Max(
            controller.pendingDropChanceMultiplier,
            multiplier);
    }

    public static void ClearDropChanceMultiplier(Enemy enemy)
    {
        EnemyDropBonusController controller = enemy != null
            ? enemy.GetComponent<EnemyDropBonusController>()
            : null;
        if (controller == null)
        {
            return;
        }

        controller.pendingDropChanceMultiplier = 1f;
    }

    public static float ConsumeDropChanceMultiplier(Enemy enemy)
    {
        EnemyDropBonusController controller = enemy != null
            ? enemy.GetComponent<EnemyDropBonusController>()
            : null;
        if (controller == null)
        {
            return 1f;
        }

        float multiplier = Mathf.Max(1f, controller.pendingDropChanceMultiplier);
        controller.pendingDropChanceMultiplier = 1f;
        return multiplier;
    }
}
