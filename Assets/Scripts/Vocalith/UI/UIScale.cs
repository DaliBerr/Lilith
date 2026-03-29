using System.Collections.Generic;
using Vocalith.Logging;
using UnityEngine;
using UnityEngine.UI;

public class UIScale : MonoBehaviour
{
    [SerializeField] private Transform uiRoot;
    [SerializeField] private float minUIScale = 0.5f;
    [SerializeField] private float maxUIScale = 2f;

    [Header("Layout Compensation")]
    [SerializeField] private bool compensateLayout = true;

    [Range(0f, 1f)]
    [SerializeField] private float spacingVisualScale = 0.0f;

    [Range(0f, 1f)]
    [SerializeField] private float paddingVisualScale = 0.0f;

    private readonly Dictionary<CanvasScaler, Vector2> _baseReferenceResolution = new();

    private struct HovLayoutBase
    {
        public float spacing;
        public int left, right, top, bottom;
    }

    private struct GridLayoutBase
    {
        public Vector2 spacing;
        public RectOffset padding;
    }

    private readonly Dictionary<HorizontalOrVerticalLayoutGroup, HovLayoutBase> _hovBase = new();
    private readonly Dictionary<GridLayoutGroup, GridLayoutBase> _gridBase = new();

    /// <summary>
    /// 应用 UI 缩放倍率：对 CanvasScaler 生效，并可选补偿 LayoutGroup 的间距与内边距。
    /// </summary>
    /// <param name="uiScale">UI 缩放倍率（1=默认；>1更大；<1更小）</param>
    /// <return>无</return>
    public void ApplyUIScale(float uiScale)
    {
        uiScale = Mathf.Clamp(uiScale, minUIScale, maxUIScale);

        ApplyCanvasScaler(uiScale);

        if (compensateLayout)
            ApplyLayoutCompensation(uiScale);

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 对所有目标 CanvasScaler 应用缩放（ScaleWithScreenSize 使用 referenceResolution 方案）。
    /// </summary>
    /// <param name="uiScale">UI 缩放倍率</param>
    /// <return>无</return>
    private void ApplyCanvasScaler(float uiScale)
    {
        var scalers = GetTargetCanvasScalers();
        if (scalers == null || scalers.Count == 0)
        {
            GameDebug.LogWarning("[ApplyUIScale] 未找到任何 CanvasScaler（请检查是否使用了 uGUI Canvas）");
            return;
        }

        foreach (var scaler in scalers)
        {
            if (scaler == null) continue;

            if (!_baseReferenceResolution.ContainsKey(scaler))
                _baseReferenceResolution[scaler] = scaler.referenceResolution;

            switch (scaler.uiScaleMode)
            {
                case CanvasScaler.ScaleMode.ScaleWithScreenSize:
                {
                    Vector2 baseRes = _baseReferenceResolution[scaler];
                    scaler.referenceResolution = baseRes / uiScale;
                    break;
                }
                case CanvasScaler.ScaleMode.ConstantPixelSize:
                case CanvasScaler.ScaleMode.ConstantPhysicalSize:
                {
                    scaler.scaleFactor = uiScale;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 补偿 LayoutGroup 的 spacing/padding：避免 UI 放大时“空隙也等比例膨胀”导致内容被挤小。
    /// </summary>
    /// <param name="uiScale">UI 缩放倍率</param>
    /// <return>无</return>
    private void ApplyLayoutCompensation(float uiScale)
    {
        // 目标：视觉 spacing ~ uiScale^(spacingVisualScale)
        // 由于 Canvas 整体会乘 uiScale，这里把 spacing 设为：base * uiScale^(spacingVisualScale - 1)
        float spacingFactor = Mathf.Pow(uiScale, spacingVisualScale - 1f);
        float paddingFactor = Mathf.Pow(uiScale, paddingVisualScale - 1f);

        // Horizontal/Vertical Layout
        var hovGroups = GetTargetHovLayoutGroups();
        foreach (var g in hovGroups)
        {
            if (g == null) continue;

            if (!_hovBase.ContainsKey(g))
            {
                var p = g.padding;
                _hovBase[g] = new HovLayoutBase
                {
                    spacing = g.spacing,
                    left = p.left,
                    right = p.right,
                    top = p.top,
                    bottom = p.bottom
                };
            }

            var b = _hovBase[g];
            g.spacing = b.spacing * spacingFactor;

            // padding 是 int，做四舍五入
            g.padding.left = Mathf.RoundToInt(b.left * paddingFactor);
            g.padding.right = Mathf.RoundToInt(b.right * paddingFactor);
            g.padding.top = Mathf.RoundToInt(b.top * paddingFactor);
            g.padding.bottom = Mathf.RoundToInt(b.bottom * paddingFactor);
        }

        // Grid Layout
        var gridGroups = GetTargetGridLayoutGroups();
        foreach (var g in gridGroups)
        {
            if (g == null) continue;

            if (!_gridBase.ContainsKey(g))
            {
                _gridBase[g] = new GridLayoutBase
                {
                    spacing = g.spacing,
                    padding = new RectOffset(g.padding.left, g.padding.right, g.padding.top, g.padding.bottom)
                };
            }

            var b = _gridBase[g];
            g.spacing = b.spacing * spacingFactor;

            g.padding.left = Mathf.RoundToInt(b.padding.left * paddingFactor);
            g.padding.right = Mathf.RoundToInt(b.padding.right * paddingFactor);
            g.padding.top = Mathf.RoundToInt(b.padding.top * paddingFactor);
            g.padding.bottom = Mathf.RoundToInt(b.padding.bottom * paddingFactor);
        }
    }

    /// <summary>
    /// 获取需要应用缩放的 CanvasScaler 列表：若指定 uiRoot 则只遍历其子节点，否则遍历全场景。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>CanvasScaler 列表</return>
    private List<CanvasScaler> GetTargetCanvasScalers()
    {
        if (uiRoot != null)
        {
            var list = new List<CanvasScaler>();
            uiRoot.GetComponentsInChildren(true, list);
            return list;
        }

#if UNITY_2023_1_OR_NEWER
        return new List<CanvasScaler>(FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None));
#else
        return new List<CanvasScaler>(FindObjectsOfType<CanvasScaler>(true));
#endif
    }

    /// <summary>
    /// 获取目标 HorizontalOrVerticalLayoutGroup 列表（用于补偿 spacing/padding）。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>HorizontalOrVerticalLayoutGroup 列表</return>
    private List<HorizontalOrVerticalLayoutGroup> GetTargetHovLayoutGroups()
    {
        var list = new List<HorizontalOrVerticalLayoutGroup>();
        if (uiRoot != null) uiRoot.GetComponentsInChildren(true, list);
        else Object.FindObjectsByType<HorizontalOrVerticalLayoutGroup>(FindObjectsSortMode.None);
        return list;
    }

    /// <summary>
    /// 获取目标 GridLayoutGroup 列表（用于补偿 spacing/padding）。
    /// </summary>
    /// <param name="none">无</param>
    /// <return>GridLayoutGroup 列表</return>
    private List<GridLayoutGroup> GetTargetGridLayoutGroups()
    {
        var list = new List<GridLayoutGroup>();
        if (uiRoot != null) uiRoot.GetComponentsInChildren(true, list);
        else Object.FindObjectsByType<GridLayoutGroup>(FindObjectsSortMode.None);
        return list;
    }
}
