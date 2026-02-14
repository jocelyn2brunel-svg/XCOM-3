using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Gère le menu des options (volume, thème de couleur, etc.)
    /// </summary>
    public class OptionsMenuManager
    {
        private enum ColorChannel { None, Red, Green, Blue }

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

        // --- Contrôle couleur UI (RGB) ---
        private byte _themeR;
        private byte _themeG;
        private byte _themeB;

        private Rectangle _redBar;
        private Rectangle _greenBar;
        private Rectangle _blueBar;

        private Rectangle _redFill;
        private Rectangle _greenFill;
        private Rectangle _blueFill;

        private Rectangle _redHandle;
        private Rectangle _greenHandle;
        private Rectangle _blueHandle;

        private ColorChannel _draggedChannel = ColorChannel.None;

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
            InitializeThemeControls();
        }

        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            HandleButtonClicks(mouseState, previousMouseState);
            HandleVolumeSlider(mouseState);
            HandleThemeSliders(mouseState);
            UpdateVolumeVisuals();
            UpdateThemeVisuals();
        }

        public void Draw()
        {
            MouseState mouse = Mouse.GetState();

            DrawTitle("Options");
            DrawButtons(_optionsButtons, mouse);
            DrawVolumeControls();
            DrawThemeControls();
        }

        public float GetMusicVolume() => _musicVolume;

        public void SetMusicVolume(float volume)
        {
            _musicVolume = MathHelper.Clamp(volume, 0f, 1f);
            MediaPlayer.Volume = _musicVolume;
        }

        private void CreateOptionsButtons()
        {
            _optionsButtons = new List<Button>
            {
                new Button("Music Volume +", new Vector2(0, 100)),
                new Button("Music Volume -", new Vector2(0, 156)),
                new Button("Back", new Vector2(0, 430))
            };
        }

        private void InitializeVolumeControls()
        {
            _volumeBar = new Rectangle(0, 134, 200, 8);
            UpdateVolumeVisuals();
        }

        private void InitializeThemeControls()
        {
            _themeR = UIThemeManager.PrimaryColor.R;
            _themeG = UIThemeManager.PrimaryColor.G;
            _themeB = UIThemeManager.PrimaryColor.B;

            int startY = 260;
            int spacing = 42;
            int width = 230;
            int height = 10;

            _redBar = new Rectangle(70, startY, width, height);
            _greenBar = new Rectangle(70, startY + spacing, width, height);
            _blueBar = new Rectangle(70, startY + spacing * 2, width, height);

            UpdateThemeVisuals();
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
            if (mouseState.LeftButton == ButtonState.Pressed && _volumeBar.Contains(mouseState.Position))
            {
                _draggingVolume = true;
            }

            if (mouseState.LeftButton == ButtonState.Released)
            {
                _draggingVolume = false;
            }

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

        private void HandleThemeSliders(MouseState mouseState)
        {
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                if (_draggedChannel == ColorChannel.None)
                {
                    if (_redBar.Contains(mouseState.Position)) _draggedChannel = ColorChannel.Red;
                    else if (_greenBar.Contains(mouseState.Position)) _draggedChannel = ColorChannel.Green;
                    else if (_blueBar.Contains(mouseState.Position)) _draggedChannel = ColorChannel.Blue;
                }
            }
            else
            {
                _draggedChannel = ColorChannel.None;
                return;
            }

            if (_draggedChannel == ColorChannel.None)
            {
                return;
            }

            switch (_draggedChannel)
            {
                case ColorChannel.Red:
                    _themeR = GetChannelValueFromMouse(mouseState.X, _redBar);
                    break;
                case ColorChannel.Green:
                    _themeG = GetChannelValueFromMouse(mouseState.X, _greenBar);
                    break;
                case ColorChannel.Blue:
                    _themeB = GetChannelValueFromMouse(mouseState.X, _blueBar);
                    break;
            }

            UIThemeManager.SetPrimaryColor(_themeR, _themeG, _themeB);
        }

        private byte GetChannelValueFromMouse(int mouseX, Rectangle bar)
        {
            float normalized = MathHelper.Clamp((mouseX - bar.X) / (float)bar.Width, 0f, 1f);
            return (byte)(normalized * 255f);
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

        private void UpdateThemeVisuals()
        {
            _redFill = BuildChannelFill(_redBar, _themeR);
            _greenFill = BuildChannelFill(_greenBar, _themeG);
            _blueFill = BuildChannelFill(_blueBar, _themeB);

            _redHandle = BuildHandle(_redBar, _themeR);
            _greenHandle = BuildHandle(_greenBar, _themeG);
            _blueHandle = BuildHandle(_blueBar, _themeB);
        }

        private Rectangle BuildChannelFill(Rectangle bar, byte value)
        {
            return new Rectangle(
                bar.X,
                bar.Y,
                (int)(bar.Width * (value / 255f)),
                bar.Height
            );
        }

        private Rectangle BuildHandle(Rectangle bar, byte value)
        {
            int x = bar.X + (int)(bar.Width * (value / 255f)) - 5;
            return new Rectangle(x, bar.Y - 4, 10, bar.Height + 8);
        }

        private void DrawTitle(string text)
        {
            _spriteBatch.DrawString(
                _font,
                text,
                Vector2.Zero,
                UIThemeManager.PrimaryColor,
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
            _spriteBatch.Draw(_pixel, _volumeBar, Color.Gray);
            _spriteBatch.Draw(_pixel, _volumeFill, UIThemeManager.PrimaryColor);
            _spriteBatch.Draw(_pixel, _volumeHandle, Color.White);

            string volumeText = $"{(_musicVolume * 100):F0}%";
            Vector2 textSize = _font.MeasureString(volumeText);
            Vector2 textPos = new Vector2(
                _volumeBar.X + _volumeBar.Width + 10,
                _volumeBar.Y - textSize.Y / 2 + _volumeBar.Height / 2
            );
            _spriteBatch.DrawString(_font, volumeText, textPos, UIThemeManager.PrimaryColor);
        }

        private void DrawThemeControls()
        {
            _spriteBatch.DrawString(_font, "UI Color (RGB)", new Vector2(0, 220), UIThemeManager.PrimaryColor);

            DrawChannel("R", _redBar, _redFill, _redHandle, Color.Red, _themeR);
            DrawChannel("G", _greenBar, _greenFill, _greenHandle, Color.LimeGreen, _themeG);
            DrawChannel("B", _blueBar, _blueFill, _blueHandle, Color.DeepSkyBlue, _themeB);

            Rectangle previewRect = new Rectangle(0, 380, 42, 22);
            _spriteBatch.Draw(_pixel, previewRect, UIThemeManager.PrimaryColor);
            _spriteBatch.DrawString(_font, $"#{_themeR:X2}{_themeG:X2}{_themeB:X2}", new Vector2(52, 380), UIThemeManager.PrimaryColor);
        }

        private void DrawChannel(string label, Rectangle bar, Rectangle fill, Rectangle handle, Color fillColor, byte value)
        {
            _spriteBatch.DrawString(_font, label, new Vector2(0, bar.Y - 8), fillColor);
            _spriteBatch.Draw(_pixel, bar, Color.Gray);
            _spriteBatch.Draw(_pixel, fill, fillColor);
            _spriteBatch.Draw(_pixel, handle, Color.White);
            _spriteBatch.DrawString(_font, value.ToString(), new Vector2(bar.Right + 12, bar.Y - 8), UIThemeManager.PrimaryColor);
        }
    }
}
