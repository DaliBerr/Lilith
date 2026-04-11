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
            if (other == null || !TryResolveFlowController())
            {
                return;
            }

            if (other.GetComponentInParent<PlayerPlaneMovement>() == null)
            {
                return;
            }

            if (!targetFlowController.TryEnterCombatRun())
            {
                GameDebug.LogWarning("[StartRoomBattleTeleporter] Combat run could not be started.");
            }
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
    }
}
