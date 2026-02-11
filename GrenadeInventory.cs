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
}
