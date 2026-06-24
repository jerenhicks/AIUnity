using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AISandbox.Memory
{
    /// <summary>
    /// Owns one agent's context file. Holds the in-memory <see cref="AgentMemoryData"/>,
    /// appends a record each turn, and persists to disk as a canonical JSON file plus
    /// a human-readable .md mirror you can open and watch while it runs.
    ///
    /// Files live in &lt;projectRoot&gt;/AgentMemory/ (outside Assets, so Unity won't
    /// reimport them): agent_&lt;id&gt;.json and agent_&lt;id&gt;.md.
    /// </summary>
    public class AgentMemory
    {
        public AgentMemoryData Data { get; }

        private readonly string _jsonPath;
        private readonly string _mdPath;

        /// <summary>Default output folder: a sibling of the Assets folder.</summary>
        public static string DefaultDirectory
        {
            get
            {
                var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath;
                return Path.Combine(root, "AgentMemory");
            }
        }

        public AgentMemory(string directory, AgentMemoryData seed)
        {
            Data = seed ?? new AgentMemoryData();

            Directory.CreateDirectory(directory);
            string safeId = MakeSafe(Data.agentId);
            _jsonPath = Path.Combine(directory, $"agent_{safeId}.json");
            _mdPath = Path.Combine(directory, $"agent_{safeId}.md");

            Save(); // write the initial (empty-history) document
        }

        /// <summary>Records a turn and persists immediately.</summary>
        public void Append(TurnRecord record)
        {
            Data.history.Add(record);
            Save();
        }

        /// <summary>The most recent <paramref name="count"/> turns (oldest-first).</summary>
        public List<TurnRecord> Recent(int count)
        {
            int start = Mathf.Max(0, Data.history.Count - count);
            return Data.history.GetRange(start, Data.history.Count - start);
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(_jsonPath, JsonUtility.ToJson(Data, true));
                File.WriteAllText(_mdPath, BuildMarkdown());
            }
            catch (IOException e)
            {
                Debug.LogWarning($"AgentMemory: failed to write for {Data.agentId}: {e.Message}");
            }
        }

        private string BuildMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Agent {Data.agentId} — memory");
            sb.AppendLine($"Stats: Move {Data.move} / Observe {Data.observe} / Talk {Data.talk}");
            sb.AppendLine();

            foreach (var r in Data.history)
            {
                string where = string.IsNullOrEmpty(r.biome) ? $"({r.x}, {r.y})" : $"({r.x}, {r.y}) on {r.biome}";
                sb.AppendLine($"## Round {r.round} — ended at {where}");
                sb.AppendLine($"- Action: {r.action}");
                sb.AppendLine($"- Saw: {Join(r.observed)}");
                sb.AppendLine($"- Heard: {Join(r.heard)}");
                if (!string.IsNullOrEmpty(r.note))
                    sb.AppendLine($"- Note: {r.note}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string Join(string[] items)
        {
            if (items == null || items.Length == 0) return "nothing";
            return string.Join("; ", items);
        }

        private static string MakeSafe(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            foreach (char c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return id;
        }
    }
}
