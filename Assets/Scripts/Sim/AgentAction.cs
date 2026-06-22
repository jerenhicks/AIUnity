using AISandbox.World;

namespace AISandbox.Sim
{
    public enum ActionType
    {
        Move,
        Observe,
        Talk,
    }

    /// <summary>
    /// A single turn's intent, produced by a brain and applied by the TurnManager.
    /// Adding a new action later = add an ActionType and a resolver branch; nothing
    /// else needs to change. Use the static factory methods to build one.
    /// </summary>
    public class AgentAction
    {
        public ActionType Type;

        /// <summary>Destination tile (for Move).</summary>
        public GridCoord MoveTarget;

        /// <summary>What the agent shouts (for Talk).</summary>
        public string Message;

        /// <summary>Optional free-form reasoning the brain attaches; saved to memory.</summary>
        public string Note;

        public static AgentAction Move(GridCoord target) =>
            new AgentAction { Type = ActionType.Move, MoveTarget = target };

        public static AgentAction Observe() =>
            new AgentAction { Type = ActionType.Observe };

        public static AgentAction Talk(string message) =>
            new AgentAction { Type = ActionType.Talk, Message = message };

        public override string ToString() => Type switch
        {
            ActionType.Move => $"Move -> {MoveTarget}",
            ActionType.Talk => $"Talk: \"{Message}\"",
            _ => "Observe",
        };
    }
}
