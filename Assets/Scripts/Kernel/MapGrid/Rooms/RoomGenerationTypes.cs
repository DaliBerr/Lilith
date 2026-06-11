using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.MapGrid
{
    public readonly struct RoomGenerationInput
    {
        public RoomGenerationInput(
            int seed,
            RoomKind roomKind,
            IReadOnlyCollection<RoomDirection> requiredDoorDirections,
            int difficultyTier)
        {
            Seed = seed;
            RoomKind = roomKind;
            RequiredDoorDirections = SanitizeDirections(requiredDoorDirections);
            DifficultyTier = Mathf.Max(0, difficultyTier);
        }

        public int Seed { get; }
        public RoomKind RoomKind { get; }
        public IReadOnlyList<RoomDirection> RequiredDoorDirections { get; }
        public int DifficultyTier { get; }

        public int GetAttemptSeed(int retryIndex)
        {
            unchecked
            {
                int hash = Seed;
                hash = (hash * 397) ^ (int)RoomKind;
                hash = (hash * 397) ^ DifficultyTier;
                hash = (hash * 397) ^ retryIndex;
                for (int i = 0; i < RequiredDoorDirections.Count; i++)
                {
                    hash = (hash * 397) ^ (int)RequiredDoorDirections[i];
                }

                return hash;
            }
        }

        private static IReadOnlyList<RoomDirection> SanitizeDirections(IReadOnlyCollection<RoomDirection> directions)
        {
            if (directions == null || directions.Count == 0)
            {
                return Array.Empty<RoomDirection>();
            }

            var uniqueDirections = new HashSet<RoomDirection>(directions);
            var result = new RoomDirection[uniqueDirections.Count];
            uniqueDirections.CopyTo(result);
            Array.Sort(result);
            return result;
        }
    }

    public sealed class RoomGenerationProfile
    {
        public RoomGenerationProfile(
            RoomKind roomKind,
            Vector2Int widthRange,
            Vector2Int heightRange,
            int minimumWidth,
            int minimumHeight,
            int minPathWidth,
            int playerSafeRadius,
            int doorBufferRadius,
            Vector2Int obstacleCountRange,
            Vector2Int obstacleWidthRange,
            Vector2Int obstacleHeightRange,
            float maxObstacleDensity,
            Vector2Int enemySpawnCountRange,
            int minEnemySpawnDistance,
            int minCombatGroundCells,
            int bossOpenRadius,
            int maxPlacementAttempts,
            int maxGenerationRetries)
        {
            RoomKind = roomKind;
            WidthRange = SanitizeRange(widthRange, 1);
            HeightRange = SanitizeRange(heightRange, 1);
            MinimumWidth = Mathf.Max(1, minimumWidth);
            MinimumHeight = Mathf.Max(1, minimumHeight);
            MinPathWidth = Mathf.Max(1, minPathWidth);
            PlayerSafeRadius = Mathf.Max(0, playerSafeRadius);
            DoorBufferRadius = Mathf.Max(0, doorBufferRadius);
            ObstacleCountRange = SanitizeRange(obstacleCountRange, 0);
            ObstacleWidthRange = SanitizeRange(obstacleWidthRange, 1);
            ObstacleHeightRange = SanitizeRange(obstacleHeightRange, 1);
            MaxObstacleDensity = Mathf.Clamp01(maxObstacleDensity);
            EnemySpawnCountRange = SanitizeRange(enemySpawnCountRange, 0);
            MinEnemySpawnDistance = Mathf.Max(0, minEnemySpawnDistance);
            MinCombatGroundCells = Mathf.Max(0, minCombatGroundCells);
            BossOpenRadius = Mathf.Max(0, bossOpenRadius);
            MaxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
            MaxGenerationRetries = Mathf.Max(1, maxGenerationRetries);
        }

        public RoomKind RoomKind { get; }
        public Vector2Int WidthRange { get; }
        public Vector2Int HeightRange { get; }
        public int MinimumWidth { get; }
        public int MinimumHeight { get; }
        public int MinPathWidth { get; }
        public int PlayerSafeRadius { get; }
        public int DoorBufferRadius { get; }
        public Vector2Int ObstacleCountRange { get; }
        public Vector2Int ObstacleWidthRange { get; }
        public Vector2Int ObstacleHeightRange { get; }
        public float MaxObstacleDensity { get; }
        public Vector2Int EnemySpawnCountRange { get; }
        public int MinEnemySpawnDistance { get; }
        public int MinCombatGroundCells { get; }
        public int BossOpenRadius { get; }
        public int MaxPlacementAttempts { get; }
        public int MaxGenerationRetries { get; }

        public bool RequestedRangeCanFit => WidthRange.y >= MinimumWidth && HeightRange.y >= MinimumHeight;
        public bool RequiresRewardAnchor => RoomKind == RoomKind.Reward;
        public bool RequiresBossAnchor => RoomKind == RoomKind.Boss;
        public bool AllowsEnemySpawns => RoomKind == RoomKind.Combat || RoomKind == RoomKind.Boss;

        private static Vector2Int SanitizeRange(Vector2Int range, int minimum)
        {
            int min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
            int max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2Int(min, max);
        }
    }

    public enum RoomValidationIssueType
    {
        InvalidDimensions,
        SurfaceCountMismatch,
        MissingPlayerEntry,
        PlayerEntryNotGround,
        DoorInvalid,
        DoorUnreachable,
        AnchorMissing,
        AnchorUnreachable,
        IsolatedGround,
        DoorBufferTooNarrow,
        CombatAreaTooSmall,
        ObstacleDensityOutOfRange,
        EnemySpawnInvalid,
    }

    public readonly struct RoomValidationIssue
    {
        public RoomValidationIssue(RoomValidationIssueType type, string message, Vector2Int coordinates = default)
        {
            Type = type;
            Message = message ?? string.Empty;
            Coordinates = coordinates;
        }

        public RoomValidationIssueType Type { get; }
        public string Message { get; }
        public Vector2Int Coordinates { get; }
    }

    public sealed class RoomValidationResult
    {
        public RoomValidationResult(IReadOnlyList<RoomValidationIssue> issues)
        {
            Issues = issues ?? Array.Empty<RoomValidationIssue>();
        }

        public IReadOnlyList<RoomValidationIssue> Issues { get; }
        public bool IsValid => Issues.Count == 0;
        public string FirstError => IsValid ? string.Empty : Issues[0].Message;
    }
}
