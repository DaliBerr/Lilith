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

        PlaceableTokenData[] items = { fire, spread, valueThree, explosion };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            null,
            null,
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

        PlaceableTokenData[] items = { ice, homing, healing };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            null,
            null,
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
        PlaceableTokenData[] items = { fire };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            null,
            null,
            new VocalithRandom(2));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("火"));
        Assert.That(visibleText, Does.Match("直线|直击|直接伤害|射出"));
        Assert.That(visibleText.Length, Is.LessThanOrEqualTo(70));
    }

    [Test]
    public void GenerateRichText_WithoutCore_ReturnsShortPrompt()
    {
        CompiledSpellProgram program = CompileProgram();

        Assert.DoesNotThrow(() => SpellDescriptionGenerator.GenerateRichText(program, null, null, null, new VocalithRandom(3)));

        string richText = SpellDescriptionGenerator.GenerateRichText(program, null, null, null, new VocalithRandom(3));
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
        PlaceableTokenData[] items = { fire };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            catalog,
            null,
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("焰"));
        Assert.That(visibleText, Does.Contain("文案表起咒"));
        Assert.That(visibleText, Does.Contain("穿表而出"));
        Assert.That(visibleText, Does.Contain("表中伤"));
    }

    [Test]
    public void GenerateRichText_WithExplosionRadiusValue_DescribesValueConsumerSlot()
    {
        CoreTokenData fire = CreateCoreToken("fire_radius_core", "火", AttackCoreType.Fire);
        ResultTokenData explosion = CreateResultToken("explosion_radius", "爆", AttackResultType.Explosion, true);
        explosion.ValueParameterKind = SpellValueParameterKind.Radius;
        explosion.DefaultExplosionRadius = 1f;
        ValueTokenData valueThree = CreateValueToken("three_radius", "三", 3f);

        PlaceableTokenData[] items = { fire, explosion, valueThree };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            null,
            null,
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(GetPrimaryProjectile(program).ExplosionRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(visibleText, Does.Contain("数值"));
        Assert.That(visibleText, Does.Contain("三"));
        Assert.That(visibleText, Does.Contain("爆炸"));
        Assert.That(visibleText, Does.Contain("范围"));
        Assert.That(richText, Does.Contain("<color=#FFD166>"));
    }

    [Test]
    public void GenerateRichText_WithHealingRadiusValue_DescribesValueConsumerSlot()
    {
        CoreTokenData light = CreateCoreToken("light_healing_core", "光", AttackCoreType.Light);
        ResultTokenData healing = CreateResultToken("healing_radius", "愈", AttackResultType.Healing, true);
        healing.ValueParameterKind = SpellValueParameterKind.Radius;
        ValueTokenData valueThree = CreateValueToken("three_healing", "三", 3f);

        PlaceableTokenData[] items = { light, healing, valueThree };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            null,
            null,
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(GetPrimaryProjectile(program).ResultEffects.effectRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(visibleText, Does.Contain("数值"));
        Assert.That(visibleText, Does.Contain("三"));
        Assert.That(visibleText, Does.Contain("治疗"));
        Assert.That(visibleText, Does.Contain("范围"));
    }

    [Test]
    public void GenerateRichText_WithStatusDurationValue_DescribesDurationSlot()
    {
        CoreTokenData ice = CreateCoreToken("ice_duration_core", "冰", AttackCoreType.Ice);
        ResultTokenData control = CreateResultToken("control_duration", "定", AttackResultType.StatusEffect, true);
        control.DefaultTriggerCount = 2;
        control.EffectDuration = 1f;
        control.ValueParameterKind = SpellValueParameterKind.Duration;
        ValueTokenData durationValue = CreateValueToken("duration_value", "2.5", 2.5f);

        PlaceableTokenData[] items = { ice, control, durationValue };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            null,
            null,
            new VocalithRandom(1));
        string visibleText = StripRichText(richText);

        Assert.That(GetPrimaryProjectile(program).ResultEffects.controlDuration, Is.EqualTo(2.5f).Within(0.0001f));
        Assert.That(visibleText, Does.Contain("数值"));
        Assert.That(visibleText, Does.Contain("2.5"));
        Assert.That(visibleText, Does.Contain("控制"));
        Assert.That(visibleText, Does.Contain("持续"));
    }

    [Test]
    public void GenerateRichText_WithCatalogJsonOverrides_UsesExternalValueBindingPhrase()
    {
        Assert.That(
            SpellDescriptionCatalogData.TryDeserializeJson(CreateCustomValueCatalogJson(), out SpellDescriptionCatalogData catalog, out string errorMessage),
            Is.True,
            errorMessage);
        CoreTokenData fire = CreateCoreToken("catalog_value_core", "火", AttackCoreType.Fire);
        ResultTokenData explosion = CreateResultToken("catalog_value_explosion", "爆", AttackResultType.Explosion, true);
        explosion.ValueParameterKind = SpellValueParameterKind.Radius;
        ValueTokenData valueThree = CreateValueToken("catalog_value_three", "三", 3f);
        PlaceableTokenData[] items = { fire, explosion, valueThree };
        CompiledSpellProgram program = CompileProgram(items);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            items,
            catalog,
            null,
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("自定义数值"));
        Assert.That(visibleText, Does.Contain("写入爆炸半径"));
    }

    [Test]
    public void GenerateRichText_WithMulticastProgram_DescribesCastBlock()
    {
        MulticastTokenData dualCast = CreateMulticastToken("dual_cast", "双", 2);
        CoreTokenData fire = CreateCoreToken("multicast_fire", "火", AttackCoreType.Fire);
        CoreTokenData ice = CreateCoreToken("multicast_ice", "冰", AttackCoreType.Ice);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            dualCast,
            fire,
            ice,
        });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            new PlaceableTokenData[] { dualCast, fire, ice },
            null,
            null,
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(program.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(2));
        Assert.That(visibleText, Does.Contain("CastBlock"));
        Assert.That(visibleText, Does.Contain("二枚外层法术"));
        Assert.That(richText, Does.Contain("<color=#B98CFF>CastBlock</color>"));
    }

    [Test]
    public void GenerateRichText_WithSpellProgram_UsesProjectileNodeSnapshot()
    {
        CoreTokenData fire = CreateCoreToken("snapshot_fire", "火", AttackCoreType.Fire);
        BehaviorTokenData spread = CreateBehaviorToken("snapshot_spread", "散", AttackBehaviorType.Spread, true, 1);
        ValueTokenData valueThree = CreateValueToken("snapshot_three", "三", 3f);
        ResultTokenData explosion = CreateResultToken("snapshot_explosion", "爆", AttackResultType.Explosion, false);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fire,
            spread,
            valueThree,
            explosion,
        });
        SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            new PlaceableTokenData[] { fire, spread, valueThree, explosion },
            null,
            null,
            new VocalithRandom(0));
        string visibleText = StripRichText(richText);

        Assert.That(projectile.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(projectile.ProjectileCount, Is.EqualTo(3));
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(visibleText, Does.Contain("火"));
        Assert.That(visibleText, Does.Contain("三道"));
        Assert.That(visibleText, Does.Contain("散射"));
        Assert.That(visibleText, Does.Contain("爆炸"));
    }

    [Test]
    public void GenerateRichText_WithModifierProgram_DescribesModifierScope()
    {
        ModifierTokenData amplify = CreateModifierToken("block_amplify", "放大", SpellModifierScope.CurrentBlock);
        MulticastTokenData dualCast = CreateMulticastToken("modifier_dual_cast", "双", 2);
        CoreTokenData fire = CreateCoreToken("modifier_fire", "火", AttackCoreType.Fire);
        CoreTokenData ice = CreateCoreToken("modifier_ice", "冰", AttackCoreType.Ice);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            amplify,
            dualCast,
            fire,
            ice,
        });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            new PlaceableTokenData[] { amplify, dualCast, fire, ice },
            null,
            null,
            new VocalithRandom(1));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("Modifier"));
        Assert.That(visibleText, Does.Contain("放大"));
        Assert.That(visibleText, Does.Contain("当前CastBlock"));
    }

    [Test]
    public void GenerateRichText_WithTriggerPayloadProgram_DescribesTriggerPayload()
    {
        CoreTokenData fire = CreateCoreToken("trigger_fire", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触");
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData explosion = CreateResultToken("payload_explosion", "爆", AttackResultType.Explosion, true);
        explosion.ValueParameterKind = SpellValueParameterKind.Radius;
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            explosion,
            payloadEnd,
        });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            new PlaceableTokenData[] { fire, trigger, payloadStart, explosion, payloadEnd },
            null,
            null,
            new VocalithRandom(2));
        string visibleText = StripRichText(richText);

        Assert.That(program.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(visibleText, Does.Contain("Trigger/Payload"));
        Assert.That(visibleText, Does.Contain("命中后"));
        Assert.That(visibleText, Does.Contain("爆炸"));
    }

    [Test]
    public void GenerateRichText_WithSpellBook_DescribesExecutorTraits()
    {
        CoreTokenData fixedFire = CreateCoreToken("book_fixed_fire", "火", AttackCoreType.Fire);
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        createdObjects.Add(spellBook);
        spellBook.name = "quick_book";
        spellBook.DisplayName = "疾书";
        spellBook.SlotCount = 4;
        spellBook.CastCooldownSeconds = 0.1f;
        spellBook.CastsPerActivation = 2;
        spellBook.ActivationSpreadAngleStep = 9f;
        spellBook.EnergyCapacity = 3f;
        spellBook.EnergyCostPerActivation = 1f;
        spellBook.EnergyRegenPerSecond = 0.75f;
        spellBook.SetExecutorModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.Damage, "*=0.85"),
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "*=1.1"),
        });
        spellBook.FixedItemPlacement = SpellBookFixedItemPlacement.BeforeEquipped;
        spellBook.SetFixedCastItems(new PlaceableTokenData[] { fixedFire });
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[] { fixedFire });

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            new PlaceableTokenData[] { fixedFire },
            null,
            spellBook,
            new VocalithRandom(3));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("疾书"));
        Assert.That(visibleText, Does.Contain("4槽"));
        Assert.That(visibleText, Does.Contain("冷却0.1秒"));
        Assert.That(visibleText, Does.Contain("每次激活二轮"));
        Assert.That(visibleText, Does.Contain("激活扇形9度"));
        Assert.That(visibleText, Does.Contain("能量3"));
        Assert.That(visibleText, Does.Contain("消耗1"));
        Assert.That(visibleText, Does.Contain("回复0.75/秒"));
        Assert.That(visibleText, Does.Contain("内建强化二项"));
        Assert.That(visibleText, Does.Contain("内建伤害x0.85"));
        Assert.That(visibleText, Does.Contain("速度x1.1"));
        Assert.That(visibleText, Does.Contain("前置常驻一词元"));
    }

    [Test]
    public void GenerateRichText_WithSpellBookResultExecutorTraits_DescribesResultTargets()
    {
        CoreTokenData fire = CreateCoreToken("book_result_fire", "火", AttackCoreType.Fire);
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        createdObjects.Add(spellBook);
        spellBook.name = "binding_book";
        spellBook.DisplayName = "缚书";
        spellBook.SlotCount = 5;
        spellBook.CastCooldownSeconds = 0.32f;
        spellBook.SetExecutorModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ResultCount, "=1"),
            new TokenModifierDefinition(TokenModifierTarget.ResultDuration, "*=1.5"),
        });
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[] { fire }, spellBook);

        string richText = SpellDescriptionGenerator.GenerateRichText(
            program,
            new PlaceableTokenData[] { fire },
            null,
            spellBook,
            new VocalithRandom(4));
        string visibleText = StripRichText(richText);

        Assert.That(visibleText, Does.Contain("缚书"));
        Assert.That(visibleText, Does.Contain("5槽"));
        Assert.That(visibleText, Does.Contain("冷却0.32秒"));
        Assert.That(visibleText, Does.Contain("内建结果数量=1"));
        Assert.That(visibleText, Does.Contain("结果时长x1.5"));
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

    private ModifierTokenData CreateModifierToken(string tokenId, string displayText, SpellModifierScope scope)
    {
        ModifierTokenData token = CreateToken<ModifierTokenData>(tokenId, displayText);
        return token;
    }

    private MulticastTokenData CreateMulticastToken(string tokenId, string displayText, int castCount)
    {
        MulticastTokenData token = CreateToken<MulticastTokenData>(tokenId, displayText);
        token.CastCount = castCount;
        return token;
    }

    private TriggerTokenData CreateTriggerToken(string tokenId, string displayText)
    {
        TriggerTokenData token = CreateToken<TriggerTokenData>(tokenId, displayText);
        token.TriggerType = SpellTriggerType.OnHit;
        return token;
    }

    private PayloadBoundaryTokenData CreatePayloadBoundaryToken(string tokenId, string displayText, PayloadBoundaryKind boundaryKind)
    {
        PayloadBoundaryTokenData token = CreateToken<PayloadBoundaryTokenData>(tokenId, displayText);
        token.BoundaryKind = boundaryKind;
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

    private static CompiledSpellProgram CompileProgram(params PlaceableTokenData[] items)
    {
        return SpellProgramCompiler.Compile(items);
    }

    private static SpellProjectileNode GetPrimaryProjectile(CompiledSpellProgram program)
    {
        Assert.That(program, Is.Not.Null);
        Assert.That(program.TryGetPrimaryProjectile(out SpellProjectileNode projectile), Is.True);
        return projectile;
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

    private static string CreateCustomValueCatalogJson()
    {
        return @"
{
  ""valueBindings"": [
    { ""tokenType"": ""Explosion"", ""parameterKind"": 2, ""phrase"": ""<value>{value}</value>写入<result>{consumer}</result>半径"" }
  ],
  ""valueBindingSentenceTemplates"": [
    ""自定义数值：{bindings}。""
  ]
}";
    }
}
