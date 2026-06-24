using System;
using System.Collections;
using System.Collections.Generic;
using AISandbox.Sim;

namespace AISandbox.Brains
{
    /// <summary>
    /// Placeholder decision-maker: picks a valid action at random. Stands in for a
    /// real model so the whole simulation loop can run and be debugged now. Replace
    /// with an LLM-backed IAgentBrain later without touching anything else.
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

        public IEnumerator Decide(AgentPerception p, Action<AgentTurn> commit)
        {
            commit(Plan(p));
            yield break; // decided instantly; an LLM brain would yield on a request here
        }

        // Each turn the stub may move, talk, and observe — a random subset, in random
        // order — so it exercises the full composite-turn path the LLM brain uses.
        private AgentTurn Plan(AgentPerception p)
        {
            var steps = new List<AgentAction>();

            if (p.ReachableTiles.Count > 0 && _rng.Next(100) < 70)
            {
                var pick = p.ReachableTiles[_rng.Next(p.ReachableTiles.Count)];
                if (pick == p.SelfCoord && p.ReachableTiles.Count > 1)
                    pick = p.ReachableTiles[_rng.Next(p.ReachableTiles.Count)];
                steps.Add(AgentAction.Move(pick));
            }

            if (_rng.Next(100) < 40)
                steps.Add(AgentAction.Talk(SmallTalk[_rng.Next(SmallTalk.Length)]));

            if (steps.Count == 0 || _rng.Next(100) < 50)
            {
                var observe = AgentAction.Observe();
                observe.Note = $"on {Terrain(p)}";
                steps.Add(observe);
            }

            // Shuffle step order.
            for (int i = steps.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (steps[i], steps[j]) = (steps[j], steps[i]);
            }

            return new AgentTurn(steps.ToArray());
        }

        private static string Terrain(AgentPerception p) =>
            string.IsNullOrEmpty(p.SelfBiome) ? "open ground" : p.SelfBiome;
    }
}
