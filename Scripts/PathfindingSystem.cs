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
        private int minFloor;
        private int maxFloor;
        private Dictionary<int, WallSegment?[,]> horizontalWallsPerFloor = new();
        private Dictionary<int, WallSegment?[,]> verticalWallsPerFloor = new();
        private readonly Func<Point, Unit> getUnit;
        private readonly Func<Point, int, Unit> getUnitByFloor;
        private readonly Func<Point, int, bool> isCellAvailableOnFloor;
        private readonly List<RampTileData> ramps;

        // Cache for GetMovementZones — invalidated when unit state changes
        private MovementZones _cachedZones;
        private Unit _cachedZonesUnit;
        private Point _cachedZonesCell;
        private int _cachedZonesFloor;
        private int _cachedZonesActionPoints = -1;
        private int _cachedZonesPhosphocreatine = -1;
        private int _cachedZonesAnaerobicFatigue = -1;

        public PathfindingSystem(int w, int h, HashSet<WallSegment> walls, Func<Point, Unit> getUnit)
            : this(
                w,
                h,
                1,
                new Dictionary<int, HashSet<WallSegment>> { { 0, walls } },
                new List<RampTileData>(),
                getUnit,
                (cell, floor) => floor == 0 ? getUnit(cell) : null,
                (cell, floor) => floor == 0)
        { }

        public PathfindingSystem(int w, int h, int floors, Dictionary<int, HashSet<WallSegment>> wallsByFloor,
            List<RampTileData> ramps,
            Func<Point, Unit> getUnit,
            Func<Point, int, Unit> getUnitByFloor,
            Func<Point, int, bool> isCellAvailableOnFloor = null)
        {
            gridW = w;
            gridH = h;
            int configuredFloorCount = Math.Max(1, floors);
            int computedMinFloor = 0;
            int computedMaxFloor = configuredFloorCount - 1;

            this.ramps = ramps ?? new List<RampTileData>();

            if (this.ramps.Count > 0)
            {
                computedMinFloor = Math.Min(computedMinFloor, this.ramps.Min(r => r.Floor));
                computedMaxFloor = Math.Max(computedMaxFloor, this.ramps.Max(r => r.Floor + 1));
            }

            minFloor = computedMinFloor;
            maxFloor = computedMaxFloor;

            this.getUnit = getUnit;
            this.getUnitByFloor = getUnitByFloor;
            this.isCellAvailableOnFloor = isCellAvailableOnFloor;
            UpdateGrid(w, h, wallsByFloor);
        }

        public void UpdateGrid(int w, int h, Dictionary<int, HashSet<WallSegment>> wallsByFloor)
        {
            gridW = w;
            gridH = h;
            BuildWallLookupPerFloor(wallsByFloor);
        }

        private void BuildWallLookupPerFloor(Dictionary<int, HashSet<WallSegment>> wallsByFloor)
        {
            horizontalWallsPerFloor = new Dictionary<int, WallSegment?[,]>();
            verticalWallsPerFloor = new Dictionary<int, WallSegment?[,]>();

            if (wallsByFloor == null)
                return;

            foreach (var kvp in wallsByFloor)
            {
                int floor = kvp.Key;
                var hWalls = new WallSegment?[gridW, gridH + 1];
                var vWalls = new WallSegment?[gridW + 1, gridH];
                horizontalWallsPerFloor[floor] = hWalls;
                verticalWallsPerFloor[floor] = vWalls;

                foreach (var wall in kvp.Value)
                {
                    if (wall.IsHorizontal)
                    {
                        int minX = Math.Min(wall.Start.X, wall.End.X);
                        int maxX = Math.Max(wall.Start.X, wall.End.X);

                        for (int x = minX; x < maxX; x++)
                        {
                            if (x >= 0 && x < gridW && wall.Start.Y >= 0 && wall.Start.Y <= gridH)
                                hWalls[x, wall.Start.Y] = wall;
                        }
                    }
                    else
                    {
                        int minY = Math.Min(wall.Start.Y, wall.End.Y);
                        int maxY = Math.Max(wall.Start.Y, wall.End.Y);

                        for (int y = minY; y < maxY; y++)
                        {
                            if (wall.Start.X >= 0 && wall.Start.X <= gridW && y >= 0 && y < gridH)
                                vWalls[wall.Start.X, y] = wall;
                        }
                    }
                }
            }
        }

        public PathResult FindPathDetailed(Point start, int startFloor, Point goal, int goalFloor, int maxCost, Unit movingUnit)
        {
            var startNode = new GridNode(start, startFloor);
            var goalNode = new GridNode(goal, goalFloor);

            if (startNode.Equals(goalNode))
                return new PathResult { Cells = new List<Point>(), EndFloor = goalFloor };

            var open = new PriorityQueue<GridNode, int>();
            var closed = new HashSet<GridNode>();
            var came = new Dictionary<GridNode, GridNode>();
            var g = new Dictionary<GridNode, int> { { startNode, 0 } };

            open.Enqueue(startNode, Heuristic(startNode, goalNode));

            while (open.Count > 0)
            {
                GridNode cur = open.Dequeue();
                if (closed.Contains(cur)) continue;
                closed.Add(cur);

                if (cur.Equals(goalNode))
                    return ReconstructPath(came, cur);

                int gCur = g.GetValueOrDefault(cur, int.MaxValue);
                foreach (var n in GetNeighbors(cur))
                {
                    if (closed.Contains(n)) continue;
                    if (!CanTraverseNeighbor(cur, n, goalNode, movingUnit))
                        continue;

                    int tentative = gCur + GetEdgeCost(cur, n);
                    if (tentative > maxCost) continue;

                    if (tentative < g.GetValueOrDefault(n, int.MaxValue))
                    {
                        came[n] = cur;
                        g[n] = tentative;
                        open.Enqueue(n, tentative + Heuristic(n, goalNode));
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

        private bool CanTraverseNeighbor(GridNode current, GridNode neighbor, GridNode goalNode, Unit movingUnit)
        {
            if (!IsFloorInBounds(neighbor.Floor))
                return false;

            if (neighbor.Floor == current.Floor && BlocksMovement(current.Cell, neighbor.Cell, current.Floor))
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
            return GetCellsInRange(u, u.GetEffectiveSprintRange(), includeAllFloors: false)
                .Select(node => node.Cell)
                .ToList();
        }

        private List<GridNode> GetCellsInRange(Unit u, int range, bool includeAllFloors)
        {
            var reachable = new HashSet<GridNode>();
            var start = new GridNode(u.Cell, u.Floor);
            var open = new PriorityQueue<GridNode, int>();
            var settled = new HashSet<GridNode>();
            var costs = new Dictionary<GridNode, int> { { start, 0 } };

            open.Enqueue(start, 0);

            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (settled.Contains(current)) continue;
                settled.Add(current);

                int currentCost = costs[current];
                if (currentCost >= range)
                    continue;

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (settled.Contains(neighbor)) continue;
                    if (!IsFloorInBounds(neighbor.Floor)) continue;
                    if (!IsWalkable(neighbor.Cell, neighbor.Floor, u)) continue;
                    if (neighbor.Floor == current.Floor && BlocksMovement(current.Cell, neighbor.Cell, current.Floor)) continue;

                    int nextCost = currentCost + GetEdgeCost(current, neighbor);
                    if (nextCost > range) continue;

                    if (costs.TryGetValue(neighbor, out int bestKnownCost) && bestKnownCost <= nextCost)
                        continue;

                    costs[neighbor] = nextCost;
                    open.Enqueue(neighbor, nextCost);

                    if (neighbor.Cell == u.Cell && neighbor.Floor == u.Floor)
                        continue;

                    if (includeAllFloors || neighbor.Floor == u.Floor)
                        reachable.Add(neighbor);
                }
            }

            return reachable.ToList();
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

            private Dictionary<int, HashSet<Point>> _shortByFloor;
            private Dictionary<int, HashSet<Point>> _maxByFloor;
            private Dictionary<int, HashSet<Point>> _sprintByFloor;

            public Dictionary<int, HashSet<Point>> GetShortByFloor()
                => _shortByFloor ??= BuildGroupedByFloor(ShortMove);

            public Dictionary<int, HashSet<Point>> GetMaxByFloor()
                => _maxByFloor ??= BuildGroupedByFloor(ShortMove, MaxMove);

            public Dictionary<int, HashSet<Point>> GetSprintByFloor()
                => _sprintByFloor ??= BuildGroupedByFloor(ShortMove, MaxMove, Sprint);

            private static Dictionary<int, HashSet<Point>> BuildGroupedByFloor(params List<GridNode>[] lists)
            {
                var result = new Dictionary<int, HashSet<Point>>();
                foreach (var list in lists)
                {
                    if (list == null) continue;
                    foreach (var n in list)
                    {
                        if (!result.TryGetValue(n.Floor, out var set))
                            result[n.Floor] = set = new HashSet<Point>();
                        set.Add(n.Cell);
                    }
                }
                return result;
            }
        }

        public void InvalidateMovementCache()
        {
            _cachedZonesUnit = null;
        }

        public MovementZones GetMovementZones(Unit u)
        {
            if (u == null) return new MovementZones();

            if (u == _cachedZonesUnit
                && u.Cell == _cachedZonesCell
                && u.Floor == _cachedZonesFloor
                && u.ActionPoints == _cachedZonesActionPoints
                && u.Phosphocreatine == _cachedZonesPhosphocreatine
                && u.AnaerobicFatigue == _cachedZonesAnaerobicFatigue)
                return _cachedZones;

            var zones = new MovementZones();
            if (u.ActionPoints >= 1)
                zones.ShortMove = GetCellsInRange(u, u.GetShortMoveRange(), includeAllFloors: true);

            if (u.ActionPoints >= 2)
            {
                var shortSet = new HashSet<GridNode>(zones.ShortMove);
                zones.MaxMove = GetCellsInRange(u, u.GetMaxMoveRange(), includeAllFloors: true)
                    .Where(n => !shortSet.Contains(n))
                    .ToList();
            }

            if (u.CanSprint())
            {
                var shortSet = new HashSet<GridNode>(zones.ShortMove);
                var maxSet = new HashSet<GridNode>(zones.MaxMove);
                zones.Sprint = GetCellsInRange(u, u.GetEffectiveSprintRange(), includeAllFloors: true)
                    .Where(n => !shortSet.Contains(n) && !maxSet.Contains(n))
                    .ToList();
            }

            _cachedZones = zones;
            _cachedZonesUnit = u;
            _cachedZonesCell = u.Cell;
            _cachedZonesFloor = u.Floor;
            _cachedZonesActionPoints = u.ActionPoints;
            _cachedZonesPhosphocreatine = u.Phosphocreatine;
            _cachedZonesAnaerobicFatigue = u.AnaerobicFatigue;

            return zones;
        }

        public bool HasLineOfSight(Point from, Point to, int floor)
        {
            return HasLineOfSight(from, floor, to, floor);
        }

        public bool HasLineOfSight(Point from, int fromFloor, Point to, int toFloor)
        {
            if (fromFloor == toFloor)
            {
                return Has2DLineOfSight(from, to, fromFloor);
            }

            // Simple 3D implementation: check LoS at both floors and ensure no slab in between at target.
            // This is a simplified 3D LoS. A more advanced one would raycast through slabs.
            if (!Has2DLineOfSight(from, to, fromFloor)) return false;
            if (!Has2DLineOfSight(from, to, toFloor)) return false;

            // If looking down
            if (fromFloor > toFloor)
            {
                // Target cell must not have a slab on floors strictly above it up to fromFloor
                for (int f = toFloor + 1; f <= fromFloor; f++)
                {
                    if (isCellAvailableOnFloor != null && isCellAvailableOnFloor(to, f))
                    {
                        // Wait, if it's available, it means there IS a slab (for floor > 0).
                        // Except for floor 0 where it's always available.
                        if (f > 0) return false;
                    }
                }
            }
            else // Looking up
            {
                 // Similar logic for looking up
                 for (int f = fromFloor + 1; f <= toFloor; f++)
                 {
                     if (isCellAvailableOnFloor != null && isCellAvailableOnFloor(from, f))
                     {
                         if (f > 0) return false;
                     }
                 }
            }

            return true;
        }

        private bool Has2DLineOfSight(Point from, Point to, int floor)
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

                if (stepX ^ stepY)
                {
                    if (BlocksSight(new Point(previousX, previousY), new Point(currentX, currentY), floor))
                        return false;

                    continue;
                }

                Point horizontalCell = new Point(previousX + (stepX ? sx : 0), previousY);
                Point verticalCell = new Point(previousX, previousY + (stepY ? sy : 0));

                bool blockedHorizontally = stepX && BlocksSight(new Point(previousX, previousY), horizontalCell, floor);
                bool blockedVertically = stepY && BlocksSight(new Point(previousX, previousY), verticalCell, floor);

                if (blockedHorizontally || blockedVertically)
                    return false;
            }

            return true;
        }

        public WallSegment? GetWallBetween(Point a, Point b, int floor)
        {
            int dx = b.X - a.X, dy = b.Y - a.Y;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return null;

            if (dy != 0) // Horizontal wall (separating North/South)
            {
                int x = a.X;
                int y = Math.Max(a.Y, b.Y);
                if (horizontalWallsPerFloor.TryGetValue(floor, out var hWalls))
                {
                    if (x >= 0 && x < gridW && y >= 0 && y <= gridH)
                        return hWalls[x, y];
                }
            }
            else // Vertical wall (separating West/East)
            {
                int x = Math.Max(a.X, b.X);
                int y = a.Y;
                if (verticalWallsPerFloor.TryGetValue(floor, out var vWalls))
                {
                    if (x >= 0 && x <= gridW && y >= 0 && y < gridH)
                        return vWalls[x, y];
                }
            }
            return null;
        }

        public bool BlocksMovement(Point a, Point b, int floor)
        {
            var wall = GetWallBetween(a, b, floor);
            return wall.HasValue && (wall.Value.Type == WallType.Full || wall.Value.Type == WallType.Window);
        }

        public bool BlocksSight(Point a, Point b, int floor)
        {
            var wall = GetWallBetween(a, b, floor);
            return wall.HasValue && wall.Value.Type == WallType.Full;
        }

        public List<Point> GetNeighbors(Point c, int floor)
        {
            return new[] { new Point(c.X, c.Y - 1), new Point(c.X, c.Y + 1), new Point(c.X - 1, c.Y), new Point(c.X + 1, c.Y) }
                   .Where(n => n.X >= 0 && n.X < gridW && n.Y >= 0 && n.Y < gridH && !BlocksMovement(c, n, floor)).ToList();
        }

        public bool IsWalkable(Point c, int floor, Unit movingUnit = null)
        {
            if (c.X < 0 || c.Y < 0 || c.X >= gridW || c.Y >= gridH || !IsFloorInBounds(floor)) return false;

            if (isCellAvailableOnFloor != null && !isCellAvailableOnFloor(c, floor))
                return false;

            var u = getUnitByFloor(c, floor);
            return u == null || u == movingUnit;
        }

        private bool IsFloorInBounds(int floor)
        {
            return floor >= minFloor && floor <= maxFloor;
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
