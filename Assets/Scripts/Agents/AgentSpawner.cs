using System;
using System.Collections.Generic;
using AISandbox.Brains;
using AISandbox.Memory;
using AISandbox.World;
using UnityEngine;

namespace AISandbox.Agents
{
    /// <summary>
    /// Spawns a set of agents into the world at Play, configured from the
    /// Inspector. No prefabs needed — each agent builds its own marker.
    /// Attach to an empty GameObject (e.g. "Agents") in the scene.
    /// </summary>
    public class AgentSpawner : MonoBehaviour
    {
        [Serializable]
        public class SpawnEntry
        {
            public string id = "Agent";
            public GridCoord coord = new GridCoord(0, 0);
            public AgentStats stats = new AgentStats();
            public Color color = Color.cyan;
        }

        public enum BrainType { Stub, Llm }

        [Header("Brain")]
        [Tooltip("Stub = random valid actions. Llm = decisions from an LLM (needs llm.config.json).")]
        [SerializeField] private BrainType brainType = BrainType.Stub;

        [Tooltip("Leave empty to auto-find the WorldGrid in the scene.")]
        [SerializeField] private WorldGrid grid;

        [Header("Memory")]
        [Tooltip("Write each agent's context file (JSON + .md) as it acts.")]
        [SerializeField] private bool writeMemoryFiles = true;

        [Tooltip("Leave empty to use <projectRoot>/AgentMemory/.")]
        [SerializeField] private string memoryDirectoryOverride = "";

        [SerializeField]
        private List<SpawnEntry> agents = new List<SpawnEntry>
        {
            new SpawnEntry { id = "Ava",   coord = new GridCoord(2, 2),
                stats = new AgentStats { move = 4, observe = 3, talk = 2 }, color = new Color(0.2f, 0.5f, 1f) },
            new SpawnEntry { id = "Bjorn", coord = new GridCoord(7, 6),
                stats = new AgentStats { move = 3, observe = 4, talk = 3 }, color = new Color(1f, 0.55f, 0.1f) },
        };

        private readonly List<Agent> _spawned = new List<Agent>();
        public IReadOnlyList<Agent> Spawned => _spawned;

        private void Start()
        {
            if (grid == null) grid = FindFirstObjectByType<WorldGrid>();
            if (grid == null)
            {
                Debug.LogError("AgentSpawner: no WorldGrid found in the scene.");
                return;
            }

            // Load LLM config once (shared by all LLM brains). Fall back to Stub on failure.
            LlmConfig llmConfig = null;
            if (brainType == BrainType.Llm)
            {
                llmConfig = LlmConfig.Load(LlmConfig.DefaultPath, out string err);
                if (llmConfig == null)
                {
                    Debug.LogError($"AgentSpawner: LLM brain selected but config invalid — {err}. Using StubBrain instead.");
                    brainType = BrainType.Stub;
                }
                else
                {
                    Debug.Log($"AgentSpawner: LLM brain via {llmConfig.ChatCompletionsUrl} (model {llmConfig.model}).");
                }
            }

            foreach (var entry in agents)
            {
                var go = new GameObject($"Agent {entry.id}");
                go.transform.SetParent(transform, false);
                var agent = go.AddComponent<Agent>();
                agent.Init(grid, entry.id, entry.coord, entry.stats, entry.color);
                agent.Brain = brainType == BrainType.Llm
                    ? (IAgentBrain)new LlmBrain(llmConfig)
                    : new StubBrain(entry.id.GetHashCode());

                if (writeMemoryFiles)
                {
                    string dir = string.IsNullOrWhiteSpace(memoryDirectoryOverride)
                        ? AgentMemory.DefaultDirectory
                        : memoryDirectoryOverride;
                    var data = new AgentMemoryData
                    {
                        agentId = entry.id,
                        move = entry.stats.move,
                        observe = entry.stats.observe,
                        talk = entry.stats.talk,
                    };
                    agent.Memory = new AgentMemory(dir, data);
                }

                _spawned.Add(agent);
            }

            Debug.Log($"AgentSpawner: spawned {_spawned.Count} agent(s).");
            if (writeMemoryFiles && _spawned.Count > 0)
            {
                string dir = string.IsNullOrWhiteSpace(memoryDirectoryOverride)
                    ? AgentMemory.DefaultDirectory : memoryDirectoryOverride;
                Debug.Log($"AgentSpawner: writing agent memory files to {dir}");
            }
        }
    }
}
