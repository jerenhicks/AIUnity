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
        public string action;     // joined steps this turn, e.g. "Move→(3, 4); Talk:\"Hi\"; Observe"
        public int x;             // position after the turn
        public int y;
        public string biome;      // terrain the agent ended the turn on
        public string[] observed; // other agents seen this turn
        public string[] heard;    // messages heard this turn
        public string note;       // optional reflection(s) the brain attached
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
