using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public readonly struct GridNode : IEquatable<GridNode>
    {
        public Point Cell { get; }
        public int Floor { get; }

        public GridNode(Point cell, int floor)
        {
            Cell = cell;
            Floor = floor;
        }

        public bool Equals(GridNode other) => Cell == other.Cell && Floor == other.Floor;
        public override bool Equals(object obj) => obj is GridNode other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cell, Floor);
    }

    public class PathResult
    {
        public List<Point> Cells { get; set; } = new List<Point>();
        public int EndFloor { get; set; }
    }

    public class PathfindingSystem
    {
        private int gridW, gridH;
        private int floorCount;
        private HashSet<WallSegment> walls;
        private readonly Func<Point, Unit> getUnit;
        private readonly Func<Point, int, Unit> getUnitByFloor;
        private readonly List<StairConnectionData> stairs;

        public PathfindingSystem(int w, int h, HashSet<WallSegment> walls, Func<Point, Unit> getUnit)
            : this(w, h, 1, walls, new List<StairConnectionData>(), getUnit, (cell, floor) => floor == 0 ? getUnit(cell) : null)
        { }

        public PathfindingSystem(int w, int h, int floors, HashSet<WallSegment> walls,
            List<StairConnectionData> stairs,
            Func<Point, Unit> getUnit,
            Func<Point, int, Unit> getUnitByFloor)
        {
            gridW = w;
            gridH = h;
            floorCount = Math.Max(1, floors);
            this.walls = walls;
            this.stairs = stairs ?? new List<StairConnectionData>();
            this.getUnit = getUnit;
            this.getUnitByFloor = getUnitByFloor;
        }

        public void UpdateGrid(int w, int h) { gridW = w; gridH = h; }

        public PathResult FindPathDetailed(Point start, int startFloor, Point goal, int goalFloor, int maxCost, Unit movingUnit)
        {
            var startNode = new GridNode(start, startFloor);
            var goalNode = new GridNode(goal, goalFloor);

            if (startNode.Equals(goalNode))
            {
                return new PathResult { Cells = new List<Point>(), EndFloor = goalFloor };
            }

            var open = new List<GridNode> { startNode };
            var came = new Dictionary<GridNode, GridNode>();
            var g = new Dictionary<GridNode, int> { { startNode, 0 } };
            var f = new Dictionary<GridNode, int> { { startNode, Heuristic(startNode, goalNode) } };

            while (open.Count > 0)
            {
                GridNode cur = open.OrderBy(p => f.GetValueOrDefault(p, int.MaxValue)).First();
                if (cur.Equals(goalNode))
                    return ReconstructPath(came, cur);

                open.Remove(cur);

                foreach (var n in GetNeighbors(cur))
                {
                    if (n.Floor < 0 || n.Floor >= floorCount) continue;

                    if (!n.Equals(goalNode))
                    {
                        if (!IsWalkable(n.Cell, n.Floor, movingUnit))
                            continue;

                        if (n.Floor == cur.Floor && BlocksMovement(cur.Cell, n.Cell))
                            continue;
                    }

                    int tentative = g[cur] + 1;
                    if (tentative > maxCost) continue;

                    if (tentative < g.GetValueOrDefault(n, int.MaxValue))
                    {
                        came[n] = cur;
                        g[n] = tentative;
                        f[n] = tentative + Heuristic(n, goalNode);
                        if (!open.Contains(n)) open.Add(n);
                    }
                }
            }

            return new PathResult { Cells = new List<Point>(), EndFloor = startFloor };
        }

        public List<Point> FindPath(Point start, Point goal, int maxCost, Unit movingUnit)
        {
            int startFloor = movingUnit?.Floor ?? 0;
            var result = FindPathDetailed(start, startFloor, goal, startFloor, maxCost, movingUnit);
            return result.Cells;
        }

        private int Heuristic(GridNode a, GridNode b)
        {
            return Math.Abs(a.Cell.X - b.Cell.X) + Math.Abs(a.Cell.Y - b.Cell.Y) + Math.Abs(a.Floor - b.Floor);
        }

        private PathResult ReconstructPath(Dictionary<GridNode, GridNode> came, GridNode cur)
        {
            var nodes = new List<GridNode> { cur };
            while (came.ContainsKey(cur))
            {
                cur = came[cur];
                nodes.Insert(0, cur);
            }

            if (nodes.Count > 0)
                nodes.RemoveAt(0);

            return new PathResult
            {
                Cells = nodes.Select(n => n.Cell).ToList(),
                EndFloor = nodes.Count > 0 ? nodes[^1].Floor : cur.Floor
            };
        }

        private IEnumerable<GridNode> GetNeighbors(GridNode node)
        {
            yield return new GridNode(new Point(node.Cell.X - 1, node.Cell.Y), node.Floor);
            yield return new GridNode(new Point(node.Cell.X + 1, node.Cell.Y), node.Floor);
            yield return new GridNode(new Point(node.Cell.X, node.Cell.Y - 1), node.Floor);
            yield return new GridNode(new Point(node.Cell.X, node.Cell.Y + 1), node.Floor);

            foreach (var stair in stairs)
            {
                if (stair.FromFloor == node.Floor && stair.FromX == node.Cell.X && stair.FromY == node.Cell.Y)
                    yield return new GridNode(new Point(stair.ToX, stair.ToY), stair.ToFloor);

                if (stair.Bidirectional && stair.ToFloor == node.Floor && stair.ToX == node.Cell.X && stair.ToY == node.Cell.Y)
                    yield return new GridNode(new Point(stair.FromX, stair.FromY), stair.FromFloor);
            }
        }

        public List<Point> GetShortMoveCells(Unit u)
        {
            if (u == null || u.ActionPoints <= 0) return new List<Point>();
            return GetCellsInRange(u, u.GetShortMoveRange());
        }

        public List<Point> GetMaxMoveCells(Unit u)
        {
            if (u == null || u.ActionPoints < 2) return new List<Point>();
            return GetCellsInRange(u, u.GetMaxMoveRange());
        }

        public List<Point> GetSprintCells(Unit u)
        {
            if (u == null || !u.CanSprint()) return new List<Point>();
            return GetCellsInRange(u, u.GetSprintRange());
        }

        private List<Point> GetCellsInRange(Unit u, int range)
        {
            var cells = new List<Point>();

            for (int x = u.Cell.X - range; x <= u.Cell.X + range; x++)
            {
                for (int y = u.Cell.Y - range; y <= u.Cell.Y + range; y++)
                {
                    var t = new Point(x, y);
                    if (t == u.Cell || x < 0 || y < 0 || x >= gridW || y >= gridH || !IsWalkable(t, u.Floor, u))
                        continue;

                    var path = FindPathDetailed(u.Cell, u.Floor, t, u.Floor, range, u);
                    if (path.Cells.Count > 0 && path.Cells.Count <= range)
                        cells.Add(t);
                }
            }

            return cells;
        }

        public List<Point> GetMovableCells(Unit u)
        {
            if (u == null || u.ActionPoints <= 0) return new List<Point>();
            int maxRange = u.CanSprint() ? u.GetSprintRange() : u.GetMaxMoveRange();
            return GetCellsInRange(u, maxRange);
        }

        public class MovementZones
        {
            public List<Point> ShortMove { get; set; } = new List<Point>();
            public List<Point> MaxMove { get; set; } = new List<Point>();
            public List<Point> Sprint { get; set; } = new List<Point>();
        }

        public MovementZones GetMovementZones(Unit u)
        {
            var zones = new MovementZones();
            if (u == null) return zones;
            if (u.ActionPoints >= 1) zones.ShortMove = GetShortMoveCells(u);
            if (u.ActionPoints >= 2) zones.MaxMove = GetMaxMoveCells(u).Except(zones.ShortMove).ToList();
            if (u.CanSprint()) zones.Sprint = GetSprintCells(u).Except(zones.ShortMove).Except(zones.MaxMove).ToList();
            return zones;
        }

        public bool HasLineOfSight(Point from, Point to)
        {
            int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx - dy;
            Point cur = from, prev = cur;

            while (true)
            {
                if (cur != from && BlocksSight(prev, cur)) return false;
                if (cur.X == x1 && cur.Y == y1) break;
                prev = cur; int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; cur.X += sx; }
                if (e2 < dx) { err += dx; cur.Y += sy; }
            }
            return true;
        }

        public WallSegment? GetWallBetween(Point a, Point b)
        {
            int dx = b.X - a.X, dy = b.Y - a.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return null;

            foreach (var w in walls)
            {
                if ((dy == 1 && w.IsHorizontal && w.Start.Y == b.Y && a.X >= w.Start.X && a.X < w.End.X) ||
                    (dy == -1 && w.IsHorizontal && w.Start.Y == a.Y && a.X >= w.Start.X && a.X < w.End.X) ||
                    (dx == 1 && !w.IsHorizontal && w.Start.X == b.X && a.Y >= w.Start.Y && a.Y < w.End.Y) ||
                    (dx == -1 && !w.IsHorizontal && w.Start.X == a.X && a.Y >= w.Start.Y && a.Y < w.End.Y))
                {
                    return w;
                }
            }
            return null;
        }

        public bool BlocksMovement(Point a, Point b)
        {
            var wall = GetWallBetween(a, b);
            return wall.HasValue && (wall.Value.Type == WallType.Full || wall.Value.Type == WallType.Window);
        }

        public bool BlocksSight(Point a, Point b)
        {
            var wall = GetWallBetween(a, b);
            return wall.HasValue && wall.Value.Type == WallType.Full;
        }

        public List<Point> GetNeighbors(Point c)
        {
            return new[] { new Point(c.X, c.Y - 1), new Point(c.X, c.Y + 1), new Point(c.X - 1, c.Y), new Point(c.X + 1, c.Y) }
                   .Where(n => n.X >= 0 && n.X < gridW && n.Y >= 0 && n.Y < gridH && !BlocksMovement(c, n)).ToList();
        }

        public bool IsWalkable(Point c, Unit movingUnit = null) => IsWalkable(c, movingUnit?.Floor ?? 0, movingUnit);

        public bool IsWalkable(Point c, int floor, Unit movingUnit = null)
        {
            if (c.X < 0 || c.Y < 0 || c.X >= gridW || c.Y >= gridH || floor < 0 || floor >= floorCount) return false;
            var u = getUnitByFloor(c, floor);
            return u == null || u == movingUnit;
        }

        public void UpdateGrid(int w, int h, HashSet<WallSegment> newWalls)
        {
            gridW = w;
            gridH = h;
            walls = newWalls;
        }

        public int ManhattanDistance(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        public bool AreAdjacent(Point a, Point b) => ManhattanDistance(a, b) == 1;
        public int GetPathCost(List<Point> path) => path.Count;
    }
}
