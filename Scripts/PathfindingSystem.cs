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
        /// <summary>
        /// Obtient les cellules de mouvement court (1 AP) - VERT
        /// </summary>
        public List<Point> GetShortMoveCells(Unit u)
        {
            if (u == null || u.ActionPoints <= 0) return new List<Point>();

            int range = u.GetShortMoveRange();
            return GetCellsInRange(u, range);
        }

        /// <summary>
        /// Obtient les cellules de mouvement complet (2 AP) - BLEU
        /// </summary>
        public List<Point> GetMaxMoveCells(Unit u)
        {
            if (u == null || u.ActionPoints < 2) return new List<Point>();

            int range = u.GetMaxMoveRange();
            return GetCellsInRange(u, range);
        }

        /// <summary>
        /// Obtient les cellules de sprint (2 AP + stamina) - JAUNE
        /// </summary>
        public List<Point> GetSprintCells(Unit u)
        {
            if (u == null || !u.CanSprint()) return new List<Point>();

            int range = u.GetSprintRange();
            return GetCellsInRange(u, range);
        }

        /// <summary>
        /// Méthode utilitaire pour obtenir les cellules dans un rayon
        /// </summary>
        private List<Point> GetCellsInRange(Unit u, int range)
        {
            var cells = new List<Point>();

            for (int x = u.Cell.X - range; x <= u.Cell.X + range; x++)
            {
                for (int y = u.Cell.Y - range; y <= u.Cell.Y + range; y++)
                {
                    var t = new Point(x, y);
                    if (t == u.Cell || x < 0 || y < 0 || x >= gridW || y >= gridH || !IsWalkable(t))
                        continue;

                    var path = FindPath(u.Cell, t, range, u);
                    if (path.Count > 0 && path.Count <= range)
                        cells.Add(t);
                }
            }

            return cells;
        }

        /// <summary>
        /// Obtient TOUTES les cellules accessibles (pour compatibilité)
        /// </summary>
        public List<Point> GetMovableCells(Unit u)
        {
            if (u == null || u.ActionPoints <= 0) return new List<Point>();

            // Retourne toutes les cellules accessibles (jusqu'au sprint si possible)
            int maxRange = u.CanSprint() ? u.GetSprintRange() : u.GetMaxMoveRange();
            return GetCellsInRange(u, maxRange);
        }

        /// <summary>
        /// Structure pour stocker les 3 zones de mouvement
        /// </summary>
        public class MovementZones
        {
            public List<Point> ShortMove { get; set; } = new List<Point>();  // 1 AP - Vert
            public List<Point> MaxMove { get; set; } = new List<Point>();    // 2 AP - Bleu
            public List<Point> Sprint { get; set; } = new List<Point>();     // 2 AP + Stamina - Jaune

            public MovementZones() { }
        }

        /// <summary>
        /// Obtient les 3 zones de mouvement pour affichage
        /// </summary>
        public MovementZones GetMovementZones(Unit u)
        {
            var zones = new MovementZones();

            if (u == null) return zones;

            // Zone 1 : Mouvement court (1 AP)
            if (u.ActionPoints >= 1)
            {
                zones.ShortMove = GetShortMoveCells(u);
            }

            // Zone 2 : Mouvement max (2 AP) - exclure zone court
            if (u.ActionPoints >= 2)
            {
                var maxCells = GetMaxMoveCells(u);
                zones.MaxMove = maxCells.Except(zones.ShortMove).ToList();
            }

            // Zone 3 : Sprint (2 AP + stamina) - exclure zones précédentes
            if (u.CanSprint())
            {
                var sprintCells = GetSprintCells(u);
                zones.Sprint = sprintCells
                    .Except(zones.ShortMove)
                    .Except(zones.MaxMove)
                    .ToList();
            }

            return zones;
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

        public void UpdateGrid(int w, int h, HashSet<WallSegment> newWalls)
        {
            gridW = w;
            gridH = h;
            walls = newWalls;
        }


        // ════════════════════ Utilitaires ════════════════════
        public int ManhattanDistance(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        public bool AreAdjacent(Point a, Point b) => ManhattanDistance(a, b) == 1;
        public int GetPathCost(List<Point> path) => path.Count;


    }
}
