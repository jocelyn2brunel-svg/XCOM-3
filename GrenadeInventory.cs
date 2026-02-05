using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Extension de la classe Unit pour supporter les grenades
    /// </summary>
    public partial class Unit
    {
        // Inventaire de grenades (à ajouter à la classe Unit existante)
        public List<GrenadeData> Grenades { get; set; } = new List<GrenadeData>();
        public int MaxGrenades { get; set; } = 3;

        /// <summary>
        /// Ajoute une grenade à l'inventaire si possible
        /// </summary>
        public bool AddGrenade(GrenadeData grenade)
        {
            if (Grenades.Count < MaxGrenades)
            {
                Grenades.Add(grenade);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Retire une grenade de l'inventaire
        /// </summary>
        public bool RemoveGrenade(GrenadeData grenade)
        {
            return Grenades.Remove(grenade);
        }

        /// <summary>
        /// Vérifie si l'unité a assez de PA pour lancer une grenade
        /// </summary>
        public bool CanThrowGrenade(GrenadeData grenade)
        {
            return ActionPoints >= grenade.AOCost && Grenades.Contains(grenade);
        }
    }

    /// <summary>
    /// Item de grenade pour l'inventaire général
    /// </summary>
    public class GrenadeItem
    {
        public GrenadeData Data;
        public Point Position;  // Position dans l'inventaire UI
        public Rectangle Bounds => new Rectangle(Position.X, Position.Y, 50, 50);

        public GrenadeItem(GrenadeData data, Point position)
        {
            Data = data;
            Position = position;
        }
    }

    /// <summary>
    /// Initialisation des types de grenades
    /// </summary>
    public static class GrenadeDatabase
    {
        public static Dictionary<string, GrenadeData> GetAllGrenades()
        {
            return new Dictionary<string, GrenadeData>
            {
                ["Frag Grenade"] = new GrenadeData(
                    "Frag Grenade",
                    GrenadeType.Frag,
                    damage: 40,
                    radius: 2,
                    destroyWalls: false,
                    digsTerrain: false,
                    digDepth: 0,
                    aoCost: 1
                ),

                ["HE Grenade"] = new GrenadeData(
                    "HE Grenade",
                    GrenadeType.HE,
                    damage: 50,
                    radius: 3,
                    destroyWalls: true,
                    digsTerrain: true,
                    digDepth: 1,
                    aoCost: 1
                ),

                ["Plasma Grenade"] = new GrenadeData(
                    "Plasma Grenade",
                    GrenadeType.Plasma,
                    damage: 70,
                    radius: 2,
                    destroyWalls: true,
                    digsTerrain: false,
                    digDepth: 0,
                    aoCost: 1
                ),

                ["Demolition Charge"] = new GrenadeData(
                    "Demolition Charge",
                    GrenadeType.HE,
                    damage: 80,
                    radius: 4,
                    destroyWalls: true,
                    digsTerrain: true,
                    digDepth: 3,
                    aoCost: 2
                ),

                ["Smoke Grenade"] = new GrenadeData(
                    "Smoke Grenade",
                    GrenadeType.Smoke,
                    damage: 0,
                    radius: 3,
                    destroyWalls: false,
                    digsTerrain: false,
                    digDepth: 0,
                    aoCost: 1
                ),

                ["Flashbang"] = new GrenadeData(
                    "Flashbang",
                    GrenadeType.Flashbang,
                    damage: 5,
                    radius: 2,
                    destroyWalls: false,
                    digsTerrain: false,
                    digDepth: 0,
                    aoCost: 1
                ),

                ["Incendiary Grenade"] = new GrenadeData(
                    "Incendiary Grenade",
                    GrenadeType.Incendiary,
                    damage: 30,
                    radius: 2,
                    destroyWalls: false,
                    digsTerrain: false,
                    digDepth: 0,
                    aoCost: 1
                ),

                ["EMP Grenade"] = new GrenadeData(
                    "EMP Grenade",
                    GrenadeType.EMP,
                    damage: 20,
                    radius: 3,
                    destroyWalls: false,
                    digsTerrain: false,
                    digDepth: 0,
                    aoCost: 1
                )
            };
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
