using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class Renderer3D
    {
        // Mur de 2 cases de haut (par rapport à la taille d'une cellule de grille).
        // NB: cela peut provoquer un recouvrement visuel entre étages si l'espacement
        // vertical des floors reste à 1 * cellSize dans certaines vues.
        private const float WallHeightRatio = 2.0f;
        private const float TileFillRatio = 1.0f;
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

            // Éviter un rendu totalement mat : on conserve un reflet spéculaire léger
            // pour redonner du relief au terrain sans effet "plastique".
            textured.SpecularPower = 18f;
            textured.SpecularColor = new Vector3(0.12f, 0.12f, 0.12f);
            textured.DirectionalLight0.SpecularColor = new Vector3(0.18f, 0.18f, 0.18f);
            textured.DirectionalLight1.SpecularColor = new Vector3(0.10f, 0.10f, 0.10f);
            textured.DirectionalLight2.SpecularColor = new Vector3(0.06f, 0.06f, 0.06f);
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
                bool isTexturedPass = ReferenceEquals(e, textured);

                // Le terrain texturé accumule plus facilement la lumière (diffuse + spéculaire).
                // On réduit légèrement son exposition globale pour éviter l'effet "sur-éclairé".
                float ambientScale = isTexturedPass ? 0.78f : 0.9f;
                float mainLightScale = isTexturedPass ? 0.72f : 0.82f;
                float fillLight1Scale = isTexturedPass ? 0.22f : 0.30f;
                float fillLight2Scale = isTexturedPass ? 0.10f : 0.16f;

                e.AmbientLightColor = ambient.ToVector3() * ambientScale;

                // Uniformiser les 3 lumières directionnelles avec la teinte du cycle jour/nuit.
                // Sans cela, le terrain texturé peut rester trop sombre car il ne reçoit
                // effectivement qu'une part de DirectionalLight0 selon ses normales.
                Vector3 directionalColor = dir.ToVector3();

                e.DirectionalLight0.Enabled = true;
                e.DirectionalLight0.DiffuseColor = directionalColor * mainLightScale;
                e.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-0.45f, -1.0f, -0.35f));

                e.DirectionalLight1.Enabled = true;
                e.DirectionalLight1.DiffuseColor = directionalColor * fillLight1Scale;
                e.DirectionalLight1.Direction = Vector3.Normalize(new Vector3(0.55f, -0.85f, 0.15f));

                e.DirectionalLight2.Enabled = true;
                e.DirectionalLight2.DiffuseColor = directionalColor * fillLight2Scale;
                e.DirectionalLight2.Direction = Vector3.Normalize(new Vector3(0.05f, -0.65f, -0.75f));
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

        public void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            VertexPositionColor[] lineVertices = new[]
            {
                new VertexPositionColor(start, color),
                new VertexPositionColor(end, color)
            };

            basic.World = Matrix.Identity;
            foreach (var pass in basic.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.LineList, lineVertices, 0, 1);
            }
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

        private void DrawTexturedVerticalQuad(Vector3 center, float width, float height, bool facesX, float axisCoord, Texture2D tex)
        {
            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            VertexPositionNormalTexture[] verts;
            if (facesX)
            {
                verts = new[]
                {
                    new VertexPositionNormalTexture(new Vector3(center.X - halfWidth, center.Y - halfHeight, axisCoord), Vector3.Forward, new Vector2(0, 1)),
                    new VertexPositionNormalTexture(new Vector3(center.X - halfWidth, center.Y + halfHeight, axisCoord), Vector3.Forward, new Vector2(0, 0)),
                    new VertexPositionNormalTexture(new Vector3(center.X + halfWidth, center.Y + halfHeight, axisCoord), Vector3.Forward, new Vector2(width / 2f, 0)),
                    new VertexPositionNormalTexture(new Vector3(center.X + halfWidth, center.Y - halfHeight, axisCoord), Vector3.Forward, new Vector2(width / 2f, 1)),
                };
            }
            else
            {
                verts = new[]
                {
                    new VertexPositionNormalTexture(new Vector3(axisCoord, center.Y - halfHeight, center.Z - halfWidth), Vector3.Right, new Vector2(0, 1)),
                    new VertexPositionNormalTexture(new Vector3(axisCoord, center.Y + halfHeight, center.Z - halfWidth), Vector3.Right, new Vector2(0, 0)),
                    new VertexPositionNormalTexture(new Vector3(axisCoord, center.Y + halfHeight, center.Z + halfWidth), Vector3.Right, new Vector2(width / 2f, 0)),
                    new VertexPositionNormalTexture(new Vector3(axisCoord, center.Y - halfHeight, center.Z + halfWidth), Vector3.Right, new Vector2(width / 2f, 1)),
                };
            }

            textured.World = Matrix.Identity;
            textured.Texture = tex;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            foreach (var pass in textured.CurrentTechnique.Passes)
                pass.Apply();

            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 4, texturedPlaneIdx, 0, 2);
        }

        private void DrawWallSection(Vector3 center, Vector3 scale, bool isHorizontal, Color color, Texture2D wallTexture)
        {
            DrawCube(center, scale, color);

            if (wallTexture == null)
                return;

            float width = isHorizontal ? scale.X : scale.Z;
            float height = scale.Y;
            float halfThickness = isHorizontal ? scale.Z / 2f : scale.X / 2f;
            const float offset = 0.005f;

            if (isHorizontal)
            {
                DrawTexturedVerticalQuad(center, width, height, true, center.Z - halfThickness - offset, wallTexture);
                DrawTexturedVerticalQuad(center, width, height, true, center.Z + halfThickness + offset, wallTexture);
            }
            else
            {
                DrawTexturedVerticalQuad(center, width, height, false, center.X - halfThickness - offset, wallTexture);
                DrawTexturedVerticalQuad(center, width, height, false, center.X + halfThickness + offset, wallTexture);
            }
        }



        public void DrawHescoBarriers(IEnumerable<Point> cells, int cellSize, float floorHeightOffset, Texture2D hescoTexture)
        {
            if (cells == null)
                return;

            float blockWidth = cellSize * 0.9f;
            float blockHeight = cellSize * 0.9f;
            float halfWidth = blockWidth / 2f;
            float halfHeight = blockHeight / 2f;

            foreach (Point cell in cells)
            {
                Vector3 center = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    floorHeightOffset + halfHeight,
                    cell.Y * cellSize + cellSize / 2f);

                DrawCube(center, new Vector3(blockWidth, blockHeight, blockWidth), new Color(132, 120, 95));

                if (hescoTexture == null)
                    continue;

                float leftX = center.X - halfWidth - 0.01f;
                float rightX = center.X + halfWidth + 0.01f;
                float nearZ = center.Z - halfWidth - 0.01f;
                float farZ = center.Z + halfWidth + 0.01f;

                DrawTexturedVerticalQuad(center, blockWidth, blockHeight, true, nearZ, hescoTexture);
                DrawTexturedVerticalQuad(center, blockWidth, blockHeight, true, farZ, hescoTexture);
                DrawTexturedVerticalQuad(center, blockWidth, blockHeight, false, leftX, hescoTexture);
                DrawTexturedVerticalQuad(center, blockWidth, blockHeight, false, rightX, hescoTexture);

                Vector3 topPos = new Vector3(center.X, floorHeightOffset + blockHeight + 0.01f, center.Z);
                DrawTexturedPlane(topPos, new Vector3(blockWidth, 1f, blockWidth), hescoTexture);
            }
        }

        public void DrawFurniture(IEnumerable<FurnitureData> furnitures, int cellSize, float floorHeightOffset)
        {
            if (furnitures == null)
                return;

            float feetToWorld = cellSize / (float)Unit.FeetPerCell;

            foreach (FurnitureData furniture in furnitures)
            {
                float heightWorld = FurnitureData.GetHeightFeet(furniture.Type) * feetToWorld;
                Vector2 footprint = GetFurnitureFootprint(furniture.Type, cellSize);
                Vector3 scale = new Vector3(footprint.X, heightWorld, footprint.Y);

                Vector3 center = new Vector3(
                    furniture.X * cellSize + cellSize / 2f,
                    floorHeightOffset + heightWorld / 2f,
                    furniture.Y * cellSize + cellSize / 2f);

                DrawCube(center, scale, GetFurnitureColor(furniture.Type));
            }
        }

        private static Vector2 GetFurnitureFootprint(FurnitureType type, int cellSize)
        {
            float baseSize = cellSize * 0.78f;
            float feetToWorld = cellSize / (float)Unit.FeetPerCell;
            return type switch
            {
                FurnitureType.Bed => new Vector2(cellSize * 0.95f, cellSize * 0.75f),
                FurnitureType.Fridge => new Vector2(cellSize * 0.62f, cellSize * 0.62f),
                FurnitureType.Chair => new Vector2(cellSize * 0.48f, cellSize * 0.48f),
                FurnitureType.SedanToyotaCorolla => new Vector2(15.2f * feetToWorld, 5.9f * feetToWorld),
                FurnitureType.SedanBmwSeries3 => new Vector2(15.6f * feetToWorld, 6.0f * feetToWorld),
                FurnitureType.SedanMercedesEClass => new Vector2(16.3f * feetToWorld, 6.2f * feetToWorld),
                FurnitureType.PickupToyotaTacoma => new Vector2(17.9f * feetToWorld, 6.3f * feetToWorld),
                FurnitureType.PickupFordF150 => new Vector2(19.5f * feetToWorld, 6.7f * feetToWorld),
                FurnitureType.PickupRam3500 => new Vector2(20.3f * feetToWorld, 6.8f * feetToWorld),
                _ => new Vector2(baseSize, baseSize)
            };
        }

        private static Color GetFurnitureColor(FurnitureType type)
        {
            return type switch
            {
                FurnitureType.Counter => new Color(146, 115, 88),
                FurnitureType.Fridge => new Color(188, 196, 208),
                FurnitureType.Table => new Color(124, 90, 62),
                FurnitureType.Chair => new Color(95, 72, 50),
                FurnitureType.Stove => new Color(96, 96, 104),
                FurnitureType.Bed => new Color(96, 128, 172),
                FurnitureType.SedanToyotaCorolla => new Color(198, 202, 208),
                FurnitureType.SedanBmwSeries3 => new Color(60, 64, 76),
                FurnitureType.SedanMercedesEClass => new Color(26, 26, 30),
                FurnitureType.PickupToyotaTacoma => new Color(76, 88, 112),
                FurnitureType.PickupFordF150 => new Color(178, 46, 52),
                FurnitureType.PickupRam3500 => new Color(220, 220, 228),
                _ => new Color(130, 130, 130)
            };
        }

        private static float ComputeCornerHeight(IReadOnlyDictionary<Point, float> terrainHeights, int vertexX, int vertexZ)
        {
            float sum = 0f;
            int count = 0;

            // Les sommets sont partagés entre jusqu'à 4 tuiles ; on moyenne
            // leurs offsets pour garantir des jonctions propres entre tuiles.
            foreach (int cellX in new[] { vertexX - 1, vertexX })
            {
                foreach (int cellZ in new[] { vertexZ - 1, vertexZ })
                {
                    if (terrainHeights != null && terrainHeights.TryGetValue(new Point(cellX, cellZ), out float height))
                    {
                        sum += height;
                        count++;
                    }
                }
            }

            if (count > 0)
                return sum / count;

            return 0f;
        }

        private static Vector3 ComputeVertexNormal(Vector3 previous, Vector3 next, Vector3 opposite)
        {
            Vector3 edgeA = next - previous;
            Vector3 edgeB = opposite - previous;
            Vector3 normal = Vector3.Cross(edgeA, edgeB);

            if (normal.LengthSquared() <= 0.000001f)
                return Vector3.Up;

            normal.Normalize();
            if (normal.Y < 0f)
                normal = -normal;
            return normal;
        }

        private void DrawTexturedTerrainTile(int x, int z, int size, Texture2D tex, IReadOnlyDictionary<Point, float> terrainHeights, float floorHeightOffset)
        {
            float xMin = x * size;
            float xMax = (x + 1) * size;
            float zMin = z * size;
            float zMax = (z + 1) * size;

            float yNW = floorHeightOffset + ComputeCornerHeight(terrainHeights, x, z);
            float ySW = floorHeightOffset + ComputeCornerHeight(terrainHeights, x, z + 1);
            float ySE = floorHeightOffset + ComputeCornerHeight(terrainHeights, x + 1, z + 1);
            float yNE = floorHeightOffset + ComputeCornerHeight(terrainHeights, x + 1, z);

            Vector3 nw = new Vector3(xMin, yNW, zMin);
            Vector3 sw = new Vector3(xMin, ySW, zMax);
            Vector3 se = new Vector3(xMax, ySE, zMax);
            Vector3 ne = new Vector3(xMax, yNE, zMin);

            // Normales douces calculées par sommet pour mieux éclairer les pentes.
            Vector3 nNW = ComputeVertexNormal(nw, sw, ne);
            Vector3 nSW = ComputeVertexNormal(sw, se, nw);
            Vector3 nSE = ComputeVertexNormal(se, ne, sw);
            Vector3 nNE = ComputeVertexNormal(ne, nw, se);

            VertexPositionNormalTexture[] verts = new[]
            {
                new VertexPositionNormalTexture(nw, nNW, new Vector2(0f, 0f)),
                new VertexPositionNormalTexture(sw, nSW, new Vector2(0f, 1f)),
                new VertexPositionNormalTexture(se, nSE, new Vector2(1f, 1f)),
                new VertexPositionNormalTexture(ne, nNE, new Vector2(1f, 0f))
            };

            textured.World = Matrix.Identity;
            textured.Texture = tex;
            foreach (var pass in textured.CurrentTechnique.Passes)
                pass.Apply();

            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 4, texturedPlaneIdx, 0, 2);
        }

        public void DrawGrid(int w, int h, int size, Texture2D tex, float floorHeightOffset = 0f)
        {
            for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
                    DrawTexturedPlane(new Vector3(x * size + size / 2f, floorHeightOffset, z * size + size / 2f),
                                      new Vector3(size * TileFillRatio, 1, size * TileFillRatio), tex);
        }

        public void DrawGridCells(IEnumerable<Point> cells, int size, Texture2D tex, float floorHeightOffset = 0f)
        {
            foreach (var cell in cells)
            {
                DrawTexturedPlane(
                    new Vector3(cell.X * size + size / 2f, floorHeightOffset, cell.Y * size + size / 2f),
                    new Vector3(size * TileFillRatio, 1, size * TileFillRatio),
                    tex);
            }
        }

        public void DrawGridWithTerrain(int w, int h, int size, Texture2D tex, IReadOnlyDictionary<Point, float> terrainHeights, float floorHeightOffset = 0f)
        {
            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < h; z++)
                {
                    DrawTexturedTerrainTile(x, z, size, tex, terrainHeights, floorHeightOffset);
                }
            }
        }

        public void DrawTerrainCells(IEnumerable<Point> cells, int size, Texture2D tex, IReadOnlyDictionary<Point, float> terrainHeights, float floorHeightOffset = 0f)
        {
            if (cells == null)
                return;

            foreach (Point cell in cells)
            {
                DrawTexturedTerrainTile(cell.X, cell.Y, size, tex, terrainHeights, floorHeightOffset);
            }
        }

        /// <summary>
        /// ? MURS AMÉLIORÉS - Version avec détails, hauteur et ombres
        /// </summary>
        public void DrawWalls(HashSet<WallSegment> walls, int size, bool editorMode = false, float floorHeightOffset = 0f, Color? wallOverrideColor = null, Texture2D brickWallTexture = null, Texture2D hescoWallTexture = null)
        {
            foreach (var s in walls)
            {
                Vector3 start = new(s.Start.X * size, floorHeightOffset, s.Start.Y * size);
                Vector3 end = new(s.End.X * size, floorHeightOffset, s.End.Y * size);
                Vector3 center = (start + end) / 2f;

                // Hauteur de mur pilotée par WallHeightRatio (2.0f = 2 cases de haut).
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

                Texture2D wallTexture = null;
                if (!editorMode)
                {
                    wallTexture = s.Material switch
                    {
                        WallMaterial.Brick => brickWallTexture,
                        WallMaterial.Hesco => hescoWallTexture,
                        _ => null
                    };
                }

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

                    DrawWallSection(bottomCenter, bottomScale, s.IsHorizontal, wallColor, wallTexture);

                    // 2. LINTEAU (Le muret du haut - 20% de la hauteur totale)
                    float topPartHeight = wallHeight * 0.2f;
                    Vector3 topPartCenter = center;
                    topPartCenter.Y = floorHeightOffset + wallHeight - (topPartHeight / 2f); // On le colle en haut
                    Vector3 topPartScale = s.IsHorizontal
                        ? new Vector3(size, topPartHeight, thickness)
                        : new Vector3(thickness, topPartHeight, size);

                    DrawWallSection(topPartCenter, topPartScale, s.IsHorizontal, wallColor, wallTexture);

                    // Le milieu reste vide pour laisser passer la ligne de vue !
                }
                else if (s.Type == WallType.Door)
                {
                    // Porte ouverte: montants latéraux + linteau en haut.
                    // L'ouverture centrale évite l'effet "mur traversé" pour les unités.
                    float frameWidth = s.IsHorizontal ? size * 0.2f : size * 0.2f;
                    float openingHeight = wallHeight * 0.72f;
                    float topPartHeight = wallHeight - openingHeight;

                    Vector3 leftCenter = center;
                    Vector3 rightCenter = center;

                    if (s.IsHorizontal)
                    {
                        leftCenter.X -= (size - frameWidth) / 2f;
                        rightCenter.X += (size - frameWidth) / 2f;
                    }
                    else
                    {
                        leftCenter.Z -= (size - frameWidth) / 2f;
                        rightCenter.Z += (size - frameWidth) / 2f;
                    }

                    leftCenter.Y = floorHeightOffset + openingHeight / 2f;
                    rightCenter.Y = floorHeightOffset + openingHeight / 2f;

                    Vector3 frameScale = s.IsHorizontal
                        ? new Vector3(frameWidth, openingHeight, thickness)
                        : new Vector3(thickness, openingHeight, frameWidth);

                    DrawWallSection(leftCenter, frameScale, s.IsHorizontal, wallColor, wallTexture);
                    DrawWallSection(rightCenter, frameScale, s.IsHorizontal, wallColor, wallTexture);

                    Vector3 topPartCenter = center;
                    topPartCenter.Y = floorHeightOffset + openingHeight + (topPartHeight / 2f);
                    Vector3 topPartScale = s.IsHorizontal
                        ? new Vector3(size, topPartHeight, thickness)
                        : new Vector3(thickness, topPartHeight, size);

                    DrawWallSection(topPartCenter, topPartScale, s.IsHorizontal, wallColor, wallTexture);
                }
                else
                {
                    // Mur plein classique (Portes et Murs normaux)
                    DrawWallSection(center, scale, s.IsHorizontal, wallColor, wallTexture);

                    // ? Ligne de démarcation (jointure au milieu) - Uniquement pour les murs pleins
                    if (!editorMode && s.Material == WallMaterial.Standard)
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

            float floorYOffset = WorldMetrics.FloorToWorldY(floorToRender, cellSize);
            foreach (var ramp in ramps)
            {
                if (ramp.Floor != floorToRender)
                    continue;

                int dx = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDx : 0;
                int dy = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDy : -1;
                DrawDirectionalRamp(ramp.X, ramp.Y, dx, dy, floorYOffset, cellSize);
            }
        }

        private void DrawDirectionalRamp(int cellX, int cellY, int ascendDx, int ascendDy, float floorYOffset, int cellSize)
        {
            const int slices = 6;
            float sliceDepth = cellSize / (float)slices;

            for (int i = 0; i < slices; i++)
            {
                float t = (i + 1f) / slices;
                float sliceHeight = t * cellSize;
                float offsetAlongAxis = i * sliceDepth;

                float centerX = cellX * cellSize + cellSize / 2f;
                float centerZ = cellY * cellSize + cellSize / 2f;

                if (ascendDx != 0)
                {
                    centerX += -ascendDx * (offsetAlongAxis - (cellSize * 0.5f - sliceDepth * 0.5f));
                }
                else
                {
                    centerZ += -ascendDy * (offsetAlongAxis - (cellSize * 0.5f - sliceDepth * 0.5f));
                }

                Vector3 pos = new Vector3(
                    centerX,
                    floorYOffset + sliceHeight / 2f,
                    centerZ);

                Vector3 size = ascendDx != 0
                    ? new Vector3(sliceDepth * 0.95f, sliceHeight, cellSize * 0.95f)
                    : new Vector3(cellSize * 0.95f, sliceHeight, sliceDepth * 0.95f);

                Color color = Color.Lerp(new Color(170, 120, 80), new Color(220, 180, 120), t);
                DrawCube(pos, size, color * 0.85f);
            }
        }

        public void DrawStairConnections(IEnumerable<StairConnectionData> stairs, int floorToRender, int cellSize)
        {
            if (stairs == null) return;

            float floorYOffset = WorldMetrics.FloorToWorldY(floorToRender, cellSize);
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




        public void DrawUnit(Unit unit, int cellSize, Color? bodyColorOverride = null, bool drawEquipment = true,
            Vector3? positionOverride = null, Matrix? modelRotationOverride = null)
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
                drawEquipment,
                positionOverride,
                modelRotationOverride
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
            {
                Color grenadeColor = GrenadeDatabase.GetGrenadeColor(g.Data.Type);
                DrawCube(g.Position, new Vector3(size * 0.2f), grenadeColor);

                if (!g.EmitsLight)
                    continue;

                float pulse = 0.72f + 0.28f * (float)Math.Sin(globalAnimationTime * 14f + g.Progress * MathHelper.TwoPi);
                DrawCube(g.Position, new Vector3(size * 0.32f), new Color(255, 250, 200, 170) * pulse);

                Vector3 haloPos = new Vector3(g.Position.X, g.Position.Y - size * 0.12f, g.Position.Z);
                DrawPlane(haloPos, new Vector3(size * 0.95f, 1f, size * 0.95f), new Color(255, 245, 170, 90) * pulse);
            }
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
            float floorYOffset = WorldMetrics.FloorToWorldY(viewedFloor, cellSize);

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

            bool previousLighting = basic.LightingEnabled;
            basic.LightingEnabled = false;

            try
            {
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
                        WorldMetrics.FloorToWorldY(node.Floor, cellSize) + 0.09f,
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
                            WorldMetrics.FloorToWorldY(nextNode.Floor, cellSize) + 0.09f,
                            nextCell.Y * cellSize + cellSize / 2f
                        );

                        DrawPathSegment(pos, nextPos, pathColor * pulse, cellSize);
                    }
                }
            }
            finally
            {
                basic.LightingEnabled = previousLighting;
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
