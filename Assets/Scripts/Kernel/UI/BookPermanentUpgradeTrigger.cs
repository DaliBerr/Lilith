using System.Collections.Generic;
using UnityEngine;

namespace Kernel.UI
{
    /// <summary>
    /// 挂在 StartRoom 的 Book 触发器上，用于在玩家进入交互区后允许通过 Interaction 按键切换永久升级 Screen。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BookPermanentUpgradeTrigger : MonoBehaviour
    {
        private readonly HashSet<Transform> activePlayerRoots = new();

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

        /// <summary>
        /// summary: 玩家进入 Book trigger 时请求打开永久升级界面；非玩家碰撞体会被忽略。
        /// param name="other": 当前进入 trigger 的碰撞体
        /// returns: 无
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            TryHandlePlayerEnter(other);
        }

        /// <summary>
        /// summary: 当 Book 上存在非 Trigger 碰撞体时，仍允许通过碰撞事件作为进入交互区的兜底入口。
        /// param name="collision": 当前碰撞事件
        /// returns: 无
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            TryHandlePlayerEnter(collision.collider);
        }

        /// <summary>
        /// summary: 玩家离开 Book trigger 时清理进入记录，离开后 Interaction 不再触发升级界面。
        /// param name="other": 当前离开 trigger 的碰撞体
        /// returns: 无
        /// </summary>
        private void OnTriggerExit(Collider other)
        {
            TryHandlePlayerExit(other);
        }

        /// <summary>
        /// summary: 碰撞态离开时同步清理玩家进入记录，避免仅走碰撞回调时无法重新触发。
        /// param name="collision": 当前碰撞退出事件
        /// returns: 无
        /// </summary>
        private void OnCollisionExit(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            TryHandlePlayerExit(collision.collider);
        }

        private void OnDisable()
        {
            UIInputRouter router = UIInputRouter.Instance;
            if (router != null)
            {
                foreach (Transform playerRoot in activePlayerRoots)
                {
                    if (playerRoot != null)
                    {
                        router.UnregisterPermanentUpgradeInteractor(playerRoot);
                    }
                }
            }

            activePlayerRoots.Clear();
        }

        /// <summary>
        /// summary: 统一处理玩家进入 Book 范围时的交互资格注册，避免 Trigger 与 Collision 路径重复实现。
        /// param name="other": 当前进入范围的碰撞体
        /// returns: 无
        /// </summary>
        private void TryHandlePlayerEnter(Collider other)
        {
            if (!TryResolvePlayerRoot(other, out Transform playerRoot))
            {
                return;
            }

            if (!activePlayerRoots.Add(playerRoot))
            {
                return;
            }

            UIInputRouter.Instance?.RegisterPermanentUpgradeInteractor(playerRoot);
        }

        /// <summary>
        /// summary: 统一处理玩家离开 Book 范围时的交互资格清理。
        /// param name="other": 当前离开范围的碰撞体
        /// returns: 无
        /// </summary>
        private void TryHandlePlayerExit(Collider other)
        {
            if (!TryResolvePlayerRoot(other, out Transform playerRoot))
            {
                return;
            }

            if (!activePlayerRoots.Remove(playerRoot))
            {
                return;
            }

            UIInputRouter.Instance?.UnregisterPermanentUpgradeInteractor(playerRoot);
        }

        /// <summary>
        /// summary: 保证该对象上至少存在一个 Trigger Collider，避免场景接线遗漏导致回调不触发。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureTriggerCollider()
        {
            Collider[] colliders = GetComponents<Collider>();
            if (colliders == null || colliders.Length == 0)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    return;
                }
            }

            if (colliders[0] != null)
            {
                colliders[0].isTrigger = true;
            }
        }

        private static bool TryResolvePlayerRoot(Collider other, out Transform playerRoot)
        {
            playerRoot = null;
            if (other == null)
            {
                return false;
            }

            PlayerPlaneMovement playerMovement = other.GetComponentInParent<PlayerPlaneMovement>();
            if (playerMovement == null)
            {
                return false;
            }

            playerRoot = playerMovement.transform.root;
            return playerRoot != null;
        }
    }
}
