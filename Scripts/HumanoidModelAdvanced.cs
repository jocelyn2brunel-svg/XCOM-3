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
                                               Color color, Matrix modelRot, int segments = 4)
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
                                         float height, float radius, Color color, Matrix rot, int segments = 4)
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
            DrawRoundedCapsuleY(device, effect, center, relative, radius * 1.35f, radius * 1.05f, color, rot, 5);
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
                                      float bodyBob = 0f, float idleBob = 0f)
        {
            Vector3 pos = unit.VisualPosition;
            Color teamColor = unit.Team == Team.Player ? new Color(100, 150, 255) : new Color(255, 100, 100);

            Vector3 animatedPos = pos + new Vector3(0, bodyBob + idleBob, 0);
            Matrix rot = Matrix.CreateRotationY(orientation);

            // Déterminer le type d'unité
            UnitType type = GetUnitType(unit);

            // Dimensions de base selon le type
            var dims = GetUnitDimensions(type, scale, unit.BodyType);

            bool hasWeapon = unit.EquippedWeapon != null || unit.WeaponData != null;
            bool isAiming = unit.IsAiming || unit.IsFiring;

            // Dessiner le corps de base
            DrawUnitBody(device, effect, animatedPos, teamColor, scale, type, rot, legSwing, armSwing, dims, hasWeapon, isAiming, unit.BodyType);

            // ✅ NOUVEAU : Dessiner l'équipement
            DrawEquipment(device, effect, animatedPos, unit, scale, rot, dims, isAiming);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DESSIN D'ÉQUIPEMENT
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawEquipment(GraphicsDevice device, BasicEffect effect, Vector3 pos, Unit unit,
                                   float scale, Matrix rot, UnitDimensions dims, bool isAiming)
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
                DrawWeapon(device, effect, pos, weaponToDraw, scale, rot, dims, isAiming);
            }

            // CHEMISE (sous le gilet)
            if (unit.EquippedShirt != null)
            {
                DrawShirt(device, effect, pos, unit.EquippedShirt, scale, rot, dims);
            }

            // ✅ NOUVEAU : GRENADES ÉQUIPÉES
            if (unit.Grenades != null && unit.Grenades.Count > 0)
            {
                DrawEquippedGrenades(device, effect, pos, unit.Grenades, scale, rot, dims);
            }
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

            // Position sur la tête
            Vector3 helmetPos = new Vector3(0, dims.ll + dims.th + dims.head * 0.6f, 0);

            // ✅ AMÉLIORÉ : Forme plus réaliste du casque PASGT
            if (helmet.Data.Name.Contains("PASGT"))
            {
                // Corps principal du casque (forme ovoïde arrondie)
                Vector3 helmetScale = new Vector3(dims.head * 1.2f, dims.head * 1.1f, dims.head * 1.3f);
                DrawBodyPart(device, effect, pos, helmetPos, helmetScale, helmetColor, rot);

                // Bord avant du casque (visière/front)
                Vector3 frontRimPos = new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, dims.head * 0.7f);
                Vector3 frontRimScale = new Vector3(dims.head * 1.3f, dims.head * 0.15f, dims.head * 0.2f);
                DrawBodyPart(device, effect, pos, frontRimPos, frontRimScale, helmetColor * 0.85f, rot);

                // Bord arrière du casque (protège-nuque étendu)
                Vector3 backRimPos = new Vector3(0, dims.ll + dims.th + dims.head * 0.3f, -dims.head * 0.7f);
                Vector3 backRimScale = new Vector3(dims.head * 1.2f, dims.head * 0.2f, dims.head * 0.3f);
                DrawBodyPart(device, effect, pos, backRimPos, backRimScale, helmetColor * 0.85f, rot);

                // Bords latéraux (protection des oreilles)
                Vector3 leftEarPos = new Vector3(-dims.head * 0.65f, dims.ll + dims.th + dims.head * 0.5f, 0);
                Vector3 earScale = new Vector3(dims.head * 0.15f, dims.head * 0.4f, dims.head * 0.5f);
                DrawBodyPart(device, effect, pos, leftEarPos, earScale, helmetColor * 0.9f, rot);

                Vector3 rightEarPos = new Vector3(dims.head * 0.65f, dims.ll + dims.th + dims.head * 0.5f, 0);
                DrawBodyPart(device, effect, pos, rightEarPos, earScale, helmetColor * 0.9f, rot);

                // Rivet/bouton visible (comme sur la photo)
                Vector3 rivetPos = new Vector3(0, dims.ll + dims.th + dims.head * 0.9f, dims.head * 0.5f);
                Vector3 rivetScale = new Vector3(dims.head * 0.08f, dims.head * 0.08f, dims.head * 0.08f);
                DrawBodyPart(device, effect, pos, rivetPos, rivetScale, new Color(60, 60, 60), rot);

                // Rails de montage sur les côtés (pour NVG ou accessoires)
                Vector3 leftRailPos = new Vector3(-dims.head * 0.55f, dims.ll + dims.th + dims.head * 0.7f, dims.head * 0.4f);
                Vector3 railScale = new Vector3(dims.head * 0.1f, dims.head * 0.08f, dims.head * 0.15f);
                DrawBodyPart(device, effect, pos, leftRailPos, railScale, new Color(40, 40, 40), rot);

                Vector3 rightRailPos = new Vector3(dims.head * 0.55f, dims.ll + dims.th + dims.head * 0.7f, dims.head * 0.4f);
                DrawBodyPart(device, effect, pos, rightRailPos, railScale, new Color(40, 40, 40), rot);
            }
            // Casques ACH/ECH/MICH (modernes)
            else if (helmet.Data.Name.Contains("ACH") || helmet.Data.Name.Contains("ECH") || helmet.Data.Name.Contains("MICH"))
            {
                // Taille légèrement plus grande que la tête
                Vector3 helmetScale = new Vector3(dims.head * 1.15f, dims.head * 1.25f, dims.head * 1.15f);
                DrawBodyPart(device, effect, pos, helmetPos, helmetScale, helmetColor, rot);

                // Visière NVG mount (caractéristique des casques modernes)
                Vector3 mountPos = new Vector3(0, dims.ll + dims.th + dims.head * 0.7f, dims.head * 0.6f);
                Vector3 mountScale = new Vector3(dims.head * 0.3f, dims.head * 0.15f, dims.head * 0.1f);
                DrawBodyPart(device, effect, pos, mountPos, mountScale, new Color(50, 50, 50), rot);

                // Rail picatinny sur le devant
                Vector3 railPos = new Vector3(0, dims.ll + dims.th + dims.head * 0.75f, dims.head * 0.55f);
                Vector3 railScale = new Vector3(dims.head * 0.25f, dims.head * 0.08f, dims.head * 0.08f);
                DrawBodyPart(device, effect, pos, railPos, railScale, new Color(30, 30, 30), rot);

                // Pads latéraux (protection d'impact)
                Vector3 leftPadPos = new Vector3(-dims.head * 0.6f, dims.ll + dims.th + dims.head * 0.6f, 0);
                Vector3 padScale = new Vector3(dims.head * 0.12f, dims.head * 0.3f, dims.head * 0.3f);
                DrawBodyPart(device, effect, pos, leftPadPos, padScale, helmetColor * 0.7f, rot);

                Vector3 rightPadPos = new Vector3(dims.head * 0.6f, dims.ll + dims.th + dims.head * 0.6f, 0);
                DrawBodyPart(device, effect, pos, rightPadPos, padScale, helmetColor * 0.7f, rot);
            }
            // Casque basique (fallback)
            else
            {
                Vector3 helmetScale = new Vector3(dims.head * 1.15f, dims.head * 1.25f, dims.head * 1.15f);
                DrawBodyPart(device, effect, pos, helmetPos, helmetScale, helmetColor, rot);
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

        private void DrawWeapon(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item weapon,
                                float scale, Matrix rot, UnitDimensions dims, bool isAiming)
        {
            Color weaponColor = GetWeaponColor(weapon.Data.Name);
            WeaponType weaponType = GetWeaponType(weapon.Data.Name);

            // Position calée entre les deux mains (pose articulée)
            Vector3 weaponPos = isAiming
                ? new Vector3(dims.tw * 0.05f, dims.ll + dims.th * 0.78f, dims.td * 0.95f)
                : new Vector3(dims.tw * 0.12f, dims.ll + dims.th * 0.66f, dims.td * 0.52f);
            Vector3 weaponScale = GetWeaponScale(weaponType, dims);

            // Corps de l'arme
            DrawBodyPart(device, effect, pos, weaponPos, weaponScale, weaponColor, rot);

            // Crosse (si rifle ou sniper)
            if (weaponType == WeaponType.Rifle || weaponType == WeaponType.Sniper)
            {
                Vector3 stockPos = new Vector3(
                    weaponPos.X - dims.tw * 0.2f,
                    weaponPos.Y + dims.lw * 0.1f,
                    weaponPos.Z - weaponScale.Z * 0.35f
                );
                Vector3 stockScale = new Vector3(weaponScale.X * 0.8f, weaponScale.Y * 0.5f, weaponScale.Z * 0.4f);
                DrawBodyPart(device, effect, pos, stockPos, stockScale, new Color(80, 50, 30), rot);
            }

            // Canon (partie avant)
            Vector3 barrelPos = new Vector3(
                weaponPos.X + dims.tw * 0.18f,
                weaponPos.Y,
                weaponPos.Z + weaponScale.Z * 0.62f
            );
            Vector3 barrelScale = new Vector3(weaponScale.X * 0.5f, weaponScale.Y * 0.5f, weaponScale.Z * 0.3f);
            DrawBodyPart(device, effect, pos, barrelPos, barrelScale, new Color(40, 40, 40), rot);
        }

        private void DrawShirt(GraphicsDevice device, BasicEffect effect, Vector3 pos, Item shirt,
                               float scale, Matrix rot, UnitDimensions dims)
        {
            Color shirtColor = new Color(100, 120, 80); // Couleur militaire

            // Chemise sous le gilet (légèrement visible)
            Vector3 shirtPos = new Vector3(0, dims.ll + dims.th * 0.5f, 0);
            Vector3 shirtScale = new Vector3(dims.tw * 0.95f, dims.th * 0.95f, dims.td * 0.95f);

            DrawBodyPart(device, effect, pos, shirtPos, shirtScale, shirtColor * 0.8f, rot);

            // Manches visibles
            Vector3 leftSleevePos = new Vector3(-dims.tw * 0.6f, dims.ll + dims.th * 0.7f, 0);
            Vector3 sleeveScale = new Vector3(dims.lw * 1.1f, dims.al * 0.8f, dims.lw * 1.1f);
            DrawBodyPart(device, effect, pos, leftSleevePos, sleeveScale, shirtColor * 0.85f, rot);

            Vector3 rightSleevePos = new Vector3(dims.tw * 0.6f, dims.ll + dims.th * 0.7f, 0);
            DrawBodyPart(device, effect, pos, rightSleevePos, sleeveScale, shirtColor * 0.85f, rot);
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
                    head = 0.25f * scale,
                    tw = 0.35f * scale,
                    th = 0.5f * scale,
                    td = 0.25f * scale,
                    lw = 0.12f * scale,
                    al = 0.45f * scale,
                    ll = 0.55f * scale
                },
                UnitType.Alien => new UnitDimensions
                {
                    head = 0.35f * scale,
                    tw = 0.3f * scale,
                    th = 0.45f * scale,
                    td = 0.2f * scale,
                    lw = 0.1f * scale,
                    al = 0.55f * scale,
                    ll = 0.45f * scale
                },
                UnitType.Zombie => new UnitDimensions
                {
                    head = 0.22f * scale,
                    tw = 0.32f * scale,
                    th = 0.48f * scale,
                    td = 0.23f * scale,
                    lw = 0.11f * scale,
                    al = 0.5f * scale,
                    ll = 0.5f * scale
                },
                UnitType.Heavy => new UnitDimensions
                {
                    head = 0.23f * scale,
                    tw = 0.5f * scale,
                    th = 0.55f * scale,
                    td = 0.35f * scale,
                    lw = 0.15f * scale,
                    al = 0.4f * scale,
                    ll = 0.5f * scale
                },
                UnitType.Scout => new UnitDimensions
                {
                    head = 0.22f * scale,
                    tw = 0.28f * scale,
                    th = 0.45f * scale,
                    td = 0.2f * scale,
                    lw = 0.09f * scale,
                    al = 0.42f * scale,
                    ll = 0.58f * scale
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

            if (type == UnitType.Soldier && bodyType == Unit.HumanBodyType.Feminine)
            {
                baseDimensions.head *= 0.98f;
                baseDimensions.tw *= 0.9f;
                baseDimensions.th *= 0.97f;
                baseDimensions.td *= 0.92f;
                baseDimensions.lw *= 0.9f;
                baseDimensions.al *= 0.96f;
                baseDimensions.ll *= 1.02f;
            }

            return baseDimensions;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // DESSIN DU CORPS (méthodes originales)
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawUnitBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s,
                                  UnitType type, Matrix r, float l, float a, UnitDimensions dims,
                                  bool hasWeapon = false, bool isAiming = false,
                                  Unit.HumanBodyType bodyType = Unit.HumanBodyType.Masculine)
        {
            switch (type)
            {
                case UnitType.Soldier: DrawSoldierBody(d, e, p, c, s, r, l, a, dims, bodyType); break;
                case UnitType.Alien: DrawAlienBody(d, e, p, c, s, r, l, a, dims); break;
                case UnitType.Zombie: DrawZombieBody(d, e, p, c, s, r, l, a, dims); break;
                case UnitType.Heavy: DrawHeavyBody(d, e, p, c, s, r, l, a, dims); break;
                case UnitType.Scout: DrawScoutBody(d, e, p, c, s, r, l, a, dims); break;
            }

            DrawSkeletonJoints(d, e, p, dims, r, c, l, a);

            if (hasWeapon)
            {
                DrawWeaponGripPose(d, e, p, dims, r, c, isAiming);
            }
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

            DrawRoundedHead(d, e, p, new Vector3(-dims.tw * 0.6f, dims.ll + dims.th * 0.9f, 0), dims.lw * 0.28f, jointColor, r);
            DrawRoundedHead(d, e, p, new Vector3(dims.tw * 0.6f, dims.ll + dims.th * 0.9f, 0), dims.lw * 0.28f, jointColor, r);

            float armPhase = MathHelper.Clamp(armSwing / 0.3f, -1f, 1f);
            float oppositeArmPhase = -armPhase;
            Vector3 leftElbow = new Vector3(-dims.tw * 0.6f, dims.ll + dims.th * (0.57f + Math.Max(0f, armPhase) * 0.06f), armPhase * dims.al * 0.32f);
            Vector3 rightElbow = new Vector3(dims.tw * 0.6f, dims.ll + dims.th * (0.57f + Math.Max(0f, oppositeArmPhase) * 0.06f), oppositeArmPhase * dims.al * 0.32f);
            DrawRoundedHead(d, e, p, leftElbow, dims.lw * 0.28f, jointColor, r);
            DrawRoundedHead(d, e, p, rightElbow, dims.lw * 0.28f, jointColor, r);
        }

        private void DrawWeaponGripPose(GraphicsDevice d, BasicEffect e, Vector3 p, UnitDimensions dims,
                                        Matrix r, Color bodyColor, bool isAiming)
        {
            Color armColor = bodyColor * 0.9f;
            float shoulderY = dims.ll + dims.th * 0.9f;
            float elbowY = dims.ll + dims.th * (isAiming ? 0.82f : 0.72f);
            float handY = dims.ll + dims.th * (isAiming ? 0.76f : 0.64f);
            float handZ = dims.td * (isAiming ? 0.95f : 0.45f);

            DrawRoundedCapsuleY(d, e, p, new Vector3(dims.tw * 0.58f, (shoulderY + elbowY) * 0.5f, handZ * 0.35f),
                dims.al * 0.52f, dims.lw * 0.5f, armColor, r, 4);
            DrawRoundedCapsuleY(d, e, p, new Vector3(dims.tw * 0.62f, (elbowY + handY) * 0.5f, handZ * 0.7f),
                dims.al * 0.48f, dims.lw * 0.46f, armColor * 0.95f, r, 4);

            DrawRoundedCapsuleY(d, e, p, new Vector3(-dims.tw * 0.58f, (shoulderY + elbowY) * 0.5f, handZ * 0.45f),
                dims.al * 0.46f, dims.lw * 0.5f, armColor * 0.95f, r, 4);
            DrawRoundedCapsuleY(d, e, p, new Vector3(-dims.tw * 0.42f, (elbowY + handY) * 0.5f, handZ * 0.85f),
                dims.al * 0.42f, dims.lw * 0.46f, armColor * 0.9f, r, 4);

            Color jointColor = bodyColor * 0.6f;
            DrawRoundedHead(d, e, p, new Vector3(dims.tw * 0.6f, elbowY, handZ * 0.5f), dims.lw * 0.3f, jointColor, r);
            DrawRoundedHead(d, e, p, new Vector3(-dims.tw * 0.5f, elbowY, handZ * 0.6f), dims.lw * 0.3f, jointColor, r);
        }

        private void DrawRunningLegPair(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                        UnitDimensions dims, float legSwing, float legSpread,
                                        float legRadiusScale, Color legColor, Color footColor)
        {
            DrawRunningLeg(d, e, p, r, dims, legSwing, -dims.tw * legSpread, legRadiusScale, legColor, footColor);
            DrawRunningLeg(d, e, p, r, dims, -legSwing, dims.tw * legSpread, legRadiusScale, legColor, footColor);
        }

        private void DrawSwingingArmPair(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                         UnitDimensions dims, float armSwing,
                                         float shoulderSpread, float shoulderHeight,
                                         float bendBias, float radiusScale, Color armColor)
        {
            DrawSwingingArm(d, e, p, r, dims, armSwing, -dims.tw * shoulderSpread, shoulderHeight, bendBias, radiusScale, armColor);
            DrawSwingingArm(d, e, p, r, dims, -armSwing, dims.tw * shoulderSpread, shoulderHeight, bendBias, radiusScale, armColor);
        }

        private void DrawSwingingArm(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                     UnitDimensions dims, float phase, float shoulderX,
                                     float shoulderHeight, float bendBias,
                                     float radiusScale, Color armColor)
        {
            float normalizedPhase = MathHelper.Clamp(phase / 0.3f, -1f, 1f);

            Vector3 shoulder = new Vector3(shoulderX, dims.ll + dims.th * shoulderHeight, -normalizedPhase * dims.al * 0.08f);
            Vector3 elbow = new Vector3(
                shoulderX,
                dims.ll + dims.th * (shoulderHeight - 0.33f) + Math.Max(0f, normalizedPhase) * dims.al * 0.18f,
                normalizedPhase * dims.al * 0.32f + bendBias * dims.al * 0.12f);
            Vector3 wrist = new Vector3(
                shoulderX,
                dims.ll + dims.th * (shoulderHeight - 0.66f) + Math.Max(0f, normalizedPhase) * dims.al * 0.1f,
                elbow.Z - Math.Min(0f, normalizedPhase) * dims.al * 0.22f + bendBias * dims.al * 0.1f);

            DrawRoundedCapsuleBetween(d, e, p, shoulder, elbow, dims.lw * radiusScale, armColor, r, 4);
            DrawRoundedCapsuleBetween(d, e, p, elbow, wrist, dims.lw * (radiusScale * 0.92f), armColor * 0.95f, r, 4);
        }

        private void DrawRunningLeg(GraphicsDevice d, BasicEffect e, Vector3 p, Matrix r,
                                    UnitDimensions dims, float phase, float hipX,
                                    float legRadiusScale, Color legColor, Color footColor)
        {
            float normalizedPhase = MathHelper.Clamp(phase / 0.42f, -1f, 1f);

            Vector3 hip = new Vector3(hipX, dims.ll, -normalizedPhase * dims.ll * 0.04f);
            Vector3 knee = new Vector3(
                hipX,
                dims.ll * (0.5f + Math.Max(0f, normalizedPhase) * 0.18f),
                normalizedPhase * dims.ll * 0.34f);
            Vector3 ankle = new Vector3(
                hipX,
                dims.ll * 0.08f + Math.Max(0f, normalizedPhase) * dims.ll * 0.12f,
                knee.Z - Math.Min(0f, normalizedPhase) * dims.ll * 0.28f + Math.Max(0f, normalizedPhase) * dims.ll * 0.06f);

            DrawRoundedCapsuleBetween(d, e, p, hip, knee, dims.lw * legRadiusScale, legColor, r, 4);
            DrawRoundedCapsuleBetween(d, e, p, knee, ankle, dims.lw * (legRadiusScale * 0.92f), legColor * 0.96f, r, 4);

            Vector3 bootPos = ankle + new Vector3(0f, dims.ll * 0.05f, dims.lw * (0.45f + Math.Max(0f, normalizedPhase) * 0.35f));
            Vector3 bootScale = new Vector3(dims.lw * (legRadiusScale * 2.35f), dims.ll * 0.2f, dims.lw * (legRadiusScale * 2.7f));
            DrawBodyPart(d, e, p, bootPos, bootScale, footColor, r);
        }

        private void DrawSoldier(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Soldier, s);
            DrawSoldierBody(d, e, p, c, s, r, l, a, dims, Unit.HumanBodyType.Masculine);
        }

        private void DrawSoldierBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                     float l, float a, UnitDimensions dims, Unit.HumanBodyType bodyType)
        {
            Color skin = new(220, 180, 140), dark = new(52, 58, 90);

            DrawRunningLegPair(d, e, p, r, dims, l, 0.3f, 0.55f, dark, dark * 0.8f);

            // Torse
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, 0),
                        new Vector3(dims.tw * 0.95f, dims.th, dims.td), c, r);

            // Poitrine plus triangulaire
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.8f, dims.td * 0.15f),
                        new Vector3(dims.tw * 0.75f, dims.th * 0.33f, dims.td * 0.85f), c * 0.8f, r);

            // Bras articulés (épaule → coude → poignet)
            DrawSwingingArmPair(d, e, p, r, dims, a, 0.6f, 0.9f, 0f, 0.52f, c * 0.85f);

            // Bracelets / avant-bras plus marqués
            DrawBodyPart(d, e, p, new Vector3(-dims.tw * 0.6f, dims.ll + dims.th * 0.5f, -a * 0.08f),
                        new Vector3(dims.lw * 1.5f, dims.al * 0.22f, dims.lw * 1.25f), new Color(55, 70, 100), r);
            DrawBodyPart(d, e, p, new Vector3(dims.tw * 0.6f, dims.ll + dims.th * 0.5f, a * 0.08f),
                        new Vector3(dims.lw * 1.5f, dims.al * 0.22f, dims.lw * 1.25f), new Color(55, 70, 100), r);

            // Tête
            DrawRoundedHead(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f, 0),
                dims.head * 0.52f, skin, r);

            DrawHumanHeadFeatures(d, e, p, c, r, dims, bodyType);
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
            float headY = dims.ll + dims.th + dims.head * 0.6f;
            Color hair = new(128, 84, 56);

            DrawBodyPart(d, e, p, new Vector3(0, headY + dims.head * 0.75f, 0),
                        new Vector3(dims.head * 0.95f, dims.head * 0.2f, dims.head * 0.95f), hair * 0.95f, r);
            DrawBodyPart(d, e, p, new Vector3(0, headY + dims.head * 0.2f, -dims.head * 0.5f),
                        new Vector3(dims.head * 0.55f, dims.head * 0.65f, dims.head * 0.28f), hair * 0.9f, r);
            DrawBodyPart(d, e, p, new Vector3(-dims.head * 0.52f, headY + dims.head * 0.3f, dims.head * 0.38f),
                        new Vector3(dims.head * 0.23f, dims.head * 0.35f, dims.head * 0.2f), hair, r);

            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.22f, dims.td * 0.05f),
                        new Vector3(dims.tw * 0.9f, dims.th * 0.11f, dims.td), new Color(88, 60, 64), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.45f, dims.td * 0.58f),
                        new Vector3(dims.tw * 0.35f, dims.th * 0.2f, dims.td * 0.2f), c * 0.75f, r);
        }

        private void DrawCloudInspiredFeatures(GraphicsDevice d, BasicEffect e, Vector3 p, Color c,
                                               Matrix r, UnitDimensions dims)
        {
            float headY = dims.ll + dims.th + dims.head * 0.6f;

            // Picots de cheveux façon Cloud (version low-poly inspirée)
            Color hair = new(236, 205, 96);
            DrawBodyPart(d, e, p, new Vector3(0, headY + dims.head * 0.82f, 0),
                        new Vector3(dims.head * 1.05f, dims.head * 0.24f, dims.head * 1.05f), hair * 0.95f, r);
            DrawBodyPart(d, e, p, new Vector3(-dims.head * 0.86f, headY + dims.head * 0.62f, dims.head * 0.06f),
                        new Vector3(dims.head * 0.42f, dims.head * 0.26f, dims.head * 0.34f), hair * 0.88f, r);
            DrawBodyPart(d, e, p, new Vector3(dims.head * 0.88f, headY + dims.head * 0.54f, 0),
                        new Vector3(dims.head * 0.55f, dims.head * 0.3f, dims.head * 0.4f), hair, r);
            DrawBodyPart(d, e, p, new Vector3(dims.head * 0.48f, headY + dims.head * 0.76f, dims.head * 0.66f),
                        new Vector3(dims.head * 0.34f, dims.head * 0.22f, dims.head * 0.34f), hair * 0.92f, r);

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
            DrawAlienBody(d, e, p, c, s, r, l, a, dims);
        }

        private void DrawAlienBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                   float l, float a, UnitDimensions dims)
        {
            Color skin = new(150, 200, 150), dark = c * 0.6f;
            DrawRunningLegPair(d, e, p, r, dims, l * 1.1f, 0.3f, 1f, dark, dark * 0.9f);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, 0), new Vector3(dims.tw, dims.th, dims.td), c, r);
            DrawSwingingArmPair(d, e, p, r, dims, a * 1.1f, 0.6f, 0.82f, 0.15f, 0.95f, c * 0.85f);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, 0), new Vector3(dims.head, dims.head * 1.3f, dims.head * 0.9f), skin, r);
            DrawBodyPart(d, e, p, new Vector3(-dims.head * 0.3f, dims.ll + dims.th + dims.head * 0.5f + 0.1f * s, dims.head * 0.5f), new Vector3(dims.head * 0.2f, dims.head * 0.25f, dims.head * 0.1f), Color.Black, r);
            DrawBodyPart(d, e, p, new Vector3(dims.head * 0.3f, dims.ll + dims.th + dims.head * 0.5f + 0.1f * s, dims.head * 0.5f), new Vector3(dims.head * 0.2f, dims.head * 0.25f, dims.head * 0.1f), Color.Black, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, dims.head * 0.55f), new Vector3(dims.head * 0.8f, dims.head * 0.15f, dims.head * 0.05f), new Color(0, 255, 0), r);
        }

        private void DrawZombie(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Zombie, s);
            DrawZombieBody(d, e, p, c, s, r, l, a, dims);
        }

        private void DrawZombieBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                    float l, float a, UnitDimensions dims)
        {
            Color skin = new(140, 160, 130), dark = new(80, 70, 60);
            DrawRunningLegPair(d, e, p, r, dims, l * 1.15f, 0.35f, 1f, dark, dark * 0.85f);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, -0.1f * s), new Vector3(dims.tw, dims.th, dims.td), c * 0.7f, r);
            DrawSwingingArmPair(d, e, p, r, dims, a * 0.8f, 0.6f, 0.72f, 0.45f, 1f, c * 0.6f);
            DrawRoundedHead(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, -0.05f * s), dims.head * 0.5f, skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, dims.head * 0.55f), new Vector3(dims.head * 0.5f, dims.head * 0.2f, dims.head * 0.05f), new Color(180, 0, 0), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.4f, dims.td * 0.9f), new Vector3(dims.lw * 1.5f, dims.lw, dims.lw * 2f), c * 0.5f, r);
        }

        private void DrawHeavy(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Heavy, s);
            DrawHeavyBody(d, e, p, c, s, r, l, a, dims);
        }

        private void DrawHeavyBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                   float l, float a, UnitDimensions dims)
        {
            Color skin = new(220, 180, 140), dark = new(50, 50, 70);
            DrawRunningLegPair(d, e, p, r, dims, l * 0.9f, 0.3f, 1.2f, dark, dark * 0.8f);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, 0), new Vector3(dims.tw, dims.th, dims.td), c, r);
            DrawSwingingArmPair(d, e, p, r, dims, a * 0.75f, 0.65f, 0.88f, -0.1f, 1.25f, c * 0.85f);
            DrawRoundedHead(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, 0), dims.head * 0.56f, skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.5f, dims.head * 0.6f), new Vector3(dims.head * 0.8f, dims.head * 0.4f, dims.head * 0.15f), new Color(30, 30, 30), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, dims.td * 0.9f), new Vector3(dims.tw * 0.4f, dims.th * 0.4f, dims.td * 0.7f), new Color(60, 60, 60), r);

            DrawCloudInspiredFeatures(d, e, p, c, r, dims);
        }

        private void DrawScout(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r, float l = 0f, float a = 0f)
        {
            var dims = GetUnitDimensions(UnitType.Scout, s);
            DrawScoutBody(d, e, p, c, s, r, l, a, dims);
        }

        private void DrawScoutBody(GraphicsDevice d, BasicEffect e, Vector3 p, Color c, float s, Matrix r,
                                   float l, float a, UnitDimensions dims)
        {
            Color skin = new(220, 180, 140), dark = new(70, 70, 90);
            DrawRunningLegPair(d, e, p, r, dims, l * 1.2f, 0.25f, 1f, dark, dark * 0.85f);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th * 0.5f, 0), new Vector3(dims.tw, dims.th, dims.td), c, r);
            DrawSwingingArmPair(d, e, p, r, dims, a * 1.2f, 0.55f, 0.9f, 0f, 0.95f, c * 0.85f);
            DrawRoundedHead(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f, 0), dims.head * 0.5f, skin, r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f + 0.05f * s, dims.head * 0.6f), new Vector3(dims.head * 0.7f, dims.head * 0.25f, dims.head * 0.08f), new Color(50, 100, 150), r);
            DrawBodyPart(d, e, p, new Vector3(0, dims.ll + dims.th + dims.head * 0.6f, dims.head * 0.8f), new Vector3(dims.head * 0.1f, dims.head * 0.1f, dims.head * 0.35f), new Color(255, 0, 0), r);

            DrawCloudInspiredFeatures(d, e, p, c, r, dims);
        }
    }
}
