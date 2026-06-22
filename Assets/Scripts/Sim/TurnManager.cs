using System.Collections;
using System.Collections.Generic;
using AISandbox.Agents;
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
                if (!_busy && kb != null && kb.spaceKey.wasPressedThisFrame)
                    StartCoroutine(StepRound());
            }
        }

        private IEnumerator ContinuousLoop()
        {
            _loopRunning = true;
            while (mode == RunMode.Continuous)
            {
                yield return StepRound();
                yield return null; // guarantee at least one frame per round
            }
            _loopRunning = false;
        }

        /// <summary>Runs one full round: every agent takes one turn, in order.</summary>
        public IEnumerator StepRound()
        {
            if (_busy || grid == null) yield break;
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
            var perception = BuildPerception(agent, all);

            AgentAction chosen = null;
            // Fully waits here — a real LLM brain yields its web request and we
            // simply don't advance until it commits an action.
            yield return agent.Brain.Decide(perception, a => chosen = a);
            if (chosen == null) yield break;

            switch (chosen.Type)
            {
                case ActionType.Move:
                    yield return agent.MoveTo(chosen.MoveTarget, moveSpeed, ok =>
                        Log(agent, ok ? $"moved to {chosen.MoveTarget}"
                                      : $"tried to move to {chosen.MoveTarget} (blocked)"));
                    break;

                case ActionType.Talk:
                    DeliverTalk(agent, chosen.Message, all);
                    break;

                case ActionType.Observe:
                    Log(agent, $"observed (sees {perception.VisibleAgents.Count} agent(s) within {agent.Stats.Observe})");
                    break;
            }

            RecordTurn(agent, chosen, perception);
        }

        /// <summary>Appends this turn to the agent's memory / context file.</summary>
        private void RecordTurn(Agent agent, AgentAction action, AgentPerception perception)
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
                action = action.ToString(),
                x = agent.Coord.x,
                y = agent.Coord.y,
                observed = observed,
                heard = heard,
                note = action.Note,
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
