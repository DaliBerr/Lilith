using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vocalith.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIButtonPressScaleFeedback :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        ICancelHandler
    {
        private const float DefaultPressedScale = 0.94f;
        private const float DefaultPressDuration = 0.06f;
        private const float DefaultReleaseDuration = 0.08f;

        [SerializeField, Range(0.5f, 1f)] private float pressedScale = DefaultPressedScale;
        [SerializeField, Min(0f)] private float pressDuration = DefaultPressDuration;
        [SerializeField, Min(0f)] private float releaseDuration = DefaultReleaseDuration;

        private RectTransform targetTransform;
        private Button button;
        private Vector3 baseScale = Vector3.one;
        private Vector3 startScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;
        private float transitionDuration;
        private float transitionTime;
        private bool isPointerHeld;
        private bool isTransitioning;

        private void Awake()
        {
            CacheReferences();
            CaptureBaseScale();
        }

        private void OnEnable()
        {
            CacheReferences();
            CaptureBaseScale();
        }

        private void Update()
        {
            if (button != null && !button.interactable && isPointerHeld)
            {
                RestoreScale();
                return;
            }

            if (!isTransitioning || targetTransform == null)
            {
                return;
            }

            if (transitionDuration <= 0f)
            {
                targetTransform.localScale = targetScale;
                isTransitioning = false;
                return;
            }

            transitionTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(transitionTime / transitionDuration);
            targetTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, t);
            if (t >= 1f)
            {
                isTransitioning = false;
            }
        }

        private void OnDisable()
        {
            RestoreScaleImmediate();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable())
            {
                return;
            }

            isPointerHeld = true;
            baseScale = targetTransform.localScale;
            BeginTransition(baseScale * pressedScale, pressDuration);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            RestoreScale();
        }

        public void OnCancel(BaseEventData eventData)
        {
            RestoreScaleImmediate();
        }

        private void CacheReferences()
        {
            if (targetTransform == null)
            {
                targetTransform = transform as RectTransform;
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void CaptureBaseScale()
        {
            if (targetTransform == null || isPointerHeld)
            {
                return;
            }

            baseScale = targetTransform.localScale;
            startScale = baseScale;
            targetScale = baseScale;
        }

        private bool IsInteractable()
        {
            CacheReferences();
            return button != null && button.interactable && targetTransform != null;
        }

        private void RestoreScale()
        {
            isPointerHeld = false;
            BeginTransition(baseScale, releaseDuration);
        }

        private void RestoreScaleImmediate()
        {
            isPointerHeld = false;
            isTransitioning = false;
            if (targetTransform != null)
            {
                targetTransform.localScale = baseScale;
            }
        }

        private void BeginTransition(Vector3 scale, float duration)
        {
            if (targetTransform == null)
            {
                return;
            }

            startScale = targetTransform.localScale;
            targetScale = scale;
            transitionDuration = Mathf.Max(0f, duration);
            transitionTime = 0f;
            if (transitionDuration <= 0f)
            {
                targetTransform.localScale = targetScale;
                isTransitioning = false;
                return;
            }

            isTransitioning = true;
        }
    }
}
