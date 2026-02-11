using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace XCOM_3
{
    public class HumanoidModel
    {
        private VertexPositionColor[] cubeVertices;
        private short[] cubeIndices;

        public HumanoidModel() => InitializeCube();

        private void InitializeCube()
        {
            cubeVertices = new VertexPositionColor[8]
            {
                new(new Vector3(-0.5f,-0.5f,-0.5f),Color.White),
                new(new Vector3(-0.5f,-0.5f,0.5f),Color.White),
                new(new Vector3(0.5f,-0.5f,0.5f),Color.White),
                new(new Vector3(0.5f,-0.5f,-0.5f),Color.White),
                new(new Vector3(-0.5f,0.5f,-0.5f),Color.White),
                new(new Vector3(-0.5f,0.5f,0.5f),Color.White),
                new(new Vector3(0.5f,0.5f,0.5f),Color.White),
                new(new Vector3(0.5f,0.5f,-0.5f),Color.White)
            };

            cubeIndices = new short[]
            {
                0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,3,2,6,3,6,7,1,5,6,1,6,2,0,3,7,0,7,4
            };
        }

        private void DrawBodyPart(GraphicsDevice d, BasicEffect e, Vector3 pos, Vector3 scale, Color col)
        {
            var verts = new VertexPositionColor[8];
            for (int i = 0; i < 8; i++) verts[i] = new VertexPositionColor(cubeVertices[i].Position, col);
            e.World = Matrix.CreateScale(scale) * Matrix.CreateTranslation(pos);
            foreach (var pass in e.CurrentTechnique.Passes) { pass.Apply(); d.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 8, cubeIndices, 0, 12); }
        }

        public void Draw(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s = 1f)
        {
            float uh = 1.8f * s, hs = 0.25f * s, tw = 0.35f * s, th = 0.5f * s, td = 0.25f * s, lw = 0.12f * s, al = 0.45f * s, ll = 0.55f * s;
            Color skin = new(220, 180, 140), dark = c * 0.7f;

            DrawBodyPart(d, e, p + new Vector3(-tw * 0.3f, ll * 0.5f, 0), new Vector3(lw, ll, lw), dark);
            DrawBodyPart(d, e, p + new Vector3(tw * 0.3f, ll * 0.5f, 0), new Vector3(lw, ll, lw), dark);
            DrawBodyPart(d, e, p + new Vector3(0, ll + th * 0.5f, 0), new Vector3(tw, th, td), c);
            DrawBodyPart(d, e, p + new Vector3(-tw * 0.6f, ll + th * 0.7f, 0), new Vector3(lw, al, lw), c * 0.85f);
            DrawBodyPart(d, e, p + new Vector3(tw * 0.6f, ll + th * 0.7f, 0), new Vector3(lw, al, lw), c * 0.85f);
            DrawBodyPart(d, e, p + new Vector3(0, ll + th + hs * 0.6f, 0), new Vector3(hs, hs * 1.2f, hs), skin);
            DrawBodyPart(d, e, p + new Vector3(0, ll + th + hs * 0.6f, hs * 0.6f), new Vector3(hs * 0.6f, hs * 0.3f, hs * 0.1f), new Color(40, 40, 40));
        }
    }
}
