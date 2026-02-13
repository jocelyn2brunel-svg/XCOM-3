using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class Renderer3D
    {
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
        {
            var verts = planeVerts.Select(v => new VertexPositionColor(v.Position, color)).ToArray();
            DrawVertices(verts, planeIdx, Matrix.CreateScale(scale) * Matrix.CreateTranslation(pos));
        }

        public void DrawTexturedPlane(Vector3 pos, Vector3 scale, Texture2D tex)
        {
            textured.World = Matrix.CreateScale(scale) * Matrix.CreateTranslation(pos);
            textured.Texture = tex;
            foreach (var pass in textured.CurrentTechnique.Passes)
                pass.Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, texturedPlaneVerts, 0, 4, texturedPlaneIdx, 0, 2);
        }

        public void DrawGrid(int w, int h, int size, Texture2D tex)
        {
            for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
                    DrawTexturedPlane(new Vector3(x * size + size / 2f, 0, z * size + size / 2f),
                                      new Vector3(size * 0.95f, 1, size * 0.95f), tex);
        }

        /// <summary>
        /// ? MURS AMÉLIORÉS - Version avec détails, hauteur et ombres
        /// </summary>
        public void DrawWalls(HashSet<WallSegment> walls, int size, bool editorMode = false)
        {
            foreach (var s in walls)
            {
                Vector3 start = new(s.Start.X * size, 0, s.Start.Y * size);
                Vector3 end = new(s.End.X * size, 0, s.End.Y * size);
                Vector3 center = (start + end) / 2f;

                // ? Hauteur du mur augmentée
                float wallHeight = size * 1.8f;
                center.Y = wallHeight / 2f;

                // ? Épaisseur du mur
                float thickness = size * 0.15f;

                Vector3 scale = s.IsHorizontal
                    ? new Vector3(size, wallHeight, thickness)
                    : new Vector3(thickness, wallHeight, size);

                // ? Couleur améliorée selon le mode
                Color wallColor = editorMode
                    ? new Color(140, 140, 140)  // Gris clair en mode éditeur
                    : new Color(100, 85, 70);   // Beige/brun en jeu

                // ? Corps principal du mur
                DrawCube(center, scale, wallColor);

                // ? Dessus du mur (plus clair)
                Vector3 topCenter = center;
                topCenter.Y = wallHeight;
                Vector3 topScale = s.IsHorizontal
                    ? new Vector3(size, thickness * 0.5f, thickness)
                    : new Vector3(thickness, thickness * 0.5f, size);

                Color topColor = editorMode
                    ? new Color(180, 180, 180)  // Gris très clair
                    : new Color(120, 105, 90);  // Beige plus clair

                DrawCube(topCenter, topScale, topColor);

                // ? Ligne de démarcation (jointure au milieu)
                if (!editorMode)
                {
                    Vector3 jointCenter = center;
                    jointCenter.Y = wallHeight * 0.6f;
                    Vector3 jointScale = s.IsHorizontal
                        ? new Vector3(size * 1.02f, thickness * 0.3f, thickness * 1.1f)
                        : new Vector3(thickness * 1.1f, thickness * 0.3f, size * 1.02f);

                    DrawCube(jointCenter, jointScale, new Color(80, 65, 50));
                }

                // ? Ombre portée au sol
                if (!editorMode)
                {
                    Vector3 shadowCenter = (start + end) / 2f;
                    shadowCenter.Y = 0.01f;

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
                    Vector3 startMarker = new Vector3(s.Start.X * size, wallHeight, s.Start.Y * size);
                    DrawCube(startMarker, new Vector3(markerSize), new Color(255, 200, 0));

                    // Marqueur fin (jaune/orange)
                    Vector3 endMarker = new Vector3(s.End.X * size, wallHeight, s.End.Y * size);
                    DrawCube(endMarker, new Vector3(markerSize), new Color(255, 200, 0));
                }
            }
        }

        


        public void DrawUnit(Unit unit, int cellSize)
        {
            if (humanoidModel == null)
            {
                Console.WriteLine("[RENDERER3D] ERROR: humanoidModel is null!");
                return;
            }

            Color teamColor = unit.Team == Team.Player ?
                new Color(100, 150, 255) :
                new Color(255, 100, 100);

            float scale = cellSize * 0.8f;

            // Calculer l'orientation (rotation vers la position cible)
            Vector2 direction = new Vector2(
                unit.TargetPosition.X - unit.VisualPosition.X,
                unit.TargetPosition.Z - unit.VisualPosition.Z
            );
            float orientation = direction.Length() > 0.01f ?
                (float)Math.Atan2(direction.X, direction.Y) : 0f;

            // Animation
            float legSwing = (float)Math.Sin(globalAnimationTime * 8f) * 0.3f;
            float armSwing = (float)Math.Sin(globalAnimationTime * 8f + Math.PI) * 0.2f;
            float bodyBob = unit.IsMoving ?
                (float)Math.Abs(Math.Sin(globalAnimationTime * 8f)) * 0.15f : 0f;
            float idleBob = !unit.IsMoving ?
                (float)Math.Sin(globalAnimationTime * 2f) * 0.05f : 0f;

            // ✅ NOUVEAU : Utiliser DrawWithEquipment au lieu de Draw
            humanoidModel.DrawWithEquipment(
                gd,
                basic,
                unit,           // ← Passer l'unité complète
                scale,
                orientation,
                unit.IsMoving ? legSwing : 0f,
                unit.IsMoving ? armSwing : 0f,
                bodyBob,
                idleBob
            );
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
        public void DrawMovementZones(PathfindingSystem.MovementZones zones, int cellSize, float gameTime)
        {
            if (zones == null) return;

            float pulse = (float)Math.Sin(gameTime * 3f) * 0.15f + 0.85f;

            // Zone 1 : Mouvement court (1 AP) - VERT
            foreach (var cell in zones.ShortMove)
            {
                Vector3 pos = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0.02f,
                    cell.Y * cellSize + cellSize / 2f
                );

                Color color = new Color(0, 255, 0, 150) * pulse; // Vert transparent
                DrawPlane(pos, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), color);
            }

            // Zone 2 : Mouvement max (2 AP) - BLEU
            foreach (var cell in zones.MaxMove)
            {
                Vector3 pos = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0.03f,
                    cell.Y * cellSize + cellSize / 2f
                );

                Color color = new Color(0, 150, 255, 130) * pulse; // Bleu transparent
                DrawPlane(pos, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), color);
            }

            // Zone 3 : Sprint (2 AP + stamina) - JAUNE avec warning
            foreach (var cell in zones.Sprint)
            {
                Vector3 pos = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0.04f,
                    cell.Y * cellSize + cellSize / 2f
                );

                // Pulse plus rapide pour le warning
                float sprintPulse = (float)Math.Sin(gameTime * 5f) * 0.2f + 0.8f;
                Color color = new Color(255, 200, 0, 140) * sprintPulse; // Jaune avertissement
                DrawPlane(pos, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), color);

                // Petit indicateur de stamina au centre
                DrawSprintIndicator(cell, cellSize, gameTime);
            }
        }

        /// <summary>
        /// Dessine un indicateur de sprint (petit symbole au centre de la case)
        /// </summary>
        private void DrawSprintIndicator(Point cell, int cellSize, float gameTime)
        {
            float pulse = (float)Math.Sin(gameTime * 6f) * 0.3f + 0.7f;

            Vector3 pos = new Vector3(
                cell.X * cellSize + cellSize / 2f,
                0.15f,
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
        public void DrawMovementPath(List<Point> path, Unit unit, int cellSize, float gameTime)
        {
            if (path == null || path.Count == 0 || unit == null) return;

            int shortRange = unit.GetShortMoveRange();
            int maxRange = unit.GetMaxMoveRange();

            for (int i = 0; i < path.Count; i++)
            {
                Point cell = path[i];
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
                    0.08f,
                    cell.Y * cellSize + cellSize / 2f
                );

                float pulse = (float)Math.Sin(gameTime * 4f + i * 0.3f) * 0.2f + 0.8f;
                DrawPlane(pos, new Vector3(cellSize * 0.7f, 1, cellSize * 0.7f), pathColor * pulse);
            }
        }



    }
}