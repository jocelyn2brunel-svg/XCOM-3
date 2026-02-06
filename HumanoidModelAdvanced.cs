using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace XCOM_3
{
    /// <summary>
    /// Modèle humanoïde amélioré style PS1 avec variantes pour différents types d'unités
    /// </summary>
    public class HumanoidModelAdvanced
    {
        private VertexPositionColor[] cubeVertices;
        private short[] cubeIndices;

        public enum UnitType
        {
            Soldier,        // Soldat humain standard
            Alien,          // Alien avec proportions différentes
            Zombie,         // Zombie avec posture courbée
            Heavy,          // Unité lourde plus large
            Scout           // Unité de reconnaissance plus petite/mince
        }

        public HumanoidModelAdvanced()
        {
            InitializeCube();
        }

        private void InitializeCube()
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

        private void DrawBodyPart(GraphicsDevice device, BasicEffect effect, Vector3 centerPosition,
                                   Vector3 relativePosition, Vector3 scale, Color color, Matrix rotationMatrix)
        {
            VertexPositionColor[] coloredVertices = new VertexPositionColor[8];
            for (int i = 0; i < 8; i++)
            {
                coloredVertices[i] = new VertexPositionColor(cubeVertices[i].Position, color);
            }

            // Appliquer la rotation à la position relative, puis ajouter la position centrale
            Vector3 rotatedPosition = Vector3.Transform(relativePosition, rotationMatrix);
            Vector3 finalPosition = centerPosition + rotatedPosition;

            Matrix world = Matrix.CreateScale(scale) * rotationMatrix * Matrix.CreateTranslation(finalPosition);
            effect.World = world;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(
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

        /// <summary>
        /// Dessine un modèle humanoïde avec type spécifique
        /// </summary>
        public void Draw(GraphicsDevice device, BasicEffect effect, Vector3 position,
                         Color teamColor, float scale, UnitType type, float orientation = 0f)
        {
            Matrix rotationMatrix = Matrix.CreateRotationY(orientation);

            switch (type)
            {
                case UnitType.Soldier:
                    DrawSoldier(device, effect, position, teamColor, scale, rotationMatrix);
                    break;
                case UnitType.Alien:
                    DrawAlien(device, effect, position, teamColor, scale, rotationMatrix);
                    break;
                case UnitType.Zombie:
                    DrawZombie(device, effect, position, teamColor, scale, rotationMatrix);
                    break;
                case UnitType.Heavy:
                    DrawHeavy(device, effect, position, teamColor, scale, rotationMatrix);
                    break;
                case UnitType.Scout:
                    DrawScout(device, effect, position, teamColor, scale, rotationMatrix);
                    break;
            }
        }

        private void DrawSoldier(GraphicsDevice device, BasicEffect effect, Vector3 position,
                                 Color teamColor, float scale, Matrix rotationMatrix)
        {
            // Proportions standard pour soldat humain
            float headSize = 0.25f * scale;
            float torsoWidth = 0.35f * scale;
            float torsoHeight = 0.5f * scale;
            float torsoDepth = 0.25f * scale;
            float limbWidth = 0.12f * scale;
            float armLength = 0.45f * scale;
            float legLength = 0.55f * scale;

            Color skinColor = new Color(220, 180, 140);
            Color darkColor = new Color(60, 60, 80);

            // Jambes
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);

            // Torse
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.5f, 0),
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor, rotationMatrix);

            // Bras
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.6f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);

            // Tête
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.6f, 0),
                        new Vector3(headSize, headSize * 1.2f, headSize), skinColor, rotationMatrix);

            // Casque/Visage (DEVANT de la tête)
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.6f, headSize * 0.6f),
                        new Vector3(headSize * 0.6f, headSize * 0.3f, headSize * 0.15f),
                        new Color(40, 40, 40), rotationMatrix);

            // Indicateur de direction - barre jaune devant le torse
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.5f, torsoDepth * 0.7f),
                        new Vector3(torsoWidth * 0.3f, torsoHeight * 0.6f, torsoDepth * 0.15f),
                        Color.Yellow, rotationMatrix);

            // Arme pointée vers l'avant (bras droit)
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.6f, torsoDepth * 0.5f),
                        new Vector3(limbWidth * 0.6f, limbWidth * 0.6f, torsoDepth * 1.2f),
                        new Color(80, 80, 80), rotationMatrix);
        }

        private void DrawAlien(GraphicsDevice device, BasicEffect effect, Vector3 position,
                              Color teamColor, float scale, Matrix rotationMatrix)
        {
            // Alien: tête plus grande, bras plus longs, jambes plus courtes
            float headSize = 0.35f * scale;
            float torsoWidth = 0.3f * scale;
            float torsoHeight = 0.45f * scale;
            float torsoDepth = 0.2f * scale;
            float limbWidth = 0.1f * scale;
            float armLength = 0.55f * scale;
            float legLength = 0.45f * scale;

            Color alienSkin = new Color(150, 200, 150);
            Color darkColor = teamColor * 0.6f;

            // Jambes
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);

            // Torse
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.5f, 0),
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor, rotationMatrix);

            // Bras longs
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.6f, legLength + torsoHeight * 0.6f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.6f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);

            // Grosse tête alien
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.5f, 0),
                        new Vector3(headSize, headSize * 1.3f, headSize * 0.9f), alienSkin, rotationMatrix);

            // Grands yeux noirs sur le DEVANT
            DrawBodyPart(device, effect, position,
                        new Vector3(-headSize * 0.3f, legLength + torsoHeight + headSize * 0.5f + 0.1f * scale, headSize * 0.5f),
                        new Vector3(headSize * 0.2f, headSize * 0.25f, headSize * 0.1f), Color.Black, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(headSize * 0.3f, legLength + torsoHeight + headSize * 0.5f + 0.1f * scale, headSize * 0.5f),
                        new Vector3(headSize * 0.2f, headSize * 0.25f, headSize * 0.1f), Color.Black, rotationMatrix);

            // Indicateur de direction alien - yeux verts brillants
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.5f, headSize * 0.55f),
                        new Vector3(headSize * 0.8f, headSize * 0.15f, headSize * 0.05f),
                        new Color(0, 255, 0), rotationMatrix);
        }

        private void DrawZombie(GraphicsDevice device, BasicEffect effect, Vector3 position,
                               Color teamColor, float scale, Matrix rotationMatrix)
        {
            // Zombie: posture courbée, bras qui pendent
            float headSize = 0.22f * scale;
            float torsoWidth = 0.32f * scale;
            float torsoHeight = 0.48f * scale;
            float torsoDepth = 0.23f * scale;
            float limbWidth = 0.11f * scale;
            float armLength = 0.5f * scale;
            float legLength = 0.5f * scale;

            Color zombieSkin = new Color(140, 160, 130);
            Color darkColor = new Color(80, 70, 60);

            // Jambes légèrement écartées
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.35f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.35f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);

            // Torse légèrement penché
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.5f, -0.1f * scale),
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor * 0.7f, rotationMatrix);

            // Bras qui pendent vers l'avant
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.6f, legLength + torsoHeight * 0.5f, 0.15f * scale),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.6f, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.5f, 0.15f * scale),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.6f, rotationMatrix);

            // Tête penchée
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.5f, -0.05f * scale),
                        new Vector3(headSize, headSize * 1.1f, headSize), zombieSkin, rotationMatrix);

            // Yeux rouges sur le DEVANT
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.5f, headSize * 0.55f),
                        new Vector3(headSize * 0.5f, headSize * 0.2f, headSize * 0.05f),
                        new Color(180, 0, 0), rotationMatrix);

            // Bras tendu vers l'avant (zombie qui grogne) - indicateur de direction
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.4f, torsoDepth * 0.9f),
                        new Vector3(limbWidth * 1.5f, limbWidth, limbWidth * 2.0f),
                        teamColor * 0.5f, rotationMatrix);
        }

        private void DrawHeavy(GraphicsDevice device, BasicEffect effect, Vector3 position,
                              Color teamColor, float scale, Matrix rotationMatrix)
        {
            // Unité lourde: plus large et massive
            float headSize = 0.23f * scale;
            float torsoWidth = 0.5f * scale;
            float torsoHeight = 0.55f * scale;
            float torsoDepth = 0.35f * scale;
            float limbWidth = 0.15f * scale;
            float armLength = 0.4f * scale;
            float legLength = 0.5f * scale;

            Color skinColor = new Color(220, 180, 140);
            Color darkColor = new Color(50, 50, 70);

            // Jambes épaisses
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth * 1.2f, legLength, limbWidth * 1.2f), darkColor, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth * 1.2f, legLength, limbWidth * 1.2f), darkColor, rotationMatrix);

            // Torse massif
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.5f, 0),
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor, rotationMatrix);

            // Bras épais
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.65f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth * 1.3f, armLength, limbWidth * 1.3f), teamColor * 0.85f, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.65f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth * 1.3f, armLength, limbWidth * 1.3f), teamColor * 0.85f, rotationMatrix);

            // Tête
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.5f, 0),
                        new Vector3(headSize * 1.1f, headSize, headSize * 1.1f), skinColor, rotationMatrix);

            // Visière/Casque lourd sur le DEVANT
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.5f, headSize * 0.6f),
                        new Vector3(headSize * 0.8f, headSize * 0.4f, headSize * 0.15f),
                        new Color(30, 30, 30), rotationMatrix);

            // Grosse arme lourde devant - indicateur de direction
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.5f, torsoDepth * 0.9f),
                        new Vector3(torsoWidth * 0.4f, torsoHeight * 0.4f, torsoDepth * 0.7f),
                        new Color(60, 60, 60), rotationMatrix);
        }

        private void DrawScout(GraphicsDevice device, BasicEffect effect, Vector3 position,
                              Color teamColor, float scale, Matrix rotationMatrix)
        {
            // Scout: plus petit et mince, agile
            float headSize = 0.22f * scale;
            float torsoWidth = 0.28f * scale;
            float torsoHeight = 0.45f * scale;
            float torsoDepth = 0.2f * scale;
            float limbWidth = 0.09f * scale;
            float armLength = 0.42f * scale;
            float legLength = 0.58f * scale;

            Color skinColor = new Color(220, 180, 140);
            Color darkColor = new Color(70, 70, 90);

            // Jambes fines et longues
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.25f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.25f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);

            // Torse mince
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight * 0.5f, 0),
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor, rotationMatrix);

            // Bras fins
            DrawBodyPart(device, effect, position,
                        new Vector3(-torsoWidth * 0.55f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);
            DrawBodyPart(device, effect, position,
                        new Vector3(torsoWidth * 0.55f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);

            // Tête
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.6f, 0),
                        new Vector3(headSize, headSize * 1.2f, headSize), skinColor, rotationMatrix);

            // Lunettes/Viseur sur le DEVANT
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.6f + 0.05f * scale, headSize * 0.6f),
                        new Vector3(headSize * 0.7f, headSize * 0.25f, headSize * 0.08f),
                        new Color(50, 100, 150), rotationMatrix);

            // Viseur laser mince - indicateur de direction
            DrawBodyPart(device, effect, position,
                        new Vector3(0, legLength + torsoHeight + headSize * 0.6f, headSize * 0.8f),
                        new Vector3(headSize * 0.1f, headSize * 0.1f, headSize * 0.35f),
                        new Color(255, 0, 0), rotationMatrix);
        }
    }
}
