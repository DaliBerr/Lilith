using System.IO;
using System.Collections.Generic;
using Kernel.Quest;
using NUnit.Framework;
using UnityEngine;

public sealed class QuestCatalogJsonUtilityTests
{
    [Test]
    public void TryDeserializeCatalogJson_DuplicateQuestId_IsRejected()
    {
        string json = @"{
  ""quests"": [
    {
      ""id"": ""quest_dup"",
      ""text"": ""First"",
      ""completion"": [{ ""kind"": ""combat_victory_count_at_least"", ""value"": 1 }],
      ""rewards"": [{ ""kind"": ""remnants"", ""amount"": 1 }]
    },
    {
      ""id"": ""quest_dup"",
      ""text"": ""Second"",
      ""completion"": [{ ""kind"": ""combat_victory_count_at_least"", ""value"": 1 }],
      ""rewards"": [{ ""kind"": ""remnants"", ""amount"": 1 }]
    }
  ]
}";

        bool success = QuestCatalogJsonUtility.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("duplicated"));
    }

    [Test]
    public void TryDeserializeCatalogJson_EmptyQuestText_IsRejected()
    {
        string json = @"{
  ""quests"": [
    {
      ""id"": ""quest_empty_text"",
      ""text"": ""   "",
      ""completion"": [{ ""kind"": ""combat_victory_count_at_least"", ""value"": 1 }],
      ""rewards"": [{ ""kind"": ""remnants"", ""amount"": 1 }]
    }
  ]
}";

        bool success = QuestCatalogJsonUtility.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("missing display text"));
    }

    [Test]
    public void TryDeserializeCatalogJson_UnsupportedRewardKind_IsRejected()
    {
        string json = @"{
  ""quests"": [
    {
      ""id"": ""quest_bad_reward"",
      ""text"": ""Bad reward kind"",
      ""completion"": [{ ""kind"": ""combat_victory_count_at_least"", ""value"": 1 }],
      ""rewards"": [{ ""kind"": ""not_supported"", ""amount"": 1 }]
    }
  ]
}";

        bool success = QuestCatalogJsonUtility.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("invalid").IgnoreCase);
    }

    [Test]
    public void TryDeserializeCatalogJson_MissingTokenAddressForTokenConditionOrReward_IsRejected()
    {
        string json = @"{
  ""quests"": [
    {
      ""id"": ""quest_missing_token_refs"",
      ""text"": ""Needs token refs"",
      ""prerequisites"": [{ ""kind"": ""inventory_contains_token"" }],
      ""completion"": [{ ""kind"": ""combat_victory_count_at_least"", ""value"": 1 }],
      ""rewards"": [{ ""kind"": ""inventory_token"" }]
    }
  ]
}";

        bool success = QuestCatalogJsonUtility.TryDeserializeCatalogJson(json, out _, out string errorMessage);

        Assert.That(success, Is.False);
        Assert.That(errorMessage, Does.Contain("tokenAddress"));
    }

    [Test]
    public void CollectTokenAddresses_ReturnsDistinctAddressesFromConditionsAndRewards()
    {
        QuestCatalogData catalog = new()
        {
            Quests = new List<QuestDefinitionData>
            {
                new()
                {
                    Id = "quest_tokens",
                    Text = "Token refs",
                    Prerequisites = new List<QuestConditionData>
                    {
                        new() { Kind = QuestConditionKind.InventoryContainsToken, TokenAddress = "token/a" }
                    },
                    Completion = new List<QuestConditionData>
                    {
                        new() { Kind = QuestConditionKind.InventoryTokenCountAtLeast, TokenAddress = "token/a", Value = 2 }
                    },
                    Rewards = new List<QuestRewardData>
                    {
                        new() { Kind = QuestRewardKind.InventoryToken, TokenAddress = "token/b" }
                    }
                }
            }
        };

        IReadOnlyCollection<string> addresses = QuestCatalogJsonUtility.CollectTokenAddresses(catalog);

        Assert.That(addresses, Is.EquivalentTo(new[]
        {
            "token/a",
            "token/b"
        }));
    }

    [Test]
    public void TutorialQuestCatalog_IsValidAndContainsExpectedGuideChain()
    {
        string catalogPath = Path.Combine(Application.dataPath, "Data", "Quest", "QuestCatalog.json");
        string json = File.ReadAllText(catalogPath);

        bool success = QuestCatalogJsonUtility.TryDeserializeCatalogJson(json, out QuestCatalogData catalog, out string errorMessage);

        Assert.That(success, Is.True, errorMessage);
        Assert.That(catalog.Quests.Count, Is.EqualTo(3));

        Assert.That(catalog.Quests[0].Id, Is.EqualTo("tutorial_open_backpack"));
        Assert.That(catalog.Quests[0].Prerequisites[0].StoryFlagId, Is.EqualTo(TutorialQuestConstants.IntroductionReadFlagId));
        Assert.That(catalog.Quests[0].Completion[0].StoryFlagId, Is.EqualTo(TutorialQuestConstants.BackpackOpenedFlagId));
        Assert.That(catalog.Quests[0].Rewards[0].TokenAddress, Is.EqualTo(TutorialQuestConstants.InitCoreTokenAddress));

        Assert.That(catalog.Quests[1].Id, Is.EqualTo("tutorial_compile_spellbook"));
        Assert.That(catalog.Quests[1].Prerequisites[0].TokenAddress, Is.EqualTo(TutorialQuestConstants.InitCoreTokenAddress));
        Assert.That(catalog.Quests[1].Completion[0].StoryFlagId, Is.EqualTo(TutorialQuestConstants.SpellBookCompiledFlagId));
        Assert.That(catalog.Quests[1].Rewards[0].StoryFlagId, Is.EqualTo(TutorialQuestConstants.TeleporterUnlockedFlagId));

        Assert.That(catalog.Quests[2].Id, Is.EqualTo("tutorial_enter_teleporter"));
        Assert.That(catalog.Quests[2].Prerequisites[0].StoryFlagId, Is.EqualTo(TutorialQuestConstants.TeleporterUnlockedFlagId));
        Assert.That(catalog.Quests[2].Completion[0].StoryFlagId, Is.EqualTo(TutorialQuestConstants.TeleporterTriggeredFlagId));
        Assert.That(catalog.Quests[2].Rewards[0].StoryFlagId, Is.EqualTo(TutorialQuestConstants.GuideChainFinishedFlagId));
    }
}
