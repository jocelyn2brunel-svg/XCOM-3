using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XCOM_3.Scripts;

namespace XCOM_3
{
    /// <summary>
    /// Panneau d'identité du personnage (raccourci C).
    /// </summary>
    public class CharacterInfoPanel
    {
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;

        private const int PanelWidth = 520;
        private const int PanelHeight = 430;

        public bool IsVisible { get; private set; }

        public CharacterInfoPanel(SpriteFont font, GraphicsDevice graphicsDevice)
        {
            _font = font;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void Show() => IsVisible = true;
        public void Hide() => IsVisible = false;
        public void Toggle() => IsVisible = !IsVisible;

        public void Draw(SpriteBatch spriteBatch, Unit unit)
        {
            if (!IsVisible || unit == null) return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            int screenHeight = spriteBatch.GraphicsDevice.Viewport.Height;

            int panelX = (screenWidth - PanelWidth) / 2;
            int panelY = (screenHeight - PanelHeight) / 2;
            Rectangle panel = new Rectangle(panelX, panelY, PanelWidth, PanelHeight);

            ParasiteEveTheme.DrawPanel(spriteBatch, _pixel, panel);

            Rectangle header = new Rectangle(panelX, panelY, PanelWidth, 42);
            ParasiteEveTheme.DrawSectionHeader(spriteBatch, _pixel, _font, header,
                $"{unit.Name.ToUpperInvariant()} - CHARACTER INFO");

            Vector2 cursor = new Vector2(panelX + 24, panelY + 60);
            DrawPair(spriteBatch, ref cursor, "Nom", unit.Name);
            DrawPair(spriteBatch, ref cursor, "Classe", unit.Class);
            DrawPair(spriteBatch, ref cursor, "Equipe", unit.Team.ToString());
            DrawPair(spriteBatch, ref cursor, "Genre", GetBodyTypeLabel(unit.BodyType));
            DrawPair(spriteBatch, ref cursor, "Main dominante", GetHandednessLabel(unit.DominantHand));
            DrawPair(spriteBatch, ref cursor, "Portee de deplacement", $"{unit.GetMaxMoveRange()} cases");
            DrawPair(spriteBatch, ref cursor, "Portee sprint", $"{unit.GetSprintRange()} cases");
            DrawPair(spriteBatch, ref cursor, "Perception", $"{unit.PerceptionRangeCells} cases");
            DrawPair(spriteBatch, ref cursor, "Points d'action", $"{unit.ActionPoints}/{unit.MaxActionPoints}");
            DrawPair(spriteBatch, ref cursor, "Sante", $"{unit.Health}/{unit.GetMaxHealth()}");
            DrawPair(spriteBatch, ref cursor, "Endurance", $"{unit.Stamina}/{unit.MaxStamina}");

            ParasiteEveTheme.DrawScanlines(spriteBatch, _pixel, panel, 0.05f);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, _font,
                "[CLOSE: C]",
                new Vector2(panelX + 24, panel.Bottom - 30),
                ParasiteEveTheme.TextDim,
                0.85f);
        }

        private void DrawPair(SpriteBatch spriteBatch, ref Vector2 cursor, string label, string value)
        {
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, _font,
                label + " :",
                cursor,
                ParasiteEveTheme.TextNormal,
                0.95f);

            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, _font,
                value,
                new Vector2(cursor.X + 220, cursor.Y),
                ParasiteEveTheme.TextHighlight,
                0.95f);

            cursor.Y += 30;
        }

        private static string GetBodyTypeLabel(Unit.HumanBodyType bodyType) =>
            bodyType == Unit.HumanBodyType.Feminine ? "Femme" : "Homme";

        private static string GetHandednessLabel(Unit.Handedness handedness) =>
            handedness == Unit.Handedness.Left ? "Gaucher" : "Droitier";
    }
}
