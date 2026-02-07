using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Spatial hash grid pour lookup ultra-rapide des unités
    /// OPTIMISATION : Réduit GetUnitAtCell de O(n) à O(1)
    /// </summary>
    public class UnitSpatialHash
    {
        private Dictionary<Point, Unit> cellToUnit;
        private Dictionary<Unit, Point> unitToCell;
        
        public UnitSpatialHash()
        {
            cellToUnit = new Dictionary<Point, Unit>();
            unitToCell = new Dictionary<Unit, Point>();
        }
        
        /// <summary>
        /// Ajoute une unité au spatial hash
        /// </summary>
        public void AddUnit(Unit unit)
        {
            if (unitToCell.ContainsKey(unit))
            {
                // Unité déjà présente, la retirer d'abord
                RemoveUnit(unit);
            }
            
            cellToUnit[unit.Cell] = unit;
            unitToCell[unit] = unit.Cell;
        }
        
        /// <summary>
        /// Retire une unité du spatial hash
        /// </summary>
        public void RemoveUnit(Unit unit)
        {
            if (unitToCell.TryGetValue(unit, out Point cell))
            {
                cellToUnit.Remove(cell);
                unitToCell.Remove(unit);
            }
        }
        
        /// <summary>
        /// Déplace une unité d'une cellule à une autre
        /// </summary>
        public void MoveUnit(Unit unit, Point newCell)
        {
            // Retirer de l'ancienne position
            if (unitToCell.TryGetValue(unit, out Point oldCell))
            {
                cellToUnit.Remove(oldCell);
            }
            
            // Ajouter à la nouvelle position
            cellToUnit[newCell] = unit;
            unitToCell[unit] = newCell;
            unit.Cell = newCell;
        }
        
        /// <summary>
        /// Obtient l'unité à une cellule donnée (O(1) au lieu de O(n))
        /// </summary>
        public Unit GetUnitAt(Point cell)
        {
            return cellToUnit.TryGetValue(cell, out Unit unit) ? unit : null;
        }
        
        /// <summary>
        /// Vérifie si une cellule est occupée
        /// </summary>
        public bool IsCellOccupied(Point cell)
        {
            return cellToUnit.ContainsKey(cell);
        }
        
        /// <summary>
        /// Obtient toutes les unités dans un rayon donné (pour explosions)
        /// </summary>
        public List<Unit> GetUnitsInRadius(Point center, int radius)
        {
            List<Unit> unitsInRadius = new List<Unit>();
            
            // Parcourir seulement les cellules dans le rayon
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                for (int y = center.Y - radius; y <= center.Y + radius; y++)
                {
                    Point cell = new Point(x, y);
                    
                    // Vérifier si dans le cercle
                    float distance = Vector2.Distance(
                        new Vector2(center.X, center.Y),
                        new Vector2(x, y)
                    );
                    
                    if (distance <= radius)
                    {
                        Unit unit = GetUnitAt(cell);
                        if (unit != null)
                            unitsInRadius.Add(unit);
                    }
                }
            }
            
            return unitsInRadius;
        }
        
        /// <summary>
        /// Nettoie toutes les unités
        /// </summary>
        public void Clear()
        {
            cellToUnit.Clear();
            unitToCell.Clear();
        }
        
        /// <summary>
        /// Reconstruit le hash à partir de listes d'unités
        /// À appeler au début de chaque mission
        /// </summary>
        public void RebuildFromLists(List<Unit> playerUnits, List<Unit> enemyUnits)
        {
            Clear();
            
            foreach (var unit in playerUnits)
                AddUnit(unit);
                
            foreach (var unit in enemyUnits)
                AddUnit(unit);
        }
        
        /// <summary>
        /// Obtient le nombre total d'unités
        /// </summary>
        public int Count => unitToCell.Count;
        
        /// <summary>
        /// Debugging - affiche l'état du spatial hash
        /// </summary>
        public void DebugPrint()
        {
            Console.WriteLine($"=== Spatial Hash Debug ===");
            Console.WriteLine($"Total units: {Count}");
            Console.WriteLine($"Occupied cells:");
            
            foreach (var kvp in cellToUnit)
            {
                Console.WriteLine($"  ({kvp.Key.X}, {kvp.Key.Y}) -> {kvp.Value.Name}");
            }
        }
    }
    
    /// <summary>
    /// Cache intelligent pour les cellules déplaçables
    /// OPTIMISATION : Évite de recalculer le pathfinding chaque frame
    /// </summary>
    public class MovementCache
    {
        private Dictionary<Unit, List<Point>> cache;
        private Dictionary<Unit, int> cachedMovementPoints;
        private bool isDirty;
        
        public MovementCache()
        {
            cache = new Dictionary<Unit, List<Point>>();
            cachedMovementPoints = new Dictionary<Unit, int>();
            isDirty = false;
        }
        
        /// <summary>
        /// Obtient les cellules déplaçables (du cache si possible)
        /// </summary>
        public List<Point> GetMovableCells(Unit unit, 
                                          Func<Unit, List<Point>> calculateFunc)
        {
            // Cache invalide si :
            // 1. Le cache est marqué dirty (murs détruits)
            // 2. L'unité n'est pas dans le cache
            // 3. Les points de mouvement ont changé
            
            bool needsRecalculation = isDirty || 
                                     !cache.ContainsKey(unit) ||
                                     !cachedMovementPoints.ContainsKey(unit) ||
                                     cachedMovementPoints[unit] != unit.GetMaxMovementPoints();
            
            if (needsRecalculation)
            {
                // Recalculer
                List<Point> cells = calculateFunc(unit);
                cache[unit] = cells;
                cachedMovementPoints[unit] = unit.GetMaxMovementPoints();
                
                return cells;
            }
            
            // Retourner depuis le cache
            return cache[unit];
        }
        
        /// <summary>
        /// Marque le cache comme invalide (ex: murs détruits)
        /// </summary>
        public void Invalidate()
        {
            isDirty = true;
        }
        
        /// <summary>
        /// Nettoie le cache pour une nouvelle mission
        /// </summary>
        public void Clear()
        {
            cache.Clear();
            cachedMovementPoints.Clear();
            isDirty = false;
        }
        
        /// <summary>
        /// Retire une unité du cache (ex: unité morte)
        /// </summary>
        public void RemoveUnit(Unit unit)
        {
            cache.Remove(unit);
            cachedMovementPoints.Remove(unit);
        }
        
        /// <summary>
        /// Nettoie le cache et recalcule lors du prochain accès
        /// </summary>
        public void RefreshAll()
        {
            cache.Clear();
            cachedMovementPoints.Clear();
            isDirty = false;
        }
    }
    
    /// <summary>
    /// Helper pour combiner le spatial hash et le cache
    /// Intégration facile dans Game1.cs
    /// </summary>
    public class OptimizedUnitManager
    {
        public UnitSpatialHash SpatialHash { get; private set; }
        public MovementCache MovementCache { get; private set; }
        
        public OptimizedUnitManager()
        {
            SpatialHash = new UnitSpatialHash();
            MovementCache = new MovementCache();
        }
        
        /// <summary>
        /// Initialise pour une nouvelle mission
        /// </summary>
        public void InitializeForMission(List<Unit> playerUnits, List<Unit> enemyUnits)
        {
            SpatialHash.RebuildFromLists(playerUnits, enemyUnits);
            MovementCache.Clear();
        }
        
        /// <summary>
        /// Appeler quand une unité se déplace
        /// </summary>
        public void OnUnitMoved(Unit unit, Point newCell)
        {
            SpatialHash.MoveUnit(unit, newCell);
            // Pas besoin d'invalider le cache de mouvement
        }
        
        /// <summary>
        /// Appeler quand une unité meurt
        /// </summary>
        public void OnUnitDied(Unit unit)
        {
            SpatialHash.RemoveUnit(unit);
            MovementCache.RemoveUnit(unit);
        }
        
        /// <summary>
        /// Appeler quand les murs sont détruits
        /// </summary>
        public void OnWallsDestroyed()
        {
            MovementCache.Invalidate();
        }
        
        /// <summary>
        /// Appeler au début d'un nouveau tour
        /// </summary>
        public void OnNewTurn()
        {
            // Rafraîchir le cache de mouvement car les PM ont été restaurés
            MovementCache.RefreshAll();
        }
    }
}
