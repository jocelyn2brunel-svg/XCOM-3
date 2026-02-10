using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Gère le menu des options (volume, etc.)
    /// </summary>
    public class OptionsMenuManager
    {
        // --- Références externes ---
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;

        // --- Boutons ---
        private List<Button> _optionsButtons;

        // --- Contrôle du volume ---
        private float _musicVolume = 0.5f;
        private Rectangle _volumeBar;
        private Rectangle _volumeFill;
        private Rectangle _volumeHandle;
        private bool _draggingVolume = false;

        // --- Événements ---
        public event Action OnBackToMainMenu;

        public OptionsMenuManager(
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            SpriteFont font,
            Texture2D pixel)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = font;
            _pixel = pixel;

            CreateOptionsButtons();
            InitializeVolumeControls();
        }

        /// <summary>
        /// Met à jour le menu d'options
        /// </summary>
        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            HandleButtonClicks(mouseState, previousMouseState);
            HandleVolumeSlider(mouseState);
            UpdateVolumeVisuals();
        }

        /// <summary>
        /// Dessine le menu d'options
        /// </summary>
        public void Draw()
        {
            MouseState mouse = Mouse.GetState();

            // Titre
            DrawTitle("Options");

            // Boutons
            DrawButtons(_optionsButtons, mouse);

            // Contrôles de volume
            DrawVolumeControls();
        }

        /// <summary>
        /// Obtient le volume actuel de la musique (0.0 à 1.0)
        /// </summary>
        public float GetMusicVolume() => _musicVolume;

        /// <summary>
        /// Définit le volume de la musique (0.0 à 1.0)
        /// </summary>
        public void SetMusicVolume(float volume)
        {
            _musicVolume = MathHelper.Clamp(volume, 0f, 1f);
            MediaPlayer.Volume = _musicVolume;
        }

        // ==================== MÉTHODES PRIVÉES ====================

        private void CreateOptionsButtons()
        {
            _optionsButtons = new List<Button>
            {
                new Button("Music Volume +", new Vector2(0, 100)),
                new Button("Music Volume -", new Vector2(0, 156)),
                new Button("Back", new Vector2(0, 184))
            };
        }

        private void InitializeVolumeControls()
        {
            _volumeBar = new Rectangle(0, 134, 200, 8);
            UpdateVolumeVisuals();
        }

        private void HandleButtonClicks(MouseState mouseState, MouseState previousMouseState)
        {
            foreach (var button in _optionsButtons)
            {
                if (button.IsClicked(mouseState, previousMouseState))
                {
                    HandleButtonAction(button.Text);
                    return;
                }
            }
        }

        private void HandleButtonAction(string buttonText)
        {
            switch (buttonText)
            {
                case "Music Volume +":
                    SetMusicVolume(_musicVolume + 0.1f);
                    Console.WriteLine($"[OPTIONS] Volume increased to: {_musicVolume:F2}");
                    break;

                case "Music Volume -":
                    SetMusicVolume(_musicVolume - 0.1f);
                    Console.WriteLine($"[OPTIONS] Volume decreased to: {_musicVolume:F2}");
                    break;

                case "Back":
                    Console.WriteLine("[OPTIONS] Back to main menu");
                    OnBackToMainMenu?.Invoke();
                    break;
            }
        }

        private void HandleVolumeSlider(MouseState mouseState)
        {
            // Démarrer le drag
            if (mouseState.LeftButton == ButtonState.Pressed && 
                _volumeBar.Contains(mouseState.Position))
            {
                _draggingVolume = true;
            }

            // Arrêter le drag
            if (mouseState.LeftButton == ButtonState.Released)
            {
                _draggingVolume = false;
            }

            // Mettre à jour le volume pendant le drag
            if (_draggingVolume)
            {
                float newVolume = MathHelper.Clamp(
                    (mouseState.X - _volumeBar.X) / (float)_volumeBar.Width,
                    0f,
                    1f
                );
                SetMusicVolume(newVolume);
            }
        }

        private void UpdateVolumeVisuals()
        {
            _volumeFill = new Rectangle(
                _volumeBar.X,
                _volumeBar.Y,
                (int)(_volumeBar.Width * _musicVolume),
                _volumeBar.Height
            );

            _volumeHandle = new Rectangle(
                _volumeBar.X + _volumeFill.Width - 5,
                _volumeBar.Y - 4,
                10,
                _volumeBar.Height + 8
            );
        }

        private void DrawTitle(string text)
        {
            _spriteBatch.DrawString(
                _font,
                text,
                Vector2.Zero,
                Color.White,
                0f,
                Vector2.Zero,
                3f,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawButtons(List<Button> buttons, MouseState mouse)
        {
            foreach (var button in buttons)
            {
                button.Draw(_spriteBatch, _font, mouse);
            }
        }

        private void DrawVolumeControls()
        {
            // Barre de fond
            _spriteBatch.Draw(_pixel, _volumeBar, Color.Gray);

            // Remplissage (volume actuel)
            _spriteBatch.Draw(_pixel, _volumeFill, Color.Yellow);

            // Poignée
            _spriteBatch.Draw(_pixel, _volumeHandle, Color.White);

            // Texte du volume (pourcentage)
            string volumeText = $"{(_musicVolume * 100):F0}%";
            Vector2 textSize = _font.MeasureString(volumeText);
            Vector2 textPos = new Vector2(
                _volumeBar.X + _volumeBar.Width + 10,
                _volumeBar.Y - textSize.Y / 2 + _volumeBar.Height / 2
            );
            _spriteBatch.DrawString(_font, volumeText, textPos, Color.White);
        }
    }
}
