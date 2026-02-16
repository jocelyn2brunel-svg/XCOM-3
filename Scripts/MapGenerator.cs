using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Générateur procédural de cartes - produit des MapData
    /// </summary>
    public class MapGenerator
    {
        private Random random;
        private EdgeWallGenerator wallGenerator;
        private readonly bool terrainReliefEnabled = false;

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
            map.HescoBarriers = wallGenerator.LastGeneratedHescoBarriers
                .ConvertAll(p => new HescoBarrierData
                {
                    X = p.X,
                    Y = p.Y,
                    Floor = 0
                });

            // Générer les zones de spawn
            map.GenerateDefaultSpawnZones();
            map.StairConnections = GenerateDefaultStairs(map.GridWidth, map.GridHeight, map.FloorCount, map.Buildings);
            map.RampTiles = GenerateDefaultRamps(map.StairConnections);
            map.TerrainHeights = GenerateTerrainRelief(map.GridWidth, map.GridHeight);

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
            map.RampTiles = GenerateDefaultRamps(map.StairConnections);
            map.TerrainHeights = GenerateTerrainRelief(width, height);

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
            map.HescoBarriers = wallGenerator.LastGeneratedHescoBarriers
                .ConvertAll(p => new HescoBarrierData
                {
                    X = p.X,
                    Y = p.Y,
                    Floor = 0
                });
            map.GenerateDefaultSpawnZones();
            map.StairConnections = GenerateDefaultStairs(width, height, map.FloorCount, map.Buildings);
            map.RampTiles = GenerateDefaultRamps(map.StairConnections);
            map.TerrainHeights = GenerateTerrainRelief(width, height);

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
                    FromY = Math.Max(2, height / 4),
                    FromFloor = floor,
                    ToX = Math.Max(1, width / 4),
                    ToY = Math.Max(1, height / 4 - 1),
                    ToFloor = floor + 1,
                    Bidirectional = true
                });

                stairs.Add(new StairConnectionData
                {
                    FromX = Math.Max(1, (width * 3) / 4),
                    FromY = Math.Max(2, (height * 3) / 4),
                    FromFloor = floor,
                    ToX = Math.Max(1, (width * 3) / 4),
                    ToY = Math.Max(1, (height * 3) / 4 - 1),
                    ToFloor = floor + 1,
                    Bidirectional = true
                });
            }

            // Ajouter des cages d'escaliers internes sur chaque bâtiment multi-étage.
            // Cela garantit une circulation verticale cohérente dans les bâtiments urbains.
            if (buildings != null && floorCount > 1)
            {
                foreach (var building in buildings)
                {
                    int maxBuildingFloor = Math.Clamp(building.FloorCount, 1, floorCount);
                    if (maxBuildingFloor <= 1 || building.Width < 3 || building.Height < 3)
                        continue;

                    int minX = Math.Clamp(building.X + 1, 1, width - 2);
                    int maxX = Math.Clamp(building.X + building.Width - 2, 1, width - 2);
                    int minY = Math.Clamp(building.Y + 1, 1, height - 2);
                    int maxY = Math.Clamp(building.Y + building.Height - 2, 1, height - 2);

                    int centerX = Math.Clamp(building.X + (building.Width / 2), minX, maxX);
                    int centerY = Math.Clamp(building.Y + (building.Height / 2), minY, maxY);

                    int radiusX = Math.Min(1, Math.Min(centerX - minX, maxX - centerX));
                    int radiusY = Math.Min(1, Math.Min(centerY - minY, maxY - centerY));

                    var spiralPoints = new List<Point>
                    {
                        new Point(centerX, Math.Clamp(centerY - radiusY, minY, maxY)),
                        new Point(Math.Clamp(centerX + radiusX, minX, maxX), centerY),
                        new Point(centerX, Math.Clamp(centerY + radiusY, minY, maxY)),
                        new Point(Math.Clamp(centerX - radiusX, minX, maxX), centerY)
                    };

                    spiralPoints = spiralPoints.Distinct().ToList();
                    if (spiralPoints.Count == 0)
                        continue;

                    for (int floor = 0; floor < maxBuildingFloor - 1; floor++)
                    {
                        Point from = spiralPoints[floor % spiralPoints.Count];
                        Point to = spiralPoints[(floor + 1) % spiralPoints.Count];

                        stairs.Add(new StairConnectionData
                        {
                            FromX = from.X,
                            FromY = from.Y,
                            FromFloor = floor,
                            ToX = to.X,
                            ToY = to.Y,
                            ToFloor = floor + 1,
                            Bidirectional = true
                        });
                    }
                }
            }

            return stairs;
        }

        private static List<RampTileData> GenerateDefaultRamps(IEnumerable<StairConnectionData> stairs)
        {
            var ramps = new List<RampTileData>();
            if (stairs == null)
                return ramps;

            foreach (var stair in stairs)
            {
                bool climbsToUpperFloor = stair.ToFloor == stair.FromFloor + 1;
                bool climbsNorth = stair.ToY == stair.FromY - 1 && stair.ToX == stair.FromX;

                if (!climbsToUpperFloor || !climbsNorth)
                    continue;

                ramps.Add(new RampTileData
                {
                    X = stair.FromX,
                    Y = stair.FromY,
                    Floor = stair.FromFloor,
                    Bidirectional = stair.Bidirectional
                });
            }

            return ramps;
        }

        private List<TerrainHeightData> GenerateTerrainRelief(int width, int height)
        {
            // Temporairement désactivé: pas de collines ni de fossés.
            if (!terrainReliefEnabled)
                return new List<TerrainHeightData>();

            var heightMap = new float[width, height];
            int featureCount = Math.Max(3, (width * height) / 420);

            for (int i = 0; i < featureCount; i++)
            {
                bool isHill = random.NextDouble() > 0.45;
                int centerX = random.Next(2, Math.Max(3, width - 2));
                int centerY = random.Next(2, Math.Max(3, height - 2));
                float radius = random.Next(2, 7);
                float amplitude = (float)(random.NextDouble() * 0.75 + 0.25) * (isHill ? 1f : -1f);

                for (int x = 1; x < width - 1; x++)
                {
                    for (int y = 1; y < height - 1; y++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float distance = MathF.Sqrt(dx * dx + dy * dy);
                        if (distance > radius)
                            continue;

                        float influence = 1f - (distance / radius);
                        heightMap[x, y] += amplitude * influence;
                    }
                }
            }

            var terrain = new List<TerrainHeightData>();
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    float smoothed = (
                        heightMap[x, y] * 0.45f +
                        heightMap[x - 1, y] * 0.12f +
                        heightMap[x + 1, y] * 0.12f +
                        heightMap[x, y - 1] * 0.12f +
                        heightMap[x, y + 1] * 0.12f +
                        heightMap[x - 1, y - 1] * 0.035f +
                        heightMap[x + 1, y - 1] * 0.035f +
                        heightMap[x - 1, y + 1] * 0.035f +
                        heightMap[x + 1, y + 1] * 0.035f);

                    float clamped = Math.Clamp(smoothed, -0.75f, 0.9f);
                    if (MathF.Abs(clamped) < 0.08f)
                        continue;

                    terrain.Add(new TerrainHeightData
                    {
                        X = x,
                        Y = y,
                        HeightOffset = clamped
                    });
                }
            }

            return terrain;
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
                "Extraction" => EdgeWallGenerator.WallPattern.Scattered,
                "Sabotage" => EdgeWallGenerator.WallPattern.Maze,
                "Blackout" => EdgeWallGenerator.WallPattern.Bunker,
                _ => (EdgeWallGenerator.WallPattern)random.Next(0, 6)
            };
        }

        private int GetFloorCountForMission(string missionType)
        {
            return missionType switch
            {
                "Centre-Ville" => random.Next(3, 6),
                "Blackout" => random.Next(2, 4),
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
