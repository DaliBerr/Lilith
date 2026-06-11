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
        [SerializeField, Min(0)] private int difficultyTier = 1;
        [SerializeField] private RoomGenerationProfileLibrary profileLibrary;
        [SerializeField] private TilemapRoomPresenter roomPresenter;

        private readonly RoomGraphGenerator graphGenerator = new();
        private readonly RoomLayoutGenerator layoutGenerator = new();
        private RoomGraph currentGraph;

        public int Seed
        {
            get => seed;
            set => seed = value;
        }

        public RoomGraph CurrentGraph => currentGraph;

        private void OnValidate()
        {
            difficultyTier = Mathf.Max(0, difficultyTier);
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

            if (!graphGenerator.TryGenerateDefaultRun(seed, out currentGraph, out error))
            {
                return false;
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

        public bool TryGetCurrentLayout(out RoomResolvedLayout layout, out string error)
        {
            layout = null;
            error = null;
            if (currentGraph == null || !currentGraph.TryGetCurrentNode(out RoomGraphNode node))
            {
                error = "Current room graph is missing or has no current room.";
                return false;
            }

            return TryGenerateLayout(node, out layout, out error);
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

            if (!TryGenerateLayout(node, out RoomResolvedLayout layout, out error))
            {
                return false;
            }

            return roomPresenter.TryApply(layout, out error);
        }

        private bool TryGenerateLayout(RoomGraphNode node, out RoomResolvedLayout layout, out string error)
        {
            layout = null;
            error = null;
            if (node == null)
            {
                error = "Room graph node is missing.";
                return false;
            }

            var input = new RoomGenerationInput(
                CreateRoomSeed(seed, node.RoomId),
                node.Kind,
                node.RequiredDoorDirections,
                difficultyTier);

            if (profileLibrary == null)
            {
                error = "ProceduralRoomMapDebugController requires a RoomGenerationProfileLibrary.";
                return false;
            }

            if (!profileLibrary.TrySelectProfile(input, out RoomGenerationProfileData profileData, out error))
            {
                return false;
            }

            RoomGenerationProfile profile = profileData.BuildRuntimeProfile(input);
            return layoutGenerator.TryGenerate(input, profile, out layout, out error);
        }

        private static int CreateRoomSeed(int runSeed, string roomId)
        {
            unchecked
            {
                int hash = runSeed;
                string id = roomId ?? string.Empty;
                for (int i = 0; i < id.Length; i++)
                {
                    hash = (hash * 397) ^ id[i];
                }

                return hash;
            }
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
