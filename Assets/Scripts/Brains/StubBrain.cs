using System;
using System.Collections;
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

        public IEnumerator Decide(AgentPerception p, Action<AgentAction> commit)
        {
            commit(Choose(p));
            yield break; // decided instantly; an LLM brain would yield on a request here
        }

        private AgentAction Choose(AgentPerception p)
        {
            // Weighted: mostly move, sometimes talk, sometimes observe.
            int roll = _rng.Next(100);

            if (roll < 60 && p.ReachableTiles.Count > 0)
            {
                // Prefer a tile other than the current one so movement is visible.
                var pick = p.ReachableTiles[_rng.Next(p.ReachableTiles.Count)];
                if (pick == p.SelfCoord && p.ReachableTiles.Count > 1)
                    pick = p.ReachableTiles[_rng.Next(p.ReachableTiles.Count)];
                return AgentAction.Move(pick);
            }

            if (roll < 85)
            {
                return AgentAction.Talk(SmallTalk[_rng.Next(SmallTalk.Length)]);
            }

            return AgentAction.Observe();
        }
    }
}
