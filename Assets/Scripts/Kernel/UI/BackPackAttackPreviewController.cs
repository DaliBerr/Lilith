using System.Collections.Generic;
using Kernel.Bullet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 驱动 BackPack Left Panel 的离屏攻击预览，直接复用 Main 场景里的静态 preview rig。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BackPackAttackPreviewController : MonoBehaviour
    {
        private const string DefaultStatusMessage = "Preview Targets: Dummy Formation";
        private const string LegacyPreviewRootPath = "MainContent/Left Panel/Preview Animation";
        private const string CurrentPreviewRootPath = "Grids Preview Panel/Left Panel/Preview Animation";
        private const int ExplosionSegmentCount = 48;

        [Header("UI")]
        [SerializeField] private RectTransform previewRoot;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Preview Rig")]
        [SerializeField, Min(0.1f)] private float previewLoopInterval = 0.75f;
        [SerializeField, Min(64)] private int renderTextureSize = 512;

        [Header("Explosion Hint")]
        [SerializeField] private Color explosionHintColor = new(1f, 0.65f, 0.2f, 0.9f);
        [SerializeField, Min(0.05f)] private float explosionHintDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float explosionHintWidth = 0.18f;

        private readonly List<CharBullet> activePreviewBullets = new();
        private readonly List<BackPackPreviewDummyEnemy> previewDummies = new();

        private PlayerPlaneMovement currentPlayer;
        private CompiledAttack currentCompiledAttack;
        private CharBullet currentBulletPrefab;
        private RenderTexture previewTexture;
        private BackPackAttackPreviewRig previewRig;
        private Camera previewCamera;
        private BackPackPreviewDummyEnemy previewDummy;
        private LineRenderer explosionHint;
        private float nextPreviewLoopTime = float.PositiveInfinity;
        private float explosionHintHideTime = float.NegativeInfinity;
        private bool hasShownExplosionThisCycle;

        private void Awake()
        {
            TryAutoBindReferences();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        private void OnDestroy()
        {
            ClearPreview();
        }

        private void Update()
        {
            UpdateExplosionHint();
            if (!Application.isPlaying || !CanLoopPreview())
            {
                return;
            }

            if (Time.unscaledTime < nextPreviewLoopTime)
            {
                return;
            }

            RunPreviewCycle();
            nextPreviewLoopTime = Time.unscaledTime + previewLoopInterval;
        }

        /// <summary>
        /// summary: 基于当前玩家使用的真实子弹 prefab 与已编译攻击，刷新一次 Left Panel 预览。
        /// param: player 当前背包界面绑定的玩家对象
        /// param: compiledAttack 当前 Spell Book 对应的编译结果
        /// returns: 无
        /// </summary>
        public void RefreshPreview(PlayerPlaneMovement player, CompiledAttack compiledAttack)
        {
            currentPlayer = player;
            currentCompiledAttack = compiledAttack;
            currentBulletPrefab = player != null ? player.BulletPrefab : null;
            nextPreviewLoopTime = float.PositiveInfinity;

            TryAutoBindReferences();
            if (previewRoot == null || previewImage == null || statusLabel == null)
            {
                ApplyStatusMessage("Preview unavailable: preview UI binding is missing.");
                ApplyPreviewTexture(null);
                return;
            }

            EnsurePreviewTexture();
            ApplyPreviewTexture(previewTexture);

            if (!TryEnsurePreviewRig(out string rigStatus))
            {
                ApplyStatusMessage(rigStatus);
                ClearActivePreviewBullets();
                ReleasePreviewTexture();
                ApplyPreviewTexture(null);
                return;
            }

            PreparePreviewSurface();
            if (!TryResolvePreviewStatus(out string statusMessage))
            {
                ApplyStatusMessage(statusMessage);
                ClearActivePreviewBullets();
                RenderPreviewIfNeeded();
                return;
            }

            ApplyStatusMessage(DefaultStatusMessage);
            RunPreviewCycle();

            if (Application.isPlaying)
            {
                nextPreviewLoopTime = Time.unscaledTime + previewLoopInterval;
            }
        }

        /// <summary>
        /// summary: 停止当前离屏预览并清理 RenderTexture、预览相机输出与残留子弹。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void ClearPreview()
        {
            nextPreviewLoopTime = float.PositiveInfinity;
            currentPlayer = null;
            currentCompiledAttack = null;
            currentBulletPrefab = null;

            ClearActivePreviewBullets();
            ClearExplosionHintImmediate();
            ReleasePreviewCamera();
            ReleasePreviewTexture();
            ApplyPreviewTexture(null);
            ApplyStatusMessage(string.Empty);
            UnbindRig();
        }

        private bool CanLoopPreview()
        {
            return currentPlayer != null &&
                   currentBulletPrefab != null &&
                   currentCompiledAttack != null &&
                   currentCompiledAttack.CanFire &&
                   previewRig != null &&
                   previewCamera != null &&
                    previewDummy != null &&
                   previewDummies.Count > 0 &&
                   previewRig.ProjectileRoot != null;
        }

        /// <summary>
        /// summary: 根据当前缓存的玩家、子弹 prefab 与编译结果，运行一轮新的离屏预览发射。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RunPreviewCycle()
        {
            if (!TryEnsurePreviewRig(out string rigStatus))
            {
                ApplyStatusMessage(rigStatus);
                ClearActivePreviewBullets();
                return;
            }

            if (!TryResolvePreviewStatus(out string statusMessage))
            {
                ApplyStatusMessage(statusMessage);
                ClearActivePreviewBullets();
                return;
            }

            PreparePreviewSurface();
            ClearActivePreviewBullets();
            ApplyStatusMessage(DefaultStatusMessage);

            Transform spawnAnchor = previewRig.SpawnAnchor;
            Transform ownerRoot = previewRig.PreviewPlayerRoot != null ? previewRig.PreviewPlayerRoot : previewRig.transform;
            Vector3 spawnPosition = spawnAnchor != null ? spawnAnchor.position : ownerRoot.position;
            Vector3 shotDirection = (previewDummy.ImpactAnchor - spawnPosition).normalized;
            if (shotDirection.sqrMagnitude <= 0f)
            {
                shotDirection = spawnAnchor != null && spawnAnchor.forward.sqrMagnitude > 0f
                    ? spawnAnchor.forward
                    : Vector3.forward;
            }

            activePreviewBullets.Clear();
            AttackProjectileEmitter.Emit(
                currentBulletPrefab,
                ownerRoot,
                spawnPosition,
                shotDirection,
                currentCompiledAttack,
                previewRig.ProjectileRoot,
                activePreviewBullets);

            for (int i = 0; i < activePreviewBullets.Count; i++)
            {
                activePreviewBullets[i]?.SetIgnoreGameplayPauseStatus(true);
            }

            RenderPreviewIfNeeded();
        }

        private void TryAutoBindReferences()
        {
            previewRoot ??= ResolvePreviewRoot();
            previewImage ??= previewRoot != null ? previewRoot.GetComponentInChildren<RawImage>(includeInactive: true) : null;
            statusLabel ??= previewRoot != null ? previewRoot.GetComponentInChildren<TMP_Text>(includeInactive: true) : null;
        }

        private RectTransform ResolvePreviewRoot()
        {
            return transform.Find(CurrentPreviewRootPath) as RectTransform
                ?? transform.Find(LegacyPreviewRootPath) as RectTransform
                ?? FindRectTransform(transform, "Preview Animation");
        }

        private static RectTransform FindRectTransform(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            Transform directChild = root.Find(targetName);
            if (directChild != null)
            {
                return directChild as RectTransform;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform match = FindRectTransform(root.GetChild(i), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// summary: 解析并校验当前场景里唯一的 preview rig，并把相机输出绑定到 RenderTexture。
        /// param: statusMessage 输出当前缺失的 rig 绑定信息
        /// returns: scene rig 可用时返回 true
        /// </summary>
        private bool TryEnsurePreviewRig(out string statusMessage)
        {
            if (!TryResolveSceneRig(out statusMessage))
            {
                ReleasePreviewCamera();
                return false;
            }

            if (!previewRig.TryValidate(out statusMessage))
            {
                ReleasePreviewCamera();
                UnbindRig();
                return false;
            }

            SyncPreviewDummiesFromRig();
            // Keep the authored lens and camera pose intact; runtime only binds the render target.
            previewCamera.targetTexture = previewTexture;
            if (!previewCamera.gameObject.activeSelf)
            {
                previewCamera.gameObject.SetActive(true);
            }

            ConfigureExplosionHint();
            statusMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// summary: 确保当前控制器缓存的是场景里唯一的 BackPackAttackPreviewRig。
        /// param: statusMessage 输出当前 rig 解析失败的原因
        /// returns: 解析成功且数量唯一时返回 true
        /// </summary>
        private bool TryResolveSceneRig(out string statusMessage)
        {
            BackPackAttackPreviewRig[] rigs = Object.FindObjectsByType<BackPackAttackPreviewRig>(FindObjectsSortMode.None);
            if (rigs.Length == 0)
            {
                statusMessage = "Preview unavailable: no BackPackAttackPreviewRig found in the active scene.";
                return false;
            }

            if (rigs.Length > 1)
            {
                statusMessage = $"Preview unavailable: expected exactly one BackPackAttackPreviewRig, found {rigs.Length}.";
                return false;
            }

            if (previewRig != rigs[0])
            {
                BindRig(rigs[0]);
            }

            statusMessage = string.Empty;
            return previewRig != null;
        }

        private void BindRig(BackPackAttackPreviewRig rig)
        {
            ReleasePreviewCamera();
            UnbindRig();
            previewRig = rig;
            previewCamera = rig != null ? rig.PreviewCamera : null;
            explosionHint = rig != null ? rig.ExplosionHint : null;
            SyncPreviewDummiesFromRig();
        }

        private void UnbindRig()
        {
            for (int i = 0; i < previewDummies.Count; i++)
            {
                if (previewDummies[i] != null)
                {
                    previewDummies[i].Damaged -= HandlePreviewDummyDamaged;
                }
            }

            previewDummies.Clear();
            previewRig = null;
            previewCamera = null;
            previewDummy = null;
            explosionHint = null;
        }

        private void SyncPreviewDummiesFromRig()
        {
            BackPackPreviewDummyEnemy[] rigPreviewDummies = previewRig != null ? previewRig.PreviewDummies : null;
            BackPackPreviewDummyEnemy rigPrimaryDummy = previewRig != null ? previewRig.PreviewDummy : null;
            if (!HaveSameDummySet(rigPreviewDummies))
            {
                for (int i = 0; i < previewDummies.Count; i++)
                {
                    if (previewDummies[i] != null)
                    {
                        previewDummies[i].Damaged -= HandlePreviewDummyDamaged;
                    }
                }

                previewDummies.Clear();
                if (rigPreviewDummies != null)
                {
                    for (int i = 0; i < rigPreviewDummies.Length; i++)
                    {
                        BackPackPreviewDummyEnemy candidate = rigPreviewDummies[i];
                        if (candidate == null)
                        {
                            continue;
                        }

                        previewDummies.Add(candidate);
                        candidate.Damaged += HandlePreviewDummyDamaged;
                    }
                }
            }

            previewDummy = rigPrimaryDummy;
        }

        private bool HaveSameDummySet(BackPackPreviewDummyEnemy[] rigPreviewDummies)
        {
            int rigCount = rigPreviewDummies != null ? rigPreviewDummies.Length : 0;
            if (rigCount != previewDummies.Count)
            {
                return false;
            }

            for (int i = 0; i < rigCount; i++)
            {
                if (previewDummies[i] != rigPreviewDummies[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// summary: 确保当前 UI 预览区域持有一张匹配配置尺寸的 RenderTexture。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsurePreviewTexture()
        {
            int sanitizedSize = Mathf.Max(64, renderTextureSize);
            if (previewTexture != null &&
                previewTexture.width == sanitizedSize &&
                previewTexture.height == sanitizedSize)
            {
                return;
            }

            ReleasePreviewTexture();
            previewTexture = new RenderTexture(sanitizedSize, sanitizedSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "BackPackPreviewTexture"
            };
            previewTexture.Create();
        }

        private void ReleasePreviewTexture()
        {
            if (previewTexture == null)
            {
                return;
            }

            previewTexture.Release();
            if (Application.isPlaying)
            {
                Destroy(previewTexture);
            }
            else
            {
                DestroyImmediate(previewTexture);
            }

            previewTexture = null;
        }

        private void ReleasePreviewCamera()
        {
            if (previewCamera == null)
            {
                return;
            }

            previewCamera.targetTexture = null;
            if (previewCamera.gameObject.activeSelf)
            {
                previewCamera.gameObject.SetActive(false);
            }
        }

        private void ApplyPreviewTexture(Texture texture)
        {
            if (previewImage != null)
            {
                previewImage.texture = texture;
            }
        }

        private void PreparePreviewSurface()
        {
            int targetLayer = currentCompiledAttack != null
                ? ResolvePreviewTargetLayer(currentCompiledAttack.AttackSpec.impactMask)
                : 0;
            float previewHealth = currentCompiledAttack != null
                ? Mathf.Max(100f, currentCompiledAttack.AttackSpec.damage * 4f)
                : 100f;

            for (int i = 0; i < previewDummies.Count; i++)
            {
                previewDummies[i].ResetPreviewState(previewHealth, targetLayer);
            }

            hasShownExplosionThisCycle = false;
            ClearExplosionHintImmediate();
        }

        private void ConfigureExplosionHint()
        {
            if (explosionHint == null)
            {
                return;
            }

            explosionHint.loop = true;
            explosionHint.useWorldSpace = false;
            explosionHint.positionCount = ExplosionSegmentCount;
            explosionHint.widthMultiplier = explosionHintWidth;
            explosionHint.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            explosionHint.receiveShadows = false;
            explosionHint.startColor = explosionHintColor;
            explosionHint.endColor = explosionHintColor;
        }

        private void HandlePreviewDummyDamaged(BackPackPreviewDummyEnemy dummy)
        {
            if (dummy == null || currentCompiledAttack == null || !currentCompiledAttack.HasExplosion || hasShownExplosionThisCycle)
            {
                return;
            }

            ShowExplosionHint(dummy, Mathf.Max(0f, currentCompiledAttack.ExplosionRadius));
            hasShownExplosionThisCycle = true;
        }

        private void ShowExplosionHint(BackPackPreviewDummyEnemy damagedDummy, float radius)
        {
            if (explosionHint == null || previewRig == null || damagedDummy == null || radius <= 0f)
            {
                return;
            }

            float clampedRadius = Mathf.Max(radius, 0.1f);
            Vector3 localCenter = previewRig.transform.InverseTransformPoint(damagedDummy.ImpactAnchor);
            explosionHint.transform.SetParent(previewRig.transform, false);
            explosionHint.transform.localPosition = localCenter;
            explosionHint.transform.localRotation = Quaternion.identity;
            explosionHint.transform.localScale = Vector3.one;

            for (int i = 0; i < ExplosionSegmentCount; i++)
            {
                float angle = (Mathf.PI * 2f * i) / ExplosionSegmentCount;
                float x = Mathf.Cos(angle) * clampedRadius;
                float z = Mathf.Sin(angle) * clampedRadius;
                explosionHint.SetPosition(i, new Vector3(x, 0f, z));
            }

            explosionHint.enabled = true;
            explosionHintHideTime = Application.isPlaying ? Time.unscaledTime + explosionHintDuration : explosionHintDuration;
            ApplyExplosionHintColor(explosionHintColor.a);
        }

        private void UpdateExplosionHint()
        {
            if (explosionHint == null || !explosionHint.enabled)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            if (Time.unscaledTime >= explosionHintHideTime)
            {
                ClearExplosionHintImmediate();
                return;
            }

            float remaining = Mathf.Max(0f, explosionHintHideTime - Time.unscaledTime);
            float alpha = explosionHintDuration > 0f ? remaining / explosionHintDuration : 0f;
            ApplyExplosionHintColor(explosionHintColor.a * alpha);
        }

        private void ClearExplosionHintImmediate()
        {
            if (explosionHint == null)
            {
                return;
            }

            explosionHint.enabled = false;
            explosionHintHideTime = float.NegativeInfinity;
            ApplyExplosionHintColor(0f);
        }

        private void ApplyExplosionHintColor(float alpha)
        {
            if (explosionHint == null)
            {
                return;
            }

            Color color = explosionHintColor;
            color.a = Mathf.Clamp01(alpha);
            explosionHint.startColor = color;
            explosionHint.endColor = color;
        }

        private bool TryResolvePreviewStatus(out string statusMessage)
        {
            if (currentPlayer == null)
            {
                statusMessage = "Preview unavailable: player not found.";
                return false;
            }

            if (currentBulletPrefab == null)
            {
                statusMessage = "Preview unavailable: player bullet prefab is missing.";
                return false;
            }

            if (currentCompiledAttack == null)
            {
                statusMessage = "Preview unavailable: no compiled attack.";
                return false;
            }

            if (!currentCompiledAttack.CanFire)
            {
                statusMessage = BuildCompileMessage(currentCompiledAttack);
                return false;
            }

            statusMessage = DefaultStatusMessage;
            return true;
        }

        private static string BuildCompileMessage(CompiledAttack compiledAttack)
        {
            if (compiledAttack == null)
            {
                return "Preview unavailable: formula failed to compile.";
            }

            for (int i = 0; i < compiledAttack.Messages.Count; i++)
            {
                if (compiledAttack.Messages[i].severity == AttackCompileMessageSeverity.Error)
                {
                    return compiledAttack.Messages[i].message;
                }
            }

            for (int i = 0; i < compiledAttack.Messages.Count; i++)
            {
                if (compiledAttack.Messages[i].severity == AttackCompileMessageSeverity.Warning)
                {
                    return compiledAttack.Messages[i].message;
                }
            }

            return "Preview unavailable: formula cannot fire.";
        }

        private void ApplyStatusMessage(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message ?? string.Empty;
            }
        }

        private void RenderPreviewIfNeeded()
        {
            if (!Application.isPlaying && previewCamera != null && previewTexture != null)
            {
                previewCamera.Render();
            }
        }

        private void ClearActivePreviewBullets()
        {
            for (int i = activePreviewBullets.Count - 1; i >= 0; i--)
            {
                DestroyPreviewBullet(activePreviewBullets[i]);
            }

            activePreviewBullets.Clear();
            if (previewRig == null || previewRig.ProjectileRoot == null)
            {
                return;
            }

            CharBullet[] trackedBullets = previewRig.ProjectileRoot.GetComponentsInChildren<CharBullet>(includeInactive: true);
            for (int i = 0; i < trackedBullets.Length; i++)
            {
                DestroyPreviewBullet(trackedBullets[i]);
            }
        }

        private static void DestroyPreviewBullet(CharBullet bullet)
        {
            if (bullet == null)
            {
                return;
            }

            bullet.Expire();
            if (bullet == null)
            {
                return;
            }

            DestroyRuntimeObject(bullet.gameObject);
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static int ResolvePreviewTargetLayer(LayerMask impactMask)
        {
            int maskValue = impactMask.value;
            if (maskValue == 0)
            {
                return 0;
            }

            for (int layer = 0; layer < 32; layer++)
            {
                if ((maskValue & (1 << layer)) != 0)
                {
                    return layer;
                }
            }

            return 0;
        }
    }
}
