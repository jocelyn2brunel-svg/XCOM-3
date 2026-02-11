using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace XCOM_3
{
    public static class GrenadeDatabase
    {
        public static Dictionary<string, GrenadeData> GetAllGrenades()
        {
            var grenades = new Dictionary<string, GrenadeData>();

            // ═══════════════════════════════════════════════════════════════════
            // GRENADES EXISTANTES (conservées)
            // ═══════════════════════════════════════════════════════════════════

            grenades["Frag Grenade"] = new GrenadeData(
                "Frag Grenade",
                GrenadeType.Frag,
                damage: 40,
                radius: 2,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["HE Grenade"] = new GrenadeData(
                "HE Grenade",
                GrenadeType.HE,
                damage: 50,
                radius: 3,
                destroyWalls: true,
                digsTerrain: true,
                digDepth: 1,
                aoCost: 1
            );

            grenades["Plasma Grenade"] = new GrenadeData(
                "Plasma Grenade",
                GrenadeType.Plasma,
                damage: 70,
                radius: 2,
                destroyWalls: true,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["Demolition Charge"] = new GrenadeData(
                "Demolition Charge",
                GrenadeType.HE,
                damage: 80,
                radius: 4,
                destroyWalls: true,
                digsTerrain: true,
                digDepth: 3,
                aoCost: 2
            );

            grenades["Smoke Grenade"] = new GrenadeData(
                "Smoke Grenade",
                GrenadeType.Smoke,
                damage: 0,
                radius: 3,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["Flashbang"] = new GrenadeData(
                "Flashbang",
                GrenadeType.Flashbang,
                damage: 5,
                radius: 2,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["Incendiary Grenade"] = new GrenadeData(
                "Incendiary Grenade",
                GrenadeType.Incendiary,
                damage: 30,
                radius: 2,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["EMP Grenade"] = new GrenadeData(
                "EMP Grenade",
                GrenadeType.EMP,
                damage: 20,
                radius: 3,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            // ═══════════════════════════════════════════════════════════════════
            // ✅ NOUVELLES GRENADES RÉALISTES
            // ═══════════════════════════════════════════════════════════════════

            // --- Grenades fragmentaires militaires ---
            grenades["MK 2"] = new GrenadeData(
                "MK 2",
                GrenadeType.Frag,
                damage: 35,
                radius: 2,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["M26"] = new GrenadeData(
                "M26",
                GrenadeType.Frag,
                damage: 38,
                radius: 2,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["M67"] = new GrenadeData(
                "M67",
                GrenadeType.Frag,
                damage: 42,
                radius: 3,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            // --- Grenades thermobariques ---
            grenades["Thermobaric Grenade"] = new GrenadeData(
                "Thermobaric Grenade",
                GrenadeType.HE,
                damage: 90,
                radius: 4,
                destroyWalls: true,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 2
            );

            // --- Grenades anti-char ---
            grenades["Anti-Tank Grenade"] = new GrenadeData(
                "Anti-Tank Grenade",
                GrenadeType.HE,
                damage: 120,
                radius: 2,
                destroyWalls: true,
                digsTerrain: true,
                digDepth: 3,
                aoCost: 2
            );

            // --- Grenades incendiaires ---
            grenades["White Phosphorus"] = new GrenadeData(
                "White Phosphorus",
                GrenadeType.Incendiary,
                damage: 45,
                radius: 3,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 2
            );

            grenades["AN-M14 Incendiary"] = new GrenadeData(
                "AN-M14 Incendiary",
                GrenadeType.Incendiary,
                damage: 25,
                radius: 2,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            // --- Grenades assourdissantes ---
            grenades["Stun Grenade"] = new GrenadeData(
                "Stun Grenade",
                GrenadeType.Flashbang,
                damage: 3,
                radius: 3,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            // --- Grenades fumigènes ---
            grenades["Colored Smoke"] = new GrenadeData(
                "Colored Smoke",
                GrenadeType.Smoke,
                damage: 0,
                radius: 2,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["HC Smoke"] = new GrenadeData(
                "HC Smoke",
                GrenadeType.Smoke,
                damage: 0,
                radius: 4,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            // --- Grenades chimiques (si vous voulez les activer) ---
            // Décommentez si besoin
            /*
            grenades["Tear Gas"] = new GrenadeData(
                "Tear Gas",
                GrenadeType.Smoke,
                damage: 5,
                radius: 4,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );
            */

            // --- Charges de démolition ---
            grenades["Breaching Charge"] = new GrenadeData(
                "Breaching Charge",
                GrenadeType.HE,
                damage: 60,
                radius: 2,
                destroyWalls: true,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            grenades["C4 Explosive"] = new GrenadeData(
                "C4 Explosive",
                GrenadeType.HE,
                damage: 100,
                radius: 3,
                destroyWalls: true,
                digsTerrain: true,
                digDepth: 4,
                aoCost: 2
            );

            // --- Grenades d'entraînement ---
            grenades["Practice Grenade"] = new GrenadeData(
                "Practice Grenade",
                GrenadeType.Smoke,
                damage: 0,
                radius: 1,
                destroyWalls: false,
                digsTerrain: false,
                digDepth: 0,
                aoCost: 1
            );

            return grenades;
        }

        /// <summary>
        /// Retourne la couleur associée au type de grenade
        /// </summary>
        public static Color GetGrenadeColor(GrenadeType type)
        {
            switch (type)
            {
                case GrenadeType.Frag:
                    return new Color(80, 80, 80);       // Gris métallique
                case GrenadeType.HE:
                    return new Color(200, 100, 0);      // Orange foncé
                case GrenadeType.Plasma:
                    return new Color(0, 255, 150);      // Vert plasma
                case GrenadeType.Smoke:
                    return new Color(200, 200, 200);    // Gris clair
                case GrenadeType.Flashbang:
                    return new Color(255, 255, 200);    // Jaune clair
                case GrenadeType.Incendiary:
                    return new Color(255, 100, 0);      // Orange vif
                case GrenadeType.EMP:
                    return new Color(100, 150, 255);    // Bleu électrique
                default:
                    return Color.White;
            }
        }

        /// <summary>
        /// Retourne l'icône/symbole de la grenade (pour UI simple)
        /// </summary>
        public static string GetGrenadeSymbol(GrenadeType type)
        {
            switch (type)
            {
                case GrenadeType.Frag:
                    return "F";
                case GrenadeType.HE:
                    return "HE";
                case GrenadeType.Plasma:
                    return "P";
                case GrenadeType.Smoke:
                    return "S";
                case GrenadeType.Flashbang:
                    return "FB";
                case GrenadeType.Incendiary:
                    return "I";
                case GrenadeType.EMP:
                    return "E";
                default:
                    return "?";
            }
        }
    }
}
