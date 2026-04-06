using UnityEngine;

namespace Kernel
{
    /// <summary>
    /// 提供统一的世界平面高度换算，供 grounded、floating 与射线投影逻辑复用。
    /// </summary>
    public static class WorldHeightUtility
    {
        /// <summary>
        /// summary: 计算一个 grounded 根节点在目标地图平面上应该使用的世界 Y 值。
        /// param: root 需要被抬高到地面的根节点
        /// param: referenceCollider 用于确定落地底边的参考 Collider
        /// param: planeY 目标地图平面的世界 Y
        /// returns: 让参考 Collider 底边贴住地图平面后的根节点世界 Y
        /// </summary>
        public static float CalculateGroundedRootY(Transform root, Collider referenceCollider, float planeY)
        {
            if (root == null || referenceCollider == null)
            {
                return planeY;
            }

            float bottomOffsetFromRoot = referenceCollider.bounds.min.y - root.position.y;
            return planeY - bottomOffsetFromRoot;
        }

        /// <summary>
        /// summary: 计算 grounded 根节点在目标地图平面上的完整世界坐标。
        /// param: root 需要被抬高到地面的根节点
        /// param: referenceCollider 用于确定落地底边的参考 Collider
        /// param: planeY 目标地图平面的世界 Y
        /// param: groundedPosition 输出的 grounded 根节点世界坐标
        /// returns: 成功拿到根节点和参考 Collider 时返回 true
        /// </summary>
        public static bool TryGetGroundedRootPosition(
            Transform root,
            Collider referenceCollider,
            float planeY,
            out Vector3 groundedPosition)
        {
            groundedPosition = default;
            if (root == null || referenceCollider == null)
            {
                return false;
            }

            groundedPosition = root.position;
            groundedPosition.y = CalculateGroundedRootY(root, referenceCollider, planeY);
            return true;
        }

        /// <summary>
        /// summary: 直接把 grounded 根节点抬到目标地图平面上。
        /// param: root 需要被抬高到地面的根节点
        /// param: referenceCollider 用于确定落地底边的参考 Collider
        /// param: planeY 目标地图平面的世界 Y
        /// returns: 成功完成 grounded snap 时返回 true
        /// </summary>
        public static bool TrySnapGroundedRoot(Transform root, Collider referenceCollider, float planeY)
        {
            if (!TryGetGroundedRootPosition(root, referenceCollider, planeY, out Vector3 groundedPosition))
            {
                return false;
            }

            root.position = groundedPosition;
            return true;
        }

        /// <summary>
        /// summary: 把当前位置投影到指定地图平面高度，并叠加一个额外的世界 Y 偏移。
        /// param: currentPosition 当前世界坐标
        /// param: planeY 目标地图平面的世界 Y
        /// param: offsetY 额外叠加的世界 Y 偏移
        /// returns: 调整后的世界坐标
        /// </summary>
        public static Vector3 GetPositionAtPlaneHeight(Vector3 currentPosition, float planeY, float offsetY = 0f)
        {
            currentPosition.y = planeY + offsetY;
            return currentPosition;
        }

        /// <summary>
        /// summary: 直接把一个 Transform 放到指定地图平面高度，并叠加一个额外的世界 Y 偏移。
        /// param: target 需要被调整高度的 Transform
        /// param: planeY 目标地图平面的世界 Y
        /// param: offsetY 额外叠加的世界 Y 偏移
        /// returns: 成功完成高度投影时返回 true
        /// </summary>
        public static bool TrySnapTransformToPlaneHeight(Transform target, float planeY, float offsetY = 0f)
        {
            if (target == null)
            {
                return false;
            }

            target.position = GetPositionAtPlaneHeight(target.position, planeY, offsetY);
            return true;
        }

        /// <summary>
        /// summary: 用给定的世界 Y 平面去投影一条射线，供鼠标瞄准和回退平面命中使用。
        /// param: ray 当前需要投影的世界射线
        /// param: planeY 目标地图平面的世界 Y
        /// param: worldPoint 输出的命中世界坐标
        /// returns: 射线命中该平面时返回 true
        /// </summary>
        public static bool TryProjectRayOntoPlaneY(Ray ray, float planeY, out Vector3 worldPoint)
        {
            worldPoint = default;
            Plane plane = new(Vector3.up, new Vector3(0f, planeY, 0f));
            if (!plane.Raycast(ray, out float hitDistance))
            {
                return false;
            }

            worldPoint = ray.GetPoint(hitDistance);
            return true;
        }

        /// <summary>
        /// summary: 在给定组件层级内解析一个适合作为 grounded 参考的 Collider。
        /// param: owner 当前 grounded 对象上的任意组件
        /// param: referenceCollider 输出的 grounded 参考 Collider
        /// returns: 成功找到非 Trigger 的参考 Collider 时返回 true
        /// </summary>
        public static bool TryFindGroundingReferenceCollider(Component owner, out Collider referenceCollider)
        {
            referenceCollider = null;
            if (owner == null)
            {
                return false;
            }

            referenceCollider = owner.GetComponent<Collider>();
            if (IsUsableGroundingCollider(referenceCollider))
            {
                return true;
            }

            Collider[] colliders = owner.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (!IsUsableGroundingCollider(candidate))
                {
                    continue;
                }

                referenceCollider = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// summary: 统一规范 grounded 动态刚体的基础物理配置，避免重力和 Y 向位移破坏平面契约。
        /// param: target 需要被规范化的 Rigidbody
        /// returns: 成功拿到刚体并完成配置时返回 true
        /// </summary>
        public static bool TryConfigureGroundedRigidbody(Rigidbody target)
        {
            if (target == null)
            {
                return false;
            }

            target.useGravity = false;
            target.constraints |= RigidbodyConstraints.FreezePositionY;
            return true;
        }

        private static bool IsUsableGroundingCollider(Collider candidate)
        {
            return candidate != null &&
                   candidate.transform != null &&
                   !candidate.isTrigger;
        }
    }
}
