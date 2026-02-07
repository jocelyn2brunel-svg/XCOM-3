using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Gère tout le rendu 3D : grille, murs, unités, effets
    /// </summary>
    public class Renderer3D
    {
        // ═══════════════════════════════════════════════════════════════════════
        // EFFETS ET PRIMITIVES
        // ═══════════════════════════════════════════════════════════════════════

        private GraphicsDevice graphicsDevice;
        private BasicEffect basicEffect;
        private BasicEffect texturedEffect;

        // Primitives géométriques
        private VertexPositionColor[] cubeVertices;
        private short[] cubeIndices;
        private VertexPositionColor[] planeVertices;
        private short[] planeIndices;
        private VertexPositionNormalTexture[] planeTexturedVertices;
        private short[] planeTexturedIndices;

        // Modèle humanoïde
        private HumanoidModelAdvanced humanoidModel;

        // ═══════════════════════════════════════════════════════════════════════
        // CONSTRUCTEUR
        // ═══════════════════════════════════════════════════════════════════════

        public Renderer3D(GraphicsDevice device)
        {
            graphicsDevice = device;

            InitializeEffects();
            CreatePrimitives();

            humanoidModel = new HumanoidModelAdvanced();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INITIALISATION
        // ═══════════════════════════════════════════════════════════════════════

        private void InitializeEffects()
        {
            basicEffect = new BasicEffect(graphicsDevice);
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = true;
            basicEffect.EnableDefaultLighting();

            texturedEffect = new BasicEffect(graphicsDevice);
            texturedEffect.TextureEnabled = true;
            texturedEffect.LightingEnabled = true;
            texturedEffect.EnableDefaultLighting();
        }

        private void CreatePrimitives()
        {
            CreateCubePrimitive();
            CreatePlanePrimitive();
            CreateTexturedPlanePrimitive();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONFIGURATION DES MATRICES
        // ═══════════════════════════════════════════════════════════════════════

        public void SetMatrices(Matrix view, Matrix projection)
        {
            basicEffect.View = view;
            basicEffect.Projection = projection;

            texturedEffect.View = view;
            texturedEffect.Projection = projection;
        }

        public void SetLighting(Color ambient, Color directional)
        {
            basicEffect.AmbientLightColor = ambient.ToVector3();
            basicEffect.DirectionalLight0.DiffuseColor = directional.ToVector3();

            texturedEffect.AmbientLightColor = ambient.ToVector3();
            texturedEffect.DirectionalLight0.DiffuseColor = directional.ToVector3();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PRIMITIVES 3D
        // ═══════════════════════════════════════════════════════════════════════

        private void CreateCubePrimitive()
        {
            cubeVertices = new VertexPositionColor[8];

            cubeVertices[0] = new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), Color.White);
            cubeVertices[1] = new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.5f), Color.White);
            cubeVertices[2] = new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.5f), Color.White);
            cubeVertices[3] = new VertexPositionColor(new Vector3(0.5f, -0.5f, -0.5f), Color.White);
            cubeVertices[4] = new VertexPositionColor(new Vector3(-0.5f, 0.5f, -0.5f), Color.White);
            cubeVertices[5] = new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.5f), Color.White);
            cubeVertices[6] = new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.5f), Color.White);
            cubeVertices[7] = new VertexPositionColor(new Vector3(0.5f, 0.5f, -0.5f), Color.White);

            cubeIndices = new short[]
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1,
                3, 2, 6, 3, 6, 7,
                1, 5, 6, 1, 6, 2,
                0, 3, 7, 0, 7, 4
            };
        }

        private void CreatePlanePrimitive()
        {
            planeVertices = new VertexPositionColor[4];
            planeVertices[0] = new VertexPositionColor(new Vector3(-0.5f, 0, -0.5f), Color.White);
            planeVertices[1] = new VertexPositionColor(new Vector3(-0.5f, 0, 0.5f), Color.White);
            planeVertices[2] = new VertexPositionColor(new Vector3(0.5f, 0, 0.5f), Color.White);
            planeVertices[3] = new VertexPositionColor(new Vector3(0.5f, 0, -0.5f), Color.White);

            planeIndices = new short[] { 0, 1, 2, 0, 2, 3 };
        }

        private void CreateTexturedPlanePrimitive()
        {
            planeTexturedVertices = new VertexPositionNormalTexture[4];

            Vector3 normal = Vector3.Up;

            planeTexturedVertices[0] = new VertexPositionNormalTexture(
                new Vector3(-0.5f, 0, -0.5f), normal, new Vector2(0, 0));
            planeTexturedVertices[1] = new VertexPositionNormalTexture(
                new Vector3(-0.5f, 0, 0.5f), normal, new Vector2(0, 1));
            planeTexturedVertices[2] = new VertexPositionNormalTexture(
                new Vector3(0.5f, 0, 0.5f), normal, new Vector2(1, 1));
            planeTexturedVertices[3] = new VertexPositionNormalTexture(
                new Vector3(0.5f, 0, -0.5f), normal, new Vector2(1, 0));

            planeTexturedIndices = new short[] { 0, 1, 2, 0, 2, 3 };
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FONCTIONS DE DESSIN PRIMITIVES
        // ═══════════════════════════════════════════════════════════════════════

        public void DrawCube(Vector3 position, Vector3 scale, Color color)
        {
            VertexPositionColor[] coloredVertices = new VertexPositionColor[8];
            for (int i = 0; i < 8; i++)
            {
                coloredVertices[i] = new VertexPositionColor(cubeVertices[i].Position, color);
            }

            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
            basicEffect.World = world;

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    coloredVertices,
                    0,
                    8,
                    cubeIndices,
                    0,
                    12
                );
            }
        }

        public void DrawPlane(Vector3 position, Vector3 scale, Color color)
        {
            VertexPositionColor[] coloredVertices = new VertexPositionColor[4];
            for (int i = 0; i < 4; i++)
            {
                coloredVertices[i] = new VertexPositionColor(planeVertices[i].Position, color);
            }

            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
            basicEffect.World = world;

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    coloredVertices,
                    0,
                    4,
                    planeIndices,
                    0,
                    2
                );
            }
        }

        public void DrawTexturedPlane(Vector3 position, Vector3 scale, Texture2D texture)
        {
            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
            texturedEffect.World = world;
            texturedEffect.Texture = texture;

            foreach (EffectPass pass in texturedEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    planeTexturedVertices,
                    0,
                    4,
                    planeTexturedIndices,
                    0,
                    2
                );
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RENDU DE SCÈNE
        // ═══════════════════════════════════════════════════════════════════════

        public void DrawGrid(int gridWidth, int gridHeight, int cellSize, Texture2D tileTexture)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    Vector3 position = new Vector3(x * cellSize + cellSize / 2f, 0, z * cellSize + cellSize / 2f);
                    DrawTexturedPlane(position, new Vector3(cellSize * 0.95f, 1, cellSize * 0.95f), tileTexture);
                }
            }
        }

        public void DrawWalls(HashSet<WallSegment> wallSegments, int cellSize)
        {
            foreach (var segment in wallSegments)
            {
                Vector3 start3D, end3D;

                if (segment.IsHorizontal)
                {
                    start3D = new Vector3(
                        segment.Start.X * cellSize,
                        cellSize * 0.75f,
                        segment.Start.Y * cellSize
                    );
                    end3D = new Vector3(
                        segment.End.X * cellSize,
                        cellSize * 0.75f,
                        segment.End.Y * cellSize
                    );
                }
                else
                {
                    start3D = new Vector3(
                        segment.Start.X * cellSize,
                        cellSize * 0.75f,
                        segment.Start.Y * cellSize
                    );
                    end3D = new Vector3(
                        segment.End.X * cellSize,
                        cellSize * 0.75f,
                        segment.End.Y * cellSize
                    );
                }

                Vector3 center = (start3D + end3D) / 2f;

                Vector3 scale;
                if (segment.IsHorizontal)
                {
                    scale = new Vector3(cellSize, cellSize * 1.5f, cellSize * 0.1f);
                }
                else
                {
                    scale = new Vector3(cellSize * 0.1f, cellSize * 1.5f, cellSize);
                }

                DrawCube(center, scale, new Color(120, 120, 120));
            }
        }

        public void DrawUnit(Unit unit, int cellSize)
        {
            Vector3 basePosition = new Vector3(
                unit.Cell.X * cellSize + cellSize / 2f,
                0,
                unit.Cell.Y * cellSize + cellSize / 2f
            );

            Vector3 visualOffset = Vector3.Zero;

            // Animation de tir
            if (unit.IsFiring && unit.FireTarget.HasValue)
            {
                Vector3 targetPos = new Vector3(
                    unit.FireTarget.Value.X * cellSize + cellSize / 2f,
                    cellSize * 0.75f,
                    unit.FireTarget.Value.Y * cellSize + cellSize / 2f
                );

                if (unit.Weapon == "Zombie Claws")
                {
                    Vector3 chargeVector = targetPos - basePosition;
                    float t = unit.FireProgress;

                    if (t < 0.5f)
                    {
                        float forwardT = t / 0.5f;
                        visualOffset = Vector3.Lerp(Vector3.Zero, chargeVector, forwardT);
                    }
                    else
                    {
                        float returnT = (t - 0.5f) / 0.5f;
                        visualOffset = Vector3.Lerp(chargeVector, Vector3.Zero, returnT);
                    }
                }
                else
                {
                    Vector3 projectilePos = Vector3.Lerp(basePosition, targetPos, unit.FireProgress);
                    DrawCube(projectilePos, new Vector3(cellSize * 0.2f), Color.Yellow);
                }
            }

            Vector3 finalPos = basePosition + visualOffset;
            Color unitColor = unit.Team == Team.Player ? Color.Blue : Color.Red;

            // Déterminer le type d'unité
            HumanoidModelAdvanced.UnitType unitType = HumanoidModelAdvanced.UnitType.Soldier;

            if (unit.Class == "Assault" || unit.Class == "Infantry")
                unitType = HumanoidModelAdvanced.UnitType.Soldier;
            else if (unit.Class == "Heavy")
                unitType = HumanoidModelAdvanced.UnitType.Heavy;
            else if (unit.Class == "Scout")
                unitType = HumanoidModelAdvanced.UnitType.Scout;
            else if (unit.Class == "Undead")
                unitType = HumanoidModelAdvanced.UnitType.Zombie;
            else if (unit.Team == Team.Enemy && unit.Name.Contains("Alien"))
                unitType = HumanoidModelAdvanced.UnitType.Alien;

            humanoidModel.Draw(graphicsDevice, basicEffect, finalPos, unitColor,
                              cellSize * 0.8f, unitType, unit.Orientation,
                              unit.LegSwing, unit.ArmSwing, unit.BodyBob, unit.IdleBobOffset);
        }

        public void DrawSelectionIndicator(Unit unit, int cellSize, Color color, float scale = 1.1f)
        {
            Vector3 position = new Vector3(
                unit.Cell.X * cellSize + cellSize / 2f,
                0.05f,
                unit.Cell.Y * cellSize + cellSize / 2f
            );

            DrawPlane(position, new Vector3(cellSize * scale, 1, cellSize * scale), color);
        }

        public void DrawCraters(List<Crater> craters, int cellSize)
        {
            foreach (var crater in craters)
            {
                Vector3 position = new Vector3(
                    crater.Cell.X * cellSize + cellSize / 2f,
                    -crater.Depth * 0.2f,
                    crater.Cell.Y * cellSize + cellSize / 2f
                );

                Color craterColor = new Color(60, 50, 40) * (0.5f + crater.Depth * 0.15f);

                DrawPlane(position,
                         new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f),
                         craterColor);
            }
        }

        public void DrawGrenades(List<Grenade> grenades, int cellSize)
        {
            foreach (var grenade in grenades)
            {
                Color grenadeColor = GrenadeDatabase.GetGrenadeColor(grenade.Data.Type);
                DrawCube(grenade.Position, new Vector3(cellSize * 0.2f), grenadeColor);
            }
        }
    }
}
