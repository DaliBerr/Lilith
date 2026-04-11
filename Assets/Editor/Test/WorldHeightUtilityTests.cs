using System.Collections.Generic;
using Kernel;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldHeightUtilityTests
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
    public void CalculateGroundedRootY_UsesColliderBottomRelativeToRootAndAppliesExtraLift()
    {
        GameObject root = CreateGameObject("Root");
        root.transform.position = new Vector3(0f, 0f, 0f);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(4f, 10f, 4f);

        float groundedY = WorldHeightUtility.CalculateGroundedRootY(root.transform, collider, 3f);

        Assert.That(groundedY, Is.EqualTo(9f));
    }

    [Test]
    public void TryProjectRayOntoPlaneY_UsesSharedWorldPlane()
    {
        Ray ray = new(new Vector3(5f, 20f, 7f), Vector3.down);

        bool success = WorldHeightUtility.TryProjectRayOntoPlaneY(ray, 4f, out Vector3 worldPoint);

        Assert.That(success, Is.True);
        Assert.That(worldPoint, Is.EqualTo(new Vector3(5f, 4f, 7f)));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }
}
