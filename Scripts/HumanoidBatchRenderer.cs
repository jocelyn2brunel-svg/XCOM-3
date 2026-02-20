using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace XCOM_3
{
    public class HumanoidBatchRenderer
    {
        private VertexPositionColor[] verts, primVerts;
        private short[] inds, primInds;
        private int vCount = 0, iCount = 0;
        private const int MAX = 2000;

        public HumanoidBatchRenderer()
        {
            verts = new VertexPositionColor[MAX * 8];
            inds = new short[MAX * 36];
            primVerts = new VertexPositionColor[8]
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
            primInds = new short[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 3, 2, 6, 3, 6, 7, 1, 5, 6, 1, 6, 2, 0, 3, 7, 0, 7, 4 };
        }

        public void BeginBatch() { vCount = iCount = 0; }

        public void AddBodyPart(Vector3 c, Vector3 r, Vector3 s, Color col, Matrix rot)
        {
            if (vCount + 8 > verts.Length || iCount + 36 > inds.Length) { Console.WriteLine("Batch full"); return; }
            int baseV = vCount;
            Vector3 relRot = Vector3.Transform(r, rot);
            for (int i = 0; i < 8; i++)
                verts[vCount++] = new VertexPositionColor(c + relRot + Vector3.Transform(primVerts[i].Position * s, rot), col);
            for (int i = 0; i < 36; i++) inds[iCount++] = (short)(primInds[i] + baseV);
        }

        public void EndBatch(GraphicsDevice d, BasicEffect e)
        {
            if (vCount == 0) return;
            Matrix old = e.World; e.World = Matrix.Identity;
            foreach (var pass in e.CurrentTechnique.Passes) { pass.Apply(); d.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, vCount, inds, 0, iCount / 3); }
            e.World = old;
        }

        public void AddHumain(Vector3 p, Color c, float sc, Matrix rot, float ls = 0f, float asw = 0f, float bb = 0f, float ib = 0f)
        {
            Vector3 pos = p + new Vector3(0, bb + ib, 0);
            float hs = 0.25f * sc, tw = 0.35f * sc, th = 0.5f * sc, td = 0.25f * sc, lw = 0.12f * sc, al = 0.45f * sc, ll = 0.55f * sc;
            Color skin = new(220, 180, 140), dark = new(60, 60, 80);

            AddBodyPart(pos, new Vector3(-tw * 0.3f + ls * 0.05f, ll * 0.5f, ls * 0.1f), new Vector3(lw, ll, lw), dark, rot);
            AddBodyPart(pos, new Vector3(tw * 0.3f - ls * 0.05f, ll * 0.5f, -ls * 0.1f), new Vector3(lw, ll, lw), dark, rot);
            AddBodyPart(pos, new Vector3(0, ll + th * 0.5f, 0), new Vector3(tw, th, td), c, rot);
            AddBodyPart(pos, new Vector3(-tw * 0.6f, ll + th * 0.7f, -asw * 0.12f), new Vector3(lw, al, lw), c * 0.85f, rot);
            AddBodyPart(pos, new Vector3(tw * 0.6f, ll + th * 0.7f, asw * 0.12f), new Vector3(lw, al, lw), c * 0.85f, rot);
            AddBodyPart(pos, new Vector3(0, ll + th + hs * 0.6f, 0), new Vector3(hs, hs * 1.2f, hs), skin, rot);
            AddBodyPart(pos, new Vector3(0, ll + th + hs * 0.6f, hs * 0.6f), new Vector3(hs * 0.6f, hs * 0.3f, hs * 0.1f), new Color(40, 40, 40), rot);
        }

        public void LogStats() { Console.WriteLine($"Batch Stats: {vCount / 8} body parts, {vCount} vertices, {iCount / 3} triangles"); }
    }
}
