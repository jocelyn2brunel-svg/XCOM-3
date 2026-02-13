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

    /// <summary>
    /// Représente un segment de mur entre deux cases
    /// </summary>
    public enum WallType { Full, Window, Door }

    public struct WallSegment
    {
        public Point Start;
        public Point End;
        public bool IsHorizontal;
        public WallType Type; // NOUVEAU

        // On ajoute le paramètre optionnel "type"
        public WallSegment(Point start, Point end, bool isHorizontal, WallType type = WallType.Full)
        {
            Start = start;
            End = end;
            IsHorizontal = isHorizontal;
            Type = type;
        }

        // Garde tes méthodes Equals et GetHashCode telles quelles ! 
        // On veut toujours identifier un mur par sa position, peu importe s'il se transforme en fenêtre.
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
        public void AddHorizontalWall(HashSet<WallSegment> walls, int x, int y)
        {
            // 20% de chances d'être une fenêtre sur les murs extérieurs par exemple
            WallType type = (random.Next(100) < 20) ? WallType.Window : WallType.Full;
            walls.Add(new WallSegment(new Point(x, y), new Point(x + 1, y), true, type));
        }

        /// <summary>
        /// Ajoute un mur vertical entre (x,y) et (x,y+1)
        /// </summary>
        private void AddVerticalWall(HashSet<WallSegment> walls, int x, int y)
        {
            WallType type = (random.Next(100) < 20) ? WallType.Window : WallType.Full;
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

            int blockSize = 14;     // Taille d’un bloc
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

                    int offsetX = random.Next(0, maxOffsetX / 2 + 1); // décalage max moitié du lot
                    int offsetY = random.Next(0, maxOffsetY / 2 + 1);

                    // Taille réelle du bâtiment
                    int buildingWidth = random.Next(6, lotWidth - offsetX + 1);
                    int buildingHeight = random.Next(6, lotHeight - offsetY + 1);

                    // Position finale du bâtiment
                    int x = blockX + offsetX;
                    int y = blockY + offsetY;

                    BuildingType type = (BuildingType)random.Next(0, 4);

                    // Murs extérieurs
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

                    // Porte
                    int doorSide = random.Next(4);
                    switch (doorSide)
                    {
                        case 0: walls.Remove(new WallSegment(new Point(x + buildingWidth / 2, y), new Point(x + buildingWidth / 2 + 1, y), true)); break;
                        case 1: walls.Remove(new WallSegment(new Point(x + buildingWidth / 2, y + buildingHeight), new Point(x + buildingWidth / 2 + 1, y + buildingHeight), true)); break;
                        case 2: walls.Remove(new WallSegment(new Point(x, y + buildingHeight / 2), new Point(x, y + buildingHeight / 2 + 1), false)); break;
                        case 3: walls.Remove(new WallSegment(new Point(x + buildingWidth, y + buildingHeight / 2), new Point(x + buildingWidth, y + buildingHeight / 2 + 1), false)); break;
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
            // Zone joueur (bas)
            walls.RemoveWhere(w => w.Start.Y > gridHeight - 4 || w.End.Y > gridHeight - 4);

            // Zone ennemie (haut)
            walls.RemoveWhere(w => w.Start.Y < 4 || w.End.Y < 4);
        }

        private void GenerateInterior(HashSet<WallSegment> walls,
                              int x, int y,
                              int width, int height,
                              BuildingType type)
        {
            // --- NOUVEAUTÉ ICI ---
            // Si c'est une petite maison, on utilise notre nouveau gabarit architectural
            if (type == BuildingType.SmallHouse)
            {
                GenerateModernHouseInterior(walls, x, y, width, height);
                return; // Très important : on quitte la méthode ici pour ne pas exécuter la découpe aléatoire en dessous !
            }
            // ---------------------

            // L'ancien système aléatoire (pour les appartements, bureaux et entrepôts)
            int roomCount;

            switch (type)
            {
                case BuildingType.Apartment:
                    roomCount = random.Next(4, 7);
                    break;
                case BuildingType.Office:
                    roomCount = random.Next(3, 6);
                    break;
                case BuildingType.Warehouse:
                    roomCount = random.Next(1, 3);
                    break;
                default:
                    roomCount = 3;
                    break;
            }

            for (int r = 0; r < roomCount; r++)
            {
                bool verticalSplit = random.Next(2) == 0;

                if (verticalSplit)
                {
                    int splitX = random.Next(x + 2, x + width - 2);

                    for (int i = y + 1; i < y + height - 1; i++)
                    {
                        if (i != y + height / 2) // Porte centrale
                            AddVerticalWall(walls, splitX, i);
                    }
                }
                else
                {
                    int splitY = random.Next(y + 2, y + height - 2);

                    for (int i = x + 1; i < x + width - 1; i++)
                    {
                        if (i != x + width / 2) // Porte centrale
                            AddHorizontalWall(walls, i, splitY);
                    }
                }
            }
        }

        /// <summary>
        /// Génère un intérieur de maison moderne avec séparation Jour (Open Space) / Nuit (Chambres)
        /// </summary>
        private void GenerateModernHouseInterior(HashSet<WallSegment> walls, int x, int y, int width, int height)
        {
            // Sécurité : si le bâtiment est trop petit, on ne fait pas de divisions complexes
            if (width < 6 || height < 6) return;

            // Étape 1 : Le Zonage principal (Séparation Nuit / Jour)
            // On coupe le bâtiment verticalement à environ 45% de sa largeur
            int splitX = x + (int)(width * 0.45f);

            // Mur central (avec une grande ouverture au milieu pour le couloir)
            int doorY = y + height / 2;
            for (int i = y + 1; i < y + height - 1; i++)
            {
                if (i != doorY && i != doorY + 1) // On laisse une double porte pour circuler
                {
                    AddVerticalWall(walls, splitX, i);
                }
            }

            // Étape 2 : Zone Nuit (Côté Gauche) - Très compartimentée
            // On coupe la zone gauche en 2 chambres horizontalement
            int roomSplitY = y + height / 2;
            for (int i = x + 1; i < splitX; i++)
            {
                AddHorizontalWall(walls, i, roomSplitY);
            }

            // Portes pour les chambres (donnant sur le centre)
            walls.Remove(new WallSegment(new Point(splitX, y + height / 4), new Point(splitX, y + height / 4 + 1), false));
            walls.Remove(new WallSegment(new Point(splitX, y + 3 * height / 4), new Point(splitX, y + 3 * height / 4 + 1), false));

            // Étape 3 : Zone Jour (Côté Droit) - Open Space + Bloc utilitaire
            int rightWidth = (x + width) - splitX;

            // On crée juste un petit bloc (Salle de bain / Buanderie) dans le coin en haut à droite
            int bathWidth = Math.Max(2, rightWidth / 3);
            int bathHeight = Math.Max(2, height / 3);

            int bathStartX = (x + width) - bathWidth;
            int bathBottomY = y + bathHeight;

            // Mur sud de la salle de bain
            for (int i = bathStartX; i < x + width; i++)
            {
                // On laisse une porte
                if (i != bathStartX + 1) AddHorizontalWall(walls, i, bathBottomY);
            }
            // Mur ouest de la salle de bain
            for (int i = y; i < bathBottomY; i++)
            {
                AddVerticalWall(walls, bathStartX, i);
            }
            // Le reste du côté droit reste totalement ouvert (Salon, Salle à manger, Cuisine) !
        }

    }
}