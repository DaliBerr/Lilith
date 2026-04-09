using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class AttackFormulaCompilerTests
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
        CompiledAttack compiledAttack = new()
        {
            AttackSpec = AttackSpec.CreateDefault(),
            ScaleMultiplier = 1f,
            ImpactRadiusMultiplier = 1f,
        };

        Assert.That(
            TokenModifierExpressionUtility.TryApplyModifier(compiledAttack, new TokenModifierDefinition(TokenModifierTarget.TextColor, "+=Color.red"), out string colorError),
            Is.False);
        Assert.That(colorError, Does.Contain("TextColor"));

        Assert.That(
            TokenModifierExpressionUtility.TryApplyModifier(compiledAttack, new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "/=0"), out string divideError),
            Is.False);
        Assert.That(divideError, Does.Contain("divide by zero"));
    }

    [Test]
    public void Compile_WithCoreOnly_UsesDefaultStraightAndDirectDamage()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(compiledAttack.BehaviorType, Is.EqualTo(AttackBehaviorType.Straight));
        Assert.That(compiledAttack.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(compiledAttack.GetProjectileCount(), Is.EqualTo(1));
        Assert.That(compiledAttack.Messages, Is.Empty);
    }

    [Test]
    public void Compile_WithFireCoreModifier_AddsTextColorOverride()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
        });

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.HasTextColorOverride, Is.True);
        Assert.That(compiledAttack.TextColor, Is.EqualTo(Color.red));
    }

    [Test]
    public void Compile_WithBulletTextOverride_UsesOverrideText()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetBulletTextOverride(true, "Ignis");

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.DisplayText, Is.EqualTo("Ignis"));
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.ScaleMultiplier, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(compiledAttack.AttackSpec.projectileSpeed, Is.EqualTo(330f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithSpreadAndExplosionValues_BindsValuesToNearestConsumer()
    {
        CoreTokenData coreToken = CreateCoreToken("ice_core", "Ice", AttackCoreType.Ice);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 2, spreadAngleStep: 12f);
        ValueTokenData valueThree = CreateValueToken("three", "3", 3f);
        ResultTokenData explosionToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 1.5f);
        ValueTokenData valueTwo = CreateValueToken("two", "2", 2f);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            valueThree,
            explosionToken,
            valueTwo,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.BehaviorType, Is.EqualTo(AttackBehaviorType.Spread));
        Assert.That(compiledAttack.ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(compiledAttack.SpreadProjectileCount, Is.EqualTo(3));
        Assert.That(compiledAttack.ExplosionRadius, Is.EqualTo(2f));
        Assert.That(compiledAttack.HasExplosion, Is.True);
        Assert.That(compiledAttack.Messages, Is.Empty);
    }

    [Test]
    public void Compile_WithInvalidOrderAndDuplicateResult_ProducesWarningsButStillCompiles()
    {
        ValueTokenData valueToken = CreateValueToken("value", "2", 2f);
        CoreTokenData coreToken = CreateCoreToken("thunder_core", "Thunder", AttackCoreType.Thunder);
        ResultTokenData firstResult = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        ResultTokenData duplicateResult = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 3f);
        BehaviorTokenData lateBehavior = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 3, spreadAngleStep: 10f);
        TestPostTokenData postToken = CreatePostToken("after", "Post");

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            valueToken,
            coreToken,
            firstResult,
            lateBehavior,
            duplicateResult,
            postToken,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(compiledAttack.Messages.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(CountMessages(compiledAttack, AttackCompileMessageSeverity.Warning), Is.GreaterThanOrEqualTo(3));
        Assert.That(compiledAttack.PostTokens.Count, Is.EqualTo(1));
    }

    [Test]
    public void Compile_WithExtraValueAfterResultValue_LeavesTrailingValueAsWarning()
    {
        CoreTokenData coreToken = CreateCoreToken("edge_core", "Edge", AttackCoreType.Edge);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 2, spreadAngleStep: 10f);
        ValueTokenData behaviorValue = CreateValueToken("two", "2", 2f);
        ResultTokenData explosionToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 1f);
        ValueTokenData resultValue = CreateValueToken("five", "5", 5f);
        ValueTokenData trailingValue = CreateValueToken("three", "3", 3f);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            behaviorValue,
            explosionToken,
            resultValue,
            trailingValue,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.SpreadProjectileCount, Is.EqualTo(2));
        Assert.That(compiledAttack.ExplosionRadius, Is.EqualTo(5f));
        Assert.That(CountMessages(compiledAttack, AttackCompileMessageSeverity.Warning), Is.EqualTo(1));
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            firstResult,
            duplicateResult,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.ResultType, Is.EqualTo(AttackResultType.DirectDamage));
        Assert.That(compiledAttack.AttackSpec.maxLifetime, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithAcceptedPreAndPost_ModifiersParticipate()
    {
        TestPreTokenData preToken = CreatePreToken("pre", "Pre");
        preToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=0.5"),
        });

        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        TestPostTokenData postToken = CreatePostToken("post", "Post");
        postToken.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
        });

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            preToken,
            coreToken,
            resultToken,
            postToken,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.ScaleMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(compiledAttack.HasTextColorOverride, Is.True);
        Assert.That(compiledAttack.TextColor, Is.EqualTo(Color.red));
    }

    [Test]
    public void Compile_WithAcceptedLinkedItem_AppliesDamageMultiplier()
    {
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        LinkedTokenData linked = CreateLinkedToken("linked_fire_hit", 1.5f, coreToken, resultToken);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new PlaceableTokenData[]
        {
            linked,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.AttackSpec.damage, Is.EqualTo(1.5f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithPartiallyAcceptedLinkedItem_DoesNotApplyDamageMultiplier()
    {
        BehaviorTokenData behaviorToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 3, spreadAngleStep: 10f);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        LinkedTokenData linked = CreateLinkedToken("linked_invalid_prefix", 2f, behaviorToken, resultToken);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new PlaceableTokenData[]
        {
            linked,
            coreToken,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.AttackSpec.damage, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithMultipleAcceptedLinkedItems_MultipliesDamageInOrder()
    {
        TestPreTokenData preToken = CreatePreToken("pre", "Pre");
        LinkedTokenData preLinked = CreateLinkedToken("linked_pre", 1.5f, preToken);
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        ResultTokenData resultToken = CreateResultToken("direct", "Hit", AttackResultType.DirectDamage, acceptsNumericValue: false, defaultExplosionRadius: 0f);
        LinkedTokenData coreLinked = CreateLinkedToken("linked_core_result", 2f, coreToken, resultToken);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new PlaceableTokenData[]
        {
            preLinked,
            coreLinked,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        Assert.That(compiledAttack.AttackSpec.damage, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void Compile_WithoutCore_ReturnsHardFailure()
    {
        ValueTokenData valueToken = CreateValueToken("two", "2", 2f);
        ResultTokenData resultToken = CreateResultToken("explosion", "Boom", AttackResultType.Explosion, acceptsNumericValue: true, defaultExplosionRadius: 3f);

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            valueToken,
            resultToken,
        });

        Assert.That(compiledAttack.CanFire, Is.False);
        Assert.That(compiledAttack.HasErrors(), Is.True);
        Assert.That(CountMessages(compiledAttack, AttackCompileMessageSeverity.Error), Is.EqualTo(1));
    }

    [Test]
    public void Emit_WithSpreadAttack_SpawnsExpectedProjectileCount()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        BehaviorTokenData spreadToken = CreateBehaviorToken("spread", "Spread", AttackBehaviorType.Spread, acceptsNumericValue: true, defaultProjectileCount: 2, spreadAngleStep: 12f);
        ValueTokenData valueThree = CreateValueToken("three", "3", 3f);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            spreadToken,
            valueThree,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);

        CharBullet[] bullets = Object.FindObjectsByType<CharBullet>(FindObjectsSortMode.None);
        Assert.That(emittedCount, Is.EqualTo(3));
        Assert.That(bullets.Length, Is.EqualTo(4));
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);
        CharBullet emittedBullet = FindEmittedBullet(bulletPrefab);

        Assert.That(emittedCount, Is.EqualTo(1));
        Assert.That(emittedBullet, Is.Not.Null);
        Assert.That(emittedBullet.GlyphText.color, Is.EqualTo(Color.red));
    }

    [Test]
    public void Emit_WithBulletTextOverride_AppliesGlyphText()
    {
        CharBullet bulletPrefab = CreateBulletPrefab();
        GameObject owner = CreateGameObject("Player");
        CoreTokenData coreToken = CreateCoreToken("fire_core", "Fire", AttackCoreType.Fire);
        coreToken.SetBulletTextOverride(true, "Ignis");

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);
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

        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
        });

        int emittedCount = AttackProjectileEmitter.Emit(bulletPrefab, owner.transform, Vector3.zero, Vector3.forward, compiledAttack);
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
        ValueTokenData radiusValue = CreateValueToken("two", "2", 2f);
        CompiledAttack compiledAttack = AttackFormulaCompiler.Compile(new BaseTokenData[]
        {
            coreToken,
            explosionToken,
            radiusValue,
        });

        Assert.That(compiledAttack.CanFire, Is.True);
        bullet.InitializeShot(owner.transform, Vector3.zero, Vector3.forward, compiledAttack.AttackSpec, compiledAttack);
        Physics.SyncTransforms();

        MethodInfo tryRegisterImpact = typeof(CharBullet).GetMethod("TryRegisterImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(tryRegisterImpact, Is.Not.Null);

        BoxCollider primaryCollider = primaryEnemy.GetComponent<BoxCollider>();
        bool handled = (bool)tryRegisterImpact.Invoke(bullet, new object[] { primaryCollider });

        Assert.That(handled, Is.True);
        Assert.That(primaryEnemy.CurrentHealth, Is.EqualTo(3f));
        Assert.That(secondaryEnemy.CurrentHealth, Is.EqualTo(4f));
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
        return token;
    }

    private BehaviorTokenData CreateBehaviorToken(string tokenId, string displayText, AttackBehaviorType behaviorType, bool acceptsNumericValue, int defaultProjectileCount, float spreadAngleStep)
    {
        BehaviorTokenData token = CreateAssetInstance<BehaviorTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.BehaviorType = behaviorType;
        token.AcceptsNumericValue = acceptsNumericValue;
        token.DefaultProjectileCount = defaultProjectileCount;
        token.SpreadAngleStep = spreadAngleStep;
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
        return token;
    }

    private ValueTokenData CreateValueToken(string tokenId, string displayText, float numericValue)
    {
        ValueTokenData token = CreateAssetInstance<ValueTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.NumericValue = numericValue;
        return token;
    }

    private TestPostTokenData CreatePostToken(string tokenId, string displayText)
    {
        TestPostTokenData token = CreateAssetInstance<TestPostTokenData>($"{tokenId}_asset");
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        return token;
    }

    private TestPreTokenData CreatePreToken(string tokenId, string displayText)
    {
        TestPreTokenData token = CreateAssetInstance<TestPreTokenData>($"{tokenId}_asset");
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

    private static int CountMessages(CompiledAttack compiledAttack, AttackCompileMessageSeverity severity)
    {
        int count = 0;
        for (int i = 0; i < compiledAttack.Messages.Count; i++)
        {
            if (compiledAttack.Messages[i].severity == severity)
            {
                count++;
            }
        }

        return count;
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

    private sealed class TestPreTokenData : PreTokenData
    {
    }

    private sealed class TestPostTokenData : PostTokenData
    {
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

        public override float AttackRange => throw new System.NotImplementedException();

        public override float AttackCooldown => throw new System.NotImplementedException();

        public override float AttackDamage => throw new System.NotImplementedException();

        public void SetHealth(float health)
        {
            maxHealth = Mathf.Max(0f, health);
            currentHealth = maxHealth;
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
    }
}
