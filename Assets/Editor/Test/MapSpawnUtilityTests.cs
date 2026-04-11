using System.Collections.Generic;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;

namespace Kernel.MapGrid.Editor.Tests
{
    public sealed class MapSpawnUtilityTests
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
        public void TryFindNearestGroundCoordinate_ReturnsClosestGroundCell()
        {
            MapGridAuthoring authoring = CreateMapAuthoring(5, 5, 0f);
            PopulateCells(authoring, 5, 5, MapGridAuthoring.WallTagName);
            SetCellTag(authoring, 3, 2, MapGridAuthoring.GroundTagName);

            bool success = MapSpawnUtility.TryFindNearestGroundCoordinate(
                authoring,
                new Vector2Int(2, 2),
                out Vector2Int resolvedCoordinates,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(resolvedCoordinates, Is.EqualTo(new Vector2Int(3, 2)));
        }

        [Test]
        public void TryTeleportToNearestGroundCell_MovesTargetAndClearsRigidbodyVelocity()
        {
            MapGridAuthoring authoring = CreateMapAuthoring(5, 5, 10f);
            PopulateCells(authoring, 5, 5, MapGridAuthoring.WallTagName);
            SetCellTag(authoring, 4, 1, MapGridAuthoring.GroundTagName);

            GameObject playerObject = CreateGameObject("Player");
            Rigidbody rigidbody = playerObject.AddComponent<Rigidbody>();
            BoxCollider collider = playerObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(15f, 15f, 15f);
            playerObject.transform.position = Vector3.zero;
            rigidbody.position = Vector3.zero;
            rigidbody.linearVelocity = new Vector3(2f, 3f, 4f);
            rigidbody.angularVelocity = Vector3.one;

            bool success = MapSpawnUtility.TryTeleportToNearestGroundCell(
                authoring,
                playerObject.transform,
                Vector2Int.zero,
                out Vector2Int resolvedCoordinates,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(resolvedCoordinates, Is.EqualTo(new Vector2Int(4, 1)));

            Vector3 expectedCellPosition = authoring.GetCellWorldPosition(4, 1);
            Assert.That(playerObject.transform.position.x, Is.EqualTo(expectedCellPosition.x).Within(0.001f));
            Assert.That(playerObject.transform.position.z, Is.EqualTo(expectedCellPosition.z).Within(0.001f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(17.5f).Within(0.001f));
            Assert.That(rigidbody.position, Is.EqualTo(playerObject.transform.position));
            Assert.That(rigidbody.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(rigidbody.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        private MapGridAuthoring CreateMapAuthoring(int width, int height, float planeY)
        {
            GameObject mapRoot = CreateGameObject("MapRoot");
            mapRoot.transform.position = new Vector3(0f, planeY, 0f);
            MapGridAuthoring authoring = mapRoot.AddComponent<MapGridAuthoring>();
            authoring.GridWidth = width;
            authoring.GridHeight = height;
            authoring.CellSize = Vector2.one;
            return authoring;
        }

        private void PopulateCells(MapGridAuthoring authoring, int width, int height, string defaultTag)
        {
            List<CellEntry> entries = new(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GameObject cellObject = CreateGameObject($"Cell_{x}_{y}");
                    cellObject.tag = defaultTag;
                    cellObject.transform.position = authoring.GetCellWorldPosition(x, y);
                    entries.Add(new CellEntry(x, y, cellObject));
                }
            }

            authoring.ReplaceCellEntries(entries);
        }

        private void SetCellTag(MapGridAuthoring authoring, int x, int y, string tagName)
        {
            Assert.That(authoring.TryGetCell(new Vector2Int(x, y), out GameObject cellObject), Is.True);
            Assert.That(cellObject, Is.Not.Null);
            cellObject.tag = tagName;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }
    }
}
