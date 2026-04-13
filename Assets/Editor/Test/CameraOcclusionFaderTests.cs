using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CameraOcclusionFaderTests
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
    public void RefreshOcclusionState_FadesOccludingWallAndRestoresWhenClear()
    {
        GameObject targetObject = CreateGameObject("Player");
        targetObject.transform.position = Vector3.zero;

        GameObject cameraObject = CreateGameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0f, 5f, -10f);
        cameraObject.transform.rotation = Quaternion.LookRotation((targetObject.transform.position - cameraObject.transform.position).normalized, Vector3.up);

        CameraOcclusionFader fader = cameraObject.AddComponent<CameraOcclusionFader>();
        Material ghostMaterial = CreateMaterial("Sprites/Default", "Ghost");
        SetPrivateField(fader, "targetTransform", targetObject.transform);
        SetPrivateField(fader, "occludedWallMaterial", ghostMaterial);

        CellData wallCell = CreateWallCell(new Vector3(0f, 2.5f, -5f), out Renderer wallRenderer, out Material originalMaterial);

        Physics.SyncTransforms();
        bool occluded = fader.RefreshOcclusionState();

        Assert.That(occluded, Is.True);
        Assert.That(wallCell.SurfaceType, Is.EqualTo(CellData.CellSurfaceType.Wall));
        Assert.That(wallRenderer.sharedMaterial, Is.SameAs(ghostMaterial));

        wallCell.transform.position = new Vector3(20f, 2.5f, -5f);
        Physics.SyncTransforms();
        bool clear = fader.RefreshOcclusionState();

        Assert.That(clear, Is.True);
        Assert.That(wallRenderer.sharedMaterial, Is.SameAs(originalMaterial));
    }

    [Test]
    public void RefreshOcclusionState_FadesWallTaggedObjectWithoutCellData()
    {
        GameObject targetObject = CreateGameObject("Player");
        targetObject.transform.position = Vector3.zero;

        GameObject cameraObject = CreateGameObject("Main Camera");
        cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0f, 5f, -10f);
        cameraObject.transform.rotation = Quaternion.LookRotation((targetObject.transform.position - cameraObject.transform.position).normalized, Vector3.up);

        CameraOcclusionFader fader = cameraObject.AddComponent<CameraOcclusionFader>();
        Material ghostMaterial = CreateMaterial("Sprites/Default", "Ghost");
        SetPrivateField(fader, "targetTransform", targetObject.transform);
        SetPrivateField(fader, "occludedWallMaterial", ghostMaterial);

        GameObject taggedWallRoot = CreateTaggedWallWithoutCellData(
            new Vector3(0f, 2.5f, -5f),
            out Renderer wallRenderer,
            out Material originalMaterial);

        Physics.SyncTransforms();
        bool occluded = fader.RefreshOcclusionState();

        Assert.That(occluded, Is.True);
        Assert.That(wallRenderer.sharedMaterial, Is.SameAs(ghostMaterial));

        taggedWallRoot.transform.position = new Vector3(20f, 2.5f, -5f);
        Physics.SyncTransforms();
        bool clear = fader.RefreshOcclusionState();

        Assert.That(clear, Is.True);
        Assert.That(wallRenderer.sharedMaterial, Is.SameAs(originalMaterial));
    }

    [Test]
    public void RefreshOcclusionState_FadesNearbyWallWithinOcclusionRadius()
    {
        GameObject targetObject = CreateGameObject("Player");
        targetObject.transform.position = Vector3.zero;

        GameObject cameraObject = CreateGameObject("Main Camera");
        cameraObject.AddComponent<Camera>();
        cameraObject.transform.position = new Vector3(0f, 5f, -10f);
        cameraObject.transform.rotation = Quaternion.LookRotation((targetObject.transform.position - cameraObject.transform.position).normalized, Vector3.up);

        CameraOcclusionFader fader = cameraObject.AddComponent<CameraOcclusionFader>();
        Material ghostMaterial = CreateMaterial("Sprites/Default", "Ghost");
        SetPrivateField(fader, "targetTransform", targetObject.transform);
        SetPrivateField(fader, "occludedWallMaterial", ghostMaterial);
        SetPrivateField(fader, "occlusionRadius", 1.6f);

        GameObject nearbyWallRoot = CreateTaggedWallWithoutCellData(
            new Vector3(1.4f, 2.5f, -5f),
            out Renderer wallRenderer,
            out Material originalMaterial);

        Physics.SyncTransforms();
        bool occluded = fader.RefreshOcclusionState();

        Assert.That(occluded, Is.True);
        Assert.That(wallRenderer.sharedMaterial, Is.SameAs(ghostMaterial));

        nearbyWallRoot.transform.position = new Vector3(20f, 2.5f, -5f);
        Physics.SyncTransforms();
        bool clear = fader.RefreshOcclusionState();

        Assert.That(clear, Is.True);
        Assert.That(wallRenderer.sharedMaterial, Is.SameAs(originalMaterial));
    }

    private CellData CreateWallCell(Vector3 position, out Renderer wallRenderer, out Material originalMaterial)
    {
        GameObject root = CreateGameObject("WallCell");
        root.transform.position = position;

        CellData cellData = root.AddComponent<CellData>();
        BoxCollider wallCollider = root.AddComponent<BoxCollider>();
        wallCollider.size = new Vector3(3f, 6f, 3f);
        BoxCollider groundCollider = root.AddComponent<BoxCollider>();
        groundCollider.size = new Vector3(3f, 0.5f, 3f);

        GameObject wallModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        createdObjects.Add(wallModel);
        wallModel.name = "WallModel";
        wallModel.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(wallModel.GetComponent<Collider>());
        wallRenderer = wallModel.GetComponent<Renderer>();
        originalMaterial = CreateMaterial("Sprites/Default", "Original");
        wallRenderer.sharedMaterial = originalMaterial;

        GameObject groundModel = CreateGameObject("GroundModel");
        groundModel.transform.SetParent(root.transform, false);

        SetPrivateField(cellData, "wallCollider", wallCollider);
        SetPrivateField(cellData, "groundCollider", groundCollider);
        SetPrivateField(cellData, "wallModelRoot", wallModel.transform);
        SetPrivateField(cellData, "groundModelRoot", groundModel.transform);
        SetPrivateField(cellData, "surfaceType", CellData.CellSurfaceType.Wall);

        Assert.That(cellData.TryRefreshSurfacePresentation(syncTags: false), Is.True);
        return cellData;
    }

    private GameObject CreateTaggedWallWithoutCellData(Vector3 position, out Renderer wallRenderer, out Material originalMaterial)
    {
        GameObject taggedWallRoot = CreateGameObject("TaggedWall");
        taggedWallRoot.transform.position = position;
        taggedWallRoot.tag = Kernel.MapGrid.MapGridAuthoring.WallTagName;
        BoxCollider wallCollider = taggedWallRoot.AddComponent<BoxCollider>();
        wallCollider.size = new Vector3(3f, 6f, 3f);

        GameObject wallVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        createdObjects.Add(wallVisual);
        wallVisual.name = "WallVisual";
        wallVisual.transform.SetParent(taggedWallRoot.transform, false);
        Object.DestroyImmediate(wallVisual.GetComponent<Collider>());

        wallRenderer = wallVisual.GetComponent<Renderer>();
        originalMaterial = CreateMaterial("Sprites/Default", "Original");
        wallRenderer.sharedMaterial = originalMaterial;
        return taggedWallRoot;
    }

    private Material CreateMaterial(string shaderName, string materialName)
    {
        Shader shader = Shader.Find(shaderName);
        Assert.That(shader, Is.Not.Null, $"Shader '{shaderName}' was not found.");

        Material material = new(shader)
        {
            name = materialName,
        };

        createdObjects.Add(material);
        return material;
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
