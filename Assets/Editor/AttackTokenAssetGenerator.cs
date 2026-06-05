using System;
using Kernel.Bullet;
using UnityEditor;
using UnityEngine;

public static class AttackTokenAssetGenerator
{
    private const string RootFolder = "Assets/Data/BulletTokens";
    private const string CoreFolder = RootFolder + "/Core";
    private const string BehaviorFolder = RootFolder + "/Behavior";
    private const string ResultFolder = RootFolder + "/Result";
    private const string ValueFolder = RootFolder + "/Value";
    private const string TokenLibFolder = RootFolder + "/TokenLib";
    private const string LinkedFolder = RootFolder + "/Linked";
    private const string PickupFolder = RootFolder + "/Pickup";
    private const string BaseLibraryPath = TokenLibFolder + "/Base_Token_Lib.asset";
    private const string FormalFolder = RootFolder + "/Formal";
    private const string FormalCoreFolder = FormalFolder + "/Core";
    private const string FormalBehaviorFolder = FormalFolder + "/Behavior";
    private const string FormalResultFolder = FormalFolder + "/Result";
    private const string FormalModifierFolder = FormalFolder + "/Modifier";
    private const string FormalValueFolder = FormalFolder + "/Value";
    private const string FormalMulticastFolder = FormalFolder + "/Multicast";
    private const string FormalTriggerFolder = FormalFolder + "/Trigger";
    private const string FormalPrototypeFolder = FormalFolder + "/Prototype";
    private const string PlayableStagingLibraryPath = TokenLibFolder + "/SpellToken_Playable_Staging_Lib.asset";
    private const string HiddenPrototypeLibraryPath = TokenLibFolder + "/SpellToken_Hidden_Prototype_Lib.asset";

    [MenuItem("Tools/Lilith/Bullet/Generate Default Token Assets")]
    public static void GenerateDefaultAssets()
    {
        EnsureFolderChain(CoreFolder);
        EnsureFolderChain(BehaviorFolder);
        EnsureFolderChain(ResultFolder);
        EnsureFolderChain(ValueFolder);
        EnsureFolderChain(TokenLibFolder);
        EnsureFolderChain(LinkedFolder);
        EnsureFolderChain(PickupFolder);

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/EdgeCore.asset", token =>
        {
            token.TokenId = "edge_core";
            token.DisplayText = "锋";
            token.Description = "高伤直射核心，对 shield 敌人额外造成 1.2 倍伤害。";
            token.SetBulletTextOverride(true, "锋");
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.CoreType = AttackCoreType.Edge;
            token.DefaultValueType = AttackValueType.oneShot;
            token.Damage = 10f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 148f;
            token.MaxTravelDistance = 256f;
            token.MaxLifetime = (256f / 148f) + 0.1f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.ArmoredEnemyId = "shield";
            token.ArmoredDamageMultiplier = 1.2f;
            token.BurnTriggerCount = 0;
            token.BurnDamagePerSecond = 0f;
            token.BurnDuration = 0f;
            token.SlowPercent = 0f;
            token.SlowDuration = 0f;
            token.ThunderChainTargetCount = 0;
            token.ThunderChainRadius = 0f;
            token.ThunderChainDamage = 0f;
        });

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/FireCore.asset", token =>
        {
            token.TokenId = "fire_core";
            token.DisplayText = "火";
            token.Description = "稳定泛用核心，连续命中同一敌人 3 次后施加 3 点每秒、持续 2 秒的灼烧。";
            token.SetBulletTextOverride(true, "火");
            token.SetModifiers(new[]
            {
                new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
            });
            token.CoreType = AttackCoreType.Fire;
            token.DefaultValueType = AttackValueType.oneShot;
            token.Damage = 7f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 125f;
            token.MaxTravelDistance = 216f;
            token.MaxLifetime = (216f / 125f) + 0.1f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.ArmoredEnemyId = string.Empty;
            token.ArmoredDamageMultiplier = 1f;
            token.BurnTriggerCount = 3;
            token.BurnDamagePerSecond = 3f;
            token.BurnDuration = 2f;
            token.SlowPercent = 0f;
            token.SlowDuration = 0f;
            token.ThunderChainTargetCount = 0;
            token.ThunderChainRadius = 0f;
            token.ThunderChainDamage = 0f;
        });

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/IceCore.asset", token =>
        {
            token.TokenId = "ice_core";
            token.DisplayText = "冰";
            token.Description = "控制型核心，命中后附加 25% 减速，持续 1.5 秒。";
            token.SetBulletTextOverride(true, "冰");
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.CoreType = AttackCoreType.Ice;
            token.DefaultValueType = AttackValueType.oneShot;
            token.Damage = 6f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 118f;
            token.MaxTravelDistance = 216f;
            token.MaxLifetime = (216f / 118f) + 0.1f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.ArmoredEnemyId = string.Empty;
            token.ArmoredDamageMultiplier = 1f;
            token.BurnTriggerCount = 0;
            token.BurnDamagePerSecond = 0f;
            token.BurnDuration = 0f;
            token.SlowPercent = 0.25f;
            token.SlowDuration = 1.5f;
            token.ThunderChainTargetCount = 0;
            token.ThunderChainRadius = 0f;
            token.ThunderChainDamage = 0f;
        });

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/ThunderCore.asset", token =>
        {
            token.TokenId = "thunder_core";
            token.DisplayText = "雷";
            token.Description = "功能型核心，命中主目标后会额外跳向 1 个附近敌人。";
            token.SetBulletTextOverride(true, "雷");
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.CoreType = AttackCoreType.Thunder;
            token.DefaultValueType = AttackValueType.oneShot;
            token.Damage = 7f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 133f;
            token.MaxTravelDistance = 216f;
            token.MaxLifetime = (216f / 133f) + 0.1f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.ArmoredEnemyId = string.Empty;
            token.ArmoredDamageMultiplier = 1f;
            token.BurnTriggerCount = 0;
            token.BurnDamagePerSecond = 0f;
            token.BurnDuration = 0f;
            token.SlowPercent = 0f;
            token.SlowDuration = 0f;
            token.ThunderChainTargetCount = 1;
            token.ThunderChainRadius = 48f;
            token.ThunderChainDamage = 4f;
        });

        CreateOrUpdateAsset<BehaviorTokenData>(BehaviorFolder + "/Straight.asset", token =>
        {
            token.TokenId = "behavior_straight";
            token.DisplayText = "直";
            token.Description = "默认直射弹道，不消耗数值词。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.BehaviorType = AttackBehaviorType.Straight;
            token.AcceptsNumericValue = false;
            token.DefaultProjectileCount = 1;
            token.SpreadAngleStep = 0f;
            token.ProjectileDamageMultiplier = 1f;
            token.PierceLifetimeDistanceScalePerCount = 0.2f;
        });

        CreateOrUpdateAsset<BehaviorTokenData>(BehaviorFolder + "/Spread.asset", token =>
        {
            token.TokenId = "behavior_spread";
            token.DisplayText = "散";
            token.Description = "发射散射子弹；默认 3 发，每发伤害为当前直伤的 50%。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.BehaviorType = AttackBehaviorType.Spread;
            token.AcceptsNumericValue = true;
            token.DefaultProjectileCount = 3;
            token.SpreadAngleStep = 12f;
            token.ProjectileDamageMultiplier = 0.5f;
            token.PierceLifetimeDistanceScalePerCount = 0.2f;
        });

        CreateOrUpdateAsset<BehaviorTokenData>(BehaviorFolder + "/Bounce.asset", token =>
        {
            token.TokenId = "behavior_bounce";
            token.DisplayText = "弹";
            token.Description = "命中墙体后反射继续飞行；默认 1 次，值词可改成 2/3/5 次。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.BehaviorType = AttackBehaviorType.Bounce;
            token.AcceptsNumericValue = true;
            token.DefaultProjectileCount = 1;
            token.SpreadAngleStep = 0f;
            token.ProjectileDamageMultiplier = 1f;
            token.PierceLifetimeDistanceScalePerCount = 0.2f;
        });

        CreateOrUpdateAsset<BehaviorTokenData>(BehaviorFolder + "/Pierce.asset", token =>
        {
            token.TokenId = "behavior_pierce";
            token.DisplayText = "透";
            token.Description = "命中敌人后继续飞行且不掉伤；默认 1 次，值词可改成 2/3/5 次。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.BehaviorType = AttackBehaviorType.Pierce;
            token.AcceptsNumericValue = true;
            token.DefaultProjectileCount = 1;
            token.SpreadAngleStep = 0f;
            token.ProjectileDamageMultiplier = 1f;
            token.PierceLifetimeDistanceScalePerCount = 0.2f;
        });

        CreateOrUpdateAsset<ResultTokenData>(ResultFolder + "/DirectDamage.asset", token =>
        {
            token.TokenId = "result_direct_damage";
            token.DisplayText = "击";
            token.Description = "只结算直击伤害。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.ResultType = AttackResultType.DirectDamage;
            token.AcceptsNumericValue = false;
            token.DefaultExplosionRadius = 0f;
            token.ExplosionDamageMultiplier = 0f;
            token.DefaultTriggerCount = 0;
            token.EffectDuration = 0f;
            token.ChildDamageMultiplier = 0f;
        });

        CreateOrUpdateAsset<ResultTokenData>(ResultFolder + "/Explosion.asset", token =>
        {
            token.TokenId = "result_explosion";
            token.DisplayText = "爆";
            token.Description = "命中后爆炸，对半径 48 内敌人造成当次直击伤害的 30%。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.ResultType = AttackResultType.Explosion;
            token.AcceptsNumericValue = false;
            token.DefaultExplosionRadius = 48f;
            token.ExplosionDamageMultiplier = 0.3f;
            token.DefaultTriggerCount = 0;
            token.EffectDuration = 0f;
            token.ChildDamageMultiplier = 0f;
        });

        CreateOrUpdateAsset<ResultTokenData>(ResultFolder + "/Split.asset", token =>
        {
            token.TokenId = "result_split";
            token.DisplayText = "散";
            token.Description = "命中后随机散射子弹；默认 2 发，值词可改成 2/3/5 发。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.ResultType = AttackResultType.Split;
            token.AcceptsNumericValue = true;
            token.DefaultExplosionRadius = 0f;
            token.ExplosionDamageMultiplier = 0f;
            token.DefaultTriggerCount = 2;
            token.EffectDuration = 0f;
            token.ChildDamageMultiplier = 0.5f;
        });

        CreateOrUpdateAsset<ResultTokenData>(ResultFolder + "/Control.asset", token =>
        {
            token.TokenId = "result_control";
            token.DisplayText = "定";
            token.Description = "命中同一敌人累计达到阈值后眩晕 1 秒；默认阈值 5，值词可改成 2/3/5。";
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
            token.ResultType = AttackResultType.StatusEffect;
            token.AcceptsNumericValue = true;
            token.DefaultExplosionRadius = 0f;
            token.ExplosionDamageMultiplier = 0f;
            token.DefaultTriggerCount = 5;
            token.EffectDuration = 1f;
            token.ChildDamageMultiplier = 0f;
        });

        CreateOrUpdateAsset<ValueTokenData>(ValueFolder + "/Value_2.asset", token =>
        {
            token.TokenId = "value_2";
            token.DisplayText = "2";
            token.Description = "把行为或结果词的触发次数改为 2。";
            token.NumericValue = 2f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<ValueTokenData>(ValueFolder + "/Value_3.asset", token =>
        {
            token.TokenId = "value_3";
            token.DisplayText = "3";
            token.Description = "把行为或结果词的触发次数改为 3。";
            token.NumericValue = 3f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<ValueTokenData>(ValueFolder + "/Value_5.asset", token =>
        {
            token.TokenId = "value_5";
            token.DisplayText = "5";
            token.Description = "把行为或结果词的触发次数改为 5。";
            token.NumericValue = 5f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/FireCore.asset");
        CoreTokenData iceCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/IceCore.asset");
        BehaviorTokenData spreadBehavior = AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Spread.asset");
        BehaviorTokenData pierceBehavior = AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Pierce.asset");
        ResultTokenData explosionResult = AssetDatabase.LoadAssetAtPath<ResultTokenData>(ResultFolder + "/Explosion.asset");
        CreateOrUpdateAsset<LinkedTokenData>(LinkedFolder + "/Fire-Spread.asset", item =>
        {
            item.ItemId = "Fire_Spread";
            item.Description = "默认火核 + 散射组合。";
            item.ConfiguredDamageMultiplier = 1f;
            item.PickupDisplayTextOverride = "火散";
            item.SetLinkedTokens(new BaseTokenData[]
            {
                fireCore,
                spreadBehavior,
            });
        });

        CreateOrUpdateAsset<LinkedTokenData>(LinkedFolder + "/Fire-Explosion.asset", item =>
        {
            item.ItemId = "Fire_Explosion";
            item.Description = "默认火核 + 爆炸组合。";
            item.ConfiguredDamageMultiplier = 1f;
            item.PickupDisplayTextOverride = "火爆";
            item.SetLinkedTokens(new BaseTokenData[]
            {
                fireCore,
                explosionResult,
            });
        });

        CreateOrUpdateAsset<LinkedTokenData>(LinkedFolder + "/Ice-Pierce.asset", item =>
        {
            item.ItemId = "Ice_Pierce";
            item.Description = "默认冰核 + 穿透组合。";
            item.ConfiguredDamageMultiplier = 1f;
            item.PickupDisplayTextOverride = "冰透";
            item.SetLinkedTokens(new BaseTokenData[]
            {
                iceCore,
                pierceBehavior,
            });
        });

        CreateOrUpdateAsset<RemnantPickupTokenData>(PickupFolder + "/RemnantPickup.asset", token =>
        {
            token.TokenId = "pickup_remnant";
            token.DisplayText = "Remnant";
            token.Description = "Collect to increase remnant currency stored in save data.";
            token.RemnantAmount = 1;
        });

        CreateOrUpdateAsset<HealingPickupTokenData>(PickupFolder + "/HealingPickup.asset", token =>
        {
            token.TokenId = "pickup_heal";
            token.DisplayText = "Heal";
            token.Description = "Collect to immediately restore player health.";
            token.HealingAmount = 20f;
        });

        CreateOrUpdateAsset<BulletTokenLibrary>(BaseLibraryPath, library =>
        {
            library.SetTokens(new PlaceableTokenData[]
            {
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/EdgeCore.asset"),
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/FireCore.asset"),
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/IceCore.asset"),
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/ThunderCore.asset"),
                AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Bounce.asset"),
                AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Pierce.asset"),
                AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Spread.asset"),
                AssetDatabase.LoadAssetAtPath<ResultTokenData>(ResultFolder + "/Explosion.asset"),
                AssetDatabase.LoadAssetAtPath<ResultTokenData>(ResultFolder + "/Split.asset"),
                AssetDatabase.LoadAssetAtPath<ResultTokenData>(ResultFolder + "/Control.asset"),
                AssetDatabase.LoadAssetAtPath<ValueTokenData>(ValueFolder + "/Value_2.asset"),
                AssetDatabase.LoadAssetAtPath<ValueTokenData>(ValueFolder + "/Value_3.asset"),
                AssetDatabase.LoadAssetAtPath<ValueTokenData>(ValueFolder + "/Value_5.asset"),
            });
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AttackTokenAssetGenerator] Bullet token assets are ready under Assets/Data/BulletTokens.");
    }

    [MenuItem("Tools/Lilith/Bullet/Generate Formal Spell Token Assets")]
    public static void GenerateFormalSpellTokenAssets()
    {
        EnsureFormalFolders();

        CoreTokenData arrow = CreateFormalCore(
            "ArrowCore",
            "core_arrow",
            "箭",
            "基础物理核心。直线飞行，命中后造成稳定直击伤害。",
            AttackCoreType.Arrow,
            6f,
            150f,
            240f);
        CoreTokenData fire = CreateFormalCore(
            "FireCore",
            "core_fire",
            "火",
            "火元素核心。命中后写入点燃状态槽。",
            AttackCoreType.Fire,
            7f,
            128f,
            224f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Ignite, 1f, duration: 2f, strength: 1f) });
        CoreTokenData ice = CreateFormalCore(
            "IceCore",
            "core_ice",
            "冰",
            "冰元素核心。命中后写入冻结状态槽。",
            AttackCoreType.Ice,
            6f,
            120f,
            224f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Freeze, 1f, duration: 1.5f, strength: 1f) });
        CoreTokenData thunder = CreateFormalCore(
            "ThunderCore",
            "core_thunder",
            "雷",
            "雷元素核心。命中后写入失能状态槽。",
            AttackCoreType.Thunder,
            7f,
            140f,
            232f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Disable, 1f, duration: 0.6f, strength: 1f) });
        CoreTokenData rock = CreateFormalCore(
            "RockCore",
            "core_rock",
            "岩",
            "岩石核心。速度偏慢，直伤更高，并写入失能状态槽。",
            AttackCoreType.Rock,
            9f,
            104f,
            208f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Disable, 0.5f, duration: 0.4f, strength: 1f) });
        CoreTokenData blade = CreateFormalCore(
            "BladeCore",
            "core_blade",
            "刃",
            "刃形核心。高速直击，适合搭配穿透或链接。",
            AttackCoreType.Edge,
            8f,
            156f,
            240f);
        CoreTokenData poison = CreateFormalCore(
            "PoisonCore",
            "core_poison",
            "毒",
            "毒性核心。命中后写入腐蚀状态槽。",
            AttackCoreType.Toxin,
            6f,
            118f,
            224f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Corrosion, 1f, duration: 3f, strength: 1f) });
        CoreTokenData shadow = CreateFormalCore(
            "ShadowCore",
            "core_shadow",
            "影",
            "暗影核心。命中后写入标记状态槽。",
            AttackCoreType.Shadow,
            6.5f,
            136f,
            232f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Mark, 1f, duration: 3f, strength: 1f) });
        CoreTokenData water = CreateFormalCore(
            "WaterCore",
            "core_water",
            "水",
            "低伤水系核心。命中后写入潮湿状态槽，可参与水雷反应。",
            AttackCoreType.Water,
            5f,
            126f,
            224f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Wet, 1f, duration: 3f, strength: 1f) });
        CoreTokenData wind = CreateFormalCore(
            "WindCore",
            "core_wind",
            "风",
            "高速低伤核心。命中点产生小范围风压，推开低重量敌人。",
            AttackCoreType.Wind,
            4.5f,
            168f,
            256f);
        wind.WindPressureRadius = 3f;
        wind.WindPressureDistance = 1.5f;
        wind.WindDisplacementWeightLimit = 1f;
        CoreTokenData lightCore = CreateFormalCore(
            "LightCore",
            "core_light",
            "光",
            "特殊穿透核心。可穿过敌人与墙体，只造成衰减直伤，不触发命中后效果。",
            AttackCoreType.Light,
            10f,
            180f,
            320f);
        lightCore.PiercesActorsAndEnvironment = true;
        lightCore.PenetrationDamageMultiplier = 0.7f;
        lightCore.SuppressImpactEffects = true;
        CoreTokenData sheep = CreateFormalCore(
            "SheepCore",
            "core_sheep",
            "羊",
            "变形核心。命中积累变形槽，普通敌人满 3 层后被强控并变色 4 秒。",
            AttackCoreType.Sheep,
            3f,
            120f,
            224f,
            new[] { new SpellStatusApplication(SpellStatusSlot.Polymorph, 1f, threshold: 3f, duration: 4f, strength: 1f) });
        CoreTokenData riddle = CreateFormalCore(
            "RiddleCore",
            "core_riddle",
            "谜",
            "随机核心。每发 projectile 发射时随机变为箭/火/冰/雷/岩/刃/毒/影之一。",
            AttackCoreType.Riddle,
            6f,
            140f,
            232f);

        BehaviorTokenData spread = CreateFormalBehavior(
            "Spread",
            "behavior_spread_formal",
            "散",
            "改为散射发射；默认 3 发，可由值词改数量。",
            AttackBehaviorType.Spread,
            true,
            SpellValueParameterKind.Count,
            3,
            12f,
            0.5f);
        BehaviorTokenData pierce = CreateFormalBehavior(
            "Pierce",
            "behavior_pierce_formal",
            "穿",
            "命中敌人后继续飞行；默认穿透 1 次，可由值词改次数。",
            AttackBehaviorType.Pierce,
            true,
            SpellValueParameterKind.Count,
            1);
        BehaviorTokenData bounce = CreateFormalBehavior(
            "Bounce",
            "behavior_bounce_formal",
            "弹",
            "命中墙体后反弹；默认 1 次，可由值词改次数。",
            AttackBehaviorType.Bounce,
            true,
            SpellValueParameterKind.Count,
            1);
        BehaviorTokenData homing = CreateFormalBehavior(
            "Homing",
            "behavior_homing",
            "追",
            "持续向最近合法目标转向。",
            AttackBehaviorType.Homing,
            false,
            SpellValueParameterKind.None,
            1);
        BehaviorTokenData chain = CreateFormalBehavior(
            "Chain",
            "behavior_chain",
            "链",
            "命中主目标后向附近未命中过的敌人传导 50% 直伤；默认 1 跳，可由值词改跳数。",
            AttackBehaviorType.Chain,
            true,
            SpellValueParameterKind.Count,
            1);
        BehaviorTokenData stasis = CreateFormalBehavior(
            "Stasis",
            "behavior_stasis",
            "滞",
            "停在发射点持续存在；默认 1.5 秒，可由值词改持续时间。只做一次出生点直击检测。",
            AttackBehaviorType.Stasis,
            true,
            SpellValueParameterKind.Duration,
            1,
            defaultBehaviorParameter: 1.5f);
        BehaviorTokenData rush = CreateFormalBehavior(
            "Rush",
            "behavior_rush",
            "驰",
            "飞行中逐渐加速；默认强度 1，可由值词改加速强度。",
            AttackBehaviorType.Rush,
            true,
            SpellValueParameterKind.Strength,
            1,
            defaultBehaviorParameter: 1f);
        BehaviorTokenData slow = CreateFormalBehavior(
            "Slow",
            "behavior_slow",
            "缓",
            "飞行中逐渐减速；默认强度 1，可由值词改减速强度。",
            AttackBehaviorType.Slow,
            true,
            SpellValueParameterKind.Strength,
            1,
            defaultBehaviorParameter: 1f);
        BehaviorTokenData snake = CreateFormalBehavior(
            "Snake",
            "behavior_snake",
            "蛇",
            "沿初始方向做蛇形摆动；默认强度 1，可由值词改摆幅。",
            AttackBehaviorType.Snake,
            true,
            SpellValueParameterKind.Strength,
            1,
            defaultBehaviorParameter: 1f);
        BehaviorTokenData wander = CreateFormalBehavior(
            "Wander",
            "behavior_wander",
            "游",
            "沿飞行方向确定性漂移；默认强度 1，可由值词改漂移强度。",
            AttackBehaviorType.Wander,
            true,
            SpellValueParameterKind.Strength,
            1,
            defaultBehaviorParameter: 1f);
        BehaviorTokenData behaviorSplit = CreateFormalBehavior(
            "BehaviorSplit",
            "behavior_split",
            "分",
            "飞行期间均匀分裂；默认 1 次，可由值词改分裂次数。",
            AttackBehaviorType.Split,
            true,
            SpellValueParameterKind.Count,
            1,
            defaultBehaviorParameter: 1f);
        BehaviorTokenData spin = CreateFormalBehavior(
            "Spin",
            "behavior_spin",
            "旋",
            "围绕施法者水平环绕；默认半径 3，可由值词改环绕半径。",
            AttackBehaviorType.Spin,
            true,
            SpellValueParameterKind.Radius,
            1,
            defaultBehaviorParameter: 3f);

        ResultTokenData explosion = CreateFormalResult(
            "Explosion",
            "result_explosion_formal",
            "爆",
            "命中后爆炸，对范围内敌人造成直伤比例伤害。",
            AttackResultType.Explosion,
            true,
            SpellValueParameterKind.Radius,
            explosionRadius: 48f,
            explosionMultiplier: 0.3f);
        ResultTokenData split = CreateFormalResult(
            "Split",
            "result_split_formal",
            "裂",
            "命中后生成只继承核心状态倾向的派生弹；默认 2 枚，可由值词改数量。",
            AttackResultType.Split,
            true,
            SpellValueParameterKind.Count,
            triggerCount: 2,
            childMultiplier: 0.5f);
        ResultTokenData healing = CreateFormalResult(
            "Healing",
            "result_healing",
            "愈",
            "命中后按治疗结算，可由值词扩大治疗范围。",
            AttackResultType.Healing,
            true,
            SpellValueParameterKind.Radius,
            effectRadius: 0f);
        ResultTokenData control = CreateFormalStatusResult(
            "Control",
            "result_control_formal",
            "定",
            "命中后积累失能控制；默认阈值 5，持续 1 秒。",
            SpellStatusSlot.Disable,
            1f,
            acceptsValue: true,
            valueKind: SpellValueParameterKind.Count,
            triggerCount: 5,
            duration: 1f);
        ResultTokenData burn = CreateFormalStatusResult("Burn", "result_burn", "燃", "命中后写入点燃状态槽。", SpellStatusSlot.Ignite, 2f);
        ResultTokenData bind = CreateFormalStatusResult("Bind", "result_bind", "缚", "命中后写入绑缚状态槽。", SpellStatusSlot.Bind, 2f, duration: 1.5f);
        ResultTokenData corrode = CreateFormalStatusResult("Corrode", "result_corrode", "蚀", "命中后写入腐蚀状态槽。", SpellStatusSlot.Corrosion, 2f, duration: 3f);
        ResultTokenData mark = CreateFormalStatusResult("Mark", "result_mark", "标", "命中后写入标记状态槽。", SpellStatusSlot.Mark, 1f, duration: 3f);
        ResultTokenData wet = CreateFormalStatusResult("Wet", "result_wet", "潮", "命中后写入潮湿状态槽。", SpellStatusSlot.Wet, 2f, duration: 3f);
        ResultTokenData shock = CreateFormalStatusResult("Shock", "result_shock", "震", "命中后写入失能状态槽。", SpellStatusSlot.Disable, 2f, duration: 0.8f);
        ResultTokenData drain = CreateFormalResult(
            "Drain",
            "result_drain",
            "汲",
            "命中后按直击伤害比例治疗施法者；值词调整汲取强度。",
            AttackResultType.Drain,
            true,
            SpellValueParameterKind.Strength,
            effectStrength: 1f);
        ResultTokenData shield = CreateFormalResult(
            "Shield",
            "result_shield",
            "护",
            "命中后按直击伤害比例给施法者添加 6 秒吸收盾；值词调整护盾强度。",
            AttackResultType.Shield,
            true,
            SpellValueParameterKind.Strength,
            effectStrength: 1f,
            shieldDuration: 6f);
        ResultTokenData leave = CreateFormalResult(
            "Leave",
            "result_leave",
            "留",
            "命中点留下持续 3 秒、半径 3 的伤害/状态场；值词调整持续时间。",
            AttackResultType.Leave,
            true,
            SpellValueParameterKind.Duration,
            effectRadius: 3f,
            duration: 3f,
            areaTickSeconds: 0.5f,
            areaDamageMultiplier: 0.25f);
        ResultTokenData push = CreateFormalResult(
            "Push",
            "result_push",
            "斥",
            "命中点半径 3 内按强度阈值推开低重量敌人；值词调整强度。",
            AttackResultType.Push,
            true,
            SpellValueParameterKind.Strength,
            effectRadius: 3f,
            effectStrength: 1f);
        ResultTokenData pull = CreateFormalResult(
            "Pull",
            "result_pull",
            "吸",
            "命中点半径 3 内按强度阈值拉近低重量敌人；值词调整强度。",
            AttackResultType.Pull,
            true,
            SpellValueParameterKind.Strength,
            effectRadius: 3f,
            effectStrength: 1f);
        ResultTokenData confuse = CreateFormalResult(
            "Confuse",
            "result_confuse",
            "混",
            "命中时随机触发一个已实现 Result；不消费值词。",
            AttackResultType.Confuse,
            false,
            SpellValueParameterKind.None);
        confuse.SetRandomResultCandidates(
            explosion,
            split,
            healing,
            control,
            burn,
            bind,
            corrode,
            mark,
            wet,
            shock,
            drain,
            shield,
            leave,
            push,
            pull);

        ModifierTokenData haste = CreateFormalModifier("Haste", "modifier_haste", "疾", "提高速度。", new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "*=1.25"));
        ModifierTokenData heavy = CreateFormalModifier("Heavy", "modifier_heavy", "重", "提高伤害但降低速度。", new TokenModifierDefinition(TokenModifierTarget.Damage, "*=1.25"), new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "*=0.85"));
        ModifierTokenData sharp = CreateFormalModifier("Sharp", "modifier_sharp", "锐", "提高伤害并略收窄碰撞半径。", new TokenModifierDefinition(TokenModifierTarget.Damage, "*=1.15"), new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=0.9"));
        ModifierTokenData field = CreateFormalModifier("Field", "modifier_field", "域", "放大影响范围。", new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=1.25"), new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=1.1"));
        ModifierTokenData longDuration = CreateFormalModifier("Long", "modifier_long", "久", "延长寿命与射程。", new TokenModifierDefinition(TokenModifierTarget.MaxLifetime, "*=1.35"), new TokenModifierDefinition(TokenModifierTarget.MaxTravelDistance, "*=1.2"));
        ModifierTokenData shortRange = CreateFormalModifier("Short", "modifier_short", "短", "缩短射程并提高伤害。", new TokenModifierDefinition(TokenModifierTarget.MaxTravelDistance, "*=0.65"), new TokenModifierDefinition(TokenModifierTarget.Damage, "*=1.2"));
        ModifierTokenData light = CreateFormalModifier("Light", "modifier_light", "轻", "提高速度并降低伤害。", new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "*=1.2"), new TokenModifierDefinition(TokenModifierTarget.Damage, "*=0.9"));
        ModifierTokenData cold = CreateFormalModifier("Cold", "modifier_cold", "冷", "延长结果持续时间。", new TokenModifierDefinition(TokenModifierTarget.ResultDuration, "*=1.2"));
        ModifierTokenData fierce = CreateFormalModifier("Fierce", "modifier_fierce", "烈", "提高结果倍率与伤害。", new TokenModifierDefinition(TokenModifierTarget.ResultMultiplier, "*=1.2"), new TokenModifierDefinition(TokenModifierTarget.Damage, "*=1.1"));
        ModifierTokenData focus = CreateFormalModifier("Focus", "modifier_focus", "聚", "收窄范围并提高结果倍率。", new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=0.75"), new TokenModifierDefinition(TokenModifierTarget.ResultMultiplier, "*=1.2"));
        ModifierTokenData count = CreateFormalModifier("Count", "modifier_count", "数", "提高结果数量。", new TokenModifierDefinition(TokenModifierTarget.ResultCount, "+=2"));
        ModifierTokenData amplify = CreateFormalModifier("Amplify", "modifier_amplify", "幅", "提高结果倍率。", new TokenModifierDefinition(TokenModifierTarget.ResultMultiplier, "*=1.5"));
        ModifierTokenData expand = CreateFormalModifier("Expand", "modifier_expand", "放", "放大弹体与命中半径。", new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=1.15"), new TokenModifierDefinition(TokenModifierTarget.ImpactRadiusMultiplier, "*=1.15"));
        ModifierTokenData stable = CreateFormalModifier("Stable", "modifier_stable", "稳", "稳定施法：降低散射/分叉/激活扇形和蛇/游扰动，但略降伤害。", new TokenModifierDefinition(TokenModifierTarget.AngleSpreadMultiplier, "*=0.5"), new TokenModifierDefinition(TokenModifierTarget.MovementVarianceMultiplier, "*=0.5"), new TokenModifierDefinition(TokenModifierTarget.Damage, "*=0.9"));
        ModifierTokenData wild = CreateFormalModifier("Wild", "modifier_wild", "狂", "发射时掉血，提高伤害，并增加本次法术书能量消耗。", new TokenModifierDefinition(TokenModifierTarget.CasterHealthCost, "+=5"), new TokenModifierDefinition(TokenModifierTarget.Damage, "*=1.35"), new TokenModifierDefinition(TokenModifierTarget.EnergyCostMultiplier, "*=1.5"));
        ModifierTokenData greedy = CreateFormalModifier("Greedy", "modifier_greedy", "贪", "发射时掉血，若本次法术击败敌人则提高该敌人的所有掉落概率。", new TokenModifierDefinition(TokenModifierTarget.CasterHealthCost, "+=5"), new TokenModifierDefinition(TokenModifierTarget.DropChanceMultiplierOnKill, "*=2"));
        ModifierTokenData urgent = CreateFormalModifier("Urgent", "modifier_urgent", "急", "减少当前法术书施法间隔。", new TokenModifierDefinition(TokenModifierTarget.CastCooldownMultiplier, "*=0.8"));
        ModifierTokenData source = CreateFormalModifier("Source", "modifier_source", "源", "减少发射时法术书结算的能量消耗。", new TokenModifierDefinition(TokenModifierTarget.EnergyCostMultiplier, "*=0.5"));
        ModifierTokenData chaos = CreateFormalModifier("Chaos", "modifier_chaos", "乱", "每次发射随机实现一个已实现 Modifier；不消费值词。");
        chaos.SetRandomModifierCandidates(
            haste,
            heavy,
            sharp,
            field,
            longDuration,
            shortRange,
            light,
            cold,
            fierce,
            focus,
            count,
            amplify,
            expand,
            stable,
            wild,
            greedy,
            urgent,
            source);

        ValueTokenData one = CreateFormalValue("One", "value_one", "一", "数值 1。", 1f, SpellValueMode.Number, SpellValueScalePreset.None);
        ValueTokenData two = CreateFormalValue("Two", "value_two", "二", "数值 2。", 2f, SpellValueMode.Number, SpellValueScalePreset.None);
        ValueTokenData three = CreateFormalValue("Three", "value_three", "三", "数值 3。", 3f, SpellValueMode.Number, SpellValueScalePreset.None);
        ValueTokenData five = CreateFormalValue("Five", "value_five", "五", "数值 5。", 5f, SpellValueMode.Number, SpellValueScalePreset.None);
        ValueTokenData eight = CreateFormalValue("Eight", "value_eight", "八", "数值 8。", 8f, SpellValueMode.Number, SpellValueScalePreset.None);
        ValueTokenData half = CreateFormalValue("Half", "value_half", "半", "按当前参数的一半解析。", 0.5f, SpellValueMode.Multiplier, SpellValueScalePreset.None);
        ValueTokenData doubled = CreateFormalValue("Double", "value_double", "倍", "按当前参数的两倍解析。", 2f, SpellValueMode.Multiplier, SpellValueScalePreset.None);
        ValueTokenData giant = CreateFormalValue("Giant", "value_giant", "巨", "按大型预设解析。", 5f, SpellValueMode.ScalePreset, SpellValueScalePreset.Large);
        ValueTokenData zero = CreateFormalValue("Zero", "value_zero", "零", "按允许零值的参数解析为 0。", 0f, SpellValueMode.ScalePreset, SpellValueScalePreset.Zero);

        MulticastTokenData dual = CreateFormalMulticast("Dual", "multicast_dual", "双", "同轮释放 2 个 projectile segment。", 2, SpellCastPattern.Simultaneous);
        MulticastTokenData triple = CreateFormalMulticast("Triple", "multicast_triple", "叁", "同轮释放 3 个 projectile segment。", 3, SpellCastPattern.Simultaneous);
        MulticastTokenData sequence = CreateFormalMulticast("Sequence", "multicast_sequence", "序", "顺序释放 2 个 projectile segment。", 2, SpellCastPattern.Sequential);
        MulticastTokenData fork = CreateFormalMulticast("Fork", "multicast_fork", "叉", "以分叉角度释放 2 个 projectile segment。", 2, SpellCastPattern.Fork);
        MulticastTokenData orbit = CreateFormalMulticast("Orbit", "multicast_orbit", "绕", "主弹携带 1 枚环绕弹释放 2 个 projectile segment。", 2, SpellCastPattern.Orbit);

        TriggerTokenData onHit = CreateFormalTrigger("OnHit", "trigger_on_hit", "触", "命中后释放后续 payload。", SpellTriggerType.OnHit, SpellTriggerParameterKind.None, 0f);
        TriggerTokenData onTimer = CreateFormalTrigger("OnTimer", "trigger_timer", "时", "计时后在 projectile 当前位置释放 payload。", SpellTriggerType.OnTimer, SpellTriggerParameterKind.TimeSeconds, 1f);
        TriggerTokenData onExpire = CreateFormalTrigger("OnExpire", "trigger_expire", "终", "projectile 消失时释放 payload。", SpellTriggerType.OnExpire, SpellTriggerParameterKind.None, 0f);
        TriggerTokenData onKill = CreateFormalTrigger("OnKill", "trigger_kill", "灭", "本 projectile 造成击杀时在死亡目标位置释放 payload。", SpellTriggerType.OnKill, SpellTriggerParameterKind.None, 0f);
        TriggerTokenData onDistance = CreateFormalTrigger("OnDistance", "trigger_distance", "程", "飞行距离达到参数后释放 payload。", SpellTriggerType.OnDistance, SpellTriggerParameterKind.Distance, 5f);
        TriggerTokenData onProximity = CreateFormalTrigger("OnProximity", "trigger_proximity", "近", "接近目标到参数半径内释放 payload。", SpellTriggerType.OnProximity, SpellTriggerParameterKind.Radius, 3f);

        PlaceableTokenData[] playableTokens =
        {
            arrow, fire, ice, thunder, rock, blade, poison, shadow, water, wind, lightCore, sheep, riddle,
            spread, pierce, bounce, homing, chain, stasis, rush, slow, snake, wander, behaviorSplit, spin,
            explosion, split, healing, control, burn, bind, corrode, mark, wet, shock, drain, shield, leave, push, pull, confuse,
            haste, heavy, sharp, field, longDuration, shortRange, light, cold, fierce, focus, count, amplify, expand, stable, wild, greedy, urgent, source, chaos,
            one, two, three, five, eight, half, doubled, giant, zero,
            dual, triple, sequence, fork, orbit,
            onHit, onTimer, onExpire, onKill, onDistance, onProximity,
        };

        CreateOrUpdateAsset<BulletTokenLibrary>(PlayableStagingLibraryPath, library =>
        {
            library.SetTokens(playableTokens);
        });

        PlaceableTokenData[] hiddenTokens =
        {
            CreatePrototype("Core_Mirror", "prototype_core_mirror", "镜", "Core", "计划语义：功能核心，无直伤，命中后在镜像方向额外执行一次 Result 或 payload。", "需要镜像轴规则、递归禁止和 payload 复制安全策略。"),
            CreatePrototype("Core_Summon", "prototype_core_summon", "召", "Core", "计划语义：发射传送门或召唤弹，命中后生成召唤物，召唤物不继承 payload。", "需要召唤物实体、生命周期、归属和性能上限。"),
            CreatePrototype("Result_Illusion", "prototype_result_illusion", "幻", "Result", "计划语义：击杀或命中后生成幻象/传送到目标位置，用于位移和欺骗。", "需要幻象对象、仇恨系统和安全落点规则。"),
            CreatePrototype("Result_Replace", "prototype_result_replace", "替", "Result", "计划语义：命中后与目标交换位置。", "需要玩家/敌人位置合法性、Boss/精英免疫和碰撞恢复。"),
            CreatePrototype("Result_Puppet", "prototype_result_puppet", "傀", "Result", "计划语义：击杀目标后复活为短暂傀儡，通常与 `灭` Trigger 搭配。", "需要傀儡实体、阵营转换、寿命和强度来源规则。"),
        };

        CreateOrUpdateAsset<BulletTokenLibrary>(HiddenPrototypeLibraryPath, library =>
        {
            library.SetTokens(hiddenTokens);
            for (int i = 0; i < hiddenTokens.Length; i++)
            {
                library.SetTokenWeight(hiddenTokens[i], 0f);
            }
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AttackTokenAssetGenerator] Formal spell token staging and hidden libraries are ready.");
    }

    private static T CreateOrUpdateAsset<T>(string assetPath, Action<T> configure) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        configure?.Invoke(asset);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void EnsureFormalFolders()
    {
        EnsureFolderChain(FormalCoreFolder);
        EnsureFolderChain(FormalBehaviorFolder);
        EnsureFolderChain(FormalResultFolder);
        EnsureFolderChain(FormalModifierFolder);
        EnsureFolderChain(FormalValueFolder);
        EnsureFolderChain(FormalMulticastFolder);
        EnsureFolderChain(FormalTriggerFolder);
        EnsureFolderChain(FormalPrototypeFolder);
        EnsureFolderChain(TokenLibFolder);
    }

    private static CoreTokenData CreateFormalCore(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        AttackCoreType coreType,
        float damage,
        float projectileSpeed,
        float maxTravelDistance,
        SpellStatusApplication[] statusApplications = null)
    {
        return CreateOrUpdateAsset<CoreTokenData>(FormalCoreFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.SetBulletTextOverride(true, displayText);
            token.CoreType = coreType;
            token.DefaultValueType = AttackValueType.oneShot;
            token.Damage = damage;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = projectileSpeed;
            token.MaxTravelDistance = maxTravelDistance;
            token.MaxLifetime = maxTravelDistance / Mathf.Max(1f, projectileSpeed) + 0.15f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
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
            token.PiercesActorsAndEnvironment = false;
            token.PenetrationDamageMultiplier = 1f;
            token.SuppressImpactEffects = false;
            token.WindPressureRadius = 0f;
            token.WindPressureDistance = 0f;
            token.WindDisplacementWeightLimit = 1f;
            token.SetStatusApplications(statusApplications ?? Array.Empty<SpellStatusApplication>());
        });
    }

    private static BehaviorTokenData CreateFormalBehavior(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        AttackBehaviorType behaviorType,
        bool acceptsValue,
        SpellValueParameterKind valueKind,
        int defaultCount,
        float spreadAngleStep = 0f,
        float damageMultiplier = 1f,
        float defaultBehaviorParameter = 0f)
    {
        return CreateOrUpdateAsset<BehaviorTokenData>(FormalBehaviorFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.BehaviorType = behaviorType;
            token.AcceptsNumericValue = acceptsValue;
            token.ConfiguredValueParameterKind = valueKind;
            token.DefaultProjectileCount = defaultCount;
            token.SpreadAngleStep = spreadAngleStep;
            token.ProjectileDamageMultiplier = damageMultiplier;
            token.PierceLifetimeDistanceScalePerCount = 0.2f;
            token.DefaultBehaviorParameter = defaultBehaviorParameter;
        });
    }

    private static ResultTokenData CreateFormalResult(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        AttackResultType resultType,
        bool acceptsValue,
        SpellValueParameterKind valueKind,
        float explosionRadius = 0f,
        float explosionMultiplier = 0f,
        float effectRadius = 0f,
        int triggerCount = 0,
        float duration = 0f,
        float childMultiplier = 0f,
        float effectStrength = 1f,
        float areaTickSeconds = 0.5f,
        float areaDamageMultiplier = 0.25f,
        float shieldDuration = 6f)
    {
        return CreateOrUpdateAsset<ResultTokenData>(FormalResultFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.ResultType = resultType;
            token.AcceptsNumericValue = acceptsValue;
            token.ConfiguredValueParameterKind = valueKind;
            token.DefaultExplosionRadius = explosionRadius;
            token.ExplosionDamageMultiplier = explosionMultiplier;
            token.DefaultEffectRadius = effectRadius;
            token.DefaultTriggerCount = triggerCount;
            token.EffectDuration = duration;
            token.ChildDamageMultiplier = childMultiplier;
            token.DefaultEffectStrength = effectStrength;
            token.AreaTickSeconds = areaTickSeconds;
            token.AreaDamageMultiplier = areaDamageMultiplier;
            token.ShieldDuration = shieldDuration;
            token.SetStatusApplications(Array.Empty<SpellStatusApplication>());
            token.SetRandomResultCandidates(Array.Empty<ResultTokenData>());
        });
    }

    private static ResultTokenData CreateFormalStatusResult(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        SpellStatusSlot statusSlot,
        float amount,
        bool acceptsValue = false,
        SpellValueParameterKind valueKind = SpellValueParameterKind.None,
        int triggerCount = 0,
        float duration = 0f)
    {
        return CreateOrUpdateAsset<ResultTokenData>(FormalResultFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.ResultType = AttackResultType.StatusEffect;
            token.AcceptsNumericValue = acceptsValue;
            token.ConfiguredValueParameterKind = valueKind;
            token.DefaultExplosionRadius = 0f;
            token.ExplosionDamageMultiplier = 0f;
            token.DefaultEffectRadius = 0f;
            token.DefaultTriggerCount = triggerCount;
            token.EffectDuration = duration;
            token.ChildDamageMultiplier = 0f;
            token.DefaultEffectStrength = 1f;
            token.AreaTickSeconds = 0.5f;
            token.AreaDamageMultiplier = 0.25f;
            token.ShieldDuration = 6f;
            token.SetStatusApplications(new SpellStatusApplication(statusSlot, amount, duration: duration, strength: 1f));
            token.SetRandomResultCandidates(Array.Empty<ResultTokenData>());
        });
    }

    private static ModifierTokenData CreateFormalModifier(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        params TokenModifierDefinition[] modifiers)
    {
        return CreateOrUpdateAsset<ModifierTokenData>(FormalModifierFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.SetModifiers(modifiers);
            token.IsRandomModifier = false;
            token.SetRandomModifierCandidates(Array.Empty<ModifierTokenData>());
        });
    }

    private static ValueTokenData CreateFormalValue(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        float numericValue,
        SpellValueMode valueMode,
        SpellValueScalePreset scalePreset)
    {
        return CreateOrUpdateAsset<ValueTokenData>(FormalValueFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.NumericValue = numericValue;
            token.ValueMode = valueMode;
            token.ScalePreset = scalePreset;
        });
    }

    private static MulticastTokenData CreateFormalMulticast(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        int castCount,
        SpellCastPattern castPattern)
    {
        return CreateOrUpdateAsset<MulticastTokenData>(FormalMulticastFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.CastCount = castCount;
            token.CastPattern = castPattern;
            token.SequentialIntervalSeconds = 0.12f;
            token.PatternAngleStep = 18f;
        });
    }

    private static TriggerTokenData CreateFormalTrigger(
        string fileName,
        string tokenId,
        string displayText,
        string description,
        SpellTriggerType triggerType,
        SpellTriggerParameterKind parameterKind,
        float defaultValue)
    {
        return CreateOrUpdateAsset<TriggerTokenData>(FormalTriggerFolder + "/" + fileName + ".asset", token =>
        {
            ConfigureBaseToken(token, tokenId, displayText, description);
            token.TriggerType = triggerType;
            token.ConfiguredParameterKind = parameterKind;
            token.DefaultParameterValue = defaultValue;
        });
    }

    private static PrototypeTokenData CreatePrototype(
        string fileName,
        string tokenId,
        string displayText,
        string category,
        string description,
        string reason)
    {
        return CreateOrUpdateAsset<PrototypeTokenData>(FormalPrototypeFolder + "/" + fileName + ".asset", token =>
        {
            token.TokenId = tokenId;
            token.DisplayText = displayText;
            token.PrototypeCategory = category;
            token.Description = description;
            token.UnimplementedReason = reason;
        });
    }

    private static void ConfigureBaseToken(BaseTokenData token, string tokenId, string displayText, string description)
    {
        token.TokenId = tokenId;
        token.DisplayText = displayText;
        token.Description = description;
        token.SetBulletTextOverride(false, string.Empty);
        token.SetModifiers(Array.Empty<TokenModifierDefinition>());
    }

    private static void EnsureFolderChain(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        if (parts.Length <= 1)
        {
            return;
        }

        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = currentPath + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }
}
