using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kernel.UI
{
    /// <summary>
    /// 结算结果类型。
    /// </summary>
    public enum SettlementOutcome
    {
        Victory = 0,
        Defeat = 1,
    }

    /// <summary>
    /// 结算界面中的单条名称计数条目。
    /// </summary>
    [Serializable]
    public readonly struct SettlementCountEntry
    {
        public SettlementCountEntry(string displayName, int count)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName.Trim();
            Count = Math.Max(0, count);
        }

        public string DisplayName { get; }
        public int Count { get; }
    }

    /// <summary>
    /// 对局结束后用于渲染结算界面的不可变快照。
    /// </summary>
    public sealed class SettlementSnapshot
    {
        private static readonly IReadOnlyList<SettlementCountEntry> EmptyEntries = Array.Empty<SettlementCountEntry>();

        public static SettlementSnapshot Empty { get; } = new(
            SettlementOutcome.Defeat,
            0,
            0,
            EmptyEntries,
            EmptyEntries);

        public SettlementSnapshot(
            SettlementOutcome outcome,
            int completedWaveCount,
            int defeatedBossCount,
            IReadOnlyList<SettlementCountEntry> harvestEntries,
            IReadOnlyList<SettlementCountEntry> summaryEntries)
        {
            Outcome = outcome;
            CompletedWaveCount = Math.Max(0, completedWaveCount);
            DefeatedBossCount = Math.Max(0, defeatedBossCount);
            HarvestEntries = harvestEntries ?? EmptyEntries;
            SummaryEntries = summaryEntries ?? EmptyEntries;
        }

        public SettlementOutcome Outcome { get; }
        public int CompletedWaveCount { get; }
        public int DefeatedBossCount { get; }
        public IReadOnlyList<SettlementCountEntry> HarvestEntries { get; }
        public IReadOnlyList<SettlementCountEntry> SummaryEntries { get; }
    }

    /// <summary>
    /// 结算文案目录的 JSON 根对象。
    /// </summary>
    [Serializable]
    public sealed class SettlementPresentationCatalogData
    {
        [JsonProperty("victoryTitles")]
        public List<string> VictoryTitles { get; set; } = new();

        [JsonProperty("defeatTitles")]
        public List<string> DefeatTitles { get; set; } = new();

        [JsonProperty("victoryResultTemplate")]
        public string VictoryResultTemplate { get; set; } = string.Empty;

        [JsonProperty("defeatResultTemplate")]
        public string DefeatResultTemplate { get; set; } = string.Empty;

        [JsonProperty("harvestHeader")]
        public string HarvestHeader { get; set; } = string.Empty;

        [JsonProperty("harvestEmptyText")]
        public string HarvestEmptyText { get; set; } = string.Empty;

        [JsonProperty("summaryHeader")]
        public string SummaryHeader { get; set; } = string.Empty;

        [JsonProperty("summaryEmptyText")]
        public string SummaryEmptyText { get; set; } = string.Empty;
    }

    /// <summary>
    /// 负责解析并兜底结算文案目录。
    /// </summary>
    public static class SettlementPresentationCatalogUtility
    {
        private static readonly string[] DefaultVictoryTitles =
        {
            "凯旋而归",
            "战斗胜利",
            "你活下来了",
        };

        private static readonly string[] DefaultDefeatTitles =
        {
            "功亏一篑",
            "败北",
            "仍有余火",
        };

        /// <summary>
        /// summary: 将结算文案目录 JSON 解析为运行时数据；失败时返回 false。
        /// param name="jsonText": 需要解析的原始 JSON 文本
        /// param name="catalog": 输出的已规范化目录
        /// param name="errorMessage": 解析失败时的错误原因
        /// returns: 解析成功时返回 true
        /// </summary>
        public static bool TryDeserializeCatalogJson(
            string jsonText,
            out SettlementPresentationCatalogData catalog,
            out string errorMessage)
        {
            catalog = null;
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Settlement presentation catalog JSON is empty.";
                return false;
            }

            try
            {
                SettlementPresentationCatalogData rawCatalog = JsonConvert.DeserializeObject<SettlementPresentationCatalogData>(jsonText);
                catalog = Sanitize(rawCatalog);
                return true;
            }
            catch (JsonException exception)
            {
                errorMessage = $"Settlement presentation catalog JSON is invalid: {exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// summary: 返回一份可直接使用的默认结算文案目录。
        /// param: 无
        /// returns: 默认结算文案目录
        /// </summary>
        public static SettlementPresentationCatalogData CreateDefault()
        {
            return new SettlementPresentationCatalogData
            {
                VictoryTitles = new List<string>(DefaultVictoryTitles),
                DefeatTitles = new List<string>(DefaultDefeatTitles),
                VictoryResultTemplate = "你击败了{waves}波敌人，{bosses}个boss。",
                DefeatResultTemplate = "你击败了{waves}波敌人，{bosses}个boss。",
                HarvestHeader = "你获得了：",
                HarvestEmptyText = "本轮没有获得长期收益",
                SummaryHeader = "你击败了：",
                SummaryEmptyText = "本轮尚未击败任何敌人",
            };
        }

        /// <summary>
        /// summary: 规整目录里的标题池和空文本；缺失字段会回退到默认值。
        /// param name="catalog": 候选目录数据
        /// returns: 可直接用于 UI 渲染的目录
        /// </summary>
        public static SettlementPresentationCatalogData Sanitize(SettlementPresentationCatalogData catalog)
        {
            SettlementPresentationCatalogData sanitized = catalog ?? CreateDefault();
            List<string> victoryTitles = SanitizeTitlePool(sanitized.VictoryTitles, DefaultVictoryTitles);
            List<string> defeatTitles = SanitizeTitlePool(sanitized.DefeatTitles, DefaultDefeatTitles);

            return new SettlementPresentationCatalogData
            {
                VictoryTitles = victoryTitles,
                DefeatTitles = defeatTitles,
                VictoryResultTemplate = SanitizeText(sanitized.VictoryResultTemplate, "你击败了{waves}波敌人，{bosses}个boss。"),
                DefeatResultTemplate = SanitizeText(sanitized.DefeatResultTemplate, "你击败了{waves}波敌人，{bosses}个boss。"),
                HarvestHeader = SanitizeText(sanitized.HarvestHeader, "你获得了："),
                HarvestEmptyText = SanitizeText(sanitized.HarvestEmptyText, "本轮没有获得长期收益"),
                SummaryHeader = SanitizeText(sanitized.SummaryHeader, "你击败了："),
                SummaryEmptyText = SanitizeText(sanitized.SummaryEmptyText, "本轮尚未击败任何敌人"),
            };
        }

        private static List<string> SanitizeTitlePool(IEnumerable<string> titles, IReadOnlyList<string> fallback)
        {
            List<string> sanitized = new();
            if (titles != null)
            {
                foreach (string title in titles)
                {
                    string trimmed = title != null ? title.Trim() : string.Empty;
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        sanitized.Add(trimmed);
                    }
                }
            }

            if (sanitized.Count > 0)
            {
                return sanitized;
            }

            return new List<string>(fallback);
        }

        private static string SanitizeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
