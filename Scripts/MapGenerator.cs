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

            // Générer les zones de spawn avant les bâtiments/murs.
            map.GenerateDefaultSpawnZones();

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
            map.Furnitures = GenerateMapFurniture(pattern, map.Buildings, map.GridWidth, map.GridHeight, map.FloorCount, map.SpawnZones, map.HescoBarriers);

            map.StairConnections = GenerateDefaultStairs(map.GridWidth, map.GridHeight, map.FloorCount, map.Buildings);
            map.RampTiles = GenerateDefaultRamps(map.StairConnections);
            map.TerrainHeights = GenerateTerrainRelief(map.GridWidth, map.GridHeight, pattern);

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
            map.TerrainHeights = GenerateTerrainRelief(width, height, null);

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
                FloorCount = pattern == EdgeWallGenerator.WallPattern.Urban ? 8 : 3,
                TimeOfDay = 0.5f
            };

            // Générer les zones de spawn avant les bâtiments/murs.
            map.GenerateDefaultSpawnZones();

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
            map.Furnitures = GenerateMapFurniture(pattern, map.Buildings, width, height, map.FloorCount, map.SpawnZones, map.HescoBarriers);
            map.StairConnections = GenerateDefaultStairs(width, height, map.FloorCount, map.Buildings);
            map.RampTiles = GenerateDefaultRamps(map.StairConnections);
            map.TerrainHeights = GenerateTerrainRelief(width, height, pattern);

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

                    var centralSpiral = BuildSpiralPoints(centerX, centerY, minX, maxX, minY, maxY, radiusX, radiusY);
                    AddStairShaftConnections(stairs, centralSpiral, maxBuildingFloor);

                    // Bâtiments suffisamment grands: ajouter une deuxième cage proche d'une façade.
                    if (building.Width >= 8 || building.Height >= 8)
                    {
                        int facadeAnchorX = Math.Clamp(building.X + 2, minX, maxX);
                        int facadeAnchorY = Math.Clamp(building.Y + Math.Max(2, building.Height - 3), minY, maxY);

                        int secondaryRadiusX = Math.Min(1, Math.Min(facadeAnchorX - minX, maxX - facadeAnchorX));
                        int secondaryRadiusY = Math.Min(1, Math.Min(facadeAnchorY - minY, maxY - facadeAnchorY));

                        var secondarySpiral = BuildSpiralPoints(
                            facadeAnchorX,
                            facadeAnchorY,
                            minX,
                            maxX,
                            minY,
                            maxY,
                            secondaryRadiusX,
                            secondaryRadiusY);

                        AddStairShaftConnections(stairs, secondarySpiral, maxBuildingFloor);
                    }
                }
            }

            return stairs;
        }

        private List<FurnitureData> GenerateMapFurniture(
            EdgeWallGenerator.WallPattern pattern,
            List<BuildingFootprintData> buildings,
            int mapWidth,
            int mapHeight,
            int maxFloors,
            List<SpawnZone> spawnZones,
            List<HescoBarrierData> hescoBarriers)
        {
            List<FurnitureData> furnitures = GenerateBuildingFurniture(buildings, mapWidth, mapHeight, maxFloors);

            if (pattern == EdgeWallGenerator.WallPattern.Urban)
            {
                furnitures.AddRange(GenerateUrbanStreetVehicles(mapWidth, mapHeight, buildings, spawnZones, hescoBarriers, furnitures));
            }

            return furnitures;
        }

        private List<FurnitureData> GenerateBuildingFurniture(
            List<BuildingFootprintData> buildings,
            int mapWidth,
            int mapHeight,
            int maxFloors)
        {
            var furnitures = new List<FurnitureData>();
            if (buildings == null || buildings.Count == 0)
                return furnitures;

            foreach (var building in buildings)
            {
                int minX = Math.Clamp(building.X + 1, 1, Math.Max(1, mapWidth - 2));
                int maxX = Math.Clamp(building.X + building.Width - 2, 1, Math.Max(1, mapWidth - 2));
                int minY = Math.Clamp(building.Y + 1, 1, Math.Max(1, mapHeight - 2));
                int maxY = Math.Clamp(building.Y + building.Height - 2, 1, Math.Max(1, mapHeight - 2));

                if (minX > maxX || minY > maxY)
                    continue;

                int floors = Math.Clamp(building.FloorCount, 1, Math.Max(1, maxFloors));
                for (int floor = 0; floor < floors; floor++)
                {
                    var occupied = new HashSet<Point>();

                    PlaceFurniture(FurnitureType.Counter, 1);
                    PlaceFurniture(FurnitureType.Fridge, 1);
                    List<Point> tables = PlaceFurniture(FurnitureType.Table, 1);
                    Point? tablePos = tables.Count > 0 ? tables[0] : null;
                    PlaceChairsAroundTable(tablePos);
                    PlaceFurniture(FurnitureType.Stove, 1);
                    PlaceFurniture(FurnitureType.Bed, 1);

                    if (building.Width >= 9 && building.Height >= 9)
                    {
                        PlaceFurniture(FurnitureType.Counter, 1);
                        PlaceFurniture(FurnitureType.Table, 1);
                        PlaceFurniture(FurnitureType.Bed, 1);
                    }

                    List<Point> PlaceFurniture(FurnitureType type, int count)
                    {
                        var placed = new List<Point>();
                        for (int i = 0; i < count; i++)
                        {
                            var point = FindAvailableInteriorCell(minX, maxX, minY, maxY, occupied);
                            if (point == null)
                                break;

                            occupied.Add(point.Value);
                            furnitures.Add(new FurnitureData
                            {
                                X = point.Value.X,
                                Y = point.Value.Y,
                                Floor = floor,
                                Type = type
                            });
                            placed.Add(point.Value);
                        }

                        return placed;
                    }

                    void PlaceChairsAroundTable(Point? table)
                    {
                        if (table == null)
                            return;

                        Point[] candidates =
                        {
                            new Point(table.Value.X - 1, table.Value.Y),
                            new Point(table.Value.X + 1, table.Value.Y),
                            new Point(table.Value.X, table.Value.Y - 1),
                            new Point(table.Value.X, table.Value.Y + 1)
                        };

                        foreach (Point candidate in candidates)
                        {
                            if (candidate.X < minX || candidate.X > maxX || candidate.Y < minY || candidate.Y > maxY)
                                continue;
                            if (occupied.Contains(candidate))
                                continue;

                            occupied.Add(candidate);
                            furnitures.Add(new FurnitureData
                            {
                                X = candidate.X,
                                Y = candidate.Y,
                                Floor = floor,
                                Type = FurnitureType.Chair
                            });
                        }
                    }
                }
            }

            return furnitures;
        }

        private List<FurnitureData> GenerateUrbanStreetVehicles(
            int mapWidth,
            int mapHeight,
            List<BuildingFootprintData> buildings,
            List<SpawnZone> spawnZones,
            List<HescoBarrierData> hescoBarriers,
            List<FurnitureData> existingFurniture)
        {
            var vehicles = new List<FurnitureData>();

            const int blockSize = 14;
            const int streetWidth = 2;
            const int startX = 2;
            int endX = Math.Max(startX + 1, mapWidth - 2);
            const int startY = 4;
            int endY = Math.Max(startY + 1, mapHeight - 4);
            int stride = blockSize + streetWidth;

            var blockedCells = new HashSet<Point>();

            if (buildings != null)
            {
                foreach (BuildingFootprintData building in buildings)
                {
                    for (int x = building.X; x < building.X + building.Width; x++)
                    {
                        for (int y = building.Y; y < building.Y + building.Height; y++)
                        {
                            blockedCells.Add(new Point(x, y));
                        }
                    }
                }
            }

            if (hescoBarriers != null)
            {
                foreach (HescoBarrierData barrier in hescoBarriers)
                {
                    blockedCells.Add(new Point(barrier.X, barrier.Y));
                }
            }

            if (existingFurniture != null)
            {
                foreach (FurnitureData furniture in existingFurniture)
                {
                    if (furniture.Floor != 0)
                        continue;

                    blockedCells.Add(new Point(furniture.X, furniture.Y));
                }
            }

            if (spawnZones != null)
            {
                foreach (SpawnZone zone in spawnZones)
                {
                    for (int x = zone.MinX; x <= zone.MaxX; x++)
                    {
                        for (int y = zone.MinY; y <= zone.MaxY; y++)
                        {
                            blockedCells.Add(new Point(x, y));
                        }
                    }
                }
            }

            var roadCandidates = new List<Point>();
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    bool isRoad = (x - startX) % stride >= blockSize || (y - startY) % stride >= blockSize;
                    if (!isRoad)
                        continue;

                    Point cell = new Point(x, y);
                    if (blockedCells.Contains(cell))
                        continue;

                    roadCandidates.Add(cell);
                }
            }

            if (roadCandidates.Count == 0)
                return vehicles;

            int targetVehicleCount = Math.Clamp((mapWidth * mapHeight) / 185, 6, 22);
            targetVehicleCount = Math.Min(targetVehicleCount, roadCandidates.Count);
            Shuffle(roadCandidates);

            FurnitureType[] vehicleTypes =
            {
                FurnitureType.SedanToyotaCorolla,
                FurnitureType.SedanBmwSeries3,
                FurnitureType.SedanMercedesEClass,
                FurnitureType.PickupToyotaTacoma,
                FurnitureType.PickupFordF150,
                FurnitureType.PickupRam3500
            };

            for (int i = 0; i < targetVehicleCount; i++)
            {
                Point candidate = roadCandidates[i];
                FurnitureType vehicleType = vehicleTypes[random.Next(vehicleTypes.Length)];

                vehicles.Add(new FurnitureData
                {
                    X = candidate.X,
                    Y = candidate.Y,
                    Floor = 0,
                    Type = vehicleType
                });
            }

            return vehicles;
        }

        private void Shuffle<T>(IList<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }

        private Point? FindAvailableInteriorCell(int minX, int maxX, int minY, int maxY, HashSet<Point> occupied)
        {
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int cells = width * height;
            if (cells <= 0)
                return null;

            int start = random.Next(cells);
            for (int i = 0; i < cells; i++)
            {
                int index = (start + i) % cells;
                int x = minX + (index % width);
                int y = minY + (index / width);
                var point = new Point(x, y);

                if (!occupied.Contains(point))
                    return point;
            }

            return null;
        }

        private static List<RampTileData> GenerateDefaultRamps(IEnumerable<StairConnectionData> stairs)
        {
            var ramps = new List<RampTileData>();
            if (stairs == null)
                return ramps;

            foreach (var stair in stairs)
            {
                bool climbsToUpperFloor = stair.ToFloor == stair.FromFloor + 1;
                int dx = stair.ToX - stair.FromX;
                int dy = stair.ToY - stair.FromY;
                bool cardinalStep = Math.Abs(dx) + Math.Abs(dy) == 1;

                if (!climbsToUpperFloor || !cardinalStep)
                    continue;

                ramps.Add(new RampTileData
                {
                    X = stair.FromX,
                    Y = stair.FromY,
                    Floor = stair.FromFloor,
                    AscendDx = dx,
                    AscendDy = dy,
                    Bidirectional = stair.Bidirectional
                });
            }

            return ramps;
        }

        private static List<Point> BuildSpiralPoints(
            int centerX,
            int centerY,
            int minX,
            int maxX,
            int minY,
            int maxY,
            int radiusX,
            int radiusY)
        {
            return new List<Point>
            {
                new Point(centerX, Math.Clamp(centerY - radiusY, minY, maxY)),
                new Point(Math.Clamp(centerX + radiusX, minX, maxX), centerY),
                new Point(centerX, Math.Clamp(centerY + radiusY, minY, maxY)),
                new Point(Math.Clamp(centerX - radiusX, minX, maxX), centerY)
            }
            .Distinct()
            .ToList();
        }

        private static void AddStairShaftConnections(List<StairConnectionData> stairs, List<Point> shaftPoints, int maxBuildingFloor)
        {
            if (shaftPoints == null || shaftPoints.Count == 0 || maxBuildingFloor <= 1)
                return;

            for (int floor = 0; floor < maxBuildingFloor - 1; floor++)
            {
                Point from = shaftPoints[floor % shaftPoints.Count];
                Point to = shaftPoints[(floor + 1) % shaftPoints.Count];

                bool duplicate = stairs.Any(st =>
                    st.FromFloor == floor &&
                    st.ToFloor == floor + 1 &&
                    st.FromX == from.X &&
                    st.FromY == from.Y &&
                    st.ToX == to.X &&
                    st.ToY == to.Y);

                if (duplicate)
                    continue;

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

        private List<TerrainHeightData> GenerateTerrainRelief(int width, int height, EdgeWallGenerator.WallPattern? pattern)
        {
            if (pattern == EdgeWallGenerator.WallPattern.Trenches)
                return GenerateTrenchTerrain(width, height);

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

        private List<TerrainHeightData> GenerateTrenchTerrain(int width, int height)
        {
            // Tranchée principale profonde: -2 cases de hauteur.
            // Tranchées secondaires: -1 case de hauteur.
            var heights = new Dictionary<Point, float>();

            void SetDepth(int x, int y, float depth)
            {
                if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1)
                    return;

                var point = new Point(x, y);
                if (heights.TryGetValue(point, out float existingDepth))
                    heights[point] = Math.Min(existingDepth, depth);
                else
                    heights[point] = depth;
            }

            int currentY = height / 2;
            var mainTrench = new List<Point>();

            for (int x = 3; x < width - 3; x++)
            {
                mainTrench.Add(new Point(x, currentY));

                if (random.Next(100) < 20)
                    currentY += random.Next(-1, 2);

                currentY = Math.Max(4, Math.Min(height - 5, currentY));
            }

            foreach (Point p in mainTrench)
            {
                SetDepth(p.X, p.Y, -2f);

                // Lèvres de tranchée plus hautes pour créer une transition visuelle.
                SetDepth(p.X, p.Y - 1, -1f);
                SetDepth(p.X, p.Y + 1, -1f);
            }

            int numCross = random.Next(2, 5);
            for (int i = 0; i < numCross; i++)
            {
                int crossX = random.Next(5, width - 5);
                int length = random.Next(4, 8);
                int startY = random.Next(4, Math.Max(5, height - length - 4));

                for (int y = startY; y < startY + length && y < height - 3; y++)
                    SetDepth(crossX, y, -1f);
            }

            return heights
                .Select(h => new TerrainHeightData
                {
                    X = h.Key.X,
                    Y = h.Key.Y,
                    HeightOffset = h.Value
                })
                .ToList();
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
                "Assault" => 8,
                "Centre-Ville" => 8,
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
