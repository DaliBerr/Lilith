using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TMPro;

public sealed class EnemyDefinitionBinderTests
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
    public void ApplyDefinition_BindsEnemyNameAndVisuals()
    {
        EnemyDefinitionBinder binder = CreateBoundEnemyShell(
            out BaseCharEnemyNorm1 enemy,
            out CharEnemyMovement movement,
            out EnemyMeleeAttacker meleeAttacker,
            out EnemyRangedTokenAttacker rangedTokenAttacker,
            out EnemySummoner summoner,
            out TMP_Text glyphText,
            out SpriteRenderer runeBaseRenderer,
            out SpriteRenderer groundShadowRenderer);

        Sprite runeBaseSprite = CreateSprite("RuneBaseSprite");
        Sprite groundShadowSprite = CreateSprite("GroundShadowSprite");
        EnemyDefinition definition = CreateEnemyDefinition(
            "Norm1",
            binder,
            EnemyMovementKind.ChaseTarget,
            EnemyAttackKind.MeleeContact,
            null,
            new EnemyDefinition.EnemyVisualDefinition
            {
                glyphText = "坚",
                glyphColor = Color.cyan,
                runeBaseSprite = runeBaseSprite,
                runeBaseTint = new Color(0.7f, 0.8f, 1f, 0.5f),
                groundShadowSprite = groundShadowSprite,
                groundShadowTint = new Color(0f, 0f, 0f, 0.3f),
            });

        Assert.That(binder.ApplyDefinition(definition), Is.True);

        Assert.That(enemy.Definition, Is.SameAs(definition));
        Assert.That(enemy.EnemyName, Is.EqualTo("Norm1"));
        Assert.That(movement.enabled, Is.True);
        Assert.That(meleeAttacker.enabled, Is.True);
        Assert.That(rangedTokenAttacker.enabled, Is.False);
        Assert.That(summoner.enabled, Is.False);
        Assert.That(glyphText.text, Is.EqualTo("坚"));
        Assert.That(glyphText.color, Is.EqualTo(Color.cyan));
        Assert.That(runeBaseRenderer.sprite, Is.SameAs(runeBaseSprite));
        Assert.That(groundShadowRenderer.sprite, Is.SameAs(groundShadowSprite));
        Assert.That(runeBaseRenderer.color, Is.EqualTo(new Color(0.7f, 0.8f, 1f, 0.5f)));
        Assert.That(groundShadowRenderer.color, Is.EqualTo(new Color(0f, 0f, 0f, 0.3f)));
    }

    [Test]
    public void ApplyDefinition_DisablesMovementAndAttackForNoneKinds()
    {
        EnemyDefinitionBinder binder = CreateBoundEnemyShell(
            out BaseCharEnemyNorm1 enemy,
            out CharEnemyMovement movement,
            out EnemyMeleeAttacker meleeAttacker,
            out EnemyRangedTokenAttacker rangedTokenAttacker,
            out EnemySummoner summoner,
            out _,
            out _,
            out _);
        EnemyDefinition definition = CreateEnemyDefinition(
            "PassiveEnemy",
            binder,
            EnemyMovementKind.None,
            EnemyAttackKind.None,
            null,
            new EnemyDefinition.EnemyVisualDefinition
            {
                glyphText = "静",
                glyphColor = Color.white,
            });

        Assert.That(binder.ApplyDefinition(definition), Is.True);

        Assert.That(enemy.EnemyName, Is.EqualTo("PassiveEnemy"));
        Assert.That(movement.enabled, Is.False);
        Assert.That(meleeAttacker.enabled, Is.False);
        Assert.That(rangedTokenAttacker.enabled, Is.False);
        Assert.That(binder.GetComponent<EnemyExplosiveAttacker>().enabled, Is.False);
        Assert.That(summoner.enabled, Is.False);
    }

    [Test]
    public void ApplyDefinition_SeparatesAttackAndSkillSlots()
    {
        EnemyDefinitionBinder binder = CreateBoundEnemyShell(
            out _,
            out CharEnemyMovement movement,
            out EnemyMeleeAttacker meleeAttacker,
            out EnemyRangedTokenAttacker rangedTokenAttacker,
            out EnemySummoner summoner,
            out _,
            out _,
            out _);
        EnemyDefinition hybridDefinition = CreateEnemyDefinition(
            "ArcherSummonerEnemy",
            binder,
            EnemyMovementKind.KeepDistance,
            EnemyAttackKind.RangedBulletToken,
            new[]
            {
                CreateSummonSkillSlot(cooldownSeconds: 0.25f, castRange: 12f),
            },
            new EnemyDefinition.EnemyVisualDefinition
            {
                glyphText = "远",
                glyphColor = Color.yellow,
            });
        EnemyDefinition meleeDefinition = CreateEnemyDefinition(
            "MeleeEnemy",
            binder,
            EnemyMovementKind.AggroOnHit,
            EnemyAttackKind.MeleeContact,
            null,
            new EnemyDefinition.EnemyVisualDefinition
            {
                glyphText = "近",
                glyphColor = Color.red,
            });

        Assert.That(binder.ApplyDefinition(hybridDefinition), Is.True);
        Assert.That(movement.enabled, Is.True);
        Assert.That(meleeAttacker.enabled, Is.False);
        Assert.That(rangedTokenAttacker.enabled, Is.True);
        Assert.That(summoner.enabled, Is.True);

        Assert.That(binder.ApplyDefinition(meleeDefinition), Is.True);
        Assert.That(movement.enabled, Is.True);
        Assert.That(meleeAttacker.enabled, Is.True);
        Assert.That(rangedTokenAttacker.enabled, Is.False);
        Assert.That(summoner.enabled, Is.False);
    }

    [Test]
    public void Prefab_ProvidesEnemyDefinitionBinderContract()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Enemy/CharEnemy.prefab");

        try
        {
            EnemyDefinitionBinder binder = prefabRoot.GetComponent<EnemyDefinitionBinder>();
            Assert.That(binder, Is.Not.Null);
            Assert.That(prefabRoot.GetComponent<Enemy>(), Is.Not.Null);
            Assert.That(prefabRoot.GetComponent<CharEnemyMovement>(), Is.Not.Null);
        Assert.That(prefabRoot.GetComponent<EnemyMeleeAttacker>(), Is.Not.Null);
        Assert.That(prefabRoot.GetComponent<EnemyRangedTokenAttacker>(), Is.Not.Null);
        Assert.That(prefabRoot.GetComponent<EnemyExplosiveAttacker>(), Is.Not.Null);
        Assert.That(prefabRoot.GetComponent<EnemySummoner>(), Is.Not.Null);
            Assert.That(prefabRoot.GetComponent<CharGlyphPresenter>(), Is.Not.Null);
            Assert.That(prefabRoot.GetComponent<CharEnemyVisualPresenter>(), Is.Not.Null);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [Test]
    public void Norm1EnemyAsset_ReferencesBindableCharEnemyPrefab()
    {
        EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/迅.asset");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.RuntimePrefabBinder, Is.Not.Null);
        Assert.That(definition.RuntimePrefab, Is.Not.Null);
        Assert.That(definition.RuntimePrefab.GetComponent<EnemyDefinitionBinder>(), Is.SameAs(definition.RuntimePrefabBinder));
        Assert.That(definition.RuntimePrefab.GetComponent<Enemy>(), Is.Not.Null);
    }

    [Test]
    public void KeepDistanceMovement_ClampsPreferredDistanceToReachableAbilityRange()
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(definition);
        SetPrivateField(definition, "movementKind", EnemyMovementKind.KeepDistance);
        SetPrivateField(definition, "attackKind", EnemyAttackKind.RangedBulletToken);
        SetPrivateField(definition, "combat", new EnemyDefinition.EnemyCombatDefinition
        {
            maxHealth = 26f,
            moveSpeed = 28f,
            attackRange = 24f,
            attackCooldown = 1.6f,
            attackDamage = 1f,
            visualScaleMultiplier = 1f,
        });
        SetPrivateField(definition, "keepDistanceMovement", new EnemyDefinition.KeepDistanceMovementDefinition
        {
            preferredDistance = 256f,
            distanceTolerance = 4f,
        });
        SetPrivateField(definition, "skillSlots", new List<EnemyDefinition.EnemySkillSlotDefinition>
        {
            CreateSummonSkillSlot(cooldownSeconds: 4.5f, castRange: 24f),
        });

        Assert.That(definition.KeepDistanceMovement.preferredDistance, Is.EqualTo(24f));
        Assert.That(definition.KeepDistanceMovement.distanceTolerance, Is.EqualTo(4f));
    }

    [Test]
    public void SummonerEnemyAsset_UsesLongRangeKiteConfigurationAndConfiguredSummonWindow()
    {
        EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/Data/Enemies/召.asset");

        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.MovementKind, Is.EqualTo(EnemyMovementKind.KeepDistance));
        Assert.That(definition.AttackKind, Is.EqualTo(EnemyAttackKind.RangedBulletToken));
        Assert.That(definition.RangedBulletAttack.bulletPrefab, Is.Not.Null);
        Assert.That(definition.SkillSlots, Has.Count.EqualTo(1));
        Assert.That(definition.KeepDistanceMovement.preferredDistance, Is.EqualTo(256f));
        Assert.That(definition.Combat.attackRange, Is.EqualTo(288f));
        Assert.That(definition.KeepDistanceMovement.preferredDistance, Is.LessThan(definition.Combat.attackRange));
        Assert.That(definition.SkillSlots[0].cooldownSeconds, Is.EqualTo(4.5f));
        Assert.That(definition.SkillSlots[0].castRange, Is.EqualTo(288f));
        Assert.That(definition.KeepDistanceMovement.preferredDistance, Is.LessThan(definition.SkillSlots[0].castRange));
        Assert.That(definition.SkillSlots[0].summonSkill.minSummonCountPerCast, Is.EqualTo(1));
        Assert.That(definition.SkillSlots[0].summonSkill.maxSummonCountPerCast, Is.EqualTo(2));
        Assert.That(definition.SkillSlots[0].summonSkill.summonRadius, Is.EqualTo(8f));
        Assert.That(definition.SkillSlots[0].summonSkill.maxAliveSummons, Is.EqualTo(6));
    }

    private EnemyDefinitionBinder CreateBoundEnemyShell(
        out BaseCharEnemyNorm1 enemy,
        out CharEnemyMovement movement,
        out EnemyMeleeAttacker meleeAttacker,
        out EnemyRangedTokenAttacker rangedTokenAttacker,
        out EnemySummoner summoner,
        out TMP_Text glyphText,
        out SpriteRenderer runeBaseRenderer,
        out SpriteRenderer groundShadowRenderer)
    {
        GameObject root = CreateGameObject("EnemyRoot");
        movement = root.AddComponent<CharEnemyMovement>();
        meleeAttacker = root.AddComponent<EnemyMeleeAttacker>();
        rangedTokenAttacker = root.AddComponent<EnemyRangedTokenAttacker>();
        root.AddComponent<EnemyExplosiveAttacker>();
        summoner = root.AddComponent<EnemySummoner>();
        CharGlyphPresenter glyphPresenter = root.AddComponent<CharGlyphPresenter>();
        CharEnemyVisualPresenter visualPresenter = root.AddComponent<CharEnemyVisualPresenter>();
        enemy = root.AddComponent<BaseCharEnemyNorm1>();
        EnemyDefinitionBinder binder = root.AddComponent<EnemyDefinitionBinder>();

        GameObject textObject = CreateGameObject("Text");
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textObject.transform.SetParent(root.transform, false);

        GameObject glyphObject = CreateGameObject("Glyph");
        RectTransform glyphRect = glyphObject.AddComponent<RectTransform>();
        glyphObject.transform.SetParent(textObject.transform, false);
        glyphText = glyphObject.AddComponent<TextMeshPro>();

        GameObject colliderObject = CreateGameObject("Collider");
        colliderObject.transform.SetParent(root.transform, false);
        BoxCollider groundingCollider = colliderObject.AddComponent<BoxCollider>();
        groundingCollider.size = new Vector3(16f, 16f, 16f);

        GameObject runeBaseObject = CreateGameObject("RuneBaseCore");
        runeBaseObject.transform.SetParent(root.transform, false);
        runeBaseRenderer = runeBaseObject.AddComponent<SpriteRenderer>();

        GameObject groundShadowObject = CreateGameObject("GroundShadow");
        groundShadowObject.transform.SetParent(root.transform, false);
        groundShadowRenderer = groundShadowObject.AddComponent<SpriteRenderer>();

        Assert.That(glyphPresenter.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(visualPresenter.TryCacheBindings(overwriteExisting: true), Is.True);
        Assert.That(textRect, Is.Not.Null);
        Assert.That(glyphRect, Is.Not.Null);
        return binder;
    }

    private EnemyDefinition CreateEnemyDefinition(
        string enemyId,
        EnemyDefinitionBinder runtimePrefab,
        EnemyMovementKind movementKind,
        EnemyAttackKind attackKind,
        IEnumerable<EnemyDefinition.EnemySkillSlotDefinition> skillSlots,
        EnemyDefinition.EnemyVisualDefinition visual)
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.name = enemyId;
        createdObjects.Add(definition);
        SetPrivateField(definition, "enemyId", enemyId);
        SetPrivateField(definition, "displayName", enemyId);
        SetPrivateField(definition, "runtimePrefab", runtimePrefab);
        SetPrivateField(definition, "movementKind", movementKind);
        SetPrivateField(definition, "attackKind", attackKind);
        SetPrivateField(
            definition,
            "skillSlots",
            skillSlots != null
                ? new List<EnemyDefinition.EnemySkillSlotDefinition>(skillSlots)
                : new List<EnemyDefinition.EnemySkillSlotDefinition>());
        SetPrivateField(definition, "visual", visual);
        return definition;
    }

    private static EnemyDefinition.EnemySkillSlotDefinition CreateSummonSkillSlot(float cooldownSeconds, float castRange)
    {
        return new EnemyDefinition.EnemySkillSlotDefinition
        {
            skillKind = EnemySkillKind.SummonEnemy,
            cooldownSeconds = cooldownSeconds,
            castRange = castRange,
            summonSkill = new EnemyDefinition.SummonSkillDefinition
            {
                minSummonCountPerCast = 1,
                maxSummonCountPerCast = 1,
                summonRadius = 6f,
                maxAliveSummons = 2,
            },
        };
    }

    private Sprite CreateSprite(string name)
    {
        Texture2D texture = new(2, 2);
        texture.name = name;
        texture.SetPixel(0, 0, Color.white);
        texture.SetPixel(1, 0, Color.white);
        texture.SetPixel(0, 1, Color.white);
        texture.SetPixel(1, 1, Color.white);
        texture.Apply();
        createdObjects.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        sprite.name = name;
        createdObjects.Add(sprite);
        return sprite;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static FieldInfo FindInstanceField(System.Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }
}
