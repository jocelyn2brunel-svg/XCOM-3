using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Système d'inventaire style Diablo avec grille et rotation d'items
    /// VERSION AMÉLIORÉE - Support du drag libre hors grille
    /// </summary>

    // ═══════════════════════════════════════════════════════════════════════
    // TAILLE DES ITEMS (en cases de grille)
    // ═══════════════════════════════════════════════════════════════════════
    public struct ItemSize
    {
        public int Width;   // Largeur en cases
        public int Height;  // Hauteur en cases

        public ItemSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Retourne la taille après rotation
        /// </summary>
        public ItemSize Rotated()
        {
            return new ItemSize(Height, Width);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLASSE ITEM AMÉLIORÉE avec rotation ET drag libre
    // ═══════════════════════════════════════════════════════════════════════
    public class GridItem
    {
        public ItemData Data;
        public Point GridPosition;  // Position dans la grille (en cases, pas en pixels)
        public ItemSize Size;       // Taille en cases de grille
        public bool IsRotated;      // Est-ce que l'item est tourné de 90°?

        // Position en pixels (calculée à partir de GridPosition)
        public Rectangle PixelBounds { get; set; } // ✅ CHANGÉ en set public

        private const int CELL_SIZE = 40; // Taille d'une case de grille en pixels

        public GridItem(ItemData data, Point gridPos, ItemSize size, bool rotated = false)
        {
            Data = data;
            GridPosition = gridPos;
            Size = size;
            IsRotated = rotated;
            UpdatePixelBounds();
        }

        /// <summary>
        /// Met à jour les bounds en pixels à partir de la position grille
        /// </summary>
        public void UpdatePixelBounds(int gridOffsetX = 0, int gridOffsetY = 0)
        {
            ItemSize currentSize = IsRotated ? Size.Rotated() : Size;

            PixelBounds = new Rectangle(
                gridOffsetX + GridPosition.X * CELL_SIZE,
                gridOffsetY + GridPosition.Y * CELL_SIZE,
                currentSize.Width * CELL_SIZE,
                currentSize.Height * CELL_SIZE
            );
        }

        /// <summary>
        /// ✅ NOUVEAU: Met à jour les bounds en pixels directement (pour le drag libre)
        /// </summary>
        public void UpdatePixelBoundsAbsolute(int pixelX, int pixelY)
        {
            ItemSize currentSize = IsRotated ? Size.Rotated() : Size;

            PixelBounds = new Rectangle(
                pixelX,
                pixelY,
                currentSize.Width * CELL_SIZE,
                currentSize.Height * CELL_SIZE
            );
        }

        /// <summary>
        /// Tourne l'item de 90°
        /// </summary>
        public void Rotate()
        {
            IsRotated = !IsRotated;
            UpdatePixelBounds();
        }

        /// <summary>
        /// Obtient la taille actuelle (avec rotation)
        /// </summary>
        public ItemSize GetCurrentSize()
        {
            return IsRotated ? Size.Rotated() : Size;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GRILLE D'INVENTAIRE style Diablo
    // ═══════════════════════════════════════════════════════════════════════
    public class InventoryGrid
    {
        public int Width;   // Largeur en cases
        public int Height;  // Hauteur en cases
        private bool[,] occupiedCells;
        private List<GridItem> items;

        private const int CELL_SIZE = 40;

        public InventoryGrid(int width, int height)
        {
            Width = width;
            Height = height;
            occupiedCells = new bool[width, height];
            items = new List<GridItem>();
        }

        /// <summary>
        /// Vérifie si on peut placer un item à une position donnée
        /// </summary>
        public bool CanPlaceItem(Point gridPos, ItemSize size, GridItem ignoreItem = null)
        {
            // Vérifier les limites
            if (gridPos.X < 0 || gridPos.Y < 0 ||
                gridPos.X + size.Width > Width ||
                gridPos.Y + size.Height > Height)
                return false;

            // Vérifier chaque case
            for (int x = 0; x < size.Width; x++)
            {
                for (int y = 0; y < size.Height; y++)
                {
                    if (occupiedCells[gridPos.X + x, gridPos.Y + y])
                    {
                        // Vérifier si c'est l'item qu'on ignore (drag en cours)
                        if (ignoreItem != null)
                        {
                            GridItem itemAtPos = GetItemAt(new Point(gridPos.X + x, gridPos.Y + y));
                            if (itemAtPos == ignoreItem)
                                continue;
                        }
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Place un item dans la grille
        /// </summary>
        public bool PlaceItem(GridItem item)
        {
            ItemSize currentSize = item.GetCurrentSize();

            if (!CanPlaceItem(item.GridPosition, currentSize))
                return false;

            // Marquer les cases comme occupées
            for (int x = 0; x < currentSize.Width; x++)
            {
                for (int y = 0; y < currentSize.Height; y++)
                {
                    occupiedCells[item.GridPosition.X + x, item.GridPosition.Y + y] = true;
                }
            }

            items.Add(item);
            return true;
        }

        /// <summary>
        /// Retire un item de la grille
        /// </summary>
        public void RemoveItem(GridItem item)
        {
            if (!items.Contains(item))
                return;

            ItemSize currentSize = item.GetCurrentSize();

            // Libérer les cases
            for (int x = 0; x < currentSize.Width; x++)
            {
                for (int y = 0; y < currentSize.Height; y++)
                {
                    int cellX = item.GridPosition.X + x;
                    int cellY = item.GridPosition.Y + y;

                    if (cellX >= 0 && cellX < Width && cellY >= 0 && cellY < Height)
                        occupiedCells[cellX, cellY] = false;
                }
            }

            items.Remove(item);
        }

        /// <summary>
        /// Obtient l'item à une position donnée
        /// </summary>
        public GridItem GetItemAt(Point gridPos)
        {
            foreach (var item in items)
            {
                ItemSize size = item.GetCurrentSize();

                if (gridPos.X >= item.GridPosition.X &&
                    gridPos.X < item.GridPosition.X + size.Width &&
                    gridPos.Y >= item.GridPosition.Y &&
                    gridPos.Y < item.GridPosition.Y + size.Height)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// Trouve la première position libre pour un item
        /// </summary>
        public Point? FindFreePosition(ItemSize size, bool tryRotation = true)
        {
            // Essayer position normale
            for (int y = 0; y <= Height - size.Height; y++)
            {
                for (int x = 0; x <= Width - size.Width; x++)
                {
                    Point pos = new Point(x, y);
                    if (CanPlaceItem(pos, size))
                        return pos;
                }
            }

            // Essayer avec rotation
            if (tryRotation && size.Width != size.Height)
            {
                ItemSize rotatedSize = size.Rotated();
                for (int y = 0; y <= Height - rotatedSize.Height; y++)
                {
                    for (int x = 0; x <= Width - rotatedSize.Width; x++)
                    {
                        Point pos = new Point(x, y);
                        if (CanPlaceItem(pos, rotatedSize))
                            return pos;
                    }
                }
            }

            return null;
        }

        public List<GridItem> GetAllItems()
        {
            return new List<GridItem>(items);
        }

        public void Clear()
        {
            items.Clear();
            occupiedCells = new bool[Width, Height];
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BASE DE DONNÉES DES TAILLES D'ITEMS
    // ═══════════════════════════════════════════════════════════════════════
    public static class ItemSizeDatabase
    {
        /// <summary>
        /// Retourne la taille en cases de grille pour un item donné
        /// </summary>
        public static ItemSize GetItemSize(string itemName)
        {
            // ARMES
            if (itemName.Contains("Rifle") || itemName.Contains("Shotgun"))
                return new ItemSize(1, 4);  // Fusils: 1x4 (verticaux)

            if (itemName.Contains("SMG") || itemName.Contains("Pistol"))
                return new ItemSize(1, 2);  // Armes courtes: 1x2

            if (itemName.Contains("Sniper"))
                return new ItemSize(1, 5);  // Sniper: 1x5 (très long)

            // CASQUES
            if (itemName.Contains("Helmet") || itemName.Contains("ACH") ||
                itemName.Contains("ECH") || itemName.Contains("MICH"))
                return new ItemSize(2, 2);  // Casques: 2x2

            // GILETS
            if (itemName.Contains("Vest") || itemName.Contains("OTV") ||
                itemName.Contains("MTV") || itemName.Contains("IMTV") ||
                itemName.Contains("IOTV") || itemName.Contains("Jacket"))
                return new ItemSize(2, 3);  // Gilets: 2x3

            // BOUCLIERS
            if (itemName.Contains("Shield"))
                return new ItemSize(2, 4);  // Boucliers: 2x4 (grands)

            // CHEMISES ET PETITS ITEMS
            if (itemName.Contains("Shirt"))
                return new ItemSize(2, 2);  // Chemises: 2x2

            // GRENADES
            if (itemName.Contains("Grenade"))
                return new ItemSize(1, 1);  // Grenades: 1x1

            // Par défaut
            return new ItemSize(1, 1);
        }

        /// <summary>
        /// Retourne la couleur de fond d'un type d'item
        /// </summary>
        public static Color GetItemColor(ItemType type)
        {
            switch (type)
            {
                case ItemType.Weapon:
                    return new Color(100, 150, 255);  // Bleu
                case ItemType.Armor:
                    return new Color(150, 100, 50);   // Marron
                case ItemType.Grenade:
                    return new Color(200, 100, 0);    // ✅ Orange
                case ItemType.Accessory:
                    return new Color(100, 200, 100);  // Vert
                default:
                    return Color.Gray;
            }
        }
    }
}