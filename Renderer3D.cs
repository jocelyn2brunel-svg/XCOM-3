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

        public void DrawWalls(HashSet<WallSegment> walls, int size)
        {
            foreach (var s in walls)
            {
                Vector3 start = new(s.Start.X * size, size * 0.75f, s.Start.Y * size);
                Vector3 end = new(s.End.X * size, size * 0.75f, s.End.Y * size);
                Vector3 center = (start + end) / 2f;
                Vector3 scale = s.IsHorizontal ? new Vector3(size, size * 1.5f, size * 0.1f)
                                               : new Vector3(size * 0.1f, size * 1.5f, size);
                DrawCube(center, scale, new Color(120, 120, 120));
            }
        }

        public void DrawUnit(Unit u, int size)
        {
            Vector3 pos = new Vector3(u.Cell.X * size + size / 2f, 0, u.Cell.Y * size + size / 2f);
            Vector3 offset = Vector3.Zero;

            if (u.IsFiring && u.FireTarget.HasValue)
            {
                Vector3 target = new(u.FireTarget.Value.X * size + size / 2f, size * 0.75f, u.FireTarget.Value.Y * size + size / 2f);
                if (u.Weapon == "Zombie Claws")
                {
                    float t = u.FireProgress;
                    Vector3 delta = target - pos;
                    offset = t < 0.5f ? Vector3.Lerp(Vector3.Zero, delta, t / 0.5f)
                                 : Vector3.Lerp(delta, Vector3.Zero, (t - 0.5f) / 0.5f);
                }
                else DrawCube(Vector3.Lerp(pos, target, u.FireProgress), new Vector3(size * 0.2f), Color.Yellow);
            }

            Color col = u.Team == Team.Player ? Color.Blue : Color.Red;
            var type = u.Class switch
            {
                "Heavy" => HumanoidModelAdvanced.UnitType.Heavy,
                "Scout" => HumanoidModelAdvanced.UnitType.Scout,
                "Undead" => HumanoidModelAdvanced.UnitType.Zombie,
                "Assault" or "Infantry" => HumanoidModelAdvanced.UnitType.Soldier,
                _ => u.Team == Team.Enemy && u.Name.Contains("Alien") ? HumanoidModelAdvanced.UnitType.Alien
                     : HumanoidModelAdvanced.UnitType.Soldier
            };
            humanoidModel.Draw(gd, basic, pos + offset, col, size * 0.8f, type, u.Orientation, u.LegSwing, u.ArmSwing, u.BodyBob, u.IdleBobOffset);
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
    }
}
