# AI Sandbox World — Game Design

> Living design doc. This is the source of truth for what the world *is*. Update it as the vision changes.

## Vision

A small isometric world where multiple AI-driven agents live, move, observe, and talk to one another. The goal is observational: watch how independent agents interact and what behaviors/structures emerge over time. Actions expand later; the world stays simple and legible.

## The World

- 3D scene viewed through an **orthographic isometric camera** (fixed angle, no perspective distortion).
- The world is a grid of **tiles**. One tile = one discrete location an agent can occupy and act within.
- Grid is square (N×N) to start. Coordinates are integer `(x, y)` where `x` runs east, `y` runs north. Tiles sit on the ground plane (XZ); height (Y) is reserved for later structures.
- Each tile knows its coordinate and its **biome** (terrain type). Tiles are flat colored slabs for now; height, mesh variation, and doodads can layer on later without changing the logic.

## World Generation

The world is generated from a tunable, rule-based pipeline (see ARCHITECTURE.md for the code). Each **biome** is data: a name, a flat color, a description (surfaced to agents), and generation parameters (weight, min/max region size). Generation runs an ordered list of **passes**, so new rules are added by writing another pass:

- **Region growth** — seeds and grows contiguous biome "blobs," each sized within that biome's min/max. This is how biomes cluster (deserts, plains, mountains, forest) and where size rules live.
- **Rivers** — carve a continuous, meandering water path (currently edge-to-edge), cutting through whatever they cross.

A seed makes a world reproducible (seed 0 = fresh each run). Default biomes: Plains, Forest, Desert, Mountains, plus Water for rivers.

## Agents

Each agent occupies exactly one tile and has a **stat block**. Each stat is an integer **1–6**.

| Stat | Range | Meaning |
|------|-------|---------|
| Move | 1–6 | Max number of tiles the agent can move in one turn. |
| Observe | 1–6 | Radius (in tiles) within which the agent can see what other agents are doing. |
| Talk | 1–6 | Radius (in tiles) the agent can "shout" — other agents within range hear the message. |

Distance metrics (resolved):
- **Movement = Manhattan** (cardinal steps only, no diagonals). Move stat = max tiles stepped per turn. Agents physically walk through tile centers, one orthogonal step at a time.
- **Observe / Talk = Chebyshev** radius (a square area N tiles around the agent).

## Actions (v1)

A **turn** is composite: an agent may take up to **one Move, one Talk, and one Observe**, in any order it chooses. Order matters — talking before vs. after moving reaches different agents.

1. **Move** — travel up to `Move` tiles to a chosen reachable tile.
2. **Talk** — emit a message heard by agents within `Talk` radius.
3. **Observe** — note something about the surroundings/terrain; the note is saved to the agent's memory.

Terrain is **always perceived** (the biome underfoot + nearby biomes within Observe range), so it isn't itself an action — Observe is the agent deliberately *recording* an observation.

Future actions (not built yet): build, gather, store, modify terrain, etc. Architecture must let new actions slot in without rework.

## Agent Memory / Context Files

Each agent maintains its **own context file** — a running log of what it has done, seen, and heard. This is the agent's memory.

- One file per agent (e.g. `AgentMemory/agent_<id>.md` or `.json`).
- Updated by the agent after each turn: action taken, outcome, observations, messages heard.
- This file is what a future LLM brain reads as context to decide its next action, and writes back to after acting.
- Format TBD — leaning JSON for machine read + a human-readable mirror. Decide in ARCHITECTURE.md.

## Turn Model

- **Turn-based, strictly sequential.** Agents act one at a time in a stable order. The manager waits for an agent's brain to fully decide (a slow LLM call is fine) and for its whole turn to finish (including walk animation) before the next agent acts. This avoids overlap, lag, and runaway LLM costs.
- Each turn the brain returns an ordered set of steps (≤1 Move, ≤1 Talk, ≤1 Observe), resolved in order.
- Continuous mode loops rounds back-to-back; Manual mode advances one round per Space press.

## AI Brain

- v1 uses a **stub brain** behind an `IAgentBrain` interface (picks a valid action via simple rules/randomness).
- The interface is designed so a real **LLM brain** drops in later with no changes to world/agent code: brain receives the agent's context + local observation, returns a chosen action.

## Resolved Decisions

- **Distance:** Movement = Manhattan (cardinal steps); Observe/Talk = Chebyshev radius.
- **Turn model:** sequential, one agent fully resolved at a time; each turn is composite (Move/Talk/Observe).
- **Memory format:** JSON (canonical) + a human-readable `.md` mirror, per agent.
- **Talk delivery:** messages land in a per-agent inbox, heard on the listener's next turn.
- **World:** bounded N×M grid (does not wrap), biome-generated.

## Open Questions

- Should terrain/elevation eventually affect movement cost or passability?
- Do doodads (trees, rocks) block tiles, or stay decorative?
- Per-turn cost/energy to gate how much an agent does, or keep ≤1 of each action?
- Rules for biome adjacency (e.g., forests favor water, deserts avoid mountains)?
