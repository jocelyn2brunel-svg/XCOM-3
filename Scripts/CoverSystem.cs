using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Type de couverture
    /// </summary>
    public enum CoverType
    {
        None,       // Pas de couverture
        Half,       // Couverture partielle (1 côté)
        Full        // Couverture complète (2+ côtés)
    }

    /// <summary>
    /// Direction de la couverture
    /// </summary>
    [Flags]
    public enum CoverDirection
    {
        None = 0,
        North = 1 << 0,  // Haut
        South = 1 << 1,  // Bas
        East = 1 << 2,   // Droite
        West = 1 << 3    // Gauche
    }

    /// <summary>
    /// Données de couverture pour une case
    /// </summary>
    public class CoverData
    {
        public CoverType Type { get; set; }
        public CoverDirection Directions { get; set; }
        public int DefenseBonus { get; set; }
        public bool IsFlankable { get; set; }

        public CoverData()
        {
            Type = CoverType.None;
            Directions = CoverDirection.None;
            DefenseBonus = 0;
            IsFlankable = true;
        }

        public bool HasCoverFrom(CoverDirection direction)
        {
            return (Directions & direction) != 0;
        }
    }

    /// <summary>
    /// Système de couverture tactique style XCOM
    /// </summary>
    public class CoverSystem
    {
        private int gridWidth;
        private int gridHeight;
        private HashSet<WallSegment> walls;
        private Dictionary<Point, CoverData> coverCache;
        private Func<Point, Unit> getUnitAtCell;

        // Bonus de défense
        public const int HALF_COVER_BONUS = 20;   // +20% esquive
        public const int FULL_COVER_BONUS = 40;   // +40% esquive

        public CoverSystem(int width, int height, HashSet<WallSegment> wallSegments, Func<Point, Unit> unitGetter)
        {
            gridWidth = width;
            gridHeight = height;
            walls = wallSegments ?? new HashSet<WallSegment>();
            getUnitAtCell = unitGetter;
            coverCache = new Dictionary<Point, CoverData>();
            
            RebuildCoverCache();
        }

        /// <summary>
        /// Met à jour le système avec de nouvelles données
        /// </summary>
        public void UpdateGrid(int width, int height, HashSet<WallSegment> wallSegments)
        {
            gridWidth = width;
            gridHeight = height;
            walls = wallSegments ?? new HashSet<WallSegment>();
            RebuildCoverCache();
        }

        /// <summary>
        /// Reconstruit le cache de couverture
        /// </summary>
        public void RebuildCoverCache()
        {
            coverCache.Clear();

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Point cell = new Point(x, y);
                    coverCache[cell] = AnalyzeCoverAt(cell);
                }
            }

            Console.WriteLine($"[COVER] Cache rebuilt: {coverCache.Count} cells analyzed");
        }

        /// <summary>
        /// Analyse la couverture disponible à une position
        /// </summary>
        private CoverData AnalyzeCoverAt(Point cell)
        {
            CoverData cover = new CoverData();
            CoverDirection directions = CoverDirection.None;

            // Vérifier chaque direction
            if (HasWallToNorth(cell)) directions |= CoverDirection.North;
            if (HasWallToSouth(cell)) directions |= CoverDirection.South;
            if (HasWallToEast(cell)) directions |= CoverDirection.East;
            if (HasWallToWest(cell)) directions |= CoverDirection.West;

            cover.Directions = directions;

            // Compter les côtés protégés
            int protectedSides = CountProtectedSides(directions);

            // Déterminer le type
            if (protectedSides == 0)
            {
                cover.Type = CoverType.None;
                cover.DefenseBonus = 0;
            }
            else if (protectedSides == 1)
            {
                cover.Type = CoverType.Half;
                cover.DefenseBonus = HALF_COVER_BONUS;
            }
            else
            {
                cover.Type = CoverType.Full;
                cover.DefenseBonus = FULL_COVER_BONUS;
            }

            cover.IsFlankable = protectedSides < 4;

            return cover;
        }

        /// <summary>
        /// Vérifie s'il y a un mur au nord
        /// </summary>
        private bool HasWallToNorth(Point cell)
        {
            return walls.Any(w =>
                w.IsHorizontal &&
                w.Start.Y == cell.Y &&
                cell.X >= w.Start.X &&
                cell.X < w.End.X
            );
        }

        /// <summary>
        /// Vérifie s'il y a un mur au sud
        /// </summary>
        private bool HasWallToSouth(Point cell)
        {
            return walls.Any(w =>
                w.IsHorizontal &&
                w.Start.Y == cell.Y + 1 &&
                cell.X >= w.Start.X &&
                cell.X < w.End.X
            );
        }

        /// <summary>
        /// Vérifie s'il y a un mur à l'est
        /// </summary>
        private bool HasWallToEast(Point cell)
        {
            return walls.Any(w =>
                !w.IsHorizontal &&
                w.Start.X == cell.X + 1 &&
                cell.Y >= w.Start.Y &&
                cell.Y < w.End.Y
            );
        }

        /// <summary>
        /// Vérifie s'il y a un mur à l'ouest
        /// </summary>
        private bool HasWallToWest(Point cell)
        {
            return walls.Any(w =>
                !w.IsHorizontal &&
                w.Start.X == cell.X &&
                cell.Y >= w.Start.Y &&
                cell.Y < w.End.Y
            );
        }

        /// <summary>
        /// Compte le nombre de côtés protégés
        /// </summary>
        private int CountProtectedSides(CoverDirection directions)
        {
            int count = 0;
            if ((directions & CoverDirection.North) != 0) count++;
            if ((directions & CoverDirection.South) != 0) count++;
            if ((directions & CoverDirection.East) != 0) count++;
            if ((directions & CoverDirection.West) != 0) count++;
            return count;
        }

        /// <summary>
        /// Obtient la couverture à une position
        /// </summary>
        public CoverData GetCoverAt(Point cell)
        {
            if (coverCache.TryGetValue(cell, out CoverData cover))
                return cover;
            
            return new CoverData();
        }

        /// <summary>
        /// Vérifie si une unité est flanquée
        /// </summary>
        public bool IsUnitFlanked(Unit unit, Unit attacker)
        {
            if (unit.CoverType == CoverType.None)
                return false;

            CoverData cover = GetCoverAt(unit.Cell);
            if (!cover.IsFlankable)
                return false;

            // Direction de l'attaque
            CoverDirection attackDirection = GetAttackDirection(unit.Cell, attacker.Cell);

            // Flanqué si l'attaque vient d'un côté non protégé
            return !cover.HasCoverFrom(attackDirection);
        }

        /// <summary>
        /// Détermine la direction d'une attaque
        /// </summary>
        private CoverDirection GetAttackDirection(Point defender, Point attacker)
        {
            int dx = attacker.X - defender.X;
            int dy = attacker.Y - defender.Y;

            // Direction dominante
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                return dx > 0 ? CoverDirection.East : CoverDirection.West;
            }
            else
            {
                return dy > 0 ? CoverDirection.South : CoverDirection.North;
            }
        }

        /// <summary>
        /// Cases avec couverture dans un rayon
        /// </summary>
        public List<Point> GetCoverCellsInRange(Point center, int range)
        {
            List<Point> coverCells = new List<Point>();

            for (int x = Math.Max(0, center.X - range); x <= Math.Min(gridWidth - 1, center.X + range); x++)
            {
                for (int y = Math.Max(0, center.Y - range); y <= Math.Min(gridHeight - 1, center.Y + range); y++)
                {
                    Point cell = new Point(x, y);
                    
                    int dist = Math.Abs(cell.X - center.X) + Math.Abs(cell.Y - center.Y);
                    if (dist <= range)
                    {
                        CoverData cover = GetCoverAt(cell);
                        if (cover.Type != CoverType.None)
                        {
                            coverCells.Add(cell);
                        }
                    }
                }
            }

            return coverCells;
        }

        /// <summary>
        /// Bonus de défense effectif contre un attaquant
        /// </summary>
        public int GetEffectiveDefenseBonus(Unit defender, Unit attacker)
        {
            if (defender.CoverType == CoverType.None)
                return 0;

            // Si flanqué, pas de bonus
            if (IsUnitFlanked(defender, attacker))
            {
                Console.WriteLine($"[COVER] {defender.Name} is FLANKED by {attacker.Name}!");
                return 0;
            }

            CoverData cover = GetCoverAt(defender.Cell);
            return cover.DefenseBonus;
        }

        /// <summary>
        /// Meilleure position de couverture accessible
        /// </summary>
        public Point? GetBestCoverPosition(Point currentPos, List<Point> reachableCells)
        {
            Point? bestCell = null;
            int bestScore = -1;

            foreach (Point cell in reachableCells)
            {
                CoverData cover = GetCoverAt(cell);
                int score = 0;

                switch (cover.Type)
                {
                    case CoverType.Half:
                        score = 1;
                        break;
                    case CoverType.Full:
                        score = 2;
                        break;
                }

                // Préférer les positions plus éloignées
                int distance = Math.Abs(cell.X - currentPos.X) + Math.Abs(cell.Y - currentPos.Y);
                score += distance / 2;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = cell;
                }
            }

            return bestCell;
        }

        /// <summary>
        /// Description textuelle de la couverture
        /// </summary>
        public string GetCoverDescription(Point cell)
        {
            CoverData cover = GetCoverAt(cell);
            
            return cover.Type switch
            {
                CoverType.None => "NO COVER",
                CoverType.Half => $"HALF COVER (+{HALF_COVER_BONUS}% Defense)",
                CoverType.Full => $"FULL COVER (+{FULL_COVER_BONUS}% Defense)",
                _ => "UNKNOWN"
            };
        }

        /// <summary>
        /// Directions de couverture en texte
        /// </summary>
        public string GetCoverDirectionsText(Point cell)
        {
            CoverData cover = GetCoverAt(cell);
            List<string> dirs = new List<string>();

            if (cover.HasCoverFrom(CoverDirection.North)) dirs.Add("N");
            if (cover.HasCoverFrom(CoverDirection.South)) dirs.Add("S");
            if (cover.HasCoverFrom(CoverDirection.East)) dirs.Add("E");
            if (cover.HasCoverFrom(CoverDirection.West)) dirs.Add("W");

            return dirs.Count > 0 ? string.Join(", ", dirs) : "None";
        }
    }
}
