using System;
using System.IO;
using UnityEngine;

namespace AISandbox.Brains
{
    /// <summary>
    /// LLM connection settings, loaded from a git-ignored JSON file at the project
    /// root (llm.config.json). Uses the OpenAI-compatible Chat Completions format,
    /// so it works against OpenAI, OpenRouter, Together, LM Studio, a local Ollama
    /// (/v1) endpoint, etc. — just change baseUrl/model.
    /// </summary>
    [Serializable]
    public class LlmConfig
    {
        public string baseUrl = "https://api.openai.com/v1";
        public string apiKey = "";
        public string model = "gpt-4o-mini";
        public float temperature = 0.8f;
        public int maxTokens = 300;
        public int timeoutSeconds = 30;

        /// <summary>Default location: &lt;projectRoot&gt;/llm.config.json (sibling of Assets).</summary>
        public static string DefaultPath
        {
            get
            {
                var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath;
                return Path.Combine(root, "llm.config.json");
            }
        }

        /// <summary>The chat completions endpoint, derived from baseUrl.</summary>
        public string ChatCompletionsUrl => baseUrl.TrimEnd('/') + "/chat/completions";

        /// <summary>
        /// Loads and validates the config. Returns null and sets <paramref name="error"/>
        /// to a short, user-facing message (suitable for the HUD) if the file is
        /// missing, unparseable, or still holds the placeholder key. Full detail
        /// (paths, exception messages) is logged via <see cref="Debug"/>.
        /// </summary>
        public static LlmConfig Load(string path, out string error)
        {
            error = null;

            if (!File.Exists(path))
            {
                Debug.LogWarning($"LlmConfig: file not found at {path} (copy llm.config.example.json to llm.config.json).");
                error = "Config file missing";
                return null;
            }

            LlmConfig cfg;
            try
            {
                cfg = JsonUtility.FromJson<LlmConfig>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LlmConfig: parse error at {path}: {e.Message}");
                error = "Config file unreadable";
                return null;
            }

            if (cfg == null || string.IsNullOrWhiteSpace(cfg.baseUrl))
            {
                Debug.LogWarning($"LlmConfig: {path} is missing baseUrl.");
                error = "Config missing baseUrl";
                return null;
            }

            // A local server may legitimately need no key; a remote one with the
            // placeholder still in place almost certainly won't work — warn loudly.
            if (cfg.apiKey == "PASTE-YOUR-KEY-HERE")
            {
                Debug.LogWarning($"LlmConfig: {path} still has the placeholder apiKey; set a real key (or empty for a local server).");
                error = "API key not set";
                return null;
            }

            return cfg;
        }
    }
}
