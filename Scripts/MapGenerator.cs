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

            // Pour les missions avec spawn au centre, ajouter une zone centrale
            // afin que les véhicules n'y soient pas générés.
            if (missionType == "Centre-Ville" || missionType == "Sabotage")
            {
                int cx = map.GridWidth / 2;
                int cy = map.GridHeight / 2;
                map.PlayerSpawnZones.Add(new SpawnZone
                {
                    MinX = Math.Max(0, cx - 3),
                    MinY = Math.Max(0, cy - 3),
                    MaxX = Math.Min(map.GridWidth - 1, cx + 3),
                    MaxY = Math.Min(map.GridHeight - 1, cy + 3),
                    MaxUnits = 0
                });
            }

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
            map.Furnitures = GenerateMapFurniture(
                pattern,
                map.Buildings,
                map.GridWidth,
                map.GridHeight,
                map.FloorCount,
                map.PlayerSpawnZones.Concat(map.EnemySpawnZones).ToList(),
                map.HescoBarriers);

            map.RampTiles = GenerateDefaultRamps(map.GridWidth, map.GridHeight, map.FloorCount, map.Buildings);
            map.TerrainHeights = GenerateTerrainRelief(map.GridWidth, map.GridHeight, pattern);

            if (missionType == "The Hive")
            {
                if (map.PlayerSpawnZones.Count > 0)
                {
                    var zone = map.PlayerSpawnZones[0];
                    map.Furnitures.Add(new FurnitureData
                    {
                        X = (zone.MinX + zone.MaxX) / 2 + 2,
                        Y = (zone.MinY + zone.MaxY) / 2,
                        Floor = 0,
                        Type = FurnitureType.SedanToyotaCorolla
                    });
                }

                var mainBuilding = map.Buildings.FirstOrDefault();
                if (mainBuilding != null)
                {
                    int objX = mainBuilding.X + mainBuilding.Width / 2;
                    int objY = mainBuilding.Y + 4;
                    int objFloor = -8;

                    map.Objectives.Add(new ObjectivePoint
                    {
                        X = objX,
                        Y = objY,
                        Floor = objFloor,
                        Type = "Sabotage",
                        Description = "Saboter l'ordinateur central (Reine Rouge)"
                    });

                    map.Furnitures.Add(new FurnitureData
                    {
                        X = objX,
                        Y = objY,
                        Floor = objFloor,
                        Type = FurnitureType.Computer
                    });
                }
            }

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
            map.RampTiles = GenerateDefaultRamps(width, height, map.FloorCount, map.Buildings);
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
            map.Furnitures = GenerateMapFurniture(
                pattern,
                map.Buildings,
                width,
                height,
                map.FloorCount,
                map.PlayerSpawnZones.Concat(map.EnemySpawnZones).ToList(),
                map.HescoBarriers);
            map.RampTiles = GenerateDefaultRamps(width, height, map.FloorCount, map.Buildings);
            map.TerrainHeights = GenerateTerrainRelief(width, height, pattern);

            Console.WriteLine($"[MAP GEN] Generated {pattern} map: {width}x{height}, floors={map.FloorCount}");

            return map;
        }


        private List<RampTileData> GenerateDefaultRamps(
            int width,
            int height,
            int floorCount,
            List<BuildingFootprintData> buildings)
        {
            var ramps = new List<RampTileData>();
            var occupiedRampCells = new HashSet<(int Floor, int X, int Y)>();

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
                    AddRampShaftConnections(ramps, centralSpiral, 0, maxBuildingFloor - 1, occupiedRampCells);

                    int basementLevels = Math.Max(0, building.BasementCount);
                    if (basementLevels > 0)
                    {
                        AddRampShaftConnections(ramps, centralSpiral, -basementLevels, 0, occupiedRampCells);
                    }

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

                        AddRampShaftConnections(ramps, secondarySpiral, 0, maxBuildingFloor - 1, occupiedRampCells);

                        if (basementLevels > 0)
                        {
                            AddRampShaftConnections(ramps, secondarySpiral, -basementLevels, 0, occupiedRampCells);
                        }
                    }
                }
            }

            return ramps;
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
            else if (pattern == EdgeWallGenerator.WallPattern.Forest)
            {
                furnitures.AddRange(GenerateForestFurniture(mapWidth, mapHeight, buildings, spawnZones, hescoBarriers, furnitures));
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

                    // Placer une caisse de loot dans certains bâtiments (seulement au rez-de-chaussée)
                    if (floor == 0 && random.NextDouble() < 0.6)
                        PlaceFurniture(FurnitureType.LootCrate, 1);

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
                            int dxToTable = table.Value.X - candidate.X;
                            int dyToTable = table.Value.Y - candidate.Y;
                            furnitures.Add(new FurnitureData
                            {
                                X = candidate.X,
                                Y = candidate.Y,
                                Floor = floor,
                                Type = FurnitureType.Chair,
                                OrientationRadians = (float)Math.Atan2(dxToTable, dyToTable)
                            });
                        }
                    }
                }
            }

            return furnitures;
        }

        private List<FurnitureData> GenerateForestFurniture(
            int mapWidth,
            int mapHeight,
            List<BuildingFootprintData> buildings,
            List<SpawnZone> spawnZones,
            List<HescoBarrierData> hescoBarriers,
            List<FurnitureData> existingFurniture)
        {
            var nature = new List<FurnitureData>();
            var blockedCells = new HashSet<Point>();

            // Marquer les bâtiments comme bloqués
            if (buildings != null)
            {
                foreach (var b in buildings)
                {
                    for (int x = b.X; x < b.X + b.Width; x++)
                        for (int y = b.Y; y < b.Y + b.Height; y++)
                            blockedCells.Add(new Point(x, y));
                }
            }

            // Marquer les zones de spawn comme bloquées (un peu plus larges pour laisser de la place)
            if (spawnZones != null)
            {
                foreach (var zone in spawnZones)
                {
                    for (int x = zone.MinX - 1; x <= zone.MaxX + 1; x++)
                        for (int y = zone.MinY - 1; y <= zone.MaxY + 1; y++)
                            blockedCells.Add(new Point(x, y));
                }
            }

            // Placer des pins éparpillés
            int targetTreeCount = (mapWidth * mapHeight) / 25; // Densité "scattered"
            int attempts = 0;
            while (nature.Count < targetTreeCount && attempts < 500)
            {
                attempts++;
                int x = random.Next(2, mapWidth - 2);
                int y = random.Next(2, mapHeight - 2);
                Point p = new Point(x, y);

                if (blockedCells.Contains(p)) continue;

                var tree = new FurnitureData
                {
                    X = x,
                    Y = y,
                    Floor = 0,
                    Type = FurnitureType.TreePine,
                    OrientationRadians = (float)(random.NextDouble() * Math.PI * 2)
                };

                bool overlapping = false;
                foreach (Point cell in FurnitureData.GetOccupiedCells(tree))
                {
                    if (cell.X < 0 || cell.X >= mapWidth || cell.Y < 0 || cell.Y >= mapHeight || blockedCells.Contains(cell))
                    {
                        overlapping = true;
                        break;
                    }
                }

                if (!overlapping)
                {
                    nature.Add(tree);
                    foreach (Point cell in FurnitureData.GetOccupiedCells(tree))
                        blockedCells.Add(cell);
                }
            }

            return nature;
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

                    foreach (Point occupiedCell in FurnitureData.GetOccupiedCells(furniture))
                    {
                        blockedCells.Add(occupiedCell);
                    }
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
                FurnitureType.PickupRam3500,
                FurnitureType.VanGeneric
            };

            foreach (Point candidate in roadCandidates)
            {
                if (vehicles.Count >= targetVehicleCount)
                    break;

                FurnitureType vehicleType = vehicleTypes[random.Next(vehicleTypes.Length)];
                var vehicle = new FurnitureData
                {
                    X = candidate.X,
                    Y = candidate.Y,
                    Floor = 0,
                    Type = vehicleType
                };

                var occupiedCells = FurnitureData.GetOccupiedCells(vehicle).ToList();
                if (occupiedCells.Any(c => c.X < 0 || c.Y < 0 || c.X >= mapWidth || c.Y >= mapHeight || blockedCells.Contains(c)))
                    continue;

                vehicles.Add(vehicle);
                foreach (Point occupiedCell in occupiedCells)
                {
                    blockedCells.Add(occupiedCell);
                }
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
            // Retourne la cellule de base et son voisin cardinal (nord, puis est, sud, ouest).
            // Les deux points forment un puits d'escalier fixe: tous les étages utilisent
            // la même direction, garantissant des rampes cardinales valides (|dx|+|dy|==1).
            Point center = new Point(centerX, centerY);
            if (centerY - 1 >= minY)
                return new List<Point> { center, new Point(centerX, centerY - 1) };
            if (centerX + 1 <= maxX)
                return new List<Point> { center, new Point(centerX + 1, centerY) };
            if (centerY + 1 <= maxY)
                return new List<Point> { center, new Point(centerX, centerY + 1) };
            if (centerX - 1 >= minX)
                return new List<Point> { center, new Point(centerX - 1, centerY) };
            return new List<Point>();
        }

        private static void AddRampShaftConnections(
            List<RampTileData> ramps,
            List<Point> shaftPoints,
            int minFloor,
            int maxFloor,
            HashSet<(int Floor, int X, int Y)> occupiedRampCells)
        {
            if (shaftPoints == null || shaftPoints.Count < 2 || minFloor >= maxFloor)
                return;

            // Utilise une paire fixe (base, voisin) pour tous les étages.
            // La rotation par étage générait des déplacements diagonaux (|dx|+|dy|==2)
            // qui étaient tous rejetés, ne produisant aucun escalier.
            Point from = shaftPoints[0];
            Point to = shaftPoints[1];

            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            // Seules les directions cardinales sont utilisables comme rampe.
            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return;

            for (int floor = minFloor; floor < maxFloor; floor++)
            {
                bool fromOccupied = occupiedRampCells != null && occupiedRampCells.Contains((floor, from.X, from.Y));
                bool toOccupied = occupiedRampCells != null && occupiedRampCells.Contains((floor + 1, to.X, to.Y));
                if (fromOccupied || toOccupied)
                    continue;

                bool duplicate = ramps.Any(r =>
                    r.Floor == floor &&
                    r.X == from.X &&
                    r.Y == from.Y &&
                    r.AscendDx == dx &&
                    r.AscendDy == dy);

                if (duplicate)
                    continue;

                ramps.Add(new RampTileData
                {
                    X = from.X,
                    Y = from.Y,
                    Floor = floor,
                    AscendDx = dx,
                    AscendDy = dy,
                    Bidirectional = true
                });

                occupiedRampCells?.Add((floor, from.X, from.Y));
                occupiedRampCells?.Add((floor + 1, to.X, to.Y));
            }
        }

        private List<TerrainHeightData> GenerateTerrainRelief(int width, int height, EdgeWallGenerator.WallPattern? pattern)
        {
            if (pattern == EdgeWallGenerator.WallPattern.Trenches)
                return GenerateTrenchTerrain(width, height);

            // Toujours activer le relief pour la forêt (collines)
            bool forceRelief = pattern == EdgeWallGenerator.WallPattern.Forest;

            if (!terrainReliefEnabled && !forceRelief)
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
                "Sprint" => EdgeWallGenerator.WallPattern.Trenches,
                "Forêt" => EdgeWallGenerator.WallPattern.Forest,
                "The Hive" => EdgeWallGenerator.WallPattern.TheHive,
                _ => (EdgeWallGenerator.WallPattern)random.Next(0, 7)
            };
        }

        private int GetFloorCountForMission(string missionType)
        {
            return missionType switch
            {
                "Assault" => 8,
                "Centre-Ville" => 8,
                "Blackout" => random.Next(2, 4),
                "Forêt" => 1,
                "The Hive" => 1,
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

            // Forest Mission - Algeria
            var forest = generator.GenerateMap(50, 50, EdgeWallGenerator.WallPattern.Forest, "Forêt");
            forest.Description = "Pine forest in Algeria with scattered cabins and hills";
            forest.BiomeType = "Forest";
            MapCatalog.SaveMap(forest);

            Console.WriteLine("[MAP GEN] Created 7 premade maps");
        }
    }
}
