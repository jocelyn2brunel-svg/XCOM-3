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
        public List<GridNode> Nodes { get; set; } = new List<GridNode>();
        public int EndFloor { get; set; }
    }

    public class PathfindingSystem
    {
        public const int VerticalTransitionExtraCost = 1;

        private int gridW, gridH;
        private int floorCount;
        private HashSet<WallSegment> walls;
        private Dictionary<(Point A, Point B), WallSegment> wallLookup = new();
        private readonly Func<Point, Unit> getUnit;
        private readonly Func<Point, int, Unit> getUnitByFloor;
        private readonly Func<Point, int, bool> isCellAvailableOnFloor;
        private readonly List<StairConnectionData> stairs;
        private readonly List<RampTileData> ramps;

        public PathfindingSystem(int w, int h, HashSet<WallSegment> walls, Func<Point, Unit> getUnit)
            : this(
                w,
                h,
                1,
                walls,
                new List<StairConnectionData>(),
                new List<RampTileData>(),
                getUnit,
                (cell, floor) => floor == 0 ? getUnit(cell) : null,
                (cell, floor) => floor == 0)
        { }

        public PathfindingSystem(int w, int h, int floors, HashSet<WallSegment> walls,
            List<StairConnectionData> stairs,
            List<RampTileData> ramps,
            Func<Point, Unit> getUnit,
            Func<Point, int, Unit> getUnitByFloor,
            Func<Point, int, bool> isCellAvailableOnFloor = null)
        {
            gridW = w;
            gridH = h;
            floorCount = Math.Max(1, floors);
            this.walls = walls;
            this.stairs = stairs ?? new List<StairConnectionData>();
            this.ramps = ramps ?? new List<RampTileData>();
            this.getUnit = getUnit;
            this.getUnitByFloor = getUnitByFloor;
            this.isCellAvailableOnFloor = isCellAvailableOnFloor;
            BuildWallLookup();
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
            var openSet = new HashSet<GridNode> { startNode };
            var came = new Dictionary<GridNode, GridNode>();
            var g = new Dictionary<GridNode, int> { { startNode, 0 } };
            var f = new Dictionary<GridNode, int> { { startNode, Heuristic(startNode, goalNode) } };

            while (open.Count > 0)
            {
                GridNode cur = GetLowestCostNode(open, f);
                if (cur.Equals(goalNode))
                    return ReconstructPath(came, cur);

                open.Remove(cur);
                openSet.Remove(cur);

                foreach (var n in GetNeighbors(cur))
                {
                    if (!CanTraverseNeighbor(cur, n, goalNode, movingUnit))
                        continue;

                    int tentative = g[cur] + GetEdgeCost(cur, n);
                    if (tentative > maxCost) continue;

                    if (tentative < g.GetValueOrDefault(n, int.MaxValue))
                    {
                        came[n] = cur;
                        g[n] = tentative;
                        f[n] = tentative + Heuristic(n, goalNode);
                        if (openSet.Add(n))
                            open.Add(n);
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
            int planarDistance = Math.Abs(a.Cell.X - b.Cell.X) + Math.Abs(a.Cell.Y - b.Cell.Y);
            int floorDistance = Math.Abs(a.Floor - b.Floor);
            return planarDistance + floorDistance * (1 + VerticalTransitionExtraCost);
        }

        private static int GetEdgeCost(GridNode from, GridNode to)
        {
            int baseCost = 1;
            if (from.Floor != to.Floor)
                baseCost += VerticalTransitionExtraCost;

            return baseCost;
        }

        private GridNode GetLowestCostNode(List<GridNode> openNodes, Dictionary<GridNode, int> fScores)
        {
            var bestNode = openNodes[0];
            var bestScore = fScores.GetValueOrDefault(bestNode, int.MaxValue);

            for (int i = 1; i < openNodes.Count; i++)
            {
                var node = openNodes[i];
                var score = fScores.GetValueOrDefault(node, int.MaxValue);

                if (score < bestScore)
                {
                    bestNode = node;
                    bestScore = score;
                }
            }

            return bestNode;
        }

        private bool CanTraverseNeighbor(GridNode current, GridNode neighbor, GridNode goalNode, Unit movingUnit)
        {
            if (neighbor.Floor < 0 || neighbor.Floor >= floorCount)
                return false;

            if (neighbor.Floor == current.Floor && BlocksMovement(current.Cell, neighbor.Cell))
                return false;

            if (neighbor.Equals(goalNode))
                return true;

            return IsWalkable(neighbor.Cell, neighbor.Floor, movingUnit);
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
                Nodes = nodes,
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

            foreach (var ramp in ramps)
            {
                int rampDx = GetRampAscendDx(ramp);
                int rampDy = GetRampAscendDy(ramp);

                if (ramp.Floor == node.Floor && ramp.X == node.Cell.X && ramp.Y == node.Cell.Y)
                {
                    yield return new GridNode(new Point(ramp.X + rampDx, ramp.Y + rampDy), node.Floor + 1);
                }

                if (ramp.Bidirectional && ramp.Floor + 1 == node.Floor && ramp.X + rampDx == node.Cell.X && ramp.Y + rampDy == node.Cell.Y)
                {
                    yield return new GridNode(new Point(ramp.X, ramp.Y), node.Floor - 1);
                }
            }
        }

        private static int GetRampAscendDx(RampTileData ramp)
            => (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDx : 0;

        private static int GetRampAscendDy(RampTileData ramp)
            => (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDy : -1;

        public List<Point> GetShortMoveCells(Unit u)
        {
            if (u == null || u.ActionPoints <= 0) return new List<Point>();
            return GetCellsInRange(u, u.GetShortMoveRange(), includeAllFloors: false)
                .Select(node => node.Cell)
                .ToList();
        }

        public List<Point> GetMaxMoveCells(Unit u)
        {
            if (u == null || u.ActionPoints < 2) return new List<Point>();
            return GetCellsInRange(u, u.GetMaxMoveRange(), includeAllFloors: false)
                .Select(node => node.Cell)
                .ToList();
        }

        public List<Point> GetSprintCells(Unit u)
        {
            if (u == null || !u.CanSprint()) return new List<Point>();
            return GetCellsInRange(u, u.GetSprintRange(), includeAllFloors: false)
                .Select(node => node.Cell)
                .ToList();
        }

        private List<GridNode> GetCellsInRange(Unit u, int range, bool includeAllFloors)
        {
            var reachable = new List<GridNode>();
            var start = new GridNode(u.Cell, u.Floor);
            var open = new List<GridNode>();
            var costs = new Dictionary<GridNode, int> { { start, 0 } };

            open.Add(start);

            while (open.Count > 0)
            {
                var current = GetLowestCostNode(open, costs);
                open.Remove(current);
                int currentCost = costs[current];

                if (currentCost >= range)
                    continue;

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (neighbor.Floor < 0 || neighbor.Floor >= floorCount)
                        continue;

                    if (!IsWalkable(neighbor.Cell, neighbor.Floor, u))
                        continue;

                    if (neighbor.Floor == current.Floor && BlocksMovement(current.Cell, neighbor.Cell))
                        continue;

                    int nextCost = currentCost + GetEdgeCost(current, neighbor);
                    if (nextCost > range)
                        continue;

                    if (costs.TryGetValue(neighbor, out int bestKnownCost) && bestKnownCost <= nextCost)
                        continue;

                    costs[neighbor] = nextCost;
                    if (!open.Contains(neighbor))
                        open.Add(neighbor);

                    if (neighbor.Cell == u.Cell && neighbor.Floor == u.Floor)
                        continue;

                    if (includeAllFloors || neighbor.Floor == u.Floor)
                        reachable.Add(neighbor);
                }
            }

            return reachable;
        }

        public List<Point> GetMovableCells(Unit u)
        {
            if (u == null || u.ActionPoints <= 0) return new List<Point>();
            int maxRange = u.CanSprint() ? u.GetSprintRange() : u.GetMaxMoveRange();
            return GetCellsInRange(u, maxRange, includeAllFloors: false)
                .Select(node => node.Cell)
                .ToList();
        }

        public class MovementZones
        {
            public List<GridNode> ShortMove { get; set; } = new List<GridNode>();
            public List<GridNode> MaxMove { get; set; } = new List<GridNode>();
            public List<GridNode> Sprint { get; set; } = new List<GridNode>();
        }

        public MovementZones GetMovementZones(Unit u)
        {
            var zones = new MovementZones();
            if (u == null) return zones;
            if (u.ActionPoints >= 1) zones.ShortMove = GetCellsInRange(u, u.GetShortMoveRange(), includeAllFloors: true);
            if (u.ActionPoints >= 2)
                zones.MaxMove = GetCellsInRange(u, u.GetMaxMoveRange(), includeAllFloors: true)
                    .Except(zones.ShortMove)
                    .ToList();
            if (u.CanSprint())
                zones.Sprint = GetCellsInRange(u, u.GetSprintRange(), includeAllFloors: true)
                    .Except(zones.ShortMove)
                    .Except(zones.MaxMove)
                    .ToList();
            return zones;
        }

        public bool HasLineOfSight(Point from, Point to)
        {
            int x0 = from.X;
            int y0 = from.Y;
            int x1 = to.X;
            int y1 = to.Y;

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            int currentX = x0;
            int currentY = y0;

            while (currentX != x1 || currentY != y1)
            {
                int previousX = currentX;
                int previousY = currentY;
                int e2 = 2 * err;

                bool stepX = false;
                bool stepY = false;

                if (e2 > -dy)
                {
                    err -= dy;
                    currentX += sx;
                    stepX = true;
                }

                if (e2 < dx)
                {
                    err += dx;
                    currentY += sy;
                    stepY = true;
                }

                // Déplacement orthogonal : un seul segment à vérifier.
                if (stepX ^ stepY)
                {
                    if (BlocksSight(new Point(previousX, previousY), new Point(currentX, currentY)))
                        return false;

                    continue;
                }

                // Déplacement diagonal : vérifier les deux arêtes traversées pour éviter
                // qu'une ligne de vue "passe à travers" un mur placé sur un coin.
                Point horizontalCell = new Point(previousX + (stepX ? sx : 0), previousY);
                Point verticalCell = new Point(previousX, previousY + (stepY ? sy : 0));

                bool blockedHorizontally = stepX && BlocksSight(new Point(previousX, previousY), horizontalCell);
                bool blockedVertically = stepY && BlocksSight(new Point(previousX, previousY), verticalCell);

                if (blockedHorizontally || blockedVertically)
                    return false;
            }

            return true;
        }

        public WallSegment? GetWallBetween(Point a, Point b)
        {
            int dx = b.X - a.X, dy = b.Y - a.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return null;

            return wallLookup.TryGetValue((a, b), out var wall) ? wall : null;
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

            if (isCellAvailableOnFloor != null && !isCellAvailableOnFloor(c, floor))
                return false;

            var u = getUnitByFloor(c, floor);
            return u == null || u == movingUnit;
        }

        public void UpdateGrid(int w, int h, HashSet<WallSegment> newWalls)
        {
            gridW = w;
            gridH = h;
            walls = newWalls;
            BuildWallLookup();
        }

        private void BuildWallLookup()
        {
            wallLookup = new Dictionary<(Point A, Point B), WallSegment>();

            if (walls == null)
                return;

            foreach (var wall in walls)
            {
                if (wall.IsHorizontal)
                {
                    int minX = Math.Min(wall.Start.X, wall.End.X);
                    int maxX = Math.Max(wall.Start.X, wall.End.X);

                    for (int x = minX; x < maxX; x++)
                    {
                        var top = new Point(x, wall.Start.Y - 1);
                        var bottom = new Point(x, wall.Start.Y);
                        wallLookup[(top, bottom)] = wall;
                        wallLookup[(bottom, top)] = wall;
                    }
                }
                else
                {
                    int minY = Math.Min(wall.Start.Y, wall.End.Y);
                    int maxY = Math.Max(wall.Start.Y, wall.End.Y);

                    for (int y = minY; y < maxY; y++)
                    {
                        var left = new Point(wall.Start.X - 1, y);
                        var right = new Point(wall.Start.X, y);
                        wallLookup[(left, right)] = wall;
                        wallLookup[(right, left)] = wall;
                    }
                }
            }
        }

        public int ManhattanDistance(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        public bool AreAdjacent(Point a, Point b) => ManhattanDistance(a, b) == 1;
        public int GetPathCost(List<GridNode> path, GridNode? startNode = null)
        {
            if (path == null || path.Count == 0)
                return 0;

            int cost = 0;
            GridNode previous = startNode ?? path[0];

            int firstIndex = startNode.HasValue ? 0 : 1;
            if (!startNode.HasValue)
                cost = 1;

            for (int i = firstIndex; i < path.Count; i++)
            {
                GridNode current = path[i];
                cost += GetEdgeCost(previous, current);
                previous = current;
            }

            return cost;
        }

        public int GetVerticalTransitionCount(List<GridNode> path, GridNode? startNode = null)
        {
            if (path == null || path.Count == 0)
                return 0;

            int transitions = 0;
            GridNode previous = startNode ?? path[0];
            int firstIndex = startNode.HasValue ? 0 : 1;

            for (int i = firstIndex; i < path.Count; i++)
            {
                if (previous.Floor != path[i].Floor)
                    transitions++;

                previous = path[i];
            }

            return transitions;
        }

        public int GetPathCost(List<Point> path) => path.Count;
    }
}
