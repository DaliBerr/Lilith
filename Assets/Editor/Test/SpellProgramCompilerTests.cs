using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SpellProgramCompilerTests
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

        CharBullet[] strayBullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < strayBullets.Length; i++)
        {
            if (strayBullets[i] != null)
            {
                Object.DestroyImmediate(strayBullets[i].gameObject);
            }
        }
    }

    [Test]
    public void Compile_CoreOnly_BuildsSingleOuterCastBlockProjectile()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken });

        Assert.That(program, Is.Not.Null);
        Assert.That(program.CanCast, Is.True);
        Assert.That(program.CastBlocks.Count, Is.EqualTo(1));
        SpellCastBlock block = program.PrimaryCastBlock;
        Assert.That(block, Is.Not.Null);
        Assert.That(block.BlockId, Is.EqualTo("outer"));
        Assert.That(block.Depth, Is.EqualTo(0));
        Assert.That(block.Modifiers, Is.Empty);
        Assert.That(block.Payloads, Is.Empty);
        Assert.That(block.Projectiles.Count, Is.EqualTo(1));

        SpellProjectileNode projectile = block.Projectiles[0];
        Assert.That(projectile.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(projectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Straight));
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(projectile.AttackSpec.damage, Is.EqualTo(coreToken.Damage).Within(0.0001f));
        Assert.That(projectile.ProjectileCount, Is.EqualTo(1));
        Assert.That(projectile.Payloads, Is.Empty);
        Assert.That(program.TryGetPrimaryProjectile(out SpellProjectileNode primaryProjectile), Is.True);
        Assert.That(primaryProjectile, Is.SameAs(projectile));
    }

    [Test]
    public void Compile_WithRuntimeSemantics_CopiesProjectileNodeSnapshot()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        fire.BurnTriggerCount = 2;
        fire.BurnDamagePerSecond = 3f;
        fire.BurnDuration = 4f;
        fire.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=2"),
            new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=3"),
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
            new TokenModifierDefinition(TokenModifierTarget.FontSize, "+=4"),
        });
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, true, 1f);
        explosion.ValueParameterKind = SpellValueParameterKind.Radius;
        explosion.ExplosionDamageMultiplier = 0.75f;
        explosion.EffectDuration = 0.4f;
        ValueTokenData radius = CreateValueToken("radius", "3", 3f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            explosion,
            radius,
        });

        SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];

        Assert.That(projectile.CoreEffects.HasBurn, Is.True);
        Assert.That(projectile.ResultEffects.explosionRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(projectile.ResultEffects.explosionDamageMultiplier, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(projectile.ResultEffects.explosionDelaySeconds, Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(projectile.HasExplosion, Is.True);
        Assert.That(projectile.ExplosionRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(projectile.ScaleMultiplier, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(projectile.ImpactRadiusMultiplier, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(projectile.HasTextColorOverride, Is.True);
        Assert.That(projectile.TextColor, Is.EqualTo(Color.red));
        Assert.That(projectile.HasFontSizeOverride, Is.True);
        Assert.That(projectile.ResolveFontSize(16f), Is.EqualTo(20f).Within(0.0001f));

        Assert.That(projectile.ScaleMultiplier, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(projectile.ResultEffects.HasExplosion, Is.True);
        Assert.That(projectile.HasExplosion, Is.True);
    }

    [Test]
    public void Compile_CurrentBlockModifiers_RecordsModifierNodes()
    {
        ModifierTokenData leadingModifier = CreateModifierToken("leading", "前", SpellModifierScope.CurrentBlock);
        CoreTokenData coreToken = CreateCoreToken("edge_core", "锋", AttackCoreType.Edge);
        ResultTokenData resultToken = CreateResultToken("direct", "击", AttackResultType.DirectDamage, false, 0f);
        ModifierTokenData trailingModifier = CreateModifierToken("trailing", "后", SpellModifierScope.CurrentBlock);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            leadingModifier,
            coreToken,
            resultToken,
            trailingModifier,
        });

        Assert.That(program.CanCast, Is.True);
        Assert.That(program.PrimaryCastBlock, Is.Not.Null);
        Assert.That(program.PrimaryCastBlock.Modifiers.Count, Is.EqualTo(1));
        Assert.That(program.PrimaryCastBlock.Modifiers[0].SourceToken, Is.SameAs(leadingModifier));
        Assert.That(program.PrimaryCastBlock.Modifiers[0].Scope, Is.EqualTo(SpellModifierScope.NextToken));
        Assert.That(program.PrimaryCastBlock.Modifiers[0].Origin, Is.EqualTo(SpellModifierOrigin.ModifierToken));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
    }

    [Test]
    public void Compile_WithoutCore_PreservesErrorsWithoutProjectileBlock()
    {
        ValueTokenData valueToken = CreateValueToken("three", "三", 3f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { valueToken });

        Assert.That(program.CanCast, Is.False);
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Error), Is.EqualTo(1));
        Assert.That(program.CastBlocks, Is.Empty);
        Assert.That(program.TryGetPrimaryProjectile(out _), Is.False);
    }

    [Test]
    public void Compile_NextTokenModifier_AppliesAndRecordsModifierScope()
    {
        ModifierTokenData haste = CreateModifierToken("haste", "疾", SpellModifierScope.NextToken);
        haste.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "+=80"),
        });
        CoreTokenData coreToken = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            haste,
            coreToken,
        });

        Assert.That(program.CanCast, Is.True);
        Assert.That(program.PrimaryCastBlock.Projectiles[0].AttackSpec.projectileSpeed, Is.EqualTo(400f).Within(0.0001f));
        Assert.That(program.PrimaryCastBlock.Modifiers.Count, Is.EqualTo(1));
        SpellModifierNode modifierNode = program.PrimaryCastBlock.Modifiers[0];
        Assert.That(modifierNode.SourceToken, Is.SameAs(haste));
        Assert.That(modifierNode.Scope, Is.EqualTo(SpellModifierScope.NextToken));
        Assert.That(modifierNode.Origin, Is.EqualTo(SpellModifierOrigin.ModifierToken));
        Assert.That(modifierNode.TargetCount, Is.EqualTo(1));
    }

    [Test]
    public void Compile_CurrentBlockModifier_AppliesWithoutWaitingForNextToken()
    {
        CoreTokenData coreToken = CreateCoreToken("edge_core", "锋", AttackCoreType.Edge);
        ModifierTokenData amplify = CreateModifierToken("amplify", "放", SpellModifierScope.CurrentBlock);
        amplify.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=2"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            amplify,
        });

        Assert.That(program.CanCast, Is.True);
        Assert.That(program.PrimaryCastBlock.Projectiles[0].ScaleMultiplier, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(program.PrimaryCastBlock.Modifiers, Is.Empty);
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
    }

    [Test]
    public void Compile_UnboundNextTokenModifier_WarnsWithoutRecordingModifierNode()
    {
        ModifierTokenData haste = CreateModifierToken("haste", "疾", SpellModifierScope.NextToken);
        haste.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "+=80"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { haste });

        Assert.That(program.CanCast, Is.False);
        Assert.That(program.CastBlocks, Is.Empty);
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Error), Is.EqualTo(1));
    }

    [Test]
    public void Compile_MulticastTwoCores_BuildsSingleCastBlockWithTwoProjectileNodes()
    {
        MulticastTokenData doubleCast = CreateMulticastToken("double", "双", 2);
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        CoreTokenData ice = CreateCoreToken("ice_core", "冰", AttackCoreType.Ice);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            doubleCast,
            fire,
            ice,
        });

        Assert.That(program.CanCast, Is.True);
        Assert.That(program.CastBlocks.Count, Is.EqualTo(1));
        Assert.That(program.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(2));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(program.PrimaryCastBlock.Projectiles[1].CoreType, Is.EqualTo(AttackCoreType.Ice));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].ProjectileCount, Is.EqualTo(1));
        Assert.That(program.PrimaryCastBlock.Projectiles[1].ProjectileCount, Is.EqualTo(1));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].AttackSpec.coreType, Is.EqualTo(AttackCoreType.Fire));
    }

    [Test]
    public void Compile_MulticastInsufficientRightSide_WarnsAndKeepsValidProjectile()
    {
        MulticastTokenData doubleCast = CreateMulticastToken("double", "双", 2);
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, true, 2f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            doubleCast,
            fire,
            explosion,
        });

        Assert.That(program.CanCast, Is.True);
        Assert.That(program.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(1));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
        Assert.That(program.Messages[0].message, Does.Contain("Multicast requested 2 projectile nodes"));
    }

    [Test]
    public void Compile_MulticastCurrentBlockModifier_AppliesToEveryProjectileNode()
    {
        ModifierTokenData amplify = CreateModifierToken("amplify", "放", SpellModifierScope.CurrentBlock);
        amplify.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=2"),
        });
        MulticastTokenData doubleCast = CreateMulticastToken("double", "双", 2);
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        CoreTokenData ice = CreateCoreToken("ice_core", "冰", AttackCoreType.Ice);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            amplify,
            doubleCast,
            fire,
            ice,
        });

        Assert.That(program.CanCast, Is.True);
        Assert.That(program.PrimaryCastBlock.Modifiers.Count, Is.EqualTo(1));
        Assert.That(program.PrimaryCastBlock.Modifiers[0].SourceToken, Is.SameAs(amplify));
        Assert.That(program.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(2));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].ScaleMultiplier, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(program.PrimaryCastBlock.Projectiles[1].ScaleMultiplier, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Compile_NestedMulticast_IgnoresNestedTokenWithoutWrapping()
    {
        MulticastTokenData outerCast = CreateMulticastToken("double_outer", "双", 2);
        MulticastTokenData nestedCast = CreateMulticastToken("double_inner", "双", 2);
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        CoreTokenData ice = CreateCoreToken("ice_core", "冰", AttackCoreType.Ice);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            outerCast,
            fire,
            nestedCast,
            ice,
        });

        Assert.That(program.CanCast, Is.True);
        Assert.That(program.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(2));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(program.PrimaryCastBlock.Projectiles[1].CoreType, Is.EqualTo(AttackCoreType.Ice));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
        Assert.That(program.Messages[0].message, Does.Contain("nested multicast"));
    }

    [Test]
    public void Compile_TriggerOnHitExplicitPayload_BuildsPayloadBlockWithResultEffect()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, false, 2f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            explosion,
            payloadEnd,
        });

        Assert.That(program.CanCast, Is.True);
        SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];
        Assert.That(projectile.Payloads.Count, Is.EqualTo(1));
        SpellPayloadBlock payload = projectile.Payloads[0];
        Assert.That(payload.TriggerType, Is.EqualTo(SpellTriggerType.OnHit));
        Assert.That(payload.InnerBlock.Depth, Is.EqualTo(1));
        Assert.That(payload.InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        Assert.That(payload.InnerBlock.PayloadEffects[0].ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(payload.InnerBlock.PayloadEffects[0].ResultEffects.explosionRadius, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Compile_TriggerPayloadStatusValue_ConsumesValueAsControlCount()
    {
        CoreTokenData ice = CreateCoreToken("ice_core", "冰", AttackCoreType.Ice);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData control = CreateResultToken("control", "定", AttackResultType.StatusEffect, true, 0f);
        control.DefaultTriggerCount = 1;
        control.EffectDuration = 1.5f;
        ValueTokenData valueThree = CreateValueToken("three", "三", 3f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            ice,
            trigger,
            payloadStart,
            control,
            valueThree,
            payloadEnd,
        });

        SpellPayloadEffectNode effect = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock.PayloadEffects[0];
        Assert.That(effect.ResultType, Is.EqualTo(AttackResultType.StatusEffect));
        Assert.That(effect.ResultEffects.controlTriggerCount, Is.EqualTo(3));
        Assert.That(effect.ResultEffects.controlDuration, Is.EqualTo(1.5f).Within(0.0001f));
    }

    [Test]
    public void Compile_TriggerPayloadExplosionValue_ConsumesDeclaredRadiusSlot()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, true, 1f);
        explosion.ValueParameterKind = SpellValueParameterKind.Radius;
        ValueTokenData radiusValue = CreateValueToken("three", "三", 3f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            explosion,
            radiusValue,
            payloadEnd,
        });

        SpellPayloadEffectNode effect = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock.PayloadEffects[0];
        Assert.That(effect.ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(effect.ResultEffects.explosionRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Compile_TriggerPayloadHealingValue_ConsumesDeclaredRadiusSlot()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, true, 0f);
        healing.ValueParameterKind = SpellValueParameterKind.Radius;
        ValueTokenData radiusValue = CreateValueToken("three", "三", 3f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            healing,
            radiusValue,
            payloadEnd,
        });

        SpellPayloadEffectNode effect = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock.PayloadEffects[0];
        Assert.That(effect.ResultType, Is.EqualTo(AttackResultType.Healing));
        Assert.That(effect.ResultEffects.effectRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(effect.ResultEffects.healingMultiplier, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Compile_TriggerPayloadSplitValue_ConsumesValueAsSplitCount()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData split = CreateResultToken("split", "裂", AttackResultType.Split, true, 0f);
        split.DefaultTriggerCount = 1;
        split.ChildDamageMultiplier = 0.5f;
        ValueTokenData valueThree = CreateValueToken("three", "三", 3f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            split,
            valueThree,
            payloadEnd,
        });

        SpellPayloadEffectNode effect = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock.PayloadEffects[0];
        Assert.That(effect.ResultType, Is.EqualTo(AttackResultType.Split));
        Assert.That(effect.ResultEffects.splitProjectileCount, Is.EqualTo(3));
        Assert.That(effect.ResultEffects.splitDamageMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Compile_TriggerPayloadProjectileCurrentPayloadModifier_AppliesOnlyInsidePayload()
    {
        CoreTokenData outerFire = CreateCoreToken("outer_fire", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ModifierTokenData payloadHaste = CreateModifierToken("payload_haste", "疾", SpellModifierScope.CurrentPayload);
        payloadHaste.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "+=80"),
        });
        CoreTokenData payloadIce = CreateCoreToken("payload_ice", "冰", AttackCoreType.Ice);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            outerFire,
            trigger,
            payloadStart,
            payloadHaste,
            payloadIce,
            payloadEnd,
        });

        Assert.That(program.PrimaryCastBlock.Projectiles[0].AttackSpec.projectileSpeed, Is.EqualTo(320f).Within(0.0001f));
        SpellCastBlock innerBlock = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock;
        Assert.That(innerBlock.Modifiers.Count, Is.EqualTo(1));
        Assert.That(innerBlock.Modifiers[0].SourceToken, Is.SameAs(payloadHaste));
        Assert.That(innerBlock.Modifiers[0].Scope, Is.EqualTo(SpellModifierScope.NextToken));
        Assert.That(innerBlock.Projectiles.Count, Is.EqualTo(1));
        Assert.That(innerBlock.Projectiles[0].AttackSpec.projectileSpeed, Is.EqualTo(400f).Within(0.0001f));
    }

    [Test]
    public void Compile_GlobalProgramModifierBeforeTrigger_AppliesToOuterAndPayloadProjectile()
    {
        ModifierTokenData globalExtend = CreateModifierToken("global_extend", "长", SpellModifierScope.GlobalProgram);
        globalExtend.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.MaxLifetime, "*=2"),
        });
        CoreTokenData outerFire = CreateCoreToken("outer_fire", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        CoreTokenData payloadIce = CreateCoreToken("payload_ice", "冰", AttackCoreType.Ice);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            globalExtend,
            outerFire,
            trigger,
            payloadStart,
            payloadIce,
            payloadEnd,
        });

        Assert.That(program.PrimaryCastBlock.Projectiles[0].AttackSpec.maxLifetime, Is.EqualTo(4f).Within(0.0001f));
        SpellCastBlock innerBlock = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock;
        Assert.That(innerBlock.Modifiers, Is.Empty);
        Assert.That(innerBlock.Projectiles[0].AttackSpec.maxLifetime, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Compile_ResultOnlyPayloadCurrentPayloadRadiusModifier_ModifiesEffectRadius()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ModifierTokenData payloadRadius = CreateModifierToken("payload_radius", "域", SpellModifierScope.CurrentPayload);
        payloadRadius.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=3"),
        });
        ValueTokenData payloadCount = CreateValueToken("payload_count", "三", 3f);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, false, 2f);
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, false, 0f);
        healing.DefaultEffectRadius = 1f;
        ResultTokenData control = CreateResultToken("control", "定", AttackResultType.StatusEffect, false, 0f);
        control.DefaultEffectRadius = 1f;
        control.DefaultTriggerCount = 2;
        control.EffectDuration = 1f;
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            payloadRadius,
            payloadCount,
            explosion,
            healing,
            control,
            payloadEnd,
        });

        SpellCastBlock innerBlock = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock;
        Assert.That(innerBlock.Modifiers.Count, Is.EqualTo(1));
        Assert.That(innerBlock.Modifiers[0].Scope, Is.EqualTo(SpellModifierScope.NextN));
        Assert.That(innerBlock.Modifiers[0].TargetCount, Is.EqualTo(3));
        Assert.That(innerBlock.PayloadEffects.Count, Is.EqualTo(3));
        SpellPayloadEffectNode explosionEffect = innerBlock.PayloadEffects[0];
        SpellPayloadEffectNode healingEffect = innerBlock.PayloadEffects[1];
        SpellPayloadEffectNode controlEffect = innerBlock.PayloadEffects[2];
        Assert.That(explosionEffect.ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(explosionEffect.ResultEffects.explosionRadius, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(healingEffect.ResultType, Is.EqualTo(AttackResultType.Healing));
        Assert.That(healingEffect.ResultEffects.effectRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(controlEffect.ResultType, Is.EqualTo(AttackResultType.StatusEffect));
        Assert.That(controlEffect.ResultEffects.effectRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].HasExplosion, Is.False);
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Compile_ResultOnlyPayloadCurrentPayloadResultModifiers_ModifiesEffectSemantics()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ModifierTokenData payloadModifier = CreateModifierToken("payload_result_mod", "变", SpellModifierScope.CurrentPayload);
        payloadModifier.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ResultCount, "+=2"),
            new TokenModifierDefinition(TokenModifierTarget.ResultDuration, "*=3"),
            new TokenModifierDefinition(TokenModifierTarget.ResultMultiplier, "*=2"),
        });
        ValueTokenData payloadCount = CreateValueToken("payload_count", "三", 3f);
        ResultTokenData split = CreateResultToken("split", "裂", AttackResultType.Split, false, 0f);
        split.DefaultTriggerCount = 1;
        split.ChildDamageMultiplier = 0.25f;
        ResultTokenData control = CreateResultToken("control", "定", AttackResultType.StatusEffect, false, 0f);
        control.DefaultTriggerCount = 1;
        control.EffectDuration = 2f;
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, false, 0f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            payloadModifier,
            payloadCount,
            split,
            control,
            healing,
            payloadEnd,
        });

        SpellCastBlock innerBlock = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock;
        Assert.That(innerBlock.PayloadEffects.Count, Is.EqualTo(3));
        SpellPayloadEffectNode splitEffect = innerBlock.PayloadEffects[0];
        SpellPayloadEffectNode controlEffect = innerBlock.PayloadEffects[1];
        SpellPayloadEffectNode healingEffect = innerBlock.PayloadEffects[2];
        Assert.That(splitEffect.ResultType, Is.EqualTo(AttackResultType.Split));
        Assert.That(splitEffect.ResultEffects.splitProjectileCount, Is.EqualTo(3));
        Assert.That(splitEffect.ResultEffects.splitDamageMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(controlEffect.ResultType, Is.EqualTo(AttackResultType.StatusEffect));
        Assert.That(controlEffect.ResultEffects.controlTriggerCount, Is.EqualTo(3));
        Assert.That(controlEffect.ResultEffects.controlDuration, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(healingEffect.ResultType, Is.EqualTo(AttackResultType.Healing));
        Assert.That(healingEffect.ResultEffects.healingMultiplier, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Compile_SpellBookExecutorModifiers_ModifyResultOnlyPayloadEffects()
    {
        SpellBookData spellBook = CreateSpellBook("catalyst", "Catalyst", 6, 0.25f);
        spellBook.SetExecutorModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.Damage, "*=0.5"),
            new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=2"),
            new TokenModifierDefinition(TokenModifierTarget.ResultCount, "+=2"),
            new TokenModifierDefinition(TokenModifierTarget.ResultDuration, "*=3"),
            new TokenModifierDefinition(TokenModifierTarget.ResultMultiplier, "*=2"),
        });
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        fire.Damage = 10f;
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, false, 2f);
        explosion.ExplosionDamageMultiplier = 0.5f;
        explosion.EffectDuration = 1f;
        ResultTokenData split = CreateResultToken("split", "裂", AttackResultType.Split, false, 0f);
        split.DefaultTriggerCount = 1;
        split.ChildDamageMultiplier = 0.25f;
        ResultTokenData control = CreateResultToken("control", "定", AttackResultType.StatusEffect, false, 0f);
        control.DefaultTriggerCount = 1;
        control.EffectDuration = 2f;
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, false, 0f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            explosion,
            split,
            control,
            healing,
            payloadEnd,
        }, spellBook);

        SpellProjectileNode outerProjectile = program.PrimaryCastBlock.Projectiles[0];
        SpellCastBlock innerBlock = outerProjectile.Payloads[0].InnerBlock;
        Assert.That(outerProjectile.AttackSpec.damage, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(innerBlock.PayloadEffects.Count, Is.EqualTo(4));

        SpellPayloadEffectNode explosionEffect = innerBlock.PayloadEffects[0];
        SpellPayloadEffectNode splitEffect = innerBlock.PayloadEffects[1];
        SpellPayloadEffectNode controlEffect = innerBlock.PayloadEffects[2];
        SpellPayloadEffectNode healingEffect = innerBlock.PayloadEffects[3];

        Assert.That(explosionEffect.ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(explosionEffect.ResultEffects.explosionRadius, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(explosionEffect.ResultEffects.explosionDamageMultiplier, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(explosionEffect.ResultEffects.explosionDelaySeconds, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(splitEffect.ResultType, Is.EqualTo(AttackResultType.Split));
        Assert.That(splitEffect.ResultEffects.splitProjectileCount, Is.EqualTo(3));
        Assert.That(splitEffect.ResultEffects.splitDamageMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(controlEffect.ResultType, Is.EqualTo(AttackResultType.StatusEffect));
        Assert.That(controlEffect.ResultEffects.controlTriggerCount, Is.EqualTo(3));
        Assert.That(controlEffect.ResultEffects.controlDuration, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(healingEffect.ResultType, Is.EqualTo(AttackResultType.Healing));
        Assert.That(healingEffect.ResultEffects.healingMultiplier, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Emit_WithSpellProgram_UsesProjectileNodeShapeWithoutRuntimeAdapter()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2, 12f);
        ValueTokenData valueThree = CreateValueToken("three", "三", 3f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            valueThree,
        });
        List<CharBullet> spawnedBullets = new();

        int emittedCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);

        Assert.That(emittedCount, Is.EqualTo(3));
        Assert.That(spawnedBullets.Count, Is.EqualTo(3));
        for (int i = 0; i < spawnedBullets.Count; i++)
        {
            Assert.That(spawnedBullets[i].CurrentProjectileNode, Is.SameAs(program.PrimaryCastBlock.Projectiles[0]));
            Assert.That(spawnedBullets[i].CurrentAttackSpec.behaviorType, Is.EqualTo(AttackBehaviorType.Spread));
        }
    }

    [Test]
    public void Emit_WithSpellProgram_UsesProjectileNodePresentationSnapshot()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        fire.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=2"),
            new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=3"),
        });
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { fire });
        SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];
        List<CharBullet> spawnedBullets = new();

        int emittedCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(spawnedBullets.Count, Is.EqualTo(1));
        Assert.That(spawnedBullets[0].CurrentProjectileNode, Is.SameAs(projectile));
        Assert.That(spawnedBullets[0].ScaleMultiplier, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(spawnedBullets[0].ImpactRadiusMultiplier, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(spawnedBullets[0].ImpactCollider.radius, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void Emit_WithMulticastProgram_EmitsEveryProjectileNode()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        MulticastTokenData doubleCast = CreateMulticastToken("double", "双", 2);
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        CoreTokenData ice = CreateCoreToken("ice_core", "冰", AttackCoreType.Ice);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            doubleCast,
            fire,
            ice,
        });
        List<CharBullet> spawnedBullets = new();

        int emittedCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);

        Assert.That(emittedCount, Is.EqualTo(2));
        Assert.That(spawnedBullets.Count, Is.EqualTo(2));
        Assert.That(spawnedBullets[0].CurrentProjectileNode, Is.SameAs(program.PrimaryCastBlock.Projectiles[0]));
        Assert.That(spawnedBullets[1].CurrentProjectileNode, Is.SameAs(program.PrimaryCastBlock.Projectiles[1]));
        Assert.That(spawnedBullets[0].CurrentProjectileNode.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(spawnedBullets[1].CurrentProjectileNode.CoreType, Is.EqualTo(AttackCoreType.Ice));
    }

    [Test]
    public void Emit_WithTriggerPayload_AttachesPayloadBlockToSpawnedBullet()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        CompiledSpellProgram program = CreateTriggeredExplosionProgram(2f, 1f);
        List<CharBullet> spawnedBullets = new();

        int emittedCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(spawnedBullets.Count, Is.EqualTo(1));
        Assert.That(spawnedBullets[0].CurrentProjectileNode, Is.SameAs(program.PrimaryCastBlock.Projectiles[0]));
        Assert.That(spawnedBullets[0].CurrentPayloads.Count, Is.EqualTo(1));
    }

    [Test]
    public void Impact_WithTriggerExplosionPayload_DamagesNearbyTargetOnHit()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 5f);
        TestEnemy secondaryEnemy = CreateEnemy("SecondaryEnemy", new Vector3(0.6f, 0f, 2f), 5f);
        CompiledSpellProgram program = CreateTriggeredExplosionProgram(1f, 1f);
        List<CharBullet> spawnedBullets = new();
        AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);
        Physics.SyncTransforms();

        bool handled = InvokeTryRegisterImpact(spawnedBullets[0], primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(4f).Within(0.0001f));
    }

    [Test]
    public void Impact_WithTriggerHealingPayload_RestoresPrimaryTargetAfterDirectHit()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 10f);
        primaryEnemy.SetHealth(10f, 6f);
        CompiledSpellProgram program = CreateTriggeredHealingProgram();
        List<CharBullet> spawnedBullets = new();
        AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);
        Physics.SyncTransforms();

        bool handled = InvokeTryRegisterImpact(spawnedBullets[0], primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(6f).Within(0.0001f));
    }

    [Test]
    public void Impact_WithTriggerHealingPayloadResultMultiplier_RestoresScaledHealing()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 10f);
        primaryEnemy.SetHealth(10f, 6f);
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        fire.Damage = 2f;
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ModifierTokenData payloadHealingAmplify = CreateModifierToken("payload_healing_amplify", "愈+", SpellModifierScope.CurrentPayload);
        payloadHealingAmplify.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ResultMultiplier, "*=2"),
        });
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, false, 0f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            payloadHealingAmplify,
            healing,
            payloadEnd,
        });
        List<CharBullet> spawnedBullets = new();
        AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);
        Physics.SyncTransforms();

        bool handled = InvokeTryRegisterImpact(spawnedBullets[0], primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(8f).Within(0.0001f));
    }

    [Test]
    public void Impact_WithTriggerHealingPayloadRadius_RestoresNearbyTargets()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 10f);
        TestEnemy secondaryEnemy = CreateEnemy("SecondaryEnemy", new Vector3(0.6f, 0f, 2f), 10f);
        TestEnemy farEnemy = CreateEnemy("FarEnemy", new Vector3(3f, 0f, 2f), 10f);
        primaryEnemy.SetHealth(10f, 6f);
        secondaryEnemy.SetHealth(10f, 6f);
        farEnemy.SetHealth(10f, 6f);
        CompiledSpellProgram program = CreateTriggeredHealingRadiusProgram(1f);
        List<CharBullet> spawnedBullets = new();
        AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);
        Physics.SyncTransforms();

        bool handled = InvokeTryRegisterImpact(spawnedBullets[0], primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(6f).Within(0.0001f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(8f).Within(0.0001f));
        Assert.That(farEnemy.CurrentHealth, Is.EqualTo(6f).Within(0.0001f));
    }

    [Test]
    public void Impact_WithTriggerControlPayloadRadius_ControlsNearbyTargets()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 10f);
        TestEnemy secondaryEnemy = CreateEnemy("SecondaryEnemy", new Vector3(0.6f, 0f, 2f), 10f);
        TestEnemy farEnemy = CreateEnemy("FarEnemy", new Vector3(3f, 0f, 2f), 10f);
        EnemyStatusEffectController primaryStatus = primaryEnemy.gameObject.AddComponent<EnemyStatusEffectController>();
        EnemyStatusEffectController secondaryStatus = secondaryEnemy.gameObject.AddComponent<EnemyStatusEffectController>();
        EnemyStatusEffectController farStatus = farEnemy.gameObject.AddComponent<EnemyStatusEffectController>();
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ModifierTokenData payloadControlField = CreateModifierToken("payload_control_field", "缚", SpellModifierScope.CurrentPayload);
        payloadControlField.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "=1.25"),
        });
        ResultTokenData control = CreateResultToken("control", "定", AttackResultType.StatusEffect, false, 0f);
        control.DefaultTriggerCount = 1;
        control.EffectDuration = 1f;
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            payloadControlField,
            control,
            payloadEnd,
        });
        List<CharBullet> spawnedBullets = new();
        AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);
        Physics.SyncTransforms();

        bool handled = InvokeTryRegisterImpact(spawnedBullets[0], primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryStatus.IsStunned, Is.True);
        Assert.That(secondaryStatus.IsStunned, Is.True);
        Assert.That(farStatus.IsStunned, Is.False);
    }

    [Test]
    public void Impact_WithTriggerSplitPayload_SpawnsDirectChildrenWithoutPayloads()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Owner");
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 10f);
        CompiledSpellProgram program = CreateTriggeredSplitProgram(3, 0.5f);
        List<CharBullet> spawnedBullets = new();
        AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            null,
            spawnedBullets);
        Physics.SyncTransforms();

        bool handled = InvokeTryRegisterImpact(spawnedBullets[0], primaryEnemy.GetComponent<BoxCollider>());
        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        int splitChildCount = 0;

        Assert.That(handled, Is.True);
        for (int i = 0; i < bullets.Length; i++)
        {
            CharBullet candidate = bullets[i];
            if (candidate == null || candidate == bulletPrefab || candidate == spawnedBullets[0])
            {
                continue;
            }

            splitChildCount++;
            Assert.That(candidate.CurrentProjectileNode, Is.Not.Null);
            Assert.That(candidate.CurrentProjectileNode.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
            Assert.That(candidate.CurrentProjectileNode.ResultEffects.HasSplit, Is.False);
            Assert.That(candidate.CurrentAttackSpec.resultType, Is.EqualTo(AttackResultType.DirectDamage));
            Assert.That(candidate.CurrentPayloads, Is.Empty);
        }

        Assert.That(splitChildCount, Is.EqualTo(3));
    }

    private CompiledSpellProgram CreateTriggeredExplosionProgram(float radius, float damageMultiplier)
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, false, radius);
        explosion.ExplosionDamageMultiplier = damageMultiplier;
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        return SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            explosion,
            payloadEnd,
        });
    }

    private CompiledSpellProgram CreateTriggeredHealingProgram()
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        fire.Damage = 2f;
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, false, 0f);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        return SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            healing,
            payloadEnd,
        });
    }

    private CompiledSpellProgram CreateTriggeredHealingRadiusProgram(float radius)
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        fire.Damage = 2f;
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData healing = CreateResultToken("healing", "愈", AttackResultType.Healing, true, 0f);
        healing.ValueParameterKind = SpellValueParameterKind.Radius;
        ValueTokenData radiusValue = CreateValueToken("radius", radius.ToString("0.##"), radius);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        return SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            healing,
            radiusValue,
            payloadEnd,
        });
    }

    private CompiledSpellProgram CreateTriggeredSplitProgram(int splitCount, float childDamageMultiplier)
    {
        CoreTokenData fire = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        TriggerTokenData trigger = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        PayloadBoundaryTokenData payloadStart = CreatePayloadBoundaryToken("payload_start", "[", PayloadBoundaryKind.Start);
        ResultTokenData split = CreateResultToken("split", "裂", AttackResultType.Split, true, 0f);
        split.DefaultTriggerCount = 1;
        split.ChildDamageMultiplier = childDamageMultiplier;
        ValueTokenData value = CreateValueToken("split_count", splitCount.ToString(), splitCount);
        PayloadBoundaryTokenData payloadEnd = CreatePayloadBoundaryToken("payload_end", "]", PayloadBoundaryKind.End);

        return SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            trigger,
            payloadStart,
            split,
            value,
            payloadEnd,
        });
    }

    private CoreTokenData CreateCoreToken(string tokenId, string displayText, AttackCoreType coreType)
    {
        CoreTokenData token = CreateToken<CoreTokenData>(tokenId, displayText);
        token.CoreType = coreType;
        token.Damage = 1f;
        token.ProjectileLife = 1;
        token.ImpactLifeCost = 1;
        token.ProjectileSpeed = 320f;
        token.MaxLifetime = 2f;
        token.MaxTravelDistance = 512f;
        token.ImpactMask = ~0;
        return token;
    }

    private TriggerTokenData CreateTriggerToken(string tokenId, string displayText, SpellTriggerType triggerType)
    {
        TriggerTokenData token = CreateToken<TriggerTokenData>(tokenId, displayText);
        token.TriggerType = triggerType;
        return token;
    }

    private PayloadBoundaryTokenData CreatePayloadBoundaryToken(string tokenId, string displayText, PayloadBoundaryKind boundaryKind)
    {
        PayloadBoundaryTokenData token = CreateToken<PayloadBoundaryTokenData>(tokenId, displayText);
        token.BoundaryKind = boundaryKind;
        return token;
    }

    private MulticastTokenData CreateMulticastToken(string tokenId, string displayText, int castCount)
    {
        MulticastTokenData token = CreateToken<MulticastTokenData>(tokenId, displayText);
        token.CastCount = castCount;
        return token;
    }

    private ModifierTokenData CreateModifierToken(string tokenId, string displayText, SpellModifierScope scope)
    {
        ModifierTokenData token = CreateToken<ModifierTokenData>(tokenId, displayText);
        return token;
    }

    private BehaviorTokenData CreateBehaviorToken(
        string tokenId,
        string displayText,
        AttackBehaviorType behaviorType,
        bool acceptsNumericValue,
        int defaultProjectileCount,
        float spreadAngleStep)
    {
        BehaviorTokenData token = CreateToken<BehaviorTokenData>(tokenId, displayText);
        token.BehaviorType = behaviorType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultProjectileCount = defaultProjectileCount;
        token.SpreadAngleStep = spreadAngleStep;
        token.ProjectileDamageMultiplier = 1f;
        return token;
    }

    private ResultTokenData CreateResultToken(
        string tokenId,
        string displayText,
        AttackResultType resultType,
        bool acceptsNumericValue,
        float defaultExplosionRadius)
    {
        ResultTokenData token = CreateToken<ResultTokenData>(tokenId, displayText);
        token.ResultType = resultType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultExplosionRadius = defaultExplosionRadius;
        token.ExplosionDamageMultiplier = 1f;
        return token;
    }

    private SpellBookData CreateSpellBook(string spellBookId, string displayName, int slotCount, float castCooldownSeconds)
    {
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        createdObjects.Add(spellBook);
        spellBook.SpellBookId = spellBookId;
        spellBook.DisplayName = displayName;
        spellBook.SlotCount = slotCount;
        spellBook.CastCooldownSeconds = castCooldownSeconds;
        spellBook.CastsPerActivation = 1;
        return spellBook;
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
        createdObjects.Add(token);
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.name = $"{tokenId}_asset";
        return token;
    }

    private CharBullet CreateBulletPrefab()
    {
        GameObject bulletObject = CreateGameObject("BulletPrefab");
        SphereCollider sphereCollider = bulletObject.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = 0.5f;
        return bulletObject.AddComponent<CharBullet>();
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private TestEnemy CreateEnemy(string name, Vector3 position, float health)
    {
        GameObject enemyObject = CreateGameObject(name);
        enemyObject.transform.position = position;
        enemyObject.tag = "Enemy_Object";
        enemyObject.AddComponent<BoxCollider>();
        TestEnemy enemy = enemyObject.AddComponent<TestEnemy>();
        enemy.SetHealth(health);
        return enemy;
    }

    private static int CountMessages(CompiledSpellProgram program, AttackCompileMessageSeverity severity)
    {
        int count = 0;
        for (int i = 0; i < program.Messages.Count; i++)
        {
            if (program.Messages[i].severity == severity)
            {
                count++;
            }
        }

        return count;
    }

    private static bool InvokeTryRegisterImpact(CharBullet bullet, Collider collider)
    {
        MethodInfo tryRegisterImpact = typeof(CharBullet).GetMethod("TryRegisterImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(tryRegisterImpact, Is.Not.Null);
        return (bool)tryRegisterImpact.Invoke(bullet, new object[] { collider });
    }

    private sealed class TestEnemy : Enemy
    {
        private float maxHealth = 1f;
        private float currentHealth = 1f;

        public override float MoveSpeed => 0f;
        public override float RotationSpeed => 0f;
        public override float StoppingDistance => 0f;
        public override float MaxHealth => maxHealth;
        public override float CurrentHealth => currentHealth;
        public override float AttackRange => throw new NotImplementedException();
        public override float AttackCooldown => throw new NotImplementedException();
        public override float AttackDamage => throw new NotImplementedException();

        public void SetHealth(float health)
        {
            maxHealth = Mathf.Max(0f, health);
            currentHealth = maxHealth;
        }

        public void SetHealth(float maxHealth, float currentHealth)
        {
            this.maxHealth = Mathf.Max(0f, maxHealth);
            this.currentHealth = Mathf.Clamp(currentHealth, 0f, this.maxHealth);
        }

        public override bool TryApplyDamage(float damage, out float remainingHealth, out bool isDead)
        {
            if (damage <= 0f || currentHealth <= 0f)
            {
                remainingHealth = currentHealth;
                isDead = currentHealth <= 0f;
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            remainingHealth = currentHealth;
            isDead = currentHealth <= 0f;
            return true;
        }

        public override bool TryApplyHealing(float healing, out float resultingHealth, out bool isDead)
        {
            resultingHealth = currentHealth;
            isDead = currentHealth <= 0f;
            if (healing <= 0f || isDead || currentHealth >= maxHealth)
            {
                return false;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + healing);
            resultingHealth = currentHealth;
            isDead = currentHealth <= 0f;
            return true;
        }
    }
}
