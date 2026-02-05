using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace XCOM_3
{
    /// <summary>
    /// Modèle humanoïde simple style PS1/Resident Evil avec graphismes low-poly
    /// </summary>
    public class HumanoidModel
    {
        private VertexPositionColor[] cubeVertices;
        private short[] cubeIndices;

        public HumanoidModel()
        {
            InitializeCube();
        }

        private void InitializeCube()
        {
            // Cube de base pour construire toutes les parties du corps
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
                0, 1, 2, 0, 2, 3,  // Bas
                4, 6, 5, 4, 7, 6,  // Haut
                0, 4, 5, 0, 5, 1,  // Gauche
                3, 2, 6, 3, 6, 7,  // Droite
                1, 5, 6, 1, 6, 2,  // Avant
                0, 3, 7, 0, 7, 4   // Arrière
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
        /// Dessine un modèle humanoïde complet style PS1
        /// </summary>
        /// <param name="device">GraphicsDevice</param>
        /// <param name="effect">BasicEffect pour le rendu</param>
        /// <param name="position">Position de base (au sol)</param>
        /// <param name="teamColor">Couleur de l'équipe</param>
        /// <param name="scale">Échelle globale</param>
        public void Draw(GraphicsDevice device, BasicEffect effect, Vector3 position, 
                         Color teamColor, float scale = 1.0f)
        {
            float unitHeight = 1.8f * scale;  // Hauteur totale du personnage
            float headSize = 0.25f * scale;
            float torsoWidth = 0.35f * scale;
            float torsoHeight = 0.5f * scale;
            float torsoDepth = 0.25f * scale;
            float limbWidth = 0.12f * scale;
            float armLength = 0.45f * scale;
            float legLength = 0.55f * scale;

            // Couleurs pour les différentes parties
            Color skinColor = new Color(220, 180, 140);  // Couleur chair
            Color darkColor = teamColor * 0.7f;  // Couleur sombre pour vêtements
            
            // JAMBES
            // Jambe gauche
            Vector3 leftLegPos = position + new Vector3(-torsoWidth * 0.3f, legLength * 0.5f, 0);
            DrawBodyPart(device, effect, leftLegPos, 
                        new Vector3(limbWidth, legLength, limbWidth), 
                        darkColor);

            // Jambe droite
            Vector3 rightLegPos = position + new Vector3(torsoWidth * 0.3f, legLength * 0.5f, 0);
            DrawBodyPart(device, effect, rightLegPos, 
                        new Vector3(limbWidth, legLength, limbWidth), 
                        darkColor);

            // TORSE
            Vector3 torsoPos = position + new Vector3(0, legLength + torsoHeight * 0.5f, 0);
            DrawBodyPart(device, effect, torsoPos, 
                        new Vector3(torsoWidth, torsoHeight, torsoDepth), 
                        teamColor);

            // BRAS
            // Bras gauche
            Vector3 leftArmPos = position + new Vector3(
                -torsoWidth * 0.6f, 
                legLength + torsoHeight * 0.7f, 
                0
            );
            DrawBodyPart(device, effect, leftArmPos, 
                        new Vector3(limbWidth, armLength, limbWidth), 
                        teamColor * 0.85f);

            // Bras droit
            Vector3 rightArmPos = position + new Vector3(
                torsoWidth * 0.6f, 
                legLength + torsoHeight * 0.7f, 
                0
            );
            DrawBodyPart(device, effect, rightArmPos, 
                        new Vector3(limbWidth, armLength, limbWidth), 
                        teamColor * 0.85f);

            // TÊTE
            Vector3 headPos = position + new Vector3(0, legLength + torsoHeight + headSize * 0.6f, 0);
            DrawBodyPart(device, effect, headPos, 
                        new Vector3(headSize, headSize * 1.2f, headSize), 
                        skinColor);

            // DÉTAILS - Casque/Yeux (petit cube pour donner du caractère)
            Vector3 faceDetailPos = headPos + new Vector3(0, 0, headSize * 0.6f);
            DrawBodyPart(device, effect, faceDetailPos,
                        new Vector3(headSize * 0.6f, headSize * 0.3f, headSize * 0.1f),
                        new Color(40, 40, 40));
        }
    }
}
