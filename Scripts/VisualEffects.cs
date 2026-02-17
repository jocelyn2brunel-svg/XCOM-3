using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    public static class VisualEffects
    {
        private static List<ExplosionEffect> activeExplosions = new();
        private static List<GibEffect> activeGibs = new();
        private static readonly Random random = new();

        public static void PlayExplosion(Vector3 position, float radius, Renderer3D renderer)
        {
            // Crée un effet d'explosion et l'ajoute à la liste active
            activeExplosions.Add(new ExplosionEffect(position, radius, renderer));
        }

        public static void PlayGibExplosion(Vector3 position, float force, Renderer3D renderer)
        {
            activeGibs.Add(new GibEffect(position, force, renderer));
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

            for (int i = activeGibs.Count - 1; i >= 0; i--)
            {
                activeGibs[i].Update(delta);
                if (activeGibs[i].IsFinished) activeGibs.RemoveAt(i);
            }
        }

        // Appel à faire dans ton Draw() pour dessiner les explosions
        public static void Draw()
        {
            foreach (var exp in activeExplosions)
                exp.Draw();

            foreach (var gib in activeGibs)
                gib.Draw();
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

        private class GibEffect
        {
            private readonly Renderer3D renderer;
            private readonly List<GibParticle> particles;
            private float elapsed;
            private readonly float lifetime;

            public bool IsFinished => elapsed >= lifetime;

            public GibEffect(Vector3 center, float force, Renderer3D renderer)
            {
                this.renderer = renderer;
                particles = new List<GibParticle>();

                int particleCount = Math.Max(10, (int)(force * 7f));
                lifetime = 1.2f;

                for (int i = 0; i < particleCount; i++)
                {
                    Vector3 direction = new Vector3(
                        (float)(random.NextDouble() - 0.5f),
                        (float)(random.NextDouble() * 0.9f + 0.1f),
                        (float)(random.NextDouble() - 0.5f));

                    if (direction.LengthSquared() > 0.0001f)
                    {
                        direction.Normalize();
                    }

                    float speed = 2.2f + (float)random.NextDouble() * (force * 0.9f + 2.5f);
                    Vector3 velocity = direction * speed;

                    float size = 0.06f + (float)random.NextDouble() * 0.11f;
                    bool isLimbChunk = random.NextDouble() < 0.2;
                    if (isLimbChunk)
                    {
                        size *= 2.2f;
                    }

                    Color color = isLimbChunk
                        ? new Color(110, 20, 20)
                        : new Color(180, 15, 15);

                    particles.Add(new GibParticle
                    {
                        StartPosition = center,
                        Velocity = velocity,
                        Size = size,
                        Color = color
                    });
                }
            }

            public void Update(float delta)
            {
                elapsed += delta;
            }

            public void Draw()
            {
                float t = MathF.Min(1f, elapsed / lifetime);
                float fade = 1f - t;

                const float gravity = 9.2f;
                foreach (GibParticle particle in particles)
                {
                    Vector3 gravityOffset = new Vector3(0f, -0.5f * gravity * elapsed * elapsed, 0f);
                    Vector3 position = particle.StartPosition + particle.Velocity * elapsed + gravityOffset;

                    Vector3 scale = new Vector3(particle.Size * (0.9f + 0.15f * fade));
                    renderer.DrawCube(position, scale, particle.Color * fade);
                }
            }

            private struct GibParticle
            {
                public Vector3 StartPosition;
                public Vector3 Velocity;
                public float Size;
                public Color Color;
            }
        }
    }
}
