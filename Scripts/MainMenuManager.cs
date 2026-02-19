using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
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
        private readonly Dictionary<Button, string> _buttonActionByInstance = new();
        private MenuScreen _currentScreen = MenuScreen.Main;

        // --- Musique de menu ---
        private ContentManager _content;

        // --- État de sauvegarde ---
        private bool _hasSavedGame;

        // --- Événements pour communiquer avec Game1 ---
        public event Action OnNewGameRequested;
        public event Action OnCharacterCreationRequested;
        public event Action OnContinueRequested;
        public event Action OnMapEditorRequested;
        public event Action OnEncyclopediaRequested;
        public event Action OnBodyEditorRequested;
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

            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        }

        /// <summary>
        /// Charge les ressources du menu (boutons, musique)
        /// </summary>
        public void LoadContent(Microsoft.Xna.Framework.Content.ContentManager content)
        {
            _content = content;

            // Créer les boutons du menu
            _menuButtons = CreateMenuButtons();

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
        /// Désactive la musique du menu (seule la mission Centre-Ville conserve une musique)
        /// </summary>
        public void PlayRandomMenuSong()
        {
            MediaPlayer.Stop();
            Console.WriteLine("[MENU] Music disabled outside Centre-Ville mission");
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
            _buttonActionByInstance.Clear();

            string[] buttonActions =
            {
                "new_game",
                "continue",
                "map_editor",
                "character_creation",
                "encyclopedia",
                "body_editor",
                "options",
                "quit"
            };
            int titleBottom = (int)(_font.MeasureString("XCOM 3").Y * 3f);
            int startY = titleBottom + 24;
            int step = _font.LineSpacing + 10;

            List<Button> buttons = buttonActions.Select((action, index) =>
                new Button(LocalizationManager.Get($"menu.{action}"), new Vector2(0, startY + index * step))
            ).ToList();

            for (int i = 0; i < buttons.Count; i++)
            {
                _buttonActionByInstance[buttons[i]] = buttonActions[i];
            }

            return buttons;
        }

        private void UpdateButtonStates()
        {
            if (_currentScreen == MenuScreen.Main)
            {
                // "Continue"
                Button continueButton = _menuButtons.FirstOrDefault(button =>
                    _buttonActionByInstance.TryGetValue(button, out string action) && action == "continue");
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
                    if (_buttonActionByInstance.TryGetValue(button, out string action))
                    {
                        HandleButtonAction(action);
                    }
                    return; // Une seule action par frame
                }
            }
        }

        private void HandleButtonAction(string buttonAction)
        {
            switch (buttonAction)
            {
                case "continue":
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

                case "map_editor":
                    Console.WriteLine("[MENU] Map Editor requested");
                    OnMapEditorRequested?.Invoke();
                    break;

                case "new_game":
                    Console.WriteLine("[MENU] New Game requested");
                    OnNewGameRequested?.Invoke();
                    break;

                case "character_creation":
                    Console.WriteLine("[MENU] Character Creation requested");
                    OnCharacterCreationRequested?.Invoke();
                    break;

                case "encyclopedia":
                    Console.WriteLine("[MENU] Encyclopedia requested");
                    OnEncyclopediaRequested?.Invoke();
                    break;

                case "body_editor":
                    Console.WriteLine("[MENU] Body editor requested");
                    OnBodyEditorRequested?.Invoke();
                    break;

                case "options":
                    Console.WriteLine("[MENU] Options requested");
                    OnOptionsRequested?.Invoke();
                    break;

                case "quit":
                    Console.WriteLine("[MENU] Quit requested");
                    OnQuitRequested?.Invoke();
                    break;
            }
        }

        private void HandleLanguageChanged(SupportedLanguage language)
        {
            if (_currentScreen == MenuScreen.Main)
            {
                _menuButtons = CreateMainMenuButtons();
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
