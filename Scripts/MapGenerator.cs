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

            // Générer les zones de spawn
            map.GenerateDefaultSpawnZones();

            Console.WriteLine($"[MAP GEN] Generated {map.Name}: {map.GridWidth}x{map.GridHeight}, {map.Walls.Count} walls");

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
                TimeOfDay = 0.5f
            };

            map.GenerateDefaultSpawnZones();

            Console.WriteLine($"[MAP GEN] Created empty map: {width}x{height}");

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
                TimeOfDay = 0.5f
            };

            int density = width * height / 10;
            HashSet<WallSegment> walls = wallGenerator.GenerateWalls(width, height, pattern, density);
            wallGenerator.ClearSpawnZones(walls, width, height);

            map.SetWalls(walls);
            map.GenerateDefaultSpawnZones();

            Console.WriteLine($"[MAP GEN] Generated {pattern} map: {width}x{height}");

            return map;
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
                _ => (EdgeWallGenerator.WallPattern)random.Next(0, 6)
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
