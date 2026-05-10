
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        [Header("Scaling")]
        [SerializeField, Min(0.1f)] private float minUIScale = 0.5f;
        [SerializeField, Min(0.1f)] private float maxUIScale = 2f;
        [SerializeField, Min(0.1f)] private float defaultUIScale = FallbackUIScale;

        private const float FallbackUIScale = 1f;

        private CanvasScaler rootCanvasScaler;
        private Vector2 rootCanvasBaseReferenceResolution = new Vector2(1920f, 1080f);
        private float rootCanvasBaseScaleFactor = 1f;
        private bool hasRootCanvasBaseReferenceResolution;
        private float currentUIScale = FallbackUIScale;
        private ResponsiveLayoutGroupFitter responsiveLayoutFitter;

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
            currentUIScale = ClampUIScale(defaultUIScale);
            CacheRootCanvasScaler();
            EnsureLayerRoots();
            ApplyRootCanvasScale();
            ApplyResponsiveLayouts();
            ResetGameCameraViewports();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
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
            EnsureLayerRoots();
            ApplyRootCanvasScale();
            ApplyResponsiveLayouts();
            ResetGameCameraViewports();
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
            rootCanvasBaseScaleFactor = Mathf.Max(0.0001f, rootCanvasScaler.scaleFactor);
            hasRootCanvasBaseReferenceResolution = true;
        }

        /// <summary>
        /// summary: 确保所有 UI 层直接铺满 rootCanvas，不再强制收敛到固定比例安全区。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void EnsureLayerRoots()
        {
            if (rootCanvas == null)
            {
                return;
            }

            CacheRootCanvasScaler();
            EnsureLayerRootParent(layerScreen);
            EnsureLayerRootParent(layerModal);
            EnsureLayerRootParent(layerOverlay);
            EnsureLayerRootParent(layerToast);
        }

        /// <summary>
        /// summary: 把当前已加载的游戏相机恢复为完整屏幕视口。
        /// param: 无
        /// returns: 无
        /// </summary>
        private static void ResetGameCameraViewports()
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].cameraType == CameraType.Game)
                {
                    cameras[i].rect = new Rect(0f, 0f, 1f, 1f);
                }
            }
        }

        /// <summary>
        /// summary: 确保层容器直接位于 rootCanvas 下并铺满屏幕。
        /// param name="layerRoot": 需要迁移的层容器
        /// returns: 无
        /// </summary>
        private void EnsureLayerRootParent(RectTransform layerRoot)
        {
            if (layerRoot == null || rootCanvas == null)
            {
                return;
            }

            if (layerRoot.parent != rootCanvas.transform)
            {
                layerRoot.SetParent(rootCanvas.transform, false);
            }

            NormalizeRect(layerRoot);
            layerRoot.SetAsLastSibling();
        }

        /// <summary>
        /// summary: 根据用户 UI 缩放设置更新根 CanvasScaler，不再补偿固定宽高比安全区。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void ApplyRootCanvasScale()
        {
            CacheRootCanvasScaler();
            if (rootCanvasScaler == null || !hasRootCanvasBaseReferenceResolution)
            {
                return;
            }

            float uiScale = Mathf.Max(0.0001f, currentUIScale);
            if (rootCanvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                rootCanvasScaler.referenceResolution = rootCanvasBaseReferenceResolution / uiScale;
            }
            else if (rootCanvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                rootCanvasScaler.scaleFactor = rootCanvasBaseScaleFactor * uiScale;
            }
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

        public float CurrentUIScale => currentUIScale;

        public float MinUIScale
        {
            get
            {
                ResolveUIScaleRange(out float min, out _);
                return min;
            }
        }

        public float MaxUIScale
        {
            get
            {
                ResolveUIScaleRange(out _, out float max);
                return max;
            }
        }

        /// <summary>
        /// summary: 应用用户 UI 缩放倍率；通过根 CanvasScaler 生效。
        /// param name="uiScale": 用户 UI 缩放倍率，1 为默认。
        /// returns: 无
        /// </summary>
        public void ApplyUIScale(float uiScale)
        {
            currentUIScale = ClampUIScale(uiScale);
            EnsureLayerRoots();
            ApplyRootCanvasScale();
            ApplyResponsiveLayouts();
        }

        internal void ApplyResponsiveLayouts()
        {
            EnsureResponsiveLayoutFitter();
            if (responsiveLayoutFitter == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            responsiveLayoutFitter.FitNow();
            Canvas.ForceUpdateCanvases();
        }

        public float ClampUIScale(float uiScale)
        {
            ResolveUIScaleRange(out float min, out float max);
            if (float.IsNaN(uiScale) || float.IsInfinity(uiScale))
            {
                return Mathf.Clamp(FallbackUIScale, min, max);
            }

            return Mathf.Clamp(uiScale, min, max);
        }

        private void ResolveUIScaleRange(out float min, out float max)
        {
            min = Mathf.Max(0.1f, minUIScale);
            max = Mathf.Max(0.1f, maxUIScale);
            if (max < min)
            {
                (min, max) = (max, min);
            }
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

            ApplyResponsiveLayouts();
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

            ClearSelectionOwnedBy(s);
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

        private static void ClearSelectionOwnedBy(UIScreen s)
        {
            if (s == null || UnityEventSystem.current == null || UnityEventSystem.current.currentSelectedGameObject == null)
            {
                return;
            }

            if (UnityEventSystem.current.currentSelectedGameObject.transform.IsChildOf(s.transform))
            {
                UnityEventSystem.current.SetSelectedGameObject(null);
            }
        }

        static void NormalizeRect(RectTransform rt)
        {
            if (!rt) return;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private void EnsureResponsiveLayoutFitter()
        {
            if (rootCanvas == null)
            {
                responsiveLayoutFitter = null;
                return;
            }

            RectTransform root = rootCanvas.transform as RectTransform;
            if (root == null)
            {
                responsiveLayoutFitter = null;
                return;
            }

            if (responsiveLayoutFitter == null || responsiveLayoutFitter.gameObject != rootCanvas.gameObject)
            {
                responsiveLayoutFitter = rootCanvas.GetComponent<ResponsiveLayoutGroupFitter>();
                if (responsiveLayoutFitter == null)
                {
                    responsiveLayoutFitter = rootCanvas.gameObject.AddComponent<ResponsiveLayoutGroupFitter>();
                }
            }

            responsiveLayoutFitter.SetRoot(root);
        }
    }
}
