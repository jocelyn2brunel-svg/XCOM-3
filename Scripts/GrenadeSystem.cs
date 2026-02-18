using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Types de grenades disponibles
    /// </summary>
    public enum GrenadeType
    {
        Frag,           // Fragmentation - dégâts moyens, rayon moyen
        Incendiary,     // Incendiaire - dégâts sur la durée, zone persistante
        Smoke,          // Fumigène - bloque la vue
        Flashbang,      // Assourdit et aveugle
        EMP,            // Désactive les systèmes électroniques
        HE,             // High Explosive - détruit les murs, creuse le sol
        Plasma,         // Plasma alien - dégâts massifs
        SatchelC4       // Charge C4 posable avec détonation à distance
    }

    /// <summary>
    /// Données d'une grenade
    /// </summary>
    public class GrenadeData
    {
        public string Name;
        public GrenadeType Type;
        public int Damage;
        public int Radius;              // Rayon d'explosion en cases
        public bool DestroyWalls;       // Peut détruire les murs
        public bool DigsTerrain;        // Peut creuser le sol
        public int DigDepth;            // Profondeur du cratère (0-3)
        public int AOCost;              // Coût en points d'action

        public GrenadeData(string name, GrenadeType type, int damage, int radius,
                          bool destroyWalls = false, bool digsTerrain = false,
                          int digDepth = 0, int aoCost = 1)
        {
            Name = name;
            Type = type;
            Damage = damage;
            Radius = radius;
            DestroyWalls = destroyWalls;
            DigsTerrain = digsTerrain;
            DigDepth = digDepth;
            AOCost = aoCost;
        }
    }

    /// <summary>
    /// Une grenade en vol
    /// </summary>
    public class Grenade
    {
        public GrenadeData Data;
        public Vector3 Position;
        public Vector3 StartPosition;
        public Vector3 TargetPosition;
        public float Progress;          // 0.0 à 1.0
        public Unit Thrower;
        public int TargetFloor;         // Étage ciblé pour l'explosion
        public float ArcHeight;         // Hauteur de l'arc parabolique
        public float FlightDurationSeconds; // Durée totale du vol pour une lecture visuelle claire
        public bool EmitsLight;         // Indique si l'objet émet de la lumière pendant le vol

        public Grenade(GrenadeData data, Vector3 start, Vector3 target, Unit thrower, int targetFloor)
        {
            Data = data;
            Position = start;
            StartPosition = start;
            TargetPosition = target;
            Progress = 0f;
            Thrower = thrower;
            TargetFloor = targetFloor;
            EmitsLight = false;

            // Calculer la hauteur de l'arc selon la distance
            float distance = Vector3.Distance(start, target);
            ArcHeight = Math.Min(distance * 0.5f, 8f);
            FlightDurationSeconds = MathHelper.Clamp(0.45f + distance * 0.018f, 0.55f, 1.2f);
        }

        /// <summary>
        /// Calcule la position actuelle sur l'arc parabolique
        /// </summary>
        public Vector3 GetCurrentPosition()
        {
            Vector3 linearPos = Vector3.Lerp(StartPosition, TargetPosition, Progress);

            // Ajouter l'arc parabolique
            float arcProgress = 4f * Progress * (1f - Progress);
            float height = arcProgress * ArcHeight;

            return linearPos + new Vector3(0, height, 0);
        }
    }

    /// <summary>
    /// Zone de cratère créée par une explosion
    /// </summary>
    public class Crater
    {
        public Point Cell;
        public int Depth;               // Profondeur (1-3)
        public float Age;               // Âge du cratère pour animation

        public Crater(Point cell, int depth)
        {
            Cell = cell;
            Depth = depth;
            Age = 0f;
        }
    }

    /// <summary>
    /// Gère les explosions et leurs effets
    /// </summary>
    public class ExplosionManager
    {
        private Random random;

        public ExplosionManager(Random rng)
        {
            random = rng;
        }

        /// <summary>
        /// Calcule toutes les cases affectées par une explosion
        /// </summary>
        public List<Point> GetExplosionCells(Point center, int radius)
        {
            List<Point> cells = new List<Point>();

            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                for (int y = center.Y - radius; y <= center.Y + radius; y++)
                {
                    Point cell = new Point(x, y);

                    // Vérifier la distance (cercle, pas carré)
                    float distance = Vector2.Distance(
                        new Vector2(center.X, center.Y),
                        new Vector2(x, y)
                    );

                    if (distance <= radius)
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// Calcule la distance volumétrique (sphère) entre deux cellules en tenant compte des étages.
        /// </summary>
        public float CalculateSphericalDistance(Point center, int centerFloor, Point target, int targetFloor)
        {
            float dx = target.X - center.X;
            float dy = target.Y - center.Y;
            float dz = targetFloor - centerFloor;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// Applique les dégâts d'explosion à une unité selon la distance sphérique
        /// </summary>
        public int CalculateExplosionDamage(int baseDamage, Point explosionCenter, int explosionFloor, Point unitCell, int unitFloor, int radius)
        {
            float distance = CalculateSphericalDistance(explosionCenter, explosionFloor, unitCell, unitFloor);

            // Dégâts dégressifs avec la distance
            float damageMultiplier = 1f - (distance / (radius + 1));
            damageMultiplier = Math.Max(0.2f, damageMultiplier); // Minimum 20% des dégâts

            return (int)(baseDamage * damageMultiplier);
        }

        /// <summary>
        /// Détermine quels murs sont détruits par l'explosion
        /// </summary>
        public List<WallSegment> GetDestroyedWalls(HashSet<WallSegment> allWalls, Point center, int radius)
        {
            List<WallSegment> destroyed = new List<WallSegment>();

            foreach (var wall in allWalls)
            {
                // Vérifier si le mur est dans le rayon d'explosion
                Vector2 wallCenter = new Vector2(
                    (wall.Start.X + wall.End.X) / 2f,
                    (wall.Start.Y + wall.End.Y) / 2f
                );

                float distance = Vector2.Distance(
                    new Vector2(center.X, center.Y),
                    wallCenter
                );

                if (distance <= radius)
                {
                    // Chance de destruction selon la distance
                    float destroyChance = 1f - (distance / (radius + 1));

                    if (random.Next(100) < destroyChance * 100)
                    {
                        destroyed.Add(wall);
                    }
                }
            }

            return destroyed;
        }

        /// <summary>
        /// Crée des cratères selon la puissance de l'explosion
        /// </summary>
        public List<Crater> CreateCraters(Point center, int digDepth, int radius)
        {
            List<Crater> craters = new List<Crater>();

            if (digDepth == 0) return craters;

            // Cratère principal au centre
            craters.Add(new Crater(center, digDepth));

            // Cratères secondaires autour
            List<Point> explosionCells = GetExplosionCells(center, radius / 2);

            foreach (var cell in explosionCells)
            {
                if (cell == center) continue;

                float distance = Vector2.Distance(
                    new Vector2(center.X, center.Y),
                    new Vector2(cell.X, cell.Y)
                );

                // Profondeur diminue avec la distance
                int cellDepth = Math.Max(1, digDepth - (int)distance);

                if (cellDepth > 0 && random.Next(100) < 50)
                {
                    craters.Add(new Crater(cell, cellDepth));
                }
            }

            return craters;
        }
    }

    /// <summary>
    /// Calcule la trajectoire de lancer d'une grenade
    /// </summary>
    public class ThrowTrajectoryCalculator
    {
        /// <summary>
        /// Vérifie si une case est dans la portée de lancer
        /// </summary>
        public static bool IsInThrowRange(Point from, Point to, int maxRange)
        {
            int distance = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
            return distance <= maxRange;
        }

        /// <summary>
        /// Calcule les points de la trajectoire pour affichage
        /// </summary>
        public static List<Vector3> CalculateArcPoints(Vector3 start, Vector3 end, int numPoints = 20)
        {
            List<Vector3> points = new List<Vector3>();

            float distance = Vector3.Distance(start, end);
            float arcHeight = Math.Min(distance * 0.5f, 8f);

            for (int i = 0; i <= numPoints; i++)
            {
                float t = i / (float)numPoints;
                Vector3 linearPos = Vector3.Lerp(start, end, t);

                // Arc parabolique
                float arcProgress = 4f * t * (1f - t);
                float height = arcProgress * arcHeight;

                points.Add(linearPos + new Vector3(0, height, 0));
            }

            return points;
        }

        /// <summary>
        /// Obtient toutes les cases dans la portée de lancer
        /// </summary>
        public static List<Point> GetThrowableCells(Point from, int maxRange, int gridWidth, int gridHeight)
        {
            List<Point> cells = new List<Point>();

            for (int x = from.X - maxRange; x <= from.X + maxRange; x++)
            {
                for (int y = from.Y - maxRange; y <= from.Y + maxRange; y++)
                {
                    if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                        continue;

                    Point cell = new Point(x, y);

                    if (IsInThrowRange(from, cell, maxRange))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// Calcule le rayon d'effet visuel pour affichage
        /// </summary>
        public static List<Point> GetExplosionPreview(Point center, int radius, int gridWidth, int gridHeight)
        {
            List<Point> cells = new List<Point>();

            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                for (int y = center.Y - radius; y <= center.Y + radius; y++)
                {
                    if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                        continue;

                    float distance = Vector2.Distance(
                        new Vector2(center.X, center.Y),
                        new Vector2(x, y)
                    );

                    if (distance <= radius)
                    {
                        cells.Add(new Point(x, y));
                    }
                }
            }

            return cells;
        }
    }
}
