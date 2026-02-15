using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace XCOM_3
{
    public static class GrenadeDatabase
    {
        public static Dictionary<string, GrenadeData> GetAllGrenades()
        {
            var grenades = new Dictionary<string, GrenadeData>();

            // Garder uniquement la MK 2
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
