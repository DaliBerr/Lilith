using System.Collections.Generic;
using System.Reflection;
using TMPro;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerVisualPresenterTests
{
    private const float MinimumPlanarDirectionSqrMagnitude = 0.0001f;
    private readonly List<Object> createdObjects = new();

    [Test]
    public void LateUpdate_KeepsGroundShadowFlatWhilePreservingPlanarFacing()
    {
        GameObject playerObject = CreateGameObject("Player");
        BoxCollider groundingCollider = playerObject.AddComponent<BoxCollider>();
        groundingCollider.size = new Vector3(12f, 18f, 12f);

        GameObject textObject = CreateUiGameObject("Text");
        textObject.transform.SetParent(playerObject.transform, false);

        GameObject glyphObject = CreateUiGameObject("Glyph");
        glyphObject.transform.SetParent(textObject.transform, false);
        glyphObject.AddComponent<TextMeshProUGUI>();

        GameObject shadowObject = CreateGameObject("GroundShadow");
        shadowObject.transform.SetParent(playerObject.transform, false);
        shadowObject.AddComponent<SpriteRenderer>();

        PlayerVisualPresenter presenter = playerObject.AddComponent<PlayerVisualPresenter>();

        playerObject.transform.rotation = Quaternion.Euler(20f, 35f, 15f);
        InvokePrivateMethod(presenter, "LateUpdate");
        Quaternion firstRotation = shadowObject.transform.rotation;
        Quaternion firstExpectedRotation = CreateExpectedGroundShadowRotation(playerObject.transform.forward);

        playerObject.transform.rotation = Quaternion.Euler(340f, 125f, 25f);
        InvokePrivateMethod(presenter, "LateUpdate");
        Quaternion secondRotation = shadowObject.transform.rotation;
        Quaternion secondExpectedRotation = CreateExpectedGroundShadowRotation(playerObject.transform.forward);

        Assert.That(Quaternion.Angle(firstRotation, firstExpectedRotation), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(secondRotation, secondExpectedRotation), Is.LessThan(0.001f));
    }

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

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private GameObject CreateUiGameObject(string name)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing private method '{methodName}'.");
        method.Invoke(target, null);
    }

    private static Quaternion CreateExpectedGroundShadowRotation(Vector3 worldForward)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(worldForward, Vector3.up);
        if (planarForward.sqrMagnitude <= MinimumPlanarDirectionSqrMagnitude)
        {
            return Quaternion.Euler(90f, 180f, 0f);
        }

        planarForward.Normalize();
        float yaw = Mathf.Atan2(planarForward.x, planarForward.z) * Mathf.Rad2Deg;
        return Quaternion.Euler(90f, yaw + 180f, 0f);
    }
}
