using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Base de données centralisée des armures
    /// </summary>
    public static class ArmorDatabase
    {
        // Cache interne (créé une seule fois)
        private static readonly ItemData[] _allArmors;
        private static readonly Dictionary<string, ItemData> _armorByName;

        // Static ctor
        static ArmorDatabase()
        {
            _allArmors = BuildArmors();
            _armorByName = _allArmors.ToDictionary(a => a.Name);
        }

        // ─────────────────────────────────────────────────────────────
        // API PUBLIQUE
        // ─────────────────────────────────────────────────────────────

        public static IReadOnlyList<ItemData> GetAllArmors() => _allArmors;

        public static ItemData GetArmor(string name)
            => _armorByName.TryGetValue(name, out var armor) ? armor : null;

        public static IReadOnlyList<ItemData> GetArmorsBySlot(ArmorSlot slot)
            => _allArmors.Where(a => a.ArmorSlot == slot).ToArray();

        // ─────────────────────────────────────────────────────────────
        // CONSTRUCTION DES ARMURES
        // ─────────────────────────────────────────────────────────────

        private static ItemData[] BuildArmors()
        {
            var list = new List<ItemData>();

            // ═════════ CASQUES ═════════
            list.AddRange(new[]
            {
                Helmet("M1 Helmet", 8, ProtectionLevel.Fragmentation, "WWII. Protection contre éclats."),
                Helmet("PASGT Helmet", 12, ProtectionLevel.NIJ_IIIA, "NIJ IIIA."),
                Helmet("Lightweight Helmet", 13, ProtectionLevel.NIJ_IIIA, "Version allégée."),
                Helmet("MICH", 14, ProtectionLevel.NIJ_IIIA, "Modular Integrated Communications Helmet."),
                Helmet("ACH", 15, ProtectionLevel.NIJ_IIIA, "Advanced Combat Helmet."),
                Helmet("ECH", 16, ProtectionLevel.NIJ_IIIA, "Enhanced Combat Helmet."),
            });

            // ═════════ GILETS ═════════
            list.AddRange(new[]
            {
                Vest("M-1952 Flak Jacket", 10, ProtectionLevel.Fragmentation, "Guerre de Corée."),
                Vest("M-69 Vest", 12, ProtectionLevel.Fragmentation, "Vietnam."),
                Vest("M-1955 Vest", 14, ProtectionLevel.Fragmentation, "Plaques Doron."),
                Vest("PASGT Vest", 18, ProtectionLevel.NIJ_II, "Kevlar standard."),
            });

            // Génération moderne + variantes
            AddPlateVariants(list, "OTV (IBA)", 22);
            AddPlateVariants(list, "MTV", 24);
            AddPlateVariants(list, "IMTV", 26);
            AddPlateVariants(list, "IOTV", 28);

            // ═════════ BOUCLIERS ═════════
            list.AddRange(new[]
            {
                Shield("Riot Shield", 15, ProtectionLevel.None, 1, "Anti-émeute."),
                Shield("Ballistic Shield", 30, ProtectionLevel.NIJ_IIIA, 2, "NIJ IIIA."),
                Shield("Heavy Ballistic Shield", 45, ProtectionLevel.NIJ_III, 2, "NIJ III."),
            });

            // ═════════ CHEMISE ═════════
            list.Add(new ItemData(
                "Army Combat Shirt",
                ItemType.Armor,
                2,
                ArmorSlot.Shirt,
                ProtectionLevel.None,
                0,
                "Protection feu / confort thermique."
            ));

            return list.ToArray();
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────

        private static ItemData Helmet(string name, int armor, ProtectionLevel level, string desc)
            => new ItemData(name, ItemType.Armor, armor, ArmorSlot.Head, level, 0, desc);

        private static ItemData Vest(string name, int armor, ProtectionLevel level, string desc)
            => new ItemData(name, ItemType.Armor, armor, ArmorSlot.Torso, level, 0, desc);

        private static ItemData Shield(string name, int armor, ProtectionLevel level, int apPenalty, string desc)
            => new ItemData(name, ItemType.Armor, armor, ArmorSlot.Shield, level, apPenalty, desc);

        private static void AddPlateVariants(List<ItemData> list, string baseName, int baseArmor)
        {
            list.Add(Vest(baseName, baseArmor, ProtectionLevel.NIJ_IIIA, "Base NIJ IIIA"));

            list.Add(new ItemData(
                $"{baseName} + SAPI",
                ItemType.Armor,
                baseArmor + 13,
                ArmorSlot.Torso,
                ProtectionLevel.NIJ_III,
                1,
                "Plaques SAPI. -1 PM."
            ));

            list.Add(new ItemData(
                $"{baseName} + ESAPI",
                ItemType.Armor,
                baseArmor + 20,
                ArmorSlot.Torso,
                ProtectionLevel.NIJ_IV,
                1,
                "Plaques ESAPI. -1 PM."
            ));
        }
    }
}
