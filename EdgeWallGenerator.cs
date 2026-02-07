using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Représente un segment de mur entre deux cases
    /// </summary>
    public struct WallSegment
    {
        public Point Start;      // Case de départ
        public Point End;        // Case d'arrivée
        public bool IsHorizontal; // true = horizontal, false = vertical

        public WallSegment(Point start, Point end, bool isHorizontal)
        {
            Start = start;
            End = end;
            IsHorizontal = isHorizontal;
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
        /// Ajoute un mur horizontal entre (x,y) et (x+1,y)
        /// </summary>
        private void AddHorizontalWall(HashSet<WallSegment> walls, int x, int y)
        {
            walls.Add(new WallSegment(new Point(x, y), new Point(x + 1, y), true));
        }

        /// <summary>
        /// Ajoute un mur vertical entre (x,y) et (x,y+1)
        /// </summary>
        private void AddVerticalWall(HashSet<WallSegment> walls, int x, int y)
        {
            walls.Add(new WallSegment(new Point(x, y), new Point(x, y + 1), false));
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

                // Murs horizontaux (nord et sud)
                for (int x = startX; x < startX + roomWidth; x++)
                {
                    AddHorizontalWall(walls, x, startY);                    // Mur nord
                    AddHorizontalWall(walls, x, startY + roomHeight);       // Mur sud
                }

                // Murs verticaux (ouest et est)
                for (int y = startY; y < startY + roomHeight; y++)
                {
                    AddVerticalWall(walls, startX, y);                      // Mur ouest
                    AddVerticalWall(walls, startX + roomWidth, y);          // Mur est
                }

                // Ajouter 1-3 portes (retirer des segments)
                int numDoors = random.Next(1, 4);
                for (int d = 0; d < numDoors; d++)
                {
                    int side = random.Next(4);

                    switch (side)
                    {
                        case 0: // Nord - retirer un segment horizontal
                            if (roomWidth > 2)
                            {
                                int doorX = startX + random.Next(1, roomWidth - 1);
                                walls.Remove(new WallSegment(new Point(doorX, startY), new Point(doorX + 1, startY), true));
                            }
                            break;
                        case 1: // Sud
                            if (roomWidth > 2)
                            {
                                int doorX = startX + random.Next(1, roomWidth - 1);
                                walls.Remove(new WallSegment(new Point(doorX, startY + roomHeight), new Point(doorX + 1, startY + roomHeight), true));
                            }
                            break;
                        case 2: // Ouest - retirer un segment vertical
                            if (roomHeight > 2)
                            {
                                int doorY = startY + random.Next(1, roomHeight - 1);
                                walls.Remove(new WallSegment(new Point(startX, doorY), new Point(startX, doorY + 1), false));
                            }
                            break;
                        case 3: // Est
                            if (roomHeight > 2)
                            {
                                int doorY = startY + random.Next(1, roomHeight - 1);
                                walls.Remove(new WallSegment(new Point(startX + roomWidth, doorY), new Point(startX + roomWidth, doorY + 1), false));
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

            // Taille standard des bâtiments et espacement
            int buildingWidth = 6;
            int buildingHeight = 6;
            int streetWidth = 3; // Largeur des rues entre les bâtiments

            // Calculer combien de bâtiments peuvent tenir
            int blocWidth = buildingWidth + streetWidth;
            int blocHeight = buildingHeight + streetWidth;

            int numBuildingsX = Math.Max(1, (gridWidth - 4) / blocWidth);
            int numBuildingsY = Math.Max(1, (gridHeight - 8) / blocHeight); // -8 pour zones de spawn

            // Centrer la grille
            int startX = (gridWidth - (numBuildingsX * blocWidth - streetWidth)) / 2;
            int startY = 4 + (gridHeight - 8 - (numBuildingsY * blocHeight - streetWidth)) / 2;

            // Créer les bâtiments sur la grille
            for (int by = 0; by < numBuildingsY; by++)
            {
                for (int bx = 0; bx < numBuildingsX; bx++)
                {
                    int x = startX + bx * blocWidth;
                    int y = startY + by * blocHeight;

                    // Murs extérieurs du bâtiment
                    for (int i = x; i < x + buildingWidth; i++)
                    {
                        AddHorizontalWall(walls, i, y);
                        AddHorizontalWall(walls, i, y + buildingHeight);
                    }

                    for (int i = y; i < y + buildingHeight; i++)
                    {
                        AddVerticalWall(walls, x, i);
                        AddVerticalWall(walls, x + buildingWidth, i);
                    }

                    // Porte orientée vers la rue la plus proche
                    bool hasRightStreet = bx < numBuildingsX - 1;
                    bool hasBottomStreet = by < numBuildingsY - 1;

                    if (hasRightStreet && hasBottomStreet)
                    {
                        // Deux portes : une à droite, une en bas
                        walls.Remove(new WallSegment(new Point(x + buildingWidth, y + buildingHeight / 2),
                                                     new Point(x + buildingWidth, y + buildingHeight / 2 + 1), false));
                        walls.Remove(new WallSegment(new Point(x + buildingWidth / 2, y + buildingHeight),
                                                     new Point(x + buildingWidth / 2 + 1, y + buildingHeight), true));
                    }
                    else if (hasRightStreet)
                    {
                        // Porte à droite
                        walls.Remove(new WallSegment(new Point(x + buildingWidth, y + buildingHeight / 2),
                                                     new Point(x + buildingWidth, y + buildingHeight / 2 + 1), false));
                    }
                    else if (hasBottomStreet)
                    {
                        // Porte en bas
                        walls.Remove(new WallSegment(new Point(x + buildingWidth / 2, y + buildingHeight),
                                                     new Point(x + buildingWidth / 2 + 1, y + buildingHeight), true));
                    }
                    else
                    {
                        // Dernier bâtiment (coin bas-droit), porte au sud ou à l'est
                        if (random.Next(2) == 0)
                            walls.Remove(new WallSegment(new Point(x + buildingWidth / 2, y + buildingHeight),
                                                         new Point(x + buildingWidth / 2 + 1, y + buildingHeight), true));
                        else
                            walls.Remove(new WallSegment(new Point(x + buildingWidth, y + buildingHeight / 2),
                                                         new Point(x + buildingWidth, y + buildingHeight / 2 + 1), false));
                    }

                    // Murs intérieurs : division en 4 pièces égales
                    int midX = x + buildingWidth / 2;
                    int midY = y + buildingHeight / 2;

                    // Couloir vertical central avec portes
                    for (int i = y + 1; i < y + buildingHeight - 1; i++)
                    {
                        if (i != midY - 1 && i != midY) // Portes au milieu
                            AddVerticalWall(walls, midX, i);
                    }

                    // Couloir horizontal central avec portes
                    for (int i = x + 1; i < x + buildingWidth - 1; i++)
                    {
                        if (i != midX - 1 && i != midX) // Portes au milieu
                            AddHorizontalWall(walls, i, midY);
                    }
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
            // Zone joueur (bas)
            walls.RemoveWhere(w => w.Start.Y > gridHeight - 4 || w.End.Y > gridHeight - 4);

            // Zone ennemie (haut)
            walls.RemoveWhere(w => w.Start.Y < 4 || w.End.Y < 4);
        }
    }
}