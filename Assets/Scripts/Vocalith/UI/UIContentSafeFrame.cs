using UnityEngine;

namespace Vocalith.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIContentSafeFrame : MonoBehaviour
    {
        public const float DefaultMaxAspect = 16f / 9f;

        [SerializeField, Min(0.01f)] private float maxAspect = DefaultMaxAspect;

        private RectTransform cachedRectTransform;

        public float MaxAspect
        {
            get => maxAspect;
            set
            {
                maxAspect = Mathf.Max(0.01f, value);
                ApplyNow();
            }
        }

        public static Vector2 CalculateConstrainedSize(Vector2 parentSize, float maxAspect)
        {
            if (!IsFinitePositive(parentSize.x) || !IsFinitePositive(parentSize.y) || !IsFinitePositive(maxAspect))
            {
                return Vector2.zero;
            }

            float parentAspect = parentSize.x / parentSize.y;
            if (parentAspect <= maxAspect)
            {
                return parentSize;
            }

            return new Vector2(parentSize.y * maxAspect, parentSize.y);
        }

        public void ApplyNow()
        {
            RectTransform rectTransform = ResolveRectTransform();
            if (rectTransform == null || rectTransform.parent is not RectTransform parent)
            {
                return;
            }

            Vector2 constrainedSize = CalculateConstrainedSize(parent.rect.size, maxAspect);
            if (constrainedSize == Vector2.zero)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = constrainedSize;
            rectTransform.localScale = Vector3.one;
        }

        private void OnEnable()
        {
            ApplyNow();
        }

        private void OnValidate()
        {
            maxAspect = Mathf.Max(0.01f, maxAspect);
            ApplyNow();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyNow();
        }

        private RectTransform ResolveRectTransform()
        {
            if (cachedRectTransform == null)
            {
                cachedRectTransform = transform as RectTransform;
            }

            return cachedRectTransform;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
