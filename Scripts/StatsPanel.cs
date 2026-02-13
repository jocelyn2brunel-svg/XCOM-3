using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using XCOM_3.Scripts;

namespace XCOM_3
{
    /// <summary>
    /// Panneau de statistiques et compétences - STYLE PARASITE EVE 2
    /// </summary>
    public class StatsPanel
    {
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;

        private bool _visible;
        private int _scrollOffset;
        private float _pulseTimer;

        private const int PanelWidth = 750;
        private const int PanelHeight = 780;

        public bool IsVisible => _visible;

        public void Hide() => _visible = false;
        public void Show()
        {
            _visible = true;
        }
        public StatsPanel(SpriteFont font, GraphicsDevice device)
        {
            _font = font;

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void Toggle() => _visible = !_visible;

        public void Update(GameTime gameTime, MouseState mouseState, MouseState previousMouseState)
        {
            if (!_visible) return;

            // Animation pulsante
            _pulseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Scroll avec molette
            int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            _scrollOffset = Math.Clamp(_scrollOffset - (scrollDelta / 120) * 20, 0, 800);
        }

        public void Draw(SpriteBatch sb, Unit unit)
        {
            if (!_visible || unit == null) return;

            int screenWidth = sb.GraphicsDevice.Viewport.Width;
            int screenHeight = sb.GraphicsDevice.Viewport.Height;

            // Centrer le panneau
            int panelX = (screenWidth - PanelWidth) / 2;
            int panelY = (screenHeight - PanelHeight) / 2;
            Rectangle panel = new Rectangle(panelX, panelY, PanelWidth, PanelHeight);

            // ✅ Panel PE2 avec fond
            ParasiteEveTheme.DrawPanel(sb, _pixel, panel);

            // ✅ Header avec le nom de l'unité
            Rectangle header = new Rectangle(panelX, panelY, PanelWidth, 40);
            ParasiteEveTheme.DrawSectionHeader(sb, _pixel, _font, header,
                $"{unit.Name.ToUpper()} - STATISTICS");

            // Position de départ du contenu (après le header, avec scroll)
            Vector2 pos = new Vector2(panelX + 25, panelY + 50 - _scrollOffset);

            // Info générale
            DrawGeneralInfo(sb, ref pos, unit);
            pos.Y += 20;

            // Compétences
            DrawSkills(sb, ref pos, unit);
            pos.Y += 25;

            // Bonus dérivés
            DrawBonuses(sb, ref pos, unit);

            // ✅ Scanlines effect
            ParasiteEveTheme.DrawScanlines(sb, _pixel, panel, 0.06f);

            // Instructions de scroll
            Vector2 scrollHintPos = new Vector2(panelX + 25, panelY + PanelHeight - 30);
            ParasiteEveTheme.DrawTextWithShadow(sb, _font,
                "[SCROLL: Mouse Wheel] | [CLOSE: K]",
                scrollHintPos,
                ParasiteEveTheme.TextDim,
                0.8f);
        }

        private void DrawGeneralInfo(SpriteBatch sb, ref Vector2 pos, Unit unit)
        {
            // Titre section
            sb.DrawString(_font, "UNIT DATA", pos, ParasiteEveTheme.TextHighlight);
            pos.Y += 28;

            // Niveau global avec effet pulsant
            float pulse = (float)Math.Sin(_pulseTimer * 2f) * 0.2f + 0.8f;
            Color levelColor = new Color(
                (int)(255 * pulse),
                (int)(220 * pulse),
                (int)(100 * pulse)
            );

            string levelText = $"OVERALL LEVEL: {unit.Skills.OverallLevel}";
            ParasiteEveTheme.DrawTextWithShadow(sb, _font, levelText,
                pos, levelColor, 1.1f);
            pos.Y += 30;

            // Séparateur
            Rectangle separator = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth - 50, 2);
            sb.Draw(_pixel, separator, ParasiteEveTheme.BorderColor);
            pos.Y += 15;
        }

        private void DrawSkills(SpriteBatch sb, ref Vector2 pos, Unit unit)
        {
            // Titre section
            Rectangle skillHeader = new Rectangle(
                (int)pos.X - 5, (int)pos.Y, PanelWidth - 40, 35);
            ParasiteEveTheme.DrawSectionHeader(sb, _pixel, _font, skillHeader, "SKILLS");
            pos.Y += 45;

            // Trier par niveau (décroissant)
            var sortedSkills = unit.Skills.AllSkills
                .OrderByDescending(s => s.Value.Level)
                .ThenBy(s => s.Key);

            foreach (var skill in sortedSkills)
            {
                var progress = skill.Value;

                // Nom de la compétence et niveau
                string skillName = skill.Key.ToString().ToUpperInvariant();
                string levelText = $"LV {progress.Level}";

                ParasiteEveTheme.DrawTextWithShadow(sb, _font, skillName,
                    pos, ParasiteEveTheme.TextNormal);

                Vector2 levelSize = _font.MeasureString(levelText);
                ParasiteEveTheme.DrawTextWithShadow(sb, _font, levelText,
                    new Vector2(pos.X + 180, pos.Y),
                    ParasiteEveTheme.TextHighlight);

                // XP info
                string xpText = $"{progress.XP} / {progress.XP + progress.XPToNext} XP";
                Vector2 xpSize = _font.MeasureString(xpText);
                ParasiteEveTheme.DrawTextWithShadow(sb, _font, xpText,
                    new Vector2(pos.X + PanelWidth - 100 - xpSize.X, pos.Y),
                    ParasiteEveTheme.TextDim,
                    0.85f);

                pos.Y += 22;

                // Barre de progression XP
                DrawXPBar(sb, pos, progress);
                pos.Y += 28;
            }
        }

        private void DrawXPBar(SpriteBatch sb, Vector2 pos, SkillProgress progress)
        {
            int barWidth = PanelWidth - 100;
            int barHeight = 14;

            Rectangle barBounds = new Rectangle((int)pos.X, (int)pos.Y, barWidth, barHeight);

            int totalForLevel = progress.XP + progress.XPToNext;
            float ratio = totalForLevel == 0 ? 0 : progress.XP / (float)totalForLevel;

            // ✅ Utiliser la barre de progression PE2
            ParasiteEveTheme.DrawProgressBar(sb, _pixel, barBounds,
                progress.XP,
                progress.XP + progress.XPToNext,
                ParasiteEveTheme.BarXP);

            // Pourcentage au centre de la barre
            string percentText = $"{(int)(ratio * 100)}%";
            Vector2 percentSize = _font.MeasureString(percentText);
            ParasiteEveTheme.DrawTextWithShadow(sb, _font, percentText,
                new Vector2(barBounds.Center.X - percentSize.X / 2,
                           barBounds.Center.Y - percentSize.Y / 2),
                Color.White,
                0.75f);
        }

        private void DrawBonuses(SpriteBatch sb, ref Vector2 pos, Unit unit)
        {
            // Titre section
            Rectangle bonusHeader = new Rectangle(
                (int)pos.X - 5, (int)pos.Y, PanelWidth - 40, 35);
            ParasiteEveTheme.DrawSectionHeader(sb, _pixel, _font, bonusHeader, "DERIVED BONUSES");
            pos.Y += 45;

            var s = unit.Skills;

            // Colonnes de bonus
            Vector2 leftCol = pos;
            Vector2 rightCol = new Vector2(pos.X + (PanelWidth - 50) / 2, pos.Y);

            // Colonne gauche
            DrawBonus(sb, ref leftCol, "ACCURACY", s.GetAccuracyBonus(), ParasiteEveTheme.BarHealth);
            DrawBonus(sb, ref leftCol, "DEFENSE", s.GetDefenseBonus(), ParasiteEveTheme.BarHealth);
            DrawBonus(sb, ref leftCol, "HEALTH", s.GetHealthBonus(), ParasiteEveTheme.BarHealth);
            DrawBonus(sb, ref leftCol, "MOVEMENT", s.GetMovementBonus(), ParasiteEveTheme.BarMP);
            DrawBonus(sb, ref leftCol, "VISION", s.GetVisionBonus(), ParasiteEveTheme.BarMP);
            DrawBonus(sb, ref leftCol, "DAMAGE", s.GetDamageBonus(), ParasiteEveTheme.TextWarning);

            // Colonne droite
            DrawBonus(sb, ref rightCol, "HACK", s.GetHackBonus(), ParasiteEveTheme.BarXP);
            DrawBonus(sb, ref rightCol, "STEALTH", s.GetStealthBonus(), ParasiteEveTheme.BarXP);
            DrawBonus(sb, ref rightCol, "EXPLOSION", s.GetExplosionRadiusBonus(), ParasiteEveTheme.TextWarning);
            DrawBonus(sb, ref rightCol, "CARRY", s.GetCarryBonus(), ParasiteEveTheme.BarXP);
            DrawBonus(sb, ref rightCol, "RECOIL", s.GetRecoilReduction(), ParasiteEveTheme.BarHealth);

            // Mettre à jour la position Y pour le scroll
            pos.Y = Math.Max(leftCol.Y, rightCol.Y);
        }

        private void DrawBonus(SpriteBatch sb, ref Vector2 pos, string name, int value, Color valueColor)
        {
            // Nom du bonus
            ParasiteEveTheme.DrawTextWithShadow(sb, _font, name + ":",
                pos, ParasiteEveTheme.TextNormal, 0.9f);

            // Valeur avec couleur spécifique
            string valueText = $"+{value}";
            Vector2 nameSize = _font.MeasureString(name + ":");

            ParasiteEveTheme.DrawTextWithShadow(sb, _font, valueText,
                new Vector2(pos.X + nameSize.X + 10, pos.Y),
                valueColor,
                0.9f);

            // Petit indicateur visuel
            if (value > 0)
            {
                Rectangle indicator = new Rectangle(
                    (int)(pos.X + nameSize.X + 10 + _font.MeasureString(valueText).X + 8),
                    (int)pos.Y + 6,
                    Math.Min(value * 2, 40),
                    8
                );
                sb.Draw(_pixel, indicator, valueColor * 0.4f);
                ParasiteEveTheme.DrawBorder(sb, _pixel, indicator, valueColor * 0.8f, 1);
            }

            pos.Y += 26;
        }
    }
}