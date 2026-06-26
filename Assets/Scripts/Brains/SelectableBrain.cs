using System;
using System.Collections;
using AISandbox.Sim;

namespace AISandbox.Brains
{
    /// <summary>
    /// A brain wrapper that dispatches each decision based on <see cref="BrainSelector.UseLlm"/>:
    /// <list type="bullet">
    ///   <item><b>UseLlm = false</b> → built-in stub brain decides (the safe default).</item>
    ///   <item><b>UseLlm = true</b> with a working LLM brain → LLM decides.</item>
    ///   <item><b>UseLlm = true</b> but no LLM brain available (config didn't load) →
    ///         <b>no decision is made</b>. We commit an End action so the contract
    ///         is honored, but we deliberately do NOT silently fall back to the stub —
    ///         the user explicitly asked to use the LLM, and acting as if everything's
    ///         fine would be misleading.</item>
    /// </list>
    /// TurnManager additionally pauses the round entirely while we're in that
    /// blocked state, so this no-op path is only hit if a round is already mid-flight.
    /// </summary>
    public class SelectableBrain : IAgentBrain
    {
        private readonly IAgentBrain _llm; // may be null if config invalid
        private readonly IAgentBrain _stub;

        public SelectableBrain(IAgentBrain llm, IAgentBrain stub)
        {
            _llm = llm;
            _stub = stub ?? throw new ArgumentNullException(nameof(stub));
        }

        public bool LlmAvailable => _llm != null;

        public IEnumerator Decide(AgentPerception perception, Action<AgentAction> commit)
        {
            if (BrainSelector.UseLlm)
            {
                if (_llm != null) return _llm.Decide(perception, commit);
                return DoNothing(commit);
            }
            return _stub.Decide(perception, commit);
        }

        private static IEnumerator DoNothing(Action<AgentAction> commit)
        {
            commit(AgentAction.End());
            yield break;
        }
    }
}
