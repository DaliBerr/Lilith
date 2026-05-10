using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// Info Popup prefab 的运行时控制脚本，负责正文文本写入与按钮行为绑定。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Info Popup")]
    public class PopUpUIScreen : GameUIScreen
    {
        [Header("Layout")]
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private Button topCloseButton;
        [SerializeField] private TMP_Text topCloseButtonText;
        [SerializeField] private RectTransform mainContent;
        [SerializeField] private RectTransform infoPanel;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private RectTransform buttonPanel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text closeButtonText;

        private UnityAction confirmAction;
        private UnityAction closeAction;
        private UnityAction topCloseAction;
        private bool closeAfterConfirm = true;
        private bool closeAfterClose = true;

        public override Status currentStatus { get; } = StatusList.PopUpStatus;

        public RectTransform TopPanel => topPanel;
        public Button TopCloseButton => topCloseButton;
        public TMP_Text TopCloseButtonText => topCloseButtonText;
        public RectTransform MainContent => mainContent;
        public RectTransform InfoPanel => infoPanel;
        public TMP_Text InfoText => infoText;
        public RectTransform ButtonPanel => buttonPanel;
        public Button ConfirmButton => confirmButton;
        public TMP_Text ConfirmButtonText => confirmButtonText;
        public Button CloseButton => closeButton;
        public TMP_Text CloseButtonText => closeButtonText;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            ResetConfiguredActions();
            BindButtonCallbacks();
        }

        protected override void OnAfterHide()
        {
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            UnbindButtonCallbacks();
            RemoveCurrentStatus();
            ResetConfiguredActions();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind Info Popup Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 统一配置正文内容、底部确认按钮和底部关闭按钮，供外部直接把 Info Popup 当作通用弹窗使用。
        /// param name="message": 需要显示在正文区域的文本
        /// param name="onConfirm": 点击确认按钮后执行的回调
        /// param name="onClose": 点击底部关闭按钮后执行的回调
        /// param name="confirmLabel": 确认按钮文案；为空时保留 prefab 默认值
        /// param name="closeLabel": 关闭按钮文案；为空时保留 prefab 默认值
        /// param name="shouldCloseAfterConfirm": 点击确认后是否自动关闭弹窗
        /// param name="shouldCloseAfterClose": 点击底部关闭后是否自动关闭弹窗
        /// param name="onTopClose": 点击顶部关闭按钮后执行的回调
        /// returns: 无
        /// </summary>
        public void Configure(
            string message,
            UnityAction onConfirm = null,
            UnityAction onClose = null,
            string confirmLabel = null,
            string closeLabel = null,
            bool shouldCloseAfterConfirm = true,
            bool shouldCloseAfterClose = true,
            UnityAction onTopClose = null)
        {
            TryAutoBindReferences();
            SetInfoText(message);
            SetConfirmButton(confirmLabel, onConfirm, shouldCloseAfterConfirm);
            SetCloseButton(closeLabel, onClose, shouldCloseAfterClose);
            topCloseAction = onTopClose;
        }

        /// <summary>
        /// summary: 写入弹窗正文文本；传入 null 时会清空正文，避免显示旧内容。
        /// param name="message": 需要显示的正文字符串
        /// returns: 无
        /// </summary>
        public void SetInfoText(string message)
        {
            if (infoText == null)
            {
                return;
            }

            infoText.text = message ?? string.Empty;
        }

        /// <summary>
        /// summary: 配置底部确认按钮的文案和点击行为。
        /// param name="label": 确认按钮文案；为空时保留 prefab 默认值
        /// param name="callback": 点击确认后的外部回调
        /// param name="shouldClosePopup": 点击确认后是否自动关闭当前弹窗
        /// returns: 无
        /// </summary>
        public void SetConfirmButton(string label = null, UnityAction callback = null, bool shouldClosePopup = true)
        {
            if (!string.IsNullOrEmpty(label) && confirmButtonText != null)
            {
                confirmButtonText.text = label;
            }

            confirmAction = callback;
            closeAfterConfirm = shouldClosePopup;
        }

        /// <summary>
        /// summary: 配置底部关闭按钮的文案和点击行为。
        /// param name="label": 关闭按钮文案；为空时保留 prefab 默认值
        /// param name="callback": 点击关闭后的外部回调
        /// param name="shouldClosePopup": 点击底部关闭后是否自动关闭当前弹窗
        /// returns: 无
        /// </summary>
        public void SetCloseButton(string label = null, UnityAction callback = null, bool shouldClosePopup = true)
        {
            if (!string.IsNullOrEmpty(label) && closeButtonText != null)
            {
                closeButtonText.text = label;
            }

            closeAction = callback;
            closeAfterClose = shouldClosePopup;
        }

        /// <summary>
        /// summary: 主动请求关闭当前弹窗；兼容它被作为 Screen 或 Modal 两种方式打开。
        /// param: 无
        /// returns: 无
        /// </summary>
        public void RequestClose()
        {
            if (ui == null)
            {
                return;
            }

            if (ui.GetTopModal() == this)
            {
                ui.CloseTopModal();
                return;
            }

            if (ui.GetTopScreen() == this)
            {
                ui.PopScreen();
            }
        }

        /// <summary>
        /// summary: 按当前 Info Popup prefab 的层级自动补齐正文和按钮引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            topPanel ??= transform.Find("Top Panel") as RectTransform;
            topCloseButton ??= FindButton("Top Panel/Close Button/Edge/Button");
            if (topCloseButton != null)
            {
                topCloseButtonText ??= topCloseButton.GetComponentInChildren<TMP_Text>(true);
            }

            mainContent ??= transform.Find("Main Content") as RectTransform;
            if (mainContent == null)
            {
                return;
            }

            infoPanel ??= mainContent.Find("Info") as RectTransform;
            infoText ??= mainContent.Find("Info/Text")?.GetComponent<TMP_Text>();
            buttonPanel ??= mainContent.Find("Button") as RectTransform;

            confirmButton ??= FindButton("Main Content/Button/Confirm Buton/Edge/Button");
            if (confirmButton != null)
            {
                confirmButtonText ??= confirmButton.GetComponentInChildren<TMP_Text>(true);
            }

            closeButton ??= FindButton("Main Content/Button/Close Button/Edge/Button");
            if (closeButton != null)
            {
                closeButtonText ??= closeButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>
        /// summary: 绑定 prefab 内三个关闭/确认按钮的统一入口，避免重复注册。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            BindButton(topCloseButton, HandleTopCloseButtonClicked);
            BindButton(confirmButton, HandleConfirmButtonClicked);
            BindButton(closeButton, HandleCloseButtonClicked);
        }

        /// <summary>
        /// summary: 清理 Info Popup 按钮事件，避免对象销毁后残留无效委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            UnbindButton(topCloseButton, HandleTopCloseButtonClicked);
            UnbindButton(confirmButton, HandleConfirmButtonClicked);
            UnbindButton(closeButton, HandleCloseButtonClicked);
        }

        /// <summary>
        /// summary: 响应顶部关闭按钮；它只关闭当前弹窗，不触发底部按钮的外部回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleTopCloseButtonClicked()
        {
            RequestClose();
            topCloseAction?.Invoke();
        }

        /// <summary>
        /// summary: 响应底部确认按钮；按配置决定是否先关闭弹窗，再执行外部确认回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleConfirmButtonClicked()
        {
            InvokeButtonAction(confirmAction, closeAfterConfirm);
        }

        /// <summary>
        /// summary: 响应底部关闭按钮；按配置决定是否关闭弹窗，并执行外部关闭回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleCloseButtonClicked()
        {
            InvokeButtonAction(closeAction, closeAfterClose);
        }

        /// <summary>
        /// summary: 统一执行弹窗按钮的“关闭自己 + 调外部回调”顺序，避免不同按钮分散实现。
        /// param name="callback": 当前按钮需要执行的外部回调
        /// param name="shouldClosePopup": 点击后是否自动关闭当前弹窗
        /// returns: 无
        /// </summary>
        private void InvokeButtonAction(UnityAction callback, bool shouldClosePopup)
        {
            if (shouldClosePopup)
            {
                RequestClose();
            }

            callback?.Invoke();
        }

        /// <summary>
        /// summary: 重置本次弹窗实例的外部按钮行为配置，避免旧回调意外沿用到下一次实例化。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ResetConfiguredActions()
        {
            confirmAction = null;
            closeAction = null;
            topCloseAction = null;
            closeAfterConfirm = true;
            closeAfterClose = true;
        }

        /// <summary>
        /// summary: 按层级路径查找 Button 组件，供自动绑定复用。
        /// param name="relativePath": 相对当前 prefab 根节点的层级路径
        /// returns: 找到时返回 Button，否则返回 null
        /// </summary>
        private Button FindButton(string relativePath)
        {
            return transform.Find(relativePath)?.GetComponent<Button>();
        }

        /// <summary>
        /// summary: 为单个按钮安全绑定点击事件，避免重复注册。
        /// param name="button": 目标按钮
        /// param name="callback": 目标回调
        /// returns: 无
        /// </summary>
        private static void BindButton(Button button, UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(callback);
        }

        /// <summary>
        /// summary: 为单个按钮安全移除点击事件。
        /// param name="button": 目标按钮
        /// param name="callback": 目标回调
        /// returns: 无
        /// </summary>
        private static void UnbindButton(Button button, UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
        }

        /// <summary>
        /// summary: 在弹窗关闭或销毁时移除 PopUp 状态，避免状态残留影响输入路由。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }
    }
}
