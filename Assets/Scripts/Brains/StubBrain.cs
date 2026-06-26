using System;
using System.Collections;
using AISandbox.Sim;
using AISandbox.World;
using UnityEngine;

namespace AISandbox.Brains
{
    /// <summary>
    /// Placeholder decision-maker: picks ONE valid action at random from whatever the
    /// agent may still do this turn (or ends the turn). Mirrors the LLM brain's
    /// one-action-per-call contract so the whole loop runs without a model.
    /// </summary>
    public class StubBrain : IAgentBrain
    {
        private readonly System.Random _rng;

        private static readonly string[] SmallTalk =
        {
            "Hello?", "Anyone there?", "Over here!", "I'm exploring.",
            "Found a spot.", "What are you building?", "Let's meet up.",
        };

        public StubBrain(int seed)
        {
            _rng = new System.Random(seed);
        }

        public IEnumerator Decide(AgentPerception p, Action<AgentAction> commit)
        {
            commit(Choose(p));
            yield break; // decided instantly; an LLM brain would yield on a request here
        }

        private AgentAction Choose(AgentPerception p)
        {
            var avail = p.AvailableActions;
            if (avail == null || avail.Count == 0) return AgentAction.End();

            // Sometimes stop early even with actions left.
            if (_rng.Next(100) < 15) return AgentAction.End();

            string key = avail[_rng.Next(avail.Count)];
            switch (key)
            {
                case "move":
                    if (p.ReachableTiles.Count > 0)
                    {
                        var pick = p.ReachableTiles[_rng.Next(p.ReachableTiles.Count)];
                        if (pick == p.SelfCoord && p.ReachableTiles.Count > 1)
                            pick = p.ReachableTiles[_rng.Next(p.ReachableTiles.Count)];
                        return AgentAction.Move(pick);
                    }
                    return AgentAction.End();

                case "talk":
                    return AgentAction.Talk(SmallTalk[_rng.Next(SmallTalk.Length)]);

                case "inspect":
                    return AgentAction.Inspect(PickVisibleTile(p));

                default:
                    return AgentAction.End();
            }
        }

        private GridCoord PickVisibleTile(AgentPerception p)
        {
            if (p.VisibleAgents.Count > 0 && _rng.Next(2) == 0)
                return p.VisibleAgents[_rng.Next(p.VisibleAgents.Count)].Coord;

            int r = Mathf.Max(1, p.ObserveRange);
            int x = Mathf.Clamp(p.SelfCoord.x + _rng.Next(-r, r + 1), 0, Mathf.Max(0, p.WorldWidth - 1));
            int y = Mathf.Clamp(p.SelfCoord.y + _rng.Next(-r, r + 1), 0, Mathf.Max(0, p.WorldHeight - 1));
            return new GridCoord(x, y);
        }
    }
}
