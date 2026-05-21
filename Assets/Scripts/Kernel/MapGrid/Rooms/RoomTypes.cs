using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.MapGrid
{
    public enum RoomKind
    {
        Start,
        Combat,
        Reward,
        Boss,
    }

    public enum RoomDirection
    {
        North,
        East,
        South,
        West,
    }

    public readonly struct RoomDoor
    {
        public RoomDoor(RoomDirection direction, Vector2Int coordinates)
        {
            Direction = direction;
            Coordinates = coordinates;
        }

        public RoomDirection Direction { get; }
        public Vector2Int Coordinates { get; }
    }

    public sealed class RoomResolvedLayout
    {
        public RoomResolvedLayout(
            string templateId,
            RoomKind roomKind,
            int width,
            int height,
            IReadOnlyList<CellData.CellSurfaceType> surfaces,
            IReadOnlyList<Vector2Int> playerEntryCells,
            IReadOnlyList<Vector2Int> enemySpawnCells,
            IReadOnlyList<RoomDoor> doors)
        {
            TemplateId = templateId ?? string.Empty;
            RoomKind = roomKind;
            Width = Mathf.Max(0, width);
            Height = Mathf.Max(0, height);
            Surfaces = surfaces ?? Array.Empty<CellData.CellSurfaceType>();
            PlayerEntryCells = playerEntryCells ?? Array.Empty<Vector2Int>();
            EnemySpawnCells = enemySpawnCells ?? Array.Empty<Vector2Int>();
            Doors = doors ?? Array.Empty<RoomDoor>();
        }

        public string TemplateId { get; }
        public RoomKind RoomKind { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<CellData.CellSurfaceType> Surfaces { get; }
        public IReadOnlyList<Vector2Int> PlayerEntryCells { get; }
        public IReadOnlyList<Vector2Int> EnemySpawnCells { get; }
        public IReadOnlyList<RoomDoor> Doors { get; }

        public CellData.CellSurfaceType GetSurface(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return CellData.CellSurfaceType.Wall;
            }

            int index = (y * Width) + x;
            return index >= 0 && index < Surfaces.Count
                ? Surfaces[index]
                : CellData.CellSurfaceType.Wall;
        }
    }

    public sealed class RoomGraph
    {
        private readonly Dictionary<string, RoomGraphNode> nodesById;

        public RoomGraph(int seed, IReadOnlyList<RoomGraphNode> nodes, string currentRoomId)
        {
            Seed = seed;
            Nodes = nodes ?? Array.Empty<RoomGraphNode>();
            CurrentRoomId = currentRoomId ?? string.Empty;
            nodesById = new Dictionary<string, RoomGraphNode>(StringComparer.Ordinal);
            for (int i = 0; i < Nodes.Count; i++)
            {
                RoomGraphNode node = Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.RoomId))
                {
                    continue;
                }

                nodesById[node.RoomId] = node;
            }
        }

        public int Seed { get; }
        public IReadOnlyList<RoomGraphNode> Nodes { get; }
        public string CurrentRoomId { get; private set; }

        public bool TryGetNode(string roomId, out RoomGraphNode node)
        {
            node = null;
            return !string.IsNullOrWhiteSpace(roomId) && nodesById.TryGetValue(roomId, out node);
        }

        public bool TryGetCurrentNode(out RoomGraphNode node)
        {
            return TryGetNode(CurrentRoomId, out node);
        }

        public bool TrySetCurrentRoom(string roomId)
        {
            if (!TryGetNode(roomId, out _))
            {
                return false;
            }

            CurrentRoomId = roomId;
            return true;
        }
    }

    public sealed class RoomGraphNode
    {
        private readonly Dictionary<RoomDirection, string> connections;
        private readonly RoomDirection[] requiredDoorDirections;

        public RoomGraphNode(
            string roomId,
            RoomKind kind,
            Vector2Int graphCoordinate,
            RoomTemplateData template,
            IReadOnlyDictionary<RoomDirection, string> connections)
        {
            RoomId = roomId ?? string.Empty;
            Kind = kind;
            GraphCoordinate = graphCoordinate;
            Template = template;
            this.connections = connections != null
                ? new Dictionary<RoomDirection, string>(connections)
                : new Dictionary<RoomDirection, string>();
            requiredDoorDirections = new RoomDirection[this.connections.Count];
            this.connections.Keys.CopyTo(requiredDoorDirections, 0);
            Array.Sort(requiredDoorDirections);
        }

        public string RoomId { get; }
        public RoomKind Kind { get; }
        public Vector2Int GraphCoordinate { get; }
        public RoomTemplateData Template { get; }
        public IReadOnlyDictionary<RoomDirection, string> Connections => connections;
        public IReadOnlyList<RoomDirection> RequiredDoorDirections => requiredDoorDirections;

        public bool TryGetConnectedRoomId(RoomDirection direction, out string roomId)
        {
            return connections.TryGetValue(direction, out roomId) && !string.IsNullOrWhiteSpace(roomId);
        }

        public bool TryResolveLayout(out RoomResolvedLayout layout, out string error)
        {
            layout = null;
            error = null;
            if (Template == null)
            {
                error = $"Room '{RoomId}' is missing a template.";
                return false;
            }

            return Template.TryResolveLayout(requiredDoorDirections, out layout, out error);
        }
    }

    public static class RoomDirectionUtility
    {
        public static RoomDirection GetOpposite(RoomDirection direction)
        {
            switch (direction)
            {
                case RoomDirection.North:
                    return RoomDirection.South;
                case RoomDirection.East:
                    return RoomDirection.West;
                case RoomDirection.South:
                    return RoomDirection.North;
                case RoomDirection.West:
                    return RoomDirection.East;
                default:
                    return RoomDirection.North;
            }
        }

        public static Vector2Int ToGraphOffset(RoomDirection direction)
        {
            switch (direction)
            {
                case RoomDirection.North:
                    return Vector2Int.up;
                case RoomDirection.East:
                    return Vector2Int.right;
                case RoomDirection.South:
                    return Vector2Int.down;
                case RoomDirection.West:
                    return Vector2Int.left;
                default:
                    return Vector2Int.zero;
            }
        }
    }
}
