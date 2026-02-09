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

        /// <summary>
        /// Démarre le tour du joueur
        /// </summary>
        public void StartPlayerTurn()
        {
            foreach (var u in playerUnits)
            {
                u.ActionPoints = 3;
                u.MovementPoints = u.GetMaxMovementPoints();
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
                int steps = Math.Min(enemy.MovementPoints, path.Count);
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
                    enemy.StartMoveTo(validCell.Value, cellSize);
                    unitManager.OnUnitMoved(enemy, validCell.Value);
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

                // Distance
                score += Math.Max(0, 100 - distance * 5);

                // Ligne de vue
                if (pathfinding.HasLineOfSight(enemy.Cell, player.Cell))
                    score += 50;

                // À portée de tir
                if (distance <= enemy.WeaponData.Range && 
                    pathfinding.HasLineOfSight(enemy.Cell, player.Cell))
                    score += 75;

                // Cible blessée
                float healthPercent = (float)player.Health / player.MaxHealth;
                if (healthPercent < 0.5f)
                    score += (1.0f - healthPercent) * 30;

                // Éviter les cibles déjà ciblées
                int alreadyTargeting = enemyUnits.Count(e => e.PendingTarget == player);
                score -= alreadyTargeting * 20;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = player;
                }
            }

            return bestTarget;
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
                    !pathfinding.HasWallBetween(enemy.Cell, move) &&
                    getUnitAtCell(move) == null)
                {
                    enemy.StartMoveTo(move, cellSize);
                    unitManager.OnUnitMoved(enemy, move);
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

            IsActionInProgress = true;
            shooter.IsFiring = true;
            shooter.FireTarget = target.Cell;
            shooter.FireProgress = 0f;

            int baseAccuracy = shooter.WeaponData.Accuracy + shooter.Skills.GetAccuracyBonus();
            int effectiveAccuracy = Math.Max(baseAccuracy - distance * 5, 10);
            
            shooter.WillHit = random.Next(100) < effectiveAccuracy;
            shooter.PendingTarget = target;
            shooter.ActionPoints--;

            Console.WriteLine($"[COMBAT] {shooter.Name} tire sur {target.Name} ({effectiveAccuracy}% chance)");
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
    }
}
