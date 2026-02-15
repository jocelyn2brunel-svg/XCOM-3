using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XCOM_3.Scripts;

namespace XCOM_3
{
    /// <summary>
    /// Système d'inventaire complet avec interface utilisateur style Diablo
    /// VERSION CORRIGÉE - Grenades fonctionnelles
    /// </summary>
    public class InventorySystem
    {
        private const float Mk2WeightLbs = 1.3228f; // 600 grammes

        // ═══════════════════════════════════════════════════════════════════════
        // CONSTANTES
        // ═══════════════════════════════════════════════════════════════════════

        private const int GRID_WIDTH = 10;
        private const int GRID_HEIGHT = 10;
        private const int CELL_SIZE = 40;
        private const int EQUIP_PANEL_WIDTH = 420;
        private const int EQUIP_SLOT_LEFT_PADDING = 120;
        private const int EQUIP_LABEL_ROW_HEIGHT = CELL_SIZE;
        private const int EQUIP_SLOT_VERTICAL_SPACING = 0;
        private const int UTILITY_SLOT_GAP = 0;
        private const int BACKPACK_UTILITY_COLUMNS = 4;
        private const int PANEL_GAP = 0;
        private const int SECTION_HEADER_HEIGHT = 36;
        private const int SECTION_PADDING = 12;
        private const int CONTEXT_WINDOW_WIDTH = 280;
        private const int CONTEXT_WINDOW_HEIGHT = 190;
        private const int LOOT_ENTRY_HEIGHT = 22;

        // ═══════════════════════════════════════════════════════════════════════
        // ÉTAT
        // ═══════════════════════════════════════════════════════════════════════

        private InventoryGrid inventoryGrid;
        private GridItem draggedItem = null;
        private Point dragGridOffset;
        private Point dragPixelOffset;
        private readonly List<ItemData> nearbyLootItems = new List<ItemData>();
        private readonly Random random = new Random();
        public Dictionary<string, ItemData> ItemDatabase { get; private set; }

        // Ressources graphiques (injectées)
        private SpriteBatch spriteBatch;
        private SpriteFont font;
        private Texture2D pixel;
        private GraphicsDevice graphicsDevice;
        private Texture2D flashlightTexture;

        // État des touches
        private KeyboardState previousKeyboardState;

        // Dans la section ÉTAT de InventorySystem.cs
        private GridItem hoveredItem = null; //
        private float totalElapsedTime = 0f; // Pour l'effet de pulsation
                                             // Dans la section ÉTAT de InventorySystem.cs
        private Point? previewPos = null;

        private const double DoubleClickThresholdSeconds = 0.35;
        private double lastClickTimeSeconds = -10;
        private string lastClickItemKey = string.Empty;

        private bool showContextMenu = false;
        private Rectangle contextMenuRect;
        private ItemContextInfo contextMenuItem;
        private Rectangle contextEquipButtonRect;
        private Rectangle contextExamineButtonRect;
        private Rectangle contextCloseButtonRect;

        private bool showExaminePopup = false;
        private Rectangle examinePopupRect;
        private ItemData examinedItemData;
        private readonly List<Rectangle> nearbyLootRowRects = new List<Rectangle>();

        private struct ItemContextInfo
        {
            public ItemData Data;
            public string Source;
            public int Index;
            public Point GridPosition;

            public string GetKey()
            {
                return $"{Source}:{Index}:{GridPosition.X}:{GridPosition.Y}:{Data?.Name}";
            }
        }


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

            flashlightTexture = LoadOptionalTexture("Flashlight32x32.jpg");

            InitializeItemDatabase();
            InitializeInventoryItems();
        }

        private Texture2D LoadOptionalTexture(string fileName)
        {
            try
            {
                string texturePath = Path.Combine(AppContext.BaseDirectory, fileName);
                if (!File.Exists(texturePath))
                    texturePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);

                if (!File.Exists(texturePath))
                    return null;

                using var stream = File.OpenRead(texturePath);
                return Texture2D.FromStream(graphicsDevice, stream);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsTacticalFlashlight(ItemData data)
        {
            return string.Equals(data?.Name, "Lampe tactique aluminium", StringComparison.OrdinalIgnoreCase);
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

            // ✅ GRENADES
            var grenadeDB = GrenadeDatabase.GetAllGrenades();

            ItemDatabase["MK 2"] = new ItemData("MK 2", grenadeDB["MK 2"], Mk2WeightLbs, "Grenade MK2 (1x1) - 600g");
            ItemDatabase["Lampe tactique aluminium"] = new ItemData(
                "Lampe tactique aluminium",
                ItemType.Accessory,
                armorValue: 0,
                armorSlot: ArmorSlot.None,
                protectionLevel: ProtectionLevel.None,
                mobilityPenalty: 0,
                weightLbs: 0.6614f,
                bonusInventorySlots: 0,
                description: "Lampe tactique aluminium (1x1) - 300g");

            // Armures (charger depuis ArmorDatabase)
            foreach (var armor in ArmorDatabase.GetAllArmors())
            {
                ItemDatabase[armor.Name] = armor;
            }
        }

        private void InitializeInventoryItems()
        {
            inventoryGrid.Clear();

            // Placement automatique pour éviter les conflits
            var itemsToAdd = new List<string>
            {
                "Plasma Rifle",
                "Shotgun",
                "SMG",
                "PASGT Helmet",
                "ACH",
                "ECH",
                "PASGT Vest",
                "OTV (IBA)",
                "MTV",
                "OTV + SAPI",
                "Army Combat Shirt",
                "T-Shirt Noir",
                "Jeans Léger",
                "Pantalon de Travail",
                // ✅ Grenades
                "MK 2",
                "Lampe tactique aluminium"
            };

            string[] backpackOptions = { "Backpack Small", "Backpack Medium", "Backpack XL" };
            int backpacksToSpawn = random.Next(1, 3);
            var selectedBackpacks = new HashSet<string>();
            while (selectedBackpacks.Count < backpacksToSpawn)
            {
                selectedBackpacks.Add(backpackOptions[random.Next(backpackOptions.Length)]);
            }

            itemsToAdd.AddRange(selectedBackpacks);

            foreach (var itemName in itemsToAdd)
            {
                if (ItemDatabase.ContainsKey(itemName))
                {
                    ItemSize size = ItemSizeDatabase.GetItemSize(itemName);
                    Point? freePos = inventoryGrid.FindFreePosition(size, true);

                    if (freePos.HasValue)
                    {
                        GridItem gridItem = new GridItem(ItemDatabase[itemName], freePos.Value, size, false);
                        inventoryGrid.PlaceItem(gridItem);
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════════════

        public void Update(MouseState mouse, MouseState previousMouse, bool leftClick, KeyboardState keyboard, Unit selectedUnit)
        {
            if (selectedUnit == null) return;

            bool rightClick = mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;

            // Accumuler le temps pour l'effet Sinus du pulse
            totalElapsedTime += 0.016f; // Environ 60 FPS, ou utilise gameTime.ElapsedGameTime

            int gridStartX = GetGridStartX();
            int gridStartY = GetGridStartY();

            // Détection de l'item survolé dans la grille
            int gridX = (mouse.X - gridStartX) / CELL_SIZE;
            int gridY = (mouse.Y - gridStartY) / CELL_SIZE;

            hoveredItem = null;
            if (gridX >= 0 && gridX < GRID_WIDTH && gridY >= 0 && gridY < GRID_HEIGHT)
            {
                hoveredItem = inventoryGrid.GetItemAt(new Point(gridX, gridY)); //
            }

            HandleContextMenus(mouse, leftClick, rightClick, selectedUnit, gridStartX, gridStartY);
            if (showContextMenu || showExaminePopup)
            {
                previousKeyboardState = keyboard;
                return;
            }

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
                if (TryPickupNearbyLoot(mouse.Position))
                {
                    previousKeyboardState = keyboard;
                    return;
                }

                if (!HandleDoubleClick(mouse, selectedUnit, gridStartX, gridStartY))
                    HandleStartDrag(mouse, selectedUnit, gridStartX, gridStartY);
            }

            // Drag en cours
            // Dans InventorySystem.Update(...)
            if (draggedItem != null && mouse.LeftButton == ButtonState.Pressed)
            {
                HandleDragUpdate(mouse, gridStartX, gridStartY);

                // ✅ La formule doit être identique à celle de HandleEndDrag
                // On soustrait l'offset pour trouver où le coin (0,0) de l'item se situerait
                int targetX = (mouse.X - gridStartX) / CELL_SIZE - dragGridOffset.X;
                int targetY = (mouse.Y - gridStartY) / CELL_SIZE - dragGridOffset.Y;

                Point potentialPos = new Point(targetX, targetY);

                // On vérifie la validité avec la grille
                if (inventoryGrid.CanPlaceItem(potentialPos, draggedItem.GetCurrentSize()))
                {
                    previewPos = potentialPos;
                }
                else
                {
                    previewPos = null;
                }
            }
            else if (draggedItem == null)
            {
                previewPos = null; // Reset quand on ne drag plus
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
                    clickedItem.UpdatePixelBounds(gridStartX, gridStartY);
                    draggedItem = clickedItem;
                    dragGridOffset = new Point(gridX - clickedItem.GridPosition.X,
                                              gridY - clickedItem.GridPosition.Y);
                    dragPixelOffset = new Point(
                        mouse.X - clickedItem.PixelBounds.X,
                        mouse.Y - clickedItem.PixelBounds.Y);
                    inventoryGrid.RemoveItem(draggedItem);
                    Console.WriteLine($"[INVENTORY] Drag from grid: {draggedItem.Data.Name}");
                    return;
                }
            }

            // ✅ VÉRIFIER ET DÉSÉQUIPER LES SLOTS
            // ✅ VÉRIFIER ET DÉSÉQUIPER LES SLOTS
            Rectangle weaponSlot = GetWeaponSlotBounds();
            if (unit.EquippedWeapon != null && weaponSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedWeapon, mouse, weaponSlot);
                unit.EquippedWeapon = null;
                unit.Weapon = string.Empty;
                unit.WeaponData = null;
                Console.WriteLine($"[INVENTORY] Unequipped weapon: {draggedItem.Data.Name}");
                return;
            }

            Rectangle helmetSlot = GetHelmetSlotBounds();
            if (unit.EquippedHelmet != null && helmetSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedHelmet, mouse, helmetSlot);
                unit.EquippedHelmet = null;
                Console.WriteLine($"[INVENTORY] Unequipped helmet: {draggedItem.Data.Name}");
                return;
            }

            Rectangle armorSlot = GetArmorSlotBounds();
            if (unit.EquippedArmor != null && armorSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedArmor, mouse, armorSlot);
                unit.EquippedArmor = null;
                Console.WriteLine($"[INVENTORY] Unequipped armor: {draggedItem.Data.Name}");
                return;
            }

            Rectangle shieldSlot = GetShieldSlotBounds();
            if (unit.EquippedShield != null && shieldSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedShield, mouse, shieldSlot);
                unit.EquippedShield = null;
                Console.WriteLine($"[INVENTORY] Unequipped shield: {draggedItem.Data.Name}");
                return;
            }

            Rectangle beltSlot = GetBeltSlotBounds();
            if (unit.EquippedAccessory != null && beltSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedAccessory, mouse, beltSlot);
                unit.EquippedAccessory = null;
                Console.WriteLine($"[INVENTORY] Unequipped accessory: {draggedItem.Data.Name}");
                return;
            }

            if (unit.EquippedBelt != null && beltSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedBelt, mouse, beltSlot);
                unit.EquippedBelt = null;
                Console.WriteLine($"[INVENTORY] Unequipped belt: {draggedItem.Data.Name}");
                return;
            }

            Rectangle shirtSlot = GetShirtSlotBounds();
            if (unit.EquippedShirt != null && shirtSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedShirt, mouse, shirtSlot);
                unit.EquippedShirt = null;
                Console.WriteLine($"[INVENTORY] Unequipped shirt: {draggedItem.Data.Name}");
                return;
            }

            Rectangle pantsSlot = GetPantsSlotBounds();
            if (unit.EquippedPants != null && pantsSlot.Contains(mouse.Position))
            {
                foreach (var pocketItem in unit.PantsInventory)
                {
                    if (pocketItem != null)
                        ReturnItemToGrid(pocketItem);
                }

                StartDragFromEquipment(unit.EquippedPants, mouse, pantsSlot);
                unit.EquippedPants = null;
                unit.PantsInventory.Clear();
                unit.RefreshGrenadeInventoryFromEquipment();
                Console.WriteLine($"[INVENTORY] Unequipped pants: {draggedItem.Data.Name}");
                return;
            }

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            for (int i = 0; i < pantsCapacity; i++)
            {
                Rectangle pocketSlot = GetPantsPocketSlotByIndex(i);
                if (i < unit.PantsInventory.Count && unit.PantsInventory[i] != null && pocketSlot.Contains(mouse.Position))
                {
                    StartDragFromEquipment(unit.PantsInventory[i], mouse, pocketSlot);
                    unit.PantsInventory.RemoveAt(i);
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] Unequipped pants pocket item from slot {i + 1}: {draggedItem.Data.Name}");
                    return;
                }
            }
            int chestRigCapacity = unit.GetChestRigInventoryCapacity();
            for (int i = 0; i < chestRigCapacity; i++)
            {
                Rectangle chestRigSlot = GetChestRigPocketSlotByIndex(i, unit);
                if (i < unit.ChestRigInventory.Count && unit.ChestRigInventory[i] != null && chestRigSlot.Contains(mouse.Position))
                {
                    StartDragFromEquipment(unit.ChestRigInventory[i], mouse, chestRigSlot);
                    unit.ChestRigInventory.RemoveAt(i);
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] Unequipped chest rig item from slot {i + 1}: {draggedItem.Data.Name}");
                    return;
                }
            }

            Rectangle backpackMainSlot = GetBackpackSlotBounds();
            if (!string.IsNullOrWhiteSpace(unit.EquippedBackpack) && backpackMainSlot.Contains(mouse.Position))
            {
                if (ItemDatabase.TryGetValue(unit.EquippedBackpack, out ItemData equippedBackpackData))
                {
                    StartDragFromEquipment(new Item(equippedBackpackData, Point.Zero), mouse, backpackMainSlot);
                    unit.EquippedBackpack = null;
                    unit.EnsureBackpackInventoryGrid();
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] Drag from backpack slot: {equippedBackpackData.Name}");
                    return;
                }
            }

            unit.EnsureBackpackInventoryGrid();
            foreach (GridItem backpackItem in unit.BackpackInventory.GetAllItems())
            {
                Rectangle backpackItemBounds = GetBackpackItemBounds(backpackItem, unit);
                if (backpackItemBounds.Contains(mouse.Position))
                {
                    StartDragFromEquipment(new Item(backpackItem.Data, Point.Zero), mouse, backpackItemBounds);
                    unit.BackpackInventory.RemoveItem(backpackItem);
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] Unequipped backpack utility item: {draggedItem.Data.Name}");
                    return;
                }
            }

            Rectangle chestRigMainSlot = GetChestRigSlotBounds();
            if (unit.EquippedChestRig != null && chestRigMainSlot.Contains(mouse.Position))
            {
                foreach (var rigItem in unit.ChestRigInventory)
                {
                    if (rigItem != null)
                        ReturnItemToGrid(rigItem);
                }

                StartDragFromEquipment(unit.EquippedChestRig, mouse, chestRigMainSlot);
                unit.EquippedChestRig = null;
                unit.ChestRigInventory.Clear();
                unit.RefreshGrenadeInventoryFromEquipment();
                Console.WriteLine($"[INVENTORY] Unequipped chest rig: {draggedItem.Data.Name}");
                return;
            }
        }

        private void StartDragFromEquipment(Item equippedItem, MouseState mouse, Rectangle sourceSlot)
        {
            ItemSize size = ItemSizeDatabase.GetItemSize(equippedItem.Data.Name);
            draggedItem = new GridItem(equippedItem.Data, new Point(0, 0), size, false);

            int maxWidth = size.Width * CELL_SIZE - 1;
            int maxHeight = size.Height * CELL_SIZE - 1;
            int offsetX = Math.Clamp(mouse.X - sourceSlot.X, 0, maxWidth);
            int offsetY = Math.Clamp(mouse.Y - sourceSlot.Y, 0, maxHeight);

            dragPixelOffset = new Point(offsetX, offsetY);
            dragGridOffset = new Point(offsetX / CELL_SIZE, offsetY / CELL_SIZE);
        }

        private void HandleDragUpdate(MouseState mouse, int gridStartX, int gridStartY)
        {
            // ✅ Drag complètement libre - suit exactement la souris
            draggedItem.PixelBounds = new Rectangle(
                mouse.X - dragPixelOffset.X,
                mouse.Y - dragPixelOffset.Y,
                draggedItem.GetCurrentSize().Width * CELL_SIZE,
                draggedItem.GetCurrentSize().Height * CELL_SIZE
            );
        }

        private void HandleEndDrag(MouseState mouse, Unit unit, int gridStartX, int gridStartY)
        {
            Rectangle inventoryWindow = GetInventoryPanelBounds();
            Rectangle lootWindow = GetLootPanelBounds();
            Rectangle equipmentWindow = GetEquipmentPanelBounds(unit);
            bool droppedOutsideInterface =
                !inventoryWindow.Contains(mouse.Position) &&
                !lootWindow.Contains(mouse.Position) &&
                !equipmentWindow.Contains(mouse.Position);

            if (droppedOutsideInterface)
            {
                nearbyLootItems.Add(draggedItem.Data);
                Console.WriteLine($"[INVENTORY] Dropped outside interface, sent to nearby loot: {draggedItem.Data.Name}");
                draggedItem = null;
                return;
            }

            // ✅ VÉRIFIER D'ABORD L'ÉQUIPEMENT (priorité absolue)
            bool equipped = TryEquipInSlot(mouse.Position, draggedItem, unit);

            if (!equipped)
            {
                // ✅ Calculer la position grille à partir de la souris
                int gridX = (mouse.X - gridStartX) / CELL_SIZE - dragGridOffset.X;
                int gridY = (mouse.Y - gridStartY) / CELL_SIZE - dragGridOffset.Y;
                draggedItem.GridPosition = new Point(gridX, gridY);

                // Vérifier si dans la zone de grille
                int gridPixelWidth = GRID_WIDTH * CELL_SIZE;
                int gridPixelHeight = GRID_HEIGHT * CELL_SIZE;
                Rectangle gridArea = new Rectangle(gridStartX, gridStartY, gridPixelWidth, gridPixelHeight);

                if (gridArea.Contains(mouse.Position))
                {
                    // Essayer de placer à la position calculée
                    if (inventoryGrid.CanPlaceItem(draggedItem.GridPosition, draggedItem.GetCurrentSize()))
                    {
                        draggedItem.UpdatePixelBounds(gridStartX, gridStartY);
                        inventoryGrid.PlaceItem(draggedItem);
                        Console.WriteLine($"[INVENTORY] Placed at grid {draggedItem.GridPosition}: {draggedItem.Data.Name}");
                    }
                    else
                    {
                        // Position occupée, trouver un emplacement libre
                        Point? freePos = inventoryGrid.FindFreePosition(draggedItem.GetCurrentSize(), true);
                        if (freePos.HasValue)
                        {
                            draggedItem.GridPosition = freePos.Value;
                            draggedItem.UpdatePixelBounds(gridStartX, gridStartY);
                            inventoryGrid.PlaceItem(draggedItem);
                            Console.WriteLine($"[INVENTORY] Auto-placed at {freePos.Value}: {draggedItem.Data.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"[INVENTORY] WARNING: No space! Item lost: {draggedItem.Data.Name}");
                        }
                    }
                }
                else
                {
                    // Hors grille, replacer automatiquement
                    Point? freePos = inventoryGrid.FindFreePosition(draggedItem.GetCurrentSize(), true);
                    if (freePos.HasValue)
                    {
                        draggedItem.GridPosition = freePos.Value;
                        draggedItem.UpdatePixelBounds(gridStartX, gridStartY);
                        inventoryGrid.PlaceItem(draggedItem);
                        Console.WriteLine($"[INVENTORY] Dropped outside, auto-placed at {freePos.Value}: {draggedItem.Data.Name}");
                    }
                    else
                    {
                        Console.WriteLine($"[INVENTORY] WARNING: No space! Item lost: {draggedItem.Data.Name}");
                    }
                }
            }

            draggedItem = null;
        }

        private bool TryPickupNearbyLoot(Point mousePosition)
        {
            if (nearbyLootItems.Count == 0)
                return false;

            for (int i = 0; i < nearbyLootRowRects.Count; i++)
            {
                if (!nearbyLootRowRects[i].Contains(mousePosition))
                    continue;

                if (i >= nearbyLootItems.Count)
                    return false;

                ItemData lootData = nearbyLootItems[i];
                ItemSize lootSize = ItemSizeDatabase.GetItemSize(lootData.Name);
                Point? freePos = inventoryGrid.FindFreePosition(lootSize, true);
                if (!freePos.HasValue)
                {
                    Console.WriteLine($"[INVENTORY] Cannot pickup nearby loot (inventory full): {lootData.Name}");
                    return true;
                }

                inventoryGrid.PlaceItem(new GridItem(lootData, freePos.Value, lootSize, false));
                nearbyLootItems.RemoveAt(i);
                Console.WriteLine($"[INVENTORY] Picked nearby loot: {lootData.Name}");
                return true;
            }

            return false;
        }

        private bool TryEquipInSlot(Point mousePosition, GridItem item, Unit unit)
        {
            Console.WriteLine($"[INVENTORY] TryEquipInSlot: {item.Data.Name} (Type: {item.Data.Type}) at mouse {mousePosition}");
            Console.WriteLine($"[INVENTORY] Viewport: {graphicsDevice.Viewport.Width}x{graphicsDevice.Viewport.Height}");

            // ✅ Les grenades ne vont QUE dans des slots utilitaires (poches / chest rig)
            if (item.Data.Type == ItemType.Grenade)
            {
                ItemSize draggedSize = item.GetCurrentSize();
                bool isPocketSized = draggedSize.Width == 1 && draggedSize.Height == 1;
                if (!isPocketSized)
                    return false;
            }

            Rectangle weaponSlot = GetWeaponSlotBounds();
            if (item.Data.Type == ItemType.Weapon && weaponSlot.Contains(mousePosition))
            {
                if (unit.EquippedWeapon != null)
                    ReturnItemToGrid(unit.EquippedWeapon);

                unit.EquippedWeapon = new Item(item.Data, Point.Zero);
                unit.Weapon = item.Data.Name;
                unit.WeaponData = item.Data.WeaponData;
                Console.WriteLine($"[INVENTORY] ✅ Equipped weapon: {item.Data.Name}");
                return true;
            }

            if (item.Data.Type == ItemType.Accessory && GetBeltSlotBounds().Contains(mousePosition))
            {
                if (unit.EquippedAccessory != null)
                    ReturnItemToGrid(unit.EquippedAccessory);
                if (unit.EquippedBelt != null)
                    ReturnItemToGrid(unit.EquippedBelt);

                unit.EquippedAccessory = new Item(item.Data, Point.Zero);
                unit.EquippedBelt = null;
                Console.WriteLine($"[INVENTORY] ✅ Equipped accessory on belt: {item.Data.Name}");
                return true;
            }

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            int chestRigCapacity = unit.GetChestRigInventoryCapacity();
            int backpackCapacity = unit.GetBackpackInventoryCapacity();
            if (pantsCapacity > 0 || chestRigCapacity > 0 || backpackCapacity > 0)
            {
                ItemSize draggedSize = item.GetCurrentSize();
                bool isPocketSized = draggedSize.Width == 1 && draggedSize.Height == 1;

                for (int i = 0; i < pantsCapacity; i++)
                {
                    Rectangle pocketSlot = GetPantsPocketSlotByIndex(i);
                    if (pocketSlot.Contains(mousePosition))
                    {
                        if (!isPocketSized)
                            return false;

                        var newPocketItem = new Item(item.Data, Point.Zero);
                        if (i < unit.PantsInventory.Count)
                        {
                            ReturnItemToGrid(unit.PantsInventory[i]);
                            unit.PantsInventory[i] = newPocketItem;
                        }
                        else
                        {
                            unit.PantsInventory.Add(newPocketItem);
                        }

                        unit.RefreshGrenadeInventoryFromEquipment();
                        Console.WriteLine($"[INVENTORY] ✅ Equipped pants pocket slot {i + 1}: {item.Data.Name}");
                        return true;
                    }
                }

                for (int i = 0; i < chestRigCapacity; i++)
                {
                    Rectangle rigSlot = GetChestRigPocketSlotByIndex(i, unit);
                    if (rigSlot.Contains(mousePosition))
                    {
                        if (!isPocketSized)
                            return false;

                        var newPocketItem = new Item(item.Data, Point.Zero);
                        if (i < unit.ChestRigInventory.Count)
                        {
                            ReturnItemToGrid(unit.ChestRigInventory[i]);
                            unit.ChestRigInventory[i] = newPocketItem;
                        }
                        else
                        {
                            unit.ChestRigInventory.Add(newPocketItem);
                        }

                        unit.RefreshGrenadeInventoryFromEquipment();
                        Console.WriteLine($"[INVENTORY] ✅ Equipped chest rig slot {i + 1}: {item.Data.Name}");
                        return true;
                    }
                }

                unit.EnsureBackpackInventoryGrid();
                Rectangle backpackGridBounds = GetBackpackUtilityGridBounds(unit);
                if (backpackGridBounds.Contains(mousePosition))
                {
                    Point backpackGridPos = GetBackpackDropGridPositionFromMouse(mousePosition, unit, dragGridOffset);
                    if (!unit.BackpackInventory.CanPlaceItem(backpackGridPos, draggedSize))
                        return false;

                    GridItem backpackGridItem = new GridItem(item.Data, backpackGridPos, item.Size, item.IsRotated);
                    unit.BackpackInventory.PlaceItem(backpackGridItem);

                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] ✅ Equipped backpack utility item at {backpackGridPos}: {item.Data.Name}");
                    return true;
                }
            }

            // ✅ ÉQUIPER UNE ARMURE
            if (item.Data.Type == ItemType.Armor)
            {
                Rectangle helmetSlot = GetHelmetSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Head && helmetSlot.Contains(mousePosition))
                {
                    if (unit.EquippedHelmet != null)
                        ReturnItemToGrid(unit.EquippedHelmet);
                    unit.EquippedHelmet = new Item(item.Data, Point.Zero);
                    Console.WriteLine($"[INVENTORY] ✅ Equipped helmet: {item.Data.Name}");
                    return true;
                }

                Rectangle armorSlot = GetArmorSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Torso && armorSlot.Contains(mousePosition))
                {
                    if (unit.EquippedArmor != null)
                        ReturnItemToGrid(unit.EquippedArmor);
                    unit.EquippedArmor = new Item(item.Data, Point.Zero);
                    Console.WriteLine($"[INVENTORY] ✅ Equipped armor: {item.Data.Name}");
                    return true;
                }

                Rectangle shieldSlot = GetShieldSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Shield && shieldSlot.Contains(mousePosition))
                {
                    if (unit.EquippedShield != null)
                        ReturnItemToGrid(unit.EquippedShield);
                    unit.EquippedShield = new Item(item.Data, Point.Zero);
                    Console.WriteLine($"[INVENTORY] ✅ Equipped shield: {item.Data.Name}");
                    return true;
                }

                Rectangle shirtSlot = GetShirtSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Shirt && shirtSlot.Contains(mousePosition))
                {
                    if (unit.EquippedShirt != null)
                        ReturnItemToGrid(unit.EquippedShirt);
                    unit.EquippedShirt = new Item(item.Data, Point.Zero);
                    Console.WriteLine($"[INVENTORY] ✅ Equipped shirt: {item.Data.Name}");
                    return true;
                }

                Rectangle pantsSlot = GetPantsSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Pants && pantsSlot.Contains(mousePosition))
                {
                    if (unit.EquippedPants != null)
                    {
                        foreach (var pocketItem in unit.PantsInventory)
                        {
                            if (pocketItem != null)
                                ReturnItemToGrid(pocketItem);
                        }

                        ReturnItemToGrid(unit.EquippedPants);
                    }

                    unit.EquippedPants = new Item(item.Data, Point.Zero);
                    unit.PantsInventory = new List<Item>();
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] ✅ Equipped pants: {item.Data.Name}");
                    return true;
                }

                Rectangle chestRigSlot = GetChestRigSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.ChestRig && chestRigSlot.Contains(mousePosition))
                {
                    if (unit.EquippedChestRig != null)
                    {
                        foreach (var rigItem in unit.ChestRigInventory)
                        {
                            if (rigItem != null)
                                ReturnItemToGrid(rigItem);
                        }

                        ReturnItemToGrid(unit.EquippedChestRig);
                    }

                    unit.EquippedChestRig = new Item(item.Data, Point.Zero);
                    unit.ChestRigInventory = new List<Item>();
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] ✅ Equipped chest rig: {item.Data.Name}");
                    return true;
                }

                Rectangle beltSlot = GetBeltSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Belt && beltSlot.Contains(mousePosition))
                {
                    if (unit.EquippedAccessory != null)
                        ReturnItemToGrid(unit.EquippedAccessory);
                    if (unit.EquippedBelt != null)
                        ReturnItemToGrid(unit.EquippedBelt);

                    unit.EquippedAccessory = null;
                    unit.EquippedBelt = new Item(item.Data, Point.Zero);
                    Console.WriteLine($"[INVENTORY] ✅ Equipped belt: {item.Data.Name}");
                    return true;
                }

                Rectangle backpackSlot = GetBackpackSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Backpack && backpackSlot.Contains(mousePosition))
                {
                    if (!string.IsNullOrWhiteSpace(unit.EquippedBackpack) &&
                        ItemDatabase.TryGetValue(unit.EquippedBackpack, out ItemData previousBackpackData))
                    {
                        ReturnItemToGrid(new Item(previousBackpackData, Point.Zero));
                    }

                    unit.EquippedBackpack = item.Data.Name;
                    unit.EnsureBackpackInventoryGrid();
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] ✅ Equipped backpack: {item.Data.Name}");
                    return true;
                }
            }

            if (item.Data.Type == ItemType.Accessory)
            {
                Rectangle beltSlot = GetBeltSlotBounds();
                if (beltSlot.Contains(mousePosition))
                {
                    if (unit.EquippedAccessory != null)
                        ReturnItemToGrid(unit.EquippedAccessory);
                    if (unit.EquippedBelt != null)
                        ReturnItemToGrid(unit.EquippedBelt);

                    unit.EquippedAccessory = new Item(item.Data, Point.Zero);
                    unit.EquippedBelt = null;
                    Console.WriteLine($"[INVENTORY] ✅ Equipped accessory on belt: {item.Data.Name}");
                    return true;
                }
            }

            Console.WriteLine($"[INVENTORY] ❌ Not equipped (no matching slot)");
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
                Console.WriteLine($"[INVENTORY] Returned old item to grid: {item.Data.Name}");
            }
        }

        private bool HandleDoubleClick(MouseState mouse, Unit unit, int gridStartX, int gridStartY)
        {
            var clickedItem = GetItemUnderMouse(mouse.Position, unit, gridStartX, gridStartY);
            if (!clickedItem.HasValue)
                return false;

            double now = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            string key = clickedItem.Value.GetKey();
            bool isDoubleClick = key == lastClickItemKey && (now - lastClickTimeSeconds) <= DoubleClickThresholdSeconds;

            lastClickItemKey = key;
            lastClickTimeSeconds = now;

            if (!isDoubleClick)
                return false;

            return TryEquipByContext(clickedItem.Value, unit);
        }

        private void HandleContextMenus(MouseState mouse, bool leftClick, bool rightClick, Unit unit, int gridStartX, int gridStartY)
        {
            bool openedExaminePopupThisClick = false;

            if (rightClick)
            {
                var clickedItem = GetItemUnderMouse(mouse.Position, unit, gridStartX, gridStartY);
                if (clickedItem.HasValue)
                {
                    contextMenuItem = clickedItem.Value;
                    contextMenuRect = BuildContextWindow(mouse.Position);
                    contextEquipButtonRect = new Rectangle(contextMenuRect.X + 12, contextMenuRect.Bottom - 76, contextMenuRect.Width - 24, 22);
                    contextExamineButtonRect = new Rectangle(contextMenuRect.X + 12, contextMenuRect.Bottom - 50, contextMenuRect.Width - 24, 22);
                    contextCloseButtonRect = new Rectangle(contextMenuRect.X + 12, contextMenuRect.Bottom - 24, contextMenuRect.Width - 24, 16);
                    showContextMenu = true;
                    showExaminePopup = false;
                }
                else
                {
                    showContextMenu = false;
                }
            }

            if (leftClick && showContextMenu)
            {
                if (contextEquipButtonRect.Contains(mouse.Position))
                {
                    TryEquipByContext(contextMenuItem, unit);
                    showContextMenu = false;
                }
                else if (contextExamineButtonRect.Contains(mouse.Position))
                {
                    examinedItemData = contextMenuItem.Data;
                    int width = 360;
                    int height = 260;
                    examinePopupRect = new Rectangle(
                        graphicsDevice.Viewport.Width / 2 - width / 2,
                        graphicsDevice.Viewport.Height / 2 - height / 2,
                        width,
                        height);
                    showExaminePopup = true;
                    openedExaminePopupThisClick = true;
                    showContextMenu = false;
                }
                else if (contextCloseButtonRect.Contains(mouse.Position))
                {
                    showContextMenu = false;
                }
                else if (!contextMenuRect.Contains(mouse.Position))
                {
                    showContextMenu = false;
                }
            }

            if (leftClick && showExaminePopup && !openedExaminePopupThisClick && !examinePopupRect.Contains(mouse.Position))
            {
                showExaminePopup = false;
            }
        }

        private Rectangle BuildContextWindow(Point clickPoint)
        {
            int maxX = Math.Max(8, graphicsDevice.Viewport.Width - CONTEXT_WINDOW_WIDTH - 8);
            int maxY = Math.Max(8, graphicsDevice.Viewport.Height - CONTEXT_WINDOW_HEIGHT - 8);

            int x = Math.Min(Math.Max(8, clickPoint.X + 16), maxX);
            int y = Math.Min(Math.Max(8, clickPoint.Y + 12), maxY);

            return new Rectangle(x, y, CONTEXT_WINDOW_WIDTH, CONTEXT_WINDOW_HEIGHT);
        }

        private bool TryEquipByContext(ItemContextInfo info, Unit unit)
        {
            if (info.Data == null)
                return false;

            if (IsAlreadyEquippedInCompatibleSlot(info))
                return true;

            Point target;
            if (!TryGetAutoEquipTarget(info.Data, unit, out target))
                return false;

            RemoveItemFromSource(info, unit);

            var quickItem = new GridItem(info.Data, Point.Zero, ItemSizeDatabase.GetItemSize(info.Data.Name), false);
            bool equipped = TryEquipInSlot(target, quickItem, unit);
            if (!equipped)
            {
                RestoreItemToSource(info, unit);
            }

            return equipped;
        }

        private bool IsAlreadyEquippedInCompatibleSlot(ItemContextInfo info)
        {
            if (info.Data.Type == ItemType.Weapon && info.Source == "weapon")
                return true;

            if (info.Data.Type == ItemType.Armor)
            {
                if (info.Data.ArmorSlot == ArmorSlot.Head && info.Source == "helmet") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Torso && info.Source == "armor") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Shield && info.Source == "shield") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Shirt && info.Source == "shirt") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Pants && info.Source == "pants") return true;
                if (info.Data.ArmorSlot == ArmorSlot.ChestRig && info.Source == "chestrig") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Belt && info.Source == "belt") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Backpack && info.Source == "backpack") return true;
            }

            if (info.Data.Type == ItemType.Accessory && info.Source == "accessory")
                return true;

            bool isPocket = ItemSizeDatabase.IsPocketSized(info.Data.Name);
            if (isPocket && (info.Source == "pantspocket" || info.Source == "rigpocket" || info.Source == "backpackutility"))
                return true;

            return false;
        }

        private bool TryGetAutoEquipTarget(ItemData data, Unit unit, out Point target)
        {
            if (data.Type == ItemType.Weapon)
            {
                target = GetWeaponSlotBounds().Center;
                return true;
            }

            if (data.Type == ItemType.Armor)
            {
                switch (data.ArmorSlot)
                {
                    case ArmorSlot.Head: target = GetHelmetSlotBounds().Center; return true;
                    case ArmorSlot.Torso: target = GetArmorSlotBounds().Center; return true;
                    case ArmorSlot.Shield: target = GetShieldSlotBounds().Center; return true;
                    case ArmorSlot.Shirt: target = GetShirtSlotBounds().Center; return true;
                    case ArmorSlot.Pants: target = GetPantsSlotBounds().Center; return true;
                    case ArmorSlot.ChestRig: target = GetChestRigSlotBounds().Center; return true;
                    case ArmorSlot.Belt: target = GetBeltSlotBounds().Center; return true;
                    case ArmorSlot.Backpack: target = GetBackpackSlotBounds().Center; return true;
                }
            }

            if (data.Type == ItemType.Accessory)
            {
                target = GetBeltSlotBounds().Center;
                return true;
            }

            bool isPocketSized = ItemSizeDatabase.IsPocketSized(data.Name);
            if (isPocketSized)
            {
                for (int i = 0; i < unit.GetPantsInventoryCapacity(); i++)
                {
                    if (i >= unit.PantsInventory.Count || unit.PantsInventory[i] == null)
                    {
                        target = GetPantsPocketSlotByIndex(i).Center;
                        return true;
                    }
                }

                for (int i = 0; i < unit.GetChestRigInventoryCapacity(); i++)
                {
                    if (i >= unit.ChestRigInventory.Count || unit.ChestRigInventory[i] == null)
                    {
                        target = GetChestRigPocketSlotByIndex(i, unit).Center;
                        return true;
                    }
                }
            }

            unit.EnsureBackpackInventoryGrid();
            ItemSize itemSize = ItemSizeDatabase.GetItemSize(data.Name);
            Point? freeBackpackPos = unit.BackpackInventory.FindFreePosition(itemSize, true);
            if (freeBackpackPos.HasValue)
            {
                target = GetBackpackGridCellBounds(freeBackpackPos.Value, unit).Center;
                return true;
            }

            target = Point.Zero;
            return false;
        }

        private ItemContextInfo? GetItemUnderMouse(Point mousePos, Unit unit, int gridStartX, int gridStartY)
        {
            int gridX = (mousePos.X - gridStartX) / CELL_SIZE;
            int gridY = (mousePos.Y - gridStartY) / CELL_SIZE;
            if (gridX >= 0 && gridX < GRID_WIDTH && gridY >= 0 && gridY < GRID_HEIGHT)
            {
                var gridItem = inventoryGrid.GetItemAt(new Point(gridX, gridY));
                if (gridItem != null)
                {
                    return new ItemContextInfo { Data = gridItem.Data, Source = "grid", GridPosition = gridItem.GridPosition, Index = -1 };
                }
            }

            if (unit.EquippedWeapon != null && GetWeaponSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedWeapon.Data, Source = "weapon", Index = -1 };
            if (unit.EquippedHelmet != null && GetHelmetSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedHelmet.Data, Source = "helmet", Index = -1 };
            if (unit.EquippedArmor != null && GetArmorSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedArmor.Data, Source = "armor", Index = -1 };
            if (unit.EquippedShield != null && GetShieldSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedShield.Data, Source = "shield", Index = -1 };
            if (unit.EquippedAccessory != null && GetBeltSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedAccessory.Data, Source = "accessory", Index = -1 };
            if (unit.EquippedShirt != null && GetShirtSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedShirt.Data, Source = "shirt", Index = -1 };
            if (unit.EquippedPants != null && GetPantsSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedPants.Data, Source = "pants", Index = -1 };
            if (unit.EquippedChestRig != null && GetChestRigSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedChestRig.Data, Source = "chestrig", Index = -1 };
            if (unit.EquippedBelt != null && GetBeltSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedBelt.Data, Source = "belt", Index = -1 };
            if (!string.IsNullOrWhiteSpace(unit.EquippedBackpack) && GetBackpackSlotBounds().Contains(mousePos) && ItemDatabase.TryGetValue(unit.EquippedBackpack, out ItemData backpackData))
                return new ItemContextInfo { Data = backpackData, Source = "backpack", Index = -1 };

            for (int i = 0; i < unit.GetPantsInventoryCapacity(); i++)
            {
                if (i < unit.PantsInventory.Count && unit.PantsInventory[i] != null && GetPantsPocketSlotByIndex(i).Contains(mousePos))
                    return new ItemContextInfo { Data = unit.PantsInventory[i].Data, Source = "pantspocket", Index = i };
            }

            for (int i = 0; i < unit.GetChestRigInventoryCapacity(); i++)
            {
                if (i < unit.ChestRigInventory.Count && unit.ChestRigInventory[i] != null && GetChestRigPocketSlotByIndex(i, unit).Contains(mousePos))
                    return new ItemContextInfo { Data = unit.ChestRigInventory[i].Data, Source = "rigpocket", Index = i };
            }

            unit.EnsureBackpackInventoryGrid();
            foreach (GridItem backpackItem in unit.BackpackInventory.GetAllItems())
            {
                if (GetBackpackItemBounds(backpackItem, unit).Contains(mousePos))
                {
                    return new ItemContextInfo
                    {
                        Data = backpackItem.Data,
                        Source = "backpackutility",
                        GridPosition = backpackItem.GridPosition,
                        Index = -1
                    };
                }
            }

            return null;
        }

        private void RemoveItemFromSource(ItemContextInfo info, Unit unit)
        {
            switch (info.Source)
            {
                case "grid":
                    var inGrid = inventoryGrid.GetItemAt(info.GridPosition);
                    if (inGrid != null) inventoryGrid.RemoveItem(inGrid);
                    break;
                case "weapon": unit.EquippedWeapon = null; unit.Weapon = string.Empty; unit.WeaponData = null; break;
                case "helmet": unit.EquippedHelmet = null; break;
                case "armor": unit.EquippedArmor = null; break;
                case "shield": unit.EquippedShield = null; break;
                case "accessory": unit.EquippedAccessory = null; break;
                case "shirt": unit.EquippedShirt = null; break;
                case "pants": unit.EquippedPants = null; break;
                case "chestrig": unit.EquippedChestRig = null; break;
                case "belt": unit.EquippedBelt = null; break;
                case "backpack": unit.EquippedBackpack = null; unit.EnsureBackpackInventoryGrid(); break;
                case "pantspocket": if (info.Index >= 0 && info.Index < unit.PantsInventory.Count) unit.PantsInventory.RemoveAt(info.Index); break;
                case "rigpocket": if (info.Index >= 0 && info.Index < unit.ChestRigInventory.Count) unit.ChestRigInventory.RemoveAt(info.Index); break;
                case "backpackutility":
                    unit.EnsureBackpackInventoryGrid();
                    var backpackItem = unit.BackpackInventory.GetItemAt(info.GridPosition);
                    if (backpackItem != null)
                        unit.BackpackInventory.RemoveItem(backpackItem);
                    break;
            }
            unit.RefreshGrenadeInventoryFromEquipment();
        }

        private void RestoreItemToSource(ItemContextInfo info, Unit unit)
        {
            var restored = new Item(info.Data, Point.Zero);
            switch (info.Source)
            {
                case "grid":
                    var size = ItemSizeDatabase.GetItemSize(info.Data.Name);
                    if (inventoryGrid.CanPlaceItem(info.GridPosition, size))
                        inventoryGrid.PlaceItem(new GridItem(info.Data, info.GridPosition, size, false));
                    else
                        ReturnItemToGrid(restored);
                    break;
                case "weapon": unit.EquippedWeapon = restored; unit.Weapon = info.Data.Name; unit.WeaponData = info.Data.WeaponData; break;
                case "helmet": unit.EquippedHelmet = restored; break;
                case "armor": unit.EquippedArmor = restored; break;
                case "shield": unit.EquippedShield = restored; break;
                case "accessory": unit.EquippedAccessory = restored; break;
                case "shirt": unit.EquippedShirt = restored; break;
                case "pants": unit.EquippedPants = restored; break;
                case "chestrig": unit.EquippedChestRig = restored; break;
                case "belt": unit.EquippedBelt = restored; break;
                case "backpack": unit.EquippedBackpack = info.Data.Name; unit.EnsureBackpackInventoryGrid(); break;
                case "pantspocket":
                    if (info.Index >= 0 && info.Index <= unit.PantsInventory.Count) unit.PantsInventory.Insert(info.Index, restored);
                    break;
                case "rigpocket":
                    if (info.Index >= 0 && info.Index <= unit.ChestRigInventory.Count) unit.ChestRigInventory.Insert(info.Index, restored);
                    break;
                case "backpackutility":
                    unit.EnsureBackpackInventoryGrid();
                    var restoredBackpackItem = new GridItem(info.Data, info.GridPosition, ItemSizeDatabase.GetItemSize(info.Data.Name), false);
                    if (unit.BackpackInventory.CanPlaceItem(restoredBackpackItem.GridPosition, restoredBackpackItem.GetCurrentSize()))
                        unit.BackpackInventory.PlaceItem(restoredBackpackItem);
                    else
                        ReturnItemToGrid(restored);
                    break;
            }
            unit.RefreshGrenadeInventoryFromEquipment();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RENDU
        // ═══════════════════════════════════════════════════════════════════════

        public void Draw(Unit selectedUnit)
        {
            if (selectedUnit == null) return;

            // ✅ AJOUT : Récupérer l'état de la souris pour l'utiliser dans le dessin
            MouseState mouse = Mouse.GetState();

            Rectangle equipmentWindow = GetEquipmentPanelBounds(selectedUnit);
            Rectangle inventoryWindow = GetInventoryPanelBounds();
            Rectangle lootWindow = GetLootPanelBounds();

            DrawWindow(equipmentWindow, "EQUIPEMENT");
            DrawWindow(inventoryWindow, $"INVENTAIRE - {selectedUnit.Name.ToUpper()}");
            DrawWindow(lootWindow, "LOOT A PROXIMITE");

            int gridStartX = GetGridStartX();
            int gridStartY = GetGridStartY();
            DrawInventoryGrid(gridStartX, gridStartY);

            int equipX = GetEquipX();
            int equipY = GetEquipY();
            DrawEquipmentSlots(equipX, equipY, selectedUnit);
            DrawNearbyLootPanel(lootWindow);

            // ✅ DESSIN DE L'EFFET DE SÉLECTION
            // Si on survole un item et qu'on n'est pas en train d'en déplacer un
            if (hoveredItem != null && draggedItem == null)
            {
                // On s'assure que les PixelBounds sont à jour pour l'item survolé
                hoveredItem.UpdatePixelBounds(gridStartX, gridStartY);

                // Appel de la méthode de ton thème
                ParasiteEveTheme.DrawSelectionIndicator(
                    spriteBatch,
                    pixel,
                    hoveredItem.PixelBounds,
                    totalElapsedTime
                );
            }

            // ✅ DESSIN DU FANTÔME DE PRÉVISUALISATION (AMÉLIORÉ)
            if (draggedItem != null)
            {
                Rectangle gridArea = new Rectangle(
                    gridStartX,
                    gridStartY,
                    GRID_WIDTH * CELL_SIZE,
                    GRID_HEIGHT * CELL_SIZE);

                // N'afficher la prévisualisation de placement que dans la zone de grille.
                // Sur le panneau d'équipement, elle n'est pas pertinente et semble décalée.
                if (gridArea.Contains(mouse.Position))
                {
                    // 1. Calcul de la position théorique dans la grille (identique à HandleEndDrag)
                    int ghostGridX = (mouse.X - gridStartX) / CELL_SIZE - dragGridOffset.X;
                    int ghostGridY = (mouse.Y - gridStartY) / CELL_SIZE - dragGridOffset.Y;
                    Point ghostPos = new Point(ghostGridX, ghostGridY);

                    // 2. Définition du rectangle visuel
                    Rectangle gridPreviewRect = new Rectangle(
                        gridStartX + ghostGridX * CELL_SIZE,
                        gridStartY + ghostGridY * CELL_SIZE,
                        draggedItem.GetCurrentSize().Width * CELL_SIZE,
                        draggedItem.GetCurrentSize().Height * CELL_SIZE
                    );

                    // 3. Vérification de la validité via InventoryGrid
                    // On passe 'draggedItem' à CanPlaceItem pour qu'il ne se bloque pas lui-même
                    bool canPlace = inventoryGrid.CanPlaceItem(ghostPos, draggedItem.GetCurrentSize(), draggedItem);

                    // 4. Choix des couleurs selon le thème PE2
                    // Fond : Vert holographique (HoverOverlay) ou Rouge (TextDanger)
                    Color ghostColor = canPlace ?
                        ParasiteEveTheme.HoverOverlay * 0.6f :
                        ParasiteEveTheme.TextDanger * 0.4f;

                    // Bordure : Plus intense pour la visibilité
                    Color borderColor = canPlace ?
                        ParasiteEveTheme.SelectionOutline * 0.5f :
                        ParasiteEveTheme.TextDanger * 0.8f;

                    // 5. Rendu
                    spriteBatch.Draw(pixel, gridPreviewRect, ghostColor);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, gridPreviewRect, borderColor, 1);
                }

                if (equipmentWindow.Contains(mouse.Position) &&
                    TryGetEquipmentPreviewRect(selectedUnit, mouse.Position, draggedItem, out Rectangle previewRect, out bool canEquip))
                {
                    Color ghostColor = canEquip
                        ? ParasiteEveTheme.HoverOverlay * 0.6f
                        : ParasiteEveTheme.TextDanger * 0.4f;

                    Color borderColor = canEquip
                        ? ParasiteEveTheme.SelectionOutline * 0.5f
                        : ParasiteEveTheme.TextDanger * 0.8f;

                    spriteBatch.Draw(pixel, previewRect, ghostColor);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, previewRect, borderColor, 1);
                }
            }

            // ✅ Item en cours de drag (avec transparence)
            if (draggedItem != null)
            {
                DrawGridItem(draggedItem, 0.7f);
            }

            // ✅ Texte d'aide avec ombre
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "DOUBLE CLICK: EQUIP | RIGHT CLICK: ACTIONS | R: ROTATE",
                new Vector2(inventoryWindow.X, inventoryWindow.Bottom + 8), ParasiteEveTheme.TextWarning, 0.8f);

            DrawContextMenuAndExamine();
        }

        private void DrawContextMenuAndExamine()
        {
            if (showContextMenu)
            {
                ParasiteEveTheme.DrawPanel(spriteBatch, pixel, contextMenuRect);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextMenuRect, ParasiteEveTheme.SelectionOutline, 1);

                Rectangle headerRect = new Rectangle(contextMenuRect.X, contextMenuRect.Y, contextMenuRect.Width, 28);
                ParasiteEveTheme.DrawSectionHeader(spriteBatch, pixel, font, headerRect, "INVENTAIRE CONTEXTUEL");

                if (contextMenuItem.Data != null)
                {
                    string line1 = contextMenuItem.Data.Name;
                    string line2 = $"Type: {contextMenuItem.Data.Type}";
                    string line3 = $"Poids: {contextMenuItem.Data.WeightLbs:0.##} lbs";

                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line1,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 40), ParasiteEveTheme.TextHighlight, 0.68f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line2,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 62), ParasiteEveTheme.TextNormal, 0.62f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line3,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 82), ParasiteEveTheme.TextDim, 0.58f);
                }

                spriteBatch.Draw(pixel, contextEquipButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                spriteBatch.Draw(pixel, contextExamineButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextEquipButtonRect, ParasiteEveTheme.BorderColor, 1);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextExamineButtonRect, ParasiteEveTheme.BorderColor, 1);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EQUIPER", new Vector2(contextEquipButtonRect.X + 8, contextEquipButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EXAMINER", new Vector2(contextExamineButtonRect.X + 8, contextExamineButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "Fermer", new Vector2(contextCloseButtonRect.X, contextCloseButtonRect.Y - 2), ParasiteEveTheme.TextWarning, 0.58f);
            }

            if (showExaminePopup && examinedItemData != null)
            {
                ParasiteEveTheme.DrawPanel(spriteBatch, pixel, examinePopupRect);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, examinePopupRect, ParasiteEveTheme.SelectionOutline, 1);

                ParasiteEveTheme.DrawSectionHeader(spriteBatch, pixel, font, new Rectangle(examinePopupRect.X, examinePopupRect.Y, examinePopupRect.Width, 32), $"EXAMINE - {examinedItemData.Name.ToUpper()}");

                Rectangle imageRect = new Rectangle(examinePopupRect.X + 16, examinePopupRect.Y + 48, 96, 96);
                spriteBatch.Draw(pixel, imageRect, ParasiteEveTheme.BackgroundMedium);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, imageRect, ParasiteEveTheme.BorderColor, 1);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "IMAGE", new Vector2(imageRect.X + 24, imageRect.Y + 40), ParasiteEveTheme.TextDim, 0.6f);

                float textY = examinePopupRect.Y + 52;
                float textX = imageRect.Right + 16;
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Type: {examinedItemData.Type}", new Vector2(textX, textY), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Poids: {examinedItemData.WeightLbs:0.##} lbs", new Vector2(textX, textY + 24), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Slots bonus: {examinedItemData.BonusInventorySlots}", new Vector2(textX, textY + 48), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Mobilite: -{examinedItemData.MobilityPenalty}", new Vector2(textX, textY + 72), ParasiteEveTheme.TextNormal, 0.7f);

                if (!string.IsNullOrWhiteSpace(examinedItemData.Description))
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, examinedItemData.Description, new Vector2(examinePopupRect.X + 16, examinePopupRect.Y + 164), ParasiteEveTheme.TextDim, 0.6f);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "Click outside to close", new Vector2(examinePopupRect.X + 16, examinePopupRect.Bottom - 26), ParasiteEveTheme.TextWarning, 0.6f);
            }
        }

        private bool TryGetEquipmentPreviewRect(Unit unit, Point mousePosition, GridItem item, out Rectangle previewRect, out bool canEquip)
        {
            previewRect = Rectangle.Empty;
            canEquip = false;

            if (unit == null || item?.Data == null)
                return false;

            ItemSize draggedSize = item.GetCurrentSize();
            bool isPocketSized = draggedSize.Width == 1 && draggedSize.Height == 1;

            if (item.Data.Type == ItemType.Weapon && GetWeaponSlotBounds().Contains(mousePosition))
            {
                previewRect = GetWeaponSlotBounds();
                canEquip = true;
                return true;
            }

            if (item.Data.Type == ItemType.Armor)
            {
                if (item.Data.ArmorSlot == ArmorSlot.Head && GetHelmetSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetHelmetSlotBounds();
                    canEquip = true;
                    return true;
                }

                if (item.Data.ArmorSlot == ArmorSlot.Torso && GetArmorSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetArmorSlotBounds();
                    canEquip = true;
                    return true;
                }

                if (item.Data.ArmorSlot == ArmorSlot.Shield && GetShieldSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetShieldSlotBounds();
                    canEquip = true;
                    return true;
                }

                if (item.Data.ArmorSlot == ArmorSlot.Shirt && GetShirtSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetShirtSlotBounds();
                    canEquip = true;
                    return true;
                }

                if (item.Data.ArmorSlot == ArmorSlot.Pants && GetPantsSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetPantsSlotBounds();
                    canEquip = true;
                    return true;
                }

                if (item.Data.ArmorSlot == ArmorSlot.ChestRig && GetChestRigSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetChestRigSlotBounds();
                    canEquip = true;
                    return true;
                }

                if (item.Data.ArmorSlot == ArmorSlot.Belt && GetBeltSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetBeltSlotBounds();
                    canEquip = true;
                    return true;
                }
            }

            if (item.Data.Type == ItemType.Accessory && GetBeltSlotBounds().Contains(mousePosition))
            {
                previewRect = GetBeltSlotBounds();
                canEquip = true;
                return true;
            }

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            for (int i = 0; i < pantsCapacity; i++)
            {
                Rectangle pocketSlot = GetPantsPocketSlotByIndex(i);
                if (pocketSlot.Contains(mousePosition))
                {
                    previewRect = pocketSlot;
                    canEquip = isPocketSized;
                    return true;
                }
            }

            int chestRigCapacity = unit.GetChestRigInventoryCapacity();
            for (int i = 0; i < chestRigCapacity; i++)
            {
                Rectangle rigSlot = GetChestRigPocketSlotByIndex(i, unit);
                if (rigSlot.Contains(mousePosition))
                {
                    previewRect = rigSlot;
                    canEquip = isPocketSized;
                    return true;
                }
            }

            Rectangle backpackGridBounds = GetBackpackUtilityGridBounds(unit);
            if (backpackGridBounds.Contains(mousePosition))
            {
                Point backpackGridPos = GetBackpackDropGridPositionFromMouse(mousePosition, unit, dragGridOffset);
                Rectangle topLeftCell = GetBackpackGridCellBounds(backpackGridPos, unit);
                previewRect = new Rectangle(
                    topLeftCell.X,
                    topLeftCell.Y,
                    draggedSize.Width * CELL_SIZE,
                    draggedSize.Height * CELL_SIZE);

                unit.EnsureBackpackInventoryGrid();
                canEquip = unit.BackpackInventory.CanPlaceItem(backpackGridPos, draggedSize);
                return true;
            }

            return false;
        }

        private void DrawInventoryGrid(int gridStartX, int gridStartY)
        {
            int gridPixelWidth = GRID_WIDTH * CELL_SIZE;
            int gridPixelHeight = GRID_HEIGHT * CELL_SIZE;
            Rectangle gridArea = new Rectangle(gridStartX, gridStartY, gridPixelWidth, gridPixelHeight);
            Rectangle inventoryContentBounds = GetInventoryContentBounds();

            DrawInventoryGridBackdrop(inventoryContentBounds, gridArea);

            // Fond de grille medium
            spriteBatch.Draw(pixel, gridArea, ParasiteEveTheme.BackgroundMedium * 0.5f);

            // ✅ Lignes de grille en vert sombre (TextDim)
            for (int x = 0; x <= GRID_WIDTH; x++)
                spriteBatch.Draw(pixel, new Rectangle(gridStartX + x * CELL_SIZE, gridStartY, 1, gridPixelHeight), ParasiteEveTheme.TextDim * 0.2f);
            for (int y = 0; y <= GRID_HEIGHT; y++)
                spriteBatch.Draw(pixel, new Rectangle(gridStartX, gridStartY + y * CELL_SIZE, gridPixelWidth, 1), ParasiteEveTheme.TextDim * 0.2f);

            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, gridArea, ParasiteEveTheme.BorderColor, 1);

            foreach (var item in inventoryGrid.GetAllItems())
            {
                if (item != draggedItem)
                {
                    item.UpdatePixelBounds(gridStartX, gridStartY);
                    DrawGridItem(item);
                }
            }
        }

        private void DrawInventoryGridBackdrop(Rectangle contentBounds, Rectangle gridArea)
        {
            Point alignedGridOrigin = new Point(
                contentBounds.X + PositiveModulo(gridArea.X - contentBounds.X, CELL_SIZE),
                contentBounds.Y + PositiveModulo(gridArea.Y - contentBounds.Y, CELL_SIZE));

            spriteBatch.Draw(pixel, contentBounds, ParasiteEveTheme.BackgroundMedium * 0.28f);

            for (int x = alignedGridOrigin.X; x <= contentBounds.Right; x += CELL_SIZE)
                spriteBatch.Draw(pixel, new Rectangle(x, contentBounds.Y, 1, contentBounds.Height), ParasiteEveTheme.TextDim * 0.12f);

            for (int y = alignedGridOrigin.Y; y <= contentBounds.Bottom; y += CELL_SIZE)
                spriteBatch.Draw(pixel, new Rectangle(contentBounds.X, y, contentBounds.Width, 1), ParasiteEveTheme.TextDim * 0.12f);
        }

        private void DrawEquipmentSlots(int equipX, int equipY, Unit unit)
        {
            bool isDragging = draggedItem != null;
            DrawEquipmentGridBackdrop(unit);

            // Slots d'équipement principaux (empilés verticalement)
            DrawEquipmentSlot(GetWeaponSlotBounds(), "RIGHT HAND", unit.EquippedWeapon,
                isDragging && draggedItem.Data.Type == ItemType.Weapon,
                labelOnLeft: true);

            DrawEquipmentSlot(GetShieldSlotBounds(), "LEFT HAND", unit.EquippedShield,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Shield,
                labelOnLeft: true);

            DrawEquipmentSlot(GetHelmetSlotBounds(), "HEAD", unit.EquippedHelmet,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Head,
                labelOnLeft: true);

            DrawEquipmentSlot(GetShirtSlotBounds(), "SUIT", unit.EquippedShirt,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Shirt,
                labelOnLeft: true);

            DrawEquipmentSlot(GetPantsSlotBounds(), "PANTS", unit.EquippedPants,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Pants,
                labelOnLeft: true);

            DrawEquipmentSlot(GetArmorSlotBounds(), "VEST", unit.EquippedArmor,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Torso,
                labelOnLeft: true);

            DrawEquipmentSlot(GetChestRigSlotBounds(), "CHEST RIG", unit.EquippedChestRig,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.ChestRig,
                labelOnLeft: true);

            Item beltItem = unit.EquippedAccessory ?? unit.EquippedBelt;
            DrawEquipmentSlot(GetBeltSlotBounds(), "BELT", beltItem,
                isDragging &&
                (draggedItem.Data.Type == ItemType.Accessory ||
                (draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Belt)),
                labelOnLeft: true);

            Rectangle backpackSlot = GetBackpackSlotBounds();
            Item backpackItem = null;
            if (!string.IsNullOrWhiteSpace(unit.EquippedBackpack) && ItemDatabase.TryGetValue(unit.EquippedBackpack, out ItemData backpackData))
                backpackItem = new Item(backpackData, Point.Zero);

            DrawEquipmentSlot(backpackSlot, "BACKPACK", backpackItem,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Backpack, labelOnLeft: true);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                string.IsNullOrWhiteSpace(unit.EquippedBackpack) ? "None" : unit.EquippedBackpack,
                new Vector2(backpackSlot.X + 4, backpackSlot.Y + backpackSlot.Height / 2 - 5),
                ParasiteEveTheme.TextNormal, 0.5f);

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            int chestRigCapacity = unit.GetChestRigInventoryCapacity();
            bool highlightPocket = isDragging && draggedItem.GetCurrentSize().Width == 1 && draggedItem.GetCurrentSize().Height == 1;
            for (int i = 0; i < pantsCapacity; i++)
            {
                Rectangle pocketSlot = GetPantsPocketSlotByIndex(i);
                Item pocketItem = i < unit.PantsInventory.Count ? unit.PantsInventory[i] : null;
                DrawEquipmentSlot(pocketSlot, $"PP{i + 1}", pocketItem, highlightPocket);
            }

            for (int i = 0; i < chestRigCapacity; i++)
            {
                Rectangle rigSlot = GetChestRigPocketSlotByIndex(i, unit);
                Item rigItem = i < unit.ChestRigInventory.Count ? unit.ChestRigInventory[i] : null;
                DrawEquipmentSlot(rigSlot, $"CR{i + 1}", rigItem, highlightPocket);
            }

            DrawBackpackUtilityGrid(unit);
        }

        private void DrawBackpackUtilityGrid(Unit unit)
        {
            unit.EnsureBackpackInventoryGrid();
            int backpackCapacity = unit.GetBackpackInventoryCapacity();
            // Pas de ghost vert sur les emplacements utilitaires du sac à dos.
            bool highlightBackpack = false;

            for (int i = 0; i < backpackCapacity; i++)
            {
                Rectangle utilitySlot = GetBackpackUtilitySlotByIndex(i);
                DrawEquipmentSlot(utilitySlot, string.Empty, null, highlightBackpack);
            }

            foreach (GridItem backpackItem in unit.BackpackInventory.GetAllItems())
            {
                GridItem drawItem = new GridItem(backpackItem.Data, backpackItem.GridPosition, backpackItem.Size, backpackItem.IsRotated);
                drawItem.UpdatePixelBoundsAbsolute(
                    GetBackpackGridCellBounds(backpackItem.GridPosition, unit).X,
                    GetBackpackGridCellBounds(backpackItem.GridPosition, unit).Y);
                DrawGridItem(drawItem);
            }
        }

        private void DrawEquipmentGridBackdrop(Unit unit)
        {
            Rectangle panelBounds = GetEquipmentPanelBounds(unit);
            Rectangle gridArea = GetEquipmentContentBounds(panelBounds);
            Point alignedGridOrigin = GetEquipmentGridAlignedOrigin(gridArea);

            spriteBatch.Draw(pixel, gridArea, ParasiteEveTheme.BackgroundMedium * 0.28f);

            for (int x = alignedGridOrigin.X; x <= gridArea.Right; x += CELL_SIZE)
                spriteBatch.Draw(pixel, new Rectangle(x, gridArea.Y, 1, gridArea.Height), ParasiteEveTheme.TextDim * 0.15f);

            for (int y = alignedGridOrigin.Y; y <= gridArea.Bottom; y += CELL_SIZE)
                spriteBatch.Draw(pixel, new Rectangle(gridArea.X, y, gridArea.Width, 1), ParasiteEveTheme.TextDim * 0.15f);
        }

        private Rectangle GetEquipmentContentBounds(Rectangle panelBounds)
        {
            return new Rectangle(
                panelBounds.X + SECTION_PADDING,
                panelBounds.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                panelBounds.Width - SECTION_PADDING * 2,
                panelBounds.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);
        }

        private Point GetEquipmentGridAlignedOrigin(Rectangle gridArea)
        {
            int inventoryGridStartX = GetGridStartX();
            int inventoryGridStartY = GetGridStartY();

            return new Point(
                gridArea.X + PositiveModulo(inventoryGridStartX - gridArea.X, CELL_SIZE),
                gridArea.Y + PositiveModulo(inventoryGridStartY - gridArea.Y, CELL_SIZE));
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int remainder = value % modulo;
            return remainder < 0 ? remainder + modulo : remainder;
        }

        private void DrawNearbyLootPanel(Rectangle lootWindow)
        {
            Rectangle content = new Rectangle(
                lootWindow.X + SECTION_PADDING,
                lootWindow.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                lootWindow.Width - SECTION_PADDING * 2,
                lootWindow.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);

            nearbyLootRowRects.Clear();

            spriteBatch.Draw(pixel, content, ParasiteEveTheme.BackgroundDark * 0.35f);
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, content, ParasiteEveTheme.BorderColor, 1);

            if (nearbyLootItems.Count == 0)
            {
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    "Aucun loot detecte a portee.",
                    new Vector2(content.X + 8, content.Y + 10),
                    ParasiteEveTheme.TextDim,
                    0.65f);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    "Approchez-vous d'un conteneur\nou d'un ennemi neutralise\npour afficher les objets ici.",
                    new Vector2(content.X + 8, content.Y + 42),
                    ParasiteEveTheme.TextNormal,
                    0.6f);
                return;
            }

            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                $"Objets au sol: {nearbyLootItems.Count}",
                new Vector2(content.X + 8, content.Y + 10),
                ParasiteEveTheme.TextHighlight,
                0.65f);

            int maxVisible = Math.Min(nearbyLootItems.Count, 10);
            for (int i = 0; i < maxVisible; i++)
            {
                Rectangle lootRow = new Rectangle(
                    content.X + 6,
                    content.Y + 34 + i * LOOT_ENTRY_HEIGHT,
                    Math.Max(0, content.Width - 12),
                    LOOT_ENTRY_HEIGHT - 2);
                nearbyLootRowRects.Add(lootRow);

                bool canPickup = inventoryGrid.FindFreePosition(ItemSizeDatabase.GetItemSize(nearbyLootItems[i].Name), true).HasValue;
                Color rowColor = canPickup ? ParasiteEveTheme.ButtonNormal * 0.28f : ParasiteEveTheme.TextDanger * 0.2f;
                spriteBatch.Draw(pixel, lootRow, rowColor);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    $"- {nearbyLootItems[i].Name}",
                    new Vector2(lootRow.X + 4, lootRow.Y + 3),
                    ParasiteEveTheme.TextNormal,
                    0.55f);
            }

            if (nearbyLootItems.Count > maxVisible)
            {
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    $"... +{nearbyLootItems.Count - maxVisible} autres",
                    new Vector2(content.X + 8, content.Y + 36 + maxVisible * 20),
                    ParasiteEveTheme.TextDim,
                    0.55f);
            }

            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                "Cliquez un objet pour le ramasser.",
                new Vector2(content.X + 8, content.Bottom - 22),
                ParasiteEveTheme.TextDim,
                0.5f);
        }

        private void DrawWindow(Rectangle bounds, string title)
        {
            ParasiteEveTheme.DrawPanel(spriteBatch, pixel, bounds);
            ParasiteEveTheme.DrawScanlines(spriteBatch, pixel, bounds, 0.08f);
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, bounds, ParasiteEveTheme.SelectionOutline, 1);

            Rectangle headerRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, SECTION_HEADER_HEIGHT);
            ParasiteEveTheme.DrawSectionHeader(spriteBatch, pixel, font, headerRect, title);
        }

        private void DrawGridItem(GridItem item, float alpha = 1f)
        {
            // Fond du bouton style PE2
            spriteBatch.Draw(pixel, item.PixelBounds, ParasiteEveTheme.ButtonNormal * alpha);

            if (IsTacticalFlashlight(item.Data) && flashlightTexture != null)
            {
                Rectangle textureRect = new Rectangle(item.PixelBounds.X + 3, item.PixelBounds.Y + 3,
                    Math.Max(1, item.PixelBounds.Width - 6), Math.Max(1, item.PixelBounds.Height - 6));
                spriteBatch.Draw(flashlightTexture, textureRect, Color.White * alpha);
            }

            // Bordure colorée selon le type (via ta DB)
            Color typeColor = ItemSizeDatabase.GetItemColor(item.Data.Type) * alpha;
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, item.PixelBounds, typeColor, 1);

            // ✅ Nom de l'item avec ombre
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, item.Data.Name,
                new Vector2(item.PixelBounds.X + 4, item.PixelBounds.Y + 4),
                ParasiteEveTheme.TextNormal * alpha, 0.6f);

            // ✅ Stats en bas
            string info = item.Data.Type == ItemType.Weapon ? $"DMG:{item.Data.WeaponData?.Damage}" : $"ARM:{item.Data.ArmorValue}";
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, info,
                new Vector2(item.PixelBounds.X + 4, item.PixelBounds.Bottom - 15),
                ParasiteEveTheme.TextHighlight * alpha, 0.4f);
        }

        private void DrawEquipmentSlot(Rectangle slot, string label, Item equippedItem, bool highlight = false, bool labelOnLeft = false)
        {
            // Fond de slot sombre (ghost vert si item compatible)
            Color slotBackground = highlight
                ? Color.Lerp(ParasiteEveTheme.BackgroundDark, Color.LimeGreen, 0.35f) * 0.9f
                : ParasiteEveTheme.BackgroundDark * 0.8f;
            spriteBatch.Draw(pixel, slot, slotBackground);

            // Bordure dynamique : brille en vert si l'item traîné est compatible
            Color borderColor = highlight ? Color.LimeGreen : ParasiteEveTheme.BorderColor;
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, slot, borderColor, highlight ? 2 : 1);

            // Label du slot
            Vector2 labelPos = labelOnLeft
                ? new Vector2(slot.X - 105, slot.Y + slot.Height / 2 - 8)
                : new Vector2(slot.X, slot.Y - EQUIP_LABEL_ROW_HEIGHT + 10);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, label, labelPos, ParasiteEveTheme.TextDim, 0.6f);

            if (equippedItem != null && draggedItem == null)
            {
                // Les objets équipés restent visiblement en vert.
                Rectangle inner = new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4);
                spriteBatch.Draw(pixel, inner, Color.Lerp(ParasiteEveTheme.ButtonNormal, Color.LimeGreen, 0.5f) * 0.75f);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, inner, Color.LimeGreen * 0.9f, 1);

                if (IsTacticalFlashlight(equippedItem.Data) && flashlightTexture != null)
                {
                    Rectangle textureRect = new Rectangle(inner.X + 3, inner.Y + 3,
                        Math.Max(1, inner.Width - 6), Math.Max(1, inner.Height - 6));
                    spriteBatch.Draw(flashlightTexture, textureRect, Color.White);
                }

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, equippedItem.Data.Name,
                    new Vector2(inner.X + 4, inner.Y + inner.Height / 2 - 5), ParasiteEveTheme.TextNormal, 0.5f);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CALCUL DES BOUNDS DES SLOTS - Centralisé
        // ═══════════════════════════════════════════════════════════════════════

        private int GetMainWindowHeight()
        {
            return Math.Min(graphicsDevice.Viewport.Height - 140, 560);
        }

        private int GetMainWindowY()
        {
            return graphicsDevice.Viewport.Height / 2 - GetMainWindowHeight() / 2;
        }

        private Rectangle GetInventoryPanelBounds()
        {
            int width = GRID_WIDTH * CELL_SIZE + SECTION_PADDING * 2;
            int x = graphicsDevice.Viewport.Width / 2 - width / 2;
            return new Rectangle(x, GetMainWindowY(), width, GetMainWindowHeight());
        }

        private Rectangle GetInventoryContentBounds()
        {
            Rectangle inventoryBounds = GetInventoryPanelBounds();
            return new Rectangle(
                inventoryBounds.X + SECTION_PADDING,
                inventoryBounds.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                inventoryBounds.Width - SECTION_PADDING * 2,
                inventoryBounds.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);
        }

        private Rectangle GetEquipmentPanelBounds(Unit unit)
        {
            int width = EQUIP_PANEL_WIDTH;
            Rectangle inventoryBounds = GetInventoryPanelBounds();
            int x = inventoryBounds.X - PANEL_GAP - width;
            int minHeight = unit == null
                ? GetMainWindowHeight()
                : GetEquipmentPanelHeight(unit) + SECTION_HEADER_HEIGHT + SECTION_PADDING;
            int height = Math.Max(GetMainWindowHeight(), minHeight);

            return new Rectangle(x, GetMainWindowY(), width, height);
        }

        private Rectangle GetLootPanelBounds()
        {
            Rectangle inventoryBounds = GetInventoryPanelBounds();
            int availableWidth = graphicsDevice.Viewport.Width - inventoryBounds.Right - PANEL_GAP - 20;
            int width = Math.Max(260, availableWidth);
            return new Rectangle(inventoryBounds.Right + PANEL_GAP, GetMainWindowY(), width, GetMainWindowHeight());
        }

        private int GetGridStartX()
        {
            Rectangle inventoryBounds = GetInventoryPanelBounds();
            return inventoryBounds.X + (inventoryBounds.Width - GRID_WIDTH * CELL_SIZE) / 2;
        }

        private int GetGridStartY()
        {
            return GetInventoryPanelBounds().Y + SECTION_HEADER_HEIGHT + SECTION_PADDING;
        }

        private int GetEquipX()
        {
            return GetEquipmentPanelBounds(null).X + SECTION_PADDING;
        }

        private int GetEquipY()
        {
            return GetMainWindowY() + SECTION_HEADER_HEIGHT + SECTION_PADDING;
        }


        private Rectangle GetWeaponSlotBounds()
        {
            return GetMainEquipmentSlotBounds(0);
        }

        private Rectangle GetHelmetSlotBounds()
        {
            return GetMainEquipmentSlotBounds(1);
        }

        private Rectangle GetArmorSlotBounds()
        {
            return GetMainEquipmentSlotBounds(2);
        }

        private Rectangle GetShieldSlotBounds()
        {
            return GetMainEquipmentSlotBounds(3);
        }

        private Rectangle GetShirtSlotBounds()
        {
            return GetMainEquipmentSlotBounds(4);
        }

        private Rectangle GetPantsSlotBounds()
        {
            return GetMainEquipmentSlotBounds(5);
        }

        private Rectangle GetChestRigSlotBounds()
        {
            return GetMainEquipmentSlotBounds(6);
        }

        private Rectangle GetBeltSlotBounds()
        {
            return GetMainEquipmentSlotBounds(7);
        }

        private Rectangle GetBackpackSlotBounds()
        {
            return GetMainEquipmentSlotBounds(8);
        }

        private Rectangle GetMainEquipmentSlotBounds(int row)
        {
            Rectangle equipmentContent = GetEquipmentContentBounds(GetEquipmentPanelBounds(null));
            Point alignedGridOrigin = GetEquipmentGridAlignedOrigin(equipmentContent);

            return new Rectangle(
                alignedGridOrigin.X + EQUIP_SLOT_LEFT_PADDING,
                alignedGridOrigin.Y + EQUIP_LABEL_ROW_HEIGHT + row * (CELL_SIZE + EQUIP_SLOT_VERTICAL_SPACING),
                CELL_SIZE,
                CELL_SIZE
            );
        }

        private Rectangle GetPantsPocketSlotByIndex(int index)
        {
            int startX = GetPantsSlotBounds().Right;
            int startY = GetPantsSlotBounds().Y;

            return new Rectangle(
                startX + index * (CELL_SIZE + UTILITY_SLOT_GAP),
                startY,
                CELL_SIZE,
                CELL_SIZE
            );
        }

        private int GetPantsPocketBottomY(Unit unit)
        {
            int pocketsCount = unit.GetPantsInventoryCapacity();
            if (pocketsCount <= 0)
                return GetPantsSlotBounds().Bottom;

            return GetPantsPocketSlotByIndex(pocketsCount - 1).Bottom;
        }

        private Rectangle GetChestRigPocketSlotByIndex(int index, Unit unit)
        {
            int startX = GetChestRigSlotBounds().Right;
            int startY = GetChestRigSlotBounds().Y;

            return new Rectangle(
                startX + index * (CELL_SIZE + UTILITY_SLOT_GAP),
                startY,
                CELL_SIZE,
                CELL_SIZE
            );
        }

        private Rectangle GetBackpackUtilitySlotByIndex(int index)
        {
            int row = index / BACKPACK_UTILITY_COLUMNS;
            int column = index % BACKPACK_UTILITY_COLUMNS;
            Rectangle backpackSlot = GetBackpackSlotBounds();
            int startX = backpackSlot.Right;
            int startY = backpackSlot.Y;

            return new Rectangle(
                startX + column * (CELL_SIZE + UTILITY_SLOT_GAP),
                startY + row * (CELL_SIZE + UTILITY_SLOT_GAP),
                CELL_SIZE,
                CELL_SIZE
            );
        }

        private Rectangle GetBackpackUtilityGridBounds(Unit unit)
        {
            int capacity = unit.GetBackpackInventoryCapacity();
            if (capacity <= 0)
                return Rectangle.Empty;

            Rectangle firstSlot = GetBackpackUtilitySlotByIndex(0);
            int rows = (capacity + BACKPACK_UTILITY_COLUMNS - 1) / BACKPACK_UTILITY_COLUMNS;
            int width = BACKPACK_UTILITY_COLUMNS * CELL_SIZE + (BACKPACK_UTILITY_COLUMNS - 1) * UTILITY_SLOT_GAP;
            int height = rows * CELL_SIZE + (rows - 1) * UTILITY_SLOT_GAP;
            return new Rectangle(firstSlot.X, firstSlot.Y, width, height);
        }

        private Rectangle GetBackpackGridCellBounds(Point gridPosition, Unit unit)
        {
            Rectangle firstSlot = GetBackpackUtilitySlotByIndex(0);
            return new Rectangle(
                firstSlot.X + gridPosition.X * (CELL_SIZE + UTILITY_SLOT_GAP),
                firstSlot.Y + gridPosition.Y * (CELL_SIZE + UTILITY_SLOT_GAP),
                CELL_SIZE,
                CELL_SIZE);
        }

        private Point GetBackpackGridPositionFromMouse(Point mousePosition, Unit unit)
        {
            unit.EnsureBackpackInventoryGrid();
            Rectangle bounds = GetBackpackUtilityGridBounds(unit);
            int localX = Math.Max(0, mousePosition.X - bounds.X);
            int localY = Math.Max(0, mousePosition.Y - bounds.Y);

            int step = CELL_SIZE + UTILITY_SLOT_GAP;
            int gridX = Math.Clamp(localX / step, 0, unit.BackpackInventory.Width - 1);
            int gridY = Math.Clamp(localY / step, 0, unit.BackpackInventory.Height - 1);
            return new Point(gridX, gridY);
        }

        private Point GetBackpackDropGridPositionFromMouse(Point mousePosition, Unit unit, Point itemGridOffset)
        {
            unit.EnsureBackpackInventoryGrid();
            Rectangle bounds = GetBackpackUtilityGridBounds(unit);
            int localX = mousePosition.X - bounds.X;
            int localY = mousePosition.Y - bounds.Y;

            int step = CELL_SIZE + UTILITY_SLOT_GAP;
            int gridX = localX / step - itemGridOffset.X;
            int gridY = localY / step - itemGridOffset.Y;
            return new Point(gridX, gridY);
        }

        private Rectangle GetBackpackItemBounds(GridItem backpackItem, Unit unit)
        {
            Rectangle topLeftCell = GetBackpackGridCellBounds(backpackItem.GridPosition, unit);
            ItemSize size = backpackItem.GetCurrentSize();
            return new Rectangle(
                topLeftCell.X,
                topLeftCell.Y,
                size.Width * CELL_SIZE,
                size.Height * CELL_SIZE);
        }

        private int GetChestRigPocketBottomY(Unit unit)
        {
            int pocketsCount = unit.GetChestRigInventoryCapacity();
            if (pocketsCount <= 0)
                return GetChestRigSlotBounds().Bottom;

            return GetChestRigPocketSlotByIndex(pocketsCount - 1, unit).Bottom;
        }

        private int GetBackpackUtilityBottomY(Unit unit)
        {
            int utilityCount = unit.GetBackpackInventoryCapacity();
            if (utilityCount <= 0)
                return GetBackpackSlotBounds().Bottom;

            return GetBackpackUtilitySlotByIndex(utilityCount - 1).Bottom;
        }

        private int GetEquipmentPanelHeight(Unit unit)
        {
            int lastContentBottom = Math.Max(
                GetBackpackUtilityBottomY(unit),
                Math.Max(GetPantsPocketBottomY(unit), GetChestRigPocketBottomY(unit)));
            return lastContentBottom - GetEquipY() + 20;
        }

        public bool IsDragging => draggedItem != null;
    }
}
