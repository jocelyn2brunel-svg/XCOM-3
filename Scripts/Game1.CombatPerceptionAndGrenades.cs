using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public partial class Game1
    {
        private void UpdateEnemyPerceptionVisibility()
        {
            currentlySpottedEnemies.Clear();

            foreach (var enemy in enemyUnits)
            {
                bool spotted = playerUnits.Any(player => CanUnitPerceiveTarget(player, enemy));
                enemy.IsSpottedByPlayerTeam = spotted;

                if (spotted)
                    currentlySpottedEnemies.Add(enemy);
            }

            if (combatUI.SelectedFireTarget?.Team == Team.Enemy && !IsEnemyVisibleToPlayers(combatUI.SelectedFireTarget))
            {
                combatUI.SelectedFireTarget = null;
            }
        }

        private bool CanUnitPerceiveTarget(Unit observer, Unit target)
        {
            if (observer == null || target == null || pathfinding == null)
                return false;

            if (observer.Health <= 0 || target.Health <= 0)
                return false;

            if (observer.Floor != target.Floor)
                return false;

            float distanceCells = Vector2.Distance(new Vector2(observer.Cell.X, observer.Cell.Y), new Vector2(target.Cell.X, target.Cell.Y));
            if (distanceCells > GetEffectivePerceptionRange(observer))
                return false;

            // Vision 360°: pas de contrainte d'angle, uniquement portée + ligne de vue.
            return pathfinding.HasLineOfSight(observer.Cell, target.Cell);
        }

        private int GetEffectivePerceptionRange(Unit observer)
        {
            float basePerception = observer?.PerceptionRangeCells ?? 0;
            float lightMultiplier = MathHelper.Lerp(0.55f, 1.05f, CalculateSunIntensity(timeOfDay));
            float fatigueMultiplier = observer != null && observer.Phosphocreatine < observer.MaxPhosphocreatine * 0.25f ? 0.9f : 1f;
            return Math.Max(8, (int)Math.Round(basePerception * lightMultiplier * fatigueMultiplier));
        }

        private List<Unit> FilterTargetsByPerception(Unit shooter, List<Unit> targets)
        {
            if (targets == null)
                return new List<Unit>();

            if (shooter == null || shooter.Team != Team.Player)
                return targets;

            return targets.Where(t => t.Team != Team.Enemy || IsEnemyVisibleToPlayers(t) || CanUnitPerceiveTarget(shooter, t)).ToList();
        }

        private bool IsEnemyVisibleToPlayers(Unit enemy)
        {
            return enemy != null && enemy.IsSpottedByPlayerTeam && currentlySpottedEnemies.Contains(enemy);
        }

        private void InitializeGrenades()
        {
            grenadeDatabase = GrenadeDatabase.GetAllGrenades();

            // Ajouter quelques grenades disponibles dans l'inventaire
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Frag Grenade"], new Point(50, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["HE Grenade"], new Point(110, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Plasma Grenade"], new Point(170, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Smoke Grenade"], new Point(230, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Demolition Charge"], new Point(290, 300)));
        }

        private int GetUnitThrowRange(Unit unit)
        {
            if (unit == null)
                return BaseThrowRange;

            return BaseThrowRange + unit.Skills.GetGrenadeThrowRangeBonus();
        }

        private void HandleGrenadeThrow(MouseState mouse, bool leftClick)
        {
            if (selectedUnit == null || selectedGrenade == null) return;
            throwTarget = camera.GetCellFromMouse(
                mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                viewedFloor * cellSize);
            if (throwTarget.X >= 0)
            {
                explosionPreview = ThrowTrajectoryCalculator.GetExplosionPreview(throwTarget, selectedGrenade.Radius, gridWidth, gridHeight);
                Vector3 startPos = new Vector3(selectedUnit.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, selectedUnit.Cell.Y * cellSize + cellSize / 2f);
                Vector3 targetPos = new Vector3(throwTarget.X * cellSize + cellSize / 2f, 0, throwTarget.Y * cellSize + cellSize / 2f);
                trajectoryPreview = ThrowTrajectoryCalculator.CalculateArcPoints(startPos, targetPos);
            }
            int throwRange = GetUnitThrowRange(selectedUnit);
            if (leftClick && throwTarget.X >= 0 && ThrowTrajectoryCalculator.IsInThrowRange(selectedUnit.Cell, throwTarget, throwRange))
            {
                LaunchGrenade(selectedUnit, selectedGrenade, throwTarget, viewedFloor);
                selectedUnit.ActionPoints -= selectedGrenade.AOCost;
                selectedUnit.RemoveGrenade(selectedGrenade);
                CancelSelection();
            }
        }

        private void LaunchGrenade(Unit thrower, GrenadeData grenadeData, Point targetCell, int targetFloor)
        {
            Vector3 startPos = new Vector3(thrower.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, thrower.Cell.Y * cellSize + cellSize / 2f);
            Vector3 targetPos = new Vector3(targetCell.X * cellSize + cellSize / 2f, 0, targetCell.Y * cellSize + cellSize / 2f);
            Grenade grenade = new Grenade(grenadeData, startPos, targetPos, thrower, targetFloor);
            activeGrenades.Add(grenade);
            Console.WriteLine($"{thrower.Name} threw {grenadeData.Name} at {targetCell}");
        }

        private void UpdateGrenades(GameTime gameTime)
        {
            float grenadeSpeed = 2.5f;
            for (int i = activeGrenades.Count - 1; i >= 0; i--)
            {
                var grenade = activeGrenades[i];
                grenade.Progress += (float)gameTime.ElapsedGameTime.TotalSeconds * grenadeSpeed;
                if (grenade.Progress >= 1f)
                {
                    Point explosionCell = new Point((int)(grenade.TargetPosition.X / cellSize), (int)(grenade.TargetPosition.Z / cellSize));
                    TriggerExplosion(explosionCell, grenade.TargetFloor, grenade.Data, grenade.Thrower);
                    activeGrenades.RemoveAt(i);
                }
                else grenade.Position = grenade.GetCurrentPosition();
            }

            foreach (var crater in craters) crater.Age += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        private void TriggerExplosion(Point center, int centerFloor, GrenadeData grenadeData, Unit thrower = null)
        {
            Console.WriteLine($"EXPLOSION at {center} - {grenadeData.Name}");

            Vector3 explosionPos = new Vector3(
                center.X * cellSize + cellSize / 2f, // centre de la cellule X
                0,                                   // hauteur sol
                center.Y * cellSize + cellSize / 2f  // centre de la cellule Z
            );
            VisualEffects.PlayExplosion(explosionPos, grenadeData.Radius, renderer3D);

            int enemiesHit = 0, totalDamage = 0;

            if (grenadeData.Name == "MK 2")
            {
                ApplyMk2Explosion(center, thrower, ref enemiesHit, ref totalDamage);

                if (thrower != null && thrower.Team == Team.Player && enemiesHit > 0)
                {
                    thrower.Skills.GainGrenadeXP(enemiesHit, totalDamage);
                }

                return;
            }

            List<Unit> unitsToEvaluate = new List<Unit>(playerUnits.Count + enemyUnits.Count);
            unitsToEvaluate.AddRange(playerUnits);
            unitsToEvaluate.AddRange(enemyUnits);

            foreach (var unit in unitsToEvaluate)
            {
                float sphericalDistance = explosionManager.CalculateSphericalDistance(center, centerFloor, unit.Cell, unit.Floor);
                if (sphericalDistance > grenadeData.Radius)
                {
                    continue;
                }

                int damage = explosionManager.CalculateExplosionDamage(grenadeData.Damage, center, centerFloor, unit.Cell, unit.Floor, grenadeData.Radius);
                unit.Health = Math.Max(0, unit.Health - damage);
                Console.WriteLine($"{unit.Name} took {damage} explosion damage! HP: {unit.Health}");
                if (unit.Team == Team.Enemy && thrower != null && thrower.Team == Team.Player) { enemiesHit++; totalDamage += damage; }
                if (unit.Health <= 0)
                {
                    (unit.Team == Team.Player ? playerUnits : enemyUnits).Remove(unit);
                    unitManager.OnUnitDied(unit);
                    Console.WriteLine($"{unit.Name} killed by explosion!");
                }
            }

            if (grenadeData.DestroyWalls)
            {
                List<WallSegment> destroyedWalls = explosionManager.GetDestroyedWalls(wallSegments, center, grenadeData.Radius);
                if (destroyedWalls.Count > 0)
                {
                    foreach (var wall in destroyedWalls) wallSegments.Remove(wall);
                    unitManager.OnWallsDestroyed();
                    Console.WriteLine($"Destroyed {destroyedWalls.Count} walls - cache invalidated");
                }
            }

            if (thrower != null && thrower.Team == Team.Player && enemiesHit > 0) thrower.Skills.GainGrenadeXP(enemiesHit, totalDamage);

            if (grenadeData.DigsTerrain)
            {
                List<Crater> newCraters = explosionManager.CreateCraters(center, grenadeData.DigDepth, grenadeData.Radius);
                craters.AddRange(newCraters);
                Console.WriteLine($"Created {newCraters.Count} craters");
            }
        }

        private void ApplyMk2Explosion(Point center, Unit thrower, ref int enemiesHit, ref int totalDamage)
        {
            const float lethalRadius = 2f;
            const float fragmentationStart = 3f;
            const float fragmentationEnd = 9f;

            List<Unit> unitsToEvaluate = new List<Unit>(playerUnits.Count + enemyUnits.Count);
            unitsToEvaluate.AddRange(playerUnits);
            unitsToEvaluate.AddRange(enemyUnits);

            foreach (var unit in unitsToEvaluate)
            {
                float distance = Vector2.Distance(new Vector2(center.X, center.Y), new Vector2(unit.Cell.X, unit.Cell.Y));

                if (distance <= lethalRadius)
                {
                    KillUnitFromMk2(unit, "blast radius", thrower, ref enemiesHit, ref totalDamage);
                    continue;
                }

                if (distance < fragmentationStart || distance > fragmentationEnd)
                    continue;

                if (HasMk2FragmentationProtection(unit))
                    continue;

                float hitChance = 0.8f * (fragmentationEnd - distance) / (fragmentationEnd - fragmentationStart);
                hitChance = MathHelper.Clamp(hitChance, 0f, 0.8f);

                if (random.NextDouble() <= hitChance)
                {
                    KillUnitFromMk2(unit, $"fragmentation ({hitChance * 100f:0}% chance)", thrower, ref enemiesHit, ref totalDamage);
                }
            }
        }

        private bool HasMk2FragmentationProtection(Unit unit)
        {
            bool hasFragmentationHelmet = unit.EquippedHelmet?.Data?.ProtectionLevel >= ProtectionLevel.Fragmentation;
            bool hasFragmentationSuit = unit.EquippedArmor?.Data?.ProtectionLevel >= ProtectionLevel.Fragmentation;
            return hasFragmentationHelmet && hasFragmentationSuit;
        }

        private void KillUnitFromMk2(Unit unit, string reason, Unit thrower, ref int enemiesHit, ref int totalDamage)
        {
            int hpBefore = unit.Health;
            unit.Health = 0;
            Console.WriteLine($"{unit.Name} killed by MK 2 {reason}.");

            if (unit.Team == Team.Enemy && thrower != null && thrower.Team == Team.Player)
            {
                enemiesHit++;
                totalDamage += hpBefore;
            }

            (unit.Team == Team.Player ? playerUnits : enemyUnits).Remove(unit);
            unitManager.OnUnitDied(unit);
        }

        private void DrawThrowMode3D(GameTime gameTime)
        {
            if (!throwMode) return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.3f + 0.7f;
            float floorYOffset = viewedFloor * cellSize;

            renderer3D.DrawZoneOutline(
                throwableCells,
                cellSize,
                floorYOffset + 0.05f,
                new Color(255, 220, 90, 225) * pulse);

            bool isMk2 = string.Equals(selectedGrenade?.Name, "MK 2", StringComparison.OrdinalIgnoreCase);
            if (isMk2 && throwTarget.X >= 0 && throwTarget.Y >= 0)
            {
                DrawVolumetricGrenadeGhost(throwTarget, 0f, 2f, new Color(255, 40, 40, 60) * pulse);
                DrawVolumetricGrenadeGhost(throwTarget, 3f, 9f, new Color(255, 235, 80, 40) * pulse);
            }

            renderer3D.DrawZoneOutline(
                explosionPreview,
                cellSize,
                floorYOffset + 0.07f,
                new Color(255, 70, 70, 235) * pulse);

            for (int i = 0; i < trajectoryPreview.Count - 1; i++)
            {
                Vector3 a = trajectoryPreview[i];
                Vector3 b = trajectoryPreview[i + 1];
                float dist = Vector3.Distance(a, b);
                int steps = Math.Max(1, (int)(dist / (cellSize * 0.05f)));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 p = Vector3.Lerp(a, b, t);
                    renderer3D.DrawCube(p, new Vector3(cellSize * 0.08f), Color.White * 0.85f);
                }
            }
        }

        private void DrawVolumetricGrenadeGhost(Point centerCell, float minRadius, float maxRadius, Color color)
        {
            int minX = Math.Max(0, centerCell.X - (int)Math.Ceiling(maxRadius));
            int maxX = Math.Min(gridWidth - 1, centerCell.X + (int)Math.Ceiling(maxRadius));
            int minY = Math.Max(0, centerCell.Y - (int)Math.Ceiling(maxRadius));
            int maxY = Math.Min(gridHeight - 1, centerCell.Y + (int)Math.Ceiling(maxRadius));

            float floorYOffset = viewedFloor * cellSize;
            float ghostHeight = cellSize * 1.35f;
            HashSet<Point> zoneCells = new HashSet<Point>();

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    float distance = Vector2.Distance(new Vector2(centerCell.X, centerCell.Y), new Vector2(x, y));
                    if (distance < minRadius || distance > maxRadius)
                        continue;

                    zoneCells.Add(new Point(x, y));
                }
            }

            if (zoneCells.Count == 0)
                return;

            RasterizerState previousRasterizer = GraphicsDevice.RasterizerState;
            BlendState previousBlend = GraphicsDevice.BlendState;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.RasterizerState = hoveredCellWireframeState;

            Point[] cardinalDirections =
            {
                new Point(0, -1),
                new Point(1, 0),
                new Point(0, 1),
                new Point(-1, 0)
            };

            foreach (Point cell in zoneCells)
            {
                bool isOuterBoundary = false;

                foreach (Point direction in cardinalDirections)
                {
                    Point neighbor = new Point(cell.X + direction.X, cell.Y + direction.Y);

                    if (neighbor.X < 0 || neighbor.X >= gridWidth || neighbor.Y < 0 || neighbor.Y >= gridHeight)
                    {
                        isOuterBoundary = true;
                        break;
                    }

                    if (zoneCells.Contains(neighbor))
                        continue;

                    float neighborDistance = Vector2.Distance(
                        new Vector2(centerCell.X, centerCell.Y),
                        new Vector2(neighbor.X, neighbor.Y));

                    if (neighborDistance > maxRadius)
                    {
                        isOuterBoundary = true;
                        break;
                    }
                }

                if (!isOuterBoundary)
                    continue;

                Vector3 center = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    floorYOffset + ghostHeight / 2f,
                    cell.Y * cellSize + cellSize / 2f);

                renderer3D.DrawCube(
                    center,
                    new Vector3(cellSize * 0.82f, ghostHeight, cellSize * 0.82f),
                    color);
            }

            GraphicsDevice.RasterizerState = previousRasterizer;
            GraphicsDevice.BlendState = previousBlend;
        }
    }
}
