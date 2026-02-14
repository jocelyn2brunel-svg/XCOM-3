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
        private OptimizedUnitManager unitManager;

        // État du tour
        public TurnState CurrentTurn { get; private set; } = TurnState.PlayerTurn;
        public int EnemyTurnIndex { get; private set; }
        public bool IsActionInProgress { get; set; }

        // Listes d'unités (références)
        private List<Unit> playerUnits;
        private List<Unit> enemyUnits;

        private CoverSystem coverSystem;
        private readonly Dictionary<Unit, int> targetPressureCache = new Dictionary<Unit, int>();

        public CombatSystem(Random random, PathfindingSystem pathfinding,
            Func<Point, Unit> getUnitAtCell, OptimizedUnitManager unitManager)
        {
            this.random = random;
            this.pathfinding = pathfinding;
            this.getUnitAtCell = getUnitAtCell;
            this.unitManager = unitManager;
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

        /// <summary>
        /// Démarre le tour du joueur
        /// </summary>
        public void StartPlayerTurn()
        {
            foreach (var u in playerUnits)
            {
                u.ActionPoints = u.MaxActionPoints; // ← Utiliser MaxActionPoints au lieu de 3
                u.RegenerateStamina(); // ← NOUVEAU : Régénérer la stamina
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
            }

            EnemyTurnIndex = 0;
            CurrentTurn = TurnState.EnemyTurn;
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
                EnemyTurnIndex++;
                return;
            }

            // Sélectionner une cible
            BuildTargetPressureCache(enemy);
            Unit target = SelectBestTarget(enemy);

            if (target == null)
            {
                EnemyTurnIndex++;
                return;
            }

            // 10% de chance de changer de cible aléatoirement
            if (random.Next(100) < 10 && playerUnits.Count > 1)
            {
                target = playerUnits[random.Next(playerUnits.Count)];
                Console.WriteLine($"[COMBAT] {enemy.Name} change de cible!");
            }

            int distance = Math.Abs(target.Cell.X - enemy.Cell.X) +
                          Math.Abs(target.Cell.Y - enemy.Cell.Y);

            // Tirer si possible
            if (distance <= enemy.WeaponData.Range &&
                pathfinding.HasLineOfSight(enemy.Cell, target.Cell))
            {
                float deltaX = target.Cell.X - enemy.Cell.X;
                float deltaZ = target.Cell.Y - enemy.Cell.Y;
                enemy.TargetOrientation = (float)Math.Atan2(deltaX, deltaZ);

                InitiateFire(enemy, target);
                return;
            }

            // Sinon se déplacer
            var path = pathfinding.FindPath(enemy.Cell, target.Cell, int.MaxValue, enemy);

            if (path.Count > 0)
            {
                int steps = Math.Min(enemy.GetMaxMoveRange(), path.Count);
                Point? validCell = null;

                for (int i = steps - 1; i >= 0; i--)
                {
                    if (getUnitAtCell(path[i]) == null)
                    {
                        validCell = path[i];
                        break;
                    }
                }

                if (validCell.HasValue)
                {
                    int targetIndex = path.IndexOf(validCell.Value);
                    if (targetIndex >= 0)
                    {
                        enemy.SetMovementStyle(1);
                        enemy.StartMoveAlongPath(path.GetRange(0, targetIndex + 1), cellSize);
                    }
                    else
                    {
                        enemy.SetMovementStyle(1);
                        enemy.StartMoveTo(validCell.Value, cellSize);
                    }
                    unitManager.OnUnitMoved(enemy, validCell.Value);
                    UpdateUnitCover(enemy);
                    enemy.ActionPoints--;
                }
                else
                {
                    TrySimpleMove(enemy, target, cellSize);
                }
            }
            else
            {
                TrySimpleMove(enemy, target, cellSize);
            }



            if (enemy.ActionPoints <= 0)
                EnemyTurnIndex++;
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
                bool hasLineOfSight = pathfinding.HasLineOfSight(enemy.Cell, player.Cell);

                // 1. Distance (plus proche = plus prioritaire)
                score += Math.Max(0, 100 - distance * 5);

                // 2. Ligne de vue et portée
                if (hasLineOfSight)
                    score += 50;
                if (distance <= enemy.WeaponData.Range && hasLineOfSight)
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
                score += player.WeaponData.Damage * 2;

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

            foreach (var enemy in enemyUnits)
            {
                Unit pendingTarget = enemy.PendingTarget;
                if (enemy == currentEnemy || pendingTarget == null)
                    continue;

                targetPressureCache.TryGetValue(pendingTarget, out int count);
                targetPressureCache[pendingTarget] = count + 1;
            }
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
                    enemy.StartMoveTo(move, cellSize);
                    unitManager.OnUnitMoved(enemy, move);
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
            if (shooter.ActionPoints <= 0)
                return;

            int distance = Math.Abs(target.Cell.X - shooter.Cell.X) +
                          Math.Abs(target.Cell.Y - shooter.Cell.Y);

            if (distance > shooter.WeaponData.Range)
                return;

            if (!pathfinding.HasLineOfSight(shooter.Cell, target.Cell))
                return;

            float deltaX = target.Cell.X - shooter.Cell.X;
            float deltaZ = target.Cell.Y - shooter.Cell.Y;
            shooter.TargetOrientation = (float)Math.Atan2(deltaX, deltaZ);

            IsActionInProgress = true;
            shooter.IsFiring = true;
            shooter.FireTarget = target.Cell;
            shooter.FireProgress = 0f;

            // ✅ CALCUL AVEC COUVERTURE
            int baseAccuracy = shooter.WeaponData.Accuracy + shooter.Skills.GetAccuracyBonus();
            int effectiveAccuracy = baseAccuracy - distance * 5;

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

            effectiveAccuracy = Math.Max(effectiveAccuracy, 5);

            shooter.WillHit = random.Next(100) < effectiveAccuracy;
            shooter.PendingTarget = target;
            shooter.ActionPoints--;

            Console.WriteLine($"[COMBAT] {shooter.Name} fires at {target.Name} ({effectiveAccuracy}% chance)");
        }

        /// <summary>
        /// Met à jour les animations de tir
        /// </summary>
        public void UpdateFiringAnimations(GameTime gameTime)
        {
            float fireSpeed = 3f;

            foreach (var u in playerUnits.Concat(enemyUnits))
            {
                if (!u.IsFiring || !u.FireTarget.HasValue)
                    continue;

                u.FireProgress += (float)gameTime.ElapsedGameTime.TotalSeconds * fireSpeed;

                if (u.FireProgress < 1f)
                    continue;

                // Fin du tir
                u.IsFiring = false;
                u.FireProgress = 0f;

                if (u.PendingTarget != null)
                {
                    if (u.WillHit)
                    {
                        ApplyDamage(u, u.PendingTarget);
                    }

                    GiveShootingXP(u, u.PendingTarget);

                    u.PendingTarget = null;
                    u.WillHit = false;
                }

                u.FireTarget = null;
                IsActionInProgress = false;

                OnFireCompleted?.Invoke();
                return;
            }
        }

        /// <summary>
        /// Applique les dégâts d'un tir
        /// </summary>
        private void ApplyDamage(Unit shooter, Unit target)
        {
            int baseDamage = shooter.WeaponData.Damage + shooter.Skills.GetDamageBonus();
            int damage = Math.Max(baseDamage - target.GetTotalArmor(), 1);

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

                OnUnitKilled?.Invoke(target);
            }
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
                int damage = Math.Max(shooter.WeaponData.Damage - target.GetTotalArmor(), 1);
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
            List<Unit> enemies = shooter.Team == Team.Player ? enemyUnits : playerUnits;

            foreach (var u in enemies)
            {
                int distance = Math.Abs(u.Cell.X - shooter.Cell.X) +
                              Math.Abs(u.Cell.Y - shooter.Cell.Y);

                if (distance <= shooter.WeaponData.Range &&
                    pathfinding.HasLineOfSight(shooter.Cell, u.Cell))
                {
                    targets.Add(u);
                }
            }

            return targets;
        }

        // Events
        public event Action OnFireCompleted;
        public event Action<Unit> OnUnitKilled;

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
