using System.Collections;
using Kernel.GameState;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    /// <summary>
    /// StartUp UI Prefab 的运行时控制脚本，负责绑定主菜单按钮。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/StartUp UI Prefab")]
    public sealed class StartUpMenuUI : GameUIScreen
    {
        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject loadButtonRoot;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        private bool isOpeningProfileModal;

        public override Status currentStatus { get; } = StatusList.InMainMenuStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            HideLegacyLoadButton();
            isOpeningProfileModal = false;
            SetButtonsInteractable(true);
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

        [ContextMenu("Auto Bind StartUp Menu")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 按当前 StartUp UI Prefab 的层级自动补齐按钮与旧 Load 节点引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            startButton ??= FindButton("Button Panel/Start Button/Edge/Button");
            loadButtonRoot ??= transform.Find("Button Panel/Load Button")?.gameObject;
            settingsButton ??= FindButton("Button Panel/Option Button/Edge/Button");
            quitButton ??= FindButton("Button Panel/Quit Button/Edge/Button");
        }

        /// <summary>
        /// summary: 隐藏旧的 Load 按钮，使开始流程统一走档位弹窗。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HideLegacyLoadButton()
        {
            if (loadButtonRoot != null && loadButtonRoot.activeSelf)
            {
                loadButtonRoot.SetActive(false);
            }
        }

        /// <summary>
        /// summary: 绑定启动菜单当前仍启用的按钮回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            BindButton(startButton, HandleStartButtonClicked);
            BindButton(settingsButton, HandleSettingsButtonClicked);
            BindButton(quitButton, HandleQuitButtonClicked);
        }

        /// <summary>
        /// summary: 清理启动菜单按钮回调，避免对象销毁后残留委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            UnbindButton(startButton, HandleStartButtonClicked);
            UnbindButton(settingsButton, HandleSettingsButtonClicked);
            UnbindButton(quitButton, HandleQuitButtonClicked);
        }

        /// <summary>
        /// summary: 统一切换启动菜单当前可见按钮的交互状态，供 profile modal 打开期间临时锁定输入。
        /// param name="interactable": 目标交互状态
        /// returns: 无
        /// </summary>
        private void SetButtonsInteractable(bool interactable)
        {
            SetButtonInteractable(startButton, interactable);
            SetButtonInteractable(settingsButton, interactable);
            SetButtonInteractable(quitButton, interactable);
        }

        /// <summary>
        /// summary: 安全切换单个按钮的 interactable 状态。
        /// param name="button": 目标按钮
        /// param name="interactable": 目标交互状态
        /// returns: 无
        /// </summary>
        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
        }

        /// <summary>
        /// summary: 通过标准 modal 流程打开 Profile 管理界面，并在打开期间临时锁定启动菜单按钮。
        /// param: 无
        /// returns: 可供协程等待的枚举器
        /// </summary>
        private IEnumerator ShowProfileManagementModal()
        {
            if (ui == null)
            {
                GameDebug.LogWarning("[StartUpMenuUI] UIManager is missing. Unable to open Profile modal.");
                yield break;
            }

            isOpeningProfileModal = true;
            SetButtonsInteractable(false);
            try
            {
                yield return ui.ShowModalAndWait<ProfileManagementUIScreen>();
            }
            finally
            {
                isOpeningProfileModal = false;
                SetButtonsInteractable(true);
            }
        }

        /// <summary>
        /// summary: 响应开始按钮，默认弹出四个固定存档栏位供玩家直接选择。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleStartButtonClicked()
        {
            if (isOpeningProfileModal)
            {
                return;
            }

            if (GlobalStartup.Instance == null)
            {
                GameDebug.LogError("[StartUpMenuUI] GlobalStartup instance is missing.");
                return;
            }

            if (!GlobalStartup.Instance.IsBootCompleted)
            {
                GameDebug.LogWarning("[StartUpMenuUI] GlobalStartup is still booting.");
                return;
            }

            StartCoroutine(ShowProfileManagementModal());
        }

        /// <summary>
        /// summary: 响应设置按钮，弹出统一的“功能未实现”提示窗口。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleSettingsButtonClicked()
        {
            StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                ui,
                nameof(StartUpMenuUI),
                "设置功能暂未实现，后续会在这里接入选项配置。"));
        }

        /// <summary>
        /// summary: 响应退出按钮，在编辑器中停止 Play，在构建中直接退出应用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleQuitButtonClicked()
        {
            GameDebug.Log("[StartUpMenuUI] Quit requested from StartUp menu.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
        /// summary: 在菜单界面关闭或销毁时移除 InMainMenu 状态，避免状态残留到后续场景。
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
