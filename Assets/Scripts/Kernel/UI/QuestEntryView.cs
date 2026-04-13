using TMPro;
using UnityEngine;

namespace Kernel.UI
{
    /// <summary>
    /// Quest Entry prefab 的轻量视图组件，负责暴露文本和淡出所需的 CanvasGroup。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text questText;
        [SerializeField] private CanvasGroup canvasGroup;

        public TMP_Text QuestText => questText;
        public CanvasGroup CanvasGroup => canvasGroup;

        private void Awake()
        {
            TryAutoBindReferences();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        /// <summary>
        /// summary: 统一写入当前任务条目的显示文本。
        /// param name="text": 需要显示的任务文本
        /// returns: 无
        /// </summary>
        public void SetText(string text)
        {
            if (questText != null)
            {
                questText.text = text ?? string.Empty;
            }
        }

        /// <summary>
        /// summary: 自动补齐 Quest Text 与 CanvasGroup 引用，减少 prefab 手动拖拽成本。
        /// param: 无
        /// returns: 无
        /// </summary>
        private void TryAutoBindReferences()
        {
            questText ??= GetComponentInChildren<TMP_Text>(true);
            canvasGroup ??= GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }
}
