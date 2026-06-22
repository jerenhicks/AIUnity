using System;
using UnityEngine;

namespace AISandbox.Agents
{
    /// <summary>
    /// An agent's capabilities. Each stat is an integer 1-6.
    ///   Move    = max tiles moved per turn.
    ///   Observe = radius (tiles) within which the agent perceives others.
    ///   Talk    = radius (tiles) the agent's shout reaches.
    /// Distance is measured with Chebyshev (king's-move) distance.
    /// </summary>
    [Serializable]
    public class AgentStats
    {
        public const int Min = 1;
        public const int Max = 6;

        [Range(Min, Max)] public int move = 4;
        [Range(Min, Max)] public int observe = 3;
        [Range(Min, Max)] public int talk = 2;

        public int Move => Mathf.Clamp(move, Min, Max);
        public int Observe => Mathf.Clamp(observe, Min, Max);
        public int Talk => Mathf.Clamp(talk, Min, Max);

        public override string ToString() => $"Move {Move} / Observe {Observe} / Talk {Talk}";
    }
}
