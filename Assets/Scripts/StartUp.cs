using System.Collections;
using System.IO;
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
        /// 启动协程：初始化状态系统，顺序压栈主菜单
        ///  + 加载界面，执行全局初始化。
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
            // 2) 顺序压栈主菜单（作为底层界面）
            //    等 MainMenu 创建 + Show 动画完全结束
            yield return UIManager.Instance.PrePushScreenCo<MainMenuScreen>();

            // 3) 再添加“游戏加载中”状态，并把加载界面压在主菜单上面
            StatusController.AddStatus(StatusList.GameLoadingStatus);
            
            //    同样顺序压栈 GameLoading（这时 MainMenu 会被 Hide）
            // yield return UIManager.Instance.PushScreenAndWait<GameLoading>();
            // GameDebug.Log("[Startup] Pushed GameLoading Screen (with waiting)");

            // 4) 执行全局初始化（Addressables + Def 加载）
            yield return StartCoroutine(InitGlobal());

            // 5) 不要再 Push 主菜单：GameLoading 完成时会自己 Pop，
            //    然后 UIManager 会把下面的 MainMenu 再 Show 出来。
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
