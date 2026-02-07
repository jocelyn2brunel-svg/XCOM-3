using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace XCOM_3
{
    /// <summary>
    /// Batch renderer pour dessiner plusieurs humanoïdes en un seul draw call
    /// OPTIMISATION MAJEURE : Réduit 80+ draw calls à 1-2 par frame
    /// </summary>
    public class HumanoidBatchRenderer
    {
        private VertexPositionColor[] batchedVertices;
        private short[] batchedIndices;
        private int currentVertexCount = 0;
        private int currentIndexCount = 0;
        
        // Données du cube de base (comme dans HumanoidModel)
        private VertexPositionColor[] cubePrimitiveVertices;
        private short[] cubePrimitiveIndices;
        
        private const int MAX_BODY_PARTS = 2000; // Support pour ~200 unités avec 10 parties chacune
        
        public HumanoidBatchRenderer()
        {
            // Pré-allouer les buffers
            batchedVertices = new VertexPositionColor[MAX_BODY_PARTS * 8];
            batchedIndices = new short[MAX_BODY_PARTS * 36];
            
            InitializeCubePrimitive();
        }
        
        private void InitializeCubePrimitive()
        {
            cubePrimitiveVertices = new VertexPositionColor[8];
            cubePrimitiveVertices[0] = new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), Color.White);
            cubePrimitiveVertices[1] = new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.5f), Color.White);
            cubePrimitiveVertices[2] = new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.5f), Color.White);
            cubePrimitiveVertices[3] = new VertexPositionColor(new Vector3(0.5f, -0.5f, -0.5f), Color.White);
            cubePrimitiveVertices[4] = new VertexPositionColor(new Vector3(-0.5f, 0.5f, -0.5f), Color.White);
            cubePrimitiveVertices[5] = new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.5f), Color.White);
            cubePrimitiveVertices[6] = new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.5f), Color.White);
            cubePrimitiveVertices[7] = new VertexPositionColor(new Vector3(0.5f, 0.5f, -0.5f), Color.White);

            cubePrimitiveIndices = new short[]
            {
                0, 1, 2, 0, 2, 3,  // Bas
                4, 6, 5, 4, 7, 6,  // Haut
                0, 4, 5, 0, 5, 1,  // Gauche
                3, 2, 6, 3, 6, 7,  // Droite
                1, 5, 6, 1, 6, 2,  // Avant
                0, 3, 7, 0, 7, 4   // Arrière
            };
        }
        
        /// <summary>
        /// Commence un nouveau batch - appeler au début du rendu
        /// </summary>
        public void BeginBatch()
        {
            currentVertexCount = 0;
            currentIndexCount = 0;
        }
        
        /// <summary>
        /// Ajoute une partie de corps au batch
        /// </summary>
        /// <param name="centerPosition">Position centrale de l'unité</param>
        /// <param name="relativePosition">Position relative de la partie du corps</param>
        /// <param name="scale">Échelle de la partie</param>
        /// <param name="color">Couleur de la partie</param>
        /// <param name="rotationMatrix">Matrice de rotation de l'unité</param>
        public void AddBodyPart(Vector3 centerPosition, Vector3 relativePosition, 
                                Vector3 scale, Color color, Matrix rotationMatrix)
        {
            if (currentVertexCount + 8 > batchedVertices.Length || 
                currentIndexCount + 36 > batchedIndices.Length)
            {
                // Buffer plein, on devrait flush mais pour simplifier on skip
                Console.WriteLine("WARNING: Batch buffer full, increase MAX_BODY_PARTS");
                return;
            }
            
            int baseVertex = currentVertexCount;
            
            // Transformer et ajouter les vertices
            for (int i = 0; i < 8; i++)
            {
                // 1. Scale
                Vector3 scaledPos = cubePrimitiveVertices[i].Position * scale;
                
                // 2. Rotate
                Vector3 rotatedPos = Vector3.Transform(scaledPos, rotationMatrix);
                
                // 3. Translate (position relative puis position centrale)
                Vector3 relativeRotated = Vector3.Transform(relativePosition, rotationMatrix);
                Vector3 finalPos = centerPosition + relativeRotated + rotatedPos;
                
                batchedVertices[currentVertexCount++] = 
                    new VertexPositionColor(finalPos, color);
            }
            
            // Ajouter les indices avec offset
            for (int i = 0; i < 36; i++)
            {
                batchedIndices[currentIndexCount++] = 
                    (short)(cubePrimitiveIndices[i] + baseVertex);
            }
        }
        
        /// <summary>
        /// Termine le batch et dessine tout en un seul draw call
        /// </summary>
        public void EndBatch(GraphicsDevice device, BasicEffect effect)
        {
            if (currentVertexCount == 0)
                return; // Rien à dessiner
            
            // Important : le world matrix doit être Identity 
            // car on a déjà transformé les vertices
            Matrix previousWorld = effect.World;
            effect.World = Matrix.Identity;
            
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                
                // UN SEUL DRAW CALL pour TOUTES les parties de TOUTES les unités!
                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    batchedVertices,
                    0,
                    currentVertexCount,
                    batchedIndices,
                    0,
                    currentIndexCount / 3 // Nombre de triangles
                );
            }
            
            // Restaurer
            effect.World = previousWorld;
        }
        
        /// <summary>
        /// Ajoute un soldat complet au batch
        /// Version optimisée de HumanoidModelAdvanced.DrawSoldier
        /// </summary>
        public void AddSoldier(Vector3 position, Color teamColor, float scale, 
                              Matrix rotationMatrix, float legSwing = 0f, 
                              float armSwing = 0f, float bodyBob = 0f, float idleBob = 0f)
        {
            Vector3 animatedPosition = position + new Vector3(0, bodyBob + idleBob, 0);
            
            // Proportions standard
            float headSize = 0.25f * scale;
            float torsoWidth = 0.35f * scale;
            float torsoHeight = 0.5f * scale;
            float torsoDepth = 0.25f * scale;
            float limbWidth = 0.12f * scale;
            float armLength = 0.45f * scale;
            float legLength = 0.55f * scale;

            Color skinColor = new Color(220, 180, 140);
            Color darkColor = new Color(60, 60, 80);

            // Jambes avec animation
            AddBodyPart(animatedPosition,
                new Vector3(-torsoWidth * 0.3f + legSwing * 0.05f, legLength * 0.5f, legSwing * 0.1f),
                new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);
            
            AddBodyPart(animatedPosition,
                new Vector3(torsoWidth * 0.3f - legSwing * 0.05f, legLength * 0.5f, -legSwing * 0.1f),
                new Vector3(limbWidth, legLength, limbWidth), darkColor, rotationMatrix);

            // Torse
            AddBodyPart(animatedPosition,
                new Vector3(0, legLength + torsoHeight * 0.5f, 0),
                new Vector3(torsoWidth, torsoHeight, torsoDepth), teamColor, rotationMatrix);

            // Bras avec animation
            AddBodyPart(animatedPosition,
                new Vector3(-torsoWidth * 0.6f, legLength + torsoHeight * 0.7f, -armSwing * 0.12f),
                new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);
            
            AddBodyPart(animatedPosition,
                new Vector3(torsoWidth * 0.6f, legLength + torsoHeight * 0.7f, armSwing * 0.12f),
                new Vector3(limbWidth, armLength, limbWidth), teamColor * 0.85f, rotationMatrix);

            // Tête
            AddBodyPart(animatedPosition,
                new Vector3(0, legLength + torsoHeight + headSize * 0.6f, 0),
                new Vector3(headSize, headSize * 1.2f, headSize), skinColor, rotationMatrix);

            // Détails visage sur le DEVANT (indicateur de direction)
            AddBodyPart(animatedPosition,
                new Vector3(0, legLength + torsoHeight + headSize * 0.6f, headSize * 0.6f),
                new Vector3(headSize * 0.6f, headSize * 0.3f, headSize * 0.1f),
                new Color(40, 40, 40), rotationMatrix);
        }
        
        /// <summary>
        /// Stats pour debugging
        /// </summary>
        public void LogStats()
        {
            int vertexCount = currentVertexCount;
            int triangleCount = currentIndexCount / 3;
            int bodyPartCount = vertexCount / 8;
            
            Console.WriteLine($"Batch Stats: {bodyPartCount} body parts, " +
                            $"{vertexCount} vertices, {triangleCount} triangles");
        }
    }
}
