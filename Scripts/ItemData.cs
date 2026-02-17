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
        None,
        Head,
        Neck,
        Torso,
        Shield,
        Shirt,
        Pants,
        ChestRig,
        Belt,
        Backpack
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
        public string Name { get; set; }
        public ItemType Type { get; set; }

        // Données d'arme
        public WeaponData WeaponData { get; set; }

        // Données d'armure
        public int ArmorValue { get; set; }
        public ArmorSlot ArmorSlot { get; set; }
        public ProtectionLevel ProtectionLevel;
        public int MobilityPenalty;
        public float WeightLbs;
        public int BonusInventorySlots;
        public int FragmentationProtectionPercent;
        public string Description;

        // Données de grenades
        public GrenadeData GrenadeData { get; set; }

        // Constructeur pour armes
        public ItemData(string name, ItemType type, WeaponData weaponData)
        {
            Name = name;
            Type = type;
            WeaponData = weaponData;
            ArmorValue = 0;
            ArmorSlot = ArmorSlot.None;
            ProtectionLevel = ProtectionLevel.None;
            MobilityPenalty = 0;
            WeightLbs = weaponData?.WeightLbs ?? 0f;
            BonusInventorySlots = 0;
            FragmentationProtectionPercent = 0;
            Description = "";
        }

        // Constructeur pour armures
        public ItemData(string name, ItemType type, int armorValue = 0,
                       ArmorSlot armorSlot = ArmorSlot.None,
                       ProtectionLevel protectionLevel = ProtectionLevel.None,
                       int mobilityPenalty = 0,
                       float weightLbs = 0f,
                       int bonusInventorySlots = 0,
                       string description = "",
                       int fragmentationProtectionPercent = 0)
        {
            Name = name;
            Type = type;
            WeaponData = null;
            ArmorValue = armorValue;
            ArmorSlot = armorSlot;
            ProtectionLevel = protectionLevel;
            MobilityPenalty = mobilityPenalty;
            WeightLbs = weightLbs;
            BonusInventorySlots = bonusInventorySlots;
            FragmentationProtectionPercent = Math.Clamp(fragmentationProtectionPercent, 0, 95);
            Description = description;
        }

        // Constructeur pour grenades
        public ItemData(string name, GrenadeData grenadeData, float weightLbs = 0f, string description = "")
        {
            Name = name;
            Type = ItemType.Grenade;
            GrenadeData = grenadeData;
            WeightLbs = weightLbs;
            BonusInventorySlots = 0;
            FragmentationProtectionPercent = 0;
            Description = description;
        }

    }

    /// <summary>
    /// Instance d'un item avec position dans l'inventaire
    /// </summary>
    public class Item
    {
        public ItemData Data { get; set; }
        public Point Position { get; set; }
        public Rectangle Bounds => new Rectangle(Position.X, Position.Y, 50, 50);

        public Item(ItemData data, Point position)
        {
            Data = data;
            Position = position;
        }
    }
}
