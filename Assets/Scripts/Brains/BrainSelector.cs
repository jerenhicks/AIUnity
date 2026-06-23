using System;

namespace AISandbox.Brains
{
    /// <summary>
    /// Process-wide flag for which brain implementation agents should use this turn.
    /// The HUD toggle writes here; <see cref="SelectableBrain"/> reads here.
    ///
    /// Kept separate from the HUD so non-UI code (tests, headless runs, the inspector
    /// defaults in AgentSpawner) can set the initial value without depending on
    /// UnityEngine.UI.
    /// </summary>
    public static class BrainSelector
    {
        /// <summary>True = LLM brain decides; false = built-in stub brain decides.</summary>
        public static bool UseLlm { get; private set; }

        /// <summary>Fires whenever <see cref="UseLlm"/> changes value.</summary>
        public static event Action<bool> Changed;

        public static void SetUseLlm(bool value)
        {
            if (UseLlm == value) return;
            UseLlm = value;
            Changed?.Invoke(UseLlm);
        }
    }
}
