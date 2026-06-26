# Progress Log

> Running log of what's done and what's next. Newest at top.

## Status: Action rework, per-action LLM loop, LLM call log

### Actions
- Action set is now **Move, Talk, Inspect** (each costs an action), plus **Look** (free, automatic survey of visible tiles, always in the prompt) and **End** (stop the turn). The old "observe/note" became **Look**; **Inspect** is the new detailed, tile-specific look (basic detail now — biome + occupant — to expand later).
- Counts live in a config file: **`agent_actions.json`** (project root), loaded by `Sim/ActionConfig`. Each action has a `usesPerTurn` (default 1 for move/talk/inspect; 0 = free). The same file's descriptions + rules become the action dictionary sent to the LLM.

### Per-action turn loop
- A turn is no longer one batched plan. `TurnManager` now loops **one action per LLM call**: build perception (incl. remaining actions + what's been done this turn) → ask the brain for ONE action → execute → repeat until the budget is spent or the agent chooses **End**. `IAgentBrain.Decide` is back to committing a single `AgentAction`. State is rebuilt between actions, so the model reacts to changes. Each executed action is written to memory.
- `StubBrain` mimics the same loop with random valid actions / early end.

### LLM call log
- `Logging/LlmLog` records every call (sent prompt + response) to `<projectRoot>/LlmLog/llm_calls.jsonl` and keeps a capped in-memory list.
- `UI/LlmLogWindow` — a **top-right "LLM Log" button** (below the turn panel) opens a large, scrollable, auto-updating window of recent calls (newest first).

### Editor steps
- Add an empty GameObject with **LlmLogWindow** (for the log button/window).
- Optional: edit `agent_actions.json` to change per-turn action counts or the action descriptions sent to the model.
- Recompile; everything else is wired.

### Notes
- `agent_actions.json` is committed (shared config, no secrets). `LlmLog/` is git-ignored (runtime output).

---

## Status (prior): World generation + terrain + composite turns

### What changed
- **Biomes & world generation** — `World/Biome`, `BiomeMap`, `WorldGenerator`, and a `Generation/` pass pipeline (`RegionGrowthPass` = contiguous blobs within each biome's min/max size; `RiverPass` = meandering edge-to-edge water). `WorldGrid` auto-finds a `WorldGenerator`, colors tiles by biome (per-biome material cache), and falls back to the old checkerboard if none. `Tile` now stores its `Biome`. Seed 0 = fresh each run. Tunable entirely in the Inspector.
- **Terrain perception** — `AgentPerception` now carries `SelfBiome`, `SelfBiomeDescription`, and `NearbyBiomes`; `TurnManager.BuildPerception` fills them from tile biomes, and `LlmBrain` puts terrain in the prompt. Agents genuinely notice where they are.
- **Composite turns** — a turn is now **Move + Talk + Observe** (≤1 each, any order) instead of one action. `IAgentBrain.Decide` commits an `AgentTurn` (ordered `AgentAction` steps). Updated `StubBrain`, `LlmBrain` (prompt asks for `{"steps":[…]}`), `SelectableBrain`, and `TurnManager` (resolves steps in order — talk uses post-move position). Memory records one entry per turn (joined step summary + notes + `biome`).

### Editor steps
- Add the **WorldGenerator** component to the **World** object (the one with WorldGrid). Tune biomes/seed/rivers in the Inspector.
- Everything else: just let Unity recompile. No other hookup needed.

### Earlier (already committed)
- **Agent inspector** — click-select an agent (URP outline) + lower-left info panel; click empty space clears. (`Shaders/AgentOutline.shader`, `Agent.SetSelected`, `UI/AgentInspector`.)
- **Checkmark toggles** — HUD checkboxes show a ✓ glyph instead of a filled square.

---

## Status (prior): HUD — LLM status indicator + turn controls

### What the HUD does (current state)
Two panels are built at runtime by `UI/HudController` (Screen Space Overlay canvas; no editor setup required beyond adding a HUD GameObject with the component):

**Top-left panel — LLM status**
- Colored dot + short label: green "LLM: Ready", yellow "LLM: Thinking…", red "LLM: <short error>". Error strings are human-readable: `Config file missing`, `API key not set`, `Config missing baseUrl`, `Config file unreadable`, `Connection failed`, `Bad model response`. Full paths/exception text still go to the Console via `Debug.LogWarning`.
- "Use LLM" checkbox below the dot. Interactable in the green and red states; **locked** while a request is in flight (yellow), per spec.

**Top-right panel — turn controls**
- `▶ Next` button — triggers `TurnManager.StepRound()` (equivalent to pressing Space). Interactable only when *all three* are true: agents are idle, continuous toggle is off, and the LLM isn't blocked.
- Colored status text — "Agents working…" (yellow) while a round is running; "Agents ready" (green) between rounds.
- "Allow continuous actions" checkbox — writes back to `TurnManager.mode`. When on, rounds loop back-to-back and the Play button greys out.

Both panels sit flush in their corners (zero padding) and the EventSystem is created automatically (with `InputSystemUIInputModule` since the project uses the new Input System).

### Code surface added/changed
- **`Brains/LlmStatus`** — process-wide static broadcaster, three real states (`Connected` / `Waiting` / `Error`) + initial `Unknown`. Anything publishes via `MarkConnected / MarkWaiting / MarkError`; the HUD subscribes to `Changed`. Messages are intentionally short (≲ 24 chars) for the label.
- **`Brains/BrainSelector`** — process-wide static `UseLlm` flag with `Changed` event. The HUD's "Use LLM" toggle writes here; `SelectableBrain` reads here.
- **`Brains/SelectableBrain`** — per-agent wrapper that dispatches each `Decide(...)` based on `BrainSelector.UseLlm`:
  - `false` → stub brain (the safe default for "built-in intelligence")
  - `true` with a working LLM brain → LLM
  - `true` but no LLM brain available → **no-op Observe with note `[no LLM available]`**. We do *not* silently fall back to the stub — the user explicitly asked for LLM.
- **`LlmBrain`** — publishes `Waiting` before each request, `Connected` on success, `Error` on transport failure / parse failure / missing config. Existing fallback-to-observe safety remains so an individual failure mid-round doesn't stall the sim.
- **`Brains/LlmConfig`** — error strings rewritten to short user-facing labels (`"Config file missing"`, `"API key not set"`, etc.); the path / exception detail moved to `Debug.LogWarning` so it stays in the Console for debugging.
- **`Agents/AgentSpawner`** — always loads `llm.config.json` so the runtime toggle has both brains ready. Inspector `Brain Type` now only seeds the initial value of `BrainSelector.UseLlm`. Each agent gets a `SelectableBrain(llm, stub)`.
- **`Sim/TurnManager`** — exposes `public bool IsBusy` for the HUD. New `IsLlmBlocked()` gate added: when `BrainSelector.UseLlm && LlmStatus.Current == Error`, both `ContinuousLoop` and `StepRound` short-circuit — no rounds run, no decisions, no movement. As soon as the LLM recovers *or* "Use LLM" is unchecked, the loop resumes.
- **`UI/HudController`** — runtime canvas builder. Panels hardcoded to flush top corners. Creates an EventSystem if one isn't present. Polls TurnManager state in `Update()` to keep the agent status label and Play button enabled-state in sync.

### Editor steps for the HUD
1. Let Unity recompile.
2. Create an empty GameObject in the scene named **HUD** → Add Component → **HudController**. (One-time only; an `EventSystem` GameObject is created at runtime by the component if it's missing.)
3. Press **Play**. You should see both panels flush to the top corners.
   - Left: LLM status. **Green** = config loaded / last request succeeded. **Yellow** = a request is in flight; "Use LLM" locks. **Red** = config missing/invalid or last request failed; "Use LLM" stays selectable.
   - Right: turn controls. **`▶ Next`** to step manually, **"Allow continuous actions"** to loop rounds back-to-back. Agent status flips between yellow "working…" and green "ready".
4. Behavior to verify: ticking "Use LLM" when the LLM is red should *freeze* the sim (no agent movement) until either the LLM recovers or the toggle is flipped back off.

---

## Status (prior): Phase 6 (LLM brain) — code authored, awaiting setup + test

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
