using System;
using System.Collections;
using System.Text;
using AISandbox.Sim;
using AISandbox.World;
using UnityEngine;
using UnityEngine.Networking;

namespace AISandbox.Brains
{
    /// <summary>
    /// An IAgentBrain backed by an LLM over an OpenAI-compatible chat endpoint.
    /// It builds a prompt from the agent's perception + memory, sends one request
    /// per turn, and parses a strict-JSON action back. Coroutine-based: it yields
    /// the web request, so the TurnManager simply doesn't advance until the model
    /// replies. Any failure falls back to a safe "observe" so the sim never stalls.
    /// </summary>
    public class LlmBrain : IAgentBrain
    {
        private readonly LlmConfig _config;

        public LlmBrain(LlmConfig config)
        {
            _config = config;
        }

        public IEnumerator Decide(AgentPerception p, Action<AgentAction> commit)
        {
            if (_config == null)
            {
                commit(Fallback("no LLM config"));
                yield break;
            }

            string body = BuildRequestBody(p);

            using (var req = new UnityWebRequest(_config.ChatCompletionsUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(_config.apiKey))
                    req.SetRequestHeader("Authorization", "Bearer " + _config.apiKey);
                req.timeout = Mathf.Max(1, _config.timeoutSeconds);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"LlmBrain ({p.SelfId}): request failed — {req.error}. Falling back to observe.");
                    commit(Fallback("request failed"));
                    yield break;
                }

                string content = ExtractAssistantContent(req.downloadHandler.text);
                AgentAction action = ParseAction(content, p);
                commit(action ?? Fallback("could not parse response"));
            }
        }

        // ---- Prompt building ----------------------------------------------------

        private string BuildRequestBody(AgentPerception p)
        {
            var messages = new[]
            {
                new ReqMessage { role = "system", content = SystemPrompt(p) },
                new ReqMessage { role = "user", content = UserPrompt(p) },
            };

            var reqBody = new ReqBody
            {
                model = _config.model,
                messages = messages,
                temperature = _config.temperature,
                max_tokens = _config.maxTokens,
            };
            return JsonUtility.ToJson(reqBody);
        }

        private static string SystemPrompt(AgentPerception p)
        {
            return
                $"You are {p.SelfId}, an autonomous agent living in a shared {p.WorldWidth}x{p.WorldHeight} tile grid world with other agents. " +
                "You experience the world one turn at a time. Each turn you choose exactly ONE action:\n" +
                $"- move: walk to a tile within Manhattan distance {p.MoveRange} of your position (cardinal steps only); you cannot enter a tile occupied by another agent.\n" +
                $"- talk: shout a short message; only agents within {p.TalkRange} tiles hear it.\n" +
                "- observe: take in your surroundings and do nothing else this turn.\n" +
                $"You can see other agents within {p.ObserveRange} tiles. You are curious and social: explore, seek out others, communicate, and react to what they say and do.\n" +
                "Respond with ONLY a single JSON object and no other text, in one of these exact shapes:\n" +
                "{\"action\":\"move\",\"x\":<int>,\"y\":<int>,\"note\":\"<short reason>\"}\n" +
                "{\"action\":\"talk\",\"message\":\"<what you say>\",\"note\":\"<short reason>\"}\n" +
                "{\"action\":\"observe\",\"note\":\"<short reason>\"}";
        }

        private static string UserPrompt(AgentPerception p)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Your position: ({p.SelfCoord.x}, {p.SelfCoord.y}).");
            sb.AppendLine($"World bounds: x 0..{p.WorldWidth - 1}, y 0..{p.WorldHeight - 1}. Move range: {p.MoveRange} (Manhattan).");

            if (p.VisibleAgents.Count > 0)
            {
                var who = new StringBuilder();
                foreach (var v in p.VisibleAgents)
                {
                    if (who.Length > 0) who.Append(", ");
                    who.Append($"{v.Id} at ({v.Coord.x},{v.Coord.y})");
                }
                sb.AppendLine($"Agents you can see: {who}.");
            }
            else sb.AppendLine("Agents you can see: none.");

            if (p.HeardMessages.Count > 0)
            {
                sb.AppendLine("You just heard:");
                foreach (var h in p.HeardMessages)
                    sb.AppendLine($"  - {h.FromId}: \"{h.Text}\"");
            }
            else sb.AppendLine("You heard nothing this turn.");

            if (p.RecentHistory.Count > 0)
            {
                sb.AppendLine("Your recent memory:");
                foreach (var r in p.RecentHistory)
                    sb.AppendLine($"  - R{r.round}: {r.action}");
            }
            else sb.AppendLine("Your recent memory: (none yet).");

            sb.Append("It is your turn. Respond with JSON only.");
            return sb.ToString();
        }

        // ---- Response parsing ---------------------------------------------------

        private static string ExtractAssistantContent(string responseText)
        {
            try
            {
                var resp = JsonUtility.FromJson<RespBody>(responseText);
                if (resp != null && resp.choices != null && resp.choices.Length > 0 && resp.choices[0].message != null)
                    return resp.choices[0].message.content;
            }
            catch { /* fall through */ }
            return null;
        }

        private static AgentAction ParseAction(string content, AgentPerception p)
        {
            if (string.IsNullOrEmpty(content)) return null;

            // The model may wrap JSON in prose or code fences; grab the object.
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            string json = content.Substring(start, end - start + 1);

            ActionJson aj;
            try { aj = JsonUtility.FromJson<ActionJson>(json); }
            catch { return null; }
            if (aj == null || string.IsNullOrEmpty(aj.action)) return null;

            AgentAction action;
            switch (aj.action.Trim().ToLowerInvariant())
            {
                case "move":
                    var requested = new GridCoord(aj.x, aj.y);
                    var target = ResolveReachable(p, requested);
                    action = target.HasValue ? AgentAction.Move(target.Value) : AgentAction.Observe();
                    break;

                case "talk":
                    action = string.IsNullOrWhiteSpace(aj.message)
                        ? AgentAction.Observe()
                        : AgentAction.Talk(aj.message.Trim());
                    break;

                default: // "observe" or anything unexpected
                    action = AgentAction.Observe();
                    break;
            }

            action.Note = aj.note;
            return action;
        }

        /// <summary>
        /// Returns the requested tile if it's reachable; otherwise the reachable
        /// tile closest to it (so a slightly-too-far request still makes progress).
        /// Null only if the agent has nowhere to go.
        /// </summary>
        private static GridCoord? ResolveReachable(AgentPerception p, GridCoord requested)
        {
            if (p.ReachableTiles.Contains(requested)) return requested;
            if (p.ReachableTiles.Count == 0) return null;

            GridCoord best = p.ReachableTiles[0];
            int bestDist = int.MaxValue;
            foreach (var c in p.ReachableTiles)
            {
                int d = GridCoord.ManhattanDistance(requested, c);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        private static AgentAction Fallback(string why)
        {
            var a = AgentAction.Observe();
            a.Note = $"[fallback: {why}]";
            return a;
        }

        // ---- JSON DTOs ----------------------------------------------------------

        [Serializable] private class ReqMessage { public string role; public string content; }

        [Serializable]
        private class ReqBody
        {
            public string model;
            public ReqMessage[] messages;
            public float temperature;
            public int max_tokens;
        }

        [Serializable] private class RespBody { public Choice[] choices; }
        [Serializable] private class Choice { public RespMessage message; }
        [Serializable] private class RespMessage { public string content; }

        [Serializable]
        private class ActionJson
        {
            public string action;
            public int x;
            public int y;
            public string message;
            public string note;
        }
    }
}
