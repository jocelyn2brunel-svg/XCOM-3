using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class UnitSpatialHash
    {
        private Dictionary<(Point Cell, int Floor), Unit> cellToUnit = new();
        private Dictionary<Unit, (Point Cell, int Floor)> unitToCell = new();

        public void AddUnit(Unit u)
        {
            if (unitToCell.ContainsKey(u)) RemoveUnit(u);
            cellToUnit[(u.Cell, u.Floor)] = u; unitToCell[u] = (u.Cell, u.Floor);
        }

        public void RemoveUnit(Unit u)
        {
            if (unitToCell.TryGetValue(u, out var c)) { cellToUnit.Remove(c); unitToCell.Remove(u); }
        }

        public void MoveUnit(Unit u, Point newCell, int newFloor)
        {
            if (unitToCell.TryGetValue(u, out var old)) cellToUnit.Remove(old);
            cellToUnit[(newCell, newFloor)] = u; unitToCell[u] = (newCell, newFloor); u.Cell = newCell; u.Floor = newFloor;
        }

        public void MoveUnit(Unit u, Point newCell) => MoveUnit(u, newCell, u.Floor);

        public Unit GetUnitAt(Point c) => GetUnitAt(c, 0);
        public Unit GetUnitAt(Point c, int floor) => cellToUnit.TryGetValue((c, floor), out Unit u) ? u : null;
        public bool IsCellOccupied(Point c) => IsCellOccupied(c, 0);
        public bool IsCellOccupied(Point c, int floor) => cellToUnit.ContainsKey((c, floor));

        public List<Unit> GetUnitsInRadius(Point center, int r)
        {
            var list = new List<Unit>();
            for (int x = center.X - r; x <= center.X + r; x++)
                for (int y = center.Y - r; y <= center.Y + r; y++)
                    if (Vector2.Distance(new Vector2(center.X, center.Y), new Vector2(x, y)) <= r)
                        if (GetUnitAt(new Point(x, y), 0) is Unit u) list.Add(u);
            return list;
        }

        public void Clear() { cellToUnit.Clear(); unitToCell.Clear(); }

        public void RebuildFromLists(List<Unit> player, List<Unit> enemy)
        { Clear(); foreach (var u in player) AddUnit(u); foreach (var u in enemy) AddUnit(u); }

        public int Count => unitToCell.Count;
    }

    public class MovementCache
    {
        private Dictionary<Unit, List<Point>> cache = new();
        private Dictionary<Unit, int> points = new();
        private bool dirty = false;

        public List<Point> GetMovableCells(Unit u, Func<Unit, List<Point>> calc)
        {
            bool recalc = dirty || !cache.ContainsKey(u) || !points.ContainsKey(u) || points[u] != u.GetMaxMovementPoints();
            if (recalc) { var c = calc(u); cache[u] = c; points[u] = u.GetMaxMovementPoints(); return c; }
            return cache[u];
        }

        public void Invalidate() => dirty = true;
        public void Clear() { cache.Clear(); points.Clear(); dirty = false; }
        public void RemoveUnit(Unit u) { cache.Remove(u); points.Remove(u); }
        public void RefreshAll() { cache.Clear(); points.Clear(); dirty = false; }
    }

    public class OptimizedUnitManager
    {
        public UnitSpatialHash SpatialHash { get; } = new();
        public MovementCache MovementCache { get; } = new();

        public void InitializeForMission(List<Unit> player, List<Unit> enemy)
        { SpatialHash.RebuildFromLists(player, enemy); MovementCache.Clear(); }

        public void OnUnitMoved(Unit u, Point c) => SpatialHash.MoveUnit(u, c, u.Floor);
        public void OnUnitMoved(Unit u, Point c, int floor) => SpatialHash.MoveUnit(u, c, floor);
        public void OnUnitDied(Unit u) { SpatialHash.RemoveUnit(u); MovementCache.RemoveUnit(u); }
        public void OnWallsDestroyed() => MovementCache.Invalidate();
        public void OnNewTurn() => MovementCache.RefreshAll();
    }
}
