using System;
using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.MapGrid
{
    public sealed class RoomLayoutGenerator
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down,
        };

        public bool TryGenerate(
            RoomGenerationInput input,
            RoomGenerationProfile profile,
            out RoomResolvedLayout layout,
            out string error)
        {
            layout = null;
            error = null;
            if (profile == null)
            {
                error = "Room generation requires a runtime profile.";
                return false;
            }

            var errors = new List<string>();
            if (profile.RequestedRangeCanFit)
            {
                for (int retryIndex = 0; retryIndex < profile.MaxGenerationRetries; retryIndex++)
                {
                    var random = new VocalithRandom(input.GetAttemptSeed(retryIndex));
                    if (!TryBuildCandidate(input, profile, random, retryIndex, out RoomResolvedLayout candidate, out string buildError))
                    {
                        errors.Add(buildError);
                        continue;
                    }

                    RoomValidationResult validation = RoomLayoutValidator.Validate(candidate, profile);
                    if (validation.IsValid)
                    {
                        layout = candidate;
                        return true;
                    }

                    if (RoomLayoutRepair.TryRepair(candidate, profile, out RoomResolvedLayout repaired, out RoomValidationResult repairValidation))
                    {
                        layout = repaired;
                        return true;
                    }

                    errors.Add(repairValidation.FirstError);
                }
            }

            if (TryGenerateSafeRoom(input, profile, out layout, out string safeRoomError))
            {
                return true;
            }

            errors.Add(safeRoomError);
            error = string.Join(Environment.NewLine, errors);
            return false;
        }

        private static bool TryBuildCandidate(
            RoomGenerationInput input,
            RoomGenerationProfile profile,
            VocalithRandom random,
            int retryIndex,
            out RoomResolvedLayout layout,
            out string error)
        {
            layout = null;
            error = null;

            int width = NextDimension(random, profile.WidthRange.x, profile.WidthRange.y, profile.MinimumWidth);
            int height = NextDimension(random, profile.HeightRange.x, profile.HeightRange.y, profile.MinimumHeight);
            if (width < profile.MinimumWidth || height < profile.MinimumHeight)
            {
                error = $"Requested {input.RoomKind} room range is too small.";
                return false;
            }

            List<CellData.CellSurfaceType> surfaces = CreateWalledRoom(width, height);
            List<RoomDoor> doors = CreateDoors(width, height, input.RequiredDoorDirections, random);
            Vector2Int playerEntry = ResolvePlayerEntry(input.RoomKind, width, height);
            List<RoomSpecialAnchor> anchors = CreateSpecialAnchors(input.RoomKind, width, height, playerEntry);
            var reserved = new bool[width * height];

            SetGround(surfaces, width, playerEntry);
            ReserveDisk(surfaces, reserved, width, height, playerEntry, profile.PlayerSafeRadius);

            for (int i = 0; i < doors.Count; i++)
            {
                RoomDoor door = doors[i];
                SetGround(surfaces, width, door.Coordinates);
                ReserveDoorBuffer(surfaces, reserved, width, height, door, profile.DoorBufferRadius);
                CarvePath(surfaces, reserved, width, height, playerEntry, GetDoorInteriorCell(door, width, height), profile.MinPathWidth, random.NextBool());
                CarvePath(surfaces, reserved, width, height, GetDoorInteriorCell(door, width, height), door.Coordinates, profile.MinPathWidth, true);
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                RoomSpecialAnchor anchor = anchors[i];
                SetGround(surfaces, width, anchor.Coordinates);
                int radius = anchor.Kind == RoomSpecialAnchorKind.Boss ? profile.BossOpenRadius : 1;
                ReserveDisk(surfaces, reserved, width, height, anchor.Coordinates, radius);
                CarvePath(surfaces, reserved, width, height, playerEntry, anchor.Coordinates, profile.MinPathWidth, random.NextBool());
            }

            SealBoundaryWallsExceptDoors(surfaces, width, height, doors);
            PlaceObstacles(surfaces, reserved, width, height, playerEntry, anchors, doors, profile, random);
            List<Vector2Int> enemySpawns = ResolveEnemySpawns(surfaces, reserved, width, height, playerEntry, anchors, doors, profile, random);
            layout = new RoomResolvedLayout(
                $"generated:{input.RoomKind}:{input.Seed}:{retryIndex}",
                input.RoomKind,
                width,
                height,
                surfaces,
                new[] { playerEntry },
                enemySpawns,
                doors,
                anchors);
            return true;
        }

        private static bool TryGenerateSafeRoom(
            RoomGenerationInput input,
            RoomGenerationProfile profile,
            out RoomResolvedLayout layout,
            out string error)
        {
            layout = null;
            error = null;

            int width = MakeOdd(Mathf.Max(profile.MinimumWidth, profile.WidthRange.x));
            int height = MakeOdd(Mathf.Max(profile.MinimumHeight, profile.HeightRange.x));
            List<CellData.CellSurfaceType> surfaces = CreateWalledRoom(width, height);
            List<RoomDoor> doors = CreateCenteredDoors(width, height, input.RequiredDoorDirections);
            Vector2Int playerEntry = ResolvePlayerEntry(input.RoomKind, width, height);
            List<RoomSpecialAnchor> anchors = CreateSpecialAnchors(input.RoomKind, width, height, playerEntry);
            var reserved = new bool[width * height];

            SetGround(surfaces, width, playerEntry);
            ReserveDisk(surfaces, reserved, width, height, playerEntry, profile.PlayerSafeRadius);
            for (int i = 0; i < doors.Count; i++)
            {
                RoomDoor door = doors[i];
                SetGround(surfaces, width, door.Coordinates);
                ReserveDoorBuffer(surfaces, reserved, width, height, door, profile.DoorBufferRadius);
                CarvePath(surfaces, reserved, width, height, playerEntry, GetDoorInteriorCell(door, width, height), profile.MinPathWidth, true);
                CarvePath(surfaces, reserved, width, height, GetDoorInteriorCell(door, width, height), door.Coordinates, profile.MinPathWidth, true);
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                RoomSpecialAnchor anchor = anchors[i];
                SetGround(surfaces, width, anchor.Coordinates);
                int radius = anchor.Kind == RoomSpecialAnchorKind.Boss ? profile.BossOpenRadius : 1;
                ReserveDisk(surfaces, reserved, width, height, anchor.Coordinates, radius);
                CarvePath(surfaces, reserved, width, height, playerEntry, anchor.Coordinates, profile.MinPathWidth, true);
            }

            SealBoundaryWallsExceptDoors(surfaces, width, height, doors);
            var random = new VocalithRandom(input.GetAttemptSeed(997));
            List<Vector2Int> enemySpawns = ResolveEnemySpawns(surfaces, reserved, width, height, playerEntry, anchors, doors, profile, random);
            layout = new RoomResolvedLayout(
                $"algorithmic_safe:{input.RoomKind}:{input.Seed}",
                input.RoomKind,
                width,
                height,
                surfaces,
                new[] { playerEntry },
                enemySpawns,
                doors,
                anchors);

            RoomValidationResult validation = RoomLayoutValidator.Validate(layout, profile);
            if (validation.IsValid)
            {
                return true;
            }

            error = validation.FirstError;
            layout = null;
            return false;
        }

        internal static void SealBoundaryWallsExceptDoors(
            IList<CellData.CellSurfaceType> surfaces,
            int width,
            int height,
            IReadOnlyList<RoomDoor> doors)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x != 0 && y != 0 && x != width - 1 && y != height - 1)
                    {
                        continue;
                    }

                    Vector2Int cell = new(x, y);
                    surfaces[GetIndex(x, y, width)] = IsDoorCell(cell, doors)
                        ? CellData.CellSurfaceType.Ground
                        : CellData.CellSurfaceType.Wall;
                }
            }
        }

        private static int NextDimension(VocalithRandom random, int minInclusive, int maxInclusive, int minimum)
        {
            int min = Mathf.Max(minimum, minInclusive);
            int max = Mathf.Max(min, maxInclusive);
            return NextInclusive(random, min, max);
        }

        private static int MakeOdd(int value)
        {
            return (value & 1) == 0 ? value + 1 : value;
        }

        private static List<CellData.CellSurfaceType> CreateWalledRoom(int width, int height)
        {
            var surfaces = new List<CellData.CellSurfaceType>(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool boundary = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    surfaces.Add(boundary ? CellData.CellSurfaceType.Wall : CellData.CellSurfaceType.Ground);
                }
            }

            return surfaces;
        }

        private static List<RoomDoor> CreateDoors(
            int width,
            int height,
            IReadOnlyList<RoomDirection> directions,
            VocalithRandom random)
        {
            var doors = new List<RoomDoor>(directions.Count);
            for (int i = 0; i < directions.Count; i++)
            {
                RoomDirection direction = directions[i];
                int jitter = random.Next(-2, 3);
                doors.Add(CreateDoor(width, height, direction, jitter));
            }

            return doors;
        }

        private static List<RoomDoor> CreateCenteredDoors(int width, int height, IReadOnlyList<RoomDirection> directions)
        {
            var doors = new List<RoomDoor>(directions.Count);
            for (int i = 0; i < directions.Count; i++)
            {
                doors.Add(CreateDoor(width, height, directions[i], 0));
            }

            return doors;
        }

        private static RoomDoor CreateDoor(int width, int height, RoomDirection direction, int jitter)
        {
            int centerX = Mathf.Clamp((width / 2) + jitter, 2, width - 3);
            int centerY = Mathf.Clamp((height / 2) + jitter, 2, height - 3);
            switch (direction)
            {
                case RoomDirection.North:
                    return new RoomDoor(direction, new Vector2Int(centerX, height - 1));
                case RoomDirection.East:
                    return new RoomDoor(direction, new Vector2Int(width - 1, centerY));
                case RoomDirection.South:
                    return new RoomDoor(direction, new Vector2Int(centerX, 0));
                case RoomDirection.West:
                    return new RoomDoor(direction, new Vector2Int(0, centerY));
                default:
                    return new RoomDoor(RoomDirection.North, new Vector2Int(centerX, height - 1));
            }
        }

        private static Vector2Int ResolvePlayerEntry(RoomKind kind, int width, int height)
        {
            switch (kind)
            {
                case RoomKind.Reward:
                case RoomKind.Boss:
                    return new Vector2Int(width / 2, Mathf.Clamp(2, 1, height - 2));
                case RoomKind.Start:
                case RoomKind.Combat:
                default:
                    return new Vector2Int(width / 2, height / 2);
            }
        }

        private static List<RoomSpecialAnchor> CreateSpecialAnchors(RoomKind kind, int width, int height, Vector2Int playerEntry)
        {
            var anchors = new List<RoomSpecialAnchor>
            {
                new(RoomSpecialAnchorKind.PlayerEntry, playerEntry),
            };

            switch (kind)
            {
                case RoomKind.Reward:
                    anchors.Add(new RoomSpecialAnchor(RoomSpecialAnchorKind.Reward, new Vector2Int(width / 2, height - 3)));
                    break;
                case RoomKind.Boss:
                    anchors.Add(new RoomSpecialAnchor(RoomSpecialAnchorKind.Boss, new Vector2Int(width / 2, height / 2)));
                    break;
            }

            return anchors;
        }

        private static Vector2Int GetDoorInteriorCell(RoomDoor door, int width, int height)
        {
            switch (door.Direction)
            {
                case RoomDirection.North:
                    return new Vector2Int(door.Coordinates.x, height - 2);
                case RoomDirection.East:
                    return new Vector2Int(width - 2, door.Coordinates.y);
                case RoomDirection.South:
                    return new Vector2Int(door.Coordinates.x, 1);
                case RoomDirection.West:
                    return new Vector2Int(1, door.Coordinates.y);
                default:
                    return door.Coordinates;
            }
        }

        private static void PlaceObstacles(
            List<CellData.CellSurfaceType> surfaces,
            IReadOnlyList<bool> reserved,
            int width,
            int height,
            Vector2Int playerEntry,
            IReadOnlyList<RoomSpecialAnchor> anchors,
            IReadOnlyList<RoomDoor> doors,
            RoomGenerationProfile profile,
            VocalithRandom random)
        {
            int targetCount = NextInclusive(random, profile.ObstacleCountRange.x, profile.ObstacleCountRange.y);
            for (int placed = 0; placed < targetCount; placed++)
            {
                for (int attempt = 0; attempt < profile.MaxPlacementAttempts; attempt++)
                {
                    int obstacleWidth = NextInclusive(random, profile.ObstacleWidthRange.x, profile.ObstacleWidthRange.y);
                    int obstacleHeight = NextInclusive(random, profile.ObstacleHeightRange.x, profile.ObstacleHeightRange.y);
                    int x = random.Next(1, Mathf.Max(2, width - obstacleWidth - 1));
                    int y = random.Next(1, Mathf.Max(2, height - obstacleHeight - 1));
                    if (!TryPlaceObstacle(surfaces, reserved, width, height, x, y, obstacleWidth, obstacleHeight))
                    {
                        continue;
                    }

                    if (AreImportantCellsReachable(surfaces, width, height, playerEntry, anchors, doors))
                    {
                        break;
                    }

                    ClearRectangle(surfaces, width, x, y, obstacleWidth, obstacleHeight);
                }
            }
        }

        private static bool TryPlaceObstacle(
            IList<CellData.CellSurfaceType> surfaces,
            IReadOnlyList<bool> reserved,
            int width,
            int height,
            int minX,
            int minY,
            int obstacleWidth,
            int obstacleHeight)
        {
            for (int y = minY; y < minY + obstacleHeight; y++)
            {
                for (int x = minX; x < minX + obstacleWidth; x++)
                {
                    if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1)
                    {
                        return false;
                    }

                    int index = GetIndex(x, y, width);
                    if (reserved[index] || surfaces[index] == CellData.CellSurfaceType.Wall)
                    {
                        return false;
                    }
                }
            }

            for (int y = minY; y < minY + obstacleHeight; y++)
            {
                for (int x = minX; x < minX + obstacleWidth; x++)
                {
                    surfaces[GetIndex(x, y, width)] = CellData.CellSurfaceType.Wall;
                }
            }

            return true;
        }

        private static void ClearRectangle(IList<CellData.CellSurfaceType> surfaces, int width, int minX, int minY, int rectangleWidth, int rectangleHeight)
        {
            for (int y = minY; y < minY + rectangleHeight; y++)
            {
                for (int x = minX; x < minX + rectangleWidth; x++)
                {
                    surfaces[GetIndex(x, y, width)] = CellData.CellSurfaceType.Ground;
                }
            }
        }

        private static List<Vector2Int> ResolveEnemySpawns(
            IReadOnlyList<CellData.CellSurfaceType> surfaces,
            IReadOnlyList<bool> reserved,
            int width,
            int height,
            Vector2Int playerEntry,
            IReadOnlyList<RoomSpecialAnchor> anchors,
            IReadOnlyList<RoomDoor> doors,
            RoomGenerationProfile profile,
            VocalithRandom random)
        {
            var spawns = new List<Vector2Int>();
            if (!profile.AllowsEnemySpawns)
            {
                return spawns;
            }

            var candidates = new List<Vector2Int>();
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = GetIndex(x, y, width);
                    Vector2Int cell = new(x, y);
                    if (surfaces[index] != CellData.CellSurfaceType.Ground ||
                        reserved[index] ||
                        ManhattanDistance(cell, playerEntry) < profile.MinEnemySpawnDistance ||
                        IsAnchorCell(cell, anchors) ||
                        IsNearDoor(cell, doors, radius: 2))
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }
            }

            Shuffle(candidates, random);
            int targetCount = Mathf.Min(candidates.Count, NextInclusive(random, profile.EnemySpawnCountRange.x, profile.EnemySpawnCountRange.y));
            for (int i = 0; i < targetCount; i++)
            {
                spawns.Add(candidates[i]);
            }

            return spawns;
        }

        private static bool AreImportantCellsReachable(
            IReadOnlyList<CellData.CellSurfaceType> surfaces,
            int width,
            int height,
            Vector2Int playerEntry,
            IReadOnlyList<RoomSpecialAnchor> anchors,
            IReadOnlyList<RoomDoor> doors)
        {
            bool[] reachable = FloodFill(surfaces, width, height, playerEntry);
            for (int i = 0; i < doors.Count; i++)
            {
                if (!IsReachable(reachable, width, doors[i].Coordinates))
                {
                    return false;
                }
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                if (!IsReachable(reachable, width, anchors[i].Coordinates))
                {
                    return false;
                }
            }

            return true;
        }

        internal static void CarvePath(
            IList<CellData.CellSurfaceType> surfaces,
            IList<bool> reserved,
            int width,
            int height,
            Vector2Int from,
            Vector2Int to,
            int minPathWidth,
            bool horizontalFirst)
        {
            Vector2Int corner = horizontalFirst ? new Vector2Int(to.x, from.y) : new Vector2Int(from.x, to.y);
            CarveStraightPath(surfaces, reserved, width, height, from, corner, minPathWidth);
            CarveStraightPath(surfaces, reserved, width, height, corner, to, minPathWidth);
        }

        private static void CarveStraightPath(
            IList<CellData.CellSurfaceType> surfaces,
            IList<bool> reserved,
            int width,
            int height,
            Vector2Int from,
            Vector2Int to,
            int minPathWidth)
        {
            int stepX = Math.Sign(to.x - from.x);
            int stepY = Math.Sign(to.y - from.y);
            Vector2Int current = from;
            ReservePathCell(surfaces, reserved, width, height, current, minPathWidth);
            while (current != to)
            {
                if (current.x != to.x)
                {
                    current.x += stepX;
                }
                else if (current.y != to.y)
                {
                    current.y += stepY;
                }

                ReservePathCell(surfaces, reserved, width, height, current, minPathWidth);
            }
        }

        private static void ReservePathCell(
            IList<CellData.CellSurfaceType> surfaces,
            IList<bool> reserved,
            int width,
            int height,
            Vector2Int center,
            int minPathWidth)
        {
            int radius = Mathf.Max(0, minPathWidth / 2);
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    int index = GetIndex(x, y, width);
                    surfaces[index] = CellData.CellSurfaceType.Ground;
                    reserved[index] = true;
                }
            }
        }

        private static void ReserveDoorBuffer(
            IList<CellData.CellSurfaceType> surfaces,
            IList<bool> reserved,
            int width,
            int height,
            RoomDoor door,
            int radius)
        {
            ReserveDisk(surfaces, reserved, width, height, door.Coordinates, radius);
            ReserveDisk(surfaces, reserved, width, height, GetDoorInteriorCell(door, width, height), radius);
        }

        private static void ReserveDisk(
            IList<CellData.CellSurfaceType> surfaces,
            IList<bool> reserved,
            int width,
            int height,
            Vector2Int center,
            int radius)
        {
            int radiusSquared = Mathf.Max(0, radius) * Mathf.Max(0, radius);
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    int dx = x - center.x;
                    int dy = y - center.y;
                    if ((dx * dx) + (dy * dy) > radiusSquared)
                    {
                        continue;
                    }

                    int index = GetIndex(x, y, width);
                    surfaces[index] = CellData.CellSurfaceType.Ground;
                    reserved[index] = true;
                }
            }
        }

        private static void SetGround(IList<CellData.CellSurfaceType> surfaces, int width, Vector2Int cell)
        {
            surfaces[GetIndex(cell.x, cell.y, width)] = CellData.CellSurfaceType.Ground;
        }

        internal static bool[] FloodFill(
            IReadOnlyList<CellData.CellSurfaceType> surfaces,
            int width,
            int height,
            Vector2Int start)
        {
            var visited = new bool[surfaces.Count];
            if (!IsInside(start, width, height) || surfaces[GetIndex(start.x, start.y, width)] != CellData.CellSurfaceType.Ground)
            {
                return visited;
            }

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited[GetIndex(start.x, start.y, width)] = true;
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    Vector2Int next = current + CardinalDirections[i];
                    if (!IsInside(next, width, height))
                    {
                        continue;
                    }

                    int nextIndex = GetIndex(next.x, next.y, width);
                    if (visited[nextIndex] || surfaces[nextIndex] != CellData.CellSurfaceType.Ground)
                    {
                        continue;
                    }

                    visited[nextIndex] = true;
                    queue.Enqueue(next);
                }
            }

            return visited;
        }

        internal static int GetIndex(int x, int y, int width)
        {
            return (y * width) + x;
        }

        internal static bool IsInside(Vector2Int cell, int width, int height)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }

        private static bool IsReachable(IReadOnlyList<bool> reachable, int width, Vector2Int cell)
        {
            int index = GetIndex(cell.x, cell.y, width);
            return index >= 0 && index < reachable.Count && reachable[index];
        }

        private static bool IsAnchorCell(Vector2Int cell, IReadOnlyList<RoomSpecialAnchor> anchors)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].Coordinates == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDoorCell(Vector2Int cell, IReadOnlyList<RoomDoor> doors)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].Coordinates == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNearDoor(Vector2Int cell, IReadOnlyList<RoomDoor> doors, int radius)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (ManhattanDistance(cell, doors[i].Coordinates) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Shuffle<T>(IList<T> values, VocalithRandom random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(0, i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }

        private static int ManhattanDistance(Vector2Int left, Vector2Int right)
        {
            return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
        }

        private static int NextInclusive(VocalithRandom random, int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive)
            {
                return minInclusive;
            }

            return random.Next(minInclusive, maxInclusive + 1);
        }
    }

    public static class RoomLayoutValidator
    {
        public static RoomValidationResult Validate(RoomResolvedLayout layout, RoomGenerationProfile profile)
        {
            var issues = new List<RoomValidationIssue>();
            if (layout == null)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.InvalidDimensions, "Room layout is missing."));
                return new RoomValidationResult(issues);
            }

            if (layout.Width <= 0 || layout.Height <= 0)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.InvalidDimensions, "Room layout must have positive dimensions."));
                return new RoomValidationResult(issues);
            }

            if (layout.Surfaces == null || layout.Surfaces.Count != layout.Width * layout.Height)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.SurfaceCountMismatch, "Room layout surface count does not match dimensions."));
                return new RoomValidationResult(issues);
            }

            if (layout.PlayerEntryCells == null || layout.PlayerEntryCells.Count == 0)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.MissingPlayerEntry, "Room layout must contain a player entry."));
                return new RoomValidationResult(issues);
            }

            Vector2Int playerEntry = layout.PlayerEntryCells[0];
            if (!IsGround(layout, playerEntry))
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.PlayerEntryNotGround, "Player entry must be placed on ground.", playerEntry));
                return new RoomValidationResult(issues);
            }

            bool[] reachable = RoomLayoutGenerator.FloodFill(layout.Surfaces, layout.Width, layout.Height, playerEntry);
            ValidateBoundaryWalls(layout, issues);
            ValidateDoors(layout, reachable, profile, issues);
            ValidateAnchors(layout, reachable, profile, issues);
            ValidateGroundConnectivity(layout, reachable, issues);
            ValidateCombatArea(layout, reachable, profile, issues);
            ValidateObstacleDensity(layout, profile, issues);
            ValidateEnemySpawns(layout, reachable, profile, playerEntry, issues);
            return new RoomValidationResult(issues);
        }

        private static void ValidateBoundaryWalls(RoomResolvedLayout layout, List<RoomValidationIssue> issues)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (x != 0 && y != 0 && x != layout.Width - 1 && y != layout.Height - 1)
                    {
                        continue;
                    }

                    Vector2Int cell = new(x, y);
                    bool isDoor = false;
                    for (int i = 0; i < layout.Doors.Count; i++)
                    {
                        if (layout.Doors[i].Coordinates == cell)
                        {
                            isDoor = true;
                            break;
                        }
                    }

                    bool isGround = layout.GetSurface(x, y) == CellData.CellSurfaceType.Ground;
                    if (isGround != isDoor)
                    {
                        issues.Add(new RoomValidationIssue(RoomValidationIssueType.DoorInvalid, "Room boundary must be wall except at door cells.", cell));
                    }
                }
            }
        }

        private static void ValidateDoors(
            RoomResolvedLayout layout,
            IReadOnlyList<bool> reachable,
            RoomGenerationProfile profile,
            List<RoomValidationIssue> issues)
        {
            for (int i = 0; i < layout.Doors.Count; i++)
            {
                RoomDoor door = layout.Doors[i];
                if (!IsDoorOnBoundary(layout, door) || !IsGround(layout, door.Coordinates))
                {
                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.DoorInvalid, "Door must be ground on the room boundary.", door.Coordinates));
                    continue;
                }

                if (!reachable[RoomLayoutGenerator.GetIndex(door.Coordinates.x, door.Coordinates.y, layout.Width)])
                {
                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.DoorUnreachable, "Door must be reachable from player entry.", door.Coordinates));
                }

                if (!HasDoorBuffer(layout, door, profile.MinPathWidth))
                {
                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.DoorBufferTooNarrow, "Door buffer is narrower than the room profile requires.", door.Coordinates));
                }
            }
        }

        private static void ValidateAnchors(
            RoomResolvedLayout layout,
            IReadOnlyList<bool> reachable,
            RoomGenerationProfile profile,
            List<RoomValidationIssue> issues)
        {
            bool hasReward = false;
            bool hasBoss = false;
            for (int i = 0; i < layout.SpecialAnchors.Count; i++)
            {
                RoomSpecialAnchor anchor = layout.SpecialAnchors[i];
                if (!IsGround(layout, anchor.Coordinates))
                {
                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.AnchorUnreachable, "Room anchor must be placed on ground.", anchor.Coordinates));
                    continue;
                }

                if (!reachable[RoomLayoutGenerator.GetIndex(anchor.Coordinates.x, anchor.Coordinates.y, layout.Width)])
                {
                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.AnchorUnreachable, "Room anchor must be reachable from player entry.", anchor.Coordinates));
                }

                hasReward |= anchor.Kind == RoomSpecialAnchorKind.Reward;
                hasBoss |= anchor.Kind == RoomSpecialAnchorKind.Boss;
            }

            if (profile.RequiresRewardAnchor && !hasReward)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.AnchorMissing, "Reward room must contain a reward anchor."));
            }

            if (profile.RequiresBossAnchor && !hasBoss)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.AnchorMissing, "Boss room must contain a boss anchor."));
            }
        }

        private static void ValidateGroundConnectivity(
            RoomResolvedLayout layout,
            IReadOnlyList<bool> reachable,
            List<RoomValidationIssue> issues)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    int index = RoomLayoutGenerator.GetIndex(x, y, layout.Width);
                    if (layout.Surfaces[index] != CellData.CellSurfaceType.Ground || reachable[index])
                    {
                        continue;
                    }

                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.IsolatedGround, "Ground island is not reachable from player entry.", new Vector2Int(x, y)));
                    return;
                }
            }
        }

        private static void ValidateCombatArea(
            RoomResolvedLayout layout,
            IReadOnlyList<bool> reachable,
            RoomGenerationProfile profile,
            List<RoomValidationIssue> issues)
        {
            int reachableGround = 0;
            for (int i = 0; i < reachable.Count; i++)
            {
                if (reachable[i])
                {
                    reachableGround++;
                }
            }

            if (reachableGround < profile.MinCombatGroundCells)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.CombatAreaTooSmall, "Reachable combat area is smaller than the room profile requires."));
                return;
            }

            if (!profile.RequiresBossAnchor)
            {
                return;
            }

            for (int i = 0; i < layout.SpecialAnchors.Count; i++)
            {
                RoomSpecialAnchor anchor = layout.SpecialAnchors[i];
                if (anchor.Kind != RoomSpecialAnchorKind.Boss)
                {
                    continue;
                }

                int openCells = CountGroundInRadius(layout, anchor.Coordinates, profile.BossOpenRadius);
                int requiredOpenCells = Mathf.Max(9, profile.BossOpenRadius * profile.BossOpenRadius);
                if (openCells < requiredOpenCells)
                {
                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.CombatAreaTooSmall, "Boss room must keep an open boss area.", anchor.Coordinates));
                }

                return;
            }
        }

        private static void ValidateObstacleDensity(RoomResolvedLayout layout, RoomGenerationProfile profile, List<RoomValidationIssue> issues)
        {
            int interiorCells = 0;
            int interiorWalls = 0;
            for (int y = 1; y < layout.Height - 1; y++)
            {
                for (int x = 1; x < layout.Width - 1; x++)
                {
                    interiorCells++;
                    if (layout.GetSurface(x, y) == CellData.CellSurfaceType.Wall)
                    {
                        interiorWalls++;
                    }
                }
            }

            if (interiorCells == 0)
            {
                return;
            }

            float density = (float)interiorWalls / interiorCells;
            if (density > profile.MaxObstacleDensity)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.ObstacleDensityOutOfRange, "Obstacle density is higher than the room profile allows."));
            }
        }

        private static void ValidateEnemySpawns(
            RoomResolvedLayout layout,
            IReadOnlyList<bool> reachable,
            RoomGenerationProfile profile,
            Vector2Int playerEntry,
            List<RoomValidationIssue> issues)
        {
            int spawnCount = layout.EnemySpawnCells?.Count ?? 0;
            if (!profile.AllowsEnemySpawns && spawnCount > 0)
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.EnemySpawnInvalid, "Start and reward rooms must not contain enemy spawns."));
                return;
            }

            if (profile.AllowsEnemySpawns &&
                (spawnCount < profile.EnemySpawnCountRange.x || spawnCount > profile.EnemySpawnCountRange.y))
            {
                issues.Add(new RoomValidationIssue(RoomValidationIssueType.EnemySpawnInvalid, "Enemy spawn count does not match the room profile."));
                return;
            }

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2Int spawn = layout.EnemySpawnCells[i];
                if (!IsGround(layout, spawn) ||
                    !reachable[RoomLayoutGenerator.GetIndex(spawn.x, spawn.y, layout.Width)] ||
                    Mathf.Abs(spawn.x - playerEntry.x) + Mathf.Abs(spawn.y - playerEntry.y) < profile.MinEnemySpawnDistance)
                {
                    issues.Add(new RoomValidationIssue(RoomValidationIssueType.EnemySpawnInvalid, "Enemy spawn must be reachable ground away from player entry.", spawn));
                }
            }
        }

        private static bool HasDoorBuffer(RoomResolvedLayout layout, RoomDoor door, int minPathWidth)
        {
            int radius = Mathf.Max(0, minPathWidth / 2);
            Vector2Int interior = door.Direction switch
            {
                RoomDirection.North => new Vector2Int(door.Coordinates.x, layout.Height - 2),
                RoomDirection.East => new Vector2Int(layout.Width - 2, door.Coordinates.y),
                RoomDirection.South => new Vector2Int(door.Coordinates.x, 1),
                RoomDirection.West => new Vector2Int(1, door.Coordinates.y),
                _ => door.Coordinates,
            };

            for (int y = interior.y - radius; y <= interior.y + radius; y++)
            {
                for (int x = interior.x - radius; x <= interior.x + radius; x++)
                {
                    if (!RoomLayoutGenerator.IsInside(new Vector2Int(x, y), layout.Width, layout.Height))
                    {
                        return false;
                    }

                    if (x == 0 || y == 0 || x == layout.Width - 1 || y == layout.Height - 1)
                    {
                        continue;
                    }

                    if (layout.GetSurface(x, y) != CellData.CellSurfaceType.Ground)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsDoorOnBoundary(RoomResolvedLayout layout, RoomDoor door)
        {
            Vector2Int cell = door.Coordinates;
            return door.Direction switch
            {
                RoomDirection.North => cell.y == layout.Height - 1 && cell.x > 0 && cell.x < layout.Width - 1,
                RoomDirection.East => cell.x == layout.Width - 1 && cell.y > 0 && cell.y < layout.Height - 1,
                RoomDirection.South => cell.y == 0 && cell.x > 0 && cell.x < layout.Width - 1,
                RoomDirection.West => cell.x == 0 && cell.y > 0 && cell.y < layout.Height - 1,
                _ => false,
            };
        }

        private static bool IsGround(RoomResolvedLayout layout, Vector2Int cell)
        {
            return RoomLayoutGenerator.IsInside(cell, layout.Width, layout.Height) &&
                   layout.GetSurface(cell.x, cell.y) == CellData.CellSurfaceType.Ground;
        }

        private static int CountGroundInRadius(RoomResolvedLayout layout, Vector2Int center, int radius)
        {
            int count = 0;
            for (int y = center.y - radius; y <= center.y + radius; y++)
            {
                for (int x = center.x - radius; x <= center.x + radius; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (!RoomLayoutGenerator.IsInside(cell, layout.Width, layout.Height) ||
                        layout.GetSurface(x, y) != CellData.CellSurfaceType.Ground)
                    {
                        continue;
                    }

                    int dx = x - center.x;
                    int dy = y - center.y;
                    if ((dx * dx) + (dy * dy) <= radius * radius)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    public static class RoomLayoutRepair
    {
        public static bool TryRepair(
            RoomResolvedLayout source,
            RoomGenerationProfile profile,
            out RoomResolvedLayout repaired,
            out RoomValidationResult validation)
        {
            repaired = null;
            validation = RoomLayoutValidator.Validate(source, profile);
            if (validation.IsValid)
            {
                repaired = source;
                return true;
            }

            if (source == null ||
                source.Surfaces == null ||
                source.Surfaces.Count != source.Width * source.Height ||
                source.PlayerEntryCells == null ||
                source.PlayerEntryCells.Count == 0)
            {
                return false;
            }

            var surfaces = new List<CellData.CellSurfaceType>(source.Surfaces);
            var reserved = new bool[surfaces.Count];
            Vector2Int playerEntry = source.PlayerEntryCells[0];
            surfaces[RoomLayoutGenerator.GetIndex(playerEntry.x, playerEntry.y, source.Width)] = CellData.CellSurfaceType.Ground;

            for (int i = 0; i < source.Doors.Count; i++)
            {
                RoomDoor door = source.Doors[i];
                surfaces[RoomLayoutGenerator.GetIndex(door.Coordinates.x, door.Coordinates.y, source.Width)] = CellData.CellSurfaceType.Ground;
                RoomLayoutGenerator.CarvePath(surfaces, reserved, source.Width, source.Height, playerEntry, GetDoorInteriorCell(door, source.Width, source.Height), profile.MinPathWidth, true);
                RoomLayoutGenerator.CarvePath(surfaces, reserved, source.Width, source.Height, GetDoorInteriorCell(door, source.Width, source.Height), door.Coordinates, profile.MinPathWidth, true);
            }

            for (int i = 0; i < source.SpecialAnchors.Count; i++)
            {
                RoomSpecialAnchor anchor = source.SpecialAnchors[i];
                if (!RoomLayoutGenerator.IsInside(anchor.Coordinates, source.Width, source.Height))
                {
                    continue;
                }

                surfaces[RoomLayoutGenerator.GetIndex(anchor.Coordinates.x, anchor.Coordinates.y, source.Width)] = CellData.CellSurfaceType.Ground;
                RoomLayoutGenerator.CarvePath(surfaces, reserved, source.Width, source.Height, playerEntry, anchor.Coordinates, profile.MinPathWidth, true);
            }

            SealUnreachableGround(surfaces, source.Width, source.Height, playerEntry);
            RoomLayoutGenerator.SealBoundaryWallsExceptDoors(surfaces, source.Width, source.Height, source.Doors);
            List<Vector2Int> enemySpawns = FilterEnemySpawns(source.EnemySpawnCells, surfaces, source.Width, source.Height, playerEntry, profile);
            repaired = new RoomResolvedLayout(
                $"{source.TemplateId}:repaired",
                source.RoomKind,
                source.Width,
                source.Height,
                surfaces,
                source.PlayerEntryCells,
                enemySpawns,
                source.Doors,
                source.SpecialAnchors);
            validation = RoomLayoutValidator.Validate(repaired, profile);
            return validation.IsValid;
        }

        private static void SealUnreachableGround(
            List<CellData.CellSurfaceType> surfaces,
            int width,
            int height,
            Vector2Int playerEntry)
        {
            bool[] reachable = RoomLayoutGenerator.FloodFill(surfaces, width, height, playerEntry);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = RoomLayoutGenerator.GetIndex(x, y, width);
                    if (surfaces[index] == CellData.CellSurfaceType.Ground && !reachable[index])
                    {
                        surfaces[index] = CellData.CellSurfaceType.Wall;
                    }
                }
            }
        }

        private static List<Vector2Int> FilterEnemySpawns(
            IReadOnlyList<Vector2Int> source,
            IReadOnlyList<CellData.CellSurfaceType> surfaces,
            int width,
            int height,
            Vector2Int playerEntry,
            RoomGenerationProfile profile)
        {
            var result = new List<Vector2Int>();
            if (!profile.AllowsEnemySpawns || source == null)
            {
                return result;
            }

            bool[] reachable = RoomLayoutGenerator.FloodFill(surfaces, width, height, playerEntry);
            for (int i = 0; i < source.Count; i++)
            {
                Vector2Int spawn = source[i];
                if (!RoomLayoutGenerator.IsInside(spawn, width, height))
                {
                    continue;
                }

                int index = RoomLayoutGenerator.GetIndex(spawn.x, spawn.y, width);
                int distance = Mathf.Abs(spawn.x - playerEntry.x) + Mathf.Abs(spawn.y - playerEntry.y);
                if (surfaces[index] == CellData.CellSurfaceType.Ground &&
                    reachable[index] &&
                    distance >= profile.MinEnemySpawnDistance)
                {
                    result.Add(spawn);
                }
            }

            return result;
        }

        private static Vector2Int GetDoorInteriorCell(RoomDoor door, int width, int height)
        {
            return door.Direction switch
            {
                RoomDirection.North => new Vector2Int(door.Coordinates.x, height - 2),
                RoomDirection.East => new Vector2Int(width - 2, door.Coordinates.y),
                RoomDirection.South => new Vector2Int(door.Coordinates.x, 1),
                RoomDirection.West => new Vector2Int(1, door.Coordinates.y),
                _ => door.Coordinates,
            };
        }
    }
}
