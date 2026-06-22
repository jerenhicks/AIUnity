using System;
using System.Collections;
using System.Collections.Generic;
using AISandbox.Brains;
using AISandbox.Memory;
using AISandbox.World;
using UnityEngine;

namespace AISandbox.Agents
{
    /// <summary>
    /// A single inhabitant of the world. Occupies exactly one tile, carries a
    /// 1-6 stat block, and renders a colored marker. Movement/decision logic
    /// arrives in later phases; for now an Agent can be placed on a tile.
    /// </summary>
    public class Agent : MonoBehaviour, ITileOccupant
    {
        public string AgentId { get; private set; }
        public AgentStats Stats { get; private set; }
        public GridCoord Coord { get; private set; }

        /// <summary>Decision-maker for this agent (StubBrain now, LLM later).</summary>
        public IAgentBrain Brain { get; set; }

        /// <summary>This agent's context file / running history (may be null if disabled).</summary>
        public AgentMemory Memory { get; set; }

        public string DisplayName => AgentId;

        private WorldGrid _grid;
        private Transform _marker;

        /// <summary>Height of the marker above the tile surface.</summary>
        private const float MarkerScale = 0.45f;

        public void Init(WorldGrid grid, string id, GridCoord coord, AgentStats stats, Color color)
        {
            _grid = grid;
            AgentId = id;
            Stats = stats ?? new AgentStats();
            name = $"Agent {id}";

            BuildMarker(color);

            if (!PlaceAt(coord))
            {
                Debug.LogWarning($"Agent {id}: start coord {coord} is invalid or occupied.");
            }
        }

        /// <summary>
        /// Instantly places the agent on a tile (used for initial spawn).
        /// Returns false if the tile is out of bounds or taken.
        /// </summary>
        public bool PlaceAt(GridCoord coord)
        {
            if (!ClaimTile(coord)) return false;
            transform.position = MarkerWorldPosition(coord);
            return true;
        }

        /// <summary>
        /// Physically walks the agent to a tile, one cardinal step at a time
        /// (never diagonally), always passing through tile centers. Claims the
        /// destination up front so occupancy stays correct, then animates.
        /// Calls <paramref name="onComplete"/> with whether the move was valid.
        /// </summary>
        public IEnumerator MoveTo(GridCoord target, float tilesPerSecond, Action<bool> onComplete)
        {
            GridCoord from = Coord;

            if (!ClaimTile(target))
            {
                onComplete?.Invoke(false);
                yield break;
            }

            float speed = Mathf.Max(0.1f, tilesPerSecond) * _grid.Config.tileSize;

            foreach (var step in BuildOrthogonalPath(from, target))
            {
                Vector3 dest = MarkerWorldPosition(step);
                while ((transform.position - dest).sqrMagnitude > 0.0001f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, dest, speed * Time.deltaTime);
                    yield return null;
                }
                transform.position = dest; // snap exactly to center
            }

            onComplete?.Invoke(true);
        }

        /// <summary>
        /// Cardinal-only path: travel along X first, then along Z, stepping one
        /// tile at a time. Guarantees no diagonal movement.
        /// </summary>
        private static IEnumerable<GridCoord> BuildOrthogonalPath(GridCoord from, GridCoord to)
        {
            int x = from.x, y = from.y;
            while (x != to.x) { x += Math.Sign(to.x - x); yield return new GridCoord(x, y); }
            while (y != to.y) { y += Math.Sign(to.y - y); yield return new GridCoord(x, y); }
        }

        /// <summary>
        /// Updates logical occupancy: vacate the current tile, take the target.
        /// Sets Coord immediately (visual position catches up via animation).
        /// </summary>
        private bool ClaimTile(GridCoord target)
        {
            var tile = _grid != null ? _grid.GetTile(target) : null;
            if (tile == null || (tile.IsOccupied && !ReferenceEquals(tile.Occupant, this)))
                return false;

            var old = _grid.GetTile(Coord);
            if (old != null && ReferenceEquals(old.Occupant, this)) old.Occupant = null;

            tile.Occupant = this;
            Coord = target;
            return true;
        }

        private Vector3 MarkerWorldPosition(GridCoord coord)
        {
            Vector3 tileCenter = _grid.CoordToWorldPosition(coord);
            float tileTop = _grid.Config.tileThickness * 0.5f;
            return tileCenter + Vector3.up * (tileTop + MarkerScale);
        }

        private void BuildMarker(Color color)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Marker";
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * MarkerScale;
            visual.GetComponent<MeshRenderer>().sharedMaterial = MaterialUtil.CreateColored(color);
            _marker = visual.transform;
        }
    }
}
