using System.Collections.Generic;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FormalSpellTokenAssetTests
{
    private const string Plan2Path = "Assets/Data/BulletTokens/SelectionPlans/Plan2.asset";
    private const string SpellProgramLibraryPath = "Assets/Data/BulletTokens/TokenLib/SpellProgram_Token_Lib.asset";
    private const string PlayableLibraryPath = "Assets/Data/BulletTokens/TokenLib/SpellToken_Playable_Staging_Lib.asset";
    private const string HiddenLibraryPath = "Assets/Data/BulletTokens/TokenLib/SpellToken_Hidden_Prototype_Lib.asset";

    [Test]
    public void FormalTokenLibraries_LoadAndStayOutOfPlan2()
    {
        CombatEntryTokenSelectionPlan plan = AssetDatabase.LoadAssetAtPath<CombatEntryTokenSelectionPlan>(Plan2Path);
        BulletTokenLibrary spellProgramLibrary = LoadLibrary(SpellProgramLibraryPath);
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        BulletTokenLibrary hiddenLibrary = LoadLibrary(HiddenLibraryPath);

        Assert.That(plan, Is.Not.Null);
        Assert.That(playableLibrary.GetTokens().Count, Is.EqualTo(78));
        Assert.That(hiddenLibrary.GetTokens().Count, Is.EqualTo(7));
        Assert.That(PlanContainsLibrary(plan, playableLibrary), Is.False);
        Assert.That(PlanContainsLibrary(plan, hiddenLibrary), Is.False);
        Assert.That(HasSharedTokens(spellProgramLibrary, playableLibrary), Is.False);
        Assert.That(HasSharedTokens(spellProgramLibrary, hiddenLibrary), Is.False);

        IReadOnlyList<PlaceableTokenData> hiddenTokens = hiddenLibrary.GetTokens();
        for (int i = 0; i < hiddenTokens.Count; i++)
        {
            Assert.That(hiddenLibrary.GetTokenWeight(hiddenTokens[i]), Is.EqualTo(0f).Within(0.0001f));
        }
    }

    [Test]
    public void FormalTokenLibraries_HaveUniqueIdsAndDisplayText()
    {
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        BulletTokenLibrary hiddenLibrary = LoadLibrary(HiddenLibraryPath);
        HashSet<string> tokenIds = new();

        AssertTokenIdentity(playableLibrary.GetTokens(), tokenIds);
        AssertTokenIdentity(hiddenLibrary.GetTokens(), tokenIds);
    }

    [Test]
    public void PlayableCoreTokens_CompileIndividually()
    {
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        IReadOnlyList<PlaceableTokenData> tokens = playableLibrary.GetTokens();

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is not CoreTokenData coreToken)
            {
                continue;
            }

            CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { coreToken });

            Assert.That(program.CanCast, Is.True, coreToken.TokenId);
            Assert.That(program.PrimaryCastBlock.Projectiles[0].CoreType, Is.EqualTo(coreToken.CoreType), coreToken.TokenId);
        }
    }

    [Test]
    public void PlayableBehaviorTokens_CompileWithFireCore()
    {
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        CoreTokenData fire = FindToken<CoreTokenData>(playableLibrary, "core_fire");
        ValueTokenData three = FindToken<ValueTokenData>(playableLibrary, "value_three");
        IReadOnlyList<PlaceableTokenData> tokens = playableLibrary.GetTokens();

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is not BehaviorTokenData behaviorToken)
            {
                continue;
            }

            CompiledSpellProgram program = behaviorToken.AcceptsNumericValue
                ? SpellProgramCompiler.Compile(new BaseTokenData[] { fire, behaviorToken, three })
                : SpellProgramCompiler.Compile(new BaseTokenData[] { fire, behaviorToken });

            Assert.That(program.CanCast, Is.True, behaviorToken.TokenId);
            SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];
            Assert.That(projectile.BehaviorType, Is.EqualTo(behaviorToken.BehaviorType), behaviorToken.TokenId);
            if (behaviorToken.BehaviorType == AttackBehaviorType.Chain)
            {
                Assert.That(projectile.AttackSpec.chainCount, Is.EqualTo(3));
            }
            else if (behaviorToken.BehaviorType == AttackBehaviorType.Stasis)
            {
                Assert.That(projectile.AttackSpec.behaviorParameter, Is.EqualTo(3f).Within(0.0001f), behaviorToken.TokenId);
                Assert.That(projectile.AttackSpec.maxLifetime, Is.EqualTo(3f).Within(0.0001f), behaviorToken.TokenId);
            }
            else if (behaviorToken.BehaviorType == AttackBehaviorType.Rush ||
                     behaviorToken.BehaviorType == AttackBehaviorType.Slow ||
                     behaviorToken.BehaviorType == AttackBehaviorType.Snake ||
                     behaviorToken.BehaviorType == AttackBehaviorType.Wander ||
                     behaviorToken.BehaviorType == AttackBehaviorType.Split ||
                     behaviorToken.BehaviorType == AttackBehaviorType.Spin)
            {
                Assert.That(projectile.AttackSpec.behaviorParameter, Is.EqualTo(3f).Within(0.0001f), behaviorToken.TokenId);
            }
        }
    }

    [Test]
    public void PlayableStatusResultTokens_WriteExpectedStatusSlots()
    {
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        CoreTokenData fire = FindToken<CoreTokenData>(playableLibrary, "core_fire");
        CoreTokenData water = FindToken<CoreTokenData>(playableLibrary, "core_water");
        CoreTokenData wind = FindToken<CoreTokenData>(playableLibrary, "core_wind");
        CoreTokenData light = FindToken<CoreTokenData>(playableLibrary, "core_light");
        CoreTokenData sheep = FindToken<CoreTokenData>(playableLibrary, "core_sheep");
        CoreTokenData riddle = FindToken<CoreTokenData>(playableLibrary, "core_riddle");

        AssertStatusResult(playableLibrary, fire, "result_burn", SpellStatusSlot.Ignite);
        AssertStatusResult(playableLibrary, fire, "result_bind", SpellStatusSlot.Bind);
        AssertStatusResult(playableLibrary, fire, "result_corrode", SpellStatusSlot.Corrosion);
        AssertStatusResult(playableLibrary, fire, "result_mark", SpellStatusSlot.Mark);
        AssertStatusResult(playableLibrary, fire, "result_wet", SpellStatusSlot.Wet);
        AssertStatusResult(playableLibrary, fire, "result_shock", SpellStatusSlot.Disable);
        Assert.That(water.CreateCoreEffects().statusApplications[0].slot, Is.EqualTo(SpellStatusSlot.Wet));
        Assert.That(wind.CreateCoreEffects().windPressureRadius, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(light.CreateCoreEffects().HasPiercingSuppression, Is.True);
        Assert.That(sheep.CreateCoreEffects().statusApplications[0].slot, Is.EqualTo(SpellStatusSlot.Polymorph));
        Assert.That(riddle.CoreType, Is.EqualTo(AttackCoreType.Riddle));
    }

    [Test]
    public void PlayableNewResultTokens_CompileWithExpectedValueParameters()
    {
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        CoreTokenData fire = FindToken<CoreTokenData>(playableLibrary, "core_fire");
        ValueTokenData three = FindToken<ValueTokenData>(playableLibrary, "value_three");
        ValueTokenData five = FindToken<ValueTokenData>(playableLibrary, "value_five");

        AssertStrengthResult(playableLibrary, fire, three, "result_drain", AttackResultType.Drain);
        AssertStrengthResult(playableLibrary, fire, three, "result_shield", AttackResultType.Shield);
        AssertStrengthResult(playableLibrary, fire, three, "result_push", AttackResultType.Push);
        AssertStrengthResult(playableLibrary, fire, three, "result_pull", AttackResultType.Pull);

        ResultTokenData leave = FindToken<ResultTokenData>(playableLibrary, "result_leave");
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { fire, leave, five });
        SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];
        Assert.That(projectile.ResultType, Is.EqualTo(AttackResultType.Leave));
        Assert.That(projectile.ResultEffects.effectDuration, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(projectile.ResultEffects.effectRadius, Is.EqualTo(3f).Within(0.0001f));
    }

    [Test]
    public void PlayableTriggerAndMulticastTokens_CompileWithExpectedStructure()
    {
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        CoreTokenData fire = FindToken<CoreTokenData>(playableLibrary, "core_fire");
        CoreTokenData ice = FindToken<CoreTokenData>(playableLibrary, "core_ice");
        CoreTokenData thunder = FindToken<CoreTokenData>(playableLibrary, "core_thunder");
        ValueTokenData three = FindToken<ValueTokenData>(playableLibrary, "value_three");
        ValueTokenData five = FindToken<ValueTokenData>(playableLibrary, "value_five");
        ResultTokenData explosion = FindToken<ResultTokenData>(playableLibrary, "result_explosion_formal");
        ResultTokenData control = FindToken<ResultTokenData>(playableLibrary, "result_control_formal");

        AssertTriggerPayload(playableLibrary, new BaseTokenData[] { fire, FindToken<TriggerTokenData>(playableLibrary, "trigger_timer"), three, explosion }, SpellTriggerType.OnTimer, 3f);
        AssertTriggerPayload(playableLibrary, new BaseTokenData[] { fire, FindToken<TriggerTokenData>(playableLibrary, "trigger_distance"), five, thunder }, SpellTriggerType.OnDistance, 5f);
        AssertTriggerPayload(playableLibrary, new BaseTokenData[] { fire, FindToken<TriggerTokenData>(playableLibrary, "trigger_proximity"), three, control }, SpellTriggerType.OnProximity, 3f);
        AssertTriggerPayload(playableLibrary, new BaseTokenData[] { fire, FindToken<TriggerTokenData>(playableLibrary, "trigger_expire"), explosion }, SpellTriggerType.OnExpire, 0f);
        AssertTriggerPayload(playableLibrary, new BaseTokenData[] { fire, FindToken<TriggerTokenData>(playableLibrary, "trigger_kill"), explosion }, SpellTriggerType.OnKill, 0f);

        AssertMulticastPattern(new BaseTokenData[] { FindToken<MulticastTokenData>(playableLibrary, "multicast_dual"), fire, ice }, SpellCastPattern.Simultaneous, 2);
        AssertMulticastPattern(new BaseTokenData[] { FindToken<MulticastTokenData>(playableLibrary, "multicast_triple"), fire, ice, thunder }, SpellCastPattern.Simultaneous, 3);
        AssertMulticastPattern(new BaseTokenData[] { FindToken<MulticastTokenData>(playableLibrary, "multicast_sequence"), fire, ice }, SpellCastPattern.Sequential, 2);
        AssertMulticastPattern(new BaseTokenData[] { FindToken<MulticastTokenData>(playableLibrary, "multicast_fork"), fire, ice }, SpellCastPattern.Fork, 2);
        AssertMulticastPattern(new BaseTokenData[] { FindToken<MulticastTokenData>(playableLibrary, "multicast_orbit"), fire, ice }, SpellCastPattern.Orbit, 2);
    }

    [Test]
    public void PlayableNewModifierTokens_CompileAsCastRuntimeModifiers()
    {
        BulletTokenLibrary playableLibrary = LoadLibrary(PlayableLibraryPath);
        CoreTokenData fire = FindToken<CoreTokenData>(playableLibrary, "core_fire");
        ModifierTokenData stable = FindToken<ModifierTokenData>(playableLibrary, "modifier_stable");
        ModifierTokenData wild = FindToken<ModifierTokenData>(playableLibrary, "modifier_wild");
        ModifierTokenData greedy = FindToken<ModifierTokenData>(playableLibrary, "modifier_greedy");
        ModifierTokenData urgent = FindToken<ModifierTokenData>(playableLibrary, "modifier_urgent");
        ModifierTokenData source = FindToken<ModifierTokenData>(playableLibrary, "modifier_source");

        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[]
        {
            fire,
            stable,
            wild,
            greedy,
            urgent,
            source,
        });

        SpellCastRuntimeModifiers runtime = program.RuntimeModifiers.GetSanitized();
        Assert.That(program.CanCast, Is.True);
        Assert.That(runtime.angleSpreadMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(runtime.movementVarianceMultiplier, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(runtime.casterHealthCost, Is.EqualTo(10f).Within(0.0001f));
        Assert.That(runtime.dropChanceMultiplierOnKill, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(runtime.castCooldownMultiplier, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(runtime.energyCostMultiplier, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(program.PrimaryCastBlock.Projectiles[0].AttackSpec.damage, Is.EqualTo(8.505f).Within(0.0001f));
    }

    [Test]
    public void HiddenPrototypeTokens_DoNotContributeCompileTokens()
    {
        BulletTokenLibrary hiddenLibrary = LoadLibrary(HiddenLibraryPath);
        IReadOnlyList<PlaceableTokenData> tokens = hiddenLibrary.GetTokens();

        AssertMissingToken(hiddenLibrary, "prototype_behavior_stasis");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_rush");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_slow");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_snake");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_wander");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_ambush");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_fall");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_split");
        AssertMissingToken(hiddenLibrary, "prototype_behavior_spin");
        AssertMissingToken(hiddenLibrary, "prototype_multicast_orbit");
        AssertMissingToken(hiddenLibrary, "prototype_result_drain");
        AssertMissingToken(hiddenLibrary, "prototype_result_shield");
        AssertMissingToken(hiddenLibrary, "prototype_result_leave");
        AssertMissingToken(hiddenLibrary, "prototype_result_push");
        AssertMissingToken(hiddenLibrary, "prototype_result_pull");
        AssertMissingToken(hiddenLibrary, "prototype_modifier_guard");
        AssertMissingToken(hiddenLibrary, "prototype_modifier_stable");
        AssertMissingToken(hiddenLibrary, "prototype_modifier_wild");
        AssertMissingToken(hiddenLibrary, "prototype_modifier_greedy");
        AssertMissingToken(hiddenLibrary, "prototype_modifier_urgent");
        AssertMissingToken(hiddenLibrary, "prototype_modifier_source");
        AssertMissingToken(hiddenLibrary, "prototype_core_water");
        AssertMissingToken(hiddenLibrary, "prototype_core_wind");
        AssertMissingToken(hiddenLibrary, "prototype_core_light");
        AssertMissingToken(hiddenLibrary, "prototype_core_sheep");
        AssertMissingToken(hiddenLibrary, "prototype_core_riddle");

        for (int i = 0; i < tokens.Count; i++)
        {
            Assert.That(tokens[i], Is.TypeOf<PrototypeTokenData>());
            List<BaseTokenData> compileTokens = new();
            tokens[i].AppendCompileTokens(compileTokens);
            Assert.That(compileTokens, Is.Empty, GetTokenId(tokens[i]));
            Assert.That(tokens[i].GetPickupDisplayText(), Is.Not.Empty, GetTokenId(tokens[i]));
            Assert.That(tokens[i].GetSelectionDescription(), Is.Not.Empty, GetTokenId(tokens[i]));
            Assert.That(((PrototypeTokenData)tokens[i]).UnimplementedReason, Is.Not.Empty, GetTokenId(tokens[i]));
        }
    }

    private static void AssertTriggerPayload(
        BulletTokenLibrary library,
        BaseTokenData[] formula,
        SpellTriggerType expectedTrigger,
        float expectedParameter)
    {
        CompiledSpellProgram program = SpellProgramCompiler.Compile(formula);

        Assert.That(library, Is.Not.Null);
        Assert.That(program.CanCast, Is.True, expectedTrigger.ToString());
        SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];
        Assert.That(projectile.Payloads.Count, Is.EqualTo(1), expectedTrigger.ToString());
        Assert.That(projectile.Payloads[0].TriggerType, Is.EqualTo(expectedTrigger), expectedTrigger.ToString());
        Assert.That(projectile.Payloads[0].ParameterValue, Is.EqualTo(expectedParameter).Within(0.0001f), expectedTrigger.ToString());
    }

    private static void AssertMulticastPattern(BaseTokenData[] formula, SpellCastPattern expectedPattern, int expectedCount)
    {
        CompiledSpellProgram program = SpellProgramCompiler.Compile(formula);

        Assert.That(program.CanCast, Is.True, expectedPattern.ToString());
        Assert.That(program.PrimaryCastBlock.CastPattern, Is.EqualTo(expectedPattern));
        Assert.That(program.PrimaryCastBlock.Projectiles.Count, Is.EqualTo(expectedCount));
    }

    private static void AssertStatusResult(BulletTokenLibrary library, CoreTokenData core, string tokenId, SpellStatusSlot expectedSlot)
    {
        ResultTokenData result = FindToken<ResultTokenData>(library, tokenId);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { core, result });

        Assert.That(program.CanCast, Is.True, tokenId);
        SpellStatusApplication[] applications = program.PrimaryCastBlock.Projectiles[0].ResultEffects.statusApplications;
        Assert.That(applications, Is.Not.Empty, tokenId);
        Assert.That(applications[0].slot, Is.EqualTo(expectedSlot), tokenId);
    }

    private static void AssertStrengthResult(BulletTokenLibrary library, CoreTokenData core, ValueTokenData value, string tokenId, AttackResultType expectedResult)
    {
        ResultTokenData result = FindToken<ResultTokenData>(library, tokenId);
        CompiledSpellProgram program = SpellProgramCompiler.Compile(new BaseTokenData[] { core, result, value });

        Assert.That(program.CanCast, Is.True, tokenId);
        SpellProjectileNode projectile = program.PrimaryCastBlock.Projectiles[0];
        Assert.That(projectile.ResultType, Is.EqualTo(expectedResult), tokenId);
        Assert.That(projectile.ResultEffects.effectStrength, Is.EqualTo(3f).Within(0.0001f), tokenId);
    }

    private static void AssertTokenIdentity(IReadOnlyList<PlaceableTokenData> tokens, HashSet<string> tokenIds)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            PlaceableTokenData token = tokens[i];
            string tokenId = GetTokenId(token);
            string displayText = GetDisplayText(token);

            Assert.That(tokenId, Is.Not.Empty);
            Assert.That(displayText, Is.Not.Empty, tokenId);
            Assert.That(tokenIds.Add(tokenId), Is.True, $"Duplicate token id: {tokenId}");
        }
    }

    private static T FindToken<T>(BulletTokenLibrary library, string tokenId) where T : PlaceableTokenData
    {
        IReadOnlyList<PlaceableTokenData> tokens = library.GetTokens();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is T typedToken && string.Equals(GetTokenId(typedToken), tokenId, System.StringComparison.Ordinal))
            {
                return typedToken;
            }
        }

        Assert.Fail($"Missing token '{tokenId}'.");
        return null;
    }

    private static void AssertMissingToken(BulletTokenLibrary library, string tokenId)
    {
        IReadOnlyList<PlaceableTokenData> tokens = library.GetTokens();
        for (int i = 0; i < tokens.Count; i++)
        {
            Assert.That(GetTokenId(tokens[i]), Is.Not.EqualTo(tokenId));
        }
    }

    private static BulletTokenLibrary LoadLibrary(string path)
    {
        BulletTokenLibrary library = AssetDatabase.LoadAssetAtPath<BulletTokenLibrary>(path);
        Assert.That(library, Is.Not.Null, path);
        return library;
    }

    private static bool PlanContainsLibrary(CombatEntryTokenSelectionPlan plan, BulletTokenLibrary library)
    {
        IReadOnlyList<CombatEntryTokenSelectionPlan.LibraryWeightEntry> entries = plan.LibraryEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].Library == library)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSharedTokens(BulletTokenLibrary left, BulletTokenLibrary right)
    {
        IReadOnlyList<PlaceableTokenData> leftTokens = left.GetTokens();
        IReadOnlyList<PlaceableTokenData> rightTokens = right.GetTokens();
        for (int i = 0; i < leftTokens.Count; i++)
        {
            for (int j = 0; j < rightTokens.Count; j++)
            {
                if (leftTokens[i] == rightTokens[j])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetTokenId(PlaceableTokenData token)
    {
        return token switch
        {
            BaseTokenData baseToken => baseToken.TokenId,
            PrototypeTokenData prototypeToken => prototypeToken.TokenId,
            _ => token != null ? token.name : string.Empty,
        };
    }

    private static string GetDisplayText(PlaceableTokenData token)
    {
        return token switch
        {
            BaseTokenData baseToken => baseToken.GetResolvedDisplayText(),
            PrototypeTokenData prototypeToken => prototypeToken.DisplayText,
            _ => token != null ? token.GetPickupDisplayText() : string.Empty,
        };
    }
}
