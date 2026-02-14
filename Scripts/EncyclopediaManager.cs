using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Gère l'encyclopédie (armes, armures, unités)
    /// </summary>
    public class EncyclopediaManager
    {
        // --- Références externes ---
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;

        // --- Données de référence ---
        private readonly Dictionary<string, WeaponData> _weaponDatabase;
        private readonly InventorySystem _inventorySystem;
        private readonly List<EnemyTemplate> _enemyPool;

        // --- État ---
        private List<Button> _encyclopediaButtons;
        private string _currentCategory = "Weapons"; // "Weapons", "Armor", "Units"

        // --- Constantes d'affichage ---
        private const int ContentX = 250;
        private const int ContentY = 100;
        private const int LineHeight = 25;

        // --- Événements ---
        public event Action OnBackToMainMenu;

        // --- État scroll ---
        private int _scrollOffset = 0;
        private const int ScrollSpeed = 20; // Pixels par "tick" de molette
        private const int ScrollPadding = 50; // Marge haute/basse pour éviter que le texte sorte complètement


        public EncyclopediaManager(
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            SpriteFont font,
            Dictionary<string, WeaponData> weaponDatabase,
            InventorySystem inventorySystem,
            List<EnemyTemplate> enemyPool)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = font;
            _weaponDatabase = weaponDatabase;
            _inventorySystem = inventorySystem;
            _enemyPool = enemyPool;

            CreateEncyclopediaButtons();
        }

        /// <summary>
        /// Met à jour l'encyclopédie
        /// </summary>
        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            HandleButtonClicks(mouseState, previousMouseState);
            HandleScroll(mouseState, previousMouseState);
        }

        private void HandleScroll(MouseState mouseState, MouseState previousMouseState)
        {
            int delta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;

            // Sens inversé pour un scroll naturel
            _scrollOffset -= delta / 120 * ScrollSpeed;

            // Limiter le scroll pour ne pas dépasser le contenu
            int contentHeight = GetCurrentContentHeight();
            int viewHeight = _graphicsDevice.Viewport.Height - ContentY - ScrollPadding;

            if (_scrollOffset < 0) _scrollOffset = 0;
            if (_scrollOffset > Math.Max(0, contentHeight - viewHeight))
                _scrollOffset = contentHeight - viewHeight;
        }

        private int GetCurrentContentHeight()
        {
            int y = 0;

            switch (_currentCategory)
            {
                case "Weapons":
                    y = _weaponDatabase.Count * (LineHeight * 4 + 10) + 60;
                    break;
                case "Armor":
                    y = _inventorySystem.ItemDatabase.Values.Count(i => i.Type == ItemType.Armor) * (LineHeight * 3 + 10) + 60;
                    break;
                case "Units":
                    y = _enemyPool.Count * (LineHeight * 5) + 200; // Ajuster selon ton layout
                    break;
            }

            return y;
        }


        /// <summary>
        /// Dessine l'encyclopédie
        /// </summary>
        public void Draw()
        {
            MouseState mouse = Mouse.GetState();

            // Titre principal
            DrawMainTitle("ENCYCLOPEDIE");

            // Boutons de catégorie
            DrawButtons(_encyclopediaButtons, mouse);

            // Contenu de la catégorie sélectionnée
            DrawCategoryContent();

            // Hint de retour
            DrawBackHint();
        }

        /// <summary>
        /// Définit la catégorie affichée
        /// </summary>
        public void SetCategory(string category)
        {
            _currentCategory = category;
        }

        // ==================== MÉTHODES PRIVÉES ====================

        private void CreateEncyclopediaButtons()
        {
            string[] labels = { "Armes", "Armures", "Unités", "Retour" };
            int startY = 100;
            int step = 28;

            _encyclopediaButtons = labels.Select((text, index) =>
                new Button(text, new Vector2(0, startY + index * step))
            ).ToList();
        }

        private void HandleButtonClicks(MouseState mouseState, MouseState previousMouseState)
        {
            foreach (var button in _encyclopediaButtons)
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
                case "Armes":
                    _currentCategory = "Weapons";
                    Console.WriteLine("[ENCYCLOPEDIA] Switched to Weapons");
                    break;

                case "Armures":
                    _currentCategory = "Armor";
                    Console.WriteLine("[ENCYCLOPEDIA] Switched to Armor");
                    break;

                case "Unités":
                    _currentCategory = "Units";
                    Console.WriteLine("[ENCYCLOPEDIA] Switched to Units");
                    break;

                case "Retour":
                    Console.WriteLine("[ENCYCLOPEDIA] Back to main menu");
                    OnBackToMainMenu?.Invoke();
                    break;
            }
        }

        private void DrawMainTitle(string text)
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

        private void DrawCategoryContent()
        {
            int y = ContentY;

            switch (_currentCategory)
            {
                case "Weapons":
                    DrawWeaponsContent(ref y);
                    break;

                case "Armor":
                    DrawArmorContent(ref y);
                    break;

                case "Units":
                    DrawUnitsContent(ref y);
                    break;
            }
        }

        private void DrawWeaponsContent(ref int y)
        {
            y = ContentY - _scrollOffset;

            // Titre de section
            _spriteBatch.DrawString(
                _font,
                "=== ARMES ===",
                new Vector2(ContentX, y),
                Color.Yellow,
                0f,
                Vector2.Zero,
                1.5f,
                SpriteEffects.None,
                0f
            );
            y += 40;

            // Liste des armes
            foreach (var weapon in _weaponDatabase.Values.OrderBy(w => w.Name))
            {
                // Nom de l'arme
                _spriteBatch.DrawString(
                    _font,
                    weapon.Name,
                    new Vector2(ContentX, y),
                    Color.Cyan,
                    0f,
                    Vector2.Zero,
                    1.2f,
                    SpriteEffects.None,
                    0f
                );
                y += LineHeight;

                // Statistiques
                _spriteBatch.DrawString(_font, $"  Dégâts: {weapon.Damage}", new Vector2(ContentX + 20, y), Color.White);
                y += LineHeight;

                _spriteBatch.DrawString(_font, $"  Précision: {weapon.Accuracy}%", new Vector2(ContentX + 20, y), Color.White);
                y += LineHeight;

                _spriteBatch.DrawString(_font, $"  Portée: {weapon.Range} cases", new Vector2(ContentX + 20, y), Color.White);
                y += LineHeight + 10;
            }
        }

        private void DrawArmorContent(ref int y)
        {
            y = ContentY - _scrollOffset;

            // Titre de section
            _spriteBatch.DrawString(
                _font,
                "=== ARMURES ===",
                new Vector2(ContentX, y),
                Color.Yellow,
                0f,
                Vector2.Zero,
                1.5f,
                SpriteEffects.None,
                0f
            );
            y += 40;

            // Liste des armures
            var armors = _inventorySystem.ItemDatabase.Values
                .Where(i => i.Type == ItemType.Armor)
                .OrderBy(a => a.Name);

            foreach (var armor in armors)
            {
                // Nom de l'armure
                _spriteBatch.DrawString(
                    _font,
                    armor.Name,
                    new Vector2(ContentX, y),
                    Color.Cyan,
                    0f,
                    Vector2.Zero,
                    1.2f,
                    SpriteEffects.None,
                    0f
                );
                y += LineHeight;

                // Statistiques
                _spriteBatch.DrawString(_font, $"  Protection: {armor.ArmorValue}", new Vector2(ContentX + 20, y), Color.White);
                y += LineHeight;

                string slot = armor.ArmorSlot switch
                {
                    ArmorSlot.Head => "Tête",
                    ArmorSlot.Torso => "Torse",
                    ArmorSlot.Shield => "Bouclier",
                    ArmorSlot.Shirt => "Chemise",
                    ArmorSlot.Pants => "Pantalon",
                    _ => "Inconnu"
                };
                _spriteBatch.DrawString(_font, $"  Emplacement: {slot}", new Vector2(ContentX + 20, y), Color.White);
                y += LineHeight;

                _spriteBatch.DrawString(_font, $"  Poids: {armor.WeightLbs:0.#} lb", new Vector2(ContentX + 20, y), Color.White);
                y += LineHeight;

                if (armor.BonusInventorySlots > 0)
                {
                    _spriteBatch.DrawString(_font, $"  Poches: {armor.BonusInventorySlots}x 1x1", new Vector2(ContentX + 20, y), Color.White);
                    y += LineHeight;
                }

                y += 10;
            }
        }

        private void DrawUnitsContent(ref int y)
        {
            y = ContentY - _scrollOffset;

            // Titre de section
            _spriteBatch.DrawString(
                _font,
                "=== UNITÉS ===",
                new Vector2(ContentX, y),
                Color.Yellow,
                0f,
                Vector2.Zero,
                1.5f,
                SpriteEffects.None,
                0f
            );
            y += 40;

            // === ÉQUIPE JOUEUR ===
            _spriteBatch.DrawString(
                _font,
                "ÉQUIPE JOUEUR:",
                new Vector2(ContentX, y),
                Color.Blue,
                0f,
                Vector2.Zero,
                1.3f,
                SpriteEffects.None,
                0f
            );
            y += 35;

            DrawUnitInfo("Soldat", "Assault", "Rifle", 100, 3, ref y);
            y += 20;

            // === ENNEMIS ===
            _spriteBatch.DrawString(
                _font,
                "ENNEMIS:",
                new Vector2(ContentX, y),
                Color.Red,
                0f,
                Vector2.Zero,
                1.3f,
                SpriteEffects.None,
                0f
            );
            y += 35;

            foreach (var enemy in _enemyPool)
            {
                var weapon = _weaponDatabase[enemy.Weapon];
                DrawEnemyInfo(enemy, weapon, ref y);
                y += 15;
            }
        }

        private void DrawUnitInfo(string name, string unitClass, string weaponName, int hp, int ap, ref int y)
        {
            _spriteBatch.DrawString(
                _font,
                name,
                new Vector2(ContentX, y),
                Color.Cyan,
                0f,
                Vector2.Zero,
                1.2f,
                SpriteEffects.None,
                0f
            );
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  Classe: {unitClass}", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  Arme de base: {weaponName}", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  PV: {hp}", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  PA: {ap} par tour", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;
        }

        private void DrawEnemyInfo(EnemyTemplate enemy, WeaponData weapon, ref int y)
        {
            _spriteBatch.DrawString(
                _font,
                enemy.Name,
                new Vector2(ContentX, y),
                Color.Cyan,
                0f,
                Vector2.Zero,
                1.2f,
                SpriteEffects.None,
                0f
            );
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  Classe: {enemy.Class}", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  Arme: {enemy.Weapon}", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  Dégâts: {weapon.Damage} | Portée: {weapon.Range}", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;

            _spriteBatch.DrawString(_font, $"  PA: {enemy.ActionPoints} par tour", new Vector2(ContentX + 20, y), Color.White);
            y += LineHeight;
        }

        private void DrawBackHint()
        {
            _spriteBatch.DrawString(
                _font,
                "Appuyez sur ESC pour retourner au menu",
                new Vector2(10, _graphicsDevice.Viewport.Height - 30),
                Color.Yellow
            );
        }
    }
}
