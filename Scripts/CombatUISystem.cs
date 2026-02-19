using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using XCOM_3.Scripts;

namespace XCOM_3
{

    public class FireTargetUI
    {
        public Unit Target;
        public Rectangle Bounds;
        public int HitChance;
    }
    /// <summary>
    /// Gère l'interface utilisateur du combat - STYLE PARASITE EVE 2
    /// </summary>
    public class CombatUISystem
    {
        private GraphicsDevice graphicsDevice;
        private SpriteBatch spriteBatch;
        private SpriteFont font;
        private Texture2D pixel;
        private float pulseTimer = 0f; // Pour les effets de pulsation

        // État UI
        public Unit SelectedFireTarget { get; set; }
        public Unit HoveredFireTarget { get; private set; }
        public bool ShowFireTargets { get; set; }
        public List<FireTargetUI> FireTargetsUI { get; private set; } = new List<FireTargetUI>();
        public List<Button> UnitActionButtons { get; private set; } = new List<Button>();

        // Boutons
        public Rectangle FireButton { get; private set; }
        public Rectangle EndTurnButton { get; private set; }
        public Rectangle WeaponPreviewBounds { get; private set; }
        public bool FireButtonHovered { get; private set; }
        public bool EndTurnHovered { get; private set; }

        // Constantes
        public const int ActionButtonWidth = 48;
        public const int ActionButtonHeight = 48;

        public CombatUISystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch,
            SpriteFont font, Texture2D pixel)
        {
            this.graphicsDevice = graphicsDevice;
            this.spriteBatch = spriteBatch;
            this.font = font;
            this.pixel = pixel;
        }

        public void Update(GameTime gameTime)
        {
            pulseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        /// <summary>
        /// Met à jour les cibles de tir
        /// </summary>
        public void UpdateFireTargets(Unit selectedUnit, List<Unit> validTargets)
        {
            Unit previousSelection = SelectedFireTarget;
            FireTargetsUI.Clear();
            SelectedFireTarget = null;

            if (selectedUnit == null || selectedUnit.Team != Team.Player || selectedUnit.ActionPoints <= 0)
            {
                ShowFireTargets = false;
                return;
            }

            ShowFireTargets = validTargets.Count > 0;

            for (int i = 0; i < validTargets.Count; i++)
            {
                var target = validTargets[i];
                FireTargetsUI.Add(new FireTargetUI
                {
                    Target = target,
                    HitChance = 0,
                    Bounds = Rectangle.Empty
                });
            }

            if (previousSelection != null)
            {
                SelectedFireTarget = FireTargetsUI
                    .Select(ui => ui.Target)
                    .FirstOrDefault(target => target == previousSelection);
            }

            UpdateFireTargetHitChances(selectedUnit, selectedUnit.Cell);

            if (SelectedFireTarget == null)
            {
                SelectedFireTarget = FireTargetsUI
                    .OrderByDescending(ui => ui.HitChance)
                    .Select(ui => ui.Target)
                    .FirstOrDefault();
            }

            UpdateFireTargetsUIPositions(selectedUnit);
        }

        public void UpdateFireTargetHitChances(Unit selectedUnit, Point firingCell)
        {
            if (selectedUnit == null || selectedUnit.WeaponData == null)
                return;

            foreach (var ui in FireTargetsUI)
            {
                int distance = Math.Abs(ui.Target.Cell.X - firingCell.X) +
                              Math.Abs(ui.Target.Cell.Y - firingCell.Y);
                int anaerobicPenalty = selectedUnit.GetAnaerobicAccuracyPenalty();
                int rangePenalty = Math.Max(0, distance * 5);
                int fireModePenalty = selectedUnit.GetFireModeAccuracyPenaltyForNextShot();
                ui.HitChance = Math.Max(selectedUnit.WeaponData.EffectiveAccuracy - rangePenalty - anaerobicPenalty - fireModePenalty, 10);
            }
        }

        /// <summary>
        /// Met à jour les positions des icônes de cibles
        /// </summary>
        public void UpdateFireTargetsUIPositions(Unit selectedUnit)
        {
            if (FireTargetsUI.Count == 0)
                return;

            int icon = 52, space = 8;
            int total = FireTargetsUI.Count * icon + (FireTargetsUI.Count - 1) * space;

            int startX, y;
            int bh = ActionButtonHeight;
            int by = graphicsDevice.Viewport.Height - bh - 20;

            if (SelectedFireTarget != null && selectedUnit != null && selectedUnit.ActionPoints > 0)
            {
                int targetRowY = by - icon - 15;
                int fireConfirmWidth = 180;
                int fireConfirmHeight = 50;
                int fireButtonX = graphicsDevice.Viewport.Width / 2 - fireConfirmWidth / 2;

                startX = fireButtonX + (fireConfirmWidth - total) / 2;
                y = targetRowY;
            }
            else
            {
                int bx = (graphicsDevice.Viewport.Width - ActionButtonWidth) / 2;
                startX = bx - 130 + (ActionButtonWidth - total) / 2;
                y = by - icon - 15;
            }

            for (int i = 0; i < FireTargetsUI.Count; i++)
            {
                FireTargetsUI[i].Bounds = new Rectangle(startX + i * (icon + space), y, icon, icon);
            }
        }

        /// <summary>
        /// Dessine le panneau d'info de l'unité - STYLE PE2
        /// </summary>
        public void DrawUnitInfoPanel(Unit selectedUnit, Dictionary<string, GrenadeData> grenadeDatabase)
        {
            if (selectedUnit == null)
            {
                WeaponPreviewBounds = Rectangle.Empty;
                return;
            }

            int m = 15, w = 320, h = 240;
            int x = m, y = graphicsDevice.Viewport.Height - h - m;
            Rectangle panel = new Rectangle(x, y, w, h);
            int padding = 12;

            int innerLeft = panel.X + padding;
            int innerRight = panel.Right - padding;
            int innerWidth = innerRight - innerLeft;


            // ✅ Panel PE2
            ParasiteEveTheme.DrawPanel(spriteBatch, pixel, panel);

            // Header
            Rectangle header = new Rectangle(x, y, w, 30);
            ParasiteEveTheme.DrawSectionHeader(spriteBatch, pixel, font, header, selectedUnit.Name.ToUpper());

            Vector2 p = new Vector2(x + 12, y + 40);

            Rectangle weaponPreview = new Rectangle(innerRight - 94, y + 38, 82, 52);
            WeaponPreviewBounds = weaponPreview;
            DrawWeaponPreview(selectedUnit, weaponPreview);

            // Classe et Arme
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                $"CLASS: {selectedUnit.Class}", p, ParasiteEveTheme.TextNormal);

            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                $"WEAPON: {selectedUnit.Weapon}", p + new Vector2(0, 22), ParasiteEveTheme.TextNormal);

            if (selectedUnit.WeaponData != null && selectedUnit.WeaponData.UsesAmmo)
            {
                string fireModeLabel = GetFireModeLabel(selectedUnit.WeaponData.CurrentFireMode);
                Color modeColor = selectedUnit.WeaponData.CanCycleFireMode
                    ? new Color(190, 225, 255)
                    : ParasiteEveTheme.TextDim;
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    $"MODE: {fireModeLabel}", p + new Vector2(0, 66), modeColor, 0.7f);
            }

            if (selectedUnit.WeaponData != null && selectedUnit.WeaponData.UsesAmmo)
            {
                selectedUnit.EnsureAmmoState();
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    $"AMMO: {selectedUnit.CurrentAmmoInMagazine}/{selectedUnit.WeaponData.EffectiveMagazineCapacity}",
                    p + new Vector2(0, 44), ParasiteEveTheme.TextDim, 0.8f);
            }

            // Barre de santé
            p.Y += 72;
            spriteBatch.DrawString(font, "HP", p, ParasiteEveTheme.TextHighlight);
            int barX = innerLeft + 40; // espace pour le texte "HP"
            int barWidth = innerRight - barX;

            Rectangle hpBar = new Rectangle(barX, (int)p.Y, barWidth, 16);
            ParasiteEveTheme.DrawHealthBar(spriteBatch, pixel, hpBar, selectedUnit.Health, selectedUnit.MaxHealth);

            string hpText = $"{selectedUnit.Health} / {selectedUnit.MaxHealth}";
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, hpText,
                new Vector2(hpBar.Right - font.MeasureString(hpText).X, p.Y),
                ParasiteEveTheme.TextNormal, 0.8f);


            // Barre de MP (Action Points)
            p.Y += 25;
            spriteBatch.DrawString(font, "AP", p, ParasiteEveTheme.TextHighlight);
            Rectangle apBar = new Rectangle(barX, (int)p.Y, barWidth, 16);
            ParasiteEveTheme.DrawProgressBar(spriteBatch, pixel, apBar,
                selectedUnit.ActionPoints, 3, ParasiteEveTheme.BarMP);

            string apText = $"{selectedUnit.ActionPoints}";
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, apText,
                new Vector2(apBar.Right - font.MeasureString(apText).X, p.Y),
                ParasiteEveTheme.TextNormal, 0.8f);

            // Barre de phosphocréatine
            p.Y += 25;
            spriteBatch.DrawString(font, "PCr", p, new Color(255, 200, 50));
            Rectangle phosphocreatineBar = new Rectangle(x + 60, (int)p.Y, w - 70, 16);
            ParasiteEveTheme.DrawProgressBar(spriteBatch, pixel, phosphocreatineBar,
                selectedUnit.Phosphocreatine, selectedUnit.MaxPhosphocreatine, new Color(255, 200, 50));

            string phosphocreatineText = $"{selectedUnit.Phosphocreatine}%";
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, phosphocreatineText,
                new Vector2(phosphocreatineBar.X + phosphocreatineBar.Width + 5, p.Y), ParasiteEveTheme.TextNormal, 0.8f);

            // Barre de fatigue anaérobie (lactate)
            p.Y += 25;
            spriteBatch.DrawString(font, "LAC", p, new Color(255, 120, 80));
            Rectangle lactateBar = new Rectangle(x + 60, (int)p.Y, w - 70, 16);
            ParasiteEveTheme.DrawProgressBar(spriteBatch, pixel, lactateBar,
                selectedUnit.AnaerobicFatigue, selectedUnit.MaxAnaerobicFatigue, new Color(255, 120, 80));

            int anaerobicPenalty = selectedUnit.GetAnaerobicAccuracyPenalty();
            string lactateText = $"{selectedUnit.AnaerobicFatigue}% (-{anaerobicPenalty}% ACC)";
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, lactateText,
                new Vector2(lactateBar.X + 2, p.Y + 1), ParasiteEveTheme.TextNormal, 0.6f);

            // Indicateur si phosphocréatine basse
            if (!selectedUnit.CanSprint())
            {
                p.Y += 20;
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "⚠ PCR LOW",
                    p, ParasiteEveTheme.TextWarning, 0.7f);
            }


            // Protection balistique
            p.Y += 28;
            string protectionLabel = Unit.GetProtectionLabel(selectedUnit.GetBestProtectionLevel());
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                $"PROTECTION: {protectionLabel}", p, ParasiteEveTheme.BarXP);

            // Niveau
            p.Y += 22;
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                $"LV: {selectedUnit.Skills.OverallLevel}", p, new Color(255, 220, 100));

            // Grenades
            p.Y += 28;
            spriteBatch.DrawString(font, "GRENADES", p, ParasiteEveTheme.TextHighlight);
            p.Y += 20;

            int grenadeSize = 30;
            int spacing = 5;

            int maxPerRow = innerWidth / (grenadeSize + spacing);

            for (int i = 0; i < selectedUnit.Grenades.Count; i++)
            {
                int row = i / maxPerRow;
                int col = i % maxPerRow;

                int gx = innerLeft + col * (grenadeSize + spacing);
                int gy = (int)p.Y + row * (grenadeSize + spacing);

                Rectangle grenadeIcon = new Rectangle(gx, gy, grenadeSize, grenadeSize);



                var grenade = selectedUnit.Grenades[i];
                string symbol = GrenadeDatabase.GetGrenadeSymbol(grenade.Type);
                Color color = GrenadeDatabase.GetGrenadeColor(grenade.Type);

                ParasiteEveTheme.DrawPanel(spriteBatch, pixel, grenadeIcon, false);
                spriteBatch.Draw(pixel, grenadeIcon, color * 0.3f);

                Vector2 symbolSize = font.MeasureString(symbol);
                spriteBatch.DrawString(font, symbol,
                    new Vector2(grenadeIcon.Center.X - symbolSize.X / 2,
                               grenadeIcon.Center.Y - symbolSize.Y / 2),
                    color, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            }

            // Scanlines effect
            ParasiteEveTheme.DrawScanlines(spriteBatch, pixel, panel, 0.08f);
        }

        public bool TryCycleWeaponFireModeAt(Point mousePosition, Unit selectedUnit)
        {
            if (selectedUnit?.WeaponData == null || !WeaponPreviewBounds.Contains(mousePosition))
                return false;

            if (!selectedUnit.WeaponData.CycleFireMode())
                return false;

            return true;
        }

        private void DrawWeaponPreview(Unit unit, Rectangle bounds)
        {
            ParasiteEveTheme.DrawPanel(spriteBatch, pixel, bounds, false);
            spriteBatch.Draw(pixel, bounds, new Color(40, 54, 70) * 0.45f);

            Color iconColor = new Color(180, 220, 255);
            DrawWeaponSilhouette(unit?.WeaponData?.Type ?? WeaponType.AssaultRifle, bounds, iconColor);

            if (unit?.WeaponData?.CanCycleFireMode == true)
            {
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "RMB", new Vector2(bounds.X + 4, bounds.Bottom - 15), ParasiteEveTheme.TextHighlight, 0.55f);
            }
        }

        private void DrawWeaponSilhouette(WeaponType type, Rectangle bounds, Color color)
        {
            int centerY = bounds.Center.Y;
            int left = bounds.X + 8;
            int right = bounds.Right - 8;

            switch (type)
            {
                case WeaponType.Pistol:
                case WeaponType.Revolver:
                    spriteBatch.Draw(pixel, new Rectangle(left + 8, centerY - 4, 24, 6), color);
                    spriteBatch.Draw(pixel, new Rectangle(left + 12, centerY + 2, 8, 10), color);
                    break;

                case WeaponType.Shotgun:
                    spriteBatch.Draw(pixel, new Rectangle(left, centerY - 3, right - left - 4, 4), color);
                    spriteBatch.Draw(pixel, new Rectangle(left + 10, centerY + 1, 18, 7), color);
                    spriteBatch.Draw(pixel, new Rectangle(left + 28, centerY + 4, 20, 4), color * 0.8f);
                    break;

                default:
                    spriteBatch.Draw(pixel, new Rectangle(left, centerY - 3, right - left, 4), color);
                    spriteBatch.Draw(pixel, new Rectangle(left + 18, centerY - 9, 14, 6), color);
                    spriteBatch.Draw(pixel, new Rectangle(left + 24, centerY + 1, 10, 10), color);
                    spriteBatch.Draw(pixel, new Rectangle(right - 10, centerY - 6, 8, 10), color * 0.85f);
                    break;
            }
        }

        private static string GetFireModeLabel(FireMode mode)
        {
            return mode switch
            {
                FireMode.Single => "SEMI",
                FireMode.Burst => "BURST",
                FireMode.Auto => "AUTO",
                _ => "SEMI"
            };
        }

        /// <summary>
        /// Dessine les boutons d'action - STYLE PE2
        /// </summary>
        public void DrawActionButtons(Unit selectedUnit, MouseState mouse, bool hasDetonatableCharges = false)
        {
            UnitActionButtons.Clear();
            string hoveredActionTooltip = null;

            bool hasFirearmEquipped = selectedUnit?.EquippedWeapon?.Data?.WeaponData != null
                && selectedUnit.EquippedWeapon.Data.WeaponData.Type != WeaponType.Melee;
            bool hasGrapplingHookEquipped = string.Equals(selectedUnit?.EquippedRightHandFlashlight?.Data?.Name, "Grappin tactique", StringComparison.OrdinalIgnoreCase)
                || string.Equals(selectedUnit?.EquippedLeftHandFlashlight?.Data?.Name, "Grappin tactique", StringComparison.OrdinalIgnoreCase);

            int bw = ActionButtonWidth, bh = ActionButtonHeight;
            int by = graphicsDevice.Viewport.Height - bh - 20;
            int spacing = 12;

            var buttons = new List<Button>();

            if (hasFirearmEquipped)
            {
                buttons.Add(new Button("FIRE", Vector2.Zero));
                buttons.Add(new Button("RELOAD", Vector2.Zero));
                buttons.Add(new Button("OVERWATCH", Vector2.Zero));
            }

            if (selectedUnit != null)
            {
                buttons.Add(new Button("ANAEROBIC", Vector2.Zero));
            }

            bool hasThrowableGrenade = selectedUnit != null && selectedUnit.Grenades.Any(g => g.Type != GrenadeType.SatchelC4);
            bool hasSatchelCharge = selectedUnit != null && selectedUnit.Grenades.Any(g => g.Type == GrenadeType.SatchelC4);

            if (hasThrowableGrenade)
            {
                buttons.Add(new Button("GRENADE", Vector2.Zero));
            }

            if (hasSatchelCharge)
            {
                buttons.Add(new Button("C4-POSE", Vector2.Zero));
            }

            if (hasDetonatableCharges)
            {
                buttons.Add(new Button("DETONATE", Vector2.Zero));
            }

            if (hasGrapplingHookEquipped)
            {
                buttons.Add(new Button("GRAPPLIN", Vector2.Zero));
            }

            if (buttons.Count > 0)
            {
                int totalWidth = buttons.Count * bw + (buttons.Count - 1) * spacing;
                int startX = graphicsDevice.Viewport.Width / 2 - totalWidth / 2;

                for (int i = 0; i < buttons.Count; i++)
                {
                    buttons[i].Position = new Vector2(startX + i * (bw + spacing), by);
                }
            }

            UnitActionButtons.AddRange(buttons);

            foreach (var btn in UnitActionButtons)
            {
                Rectangle r = new Rectangle((int)btn.Position.X, (int)btn.Position.Y, bw, bh);
                bool isHovered = r.Contains(mouse.Position);

                if (isHovered)
                {
                    hoveredActionTooltip = GetActionTooltip(btn.Text);
                }

                Color bgColor = isHovered ? ParasiteEveTheme.ButtonHover : ParasiteEveTheme.ButtonNormal;
                spriteBatch.Draw(pixel, r, bgColor);

                Color borderColor = isHovered ? ParasiteEveTheme.SelectionOutline : ParasiteEveTheme.BorderColor;
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, r, borderColor, 2);

                DrawActionIcon(btn.Text, r, isHovered);
            }

            if (!string.IsNullOrWhiteSpace(hoveredActionTooltip))
            {
                DrawActionTooltip(mouse.Position, hoveredActionTooltip);
            }

            // Bouton CONFIRMER TIR
            if (hasFirearmEquipped && SelectedFireTarget != null && selectedUnit != null && selectedUnit.ActionPoints > 0)
            {
                int icon = 52;
                int fireConfirmWidth = 180;
                int fireConfirmHeight = 50;
                int targetRowY = by - icon - 15;
                FireButton = new Rectangle(
                    graphicsDevice.Viewport.Width / 2 - fireConfirmWidth / 2,
                    targetRowY - fireConfirmHeight - 15,
                    fireConfirmWidth,
                    fireConfirmHeight
                );

                bool isHovered = FireButton.Contains(mouse.Position);

                // Fond spécial pour le bouton de tir (rouge)
                Color bgColor = isHovered ? new Color(180, 60, 60) : new Color(140, 40, 40);
                spriteBatch.Draw(pixel, FireButton, bgColor);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, FireButton,
                    isHovered ? new Color(255, 100, 100) : ParasiteEveTheme.BorderColor, 2);

                string fireText = ">> CONFIRM FIRE <<";
                Vector2 fireTextSize = font.MeasureString(fireText);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, fireText,
                    new Vector2(FireButton.X + (fireConfirmWidth - fireTextSize.X) / 2,
                               FireButton.Y + (fireConfirmHeight - fireTextSize.Y) / 2),
                    isHovered ? Color.White : new Color(255, 200, 200));
            }
            else
            {
                FireButton = Rectangle.Empty;
            }
        }

        private void DrawActionIcon(string action, Rectangle bounds, bool isHovered)
        {
            Color iconColor = isHovered ? Color.White : ParasiteEveTheme.TextNormal;

            switch (action)
            {
                case "FIRE":
                    DrawTargetIcon(bounds, iconColor);
                    break;
                case "RELOAD":
                    DrawMagazineIcon(bounds, iconColor);
                    break;
                case "GRENADE":
                    DrawGrenadeIcon(bounds, iconColor);
                    break;
                case "OVERWATCH":
                    DrawEyeIcon(bounds, iconColor);
                    break;
                case "GRAPPLIN":
                    DrawGrappleIcon(bounds, iconColor);
                    break;
                case "C4-POSE":
                    DrawC4Icon(bounds, iconColor);
                    break;
                case "DETONATE":
                    DrawDetonateIcon(bounds, iconColor);
                    break;
                default:
                    Vector2 textSize = font.MeasureString(action);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, action,
                        new Vector2(bounds.Center.X - textSize.X / 2, bounds.Center.Y - textSize.Y / 2),
                        iconColor, 0.6f);
                    break;
            }
        }

        private string GetActionTooltip(string action)
        {
            return action switch
            {
                "FIRE" => "Tirer sur la cible sélectionnée (coût: 1 AP)",
                "RELOAD" => "Recharger l'arme",
                "OVERWATCH" => "Passer en tir de réaction",
                "ANAEROBIC" => "Effort anaérobie: +1 AP, mais malus de précision au prochain tour",
                "GRENADE" => "Lancer une grenade",
                "C4-POSE" => "Poser une charge C4",
                "DETONATE" => "Détoner toutes les charges posées",
                "GRAPPLIN" => "Utiliser le grappin tactique",
                _ => action
            };
        }

        private void DrawActionTooltip(Point mousePosition, string tooltip)
        {
            const int padding = 8;
            Vector2 textSize = font.MeasureString(tooltip);

            int tooltipWidth = (int)MathF.Ceiling(textSize.X + padding * 2);
            int tooltipHeight = (int)MathF.Ceiling(textSize.Y + padding * 2);
            int tooltipX = mousePosition.X + 14;
            int tooltipY = mousePosition.Y - tooltipHeight - 12;

            if (tooltipX + tooltipWidth > graphicsDevice.Viewport.Width - 8)
                tooltipX = graphicsDevice.Viewport.Width - tooltipWidth - 8;

            if (tooltipY < 8)
                tooltipY = mousePosition.Y + 14;

            Rectangle tooltipBounds = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

            spriteBatch.Draw(pixel, tooltipBounds, new Color(16, 20, 30, 235));
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, tooltipBounds, ParasiteEveTheme.SelectionOutline, 2);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, tooltip,
                new Vector2(tooltipBounds.X + padding, tooltipBounds.Y + padding),
                ParasiteEveTheme.TextNormal, 0.62f);
        }

        private void DrawTargetIcon(Rectangle bounds, Color color)
        {
            int cx = bounds.Center.X;
            int cy = bounds.Center.Y;
            int r = 12;

            DrawCircleOutline(cx, cy, r, color, 2);
            DrawCircleOutline(cx, cy, 5, color, 2);
            spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - r - 4, 2, r - 2), color);
            spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy + 2, 2, r + 2), color);
            spriteBatch.Draw(pixel, new Rectangle(cx - r - 4, cy - 1, r - 2, 2), color);
            spriteBatch.Draw(pixel, new Rectangle(cx + 2, cy - 1, r + 2, 2), color);
        }

        private void DrawMagazineIcon(Rectangle bounds, Color color)
        {
            Rectangle magBody = new Rectangle(bounds.Center.X - 8, bounds.Center.Y - 10, 16, 20);
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, magBody, color, 2);
            spriteBatch.Draw(pixel, new Rectangle(magBody.X + 3, magBody.Y + 3, magBody.Width - 6, magBody.Height - 6), color * 0.35f);
            spriteBatch.Draw(pixel, new Rectangle(magBody.X + 3, magBody.Y - 4, magBody.Width - 6, 3), color);
        }

        private void DrawGrenadeIcon(Rectangle bounds, Color color)
        {
            int cx = bounds.Center.X;
            int cy = bounds.Center.Y + 2;
            DrawCircleOutline(cx, cy, 9, color, 2);
            spriteBatch.Draw(pixel, new Rectangle(cx - 2, cy - 16, 4, 6), color);
            spriteBatch.Draw(pixel, new Rectangle(cx + 2, cy - 16, 6, 2), color);
        }



        private void DrawC4Icon(Rectangle bounds, Color color)
        {
            Rectangle satchel = new Rectangle(bounds.Center.X - 11, bounds.Center.Y - 7, 22, 14);
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, satchel, color, 2);
            spriteBatch.Draw(pixel, new Rectangle(satchel.X + 2, satchel.Y + 2, satchel.Width - 4, satchel.Height - 4), color * 0.25f);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Center.X - 1, satchel.Y - 6, 2, 6), color);
            spriteBatch.Draw(pixel, new Rectangle(bounds.Center.X - 5, satchel.Y - 8, 10, 2), color);
        }

        private void DrawDetonateIcon(Rectangle bounds, Color color)
        {
            int cx = bounds.Center.X;
            int cy = bounds.Center.Y;
            DrawCircleOutline(cx, cy, 8, color, 2);
            spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 14, 2, 6), color);
            spriteBatch.Draw(pixel, new Rectangle(cx - 6, cy - 14, 12, 2), color);

            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                int px = cx + (int)(MathF.Cos(angle) * 14f);
                int py = cy + (int)(MathF.Sin(angle) * 14f);
                spriteBatch.Draw(pixel, new Rectangle(px - 1, py - 1, 2, 2), color);
            }
        }

        private void DrawCircleOutline(int centerX, int centerY, int radius, Color color, int thickness)
        {
            int safeThickness = Math.Max(1, thickness);

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int distSquared = x * x + y * y;
                    if (distSquared > radius * radius || distSquared < (radius - safeThickness) * (radius - safeThickness))
                        continue;

                    spriteBatch.Draw(pixel, new Rectangle(centerX + x, centerY + y, 1, 1), color);
                }
            }
        }
        private void DrawEyeIcon(Rectangle bounds, Color color)
        {
            int cx = bounds.Center.X;
            int cy = bounds.Center.Y;
            Rectangle eye = new Rectangle(cx - 14, cy - 8, 28, 16);

            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, eye, color, 2);
            DrawCircleOutline(cx, cy, 5, color, 2);
            spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - 1, 2, 2), color);
        }

        private void DrawGrappleIcon(Rectangle bounds, Color color)
        {
            int cx = bounds.Center.X;
            int cy = bounds.Center.Y;

            spriteBatch.Draw(pixel, new Rectangle(cx - 2, cy - 12, 4, 18), color);
            spriteBatch.Draw(pixel, new Rectangle(cx - 9, cy + 4, 18, 3), color);
            spriteBatch.Draw(pixel, new Rectangle(cx - 13, cy - 11, 9, 3), color);
            spriteBatch.Draw(pixel, new Rectangle(cx + 4, cy - 11, 9, 3), color);
            spriteBatch.Draw(pixel, new Rectangle(cx - 14, cy - 11, 2, 10), color);
            spriteBatch.Draw(pixel, new Rectangle(cx + 12, cy - 11, 2, 10), color);
            DrawCircleOutline(cx, cy + 9, 4, color, 2);
        }

        /// <summary>
        /// Dessine le bouton de fin de tour - STYLE PE2
        /// </summary>
        public void DrawEndTurnButton(MouseState mouse)
        {
            int w = 160, h = 42;
            EndTurnButton = new Rectangle(
                graphicsDevice.Viewport.Width - w - 20,
                graphicsDevice.Viewport.Height - h - 20,
                w, h
            );

            EndTurnHovered = EndTurnButton.Contains(mouse.Position);

            // Couleur rouge pour end turn
            Color bgColor = EndTurnHovered ? new Color(180, 60, 60) : new Color(120, 40, 40);
            spriteBatch.Draw(pixel, EndTurnButton, bgColor);

            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, EndTurnButton,
                EndTurnHovered ? new Color(255, 150, 150) : ParasiteEveTheme.BorderColor, 2);

            Vector2 txtSize = font.MeasureString("END TURN");
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "END TURN",
                new Vector2(EndTurnButton.X + (w - txtSize.X) / 2,
                           EndTurnButton.Y + (h - txtSize.Y) / 2),
                EndTurnHovered ? Color.White : ParasiteEveTheme.TextNormal);
        }

        /// <summary>
        /// Dessine les icônes de cibles de tir - STYLE PE2
        /// </summary>
        public void DrawFireTargets(MouseState mouse)
        {
            HoveredFireTarget = null;

            foreach (var ui in FireTargetsUI)
            {
                if (ui.Bounds.Contains(mouse.Position))
                {
                    HoveredFireTarget = ui.Target;
                    break;
                }
            }

            foreach (var ui in FireTargetsUI)
            {
                bool isSelected = ui.Target == SelectedFireTarget;
                bool isHovered = ui.Target == HoveredFireTarget;

                // Fond
                Color bg = isSelected ? new Color(140, 40, 40) :
                          isHovered ? ParasiteEveTheme.ButtonHover :
                          ParasiteEveTheme.ButtonNormal;

                spriteBatch.Draw(pixel, ui.Bounds, bg);

                // Bordure
                Color borderColor = isSelected || isHovered ?
                    ParasiteEveTheme.SelectionOutline :
                    ParasiteEveTheme.BorderColor;
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, ui.Bounds, borderColor, 2);

                // Pourcentage de chance de toucher
                string chanceText = ui.HitChance + "%";
                Vector2 size = font.MeasureString(chanceText);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, chanceText,
                    new Vector2(ui.Bounds.Center.X - size.X / 2, ui.Bounds.Center.Y - size.Y / 2),
                    ui.HitChance >= 75 ? ParasiteEveTheme.BarHealth :
                    ui.HitChance >= 50 ? ParasiteEveTheme.TextWarning :
                    ParasiteEveTheme.TextDanger);
            }

            // Effet de sélection pulsant
            Unit highlight = SelectedFireTarget ?? HoveredFireTarget;
            if (highlight != null)
            {
                var ui = FireTargetsUI.FirstOrDefault(f => f.Target == highlight);
                if (ui != null)
                {
                    ParasiteEveTheme.DrawSelectionIndicator(spriteBatch, pixel, ui.Bounds, pulseTimer);
                }
            }
        }

        /// <summary>
        /// Vérifie si la souris est sur un bouton d'action
        /// </summary>
        public bool IsMouseOverActionButton(MouseState mouse)
        {
            foreach (var btn in UnitActionButtons)
            {
                Rectangle r = new Rectangle((int)btn.Position.X, (int)btn.Position.Y,
                    ActionButtonWidth, ActionButtonHeight);

                if (r.Contains(mouse.Position))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Vérifie si la souris est sur une icône de cible
        /// </summary>
        public bool IsMouseOverFireTargets(MouseState mouse)
        {
            if (!ShowFireTargets)
                return false;

            foreach (var ui in FireTargetsUI)
            {
                if (ui.Bounds.Contains(mouse.Position))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gère le clic sur les icônes de cibles
        /// </summary>
        public bool HandleFireTargetClick(MouseState mouse, Unit selectedUnit)
        {
            if (!ShowFireTargets)
                return false;

            foreach (var ui in FireTargetsUI)
            {
                if (ui.Bounds.Contains(mouse.Position))
                {
                    SelectedFireTarget = ui.Target;

                    if (selectedUnit != null && SelectedFireTarget != null)
                    {
                        float deltaX = SelectedFireTarget.Cell.X - selectedUnit.Cell.X;
                        float deltaZ = SelectedFireTarget.Cell.Y - selectedUnit.Cell.Y;
                        selectedUnit.TargetOrientation = Unit.ComputeOrientationFromDelta(deltaX, deltaZ);
                    }

                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Dessine les informations de mouvement (portées et coûts)
        /// </summary>
        public void DrawMovementInfo(Unit selectedUnit, Point hoveredCell, List<Point> currentPath)
        {
            if (selectedUnit == null || selectedUnit.Team != Team.Player)
                return;

            int x = graphicsDevice.Viewport.Width - 250;
            int y = 80;
            int w = 230;
            int h = 160;

            Rectangle panel = new Rectangle(x, y, w, h);
            ParasiteEveTheme.DrawPanel(spriteBatch, pixel, panel);

            // Header
            Rectangle header = new Rectangle(x, y, w, 30);
            ParasiteEveTheme.DrawSectionHeader(spriteBatch, pixel, font, header, "MOVEMENT");

            Vector2 pos = new Vector2(x + 12, y + 40);

            // Portées
            int shortRange = selectedUnit.GetShortMoveRange();
            int maxRange = selectedUnit.GetMaxMoveRange();
            int sprintRange = selectedUnit.GetSprintRange();

            // Court (1 AP)
            string shortText = $"Short: {shortRange} cells (1 AP)";
            Color shortColor = selectedUnit.ActionPoints >= 1 ?
                new Color(100, 255, 100) : ParasiteEveTheme.TextDim;
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, shortText,
                pos, shortColor, 0.8f);
            pos.Y += 20;

            // Max (2 AP)
            string maxText = $"Max: {maxRange} cells (2 AP)";
            Color maxColor = selectedUnit.ActionPoints >= 2 ?
                new Color(100, 200, 255) : ParasiteEveTheme.TextDim;
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, maxText,
                pos, maxColor, 0.8f);
            pos.Y += 20;

            // Sprint (2 AP + phosphocréatine variable)
            string sprintText = $"Sprint: {sprintRange} cells (2 AP + PCr variable)";
            Color sprintColor = selectedUnit.CanSprint() ?
                new Color(255, 200, 50) : ParasiteEveTheme.TextDim;
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, sprintText,
                pos, sprintColor, 0.8f);

            // Info du chemin survolé
            if (hoveredCell.X != -1 && currentPath != null && currentPath.Count > 0)
            {
                pos.Y += 30;

                int distance = currentPath.Count;
                int apCost = selectedUnit.GetMovementAPCost(distance);
                string moveType;
                Color moveColor;

                if (distance <= shortRange)
                {
                    moveType = "SHORT MOVE";
                    moveColor = new Color(100, 255, 100);
                }
                else if (distance <= maxRange)
                {
                    moveType = "MAX MOVE";
                    moveColor = new Color(100, 200, 255);
                }
                else
                {
                    moveType = "SPRINT";
                    moveColor = new Color(255, 200, 50);
                }

                int phosphocreatineCost = selectedUnit.GetMovementPhosphocreatineCost(distance);
                string costText = $"{distance} cells: {apCost} AP + {phosphocreatineCost}% PCr";

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, moveType,
                    pos, moveColor, 0.75f);
                pos.Y += 16;
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, costText,
                    pos, ParasiteEveTheme.TextNormal, 0.7f);
            }

            ParasiteEveTheme.DrawScanlines(spriteBatch, pixel, panel, 0.06f);
        }


    }
}
