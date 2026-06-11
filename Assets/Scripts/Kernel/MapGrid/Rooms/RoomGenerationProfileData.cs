using UnityEngine;

namespace Kernel.MapGrid
{
    [CreateAssetMenu(fileName = "RoomGenerationProfile", menuName = "Lilith/Map/Room Generation Profile")]
    public sealed class RoomGenerationProfileData : ScriptableObject
    {
        [SerializeField] private string id = "RoomGenerationProfile";
        [SerializeField] private RoomKind roomKind = RoomKind.Combat;
        [SerializeField] private Vector2Int widthRange = new(15, 19);
        [SerializeField] private Vector2Int heightRange = new(13, 17);
        [SerializeField, Min(1)] private int minimumWidth = 13;
        [SerializeField, Min(1)] private int minimumHeight = 11;
        [SerializeField, Min(1)] private int minPathWidth = 2;
        [SerializeField, Min(0)] private int playerSafeRadius = 3;
        [SerializeField, Min(0)] private int doorBufferRadius = 2;
        [SerializeField] private Vector2Int obstacleCountRange = new(4, 7);
        [SerializeField] private Vector2Int obstacleWidthRange = new(1, 3);
        [SerializeField] private Vector2Int obstacleHeightRange = new(1, 2);
        [SerializeField, Range(0f, 1f)] private float maxObstacleDensity = 0.24f;
        [SerializeField] private Vector2Int enemySpawnCountRange = new(3, 5);
        [SerializeField, Min(0)] private int minEnemySpawnDistance = 5;
        [SerializeField, Min(0)] private int minCombatGroundCells = 56;
        [SerializeField, Min(0)] private int bossOpenRadius;
        [SerializeField, Min(1)] private int maxPlacementAttempts = 32;
        [SerializeField, Min(1)] private int maxGenerationRetries = 4;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
        public RoomKind RoomKind => roomKind;

        public RoomGenerationProfile BuildRuntimeProfile(RoomGenerationInput input)
        {
            return new RoomGenerationProfile(
                input.RoomKind,
                widthRange,
                heightRange,
                minimumWidth,
                minimumHeight,
                minPathWidth,
                playerSafeRadius,
                doorBufferRadius,
                obstacleCountRange,
                obstacleWidthRange,
                obstacleHeightRange,
                maxObstacleDensity,
                enemySpawnCountRange,
                minEnemySpawnDistance,
                minCombatGroundCells,
                bossOpenRadius,
                maxPlacementAttempts,
                maxGenerationRetries);
        }

        private void OnValidate()
        {
            widthRange = SanitizeRange(widthRange, 1);
            heightRange = SanitizeRange(heightRange, 1);
            minimumWidth = Mathf.Max(1, minimumWidth);
            minimumHeight = Mathf.Max(1, minimumHeight);
            minPathWidth = Mathf.Max(1, minPathWidth);
            playerSafeRadius = Mathf.Max(0, playerSafeRadius);
            doorBufferRadius = Mathf.Max(0, doorBufferRadius);
            obstacleCountRange = SanitizeRange(obstacleCountRange, 0);
            obstacleWidthRange = SanitizeRange(obstacleWidthRange, 1);
            obstacleHeightRange = SanitizeRange(obstacleHeightRange, 1);
            maxObstacleDensity = Mathf.Clamp01(maxObstacleDensity);
            enemySpawnCountRange = SanitizeRange(enemySpawnCountRange, 0);
            minEnemySpawnDistance = Mathf.Max(0, minEnemySpawnDistance);
            minCombatGroundCells = Mathf.Max(0, minCombatGroundCells);
            bossOpenRadius = Mathf.Max(0, bossOpenRadius);
            maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
            maxGenerationRetries = Mathf.Max(1, maxGenerationRetries);
        }

        private static Vector2Int SanitizeRange(Vector2Int range, int minimum)
        {
            int min = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
            int max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return new Vector2Int(min, max);
        }
    }
}
