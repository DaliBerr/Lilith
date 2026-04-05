using System.Collections;
// using Kernel.Building;
// using Kernel.Inventory;
// using Kernel.Item;
using Kernel.GameState;
using Kernel.UI;
using Vocalith.Logging;
using Vocalith.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vocalith.Localization;
namespace Kernel
{
    public sealed class Startup : MonoBehaviour
    {
        public static Startup Instance { get; private set; }
        private static readonly bool useDontDestroyOnLoad = true;

        [SerializeField] public bool isEnableDevMode = true;

        public static class LoggingInit
        {
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
            public static void Init()
            {
                LogBootstrap.EnsureInitialized();
// #if UNITY_EDITOR
//                 Log.AddSink(new UnitySink());
// #endif
                // GameDebug.Log("Check refresh");
                Log.Info("Log bootstrap ok (pid={0})", System.Diagnostics.Process.GetCurrentProcess().Id);
            }
        }

        /// <summary>
        /// summary: 在场景加载后确保存在 Startup 组件；若场景中只有空的 Startup 根对象，则直接补挂到该对象上。
        /// param: 无
        /// returns: 无
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (FindFirstObjectByType<Startup>() != null)
            {
                return;
            }

            GameObject bootstrapObject = GameObject.Find(nameof(Startup));
            if (bootstrapObject == null)
            {
                bootstrapObject = new GameObject(nameof(Startup));
            }

            bootstrapObject.AddComponent<Startup>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                
                return;
            }
            Instance = this;

            if (useDontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        IEnumerator Start()
        {

            // 如果担心 UIManager 还没起来，可以在这里先 yield return null;
            // yield return null;
            yield return StartCoroutine(Boot());
        }

        /// <summary>
        /// 启动协程：初始化状态系统，完成全局初始化后进入主战斗 HUD。
        /// </summary>
        /// <returns>协程枚举器。</returns>
        
        private IEnumerator Boot()
        {
            yield return InitLanguage();
            // 1) 初始化状态系统
            StatusController.Initialize();
            //全局加载进度重置（如果有的话）
            // GlobalLoadingProgress.Reset();
            if (isEnableDevMode)
            {
                StatusController.AddStatus(StatusList.DevModeStatus);
            }
            // 2) 进入全局初始化期。
            StatusController.AddStatus(StatusList.GameLoadingStatus);

            // 3) 执行全局初始化（Addressables + Def 加载）
            yield return StartCoroutine(InitGlobal());

            if (StatusController.HasStatus(StatusList.GameLoadingStatus))
            {
                StatusController.RemoveStatus(StatusList.GameLoadingStatus);
            }

            // 4) 初始化完成后进入主战斗 HUD，作为背包和暂停菜单的根界面。
            yield return StartCoroutine(EnterMainUI());
        }

        /// <summary>
        /// summary: 在启动完成后确保 UI 栈顶进入 MainUIScreen，作为运行时战斗 HUD 的入口。
        /// param: 无
        /// returns: 协程枚举器
        /// </summary>
        private IEnumerator EnterMainUI()
        {
            if (UIManager.Instance == null)
            {
                GameDebug.LogError("[Startup] UIManager is missing. MainUIScreen cannot be pushed.");
                yield break;
            }

            if (UIManager.Instance.GetTopScreen() is MainUIScreen)
            {
                yield break;
            }

            yield return UIManager.Instance.PushScreenAndWait<MainUIScreen>();
        }

        private IEnumerator InitLanguage()
        {
            var task = LocalizationManager.InitializeAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                GameDebug.LogError($"InitLanguage failed: {task.Exception}");
            }
        }

        /// <summary>
        /// 全局初始化：初始化 Addressables 并加载静态 Def 数据。
        /// </summary>
        /// <returns>协程枚举器。</returns>
        private IEnumerator InitGlobal()
        {
            // 1) Addressables 初始化（内部完整 Def 仍走 Addressables）
            var initHandle = Addressables.InitializeAsync();
            yield return initHandle;

            // 2) 加载所有 Def（Player 运行时会在数据库内部追加外部数值补丁）
            yield return StartCoroutine(LoadAllDefsCoroutine());

            // 3) 设置物品标签目录
            // if (Storage.StorageSystem.Instance.ItemCatalog == null)
            // {
            //     Storage.StorageSystem.Instance.ItemCatalog = new ItemDefCatalog();
            // }

            // 4) 预留位置：例如音乐系统、按键绑定等后续全局初始化内容
        }

        /// <summary>
        /// 协程形式等待 Building / Item Def 异步加载完成。
        /// </summary>
        /// <returns>协程枚举器。</returns>
        private IEnumerator LoadAllDefsCoroutine()
        {
            yield return null;
            // GlobalLoadingProgress.Reset();
            // StatusController.AddStatus(StatusList.GameLoadingStatus);
            // UIManager.Instance.PushScreen<GameLoading>();
            // 建筑定义加载 :contentReference[oaicite:3]{index=3}
            // var buildingTask = BuildingDatabase.LoadAllAsync();
            // while (!buildingTask.IsCompleted)
            // {
            //     yield return null;
            // }

            // 物品定义加载 :contentReference[oaicite:4]{index=4}
            // var itemTask = ItemDatabase.LoadAllAsync();
            // while (!itemTask.IsCompleted)
            // {
            //     yield return null;
            // }
        }
    }
}
