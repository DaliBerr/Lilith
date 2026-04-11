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
    private const string LinkedFolder = RootFolder + "/Linked";
    private const string PickupFolder = RootFolder + "/Pickup";
    private const string LibraryPath = RootFolder + "/BulletTokenLibrary.asset";

    [MenuItem("Tools/Lilith/Bullet/Generate Default Token Assets")]
    public static void GenerateDefaultAssets()
    {
        EnsureFolderChain(CoreFolder);
        EnsureFolderChain(BehaviorFolder);
        EnsureFolderChain(ResultFolder);
        EnsureFolderChain(ValueFolder);
        EnsureFolderChain(LinkedFolder);
        EnsureFolderChain(PickupFolder);

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/FireCore.asset", token =>
        {
            token.TokenId = "fire_core";
            token.DisplayText = "Fire";
            token.Description = "Base fire core.";
            token.CoreType = AttackCoreType.Fire;
            token.Damage = 1f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 320f;
            token.MaxLifetime = 2f;
            token.MaxTravelDistance = 512f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.SetModifiers(new[]
            {
                new TokenModifierDefinition(TokenModifierTarget.TextColor, "=Color.red"),
            });
        });

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/IceCore.asset", token =>
        {
            token.TokenId = "ice_core";
            token.DisplayText = "Ice";
            token.Description = "Base ice core.";
            token.CoreType = AttackCoreType.Ice;
            token.Damage = 1f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 320f;
            token.MaxLifetime = 2f;
            token.MaxTravelDistance = 512f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/ThunderCore.asset", token =>
        {
            token.TokenId = "thunder_core";
            token.DisplayText = "Thunder";
            token.Description = "Base thunder core.";
            token.CoreType = AttackCoreType.Thunder;
            token.Damage = 1f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 320f;
            token.MaxLifetime = 2f;
            token.MaxTravelDistance = 512f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<CoreTokenData>(CoreFolder + "/EdgeCore.asset", token =>
        {
            token.TokenId = "edge_core";
            token.DisplayText = "Edge";
            token.Description = "Base edge core.";
            token.CoreType = AttackCoreType.Edge;
            token.Damage = 1f;
            token.ProjectileLife = 1;
            token.ImpactLifeCost = 1;
            token.ProjectileSpeed = 320f;
            token.MaxLifetime = 2f;
            token.MaxTravelDistance = 512f;
            token.ImpactMask = Physics.DefaultRaycastLayers;
            token.SetModifiers(new[]
            {
                new TokenModifierDefinition(TokenModifierTarget.ScaleMultiplier, "*=0.8"),
                new TokenModifierDefinition(TokenModifierTarget.ProjectileSpeed, "+=10f"),
            });
        });

        CreateOrUpdateAsset<BehaviorTokenData>(BehaviorFolder + "/Straight.asset", token =>
        {
            token.TokenId = "behavior_straight";
            token.DisplayText = "Straight";
            token.Description = "Default straight projectile.";
            token.BehaviorType = AttackBehaviorType.Straight;
            token.AcceptsNumericValue = false;
            token.DefaultProjectileCount = 1;
            token.SpreadAngleStep = 0f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<BehaviorTokenData>(BehaviorFolder + "/Spread.asset", token =>
        {
            token.TokenId = "behavior_spread";
            token.DisplayText = "Spread";
            token.Description = "Symmetric spread shot.";
            token.BehaviorType = AttackBehaviorType.Spread;
            token.AcceptsNumericValue = true;
            token.DefaultProjectileCount = 3;
            token.SpreadAngleStep = 12f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<ResultTokenData>(ResultFolder + "/DirectDamage.asset", token =>
        {
            token.TokenId = "result_direct_damage";
            token.DisplayText = "Hit";
            token.Description = "Single target direct damage.";
            token.ResultType = AttackResultType.DirectDamage;
            token.AcceptsNumericValue = false;
            token.DefaultExplosionRadius = 0f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<ResultTokenData>(ResultFolder + "/Explosion.asset", token =>
        {
            token.TokenId = "result_explosion";
            token.DisplayText = "Boom";
            token.Description = "Direct damage plus explosion damage.";
            token.ResultType = AttackResultType.Explosion;
            token.AcceptsNumericValue = true;
            token.DefaultExplosionRadius = 2f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<ValueTokenData>(ValueFolder + "/Value_2.asset", token =>
        {
            token.TokenId = "value_2";
            token.DisplayText = "2";
            token.Description = "Numeric token 2.";
            token.NumericValue = 2f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<ValueTokenData>(ValueFolder + "/Value_3.asset", token =>
        {
            token.TokenId = "value_3";
            token.DisplayText = "3";
            token.Description = "Numeric token 3.";
            token.NumericValue = 3f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CreateOrUpdateAsset<ValueTokenData>(ValueFolder + "/Value_5.asset", token =>
        {
            token.TokenId = "value_5";
            token.DisplayText = "5";
            token.Description = "Numeric token 5.";
            token.NumericValue = 5f;
            token.SetModifiers(Array.Empty<TokenModifierDefinition>());
        });

        CoreTokenData fireCore = AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/FireCore.asset");
        ResultTokenData directDamage = AssetDatabase.LoadAssetAtPath<ResultTokenData>(ResultFolder + "/DirectDamage.asset");
        LinkedTokenData fireSpread = null;
        CreateOrUpdateAsset<LinkedTokenData>(LinkedFolder + "/Fire-Spread.asset", item =>
        {
            item.ItemId = "Fire_Spread";
            item.Description = string.Empty;
            item.ConfiguredDamageMultiplier = 1.2f;
            item.PickupDisplayTextOverride = "火";
            item.SetLinkedTokens(new BaseTokenData[]
            {
                fireCore,
                directDamage,
            });
            fireSpread = item;
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

        CreateOrUpdateAsset<BulletTokenLibrary>(LibraryPath, library =>
        {
            library.SetTokens(new PlaceableTokenData[]
            {
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/FireCore.asset"),
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/IceCore.asset"),
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/ThunderCore.asset"),
                AssetDatabase.LoadAssetAtPath<CoreTokenData>(CoreFolder + "/EdgeCore.asset"),
                AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Straight.asset"),
                AssetDatabase.LoadAssetAtPath<BehaviorTokenData>(BehaviorFolder + "/Spread.asset"),
                AssetDatabase.LoadAssetAtPath<ResultTokenData>(ResultFolder + "/DirectDamage.asset"),
                AssetDatabase.LoadAssetAtPath<ResultTokenData>(ResultFolder + "/Explosion.asset"),
                AssetDatabase.LoadAssetAtPath<ValueTokenData>(ValueFolder + "/Value_2.asset"),
                AssetDatabase.LoadAssetAtPath<ValueTokenData>(ValueFolder + "/Value_3.asset"),
                AssetDatabase.LoadAssetAtPath<ValueTokenData>(ValueFolder + "/Value_5.asset"),
                fireSpread ?? AssetDatabase.LoadAssetAtPath<LinkedTokenData>(LinkedFolder + "/Fire-Spread.asset"),
                AssetDatabase.LoadAssetAtPath<RemnantPickupTokenData>(PickupFolder + "/RemnantPickup.asset"),
                AssetDatabase.LoadAssetAtPath<HealingPickupTokenData>(PickupFolder + "/HealingPickup.asset"),
            });
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AttackTokenAssetGenerator] Default bullet token assets and BulletTokenLibrary are ready under Assets/Data/BulletTokens.");
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
