using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 在命中点执行一次可暂停的延时爆炸：先渲染扩张预警圈，再在到时后结算 AoE 伤害。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DelayedExplosionEffectRuntime : MonoBehaviour
    {
        private const string EnemyTagName = "Enemy_Object";
        private const int MinimumIndicatorSegments = 16;
        private const float MinimumIndicatorRadius = 0.01f;
        private const float FillPlaneOffset = 0.002f;
        private const float OuterRingPlaneOffset = 0.004f;
        private const float ExpandingRingPlaneOffset = 0.006f;

        private static Material sharedIndicatorMaterial;

        private sealed class IndicatorVisual
        {
            public LineRenderer outerRing;
            public LineRenderer expandingRing;
            public MeshFilter fillMeshFilter;
            public MeshRenderer fillRenderer;
            public Mesh fillMesh;
            public Material fillMaterial;
        }

        [SerializeField, Min(MinimumIndicatorSegments)] private int indicatorSegmentCount = 64;
        [SerializeField, Min(0f)] private float indicatorWidth = 0.2f;
        [SerializeField] private Color indicatorColor = new(1f, 0.1f, 0.1f, 0.92f);
        [SerializeField, Min(0f)] private float indicatorHeightOffset = 0.08f;
        [SerializeField] private Material indicatorMaterial;

        private readonly HashSet<int> damagedRoots = new();
        private IndicatorVisual indicator;

        private float delaySeconds;
        private float explosionRadius;
        private float explosionDamage;
        private LayerMask impactMask;
        private BulletTargetPolicy targetPolicy;
        private Transform ownerRoot;

        private float elapsed;
        private bool isInitialized;
        private bool hasExploded;

        public float DelaySeconds => delaySeconds;
        public float ExplosionRadius => explosionRadius;

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            CleanupVisuals();
        }

        private void OnDestroy()
        {
            CleanupVisuals();
        }

        /// <summary>
        /// summary: 初始化一个延时爆炸实例，并在当前位置创建预警圈可视化。
        /// param: centerWorld 爆炸中心点
        /// param: delaySeconds 命中后到爆炸之间的延迟秒数
        /// param: explosionRadius 爆炸半径
        /// param: explosionDamage 爆炸伤害
        /// param: impactMask 爆炸重叠检测使用的物理层
        /// param: targetPolicy 允许受伤的目标阵营
        /// param: ownerRoot 发射者根节点，用于排除自伤碰撞
        /// param: indicatorWidth 预警圈线宽
        /// param: indicatorColor 预警圈主色
        /// param: indicatorHeightOffset 预警圈相对地面的高度偏移
        /// returns: 初始化成功时返回 true
        /// </summary>
        public bool Initialize(
            Vector3 centerWorld,
            float delaySeconds,
            float explosionRadius,
            float explosionDamage,
            LayerMask impactMask,
            BulletTargetPolicy targetPolicy,
            Transform ownerRoot,
            float indicatorWidth,
            Color indicatorColor,
            float indicatorHeightOffset)
        {
            transform.position = centerWorld;

            this.delaySeconds = Mathf.Max(0f, delaySeconds);
            this.explosionRadius = Mathf.Max(0f, explosionRadius);
            this.explosionDamage = Mathf.Max(0f, explosionDamage);
            this.impactMask = impactMask;
            this.targetPolicy = targetPolicy;
            this.ownerRoot = ownerRoot;

            this.indicatorWidth = Mathf.Max(0f, indicatorWidth);
            this.indicatorColor = indicatorColor;
            this.indicatorHeightOffset = Mathf.Max(0f, indicatorHeightOffset);
            indicatorSegmentCount = Mathf.Max(MinimumIndicatorSegments, indicatorSegmentCount);

            elapsed = 0f;
            hasExploded = false;
            isInitialized = this.explosionRadius > 0f && this.explosionDamage > 0f;
            if (!isInitialized)
            {
                return false;
            }

            indicator = CreateIndicatorVisual();
            UpdateIndicatorVisual(0f, 0f);
            return true;
        }

        /// <summary>
        /// summary: 推进延时爆状态机；用于运行时 Update 以及测试环境手动推进。
        /// param: deltaTime 本次推进的逻辑秒数
        /// returns: 无
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!isInitialized || hasExploded)
            {
                return;
            }

            if (EnemyGameplayPauseGuard.ShouldSuspendEnemyActions())
            {
                return;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            elapsed += safeDeltaTime;

            float progress = delaySeconds > 0f ? Mathf.Clamp01(elapsed / delaySeconds) : 1f;
            float expandingRadius = Mathf.Lerp(0f, explosionRadius, progress);
            UpdateIndicatorVisual(expandingRadius, progress);

            if (elapsed < delaySeconds)
            {
                return;
            }

            ExplodeNow();
        }

        private void ExplodeNow()
        {
            if (hasExploded)
            {
                return;
            }

            hasExploded = true;
            TryApplyExplosionDamage();
            DisposeSelf();
        }

        private void TryApplyExplosionDamage()
        {
            Collider[] overlaps = Physics.OverlapSphere(transform.position, explosionRadius, impactMask, QueryTriggerInteraction.Ignore);
            damagedRoots.Clear();
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap == null ||
                    overlap.isTrigger ||
                    overlap.GetComponentInParent<CharBullet>() != null ||
                    IsOwnedTransform(overlap.transform))
                {
                    continue;
                }

                Transform overlapRoot = overlap.attachedRigidbody != null ? overlap.attachedRigidbody.transform : overlap.transform.root;
                if (!damagedRoots.Add(overlapRoot.GetInstanceID()))
                {
                    continue;
                }

                if (ShouldDamageEnemies())
                {
                    TryApplyDamageToEnemy(overlap, overlapRoot);
                }

                if (ShouldDamagePlayer())
                {
                    TryApplyDamageToPlayer(overlap);
                }
            }
        }

        private bool TryApplyDamageToEnemy(Collider overlap, Transform overlapRoot)
        {
            Enemy enemy = overlap.GetComponentInParent<Enemy>();
            if (enemy == null || !IsEnemyImpactTarget(overlap, overlapRoot, enemy))
            {
                return false;
            }

            return enemy.TryApplyDamage(explosionDamage, out _, out _);
        }

        private bool TryApplyDamageToPlayer(Collider overlap)
        {
            PlayerHealth playerHealth = overlap.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return false;
            }

            return playerHealth.TryApplyDamage(explosionDamage, out _, out _);
        }

        private bool IsOwnedTransform(Transform candidate)
        {
            return ownerRoot != null && candidate != null && (candidate == ownerRoot || candidate.IsChildOf(ownerRoot));
        }

        private bool ShouldDamageEnemies()
        {
            return targetPolicy == BulletTargetPolicy.EnemiesOnly || targetPolicy == BulletTargetPolicy.Both;
        }

        private bool ShouldDamagePlayer()
        {
            return targetPolicy == BulletTargetPolicy.PlayerOnly || targetPolicy == BulletTargetPolicy.Both;
        }

        private static bool IsEnemyImpactTarget(Collider other, Transform targetRoot, Enemy enemy)
        {
            return HasEnemyTag(other.transform) ||
                   HasEnemyTag(targetRoot) ||
                   (enemy != null && HasEnemyTag(enemy.transform));
        }

        private static bool HasEnemyTag(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            return string.Equals(target.tag, EnemyTagName, System.StringComparison.Ordinal);
        }

        private IndicatorVisual CreateIndicatorVisual()
        {
            Material resolvedMaterial = indicatorMaterial != null ? indicatorMaterial : ResolveSharedIndicatorMaterial();
            if (resolvedMaterial == null)
            {
                return null;
            }

            IndicatorVisual visual = new();
            visual.outerRing = CreateRingRenderer("OuterRing", indicatorWidth, BuildOuterRingColor(indicatorColor), resolvedMaterial, 10);
            visual.expandingRing = CreateRingRenderer("ExpandingRing", indicatorWidth, BuildExpandingRingColor(indicatorColor), resolvedMaterial, 12);

            GameObject fillObject = new("Fill");
            fillObject.transform.SetParent(transform, false);
            visual.fillMeshFilter = fillObject.AddComponent<MeshFilter>();
            visual.fillRenderer = fillObject.AddComponent<MeshRenderer>();
            visual.fillMesh = new Mesh
            {
                name = "DelayedExplosionFillMesh",
            };
            visual.fillMesh.MarkDynamic();
            visual.fillMeshFilter.sharedMesh = visual.fillMesh;

            visual.fillMaterial = new Material(resolvedMaterial)
            {
                name = "DelayedExplosionFillMaterial"
            };
            ApplyMaterialColor(visual.fillMaterial, ResolveFillColor(indicatorColor, 0f));
            visual.fillRenderer.sharedMaterial = visual.fillMaterial;
            visual.fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visual.fillRenderer.receiveShadows = false;
            visual.fillRenderer.allowOcclusionWhenDynamic = true;
            visual.fillRenderer.sortingOrder = 8;
            return visual;
        }

        private LineRenderer CreateRingRenderer(string objectName, float width, Color color, Material material, int sortingOrder)
        {
            GameObject ringObject = new(objectName);
            ringObject.transform.SetParent(transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.enabled = true;
            ring.loop = true;
            ring.useWorldSpace = true;
            ring.positionCount = Mathf.Max(MinimumIndicatorSegments, indicatorSegmentCount);
            ring.widthMultiplier = width;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.textureMode = LineTextureMode.Stretch;
            ring.startColor = color;
            ring.endColor = color;
            ring.sharedMaterial = material;
            ring.sortingOrder = sortingOrder;
            return ring;
        }

        private void UpdateIndicatorVisual(float expandingRadius, float progress)
        {
            if (indicator == null)
            {
                return;
            }

            Vector3 center = transform.position;
            center.y += indicatorHeightOffset;
            UpdateRingGeometry(indicator.outerRing, center, explosionRadius, OuterRingPlaneOffset);
            UpdateRingGeometry(indicator.expandingRing, center, expandingRadius, ExpandingRingPlaneOffset);
            UpdateFillMesh(indicator, center, expandingRadius, progress);
        }

        private void UpdateRingGeometry(LineRenderer ring, Vector3 center, float radius, float planeOffset)
        {
            if (ring == null)
            {
                return;
            }

            int segmentCount = Mathf.Max(MinimumIndicatorSegments, ring.positionCount);
            float resolvedRadius = Mathf.Max(MinimumIndicatorRadius, radius);
            float worldY = center.y + planeOffset;
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segmentCount;
                Vector3 position = new(
                    center.x + Mathf.Cos(angle) * resolvedRadius,
                    worldY,
                    center.z + Mathf.Sin(angle) * resolvedRadius);
                ring.SetPosition(i, position);
            }
        }

        private void UpdateFillMesh(IndicatorVisual visual, Vector3 center, float radius, float progress)
        {
            if (visual == null || visual.fillMesh == null || visual.fillMaterial == null)
            {
                return;
            }

            int segmentCount = Mathf.Max(MinimumIndicatorSegments, indicatorSegmentCount);
            float resolvedRadius = Mathf.Max(MinimumIndicatorRadius, radius);
            Vector3[] vertices = new Vector3[segmentCount + 1];
            int[] triangles = new int[segmentCount * 3];
            float worldY = center.y + FillPlaneOffset;
            vertices[0] = new Vector3(center.x, worldY, center.z);

            for (int i = 0; i < segmentCount; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segmentCount;
                vertices[i + 1] = new Vector3(
                    center.x + Mathf.Cos(angle) * resolvedRadius,
                    worldY,
                    center.z + Mathf.Sin(angle) * resolvedRadius);

                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == segmentCount - 1 ? 1 : i + 2;
            }

            visual.fillMesh.Clear();
            visual.fillMesh.vertices = vertices;
            visual.fillMesh.triangles = triangles;
            visual.fillMesh.RecalculateNormals();
            visual.fillMesh.RecalculateBounds();
            ApplyMaterialColor(visual.fillMaterial, ResolveFillColor(indicatorColor, progress));
        }

        private void CleanupVisuals()
        {
            if (indicator == null)
            {
                return;
            }

            if (indicator.fillMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(indicator.fillMesh);
                }
                else
                {
                    DestroyImmediate(indicator.fillMesh);
                }
            }

            if (indicator.fillMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(indicator.fillMaterial);
                }
                else
                {
                    DestroyImmediate(indicator.fillMaterial);
                }
            }

            if (indicator.outerRing != null)
            {
                DestroyGameObject(indicator.outerRing.gameObject);
            }

            if (indicator.expandingRing != null)
            {
                DestroyGameObject(indicator.expandingRing.gameObject);
            }

            if (indicator.fillMeshFilter != null)
            {
                DestroyGameObject(indicator.fillMeshFilter.gameObject);
            }

            indicator = null;
        }

        private void DisposeSelf()
        {
            CleanupVisuals();
            DestroyGameObject(gameObject);
        }

        private static void DestroyGameObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Color ResolveFillColor(Color baseColor, float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);

            Color startColor = baseColor;
            startColor.g *= 0.85f;
            startColor.b *= 0.85f;
            startColor.a = Mathf.Clamp01(Mathf.Max(baseColor.a * 0.08f, 0.06f));

            Color endColor = baseColor;
            endColor.g *= 0.35f;
            endColor.b *= 0.35f;
            endColor.a = Mathf.Clamp01(Mathf.Max(baseColor.a * 0.6f, 0.45f));

            return Color.Lerp(startColor, endColor, clampedProgress);
        }

        private static Color BuildOuterRingColor(Color baseColor)
        {
            Color color = baseColor;
            color.a = Mathf.Clamp01(Mathf.Max(baseColor.a * 0.85f, 0.35f));
            return color;
        }

        private static Color BuildExpandingRingColor(Color baseColor)
        {
            Color color = baseColor;
            color.a = Mathf.Clamp01(Mathf.Max(baseColor.a, 0.8f));
            return color;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
        }

        private static Material ResolveSharedIndicatorMaterial()
        {
            if (sharedIndicatorMaterial != null)
            {
                return sharedIndicatorMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            sharedIndicatorMaterial = new Material(shader)
            {
                name = "DelayedExplosionIndicatorMaterial"
            };
            return sharedIndicatorMaterial;
        }
    }
}
