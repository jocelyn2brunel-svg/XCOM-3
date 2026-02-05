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

        private void DrawBodyPart(GraphicsDevice device, BasicEffect effect, Vector3 position, 
                                   Vector3 scale, Color color)
        {
            VertexPositionColor[] coloredVertices = new VertexPositionColor[8];
            for (int i = 0; i < 8; i++)
            {
                coloredVertices[i] = new VertexPositionColor(cubeVertices[i].Position, color);
            }

            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
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
                         Color teamColor, float scale, UnitType type)
        {
            switch (type)
            {
                case UnitType.Soldier:
                    DrawSoldier(device, effect, position, teamColor, scale);
                    break;
                case UnitType.Alien:
                    DrawAlien(device, effect, position, teamColor, scale);
                    break;
                case UnitType.Zombie:
                    DrawZombie(device, effect, position, teamColor, scale);
                    break;
                case UnitType.Heavy:
                    DrawHeavy(device, effect, position, teamColor, scale);
                    break;
                case UnitType.Scout:
                    DrawScout(device, effect, position, teamColor, scale);
                    break;
            }
        }

        private void DrawSoldier(GraphicsDevice device, BasicEffect effect, Vector3 position, 
                                 Color teamColor, float scale)
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
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);

            // Torse
            Vector3 torsoPos = position + new Vector3(0, legLength + torsoHeight * 0.5f, 0);
            DrawBodyPart(device, effect, torsoPos,
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor);

            // Bras
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.6f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f);

            // Tête
            Vector3 headPos = position + new Vector3(0, legLength + torsoHeight + headSize * 0.6f, 0);
            DrawBodyPart(device, effect, headPos,
                        new Vector3(headSize, headSize * 1.2f, headSize), skinColor);

            // Casque/Visage
            DrawBodyPart(device, effect, headPos + new Vector3(0, 0, headSize * 0.6f),
                        new Vector3(headSize * 0.6f, headSize * 0.3f, headSize * 0.1f),
                        new Color(40, 40, 40));
        }

        private void DrawAlien(GraphicsDevice device, BasicEffect effect, Vector3 position,
                              Color teamColor, float scale)
        {
            // Alien: tête plus grande, bras plus longs, jambes plus courtes
            float headSize = 0.35f * scale;  // Tête plus grande
            float torsoWidth = 0.3f * scale;
            float torsoHeight = 0.45f * scale;
            float torsoDepth = 0.2f * scale;
            float limbWidth = 0.1f * scale;  // Membres plus fins
            float armLength = 0.55f * scale;  // Bras plus longs
            float legLength = 0.45f * scale;  // Jambes plus courtes

            Color alienSkin = new Color(150, 200, 150);  // Vert alien
            Color darkColor = teamColor * 0.6f;

            // Jambes
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);

            // Torse
            Vector3 torsoPos = position + new Vector3(0, legLength + torsoHeight * 0.5f, 0);
            DrawBodyPart(device, effect, torsoPos,
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor);

            // Bras longs
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.6f, legLength + torsoHeight * 0.6f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.6f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f);

            // Grosse tête alien
            Vector3 headPos = position + new Vector3(0, legLength + torsoHeight + headSize * 0.5f, 0);
            DrawBodyPart(device, effect, headPos,
                        new Vector3(headSize, headSize * 1.3f, headSize * 0.9f), alienSkin);

            // Grands yeux noirs
            DrawBodyPart(device, effect, headPos + new Vector3(-headSize * 0.3f, 0.1f * scale, headSize * 0.5f),
                        new Vector3(headSize * 0.2f, headSize * 0.25f, headSize * 0.1f), Color.Black);
            DrawBodyPart(device, effect, headPos + new Vector3(headSize * 0.3f, 0.1f * scale, headSize * 0.5f),
                        new Vector3(headSize * 0.2f, headSize * 0.25f, headSize * 0.1f), Color.Black);
        }

        private void DrawZombie(GraphicsDevice device, BasicEffect effect, Vector3 position,
                               Color teamColor, float scale)
        {
            // Zombie: posture courbée, bras qui pendent
            float headSize = 0.22f * scale;
            float torsoWidth = 0.32f * scale;
            float torsoHeight = 0.48f * scale;
            float torsoDepth = 0.23f * scale;
            float limbWidth = 0.11f * scale;
            float armLength = 0.5f * scale;
            float legLength = 0.5f * scale;

            Color zombieSkin = new Color(140, 160, 130);  // Peau verdâtre
            Color darkColor = new Color(80, 70, 60);

            // Jambes légèrement écartées
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.35f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.35f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);

            // Torse légèrement penché
            Vector3 torsoPos = position + new Vector3(0, legLength + torsoHeight * 0.5f, -0.1f * scale);
            DrawBodyPart(device, effect, torsoPos,
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor * 0.7f);

            // Bras qui pendent vers l'avant
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.6f, legLength + torsoHeight * 0.5f, 0.15f * scale),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.6f);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.5f, 0.15f * scale),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.6f);

            // Tête penchée
            Vector3 headPos = position + new Vector3(0, legLength + torsoHeight + headSize * 0.5f, -0.05f * scale);
            DrawBodyPart(device, effect, headPos,
                        new Vector3(headSize, headSize * 1.1f, headSize), zombieSkin);

            // Yeux rouges
            DrawBodyPart(device, effect, headPos + new Vector3(0, 0, headSize * 0.6f),
                        new Vector3(headSize * 0.5f, headSize * 0.2f, headSize * 0.05f),
                        new Color(180, 0, 0));
        }

        private void DrawHeavy(GraphicsDevice device, BasicEffect effect, Vector3 position,
                              Color teamColor, float scale)
        {
            // Unité lourde: plus large et massive
            float headSize = 0.23f * scale;
            float torsoWidth = 0.5f * scale;  // Plus large
            float torsoHeight = 0.55f * scale;  // Plus haut
            float torsoDepth = 0.35f * scale;  // Plus épais
            float limbWidth = 0.15f * scale;  // Membres plus épais
            float armLength = 0.4f * scale;
            float legLength = 0.5f * scale;

            Color skinColor = new Color(220, 180, 140);
            Color darkColor = new Color(50, 50, 70);

            // Jambes épaisses
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth * 1.2f, legLength, limbWidth * 1.2f), darkColor);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.3f, legLength * 0.5f, 0),
                        new Vector3(limbWidth * 1.2f, legLength, limbWidth * 1.2f), darkColor);

            // Torse massif
            Vector3 torsoPos = position + new Vector3(0, legLength + torsoHeight * 0.5f, 0);
            DrawBodyPart(device, effect, torsoPos,
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor);

            // Bras épais
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.65f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth * 1.3f, armLength, limbWidth * 1.3f), teamColor * 0.85f);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.65f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth * 1.3f, armLength, limbWidth * 1.3f), teamColor * 0.85f);

            // Tête
            Vector3 headPos = position + new Vector3(0, legLength + torsoHeight + headSize * 0.5f, 0);
            DrawBodyPart(device, effect, headPos,
                        new Vector3(headSize * 1.1f, headSize, headSize * 1.1f), skinColor);

            // Visière/Casque lourd
            DrawBodyPart(device, effect, headPos + new Vector3(0, 0, headSize * 0.6f),
                        new Vector3(headSize * 0.8f, headSize * 0.4f, headSize * 0.15f),
                        new Color(30, 30, 30));
        }

        private void DrawScout(GraphicsDevice device, BasicEffect effect, Vector3 position,
                              Color teamColor, float scale)
        {
            // Scout: plus petit et mince, agile
            float headSize = 0.22f * scale;
            float torsoWidth = 0.28f * scale;  // Plus étroit
            float torsoHeight = 0.45f * scale;
            float torsoDepth = 0.2f * scale;
            float limbWidth = 0.09f * scale;  // Membres fins
            float armLength = 0.42f * scale;
            float legLength = 0.58f * scale;  // Jambes plus longues pour vitesse

            Color skinColor = new Color(220, 180, 140);
            Color darkColor = new Color(70, 70, 90);

            // Jambes fines et longues
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.25f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.25f, legLength * 0.5f, 0),
                        new Vector3(limbWidth, legLength, limbWidth), darkColor);

            // Torse mince
            Vector3 torsoPos = position + new Vector3(0, legLength + torsoHeight * 0.5f, 0);
            DrawBodyPart(device, effect, torsoPos,
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor);

            // Bras fins
            DrawBodyPart(device, effect, position + new Vector3(-torsoWidth * 0.55f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f);
            DrawBodyPart(device, effect, position + new Vector3(torsoWidth * 0.55f, legLength + torsoHeight * 0.7f, 0),
                        new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f);

            // Tête
            Vector3 headPos = position + new Vector3(0, legLength + torsoHeight + headSize * 0.6f, 0);
            DrawBodyPart(device, effect, headPos,
                        new Vector3(headSize, headSize * 1.2f, headSize), skinColor);

            // Lunettes/Viseur
            DrawBodyPart(device, effect, headPos + new Vector3(0, 0.05f * scale, headSize * 0.6f),
                        new Vector3(headSize * 0.7f, headSize * 0.25f, headSize * 0.08f),
                        new Color(50, 100, 150));
        }
    }
}
