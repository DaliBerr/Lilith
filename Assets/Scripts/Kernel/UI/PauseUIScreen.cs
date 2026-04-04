using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// PauseUI 的运行时模板脚本，只负责暴露暂停菜单引用与状态入口。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/PauseUI")]
    public class PauseUIScreen : GameUIScreen
    {
        [Header("Layout")]
        [SerializeField] private Image backgroundMask;
        [SerializeField] private RectTransform mainPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private TMP_Text resumeButtonText;
        [SerializeField] private Button optionsButton;
        [SerializeField] private TMP_Text optionsButtonText;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text backButtonText;

        public override Status currentStatus { get; } = StatusList.InPauseMenuStatus;

        public Image BackgroundMask => backgroundMask;
        public RectTransform MainPanel => mainPanel;
        public Button ResumeButton => resumeButton;
        public TMP_Text ResumeButtonText => resumeButtonText;
        public Button OptionsButton => optionsButton;
        public TMP_Text OptionsButtonText => optionsButtonText;
        public Button BackButton => backButton;
        public TMP_Text BackButtonText => backButtonText;

        protected override void OnInit()
        {
            TryAutoBindReferences();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        [ContextMenu("Auto Bind Pause UI Template")]
        private void AutoBindTemplate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// 按当前 PauseUI prefab 的层级自动补齐暂停菜单引用。
        /// </summary>
        /// <returns>无。</returns>
        private void TryAutoBindReferences()
        {
            backgroundMask ??= GetComponent<Image>();
            mainPanel ??= transform.Find("Main Panel") as RectTransform;
            if (mainPanel == null)
            {
                return;
            }

            resumeButton ??= ResolveButton(mainPanel, "Button Prefab", 0);
            if (resumeButton != null)
            {
                resumeButtonText ??= resumeButton.GetComponentInChildren<TMP_Text>(true);
            }

            optionsButton ??= ResolveButton(mainPanel, "Button Prefab (1)", 1);
            if (optionsButton != null)
            {
                optionsButtonText ??= optionsButton.GetComponentInChildren<TMP_Text>(true);
            }

            backButton ??= ResolveButton(mainPanel, "Button Prefab (2)", 2);
            if (backButton != null)
            {
                backButtonText ??= backButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>
        /// 优先按层级名找按钮，找不到时再按子节点顺序回退。
        /// </summary>
        /// <param name="panel">暂停面板根节点。</param>
        /// <param name="childName">目标按钮子节点名。</param>
        /// <param name="fallbackIndex">回退使用的子节点索引。</param>
        /// <returns>解析到的按钮；未找到时返回 null。</returns>
        private static Button ResolveButton(Transform panel, string childName, int fallbackIndex)
        {
            if (panel == null)
            {
                return null;
            }

            Transform child = panel.Find(childName);
            if (child != null)
            {
                return child.GetComponentInChildren<Button>(true);
            }

            if (panel.childCount == 0)
            {
                return null;
            }

            int clampedIndex = Mathf.Clamp(fallbackIndex, 0, panel.childCount - 1);
            return panel.GetChild(clampedIndex).GetComponentInChildren<Button>(true);
        }
    }
}
