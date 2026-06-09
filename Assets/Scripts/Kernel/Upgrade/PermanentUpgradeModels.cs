using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Kernel.Upgrade
{
    /// <summary>
    /// 永久升级当前支持的效果类型。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PermanentUpgradeEffectType
    {
        DamageMultiplierBonus = 0,
    }

    /// <summary>
    /// 永久升级科技树节点第一版支持的外观形状。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PermanentUpgradeNodeShape
    {
        Rectangle = 0,
        Circle = 1,
        Diamond = 2,
        Hexagon = 3,
    }

    /// <summary>
    /// 永久升级 JSON 中使用的二维数值。
    /// </summary>
    [Serializable]
    public sealed class PermanentUpgradeVector2Data
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }
    }

    /// <summary>
    /// 永久升级科技树节点之间的连线配置。
    /// </summary>
    [Serializable]
    public sealed class PermanentUpgradeEdgeData
    {
        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("width")]
        public float Width { get; set; }
    }

    /// <summary>
    /// 永久升级目录的根对象。
    /// </summary>
    [Serializable]
    public sealed class PermanentUpgradeCatalogData
    {
        [JsonProperty("canvasSize")]
        public PermanentUpgradeVector2Data CanvasSize { get; set; }

        [JsonProperty("edges")]
        public List<PermanentUpgradeEdgeData> Edges { get; set; } = new();

        [JsonProperty("sections")]
        public List<PermanentUpgradeSectionData> Sections { get; set; } = new();
    }

    /// <summary>
    /// 一个永久升级分区，负责组织一组升级条目。
    /// </summary>
    [Serializable]
    public sealed class PermanentUpgradeSectionData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("entries")]
        public List<PermanentUpgradeEntryData> Entries { get; set; } = new();
    }

    /// <summary>
    /// 单个永久升级条目。
    /// </summary>
    [Serializable]
    public sealed class PermanentUpgradeEntryData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("costRemnants")]
        public int CostRemnants { get; set; }

        [JsonProperty("maxLevel")]
        public int MaxLevel { get; set; } = 1;

        [JsonProperty("effectType")]
        public PermanentUpgradeEffectType EffectType { get; set; } = PermanentUpgradeEffectType.DamageMultiplierBonus;

        [JsonProperty("effectValue")]
        public float EffectValue { get; set; }

        [JsonProperty("requires")]
        public List<string> Requires { get; set; } = new();

        [JsonProperty("position")]
        public PermanentUpgradeVector2Data Position { get; set; }

        [JsonProperty("size")]
        public PermanentUpgradeVector2Data Size { get; set; }

        [JsonProperty("shape")]
        public PermanentUpgradeNodeShape Shape { get; set; } = PermanentUpgradeNodeShape.Rectangle;

        [JsonProperty("iconAddress")]
        public string IconAddress { get; set; }

        [JsonProperty("backgroundColor")]
        public string BackgroundColor { get; set; }

        [JsonProperty("borderColor")]
        public string BorderColor { get; set; }

        [JsonProperty("borderWidth")]
        public float BorderWidth { get; set; }
    }

    /// <summary>
    /// 永久升级购买失败原因。
    /// </summary>
    public enum PermanentUpgradePurchaseFailureReason
    {
        None = 0,
        CatalogNotReady = 1,
        InvalidEntry = 2,
        MaxLevelReached = 3,
        InsufficientRemnants = 4,
        SaveUnavailable = 5,
        PrerequisiteMissing = 6,
    }

    /// <summary>
    /// 永久升级购买结果。
    /// </summary>
    public readonly struct PermanentUpgradePurchaseResult
    {
        public PermanentUpgradePurchaseResult(
            bool succeeded,
            PermanentUpgradePurchaseFailureReason failureReason,
            string entryId,
            int newLevel,
            int remainingRemnants,
            string message)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            EntryId = entryId ?? string.Empty;
            NewLevel = Math.Max(0, newLevel);
            RemainingRemnants = Math.Max(0, remainingRemnants);
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public PermanentUpgradePurchaseFailureReason FailureReason { get; }
        public string EntryId { get; }
        public int NewLevel { get; }
        public int RemainingRemnants { get; }
        public string Message { get; }
    }
}
