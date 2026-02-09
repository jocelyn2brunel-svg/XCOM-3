using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    public static class VisualEffects
    {
        private static List<ExplosionEffect> activeExplosions = new();

        public static void PlayExplosion(Vector3 position, float radius, Renderer3D renderer)
        {
            // Crée un effet d'explosion et l'ajoute à la liste active
            activeExplosions.Add(new ExplosionEffect(position, radius, renderer));
        }

        // Appel à faire dans ton Update() pour mettre à jour les effets actifs
        public static void Update(GameTime gameTime)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            for (int i = activeExplosions.Count - 1; i >= 0; i--)
            {
                activeExplosions[i].Update(delta);
                if (activeExplosions[i].IsFinished) activeExplosions.RemoveAt(i);
            }
        }

        // Appel à faire dans ton Draw() pour dessiner les explosions
        public static void Draw()
        {
            foreach (var exp in activeExplosions)
                exp.Draw();
        }

        // --- Classe interne représentant une explosion ---
        private class ExplosionEffect
        {
            private Vector3 position;
            private float radius;
            private float lifetime = 0.5f; // durée totale en secondes
            private float elapsed = 0f;
            private Renderer3D renderer;
            private List<Vector3> particleOffsets;

            public bool IsFinished => elapsed >= lifetime;

            public ExplosionEffect(Vector3 pos, float rad, Renderer3D rend)
            {
                position = pos;
                radius = rad;
                renderer = rend;

                // Génération de particules aléatoires autour du centre
                particleOffsets = new List<Vector3>();
                Random rand = new();
                for (int i = 0; i < 20; i++)
                {
                    float x = (float)(rand.NextDouble() - 0.5) * radius;
                    float y = (float)(rand.NextDouble()) * radius * 0.5f;
                    float z = (float)(rand.NextDouble() - 0.5) * radius;
                    particleOffsets.Add(new Vector3(x, y, z));
                }
            }

            public void Update(float delta)
            {
                elapsed += delta;
            }

            public void Draw()
            {
                float alpha = 1f - elapsed / lifetime;
                Color flashColor = new Color(255, 160, 0) * alpha; // orange vif qui s'estompe

                // Flash central
                renderer.DrawCube(position, new Vector3(radius * 0.5f), flashColor);

                // Particules
                foreach (var offset in particleOffsets)
                {
                    Vector3 pos = position + offset + new Vector3(0, elapsed * radius * 0.5f, 0); // monte légèrement
                    renderer.DrawCube(pos, new Vector3(radius * 0.1f), new Color(200, 100, 50) * alpha);
                }
            }
        }
    }
}
