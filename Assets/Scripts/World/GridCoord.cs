using System;
using UnityEngine;

namespace AISandbox.World
{
    /// <summary>
    /// Integer grid coordinate. This is the world's addressing contract:
    /// everything refers to locations by GridCoord, and visual positions are
    /// derived from it (never the reverse).
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int x;
        public int y;

        public GridCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// Chebyshev (king's-move) distance: diagonals count as 1.
        /// This is the metric used for Move / Observe / Talk ranges.
        /// </summary>
        public static int ChebyshevDistance(GridCoord a, GridCoord b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        /// <summary>Manhattan distance (no diagonals), kept handy if we switch metrics.</summary>
        public static int ManhattanDistance(GridCoord a, GridCoord b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        public bool Equals(GridCoord other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is GridCoord o && Equals(o);
        public override int GetHashCode() => (x * 397) ^ y;
        public override string ToString() => $"({x}, {y})";

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);
    }
}
