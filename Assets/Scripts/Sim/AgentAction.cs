using AISandbox.World;

namespace AISandbox.Sim
{
    public enum ActionType
    {
        Move,
        Talk,
        Inspect,
        End,
    }

    /// <summary>
    /// A single action an agent takes. The brain commits one of these per call; the
    /// TurnManager loops, asking again until the budget is spent or the agent ends
    /// its turn. (Look is not an action here — it's the free survey always included
    /// in perception.) Use the static factories to build one.
    /// </summary>
    public class AgentAction
    {
        public ActionType Type;

        /// <summary>Destination (Move) or examined tile (Inspect).</summary>
        public GridCoord TargetTile;

        /// <summary>What the agent says (Talk).</summary>
        public string Message;

        /// <summary>The action's config key ("move"/"talk"/"inspect"/"end"); used for budgeting.</summary>
        public string Key => Type switch
        {
            ActionType.Move => "move",
            ActionType.Talk => "talk",
            ActionType.Inspect => "inspect",
            _ => "end",
        };

        public static AgentAction Move(GridCoord target) =>
            new AgentAction { Type = ActionType.Move, TargetTile = target };

        public static AgentAction Talk(string message) =>
            new AgentAction { Type = ActionType.Talk, Message = message };

        public static AgentAction Inspect(GridCoord target) =>
            new AgentAction { Type = ActionType.Inspect, TargetTile = target };

        public static AgentAction End() =>
            new AgentAction { Type = ActionType.End };

        public override string ToString() => Type switch
        {
            ActionType.Move => $"Move → {TargetTile}",
            ActionType.Talk => $"Talk: \"{Message}\"",
            ActionType.Inspect => $"Inspect {TargetTile}",
            _ => "End turn",
        };
    }
}
