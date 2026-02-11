using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;

namespace XCOM_3
{
    public class StatsPanel
    {
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;

        private bool _visible;
        private int _scrollOffset;

        private const int PanelWidth = 700;
        private const int PanelHeight = 750;

        public StatsPanel(SpriteFont font, GraphicsDevice device)
        {
            _font = font;

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void Toggle() => _visible = !_visible;

        public void Update()
        {
            if (!_visible) return;

            var wheel = Mouse.GetState().ScrollWheelValue;
            _scrollOffset = Math.Clamp(_scrollOffset - wheel / 120 * 20, 0, 500);
        }

        public void Draw(SpriteBatch sb, Unit unit)
        {
            if (!_visible || unit == null) return;

            Rectangle panel = new Rectangle(150, 80, PanelWidth, PanelHeight);

            // Background
            sb.Draw(_pixel, panel, new Color(15, 15, 20, 230));

            Vector2 pos = new Vector2(panel.X + 25, panel.Y + 25 - _scrollOffset);

            DrawHeader(sb, pos, unit);
            pos.Y += 60;

            DrawSkills(sb, ref pos, unit);
            pos.Y += 30;

            DrawBonuses(sb, ref pos, unit);
        }

        private void DrawHeader(SpriteBatch sb, Vector2 pos, Unit unit)
        {
            sb.DrawString(_font, "UNIT STATISTICS", pos, Color.Gold);
            pos.Y += 30;

            sb.DrawString(_font,
                $"Overall Level: {unit.Skills.OverallLevel}",
                pos,
                Color.White);
        }

        private void DrawSkills(SpriteBatch sb, ref Vector2 pos, Unit unit)
        {
            sb.DrawString(_font, "SKILLS", pos, Color.Orange);
            pos.Y += 30;

            foreach (var skill in unit.Skills.AllSkills
                         .OrderByDescending(s => s.Value.Level))
            {
                var progress = skill.Value;

                string line =
                    $"{skill.Key,-15}  Lv {progress.Level,-3}  XP {progress.XP,-5}  Next: {progress.XPToNext}";

                sb.DrawString(_font, line, pos, Color.LightGray);
                pos.Y += 20;

                DrawXPBar(sb, pos, progress);
                pos.Y += 25;
            }
        }

        private void DrawXPBar(SpriteBatch sb, Vector2 pos, SkillProgress progress)
        {
            int barWidth = 400;
            int barHeight = 10;

            int totalForLevel = progress.XP + progress.XPToNext;
            float ratio = totalForLevel == 0 ? 0 :
                progress.XP / (float)totalForLevel;

            Rectangle bg = new Rectangle((int)pos.X, (int)pos.Y, barWidth, barHeight);
            Rectangle fill = new Rectangle((int)pos.X, (int)pos.Y,
                (int)(barWidth * ratio), barHeight);

            sb.Draw(_pixel, bg, Color.DarkSlateGray);
            sb.Draw(_pixel, fill, Color.LimeGreen);
        }

        private void DrawBonuses(SpriteBatch sb, ref Vector2 pos, Unit unit)
        {
            sb.DrawString(_font, "DERIVED BONUSES", pos, Color.Orange);
            pos.Y += 30;

            var s = unit.Skills;

            DrawBonus(sb, ref pos, "Accuracy", s.GetAccuracyBonus());
            DrawBonus(sb, ref pos, "Defense", s.GetDefenseBonus());
            DrawBonus(sb, ref pos, "Health", s.GetHealthBonus());
            DrawBonus(sb, ref pos, "Movement", s.GetMovementBonus());
            DrawBonus(sb, ref pos, "Vision", s.GetVisionBonus());
            DrawBonus(sb, ref pos, "Hack", s.GetHackBonus());
            DrawBonus(sb, ref pos, "Stealth", s.GetStealthBonus());
            DrawBonus(sb, ref pos, "Explosion Radius", s.GetExplosionRadiusBonus());
            DrawBonus(sb, ref pos, "Carry", s.GetCarryBonus());
            DrawBonus(sb, ref pos, "Recoil Reduction", s.GetRecoilReduction());
            DrawBonus(sb, ref pos, "Damage", s.GetDamageBonus());
        }

        private void DrawBonus(SpriteBatch sb, ref Vector2 pos, string name, int value)
        {
            sb.DrawString(_font,
                $"{name,-20}: +{value}",
                pos,
                Color.LightGreen);

            pos.Y += 22;
        }
    }
}
