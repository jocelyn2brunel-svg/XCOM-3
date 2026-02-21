using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    enum BuildingType
    {
        SmallHouse,
        Apartment,
        Office,
        Warehouse
    }

    public struct GeneratedBuilding
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int FloorCount;
        public int BasementCount;

        public GeneratedBuilding(int x, int y, int width, int height, int floorCount, int basementCount = 0)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            FloorCount = floorCount;
            BasementCount = basementCount;
        }
    }

    /// <summary>
    /// Représente un segment de mur entre deux cases
    /// </summary>
    public enum WallType { Full, Window, Door, ShatteredWindow }
    public enum WallMaterial { Standard, Brick, Hesco }

    public struct WallSegment
    {
        public Point Start;
        public Point End;
        public bool IsHorizontal;
        public WallType Type;
        public WallMaterial Material;

        public WallSegment(Point start, Point end, bool isHorizontal, WallType type = WallType.Full, WallMaterial material = WallMaterial.Standard)
        {
            Start = start;
            End = end;
            IsHorizontal = isHorizontal;
            Type = type;
            Material = material;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is WallSegment)) return false;
            WallSegment other = (WallSegment)obj;
            return Start == other.Start && End == other.End && IsHorizontal == other.IsHorizontal;
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() ^ End.GetHashCode() ^ IsHorizontal.GetHashCode();
        }
    }

    /// <summary>
    /// Générateur de murs qui crée des segments entre les cases (pas sur les cases)
    /// </summary>
    public class EdgeWallGenerator
    {
        private Random random;
        public List<GeneratedBuilding> LastGeneratedBuildings { get; } = new List<GeneratedBuilding>();
        public List<Point> LastGeneratedHescoBarriers { get; } = new List<Point>();

        public enum WallPattern
        {
            Rooms,          // Pièces avec couloirs
            Maze,           // Labyrinthe
            Scattered,      // Murs éparpillés
            Bunker,         // Structure défensive
            Urban,          // Bâtiments urbains
            Trenches,       // Tranchées de guerre
            Forest,         // Forêt avec cabanes
            TheHive         // Bunker souterrain high-tech
        }

        public EdgeWallGenerator(Random rng)
        {
            random = rng;
        }

        /// <summary>
        /// Génère des segments de murs selon le pattern choisi
        /// </summary>
        public HashSet<WallSegment> GenerateWalls(int gridWidth, int gridHeight, WallPattern pattern, int density = 20)
        {
            LastGeneratedBuildings.Clear();
            LastGeneratedHescoBarriers.Clear();

            if (gridWidth < 10 || gridHeight < 10)
            {
                Console.WriteLine("Grid too small, using scattered pattern");
                return GenerateScattered(gridWidth, gridHeight, Math.Max(5, density / 2));
            }

            try
            {
                switch (pattern)
                {
                    case WallPattern.Rooms:
                        return GenerateRooms(gridWidth, gridHeight, density);
                    case WallPattern.Maze:
                        return GenerateMaze(gridWidth, gridHeight);
                    case WallPattern.Scattered:
                        return GenerateScattered(gridWidth, gridHeight, density);
                    case WallPattern.Bunker:
                        return GenerateBunker(gridWidth, gridHeight);
                    case WallPattern.Urban:
                        return GenerateUrban(gridWidth, gridHeight);
                    case WallPattern.Trenches:
                        return GenerateTrenches(gridWidth, gridHeight);
                    case WallPattern.Forest:
                        return GenerateForest(gridWidth, gridHeight);
                    case WallPattern.TheHive:
                        return GenerateTheHive(gridWidth, gridHeight);
                    default:
                        return GenerateRooms(gridWidth, gridHeight, density);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating walls: {ex.Message}, falling back");
                return GenerateScattered(gridWidth, gridHeight, Math.Max(5, density / 2));
            }
        }

        /// <summary>
        /// Ajoute un mur horizontal entre (x,y) et (x+1,y) avec type spécifié ou aléatoire
        /// </summary>
        public void AddHorizontalWall(HashSet<WallSegment> walls, int x, int y, WallType type = WallType.Full, bool randomWindows = false, int windowChance = 20, WallMaterial material = WallMaterial.Standard)
        {
            if (type == WallType.Full && randomWindows && random.Next(100) < windowChance)
            {
                type = WallType.Window;
            }
            walls.Add(new WallSegment(new Point(x, y), new Point(x + 1, y), true, type, material));
        }

        /// <summary>
        /// Ajoute un mur vertical entre (x,y) et (x,y+1) avec type spécifié ou aléatoire
        /// </summary>
        public void AddVerticalWall(HashSet<WallSegment> walls, int x, int y, WallType type = WallType.Full, bool randomWindows = false, int windowChance = 20, WallMaterial material = WallMaterial.Standard)
        {
            if (type == WallType.Full && randomWindows && random.Next(100) < windowChance)
            {
                type = WallType.Window;
            }
            walls.Add(new WallSegment(new Point(x, y), new Point(x, y + 1), false, type, material));
        }

        /// <summary>
        /// Ajoute une porte horizontale (supprime le mur et ajoute un segment Door)
        /// </summary>
        private void AddHorizontalDoor(HashSet<WallSegment> walls, int x, int y)
        {
            var wallToRemove = new WallSegment(new Point(x, y), new Point(x + 1, y), true);
            WallMaterial material = WallMaterial.Standard;
            if (walls.TryGetValue(wallToRemove, out var existingWall))
                material = existingWall.Material;
            walls.Remove(wallToRemove);
            walls.Add(new WallSegment(new Point(x, y), new Point(x + 1, y), true, WallType.Door, material));
        }

        /// <summary>
        /// Ajoute une porte verticale (supprime le mur et ajoute un segment Door)
        /// </summary>
        private void AddVerticalDoor(HashSet<WallSegment> walls, int x, int y)
        {
            var wallToRemove = new WallSegment(new Point(x, y), new Point(x, y + 1), false);
            WallMaterial material = WallMaterial.Standard;
            if (walls.TryGetValue(wallToRemove, out var existingWall))
                material = existingWall.Material;
            walls.Remove(wallToRemove);
            walls.Add(new WallSegment(new Point(x, y), new Point(x, y + 1), false, WallType.Door, material));
        }

        /// <summary>
        /// Génère des pièces rectangulaires avec portes
        /// </summary>
        private HashSet<WallSegment> GenerateRooms(int gridWidth, int gridHeight, int density)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();
            int numRooms = Math.Max(2, Math.Min(density / 10, 6));

            for (int i = 0; i < numRooms; i++)
            {
                int roomWidth = random.Next(4, Math.Min(9, gridWidth - 6));
                int roomHeight = random.Next(4, Math.Min(8, gridHeight - 6));

                int startX = random.Next(3, Math.Max(4, gridWidth - roomWidth - 3));
                int startY = random.Next(3, Math.Max(4, gridHeight - roomHeight - 3));

                // Murs horizontaux (nord et sud) - Extérieurs avec fenêtres
                for (int x = startX; x < startX + roomWidth; x++)
                {
                    AddHorizontalWall(walls, x, startY, WallType.Full, true, 30);           // Mur nord avec fenêtres
                    AddHorizontalWall(walls, x, startY + roomHeight, WallType.Full, true, 30); // Mur sud avec fenêtres
                }

                // Murs verticaux (ouest et est) - Extérieurs avec fenêtres
                for (int y = startY; y < startY + roomHeight; y++)
                {
                    AddVerticalWall(walls, startX, y, WallType.Full, true, 30);                  // Mur ouest avec fenêtres
                    AddVerticalWall(walls, startX + roomWidth, y, WallType.Full, true, 30);      // Mur est avec fenêtres
                }

                // Ajouter 1-3 portes
                int numDoors = random.Next(1, 4);
                for (int d = 0; d < numDoors; d++)
                {
                    int side = random.Next(4);

                    switch (side)
                    {
                        case 0: // Nord
                            if (roomWidth > 2)
                            {
                                int doorX = startX + random.Next(1, roomWidth - 1);
                                AddHorizontalDoor(walls, doorX, startY);
                            }
                            break;
                        case 1: // Sud
                            if (roomWidth > 2)
                            {
                                int doorX = startX + random.Next(1, roomWidth - 1);
                                AddHorizontalDoor(walls, doorX, startY + roomHeight);
                            }
                            break;
                        case 2: // Ouest
                            if (roomHeight > 2)
                            {
                                int doorY = startY + random.Next(1, roomHeight - 1);
                                AddVerticalDoor(walls, startX, doorY);
                            }
                            break;
                        case 3: // Est
                            if (roomHeight > 2)
                            {
                                int doorY = startY + random.Next(1, roomHeight - 1);
                                AddVerticalDoor(walls, startX + roomWidth, doorY);
                            }
                            break;
                    }
                }
            }

            return walls;
        }

        /// <summary>
        /// Génère un labyrinthe
        /// </summary>
        private HashSet<WallSegment> GenerateMaze(int gridWidth, int gridHeight)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();

            if (gridWidth < 12 || gridHeight < 12)
                return GenerateRooms(gridWidth, gridHeight, 20);

            // Bordures extérieures
            for (int x = 0; x < gridWidth; x++)
            {
                AddHorizontalWall(walls, x, 0);
                AddHorizontalWall(walls, x, gridHeight);
            }
            for (int y = 0; y < gridHeight; y++)
            {
                AddVerticalWall(walls, 0, y);
                AddVerticalWall(walls, gridWidth, y);
            }

            // Lignes internes du labyrinthe
            DivideMaze(walls, 1, 1, gridWidth - 1, gridHeight - 1);

            return walls;
        }

        private void DivideMaze(HashSet<WallSegment> walls, int x, int y, int width, int height)
        {
            if (width < 3 || height < 3) return;

            bool horizontal = height > width;

            if (horizontal)
            {
                int wallY = y + random.Next(1, Math.Max(2, height - 1));
                int gapX = x + random.Next(0, Math.Max(1, width));

                // Créer un mur horizontal avec une ouverture
                for (int i = x; i < x + width; i++)
                {
                    if (i != gapX)
                        AddHorizontalWall(walls, i, wallY);
                }

                DivideMaze(walls, x, y, width, wallY - y);
                DivideMaze(walls, x, wallY, width, y + height - wallY);
            }
            else
            {
                int wallX = x + random.Next(1, Math.Max(2, width - 1));
                int gapY = y + random.Next(0, Math.Max(1, height));

                // Créer un mur vertical avec une ouverture
                for (int i = y; i < y + height; i++)
                {
                    if (i != gapY)
                        AddVerticalWall(walls, wallX, i);
                }

                DivideMaze(walls, x, y, wallX - x, height);
                DivideMaze(walls, wallX, y, x + width - wallX, height);
            }
        }

        /// <summary>
        /// Génère des murs éparpillés
        /// </summary>
        private HashSet<WallSegment> GenerateScattered(int gridWidth, int gridHeight, int density)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();
            int numWalls = Math.Max(10, density * 2);

            for (int i = 0; i < numWalls; i++)
            {
                int x = random.Next(2, gridWidth - 2);
                int y = random.Next(2, gridHeight - 2);
                int length = random.Next(2, 6);
                bool horizontal = random.Next(2) == 0;

                if (horizontal)
                {
                    for (int j = 0; j < length && x + j < gridWidth - 1; j++)
                        AddHorizontalWall(walls, x + j, y);
                }
                else
                {
                    for (int j = 0; j < length && y + j < gridHeight - 1; j++)
                        AddVerticalWall(walls, x, y + j);
                }
            }

            return walls;
        }

        /// <summary>
        /// Génère un bunker défensif
        /// </summary>
        private HashSet<WallSegment> GenerateBunker(int gridWidth, int gridHeight)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();

            // Murs périmétriques avec créneaux
            for (int x = 1; x < gridWidth - 1; x++)
            {
                if (x % 4 != 0) // Créneaux
                {
                    AddHorizontalWall(walls, x, 2);
                    AddHorizontalWall(walls, x, gridHeight - 3);
                }
            }

            // Barricades internes
            int numBarricades = random.Next(4, 8);
            for (int i = 0; i < numBarricades; i++)
            {
                int x = random.Next(4, gridWidth - 4);
                int y = random.Next(4, gridHeight - 4);
                int length = random.Next(3, 6);
                bool horizontal = random.Next(2) == 0;

                if (horizontal)
                {
                    for (int j = 0; j < length && x + j < gridWidth - 2; j++)
                        AddHorizontalWall(walls, x + j, y);
                }
                else
                {
                    for (int j = 0; j < length && y + j < gridHeight - 2; j++)
                        AddVerticalWall(walls, x, y + j);
                }
            }

            return walls;
        }

        /// <summary>
        /// Génère des bâtiments urbains ordonnés sur une grille régulière
        /// </summary>
        private HashSet<WallSegment> GenerateUrban(int gridWidth, int gridHeight)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();

            int blockSize = 14;     // Taille d'un bloc
            int streetWidth = 2;    // Rues fines
            int sidewalkWidth = 1;  // Trottoirs en bord de lot
            int startX = 2;
            int endX = gridWidth - 2;

            int startY = 4;
            int endY = gridHeight - 4;

            // Pipeline explicite: routes -> sidewalks -> bâtiments.
            HashSet<Point> roadCells = GenerateUrbanRoadCells(gridWidth, gridHeight, blockSize, streetWidth, startX, endX, startY, endY);
            HashSet<Point> sidewalkCells = GenerateUrbanSidewalkCells(gridWidth, gridHeight, roadCells);

            for (int blockY = startY; blockY < endY; blockY += blockSize + streetWidth)
            {
                for (int blockX = startX; blockX < endX; blockX += blockSize + streetWidth)
                {
                    int lotWidth = Math.Min(blockSize, endX - blockX);
                    int lotHeight = Math.Min(blockSize, endY - blockY);

                    if (lotWidth < 6 || lotHeight < 6)
                        continue;

                    int lotMinX = blockX + sidewalkWidth;
                    int lotMinY = blockY + sidewalkWidth;
                    int lotMaxX = blockX + lotWidth - sidewalkWidth;
                    int lotMaxY = blockY + lotHeight - sidewalkWidth;
                    int innerWidth = lotMaxX - lotMinX;
                    int innerHeight = lotMaxY - lotMinY;

                    if (innerWidth < 6 || innerHeight < 6)
                        continue;

                    int buildingCount = (innerWidth >= 13 && innerHeight >= 8 && random.Next(100) < 40) ? 2 : 1;

                    var lotBuildings = new List<GeneratedBuilding>();

                    for (int index = 0; index < buildingCount; index++)
                    {
                        if (!TryPlaceUrbanBuildingInLot(
                            lotMinX,
                            lotMinY,
                            innerWidth,
                            innerHeight,
                            lotBuildings,
                            roadCells,
                            sidewalkCells,
                            out GeneratedBuilding building))
                            continue;

                        lotBuildings.Add(building);
                        AddUrbanBuilding(walls, building);
                    }
                }
            }

            AddUrbanHescoFortifications(gridWidth, gridHeight);

            return walls;
        }

        private static HashSet<Point> GenerateUrbanRoadCells(
            int gridWidth,
            int gridHeight,
            int blockSize,
            int streetWidth,
            int startX,
            int endX,
            int startY,
            int endY)
        {
            var roads = new HashSet<Point>();
            int stride = blockSize + streetWidth;

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    if ((x - startX) % stride >= blockSize || (y - startY) % stride >= blockSize)
                    {
                        roads.Add(new Point(x, y));
                    }
                }
            }

            return roads;
        }

        private static HashSet<Point> GenerateUrbanSidewalkCells(int gridWidth, int gridHeight, HashSet<Point> roads)
        {
            var sidewalks = new HashSet<Point>();

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

                        Point cell = new Point(x, y);
                        if (roads.Contains(cell))
                            continue;

                        sidewalks.Add(cell);
                    }
                }
            }

            return sidewalks;
        }

        private bool TryPlaceUrbanBuildingInLot(
            int lotMinX,
            int lotMinY,
            int lotWidth,
            int lotHeight,
            List<GeneratedBuilding> existingBuildings,
            HashSet<Point> roads,
            HashSet<Point> sidewalks,
            out GeneratedBuilding result)
        {
            result = default;

            int maxWidth = Math.Min(11, lotWidth);
            int maxHeight = Math.Min(11, lotHeight);
            if (maxWidth < 6 || maxHeight < 6)
                return false;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                int buildingWidth = random.Next(6, maxWidth + 1);
                int buildingHeight = random.Next(6, maxHeight + 1);

                int xMin = lotMinX;
                int yMin = lotMinY;
                int xMax = lotMinX + lotWidth - buildingWidth;
                int yMax = lotMinY + lotHeight - buildingHeight;

                if (xMin > xMax || yMin > yMax)
                    continue;

                // Priorité aux coins pour avoir des façades collées aux trottoirs.
                // Les rectangles restent dans la zone interne du lot (pas sur trottoir/route).
                int anchorCorner = random.Next(100) < 80 ? random.Next(4) : -1;

                int x;
                switch (anchorCorner)
                {
                    case 0:
                    case 2:
                        x = xMin;
                        break;
                    case 1:
                    case 3:
                        x = xMax;
                        break;
                    default:
                        x = random.Next(xMin, xMax + 1);
                        break;
                }

                int y;
                switch (anchorCorner)
                {
                    case 0:
                    case 1:
                        y = yMin;
                        break;
                    case 2:
                    case 3:
                        y = yMax;
                        break;
                    default:
                        y = random.Next(yMin, yMax + 1);
                        break;
                }

                if (!HasMinimumOneCellGap(x, y, buildingWidth, buildingHeight, existingBuildings))
                    continue;
                if (OverlapsRoadOrSidewalk(x, y, buildingWidth, buildingHeight, roads, sidewalks))
                    continue;

                int floors = random.Next(1, 9);
                int footprint = buildingWidth * buildingHeight;
                int basementChance = footprint >= 96 ? 65 : footprint >= 72 ? 45 : 20;
                int basementCount = random.Next(100) < basementChance ? random.Next(1, 3) : 0;

                result = new GeneratedBuilding(x, y, buildingWidth, buildingHeight, floors, basementCount);
                return true;
            }

            return false;
        }

        private static bool OverlapsRoadOrSidewalk(
            int x,
            int y,
            int width,
            int height,
            HashSet<Point> roads,
            HashSet<Point> sidewalks)
        {
            for (int cx = x; cx < x + width; cx++)
            {
                for (int cy = y; cy < y + height; cy++)
                {
                    Point cell = new Point(cx, cy);
                    if (roads.Contains(cell) || sidewalks.Contains(cell))
                        return true;
                }
            }

            return false;
        }

        private bool HasMinimumOneCellGap(int x, int y, int width, int height, List<GeneratedBuilding> existingBuildings)
        {
            foreach (var other in existingBuildings)
            {
                int expandedMinX = other.X - 1;
                int expandedMinY = other.Y - 1;
                int expandedMaxX = other.X + other.Width;
                int expandedMaxY = other.Y + other.Height;

                bool overlapsExpandedX = x <= expandedMaxX && x + width - 1 >= expandedMinX;
                bool overlapsExpandedY = y <= expandedMaxY && y + height - 1 >= expandedMinY;

                if (overlapsExpandedX && overlapsExpandedY)
                    return false;
            }

            return true;
        }

        private void AddUrbanBuilding(HashSet<WallSegment> walls, GeneratedBuilding building)
        {
            int x = building.X;
            int y = building.Y;
            int buildingWidth = building.Width;
            int buildingHeight = building.Height;

            LastGeneratedBuildings.Add(building);

            BuildingType type = (BuildingType)random.Next(0, 4);

            // Murs extérieurs avec fenêtres (40% de chance)
            for (int i = x; i < x + buildingWidth; i++)
            {
                AddHorizontalWall(walls, i, y, WallType.Full, true, 40, WallMaterial.Brick);
                AddHorizontalWall(walls, i, y + buildingHeight, WallType.Full, true, 40, WallMaterial.Brick);
            }

            for (int i = y; i < y + buildingHeight; i++)
            {
                AddVerticalWall(walls, x, i, WallType.Full, true, 40, WallMaterial.Brick);
                AddVerticalWall(walls, x + buildingWidth, i, WallType.Full, true, 40, WallMaterial.Brick);
            }

            // Porte d'entrée
            int doorSide = random.Next(4);
            switch (doorSide)
            {
                case 0: AddHorizontalDoor(walls, x + buildingWidth / 2, y); break;
                case 1: AddHorizontalDoor(walls, x + buildingWidth / 2, y + buildingHeight); break;
                case 2: AddVerticalDoor(walls, x, y + buildingHeight / 2); break;
                case 3: AddVerticalDoor(walls, x + buildingWidth, y + buildingHeight / 2); break;
            }

            GenerateInterior(walls, x, y, buildingWidth, buildingHeight, type);
        }

        private void AddUrbanHescoFortifications(int gridWidth, int gridHeight)
        {
            if (LastGeneratedBuildings.Count < 2)
                return;

            int placedFortifications = 0;
            const int maxFortifications = 10;

            for (int i = 0; i < LastGeneratedBuildings.Count - 1 && placedFortifications < maxFortifications; i++)
            {
                for (int j = i + 1; j < LastGeneratedBuildings.Count && placedFortifications < maxFortifications; j++)
                {
                    if (random.Next(100) >= 45)
                        continue;

                    var a = LastGeneratedBuildings[i];
                    var b = LastGeneratedBuildings[j];

                    var left = a.X <= b.X ? a : b;
                    var right = a.X <= b.X ? b : a;
                    int horizontalGap = right.X - (left.X + left.Width);
                    int overlapYStart = Math.Max(left.Y, right.Y);
                    int overlapYEnd = Math.Min(left.Y + left.Height, right.Y + right.Height);
                    int overlapY = overlapYEnd - overlapYStart;

                    if (horizontalGap >= 2 && horizontalGap <= 5 && overlapY >= 3)
                    {
                        int barrierX = left.X + left.Width + (horizontalGap / 2);
                        int barrierLen = Math.Min(overlapY - 1, random.Next(2, 6));
                        int barrierStartY = overlapYStart + random.Next(0, Math.Max(1, overlapY - barrierLen));

                        if (barrierX > 1 && barrierX < gridWidth - 1)
                        {
                            for (int y = barrierStartY; y < barrierStartY + barrierLen && y < gridHeight - 2; y++)
                                LastGeneratedHescoBarriers.Add(new Point(barrierX, y));

                            placedFortifications++;
                            continue;
                        }
                    }

                    var top = a.Y <= b.Y ? a : b;
                    var bottom = a.Y <= b.Y ? b : a;
                    int verticalGap = bottom.Y - (top.Y + top.Height);
                    int overlapXStart = Math.Max(top.X, bottom.X);
                    int overlapXEnd = Math.Min(top.X + top.Width, bottom.X + bottom.Width);
                    int overlapX = overlapXEnd - overlapXStart;

                    if (verticalGap >= 2 && verticalGap <= 5 && overlapX >= 3)
                    {
                        int barrierY = top.Y + top.Height + (verticalGap / 2);
                        int barrierLen = Math.Min(overlapX - 1, random.Next(2, 6));
                        int barrierStartX = overlapXStart + random.Next(0, Math.Max(1, overlapX - barrierLen));

                        if (barrierY > 1 && barrierY < gridHeight - 1)
                        {
                            for (int x = barrierStartX; x < barrierStartX + barrierLen && x < gridWidth - 2; x++)
                                LastGeneratedHescoBarriers.Add(new Point(x, barrierY));

                            placedFortifications++;
                        }
                    }
                }
            }

            if (LastGeneratedHescoBarriers.Count > 1)
            {
                var uniqueBarriers = new HashSet<Point>(LastGeneratedHescoBarriers);
                LastGeneratedHescoBarriers.Clear();
                LastGeneratedHescoBarriers.AddRange(uniqueBarriers);
            }
        }

        /// <summary>
        /// Génère des tranchées
        /// </summary>
        private HashSet<WallSegment> GenerateTrenches(int gridWidth, int gridHeight)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();
            // Les tranchées sont maintenant matérialisées par le relief du terrain.
            // On ne place donc plus de murs structurels sur ce pattern.
            return walls;
        }

        /// <summary>
        /// Génère une forêt avec quelques cabanes éparpillées
        /// </summary>
        private HashSet<WallSegment> GenerateForest(int gridWidth, int gridHeight)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();

            // Générer 1 à 3 petites cabanes
            int numCabins = random.Next(1, 4);
            for (int i = 0; i < numCabins; i++)
            {
                int cabinWidth = random.Next(4, 7);
                int cabinHeight = random.Next(4, 7);

                int startX = random.Next(5, gridWidth - cabinWidth - 5);
                int startY = random.Next(5, gridHeight - cabinHeight - 5);

                // Vérifier qu'on n'est pas dans les zones de spawn
                if (startY < 6 || startY > gridHeight - cabinHeight - 6)
                    continue;

                LastGeneratedBuildings.Add(new GeneratedBuilding(startX, startY, cabinWidth, cabinHeight, 1));

                // Murs extérieurs
                for (int x = startX; x < startX + cabinWidth; x++)
                {
                    AddHorizontalWall(walls, x, startY, WallType.Full, true, 20, WallMaterial.Standard);
                    AddHorizontalWall(walls, x, startY + cabinHeight, WallType.Full, true, 20, WallMaterial.Standard);
                }
                for (int y = startY; y < startY + cabinHeight; y++)
                {
                    AddVerticalWall(walls, startX, y, WallType.Full, true, 20, WallMaterial.Standard);
                    AddVerticalWall(walls, startX + cabinWidth, y, WallType.Full, true, 20, WallMaterial.Standard);
                }

                // Une porte
                int side = random.Next(4);
                switch (side)
                {
                    case 0: AddHorizontalDoor(walls, startX + cabinWidth / 2, startY); break;
                    case 1: AddHorizontalDoor(walls, startX + cabinWidth / 2, startY + cabinHeight); break;
                    case 2: AddVerticalDoor(walls, startX, startY + cabinHeight / 2); break;
                    case 3: AddVerticalDoor(walls, startX + cabinWidth, startY + cabinHeight / 2); break;
                }
            }

            return walls;
        }

        /// <summary>
        /// Nettoie les zones de spawn
        /// </summary>
        public void ClearSpawnZones(HashSet<WallSegment> walls, int gridWidth, int gridHeight)
        {
            bool IsPerimeterWall(WallSegment wall)
            {
                bool onTopEdge = wall.IsHorizontal && wall.Start.Y == 0;
                bool onBottomEdge = wall.IsHorizontal && wall.Start.Y == gridHeight;
                bool onLeftEdge = !wall.IsHorizontal && wall.Start.X == 0;
                bool onRightEdge = !wall.IsHorizontal && wall.Start.X == gridWidth;
                return onTopEdge || onBottomEdge || onLeftEdge || onRightEdge;
            }

            // Zone joueur (bas)
            walls.RemoveWhere(w => !IsPerimeterWall(w) && (w.Start.Y > gridHeight - 4 || w.End.Y > gridHeight - 4));

            // Zone ennemie (haut)
            walls.RemoveWhere(w => !IsPerimeterWall(w) && (w.Start.Y < 4 || w.End.Y < 4));
        }

        /// <summary>
        /// Génère l'intérieur d'un bâtiment selon son type
        /// </summary>
        private void GenerateInterior(HashSet<WallSegment> walls, int x, int y, int width, int height, BuildingType type)
        {
            switch (type)
            {
                case BuildingType.SmallHouse:
                    GenerateModernHouseInterior(walls, x, y, width, height);
                    break;
                case BuildingType.Apartment:
                    GenerateApartmentInterior(walls, x, y, width, height);
                    break;
                case BuildingType.Office:
                    GenerateOfficeInterior(walls, x, y, width, height);
                    break;
                case BuildingType.Warehouse:
                    GenerateWarehouseInterior(walls, x, y, width, height);
                    break;
            }
        }

        /// <summary>
        /// Génère un intérieur de maison moderne avec séparation Jour (Open Space) / Nuit (Chambres)
        /// </summary>
        private void GenerateModernHouseInterior(HashSet<WallSegment> walls, int x, int y, int width, int height)
        {
            if (width < 6 || height < 6) return;

            // Séparation Nuit / Jour (45% gauche = chambres, 55% droite = living)
            int splitX = x + (int)(width * 0.45f);

            // Mur central avec double porte
            int doorY = y + height / 2;
            for (int i = y + 1; i < y + height - 1; i++)
            {
                if (i != doorY && i != doorY + 1)
                {
                    AddVerticalWall(walls, splitX, i);
                }
            }

            // ZONE NUIT (Gauche) - Deux chambres
            int roomSplitY = y + height / 2;
            for (int i = x + 1; i < splitX; i++)
            {
                AddHorizontalWall(walls, i, roomSplitY);
            }

            // Portes des chambres vers le couloir central
            AddVerticalDoor(walls, splitX, y + height / 4);
            AddVerticalDoor(walls, splitX, y + 3 * height / 4);

            // ZONE JOUR (Droite) - Open space + salle de bain
            int rightWidth = (x + width) - splitX;
            int bathWidth = Math.Max(2, rightWidth / 3);
            int bathHeight = Math.Max(2, height / 3);

            int bathStartX = (x + width) - bathWidth;
            int bathBottomY = y + bathHeight;

            // Murs de la salle de bain
            for (int i = bathStartX + 1; i < x + width; i++)
            {
                AddHorizontalWall(walls, i, bathBottomY);
            }
            for (int i = y + 1; i < bathBottomY; i++)
            {
                AddVerticalWall(walls, bathStartX, i);
            }
            
            // Porte de la salle de bain
            AddHorizontalDoor(walls, bathStartX + 1, bathBottomY);
        }

        /// <summary>
        /// Génère l'intérieur d'un appartement avec couloir central et pièces latérales
        /// </summary>
        private void GenerateApartmentInterior(HashSet<WallSegment> walls, int x, int y, int width, int height)
        {
            if (width < 8 || height < 6) return;

            // Couloir central horizontal (1/3 de la hauteur, au centre)
            int corridorY = y + height / 3;
            int corridorHeight = Math.Max(2, height / 3);

            // Pièces au-dessus du couloir
            int numRoomsTop = random.Next(2, 4);
            int roomWidthTop = (width - 2) / numRoomsTop;

            for (int i = 0; i < numRoomsTop - 1; i++)
            {
                int wallX = x + 1 + (i + 1) * roomWidthTop;
                for (int yy = y + 1; yy < corridorY; yy++)
                {
                    AddVerticalWall(walls, wallX, yy);
                }
                // Porte vers le couloir
                AddHorizontalDoor(walls, wallX - roomWidthTop / 2, corridorY);
            }

            // Pièces en dessous du couloir
            int numRoomsBottom = random.Next(2, 4);
            int roomWidthBottom = (width - 2) / numRoomsBottom;

            for (int i = 0; i < numRoomsBottom - 1; i++)
            {
                int wallX = x + 1 + (i + 1) * roomWidthBottom;
                for (int yy = corridorY + corridorHeight; yy < y + height - 1; yy++)
                {
                    AddVerticalWall(walls, wallX, yy);
                }
                // Porte vers le couloir
                AddHorizontalDoor(walls, wallX - roomWidthBottom / 2, corridorY + corridorHeight);
            }
        }

        /// <summary>
        /// Génère l'intérieur d'un bureau avec cubicules
        /// </summary>
        private void GenerateOfficeInterior(HashSet<WallSegment> walls, int x, int y, int width, int height)
        {
            if (width < 8 || height < 6) return;

            // Couloir principal vertical sur le côté gauche
            int corridorX = x + width / 4;

            for (int yy = y + 1; yy < y + height - 1; yy++)
            {
                AddVerticalWall(walls, corridorX, yy);
            }

            // Cubicules à droite du couloir (grille 2x2 ou 2x3)
            int cubicleCols = 2;
            int cubicleRows = random.Next(2, 4);

            int cubicleWidth = (width - (corridorX - x) - 1) / cubicleCols;
            int cubicleHeight = (height - 2) / cubicleRows;

            for (int row = 0; row < cubicleRows; row++)
            {
                for (int col = 0; col < cubicleCols; col++)
                {
                    int cubX = corridorX + 1 + col * cubicleWidth;
                    int cubY = y + 1 + row * cubicleHeight;

                    // Murs de cubicule (partiels, hauteur 1)
                    if (col > 0)
                    {
                        AddVerticalWall(walls, cubX, cubY, WallType.Full, false, 0);
                    }
                    if (row > 0)
                    {
                        for (int i = 0; i < cubicleWidth - 1; i++)
                        {
                            AddHorizontalWall(walls, cubX + i, cubY, WallType.Full, false, 0);
                        }
                    }
                }
            }

            // Quelques portes vers le couloir
            for (int row = 0; row < cubicleRows; row++)
            {
                int doorY = y + 1 + row * cubicleHeight + cubicleHeight / 2;
                AddVerticalDoor(walls, corridorX, doorY);
            }
        }

        /// <summary>
        /// Génère le pattern "The Hive" : un bunker souterrain massif
        /// </summary>
        private HashSet<WallSegment> GenerateTheHive(int gridWidth, int gridHeight)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();

            // Un seul bâtiment massif qui occupe le centre
            int margin = 6;
            int bWidth = gridWidth - margin * 2;
            int bHeight = gridHeight - margin * 2;
            int bX = margin;
            int bY = margin;

            if (bWidth < 10 || bHeight < 10)
                return GenerateScattered(gridWidth, gridHeight, 20);

            // 1 étage en surface, 8 en sous-sol
            GeneratedBuilding building = new GeneratedBuilding(bX, bY, bWidth, bHeight, 1, 8);
            LastGeneratedBuildings.Add(building);

            // Murs extérieurs du bâtiment
            for (int i = bX; i < bX + bWidth; i++)
            {
                AddHorizontalWall(walls, i, bY, WallType.Full, false, 0, WallMaterial.Brick);
                AddHorizontalWall(walls, i, bY + bHeight, WallType.Full, false, 0, WallMaterial.Brick);
            }
            for (int i = bY; i < bY + bHeight; i++)
            {
                AddVerticalWall(walls, bX, i, WallType.Full, false, 0, WallMaterial.Brick);
                AddVerticalWall(walls, bX + bWidth, i, WallType.Full, false, 0, WallMaterial.Brick);
            }

            // Entrée principale (Porte) au sud
            AddHorizontalDoor(walls, bX + bWidth / 2, bY + bHeight);

            // Structure interne : Couloir central avec des labos de chaque côté
            int centerX = bX + bWidth / 2;
            int corridorHalfWidth = 1; // Couloir de 3 cases de large (centerX-1, centerX, centerX+1)

            // Couloir vertical central
            for (int y = bY + 1; y < bY + bHeight - 1; y++)
            {
                AddVerticalWall(walls, centerX - corridorHalfWidth, y);
                AddVerticalWall(walls, centerX + corridorHalfWidth + 1, y);
            }

            // Pièces latérales (Laboratoires)
            int roomHeight = 6;
            for (int y = bY + 2; y < bY + bHeight - roomHeight; y += roomHeight + 2)
            {
                // Murs horizontaux pour séparer les pièces
                for (int x = bX + 1; x < centerX - corridorHalfWidth; x++)
                    AddHorizontalWall(walls, x, y);
                for (int x = centerX + corridorHalfWidth + 1; x < bX + bWidth; x++)
                    AddHorizontalWall(walls, x, y);

                // Portes vers le couloir
                AddVerticalDoor(walls, centerX - corridorHalfWidth, y + roomHeight / 2);
                AddVerticalDoor(walls, centerX + corridorHalfWidth + 1, y + roomHeight / 2);
            }

            // Hub central (Reine Rouge) au fond (nord)
            int hubY = bY + 4;
            for (int x = centerX - 4; x <= centerX + 4; x++)
            {
                if (x < centerX - corridorHalfWidth || x > centerX + corridorHalfWidth)
                    AddHorizontalWall(walls, x, hubY);
            }

            return walls;
        }

        /// <summary>
        /// Génère l'intérieur d'un entrepôt (très ouvert avec quelques rayonnages)
        /// </summary>
        private void GenerateWarehouseInterior(HashSet<WallSegment> walls, int x, int y, int width, int height)
        {
            if (width < 8 || height < 8) return;

            // Quelques rangées de rayonnages (murs courts parallèles)
            int numRows = random.Next(2, 4);
            int rowSpacing = (height - 4) / (numRows + 1);

            for (int row = 0; row < numRows; row++)
            {
                int rowY = y + 2 + (row + 1) * rowSpacing;
                int shelfLength = random.Next(3, 6);

                // Plusieurs segments de rayonnage sur cette ligne
                int numShelves = random.Next(2, 4);
                for (int shelf = 0; shelf < numShelves; shelf++)
                {
                    int startX = x + 2 + shelf * (width / numShelves);
                    for (int i = 0; i < shelfLength && startX + i < x + width - 2; i++)
                    {
                        AddHorizontalWall(walls, startX + i, rowY);
                    }
                }
            }

            // Petit bureau dans un coin (10% de la surface)
            int officeWidth = Math.Max(3, width / 5);
            int officeHeight = Math.Max(3, height / 5);

            int officeX = x + width - officeWidth - 1;
            int officeY = y + 1;

            // Murs du bureau
            for (int i = officeX; i < officeX + officeWidth; i++)
            {
                AddHorizontalWall(walls, i, officeY);
                AddHorizontalWall(walls, i, officeY + officeHeight);
            }
            for (int i = officeY; i < officeY + officeHeight; i++)
            {
                AddVerticalWall(walls, officeX, i);
            }

            // Porte du bureau
            AddVerticalDoor(walls, officeX, officeY + officeHeight / 2);
        }
    }
}
