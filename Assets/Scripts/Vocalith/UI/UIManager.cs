
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Vocalith.Logging;
using UnityEventSystem = UnityEngine.EventSystems.EventSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif


namespace Vocalith.UI
{
    public enum UILayer { Screen, Modal, Overlay, Toast }

    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Roots")]
        public Canvas rootCanvas;
        public RectTransform layerScreen;
        public RectTransform layerModal;
        public RectTransform layerOverlay;
        public RectTransform layerToast;

        [Header("Defaults")]
        public float defaultShow = 0.15f;
        public float defaultHide = 0.12f;

        private const float TargetAspectRatio = 16f / 9f;
        private const string AspectLetterboxRootName = "AspectLetterboxRoot";
        private const string AspectLetterboxTopBarName = "AspectLetterboxTopBar";
        private const string AspectLetterboxBottomBarName = "AspectLetterboxBottomBar";
        private const string AspectLetterboxLeftBarName = "AspectLetterboxLeftBar";
        private const string AspectLetterboxRightBarName = "AspectLetterboxRightBar";
        private const string AspectLetterboxContentName = "AspectLetterboxContent";

        private RectTransform aspectLetterboxRoot;
        private RectTransform aspectLetterboxTopBar;
        private RectTransform aspectLetterboxBottomBar;
        private RectTransform aspectLetterboxLeftBar;
        private RectTransform aspectLetterboxRightBar;
        private RectTransform aspectLetterboxContent;
        private CanvasScaler rootCanvasScaler;
        private Vector2 rootCanvasBaseReferenceResolution = new Vector2(1920f, 1080f);
        private bool hasRootCanvasBaseReferenceResolution;
        private Vector2Int lastAspectScreenSize = new Vector2Int(-1, -1);
        private bool hasAppliedAspectLockLayout;

        readonly Stack<UIScreen> screenStack = new();
        readonly Stack<UIScreen> modalStack = new();
        readonly Dictionary<GameObject, AsyncOperationHandle<GameObject>> addrInstances = new();
        readonly Dictionary<Type, UIScreen> overlayRegistry = new();

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            EnsureAspectLockRoots();
            ApplyAspectLockLayout();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        /// <summary>
        /// summary: 在切场景后统一收敛 EventSystem，避免持久化 UIManager 与场景内 EventSystem 重复。
        /// param name="scene": 刚完成加载的场景
        /// param name="mode": 场景加载模式
        /// returns: 无
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
            EnsureAspectLockRoots();
            ApplyAspectLockLayout();
        }

        /// <summary>
        /// summary: 在每个游戏相机渲染前应用 16:9 视口，确保黑边在屏幕边缘而不是拉伸内容。
        /// param name="context": 当前渲染上下文
        /// param name="camera": 即将渲染的相机
        /// returns: 无
        /// </summary>
        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game)
            {
                return;
            }

            EnsureAspectLockRoots();
            ApplyAspectLockLayout();
            ApplyCameraViewport(camera);
        }

        /// <summary>
        /// summary: 确保场景内始终只有一个兼容当前输入后端的 EventSystem。
        /// param: 无
        /// returns: 无
        /// </summary>
        private static void EnsureEventSystem()
        {
            var eventSystems = FindObjectsByType<UnityEventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            UnityEventSystem primary = null;

            foreach (var eventSystem in eventSystems)
            {
                if (eventSystem == null)
                {
                    continue;
                }

                if (primary == null)
                {
                    primary = eventSystem;
                    continue;
                }

                Destroy(eventSystem.gameObject);
            }

            if (primary == null)
            {
                primary = CreateEventSystem();
            }

            ConfigureInputModule(primary.gameObject);
            DontDestroyOnLoad(primary.gameObject);
        }

        /// <summary>
        /// summary: 创建一个新的 EventSystem 根对象，供没有场景级 EventSystem 的启动场景兜底。
        /// param: 无
        /// returns: 新创建的 EventSystem 组件
        /// </summary>
        private static UnityEventSystem CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(UnityEventSystem));
            return go.GetComponent<UnityEventSystem>();
        }

        /// <summary>
        /// summary: 按当前项目启用的输入方案配置 EventSystem 模块，避免旧输入模块在 Input System 下抛异常。
        /// param name="eventSystemObject": EventSystem 根对象
        /// returns: 无
        /// </summary>
        private static void ConfigureInputModule(GameObject eventSystemObject)
        {
            if (eventSystemObject == null)
            {
                return;
            }

            var standaloneModule = eventSystemObject.GetComponent<StandaloneInputModule>();
#if ENABLE_INPUT_SYSTEM
            if (standaloneModule != null)
            {
                Destroy(standaloneModule);
            }

            if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }
#else
            if (standaloneModule == null)
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }
        private bool _isNavigating;

        /// <summary>
        /// summary: 缓存 rootCanvas 对应的 CanvasScaler 以及其基准 referenceResolution。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void CacheRootCanvasScaler()
        {
            if (rootCanvas == null)
            {
                rootCanvasScaler = null;
                hasRootCanvasBaseReferenceResolution = false;
                return;
            }

            if (rootCanvasScaler == null || rootCanvasScaler.gameObject != rootCanvas.gameObject)
            {
                rootCanvasScaler = rootCanvas.GetComponent<CanvasScaler>();
                hasRootCanvasBaseReferenceResolution = false;
            }

            if (rootCanvasScaler == null || hasRootCanvasBaseReferenceResolution)
            {
                return;
            }

            Vector2 capturedResolution = rootCanvasScaler.referenceResolution;
            if (capturedResolution.x <= 0f || capturedResolution.y <= 0f)
            {
                capturedResolution = new Vector2(1920f, 1080f);
            }

            rootCanvasBaseReferenceResolution = capturedResolution;
            hasRootCanvasBaseReferenceResolution = true;
        }

        /// <summary>
        /// summary: 确保 16:9 安全区根节点与四条黑边存在；若首次创建，会把层容器迁移到内容容器下。
        /// param: 无
        /// returns: 若本次创建了新的容器则返回 true，否则返回 false
        /// </summary>
        private bool EnsureAspectLockRoots()
        {
            if (rootCanvas == null)
            {
                return false;
            }

            CacheRootCanvasScaler();

            bool createdRoot = false;

            if (aspectLetterboxRoot == null)
            {
                aspectLetterboxRoot = CreateAspectLetterboxRoot();
                createdRoot = true;
            }

            if (aspectLetterboxTopBar == null)
            {
                aspectLetterboxTopBar = CreateAspectLetterboxBar(AspectLetterboxTopBarName, aspectLetterboxRoot);
                createdRoot = true;
            }

            if (aspectLetterboxBottomBar == null)
            {
                aspectLetterboxBottomBar = CreateAspectLetterboxBar(AspectLetterboxBottomBarName, aspectLetterboxRoot);
                createdRoot = true;
            }

            if (aspectLetterboxLeftBar == null)
            {
                aspectLetterboxLeftBar = CreateAspectLetterboxBar(AspectLetterboxLeftBarName, aspectLetterboxRoot);
                createdRoot = true;
            }

            if (aspectLetterboxRightBar == null)
            {
                aspectLetterboxRightBar = CreateAspectLetterboxBar(AspectLetterboxRightBarName, aspectLetterboxRoot);
                createdRoot = true;
            }

            if (aspectLetterboxContent == null)
            {
                aspectLetterboxContent = CreateAspectLetterboxContent();
                createdRoot = true;
            }

            if (createdRoot)
            {
                hasAppliedAspectLockLayout = false;
            }

            return createdRoot;
        }

        /// <summary>
        /// summary: 根据当前屏幕尺寸更新 16:9 安全区域，并把 UI 层容器与相机视口收敛到该区域。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ApplyAspectLockLayout()
        {
            if (rootCanvas == null || aspectLetterboxRoot == null || aspectLetterboxContent == null)
            {
                return;
            }

            Vector2Int currentScreenSize = GetCurrentScreenSize();
            bool screenSizeChanged = currentScreenSize != lastAspectScreenSize;

            if (!hasAppliedAspectLockLayout || screenSizeChanged)
            {
                Rect viewport = CalculateLetterboxViewport(currentScreenSize.x, currentScreenSize.y);
                ApplyLetterboxViewport(viewport);
                lastAspectScreenSize = currentScreenSize;
                hasAppliedAspectLockLayout = true;
            }

            EnsureLayerRootParent(layerScreen);
            EnsureLayerRootParent(layerModal);
            EnsureLayerRootParent(layerOverlay);
            EnsureLayerRootParent(layerToast);
        }

        /// <summary>
        /// summary: 将当前游戏相机的视口设置为 16:9 安全区域。
        /// param name="camera": 需要应用视口的相机
        /// returns: 无
        /// </summary>
        private static void ApplyCameraViewport(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            Rect viewport = CalculateLetterboxViewport(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            camera.rect = viewport;
        }

        /// <summary>
        /// summary: 将四条黑边与内容容器收敛到当前屏幕的 16:9 安全区域。
        /// param name="viewport": 归一化后的安全视口
        /// returns: 无
        /// </summary>
        private void ApplyLetterboxViewport(Rect viewport)
        {
            if (aspectLetterboxRoot != null)
            {
                NormalizeRect(aspectLetterboxRoot);
                aspectLetterboxRoot.SetAsFirstSibling();
            }

            SetBarRect(aspectLetterboxTopBar, new Rect(0f, viewport.yMax, 1f, 1f - viewport.yMax));
            SetBarRect(aspectLetterboxBottomBar, new Rect(0f, 0f, 1f, viewport.yMin));
            SetBarRect(aspectLetterboxLeftBar, new Rect(0f, 0f, viewport.xMin, 1f));
            SetBarRect(aspectLetterboxRightBar, new Rect(viewport.xMax, 0f, 1f - viewport.xMax, 1f));

            if (aspectLetterboxContent != null)
            {
                aspectLetterboxContent.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
                aspectLetterboxContent.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
                aspectLetterboxContent.offsetMin = Vector2.zero;
                aspectLetterboxContent.offsetMax = Vector2.zero;
                aspectLetterboxContent.localScale = Vector3.one;
                aspectLetterboxContent.pivot = new Vector2(0.5f, 0.5f);
                aspectLetterboxContent.SetAsLastSibling();
            }

            ApplyCanvasScalerForLetterbox(viewport);
        }

        /// <summary>
        /// summary: 创建 16:9 安全区的黑边根节点。
        /// param: 无
        /// returns: 黑边根节点 RectTransform
        /// </summary>
        private RectTransform CreateAspectLetterboxRoot()
        {
            var rootObject = new GameObject(AspectLetterboxRootName, typeof(RectTransform));
            rootObject.layer = rootCanvas.gameObject.layer;
            rootObject.transform.SetParent(rootCanvas.transform, false);

            var rootRect = rootObject.GetComponent<RectTransform>();
            NormalizeRect(rootRect);
            rootObject.transform.SetAsFirstSibling();

            return rootRect;
        }

        /// <summary>
        /// summary: 创建单条黑边条带。
        /// param name="barName": 条带名称
        /// param name="parent": 条带父节点
        /// returns: 条带 RectTransform
        /// </summary>
        private RectTransform CreateAspectLetterboxBar(string barName, RectTransform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var barObject = new GameObject(barName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barObject.layer = rootCanvas.gameObject.layer;
            barObject.transform.SetParent(parent, false);

            var barRect = barObject.GetComponent<RectTransform>();
            NormalizeRect(barRect);

            var image = barObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            return barRect;
        }

        /// <summary>
        /// summary: 创建居中的 16:9 内容容器，所有 UI 层会迁移到该容器下。
        /// param: 无
        /// returns: 内容容器 RectTransform
        /// </summary>
        private RectTransform CreateAspectLetterboxContent()
        {
            var contentObject = new GameObject(AspectLetterboxContentName, typeof(RectTransform));
            contentObject.layer = rootCanvas.gameObject.layer;
            contentObject.transform.SetParent(rootCanvas.transform, false);

            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.localScale = Vector3.one;
            contentObject.transform.SetAsLastSibling();

            return contentRect;
        }

        /// <summary>
        /// summary: 依据 16:9 安全视口补偿 CanvasScaler 的 referenceResolution，保证锚点布局等价于 1080p 设计区。
        /// param name="viewport": 当前归一化安全视口
        /// returns: 无
        /// </summary>
        private void ApplyCanvasScalerForLetterbox(Rect viewport)
        {
            if (rootCanvasScaler == null || !hasRootCanvasBaseReferenceResolution)
            {
                return;
            }

            if (rootCanvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                return;
            }

            float widthFactor = Mathf.Max(0.0001f, viewport.width);
            float heightFactor = Mathf.Max(0.0001f, viewport.height);
            rootCanvasScaler.referenceResolution = new Vector2(
                rootCanvasBaseReferenceResolution.x / widthFactor,
                rootCanvasBaseReferenceResolution.y / heightFactor);
        }

        /// <summary>
        /// summary: 设置单条黑边的矩形与显示状态。
        /// param name="bar": 目标黑边条带
        /// param name="rect": 归一化矩形
        /// returns: 无
        /// </summary>
        private static void SetBarRect(RectTransform bar, Rect rect)
        {
            if (bar == null)
            {
                return;
            }

            bool isVisible = rect.width > 0f && rect.height > 0f;
            bar.gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                return;
            }

            bar.anchorMin = new Vector2(rect.xMin, rect.yMin);
            bar.anchorMax = new Vector2(rect.xMax, rect.yMax);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;
            bar.localScale = Vector3.one;
            bar.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// summary: 如果层容器还不在 16:9 内容容器下，则迁移并恢复为全屏拉伸布局。
        /// param name="layerRoot": 需要迁移的层容器
        /// returns: 无
        /// </summary>
        private void EnsureLayerRootParent(RectTransform layerRoot)
        {
            if (layerRoot == null || aspectLetterboxContent == null)
            {
                return;
            }

            if (layerRoot.parent != aspectLetterboxContent)
            {
                layerRoot.SetParent(aspectLetterboxContent, false);
            }

            NormalizeRect(layerRoot);
            layerRoot.SetAsLastSibling();
        }

        /// <summary>
        /// summary: 计算当前屏幕对应的 16:9 安全区域（归一化到 [0,1]）。
        /// param name="screenWidth": 屏幕宽度
        /// param name="screenHeight": 屏幕高度
        /// returns: 居中安全视口矩形
        /// </summary>
        private static Rect CalculateLetterboxViewport(int screenWidth, int screenHeight)
        {
            int safeWidth = Mathf.Max(1, screenWidth);
            int safeHeight = Mathf.Max(1, screenHeight);
            float screenAspect = (float)safeWidth / safeHeight;

            if (screenAspect > TargetAspectRatio)
            {
                float normalizedWidth = Mathf.Clamp01(TargetAspectRatio / screenAspect);
                float xOffset = (1f - normalizedWidth) * 0.5f;
                return new Rect(xOffset, 0f, normalizedWidth, 1f);
            }

            float normalizedHeight = Mathf.Clamp01(screenAspect / TargetAspectRatio);
            float yOffset = (1f - normalizedHeight) * 0.5f;
            return new Rect(0f, yOffset, 1f, normalizedHeight);
        }

        /// <summary>
        /// summary: 获取当前屏幕尺寸，统一处理 0 尺寸和编辑器首帧初始化场景。
        /// param: 无
        /// returns: 当前屏幕尺寸
        /// </summary>
        private static Vector2Int GetCurrentScreenSize()
        {
            return new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        }

        /// <summary>
        /// 当前 UI 是否正在执行过渡（Push/Pop/Show/Hide 等）。
        /// </summary>
        /// <param name="none">无</param>
        /// <return>正在过渡返回 true，否则 false</return>
        public bool IsNavigating()
        {
            return _isNavigating;
        }

        /// <summary>
        /// 以互斥方式执行一个 UI 导航协程：过渡期间等待，防止栈并发错乱。
        /// </summary>
        /// <param name="routine">要执行的导航协程</param>
        /// <return>可 yield 的协程枚举器</return>
        private IEnumerator RunNavigationLockedWait(IEnumerator routine)
        {
            while (_isNavigating) yield return null;

            _isNavigating = true;
            try
            {
                yield return routine;
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// 强制清空所有 Modal 与 Screen 栈（立即销毁/释放实例），用于切场景/回主菜单等“必须干净”的场景。
        /// </summary>
        /// <param name="cancelTransitions">是否取消当前所有 UI 过渡协程；为 true 可避免卡死与请求被吞。</param>
        /// <returns>无</returns>
        public void ClearAllScreensAndModals(bool cancelTransitions = true)
        {
            if (cancelTransitions)
            {
                // 取消所有正在运行的 Push/Pop/Show/Hide 协程，避免栈被并发修改或卡在 timeScale=0 的等待里
                StopAllCoroutines();
                _isNavigating = false;
            }

            StartCoroutine(ClearAllScreensAndModalsCo());
        }
                /// <summary>
        /// 清空栈的协程实现：优先清 Modal，再清 Screen，避免遮罩残留或 UI 顺序问题。
        /// </summary>
        /// <param name="none">无</param>
        /// <returns>协程枚举器</returns>
        private IEnumerator ClearAllScreensAndModalsCo()
        {
            // 这里不使用 RunNavigationLocked：它会在忙时 yield break 直接吞请求
            _isNavigating = true;
            try
            {
                // 先清 Modal
                while (modalStack.Count > 0)
                {
                    var m = modalStack.Pop();
                    DestroyScreenImmediate(m);
                }

                // 再清 Screen
                while (screenStack.Count > 0)
                {
                    var s = screenStack.Pop();
                    DestroyScreenImmediate(s);
                }

                // 让 Unity 有一帧处理 Destroy / ReleaseInstance
                yield return null;
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// 立即销毁/释放一个 UIScreen：若为 Addressables 实例则 ReleaseInstance，否则 Destroy。
        /// </summary>
        /// <param name="s">要销毁的 UIScreen</param>
        /// <returns>无</returns>
        private void DestroyScreenImmediate(UIScreen s)
        {
            if (s == null) return;

            // 先失活，避免一帧闪烁（可选，但通常更稳）
            if (s.gameObject != null) s.gameObject.SetActive(false);

            if (addrInstances.TryGetValue(s.gameObject, out var handle))
            {
                addrInstances.Remove(s.gameObject);
                UnityEngine.AddressableAssets.Addressables.ReleaseInstance(handle);
            }
            else
            {
                Destroy(s.gameObject);
            }
        }
        // --------- 公共 API ---------
        public void PushScreen<T>() where T : UIScreen
        {
            StartCoroutine(RunNavigationLockedWait(PushScreenCo<T>()));
        }

        // summary: 顺序压栈指定类型的UIScreen，并等待动画完成。
        // param T: 要创建的UIScreen类型。
        // return: 用于yield的协程枚举器。
        public IEnumerator PushScreenAndWait<T>() where T : UIScreen
        {
            yield return RunNavigationLockedWait(PushScreenCo<T>());
        }

        public IEnumerator PrePushScreenAndWait<T>() where T : UIScreen
        {
            yield return RunNavigationLockedWait(PrePushScreenCo<T>());
        }
        public IEnumerator PopScreenNoShowAndWait()
        {
            if (screenStack.Count == 0) yield break;
            yield return RunNavigationLockedWait(PopScreenCoNoShow());
        }
        public IEnumerator PopScreenAndWait()
        {
            if (screenStack.Count == 0) yield break;
            yield return RunNavigationLockedWait(PopScreenCo());
        }
        public IEnumerator PopModalAndWait()
        {
            if (modalStack.Count == 0) yield break;
            yield return RunNavigationLockedWait(DestroyAfterHide(modalStack.Pop()));
        }
        public IEnumerator ShowModalAndWait<T>() where T : UIScreen
        {
            yield return RunNavigationLockedWait(ShowModalCo<T>());
        }



        public void PopScreen()
        {
            // Debug.Log("Popping screen.");
            if (screenStack.Count == 0) return;
            StartCoroutine(RunNavigationLockedWait(PopScreenCo()));
        }
        public void ShowModal<T>() where T : UIScreen
        {
            StartCoroutine(RunNavigationLockedWait(ShowModalCo<T>()));
        }
        public void CloseTopModal()
        {
            if (modalStack.Count == 0) return;
            StartCoroutine(RunNavigationLockedWait(DestroyAfterHide(modalStack.Pop())));
        }

        /// <summary>
        /// 仅当目标就是当前 modal 栈顶时关闭它，避免重复触发时误关到底下那层 modal。
        /// </summary>
        /// <param name="target">希望关闭的具体 modal 实例。</param>
        /// <returns>无</returns>
        public void CloseModal(UIScreen target)
        {
            if (target == null || modalStack.Count == 0)
            {
                return;
            }

            var top = GetTopModal(false);
            if (top != target)
            {
                return;
            }

            StartCoroutine(RunNavigationLockedWait(DestroyAfterHide(modalStack.Pop())));
        }

        public void CloseTop()
        {
            if (modalStack.Count == 0)
            {
                if (screenStack.Count > 1)
                    PopScreen();
                else
                {
                    // switch()
                }
            }
            else
            {
                // if(screenStack.Count == 1) return;
                CloseTopModal();
            }
        }

        public T ShowOverlayImmediate<T>(T existing) where T : UIScreen
        {
            // 可选：复用场景里的现成 Overlay（非 Addressables）
            existing.transform.SetParent(layerOverlay, false);
            existing.__Init(this);
            StartCoroutine(existing.Show(0f));
            return existing;
        }
        // public T ShowOverlay<T>() where T : UIScreen
        // {
        //     var ov = Create<T>(UILayer.Overlay);
        //     StartCoroutine(ov.Show(0f));
        //     return ov;
        // }

        public void HideAndDestroy(UIScreen s) => StartCoroutine(DestroyAfterHide(s));

        /// <summary>
        /// 确保指定 Overlay prefab 已实例化并完成初始化；已存在时不会重复创建。
        /// </summary>
        /// <typeparam name="T">目标 Overlay 的 UIScreen 类型。</typeparam>
        /// <returns>无。</returns>
        public void EnsureOverlay<T>() where T : UIScreen
        {
            StartCoroutine(RunNavigationLockedWait(EnsureOverlayCo<T>()));
        }

        /// <summary>
        /// 同步等待指定 Overlay prefab 完成实例化与初始化。
        /// </summary>
        /// <typeparam name="T">目标 Overlay 的 UIScreen 类型。</typeparam>
        /// <returns>协程枚举器。</returns>
        public IEnumerator EnsureOverlayAndWait<T>() where T : UIScreen
        {
            yield return RunNavigationLockedWait(EnsureOverlayCo<T>());
        }

        /// <summary>
        /// 尝试获取当前已缓存的 Overlay 实例；已销毁对象会被自动剔除。
        /// </summary>
        /// <typeparam name="T">目标 Overlay 的 UIScreen 类型。</typeparam>
        /// <param name="overlay">输出的 Overlay 实例。</param>
        /// <returns>命中可用实例时返回 true。</returns>
        public bool TryGetOverlay<T>(out T overlay) where T : UIScreen
        {
            if (overlayRegistry.TryGetValue(typeof(T), out UIScreen existing) && existing != null)
            {
                overlay = existing as T;
                return overlay != null;
            }

            overlayRegistry.Remove(typeof(T));
            overlay = null;
            return false;
        }

        // --------- 内部：加载/实例化 ---------
        public IEnumerator PrePushScreenCo<T>() where T : UIScreen
        {
            // if (screenStack.Count > 0)
            //     yield return screenStack.Peek().Hide(defaultHide);

            T screen = null;
            yield return CreateScreenCo<T>(UILayer.Screen, s => screen = s);
            if (screen == null) yield break;
            screen.gameObject.SetActive(false);
            // screen.Hide();
            screen.setAlpha(0f);
            screenStack.Push(screen);
            
        }
    


        IEnumerator PushScreenCo<T>() where T : UIScreen
        {
            if (screenStack.Count > 0 && screenStack.Peek().getAlpha()>0f)
                yield return screenStack.Peek().Hide(defaultHide);

            T screen = null;
            yield return CreateScreenCo<T>(UILayer.Screen, s => screen = s);
            if (screen == null) yield break;

            screenStack.Push(screen);
            yield return screen.Show(defaultShow);
        }

        IEnumerator PopScreenCo()
        {
            // GameDebug.Log("stack count:"+screenStack.Count);
            var top = screenStack.Pop();
            yield return DestroyAfterHide(top);

            if (screenStack.Count > 0)
                yield return screenStack.Peek().Show(defaultShow);
        }
        IEnumerator PopScreenCoNoShow()
        {
            // GameDebug.Log("stack count:"+screenStack.Count);
            var top = screenStack.Pop();
            yield return DestroyAfterHide(top);
        }
        IEnumerator ShowModalCo<T>() where T : UIScreen
        {
            T modal = null;
            yield return CreateScreenCo<T>(UILayer.Modal, m => modal = m);
            if (modal == null) yield break;

            modalStack.Push(modal);
            yield return modal.Show(defaultShow);
        }

        IEnumerator CreateScreenCo<T>(UILayer layer, Action<T> onReady) where T : UIScreen
        {
            var address = GetPrefabAddress(typeof(T));
            var parent = GetLayer(layer) ?? (RectTransform)rootCanvas.transform;

            var handle = AddressableInstantiate(address, parent);
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Log.Error($"[UI] Addressables instantiate failed: {address}");
                yield break;
            }

            var go = handle.Result;
            addrInstances[go] = handle; // 记录句柄，销毁时释放
            var screen = go.GetComponent<T>() ?? go.AddComponent<T>();

            screen.__Init(this);
            if (!screen.PreservePrefabRootRectTransform)
            {
                NormalizeRect(go.transform as RectTransform);
            }
            onReady?.Invoke(screen);
        }

        IEnumerator EnsureOverlayCo<T>() where T : UIScreen
        {
            if (TryGetOverlay(out T existingOverlay) && existingOverlay != null)
            {
                yield break;
            }

            T overlay = null;
            yield return CreateScreenCo<T>(UILayer.Overlay, s => overlay = s);
            if (overlay == null)
            {
                yield break;
            }

            overlay.gameObject.SetActive(true);
            overlay.setAlpha(0f);
            overlayRegistry[typeof(T)] = overlay;
        }
        static AsyncOperationHandle<GameObject> AddressableInstantiate(string address, Transform parent)
        {
            // 直接实例化到父节点下，避免世界坐标错乱
            return Addressables.InstantiateAsync(address, parent);
        }

        string GetPrefabAddress(Type t)
        {
            var attr = t.GetCustomAttribute<UIPrefabAttribute>();
            if (attr != null && !string.IsNullOrEmpty(attr.Path)) return attr.Path;
            // 没写特性就用类名作为地址（确保你在 Addressables 里也这么配）
            return $"UI/{t.Name}";
        }

        // T Create<T>(UILayer layer) where T : UIScreen
        // {
        //     var path = GetPrefabPath(typeof(T));
        //     var prefab = Resources.Load<GameObject>(path);
        //     if (!prefab) throw new Exception($"UIPrefab not found at: {path}");

        //     var parent = GetLayer(layer);
        //     if (parent == null) {
        //         Debug.LogError($"[UI] Target layer {layer} is not assigned. Falling back to rootCanvas!");
        //         parent = (RectTransform)rootCanvas.transform; // 兜底：挂到整个 Canvas 下
        //     }

        //     // ——关键点：不要用不带 parent 的 Instantiate，也不要传 worldPositionStays=true——
        //     var go = Instantiate(prefab);                     // 先裸实例化
        //     go.transform.SetParent(parent, worldPositionStays: false); // 再强制设父节点
        //     var screen = go.GetComponent<T>() ?? go.AddComponent<T>();
        //     screen.__Init(this);
        //     NormalizeRect(go.transform as RectTransform);
        //     return screen;
        // }
        // T Create<T>(UILayer layer) where T : UIScreen
        // {
        //     var path = GetPrefabPath(typeof(T));
        //     var prefab = Resources.Load<GameObject>(path);
        //     if (!prefab) throw new Exception($"UIPrefab not found at: {path}");

        //     var parent = GetLayer(layer);
        //     var go = Instantiate(prefab, parent, worldPositionStays:false);
        //     var screen = go.GetComponent<T>();
        //     if (!screen) screen = go.AddComponent<T>(); // 防呆
        //     screen.__Init(this);
        //     NormalizeRect(go.transform as RectTransform);
        //     return screen;
        // }
        RectTransform GetLayer(UILayer layer) => layer switch
        {
            UILayer.Screen => layerScreen,
            UILayer.Modal => layerModal,
            UILayer.Overlay => layerOverlay,
            UILayer.Toast => layerToast,
            _ => layerScreen
        };
        // string GetPrefabPath(Type t)
        // {
        //     var attr = t.GetCustomAttribute<UIPrefabAttribute>();
        //     if (attr != null && !string.IsNullOrEmpty(attr.Path)) return attr.Path;
        //     // 约定：如果没写 Attribute，则走默认路径
        //     return $"UI/{t.Name}";
        // }

        // RectTransform GetLayer(UILayer layer) => layer switch
        // {
        //     UILayer.Screen  => layerScreen,
        //     UILayer.Modal   => layerModal,
        //     UILayer.Overlay => layerOverlay,
        //     UILayer.Toast   => layerToast,
        //     _ => layerScreen
        // };

        /// <summary>
        /// 获取当前Screen栈顶（不考虑Modal）。
        /// </summary>
        /// <param name="includeInactive">是否允许返回已失活/被销毁的对象；为false时会自动跳过无效引用。</param>
        /// <returns>Screen栈顶UIScreen；若为空则返回null。</returns>
        public UIScreen GetTopScreen(bool includeInactive = false)
        {
            return PeekValid(screenStack, includeInactive);
        }
        /// <summary>
        /// 获取当前Modal栈顶（如果没有Modal则返回null）。
        /// </summary>
        /// <param name="includeInactive">是否允许返回已失活/被销毁的对象；为false时会自动跳过无效引用。</param>
        /// <returns>Modal栈顶UIScreen；若为空则返回null。</returns>
        public UIScreen GetTopModal(bool includeInactive = false)
        {
            return PeekValid(modalStack, includeInactive);
        }
        /// <summary>
        /// 从栈顶开始，弹掉无效引用（被Destroy或失活），返回第一个有效对象但不移除它。
        /// </summary>
        /// <param name="stack">要检查的UIScreen栈。</param>
        /// <param name="includeInactive">是否允许返回失活对象。</param>
        /// <returns>有效UIScreen或null。</returns>
        private static UIScreen PeekValid(Stack<UIScreen> stack, bool includeInactive)
        {
            while (stack.Count > 0)
            {
                var s = stack.Peek();
                if (s == null)
                {
                    stack.Pop();
                    continue;
                }

                if (!includeInactive)
                {
                    // 这里用 activeInHierarchy 作为“是否激活”的判断标准
                    // 如果你的 UIScreen.Hide() 只是 CanvasGroup 淡出但不 SetActive(false)，那它仍会被认为是激活的
                    if (!s.gameObject.activeInHierarchy)
                    {
                        stack.Pop();
                        continue;
                    }
                }

                return s;
            }
            return null;
        }
        IEnumerator DestroyAfterHide(UIScreen s)
        {
            if (s == null) yield break;

            yield return s.Hide(defaultHide);
            if (s == null) yield break;

            if (addrInstances.TryGetValue(s.gameObject, out var handle))
            {
                addrInstances.Remove(s.gameObject);
                Addressables.ReleaseInstance(handle);
            }
            else
            {
                Destroy(s.gameObject);
            }
        }

        static void NormalizeRect(RectTransform rt)
        {
            if (!rt) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
