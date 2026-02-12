using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace XCOM_3.Scripts
{
    /// <summary>
    /// Thème visuel inspiré de Parasite Eve 2
    /// Style vert militaire avec effets de scanlines
    /// </summary>
    public static class ParasiteEveTheme
    {
        // ═══════════════════════════════════════════════════════════════════
        // COULEURS PRINCIPALES (Parasite Eve 2)
        // ═══════════════════════════════════════════════════════════════════

        // Fond principal (vert très foncé, presque noir)
        public static readonly Color BackgroundDark = new Color(10, 25, 15, 230);
        public static readonly Color BackgroundMedium = new Color(15, 35, 20, 220);
        
        // Bordures et panels (vert moyen)
        public static readonly Color BorderColor = new Color(60, 140, 80, 255);
        public static readonly Color PanelBackground = new Color(20, 45, 25, 200);
        
        // Texte
        public static readonly Color TextNormal = new Color(200, 255, 200, 255);  // Vert clair
        public static readonly Color TextHighlight = new Color(150, 255, 150, 255); // Vert vif
        public static readonly Color TextDim = new Color(100, 150, 100, 200);     // Vert sombre
        public static readonly Color TextWarning = new Color(255, 200, 100, 255); // Orange
        public static readonly Color TextDanger = new Color(255, 100, 100, 255);  // Rouge
        
        // Boutons
        public static readonly Color ButtonNormal = new Color(40, 80, 50, 220);
        public static readonly Color ButtonHover = new Color(60, 120, 70, 240);
        public static readonly Color ButtonPressed = new Color(80, 160, 90, 255);
        public static readonly Color ButtonDisabled = new Color(30, 50, 35, 150);
        
        // Barres de vie/stats
        public static readonly Color BarBackground = new Color(20, 40, 25, 200);
        public static readonly Color BarHealth = new Color(80, 200, 100, 255);    // Vert santé
        public static readonly Color BarHealthLow = new Color(200, 100, 50, 255); // Orange danger
        public static readonly Color BarMP = new Color(100, 150, 255, 255);       // Bleu MP
        public static readonly Color BarXP = new Color(150, 200, 255, 255);       // Bleu clair XP
        
        // Icônes d'éléments (PE2)
        public static readonly Color ElementFire = new Color(255, 100, 50, 255);
        public static readonly Color ElementWind = new Color(150, 200, 255, 255);
        public static readonly Color ElementWater = new Color(100, 150, 255, 255);
        public static readonly Color ElementEarth = new Color(180, 140, 80, 255);
        
        // Sélection et hover
        public static readonly Color SelectionOutline = new Color(150, 255, 150, 255);
        public static readonly Color HoverOverlay = new Color(60, 140, 80, 100);

        // ═══════════════════════════════════════════════════════════════════
        // EFFETS VISUELS
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dessine un panneau avec le style Parasite Eve 2
        /// </summary>
        public static void DrawPanel(SpriteBatch sb, Texture2D pixel, Rectangle bounds, bool drawBorder = true)
        {
            // Fond dégradé
            sb.Draw(pixel, bounds, BackgroundDark);
            
            // Overlay semi-transparent pour effet de profondeur
            Rectangle innerBounds = new Rectangle(
                bounds.X + 2,
                bounds.Y + 2,
                bounds.Width - 4,
                bounds.Height - 4
            );
            sb.Draw(pixel, innerBounds, BackgroundMedium);
            
            if (drawBorder)
            {
                DrawBorder(sb, pixel, bounds, BorderColor, 2);
            }
        }

        /// <summary>
        /// Dessine une bordure style PE2
        /// </summary>
        public static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            // Haut
            sb.Draw(pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
            // Bas
            sb.Draw(pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
            // Gauche
            sb.Draw(pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
            // Droite
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
        }

        /// <summary>
        /// Dessine un bouton style PE2
        /// </summary>
        public static void DrawButton(SpriteBatch sb, Texture2D pixel, SpriteFont font, 
                                      Rectangle bounds, string text, bool isHovered, bool isPressed, bool isEnabled = true)
        {
            Color bgColor = !isEnabled ? ButtonDisabled :
                           isPressed ? ButtonPressed :
                           isHovered ? ButtonHover : ButtonNormal;
            
            // Fond du bouton
            sb.Draw(pixel, bounds, bgColor);
            
            // Bordure
            Color borderColor = isHovered ? SelectionOutline : BorderColor;
            DrawBorder(sb, pixel, bounds, borderColor, isHovered ? 2 : 1);
            
            // Texte centré
            Vector2 textSize = font.MeasureString(text);
            Vector2 textPos = new Vector2(
                bounds.X + (bounds.Width - textSize.X) / 2,
                bounds.Y + (bounds.Height - textSize.Y) / 2
            );
            
            Color textColor = !isEnabled ? TextDim :
                             isHovered ? TextHighlight : TextNormal;
            
            sb.DrawString(font, text, textPos, textColor);
        }

        /// <summary>
        /// Dessine une barre de progression style PE2
        /// </summary>
        public static void DrawProgressBar(SpriteBatch sb, Texture2D pixel, Rectangle bounds, 
                                           float currentValue, float maxValue, Color fillColor)
        {
            // Fond
            sb.Draw(pixel, bounds, BarBackground);
            
            // Remplissage
            float ratio = maxValue > 0 ? currentValue / maxValue : 0f;
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            
            Rectangle fillRect = new Rectangle(
                bounds.X + 1,
                bounds.Y + 1,
                (int)((bounds.Width - 2) * ratio),
                bounds.Height - 2
            );
            
            if (fillRect.Width > 0)
            {
                sb.Draw(pixel, fillRect, fillColor);
            }
            
            // Bordure
            DrawBorder(sb, pixel, bounds, BorderColor, 1);
        }

        /// <summary>
        /// Dessine une barre de santé avec changement de couleur selon le niveau
        /// </summary>
        public static void DrawHealthBar(SpriteBatch sb, Texture2D pixel, Rectangle bounds, 
                                         int currentHP, int maxHP)
        {
            float ratio = maxHP > 0 ? (float)currentHP / maxHP : 0f;
            
            // Couleur change selon le niveau de santé
            Color fillColor = ratio > 0.5f ? BarHealth :
                             ratio > 0.25f ? new Color(200, 200, 100, 255) :
                             BarHealthLow;
            
            DrawProgressBar(sb, pixel, bounds, currentHP, maxHP, fillColor);
        }

        /// <summary>
        /// Dessine un header de section style PE2
        /// </summary>
        public static void DrawSectionHeader(SpriteBatch sb, Texture2D pixel, SpriteFont font, 
                                             Rectangle bounds, string title)
        {
            // Fond du header (plus clair)
            sb.Draw(pixel, bounds, new Color(40, 90, 55, 240));
            
            // Ligne décorative en haut
            sb.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), SelectionOutline);
            
            // Titre
            Vector2 titleSize = font.MeasureString(title);
            sb.DrawString(font, title, 
                new Vector2(bounds.X + 10, bounds.Y + (bounds.Height - titleSize.Y) / 2),
                TextHighlight);
            
            // Bordure
            DrawBorder(sb, pixel, bounds, BorderColor, 1);
        }

        /// <summary>
        /// Dessine un effet de scanline (lignes horizontales subtiles)
        /// </summary>
        public static void DrawScanlines(SpriteBatch sb, Texture2D pixel, Rectangle area, float opacity = 0.05f)
        {
            Color scanlineColor = new Color(0, 0, 0, (int)(255 * opacity));
            
            for (int y = area.Top; y < area.Bottom; y += 3)
            {
                Rectangle line = new Rectangle(area.X, y, area.Width, 1);
                sb.Draw(pixel, line, scanlineColor);
            }
        }

        /// <summary>
        /// Dessine un indicateur de sélection style PE2
        /// </summary>
        public static void DrawSelectionIndicator(SpriteBatch sb, Texture2D pixel, Rectangle bounds, 
                                                  float pulseTime)
        {
            float pulse = (float)System.Math.Sin(pulseTime * 4f) * 0.3f + 0.7f;
            Color glowColor = SelectionOutline * pulse;
            
            // Bordure qui pulse
            DrawBorder(sb, pixel, bounds, glowColor, 3);
            
            // Coins accentués (style PE2)
            int cornerSize = 8;
            
            // Coin haut-gauche
            sb.Draw(pixel, new Rectangle(bounds.Left - 2, bounds.Top - 2, cornerSize, 2), TextHighlight);
            sb.Draw(pixel, new Rectangle(bounds.Left - 2, bounds.Top - 2, 2, cornerSize), TextHighlight);
            
            // Coin haut-droite
            sb.Draw(pixel, new Rectangle(bounds.Right - cornerSize + 2, bounds.Top - 2, cornerSize, 2), TextHighlight);
            sb.Draw(pixel, new Rectangle(bounds.Right, bounds.Top - 2, 2, cornerSize), TextHighlight);
            
            // Coin bas-gauche
            sb.Draw(pixel, new Rectangle(bounds.Left - 2, bounds.Bottom, cornerSize, 2), TextHighlight);
            sb.Draw(pixel, new Rectangle(bounds.Left - 2, bounds.Bottom - cornerSize + 2, 2, cornerSize), TextHighlight);
            
            // Coin bas-droite
            sb.Draw(pixel, new Rectangle(bounds.Right - cornerSize + 2, bounds.Bottom, cornerSize, 2), TextHighlight);
            sb.Draw(pixel, new Rectangle(bounds.Right, bounds.Bottom - cornerSize + 2, 2, cornerSize), TextHighlight);
        }

        /// <summary>
        /// Dessine un overlay semi-transparent avec effet de vignette
        /// </summary>
        public static void DrawVignetteOverlay(SpriteBatch sb, Texture2D pixel, int screenWidth, int screenHeight)
        {
            int vignetteSize = 100;
            
            // Haut
            for (int y = 0; y < vignetteSize; y++)
            {
                float alpha = 1f - y / (float)vignetteSize;
                Color color = BackgroundDark * (alpha * 0.5f);
                sb.Draw(pixel, new Rectangle(0, y, screenWidth, 1), color);
            }
            
            // Bas
            for (int y = 0; y < vignetteSize; y++)
            {
                float alpha = 1f - y / (float)vignetteSize;
                Color color = BackgroundDark * (alpha * 0.5f);
                sb.Draw(pixel, new Rectangle(0, screenHeight - vignetteSize + y, screenWidth, 1), color);
            }
        }

        /// <summary>
        /// Dessine un texte avec ombre pour meilleure lisibilité
        /// </summary>
        public static void DrawTextWithShadow(SpriteBatch sb, SpriteFont font, string text, 
                                              Vector2 position, Color color, float scale = 1f)
        {
            // Ombre
            sb.DrawString(font, text, position + new Vector2(1, 1), Color.Black * 0.7f, 
                         0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            
            // Texte principal
            sb.DrawString(font, text, position, color, 
                         0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
