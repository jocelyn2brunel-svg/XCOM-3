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
        private static readonly HashSet<string> HumanClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assault",
            "Heavy",
            "Scout"
        };

        private static readonly HashSet<string> KnownFeminineNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Nadia", "Maya", "Elena", "Sofia", "Leila", "Iris"
        };

        private static readonly HashSet<string> KnownMasculineNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Alex", "Victor", "Jonas", "Marco", "Ethan", "Hugo"
        };

        public enum HumanBodyType { Masculine, Feminine }
        public enum Handedness { Right, Left }
        public enum EyeColorOption { Brown, Blue, Green }

        public Point Cell;
        public int Floor { get; set; } = 0;
        public Team Team { get; set; }
        public string Name, Weapon;
        public int ActionPoints = 2;
        public int MaxActionPoints = 2;
        public int Phosphocreatine = 100;
        public int MaxPhosphocreatine = 100;
        public int MovementRange = 4; // Portée max en cases
        public int Health = 100, MaxHealth = 100;
        public const int FeetPerCell = 5;
        public const int DefaultHumanPerceptionFeet = 120;
        public int PerceptionRangeCells { get; set; } = DefaultHumanPerceptionFeet / FeetPerCell;
        public int PerceptionRangeFeet => PerceptionRangeCells * FeetPerCell;
        public bool IsSpottedByPlayerTeam { get; set; } = false;

        private static readonly int[] phosphocreatineRegenByRound = { 18, 15, 12, 10, 8, 7, 6, 5, 4, 3 };
        private int phosphocreatineRegenRound = 0;

        public WeaponData WeaponData;

        public int CurrentAmmoInMagazine { get; private set; } = 0;
        private string ammoTrackedWeaponName = string.Empty;
        public bool IsFiring = false, WillHit = false;
        public bool IsAiming = false;
        public Point? FireTarget = null;
        public float FireProgress = 0f;
        public int FireRoundsToAnimate = 1;
        public int FireActionPointsSpent = 1;
        public float FireAnimationDurationSeconds = 0.25f;
        public Unit PendingTarget = null;
        public bool IsOnOverwatch = false;
        public int OverwatchShotsRemaining = 0;
        public float OverwatchCooldownRemainingSeconds = 0f;
        public Unit LastOverwatchTarget = null;
        public Vector3 VisualOffset = Vector3.Zero;
        public Vector3 ChargeStart;
        public Vector3 ChargeTarget;
        public bool IsChargingForward = true;
        public string Class { get; set; }
        public HumanBodyType BodyType { get; set; } = HumanBodyType.Masculine;
        public Handedness DominantHand { get; set; } = Handedness.Right;
        public EyeColorOption EyeColor { get; set; } = EyeColorOption.Brown;


        // Système d'inventaire
        public Item EquippedWeapon { get; set; }
        public Item EquippedHelmet { get; set; }
        public Item EquippedNeck { get; set; }
        public Item EquippedArmor { get; set; }
        public Item EquippedShield { get; set; }
        public Item EquippedAccessory { get; set; }
        public Item EquippedRightHandFlashlight { get; set; }
        public Item EquippedLeftHandFlashlight { get; set; }
        public bool IsRightHandFlashlightOn { get; set; } = false;
        public bool IsLeftHandFlashlightOn { get; set; } = false;
        public Item EquippedShirt { get; set; }
        public Item EquippedPants { get; set; }
        public Item EquippedKnees { get; set; }
        public Item EquippedFeet { get; set; }
        public Item EquippedChestRig { get; set; }
        public Item EquippedBelt { get; set; }
        public List<Item> PantsInventory { get; set; } = new List<Item>();
        public List<Item> ChestRigInventory { get; set; } = new List<Item>();
        public const int BackpackGridColumns = 4;
        private InventoryGrid backpackInventoryGrid;
        public InventoryGrid BackpackInventory => backpackInventoryGrid;
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
            DominantHand = DetermineHandedness(team, unitClass, name, BodyType);
            EyeColor = DetermineEyeColor(name);

            ActionPoints = 2;
            MaxActionPoints = 2;
            Phosphocreatine = 100;
            MaxPhosphocreatine = 100;
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
            EquippedNeck = null;
            EquippedArmor = null;
            EquippedShield = null;
            EquippedAccessory = null;
            EquippedRightHandFlashlight = null;
            EquippedLeftHandFlashlight = null;
            IsRightHandFlashlightOn = false;
            IsLeftHandFlashlightOn = false;
            EquippedShirt = null;
            EquippedPants = null;
            EquippedKnees = null;
            EquippedFeet = null;
            EquippedChestRig = null;
            EquippedBelt = null;
            PantsInventory = new List<Item>();
            ChestRigInventory = new List<Item>();
            EquippedBackpack = "Backpack XL";
            EnsureBackpackInventoryGrid();
            EnsureAmmoState();

        }

        private static EyeColorOption DetermineEyeColor(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return EyeColorOption.Brown;

            int hash = Math.Abs(name.GetHashCode());
            return (EyeColorOption)(hash % 3);
        }

        public static EyeColorOption ParseEyeColor(string eyeColor)
        {
            if (string.IsNullOrWhiteSpace(eyeColor))
                return EyeColorOption.Brown;

            if (eyeColor.Equals("Blue", StringComparison.OrdinalIgnoreCase))
                return EyeColorOption.Blue;
            if (eyeColor.Equals("Green", StringComparison.OrdinalIgnoreCase))
                return EyeColorOption.Green;

            return EyeColorOption.Brown;
        }

        public Unit(Unit other)
        {
            Cell = other.Cell;
            Floor = other.Floor;
            Team = other.Team;
            Name = other.Name;
            Class = other.Class;
            BodyType = other.BodyType;
            DominantHand = other.DominantHand;
            EyeColor = other.EyeColor;
            Weapon = other.Weapon;
            WeaponData = other.WeaponData;
            CurrentAmmoInMagazine = other.CurrentAmmoInMagazine;
            ammoTrackedWeaponName = other.ammoTrackedWeaponName;
            ActionPoints = other.ActionPoints;
            MaxActionPoints = other.MaxActionPoints;
            Phosphocreatine = other.Phosphocreatine;
            MaxPhosphocreatine = other.MaxPhosphocreatine;
            MovementRange = other.MovementRange;
            jogRangeCells = other.jogRangeCells;
            runRangeCells = other.runRangeCells;
            sprintRangeCells = other.sprintRangeCells;
            phosphocreatineRegenRound = other.phosphocreatineRegenRound;
            Health = other.Health;
            MaxHealth = other.MaxHealth;
            PerceptionRangeCells = other.PerceptionRangeCells;
            IsSpottedByPlayerTeam = other.IsSpottedByPlayerTeam;

            EquippedWeapon = other.EquippedWeapon;
            EquippedHelmet = other.EquippedHelmet;
            EquippedNeck = other.EquippedNeck;
            EquippedArmor = other.EquippedArmor;
            EquippedShield = other.EquippedShield;  // NOUVEAU
            EquippedAccessory = other.EquippedAccessory;
            EquippedRightHandFlashlight = other.EquippedRightHandFlashlight;
            EquippedLeftHandFlashlight = other.EquippedLeftHandFlashlight;
            IsRightHandFlashlightOn = other.IsRightHandFlashlightOn;
            IsLeftHandFlashlightOn = other.IsLeftHandFlashlightOn;
            EquippedShirt = other.EquippedShirt;    // NOUVEAU
            EquippedPants = other.EquippedPants;
            EquippedKnees = other.EquippedKnees;
            EquippedFeet = other.EquippedFeet;
            EquippedChestRig = other.EquippedChestRig;
            EquippedBelt = other.EquippedBelt;
            PantsInventory = new List<Item>(other.PantsInventory);
            ChestRigInventory = new List<Item>(other.ChestRigInventory);
            EquippedBackpack = other.EquippedBackpack; // ← AJOUTER CETTE LIGNE
            EnsureBackpackInventoryGrid();
            if (other.BackpackInventory != null)
            {
                foreach (var backpackItem in other.BackpackInventory.GetAllItems())
                {
                    BackpackInventory.PlaceItem(new GridItem(
                        backpackItem.Data,
                        backpackItem.GridPosition,
                        backpackItem.Size,
                        backpackItem.IsRotated,
                        backpackItem.Payload));
                }
            }

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
            FireRoundsToAnimate = other.FireRoundsToAnimate;
            FireActionPointsSpent = other.FireActionPointsSpent;
            FireAnimationDurationSeconds = other.FireAnimationDurationSeconds;
            IsOnOverwatch = other.IsOnOverwatch;
            OverwatchShotsRemaining = other.OverwatchShotsRemaining;
            OverwatchCooldownRemainingSeconds = other.OverwatchCooldownRemainingSeconds;
            LastOverwatchTarget = other.LastOverwatchTarget;

        }


        public void EnsureAmmoState()
        {
            if (WeaponData == null)
            {
                CurrentAmmoInMagazine = 0;
                ammoTrackedWeaponName = string.Empty;
                return;
            }

            string currentWeaponName = WeaponData.Name ?? string.Empty;
            if (!string.Equals(ammoTrackedWeaponName, currentWeaponName, StringComparison.Ordinal))
            {
                ammoTrackedWeaponName = currentWeaponName;
                CurrentAmmoInMagazine = WeaponData.UsesAmmo ? WeaponData.EffectiveMagazineCapacity : 0;
            }

            if (!WeaponData.UsesAmmo)
                CurrentAmmoInMagazine = 0;
            else
                CurrentAmmoInMagazine = Math.Clamp(CurrentAmmoInMagazine, 0, WeaponData.EffectiveMagazineCapacity);
        }

        public bool NeedsReloadForFireAction()
        {
            EnsureAmmoState();
            if (WeaponData == null || !WeaponData.UsesAmmo)
                return false;

            int roundsNeeded = WeaponData.GetRoundsConsumedPerActionPoint();
            return CurrentAmmoInMagazine < roundsNeeded;
        }

        public bool ReloadWeapon()
        {
            EnsureAmmoState();
            if (WeaponData == null || !WeaponData.UsesAmmo)
                return false;

            CurrentAmmoInMagazine = WeaponData.EffectiveMagazineCapacity;
            return true;
        }

        public int ConsumeRoundsForFireAction()
        {
            EnsureAmmoState();
            if (WeaponData == null || !WeaponData.UsesAmmo)
                return 0;

            int roundsNeeded = WeaponData.GetRoundsConsumedPerActionPoint();
            int roundsConsumed = Math.Min(roundsNeeded, CurrentAmmoInMagazine);
            CurrentAmmoInMagazine -= roundsConsumed;
            return roundsConsumed;
        }

        public void ActivateOverwatch(int actionPointsSpent, int shotsToReserve)
        {
            int safeAp = Math.Max(1, actionPointsSpent);
            int safeShots = Math.Max(0, shotsToReserve);

            ActionPoints = Math.Max(0, ActionPoints - safeAp);
            IsOnOverwatch = safeShots > 0;
            OverwatchShotsRemaining = safeShots;
            OverwatchCooldownRemainingSeconds = 0f;
            LastOverwatchTarget = null;
        }

        public void ClearOverwatch()
        {
            IsOnOverwatch = false;
            OverwatchShotsRemaining = 0;
            OverwatchCooldownRemainingSeconds = 0f;
            LastOverwatchTarget = null;
        }

        public int GetTotalArmor()
        {
            int total = 0;
            if (EquippedHelmet != null) total += EquippedHelmet.Data.ArmorValue;
            if (EquippedNeck != null) total += EquippedNeck.Data.ArmorValue;
            if (EquippedArmor != null) total += EquippedArmor.Data.ArmorValue;
            if (EquippedShield != null) total += EquippedShield.Data.ArmorValue; // NOUVEAU
            if (EquippedShirt != null) total += EquippedShirt.Data.ArmorValue;   // NOUVEAU
            if (EquippedPants != null) total += EquippedPants.Data.ArmorValue;
            if (EquippedKnees != null) total += EquippedKnees.Data.ArmorValue;
            if (EquippedFeet != null) total += EquippedFeet.Data.ArmorValue;
            if (EquippedChestRig != null) total += EquippedChestRig.Data.ArmorValue;
            if (EquippedBelt != null) total += EquippedBelt.Data.ArmorValue;
            total += Skills.GetDefenseBonus();
            return total;
        }

        public ProtectionLevel GetBestProtectionLevel()
        {
            ProtectionLevel bestLevel = ProtectionLevel.None;

            foreach (var equipped in GetEquippedArmorItems())
            {
                if (equipped?.Data == null)
                    continue;

                if (equipped.Data.ProtectionLevel > bestLevel)
                    bestLevel = equipped.Data.ProtectionLevel;
            }

            return bestLevel;
        }

        public static string GetProtectionLabel(ProtectionLevel level)
        {
            return level switch
            {
                ProtectionLevel.Fragmentation => "Fragments",
                ProtectionLevel.NIJ_II => "Armes légères (NIJ II)",
                ProtectionLevel.NIJ_IIIA => "Armes moyennes (NIJ IIIA)",
                ProtectionLevel.NIJ_III => "Armes d'assaut (NIJ III)",
                ProtectionLevel.NIJ_IV => "Armes lourdes (NIJ IV)",
                _ => "Aucune"
            };
        }

        public int GetBallisticDamageReduction()
        {
            int nijProtectionPercent = 0;

            foreach (var equipped in GetEquippedArmorItems())
            {
                if (equipped?.Data == null)
                    continue;

                nijProtectionPercent += equipped.Data.GetNijProtectionPercent();
            }

            nijProtectionPercent = Math.Clamp(nijProtectionPercent, 0, 95);
            int protectionReduction = (int)Math.Round(nijProtectionPercent / 20f);

            return protectionReduction + Skills.GetDefenseBonus();
        }

        private IEnumerable<Item> GetEquippedArmorItems()
        {
            yield return EquippedHelmet;
            yield return EquippedNeck;
            yield return EquippedArmor;
            yield return EquippedShield;
            yield return EquippedShirt;
            yield return EquippedPants;
            yield return EquippedKnees;
            yield return EquippedFeet;
            yield return EquippedChestRig;
            yield return EquippedBelt;
        }

        private static HumanBodyType DetermineBodyType(Team team, string unitClass, string name)
        {
            bool isHuman = IsHumanUnit(team, unitClass);

            if (!isHuman)
                return HumanBodyType.Masculine;

            if (!string.IsNullOrWhiteSpace(name))
            {
                string trimmedName = name.Trim();

                if (KnownFeminineNames.Contains(trimmedName))
                    return HumanBodyType.Feminine;

                if (KnownMasculineNames.Contains(trimmedName))
                    return HumanBodyType.Masculine;
            }

            int hash = string.IsNullOrWhiteSpace(name) ? 0 : name.GetHashCode() & int.MaxValue;
            return hash % 2 == 0 ? HumanBodyType.Feminine : HumanBodyType.Masculine;
        }

        private static Handedness DetermineHandedness(Team team, string unitClass, string name, HumanBodyType bodyType)
        {
            bool isHuman = IsHumanUnit(team, unitClass);

            if (!isHuman)
                return Handedness.Right;

            int hash = string.IsNullOrWhiteSpace(name)
                ? 0
                : HashCode.Combine(name, unitClass, (int)bodyType) & int.MaxValue;

            // Méta-analyses : hommes ~11-13% gauchers, femmes ~9-11% gauchères.
            int leftHandedChancePercent = bodyType == HumanBodyType.Masculine ? 12 : 10;
            return (hash % 100) < leftHandedChancePercent ? Handedness.Left : Handedness.Right;
        }

        private static bool IsHumanUnit(Team team, string unitClass)
        {
            return team == Team.Player || (!string.IsNullOrWhiteSpace(unitClass) && HumanClasses.Contains(unitClass));
        }

        public int GetMobilityPenalty()
        {
            int penalty = 0;
            if (EquippedArmor != null) penalty += EquippedArmor.Data.MobilityPenalty;
            if (EquippedShield != null) penalty += EquippedShield.Data.MobilityPenalty;
            if (EquippedPants != null) penalty += EquippedPants.Data.MobilityPenalty;
            if (EquippedKnees != null) penalty += EquippedKnees.Data.MobilityPenalty;
            if (EquippedFeet != null) penalty += EquippedFeet.Data.MobilityPenalty;
            return penalty;
        }

        public float GetEquippedWeightLbs()
        {
            float total = 0f;

            total += EquippedWeapon?.Data?.WeightLbs ?? 0f;
            total += EquippedHelmet?.Data?.WeightLbs ?? 0f;
            total += EquippedNeck?.Data?.WeightLbs ?? 0f;
            total += EquippedArmor?.Data?.WeightLbs ?? 0f;
            total += EquippedShield?.Data?.WeightLbs ?? 0f;
            total += EquippedAccessory?.Data?.WeightLbs ?? 0f;
            total += EquippedRightHandFlashlight?.Data?.WeightLbs ?? 0f;
            total += EquippedLeftHandFlashlight?.Data?.WeightLbs ?? 0f;
            total += EquippedShirt?.Data?.WeightLbs ?? 0f;
            total += EquippedPants?.Data?.WeightLbs ?? 0f;
            total += EquippedKnees?.Data?.WeightLbs ?? 0f;
            total += EquippedFeet?.Data?.WeightLbs ?? 0f;
            total += EquippedChestRig?.Data?.WeightLbs ?? 0f;
            total += EquippedBelt?.Data?.WeightLbs ?? 0f;

            foreach (var item in PantsInventory)
                total += item?.Data?.WeightLbs ?? 0f;
            foreach (var item in ChestRigInventory)
                total += item?.Data?.WeightLbs ?? 0f;
            EnsureBackpackInventoryGrid();
            foreach (var item in BackpackInventory.GetAllItems())
                total += item?.Data?.WeightLbs ?? 0f;

            return total;
        }

        public int GetPantsInventoryCapacity()
        {
            return EquippedPants?.Data?.BonusInventorySlots ?? 0;
        }

        public int GetChestRigInventoryCapacity()
        {
            return EquippedChestRig?.Data?.BonusInventorySlots ?? 0;
        }

        public void RefreshGrenadeInventoryFromEquipment()
        {
            Grenades.Clear();

            foreach (var item in PantsInventory)
            {
                if (item?.Data?.GrenadeData != null)
                    Grenades.Add(item.Data.GrenadeData);
            }

            foreach (var item in ChestRigInventory)
            {
                if (item?.Data?.GrenadeData != null)
                    Grenades.Add(item.Data.GrenadeData);
            }

            EnsureBackpackInventoryGrid();
            foreach (var item in BackpackInventory.GetAllItems())
                if (item?.Data?.GrenadeData != null)
                    Grenades.Add(item.Data.GrenadeData);

            MaxGrenades = Grenades.Count;
        }

        public int GetBackpackInventoryCapacity()
        {
            if (string.IsNullOrWhiteSpace(EquippedBackpack))
                return 0;

            if (EquippedBackpack.Contains("XL", StringComparison.OrdinalIgnoreCase))
                return 12;
            if (EquippedBackpack.Contains("Medium", StringComparison.OrdinalIgnoreCase))
                return 8;
            if (EquippedBackpack.Contains("Small", StringComparison.OrdinalIgnoreCase))
                return 4;

            return 6;
        }

        public ItemSize GetBackpackGridSize()
        {
            int capacity = GetBackpackInventoryCapacity();
            int rows = Math.Max(1, (int)Math.Ceiling(capacity / (float)BackpackGridColumns));
            return new ItemSize(BackpackGridColumns, rows);
        }

        public void EnsureBackpackInventoryGrid()
        {
            ItemSize gridSize = GetBackpackGridSize();

            if (backpackInventoryGrid != null &&
                backpackInventoryGrid.Width == gridSize.Width &&
                backpackInventoryGrid.Height == gridSize.Height)
            {
                return;
            }

            InventoryGrid resizedGrid = new InventoryGrid(gridSize.Width, gridSize.Height);
            if (backpackInventoryGrid != null)
            {
                foreach (GridItem item in backpackInventoryGrid.GetAllItems())
                {
                    GridItem migratedItem = new GridItem(item.Data, item.GridPosition, item.Size, item.IsRotated, item.Payload);
                    if (resizedGrid.CanPlaceItem(migratedItem.GridPosition, migratedItem.GetCurrentSize()))
                        resizedGrid.PlaceItem(migratedItem);
                }
            }

            backpackInventoryGrid = resizedGrid;
        }

        public int GetMaxHealth()
        {
            return MaxHealth + Skills.GetHealthBonus();
        }

        public int GetMaxMovementPoints()
        {
            return GetMaxMoveRange(); // Utilise la nouvelle méthode
        }

        public static float ComputeOrientationFromDelta(float deltaX, float deltaZ)
        {
            // L'orientation est utilisée à la fois pour la rotation du modèle et pour la flèche
            // de direction; elle doit donc pointer directement vers le vecteur de déplacement.
            // Convention retenue: un yaw de 0 regarde vers +Z, donc le vecteur forward est
            // (sin(orientation), cos(orientation)).
            return MathHelper.WrapAngle((float)Math.Atan2(deltaX, deltaZ));
        }

        public void UpdateVisualPosition(int cellSize = 2)
        {
            VisualPosition = new Vector3(
                Cell.X * cellSize + cellSize / 2f,
                WorldMetrics.FloorToWorldY(Floor, cellSize),
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
                    WorldMetrics.FloorToWorldY(Floor, cellSize),
                    cell.Y * cellSize + cellSize / 2f
                ));
            }

            BeginNextMoveSegment();
        }

        public void StartMoveAlongPath(List<GridNode> path, int cellSize = 2)
        {
            if (path == null || path.Count == 0)
                return;

            Point newCell = path[path.Count - 1].Cell;

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
            foreach (GridNode node in path)
            {
                movementWaypoints.Enqueue(new Vector3(
                    node.Cell.X * cellSize + cellSize / 2f,
                    WorldMetrics.FloorToWorldY(node.Floor, cellSize),
                    node.Cell.Y * cellSize + cellSize / 2f
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
                TargetOrientation = ComputeOrientationFromDelta(direction.X, direction.Z);
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
                IdleBobOffset = 0f;
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
            return GetShortMoveRange(GetEquippedWeightLbs());
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
            return GetMaxMoveRange(GetEquippedWeightLbs());
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
        /// Obtient la portée de sprint (2 AP + phosphocréatine)
        /// </summary>
        public int GetSprintRange()
        {
            return GetSprintRange(GetEquippedWeightLbs());
        }

        /// <summary>
        /// Obtient la portée de sprint (2 AP + phosphocréatine), ajustée selon la charge portée.
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
            const float feetPerCell = FeetPerCell;

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
            const int feetPerCell = FeetPerCell;

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
            return CanSprint(GetMaxMoveRange() + 1);
        }

        public bool CanSprint(int distance)
        {
            return ActionPoints >= 2 && Phosphocreatine >= GetMovementPhosphocreatineCost(distance);
        }

        public int GetMovementPhosphocreatineCost(int distance)
        {
            if (distance <= 0)
                return 0;

            int shortRange = Math.Max(1, GetShortMoveRange());
            int maxRange = Math.Max(shortRange, GetMaxMoveRange());
            int sprintRange = Math.Max(maxRange, GetSprintRange());

            if (distance <= shortRange)
            {
                float ratio = MathHelper.Clamp(distance / (float)shortRange, 0f, 1f);
                return (int)MathF.Ceiling(MaxPhosphocreatine * 0.15f * ratio);
            }

            if (distance <= maxRange)
            {
                float ratio = MathHelper.Clamp(distance / (float)maxRange, 0f, 1f);
                return (int)MathF.Ceiling(MaxPhosphocreatine * 0.30f * ratio);
            }

            float sprintRatio = MathHelper.Clamp(distance / (float)sprintRange, 0f, 1f);
            return (int)MathF.Ceiling(MaxPhosphocreatine * 0.60f * sprintRatio);
        }

        public int GetSprintPhosphocreatineCost(int distance)
        {
            return GetMovementPhosphocreatineCost(distance);
        }

        /// <summary>
        /// Consomme la phosphocréatine pour un sprint
        /// </summary>
        public void ConsumeSprint(int distance)
        {
            int cost = GetMovementPhosphocreatineCost(distance);
            Phosphocreatine = Math.Max(0, Phosphocreatine - cost);
            phosphocreatineRegenRound = 0;
            Console.WriteLine($"[UNIT] {Name} sprints! Phosphocreatine: {Phosphocreatine}/{MaxPhosphocreatine} (cost {cost})");
        }

        /// <summary>
        /// Régénère la phosphocréatine (appelé chaque tour)
        /// </summary>
        public void RegeneratePhosphocreatine()
        {
            int tableIndex = Math.Min(phosphocreatineRegenRound, phosphocreatineRegenByRound.Length - 1);
            int regenPercent = phosphocreatineRegenByRound[tableIndex];
            int regenAmount = (int)MathF.Ceiling(MaxPhosphocreatine * (regenPercent / 100f));
            Phosphocreatine = Math.Min(MaxPhosphocreatine, Phosphocreatine + regenAmount);
            phosphocreatineRegenRound++;
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
                return 2; // Sprint (+ phosphocréatine)
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
