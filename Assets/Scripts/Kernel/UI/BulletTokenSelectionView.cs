using Kernel.Bullet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    /// <summary>
    /// 单张 BulletToken 选择卡片的运行时绑定脚本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletTokenSelectionView : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private RectTransform tokenRoot;
        [SerializeField] private TMP_Text tokenText;
        [SerializeField] private RectTransform descriptionRoot;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectButtonText;

        [Header("Defaults")]
        [SerializeField] private string defaultSelectButtonLabel = "Select";

        private TokenSelectUIScreen ownerScreen;
        private PlaceableTokenData boundToken;
        private RectTransform rectTransform;

        /// <summary>
        /// summary: 当前卡片绑定的 token。
        /// param: 无
        /// returns: 当前卡片绑定的 token
        /// </summary>
        public PlaceableTokenData BoundToken => boundToken;

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
        }

        /// <summary>
        /// summary: 由 Token Select 屏幕绑定当前卡片对应的 token 与点击回调宿主。
        /// param name="owner": 当前卡片所属的 Token Select 屏幕
        /// param name="token": 需要展示的 token
        /// returns: 无
        /// </summary>
        public void Bind(TokenSelectUIScreen owner, PlaceableTokenData token)
        {
            ownerScreen = owner;
            boundToken = token;
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
                tokenText.text = boundToken != null ? boundToken.GetPickupDisplayText() : string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = boundToken != null ? boundToken.GetSelectionDescription() : string.Empty;
            }

            if (selectButtonText != null && string.IsNullOrWhiteSpace(selectButtonText.text))
            {
                selectButtonText.text = defaultSelectButtonLabel;
            }

            if (selectButton != null)
            {
                selectButton.interactable = boundToken != null;
            }
        }

        /// <summary>
        /// summary: 手动触发当前卡片的选择逻辑，便于测试和其他自动化入口调用。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestSelect()
        {
            if (boundToken == null)
            {
                return;
            }

            ownerScreen?.RequestSelection(boundToken);
        }

        /// <summary>
        /// summary: 按当前 prefab 层级自动补齐 token、description 与 button 引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            rectTransform ??= transform as RectTransform;
            tokenRoot ??= transform.Find("Token") as RectTransform;
            tokenText ??= transform.Find("Token/Text")?.GetComponent<TMP_Text>();
            descriptionRoot ??= transform.Find("Description") as RectTransform;
            descriptionText ??= transform.Find("Description/Text")?.GetComponent<TMP_Text>();
            selectButton ??= transform.Find("Button")?.GetComponent<Button>();
            if (selectButton != null)
            {
                selectButtonText ??= selectButton.GetComponentInChildren<TMP_Text>(true);
            }
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
            if (boundToken == null)
            {
                return;
            }

            ownerScreen?.RequestSelection(boundToken);
        }
    }
}
