using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using VocalithRandom = Vocalith.Random;

public sealed class SpellDescriptionGeneratorTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void GenerateRichText_WithFireSpreadExplosion_HighlightsKeyTerms()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        fire.BurnTriggerCount = 3;
        fire.BurnDamagePerSecond = 3f;
        fire.BurnDuration = 2f;
        BehaviorTokenData spread = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2);
        ValueTokenData valueThree = CreateValueToken("three", "三", 3f);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, false);
        explosion.DefaultExplosionRadius = 48f;
        explosion.ExplosionDamageMultiplier = 0.3f;

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            fire,
            spread,
            valueThree,
            explosion,
        });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            compiledAttack,
            new PlaceableTokenData[] { fire, spread, valueThree, explosion },
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(richText, Does.Contain("<color=#FF7A3D>"));
        Assert.That(richText, Does.Contain("<color=#66C7FF>"));
        Assert.That(richText, Does.Contain("<color=#FF5C7A>"));
        Assert.That(visibleText, Does.Contain("火"));
        Assert.That(visibleText, Does.Contain("三道"));
        Assert.That(visibleText, Does.Contain("散射"));
        Assert.That(visibleText, Does.Contain("爆炸"));
        Assert.That(visibleText, Does.Contain("灼烧"));
        Assert.That(visibleText.Length, Is.LessThanOrEqualTo(80));
    }

    [Test]
    public void GenerateRichText_WithIceHomingHealing_HighlightsBehaviorAndHealing()
    {
        CoreTokenData ice = CreateCoreToken("ice_core", "冰", AttackCoreType.Ice);
        BehaviorTokenData homing = CreateBehaviorToken("homing", "追", AttackBehaviorType.Homing, false, 1);
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, false);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            ice,
            homing,
            healing,
        });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            compiledAttack,
            new PlaceableTokenData[] { ice, homing, healing },
            new VocalithRandom(1));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("冰"));
        Assert.That(visibleText, Does.Contain("追踪"));
        Assert.That(visibleText, Does.Contain("治疗"));
        Assert.That(richText, Does.Contain("<color=#FF5C7A>"));
    }

    [Test]
    public void GenerateRichText_WithCoreOnly_UsesShortDirectDescription()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[] { fire });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            compiledAttack,
            new PlaceableTokenData[] { fire },
            new VocalithRandom(2));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("火"));
        Assert.That(visibleText, Does.Match("直线|直击|直接伤害|射出"));
        Assert.That(visibleText.Length, Is.LessThanOrEqualTo(70));
    }

    [Test]
    public void GenerateRichText_WithoutCore_ReturnsShortPrompt()
    {
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(Array.Empty<BaseTokenData>());

        Assert.DoesNotThrow(() => SpellDescriptionGenerator.GenerateRichText(compiledAttack, null, new VocalithRandom(3)));

        string richText = SpellDescriptionGenerator.GenerateRichText(compiledAttack, null, new VocalithRandom(3));
        string visibleText = StripRichText(richText);
        Assert.That(visibleText, Does.Contain("核心"));
        Assert.That(richText, Does.Contain("<color=#FF7A3D>"));
        Assert.That(visibleText.Length, Is.LessThanOrEqualTo(24));
    }

    [Test]
    public void GenerateRichText_WithCatalogJsonOverrides_UsesExternalPhrases()
    {
        Assert.That(
            SpellDescriptionCatalogData.TryDeserializeJson(CreateCustomCatalogJson(), out SpellDescriptionCatalogData catalog, out string errorMessage),
            Is.True,
            errorMessage);
        CoreTokenData fire = CreateCoreToken("catalog_fire_core", "火", AttackCoreType.Fire);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[] { fire });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            compiledAttack,
            new PlaceableTokenData[] { fire },
            catalog,
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("焰"));
        Assert.That(visibleText, Does.Contain("文案表起咒"));
        Assert.That(visibleText, Does.Contain("穿表而出"));
        Assert.That(visibleText, Does.Contain("表中伤"));
    }

    private CoreTokenData CreateCoreToken(string tokenId, string displayText, AttackCoreType coreType)
    {
        CoreTokenData token = CreateToken<CoreTokenData>(tokenId, displayText);
        token.CoreType = coreType;
        token.Damage = 4f;
        return token;
    }

    private BehaviorTokenData CreateBehaviorToken(string tokenId, string displayText, AttackBehaviorType behaviorType, bool acceptsNumericValue, int defaultProjectileCount)
    {
        BehaviorTokenData token = CreateToken<BehaviorTokenData>(tokenId, displayText);
        token.BehaviorType = behaviorType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultProjectileCount = defaultProjectileCount;
        return token;
    }

    private ResultTokenData CreateResultToken(string tokenId, string displayText, AttackResultType resultType, bool acceptsNumericValue)
    {
        ResultTokenData token = CreateToken<ResultTokenData>(tokenId, displayText);
        token.ResultType = resultType;
        token.AcceptsNumericValue = acceptsNumericValue;
        return token;
    }

    private ValueTokenData CreateValueToken(string tokenId, string displayText, float numericValue)
    {
        ValueTokenData token = CreateToken<ValueTokenData>(tokenId, displayText);
        token.NumericValue = numericValue;
        return token;
    }

    private T CreateToken<T>(string tokenId, string displayText) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = tokenId;
        createdObjects.Add(token);
        return token;
    }

    private static string StripRichText(string text)
    {
        return Regex.Replace(text ?? string.Empty, "<.*?>", string.Empty);
    }

    private static string CreateCustomCatalogJson()
    {
        return @"
{
  ""coreLabels"": [
    { ""coreType"": ""Fire"", ""label"": ""焰"" }
  ],
  ""mainSentenceTemplates"": [
    ""{core}自文案表起咒，{behavior}，{result}。""
  ],
  ""behaviorPhrases"": [
    {
      ""behaviorType"": ""Straight"",
      ""phraseTemplates"": [
        ""<behavior>穿表而出</behavior>""
      ]
    }
  ],
  ""resultPhrases"": [
    {
      ""resultType"": ""DirectDamage"",
      ""phraseTemplates"": [
        ""留下<result>表中伤</result>""
      ]
    }
  ]
}";
    }
}
