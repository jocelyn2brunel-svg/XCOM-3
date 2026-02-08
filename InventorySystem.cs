using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Système d'inventaire complet avec interface utilisateur style Diablo
    /// Gère : drag & drop, rotation, équipement, affichage
    /// </summary>
    public class InventorySystem
    {
        // ═══════════════════════════════════════════════════════════════════════
        // CONSTANTES
        // ═══════════════════════════════════════════════════════════════════════

        private const int GRID_WIDTH = 10;
        private const int GRID_HEIGHT = 10;
        private const int CELL_SIZE = 40;

        // ═══════════════════════════════════════════════════════════════════════
        // ÉTAT
        // ═══════════════════════════════════════════════════════════════════════

        private InventoryGrid inventoryGrid;
        private GridItem draggedItem = null;
        private Point dragGridOffset;
        public Dictionary<string, ItemData> ItemDatabase { get; private set; }

        // Ressources graphiques (injectées)
        private SpriteBatch spriteBatch;
        private SpriteFont font;
        private Texture2D pixel;
        private GraphicsDevice graphicsDevice;

        // État des touches
        private KeyboardState previousKeyboardState;

        // ═══════════════════════════════════════════════════════════════════════
        // CONSTRUCTEUR
        // ═══════════════════════════════════════════════════════════════════════

        public InventorySystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, 
            SpriteFont font, Texture2D pixel)
        {
            this.graphicsDevice = graphicsDevice;
            this.spriteBatch = spriteBatch;
            this.font = font;
            this.pixel = pixel;

            inventoryGrid = new InventoryGrid(GRID_WIDTH, GRID_HEIGHT);
            ItemDatabase = new Dictionary<string, ItemData>();

            InitializeItemDatabase();
            InitializeInventoryItems();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INITIALISATION
        // ═══════════════════════════════════════════════════════════════════════

        private void InitializeItemDatabase()
        {
            // Armes
            ItemDatabase["Rifle"] = new ItemData("Rifle", ItemType.Weapon,
                new WeaponData("Rifle", 25, 80, 5));
            ItemDatabase["Plasma Rifle"] = new ItemData("Plasma Rifle", ItemType.Weapon,
                new WeaponData("Plasma Rifle", 30, 75, 5));
            ItemDatabase["Plasma Sniper"] = new ItemData("Plasma Sniper", ItemType.Weapon,
                new WeaponData("Plasma Sniper", 50, 90, 8));
            ItemDatabase["Shotgun"] = new ItemData("Shotgun", ItemType.Weapon,
                new WeaponData("Shotgun", 45, 70, 3));
            ItemDatabase["SMG"] = new ItemData("SMG", ItemType.Weapon,
                new WeaponData("SMG", 20, 75, 4));

            // Armures (charger depuis ArmorDatabase)
            foreach (var armor in ArmorDatabase.GetAllArmors())
            {
                ItemDatabase[armor.Name] = armor;
            }
        }

        private void InitializeInventoryItems()
        {
            inventoryGrid.Clear();

            var items = new List<(string name, Point pos, bool rotated)>
            {
                // Armes
                ("Plasma Rifle", new Point(0, 0), false),
                ("Shotgun", new Point(2, 0), false),
                ("SMG", new Point(4, 0), false),
                
                // Casques
                ("PASGT Helmet", new Point(0, 5), false),
                ("ACH", new Point(2, 5), false),
                ("ECH", new Point(4, 5), false),
                
                // Gilets
                ("PASGT Vest", new Point(6, 0), false),
                ("OTV (IBA)", new Point(8, 0), false),
                ("MTV", new Point(6, 3), false),
                
                // Gilets avec plaques
                ("OTV + SAPI", new Point(8, 3), false),
                ("IOTV + ESAPI", new Point(6, 6), false),
                
                // Équipement spécial
                ("Riot Shield", new Point(0, 4), true),
                ("Army Combat Shirt", new Point(8, 6), false),
            };

            foreach (var (name, pos, rotated) in items)
            {
                if (ItemDatabase.ContainsKey(name))
                {
                    ItemSize size = ItemSizeDatabase.GetItemSize(name);
                    GridItem gridItem = new GridItem(ItemDatabase[name], pos, size, rotated);
                    gridItem.UpdatePixelBounds();

                    if (!inventoryGrid.PlaceItem(gridItem))
                    {
                        Console.WriteLine($"[INVENTORY] Impossible de placer {name} à {pos}");
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════════════

        public void Update(MouseState mouse, bool leftClick, KeyboardState keyboard, Unit selectedUnit)
        {
            if (selectedUnit == null) return;

            int panelWidth = 750;
            int panelHeight = 500;
            int gridStartX = graphicsDevice.Viewport.Width / 2 - panelWidth / 2 + 20;
            int gridStartY = graphicsDevice.Viewport.Height / 2 - panelHeight / 2 + 60;

            // Rotation avec touche R
            bool rPressed = keyboard.IsKeyDown(Keys.R) && previousKeyboardState.IsKeyUp(Keys.R);
            if (rPressed && draggedItem != null)
            {
                draggedItem.Rotate();
                Console.WriteLine($"[INVENTORY] Item tourné: {draggedItem.Data.Name}");
            }

            // Démarrer le drag
            if (leftClick && draggedItem == null)
            {
                HandleStartDrag(mouse, selectedUnit, gridStartX, gridStartY);
            }

            // Drag en cours
            if (draggedItem != null && mouse.LeftButton == ButtonState.Pressed)
            {
                HandleDragUpdate(mouse, gridStartX, gridStartY);
            }

            // Terminer le drag
            if (draggedItem != null && mouse.LeftButton == ButtonState.Released)
            {
                HandleEndDrag(mouse, selectedUnit, gridStartX, gridStartY);
            }

            previousKeyboardState = keyboard;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GESTION DU DRAG & DROP
        // ═══════════════════════════════════════════════════════════════════════

        private void HandleStartDrag(MouseState mouse, Unit unit, int gridStartX, int gridStartY)
        {
            // Convertir position souris en position grille
            int gridX = (mouse.X - gridStartX) / CELL_SIZE;
            int gridY = (mouse.Y - gridStartY) / CELL_SIZE;

            // Vérifier clic dans la grille
            if (gridX >= 0 && gridX < GRID_WIDTH && gridY >= 0 && gridY < GRID_HEIGHT)
            {
                GridItem clickedItem = inventoryGrid.GetItemAt(new Point(gridX, gridY));

                if (clickedItem != null)
                {
                    draggedItem = clickedItem;
                    dragGridOffset = new Point(gridX - clickedItem.GridPosition.X,
                                              gridY - clickedItem.GridPosition.Y);
                    inventoryGrid.RemoveItem(draggedItem);
                    return;
                }
            }

            // Vérifier les slots équipés
            CheckEquipmentSlotDrag(mouse, unit.EquippedWeapon, GetWeaponSlotBounds());
            CheckEquipmentSlotDrag(mouse, unit.EquippedHelmet, GetHelmetSlotBounds());
            CheckEquipmentSlotDrag(mouse, unit.EquippedArmor, GetArmorSlotBounds());
            CheckEquipmentSlotDrag(mouse, unit.EquippedShield, GetShieldSlotBounds());
            CheckEquipmentSlotDrag(mouse, unit.EquippedShirt, GetShirtSlotBounds());
        }

        private void HandleDragUpdate(MouseState mouse, int gridStartX, int gridStartY)
        {
            int newGridX = (mouse.X - gridStartX) / CELL_SIZE - dragGridOffset.X;
            int newGridY = (mouse.Y - gridStartY) / CELL_SIZE - dragGridOffset.Y;

            draggedItem.GridPosition = new Point(
                Math.Max(0, Math.Min(GRID_WIDTH - 1, newGridX)),
                Math.Max(0, Math.Min(GRID_HEIGHT - 1, newGridY))
            );

            draggedItem.UpdatePixelBounds(gridStartX, gridStartY);
        }

        private void HandleEndDrag(MouseState mouse, Unit unit, int gridStartX, int gridStartY)
        {
            bool equipped = TryEquipInSlot(mouse, draggedItem, unit);

            if (!equipped)
            {
                // Essayer de replacer dans la grille
                if (inventoryGrid.CanPlaceItem(draggedItem.GridPosition, draggedItem.GetCurrentSize()))
                {
                    inventoryGrid.PlaceItem(draggedItem);
                }
                else
                {
                    // Trouver une position libre
                    Point? freePos = inventoryGrid.FindFreePosition(draggedItem.GetCurrentSize(), true);

                    if (freePos.HasValue)
                    {
                        draggedItem.GridPosition = freePos.Value;
                        draggedItem.UpdatePixelBounds(gridStartX, gridStartY);
                        inventoryGrid.PlaceItem(draggedItem);
                    }
                    else
                    {
                        Console.WriteLine("[INVENTORY] Pas de place dans l'inventaire!");
                    }
                }
            }

            draggedItem = null;
        }

        private void CheckEquipmentSlotDrag(MouseState mouse, Item equippedItem, Rectangle slot)
        {
            if (equippedItem != null && slot.Contains(mouse.Position) && draggedItem == null)
            {
                ItemSize size = ItemSizeDatabase.GetItemSize(equippedItem.Data.Name);
                draggedItem = new GridItem(equippedItem.Data, new Point(0, 0), size, false);
            }
        }

        private bool TryEquipInSlot(MouseState mouse, GridItem item, Unit unit)
        {
            // Équiper une arme
            if (item.Data.Type == ItemType.Weapon && GetWeaponSlotBounds().Contains(mouse.Position))
            {
                if (unit.EquippedWeapon != null)
                    ReturnItemToGrid(unit.EquippedWeapon);

                unit.EquippedWeapon = new Item(item.Data, Point.Zero);
                unit.Weapon = item.Data.Name;
                unit.WeaponData = item.Data.WeaponData;
                return true;
            }

            // Équiper une armure
            if (item.Data.Type == ItemType.Armor)
            {
                if (item.Data.ArmorSlot == ArmorSlot.Head && GetHelmetSlotBounds().Contains(mouse.Position))
                {
                    if (unit.EquippedHelmet != null)
                        ReturnItemToGrid(unit.EquippedHelmet);
                    unit.EquippedHelmet = new Item(item.Data, Point.Zero);
                    return true;
                }
                else if (item.Data.ArmorSlot == ArmorSlot.Torso && GetArmorSlotBounds().Contains(mouse.Position))
                {
                    if (unit.EquippedArmor != null)
                        ReturnItemToGrid(unit.EquippedArmor);
                    unit.EquippedArmor = new Item(item.Data, Point.Zero);
                    return true;
                }
                else if (item.Data.ArmorSlot == ArmorSlot.Shield && GetShieldSlotBounds().Contains(mouse.Position))
                {
                    if (unit.EquippedShield != null)
                        ReturnItemToGrid(unit.EquippedShield);
                    unit.EquippedShield = new Item(item.Data, Point.Zero);
                    return true;
                }
                else if (item.Data.ArmorSlot == ArmorSlot.Shirt && GetShirtSlotBounds().Contains(mouse.Position))
                {
                    if (unit.EquippedShirt != null)
                        ReturnItemToGrid(unit.EquippedShirt);
                    unit.EquippedShirt = new Item(item.Data, Point.Zero);
                    return true;
                }
            }

            return false;
        }

        private void ReturnItemToGrid(Item item)
        {
            ItemSize size = ItemSizeDatabase.GetItemSize(item.Data.Name);
            Point? freePos = inventoryGrid.FindFreePosition(size, true);

            if (freePos.HasValue)
            {
                GridItem gridItem = new GridItem(item.Data, freePos.Value, size, false);
                inventoryGrid.PlaceItem(gridItem);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RENDU
        // ═══════════════════════════════════════════════════════════════════════

        public void Draw(Unit selectedUnit)
        {
            if (selectedUnit == null) return;

            int panelWidth = 750;
            int panelHeight = 500;
            int panelX = graphicsDevice.Viewport.Width / 2 - panelWidth / 2;
            int panelY = graphicsDevice.Viewport.Height / 2 - panelHeight / 2;

            // Fond du panneau
            Rectangle panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);
            spriteBatch.Draw(pixel, panel, new Color(20, 20, 20, 240));
            DrawRectangleBorder(panel, Color.Gold, 3);

            // Titre
            string title = $"Inventaire - {selectedUnit.Name}";
            Vector2 titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title,
                new Vector2(panel.Center.X - titleSize.X * 0.75f, panelY + 10),
                Color.Gold, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);

            // Grille d'inventaire
            int gridStartX = panelX + 20;
            int gridStartY = panelY + 60;
            DrawInventoryGrid(gridStartX, gridStartY);

            // Slots d'équipement
            int equipX = gridStartX + GRID_WIDTH * CELL_SIZE + 30;
            int equipY = gridStartY;
            DrawEquipmentSlots(equipX, equipY, selectedUnit);

            // Item en cours de drag
            if (draggedItem != null)
            {
                DrawGridItem(draggedItem, 0.7f);
            }

            // Instructions
            spriteBatch.DrawString(font, "Glissez pour équiper | R: Tourner",
                new Vector2(panelX + 20, panelY + panelHeight - 30), Color.Yellow);
            spriteBatch.DrawString(font, "I ou ESC pour fermer",
                new Vector2(panelX + 20, panelY + panelHeight - 15), Color.Yellow);
        }

        private void DrawInventoryGrid(int gridStartX, int gridStartY)
        {
            int gridPixelWidth = GRID_WIDTH * CELL_SIZE;
            int gridPixelHeight = GRID_HEIGHT * CELL_SIZE;
            Rectangle gridArea = new Rectangle(gridStartX, gridStartY, gridPixelWidth, gridPixelHeight);

            // Fond
            spriteBatch.Draw(pixel, gridArea, new Color(40, 40, 40, 200));

            // Lignes verticales
            for (int x = 0; x <= GRID_WIDTH; x++)
            {
                Rectangle vertLine = new Rectangle(gridStartX + x * CELL_SIZE, gridStartY, 1, gridPixelHeight);
                spriteBatch.Draw(pixel, vertLine, new Color(60, 60, 60));
            }

            // Lignes horizontales
            for (int y = 0; y <= GRID_HEIGHT; y++)
            {
                Rectangle horizLine = new Rectangle(gridStartX, gridStartY + y * CELL_SIZE, gridPixelWidth, 1);
                spriteBatch.Draw(pixel, horizLine, new Color(60, 60, 60));
            }

            DrawRectangleBorder(gridArea, Color.Gray, 2);

            // Label
            spriteBatch.DrawString(font, "Items Disponibles:",
                new Vector2(gridStartX, gridStartY - 20), Color.White);

            // Items
            foreach (var item in inventoryGrid.GetAllItems())
            {
                if (item != draggedItem)
                {
                    item.UpdatePixelBounds(gridStartX, gridStartY);
                    DrawGridItem(item);
                }
            }
        }

        private void DrawEquipmentSlots(int equipX, int equipY, Unit unit)
        {
            Rectangle equipArea = new Rectangle(equipX, equipY, 190, 400);
            spriteBatch.Draw(pixel, equipArea, new Color(40, 40, 40, 200));
            DrawRectangleBorder(equipArea, Color.Gray, 2);

            spriteBatch.DrawString(font, "Equipement:",
                new Vector2(equipX + 5, equipY - 20), Color.White);

            DrawEquipmentSlot(GetWeaponSlotBounds(), "Arme", unit.EquippedWeapon);
            DrawEquipmentSlot(GetHelmetSlotBounds(), "Casque", unit.EquippedHelmet);
            DrawEquipmentSlot(GetArmorSlotBounds(), "Gilet", unit.EquippedArmor);
            DrawEquipmentSlot(GetShieldSlotBounds(), "Bouclier", unit.EquippedShield);
            DrawEquipmentSlot(GetShirtSlotBounds(), "Chemise", unit.EquippedShirt);
        }

        private void DrawGridItem(GridItem item, float alpha = 1f)
        {
            Color itemColor = ItemSizeDatabase.GetItemColor(item.Data.Type) * alpha;

            spriteBatch.Draw(pixel, item.PixelBounds, itemColor);
            DrawRectangleBorder(item.PixelBounds, Color.White * alpha, 2);

            string name = item.Data.Name;
            Vector2 nameSize = font.MeasureString(name);
            float scale = Math.Min(0.6f, (item.PixelBounds.Width - 10) / nameSize.X);

            spriteBatch.DrawString(font, name,
                new Vector2(item.PixelBounds.X + 5, item.PixelBounds.Y + 5),
                Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            string info = item.Data.Type == ItemType.Weapon ?
                $"Dmg:{item.Data.WeaponData?.Damage}" :
                $"Arm:{item.Data.ArmorValue}";

            spriteBatch.DrawString(font, info,
                new Vector2(item.PixelBounds.X + 5, item.PixelBounds.Bottom - 20),
                Color.Yellow * alpha, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        private void DrawEquipmentSlot(Rectangle slot, string label, Item equippedItem)
        {
            spriteBatch.Draw(pixel, slot, new Color(60, 60, 60, 200));
            DrawRectangleBorder(slot, Color.Gray, 2);

            spriteBatch.DrawString(font, label,
                new Vector2(slot.X, slot.Y - 20), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

            if (equippedItem != null && draggedItem == null)
            {
                Color itemColor = equippedItem.Data.Type == ItemType.Weapon ?
                    new Color(100, 150, 255) : new Color(150, 100, 50);

                Rectangle itemRect = new Rectangle(slot.X + 5, slot.Y + 5, slot.Width - 10, slot.Height - 10);
                spriteBatch.Draw(pixel, itemRect, itemColor);

                Vector2 nameSize = font.MeasureString(equippedItem.Data.Name);
                float scale = Math.Min(1f, (itemRect.Width - 4) / nameSize.X);

                spriteBatch.DrawString(font, equippedItem.Data.Name,
                    new Vector2(itemRect.X + 2, itemRect.Y + itemRect.Height / 2 - 8),
                    Color.White, 0f, Vector2.Zero, scale * 0.5f, SpriteEffects.None, 0f);
            }
            else if (equippedItem == null)
            {
                spriteBatch.DrawString(font, "Vide",
                    new Vector2(slot.Center.X - 20, slot.Center.Y - 8),
                    Color.Gray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            }
        }

        private void DrawRectangleBorder(Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CALCUL DES BOUNDS DES SLOTS
        // ═══════════════════════════════════════════════════════════════════════

        private Rectangle GetWeaponSlotBounds()
        {
            int panelX = graphicsDevice.Viewport.Width / 2 - 300;
            int panelY = graphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 450, panelY + 100, 80, 80);
        }

        private Rectangle GetHelmetSlotBounds()
        {
            int panelX = graphicsDevice.Viewport.Width / 2 - 300;
            int panelY = graphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 450, panelY + 200, 80, 80);
        }

        private Rectangle GetArmorSlotBounds()
        {
            int panelX = graphicsDevice.Viewport.Width / 2 - 300;
            int panelY = graphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 450, panelY + 300, 80, 80);
        }

        private Rectangle GetShieldSlotBounds()
        {
            int panelX = graphicsDevice.Viewport.Width / 2 - 300;
            int panelY = graphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 550, panelY + 100, 80, 80);
        }

        private Rectangle GetShirtSlotBounds()
        {
            int panelX = graphicsDevice.Viewport.Width / 2 - 300;
            int panelY = graphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 550, panelY + 200, 80, 80);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ACCESSEURS
        // ═══════════════════════════════════════════════════════════════════════

        public bool IsDragging => draggedItem != null;
    }
}
