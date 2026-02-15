using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Gère l'affichage et la logique du menu principal
    /// </summary>
    public class MainMenuManager
    {
        private enum MenuScreen
        {
            Main
        }

        // --- Références externes ---
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private readonly Random _random;

        // --- Boutons du menu ---
        private List<Button> _menuButtons;
        private MenuScreen _currentScreen = MenuScreen.Main;

        // --- Musique de menu ---
        private List<Song> _menuSongs;
        private Song _currentSong;

        // --- État de sauvegarde ---
        private bool _hasSavedGame;

        // --- Événements pour communiquer avec Game1 ---
        public event Action OnNewGameRequested;
        public event Action OnCharacterCreationRequested;
        public event Action OnContinueRequested;
        public event Action OnMapEditorRequested;
        public event Action OnEncyclopediaRequested;
        public event Action OnOptionsRequested;
        public event Action OnQuitRequested;

        public MainMenuManager(
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            SpriteFont font,
            Random random)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = font;
            _random = random;
        }

        /// <summary>
        /// Charge les ressources du menu (boutons, musique)
        /// </summary>
        public void LoadContent(Microsoft.Xna.Framework.Content.ContentManager content)
        {
            // Créer les boutons du menu
            _menuButtons = CreateMenuButtons();

            // Charger les musiques de menu
            _menuSongs = new[]
            {
                "menu_music_1",
                "menu_music_2",
                "menu_music_3",
                "menu_music_4"
            }.Select(content.Load<Song>).ToList();

            // Jouer une musique aléatoire
            PlayRandomMenuSong();
        }

        /// <summary>
        /// Met à jour l'état du menu (gère les clics)
        /// </summary>
        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            UpdateButtonStates();

            // Gérer les clics sur les boutons
            HandleButtonClicks(mouseState, previousMouseState);
        }

        /// <summary>
        /// Dessine le menu principal
        /// </summary>
        public void Draw()
        {
            MouseState mouse = Mouse.GetState();

            // Titre
            DrawTitle("XCOM 3");

            // Boutons
            DrawButtons(_menuButtons, mouse);
        }

        /// <summary>
        /// Active ou désactive la sauvegarde disponible
        /// </summary>
        public void SetHasSavedGame(bool hasSavedGame)
        {
            _hasSavedGame = hasSavedGame;
        }

        public void ResetToRootMenu()
        {
            _currentScreen = MenuScreen.Main;
            _menuButtons = CreateMainMenuButtons();
        }

        /// <summary>
        /// Joue une musique de menu aléatoire
        /// </summary>
        public void PlayRandomMenuSong()
        {
            _currentSong = _menuSongs[_random.Next(_menuSongs.Count)];
            MediaPlayer.Play(_currentSong);
            MediaPlayer.Volume = 0.5f;
            
            Console.WriteLine($"[MENU] Playing: {_currentSong.Name}");
        }

        /// <summary>
        /// Arrête la musique du menu
        /// </summary>
        public void StopMusic()
        {
            MediaPlayer.Stop();
        }

        // ==================== MÉTHODES PRIVÉES ====================

        private List<Button> CreateMenuButtons()
        {
            return CreateMainMenuButtons();
        }

        private List<Button> CreateMainMenuButtons()
        {
            string[] labels = { "New Game", "Continue", "Map Editor", "Character Creation", "Encyclopedia", "Options", "Quit" };
            int startY = 100;
            int step = 28;

            return labels.Select((text, index) =>
                new Button(text, new Vector2(0, startY + index * step))
            ).ToList();
        }

        private void UpdateButtonStates()
        {
            if (_currentScreen == MenuScreen.Main)
            {
                // "Continue"
                Button continueButton = _menuButtons.FirstOrDefault(button => button.Text == "Continue");
                if (continueButton != null)
                {
                    continueButton.IsEnabled = _hasSavedGame;
                }
            }
        }

        private void HandleButtonClicks(MouseState mouseState, MouseState previousMouseState)
        {
            foreach (var button in _menuButtons)
            {
                if (button.IsClicked(mouseState, previousMouseState))
                {
                    HandleButtonAction(button.Text);
                    return; // Une seule action par frame
                }
            }
        }

        private void HandleButtonAction(string buttonText)
        {
            switch (buttonText)
            {
                case "Continue":
                    if (_hasSavedGame)
                    {
                        Console.WriteLine("[MENU] Continue requested");
                        OnContinueRequested?.Invoke();
                    }
                    else
                    {
                        Console.WriteLine("[MENU] No saved game to continue!");
                    }
                    break;

                case "Map Editor":
                    Console.WriteLine("[MENU] Map Editor requested");
                    OnMapEditorRequested?.Invoke();
                    break;

                case "New Game":
                    Console.WriteLine("[MENU] New Game requested");
                    OnNewGameRequested?.Invoke();
                    break;

                case "Character Creation":
                    Console.WriteLine("[MENU] Character Creation requested");
                    OnCharacterCreationRequested?.Invoke();
                    break;

                case "Encyclopedia":
                    Console.WriteLine("[MENU] Encyclopedia requested");
                    OnEncyclopediaRequested?.Invoke();
                    break;

                case "Options":
                    Console.WriteLine("[MENU] Options requested");
                    OnOptionsRequested?.Invoke();
                    break;

                case "Quit":
                    Console.WriteLine("[MENU] Quit requested");
                    OnQuitRequested?.Invoke();
                    break;
            }
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
    }
}
