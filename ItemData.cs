using System;
using Microsoft.Xna.Framework;

namespace XCOM_3
{
    // ═══════════════════════════════════════════════════════════════════════
    // SYSTÈME D'INVENTAIRE - VERSION UNIFIÉE
    // Ce fichier remplace TOUTES les autres définitions de ItemData
    // ═══════════════════════════════════════════════════════════════════════

    public enum ItemType
    {
        Weapon,
        Armor,
        Accessory,
        Grenade
    }

    public enum ArmorSlot
    {
        Head,
        Torso,
        Shield,
        Shirt
    }

    public enum ProtectionLevel
    {
        None,
        Fragmentation,
        NIJ_II,
        NIJ_IIIA,
        NIJ_III,
        NIJ_IV
    }

    /// <summary>
    /// Classe UNIQUE pour tous les items du jeu
    /// </summary>
    public class ItemData
    {
        public string Name;
        public ItemType Type;

        // Données d'arme
        public WeaponData WeaponData;

        // Données d'armure
        public int ArmorValue;
        public ArmorSlot ArmorSlot;
        public ProtectionLevel ProtectionLevel;
        public int MobilityPenalty;
        public string Description;

        // Données de grenades
        public GrenadeData GrenadeData;

        // Constructeur pour armes
        public ItemData(string name, ItemType type, WeaponData weaponData)
        {
            Name = name;
            Type = type;
            WeaponData = weaponData;
            ArmorValue = 0;
            ArmorSlot = ArmorSlot.Head;
            ProtectionLevel = ProtectionLevel.None;
            MobilityPenalty = 0;
            Description = "";
        }

        // Constructeur pour armures
        public ItemData(string name, ItemType type, int armorValue = 0,
                       ArmorSlot armorSlot = ArmorSlot.Head,
                       ProtectionLevel protectionLevel = ProtectionLevel.None,
                       int mobilityPenalty = 0,
                       string description = "")
        {
            Name = name;
            Type = type;
            WeaponData = null;
            ArmorValue = armorValue;
            ArmorSlot = armorSlot;
            ProtectionLevel = protectionLevel;
            MobilityPenalty = mobilityPenalty;
            Description = description;
        }

        // Constructeur pour grenades
        public ItemData(string name, GrenadeData grenadeData)
        {
            Name = name;
            Type = ItemType.Grenade;
            GrenadeData = grenadeData;
        }

    }

    /// <summary>
    /// Instance d'un item avec position dans l'inventaire
    /// </summary>
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