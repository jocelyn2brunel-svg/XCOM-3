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
        // Cache interne
        private static readonly ItemData[] _allArmors;
        private static readonly Dictionary<string, ItemData> _armorByName;

        // Static constructor
        static ArmorDatabase()
        {
            _allArmors = BuildArmors();
            _armorByName = _allArmors.ToDictionary(a => a.Name);
        }

        // ─────────────────────────────────────────────
        // API PUBLIQUE
        // ─────────────────────────────────────────────

        public static IReadOnlyList<ItemData> GetAllArmors() => _allArmors;

        public static ItemData GetArmor(string name)
            => _armorByName.TryGetValue(name, out var armor) ? armor : null;

        public static IReadOnlyList<ItemData> GetArmorsBySlot(ArmorSlot slot)
            => _allArmors.Where(a => a.ArmorSlot == slot).ToArray();

        // ─────────────────────────────────────────────
        // CONSTRUCTION DES ARMURES
        // ─────────────────────────────────────────────

        private static ItemData[] BuildArmors()
        {
            var list = new List<ItemData>();

            AddHelmets(list);
            AddNeckArmors(list);
            AddVests(list);
            AddModernPlates(list);
            AddShields(list);
            AddCombatShirts(list);
            AddPants(list);
            AddKneeArmors(list);
            AddBoots(list);
            AddChestRigs(list);
            AddBackpacks(list);

            return list.ToArray();
        }

        // ─────────────────────────────────────────────
        // CASQUES
        // ─────────────────────────────────────────────
        private static void AddHelmets(List<ItemData> list)
        {
            list.AddRange(new[]
            {
                Helmet("M1 Helmet", 8, ProtectionLevel.Fragmentation, 2.9f, "Casque acier WWII. Bonne protection contre les éclats.", 18),
                Helmet("PASGT Helmet", 12, ProtectionLevel.NIJ_IIIA, 3.1f, "Kevlar balistique NIJ IIIA, couvre mieux nuque et tempes.", 24),
                Helmet("Lightweight Helmet", 13, ProtectionLevel.NIJ_IIIA, 2.4f, "Version allégée du PASGT, mobilité améliorée.", 20),
                Helmet("MICH", 14, ProtectionLevel.NIJ_IIIA, 3.0f, "Casque modulaire MICH compatible communications.", 23),
                Helmet("ACH", 15, ProtectionLevel.NIJ_IIIA, 3.3f, "Advanced Combat Helmet avec meilleur amorti d'impact.", 26),
                Helmet("ECH", 16, ProtectionLevel.NIJ_IIIA, 3.6f, "Enhanced Combat Helmet, excellente tenue face aux éclats.", 30),
                Helmet("Casquette Patrouille", 4, ProtectionLevel.Fragmentation, 1.1f, "Casquette légère avec renfort anti-éclats.", 8),
                Helmet("Casquette Tactique", 5, ProtectionLevel.Fragmentation, 1.3f, "Casquette tactique modernisée, confortable et discrète.", 10),
            });
        }

        // ─────────────────────────────────────────────
        // PROTECTIONS DE COU
        // ─────────────────────────────────────────────
        private static void AddNeckArmors(List<ItemData> list)
        {
            list.Add(new ItemData(
                "Ballistic Neck Armor",
                ItemType.Armor,
                1,
                ArmorSlot.Neck,
                ProtectionLevel.Fragmentation,
                0,
                0.4f,
                0,
                "Protège-cou balistique léger. Réduit légèrement les blessures par éclats.",
                2
            ));
        }

        // ─────────────────────────────────────────────
        // GILETS
        // ─────────────────────────────────────────────
        private static void AddVests(List<ItemData> list)
        {
            list.AddRange(new[]
            {
                Vest("M-1952 Flak Jacket", 10, ProtectionLevel.Fragmentation, 8.5f, "Flak Jacket guerre de Corée, optimisé anti-fragments.", 36),
                Vest("M-69 Vest", 12, ProtectionLevel.Fragmentation, 6.8f, "Gilet M-69 Vietnam, compromis protection/poids.", 30),
                Vest("M-1955 Vest", 14, ProtectionLevel.Fragmentation, 9.7f, "Gilet M-1955 à plaques Doron, très bon contre éclats.", 34),
                Vest("PASGT Vest", 18, ProtectionLevel.NIJ_II, 7.8f, "Gilet Kevlar PASGT standard, bonne couverture torse.", 28),
            });
        }

        // ─────────────────────────────────────────────
        // PLAQUES MODERNES + VARIANTES
        // ─────────────────────────────────────────────
        private static void AddModernPlates(List<ItemData> list)
        {
            AddPlateVariants(list, "OTV (IBA)", 22);
            AddPlateVariants(list, "MTV", 24);
            AddPlateVariants(list, "IMTV", 26);
            AddPlateVariants(list, "IOTV", 28);
        }

        // ─────────────────────────────────────────────
        // BOUCLIERS
        // ─────────────────────────────────────────────
        private static void AddShields(List<ItemData> list)
        {
            list.AddRange(new[]
            {
                Shield("Riot Shield", 15, ProtectionLevel.None, 1, 7.5f, "Bouclier anti-émeute, utile en protection de proximité.", 12),
                Shield("Ballistic Shield", 30, ProtectionLevel.NIJ_IIIA, 2, 12.5f, "Bouclier balistique NIJ IIIA, forte absorption des éclats.", 35),
                Shield("Heavy Ballistic Shield", 45, ProtectionLevel.NIJ_III, 2, 19.0f, "Bouclier lourd NIJ III, protection maximale mais encombrant.", 45),
            });
        }

        // ─────────────────────────────────────────────
        // CHEMISES
        // ─────────────────────────────────────────────
        private static void AddCombatShirts(List<ItemData> list)
        {
            list.Add(new ItemData(
                "Army Combat Shirt",
                ItemType.Armor,
                2,
                ArmorSlot.Shirt,
                ProtectionLevel.None,
                0,
                0.8f,
                0,
                "Protection feu / confort thermique.",
                4
            ));

            list.AddRange(new[]
            {
                Tshirt("T-Shirt Noir"),
                Tshirt("T-Shirt Blanc"),
                Tshirt("T-Shirt Bleu"),
                Tshirt("T-Shirt Vert"),
            });
        }

        private static void AddPants(List<ItemData> list)
        {
            list.Add(new ItemData(
                "Jeans Léger",
                ItemType.Armor,
                0,
                ArmorSlot.Pants,
                ProtectionLevel.None,
                0,
                1.2f,
                4,
                "Jeans léger modernisé (1.2 lb). Ajoute 4 poches 1x1."
            ));

            list.Add(new ItemData(
                "Pantalon de Travail",
                ItemType.Armor,
                0,
                ArmorSlot.Pants,
                ProtectionLevel.None,
                0,
                2.2f,
                5,
                "Pantalon de travail renforcé (2.2 lb). Ajoute 5 poches 1x1."
            ));

            list.Add(new ItemData(
                "Pantalon Cargo Tactique",
                ItemType.Armor,
                0,
                ArmorSlot.Pants,
                ProtectionLevel.None,
                0,
                2.4f,
                6,
                "Pantalon cargo tactique (2.4 lb). Ajoute 6 poches 1x1."
            ));
        }

        private static void AddChestRigs(List<ItemData> list)
        {
            list.Add(new ItemData(
                "Chest Rig Léger",
                ItemType.Armor,
                0,
                ArmorSlot.ChestRig,
                ProtectionLevel.None,
                0,
                1.2f,
                3,
                "Chest rig léger. Ajoute 3 emplacements utilitaires 1x1."
            ));

            list.Add(new ItemData(
                "Chest Rig Assaut",
                ItemType.Armor,
                1,
                ArmorSlot.ChestRig,
                ProtectionLevel.Fragmentation,
                0,
                1.8f,
                4,
                "Chest rig assaut renforcé, protection limitée contre éclats. Ajoute 4 emplacements utilitaires 1x1.",
                8
            ));
        }

        private static void AddKneeArmors(List<ItemData> list)
        {
            list.Add(new ItemData(
                "Genouilleres Souples",
                ItemType.Armor,
                1,
                ArmorSlot.Knees,
                ProtectionLevel.Fragmentation,
                0,
                0.7f,
                0,
                "Genouilleres souples pour patrouille. Confort et protection legere contre les eclats.",
                6
            ));

            list.Add(new ItemData(
                "Genouilleres Renforcees",
                ItemType.Armor,
                3,
                ArmorSlot.Knees,
                ProtectionLevel.NIJ_II,
                0,
                1.1f,
                0,
                "Coques renforcees avec mousse interne. Bonne protection en progression urbaine.",
                12
            ));
        }

        private static void AddBoots(List<ItemData> list)
        {
            list.Add(new ItemData(
                "Bottes de Patrouille",
                ItemType.Armor,
                1,
                ArmorSlot.Feet,
                ProtectionLevel.Fragmentation,
                0,
                2.3f,
                0,
                "Bottes de patrouille legeres. Maintien correct et protection de base.",
                5
            ));

            list.Add(new ItemData(
                "Bottes Tactiques Renforcees",
                ItemType.Armor,
                3,
                ArmorSlot.Feet,
                ProtectionLevel.NIJ_II,
                1,
                3.1f,
                0,
                "Bottes tactiques a embout renforce et semelle anti-perforation. Plus protectrices mais un peu lourdes.",
                10
            ));
        }

        private static void AddBackpacks(List<ItemData> list)
        {
            list.Add(new ItemData(
                "Backpack Small",
                ItemType.Armor,
                0,
                ArmorSlot.Backpack,
                ProtectionLevel.None,
                0,
                1.0f,
                0,
                "Sac à dos compact. 4 emplacements utilitaires."
            ));

            list.Add(new ItemData(
                "Backpack Medium",
                ItemType.Armor,
                0,
                ArmorSlot.Backpack,
                ProtectionLevel.None,
                0,
                1.7f,
                0,
                "Sac à dos intermédiaire. 8 emplacements utilitaires."
            ));

            list.Add(new ItemData(
                "Backpack XL",
                ItemType.Armor,
                0,
                ArmorSlot.Backpack,
                ProtectionLevel.None,
                0,
                2.4f,
                0,
                "Sac à dos grande capacité. 12 emplacements utilitaires."
            ));
        }

        // ─────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────

        private static ItemData Helmet(string name, int armor, ProtectionLevel level, float weightLbs, string desc, int fragResistPercent)
            => ItemCreationPipeline.CreateHelmet(name, armor, level, weightLbs, desc, fragResistPercent);

        private static ItemData Vest(string name, int armor, ProtectionLevel level, float weightLbs, string desc, int fragResistPercent)
            => new ItemData(name, ItemType.Armor, armor, ArmorSlot.Torso, level, 0, weightLbs, 0, desc, fragResistPercent);

        private static ItemData Shield(string name, int armor, ProtectionLevel level, int apPenalty, float weightLbs, string desc, int fragResistPercent)
            => new ItemData(name, ItemType.Armor, armor, ArmorSlot.Shield, level, apPenalty, weightLbs, 0, desc, fragResistPercent);

        private static void AddPlateVariants(List<ItemData> list, string baseName, int baseArmor)
        {
            list.Add(Vest(baseName, baseArmor, ProtectionLevel.NIJ_IIIA, 16.0f, "Veste ballistique NIJ IIIA avec slots pour plaques SAPI/ESAPI.", 18));
            list.Add(new ItemData(
                $"{baseName} + SAPI",
                ItemType.Armor,
                baseArmor + 13,
                ArmorSlot.Torso,
                ProtectionLevel.NIJ_III,
                1,
                21.0f,
                0,
                "Plaques SAPI. -1 PM. Meilleure tenue aux éclats secondaires.",
                26
            ));
            list.Add(new ItemData(
                $"{baseName} + ESAPI",
                ItemType.Armor,
                baseArmor + 20,
                ArmorSlot.Torso,
                ProtectionLevel.NIJ_IV,
                1,
                24.0f,
                0,
                "Plaques ESAPI. -1 PM. Excellente résistance multi-menaces.",
                32
            ));
        }

        private static ItemData Tshirt(string name)
            => new ItemData(
                name,
                ItemType.Armor,
                0,
                ArmorSlot.Shirt,
                ProtectionLevel.None,
                0,
                0.33f,
                0,
                "T-shirt casual (150 g), volume 1x1. Plusieurs couleurs disponibles."
            );
    }
}
