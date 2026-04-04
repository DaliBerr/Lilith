using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// MainUI 的运行时模板脚本，只负责暴露 HUD 常用引用与状态入口。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/MainUI")]
    public class MainUIScreen : GameUIScreen
    {
        [Header("Layout")]
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform healthPanel;
        [SerializeField] private TMP_Text healthTitleText;
        [SerializeField] private RectTransform healthBarRoot;
        [SerializeField] private PlayerHealthBarController healthBarController;
        [SerializeField] private RectTransform pauseButtonRoot;
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text pauseButtonText;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        public RectTransform TopPanel => topPanel;
        public RectTransform HealthPanel => healthPanel;
        public TMP_Text HealthTitleText => healthTitleText;
        public RectTransform HealthBarRoot => healthBarRoot;
        public PlayerHealthBarController HealthBarController => healthBarController;
        public RectTransform PauseButtonRoot => pauseButtonRoot;
        public Button PauseButton => pauseButton;
        public TMP_Text PauseButtonText => pauseButtonText;

        protected override void OnInit()
        {
            TryAutoBindReferences();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind Main UI Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// 按当前 MainUI prefab 的层级自动补齐常用字段，减少手动拖拽成本。
        /// </summary>
        /// <returns>无。</returns>
        private void TryAutoBindReferences()
        {
            topPanel ??= transform.Find("TopPanel") as RectTransform;
            if (topPanel == null)
            {
                return;
            }

            healthPanel ??= topPanel.Find("HP Bar") as RectTransform;
            if (healthPanel != null)
            {
                healthTitleText ??= healthPanel.Find("Titlle")?.GetComponent<TMP_Text>();
                healthBarRoot ??= healthPanel.Find("Bar") as RectTransform;
                if (healthBarRoot != null)
                {
                    healthBarController ??= healthBarRoot.GetComponent<PlayerHealthBarController>();
                }
            }

            pauseButtonRoot ??= ResolvePauseButtonRoot(topPanel);
            if (pauseButtonRoot == null)
            {
                return;
            }

            pauseButton ??= pauseButtonRoot.GetComponentInChildren<Button>(true);
            if (pauseButton != null)
            {
                pauseButtonText ??= pauseButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>
        /// 兼容当前 MainUI prefab 中已有的暂停按钮节点命名。
        /// </summary>
        /// <param name="root">顶部面板根节点。</param>
        /// <returns>暂停按钮根节点；未找到时返回 null。</returns>
        private static RectTransform ResolvePauseButtonRoot(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            return root.Find("Pause Btn") as RectTransform
                ?? root.Find("Pause Button") as RectTransform;
        }
    }
}
