using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AISandbox.Logging
{
    /// <summary>
    /// Process-wide record of every LLM call: what was sent and what came back.
    /// Appends each call to a global file (&lt;projectRoot&gt;/LlmLog/llm_calls.jsonl)
    /// and keeps a capped in-memory list that the on-screen log window renders.
    /// Subscribe to <see cref="Changed"/> to refresh a view when a new call lands.
    /// </summary>
    public static class LlmLog
    {
        [Serializable]
        public struct Entry
        {
            public string time;
            public string agent;
            public string prompt;
            public string response;
        }

        private const int MaxInMemory = 200;
        private static readonly List<Entry> _entries = new List<Entry>();
        private static string _filePath;

        public static IReadOnlyList<Entry> Entries => _entries;
        public static event Action Changed;

        private static string FilePath
        {
            get
            {
                if (_filePath == null)
                {
                    var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath;
                    var dir = Path.Combine(root, "LlmLog");
                    try { Directory.CreateDirectory(dir); } catch { /* ignore */ }
                    _filePath = Path.Combine(dir, "llm_calls.jsonl");
                }
                return _filePath;
            }
        }

        public static void Record(string agent, string prompt, string response)
        {
            var entry = new Entry
            {
                time = DateTime.Now.ToString("HH:mm:ss"),
                agent = agent,
                prompt = prompt,
                response = response,
            };

            _entries.Add(entry);
            if (_entries.Count > MaxInMemory)
                _entries.RemoveRange(0, _entries.Count - MaxInMemory);

            try { File.AppendAllText(FilePath, JsonUtility.ToJson(entry) + "\n"); }
            catch (Exception e) { Debug.LogWarning($"LlmLog: write failed — {e.Message}"); }

            Changed?.Invoke();
        }
    }
}
