using System.Collections;
using System.Reflection;
using Kernel.Bullet;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Vocalith.Localization;

public sealed class LocalizationTests
{
    [SetUp]
    public void SetUp()
    {
        ResetLocalization("en-US");
    }

    [TearDown]
    public void TearDown()
    {
        ResetLocalization("en-US");
    }

    [Test]
    public void StringPack_MergesMatchingLanguageAndSupportsFallbacks()
    {
        bool merged = InvokeMergeStringPack(@"{
  ""language"": ""en-US"",
  ""entries"": {
    ""ui.test.hello"": ""Hello {0}"",
    ""ui.test.plain"": ""Plain""
  }
}");

        Assert.That(merged, Is.True);
        Assert.That(LocalizationManager.Translate("ui.test.plain"), Is.EqualTo("Plain"));
        Assert.That(LocalizationManager.Translate("$ui.test.plain"), Is.EqualTo("Plain"));
        Assert.That(LocalizationManager.Translate("ui.test.missing"), Is.EqualTo("ui.test.missing"));
        Assert.That(LocalizationManager.TranslateOrDefault("ui.test.missing", "Fallback"), Is.EqualTo("Fallback"));
        Assert.That(LocalizationManager.TranslateFormat("ui.test.hello", "World"), Is.EqualTo("Hello World"));
    }

    [Test]
    public void JsonPatch_AppliesToNestedObjectsById()
    {
        bool merged = InvokeMergeJsonPatchPack(@"{
  ""language"": ""en-US"",
  ""domain"": ""OptionsCatalog"",
  ""patches"": {
    ""audio"": { ""title"": ""Audio"" },
    ""master_volume"": { ""title"": ""Master Volume"" }
  }
}");

        Assert.That(merged, Is.True);

        JObject localized = LocalizedJsonUtility.ParseAndLocalize(@"{
  ""categories"": [
    {
      ""id"": ""audio"",
      ""title"": ""音频"",
      ""entries"": [
        { ""id"": ""master_volume"", ""title"": ""主音量"" }
      ]
    }
  ]
}", "OptionsCatalog");

        Assert.That(localized["categories"]?[0]?["title"]?.ToString(), Is.EqualTo("Audio"));
        Assert.That(localized["categories"]?[0]?["entries"]?[0]?["title"]?.ToString(), Is.EqualTo("Master Volume"));
    }

    [Test]
    public void TokenDisplay_UsesKeyWhenPresentAndSerializedTextAsFallback()
    {
        LocalizationManager.RegisterString("token.test.display", "Fire");
        LocalizationManager.RegisterString("token.test.description", "A fire token.");

        CoreTokenData token = ScriptableObject.CreateInstance<CoreTokenData>();
        try
        {
            token.TokenId = "fire";
            token.DisplayText = "火";
            token.Description = "火焰说明";
            token.DisplayTextKey = "token.test.display";
            token.DescriptionKey = "token.test.description";

            Assert.That(token.GetResolvedDisplayText(), Is.EqualTo("Fire"));
            Assert.That(token.GetSelectionDescription(), Is.EqualTo("A fire token."));

            token.DisplayTextKey = "token.test.missing_display";
            token.DescriptionKey = "token.test.missing_description";

            Assert.That(token.GetResolvedDisplayText(), Is.EqualTo("火"));
            Assert.That(token.GetSelectionDescription(), Is.EqualTo("火焰说明"));
        }
        finally
        {
            Object.DestroyImmediate(token);
        }
    }

    [Test]
    public void EnemyDisplay_UsesKeyWhenPresentAndSerializedTextAsFallback()
    {
        LocalizationManager.RegisterString("enemy.test.display", "Scout");
        LocalizationManager.RegisterString("enemy.test.description", "A quick enemy.");

        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        try
        {
            SetPrivateField(definition, "enemyId", "scout");
            SetPrivateField(definition, "displayName", "迅");
            SetPrivateField(definition, "description", "移动极快。");
            SetPrivateField(definition, "displayNameKey", "enemy.test.display");
            SetPrivateField(definition, "descriptionKey", "enemy.test.description");

            Assert.That(definition.DisplayName, Is.EqualTo("Scout"));
            Assert.That(definition.Description, Is.EqualTo("A quick enemy."));

            SetPrivateField(definition, "displayNameKey", "enemy.test.missing_display");
            SetPrivateField(definition, "descriptionKey", "enemy.test.missing_description");

            Assert.That(definition.DisplayName, Is.EqualTo("迅"));
            Assert.That(definition.Description, Is.EqualTo("移动极快。"));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    private static bool InvokeMergeStringPack(string json)
    {
        return (bool)GetPrivateStaticMethod("TryMergeStringPack")
            .Invoke(null, new object[] { "test-pack", json });
    }

    private static bool InvokeMergeJsonPatchPack(string json)
    {
        return (bool)GetPrivateStaticMethod("TryMergeJsonPatchPack")
            .Invoke(null, new object[] { "test-patch", json });
    }

    private static MethodInfo GetPrivateStaticMethod(string name)
    {
        return typeof(LocalizationManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
    }

    private static void ResetLocalization(string languageTag)
    {
        typeof(LocalizationManager)
            .GetProperty(nameof(LocalizationManager.CurrentLanguageTag), BindingFlags.Public | BindingFlags.Static)
            ?.GetSetMethod(nonPublic: true)
            ?.Invoke(null, new object[] { languageTag });

        ((IDictionary)typeof(LocalizationManager)
            .GetField("_stringTable", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null))?.Clear();

        ((IDictionary)typeof(LocalizationManager)
            .GetField("_jsonPatchTable", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null))?.Clear();
    }

    private static void SetPrivateField(object target, string fieldName, string value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(target, value);
    }
}
