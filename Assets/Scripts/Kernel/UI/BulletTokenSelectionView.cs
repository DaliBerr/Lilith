using System.Collections.Generic;
using Kernel.Bullet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.Localization;

namespace Kernel.UI
{
    /// <summary>
    /// 单张 BulletToken 选择卡片的运行时绑定脚本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletTokenSelectionView : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private Image rootImage;
        [SerializeField] private RectTransform tokenRoot;
        [SerializeField] private TMP_Text tokenText;
        [SerializeField] private RectTransform catalogRoot;
        [SerializeField] private TMP_Text catalogText;
        [SerializeField] private RectTransform descriptionRoot;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectButtonText;

        [Header("Defaults")]
        [SerializeField] private string defaultSelectButtonLabel = "Select";
        [SerializeField] private Color emptyTokenColor = new(1f, 1f, 1f, 1f);

        [Header("Type Colors")]
        [SerializeField] private Color coreTypeColor = new(1f, 0.73f, 0.36f, 1f);
        [SerializeField] private Color behaviorTypeColor = new(0.48f, 0.74f, 1f, 1f);
        [SerializeField] private Color resultTypeColor = new(1f, 0.53f, 0.53f, 1f);
        [SerializeField] private Color valueTypeColor = new(0.56f, 0.9f, 0.6f, 1f);
        [SerializeField] private Color modifierTypeColor = new(0.74f, 0.62f, 1f, 1f);
        [SerializeField] private Color multicastTypeColor = new(1f, 0.84f, 0.42f, 1f);
        [SerializeField] private Color triggerTypeColor = new(0.42f, 0.92f, 0.95f, 1f);
        [SerializeField] private Color payloadTypeColor = new(1f, 0.62f, 0.84f, 1f);
        [SerializeField] private Color spellBookTypeColor = new(0.48f, 0.86f, 0.68f, 1f);
        [SerializeField] private Color fallbackTypeColor = new(0.85f, 0.85f, 0.85f, 1f);

        private TokenSelectUIScreen ownerScreen;
        private PlaceableTokenData boundToken;
        private RunRewardOption boundReward;
        private RectTransform rectTransform;
        private readonly List<BaseTokenData> compileTokenBuffer = new();

        /// <summary>
        /// summary: 当前卡片绑定的 token。
        /// param: 无
        /// returns: 当前卡片绑定的 token
        /// </summary>
        public PlaceableTokenData BoundToken => boundToken;

        public RunRewardOption BoundReward => boundReward;

        /// <summary>
        /// summary: 当前卡片的 token 文本引用。
        /// param: 无
        /// returns: token 文本组件
        /// </summary>
        public TMP_Text TokenText => tokenText;

        /// <summary>
        /// summary: 当前卡片的描述文本引用。
        /// param: 无
        /// returns: 描述文本组件
        /// </summary>
        public TMP_Text DescriptionText => descriptionText;

        /// <summary>
        /// summary: 当前卡片的类型目录文本引用。
        /// param: 无
        /// returns: 类型目录文本组件
        /// </summary>
        public TMP_Text CatalogText => catalogText;

        /// <summary>
        /// summary: 当前卡片根节点背景图片引用。
        /// param: 无
        /// returns: 根节点 Image 组件
        /// </summary>
        public Image RootImage => rootImage;

        /// <summary>
        /// summary: 当前卡片的按钮引用。
        /// param: 无
        /// returns: 选择按钮组件
        /// </summary>
        public Button SelectButton => selectButton;

        private void Awake()
        {
            TryAutoBindReferences();
            BindButtonCallbacks();
            RefreshView();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
            RefreshView();
        }

        private void OnDestroy()
        {
            UnbindButtonCallbacks();
            ownerScreen = null;
            boundToken = null;
            boundReward = RunRewardOption.None;
        }

        /// <summary>
        /// summary: 由 Token Select 屏幕绑定当前卡片对应的 token 与点击回调宿主。
        /// param name="owner": 当前卡片所属的 Token Select 屏幕
        /// param name="token": 需要展示的 token
        /// returns: 无
        /// </summary>
        public void Bind(TokenSelectUIScreen owner, PlaceableTokenData token)
        {
            Bind(owner, RunRewardOption.FromToken(token));
        }

        public void Bind(TokenSelectUIScreen owner, RunRewardOption reward)
        {
            ownerScreen = owner;
            boundReward = reward;
            boundToken = reward.Kind == RunRewardOptionKind.Token ? reward.Token : null;
            TryAutoBindReferences();
            BindButtonCallbacks();
            RefreshView();
        }

        /// <summary>
        /// summary: 按当前绑定 token 刷新标题、描述和按钮显示。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RefreshView()
        {
            TryAutoBindReferences();

            if (tokenText != null)
            {
                tokenText.text = boundReward.IsValid ? boundReward.GetDisplayText() : string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = boundReward.IsValid ? boundReward.GetSelectionDescription() : string.Empty;
            }

            RefreshCatalogAndTypeVisuals();

            if (selectButtonText != null && string.IsNullOrWhiteSpace(selectButtonText.text))
            {
                selectButtonText.text = LocalizationManager.TranslateOrDefault(
                    "ui.token_select.select",
                    defaultSelectButtonLabel);
            }

            if (selectButton != null)
            {
                selectButton.interactable = boundReward.IsValid;
            }
        }

        /// <summary>
        /// summary: 手动触发当前卡片的选择逻辑，便于测试和其他自动化入口调用。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestSelect()
        {
            if (!boundReward.IsValid)
            {
                return;
            }

            ownerScreen?.RequestSelection(boundReward);
        }

        /// <summary>
        /// summary: 按当前 prefab 层级自动补齐 token、description 与 button 引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            rectTransform ??= transform as RectTransform;
            rootImage ??= GetComponent<Image>();
            tokenRoot ??= transform.Find("Token") as RectTransform;
            tokenText ??= transform.Find("Token/Text")?.GetComponent<TMP_Text>();
            catalogRoot ??= transform.Find("Catalog") as RectTransform;
            catalogText ??= transform.Find("Catalog/Text (TMP)")?.GetComponent<TMP_Text>();
            catalogText ??= transform.Find("Catalog/Text")?.GetComponent<TMP_Text>();
            if (catalogText == null && catalogRoot != null)
            {
                catalogText = catalogRoot.GetComponentInChildren<TMP_Text>(true);
            }

            descriptionRoot ??= transform.Find("Description") as RectTransform;
            descriptionText ??= transform.Find("Description/Text")?.GetComponent<TMP_Text>();
            selectButton ??= transform.Find("Button")?.GetComponent<Button>();
            if (selectButton != null)
            {
                selectButtonText ??= selectButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>
        /// summary: 刷新当前卡片的类型目录文案和根图染色。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RefreshCatalogAndTypeVisuals()
        {
            TokenType tokenType = ResolvePrimaryTokenType(boundToken);
            string catalogLabel = ResolveCatalogLabel(boundReward, tokenType);

            if (catalogText != null)
            {
                catalogText.text = catalogLabel;
            }

            if (catalogRoot != null)
            {
                catalogRoot.gameObject.SetActive(boundReward.IsValid && !string.IsNullOrWhiteSpace(catalogLabel));
            }

            if (rootImage != null)
            {
                rootImage.color = boundReward.IsValid ? ResolveTypeColor(boundReward, tokenType) : emptyTokenColor;
            }
        }

        /// <summary>
        /// summary: 解析当前可放置 token 的主类型，连锁 token 会取首个有效编译成员。
        /// param name="token": 需要解析类型的 token
        /// returns: 当前 token 的主类型
        /// </summary>
        private TokenType ResolvePrimaryTokenType(PlaceableTokenData token)
        {
            if (token == null)
            {
                return TokenType.None;
            }

            if (token is BaseTokenData baseToken)
            {
                return baseToken.TokenType;
            }

            compileTokenBuffer.Clear();
            token.AppendCompileTokens(compileTokenBuffer);
            TokenType fallbackType = TokenType.None;
            for (int i = 0; i < compileTokenBuffer.Count; i++)
            {
                BaseTokenData compileToken = compileTokenBuffer[i];
                if (compileToken == null)
                {
                    continue;
                }

                if (fallbackType == TokenType.None)
                {
                    fallbackType = compileToken.TokenType;
                }

                if (compileToken.TokenType != TokenType.None)
                {
                    return compileToken.TokenType;
                }
            }

            return fallbackType;
        }

        /// <summary>
        /// summary: 把 token 类型映射为 Selection 卡片上展示的中文目录文案。
        /// param name="tokenType": 当前 token 类型
        /// returns: 对应中文类型文案
        /// </summary>
        private static string ResolveCatalogLabel(RunRewardOption reward, TokenType tokenType)
        {
            if (reward.Kind == RunRewardOptionKind.SpellBook)
            {
                return LocalizationManager.TranslateOrDefault("ui.reward_type.spellbook", "法术书");
            }

            return tokenType switch
            {
                TokenType.Core => LocalizationManager.TranslateOrDefault("ui.token_type.core", "核心"),
                TokenType.Behavior => LocalizationManager.TranslateOrDefault("ui.token_type.behavior", "行为"),
                TokenType.Result => LocalizationManager.TranslateOrDefault("ui.token_type.result", "结果"),
                TokenType.Value => LocalizationManager.TranslateOrDefault("ui.token_type.value", "数值"),
                TokenType.Modifier => LocalizationManager.TranslateOrDefault("ui.token_type.modifier", "修饰"),
                TokenType.Multicast => LocalizationManager.TranslateOrDefault("ui.token_type.multicast", "多重"),
                TokenType.Trigger => LocalizationManager.TranslateOrDefault("ui.token_type.trigger", "触发"),
                TokenType.PayloadStart => LocalizationManager.TranslateOrDefault("ui.token_type.payload", "载荷"),
                TokenType.PayloadEnd => LocalizationManager.TranslateOrDefault("ui.token_type.payload", "载荷"),
                _ => string.Empty,
            };
        }

        /// <summary>
        /// summary: 解析当前 token 类型在 Selection 卡片上的背景颜色。
        /// param name="tokenType": 当前 token 类型
        /// returns: 对应的背景色
        /// </summary>
        private Color ResolveTypeColor(RunRewardOption reward, TokenType tokenType)
        {
            if (reward.Kind == RunRewardOptionKind.SpellBook)
            {
                return spellBookTypeColor;
            }

            return tokenType switch
            {
                TokenType.Core => coreTypeColor,
                TokenType.Behavior => behaviorTypeColor,
                TokenType.Result => resultTypeColor,
                TokenType.Value => valueTypeColor,
                TokenType.Modifier => modifierTypeColor,
                TokenType.Multicast => multicastTypeColor,
                TokenType.Trigger => triggerTypeColor,
                TokenType.PayloadStart => payloadTypeColor,
                TokenType.PayloadEnd => payloadTypeColor,
                _ => fallbackTypeColor,
            };
        }

        /// <summary>
        /// summary: 绑定当前卡片按钮的点击事件，避免重复注册。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            if (selectButton == null)
            {
                return;
            }

            selectButton.onClick.RemoveListener(HandleSelectButtonClicked);
            selectButton.onClick.AddListener(HandleSelectButtonClicked);
        }

        /// <summary>
        /// summary: 清理按钮点击事件，避免对象销毁后残留无效委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            if (selectButton == null)
            {
                return;
            }

            selectButton.onClick.RemoveListener(HandleSelectButtonClicked);
        }

        /// <summary>
        /// summary: 响应卡片按钮点击，把当前绑定 token 回传给所属的 Token Select 屏幕。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleSelectButtonClicked()
        {
            if (!boundReward.IsValid)
            {
                return;
            }

            ownerScreen?.RequestSelection(boundReward);
        }
    }
}
