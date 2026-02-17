using System;

namespace XCOM_3
{
    /// <summary>
    /// Pipeline centralisé pour créer de nouveaux items avec les informations de base.
    /// </summary>
    public static class ItemCreationPipeline
    {
        public static ItemData CreateArmorItem(
            string name,
            ArmorSlot armorSlot,
            float weightLbs,
            int armorValue = 0,
            ProtectionLevel protectionLevel = ProtectionLevel.None,
            int mobilityPenalty = 0,
            int bonusInventorySlots = 0,
            string description = "",
            int fragmentationProtectionPercent = 0)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Le nom d'item est obligatoire.", nameof(name));

            if (weightLbs <= 0f)
                throw new ArgumentOutOfRangeException(nameof(weightLbs), "Le poids doit être supérieur à 0.");

            var item = new ItemData(
                name,
                ItemType.Armor,
                armorValue,
                armorSlot,
                protectionLevel,
                mobilityPenalty,
                weightLbs,
                bonusInventorySlots,
                description,
                fragmentationProtectionPercent);

            item.GeneratedTextureKey = BuildGeneratedTextureKey(name, armorSlot);
            return item;
        }

        public static ItemData CreateHelmet(
            string name,
            int armorValue,
            ProtectionLevel protectionLevel,
            float weightLbs,
            string description,
            int fragmentationProtectionPercent)
        {
            return CreateArmorItem(
                name,
                ArmorSlot.Head,
                weightLbs,
                armorValue,
                protectionLevel,
                0,
                0,
                description,
                fragmentationProtectionPercent);
        }

        private static string BuildGeneratedTextureKey(string name, ArmorSlot slot)
        {
            string safeName = name.Trim().ToLowerInvariant().Replace(' ', '_');
            return $"generated_{slot.ToString().ToLowerInvariant()}_{safeName}";
        }
    }
}
