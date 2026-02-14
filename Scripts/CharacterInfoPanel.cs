using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        private readonly GraphicsDevice _graphicsDevice;
        private readonly BasicEffect _previewEffect;
        private readonly HumanoidModelAdvanced _previewModel;

        private const int PanelWidth = 930;
        private const int PanelHeight = 500;

        private const float PreviewRotationSpeed = 1.8f;
        private float _previewRotation;

        public bool IsVisible { get; private set; }

        public CharacterInfoPanel(SpriteFont font, GraphicsDevice graphicsDevice)
        {
            _font = font;
            _graphicsDevice = graphicsDevice;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _previewEffect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = true,
                AmbientLightColor = new Vector3(0.5f),
                DiffuseColor = new Vector3(1f)
            };
            _previewEffect.EnableDefaultLighting();

            _previewModel = new HumanoidModelAdvanced();
        }

        public void Show() => IsVisible = true;
        public void Hide() => IsVisible = false;
        public void Toggle() => IsVisible = !IsVisible;

        public void Update(GameTime gameTime, KeyboardState keyboard)
        {
            if (!IsVisible)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (keyboard.IsKeyDown(Keys.Left))
                _previewRotation -= PreviewRotationSpeed * dt;
            if (keyboard.IsKeyDown(Keys.Right))
                _previewRotation += PreviewRotationSpeed * dt;
        }

        public void DrawPreview3D(Unit unit)
        {
            if (!IsVisible || unit == null)
                return;

            Rectangle panel = GetPanelBounds();
            Rectangle previewRect = GetPreviewRect(panel);

            Viewport originalViewport = _graphicsDevice.Viewport;
            DepthStencilState originalDepth = _graphicsDevice.DepthStencilState;
            BlendState originalBlend = _graphicsDevice.BlendState;
            RasterizerState originalRasterizer = _graphicsDevice.RasterizerState;

            _graphicsDevice.Viewport = new Viewport(previewRect);
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.Clear(new Color(14, 18, 28));

            _previewEffect.View = Matrix.CreateLookAt(new Vector3(0f, 2.5f, 5.6f), new Vector3(0f, 1.8f, 0f), Vector3.Up);
            _previewEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f),
                Math.Max(0.2f, previewRect.Width / (float)previewRect.Height),
                0.1f,
                100f);

            Unit previewUnit = new Unit(unit)
            {
                VisualPosition = new Vector3(0f, 0f, 0f),
                IsMoving = false,
                IsAiming = false,
                IsFiring = false,
                Orientation = MathHelper.Pi + _previewRotation
            };
            previewUnit.LegSwing = 0f;
            previewUnit.ArmSwing = 0f;
            previewUnit.BodyBob = 0f;
            previewUnit.IdleBobOffset = 0f;

            _previewModel.DrawWithEquipment(_graphicsDevice, _previewEffect, previewUnit, 2.35f);

            _graphicsDevice.Viewport = originalViewport;
            _graphicsDevice.DepthStencilState = originalDepth;
            _graphicsDevice.BlendState = originalBlend;
            _graphicsDevice.RasterizerState = originalRasterizer;
        }

        public void Draw(SpriteBatch spriteBatch, Unit unit)
        {
            if (!IsVisible || unit == null) return;

            Rectangle panel = GetPanelBounds();
            Rectangle previewRect = GetPreviewRect(panel);
            int panelX = panel.X;
            int panelY = panel.Y;

            ParasiteEveTheme.DrawPanel(spriteBatch, _pixel, panel);

            Rectangle header = new Rectangle(panelX, panelY, PanelWidth, 42);
            ParasiteEveTheme.DrawSectionHeader(spriteBatch, _pixel, _font, header,
                $"{unit.Name.ToUpperInvariant()} - CHARACTER INFO");

            Rectangle infoSection = new Rectangle(panelX + 16, panelY + 52, 450, PanelHeight - 90);
            ParasiteEveTheme.DrawPanel(spriteBatch, _pixel, infoSection);

            Vector2 cursor = new Vector2(infoSection.X + 12, infoSection.Y + 12);
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

            // Ne pas recouvrir l'aperçu 3D avec un fond opaque : on dessine seulement un cadre
            // et des bandeaux d'UI pour conserver la lisibilité du texte.
            ParasiteEveTheme.DrawBorder(spriteBatch, _pixel, previewRect, ParasiteEveTheme.BorderColor, 2);

            Rectangle previewHeader = new Rectangle(previewRect.X + 2, previewRect.Y + 2, previewRect.Width - 4, 30);
            Rectangle previewFooter = new Rectangle(previewRect.X + 2, previewRect.Bottom - 34, previewRect.Width - 4, 32);
            spriteBatch.Draw(_pixel, previewHeader, new Color(8, 20, 12, 190));
            spriteBatch.Draw(_pixel, previewFooter, new Color(8, 20, 12, 170));
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, _font,
                "APERÇU 3D",
                new Vector2(previewRect.X + 14, previewRect.Y + 10),
                ParasiteEveTheme.TextHighlight,
                0.9f);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, _font,
                "Tourner: ← / →",
                new Vector2(previewRect.X + 14, previewRect.Bottom - 32),
                ParasiteEveTheme.TextDim,
                0.8f);

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
                new Vector2(cursor.X + 200, cursor.Y),
                ParasiteEveTheme.TextHighlight,
                0.95f);

            cursor.Y += 30;
        }

        private static string GetBodyTypeLabel(Unit.HumanBodyType bodyType) =>
            bodyType == Unit.HumanBodyType.Feminine ? "Femme" : "Homme";

        private static string GetHandednessLabel(Unit.Handedness handedness) =>
            handedness == Unit.Handedness.Left ? "Gaucher" : "Droitier";

        private Rectangle GetPanelBounds()
        {
            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;
            int panelX = (screenWidth - PanelWidth) / 2;
            int panelY = (screenHeight - PanelHeight) / 2;
            return new Rectangle(panelX, panelY, PanelWidth, PanelHeight);
        }

        private static Rectangle GetPreviewRect(Rectangle panel)
            => new Rectangle(panel.X + 485, panel.Y + 52, 430, panel.Height - 90);
    }
}
