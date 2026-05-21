using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kernel.MapGrid.Editor.Tests
{
    public sealed class ProceduralRoomMapTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object createdObject = createdObjects[i];
                if (createdObject == null)
                {
                    continue;
                }

                if (createdObject is GameObject gameObject)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void RoomTemplateData_ValidTemplate_ResolvesLayout()
        {
            RoomTemplateData template = CreateTemplate(
                "valid",
                RoomKind.Combat,
                5,
                5,
                "##D##",
                "#...#",
                "D.P.S",
                "#...#",
                "##D##");

            bool success = template.TryResolveLayout(
                new[] { RoomDirection.North, RoomDirection.West, RoomDirection.South },
                out RoomResolvedLayout layout,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(layout.Width, Is.EqualTo(5));
            Assert.That(layout.Height, Is.EqualTo(5));
            Assert.That(layout.PlayerEntryCells, Has.Count.EqualTo(1));
            Assert.That(layout.EnemySpawnCells, Has.Count.EqualTo(1));
            Assert.That(layout.Doors, Has.Count.EqualTo(3));
            Assert.That(layout.GetSurface(2, 2), Is.EqualTo(CellData.CellSurfaceType.Ground));
            Assert.That(layout.GetSurface(0, 4), Is.EqualTo(CellData.CellSurfaceType.Wall));
        }

        [Test]
        public void RoomTemplateData_InvalidRowsOrSymbols_AreRejected()
        {
            AssertTemplateFails(
                CreateTemplate("row_count", RoomKind.Combat, 3, 3, "###", "#P#"),
                "height");
            AssertTemplateFails(
                CreateTemplate("row_width", RoomKind.Combat, 3, 2, "###", "#P"),
                "width");
            AssertTemplateFails(
                CreateTemplate("unknown", RoomKind.Combat, 3, 3, "###", "#P#", "#X#"),
                "Unsupported");
            AssertTemplateFails(
                CreateTemplate("missing_player", RoomKind.Combat, 3, 3, "###", "#.#", "###"),
                "player entry");
            AssertTemplateFails(
                CreateTemplate("internal_door", RoomKind.Combat, 5, 5, "#####", "#...#", "#.DP#", "#...#", "#####"),
                "boundary");
        }

        [Test]
        public void RoomGraphGenerator_SameSeed_ProducesSameGraph()
        {
            List<RoomTemplateData> templates = CreateDefaultTemplateSet();
            var generator = new RoomGraphGenerator();

            bool leftSuccess = generator.TryGenerateDefaultRun(73, templates, out RoomGraph leftGraph, out _, out string leftError);
            bool rightSuccess = generator.TryGenerateDefaultRun(73, templates, out RoomGraph rightGraph, out _, out string rightError);

            Assert.That(leftSuccess, Is.True, leftError);
            Assert.That(rightSuccess, Is.True, rightError);
            Assert.That(BuildGraphSignature(leftGraph), Is.EqualTo(BuildGraphSignature(rightGraph)));
        }

        [Test]
        public void RoomGraphGenerator_DefaultRun_HasBidirectionalConnectionsAndUniqueCoordinates()
        {
            List<RoomTemplateData> templates = CreateDefaultTemplateSet();
            var generator = new RoomGraphGenerator();

            bool success = generator.TryGenerateDefaultRun(19, templates, out RoomGraph graph, out _, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(graph.Nodes, Has.Count.EqualTo(7));
            var coordinates = new HashSet<Vector2Int>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                RoomGraphNode node = graph.Nodes[i];
                Assert.That(coordinates.Add(node.GraphCoordinate), Is.True, $"Duplicate coordinate at {node.RoomId}.");
                foreach (KeyValuePair<RoomDirection, string> connection in node.Connections)
                {
                    Assert.That(graph.TryGetNode(connection.Value, out RoomGraphNode connectedNode), Is.True);
                    RoomDirection opposite = RoomDirectionUtility.GetOpposite(connection.Key);
                    Assert.That(connectedNode.TryGetConnectedRoomId(opposite, out string returnRoomId), Is.True);
                    Assert.That(returnRoomId, Is.EqualTo(node.RoomId));
                }
            }
        }

        [Test]
        public void RoomGraphGenerator_UsesFallbackTemplate_WhenKindMatchIsMissing()
        {
            RoomTemplateData fallback = CreateAllDoorTemplate("fallback", RoomKind.Start);
            var generator = new RoomGraphGenerator();

            bool success = generator.TryGenerateDefaultRun(
                2,
                new List<RoomTemplateData> { fallback },
                out RoomGraph graph,
                out string warning,
                out string error);

            Assert.That(success, Is.True, error);
            Assert.That(graph.Nodes, Has.Count.EqualTo(7));
            Assert.That(warning, Does.Contain("fallback"));
        }

        [Test]
        public void TilemapRoomPresenter_Apply_ClearsPreviousRoomTiles()
        {
            TilemapRoomPresenter presenter = CreatePresenter(out Tilemap floorTilemap, out Tilemap wallTilemap);
            Tile floorTile = CreateTile();
            Tile wallTile = CreateTile();
            SetPrivateField(presenter, "floorTile", floorTile);
            SetPrivateField(presenter, "wallTile", wallTile);

            RoomResolvedLayout firstLayout = CreateResolvedLayout(3, 2, new[]
            {
                CellData.CellSurfaceType.Ground,
                CellData.CellSurfaceType.Wall,
                CellData.CellSurfaceType.Ground,
                CellData.CellSurfaceType.Wall,
                CellData.CellSurfaceType.Ground,
                CellData.CellSurfaceType.Ground,
            });
            RoomResolvedLayout secondLayout = CreateResolvedLayout(1, 1, new[] { CellData.CellSurfaceType.Ground });

            Assert.That(presenter.TryApply(firstLayout, out string firstError), Is.True, firstError);
            Assert.That(presenter.LastAppliedFloorTileCount, Is.EqualTo(4));
            Assert.That(presenter.LastAppliedWallTileCount, Is.EqualTo(2));
            Assert.That(floorTilemap.GetTile(new Vector3Int(2, 1, 0)), Is.EqualTo(floorTile));
            Assert.That(wallTilemap.GetTile(new Vector3Int(0, 1, 0)), Is.EqualTo(wallTile));

            Assert.That(presenter.TryApply(secondLayout, out string secondError), Is.True, secondError);
            Assert.That(presenter.LastAppliedFloorTileCount, Is.EqualTo(1));
            Assert.That(presenter.LastAppliedWallTileCount, Is.EqualTo(0));
            Assert.That(floorTilemap.GetTile(new Vector3Int(2, 1, 0)), Is.Null);
            Assert.That(wallTilemap.GetTile(new Vector3Int(0, 1, 0)), Is.Null);
        }

        [Test]
        public void ProceduralRoomMapDebugController_GenerateRun_RendersOnlyCurrentRoom()
        {
            TilemapRoomPresenter presenter = CreatePresenter(out _, out _);
            Tile floorTile = CreateTile();
            Tile wallTile = CreateTile();
            SetPrivateField(presenter, "floorTile", floorTile);
            SetPrivateField(presenter, "wallTile", wallTile);

            GameObject controllerObject = CreateGameObject("Debug Controller");
            ProceduralRoomMapDebugController controller = controllerObject.AddComponent<ProceduralRoomMapDebugController>();
            SetPrivateField(controller, "roomPresenter", presenter);
            SetPrivateField(controller, "roomTemplates", CreateDefaultTemplateSet());

            Assert.That(controller.GenerateRun(out string error), Is.True, error);
            Assert.That(controller.CurrentGraph, Is.Not.Null);
            Assert.That(controller.CurrentGraph.Nodes, Has.Count.EqualTo(7));
            Assert.That(controller.CurrentGraph.CurrentRoomId, Is.EqualTo("start_0"));
            Assert.That(presenter.LastAppliedFloorTileCount + presenter.LastAppliedWallTileCount, Is.EqualTo(49));
        }

        private static void AssertTemplateFails(RoomTemplateData template, string expectedErrorFragment)
        {
            bool success = template.TryResolveLayout(Array.Empty<RoomDirection>(), out _, out string error);
            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain(expectedErrorFragment));
        }

        private List<RoomTemplateData> CreateDefaultTemplateSet()
        {
            return new List<RoomTemplateData>
            {
                CreateAllDoorTemplate("start", RoomKind.Start),
                CreateAllDoorTemplate("combat", RoomKind.Combat),
                CreateAllDoorTemplate("reward", RoomKind.Reward),
                CreateAllDoorTemplate("boss", RoomKind.Boss),
            };
        }

        private RoomTemplateData CreateAllDoorTemplate(string id, RoomKind kind)
        {
            return CreateTemplate(
                id,
                kind,
                7,
                7,
                "###D###",
                "#.....#",
                "#..S..#",
                "D..P..D",
                "#.....#",
                "#.....#",
                "###D###");
        }

        private RoomTemplateData CreateTemplate(string id, RoomKind kind, int width, int height, params string[] rows)
        {
            RoomTemplateData template = ScriptableObject.CreateInstance<RoomTemplateData>();
            template.name = id;
            createdObjects.Add(template);
            SetPrivateField(template, "id", id);
            SetPrivateField(template, "roomKind", kind);
            SetPrivateField(template, "width", width);
            SetPrivateField(template, "height", height);
            SetPrivateField(template, "layoutRows", new List<string>(rows));
            return template;
        }

        private Tile CreateTile()
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            createdObjects.Add(tile);
            return tile;
        }

        private TilemapRoomPresenter CreatePresenter(out Tilemap floorTilemap, out Tilemap wallTilemap)
        {
            GameObject root = CreateGameObject("Tilemap Presenter");
            root.AddComponent<Grid>();
            GameObject floorObject = CreateGameObject("Floor Tilemap");
            floorObject.transform.SetParent(root.transform);
            floorTilemap = floorObject.AddComponent<Tilemap>();
            floorObject.AddComponent<TilemapRenderer>();
            GameObject wallObject = CreateGameObject("Wall Tilemap");
            wallObject.transform.SetParent(root.transform);
            wallTilemap = wallObject.AddComponent<Tilemap>();
            wallObject.AddComponent<TilemapRenderer>();

            TilemapRoomPresenter presenter = root.AddComponent<TilemapRoomPresenter>();
            SetPrivateField(presenter, "floorTilemap", floorTilemap);
            SetPrivateField(presenter, "wallTilemap", wallTilemap);
            return presenter;
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static RoomResolvedLayout CreateResolvedLayout(int width, int height, IReadOnlyList<CellData.CellSurfaceType> surfaces)
        {
            return new RoomResolvedLayout(
                "test",
                RoomKind.Combat,
                width,
                height,
                surfaces,
                new[] { Vector2Int.zero },
                Array.Empty<Vector2Int>(),
                Array.Empty<RoomDoor>());
        }

        private static string BuildGraphSignature(RoomGraph graph)
        {
            var lines = new List<string> { graph.CurrentRoomId };
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                RoomGraphNode node = graph.Nodes[i];
                var connections = new List<string>();
                foreach (RoomDirection direction in node.RequiredDoorDirections)
                {
                    if (node.TryGetConnectedRoomId(direction, out string connectedRoomId))
                    {
                        connections.Add($"{direction}:{connectedRoomId}");
                    }
                }

                lines.Add($"{node.RoomId}|{node.Kind}|{node.GraphCoordinate.x},{node.GraphCoordinate.y}|{string.Join(",", connections)}");
            }

            return string.Join("\n", lines);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
