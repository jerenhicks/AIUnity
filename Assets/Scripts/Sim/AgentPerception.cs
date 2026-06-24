using System.Collections.Generic;
using AISandbox.Memory;
using AISandbox.World;

namespace AISandbox.Sim
{
    /// <summary>One other agent this agent can currently see.</summary>
    public struct ObservedAgent
    {
        public string Id;
        public GridCoord Coord;
        public int Distance;
    }

    /// <summary>A message this agent heard (spoken within Talk range).</summary>
    public struct HeardMessage
    {
        public string FromId;
        public string Text;
    }

    /// <summary>
    /// A read-only snapshot of everything an agent's brain is allowed to know when
    /// deciding its next action. This is deliberately plain data (ids, coords,
    /// strings) so a future LLM brain can serialize it straight into a prompt.
    /// </summary>
    public class AgentPerception
    {
        public string SelfId;
        public GridCoord SelfCoord;

        /// <summary>World size in tiles; valid coords are 0..Width-1, 0..Height-1.</summary>
        public int WorldWidth;
        public int WorldHeight;

        public int MoveRange;
        public int ObserveRange;
        public int TalkRange;

        /// <summary>The biome the agent is currently standing on (null if ungenerated world).</summary>
        public string SelfBiome;
        public string SelfBiomeDescription;

        /// <summary>Distinct other biome names visible within Observe range.</summary>
        public List<string> NearbyBiomes = new List<string>();

        /// <summary>Empty tiles reachable this turn (includes current tile = "stay").</summary>
        public List<GridCoord> ReachableTiles = new List<GridCoord>();

        /// <summary>Other agents within Observe range.</summary>
        public List<ObservedAgent> VisibleAgents = new List<ObservedAgent>();

        /// <summary>Messages heard since this agent last acted.</summary>
        public List<HeardMessage> HeardMessages = new List<HeardMessage>();

        /// <summary>The agent's own recent turns (its memory), oldest-first.
        /// Unused by the StubBrain; the LLM brain will fold this into its prompt.</summary>
        public List<TurnRecord> RecentHistory = new List<TurnRecord>();
    }
}
