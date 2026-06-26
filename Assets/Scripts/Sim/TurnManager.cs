using System.Collections;
using System.Collections.Generic;
using AISandbox.Agents;
using AISandbox.Brains;
using AISandbox.Memory;
using AISandbox.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AISandbox.Sim
{
    /// <summary>
    /// Turn-based driver. Agents act strictly one at a time: the manager builds an
    /// agent's perception, fully waits for its brain to decide (a slow LLM call is
    /// fine — nothing else runs meanwhile), then waits for the resulting action to
    /// finish (including the walk animation) before the next agent takes its turn.
    ///
    /// Continuous mode loops rounds back-to-back, paced by how long each turn
    /// actually takes. Manual mode advances one full round per Space press.
    /// Attach to an empty GameObject (e.g. "Sim").
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public enum RunMode { Continuous, Manual }

        [Tooltip("Continuous = rounds run automatically. Manual = press Space for one round.")]
        public RunMode mode = RunMode.Continuous;

        [Tooltip("Pause inserted after each agent's turn, so it's watchable.")]
        [Min(0f)] public float pauseBetweenTurns = 0.3f;

        [Tooltip("Pause between individual actions within one agent's turn.")]
        [Min(0f)] public float pauseBetweenActions = 0.2f;

        [Tooltip("Agent walking speed, in tiles per second.")]
        [Min(0.1f)] public float moveSpeed = 4f;

        [Tooltip("Log each agent's action to the Console.")]
        public bool verboseLog = true;

        [Tooltip("How many recent turns of its own memory each agent sees when deciding.")]
        [Min(0)] public int recentMemoryWindow = 8;

        [SerializeField] private WorldGrid grid;

        private bool _busy;        // a round is currently running
        private bool _loopRunning; // the continuous loop coroutine is active
        private int _round;

        /// <summary>True while a round is currently running; false between rounds.</summary>
        public bool IsBusy => _busy;

        // Messages each agent will hear on its next turn.
        private readonly Dictionary<Agent, List<HeardMessage>> _inbox =
            new Dictionary<Agent, List<HeardMessage>>();

        private void Start()
        {
            if (grid == null) grid = FindFirstObjectByType<WorldGrid>();
            if (grid == null) Debug.LogError("TurnManager: no WorldGrid found in the scene.");
        }

        private void Update()
        {
            if (grid == null) return;

            if (mode == RunMode.Continuous)
            {
                if (!_loopRunning) StartCoroutine(ContinuousLoop());
            }
            else // Manual
            {
                var kb = Keyboard.current;
                if (!_busy && !IsLlmBlocked() && kb != null && kb.spaceKey.wasPressedThisFrame)
                    StartCoroutine(StepRound());
            }
        }

        /// <summary>
        /// True when the user has the HUD's "Use LLM" toggle on but the LLM is
        /// currently errored / unreachable. In that state we don't run rounds —
        /// the agents make no decisions and don't move, per spec.
        /// </summary>
        private static bool IsLlmBlocked()
        {
            return BrainSelector.UseLlm && LlmStatus.Current == LlmStatus.State.Error;
        }

        private IEnumerator ContinuousLoop()
        {
            _loopRunning = true;
            while (mode == RunMode.Continuous)
            {
                if (IsLlmBlocked())
                {
                    yield return null; // wait until either the LLM recovers or the toggle is flipped off
                    continue;
                }
                yield return StepRound();
                yield return null; // guarantee at least one frame per round
            }
            _loopRunning = false;
        }

        /// <summary>Runs one full round: every agent takes one turn, in order.</summary>
        public IEnumerator StepRound()
        {
            if (_busy || grid == null || IsLlmBlocked()) yield break;
            _busy = true;
            _round++;

            var agents = FindObjectsByType<Agent>(FindObjectsSortMode.InstanceID);

            foreach (var agent in agents)
            {
                if (agent == null || agent.Brain == null) continue;

                yield return RunTurn(agent, agents);

                if (pauseBetweenTurns > 0f)
                    yield return new WaitForSeconds(pauseBetweenTurns);
            }

            _busy = false;
        }

        private IEnumerator RunTurn(Agent agent, Agent[] all)
        {
            // Per-turn budget: how many times each action type may be used (from config).
            var remaining = new Dictionary<string, int>();
            foreach (var key in ActionConfig.BudgetedKeys())
                remaining[key] = ActionConfig.UsesPerTurn(key);

            var actionsThisTurn = new List<string>();
            int safety = 0;
            bool actedAtLeastOnce = false;

            while (safety++ < 24)
            {
                // Which budgeted actions are still available?
                var available = new List<string>();
                foreach (var kv in remaining)
                    if (kv.Value > 0) available.Add(kv.Key);
                if (available.Count == 0) break; // nothing left to do

                var perception = BuildPerception(agent, all);
                perception.AvailableActions = available;
                perception.ActionsThisTurn = new List<string>(actionsThisTurn);

                // Ask the brain for ONE action (a slow LLM call simply blocks here).
                AgentAction action = null;
                yield return agent.Brain.Decide(perception, a => action = a);

                if (action == null || action.Type == ActionType.End) break;

                string key = action.Key;
                if (!remaining.TryGetValue(key, out int left) || left <= 0)
                    break; // brain chose an unavailable action — end rather than loop

                string summary = null;
                switch (action.Type)
                {
                    case ActionType.Move:
                        bool moved = false;
                        var dest = action.TargetTile;
                        yield return agent.MoveTo(dest, moveSpeed, ok => moved = ok);
                        summary = moved ? $"Move→{dest}" : $"Move→{dest} (blocked)";
                        Log(agent, moved ? $"moved to {dest}" : $"tried to move to {dest} (blocked)");
                        break;

                    case ActionType.Talk:
                        DeliverTalk(agent, action.Message, all);
                        summary = $"Talk:\"{action.Message}\"";
                        break;

                    case ActionType.Inspect:
                        summary = ResolveInspect(agent, action.TargetTile);
                        break;
                }

                remaining[key] = left - 1;
                actionsThisTurn.Add(summary);
                RecordAction(agent, summary, perception);
                actedAtLeastOnce = true;

                if (pauseBetweenActions > 0f)
                    yield return new WaitForSeconds(pauseBetweenActions);
            }

            if (!actedAtLeastOnce)
                RecordAction(agent, "idle", BuildPerception(agent, all));
        }

        /// <summary>Detailed examination of one tile. Placeholder detail for now; expandable.</summary>
        private string ResolveInspect(Agent agent, GridCoord target)
        {
            var tile = grid.GetTile(target);
            string detail;
            if (tile == null)
                detail = $"Inspect ({target.x},{target.y}): outside the world";
            else
            {
                string biome = tile.Biome != null ? tile.Biome.name : "unknown terrain";
                string occ = tile.IsOccupied ? (tile.Occupant?.DisplayName ?? "occupied") : "empty";
                detail = $"Inspect ({target.x},{target.y}): {biome}, {occ}";
            }
            Log(agent, detail);
            return detail;
        }

        /// <summary>Appends one executed action to the agent's memory / context file.</summary>
        private void RecordAction(Agent agent, string summary, AgentPerception perception)
        {
            if (agent.Memory == null) return;

            var observed = new string[perception.VisibleAgents.Count];
            for (int i = 0; i < observed.Length; i++)
            {
                var v = perception.VisibleAgents[i];
                observed[i] = $"{v.Id}@({v.Coord.x},{v.Coord.y}) d{v.Distance}";
            }

            var heard = new string[perception.HeardMessages.Count];
            for (int i = 0; i < heard.Length; i++)
            {
                var h = perception.HeardMessages[i];
                heard[i] = $"{h.FromId}: {h.Text}";
            }

            agent.Memory.Append(new TurnRecord
            {
                round = _round,
                action = summary,
                x = agent.Coord.x,
                y = agent.Coord.y,
                biome = perception.SelfBiome,
                observed = observed,
                heard = heard,
            });
        }

        private AgentPerception BuildPerception(Agent agent, Agent[] all)
        {
            var stats = agent.Stats;
            var self = agent.Coord;

            var p = new AgentPerception
            {
                SelfId = agent.AgentId,
                SelfCoord = self,
                WorldWidth = grid.Width,
                WorldHeight = grid.Height,
                MoveRange = stats.Move,
                ObserveRange = stats.Observe,
                TalkRange = stats.Talk,
            };

            // Reachable tiles = orthogonal stepping budget (Manhattan distance),
            // matching the cardinal-only walk animation. Includes current tile (stay).
            for (int dx = -stats.Move; dx <= stats.Move; dx++)
            {
                for (int dy = -stats.Move; dy <= stats.Move; dy++)
                {
                    var c = new GridCoord(self.x + dx, self.y + dy);
                    if (!grid.Config.InBounds(c)) continue;
                    if (GridCoord.ManhattanDistance(self, c) > stats.Move) continue;

                    var tile = grid.GetTile(c);
                    if (c == self || (tile != null && !tile.IsOccupied))
                        p.ReachableTiles.Add(c);
                }
            }

            // Observe range = Chebyshev radius (a square area around the agent).
            foreach (var other in all)
            {
                if (other == null || other == agent) continue;
                int d = GridCoord.ChebyshevDistance(self, other.Coord);
                if (d <= stats.Observe)
                    p.VisibleAgents.Add(new ObservedAgent { Id = other.AgentId, Coord = other.Coord, Distance = d });
            }

            // Terrain: the biome underfoot, plus distinct other biomes within Observe range.
            var selfTile = grid.GetTile(self);
            if (selfTile != null && selfTile.Biome != null)
            {
                p.SelfBiome = selfTile.Biome.name;
                p.SelfBiomeDescription = selfTile.Biome.description;
            }
            var nearby = new HashSet<string>();
            for (int dx = -stats.Observe; dx <= stats.Observe; dx++)
            {
                for (int dy = -stats.Observe; dy <= stats.Observe; dy++)
                {
                    var c = new GridCoord(self.x + dx, self.y + dy);
                    if (!grid.Config.InBounds(c)) continue;
                    var t = grid.GetTile(c);
                    if (t != null && t.Biome != null && t.Biome.name != p.SelfBiome)
                        nearby.Add(t.Biome.name);
                }
            }
            p.NearbyBiomes = new List<string>(nearby);

            if (_inbox.TryGetValue(agent, out var heard) && heard.Count > 0)
            {
                p.HeardMessages = heard;
                _inbox[agent] = new List<HeardMessage>();
            }

            // The agent's own recent history (its memory), for the brain to reason over.
            if (agent.Memory != null && recentMemoryWindow > 0)
                p.RecentHistory = agent.Memory.Recent(recentMemoryWindow);

            return p;
        }

        private void DeliverTalk(Agent speaker, string message, Agent[] all)
        {
            int reach = speaker.Stats.Talk;
            int heardBy = 0;

            foreach (var other in all)
            {
                if (other == null || other == speaker) continue;
                if (GridCoord.ChebyshevDistance(speaker.Coord, other.Coord) > reach) continue;

                GetInbox(other).Add(new HeardMessage { FromId = speaker.AgentId, Text = message });
                heardBy++;
            }

            Log(speaker, $"said \"{message}\" (heard by {heardBy} within {reach})");
        }

        private List<HeardMessage> GetInbox(Agent agent)
        {
            if (!_inbox.TryGetValue(agent, out var list))
            {
                list = new List<HeardMessage>();
                _inbox[agent] = list;
            }
            return list;
        }

        private void Log(Agent agent, string what)
        {
            if (verboseLog) Debug.Log($"[Round {_round}] {agent.AgentId}: {what}");
        }
    }
}
