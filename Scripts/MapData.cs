using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCOM_3
{
    /// <summary>
    /// Données d'une carte complète - sérialisable en JSON
    /// </summary>
    [Serializable]
    public class MapData
    {
        // Métadonnées
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Version { get; set; } = "1.0";

        // Dimensions
        public int GridWidth { get; set; }
        public int GridHeight { get; set; }
        public int CellSize { get; set; } = 2;
        public int FloorCount { get; set; } = 1;

        // Environnement
        public float TimeOfDay { get; set; } = 0.5f; // 0.0 = minuit, 0.5 = midi
        public string BiomeType { get; set; } = "Urban"; // Urban, Desert, Forest, Arctic, etc.

        // Murs (sérialisés comme liste de tuples)
        public List<WallSegmentData> Walls { get; set; } = new List<WallSegmentData>();

        // Zones de spawn
        public List<SpawnZone> PlayerSpawnZones { get; set; } = new List<SpawnZone>();
        public List<SpawnZone> EnemySpawnZones { get; set; } = new List<SpawnZone>();

        // Points d'intérêt
        public List<ObjectivePoint> Objectives { get; set; } = new List<ObjectivePoint>();
        public List<StairConnectionData> StairConnections { get; set; } = new List<StairConnectionData>();
        public List<BuildingFootprintData> Buildings { get; set; } = new List<BuildingFootprintData>();

        // Paramètres de mission
        public string SuggestedMissionType { get; set; } = "Tutorial";
        public int MaxPlayerUnits { get; set; } = 6;
        public int MaxEnemyUnits { get; set; } = 6;

        public MapData()
        {
            Name = "Untitled Map";
            Description = "A custom map";
            Author = "Unknown";
            CreatedDate = DateTime.Now;
        }

        /// <summary>
        /// Convertit les WallSegments du jeu en données sérialisables
        /// </summary>
        public void SetWalls(HashSet<WallSegment> wallSegments)
        {
            Walls.Clear();
            foreach (var wall in wallSegments)
            {
                Walls.Add(new WallSegmentData
                {
                    StartX = wall.Start.X,
                    StartY = wall.Start.Y,
                    EndX = wall.End.X,
                    EndY = wall.End.Y,
                    IsHorizontal = wall.IsHorizontal,
                    Type = (int)wall.Type
                });
            }
        }

        /// <summary>
        /// Convertit les données sérialisées en WallSegments du jeu
        /// </summary>
        public HashSet<WallSegment> GetWalls()
        {
            HashSet<WallSegment> wallSegments = new HashSet<WallSegment>();
            foreach (var wallData in Walls)
            {
                wallSegments.Add(new WallSegment(
                    new Point(wallData.StartX, wallData.StartY),
                    new Point(wallData.EndX, wallData.EndY),
                    wallData.IsHorizontal,
                    (WallType)wallData.Type
                ));
            }
            return wallSegments;
        }

        /// <summary>
        /// Génère des zones de spawn par défaut
        /// </summary>
        public void GenerateDefaultSpawnZones()
        {
            PlayerSpawnZones.Clear();
            EnemySpawnZones.Clear();

            // Zone joueur (bas de la carte)
            PlayerSpawnZones.Add(new SpawnZone
            {
                MinX = 2,
                MinY = GridHeight - 3,
                MaxX = Math.Min(GridWidth - 2, 12),
                MaxY = GridHeight - 2,
                MaxUnits = MaxPlayerUnits
            });

            // Zone ennemie (haut de la carte)
            EnemySpawnZones.Add(new SpawnZone
            {
                MinX = 2,
                MinY = 1,
                MaxX = Math.Min(GridWidth - 2, 12),
                MaxY = 2,
                MaxUnits = MaxEnemyUnits
            });
        }

        /// <summary>
        /// Sauvegarde la carte en JSON
        /// </summary>
        public void SaveToFile(string filepath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(filepath, json);
                Console.WriteLine($"[MAP] Saved to: {filepath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAP] Error saving: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Charge une carte depuis un fichier JSON
        /// </summary>
        public static MapData LoadFromFile(string filepath)
        {
            try
            {
                string json = File.ReadAllText(filepath);
                var map = JsonSerializer.Deserialize<MapData>(json);
                Console.WriteLine($"[MAP] Loaded: {map.Name} ({map.GridWidth}x{map.GridHeight})");
                return map;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAP] Error loading: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Valide la cohérence de la carte
        /// </summary>
        public bool Validate(out string error)
        {
            if (GridWidth < 10 || GridHeight < 10)
            {
                error = "Map too small (min 10x10)";
                return false;
            }

            if (GridWidth > 200 || GridHeight > 200)
            {
                error = "Map too large (max 200x200)";
                return false;
            }

            if (PlayerSpawnZones.Count == 0)
            {
                error = "No player spawn zones defined";
                return false;
            }

            if (EnemySpawnZones.Count == 0)
            {
                error = "No enemy spawn zones defined";
                return false;
            }

            if (FloorCount < 1 || FloorCount > 8)
            {
                error = "Invalid floor count (min 1, max 8)";
                return false;
            }

            error = null;
            return true;
        }
    }

    [Serializable]
    public class StairConnectionData
    {
        public int FromX { get; set; }
        public int FromY { get; set; }
        public int FromFloor { get; set; }
        public int ToX { get; set; }
        public int ToY { get; set; }
        public int ToFloor { get; set; }
        public bool Bidirectional { get; set; } = true;
    }

    [Serializable]
    public class BuildingFootprintData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int FloorCount { get; set; } = 1;
        public int BasementCount { get; set; } = 0;
    }

    /// <summary>
    /// Données d'un mur sérialisables (pas de Point car pas JSON-friendly)
    /// </summary>
    [Serializable]
    public struct WallSegmentData
    {
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int EndX { get; set; }
        public int EndY { get; set; }
        public bool IsHorizontal { get; set; }
        public int Type { get; set; } // AJOUTE CETTE LIGNE (0=Full, 1=Window, etc.)
    }

    /// <summary>
    /// Zone de spawn rectangulaire
    /// </summary>
    [Serializable]
    public class SpawnZone
    {
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public int MaxUnits { get; set; } = 6;

        public bool Contains(Point cell)
        {
            return cell.X >= MinX && cell.X <= MaxX &&
                   cell.Y >= MinY && cell.Y <= MaxY;
        }

        public Point GetRandomPoint(Random random)
        {
            return new Point(
                random.Next(MinX, MaxX + 1),
                random.Next(MinY, MaxY + 1)
            );
        }
    }

    /// <summary>
    /// Point d'objectif pour les missions
    /// </summary>
    [Serializable]
    public class ObjectivePoint
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Type { get; set; } // "Extraction", "Defense", "Capture", etc.
        public string Description { get; set; }
    }

    /// <summary>
    /// Gestionnaire de cartes - catalogue et chargement
    /// </summary>
    public static class MapCatalog
    {
        private static readonly string MapsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "XCOM3", "Maps"
        );

        static MapCatalog()
        {
            // Créer le dossier s'il n'existe pas
            if (!Directory.Exists(MapsDirectory))
            {
                Directory.CreateDirectory(MapsDirectory);
                Console.WriteLine($"[MAP CATALOG] Created directory: {MapsDirectory}");
            }
        }

        /// <summary>
        /// Liste toutes les cartes disponibles
        /// </summary>
        public static List<MapData> GetAvailableMaps()
        {
            List<MapData> maps = new List<MapData>();

            try
            {
                var files = Directory.GetFiles(MapsDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var map = MapData.LoadFromFile(file);
                        maps.Add(map);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MAP CATALOG] Failed to load {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAP CATALOG] Error reading directory: {ex.Message}");
            }

            return maps.OrderBy(m => m.Name).ToList();
        }

        /// <summary>
        /// Sauvegarde une carte dans le catalogue
        /// </summary>
        public static void SaveMap(MapData map)
        {
            string filename = SanitizeFilename(map.Name) + ".json";
            string filepath = Path.Combine(MapsDirectory, filename);
            map.SaveToFile(filepath);
        }

        /// <summary>
        /// Charge une carte par son nom
        /// </summary>
        public static MapData LoadMap(string mapName)
        {
            string filename = SanitizeFilename(mapName) + ".json";
            string filepath = Path.Combine(MapsDirectory, filename);

            if (!File.Exists(filepath))
            {
                Console.WriteLine($"[MAP CATALOG] Map not found: {mapName}");
                return null;
            }

            return MapData.LoadFromFile(filepath);
        }

        /// <summary>
        /// Supprime une carte du catalogue
        /// </summary>
        public static bool DeleteMap(string mapName)
        {
            try
            {
                string filename = SanitizeFilename(mapName) + ".json";
                string filepath = Path.Combine(MapsDirectory, filename);

                if (File.Exists(filepath))
                {
                    File.Delete(filepath);
                    Console.WriteLine($"[MAP CATALOG] Deleted: {mapName}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAP CATALOG] Error deleting: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Nettoie un nom de fichier
        /// </summary>
        private static string SanitizeFilename(string filename)
        {
            char[] invalids = Path.GetInvalidFileNameChars();
            return string.Join("_", filename.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        }

        /// <summary>
        /// Obtient le chemin du dossier de cartes
        /// </summary>
        public static string GetMapsDirectory() => MapsDirectory;
    }
}
