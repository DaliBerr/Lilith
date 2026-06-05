using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SpellProgramProjectileCompilerTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        AttackProjectileEmitter.RiddleCandidateIndexResolver = count => UnityEngine.Random.Range(0, count);
        CharBullet.ConfuseCandidateIndexResolver = count => UnityEngine.Random.Range(0, count);
        SpellProgramCompiler.ChaosCandidateIndexResolver = count => UnityEngine.Random.Range(0, count);

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
    public void ModifierDsl_ParsesNumericExpression()
    {
        bool parsed = TokenModifierExpressionUtility.TryParseExpression("+=10f", out TokenModifierOperator operation, out string literal, out string errorMessage);

        Assert.That(parsed, Is.True, errorMessage);
        Assert.That(operation, Is.EqualTo(TokenModifierOperator.Add));
        Assert.That(literal, Is.EqualTo("10f"));
        Assert.That(TokenModifierExpressionUtility.TryParseNumericLiteral(literal, out float numericValue, out errorMessage), Is.True, errorMessage);
        Assert.That(numericValue, Is.EqualTo(10f));
    }

    [Test]
    public void ModifierDsl_ParsesColorLiterals()
    {
        Assert.That(TokenModifierExpressionUtility.TryParseColorLiteral("Color.red", out Color namedColor, out string namedError), Is.True, namedError);
        Assert.That(namedColor, Is.EqualTo(Color.red));

        Assert.That(TokenModifierExpressionUtility.TryParseColorLiteral("#FF0000", out Color hexColor, out string hexError), Is.True, hexError);
        Assert.That(hexColor, Is.EqualTo(Color.red));
    }

    [Test]
    public void ModifierDsl_RejectsInvalidExpressions()
    {
        CoreTokenData invalidColorCore = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        invalidColorCore.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "+=Color.red"),
        });

        CompiledSpellProgram colorProgram = SpellProgramCompiler.Compile(new BaseTokenData[] { invalidColorCore });

        Assert.That(colorProgram.CanCast, Is.True);
        Assert.That(HasMessageContaining(colorProgram, AttackCompileMessageSeverity.Warning, "TextColor"), Is.True);

        CoreTokenData invalidSpeedCore = CreateCoreToken("edge_core", "Edge", AttackCoreType.Edge);
        invalidSpeedCore.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "/=0"),
        });

        CompiledSpellProgram speedProgram = SpellProgramCompiler.Compile(new BaseTokenData[] { invalidSpeedCore });

        Assert.That(speedProgram.CanCast, Is.True);
        Assert.That(HasMessageContaining(speedProgram, AttackCompileMessageSeverity.Warning, "divide by zero"), Is.True);
    }

    [Test]
    public void Compile_WithCoreOnly_UsesDefaultStraightAndDirectDamage()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(projectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Straight));
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(projectile.ProjectileCount, Is.EqualTo(1));
        Assert.That(program.Messages, Is.Empty);
    }

    [Test]
    public void Compile_WithFireCoreModifier_AddsTextColorOverride()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.HasTextColorOverride, Is.True);
        Assert.That(projectile.TextColor, Is.EqualTo(Color.red));
    }

    [Test]
    public void Compile_WithBulletTextOverride_UsesOverrideText()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetBulletTextOverride(true, "Ignis");

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.DisplayText, Is.EqualTo("Ignis"));
    }

    [Test]
    public void Compile_WithEdgeCoreModifier_ModifiesSpeedAndScale()
    {
        CoreTokenData coreToken = CreateCoreToken("edge_core", "Edge", AttackCoreType.Edge);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=0.8"),
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "+=10f"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.ScaleMultiplier, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(projectile.AttackSpec.projectileSpeed, Is.EqualTo(330f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithSpreadAndExplosionValues_BindsValuesToNearestConsumer()
    {
        CoreTokenData coreToken = CreateCoreToken("ice_core", "Ice", AttackCoreType.Ice);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 2, spreadAngleStep: 12f);
        ValueTokenData valueThree = CreateValueToken("three", "3", 3f);
        ResultTokenData explosionToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 1.5f);
        ValueTokenData valueTwo = CreateValueToken("two", "2", 2f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            valueThree,
            explosionToken,
            valueTwo,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Spread));
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(projectile.ProjectileCount, Is.EqualTo(3));
        Assert.That(projectile.ExplosionRadius, Is.EqualTo(1.5f));
        Assert.That(projectile.HasExplosion, Is.True);
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
    }

    [Test]
    public void Compile_ResultValueRadius_UsesDeclaredResultRadiusSlot()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData explosionToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 1.5f);
        explosionToken.ValueParameterKind = SpellValueParameterKind.Radius;
        ValueTokenData radiusValue = CreateValueToken("three", "3", 3f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            explosionToken,
            radiusValue,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(projectile.ExplosionRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(projectile.ResultEffects.explosionRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(projectile.HasExplosion, Is.True);
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Compile_ResultValueDuration_UsesDeclaredStatusDurationSlot()
    {
        CoreTokenData coreToken = CreateCoreToken("ice_core", "Ice", AttackCoreType.Ice);
        ResultTokenData controlToken = CreateResultToken("control", "Control", AttackResultType.StatusEffect, acceptsNumericValue: true, defaultExplosionRadius: 0f);
        controlToken.DefaultTriggerCount = 2;
        controlToken.EffectDuration = 1f;
        controlToken.ValueParameterKind = SpellValueParameterKind.Duration;
        ValueTokenData durationValue = CreateValueToken("long", "2.5", 2.5f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            controlToken,
            durationValue,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.StatusEffect));
        Assert.That(projectile.ResultEffects.controlTriggerCount, Is.EqualTo(2));
        Assert.That(projectile.ResultEffects.controlDuration, Is.EqualTo(2.5f).Within(0.0001f));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.EqualTo(0));
    }

    [Test]
    public void Compile_NewMovementBehaviorValues_WriteBehaviorParameter()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ValueTokenData three = CreateValueToken("three", "3", 3f);

        AssertBehaviorParameter(coreToken, CreateBehaviorToken("stasis", "Stasis", AttackBehaviorType.Stasis, true, 1, 0f, 1.5f), three, 3f);
        AssertBehaviorParameter(coreToken, CreateBehaviorToken("rush", "Rush", AttackBehaviorType.Rush, true, 1, 0f, 1f), three, 3f);
        AssertBehaviorParameter(coreToken, CreateBehaviorToken("slow", "Slow", AttackBehaviorType.Slow, true, 1, 0f, 1f), three, 3f);
        AssertBehaviorParameter(coreToken, CreateBehaviorToken("snake", "Snake", AttackBehaviorType.Snake, true, 1, 0f, 1f), three, 3f);
        AssertBehaviorParameter(coreToken, CreateBehaviorToken("wander", "Wander", AttackBehaviorType.Wander, true, 1, 0f, 1f), three, 3f);
        AssertBehaviorParameter(coreToken, CreateBehaviorToken("split", "Split", AttackBehaviorType.Split, true, 1, 0f, 1f), three, 3f);
        AssertBehaviorParameter(coreToken, CreateBehaviorToken("spin", "Spin", AttackBehaviorType.Spin, true, 1, 0f, 3f), three, 3f);
    }

    [Test]
    public void Compile_NewSplitAndSpinDefaults_WriteBehaviorParameter()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        BehaviorTokenData split = CreateBehaviorToken("split", "Split", AttackBehaviorType.Split, true, 1, 0f, 1f);
        BehaviorTokenData spin = CreateBehaviorToken("spin", "Spin", AttackBehaviorType.Spin, true, 1, 0f, 3f);

        SpellProjectileNode splitProjectile = GetPrimaryProjectile(SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken, split }));
        SpellProjectileNode spinProjectile = GetPrimaryProjectile(SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken, spin }));

        Assert.That(splitProjectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Split));
        Assert.That(splitProjectile.AttackSpec.behaviorParameter, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(spinProjectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Spin));
        Assert.That(spinProjectile.AttackSpec.behaviorParameter, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void Compile_NewCoreTokens_WriteRuntimePayloads()
    {
        CoreTokenData water = CreateCoreToken("water_core", "水", AttackCoreType.Water);
        water.Damage = 5f;
        water.SetStatusApplications(new SpellStatusApplication(SpellStatusSlot.Wet, 1f, duration: 3f, strength: 1f));

        CoreTokenData wind = CreateCoreToken("wind_core", "风", AttackCoreType.Wind);
        wind.Damage = 4.5f;
        wind.WindPressureRadius = 3f;
        wind.WindPressureDistance = 1.5f;
        wind.WindDisplacementWeightLimit = 1f;

        CoreTokenData light = CreateCoreToken("light_core", "光", AttackCoreType.Light);
        light.Damage = 10f;
        light.PiercesActorsAndEnvironment = true;
        light.PenetrationDamageMultiplier = 0.7f;
        light.SuppressImpactEffects = true;

        CoreTokenData sheep = CreateCoreToken("sheep_core", "羊", AttackCoreType.Sheep);
        sheep.SetStatusApplications(new SpellStatusApplication(SpellStatusSlot.Polymorph, 1f, threshold: 3f, duration: 4f, strength: 1f));

        CoreTokenData riddle = CreateCoreToken("riddle_core", "谜", AttackCoreType.Riddle);

        SpellProjectileNode waterProjectile = GetPrimaryProjectile(SpellProgramCompiler.Compile(new BaseTokenData[] { water }));
        SpellProjectileNode windProjectile = GetPrimaryProjectile(SpellProgramCompiler.Compile(new BaseTokenData[] { wind }));
        SpellProjectileNode lightProjectile = GetPrimaryProjectile(SpellProgramCompiler.Compile(new BaseTokenData[] { light }));
        SpellProjectileNode sheepProjectile = GetPrimaryProjectile(SpellProgramCompiler.Compile(new BaseTokenData[] { sheep }));
        SpellProjectileNode riddleProjectile = GetPrimaryProjectile(SpellProgramCompiler.Compile(new BaseTokenData[] { riddle }));

        Assert.That(waterProjectile.CoreType, Is.EqualTo(AttackCoreType.Water));
        Assert.That(waterProjectile.CoreEffects.statusApplications[0].slot, Is.EqualTo(SpellStatusSlot.Wet));
        Assert.That(windProjectile.CoreType, Is.EqualTo(AttackCoreType.Wind));
        Assert.That(windProjectile.CoreEffects.windPressureRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(windProjectile.CoreEffects.windPressureDistance, Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(lightProjectile.CoreType, Is.EqualTo(AttackCoreType.Light));
        Assert.That(lightProjectile.CoreEffects.HasPiercingSuppression, Is.True);
        Assert.That(lightProjectile.CoreEffects.penetrationDamageMultiplier, Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(sheepProjectile.CoreType, Is.EqualTo(AttackCoreType.Sheep));
        Assert.That(sheepProjectile.CoreEffects.statusApplications[0].slot, Is.EqualTo(SpellStatusSlot.Polymorph));
        Assert.That(sheepProjectile.CoreEffects.statusApplications[0].threshold, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(riddleProjectile.CoreType, Is.EqualTo(AttackCoreType.Riddle));
        Assert.That(SpellCoreRuntimeCatalog.RiddleCandidateCount, Is.EqualTo(8));
    }

    [Test]
    public void Compile_WithInvalidOrderAndDuplicateResult_ProducesWarningsButStillCompiles()
    {
        ValueTokenData valueToken = CreateValueToken("value", "2", 2f);
        CoreTokenData coreToken = CreateCoreToken("thunder_core", "Thunder", AttackCoreType.Thunder);
        ResultTokenData firstResult = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        ResultTokenData duplicateResult = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 3f);
        BehaviorTokenData lateBehavior = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 3, spreadAngleStep: 10f);
        ModifierTokenData trailingModifier = CreateModifierToken("after", "Post", SpellModifierScope.CurrentBlock);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            valueToken,
            coreToken,
            firstResult,
            lateBehavior,
            duplicateResult,
            trailingModifier,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(program.Messages.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Warning), Is.GreaterThanOrEqualTo(3));
        Assert.That(CountBlockModifiers(program, SpellModifierOrigin.ModifierToken), Is.EqualTo(0));
    }

    [Test]
    public void Compile_WithSplitAndControlValues_ConsumeOnlySupportedResultValues()
    {
        CoreTokenData coreToken = CreateCoreToken("edge_core", "Edge", AttackCoreType.Edge);
        ResultTokenData splitToken = CreateResultToken("split", "Split", AttackResultType.Split, acceptsNumericValue: true, defaultExplosionRadius: 0f);
        splitToken.DefaultTriggerCount = 2;
        splitToken.ChildDamageMultiplier = 0.5f;
        ValueTokenData splitValue = CreateValueToken("five", "5", 5f);
        CompiledSpellProgram splitProgram = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            splitToken,
            splitValue,
        });
        SpellProjectileNode splitProjectile = GetPrimaryProjectile(splitProgram);

        ResultTokenData controlToken = CreateResultToken("control", "Control", AttackResultType.StatusEffect, acceptsNumericValue: true, defaultExplosionRadius: 0f);
        controlToken.DefaultTriggerCount = 5;
        controlToken.EffectDuration = 1f;
        ValueTokenData controlValue = CreateValueToken("two", "2", 2f);
        CompiledSpellProgram controlProgram = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            controlToken,
            controlValue,
        });
        SpellProjectileNode controlProjectile = GetPrimaryProjectile(controlProgram);

        Assert.That(splitProgram.CanCast, Is.True);
        Assert.That(splitProjectile.ResultEffects.splitProjectileCount, Is.EqualTo(5));
        Assert.That(splitProjectile.ResultEffects.splitDamageMultiplier, Is.EqualTo(0.5f));
        Assert.That(controlProgram.CanCast, Is.True);
        Assert.That(controlProjectile.ResultEffects.controlTriggerCount, Is.EqualTo(2));
        Assert.That(controlProjectile.ResultEffects.controlDuration, Is.EqualTo(1f));
    }

    [Test]
    public void Compile_WithBounceAndPierceValues_AssignsBehaviorCountsAndPierceExtension()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        BehaviorTokenData bounceToken = CreateBehaviorToken("bounce", "Bounce", AttackBehaviorType.Bounce, acceptsNumericValue: true, defaultProjectileCount: 1, spreadAngleStep: 0f);
        ValueTokenData valueThree = CreateValueToken("three", "3", 3f);
        CompiledSpellProgram bounceProgram = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            bounceToken,
            valueThree,
        });
        SpellProjectileNode bounceProjectile = GetPrimaryProjectile(bounceProgram);

        BehaviorTokenData pierceToken = CreateBehaviorToken("pierce", "Pierce", AttackBehaviorType.Pierce, acceptsNumericValue: true, defaultProjectileCount: 1, spreadAngleStep: 0f);
        pierceToken.PierceLifetimeDistanceScalePerCount = 0.2f;
        ValueTokenData valueFive = CreateValueToken("five", "5", 5f);
        CompiledSpellProgram pierceProgram = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            pierceToken,
            valueFive,
        });
        SpellProjectileNode pierceProjectile = GetPrimaryProjectile(pierceProgram);

        Assert.That(bounceProgram.CanCast, Is.True);
        Assert.That(bounceProjectile.AttackSpec.bounceCount, Is.EqualTo(3));
        Assert.That(bounceProjectile.AttackSpec.pierceCount, Is.EqualTo(0));
        Assert.That(pierceProgram.CanCast, Is.True);
        Assert.That(pierceProjectile.AttackSpec.pierceCount, Is.EqualTo(5));
        Assert.That(pierceProjectile.AttackSpec.projectileLife, Is.EqualTo(6));
        Assert.That(pierceProjectile.AttackSpec.maxLifetime, Is.EqualTo(4f).Within(0.0001f));
        Assert.That(pierceProjectile.AttackSpec.maxTravelDistance, Is.EqualTo(1024f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithHomingAndHealing_ProducesHomingHealingAttack()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        BehaviorTokenData homingToken = CreateBehaviorToken("homing", "Homing", AttackBehaviorType.Homing, acceptsNumericValue: false, defaultProjectileCount: 1, spreadAngleStep: 0f);
        ResultTokenData healingToken = CreateResultToken("healing", "Healing", AttackResultType.Healing, acceptsNumericValue: false, defaultExplosionRadius: 0f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            homingToken,
            healingToken,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Homing));
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.Healing));
        Assert.That(projectile.ProjectileCount, Is.EqualTo(1));
        Assert.That(projectile.HasExplosion, Is.False);
    }

    [Test]
    public void Compile_WithIgnoredDuplicateToken_DoesNotApplyItsModifiers()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData firstResult = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        ResultTokenData duplicateResult = CreateResultToken("duplicate_explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 2f);
        duplicateResult.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.MaxLifetime, "*=10"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            firstResult,
            duplicateResult,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(projectile.AttackSpec.maxLifetime, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithAcceptedCurrentBlockModifiers_ModifiersParticipate()
    {
        ModifierTokenData leadingModifier = CreateModifierToken("pre", "Pre", SpellModifierScope.CurrentBlock);
        leadingModifier.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=0.5"),
        });

        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        ModifierTokenData trailingModifier = CreateModifierToken("post", "Post", SpellModifierScope.CurrentBlock);
        trailingModifier.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            leadingModifier,
            coreToken,
            resultToken,
            trailingModifier,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.ScaleMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(projectile.HasTextColorOverride, Is.False);
        Assert.That(CountBlockModifiers(program, SpellModifierOrigin.ModifierToken), Is.EqualTo(1));
    }

    [Test]
    public void Compile_WithAcceptedLinkedItem_AppliesDamageMultiplier()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", 1.5f, coreToken, resultToken);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            linked,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.AttackSpec.damage, Is.EqualTo(1.5f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithPartiallyAcceptedLinkedItem_DoesNotApplyDamageMultiplier()
    {
        BehaviorTokenData behaviorToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 3, spreadAngleStep: 10f);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        LinkedTokenData linked = CreateLinkedToken("linked_invalid_prefix", 2f, behaviorToken, resultToken);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            linked,
            coreToken,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.AttackSpec.damage, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithMultipleAcceptedLinkedItems_MultipliesDamageInOrder()
    {
        ModifierTokenData modifierToken = CreateModifierToken("modifier", "Mod", SpellModifierScope.CurrentBlock);
        LinkedTokenData modifierLinked = CreateLinkedToken("linked_modifier", 1.5f, modifierToken);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        LinkedTokenData coreLinked = CreateLinkedToken("linked_core_result", 2f, coreToken, resultToken);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            modifierLinked,
            coreLinked,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True);
        Assert.That(projectile.AttackSpec.damage, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithoutCore_ReturnsHardFailure()
    {
        ValueTokenData valueToken = CreateValueToken("two", "2", 2f);
        ResultTokenData resultToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 3f);

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            valueToken,
            resultToken,
        });

        Assert.That(program.CanCast, Is.False);
        Assert.That(HasErrors(program), Is.True);
        Assert.That(CountMessages(program, AttackCompileMessageSeverity.Error), Is.EqualTo(1));
    }

    [Test]
    public void Emit_WithSpreadAttack_SpawnsExpectedProjectileCount()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 2, spreadAngleStep: 12f);
        ValueTokenData valueThree = CreateValueToken("three", "3", 3f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            valueThree,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);

        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        Assert.That(emittedCount, Is.EqualTo(3));
        Assert.That(bullets.Length, Is.EqualTo(4));
    }

    [Test]
    public void Emit_WithActivationCastCount_SpawnsRepeatedProgramInFan()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });
        List<CharBullet> spawnedBullets = new();

        int emittedCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            program,
            BulletTargetPolicy.EnemiesOnly,
            null,
            spawnedBullets,
            activationCastCount: 3,
            activationSpreadAngleStep: 10f);

        Assert.That(emittedCount, Is.EqualTo(3));
        Assert.That(spawnedBullets.Count, Is.EqualTo(3));
        AssertDirection(spawnedBullets[0].Direction, Quaternion.AngleAxis(-10f, Vector3.up) * Vector3.forward);
        AssertDirection(spawnedBullets[1].Direction, Vector3.forward);
        AssertDirection(spawnedBullets[2].Direction, Quaternion.AngleAxis(10f, Vector3.up) * Vector3.forward);
    }

    [Test]
    public void Emit_WithFireCoreColor_AppliesGlyphColor()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.CurrentProjectileNode, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText.color, Is.EqualTo(Color.red));
    }

    [Test]
    public void Emit_WithBulletTextOverride_AppliesGlyphText()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetBulletTextOverride(true, "Ignis");

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText.text, Is.EqualTo("Ignis"));
    }

    [Test]
    public void Emit_WithFontSizeModifier_ResizesGlyphRectToSquare()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.FontSize, "=42"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText.rectTransform.rect.width, Is.EqualTo(42f).Within(0.0001f));
        Assert.That(emittedBullet.GlyphText.rectTransform.rect.height, Is.EqualTo(42f).Within(0.0001f));
        Assert.That(emittedBullet.ImpactCollider.radius, Is.EqualTo(21f).Within(0.0001f));
    }

    [Test]
    public void Emit_WithFontSizeAdditiveModifier_AppliesRelativeToCurrentRectSize()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.FontSize, "+=12"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText.rectTransform.rect.width, Is.EqualTo(32f).Within(0.0001f));
        Assert.That(emittedBullet.GlyphText.rectTransform.rect.height, Is.EqualTo(32f).Within(0.0001f));
        Assert.That(emittedBullet.ImpactCollider.radius, Is.EqualTo(16f).Within(0.0001f));
    }

    [Test]
    public void InitializeShot_WithEdgeCoreModifier_AppliesSpeedAndScale()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("edge_core", "Edge", AttackCoreType.Edge);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=0.8"),
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "+=10f"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.Speed, Is.EqualTo(330f).Within(0.0001f));
        Assert.That(emittedBullet.ScaleMultiplier, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void InitializeShot_WithSeparateImpactRadiusModifier_KeepsScaleAndColliderIndependent()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("edge_core", "Edge", AttackCoreType.Edge);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=0.5"),
            new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=3"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.ScaleMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(emittedBullet.ImpactRadiusMultiplier, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(emittedBullet.ImpactCollider.radius, Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void Emit_WithoutGlyphText_DoesNotFailVisualModifiers()
    {
        CharBullet bulletPrefab = CreateBulletPrefab(includeGlyph: false);
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
            new TokenModifierDefinition(TokenModifierTarget.FontSize, "=42"),
        });

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, program);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText, Is.Null);
    }

    [Test]
    public void RegisterImpact_WithExplosion_DamagesPrimaryTwiceAndSecondaryOnce()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 5f);
        TestEnemy secondaryEnemy = CreateEnemy("SecondaryEnemy", new Vector3(1f, 0f, 2f), 5f);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData explosionToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 1f);
        explosionToken.ExplosionDamageMultiplier = 0.3f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            explosionToken,
        });

        Assert.That(program.CanCast, Is.True);
        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        Assert.That(bullet.CurrentProjectileNode, Is.Not.Null);
        Physics.SyncTransforms();

        MethodInfo tryRegisterImpact = typeof(CharBullet).GetMethod("TryRegisterImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(tryRegisterImpact, Is.Not.Null);

        BoxCollider primaryCollider = primaryEnemy.GetComponent<BoxCollider>();
        bool handled = (bool)tryRegisterImpact.Invoke(bullet, new object[] { primaryCollider });

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(3.7f).Within(0.0001f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(4.7f).Within(0.0001f));
    }

    [Test]
    public void RegisterImpact_WithEdgeCore_OnlyShieldEnemyGetsArmoredBonus()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        TestEnemy shieldEnemy = CreateEnemy("ShieldEnemy", new Vector3(0f, 0f, 2f), 20f);
        shieldEnemy.TryBindDefinition(CreateEnemyDefinition("shield"));
        TestEnemy normalEnemy = CreateEnemy("NormalEnemy", new Vector3(0f, 0f, 4f), 20f);
        normalEnemy.TryBindDefinition(CreateEnemyDefinition("normal"));

        CoreTokenData coreToken = CreateCoreToken("edge_core", "Edge", AttackCoreType.Edge);
        coreToken.Damage = 10f;
        coreToken.ArmoredEnemyId = "shield";
        coreToken.ArmoredDamageMultiplier = 1.2f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        bool shieldHandled = InvokeTryRegisterImpact(bullet, shieldEnemy.GetComponent<BoxCollider>());
        Assert.That(shieldHandled, Is.True);
        Assert.That(shieldEnemy.CurrentHealth, Is.EqualTo(8f).Within(0.0001f));

        CharBullet secondBullet = CreateBulletPrefab();
        InitializeShotFromProgram(secondBullet, owner.transform, Vector3.zero, Vector3.forward, program);
        bool normalHandled = InvokeTryRegisterImpact(secondBullet, normalEnemy.GetComponent<BoxCollider>());
        Assert.That(normalHandled, Is.True);
        Assert.That(normalEnemy.CurrentHealth, Is.EqualTo(10f).Within(0.0001f));
    }

    [Test]
    public void RegisterImpact_WithThunderCore_DamagesOnlyOneNearbySecondaryEnemy()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 20f);
        TestEnemy secondaryEnemy = CreateEnemy("SecondaryEnemy", new Vector3(1f, 0f, 2f), 20f);
        TestEnemy farEnemy = CreateEnemy("FarEnemy", new Vector3(60f, 0f, 2f), 20f);

        CoreTokenData coreToken = CreateCoreToken("thunder_core", "Thunder", AttackCoreType.Thunder);
        coreToken.Damage = 7f;
        coreToken.ThunderChainTargetCount = 1;
        coreToken.ThunderChainRadius = 48f;
        coreToken.ThunderChainDamage = 4f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        bool handled = InvokeTryRegisterImpact(bullet, primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(13f).Within(0.0001f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(16f).Within(0.0001f));
        Assert.That(farEnemy.CurrentHealth, Is.EqualTo(20f).Within(0.0001f));
    }

    [Test]
    public void RegisterImpact_WithChainBehavior_DamagesSecondaryWithoutTriggeringPayload()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 20f);
        TestEnemy secondaryEnemy = CreateEnemy("SecondaryEnemy", new Vector3(1f, 0f, 2f), 1f);
        TestEnemy payloadProbeEnemy = CreateEnemy("PayloadProbeEnemy", new Vector3(1.5f, 0f, 2f), 20f);

        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.Damage = 4f;
        BehaviorTokenData chainToken = CreateBehaviorToken(
            "chain",
            "Chain",
            AttackBehaviorType.Chain,
            acceptsNumericValue: true,
            defaultProjectileCount: 1,
            spreadAngleStep: 0f);
        ValueTokenData one = CreateValueToken("one", "1", 1f);
        TriggerTokenData onKill = CreateTriggerToken("on_kill", "Kill", SpellTriggerType.OnKill);
        ResultTokenData explosion = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: false, defaultExplosionRadius: 3f);
        explosion.ExplosionDamageMultiplier = 1f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            chainToken,
            one,
            onKill,
            explosion,
        });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        bool handled = InvokeTryRegisterImpact(bullet, primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(16f).Within(0.0001f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(payloadProbeEnemy.CurrentHealth, Is.EqualTo(20f).Within(0.0001f));
    }

    [Test]
    public void RegisterImpact_WithSplitResult_SpawnedChildrenLoseSplitResult()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bulletPrefab = CreateBulletPrefab();
        CharBullet bullet = Object.Instantiate(bulletPrefab);
        createdObjects.Add(bullet.gameObject);
        bullet.SetSpawnTemplate(bulletPrefab);
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 20f);

        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData splitToken = CreateResultToken("split", "Split", AttackResultType.Split, acceptsNumericValue: true, defaultExplosionRadius: 0f);
        splitToken.DefaultTriggerCount = 2;
        splitToken.ChildDamageMultiplier = 0.5f;
        ValueTokenData splitValue = CreateValueToken("three", "3", 3f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            splitToken,
            splitValue,
        });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        bool handled = InvokeTryRegisterImpact(bullet, primaryEnemy.GetComponent<BoxCollider>());
        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);

        Assert.That(handled, Is.True);
        Assert.That(bullets.Length, Is.GreaterThanOrEqualTo(4));
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] == null || bullets[i] == bullet || bullets[i] == bulletPrefab)
            {
                continue;
            }

            Assert.That(bullets[i].CurrentProjectileNode, Is.Not.Null);
            Assert.That(bullets[i].CurrentProjectileNode.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
            Assert.That(bullets[i].CurrentAttackSpec.resultType, Is.EqualTo(AttackResultType.DirectDamage));
        }
    }

    [Test]
    public void RegisterImpact_WithPierceBehavior_DamagesMultipleEnemiesAtFullDamage()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        TestEnemy firstEnemy = CreateEnemy("FirstEnemy", new Vector3(0f, 0f, 2f), 20f);
        TestEnemy secondEnemy = CreateEnemy("SecondEnemy", new Vector3(0f, 0f, 4f), 20f);

        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.Damage = 5f;
        BehaviorTokenData pierceToken = CreateBehaviorToken("pierce", "Pierce", AttackBehaviorType.Pierce, acceptsNumericValue: true, defaultProjectileCount: 1, spreadAngleStep: 0f);
        ValueTokenData valueOne = CreateValueToken("one", "1", 1f);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            pierceToken,
            valueOne,
        });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);

        bool firstHandled = InvokeTryRegisterImpact(bullet, firstEnemy.GetComponent<BoxCollider>());
        bool secondHandled = InvokeTryRegisterImpact(bullet, secondEnemy.GetComponent<BoxCollider>());

        Assert.That(firstHandled, Is.True);
        Assert.That(secondHandled, Is.True);
        Assert.That(firstEnemy.CurrentHealth, Is.EqualTo(15f).Within(0.0001f));
        Assert.That(secondEnemy.CurrentHealth, Is.EqualTo(15f).Within(0.0001f));
    }

    [Test]
    public void RegisterImpact_WithLightCore_PiercesWallAndEnemiesWithDecayWithoutResultOrPayload()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        BoxCollider wall = CreateGameObject("Wall").AddComponent<BoxCollider>();
        TestEnemy firstEnemy = CreateEnemy("FirstEnemy", new Vector3(0f, 0f, 2f), 20f);
        TestEnemy secondEnemy = CreateEnemy("SecondEnemy", new Vector3(0f, 0f, 4f), 20f);
        TestEnemy explosionProbe = CreateEnemy("ExplosionProbe", new Vector3(1f, 0f, 2f), 20f);

        CoreTokenData light = CreateCoreToken("light_core", "光", AttackCoreType.Light);
        light.Damage = 10f;
        light.PiercesActorsAndEnvironment = true;
        light.PenetrationDamageMultiplier = 0.7f;
        light.SuppressImpactEffects = true;
        ResultTokenData explosion = CreateResultToken("explosion", "爆", AttackResultType.Explosion, acceptsNumericValue: false, defaultExplosionRadius: 3f);
        explosion.ExplosionDamageMultiplier = 1f;
        TriggerTokenData onHit = CreateTriggerToken("on_hit", "触", SpellTriggerType.OnHit);
        ResultTokenData payloadExplosion = CreateResultToken("payload_explosion", "爆", AttackResultType.Explosion, acceptsNumericValue: false, defaultExplosionRadius: 3f);
        payloadExplosion.ExplosionDamageMultiplier = 1f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            light,
            explosion,
            onHit,
            payloadExplosion,
        });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        bool wallHandled = InvokeTryRegisterImpact(bullet, wall);
        bool firstHandled = InvokeTryRegisterImpact(bullet, firstEnemy.GetComponent<BoxCollider>());
        bool secondHandled = InvokeTryRegisterImpact(bullet, secondEnemy.GetComponent<BoxCollider>());

        Assert.That(wallHandled, Is.True);
        Assert.That(firstHandled, Is.True);
        Assert.That(secondHandled, Is.True);
        Assert.That(bullet.IsActiveShot, Is.True);
        Assert.That(bullet.RemainingLife, Is.EqualTo(1));
        Assert.That(firstEnemy.CurrentHealth, Is.EqualTo(13f).Within(0.0001f));
        Assert.That(secondEnemy.CurrentHealth, Is.EqualTo(15.1f).Within(0.0001f));
        Assert.That(explosionProbe.CurrentHealth, Is.EqualTo(20f).Within(0.0001f));
    }

    [Test]
    public void RegisterImpact_WithWindCore_PushesOnlyLowWeightEnemies()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bullet = CreateBulletPrefab();
        TestEnemy primaryEnemy = CreateEnemy("PrimaryEnemy", new Vector3(0f, 0f, 2f), 20f);
        TestEnemy lightEnemy = CreateEnemy("LightEnemy", new Vector3(1f, 0f, 2f), 20f);
        TestEnemy heavyEnemy = CreateEnemy("HeavyEnemy", new Vector3(2f, 0f, 2f), 20f);
        heavyEnemy.SetDisplacementWeight(2f);

        CoreTokenData wind = CreateCoreToken("wind_core", "风", AttackCoreType.Wind);
        wind.Damage = 1f;
        wind.WindPressureRadius = 3f;
        wind.WindPressureDistance = 1.5f;
        wind.WindDisplacementWeightLimit = 1f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { wind });

        InitializeShotFromProgram(bullet, owner.transform, Vector3.zero, Vector3.forward, program);
        Physics.SyncTransforms();
        bool handled = InvokeTryRegisterImpact(bullet, primaryEnemy.GetComponent<BoxCollider>());

        Assert.That(handled, Is.True);
        Assert.That(lightEnemy.transform.position.x, Is.GreaterThan(1f));
        Assert.That(heavyEnemy.transform.position.x, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Emit_WithRiddleCore_ResolvesCandidatePerProjectile()
    {
        GameObject owner = CreateGameObject("Owner");
        CharBullet bulletPrefab = CreateBulletPrefab();
        CoreTokenData riddle = CreateCoreToken("riddle_core", "谜", AttackCoreType.Riddle);
        riddle.Damage = 1f;
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { riddle });
        SpellProjectileNode riddleProjectile = GetPrimaryProjectile(program);

        int[] candidateIndexes = { 1, 2 };
        int callIndex = 0;
        AttackProjectileEmitter.RiddleCandidateIndexResolver = count => candidateIndexes[Mathf.Min(callIndex++, candidateIndexes.Length - 1)];
        List<CharBullet> spawned = new();

        int firstCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            riddleProjectile,
            BulletTargetPolicy.EnemiesOnly,
            null,
            spawned);
        int secondCount = AttackProjectileEmitter.Emit(
            bulletPrefab,
            owner.transform,
            Vector3.zero,
            Vector3.forward,
            riddleProjectile,
            BulletTargetPolicy.EnemiesOnly,
            null,
            spawned);

        Assert.That(firstCount, Is.EqualTo(1));
        Assert.That(secondCount, Is.EqualTo(1));
        Assert.That(spawned[0].CurrentProjectileNode.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(spawned[0].CurrentAttackSpec.damage, Is.EqualTo(7f).Within(0.0001f));
        Assert.That(spawned[1].CurrentProjectileNode.CoreType, Is.EqualTo(AttackCoreType.Ice));
        Assert.That(spawned[1].CurrentAttackSpec.damage, Is.EqualTo(6f).Within(0.0001f));
    }

    private CoreTokenData CreateCoreToken(string tokenId, string displayText, AttackCoreType coreType)
    {
        CoreTokenData token = CreateAssetInstance<CoreTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.CoreType = coreType;
        token.Damage = 1f;
        token.ProjectileLife = 1;
        token.ImpactLifeCost = 1;
        token.ProjectileSpeed = 320f;
        token.MaxLifetime = 2f;
        token.MaxTravelDistance = 512f;
        token.ImpactMask = ~0;
        token.ArmoredEnemyId = string.Empty;
        token.ArmoredDamageMultiplier = 1f;
        token.BurnTriggerCount = 0;
        token.BurnDamagePerSecond = 0f;
        token.BurnDuration = 0f;
        token.SlowPercent = 0f;
        token.SlowDuration = 0f;
        token.ThunderChainTargetCount = 0;
        token.ThunderChainRadius = 0f;
        token.ThunderChainDamage = 0f;
        return token;
    }

    private BehaviorTokenData CreateBehaviorToken(
        string tokenId,
        string displayText,
        AttackBehaviorType behaviorType,
        bool acceptsNumericValue,
        int defaultProjectileCount,
        float spreadAngleStep,
        float defaultBehaviorParameter = 0f)
    {
        BehaviorTokenData token = CreateAssetInstance<BehaviorTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.BehaviorType = behaviorType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultProjectileCount = defaultProjectileCount;
        token.SpreadAngleStep = spreadAngleStep;
        token.ProjectileDamageMultiplier = 1f;
        token.PierceLifetimeDistanceScalePerCount = 0.2f;
        token.DefaultBehaviorParameter = defaultBehaviorParameter;
        return token;
    }

    private TriggerTokenData CreateTriggerToken(string tokenId, string displayText, SpellTriggerType triggerType)
    {
        TriggerTokenData token = CreateAssetInstance<TriggerTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.TriggerType = triggerType;
        return token;
    }

    private ResultTokenData CreateResultToken(string tokenId, string displayText, AttackResultType resultType, bool acceptsNumericValue, float defaultExplosionRadius)
    {
        ResultTokenData token = CreateAssetInstance<ResultTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.ResultType = resultType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultExplosionRadius = defaultExplosionRadius;
        token.ExplosionDamageMultiplier = 1f;
        token.DefaultTriggerCount = 0;
        token.EffectDuration = 0f;
        token.ChildDamageMultiplier = 0.5f;
        return token;
    }

    private EnemyDefinition CreateEnemyDefinition(string enemyId)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", enemyId);
        SetPrivateField(definition, "displayName", enemyId);
        return definition;
    }

    private ValueTokenData CreateValueToken(string tokenId, string displayText, float numericValue)
    {
        ValueTokenData token = CreateAssetInstance<ValueTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.NumericValue = numericValue;
        return token;
    }

    private ModifierTokenData CreateModifierToken(string tokenId, string displayText, SpellModifierScope scope)
    {
        ModifierTokenData token = CreateAssetInstance<ModifierTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        return token;
    }

    private T CreateAssetInstance<T>(string name) where T : BaseTokenData
    {
        T token = ScriptableObject.CreateInstance<T>();
        token.name = name;
        createdObjects.Add(token);
        return token;
    }

    private LinkedTokenData CreateLinkedToken(string itemId, float damageMultiplier, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        token.ItemId = itemId;
        token.ConfiguredDamageMultiplier = damageMultiplier;
        token.SetLinkedTokens(linkedTokens);
        token.name = itemId;
        createdObjects.Add(token);
        return token;
    }

    private CharBullet CreateBulletPrefab(bool includeGlyph = true)
    {
        GameObject bulletObject = CreateGameObject("BulletPrefab");
        SphereCollider sphereCollider = bulletObject.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = 0.5f;

        if (includeGlyph)
        {
            GameObject glyphObject = CreateGameObject("BulletGlyph");
            glyphObject.transform.SetParent(bulletObject.transform, false);
            TextMeshPro textMeshPro = glyphObject.AddComponent<TextMeshPro>();
            textMeshPro.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 20f);
            textMeshPro.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
        }

        return bulletObject.AddComponent<CharBullet>();
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

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void InitializeShotFromProgram(
        CharBullet bullet,
        Transform owner,
        Vector3 spawnPosition,
        Vector3 direction,
        CompiledSpellProgram program,
        BulletTargetPolicy targetPolicy = BulletTargetPolicy.EnemiesOnly)
    {
        Assert.That(program, Is.Not.Null);
        Assert.That(program.TryGetPrimaryProjectile(out SpellProjectileNode projectileNode), Is.True);
        bullet.InitializeShot(owner, spawnPosition, direction, projectileNode.AttackSpec, projectileNode, targetPolicy);
    }

    private static SpellProjectileNode GetPrimaryProjectile(CompiledSpellProgram program)
    {
        Assert.That(program, Is.Not.Null);
        Assert.That(program.TryGetPrimaryProjectile(out SpellProjectileNode projectile), Is.True);
        return projectile;
    }

    private static void AssertDirection(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
    }

    private static void AssertBehaviorParameter(
        CoreTokenData coreToken,
        BehaviorTokenData behaviorToken,
        ValueTokenData valueToken,
        float expectedParameter)
    {
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            behaviorToken,
            valueToken,
        });
        SpellProjectileNode projectile = GetPrimaryProjectile(program);

        Assert.That(program.CanCast, Is.True, behaviorToken.TokenId);
        Assert.That(projectile.BehaviorType, Is.EqualTo(behaviorToken.BehaviorType), behaviorToken.TokenId);
        Assert.That(projectile.AttackSpec.behaviorParameter, Is.EqualTo(expectedParameter).Within(0.0001f), behaviorToken.TokenId);
        if (behaviorToken.BehaviorType == AttackBehaviorType.Stasis)
        {
            Assert.That(projectile.AttackSpec.maxLifetime, Is.EqualTo(expectedParameter).Within(0.0001f), behaviorToken.TokenId);
        }
    }

    private static bool HasErrors(CompiledSpellProgram program)
    {
        return CountMessages(program, AttackCompileMessageSeverity.Error) > 0;
    }

    private static bool HasMessageContaining(
        CompiledSpellProgram program,
        AttackCompileMessageSeverity severity,
        string expectedText)
    {
        Assert.That(program, Is.Not.Null);
        for (int i = 0; i < program.Messages.Count; i++)
        {
            AttackCompileMessage message = program.Messages[i];
            if (message.severity == severity &&
                message.message.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountMessages(CompiledSpellProgram program, AttackCompileMessageSeverity severity)
    {
        Assert.That(program, Is.Not.Null);
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

    private static int CountBlockModifiers(CompiledSpellProgram program, SpellModifierOrigin origin)
    {
        Assert.That(program, Is.Not.Null);
        SpellCastBlock block = program.PrimaryCastBlock;
        if (block == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < block.Modifiers.Count; i++)
        {
            if (block.Modifiers[i] != null && block.Modifiers[i].Origin == origin)
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

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        Type currentType = target.GetType();
        while (currentType != null)
        {
            FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            currentType = currentType.BaseType;
        }

        Assert.Fail($"Missing private field '{fieldName}'.");
    }

    private CharBullet FindEmittedBullet(CharBullet bulletPrefab)
    {
        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null && bullets[i] != bulletPrefab)
            {
                return bullets[i];
            }
        }

        return null;
    }

    private sealed class TestEnemy : Enemy
    {
        private float maxHealth = 1f;
        private float currentHealth = 1f;
        private float displacementWeight = 1f;

        public override float MoveSpeed => 0f;
        public override float RotationSpeed => 0f;
        public override float StoppingDistance => 0f;
        public override float MaxHealth => maxHealth;
        public override float CurrentHealth => currentHealth;
        public override float DisplacementWeight => displacementWeight;

        public override float AttackRange => throw new System.NotImplementedException();

        public override float AttackCooldown => throw new System.NotImplementedException();

        public override float AttackDamage => throw new System.NotImplementedException();

        public void SetHealth(float health)
        {
            maxHealth = Mathf.Max(0f, health);
            currentHealth = maxHealth;
        }

        public void SetDisplacementWeight(float weight)
        {
            displacementWeight = Mathf.Max(0f, weight);
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
