using System.Collections.Generic;

namespace Kernel.GameState
{
    /// <summary>
    /// 单个状态的定义，包含名称、互斥关系、可切换关系以及是否需要持久化。
    /// </summary>
    public struct Status
    {
        /// <summary>
        /// 状态名，作为唯一标识使用。
        /// </summary>
        public string StatusName;

        /// <summary>
        /// 互斥状态名列表，只要当前存在任意一个，则不能添加本状态。
        /// </summary>
        public List<string> InActiveWith;

        /// <summary>
        /// 允许切换的状态名列表，如果当前存在，则在添加本状态时会被移除。
        /// </summary>
        public List<string> allowSwitchWith;

        /// <summary>
        /// 是否需要随存档一起保存（true 表示存档/读档时会被处理）。
        /// </summary>
        public bool Persistent;
    };

    /// <summary>
    /// 预定义状态列表，同时提供通过状态名查找状态的功能。
    /// </summary>
    public static class StatusList
    {
        #region 建筑相关状态
        // 建筑放置状态（一般不需要跨存档保存）
        public static Status BuildingPlacementStatus = new Status
        {
            StatusName = "PlacingBuilding",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "BuildingDestroying" },
            Persistent = false
        };

        // 拆除建筑状态（一般不需要跨存档保存）
        public static Status BuildingDestroyingStatus = new Status
        {
            StatusName = "BuildingDestroying",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "PlacingBuilding" },
            Persistent = false
        };
        #endregion

        #region 游戏模式状态
        // 开发者模式状态（通常希望读档后保持）
        public static Status DevModeStatus = new Status
        {
            StatusName = "DevMode",
            InActiveWith = new List<string> { "NormalMode" },
            allowSwitchWith = null,
            Persistent = true
        };

        // 普通模式状态（通常希望读档后保持）
        public static Status NormalModeStatus = new Status
        {
            StatusName = "NormalMode",
            InActiveWith = new List<string> { "DevMode" },
            allowSwitchWith = null,
            Persistent = true
        };
        #endregion

        #region 游戏暂停相关状态
        // 游戏暂停状态（看需求，一般存档时会自动暂停，这里先不持久化）
        public static Status PausedStatus = new Status
        {
            StatusName = "Paused",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "Playing" , "InMenu", "InPauseMenu","InMainMenu"},
            Persistent = false
        };

        // 游戏进行中状态
        public static Status PlayingStatus = new Status
        {
            StatusName = "Playing",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "Paused","InMenu", "InPauseMenu","InMainMenu" },
            Persistent = false
        };
        public static Status InPauseMenuStatus = new Status
        {
            StatusName = "InPauseMenu",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "Playing", "Paused", "InMenu","InMainMenu" },
            Persistent = false
        };
        public static Status InMainMenuStatus = new Status
        {
            StatusName = "InMainMenu",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "Playing", "Paused", "InMenu", "InPauseMenu" },
            Persistent = false
        };
        public static Status InMenuStatus = new Status
        {
            StatusName = "InMenu",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "Playing", "Paused", "InPauseMenu", "InMainMenu" },
            Persistent = false
        };
        public static Status GameLoadingStatus = new Status
        {
            StatusName = "Loading",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "Paused", "Playing", "SaveLoading" },
            Persistent = false
        };

        public static Status SaveLoadingStatus = new Status
        {
            StatusName = "SaveLoading",
            InActiveWith = null,
            allowSwitchWith = new List<string> { "Paused", "Playing", "GameLoading" },
            Persistent = false
        };

        public static Status PopUpStatus = new Status
        {
            StatusName = "PopUp",
            InActiveWith = null,
            allowSwitchWith = null,
            Persistent = false
        };
        #endregion

        /// <summary>
        /// 内部字典，用于通过状态名查找预定义状态。
        /// </summary>
        private static readonly Dictionary<string, Status> _statusByName;

        /// <summary>
        /// 静态构造函数，初始化状态名到状态的映射。
        /// </summary>
        static StatusList()
        {
            _statusByName = new Dictionary<string, Status>();
            Register(BuildingPlacementStatus);
            Register(BuildingDestroyingStatus);
            Register(DevModeStatus);
            Register(NormalModeStatus);
            Register(PausedStatus);
            Register(PlayingStatus);
            Register(GameLoadingStatus);
            Register(SaveLoadingStatus);
            Register(InPauseMenuStatus);
            Register(InMainMenuStatus);
            Register(InMenuStatus);
        }

        /// <summary>
        /// 注册一个状态到字典中（内部使用）。
        /// </summary>
        /// <param name="status">要注册的状态定义</param>
        /// <returns>无返回值</returns>
        private static void Register(Status status)
        {
            if (string.IsNullOrEmpty(status.StatusName)) return;
            _statusByName[status.StatusName] = status;
        }

        /// <summary>
        /// 通过状态名查找预定义状态。
        /// </summary>
        /// <param name="name">状态名</param>
        /// <param name="status">输出：找到的状态定义</param>
        /// <returns>若找到对应状态返回 true，否则返回 false</returns>
        public static bool TryGetStatus(string name, out Status status)
        {
            if (string.IsNullOrEmpty(name))
            {
                status = default;
                return false;
            }

            return _statusByName.TryGetValue(name, out status);
        }

        /// <summary>
        /// 获取所有预定义状态的副本列表。
        /// </summary>
        /// <returns>包含所有预定义状态的列表</returns>
        public static List<Status> GetAllStatuses()
        {
            return new List<Status>(_statusByName.Values);
        }
    }
}
