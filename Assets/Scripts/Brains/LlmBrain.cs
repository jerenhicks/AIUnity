using System;
using System.Collections;
using System.Collections.Generic;
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

        public IEnumerator Decide(AgentPerception p, Action<AgentTurn> commit)
        {
            if (_config == null)
            {
                LlmStatus.MarkError("No config loaded");
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

                LlmStatus.MarkWaiting("Thinking…");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"LlmBrain ({p.SelfId}): request failed — {req.error}. Falling back to observe.");
                    LlmStatus.MarkError("Connection failed");
                    commit(Fallback("request failed"));
                    yield break;
                }

                string content = ExtractAssistantContent(req.downloadHandler.text);
                AgentTurn plan = ParsePlan(content, p);
                if (plan != null)
                    LlmStatus.MarkConnected("Ready");
                else
                    LlmStatus.MarkError("Bad model response");
                commit(plan ?? Fallback("could not parse response"));
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
                "You act one turn at a time. In a single turn you may take up to THREE steps — at most one move, one talk, and one observe — in any order you choose:\n" +
                $"- move: walk to a tile within Manhattan distance {p.MoveRange} of your position (cardinal steps only); you cannot enter a tile occupied by another agent.\n" +
                $"- talk: shout a short message; only agents within {p.TalkRange} tiles hear it. Order matters — talking before vs after moving reaches different agents.\n" +
                "- observe: note something about the terrain or your surroundings; this is recorded to your memory.\n" +
                $"You can see other agents and terrain within {p.ObserveRange} tiles. You are curious and social: explore, seek out others, communicate, and react to what they say and do.\n" +
                "Respond with ONLY a JSON object of this exact shape and no other text. Include only the steps you want, in the order you intend them to happen:\n" +
                "{\"steps\":[" +
                "{\"action\":\"move\",\"x\":<int>,\"y\":<int>,\"note\":\"<short reason>\"}," +
                "{\"action\":\"talk\",\"message\":\"<what you say>\",\"note\":\"<short reason>\"}," +
                "{\"action\":\"observe\",\"note\":\"<what you notice>\"}" +
                "]}";
        }

        private static string UserPrompt(AgentPerception p)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Your position: ({p.SelfCoord.x}, {p.SelfCoord.y}).");
            sb.AppendLine($"World bounds: x 0..{p.WorldWidth - 1}, y 0..{p.WorldHeight - 1}. Move range: {p.MoveRange} (Manhattan).");

            if (!string.IsNullOrEmpty(p.SelfBiome))
            {
                string desc = string.IsNullOrEmpty(p.SelfBiomeDescription) ? "" : $" — {p.SelfBiomeDescription}";
                sb.AppendLine($"You are standing on: {p.SelfBiome}{desc}.");
            }
            if (p.NearbyBiomes != null && p.NearbyBiomes.Count > 0)
                sb.AppendLine($"Nearby terrain: {string.Join(", ", p.NearbyBiomes)}.");

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

        private static AgentTurn ParsePlan(string content, AgentPerception p)
        {
            if (string.IsNullOrEmpty(content)) return null;

            // The model may wrap JSON in prose or code fences; grab the object.
            int start = content.IndexOf('{');
            int end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            string json = content.Substring(start, end - start + 1);

            PlanJson pj;
            try { pj = JsonUtility.FromJson<PlanJson>(json); }
            catch { return null; }
            if (pj == null || pj.steps == null || pj.steps.Length == 0) return null;

            var turn = new AgentTurn();
            var seenTypes = new HashSet<string>();

            foreach (var s in pj.steps)
            {
                if (s == null || string.IsNullOrEmpty(s.action)) continue;
                string kind = s.action.Trim().ToLowerInvariant();
                if (seenTypes.Contains(kind)) continue; // at most one of each type

                AgentAction step = kind switch
                {
                    "move" => BuildMove(s, p),
                    "talk" => string.IsNullOrWhiteSpace(s.message) ? null : AgentAction.Talk(s.message.Trim()),
                    "observe" => AgentAction.Observe(),
                    _ => null,
                };
                if (step == null) continue;

                step.Note = s.note;
                seenTypes.Add(kind);
                turn.Add(step);
            }

            return turn.IsEmpty ? null : turn;
        }

        private static AgentAction BuildMove(StepJson s, AgentPerception p)
        {
            var target = ResolveReachable(p, new GridCoord(s.x, s.y));
            return target.HasValue ? AgentAction.Move(target.Value) : null;
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

        private static AgentTurn Fallback(string why)
        {
            var a = AgentAction.Observe();
            a.Note = $"[fallback: {why}]";
            return AgentTurn.Of(a);
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

        [Serializable] private class PlanJson { public StepJson[] steps; }

        [Serializable]
        private class StepJson
        {
            public string action;
            public int x;
            public int y;
            public string message;
            public string note;
        }
    }
}
