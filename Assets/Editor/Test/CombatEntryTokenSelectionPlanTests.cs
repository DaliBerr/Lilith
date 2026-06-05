using System.Collections.Generic;
using System.Text.RegularExpressions;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

public sealed class CombatEntryTokenSelectionPlanTests
{
    private const string Plan2Path = "Assets/Data/BulletTokens/SelectionPlans/Plan2.asset";
    private const string SpellProgramLibraryPath = "Assets/Data/BulletTokens/TokenLib/SpellProgram_Token_Lib.asset";
    private const string SpellBookRewardLibraryPath = "Assets/Data/SpellBooks/SpellBookReward_Lib.asset";
    private const string QuickSpellBookPath = "Assets/Data/SpellBooks/QuickSpellBook.asset";
    private const string WideSpellBookPath = "Assets/Data/SpellBooks/WideSpellBook.asset";
    private const string TriggerSpellBookPath = "Assets/Data/SpellBooks/TriggerSpellBook.asset";
    private const string SurgeSpellBookPath = "Assets/Data/SpellBooks/SurgeSpellBook.asset";
    private const string BindingSpellBookPath = "Assets/Data/SpellBooks/BindingSpellBook.asset";
    private const string FireCorePath = "Assets/Data/BulletTokens/Core/FireCore.asset";
    private const string PayloadAmplifyModifierPath = "Assets/Data/BulletTokens/Modifier/PayloadAmplifyModifier.asset";
    private const string PayloadRadiusModifierPath = "Assets/Data/BulletTokens/Modifier/PayloadRadiusModifier.asset";
    private const string PayloadCountModifierPath = "Assets/Data/BulletTokens/Modifier/PayloadCountModifier.asset";
    private const string PayloadControlFieldModifierPath = "Assets/Data/BulletTokens/Modifier/PayloadControlFieldModifier.asset";
    private const string OnHitTriggerPath = "Assets/Data/BulletTokens/Trigger/OnHitTrigger.asset";
    private const string ExplosionPath = "Assets/Data/BulletTokens/Result/Explosion.asset";
    private const string SplitPath = "Assets/Data/BulletTokens/Result/Split.asset";
    private const string ControlPath = "Assets/Data/BulletTokens/Result/Control.asset";
    private const string HealingPath = "Assets/Data/BulletTokens/Result/Healing.asset";
    private const string Value3Path = "Assets/Data/BulletTokens/Value/Value_3.asset";

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
    public void TrySampleLibrary_UsesWeightedLibrariesWithinPlan()
    {
        CombatEntryTokenSelectionPlan plan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(plan);

        BulletTokenLibrary weightedLibrary = CreateLibrary("WeightedLibrary", CreateToken<CoreTokenData>("fire", "Fire"));
        BulletTokenLibrary zeroWeightLibrary = CreateLibrary("ZeroWeightLibrary", CreateToken<CoreTokenData>("ice", "Ice"));

        plan.AddLibrary(weightedLibrary, 1f);
        plan.AddLibrary(zeroWeightLibrary, 0f);

        for (int i = 0; i < 8; i++)
        {
            bool success = plan.TrySampleLibrary(new VocalithRandom(1000 + i), out BulletTokenLibrary sampledLibrary);
            Assert.That(success, Is.True);
            Assert.That(sampledLibrary, Is.SameAs(weightedLibrary));
        }
    }

    [Test]
    public void TrySampleLibrary_ReturnsFalseWhenAllWeightsAreZero()
    {
        CombatEntryTokenSelectionPlan plan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(plan);

        BulletTokenLibrary zeroWeightLibrary = CreateLibrary("ZeroWeightLibrary", CreateToken<CoreTokenData>("ice", "Ice"));
        plan.AddLibrary(zeroWeightLibrary, 0f);

        bool success = plan.TrySampleLibrary(new VocalithRandom(24680), out BulletTokenLibrary sampledLibrary);

        Assert.That(success, Is.False);
        Assert.That(sampledLibrary, Is.Null);
    }

    [Test]
    public void TrySampleRewardLibrary_UsesWeightedSpellBookLibrariesWithinPlan()
    {
        CombatEntryTokenSelectionPlan plan = ScriptableObject.CreateInstance<CombatEntryTokenSelectionPlan>();
        createdObjects.Add(plan);

        BulletTokenLibrary zeroTokenLibrary = CreateLibrary("ZeroTokenLibrary", CreateToken<CoreTokenData>("fire", "Fire"));
        SpellBookRewardLibrary weightedSpellBookLibrary = CreateSpellBookLibrary(
            "WeightedSpellBookLibrary",
            CreateSpellBook("wide", "Wide Spellbook", 7, 0.4f));
        SpellBookRewardLibrary zeroSpellBookLibrary = CreateSpellBookLibrary(
            "ZeroSpellBookLibrary",
            CreateSpellBook("quick", "Quick Spellbook", 4, 0.12f));

        plan.AddLibrary(zeroTokenLibrary, 0f);
        plan.AddSpellBookLibrary(weightedSpellBookLibrary, 1f);
        plan.AddSpellBookLibrary(zeroSpellBookLibrary, 0f);

        for (int i = 0; i < 8; i++)
        {
            bool success = plan.TrySampleRewardLibrary(
                new VocalithRandom(2000 + i),
                out BulletTokenLibrary sampledTokenLibrary,
                out SpellBookRewardLibrary sampledSpellBookLibrary);

            Assert.That(success, Is.True);
            Assert.That(sampledTokenLibrary, Is.Null);
            Assert.That(sampledSpellBookLibrary, Is.SameAs(weightedSpellBookLibrary));
        }
    }

    [Test]
    public void Plan2Asset_IncludesSpellProgramTokenLibraryWithNewTokenTypes()
    {
        CombatEntryTokenSelectionPlan plan = AssetDatabase.LoadAssetAtPath<CombatEntryTokenSelectionPlan>(Plan2Path);
        BulletTokenLibrary spellProgramLibrary = AssetDatabase.LoadAssetAtPath<BulletTokenLibrary>(SpellProgramLibraryPath);
        ModifierTokenData payloadAmplify = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadAmplifyModifierPath);
        ModifierTokenData payloadRadius = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadRadiusModifierPath);
        ModifierTokenData payloadCount = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadCountModifierPath);
        ModifierTokenData payloadControlField = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadControlFieldModifierPath);

        Assert.That(plan, Is.Not.Null);
        Assert.That(spellProgramLibrary, Is.Not.Null);
        Assert.That(payloadAmplify, Is.Not.Null);
        Assert.That(payloadRadius, Is.Not.Null);
        Assert.That(payloadCount, Is.Not.Null);
        Assert.That(payloadControlField, Is.Not.Null);
        Assert.That(PlanContainsLibrary(plan, spellProgramLibrary), Is.True);
        Assert.That(TokenListContains(spellProgramLibrary.GetTokens(), payloadAmplify), Is.True);
        Assert.That(TokenListContains(spellProgramLibrary.GetTokens(), payloadRadius), Is.True);
        Assert.That(TokenListContains(spellProgramLibrary.GetTokens(), payloadCount), Is.True);
        Assert.That(TokenListContains(spellProgramLibrary.GetTokens(), payloadControlField), Is.True);
        AssertPayloadAmplifyModifier(payloadAmplify);
        AssertPayloadRadiusModifier(payloadRadius);
        AssertPayloadCountModifier(payloadCount);
        AssertPayloadControlFieldModifier(payloadControlField);

        AssertTokenTypesInclude(
            spellProgramLibrary.GetTokens(),
            TokenType.Core,
            TokenType.Value,
            TokenType.Modifier,
            TokenType.Multicast,
            TokenType.Trigger);

        List<PlaceableTokenData> sampledTokens = spellProgramLibrary.SampleChoices(
            new VocalithRandom(123),
            desiredCount: spellProgramLibrary.GetTokens().Count);
        AssertTokenTypesInclude(
            sampledTokens,
            TokenType.Core,
            TokenType.Value,
            TokenType.Modifier,
            TokenType.Multicast,
            TokenType.Trigger);
    }

    [Test]
    public void Plan2Asset_KeepsFirstPassRewardBalanceWeights()
    {
        CombatEntryTokenSelectionPlan plan = AssetDatabase.LoadAssetAtPath<CombatEntryTokenSelectionPlan>(Plan2Path);
        BulletTokenLibrary spellProgramLibrary = AssetDatabase.LoadAssetAtPath<BulletTokenLibrary>(SpellProgramLibraryPath);
        SpellBookRewardLibrary spellBookLibrary = AssetDatabase.LoadAssetAtPath<SpellBookRewardLibrary>(SpellBookRewardLibraryPath);
        SpellBookData quickBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(QuickSpellBookPath);
        SpellBookData wideBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(WideSpellBookPath);
        SpellBookData triggerBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(TriggerSpellBookPath);
        SpellBookData surgeBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(SurgeSpellBookPath);
        SpellBookData bindingBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(BindingSpellBookPath);
        ModifierTokenData payloadAmplify = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadAmplifyModifierPath);
        ModifierTokenData payloadRadius = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadRadiusModifierPath);
        ModifierTokenData payloadCount = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadCountModifierPath);
        ModifierTokenData payloadControlField = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadControlFieldModifierPath);
        TriggerTokenData onHitTrigger = AssetDatabase.LoadAssetAtPath<TriggerTokenData>(OnHitTriggerPath);

        Assert.That(plan, Is.Not.Null);
        Assert.That(spellProgramLibrary, Is.Not.Null);
        Assert.That(spellBookLibrary, Is.Not.Null);
        Assert.That(quickBook, Is.Not.Null);
        Assert.That(wideBook, Is.Not.Null);
        Assert.That(triggerBook, Is.Not.Null);
        Assert.That(surgeBook, Is.Not.Null);
        Assert.That(bindingBook, Is.Not.Null);
        Assert.That(payloadAmplify, Is.Not.Null);
        Assert.That(payloadRadius, Is.Not.Null);
        Assert.That(payloadCount, Is.Not.Null);
        Assert.That(payloadControlField, Is.Not.Null);
        Assert.That(onHitTrigger, Is.Not.Null);

        float spellProgramSourceWeight = FindPlanLibraryWeight(plan, spellProgramLibrary);
        float spellBookSourceWeight = FindPlanSpellBookLibraryWeight(plan, spellBookLibrary);
        float totalRewardSourceWeight = SumPlanRewardSourceWeights(plan);

        Assert.That(spellProgramSourceWeight, Is.EqualTo(0.45f).Within(0.0001f));
        Assert.That(spellBookSourceWeight, Is.EqualTo(0.35f).Within(0.0001f));
        Assert.That(totalRewardSourceWeight, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(spellBookSourceWeight / totalRewardSourceWeight, Is.InRange(0.1f, 0.15f));
        Assert.That(spellBookSourceWeight, Is.LessThan(spellProgramSourceWeight));

        Assert.That(spellProgramLibrary.GetTokenWeight(payloadAmplify), Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(spellProgramLibrary.GetTokenWeight(payloadRadius), Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(spellProgramLibrary.GetTokenWeight(payloadCount), Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(spellProgramLibrary.GetTokenWeight(payloadControlField), Is.EqualTo(0.65f).Within(0.0001f));
        Assert.That(spellProgramLibrary.GetTokenWeight(onHitTrigger), Is.EqualTo(0.7f).Within(0.0001f));

        Assert.That(FindSpellBookWeight(spellBookLibrary, quickBook), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(FindSpellBookWeight(spellBookLibrary, wideBook), Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That(FindSpellBookWeight(spellBookLibrary, triggerBook), Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(FindSpellBookWeight(spellBookLibrary, bindingBook), Is.EqualTo(0.7f).Within(0.0001f));
        Assert.That(FindSpellBookWeight(spellBookLibrary, surgeBook), Is.EqualTo(0.65f).Within(0.0001f));
        Assert.That(
            FindSpellBookWeight(spellBookLibrary, surgeBook),
            Is.LessThan(FindSpellBookWeight(spellBookLibrary, quickBook)));
        Assert.That(
            FindSpellBookWeight(spellBookLibrary, bindingBook),
            Is.LessThan(FindSpellBookWeight(spellBookLibrary, wideBook)));
    }

    [Test]
    public void PayloadAmplifyModifierAsset_AmplifiesCurrentPayloadResultOnlyEffects()
    {
        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(FireCorePath);
        TriggerTokenData onHitTrigger = AssetDatabase.LoadAssetAtPath<TriggerTokenData>(OnHitTriggerPath);
        ModifierTokenData payloadAmplify = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadAmplifyModifierPath);
        ResultTokenData explosion = AssetDatabase.LoadAssetAtPath<ResultTokenData>(ExplosionPath);

        Assert.That(fireCore, Is.Not.Null);
        Assert.That(onHitTrigger, Is.Not.Null);
        Assert.That(payloadAmplify, Is.Not.Null);
        Assert.That(explosion, Is.Not.Null);
        AssertPayloadAmplifyModifier(payloadAmplify);

        CompiledSpellProgram baseProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            explosion,
        });
        CompiledSpellProgram amplifiedProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            payloadAmplify,
            explosion,
        });

        Assert.That(baseProgram.CanCast, Is.True);
        Assert.That(amplifiedProgram.CanCast, Is.True);
        Assert.That(baseProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(amplifiedProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        Assert.That(amplifiedProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));

        ResultEffectPayload basePayload = baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        ResultEffectPayload amplifiedPayload = amplifiedProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        Assert.That(amplifiedPayload.explosionRadius, Is.EqualTo(basePayload.explosionRadius).Within(0.0001f));
        Assert.That(
            amplifiedPayload.explosionDamageMultiplier,
            Is.EqualTo(basePayload.explosionDamageMultiplier * 1.5f).Within(0.0001f));
    }

    [Test]
    public void PayloadRadiusModifierAsset_ExpandsCurrentPayloadResultOnlyRadius()
    {
        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(FireCorePath);
        TriggerTokenData onHitTrigger = AssetDatabase.LoadAssetAtPath<TriggerTokenData>(OnHitTriggerPath);
        ModifierTokenData payloadRadius = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadRadiusModifierPath);
        ResultTokenData explosion = AssetDatabase.LoadAssetAtPath<ResultTokenData>(ExplosionPath);

        Assert.That(fireCore, Is.Not.Null);
        Assert.That(onHitTrigger, Is.Not.Null);
        Assert.That(payloadRadius, Is.Not.Null);
        Assert.That(explosion, Is.Not.Null);
        AssertPayloadRadiusModifier(payloadRadius);

        CompiledSpellProgram baseProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            explosion,
        });
        CompiledSpellProgram expandedProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            payloadRadius,
            explosion,
        });

        Assert.That(baseProgram.CanCast, Is.True);
        Assert.That(expandedProgram.CanCast, Is.True);
        Assert.That(baseProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(expandedProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        Assert.That(expandedProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));

        ResultEffectPayload basePayload = baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        ResultEffectPayload expandedPayload = expandedProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        Assert.That(expandedPayload.explosionRadius, Is.EqualTo(basePayload.explosionRadius * 1.35f).Within(0.0001f));
        Assert.That(
            expandedPayload.explosionDamageMultiplier,
            Is.EqualTo(basePayload.explosionDamageMultiplier).Within(0.0001f));
    }

    [Test]
    public void PayloadCountModifierAsset_IncreasesCurrentPayloadResultOnlySplitCount()
    {
        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(FireCorePath);
        TriggerTokenData onHitTrigger = AssetDatabase.LoadAssetAtPath<TriggerTokenData>(OnHitTriggerPath);
        ModifierTokenData payloadCount = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadCountModifierPath);
        ResultTokenData split = AssetDatabase.LoadAssetAtPath<ResultTokenData>(SplitPath);

        Assert.That(fireCore, Is.Not.Null);
        Assert.That(onHitTrigger, Is.Not.Null);
        Assert.That(payloadCount, Is.Not.Null);
        Assert.That(split, Is.Not.Null);
        AssertPayloadCountModifier(payloadCount);

        CompiledSpellProgram baseProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            split,
        });
        CompiledSpellProgram countedProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            payloadCount,
            split,
        });

        Assert.That(baseProgram.CanCast, Is.True);
        Assert.That(countedProgram.CanCast, Is.True);
        Assert.That(baseProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(countedProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        Assert.That(countedProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));

        ResultEffectPayload basePayload = baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        ResultEffectPayload countedPayload = countedProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        Assert.That(countedPayload.splitProjectileCount, Is.EqualTo(basePayload.splitProjectileCount + 2));
        Assert.That(
            countedPayload.splitDamageMultiplier,
            Is.EqualTo(basePayload.splitDamageMultiplier).Within(0.0001f));
    }

    [Test]
    public void PayloadControlFieldModifierAsset_GivesCurrentPayloadControlArea()
    {
        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(FireCorePath);
        TriggerTokenData onHitTrigger = AssetDatabase.LoadAssetAtPath<TriggerTokenData>(OnHitTriggerPath);
        ModifierTokenData payloadControlField = AssetDatabase.LoadAssetAtPath<ModifierTokenData>(PayloadControlFieldModifierPath);
        ResultTokenData control = AssetDatabase.LoadAssetAtPath<ResultTokenData>(ControlPath);

        Assert.That(fireCore, Is.Not.Null);
        Assert.That(onHitTrigger, Is.Not.Null);
        Assert.That(payloadControlField, Is.Not.Null);
        Assert.That(control, Is.Not.Null);
        AssertPayloadControlFieldModifier(payloadControlField);

        CompiledSpellProgram baseProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            control,
        });
        CompiledSpellProgram fieldProgram = SpellProgramCompiler.Compile(new PlaceableTokenData[]
        {
            fireCore,
            onHitTrigger,
            payloadControlField,
            control,
        });

        Assert.That(baseProgram.CanCast, Is.True);
        Assert.That(fieldProgram.CanCast, Is.True);
        Assert.That(baseProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(fieldProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        Assert.That(fieldProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));

        ResultEffectPayload basePayload = baseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        ResultEffectPayload fieldPayload = fieldProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        Assert.That(basePayload.effectRadius, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(fieldPayload.effectRadius, Is.EqualTo(1.25f).Within(0.0001f));
        Assert.That(fieldPayload.controlTriggerCount, Is.EqualTo(basePayload.controlTriggerCount));
        Assert.That(fieldPayload.controlDuration, Is.EqualTo(basePayload.controlDuration).Within(0.0001f));
    }

    [Test]
    public void HealingAsset_ConsumesRadiusValueForPayloadArea()
    {
        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(FireCorePath);
        TriggerTokenData onHitTrigger = AssetDatabase.LoadAssetAtPath<TriggerTokenData>(OnHitTriggerPath);
        ResultTokenData healing = AssetDatabase.LoadAssetAtPath<ResultTokenData>(HealingPath);
        ValueTokenData valueThree = AssetDatabase.LoadAssetAtPath<ValueTokenData>(Value3Path);

        Assert.That(fireCore, Is.Not.Null);
        Assert.That(onHitTrigger, Is.Not.Null);
        Assert.That(healing, Is.Not.Null);
        Assert.That(valueThree, Is.Not.Null);
        Assert.That(healing.AcceptsNumericValue, Is.True);
        Assert.That(healing.ValueParameterKind, Is.EqualTo(SpellValueParameterKind.Radius));

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fireCore,
            onHitTrigger,
            healing,
            valueThree,
        });

        SpellPayloadEffectNode effect = program.PrimaryCastBlock.Projectiles[0].Payloads[0].InnerBlock.PayloadEffects[0];
        Assert.That(effect.ResultType, Is.EqualTo(AttackResultType.Healing));
        Assert.That(effect.ResultEffects.effectRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(effect.ResultEffects.healingMultiplier, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void Plan2Asset_IncludesSpellBookRewardLibrary()
    {
        CombatEntryTokenSelectionPlan plan = AssetDatabase.LoadAssetAtPath<CombatEntryTokenSelectionPlan>(Plan2Path);
        SpellBookRewardLibrary spellBookLibrary = AssetDatabase.LoadAssetAtPath<SpellBookRewardLibrary>(SpellBookRewardLibraryPath);

        Assert.That(plan, Is.Not.Null);
        Assert.That(spellBookLibrary, Is.Not.Null);
        Assert.That(PlanContainsSpellBookLibrary(plan, spellBookLibrary), Is.True);
        SpellBookData bindingBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(BindingSpellBookPath);

        Assert.That(bindingBook, Is.Not.Null);
        Assert.That(spellBookLibrary.GetSpellBooks().Count, Is.GreaterThanOrEqualTo(5));
        Assert.That(SpellBookListContains(spellBookLibrary.GetSpellBooks(), bindingBook), Is.True);
        Assert.That(spellBookLibrary.SampleChoices(new VocalithRandom(123), desiredCount: 5).Count, Is.EqualTo(5));
    }

    [Test]
    public void SpellBookRewardAssets_CompileDescribeAndSampleEveryRewardBook()
    {
        SpellBookRewardLibrary spellBookLibrary = AssetDatabase.LoadAssetAtPath<SpellBookRewardLibrary>(SpellBookRewardLibraryPath);
        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(FireCorePath);

        Assert.That(spellBookLibrary, Is.Not.Null);
        Assert.That(fireCore, Is.Not.Null);

        IReadOnlyList<SpellBookData> spellBooks = spellBookLibrary.GetSpellBooks();
        IReadOnlyList<SpellBookRewardLibrary.SpellBookWeightEntry> weightEntries = spellBookLibrary.SpellBookWeights;

        Assert.That(spellBooks.Count, Is.GreaterThanOrEqualTo(5));
        Assert.That(weightEntries.Count, Is.EqualTo(spellBooks.Count));

        HashSet<string> spellBookIds = new();
        HashSet<string> displayNames = new();
        HashSet<string> signatures = new();

        for (int i = 0; i < weightEntries.Count; i++)
        {
            Assert.That(weightEntries[i], Is.Not.Null);
            Assert.That(weightEntries[i].SpellBook, Is.Not.Null);
            Assert.That(weightEntries[i].DrawWeight, Is.GreaterThan(0f), weightEntries[i].SpellBook.DisplayName);
        }

        for (int i = 0; i < spellBooks.Count; i++)
        {
            SpellBookData spellBook = spellBooks[i];
            Assert.That(spellBook, Is.Not.Null);
            Assert.That(spellBookIds.Add(spellBook.SpellBookId), Is.True, spellBook.SpellBookId);
            Assert.That(displayNames.Add(spellBook.DisplayName), Is.True, spellBook.DisplayName);
            Assert.That(HasPositiveWeightEntry(weightEntries, spellBook), Is.True, spellBook.DisplayName);

            string selectionDescription = spellBook.GetSelectionDescription();
            Assert.That(selectionDescription, Is.Not.Null.And.Not.Empty, spellBook.DisplayName);
            Assert.That(selectionDescription.ToLowerInvariant(), Does.Contain("slot"), spellBook.DisplayName);
            Assert.That(selectionDescription.ToLowerInvariant(), Does.Contain("cooldown"), spellBook.DisplayName);

            string signature = BuildSpellBookSignature(spellBook);
            Assert.That(signatures.Add(signature), Is.True, $"{spellBook.DisplayName}: {signature}");

            for (int itemIndex = 0; itemIndex < spellBook.FixedCastItems.Count; itemIndex++)
            {
                Assert.That(spellBook.FixedCastItems[itemIndex], Is.Not.Null, $"{spellBook.DisplayName} fixed item {itemIndex}");
            }

            List<PlaceableTokenData> executionItems = spellBook.BuildExecutionItems(new PlaceableTokenData[] { fireCore });
            CompiledSpellProgram program = SpellProgramCompiler.Compile(executionItems, spellBook);

            Assert.That(executionItems.Count, Is.EqualTo(spellBook.FixedCastItems.Count + 1), spellBook.DisplayName);
            Assert.That(program.CanCast, Is.True, spellBook.DisplayName);
            Assert.That(program.TryGetPrimaryProjectile(out _), Is.True, spellBook.DisplayName);
            Assert.That(FindFirstCompileError(program), Is.Empty, spellBook.DisplayName);

            string richText = SpellDescriptionGenerator.GenerateRichText(
                program,
                executionItems,
                null,
                spellBook,
                new VocalithRandom(5000 + i));
            string visibleText = StripRichText(richText);

            Assert.That(visibleText, Does.Contain(spellBook.DisplayName), spellBook.DisplayName);
            Assert.That(visibleText, Does.Contain("槽"), spellBook.DisplayName);
            Assert.That(visibleText, Does.Contain("冷却"), spellBook.DisplayName);
        }

        List<SpellBookData> sampledSpellBooks = spellBookLibrary.SampleChoices(
            new VocalithRandom(777),
            desiredCount: spellBooks.Count);

        Assert.That(sampledSpellBooks.Count, Is.EqualTo(spellBooks.Count));
        for (int i = 0; i < spellBooks.Count; i++)
        {
            Assert.That(SpellBookListContains(sampledSpellBooks, spellBooks[i]), Is.True, spellBooks[i].DisplayName);
        }
    }

    [Test]
    public void SpellBookAssets_ExposeDistinctExecutorTraits()
    {
        SpellBookData quickBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(QuickSpellBookPath);
        SpellBookData wideBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(WideSpellBookPath);
        SpellBookData triggerBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(TriggerSpellBookPath);
        SpellBookData surgeBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(SurgeSpellBookPath);
        SpellBookData bindingBook = AssetDatabase.LoadAssetAtPath<SpellBookData>(BindingSpellBookPath);
        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(FireCorePath);

        Assert.That(quickBook, Is.Not.Null);
        Assert.That(wideBook, Is.Not.Null);
        Assert.That(triggerBook, Is.Not.Null);
        Assert.That(surgeBook, Is.Not.Null);
        Assert.That(bindingBook, Is.Not.Null);
        Assert.That(fireCore, Is.Not.Null);

        Assert.That(quickBook.FixedCastItems.Count, Is.EqualTo(1));
        Assert.That(quickBook.FixedCastItems[0], Is.TypeOf<ModifierTokenData>());
        Assert.That(quickBook.ExecutorModifiers.Count, Is.EqualTo(1));
        Assert.That(quickBook.ExecutorModifiers[0].target, Is.EqualTo(TokenModifierTarget.Damage));
        Assert.That(quickBook.ExecutorModifiers[0].expression, Is.EqualTo("*=0.85"));

        Assert.That(wideBook.FixedCastItems.Count, Is.EqualTo(1));
        Assert.That(wideBook.FixedCastItems[0], Is.TypeOf<ModifierTokenData>());
        Assert.That(wideBook.CastsPerActivation, Is.EqualTo(2));
        Assert.That(wideBook.ActivationSpreadAngleStep, Is.EqualTo(10f).Within(0.0001f));

        Assert.That(surgeBook.CastsPerActivation, Is.EqualTo(3));
        Assert.That(surgeBook.ActivationSpreadAngleStep, Is.EqualTo(8f).Within(0.0001f));
        Assert.That(surgeBook.UsesEnergy, Is.True);
        Assert.That(surgeBook.EnergyCapacity, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(surgeBook.EnergyCostPerActivation, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(surgeBook.EnergyRegenPerSecond, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(surgeBook.ExecutorModifiers.Count, Is.EqualTo(1));
        Assert.That(surgeBook.ExecutorModifiers[0].target, Is.EqualTo(TokenModifierTarget.Damage));
        Assert.That(surgeBook.ExecutorModifiers[0].expression, Is.EqualTo("*=0.8"));

        Assert.That(triggerBook.FixedItemPlacement, Is.EqualTo(SpellBookFixedItemPlacement.AfterEquipped));
        Assert.That(triggerBook.FixedCastItems.Count, Is.EqualTo(2));
        Assert.That(triggerBook.FixedCastItems[0], Is.TypeOf<TriggerTokenData>());
        Assert.That(triggerBook.FixedCastItems[1], Is.TypeOf<ResultTokenData>());
        Assert.That(triggerBook.ExecutorModifiers.Count, Is.EqualTo(1));
        Assert.That(triggerBook.ExecutorModifiers[0].target, Is.EqualTo(TokenModifierTarget.ResultMultiplier));
        Assert.That(triggerBook.ExecutorModifiers[0].expression, Is.EqualTo("*=1.25"));

        List<PlaceableTokenData> triggerExecutionItems = triggerBook.BuildExecutionItems(new PlaceableTokenData[] { fireCore });
        CompiledSpellProgram triggerProgram = SpellProgramCompiler.Compile(triggerExecutionItems);
        CompiledSpellProgram triggerWithExecutorBonus = SpellProgramCompiler.Compile(triggerExecutionItems, triggerBook);

        Assert.That(triggerProgram.CanCast, Is.True);
        Assert.That(triggerWithExecutorBonus.CanCast, Is.True);
        Assert.That(triggerProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(triggerProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        Assert.That(triggerProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultType, Is.EqualTo(AttackResultType.Explosion));
        Assert.That(triggerWithExecutorBonus.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(triggerWithExecutorBonus.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        ResultEffectPayload triggerBaseEffect = triggerProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        ResultEffectPayload triggerBonusEffect = triggerWithExecutorBonus.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        Assert.That(
            triggerBonusEffect.explosionRadius,
            Is.EqualTo(triggerBaseEffect.explosionRadius).Within(0.0001f));
        Assert.That(
            triggerBonusEffect.explosionDamageMultiplier,
            Is.EqualTo(triggerBaseEffect.explosionDamageMultiplier * 1.25f).Within(0.0001f));

        List<PlaceableTokenData> quickExecutionItems = quickBook.BuildExecutionItems(new PlaceableTokenData[] { fireCore });
        CompiledSpellProgram quickWithoutExecutorBonus = SpellProgramCompiler.Compile(quickExecutionItems);
        CompiledSpellProgram quickWithExecutorBonus = SpellProgramCompiler.Compile(quickExecutionItems, quickBook);

        Assert.That(quickWithoutExecutorBonus.TryGetPrimaryProjectile(out SpellProjectileNode quickBaseProjectile), Is.True);
        Assert.That(quickWithExecutorBonus.TryGetPrimaryProjectile(out SpellProjectileNode quickBonusProjectile), Is.True);
        Assert.That(
            quickBonusProjectile.AttackSpec.damage,
            Is.EqualTo(quickBaseProjectile.AttackSpec.damage * 0.85f).Within(0.0001f));

        List<PlaceableTokenData> surgeExecutionItems = surgeBook.BuildExecutionItems(new PlaceableTokenData[] { fireCore });
        CompiledSpellProgram surgeWithoutExecutorBonus = SpellProgramCompiler.Compile(surgeExecutionItems);
        CompiledSpellProgram surgeWithExecutorBonus = SpellProgramCompiler.Compile(surgeExecutionItems, surgeBook);

        Assert.That(surgeWithoutExecutorBonus.TryGetPrimaryProjectile(out SpellProjectileNode surgeBaseProjectile), Is.True);
        Assert.That(surgeWithExecutorBonus.TryGetPrimaryProjectile(out SpellProjectileNode surgeBonusProjectile), Is.True);
        Assert.That(
            surgeBonusProjectile.AttackSpec.damage,
            Is.EqualTo(surgeBaseProjectile.AttackSpec.damage * 0.8f).Within(0.0001f));

        Assert.That(bindingBook.FixedItemPlacement, Is.EqualTo(SpellBookFixedItemPlacement.AfterEquipped));
        Assert.That(bindingBook.FixedCastItems.Count, Is.EqualTo(2));
        Assert.That(bindingBook.FixedCastItems[0], Is.TypeOf<TriggerTokenData>());
        Assert.That(bindingBook.FixedCastItems[1], Is.TypeOf<ResultTokenData>());
        Assert.That(((ResultTokenData)bindingBook.FixedCastItems[1]).ResultType, Is.EqualTo(AttackResultType.StatusEffect));
        Assert.That(bindingBook.ExecutorModifiers.Count, Is.EqualTo(2));
        Assert.That(bindingBook.ExecutorModifiers[0].target, Is.EqualTo(TokenModifierTarget.ResultCount));
        Assert.That(bindingBook.ExecutorModifiers[0].expression, Is.EqualTo("=1"));
        Assert.That(bindingBook.ExecutorModifiers[1].target, Is.EqualTo(TokenModifierTarget.ResultDuration));
        Assert.That(bindingBook.ExecutorModifiers[1].expression, Is.EqualTo("*=1.5"));

        List<PlaceableTokenData> bindingExecutionItems = bindingBook.BuildExecutionItems(new PlaceableTokenData[] { fireCore });
        CompiledSpellProgram bindingBaseProgram = SpellProgramCompiler.Compile(bindingExecutionItems);
        CompiledSpellProgram bindingBonusProgram = SpellProgramCompiler.Compile(bindingExecutionItems, bindingBook);

        Assert.That(bindingBaseProgram.CanCast, Is.True);
        Assert.That(bindingBonusProgram.CanCast, Is.True);
        Assert.That(bindingBaseProgram.PrimaryCastBlock.Payloads.Count, Is.EqualTo(1));
        Assert.That(bindingBaseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects.Count, Is.EqualTo(1));
        Assert.That(bindingBaseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultType, Is.EqualTo(AttackResultType.StatusEffect));
        ResultEffectPayload bindingBaseEffect = bindingBaseProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        ResultEffectPayload bindingBonusEffect = bindingBonusProgram.PrimaryCastBlock.Payloads[0].InnerBlock.PayloadEffects[0].ResultEffects;
        Assert.That(bindingBonusEffect.controlTriggerCount, Is.EqualTo(1));
        Assert.That(bindingBonusEffect.controlDuration, Is.EqualTo(bindingBaseEffect.controlDuration * 1.5f).Within(0.0001f));
    }

    private BulletTokenLibrary CreateLibrary(string name, params PlaceableTokenData[] tokens)
    {
        BulletTokenLibrary library = ScriptableObject.CreateInstance<BulletTokenLibrary>();
        library.name = name;
        library.SetTokens(tokens);
        createdObjects.Add(library);
        return library;
    }

    private SpellBookRewardLibrary CreateSpellBookLibrary(string name, params SpellBookData[] spellBooks)
    {
        SpellBookRewardLibrary library = ScriptableObject.CreateInstance<SpellBookRewardLibrary>();
        library.name = name;
        library.SetSpellBooks(spellBooks);
        createdObjects.Add(library);
        return library;
    }

    private SpellBookData CreateSpellBook(string spellBookId, string displayName, int slotCount, float castCooldownSeconds)
    {
        SpellBookData spellBook = ScriptableObject.CreateInstance<SpellBookData>();
        spellBook.SpellBookId = spellBookId;
        spellBook.DisplayName = displayName;
        spellBook.SlotCount = slotCount;
        spellBook.CastCooldownSeconds = castCooldownSeconds;
        spellBook.CastsPerActivation = 1;
        spellBook.name = spellBookId;
        createdObjects.Add(spellBook);
        return spellBook;
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

    private static bool PlanContainsLibrary(CombatEntryTokenSelectionPlan plan, BulletTokenLibrary library)
    {
        if (plan == null || library == null)
        {
            return false;
        }

        IReadOnlyList<CombatEntryTokenSelectionPlan.LibraryWeightEntry> entries = plan.LibraryEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Library == library && entries[i].SelectionWeight > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static float FindPlanLibraryWeight(CombatEntryTokenSelectionPlan plan, BulletTokenLibrary library)
    {
        if (plan == null || library == null)
        {
            return 0f;
        }

        IReadOnlyList<CombatEntryTokenSelectionPlan.LibraryWeightEntry> entries = plan.LibraryEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Library == library)
            {
                return entries[i].SelectionWeight;
            }
        }

        return 0f;
    }

    private static float FindPlanSpellBookLibraryWeight(
        CombatEntryTokenSelectionPlan plan,
        SpellBookRewardLibrary library)
    {
        if (plan == null || library == null)
        {
            return 0f;
        }

        IReadOnlyList<CombatEntryTokenSelectionPlan.SpellBookLibraryWeightEntry> entries = plan.SpellBookLibraryEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Library == library)
            {
                return entries[i].SelectionWeight;
            }
        }

        return 0f;
    }

    private static float SumPlanRewardSourceWeights(CombatEntryTokenSelectionPlan plan)
    {
        if (plan == null)
        {
            return 0f;
        }

        float totalWeight = 0f;
        IReadOnlyList<CombatEntryTokenSelectionPlan.LibraryWeightEntry> tokenEntries = plan.LibraryEntries;
        for (int i = 0; i < tokenEntries.Count; i++)
        {
            if (tokenEntries[i] != null && tokenEntries[i].Library != null && tokenEntries[i].SelectionWeight > 0f)
            {
                totalWeight += tokenEntries[i].SelectionWeight;
            }
        }

        IReadOnlyList<CombatEntryTokenSelectionPlan.SpellBookLibraryWeightEntry> spellBookEntries = plan.SpellBookLibraryEntries;
        for (int i = 0; i < spellBookEntries.Count; i++)
        {
            if (spellBookEntries[i] != null && spellBookEntries[i].Library != null && spellBookEntries[i].SelectionWeight > 0f)
            {
                totalWeight += spellBookEntries[i].SelectionWeight;
            }
        }

        return totalWeight;
    }

    private static bool PlanContainsSpellBookLibrary(CombatEntryTokenSelectionPlan plan, SpellBookRewardLibrary library)
    {
        if (plan == null || library == null)
        {
            return false;
        }

        IReadOnlyList<CombatEntryTokenSelectionPlan.SpellBookLibraryWeightEntry> entries = plan.SpellBookLibraryEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Library == library && entries[i].SelectionWeight > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static float FindSpellBookWeight(SpellBookRewardLibrary library, SpellBookData spellBook)
    {
        if (library == null || spellBook == null)
        {
            return 0f;
        }

        IReadOnlyList<SpellBookRewardLibrary.SpellBookWeightEntry> entries = library.SpellBookWeights;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].SpellBook == spellBook)
            {
                return entries[i].DrawWeight;
            }
        }

        return 0f;
    }

    private static bool TokenListContains(IReadOnlyList<PlaceableTokenData> tokens, PlaceableTokenData expectedToken)
    {
        if (tokens == null || expectedToken == null)
        {
            return false;
        }

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] == expectedToken)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SpellBookListContains(IReadOnlyList<SpellBookData> spellBooks, SpellBookData expectedSpellBook)
    {
        if (spellBooks == null || expectedSpellBook == null)
        {
            return false;
        }

        for (int i = 0; i < spellBooks.Count; i++)
        {
            if (spellBooks[i] == expectedSpellBook)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPositiveWeightEntry(
        IReadOnlyList<SpellBookRewardLibrary.SpellBookWeightEntry> entries,
        SpellBookData spellBook)
    {
        if (entries == null || spellBook == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].SpellBook == spellBook && entries[i].DrawWeight > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildSpellBookSignature(SpellBookData spellBook)
    {
        if (spellBook == null)
        {
            return string.Empty;
        }

        List<string> parts = new()
        {
            $"slots:{spellBook.SlotCount}",
            $"cooldown:{spellBook.CastCooldownSeconds:0.###}",
            $"casts:{spellBook.CastsPerActivation}",
            $"spread:{spellBook.ActivationSpreadAngleStep:0.###}",
            $"energy:{spellBook.EnergyCapacity:0.###}/{spellBook.EnergyCostPerActivation:0.###}/{spellBook.EnergyRegenPerSecond:0.###}",
            $"placement:{spellBook.FixedItemPlacement}",
        };

        for (int i = 0; i < spellBook.FixedCastItems.Count; i++)
        {
            PlaceableTokenData item = spellBook.FixedCastItems[i];
            parts.Add($"fixed:{ResolvePrimaryTokenType(item)}:{ResolvePlaceableTokenIds(item)}");
        }

        for (int i = 0; i < spellBook.ExecutorModifiers.Count; i++)
        {
            TokenModifierDefinition modifier = spellBook.ExecutorModifiers[i].GetSanitized();
            parts.Add($"executor:{modifier.target}:{modifier.expression}");
        }

        return string.Join("|", parts);
    }

    private static string ResolvePlaceableTokenIds(PlaceableTokenData item)
    {
        if (item is BaseTokenData baseToken)
        {
            return baseToken.TokenId;
        }

        List<BaseTokenData> compileTokens = new();
        item?.AppendCompileTokens(compileTokens);
        List<string> tokenIds = new();
        for (int i = 0; i < compileTokens.Count; i++)
        {
            if (compileTokens[i] != null)
            {
                tokenIds.Add(compileTokens[i].TokenId);
            }
        }

        return string.Join("+", tokenIds);
    }

    private static string FindFirstCompileError(CompiledSpellProgram program)
    {
        if (program == null)
        {
            return "Program is null.";
        }

        for (int i = 0; i < program.Messages.Count; i++)
        {
            if (program.Messages[i].severity == AttackCompileMessageSeverity.Error)
            {
                return program.Messages[i].message;
            }
        }

        return string.Empty;
    }

    private static string StripRichText(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : Regex.Replace(text, "<.*?>", string.Empty);
    }

    private static void AssertPayloadAmplifyModifier(ModifierTokenData payloadAmplify)
    {
        Assert.That(payloadAmplify.Modifiers.Count, Is.EqualTo(1));
        Assert.That(payloadAmplify.Modifiers[0].target, Is.EqualTo(TokenModifierTarget.ResultMultiplier));
        Assert.That(payloadAmplify.Modifiers[0].expression, Is.EqualTo("*=1.5"));
    }

    private static void AssertPayloadRadiusModifier(ModifierTokenData payloadRadius)
    {
        Assert.That(payloadRadius.Modifiers.Count, Is.EqualTo(1));
        Assert.That(payloadRadius.Modifiers[0].target, Is.EqualTo(TokenModifierTarget.ImpactRadiusMultiplier));
        Assert.That(payloadRadius.Modifiers[0].expression, Is.EqualTo("*=1.35"));
    }

    private static void AssertPayloadCountModifier(ModifierTokenData payloadCount)
    {
        Assert.That(payloadCount.Modifiers.Count, Is.EqualTo(1));
        Assert.That(payloadCount.Modifiers[0].target, Is.EqualTo(TokenModifierTarget.ResultCount));
        Assert.That(payloadCount.Modifiers[0].expression, Is.EqualTo("+=2"));
    }

    private static void AssertPayloadControlFieldModifier(ModifierTokenData payloadControlField)
    {
        Assert.That(payloadControlField.Modifiers.Count, Is.EqualTo(1));
        Assert.That(payloadControlField.Modifiers[0].target, Is.EqualTo(TokenModifierTarget.ImpactRadiusMultiplier));
        Assert.That(payloadControlField.Modifiers[0].expression, Is.EqualTo("=1.25"));
    }

    private static void AssertTokenTypesInclude(IReadOnlyList<PlaceableTokenData> tokens, params TokenType[] expectedTypes)
    {
        List<TokenType> actualTypes = new();
        if (tokens != null)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                actualTypes.Add(ResolvePrimaryTokenType(tokens[i]));
            }
        }

        for (int i = 0; i < expectedTypes.Length; i++)
        {
            Assert.That(actualTypes.Contains(expectedTypes[i]), Is.True, $"Expected token set to include {expectedTypes[i]}.");
        }
    }

    private static TokenType ResolvePrimaryTokenType(PlaceableTokenData token)
    {
        if (token is BaseTokenData baseToken)
        {
            return baseToken.TokenType;
        }

        List<BaseTokenData> compileTokens = new();
        token?.AppendCompileTokens(compileTokens);
        for (int i = 0; i < compileTokens.Count; i++)
        {
            if (compileTokens[i] != null && compileTokens[i].TokenType != TokenType.None)
            {
                return compileTokens[i].TokenType;
            }
        }

        return TokenType.None;
    }
}
