using System;
using System.Collections;
using AISandbox.Sim;

namespace AISandbox.Brains
{
    /// <summary>
    /// Decides ONE action for an agent given its current perception. The TurnManager
    /// calls this repeatedly within a turn (re-building perception each time) until
    /// the agent's action budget is spent or it commits an End action.
    ///
    /// Coroutine-based on purpose: the StubBrain decides instantly and just calls
    /// <paramref name="commit"/>, but the LLM brain can `yield return` a web request
    /// and call <paramref name="commit"/> when the model replies — no change to the
    /// TurnManager. That is the "LLM-ready" contract: swap the implementation, keep
    /// the interface.
    /// </summary>
    public interface IAgentBrain
    {
        /// <param name="perception">Read-only snapshot of what the agent knows now.</param>
        /// <param name="commit">Call exactly once with the chosen action.</param>
        IEnumerator Decide(AgentPerception perception, Action<AgentAction> commit);
    }
}
