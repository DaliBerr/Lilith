using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kernel.UI
{
    /// <summary>
    /// Hint 目录 JSON 根对象。
    /// </summary>
    [Serializable]
    public sealed class HintCatalogData
    {
        [JsonProperty("categories")]
        public List<HintCategoryData> Categories { get; set; } = new();
    }

    /// <summary>
    /// Hint 分类数据，映射到顶部 Catalog。
    /// </summary>
    [Serializable]
    public sealed class HintCategoryData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("entries")]
        public List<HintEntryData> Entries { get; set; } = new();
    }

    /// <summary>
    /// Hint 条目数据，映射到左侧条目与右侧正文。
    /// </summary>
    [Serializable]
    public sealed class HintEntryData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    /// <summary>
    /// Hint 目录 JSON 解析与兜底工具。
    /// </summary>
    public static class HintCatalogUtility
    {
        /// <summary>
        /// summary: 返回一份可直接使用的空 Hint 目录。
        /// param: 无
        /// returns: 默认 Hint 目录对象
        /// </summary>
        public static HintCatalogData CreateDefault()
        {
            return new HintCatalogData();
        }

        /// <summary>
        /// summary: 解析 Hint 目录 JSON，并输出一份经过规范化的目录对象。
        /// param name="jsonText": 原始 JSON 文本
        /// param name="catalog": 输出的规范化目录
        /// param name="errorMessage": 失败时的错误信息
        /// returns: 成功解析并规范化时返回 true
        /// </summary>
        public static bool TryDeserializeCatalogJson(string jsonText, out HintCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Hint catalog JSON is empty.";
                return false;
            }

            try
            {
                HintCatalogData rawCatalog = JsonConvert.DeserializeObject<HintCatalogData>(jsonText);
                catalog = Sanitize(rawCatalog);
                return true;
            }
            catch (JsonException exception)
            {
                errorMessage = $"Hint catalog JSON is invalid: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// summary: 规范化分类和条目 id/title/content，去除空白和重复键。
        /// param name="catalog": 待规范化目录
        /// returns: 可直接用于运行时渲染的目录
        /// </summary>
        public static HintCatalogData Sanitize(HintCatalogData catalog)
        {
            List<HintCategoryData> rawCategories = catalog?.Categories ?? new List<HintCategoryData>();
            List<HintCategoryData> sanitizedCategories = new(rawCategories.Count);
            HashSet<string> seenCategoryIds = new(StringComparer.Ordinal);

            for (int categoryIndex = 0; categoryIndex < rawCategories.Count; categoryIndex++)
            {
                HintCategoryData rawCategory = rawCategories[categoryIndex];
                string categoryId = SanitizeIdentifier(rawCategory?.Id, $"category_{categoryIndex + 1}");
                categoryId = EnsureUniqueIdentifier(categoryId, seenCategoryIds);

                List<HintEntryData> sanitizedEntries = SanitizeEntries(rawCategory?.Entries);
                string titleFallback = categoryId;
                string categoryTitle = SanitizeText(rawCategory?.Title, titleFallback);

                sanitizedCategories.Add(new HintCategoryData
                {
                    Id = categoryId,
                    Title = categoryTitle,
                    Entries = sanitizedEntries,
                });
            }

            return new HintCatalogData
            {
                Categories = sanitizedCategories,
            };
        }

        private static List<HintEntryData> SanitizeEntries(List<HintEntryData> entries)
        {
            List<HintEntryData> rawEntries = entries ?? new List<HintEntryData>();
            List<HintEntryData> sanitizedEntries = new(rawEntries.Count);
            HashSet<string> seenEntryIds = new(StringComparer.Ordinal);

            for (int entryIndex = 0; entryIndex < rawEntries.Count; entryIndex++)
            {
                HintEntryData rawEntry = rawEntries[entryIndex];
                string entryId = SanitizeIdentifier(rawEntry?.Id, $"entry_{entryIndex + 1}");
                entryId = EnsureUniqueIdentifier(entryId, seenEntryIds);
                string entryTitle = SanitizeText(rawEntry?.Title, entryId);
                string entryContent = SanitizeText(rawEntry?.Content, string.Empty);

                sanitizedEntries.Add(new HintEntryData
                {
                    Id = entryId,
                    Title = entryTitle,
                    Content = entryContent,
                });
            }

            return sanitizedEntries;
        }

        private static string SanitizeIdentifier(string value, string fallback)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        private static string EnsureUniqueIdentifier(string identifier, ISet<string> seen)
        {
            if (!seen.Contains(identifier))
            {
                seen.Add(identifier);
                return identifier;
            }

            int suffix = 2;
            string uniqueId = identifier;
            do
            {
                uniqueId = $"{identifier}_{suffix}";
                suffix++;
            }
            while (!seen.Add(uniqueId));

            return uniqueId;
        }

        private static string SanitizeText(string value, string fallback)
        {
            string trimmed = value != null ? value.Trim() : string.Empty;
            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }
    }
}
