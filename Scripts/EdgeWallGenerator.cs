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
    public enum WallType { Full, Window, Door }
    public enum WallMaterial { Standard, Brick }

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

        public enum WallPattern
        {
            Rooms,          // Pièces avec couloirs
            Maze,           // Labyrinthe
            Scattered,      // Murs éparpillés
            Bunker,         // Structure défensive
            Urban,          // Bâtiments urbains
            Trenches        // Tranchées de guerre
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

            int startY = 4;
            int endY = gridHeight - 4;

            for (int blockY = startY; blockY < endY; blockY += blockSize + streetWidth)
            {
                for (int blockX = 2; blockX < gridWidth - 2; blockX += blockSize + streetWidth)
                {
                    int lotWidth = Math.Min(blockSize, gridWidth - blockX - 2);
                    int lotHeight = Math.Min(blockSize, gridHeight - blockY - 2);

                    if (lotWidth < 6 || lotHeight < 6)
                        continue;

                    // Décalage aléatoire dans le lot pour casser la régularité
                    int maxOffsetX = Math.Max(0, lotWidth - 6);
                    int maxOffsetY = Math.Max(0, lotHeight - 6);

                    int offsetX = random.Next(0, maxOffsetX / 2 + 1);
                    int offsetY = random.Next(0, maxOffsetY / 2 + 1);

                    // Taille réelle du bâtiment
                    int buildingWidth = random.Next(6, lotWidth - offsetX + 1);
                    int buildingHeight = random.Next(6, lotHeight - offsetY + 1);

                    // Position finale du bâtiment
                    int x = blockX + offsetX;
                    int y = blockY + offsetY;

                    BuildingType type = (BuildingType)random.Next(0, 4);

                    // Règle métier: 1 étage = 2 cases de haut.
                    // On convertit donc la hauteur (en cases) du bâtiment en nombre d'étages.
                    int maxFloorsFromHeight = Math.Max(1, buildingHeight / 2);
                    int minFloors = Math.Min(2, maxFloorsFromHeight);
                    int buildingFloors = random.Next(minFloors, maxFloorsFromHeight + 1);

                    // Sous-sol plus fréquent sur les grands immeubles urbains
                    int footprint = buildingWidth * buildingHeight;
                    int basementChance = footprint >= 96 ? 65 : footprint >= 72 ? 45 : 20;
                    int basementCount = random.Next(100) < basementChance ? random.Next(1, 3) : 0;
                    LastGeneratedBuildings.Add(new GeneratedBuilding(x, y, buildingWidth, buildingHeight, buildingFloors, basementCount));

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
            }

            return walls;
        }

        /// <summary>
        /// Génère des tranchées
        /// </summary>
        private HashSet<WallSegment> GenerateTrenches(int gridWidth, int gridHeight)
        {
            HashSet<WallSegment> walls = new HashSet<WallSegment>();

            // Tranchée principale horizontale avec zigzag
            int currentY = gridHeight / 2;

            for (int x = 3; x < gridWidth - 3; x++)
            {
                AddHorizontalWall(walls, x, currentY);
                AddHorizontalWall(walls, x, currentY + 1);

                if (random.Next(100) < 20)
                    currentY += random.Next(-1, 2);

                currentY = Math.Max(4, Math.Min(gridHeight - 5, currentY));
            }

            // Tranchées perpendiculaires
            int numCross = random.Next(2, 5);
            for (int i = 0; i < numCross; i++)
            {
                int crossX = random.Next(5, gridWidth - 5);
                int length = random.Next(4, 8);
                int startY = random.Next(4, Math.Max(5, gridHeight - length - 4));

                for (int y = startY; y < startY + length && y < gridHeight - 3; y++)
                {
                    AddVerticalWall(walls, crossX, y);
                }
            }

            // Sacs de sable (petits segments)
            for (int i = 0; i < 15; i++)
            {
                int x = random.Next(3, gridWidth - 3);
                int y = random.Next(3, gridHeight - 3);
                int length = random.Next(2, 4);

                if (random.Next(2) == 0)
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
