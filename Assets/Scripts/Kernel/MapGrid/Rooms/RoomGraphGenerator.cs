using System;
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
            IReadOnlyList<RoomTemplateData> templates,
            out RoomGraph graph,
            out string warning,
            out string error)
        {
            graph = null;
            warning = null;
            error = null;

            if (templates == null || templates.Count == 0)
            {
                error = "Room graph generation requires at least one room template.";
                return false;
            }

            var random = new VocalithRandom(seed);
            List<NodeDraft> drafts = BuildDefaultDrafts(random);
            var warningLines = new List<string>();
            var nodes = new List<RoomGraphNode>(drafts.Count);
            for (int i = 0; i < drafts.Count; i++)
            {
                NodeDraft draft = drafts[i];
                if (!TrySelectTemplate(
                        draft.Kind,
                        draft.Connections.Keys,
                        templates,
                        random,
                        out RoomTemplateData selectedTemplate,
                        out bool usedFallback,
                        out error))
                {
                    return false;
                }

                if (usedFallback)
                {
                    warningLines.Add($"Room '{draft.RoomId}' used fallback template '{selectedTemplate.Id}'.");
                }

                nodes.Add(new RoomGraphNode(
                    draft.RoomId,
                    draft.Kind,
                    draft.GraphCoordinate,
                    selectedTemplate,
                    draft.Connections));
            }

            warning = warningLines.Count > 0 ? string.Join(Environment.NewLine, warningLines) : null;
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

        private static bool TrySelectTemplate(
            RoomKind roomKind,
            IReadOnlyCollection<RoomDirection> requiredDirections,
            IReadOnlyList<RoomTemplateData> templates,
            VocalithRandom random,
            out RoomTemplateData template,
            out bool usedFallback,
            out string error)
        {
            template = null;
            usedFallback = false;
            error = null;

            List<RoomTemplateData> kindMatches = CollectTemplates(
                templates,
                candidate => candidate.RoomKind == roomKind && candidate.SupportsDoorDirections(requiredDirections));
            if (kindMatches.Count > 0)
            {
                template = kindMatches[random.Next(0, kindMatches.Count)];
                return true;
            }

            List<RoomTemplateData> fallbackMatches = CollectTemplates(
                templates,
                candidate => candidate.HasAllDoorDirections() && candidate.SupportsDoorDirections(requiredDirections));
            if (fallbackMatches.Count > 0)
            {
                template = fallbackMatches[0];
                usedFallback = true;
                return true;
            }

            error = $"No room template supports kind '{roomKind}' with the required doors.";
            return false;
        }

        private static List<RoomTemplateData> CollectTemplates(
            IReadOnlyList<RoomTemplateData> templates,
            Predicate<RoomTemplateData> predicate)
        {
            var matches = new List<RoomTemplateData>();
            for (int i = 0; i < templates.Count; i++)
            {
                RoomTemplateData template = templates[i];
                if (template == null || !predicate(template))
                {
                    continue;
                }

                matches.Add(template);
            }

            matches.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
            return matches;
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
