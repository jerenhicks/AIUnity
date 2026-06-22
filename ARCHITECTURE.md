# Architecture & Build Plan

> How the code is organized and the order we build it. Update as structure evolves.

## Target

- **Unity 6.3 LTS** (current LTS as of June 2026), 3D project template.
- Rendering: 3D meshes + **orthographic camera** angled for an isometric look.
- Language: C#.

## Script Folder Layout (under `Assets/Scripts/`)

```
Assets/Scripts/
  World/        Grid, tiles, coordinates, world state
    GridConfig.cs
    GridCoord.cs
    Tile.cs
    WorldGrid.cs
  Agents/       Agent entity, stats, memory   (built in Phase 2)
    AgentStats.cs
    Agent.cs
  Brains/       Decision-making                (built in Phase 3)
    IAgentBrain.cs
    StubBrain.cs
  Sim/          Tick loop, action resolution   (built in Phase 3-4)
    TickManager.cs
    Actions/
  Memory/       Per-agent context file I/O     (built in Phase 5)
    AgentMemory.cs
```

## Core Design Principles

- **World code knows nothing about brains.** Agents ask an `IAgentBrain` for an action; the brain is swappable (stub now, LLM later).
- **Actions are data + a resolver.** An action is a small object describing intent (Move-to-tile, Talk-message, Observe). A resolver applies it to world state. Adding a new action = new action type + resolver branch, no changes elsewhere.
- **Coordinates are the contract.** Everything addresses the world by integer `GridCoord(x, y)`. Visual world position is derived from coordinate, never the reverse.

## Build Phases

- **Phase 0 — Project + Camera** *(Jeren, in Editor)*: Create the Unity 6.3 3D project, set up the orthographic iso camera + a light. See SETUP below.
- **Phase 1 — Tile Grid World** *(now)*: `GridConfig`, `GridCoord`, `Tile`, `WorldGrid`. Spawns a visible N×N grid of tiles. **Milestone: you can see the isometric world.**
- **Phase 2 — Agent + Stats**: An `Agent` that occupies a tile, with a 1–6 stat block, rendered as a marker. Place a couple by hand.
- **Phase 3 — Tick Loop + Stub Brain**: `IAgentBrain`, `StubBrain`, `TickManager`. Agents take random valid actions each tick.
- **Phase 4 — Actions (Move / Observe / Talk)**: Real resolution honoring stat ranges + Chebyshev distance.
- **Phase 5 — Memory / Context Files**: Each agent reads/writes its own context file every turn.
- **Phase 6+ — LLM Brain**: Implement `IAgentBrain` backed by a real model. New actions (build, gather, …).

## Coordinate ↔ World Position

For a flat XZ grid with tile size `s`: world position of `(x, y)` = `(x * s, 0, y * s)`. The *isometric look* comes from the camera angle, not from skewing tiles — this keeps math trivial and lets us add real 3D height later.

## SETUP (Phase 0 steps live here)

See the chat message / PROGRESS.md for the exact click-by-click. Summary:
1. Unity Hub → New Project → Universal 3D (or Built-in 3D) → Unity 6.3 LTS.
2. Camera: Orthographic; Rotation ~ (30, 45, 0); position pulled back; size ~5–8.
3. Add a Directional Light (default scene has one).
