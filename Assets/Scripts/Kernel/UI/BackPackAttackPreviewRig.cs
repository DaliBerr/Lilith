using System;
using UnityEngine;

namespace Kernel.UI
{
    /// <summary>
    /// 为 BackPack Left Panel 预览提供一套可在 Prefab Mode 中直接调整的静态 rig。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackPackAttackPreviewRig : MonoBehaviour
    {
        private const string PreviewCameraPath = "PreviewCamera";
        private const string SpawnAnchorPath = "PreviewPlayer/SpawnAnchor";
        private const string ProjectileRootPath = "PreviewBullets";
        private const string PreviewPlayerRootPath = "PreviewPlayer";
        private const string PreviewDummyPath = "PreviewDummy";
        private const string PreviewDummyMiddlePath = "PreviewDummy-M";
        private const string ExplosionHintPath = "ExplosionHint";
        private const string FloorRootPath = "Floor";

        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform spawnAnchor;
        [SerializeField] private Transform projectileRoot;
        [SerializeField] private Transform previewPlayerRoot;
        [SerializeField] private BackPackPreviewDummyEnemy previewDummy;
        [SerializeField] private BackPackPreviewDummyEnemy[] previewDummies = Array.Empty<BackPackPreviewDummyEnemy>();
        [SerializeField] private LineRenderer explosionHint;
        [SerializeField] private Transform floorRoot;

        public Camera PreviewCamera => previewCamera;
        public Transform SpawnAnchor => spawnAnchor;
        public Transform ProjectileRoot => projectileRoot;
        public Transform PreviewPlayerRoot => previewPlayerRoot;
        public BackPackPreviewDummyEnemy PreviewDummy => previewDummy;
        public BackPackPreviewDummyEnemy[] PreviewDummies => previewDummies;
        public LineRenderer ExplosionHint => explosionHint;
        public Transform FloorRoot => floorRoot;

        /// <summary>
        /// summary: 对外暴露一次显式的自动绑定入口，方便运行时调试脚本在不做完整校验时也能拉起当前 prefab 的关键引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void AutoBindReferences()
        {
            TryAutoBindReferences();
        }

        private void Reset()
        {
            TryAutoBindReferences();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 校验当前 preview rig 是否具备运行时预览所需的全部关键引用。
        /// param: errorMessage 输出的首个缺失项说明
        /// returns: 所有关键引用都有效时返回 true
        /// </summary>
        public bool TryValidate(out string errorMessage)
        {
            TryAutoBindReferences();

            if (previewCamera == null)
            {
                errorMessage = "Preview unavailable: preview rig is missing PreviewCamera.";
                return false;
            }

            if (spawnAnchor == null)
            {
                errorMessage = "Preview unavailable: preview rig is missing SpawnAnchor.";
                return false;
            }

            if (projectileRoot == null)
            {
                errorMessage = "Preview unavailable: preview rig is missing PreviewBullets.";
                return false;
            }

            if (previewPlayerRoot == null)
            {
                errorMessage = "Preview unavailable: preview rig is missing PreviewPlayer.";
                return false;
            }

            if (previewDummies == null || previewDummies.Length == 0)
            {
                errorMessage = "Preview unavailable: preview rig is missing PreviewDummy.";
                return false;
            }

            if (previewDummy == null)
            {
                errorMessage = "Preview unavailable: preview rig could not resolve a primary PreviewDummy.";
                return false;
            }

            for (int i = 0; i < previewDummies.Length; i++)
            {
                BackPackPreviewDummyEnemy candidate = previewDummies[i];
                if (candidate == null)
                {
                    errorMessage = "Preview unavailable: preview rig contains a missing PreviewDummy reference.";
                    return false;
                }

                if (candidate.TargetCollider == null)
                {
                    errorMessage = $"Preview unavailable: preview dummy '{candidate.name}' is missing a target collider.";
                    return false;
                }
            }

            if (explosionHint == null)
            {
                errorMessage = "Preview unavailable: preview rig is missing ExplosionHint.";
                return false;
            }

            if (floorRoot == null)
            {
                errorMessage = "Preview unavailable: preview rig is missing Floor.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private void TryAutoBindReferences()
        {
            previewCamera ??= transform.Find(PreviewCameraPath)?.GetComponent<Camera>();
            spawnAnchor ??= transform.Find(SpawnAnchorPath);
            projectileRoot ??= transform.Find(ProjectileRootPath);
            previewPlayerRoot ??= transform.Find(PreviewPlayerRootPath);
            previewDummies = GetComponentsInChildren<BackPackPreviewDummyEnemy>(includeInactive: true);
            Array.Sort(previewDummies, CompareDummyOrder);
            previewDummy = ResolvePrimaryPreviewDummy();
            explosionHint ??= transform.Find(ExplosionHintPath)?.GetComponent<LineRenderer>();
            floorRoot ??= transform.Find(FloorRootPath);
        }

        private BackPackPreviewDummyEnemy ResolvePrimaryPreviewDummy()
        {
            if (previewDummies == null || previewDummies.Length == 0)
            {
                return null;
            }

            BackPackPreviewDummyEnemy exactMatch = transform.Find(PreviewDummyPath)?.GetComponent<BackPackPreviewDummyEnemy>();
            if (exactMatch != null)
            {
                return exactMatch;
            }

            BackPackPreviewDummyEnemy middleMatch = transform.Find(PreviewDummyMiddlePath)?.GetComponent<BackPackPreviewDummyEnemy>();
            if (middleMatch != null)
            {
                return middleMatch;
            }

            for (int i = 0; i < previewDummies.Length; i++)
            {
                BackPackPreviewDummyEnemy candidate = previewDummies[i];
                if (candidate != null && candidate.name.EndsWith("-M", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return previewDummies[0];
        }

        private static int CompareDummyOrder(BackPackPreviewDummyEnemy left, BackPackPreviewDummyEnemy right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
        }
    }
}
