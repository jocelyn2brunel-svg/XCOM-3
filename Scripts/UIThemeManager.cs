using Microsoft.Xna.Framework;

namespace XCOM_3
{
    /// <summary>
    /// Thème visuel global de l'interface.
    /// </summary>
    public static class UIThemeManager
    {
        public static Color PrimaryColor { get; private set; } = Color.White;

        public static Color DisabledColor => Color.Gray;

        public static Color SecondaryColor
        {
            get
            {
                // Variante atténuée de la couleur primaire pour les textes secondaires.
                return new Color(
                    (int)MathHelper.Clamp(PrimaryColor.R * 0.75f, 0, 255),
                    (int)MathHelper.Clamp(PrimaryColor.G * 0.75f, 0, 255),
                    (int)MathHelper.Clamp(PrimaryColor.B * 0.75f, 0, 255)
                );
            }
        }

        public static Color HoverColor
        {
            get
            {
                // Couleur plus lumineuse au survol.
                return new Color(
                    (int)MathHelper.Clamp(PrimaryColor.R + 60, 0, 255),
                    (int)MathHelper.Clamp(PrimaryColor.G + 60, 0, 255),
                    (int)MathHelper.Clamp(PrimaryColor.B + 60, 0, 255)
                );
            }
        }

        public static void SetPrimaryColor(byte r, byte g, byte b)
        {
            PrimaryColor = new Color(r, g, b);
        }
    }
}
