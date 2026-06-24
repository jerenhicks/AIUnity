using System.Collections.Generic;

namespace AISandbox.Sim
{
    /// <summary>
    /// What an agent decides to do in a single turn: an ordered list of steps.
    /// A turn may contain at most one Move, one Talk, and one Observe, performed in
    /// the order given (so talking before vs after moving reaches different agents).
    /// The TurnManager enforces the one-per-type rule when resolving.
    /// </summary>
    public class AgentTurn
    {
        public List<AgentAction> Steps = new List<AgentAction>();

        public AgentTurn() { }

        public AgentTurn(params AgentAction[] steps)
        {
            if (steps != null) Steps.AddRange(steps);
        }

        public bool IsEmpty => Steps.Count == 0;

        public void Add(AgentAction step)
        {
            if (step != null) Steps.Add(step);
        }

        public static AgentTurn Of(params AgentAction[] steps) => new AgentTurn(steps);
    }
}
