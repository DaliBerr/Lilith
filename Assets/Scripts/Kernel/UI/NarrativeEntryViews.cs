using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    [DisallowMultipleComponent]
    public sealed class NarrativeStoryEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button triggerButton;

        private NarrativeEntryData boundEntry;
        private Action<NarrativeEntryData> clickedCallback;

        public TMP_Text TitleText => titleText;
        public TMP_Text ProgressText => progressText;
        public Button TriggerButton => triggerButton;
        public NarrativeEntryData BoundEntry => boundEntry;

        private void Awake()
        {
            TryAutoBindReferences();
            BindButton();
        }

        private void OnDestroy()
        {
            UnbindButton();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        public void Bind(NarrativeEntryData entry, Action<NarrativeEntryData> onClicked)
        {
            TryAutoBindReferences();
            BindButton();
            boundEntry = entry;
            clickedCallback = onClicked;

            if (titleText != null)
            {
                titleText.text = string.IsNullOrWhiteSpace(entry?.Title) ? "-" : entry.Title.Trim();
            }

            if (progressText != null)
            {
                int chapterCount = entry?.Chapters != null ? entry.Chapters.Count : 0;
                progressText.text = $"{chapterCount}/{chapterCount}";
            }

            if (triggerButton != null)
            {
                triggerButton.interactable = entry != null;
            }
        }

        private void TryAutoBindReferences()
        {
            titleText ??= transform.Find("Tittle")?.GetComponent<TMP_Text>();
            titleText ??= transform.Find("Tittle")?.GetComponentInChildren<TMP_Text>(true);
            titleText ??= ResolveNamedText("title");
            titleText ??= ResolveNamedText("tittle");
            titleText ??= GetComponentInChildren<TMP_Text>(true);

            progressText ??= transform.Find("Progress")?.GetComponent<TMP_Text>();
            progressText ??= transform.Find("Progress")?.GetComponentInChildren<TMP_Text>(true);
            progressText ??= ResolveNamedText("progress");

            triggerButton ??= transform.Find("Trigger Button")?.GetComponent<Button>();
            triggerButton ??= GetComponent<Button>();
            triggerButton ??= GetComponentInChildren<Button>(true);
        }

        private TMP_Text ResolveNamedText(string token)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                TMP_Text candidate = texts[index];
                if (candidate != null && candidate.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void BindButton()
        {
            if (triggerButton == null)
            {
                return;
            }

            triggerButton.onClick.RemoveListener(HandleButtonClicked);
            triggerButton.onClick.AddListener(HandleButtonClicked);
        }

        private void UnbindButton()
        {
            if (triggerButton == null)
            {
                return;
            }

            triggerButton.onClick.RemoveListener(HandleButtonClicked);
        }

        private void HandleButtonClicked()
        {
            if (boundEntry != null)
            {
                clickedCallback?.Invoke(boundEntry);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class NarrativeChapterEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Button button;

        private int chapterIndex = -1;
        private Action<int> clickedCallback;

        public TMP_Text LabelText => labelText;
        public Button Button => button;
        public int ChapterIndex => chapterIndex;

        private void Awake()
        {
            TryAutoBindReferences();
            BindButton();
        }

        private void OnDestroy()
        {
            UnbindButton();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        public void Bind(int index, NarrativeChapterData chapter, bool isSelected, Action<int> onClicked)
        {
            TryAutoBindReferences();
            BindButton();
            chapterIndex = index;
            clickedCallback = onClicked;

            if (labelText != null)
            {
                labelText.text = string.IsNullOrWhiteSpace(chapter?.Title) ? (index + 1).ToString() : chapter.Title.Trim();
            }

            if (button != null)
            {
                button.interactable = !isSelected;
            }
        }

        private void TryAutoBindReferences()
        {
            labelText ??= transform.Find("Text (TMP)")?.GetComponent<TMP_Text>();
            labelText ??= GetComponentInChildren<TMP_Text>(true);
            button ??= transform.Find("Button")?.GetComponent<Button>();
            button ??= GetComponent<Button>();
            button ??= GetComponentInChildren<Button>(true);
        }

        private void BindButton()
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleButtonClicked);
            button.onClick.AddListener(HandleButtonClicked);
        }

        private void UnbindButton()
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleButtonClicked);
        }

        private void HandleButtonClicked()
        {
            if (chapterIndex >= 0)
            {
                clickedCallback?.Invoke(chapterIndex);
            }
        }
    }
}
