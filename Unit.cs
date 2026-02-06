using Microsoft.Xna.Framework;
using System;

namespace XCOM_3
{
    // ═══════════════════════════════════════════════════════════════════════
    // CLASSE UNIT AVEC INVENTAIRE, GRENADES, ANIMATIONS ET COMPÉTENCES
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
        public Item EquippedShield = null;  // Nouveau slot pour bouclier
        public Item EquippedShirt = null;   // Nouveau slot pour chemise tactique


        // Orientation et animation
        public float Orientation = 0f;
        public float TargetOrientation = 0f;

        // Animation de déplacement
        public bool IsMoving = false;
        public Vector3 VisualPosition;
        public Vector3 TargetPosition;
        public float MoveProgress = 0f;

        // Animation idle
        public float IdleTime = 0f;
        public float IdleBobOffset = 0f;

        // Animation de marche
        public float WalkCycleTime = 0f;
        public float LegSwing = 0f;
        public float ArmSwing = 0f;
        public float BodyBob = 0f;

        // Système de compétences et progression
        public UnitSkills Skills = new UnitSkills();
        private Point? lastPosition = null;

        public Unit(Point cell, Team team, string name, string unitClass, string weapon, WeaponData weaponData)
        {
            Cell = cell;
            Team = team;
            Name = name;
            Class = unitClass;
            Weapon = weapon;
            WeaponData = weaponData;

            UpdateVisualPosition();
            TargetPosition = VisualPosition;

            Random rand = new Random(name.GetHashCode());
            Orientation = (float)(rand.NextDouble() * MathHelper.TwoPi);
            TargetOrientation = Orientation;

            lastPosition = cell;
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

            EquippedWeapon = other.EquippedWeapon;
            EquippedHelmet = other.EquippedHelmet;
            EquippedArmor = other.EquippedArmor;
            EquippedShield = other.EquippedShield;  // NOUVEAU
            EquippedShirt = other.EquippedShirt;    // NOUVEAU

            Grenades = new System.Collections.Generic.List<GrenadeData>(other.Grenades);
            MaxGrenades = other.MaxGrenades;

            Orientation = other.Orientation;
            TargetOrientation = other.TargetOrientation;
            VisualPosition = other.VisualPosition;
            TargetPosition = other.TargetPosition;

            Skills = new UnitSkills(other.Skills);
            lastPosition = other.lastPosition;
        }

        public int GetTotalArmor()
        {
            int total = 0;
            if (EquippedHelmet != null) total += EquippedHelmet.Data.ArmorValue;
            if (EquippedArmor != null) total += EquippedArmor.Data.ArmorValue;
            if (EquippedShield != null) total += EquippedShield.Data.ArmorValue; // NOUVEAU
            if (EquippedShirt != null) total += EquippedShirt.Data.ArmorValue;   // NOUVEAU
            total += Skills.GetDefenseBonus();
            return total;
        }

        public int GetMobilityPenalty()
        {
            int penalty = 0;
            if (EquippedArmor != null) penalty += EquippedArmor.Data.MobilityPenalty;
            if (EquippedShield != null) penalty += EquippedShield.Data.MobilityPenalty;
            return penalty;
        }

        public int GetMaxHealth()
        {
            return MaxHealth + Skills.GetHealthBonus();
        }

        public int GetMaxMovementPoints()
        {
            int baseMovement = MovementPoints + Skills.GetMovementBonus();
            int penalty = GetMobilityPenalty();
            return Math.Max(1, baseMovement - penalty); // Minimum 1 PM
        }

        public void UpdateVisualPosition(int cellSize = 2)
        {
            VisualPosition = new Vector3(
                Cell.X * cellSize + cellSize / 2f,
                0,
                Cell.Y * cellSize + cellSize / 2f
            );
        }

        public void StartMoveTo(Point newCell, int cellSize = 2)
        {
            if (lastPosition.HasValue && Team == Team.Player)
            {
                int distance = Math.Abs(newCell.X - lastPosition.Value.X) +
                              Math.Abs(newCell.Y - lastPosition.Value.Y);
                Skills.GainMovementXP(distance);
            }

            lastPosition = Cell;
            Cell = newCell;
            IsMoving = true;
            MoveProgress = 0f;
            WalkCycleTime = 0f;

            TargetPosition = new Vector3(
                newCell.X * cellSize + cellSize / 2f,
                0,
                newCell.Y * cellSize + cellSize / 2f
            );

            Vector3 direction = TargetPosition - VisualPosition;
            if (direction.LengthSquared() > 0.001f)
            {
                TargetOrientation = (float)Math.Atan2(direction.X, direction.Z);
            }
        }

        public void UpdateAnimation(float deltaTime)
        {
            float orientationDiff = TargetOrientation - Orientation;

            while (orientationDiff > MathHelper.Pi) orientationDiff -= MathHelper.TwoPi;
            while (orientationDiff < -MathHelper.Pi) orientationDiff += MathHelper.TwoPi;

            float rotationSpeed = 8f * deltaTime;
            if (Math.Abs(orientationDiff) < rotationSpeed)
            {
                Orientation = TargetOrientation;
            }
            else
            {
                Orientation += Math.Sign(orientationDiff) * rotationSpeed;
            }

            if (IsMoving)
            {
                float moveSpeed = 3f * deltaTime;
                MoveProgress += moveSpeed;

                if (MoveProgress >= 1f)
                {
                    MoveProgress = 1f;
                    VisualPosition = TargetPosition;
                    IsMoving = false;
                    WalkCycleTime = 0f;
                    LegSwing = 0f;
                    ArmSwing = 0f;
                    BodyBob = 0f;
                }
                else
                {
                    VisualPosition = Vector3.Lerp(VisualPosition, TargetPosition, MoveProgress);

                    WalkCycleTime += deltaTime * 8f;
                    LegSwing = (float)Math.Sin(WalkCycleTime) * 0.3f;
                    ArmSwing = (float)Math.Sin(WalkCycleTime + MathHelper.Pi) * 0.2f;
                    BodyBob = Math.Abs((float)Math.Sin(WalkCycleTime * 2f)) * 0.1f;
                }
            }
            else
            {
                IdleTime += deltaTime;
                IdleBobOffset = (float)Math.Sin(IdleTime * 2f) * 0.05f;
            }
        }
    }
}
