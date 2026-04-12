using System.Collections.Generic;
using System.Reflection;
using Kernel.Bullet;
using NUnit.Framework;
using UnityEngine;

public sealed class ScriptableObjectOnValidateTests
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
    public void LinkedTokenData_OnValidate_PreservesNullLinkedTokenSlots()
    {
        LinkedTokenData linkedToken = ScriptableObject.CreateInstance<LinkedTokenData>();
        createdObjects.Add(linkedToken);

        CoreTokenData coreToken = CreateToken<CoreTokenData>("linked_core", "Core");
        SetPrivateField(linkedToken, "linkedTokens", new List<BaseTokenData> { coreToken, null });

        InvokePrivateMethod(linkedToken, "OnValidate");

        Assert.That(linkedToken.LinkedTokens, Is.Not.Null);
        Assert.That(linkedToken.LinkedTokens, Has.Count.EqualTo(2));
        Assert.That(linkedToken.LinkedTokens[0], Is.SameAs(coreToken));
        Assert.That(linkedToken.LinkedTokens[1], Is.Null);
    }

    [Test]
    public void EnemyDefinition_OnValidate_PreservesNullRangedFormulaSlots()
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        createdObjects.Add(definition);

        CoreTokenData coreToken = CreateToken<CoreTokenData>("enemy_core", "Core");
        EnemyDefinition.RangedBulletAttackDefinition rangedAttack = new()
        {
            formulaItems = new List<PlaceableTokenData> { coreToken, null },
            targetPolicy = BulletTargetPolicy.PlayerOnly,
        };

        SetPrivateField(definition, "rangedBulletAttack", rangedAttack);
        InvokePrivateMethod(definition, "OnValidate");

        EnemyDefinition.RangedBulletAttackDefinition validatedAttack =
            GetPrivateField<EnemyDefinition.RangedBulletAttackDefinition>(definition, "rangedBulletAttack");

        Assert.That(validatedAttack.formulaItems, Is.Not.Null);
        Assert.That(validatedAttack.formulaItems, Has.Count.EqualTo(2));
        Assert.That(validatedAttack.formulaItems[0], Is.SameAs(coreToken));
        Assert.That(validatedAttack.formulaItems[1], Is.Null);
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

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, args);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindInstanceField(target.GetType(), fieldName);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        return (T)field.GetValue(target);
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
