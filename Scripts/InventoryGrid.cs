using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    public struct ItemSize
    {
        public int Width, Height;
        public ItemSize(int w, int h) { Width = w; Height = h; }
        public ItemSize Rotated() => new ItemSize(Height, Width);
    }

    public class GridItem
    {
        public ItemData Data;
        public Point GridPosition;
        public ItemSize Size;
        public bool IsRotated;
        public Rectangle PixelBounds { get; set; }
        private const int CELL_SIZE = 40;

        public GridItem(ItemData data, Point pos, ItemSize size, bool rotated = false)
        {
            Data = data; GridPosition = pos; Size = size; IsRotated = rotated;
            UpdatePixelBounds();
        }

        public void UpdatePixelBounds(int offsetX = 0, int offsetY = 0)
        {
            var s = IsRotated ? Size.Rotated() : Size;
            PixelBounds = new Rectangle(offsetX + GridPosition.X * CELL_SIZE,
                                       offsetY + GridPosition.Y * CELL_SIZE,
                                       s.Width * CELL_SIZE, s.Height * CELL_SIZE);
        }

        public void UpdatePixelBoundsAbsolute(int x, int y)
        {
            var s = IsRotated ? Size.Rotated() : Size;
            PixelBounds = new Rectangle(x, y, s.Width * CELL_SIZE, s.Height * CELL_SIZE);
        }

        public void Rotate() { IsRotated = !IsRotated; UpdatePixelBounds(); }
        public ItemSize GetCurrentSize() => IsRotated ? Size.Rotated() : Size;
    }

    public class InventoryGrid
    {
        public int Width, Height;
        private bool[,] occupied;
        private List<GridItem> items;
        private const int CELL_SIZE = 40;

        public InventoryGrid(int w, int h)
        {
            Width = w; Height = h; occupied = new bool[w, h]; items = new List<GridItem>();
        }

        public bool CanPlaceItem(Point pos, ItemSize size, GridItem ignore = null)
        {
            if (pos.X < 0 || pos.Y < 0 || pos.X + size.Width > Width || pos.Y + size.Height > Height)
                return false;
            for (int x = 0; x < size.Width; x++)
                for (int y = 0; y < size.Height; y++)
                    if (occupied[pos.X + x, pos.Y + y])
                        if (ignore == null || GetItemAt(new Point(pos.X + x, pos.Y + y)) != ignore)
                            return false;
            return true;
        }

        public bool PlaceItem(GridItem item)
        {
            var s = item.GetCurrentSize();
            if (!CanPlaceItem(item.GridPosition, s)) return false;
            for (int x = 0; x < s.Width; x++)
                for (int y = 0; y < s.Height; y++)
                    occupied[item.GridPosition.X + x, item.GridPosition.Y + y] = true;
            items.Add(item); return true;
        }

        public void RemoveItem(GridItem item)
        {
            if (!items.Contains(item)) return;
            var s = item.GetCurrentSize();
            for (int x = 0; x < s.Width; x++)
                for (int y = 0; y < s.Height; y++)
                {
                    int cx = item.GridPosition.X + x, cy = item.GridPosition.Y + y;
                    if (cx >= 0 && cx < Width && cy >= 0 && cy < Height)
                        occupied[cx, cy] = false;
                }
            items.Remove(item);
        }

        public GridItem GetItemAt(Point pos)
        {
            foreach (var item in items)
            {
                var s = item.GetCurrentSize();
                if (pos.X >= item.GridPosition.X && pos.X < item.GridPosition.X + s.Width &&
                    pos.Y >= item.GridPosition.Y && pos.Y < item.GridPosition.Y + s.Height)
                    return item;
            }
            return null;
        }

        public Point? FindFreePosition(ItemSize size, bool tryRotation = true)
        {
            for (int y = 0; y <= Height - size.Height; y++)
                for (int x = 0; x <= Width - size.Width; x++)
                    if (CanPlaceItem(new Point(x, y), size)) return new Point(x, y);
            if (tryRotation && size.Width != size.Height)
            {
                var r = size.Rotated();
                for (int y = 0; y <= Height - r.Height; y++)
                    for (int x = 0; x <= Width - r.Width; x++)
                        if (CanPlaceItem(new Point(x, y), r)) return new Point(x, y);
            }
            return null;
        }

        public List<GridItem> GetAllItems() => new List<GridItem>(items);
        public void Clear() { items.Clear(); occupied = new bool[Width, Height]; }
    }

    public static class ItemSizeDatabase
    {
        public static ItemSize GetItemSize(string name)
        {
            if (name.Contains("T-Shirt")) return new ItemSize(1, 1);
            if (name.Contains("Rifle") || name.Contains("Shotgun")) return new ItemSize(1, 4);
            if (name.Contains("SMG") || name.Contains("Pistol")) return new ItemSize(1, 2);
            if (name.Contains("Sniper")) return new ItemSize(1, 5);
            if (name.Contains("Helmet") || name.Contains("ACH") || name.Contains("ECH") || name.Contains("MICH")) return new ItemSize(2, 2);
            if (name.Contains("Vest") || name.Contains("OTV") || name.Contains("MTV") || name.Contains("IMTV") || name.Contains("IOTV") || name.Contains("Jacket")) return new ItemSize(2, 3);
            if (name.Contains("Shield")) return new ItemSize(2, 4);
            if (name.Contains("Shirt")) return new ItemSize(2, 2);
            if (name.Contains("Jeans") || name.Contains("Pantalon")) return new ItemSize(2, 1);
            if (name.Contains("Chest Rig")) return new ItemSize(2, 2);
            if (name.Contains("Grenade")) return new ItemSize(1, 1);
            return new ItemSize(1, 1);
        }


        public static bool IsPocketSized(string name)
        {
            ItemSize size = GetItemSize(name);
            return size.Width == 1 && size.Height == 1;
        }

        public static Color GetItemColor(ItemType type) => type switch
        {
            ItemType.Weapon => new Color(100, 150, 255),
            ItemType.Armor => new Color(150, 100, 50),
            ItemType.Grenade => new Color(200, 100, 0),
            ItemType.Accessory => new Color(100, 200, 100),
            _ => Color.Gray
        };
    }
}
