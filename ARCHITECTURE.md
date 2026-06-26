# Architecture & Build Plan

> How the code is organized and the order we build it. Update as structure evolves.

## Target

- **Unity 6.3 LTS** (6000.3.9f1), 3D project template, URP.
- Rendering: 3D meshes + **orthographic camera** angled for an isometric look.
- Language: C#. New Input System package (TurnManager, IsoCameraController, and HUD all use it).

## Script Folder Layout (under `Assets/Scripts/`)

```
Assets/Scripts/
  World/        Grid, tiles, coordinates, world state, generation
    GridConfig.cs        Tile size + grid dimensions
    GridCoord.cs         Integer (x, y) + Chebyshev/Manhattan helpers
    Tile.cs              One tile; tracks its occupant and biome
    WorldGrid.cs         Spawns the visible grid; colors tiles by biome; tile lookup
    ITileOccupant.cs     Anything that sits on a tile
    MaterialUtil.cs      Shared material helpers
    Biome.cs             Terrain type data (name, color, description, size rules)
    BiomeMap.cs          Generator's per-coordinate biome canvas
    WorldGenerator.cs    Runs the generation pass pipeline; returns a BiomeMap
    Generation/
      IWorldGenPass.cs   One generation rule (mutates the BiomeMap)
      RegionGrowthPass.cs  Contiguous biome blobs within min/max size
      RiverPass.cs         Meandering connected water path

  Agents/       Agent entity, stats, spawning
    AgentStats.cs        Move / Observe / Talk ranges (1–6)
    Agent.cs             The agent: position, brain slot, memory slot, walk animation
    AgentSpawner.cs      Builds agents at Play; wires brains + memory

  Brains/       Decision-making
    IAgentBrain.cs       Coroutine-based decision interface (LLM-ready seam)
    StubBrain.cs         Picks a valid random action
    LlmBrain.cs          Coroutine brain talking to an OpenAI-compatible endpoint
    LlmConfig.cs         Loads/validates llm.config.json
    LlmStatus.cs         Static broadcaster: Connected / Waiting / Error
    BrainSelector.cs     Static UseLlm flag with Changed event
    SelectableBrain.cs   Per-agent wrapper that dispatches LLM vs stub

  Sim/          Turn loop, perception, action data
    AgentAction.cs       A single action (Move / Talk / Inspect / End) + factory
    ActionConfig.cs      Loads agent_actions.json: per-turn budget + LLM action dictionary
    AgentPerception.cs   Read-only snapshot a brain decides from (terrain, available actions, this-turn history)
    TurnManager.cs       Turn-based driver; per-action budget loop; Inspect; IsBusy; LLM gate

  Logging/      LLM call logging
    LlmLog.cs            Global record of every call (sent + response); file + in-memory + event

  Memory/       Per-agent context file I/O
    AgentMemory.cs       Writes per-agent JSON + .md every turn
    TurnRecord.cs        One turn's record (action, position, observed, heard, note)

  View/         Camera + visual controls
    IsoCameraController.cs   WASD/MMB pan, scroll zoom, Q/E rotate

  UI/           Runtime HUD + agent inspector
    HudController.cs     Builds canvas + both panels at Play; no editor setup
    AgentInspector.cs    Click-select an agent (outline) + lower-left info panel
    LlmLogWindow.cs      Top-right "LLM Log" button + large auto-updating call viewer
```

Config files at project root: `llm.config.json` (secret, git-ignored), `agent_actions.json` (action rulebook + per-turn budget, committed).

Plus `Assets/Shaders/AgentOutline.shader` — a URP inverted-hull outline used by the selection highlight (`Agent.SetSelected`).

## Core Design Principles

- **World code knows nothing about brains.** Agents ask an `IAgentBrain` for an action; the brain is swappable (stub, LLM, or a selectable wrapper around both).
- **Actions are data + a resolver.** An action is a small object describing intent (Move-to-tile, Talk-message, Observe). A resolver applies it to world state. Adding a new action = new action type + resolver branch, no changes elsewhere.
- **Coordinates are the contract.** Everything addresses the world by integer `GridCoord(x, y)`. Visual world position is derived from coordinate, never the reverse.
- **UI talks to the sim through static broadcasters, not direct references.** `Brains/LlmStatus` and `Brains/BrainSelector` are static publishers/flags. The HUD subscribes to them and writes to them; brains and the spawner read/write them as needed. The HUD's only direct reference is to `TurnManager` (for the Play button and busy-polling), and that's a one-way read/control of the sim's *driver*, not of agents themselves.
- **The LLM is opt-in, and "broken LLM" is an honest state.** When the user has "Use LLM" on but the LLM is unavailable, `SelectableBrain` does not silently fall back to the stub, and `TurnManager` pauses rounds entirely (`IsLlmBlocked()` gate). The sim freezes until either the LLM recovers or the user opts back into the stub.

## Build Phases

- **Phase 0 — Project + Camera** *(done)*: Unity 6.3 LTS 3D project, orthographic iso camera, light.
- **Phase 1 — Tile Grid World** *(done)*: `GridConfig`, `GridCoord`, `Tile`, `WorldGrid`. 10×10 board renders.
- **Phase 2 — Agent + Stats** *(done)*: `AgentStats`, `Agent`, `AgentSpawner`, tile occupancy. Two agents spawn and render.
- **Phase 3 — Turn loop + Stub Brain** *(done)*: `IAgentBrain`, `StubBrain`, `Sim/TurnManager` (replaced the original `TickManager`). Agents take random valid actions each turn.
- **Phase 4 — Actions (Move / Observe / Talk)** *(done)*: Real resolution honoring stat ranges + Chebyshev/Manhattan distance. Animated cardinal-only walks.
- **Phase 5 — Memory / Context Files** *(done)*: Each agent reads/writes its own context file every turn (JSON + .md mirror in `<projectRoot>/AgentMemory/`).
- **Phase 6 — LLM Brain** *(code authored; awaiting real-LLM connection)*: `LlmConfig`, `LlmBrain`. OpenAI-compatible chat endpoint, strict-JSON action parsing, safe-fallback to Observe on any error.
- **Phase 6.5 — HUD + runtime brain switching** *(done)*: `LlmStatus`, `BrainSelector`, `SelectableBrain`, `UI/HudController`. Two-panel runtime HUD: LLM status indicator + Use LLM toggle on the left; Play button, agent status, and continuous toggle on the right. `TurnManager` pauses when LLM is selected-and-errored.
- **Phase 6.6 — Agent inspector** *(done)*: `Shaders/AgentOutline.shader`, `Agent.SetSelected`, `UI/AgentInspector`. Left-click selects an agent (outline) and shows a lower-left info panel; clicking empty space clears it.
- **Phase 7 — Biomes + world generation** *(done)*: `Biome`, `BiomeMap`, `WorldGenerator`, `Generation/` pass pipeline (region growth + rivers). `WorldGrid` colors tiles by biome; `Tile` stores its biome. Flat colors for now; height/doodads deferred.
- **Phase 8 — Terrain perception + composite turns** *(done)*: `AgentPerception` carries the biome underfoot + nearby biomes; the LLM prompt includes terrain. Turns became composite — the brain returns an `AgentTurn` (≤1 Move + ≤1 Talk + ≤1 Observe, ordered); `TurnManager` resolves the steps in order and records one memory entry per turn (with biome).
- **Phase 9 — Action rework + per-action LLM loop + call log** *(done)*: actions = Move/Talk/Inspect (+free Look, +End), counts from `agent_actions.json` via `ActionConfig`. `TurnManager` asks the brain for one action at a time until the budget is spent or the agent ends; `Logging/LlmLog` + `UI/LlmLogWindow` record and display every call.
- **Phase 10+ — Connect a real LLM and expand** *(next)*: point `llm.config.json` at a real endpoint, watch the LLM Log window, flesh out Inspect detail, then add new action types (build, gather, …) and richer tile visuals (height, doodads).

## Coordinate ↔ World Position

For a flat XZ grid with tile size `s`: world position of `(x, y)` = `(x * s, 0, y * s)`. The *isometric look* comes from the camera angle, not from skewing tiles — this keeps math trivial and lets us add real 3D height later.

## SETUP (Phase 0 steps live here)

See PROGRESS.md for the running click-by-click. Summary:
1. Unity Hub → New Project → Universal 3D (or Built-in 3D) → Unity 6.3 LTS.
2. Camera: Orthographic; Rotation ~ (30, 45, 0); position pulled back; size ~5–8.
3. Add a Directional Light (default scene has one).
4. After Phase 6.5: add an empty GameObject named **HUD** with `HudController`. No other UI editor setup needed.
