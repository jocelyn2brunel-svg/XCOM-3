using Microsoft.Xna.Framework;

namespace XCOM_3
{
    // ═══════════════════════════════════════════════════════════════════════
    // CLASSE UNIT MODIFIÉE AVEC INVENTAIRE ET GRENADES
    // IMPORTANT: Ajout du mot-clé "partial" pour supporter les grenades
    // ═══════════════════════════════════════════════════════════════════════
    public partial class Unit
    {
        public Point Cell;
        public Team Team;
        public string Name, Class, Weapon;
        public int ActionPoints = 3, MovementPoints = 3, Health = 100, MaxHealth = 100;
        public WeaponData WeaponData;
        public bool IsFiring = false, WillHit = false;
        public Point? FireTarget = null;
        public float FireProgress = 0f;
        public Unit PendingTarget = null;
        public Vector3 VisualOffset = Vector3.Zero;
        public Vector3 ChargeStart;
        public Vector3 ChargeTarget;
        public bool IsChargingForward = true;

        // Système d'inventaire
        public Item EquippedWeapon = null;
        public Item EquippedHelmet = null;
        public Item EquippedArmor = null;

        public Unit(Point cell, Team team, string name, string unitClass, string weapon, WeaponData weaponData)
        {
            Cell = cell;
            Team = team;
            Name = name;
            Class = unitClass;
            Weapon = weapon;
            WeaponData = weaponData;
        }

        public Unit(Unit other)
        {
            Cell = other.Cell;
            Team = other.Team;
            Name = other.Name;
            Class = other.Class;
            Weapon = other.Weapon;
            WeaponData = other.WeaponData;
            ActionPoints = other.ActionPoints;
            Health = other.Health;
            MaxHealth = other.MaxHealth;

            // Copie les items équipés
            EquippedWeapon = other.EquippedWeapon;
            EquippedHelmet = other.EquippedHelmet;
            EquippedArmor = other.EquippedArmor;

            // Copie les grenades
            Grenades = new System.Collections.Generic.List<GrenadeData>(other.Grenades);
            MaxGrenades = other.MaxGrenades;
        }

        // Calcule l'armure totale
        public int GetTotalArmor()
        {
            int total = 0;
            if (EquippedHelmet != null) total += EquippedHelmet.Data.ArmorValue;
            if (EquippedArmor != null) total += EquippedArmor.Data.ArmorValue;
            return total;
        }
    }
}
