using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class Renderer3D
    {
        private const float WallHeightRatio = 0.92f;
        private GraphicsDevice gd;
        private BasicEffect basic, textured;
        private VertexPositionColor[] cubeVerts, planeVerts;
        private short[] cubeIdx, planeIdx;
        private VertexPositionNormalTexture[] texturedPlaneVerts;
        private short[] texturedPlaneIdx;
        private HumanoidModelAdvanced humanoidModel;

        // Dans Renderer3D.cs, ajoutez :
        private float globalAnimationTime = 0f;

        public void Update(GameTime gameTime)
        {
            globalAnimationTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        public Renderer3D(GraphicsDevice device)
        {
            gd = device;
            InitEffects();
            InitPrimitives();
            humanoidModel = new HumanoidModelAdvanced();
        }

        private void InitEffects()
        {
            basic = new BasicEffect(gd) { VertexColorEnabled = true, LightingEnabled = true };
            basic.EnableDefaultLighting();
            textured = new BasicEffect(gd) { TextureEnabled = true, LightingEnabled = true };
            textured.EnableDefaultLighting();

            // Rendu mat pour les tuiles : on coupe les reflets spéculaires.
            textured.SpecularColor = Vector3.Zero;
            textured.DirectionalLight0.SpecularColor = Vector3.Zero;
            textured.DirectionalLight1.SpecularColor = Vector3.Zero;
            textured.DirectionalLight2.SpecularColor = Vector3.Zero;
        }

        private void InitPrimitives()
        {
            cubeVerts = new[]
            {
                new VertexPositionColor(new Vector3(-0.5f,-0.5f,-0.5f),Color.White),
                new VertexPositionColor(new Vector3(-0.5f,-0.5f,0.5f),Color.White),
                new VertexPositionColor(new Vector3(0.5f,-0.5f,0.5f),Color.White),
                new VertexPositionColor(new Vector3(0.5f,-0.5f,-0.5f),Color.White),
                new VertexPositionColor(new Vector3(-0.5f,0.5f,-0.5f),Color.White),
                new VertexPositionColor(new Vector3(-0.5f,0.5f,0.5f),Color.White),
                new VertexPositionColor(new Vector3(0.5f,0.5f,0.5f),Color.White),
                new VertexPositionColor(new Vector3(0.5f,0.5f,-0.5f),Color.White)
            };
            cubeIdx = new short[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 3, 2, 6, 3, 6, 7, 1, 5, 6, 1, 6, 2, 0, 3, 7, 0, 7, 4 };

            planeVerts = new[]
            {
                new VertexPositionColor(new Vector3(-0.5f,0,-0.5f),Color.White),
                new VertexPositionColor(new Vector3(-0.5f,0,0.5f),Color.White),
                new VertexPositionColor(new Vector3(0.5f,0,0.5f),Color.White),
                new VertexPositionColor(new Vector3(0.5f,0,-0.5f),Color.White)
            };
            planeIdx = new short[] { 0, 1, 2, 0, 2, 3 };

            Vector3 n = Vector3.Up;
            texturedPlaneVerts = new[]
            {
                new VertexPositionNormalTexture(new Vector3(-0.5f,0,-0.5f), n, new Vector2(0,0)),
                new VertexPositionNormalTexture(new Vector3(-0.5f,0,0.5f), n, new Vector2(0,1)),
                new VertexPositionNormalTexture(new Vector3(0.5f,0,0.5f), n, new Vector2(1,1)),
                new VertexPositionNormalTexture(new Vector3(0.5f,0,-0.5f), n, new Vector2(1,0))
            };
            texturedPlaneIdx = planeIdx;
        }

        public void SetMatrices(Matrix view, Matrix proj)
        {
            foreach (var e in new[] { basic, textured }) { e.View = view; e.Projection = proj; }
        }

        public void SetLighting(Color ambient, Color dir)
        {
            foreach (var e in new[] { basic, textured })
            {
                e.AmbientLightColor = ambient.ToVector3();
                e.DirectionalLight0.DiffuseColor = dir.ToVector3();
            }
        }

        private void DrawVertices(VertexPositionColor[] verts, short[] idx, Matrix world)
        {
            basic.World = world;
            foreach (var pass in basic.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length, idx, 0, idx.Length / 3);
            }
        }

        public void DrawCube(Vector3 pos, Vector3 scale, Color color)
        {
            var verts = cubeVerts.Select(v => new VertexPositionColor(v.Position, color)).ToArray();
            DrawVertices(verts, cubeIdx, Matrix.CreateScale(scale) * Matrix.CreateTranslation(pos));
        }

        public void DrawPlane(Vector3 pos, Vector3 scale, Color color)
            => DrawPlane(pos, scale, color, 0f, 0f, 0f);

        public void DrawPlane(Vector3 pos, Vector3 scale, Color color, float rotationX, float rotationY, float rotationZ)
        {
            var verts = planeVerts.Select(v => new VertexPositionColor(v.Position, color)).ToArray();
            Matrix world = Matrix.CreateScale(scale)
                * Matrix.CreateRotationX(rotationX)
                * Matrix.CreateRotationY(rotationY)
                * Matrix.CreateRotationZ(rotationZ)
                * Matrix.CreateTranslation(pos);
            DrawVertices(verts, planeIdx, world);
        }

        public void DrawTexturedPlane(Vector3 pos, Vector3 scale, Texture2D tex)
        {
            textured.World = Matrix.CreateScale(scale) * Matrix.CreateTranslation(pos);
            textured.Texture = tex;
            foreach (var pass in textured.CurrentTechnique.Passes)
                pass.Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, texturedPlaneVerts, 0, 4, texturedPlaneIdx, 0, 2);
        }

        public void DrawGrid(int w, int h, int size, Texture2D tex, float floorHeightOffset = 0f)
        {
            for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
                    DrawTexturedPlane(new Vector3(x * size + size / 2f, floorHeightOffset, z * size + size / 2f),
                                      new Vector3(size * 0.95f, 1, size * 0.95f), tex);
        }

        public void DrawGridCells(IEnumerable<Point> cells, int size, Texture2D tex, float floorHeightOffset = 0f)
        {
            foreach (var cell in cells)
            {
                DrawTexturedPlane(
                    new Vector3(cell.X * size + size / 2f, floorHeightOffset, cell.Y * size + size / 2f),
                    new Vector3(size * 0.95f, 1, size * 0.95f),
                    tex);
            }
        }

        /// <summary>
        /// ? MURS AMÉLIORÉS - Version avec détails, hauteur et ombres
        /// </summary>
        public void DrawWalls(HashSet<WallSegment> walls, int size, bool editorMode = false, float floorHeightOffset = 0f, Color? wallOverrideColor = null)
        {
            foreach (var s in walls)
            {
                Vector3 start = new(s.Start.X * size, floorHeightOffset, s.Start.Y * size);
                Vector3 end = new(s.End.X * size, floorHeightOffset, s.End.Y * size);
                Vector3 center = (start + end) / 2f;

                // ? Hauteur du mur augmentée
                // Conserver une hauteur de mur légèrement inférieure à l'écart entre étages
                // pour éviter les recouvrements visuels (z-fighting) entre niveaux.
                float wallHeight = size * WallHeightRatio;
                center.Y = floorHeightOffset + wallHeight / 2f;

                // ? Épaisseur du mur
                float thickness = size * 0.15f;

                Vector3 scale = s.IsHorizontal
                    ? new Vector3(size, wallHeight, thickness)
                    : new Vector3(thickness, wallHeight, size);

                // ? Couleur améliorée selon le mode
                Color wallColor = wallOverrideColor ?? (editorMode
                    ? new Color(140, 140, 140)  // Gris clair en mode éditeur
                    : new Color(100, 85, 70));   // Beige/brun en jeu

                // ------------------- NOUVEAU : GESTION DES FENÊTRES -------------------
                if (s.Type == WallType.Window)
                {
                    // 1. ALLÈGE (Le muret du bas - 35% de la hauteur totale)
                    float bottomHeight = wallHeight * 0.35f;
                    Vector3 bottomCenter = center;
                    bottomCenter.Y = floorHeightOffset + bottomHeight / 2f; // On le pose au sol
                    Vector3 bottomScale = s.IsHorizontal
                        ? new Vector3(size, bottomHeight, thickness)
                        : new Vector3(thickness, bottomHeight, size);

                    DrawCube(bottomCenter, bottomScale, wallColor);

                    // 2. LINTEAU (Le muret du haut - 20% de la hauteur totale)
                    float topPartHeight = wallHeight * 0.2f;
                    Vector3 topPartCenter = center;
                    topPartCenter.Y = floorHeightOffset + wallHeight - (topPartHeight / 2f); // On le colle en haut
                    Vector3 topPartScale = s.IsHorizontal
                        ? new Vector3(size, topPartHeight, thickness)
                        : new Vector3(thickness, topPartHeight, size);

                    DrawCube(topPartCenter, topPartScale, wallColor);

                    // Le milieu reste vide pour laisser passer la ligne de vue !
                }
                else
                {
                    // Mur plein classique (Portes et Murs normaux)
                    DrawCube(center, scale, wallColor);

                    // ? Ligne de démarcation (jointure au milieu) - Uniquement pour les murs pleins
                    if (!editorMode)
                    {
                        Vector3 jointCenter = center;
                        jointCenter.Y = floorHeightOffset + wallHeight * 0.6f;
                        Vector3 jointScale = s.IsHorizontal
                            ? new Vector3(size * 1.02f, thickness * 0.3f, thickness * 1.1f)
                            : new Vector3(thickness * 1.1f, thickness * 0.3f, size * 1.02f);

                        DrawCube(jointCenter, jointScale, new Color(80, 65, 50));
                    }
                }


                // ? Ombre portée au sol
                if (!editorMode)
                {
                    Vector3 shadowCenter = (start + end) / 2f;
                    shadowCenter.Y = floorHeightOffset + 0.01f;

                    float shadowWidth = s.IsHorizontal ? size : thickness * 2.5f;
                    float shadowLength = s.IsHorizontal ? thickness * 2.5f : size;

                    Vector3 shadowScale = new Vector3(shadowWidth, 0.02f, shadowLength);
                    DrawCube(shadowCenter, shadowScale, new Color(0, 0, 0, 80));
                }

                // ? ÉDITEUR: Marquer les extrémités avec des petits cubes
                if (editorMode)
                {
                    float markerSize = size * 0.12f;

                    // Marqueur début (jaune/orange)
                    Vector3 startMarker = new Vector3(s.Start.X * size, floorHeightOffset + wallHeight, s.Start.Y * size);
                    DrawCube(startMarker, new Vector3(markerSize), new Color(255, 200, 0));

                    // Marqueur fin (jaune/orange)
                    Vector3 endMarker = new Vector3(s.End.X * size, floorHeightOffset + wallHeight, s.End.Y * size);
                    DrawCube(endMarker, new Vector3(markerSize), new Color(255, 200, 0));
                }
            }
        }


        public void DrawRampTiles(IEnumerable<RampTileData> ramps, int floorToRender, int cellSize)
        {
            if (ramps == null)
                return;

            float floorYOffset = floorToRender * cellSize;
            foreach (var ramp in ramps)
            {
                if (ramp.Floor != floorToRender)
                    continue;

                DrawNorthRamp(ramp.X, ramp.Y, floorYOffset, cellSize);
            }
        }

        private void DrawNorthRamp(int cellX, int cellY, float floorYOffset, int cellSize)
        {
            const int slices = 6;
            float sliceDepth = cellSize / (float)slices;

            for (int i = 0; i < slices; i++)
            {
                float t = (i + 1f) / slices;
                float sliceHeight = t * cellSize;
                float zMin = cellY * cellSize - i * sliceDepth;

                Vector3 pos = new Vector3(
                    cellX * cellSize + cellSize / 2f,
                    floorYOffset + sliceHeight / 2f,
                    zMin + sliceDepth / 2f);

                Color color = Color.Lerp(new Color(170, 120, 80), new Color(220, 180, 120), t);
                DrawCube(pos, new Vector3(cellSize * 0.95f, sliceHeight, sliceDepth * 0.95f), color * 0.85f);
            }
        }

        public void DrawStairConnections(IEnumerable<StairConnectionData> stairs, int floorToRender, int cellSize)
        {
            if (stairs == null) return;

            float floorYOffset = floorToRender * cellSize;
            float pulse = 0.8f + 0.2f * (float)Math.Sin(globalAnimationTime * 4f);

            foreach (var stair in stairs)
            {
                if (stair.FromFloor == floorToRender)
                {
                    DrawStairMarker(stair.FromX, stair.FromY, floorYOffset, cellSize, new Color(255, 170, 40) * pulse);
                }

                if (stair.Bidirectional && stair.ToFloor == floorToRender)
                {
                    DrawStairMarker(stair.ToX, stair.ToY, floorYOffset, cellSize, new Color(60, 210, 255) * pulse);
                }
            }
        }

        private void DrawStairMarker(int cellX, int cellY, float floorYOffset, int cellSize, Color color)
        {
            Vector3 center = new Vector3(
                cellX * cellSize + cellSize / 2f,
                floorYOffset + 0.03f,
                cellY * cellSize + cellSize / 2f);

            DrawCube(center, new Vector3(cellSize * 0.45f, 0.06f, cellSize * 0.45f), color);
            DrawCube(center + new Vector3(0f, 0.35f, 0f), new Vector3(cellSize * 0.12f, 0.7f, cellSize * 0.12f), color * 0.85f);
        }




        public void DrawUnit(Unit unit, int cellSize, Color? bodyColorOverride = null, bool drawEquipment = true)
        {
            if (humanoidModel == null)
            {
                Console.WriteLine("[RENDERER3D] ERROR: humanoidModel is null!");
                return;
            }

            // Une cellule représente un volume de 5x5x5 pieds.
            // On calibre la hauteur visuelle d'un humain (~6 pieds) à ~1.2 cellule.
            // Le modèle humanoïde fait environ 2x "scale" en hauteur totale,
            // donc on convertit via ce facteur pour conserver des proportions réalistes.
            const float cellSizeFeet = 5f;
            const float averageUnitHeightFeet = 6f;
            const float humanoidModelHeightInScaleUnits = 2f;
            float scale = cellSize * (averageUnitHeightFeet / cellSizeFeet) / humanoidModelHeightInScaleUnits;

            // Utiliser l'orientation pilotée par l'unité (déplacement, visée, tir)
            float orientation = unit.Orientation;

            // Animation pilotée par l'état de l'unité (jog / run / sprint)
            float legSwing = unit.IsMoving ? unit.LegSwing : 0f;
            float armSwing = unit.IsMoving ? unit.ArmSwing : 0f;
            float bodyBob = unit.IsMoving ? unit.BodyBob : 0f;
            float idleBob = unit.IsMoving ? 0f : unit.IdleBobOffset;

            if (unit.IsAiming || unit.IsFiring)
            {
                armSwing = unit.DominantHand == Unit.Handedness.Right ? -0.28f : 0.28f;
            }

            // ✅ NOUVEAU : Utiliser DrawWithEquipment au lieu de Draw
            humanoidModel.DrawWithEquipment(
                gd,
                basic,
                unit,           // ← Passer l'unité complète
                scale,
                orientation,
                legSwing,
                armSwing,
                bodyBob,
                idleBob,
                bodyColorOverride,
                drawEquipment
            );

        }

        private void DrawUnitFacingArrow(Unit unit, int cellSize)
        {
            Vector3 forward = new Vector3((float)Math.Sin(unit.Orientation), 0f, (float)Math.Cos(unit.Orientation));
            if (forward.LengthSquared() < 0.0001f)
                return;

            forward.Normalize();

            Vector3 unitCenter = new Vector3(
                unit.VisualPosition.X,
                unit.VisualPosition.Y,
                unit.VisualPosition.Z);

            float yOffset = cellSize * 0.95f;
            float shaftLength = cellSize * 0.38f;
            float shaftThickness = cellSize * 0.07f;
            float headLength = cellSize * 0.18f;
            float headWidth = cellSize * 0.19f;

            Vector3 shaftCenter = unitCenter + forward * (shaftLength * 0.5f) + new Vector3(0f, yOffset, 0f);
            Vector3 headCenter = unitCenter + forward * (shaftLength + headLength * 0.5f) + new Vector3(0f, yOffset, 0f);

            float yaw = (float)Math.Atan2(forward.X, forward.Z);
            Matrix arrowRotation = Matrix.CreateRotationY(yaw);

            DrawVertices(
                cubeVerts.Select(v => new VertexPositionColor(v.Position, new Color(255, 215, 0))).ToArray(),
                cubeIdx,
                Matrix.CreateScale(shaftThickness, shaftThickness, shaftLength) *
                arrowRotation *
                Matrix.CreateTranslation(shaftCenter));

            DrawVertices(
                cubeVerts.Select(v => new VertexPositionColor(v.Position, new Color(255, 140, 0))).ToArray(),
                cubeIdx,
                Matrix.CreateScale(headWidth, shaftThickness * 1.25f, headLength) *
                arrowRotation *
                Matrix.CreateTranslation(headCenter));
        }

        public void DrawUnitSilhouette(Unit unit, int cellSize, Color silhouetteColor)
        {
            DrawUnit(unit, cellSize, silhouetteColor, drawEquipment: false);
        }

        public void DrawUnitGhost(Unit unit, int cellSize, Color ghostColor)
        {
            DrawUnit(unit, cellSize, ghostColor, drawEquipment: true);
        }



        public void DrawSelectionIndicator(Unit u, int size, Color c, float scale = 1.1f) =>
            DrawPlane(new Vector3(u.Cell.X * size + size / 2f, 0.05f, u.Cell.Y * size + size / 2f),
                      new Vector3(size * scale, 1, size * scale), c);

        public void DrawCraters(List<Crater> craters, int size)
        {
            foreach (var cr in craters)
            {
                Color col = new Color(60, 50, 40) * (0.5f + cr.Depth * 0.15f);
                DrawPlane(new Vector3(cr.Cell.X * size + size / 2f, -cr.Depth * 0.2f, cr.Cell.Y * size + size / 2f),
                          new Vector3(size * 0.9f, 1, size * 0.9f), col);
            }
        }

        public void DrawGrenades(List<Grenade> grenades, int size)
        {
            foreach (var g in grenades)
                DrawCube(g.Position, new Vector3(size * 0.2f), GrenadeDatabase.GetGrenadeColor(g.Data.Type));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AJOUTS POUR RENDERER3D - VISUALISATION DES COUVERTURES
        // Ajoutez ces méthodes à votre classe Renderer3D existante
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dessine les indicateurs de couverture sur la grille
        /// </summary>
        public void DrawCoverIndicators(CoverSystem coverSystem, int gridWidth, int gridHeight, int cellSize, float gameTime)
        {
            if (coverSystem == null)
                return;

            float pulse = (float)Math.Sin(gameTime * 2f) * 0.3f + 0.7f;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Point cell = new Point(x, y);
                    CoverData cover = coverSystem.GetCoverAt(cell);

                    if (cover.Type == CoverType.None)
                        continue;

                    Vector3 position = new Vector3(
                        x * cellSize + cellSize / 2f,
                        0.05f,
                        y * cellSize + cellSize / 2f
                    );

                    // Couleur selon le type
                    Color coverColor = cover.Type == CoverType.Half
                        ? new Color(255, 200, 100) * 0.5f * pulse  // Orange
                        : new Color(100, 200, 255) * 0.5f * pulse; // Bleu

                    DrawPlane(position, new Vector3(cellSize * 0.7f, 1, cellSize * 0.7f), coverColor);

                    // Indicateurs de direction
                    DrawCoverDirections(cell, cover, cellSize, pulse);
                }
            }
        }

        /// <summary>
        /// Dessine les indicateurs directionnels de couverture
        /// </summary>
        private void DrawCoverDirections(Point cell, CoverData cover, int cellSize, float pulse)
        {
            float height = cellSize * 0.3f;
            float thickness = cellSize * 0.1f;
            float offset = cellSize * 0.4f;

            Vector3 center = new Vector3(
                cell.X * cellSize + cellSize / 2f,
                height / 2f,
                cell.Y * cellSize + cellSize / 2f
            );

            Color dirColor = new Color(100, 255, 100) * 0.8f * pulse;

            // Nord
            if (cover.HasCoverFrom(CoverDirection.North))
            {
                Vector3 pos = center + new Vector3(0, 0, -offset);
                DrawCube(pos, new Vector3(cellSize * 0.6f, height, thickness), dirColor);
            }

            // Sud
            if (cover.HasCoverFrom(CoverDirection.South))
            {
                Vector3 pos = center + new Vector3(0, 0, offset);
                DrawCube(pos, new Vector3(cellSize * 0.6f, height, thickness), dirColor);
            }

            // Est
            if (cover.HasCoverFrom(CoverDirection.East))
            {
                Vector3 pos = center + new Vector3(offset, 0, 0);
                DrawCube(pos, new Vector3(thickness, height, cellSize * 0.6f), dirColor);
            }

            // Ouest
            if (cover.HasCoverFrom(CoverDirection.West))
            {
                Vector3 pos = center + new Vector3(-offset, 0, 0);
                DrawCube(pos, new Vector3(thickness, height, cellSize * 0.6f), dirColor);
            }
        }

        /// <summary>
        /// Dessine l'icône de couverture au-dessus d'une unité
        /// </summary>
        public void DrawUnitCoverIcon(Unit unit, int cellSize, float gameTime)
        {
            if (unit.CoverType == CoverType.None)
                return;

            float pulse = (float)Math.Sin(gameTime * 3f) * 0.1f + 0.9f;
            float height = cellSize * 2f;

            Vector3 iconPos = new Vector3(
                unit.VisualPosition.X,
                height,
                unit.VisualPosition.Z
            );

            // Couleur selon le type
            Color iconColor = unit.CoverType == CoverType.Half
                ? new Color(255, 200, 100) * pulse  // Orange
                : new Color(100, 200, 255) * pulse; // Bleu

            // Bouclier stylisé
            float shieldSize = cellSize * 0.3f;
            DrawCube(iconPos, new Vector3(shieldSize, shieldSize * 1.2f, shieldSize * 0.15f), iconColor);

            // Bord du bouclier
            Color borderColor = iconColor * 0.6f;
            float borderThickness = shieldSize * 0.1f;

            DrawCube(iconPos + new Vector3(0, shieldSize * 0.6f, 0),
                new Vector3(shieldSize, borderThickness, shieldSize * 0.15f), borderColor);

            DrawCube(iconPos + new Vector3(0, -shieldSize * 0.6f, 0),
                new Vector3(shieldSize, borderThickness, shieldSize * 0.15f), borderColor);
        }

        /// <summary>
        /// Dessine les cellules de couverture accessibles
        /// </summary>
        public void DrawReachableCoverCells(List<Point> coverCells, int cellSize, float gameTime)
        {
            if (coverCells == null || coverCells.Count == 0)
                return;

            float pulse = (float)Math.Sin(gameTime * 4f) * 0.3f + 0.7f;

            foreach (Point cell in coverCells)
            {
                Vector3 position = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0.08f,
                    cell.Y * cellSize + cellSize / 2f
                );

                Color highlightColor = new Color(100, 255, 100) * 0.6f * pulse;
                DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), highlightColor);
            }
        }

        /// <summary>
        /// Dessine un indicateur de flanking (unité flanquée)
        /// </summary>
        public void DrawFlankingIndicator(Unit unit, int cellSize, float gameTime)
        {
            float pulse = (float)Math.Sin(gameTime * 6f) * 0.4f + 0.6f;

            Vector3 position = new Vector3(
                unit.VisualPosition.X,
                cellSize * 1.5f,
                unit.VisualPosition.Z
            );

            // X rouge pour flanked
            Color dangerColor = new Color(255, 50, 50) * pulse;
            float size = cellSize * 0.4f;
            float thickness = cellSize * 0.08f;

            // Barre diagonale \
            Matrix rotation1 = Matrix.CreateRotationY(MathHelper.PiOver4);
            basic.World = Matrix.CreateScale(new Vector3(size, thickness, thickness)) *
                         rotation1 *
                         Matrix.CreateTranslation(position);

            var verts1 = cubeVerts.Select(v => new VertexPositionColor(v.Position, dangerColor)).ToArray();
            foreach (var pass in basic.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts1, 0, 8, cubeIdx, 0, 12);
            }

            // Barre diagonale /
            Matrix rotation2 = Matrix.CreateRotationY(-MathHelper.PiOver4);
            basic.World = Matrix.CreateScale(new Vector3(size, thickness, thickness)) *
                         rotation2 *
                         Matrix.CreateTranslation(position);

            var verts2 = cubeVerts.Select(v => new VertexPositionColor(v.Position, dangerColor)).ToArray();
            foreach (var pass in basic.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts2, 0, 8, cubeIdx, 0, 12);
            }
        }

        /// <summary>
        /// Dessine les 3 zones de mouvement (court, max, sprint)
        /// </summary>
        /// <summary>
        /// Dessine les 3 zones de mouvement (court, max, sprint)
        /// </summary>
        public void DrawMovementZones(PathfindingSystem.MovementZones zones, int cellSize, float gameTime, int viewedFloor)
        {
            if (zones == null) return;

            float pulse = (float)Math.Sin(gameTime * 3f) * 0.15f + 0.85f;
            float floorYOffset = viewedFloor * cellSize;

            HashSet<Point> shortZone = zones.ShortMove != null
                ? zones.ShortMove.Where(node => node.Floor == viewedFloor).Select(node => node.Cell).ToHashSet()
                : new HashSet<Point>();
            HashSet<Point> maxZone = new HashSet<Point>(shortZone);
            if (zones.MaxMove != null)
            {
                maxZone.UnionWith(zones.MaxMove.Where(node => node.Floor == viewedFloor).Select(node => node.Cell));
            }
            HashSet<Point> sprintZone = new HashSet<Point>(maxZone);
            if (zones.Sprint != null)
            {
                sprintZone.UnionWith(zones.Sprint.Where(node => node.Floor == viewedFloor).Select(node => node.Cell));
            }

            // Zone 1 : contour externe du mouvement court (1 AP) - VERT
            DrawZonePerimeter(shortZone, cellSize, floorYOffset + 0.02f, new Color(0, 255, 0, 220) * pulse);

            // Zone 2 : contour externe du mouvement max (2 AP) - BLEU
            DrawZonePerimeter(maxZone, cellSize, floorYOffset + 0.03f, new Color(0, 150, 255, 210) * pulse);

            // Zone 3 : contour externe du sprint (2 AP + phosphocréatine) - JAUNE
            float sprintPulse = (float)Math.Sin(gameTime * 5f) * 0.2f + 0.8f;
            DrawZonePerimeter(sprintZone, cellSize, floorYOffset + 0.04f, new Color(255, 200, 0, 215) * sprintPulse);

            // Indicateur sprint uniquement sur les cellules de frontière
            foreach (var cell in sprintZone)
            {
                if (IsBoundaryCell(cell, sprintZone))
                {
                    DrawSprintIndicator(cell, cellSize, gameTime, floorYOffset);
                }
            }
        }

        private static readonly Point[] CardinalDirections =
        {
            new Point(0, -1),
            new Point(1, 0),
            new Point(0, 1),
            new Point(-1, 0)
        };

        private bool IsBoundaryCell(Point cell, HashSet<Point> zone)
        {
            if (zone == null || zone.Count == 0) return false;

            foreach (var dir in CardinalDirections)
            {
                if (!zone.Contains(new Point(cell.X + dir.X, cell.Y + dir.Y)))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawZonePerimeter(HashSet<Point> zone, int cellSize, float height, Color color)
        {
            if (zone == null || zone.Count == 0) return;

            float edgeThickness = Math.Max(cellSize * 0.09f, 0.06f);
            float edgeLength = cellSize * 0.9f;
            float halfLength = edgeLength / 2f;

            foreach (Point cell in zone)
            {
                float centerX = cell.X * cellSize + cellSize / 2f;
                float centerZ = cell.Y * cellSize + cellSize / 2f;

                if (!zone.Contains(new Point(cell.X, cell.Y - 1)))
                {
                    DrawCube(new Vector3(centerX, height, centerZ - halfLength), new Vector3(edgeLength, 0.03f, edgeThickness), color);
                }

                if (!zone.Contains(new Point(cell.X + 1, cell.Y)))
                {
                    DrawCube(new Vector3(centerX + halfLength, height, centerZ), new Vector3(edgeThickness, 0.03f, edgeLength), color);
                }

                if (!zone.Contains(new Point(cell.X, cell.Y + 1)))
                {
                    DrawCube(new Vector3(centerX, height, centerZ + halfLength), new Vector3(edgeLength, 0.03f, edgeThickness), color);
                }

                if (!zone.Contains(new Point(cell.X - 1, cell.Y)))
                {
                    DrawCube(new Vector3(centerX - halfLength, height, centerZ), new Vector3(edgeThickness, 0.03f, edgeLength), color);
                }
            }
        }

        public void DrawZoneOutline(IEnumerable<Point> cells, int cellSize, float height, Color color)
        {
            if (cells == null)
                return;

            HashSet<Point> zone = cells.ToHashSet();
            if (zone.Count == 0)
                return;

            DrawZonePerimeter(zone, cellSize, height, color);
        }

        /// <summary>
        /// Dessine un indicateur de sprint (petit symbole au centre de la case)
        /// </summary>
        private void DrawSprintIndicator(Point cell, int cellSize, float gameTime, float floorYOffset)
        {
            float pulse = (float)Math.Sin(gameTime * 6f) * 0.3f + 0.7f;

            Vector3 pos = new Vector3(
                cell.X * cellSize + cellSize / 2f,
                floorYOffset + 0.15f,
                cell.Y * cellSize + cellSize / 2f
            );

            // Petit cube jaune qui pulse
            float size = cellSize * 0.15f;
            Color color = new Color(255, 220, 0) * pulse;
            DrawCube(pos, new Vector3(size, size * 0.3f, size), color);
        }

        /// <summary>
        /// Dessine le chemin avec coloration selon le coût (VERSION SIMPLIFIÉE)
        /// </summary>
        public void DrawMovementPath(List<GridNode> path, Unit unit, int cellSize, float gameTime)
        {
            if (path == null || path.Count == 0 || unit == null) return;

            int shortRange = unit.GetShortMoveRange();
            int maxRange = unit.GetMaxMoveRange();

            for (int i = 0; i < path.Count; i++)
            {
                GridNode node = path[i];
                Point cell = node.Cell;
                int distance = i + 1;

                // Déterminer la couleur selon la distance
                Color pathColor;
                if (distance <= shortRange)
                {
                    pathColor = new Color(0, 255, 100, 200); // Vert
                }
                else if (distance <= maxRange)
                {
                    pathColor = new Color(0, 200, 255, 200); // Bleu
                }
                else
                {
                    pathColor = new Color(255, 200, 0, 200); // Jaune (sprint)
                }

                Vector3 pos = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    node.Floor * cellSize + 0.09f,
                    cell.Y * cellSize + cellSize / 2f
                );

                float pulse = (float)Math.Sin(gameTime * 4f + i * 0.3f) * 0.08f + 0.92f;

                // Marqueur de point de passage
                DrawCube(pos, new Vector3(cellSize * 0.22f, 0.05f, cellSize * 0.22f), pathColor * pulse);

                // Tracé de trajectoire entre 2 cases
                if (i < path.Count - 1)
                {
                    GridNode nextNode = path[i + 1];
                    Point nextCell = nextNode.Cell;
                    Vector3 nextPos = new Vector3(
                        nextCell.X * cellSize + cellSize / 2f,
                        nextNode.Floor * cellSize + 0.09f,
                        nextCell.Y * cellSize + cellSize / 2f
                    );

                    DrawPathSegment(pos, nextPos, pathColor * pulse, cellSize);
                }
            }
        }

        private void DrawPathSegment(Vector3 start, Vector3 end, Color color, int cellSize)
        {
            Vector3 delta = end - start;
            int steps = Math.Max(2, (int)(delta.Length() / (cellSize * 0.12f)));

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 pos = Vector3.Lerp(start, end, t);
                DrawCube(pos, new Vector3(cellSize * 0.1f, 0.03f, cellSize * 0.1f), color);
            }
        }



    }
}
