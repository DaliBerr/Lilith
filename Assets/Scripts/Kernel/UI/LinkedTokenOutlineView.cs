using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 独立的连锁 token 外框视图，用于在槽位上方绘制整件包围框。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LinkedTokenOutlineView : MonoBehaviour
    {
        private static int runtimeConstructionDepth;

        [SerializeField] private RectTransform topEdge;
        [SerializeField] private RectTransform bottomEdge;
        [SerializeField] private RectTransform leftEdge;
        [SerializeField] private RectTransform rightEdge;
        [SerializeField] private Image topEdgeImage;
        [SerializeField] private Image bottomEdgeImage;
        [SerializeField] private Image leftEdgeImage;
        [SerializeField] private Image rightEdgeImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(1f)] private float edgeThickness = 4f;

        private RectTransform rectTransform;

        public RectTransform OutlineRectTransform => rectTransform;

        private void Awake()
        {
            EnsureVisualTree();
            ApplyEdgeThickness();
        }

        private void OnValidate()
        {
            if (runtimeConstructionDepth > 0)
            {
                return;
            }

            EnsureVisualTree();
            ApplyEdgeThickness();
        }

        /// <summary>
        /// summary: 创建一个运行时外框视图并挂到指定父节点下。
        /// param: name 新外框节点名称
        /// param: parent 新外框节点父节点
        /// param: uiLayer 新节点使用的 Unity Layer
        /// returns: 创建好的外框视图组件
        /// </summary>
        public static LinkedTokenOutlineView CreateRuntime(string name, Transform parent, int uiLayer)
        {
            runtimeConstructionDepth++;
            try
            {
                GameObject root = new(name, typeof(RectTransform), typeof(CanvasGroup));
                root.layer = uiLayer;
                if (parent != null)
                {
                    root.transform.SetParent(parent, false);
                }

                LinkedTokenOutlineView outlineView = root.AddComponent<LinkedTokenOutlineView>();
                outlineView.EnsureVisualTree();
                outlineView.ApplyEdgeThickness();
                return outlineView;
            }
            finally
            {
                runtimeConstructionDepth--;
            }
        }

        /// <summary>
        /// summary: 统一设置外框颜色和线宽。
        /// param: color 当前外框颜色
        /// param: thickness 当前边框线宽
        /// returns: 无
        /// </summary>
        public void ApplyStyle(Color color, float thickness)
        {
            edgeThickness = Mathf.Max(1f, thickness);
            EnsureVisualTree();
            ApplyEdgeThickness();
            ApplyColor(color);
        }

        /// <summary>
        /// summary: 按两个槽位的世界边界在指定 layerRoot 下生成一个包围框。
        /// param: layerRoot 当前外框所在的局部坐标根节点
        /// param: firstSlot 当前连锁件首格
        /// param: lastSlot 当前连锁件末格
        /// param: padding 当前包围框四周补白
        /// returns: 无
        /// </summary>
        public void FitToSlots(RectTransform layerRoot, RectTransform firstSlot, RectTransform lastSlot, Vector2 padding)
        {
            if (layerRoot == null || firstSlot == null || lastSlot == null)
            {
                return;
            }

            Rect localRect = CalculateLocalRect(layerRoot, firstSlot, lastSlot, padding);
            SetLocalRect(localRect);
        }

        /// <summary>
        /// summary: 直接用一个局部矩形更新当前外框的位置和尺寸。
        /// param: localRect 当前外框应覆盖的局部矩形
        /// returns: 无
        /// </summary>
        public void SetLocalRect(Rect localRect)
        {
            EnsureVisualTree();
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = localRect.center;
            rectTransform.sizeDelta = localRect.size;
            rectTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// summary: 用屏幕坐标驱动当前外框在指定 layerRoot 下跟手移动。
        /// param: layerRoot 当前外框所在的局部坐标根节点
        /// param: screenPosition 当前屏幕坐标
        /// param: eventCamera 当前 UI 事件相机
        /// returns: 无
        /// </summary>
        public void UpdateScreenPosition(RectTransform layerRoot, Vector2 screenPosition, Camera eventCamera)
        {
            EnsureVisualTree();
            if (rectTransform == null || layerRoot == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layerRoot, screenPosition, eventCamera, out Vector2 localPoint))
            {
                return;
            }

            rectTransform.anchoredPosition = localPoint;
        }

        /// <summary>
        /// summary: 计算单个槽位中心的屏幕坐标。
        /// param: slot 当前槽位 RectTransform
        /// param: eventCamera 当前 UI 事件相机
        /// returns: 当前槽位中心的屏幕坐标
        /// </summary>
        public static Vector2 GetScreenCenter(RectTransform slot, Camera eventCamera)
        {
            if (slot == null)
            {
                return Vector2.zero;
            }

            return GetScreenCenterInternal(slot, slot, eventCamera);
        }

        /// <summary>
        /// summary: 计算由首尾两个槽位包围出的整体矩形中心的屏幕坐标。
        /// param: firstSlot 当前连锁件首格
        /// param: lastSlot 当前连锁件末格
        /// param: eventCamera 当前 UI 事件相机
        /// returns: 当前整件外框中心的屏幕坐标
        /// </summary>
        public static Vector2 GetScreenCenter(RectTransform firstSlot, RectTransform lastSlot, Camera eventCamera)
        {
            if (firstSlot == null || lastSlot == null)
            {
                return Vector2.zero;
            }

            return GetScreenCenterInternal(firstSlot, lastSlot, eventCamera);
        }

        private void EnsureVisualTree()
        {
            rectTransform ??= transform as RectTransform;
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (canvasGroup == null && Application.isPlaying)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            EnsureEdge(ref topEdge, ref topEdgeImage, "TopEdge");
            EnsureEdge(ref bottomEdge, ref bottomEdgeImage, "BottomEdge");
            EnsureEdge(ref leftEdge, ref leftEdgeImage, "LeftEdge");
            EnsureEdge(ref rightEdge, ref rightEdgeImage, "RightEdge");
        }

        private void EnsureEdge(ref RectTransform edgeTransform, ref Image edgeImage, string childName)
        {
            if (edgeTransform == null || edgeImage == null)
            {
                Transform child = transform.Find(childName);
                if (child == null)
                {
                    GameObject edgeObject = new(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    edgeObject.layer = gameObject.layer;
                    child = edgeObject.transform;
                    child.SetParent(transform, false);
                }

                edgeTransform = child as RectTransform;
                edgeImage = child.GetComponent<Image>();
            }

            if (edgeImage != null)
            {
                edgeImage.raycastTarget = false;
                edgeImage.maskable = false;
            }
        }

        private void ApplyEdgeThickness()
        {
            float thickness = Mathf.Max(1f, edgeThickness);
            ConfigureEdge(topEdge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), Vector2.zero);
            ConfigureEdge(bottomEdge, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), Vector2.zero);
            ConfigureEdge(leftEdge, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), Vector2.zero);
            ConfigureEdge(rightEdge, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), Vector2.zero);
        }

        private static void ConfigureEdge(RectTransform edgeTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            if (edgeTransform == null)
            {
                return;
            }

            edgeTransform.anchorMin = anchorMin;
            edgeTransform.anchorMax = anchorMax;
            edgeTransform.pivot = pivot;
            edgeTransform.anchoredPosition = anchoredPosition;
            edgeTransform.sizeDelta = sizeDelta;
            edgeTransform.localScale = Vector3.one;
        }

        private void ApplyColor(Color color)
        {
            ApplyImageColor(topEdgeImage, color);
            ApplyImageColor(bottomEdgeImage, color);
            ApplyImageColor(leftEdgeImage, color);
            ApplyImageColor(rightEdgeImage, color);
        }

        private static void ApplyImageColor(Image image, Color color)
        {
            if (image != null)
            {
                image.color = color;
            }
        }

        private static Rect CalculateLocalRect(RectTransform layerRoot, RectTransform firstSlot, RectTransform lastSlot, Vector2 padding)
        {
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);

            AccumulateLocalCorners(layerRoot, firstSlot, ref min, ref max);
            AccumulateLocalCorners(layerRoot, lastSlot, ref min, ref max);

            min -= padding;
            max += padding;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AccumulateLocalCorners(RectTransform layerRoot, RectTransform slot, ref Vector2 min, ref Vector2 max)
        {
            if (layerRoot == null || slot == null)
            {
                return;
            }

            Vector3[] corners = new Vector3[4];
            slot.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 localCorner = layerRoot.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, localCorner);
                max = Vector2.Max(max, localCorner);
            }
        }

        private static Vector2 GetScreenCenterInternal(RectTransform firstSlot, RectTransform lastSlot, Camera eventCamera)
        {
            Vector3[] firstCorners = new Vector3[4];
            Vector3[] lastCorners = new Vector3[4];
            firstSlot.GetWorldCorners(firstCorners);
            lastSlot.GetWorldCorners(lastCorners);

            Vector3 min = firstCorners[0];
            Vector3 max = firstCorners[2];
            for (int i = 0; i < 4; i++)
            {
                min = Vector3.Min(min, firstCorners[i]);
                min = Vector3.Min(min, lastCorners[i]);
                max = Vector3.Max(max, firstCorners[i]);
                max = Vector3.Max(max, lastCorners[i]);
            }

            Vector3 worldCenter = (min + max) * 0.5f;
            return RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
        }
    }
}
