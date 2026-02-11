using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Gère l'écran de sélection de mission
    /// </summary>
    public class MissionSelectManager
    {
        // --- Références externes ---
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;

        // --- Boutons de sélection ---
        private List<Button> _missionButtons;

        // --- Mission sélectionnée ---
        private string _selectedMission = "";

        // --- Événements ---
        public event Action<string> OnMissionSelected;
        public event Action OnBackToMainMenu;

        public MissionSelectManager(SpriteBatch spriteBatch, SpriteFont font)
        {
            _spriteBatch = spriteBatch;
            _font = font;

            CreateMissionButtons();
        }

        /// <summary>
        /// Met à jour l'écran de sélection de mission
        /// </summary>
        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            HandleButtonClicks(mouseState, previousMouseState);
        }

        /// <summary>
        /// Dessine l'écran de sélection de mission
        /// </summary>
        public void Draw()
        {
            MouseState mouse = Mouse.GetState();

            // Titre
            DrawTitle("Select Mission");

            // Boutons
            DrawButtons(_missionButtons, mouse);
        }

        // ==================== MÉTHODES PRIVÉES ====================

        private void CreateMissionButtons()
        {
            string[] labels = { "Tutorial", "Survival", "Assault", "Defense", "Back" };
            int startY = 100;
            int step = 28;

            _missionButtons = labels.Select((text, index) =>
                new Button(text, new Vector2(0, startY + index * step))
            ).ToList();
        }

        private void HandleButtonClicks(MouseState mouseState, MouseState previousMouseState)
        {
            foreach (var button in _missionButtons)
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
                case "Tutorial":
                case "Survival":
                case "Assault":
                case "Defense":
                    _selectedMission = buttonText;
                    Console.WriteLine($"[MISSION SELECT] Mission selected: {buttonText}");
                    OnMissionSelected?.Invoke(buttonText);
                    break;

                case "Back":
                    Console.WriteLine("[MISSION SELECT] Back to main menu");
                    OnBackToMainMenu?.Invoke();
                    break;
            }
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
    }
}
