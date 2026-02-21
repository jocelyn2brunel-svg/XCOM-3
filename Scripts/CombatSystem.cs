using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// État du tour
    /// </summary>
    public enum TurnState { PlayerTurn, EnemyTurn, Busy }

    /// <summary>
    /// Gère les tours, le combat et les actions des unités
    /// </summary>
    public class CombatSystem
    {
        private Random random;
        private PathfindingSystem pathfinding;
        private Func<Point, Unit> getUnitAtCell;
        private Func<Point, int, FurnitureData> getFurnitureAtCellOnFloor;
        private Func<Unit, Point, int, bool> isEnemyCellVisibleToPlayers;
        private OptimizedUnitManager unitManager;

        // État du tour
        public TurnState CurrentTurn { get; private set; } = TurnState.PlayerTurn;
        public int PlayerTurnNumber { get; private set; } = 0;
        public int EnemyTurnIndex { get; private set; }
        public bool IsActionInProgress { get; set; }

        // Listes d'unités (références)
        private List<Unit> playerUnits;
        private List<Unit> enemyUnits;

        private CoverSystem coverSystem;
        private readonly Dictionary<Unit, int> targetPressureCache = new Dictionary<Unit, int>();
        private readonly Dictionary<Unit, bool> lineOfSightCache = new Dictionary<Unit, bool>();
        private readonly Dictionary<Unit, float> postHitPauseTimers = new Dictionary<Unit, float>();
        private const float HitConfirmPauseSeconds = 0.5f;

        private enum EnemyIntentType { Fire, Move, EndTurn }

        private readonly struct EnemyIntent
        {
            public EnemyIntentType Type { get; }
            public Unit Target { get; }
            public List<Point> MovePath { get; }

            public EnemyIntent(EnemyIntentType type, Unit target = null, List<Point> movePath = null)
            {
                Type = type;
                Target = target;
                MovePath = movePath;
            }
        }

        private readonly List<EnemyIntent> enemyIntentQueue = new List<EnemyIntent>();

        private static int GetWeaponRange(Unit unit) => unit?.WeaponData?.EffectiveRange ?? 0;
        private static int GetWeaponAccuracy(Unit unit) => unit?.WeaponData?.EffectiveAccuracy ?? 0;
        private static int GetWeaponDamage(Unit unit) => unit?.WeaponData?.EffectiveDamage ?? 0;
        private static bool HasUsableWeapon(Unit unit) => unit?.WeaponData != null;

        private static int GetFurnitureCoverBonus(FurnitureType type)
        {
            return type switch
            {
                FurnitureType.Fridge => 30,
                FurnitureType.Counter => 20,
                FurnitureType.Stove => 20,
                FurnitureType.Table => 15,
                FurnitureType.Bed => 10,
                FurnitureType.Chair => 5,
                FurnitureType.SedanToyotaCorolla => 35,
                FurnitureType.SedanBmwSeries3 => 35,
                FurnitureType.SedanMercedesEClass => 35,
                FurnitureType.PickupToyotaTacoma => 40,
                FurnitureType.PickupFordF150 => 45,
                FurnitureType.PickupRam3500 => 50,
                FurnitureType.TreePine => 40,
                FurnitureType.LootCrate => 20,
                _ => 0
            };
        }

        private static int GetRangeBasedAccuracyPenalty(int distance, int weaponRange)
        {
            if (distance <= 0)
                return 0;

            if (weaponRange <= 1)
                return distance > 1 ? 100 : 0;

            float normalizedDistance = MathHelper.Clamp(distance / (float)weaponRange, 0f, 1f);
            float adjustedDistance = MathHelper.Clamp((normalizedDistance - 0.25f) / 0.75f, 0f, 1f);
            return (int)Math.Round(adjustedDistance * 40f);
        }

        public CombatSystem(Random random, PathfindingSystem pathfinding,
            Func<Point, Unit> getUnitAtCell,
            Func<Point, int, FurnitureData> getFurnitureAtCellOnFloor,
            OptimizedUnitManager unitManager)
        {
            this.random = random;
            this.pathfinding = pathfinding;
            this.getUnitAtCell = getUnitAtCell;
            this.getFurnitureAtCellOnFloor = getFurnitureAtCellOnFloor;
            this.unitManager = unitManager;
        }

        private int GetFurnitureDefenseBonus(Unit target, Unit shooter)
        {
            if (target == null || shooter == null || getFurnitureAtCellOnFloor == null)
                return 0;

            if (target.Floor != shooter.Floor)
                return 0;

            int dx = Math.Sign(target.Cell.X - shooter.Cell.X);
            int dy = Math.Sign(target.Cell.Y - shooter.Cell.Y);
            Point blockingCell = new Point(target.Cell.X - dx, target.Cell.Y - dy);

            FurnitureData furniture = getFurnitureAtCellOnFloor(blockingCell, target.Floor);
            if (furniture == null)
                return 0;

            return GetFurnitureCoverBonus(furniture.Type);
        }

        public void SetUnits(List<Unit> players, List<Unit> enemies)
        {
            playerUnits = players;
            enemyUnits = enemies;
        }

        public void SetPathfinding(PathfindingSystem newPathfinding)
        {
            pathfinding = newPathfinding;
        }

        public void SetEnemyVisibilityEvaluator(Func<Unit, Point, int, bool> visibilityEvaluator)
        {
            isEnemyCellVisibleToPlayers = visibilityEvaluator;
        }

        /// <summary>
        /// Démarre le tour du joueur
        /// </summary>
        public void StartPlayerTurn()
        {
            PlayerTurnNumber++;

            foreach (var u in playerUnits)
            {
                u.ActionPoints = u.MaxActionPoints; // ← Utiliser MaxActionPoints au lieu de 3
                u.BeginTurnMetabolicRecovery();
                u.ClearOverwatch();
                u.ResetFireActionStreak();
            }

            CurrentTurn = TurnState.PlayerTurn;
            if (unitManager == null)
                throw new InvalidOperationException("unitManager n'a pas été initialisé !");
            unitManager.OnNewTurn();

            Console.WriteLine("[COMBAT] Tour du joueur");
        }


        /// <summary>
        /// Démarre le tour ennemi
        /// </summary>
        public void StartEnemyTurn()
        {
            foreach (var u in enemyUnits)
            {
                u.ActionPoints = 2;
                u.BeginTurnMetabolicRecovery();
                u.ResetFireActionStreak();
            }

            EnemyTurnIndex = 0;
            CurrentTurn = TurnState.EnemyTurn;
            enemyIntentQueue.Clear();
            unitManager.OnNewTurn();

            Console.WriteLine("[COMBAT] Tour ennemi");
        }

        /// <summary>
        /// Met à jour le tour ennemi
        /// </summary>
        public void UpdateEnemyTurn(int cellSize)
        {
            if (EnemyTurnIndex >= enemyUnits.Count)
            {
                StartPlayerTurn();
                return;
            }

            Unit enemy = enemyUnits[EnemyTurnIndex];

            // Attendre la fin des animations
            if (enemy.IsFiring || enemy.IsMoving)
                return;

            if (enemy.ActionPoints <= 0)
            {
                enemyIntentQueue.Clear();
                EnemyTurnIndex++;
                return;
            }

            if (enemyIntentQueue.Count == 0)
            {
                BuildEnemyIntentions(enemy);
            }

            if (enemyIntentQueue.Count == 0)
            {
                enemy.ActionPoints = 0;
                enemyIntentQueue.Clear();
                EnemyTurnIndex++;
                return;
            }

            EnemyIntent intent = enemyIntentQueue[0];
            enemyIntentQueue.RemoveAt(0);
            ExecuteEnemyIntent(enemy, intent, cellSize);

            if (enemy.ActionPoints <= 0)
            {
                enemyIntentQueue.Clear();
                EnemyTurnIndex++;
            }
        }

        private void BuildEnemyIntentions(Unit enemy)
        {
            enemyIntentQueue.Clear();

            if (enemy == null || enemy.ActionPoints <= 0)
                return;

            BuildTargetPressureCache(enemy);
            Unit target = SelectBestTarget(enemy);
            if (target == null)
                return;

            if (random.Next(100) < 10 && playerUnits.Count > 1)
            {
                target = playerUnits[random.Next(playerUnits.Count)];
                Console.WriteLine($"[COMBAT] {enemy.Name} change de cible!");
            }

            int distance = Math.Abs(target.Cell.X - enemy.Cell.X) + Math.Abs(target.Cell.Y - enemy.Cell.Y);
            bool canSeeTarget = lineOfSightCache.TryGetValue(target, out bool cachedLoS)
                ? cachedLoS
                : pathfinding.HasLineOfSight(enemy.Cell, target.Cell);

            if (distance <= GetWeaponRange(enemy) && canSeeTarget)
            {
                enemyIntentQueue.Add(new EnemyIntent(EnemyIntentType.Fire, target));
                if (enemy.ActionPoints > 1)
                    enemyIntentQueue.Add(new EnemyIntent(EnemyIntentType.Fire, target));
                return;
            }

            var movePath = BuildMovementPathTowardTarget(enemy, target);
            if (movePath.Count > 0)
            {
                enemyIntentQueue.Add(new EnemyIntent(EnemyIntentType.Move, target, movePath));
            }

            if (enemy.ActionPoints > 1)
            {
                Point plannedCell = movePath.Count > 0 ? movePath[movePath.Count - 1] : enemy.Cell;
                int plannedDistance = Math.Abs(target.Cell.X - plannedCell.X) + Math.Abs(target.Cell.Y - plannedCell.Y);
                bool canSeeAfterMove = pathfinding.HasLineOfSight(plannedCell, target.Cell);
                if (plannedDistance <= GetWeaponRange(enemy) && canSeeAfterMove)
                {
                    enemyIntentQueue.Add(new EnemyIntent(EnemyIntentType.Fire, target));
                }
            }

            if (enemyIntentQueue.Count == 0)
                enemyIntentQueue.Add(new EnemyIntent(EnemyIntentType.EndTurn));
        }

        private void ExecuteEnemyIntent(Unit enemy, EnemyIntent intent, int cellSize)
        {
            switch (intent.Type)
            {
                case EnemyIntentType.Fire:
                    if (intent.Target != null && intent.Target.Health > 0)
                    {
                        float deltaX = intent.Target.Cell.X - enemy.Cell.X;
                        float deltaZ = intent.Target.Cell.Y - enemy.Cell.Y;
                        enemy.TargetOrientation = Unit.ComputeOrientationFromDelta(deltaX, deltaZ);
                        InitiateFire(enemy, intent.Target);
                    }
                    else
                    {
                        enemy.ActionPoints = Math.Max(0, enemy.ActionPoints - 1);
                    }
                    break;

                case EnemyIntentType.Move:
                    if (intent.Target != null)
                    {
                        ExecuteMoveIntent(enemy, intent.Target, intent.MovePath, cellSize);
                    }
                    else
                    {
                        enemy.ActionPoints = Math.Max(0, enemy.ActionPoints - 1);
                    }
                    break;

                default:
                    enemy.ActionPoints = 0;
                    break;
            }
        }

        private List<Point> BuildMovementPathTowardTarget(Unit enemy, Unit target)
        {
            var path = pathfinding.FindPath(enemy.Cell, target.Cell, int.MaxValue, enemy);
            if (path.Count == 0)
                return new List<Point>();

            int steps = Math.Min(enemy.GetMaxMoveRange(), path.Count);
            for (int i = steps - 1; i >= 0; i--)
            {
                if (getUnitAtCell(path[i]) == null)
                {
                    return path.GetRange(0, i + 1);
                }
            }

            return new List<Point>();
        }

        private void ExecuteMoveIntent(Unit enemy, Unit target, List<Point> movePath, int cellSize)
        {
            if (movePath != null && movePath.Count > 0)
            {
                ExecuteEnemyMoveWithVisibilityFastForward(enemy, movePath, cellSize);
                UpdateUnitCover(enemy);
                enemy.ActionPoints--;
                return;
            }

            TrySimpleMove(enemy, target, cellSize);
        }

        private void ExecuteEnemyMoveWithVisibilityFastForward(Unit enemy, List<Point> movePath, int cellSize)
        {
            Point finalCell = movePath[movePath.Count - 1];
            bool currentlyVisible = IsEnemyVisibleAtCell(enemy, enemy.Cell);

            if (!currentlyVisible)
            {
                int firstVisibleIndex = -1;
                for (int i = 0; i < movePath.Count; i++)
                {
                    if (IsEnemyVisibleAtCell(enemy, movePath[i]))
                    {
                        firstVisibleIndex = i;
                        break;
                    }
                }

                if (firstVisibleIndex == -1)
                {
                    InstantRelocateEnemy(enemy, finalCell, cellSize);
                    unitManager.OnUnitMoved(enemy, finalCell);
                    return;
                }

                if (firstVisibleIndex > 0)
                {
                    Point lastHiddenCell = movePath[firstVisibleIndex - 1];
                    InstantRelocateEnemy(enemy, lastHiddenCell, cellSize);
                    movePath = movePath.GetRange(firstVisibleIndex, movePath.Count - firstVisibleIndex);
                }
            }

            enemy.SetMovementStyle(1);
            enemy.StartMoveAlongPath(movePath, cellSize);
            unitManager.OnUnitMoved(enemy, finalCell);
        }

        private bool IsEnemyVisibleAtCell(Unit enemy, Point cell)
        {
            if (isEnemyCellVisibleToPlayers == null)
                return true;

            return isEnemyCellVisibleToPlayers(enemy, cell, enemy.Floor);
        }

        private static void InstantRelocateEnemy(Unit enemy, Point destination, int cellSize)
        {
            enemy.Cell = destination;
            enemy.IsMoving = false;
            enemy.MoveProgress = 1f;
            enemy.WalkCycleTime = 0f;
            enemy.UpdateVisualPosition(cellSize);
            enemy.TargetPosition = enemy.VisualPosition;
        }

        /// <summary>
        /// Sélection intelligente de cible
        /// </summary>
        private Unit SelectBestTarget(Unit enemy)
        {
            if (playerUnits.Count == 0)
                return null;

            Unit bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var player in playerUnits)
            {
                float score = 0f;
                int distance = Math.Abs(player.Cell.X - enemy.Cell.X) +
                               Math.Abs(player.Cell.Y - enemy.Cell.Y);
                bool hasLineOfSight = HasPotentialLineOfSight(enemy, player, distance);

                // 1. Distance (plus proche = plus prioritaire)
                score += Math.Max(0, 100 - distance * 5);

                // 2. Ligne de vue et portée
                if (hasLineOfSight)
                    score += 50;
                if (distance <= GetWeaponRange(enemy) && hasLineOfSight)
                    score += 75;

                // 3. Santé de la cible (plus faible = plus prioritaire)
                float healthPercent = (float)player.Health / player.MaxHealth;
                if (healthPercent < 0.5f)
                    score += (1.0f - healthPercent) * 30;

                // 4. Cible déjà visée par d’autres ennemis (moins prioritaire)
                int alreadyTargeting = targetPressureCache.TryGetValue(player, out int pressure) ? pressure : 0;
                score -= alreadyTargeting * 20;

                // 7. Menace (AP + dégâts potentiels)
                score += player.ActionPoints * 5;
                score += GetWeaponDamage(player) * 2;

                // 8. Nombre d’alliés proches (cible protégée = moins prioritaire)
                int alliesNearby = 0;
                foreach (var ally in playerUnits)
                {
                    if (ally == player)
                        continue;

                    int allyDistance = Math.Abs(ally.Cell.X - player.Cell.X) + Math.Abs(ally.Cell.Y - player.Cell.Y);
                    if (allyDistance <= 2)
                        alliesNearby++;
                }
                score -= alliesNearby * 15;

                // 9. Comportement adaptatif selon la santé de l’ennemi
                float selfHealthPercent = (float)enemy.Health / enemy.MaxHealth;
                if (selfHealthPercent < 0.3f && healthPercent > 0.7f)
                    score -= 20; // évite les cibles fortes si faible en PV

                // 10. Aléatoire pour surprendre le joueur
                score += (float)(random.NextDouble() * 20 - 10); // +/-10 points

                // Choisir la meilleure cible
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = player;
                }
            }

            return bestTarget;
        }

        private void BuildTargetPressureCache(Unit currentEnemy)
        {
            targetPressureCache.Clear();
            lineOfSightCache.Clear();

            foreach (var enemy in enemyUnits)
            {
                Unit pendingTarget = enemy.PendingTarget;
                if (enemy == currentEnemy || pendingTarget == null)
                    continue;

                targetPressureCache.TryGetValue(pendingTarget, out int count);
                targetPressureCache[pendingTarget] = count + 1;
            }
        }

        private bool HasPotentialLineOfSight(Unit enemy, Unit target, int distance)
        {
            if (lineOfSightCache.TryGetValue(target, out bool cachedValue))
            {
                return cachedValue;
            }

            // Optimisation IA : inutile de tester la ligne de vue des cibles très lointaines.
            if (distance > GetWeaponRange(enemy) + 6)
            {
                lineOfSightCache[target] = false;
                return false;
            }

            bool hasLoS = pathfinding.HasLineOfSight(enemy.Cell, target.Cell);
            lineOfSightCache[target] = hasLoS;
            return hasLoS;
        }


        /// <summary>
        /// Déplacement simple vers la cible
        /// </summary>
        private void TrySimpleMove(Unit enemy, Unit target, int cellSize)
        {
            int dx = Math.Sign(target.Cell.X - enemy.Cell.X);
            int dy = Math.Sign(target.Cell.Y - enemy.Cell.Y);

            List<Point> moves = new List<Point>();

            if (dx != 0 && dy != 0)
                moves.Add(new Point(enemy.Cell.X + dx, enemy.Cell.Y + dy));

            if (dx != 0)
                moves.Add(new Point(enemy.Cell.X + dx, enemy.Cell.Y));

            if (dy != 0)
                moves.Add(new Point(enemy.Cell.X, enemy.Cell.Y + dy));

            foreach (Point move in moves)
            {
                if (pathfinding.IsWalkable(move, enemy) &&
                    !pathfinding.BlocksMovement(enemy.Cell, move) &&
                    getUnitAtCell(move) == null)
                {
                    enemy.SetMovementStyle(1);
                    ExecuteEnemyMoveWithVisibilityFastForward(enemy, new List<Point> { move }, cellSize);
                    UpdateUnitCover(enemy);
                    enemy.ActionPoints--;
                    return;
                }
            }

            enemy.ActionPoints = 0;
        }

        /// <summary>
        /// Initie un tir
        /// </summary>
        public void InitiateFire(Unit shooter, Unit target)
        {
            if (shooter == null || target == null)
                return;

            if (shooter.ActionPoints <= 0)
                return;

            if (!HasUsableWeapon(shooter))
                return;

            shooter.EnsureAmmoState();
            if (shooter.NeedsReloadForFireAction())
            {
                if (shooter.ActionPoints <= 0)
                    return;

                bool reloaded = shooter.ReloadWeapon();
                if (reloaded)
                {
                    shooter.ActionPoints--;
                    Console.WriteLine($"[COMBAT] {shooter.Name} recharge {shooter.WeaponData.Name} ({shooter.CurrentAmmoInMagazine}/{shooter.WeaponData.EffectiveMagazineCapacity})");
                }

                return;
            }

            int distance = Math.Abs(target.Cell.X - shooter.Cell.X) +
                          Math.Abs(target.Cell.Y - shooter.Cell.Y);

            int weaponRange = GetWeaponRange(shooter);

            if (distance > weaponRange)
                return;

            if (!pathfinding.HasLineOfSight(shooter.Cell, target.Cell))
                return;

            float deltaX = target.Cell.X - shooter.Cell.X;
            float deltaZ = target.Cell.Y - shooter.Cell.Y;
            shooter.TargetOrientation = Unit.ComputeOrientationFromDelta(deltaX, deltaZ);

            IsActionInProgress = true;
            shooter.IsFiring = true;
            shooter.FireTarget = target.Cell;
            shooter.FireProgress = 0f;
            shooter.FireActionPointsSpent = 1;
            postHitPauseTimers.Remove(shooter);

            // ✅ CALCUL AVEC COUVERTURE
            int baseAccuracy = GetWeaponAccuracy(shooter) + shooter.Skills.GetAccuracyBonus();
            int rangePenalty = GetRangeBasedAccuracyPenalty(distance, weaponRange);
            int fireModePenalty = shooter.GetFireModeAccuracyPenaltyForNextShot();
            int effectiveAccuracy = baseAccuracy - rangePenalty - shooter.GetAnaerobicAccuracyPenalty() - fireModePenalty;

            // Appliquer le malus de couverture
            if (coverSystem != null)
            {
                int coverBonus = coverSystem.GetEffectiveDefenseBonus(target, shooter);
                effectiveAccuracy -= coverBonus;

                if (coverBonus > 0)
                {
                    Console.WriteLine($"[COMBAT] {target.Name}'s cover gives -{coverBonus}% to hit");
                }

                if (target.CoverType != CoverType.None && coverSystem.IsUnitFlanked(target, shooter))
                {
                    Console.WriteLine($"[COMBAT] {target.Name} is FLANKED! No cover bonus!");
                }
            }

            int furnitureBonus = GetFurnitureDefenseBonus(target, shooter);
            if (furnitureBonus > 0)
            {
                effectiveAccuracy -= furnitureBonus;
                Console.WriteLine($"[COMBAT] {target.Name} is behind furniture: -{furnitureBonus}% to hit");
            }

            effectiveAccuracy = Math.Max(effectiveAccuracy, 5);

            shooter.WillHit = random.Next(100) < effectiveAccuracy;
            shooter.PendingTarget = target;
            shooter.ActionPoints--;
            shooter.RegisterIntenseAction("tir", 18);
            int roundsConsumed = shooter.ConsumeRoundsForFireAction();
            shooter.RegisterFireActionInCurrentTurn();
            shooter.FireRoundsToAnimate = Math.Max(1, roundsConsumed);
            shooter.FireAnimationDurationSeconds = ComputeFireAnimationDurationSeconds(shooter, shooter.FireRoundsToAnimate, shooter.FireActionPointsSpent);
            OnShotFired?.Invoke(shooter);

            if (shooter.WeaponData != null && shooter.WeaponData.UsesAmmo)
            {
                Console.WriteLine($"[COMBAT] {shooter.Name} fires at {target.Name} ({effectiveAccuracy}% chance, range penalty: -{rangePenalty}%, fire mode penalty: -{fireModePenalty}%, ammo -{roundsConsumed}, left {shooter.CurrentAmmoInMagazine}/{shooter.WeaponData.EffectiveMagazineCapacity})");
            }
            else
            {
                Console.WriteLine($"[COMBAT] {shooter.Name} fires at {target.Name} ({effectiveAccuracy}% chance, range penalty: -{rangePenalty}%, fire mode penalty: -{fireModePenalty}%)");
            }
        }

        /// <summary>
        /// Met à jour les animations de tir
        /// </summary>
        public void UpdateFiringAnimations(GameTime gameTime)
        {
            float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var u in playerUnits.Concat(enemyUnits))
            {
                if (!u.IsFiring || !u.FireTarget.HasValue)
                    continue;

                float fireDuration = Math.Max(0.01f, u.FireAnimationDurationSeconds);
                u.FireProgress += deltaSeconds / fireDuration;

                if (u.FireProgress < 1f)
                    continue;

                if (u.PendingTarget != null)
                {
                    if (u.WillHit)
                    {
                        if (!postHitPauseTimers.ContainsKey(u))
                        {
                            // Impact: appliquer les dégâts puis conserver le plan de tir
                            // pendant un court instant pour laisser la réaction visuelle apparaître.
                            ApplyDamage(u, u.PendingTarget);
                            GiveShootingXP(u, u.PendingTarget);

                            u.PendingTarget = null;
                            u.WillHit = false;
                            postHitPauseTimers[u] = HitConfirmPauseSeconds;
                            continue;
                        }
                    }
                    else
                    {
                        GiveShootingXP(u, u.PendingTarget);
                        u.PendingTarget = null;
                        u.WillHit = false;
                    }
                }

                if (postHitPauseTimers.TryGetValue(u, out float remainingPause))
                {
                    remainingPause -= deltaSeconds;
                    if (remainingPause > 0f)
                    {
                        postHitPauseTimers[u] = remainingPause;
                        continue;
                    }

                    postHitPauseTimers.Remove(u);
                }

                // Fin du tir
                u.IsFiring = false;
                u.FireProgress = 0f;
                u.FireTarget = null;
                u.FireRoundsToAnimate = 1;
                u.FireActionPointsSpent = 1;
                u.FireAnimationDurationSeconds = 0.25f;
                IsActionInProgress = false;

                OnFireCompleted?.Invoke();
                return;
            }
        }

        private float ComputeFireAnimationDurationSeconds(Unit shooter, int roundsToAnimate, int actionPointsSpent)
        {
            int safeRounds = Math.Max(1, roundsToAnimate);
            int safeActionPoints = Math.Max(1, actionPointsSpent);
            int rpm = Math.Max(1, shooter?.WeaponData?.RoundsPerMinute ?? 0);

            float secondsPerRound = 60f / rpm;
            float burstDuration = safeRounds * secondsPerRound;

            // Conserve une lisibilité minimale pour les armes lentes / sans munitions,
            // mais respecte strictement le lien cadence(AP/RPM) -> durée.
            return Math.Max(0.12f * safeActionPoints, burstDuration);
        }

        /// <summary>
        /// Applique les dégâts d'un tir
        /// </summary>
        private void ApplyDamage(Unit shooter, Unit target)
        {
            int baseDamage = GetWeaponDamage(shooter) + shooter.Skills.GetDamageBonus();
            int damage = Math.Max(baseDamage - target.GetBallisticDamageReduction(), 1);

            target.Health = Math.Max(target.Health - damage, 0);

            Console.WriteLine($"[COMBAT] {target.Name} prend {damage} dégâts! HP: {target.Health}/{target.MaxHealth}");

            // XP de survie
            if (target.Team == Team.Player)
            {
                target.Skills.GainSurvivalXP(damage, target.Health > 0);
            }

            if (target.Health <= 0)
            {
                Console.WriteLine($"[COMBAT] {target.Name} est éliminé!");

                if (shooter.Team == Team.Player)
                {
                    shooter.Skills.GainKillXP(target.Class);
                }

                Vector3 impactDirection = ComputeImpactDirection(shooter, target);
                float impactMagnitude = MathHelper.Clamp(damage * 0.08f, 0.9f, 3.8f);
                Vector3 kineticImpulse = impactDirection * impactMagnitude;

                OnUnitKilled?.Invoke(target, kineticImpulse);
            }
        }

        private Vector3 ComputeImpactDirection(Unit shooter, Unit target)
        {
            if (shooter == null || target == null)
                return Vector3.Forward;

            Vector3 shooterWorld = new Vector3(shooter.Cell.X, shooter.Floor * 0.2f, shooter.Cell.Y);
            Vector3 targetWorld = new Vector3(target.Cell.X, target.Floor * 0.2f, target.Cell.Y);
            Vector3 direction = targetWorld - shooterWorld;

            if (direction.LengthSquared() < 0.0001f)
            {
                direction = new Vector3((float)Math.Sin(shooter.Orientation), 0.08f, (float)Math.Cos(shooter.Orientation));
            }

            direction.Y = Math.Max(direction.Y, 0.06f);
            direction.Normalize();
            return direction;
        }

        /// <summary>
        /// Donne l'XP de tir
        /// </summary>
        private void GiveShootingXP(Unit shooter, Unit target)
        {
            if (shooter.Team != Team.Player)
                return;

            int distance = Math.Abs(shooter.Cell.X - target.Cell.X) +
                          Math.Abs(shooter.Cell.Y - target.Cell.Y);

            if (shooter.WillHit)
            {
                int damage = Math.Max(GetWeaponDamage(shooter) - target.GetBallisticDamageReduction(), 1);
                shooter.Skills.GainShootingXP(true, distance, damage);
            }
            else
            {
                shooter.Skills.GainShootingXP(false, distance, 0);
            }
        }

        /// <summary>
        /// Obtient les cibles de tir valides
        /// </summary>
        public List<Unit> GetValidFireTargets(Unit shooter)
        {
            List<Unit> targets = new List<Unit>();

            if (shooter == null || !HasUsableWeapon(shooter))
                return targets;

            List<Unit> enemies = shooter.Team == Team.Player ? enemyUnits : playerUnits;

            foreach (var u in enemies)
            {
                int distance = Math.Abs(u.Cell.X - shooter.Cell.X) +
                              Math.Abs(u.Cell.Y - shooter.Cell.Y);

                if (distance <= GetWeaponRange(shooter) &&
                    pathfinding.HasLineOfSight(shooter.Cell, u.Cell))
                {
                    targets.Add(u);
                }
            }

            return targets;
        }

        // Events
        public event Action OnFireCompleted;
        public event Action<Unit, Vector3> OnUnitKilled;
        public event Action<Unit> OnShotFired;

        public void InitializeCoverSystem(int gridWidth, int gridHeight, HashSet<WallSegment> walls)
        {
            coverSystem = new CoverSystem(gridWidth, gridHeight, walls, getUnitAtCell);
            Console.WriteLine("[COMBAT] Cover system initialized");
        }

        public CoverSystem GetCoverSystem()
        {
            return coverSystem;
        }

        public bool TakeCover(Unit unit)
        {
            if (unit.ActionPoints <= 0)
            {
                Console.WriteLine($"[COMBAT] {unit.Name} has no AP to take cover");
                return false;
            }

            if (coverSystem == null)
            {
                Console.WriteLine("[COMBAT] Cover system not initialized!");
                return false;
            }

            CoverData cover = coverSystem.GetCoverAt(unit.Cell);

            if (cover.Type == CoverType.None)
            {
                Console.WriteLine($"[COMBAT] No cover available at {unit.Cell}");
                return false;
            }

            unit.EnterCover(cover);
            unit.ActionPoints--;

            Console.WriteLine($"[COMBAT] {unit.Name} takes {cover.Type} cover (Defense +{cover.DefenseBonus}%)");
            return true;
        }

        /// <summary>
        /// Met à jour automatiquement la couverture de l'unité selon sa cellule actuelle.
        /// </summary>
        public void UpdateUnitCover(Unit unit)
        {
            if (unit == null || coverSystem == null)
                return;

            CoverData cover = coverSystem.GetCoverAt(unit.Cell);

            if (cover.Type == CoverType.None)
            {
                LeaveCover(unit);
                return;
            }

            bool coverChanged = unit.CoverType != cover.Type || unit.CoverDirections != cover.Directions;
            if (coverChanged)
            {
                unit.EnterCover(cover);
                Console.WriteLine($"[COMBAT] {unit.Name} auto-updated to {cover.Type} cover");
            }
        }

        /// <summary>
        /// Met à jour automatiquement la couverture de toutes les unités vivantes.
        /// </summary>
        public void RefreshAllUnitsCover()
        {
            if (playerUnits == null || enemyUnits == null)
                return;

            foreach (var unit in playerUnits)
            {
                if (unit.Health > 0)
                    UpdateUnitCover(unit);
            }

            foreach (var unit in enemyUnits)
            {
                if (unit.Health > 0)
                    UpdateUnitCover(unit);
            }
        }


        public void LeaveCover(Unit unit)
        {
            if (unit.CoverType != CoverType.None)
            {
                unit.ExitCover();
                Console.WriteLine($"[COMBAT] {unit.Name} leaves cover");
            }
        }

        public List<Point> GetReachableCoverCells(Unit unit)
        {
            if (coverSystem == null || pathfinding == null)
                return new List<Point>();

            List<Point> movableCells = pathfinding.GetMovableCells(unit);

            List<Point> coverCells = new List<Point>();
            foreach (Point cell in movableCells)
            {
                CoverData cover = coverSystem.GetCoverAt(cell);
                if (cover.Type != CoverType.None)
                {
                    coverCells.Add(cell);
                }
            }

            return coverCells;
        }

        public Point? FindBestCoverForEnemy(Unit enemy)
        {
            if (coverSystem == null || pathfinding == null)
                return null;

            List<Point> movableCells = pathfinding.GetMovableCells(enemy);
            return coverSystem.GetBestCoverPosition(enemy.Cell, movableCells);
        }

    }
}
