using TMPro;
using UnityEngine;
using Vocalith.UI;

namespace Kernel.UI
{
    /// <summary>
    /// StartUp 到 Main 交接期间的加载界面，只负责显示数据预加载进度。
    /// </summary>
    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Loading Panel")]
    public sealed class LoadingUIScreen : UIScreen
    {
        [Header("Bindings")]
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private RectTransform progressFill;

        private float currentProgress;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            ApplyProgress();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
            ApplyProgress();
        }

        public void SetProgress(float progress)
        {
            currentProgress = Mathf.Clamp01(progress);
            ApplyProgress();
        }

        private void TryAutoBindReferences()
        {
            progressText ??= transform.Find("Progress/Text/Text (TMP)")?.GetComponent<TMP_Text>();
            progressText ??= GetComponentInChildren<TMP_Text>(true);
            progressFill ??= transform.Find("Progress/Animation") as RectTransform;
        }

        private void ApplyProgress()
        {
            int percent = Mathf.RoundToInt(currentProgress * 100f);
            if (progressText != null)
            {
                progressText.text = $"{percent}%";
            }

            if (progressFill == null)
            {
                return;
            }

            Vector2 anchorMax = progressFill.anchorMax;
            anchorMax.x = currentProgress;
            progressFill.anchorMax = anchorMax;
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
        }
    }
}
