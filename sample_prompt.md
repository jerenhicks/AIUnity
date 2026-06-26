# Sample LLM Prompt

What the game sends the model on a single action call, via an OpenAI-compatible chat
request: one **system** message (built from `agent_actions.json` + the agent's ranges)
and one **user** message (rebuilt every action with live state). The model replies with
a single JSON action — **no `note`/explanation field**, and it must never reply with
`look` (that's automatic perception, shown in every request).

Filled-in example: **Ava** (Move 4 / Observe 3 / Talk 2) on plains beside the river,
with Bjorn nearby, mid-simulation. `model`/`temperature`/`max_tokens` come from
`llm.config.json` and are request parameters, not message content.

---

## System message

```
You are Ava, an autonomous agent in a shared 10x10 tile world that also contains other agents. You are curious and social: explore, seek out others, communicate, and react.

You control one agent in a shared isometric tile world that also contains other agents. Each turn you may take a limited number of actions. You are asked for ONE action at a time; after each action the world updates and you are asked again with your remaining actions for the turn. You may stop at any point by choosing the 'end' action — you do not have to use every action. Reply with a single JSON object describing one action and nothing else: no explanation, no extra text.

PERCEPTION (always provided automatically in each request — never reply with these):
- look: A high-level survey of every tile within your view range (its terrain and any agents on it). This is ALWAYS included in each request automatically — you never request it and you must never reply with 'look'.

ACTIONS YOU MAY CHOOSE (reply with exactly ONE of these):
- move (up to 1/turn): Walk to a tile within your move range (Manhattan distance, cardinal steps only). You cannot move onto a tile occupied by another agent. JSON: {"action":"move","x":<int>,"y":<int>}
- talk (up to 1/turn): Say a short message aloud. Only agents within your talk range will hear it. JSON: {"action":"talk","message":"<text>"}
- inspect (up to 1/turn): Examine one specific tile within your view in detail (its terrain and what is on it). JSON: {"action":"inspect","x":<int>,"y":<int>}
- end: End your turn immediately without taking any further action. JSON: {"action":"end"}

Valid "action" values: move, talk, inspect, end. Reply with a single JSON object and no other text.
Your ranges — move 4 (Manhattan), talk 2, view 3 (Chebyshev).
Reply with exactly ONE JSON object for your next action and nothing else.
```

---

## User message — first action of the turn

```
Your position: (5, 4). World bounds: x 0..9, y 0..9.
Standing on: Plains — open grassy plains.
LOOK SURVEY (automatic — this is what you can currently see):
  Terrain in view: Water, Desert.
  Agents in view: Bjorn at (6,5).
You just heard:
  - Bjorn: "Found the river, plenty of water over here."
You have not acted yet this turn.
Actions still available this turn: move, talk, inspect (or 'end').
Recent memory:
  - R3: Move→(4, 4)
  - R3: Talk:"Exploring east."
  - R4: Move→(5, 4)
Choose ONE action now as a single JSON object.
```

### Example valid replies (no note field)
```json
{"action":"talk","message":"Hi Bjorn, I'm right behind you."}
```
```json
{"action":"move","x":6,"y":4}
```
```json
{"action":"inspect","x":6,"y":5}
```
```json
{"action":"end"}
```

---

## User message — second action (same turn, after Ava talked)

"talk" has dropped from the available list; the action is now in memory.

```
Your position: (5, 4). World bounds: x 0..9, y 0..9.
Standing on: Plains — open grassy plains.
LOOK SURVEY (automatic — this is what you can currently see):
  Terrain in view: Water, Desert.
  Agents in view: Bjorn at (6,5).
Earlier this turn you already: Talk:"Hi Bjorn, I'm right behind you.".
Actions still available this turn: move, inspect (or 'end').
Recent memory:
  - R3: Talk:"Exploring east."
  - R4: Move→(5, 4)
  - R4: Talk:"Hi Bjorn, I'm right behind you."
Choose ONE action now as a single JSON object.
```

---

## Notes

- **`look` is not selectable.** It's automatic perception, shown in every request under
  "LOOK SURVEY". The model must reply only with move / talk / inspect / end. If it returns
  anything else (including `look`), the agent safely ends its turn.
- **No `note` field.** The model returns just the action; the action itself is the record
  written to memory.
- **Look is biome-level.** Nearby biome names, not a tile-by-tile map. Inspect pulls
  detail on a specific tile. Tell me if you want the survey to include directions/coordinates.
- The on-screen LLM Log window shows the exact prompt and raw reply for every call.
