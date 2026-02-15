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

            if (currentMap.RampTiles.Count == 0 && currentMap.StairConnections != null)
            {
                foreach (var stair in currentMap.StairConnections)
                {
                    if (stair.ToFloor == stair.FromFloor + 1 && stair.ToX == stair.FromX && stair.ToY == stair.FromY - 1)
                    {
                        currentMap.RampTiles.Add(new RampTileData
                        {
                            X = stair.FromX,
                            Y = stair.FromY,
                            Floor = stair.FromFloor,
                            Bidirectional = stair.Bidirectional
                        });
                    }
                }
            }
            viewedFloor = 0;
            gridWidth = map.GridWidth;
            gridHeight = map.GridHeight;
            cellSize = map.CellSize;
            timeOfDay = map.TimeOfDay;
            dayNightSpeed = 1f / 86400f;

            // Charger les murs
            wallSegments = map.GetWalls();
            upperFloorCells = ComputeUpperFloorCells();

            Console.WriteLine($"[GAME] Loaded map: {map.Name} ({gridWidth}x{gridHeight})");

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
                pathfinding.UpdateGrid(gridWidth, gridHeight, wallSegments);

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
            currentPathEndFloor = viewedFloor;
            pathCosts.Clear();
            hoveredCell = new Point(-1, -1);
            throwTarget = new Point(-1, -1);

            // Réinitialiser spatial hash
            if (unitManager != null)
                unitManager.InitializeForMission(playerUnits, enemyUnits);
        }

        private static (int MinSize, int MaxSize) GetMissionMapSizeRange(string missionType)
        {
            return missionType switch
            {
                // Mission très dense (IA + bâtiments + étages) : limiter la taille évite les chutes de FPS.
                "Centre-Ville" => (30, 60),
                "Sabotage" => (32, 68),
                "Blackout" => (24, 52),
                _ => (20, 100)
            };
        }

        private HashSet<WallSegment> GetWallsForFloor(int floor)
        {
            if (floor == 0 || currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return wallSegments;

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

                AddExteriorWallsForFloor(filteredWalls, minX, minY, maxX, maxY);

                foreach (var wall in wallSegments)
                {
                    bool inBounds = wall.IsHorizontal
                        ? wall.Start.X >= minX && wall.End.X <= maxX && wall.Start.Y >= minY && wall.Start.Y <= maxY
                        : wall.Start.X >= minX && wall.Start.X <= maxX && wall.Start.Y >= minY && wall.End.Y <= maxY;

                    if (!inBounds)
                        continue;

                    if (ShouldSkipWallForFloor(building, wall, floor))
                        continue;

                    filteredWalls.Add(wall);
                }
            }

            return filteredWalls;
        }

        private static void AddExteriorWallsForFloor(HashSet<WallSegment> target, int minX, int minY, int maxX, int maxY)
        {
            if (target == null)
                return;

            if (maxX - minX < 2 || maxY - minY < 2)
                return;

            target.Add(new WallSegment(new Point(minX, minY), new Point(maxX, minY), true, WallType.Full));
            target.Add(new WallSegment(new Point(minX, maxY), new Point(maxX, maxY), true, WallType.Full));
            target.Add(new WallSegment(new Point(minX, minY), new Point(minX, maxY), false, WallType.Full));
            target.Add(new WallSegment(new Point(maxX, minY), new Point(maxX, maxY), false, WallType.Full));
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
            if (floor <= 1)
                return 0;

            int seed = building.X * 73856093 ^ building.Y * 19349663 ^ floor * 83492791;
            int roll = Math.Abs(seed % 100);

            // Quelques étages prennent du retrait pour créer terrasses et toits variés.
            return roll < 40 ? 1 : 0;
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

            if ((interiorHorizontal || interiorVertical) && roll < 18 + floor * 2)
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
                    if (wall.Type == WallType.Door)
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
            float denominator = Cross(r, s);
            Vector2 delta = q1 - p1;

            if (Math.Abs(denominator) <= epsilon)
            {
                // Segments parallèles (ou colinéaires).
                if (Math.Abs(Cross(delta, r)) > epsilon)
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

            float t = Cross(delta, s) / denominator;
            float u = Cross(delta, r) / denominator;

            if (t < -epsilon || t > 1f + epsilon || u < -epsilon || u > 1f + epsilon)
                return false;

            rayT = MathHelper.Clamp(t, 0f, 1f);
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private HashSet<Point> GetCellsForFloor(int floor)
        {
            if (floor <= 0)
            {
                if (floor == 0)
                    return new HashSet<Point>();

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

                return cells;
            }

            return upperFloorCells;
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

        private bool IsBlockedByWall(Point a, Point b)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return true;

            foreach (var wall in wallSegments)
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
