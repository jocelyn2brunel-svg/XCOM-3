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
        private float geometryOpacityMultiplier = 1f;
        private float textureOpacityMultiplier = 1f;

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
            DrawCube(pos, scale, color, Matrix.Identity);
        }

        public void DrawCube(Vector3 pos, Vector3 scale, Color color, Matrix rotation)
        {
            Color effectiveColor = color * geometryOpacityMultiplier;
            var verts = cubeVerts.Select(v => new VertexPositionColor(v.Position, effectiveColor)).ToArray();
            DrawVertices(verts, cubeIdx, Matrix.CreateScale(scale) * rotation * Matrix.CreateTranslation(pos));
        }

        public void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            Color effectiveColor = color * geometryOpacityMultiplier;
            VertexPositionColor[] lineVertices = new[]
            {
                new VertexPositionColor(start, effectiveColor),
                new VertexPositionColor(end, effectiveColor)
            };

            basic.World = Matrix.Identity;
            foreach (var pass in basic.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.LineList, lineVertices, 0, 1);
            }
        }

        /// <summary>
        /// Rend un segment incliné entre deux points 3D (world-space) sous forme de quad
        /// plat suivi le slope du terrain. Remplace DrawCube pour les arêtes du hover outline,
        /// garantissant une correspondance exacte avec la géométrie des tuiles.
        /// </summary>
        public void DrawSlopedEdge(Vector3 start, Vector3 end, Color color, float width)
        {
            Color effectiveColor = color * geometryOpacityMultiplier;

            Vector3 dir = end - start;
            float length = dir.Length();
            if (length < 0.0001f) return;
            dir /= length;

            // Direction perpendiculaire dans le plan horizontal (rotation 90° en XZ)
            Vector3 perp = Vector3.Normalize(new Vector3(-dir.Z, 0f, dir.X)) * (width * 0.5f);

            VertexPositionColor[] verts = new[]
            {
                new VertexPositionColor(start - perp, effectiveColor),
                new VertexPositionColor(start + perp, effectiveColor),
                new VertexPositionColor(end   + perp, effectiveColor),
                new VertexPositionColor(end   - perp, effectiveColor),
            };
            short[] idx = new short[] { 0, 1, 2, 0, 2, 3 };

            basic.World = Matrix.Identity;
            foreach (var pass in basic.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 4, idx, 0, 2);
            }
        }

        public void DrawPlane(Vector3 pos, Vector3 scale, Color color)
            => DrawPlane(pos, scale, color, 0f, 0f, 0f);

        public void DrawPlane(Vector3 pos, Vector3 scale, Color color, float rotationX, float rotationY, float rotationZ)
        {
            Color effectiveColor = color * geometryOpacityMultiplier;
            var verts = planeVerts.Select(v => new VertexPositionColor(v.Position, effectiveColor)).ToArray();
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
            textured.Alpha = textureOpacityMultiplier;
            foreach (var pass in textured.CurrentTechnique.Passes)
                pass.Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, texturedPlaneVerts, 0, 4, texturedPlaneIdx, 0, 2);
            textured.Alpha = 1f;
        }

        private void DrawTexturedFloorVolumeTop(Vector3 topCenter, Vector3 topScale, Texture2D tex, int cellSize)
        {
            float slabThickness = Math.Max(0.001f, cellSize * WorldMetrics.FloorSlabThicknessInCells);

            // Volume extrudé vers le bas: la face supérieure reste alignée sur topCenter.Y.
            Vector3 slabCenter = new Vector3(
                topCenter.X,
                topCenter.Y - slabThickness / 2f,
                topCenter.Z);
            DrawCube(slabCenter, new Vector3(topScale.X, slabThickness, topScale.Z), new Color(96, 96, 96));

            // Dessiner la texture légèrement au-dessus pour éviter le z-fighting avec la face du volume.
            DrawTexturedPlane(
                new Vector3(topCenter.X, topCenter.Y + 0.001f, topCenter.Z),
                topScale,
                tex);
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
            textured.Alpha = textureOpacityMultiplier;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            foreach (var pass in textured.CurrentTechnique.Passes)
                pass.Apply();

            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 4, texturedPlaneIdx, 0, 2);
            textured.Alpha = 1f;
        }

        private void ExecuteWithOpacity(float opacity, Action drawAction, bool affectTextures = false)
        {
            float clamped = MathHelper.Clamp(opacity, 0f, 1f);
            BlendState previousBlend = gd.BlendState;
            float previousGeometryOpacity = geometryOpacityMultiplier;
            float previousTextureOpacity = textureOpacityMultiplier;

            if (clamped < 0.999f)
                gd.BlendState = BlendState.AlphaBlend;

            geometryOpacityMultiplier = clamped;
            if (affectTextures)
                textureOpacityMultiplier = clamped;

            drawAction?.Invoke();

            geometryOpacityMultiplier = previousGeometryOpacity;
            textureOpacityMultiplier = previousTextureOpacity;

            if (clamped < 0.999f)
                gd.BlendState = previousBlend;
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

        public void DrawFurniture(IEnumerable<FurnitureData> furnitures, int cellSize, float floorHeightOffset, float opacity = 1f)
        {
            if (furnitures == null)
                return;

            float feetToWorld = cellSize / (float)Unit.FeetPerCell;
            ExecuteWithOpacity(opacity, () =>
            {
                foreach (FurnitureData furniture in furnitures)
                {
                    float heightWorld = FurnitureData.GetHeightFeet(furniture.Type) * feetToWorld;
                    Vector2 footprint = GetFurnitureFootprint(furniture.Type, cellSize);
                    Vector3 scale = new Vector3(footprint.X, heightWorld, footprint.Y);

                    bool isVehicle = IsVehicleFurniture(furniture.Type);
                    Vector3 center = new Vector3(
                        furniture.X * cellSize + (isVehicle ? 0f : cellSize / 2f),
                        floorHeightOffset + heightWorld / 2f,
                        furniture.Y * cellSize + (isVehicle ? 0f : cellSize / 2f));

                    if (isVehicle)
                        DrawVehicleFurniture(furniture.Type, center, scale);
                    else
                        DrawDetailedFurniture(furniture.Type, center, scale, furniture.OrientationRadians);
                }
            });
        }

        private void DrawDetailedFurniture(FurnitureType type, Vector3 center, Vector3 totalScale, float orientationRadians)
        {
            Color baseColor = GetFurnitureColor(type);

            switch (type)
            {
                case FurnitureType.Table:
                    DrawTableFurniture(center, totalScale, baseColor);
                    break;

                case FurnitureType.Chair:
                    DrawChairFurniture(center, totalScale, baseColor, orientationRadians);
                    break;

                case FurnitureType.Bed:
                    DrawBedFurniture(center, totalScale, baseColor);
                    break;

                case FurnitureType.Fridge:
                    DrawFridgeFurniture(center, totalScale, baseColor);
                    break;

                case FurnitureType.Stove:
                    DrawStoveFurniture(center, totalScale, baseColor);
                    break;

                case FurnitureType.Counter:
                    DrawCounterFurniture(center, totalScale, baseColor);
                    break;

                case FurnitureType.TreePine:
                    DrawTreePineFurniture(center, totalScale, baseColor);
                    break;

                case FurnitureType.LootCrate:
                    DrawLootCrateFurniture(center, totalScale, baseColor);
                    break;
                
                case FurnitureType.Computer:
                    DrawComputerFurniture(center, totalScale, baseColor);
                    break;

                default:
                    DrawCube(center, totalScale, baseColor);
                    break;
            }
        }

        private void DrawComputerFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;

            // Corps principal (armoire serveur)
            Vector3 bodyScale = new Vector3(totalScale.X * 0.8f, totalScale.Y * 0.95f, totalScale.Z * 0.8f);
            Vector3 bodyCenter = new Vector3(center.X, bottomY + bodyScale.Y / 2f, center.Z);
            DrawCube(bodyCenter, bodyScale, new Color(40, 40, 45));

            // Écran/Face avant
            Vector3 screenScale = new Vector3(bodyScale.X * 0.9f, bodyScale.Y * 0.4f, bodyScale.Z * 0.1f);
            Vector3 screenCenter = new Vector3(center.X, bodyCenter.Y + bodyScale.Y * 0.2f, center.Z - bodyScale.Z * 0.45f);
            float pulse = (float)Math.Sin(globalAnimationTime * 2f) * 0.2f + 0.8f;
            DrawCube(screenCenter, screenScale, new Color(0, 150, 255) * pulse); // Écran bleu qui pulse

            // Clavier/Console
            Vector3 keyboardScale = new Vector3(bodyScale.X * 0.9f, bodyScale.Y * 0.05f, bodyScale.Z * 0.3f);
            Vector3 keyboardCenter = new Vector3(center.X, bodyCenter.Y - bodyScale.Y * 0.1f, center.Z - bodyScale.Z * 0.55f);
            DrawCube(keyboardCenter, keyboardScale, new Color(60, 60, 65));

            // Lumières d'état
            for (int i = 0; i < 3; i++)
            {
                Vector3 lightScale = new Vector3(bodyScale.X * 0.1f, bodyScale.Y * 0.05f, bodyScale.Z * 0.05f);
                Vector3 lightCenter = new Vector3(center.X - bodyScale.X * 0.3f + i * bodyScale.X * 0.3f, bodyCenter.Y - bodyScale.Y * 0.3f, center.Z - bodyScale.Z * 0.46f);
                Color lightColor = i == 0 ? Color.Red : (i == 1 ? Color.Green : Color.Yellow);
                DrawCube(lightCenter, lightScale, lightColor * pulse);
            }
        }

        private void DrawTreePineFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;
            float trunkHeight = totalScale.Y * 0.25f;
            float trunkWidth = totalScale.X * 0.18f;

            // Trunk (Tronc)
            Vector3 trunkScale = new Vector3(trunkWidth, trunkHeight, trunkWidth);
            Vector3 trunkCenter = new Vector3(center.X, bottomY + trunkHeight / 2f, center.Z);
            DrawCube(trunkCenter, trunkScale, new Color(90, 60, 40)); // Dark brown

            // Foliage (Aiguilles) - 4 tiers for a pine shape
            int tiers = 4;
            float foliageHeight = totalScale.Y - trunkHeight;
            float tierHeight = (foliageHeight / tiers) * 1.3f; // Slight overlap

            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)(tiers - 1);
                // The tier width gets smaller as we go up
                float tierWidth = MathHelper.Lerp(totalScale.X, totalScale.X * 0.2f, t);
                Vector3 tierScale = new Vector3(tierWidth, tierHeight, tierWidth);

                // Position each tier slightly above the previous one
                float tierBottomY = bottomY + trunkHeight + (i * (foliageHeight / tiers));
                Vector3 tierCenter = new Vector3(center.X, tierBottomY + tierHeight / 2f, center.Z);

                // Alternate greens for a bit of variety
                Color foliageColor = Color.Lerp(new Color(34, 110, 34), new Color(20, 70, 20), t);
                DrawCube(tierCenter, tierScale, foliageColor);
            }
        }

        private void DrawLootCrateFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;

            // Corps principal de la caisse
            Vector3 bodyScale = new Vector3(totalScale.X * 0.92f, totalScale.Y * 0.88f, totalScale.Z * 0.92f);
            Vector3 bodyCenter = new Vector3(center.X, bottomY + bodyScale.Y / 2f, center.Z);
            DrawCube(bodyCenter, bodyScale, baseColor);

            // Couvercle légèrement plus large
            Vector3 lidScale = new Vector3(totalScale.X * 0.96f, totalScale.Y * 0.14f, totalScale.Z * 0.96f);
            Vector3 lidCenter = new Vector3(center.X, bottomY + bodyScale.Y + lidScale.Y / 2f, center.Z);
            DrawCube(lidCenter, lidScale, baseColor * 1.12f);

            // Armatures métalliques (coins horizontaux)
            Color metalColor = new Color(80, 80, 88);
            float bandThickness = totalScale.Y * 0.06f;
            float bandWidth = totalScale.X * 0.94f;
            float bandMid = bottomY + bodyScale.Y * 0.5f;

            DrawCube(new Vector3(center.X, bandMid, center.Z),
                     new Vector3(bandWidth, bandThickness, totalScale.Z * 0.94f),
                     metalColor);
        }

        private void DrawCounterFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;
            Vector3 bodyScale = new Vector3(totalScale.X, totalScale.Y * 0.9f, totalScale.Z);
            Vector3 bodyCenter = new Vector3(center.X, bottomY + bodyScale.Y / 2f, center.Z);
            DrawCube(bodyCenter, bodyScale, baseColor);

            Vector3 topScale = new Vector3(totalScale.X * 1.05f, totalScale.Y * 0.10f, totalScale.Z * 1.05f);
            Vector3 topCenter = new Vector3(center.X, bodyCenter.Y + bodyScale.Y / 2f + topScale.Y / 2f, center.Z);
            DrawCube(topCenter, topScale, baseColor * 1.15f);

            float insetZ = totalScale.Z * 0.47f;
            DrawCube(new Vector3(center.X, bodyCenter.Y, center.Z - insetZ), new Vector3(totalScale.X * 0.86f, bodyScale.Y * 0.94f, totalScale.Z * 0.04f), baseColor * 0.82f);
            DrawCube(new Vector3(center.X, bodyCenter.Y, center.Z + insetZ), new Vector3(totalScale.X * 0.86f, bodyScale.Y * 0.94f, totalScale.Z * 0.04f), baseColor * 0.82f);
        }

        private void DrawTableFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;
            float topThickness = totalScale.Y * 0.14f;
            Vector3 topScale = new Vector3(totalScale.X, topThickness, totalScale.Z);
            Vector3 topCenter = new Vector3(center.X, bottomY + totalScale.Y - topThickness / 2f, center.Z);
            DrawCube(topCenter, topScale, baseColor * 1.08f);

            float legHeight = totalScale.Y - topThickness;
            Vector3 legScale = new Vector3(totalScale.X * 0.13f, legHeight, totalScale.Z * 0.13f);
            float legOffsetX = totalScale.X * 0.36f;
            float legOffsetZ = totalScale.Z * 0.36f;

            DrawCube(new Vector3(center.X - legOffsetX, bottomY + legHeight / 2f, center.Z - legOffsetZ), legScale, baseColor * 0.86f);
            DrawCube(new Vector3(center.X + legOffsetX, bottomY + legHeight / 2f, center.Z - legOffsetZ), legScale, baseColor * 0.86f);
            DrawCube(new Vector3(center.X - legOffsetX, bottomY + legHeight / 2f, center.Z + legOffsetZ), legScale, baseColor * 0.86f);
            DrawCube(new Vector3(center.X + legOffsetX, bottomY + legHeight / 2f, center.Z + legOffsetZ), legScale, baseColor * 0.86f);
        }

        private void DrawChairFurniture(Vector3 center, Vector3 totalScale, Color baseColor, float orientationRadians)
        {
            float bottomY = center.Y - totalScale.Y / 2f;
            float seatHeight = totalScale.Y * 0.20f;
            float seatBottom = bottomY + totalScale.Y * 0.36f;
            Matrix rotation = Matrix.CreateRotationY(orientationRadians);

            Vector3 seatScale = new Vector3(totalScale.X * 0.92f, seatHeight, totalScale.Z * 0.92f);
            DrawCube(new Vector3(center.X, seatBottom + seatHeight / 2f, center.Z), seatScale, baseColor * 1.06f, rotation);

            float backHeight = totalScale.Y * 0.42f;
            Vector3 backScale = new Vector3(totalScale.X * 0.90f, backHeight, totalScale.Z * 0.15f);
            Vector3 backOffset = Vector3.Transform(new Vector3(0f, 0f, -totalScale.Z * 0.38f), rotation);
            DrawCube(new Vector3(center.X + backOffset.X, seatBottom + seatHeight + backHeight / 2f, center.Z + backOffset.Z), backScale, baseColor * 0.9f, rotation);

            float legHeight = seatBottom - bottomY;
            Vector3 legScale = new Vector3(totalScale.X * 0.15f, legHeight, totalScale.Z * 0.15f);
            float legOffsetX = totalScale.X * 0.31f;
            float legOffsetZ = totalScale.Z * 0.31f;

            Vector3 legBackLeft = Vector3.Transform(new Vector3(-legOffsetX, 0f, -legOffsetZ), rotation);
            Vector3 legBackRight = Vector3.Transform(new Vector3(legOffsetX, 0f, -legOffsetZ), rotation);
            Vector3 legFrontLeft = Vector3.Transform(new Vector3(-legOffsetX, 0f, legOffsetZ), rotation);
            Vector3 legFrontRight = Vector3.Transform(new Vector3(legOffsetX, 0f, legOffsetZ), rotation);

            DrawCube(new Vector3(center.X + legBackLeft.X, bottomY + legHeight / 2f, center.Z + legBackLeft.Z), legScale, baseColor * 0.8f, rotation);
            DrawCube(new Vector3(center.X + legBackRight.X, bottomY + legHeight / 2f, center.Z + legBackRight.Z), legScale, baseColor * 0.8f, rotation);
            DrawCube(new Vector3(center.X + legFrontLeft.X, bottomY + legHeight / 2f, center.Z + legFrontLeft.Z), legScale, baseColor * 0.8f, rotation);
            DrawCube(new Vector3(center.X + legFrontRight.X, bottomY + legHeight / 2f, center.Z + legFrontRight.Z), legScale, baseColor * 0.8f, rotation);
        }

        private void DrawBedFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;
            float frameHeight = totalScale.Y * 0.28f;
            float mattressHeight = totalScale.Y * 0.46f;
            float headboardHeight = totalScale.Y * 0.40f;

            DrawCube(
                new Vector3(center.X, bottomY + frameHeight / 2f, center.Z),
                new Vector3(totalScale.X, frameHeight, totalScale.Z),
                new Color(124, 94, 68));

            DrawCube(
                new Vector3(center.X, bottomY + frameHeight + mattressHeight / 2f, center.Z),
                new Vector3(totalScale.X * 0.94f, mattressHeight, totalScale.Z * 0.92f),
                baseColor * 1.16f);

            DrawCube(
                new Vector3(center.X - totalScale.X * 0.41f, bottomY + frameHeight + mattressHeight + headboardHeight / 2f, center.Z),
                new Vector3(totalScale.X * 0.16f, headboardHeight, totalScale.Z * 0.96f),
                new Color(86, 66, 48));

            DrawCube(
                new Vector3(center.X + totalScale.X * 0.28f, bottomY + frameHeight + mattressHeight * 0.58f, center.Z - totalScale.Z * 0.22f),
                new Vector3(totalScale.X * 0.24f, mattressHeight * 0.36f, totalScale.Z * 0.28f),
                new Color(232, 232, 236));
        }

        private void DrawFridgeFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;
            float bodyHeight = totalScale.Y * 0.95f;
            Vector3 bodyScale = new Vector3(totalScale.X, bodyHeight, totalScale.Z);
            Vector3 bodyCenter = new Vector3(center.X, bottomY + bodyHeight / 2f, center.Z);
            DrawCube(bodyCenter, bodyScale, baseColor);

            DrawCube(
                new Vector3(center.X, bodyCenter.Y + bodyHeight * 0.06f, center.Z - totalScale.Z * 0.46f),
                new Vector3(totalScale.X * 0.90f, totalScale.Y * 0.02f, totalScale.Z * 0.05f),
                baseColor * 0.76f);

            DrawCube(
                new Vector3(center.X + totalScale.X * 0.34f, bodyCenter.Y + bodyHeight * 0.18f, center.Z - totalScale.Z * 0.47f),
                new Vector3(totalScale.X * 0.07f, bodyHeight * 0.24f, totalScale.Z * 0.05f),
                baseColor * 0.62f);

            DrawCube(
                new Vector3(center.X + totalScale.X * 0.34f, bodyCenter.Y - bodyHeight * 0.20f, center.Z - totalScale.Z * 0.47f),
                new Vector3(totalScale.X * 0.07f, bodyHeight * 0.18f, totalScale.Z * 0.05f),
                baseColor * 0.62f);
        }

        private void DrawStoveFurniture(Vector3 center, Vector3 totalScale, Color baseColor)
        {
            float bottomY = center.Y - totalScale.Y / 2f;
            Vector3 bodyScale = new Vector3(totalScale.X * 0.95f, totalScale.Y * 0.92f, totalScale.Z * 0.95f);
            Vector3 bodyCenter = new Vector3(center.X, bottomY + bodyScale.Y / 2f, center.Z);
            DrawCube(bodyCenter, bodyScale, baseColor);

            DrawCube(
                new Vector3(center.X, bodyCenter.Y + bodyScale.Y / 2f - totalScale.Y * 0.05f, center.Z),
                new Vector3(bodyScale.X * 0.96f, totalScale.Y * 0.08f, bodyScale.Z * 0.96f),
                new Color(58, 60, 68));

            float burnerSizeX = bodyScale.X * 0.22f;
            float burnerSizeZ = bodyScale.Z * 0.22f;
            float burnerY = bodyCenter.Y + bodyScale.Y / 2f;
            float offsetX = bodyScale.X * 0.24f;
            float offsetZ = bodyScale.Z * 0.24f;
            Color burnerColor = new Color(22, 24, 30);

            DrawCube(new Vector3(center.X - offsetX, burnerY, center.Z - offsetZ), new Vector3(burnerSizeX, totalScale.Y * 0.04f, burnerSizeZ), burnerColor);
            DrawCube(new Vector3(center.X + offsetX, burnerY, center.Z - offsetZ), new Vector3(burnerSizeX, totalScale.Y * 0.04f, burnerSizeZ), burnerColor);
            DrawCube(new Vector3(center.X - offsetX, burnerY, center.Z + offsetZ), new Vector3(burnerSizeX, totalScale.Y * 0.04f, burnerSizeZ), burnerColor);
            DrawCube(new Vector3(center.X + offsetX, burnerY, center.Z + offsetZ), new Vector3(burnerSizeX, totalScale.Y * 0.04f, burnerSizeZ), burnerColor);
        }

        private static bool IsVehicleFurniture(FurnitureType type)
        {
            return type is FurnitureType.SedanToyotaCorolla
                or FurnitureType.SedanBmwSeries3
                or FurnitureType.SedanMercedesEClass
                or FurnitureType.PickupToyotaTacoma
                or FurnitureType.PickupFordF150
                or FurnitureType.PickupRam3500;
        }

        private void DrawVehicleFurniture(FurnitureType type, Vector3 center, Vector3 totalScale)
        {
            Color bodyColor = GetFurnitureColor(type);
            Color trimColor = new Color(36, 38, 44);
            Color wheelColor = new Color(18, 18, 22);
            Color rimColor = new Color(136, 142, 156);
            Color windowColor = new Color(112, 158, 196) * 0.75f;
            Color doorLineColor = bodyColor * 0.72f;

            bool isPickup = type is FurnitureType.PickupToyotaTacoma
                or FurnitureType.PickupFordF150
                or FurnitureType.PickupRam3500;

            float length = totalScale.X;
            float width = totalScale.Z;
            float height = totalScale.Y;
            float bottomY = center.Y - height / 2f;

            float bodyHeight = isPickup ? height * 0.56f : height * 0.50f;
            float cabinHeight = isPickup ? height * 0.28f : height * 0.30f;
            float wheelHeight = height * 0.22f;

            // Châssis principal
            Vector3 bodyScale = new Vector3(length * 0.96f, bodyHeight, width * 0.90f);
            Vector3 bodyCenter = new Vector3(center.X, bottomY + wheelHeight + bodyHeight / 2f, center.Z);
            DrawCube(bodyCenter, bodyScale, bodyColor);

            if (isPickup)
            {
                // Cabine pickup
                Vector3 cabinScale = new Vector3(length * 0.38f, cabinHeight, width * 0.78f);
                Vector3 cabinCenter = new Vector3(
                    center.X - length * 0.20f,
                    bodyCenter.Y + bodyHeight / 2f + cabinHeight / 2f - height * 0.03f,
                    center.Z);
                DrawCube(cabinCenter, cabinScale, bodyColor * 1.02f);

                // Benne arrière
                Vector3 bedScale = new Vector3(length * 0.48f, cabinHeight * 0.72f, width * 0.82f);
                Vector3 bedCenter = new Vector3(
                    center.X + length * 0.20f,
                    bodyCenter.Y + bodyHeight / 2f + bedScale.Y / 2f - height * 0.05f,
                    center.Z);
                DrawCube(bedCenter, bedScale, bodyColor * 0.92f);

                // Vitres cabine
                Vector3 frontWindow = new Vector3(
                    cabinCenter.X - cabinScale.X * 0.22f,
                    cabinCenter.Y + cabinScale.Y * 0.10f,
                    cabinCenter.Z);
                DrawCube(frontWindow, new Vector3(cabinScale.X * 0.30f, cabinScale.Y * 0.36f, cabinScale.Z * 0.75f), windowColor);

                Vector3 sideWindowLeft = new Vector3(cabinCenter.X, cabinCenter.Y + cabinScale.Y * 0.08f, cabinCenter.Z - cabinScale.Z * 0.28f);
                Vector3 sideWindowRight = new Vector3(cabinCenter.X, cabinCenter.Y + cabinScale.Y * 0.08f, cabinCenter.Z + cabinScale.Z * 0.28f);
                DrawCube(sideWindowLeft, new Vector3(cabinScale.X * 0.42f, cabinScale.Y * 0.30f, cabinScale.Z * 0.10f), windowColor);
                DrawCube(sideWindowRight, new Vector3(cabinScale.X * 0.42f, cabinScale.Y * 0.30f, cabinScale.Z * 0.10f), windowColor);

                // Porte cabine + séparation benne
                float doorX = cabinCenter.X + cabinScale.X * 0.02f;
                DrawCube(new Vector3(doorX, bodyCenter.Y + bodyHeight * 0.35f, cabinCenter.Z - cabinScale.Z * 0.44f), new Vector3(cabinScale.X * 0.03f, bodyHeight * 0.55f, cabinScale.Z * 0.02f), doorLineColor);
                DrawCube(new Vector3(doorX, bodyCenter.Y + bodyHeight * 0.35f, cabinCenter.Z + cabinScale.Z * 0.44f), new Vector3(cabinScale.X * 0.03f, bodyHeight * 0.55f, cabinScale.Z * 0.02f), doorLineColor);
                DrawCube(new Vector3(cabinCenter.X + cabinScale.X * 0.52f, bedCenter.Y, bedCenter.Z), new Vector3(cabinScale.X * 0.03f, bedScale.Y * 0.92f, bedScale.Z * 0.92f), trimColor * 0.9f);
            }
            else
            {
                // Toit/cabine berline
                Vector3 cabinScale = new Vector3(length * 0.52f, cabinHeight, width * 0.74f);
                Vector3 cabinCenter = new Vector3(
                    center.X - length * 0.02f,
                    bodyCenter.Y + bodyHeight / 2f + cabinHeight / 2f - height * 0.05f,
                    center.Z);
                DrawCube(cabinCenter, cabinScale, bodyColor * 1.04f);

                // Pare-brise et lunette arrière
                DrawCube(
                    new Vector3(cabinCenter.X - cabinScale.X * 0.32f, cabinCenter.Y + cabinScale.Y * 0.04f, cabinCenter.Z),
                    new Vector3(cabinScale.X * 0.26f, cabinScale.Y * 0.45f, cabinScale.Z * 0.74f),
                    windowColor);
                DrawCube(
                    new Vector3(cabinCenter.X + cabinScale.X * 0.32f, cabinCenter.Y + cabinScale.Y * 0.02f, cabinCenter.Z),
                    new Vector3(cabinScale.X * 0.20f, cabinScale.Y * 0.38f, cabinScale.Z * 0.68f),
                    windowColor * 0.95f);

                // Vitres latérales
                float sideZOffset = cabinScale.Z * 0.32f;
                DrawCube(
                    new Vector3(cabinCenter.X, cabinCenter.Y + cabinScale.Y * 0.04f, cabinCenter.Z - sideZOffset),
                    new Vector3(cabinScale.X * 0.74f, cabinScale.Y * 0.34f, cabinScale.Z * 0.10f),
                    windowColor);
                DrawCube(
                    new Vector3(cabinCenter.X, cabinCenter.Y + cabinScale.Y * 0.04f, cabinCenter.Z + sideZOffset),
                    new Vector3(cabinScale.X * 0.74f, cabinScale.Y * 0.34f, cabinScale.Z * 0.10f),
                    windowColor);

                // Traits de portes (2 portes par côté)
                float[] doorBreaks = { -0.12f, 0.16f };
                foreach (float doorBreak in doorBreaks)
                {
                    float doorX = center.X + length * doorBreak;
                    DrawCube(new Vector3(doorX, bodyCenter.Y + bodyHeight * 0.37f, center.Z - width * 0.43f), new Vector3(length * 0.02f, bodyHeight * 0.6f, width * 0.02f), doorLineColor);
                    DrawCube(new Vector3(doorX, bodyCenter.Y + bodyHeight * 0.37f, center.Z + width * 0.43f), new Vector3(length * 0.02f, bodyHeight * 0.6f, width * 0.02f), doorLineColor);
                }
            }

            // Pare-chocs avant/arrière
            float bumperHeight = bodyHeight * 0.18f;
            float bumperLength = length * 0.05f;
            DrawCube(new Vector3(center.X - length * 0.48f, bodyCenter.Y - bodyHeight * 0.28f, center.Z), new Vector3(bumperLength, bumperHeight, width * 0.84f), trimColor);
            DrawCube(new Vector3(center.X + length * 0.48f, bodyCenter.Y - bodyHeight * 0.28f, center.Z), new Vector3(bumperLength, bumperHeight, width * 0.84f), trimColor);

            // Roues + jantes
            float wheelLength = length * 0.16f;
            float wheelWidth = width * 0.15f;
            float frontAxle = center.X - length * 0.28f;
            float rearAxle = center.X + length * (isPickup ? 0.28f : 0.26f);
            float wheelY = bottomY + wheelHeight * 0.52f;
            float leftZ = center.Z - width * 0.41f;
            float rightZ = center.Z + width * 0.41f;

            DrawVehicleWheel(frontAxle, wheelY, leftZ, wheelLength, wheelHeight, wheelWidth, wheelColor, rimColor);
            DrawVehicleWheel(frontAxle, wheelY, rightZ, wheelLength, wheelHeight, wheelWidth, wheelColor, rimColor);
            DrawVehicleWheel(rearAxle, wheelY, leftZ, wheelLength, wheelHeight, wheelWidth, wheelColor, rimColor);
            DrawVehicleWheel(rearAxle, wheelY, rightZ, wheelLength, wheelHeight, wheelWidth, wheelColor, rimColor);
        }

        private void DrawVehicleWheel(float x, float y, float z, float wheelLength, float wheelHeight, float wheelWidth, Color tireColor, Color rimColor)
        {
            DrawCube(new Vector3(x, y, z), new Vector3(wheelLength, wheelHeight, wheelWidth), tireColor);
            DrawCube(new Vector3(x, y, z), new Vector3(wheelLength * 0.45f, wheelHeight * 0.45f, wheelWidth * 1.05f), rimColor);
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
                FurnitureType.TreePine => new Vector2(8f * feetToWorld, 8f * feetToWorld),
                FurnitureType.LootCrate => new Vector2(cellSize * 0.6f, cellSize * 0.6f),
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
                FurnitureType.TreePine => new Color(34, 139, 34),
                FurnitureType.LootCrate => new Color(148, 108, 60),
                FurnitureType.Computer => new Color(50, 50, 60),
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
                    DrawTexturedFloorVolumeTop(
                        new Vector3(x * size + size / 2f, floorHeightOffset, z * size + size / 2f),
                        new Vector3(size * TileFillRatio, 1, size * TileFillRatio),
                        tex,
                        size);
        }

        public void DrawGridCells(IEnumerable<Point> cells, int size, Texture2D tex, float floorHeightOffset = 0f, float opacity = 1f)
        {
            ExecuteWithOpacity(opacity, () =>
            {
                foreach (var cell in cells)
                {
                    DrawTexturedFloorVolumeTop(
                        new Vector3(cell.X * size + size / 2f, floorHeightOffset, cell.Y * size + size / 2f),
                        new Vector3(size * TileFillRatio, 1, size * TileFillRatio),
                        tex,
                        size);
                }
            }, affectTextures: true);
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
        public void DrawWalls(HashSet<WallSegment> walls, int size, bool editorMode = false, float floorHeightOffset = 0f, Color? wallOverrideColor = null, Texture2D brickWallTexture = null, Texture2D hescoWallTexture = null, float wallOpacity = 1f)
        {
            float clampedOpacity = MathHelper.Clamp(wallOpacity, 0f, 1f);
            BlendState previousBlend = gd.BlendState;

            if (clampedOpacity < 0.999f)
                gd.BlendState = BlendState.AlphaBlend;

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
                wallColor *= clampedOpacity;

                Texture2D wallTexture = null;
                if (!editorMode)
                {
                    wallTexture = s.Material switch
                    {
                        WallMaterial.Brick => null,
                        WallMaterial.Hesco => hescoWallTexture,
                        _ => null
                    };
                }

                // ------------------- NOUVEAU : GESTION DES FENÊTRES -------------------
                if (s.Type == WallType.Window || s.Type == WallType.ShatteredWindow)
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

                    if (s.Type == WallType.Window)
                    {
                        // 3. VITRE TRANSPARENTE AU CENTRE
                        float glassHeight = wallHeight - bottomHeight - topPartHeight;
                        Vector3 glassCenter = center;
                        glassCenter.Y = floorHeightOffset + bottomHeight + (glassHeight / 2f);
                        float glassThickness = Math.Max(0.03f, thickness * 0.35f);
                        Vector3 glassScale = s.IsHorizontal
                            ? new Vector3(size * 0.94f, glassHeight * 0.96f, glassThickness)
                            : new Vector3(glassThickness, glassHeight * 0.96f, size * 0.94f);

                        DrawCube(glassCenter, glassScale, new Color(175, 225, 255, 95) * clampedOpacity);
                        DrawCube(glassCenter, glassScale * new Vector3(1.02f, 0.08f, 1.02f), new Color(235, 245, 255, 65) * clampedOpacity);
                    }
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

            if (clampedOpacity < 0.999f)
                gd.BlendState = previousBlend;
        }


        public void DrawRampTiles(IEnumerable<RampTileData> ramps, int floorToRender, int cellSize, float opacity = 1f)
        {
            if (ramps == null)
                return;

            float floorYOffset = WorldMetrics.FloorToWorldY(floorToRender, cellSize);
            ExecuteWithOpacity(opacity, () =>
            {
                foreach (var ramp in ramps)
                {
                    if (ramp.Floor != floorToRender)
                        continue;

                    int dx = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDx : 0;
                    int dy = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDy : -1;
                    DrawDirectionalRamp(ramp.X, ramp.Y, dx, dy, floorYOffset, cellSize);
                }
            });
        }

        /// <summary>
        /// Draws visible upward-arrow markers above the base cell of each ramp so that the
        /// player can identify stair entry points and knows to click them to change floor.
        /// </summary>
        public void DrawRampMarkers(IEnumerable<RampTileData> ramps, int floorToRender, int cellSize, float gameTime, float opacity = 1f)
        {
            if (ramps == null)
                return;

            float floorYOffset = WorldMetrics.FloorToWorldY(floorToRender, cellSize);
            float pulse = (float)Math.Sin(gameTime * 2.5f) * 0.2f + 0.8f;

            bool prevLighting = basic.LightingEnabled;
            basic.LightingEnabled = false;
            try
            {
                foreach (var ramp in ramps)
                {
                    if (ramp.Floor != floorToRender)
                        continue;

                    float baseCX = ramp.X * cellSize + cellSize * 0.5f;
                    float baseCZ = ramp.Y * cellSize + cellSize * 0.5f;

                    Color markerColor = new Color(255, 220, 60, 220) * pulse * opacity;
                    Color arrowColor  = new Color(255, 220, 60, 240) * opacity;

                    // Flat square at the floor entry of the ramp (the clickable cell).
                    DrawCube(
                        new Vector3(baseCX, floorYOffset + cellSize * 0.03f, baseCZ),
                        new Vector3(cellSize * 0.75f, cellSize * 0.04f, cellSize * 0.75f),
                        markerColor);

                    // Thin vertical pole rising from the marker.
                    float poleBase = floorYOffset + cellSize * 0.5f;
                    float poleH    = cellSize * 0.55f;
                    DrawCube(
                        new Vector3(baseCX, poleBase + poleH * 0.5f, baseCZ),
                        new Vector3(cellSize * 0.06f, poleH, cellSize * 0.06f),
                        arrowColor);

                    // Arrowhead — three stacked tiers that taper toward the top.
                    float tip = poleBase + poleH;
                    DrawCube(new Vector3(baseCX, tip + cellSize * 0.04f, baseCZ), new Vector3(cellSize * 0.32f, cellSize * 0.06f, cellSize * 0.32f), arrowColor);
                    DrawCube(new Vector3(baseCX, tip + cellSize * 0.11f, baseCZ), new Vector3(cellSize * 0.20f, cellSize * 0.06f, cellSize * 0.20f), arrowColor);
                    DrawCube(new Vector3(baseCX, tip + cellSize * 0.18f, baseCZ), new Vector3(cellSize * 0.09f, cellSize * 0.06f, cellSize * 0.09f), arrowColor);
                }
            }
            finally
            {
                basic.LightingEnabled = prevLighting;
            }
        }

        private void DrawDirectionalRamp(int cellX, int cellY, int ascendDx, int ascendDy, float floorYOffset, int cellSize)
        {
            // Règle métier: 1 étage = 2 cases de hauteur.
            // La rampe visuelle doit donc s'étendre sur 2 cases en longueur
            // (de la case de départ à la case adjacente dans la direction montante).
            const float rampLengthInCells = 2f;
            const int slices = 12;
            float rampLength = cellSize * rampLengthInCells;
            float sliceDepth = rampLength / slices;
            float baseCenterX = cellX * cellSize + cellSize / 2f + ascendDx * (cellSize * 0.5f);
            float baseCenterZ = cellY * cellSize + cellSize / 2f + ascendDy * (cellSize * 0.5f);

            for (int i = 0; i < slices; i++)
            {
                float t = (i + 1f) / slices;
                float sliceHeight = t * (cellSize * WorldMetrics.FloorHeightInCells);
                float offsetAlongAxis = i * sliceDepth;

                float centerX = baseCenterX;
                float centerZ = baseCenterZ;

                if (ascendDx != 0)
                {
                    centerX += -ascendDx * (offsetAlongAxis - (rampLength * 0.5f - sliceDepth * 0.5f));
                }
                else
                {
                    centerZ += -ascendDy * (offsetAlongAxis - (rampLength * 0.5f - sliceDepth * 0.5f));
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
        public void DrawCoverIndicators(CoverSystem coverSystem, int gridWidth, int gridHeight, int cellSize, float gameTime, Func<Point, int, bool> isExplored)
        {
            if (coverSystem == null)
                return;

            float pulse = (float)Math.Sin(gameTime * 2f) * 0.3f + 0.7f;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Point cell = new Point(x, y);

                    if (isExplored != null && !isExplored(cell, 0))
                        continue;

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
        public void DrawMovementZones(PathfindingSystem.MovementZones zones, int cellSize, float gameTime, int viewedFloor, IReadOnlyDictionary<Point, float> terrainHeights = null, IReadOnlyList<BuildingFootprintData> buildings = null, Vector3? cameraPosition = null, Func<Point, int, bool> isExplored = null)
        {
            if (zones == null) return;

            float pulse = (float)Math.Sin(gameTime * 3f) * 0.15f + 0.85f;

            // Use lazily-cached per-floor dictionaries (built once per zone change, not every frame)
            var shortByFloor = zones.GetShortByFloor();
            var maxByFloor = zones.GetMaxByFloor();
            var sprintByFloor = zones.GetSprintByFloor();

            // Collect all floors present across the three zones
            var floors = new HashSet<int>(shortByFloor.Keys);
            floors.UnionWith(maxByFloor.Keys);
            floors.UnionWith(sprintByFloor.Keys);


            bool prevLighting = basic.LightingEnabled;
            basic.LightingEnabled = false;
            try
            {
                foreach (int floor in floors)
                {
                    float floorYOffset = WorldMetrics.FloorToWorldY(floor, cellSize);

                    bool needBuildingFilter = floor == 0 && viewedFloor != 0 && buildings != null && buildings.Count > 0;

                    HashSet<Point> shortZone, maxZone, sprintZone;
                    if (needBuildingFilter || isExplored != null)
                    {
                        // Clone cached sets before mutating
                        shortZone = shortByFloor.TryGetValue(floor, out var s) ? new HashSet<Point>(s) : new HashSet<Point>();
                        maxZone = maxByFloor.TryGetValue(floor, out var m) ? new HashSet<Point>(m) : new HashSet<Point>();
                        sprintZone = sprintByFloor.TryGetValue(floor, out var sp) ? new HashSet<Point>(sp) : new HashSet<Point>();

                        if (needBuildingFilter)
                        {
                            shortZone.RemoveWhere(cell => IsInsideBuildingFootprint(cell, buildings));
                            maxZone.RemoveWhere(cell => IsInsideBuildingFootprint(cell, buildings));
                            sprintZone.RemoveWhere(cell => IsInsideBuildingFootprint(cell, buildings));
                        }

                        if (isExplored != null)
                        {
                            shortZone.RemoveWhere(cell => !isExplored(cell, floor));
                            maxZone.RemoveWhere(cell => !isExplored(cell, floor));
                            sprintZone.RemoveWhere(cell => !isExplored(cell, floor));
                        }
                    }
                    else
                    {
                        // No mutation needed — reuse the cached sets directly
                        shortZone = shortByFloor.TryGetValue(floor, out var s) ? s : new HashSet<Point>();
                        maxZone = maxByFloor.TryGetValue(floor, out var m) ? m : new HashSet<Point>();
                        sprintZone = sprintByFloor.TryGetValue(floor, out var sp) ? sp : new HashSet<Point>();
                    }

                    // Quand on est à un étage supérieur, estomper les segments du périmètre
                    // RDC qui sont cachés derrière un bâtiment du point de vue de la caméra.
                    Vector2? camXZ = null;
                    IReadOnlyList<BuildingFootprintData> occlusionBuildings = null;
                    if (floor == 0 && viewedFloor != 0 && cameraPosition.HasValue && buildings != null && buildings.Count > 0)
                    {
                        camXZ = new Vector2(cameraPosition.Value.X, cameraPosition.Value.Z);
                        occlusionBuildings = buildings;
                    }

                    // Zone 1 : contour externe du mouvement court (1 AP) - VERT
                    DrawZonePerimeter(shortZone, cellSize, floorYOffset, 0.02f, new Color(0, 255, 0, 255) * pulse, terrainHeights, camXZ, occlusionBuildings);

                    // Zone 2 : contour externe du mouvement max (2 AP) - BLEU
                    DrawZonePerimeter(maxZone, cellSize, floorYOffset, 0.03f, new Color(0, 150, 255, 255) * pulse, terrainHeights, camXZ, occlusionBuildings);

                    // Zone 3 : contour externe du sprint (2 AP + phosphocréatine) - JAUNE
                    float sprintPulse = (float)Math.Sin(gameTime * 5f) * 0.2f + 0.8f;
                    DrawZonePerimeter(sprintZone, cellSize, floorYOffset, 0.04f, new Color(255, 200, 0, 255) * sprintPulse, terrainHeights, camXZ, occlusionBuildings);

                    // Indicateur sprint uniquement sur les cellules de frontière
                    foreach (var cell in sprintZone)
                    {
                        if (IsBoundaryCell(cell, sprintZone))
                        {
                            DrawSprintIndicator(cell, cellSize, gameTime, floorYOffset, terrainHeights);
                        }
                    }
                }
            }
            finally
            {
                basic.LightingEnabled = prevLighting;
            }
        }

        private static readonly Point[] CardinalDirections =
        {
            new Point(0, -1),
            new Point(1, 0),
            new Point(0, 1),
            new Point(-1, 0)
        };

        private static bool IsInsideBuildingFootprint(Point cell, IReadOnlyList<BuildingFootprintData> buildings)
        {
            foreach (var b in buildings)
            {
                if (cell.X >= b.X && cell.X < b.X + b.Width &&
                    cell.Y >= b.Y && cell.Y < b.Y + b.Height)
                    return true;
            }
            return false;
        }

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

        private void DrawZonePerimeter(HashSet<Point> zone, int cellSize, float floorYOffset, float lift, Color color, IReadOnlyDictionary<Point, float> terrainHeights, Vector2? cameraXZ = null, IReadOnlyList<BuildingFootprintData> occlusionBuildings = null)
        {
            if (zone == null || zone.Count == 0) return;

            bool checkOcclusion = cameraXZ.HasValue && occlusionBuildings != null && occlusionBuildings.Count > 0;

            foreach (Point cell in zone)
            {
                float xMin = cell.X * cellSize;
                float xMax = (cell.X + 1) * cellSize;
                float zMin = cell.Y * cellSize;
                float zMax = (cell.Y + 1) * cellSize;

                float yNW = floorYOffset + ComputeCornerHeight(terrainHeights, cell.X, cell.Y) + lift;
                float ySW = floorYOffset + ComputeCornerHeight(terrainHeights, cell.X, cell.Y + 1) + lift;
                float ySE = floorYOffset + ComputeCornerHeight(terrainHeights, cell.X + 1, cell.Y + 1) + lift;
                float yNE = floorYOffset + ComputeCornerHeight(terrainHeights, cell.X + 1, cell.Y) + lift;

                float lw = cellSize * 0.12f;
                float lh = 0.04f;

                if (!zone.Contains(new Point(cell.X, cell.Y - 1)))
                {
                    Vector3 mid = new Vector3((xMin + xMax) / 2f, (yNW + yNE) / 2f, zMin);
                    Color c = checkOcclusion && IsOccludedByBuilding(cameraXZ.Value, new Vector2(mid.X, mid.Z), occlusionBuildings, cellSize) ? color * 0.15f : color;
                    DrawCube(mid, new Vector3(cellSize, lh, lw), c);
                }

                if (!zone.Contains(new Point(cell.X + 1, cell.Y)))
                {
                    Vector3 mid = new Vector3(xMax, (yNE + ySE) / 2f, (zMin + zMax) / 2f);
                    Color c = checkOcclusion && IsOccludedByBuilding(cameraXZ.Value, new Vector2(mid.X, mid.Z), occlusionBuildings, cellSize) ? color * 0.15f : color;
                    DrawCube(mid, new Vector3(lw, lh, cellSize), c);
                }

                if (!zone.Contains(new Point(cell.X, cell.Y + 1)))
                {
                    Vector3 mid = new Vector3((xMin + xMax) / 2f, (ySW + ySE) / 2f, zMax);
                    Color c = checkOcclusion && IsOccludedByBuilding(cameraXZ.Value, new Vector2(mid.X, mid.Z), occlusionBuildings, cellSize) ? color * 0.15f : color;
                    DrawCube(mid, new Vector3(cellSize, lh, lw), c);
                }

                if (!zone.Contains(new Point(cell.X - 1, cell.Y)))
                {
                    Vector3 mid = new Vector3(xMin, (yNW + ySW) / 2f, (zMin + zMax) / 2f);
                    Color c = checkOcclusion && IsOccludedByBuilding(cameraXZ.Value, new Vector2(mid.X, mid.Z), occlusionBuildings, cellSize) ? color * 0.15f : color;
                    DrawCube(mid, new Vector3(lw, lh, cellSize), c);
                }
            }
        }

        private static bool IsOccludedByBuilding(Vector2 cameraXZ, Vector2 segmentXZ, IReadOnlyList<BuildingFootprintData> buildings, int cellSize)
        {
            foreach (var b in buildings)
            {
                float x0 = b.X * cellSize;
                float z0 = b.Y * cellSize;
                float x1 = (b.X + b.Width) * cellSize;
                float z1 = (b.Y + b.Height) * cellSize;

                if (LineIntersectsRect2D(cameraXZ, segmentXZ, x0, z0, x1, z1))
                    return true;
            }
            return false;
        }

        private static bool LineIntersectsRect2D(Vector2 p1, Vector2 p2, float x0, float z0, float x1, float z1)
        {
            Vector2 a = new Vector2(x0, z0);
            Vector2 b = new Vector2(x1, z0);
            Vector2 c = new Vector2(x1, z1);
            Vector2 d = new Vector2(x0, z1);

            return TrySegmentIntersect2D(p1, p2, a, b)
                || TrySegmentIntersect2D(p1, p2, b, c)
                || TrySegmentIntersect2D(p1, p2, c, d)
                || TrySegmentIntersect2D(p1, p2, d, a);
        }

        private static bool TrySegmentIntersect2D(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            const float epsilon = 0.0001f;
            Vector2 r = p2 - p1;
            Vector2 s = q2 - q1;
            float denom = r.X * s.Y - r.Y * s.X;

            if (Math.Abs(denom) <= epsilon)
                return false;

            Vector2 delta = q1 - p1;
            float t = (delta.X * s.Y - delta.Y * s.X) / denom;
            float u = (delta.X * r.Y - delta.Y * r.X) / denom;

            return t >= -epsilon && t <= 1f + epsilon && u >= -epsilon && u <= 1f + epsilon;
        }

        private void DrawZoneFill(IEnumerable<Point> cells, int cellSize, float floorYOffset, Color color, IReadOnlyDictionary<Point, float> terrainHeights)
        {
            if (cells == null) return;

            float fillSize = cellSize * 0.94f;
            foreach (Point cell in cells)
            {
                float terrainH = GetCellTerrainHeight(terrainHeights, cell);
                Vector3 pos = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    floorYOffset + terrainH + 0.01f,
                    cell.Y * cellSize + cellSize / 2f
                );
                DrawPlane(pos, new Vector3(fillSize, 1f, fillSize), color);
            }
        }

        public void DrawZoneOutline(IEnumerable<Point> cells, int cellSize, float height, Color color)
        {
            if (cells == null)
                return;

            HashSet<Point> zone = cells.ToHashSet();
            if (zone.Count == 0)
                return;

            DrawZonePerimeter(zone, cellSize, height, 0f, color, null);
        }

        /// <summary>
        /// Dessine un indicateur de sprint (petit symbole au centre de la case)
        /// </summary>
        private void DrawSprintIndicator(Point cell, int cellSize, float gameTime, float floorYOffset, IReadOnlyDictionary<Point, float> terrainHeights)
        {
            float pulse = (float)Math.Sin(gameTime * 6f) * 0.3f + 0.7f;
            float terrainOffset = GetCellTerrainHeight(terrainHeights, cell);

            Vector3 pos = new Vector3(
                cell.X * cellSize + cellSize / 2f,
                floorYOffset + terrainOffset + 0.15f,
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
        public void DrawMovementPath(List<GridNode> path, Unit unit, int cellSize, float gameTime, IReadOnlyDictionary<Point, float> terrainHeights = null, Func<Point, int, bool> isExplored = null)
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

                    if (isExplored != null && !isExplored(cell, node.Floor))
                        continue;
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
                        WorldMetrics.FloorToWorldY(node.Floor, cellSize) + GetCellTerrainHeight(terrainHeights, cell) + 0.09f,
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
                            WorldMetrics.FloorToWorldY(nextNode.Floor, cellSize) + GetCellTerrainHeight(terrainHeights, nextCell) + 0.09f,
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

        private static float GetCellTerrainHeight(IReadOnlyDictionary<Point, float> terrainHeights, Point cell)
            => terrainHeights != null && terrainHeights.TryGetValue(cell, out float height) ? height : 0f;

        public void DrawObjectives(IEnumerable<ObjectivePoint> objectives, int cellSize, int viewedFloor)
        {
            if (objectives == null) return;

            float pulse = (float)Math.Sin(globalAnimationTime * 5f) * 0.25f + 0.75f;

            foreach (var obj in objectives)
            {
                if (obj.Floor != viewedFloor) continue;

                Vector3 pos = new Vector3(
                    obj.X * cellSize + cellSize / 2f,
                    WorldMetrics.FloorToWorldY(obj.Floor, cellSize) + cellSize * 1.8f,
                    obj.Y * cellSize + cellSize / 2f
                );

                // Marqueur d'objectif : un losange flottant qui tourne
                Matrix rotation = Matrix.CreateRotationY(globalAnimationTime * 3f) * Matrix.CreateRotationZ(MathHelper.PiOver4);
                DrawCube(pos, new Vector3(cellSize * 0.5f), Color.Gold * pulse, rotation);

                // Un socle au sol
                Vector3 basePos = new Vector3(pos.X, WorldMetrics.FloorToWorldY(obj.Floor, cellSize) + 0.05f, pos.Z);
                DrawPlane(basePos, new Vector3(cellSize * 0.8f, 1f, cellSize * 0.8f), Color.Gold * 0.5f * pulse);
            }
        }

        public void DrawFogMesh(int w, int h, int size, float yOffset, bool[,] vis, bool[,] exp, IReadOnlyDictionary<Point, float> terrainHeights)
        {
            if (vis == null || exp == null) return;

            // Nous avons besoin de (w+1) * (h+1) sommets
            VertexPositionColor[] verts = new VertexPositionColor[(w + 1) * (h + 1)];
            for (int z = 0; z <= h; z++)
            {
                for (int x = 0; x <= w; x++)
                {
                    float alpha = GetFogAlpha(x, z, w, h, vis, exp);
                    float terrainH = terrainHeights != null ? ComputeCornerHeight(terrainHeights, x, z) : 0f;
                    verts[z * (w + 1) + x] = new VertexPositionColor(
                        new Vector3(x * size, yOffset + terrainH + 0.15f, z * size),
                        new Color(0, 0, 0, alpha)
                    );
                }
            }

            // Indices pour les triangles
            short[] indices = new short[w * h * 6];
            int k = 0;
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    int v = z * (w + 1) + x;
                    indices[k++] = (short)v;
                    indices[k++] = (short)(v + w + 1);
                    indices[k++] = (short)(v + 1);
                    indices[k++] = (short)(v + 1);
                    indices[k++] = (short)(v + w + 1);
                    indices[k++] = (short)(v + w + 2);
                }
            }

            basic.World = Matrix.Identity;
            bool oldLighting = basic.LightingEnabled;
            basic.LightingEnabled = false; // Le brouillard n'est pas éclairé

            BlendState oldBS = gd.BlendState;
            gd.BlendState = BlendState.AlphaBlend;

            foreach (var pass in basic.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length, indices, 0, indices.Length / 3);
            }

            gd.BlendState = oldBS;
            basic.LightingEnabled = oldLighting;
        }

        private float GetFogAlpha(int x, int z, int w, int h, bool[,] vis, bool[,] exp)
        {
            // Moyenne de jusqu'à 4 cellules adjacentes pour lisser
            float sum = 0;
            int count = 0;
            for (int dz = -1; dz <= 0; dz++)
            {
                for (int dx = -1; dx <= 0; dx++)
                {
                    int cx = x + dx;
                    int cz = z + dz;
                    if (cx >= 0 && cx < w && cz >= 0 && cz < h)
                    {
                        if (vis[cx, cz]) sum += 0.0f;
                        else if (exp[cx, cz]) sum += 0.65f;
                        else sum += 1.0f;
                        count++;
                    }
                }
            }
            return count > 0 ? sum / count : 1.0f;
        }
    }
}
