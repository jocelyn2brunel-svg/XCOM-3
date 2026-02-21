using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public partial class Game1
    {
        private void LoadMap(MapData map = null)
        {
            // Si aucune carte fournie, générer une carte aléatoire
            if (map == null)
            {
                (int minSize, int maxSize) = GetMissionMapSizeRange(selectedMission);

                map = mapGenerator.GenerateRandomMap(
                    selectedMission,
                    minWidth: minSize,
                    maxWidth: maxSize,
                    minHeight: minSize,
                    maxHeight: maxSize
                );
            }

            // Appliquer les données de la carte
            currentMap = map;
            currentMap.RampTiles ??= new List<RampTileData>();
            currentMap.TerrainHeights ??= new List<TerrainHeightData>();
            currentMap.HescoBarriers ??= new List<HescoBarrierData>();
            currentMap.Furnitures ??= new List<FurnitureData>();


            viewedFloor = 0;
            gridWidth = map.GridWidth;
            gridHeight = map.GridHeight;
            cellSize = map.CellSize;
            timeOfDay = map.TimeOfDay;
            dayNightSpeed = 1f / 86400f;

            // Charger les murs
            wallSegments = map.GetWalls();
            shatteredWindows.Clear();
            InvalidateWallsByFloorCache();
            InvalidateCellsByFloorCache();
            terrainHeights = currentMap.TerrainHeights
                .Where(t => t.X >= 0 && t.X < gridWidth && t.Y >= 0 && t.Y < gridHeight)
                .GroupBy(t => new Point(t.X, t.Y))
                .ToDictionary(g => g.Key, g => g.Last().HeightOffset);
            upperFloorCells = ComputeUpperFloorCells();
            roadCells = GenerateRoadCells();
            sidewalkCells = GenerateSidewalkCells(roadCells);

            Console.WriteLine($"[GAME] Loaded map: {map.Name} ({gridWidth}x{gridHeight}), roads={roadCells.Count}, sidewalks={sidewalkCells.Count}");

            // Réinitialiser la caméra
            if (camera != null)
            {
                camera = new CameraController(gridWidth, gridHeight, cellSize,
                                             GraphicsDevice.Viewport.AspectRatio);
                camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);

                if (selectedUnit != null)
                    camera.CenterOnPosition(selectedUnit.Cell.X * cellSize,
                                           selectedUnit.Cell.Y * cellSize);
            }

            // Mise à jour du pathfinding
            if (pathfinding != null)
            {
                var wallsByFloor = new Dictionary<int, HashSet<WallSegment>>();
                int floors = currentMap?.FloorCount ?? 1;
                int minF = GetMinimumViewFloor();
                for (int f = minF; f < floors; f++)
                {
                    wallsByFloor[f] = GetWallsForFloor(f);
                }
                pathfinding.UpdateGrid(gridWidth, gridHeight, wallsByFloor);
            }

            // Réinitialiser les unités
            foreach (var unit in playerUnits)
            {
                unit.UpdateVisualPosition(cellSize);
                unit.TargetPosition = unit.VisualPosition;
            }
            foreach (var unit in enemyUnits)
            {
                unit.UpdateVisualPosition(cellSize);
                unit.TargetPosition = unit.VisualPosition;
            }

            // Recalcul des cellules navigables
            if (selectedUnit != null && pathfinding != null)
                cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);

            currentPath.Clear();
            currentPathNodes.Clear();
            currentPathEndFloor = viewedFloor;
            pathCosts.Clear();
            hoveredCell = new Point(-1, -1);
            throwTarget = new Point(-1, -1);

            // Réinitialiser spatial hash
            if (unitManager != null)
                unitManager.InitializeForMission(playerUnits, enemyUnits);
        }


        private HashSet<Point> GenerateRoadCells()
        {
            HashSet<Point> roads = new HashSet<Point>();
            if (gridWidth <= 0 || gridHeight <= 0)
                return roads;

            int centerX = gridWidth / 2;
            int centerY = gridHeight / 2;
            int roadHalfWidth = 1; // Route de 3 cases de large

            // Axe principal horizontal
            for (int y = centerY - roadHalfWidth; y <= centerY + roadHalfWidth; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                    TryAddRoadCell(roads, x, y);
            }

            // Axe principal vertical
            for (int x = centerX - roadHalfWidth; x <= centerX + roadHalfWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                    TryAddRoadCell(roads, x, y);
            }

            // Connexions secondaires vers les zones de spawn
            foreach (var zone in (currentMap?.PlayerSpawnZones ?? Enumerable.Empty<SpawnZone>())
                .Concat(currentMap?.EnemySpawnZones ?? Enumerable.Empty<SpawnZone>()))
            {
                Point spawnCenter = new Point((zone.MinX + zone.MaxX) / 2, (zone.MinY + zone.MaxY) / 2);
                CarveRoadCorridor(roads, spawnCenter.X, spawnCenter.Y, centerX, spawnCenter.Y, roadHalfWidth);
                CarveRoadCorridor(roads, centerX, spawnCenter.Y, centerX, centerY, roadHalfWidth);
            }

            return roads;
        }

        private HashSet<Point> GenerateSidewalkCells(HashSet<Point> roads)
        {
            HashSet<Point> sidewalks = new HashSet<Point>();
            if (roads == null || roads.Count == 0)
                return sidewalks;

            foreach (Point road in roads)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        int x = road.X + dx;
                        int y = road.Y + dy;
                        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                            continue;

                        Point sidewalk = new Point(x, y);
                        if (roads.Contains(sidewalk))
                            continue;
                        if (IsInsideBuildingFootprint(sidewalk))
                            continue;
                        if (upperFloorCells.Contains(sidewalk))
                            continue;

                        sidewalks.Add(sidewalk);
                    }
                }
            }

            return sidewalks;
        }

        private void CarveRoadCorridor(HashSet<Point> roads, int startX, int startY, int endX, int endY, int halfWidth)
        {
            int x = startX;
            int y = startY;
            while (x != endX)
            {
                AddRoadBand(roads, x, y, halfWidth);
                x += Math.Sign(endX - x);
            }

            while (y != endY)
            {
                AddRoadBand(roads, x, y, halfWidth);
                y += Math.Sign(endY - y);
            }

            AddRoadBand(roads, endX, endY, halfWidth);
        }

        private void AddRoadBand(HashSet<Point> roads, int centerX, int centerY, int halfWidth)
        {
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
            {
                for (int dy = -halfWidth; dy <= halfWidth; dy++)
                {
                    TryAddRoadCell(roads, centerX + dx, centerY + dy);
                }
            }
        }

        private void TryAddRoadCell(HashSet<Point> roads, int x, int y)
        {
            if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                return;

            var cell = new Point(x, y);
            if (IsInsideBuildingFootprint(cell))
                return;

            if (upperFloorCells.Contains(cell))
                return;

            roads.Add(cell);
        }

        private bool IsInsideBuildingFootprint(Point cell)
        {
            return GetBuildingIndexAt(cell) != -1;
        }

        private int GetBuildingIndexAt(Point cell)
        {
            if (currentMap?.Buildings == null)
                return -1;

            for (int i = 0; i < currentMap.Buildings.Count; i++)
            {
                var building = currentMap.Buildings[i];
                if (cell.X >= building.X && cell.X < building.X + building.Width &&
                    cell.Y >= building.Y && cell.Y < building.Y + building.Height)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsWallExterior(WallSegment wall)
        {
            var adj = GetCellsAdjacentToWall(wall).ToList();
            if (adj.Count < 2) return false;

            int b0 = GetBuildingIndexAt(adj[0]);
            int b1 = GetBuildingIndexAt(adj[1]);

            if (b0 == -1 && b1 == -1) return false; // Les deux sont à l'extérieur
            return b0 != b1; // Un dedans/un dehors OU deux bâtiments différents
        }

        private static (int MinSize, int MaxSize) GetMissionMapSizeRange(string missionType)
        {
            return missionType switch
            {
                // Mission très dense (IA + bâtiments + étages) : taille augmentée pour doubler la superficie jouable.
                "Centre-Ville" => (42, 85),
                "Sabotage" => (32, 68),
                "Blackout" => (24, 52),
                _ => (20, 100)
            };
        }

        private HashSet<WallSegment> GetWallsForFloor(int floor)
        {
            if (wallsByFloorCache.TryGetValue(floor, out var cachedWalls))
                return cachedWalls;

            if (floor == 0 || currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
            {
                var baseFloorWalls = new HashSet<WallSegment>();
                foreach (var wall in wallSegments)
                    baseFloorWalls.Add(ApplyShatteredWindowState(wall, floor));

                wallsByFloorCache[floor] = baseFloorWalls;
                return baseFloorWalls;
            }

            var filteredWalls = new HashSet<WallSegment>();

            foreach (var building in currentMap.Buildings)
            {
                if (floor > 0)
                {
                    if (building.FloorCount <= floor)
                        continue;
                }
                else
                {
                    if (building.BasementCount < Math.Abs(floor))
                        continue;
                }

                int minX = building.X;
                int minY = building.Y;
                int maxX = building.X + building.Width;
                int maxY = building.Y + building.Height;

                int setback = GetFloorSetback(building, floor);
                minX += setback;
                minY += setback;
                maxX -= setback;
                maxY -= setback;

                if (maxX - minX < 3 || maxY - minY < 3)
                    continue;

                AddExteriorWallsForFloor(filteredWalls, minX, minY, maxX, maxY, floor);

                foreach (var wall in wallSegments)
                {
                    bool inBounds = wall.IsHorizontal
                        ? wall.Start.X >= minX && wall.End.X <= maxX && wall.Start.Y >= minY && wall.Start.Y <= maxY
                        : wall.Start.X >= minX && wall.Start.X <= maxX && wall.Start.Y >= minY && wall.End.Y <= maxY;

                    if (!inBounds)
                        continue;

                    if (ShouldSkipWallForFloor(building, wall, floor))
                        continue;

                    if (floor > 0 && wall.Type == WallType.Door)
                        continue;

                    WallSegment wallForFloor = wall;
                    if (floor > 0 && wall.Type == WallType.Full && IsPerimeterWall(wall, minX, minY, maxX, maxY))
                    {
                        int roll = Math.Abs((
                            floor * 16057 +
                            wall.Start.X * 92821 +
                            wall.Start.Y * 68917 +
                            wall.End.X * 5171 +
                            wall.End.Y * 3463) % 100);

                        if (roll < 65)
                            wallForFloor = new WallSegment(wall.Start, wall.End, wall.IsHorizontal, WallType.Window, wall.Material);
                    }

                    wallForFloor = ApplyShatteredWindowState(wallForFloor, floor);

                    filteredWalls.Add(wallForFloor);
                }
            }

            wallsByFloorCache[floor] = filteredWalls;
            return filteredWalls;
        }

        private void InvalidateWallsByFloorCache()
        {
            wallsByFloorCache.Clear();
        }

        private void InvalidateCellsByFloorCache()
        {
            cellsByFloorCache.Clear();
        }

        private static bool IsPerimeterWall(WallSegment wall, int minX, int minY, int maxX, int maxY)
        {
            return wall.IsHorizontal
                ? (wall.Start.Y == minY || wall.Start.Y == maxY)
                : (wall.Start.X == minX || wall.Start.X == maxX);
        }

        private void AddExteriorWallsForFloor(HashSet<WallSegment> target, int minX, int minY, int maxX, int maxY, int floor)
        {
            if (target == null)
                return;

            if (maxX - minX < 2 || maxY - minY < 2)
                return;

            for (int x = minX; x < maxX; x++)
            {
                WallType northType = WallType.Full;
                WallType southType = WallType.Full;

                if (floor > 0)
                {
                    int northRoll = Math.Abs((floor * 16057 + x * 92821 + minY * 68917 + 11) % 100);
                    int southRoll = Math.Abs((floor * 16057 + x * 92821 + maxY * 68917 + 17) % 100);

                    if (northRoll < 65)
                        northType = WallType.Window;
                    if (southRoll < 65)
                        southType = WallType.Window;
                }

                WallSegment northWall = new WallSegment(new Point(x, minY), new Point(x + 1, minY), true, northType, WallMaterial.Brick);
                WallSegment southWall = new WallSegment(new Point(x, maxY), new Point(x + 1, maxY), true, southType, WallMaterial.Brick);

                target.Add(ApplyShatteredWindowState(northWall, floor));
                target.Add(ApplyShatteredWindowState(southWall, floor));
            }

            for (int y = minY; y < maxY; y++)
            {
                WallType westType = WallType.Full;
                WallType eastType = WallType.Full;

                if (floor > 0)
                {
                    int westRoll = Math.Abs((floor * 16057 + minX * 92821 + y * 68917 + 23) % 100);
                    int eastRoll = Math.Abs((floor * 16057 + maxX * 92821 + y * 68917 + 29) % 100);

                    if (westRoll < 65)
                        westType = WallType.Window;
                    if (eastRoll < 65)
                        eastType = WallType.Window;
                }

                WallSegment westWall = new WallSegment(new Point(minX, y), new Point(minX, y + 1), false, westType, WallMaterial.Brick);
                WallSegment eastWall = new WallSegment(new Point(maxX, y), new Point(maxX, y + 1), false, eastType, WallMaterial.Brick);

                target.Add(ApplyShatteredWindowState(westWall, floor));
                target.Add(ApplyShatteredWindowState(eastWall, floor));
            }
        }

        private WallSegment ApplyShatteredWindowState(WallSegment wall, int floor)
        {
            if (wall.Type != WallType.Window)
                return wall;

            WindowInstance instance = new WindowInstance(floor, wall);
            if (!shatteredWindows.Contains(instance))
                return wall;

            return new WallSegment(wall.Start, wall.End, wall.IsHorizontal, WallType.ShatteredWindow, wall.Material);
        }

        private HashSet<WallSegment> FilterUpperFloorWallsForLowerView(int sourceFloor, int viewedFloor, HashSet<WallSegment> walls)
        {
            if (sourceFloor <= viewedFloor || currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return walls;

            var filteredWalls = new HashSet<WallSegment>();
            foreach (var wall in walls)
            {
                if (!IsInteriorWallOnFloor(wall, sourceFloor))
                    filteredWalls.Add(wall);
            }

            return filteredWalls;
        }

        private bool IsInteriorWallOnFloor(WallSegment wall, int floor)
        {
            if (currentMap?.Buildings == null)
                return false;

            foreach (var building in currentMap.Buildings)
            {
                if (!BuildingHasFloor(building, floor))
                    continue;

                int setback = GetFloorSetback(building, floor);
                int minX = building.X + setback;
                int minY = building.Y + setback;
                int maxX = building.X + building.Width - setback;
                int maxY = building.Y + building.Height - setback;

                bool inBounds = wall.IsHorizontal
                    ? wall.Start.X >= minX && wall.End.X <= maxX && wall.Start.Y >= minY && wall.Start.Y <= maxY
                    : wall.Start.X >= minX && wall.Start.X <= maxX && wall.Start.Y >= minY && wall.End.Y <= maxY;

                if (!inBounds)
                    continue;

                if (wall.IsHorizontal)
                    return wall.Start.Y > minY && wall.Start.Y < maxY;

                return wall.Start.X > minX && wall.Start.X < maxX;
            }

            return false;
        }

        private static bool BuildingHasFloor(BuildingFootprintData building, int floor)
        {
            if (floor >= 0)
                return building.FloorCount > floor;

            return building.BasementCount >= Math.Abs(floor);
        }

        private int GetFloorSetback(BuildingFootprintData building, int floor)
        {
            // Les étages doivent conserver la même empreinte que le périmètre du bâtiment.
            // Aucun retrait n'est appliqué.
            return 0;
        }

        private bool ShouldSkipWallForFloor(BuildingFootprintData building, WallSegment wall, int floor)
        {
            if (floor <= 0)
                return false;

            int setback = GetFloorSetback(building, floor);
            int minX = building.X + setback;
            int minY = building.Y + setback;
            int maxX = building.X + building.Width - setback;
            int maxY = building.Y + building.Height - setback;

            bool onPerimeter = wall.IsHorizontal
                ? (wall.Start.Y == minY || wall.Start.Y == maxY)
                : (wall.Start.X == minX || wall.Start.X == maxX);

            // Les façades doivent toujours rester présentes sur l'empreinte réelle de l'étage,
            // y compris quand l'étage applique un retrait (setback).
            if (onPerimeter)
                return false;

            int seed =
                building.X * 92821 +
                building.Y * 68917 +
                floor * 15401 +
                wall.Start.X * 733 +
                wall.Start.Y * 547 +
                wall.End.X * 389 +
                wall.End.Y * 277;

            int roll = Math.Abs(seed % 100);

            // Retirer ponctuellement des cloisons intérieures sur les étages.
            bool interiorHorizontal = wall.IsHorizontal && wall.Start.Y > minY && wall.Start.Y < maxY;
            bool interiorVertical = !wall.IsHorizontal && wall.Start.X > minX && wall.Start.X < maxX;

            if ((interiorHorizontal || interiorVertical) && roll < Math.Min(70, 30 + floor * 10))
                return true;

            // Ne retire plus les façades en étage: cela créait des murs manquants sur certaines générations.
            return false;
        }

        private int GetMinimumViewFloor()
        {
            if (currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return 0;

            int deepestBasement = currentMap.Buildings.Max(b => Math.Max(0, b.BasementCount));
            return -deepestBasement;
        }

        private HashSet<WallSegment> FilterCameraFacingWallsForNonViewedFloor(HashSet<WallSegment> walls)
        {
            if (walls == null || walls.Count == 0)
                return walls;

            var filteredWalls = new HashSet<WallSegment>(walls.Where(w => !IsWallFacingCamera(w, camera.Position)));

            // Garde-fou: ne jamais vider complètement les murs d'un étage.
            return filteredWalls.Count > 0 ? filteredWalls : walls;
        }

        private bool IsWallFacingCamera(WallSegment wall, Vector3 cameraPos)
        {
            float wallCenterX = (wall.Start.X + wall.End.X) * 0.5f * cellSize;
            float wallCenterZ = (wall.Start.Y + wall.End.Y) * 0.5f * cellSize;
            float sideTolerance = cellSize * 0.1f;

            if (wall.IsHorizontal)
            {
                float dz = cameraPos.Z - wallCenterZ;
                if (Math.Abs(dz) <= sideTolerance)
                    return false;

                return dz > 0f;
            }

            float dx = cameraPos.X - wallCenterX;
            if (Math.Abs(dx) <= sideTolerance)
                return false;

            return dx > 0f;
        }

        private void ComputeOcclusionFromWalls(
            IEnumerable<WallSegment> walls,
            IEnumerable<Unit> units,
            float floorHeightOffset,
            HashSet<WallSegment> fadedWalls,
            HashSet<Unit> occludedUnits)
        {
            Vector3 cameraPos = camera.Position;

            foreach (var unit in units)
            {
                Vector3 unitPosition = unit.VisualPosition + new Vector3(0f, cellSize * 0.9f, 0f);
                bool blocked = false;

                foreach (var wall in walls)
                {
                    if (wall.Type == WallType.Door || wall.Type == WallType.ShatteredWindow)
                        continue;

                    if (!IsWallBetweenCameraAndUnit(wall, floorHeightOffset, cameraPos, unitPosition))
                        continue;

                    fadedWalls.Add(wall);
                    blocked = true;
                }

                if (blocked)
                    occludedUnits.Add(unit);
            }
        }

        private List<Unit> GetVisibleUnitsForFloor(int floor)
        {
            return playerUnits.Where(u => u.Floor == floor && u.Health > 0)
                .Concat(enemyUnits.Where(u => u.Floor == floor && u.Health > 0 && IsEnemyVisibleToPlayers(u)))
                .ToList();
        }

        private bool IsWallBetweenCameraAndUnit(WallSegment wall, float floorHeightOffset, Vector3 cameraPos, Vector3 unitPos)
        {
            Vector2 camera2D = new Vector2(cameraPos.X, cameraPos.Z);
            Vector2 unit2D = new Vector2(unitPos.X, unitPos.Z);

            Vector2 wallStart = new Vector2(wall.Start.X * cellSize, wall.Start.Y * cellSize);
            Vector2 wallEnd = new Vector2(wall.End.X * cellSize, wall.End.Y * cellSize);

            if (!TryGetSegmentIntersectionParam(camera2D, unit2D, wallStart, wallEnd, out float rayT))
                return false;

            float wallHeight = cellSize * WallHeightRatio;
            float wallBottom = floorHeightOffset;
            float wallTop = floorHeightOffset + wallHeight;

            // Évaluer la hauteur réelle du rayon caméra->cible au point d'intersection avec le mur.
            // Cela évite de partir du "pied" de caméra (projection au sol) et respecte la vraie hauteur Y de la caméra.
            float rayHeightAtWall = MathHelper.Lerp(cameraPos.Y, unitPos.Y, rayT);
            const float verticalTolerance = 0.5f;
            return rayHeightAtWall >= wallBottom - verticalTolerance &&
                   rayHeightAtWall <= wallTop + verticalTolerance;
        }

        private static bool TryGetSegmentIntersectionParam(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out float rayT)
        {
            const float epsilon = 0.0001f;
            rayT = 0f;

            Vector2 r = p2 - p1;
            Vector2 s = q2 - q1;
            float denominator = Cross2D(r, s);
            Vector2 delta = q1 - p1;

            if (Math.Abs(denominator) <= epsilon)
            {
                // Segments parallèles (ou colinéaires).
                if (Math.Abs(Cross2D(delta, r)) > epsilon)
                    return false;

                float rLenSq = r.LengthSquared();
                if (rLenSq <= epsilon)
                    return false;

                float t0 = Vector2.Dot(q1 - p1, r) / rLenSq;
                float t1 = Vector2.Dot(q2 - p1, r) / rLenSq;
                float minT = Math.Max(0f, Math.Min(t0, t1));
                float maxT = Math.Min(1f, Math.Max(t0, t1));

                if (minT > maxT)
                    return false;

                rayT = minT;
                return true;
            }

            float t = Cross2D(delta, s) / denominator;
            float u = Cross2D(delta, r) / denominator;

            if (t < -epsilon || t > 1f + epsilon || u < -epsilon || u > 1f + epsilon)
                return false;

            rayT = MathHelper.Clamp(t, 0f, 1f);
            return true;
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private HashSet<Point> GetCellsForFloor(int floor)
        {
            if (floor == 0)
                return new HashSet<Point>();

            if (cellsByFloorCache.TryGetValue(floor, out var cached))
                return cached;

            HashSet<Point> result = ComputeCellsForFloor(floor);
            cellsByFloorCache[floor] = result;
            return result;
        }

        private bool IsSlabAt(Point cell, int floor)
        {
            if (floor == 0) return true;
            return GetCellsForFloor(floor).Contains(cell);
        }

        private bool IsCellCovered(Point cell, int floor)
        {
            int maxF = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);
            for (int f = floor + 1; f <= maxF; f++)
            {
                if (IsSlabAt(cell, f)) return true;
            }
            return false;
        }

        private bool[,] GetSlabMaskForFloor(int floor)
        {
            var mask = new bool[gridWidth, gridHeight];
            if (floor == 0)
            {
                for (int x = 0; x < gridWidth; x++)
                    for (int y = 0; y < gridHeight; y++)
                        mask[x, y] = true;
                return mask;
            }

            var floorCells = GetCellsForFloor(floor);
            foreach (var cell in floorCells)
            {
                if (cell.X >= 0 && cell.X < gridWidth && cell.Y >= 0 && cell.Y < gridHeight)
                    mask[cell.X, cell.Y] = true;
            }
            return mask;
        }

        private HashSet<Point> ComputeCellsForFloor(int floor)
        {
            if (floor < 0)
            {
                var basementCells = new HashSet<Point>();
                if (currentMap?.Buildings == null)
                    return basementCells;

                int basementLevel = Math.Abs(floor);
                foreach (var building in currentMap.Buildings)
                {
                    if (building.BasementCount < basementLevel)
                        continue;

                    int minX = Math.Max(0, building.X);
                    int minY = Math.Max(0, building.Y);
                    int maxX = Math.Min(gridWidth, building.X + building.Width);
                    int maxY = Math.Min(gridHeight, building.Y + building.Height);

                    for (int x = minX; x < maxX; x++)
                    {
                        for (int y = minY; y < maxY; y++)
                        {
                            basementCells.Add(new Point(x, y));
                        }
                    }
                }

                return basementCells;
            }

            if (currentMap?.Buildings != null && currentMap.Buildings.Count > 0)
            {
                var cells = new HashSet<Point>();
                foreach (var building in currentMap.Buildings)
                {
                    if (building.FloorCount <= floor)
                        continue;

                    int setback = GetFloorSetback(building, floor);
                    int minX = Math.Max(0, building.X + setback);
                    int minY = Math.Max(0, building.Y + setback);
                    int maxX = Math.Min(gridWidth, building.X + building.Width - setback);
                    int maxY = Math.Min(gridHeight, building.Y + building.Height - setback);

                    for (int x = minX; x < maxX; x++)
                    {
                        for (int y = minY; y < maxY; y++)
                        {
                            cells.Add(new Point(x, y));
                        }
                    }
                }

                // Réserver une ouverture 2x1 dans le plancher au-dessus de chaque rampe.
                // Pour un passage entre floor-1 -> floor, on enlève les 2 cases du puits
                // (la case de départ de la rampe + la case d'arrivée sur l'étage supérieur).
                if (currentMap?.RampTiles != null && currentMap.RampTiles.Count > 0)
                {
                    foreach (var ramp in currentMap.RampTiles)
                    {
                        if (ramp.Floor != floor - 1)
                            continue;

                        int rampDx = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDx : 0;
                        int rampDy = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDy : -1;

                        cells.Remove(new Point(ramp.X, ramp.Y));
                        cells.Remove(new Point(ramp.X + rampDx, ramp.Y + rampDy));
                    }
                }

                return cells;
            }

            return upperFloorCells;
        }




        private List<FurnitureData> GetFurnitureForFloor(int floor)
        {
            if (currentMap?.Furnitures == null || currentMap.Furnitures.Count == 0)
                return new List<FurnitureData>();

            return currentMap.Furnitures
                .Where(f => f.Floor == floor)
                .ToList();
        }

        private List<Point> GetHescoBarriersForFloor(int floor)
        {
            if (currentMap?.HescoBarriers == null || currentMap.HescoBarriers.Count == 0)
                return new List<Point>();

            return currentMap.HescoBarriers
                .Where(b => b.Floor == floor)
                .Select(b => new Point(b.X, b.Y))
                .ToList();
        }

        private HashSet<Point> GetExteriorCells(HashSet<Point> blockedCells)
        {
            var exteriorCells = new HashSet<Point>();

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Point cell = new Point(x, y);
                    if (!blockedCells.Contains(cell))
                        exteriorCells.Add(cell);
                }
            }

            return exteriorCells;
        }

        private HashSet<Point> ComputeUpperFloorCells()
        {
            var outsideCells = new HashSet<Point>();
            var queue = new Queue<Point>();

            for (int x = 0; x < gridWidth; x++)
            {
                EnqueueBoundaryCell(new Point(x, 0), outsideCells, queue);
                EnqueueBoundaryCell(new Point(x, gridHeight - 1), outsideCells, queue);
            }

            for (int y = 1; y < gridHeight - 1; y++)
            {
                EnqueueBoundaryCell(new Point(0, y), outsideCells, queue);
                EnqueueBoundaryCell(new Point(gridWidth - 1, y), outsideCells, queue);
            }

            while (queue.Count > 0)
            {
                Point current = queue.Dequeue();
                foreach (var neighbor in GetCardinalNeighbors(current))
                {
                    if (outsideCells.Contains(neighbor) || IsBlockedByWall(current, neighbor))
                        continue;

                    outsideCells.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            var interiorCells = new HashSet<Point>();
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Point cell = new Point(x, y);
                    if (!outsideCells.Contains(cell))
                    {
                        interiorCells.Add(cell);
                    }
                }
            }

            return interiorCells;
        }

        private void EnqueueBoundaryCell(Point cell, HashSet<Point> visited, Queue<Point> queue)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= gridWidth || cell.Y >= gridHeight || visited.Contains(cell))
                return;

            visited.Add(cell);
            queue.Enqueue(cell);
        }

        private IEnumerable<Point> GetCardinalNeighbors(Point cell)
        {
            Point[] neighbors =
            {
                new Point(cell.X, cell.Y - 1),
                new Point(cell.X, cell.Y + 1),
                new Point(cell.X - 1, cell.Y),
                new Point(cell.X + 1, cell.Y)
            };

            foreach (var neighbor in neighbors)
            {
                if (neighbor.X >= 0 && neighbor.X < gridWidth && neighbor.Y >= 0 && neighbor.Y < gridHeight)
                    yield return neighbor;
            }
        }

        private bool IsBlockedByWall(Point a, Point b, int floor = 0)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return true;

            var wallsOnFloor = GetWallsForFloor(floor);
            foreach (var wall in wallsOnFloor)
            {
                bool isBetweenCells =
                    (dy == 1 && wall.IsHorizontal && wall.Start.Y == b.Y && a.X >= wall.Start.X && a.X < wall.End.X) ||
                    (dy == -1 && wall.IsHorizontal && wall.Start.Y == a.Y && a.X >= wall.Start.X && a.X < wall.End.X) ||
                    (dx == 1 && !wall.IsHorizontal && wall.Start.X == b.X && a.Y >= wall.Start.Y && a.Y < wall.End.Y) ||
                    (dx == -1 && !wall.IsHorizontal && wall.Start.X == a.X && a.Y >= wall.Start.Y && a.Y < wall.End.Y);

                if (isBetweenCells && (wall.Type == WallType.Full || wall.Type == WallType.Window))
                    return true;
            }

            return false;
        }

    }
}
