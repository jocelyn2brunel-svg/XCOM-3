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
        private const float Mk2LethalRadius = 2f;
        private const float Mk2FragmentationStartRadius = 3f;
        private const float Mk2FragmentationEndRadius = 9f;
        private const int BaseThrowAccuracyPercent = 92;
        private const int ThrowDistancePenaltyPercentPerCell = 7;

        private readonly struct GrappleAnchor
        {
            public Point DestinationCell { get; }
            public int DestinationFloor { get; }
            public WallSegment Wall { get; }

            public GrappleAnchor(Point destinationCell, int destinationFloor, WallSegment wall)
            {
                DestinationCell = destinationCell;
                DestinationFloor = destinationFloor;
                Wall = wall;
            }
        }

        private static IEnumerable<Point> GetCellsAdjacentToWall(WallSegment wall)
        {
            Point sideA = wall.IsHorizontal
                ? new Point(Math.Min(wall.Start.X, wall.End.X), wall.Start.Y - 1)
                : new Point(wall.Start.X - 1, Math.Min(wall.Start.Y, wall.End.Y));

            Point sideB = wall.IsHorizontal
                ? new Point(Math.Min(wall.Start.X, wall.End.X), wall.Start.Y)
                : new Point(wall.Start.X, Math.Min(wall.Start.Y, wall.End.Y));

            yield return sideA;
            yield return sideB;
        }

        private bool CellHasSupportingWall(Point cell, int floor)
        {
            HashSet<WallSegment> wallsOnFloor = GetWallsForFloor(floor);
            return wallsOnFloor.Any(wall => wall.Type == WallType.Full && GetCellsAdjacentToWall(wall).Contains(cell));
        }

        private int GetGrappleActionPointCost(int floorsClimbed, bool hasSupportingWall)
        {
            int apPerFloor = hasSupportingWall ? 1 : 2;
            return Math.Max(1, floorsClimbed) * apPerFloor;
        }

        private readonly struct Mk2FragmentationPreviewInfo
        {
            public Unit Unit { get; }
            public int HitChancePercent { get; }
            public Vector3 UnitWorldCenter { get; }

            public Mk2FragmentationPreviewInfo(Unit unit, int hitChancePercent, Vector3 unitWorldCenter)
            {
                Unit = unit;
                HitChancePercent = hitChancePercent;
                UnitWorldCenter = unitWorldCenter;
            }
        }

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

            // Garder uniquement la MK 2 dans l'inventaire disponible
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["MK 2"], new Point(50, 300)));
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
                WorldMetrics.FloorToWorldY(viewedFloor, cellSize));
            if (throwTarget.X >= 0)
            {
                if (throwModeUsesFlashlight)
                    explosionPreview.Clear();
                else
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

                if (throwModeUsesFlashlight)
                {
                    if (throwFlashlightFromRightHand)
                    {
                        selectedUnit.EquippedRightHandFlashlight = null;
                        selectedUnit.IsRightHandFlashlightOn = false;
                    }
                    else
                    {
                        selectedUnit.EquippedLeftHandFlashlight = null;
                        selectedUnit.IsLeftHandFlashlightOn = false;
                    }

                    Console.WriteLine($"{selectedUnit.Name} threw tactical flashlight at {throwTarget}");
                }
                else
                {
                    selectedUnit.RemoveGrenade(selectedGrenade);
                }

                CancelSelection();
            }
        }

        private bool UnitHasGrapplingHookEquipped(Unit unit)
        {
            return string.Equals(unit?.EquippedRightHandFlashlight?.Data?.Name, GrapplingHookItemName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(unit?.EquippedLeftHandFlashlight?.Data?.Name, GrapplingHookItemName, StringComparison.OrdinalIgnoreCase);
        }

        private int GetGrappleThrowStrengthFeet(Unit unit)
        {
            int strengthBonus = unit?.Skills?[SkillType.Strength]?.Level ?? 0;
            int throwStrengthFeet = GrappleBaseThrowStrengthFeet + strengthBonus;
            return Math.Clamp(throwStrengthFeet, GrappleBaseThrowStrengthFeet, GrappleMaxThrowStrengthFeet);
        }

        private int GetGrappleDexterityAccuracyBonus(Unit unit)
        {
            return Math.Max(0, (unit?.Skills?[SkillType.WeaponHandling]?.Level ?? 0) * 3);
        }

        private void ActivateGrappleMode()
        {
            if (selectedUnit == null || selectedUnit.Team != Team.Player)
                return;

            if (!UnitHasGrapplingHookEquipped(selectedUnit) || selectedUnit.ActionPoints < GrappleActionPointCost)
            {
                Console.WriteLine("[GRAPPLIN] Impossible d'activer le grappin (équipement/AP insuffisant).");
                return;
            }

            int maxFloorsUp = Math.Max(1, GetGrappleThrowStrengthFeet(selectedUnit) / GrappleFloorHeightFeet);
            int mapTopFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);
            grappleTargetFloor = Math.Min(mapTopFloor, selectedUnit.Floor + maxFloorsUp);
            if (grappleTargetFloor <= selectedUnit.Floor)
            {
                Console.WriteLine("[GRAPPLIN] Aucun étage supérieur accessible depuis cette position.");
                return;
            }

            grappleAnchors = GetValidGrappleAnchors(selectedUnit, grappleTargetFloor);
            if (grappleAnchors.Count == 0)
            {
                Console.WriteLine("[GRAPPLIN] Aucun point d'ancrage accessible (fenêtre/demi-mur). ");
                return;
            }

            throwMode = false;
            grappleMode = true;
            viewedFloor = grappleTargetFloor;
            Console.WriteLine($"[GRAPPLIN] Mode activé: {grappleAnchors.Count} points d'ancrage à l'étage {grappleTargetFloor}.");
        }

        private List<GrappleAnchor> GetValidGrappleAnchors(Unit unit, int destinationFloor)
        {
            var anchors = new List<GrappleAnchor>();
            if (unit == null || destinationFloor <= unit.Floor)
                return anchors;

            HashSet<Point> validFloorCells = GetCellsForFloor(destinationFloor);
            HashSet<WallSegment> wallsOnFloor = GetWallsForFloor(destinationFloor);

            foreach (var wall in wallsOnFloor)
            {
                if (wall.Type == WallType.Full)
                    continue;

                foreach (Point candidate in GetCellsAdjacentToWall(wall))
                {
                    if (!validFloorCells.Contains(candidate))
                        continue;

                    if (!IsCellAvailableOnFloor(candidate, destinationFloor))
                        continue;

                    int manhattan = Math.Abs(candidate.X - unit.Cell.X) + Math.Abs(candidate.Y - unit.Cell.Y);
                    if (manhattan > 8)
                        continue;

                    anchors.Add(new GrappleAnchor(candidate, destinationFloor, wall));
                }
            }

            return anchors
                .GroupBy(a => a.DestinationCell)
                .Select(g => g.First())
                .ToList();
        }

        private void HandleGrappleAction(MouseState mouse, bool leftClick)
        {
            if (!grappleMode || selectedUnit == null)
                return;

            Point hovered = camera.GetCellFromMouse(
                mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                WorldMetrics.FloorToWorldY(grappleTargetFloor, cellSize));

            if (hovered.X >= 0)
                hoveredCell = hovered;

            if (!leftClick || hovered.X < 0)
                return;

            int anchorIndex = grappleAnchors.FindIndex(a => a.DestinationCell == hovered);
            if (anchorIndex < 0)
                return;

            GrappleAnchor selectedAnchor = grappleAnchors[anchorIndex];
            int floorsClimbed = Math.Max(1, selectedAnchor.DestinationFloor - selectedUnit.Floor);
            bool hasSupportingWall = CellHasSupportingWall(selectedAnchor.DestinationCell, selectedAnchor.DestinationFloor);
            int grappleApCost = GetGrappleActionPointCost(floorsClimbed, hasSupportingWall);

            if (selectedUnit.ActionPoints < grappleApCost)
            {
                Console.WriteLine($"[GRAPPLIN] AP insuffisants: coût {grappleApCost} AP pour {floorsClimbed} case(s) {(hasSupportingWall ? "avec appui" : "sans appui")}. ");
                ExitGrappleMode();
                return;
            }

            int hitChance = Math.Clamp(
                GrappleBaseAccuracyPercent
                + GetGrappleDexterityAccuracyBonus(selectedUnit)
                - floorsClimbed * GrappleHeightPenaltyPercentPerFloor,
                20,
                98);

            int roll = random.Next(100);
            if (roll >= hitChance)
            {
                Console.WriteLine($"[GRAPPLIN] Échec ({hitChance}% / roll {roll}).");
                selectedUnit.ActionPoints = Math.Max(0, selectedUnit.ActionPoints - grappleApCost);
                ExitGrappleMode();
                return;
            }

            selectedUnit.Cell = selectedAnchor.DestinationCell;
            selectedUnit.Floor = selectedAnchor.DestinationFloor;
            selectedUnit.VisualPosition = new Vector3(
                selectedUnit.Cell.X * cellSize + cellSize / 2f,
                WorldMetrics.FloorToWorldY(selectedUnit.Floor, cellSize),
                selectedUnit.Cell.Y * cellSize + cellSize / 2f);
            selectedUnit.ActionPoints = Math.Max(0, selectedUnit.ActionPoints - grappleApCost);
            unitManager.OnUnitMoved(selectedUnit, selectedUnit.Cell, selectedUnit.Floor);
            combatSystem.UpdateUnitCover(selectedUnit);

            Console.WriteLine($"[GRAPPLIN] {selectedUnit.Name} atteint {selectedUnit.Cell} à l'étage {selectedUnit.Floor}.");
            ExitGrappleMode();
        }

        private void DrawGrappleMode3D(GameTime gameTime)
        {
            if (!grappleMode || grappleAnchors.Count == 0)
                return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f) * 0.25f + 0.75f;
            float floorYOffset = WorldMetrics.FloorToWorldY(grappleTargetFloor, cellSize);

            renderer3D.DrawZoneOutline(
                grappleAnchors.Select(a => a.DestinationCell).ToList(),
                cellSize,
                floorYOffset + 0.09f,
                new Color(80, 240, 255, 240) * pulse);
        }

        private void LaunchGrenade(Unit thrower, GrenadeData grenadeData, Point targetCell, int targetFloor)
        {
            Point actualLandingCell = ResolveThrowLandingCell(thrower, targetCell);

            Vector3 startPos = new Vector3(thrower.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, thrower.Cell.Y * cellSize + cellSize / 2f);
            Vector3 targetPos = new Vector3(actualLandingCell.X * cellSize + cellSize / 2f, 0, actualLandingCell.Y * cellSize + cellSize / 2f);
            Grenade grenade = new Grenade(grenadeData, startPos, targetPos, thrower, targetFloor);
            activeGrenades.Add(grenade);
            if (actualLandingCell != targetCell)
            {
                Console.WriteLine($"{thrower.Name} missed throw target {targetCell}, {grenadeData.Name} landed at {actualLandingCell}.");
            }
            else
            {
                Console.WriteLine($"{thrower.Name} threw {grenadeData.Name} at {targetCell}");
            }
        }

        private Point ResolveThrowLandingCell(Unit thrower, Point desiredTargetCell)
        {
            if (thrower == null)
                return desiredTargetCell;

            int throwDistance = Math.Abs(desiredTargetCell.X - thrower.Cell.X) + Math.Abs(desiredTargetCell.Y - thrower.Cell.Y);
            int distancePenalty = Math.Max(0, throwDistance - 1) * ThrowDistancePenaltyPercentPerCell;
            int throwSkillBonus = thrower.Skills?.GetThrowAccuracyBonus() ?? 0;

            int hitChance = Math.Clamp(BaseThrowAccuracyPercent - distancePenalty + throwSkillBonus, 10, 98);
            int roll = random.Next(100);

            if (roll < hitChance)
                return desiredTargetCell;

            List<Point> adjacentCells = new List<Point>();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int x = desiredTargetCell.X + dx;
                    int y = desiredTargetCell.Y + dy;

                    if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                        continue;

                    adjacentCells.Add(new Point(x, y));
                }
            }

            if (adjacentCells.Count == 0)
                return desiredTargetCell;

            Point scatteredCell = adjacentCells[random.Next(adjacentCells.Count)];
            Console.WriteLine($"Throw deviation: distance={throwDistance}, chance={hitChance}%, roll={roll} -> {scatteredCell}");
            return scatteredCell;
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

                    if (string.Equals(grenade.Data?.Name, TacticalFlashlightItemName, StringComparison.OrdinalIgnoreCase))
                    {
                        RegisterFlashlightLoot(explosionCell, grenade.TargetFloor);
                    }

                    TriggerExplosion(explosionCell, grenade.TargetFloor, grenade.Data, grenade.Thrower);
                    activeGrenades.RemoveAt(i);
                }
                else grenade.Position = grenade.GetCurrentPosition();
            }

            foreach (var crater in craters) crater.Age += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }


        private void RegisterFlashlightLoot(Point lootCell, int lootFloor)
        {
            bool addedToNearbyLoot = RegisterGroundLoot(TacticalFlashlightItemName, lootCell, lootFloor);
            Console.WriteLine(addedToNearbyLoot
                ? $"Flashlight landed at {lootCell} (floor {lootFloor}) and added to nearby loot."
                : $"Flashlight landed at {lootCell} (floor {lootFloor}) but could not be added to nearby loot.");
        }

        private bool RegisterGroundLoot(string itemName, Point lootCell, int lootFloor)
        {
            bool mergedWithExistingCell = false;

            for (int i = 0; i < flashlightLootMarkers.Count; i++)
            {
                FlashlightLootMarker marker = flashlightLootMarkers[i];
                if (marker.Cell != lootCell || marker.Floor != lootFloor)
                    continue;

                marker.Quantity += 1;
                marker.PulseSeed = (float)(random.NextDouble() * MathHelper.TwoPi);
                flashlightLootMarkers[i] = marker;
                mergedWithExistingCell = true;
                break;
            }

            if (!mergedWithExistingCell)
            {
                flashlightLootMarkers.Add(new FlashlightLootMarker
                {
                    Cell = lootCell,
                    Floor = lootFloor,
                    Quantity = 1,
                    PulseSeed = (float)(random.NextDouble() * MathHelper.TwoPi)
                });
            }

            return inventorySystem.TryAddNearbyLootByName(itemName);
        }

        private void DrawFlashlightLootHighlights(GameTime gameTime)
        {
            if (flashlightLootMarkers.Count == 0)
                return;

            float gameSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
            float floorYOffset = WorldMetrics.FloorToWorldY(viewedFloor, cellSize);

            foreach (FlashlightLootMarker marker in flashlightLootMarkers)
            {
                if (marker.Floor != viewedFloor || marker.Quantity <= 0)
                    continue;

                float pulse = 0.58f + 0.42f * (float)Math.Sin(gameSeconds * 6f + marker.PulseSeed);
                Color pulseColor = new Color(255, 230, 80, 235) * pulse;

                renderer3D.DrawZoneOutline(new[] { marker.Cell }, cellSize, floorYOffset + 0.09f, pulseColor);
            }
        }
        private void TriggerExplosion(Point center, int centerFloor, GrenadeData grenadeData, Unit thrower = null)
        {
            if (string.Equals(grenadeData?.Name, TacticalFlashlightItemName, StringComparison.OrdinalIgnoreCase))
                return;

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
                ApplyMk2Explosion(center, centerFloor, thrower, ref enemiesHit, ref totalDamage);

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

                if (!IsUnitExposedToExplosion(center, centerFloor, unit))
                {
                    continue;
                }

                int damage = explosionManager.CalculateExplosionDamage(grenadeData.Damage, center, centerFloor, unit.Cell, unit.Floor, grenadeData.Radius);
                unit.Health = Math.Max(0, unit.Health - damage);
                Console.WriteLine($"{unit.Name} took {damage} explosion damage! HP: {unit.Health}");
                if (unit.Team == Team.Enemy && thrower != null && thrower.Team == Team.Player) { enemiesHit++; totalDamage += damage; }
                if (unit.Health <= 0)
                {
                    PlayGrenadeFatalityEffect(unit, grenadeData);
                    HandleUnitKilled(unit);
                    Console.WriteLine($"{unit.Name} killed by explosion!");
                }
            }

            if (grenadeData.DestroyWalls)
            {
                List<WallSegment> destroyedWalls = explosionManager.GetDestroyedWalls(wallSegments, center, grenadeData.Radius);
                if (destroyedWalls.Count > 0)
                {
                    foreach (var wall in destroyedWalls) wallSegments.Remove(wall);
                    InvalidateWallsByFloorCache();
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

        private void ApplyMk2Explosion(Point center, int centerFloor, Unit thrower, ref int enemiesHit, ref int totalDamage)
        {
            List<Unit> unitsToEvaluate = new List<Unit>(playerUnits.Count + enemyUnits.Count);
            unitsToEvaluate.AddRange(playerUnits);
            unitsToEvaluate.AddRange(enemyUnits);

            foreach (var unit in unitsToEvaluate)
            {
                float distance = Vector2.Distance(new Vector2(center.X, center.Y), new Vector2(unit.Cell.X, unit.Cell.Y));

                if (!IsUnitExposedToExplosion(center, centerFloor, unit))
                    continue;

                if (distance > Mk2FragmentationEndRadius)
                    continue;

                if (!TryGetMk2FragmentHitChancePercent(center, centerFloor, unit, out int hitChancePercent))
                    continue;

                float hitChance = hitChancePercent / 100f;

                if (random.NextDouble() <= hitChance)
                {
                    KillUnitFromMk2(unit, $"fragmentation ({hitChance * 100f:0}% chance)", thrower, ref enemiesHit, ref totalDamage);
                }
            }
        }

        private bool IsUnitExposedToExplosion(Point explosionCenter, int explosionFloor, Unit unit)
        {
            if (unit == null)
                return false;

            if (unit.Cell == explosionCenter && unit.Floor == explosionFloor)
                return true;

            if (pathfinding == null)
                return true;

            if (unit.Floor != explosionFloor)
                return true;

            return pathfinding.HasLineOfSight(explosionCenter, unit.Cell);
        }

        private int GetMk2FragmentationProtectionReductionPercent(Unit unit)
        {
            if (unit == null)
                return 0;

            int totalReduction = 0;
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedHelmet);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedNeck);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedArmor);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedShield);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedShirt);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedPants);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedKnees);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedFeet);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedChestRig);
            totalReduction += GetItemFragmentationProtectionPercent(unit.EquippedBelt);

            if (!string.IsNullOrWhiteSpace(unit.EquippedBackpack) &&
                inventorySystem?.ItemDatabase != null &&
                inventorySystem.ItemDatabase.TryGetValue(unit.EquippedBackpack, out ItemData backpackData))
            {
                totalReduction += GetItemFragmentationProtectionPercent(new Item(backpackData, Point.Zero));
            }

            return Math.Clamp(totalReduction, 0, 95);
        }

        private int GetItemFragmentationProtectionPercent(Item item)
        {
            ItemData data = item?.Data;
            if (data == null)
                return 0;

            return data.GetEffectiveFragmentationProtectionPercent();
        }

        private bool TryGetMk2FragmentHitChancePercent(Point centerCell, int centerFloor, Unit unit, out int hitChancePercent)
        {
            hitChancePercent = 0;

            if (unit == null || unit.Health <= 0)
                return false;

            float distance = Vector2.Distance(new Vector2(centerCell.X, centerCell.Y), new Vector2(unit.Cell.X, unit.Cell.Y));
            if (distance > Mk2FragmentationEndRadius)
                return false;

            if (!IsUnitExposedToExplosion(centerCell, centerFloor, unit))
                return false;

            float baseHitChance = distance <= Mk2LethalRadius
                ? 1f
                : 0.8f * (Mk2FragmentationEndRadius - distance) / (Mk2FragmentationEndRadius - Mk2FragmentationStartRadius);

            baseHitChance = MathHelper.Clamp(baseHitChance, 0f, 1f);

            float protectionMultiplier = 1f - (GetMk2FragmentationProtectionReductionPercent(unit) / 100f);
            protectionMultiplier = MathHelper.Clamp(protectionMultiplier, 0.05f, 1f);

            float finalHitChance = MathHelper.Clamp(baseHitChance * protectionMultiplier, 0f, 1f);
            hitChancePercent = (int)Math.Round(finalHitChance * 100f);
            return hitChancePercent > 0;
        }

        private void KillUnitFromMk2(Unit unit, string reason, Unit thrower, ref int enemiesHit, ref int totalDamage)
        {
            int hpBefore = unit.Health;
            unit.Health = 0;
            Console.WriteLine($"{unit.Name} killed by MK 2 {reason}.");

            PlayGrenadeFatalityEffect(unit, null);

            if (unit.Team == Team.Enemy && thrower != null && thrower.Team == Team.Player)
            {
                enemiesHit++;
                totalDamage += hpBefore;
            }

            HandleUnitKilled(unit);
        }

        private void PlayGrenadeFatalityEffect(Unit unit, GrenadeData grenadeData)
        {
            if (unit == null)
                return;

            float floorY = WorldMetrics.FloorToWorldY(unit.Floor, cellSize);
            Vector3 bodyCenter = new Vector3(
                unit.Cell.X * cellSize + cellSize * 0.5f,
                floorY + cellSize * 0.85f,
                unit.Cell.Y * cellSize + cellSize * 0.5f);

            float goreForce = grenadeData?.Radius ?? Mk2FragmentationEndRadius;
            goreForce = MathHelper.Clamp(goreForce, 2f, 6f);

            VisualEffects.PlayGibExplosion(bodyCenter, goreForce, renderer3D);
        }

        private void DrawThrowMode3D(GameTime gameTime)
        {
            if (!throwMode) return;

            if (selectedUnit != null)
            {
                int throwRange = GetUnitThrowRange(selectedUnit);
                throwableCells = ThrowTrajectoryCalculator.GetThrowableCells(selectedUnit.Cell, throwRange, gridWidth, gridHeight);
            }

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.3f + 0.7f;
            float floorYOffset = WorldMetrics.FloorToWorldY(viewedFloor, cellSize);

            renderer3D.DrawZoneOutline(
                throwableCells,
                cellSize,
                floorYOffset + 0.05f,
                new Color(255, 220, 90, 225) * pulse);

            bool isMk2 = string.Equals(selectedGrenade?.Name, "MK 2", StringComparison.OrdinalIgnoreCase);
            if (isMk2 && throwTarget.X >= 0 && throwTarget.Y >= 0)
            {
                DrawVolumetricGrenadeGhost(throwTarget, 0f, Mk2LethalRadius, new Color(255, 40, 40, 60) * pulse);
                DrawVolumetricGrenadeGhost(throwTarget, Mk2FragmentationStartRadius, Mk2FragmentationEndRadius, new Color(255, 235, 80, 40) * pulse);
                DrawMk2FragmentationDirectionPreview(throwTarget, viewedFloor, pulse);
            }

            if (!throwModeUsesFlashlight)
            {
                renderer3D.DrawZoneOutline(
                    explosionPreview,
                    cellSize,
                    floorYOffset + 0.07f,
                    new Color(255, 70, 70, 235) * pulse);
            }

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

        private List<Mk2FragmentationPreviewInfo> GetMk2FragmentationPreviewInfos(Point centerCell, int centerFloor)
        {
            List<Mk2FragmentationPreviewInfo> result = new List<Mk2FragmentationPreviewInfo>();

            void AddUnits(List<Unit> units)
            {
                foreach (Unit unit in units)
                {
                    if (unit == null || unit.Health <= 0 || unit.Floor != viewedFloor)
                        continue;

                    if (unit.Team == Team.Enemy && !IsEnemyVisibleToPlayers(unit))
                        continue;

                    if (!TryGetMk2FragmentHitChancePercent(centerCell, centerFloor, unit, out int hitChancePercent))
                        continue;

                    Vector3 worldCenter = new Vector3(
                        unit.Cell.X * cellSize + cellSize / 2f,
                        WorldMetrics.FloorToWorldY(unit.Floor, cellSize) + cellSize * 0.95f,
                        unit.Cell.Y * cellSize + cellSize / 2f);

                    result.Add(new Mk2FragmentationPreviewInfo(unit, hitChancePercent, worldCenter));
                }
            }

            AddUnits(playerUnits);
            AddUnits(enemyUnits);

            return result;
        }

        private void DrawMk2FragmentationDirectionPreview(Point centerCell, int centerFloor, float pulse)
        {
            List<Mk2FragmentationPreviewInfo> infos = GetMk2FragmentationPreviewInfos(centerCell, centerFloor);
            if (infos.Count == 0)
                return;

            Vector3 blastOrigin = new Vector3(
                centerCell.X * cellSize + cellSize / 2f,
                WorldMetrics.FloorToWorldY(centerFloor, cellSize) + cellSize * 0.12f,
                centerCell.Y * cellSize + cellSize / 2f);

            foreach (Mk2FragmentationPreviewInfo info in infos)
            {
                Color rayColor = GetMk2PreviewChanceColor(info.HitChancePercent) * (0.55f + 0.35f * pulse);
                Vector3 start = blastOrigin;
                Vector3 end = info.UnitWorldCenter;

                renderer3D.DrawLine(start, end, rayColor);

                Vector3 direction = end - start;
                if (direction.LengthSquared() > 0.0001f)
                {
                    direction.Normalize();
                    Vector3 arrowHead = end + direction * (cellSize * 0.15f);
                    renderer3D.DrawCube(arrowHead, new Vector3(cellSize * 0.14f), rayColor);
                }
            }
        }

        private Color GetMk2PreviewChanceColor(int hitChancePercent)
        {
            if (hitChancePercent >= 60)
                return new Color(255, 70, 70, 225);

            if (hitChancePercent >= 35)
                return new Color(255, 170, 70, 220);

            return new Color(255, 230, 120, 210);
        }

        private void DrawMk2FragmentationHitChanceLabels()
        {
            if (!throwMode || throwModeUsesFlashlight)
                return;

            bool isMk2 = string.Equals(selectedGrenade?.Name, "MK 2", StringComparison.OrdinalIgnoreCase);
            if (!isMk2 || throwTarget.X < 0 || throwTarget.Y < 0)
                return;

            List<Mk2FragmentationPreviewInfo> infos = GetMk2FragmentationPreviewInfos(throwTarget, viewedFloor);
            if (infos.Count == 0)
                return;

            foreach (Mk2FragmentationPreviewInfo info in infos)
            {
                Vector3 worldTextAnchor = info.UnitWorldCenter + new Vector3(0f, cellSize * 0.95f, 0f);
                Vector3 projected = GraphicsDevice.Viewport.Project(
                    worldTextAnchor,
                    camera.ProjectionMatrix,
                    camera.ViewMatrix,
                    Matrix.Identity);

                if (projected.Z <= 0f || projected.Z >= 1f)
                    continue;

                string label = $"Frag {info.HitChancePercent}%";
                Vector2 textSize = font.MeasureString(label);
                Vector2 panelSize = textSize + new Vector2(14f, 8f) * 2f;
                Vector2 panelPos = new Vector2(
                    projected.X - panelSize.X / 2f,
                    projected.Y - panelSize.Y - 10f);

                Rectangle panelRect = new Rectangle(
                    (int)panelPos.X,
                    (int)panelPos.Y,
                    (int)panelSize.X,
                    (int)panelSize.Y);

                _spriteBatch.Draw(pixel, panelRect, new Color(20, 22, 20, 210));
                DrawPanelBorder(panelRect, GetMk2PreviewChanceColor(info.HitChancePercent));
                _spriteBatch.DrawString(font, label, panelPos + new Vector2(14f, 8f), new Color(250, 250, 230));
            }
        }

        private void DrawVolumetricGrenadeGhost(Point centerCell, float minRadius, float maxRadius, Color color)
        {
            float ghostHeight = cellSize * 1.35f;
            List<Unit> unitsInZone = new List<Unit>();

            void AddUnitsWithinZone(List<Unit> units)
            {
                foreach (Unit unit in units)
                {
                    if (unit == null || unit.Health <= 0 || unit.Floor != viewedFloor)
                        continue;

                    float distance = Vector2.Distance(
                        new Vector2(centerCell.X, centerCell.Y),
                        new Vector2(unit.Cell.X, unit.Cell.Y));

                    if (distance < minRadius || distance > maxRadius)
                        continue;

                    unitsInZone.Add(unit);
                }
            }

            AddUnitsWithinZone(playerUnits);
            AddUnitsWithinZone(enemyUnits);

            if (unitsInZone.Count == 0)
                return;

            RasterizerState previousRasterizer = GraphicsDevice.RasterizerState;
            BlendState previousBlend = GraphicsDevice.BlendState;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.RasterizerState = hoveredCellWireframeState;

            foreach (Unit unit in unitsInZone)
            {
                float floorYOffset = WorldMetrics.FloorToWorldY(unit.Floor, cellSize);

                Vector3 center = new Vector3(
                    unit.Cell.X * cellSize + cellSize / 2f,
                    floorYOffset + ghostHeight / 2f,
                    unit.Cell.Y * cellSize + cellSize / 2f);

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
