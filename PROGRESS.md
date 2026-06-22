# Progress Log

> Running log of what's done and what's next. Newest at top.

## Status: Phase 6 (LLM brain) — code authored, awaiting setup + test

### Phase 6 — the LLM brain
- `Brains/LlmConfig` — loads `llm.config.json` (project root, git-ignored): `baseUrl`, `apiKey`, `model`, `temperature`, `maxTokens`, `timeoutSeconds`. OpenAI-compatible format (OpenAI, OpenRouter, Together, LM Studio, local Ollama `/v1`, …).
- `Brains/LlmBrain` — coroutine `IAgentBrain`: builds a system+user prompt from perception + recent memory, POSTs to `{baseUrl}/chat/completions` via UnityWebRequest, parses a strict-JSON action, validates the move against reachable tiles (snaps to nearest if slightly off), and falls back to a safe `observe` on any error so the sim never stalls. The brain's reasoning lands in memory via `AgentAction.Note`.
- `AgentSpawner` has a **Brain Type** toggle (Stub / Llm); Llm loads the config once and auto-falls-back to Stub if the config is missing/placeholder.
- `AgentPerception` now includes world bounds so the model knows the board edges.
- Added `.gitignore` (ignores `llm.config.json`, `AgentMemory/`, Unity junk) and `llm.config.example.json` (committed template).

### Setup + test for Phase 6
1. Edit `AI-Unity/llm.config.json`: set `baseUrl`, `apiKey` (empty is fine for most local servers), and `model`.
2. Select the **Agents** object → set **Brain Type = Llm**.
3. On **TurnManager**, set **Mode = Manual** for the first test (press **Space** = one round) so you control how many API calls happen, and a small bill. Increase `Pause Between Turns` if needed.
4. Press **Play**, press **Space** a few times. Watch the Console (each agent's action) and open `AgentMemory/agent_*.md` to see the `Note` reasoning the model wrote. Switch Mode to Continuous once it looks good.

> Cost/perf note: one API call per agent per round. Manual mode + a cheap/fast model keeps it controllable. Turn-based execution means nothing overlaps — each agent fully finishes before the next calls the model.

---

## Status (prior): Phase 5 (Agent memory / context files)

### Phase 5 — per-agent memory
- `Memory/TurnRecord` — one turn's entry (round, action, end position, what was seen/heard, optional note) + `AgentMemoryData` (identity, stats, history).
- `Memory/AgentMemory` — writes each agent's context file as it acts: canonical **JSON** + a readable **.md** mirror, in `<projectRoot>/AgentMemory/` (outside Assets, so no reimport).
- Wired in: `Agent.Memory` slot; spawner creates it (toggle `Write Memory Files`, optional dir override); `TurnManager` feeds each agent its own recent history into `AgentPerception.RecentHistory` and appends a `TurnRecord` after every action. `AgentAction.Note` lets a brain attach reasoning that lands in memory.
- This is the context the LLM brain will read in/write back (Phase 6).

### Editor test for Phase 5
1. Let Unity recompile, press **Play**, let it run a bit, then **Stop**.
2. Open the folder `AI-Unity/AgentMemory/` — you should see `agent_Ava.json` / `agent_Ava.md` (and Bjorn's). The `.md` reads like a diary of each round. The Console also logs the exact folder path on spawn.
3. Confirm the files fill in over rounds, then we're set for **Phase 6: the LLM brain**.

---

## Status (prior): Mid-project adjustments

### Adjustments just made (camera / turns / movement)
- **Camera controls** — `View/IsoCameraController` (new Input System): WASD/arrows or middle-mouse drag to pan, scroll to zoom, Q/E to rotate 90°.
- **Turn-based** — replaced timer-based `TickManager` with `Sim/TurnManager`: agents act one at a time, fully awaiting each brain decision + walk animation before the next. Continuous mode (auto rounds) or Manual mode (Space = one round). **TickManager.cs was deleted** — if you added a TickManager component to the Sim object, remove it and add TurnManager.
- **Animated movement** — agents now walk between tile centers in cardinal steps only (no diagonal sliding), via `Agent.MoveTo`. Movement reachability switched to **Manhattan** to match; Observe/Talk stay Chebyshev.

### Editor steps for these adjustments
1. Let Unity recompile.
2. Select **Main Camera** → Add Component → **IsoCameraController**. (Keep its existing orthographic/rotation setup; the controller reads and drives from there.)
3. On the **Sim** object: ensure it has **TurnManager** (not the old TickManager). Leave mode = Continuous to watch it run, or set Manual and press Space to step.
4. Press **Play**: agents should walk smoothly tile-to-tile, one at a time; the camera should pan/zoom/rotate. Report back.

---

## Status (prior): Phase 3 (Tick loop + Stub brain)

### Done
- **Phase 0** — Unity 6.3 LTS (6000.3.9f1) Universal 3D project created; orthographic iso camera set up. Verified.
- **Phase 1** — Tile grid world working: `GridConfig`, `GridCoord`, `Tile`, `WorldGrid`. 10×10 board renders. Verified.
- **Phase 2** — Agents on tiles with 1–6 stat blocks: `AgentStats`, `Agent`, `AgentSpawner`, plus `ITileOccupant`/`MaterialUtil` and tile occupancy. Two agents (Ava, Bjorn) spawn and render. Verified.
- **Phase 3 (code)** — Authored the simulation loop:
  - `Sim/AgentAction` — Move / Observe / Talk intent (data + factory).
  - `Sim/AgentPerception` — read-only snapshot a brain decides from (plain data, LLM-serializable).
  - `Brains/IAgentBrain` — **coroutine-based** decision interface; an async LLM brain drops in with zero refactor.
  - `Brains/StubBrain` — picks valid random actions now.
  - `Sim/TickManager` — per tick: build perception → ask brain → resolve action. Sequential, stable order. Talk uses a per-agent inbox heard next turn.
  - Agent gained a `Brain` slot; spawner assigns a `StubBrain` per agent.

### Next up (your move, in the Editor)
1. Let Unity recompile.
2. Create an empty GameObject named **Sim** → add the **TickManager** component.
3. Press **Play**. Agents should wander tile-by-tile every ~1s; open the **Console** to see their actions (moved / said / observed). Talk only reaches agents within Talk range, so they need to get close before messages land.
   - Tune `Tick Interval`, toggle `Auto Run`, or turn off `Verbose Log` in the Inspector.
4. Report back, then **Phase 5 (per-agent memory/context files)** — Phase 4's core (movement + ranged talk/observe) is already in TickManager, so we'll fold remaining action polish into Phase 5/6.

### Notes
- Decision interface is intentionally coroutine-based — that is the LLM-ready seam.
- Stat block: Move / Observe / Talk, each 1–6. Distance = Chebyshev (diagonals = 1).
- Brain: stub now, `IAgentBrain` LLM-ready interface (Phase 3).
- Render: 3D + orthographic iso camera.
- All scripts compile into the default Assembly-CSharp (no asmdef yet).
