using Microsoft.Xna.Framework;
using System;

namespace XCOM_3
{
    // ═══════════════════════════════════════════════════════════════════════
    // CLASSE UNIT MODIFIÉE AVEC INVENTAIRE, GRENADES ET ANIMATIONS
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

        // NOUVEAU: Orientation et animation
        public float Orientation = 0f; // Direction en radians (0 = Nord/+Z)
        public float TargetOrientation = 0f; // Orientation cible pour rotation douce

        // NOUVEAU: Animation de déplacement
        public bool IsMoving = false;
        public Vector3 VisualPosition; // Position 3D pour animation
        public Vector3 TargetPosition; // Destination de l'animation
        public float MoveProgress = 0f; // 0 à 1

        // NOUVEAU: Animation idle
        public float IdleTime = 0f;
        public float IdleBobOffset = 0f;

        // NOUVEAU: Animation de marche
        public float WalkCycleTime = 0f;
        public float LegSwing = 0f;
        public float ArmSwing = 0f;
        public float BodyBob = 0f;

        public Unit(Point cell, Team team, string name, string unitClass, string weapon, WeaponData weaponData)
        {
            Cell = cell;
            Team = team;
            Name = name;
            Class = unitClass;
            Weapon = weapon;
            WeaponData = weaponData;

            // NOUVEAU: Initialiser la position visuelle
            UpdateVisualPosition();
            TargetPosition = VisualPosition;

            // NOUVEAU: Orientation aléatoire initiale
            Random rand = new Random(name.GetHashCode());
            Orientation = (float)(rand.NextDouble() * MathHelper.TwoPi);
            TargetOrientation = Orientation;
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

            // NOUVEAU: Copie des données d'animation
            Orientation = other.Orientation;
            TargetOrientation = other.TargetOrientation;
            VisualPosition = other.VisualPosition;
            TargetPosition = other.TargetPosition;
        }

        // Calcule l'armure totale
        public int GetTotalArmor()
        {
            int total = 0;
            if (EquippedHelmet != null) total += EquippedHelmet.Data.ArmorValue;
            if (EquippedArmor != null) total += EquippedArmor.Data.ArmorValue;
            return total;
        }

        // NOUVEAU: Met à jour la position visuelle basée sur Cell
        public void UpdateVisualPosition(int cellSize = 2)
        {
            VisualPosition = new Vector3(
                Cell.X * cellSize + cellSize / 2f,
                0,
                Cell.Y * cellSize + cellSize / 2f
            );
        }

        // NOUVEAU: Démarre une animation de déplacement vers une nouvelle cellule
        public void StartMoveTo(Point newCell, int cellSize = 2)
        {
            Cell = newCell;
            IsMoving = true;
            MoveProgress = 0f;
            WalkCycleTime = 0f;

            TargetPosition = new Vector3(
                newCell.X * cellSize + cellSize / 2f,
                0,
                newCell.Y * cellSize + cellSize / 2f
            );

            // Calculer l'orientation vers la destination
            Vector3 direction = TargetPosition - VisualPosition;
            if (direction.LengthSquared() > 0.001f)
            {
                TargetOrientation = (float)Math.Atan2(direction.X, direction.Z);
            }
        }

        // NOUVEAU: Met à jour toutes les animations
        public void UpdateAnimation(float deltaTime)
        {
            // Rotation douce vers l'orientation cible
            float orientationDiff = TargetOrientation - Orientation;

            // Normaliser la différence entre -PI et PI
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

            // Animation de déplacement
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

                    // Cycle de marche
                    WalkCycleTime += deltaTime * 8f;
                    LegSwing = (float)Math.Sin(WalkCycleTime) * 0.3f;
                    ArmSwing = (float)Math.Sin(WalkCycleTime + MathHelper.Pi) * 0.2f;
                    BodyBob = Math.Abs((float)Math.Sin(WalkCycleTime * 2f)) * 0.1f;
                }
            }
            else
            {
                // Animation idle (légère oscillation)
                IdleTime += deltaTime;
                IdleBobOffset = (float)Math.Sin(IdleTime * 2f) * 0.05f;
            }
        }
    }
}
