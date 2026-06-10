using System;
using System.Collections;
using System.Collections.Generic;
using Kernel.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Vocalith.EventSystem;
using Vocalith.Logging;
using Vocalith.UI;

namespace Kernel.UI
{
    internal static class NarrativeReaderContext
    {
        public static NarrativeEntryData SelectedEntry { get; private set; }

        public static void SetSelectedEntry(NarrativeEntryData entry)
        {
            SelectedEntry = entry;
        }

        public static void Clear()
        {
            SelectedEntry = null;
        }
    }

    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Narrative/Narrative Menu Panel")]
    public sealed class NarrativeMenuUIScreen : GameUIScreen
    {
        private const string StoryEntryPrefabAddress = "Assets/Prefabs/UI/Narrative/Story Entry";

        [Header("Layout")]
        [SerializeField] private RectTransform leftPanelRoot;
        [SerializeField] private RectTransform rightPanelRoot;
        [SerializeField] private Button closeButton;

        [Header("Data")]
        [SerializeField] private string storyEntryPrefabAddress = StoryEntryPrefabAddress;

        private readonly List<NarrativeStoryEntryView> runtimeEntryViews = new();
        private readonly List<GameObject> runtimeEntryObjects = new();
        private AsyncOperationHandle<GameObject> storyEntryPrefabHandle;
        private bool hasStoryEntryPrefabHandle;
        private GameObject storyEntryPrefab;
        private bool isOpeningContent;

        public override Status currentStatus { get; } = StatusList.InNarrativeScreenStatus;
        public IReadOnlyList<NarrativeStoryEntryView> RuntimeEntryViews => runtimeEntryViews;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindCloseButton();
        }

        public override IEnumerator Show(float fade = 0.15f)
        {
            EnsureCurrentStatus();
            yield return EnsureAssetsLoadedCo();
            RebuildView();
            yield return base.Show(fade);
        }

        protected override void OnAfterHide()
        {
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            UnbindCloseButton();
            ClearRuntimeEntries();
            ReleasePrefabHandle();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        public void RequestClose()
        {
            if (ui == null || ui.IsNavigating())
            {
                return;
            }

            if (ui.GetTopScreen() == this)
            {
                ui.PopScreen();
            }
        }

        private IEnumerator EnsureAssetsLoadedCo()
        {
            if (!hasStoryEntryPrefabHandle)
            {
                string address = string.IsNullOrWhiteSpace(storyEntryPrefabAddress)
                    ? StoryEntryPrefabAddress
                    : storyEntryPrefabAddress.Trim();
                storyEntryPrefabHandle = Addressables.LoadAssetAsync<GameObject>(address);
                hasStoryEntryPrefabHandle = true;
                yield return storyEntryPrefabHandle;

                if (storyEntryPrefabHandle.Status == AsyncOperationStatus.Succeeded && storyEntryPrefabHandle.Result != null)
                {
                    storyEntryPrefab = storyEntryPrefabHandle.Result;
                }
                else
                {
                    GameDebug.LogWarning($"[NarrativeMenuUIScreen] Failed to load story entry prefab at '{address}'.");
                    ReleasePrefabHandle();
                }
            }

            NarrativeCatalogService service = NarrativeCatalogService.GetOrCreateInstance();
            if (service != null)
            {
                yield return service.LoadCatalogIfNeededCo();
            }
        }

        private void RebuildView()
        {
            TryAutoBindReferences();
            ClearRuntimeEntries();

            if (leftPanelRoot == null || rightPanelRoot == null || storyEntryPrefab == null)
            {
                return;
            }

            NarrativeCatalogData catalog = NarrativeCatalogService.GetOrCreateInstance()?.Catalog;
            IReadOnlyList<NarrativeEntryData> entries = catalog?.Entries != null
                ? catalog.Entries
                : Array.Empty<NarrativeEntryData>();
            int leftColumnCount = ResolveLeftColumnCount(entries.Count);

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                NarrativeEntryData entry = entries[entryIndex];
                RectTransform parent = entryIndex < leftColumnCount ? leftPanelRoot : rightPanelRoot;
                GameObject entryObject = Instantiate(storyEntryPrefab, parent, false);
                entryObject.name = $"Narrative Story Entry {entryIndex + 1:D2} - {entry?.Id ?? "missing"}";
                entryObject.SetActive(true);
                runtimeEntryObjects.Add(entryObject);

                NarrativeStoryEntryView view = entryObject.GetComponent<NarrativeStoryEntryView>();
                if (view == null)
                {
                    view = entryObject.AddComponent<NarrativeStoryEntryView>();
                }

                view.Bind(entry, HandleStoryEntryClicked);
                runtimeEntryViews.Add(view);
            }
        }

        private void HandleStoryEntryClicked(NarrativeEntryData entry)
        {
            if (entry == null || ui == null || isOpeningContent)
            {
                return;
            }

            StartCoroutine(OpenContentCo(entry));
        }

        private IEnumerator OpenContentCo(NarrativeEntryData entry)
        {
            isOpeningContent = true;
            try
            {
                NarrativeReaderContext.SetSelectedEntry(entry);
                yield return ui.PushScreenAndWait<NarrativeContentUIScreen>();
            }
            finally
            {
                isOpeningContent = false;
            }
        }

        private void TryAutoBindReferences()
        {
            leftPanelRoot ??= transform.Find("Left Panel") as RectTransform;
            rightPanelRoot ??= transform.Find("Right Panel") as RectTransform;
            closeButton ??= ResolveCloseButton(transform);
        }

        private static int ResolveLeftColumnCount(int entryCount)
        {
            if (entryCount <= 0)
            {
                return 0;
            }

            return Mathf.CeilToInt(entryCount * 0.5f);
        }

        private static Button ResolveCloseButton(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Transform named = root.Find("Close Button") ?? root.Find("Close");
            if (named != null)
            {
                Button button = named.GetComponent<Button>();
                return button != null ? button : named.GetComponentInChildren<Button>(true);
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button candidate = buttons[index];
                if (candidate != null && candidate.name.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return null;
        }

        private void BindCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(RequestClose);
            closeButton.onClick.AddListener(RequestClose);
        }

        private void UnbindCloseButton()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(RequestClose);
        }

        private void ClearRuntimeEntries()
        {
            for (int index = runtimeEntryObjects.Count - 1; index >= 0; index--)
            {
                DestroyChild(runtimeEntryObjects[index]);
            }

            runtimeEntryObjects.Clear();
            runtimeEntryViews.Clear();
        }

        private void ReleasePrefabHandle()
        {
            if (hasStoryEntryPrefabHandle && storyEntryPrefabHandle.IsValid())
            {
                Addressables.Release(storyEntryPrefabHandle);
            }

            hasStoryEntryPrefabHandle = false;
            storyEntryPrefabHandle = default;
            storyEntryPrefab = null;
        }

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        private static void DestroyChild(GameObject child)
        {
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    [DisallowMultipleComponent]
    [UIPrefab("Assets/Prefabs/UI/Narrative/Narrative Content Panel")]
    public sealed class NarrativeContentUIScreen : GameUIScreen
    {
        private const string ChapterEntryPrefabAddress = "Assets/Prefabs/UI/Narrative/Chapter Entry";
        private const string EmptyPageText = "";

        [Header("Layout")]
        [SerializeField] private RectTransform chapterSelectionRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text leftPageText;
        [SerializeField] private TMP_Text rightPageText;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private GameObject startBattleButtonRoot;
        [SerializeField] private Button startBattleButton;

        [Header("Data")]
        [SerializeField] private string chapterEntryPrefabAddress = ChapterEntryPrefabAddress;

        private readonly List<NarrativeChapterEntryView> runtimeChapterViews = new();
        private readonly List<GameObject> runtimeChapterObjects = new();
        private AsyncOperationHandle<GameObject> chapterEntryPrefabHandle;
        private bool hasChapterEntryPrefabHandle;
        private GameObject chapterEntryPrefab;
        private NarrativeEntryData activeEntry;
        private int activeChapterIndex;
        private int activePagePairIndex;

        public override Status currentStatus { get; } = StatusList.InNarrativeScreenStatus;
        public IReadOnlyList<NarrativeChapterEntryView> RuntimeChapterViews => runtimeChapterViews;
        public NarrativeEntryData ActiveEntry => activeEntry;
        public int ActiveChapterIndex => activeChapterIndex;
        public int ActivePagePairIndex => activePagePairIndex;

        protected override void OnInit()
        {
            TryAutoBindReferences();
            BindButtons();
            activeEntry = NarrativeReaderContext.SelectedEntry;
        }

        public override IEnumerator Show(float fade = 0.15f)
        {
            EnsureCurrentStatus();
            yield return EnsureAssetsLoadedCo();
            RebuildView();
            yield return base.Show(fade);
        }

        protected override void OnAfterHide()
        {
            RemoveCurrentStatus();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            ClearRuntimeChapters();
            ReleasePrefabHandle();
            RemoveCurrentStatus();
        }

        private void OnValidate()
        {
            TryAutoBindReferences();
        }

        public void Configure(NarrativeEntryData entry)
        {
            activeEntry = entry;
            activeChapterIndex = 0;
            activePagePairIndex = 0;
            RebuildView();
        }

        public void RequestClose()
        {
            if (ui == null || ui.IsNavigating())
            {
                return;
            }

            if (ui.GetTopScreen() == this)
            {
                ui.PopScreen();
            }
        }

        private IEnumerator EnsureAssetsLoadedCo()
        {
            if (!hasChapterEntryPrefabHandle)
            {
                string address = string.IsNullOrWhiteSpace(chapterEntryPrefabAddress)
                    ? ChapterEntryPrefabAddress
                    : chapterEntryPrefabAddress.Trim();
                chapterEntryPrefabHandle = Addressables.LoadAssetAsync<GameObject>(address);
                hasChapterEntryPrefabHandle = true;
                yield return chapterEntryPrefabHandle;

                if (chapterEntryPrefabHandle.Status == AsyncOperationStatus.Succeeded && chapterEntryPrefabHandle.Result != null)
                {
                    chapterEntryPrefab = chapterEntryPrefabHandle.Result;
                }
                else
                {
                    GameDebug.LogWarning($"[NarrativeContentUIScreen] Failed to load chapter entry prefab at '{address}'.");
                    ReleasePrefabHandle();
                }
            }
        }

        private void RebuildView()
        {
            TryAutoBindReferences();
            ClearRuntimeChapters();

            if (activeEntry == null)
            {
                SetContentText(EmptyPageText, EmptyPageText);
                SetStartBattleVisible(false);
                return;
            }

            IReadOnlyList<NarrativeChapterData> chapters = activeEntry.Chapters != null
                ? activeEntry.Chapters
                : Array.Empty<NarrativeChapterData>();
            activeChapterIndex = Mathf.Clamp(activeChapterIndex, 0, Mathf.Max(0, chapters.Count - 1));
            activePagePairIndex = 0;

            BuildChapterEntries(chapters);
            RefreshContent();
        }

        private void BuildChapterEntries(IReadOnlyList<NarrativeChapterData> chapters)
        {
            if (chapterSelectionRoot == null || chapterEntryPrefab == null || chapters == null)
            {
                return;
            }

            for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
            {
                GameObject chapterObject = Instantiate(chapterEntryPrefab, chapterSelectionRoot, false);
                chapterObject.name = $"Narrative Chapter Entry {chapterIndex + 1:D2} - {chapters[chapterIndex]?.Id ?? "missing"}";
                chapterObject.SetActive(true);
                runtimeChapterObjects.Add(chapterObject);

                NarrativeChapterEntryView view = chapterObject.GetComponent<NarrativeChapterEntryView>();
                if (view == null)
                {
                    view = chapterObject.AddComponent<NarrativeChapterEntryView>();
                }

                view.Bind(chapterIndex, chapters[chapterIndex], chapterIndex == activeChapterIndex, SelectChapter);
                runtimeChapterViews.Add(view);
            }
        }

        private void SelectChapter(int chapterIndex)
        {
            if (activeEntry?.Chapters == null || chapterIndex < 0 || chapterIndex >= activeEntry.Chapters.Count)
            {
                return;
            }

            activeChapterIndex = chapterIndex;
            activePagePairIndex = 0;
            RefreshChapterSelection();
            RefreshContent();
        }

        private void RequestPreviousPage()
        {
            if (activePagePairIndex <= 0)
            {
                return;
            }

            activePagePairIndex--;
            RefreshContent();
        }

        private void RequestNextPage()
        {
            int maxPairIndex = GetMaxPagePairIndex(GetActiveChapter());
            if (activePagePairIndex >= maxPairIndex)
            {
                return;
            }

            activePagePairIndex++;
            RefreshContent();
        }

        private void RequestStartBattle()
        {
            NarrativeChapterData chapter = GetActiveChapter();
            if (!ShouldShowStartBattleButton(chapter))
            {
                return;
            }

            EventManager.eventBus.Publish(new NarrativeStartBattleRequestedEvent(activeEntry.Id, chapter.Id));
        }

        private void RefreshContent()
        {
            NarrativeChapterData chapter = GetActiveChapter();
            if (chapter == null)
            {
                SetContentText(EmptyPageText, EmptyPageText);
                SetTitleText(activeEntry?.Title ?? string.Empty);
                SetPageButtonsInteractable(false, false);
                SetStartBattleVisible(false);
                return;
            }

            int maxPairIndex = GetMaxPagePairIndex(chapter);
            activePagePairIndex = Mathf.Clamp(activePagePairIndex, 0, maxPairIndex);
            int leftPageIndex = activePagePairIndex * 2;
            int rightPageIndex = leftPageIndex + 1;
            string leftText = GetPageText(chapter, leftPageIndex);
            string rightText = GetPageText(chapter, rightPageIndex);

            SetTitleText($"{activeEntry.Title} / {chapter.Title}");
            SetContentText(leftText, rightText);
            SetPageButtonsInteractable(activePagePairIndex > 0, activePagePairIndex < maxPairIndex);
            SetStartBattleVisible(ShouldShowStartBattleButton(chapter));
        }

        private void RefreshChapterSelection()
        {
            IReadOnlyList<NarrativeChapterData> chapters = activeEntry?.Chapters != null
                ? activeEntry.Chapters
                : Array.Empty<NarrativeChapterData>();
            for (int index = 0; index < runtimeChapterViews.Count; index++)
            {
                NarrativeChapterEntryView view = runtimeChapterViews[index];
                if (view != null && index < chapters.Count)
                {
                    view.Bind(index, chapters[index], index == activeChapterIndex, SelectChapter);
                }
            }
        }

        private NarrativeChapterData GetActiveChapter()
        {
            if (activeEntry?.Chapters == null || activeChapterIndex < 0 || activeChapterIndex >= activeEntry.Chapters.Count)
            {
                return null;
            }

            return activeEntry.Chapters[activeChapterIndex];
        }

        private bool ShouldShowStartBattleButton(NarrativeChapterData chapter)
        {
            return activeEntry != null
                && chapter != null
                && activeEntry.ShowStartBattleOnLastChapter
                && activeEntry.Chapters != null
                && activeChapterIndex == activeEntry.Chapters.Count - 1;
        }

        private static int GetMaxPagePairIndex(NarrativeChapterData chapter)
        {
            int pageCount = chapter?.Pages != null ? chapter.Pages.Count : 0;
            return Mathf.Max(0, (pageCount - 1) / 2);
        }

        private static string GetPageText(NarrativeChapterData chapter, int pageIndex)
        {
            if (chapter?.Pages == null || pageIndex < 0 || pageIndex >= chapter.Pages.Count)
            {
                return EmptyPageText;
            }

            return chapter.Pages[pageIndex] ?? EmptyPageText;
        }

        private void SetTitleText(string text)
        {
            if (titleText != null)
            {
                titleText.text = text ?? string.Empty;
            }
        }

        private void SetContentText(string leftText, string rightText)
        {
            if (leftPageText != null)
            {
                leftPageText.text = leftText ?? string.Empty;
            }

            if (rightPageText != null)
            {
                rightPageText.text = rightText ?? string.Empty;
            }
        }

        private void SetPageButtonsInteractable(bool canGoPrevious, bool canGoNext)
        {
            if (previousPageButton != null)
            {
                previousPageButton.interactable = canGoPrevious;
            }

            if (nextPageButton != null)
            {
                nextPageButton.interactable = canGoNext;
            }
        }

        private void SetStartBattleVisible(bool isVisible)
        {
            if (startBattleButtonRoot != null && startBattleButtonRoot.activeSelf != isVisible)
            {
                startBattleButtonRoot.SetActive(isVisible);
            }

            if (startBattleButton != null)
            {
                startBattleButton.interactable = isVisible;
            }
        }

        private void TryAutoBindReferences()
        {
            chapterSelectionRoot ??= transform.Find("Chapter Selection Panel") as RectTransform;
            titleText ??= transform.Find("Tittle")?.GetComponent<TMP_Text>();
            titleText ??= transform.Find("Tittle")?.GetComponentInChildren<TMP_Text>(true);
            leftPageText ??= ResolveNamedText("Left Page Text");
            rightPageText ??= ResolveNamedText("Right Page Text");
            previousPageButton ??= ResolveButton("Previous Page");
            nextPageButton ??= ResolveButton("Next Page");
            startBattleButtonRoot ??= transform.Find("Start Battle Button")?.gameObject;
            if (startBattleButtonRoot != null)
            {
                startBattleButton ??= startBattleButtonRoot.GetComponent<Button>();
                startBattleButton ??= startBattleButtonRoot.GetComponentInChildren<Button>(true);
            }
        }

        private TMP_Text ResolveNamedText(string objectName)
        {
            Transform target = transform.Find(objectName);
            if (target == null)
            {
                return null;
            }

            TMP_Text text = target.GetComponent<TMP_Text>();
            return text != null ? text : target.GetComponentInChildren<TMP_Text>(true);
        }

        private Button ResolveButton(string objectName)
        {
            Transform target = transform.Find(objectName);
            if (target == null)
            {
                return null;
            }

            Button button = target.GetComponent<Button>();
            return button != null ? button : target.GetComponentInChildren<Button>(true);
        }

        private void BindButtons()
        {
            if (previousPageButton != null)
            {
                previousPageButton.onClick.RemoveListener(RequestPreviousPage);
                previousPageButton.onClick.AddListener(RequestPreviousPage);
            }

            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveListener(RequestNextPage);
                nextPageButton.onClick.AddListener(RequestNextPage);
            }

            if (startBattleButton != null)
            {
                startBattleButton.onClick.RemoveListener(RequestStartBattle);
                startBattleButton.onClick.AddListener(RequestStartBattle);
            }
        }

        private void UnbindButtons()
        {
            if (previousPageButton != null)
            {
                previousPageButton.onClick.RemoveListener(RequestPreviousPage);
            }

            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveListener(RequestNextPage);
            }

            if (startBattleButton != null)
            {
                startBattleButton.onClick.RemoveListener(RequestStartBattle);
            }
        }

        private void ClearRuntimeChapters()
        {
            for (int index = runtimeChapterObjects.Count - 1; index >= 0; index--)
            {
                DestroyChild(runtimeChapterObjects[index]);
            }

            runtimeChapterObjects.Clear();
            runtimeChapterViews.Clear();
        }

        private void ReleasePrefabHandle()
        {
            if (hasChapterEntryPrefabHandle && chapterEntryPrefabHandle.IsValid())
            {
                Addressables.Release(chapterEntryPrefabHandle);
            }

            hasChapterEntryPrefabHandle = false;
            chapterEntryPrefabHandle = default;
            chapterEntryPrefab = null;
        }

        private void RemoveCurrentStatus()
        {
            if (StatusController.HasStatus(currentStatus))
            {
                StatusController.RemoveStatus(currentStatus);
            }
        }

        private static void DestroyChild(GameObject child)
        {
            if (child == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }
}
