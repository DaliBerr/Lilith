using System.Collections.Generic;
using UnityEngine;
using Vocalith.Logging;

namespace Kernel.MapGrid
{
    [DisallowMultipleComponent]
    public sealed class ProceduralRoomMapDebugController : MonoBehaviour
    {
        public static readonly Vector3 DefaultWorldPosition = new(8000f, 0f, 0f);

        [SerializeField] private int seed = 12345;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private List<RoomTemplateData> roomTemplates = new();
        [SerializeField] private TilemapRoomPresenter roomPresenter;

        private readonly RoomGraphGenerator graphGenerator = new();
        private RoomGraph currentGraph;

        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        public RoomGraph CurrentGraph => currentGraph;

        private void OnValidate()
        {
            roomTemplates ??= new List<RoomTemplateData>();
        }

        private void Start()
        {
            if (!generateOnStart)
            {
                return;
            }

            if (!GenerateRun(out string error))
            {
                GameDebug.LogError($"[ProceduralRoomMapDebugController] {error}");
            }
        }

        public bool GenerateRun(out string error)
        {
            error = null;
            if (!TryResolvePresenter(out error))
            {
                return false;
            }

            if (!graphGenerator.TryGenerateDefaultRun(seed, roomTemplates, out currentGraph, out string warning, out error))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(warning))
            {
                GameDebug.LogWarning($"[ProceduralRoomMapDebugController] {warning}");
            }

            return RenderCurrentRoom(out error);
        }

        public bool RenderRoom(string roomId, out string error)
        {
            error = null;
            if (currentGraph == null)
            {
                error = "Generate a room graph before rendering a room.";
                return false;
            }

            if (!currentGraph.TrySetCurrentRoom(roomId) ||
                !currentGraph.TryGetCurrentNode(out RoomGraphNode node))
            {
                error = $"Room '{roomId}' does not exist in the current room graph.";
                return false;
            }

            return RenderNode(node, out error);
        }

        public bool RenderCurrentRoom(out string error)
        {
            error = null;
            if (currentGraph == null || !currentGraph.TryGetCurrentNode(out RoomGraphNode node))
            {
                error = "Current room graph is missing or has no current room.";
                return false;
            }

            return RenderNode(node, out error);
        }

        public bool RenderFirstConnectedRoom(out string error)
        {
            error = null;
            if (currentGraph == null || !currentGraph.TryGetCurrentNode(out RoomGraphNode currentNode))
            {
                error = "Generate a room graph before rendering connected rooms.";
                return false;
            }

            foreach (RoomDirection direction in currentNode.RequiredDoorDirections)
            {
                if (currentNode.TryGetConnectedRoomId(direction, out string connectedRoomId))
                {
                    return RenderRoom(connectedRoomId, out error);
                }
            }

            error = $"Room '{currentNode.RoomId}' has no connected rooms.";
            return false;
        }

        [ContextMenu("Generate Debug Run")]
        private void GenerateDebugRunContext()
        {
            if (!GenerateRun(out string error))
            {
                GameDebug.LogError($"[ProceduralRoomMapDebugController] {error}");
            }
        }

        [ContextMenu("Render Current Room")]
        private void RenderCurrentRoomContext()
        {
            if (!RenderCurrentRoom(out string error))
            {
                GameDebug.LogError($"[ProceduralRoomMapDebugController] {error}");
            }
        }

        [ContextMenu("Render First Connected Room")]
        private void RenderFirstConnectedRoomContext()
        {
            if (!RenderFirstConnectedRoom(out string error))
            {
                GameDebug.LogError($"[ProceduralRoomMapDebugController] {error}");
            }
        }

        private bool RenderNode(RoomGraphNode node, out string error)
        {
            error = null;
            if (!TryResolvePresenter(out error))
            {
                return false;
            }

            if (!node.TryResolveLayout(out RoomResolvedLayout layout, out error))
            {
                return false;
            }

            return roomPresenter.TryApply(layout, out error);
        }

        private bool TryResolvePresenter(out string error)
        {
            error = null;
            if (roomPresenter == null)
            {
                roomPresenter = GetComponentInChildren<TilemapRoomPresenter>(true);
            }

            if (roomPresenter != null)
            {
                return true;
            }

            error = "ProceduralRoomMapDebugController requires a TilemapRoomPresenter.";
            return false;
        }
    }
}
