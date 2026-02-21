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
        private const int GrenadeCollisionSamples = 32;
        private const int MaxGrenadeBounces = 1;
        private const float GrenadeRicochetEnergyRetention = 0.6f;
        private const float Mk2WindowBreakDistanceMeters = 18f;
        private const float ConfinedBlastDistanceMultiplier = 1.5f;

        private readonly struct GrenadeWallHit
        {
            public WallSegment Wall { get; }
            public float PathT { get; }
            public Vector2 Point { get; }
            public Vector2 Normal { get; }

            public GrenadeWallHit(WallSegment wall, float pathT, Vector2 point, Vector2 normal)
            {
                Wall = wall;
                PathT = pathT;
                Point = point;
                Normal = normal;
            }
        }

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

        private int GetGrappleClimbPhosphocreatineCost(Unit unit, int climbedFloors)
        {
            if (unit == null || climbedFloors <= 0)
                return 0;

            // Modèle effort maximal continu:
            // 0-6s -> ~55% PCr consommée
            // 6-10s -> chute rapide vers ~75%
            // 10-18s -> quasi-épuisement vers ~85%
            float climbDurationSeconds = Math.Min(18f, climbedFloors * 6f);
            float consumedRatio;

            if (climbDurationSeconds <= 6f)
            {
                consumedRatio = 0.55f * (climbDurationSeconds / 6f);
            }
            else if (climbDurationSeconds <= 10f)
            {
                float phaseRatio = (climbDurationSeconds - 6f) / 4f;
                consumedRatio = MathHelper.Lerp(0.55f, 0.75f, phaseRatio);
            }
            else
            {
                float phaseRatio = (climbDurationSeconds - 10f) / 8f;
                consumedRatio = MathHelper.Lerp(0.75f, 0.85f, phaseRatio);
            }

            return (int)MathF.Ceiling(unit.MaxPhosphocreatine * consumedRatio);
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
            // Réinitialiser la visibilité pour cette mise à jour
            foreach (var grid in visibleCells.Values)
                Array.Clear(grid, 0, grid.Length);

            // Mettre à jour les cellules visibles et explorées basées sur les joueurs
            // On parcourt tous les étages de la carte (y compris sous-sols) pour la visibilité.
            int minMapFloor = GetMinimumViewFloor();
            int maxMapFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);

            foreach (var player in playerUnits)
            {
                if (player.Health <= 0) continue;
                int range = GetEffectivePerceptionRange(player);

                // Perception multi-étages : on vérifie la visibilité sur tous les étages à portée
                for (int f = minMapFloor; f <= maxMapFloor; f++)
                {
                    var visGrid = GetVisibilityGrid(f);
                    var expGrid = GetExplorationGrid(f);

                    int minX = Math.Max(0, player.Cell.X - range);
                    int maxX = Math.Min(gridWidth - 1, player.Cell.X + range);
                    int minY = Math.Max(0, player.Cell.Y - range);
                    int maxY = Math.Min(gridHeight - 1, player.Cell.Y + range);

                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            if (visGrid[x, y]) continue;

                            if (Vector2.DistanceSquared(new Vector2(player.Cell.X, player.Cell.Y), new Vector2(x, y)) <= range * range)
                            {
                                if (pathfinding.HasLineOfSight(player.Cell, player.Floor, new Point(x, y), f))
                                {
                                    visGrid[x, y] = true;
                                    expGrid[x, y] = true;

                                    // Propagation de la visibilité pour les zones extérieures (non couvertes)
                                    // pour un effet 3D cohérent (on voit le sol si on voit le balcon, et inversement).
                                    if (f > 0 && !IsCellCovered(new Point(x, y), f))
                                    {
                                        GetVisibilityGrid(0)[x, y] = true;
                                        GetExplorationGrid(0)[x, y] = true;
                                    }
                                    else if (f == 0 && !IsCellCovered(new Point(x, y), 0))
                                    {
                                        // Si on voit le sol et qu'il n'est pas couvert, on révèle aussi les étages supérieurs
                                        // qui n'ont pas de dalle (le "vide" extérieur).
                                        for (int upF = 1; upF <= maxMapFloor; upF++)
                                        {
                                            if (!IsSlabAt(new Point(x, y), upF))
                                            {
                                                GetVisibilityGrid(upF)[x, y] = true;
                                                GetExplorationGrid(upF)[x, y] = true;
                                            }
                                            else break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            currentlySpottedEnemies.Clear();
            foreach (var enemy in enemyUnits)
            {
                bool wasSpotted = enemy.IsSpottedByPlayerTeam;
                bool isSpotted = enemy.Health > 0 && IsCellVisible(enemy.Cell, enemy.Floor);

                enemy.IsSpottedByPlayerTeam = isSpotted;

                if (isSpotted)
                {
                    currentlySpottedEnemies.Add(enemy);
                    // Retirer le fantôme si il existe
                    enemyGhosts.RemoveAll(g => g.Id == enemy.Id);
                }
                else if (wasSpotted && enemy.Health > 0)
                {
                    // L'ennemi vient de disparaître. Créer/Mettre à jour le fantôme.
                    enemyGhosts.RemoveAll(g => g.Id == enemy.Id);
                    enemyGhosts.Add(new Unit(enemy));
                }

                // Si l'ennemi est mort, retirer son fantôme
                if (enemy.Health <= 0)
                {
                    enemyGhosts.RemoveAll(g => g.Id == enemy.Id);
                }
            }

            if (combatUI.SelectedFireTarget?.Team == Team.Enemy && !IsEnemyVisibleToPlayers(combatUI.SelectedFireTarget))
            {
                combatUI.SelectedFireTarget = null;
            }
        }

        private bool CanUnitPerceiveTarget(Unit observer, Unit target)
        {
            if (observer == null || target == null)
                return false;

            if (target.Health <= 0)
                return false;

            return CanUnitPerceiveCell(observer, target.Cell, target.Floor);
        }

        private bool CanUnitPerceiveCell(Unit observer, Point targetCell, int targetFloor)
        {
            if (observer == null || pathfinding == null)
                return false;

            if (observer.Health <= 0)
                return false;

            float distanceCells = Vector2.Distance(new Vector2(observer.Cell.X, observer.Cell.Y), new Vector2(targetCell.X, targetCell.Y));
            if (distanceCells > GetEffectivePerceptionRange(observer))
                return false;

            // Vision 360°: pas de contrainte d'angle, uniquement portée + ligne de vue.
            return pathfinding.HasLineOfSight(observer.Cell, observer.Floor, targetCell, targetFloor);
        }

        private bool IsEnemyCellVisibleToPlayers(Unit enemy, Point cell, int floor)
        {
            if (enemy == null || enemy.Health <= 0)
                return false;

            return playerUnits.Any(player => CanUnitPerceiveCell(player, cell, floor));
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

            availableGrenades.Add(new GrenadeItem(grenadeDatabase["MK 2"], new Point(50, 300)));
            if (grenadeDatabase.ContainsKey("Satchel Charge (C4)"))
                availableGrenades.Add(new GrenadeItem(grenadeDatabase["Satchel Charge (C4)"], new Point(95, 300)));
        }

        private int GetUnitThrowRange(Unit unit)
        {
            if (unit == null)
                return BaseThrowRange;

            return BaseThrowRange + unit.Skills.GetGrenadeThrowRangeBonus();
        }

        private void RefreshThrowableCellsIfNeeded()
        {
            if (!throwMode || selectedUnit == null)
            {
                throwableCells.Clear();
                throwableCellsCacheValid = false;
                return;
            }

            int throwRange = GetUnitThrowRange(selectedUnit);
            bool cacheHit = throwableCellsCacheValid
                && throwableCellsCachedUnit == selectedUnit
                && throwableCellsCachedFloor == viewedFloor
                && throwableCellsCachedRange == throwRange;

            if (cacheHit)
                return;

            throwableCells = ThrowTrajectoryCalculator.GetThrowableCells(selectedUnit.Cell, throwRange, gridWidth, gridHeight);

            if (grenadeOptionWallAwareTargeting)
            {
                // Quand le ciblage sensible aux murs est activé, l'outline doit refléter
                // les cellules réellement atteignables, y compris derrière les murs verticaux.
                throwableCells = throwableCells
                    .Where(cell => CanThrowToTarget(selectedUnit, cell, viewedFloor))
                    .ToList();
            }

            throwableCellsCachedUnit = selectedUnit;
            throwableCellsCachedFloor = viewedFloor;
            throwableCellsCachedRange = throwRange;
            throwableCellsCacheValid = true;
        }

        private void HandleGrenadeThrow(MouseState mouse, bool leftClick)
        {
            if (selectedUnit == null || selectedGrenade == null) return;

            // Sélection de la cible : on compare le t-value du sol et du mur le plus proche,
            // et on retient l'intersection la plus proche de la caméra.
            {
                float floorY = WorldMetrics.FloorToWorldY(viewedFloor, cellSize);
                Ray pickRay = camera.ScreenPointToRay(
                    mouse.Position,
                    GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height);

                float floorT = float.MaxValue;
                if (Math.Abs(pickRay.Direction.Y) > 0.001f)
                {
                    float t = (floorY - pickRay.Position.Y) / pickRay.Direction.Y;
                    if (t > 0f) floorT = t;
                }

                if (TryGetWallCellFromMouse(mouse, viewedFloor, selectedUnit.Cell, out Point wallCell, out float wallT)
                    && wallT < floorT)
                    throwTarget = wallCell;
                else
                    throwTarget = camera.GetCellFromMouse(
                        mouse.Position,
                        GraphicsDevice.Viewport.Width,
                        GraphicsDevice.Viewport.Height,
                        floorY);
            }
            if (throwTarget.X >= 0)
            {
                if (throwModeUsesFlashlight)
                    explosionPreview.Clear();
                else
                    explosionPreview = ThrowTrajectoryCalculator.GetExplosionPreview(throwTarget, selectedGrenade.Radius, gridWidth, gridHeight);

                Vector3 startPos = new Vector3(selectedUnit.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, selectedUnit.Cell.Y * cellSize + cellSize / 2f);
                Vector3 targetPos = new Vector3(throwTarget.X * cellSize + cellSize / 2f, 0, throwTarget.Y * cellSize + cellSize / 2f);
                trajectoryPreview = ThrowTrajectoryCalculator.CalculateArcPoints(startPos, targetPos);
                ricochetPreview.Clear();

                if (grenadeOptionThrowFeedback)
                {
                    Point previewLanding = PredictLandingCellWithEnvironment(selectedUnit, throwTarget, viewedFloor, out List<Vector3> previewRicochetPoints);
                    if (previewLanding != throwTarget)
                        ricochetPreview = previewRicochetPoints;
                }
            }
            int throwRange = GetUnitThrowRange(selectedUnit);
            bool canThrow = ThrowTrajectoryCalculator.IsInThrowRange(selectedUnit.Cell, throwTarget, throwRange)
                && (!grenadeOptionWallAwareTargeting || CanThrowToTarget(selectedUnit, throwTarget, viewedFloor));

            if (leftClick && throwTarget.X >= 0 && canThrow)
            {
                bool flashlightWasOn = false;
                if (throwModeUsesFlashlight)
                    flashlightWasOn = throwFlashlightFromRightHand ? selectedUnit.IsRightHandFlashlightOn : selectedUnit.IsLeftHandFlashlightOn;

                LaunchGrenade(selectedUnit, selectedGrenade, throwTarget, viewedFloor, flashlightWasOn);
                selectedUnit.ActionPoints -= selectedGrenade.AOCost;
                selectedUnit.RegisterIntenseAction("lancer", 14);

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

                    Console.WriteLine($"{selectedUnit.Name} threw tactical flashlight at {throwTarget} (light {(flashlightWasOn ? "ON" : "OFF")}).");
                }
                else
                {
                    selectedUnit.RemoveGrenade(selectedGrenade);
                }

                CancelSelection();
            }
        }


        private void ActivateSatchelPlacementMode()
        {
            if (selectedUnit == null || selectedUnit.Team != Team.Player)
                return;

            GrenadeData satchel = selectedUnit.Grenades.FirstOrDefault(g => g.Type == GrenadeType.SatchelC4);
            if (satchel == null)
            {
                Console.WriteLine("Aucune charge C4 disponible.");
                return;
            }

            if (selectedUnit.ActionPoints < satchel.AOCost)
            {
                Console.WriteLine("AP insuffisants pour poser une charge C4.");
                return;
            }

            ExitThrowMode();
            ExitGrappleMode();
            c4PlacementMode = true;
            selectedGrenade = satchel;
            throwableCells = GetSatchelPlacementCells(selectedUnit);
            Console.WriteLine($"Mode pose C4 activé: {throwableCells.Count} cases valides.");
        }

        private void ExitSatchelPlacementMode()
        {
            c4PlacementMode = false;
            throwableCells.Clear();
            explosionPreview.Clear();
            trajectoryPreview.Clear();

            if (selectedGrenade?.Type == GrenadeType.SatchelC4)
                selectedGrenade = null;
        }

        private List<Point> GetSatchelPlacementCells(Unit unit)
        {
            var cells = new List<Point>();
            if (unit == null)
                return cells;

            for (int dx = -SatchelPlacementRange; dx <= SatchelPlacementRange; dx++)
            {
                for (int dy = -SatchelPlacementRange; dy <= SatchelPlacementRange; dy++)
                {
                    Point cell = new Point(unit.Cell.X + dx, unit.Cell.Y + dy);
                    if (Math.Abs(dx) + Math.Abs(dy) > SatchelPlacementRange)
                        continue;

                    if (cell.X < 0 || cell.Y < 0 || cell.X >= gridWidth || cell.Y >= gridHeight)
                        continue;

                    if (!GetCellsForFloor(viewedFloor).Contains(cell))
                        continue;

                    if (plantedSatchelCharges.Any(c => c.Cell == cell && c.Floor == viewedFloor && c.Team == unit.Team))
                        continue;

                    cells.Add(cell);
                }
            }

            return cells;
        }

        private void HandleSatchelPlacement(MouseState mouse, bool leftClick)
        {
            if (!c4PlacementMode || selectedUnit == null)
                return;

            Point targetCell = camera.GetCellFromMouse(
                mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                WorldMetrics.FloorToWorldY(viewedFloor, cellSize));

            if (targetCell.X >= 0)
                hoveredCell = targetCell;

            if (!leftClick || targetCell.X < 0)
                return;

            throwableCells = GetSatchelPlacementCells(selectedUnit);
            if (!throwableCells.Contains(targetCell))
                return;

            GrenadeData satchel = selectedUnit.Grenades.FirstOrDefault(g => g.Type == GrenadeType.SatchelC4);
            if (satchel == null || selectedUnit.ActionPoints < satchel.AOCost)
                return;

            plantedSatchelCharges.Add(new PlantedSatchelCharge
            {
                Cell = targetCell,
                Floor = viewedFloor,
                Team = selectedUnit.Team,
                Owner = selectedUnit
            });

            selectedUnit.ActionPoints -= satchel.AOCost;
            selectedUnit.RegisterIntenseAction("pose de charge", 10);
            selectedUnit.RemoveGrenade(satchel);
            Console.WriteLine($"{selectedUnit.Name} pose une charge C4 sur {targetCell} (étage {viewedFloor}).");
            ExitSatchelPlacementMode();
        }

        private bool HasDetonatableSatchelCharges(Unit unit)
        {
            return unit != null && plantedSatchelCharges.Any(c => c.Team == unit.Team);
        }

        private void TriggerSatchelDetonation(Unit detonator)
        {
            if (detonator == null)
                return;

            if (detonator.ActionPoints < SatchelDetonationActionPointCost)
            {
                Console.WriteLine("AP insuffisants pour détoner les charges C4.");
                return;
            }

            var chargesToDetonate = plantedSatchelCharges
                .Where(c => c.Team == detonator.Team)
                .ToList();

            if (chargesToDetonate.Count == 0)
                return;

            detonator.ActionPoints -= SatchelDetonationActionPointCost;
            detonator.RegisterIntenseAction("détonation coordonnée", 8);

            foreach (PlantedSatchelCharge charge in chargesToDetonate)
            {
                TriggerExplosion(charge.Cell, charge.Floor, grenadeDatabase["Satchel Charge (C4)"], charge.Owner ?? detonator);
            }

            plantedSatchelCharges.RemoveAll(c => c.Team == detonator.Team);
            Console.WriteLine($"{detonator.Name} déclenche {chargesToDetonate.Count} charge(s) C4.");
            ExitSatchelPlacementMode();
        }

        private void DrawSatchelPlacementMode3D(GameTime gameTime)
        {
            if (!c4PlacementMode)
                return;

            throwableCells = GetSatchelPlacementCells(selectedUnit);

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f) * 0.3f + 0.7f;
            float floorYOffset = WorldMetrics.FloorToWorldY(viewedFloor, cellSize);

            renderer3D.DrawZoneOutline(
                throwableCells,
                cellSize,
                floorYOffset + 0.05f,
                new Color(255, 210, 80, 220) * pulse);
        }

        private void DrawPlantedSatchelCharges3D(GameTime gameTime)
        {
            if (plantedSatchelCharges.Count == 0)
                return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f) * 0.25f + 0.75f;

            foreach (PlantedSatchelCharge charge in plantedSatchelCharges)
            {
                // Uniquement si la case est visible
                if (!IsCellVisible(charge.Cell, charge.Floor))
                    continue;

                float floorY = WorldMetrics.FloorToWorldY(charge.Floor, cellSize);
                Vector3 center = new Vector3(
                    charge.Cell.X * cellSize + cellSize * 0.5f,
                    floorY + cellSize * 0.08f,
                    charge.Cell.Y * cellSize + cellSize * 0.5f);

                renderer3D.DrawCube(center, new Vector3(cellSize * 0.18f, cellSize * 0.08f, cellSize * 0.18f), new Color(230, 30, 30, 240) * pulse);
                renderer3D.DrawCube(center + new Vector3(0f, cellSize * 0.06f, 0f), new Vector3(cellSize * 0.06f), new Color(255, 250, 170, 235) * pulse);
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

            if (selectedUnit.IsGrappleConcentrating)
            {
                Console.WriteLine($"[GRAPPLIN] {selectedUnit.Name} est déjà en concentration vers l'étage {selectedUnit.GrappleAnchorFloor}.");
                return;
            }

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
            floorViewMode = FloorViewMode.AbilityLocked;
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
            int floorsToClimbTotal = Math.Max(1, selectedAnchor.DestinationFloor - selectedUnit.Floor);

            if (selectedUnit.ActionPoints < GrappleActionPointCost)
            {
                Console.WriteLine($"[GRAPPLIN] AP insuffisants: coût {GrappleActionPointCost} AP pour lancer le grappin.");
                ExitGrappleMode();
                return;
            }

            int hitChance = Math.Clamp(
                GrappleBaseAccuracyPercent
                + GetGrappleDexterityAccuracyBonus(selectedUnit)
                - floorsToClimbTotal * GrappleHeightPenaltyPercentPerFloor,
                20,
                98);

            int roll = random.Next(100);
            selectedUnit.ActionPoints = Math.Max(0, selectedUnit.ActionPoints - GrappleActionPointCost);
            selectedUnit.RegisterIntenseAction("lancer de grappin", 16);
            if (roll >= hitChance)
            {
                Console.WriteLine($"[GRAPPLIN] Échec du lancer ({hitChance}% / roll {roll}).");
                selectedUnit.IsGrappleConcentrating = false;
                selectedUnit.GrappleAnchorCell = Point.Zero;
                selectedUnit.GrappleAnchorFloor = -1;
                ExitGrappleMode();
                return;
            }

            selectedUnit.IsGrappleConcentrating = true;
            selectedUnit.GrappleAnchorCell = selectedAnchor.DestinationCell;
            selectedUnit.GrappleAnchorFloor = selectedAnchor.DestinationFloor;
            Console.WriteLine($"[GRAPPLIN] {selectedUnit.Name} accroche le grappin vers {selectedAnchor.DestinationCell} (étage {selectedAnchor.DestinationFloor}). Concentration active.");
            ExitGrappleMode();
        }

        private void ProcessGrappleConcentrationAtTurnStart()
        {
            if (combatSystem == null || combatSystem.CurrentTurn != TurnState.PlayerTurn)
                return;

            if (combatSystem.PlayerTurnNumber == lastProcessedGrapplePlayerTurn)
                return;

            lastProcessedGrapplePlayerTurn = combatSystem.PlayerTurnNumber;

            foreach (Unit unit in playerUnits)
            {
                AdvanceGrappleConcentration(unit);
            }
        }

        private void AdvanceGrappleConcentration(Unit unit)
        {
            if (unit == null || !unit.IsGrappleConcentrating || unit.Health <= 0)
                return;

            if (unit.GrappleAnchorFloor <= unit.Floor)
            {
                unit.IsGrappleConcentrating = false;
                unit.GrappleAnchorCell = Point.Zero;
                unit.GrappleAnchorFloor = -1;
                return;
            }

            if (!UnitHasGrapplingHookEquipped(unit))
            {
                unit.IsGrappleConcentrating = false;
                unit.GrappleAnchorCell = Point.Zero;
                unit.GrappleAnchorFloor = -1;
                Console.WriteLine($"[GRAPPLIN] {unit.Name} perd sa concentration (grappin non équipé).");
                return;
            }

            int climbedFloorsThisTurn = 0;
            while (climbedFloorsThisTurn < GrappleConcentrationClimbFloorsPerTurn && unit.Floor < unit.GrappleAnchorFloor)
            {
                int nextFloor = unit.Floor + 1;
                if (!IsCellAvailableOnFloor(unit.GrappleAnchorCell, nextFloor))
                {
                    Console.WriteLine($"[GRAPPLIN] {unit.Name} ne peut pas continuer l'ascension vers l'étage {nextFloor}.");
                    break;
                }

                bool hasSupportingWall = CellHasSupportingWall(unit.GrappleAnchorCell, nextFloor);
                int climbApCost = GetGrappleActionPointCost(1, hasSupportingWall);
                if (unit.ActionPoints < climbApCost)
                    break;

                unit.ActionPoints = Math.Max(0, unit.ActionPoints - climbApCost);
                unit.RegisterIntenseAction("ascension au grappin", 20);
                unit.Cell = unit.GrappleAnchorCell;
                unit.Floor = nextFloor;
                unit.VisualPosition = new Vector3(
                    unit.Cell.X * cellSize + cellSize / 2f,
                    WorldMetrics.FloorToWorldY(unit.Floor, cellSize),
                    unit.Cell.Y * cellSize + cellSize / 2f);

                climbedFloorsThisTurn++;
            }

            if (climbedFloorsThisTurn > 0)
            {
                int phosphocreatineCost = GetGrappleClimbPhosphocreatineCost(unit, climbedFloorsThisTurn);
                unit.ConsumePhosphocreatine(phosphocreatineCost, $"ascension au grappin ({climbedFloorsThisTurn} étage(s))");

                unitManager.OnUnitMoved(unit, unit.Cell, unit.Floor);
                combatSystem.UpdateUnitCover(unit);
                Console.WriteLine($"[GRAPPLIN] {unit.Name} grimpe {climbedFloorsThisTurn} étage(s) ce tour (position: {unit.Cell}, étage {unit.Floor}).");
            }

            if (unit.Floor >= unit.GrappleAnchorFloor)
            {
                unit.IsGrappleConcentrating = false;
                unit.GrappleAnchorCell = Point.Zero;
                unit.GrappleAnchorFloor = -1;
                Console.WriteLine($"[GRAPPLIN] {unit.Name} termine l'ascension au point d'ancrage.");
            }
        }

        private void DrawGrappleMode3D(GameTime gameTime)
        {
            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 5f) * 0.25f + 0.75f;

            if (grappleMode && grappleAnchors.Count > 0)
            {
                float floorYOffset = WorldMetrics.FloorToWorldY(grappleTargetFloor, cellSize);

                renderer3D.DrawZoneOutline(
                    grappleAnchors.Select(a => a.DestinationCell).ToList(),
                    cellSize,
                    floorYOffset + 0.09f,
                    new Color(80, 240, 255, 240) * pulse);

                foreach (GrappleAnchor anchor in grappleAnchors)
                {
                    DrawGrappleAnchorVisual(anchor.DestinationCell, anchor.DestinationFloor, anchor.Wall, selectedUnit?.Floor ?? 0, pulse);
                }
            }

            IEnumerable<Unit> grapplingUnits = playerUnits
                .Concat(enemyUnits)
                .Where(u => u != null && u.Health > 0 && u.IsGrappleConcentrating && u.GrappleAnchorFloor >= u.Floor);

            foreach (Unit unit in grapplingUnits)
            {
                DrawGrappleAnchorVisual(unit.GrappleAnchorCell, unit.GrappleAnchorFloor, null, unit.Floor, pulse * 0.9f);
            }
        }

        private void DrawGrappleAnchorVisual(Point anchorCell, int anchorFloor, WallSegment? supportingWall, int ropeDestinationFloor, float pulse)
        {
            Vector3 anchorPosition = GetGrappleAnchorWorldPosition(anchorCell, anchorFloor, supportingWall);
            float ropeBottomY = WorldMetrics.FloorToWorldY(ropeDestinationFloor, cellSize) + cellSize * 0.2f;
            ropeBottomY = Math.Min(ropeBottomY, anchorPosition.Y - cellSize * 0.1f);
            Vector3 ropeEnd = new Vector3(anchorPosition.X, ropeBottomY, anchorPosition.Z);

            Color ropeColor = new Color(196, 226, 240, 220) * pulse;
            renderer3D.DrawLine(anchorPosition, ropeEnd, ropeColor);
            renderer3D.DrawLine(anchorPosition + new Vector3(0.035f, 0f, 0.035f), ropeEnd + new Vector3(0.035f, 0f, 0.035f), ropeColor * 0.85f);

            Color hookColor = new Color(84, 212, 230, 230) * pulse;
            float hookSize = cellSize * 0.09f;
            renderer3D.DrawCube(anchorPosition, new Vector3(hookSize, hookSize * 0.75f, hookSize), hookColor);
            renderer3D.DrawCube(anchorPosition + new Vector3(-hookSize * 0.7f, -hookSize * 0.5f, 0f), new Vector3(hookSize * 0.55f, hookSize * 0.22f, hookSize * 0.22f), hookColor);
            renderer3D.DrawCube(anchorPosition + new Vector3(hookSize * 0.7f, -hookSize * 0.5f, 0f), new Vector3(hookSize * 0.55f, hookSize * 0.22f, hookSize * 0.22f), hookColor);
        }

        private Vector3 GetGrappleAnchorWorldPosition(Point anchorCell, int anchorFloor, WallSegment? supportingWall)
        {
            float floorY = WorldMetrics.FloorToWorldY(anchorFloor, cellSize);
            Vector3 position = new Vector3(
                anchorCell.X * cellSize + cellSize / 2f,
                floorY + cellSize * 1.55f,
                anchorCell.Y * cellSize + cellSize / 2f);

            if (!supportingWall.HasValue)
                return position;

            WallSegment wall = supportingWall.Value;
            float edgeOffset = cellSize * 0.35f;

            if (wall.IsHorizontal)
            {
                float wallZ = wall.Start.Y * cellSize;
                bool isNorthSide = anchorCell.Y < wall.Start.Y;
                position.Z = wallZ + (isNorthSide ? -edgeOffset : edgeOffset);
            }
            else
            {
                float wallX = wall.Start.X * cellSize;
                bool isWestSide = anchorCell.X < wall.Start.X;
                position.X = wallX + (isWestSide ? -edgeOffset : edgeOffset);
            }

            return position;
        }

        private void LaunchGrenade(Unit thrower, GrenadeData grenadeData, Point targetCell, int targetFloor, bool flashlightEmitsLight = false)
        {
            Point actualLandingCell = ResolveThrowLandingCell(thrower, targetCell, targetFloor);

            Vector3 startPos = new Vector3(thrower.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, thrower.Cell.Y * cellSize + cellSize / 2f);
            Vector3 targetPos = new Vector3(
                actualLandingCell.X * cellSize + cellSize / 2f,
                WorldMetrics.FloorToWorldY(targetFloor, cellSize) + cellSize * 0.5f,
                actualLandingCell.Y * cellSize + cellSize / 2f);
            Grenade grenade = new Grenade(grenadeData, startPos, targetPos, thrower, targetFloor);
            grenade.EmitsLight = flashlightEmitsLight;
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

        private Point ResolveThrowLandingCell(Unit thrower, Point desiredTargetCell, int targetFloor)
        {
            if (thrower == null)
                return desiredTargetCell;

            int throwDistance = Math.Abs(desiredTargetCell.X - thrower.Cell.X) + Math.Abs(desiredTargetCell.Y - thrower.Cell.Y);
            int distancePenalty = Math.Max(0, throwDistance - 1) * ThrowDistancePenaltyPercentPerCell;
            int throwSkillBonus = thrower.Skills?.GetThrowAccuracyBonus() ?? 0;

            int hitChance = Math.Clamp(BaseThrowAccuracyPercent - distancePenalty + throwSkillBonus, 10, 98);
            int roll = random.Next(100);

            if (roll < hitChance)
                return PredictLandingCellWithEnvironment(thrower, desiredTargetCell, targetFloor, out _);

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
            return PredictLandingCellWithEnvironment(thrower, scatteredCell, targetFloor, out _);
        }

        private bool CanThrowToTarget(Unit thrower, Point targetCell, int targetFloor)
        {
            if (thrower == null)
                return false;

            Point predicted = PredictLandingCellWithEnvironment(thrower, targetCell, targetFloor, out _);
            return predicted == targetCell;
        }

        private Point PredictLandingCellWithEnvironment(Unit thrower, Point desiredTargetCell, int targetFloor, out List<Vector3> bouncePreviewPoints)
        {
            bouncePreviewPoints = new List<Vector3>();

            if (thrower == null)
                return desiredTargetCell;

            Point currentTarget = desiredTargetCell;
            int remainingBounces = grenadeOptionRicochet ? MaxGrenadeBounces : 0;

            while (true)
            {
                if (!TryFindFirstBlockingWall(thrower.Cell, currentTarget, targetFloor, out GrenadeWallHit hit))
                    return currentTarget;

                // Fenêtres des étages supérieurs: la grenade peut passer si la hauteur croise l'ouverture.
                if (hit.Wall.Type == WallType.Window && CanPassWindowOpening(thrower.Cell, currentTarget, hit.PathT, targetFloor))
                    return currentTarget;

                if (!grenadeOptionRicochet || remainingBounces <= 0 || hit.Wall.Type != WallType.Full)
                {
                    Point impactCell = new Point(
                        Math.Clamp((int)MathF.Floor(hit.Point.X), 0, gridWidth - 1),
                        Math.Clamp((int)MathF.Floor(hit.Point.Y), 0, gridHeight - 1));
                    return impactCell;
                }

                Vector2 source = new Vector2(thrower.Cell.X + 0.5f, thrower.Cell.Y + 0.5f);
                Vector2 destination = new Vector2(currentTarget.X + 0.5f, currentTarget.Y + 0.5f);
                Vector2 incoming = destination - source;
                if (incoming.LengthSquared() < 0.001f)
                    return currentTarget;

                incoming.Normalize();
                Vector2 reflected = incoming - 2f * Vector2.Dot(incoming, hit.Normal) * hit.Normal;
                if (reflected.LengthSquared() < 0.001f)
                    return currentTarget;

                reflected.Normalize();
                float remainingDistance = Vector2.Distance(hit.Point, destination) * GrenadeRicochetEnergyRetention;
                Vector2 bounceTarget = hit.Point + reflected * MathF.Max(1f, remainingDistance);

                Point bouncedCell = new Point(
                    Math.Clamp((int)MathF.Floor(bounceTarget.X), 0, gridWidth - 1),
                    Math.Clamp((int)MathF.Floor(bounceTarget.Y), 0, gridHeight - 1));

                float floorY = WorldMetrics.FloorToWorldY(targetFloor, cellSize);
                bouncePreviewPoints.Add(new Vector3(hit.Point.X * cellSize, floorY + cellSize * 0.25f, hit.Point.Y * cellSize));
                bouncePreviewPoints.Add(new Vector3((bouncedCell.X + 0.5f) * cellSize, floorY + cellSize * 0.25f, (bouncedCell.Y + 0.5f) * cellSize));

                if (bouncedCell == currentTarget)
                    return bouncedCell;

                currentTarget = bouncedCell;
                remainingBounces--;
            }
        }

        private bool TryFindFirstBlockingWall(Point fromCell, Point toCell, int floor, out GrenadeWallHit firstHit)
        {
            firstHit = default;

            if (!grenadeOptionArcCollisionSampling)
                return false;

            HashSet<WallSegment> walls = GetWallsForFloor(floor);
            if (walls == null || walls.Count == 0)
                return false;

            Vector3 start = new Vector3(fromCell.X * cellSize + cellSize / 2f, cellSize * 1.5f, fromCell.Y * cellSize + cellSize / 2f);
            Vector3 end = new Vector3(toCell.X * cellSize + cellSize / 2f, WorldMetrics.FloorToWorldY(floor, cellSize) + cellSize * 0.5f, toCell.Y * cellSize + cellSize / 2f);
            List<Vector3> arc = ThrowTrajectoryCalculator.CalculateArcPoints(start, end, GrenadeCollisionSamples);

            float nearestT = float.MaxValue;
            for (int i = 0; i < arc.Count - 1; i++)
            {
                Vector2 segA = new Vector2(arc[i].X / cellSize, arc[i].Z / cellSize);
                Vector2 segB = new Vector2(arc[i + 1].X / cellSize, arc[i + 1].Z / cellSize);
                float segmentStartT = i / (float)(arc.Count - 1);
                float segmentEndT = (i + 1) / (float)(arc.Count - 1);

                foreach (var wall in walls)
                {
                    if (wall.Type == WallType.Door || wall.Type == WallType.ShatteredWindow)
                        continue;

                    if (!TryGetWallSegment2D(wall, out Vector2 wA, out Vector2 wB, out Vector2 normal))
                        continue;

                    if (!TryGetSegmentIntersection(segA, segB, wA, wB, out float segT, out _))
                        continue;

                    float pathT = MathHelper.Lerp(segmentStartT, segmentEndT, segT);
                    if (pathT >= nearestT)
                        continue;

                    Vector2 hitPoint = Vector2.Lerp(segA, segB, segT);
                    nearestT = pathT;
                    firstHit = new GrenadeWallHit(wall, pathT, hitPoint, normal);
                }
            }

            return nearestT < float.MaxValue;
        }

        private bool CanPassWindowOpening(Point fromCell, Point toCell, float pathT, int floor)
        {
            if (floor <= 0)
                return false;

            Vector3 start = new Vector3(fromCell.X * cellSize + cellSize / 2f, cellSize * 1.5f, fromCell.Y * cellSize + cellSize / 2f);
            Vector3 end = new Vector3(toCell.X * cellSize + cellSize / 2f, WorldMetrics.FloorToWorldY(floor, cellSize) + cellSize * 0.5f, toCell.Y * cellSize + cellSize / 2f);

            float distance = Vector3.Distance(start, end);
            float arcHeight = Math.Min(distance * 0.5f, 8f);
            float linearY = MathHelper.Lerp(start.Y, end.Y, pathT);
            float arcProgress = 4f * pathT * (1f - pathT);
            float grenadeY = linearY + arcProgress * arcHeight;

            float floorY = WorldMetrics.FloorToWorldY(floor, cellSize);
            float windowBottom = floorY + cellSize * 0.7f;
            float windowTop = floorY + cellSize * 1.45f;
            return grenadeY >= windowBottom && grenadeY <= windowTop;
        }

        private static bool TryGetWallSegment2D(WallSegment wall, out Vector2 a, out Vector2 b, out Vector2 normal)
        {
            if (wall.IsHorizontal)
            {
                int minX = Math.Min(wall.Start.X, wall.End.X);
                int maxX = Math.Max(wall.Start.X, wall.End.X);
                a = new Vector2(minX, wall.Start.Y);
                b = new Vector2(maxX, wall.Start.Y);
                normal = new Vector2(0f, 1f);
            }
            else
            {
                int minY = Math.Min(wall.Start.Y, wall.End.Y);
                int maxY = Math.Max(wall.Start.Y, wall.End.Y);
                a = new Vector2(wall.Start.X, minY);
                b = new Vector2(wall.Start.X, maxY);
                normal = new Vector2(1f, 0f);
            }

            return Vector2.DistanceSquared(a, b) > 0.0001f;
        }

        /// <summary>
        /// Raycast 3D contre les murs du sol donné.
        /// Retourne la cellule d'impact (côté tireur) du mur le plus proche touché par le rayon,
        /// ainsi que la valeur t du rayon (distance relative depuis la caméra).
        /// </summary>
        private bool TryGetWallCellFromMouse(
            MouseState mouse, int floor, Point throwerCell,
            out Point wallCell, out float hitT)
        {
            wallCell = new Point(-1, -1);
            hitT = float.MaxValue;

            HashSet<WallSegment> walls = GetWallsForFloor(floor);
            if (walls == null || walls.Count == 0)
                return false;

            Ray ray = camera.ScreenPointToRay(
                mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);

            float floorY = WorldMetrics.FloorToWorldY(floor, cellSize);
            float wallTop = floorY + cellSize * 2.0f; // WallHeightRatio = 2.0f

            float nearestT = float.MaxValue;
            Point bestCell = new Point(-1, -1);

            foreach (var wall in walls)
            {
                float t;
                Vector3 hit;
                Point candidate;

                if (wall.IsHorizontal)
                {
                    // Plan du mur : Z_monde = wall.Start.Y * cellSize
                    float wallZ = wall.Start.Y * cellSize;
                    if (Math.Abs(ray.Direction.Z) < 0.0001f) continue;
                    t = (wallZ - ray.Position.Z) / ray.Direction.Z;
                    if (t <= 0f || t >= nearestT) continue;

                    hit = ray.Position + ray.Direction * t;

                    int minX = Math.Min(wall.Start.X, wall.End.X);
                    int maxX = Math.Max(wall.Start.X, wall.End.X);
                    if (hit.X < minX * cellSize || hit.X > maxX * cellSize) continue;
                    if (hit.Y < floorY || hit.Y > wallTop) continue;

                    int cellX = Math.Clamp((int)(hit.X / cellSize), 0, gridWidth - 1);
                    int cellZ = throwerCell.Y < wall.Start.Y ? wall.Start.Y - 1 : wall.Start.Y;
                    cellZ = Math.Clamp(cellZ, 0, gridHeight - 1);
                    candidate = new Point(cellX, cellZ);
                }
                else
                {
                    // Plan du mur : X_monde = wall.Start.X * cellSize
                    float wallX = wall.Start.X * cellSize;
                    if (Math.Abs(ray.Direction.X) < 0.0001f) continue;
                    t = (wallX - ray.Position.X) / ray.Direction.X;
                    if (t <= 0f || t >= nearestT) continue;

                    hit = ray.Position + ray.Direction * t;

                    int minY = Math.Min(wall.Start.Y, wall.End.Y);
                    int maxY = Math.Max(wall.Start.Y, wall.End.Y);
                    if (hit.Z < minY * cellSize || hit.Z > maxY * cellSize) continue;
                    if (hit.Y < floorY || hit.Y > wallTop) continue;

                    int cellX2 = throwerCell.X < wall.Start.X ? wall.Start.X - 1 : wall.Start.X;
                    cellX2 = Math.Clamp(cellX2, 0, gridWidth - 1);
                    int cellZ2 = Math.Clamp((int)(hit.Z / cellSize), 0, gridHeight - 1);
                    candidate = new Point(cellX2, cellZ2);
                }

                nearestT = t;
                bestCell = candidate;
            }

            if (nearestT < float.MaxValue)
            {
                wallCell = bestCell;
                hitT = nearestT;
                return true;
            }
            return false;
        }

        private static bool TryGetSegmentIntersection(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out float t, out float u)
        {
            t = 0f;
            u = 0f;

            Vector2 r = p2 - p;
            Vector2 s = q2 - q;
            float rxs = Cross(r, s);
            if (Math.Abs(rxs) < 0.0001f)
                return false;

            Vector2 qMinusP = q - p;
            t = Cross(qMinusP, s) / rxs;
            u = Cross(qMinusP, r) / rxs;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        private static float Cross(Vector2 a, Vector2 b)
            => a.X * b.Y - a.Y * b.X;

        private void UpdateGrenades(GameTime gameTime)
        {
            float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = activeGrenades.Count - 1; i >= 0; i--)
            {
                var grenade = activeGrenades[i];
                float duration = Math.Max(0.001f, grenade.FlightDurationSeconds);
                grenade.Progress += elapsedSeconds / duration;
                if (grenade.Progress >= 1f)
                {
                    Point explosionCell = new Point((int)(grenade.TargetPosition.X / cellSize), (int)(grenade.TargetPosition.Z / cellSize));

                    if (string.Equals(grenade.Data?.Name, TacticalFlashlightItemName, StringComparison.OrdinalIgnoreCase))
                    {
                        RegisterFlashlightLoot(explosionCell, grenade.TargetFloor, grenade.EmitsLight);
                    }

                    TriggerExplosion(explosionCell, grenade.TargetFloor, grenade.Data, grenade.Thrower);
                    activeGrenades.RemoveAt(i);
                }
                else grenade.Position = grenade.GetCurrentPosition();
            }

            foreach (var crater in craters) crater.Age += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }


        private void RegisterFlashlightLoot(Point lootCell, int lootFloor, bool isOn)
        {
            bool addedToNearbyLoot = RegisterGroundLoot(TacticalFlashlightItemName, lootCell, lootFloor, isOn);
            Console.WriteLine(addedToNearbyLoot
                ? $"Flashlight landed at {lootCell} (floor {lootFloor}) and added to nearby loot."
                : $"Flashlight landed at {lootCell} (floor {lootFloor}) but could not be added to nearby loot.");
        }

        private bool RegisterGroundLoot(string itemName, Point lootCell, int lootFloor, bool isOn = false)
        {
            bool mergedWithExistingCell = false;

            for (int i = 0; i < flashlightLootMarkers.Count; i++)
            {
                FlashlightLootMarker marker = flashlightLootMarkers[i];
                if (marker.Cell != lootCell || marker.Floor != lootFloor)
                    continue;

                marker.Quantity += 1;
                marker.IsOn = marker.IsOn || isOn;
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
                    PulseSeed = (float)(random.NextDouble() * MathHelper.TwoPi),
                    IsOn = isOn
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

                // Uniquement si exploré
                if (!IsCellExplored(marker.Cell, marker.Floor))
                    continue;

                float pulse = 0.58f + 0.42f * (float)Math.Sin(gameSeconds * 6f + marker.PulseSeed);
                Color pulseColor = new Color(255, 230, 80, 235) * pulse;

                if (marker.IsOn)
                {
                    Vector3 lightCenter = new Vector3(
                        marker.Cell.X * cellSize + cellSize * 0.5f,
                        floorYOffset + cellSize * 0.12f,
                        marker.Cell.Y * cellSize + cellSize * 0.5f);

                    float haloScale = cellSize * (0.75f + pulse * 0.35f);
                    renderer3D.DrawPlane(lightCenter, new Vector3(haloScale, 1f, haloScale), new Color(255, 248, 180, 90) * pulse);
                    renderer3D.DrawCube(lightCenter + new Vector3(0f, cellSize * 0.12f, 0f), new Vector3(cellSize * 0.10f), new Color(255, 252, 210, 225) * pulse);
                }

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
            bool showShrapnel = grenadeData.Type == GrenadeType.Frag;
            VisualEffects.PlayExplosion(explosionPos, grenadeData.Radius, renderer3D, showShrapnel);
            PlayGrenadeExplosionSound(explosionPos, grenadeData.Radius);

            int shatteredCount = ShatterWindowsAroundExplosion(center, centerFloor, grenadeData);
            if (shatteredCount > 0)
            {
                Console.WriteLine($"Shattered {shatteredCount} windows around explosion");
            }

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
                    Vector3 blastImpulse = ComputeExplosionImpulse(center, centerFloor, unit, grenadeData.Radius);
                    HandleUnitKilled(unit, blastImpulse);
                    Console.WriteLine($"{unit.Name} killed by explosion!");
                }
            }

            if (grenadeData.DestroyWalls)
            {
                List<WallSegment> destroyedWalls = explosionManager.GetDestroyedWalls(wallSegments, center, grenadeData.Radius);
                if (destroyedWalls.Count > 0)
                {
                    VisualEffects.PlayWallDestruction(explosionPos, destroyedWalls, cellSize, centerFloor, renderer3D);

                    foreach (var wall in destroyedWalls) wallSegments.Remove(wall);
                    InvalidateWallsByFloorCache();
                    unitManager.OnWallsDestroyed();
                    visibilityDirty = true;
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

        private int ShatterWindowsAroundExplosion(Point center, int centerFloor, GrenadeData grenadeData)
        {
            if (grenadeData == null)
                return 0;

            float breakDistanceMeters = GetWindowBreakDistanceMeters(grenadeData, centerFloor);
            if (breakDistanceMeters <= 0.01f)
                return 0;

            float breakDistanceCells = breakDistanceMeters / Math.Max(0.1f, cellSize);
            HashSet<WallSegment> wallsOnFloor = GetWallsForFloor(centerFloor);
            List<WallSegment> windowsToShatter = new List<WallSegment>();

            foreach (WallSegment wall in wallsOnFloor)
            {
                if (wall.Type != WallType.Window)
                    continue;

                Vector2 wallCenter = new Vector2(
                    (wall.Start.X + wall.End.X) * 0.5f,
                    (wall.Start.Y + wall.End.Y) * 0.5f);

                if (Vector2.Distance(new Vector2(center.X, center.Y), wallCenter) <= breakDistanceCells)
                    windowsToShatter.Add(wall);
            }

            if (windowsToShatter.Count == 0)
                return 0;

            VisualEffects.PlayWindowShatter(windowsToShatter, cellSize, centerFloor, renderer3D);

            foreach (WallSegment window in windowsToShatter)
                shatteredWindows.Add(new WindowInstance(centerFloor, window));

            // Bris de vitres de véhicules
            int shatteredVehiclesCount = 0;
            if (currentMap != null)
            {
                foreach (var furniture in currentMap.Furnitures)
                {
                    if (furniture.Floor != centerFloor || !FurnitureData.IsVehicle(furniture.Type))
                        continue;

                    Vector2 vehiclePos = new Vector2(furniture.X, furniture.Y);
                    if (Vector2.Distance(new Vector2(center.X, center.Y), vehiclePos) <= breakDistanceCells)
                    {
                        if (shatteredVehicleWindows.Add((furniture.Floor, furniture.X, furniture.Y)))
                            shatteredVehiclesCount++;
                    }
                }
            }

            if (windowsToShatter.Count > 0 || shatteredVehiclesCount > 0)
            {
                InvalidateWallsByFloorCache();
                unitManager.OnWallsDestroyed();
            }

            return windowsToShatter.Count + shatteredVehiclesCount;
        }

        private float GetWindowBreakDistanceMeters(GrenadeData grenadeData, int explosionFloor)
        {
            float explosiveEquivalentKg = grenadeData.Name switch
            {
                "MK 2" => 0.055f,
                "Satchel Charge (C4)" => 0.248f,
                _ => 0.055f * MathF.Max(0.35f, grenadeData.Radius / 2f)
            };

            float mk2Scale = MathF.Pow(MathF.Max(0.001f, explosiveEquivalentKg / 0.055f), 1f / 3f);
            float distance = Mk2WindowBreakDistanceMeters * mk2Scale;

            if (explosionFloor != 0)
                distance *= ConfinedBlastDistanceMultiplier;

            return distance;
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
                    KillUnitFromMk2(unit, $"fragmentation ({hitChance * 100f:0}% chance)", thrower, center, centerFloor, ref enemiesHit, ref totalDamage);
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
            {
                // Unité à un étage différent : elle est exposée si LoS 3D directe.
                return pathfinding.HasLineOfSight(explosionCenter, explosionFloor, unit.Cell, unit.Floor);
            }

            return pathfinding.HasLineOfSight(explosionCenter, explosionFloor, unit.Cell, unit.Floor);
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

        private void KillUnitFromMk2(Unit unit, string reason, Unit thrower, Point detonationCenter, int detonationFloor, ref int enemiesHit, ref int totalDamage)
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

            Vector3 fragmentationImpulse = ComputeExplosionImpulse(detonationCenter, detonationFloor, unit, Mk2FragmentationEndRadius + 1f);
            HandleUnitKilled(unit, fragmentationImpulse);
        }

        private Vector3 ComputeExplosionImpulse(Point explosionCenter, int explosionFloor, Unit unit, float blastRadius)
        {
            if (unit == null)
                return Vector3.Up;

            Vector3 center = new Vector3(explosionCenter.X + 0.5f, explosionFloor * 0.25f, explosionCenter.Y + 0.5f);
            Vector3 victim = new Vector3(unit.Cell.X + 0.5f, unit.Floor * 0.25f, unit.Cell.Y + 0.5f);

            Vector3 away = victim - center;
            float distance = away.Length();

            if (distance < 0.0001f)
            {
                away = new Vector3(
                    (float)(random.NextDouble() * 2.0 - 1.0),
                    0.55f,
                    (float)(random.NextDouble() * 2.0 - 1.0));
            }
            else
            {
                away /= distance;
                away.Y += 0.28f;
            }

            away.Normalize();

            float normalizedDistance = blastRadius <= 0f ? 1f : MathHelper.Clamp(distance / blastRadius, 0f, 1f);
            float strength = MathHelper.Lerp(2.8f, 0.9f, normalizedDistance);

            return away * strength;
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

            RefreshThrowableCellsIfNeeded();

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

            if (grenadeOptionThrowFeedback && ricochetPreview.Count >= 2)
            {
                for (int i = 0; i < ricochetPreview.Count - 1; i += 2)
                {
                    renderer3D.DrawCube(ricochetPreview[i], new Vector3(cellSize * 0.12f), new Color(255, 180, 60, 220));
                    renderer3D.DrawCube(ricochetPreview[i + 1], new Vector3(cellSize * 0.12f), new Color(255, 140, 30, 220));
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

            float linearPulse = renderer3D.GlobalAnimationTime % 1.0f;

            foreach (Mk2FragmentationPreviewInfo info in infos)
            {
                Color rayColor = GetMk2PreviewChanceColor(info.HitChancePercent) * (0.55f + 0.35f * pulse);
                Vector3 start = blastOrigin;
                Vector3 end = info.UnitWorldCenter;

                // On dessine la ligne de base avec une opacité un peu réduite pour faire ressortir le pulse
                renderer3D.DrawLine(start, end, rayColor * 0.6f);

                Vector3 direction = end - start;
                if (direction.LengthSquared() > 0.0001f)
                {
                    direction.Normalize();

                    // Flèche de pulse qui parcourt la ligne
                    Vector3 pulsePos = Vector3.Lerp(start, end, linearPulse);
                    renderer3D.DrawCube(pulsePos, new Vector3(cellSize * 0.12f), rayColor);

                    // Pointe de flèche statique à la fin
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
