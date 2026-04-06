using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class GameplayBillboardTests
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
    public void ApplyFacing_AlignsVisualNodeWithTargetCameraRotation()
    {
        GameObject cameraObject = CreateGameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.rotation = Quaternion.Euler(40f, 25f, 0f);

        GameObject visualObject = CreateGameObject("Visual");
        GameplayBillboard billboard = visualObject.AddComponent<GameplayBillboard>();
        SetPrivateField(billboard, "targetCamera", camera);

        bool success = billboard.ApplyFacing();

        Assert.That(success, Is.True);
        Assert.That(Quaternion.Angle(visualObject.transform.rotation, cameraObject.transform.rotation), Is.LessThan(0.001f));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
