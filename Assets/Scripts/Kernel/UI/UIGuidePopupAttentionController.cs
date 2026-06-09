using TMPro;
using UnityEngine;
using Vocalith.Localization;

namespace Kernel.UI
{
    [DisallowMultipleComponent]
    public sealed class UIGuidePopupAttentionController : MonoBehaviour
    {
        private const string ClickHintKey = "ui.guide.popup.click_hint";
        private const string ClickHintFallback = "Click anywhere to continue";
        private static readonly Vocalith.Random RandomSource = new();

        [SerializeField] private RectTransform clickNotePanel;
        [SerializeField] private TMP_Text clickNoteText;
        [SerializeField] private Color idleTextColor = new(0.68f, 0f, 0.46f, 0.92f);
        [SerializeField] private Color pulseTextColor = new(1f, 0.8f, 0.22f, 1f);
        [SerializeField, Min(0f)] private float pulseSpeed = 3.2f;
        [SerializeField, Min(0f)] private float floatAmplitude = 3f;
        [SerializeField, Min(0f)] private float scaleAmplitude = 0.06f;

        private Vector2 baseAnchoredPosition;
        private Vector3 baseScale = Vector3.one;
        private float phaseOffset;
        private bool hasCapturedBaseState;

#if UNITY_INCLUDE_TESTS
        public RectTransform ClickNotePanelForTests => clickNotePanel;
        public TMP_Text ClickNoteTextForTests => clickNoteText;
#endif

        private void Awake()
        {
            TryAutoBindReferences();
            CaptureBaseStateIfNeeded();
            phaseOffset = RandomSource.NextFloat01() * Mathf.PI * 2f;
        }

        private void OnEnable()
        {
            TryAutoBindReferences();
            CaptureBaseStateIfNeeded();
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
            RefreshLocalizedText();
            ApplyAttentionVisual(Time.unscaledTime);
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
            RestoreBaseState();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
            CaptureBaseStateIfNeeded();
        }

        private void Update()
        {
            ApplyAttentionVisual(Time.unscaledTime);
        }

        private void TryAutoBindReferences()
        {
            clickNotePanel ??= transform.Find("Click Note Panel") as RectTransform;
            clickNoteText ??= transform.Find("Click Note Panel/Text (TMP)")?.GetComponent<TMP_Text>();
        }

        private void CaptureBaseStateIfNeeded()
        {
            if (hasCapturedBaseState || clickNotePanel == null)
            {
                return;
            }

            baseAnchoredPosition = clickNotePanel.anchoredPosition;
            baseScale = clickNotePanel.localScale;
            hasCapturedBaseState = true;
        }

        private void RefreshLocalizedText()
        {
            if (clickNoteText == null)
            {
                return;
            }

            clickNoteText.text = LocalizationManager.TranslateOrDefault(ClickHintKey, ClickHintFallback);
        }

        private void ApplyAttentionVisual(float unscaledTime)
        {
            if (clickNotePanel == null || clickNoteText == null)
            {
                return;
            }

            float t = (Mathf.Sin(unscaledTime * pulseSpeed + phaseOffset) + 1f) * 0.5f;
            clickNoteText.color = Color.Lerp(idleTextColor, pulseTextColor, t);
            clickNotePanel.anchoredPosition = baseAnchoredPosition + new Vector2(0f, Mathf.Lerp(-floatAmplitude, floatAmplitude, t));
            clickNotePanel.localScale = baseScale * (1f + scaleAmplitude * t);
        }

        private void RestoreBaseState()
        {
            if (!hasCapturedBaseState || clickNotePanel == null || clickNoteText == null)
            {
                return;
            }

            clickNotePanel.anchoredPosition = baseAnchoredPosition;
            clickNotePanel.localScale = baseScale;
            clickNoteText.color = idleTextColor;
        }
    }
}
