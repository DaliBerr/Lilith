using Kernel.Quest;
using UnityEngine;
using Vocalith.Logging;

namespace Kernel.MapGrid
{
    /// <summary>
    /// 起始房间中的战斗传送装置；玩家进入触发区后会请求进入新一局战斗。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartRoomBattleTeleporter : MonoBehaviour
    {
        [SerializeField] private MapRunFlowController targetFlowController;

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerTrigger(other))
            {
                return;
            }

            if (!IsTeleporterUnlocked())
            {
                return;
            }

            if (!TryResolveFlowController())
            {
                return;
            }

            if (!targetFlowController.TryEnterCombatRun())
            {
                GameDebug.LogWarning("[StartRoomBattleTeleporter] Combat run could not be started.");
                return;
            }

            MarkTeleporterTriggered();
            targetFlowController.TryGrantTutorialEntryTokenAfterTeleporterTriggered();
        }

        /// <summary>
        /// summary: 当 Inspector 未显式绑定 flow controller 时，自动解析场景中的 MapRunFlowController。
        /// param: 无
        /// returns: 成功拿到 flow controller 时返回 true
        /// </summary>
        private bool TryResolveFlowController()
        {
            if (targetFlowController != null)
            {
                return true;
            }

            targetFlowController = FindFirstObjectByType<MapRunFlowController>();
            return targetFlowController != null;
        }

        private void EnsureTriggerCollider()
        {
            if (!TryGetComponent(out Collider triggerCollider))
            {
                return;
            }

            if (!triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// summary: 判断当前进入 trigger 的 collider 是否属于玩家根节点。
        /// param name="other": 当前进入触发区的 collider
        /// returns: 当前 collider 来自玩家时返回 true
        /// </summary>
        private static bool IsPlayerTrigger(Collider other)
        {
            return other != null && other.GetComponentInParent<PlayerPlaneMovement>() != null;
        }

        /// <summary>
        /// summary: 判断当前存档是否已经永久解锁起始房间传送装置。
        /// param: 无
        /// returns: 当前 profile 已记录解锁标记时返回 true
        /// </summary>
        private static bool IsTeleporterUnlocked()
        {
            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            return saveService != null && saveService.HasStoryFlag(TutorialQuestConstants.TeleporterUnlockedFlagId);
        }

        /// <summary>
        /// summary: 在玩家成功触发战斗传送后记录一次永久剧情标记，供任务链和后续逻辑复用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private static void MarkTeleporterTriggered()
        {
            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            saveService?.SetStoryFlag(TutorialQuestConstants.TeleporterTriggeredFlagId, true);
        }
    }
}
