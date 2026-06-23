using System;

namespace AISandbox.Brains
{
    /// <summary>
    /// Process-wide broadcaster for the LLM connection's current state. The HUD
    /// subscribes here and updates the indicator + toggle accordingly.
    ///
    /// Intentionally static so any code path can publish without holding a
    /// reference (LlmBrain on request start/finish, AgentSpawner on startup
    /// config load, etc.). Subscribe in OnEnable / unsubscribe in OnDisable.
    /// </summary>
    public static class LlmStatus
    {
        public enum State
        {
            /// <summary>Initial value before anything has reported in.</summary>
            Unknown,
            /// <summary>Last attempt succeeded, or config loaded cleanly — assume reachable.</summary>
            Connected,
            /// <summary>A request is in flight; we're awaiting the model's response.</summary>
            Waiting,
            /// <summary>Last attempt failed, or config is missing/invalid.</summary>
            Error,
        }

        public static State Current { get; private set; } = State.Unknown;
        public static string Message { get; private set; } = "Initializing";

        /// <summary>(newState, message) — message is the short, user-facing label for the HUD.</summary>
        public static event Action<State, string> Changed;

        /// <summary>
        /// Update the global state. Fires <see cref="Changed"/> even when the state
        /// value didn't change (the message text may still have changed). Keep
        /// messages short (≲ 24 chars) so they fit the HUD label without clipping.
        /// </summary>
        public static void Set(State state, string message)
        {
            Current = state;
            Message = message ?? string.Empty;
            Changed?.Invoke(Current, Message);
        }

        /// <summary>Convenience: green / ready.</summary>
        public static void MarkConnected(string message = "Ready") => Set(State.Connected, message);

        /// <summary>Convenience: yellow / request in flight.</summary>
        public static void MarkWaiting(string message = "Thinking…") => Set(State.Waiting, message);

        /// <summary>Convenience: red / unreachable or errored.</summary>
        public static void MarkError(string message) => Set(State.Error, message ?? "Error");
    }
}
