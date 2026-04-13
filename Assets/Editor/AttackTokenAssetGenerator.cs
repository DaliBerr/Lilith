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
        BehaviorTokenData spreadBehavior = AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Spread.asset");
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

    private static void CreateOrUpdateAsset<T>(string assetPath, Action<T> configure) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        configure?.Invoke(asset);
        EditorUtility.SetDirty(asset);
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
