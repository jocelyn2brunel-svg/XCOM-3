using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class PathfindingSystem
    {
        private int gridW, gridH;
        private HashSet<WallSegment> walls;
        private Func<Point, Unit> getUnit;

        public PathfindingSystem(int w, int h, HashSet<WallSegment> walls, Func<Point, Unit> getUnit)
        { gridW = w; gridH = h; this.walls = walls; this.getUnit = getUnit; }

        public void UpdateGrid(int w, int h) { gridW = w; gridH = h; }

        // ════════════════════ A* ════════════════════
        public List<Point> FindPath(Point start, Point goal, int maxCost, Unit movingUnit)
        {
            if (start == goal) return new List<Point>();
            var open = new List<Point> { start };
            var came = new Dictionary<Point, Point>();
            var g = new Dictionary<Point, int> { { start, 0 } };
            var f = new Dictionary<Point, int> { { start, Heuristic(start, goal) } };

            while (open.Count > 0)
            {
                Point cur = open.OrderBy(p => f.GetValueOrDefault(p, int.MaxValue)).First();
                if (cur == goal) return ReconstructPath(came, cur);
                open.Remove(cur);

                foreach (var n in new[]{ new Point(cur.X-1,cur.Y), new Point(cur.X+1,cur.Y),
                                         new Point(cur.X,cur.Y-1), new Point(cur.X,cur.Y+1) })
                {
                    if (n != goal && (!IsWalkable(n, movingUnit) || (getUnit(n) is Unit u && u != movingUnit))) continue;
                    if (HasWallBetween(cur, n)) continue;
                    int tentative = g[cur] + 1;
                    if (tentative < g.GetValueOrDefault(n, int.MaxValue))
                    {
                        came[n] = cur; g[n] = tentative; f[n] = tentative + Heuristic(n, goal);
                        if (!open.Contains(n)) open.Add(n);
                    }
                }
            }
            return new List<Point>();
        }

        private int Heuristic(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private List<Point> ReconstructPath(Dictionary<Point, Point> came, Point cur)
        {
            var path = new List<Point> { cur };
            while (came.ContainsKey(cur)) { cur = came[cur]; path.Insert(0, cur); }
            path.RemoveAt(0);
            return path;
        }

        // ════════════════════ Mouvement ════════════════════
        public List<Point> GetMovableCells(Unit u)
        {
            if (u == null || u.MovementPoints <= 0) return new List<Point>();
            int r = u.MovementPoints; var cells = new List<Point>();

            for (int x = u.Cell.X - r; x <= u.Cell.X + r; x++)
                for (int y = u.Cell.Y - r; y <= u.Cell.Y + r; y++)
                {
                    var t = new Point(x, y);
                    if (t == u.Cell || x < 0 || y < 0 || x >= gridW || y >= gridH || !IsWalkable(t)) continue;
                    var path = FindPath(u.Cell, t, r, u);
                    if (path.Count > 0 && path.Count <= r) cells.Add(t);
                }
            return cells;
        }

        // ════════════════════ Ligne de vue ════════════════════
        public bool HasLineOfSight(Point from, Point to)
        {
            int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
            Point cur = from, prev = cur;

            while (true)
            {
                if (cur != from && HasWallBetween(prev, cur)) return false;
                if (cur.X == x1 && cur.Y == y1) break;
                prev = cur; int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; cur.X += sx; }
                if (e2 < dx) { err += dx; cur.Y += sy; }
            }
            return true;
        }

        // ════════════════════ Murs ════════════════════
        public bool HasWallBetween(Point a, Point b)
        {
            int dx = b.X - a.X, dy = b.Y - a.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return false;
            return walls.Any(w =>
                (dy == 1 && w.IsHorizontal && w.Start.Y == b.Y && a.X >= w.Start.X && a.X < w.End.X) ||
                (dy == -1 && w.IsHorizontal && w.Start.Y == a.Y && a.X >= w.Start.X && a.X < w.End.X) ||
                (dx == 1 && !w.IsHorizontal && w.Start.X == b.X && a.Y >= w.Start.Y && a.Y < w.End.Y) ||
                (dx == -1 && !w.IsHorizontal && w.Start.X == a.X && a.Y >= w.Start.Y && a.Y < w.End.Y));
        }

        // ════════════════════ Voisinage et marchabilité ════════════════════
        public List<Point> GetNeighbors(Point c)
        {
            return new[] { new Point(c.X, c.Y - 1), new Point(c.X, c.Y + 1), new Point(c.X - 1, c.Y), new Point(c.X + 1, c.Y) }
                   .Where(n => n.X >= 0 && n.X < gridW && n.Y >= 0 && n.Y < gridH && !HasWallBetween(c, n)).ToList();
        }

        public bool IsWalkable(Point c, Unit movingUnit = null)
        {
            if (c.X < 0 || c.Y < 0 || c.X >= gridW || c.Y >= gridH) return false;
            var u = getUnit(c);
            return u == null || u == movingUnit;
        }

        // ════════════════════ Utilitaires ════════════════════
        public int ManhattanDistance(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        public bool AreAdjacent(Point a, Point b) => ManhattanDistance(a, b) == 1;
        public int GetPathCost(List<Point> path) => path.Count;
    }
}
