using System;
using Kernel.Upgrade;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Vocalith.Localization;
using Vocalith.Logging;

namespace Kernel.UI
{
    public enum UpgradeNodeVisualState
    {
        Locked = 0,
        Available = 1,
        InsufficientRemnants = 2,
        Purchased = 3,
        Maxed = 4,
    }

    /// <summary>
    /// 永久升级科技树节点视图，负责 JSON 外观、图标和点击购买入口。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UpgradeNodeView : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private Image borderImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button button;

        [Header("Shape Sprites")]
        [SerializeField] private Sprite rectangleSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Sprite hexagonSprite;

        private AsyncOperationHandle<Sprite> iconHandle;
        private bool hasIconHandle;
        private string activeIconAddress;
        private Action<string> clickCallback;

        public string EntryId { get; private set; } = string.Empty;
        public UpgradeNodeVisualState VisualState { get; private set; } = UpgradeNodeVisualState.Locked;
        public Button PurchaseButton => button;

        private void Awake()
        {
            TryAutoBindReferences();
            EnsureButtonComponent();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        private void OnDestroy()
        {
            ReleaseIconHandle();
        }

        public void Bind(
            PermanentUpgradeEntryData entry,
            int currentLevel,
            int currentRemnants,
            bool prerequisitesMet,
            Action<string> onClicked)
        {
            if (entry == null)
            {
                return;
            }

            TryAutoBindReferences();
            EnsureButtonComponent();

            EntryId = entry.Id ?? string.Empty;
            clickCallback = onClicked;

            bool isMaxLevel = currentLevel >= entry.MaxLevel;
            bool canAfford = currentRemnants >= entry.CostRemnants;
            bool canPurchase = prerequisitesMet && !isMaxLevel && canAfford;
            VisualState = ResolveVisualState(prerequisitesMet, isMaxLevel, currentLevel, canAfford);

            if (titleText != null)
            {
                titleText.text = entry.Title;
            }

            if (costText != null)
            {
                costText.text = BuildCostText(entry, currentLevel, canAfford, prerequisitesMet, isMaxLevel);
            }

            ApplyShape(entry.Shape);
            ApplyColors(entry, VisualState);
            ApplyBorderInset(Mathf.Max(0f, entry.BorderWidth));
            LoadIcon(entry.IconAddress);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = canPurchase;
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void HandleClicked()
        {
            if (button != null && !button.interactable)
            {
                return;
            }

            if (!string.IsNullOrEmpty(EntryId))
            {
                clickCallback?.Invoke(EntryId);
            }
        }

        private static UpgradeNodeVisualState ResolveVisualState(
            bool prerequisitesMet,
            bool isMaxLevel,
            int currentLevel,
            bool canAfford)
        {
            if (!prerequisitesMet)
            {
                return UpgradeNodeVisualState.Locked;
            }

            if (isMaxLevel)
            {
                return UpgradeNodeVisualState.Maxed;
            }

            if (currentLevel > 0)
            {
                return UpgradeNodeVisualState.Purchased;
            }

            return canAfford ? UpgradeNodeVisualState.Available : UpgradeNodeVisualState.InsufficientRemnants;
        }

        private static string BuildCostText(
            PermanentUpgradeEntryData entry,
            int currentLevel,
            bool canAfford,
            bool prerequisitesMet,
            bool isMaxLevel)
        {
            string levelLine = LocalizationManager.TranslateFormatOrDefault(
                "ui.upgrade.level",
                "等级 {0}/{1}",
                currentLevel,
                entry.MaxLevel);

            string stateLine;
            if (!prerequisitesMet)
            {
                stateLine = LocalizationManager.TranslateOrDefault("ui.upgrade.locked", "未解锁");
            }
            else if (isMaxLevel)
            {
                stateLine = LocalizationManager.TranslateOrDefault("ui.upgrade.max_level", "已满级");
            }
            else if (canAfford)
            {
                stateLine = LocalizationManager.TranslateFormatOrDefault(
                    "ui.upgrade.cost",
                    "消耗 {0} 残卷",
                    entry.CostRemnants);
            }
            else
            {
                stateLine = LocalizationManager.TranslateFormatOrDefault(
                    "ui.upgrade.insufficient_remnants",
                    "残卷不足 ({0})",
                    entry.CostRemnants);
            }

            return $"{levelLine}\n{stateLine}";
        }

        private void ApplyShape(PermanentUpgradeNodeShape shape)
        {
            Sprite shapeSprite = ResolveShapeSprite(shape);
            if (shapeSprite == null)
            {
                return;
            }

            if (borderImage != null)
            {
                borderImage.sprite = shapeSprite;
                borderImage.type = Image.Type.Simple;
            }

            if (backgroundImage != null)
            {
                backgroundImage.sprite = shapeSprite;
                backgroundImage.type = Image.Type.Simple;
            }
        }

        private Sprite ResolveShapeSprite(PermanentUpgradeNodeShape shape)
        {
            return shape switch
            {
                PermanentUpgradeNodeShape.Circle => circleSprite,
                PermanentUpgradeNodeShape.Diamond => diamondSprite,
                PermanentUpgradeNodeShape.Hexagon => hexagonSprite,
                _ => rectangleSprite,
            };
        }

        private void ApplyColors(PermanentUpgradeEntryData entry, UpgradeNodeVisualState state)
        {
            Color borderColor = ParseColorOrDefault(entry.BorderColor, new Color(0.4f, 0.89f, 0.37f, 1f));
            Color backgroundColor = ParseColorOrDefault(entry.BackgroundColor, new Color(0.12f, 0.16f, 0.22f, 1f));

            if (state == UpgradeNodeVisualState.Locked)
            {
                borderColor = new Color(0.42f, 0.45f, 0.48f, 0.95f);
                backgroundColor = new Color(0.18f, 0.19f, 0.21f, 0.95f);
            }

            if (borderImage != null)
            {
                borderImage.color = borderColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
            }

            Color textColor = state == UpgradeNodeVisualState.Locked
                ? new Color(0.72f, 0.74f, 0.77f, 1f)
                : Color.white;
            if (titleText != null)
            {
                titleText.color = textColor;
            }

            if (costText != null)
            {
                costText.color = state == UpgradeNodeVisualState.InsufficientRemnants
                    ? new Color(1f, 0.66f, 0.42f, 1f)
                    : textColor;
            }
        }

        private void ApplyBorderInset(float borderWidth)
        {
            if (backgroundImage == null)
            {
                return;
            }

            RectTransform backgroundRect = backgroundImage.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = new Vector2(borderWidth, borderWidth);
            backgroundRect.offsetMax = new Vector2(-borderWidth, -borderWidth);
        }

        private void LoadIcon(string iconAddress)
        {
            string trimmedAddress = iconAddress != null ? iconAddress.Trim() : string.Empty;
            if (string.Equals(activeIconAddress, trimmedAddress, StringComparison.Ordinal) && hasIconHandle)
            {
                return;
            }

            ReleaseIconHandle();
            activeIconAddress = trimmedAddress;
            if (iconImage == null)
            {
                return;
            }

            iconImage.enabled = false;
            iconImage.sprite = null;

            if (string.IsNullOrEmpty(activeIconAddress))
            {
                return;
            }

            iconHandle = Addressables.LoadAssetAsync<Sprite>(activeIconAddress);
            hasIconHandle = true;
            iconHandle.Completed += HandleIconLoaded;
        }

        private void HandleIconLoaded(AsyncOperationHandle<Sprite> handle)
        {
            if (!hasIconHandle || !handle.Equals(iconHandle))
            {
                return;
            }

            if (iconImage == null)
            {
                ReleaseIconHandle();
                return;
            }

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                iconImage.sprite = handle.Result;
                iconImage.enabled = true;
                return;
            }

            iconImage.enabled = false;
            iconImage.sprite = null;
            GameDebug.LogWarning($"[UpgradeNodeView] Failed to load icon sprite at '{activeIconAddress}'.");
            ReleaseIconHandle();
        }

        private void ReleaseIconHandle()
        {
            if (!hasIconHandle)
            {
                return;
            }

            if (iconHandle.IsValid())
            {
                iconHandle.Completed -= HandleIconLoaded;
                Addressables.Release(iconHandle);
            }

            iconHandle = default;
            hasIconHandle = false;
            activeIconAddress = string.Empty;
        }

        private void EnsureButtonComponent()
        {
            button ??= GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }

            if (button.targetGraphic == null)
            {
                button.targetGraphic = borderImage != null ? borderImage : backgroundImage;
            }
        }

        private void TryAutoBindReferences()
        {
            borderImage ??= GetComponent<Image>();
            backgroundImage ??= transform.Find("Background")?.GetComponent<Image>();
            iconImage ??= transform.Find("Icon")?.GetComponent<Image>();
            titleText ??= transform.Find("Tittle")?.GetComponent<TMP_Text>();
            costText ??= transform.Find("Cost")?.GetComponent<TMP_Text>();
            button ??= GetComponent<Button>();
        }

        private static Color ParseColorOrDefault(string htmlColor, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(htmlColor, out Color parsedColor) ? parsedColor : fallback;
        }
    }
}
