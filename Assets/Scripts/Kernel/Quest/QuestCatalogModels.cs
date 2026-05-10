using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Vocalith.Localization;

namespace Kernel.Quest
{
    /// <summary>
    /// 任务条件种类。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum QuestConditionKind
    {
        [EnumMember(Value = "story_flag_set")]
        StoryFlagSet = 0,

        [EnumMember(Value = "lifetime_stat_at_least")]
        LifetimeStatAtLeast = 1,

        [EnumMember(Value = "remnants_at_least")]
        RemnantsAtLeast = 2,

        [EnumMember(Value = "inventory_contains_token")]
        InventoryContainsToken = 3,

        [EnumMember(Value = "inventory_token_count_at_least")]
        InventoryTokenCountAtLeast = 4,

        [EnumMember(Value = "enemy_kill_count_at_least")]
        EnemyKillCountAtLeast = 5,

        [EnumMember(Value = "combat_victory_count_at_least")]
        CombatVictoryCountAtLeast = 6,

        [EnumMember(Value = "boss_kill_count_at_least")]
        BossKillCountAtLeast = 7,
    }

    /// <summary>
    /// 任务奖励种类。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum QuestRewardKind
    {
        [EnumMember(Value = "inventory_token")]
        InventoryToken = 0,

        [EnumMember(Value = "remnants")]
        Remnants = 1,

        [EnumMember(Value = "unlock_id")]
        UnlockId = 2,

        [EnumMember(Value = "story_flag_set")]
        StoryFlagSet = 3,

        [EnumMember(Value = "lifetime_stat_delta")]
        LifetimeStatDelta = 4,
    }

    /// <summary>
    /// 任务 JSON 根对象。
    /// </summary>
    [Serializable]
    public sealed class QuestCatalogData
    {
        public List<QuestDefinitionData> Quests { get; set; } = new();
    }

    /// <summary>
    /// 单个任务定义。
    /// </summary>
    [Serializable]
    public sealed class QuestDefinitionData
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<QuestConditionData> Prerequisites { get; set; } = new();
        public List<QuestConditionData> Completion { get; set; } = new();
        public List<QuestRewardData> Rewards { get; set; } = new();
    }

    /// <summary>
    /// 单条任务条件定义。
    /// </summary>
    [Serializable]
    public sealed class QuestConditionData
    {
        public QuestConditionKind Kind { get; set; }
        public string StoryFlagId { get; set; } = string.Empty;
        public string LifetimeStatKey { get; set; } = string.Empty;
        public int Value { get; set; }
        public string TokenAddress { get; set; } = string.Empty;
    }

    /// <summary>
    /// 单条任务奖励定义。
    /// </summary>
    [Serializable]
    public sealed class QuestRewardData
    {
        public QuestRewardKind Kind { get; set; }
        public string TokenAddress { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string UnlockId { get; set; } = string.Empty;
        public string StoryFlagId { get; set; } = string.Empty;
        public string LifetimeStatKey { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    /// <summary>
    /// 统一负责任务目录 JSON 的解析与校验。
    /// </summary>
    public static class QuestCatalogJsonUtility
    {
        /// <summary>
        /// summary: 解析任务目录 JSON，并输出一份经过规范化和校验的目录对象。
        /// param name="jsonText": 需要解析的原始 JSON 文本
        /// param name="catalog": 输出的有效任务目录
        /// param name="errorMessage": 解析或校验失败时的错误原因
        /// returns: 解析和校验都成功时返回 true
        /// </summary>
        public static bool TryDeserializeCatalogJson(string jsonText, out QuestCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                errorMessage = "Quest catalog JSON is empty.";
                return false;
            }

            QuestCatalogData rawCatalog;
            try
            {
                rawCatalog = LocalizedJsonUtility.DeserializeLocalized<QuestCatalogData>(
                    jsonText,
                    "QuestCatalog",
                    settings: null);
            }
            catch (JsonException exception)
            {
                errorMessage = $"Quest catalog JSON is invalid: {exception.Message}";
                return false;
            }

            return TryBuildValidatedCatalog(rawCatalog, out catalog, out errorMessage);
        }

        /// <summary>
        /// summary: 收集目录里所有任务条件与奖励引用到的 token Addressables 地址。
        /// param name="catalog": 已通过校验的任务目录
        /// returns: 去重后的 token 地址只读集合
        /// </summary>
        public static IReadOnlyCollection<string> CollectTokenAddresses(QuestCatalogData catalog)
        {
            HashSet<string> addresses = new(StringComparer.Ordinal);
            if (catalog?.Quests == null)
            {
                return addresses;
            }

            for (int questIndex = 0; questIndex < catalog.Quests.Count; questIndex++)
            {
                QuestDefinitionData quest = catalog.Quests[questIndex];
                CollectConditionTokenAddresses(quest?.Prerequisites, addresses);
                CollectConditionTokenAddresses(quest?.Completion, addresses);
                CollectRewardTokenAddresses(quest?.Rewards, addresses);
            }

            return addresses;
        }

        /// <summary>
        /// summary: 判断当前任务是否在完成条件里依赖指定的事件型计数。
        /// param name="quest": 需要检查的任务定义
        /// param name="kind": 需要检查的条件类型
        /// returns: 完成条件中包含目标类型时返回 true
        /// </summary>
        public static bool UsesCompletionCondition(QuestDefinitionData quest, QuestConditionKind kind)
        {
            if (quest?.Completion == null)
            {
                return false;
            }

            for (int index = 0; index < quest.Completion.Count; index++)
            {
                if (quest.Completion[index] != null && quest.Completion[index].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildValidatedCatalog(QuestCatalogData rawCatalog, out QuestCatalogData catalog, out string errorMessage)
        {
            catalog = null;
            errorMessage = null;

            List<QuestDefinitionData> rawQuests = rawCatalog?.Quests ?? new List<QuestDefinitionData>();
            List<QuestDefinitionData> validatedQuests = new(rawQuests.Count);
            HashSet<string> seenQuestIds = new(StringComparer.Ordinal);

            for (int questIndex = 0; questIndex < rawQuests.Count; questIndex++)
            {
                QuestDefinitionData rawQuest = rawQuests[questIndex];
                if (!TryValidateQuest(rawQuest, questIndex, seenQuestIds, out QuestDefinitionData validatedQuest, out errorMessage))
                {
                    return false;
                }

                validatedQuests.Add(validatedQuest);
            }

            catalog = new QuestCatalogData
            {
                Quests = validatedQuests
            };
            return true;
        }

        private static bool TryValidateQuest(
            QuestDefinitionData rawQuest,
            int questIndex,
            ISet<string> seenQuestIds,
            out QuestDefinitionData validatedQuest,
            out string errorMessage)
        {
            validatedQuest = null;
            errorMessage = null;

            if (rawQuest == null)
            {
                errorMessage = $"Quest entry at index {questIndex} is null.";
                return false;
            }

            string questId = rawQuest.Id != null ? rawQuest.Id.Trim() : string.Empty;
            if (string.IsNullOrEmpty(questId))
            {
                errorMessage = $"Quest entry at index {questIndex} is missing a valid id.";
                return false;
            }

            if (!seenQuestIds.Add(questId))
            {
                errorMessage = $"Quest id '{questId}' is duplicated.";
                return false;
            }

            string text = rawQuest.Text != null ? rawQuest.Text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                errorMessage = $"Quest '{questId}' is missing display text.";
                return false;
            }

            if (!TryValidateConditions(questId, "prerequisites", rawQuest.Prerequisites, allowEmpty: true, out List<QuestConditionData> prerequisites, out errorMessage))
            {
                return false;
            }

            if (!TryValidateConditions(questId, "completion", rawQuest.Completion, allowEmpty: false, out List<QuestConditionData> completion, out errorMessage))
            {
                return false;
            }

            if (!TryValidateRewards(questId, rawQuest.Rewards, out List<QuestRewardData> rewards, out errorMessage))
            {
                return false;
            }

            validatedQuest = new QuestDefinitionData
            {
                Id = questId,
                Text = text,
                Prerequisites = prerequisites,
                Completion = completion,
                Rewards = rewards,
            };
            return true;
        }

        private static bool TryValidateConditions(
            string questId,
            string sectionName,
            List<QuestConditionData> rawConditions,
            bool allowEmpty,
            out List<QuestConditionData> validatedConditions,
            out string errorMessage)
        {
            validatedConditions = new();
            errorMessage = null;

            if (rawConditions == null || rawConditions.Count <= 0)
            {
                if (!allowEmpty)
                {
                    errorMessage = $"Quest '{questId}' requires at least one {sectionName} condition.";
                    return false;
                }

                return true;
            }

            for (int index = 0; index < rawConditions.Count; index++)
            {
                if (!TryValidateCondition(questId, sectionName, index, rawConditions[index], out QuestConditionData validatedCondition, out errorMessage))
                {
                    return false;
                }

                validatedConditions.Add(validatedCondition);
            }

            return true;
        }

        private static bool TryValidateCondition(
            string questId,
            string sectionName,
            int index,
            QuestConditionData rawCondition,
            out QuestConditionData validatedCondition,
            out string errorMessage)
        {
            validatedCondition = null;
            errorMessage = null;

            if (rawCondition == null)
            {
                errorMessage = $"Quest '{questId}' has a null condition in {sectionName}[{index}].";
                return false;
            }

            QuestConditionData condition = new()
            {
                Kind = rawCondition.Kind,
                StoryFlagId = rawCondition.StoryFlagId != null ? rawCondition.StoryFlagId.Trim() : string.Empty,
                LifetimeStatKey = rawCondition.LifetimeStatKey != null ? rawCondition.LifetimeStatKey.Trim() : string.Empty,
                Value = rawCondition.Value,
                TokenAddress = rawCondition.TokenAddress != null ? rawCondition.TokenAddress.Trim() : string.Empty,
            };

            switch (condition.Kind)
            {
                case QuestConditionKind.StoryFlagSet:
                    if (string.IsNullOrEmpty(condition.StoryFlagId))
                    {
                        errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] requires storyFlagId.";
                        return false;
                    }
                    break;

                case QuestConditionKind.LifetimeStatAtLeast:
                    if (string.IsNullOrEmpty(condition.LifetimeStatKey))
                    {
                        errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] requires lifetimeStatKey.";
                        return false;
                    }

                    if (condition.Value < 1)
                    {
                        errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] requires value >= 1.";
                        return false;
                    }
                    break;

                case QuestConditionKind.RemnantsAtLeast:
                case QuestConditionKind.EnemyKillCountAtLeast:
                case QuestConditionKind.CombatVictoryCountAtLeast:
                case QuestConditionKind.BossKillCountAtLeast:
                    if (condition.Value < 1)
                    {
                        errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] requires value >= 1.";
                        return false;
                    }
                    break;

                case QuestConditionKind.InventoryContainsToken:
                    if (string.IsNullOrEmpty(condition.TokenAddress))
                    {
                        errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] requires tokenAddress.";
                        return false;
                    }
                    break;

                case QuestConditionKind.InventoryTokenCountAtLeast:
                    if (string.IsNullOrEmpty(condition.TokenAddress))
                    {
                        errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] requires tokenAddress.";
                        return false;
                    }

                    if (condition.Value < 1)
                    {
                        errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] requires value >= 1.";
                        return false;
                    }
                    break;

                default:
                    errorMessage = $"Quest '{questId}' condition {sectionName}[{index}] uses unsupported kind '{condition.Kind}'.";
                    return false;
            }

            validatedCondition = condition;
            return true;
        }

        private static bool TryValidateRewards(
            string questId,
            List<QuestRewardData> rawRewards,
            out List<QuestRewardData> validatedRewards,
            out string errorMessage)
        {
            validatedRewards = new();
            errorMessage = null;

            if (rawRewards == null || rawRewards.Count <= 0)
            {
                errorMessage = $"Quest '{questId}' requires at least one reward.";
                return false;
            }

            for (int index = 0; index < rawRewards.Count; index++)
            {
                if (!TryValidateReward(questId, index, rawRewards[index], out QuestRewardData validatedReward, out errorMessage))
                {
                    return false;
                }

                validatedRewards.Add(validatedReward);
            }

            return true;
        }

        private static bool TryValidateReward(
            string questId,
            int index,
            QuestRewardData rawReward,
            out QuestRewardData validatedReward,
            out string errorMessage)
        {
            validatedReward = null;
            errorMessage = null;

            if (rawReward == null)
            {
                errorMessage = $"Quest '{questId}' has a null reward in rewards[{index}].";
                return false;
            }

            QuestRewardData reward = new()
            {
                Kind = rawReward.Kind,
                TokenAddress = rawReward.TokenAddress != null ? rawReward.TokenAddress.Trim() : string.Empty,
                Amount = rawReward.Amount,
                UnlockId = rawReward.UnlockId != null ? rawReward.UnlockId.Trim() : string.Empty,
                StoryFlagId = rawReward.StoryFlagId != null ? rawReward.StoryFlagId.Trim() : string.Empty,
                LifetimeStatKey = rawReward.LifetimeStatKey != null ? rawReward.LifetimeStatKey.Trim() : string.Empty,
                Value = rawReward.Value,
            };

            switch (reward.Kind)
            {
                case QuestRewardKind.InventoryToken:
                    if (string.IsNullOrEmpty(reward.TokenAddress))
                    {
                        errorMessage = $"Quest '{questId}' reward rewards[{index}] requires tokenAddress.";
                        return false;
                    }
                    break;

                case QuestRewardKind.Remnants:
                    if (reward.Amount < 1)
                    {
                        errorMessage = $"Quest '{questId}' reward rewards[{index}] requires amount >= 1.";
                        return false;
                    }
                    break;

                case QuestRewardKind.UnlockId:
                    if (string.IsNullOrEmpty(reward.UnlockId))
                    {
                        errorMessage = $"Quest '{questId}' reward rewards[{index}] requires unlockId.";
                        return false;
                    }
                    break;

                case QuestRewardKind.StoryFlagSet:
                    if (string.IsNullOrEmpty(reward.StoryFlagId))
                    {
                        errorMessage = $"Quest '{questId}' reward rewards[{index}] requires storyFlagId.";
                        return false;
                    }
                    break;

                case QuestRewardKind.LifetimeStatDelta:
                    if (string.IsNullOrEmpty(reward.LifetimeStatKey))
                    {
                        errorMessage = $"Quest '{questId}' reward rewards[{index}] requires lifetimeStatKey.";
                        return false;
                    }

                    if (reward.Value == 0)
                    {
                        errorMessage = $"Quest '{questId}' reward rewards[{index}] requires a non-zero value.";
                        return false;
                    }
                    break;

                default:
                    errorMessage = $"Quest '{questId}' reward rewards[{index}] uses unsupported kind '{reward.Kind}'.";
                    return false;
            }

            validatedReward = reward;
            return true;
        }

        private static void CollectConditionTokenAddresses(IReadOnlyList<QuestConditionData> conditions, ISet<string> addresses)
        {
            if (conditions == null || addresses == null)
            {
                return;
            }

            for (int index = 0; index < conditions.Count; index++)
            {
                QuestConditionData condition = conditions[index];
                if (condition == null || string.IsNullOrEmpty(condition.TokenAddress))
                {
                    continue;
                }

                addresses.Add(condition.TokenAddress);
            }
        }

        private static void CollectRewardTokenAddresses(IReadOnlyList<QuestRewardData> rewards, ISet<string> addresses)
        {
            if (rewards == null || addresses == null)
            {
                return;
            }

            for (int index = 0; index < rewards.Count; index++)
            {
                QuestRewardData reward = rewards[index];
                if (reward == null || string.IsNullOrEmpty(reward.TokenAddress))
                {
                    continue;
                }

                addresses.Add(reward.TokenAddress);
            }
        }
    }
}
