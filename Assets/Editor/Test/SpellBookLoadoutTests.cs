using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SpellBookLoadoutTests
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
    public void SpellBookData_BuildExecutionItems_ComposesFixedItemsAndTrimsEquippedSlots()
    {
        CoreTokenData fixedCore = CreateCoreToken("fixed_fire", "火", AttackCoreType.Fire);
        BehaviorTokenData spread = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2, 12f);
        ValueTokenData valueThree = CreateValueToken("value_three", "三", 3f);
        ResultTokenData extraResult = CreateResultToken("direct", "击", AttackResultType.DirectDamage, false, 0f);
        LinkedTokenData twoSlotLinked = CreateLinkedToken("spread_three", 1f, spread, valueThree);
        SpellBookData spellBook = CreateSpellBook("apprentice", slotCount: 2, castCooldownSeconds: 0.25f);
        spellBook.FixedItemPlacement = SpellBookFixedItemPlacement.BeforeEquipped;
        spellBook.SetFixedCastItems(new PlaceableTokenData[] { fixedCore });

        List<PlaceableTokenData> executionItems = spellBook.BuildExecutionItems(new PlaceableTokenData[]
        {
            twoSlotLinked,
            extraResult,
        });

        Assert.That(executionItems.Count, Is.EqualTo(2));
        Assert.That(executionItems[0], Is.SameAs(fixedCore));
        Assert.That(executionItems[1], Is.SameAs(twoSlotLinked));
        Assert.That(spellBook.SlotCount, Is.EqualTo(2));
        Assert.That(spellBook.CastCooldownSeconds, Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void SpellBookLoadout_CompilesFixedCoreWithEquippedBehavior()
    {
        GameObject owner = CreateGameObject("SpellBookOwner");
        SpellBookLoadout loadout = owner.AddComponent<SpellBookLoadout>();
        CoreTokenData fixedCore = CreateCoreToken("fixed_fire", "火", AttackCoreType.Fire);
        BehaviorTokenData spread = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2, 12f);
        ValueTokenData valueThree = CreateValueToken("value_three", "三", 3f);
        SpellBookData spellBook = CreateSpellBook("apprentice", slotCount: 2, castCooldownSeconds: 0.45f);
        spellBook.SetFixedCastItems(new PlaceableTokenData[] { fixedCore });

        loadout.SetSpellBook(spellBook);
        loadout.SetTokens(new BaseTokenData[] { spread, valueThree });

        bool canCast = loadout.TryGetCompiledProgram(out CompiledSpellProgram compiledProgram);
        SpellProjectileNode projectile = compiledProgram.PrimaryCastBlock.Projectiles[0];

        Assert.That(loadout.SlotCount, Is.EqualTo(2));
        Assert.That(loadout.CastCooldownSeconds, Is.EqualTo(0.45f).Within(0.0001f));
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(2));
        Assert.That(loadout.ExecutionItems.Count, Is.EqualTo(3));
        Assert.That(loadout.Tokens.Count, Is.EqualTo(3));
        Assert.That(canCast, Is.True);
        Assert.That(compiledProgram, Is.SameAs(loadout.CurrentCompiledProgram));
        Assert.That(compiledProgram.CastBlocks.Count, Is.EqualTo(1));
        Assert.That(compiledProgram.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(1));
        Assert.That(projectile.ProjectileCount, Is.EqualTo(3));
        Assert.That(projectile.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(projectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Spread));
    }

    [Test]
    public void SpellBookLoadout_SwitchingBooksRebuildsSlotLimitAndRevision()
    {
        GameObject owner = CreateGameObject("SpellBookOwner");
        SpellBookLoadout loadout = owner.AddComponent<SpellBookLoadout>();
        SpellBookData wideBook = CreateSpellBook("wide", slotCount: 3, castCooldownSeconds: 0.6f);
        SpellBookData quickBook = CreateSpellBook("quick", slotCount: 1, castCooldownSeconds: 0.1f);
        CoreTokenData fireCore = CreateCoreToken("fire_core", "火", AttackCoreType.Fire);
        BehaviorTokenData spread = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2, 12f);
        ValueTokenData valueThree = CreateValueToken("value_three", "三", 3f);
        int changedCount = 0;
        loadout.Changed += () => changedCount++;

        loadout.SetSpellBook(wideBook);
        loadout.SetTokens(new BaseTokenData[] { fireCore, spread, valueThree });
        int wideRevision = loadout.Revision;
        SpellProjectileNode wideProjectile = loadout.CurrentCompiledProgram.PrimaryCastBlock.Projectiles[0];

        loadout.SetSpellBook(quickBook);
        SpellProjectileNode quickProjectile = loadout.CurrentCompiledProgram.PrimaryCastBlock.Projectiles[0];

        Assert.That(wideProjectile.CanFire, Is.True);
        Assert.That(wideProjectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Spread));
        Assert.That(wideProjectile.ProjectileCount, Is.EqualTo(3));
        Assert.That(loadout.SlotCount, Is.EqualTo(1));
        Assert.That(loadout.CastCooldownSeconds, Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.ExecutionItems.Count, Is.EqualTo(1));
        Assert.That(loadout.Revision, Is.GreaterThan(wideRevision));
        Assert.That(changedCount, Is.EqualTo(3));
        Assert.That(quickProjectile.CanFire, Is.True);
        Assert.That(quickProjectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Straight));
        Assert.That(quickProjectile.ProjectileCount, Is.EqualTo(1));
    }

    [Test]
    public void SpellBookLoadout_ResetToStartingItems_RestoresCapturedEquippedItems()
    {
        GameObject owner = CreateGameObject("SpellBookOwner");
        SpellBookLoadout loadout = owner.AddComponent<SpellBookLoadout>();
        CoreTokenData startingCore = CreateCoreToken("starting_fire", "火", AttackCoreType.Fire);
        CoreTokenData combatCore = CreateCoreToken("combat_ice", "冰", AttackCoreType.Ice);
        SetInstanceField(loadout, "equippedItems", new List<PlaceableTokenData> { startingCore });
        FindInstanceMethod(typeof(SpellBookLoadout), "Awake").Invoke(loadout, null);

        loadout.SetItems(new PlaceableTokenData[] { combatCore });
        loadout.ResetToStartingItems();

        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.EquippedItems[0], Is.SameAs(startingCore));
        Assert.That(loadout.CurrentCompiledProgram.PrimaryCastBlock.Projectiles[0].CoreType, Is.EqualTo(AttackCoreType.Fire));
    }

    [Test]
    public void PlayerPlaneMovement_TryResolveSpellProgramForFiring_UsesSpellBookLoadout()
    {
        GameObject player = CreateGameObject("Player");
        PlayerPlaneMovement movement = player.AddComponent<PlayerPlaneMovement>();
        SpellBookLoadout spellBookLoadout = player.AddComponent<SpellBookLoadout>();

        CoreTokenData fixedCore = CreateCoreToken("fixed_fire", "火", AttackCoreType.Fire);
        BehaviorTokenData spread = CreateBehaviorToken("spread", "散", AttackBehaviorType.Spread, true, 2, 12f);
        ValueTokenData valueThree = CreateValueToken("value_three", "三", 3f);
        SpellBookData spellBook = CreateSpellBook("apprentice", slotCount: 2, castCooldownSeconds: 0.25f);
        spellBook.SetFixedCastItems(new PlaceableTokenData[] { fixedCore });
        spellBookLoadout.SetSpellBook(spellBook);
        spellBookLoadout.SetTokens(new BaseTokenData[] { spread, valueThree });

        MethodInfo resolveMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "TryResolveSpellProgramForFiring");
        object[] args = { null };
        bool resolved = (bool)resolveMethod.Invoke(movement, args);
        CompiledSpellProgram resolvedProgram = (CompiledSpellProgram)args[0];

        Assert.That(resolved, Is.True);
        Assert.That(resolvedProgram, Is.Not.Null);
        Assert.That(resolvedProgram.CanCast, Is.True);
        Assert.That(resolvedProgram.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(1));
        SpellProjectileNode projectile = resolvedProgram.PrimaryCastBlock.Projectiles[0];
        Assert.That(projectile.CoreType, Is.EqualTo(AttackCoreType.Fire));
        Assert.That(projectile.BehaviorType, Is.EqualTo(AttackBehaviorType.Spread));
        Assert.That(projectile.ProjectileCount, Is.EqualTo(3));
    }

    [Test]
    public void SpellBookActivationTraits_FlowIntoLoadoutAndPlayerFirePacing()
    {
        GameObject player = CreateGameObject("Player");
        PlayerPlaneMovement movement = player.AddComponent<PlayerPlaneMovement>();
        SpellBookLoadout spellBookLoadout = player.AddComponent<SpellBookLoadout>();
        SpellBookData spellBook = CreateSpellBook("wide", slotCount: 7, castCooldownSeconds: 0.35f);
        spellBook.CastsPerActivation = 3;
        spellBook.ActivationSpreadAngleStep = 9f;

        spellBookLoadout.SetSpellBook(spellBook);

        float resolvedFireInterval = (float)FindInstanceMethod(typeof(PlayerPlaneMovement), "ResolveCurrentFireInterval").Invoke(movement, null);
        int resolvedCastCount = (int)FindInstanceMethod(typeof(PlayerPlaneMovement), "ResolveCurrentActivationCastCount").Invoke(movement, null);
        float resolvedActivationSpread = (float)FindInstanceMethod(typeof(PlayerPlaneMovement), "ResolveCurrentActivationSpreadAngleStep").Invoke(movement, null);

        Assert.That(spellBookLoadout.CastCooldownSeconds, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(spellBookLoadout.CastsPerActivation, Is.EqualTo(3));
        Assert.That(spellBookLoadout.ActivationSpreadAngleStep, Is.EqualTo(9f).Within(0.0001f));
        Assert.That(resolvedFireInterval, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(resolvedCastCount, Is.EqualTo(3));
        Assert.That(resolvedActivationSpread, Is.EqualTo(9f).Within(0.0001f));
    }

    [Test]
    public void SpellBookLoadout_ActivationEnergy_GatesAndRegeneratesCasts()
    {
        GameObject owner = CreateGameObject("SpellBookOwner");
        SpellBookLoadout loadout = owner.AddComponent<SpellBookLoadout>();
        SpellBookData spellBook = CreateSpellBook("surge", slotCount: 5, castCooldownSeconds: 0.18f);
        spellBook.EnergyCapacity = 2f;
        spellBook.EnergyCostPerActivation = 1f;
        spellBook.EnergyRegenPerSecond = 0.5f;

        loadout.SetSpellBook(spellBook);
        loadout.RefillActivationEnergy(0f);

        Assert.That(loadout.UsesEnergy, Is.True);
        Assert.That(loadout.CurrentEnergy, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(loadout.TryConsumeActivationEnergy(0f), Is.True);
        Assert.That(loadout.CurrentEnergy, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(loadout.TryConsumeActivationEnergy(0f), Is.True);
        Assert.That(loadout.CurrentEnergy, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(loadout.HasActivationEnergy(1f), Is.False);
        Assert.That(loadout.CurrentEnergy, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(loadout.HasActivationEnergy(2f), Is.True);
        Assert.That(loadout.TryConsumeActivationEnergy(2f), Is.True);
        Assert.That(loadout.CurrentEnergy, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void PlayerPlaneMovement_ActivationEnergy_UsesSpellBookLoadoutGate()
    {
        GameObject player = CreateGameObject("Player");
        PlayerPlaneMovement movement = player.AddComponent<PlayerPlaneMovement>();
        SpellBookLoadout loadout = player.AddComponent<SpellBookLoadout>();
        SpellBookData spellBook = CreateSpellBook("surge", slotCount: 5, castCooldownSeconds: 0.18f);
        spellBook.EnergyCapacity = 1f;
        spellBook.EnergyCostPerActivation = 1f;
        spellBook.EnergyRegenPerSecond = 0f;

        loadout.SetSpellBook(spellBook);
        loadout.RefillActivationEnergy(0f);

        MethodInfo hasEnergyMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "HasActivationEnergyForFiring");
        MethodInfo consumeEnergyMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "TryConsumeActivationEnergyForFiring");

        Assert.That((bool)hasEnergyMethod.Invoke(movement, new object[] { 0f }), Is.True);
        Assert.That((bool)consumeEnergyMethod.Invoke(movement, new object[] { 0f }), Is.True);
        Assert.That(loadout.CurrentEnergy, Is.EqualTo(0f).Within(0.0001f));
        Assert.That((bool)hasEnergyMethod.Invoke(movement, new object[] { 0f }), Is.False);
        Assert.That((bool)consumeEnergyMethod.Invoke(movement, new object[] { 0f }), Is.False);
    }

    [Test]
    public void PlayerPlaneMovement_CastRuntimeModifiers_AdjustFirePacingEnergyAndHealthCost()
    {
        GameObject player = CreateGameObject("Player");
        PlayerHealth health = player.AddComponent<PlayerHealth>();
        PlayerPlaneMovement movement = player.AddComponent<PlayerPlaneMovement>();
        SpellBookLoadout loadout = player.AddComponent<SpellBookLoadout>();
        SpellBookData spellBook = CreateSpellBook("surge", slotCount: 5, castCooldownSeconds: 0.5f);
        spellBook.EnergyCapacity = 2f;
        spellBook.EnergyCostPerActivation = 1f;
        spellBook.EnergyRegenPerSecond = 0f;
        CoreTokenData fire = CreateCoreToken("fire", "火", AttackCoreType.Fire);
        ModifierTokenData urgent = CreateModifierToken("urgent", "急");
        urgent.SetModifiers(new[] { new TokenModifierDefinition(TokenModifierTarget.CastCooldownMultiplier, "*=0.8") });
        ModifierTokenData source = CreateModifierToken("source", "源");
        source.SetModifiers(new[] { new TokenModifierDefinition(TokenModifierTarget.EnergyCostMultiplier, "*=0.5") });
        ModifierTokenData wild = CreateModifierToken("wild", "狂");
        wild.SetModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.CasterHealthCost, "+=5"),
            new TokenModifierDefinition(TokenModifierTarget.EnergyCostMultiplier, "*=1.5"),
        });

        loadout.SetSpellBook(spellBook);
        loadout.SetTokens(new BaseTokenData[] { fire, urgent, source, wild });
        loadout.RefillActivationEnergy(0f);

        MethodInfo resolveMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "TryResolveSpellProgramForFiring");
        MethodInfo intervalMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "ResolveCurrentFireInterval");
        MethodInfo hasEnergyMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "HasActivationEnergyForFiring");
        MethodInfo consumeEnergyMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "TryConsumeActivationEnergyForFiring");
        MethodInfo healthCostMethod = FindInstanceMethod(typeof(PlayerPlaneMovement), "TryApplyCasterHealthCost");

        object[] args = { null };
        Assert.That((bool)resolveMethod.Invoke(movement, args), Is.True);
        Assert.That((float)intervalMethod.Invoke(movement, null), Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That((bool)hasEnergyMethod.Invoke(movement, new object[] { 0f }), Is.True);
        Assert.That((bool)consumeEnergyMethod.Invoke(movement, new object[] { 0f }), Is.True);
        Assert.That(loadout.CurrentEnergy, Is.EqualTo(1.25f).Within(0.0001f));

        healthCostMethod.Invoke(movement, new object[] { 5f });
        Assert.That(health.CurrentHealth, Is.EqualTo(95f).Within(0.0001f));
    }

    [Test]
    public void SpellBookLoadout_ExecutorModifiers_ApplyWithoutConsumingSlots()
    {
        GameObject owner = CreateGameObject("SpellBookOwner");
        SpellBookLoadout loadout = owner.AddComponent<SpellBookLoadout>();
        CoreTokenData fireCore = CreateCoreToken("fire", "火", AttackCoreType.Fire);
        fireCore.Damage = 6f;
        fireCore.ProjectileSpeed = 100f;
        SpellBookData spellBook = CreateSpellBook("quick", slotCount: 1, castCooldownSeconds: 0.14f);
        spellBook.SetExecutorModifiers(new[]
        {
            new TokenModifierDefinition(TokenModifierTarget.Damage, "*=0.5"),
            new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "*=2"),
        });

        loadout.SetSpellBook(spellBook);
        loadout.SetTokens(new BaseTokenData[] { fireCore });

        SpellProjectileNode projectile = loadout.CurrentCompiledProgram.PrimaryCastBlock.Projectiles[0];

        Assert.That(spellBook.HasExecutorModifiers, Is.True);
        Assert.That(spellBook.ExecutorModifiers.Count, Is.EqualTo(2));
        Assert.That(spellBook.GetSelectionDescription(), Does.Contain("内建强化 2 项"));
        Assert.That(loadout.EquippedItems.Count, Is.EqualTo(1));
        Assert.That(loadout.ExecutionItems.Count, Is.EqualTo(1));
        Assert.That(projectile.AttackSpec.damage, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(projectile.AttackSpec.projectileSpeed, Is.EqualTo(200f).Within(0.0001f));
    }

    private SpellBookData CreateSpellBook(string spellBookId, int slotCount, float castCooldownSeconds)
    {
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        createdObjects.Add(spellBook);
        spellBook.SpellBookId = spellBookId;
        spellBook.DisplayName = spellBookId;
        spellBook.SlotCount = slotCount;
        spellBook.CastCooldownSeconds = castCooldownSeconds;
        spellBook.CastsPerActivation = 1;
        return spellBook;
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
        return token;
    }

    private ValueTokenData CreateValueToken(string tokenId, string displayText, float numericValue)
    {
        ValueTokenData token = CreateToken<ValueTokenData>(tokenId, displayText);
        token.NumericValue = numericValue;
        return token;
    }

    private ModifierTokenData CreateModifierToken(string tokenId, string displayText)
    {
        return CreateToken<ModifierTokenData>(tokenId, displayText);
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

    private LinkedTokenData CreateLinkedToken(string itemId, float damageMultiplier, params BaseTokenData[] linkedTokens)
    {
        LinkedTokenData token = ScriptableObject.CreateInstance<LinkedTokenData>();
        createdObjects.Add(token);
        token.ItemId = itemId;
        token.ConfiguredDamageMultiplier = damageMultiplier;
        token.SetLinkedTokens(linkedTokens);
        return token;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static MethodInfo FindInstanceMethod(Type type, string methodName)
    {
        while (type != null)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            MethodInfo fallback = null;
            MethodInfo oneParameterFallback = null;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                fallback ??= method;
                int parameterCount = method.GetParameters().Length;
                if (parameterCount == 0)
                {
                    return method;
                }

                if (parameterCount == 1)
                {
                    oneParameterFallback ??= method;
                }
            }

            if (oneParameterFallback != null)
            {
                return oneParameterFallback;
            }

            if (fallback != null)
            {
                return fallback;
            }

            type = type.BaseType;
        }

        Assert.Fail($"Missing private method '{methodName}'.");
        return null;
    }

    private static void SetInstanceField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
