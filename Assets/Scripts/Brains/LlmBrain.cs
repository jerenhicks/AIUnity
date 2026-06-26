using System;
using System.Collections;
using System.Text;
using AISandbox.Logging;
using AISandbox.Sim;
using AISandbox.World;
using UnityEngine;
using UnityEngine.Networking;

namespace AISandbox.Brains
{
    /// <summary>
    /// An IAgentBrain backed by an LLM over an OpenAI-compatible chat endpoint.
    /// Decides ONE action per call: builds a prompt from the action rulebook
    /// (ActionConfig), the agent's perception (incl. the free Look survey), what it
    /// has already done this turn, and its remaining actions; POSTs to the endpoint;
    /// parses a single-action JSON reply. Every call is logged via LlmLog. Any
    /// failure falls back to ending the turn so the sim never stalls.
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
                LlmStatus.MarkError("No config loaded");
                LlmLog.Record(p.SelfId, "(no request — config not loaded)", "(error)");
                commit(AgentAction.End());
                yield break;
            }

            string systemPrompt = SystemPrompt(p);
            string userPrompt = UserPrompt(p);
            string body = BuildRequestBody(systemPrompt, userPrompt);
            string sent = systemPrompt + "\n\n---\n\n" + userPrompt;

            using (var req = new UnityWebRequest(_config.ChatCompletionsUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(_config.apiKey))
                    req.SetRequestHeader("Authorization", "Bearer " + _config.apiKey);
                req.timeout = Mathf.Max(1, _config.timeoutSeconds);

                LlmStatus.MarkWaiting("Thinking…");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"LlmBrain ({p.SelfId}): request failed — {req.error}.");
                    LlmStatus.MarkError("Connection failed");
                    LlmLog.Record(p.SelfId, sent, $"(request failed: {req.error})");
                    commit(AgentAction.End());
                    yield break;
                }

                string content = ExtractAssistantContent(req.downloadHandler.text);
                LlmLog.Record(p.SelfId, sent, content ?? req.downloadHandler.text);

                AgentAction action = ParseAction(content, p);
                if (action != null)
                    LlmStatus.MarkConnected("Ready");
                else
                {
                    LlmStatus.MarkError("Bad model response");
                    action = AgentAction.End();
                }
                commit(action);
            }
        }

        // ---- Prompt building ----------------------------------------------------

        private string BuildRequestBody(string systemPrompt, string userPrompt)
        {
            var reqBody = new ReqBody
            {
                model = _config.model,
                messages = new[]
                {
                    new ReqMessage { role = "system", content = systemPrompt },
                    new ReqMessage { role = "user", content = userPrompt },
                },
                temperature = _config.temperature,
                max_tokens = _config.maxTokens,
            };
            return JsonUtility.ToJson(reqBody);
        }

        private static string SystemPrompt(AgentPerception p)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"You are {p.SelfId}, an autonomous agent in a shared {p.WorldWidth}x{p.WorldHeight} tile world that also contains other agents. You are curious and social: explore, seek out others, communicate, and react.");
            sb.AppendLine();
            sb.AppendLine(ActionConfig.DictionaryText());
            sb.AppendLine($"Your ranges — move {p.MoveRange} (Manhattan), talk {p.TalkRange}, view {p.ObserveRange} (Chebyshev).");
            sb.Append("Reply with exactly ONE JSON object for your next action and nothing else.");
            return sb.ToString();
        }

        private static string UserPrompt(AgentPerception p)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Your position: ({p.SelfCoord.x}, {p.SelfCoord.y}). World bounds: x 0..{p.WorldWidth - 1}, y 0..{p.WorldHeight - 1}.");

            if (!string.IsNullOrEmpty(p.SelfBiome))
            {
                string desc = string.IsNullOrEmpty(p.SelfBiomeDescription) ? "" : $" — {p.SelfBiomeDescription}";
                sb.AppendLine($"Standing on: {p.SelfBiome}{desc}.");
            }

            // The automatic LOOK survey — label it clearly so the model knows it already has perception.
            sb.AppendLine("LOOK SURVEY (automatic — this is what you can currently see):");
            sb.AppendLine(p.NearbyBiomes != null && p.NearbyBiomes.Count > 0
                ? $"  Terrain in view: {string.Join(", ", p.NearbyBiomes)}."
                : "  Terrain in view: open, uniform terrain around you.");
            if (p.VisibleAgents.Count > 0)
            {
                var who = new StringBuilder();
                foreach (var v in p.VisibleAgents)
                {
                    if (who.Length > 0) who.Append(", ");
                    who.Append($"{v.Id} at ({v.Coord.x},{v.Coord.y})");
                }
                sb.AppendLine($"  Agents in view: {who}.");
            }
            else sb.AppendLine("  Agents in view: none.");

            if (p.HeardMessages.Count > 0)
            {
                sb.AppendLine("You just heard:");
                foreach (var h in p.HeardMessages)
                    sb.AppendLine($"  - {h.FromId}: \"{h.Text}\"");
            }

            sb.AppendLine(p.ActionsThisTurn.Count > 0
                ? $"Earlier this turn you already: {string.Join("; ", p.ActionsThisTurn)}."
                : "You have not acted yet this turn.");

            sb.AppendLine(p.AvailableActions.Count > 0
                ? $"Actions still available this turn: {string.Join(", ", p.AvailableActions)} (or 'end')."
                : "No actions remain — you must 'end'.");

            if (p.RecentHistory.Count > 0)
            {
                sb.AppendLine("Recent memory:");
                foreach (var r in p.RecentHistory)
                    sb.AppendLine($"  - R{r.round}: {r.action}");
            }

            sb.Append("Choose ONE action now as a single JSON object.");
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

            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            string json = content.Substring(start, end - start + 1);

            ActionJson aj;
            try { aj = JsonUtility.FromJson<ActionJson>(json); }
            catch { return null; }
            if (aj == null || string.IsNullOrEmpty(aj.action)) return null;

            switch (aj.action.Trim().ToLowerInvariant())
            {
                case "move":
                    var target = ResolveReachable(p, new GridCoord(aj.x, aj.y));
                    return target.HasValue ? AgentAction.Move(target.Value) : AgentAction.End();

                case "talk":
                    return string.IsNullOrWhiteSpace(aj.message)
                        ? AgentAction.End()
                        : AgentAction.Talk(aj.message.Trim());

                case "inspect":
                    int ix = Mathf.Clamp(aj.x, 0, Mathf.Max(0, p.WorldWidth - 1));
                    int iy = Mathf.Clamp(aj.y, 0, Mathf.Max(0, p.WorldHeight - 1));
                    return AgentAction.Inspect(new GridCoord(ix, iy));

                case "end":
                    return AgentAction.End();

                default:
                    // Includes "look" (automatic, not a valid reply) or anything unrecognized.
                    return null;
            }
        }

        /// <summary>
        /// Returns the requested tile if reachable; otherwise the reachable tile
        /// closest to it. Null only if the agent has nowhere to go.
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
        }
    }
}
