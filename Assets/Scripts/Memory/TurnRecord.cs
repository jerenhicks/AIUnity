using System;
using System.Collections.Generic;

namespace AISandbox.Memory
{
    /// <summary>
    /// One turn's entry in an agent's memory: what it did, where it ended up,
    /// what it saw and heard, and an optional free-form note (which an LLM brain
    /// can use to record its reasoning). Plain serializable fields so Unity's
    /// JsonUtility can round-trip it.
    /// </summary>
    [Serializable]
    public class TurnRecord
    {
        public int round;
        public string action;     // the action taken, e.g. "Move→(3, 4)", "Talk:\"Hi\"", "Inspect (5,2): Desert, empty"
        public int x;             // position after the action
        public int y;
        public string biome;      // terrain the agent is on
        public string[] observed; // other agents seen
        public string[] heard;    // messages heard
    }

    /// <summary>
    /// The full memory document for a single agent: identity, stat block, and the
    /// running history of turns. This is the canonical context an LLM brain reads.
    /// </summary>
    [Serializable]
    public class AgentMemoryData
    {
        public string agentId;
        public int move;
        public int observe;
        public int talk;
        public List<TurnRecord> history = new List<TurnRecord>();
    }
}
