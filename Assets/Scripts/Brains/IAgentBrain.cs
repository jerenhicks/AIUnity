using System;
using System.Collections;
using AISandbox.Sim;

namespace AISandbox.Brains
{
    /// <summary>
    /// Decides one action for an agent given its perception.
    ///
    /// This is coroutine-based on purpose: the StubBrain decides instantly and
    /// just calls <paramref name="commit"/>, but a future LLM brain can `yield
    /// return` a web request and call <paramref name="commit"/> when the model
    /// responds — no change to the TurnManager or anything else. That is the
    /// "LLM-ready" contract: swap the implementation, keep the interface.
    /// </summary>
    public interface IAgentBrain
    {
        /// <param name="perception">Read-only snapshot of what the agent knows.</param>
        /// <param name="commit">Call exactly once with the chosen action.</param>
        IEnumerator Decide(AgentPerception perception, Action<AgentAction> commit);
    }
}
