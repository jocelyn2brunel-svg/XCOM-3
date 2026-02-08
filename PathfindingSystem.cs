using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Système de pathfinding A*, ligne de vue et calcul de mouvement
    /// </summary>
    public class PathfindingSystem
    {
        // ═══════════════════════════════════════════════════════════════════════
        // PROPRIÉTÉS
        // ═══════════════════════════════════════════════════════════════════════

        private int gridWidth;
        private int gridHeight;
        private HashSet<WallSegment> wallSegments;
        private Func<Point, Unit> getUnitAtCell;

        // ═══════════════════════════════════════════════════════════════════════
        // CONSTRUCTEUR
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Crée un nouveau système de pathfinding
        /// </summary>
        /// <param name="gridWidth">Largeur de la grille</param>
        /// <param name="gridHeight">Hauteur de la grille</param>
        /// <param name="wallSegments">Référence aux murs de la carte</param>
        /// <param name="getUnitAtCell">Fonction pour obtenir l'unité à une position</param>
        public PathfindingSystem(int gridWidth, int gridHeight, 
            HashSet<WallSegment> wallSegments, 
            Func<Point, Unit> getUnitAtCell)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.wallSegments = wallSegments;
            this.getUnitAtCell = getUnitAtCell;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MISE À JOUR DE LA GRILLE
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Met à jour les dimensions de la grille (appelé lors du chargement d'une nouvelle carte)
        /// </summary>
        public void UpdateGrid(int width, int height)
        {
            gridWidth = width;
            gridHeight = height;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // A* PATHFINDING
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Trouve le chemin le plus court entre deux points en utilisant A*
        /// </summary>
        /// <param name="start">Point de départ</param>
        /// <param name="goal">Point d'arrivée</param>
        /// <param name="maxCost">Coût maximum (PM disponibles)</param>
        /// <param name="movingUnit">Unité qui se déplace (pour ignorer sa propre position)</param>
        /// <returns>Liste des points du chemin (sans le point de départ)</returns>
        public List<Point> FindPath(Point start, Point goal, int maxCost, Unit movingUnit)
        {
            if (start == goal) 
                return new List<Point>();

            // Structures pour A*
            var openSet = new List<Point> { start };
            var cameFrom = new Dictionary<Point, Point>();
            var gScore = new Dictionary<Point, int> { [start] = 0 };
            var fScore = new Dictionary<Point, int> { [start] = Heuristic(start, goal) };

            while (openSet.Count > 0)
            {
                // Trouver le nœud avec le meilleur fScore
                Point current = openSet.OrderBy(p => fScore.GetValueOrDefault(p, int.MaxValue)).First();

                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                openSet.Remove(current);

                // Voisins (4 directions)
                Point[] neighbors = new[]
                {
                    new Point(current.X - 1, current.Y), // Ouest
                    new Point(current.X + 1, current.Y), // Est
                    new Point(current.X, current.Y - 1), // Nord
                    new Point(current.X, current.Y + 1)  // Sud
                };

                foreach (Point neighbor in neighbors)
                {
                    // Ignorer les cases non marchables (sauf la destination)
                    if (neighbor != goal && !IsWalkable(neighbor, movingUnit))
                        continue;

                    // Vérifier si occupé par une autre unité
                    Unit unitAtNeighbor = getUnitAtCell(neighbor);
                    if (neighbor != goal && unitAtNeighbor != null && unitAtNeighbor != movingUnit)
                        continue;

                    // Vérifier les murs
                    if (HasWallBetween(current, neighbor))
                        continue;

                    int tentativeGScore = gScore[current] + 1;

                    if (tentativeGScore < gScore.GetValueOrDefault(neighbor, int.MaxValue))
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + Heuristic(neighbor, goal);

                        if (!openSet.Contains(neighbor))
                            openSet.Add(neighbor);
                    }
                }
            }

            return new List<Point>(); // Pas de chemin trouvé
        }

        /// <summary>
        /// Heuristique de distance de Manhattan
        /// </summary>
        private int Heuristic(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        /// <summary>
        /// Reconstruit le chemin à partir du dictionnaire cameFrom
        /// </summary>
        private List<Point> ReconstructPath(Dictionary<Point, Point> cameFrom, Point current)
        {
            var path = new List<Point> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }
            path.RemoveAt(0); // Enlever la position de départ
            return path;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CALCUL DES CASES ATTEIGNABLES
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calcule toutes les cases atteignables par une unité
        /// </summary>
        /// <param name="unit">Unité dont on calcule les mouvements possibles</param>
        /// <returns>Liste des cases atteignables</returns>
        public List<Point> GetMovableCells(Unit unit)
        {
            var cells = new List<Point>();
            
            if (unit == null || unit.MovementPoints <= 0) 
                return cells;

            int maxRange = unit.MovementPoints;

            // Parcourir une zone carrée autour de l'unité
            for (int x = unit.Cell.X - maxRange; x <= unit.Cell.X + maxRange; x++)
            {
                for (int y = unit.Cell.Y - maxRange; y <= unit.Cell.Y + maxRange; y++)
                {
                    Point targetCell = new Point(x, y);

                    // Ignorer la case de l'unité elle-même
                    if (targetCell == unit.Cell)
                        continue;

                    // Vérifier que c'est dans la grille
                    if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                        continue;

                    // Vérifier que la case est marchable
                    if (!IsWalkable(targetCell))
                        continue;

                    // Calculer le chemin pour vérifier la distance réelle
                    var path = FindPath(unit.Cell, targetCell, maxRange, unit);

                    if (path.Count > 0 && path.Count <= maxRange)
                    {
                        cells.Add(targetCell);
                    }
                }
            }

            return cells;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LIGNE DE VUE (LINE OF SIGHT)
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Vérifie s'il y a une ligne de vue entre deux points (algorithme de Bresenham)
        /// </summary>
        /// <param name="from">Point de départ</param>
        /// <param name="to">Point d'arrivée</param>
        /// <returns>True si ligne de vue dégagée</returns>
        public bool HasLineOfSight(Point from, Point to)
        {
            // Algorithme de Bresenham pour ligne de vue
            int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            Point current = new Point(x0, y0);
            Point previous = current;

            while (true)
            {
                // Vérifier s'il y a un mur entre les deux cases
                if (current != from && HasWallBetween(previous, current))
                    return false;

                if (current.X == x1 && current.Y == y1)
                    break;

                previous = current;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    current.X += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    current.Y += sy;
                }
            }

            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DÉTECTION DE MURS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Vérifie s'il y a un mur entre deux cases adjacentes
        /// </summary>
        /// <param name="from">Case de départ</param>
        /// <param name="to">Case d'arrivée (doit être adjacente)</param>
        /// <returns>True s'il y a un mur</returns>
        public bool HasWallBetween(Point from, Point to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            // Doivent être adjacentes (distance de Manhattan = 1)
            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return false;

            foreach (var wall in wallSegments)
            {
                if (dy == 1 && wall.IsHorizontal) // Mouvement vers le bas
                {
                    if (wall.Start.Y == to.Y &&
                        from.X >= wall.Start.X && from.X < wall.End.X)
                        return true;
                }
                else if (dy == -1 && wall.IsHorizontal) // Mouvement vers le haut
                {
                    if (wall.Start.Y == from.Y &&
                        from.X >= wall.Start.X && from.X < wall.End.X)
                        return true;
                }
                else if (dx == 1 && !wall.IsHorizontal) // Mouvement vers la droite
                {
                    if (wall.Start.X == to.X &&
                        from.Y >= wall.Start.Y && from.Y < wall.End.Y)
                        return true;
                }
                else if (dx == -1 && !wall.IsHorizontal) // Mouvement vers la gauche
                {
                    if (wall.Start.X == from.X &&
                        from.Y >= wall.Start.Y && from.Y < wall.End.Y)
                        return true;
                }
            }

            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // VOISINAGE ET MARCHABILITÉ
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Obtient les voisins accessibles d'une case (4 directions)
        /// </summary>
        /// <param name="cell">Case dont on veut les voisins</param>
        /// <returns>Liste des cases voisines accessibles</returns>
        public List<Point> GetNeighbors(Point cell)
        {
            List<Point> neighbors = new List<Point>();

            Point[] potentialNeighbors = new Point[]
            {
                new Point(cell.X, cell.Y - 1), // Nord
                new Point(cell.X, cell.Y + 1), // Sud
                new Point(cell.X - 1, cell.Y), // Ouest
                new Point(cell.X + 1, cell.Y)  // Est
            };

            foreach (var neighbor in potentialNeighbors)
            {
                // Vérifier que le voisin est dans la grille
                if (neighbor.X >= 0 && neighbor.X < gridWidth &&
                    neighbor.Y >= 0 && neighbor.Y < gridHeight)
                {
                    // Vérifier qu'il n'y a pas de mur entre les deux cases
                    if (!HasWallBetween(cell, neighbor))
                    {
                        neighbors.Add(neighbor);
                    }
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Vérifie si une case est marchable (dans la grille et non occupée)
        /// </summary>
        /// <param name="cell">Case à vérifier</param>
        /// <param name="movingUnit">Unité qui se déplace (ignorée dans le calcul)</param>
        /// <returns>True si marchable</returns>
        public bool IsWalkable(Point cell, Unit movingUnit = null)
        {
            // Vérifier les limites de la grille
            if (cell.X < 0 || cell.Y < 0 || cell.X >= gridWidth || cell.Y >= gridHeight)
                return false;

            // Vérifier si occupé par une autre unité
            var unit = getUnitAtCell(cell);
            if (unit != null && unit != movingUnit)
                return false;

            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UTILITAIRES
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calcule la distance de Manhattan entre deux points
        /// </summary>
        public int ManhattanDistance(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        /// <summary>
        /// Vérifie si deux points sont adjacents (distance de Manhattan = 1)
        /// </summary>
        public bool AreAdjacent(Point a, Point b)
        {
            return ManhattanDistance(a, b) == 1;
        }

        /// <summary>
        /// Calcule le coût total d'un chemin
        /// </summary>
        public int GetPathCost(List<Point> path)
        {
            return path.Count;
        }
    }
}
