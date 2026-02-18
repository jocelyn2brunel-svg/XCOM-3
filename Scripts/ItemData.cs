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
        Knees,
        Feet,
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
        public int BodyCoveragePercent;
        public string Description;
        public string GeneratedTextureKey { get; set; }

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
            BodyCoveragePercent = 0;
            Description = "";
            GeneratedTextureKey = string.Empty;
        }

        // Constructeur pour armures
        public ItemData(string name, ItemType type, int armorValue = 0,
                       ArmorSlot armorSlot = ArmorSlot.None,
                       ProtectionLevel protectionLevel = ProtectionLevel.None,
                       int mobilityPenalty = 0,
                       float weightLbs = 0f,
                       int bonusInventorySlots = 0,
                       string description = "",
                       int fragmentationProtectionPercent = 0,
                       int bodyCoveragePercent = 0)
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
            BodyCoveragePercent = Math.Clamp(bodyCoveragePercent, 0, 100);
            Description = description;
            GeneratedTextureKey = string.Empty;
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
            BodyCoveragePercent = 0;
            Description = description;
            GeneratedTextureKey = string.Empty;
        }

        public int GetEffectiveBodyCoveragePercent()
            => BodyCoveragePercent > 0 ? BodyCoveragePercent : GetDefaultBodyCoveragePercent(ArmorSlot);

        public int GetNijProtectionPercent()
        {
            int nijBase = GetNijBaseProtectionPercent(ProtectionLevel);
            if (nijBase <= 0)
                return 0;

            float coverageRatio = GetEffectiveBodyCoveragePercent() / 100f;
            return Math.Clamp((int)Math.Round(nijBase * coverageRatio), 0, 95);
        }

        public int GetEffectiveFragmentationProtectionPercent()
        {
            if (FragmentationProtectionPercent > 0)
                return FragmentationProtectionPercent;

            return ProtectionLevel == ProtectionLevel.Fragmentation
                ? 12
                : GetNijProtectionPercent();
        }

        private static int GetNijBaseProtectionPercent(ProtectionLevel level)
        {
            return level switch
            {
                ProtectionLevel.NIJ_II => 35,
                ProtectionLevel.NIJ_IIIA => 45,
                ProtectionLevel.NIJ_III => 60,
                ProtectionLevel.NIJ_IV => 75,
                _ => 0
            };
        }

        private static int GetDefaultBodyCoveragePercent(ArmorSlot slot)
        {
            return slot switch
            {
                ArmorSlot.Head => 12,
                ArmorSlot.Neck => 4,
                ArmorSlot.Torso => 38,
                ArmorSlot.Shield => 30,
                ArmorSlot.Shirt => 18,
                ArmorSlot.Pants => 24,
                ArmorSlot.Knees => 6,
                ArmorSlot.Feet => 6,
                ArmorSlot.ChestRig => 16,
                ArmorSlot.Belt => 4,
                ArmorSlot.Backpack => 12,
                _ => 0
            };
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
