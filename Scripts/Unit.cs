using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    // ═══════════════════════════════════════════════════════════════════════
    // CLASSE UNIT AVEC INVENTAIRE, GRENADES, ANIMATIONS ET COMPÉTENCES
    // ═══════════════════════════════════════════════════════════════════════
    public partial class Unit
    {
        public enum HumanBodyType { Masculine, Feminine }

        public Point Cell;
        public int Floor { get; set; } = 0;
        public Team Team { get; set; }
        public string Name, Weapon;
        public int ActionPoints = 2;
        public int MaxActionPoints = 2;
        public int Stamina = 100;
        public int MaxStamina = 100;
        public int MovementRange = 4; // Portée max en cases
        public int Health = 100, MaxHealth = 100;

        /// <summary>
        /// Coût en stamina pour sprinter
        /// </summary>
        public const int SPRINT_STAMINA_COST = 20;

        public WeaponData WeaponData;
        public bool IsFiring = false, WillHit = false;
        public bool IsAiming = false;
        public Point? FireTarget = null;
        public float FireProgress = 0f;
        public Unit PendingTarget = null;
        public Vector3 VisualOffset = Vector3.Zero;
        public Vector3 ChargeStart;
        public Vector3 ChargeTarget;
        public bool IsChargingForward = true;
        public string Class { get; set; }
        public HumanBodyType BodyType { get; set; } = HumanBodyType.Masculine;


        // Système d'inventaire
        public Item EquippedWeapon { get; set; }
        public Item EquippedHelmet { get; set; }
        public Item EquippedArmor { get; set; }
        public Item EquippedShield { get; set; }
        public Item EquippedShirt { get; set; }
        public string EquippedBackpack;

        // Orientation et animation
        public float Orientation = 0f;
        public float TargetOrientation = 0f;

        // Animation de déplacement
        public bool IsMoving = false;
        public Vector3 VisualPosition { get; set; }
        public Vector3 TargetPosition;
        public float MoveProgress = 0f;
        private Vector3 moveSegmentStart;
        private readonly Queue<Vector3> movementWaypoints = new Queue<Vector3>();
        private float? finalMoveOrientation;

        // Animation idle
        public float IdleTime = 0f;
        public float IdleBobOffset = 0f;

        public enum MovementGait { Jog, Run, Sprint }

        // Animation de marche
        public float WalkCycleTime = 0f;
        public float LegSwing = 0f;
        public float ArmSwing = 0f;
        public float BodyBob = 0f;
        public MovementGait CurrentMovementGait = MovementGait.Jog;

        private int jogRangeCells;
        private int runRangeCells;
        private int sprintRangeCells;

        // Système de compétences et progression
        public UnitSkills Skills = new UnitSkills();
        private Point? lastPosition = null;

        // NOUVELLES PROPRIÉTÉS DE COUVERTURE
        public CoverType CoverType { get; set; } = CoverType.None;
        public CoverDirection CoverDirections { get; set; } = CoverDirection.None;
        public bool IsCrouched { get; set; } = false;
        public float CoverTransitionProgress { get; set; } = 0f;

        public Unit(Point cell, Team team, string name, string unitClass, string weapon, WeaponData weaponData)
        {
            Cell = cell;
            Floor = 0;
            Team = team;
            Name = name;
            Class = unitClass;
            Weapon = weapon;
            WeaponData = weaponData;

            BodyType = DetermineBodyType(team, unitClass, name);

            ActionPoints = 2;
            MaxActionPoints = 2;
            Stamina = 100;
            MaxStamina = 100;
            MovementRange = 4;

            InitializeMovementProfile();


            UpdateVisualPosition();
            TargetPosition = VisualPosition;

            Random rand = new Random(name.GetHashCode());
            Orientation = (float)(rand.NextDouble() * MathHelper.TwoPi);
            TargetOrientation = Orientation;

            lastPosition = cell;

            EquippedWeapon = weaponData != null
                ? new Item(new ItemData(weapon, ItemType.Weapon, weaponData), Point.Zero)
                : null;
            EquippedHelmet = null;
            EquippedArmor = null;
            EquippedShield = null;
            EquippedShirt = null;
            EquippedBackpack = "Medium Backpack"; // ← AJOUTER CETTE LIGNE (sac par défaut)

        }

        public Unit(Unit other)
        {
            Cell = other.Cell;
            Floor = other.Floor;
            Team = other.Team;
            Name = other.Name;
            Class = other.Class;
            BodyType = other.BodyType;
            Weapon = other.Weapon;
            WeaponData = other.WeaponData;
            ActionPoints = other.ActionPoints;
            MaxActionPoints = other.MaxActionPoints;
            Stamina = other.Stamina;
            MaxStamina = other.MaxStamina;
            MovementRange = other.MovementRange;
            jogRangeCells = other.jogRangeCells;
            runRangeCells = other.runRangeCells;
            sprintRangeCells = other.sprintRangeCells;
            Health = other.Health;
            MaxHealth = other.MaxHealth;

            EquippedWeapon = other.EquippedWeapon;
            EquippedHelmet = other.EquippedHelmet;
            EquippedArmor = other.EquippedArmor;
            EquippedShield = other.EquippedShield;  // NOUVEAU
            EquippedShirt = other.EquippedShirt;    // NOUVEAU
            EquippedBackpack = other.EquippedBackpack; // ← AJOUTER CETTE LIGNE

            Grenades = new System.Collections.Generic.List<GrenadeData>(other.Grenades);
            MaxGrenades = other.MaxGrenades;

            Orientation = other.Orientation;
            TargetOrientation = other.TargetOrientation;
            VisualPosition = other.VisualPosition;
            TargetPosition = other.TargetPosition;

            Skills = new UnitSkills(other.Skills);
            lastPosition = other.lastPosition;

            CoverType = other.CoverType;
            CoverDirections = other.CoverDirections;
            IsCrouched = other.IsCrouched;
            CoverTransitionProgress = other.CoverTransitionProgress;

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

        private static HumanBodyType DetermineBodyType(Team team, string unitClass, string name)
        {
            bool isHuman = team == Team.Player ||
                           string.Equals(unitClass, "Assault", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(unitClass, "Heavy", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(unitClass, "Scout", StringComparison.OrdinalIgnoreCase);

            if (!isHuman)
                return HumanBodyType.Masculine;

            int hash = string.IsNullOrWhiteSpace(name) ? 0 : name.GetHashCode() & int.MaxValue;
            return hash % 2 == 0 ? HumanBodyType.Feminine : HumanBodyType.Masculine;
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
            return GetMaxMoveRange(); // Utilise la nouvelle méthode
        }

        public void UpdateVisualPosition(int cellSize = 2)
        {
            VisualPosition = new Vector3(
                Cell.X * cellSize + cellSize / 2f,
                Floor * cellSize,
                Cell.Y * cellSize + cellSize / 2f
            );
        }

        public void StartMoveTo(Point newCell, int cellSize = 2)
        {
            StartMoveAlongPath(new List<Point> { newCell }, cellSize);
        }

        public void StartMoveAlongPath(List<Point> path, int cellSize = 2)
        {
            if (path == null || path.Count == 0)
                return;

            Point newCell = path[path.Count - 1];

            if (lastPosition.HasValue && Team == Team.Player)
            {
                Skills.GainMovementXP(path.Count);
            }

            lastPosition = Cell;
            Cell = newCell;
            IsMoving = true;
            MoveProgress = 0f;
            WalkCycleTime = 0f;

            movementWaypoints.Clear();
            foreach (Point cell in path)
            {
                movementWaypoints.Enqueue(new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0,
                    cell.Y * cellSize + cellSize / 2f
                ));
            }

            BeginNextMoveSegment();
        }

        private void BeginNextMoveSegment()
        {
            if (movementWaypoints.Count == 0)
                return;

            moveSegmentStart = VisualPosition;
            TargetPosition = movementWaypoints.Dequeue();

            Vector3 direction = TargetPosition - VisualPosition;
            if (direction.LengthSquared() > 0.001f)
            {
                TargetOrientation = (float)Math.Atan2(direction.X, direction.Z);
                finalMoveOrientation = TargetOrientation;
            }
        }


        public void SetMovementStyle(int actionPointCost, bool isSprint = false)
        {
            if (isSprint)
            {
                CurrentMovementGait = MovementGait.Sprint;
                return;
            }

            CurrentMovementGait = actionPointCost <= 1 ? MovementGait.Jog : MovementGait.Run;
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
                float moveSpeedMultiplier;
                float cycleSpeed;
                float legAmplitude;
                float armAmplitude;
                float bobAmplitude;
                float armDriveBias;
                float armPhaseOffset;

                switch (CurrentMovementGait)
                {
                    case MovementGait.Sprint:
                        // Sprint: cadence et extension max, bras très dynamiques
                        moveSpeedMultiplier = 4.15f;
                        cycleSpeed = 10.8f;
                        legAmplitude = 0.46f;
                        armAmplitude = 0.38f;
                        bobAmplitude = 0.135f;
                        armDriveBias = 0.06f;
                        armPhaseOffset = 0.22f;
                        break;

                    case MovementGait.Run:
                        // Run: allure intermédiaire, énergie marquée mais contrôlée
                        moveSpeedMultiplier = 3.45f;
                        cycleSpeed = 8.25f;
                        legAmplitude = 0.35f;
                        armAmplitude = 0.27f;
                        bobAmplitude = 0.095f;
                        armDriveBias = 0.035f;
                        armPhaseOffset = 0.14f;
                        break;

                    default:
                        // Jog: foulée plus courte, cadence stable, rebond léger
                        moveSpeedMultiplier = 2.8f;
                        cycleSpeed = 6.4f;
                        legAmplitude = 0.24f;
                        armAmplitude = 0.19f;
                        bobAmplitude = 0.072f;
                        armDriveBias = 0.015f;
                        armPhaseOffset = 0.08f;
                        break;
                }

                MoveProgress += moveSpeedMultiplier * deltaTime;

                if (MoveProgress >= 1f)
                {
                    VisualPosition = TargetPosition;

                    if (movementWaypoints.Count > 0)
                    {
                        MoveProgress = 0f;
                        BeginNextMoveSegment();
                    }
                    else
                    {
                        MoveProgress = 1f;
                        IsMoving = false;

                        if (finalMoveOrientation.HasValue)
                            TargetOrientation = finalMoveOrientation.Value;

                        finalMoveOrientation = null;
                        WalkCycleTime = 0f;
                        LegSwing = 0f;
                        ArmSwing = 0f;
                        BodyBob = 0f;
                    }
                }
                else
                {
                    VisualPosition = Vector3.Lerp(moveSegmentStart, TargetPosition, MoveProgress);
                    WalkCycleTime += deltaTime * cycleSpeed;
                    float gaitSin = (float)Math.Sin(WalkCycleTime);
                    float gaitHarmonic = (float)Math.Sin(WalkCycleTime * 2f);

                    LegSwing = gaitSin * legAmplitude;
                    ArmSwing = (float)Math.Sin(WalkCycleTime + MathHelper.Pi + armPhaseOffset) * armAmplitude +
                               gaitHarmonic * armDriveBias;
                    BodyBob = Math.Abs(gaitHarmonic) * bobAmplitude;
                }
            }
            else
            {
                IdleTime += deltaTime;
                IdleBobOffset = (float)Math.Sin(IdleTime * 2f) * 0.05f;
            }

            UpdateCoverTransition(deltaTime);
        }

        public void EnterCover(CoverData coverData)
        {
            if (coverData.Type == CoverType.None)
            {
                Console.WriteLine($"[COVER] {Name} tried to take cover but none available");
                return;
            }

            CoverType = coverData.Type;
            CoverDirections = coverData.Directions;
            IsCrouched = true;
            CoverTransitionProgress = 0f;

            Console.WriteLine($"[COVER] {Name} takes {coverData.Type} cover (+{coverData.DefenseBonus}% defense)");
        }

        public void ExitCover()
        {
            if (CoverType == CoverType.None)
                return;

            Console.WriteLine($"[COVER] {Name} leaves {CoverType} cover");

            CoverType = CoverType.None;
            CoverDirections = CoverDirection.None;
            IsCrouched = false;
            CoverTransitionProgress = 0f;
        }

        public void UpdateCoverTransition(float deltaTime)
        {
            if (IsCrouched && CoverTransitionProgress < 1f)
            {
                CoverTransitionProgress += deltaTime * 3f;
                if (CoverTransitionProgress > 1f)
                    CoverTransitionProgress = 1f;
            }
            else if (!IsCrouched && CoverTransitionProgress > 0f)
            {
                CoverTransitionProgress -= deltaTime * 3f;
                if (CoverTransitionProgress < 0f)
                    CoverTransitionProgress = 0f;
            }
        }

        public int GetCoverDefenseBonus()
        {
            return CoverType switch
            {
                CoverType.Half => CoverSystem.HALF_COVER_BONUS,
                CoverType.Full => CoverSystem.FULL_COVER_BONUS,
                _ => 0
            };
        }

        public bool HasCoverFrom(CoverDirection direction)
        {
            return (CoverDirections & direction) != 0;
        }

        /// <summary>
        /// Obtient la portée de mouvement court (1 AP)
        /// </summary>
        public int GetShortMoveRange()
        {
            return GetShortMoveRange(0f);
        }

        /// <summary>
        /// Obtient la portée de mouvement court (1 AP), ajustée selon la charge portée.
        /// </summary>
        public int GetShortMoveRange(float carriedWeightLbs)
        {
            int baseRange = jogRangeCells + Skills.GetMovementBonus();
            int penalty = GetMobilityPenalty() + GetCarriedWeightMobilityPenaltyCells(carriedWeightLbs, baseRange);
            return Math.Max(1, baseRange - penalty);
        }

        /// <summary>
        /// Obtient la portée de mouvement maximale (2 AP)
        /// </summary>
        public int GetMaxMoveRange()
        {
            return GetMaxMoveRange(0f);
        }

        /// <summary>
        /// Obtient la portée de mouvement maximale (2 AP), ajustée selon la charge portée.
        /// </summary>
        public int GetMaxMoveRange(float carriedWeightLbs)
        {
            int baseRange = runRangeCells + Skills.GetMovementBonus();
            int penalty = GetMobilityPenalty() + GetCarriedWeightMobilityPenaltyCells(carriedWeightLbs, baseRange);
            return Math.Max(1, baseRange - penalty);
        }

        /// <summary>
        /// Obtient la portée de sprint (2 AP + stamina)
        /// </summary>
        public int GetSprintRange()
        {
            return GetSprintRange(0f);
        }

        /// <summary>
        /// Obtient la portée de sprint (2 AP + stamina), ajustée selon la charge portée.
        /// </summary>
        public int GetSprintRange(float carriedWeightLbs)
        {
            int baseRange = sprintRangeCells + Skills.GetMovementBonus();
            int penalty = GetMobilityPenalty() + GetCarriedWeightMobilityPenaltyCells(carriedWeightLbs, baseRange);
            return Math.Max(GetMaxMoveRange(carriedWeightLbs), baseRange - penalty);
        }

        /// <summary>
        /// Convertit le poids porté en pénalité de mobilité (en cases) via une règle empirique.
        ///
        /// Règle utilisée (effort court ~6s) :
        /// - 0.75% de perte de vitesse par 1% de poids corporel ajouté.
        /// - Cap à 42% de perte max pour éviter un effondrement irréaliste sur un sprint court.
        /// </summary>
        public int GetCarriedWeightMobilityPenaltyCells(float carriedWeightLbs, int baseRangeCells)
        {
            if (carriedWeightLbs <= 0f || baseRangeCells <= 1)
                return 0;

            const float speedPenaltyPerBodyWeightPercent = 0.75f;
            const float maxSpeedPenaltyPercent = 42f;
            const float feetPerCell = 5f;

            float referenceBodyWeight = GetReferenceBodyWeightLbs();
            float carriedPercent = (carriedWeightLbs / referenceBodyWeight) * 100f;
            float speedPenaltyPercent = MathF.Min(maxSpeedPenaltyPercent, carriedPercent * speedPenaltyPerBodyWeightPercent);

            float baseDistanceFeet = baseRangeCells * feetPerCell;
            float adjustedDistanceFeet = baseDistanceFeet * (1f - speedPenaltyPercent / 100f);

            int adjustedRangeCells = Math.Max(1, (int)MathF.Round(adjustedDistanceFeet / feetPerCell));
            return Math.Max(0, baseRangeCells - adjustedRangeCells);
        }

        private float GetReferenceBodyWeightLbs()
        {
            return BodyType == HumanBodyType.Feminine ? 150f : 180f;
        }

        private void InitializeMovementProfile()
        {
            const int feetPerCell = 5;

            // Même indice de condition physique pour maintenir une progression logique
            // (un jog faible donne aussi un run/sprint plutôt faibles).
            float fitnessIndex = CreateNormalizedSeed(11);
            float gaitVariation = CreateSignedSeed(29) * 0.08f;

            float runIndex = MathHelper.Clamp(fitnessIndex + gaitVariation, 0f, 1f);
            float sprintIndex = MathHelper.Clamp(fitnessIndex + gaitVariation * 1.35f, 0f, 1f);

            if (BodyType == HumanBodyType.Feminine)
            {
                jogRangeCells = InterpolateFeetRangeToCells(53, 70, fitnessIndex, feetPerCell);
                runRangeCells = InterpolateFeetRangeToCells(85, 100, runIndex, feetPerCell);
                sprintRangeCells = InterpolateFeetRangeToCells(115, 140, sprintIndex, feetPerCell);
            }
            else
            {
                jogRangeCells = InterpolateFeetRangeToCells(53, 70, fitnessIndex, feetPerCell);
                runRangeCells = InterpolateFeetRangeToCells(95, 105, runIndex, feetPerCell);
                sprintRangeCells = InterpolateFeetRangeToCells(130, 155, sprintIndex, feetPerCell);
            }

            MovementRange = runRangeCells;
        }

        private int InterpolateFeetRangeToCells(int minFeet, int maxFeet, float t, int feetPerCell)
        {
            float feet = MathHelper.Lerp(minFeet, maxFeet, t);
            return Math.Max(1, (int)MathF.Round(feet / feetPerCell));
        }

        private float CreateNormalizedSeed(int salt)
        {
            int baseHash = string.IsNullOrWhiteSpace(Name) ? 0 : Name.GetHashCode();
            int seed = HashCode.Combine(baseHash, (int)BodyType, salt) & int.MaxValue;
            return seed / (float)int.MaxValue;
        }

        private float CreateSignedSeed(int salt)
        {
            return (CreateNormalizedSeed(salt) * 2f) - 1f;
        }

        /// <summary>
        /// Vérifie si l'unité peut sprinter
        /// </summary>
        public bool CanSprint()
        {
            return Stamina >= SPRINT_STAMINA_COST && ActionPoints >= 2;
        }

        /// <summary>
        /// Consomme la stamina pour un sprint
        /// </summary>
        public void ConsumeSprint()
        {
            Stamina = Math.Max(0, Stamina - SPRINT_STAMINA_COST);
            Console.WriteLine($"[UNIT] {Name} sprints! Stamina: {Stamina}/{MaxStamina}");
        }

        /// <summary>
        /// Régénère la stamina (appelé chaque tour)
        /// </summary>
        public void RegenerateStamina()
        {
            int regenAmount = 10; // Régénère 10 stamina par tour
            Stamina = Math.Min(MaxStamina, Stamina + regenAmount);
        }

        /// <summary>
        /// Obtient le coût en AP d'un déplacement
        /// </summary>
        public int GetMovementAPCost(int distance)
        {
            int shortRange = GetShortMoveRange();
            int maxRange = GetMaxMoveRange();

            if (distance <= shortRange)
                return 1; // Mouvement court
            else if (distance <= maxRange)
                return 2; // Mouvement complet
            else
                return 2; // Sprint (+ stamina)
        }

        /// <summary>
        /// Vérifie si un mouvement est un sprint
        /// </summary>
        public bool IsSprint(int distance)
        {
            return distance > GetMaxMoveRange();
        }


    }
}
