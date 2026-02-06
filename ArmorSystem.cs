using System;

namespace XCOM_3
{
    // ═══════════════════════════════════════════════════════════════════════
    // BASE DE DONNÉES DES ARMURES MILITAIRES
    // ATTENTION: Ce fichier NE définit PAS ItemData, ArmorSlot, etc.
    // Ces classes doivent être définies dans ItemData_UNIFIED.cs
    // ═══════════════════════════════════════════════════════════════════════

    public static class ArmorDatabase
    {
        /// <summary>
        /// Retourne toutes les armures disponibles dans le jeu
        /// </summary>
        public static ItemData[] GetAllArmors()
        {
            return new ItemData[]
            {
                // ═══════════════════════════════════════════════════════════
                // CASQUES (HEAD)
                // ═══════════════════════════════════════════════════════════
                
                new ItemData("M1 Helmet", ItemType.Armor, 8, ArmorSlot.Head,
                    ProtectionLevel.Fragmentation, 0,
                    "Casque historique WWII. Protection contre éclats."),

                new ItemData("PASGT Helmet", ItemType.Armor, 12, ArmorSlot.Head,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Personnel Armor System for Ground Troops. NIJ IIIA."),

                new ItemData("Lightweight Helmet", ItemType.Armor, 13, ArmorSlot.Head,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Version allégée moderne. NIJ IIIA."),

                new ItemData("MICH", ItemType.Armor, 14, ArmorSlot.Head,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Modular Integrated Communications Helmet. NIJ IIIA."),

                new ItemData("ACH", ItemType.Armor, 15, ArmorSlot.Head,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Advanced Combat Helmet. NIJ IIIA."),

                new ItemData("ECH", ItemType.Armor, 16, ArmorSlot.Head,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Enhanced Combat Helmet. Meilleure protection NIJ IIIA."),
                
                // ═══════════════════════════════════════════════════════════
                // GILETS PARE-BALLES (TORSO)
                // ═══════════════════════════════════════════════════════════
                
                // --- Génération Early (1950s-1960s) ---
                
                new ItemData("M-1952 Flak Jacket", ItemType.Armor, 10, ArmorSlot.Torso,
                    ProtectionLevel.Fragmentation, 0,
                    "Gilet anti-éclats. Guerre de Corée."),

                new ItemData("M-69 Vest", ItemType.Armor, 12, ArmorSlot.Torso,
                    ProtectionLevel.Fragmentation, 0,
                    "Gilet nylon anti-fragmentation. Vietnam."),

                new ItemData("M-1955 Vest", ItemType.Armor, 14, ArmorSlot.Torso,
                    ProtectionLevel.Fragmentation, 0,
                    "Nylon + plaques Doron. Protection améliorée."),
                
                // --- Génération PASGT (1980s) ---
                
                new ItemData("PASGT Vest", ItemType.Armor, 18, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_II, 0,
                    "Premier gilet kevlar standard. NIJ II."),
                
                // --- Génération moderne (2000s+) ---
                
                new ItemData("OTV (IBA)", ItemType.Armor, 22, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Outer Tactical Vest. Base NIJ IIIA."),

                new ItemData("OTV + SAPI", ItemType.Armor, 35, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_III, 1,
                    "OTV avec plaques céramique. NIJ III. -1 PM."),

                new ItemData("OTV + ESAPI", ItemType.Armor, 42, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IV, 1,
                    "OTV avec plaques renforcées. NIJ IV. -1 PM."),

                new ItemData("MTV", ItemType.Armor, 24, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Modular Tactical Vest. Base NIJ IIIA."),

                new ItemData("MTV + SAPI", ItemType.Armor, 37, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_III, 1,
                    "MTV avec plaques SAPI. NIJ III. -1 PM."),

                new ItemData("MTV + ESAPI", ItemType.Armor, 44, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IV, 1,
                    "MTV avec plaques ESAPI. NIJ IV. -1 PM."),

                new ItemData("IMTV", ItemType.Armor, 26, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Improved MTV. Protection optimisée."),

                new ItemData("IMTV + SAPI", ItemType.Armor, 38, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_III, 1,
                    "IMTV avec plaques SAPI. NIJ III. -1 PM."),

                new ItemData("IMTV + ESAPI", ItemType.Armor, 45, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IV, 1,
                    "IMTV avec plaques ESAPI. NIJ IV. -1 PM."),

                new ItemData("IOTV", ItemType.Armor, 28, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IIIA, 0,
                    "Improved OTV. Dernier cri."),

                new ItemData("IOTV + SAPI", ItemType.Armor, 40, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_III, 1,
                    "IOTV avec SAPI. Protection maximale. -1 PM."),

                new ItemData("IOTV + ESAPI", ItemType.Armor, 48, ArmorSlot.Torso,
                    ProtectionLevel.NIJ_IV, 1,
                    "IOTV avec ESAPI. Protection ultime. -1 PM."),
                
                // ═══════════════════════════════════════════════════════════
                // BOUCLIERS (SHIELD) - Équipement spécial
                // ═══════════════════════════════════════════════════════════
                
                new ItemData("Riot Shield", ItemType.Armor, 15, ArmorSlot.Shield,
                    ProtectionLevel.None, 1,
                    "Bouclier anti-émeute. Aucune protection balistique. -1 PM."),

                new ItemData("Ballistic Shield", ItemType.Armor, 30, ArmorSlot.Shield,
                    ProtectionLevel.NIJ_IIIA, 2,
                    "Bouclier balistique NIJ IIIA. Très lourd. -2 PM."),

                new ItemData("Heavy Ballistic Shield", ItemType.Armor, 45, ArmorSlot.Shield,
                    ProtectionLevel.NIJ_III, 2,
                    "Bouclier balistique renforcé NIJ III. -2 PM."),
                
                // ═══════════════════════════════════════════════════════════
                // CHEMISES TACTIQUES (SHIRT)
                // ═══════════════════════════════════════════════════════════
                
                new ItemData("Army Combat Shirt", ItemType.Armor, 2, ArmorSlot.Shirt,
                    ProtectionLevel.None, 0,
                    "Protection contre le feu. Bonus mineur."),
            };
        }

        /// <summary>
        /// Retourne une armure par son nom
        /// </summary>
        public static ItemData GetArmor(string name)
        {
            foreach (var armor in GetAllArmors())
            {
                if (armor.Name == name)
                    return armor;
            }
            return null;
        }

        /// <summary>
        /// Retourne toutes les armures d'un slot spécifique
        /// </summary>
        public static ItemData[] GetArmorsBySlot(ArmorSlot slot)
        {
            var allArmors = GetAllArmors();
            var filtered = new System.Collections.Generic.List<ItemData>();

            foreach (var armor in allArmors)
            {
                if (armor.ArmorSlot == slot)
                    filtered.Add(armor);
            }

            return filtered.ToArray();
        }
    }
}
