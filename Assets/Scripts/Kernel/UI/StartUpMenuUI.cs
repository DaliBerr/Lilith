using System.Collections;
using Kernel.GameState;
using TMPro;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
        private static readonly Vocalith.Random RandomSource = new();

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject loadButtonRoot;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Seal")]
        [SerializeField] private Image sealImage;

        private static readonly Color DefaultButtonTextColor = Color.white;
        private static readonly Color HoverButtonTextColor = new Color(78f / 255f, 69f / 255f, 60f / 255f, 1f);
        private static readonly string[] SealSpriteAddresses =
        {
            "Assets/Art/UI/Start up/Seal/\u4ED9",
            "Assets/Art/UI/Start up/Seal/\u547D",
            "Assets/Art/UI/Start up/Seal/\u751F",
            "Assets/Art/UI/Start up/Seal/\u901D",
            "Assets/Art/UI/Start up/Seal/\u5BFF",
            "Assets/Art/UI/Start up/Seal/\u859B",
            "Assets/Art/UI/Start up/Seal/\u5BFB",
            "Assets/Art/UI/Start up/Seal/\u9038",
            "Assets/Art/UI/Start up/Seal/\u90B9",
            "Assets/Art/UI/Start up/Seal/\u9EC4"
        };

        private bool isHandlingStartRequest;
        private bool isOpeningProfileModal;
        private bool isOpeningExitConfirmation;
        private Coroutine sealSpriteLoadCoroutine;
        private AsyncOperationHandle<Sprite> activeSealSpriteHandle;
        private AsyncOperationHandle<Sprite> loadingSealSpriteHandle;

        public override Status currentStatus { get; } = StatusList.InMainMenuStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            EnsureButtonRootsVisible();
            ConfigureButtonVisuals();
            BeginRandomSealSpriteLoad();
            isHandlingStartRequest = false;
            isOpeningProfileModal = false;
            isOpeningExitConfirmation = false;
            SetButtonsInteractable(true);
            BindButtonCallbacks();
        }

        protected override void OnAfterHide()
        {
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            StopSealSpriteLoad();
            ReleaseSealSpriteHandles();
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
        /// summary: 按当前 StartUp UI Prefab 的层级自动补齐四个菜单按钮引用。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            if (startButton == null)
            {
                startButton = FindButton("Button Panel/Start");
                startButton ??= FindButton("Button Panel/Start Button");
            }

            loadButtonRoot ??= FindInContentSafeFrame("Button Panel/Load")?.gameObject;
            loadButtonRoot ??= FindInContentSafeFrame("Button Panel/Load Button")?.gameObject;
            if (loadButton == null)
            {
                loadButton = loadButtonRoot != null ? loadButtonRoot.GetComponent<Button>() : null;
                loadButton ??= FindButton("Button Panel/Load");
                loadButton ??= FindButton("Button Panel/Load Button");
            }

            if (settingsButton == null)
            {
                settingsButton = FindButton("Button Panel/Settings");
                settingsButton ??= FindButton("Button Panel/Option Button");
            }

            if (quitButton == null)
            {
                quitButton = FindButton("Button Panel/Quit");
                quitButton ??= FindButton("Button Panel/Quit Button");
            }

            sealImage ??= FindInContentSafeFrame("Seal Panel")?.GetComponent<Image>();
        }

        /// <summary>
        /// summary: 每次启动菜单初始化时随机加载一张 Addressables Seal 图片。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BeginRandomSealSpriteLoad()
        {
            if (!Application.isPlaying || sealImage == null || SealSpriteAddresses.Length == 0)
            {
                return;
            }

            StopSealSpriteLoad();
            sealSpriteLoadCoroutine = StartCoroutine(LoadRandomSealSpriteCoroutine());
        }

        /// <summary>
        /// summary: 从 Seal Addressables 地址列表中随机挑选并加载一张 Sprite。
        /// param: 无
        /// returns: 可供协程等待的枚举器
        /// </summary>
        private IEnumerator LoadRandomSealSpriteCoroutine()
        {
            int startIndex = RandomSource.Next(0, SealSpriteAddresses.Length);
            for (int offset = 0; offset < SealSpriteAddresses.Length; offset++)
            {
                string address = SealSpriteAddresses[(startIndex + offset) % SealSpriteAddresses.Length];
                if (string.IsNullOrWhiteSpace(address))
                {
                    continue;
                }

                loadingSealSpriteHandle = Addressables.LoadAssetAsync<Sprite>(address);
                yield return loadingSealSpriteHandle;

                AsyncOperationHandle<Sprite> completedHandle = loadingSealSpriteHandle;
                loadingSealSpriteHandle = default;
                if (completedHandle.Status == AsyncOperationStatus.Succeeded && completedHandle.Result != null)
                {
                    ReleaseActiveSealSpriteHandle();
                    activeSealSpriteHandle = completedHandle;
                    sealImage.sprite = completedHandle.Result;
                    sealSpriteLoadCoroutine = null;
                    yield break;
                }

                if (completedHandle.IsValid())
                {
                    Addressables.Release(completedHandle);
                }
            }

            GameDebug.LogWarning("[StartUpMenuUI] Failed to load any startup seal sprite from Addressables.");
            sealSpriteLoadCoroutine = null;
        }

        /// <summary>
        /// summary: 停止当前 Seal Addressables 加载协程并释放正在加载的句柄。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void StopSealSpriteLoad()
        {
            if (sealSpriteLoadCoroutine != null)
            {
                StopCoroutine(sealSpriteLoadCoroutine);
                sealSpriteLoadCoroutine = null;
            }

            ReleaseLoadingSealSpriteHandle();
        }

        /// <summary>
        /// summary: 释放 Seal Sprite 的 Addressables 句柄。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ReleaseSealSpriteHandles()
        {
            ReleaseLoadingSealSpriteHandle();
            ReleaseActiveSealSpriteHandle();
        }

        private void ReleaseLoadingSealSpriteHandle()
        {
            if (loadingSealSpriteHandle.IsValid())
            {
                Addressables.Release(loadingSealSpriteHandle);
                loadingSealSpriteHandle = default;
            }
        }

        private void ReleaseActiveSealSpriteHandle()
        {
            if (activeSealSpriteHandle.IsValid())
            {
                Addressables.Release(activeSealSpriteHandle);
                activeSealSpriteHandle = default;
            }
        }

        /// <summary>
        /// summary: 确保启动菜单四个按钮根节点都默认显示。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureButtonRootsVisible()
        {
            SetButtonRootActive(startButton, true);
            SetObjectActive(loadButtonRoot, true);
            SetButtonRootActive(loadButton, true);
            SetButtonRootActive(settingsButton, true);
            SetButtonRootActive(quitButton, true);
        }

        /// <summary>
        /// summary: 配置四个启动菜单按钮的默认和 hover 视觉状态。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ConfigureButtonVisuals()
        {
            ConfigureButtonVisual(startButton);
            ConfigureButtonVisual(loadButton);
            ConfigureButtonVisual(settingsButton);
            ConfigureButtonVisual(quitButton);
        }

        /// <summary>
        /// summary: 配置单个按钮背景和 TMP 文字的 hover 反馈。
        /// param name="button": 目标按钮
        /// returns: 无
        /// </summary>
        private static void ConfigureButtonVisual(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.None;
            Image backgroundImage = button.image != null ? button.image : button.GetComponent<Image>();
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            StartUpButtonHoverFeedback hoverFeedback = button.GetComponent<StartUpButtonHoverFeedback>();
            if (hoverFeedback == null)
            {
                hoverFeedback = button.gameObject.AddComponent<StartUpButtonHoverFeedback>();
            }

            hoverFeedback.Configure(backgroundImage, label, DefaultButtonTextColor, HoverButtonTextColor);
        }

        /// <summary>
        /// summary: 绑定启动菜单四个按钮回调。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void BindButtonCallbacks()
        {
            BindButton(startButton, HandleStartButtonClicked);
            BindButton(loadButton, HandleLoadButtonClicked);
            BindButton(settingsButton, HandleSettingsButtonClicked);
            BindButton(quitButton, HandleQuitButtonClicked);
        }

        /// <summary>
        /// summary: 清理启动菜单四个按钮回调，避免对象销毁后残留委托。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void UnbindButtonCallbacks()
        {
            UnbindButton(startButton, HandleStartButtonClicked);
            UnbindButton(loadButton, HandleLoadButtonClicked);
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
            SetButtonInteractable(loadButton, interactable);
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
            if (!interactable && button.TryGetComponent(out StartUpButtonHoverFeedback hoverFeedback))
            {
                hoverFeedback.ApplyDefaultState();
            }
        }

        /// <summary>
        /// summary: 安全切换按钮所在 GameObject 的显示状态。
        /// param name="button": 目标按钮
        /// param name="isActive": 目标显示状态
        /// returns: 无
        /// </summary>
        private static void SetButtonRootActive(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            SetObjectActive(button.gameObject, isActive);
        }

        /// <summary>
        /// summary: 安全切换 GameObject 的显示状态。
        /// param name="target": 目标对象
        /// param name="isActive": 目标显示状态
        /// returns: 无
        /// </summary>
        private static void SetObjectActive(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
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
        /// summary: 通过标准 modal 流程打开设置界面。
        /// param: 无
        /// returns: 可供协程等待的枚举器
        /// </summary>
        private IEnumerator ShowOptionsModal()
        {
            if (ui == null)
            {
                GameDebug.LogWarning("[StartUpMenuUI] UIManager is missing. Unable to open Options modal.");
                yield break;
            }

            yield return ui.ShowModalAndWait<OptionsUIScreen>();
        }

        /// <summary>
        /// summary: 通过通用 Info Popup 打开退出游戏确认弹窗。
        /// param: 无
        /// returns: 可供协程等待的枚举器
        /// </summary>
        private IEnumerator ShowExitConfirmationModal()
        {
            if (ui == null)
            {
                GameDebug.LogWarning("[StartUpMenuUI] UIManager is missing. Unable to open exit confirmation.");
                yield break;
            }

            isOpeningExitConfirmation = true;
            SetButtonsInteractable(false);
            try
            {
                yield return GameExitUIUtility.ShowExitConfirmation(ui, nameof(StartUpMenuUI));
            }
            finally
            {
                isOpeningExitConfirmation = false;
                SetButtonsInteractable(true);
            }
        }

        /// <summary>
        /// summary: 响应开始按钮，直接在最小空槽位创建新档并进入新档流程。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleStartButtonClicked()
        {
            if (isHandlingStartRequest || isOpeningProfileModal || isOpeningExitConfirmation)
            {
                return;
            }

            if (!StartupFlowBridge.HasStartup)
            {
                GameDebug.LogError("[StartUpMenuUI] GlobalStartup instance is missing.");
                return;
            }

            if (!StartupFlowBridge.IsBootCompleted)
            {
                GameDebug.LogWarning("[StartUpMenuUI] GlobalStartup is still booting.");
                return;
            }

            if (ui == null || ui.IsNavigating() || ui.GetTopModal() != null)
            {
                return;
            }

            RuntimeSaveService saveService = RuntimeSaveService.GetOrCreateInstance();
            if (saveService == null)
            {
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(StartUpMenuUI),
                    Vocalith.Localization.LocalizationManager.TranslateOrDefault("ui.profile.save_unavailable", "存档服务不可用。")));
                return;
            }

            if (!saveService.CreateProfileInNextEmptySlot(out int createdSlotIndex))
            {
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(StartUpMenuUI),
                    Vocalith.Localization.LocalizationManager.TranslateOrDefault("ui.profile.create_failed", "新建存档失败。")));
                return;
            }

            bool requestAccepted = StartupFlowBridge.RequestStartGame();
            if (!requestAccepted)
            {
                saveService.DeleteProfileSlot(createdSlotIndex);
                StartCoroutine(PopUpUIUtility.ShowInfoPopup(
                    ui,
                    nameof(StartUpMenuUI),
                    Vocalith.Localization.LocalizationManager.TranslateOrDefault("ui.profile.enter_game_failed", "当前无法进入游戏，请稍后再试。")));
                return;
            }

            isHandlingStartRequest = true;
            SetButtonsInteractable(false);
        }

        /// <summary>
        /// summary: 响应加载按钮，打开 Profile 弹窗供玩家手动选择已有存档。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleLoadButtonClicked()
        {
            if (isHandlingStartRequest || isOpeningProfileModal || isOpeningExitConfirmation)
            {
                return;
            }

            if (!StartupFlowBridge.HasStartup)
            {
                GameDebug.LogError("[StartUpMenuUI] GlobalStartup instance is missing.");
                return;
            }

            if (!StartupFlowBridge.IsBootCompleted)
            {
                GameDebug.LogWarning("[StartUpMenuUI] GlobalStartup is still booting.");
                return;
            }

            if (ui == null || ui.IsNavigating() || ui.GetTopModal() != null)
            {
                return;
            }

            StartCoroutine(ShowProfileManagementModal());
        }

        /// <summary>
        /// summary: 响应设置按钮，打开 JSON 驱动的设置弹窗。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleSettingsButtonClicked()
        {
            if (isHandlingStartRequest || isOpeningProfileModal || ui == null || ui.IsNavigating() || ui.GetTopModal() != null)
            {
                return;
            }

            StartCoroutine(ShowOptionsModal());
        }

        /// <summary>
        /// summary: 响应退出按钮，先打开确认弹窗，再由确认按钮执行真正退出。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void HandleQuitButtonClicked()
        {
            if (isHandlingStartRequest || isOpeningProfileModal || isOpeningExitConfirmation || ui == null || ui.IsNavigating() || ui.GetTopModal() != null)
            {
                return;
            }

            GameDebug.Log("[StartUpMenuUI] Quit requested from StartUp menu.");
            StartCoroutine(ShowExitConfirmationModal());
        }

        /// <summary>
        /// summary: 按按钮外壳路径查找 Button 组件，兼容直接挂载和子层级挂载。
        /// param name="relativePath": 相对当前 prefab 根节点的按钮外壳路径
        /// returns: 找到时返回 Button，否则返回 null
        /// </summary>
        private Button FindButton(string relativePath)
        {
            Transform target = FindInContentSafeFrame(relativePath);
            if (target == null)
            {
                return null;
            }

            Button button = target.GetComponent<Button>();
            return button != null ? button : target.GetComponentInChildren<Button>(true);
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
