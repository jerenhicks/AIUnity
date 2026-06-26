using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AISandbox.Sim
{
    [Serializable]
    public class ActionDef
    {
        public string key;
        public int usesPerTurn;   // 0 = no budget cost; >0 = max uses per turn
        public bool automatic;    // true = perception provided automatically, NOT a reply option (look)
        public string description;
    }

    [Serializable]
    public class ActionConfigData
    {
        public string rules = "";
        public ActionDef[] actions = Array.Empty<ActionDef>();
    }

    /// <summary>
    /// Loads the action rulebook from agent_actions.json at the project root. This
    /// single file drives two things: the per-turn budget (how many times each
    /// action may be used) and the action dictionary sent to the LLM each call.
    /// Falls back to sensible built-in defaults if the file is missing/invalid, so
    /// the sim always runs.
    /// </summary>
    public static class ActionConfig
    {
        private static ActionConfigData _data;

        public static ActionConfigData Data => _data ??= Load();

        public static string DefaultPath
        {
            get
            {
                var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath;
                return Path.Combine(root, "agent_actions.json");
            }
        }

        /// <summary>Force a reload (e.g. after editing the file).</summary>
        public static void Reload() => _data = Load();

        /// <summary>How many times an action key may be used per turn (0 = free/uncapped).</summary>
        public static int UsesPerTurn(string key)
        {
            foreach (var a in Data.actions)
                if (a != null && string.Equals(a.key, key, StringComparison.OrdinalIgnoreCase))
                    return Mathf.Max(0, a.usesPerTurn);
            return 0;
        }

        /// <summary>Action keys that cost budget (usesPerTurn > 0): move, talk, inspect, ...</summary>
        public static List<string> BudgetedKeys()
        {
            var keys = new List<string>();
            foreach (var a in Data.actions)
                if (a != null && a.usesPerTurn > 0) keys.Add(a.key);
            return keys;
        }

        /// <summary>The rulebook text sent to the LLM: rules, automatic perception, and the
        /// actions the model may actually reply with. 'look' is shown as automatic perception
        /// (never a reply); only non-automatic actions are listed as choices.</summary>
        public static string DictionaryText()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Data.rules))
            {
                sb.AppendLine(Data.rules);
                sb.AppendLine();
            }

            sb.AppendLine("PERCEPTION (always provided automatically in each request — never reply with these):");
            foreach (var a in Data.actions)
            {
                if (a == null || string.IsNullOrEmpty(a.key) || !a.automatic) continue;
                sb.AppendLine($"- {a.key}: {a.description}");
            }
            sb.AppendLine();

            var choices = new List<string>();
            sb.AppendLine("ACTIONS YOU MAY CHOOSE (reply with exactly ONE of these):");
            foreach (var a in Data.actions)
            {
                if (a == null || string.IsNullOrEmpty(a.key) || a.automatic) continue;
                string limit = a.usesPerTurn > 0 ? $" (up to {a.usesPerTurn}/turn)" : "";
                sb.AppendLine($"- {a.key}{limit}: {a.description}");
                choices.Add(a.key);
            }
            sb.AppendLine();
            sb.AppendLine($"Valid \"action\" values: {string.Join(", ", choices)}. Reply with a single JSON object and no other text.");
            return sb.ToString();
        }

        private static ActionConfigData Load()
        {
            try
            {
                string path = DefaultPath;
                if (File.Exists(path))
                {
                    var data = JsonUtility.FromJson<ActionConfigData>(File.ReadAllText(path));
                    if (data != null && data.actions != null && data.actions.Length > 0)
                        return data;
                }
                Debug.Log($"ActionConfig: agent_actions.json not found/empty at {path}; using built-in defaults.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ActionConfig: failed to read agent_actions.json ({e.Message}); using built-in defaults.");
            }
            return Defaults();
        }

        private static ActionConfigData Defaults()
        {
            return new ActionConfigData
            {
                rules = "You control one agent in a shared tile world with other agents. You are asked for ONE action at a time; after each, the world updates and you are asked again with your remaining actions. You may stop at any point with 'end'. Always reply with a single JSON object and nothing else.",
                actions = new[]
                {
                    new ActionDef { key = "look",    usesPerTurn = 0, automatic = true, description = "A high-level survey of tiles within view (terrain + agents), always included in each request." },
                    new ActionDef { key = "move",    usesPerTurn = 1, description = "Walk within move range (Manhattan). JSON: {\"action\":\"move\",\"x\":<int>,\"y\":<int>}" },
                    new ActionDef { key = "talk",    usesPerTurn = 1, description = "Speak; heard within talk range. JSON: {\"action\":\"talk\",\"message\":\"\"}" },
                    new ActionDef { key = "inspect", usesPerTurn = 1, description = "Examine one tile in view in detail. JSON: {\"action\":\"inspect\",\"x\":<int>,\"y\":<int>}" },
                    new ActionDef { key = "end",     usesPerTurn = 0, description = "End your turn now. JSON: {\"action\":\"end\"}" },
                },
            };
        }
    }
}
