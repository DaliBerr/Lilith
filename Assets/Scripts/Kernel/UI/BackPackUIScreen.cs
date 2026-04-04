using System;
using Kernel.GameState;
using UnityEngine;
using Vocalith.UI;

namespace Kernel.UI
{
    [UIPrefab("Assets/Prefabs/UI/BackPackUI")]
    public class BackPackUIScreen : GameUIScreen
    {


        public override Status currentStatus { get; } = StatusList.InBackPackStatus;

        protected override void OnInit()
        {
            TryAutoBindReferences();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind BackPack UI Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// 按当前 BackPackUI prefab 的层级自动补齐常用字段，减少手动拖拽成本。
        /// </summary>
        /// <returns>无。</returns>
        private void TryAutoBindReferences()
        {
        }
    }
}