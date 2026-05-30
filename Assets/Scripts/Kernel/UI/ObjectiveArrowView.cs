using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveArrowView : MonoBehaviour
    {
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private RectTransform arrowRect;
        [SerializeField] private Image arrowImage;
        [SerializeField, Min(0f)] private float edgePadding = 48f;
        [SerializeField, Min(0f)] private float onscreenCenterRadius = 96f;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform targetTransform;

        private void Reset()
        {
            ResolveMissingReferences();
            DisableRaycastTargets();
        }

        private void Awake()
        {
            ResolveMissingReferences();
            DisableRaycastTargets();
        }

        private void OnEnable()
        {
            RefreshArrow();
        }

        private void OnValidate()
        {
            edgePadding = Mathf.Max(0f, edgePadding);
            onscreenCenterRadius = Mathf.Max(0f, onscreenCenterRadius);
            ResolveMissingReferences();
            DisableRaycastTargets();
        }

        private void LateUpdate()
        {
            RefreshArrow();
        }

        public void Bind(Camera camera, Transform target)
        {
            worldCamera = camera;
            targetTransform = target;
            RefreshArrow();
        }

        public void ClearTarget()
        {
            targetTransform = null;
            HideArrow();
        }

        private void RefreshArrow()
        {
            ResolveMissingReferences();
            DisableRaycastTargets();

            if (panelRoot == null || arrowRect == null || worldCamera == null || targetTransform == null)
            {
                HideArrow();
                return;
            }

            Vector3 viewportPoint = worldCamera.WorldToViewportPoint(targetTransform.position);
            Vector2 screenPoint = worldCamera.WorldToScreenPoint(targetTransform.position);
            Camera uiCamera = ResolveUICamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRoot, screenPoint, uiCamera, out Vector2 targetLocal))
            {
                HideArrow();
                return;
            }

            Rect panelRect = panelRoot.rect;
            bool targetInFront = viewportPoint.z > 0f;
            if (!targetInFront)
            {
                targetLocal = panelRect.center - (targetLocal - panelRect.center);
            }

            bool targetInsidePanel = targetInFront && IsInsideViewport(viewportPoint) && IsInside(targetLocal, panelRect);
            Vector2 arrowLocal = targetInsidePanel
                ? ResolveOnscreenArrowPosition(targetLocal, panelRect, arrowRect.rect.size)
                : targetLocal;
            arrowLocal = ClampToSafeRect(arrowLocal, panelRect, arrowRect.rect.size);

            SetArrowLocalPosition(arrowLocal);
            Vector2 direction = targetLocal - arrowLocal;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector2.right;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
            ShowArrow();
        }

        private Vector2 ResolveOnscreenArrowPosition(Vector2 targetLocal, Rect panelRect, Vector2 arrowSize)
        {
            Vector2 center = panelRect.center;
            Vector2 centerToTarget = targetLocal - center;
            if (centerToTarget.sqrMagnitude < 0.001f)
            {
                return center;
            }

            float targetDistance = centerToTarget.magnitude;
            float arrowClearance = Mathf.Max(arrowSize.x, arrowSize.y);
            float distanceFromCenter = Mathf.Min(onscreenCenterRadius, Mathf.Max(0f, targetDistance - arrowClearance));
            return center + centerToTarget.normalized * distanceFromCenter;
        }

        private void SetArrowLocalPosition(Vector2 value)
        {
            Vector3 localPosition = arrowRect.localPosition;
            arrowRect.localPosition = new Vector3(value.x, value.y, localPosition.z);
        }

        private void ResolveMissingReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = transform as RectTransform;
            }

            if (arrowRect == null)
            {
                Transform arrowTransform = transform.Find("Arrow");
                if (arrowTransform != null)
                {
                    arrowRect = arrowTransform as RectTransform;
                }
            }

            if (arrowImage == null && arrowRect != null)
            {
                arrowImage = arrowRect.GetComponent<Image>();
            }
        }

        private Camera ResolveUICamera()
        {
            Canvas canvas = panelRoot != null ? panelRoot.GetComponentInParent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
        }

        private void DisableRaycastTargets()
        {
            if (arrowImage != null)
            {
                arrowImage.raycastTarget = false;
            }

            if (panelRoot != null && panelRoot.TryGetComponent(out Image panelImage))
            {
                panelImage.raycastTarget = false;
            }
        }

        private void ShowArrow()
        {
            if (arrowRect != null && !arrowRect.gameObject.activeSelf)
            {
                arrowRect.gameObject.SetActive(true);
            }
        }

        private void HideArrow()
        {
            if (arrowRect != null && arrowRect.gameObject.activeSelf)
            {
                arrowRect.gameObject.SetActive(false);
            }
        }

        private Vector2 ClampToSafeRect(Vector2 value, Rect panelRect, Vector2 arrowSize)
        {
            float horizontalInset = edgePadding + arrowSize.x * 0.5f;
            float verticalInset = edgePadding + arrowSize.y * 0.5f;
            return new Vector2(
                ClampAxis(value.x, panelRect.xMin + horizontalInset, panelRect.xMax - horizontalInset, panelRect.center.x),
                ClampAxis(value.y, panelRect.yMin + verticalInset, panelRect.yMax - verticalInset, panelRect.center.y));
        }

        private static float ClampAxis(float value, float min, float max, float fallback)
        {
            return min <= max ? Mathf.Clamp(value, min, max) : fallback;
        }

        private static bool IsInside(Vector2 value, Rect rect)
        {
            return value.x >= rect.xMin &&
                value.x <= rect.xMax &&
                value.y >= rect.yMin &&
                value.y <= rect.yMax;
        }

        private static bool IsInsideViewport(Vector3 viewportPoint)
        {
            return viewportPoint.x >= 0f &&
                viewportPoint.x <= 1f &&
                viewportPoint.y >= 0f &&
                viewportPoint.y <= 1f;
        }
    }
}
