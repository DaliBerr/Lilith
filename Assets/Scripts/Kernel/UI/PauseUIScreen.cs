using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// PauseUI 的运行时模板脚本，负责绑定暂停菜单按钮并维护暂停菜单状态入口。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/PauseUI")]
    public class PauseUIScreen : GameUIScreen
    {
        [Header("Layout")]
        [SerializeField] private Image backgroundMask;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private TMP_Text resumeButtonText;
        [SerializeField] private Button optionsButton;
        [SerializeField] private TMP_Text optionsButtonText;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backButtonText;

        public override Status currentStatus { get; } = StatusList.InPauseMenuStatus;

        public Image BackgroundMask => backgroundMask;
        public RectTransform MainPanel => mainPanel;
        public Button ResumeButton => resumeButton;
        public TMP_Text ResumeButtonText => resumeButtonText;
        public Button OptionsButton => optionsButton;
        public TMP_Text OptionsButtonText => optionsButtonText;
        public Button BackButton => backButton;
        public TMP_Text BackButtonText => backButtonText;

        protected override void OnInit()
        {
            TryAutoBindReferences();
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
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind Pause UI Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// 按当前 PauseUI prefab 的层级自动补齐暂停菜单引用。
        /// </summary>
        /// <returns>无。</returns>
        private void TryAutoBindReferences()
        {
            backgroundMask ??= GetComponent<Image>();
            mainPanel ??= transform.Find("Main Panel") as RectTransform;
            if (mainPanel == null)
            {
                return;
            }

            resumeButton ??= ResolveButton(mainPanel, "Resume Button", 0);
            if (resumeButton != null)
            {
                resumeButtonText ??= resumeButton.GetComponentInChildren<TMP_Text>(true);
            }

            optionsButton ??= ResolveButton(mainPanel, "Option Button", 1);
            if (optionsButton != null)
            {
                optionsButtonText ??= optionsButton.GetComponentInChildren<TMP_Text>(true);
            }

            backButton ??= ResolveButton(mainPanel, "Quit Button", 2);
            if (backButton != null)
            {
                backButtonText ??= backButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>
        /// summary: 把 PauseUI 中当前已经定义好的按钮事件接到统一的 UI 路由入口。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            BindButton(resumeButton, HandleResumeButtonClicked);
            BindButton(optionsButton, HandleOptionsButtonClicked);
            BindButton(backButton, HandleBackButtonClicked);
        }

        /// <summary>
        /// summary: 清理 PauseUI 按钮事件，避免对象销毁后残留无效委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            UnbindButton(resumeButton, HandleResumeButtonClicked);
            UnbindButton(optionsButton, HandleOptionsButtonClicked);
            UnbindButton(backButton, HandleBackButtonClicked);
        }

        /// <summary>
        /// summary: 点击恢复按钮时，通过 UIInputRouter 请求关闭当前暂停菜单。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleResumeButtonClicked()
        {
            UIInputRouter.Instance?.RequestClosePauseMenu();
        }

        /// <summary>
        /// summary: 点击设置按钮时，走统一的 UI 路由入口；当前先保留未实现提示。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleOptionsButtonClicked()
        {
            UIInputRouter.Instance?.RequestOpenPauseOptions();
        }

        /// <summary>
        /// summary: 点击返回按钮时，清空当前战斗 UI 并回到 StartUp 场景。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleBackButtonClicked()
        {
            UIInputRouter.Instance?.RequestReturnToStartUpScene();
        }

        /// <summary>
        /// 优先按层级名找按钮，找不到时再按子节点顺序回退。
        /// </summary>
        /// <param name="panel">暂停面板根节点。</param>
        /// <param name="childName">目标按钮子节点名。</param>
        /// <param name="fallbackIndex">回退使用的子节点索引。</param>
        /// <returns>解析到的按钮；未找到时返回 null。</returns>
        private static Button ResolveButton(Transform panel, string childName, int fallbackIndex)
        {
            if (panel == null)
            {
                return null;
            }

            Transform child = panel.Find(childName);
            if (child != null)
            {
                return child.GetComponentInChildren<Button>(true);
            }

            if (panel.childCount == 0)
            {
                return null;
            }

            int clampedIndex = Mathf.Clamp(fallbackIndex, 0, panel.childCount - 1);
            return panel.GetChild(clampedIndex).GetComponentInChildren<Button>(true);
        }

        /// <summary>
        /// summary: 为单个按钮安全绑定点击事件，避免重复注册。
        /// param name="button": 目标按钮
        /// param name="callback": 目标回调
        /// returns: 无
        /// </summary>
        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
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
        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(callback);
        }

        /// <summary>
        /// summary: 在暂停菜单关闭或销毁时移除 InPauseMenu 状态，避免状态残留。
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
