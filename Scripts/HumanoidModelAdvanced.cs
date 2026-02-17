using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Modèle 3D humanoïde amélioré avec visualisation de l'équipement
    /// </summary>
    public class HumanoidModelAdvanced
    {
        private const float TacticalFlashlightWeightGrams = 300f;
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
            => DrawBodyPart(device, effect, center, relative, scale, color, rot, rot);

        private void DrawBodyPart(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                  Vector3 scale, Color color, Matrix modelRot, Matrix partRot)
        {
            var verts = new VertexPositionColor[8];
            for (int i = 0; i < 8; i++) verts[i] = new VertexPositionColor(cubeVertices[i].Position, color);
            Vector3 finalPos = center + Vector3.Transform(relative, modelRot);
            effect.World = Matrix.CreateScale(scale) * partRot * Matrix.CreateTranslation(finalPos);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 8, cubeIndices, 0, 12);
            }
        }

        private void DrawTorsoPolygon(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                      float height, float topWidth, float bottomWidth, float depth,
                                      Color color, Matrix modelRot)
        {
            float halfHeight = height * 0.5f;
            float halfTop = topWidth * 0.5f;
            float halfBottom = bottomWidth * 0.5f;
            float halfDepth = depth * 0.5f;

            var verts = new VertexPositionColor[8]
            {
                // Bas
                new VertexPositionColor(new Vector3(-halfBottom, -halfHeight, -halfDepth), color),
                new VertexPositionColor(new Vector3(-halfBottom, -halfHeight,  halfDepth), color),
                new VertexPositionColor(new Vector3( halfBottom, -halfHeight,  halfDepth), color),
                new VertexPositionColor(new Vector3( halfBottom, -halfHeight, -halfDepth), color),
                // Haut
                new VertexPositionColor(new Vector3(-halfTop, halfHeight, -halfDepth), color),
                new VertexPositionColor(new Vector3(-halfTop, halfHeight,  halfDepth), color),
                new VertexPositionColor(new Vector3( halfTop, halfHeight,  halfDepth), color),
                new VertexPositionColor(new Vector3( halfTop, halfHeight, -halfDepth), color)
            };

            Vector3 finalPos = center + Vector3.Transform(relative, modelRot);
            effect.World = modelRot * Matrix.CreateTranslation(finalPos);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 8, cubeIndices, 0, 12);
            }
        }

        private static Matrix CreateRotationFromUpTo(Vector3 direction)
        {
            const float epsilon = 1e-5f;
            if (direction.LengthSquared() < epsilon)
                return Matrix.Identity;

            Vector3 from = Vector3.Up;
            Vector3 to = Vector3.Normalize(direction);
            float dot = MathHelper.Clamp(Vector3.Dot(from, to), -1f, 1f);

            if (dot > 0.9999f)
                return Matrix.Identity;

            if (dot < -0.9999f)
                return Matrix.CreateFromAxisAngle(Vector3.Right, MathHelper.Pi);

            Vector3 axis = Vector3.Normalize(Vector3.Cross(from, to));
            float angle = MathF.Acos(dot);
            return Matrix.CreateFromAxisAngle(axis, angle);
        }

        private void DrawRoundedCapsuleBetween(GraphicsDevice device, BasicEffect effect, Vector3 center,
                                               Vector3 start, Vector3 end, float radius,
                                               Color color, Matrix modelRot, int segments = 6)
        {
            Vector3 segment = end - start;
            float length = segment.Length();
            if (length < 0.0001f)
                return;

            int safeSegments = Math.Max(2, segments);
            float segmentHeight = length / safeSegments;
            Vector3 dir = segment / length;
            Matrix limbRot = CreateRotationFromUpTo(dir);
            Matrix partRot = limbRot * modelRot;
            Vector3 midpoint = (start + end) * 0.5f;

            for (int i = 0; i < safeSegments; i++)
            {
                float t = safeSegments == 1 ? 0f : i / (safeSegments - 1f);
                float arch = 1f - MathF.Abs((t - 0.5f) * 2f);
                float bulge = 0.82f + arch * 0.18f;

                Vector3 localOffset = new Vector3(0f, (i - (safeSegments - 1) * 0.5f) * segmentHeight, 0f);
                Vector3 rotatedOffset = Vector3.Transform(localOffset, limbRot);
                Vector3 segPos = midpoint + rotatedOffset;
                Vector3 segScale = new Vector3(radius * bulge, segmentHeight * 1.05f, radius * bulge);

                DrawBodyPart(device, effect, center, segPos, segScale, color * (0.92f + arch * 0.08f), modelRot, partRot);
            }
        }

        private void DrawRoundedCapsuleY(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                         float height, float radius, Color color, Matrix rot, int segments = 6)
        {
            int safeSegments = Math.Max(2, segments);
            float segmentHeight = height / safeSegments;
            float half = (safeSegments - 1) * 0.5f;

            for (int i = 0; i < safeSegments; i++)
            {
                float t = safeSegments == 1 ? 0f : i / (safeSegments - 1f);
                float arch = 1f - MathF.Abs((t - 0.5f) * 2f);
                float bulge = 0.82f + arch * 0.18f;

                Vector3 segPos = relative + new Vector3(0f, (i - half) * segmentHeight, 0f);
                Vector3 segScale = new Vector3(radius * bulge, segmentHeight * 1.05f, radius * bulge);
                DrawBodyPart(device, effect, center, segPos, segScale, color * (0.92f + arch * 0.08f), rot);
            }
        }

        private void DrawRoundedHead(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                     float radius, Color color, Matrix rot)
        {
            DrawRoundedCapsuleY(device, effect, center, relative, radius * 1.4f, radius * 1.02f, color, rot, 7);
        }

        private void DrawSphere(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                float radius, Color color, Matrix rot, int latSegments = 8, int lonSegments = 12)
        {
            int lat = Math.Max(3, latSegments);
            int lon = Math.Max(6, lonSegments);

            var verts = new List<VertexPositionColor>((lat + 1) * (lon + 1));
            var indices = new List<short>(lat * lon * 6);

            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                float phi = v * MathF.PI;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float theta = u * MathF.Tau;
                    float sinTheta = MathF.Sin(theta);
                    float cosTheta = MathF.Cos(theta);

                    Vector3 local = new(
                        radius * sinPhi * cosTheta,
                        radius * cosPhi,
                        radius * sinPhi * sinTheta);

                    verts.Add(new VertexPositionColor(local, color));
                }
            }

            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    short i0 = (short)(y * (lon + 1) + x);
                    short i1 = (short)(i0 + 1);
                    short i2 = (short)(i0 + lon + 1);
                    short i3 = (short)(i2 + 1);

                    indices.Add(i0); indices.Add(i2); indices.Add(i1);
                    indices.Add(i1); indices.Add(i2); indices.Add(i3);
                }
            }

            Vector3 finalPos = center + Vector3.Transform(relative, rot);
            effect.World = rot * Matrix.CreateTranslation(finalPos);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    verts.ToArray(),
                    0,
                    verts.Count,
                    indices.ToArray(),
                    0,
                    indices.Count / 3);
            }
        }

        private void DrawHemisphere(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                    Vector3 radii, Color color, Matrix rot, int latSegments = 6, int lonSegments = 12)
        {
            int lat = Math.Max(3, latSegments);
            int lon = Math.Max(6, lonSegments);

            var verts = new List<VertexPositionColor>((lat + 1) * (lon + 1));
            var indices = new List<short>(lat * lon * 6);

            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                float phi = v * MathHelper.PiOver2;
                float sinPhi = MathF.Sin(phi);
                float cosPhi = MathF.Cos(phi);

                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float theta = u * MathF.Tau;
                    float sinTheta = MathF.Sin(theta);
                    float cosTheta = MathF.Cos(theta);

                    Vector3 local = new(
                        radii.X * sinPhi * cosTheta,
                        radii.Y * cosPhi,
                        radii.Z * sinPhi * sinTheta);

                    verts.Add(new VertexPositionColor(local, color));
                }
            }

            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    short i0 = (short)(y * (lon + 1) + x);
                    short i1 = (short)(i0 + 1);
                    short i2 = (short)(i0 + lon + 1);
                    short i3 = (short)(i2 + 1);

                    indices.Add(i0); indices.Add(i2); indices.Add(i1);
                    indices.Add(i1); indices.Add(i2); indices.Add(i3);
                }
            }

            Vector3 finalPos = center + Vector3.Transform(relative, rot);
            effect.World = rot * Matrix.CreateTranslation(finalPos);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    verts.ToArray(),
                    0,
                    verts.Count,
                    indices.ToArray(),
                    0,
                    indices.Count / 3);
            }
        }

        private void DrawFrustumBetween(GraphicsDevice device, BasicEffect effect, Vector3 center,
                                        Vector3 start, Vector3 end, float startRadius, float endRadius,
                                        Color color, Matrix modelRot, int segments = 5)
        {
            Vector3 segment = end - start;
            float length = segment.Length();
            if (length < 0.0001f)
                return;

            int safeSegments = Math.Max(2, segments);
            float segmentHeight = length / safeSegments;
            Vector3 dir = segment / length;
            Matrix limbRot = CreateRotationFromUpTo(dir);
            Matrix partRot = limbRot * modelRot;

            for (int i = 0; i < safeSegments; i++)
            {
                float t = safeSegments == 1 ? 0f : i / (safeSegments - 1f);
                float radius = MathHelper.Lerp(startRadius, endRadius, t);
                Vector3 segPos = Vector3.Lerp(start, end, t);
                Vector3 segScale = new Vector3(radius, segmentHeight * 1.03f, radius);
                DrawBodyPart(device, effect, center, segPos, segScale, color * (0.9f + 0.1f * (1f - t)), modelRot, partRot);
            }
        }

        private void DrawForearmBetween(GraphicsDevice device, BasicEffect effect, Vector3 center,
                                        Vector3 start, Vector3 end, float elbowRadius, float wristRadius,
                                        Color color, Matrix modelRot, int segments = 6)
        {
            Vector3 segment = end - start;
            float length = segment.Length();
            if (length < 0.0001f)
                return;

            Vector3 dir = segment / length;
            Matrix limbRot = CreateRotationFromUpTo(dir);
            Matrix partRot = limbRot * modelRot;

            // Avant-bras low-poly optimisé : 2 prismes trapézoïdaux inversés.
            // 1) Prisme court (proche du coude), 2) prisme long (vers le poignet).
            const float shortPrismRatio = 0.38f;
            float shortLength = length * shortPrismRatio;
            float longLength = length - shortLength;

            Vector3 mid = Vector3.Lerp(start, end, shortPrismRatio);
            Vector3 shortCenter = Vector3.Lerp(start, mid, 0.5f);
            Vector3 longCenter = Vector3.Lerp(mid, end, 0.5f);

            float midRadius = MathHelper.Lerp(elbowRadius, wristRadius, shortPrismRatio);

            // Prisme court : plus massif côté coude.
            Vector3 shortScale = new Vector3(
                ((elbowRadius + midRadius) * 0.5f) * 0.96f,
                shortLength * 0.52f,
                ((elbowRadius + midRadius) * 0.5f) * 1.02f);
            DrawBodyPart(device, effect, center, shortCenter, shortScale, color, modelRot, partRot);

            // Prisme long : plus fin et légèrement aplati vers le poignet.
            Vector3 longScale = new Vector3(
                ((midRadius + wristRadius) * 0.5f) * 0.84f,
                longLength * 0.52f,
                ((midRadius + wristRadius) * 0.5f) * 0.94f);
            DrawBodyPart(device, effect, center, longCenter, longScale, color * 0.95f, modelRot, partRot);
        }

        private void DrawFootPyramid(GraphicsDevice device, BasicEffect effect, Vector3 center, Vector3 relative,
                                     float width, float height, float backDepth, float frontDepth,
                                     Color color, Matrix modelRot)
        {
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float back = backDepth * 0.5f;
            float front = frontDepth;
            float apexW = width * 0.16f;

            var verts = new VertexPositionColor[8]
            {
                new(new Vector3(-halfW, -halfH, -back), color),
                new(new Vector3(-halfW, -halfH, front), color),
                new(new Vector3( halfW, -halfH, front), color),
                new(new Vector3( halfW, -halfH, -back), color),
                new(new Vector3(-apexW, halfH, -back * 0.4f), color),
                new(new Vector3(-apexW, halfH, front * 0.25f), color),
                new(new Vector3( apexW, halfH, front * 0.25f), color),
                new(new Vector3( apexW, halfH, -back * 0.4f), color)
            };

            Vector3 finalPos = center + Vector3.Transform(relative, modelRot);
            effect.World = modelRot * Matrix.CreateTranslation(finalPos);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 8, cubeIndices, 0, 12);
            }
        }

        /// <summary>
        /// Dessine une unité avec son équipement visible
        /// </summary>
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

        /// <summary>
        /// Dessine une unité avec équipement visible (NOUVELLE VERSION)
        /// </summary>
        public void DrawWithEquipment(GraphicsDevice device, BasicEffect effect, Unit unit, float scale,
                                      float orientation = 0f, float legSwing = 0f, float armSwing = 0f,
                                      float bodyBob = 0f, float idleBob = 0f,
                                      Color? bodyColorOverride = null, bool drawEquipment = true)
        {
            Vector3 pos = unit.VisualPosition;
            Vector3 animatedPos = pos + new Vector3(0, bodyBob + idleBob, 0);
            Matrix rot = Matrix.CreateRotationY(orientation);

            // Déterminer le type d'unité
            UnitType type = GetUnitType(unit);
            Color bodyColor = bodyColorOverride ?? GetDefaultBodyColor(type);

            // Dimensions de base selon le type
            var dims = GetUnitDimensions(type, scale, unit.BodyType);

            bool hasWeapon = GetDisplayedWeapon(unit) != null;
            bool isAiming = unit.IsAiming || unit.IsFiring;
            bool hasPants = unit.EquippedPants != null;
            bool hasShirt = unit.EquippedShirt != null;

            // Dessiner le corps de base
            DrawUnitBody(device, effect, animatedPos, bodyColor, scale, type, rot, legSwing, armSwing, dims,
                hasWeapon, isAiming, unit.BodyType, hasPants, unit.DominantHand, hasShirt);

            // Afficher l'équipement porté (armes, armures, vêtements, poches)
            if (drawEquipment)
            {
                DrawEquipment(device, effect, animatedPos, unit, scale, rot, dims, legSwing, armSwing, isAiming, unit.DominantHand);
            }
        }

        private static Color GetDefaultBodyColor(UnitType type)
            => type switch
            {
                UnitType.Alien => new Color(110, 170, 110),
                UnitType.Zombie => new Color(120, 110, 95),
                UnitType.Heavy => new Color(110, 120, 130),
                UnitType.Scout => new Color(105, 125, 115),
                _ => new Color(115, 125, 110)
            };

        // ═══════════════════════════════════════════════════════════════════════
        // DESSIN D'ÉQUIPEMENT
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawEquipment(GraphicsDevice device, BasicEffect effect, Vector3 pos, Unit unit,
                                   float scale, Matrix rot, UnitDimensions dims, float legSwing, float armSwing, bool isAiming,
                                   Unit.Handedness dominantHand)
        {
            Item weaponToDraw = GetDisplayedWeapon(unit);

            // CASQUE
            if (unit.EquippedHelmet != null)
            {
                DrawHelmet(device, effect, pos, unit.EquippedHelmet, scale, rot, dims);
            }

            // GILET PARE-BALLES
            if (unit.EquippedArmor != null)
            {
                DrawArmor(device, effect, pos, unit.EquippedArmor, scale, rot, dims);
            }

            // BOUCLIER
            if (unit.EquippedShield != null)
            {
                DrawShield(device, effect, pos, unit.EquippedShield, scale, rot, dims);
            }

            // ARME
            if (weaponToDraw != null)
            {
                DrawWeapon(device, effect, pos, weaponToDraw, scale, rot, dims, isAiming, dominantHand);
            }

            // Lampe tactique 1x1 en aluminium (300 g), portée gérée dans le rendu de la scène.
            if (unit.Team == Team.Player && HasEquippedTacticalFlashlight(unit))
            {
                DrawTacticalFlashlight(device, effect, pos, rot, dims, dominantHand, isAiming, weaponToDraw != null);
            }

            // CHEMISE (sous le gilet)
            if (unit.EquippedShirt != null)
            {
                DrawShirt(device, effect, pos, unit.EquippedShirt, scale, rot, dims, armSwing,
                    weaponToDraw != null, isAiming, dominantHand);
            }

            // PANTALON + POCHES CARGO
            if (unit.EquippedPants != null)
            {
                DrawPants(device, effect, pos, unit.EquippedPants, unit.PantsInventory, scale, rot, dims, legSwing);
            }

            // ✅ NOUVEAU : GRENADES ÉQUIPÉES
            if (unit.Grenades != null && unit.Grenades.Count > 0)
            {
                DrawEquippedGrenades(device, effect, pos, unit.Grenades, scale, rot, dims);
            }
        }

        private static bool HasEquippedTacticalFlashlight(Unit unit)
        {
            bool rightOn = string.Equals(unit?.EquippedRightHandFlashlight?.Data?.Name, "Lampe tactique aluminium", StringComparison.OrdinalIgnoreCase)
                && unit.IsRightHandFlashlightOn;
            bool leftOn = string.Equals(unit?.EquippedLeftHandFlashlight?.Data?.Name, "Lampe tactique aluminium", StringComparison.OrdinalIgnoreCase)
                && unit.IsLeftHandFlashlightOn;
            return rightOn || leftOn;
        }

        private void DrawTacticalFlashlight(GraphicsDevice device, BasicEffect effect, Vector3 pos,
                                            Matrix rot, UnitDimensions dims, Unit.Handedness dominantHand,
                                            bool isAiming, bool hasWeapon)
        {
            float massFactor = MathHelper.Clamp(TacticalFlashlightWeightGrams / 300f, 0.5f, 1.5f);
            float handSign = GetHandSideSign(dominantHand);
            Vector3 handPos = hasWeapon
                ? new Vector3(handSign * dims.tw * 0.48f, dims.ll + dims.th * (isAiming ? 0.75f : 0.68f), dims.td * (isAiming ? 0.94f : 0.6f))
                : new Vector3(handSign * dims.tw * 0.62f, dims.ll + dims.th * 0.68f, dims.td * 0.95f);

            // Corps en aluminium (1x1, compacte) orienté vers l'avant.
            Vector3 bodyScale = new Vector3(dims.lw * 0.28f, dims.lw * 0.28f, dims.lw * 0.62f * massFactor);
            DrawBodyPart(device, effect, pos, handPos, bodyScale, new Color(150, 155, 165), rot);

            // Lentille avant lumineuse.
            Vector3 lensPos = handPos + new Vector3(0f, 0f, bodyScale.Z * 0.58f);
            Vector3 lensScale = new Vector3(bodyScale.X * 0.85f, bodyScale.Y * 0.85f, bodyScale.Z * 0.22f);
            DrawBodyPart(device, effect, pos, lensPos, lensScale, new Color(255, 244, 190), rot);
        }

        private void DrawLimbAlignedBand(GraphicsDevice device, BasicEffect effect, Vector3 center,
                                         Vector3 start, Vector3 end, float normalizedPosition,
                                         Vector3 bandScale, Color color, Matrix modelRot)
        {
            Vector3 limb = end - start;
            if (limb.LengthSquared() < 1e-5f)
                return;

            Vector3 bandPos = Vector3.Lerp(start, end, MathHelper.Clamp(normalizedPosition, 0f, 1f));
            Matrix limbRot = CreateRotationFromUpTo(limb);
            Matrix partRot = limbRot * modelRot;
            DrawBodyPart(device, effect, center, bandPos, bandScale, color, modelRot, partRot);
        }


        private static float GetShoulderSlopeScale()
            => HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderSlopeScale, 0.7f, 1.3f);

        private static float GetShoulderVerticalOffset(UnitDimensions dims, float shoulderX)
        {
            float slope = (GetShoulderSlopeScale() - 1f) * 0.14f;
            float maxShoulderX = Math.Max(dims.tw * 0.62f, 0.0001f);
            float sideFactor = MathHelper.Clamp(Math.Abs(shoulderX) / maxShoulderX, 0f, 1f);
            return -dims.th * slope * sideFactor;
        }

        private static void ComputeSwingingArmPoints(UnitDimensions dims, float phase, float shoulderX,
                                                      float shoulderHeight, float bendBias,
                                                      out Vector3 shoulder, out Vector3 elbow, out Vector3 wrist)
        {
            float normalizedPhase = MathHelper.Clamp(phase / 0.3f, -1f, 1f);
            float forward = normalizedPhase;
            float backward = -normalizedPhase;

            float shoulderYOffset = GetShoulderVerticalOffset(dims, shoulderX);

            shoulder = new Vector3(shoulderX, dims.ll + dims.th * shoulderHeight + shoulderYOffset, -forward * dims.al * 0.08f);
            elbow = new Vector3(
                shoulderX,
                dims.ll + dims.th * (shoulderHeight - 0.3f) + shoulderYOffset + Math.Max(0f, forward) * dims.al * 0.15f,
                forward * dims.al * 0.34f + bendBias * dims.al * 0.12f);

            wrist = elbow + new Vector3(
                0f,
                -dims.al * (0.32f + Math.Max(0f, backward) * 0.1f),
                forward * dims.al * 0.28f + Math.Max(0f, backward) * dims.al * 0.12f + bendBias * dims.al * 0.08f);
        }

        private static void ComputeForwardDominantArmPoints(UnitDimensions dims, Unit.Handedness dominantHand,
                                                             out Vector3 shoulder, out Vector3 elbow, out Vector3 wrist)
        {
            float dominantSign = GetHandSideSign(dominantHand);
            float shoulderHeightScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderHeightScale, 0.7f, 1.3f);
            float shoulderWidthScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderWidthScale, 0.7f, 1.3f);
            float shoulderX = dominantSign * dims.tw * 0.58f * shoulderWidthScale;
            float shoulderY = dims.ll + dims.th * (0.86f * shoulderHeightScale) + GetShoulderVerticalOffset(dims, shoulderX);

            shoulder = new Vector3(shoulderX, shoulderY, 0f);
            elbow = new Vector3(shoulderX, shoulderY - dims.al * 0.06f, dims.al * 0.48f);
            wrist = new Vector3(shoulderX, shoulderY - dims.al * 0.1f, dims.al * 0.96f);
        }

        private static void ComputeRunningLegPoints(UnitDimensions dims, float phase, float hipX,
                                                     out Vector3 hip, out Vector3 knee, out Vector3 ankle)
        {
            float normalizedPhase = MathHelper.Clamp(phase / 0.42f, -1f, 1f);
            float forward = normalizedPhase;
            float backward = -normalizedPhase;

            float arcSign = hipX < 0f ? -1f : 1f;
            float forwardLift = Math.Max(0f, forward);
            float trailingCompression = Math.Max(0f, backward);

            hip = new Vector3(
                hipX,
                dims.ll + dims.th * 0.03f,
                -forward * dims.ll * 0.06f + arcSign * dims.lw * 0.12f);
            knee = new Vector3(
                hipX * 0.88f,
                dims.ll * (0.49f + forwardLift * 0.2f),
                forward * dims.ll * 0.33f + dims.lw * 0.08f);
            ankle = knee + new Vector3(
                arcSign * dims.lw * 0.05f,
                -dims.ll * (0.42f + trailingCompression * 0.14f),
                -dims.ll * 0.05f + forward * dims.ll * 0.16f + trailingCompression * dims.ll * 0.26f);
        }

        private Item GetDisplayedWeapon(Unit unit)
        {
            if (unit.EquippedWeapon != null)
            {
                return unit.EquippedWeapon;
            }

            if (unit.WeaponData == null)
            {
                return null;
            }

            string weaponName = string.IsNullOrWhiteSpace(unit.Weapon)
                ? unit.WeaponData.Name
                : unit.Weapon;

            return new Item(new ItemData(weaponName, ItemType.Weapon, unit.WeaponData), Point.Zero);
        }

        private void DrawHelmet(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item helmet,
                                float scale, Matrix rot, UnitDimensions dims)
        {
            Color helmetColor = GetArmorColor(helmet.Data.Name);

            // La forme de la tête a évolué ; on recale tous les casques
            // sur un repère commun dérivé du crâne réel.
            float headCenterY = dims.ll + dims.th + dims.head * 0.6f;
            float headRadius = dims.head * 0.52f;

            // ✅ AMÉLIORÉ : Forme plus réaliste du casque PASGT
            if (helmet.Data.Name.Contains("PASGT"))
            {
                // Corps principal du casque : demi-sphère qui recouvre le dessus du crâne.
                Vector3 shellPos = new Vector3(0, headCenterY + headRadius * 0.32f, 0);
                Vector3 shellRadii = new Vector3(dims.head * 0.64f, dims.head * 0.58f, dims.head * 0.7f);
                DrawHemisphere(device, effect, pos, shellPos, shellRadii, helmetColor, rot);

                // Bord avant du casque (visière/front)
                Vector3 frontRimPos = new Vector3(0, headCenterY - headRadius * 0.18f, dims.head * 0.76f);
                Vector3 frontRimScale = new Vector3(dims.head * 1.3f, dims.head * 0.15f, dims.head * 0.2f);
                DrawBodyPart(device, effect, pos, frontRimPos, frontRimScale, helmetColor * 0.85f, rot);

                // Bord arrière du casque (protège-nuque étendu)
                Vector3 backRimPos = new Vector3(0, headCenterY - headRadius * 0.5f, -dims.head * 0.76f);
                Vector3 backRimScale = new Vector3(dims.head * 1.2f, dims.head * 0.2f, dims.head * 0.3f);
                DrawBodyPart(device, effect, pos, backRimPos, backRimScale, helmetColor * 0.85f, rot);

                // Bords latéraux (protection des oreilles)
                Vector3 leftEarPos = new Vector3(-dims.head * 0.7f, headCenterY - headRadius * 0.15f, 0);
                Vector3 earScale = new Vector3(dims.head * 0.15f, dims.head * 0.4f, dims.head * 0.5f);
                DrawBodyPart(device, effect, pos, leftEarPos, earScale, helmetColor * 0.9f, rot);

                Vector3 rightEarPos = new Vector3(dims.head * 0.7f, headCenterY - headRadius * 0.15f, 0);
                DrawBodyPart(device, effect, pos, rightEarPos, earScale, helmetColor * 0.9f, rot);

                // Rivet/bouton visible (comme sur la photo)
                Vector3 rivetPos = new Vector3(0, headCenterY + headRadius * 0.7f, dims.head * 0.52f);
                Vector3 rivetScale = new Vector3(dims.head * 0.08f, dims.head * 0.08f, dims.head * 0.08f);
                DrawBodyPart(device, effect, pos, rivetPos, rivetScale, new Color(60, 60, 60), rot);

                // Rails de montage sur les côtés (pour NVG ou accessoires)
                Vector3 leftRailPos = new Vector3(-dims.head * 0.6f, headCenterY + headRadius * 0.34f, dims.head * 0.45f);
                Vector3 railScale = new Vector3(dims.head * 0.1f, dims.head * 0.08f, dims.head * 0.15f);
                DrawBodyPart(device, effect, pos, leftRailPos, railScale, new Color(40, 40, 40), rot);

                Vector3 rightRailPos = new Vector3(dims.head * 0.6f, headCenterY + headRadius * 0.34f, dims.head * 0.45f);
                DrawBodyPart(device, effect, pos, rightRailPos, railScale, new Color(40, 40, 40), rot);
            }
            // Casques ACH/ECH/MICH (modernes)
            else if (helmet.Data.Name.Contains("ACH") || helmet.Data.Name.Contains("ECH") || helmet.Data.Name.Contains("MICH"))
            {
                // Coque principale : demi-sphère moderne haute.
                Vector3 shellPos = new Vector3(0, headCenterY + headRadius * 0.35f, 0);
                Vector3 shellRadii = new Vector3(dims.head * 0.62f, dims.head * 0.62f, dims.head * 0.64f);
                DrawHemisphere(device, effect, pos, shellPos, shellRadii, helmetColor, rot);

                // Visière NVG mount (caractéristique des casques modernes)
                Vector3 mountPos = new Vector3(0, headCenterY + headRadius * 0.34f, dims.head * 0.66f);
                Vector3 mountScale = new Vector3(dims.head * 0.3f, dims.head * 0.15f, dims.head * 0.1f);
                DrawBodyPart(device, effect, pos, mountPos, mountScale, new Color(50, 50, 50), rot);

                // Rail picatinny sur le devant
                Vector3 railPos = new Vector3(0, headCenterY + headRadius * 0.5f, dims.head * 0.6f);
                Vector3 railScale = new Vector3(dims.head * 0.25f, dims.head * 0.08f, dims.head * 0.08f);
                DrawBodyPart(device, effect, pos, railPos, railScale, new Color(30, 30, 30), rot);

                // Pads latéraux (protection d'impact)
                Vector3 leftPadPos = new Vector3(-dims.head * 0.65f, headCenterY + headRadius * 0.06f, 0);
                Vector3 padScale = new Vector3(dims.head * 0.12f, dims.head * 0.3f, dims.head * 0.3f);
                DrawBodyPart(device, effect, pos, leftPadPos, padScale, helmetColor * 0.7f, rot);

                Vector3 rightPadPos = new Vector3(dims.head * 0.65f, headCenterY + headRadius * 0.06f, 0);
                DrawBodyPart(device, effect, pos, rightPadPos, padScale, helmetColor * 0.7f, rot);
            }
            // Casque basique (fallback)
            else
            {
                Vector3 shellPos = new Vector3(0, headCenterY + headRadius * 0.3f, 0);
                Vector3 shellRadii = new Vector3(dims.head * 0.62f, dims.head * 0.58f, dims.head * 0.62f);
                DrawHemisphere(device, effect, pos, shellPos, shellRadii, helmetColor, rot);
            }
        }

        private void DrawArmor(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item armor,
                               float scale, Matrix rot, UnitDimensions dims)
        {
            Color armorColor = GetArmorColor(armor.Data.Name);

            // Gilet pare-balles sur le torse
            Vector3 armorPos = new Vector3(0, dims.ll + dims.th * 0.5f, 0);
            Vector3 armorScale = new Vector3(dims.tw * 1.15f, dims.th * 1.05f, dims.td * 1.2f);

            DrawBodyPart(device, effect, pos, armorPos, armorScale, armorColor * 0.9f, rot);

            // Plaques SAPI si OTV+SAPI
            if (armor.Data.Name.Contains("SAPI"))
            {
                Vector3 frontPlate = new Vector3(0, dims.ll + dims.th * 0.5f, dims.td * 0.65f);
                Vector3 plateScale = new Vector3(dims.tw * 0.7f, dims.th * 0.7f, dims.td * 0.08f);
                DrawBodyPart(device, effect, pos, frontPlate, plateScale, new Color(40, 40, 40), rot);
            }

            // Poches et détails
            for (int i = 0; i < 3; i++)
            {
                Vector3 pouchPos = new Vector3(
                    -dims.tw * 0.4f + i * dims.tw * 0.4f,
                    dims.ll + dims.th * 0.3f,
                    dims.td * 0.7f
                );
                Vector3 pouchScale = new Vector3(dims.tw * 0.2f, dims.th * 0.15f, dims.td * 0.2f);
                DrawBodyPart(device, effect, pos, pouchPos, pouchScale, armorColor * 0.7f, rot);
            }
        }

        private void DrawShield(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item shield,
                                float scale, Matrix rot, UnitDimensions dims)
        {
            Color shieldColor = new Color(60, 60, 80);

            // Bouclier sur le bras gauche
            Vector3 shieldPos = new Vector3(-dims.tw * 0.7f, dims.ll + dims.th * 0.6f, 0);
            Vector3 shieldScale = new Vector3(dims.td * 0.3f, dims.th * 0.8f, dims.tw * 0.7f);

            DrawBodyPart(device, effect, pos, shieldPos, shieldScale, shieldColor, rot);

            // Poignée visible
            Vector3 handlePos = new Vector3(-dims.tw * 0.7f, dims.ll + dims.th * 0.6f, -dims.tw * 0.1f);
            Vector3 handleScale = new Vector3(dims.lw * 0.5f, dims.th * 0.4f, dims.lw * 0.3f);
            DrawBodyPart(device, effect, pos, handlePos, handleScale, new Color(100, 70, 40), rot);
        }

        private static float GetHandSideSign(Unit.Handedness dominantHand)
            => dominantHand == Unit.Handedness.Left ? 1f : -1f;

        private void DrawWeapon(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item weapon,
                                float scale, Matrix rot, UnitDimensions dims, bool isAiming,
                                Unit.Handedness dominantHand)
        {
            Color weaponColor = GetWeaponColor(weapon.Data.Name);
            WeaponType weaponType = GetWeaponType(weapon.Data.Name);
            float handSign = GetHandSideSign(dominantHand);

            // Position calée entre les deux mains (pose articulée)
            Vector3 weaponPos = isAiming
                ? new Vector3(handSign * dims.tw * 0.05f, dims.ll + dims.th * 0.78f, dims.td * 0.95f)
                : new Vector3(handSign * dims.tw * 0.12f, dims.ll + dims.th * 0.66f, dims.td * 0.52f);
            Vector3 weaponScale = GetWeaponScale(weaponType, dims);

            // Corps de l'arme
            DrawBodyPart(device, effect, pos, weaponPos, weaponScale, weaponColor, rot);

            // Crosse (si rifle ou sniper)
            if (weaponType == WeaponType.Rifle || weaponType == WeaponType.Sniper)
            {
                Vector3 stockPos = new Vector3(
                    weaponPos.X - handSign * dims.tw * 0.2f,
                    weaponPos.Y + dims.lw * 0.1f,
                    weaponPos.Z - weaponScale.Z * 0.35f
                );
                Vector3 stockScale = new Vector3(weaponScale.X * 0.8f, weaponScale.Y * 0.5f, weaponScale.Z * 0.4f);
                DrawBodyPart(device, effect, pos, stockPos, stockScale, new Color(80, 50, 30), rot);
            }

            // Canon (partie avant)
            Vector3 barrelPos = new Vector3(
                weaponPos.X + handSign * dims.tw * 0.18f,
                weaponPos.Y,
                weaponPos.Z + weaponScale.Z * 0.62f
            );
            Vector3 barrelScale = new Vector3(weaponScale.X * 0.5f, weaponScale.Y * 0.5f, weaponScale.Z * 0.3f);
            DrawBodyPart(device, effect, pos, barrelPos, barrelScale, new Color(40, 40, 40), rot);
        }

        private void DrawShirt(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item shirt,
                               float scale, Matrix rot, UnitDimensions dims, float armSwing,
                               bool hasWeapon, bool isAiming, Unit.Handedness dominantHand)
        {
            Color shirtColor = new Color(100, 120, 80); // Couleur militaire

            // Chemise sous le gilet (légèrement visible)
            Vector3 shirtPos = new Vector3(0, dims.ll + dims.th * 0.5f, 0);
            Vector3 shirtScale = new Vector3(dims.tw * 0.95f, dims.th * 0.95f, dims.td * 0.95f);

            DrawBodyPart(device, effect, pos, shirtPos, shirtScale, shirtColor * 0.8f, rot);

            // Manches alignées à la pose active pour éviter les bras fantômes / chevauchement visuel.
            Vector3 leftShoulder;
            Vector3 leftElbow;
            Vector3 leftWrist;
            Vector3 rightShoulder;
            Vector3 rightElbow;
            Vector3 rightWrist;

            if (!hasWeapon)
            {
                ComputeForwardDominantArmPoints(dims, dominantHand,
                    out Vector3 dominantShoulder, out Vector3 dominantElbow, out Vector3 dominantWrist);

                if (dominantHand == Unit.Handedness.Left)
                {
                    leftShoulder = dominantShoulder;
                    leftElbow = dominantElbow;
                    leftWrist = dominantWrist;
                    ComputeSwingingArmPoints(dims, -armSwing, dims.tw * 0.6f, 0.9f, 0f,
                        out rightShoulder, out rightElbow, out rightWrist);
                }
                else
                {
                    rightShoulder = dominantShoulder;
                    rightElbow = dominantElbow;
                    rightWrist = dominantWrist;
                    ComputeSwingingArmPoints(dims, armSwing, -dims.tw * 0.6f, 0.9f, 0f,
                        out leftShoulder, out leftElbow, out leftWrist);
                }
            }
            else
            {
                float aimingDampen = isAiming ? 0.2f : 1f;
                ComputeSwingingArmPoints(dims, armSwing * aimingDampen, -dims.tw * 0.6f, 0.9f, 0f,
                    out leftShoulder, out leftElbow, out leftWrist);
                ComputeSwingingArmPoints(dims, -armSwing * aimingDampen, dims.tw * 0.6f, 0.9f, 0f,
                    out rightShoulder, out rightElbow, out rightWrist);
            }

            // Ajoute un vrai volume d'épaule au-dessus des manches pour éviter
            // l'impression d'épaule « absente » quand une chemise est équipée.
            float shoulderCapRadius = dims.lw * 0.36f;
            DrawSphere(device, effect, pos, leftShoulder, shoulderCapRadius, shirtColor * 0.95f, rot);
            DrawSphere(device, effect, pos, rightShoulder, shoulderCapRadius, shirtColor * 0.95f, rot);

            DrawFrustumBetween(device, effect, pos, leftShoulder, leftElbow,
                dims.lw * 0.62f, dims.lw * 0.56f, shirtColor * 0.9f, rot, 5);
            DrawFrustumBetween(device, effect, pos, leftElbow, leftWrist,
                dims.lw * 0.56f, dims.lw * 0.5f, shirtColor * 0.84f, rot, 5);

            DrawFrustumBetween(device, effect, pos, rightShoulder, rightElbow,
                dims.lw * 0.62f, dims.lw * 0.56f, shirtColor * 0.9f, rot, 5);
            DrawFrustumBetween(device, effect, pos, rightElbow, rightWrist,
                dims.lw * 0.56f, dims.lw * 0.5f, shirtColor * 0.84f, rot, 5);
        }


        private void DrawPants(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item pants,
                               List<Item> pocketItems, float scale, Matrix rot, UnitDimensions dims, float legSwing)
        {
            string pantsName = pants?.Data?.Name ?? string.Empty;

            Color baseColor = new Color(45, 75, 130); // Jeans par défaut
            Color legA = baseColor * 0.94f;
            Color legB = baseColor * 0.8f;
            Color pocketColor = baseColor * 0.75f;
            float legWidthFactor = 0.68f;
            float hipDepthFactor = 0.92f;
            bool showKneePads = false;

            if (pantsName.Contains("Cargo", StringComparison.OrdinalIgnoreCase))
            {
                // Cargo tactique : silhouette plus utilitaire + genouillères
                baseColor = new Color(72, 88, 62);
                legA = baseColor * 0.96f;
                legB = baseColor * 0.82f;
                pocketColor = new Color(58, 74, 52);
                legWidthFactor = 0.74f;
                hipDepthFactor = 0.98f;
                showKneePads = true;
            }
            else if (pantsName.Contains("Travail", StringComparison.OrdinalIgnoreCase))
            {
                // Pantalon de travail : ton sombre + coupe un peu plus épaisse
                baseColor = new Color(70, 70, 74);
                legA = baseColor * 0.95f;
                legB = baseColor * 0.84f;
                pocketColor = new Color(58, 58, 62);
                legWidthFactor = 0.72f;
                hipDepthFactor = 0.95f;
            }

            Vector3 hipPos = new Vector3(0, dims.ll * 1.02f, 0);
            Vector3 hipScale = new Vector3(dims.tw * 0.95f, dims.ll * 0.55f, dims.td * hipDepthFactor);
            DrawBodyPart(device, effect, pos, hipPos, hipScale, baseColor, rot);

            // Volume arrière du pantalon : deux formes arrondies pour mieux marquer les fessiers.
            float gluteRadius = dims.tw * (pantsName.Contains("Cargo", StringComparison.OrdinalIgnoreCase) ? 0.26f : 0.24f);
            float gluteDepth = dims.td * (pantsName.Contains("Cargo", StringComparison.OrdinalIgnoreCase) ? 0.34f : 0.32f);
            float gluteHeight = dims.ll * 0.42f;

            DrawRoundedHead(device, effect, pos,
                new Vector3(-dims.tw * 0.24f, gluteHeight, -gluteDepth),
                gluteRadius,
                baseColor * 0.9f,
                rot);
            DrawRoundedHead(device, effect, pos,
                new Vector3(dims.tw * 0.24f, gluteHeight, -gluteDepth),
                gluteRadius,
                baseColor * 0.9f,
                rot);

            UnitDimensions pantsDims = dims;
            pantsDims.lw *= legWidthFactor;

            DrawRunningLegPair(device, effect, pos, rot, pantsDims, legSwing, 0.3f, 0.88f, legA, legB);

            int visiblePockets = Math.Min(Math.Max(pocketItems?.Count ?? 0, 0), 4);
            float pocketSize = dims.lw * 0.45f;

            Vector3[] pocketOffsets =
            {
                new Vector3(0f, dims.ll * 1.16f, dims.td * 0.58f),      // avant centre
                new Vector3(dims.tw * 0.6f, dims.ll * 0.98f, 0f),       // côté droit
                new Vector3(0f, dims.ll * 0.78f, -dims.td * 0.58f),     // arrière centre
                new Vector3(-dims.tw * 0.6f, dims.ll * 0.98f, 0f)       // côté gauche
            };

            for (int i = 0; i < visiblePockets; i++)
            {
                Vector3 pocketScale = new Vector3(pocketSize * 0.9f, pocketSize * 0.55f, pocketSize * 0.25f);
                DrawBodyPart(device, effect, pos, pocketOffsets[i], pocketScale, pocketColor, rot);
            }

            float hipCornerOffset = dims.tw * 0.06f;
            float leftHipX = -dims.tw * 0.3f - hipCornerOffset;
            float rightHipX = dims.tw * 0.3f + hipCornerOffset;
            ComputeRunningLegPoints(pantsDims, legSwing, leftHipX, out _, out Vector3 leftKnee, out _);
            ComputeRunningLegPoints(pantsDims, -legSwing, rightHipX, out _, out Vector3 rightKnee, out _);

            if (showKneePads)
            {
                Color padColor = new Color(40, 48, 36);
                Vector3 padScale = new Vector3(dims.lw * 0.82f, dims.ll * 0.2f, dims.lw * 0.52f);

                DrawBodyPart(device, effect, pos, leftKnee + new Vector3(0f, 0f, dims.lw * 0.16f), padScale, padColor, rot);
                DrawBodyPart(device, effect, pos, rightKnee + new Vector3(0f, 0f, dims.lw * 0.16f), padScale, padColor, rot);
            }

            // Poches cargo latérales orientées selon l'axe de chaque cuisse
            if (visiblePockets > 0)
            {
                ComputeRunningLegPoints(pantsDims, legSwing, leftHipX, out Vector3 leftHip, out Vector3 leftLegKnee, out _);
                ComputeRunningLegPoints(pantsDims, -legSwing, rightHipX, out Vector3 rightHip, out Vector3 rightLegKnee, out _);

                Vector3 cargoPocketScale = new Vector3(pocketSize * 0.8f, pocketSize * 0.5f, pocketSize * 0.22f);
                DrawLimbAlignedBand(device, effect, pos, leftHip, leftLegKnee, 0.55f, cargoPocketScale, pocketColor * 0.95f, rot);
                if (visiblePockets > 1)
                    DrawLimbAlignedBand(device, effect, pos, rightHip, rightLegKnee, 0.55f, cargoPocketScale, pocketColor * 0.95f, rot);
            }
        }

        /// <summary>
        /// Dessine les grenades équipées sur le gilet tactique
        /// </summary>
        private void DrawEquippedGrenades(GraphicsDevice device, BasicEffect effect, Vector3 pos,
                                          List<GrenadeData> grenades, float scale, Matrix rot, UnitDimensions dims)
        {
            // Position de base sur le gilet (côté gauche pour grenade 1, droite pour autres)
            float grenadeSize = dims.lw * 0.35f; // Taille des grenades
            float yPos = dims.ll + dims.th * 0.4f; // Hauteur sur le gilet (mi-torse)

            for (int i = 0; i < Math.Min(grenades.Count, 3); i++) // Max 3 grenades visibles
            {
                GrenadeData grenade = grenades[i];
                Color grenadeColor = GrenadeDatabase.GetGrenadeColor(grenade.Type);

                // Position selon l'index
                Vector3 grenadePos = i switch
                {
                    0 => new Vector3(-dims.tw * 0.5f, yPos, dims.td * 0.6f), // Gauche haut
                    1 => new Vector3(dims.tw * 0.5f, yPos, dims.td * 0.6f),  // Droite haut
                    2 => new Vector3(0, yPos - dims.th * 0.15f, dims.td * 0.6f), // Centre bas
                    _ => Vector3.Zero
                };

                // Corps de la grenade (cylindrique approximé avec un cube allongé)
                Vector3 grenadeScale = new Vector3(grenadeSize * 0.6f, grenadeSize * 1.2f, grenadeSize * 0.6f);
                DrawBodyPart(device, effect, pos, grenadePos, grenadeScale, grenadeColor, rot);

                // Goupille/poignée (petit détail en haut)
                Vector3 pinPos = grenadePos;
                pinPos.Y += grenadeSize * 0.7f;
                Vector3 pinScale = new Vector3(grenadeSize * 0.3f, grenadeSize * 0.2f, grenadeSize * 0.3f);
                DrawBodyPart(device, effect, pos, pinPos, pinScale, new Color(200, 200, 50), rot); // Jaune/doré

                // Anneau de sécurité (petit cube pour représenter l'anneau)
                Vector3 ringPos = pinPos;
                ringPos.Z += grenadeSize * 0.3f;
                Vector3 ringScale = new Vector3(grenadeSize * 0.15f, grenadeSize * 0.15f, grenadeSize * 0.1f);
                DrawBodyPart(device, effect, pos, ringPos, ringScale, new Color(180, 180, 180), rot); // Métal
            }

        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        private enum WeaponType { Rifle, Sniper, SMG, Shotgun, Pistol }

        private WeaponType GetWeaponType(string weaponName)
        {
            if (weaponName.Contains("Sniper")) return WeaponType.Sniper;
            if (weaponName.Contains("SMG")) return WeaponType.SMG;
            if (weaponName.Contains("Shotgun")) return WeaponType.Shotgun;
            if (weaponName.Contains("Pistol")) return WeaponType.Pistol;
            return WeaponType.Rifle;
        }

        private Vector3 GetWeaponScale(WeaponType type, UnitDimensions dims)
        {
            return type switch
            {
                WeaponType.Rifle => new Vector3(dims.lw * 0.6f, dims.lw * 0.6f, dims.al * 1.2f),
                WeaponType.Sniper => new Vector3(dims.lw * 0.5f, dims.lw * 0.5f, dims.al * 1.6f),
                WeaponType.SMG => new Vector3(dims.lw * 0.5f, dims.lw * 0.5f, dims.al * 0.8f),
                WeaponType.Shotgun => new Vector3(dims.lw * 0.7f, dims.lw * 0.7f, dims.al * 1.1f),
                WeaponType.Pistol => new Vector3(dims.lw * 0.4f, dims.lw * 0.4f, dims.al * 0.5f),
                _ => new Vector3(dims.lw * 0.6f, dims.lw * 0.6f, dims.al * 1.0f)
            };
        }

        private Color GetWeaponColor(string weaponName)
        {
            if (weaponName.Contains("Plasma")) return new Color(0, 255, 150);
            return new Color(40, 40, 40); // Métal noir
        }

        private Color GetArmorColor(string armorName)
        {
            if (armorName.Contains("PASGT")) return new Color(80, 100, 70); // Vert militaire
            if (armorName.Contains("ACH") || armorName.Contains("ECH")) return new Color(120, 120, 100); // Tan
            if (armorName.Contains("MICH")) return new Color(60, 80, 60);
            if (armorName.Contains("MTV") || armorName.Contains("IOTV")) return new Color(90, 90, 70);
            return new Color(70, 70, 70); // Gris par défaut
        }

        private UnitType GetUnitType(Unit unit)
        {
            if (unit.Class == "Heavy") return UnitType.Heavy;
            if (unit.Class == "Scout") return UnitType.Scout;
            if (unit.Class == "Undead") return UnitType.Zombie;
            if (unit.Team == Team.Enemy && !unit.Class.Contains("Assault")) return UnitType.Alien;
            return UnitType.Soldier;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DIMENSIONS DES UNITÉS
        // ═══════════════════════════════════════════════════════════════════════

        private struct UnitDimensions
        {
            public float head, tw, th, td, lw, al, ll;
        }

        private UnitDimensions GetUnitDimensions(UnitType type, float scale, Unit.HumanBodyType bodyType = Unit.HumanBodyType.Masculine)
        {
            UnitDimensions baseDimensions = type switch
            {
                UnitType.Soldier => new UnitDimensions
                {
                    head = 0.28f * scale,
                    tw = 0.38f * scale,
                    th = 0.70f * scale,
                    td = 0.28f * scale,
                    lw = 0.13f * scale,
                    al = 0.55f * scale,
                    ll = 1.00f * scale
                },
                UnitType.Alien => new UnitDimensions
                {
                    head = 0.40f * scale,
                    tw = 0.32f * scale,
                    th = 0.65f * scale,
                    td = 0.22f * scale,
                    lw = 0.1f * scale,
                    al = 0.65f * scale,
                    ll = 1.05f * scale
                },
                UnitType.Zombie => new UnitDimensions
                {
                    head = 0.26f * scale,
                    tw = 0.36f * scale,
                    th = 0.65f * scale,
                    td = 0.26f * scale,
                    lw = 0.12f * scale,
                    al = 0.58f * scale,
                    ll = 0.95f * scale
                },
                UnitType.Heavy => new UnitDimensions
                {
                    head = 0.26f * scale,
                    tw = 0.55f * scale,
                    th = 0.75f * scale,
                    td = 0.40f * scale,
                    lw = 0.18f * scale,
                    al = 0.52f * scale,
                    ll = 0.95f * scale
                },
                UnitType.Scout => new UnitDimensions
                {
                    head = 0.26f * scale,
                    tw = 0.30f * scale,
                    th = 0.68f * scale,
                    td = 0.22f * scale,
                    lw = 0.10f * scale,
                    al = 0.58f * scale,
                    ll = 1.05f * scale
                },
                _ => new UnitDimensions
                {
                    head = 0.25f * scale,
                    tw = 0.35f * scale,
                    th = 0.5f * scale,
                    td = 0.25f * scale,
                    lw = 0.12f * scale,
                    al = 0.45f * scale,
                    ll = 0.55f * scale
                }
            };

            if (type == UnitType.Soldier)
            {
                baseDimensions.head *= HumanBodyMorphSettings.HeadScale;
                baseDimensions.tw *= HumanBodyMorphSettings.TorsoWidthScale;
                baseDimensions.th *= HumanBodyMorphSettings.TorsoHeightScale;
                baseDimensions.td *= HumanBodyMorphSettings.TorsoDepthScale;
                baseDimensions.lw *= HumanBodyMorphSettings.LimbWidthScale;
                baseDimensions.al *= HumanBodyMorphSettings.ArmLengthScale;
                baseDimensions.ll *= HumanBodyMorphSettings.LegLengthScale;
            }

            if (type == UnitType.Soldier && bodyType == Unit.HumanBodyType.Feminine)
            {
                baseDimensions.head *= HumanBodyMorphSettings.FeminineHeadScale;
                baseDimensions.tw *= HumanBodyMorphSettings.FeminineTorsoWidthScale;
                baseDimensions.td *= HumanBodyMorphSettings.FeminineTorsoDepthScale;
                baseDimensions.lw *= HumanBodyMorphSettings.FeminineLimbWidthScale;
                baseDimensions.th *= HumanBodyMorphSettings.FeminineTorsoHeightScale;
                baseDimensions.ll *= HumanBodyMorphSettings.FeminineLegLengthScale;
            }

            return baseDimensions;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DESSIN DU CORPS (méthodes originales)
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawUnitBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s,
                                  UnitType type, Matrix r, float l, float a, UnitDimensions dims,
                                  bool hasWeapon = false, bool isAiming = false,
                                  Unit.HumanBodyType bodyType = Unit.HumanBodyType.Masculine,
                                  bool hasPants = false,
                                  Unit.Handedness dominantHand = Unit.Handedness.Right,
                                  bool hasShirt = false)
        {
            bool useTPose = false;
            bool useCustomArmPose = (hasWeapon && isAiming) || !hasWeapon;
            bool drawDefaultArms = !useCustomArmPose && !hasShirt;

            switch (type)
            {
                case UnitType.Soldier: DrawSoldierBody(d, e, p, c, s, r, l, a, dims, bodyType, hasPants, useTPose, drawDefaultArms); break;
                case UnitType.Alien: DrawAlienBody(d, e, p, c, s, r, l, a, dims, useTPose, drawDefaultArms); break;
                case UnitType.Zombie: DrawZombieBody(d, e, p, c, s, r, l, a, dims, useTPose, drawDefaultArms); break;
                case UnitType.Heavy: DrawHeavyBody(d, e, p, c, s, r, l, a, dims, useTPose, drawDefaultArms); break;
                case UnitType.Scout: DrawScoutBody(d, e, p, c, s, r, l, a, dims, useTPose, drawDefaultArms); break;
            }

            DrawSkeletonJoints(d, e, p, dims, r, c, l, a);

            // Ne verrouille la pose "prise d'arme" que pendant la visée/tir,
            // sinon on laisse l'animation de balancement des bras visible en déplacement.
            if (!hasShirt && hasWeapon && isAiming)
            {
                DrawWeaponGripPose(d, e, p, dims, r, c, isAiming, dominantHand);
            }
            else if (!hasShirt && !hasWeapon)
            {
                DrawUnarmedDominantArmForwardPose(d, e, p, dims, r, c, dominantHand);
            }
        }

        private void DrawUnarmedDominantArmForwardPose(GraphicsDevice d, BasicEffect e, Vector3 p,
                                                       UnitDimensions dims, Matrix r, Color bodyColor,
                                                       Unit.Handedness dominantHand)
        {
            Color armColor = bodyColor * 0.9f;
            Color forearmColor = armColor * 0.95f;
            Color jointColor = bodyColor * 0.62f;

            float dominantSign = GetHandSideSign(dominantHand);
            float shoulderHeightScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderHeightScale, 0.7f, 1.3f);
            float shoulderWidthScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderWidthScale, 0.7f, 1.3f);
            float shoulderX = dominantSign * dims.tw * 0.58f * shoulderWidthScale;
            float shoulderY = dims.ll + dims.th * (0.86f * shoulderHeightScale) + GetShoulderVerticalOffset(dims, shoulderX);

            Vector3 shoulder = new Vector3(shoulderX, shoulderY, 0f);
            Vector3 elbow = new Vector3(shoulderX, shoulderY - dims.al * 0.06f, dims.al * 0.48f);
            Vector3 wrist = new Vector3(shoulderX, shoulderY - dims.al * 0.1f, dims.al * 0.96f);

            DrawSphere(d, e, p, shoulder, dims.lw * 0.28f, armColor * 0.88f, r);
            DrawFrustumBetween(d, e, p, shoulder, elbow,
                dims.lw * 0.58f, dims.lw * 0.46f, armColor, r, 6);
            DrawForearmBetween(d, e, p, elbow, wrist,
                dims.lw * 0.46f, dims.lw * 0.36f, forearmColor, r, 6);

            Vector3 handPos = wrist + new Vector3(0f, -dims.lw * 0.08f, dims.lw * 0.26f);
            DrawBodyPart(d, e, p, handPos,
                new Vector3(dims.lw * 0.62f, dims.lw * 0.36f, dims.lw * 0.72f), forearmColor * 0.95f, r);

            DrawRoundedHead(d, e, p, elbow, dims.lw * 0.3f, jointColor, r);
        }


        private void DrawSkeletonJoints(GraphicsDevice d, BasicEffect e, Vector3 p, UnitDimensions dims,
                                        Matrix r, Color bodyColor, float legSwing, float armSwing)
        {
            Color jointColor = bodyColor * 0.65f;

            float legPhase = MathHelper.Clamp(legSwing / 0.42f, -1f, 1f);
            float rearLegPhase = -legPhase;
            Vector3 leftKnee = new Vector3(-dims.tw * 0.3f, dims.ll * (0.5f + Math.Max(0f, legPhase) * 0.18f), legPhase * dims.ll * 0.34f);
            Vector3 rightKnee = new Vector3(dims.tw * 0.3f, dims.ll * (0.5f + Math.Max(0f, rearLegPhase) * 0.18f), rearLegPhase * dims.ll * 0.34f);
            DrawRoundedHead(d, e, p, leftKnee, dims.lw * 0.28f, jointColor, r);
            DrawRoundedHead(d, e, p, rightKnee, dims.lw * 0.28f, jointColor, r);

            float shoulderJointX = dims.tw * 0.6f;
            float shoulderJointBaseY = dims.ll + dims.th * 0.9f;
            DrawRoundedHead(d, e, p, new Vector3(-shoulderJointX, shoulderJointBaseY + GetShoulderVerticalOffset(dims, -shoulderJointX), 0), dims.lw * 0.28f, jointColor, r);
            DrawRoundedHead(d, e, p, new Vector3(shoulderJointX, shoulderJointBaseY + GetShoulderVerticalOffset(dims, shoulderJointX), 0), dims.lw * 0.28f, jointColor, r);

            float armPhase = MathHelper.Clamp(armSwing / 0.3f, -1f, 1f);
            float oppositeArmPhase = -armPhase;
            Vector3 leftElbow = new Vector3(-dims.tw * 0.6f, dims.ll + dims.th * (0.57f + Math.Max(0f, armPhase) * 0.06f), armPhase * dims.al * 0.32f);
            Vector3 rightElbow = new Vector3(dims.tw * 0.6f, dims.ll + dims.th * (0.57f + Math.Max(0f, oppositeArmPhase) * 0.06f), oppositeArmPhase * dims.al * 0.32f);
            DrawRoundedHead(d, e, p, leftElbow, dims.lw * 0.28f, jointColor, r);
            DrawRoundedHead(d, e, p, rightElbow, dims.lw * 0.28f, jointColor, r);
        }

        private void DrawWeaponGripPose(GraphicsDevice d, BasicEffect e, Vector3 p, UnitDimensions dims,
                                        Matrix r, Color bodyColor, bool isAiming, Unit.Handedness dominantHand)
        {
            Color armColor = bodyColor * 0.9f;
            float shoulderHeightScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderHeightScale, 0.7f, 1.3f);
            float shoulderWidthScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderWidthScale, 0.7f, 1.3f);
            float shoulderBaseY = dims.ll + dims.th * (0.9f * shoulderHeightScale);
            float elbowY = dims.ll + dims.th * (isAiming ? 0.82f : 0.72f);
            float handY = dims.ll + dims.th * (isAiming ? 0.76f : 0.64f);
            float handZ = dims.td * (isAiming ? 0.95f : 0.45f);
            float handSign = GetHandSideSign(dominantHand);
            float supportSign = -handSign;

            float dominantShoulderX = handSign * dims.tw * 0.58f * shoulderWidthScale;
            float supportShoulderX = supportSign * dims.tw * 0.58f * shoulderWidthScale;
            float dominantShoulderY = shoulderBaseY + GetShoulderVerticalOffset(dims, dominantShoulderX);
            float supportShoulderY = shoulderBaseY + GetShoulderVerticalOffset(dims, supportShoulderX);

            DrawRoundedCapsuleY(d, e, p, new Vector3(dominantShoulderX, (dominantShoulderY + elbowY) * 0.5f, handZ * 0.35f),
                dims.al * 0.52f, dims.lw * 0.5f, armColor, r, 6);
            DrawRoundedCapsuleY(d, e, p, new Vector3(handSign * dims.tw * 0.62f, (elbowY + handY) * 0.5f, handZ * 0.7f),
                dims.al * 0.48f, dims.lw * 0.46f, armColor * 0.95f, r, 6);

            DrawRoundedCapsuleY(d, e, p, new Vector3(supportShoulderX, (supportShoulderY + elbowY) * 0.5f, handZ * 0.45f),
                dims.al * 0.46f, dims.lw * 0.5f, armColor * 0.95f, r, 6);
            DrawRoundedCapsuleY(d, e, p, new Vector3(supportSign * dims.tw * 0.42f, (elbowY + handY) * 0.5f, handZ * 0.85f),
                dims.al * 0.42f, dims.lw * 0.46f, armColor * 0.9f, r, 6);

            Color jointColor = bodyColor * 0.6f;
            DrawRoundedHead(d, e, p, new Vector3(handSign * dims.tw * 0.6f, elbowY, handZ * 0.5f), dims.lw * 0.3f, jointColor, r);
            DrawRoundedHead(d, e, p, new Vector3(supportSign * dims.tw * 0.5f, elbowY, handZ * 0.6f), dims.lw * 0.3f, jointColor, r);
        }

        private void DrawRunningLegPair(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                        UnitDimensions dims, float legSwing, float legSpread,
                                        float legRadiusScale, Color legColor, Color footColor)
        {
            float hipCornerOffset = dims.tw * 0.06f;
            DrawRunningLeg(d, e, p, r, dims, legSwing, -dims.tw * legSpread - hipCornerOffset, legRadiusScale, legColor, footColor);
            DrawRunningLeg(d, e, p, r, dims, -legSwing, dims.tw * legSpread + hipCornerOffset, legRadiusScale, legColor, footColor);
        }

        private void DrawSwingingArmPair(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                         UnitDimensions dims, float armSwing,
                                         float shoulderSpread, float shoulderHeight,
                                         float bendBias, float radiusScale, Color armColor)
        {
            float shoulderWidthScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderWidthScale, 0.7f, 1.3f);
            float shoulderHeightScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderHeightScale, 0.7f, 1.3f);

            float shoulderBaseY = dims.ll + dims.th * shoulderHeight * shoulderHeightScale;
            float shoulderOffsetX = dims.tw * shoulderSpread * shoulderWidthScale;
            float leftShoulderY = shoulderBaseY + GetShoulderVerticalOffset(dims, -shoulderOffsetX);
            float rightShoulderY = shoulderBaseY + GetShoulderVerticalOffset(dims, shoulderOffsetX);
            float shoulderSphereRadius = dims.lw * 0.28f * shoulderWidthScale;

            DrawSphere(d, e, p, new Vector3(-shoulderOffsetX, leftShoulderY, 0f), shoulderSphereRadius, armColor * 0.88f, r);
            DrawSphere(d, e, p, new Vector3(shoulderOffsetX, rightShoulderY, 0f), shoulderSphereRadius, armColor * 0.88f, r);

            DrawSwingingArm(d, e, p, r, dims, armSwing, -dims.tw * shoulderSpread * shoulderWidthScale, shoulderHeight * shoulderHeightScale, bendBias, radiusScale, armColor);
            DrawSwingingArm(d, e, p, r, dims, -armSwing, dims.tw * shoulderSpread * shoulderWidthScale, shoulderHeight * shoulderHeightScale, bendBias, radiusScale, armColor);
        }

        private void DrawSwingingArm(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                     UnitDimensions dims, float phase, float shoulderX,
                                     float shoulderHeight, float bendBias,
                                     float radiusScale, Color armColor)
        {
            float normalizedPhase = MathHelper.Clamp(phase / 0.3f, -1f, 1f);
            float forward = normalizedPhase;
            float backward = -normalizedPhase;

            float shoulderYOffset = GetShoulderVerticalOffset(dims, shoulderX);

            Vector3 shoulder = new Vector3(shoulderX, dims.ll + dims.th * shoulderHeight + shoulderYOffset, -forward * dims.al * 0.08f);
            Vector3 elbow = new Vector3(
                shoulderX,
                dims.ll + dims.th * (shoulderHeight - 0.3f) + shoulderYOffset + Math.Max(0f, forward) * dims.al * 0.15f,
                forward * dims.al * 0.34f + bendBias * dims.al * 0.12f);

            Vector3 wrist = elbow + new Vector3(
                0f,
                -dims.al * (0.32f + Math.Max(0f, backward) * 0.1f),
                forward * dims.al * 0.28f + Math.Max(0f, backward) * dims.al * 0.12f + bendBias * dims.al * 0.08f);

            // Épaule = vraie sphère
            DrawSphere(d, e, p, shoulder, dims.lw * (radiusScale * 0.56f), armColor * 0.88f, r);

            // Biceps + avant-bras = cônes tronqués
            DrawFrustumBetween(d, e, p, shoulder, elbow,
                dims.lw * (radiusScale * 1.08f), dims.lw * (radiusScale * 0.88f), armColor, r, 6);
            DrawForearmBetween(d, e, p, elbow, wrist,
                dims.lw * (radiusScale * 0.9f), dims.lw * (radiusScale * 0.72f), armColor * 0.95f, r, 6);

            // Main = prisme rectangulaire
            Vector3 handPos = wrist + new Vector3(0f, -dims.lw * 0.25f, forward * dims.lw * 0.95f);
            Vector3 handScale = new Vector3(dims.lw * (radiusScale * 1.02f), dims.lw * (radiusScale * 0.82f), dims.lw * (radiusScale * 1.08f));
            DrawBodyPart(d, e, p, handPos, handScale, armColor * 0.92f, r);
        }

        private void DrawRunningLeg(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                    UnitDimensions dims, float phase, float hipX,
                                    float legRadiusScale, Color legColor, Color footColor)
        {
            float normalizedPhase = MathHelper.Clamp(phase / 0.42f, -1f, 1f);
            float forward = normalizedPhase;
            float backward = -normalizedPhase;

            float arcSign = hipX < 0f ? -1f : 1f;
            float forwardLift = Math.Max(0f, forward);
            float trailingCompression = Math.Max(0f, backward);

            // Cuisse : tronc de cône massif, légèrement orienté vers l'intérieur et courbé vers l'avant.
            Vector3 hip = new Vector3(
                hipX,
                dims.ll + dims.th * 0.03f,
                -forward * dims.ll * 0.06f + arcSign * dims.lw * 0.12f);
            Vector3 knee = new Vector3(
                hipX * 0.88f,
                dims.ll * (0.49f + forwardLift * 0.2f),
                forward * dims.ll * 0.33f + dims.lw * 0.08f);

            // Jambe : volume affiné et asymétrique, arc léger vers l'arrière.
            Vector3 ankle = knee + new Vector3(
                arcSign * dims.lw * 0.05f,
                -dims.ll * (0.42f + trailingCompression * 0.14f),
                -dims.ll * 0.05f + forward * dims.ll * 0.16f + trailingCompression * dims.ll * 0.26f);

            Vector3 calf = Vector3.Lerp(knee, ankle, 0.38f) + new Vector3(
                -arcSign * dims.lw * 0.06f,
                dims.ll * 0.08f,
                -dims.ll * 0.05f);

            // Cuisse = gros tronc de cône.
            DrawFrustumBetween(d, e, p, hip, knee,
                dims.lw * (legRadiusScale * 1.36f), dims.lw * (legRadiusScale * 1.0f), legColor, r, 7);

            // Genou = bloc de transition avec rotule frontale plate.
            DrawBodyPart(d, e, p, knee,
                new Vector3(dims.lw * (legRadiusScale * 0.86f), dims.lw * (legRadiusScale * 0.62f), dims.lw * (legRadiusScale * 0.72f)),
                legColor * 0.86f, r);
            DrawBodyPart(d, e, p, knee + new Vector3(0f, 0f, dims.lw * 0.26f),
                new Vector3(dims.lw * (legRadiusScale * 0.34f), dims.lw * (legRadiusScale * 0.22f), dims.lw * (legRadiusScale * 0.14f)),
                legColor * 0.72f, r);

            // Mollet asymétrique (renflé en haut/arrière), puis tibia affiné.
            DrawFrustumBetween(d, e, p, knee, calf,
                dims.lw * (legRadiusScale * 0.98f), dims.lw * (legRadiusScale * 1.05f), legColor * 0.95f, r, 7);
            DrawFrustumBetween(d, e, p, calf, ankle,
                dims.lw * (legRadiusScale * 1.05f), dims.lw * (legRadiusScale * 0.62f), legColor * 0.93f, r, 7);

            // Tibia mis en avant par une face dure.
            Vector3 shinPlatePos = Vector3.Lerp(knee, ankle, 0.56f) + new Vector3(0f, -dims.ll * 0.02f, dims.lw * 0.18f);
            DrawBodyPart(d, e, p, shinPlatePos,
                new Vector3(dims.lw * (legRadiusScale * 0.32f), dims.ll * 0.16f, dims.lw * (legRadiusScale * 0.08f)),
                legColor * 0.7f, r);

            // Cheville = bloc rectangulaire avec malléoles décalées.
            DrawBodyPart(d, e, p, ankle,
                new Vector3(dims.lw * (legRadiusScale * 0.56f), dims.lw * (legRadiusScale * 0.42f), dims.lw * (legRadiusScale * 0.44f)),
                legColor * 0.84f, r);
            DrawRoundedHead(d, e, p, ankle + new Vector3(-dims.lw * 0.2f, dims.lw * 0.09f, 0f), dims.lw * (legRadiusScale * 0.12f), legColor * 0.9f, r);
            DrawRoundedHead(d, e, p, ankle + new Vector3(dims.lw * 0.2f, -dims.lw * 0.02f, 0f), dims.lw * (legRadiusScale * 0.11f), legColor * 0.82f, r);

            // Pied = prisme en coin pour l'appui au sol.
            Vector3 footCenter = ankle + new Vector3(0f, -dims.ll * 0.08f, dims.lw * (0.56f + forwardLift * 0.26f));
            DrawBodyPart(d, e, p, footCenter,
                new Vector3(dims.lw * (legRadiusScale * 0.86f), dims.ll * 0.08f, dims.lw * (legRadiusScale * 1.9f)),
                footColor, r);
            DrawBodyPart(d, e, p, footCenter + new Vector3(0f, dims.ll * 0.05f, -dims.lw * 0.22f),
                new Vector3(dims.lw * (legRadiusScale * 0.7f), dims.ll * 0.05f, dims.lw * (legRadiusScale * 1.2f)),
                footColor * 0.9f, r);
        }

        private void DrawSoldier(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Soldier, s);
            DrawSoldierBody(d, e, p, c, s, r, l, a, dims, Unit.HumanBodyType.Masculine);
        }

        private void DrawSoldierBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                     float l, float a, UnitDimensions dims, Unit.HumanBodyType bodyType,
                                     bool hasPants = false, bool useKungFuPose = false,
                                     bool drawDefaultArms = true)
        {
            Color skin = new(220, 180, 140);
            Color body = skin * 0.95f;
            Color dark = body * 0.85f;

            if (!hasPants)
            {
                DrawRunningLegPair(d, e, p, r, dims, l, 0.3f, 0.55f, dark, dark * 0.8f);
            }

            DrawStructuredTorso(d, e, p, r, dims, body, dark, bodyType);

            // Bras: épaules sphériques, segments en cônes tronqués, mains en prismes rectangulaires
            if (drawDefaultArms)
            {
                if (useKungFuPose)
                    DrawTPose(d, e, p, r, dims, body * 0.9f);
                else
                    DrawSwingingArmPair(d, e, p, r, dims, a, 0.6f, 0.9f, 0f, 0.52f, body * 0.9f);
            }

            // Cou = cylindre
            DrawRoundedCapsuleY(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.1f, 0),
                dims.head * 0.48f, dims.head * 0.2f, skin * 0.95f, r, 5);

            // Tête = sphère
            DrawSphere(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f, 0),
                dims.head * 0.52f, skin, r);

            // Pas d'accessoires vestimentaires/cosmétiques.
        }

        private void DrawStructuredTorso(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                         UnitDimensions dims, Color chestColor, Color pelvisColor,
                                         Unit.HumanBodyType bodyType)
        {
            bool feminine = bodyType == Unit.HumanBodyType.Feminine;

            // Structure de tronc en 3 blocs (1 : 0.8 : 1), avec axe vertical stabilisé.
            // On évite ainsi les ventres projetés et la "posture en banane".
            float ribRatio = HumanBodyMorphSettings.RibRatio;
            float abdomenRatio = HumanBodyMorphSettings.AbdomenRatio;
            float pelvisRatio = HumanBodyMorphSettings.PelvisRatio;
            float ratioSum = ribRatio + abdomenRatio + pelvisRatio;

            float ribHeight = dims.th * (ribRatio / ratioSum);
            float abdomenHeight = dims.th * (abdomenRatio / ratioSum);
            float pelvisHeight = dims.th * (pelvisRatio / ratioSum);

            float pelvisCenterY = dims.ll + pelvisHeight * 0.5f;
            float abdomenCenterY = pelvisCenterY + pelvisHeight * 0.5f + abdomenHeight * 0.5f;
            float ribCenterY = abdomenCenterY + abdomenHeight * 0.5f + ribHeight * 0.5f;

            // Sternum / bassin alignés en Z pour préserver la verticalité.
            float trunkCenterZ = 0f;

            // 1) Cage thoracique
            float ribTopWidth = dims.tw * (feminine ? HumanBodyMorphSettings.FeminineRibTopWidthFactor : HumanBodyMorphSettings.MasculineRibTopWidthFactor);
            float ribBottomWidth = dims.tw * (feminine ? HumanBodyMorphSettings.FeminineRibBottomWidthFactor : HumanBodyMorphSettings.MasculineRibBottomWidthFactor);
            float chestDepth = dims.td;
            Matrix ribRot = Matrix.CreateRotationX(MathHelper.ToRadians(-2f)) * r;
            DrawTorsoPolygon(d, e, p, new Vector3(0, ribCenterY, trunkCenterZ),
                ribHeight, ribTopWidth, ribBottomWidth, chestDepth, chestColor, ribRot);

            // 2) Abdomen-pont : capsule continue + bloc de soutien
            // pour relier visuellement la sortie de cage et l'entrée de bassin.
            float abdomenDepth = chestDepth * 0.8f;
            float pelvisTopWidth = dims.tw * (feminine ? HumanBodyMorphSettings.FemininePelvisTopWidthFactor : HumanBodyMorphSettings.MasculinePelvisTopWidthFactor);
            float pelvisBottomWidth = dims.tw * (feminine ? HumanBodyMorphSettings.FemininePelvisBottomWidthFactor : HumanBodyMorphSettings.MasculinePelvisBottomWidthFactor);
            float abdomenStartY = ribCenterY - ribHeight * 0.45f;
            float abdomenEndY = pelvisCenterY + pelvisHeight * 0.45f;
            float abdomenRadius = MathHelper.Lerp(ribBottomWidth, pelvisTopWidth, 0.5f) * 0.36f;

            DrawRoundedCapsuleBetween(d, e, p,
                new Vector3(0, abdomenStartY, trunkCenterZ),
                new Vector3(0, abdomenEndY, trunkCenterZ),
                abdomenRadius,
                chestColor * 0.88f,
                r,
                6);

            DrawBodyPart(d, e, p, new Vector3(0, abdomenCenterY, trunkCenterZ),
                new Vector3(MathHelper.Lerp(ribBottomWidth, pelvisTopWidth, 0.5f), abdomenHeight * 0.58f, abdomenDepth),
                chestColor * 0.83f, r);

            // 3) Bassin : forme en coin/trapèze (plus large en haut), incliné vers l'avant.
            float pelvisDepth = chestDepth * 0.84f;
            Matrix pelvisRot = Matrix.CreateRotationX(MathHelper.ToRadians(-5f)) * r;
            DrawTorsoPolygon(d, e, p, new Vector3(0, pelvisCenterY, trunkCenterZ),
                pelvisHeight, pelvisTopWidth, pelvisBottomWidth, pelvisDepth, pelvisColor * 0.94f, pelvisRot);

            // Face avant : triangle pubien inversé (plane plus nette).
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + pelvisHeight * 0.36f, dims.td * 0.33f),
                new Vector3(dims.tw * 0.12f, pelvisHeight * 0.13f, dims.td * 0.06f), pelvisColor * 0.72f, pelvisRot);
            DrawBodyPart(d, e, p, new Vector3(-dims.tw * 0.08f, dims.ll + pelvisHeight * 0.31f, dims.td * 0.29f),
                new Vector3(dims.tw * 0.09f, pelvisHeight * 0.07f, dims.td * 0.05f), pelvisColor * 0.7f, pelvisRot);
            DrawBodyPart(d, e, p, new Vector3(dims.tw * 0.08f, dims.ll + pelvisHeight * 0.31f, dims.td * 0.29f),
                new Vector3(dims.tw * 0.09f, pelvisHeight * 0.07f, dims.td * 0.05f), pelvisColor * 0.7f, pelvisRot);

            // Faces latérales : plans inclinés vers les hanches / grand trochanter.
            DrawBodyPart(d, e, p, new Vector3(-dims.tw * 0.47f, pelvisCenterY, dims.td * 0.07f),
                new Vector3(dims.tw * 0.11f, pelvisHeight * 0.4f, dims.td * 0.2f), pelvisColor * 0.82f, pelvisRot);
            DrawBodyPart(d, e, p, new Vector3(dims.tw * 0.47f, pelvisCenterY, dims.td * 0.07f),
                new Vector3(dims.tw * 0.11f, pelvisHeight * 0.4f, dims.td * 0.2f), pelvisColor * 0.82f, pelvisRot);

            // Faces arrière : deux volumes "D" pour les fessiers.
            DrawRoundedHead(d, e, p, new Vector3(-dims.tw * 0.27f, dims.ll + pelvisHeight * 0.42f, -dims.td * 0.22f),
                dims.tw * 0.22f, pelvisColor * 0.87f, pelvisRot);
            DrawRoundedHead(d, e, p, new Vector3(dims.tw * 0.27f, dims.ll + pelvisHeight * 0.42f, -dims.td * 0.22f),
                dims.tw * 0.22f, pelvisColor * 0.87f, pelvisRot);

            // Points de repère : épaules, plexus (V inversé), axe central.
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.88f, -dims.td * 0.02f),
                new Vector3(dims.tw * 0.74f, dims.th * 0.08f, dims.td * 0.56f), chestColor * 1.04f, r);

            float plexusY = dims.ll + dims.th * 0.46f;
            DrawBodyPart(d, e, p, new Vector3(-dims.tw * 0.08f, plexusY, dims.td * 0.34f),
                new Vector3(dims.tw * 0.08f, dims.th * 0.035f, dims.td * 0.04f), chestColor * 0.7f, r);
            DrawBodyPart(d, e, p, new Vector3(dims.tw * 0.08f, plexusY, dims.td * 0.34f),
                new Vector3(dims.tw * 0.08f, dims.th * 0.035f, dims.td * 0.04f), chestColor * 0.7f, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.43f, dims.td * 0.35f),
                new Vector3(dims.tw * 0.035f, dims.th * 0.05f, dims.td * 0.04f), chestColor * 0.66f, r);

            DrawRoundedCapsuleY(d, e, p, new Vector3(0, dims.ll + dims.th * 0.34f, dims.td * 0.36f),
                dims.th * 0.52f, dims.tw * 0.03f, chestColor * 0.72f, r, 4);
        }

        private void DrawHumanHeadFeatures(GraphicsDevice d, BasicEffect e, Vector3 p, Color c,
                                           Matrix r, UnitDimensions dims, Unit.HumanBodyType bodyType)
        {
            if (bodyType == Unit.HumanBodyType.Feminine)
            {
                DrawAerisInspiredFeatures(d, e, p, c, r, dims);
                return;
            }

            DrawCloudInspiredFeatures(d, e, p, c, r, dims);
        }

        private void DrawAerisInspiredFeatures(GraphicsDevice d, BasicEffect e, Vector3 p, Color c,
                                               Matrix r, UnitDimensions dims)
        {
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.22f, dims.td * 0.05f),
                        new Vector3(dims.tw * 0.9f, dims.th * 0.11f, dims.td), new Color(88, 60, 64), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.45f, dims.td * 0.58f),
                        new Vector3(dims.tw * 0.35f, dims.th * 0.2f, dims.td * 0.2f), c * 0.75f, r);
        }

        private void DrawCloudInspiredFeatures(GraphicsDevice d, BasicEffect e, Vector3 p, Color c,
                                               Matrix r, UnitDimensions dims)
        {
            // Épaulette unique sur l'épaule gauche pour une silhouette marquante
            DrawBodyPart(d, e, p, new Vector3(-dims.tw * 0.86f, dims.ll + dims.th * 0.84f, 0),
                        new Vector3(dims.tw * 0.42f, dims.al * 0.35f, dims.td * 0.95f), new Color(85, 92, 112), r);

            // Ceinture large contrastée
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.22f, dims.td * 0.05f),
                        new Vector3(dims.tw * 0.98f, dims.th * 0.14f, dims.td * 1.08f), new Color(82, 64, 46), r);
        }

        private void DrawAlien(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Alien, s);
            DrawAlienBody(d, e, p, c, s, r, l, a, dims, false);
        }

        private void DrawAlienBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                   float l, float a, UnitDimensions dims, bool useKungFuPose = false,
                                   bool drawDefaultArms = true)
        {
            Color skin = new(150, 200, 150), dark = c * 0.6f;
            DrawRunningLegPair(d, e, p, r, dims, l * 1.1f, 0.3f, 1f, dark, dark * 0.9f);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, 0), new Vector3(dims.tw, dims.th, dims.td), c, r);
            if (drawDefaultArms)
            {
                if (useKungFuPose)
                    DrawTPose(d, e, p, r, dims, c * 0.85f);
                else
                    DrawSwingingArmPair(d, e, p, r, dims, a * 1.1f, 0.6f, 0.82f, 0.15f, 0.95f, c * 0.85f);
            }
            DrawSphere(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, 0), dims.head * 0.56f, skin, r);
            DrawBodyPart(d, e, p, new Vector3(-dims.head * 0.3f, dims.ll + dims.th + dims.head * 0.5f + 0.1f * s, dims.head * 0.5f), new Vector3(dims.head * 0.2f, dims.head * 0.25f, dims.head * 0.1f), Color.Black, r);
            DrawBodyPart(d, e, p, new Vector3(dims.head * 0.3f, dims.ll + dims.th + dims.head * 0.5f + 0.1f * s, dims.head * 0.5f), new Vector3(dims.head * 0.2f, dims.head * 0.25f, dims.head * 0.1f), Color.Black, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, dims.head * 0.55f), new Vector3(dims.head * 0.8f, dims.head * 0.15f, dims.head * 0.05f), new Color(0, 255, 0), r);
        }

        private void DrawZombie(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Zombie, s);
            DrawZombieBody(d, e, p, c, s, r, l, a, dims, false);
        }

        private void DrawZombieBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                    float l, float a, UnitDimensions dims, bool useKungFuPose = false,
                                    bool drawDefaultArms = true)
        {
            Color skin = new(140, 160, 130), dark = new(80, 70, 60);
            DrawRunningLegPair(d, e, p, r, dims, l * 1.15f, 0.35f, 1f, dark, dark * 0.85f);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, -0.1f * s), new Vector3(dims.tw, dims.th, dims.td), c * 0.7f, r);
            if (drawDefaultArms)
            {
                if (useKungFuPose)
                    DrawTPose(d, e, p, r, dims, c * 0.6f);
                else
                    DrawSwingingArmPair(d, e, p, r, dims, a * 0.8f, 0.6f, 0.72f, 0.45f, 1f, c * 0.6f);
            }
            DrawSphere(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, -0.05f * s), dims.head * 0.5f, skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, dims.head * 0.55f), new Vector3(dims.head * 0.5f, dims.head * 0.2f, dims.head * 0.05f), new Color(180, 0, 0), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.4f, dims.td * 0.9f), new Vector3(dims.lw * 1.5f, dims.lw, dims.lw * 2f), c * 0.5f, r);
        }

        private void DrawHeavy(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Heavy, s);
            DrawHeavyBody(d, e, p, c, s, r, l, a, dims, false);
        }

        private void DrawHeavyBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                   float l, float a, UnitDimensions dims, bool useKungFuPose = false,
                                   bool drawDefaultArms = true)
        {
            Color skin = new(220, 180, 140), dark = new(50, 50, 70);
            DrawRunningLegPair(d, e, p, r, dims, l * 0.9f, 0.3f, 1.2f, dark, dark * 0.8f);
            DrawStructuredTorso(d, e, p, r, dims, c, dark, Unit.HumanBodyType.Masculine);
            if (drawDefaultArms)
            {
                if (useKungFuPose)
                    DrawTPose(d, e, p, r, dims, c * 0.85f);
                else
                    DrawSwingingArmPair(d, e, p, r, dims, a * 0.75f, 0.65f, 0.88f, -0.1f, 1.25f, c * 0.85f);
            }
            DrawSphere(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, 0), dims.head * 0.56f, skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, dims.head * 0.6f), new Vector3(dims.head * 0.8f, dims.head * 0.4f, dims.head * 0.15f), new Color(30, 30, 30), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, dims.td * 0.9f), new Vector3(dims.tw * 0.4f, dims.th * 0.4f, dims.td * 0.7f), new Color(60, 60, 60), r);

            DrawCloudInspiredFeatures(d, e, p, c, r, dims);
        }

        private void DrawScout(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Scout, s);
            DrawScoutBody(d, e, p, c, s, r, l, a, dims, false);
        }

        private void DrawScoutBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                   float l, float a, UnitDimensions dims, bool useKungFuPose = false,
                                   bool drawDefaultArms = true)
        {
            Color skin = new(220, 180, 140), dark = new(70, 70, 90);
            DrawRunningLegPair(d, e, p, r, dims, l * 1.2f, 0.25f, 1f, dark, dark * 0.85f);
            DrawStructuredTorso(d, e, p, r, dims, c, dark, Unit.HumanBodyType.Masculine);
            if (drawDefaultArms)
            {
                if (useKungFuPose)
                    DrawTPose(d, e, p, r, dims, c * 0.85f);
                else
                    DrawSwingingArmPair(d, e, p, r, dims, a * 1.2f, 0.55f, 0.9f, 0f, 0.95f, c * 0.85f);
            }
            DrawSphere(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f, 0), dims.head * 0.5f, skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f + 0.05f * s, dims.head * 0.6f), new Vector3(dims.head * 0.7f, dims.head * 0.25f, dims.head * 0.08f), new Color(50, 100, 150), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f, dims.head * 0.8f), new Vector3(dims.head * 0.1f, dims.head * 0.1f, dims.head * 0.35f), new Color(255, 0, 0), r);

            DrawCloudInspiredFeatures(d, e, p, c, r, dims);
        }

        private void DrawTPose(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                               UnitDimensions dims, Color armColor)
        {
            Color forearmColor = armColor * 0.95f;

            float shoulderHeightScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderHeightScale, 0.7f, 1.3f);
            float shoulderWidthScale = HumanBodyMorphSettings.ClampScale(HumanBodyMorphSettings.ShoulderWidthScale, 0.7f, 1.3f);
            float shoulderBaseY = dims.ll + dims.th * (0.9f * shoulderHeightScale);
            float armReach = dims.tw * 1.55f * shoulderWidthScale;
            float elbowDrop = dims.lw * 0.06f;
            float shoulderSphereRadius = dims.lw * 0.3f * shoulderWidthScale;

            float leftShoulderX = -dims.tw * 0.62f * shoulderWidthScale;
            float rightShoulderX = dims.tw * 0.62f * shoulderWidthScale;
            float leftShoulderY = shoulderBaseY + GetShoulderVerticalOffset(dims, leftShoulderX);
            float rightShoulderY = shoulderBaseY + GetShoulderVerticalOffset(dims, rightShoulderX);

            Vector3 leftShoulder = new Vector3(leftShoulderX, leftShoulderY, 0f);
            Vector3 leftElbow = new Vector3(-armReach, leftShoulderY - elbowDrop, 0f);
            Vector3 leftWrist = new Vector3(-armReach - dims.al * 0.55f, leftShoulderY - elbowDrop, 0f);

            Vector3 rightShoulder = new Vector3(rightShoulderX, rightShoulderY, 0f);
            Vector3 rightElbow = new Vector3(armReach, rightShoulderY - elbowDrop, 0f);
            Vector3 rightWrist = new Vector3(armReach + dims.al * 0.55f, rightShoulderY - elbowDrop, 0f);

            DrawSphere(d, e, p, leftShoulder, shoulderSphereRadius, armColor * 0.88f, r);
            DrawFrustumBetween(d, e, p, leftShoulder, leftElbow, dims.lw * 0.54f, dims.lw * 0.42f, armColor, r, 6);
            DrawForearmBetween(d, e, p, leftElbow, leftWrist, dims.lw * 0.43f, dims.lw * 0.34f, forearmColor, r, 6);
            DrawBodyPart(d, e, p, leftWrist + new Vector3(-dims.lw * 0.24f, -dims.lw * 0.05f, 0f),
                new Vector3(dims.lw * 0.52f, dims.lw * 0.30f, dims.lw * 0.48f), forearmColor * 0.95f, r);

            DrawSphere(d, e, p, rightShoulder, shoulderSphereRadius, armColor * 0.88f, r);
            DrawFrustumBetween(d, e, p, rightShoulder, rightElbow, dims.lw * 0.54f, dims.lw * 0.42f, armColor, r, 6);
            DrawForearmBetween(d, e, p, rightElbow, rightWrist, dims.lw * 0.43f, dims.lw * 0.34f, forearmColor, r, 6);
            DrawBodyPart(d, e, p, rightWrist + new Vector3(dims.lw * 0.24f, -dims.lw * 0.05f, 0f),
                new Vector3(dims.lw * 0.52f, dims.lw * 0.30f, dims.lw * 0.48f), forearmColor * 0.95f, r);
        }
    }
}
