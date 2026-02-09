using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace XCOM_3
{
    public class HumanoidModelAdvanced
    {
        private VertexPositionColor[] cubeVertices;
        private short[] cubeIndices;

        public enum UnitType { Soldier, Alien, Zombie, Heavy, Scout }

        public HumanoidModelAdvanced() => InitializeCube();

        private void InitializeCube()
        {
            cubeVertices = new VertexPositionColor[8]
            {
                new VertexPositionColor(new Vector3(-0.5f,-0.5f,-0.5f), Color.White),
                new VertexPositionColor(new Vector3(-0.5f,-0.5f,0.5f), Color.White),
                new VertexPositionColor(new Vector3(0.5f,-0.5f,0.5f), Color.White),
                new VertexPositionColor(new Vector3(0.5f,-0.5f,-0.5f), Color.White),
                new VertexPositionColor(new Vector3(-0.5f,0.5f,-0.5f), Color.White),
                new VertexPositionColor(new Vector3(-0.5f,0.5f,0.5f), Color.White),
                new VertexPositionColor(new Vector3(0.5f,0.5f,0.5f), Color.White),
                new VertexPositionColor(new Vector3(0.5f,0.5f,-0.5f), Color.White)
            };

            cubeIndices = new short[]
            {
                0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,
                3,2,6,3,6,7,1,5,6,1,6,2,0,3,7,0,7,4
            };
        }

        private void DrawBodyPart(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                  Vector3 scale, Color color, Matrix rot)
        {
            var verts = new VertexPositionColor[8];
            for (int i = 0; i < 8; i++) verts[i] = new VertexPositionColor(cubeVertices[i].Position, color);
            Vector3 finalPos = center + Vector3.Transform(relative, rot);
            effect.World = Matrix.CreateScale(scale) * rot * Matrix.CreateTranslation(finalPos);
            foreach (var pass in effect.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 8, cubeIndices, 0, 12); }
        }

        public void Draw(GraphicsDevice device, BasicEffect effect, Vector3 pos, Color teamColor, float scale,
                         UnitType type, float orientation = 0f, float legSwing = 0f, float armSwing = 0f,
                         float bodyBob = 0f, float idleBob = 0f)
        {
            Vector3 animatedPos = pos + new Vector3(0, bodyBob + idleBob, 0);
            Matrix rot = Matrix.CreateRotationY(orientation);
            switch (type)
            {
                case UnitType.Soldier: DrawSoldier(device, effect, animatedPos, teamColor, scale, rot, legSwing, armSwing); break;
                case UnitType.Alien: DrawAlien(device, effect, animatedPos, teamColor, scale, rot, legSwing, armSwing); break;
                case UnitType.Zombie: DrawZombie(device, effect, animatedPos, teamColor, scale, rot, legSwing, armSwing); break;
                case UnitType.Heavy: DrawHeavy(device, effect, animatedPos, teamColor, scale, rot, legSwing, armSwing); break;
                case UnitType.Scout: DrawScout(device, effect, animatedPos, teamColor, scale, rot, legSwing, armSwing); break;
            }
        }

        private void DrawSoldier(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            float head = 0.25f * s, tw = 0.35f * s, th = 0.5f * s, td = 0.25f * s, lw = 0.12f * s, al = 0.45f * s, ll = 0.55f * s;
            Color skin = new(220, 180, 140), dark = new(60, 60, 80);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.3f + l * 0.05f, ll * 0.5f, l * 0.1f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.3f - l * 0.05f, ll * 0.5f, -l * 0.1f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.5f, 0), new Vector3(tw, th, td), c, r);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.6f, ll + th * 0.7f, -a * 0.15f), new Vector3(lw, al, lw), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.6f, ll + th * 0.7f, a * 0.15f), new Vector3(lw, al, lw), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.6f, 0), new Vector3(head, head * 1.2f, head), skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.6f, head * 0.6f), new Vector3(head * 0.6f, head * 0.3f, head * 0.15f), new Color(40, 40, 40), r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.5f, td * 0.7f), new Vector3(tw * 0.3f, th * 0.6f, td * 0.15f), Color.Yellow, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.6f, ll + th * 0.6f, td * 0.5f), new Vector3(lw * 0.6f, lw * 0.6f, td * 1.2f), new Color(80, 80, 80), r);
        }

        private void DrawAlien(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            float head = 0.35f * s, tw = 0.3f * s, th = 0.45f * s, td = 0.2f * s, lw = 0.1f * s, al = 0.55f * s, ll = 0.45f * s;
            Color skin = new(150, 200, 150), dark = c * 0.6f;
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.3f + l * 0.05f, ll * 0.5f, l * 0.1f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.3f - l * 0.05f, ll * 0.5f, -l * 0.1f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.5f, 0), new Vector3(tw, th, td), c, r);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.6f, ll + th * 0.6f, -a * 0.2f), new Vector3(lw, al, lw), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.6f, ll + th * 0.6f, a * 0.2f), new Vector3(lw, al, lw), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.5f, 0), new Vector3(head, head * 1.3f, head * 0.9f), skin, r);
            DrawBodyPart(d, e, p, new Vector3(-head * 0.3f, ll + th + head * 0.5f + 0.1f * s, head * 0.5f), new Vector3(head * 0.2f, head * 0.25f, head * 0.1f), Color.Black, r);
            DrawBodyPart(d, e, p, new Vector3(head * 0.3f, ll + th + head * 0.5f + 0.1f * s, head * 0.5f), new Vector3(head * 0.2f, head * 0.25f, head * 0.1f), Color.Black, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.5f, head * 0.55f), new Vector3(head * 0.8f, head * 0.15f, head * 0.05f), new Color(0, 255, 0), r);
        }

        private void DrawZombie(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            float head = 0.22f * s, tw = 0.32f * s, th = 0.48f * s, td = 0.23f * s, lw = 0.11f * s, al = 0.5f * s, ll = 0.5f * s;
            Color skin = new(140, 160, 130), dark = new(80, 70, 60);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.35f + l * 0.08f, ll * 0.5f, l * 0.15f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.35f - l * 0.08f, ll * 0.5f, -l * 0.15f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.5f, -0.1f * s), new Vector3(tw, th, td), c * 0.7f, r);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.6f, ll + th * 0.5f, 0.15f * s - a * 0.1f), new Vector3(lw, al, lw), c * 0.6f, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.6f, ll + th * 0.5f, 0.15f * s + a * 0.1f), new Vector3(lw, al, lw), c * 0.6f, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.5f, -0.05f * s), new Vector3(head, head * 1.1f, head), skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.5f, head * 0.55f), new Vector3(head * 0.5f, head * 0.2f, head * 0.05f), new Color(180, 0, 0), r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.4f, td * 0.9f), new Vector3(lw * 1.5f, lw, lw * 2f), c * 0.5f, r);
        }

        private void DrawHeavy(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            float head = 0.23f * s, tw = 0.5f * s, th = 0.55f * s, td = 0.35f * s, lw = 0.15f * s, al = 0.4f * s, ll = 0.5f * s;
            Color skin = new(220, 180, 140), dark = new(50, 50, 70);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.3f + l * 0.03f, ll * 0.5f, l * 0.08f), new Vector3(lw * 1.2f, ll, lw * 1.2f), dark, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.3f - l * 0.03f, ll * 0.5f, -l * 0.08f), new Vector3(lw * 1.2f, ll, lw * 1.2f), dark, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.5f, 0), new Vector3(tw, th, td), c, r);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.65f, ll + th * 0.7f, -a * 0.1f), new Vector3(lw * 1.3f, al, lw * 1.3f), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.65f, ll + th * 0.7f, a * 0.1f), new Vector3(lw * 1.3f, al, lw * 1.3f), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.5f, 0), new Vector3(head * 1.1f, head, head * 1.1f), skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.5f, head * 0.6f), new Vector3(head * 0.8f, head * 0.4f, head * 0.15f), new Color(30, 30, 30), r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.5f, td * 0.9f), new Vector3(tw * 0.4f, th * 0.4f, td * 0.7f), new Color(60, 60, 60), r);
        }

        private void DrawScout(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            float head = 0.22f * s, tw = 0.28f * s, th = 0.45f * s, td = 0.2f * s, lw = 0.09f * s, al = 0.42f * s, ll = 0.58f * s;
            Color skin = new(220, 180, 140), dark = new(70, 70, 90);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.25f + l * 0.07f, ll * 0.5f, l * 0.12f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.25f - l * 0.07f, ll * 0.5f, -l * 0.12f), new Vector3(lw, ll, lw), dark, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th * 0.5f, 0), new Vector3(tw, th, td), c, r);
            DrawBodyPart(d, e, p, new Vector3(-tw * 0.55f, ll + th * 0.7f, -a * 0.18f), new Vector3(lw, al, lw), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(tw * 0.55f, ll + th * 0.7f, a * 0.18f), new Vector3(lw, al, lw), c * 0.85f, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.6f, 0), new Vector3(head, head * 1.2f, head), skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.6f + 0.05f * s, head * 0.6f), new Vector3(head * 0.7f, head * 0.25f, head * 0.08f), new Color(50, 100, 150), r);
            DrawBodyPart(d, e, p, new Vector3(0, ll + th + head * 0.6f, head * 0.8f), new Vector3(head * 0.1f, head * 0.1f, head * 0.35f), new Color(255, 0, 0), r);
        }
    }
}
