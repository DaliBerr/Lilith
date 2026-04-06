using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 让相机与玩家焦点之间的墙体暂时切换为幽灵材质，降低透视镜头下的遮挡问题。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraOcclusionFader : MonoBehaviour
{
    private const int MinimumMaxHits = 1;
    private const float MinimumRayDistance = 0.01f;
    private const float DefaultGhostAlpha = 0.2f;

    [SerializeField] private Transform targetTransform;
    [SerializeField] private Material occludedWallMaterial;
    [SerializeField] private LayerMask occlusionMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(MinimumMaxHits)] private int maxHits = 32;

    private readonly Dictionary<Renderer, Material[]> occludedRendererMaterials = new();
    private readonly HashSet<Renderer> currentFrameOccludedRenderers = new();
    private RaycastHit[] hitBuffer = new RaycastHit[MinimumMaxHits];
    private PlayerFollowCamera followCamera;
    private Camera cachedCamera;
    private Material runtimeOccludedWallMaterial;

    private void Awake()
    {
        EnsureReferences();
        EnsureHitBuffer();
    }

    private void LateUpdate()
    {
        RefreshOcclusionState();
    }

    private void OnDisable()
    {
        RestoreAllOccludedRenderers();
    }

    private void OnDestroy()
    {
        RestoreAllOccludedRenderers();
        if (runtimeOccludedWallMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeOccludedWallMaterial);
            }
            else
            {
                DestroyImmediate(runtimeOccludedWallMaterial);
            }
        }
    }

    private void OnValidate()
    {
        maxHits = Mathf.Max(MinimumMaxHits, maxHits);
        EnsureReferences();
        EnsureHitBuffer();
    }

    /// <summary>
    /// summary: 刷新当前帧的墙体遮挡状态；命中相机和玩家之间的墙体时切到幽灵材质，离开后恢复原材质。
    /// param: 无
    /// returns: 成功完成一次遮挡状态刷新时返回 true
    /// </summary>
    public bool RefreshOcclusionState()
    {
        EnsureReferences();
        EnsureHitBuffer();
        currentFrameOccludedRenderers.Clear();

        if (!TryResolveTargetTransform() || cachedCamera == null)
        {
            RestoreUnusedOccludedRenderers();
            return false;
        }

        Vector3 origin = cachedCamera.transform.position;
        Vector3 targetPoint = ResolveTargetPoint();
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= MinimumRayDistance)
        {
            RestoreUnusedOccludedRenderers();
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction / distance,
            hitBuffer,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (!TryResolveWallRenderers(hit.collider, out Renderer[] renderers))
            {
                continue;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || !currentFrameOccludedRenderers.Add(renderer))
                {
                    continue;
                }

                ApplyOccludedMaterial(renderer);
            }
        }

        RestoreUnusedOccludedRenderers();
        return true;
    }

    /// <summary>
    /// summary: 获取当前遮挡检测应该命中的玩家焦点；若存在 PlayerFollowCamera，则优先使用它的焦点坐标。
    /// param: 无
    /// returns: 当前遮挡检测使用的世界坐标
    /// </summary>
    private Vector3 ResolveTargetPoint()
    {
        if (followCamera != null && followCamera.TargetPlayer != null)
        {
            return followCamera.FocusWorldPoint;
        }

        return targetTransform != null ? targetTransform.position : transform.position;
    }

    /// <summary>
    /// summary: 尝试把一次 Physics 命中的碰撞体解析为墙体 cell 的 renderer 集合。
    /// param: hitCollider 当前命中的碰撞体
    /// param: renderers 输出的墙体 renderer 集合
    /// returns: 命中确实属于墙体 cell 且存在可淡出的 renderer 时返回 true
    /// </summary>
    private static bool TryResolveWallRenderers(Collider hitCollider, out Renderer[] renderers)
    {
        renderers = null;
        if (hitCollider == null)
        {
            return false;
        }

        CellData cellData = hitCollider.GetComponentInParent<CellData>();
        if (cellData == null || cellData.SurfaceType != CellData.CellSurfaceType.Wall || cellData.WallModelRoot == null)
        {
            return false;
        }

        renderers = cellData.WallModelRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
        return renderers != null && renderers.Length > 0;
    }

    /// <summary>
    /// summary: 把一个 renderer 切换到幽灵材质，并缓存原始材质数组用于恢复。
    /// param: renderer 需要淡出的 renderer
    /// returns: 无
    /// </summary>
    private void ApplyOccludedMaterial(Renderer renderer)
    {
        Material ghostMaterial = ResolveOccludedWallMaterial();
        if (renderer == null || ghostMaterial == null)
        {
            return;
        }

        if (!occludedRendererMaterials.ContainsKey(renderer))
        {
            occludedRendererMaterials.Add(renderer, renderer.sharedMaterials);
        }

        Material[] ghostMaterials = BuildUniformMaterialArray(renderer.sharedMaterials.Length, ghostMaterial);
        renderer.sharedMaterials = ghostMaterials;
    }

    /// <summary>
    /// summary: 恢复当前帧没有继续遮挡的 renderer，避免墙体在玩家离开后保持幽灵状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void RestoreUnusedOccludedRenderers()
    {
        if (occludedRendererMaterials.Count == 0)
        {
            return;
        }

        var renderersToRestore = ListPool<Renderer>.Get();
        foreach (KeyValuePair<Renderer, Material[]> pair in occludedRendererMaterials)
        {
            if (!currentFrameOccludedRenderers.Contains(pair.Key))
            {
                renderersToRestore.Add(pair.Key);
            }
        }

        for (int i = 0; i < renderersToRestore.Count; i++)
        {
            RestoreRenderer(renderersToRestore[i]);
        }

        ListPool<Renderer>.Release(renderersToRestore);
    }

    /// <summary>
    /// summary: 恢复所有当前处于幽灵状态的 renderer；常用于禁用组件或销毁对象时清理状态。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void RestoreAllOccludedRenderers()
    {
        if (occludedRendererMaterials.Count == 0)
        {
            return;
        }

        var renderersToRestore = ListPool<Renderer>.Get();
        foreach (KeyValuePair<Renderer, Material[]> pair in occludedRendererMaterials)
        {
            renderersToRestore.Add(pair.Key);
        }

        for (int i = 0; i < renderersToRestore.Count; i++)
        {
            RestoreRenderer(renderersToRestore[i]);
        }

        ListPool<Renderer>.Release(renderersToRestore);
    }

    /// <summary>
    /// summary: 恢复单个 renderer 的原始共享材质数组。
    /// param: renderer 需要恢复的 renderer
    /// returns: 无
    /// </summary>
    private void RestoreRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            occludedRendererMaterials.Remove(renderer);
            return;
        }

        if (!occludedRendererMaterials.TryGetValue(renderer, out Material[] originalMaterials))
        {
            return;
        }

        renderer.sharedMaterials = originalMaterials;
        occludedRendererMaterials.Remove(renderer);
    }

    /// <summary>
    /// summary: 解析当前应该使用的幽灵材质；未显式配置时按当前渲染管线创建一个默认透明材质。
    /// param: 无
    /// returns: 当前可用的幽灵材质；解析失败时返回 null
    /// </summary>
    private Material ResolveOccludedWallMaterial()
    {
        if (occludedWallMaterial != null)
        {
            return occludedWallMaterial;
        }

        if (runtimeOccludedWallMaterial != null)
        {
            return runtimeOccludedWallMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeOccludedWallMaterial = new Material(shader)
        {
            name = "Runtime_OccludedWallMaterial",
            renderQueue = (int)RenderQueue.Transparent,
        };

        Color ghostColor = new(1f, 1f, 1f, DefaultGhostAlpha);
        if (runtimeOccludedWallMaterial.HasProperty("_BaseColor"))
        {
            runtimeOccludedWallMaterial.SetColor("_BaseColor", ghostColor);
        }

        if (runtimeOccludedWallMaterial.HasProperty("_Color"))
        {
            runtimeOccludedWallMaterial.SetColor("_Color", ghostColor);
        }

        if (runtimeOccludedWallMaterial.HasProperty("_Surface"))
        {
            runtimeOccludedWallMaterial.SetFloat("_Surface", 1f);
        }

        if (runtimeOccludedWallMaterial.HasProperty("_Blend"))
        {
            runtimeOccludedWallMaterial.SetFloat("_Blend", 0f);
        }

        if (runtimeOccludedWallMaterial.HasProperty("_SrcBlend"))
        {
            runtimeOccludedWallMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (runtimeOccludedWallMaterial.HasProperty("_DstBlend"))
        {
            runtimeOccludedWallMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (runtimeOccludedWallMaterial.HasProperty("_ZWrite"))
        {
            runtimeOccludedWallMaterial.SetFloat("_ZWrite", 0f);
        }

        return runtimeOccludedWallMaterial;
    }

    /// <summary>
    /// summary: 补齐当前命中缓冲区大小，确保 RaycastNonAlloc 始终有足够空间容纳命中结果。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureHitBuffer()
    {
        if (hitBuffer != null && hitBuffer.Length == maxHits)
        {
            return;
        }

        hitBuffer = new RaycastHit[Mathf.Max(MinimumMaxHits, maxHits)];
    }

    /// <summary>
    /// summary: 缓存当前相机和 PlayerFollowCamera 组件，供遮挡逻辑复用。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void EnsureReferences()
    {
        if (cachedCamera == null)
        {
            cachedCamera = GetComponent<Camera>();
        }

        if (followCamera == null)
        {
            followCamera = GetComponent<PlayerFollowCamera>();
        }
    }

    /// <summary>
    /// summary: 解析当前要保护可见性的目标 Transform；未显式绑定时回退到同物体上的 PlayerFollowCamera。
    /// param: 无
    /// returns: 成功找到目标时返回 true
    /// </summary>
    private bool TryResolveTargetTransform()
    {
        if (targetTransform != null)
        {
            return true;
        }

        EnsureReferences();
        if (followCamera == null || followCamera.TargetPlayer == null)
        {
            return false;
        }

        targetTransform = followCamera.TargetPlayer;
        return true;
    }

    private static Material[] BuildUniformMaterialArray(int length, Material material)
    {
        int resolvedLength = Mathf.Max(1, length);
        var materials = new Material[resolvedLength];
        for (int i = 0; i < resolvedLength; i++)
        {
            materials[i] = material;
        }

        return materials;
    }

    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new();

        public static List<T> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>();
        }

        public static void Release(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();
            Pool.Push(list);
        }
    }
}
