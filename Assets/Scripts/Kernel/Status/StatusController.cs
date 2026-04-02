using System.Collections.Generic;
using Vocalith.Logging;
using UnityEngine;

namespace Kernel.GameState
{
    /// <summary>
    /// 状态控制器，负责管理和更新游戏中的各种状态信息。
    /// </summary>
    public static class StatusController
    {
        /// <summary>
        /// 当前激活的状态列表。
        /// </summary>
        public static List<Status> CurrentStatus = new();

        /// <summary>
        /// 初始化状态控制器，清空所有当前状态。
        /// </summary>
        /// <returns>无返回值</returns>
        public static void Initialize()
        {
            CurrentStatus.Clear();
        }

        public static void ShowStatus()
        {
            string statusStr = "Current Status:";
            foreach (var status in CurrentStatus)
            {
                statusStr += $" {status.StatusName};";
            }
            GameDebug.Log(statusStr);
        }

        /// <summary>
        /// 添加状态，如果存在互斥状态则添加失败；若存在允许切换的状态则自动移除旧状态。
        /// </summary>
        /// <param name="status">要添加的状态定义</param>
        /// <returns>成功添加返回 true；若互斥或已存在则返回 false</returns>
        public static bool AddStatus(Status status)
        {
            // 已有同名状态则直接失败
            if (HasStatus(status.StatusName))
            {
                return false;
            }

            // 检查互斥状态


            // 处理允许切换的状态（注意不能在 foreach 里 Remove，这会抛异常）
            if (status.allowSwitchWith != null && status.allowSwitchWith.Count > 0)
            {
                for (int i = CurrentStatus.Count - 1; i >= 0; i--)
                {
                    if (status.allowSwitchWith.Contains(CurrentStatus[i].StatusName))
                    {
                        CurrentStatus.RemoveAt(i);
                        break;
                    }
                }
            }
            
            if (status.InActiveWith != null && status.InActiveWith.Count > 0)
            {
                foreach (var s in CurrentStatus)
                {
                    if (status.InActiveWith.Contains(s.StatusName))
                    {
                        // 存在互斥状态，不能添加
                        return false;
                    }
                }
            }
            CurrentStatus.Add(status);
            ShowStatus();
            return true;
        }

        /// <summary>
        /// 使用状态名添加一个预定义状态。
        /// </summary>
        /// <param name="statusName">要添加的状态名（需在 StatusList 中预定义）</param>
        /// <returns>成功添加返回 true；找不到或互斥/重复则返回 false</returns>
        public static bool AddStatus(string statusName)
        {
            if (!StatusList.TryGetStatus(statusName, out var status))
            {
                GameDebug.LogWarning($"[StatusController] 尝试添加未知状态 '{statusName}'，请先在 StatusList 中定义。");
                return false;
            }

            return AddStatus(status);
        }

        /// <summary>
        /// 根据状态定义移除当前状态（按 StatusName 匹配）。
        /// </summary>
        /// <param name="status">要移除的状态定义</param>
        /// <returns>无返回值</returns>
        public static void RemoveStatus(Status status)
        {
            int idx = IndexOfStatus(status.StatusName);
            if (idx >= 0)
            {
                CurrentStatus.RemoveAt(idx);
            }
            ShowStatus();
        }

        /// <summary>
        /// 根据状态名移除当前状态。
        /// </summary>
        /// <param name="statusName">要移除的状态名</param>
        /// <returns>无返回值</returns>
        public static void RemoveStatus(string statusName)
        {
            int idx = IndexOfStatus(statusName);
            if (idx >= 0)
            {
                CurrentStatus.RemoveAt(idx);
            }
        }

        /// <summary>
        /// 清空所有当前状态。
        /// </summary>
        /// <returns>无返回值</returns>
        public static void ClearStatus()
        {
            CurrentStatus.Clear();
        }

        /// <summary>
        /// 检查是否拥有指定状态（按状态名）。
        /// </summary>
        /// <param name="statusName">要检查的状态名</param>
        /// <returns>若存在该状态返回 true，否则返回 false</returns>
        public static bool HasStatus(string statusName)
        {
            return IndexOfStatus(statusName) >= 0;
        }

        /// <summary>
        /// 检查是否拥有指定状态（按 StatusName 匹配）。
        /// </summary>
        /// <param name="status">要检查的状态定义</param>
        /// <returns>若存在该状态返回 true，否则返回 false</returns>
        public static bool HasStatus(Status status)
        {
            return HasStatus(status.StatusName);
        }

        /// <summary>
        /// 导出当前所有需要持久化的状态名列表，用于存档系统。
        /// </summary>
        /// <returns>需要随存档保存的状态名列表</returns>
        public static List<string> DumpPersistentStatusNames()
        {
            var result = new List<string>();
            foreach (var status in CurrentStatus)
            {
                if (status.Persistent && !string.IsNullOrEmpty(status.StatusName))
                {
                    result.Add(status.StatusName);
                }
            }

            return result;
        }

        /// <summary>
        /// 从状态名列表恢复当前状态，一般在读档完成后调用。
        /// </summary>
        /// <param name="statusNames">需要恢复的状态名集合</param>
        /// <returns>无返回值</returns>
        public static void RestoreFromNames(IEnumerable<string> statusNames)
        {
            CurrentStatus.Clear();
            if (statusNames == null) return;

            foreach (var name in statusNames)
            {
                if (string.IsNullOrEmpty(name)) continue;

                if (!StatusList.TryGetStatus(name, out var status))
                {
                    Log.Warn($"[StatusController] 读档时发现未知状态 '{name}'，已跳过。");
                    GameDebug.LogWarning($"[StatusController] 读档时发现未知状态 '{name}'，已跳过。");
                    continue;
                }

                // 使用原有的互斥/切换逻辑重新添加
                AddStatus(status);
            }
        }

        /// <summary>
        /// 根据状态名在当前状态列表中查找索引。
        /// </summary>
        /// <param name="statusName">要查找的状态名</param>
        /// <returns>找到则返回索引，未找到返回 -1</returns>
        private static int IndexOfStatus(string statusName)
        {
            if (string.IsNullOrEmpty(statusName)) return -1;

            for (int i = 0; i < CurrentStatus.Count; i++)
            {
                if (CurrentStatus[i].StatusName == statusName)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
