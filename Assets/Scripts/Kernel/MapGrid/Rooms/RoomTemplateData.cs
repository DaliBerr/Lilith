using System.Collections.Generic;
using UnityEngine;

namespace Kernel.MapGrid
{
    [CreateAssetMenu(menuName = "Lilith/Map/Room Template", fileName = "RoomTemplate")]
    public sealed class RoomTemplateData : ScriptableObject
    {
        private const char WallSymbol = '#';
        private const char GroundSymbol = '.';
        private const char PlayerEntrySymbol = 'P';
        private const char EnemySpawnSymbol = 'S';
        private const char DoorSymbol = 'D';

        [SerializeField] private string id = string.Empty;
        [SerializeField] private RoomKind roomKind = RoomKind.Combat;
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int height = 1;
        [SerializeField] private List<string> layoutRows = new();

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
        public RoomKind RoomKind => roomKind;
        public int Width => width;
        public int Height => height;
        public IReadOnlyList<string> LayoutRows => layoutRows;

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            layoutRows ??= new List<string>();
        }

        public bool SupportsDoorDirections(IReadOnlyCollection<RoomDirection> requiredDirections)
        {
            if (requiredDirections == null || requiredDirections.Count == 0)
            {
                return true;
            }

            if (!TryCollectDoorDirections(out HashSet<RoomDirection> availableDirections, out _))
            {
                return false;
            }

            foreach (RoomDirection direction in requiredDirections)
            {
                if (!availableDirections.Contains(direction))
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasAllDoorDirections()
        {
            return TryCollectDoorDirections(out HashSet<RoomDirection> availableDirections, out _) &&
                   availableDirections.Contains(RoomDirection.North) &&
                   availableDirections.Contains(RoomDirection.East) &&
                   availableDirections.Contains(RoomDirection.South) &&
                   availableDirections.Contains(RoomDirection.West);
        }

        public bool TryResolveLayout(
            IReadOnlyCollection<RoomDirection> requiredDoorDirections,
            out RoomResolvedLayout layout,
            out string error)
        {
            layout = null;
            error = null;

            if (!TryValidateRows(out error))
            {
                return false;
            }

            var surfaces = new List<CellData.CellSurfaceType>(width * height);
            var playerEntryCells = new List<Vector2Int>();
            var enemySpawnCells = new List<Vector2Int>();
            var doors = new List<RoomDoor>();
            var doorDirections = new HashSet<RoomDirection>();
            for (int index = 0; index < width * height; index++)
            {
                surfaces.Add(CellData.CellSurfaceType.Ground);
            }

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                string row = layoutRows[rowIndex] ?? string.Empty;
                int y = height - 1 - rowIndex;
                for (int x = 0; x < width; x++)
                {
                    char symbol = row[x];
                    int layoutIndex = (y * width) + x;
                    switch (symbol)
                    {
                        case WallSymbol:
                            surfaces[layoutIndex] = CellData.CellSurfaceType.Wall;
                            break;
                        case GroundSymbol:
                            surfaces[layoutIndex] = CellData.CellSurfaceType.Ground;
                            break;
                        case PlayerEntrySymbol:
                            surfaces[layoutIndex] = CellData.CellSurfaceType.Ground;
                            playerEntryCells.Add(new Vector2Int(x, y));
                            break;
                        case EnemySpawnSymbol:
                            surfaces[layoutIndex] = CellData.CellSurfaceType.Ground;
                            enemySpawnCells.Add(new Vector2Int(x, y));
                            break;
                        case DoorSymbol:
                            if (!TryResolveDoorDirection(x, y, out RoomDirection direction))
                            {
                                error = $"Door in template '{Id}' must be placed on the room boundary.";
                                return false;
                            }

                            surfaces[layoutIndex] = CellData.CellSurfaceType.Ground;
                            var door = new RoomDoor(direction, new Vector2Int(x, y));
                            doors.Add(door);
                            doorDirections.Add(direction);
                            break;
                        default:
                            error = $"Unsupported room template symbol '{symbol}' in template '{Id}'.";
                            return false;
                    }
                }
            }

            if (playerEntryCells.Count == 0)
            {
                error = $"Room template '{Id}' must contain at least one player entry symbol '{PlayerEntrySymbol}'.";
                return false;
            }

            if (!ContainsRequiredDoorDirections(doorDirections, requiredDoorDirections, out error))
            {
                error = $"Room template '{Id}' {error}";
                return false;
            }

            layout = new RoomResolvedLayout(Id, roomKind, width, height, surfaces, playerEntryCells, enemySpawnCells, doors);
            return true;
        }

        private bool TryCollectDoorDirections(out HashSet<RoomDirection> doorDirections, out string error)
        {
            doorDirections = new HashSet<RoomDirection>();
            error = null;

            if (!TryValidateRows(out error))
            {
                return false;
            }

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                string row = layoutRows[rowIndex] ?? string.Empty;
                int y = height - 1 - rowIndex;
                for (int x = 0; x < width; x++)
                {
                    if (row[x] != DoorSymbol)
                    {
                        continue;
                    }

                    if (!TryResolveDoorDirection(x, y, out RoomDirection direction))
                    {
                        error = $"Door in template '{Id}' must be placed on the room boundary.";
                        return false;
                    }

                    doorDirections.Add(direction);
                }
            }

            return true;
        }

        private bool TryValidateRows(out string error)
        {
            error = null;
            if (width <= 0 || height <= 0)
            {
                error = $"Room template '{Id}' must have positive width and height.";
                return false;
            }

            if (layoutRows == null || layoutRows.Count != height)
            {
                error = $"Room template '{Id}' has {layoutRows?.Count ?? 0} rows, but height is {height}.";
                return false;
            }

            for (int rowIndex = 0; rowIndex < layoutRows.Count; rowIndex++)
            {
                string row = layoutRows[rowIndex] ?? string.Empty;
                if (row.Length != width)
                {
                    error = $"Room template '{Id}' row {rowIndex} has length {row.Length}, but width is {width}.";
                    return false;
                }
            }

            return true;
        }

        private bool TryResolveDoorDirection(int x, int y, out RoomDirection direction)
        {
            if (y == height - 1)
            {
                direction = RoomDirection.North;
                return true;
            }

            if (x == width - 1)
            {
                direction = RoomDirection.East;
                return true;
            }

            if (y == 0)
            {
                direction = RoomDirection.South;
                return true;
            }

            if (x == 0)
            {
                direction = RoomDirection.West;
                return true;
            }

            direction = default;
            return false;
        }

        private static bool ContainsRequiredDoorDirections(
            IReadOnlyCollection<RoomDirection> availableDirections,
            IReadOnlyCollection<RoomDirection> requiredDirections,
            out string error)
        {
            error = null;
            if (requiredDirections == null || requiredDirections.Count == 0)
            {
                return true;
            }

            foreach (RoomDirection requiredDirection in requiredDirections)
            {
                if (ContainsDirection(availableDirections, requiredDirection))
                {
                    continue;
                }

                error = $"does not contain a required {requiredDirection} door.";
                return false;
            }

            return true;
        }

        private static bool ContainsDirection(IReadOnlyCollection<RoomDirection> directions, RoomDirection target)
        {
            if (directions == null)
            {
                return false;
            }

            foreach (RoomDirection direction in directions)
            {
                if (direction == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
