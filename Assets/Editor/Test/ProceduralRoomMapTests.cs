using System;
using System.Collections.Generic;
using System.Reflection;
using Kernel.MapGrid;
using NUnit.Framework;
using UnityEditor;
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
            Assert.That(layout.PlayerEntryCells.Count, Is.EqualTo(1));
            Assert.That(layout.EnemySpawnCells.Count, Is.EqualTo(1));
            Assert.That(layout.Doors.Count, Is.EqualTo(3));
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
            var generator = new RoomGraphGenerator();

            bool leftSuccess = generator.TryGenerateDefaultRun(73, out RoomGraph leftGraph, out string leftError);
            bool rightSuccess = generator.TryGenerateDefaultRun(73, out RoomGraph rightGraph, out string rightError);

            Assert.That(leftSuccess, Is.True, leftError);
            Assert.That(rightSuccess, Is.True, rightError);
            Assert.That(BuildGraphSignature(leftGraph), Is.EqualTo(BuildGraphSignature(rightGraph)));
        }

        [Test]
        public void RoomGraphGenerator_DefaultRun_HasBidirectionalConnectionsAndUniqueCoordinates()
        {
            var generator = new RoomGraphGenerator();

            bool success = generator.TryGenerateDefaultRun(19, out RoomGraph graph, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(graph.Nodes.Count, Is.EqualTo(7));
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
        public void RoomGraphGenerator_DefaultRun_DoesNotRequireRoomTemplates()
        {
            var generator = new RoomGraphGenerator();

            bool success = generator.TryGenerateDefaultRun(2, out RoomGraph graph, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(graph.Nodes.Count, Is.EqualTo(7));
            Assert.That(graph.TryGetCurrentNode(out RoomGraphNode startNode), Is.True);
            Assert.That(startNode.Kind, Is.EqualTo(RoomKind.Start));
        }

        [Test]
        public void RoomGenerator_SameSeed_ProducesSameLayout()
        {
            var generator = new RoomLayoutGenerator();
            RoomGenerationInput input = CreateGenerationInput(RoomKind.Combat, 81, RoomDirection.West, RoomDirection.East);
            RoomGenerationProfile profile = CreateRuntimeProfile(input);

            bool leftSuccess = generator.TryGenerate(input, profile, out RoomResolvedLayout left, out string leftError);
            bool rightSuccess = generator.TryGenerate(input, profile, out RoomResolvedLayout right, out string rightError);

            Assert.That(leftSuccess, Is.True, leftError);
            Assert.That(rightSuccess, Is.True, rightError);
            Assert.That(BuildLayoutSignature(left), Is.EqualTo(BuildLayoutSignature(right)));
        }

        [Test]
        public void RoomGenerationProfileData_BuildRuntimeProfile_ClampsInvalidValues()
        {
            RoomGenerationProfileData data = CreateProfileData("invalid", RoomKind.Combat);
            SetPrivateField(data, "widthRange", new Vector2Int(-5, 0));
            SetPrivateField(data, "heightRange", new Vector2Int(0, -3));
            SetPrivateField(data, "minimumWidth", -10);
            SetPrivateField(data, "minimumHeight", 0);
            SetPrivateField(data, "minPathWidth", 0);
            SetPrivateField(data, "playerSafeRadius", -1);
            SetPrivateField(data, "doorBufferRadius", -2);
            SetPrivateField(data, "obstacleCountRange", new Vector2Int(5, -4));
            SetPrivateField(data, "obstacleWidthRange", new Vector2Int(0, -6));
            SetPrivateField(data, "obstacleHeightRange", new Vector2Int(-1, 0));
            SetPrivateField(data, "maxObstacleDensity", 2f);
            SetPrivateField(data, "enemySpawnCountRange", new Vector2Int(-3, 2));
            SetPrivateField(data, "minEnemySpawnDistance", -1);
            SetPrivateField(data, "minCombatGroundCells", -1);
            SetPrivateField(data, "bossOpenRadius", -1);
            SetPrivateField(data, "maxPlacementAttempts", 0);
            SetPrivateField(data, "maxGenerationRetries", 0);

            RoomGenerationProfile profile = data.BuildRuntimeProfile(CreateGenerationInput(RoomKind.Combat, 91));

            Assert.That(profile.WidthRange.x, Is.EqualTo(1));
            Assert.That(profile.HeightRange.x, Is.EqualTo(1));
            Assert.That(profile.MinimumWidth, Is.EqualTo(1));
            Assert.That(profile.MinimumHeight, Is.EqualTo(1));
            Assert.That(profile.MinPathWidth, Is.EqualTo(1));
            Assert.That(profile.PlayerSafeRadius, Is.EqualTo(0));
            Assert.That(profile.DoorBufferRadius, Is.EqualTo(0));
            Assert.That(profile.ObstacleCountRange, Is.EqualTo(new Vector2Int(0, 5)));
            Assert.That(profile.ObstacleWidthRange, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(profile.ObstacleHeightRange, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(profile.MaxObstacleDensity, Is.EqualTo(1f));
            Assert.That(profile.EnemySpawnCountRange, Is.EqualTo(new Vector2Int(0, 2)));
            Assert.That(profile.MinEnemySpawnDistance, Is.EqualTo(0));
            Assert.That(profile.MinCombatGroundCells, Is.EqualTo(0));
            Assert.That(profile.BossOpenRadius, Is.EqualTo(0));
            Assert.That(profile.MaxPlacementAttempts, Is.EqualTo(1));
            Assert.That(profile.MaxGenerationRetries, Is.EqualTo(1));
        }

        [Test]
        public void RoomGenerationProfileLibrary_SelectsDefaultProfile_ForEachRoomKind()
        {
            RoomGenerationProfileData start = CreateProfileData("start", RoomKind.Start);
            RoomGenerationProfileData combat = CreateProfileData("combat", RoomKind.Combat);
            RoomGenerationProfileData reward = CreateProfileData("reward", RoomKind.Reward);
            RoomGenerationProfileData boss = CreateProfileData("boss", RoomKind.Boss);
            RoomGenerationProfileLibrary library = CreateProfileLibrary(start, new[] { combat }, reward, boss);

            AssertSelectedProfile(library, RoomKind.Start, start);
            AssertSelectedProfile(library, RoomKind.Combat, combat);
            AssertSelectedProfile(library, RoomKind.Reward, reward);
            AssertSelectedProfile(library, RoomKind.Boss, boss);
        }

        [Test]
        public void RoomGenerationProfileLibrary_SameSeed_SelectsSameCombatProfile()
        {
            RoomGenerationProfileData open = CreateProfileData("combat_open", RoomKind.Combat);
            RoomGenerationProfileData cover = CreateProfileData("combat_cover", RoomKind.Combat);
            RoomGenerationProfileLibrary library = CreateProfileLibrary(
                CreateProfileData("start", RoomKind.Start),
                new[] { open, cover },
                CreateProfileData("reward", RoomKind.Reward),
                CreateProfileData("boss", RoomKind.Boss));
            RoomGenerationInput input = CreateGenerationInput(RoomKind.Combat, 456, RoomDirection.West, RoomDirection.East);

            Assert.That(library.TrySelectProfile(input, out RoomGenerationProfileData first, out string firstError), Is.True, firstError);
            Assert.That(library.TrySelectProfile(input, out RoomGenerationProfileData second, out string secondError), Is.True, secondError);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void RoomGenerator_AllRoomKinds_PassValidation()
        {
            var generator = new RoomLayoutGenerator();
            var cases = new[]
            {
                CreateGenerationInput(RoomKind.Start, 101, RoomDirection.East),
                CreateGenerationInput(RoomKind.Combat, 102, RoomDirection.West, RoomDirection.East),
                CreateGenerationInput(RoomKind.Reward, 103, RoomDirection.South),
                CreateGenerationInput(RoomKind.Boss, 104, RoomDirection.West),
            };

            for (int i = 0; i < cases.Length; i++)
            {
                RoomGenerationInput input = cases[i];
                RoomGenerationProfile profile = CreateRuntimeProfile(input);
                Assert.That(generator.TryGenerate(input, profile, out RoomResolvedLayout layout, out string error), Is.True, error);

                RoomValidationResult validation = RoomLayoutValidator.Validate(layout, profile);
                Assert.That(validation.IsValid, Is.True, $"{input.RoomKind}: {validation.FirstError}");
                Assert.That(layout.Doors.Count, Is.EqualTo(input.RequiredDoorDirections.Count));
                Assert.That(layout.PlayerEntryCells.Count, Is.EqualTo(1));

                if (input.RoomKind == RoomKind.Start || input.RoomKind == RoomKind.Reward)
                {
                    Assert.That(layout.EnemySpawnCells.Count, Is.EqualTo(0));
                }
                else
                {
                    Assert.That(layout.EnemySpawnCells.Count, Is.GreaterThan(0));
                }

                if (input.RoomKind == RoomKind.Reward)
                {
                    Assert.That(HasAnchor(layout, RoomSpecialAnchorKind.Reward), Is.True);
                }

                if (input.RoomKind == RoomKind.Boss)
                {
                    Assert.That(HasAnchor(layout, RoomSpecialAnchorKind.Boss), Is.True);
                }
            }
        }

        [Test]
        public void RoomGenerator_AllProfileAssets_PassValidation()
        {
            var generator = new RoomLayoutGenerator();
            var paths = new[]
            {
                "Assets/Data/Rooms/GenerationProfiles/Start_Default.asset",
                "Assets/Data/Rooms/GenerationProfiles/Combat_Open.asset",
                "Assets/Data/Rooms/GenerationProfiles/Combat_Cover.asset",
                "Assets/Data/Rooms/GenerationProfiles/Reward_Default.asset",
                "Assets/Data/Rooms/GenerationProfiles/Boss_Open.asset",
            };

            for (int i = 0; i < paths.Length; i++)
            {
                RoomGenerationProfileData data = AssetDatabase.LoadAssetAtPath<RoomGenerationProfileData>(paths[i]);
                Assert.That(data, Is.Not.Null, $"Missing profile asset at {paths[i]}.");
                RoomGenerationInput input = CreateGenerationInput(data.RoomKind, 900 + i, GetDefaultDirections(data.RoomKind));
                RoomGenerationProfile profile = data.BuildRuntimeProfile(input);

                Assert.That(generator.TryGenerate(input, profile, out RoomResolvedLayout layout, out string error), Is.True, $"{data.name}: {error}");
                RoomValidationResult validation = RoomLayoutValidator.Validate(layout, profile);
                Assert.That(validation.IsValid, Is.True, $"{data.name}: {validation.FirstError}");
            }
        }

        [Test]
        public void RoomValidator_RejectsBlockedDoorPaths()
        {
            RoomGenerationInput input = new(
                11,
                RoomKind.Start,
                new[] { RoomDirection.East },
                0);
            RoomResolvedLayout blocked = CreateBlockedDoorLayout();

            RoomValidationResult validation = RoomLayoutValidator.Validate(blocked, CreateRuntimeProfile(input));

            Assert.That(validation.IsValid, Is.False);
            Assert.That(ContainsIssue(validation, RoomValidationIssueType.DoorUnreachable), Is.True);
        }

        [Test]
        public void RoomRepair_RemovesBlockingObstacle()
        {
            RoomGenerationInput input = new(
                12,
                RoomKind.Start,
                new[] { RoomDirection.East },
                0);
            RoomResolvedLayout blocked = CreateBlockedDoorLayout();

            bool success = RoomLayoutRepair.TryRepair(
                blocked,
                CreateRuntimeProfile(input),
                out RoomResolvedLayout repaired,
                out RoomValidationResult validation);

            Assert.That(success, Is.True, validation.FirstError);
            Assert.That(repaired.GetSurface(4, 4), Is.EqualTo(CellData.CellSurfaceType.Ground));
            Assert.That(validation.IsValid, Is.True);
        }

        [Test]
        public void RoomGenerator_FallsBackToAlgorithmicSafeRoom_WhenRetriesFail()
        {
            var generator = new RoomLayoutGenerator();
            RoomGenerationInput input = new(
                13,
                RoomKind.Combat,
                new[] { RoomDirection.East },
                0);
            RoomGenerationProfileData data = CreateProfileData(
                "small_combat",
                RoomKind.Combat,
                new Vector2Int(3, 3),
                new Vector2Int(3, 3));
            RoomGenerationProfile profile = data.BuildRuntimeProfile(input);

            bool success = generator.TryGenerate(input, profile, out RoomResolvedLayout layout, out string error);

            Assert.That(success, Is.True, error);
            Assert.That(layout.TemplateId, Does.StartWith("algorithmic_safe"));
            RoomValidationResult validation = RoomLayoutValidator.Validate(layout, profile);
            Assert.That(validation.IsValid, Is.True, validation.FirstError);
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
            SetPrivateField(controller, "profileLibrary", CreateDefaultProfileLibrary());

            Assert.That(controller.GenerateRun(out string error), Is.True, error);
            Assert.That(controller.TryGetCurrentLayout(out RoomResolvedLayout layout, out string layoutError), Is.True, layoutError);
            Assert.That(controller.CurrentGraph, Is.Not.Null);
            Assert.That(controller.CurrentGraph.Nodes.Count, Is.EqualTo(7));
            Assert.That(controller.CurrentGraph.CurrentRoomId, Is.EqualTo("start_0"));
            Assert.That(layout.TemplateId, Does.StartWith("generated:"));
            Assert.That(presenter.LastAppliedFloorTileCount + presenter.LastAppliedWallTileCount, Is.EqualTo(layout.Width * layout.Height));
        }

        [Test]
        public void ProceduralRoomMapDebugController_UsesProfileLibrary()
        {
            TilemapRoomPresenter presenter = CreatePresenter(out _, out _);
            Tile floorTile = CreateTile();
            Tile wallTile = CreateTile();
            SetPrivateField(presenter, "floorTile", floorTile);
            SetPrivateField(presenter, "wallTile", wallTile);

            GameObject controllerObject = CreateGameObject("Debug Controller");
            ProceduralRoomMapDebugController controller = controllerObject.AddComponent<ProceduralRoomMapDebugController>();
            SetPrivateField(controller, "roomPresenter", presenter);

            Assert.That(controller.GenerateRun(out string missingError), Is.False);
            Assert.That(missingError, Does.Contain("RoomGenerationProfileLibrary"));

            RoomGenerationProfileData compactStart = CreateProfileData(
                "compact_start",
                RoomKind.Start,
                new Vector2Int(9, 9),
                new Vector2Int(9, 9));
            SetPrivateField(controller, "profileLibrary", CreateProfileLibrary(
                compactStart,
                new[] { CreateProfileData("combat", RoomKind.Combat) },
                CreateProfileData("reward", RoomKind.Reward),
                CreateProfileData("boss", RoomKind.Boss)));

            Assert.That(controller.GenerateRun(out string error), Is.True, error);
            Assert.That(controller.TryGetCurrentLayout(out RoomResolvedLayout layout, out string layoutError), Is.True, layoutError);
            Assert.That(layout.RoomKind, Is.EqualTo(RoomKind.Start));
            Assert.That(layout.Width, Is.EqualTo(9));
            Assert.That(layout.Height, Is.EqualTo(9));
        }

        [Test]
        public void ProceduralRoomMapDebugController_TryGetCurrentLayout_ReflectsGeneratedCurrentRoom()
        {
            TilemapRoomPresenter presenter = CreatePresenter(out _, out _);
            Tile floorTile = CreateTile();
            Tile wallTile = CreateTile();
            SetPrivateField(presenter, "floorTile", floorTile);
            SetPrivateField(presenter, "wallTile", wallTile);

            GameObject controllerObject = CreateGameObject("Debug Controller");
            ProceduralRoomMapDebugController controller = controllerObject.AddComponent<ProceduralRoomMapDebugController>();
            SetPrivateField(controller, "roomPresenter", presenter);
            SetPrivateField(controller, "profileLibrary", CreateDefaultProfileLibrary());

            Assert.That(controller.TryGetCurrentLayout(out _, out string missingError), Is.False);
            Assert.That(missingError, Does.Contain("Current room graph"));

            Assert.That(controller.GenerateRun(out string generateError), Is.True, generateError);
            Assert.That(controller.TryGetCurrentLayout(out RoomResolvedLayout layout, out string layoutError), Is.True, layoutError);
            Assert.That(layout.TemplateId, Does.StartWith("generated:Start:"));
            Assert.That(layout.PlayerEntryCells.Count, Is.EqualTo(1));
            Assert.That(HasAnchor(layout, RoomSpecialAnchorKind.PlayerEntry), Is.True);
        }

        [Test]
        public void ProceduralRoom2DDebugBootstrap_GenerateAndSpawn_SpawnsAtPlayerEntryAndBindsCamera()
        {
            TilemapRoomPresenter presenter = CreatePresenter(out Tilemap floorTilemap, out _);
            Tile floorTile = CreateTile();
            Tile wallTile = CreateTile();
            SetPrivateField(presenter, "floorTile", floorTile);
            SetPrivateField(presenter, "wallTile", wallTile);

            GameObject controllerObject = CreateGameObject("GeneratedRoomMapRoot");
            ProceduralRoomMapDebugController controller = controllerObject.AddComponent<ProceduralRoomMapDebugController>();
            SetPrivateField(controller, "roomPresenter", presenter);
            SetPrivateField(controller, "profileLibrary", CreateDefaultProfileLibrary());

            GameObject playerPrefabObject = CreateGameObject("Player2DPrefab");
            playerPrefabObject.AddComponent<Rigidbody2D>();
            playerPrefabObject.AddComponent<CircleCollider2D>();
            Player2DMovementController playerPrefab = playerPrefabObject.AddComponent<Player2DMovementController>();

            GameObject cameraObject = CreateGameObject("Main Camera");
            cameraObject.AddComponent<Camera>();
            Player2DIsometricCamera cameraController = cameraObject.AddComponent<Player2DIsometricCamera>();

            ProceduralRoom2DDebugBootstrap bootstrap = controllerObject.AddComponent<ProceduralRoom2DDebugBootstrap>();
            SetPrivateField(bootstrap, "roomController", controller);
            SetPrivateField(bootstrap, "roomPresenter", presenter);
            SetPrivateField(bootstrap, "playerPrefab", playerPrefab);
            SetPrivateField(bootstrap, "targetCamera", cameraController);
            SetPrivateField(bootstrap, "spawnOnStart", false);

            Assert.That(bootstrap.GenerateAndSpawn(out string error), Is.True, error);
            Assert.That(controller.TryGetCurrentLayout(out RoomResolvedLayout layout, out string layoutError), Is.True, layoutError);

            Vector2Int entry = layout.PlayerEntryCells[0];
            Vector3 expectedPosition = floorTilemap.GetCellCenterWorld(new Vector3Int(entry.x, entry.y, 0));
            Assert.That(bootstrap.SpawnedPlayer, Is.Not.Null);
            Assert.That(bootstrap.SpawnedPlayer.transform.position.x, Is.EqualTo(expectedPosition.x).Within(0.0001f));
            Assert.That(bootstrap.SpawnedPlayer.transform.position.y, Is.EqualTo(expectedPosition.y).Within(0.0001f));
            Assert.That(cameraController.Target, Is.EqualTo(bootstrap.SpawnedPlayer.transform));
        }

        private static void AssertTemplateFails(RoomTemplateData template, string expectedErrorFragment)
        {
            bool success = template.TryResolveLayout(Array.Empty<RoomDirection>(), out _, out string error);
            Assert.That(success, Is.False);
            Assert.That(error, Does.Contain(expectedErrorFragment));
        }

        private static RoomGenerationInput CreateGenerationInput(
            RoomKind kind,
            int seed,
            params RoomDirection[] directions)
        {
            return new RoomGenerationInput(
                seed,
                kind,
                directions,
                1);
        }

        private RoomGenerationProfile CreateRuntimeProfile(RoomGenerationInput input)
        {
            return CreateProfileData($"{input.RoomKind}_Runtime", input.RoomKind).BuildRuntimeProfile(input);
        }

        private RoomGenerationProfileData CreateProfileData(
            string id,
            RoomKind kind,
            Vector2Int? widthRangeOverride = null,
            Vector2Int? heightRangeOverride = null)
        {
            RoomGenerationProfileData data = ScriptableObject.CreateInstance<RoomGenerationProfileData>();
            data.name = id;
            createdObjects.Add(data);

            SetPrivateField(data, "id", id);
            SetPrivateField(data, "roomKind", kind);
            SetPrivateField(data, "widthRange", widthRangeOverride ?? new Vector2Int(17, 21));
            SetPrivateField(data, "heightRange", heightRangeOverride ?? new Vector2Int(15, 19));
            SetPrivateField(data, "minimumWidth", kind switch
            {
                RoomKind.Boss => 15,
                RoomKind.Combat => 13,
                _ => 9,
            });
            SetPrivateField(data, "minimumHeight", kind switch
            {
                RoomKind.Boss => 13,
                RoomKind.Combat => 11,
                _ => 9,
            });
            SetPrivateField(data, "minPathWidth", kind == RoomKind.Boss ? 3 : 2);
            SetPrivateField(data, "playerSafeRadius", kind == RoomKind.Reward ? 2 : 3);
            SetPrivateField(data, "doorBufferRadius", 2);
            SetPrivateField(data, "obstacleCountRange", kind switch
            {
                RoomKind.Start => new Vector2Int(0, 1),
                RoomKind.Reward => new Vector2Int(0, 2),
                RoomKind.Boss => new Vector2Int(0, 3),
                _ => new Vector2Int(4, 7),
            });
            SetPrivateField(data, "obstacleWidthRange", new Vector2Int(1, 3));
            SetPrivateField(data, "obstacleHeightRange", new Vector2Int(1, 2));
            SetPrivateField(data, "maxObstacleDensity", kind switch
            {
                RoomKind.Reward => 0.15f,
                RoomKind.Combat => 0.24f,
                _ => 0.12f,
            });
            SetPrivateField(data, "enemySpawnCountRange", kind switch
            {
                RoomKind.Combat => new Vector2Int(3, 5),
                RoomKind.Boss => new Vector2Int(1, 3),
                _ => Vector2Int.zero,
            });
            SetPrivateField(data, "minEnemySpawnDistance", kind switch
            {
                RoomKind.Combat => 5,
                RoomKind.Boss => 6,
                _ => 0,
            });
            SetPrivateField(data, "minCombatGroundCells", kind switch
            {
                RoomKind.Start => 32,
                RoomKind.Reward => 30,
                RoomKind.Boss => 80,
                _ => 56,
            });
            SetPrivateField(data, "bossOpenRadius", kind == RoomKind.Boss ? 3 : 0);
            SetPrivateField(data, "maxPlacementAttempts", 32);
            SetPrivateField(data, "maxGenerationRetries", 4);
            return data;
        }

        private RoomGenerationProfileLibrary CreateDefaultProfileLibrary()
        {
            return CreateProfileLibrary(
                CreateProfileData("start", RoomKind.Start),
                new[] { CreateProfileData("combat", RoomKind.Combat) },
                CreateProfileData("reward", RoomKind.Reward),
                CreateProfileData("boss", RoomKind.Boss));
        }

        private RoomGenerationProfileLibrary CreateProfileLibrary(
            RoomGenerationProfileData start,
            RoomGenerationProfileData[] combat,
            RoomGenerationProfileData reward,
            RoomGenerationProfileData boss)
        {
            RoomGenerationProfileLibrary library = ScriptableObject.CreateInstance<RoomGenerationProfileLibrary>();
            library.name = "profile_library";
            createdObjects.Add(library);
            SetPrivateField(library, "startProfile", start);
            SetPrivateField(library, "combatProfiles", new List<RoomGenerationProfileData>(combat));
            SetPrivateField(library, "rewardProfile", reward);
            SetPrivateField(library, "bossProfile", boss);
            return library;
        }

        private static void AssertSelectedProfile(
            RoomGenerationProfileLibrary library,
            RoomKind kind,
            RoomGenerationProfileData expectedProfile)
        {
            Assert.That(library.TrySelectProfile(CreateGenerationInput(kind, 123, GetDefaultDirections(kind)), out RoomGenerationProfileData selected, out string error), Is.True, error);
            Assert.That(selected, Is.EqualTo(expectedProfile));
        }

        private static RoomDirection[] GetDefaultDirections(RoomKind kind)
        {
            return kind switch
            {
                RoomKind.Start => new[] { RoomDirection.East },
                RoomKind.Combat => new[] { RoomDirection.West, RoomDirection.East },
                RoomKind.Reward => new[] { RoomDirection.South },
                RoomKind.Boss => new[] { RoomDirection.West },
                _ => Array.Empty<RoomDirection>(),
            };
        }

        private static bool HasAnchor(RoomResolvedLayout layout, RoomSpecialAnchorKind kind)
        {
            for (int i = 0; i < layout.SpecialAnchors.Count; i++)
            {
                if (layout.SpecialAnchors[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIssue(RoomValidationResult validation, RoomValidationIssueType issueType)
        {
            for (int i = 0; i < validation.Issues.Count; i++)
            {
                if (validation.Issues[i].Type == issueType)
                {
                    return true;
                }
            }

            return false;
        }

        private static RoomResolvedLayout CreateBlockedDoorLayout()
        {
            const int width = 9;
            const int height = 9;
            var surfaces = new List<CellData.CellSurfaceType>(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool boundary = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    bool blocker = x == 4 && y > 0 && y < height - 1;
                    surfaces.Add(boundary || blocker ? CellData.CellSurfaceType.Wall : CellData.CellSurfaceType.Ground);
                }
            }

            Vector2Int playerEntry = new(2, 4);
            var door = new RoomDoor(RoomDirection.East, new Vector2Int(width - 1, 4));
            surfaces[(door.Coordinates.y * width) + door.Coordinates.x] = CellData.CellSurfaceType.Ground;
            return new RoomResolvedLayout(
                "blocked",
                RoomKind.Start,
                width,
                height,
                surfaces,
                new[] { playerEntry },
                Array.Empty<Vector2Int>(),
                new[] { door },
                new[] { new RoomSpecialAnchor(RoomSpecialAnchorKind.PlayerEntry, playerEntry) });
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

        private static string BuildLayoutSignature(RoomResolvedLayout layout)
        {
            var lines = new List<string>
            {
                $"{layout.TemplateId}|{layout.RoomKind}|{layout.Width}x{layout.Height}",
            };

            for (int y = 0; y < layout.Height; y++)
            {
                var chars = new char[layout.Width];
                for (int x = 0; x < layout.Width; x++)
                {
                    chars[x] = layout.GetSurface(x, y) == CellData.CellSurfaceType.Wall ? '#' : '.';
                }

                lines.Add(new string(chars));
            }

            for (int i = 0; i < layout.Doors.Count; i++)
            {
                RoomDoor door = layout.Doors[i];
                lines.Add($"D:{door.Direction}:{door.Coordinates.x},{door.Coordinates.y}");
            }

            for (int i = 0; i < layout.EnemySpawnCells.Count; i++)
            {
                Vector2Int spawn = layout.EnemySpawnCells[i];
                lines.Add($"S:{spawn.x},{spawn.y}");
            }

            for (int i = 0; i < layout.SpecialAnchors.Count; i++)
            {
                RoomSpecialAnchor anchor = layout.SpecialAnchors[i];
                lines.Add($"A:{anchor.Kind}:{anchor.Coordinates.x},{anchor.Coordinates.y}");
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
