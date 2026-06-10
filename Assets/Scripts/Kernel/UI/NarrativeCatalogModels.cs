using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Vocalith.Localization;
using Vocalith.Logging;

namespace Kernel.UI
{
    [Serializable]
    public sealed class NarrativeCatalogData
    {
        [JsonProperty("entries")]
        public List<NarrativeEntryData> Entries { get; set; } = new();
    }

    [Serializable]
    public sealed class NarrativeEntryData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("showStartBattleOnLastChapter")]
        public bool ShowStartBattleOnLastChapter { get; set; } = true;

        [JsonProperty("chapters")]
        public List<NarrativeChapterData> Chapters { get; set; } = new();
    }

    [Serializable]
    public sealed class NarrativeChapterData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("pages")]
        public List<string> Pages { get; set; } = new();
    }

    public static class NarrativeCatalogUtility
    {
        public static NarrativeCatalogData CreateDefault()
        {
            return new NarrativeCatalogData();
        }

        public static bool TryDeserializeCatalogJson(string jsonText, out NarrativeCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Narrative catalog JSON is empty.";
                return false;
            }

            try
            {
                NarrativeCatalogData rawCatalog = LocalizedJsonUtility.DeserializeLocalized<NarrativeCatalogData>(
                    jsonText,
                    "NarrativeCatalog",
                    settings: null);
                return TrySanitize(rawCatalog, out catalog, out errorMessage);
            }
            catch (JsonException exception)
            {
                errorMessage = $"Narrative catalog JSON is invalid: {exception.Message}";
                return false;
            }
        }

        public static bool TrySanitize(NarrativeCatalogData rawCatalog, out NarrativeCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            if (rawCatalog?.Entries == null || rawCatalog.Entries.Count == 0)
            {
                errorMessage = "Narrative catalog must contain at least one entry.";
                return false;
            }

            List<NarrativeEntryData> sanitizedEntries = new(rawCatalog.Entries.Count);
            HashSet<string> seenEntryIds = new(StringComparer.Ordinal);

            for (int entryIndex = 0; entryIndex < rawCatalog.Entries.Count; entryIndex++)
            {
                NarrativeEntryData rawEntry = rawCatalog.Entries[entryIndex];
                if (rawEntry == null)
                {
                    errorMessage = $"Narrative entry {entryIndex + 1} is null.";
                    return false;
                }

                string entryId = SanitizeIdentifier(rawEntry.Id, $"entry_{entryIndex + 1}");
                if (!seenEntryIds.Add(entryId))
                {
                    errorMessage = $"Narrative entry id '{entryId}' is duplicated.";
                    return false;
                }

                if (!TrySanitizeChapters(rawEntry.Chapters, entryId, out List<NarrativeChapterData> chapters, out errorMessage))
                {
                    return false;
                }

                sanitizedEntries.Add(new NarrativeEntryData
                {
                    Id = entryId,
                    Title = SanitizeText(rawEntry.Title, entryId),
                    ShowStartBattleOnLastChapter = rawEntry.ShowStartBattleOnLastChapter,
                    Chapters = chapters,
                });
            }

            catalog = new NarrativeCatalogData
            {
                Entries = sanitizedEntries,
            };
            return true;
        }

        private static bool TrySanitizeChapters(
            List<NarrativeChapterData> rawChapters,
            string entryId,
            out List<NarrativeChapterData> chapters,
            out string errorMessage)
        {
            chapters = null;
            errorMessage = null;

            if (rawChapters == null || rawChapters.Count == 0)
            {
                errorMessage = $"Narrative entry '{entryId}' must contain at least one chapter.";
                return false;
            }

            chapters = new List<NarrativeChapterData>(rawChapters.Count);
            HashSet<string> seenChapterIds = new(StringComparer.Ordinal);
            for (int chapterIndex = 0; chapterIndex < rawChapters.Count; chapterIndex++)
            {
                NarrativeChapterData rawChapter = rawChapters[chapterIndex];
                if (rawChapter == null)
                {
                    errorMessage = $"Narrative chapter {chapterIndex + 1} in entry '{entryId}' is null.";
                    return false;
                }

                string chapterId = SanitizeIdentifier(rawChapter.Id, $"chapter_{chapterIndex + 1}");
                if (!seenChapterIds.Add(chapterId))
                {
                    errorMessage = $"Narrative chapter id '{chapterId}' is duplicated in entry '{entryId}'.";
                    return false;
                }

                List<string> pages = SanitizePages(rawChapter.Pages);
                if (pages.Count == 0)
                {
                    errorMessage = $"Narrative chapter '{chapterId}' in entry '{entryId}' must contain at least one non-empty page.";
                    return false;
                }

                chapters.Add(new NarrativeChapterData
                {
                    Id = chapterId,
                    Title = SanitizeText(rawChapter.Title, chapterId),
                    Pages = pages,
                });
            }

            return true;
        }

        private static List<string> SanitizePages(List<string> rawPages)
        {
            List<string> pages = new();
            if (rawPages == null)
            {
                return pages;
            }

            for (int index = 0; index < rawPages.Count; index++)
            {
                string trimmed = rawPages[index] != null ? rawPages[index].Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    pages.Add(trimmed);
                }
            }

            return pages;
        }

        private static string SanitizeIdentifier(string value, string fallback)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        private static string SanitizeText(string value, string fallback)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }
    }

    [DisallowMultipleComponent]
    public sealed class NarrativeCatalogService : MonoBehaviour
    {
        public const string DefaultCatalogAddress = "Assets/Data/Story/NarrativeCatalog";

        public static NarrativeCatalogService Instance { get; private set; }

        [SerializeField] private string catalogAddress = DefaultCatalogAddress;

        private NarrativeCatalogData catalog = NarrativeCatalogUtility.CreateDefault();
        private bool hasLoadedCatalog;
        private bool isLoadingCatalog;

        public bool HasCatalogLoaded => hasLoadedCatalog;
        public NarrativeCatalogData Catalog => catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            GetOrCreateInstance();
        }

        public static NarrativeCatalogService GetOrCreateInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            NarrativeCatalogService existing = FindFirstObjectByType<NarrativeCatalogService>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject serviceObject = new(nameof(NarrativeCatalogService));
            return serviceObject.AddComponent<NarrativeCatalogService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public IEnumerator LoadCatalogIfNeededCo()
        {
            if (hasLoadedCatalog)
            {
                yield break;
            }

            while (isLoadingCatalog)
            {
                yield return null;
            }

            if (hasLoadedCatalog)
            {
                yield break;
            }

            isLoadingCatalog = true;
            catalog = NarrativeCatalogUtility.CreateDefault();
            string address = ResolveCatalogAddress();
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(address);
            yield return handle;

            try
            {
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    GameDebug.LogWarning($"[NarrativeCatalogService] Failed to load narrative catalog JSON at '{address}'.");
                    yield break;
                }

                if (!NarrativeCatalogUtility.TryDeserializeCatalogJson(handle.Result.text, out NarrativeCatalogData parsedCatalog, out string errorMessage))
                {
                    GameDebug.LogWarning($"[NarrativeCatalogService] {errorMessage}");
                    yield break;
                }

                catalog = parsedCatalog;
                hasLoadedCatalog = true;
            }
            finally
            {
                isLoadingCatalog = false;
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        public bool TryUseCatalog(NarrativeCatalogData candidateCatalog, out string errorMessage)
        {
            if (!NarrativeCatalogUtility.TrySanitize(candidateCatalog, out NarrativeCatalogData sanitizedCatalog, out errorMessage))
            {
                return false;
            }

            catalog = sanitizedCatalog;
            hasLoadedCatalog = true;
            isLoadingCatalog = false;
            return true;
        }

        public void ResetLoadedCatalogForTest()
        {
            catalog = NarrativeCatalogUtility.CreateDefault();
            hasLoadedCatalog = false;
            isLoadingCatalog = false;
        }

        private string ResolveCatalogAddress()
        {
            return string.IsNullOrWhiteSpace(catalogAddress)
                ? DefaultCatalogAddress
                : catalogAddress.Trim();
        }
    }
}
