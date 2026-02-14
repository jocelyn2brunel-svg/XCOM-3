using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
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

        private bool showExaminePopup = false;
        private Rectangle examinePopupRect;
        private ItemData examinedItemData;

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

            // ✅ GRENADES
            var grenadeDB = GrenadeDatabase.GetAllGrenades();

            ItemDatabase["Frag Grenade"] = new ItemData("Frag Grenade", grenadeDB["Frag Grenade"]);
            ItemDatabase["HE Grenade"] = new ItemData("HE Grenade", grenadeDB["HE Grenade"]);
            ItemDatabase["Plasma Grenade"] = new ItemData("Plasma Grenade", grenadeDB["Plasma Grenade"]);
            ItemDatabase["Smoke Grenade"] = new ItemData("Smoke Grenade", grenadeDB["Smoke Grenade"]);
            ItemDatabase["Flashbang"] = new ItemData("Flashbang", grenadeDB["Flashbang"]);
            ItemDatabase["Incendiary Grenade"] = new ItemData("Incendiary Grenade", grenadeDB["Incendiary Grenade"]);
            ItemDatabase["EMP Grenade"] = new ItemData("EMP Grenade", grenadeDB["EMP Grenade"]);
            ItemDatabase["Demolition Charge"] = new ItemData("Demolition Charge", grenadeDB["Demolition Charge"]);
            ItemDatabase["MK 2"] = new ItemData("MK 2", grenadeDB["MK 2"], Mk2WeightLbs, "Grenade MK2 (1x1) - 600g");

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
                "Frag Grenade",
                "Smoke Grenade",
                "HE Grenade",
                "Plasma Grenade",
                "MK 2"
            };

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

            int panelX = graphicsDevice.Viewport.Width / 2 - (int)(graphicsDevice.Viewport.Width * 0.75f) / 2;
            int panelY = graphicsDevice.Viewport.Height / 2 - (int)(graphicsDevice.Viewport.Height * 0.85f) / 2;
            int gridStartX = panelX + 20;
            int gridStartY = panelY + 60;

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
                    draggedItem = clickedItem;
                    dragGridOffset = new Point(gridX - clickedItem.GridPosition.X,
                                              gridY - clickedItem.GridPosition.Y);
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
                StartDragFromEquipment(unit.EquippedWeapon);
                unit.EquippedWeapon = null;
                unit.Weapon = string.Empty;
                unit.WeaponData = null;
                Console.WriteLine($"[INVENTORY] Unequipped weapon: {draggedItem.Data.Name}");
                return;
            }

            Rectangle helmetSlot = GetHelmetSlotBounds();
            if (unit.EquippedHelmet != null && helmetSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedHelmet);
                unit.EquippedHelmet = null;
                Console.WriteLine($"[INVENTORY] Unequipped helmet: {draggedItem.Data.Name}");
                return;
            }

            Rectangle armorSlot = GetArmorSlotBounds();
            if (unit.EquippedArmor != null && armorSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedArmor);
                unit.EquippedArmor = null;
                Console.WriteLine($"[INVENTORY] Unequipped armor: {draggedItem.Data.Name}");
                return;
            }

            Rectangle shieldSlot = GetShieldSlotBounds();
            if (unit.EquippedShield != null && shieldSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedShield);
                unit.EquippedShield = null;
                Console.WriteLine($"[INVENTORY] Unequipped shield: {draggedItem.Data.Name}");
                return;
            }

            Rectangle shirtSlot = GetShirtSlotBounds();
            if (unit.EquippedShirt != null && shirtSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedShirt);
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

                StartDragFromEquipment(unit.EquippedPants);
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
                    StartDragFromEquipment(unit.PantsInventory[i]);
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
                    StartDragFromEquipment(unit.ChestRigInventory[i]);
                    unit.ChestRigInventory.RemoveAt(i);
                    unit.RefreshGrenadeInventoryFromEquipment();
                    Console.WriteLine($"[INVENTORY] Unequipped chest rig item from slot {i + 1}: {draggedItem.Data.Name}");
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

                StartDragFromEquipment(unit.EquippedChestRig);
                unit.EquippedChestRig = null;
                unit.ChestRigInventory.Clear();
                unit.RefreshGrenadeInventoryFromEquipment();
                Console.WriteLine($"[INVENTORY] Unequipped chest rig: {draggedItem.Data.Name}");
                return;
            }
        }

        private void StartDragFromEquipment(Item equippedItem)
        {
            ItemSize size = ItemSizeDatabase.GetItemSize(equippedItem.Data.Name);
            draggedItem = new GridItem(equippedItem.Data, new Point(0, 0), size, false);
            dragGridOffset = Point.Zero;
        }

        private void HandleDragUpdate(MouseState mouse, int gridStartX, int gridStartY)
        {
            // ✅ Drag complètement libre - suit exactement la souris
            draggedItem.PixelBounds = new Rectangle(
                mouse.X - dragGridOffset.X * CELL_SIZE,
                mouse.Y - dragGridOffset.Y * CELL_SIZE,
                draggedItem.GetCurrentSize().Width * CELL_SIZE,
                draggedItem.GetCurrentSize().Height * CELL_SIZE
            );
        }

        private void HandleEndDrag(MouseState mouse, Unit unit, int gridStartX, int gridStartY)
        {
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

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            int chestRigCapacity = unit.GetChestRigInventoryCapacity();
            if (pantsCapacity > 0 || chestRigCapacity > 0)
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
            if (rightClick)
            {
                var clickedItem = GetItemUnderMouse(mouse.Position, unit, gridStartX, gridStartY);
                if (clickedItem.HasValue)
                {
                    contextMenuItem = clickedItem.Value;
                    contextMenuRect = new Rectangle(mouse.X, mouse.Y, 140, 64);
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
                Rectangle equipRect = new Rectangle(contextMenuRect.X + 4, contextMenuRect.Y + 4, contextMenuRect.Width - 8, 24);
                Rectangle examineRect = new Rectangle(contextMenuRect.X + 4, contextMenuRect.Y + 34, contextMenuRect.Width - 8, 24);

                if (equipRect.Contains(mouse.Position))
                {
                    TryEquipByContext(contextMenuItem, unit);
                    showContextMenu = false;
                }
                else if (examineRect.Contains(mouse.Position))
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
                    showContextMenu = false;
                }
                else if (!contextMenuRect.Contains(mouse.Position))
                {
                    showContextMenu = false;
                }
            }

            if (leftClick && showExaminePopup && !examinePopupRect.Contains(mouse.Position))
            {
                showExaminePopup = false;
            }
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
            }

            bool isPocket = ItemSizeDatabase.IsPocketSized(info.Data.Name);
            if (isPocket && (info.Source == "pantspocket" || info.Source == "rigpocket"))
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
                }
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
            if (unit.EquippedShirt != null && GetShirtSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedShirt.Data, Source = "shirt", Index = -1 };
            if (unit.EquippedPants != null && GetPantsSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedPants.Data, Source = "pants", Index = -1 };
            if (unit.EquippedChestRig != null && GetChestRigSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedChestRig.Data, Source = "chestrig", Index = -1 };

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
                case "shirt": unit.EquippedShirt = null; break;
                case "pants": unit.EquippedPants = null; break;
                case "chestrig": unit.EquippedChestRig = null; break;
                case "pantspocket": if (info.Index >= 0 && info.Index < unit.PantsInventory.Count) unit.PantsInventory.RemoveAt(info.Index); break;
                case "rigpocket": if (info.Index >= 0 && info.Index < unit.ChestRigInventory.Count) unit.ChestRigInventory.RemoveAt(info.Index); break;
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
                case "shirt": unit.EquippedShirt = restored; break;
                case "pants": unit.EquippedPants = restored; break;
                case "chestrig": unit.EquippedChestRig = restored; break;
                case "pantspocket":
                    if (info.Index >= 0 && info.Index <= unit.PantsInventory.Count) unit.PantsInventory.Insert(info.Index, restored);
                    break;
                case "rigpocket":
                    if (info.Index >= 0 && info.Index <= unit.ChestRigInventory.Count) unit.ChestRigInventory.Insert(info.Index, restored);
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

            int panelWidth = (int)(graphicsDevice.Viewport.Width * 0.75f);
            int panelHeight = (int)(graphicsDevice.Viewport.Height * 0.85f);
            int panelX = graphicsDevice.Viewport.Width / 2 - panelWidth / 2;
            int panelY = graphicsDevice.Viewport.Height / 2 - panelHeight / 2;
            Rectangle panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            // ✅ Fond et Scanlines style PE2
            ParasiteEveTheme.DrawPanel(spriteBatch, pixel, panel);
            ParasiteEveTheme.DrawScanlines(spriteBatch, pixel, panel, 0.08f);

            // ✅ Header avec le style spécifique
            Rectangle headerRect = new Rectangle(panelX, panelY, panelWidth, 40);
            ParasiteEveTheme.DrawSectionHeader(spriteBatch, pixel, font, headerRect, $"INVENTORY - {selectedUnit.Name.ToUpper()}");

            int gridStartX = panelX + 20;
            int gridStartY = panelY + 60;
            DrawInventoryGrid(gridStartX, gridStartY);

            int equipX = GetEquipX();
            int equipY = GetEquipY();
            DrawEquipmentSlots(equipX, equipY, selectedUnit);

            // ✅ DESSIN DE L'EFFET DE SÉLECTION
            // Si on survole un item et qu'on n'est pas en train d'en déplacer un
            if (hoveredItem != null && draggedItem == null)
            {
                // On s'assure que les PixelBounds sont à jour pour l'item survolé
                hoveredItem.UpdatePixelBounds(panelX + 20, panelY + 60);

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
                // 1. Calcul de la position théorique dans la grille (identique à HandleEndDrag)
                int ghostGridX = (mouse.X - gridStartX) / CELL_SIZE - dragGridOffset.X;
                int ghostGridY = (mouse.Y - gridStartY) / CELL_SIZE - dragGridOffset.Y;
                Point ghostPos = new Point(ghostGridX, ghostGridY);

                // 2. Définition du rectangle visuel
                Rectangle previewRect = new Rectangle(
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
                spriteBatch.Draw(pixel, previewRect, ghostColor);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, previewRect, borderColor, 1);
            }

            // ✅ Item en cours de drag (avec transparence)
            if (draggedItem != null)
            {
                DrawGridItem(draggedItem, 0.7f);
            }

            // ✅ Texte d'aide avec ombre
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "DOUBLE CLICK: EQUIP | RIGHT CLICK: ACTIONS | R: ROTATE",
                new Vector2(panelX + 20, panelY + panelHeight - 40), ParasiteEveTheme.TextWarning, 0.8f);

            DrawContextMenuAndExamine();
        }

        private void DrawContextMenuAndExamine()
        {
            if (showContextMenu)
            {
                ParasiteEveTheme.DrawPanel(spriteBatch, pixel, contextMenuRect);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextMenuRect, ParasiteEveTheme.SelectionOutline, 1);

                Rectangle equipRect = new Rectangle(contextMenuRect.X + 4, contextMenuRect.Y + 4, contextMenuRect.Width - 8, 24);
                Rectangle examineRect = new Rectangle(contextMenuRect.X + 4, contextMenuRect.Y + 34, contextMenuRect.Width - 8, 24);

                spriteBatch.Draw(pixel, equipRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                spriteBatch.Draw(pixel, examineRect, ParasiteEveTheme.ButtonNormal * 0.7f);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EQUIP", new Vector2(equipRect.X + 8, equipRect.Y + 5), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EXAMINE", new Vector2(examineRect.X + 8, examineRect.Y + 5), ParasiteEveTheme.TextNormal, 0.7f);
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

        private void DrawInventoryGrid(int gridStartX, int gridStartY)
        {
            int gridPixelWidth = GRID_WIDTH * CELL_SIZE;
            int gridPixelHeight = GRID_HEIGHT * CELL_SIZE;
            Rectangle gridArea = new Rectangle(gridStartX, gridStartY, gridPixelWidth, gridPixelHeight);

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

        private void DrawEquipmentSlots(int equipX, int equipY, Unit unit)
        {
            // Zone globale de l'équipement (hauteur dynamique selon le contenu équipé)
            Rectangle equipArea = new Rectangle(equipX, equipY, 170, GetEquipmentPanelHeight(unit));

            // Rendu style Holographique PE2
            ParasiteEveTheme.DrawPanel(spriteBatch, pixel, equipArea);
            ParasiteEveTheme.DrawScanlines(spriteBatch, pixel, equipArea, 0.05f);

            // Titre de section "TECH-EQUIP"
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EQUIPMENT",
                new Vector2(equipX + 10, equipY + 10), ParasiteEveTheme.TextHighlight, 0.75f);

            bool isDragging = draggedItem != null;

            // Slots d'équipement principaux (Organisés en 2 colonnes)
            DrawEquipmentSlot(GetWeaponSlotBounds(), "WEAPON", unit.EquippedWeapon,
                isDragging && draggedItem.Data.Type == ItemType.Weapon);

            DrawEquipmentSlot(GetShieldSlotBounds(), "OFF-HAND", unit.EquippedShield,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Shield);

            DrawEquipmentSlot(GetHelmetSlotBounds(), "HEAD", unit.EquippedHelmet,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Head);

            DrawEquipmentSlot(GetShirtSlotBounds(), "SUIT", unit.EquippedShirt,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Shirt);

            DrawEquipmentSlot(GetPantsSlotBounds(), "PANTS", unit.EquippedPants,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Pants);

            DrawEquipmentSlot(GetArmorSlotBounds(), "VEST", unit.EquippedArmor,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Torso);

            DrawEquipmentSlot(GetChestRigSlotBounds(), "CHEST RIG", unit.EquippedChestRig,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.ChestRig);

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            int chestRigCapacity = unit.GetChestRigInventoryCapacity();
            bool hasUtilitySlots = pantsCapacity > 0 || chestRigCapacity > 0;
            if (hasUtilitySlots)
            {
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "UTILITY SLOTS",
                    new Vector2(equipX + 10, GetUtilityLabelY()), ParasiteEveTheme.TextHighlight, 0.65f);
            }

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
        }

        private void DrawGridItem(GridItem item, float alpha = 1f)
        {
            // Fond du bouton style PE2
            spriteBatch.Draw(pixel, item.PixelBounds, ParasiteEveTheme.ButtonNormal * alpha);

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

        private void DrawEquipmentSlot(Rectangle slot, string label, Item equippedItem, bool highlight = false)
        {
            // Fond de slot sombre
            spriteBatch.Draw(pixel, slot, ParasiteEveTheme.BackgroundDark * 0.8f);

            // Bordure dynamique : brille si l'item traîné est compatible
            Color borderColor = highlight ? ParasiteEveTheme.SelectionOutline : ParasiteEveTheme.BorderColor;
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, slot, borderColor, highlight ? 2 : 1);

            // Label au-dessus du slot
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, label,
                new Vector2(slot.X, slot.Y - 15), ParasiteEveTheme.TextDim, 0.6f);

            if (equippedItem != null && draggedItem == null)
            {
                // Petit effet de surbrillance pour l'objet équipé
                Rectangle inner = new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4);
                spriteBatch.Draw(pixel, inner, ParasiteEveTheme.ButtonNormal * 0.5f);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, equippedItem.Data.Name,
                    new Vector2(inner.X + 4, inner.Y + inner.Height / 2 - 5), ParasiteEveTheme.TextNormal, 0.5f);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CALCUL DES BOUNDS DES SLOTS - Centralisé
        // ═══════════════════════════════════════════════════════════════════════

        private int GetPanelY()
        {
            return graphicsDevice.Viewport.Height / 2 - 275; // panelHeight / 2
        }

        private int GetEquipX()
        {
            int panelWidth = (int)(graphicsDevice.Viewport.Width * 0.75f);
            int panelX = graphicsDevice.Viewport.Width / 2 - panelWidth / 2;

            // On place le bloc d'équipement à l'extrémité droite du panel (moins sa propre largeur)
            // 170 est la largeur du bloc d'équipement définie dans DrawEquipmentSlots
            return panelX + panelWidth - 170 - 20;
        }

        private int GetEquipY()
        {
            // Aligné sur le header (40px) + une petite marge de 20px
            return GetPanelY() + 60;
        }


        private Rectangle GetWeaponSlotBounds()
        {
            int equipX = GetEquipX();
            int equipY = GetEquipY();
            return new Rectangle(equipX + 10, equipY + 40, CELL_SIZE, CELL_SIZE);
        }

        private Rectangle GetHelmetSlotBounds()
        {
            int equipX = GetEquipX();
            int equipY = GetEquipY();
            return new Rectangle(equipX + 10, equipY + 90, CELL_SIZE, CELL_SIZE);
        }

        private Rectangle GetArmorSlotBounds()
        {
            int equipX = GetEquipX();
            int equipY = GetEquipY();
            return new Rectangle(equipX + 10, equipY + 140, CELL_SIZE, CELL_SIZE);
        }

        private Rectangle GetShieldSlotBounds()
        {
            int equipX = GetEquipX();
            int equipY = GetEquipY();
            return new Rectangle(equipX + 60, equipY + 40, CELL_SIZE, CELL_SIZE);
        }

        private Rectangle GetShirtSlotBounds()
        {
            int equipX = GetEquipX();
            int equipY = GetEquipY();
            return new Rectangle(equipX + 60, equipY + 90, CELL_SIZE, CELL_SIZE);
        }

        private Rectangle GetPantsSlotBounds()
        {
            int equipX = GetEquipX();
            int equipY = GetEquipY();
            return new Rectangle(equipX + 60, equipY + 140, CELL_SIZE, CELL_SIZE);
        }

        private Rectangle GetChestRigSlotBounds()
        {
            int equipX = GetEquipX();
            int equipY = GetEquipY();
            return new Rectangle(equipX + 110, equipY + 40, CELL_SIZE, CELL_SIZE);
        }

        private int GetUtilityLabelY()
        {
            return GetPantsSlotBounds().Bottom + 10;
        }

        private Rectangle GetPantsPocketSlotByIndex(int index)
        {
            int slotSize = CELL_SIZE;
            int spacing = 6;
            int columns = 3;

            int row = index / columns;
            int col = index % columns;
            int startX = GetEquipX() + 10;
            int startY = GetUtilityLabelY() + 20;

            return new Rectangle(
                startX + col * (slotSize + spacing),
                startY + row * (slotSize + spacing),
                slotSize,
                slotSize
            );
        }

        private int GetPantsPocketBottomY(Unit unit)
        {
            int pocketsCount = unit.GetPantsInventoryCapacity();
            if (pocketsCount <= 0)
                return GetUtilityLabelY() + 20;

            return GetPantsPocketSlotByIndex(pocketsCount - 1).Bottom;
        }

        private Rectangle GetChestRigPocketSlotByIndex(int index, Unit unit)
        {
            int slotSize = CELL_SIZE;
            int spacing = 6;
            int columns = 3;

            int row = index / columns;
            int col = index % columns;
            int startX = GetEquipX() + 10;
            int startY = GetPantsPocketBottomY(unit) + 20;

            return new Rectangle(
                startX + col * (slotSize + spacing),
                startY + row * (slotSize + spacing),
                slotSize,
                slotSize
            );
        }

        private int GetChestRigPocketBottomY(Unit unit)
        {
            int pocketsCount = unit.GetChestRigInventoryCapacity();
            if (pocketsCount <= 0)
                return GetPantsPocketBottomY(unit) + 20;

            return GetChestRigPocketSlotByIndex(pocketsCount - 1, unit).Bottom;
        }

        private int GetEquipmentPanelHeight(Unit unit)
        {
            int lastContentBottom = Math.Max(GetPantsPocketBottomY(unit), GetChestRigPocketBottomY(unit));
            return lastContentBottom - GetEquipY() + 20;
        }

        public bool IsDragging => draggedItem != null;
    }
}
