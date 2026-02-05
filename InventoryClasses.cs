using System;
using Microsoft.Xna.Framework;

namespace XCOM_3
{
    // ═══════════════════════════════════════════════════════════════════════
    // SYSTÈME D'INVENTAIRE - NOUVELLES CLASSES
    // ═══════════════════════════════════════════════════════════════════════

    public enum ItemType
    {
        Weapon,
        Armor
    }

    public enum ArmorSlot
    {
        Head,
        Torso
    }

    public class ItemData
    {
        public string Name;
        public ItemType Type;
        public WeaponData WeaponData;
        public int ArmorValue;
        public ArmorSlot ArmorSlot;

        // Constructeur pour armes
        public ItemData(string name, ItemType type, WeaponData weaponData)
        {
            Name = name;
            Type = type;
            WeaponData = weaponData;
        }

        // Constructeur pour armures
        public ItemData(string name, ItemType type, int armorValue = 0, ArmorSlot armorSlot = ArmorSlot.Head)
        {
            Name = name;
            Type = type;
            ArmorValue = armorValue;
            ArmorSlot = armorSlot;
        }
    }

    public class Item
    {
        public ItemData Data;
        public Point Position;
        public Rectangle Bounds => new Rectangle(Position.X, Position.Y, 50, 50);

        public Item(ItemData data, Point position)
        {
            Data = data;
            Position = position;
        }
    }
}
