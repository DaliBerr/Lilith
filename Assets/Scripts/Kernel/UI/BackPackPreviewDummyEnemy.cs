using System;
using UnityEngine;

namespace Kernel.UI
{
    /// <summary>
    /// 仅服务于 BackPack Left Panel 预览的假目标；负责承接真实子弹伤害并提供本地受击反馈。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackPackPreviewDummyEnemy : Enemy
    {
        private const int InvalidColorPropertyId = -1;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int FaceColorPropertyId = Shader.PropertyToID("_FaceColor");

        [Header("Bindings")]
        [SerializeField] private Collider targetCollider;
        [SerializeField] private Renderer[] targetRenderers;

        [Header("Feedback")]
        [SerializeField] private Color hitFlashColor = new(1f, 0.82f, 0.62f, 1f);
        [SerializeField, Min(0f)] private float hitScaleMultiplier = 1.08f;
        [SerializeField, Min(0.01f)] private float hitRecoverDuration = 0.18f;

        private Color[] baseColors = Array.Empty<Color>();
        private int[] baseColorPropertyIds = Array.Empty<int>();
        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseLocalScale = Vector3.one;
        private float maxHealth = 100f;
        private float currentHealth = 100f;
        private float hitRecoverTimer;
        private bool hasCapturedBaseVisualState;

        public override float MoveSpeed => 0f;
        public override float RotationSpeed => 0f;
        public override float StoppingDistance => 0f;
        public override float AttackRange => 0f;
        public override float AttackCooldown => 0f;
        public override float AttackDamage => 0f;
        public override float MaxHealth => maxHealth;
        public override float CurrentHealth => currentHealth;
        public Collider TargetCollider
        {
            get
            {
                EnsureBindings();
                return targetCollider;
            }
        }

        public new event Action<BackPackPreviewDummyEnemy> Damaged;

        /// <summary>
        /// summary: 返回预览目标当前可用于放置爆炸圈提示的世界锚点。
        /// param: 无
        /// returns: 优先使用目标碰撞体 bounds center；缺失时回退到自身位置
        /// </summary>
        public Vector3 ImpactAnchor
        {
            get
            {
                EnsureBindings();
                return targetCollider != null ? targetCollider.bounds.center : transform.position;
            }
        }

        private void Awake()
        {
            EnsureBindings();
            CaptureBaseVisualState();
            ResetPreviewState(maxHealth, gameObject.layer);
        }

        private void OnValidate()
        {
            EnsureBindings();
            CaptureBaseVisualState();
        }

        private void Update()
        {
            if (hitRecoverTimer <= 0f)
            {
                return;
            }

            hitRecoverTimer = Mathf.Max(0f, hitRecoverTimer - Time.unscaledDeltaTime);
            float progress = hitRecoverDuration > 0f ? 1f - (hitRecoverTimer / hitRecoverDuration) : 1f;
            ApplyInterpolatedVisualState(progress);
        }

        /// <summary>
        /// summary: 在每一轮新的预览发射开始前重置生命、图层与受击表现。
        /// param: previewHealth 当前预览轮次应使用的生命值
        /// param: targetLayer 当前轮次应命中的 layer
        /// returns: 无
        /// </summary>
        public void ResetPreviewState(float previewHealth, int targetLayer)
        {
            EnsureBindings();
            RestoreBaseVisualState();
            maxHealth = Mathf.Max(1f, previewHealth);
            currentHealth = maxHealth;
            SetLayerRecursively(transform, targetLayer);
            ResetDeathNotificationState();
        }

        /// <summary>
        /// summary: 对预览 Dummy 施加一次伤害；不会销毁对象，只会更新生命并触发本地受击反馈。
        /// param: damage 本次需要扣减的伤害值
        /// param: remainingHealth 输出的剩余生命
        /// param: isDead 输出的死亡状态
        /// returns: 当伤害被实际接受时返回 true
        /// </summary>
        public override bool TryApplyDamage(float damage, out float remainingHealth, out bool isDead)
        {
            if (damage <= 0f || currentHealth <= 0f)
            {
                remainingHealth = currentHealth;
                isDead = currentHealth <= 0f;
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            remainingHealth = currentHealth;
            isDead = currentHealth <= 0f;

            TriggerHitFeedback();
            if (isDead)
            {
                TryNotifyDied();
            }

            Damaged?.Invoke(this);
            return true;
        }

        private void EnsureBindings()
        {
            propertyBlock ??= new MaterialPropertyBlock();
            targetCollider ??= GetComponent<Collider>();
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }
        }

        private void CaptureBaseVisualState()
        {
            baseLocalScale = transform.localScale;
            hasCapturedBaseVisualState = true;
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                baseColors = Array.Empty<Color>();
                baseColorPropertyIds = Array.Empty<int>();
                return;
            }

            if (baseColors.Length != targetRenderers.Length)
            {
                baseColors = new Color[targetRenderers.Length];
            }

            if (baseColorPropertyIds.Length != targetRenderers.Length)
            {
                baseColorPropertyIds = new int[targetRenderers.Length];
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                int colorPropertyId = ResolveColorPropertyId(targetRenderer);
                baseColorPropertyIds[i] = colorPropertyId;
                baseColors[i] = TryGetRendererColor(targetRenderer, colorPropertyId, out Color baseColor)
                    ? baseColor
                    : Color.white;
            }
        }

        private void RestoreBaseVisualState()
        {
            if (!hasCapturedBaseVisualState)
            {
                CaptureBaseVisualState();
            }

            hitRecoverTimer = 0f;
            ApplyInterpolatedVisualState(1f);
        }

        private void TriggerHitFeedback()
        {
            hitRecoverTimer = hitRecoverDuration;
            ApplyInterpolatedVisualState(0f);
        }

        private void ApplyInterpolatedVisualState(float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            transform.localScale = Vector3.Lerp(baseLocalScale * hitScaleMultiplier, baseLocalScale, clampedProgress);

            if (targetRenderers == null)
            {
                return;
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                Color baseColor = i < baseColors.Length ? baseColors[i] : Color.white;
                int colorPropertyId = i < baseColorPropertyIds.Length ? baseColorPropertyIds[i] : InvalidColorPropertyId;
                if (colorPropertyId == InvalidColorPropertyId)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(colorPropertyId, Color.Lerp(hitFlashColor, baseColor, clampedProgress));
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static int ResolveColorPropertyId(Renderer targetRenderer)
        {
            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            {
                return InvalidColorPropertyId;
            }

            Material sharedMaterial = targetRenderer.sharedMaterial;
            if (sharedMaterial.HasProperty(ColorPropertyId))
            {
                return ColorPropertyId;
            }

            if (sharedMaterial.HasProperty(BaseColorPropertyId))
            {
                return BaseColorPropertyId;
            }

            if (sharedMaterial.HasProperty(FaceColorPropertyId))
            {
                return FaceColorPropertyId;
            }

            return InvalidColorPropertyId;
        }

        private static bool TryGetRendererColor(Renderer targetRenderer, int colorPropertyId, out Color color)
        {
            color = Color.white;
            if (targetRenderer == null || targetRenderer.sharedMaterial == null || colorPropertyId == InvalidColorPropertyId)
            {
                return false;
            }

            Material sharedMaterial = targetRenderer.sharedMaterial;
            if (!sharedMaterial.HasProperty(colorPropertyId))
            {
                return false;
            }

            color = sharedMaterial.GetColor(colorPropertyId);
            return true;
        }

        private static void SetLayerRecursively(Transform root, int targetLayer)
        {
            if (root == null)
            {
                return;
            }

            root.gameObject.layer = targetLayer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), targetLayer);
            }
        }
    }
}
