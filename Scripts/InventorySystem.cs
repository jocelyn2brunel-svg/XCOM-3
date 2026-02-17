using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
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
        private const int CONTEXT_WINDOW_HEIGHT = 220;
        private const int LOOT_GRID_CELL_SIZE = CELL_SIZE;
        private const int LOOT_GRID_LABEL_HEIGHT = 22;
        private const int LOOT_GRID_BOTTOM_INFO_HEIGHT = 24;
        private const int LootHeaderTextHeight = 12;
        private const int UiSoundSampleRate = 22050;

        // ═══════════════════════════════════════════════════════════════════════
        // ÉTAT
        // ═══════════════════════════════════════════════════════════════════════

        private InventoryGrid inventoryGrid;
        private GridItem draggedItem = null;
        private bool draggedItemFromNearbyLoot = false;
        private Point dragGridOffset;
        private Point dragPixelOffset;
        private readonly List<ItemData> nearbyLootItems = new List<ItemData>();
        private readonly Random random = new Random();
        private readonly SoundEffect uiClickSound;
        private readonly SoundEffect uiEquipSound;
        private readonly SoundEffect uiErrorSound;
        public Dictionary<string, ItemData> ItemDatabase { get; private set; }

        // Ressources graphiques (injectées)
        private SpriteBatch spriteBatch;
        private SpriteFont font;
        private Texture2D pixel;
        private GraphicsDevice graphicsDevice;
        private Texture2D flashlightTexture;
        private Dictionary<string, Texture2D> armorTextures;
        private Dictionary<string, Texture2D> weaponTextures;
        private readonly BasicEffect previewEffect;
        private readonly HumanoidModelAdvanced previewModel;
        private RenderTarget2D previewRenderTarget;

        private const float PreviewModelScale = 1.85f;
        private const float PreviewRotationSpeed = 1.8f;
        private const float PreviewMouseRotationSensitivity = 0.015f;
        private float previewRotation = 0f;
        private bool isDraggingPreview = false;
        private int lastDragMouseX = 0;

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
        private Rectangle contextThrowButtonRect;
        private Rectangle contextToggleFlashlightButtonRect;
        private Rectangle contextUnequipButtonRect;
        private Rectangle contextCloseButtonRect;
        private bool contextMenuForEquippedFlashlight = false;
        private string contextFlashlightToggleLabel = "ALLUMER/ETEINDRE";
        private bool pendingFlashlightThrowRequest = false;
        private FlashlightHand pendingFlashlightThrowHand = FlashlightHand.None;

        private bool showExaminePopup = false;
        private Rectangle examinePopupRect;
        private ItemData examinedItemData;
        private readonly List<Rectangle> nearbyLootSlotRects = new List<Rectangle>();
        private readonly List<int> nearbyLootSlotItemIndexes = new List<int>();
        private int nearbyLootScrollRow = 0;

        private struct NearbyLootLayoutEntry
        {
            public int ItemIndex;
            public int CellX;
            public int CellY;
            public int CellWidth;
            public int CellHeight;
        }

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

        private enum FlashlightHand
        {
            None,
            Right,
            Left
        }

        public bool TryConsumeFlashlightThrowRequest(out bool isRightHand)
        {
            isRightHand = false;

            if (!pendingFlashlightThrowRequest || pendingFlashlightThrowHand == FlashlightHand.None)
                return false;

            isRightHand = pendingFlashlightThrowHand == FlashlightHand.Right;
            pendingFlashlightThrowRequest = false;
            pendingFlashlightThrowHand = FlashlightHand.None;
            return true;
        }

        public bool TryAddNearbyLootByName(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return false;

            if (!ItemDatabase.TryGetValue(itemName, out ItemData lootData) || lootData == null)
                return false;

            nearbyLootItems.Add(lootData);
            ClampNearbyLootScroll();
            return true;
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
            armorTextures = LoadArmorTextures();
            weaponTextures = LoadWeaponTextures();

            uiClickSound = CreateUiTone(760f, 46, 0.15f);
            uiEquipSound = CreateUiTone(980f, 58, 0.2f);
            uiErrorSound = CreateUiTone(220f, 90, 0.2f);

            previewEffect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                AmbientLightColor = new Vector3(0.5f),
                DiffuseColor = new Vector3(1f)
            };
            previewModel = new HumanoidModelAdvanced();

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




        private Dictionary<string, Texture2D> LoadArmorTextures()
        {
            var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
            {
                ["helmet_m1"] = CreateArmorIconTexture(new Color(130, 160, 110), ArmorIconKind.Helmet),
                ["helmet_pasgt"] = CreateArmorIconTexture(new Color(95, 125, 85), ArmorIconKind.Helmet),
                ["helmet_modern"] = CreateArmorIconTexture(new Color(170, 170, 150), ArmorIconKind.Helmet),
                ["neck_ballistic"] = CreateArmorIconTexture(new Color(125, 125, 120), ArmorIconKind.Neck),
                ["vest_flak"] = CreateArmorIconTexture(new Color(150, 130, 95), ArmorIconKind.Vest),
                ["vest_pasgt"] = CreateArmorIconTexture(new Color(95, 120, 85), ArmorIconKind.Vest),
                ["vest_plate"] = CreateArmorIconTexture(new Color(90, 110, 125), ArmorIconKind.Vest),
                ["shield_riot"] = CreateArmorIconTexture(new Color(105, 125, 145), ArmorIconKind.Shield),
                ["shield_ballistic"] = CreateArmorIconTexture(new Color(75, 85, 95), ArmorIconKind.Shield),
                ["shirt_combat"] = CreateArmorIconTexture(new Color(115, 140, 100), ArmorIconKind.Shirt),
                ["pants_jeans"] = CreateArmorIconTexture(new Color(70, 110, 170), ArmorIconKind.Pants),
                ["pants_cargo"] = CreateArmorIconTexture(new Color(115, 135, 100), ArmorIconKind.Pants),
                ["chest_rig"] = CreateArmorIconTexture(new Color(120, 105, 85), ArmorIconKind.ChestRig),
                ["backpack"] = CreateArmorIconTexture(new Color(90, 120, 85), ArmorIconKind.Backpack)
            };

            return textures;
        }

        private Dictionary<string, Texture2D> LoadWeaponTextures()
        {
            return new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
            {
                ["pistol"] = CreateWeaponIconTexture(new Color(160, 160, 170), WeaponIconKind.Pistol),
                ["smg"] = CreateWeaponIconTexture(new Color(120, 135, 150), WeaponIconKind.Smg),
                ["assault_rifle"] = CreateWeaponIconTexture(new Color(105, 125, 95), WeaponIconKind.AssaultRifle),
                ["rifle"] = CreateWeaponIconTexture(new Color(130, 120, 90), WeaponIconKind.Rifle),
                ["shotgun"] = CreateWeaponIconTexture(new Color(110, 95, 80), WeaponIconKind.Shotgun),
                ["sniper"] = CreateWeaponIconTexture(new Color(95, 110, 125), WeaponIconKind.Sniper)
            };
        }

        private enum ArmorIconKind
        {
            Helmet,
            Neck,
            Vest,
            Shield,
            Shirt,
            Pants,
            ChestRig,
            Backpack
        }

        private enum WeaponIconKind
        {
            Pistol,
            Smg,
            AssaultRifle,
            Rifle,
            Shotgun,
            Sniper
        }

        private Texture2D CreateArmorIconTexture(Color accent, ArmorIconKind kind)
        {
            const int width = 64;
            const int height = 64;
            var data = new Color[width * height];

            Color background = ParasiteEveTheme.BackgroundDark;
            Color inner = Color.Lerp(accent, Color.Black, 0.25f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    data[y * width + x] = border ? accent : background;
                }
            }

            bool FillRect(int x0, int y0, int x1, int y1)
            {
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++)
                    for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                        data[y * width + x] = accent;
                return true;
            }

            switch (kind)
            {
                case ArmorIconKind.Helmet:
                    for (int y = 12; y <= 42; y++)
                    {
                        for (int x = 10; x <= 54; x++)
                        {
                            int dx = x - 32;
                            int dy = y - 26;
                            if (dx * dx * 3 + dy * dy * 5 <= 1450)
                                data[y * width + x] = accent;
                        }
                    }
                    FillRect(14, 38, 50, 44);
                    break;

                case ArmorIconKind.Neck:
                    FillRect(20, 12, 44, 48);
                    FillRect(16, 20, 48, 30);
                    FillRect(24, 24, 40, 40);
                    for (int y = 26; y <= 36; y++)
                        for (int x = 26; x <= 38; x++)
                            data[y * width + x] = inner;
                    break;

                case ArmorIconKind.Vest:
                    FillRect(16, 10, 48, 52);
                    FillRect(24, 18, 40, 50);
                    for (int y = 24; y <= 46; y++)
                        for (int x = 26; x <= 38; x++)
                            data[y * width + x] = inner;
                    break;

                case ArmorIconKind.Shield:
                    for (int y = 8; y <= 56; y++)
                    {
                        int half = 20 - Math.Abs(y - 32) / 2;
                        for (int x = 32 - half; x <= 32 + half; x++)
                            data[y * width + x] = accent;
                    }
                    break;

                case ArmorIconKind.Shirt:
                    FillRect(18, 16, 46, 52);
                    FillRect(10, 18, 17, 34);
                    FillRect(47, 18, 54, 34);
                    break;

                case ArmorIconKind.Pants:
                    FillRect(22, 10, 42, 24);
                    FillRect(22, 24, 30, 54);
                    FillRect(34, 24, 42, 54);
                    break;

                case ArmorIconKind.ChestRig:
                    FillRect(14, 14, 50, 50);
                    FillRect(14, 10, 50, 14);
                    for (int i = 0; i < 4; i++)
                        FillRect(18 + i * 8, 26, 22 + i * 8, 42);
                    break;

                case ArmorIconKind.Backpack:
                    FillRect(16, 10, 48, 54);
                    FillRect(22, 16, 42, 48);
                    for (int y = 16; y <= 48; y++)
                        for (int x = 22; x <= 42; x++)
                            data[y * width + x] = inner;
                    break;
            }

            var texture = new Texture2D(graphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateWeaponIconTexture(Color accent, WeaponIconKind kind)
        {
            const int width = 96;
            const int height = 64;
            var data = new Color[width * height];

            Color background = ParasiteEveTheme.BackgroundDark;
            Color metal = Color.Lerp(accent, Color.White, 0.2f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    data[y * width + x] = border ? accent : background;
                }
            }

            void FillRect(int x0, int y0, int x1, int y1, Color color)
            {
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++)
                    for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                        data[y * width + x] = color;
            }

            switch (kind)
            {
                case WeaponIconKind.Pistol:
                    FillRect(20, 24, 66, 32, metal);
                    FillRect(38, 33, 48, 50, accent);
                    FillRect(62, 20, 68, 23, accent);
                    break;
                case WeaponIconKind.Smg:
                    FillRect(14, 22, 72, 30, metal);
                    FillRect(26, 31, 35, 45, accent);
                    FillRect(42, 30, 62, 34, accent);
                    FillRect(70, 24, 80, 26, accent);
                    break;
                case WeaponIconKind.AssaultRifle:
                    FillRect(10, 22, 82, 29, metal);
                    FillRect(20, 30, 32, 48, accent);
                    FillRect(45, 30, 62, 34, accent);
                    FillRect(74, 20, 89, 23, accent);
                    break;
                case WeaponIconKind.Rifle:
                    FillRect(6, 24, 88, 29, metal);
                    FillRect(12, 30, 26, 44, accent);
                    FillRect(80, 22, 92, 24, accent);
                    break;
                case WeaponIconKind.Shotgun:
                    FillRect(8, 23, 84, 30, metal);
                    FillRect(18, 31, 34, 45, accent);
                    FillRect(70, 21, 88, 22, accent);
                    break;
                case WeaponIconKind.Sniper:
                    FillRect(4, 25, 90, 29, metal);
                    FillRect(30, 21, 54, 24, accent);
                    FillRect(24, 30, 36, 44, accent);
                    FillRect(84, 23, 94, 24, accent);
                    break;
            }

            var texture = new Texture2D(graphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }


        private Texture2D GetArmorTexture(ItemData data)
        {
            if (data == null || data.Type != ItemType.Armor || armorTextures == null || armorTextures.Count == 0)
                return null;

            string name = data.Name ?? string.Empty;

            if (name.Contains("M1", StringComparison.OrdinalIgnoreCase) && armorTextures.TryGetValue("helmet_m1", out Texture2D m1))
                return m1;

            if (data.ArmorSlot == ArmorSlot.Neck && armorTextures.TryGetValue("neck_ballistic", out Texture2D neckArmor))
                return neckArmor;

            if ((name.Contains("PASGT", StringComparison.OrdinalIgnoreCase) || name.Contains("Lightweight Helmet", StringComparison.OrdinalIgnoreCase)) &&
                data.ArmorSlot == ArmorSlot.Head && armorTextures.TryGetValue("helmet_pasgt", out Texture2D pasgtHelmet))
                return pasgtHelmet;

            if ((name.Contains("MICH", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ACH", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ECH", StringComparison.OrdinalIgnoreCase)) &&
                data.ArmorSlot == ArmorSlot.Head && armorTextures.TryGetValue("helmet_modern", out Texture2D modernHelmet))
                return modernHelmet;

            if ((name.Contains("M-1952", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("M-69", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("M-1955", StringComparison.OrdinalIgnoreCase)) &&
                armorTextures.TryGetValue("vest_flak", out Texture2D flakVest))
                return flakVest;

            if (name.Contains("PASGT", StringComparison.OrdinalIgnoreCase) && data.ArmorSlot == ArmorSlot.Torso && armorTextures.TryGetValue("vest_pasgt", out Texture2D pasgtVest))
                return pasgtVest;

            if ((name.Contains("OTV", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("MTV", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("IMTV", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("IOTV", StringComparison.OrdinalIgnoreCase)) &&
                armorTextures.TryGetValue("vest_plate", out Texture2D plateVest))
                return plateVest;

            if (name.Contains("Riot Shield", StringComparison.OrdinalIgnoreCase) && armorTextures.TryGetValue("shield_riot", out Texture2D riotShield))
                return riotShield;

            if (data.ArmorSlot == ArmorSlot.Shield && armorTextures.TryGetValue("shield_ballistic", out Texture2D ballisticShield))
                return ballisticShield;

            if (data.ArmorSlot == ArmorSlot.Shirt && armorTextures.TryGetValue("shirt_combat", out Texture2D shirt))
                return shirt;

            if (name.Contains("Jeans", StringComparison.OrdinalIgnoreCase) && armorTextures.TryGetValue("pants_jeans", out Texture2D jeans))
                return jeans;

            if (data.ArmorSlot == ArmorSlot.Pants && armorTextures.TryGetValue("pants_cargo", out Texture2D pants))
                return pants;

            if (data.ArmorSlot == ArmorSlot.ChestRig && armorTextures.TryGetValue("chest_rig", out Texture2D chestRig))
                return chestRig;

            if (data.ArmorSlot == ArmorSlot.Backpack && armorTextures.TryGetValue("backpack", out Texture2D backpack))
                return backpack;

            return null;
        }

        private Texture2D GetWeaponTexture(ItemData data)
        {
            if (data?.Type != ItemType.Weapon)
                return null;

            string name = data.Name ?? string.Empty;

            if (name.Contains("Sniper", StringComparison.OrdinalIgnoreCase) && weaponTextures.TryGetValue("sniper", out Texture2D sniperTexture))
                return sniperTexture;

            if (name.Contains("Shotgun", StringComparison.OrdinalIgnoreCase) && weaponTextures.TryGetValue("shotgun", out Texture2D shotgunTexture))
                return shotgunTexture;

            if ((name.Contains("SMG", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("MP5", StringComparison.OrdinalIgnoreCase)) && weaponTextures.TryGetValue("smg", out Texture2D smgTexture))
                return smgTexture;

            if ((name.Contains("Assault", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("M16", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("AK-", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("M4", StringComparison.OrdinalIgnoreCase)) && weaponTextures.TryGetValue("assault_rifle", out Texture2D assaultRifleTexture))
                return assaultRifleTexture;

            if ((name.Contains("Pistol", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Beretta", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Glock", StringComparison.OrdinalIgnoreCase)) && weaponTextures.TryGetValue("pistol", out Texture2D pistolTexture))
                return pistolTexture;

            if (weaponTextures.TryGetValue("rifle", out Texture2D rifleTexture))
                return rifleTexture;

            return null;
        }

        private Texture2D GetItemPreviewTexture(ItemData data)
            => GetArmorTexture(data) ?? GetWeaponTexture(data);

        private void DrawItemPreviewImage(ItemData data, Rectangle targetRect, float alpha = 1f)
        {
            Texture2D previewTexture = GetItemPreviewTexture(data);
            if (previewTexture == null)
                return;

            Rectangle imageRect = new Rectangle(targetRect.X + 3, targetRect.Y + 3,
                Math.Max(1, targetRect.Width - 6), Math.Max(1, targetRect.Height - 6));
            spriteBatch.Draw(previewTexture, imageRect, Color.White * alpha);
        }

        private SoundEffect CreateUiTone(float frequencyHz, int durationMs, float baseVolume)
        {
            int sampleCount = Math.Max(1, UiSoundSampleRate * durationMs / 1000);
            byte[] samples = new byte[sampleCount * 2];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)UiSoundSampleRate;
                float envelope = 1f - (i / (float)sampleCount);
                float wave = (float)Math.Sin(MathHelper.TwoPi * frequencyHz * t);
                short sample = (short)(wave * envelope * baseVolume * short.MaxValue);

                samples[i * 2] = (byte)(sample & 0xFF);
                samples[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return new SoundEffect(samples, UiSoundSampleRate, AudioChannels.Mono);
        }

        private static void PlayUiSound(SoundEffect sound, float volume = 1f)
        {
            sound?.Play(Math.Clamp(volume, 0f, 1f), 0f, 0f);
        }

        private static bool IsTacticalFlashlight(ItemData data)
        {
            return string.Equals(data?.Name, "Lampe tactique aluminium", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHandFlashlightSource(string source)
        {
            return source == "rightflashlight" || source == "leftflashlight";
        }

        private static FlashlightHand GetFlashlightHandFromSource(string source)
        {
            if (source == "rightflashlight") return FlashlightHand.Right;
            if (source == "leftflashlight") return FlashlightHand.Left;
            return FlashlightHand.None;
        }

        private static bool TryToggleFlashlight(ItemContextInfo info, Unit unit)
        {
            if (!IsTacticalFlashlight(info.Data) || unit == null)
                return false;

            FlashlightHand hand = GetFlashlightHandFromSource(info.Source);
            if (hand == FlashlightHand.Right)
            {
                unit.IsRightHandFlashlightOn = !unit.IsRightHandFlashlightOn;
                return true;
            }

            if (hand == FlashlightHand.Left)
            {
                unit.IsLeftHandFlashlightOn = !unit.IsLeftHandFlashlightOn;
                return true;
            }

            return false;
        }

        private static bool IsEquippedTacticalFlashlight(ItemContextInfo info)
        {
            return IsTacticalFlashlight(info.Data) && IsHandFlashlightSource(info.Source);
        }

        private static string GetFlashlightToggleLabel(ItemContextInfo info, Unit unit)
        {
            if (GetFlashlightHandFromSource(info.Source) == FlashlightHand.Right)
                return unit?.IsRightHandFlashlightOn == true ? "ETEINDRE" : "ALLUMER";

            if (GetFlashlightHandFromSource(info.Source) == FlashlightHand.Left)
                return unit?.IsLeftHandFlashlightOn == true ? "ETEINDRE" : "ALLUMER";

            return "ALLUMER/ETEINDRE";
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INITIALISATION
        // ═══════════════════════════════════════════════════════════════════════

        private void InitializeItemDatabase()
        {
            // Armes
            ItemDatabase["Rifle"] = new ItemData("Rifle", ItemType.Weapon,
                new WeaponData("Rifle", 25, 80, 5));
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
                "Rifle",
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

            HandlePreviewRotation(mouse, previousMouse, keyboard);

            bool rightClick = mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;

            // Accumuler le temps pour l'effet Sinus du pulse
            totalElapsedTime += 0.016f; // Environ 60 FPS, ou utilise gameTime.ElapsedGameTime

            int gridStartX = GetGridStartX();
            int gridStartY = GetGridStartY();

            HandleNearbyLootScroll(mouse, previousMouse);

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
                PlayUiSound(uiClickSound, 0.55f);

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

        public void DrawPreview3D(Unit unit)
        {
            if (unit == null)
                return;

            Rectangle previewRect = GetPreviewViewportRect();
            EnsurePreviewRenderTarget(previewRect.Width, previewRect.Height);
            if (previewRenderTarget == null)
                return;

            Viewport originalViewport = graphicsDevice.Viewport;
            DepthStencilState originalDepth = graphicsDevice.DepthStencilState;
            BlendState originalBlend = graphicsDevice.BlendState;
            RasterizerState originalRasterizer = graphicsDevice.RasterizerState;
            RenderTargetBinding[] originalRenderTargets = graphicsDevice.GetRenderTargets();

            graphicsDevice.SetRenderTarget(previewRenderTarget);
            graphicsDevice.Viewport = new Viewport(0, 0, previewRenderTarget.Width, previewRenderTarget.Height);
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.RasterizerState = RasterizerState.CullNone;
            graphicsDevice.Clear(new Color(14, 18, 28));

            previewEffect.View = Matrix.CreateLookAt(new Vector3(0f, 2.5f, 5.6f), new Vector3(0f, 1.8f, 0f), Vector3.Up);
            previewEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f),
                Math.Max(0.2f, previewRect.Width / (float)previewRect.Height),
                0.1f,
                100f);

            Unit previewUnit = new Unit(unit)
            {
                VisualPosition = new Vector3(0f, 0f, 0f),
                IsMoving = false,
                IsAiming = false,
                IsFiring = false
            };
            previewUnit.LegSwing = 0f;
            previewUnit.ArmSwing = 0f;
            previewUnit.BodyBob = 0f;
            previewUnit.IdleBobOffset = 0f;

            previewModel.DrawWithEquipment(
                graphicsDevice,
                previewEffect,
                previewUnit,
                PreviewModelScale,
                MathHelper.Pi + previewRotation);

            graphicsDevice.SetRenderTargets(originalRenderTargets);
            graphicsDevice.Viewport = originalViewport;
            graphicsDevice.DepthStencilState = originalDepth;
            graphicsDevice.BlendState = originalBlend;
            graphicsDevice.RasterizerState = originalRasterizer;
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
                    PlayUiSound(uiClickSound, 0.45f);

                    Console.WriteLine($"[INVENTORY] Drag from grid: {draggedItem.Data.Name}");
                    return;
                }
            }

            if (TryStartDragFromNearbyLoot(mouse.Position))
                return;

            // ✅ VÉRIFIER ET DÉSÉQUIPER LES SLOTS
            // ✅ VÉRIFIER ET DÉSÉQUIPER LES SLOTS
            Rectangle weaponSlot = GetWeaponSlotBounds();
            if (weaponSlot.Contains(mouse.Position))
            {
                if (unit.EquippedWeapon != null)
                {
                    StartDragFromEquipment(unit.EquippedWeapon, mouse, weaponSlot);
                    unit.EquippedWeapon = null;
                    unit.Weapon = string.Empty;
                    unit.WeaponData = null;
                    PlayUiSound(uiClickSound, 0.48f);

                    Console.WriteLine($"[INVENTORY] Unequipped weapon: {draggedItem.Data.Name}");
                    return;
                }

                if (unit.EquippedRightHandFlashlight != null)
                {
                    StartDragFromEquipment(unit.EquippedRightHandFlashlight, mouse, weaponSlot);
                    unit.EquippedRightHandFlashlight = null;
                    unit.IsRightHandFlashlightOn = false;
                    PlayUiSound(uiClickSound, 0.48f);

                    Console.WriteLine($"[INVENTORY] Unequipped right hand flashlight: {draggedItem.Data.Name}");
                    return;
                }
            }

            Rectangle helmetSlot = GetHelmetSlotBounds();
            if (unit.EquippedHelmet != null && helmetSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedHelmet, mouse, helmetSlot);
                unit.EquippedHelmet = null;
                PlayUiSound(uiClickSound, 0.48f);

                Console.WriteLine($"[INVENTORY] Unequipped helmet: {draggedItem.Data.Name}");
                return;
            }

            Rectangle neckSlot = GetNeckSlotBounds();
            if (unit.EquippedNeck != null && neckSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedNeck, mouse, neckSlot);
                unit.EquippedNeck = null;
                PlayUiSound(uiClickSound, 0.48f);

                Console.WriteLine($"[INVENTORY] Unequipped neck armor: {draggedItem.Data.Name}");
                return;
            }

            Rectangle armorSlot = GetArmorSlotBounds();
            if (unit.EquippedArmor != null && armorSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedArmor, mouse, armorSlot);
                unit.EquippedArmor = null;
                PlayUiSound(uiClickSound, 0.48f);

                Console.WriteLine($"[INVENTORY] Unequipped armor: {draggedItem.Data.Name}");
                return;
            }

            Rectangle shieldSlot = GetShieldSlotBounds();
            if (shieldSlot.Contains(mouse.Position))
            {
                if (unit.EquippedShield != null)
                {
                    StartDragFromEquipment(unit.EquippedShield, mouse, shieldSlot);
                    unit.EquippedShield = null;
                    PlayUiSound(uiClickSound, 0.48f);

                    Console.WriteLine($"[INVENTORY] Unequipped shield: {draggedItem.Data.Name}");
                    return;
                }

                if (unit.EquippedLeftHandFlashlight != null)
                {
                    StartDragFromEquipment(unit.EquippedLeftHandFlashlight, mouse, shieldSlot);
                    unit.EquippedLeftHandFlashlight = null;
                    unit.IsLeftHandFlashlightOn = false;
                    PlayUiSound(uiClickSound, 0.48f);

                    Console.WriteLine($"[INVENTORY] Unequipped left hand flashlight: {draggedItem.Data.Name}");
                    return;
                }
            }

            Rectangle beltSlot = GetBeltSlotBounds();
            if (unit.EquippedAccessory != null && beltSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedAccessory, mouse, beltSlot);
                unit.EquippedAccessory = null;
                PlayUiSound(uiClickSound, 0.48f);

                Console.WriteLine($"[INVENTORY] Unequipped accessory: {draggedItem.Data.Name}");
                return;
            }

            if (unit.EquippedBelt != null && beltSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedBelt, mouse, beltSlot);
                unit.EquippedBelt = null;
                PlayUiSound(uiClickSound, 0.48f);

                Console.WriteLine($"[INVENTORY] Unequipped belt: {draggedItem.Data.Name}");
                return;
            }

            Rectangle shirtSlot = GetShirtSlotBounds();
            if (unit.EquippedShirt != null && shirtSlot.Contains(mouse.Position))
            {
                StartDragFromEquipment(unit.EquippedShirt, mouse, shirtSlot);
                unit.EquippedShirt = null;
                PlayUiSound(uiClickSound, 0.48f);

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
                PlayUiSound(uiClickSound, 0.48f);

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
                    PlayUiSound(uiClickSound, 0.48f);

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
                    PlayUiSound(uiClickSound, 0.48f);

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
                    PlayUiSound(uiClickSound, 0.48f);

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
                    PlayUiSound(uiClickSound, 0.48f);

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
                PlayUiSound(uiClickSound, 0.48f);

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
                draggedItemFromNearbyLoot = false;
                PlayUiSound(uiClickSound, 0.5f);

                Console.WriteLine($"[INVENTORY] Dropped outside interface, sent to nearby loot: {draggedItem.Data.Name}");
                draggedItem = null;
                return;
            }

            // ✅ VÉRIFIER D'ABORD L'ÉQUIPEMENT (priorité absolue)
            bool equipped = TryEquipInSlot(mouse.Position, draggedItem, unit);

            if (!equipped)
            {
                if (lootWindow.Contains(mouse.Position))
                {
                    nearbyLootItems.Add(draggedItem.Data);
                    draggedItemFromNearbyLoot = false;
                    PlayUiSound(uiClickSound, 0.5f);

                    Console.WriteLine($"[INVENTORY] Dropped in nearby loot panel: {draggedItem.Data.Name}");
                    draggedItem = null;
                    return;
                }

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
                        PlayUiSound(uiClickSound, 0.5f);

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
                            PlayUiSound(uiClickSound, 0.5f);

                            Console.WriteLine($"[INVENTORY] Auto-placed at {freePos.Value}: {draggedItem.Data.Name}");
                        }
                        else
                        {
                            if (draggedItemFromNearbyLoot)
                            {
                                nearbyLootItems.Add(draggedItem.Data);
                                PlayUiSound(uiErrorSound, 0.65f);

                                Console.WriteLine($"[INVENTORY] No space. Item returned to nearby loot: {draggedItem.Data.Name}");
                            }
                            else
                            {
                                PlayUiSound(uiErrorSound, 0.65f);

                                Console.WriteLine($"[INVENTORY] WARNING: No space! Item lost: {draggedItem.Data.Name}");
                            }
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
                        PlayUiSound(uiClickSound, 0.5f);

                        Console.WriteLine($"[INVENTORY] Dropped outside, auto-placed at {freePos.Value}: {draggedItem.Data.Name}");
                    }
                    else
                    {
                        if (draggedItemFromNearbyLoot)
                        {
                            nearbyLootItems.Add(draggedItem.Data);
                            PlayUiSound(uiErrorSound, 0.65f);

                            Console.WriteLine($"[INVENTORY] No space. Item returned to nearby loot: {draggedItem.Data.Name}");
                        }
                        else
                        {
                            PlayUiSound(uiErrorSound, 0.65f);

                            Console.WriteLine($"[INVENTORY] WARNING: No space! Item lost: {draggedItem.Data.Name}");
                        }
                    }
                }
            }

            draggedItem = null;
            draggedItemFromNearbyLoot = false;
        }

        private bool TryStartDragFromNearbyLoot(Point mousePosition)
        {
            if (!TryGetNearbyLootEntryAt(mousePosition, out int lootIndex, out Rectangle lootSlot))
                return false;

            ItemData lootData = nearbyLootItems[lootIndex];
            ItemSize lootSize = ItemSizeDatabase.GetItemSize(lootData.Name);
            draggedItem = new GridItem(lootData, Point.Zero, lootSize, false);
            nearbyLootItems.RemoveAt(lootIndex);
            ClampNearbyLootScroll();

            int maxWidth = lootSize.Width * LOOT_GRID_CELL_SIZE - 1;
            int maxHeight = lootSize.Height * LOOT_GRID_CELL_SIZE - 1;
            int offsetX = Math.Clamp(mousePosition.X - lootSlot.X, 0, maxWidth);
            int offsetY = Math.Clamp(mousePosition.Y - lootSlot.Y, 0, maxHeight);

            dragPixelOffset = new Point(offsetX, offsetY);
            dragGridOffset = new Point(offsetX / LOOT_GRID_CELL_SIZE, offsetY / LOOT_GRID_CELL_SIZE);
            draggedItemFromNearbyLoot = true;
            PlayUiSound(uiClickSound, 0.45f);

            Console.WriteLine($"[INVENTORY] Drag from nearby loot: {draggedItem.Data.Name}");
            return true;
        }

        private bool TryPickupNearbyLoot(Point mousePosition)
        {
            if (!TryGetNearbyLootEntryAt(mousePosition, out int lootIndex, out _))
                return false;

            ItemData lootData = nearbyLootItems[lootIndex];
            ItemSize lootSize = ItemSizeDatabase.GetItemSize(lootData.Name);
            Point? freePos = inventoryGrid.FindFreePosition(lootSize, true);
            if (!freePos.HasValue)
            {
                PlayUiSound(uiErrorSound, 0.65f);

                Console.WriteLine($"[INVENTORY] Cannot pickup nearby loot (inventory full): {lootData.Name}");
                return true;
            }

            inventoryGrid.PlaceItem(new GridItem(lootData, freePos.Value, lootSize, false));
            nearbyLootItems.RemoveAt(lootIndex);
            ClampNearbyLootScroll();
            PlayUiSound(uiEquipSound, 0.58f);

            Console.WriteLine($"[INVENTORY] Picked nearby loot: {lootData.Name}");
            return true;
        }

        private bool TryGetNearbyLootEntryAt(Point mousePosition, out int lootIndex, out Rectangle lootSlot)
        {
            lootIndex = -1;
            lootSlot = Rectangle.Empty;

            if (nearbyLootItems.Count == 0)
                return false;

            for (int i = 0; i < nearbyLootSlotRects.Count; i++)
            {
                if (!nearbyLootSlotRects[i].Contains(mousePosition))
                    continue;

                if (i >= nearbyLootSlotItemIndexes.Count)
                    return false;

                int candidateIndex = nearbyLootSlotItemIndexes[i];
                if (candidateIndex < 0 || candidateIndex >= nearbyLootItems.Count)
                    return false;

                lootIndex = candidateIndex;
                lootSlot = nearbyLootSlotRects[i];
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

            FlashlightHand flashlightHand = TryGetFlashlightHandForSlot(mousePosition);
            if (IsTacticalFlashlight(item.Data) && flashlightHand != FlashlightHand.None)
            {
                EquipFlashlightInHand(unit, flashlightHand, item.Data);
                PlayUiSound(uiEquipSound, 0.6f);

                Console.WriteLine($"[INVENTORY] ✅ Equipped flashlight in {flashlightHand} hand: {item.Data.Name}");
                return true;
            }

            Rectangle weaponSlot = GetWeaponSlotBounds();
            if (item.Data.Type == ItemType.Weapon && weaponSlot.Contains(mousePosition))
            {
                if (unit.EquippedWeapon != null)
                    ReturnItemToGrid(unit.EquippedWeapon);

                unit.EquippedWeapon = new Item(item.Data, Point.Zero);
                unit.Weapon = item.Data.Name;
                unit.WeaponData = item.Data.WeaponData;
                PlayUiSound(uiEquipSound, 0.6f);

                Console.WriteLine($"[INVENTORY] ✅ Equipped weapon: {item.Data.Name}");
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
                        PlayUiSound(uiEquipSound, 0.6f);

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
                        PlayUiSound(uiEquipSound, 0.6f);

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
                    PlayUiSound(uiEquipSound, 0.6f);

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
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped helmet: {item.Data.Name}");
                    return true;
                }

                Rectangle neckSlot = GetNeckSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Neck && neckSlot.Contains(mousePosition))
                {
                    if (unit.EquippedNeck != null)
                        ReturnItemToGrid(unit.EquippedNeck);
                    unit.EquippedNeck = new Item(item.Data, Point.Zero);
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped neck armor: {item.Data.Name}");
                    return true;
                }

                Rectangle armorSlot = GetArmorSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Torso && armorSlot.Contains(mousePosition))
                {
                    if (unit.EquippedArmor != null)
                        ReturnItemToGrid(unit.EquippedArmor);
                    unit.EquippedArmor = new Item(item.Data, Point.Zero);
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped armor: {item.Data.Name}");
                    return true;
                }

                Rectangle shieldSlot = GetShieldSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Shield && shieldSlot.Contains(mousePosition))
                {
                    if (unit.EquippedShield != null)
                        ReturnItemToGrid(unit.EquippedShield);
                    unit.EquippedShield = new Item(item.Data, Point.Zero);
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped shield: {item.Data.Name}");
                    return true;
                }

                Rectangle shirtSlot = GetShirtSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Shirt && shirtSlot.Contains(mousePosition))
                {
                    if (unit.EquippedShirt != null)
                        ReturnItemToGrid(unit.EquippedShirt);
                    unit.EquippedShirt = new Item(item.Data, Point.Zero);
                    PlayUiSound(uiEquipSound, 0.6f);

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
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped pants: {item.Data.Name}");
                    return true;
                }

                Rectangle kneesSlot = GetKneesSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Knees && kneesSlot.Contains(mousePosition))
                {
                    if (unit.EquippedKnees != null)
                        ReturnItemToGrid(unit.EquippedKnees);
                    unit.EquippedKnees = new Item(item.Data, Point.Zero);
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped knees armor: {item.Data.Name}");
                    return true;
                }

                Rectangle feetSlot = GetFeetSlotBounds();
                if (item.Data.ArmorSlot == ArmorSlot.Feet && feetSlot.Contains(mousePosition))
                {
                    if (unit.EquippedFeet != null)
                        ReturnItemToGrid(unit.EquippedFeet);
                    unit.EquippedFeet = new Item(item.Data, Point.Zero);
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped feet armor: {item.Data.Name}");
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
                    PlayUiSound(uiEquipSound, 0.6f);

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
                    PlayUiSound(uiEquipSound, 0.6f);

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
                    PlayUiSound(uiEquipSound, 0.6f);

                    Console.WriteLine($"[INVENTORY] ✅ Equipped backpack: {item.Data.Name}");
                    return true;
                }
            }

            PlayUiSound(uiErrorSound, 0.62f);


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
                PlayUiSound(uiClickSound, 0.42f);

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
                    ItemContextInfo info = clickedItem.Value;
                    contextMenuItem = info;
                    contextMenuForEquippedFlashlight = IsEquippedTacticalFlashlight(info);
                    contextMenuRect = BuildContextWindow(mouse.Position);

                    if (contextMenuForEquippedFlashlight)
                    {
                        contextFlashlightToggleLabel = GetFlashlightToggleLabel(info, unit);
                        contextThrowButtonRect = new Rectangle(contextMenuRect.X + 12, contextMenuRect.Y + 98, contextMenuRect.Width - 24, 22);
                        contextToggleFlashlightButtonRect = new Rectangle(contextMenuRect.X + 12, contextThrowButtonRect.Bottom + 4, contextMenuRect.Width - 24, 22);
                        contextUnequipButtonRect = new Rectangle(contextMenuRect.X + 12, contextToggleFlashlightButtonRect.Bottom + 4, contextMenuRect.Width - 24, 22);
                        contextExamineButtonRect = new Rectangle(contextMenuRect.X + 12, contextUnequipButtonRect.Bottom + 4, contextMenuRect.Width - 24, 22);
                        contextCloseButtonRect = new Rectangle(contextMenuRect.X + 12, contextExamineButtonRect.Bottom + 4, contextMenuRect.Width - 24, 16);
                    }
                    else
                    {
                        contextEquipButtonRect = new Rectangle(contextMenuRect.X + 12, contextMenuRect.Bottom - 76, contextMenuRect.Width - 24, 22);
                        contextExamineButtonRect = new Rectangle(contextMenuRect.X + 12, contextMenuRect.Bottom - 50, contextMenuRect.Width - 24, 22);
                        contextCloseButtonRect = new Rectangle(contextMenuRect.X + 12, contextMenuRect.Bottom - 24, contextMenuRect.Width - 24, 16);
                    }

                    showContextMenu = true;
                    showExaminePopup = false;
                    PlayUiSound(uiClickSound, 0.45f);
                }
                else
                {
                    showContextMenu = false;
                    contextMenuForEquippedFlashlight = false;
                }
            }

            if (leftClick && showContextMenu)
            {
                if (contextMenuForEquippedFlashlight && contextThrowButtonRect.Contains(mouse.Position))
                {
                    FlashlightHand requestedHand = GetFlashlightHandFromSource(contextMenuItem.Source);
                    if (requestedHand != FlashlightHand.None)
                    {
                        pendingFlashlightThrowRequest = true;
                        pendingFlashlightThrowHand = requestedHand;
                        Console.WriteLine($"[INVENTORY] Flashlight throw requested ({requestedHand}).");
                    }
                    showContextMenu = false;
                    PlayUiSound(uiClickSound, 0.42f);
                }
                else if (contextMenuForEquippedFlashlight && contextToggleFlashlightButtonRect.Contains(mouse.Position))
                {
                    TryToggleFlashlight(contextMenuItem, unit);
                    showContextMenu = false;
                    PlayUiSound(uiClickSound, 0.5f);
                }
                else if (contextMenuForEquippedFlashlight && contextUnequipButtonRect.Contains(mouse.Position))
                {
                    RemoveItemFromSource(contextMenuItem, unit);

                    ItemSize size = ItemSizeDatabase.GetItemSize(contextMenuItem.Data.Name);
                    Point? freePos = inventoryGrid.FindFreePosition(size, true);
                    if (freePos.HasValue)
                    {
                        inventoryGrid.PlaceItem(new GridItem(contextMenuItem.Data, freePos.Value, size, false));
                    }
                    else
                    {
                        nearbyLootItems.Add(contextMenuItem.Data);
                    }

                    showContextMenu = false;
                    PlayUiSound(uiClickSound, 0.4f);
                }
                else if (!contextMenuForEquippedFlashlight && contextEquipButtonRect.Contains(mouse.Position))
                {
                    TryEquipByContext(contextMenuItem, unit);
                    showContextMenu = false;
                    PlayUiSound(uiClickSound, 0.4f);
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
                    PlayUiSound(uiClickSound, 0.5f);
                }
                else if (contextCloseButtonRect.Contains(mouse.Position))
                {
                    showContextMenu = false;
                    contextMenuForEquippedFlashlight = false;
                    PlayUiSound(uiClickSound, 0.4f);
                }
                else if (!contextMenuRect.Contains(mouse.Position))
                {
                    showContextMenu = false;
                    contextMenuForEquippedFlashlight = false;
                }
            }

            if (leftClick && showExaminePopup && !openedExaminePopupThisClick && !examinePopupRect.Contains(mouse.Position))
            {
                showExaminePopup = false;
                PlayUiSound(uiClickSound, 0.4f);
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
                if (info.Data.ArmorSlot == ArmorSlot.Neck && info.Source == "neck") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Torso && info.Source == "armor") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Shield && info.Source == "shield") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Shirt && info.Source == "shirt") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Pants && info.Source == "pants") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Knees && info.Source == "knees") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Feet && info.Source == "feet") return true;
                if (info.Data.ArmorSlot == ArmorSlot.ChestRig && info.Source == "chestrig") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Belt && info.Source == "belt") return true;
                if (info.Data.ArmorSlot == ArmorSlot.Backpack && info.Source == "backpack") return true;
            }

            if (info.Data.Type == ItemType.Accessory && (info.Source == "accessory" || IsHandFlashlightSource(info.Source)))
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
                    case ArmorSlot.Neck: target = GetNeckSlotBounds().Center; return true;
                    case ArmorSlot.Torso: target = GetArmorSlotBounds().Center; return true;
                    case ArmorSlot.Shield: target = GetShieldSlotBounds().Center; return true;
                    case ArmorSlot.Shirt: target = GetShirtSlotBounds().Center; return true;
                    case ArmorSlot.Pants: target = GetPantsSlotBounds().Center; return true;
                    case ArmorSlot.Knees: target = GetKneesSlotBounds().Center; return true;
                    case ArmorSlot.Feet: target = GetFeetSlotBounds().Center; return true;
                    case ArmorSlot.ChestRig: target = GetChestRigSlotBounds().Center; return true;
                    case ArmorSlot.Belt: target = GetBeltSlotBounds().Center; return true;
                    case ArmorSlot.Backpack: target = GetBackpackSlotBounds().Center; return true;
                }
            }

            if (data.Type == ItemType.Accessory)
            {
                if (IsTacticalFlashlight(data))
                {
                    if (unit.EquippedRightHandFlashlight == null)
                    {
                        target = GetWeaponSlotBounds().Center;
                        return true;
                    }

                    if (unit.EquippedLeftHandFlashlight == null)
                    {
                        target = GetShieldSlotBounds().Center;
                        return true;
                    }
                }

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

            if (GetWeaponSlotBounds().Contains(mousePos))
            {
                if (unit.EquippedWeapon != null) return new ItemContextInfo { Data = unit.EquippedWeapon.Data, Source = "weapon", Index = -1 };
                if (unit.EquippedRightHandFlashlight != null) return new ItemContextInfo { Data = unit.EquippedRightHandFlashlight.Data, Source = "rightflashlight", Index = -1 };
            }
            if (unit.EquippedHelmet != null && GetHelmetSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedHelmet.Data, Source = "helmet", Index = -1 };
            if (unit.EquippedNeck != null && GetNeckSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedNeck.Data, Source = "neck", Index = -1 };
            if (unit.EquippedArmor != null && GetArmorSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedArmor.Data, Source = "armor", Index = -1 };
            if (GetShieldSlotBounds().Contains(mousePos))
            {
                if (unit.EquippedShield != null) return new ItemContextInfo { Data = unit.EquippedShield.Data, Source = "shield", Index = -1 };
                if (unit.EquippedLeftHandFlashlight != null) return new ItemContextInfo { Data = unit.EquippedLeftHandFlashlight.Data, Source = "leftflashlight", Index = -1 };
            }
            if (unit.EquippedAccessory != null && GetBeltSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedAccessory.Data, Source = "accessory", Index = -1 };
            if (unit.EquippedShirt != null && GetShirtSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedShirt.Data, Source = "shirt", Index = -1 };
            if (unit.EquippedPants != null && GetPantsSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedPants.Data, Source = "pants", Index = -1 };
            if (unit.EquippedKnees != null && GetKneesSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedKnees.Data, Source = "knees", Index = -1 };
            if (unit.EquippedFeet != null && GetFeetSlotBounds().Contains(mousePos)) return new ItemContextInfo { Data = unit.EquippedFeet.Data, Source = "feet", Index = -1 };
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

            if (TryGetNearbyLootEntryAt(mousePos, out int lootIndex, out _))
            {
                return new ItemContextInfo
                {
                    Data = nearbyLootItems[lootIndex],
                    Source = "nearbyloot",
                    Index = lootIndex,
                    GridPosition = Point.Zero
                };
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
                case "neck": unit.EquippedNeck = null; break;
                case "armor": unit.EquippedArmor = null; break;
                case "shield": unit.EquippedShield = null; break;
                case "accessory": unit.EquippedAccessory = null; break;
                case "rightflashlight": unit.EquippedRightHandFlashlight = null; unit.IsRightHandFlashlightOn = false; break;
                case "leftflashlight": unit.EquippedLeftHandFlashlight = null; unit.IsLeftHandFlashlightOn = false; break;
                case "shirt": unit.EquippedShirt = null; break;
                case "pants": unit.EquippedPants = null; break;
                case "knees": unit.EquippedKnees = null; break;
                case "feet": unit.EquippedFeet = null; break;
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
                case "nearbyloot":
                    if (info.Index >= 0 && info.Index < nearbyLootItems.Count)
                        nearbyLootItems.RemoveAt(info.Index);
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
                case "neck": unit.EquippedNeck = restored; break;
                case "armor": unit.EquippedArmor = restored; break;
                case "shield": unit.EquippedShield = restored; break;
                case "accessory": unit.EquippedAccessory = restored; break;
                case "rightflashlight": unit.EquippedRightHandFlashlight = restored; unit.IsRightHandFlashlightOn = true; break;
                case "leftflashlight": unit.EquippedLeftHandFlashlight = restored; unit.IsLeftHandFlashlightOn = true; break;
                case "shirt": unit.EquippedShirt = restored; break;
                case "pants": unit.EquippedPants = restored; break;
                case "knees": unit.EquippedKnees = restored; break;
                case "feet": unit.EquippedFeet = restored; break;
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
                case "nearbyloot":
                    if (info.Index >= 0 && info.Index <= nearbyLootItems.Count)
                        nearbyLootItems.Insert(info.Index, info.Data);
                    else
                        nearbyLootItems.Add(info.Data);
                    ClampNearbyLootScroll();
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
            DrawWindow(inventoryWindow, $"APERCU UNITE - {selectedUnit.Name.ToUpper()}");
            DrawWindow(lootWindow, "LOOT A PROXIMITE");

            int gridStartX = GetGridStartX();
            int gridStartY = GetGridStartY();
            DrawUnitPreviewPanel(selectedUnit);

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

        private void DrawUnitPreviewPanel(Unit unit)
        {
            Rectangle content = GetInventoryContentBounds();
            Rectangle previewRect = GetPreviewViewportRect();

            spriteBatch.Draw(pixel, content, ParasiteEveTheme.BackgroundDark * 0.35f);
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, content, ParasiteEveTheme.BorderColor, 1);
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, previewRect, ParasiteEveTheme.BorderColor, 2);

            if (previewRenderTarget != null)
                spriteBatch.Draw(previewRenderTarget, previewRect, Color.White);

            float infoY = previewRect.Bottom + 12;
            float infoX = content.X + 16;
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Nom: {unit.Name}", new Vector2(infoX, infoY), ParasiteEveTheme.TextHighlight, 0.65f);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Classe: {unit.Class}", new Vector2(infoX, infoY + 22), ParasiteEveTheme.TextNormal, 0.62f);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Sante: {unit.Health}/{unit.GetMaxHealth()}", new Vector2(infoX, infoY + 44), ParasiteEveTheme.TextNormal, 0.62f);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"PA: {unit.ActionPoints}/{unit.MaxActionPoints}", new Vector2(infoX, infoY + 66), ParasiteEveTheme.TextNormal, 0.62f);

            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                "APERÇU 3D",
                new Vector2(content.X + 14, content.Y + 8),
                ParasiteEveTheme.TextDim,
                0.55f);
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                "Tourner: ← / → ou glisser souris",
                new Vector2(content.X + 14, previewRect.Bottom - 20),
                ParasiteEveTheme.TextDim,
                0.52f);
        }

        private void HandlePreviewRotation(MouseState mouse, MouseState previousMouse, KeyboardState keyboard)
        {
            if (keyboard.IsKeyDown(Keys.Left))
                previewRotation -= PreviewRotationSpeed * 0.016f;
            if (keyboard.IsKeyDown(Keys.Right))
                previewRotation += PreviewRotationSpeed * 0.016f;

            Rectangle previewRect = GetPreviewViewportRect();
            bool mouseInsidePreview = previewRect.Contains(mouse.Position);
            bool isMousePressed = mouse.LeftButton == ButtonState.Pressed;

            if (!isMousePressed)
            {
                isDraggingPreview = false;
            }
            else if (!isDraggingPreview && previousMouse.LeftButton != ButtonState.Pressed && mouseInsidePreview)
            {
                isDraggingPreview = true;
                lastDragMouseX = mouse.X;
            }

            if (isDraggingPreview)
            {
                int deltaX = mouse.X - lastDragMouseX;
                previewRotation += deltaX * PreviewMouseRotationSensitivity;
                lastDragMouseX = mouse.X;
            }
        }

        private Rectangle GetPreviewViewportRect()
        {
            Rectangle content = GetInventoryContentBounds();
            return new Rectangle(
                content.X + 18,
                content.Y + 26,
                content.Width - 36,
                Math.Max(180, content.Height - 170));
        }

        private void EnsurePreviewRenderTarget(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            if (previewRenderTarget != null && (previewRenderTarget.Width != width || previewRenderTarget.Height != height))
            {
                previewRenderTarget.Dispose();
                previewRenderTarget = null;
            }

            if (previewRenderTarget == null)
            {
                previewRenderTarget = new RenderTarget2D(
                    graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.Depth24);
            }
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
                    Rectangle contextImageRect = new Rectangle(contextMenuRect.Right - 92, contextMenuRect.Y + 38, 80, 80);
                    spriteBatch.Draw(pixel, contextImageRect, ParasiteEveTheme.BackgroundMedium * 0.9f);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextImageRect, ParasiteEveTheme.BorderColor, 1);
                    DrawItemPreviewImage(contextMenuItem.Data, contextImageRect);

                    string line1 = contextMenuItem.Data.Name;
                    string line2 = $"Type: {contextMenuItem.Data.Type}";
                    string line3 = $"Poids: {contextMenuItem.Data.WeightLbs:0.##} lbs";
                    string line4 = contextMenuItem.Data.Type == ItemType.Armor
                        ? $"Résistance éclats: {contextMenuItem.Data.FragmentationProtectionPercent}%"
                        : string.Empty;

                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line1,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 40), ParasiteEveTheme.TextHighlight, 0.68f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line2,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 62), ParasiteEveTheme.TextNormal, 0.62f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line3,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 82), ParasiteEveTheme.TextDim, 0.58f);

                    if (!string.IsNullOrEmpty(line4))
                    {
                        ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line4,
                            new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 102), ParasiteEveTheme.TextDim, 0.56f);
                    }
                }

                if (contextMenuForEquippedFlashlight)
                {
                    spriteBatch.Draw(pixel, contextThrowButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    spriteBatch.Draw(pixel, contextToggleFlashlightButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    spriteBatch.Draw(pixel, contextUnequipButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    spriteBatch.Draw(pixel, contextExamineButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);

                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextThrowButtonRect, ParasiteEveTheme.BorderColor, 1);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextToggleFlashlightButtonRect, ParasiteEveTheme.BorderColor, 1);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextUnequipButtonRect, ParasiteEveTheme.BorderColor, 1);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextExamineButtonRect, ParasiteEveTheme.BorderColor, 1);

                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "LANCER", new Vector2(contextThrowButtonRect.X + 8, contextThrowButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, contextFlashlightToggleLabel, new Vector2(contextToggleFlashlightButtonRect.X + 8, contextToggleFlashlightButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "DESEQUIPER", new Vector2(contextUnequipButtonRect.X + 8, contextUnequipButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EXAMINER", new Vector2(contextExamineButtonRect.X + 8, contextExamineButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                }
                else
                {
                    spriteBatch.Draw(pixel, contextEquipButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    spriteBatch.Draw(pixel, contextExamineButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextEquipButtonRect, ParasiteEveTheme.BorderColor, 1);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextExamineButtonRect, ParasiteEveTheme.BorderColor, 1);

                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EQUIPER", new Vector2(contextEquipButtonRect.X + 8, contextEquipButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EXAMINER", new Vector2(contextExamineButtonRect.X + 8, contextExamineButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                }

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
                DrawItemPreviewImage(examinedItemData, imageRect);
                if (GetItemPreviewTexture(examinedItemData) == null)
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "IMAGE", new Vector2(imageRect.X + 24, imageRect.Y + 40), ParasiteEveTheme.TextDim, 0.6f);

                float textY = examinePopupRect.Y + 52;
                float textX = imageRect.Right + 16;
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Type: {examinedItemData.Type}", new Vector2(textX, textY), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Poids: {examinedItemData.WeightLbs:0.##} lbs", new Vector2(textX, textY + 24), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Slots bonus: {examinedItemData.BonusInventorySlots}", new Vector2(textX, textY + 48), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Mobilite: -{examinedItemData.MobilityPenalty}", new Vector2(textX, textY + 72), ParasiteEveTheme.TextNormal, 0.7f);
                if (examinedItemData.Type == ItemType.Armor)
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Résistance éclats: {examinedItemData.FragmentationProtectionPercent}%", new Vector2(textX, textY + 96), ParasiteEveTheme.TextNormal, 0.7f);

                if (!string.IsNullOrWhiteSpace(examinedItemData.Description))
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, examinedItemData.Description, new Vector2(examinePopupRect.X + 16, examinePopupRect.Y + 190), ParasiteEveTheme.TextDim, 0.6f);

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

                if (item.Data.ArmorSlot == ArmorSlot.Neck && GetNeckSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetNeckSlotBounds();
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

                if (item.Data.ArmorSlot == ArmorSlot.Knees && GetKneesSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetKneesSlotBounds();
                    canEquip = true;
                    return true;
                }

                if (item.Data.ArmorSlot == ArmorSlot.Feet && GetFeetSlotBounds().Contains(mousePosition))
                {
                    previewRect = GetFeetSlotBounds();
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

        private FlashlightHand TryGetFlashlightHandForSlot(Point mousePosition)
        {
            if (GetWeaponSlotBounds().Contains(mousePosition))
                return FlashlightHand.Right;

            if (GetShieldSlotBounds().Contains(mousePosition))
                return FlashlightHand.Left;

            return FlashlightHand.None;
        }

        private void EquipFlashlightInHand(Unit unit, FlashlightHand hand, ItemData data)
        {
            Item flashlight = new Item(data, Point.Zero);
            if (hand == FlashlightHand.Right)
            {
                if (unit.EquippedWeapon != null)
                {
                    ReturnItemToGrid(unit.EquippedWeapon);
                    unit.EquippedWeapon = null;
                    unit.Weapon = string.Empty;
                    unit.WeaponData = null;
                }

                if (unit.EquippedRightHandFlashlight != null)
                    ReturnItemToGrid(unit.EquippedRightHandFlashlight);

                unit.EquippedRightHandFlashlight = flashlight;
                unit.IsRightHandFlashlightOn = true;
                return;
            }

            if (unit.EquippedShield != null)
            {
                ReturnItemToGrid(unit.EquippedShield);
                unit.EquippedShield = null;
            }

            if (unit.EquippedLeftHandFlashlight != null)
                ReturnItemToGrid(unit.EquippedLeftHandFlashlight);

            unit.EquippedLeftHandFlashlight = flashlight;
            unit.IsLeftHandFlashlightOn = true;
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
            Item rightHandItem = unit.EquippedWeapon ?? unit.EquippedRightHandFlashlight;
            bool highlightRightHand = isDragging && (draggedItem.Data.Type == ItemType.Weapon || IsTacticalFlashlight(draggedItem.Data));
            DrawEquipmentSlot(GetWeaponSlotBounds(), "RIGHT HAND", rightHandItem,
                highlightRightHand,
                labelOnLeft: true);

            Item leftHandItem = unit.EquippedShield ?? unit.EquippedLeftHandFlashlight;
            bool highlightLeftHand = isDragging && ((draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Shield) || IsTacticalFlashlight(draggedItem.Data));
            DrawEquipmentSlot(GetShieldSlotBounds(), "LEFT HAND", leftHandItem,
                highlightLeftHand,
                labelOnLeft: true);

            DrawEquipmentSlot(GetHelmetSlotBounds(), "HEAD", unit.EquippedHelmet,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Head,
                labelOnLeft: true);

            DrawEquipmentSlot(GetNeckSlotBounds(), "NECK", unit.EquippedNeck,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Neck,
                labelOnLeft: true);

            DrawEquipmentSlot(GetShirtSlotBounds(), "SUIT", unit.EquippedShirt,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Shirt,
                labelOnLeft: true);

            DrawEquipmentSlot(GetPantsSlotBounds(), "PANTS", unit.EquippedPants,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Pants,
                labelOnLeft: true);

            DrawEquipmentSlot(GetKneesSlotBounds(), "KNEES", unit.EquippedKnees,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Knees,
                labelOnLeft: true);

            DrawEquipmentSlot(GetFeetSlotBounds(), "FEET", unit.EquippedFeet,
                isDragging && draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Feet,
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
                draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Belt,
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

            nearbyLootSlotRects.Clear();
            nearbyLootSlotItemIndexes.Clear();

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

            int lootCellSize = LOOT_GRID_CELL_SIZE;
            int columnCount = Math.Max(1, (content.Width - 12) / lootCellSize);
            int visibleRows = Math.Max(1, (content.Height - LootHeaderTextHeight - LOOT_GRID_BOTTOM_INFO_HEIGHT) / lootCellSize);
            List<NearbyLootLayoutEntry> layout = BuildNearbyLootLayout(columnCount, out int totalRows);
            int maxScrollRows = Math.Max(0, totalRows - visibleRows);
            nearbyLootScrollRow = Math.Clamp(nearbyLootScrollRow, 0, maxScrollRows);

            Rectangle gridArea = new Rectangle(
                content.X + 6,
                content.Y + LootHeaderTextHeight,
                content.Width - 12,
                Math.Max(1, visibleRows * lootCellSize));

            DrawLootGridBackdrop(gridArea);
            Point alignedGridOrigin = GetAlignedLootGridOrigin(gridArea);

            int visibleStartRow = nearbyLootScrollRow;
            int visibleEndRow = nearbyLootScrollRow + visibleRows;

            foreach (NearbyLootLayoutEntry entry in layout)
            {
                if (entry.CellY + entry.CellHeight <= visibleStartRow || entry.CellY >= visibleEndRow)
                    continue;

                int itemIndex = entry.ItemIndex;
                int drawX = alignedGridOrigin.X + entry.CellX * lootCellSize;
                int drawY = alignedGridOrigin.Y + (entry.CellY - nearbyLootScrollRow) * lootCellSize;
                Rectangle lootSlot = new Rectangle(
                    drawX,
                    drawY,
                    Math.Max(1, entry.CellWidth * lootCellSize),
                    Math.Max(1, entry.CellHeight * lootCellSize));

                nearbyLootSlotRects.Add(lootSlot);
                nearbyLootSlotItemIndexes.Add(itemIndex);

                bool canPickup = inventoryGrid.FindFreePosition(ItemSizeDatabase.GetItemSize(nearbyLootItems[itemIndex].Name), true).HasValue;
                Color slotColor = canPickup ? ParasiteEveTheme.ButtonNormal * 0.28f : ParasiteEveTheme.TextDanger * 0.2f;
                spriteBatch.Draw(pixel, lootSlot, slotColor);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, lootSlot, ParasiteEveTheme.BorderColor, 1);

                DrawItemPreviewImage(nearbyLootItems[itemIndex], lootSlot);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    nearbyLootItems[itemIndex].Name,
                    new Vector2(lootSlot.X + 4, lootSlot.Y + 4),
                    ParasiteEveTheme.TextNormal,
                    0.4f);
            }

            if (maxScrollRows > 0)
            {
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                    $"Scroll: {nearbyLootScrollRow + 1}/{maxScrollRows + 1}",
                    new Vector2(content.X + 8, content.Bottom - 44),
                    ParasiteEveTheme.TextDim,
                    0.55f);
            }

            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font,
                "Cliquez un objet pour le ramasser.",
                new Vector2(content.X + 8, content.Bottom - 22),
                ParasiteEveTheme.TextDim,
                0.5f);

        }

        private void DrawLootGridBackdrop(Rectangle gridArea)
        {
            spriteBatch.Draw(pixel, gridArea, ParasiteEveTheme.BackgroundMedium * 0.28f);

            Point alignedGridOrigin = GetAlignedLootGridOrigin(gridArea);

            for (int x = alignedGridOrigin.X; x <= gridArea.Right; x += LOOT_GRID_CELL_SIZE)
                spriteBatch.Draw(pixel, new Rectangle(x, gridArea.Y, 1, gridArea.Height), ParasiteEveTheme.TextDim * 0.15f);

            for (int y = alignedGridOrigin.Y; y <= gridArea.Bottom; y += LOOT_GRID_CELL_SIZE)
                spriteBatch.Draw(pixel, new Rectangle(gridArea.X, y, gridArea.Width, 1), ParasiteEveTheme.TextDim * 0.15f);
        }

        private Point GetAlignedLootGridOrigin(Rectangle gridArea)
        {
            return new Point(
                gridArea.X + PositiveModulo(GetGridStartX() - gridArea.X, LOOT_GRID_CELL_SIZE),
                gridArea.Y + PositiveModulo(GetGridStartY() - gridArea.Y, LOOT_GRID_CELL_SIZE));
        }

        private void HandleNearbyLootScroll(MouseState mouse, MouseState previousMouse)
        {
            Rectangle lootWindow = GetLootPanelBounds();
            if (!lootWindow.Contains(mouse.Position))
                return;

            int scrollDelta = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
            if (scrollDelta == 0)
                return;

            int direction = Math.Sign(scrollDelta);
            nearbyLootScrollRow = Math.Max(0, nearbyLootScrollRow - direction);
            ClampNearbyLootScroll();
        }

        private void ClampNearbyLootScroll()
        {
            Rectangle lootWindow = GetLootPanelBounds();
            Rectangle content = new Rectangle(
                lootWindow.X + SECTION_PADDING,
                lootWindow.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                lootWindow.Width - SECTION_PADDING * 2,
                lootWindow.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);

            int columnCount = Math.Max(1, (content.Width - 12) / LOOT_GRID_CELL_SIZE);
            int visibleRows = Math.Max(1, (content.Height - LootHeaderTextHeight - LOOT_GRID_BOTTOM_INFO_HEIGHT) / LOOT_GRID_CELL_SIZE);
            BuildNearbyLootLayout(columnCount, out int totalRows);
            int maxScrollRows = Math.Max(0, totalRows - visibleRows);

            nearbyLootScrollRow = Math.Clamp(nearbyLootScrollRow, 0, maxScrollRows);
        }

        private List<NearbyLootLayoutEntry> BuildNearbyLootLayout(int columnCount, out int totalRows)
        {
            List<NearbyLootLayoutEntry> layout = new List<NearbyLootLayoutEntry>(nearbyLootItems.Count);
            int currentX = 0;
            int currentY = 0;
            int rowHeightInCells = 1;

            for (int i = 0; i < nearbyLootItems.Count; i++)
            {
                ItemSize size = ItemSizeDatabase.GetItemSize(nearbyLootItems[i].Name);
                int cellWidth = Math.Clamp(size.Width, 1, columnCount);
                int cellHeight = Math.Max(1, size.Height);

                if (currentX + cellWidth > columnCount)
                {
                    currentX = 0;
                    currentY += rowHeightInCells;
                    rowHeightInCells = 1;
                }

                layout.Add(new NearbyLootLayoutEntry
                {
                    ItemIndex = i,
                    CellX = currentX,
                    CellY = currentY,
                    CellWidth = cellWidth,
                    CellHeight = cellHeight
                });

                currentX += cellWidth;
                rowHeightInCells = Math.Max(rowHeightInCells, cellHeight);

                if (currentX >= columnCount)
                {
                    currentX = 0;
                    currentY += rowHeightInCells;
                    rowHeightInCells = 1;
                }
            }

            totalRows = Math.Max(1, currentY + rowHeightInCells);
            return layout;
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

            DrawItemPreviewImage(item.Data, item.PixelBounds, alpha);

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

                DrawItemPreviewImage(equippedItem.Data, inner);

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
            int minHeight = GetMainWindowHeight();
            int maxHeight = Math.Max(minHeight, graphicsDevice.Viewport.Height - 40);
            int desiredHeight = GetDesiredLootPanelHeight(width);
            int height = Math.Clamp(desiredHeight, minHeight, maxHeight);
            int y = graphicsDevice.Viewport.Height / 2 - height / 2;

            return new Rectangle(inventoryBounds.Right + PANEL_GAP, y, width, height);
        }

        private int GetDesiredLootPanelHeight(int panelWidth)
        {
            int contentWidth = Math.Max(1, panelWidth - SECTION_PADDING * 2);
            int columnCount = Math.Max(1, (contentWidth - 12) / LOOT_GRID_CELL_SIZE);
            BuildNearbyLootLayout(columnCount, out int totalRows);

            int contentHeight = LootHeaderTextHeight + LOOT_GRID_BOTTOM_INFO_HEIGHT + totalRows * LOOT_GRID_CELL_SIZE;
            return SECTION_HEADER_HEIGHT + SECTION_PADDING * 2 + contentHeight;
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

        private Rectangle GetNeckSlotBounds()
        {
            return GetMainEquipmentSlotBounds(2);
        }

        private Rectangle GetArmorSlotBounds()
        {
            return GetMainEquipmentSlotBounds(3);
        }

        private Rectangle GetShieldSlotBounds()
        {
            return GetMainEquipmentSlotBounds(4);
        }

        private Rectangle GetShirtSlotBounds()
        {
            return GetMainEquipmentSlotBounds(5);
        }

        private Rectangle GetPantsSlotBounds()
        {
            return GetMainEquipmentSlotBounds(6);
        }

        private Rectangle GetKneesSlotBounds()
        {
            return GetMainEquipmentSlotBounds(7);
        }

        private Rectangle GetFeetSlotBounds()
        {
            return GetMainEquipmentSlotBounds(8);
        }

        private Rectangle GetChestRigSlotBounds()
        {
            return GetMainEquipmentSlotBounds(9);
        }

        private Rectangle GetBeltSlotBounds()
        {
            return GetMainEquipmentSlotBounds(10);
        }

        private Rectangle GetBackpackSlotBounds()
        {
            return GetMainEquipmentSlotBounds(11);
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
