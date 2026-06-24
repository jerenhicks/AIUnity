using System;
using System.Collections;
using AISandbox.Sim;

namespace AISandbox.Brains
{
    /// <summary>
    /// Decides what an agent does this turn — an <see cref="AgentTurn"/> of up to one
    /// move, one talk, and one observe, in a chosen order.
    ///
    /// Coroutine-based on purpose: the StubBrain decides instantly and just calls
    /// <paramref name="commit"/>, but the LLM brain can `yield return` a web request
    /// and call <paramref name="commit"/> when the model replies — no change to the
    /// TurnManager. That is the "LLM-ready" contract: swap the implementation, keep
    /// the interface.
    /// </summary>
    public interface IAgentBrain
    {
        /// <param name="perception">Read-only snapshot of what the agent knows.</param>
        /// <param name="commit">Call exactly once with the chosen turn.</param>
        IEnumerator Decide(AgentPerception perception, Action<AgentTurn> commit);
    }
}
