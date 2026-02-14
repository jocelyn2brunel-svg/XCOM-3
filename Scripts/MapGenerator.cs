using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Générateur procédural de cartes - produit des MapData
    /// </summary>
    public class MapGenerator
    {
        private Random random;
        private EdgeWallGenerator wallGenerator;

        public MapGenerator(Random rng)
        {
            random = rng;
            wallGenerator = new EdgeWallGenerator(rng);
        }

        /// <summary>
        /// Génère une carte aléatoire selon un pattern
        /// </summary>
        public MapData GenerateRandomMap(
            string missionType,
            int minWidth = 20,
            int maxWidth = 100,
            int minHeight = 20,
            int maxHeight = 100)
        {
            MapData map = new MapData
            {
                Name = $"{missionType} Map {DateTime.Now:yyyy-MM-dd HH:mm}",
                Description = $"Procedurally generated {missionType} map",
                Author = "Procedural Generator",
                CreatedDate = DateTime.Now,
                SuggestedMissionType = missionType,
                GridWidth = random.Next(minWidth, maxWidth),
                GridHeight = random.Next(minHeight, maxHeight),
                CellSize = 2,
                FloorCount = GetFloorCountForMission(missionType),
                TimeOfDay = (float)random.NextDouble()
            };

            // Choisir le pattern de murs selon la mission
            EdgeWallGenerator.WallPattern pattern = GetPatternForMission(missionType);
            int density = map.GridWidth * map.GridHeight / 10;

            // Générer les murs
            HashSet<WallSegment> walls = wallGenerator.GenerateWalls(
                map.GridWidth,
                map.GridHeight,
                pattern,
                density
            );

            // Nettoyer les zones de spawn
            wallGenerator.ClearSpawnZones(walls, map.GridWidth, map.GridHeight);

            map.SetWalls(walls);
            map.Buildings = wallGenerator.LastGeneratedBuildings
                .ConvertAll(b => new BuildingFootprintData
                {
                    X = b.X,
                    Y = b.Y,
                    Width = b.Width,
                    Height = b.Height,
                    FloorCount = Math.Clamp(b.FloorCount, 1, map.FloorCount),
                    BasementCount = Math.Max(0, b.BasementCount)
                });

            // Générer les zones de spawn
            map.GenerateDefaultSpawnZones();
            map.StairConnections = GenerateDefaultStairs(map.GridWidth, map.GridHeight, map.FloorCount, map.Buildings);

            Console.WriteLine($"[MAP GEN] Generated {map.Name}: {map.GridWidth}x{map.GridHeight}, floors={map.FloorCount}, {map.Walls.Count} walls");

            return map;
        }

        /// <summary>
        /// Génère une carte vide de taille spécifiée
        /// </summary>
        public MapData GenerateEmptyMap(int width, int height, string name = "New Map")
        {
            MapData map = new MapData
            {
                Name = name,
                Description = "Empty map for editing",
                Author = "Map Editor",
                CreatedDate = DateTime.Now,
                GridWidth = width,
                GridHeight = height,
                CellSize = 2,
                FloorCount = 3,
                TimeOfDay = 0.5f
            };

            map.GenerateDefaultSpawnZones();
            map.FloorCount = 3;
            map.StairConnections = GenerateDefaultStairs(width, height, map.FloorCount, map.Buildings);

            Console.WriteLine($"[MAP GEN] Created empty map: {width}x{height}, floors={map.FloorCount}");

            return map;
        }

        /// <summary>
        /// Génère une carte avec pattern spécifique
        /// </summary>
        public MapData GenerateMap(
            int width,
            int height,
            EdgeWallGenerator.WallPattern pattern,
            string name = "Generated Map")
        {
            MapData map = new MapData
            {
                Name = name,
                Description = $"Map with {pattern} pattern",
                Author = "Map Generator",
                CreatedDate = DateTime.Now,
                GridWidth = width,
                GridHeight = height,
                CellSize = 2,
                FloorCount = 3,
                TimeOfDay = 0.5f
            };

            int density = width * height / 10;
            HashSet<WallSegment> walls = wallGenerator.GenerateWalls(width, height, pattern, density);
            wallGenerator.ClearSpawnZones(walls, width, height);

            map.SetWalls(walls);
            map.Buildings = wallGenerator.LastGeneratedBuildings
                .ConvertAll(b => new BuildingFootprintData
                {
                    X = b.X,
                    Y = b.Y,
                    Width = b.Width,
                    Height = b.Height,
                    FloorCount = Math.Clamp(b.FloorCount, 1, map.FloorCount),
                    BasementCount = Math.Max(0, b.BasementCount)
                });
            map.GenerateDefaultSpawnZones();
            map.StairConnections = GenerateDefaultStairs(width, height, map.FloorCount, map.Buildings);

            Console.WriteLine($"[MAP GEN] Generated {pattern} map: {width}x{height}, floors={map.FloorCount}");

            return map;
        }


        private List<StairConnectionData> GenerateDefaultStairs(
            int width,
            int height,
            int floorCount,
            List<BuildingFootprintData> buildings)
        {
            var stairs = new List<StairConnectionData>();
            for (int floor = 0; floor < floorCount - 1; floor++)
            {
                stairs.Add(new StairConnectionData
                {
                    FromX = Math.Max(1, width / 4),
                    FromY = Math.Max(1, height / 4),
                    FromFloor = floor,
                    ToX = Math.Max(2, width / 4 + 1),
                    ToY = Math.Max(2, height / 4 + 1),
                    ToFloor = floor + 1,
                    Bidirectional = true
                });

                stairs.Add(new StairConnectionData
                {
                    FromX = Math.Max(1, (width * 3) / 4),
                    FromY = Math.Max(1, (height * 3) / 4),
                    FromFloor = floor,
                    ToX = Math.Max(2, (width * 3) / 4 - 1),
                    ToY = Math.Max(2, (height * 3) / 4 - 1),
                    ToFloor = floor + 1,
                    Bidirectional = true
                });
            }

            // Ajouter des cages d'escaliers internes sur chaque bâtiment multi-étage.
            // Cela garantit une circulation verticale cohérente même quand les étages
            // supérieurs ont un retrait (setback) et deviennent plus petits.
            if (buildings != null && floorCount > 1)
            {
                foreach (var building in buildings)
                {
                    int maxBuildingFloor = Math.Clamp(building.FloorCount, 1, floorCount);
                    if (maxBuildingFloor <= 1 || building.Width < 3 || building.Height < 3)
                        continue;

                    int sx = Math.Clamp(building.X + (building.Width / 2), building.X + 1, building.X + building.Width - 2);
                    int sy = Math.Clamp(building.Y + (building.Height / 2), building.Y + 1, building.Y + building.Height - 2);
                    sx = Math.Clamp(sx, 1, width - 2);
                    sy = Math.Clamp(sy, 1, height - 2);

                    for (int floor = 0; floor < maxBuildingFloor - 1; floor++)
                    {
                        stairs.Add(new StairConnectionData
                        {
                            FromX = sx,
                            FromY = sy,
                            FromFloor = floor,
                            ToX = sx,
                            ToY = sy,
                            ToFloor = floor + 1,
                            Bidirectional = true
                        });
                    }
                }
            }

            return stairs;
        }

        /// <summary>
        /// Détermine le pattern selon le type de mission
        /// </summary>
        private EdgeWallGenerator.WallPattern GetPatternForMission(string missionType)
        {
            return missionType switch
            {
                "Tutorial" => EdgeWallGenerator.WallPattern.Scattered,
                "Survival" => EdgeWallGenerator.WallPattern.Bunker,
                "Assault" => EdgeWallGenerator.WallPattern.Urban,
                "Defense" => EdgeWallGenerator.WallPattern.Trenches,
                "Centre-Ville" => EdgeWallGenerator.WallPattern.Urban,
                _ => (EdgeWallGenerator.WallPattern)random.Next(0, 6)
            };
        }

        private int GetFloorCountForMission(string missionType)
        {
            return missionType switch
            {
                "Centre-Ville" => random.Next(5, 11),
                _ => random.Next(2, 4)
            };
        }

        /// <summary>
        /// Crée une collection de cartes prédéfinies
        /// </summary>
        public static void GeneratePremadeMaps()
        {
            Console.WriteLine("[MAP GEN] Generating premade maps...");

            MapGenerator generator = new MapGenerator(new Random());

            // Tutorial Map - Simple
            var tutorial = generator.GenerateMap(30, 30, EdgeWallGenerator.WallPattern.Scattered, "Tutorial - Open Field");
            tutorial.Description = "A simple map with scattered cover for learning the basics";
            tutorial.MaxEnemyUnits = 4;
            MapCatalog.SaveMap(tutorial);

            // Urban Combat
            var urban = generator.GenerateMap(50, 50, EdgeWallGenerator.WallPattern.Urban, "Urban Warfare");
            urban.Description = "Dense city environment with buildings and streets";
            urban.BiomeType = "Urban";
            MapCatalog.SaveMap(urban);

            // Bunker Assault
            var bunker = generator.GenerateMap(40, 40, EdgeWallGenerator.WallPattern.Bunker, "Bunker Assault");
            bunker.Description = "Fortified defensive position with barricades";
            bunker.BiomeType = "Military";
            MapCatalog.SaveMap(bunker);

            // Maze Challenge
            var maze = generator.GenerateMap(45, 45, EdgeWallGenerator.WallPattern.Maze, "The Labyrinth");
            maze.Description = "Complex maze requiring careful navigation";
            MapCatalog.SaveMap(maze);

            // Trenches
            var trenches = generator.GenerateMap(60, 40, EdgeWallGenerator.WallPattern.Trenches, "No Man's Land");
            trenches.Description = "WWI-style trench warfare";
            trenches.BiomeType = "Wasteland";
            MapCatalog.SaveMap(trenches);

            // Large Open Map
            var large = generator.GenerateEmptyMap(80, 80, "Arena - Large");
            large.Description = "Large open arena for epic battles";
            MapCatalog.SaveMap(large);

            Console.WriteLine("[MAP GEN] Created 6 premade maps");
        }
    }
}
