using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class UnitSpatialHash
    {
        private Dictionary<Point, Unit> cellToUnit = new();
        private Dictionary<Unit, Point> unitToCell = new();

        public void AddUnit(Unit u)
        {
            if (unitToCell.ContainsKey(u)) RemoveUnit(u);
            cellToUnit[u.Cell] = u; unitToCell[u] = u.Cell;
        }

        public void RemoveUnit(Unit u)
        {
            if (unitToCell.TryGetValue(u, out Point c)) { cellToUnit.Remove(c); unitToCell.Remove(u); }
        }

        public void MoveUnit(Unit u, Point newCell)
        {
            if (unitToCell.TryGetValue(u, out Point old)) cellToUnit.Remove(old);
            cellToUnit[newCell] = u; unitToCell[u] = newCell; u.Cell = newCell;
        }

        public Unit GetUnitAt(Point c) => cellToUnit.TryGetValue(c, out Unit u) ? u : null;
        public bool IsCellOccupied(Point c) => cellToUnit.ContainsKey(c);

        public List<Unit> GetUnitsInRadius(Point center, int r)
        {
            var list = new List<Unit>();
            for (int x = center.X - r; x <= center.X + r; x++)
                for (int y = center.Y - r; y <= center.Y + r; y++)
                    if (Vector2.Distance(new Vector2(center.X, center.Y), new Vector2(x, y)) <= r)
                        if (GetUnitAt(new Point(x, y)) is Unit u) list.Add(u);
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

        public void OnUnitMoved(Unit u, Point c) => SpatialHash.MoveUnit(u, c);
        public void OnUnitDied(Unit u) { SpatialHash.RemoveUnit(u); MovementCache.RemoveUnit(u); }
        public void OnWallsDestroyed() => MovementCache.Invalidate();
        public void OnNewTurn() => MovementCache.RefreshAll();
    }
}
