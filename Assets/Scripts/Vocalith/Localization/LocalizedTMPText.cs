using TMPro;
using UnityEngine;

namespace Vocalith.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTMPText : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private string localizationKey = string.Empty;
        [SerializeField, TextArea] private string fallbackText = string.Empty;
        [SerializeField] private bool captureCurrentTextAsFallback = true;

        public string LocalizationKey
        {
            get => localizationKey;
            set
            {
                localizationKey = value != null ? value.Trim() : string.Empty;
                Refresh();
            }
        }

        public string FallbackText
        {
            get => fallbackText ?? string.Empty;
            set
            {
                fallbackText = value ?? string.Empty;
                Refresh();
            }
        }

        public void SetKey(string key, string fallback = null)
        {
            localizationKey = key != null ? key.Trim() : string.Empty;
            if (fallback != null)
            {
                fallbackText = fallback;
            }

            Refresh();
        }

        public void Refresh()
        {
            ResolveTarget();
            if (targetText == null)
                return;

            CaptureFallbackIfNeeded();
            targetText.text = string.IsNullOrWhiteSpace(localizationKey)
                ? FallbackText
                : LocalizationManager.TranslateOrDefault(localizationKey, FallbackText);
        }

        void Reset()
        {
            ResolveTarget();
            CaptureFallbackIfNeeded();
        }

        void Awake()
        {
            ResolveTarget();
            CaptureFallbackIfNeeded();
        }

        void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= Refresh;
        }

        void OnValidate()
        {
            ResolveTarget();
            localizationKey = localizationKey != null ? localizationKey.Trim() : string.Empty;
            fallbackText ??= string.Empty;
        }

        void ResolveTarget()
        {
            if (targetText == null)
            {
                targetText = GetComponent<TMP_Text>();
            }
        }

        void CaptureFallbackIfNeeded()
        {
            if (!captureCurrentTextAsFallback || targetText == null || !string.IsNullOrEmpty(fallbackText))
                return;

            fallbackText = targetText.text ?? string.Empty;
        }
    }
}
