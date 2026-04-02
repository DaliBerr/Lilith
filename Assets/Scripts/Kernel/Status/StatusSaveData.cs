using System.Collections.Generic;
using Vocalith.Scribe;

namespace Kernel.GameState
{
    /// <summary>
    /// 用于将 StatusController 的持久化状态接入 Scribe 存档系统的适配器。
    /// </summary>
    public class SaveStatus : ISaveItem
    {
        public string TypeId => "StatusNames";

        public List<string> names = StatusController.DumpPersistentStatusNames();

        /// <summary>
        /// Scribe 回调，在存档/读档时序列化或反序列化状态列表。
        /// </summary>
        /// <returns>无返回值</returns>
        public void ExposeData()
        {
            Scribe_Collections.Look("statusNames", ref names);

            // // 读档：names 已被填充，恢复状态
            if (Scribe.mode == ScribeMode.Loading)
            {
                StatusController.RestoreFromNames(names);
            }
        }
    }
}
