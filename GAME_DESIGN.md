# AI Sandbox World — Game Design

> Living design doc. This is the source of truth for what the world *is*. Update it as the vision changes.

## Vision

A small isometric world where multiple AI-driven agents live, move, observe, and talk to one another. The goal is observational: watch how independent agents interact and what behaviors/structures emerge over time. Actions expand later; the world stays simple and legible.

## The World

- 3D scene viewed through an **orthographic isometric camera** (fixed angle, no perspective distortion).
- The world is a grid of **tiles**. One tile = one discrete location an agent can occupy and act within.
- Grid is square (N×N) to start. Coordinates are integer `(x, y)` where `x` runs east, `y` runs north. Tiles sit on the ground plane (XZ); height (Y) is reserved for later structures.
- Each tile knows its coordinate and (later) its contents/terrain type.

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

Only three actions exist right now. Each agent takes one action per turn.

1. **Move** — travel up to `Move` tiles to a chosen reachable tile.
2. **Observe** — perceive other agents and their recent actions within `Observe` radius.
3. **Talk** — emit a message heard by agents within `Talk` radius.

Future actions (not built yet): build, gather, store, modify terrain, etc. Architecture must let new actions slot in without rework.

## Agent Memory / Context Files

Each agent maintains its **own context file** — a running log of what it has done, seen, and heard. This is the agent's memory.

- One file per agent (e.g. `AgentMemory/agent_<id>.md` or `.json`).
- Updated by the agent after each turn: action taken, outcome, observations, messages heard.
- This file is what a future LLM brain reads as context to decide its next action, and writes back to after acting.
- Format TBD — leaning JSON for machine read + a human-readable mirror. Decide in ARCHITECTURE.md.

## Turn Model

- **Turn-based, strictly sequential.** Agents act one at a time in a stable order. The manager waits for an agent's brain to fully decide (a slow LLM call is fine) and for its action to finish (including the walk animation) before the next agent acts. This avoids overlap, lag, and runaway LLM costs.
- Continuous mode loops rounds back-to-back; Manual mode advances one round per Space press.

## AI Brain

- v1 uses a **stub brain** behind an `IAgentBrain` interface (picks a valid action via simple rules/randomness).
- The interface is designed so a real **LLM brain** drops in later with no changes to world/agent code: brain receives the agent's context + local observation, returns a chosen action.

## Open Questions

- Distance metric: Chebyshev vs Manhattan?
- Tick model: simultaneous vs sequential resolution?
- Memory format: JSON, Markdown, or both?
- World size and whether it's bounded or wraps.
- How talk messages are stored/delivered (queue per agent?).
