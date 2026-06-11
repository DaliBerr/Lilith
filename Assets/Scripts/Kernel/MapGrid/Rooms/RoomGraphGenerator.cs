using System.Collections.Generic;
using UnityEngine;
using VocalithRandom = Vocalith.Random;

namespace Kernel.MapGrid
{
    public sealed class RoomGraphGenerator
    {
        private static readonly RoomKind[] MainPathKinds =
        {
            RoomKind.Start,
            RoomKind.Combat,
            RoomKind.Combat,
            RoomKind.Combat,
            RoomKind.Combat,
            RoomKind.Boss,
        };

        public bool TryGenerateDefaultRun(
            int seed,
            out RoomGraph graph,
            out string error)
        {
            graph = null;
            error = null;

            var random = new VocalithRandom(seed);
            List<NodeDraft> drafts = BuildDefaultDrafts(random);
            var nodes = new List<RoomGraphNode>(drafts.Count);
            for (int i = 0; i < drafts.Count; i++)
            {
                NodeDraft draft = drafts[i];
                nodes.Add(new RoomGraphNode(
                    draft.RoomId,
                    draft.Kind,
                    draft.GraphCoordinate,
                    draft.Connections));
            }

            graph = new RoomGraph(seed, nodes, drafts[0].RoomId);
            return true;
        }

        private static List<NodeDraft> BuildDefaultDrafts(VocalithRandom random)
        {
            var drafts = new List<NodeDraft>(MainPathKinds.Length + 1);
            for (int i = 0; i < MainPathKinds.Length; i++)
            {
                string roomId = MainPathKinds[i] == RoomKind.Start
                    ? "start_0"
                    : MainPathKinds[i] == RoomKind.Boss
                        ? "boss_5"
                        : $"combat_{i}";
                drafts.Add(new NodeDraft(roomId, MainPathKinds[i], new Vector2Int(i, 0)));
            }

            for (int i = 0; i < MainPathKinds.Length - 1; i++)
            {
                Connect(drafts[i], drafts[i + 1], RoomDirection.East);
            }

            int rewardParentIndex = random.Next(0, 2) == 0 ? 2 : 3;
            RoomDirection rewardDirection = random.Next(0, 2) == 0 ? RoomDirection.North : RoomDirection.South;
            Vector2Int rewardCoordinate = drafts[rewardParentIndex].GraphCoordinate + RoomDirectionUtility.ToGraphOffset(rewardDirection);
            var reward = new NodeDraft("reward_0", RoomKind.Reward, rewardCoordinate);
            Connect(drafts[rewardParentIndex], reward, rewardDirection);
            drafts.Add(reward);
            return drafts;
        }

        private static void Connect(NodeDraft from, NodeDraft to, RoomDirection directionFromTo)
        {
            from.Connections[directionFromTo] = to.RoomId;
            to.Connections[RoomDirectionUtility.GetOpposite(directionFromTo)] = from.RoomId;
        }

        private sealed class NodeDraft
        {
            public NodeDraft(string roomId, RoomKind kind, Vector2Int graphCoordinate)
            {
                RoomId = roomId;
                Kind = kind;
                GraphCoordinate = graphCoordinate;
            }

            public string RoomId { get; }
            public RoomKind Kind { get; }
            public Vector2Int GraphCoordinate { get; }
            public Dictionary<RoomDirection, string> Connections { get; } = new();
        }
    }
}
