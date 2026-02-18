using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
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
        private const int CONTAINER_POPUP_WIDTH = 360;
        private const int CONTAINER_POPUP_HEIGHT = 320;
        private const int CONTAINER_POPUP_CELL_SIZE = 34;
        private const int LOOT_GRID_CELL_SIZE = CELL_SIZE;
        private const int LOOT_GRID_LABEL_HEIGHT = 22;
        private const int LOOT_GRID_BOTTOM_INFO_HEIGHT = 24;
        private const int LootHeaderTextHeight = 12;
        private const int UiSoundSampleRate = 22050;
        private const bool IsMainInventoryGridVisible = false;

        // ═══════════════════════════════════════════════════════════════════════
        // ÉTAT
        // ═══════════════════════════════════════════════════════════════════════

        private InventoryGrid inventoryGrid;
        private GridItem draggedItem = null;
        private bool draggedItemFromNearbyLoot = false;
        private Point dragGridOffset;
        private Point dragPixelOffset;
        private const int LOOT_GRID_MAX_ROWS = 40;
        private readonly InventoryGrid nearbyLootGrid;
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
        private Texture2D grapplingHookTexture;
        private Texture2D satchelChargeTexture;
        private Dictionary<string, Texture2D> armorTextures;
        private Dictionary<string, Texture2D> weaponTextures;
        private Dictionary<GrenadeType, Texture2D> grenadeTextures;
        private readonly Dictionary<string, Texture2D> generatedItemTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> generatedGrenadeTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
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

        private Unit activeUnit = null;

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
        private Rectangle contextOpenButtonRect;
        private Rectangle contextCloseButtonRect;
        private bool contextMenuForEquippedFlashlight = false;
        private bool contextMenuHasOpenAction = false;
        private string contextFlashlightToggleLabel = "ALLUMER/ETEINDRE";
        private bool pendingFlashlightThrowRequest = false;
        private FlashlightHand pendingFlashlightThrowHand = FlashlightHand.None;

        private bool showExaminePopup = false;
        private Rectangle examinePopupRect;
        private ItemData examinedItemData;
        private sealed class ContainerPopupState
        {
            public int Id;
            public Rectangle Rect;
            public List<GridItem> Items = new List<GridItem>();
            public InventoryGrid Grid;
            public ItemSize GridSize = new ItemSize(1, 1);
            public Rectangle GridRect;
            public string Title = string.Empty;
            public ItemContextInfo SourceInfo;
            public bool IsDragging;
            public Point DragOffset;
        }

        private readonly List<ContainerPopupState> containerPopups = new List<ContainerPopupState>();
        private int nextContainerPopupId = 1;
        private readonly List<Rectangle> nearbyLootSlotRects = new List<Rectangle>();
        private readonly List<GridItem> nearbyLootSlotItems = new List<GridItem>();
        private int nearbyLootScrollRow = 0;
        private Point draggedNearbyLootSourcePosition = Point.Zero;
        private bool hasDraggedNearbyLootSourcePosition = false;
        private ItemContextInfo draggedItemSourceInfo;
        private bool hasDraggedItemSourceInfo = false;

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

            bool added = TryPlaceItemInNearbyLootGrid(lootData, out _);
            if (added)
                ClampNearbyLootScroll();
            return added;
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
            nearbyLootGrid = new InventoryGrid(8, LOOT_GRID_MAX_ROWS);
            ItemDatabase = new Dictionary<string, ItemData>();

            flashlightTexture = CreateFlashlightIconTexture(new Color(140, 156, 170));
            grapplingHookTexture = CreateGrapplingHookIconTexture(new Color(110, 165, 188));
            satchelChargeTexture = CreateSatchelChargeIconTexture(new Color(138, 102, 86), new Color(178, 128, 108));
            armorTextures = LoadArmorTextures();
            weaponTextures = LoadWeaponTextures();
            grenadeTextures = LoadGrenadeTextures();

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

        private Dictionary<GrenadeType, Texture2D> LoadGrenadeTextures()
        {
            return new Dictionary<GrenadeType, Texture2D>
            {
                [GrenadeType.Frag] = CreateGrenadeIconTexture(new Color(82, 106, 76), new Color(115, 145, 102)),
                [GrenadeType.HE] = CreateGrenadeIconTexture(new Color(120, 86, 74), new Color(152, 114, 96)),
                [GrenadeType.Flashbang] = CreateGrenadeIconTexture(new Color(120, 124, 130), new Color(165, 170, 176)),
                [GrenadeType.Incendiary] = CreateGrenadeIconTexture(new Color(126, 78, 50), new Color(174, 102, 62)),
                [GrenadeType.EMP] = CreateGrenadeIconTexture(new Color(68, 96, 128), new Color(94, 130, 172)),
                [GrenadeType.Smoke] = CreateGrenadeIconTexture(new Color(84, 92, 100), new Color(122, 132, 142)),
                [GrenadeType.Plasma] = CreateGrenadeIconTexture(new Color(92, 76, 126), new Color(142, 118, 185))
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
            Color edge = Color.Lerp(accent, Color.White, 0.26f);
            Color shadow = Color.Lerp(inner, Color.Black, 0.34f);

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

            bool IsInside(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                    return false;

                Color pixel = data[y * width + x];
                return pixel != background;
            }

            void PaintTextureOverlay(int x0, int y0, int x1, int y1, float strength, int seed)
            {
                int minX = Math.Max(0, x0);
                int minY = Math.Max(0, y0);
                int maxX = Math.Min(width - 1, x1);
                int maxY = Math.Min(height - 1, y1);

                for (int y = minY; y <= maxY; y++)
                {
                    float verticalShade = MathHelper.Lerp(0.14f, -0.12f, (y - minY) / (float)Math.Max(1, maxY - minY));
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!IsInside(x, y))
                            continue;

                        float wave = MathF.Sin((x + seed) * 0.48f) * 0.05f + MathF.Cos((y - seed) * 0.37f) * 0.05f;
                        float hash = (((x + 11) * 73856093) ^ ((y + 7) * 19349663) ^ seed) & 255;
                        float microNoise = (hash / 255f - 0.5f) * 0.1f;
                        float shadeAmount = MathHelper.Clamp((verticalShade + wave + microNoise) * strength, -0.28f, 0.28f);
                        data[y * width + x] = Color.Lerp(data[y * width + x], shadeAmount > 0f ? edge : shadow, MathF.Abs(shadeAmount));
                    }
                }
            }

            void AddEdgeHighlights()
            {
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        if (!IsInside(x, y))
                            continue;

                        bool nearVoid = !IsInside(x - 1, y) || !IsInside(x + 1, y) || !IsInside(x, y - 1);
                        bool deepInside = IsInside(x - 1, y) && IsInside(x + 1, y) && IsInside(x, y + 1);

                        if (nearVoid)
                            data[y * width + x] = Color.Lerp(data[y * width + x], edge, 0.28f);
                        else if (deepInside && y > height / 2)
                            data[y * width + x] = Color.Lerp(data[y * width + x], shadow, 0.12f);
                    }
                }
            }

            void AddStitchLines(int x0, int y0, int x1, int y1, int spacing, bool vertical)
            {
                for (int i = vertical ? y0 : x0; i <= (vertical ? y1 : x1); i += Math.Max(2, spacing))
                {
                    int sx = vertical ? x0 : i;
                    int sy = vertical ? i : y0;
                    for (int j = 0; j < 2; j++)
                    {
                        int px = sx + (vertical ? j : 0);
                        int py = sy + (vertical ? 0 : j);
                        if (IsInside(px, py))
                            data[py * width + px] = Color.Lerp(data[py * width + px], edge, 0.45f);
                    }
                }
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
                    PaintTextureOverlay(10, 12, 54, 44, 0.95f, 91);
                    AddStitchLines(16, 38, 48, 38, 4, vertical: false);
                    break;

                case ArmorIconKind.Neck:
                    FillRect(20, 12, 44, 48);
                    FillRect(16, 20, 48, 30);
                    FillRect(24, 24, 40, 40);
                    for (int y = 26; y <= 36; y++)
                        for (int x = 26; x <= 38; x++)
                            data[y * width + x] = inner;
                    PaintTextureOverlay(16, 12, 48, 48, 0.8f, 37);
                    break;

                case ArmorIconKind.Vest:
                    FillRect(16, 10, 48, 52);
                    FillRect(24, 18, 40, 50);
                    for (int y = 24; y <= 46; y++)
                        for (int x = 26; x <= 38; x++)
                            data[y * width + x] = inner;
                    PaintTextureOverlay(16, 10, 48, 52, 1f, 13);
                    AddStitchLines(20, 16, 20, 50, 5, vertical: true);
                    AddStitchLines(44, 16, 44, 50, 5, vertical: true);
                    break;

                case ArmorIconKind.Shield:
                    for (int y = 8; y <= 56; y++)
                    {
                        int half = 20 - Math.Abs(y - 32) / 2;
                        for (int x = 32 - half; x <= 32 + half; x++)
                            data[y * width + x] = accent;
                    }
                    PaintTextureOverlay(12, 8, 52, 56, 0.72f, 74);
                    break;

                case ArmorIconKind.Shirt:
                    FillRect(18, 16, 46, 52);
                    FillRect(10, 18, 17, 34);
                    FillRect(47, 18, 54, 34);
                    PaintTextureOverlay(10, 16, 54, 52, 0.75f, 21);
                    break;

                case ArmorIconKind.Pants:
                    FillRect(22, 10, 42, 24);
                    FillRect(22, 24, 30, 54);
                    FillRect(34, 24, 42, 54);
                    PaintTextureOverlay(22, 10, 42, 54, 0.92f, 56);
                    AddStitchLines(31, 24, 31, 54, 3, vertical: true);
                    break;

                case ArmorIconKind.ChestRig:
                    FillRect(14, 14, 50, 50);
                    FillRect(14, 10, 50, 14);
                    for (int i = 0; i < 4; i++)
                        FillRect(18 + i * 8, 26, 22 + i * 8, 42);
                    PaintTextureOverlay(14, 10, 50, 50, 0.98f, 5);
                    break;

                case ArmorIconKind.Backpack:
                    FillRect(16, 10, 48, 54);
                    FillRect(22, 16, 42, 48);
                    for (int y = 16; y <= 48; y++)
                        for (int x = 22; x <= 42; x++)
                            data[y * width + x] = inner;
                    PaintTextureOverlay(16, 10, 48, 54, 0.86f, 63);
                    AddStitchLines(18, 18, 18, 48, 4, vertical: true);
                    AddStitchLines(46, 18, 46, 48, 4, vertical: true);
                    break;
            }

            AddEdgeHighlights();

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

        private Texture2D CreateFlashlightIconTexture(Color accent)
        {
            const int width = 64;
            const int height = 64;
            var data = new Color[width * height];

            Color background = Color.Lerp(ParasiteEveTheme.BackgroundDark, Color.Black, 0.35f);
            Color body = Color.Lerp(accent, Color.Black, 0.25f);
            Color metal = Color.Lerp(accent, Color.White, 0.25f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float grain = ((x * 43 + y * 97) % 17) / 16f;
                    data[y * width + x] = Color.Lerp(background, Color.Black, 0.12f * grain);
                }
            }

            void FillRect(int x0, int y0, int x1, int y1, Color color)
            {
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++)
                    for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                        data[y * width + x] = color;
            }

            FillRect(12, 24, 46, 40, body);
            FillRect(44, 22, 54, 42, metal);
            FillRect(14, 27, 40, 37, Color.Lerp(body, Color.Black, 0.35f));

            for (int rib = 16; rib <= 38; rib += 4)
                FillRect(rib, 24, rib, 40, Color.Lerp(metal, Color.White, 0.15f));

            for (int y = 20; y <= 44; y++)
            {
                int beamEnd = Math.Min(width - 1, 54 + (y > 32 ? (44 - y) : (y - 20)) / 2);
                for (int x = 54; x <= beamEnd; x++)
                    data[y * width + x] = Color.Lerp(new Color(250, 242, 190), data[y * width + x], 0.4f);
            }

            var texture = new Texture2D(graphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateGrenadeIconTexture(Color shell, Color highlight)
        {
            const int width = 64;
            const int height = 64;
            var data = new Color[width * height];

            Color background = Color.Lerp(ParasiteEveTheme.BackgroundDark, Color.Black, 0.3f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float grain = ((x * 31 + y * 53) % 23) / 22f;
                    data[y * width + x] = Color.Lerp(background, Color.Black, 0.15f * grain);
                }
            }

            void SetPixel(int x, int y, Color c)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                    data[y * width + x] = c;
            }

            for (int y = 16; y <= 52; y++)
            {
                float ny = (y - 34) / 18f;
                float radius = 16f * (float)Math.Sqrt(Math.Max(0f, 1f - ny * ny));
                int x0 = (int)Math.Round(32 - radius);
                int x1 = (int)Math.Round(32 + radius);
                for (int x = x0; x <= x1; x++)
                {
                    float nx = (x - 32) / Math.Max(1f, radius);
                    float shade = 0.55f + 0.45f * (1f - (nx + 1f) * 0.5f);
                    SetPixel(x, y, Color.Lerp(shell, highlight, shade * 0.45f));
                }
            }

            for (int y = 16; y <= 52; y += 6)
                for (int x = 18; x <= 46; x++)
                    SetPixel(x, y, Color.Lerp(shell, Color.Black, 0.4f));

            for (int x = 20; x <= 44; x += 6)
                for (int y = 17; y <= 51; y++)
                    SetPixel(x, y, Color.Lerp(shell, Color.Black, 0.35f));

            for (int y = 10; y <= 16; y++)
                for (int x = 27; x <= 37; x++)
                    SetPixel(x, y, new Color(120, 120, 120));

            for (int y = 8; y <= 10; y++)
                for (int x = 29; x <= 35; x++)
                    SetPixel(x, y, new Color(165, 165, 165));

            var texture = new Texture2D(graphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateGrapplingHookIconTexture(Color accent)
        {
            const int width = 64;
            const int height = 64;
            var data = new Color[width * height];

            Color background = Color.Lerp(ParasiteEveTheme.BackgroundDark, Color.Black, 0.36f);
            Color rope = new Color(174, 188, 198);
            Color metal = Color.Lerp(accent, Color.White, 0.35f);
            Color darkMetal = Color.Lerp(accent, Color.Black, 0.35f);

            void SetPixel(int x, int y, Color c)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                    data[y * width + x] = c;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float grain = ((x * 29 + y * 73) % 17) / 16f;
                    data[y * width + x] = Color.Lerp(background, Color.Black, 0.13f * grain);
                }
            }

            for (int y = 8; y <= 41; y++)
            {
                int sway = (int)MathF.Round(MathF.Sin((y - 8) * 0.24f) * 1.8f);
                SetPixel(32 + sway, y, rope);
                SetPixel(33 + sway, y, Color.Lerp(rope, Color.Black, 0.25f));
            }

            for (int y = 32; y <= 56; y++)
            {
                float ny = (y - 44) / 12f;
                float radius = 7f * (float)Math.Sqrt(Math.Max(0f, 1f - ny * ny));
                int x0 = (int)Math.Round(32 - radius);
                int x1 = (int)Math.Round(32 + radius);
                for (int x = x0; x <= x1; x++)
                    SetPixel(x, y, Color.Lerp(metal, darkMetal, (x - x0) / Math.Max(1f, x1 - x0)));
            }

            for (int offset = 0; offset <= 8; offset++)
            {
                SetPixel(24 - offset, 48 - offset, metal);
                SetPixel(23 - offset, 48 - offset, darkMetal);
                SetPixel(40 + offset, 48 - offset, metal);
                SetPixel(41 + offset, 48 - offset, darkMetal);
            }

            for (int x = 16; x <= 24; x++)
                SetPixel(x, 40, Color.Lerp(metal, Color.White, 0.2f));

            for (int x = 40; x <= 48; x++)
                SetPixel(x, 40, Color.Lerp(metal, Color.White, 0.2f));

            var texture = new Texture2D(graphicsDevice, width, height);
            texture.SetData(data);
            return texture;
        }

        private Texture2D CreateSatchelChargeIconTexture(Color bagColor, Color strapColor)
        {
            const int width = 64;
            const int height = 64;
            var data = new Color[width * height];

            Color background = Color.Lerp(ParasiteEveTheme.BackgroundDark, Color.Black, 0.34f);
            Color explosive = new Color(208, 78, 66);
            Color wire = new Color(210, 202, 140);

            void FillRect(int x0, int y0, int x1, int y1, Color color)
            {
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++)
                    for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                        data[y * width + x] = color;
            }

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    data[y * width + x] = background;

            FillRect(14, 20, 49, 48, bagColor);
            FillRect(16, 22, 47, 46, Color.Lerp(bagColor, Color.Black, 0.28f));

            FillRect(14, 20, 49, 22, strapColor);
            FillRect(29, 12, 34, 21, strapColor);
            FillRect(24, 10, 39, 13, Color.Lerp(strapColor, Color.White, 0.15f));

            FillRect(20, 28, 30, 40, explosive);
            FillRect(33, 28, 43, 40, explosive);

            FillRect(20, 27, 43, 28, Color.Lerp(explosive, Color.White, 0.2f));
            FillRect(26, 24, 38, 26, wire);
            FillRect(31, 18, 32, 24, wire);

            for (int x = 14; x <= 49; x++)
            {
                data[20 * width + x] = Color.Lerp(strapColor, Color.White, 0.25f);
                data[48 * width + x] = Color.Lerp(bagColor, Color.Black, 0.55f);
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

            return GetOrCreateGeneratedItemTexture(data);
        }

        private Texture2D GetOrCreateGeneratedItemTexture(ItemData data)
        {
            if (data == null)
                return null;

            string textureKey = !string.IsNullOrWhiteSpace(data.GeneratedTextureKey)
                ? data.GeneratedTextureKey
                : $"generated_{data.ArmorSlot}_{data.Name}";

            if (generatedItemTextures.TryGetValue(textureKey, out Texture2D existingTexture))
                return existingTexture;

            var slotColor = GetSlotAccentColor(data.ArmorSlot, data.Name);
            var kind = GetArmorIconKind(data.ArmorSlot);
            Texture2D generated = CreateArmorIconTexture(slotColor, kind);
            generatedItemTextures[textureKey] = generated;
            return generated;
        }

        private static ArmorIconKind GetArmorIconKind(ArmorSlot slot)
        {
            return slot switch
            {
                ArmorSlot.Head => ArmorIconKind.Helmet,
                ArmorSlot.Neck => ArmorIconKind.Neck,
                ArmorSlot.Torso => ArmorIconKind.Vest,
                ArmorSlot.Shield => ArmorIconKind.Shield,
                ArmorSlot.Shirt => ArmorIconKind.Shirt,
                ArmorSlot.Pants => ArmorIconKind.Pants,
                ArmorSlot.ChestRig => ArmorIconKind.ChestRig,
                ArmorSlot.Backpack => ArmorIconKind.Backpack,
                _ => ArmorIconKind.Vest
            };
        }

        private static Color GetSlotAccentColor(ArmorSlot slot, string itemName)
        {
            int seed = StringComparer.OrdinalIgnoreCase.GetHashCode(itemName ?? string.Empty);
            byte variation = (byte)(Math.Abs(seed) % 28);

            return slot switch
            {
                ArmorSlot.Head => new Color((byte)(95 + variation), (byte)(120 + variation), (byte)(80 + variation / 2)),
                ArmorSlot.Neck => new Color((byte)(100 + variation), (byte)(100 + variation), (byte)(100 + variation)),
                ArmorSlot.Torso => new Color((byte)(85 + variation), (byte)(105 + variation), (byte)(118 + variation / 2)),
                ArmorSlot.Shield => new Color((byte)(72 + variation), (byte)(88 + variation), (byte)(98 + variation / 2)),
                ArmorSlot.Shirt => new Color((byte)(108 + variation), (byte)(132 + variation), (byte)(95 + variation / 2)),
                ArmorSlot.Pants => new Color((byte)(70 + variation), (byte)(105 + variation), (byte)(125 + variation / 2)),
                ArmorSlot.ChestRig => new Color((byte)(105 + variation), (byte)(90 + variation), (byte)(74 + variation / 2)),
                ArmorSlot.Backpack => new Color((byte)(85 + variation), (byte)(110 + variation), (byte)(80 + variation / 2)),
                _ => new Color((byte)(90 + variation), (byte)(90 + variation), (byte)(90 + variation))
            };
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

        private Texture2D GetGrenadeTexture(ItemData data)
        {
            if (data?.Type != ItemType.Grenade)
                return null;

            if (data.GrenadeData?.Type == GrenadeType.SatchelC4 ||
                (data.Name?.Contains("Satchel", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (data.Name?.Contains("C4", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return satchelChargeTexture;
            }

            if (data.GrenadeData != null &&
                grenadeTextures != null &&
                grenadeTextures.TryGetValue(data.GrenadeData.Type, out Texture2D grenadeTexture))
            {
                return grenadeTexture;
            }

            if (!string.IsNullOrWhiteSpace(data.Name) &&
                data.Name.Contains("MK 2", StringComparison.OrdinalIgnoreCase) &&
                grenadeTextures != null &&
                grenadeTextures.TryGetValue(GrenadeType.Frag, out Texture2D mk2FallbackTexture))
            {
                return mk2FallbackTexture;
            }

            return GetOrCreateGeneratedGrenadeTexture(data);
        }

        private Texture2D GetOrCreateGeneratedGrenadeTexture(ItemData data)
        {
            if (data == null)
                return null;

            string textureKey = !string.IsNullOrWhiteSpace(data.GeneratedTextureKey)
                ? data.GeneratedTextureKey
                : $"generated_grenade_{data.Name}";

            if (generatedGrenadeTextures.TryGetValue(textureKey, out Texture2D existingTexture))
                return existingTexture;

            int seed = StringComparer.OrdinalIgnoreCase.GetHashCode(data.Name ?? string.Empty);
            byte variation = (byte)(Math.Abs(seed) % 35);
            Color shell = new Color((byte)(80 + variation), (byte)(95 + variation / 2), (byte)(76 + variation / 3));
            Color highlight = new Color((byte)(115 + variation), (byte)(130 + variation / 2), (byte)(102 + variation / 3));

            Texture2D generated = CreateGrenadeIconTexture(shell, highlight);
            generatedGrenadeTextures[textureKey] = generated;
            return generated;
        }

        private Texture2D GetAccessoryTexture(ItemData data)
        {
            if (IsTacticalFlashlight(data))
                return flashlightTexture;

            if (IsGrapplingHook(data))
                return grapplingHookTexture;

            return null;
        }

        private Texture2D GetItemPreviewTexture(ItemData data)
            => GetArmorTexture(data) ?? GetWeaponTexture(data) ?? GetGrenadeTexture(data) ?? GetAccessoryTexture(data);

        private void DrawItemPreviewImage(ItemData data, Rectangle targetRect, float alpha = 1f)
        {
            Texture2D previewTexture = GetItemPreviewTexture(data);
            if (previewTexture == null)
                return;

            Rectangle imageRect = new Rectangle(targetRect.X + 3, targetRect.Y + 3,
                Math.Max(1, targetRect.Width - 6), Math.Max(1, targetRect.Height - 6));

            if (data?.Type == ItemType.Weapon)
            {
                Color glowColor = GetWeaponAccentColor(data) * (0.2f * alpha);
                spriteBatch.Draw(pixel, imageRect, glowColor);

                Rectangle metalBand = new Rectangle(imageRect.X + 2, imageRect.Bottom - 10, Math.Max(1, imageRect.Width - 4), 6);
                spriteBatch.Draw(pixel, metalBand, new Color(70, 80, 95) * (0.65f * alpha));
            }

            spriteBatch.Draw(previewTexture, imageRect, Color.White * alpha);

            if (data?.Type == ItemType.Weapon)
            {
                Color accent = GetWeaponAccentColor(data) * (0.9f * alpha);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, imageRect, accent, 1);

                Rectangle diagonalAccent = new Rectangle(imageRect.X + 3, imageRect.Y + 3, Math.Max(1, imageRect.Width / 3), 2);
                spriteBatch.Draw(pixel, diagonalAccent, accent * 0.8f);
            }
        }

        private static string GetWeaponClassLabel(ItemData data)
        {
            string name = data?.Name ?? string.Empty;
            if (name.Contains("Sniper", StringComparison.OrdinalIgnoreCase)) return "SNIPER";
            if (name.Contains("Shotgun", StringComparison.OrdinalIgnoreCase)) return "SHOTGUN";
            if (name.Contains("SMG", StringComparison.OrdinalIgnoreCase) || name.Contains("MP5", StringComparison.OrdinalIgnoreCase)) return "SMG";
            if (name.Contains("Pistol", StringComparison.OrdinalIgnoreCase) || name.Contains("Glock", StringComparison.OrdinalIgnoreCase) || name.Contains("Beretta", StringComparison.OrdinalIgnoreCase)) return "PISTOL";
            if (name.Contains("Assault", StringComparison.OrdinalIgnoreCase) || name.Contains("M4", StringComparison.OrdinalIgnoreCase) || name.Contains("M16", StringComparison.OrdinalIgnoreCase) || name.Contains("AK", StringComparison.OrdinalIgnoreCase)) return "AR";
            return "RIFLE";
        }

        private static Color GetWeaponAccentColor(ItemData data)
        {
            string classLabel = GetWeaponClassLabel(data);
            return classLabel switch
            {
                "SNIPER" => new Color(110, 170, 210),
                "SHOTGUN" => new Color(185, 130, 95),
                "SMG" => new Color(120, 175, 140),
                "PISTOL" => new Color(165, 150, 210),
                "AR" => new Color(175, 155, 95),
                _ => new Color(130, 150, 165)
            };
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

        private static bool IsGrapplingHook(ItemData data)
        {
            return string.Equals(data?.Name, "Grappin tactique", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHandUtilityItem(ItemData data)
            => IsTacticalFlashlight(data) || IsGrapplingHook(data);

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
            ItemDatabase["Satchel Charge (C4)"] = new ItemData("Satchel Charge (C4)", grenadeDB["Satchel Charge (C4)"], 2.2046f, "Charge C4 (1x1) - télécommande à distance");
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
            ItemDatabase["Grappin tactique"] = new ItemData(
                "Grappin tactique",
                ItemType.Accessory,
                armorValue: 0,
                armorSlot: ArmorSlot.None,
                protectionLevel: ProtectionLevel.None,
                mobilityPenalty: 0,
                weightLbs: 2.2046f,
                bonusInventorySlots: 0,
                description: "Grappin tactique compact (1x1) - 1kg");

            string[] commonMagCalibers = { "9x19mm", "5.56x45mm", "7.62x39mm", "7.62x51mm NATO", "12 Gauge" };
            foreach (string caliber in commonMagCalibers)
            {
                string magName = $"Chargeur {caliber} (30)";
                ItemDatabase[magName] = new ItemData(
                    magName,
                    caliber,
                    ammoCount: caliber == "12 Gauge" ? 8 : 30,
                    weightLbs: caliber == "12 Gauge" ? 0.8f : 0.55f,
                    description: $"Chargeur compatible {caliber} (1x1).");
            }

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
                "Satchel Charge (C4)",
                "Lampe tactique aluminium",
                "Grappin tactique"
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

            activeUnit = selectedUnit;
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
            if (IsMainInventoryGridVisible && gridX >= 0 && gridX < GRID_WIDTH && gridY >= 0 && gridY < GRID_HEIGHT)
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
            hasDraggedItemSourceInfo = false;

            // Convertir position souris en position grille
            int gridX = (mouse.X - gridStartX) / CELL_SIZE;
            int gridY = (mouse.Y - gridStartY) / CELL_SIZE;

            // Vérifier clic dans la grille
            if (IsMainInventoryGridVisible && gridX >= 0 && gridX < GRID_WIDTH && gridY >= 0 && gridY < GRID_HEIGHT)
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
                    draggedItemSourceInfo = new ItemContextInfo { Data = clickedItem.Data, Source = "grid", GridPosition = clickedItem.GridPosition, Index = -1 };
                    hasDraggedItemSourceInfo = true;
                    inventoryGrid.RemoveItem(draggedItem);
                    PlayUiSound(uiClickSound, 0.45f);

                    Console.WriteLine($"[INVENTORY] Drag from grid: {draggedItem.Data.Name}");
                    return;
                }
            }

            if (TryStartDragFromNearbyLoot(mouse.Position))
                return;

            if (TryStartDragFromContainerPopup(mouse.Position))
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
                GridItem.ContainerPayload payload = new GridItem.ContainerPayload
                {
                    PantsItems = ClonePocketItems(unit.PantsInventory)
                };

                StartDragFromEquipment(unit.EquippedPants, mouse, pantsSlot, payload);
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
                    GridItem.ContainerPayload payload = new GridItem.ContainerPayload
                    {
                        BackpackItems = CloneGridItems(unit.BackpackInventory.GetAllItems())
                    };

                    StartDragFromEquipment(new Item(equippedBackpackData, Point.Zero), mouse, backpackMainSlot, payload);
                    unit.EquippedBackpack = null;
                    unit.EnsureBackpackInventoryGrid();
                    unit.BackpackInventory.Clear();
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
                GridItem.ContainerPayload payload = new GridItem.ContainerPayload
                {
                    ChestRigItems = ClonePocketItems(unit.ChestRigInventory)
                };

                StartDragFromEquipment(unit.EquippedChestRig, mouse, chestRigMainSlot, payload);
                unit.EquippedChestRig = null;
                unit.ChestRigInventory.Clear();
                unit.RefreshGrenadeInventoryFromEquipment();
                PlayUiSound(uiClickSound, 0.48f);

                Console.WriteLine($"[INVENTORY] Unequipped chest rig: {draggedItem.Data.Name}");
                return;
            }
        }

        private void StartDragFromEquipment(Item equippedItem, MouseState mouse, Rectangle sourceSlot)
            => StartDragFromEquipment(equippedItem, mouse, sourceSlot, null);

        private void StartDragFromEquipment(Item equippedItem, MouseState mouse, Rectangle sourceSlot, GridItem.ContainerPayload payload)
        {
            ItemSize size = ItemSizeDatabase.GetItemSize(equippedItem.Data.Name);
            draggedItem = new GridItem(equippedItem.Data, new Point(0, 0), size, false, payload);

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
                TryPlaceItemInNearbyLootGrid(draggedItem.Data, out _, draggedItem.Payload);
                draggedItemFromNearbyLoot = false;
                hasDraggedNearbyLootSourcePosition = false;
                PlayUiSound(uiClickSound, 0.5f);

                Console.WriteLine($"[INVENTORY] Dropped outside interface, sent to nearby loot: {draggedItem.Data.Name}");
                draggedItem = null;
                return;
            }

            // ✅ VÉRIFIER D'ABORD L'ÉQUIPEMENT (priorité absolue)
            bool equipped = TryEquipInSlot(mouse.Position, draggedItem, unit);

            if (!equipped)
            {
                if (TryPlaceDraggedItemInContainerPopup(mouse.Position))
                {
                    draggedItem = null;
                    draggedItemFromNearbyLoot = false;
                    hasDraggedNearbyLootSourcePosition = false;
                    return;
                }

                ItemContextInfo? closedContainerTarget = GetItemUnderMouse(mouse.Position, unit, gridStartX, gridStartY);
                if (closedContainerTarget.HasValue && TryAddItemToContainerSource(closedContainerTarget.Value, draggedItem, unit))
                {
                    PlayUiSound(uiClickSound, 0.5f);
                    draggedItem = null;
                    draggedItemFromNearbyLoot = false;
                    hasDraggedNearbyLootSourcePosition = false;
                    return;
                }

                if (lootWindow.Contains(mouse.Position))
                {
                    if (TryGetLootGridPlacement(mouse.Position, out Point lootGridPos) &&
                        nearbyLootGrid.CanPlaceItem(lootGridPos, draggedItem.GetCurrentSize()))
                    {
                        draggedItem.GridPosition = lootGridPos;
                        nearbyLootGrid.PlaceItem(new GridItem(draggedItem.Data, lootGridPos, draggedItem.Size, draggedItem.IsRotated, draggedItem.Payload));
                    }
                    else if (draggedItemFromNearbyLoot && hasDraggedNearbyLootSourcePosition &&
                             nearbyLootGrid.CanPlaceItem(draggedNearbyLootSourcePosition, draggedItem.GetCurrentSize()))
                    {
                        nearbyLootGrid.PlaceItem(new GridItem(draggedItem.Data, draggedNearbyLootSourcePosition, draggedItem.Size, draggedItem.IsRotated, draggedItem.Payload));
                    }
                    else
                    {
                        TryPlaceItemInNearbyLootGrid(draggedItem.Data, out _, draggedItem.Payload);
                    }

                    hasDraggedNearbyLootSourcePosition = false;
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

                if (IsMainInventoryGridVisible && gridArea.Contains(mouse.Position))
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
                                TryPlaceItemInNearbyLootGrid(draggedItem.Data, out _, draggedItem.Payload);
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
                    if (!IsMainInventoryGridVisible && draggedItemFromNearbyLoot)
                    {
                        TryPlaceItemInNearbyLootGrid(draggedItem.Data, out _, draggedItem.Payload);
                        ClampNearbyLootScroll();
                        PlayUiSound(uiErrorSound, 0.65f);

                        Console.WriteLine($"[INVENTORY] Main grid hidden. Item returned to nearby loot: {draggedItem.Data.Name}");
                        draggedItem = null;
                        draggedItemFromNearbyLoot = false;
                        hasDraggedNearbyLootSourcePosition = false;
                        return;
                    }

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
                            TryPlaceItemInNearbyLootGrid(draggedItem.Data, out _, draggedItem.Payload);
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
            hasDraggedNearbyLootSourcePosition = false;
        }

        private bool TryStartDragFromNearbyLoot(Point mousePosition)
        {
            if (!TryGetNearbyLootEntryAt(mousePosition, out GridItem lootItem, out Rectangle lootSlot))
                return false;

            ItemSize lootSize = lootItem.GetCurrentSize();
            draggedItem = new GridItem(lootItem.Data, Point.Zero, lootItem.Size, lootItem.IsRotated, lootItem.Payload);
            draggedItemSourceInfo = new ItemContextInfo { Data = lootItem.Data, Source = "nearbyloot", GridPosition = lootItem.GridPosition, Index = -1 };
            hasDraggedItemSourceInfo = true;
            nearbyLootGrid.RemoveItem(lootItem);
            draggedNearbyLootSourcePosition = lootItem.GridPosition;
            hasDraggedNearbyLootSourcePosition = true;
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
            if (!IsMainInventoryGridVisible)
                return false;

            if (!TryGetNearbyLootEntryAt(mousePosition, out GridItem lootItem, out _))
                return false;

            ItemSize lootSize = lootItem.GetCurrentSize();
            Point? freePos = inventoryGrid.FindFreePosition(lootSize, true);
            if (!freePos.HasValue)
            {
                PlayUiSound(uiErrorSound, 0.65f);

                Console.WriteLine($"[INVENTORY] Cannot pickup nearby loot (inventory full): {lootItem.Data.Name}");
                return true;
            }

            inventoryGrid.PlaceItem(new GridItem(lootItem.Data, freePos.Value, lootSize, lootItem.IsRotated, lootItem.Payload));
            nearbyLootGrid.RemoveItem(lootItem);
            ClampNearbyLootScroll();
            PlayUiSound(uiEquipSound, 0.58f);

            Console.WriteLine($"[INVENTORY] Picked nearby loot: {lootItem.Data.Name}");
            return true;
        }

        private bool TryGetNearbyLootEntryAt(Point mousePosition, out GridItem lootItem, out Rectangle lootSlot)
        {
            lootItem = null;
            lootSlot = Rectangle.Empty;

            for (int i = 0; i < nearbyLootSlotRects.Count; i++)
            {
                if (!nearbyLootSlotRects[i].Contains(mousePosition))
                    continue;

                if (i >= nearbyLootSlotItems.Count)
                    return false;

                lootItem = nearbyLootSlotItems[i];
                lootSlot = nearbyLootSlotRects[i];
                return lootItem != null;
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
            if (IsHandUtilityItem(item.Data) && flashlightHand != FlashlightHand.None)
            {
                if (flashlightHand == FlashlightHand.Left && IsTwoHandedWeapon(unit?.EquippedWeapon?.Data))
                    return false;

                EquipFlashlightInHand(unit, flashlightHand, item.Data);
                PlayUiSound(uiEquipSound, 0.6f);

                Console.WriteLine($"[INVENTORY] ✅ Equipped utility in {flashlightHand} hand: {item.Data.Name}");
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

                if (IsTwoHandedWeapon(item.Data))
                    UnequipLeftHand(unit);

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
                    if (IsTwoHandedWeapon(unit?.EquippedWeapon?.Data))
                        return false;

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
                        GridItem.ContainerPayload previousPayload = new GridItem.ContainerPayload
                        {
                            PantsItems = ClonePocketItems(unit.PantsInventory)
                        };
                        ReturnGridItemToGrid(new GridItem(unit.EquippedPants.Data, Point.Zero,
                            ItemSizeDatabase.GetItemSize(unit.EquippedPants.Data.Name), false, previousPayload));
                    }

                    unit.EquippedPants = new Item(item.Data, Point.Zero);
                    unit.PantsInventory = ClonePocketItems(item.Payload?.PantsItems) ?? new List<Item>();
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
                        GridItem.ContainerPayload previousPayload = new GridItem.ContainerPayload
                        {
                            ChestRigItems = ClonePocketItems(unit.ChestRigInventory)
                        };
                        ReturnGridItemToGrid(new GridItem(unit.EquippedChestRig.Data, Point.Zero,
                            ItemSizeDatabase.GetItemSize(unit.EquippedChestRig.Data.Name), false, previousPayload));
                    }

                    unit.EquippedChestRig = new Item(item.Data, Point.Zero);
                    unit.ChestRigInventory = ClonePocketItems(item.Payload?.ChestRigItems) ?? new List<Item>();
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
                        GridItem.ContainerPayload previousPayload = new GridItem.ContainerPayload
                        {
                            BackpackItems = CloneGridItems(unit.BackpackInventory.GetAllItems())
                        };
                        ReturnGridItemToGrid(new GridItem(previousBackpackData, Point.Zero,
                            ItemSizeDatabase.GetItemSize(previousBackpackData.Name), false, previousPayload));
                    }

                    unit.EquippedBackpack = item.Data.Name;
                    unit.EnsureBackpackInventoryGrid();
                    unit.BackpackInventory.Clear();
                    if (item.Payload?.BackpackItems != null)
                    {
                        foreach (GridItem backpackItem in item.Payload.BackpackItems)
                        {
                            GridItem restoredItem = backpackItem.Clone();
                            if (unit.BackpackInventory.CanPlaceItem(restoredItem.GridPosition, restoredItem.GetCurrentSize()))
                                unit.BackpackInventory.PlaceItem(restoredItem);
                        }
                    }
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

        private static bool IsTwoHandedWeapon(ItemData weaponData)
        {
            if (weaponData?.Type != ItemType.Weapon || weaponData.WeaponData == null)
                return false;

            return weaponData.WeaponData.Type switch
            {
                WeaponType.Pistol => false,
                WeaponType.Revolver => false,
                WeaponType.Melee => false,
                _ => true
            };
        }

        private void UnequipLeftHand(Unit unit)
        {
            if (unit == null)
                return;

            if (unit.EquippedShield != null)
            {
                ReturnItemToGrid(unit.EquippedShield);
                unit.EquippedShield = null;
            }

            if (unit.EquippedLeftHandFlashlight != null)
            {
                ReturnItemToGrid(unit.EquippedLeftHandFlashlight);
                unit.EquippedLeftHandFlashlight = null;
                unit.IsLeftHandFlashlightOn = false;
            }
        }

        private List<Item> ClonePocketItems(List<Item> items)
        {
            if (items == null)
                return null;

            List<Item> cloned = new List<Item>(items.Count);
            foreach (Item item in items)
                cloned.Add(item == null ? null : new Item(item.Data, Point.Zero));
            return cloned;
        }

        private List<GridItem> CloneGridItems(List<GridItem> items)
        {
            if (items == null)
                return null;

            List<GridItem> cloned = new List<GridItem>(items.Count);
            foreach (GridItem item in items)
                cloned.Add(item?.Clone());
            return cloned;
        }

        private void ReturnGridItemToGrid(GridItem item)
        {
            Point? freePos = inventoryGrid.FindFreePosition(item.GetCurrentSize(), true);
            if (!freePos.HasValue)
                return;

            item.GridPosition = freePos.Value;
            inventoryGrid.PlaceItem(item);
            PlayUiSound(uiClickSound, 0.42f);
            Console.WriteLine($"[INVENTORY] Returned old item to grid: {item.Data.Name}");
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

            HandleContainerPopup(mouse, leftClick, rightClick);

            if (rightClick)
            {
                var clickedItem = GetItemUnderMouse(mouse.Position, unit, gridStartX, gridStartY);
                if (clickedItem.HasValue)
                {
                    ItemContextInfo info = clickedItem.Value;
                    contextMenuItem = info;
                    contextMenuForEquippedFlashlight = IsEquippedTacticalFlashlight(info);
                    contextMenuHasOpenAction = TryBuildContainerPopupContent(info, unit, out _, out _, out _);
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
                        int buttonTop = contextMenuRect.Bottom - (contextMenuHasOpenAction ? 102 : 76);
                        if (contextMenuHasOpenAction)
                        {
                            contextOpenButtonRect = new Rectangle(contextMenuRect.X + 12, buttonTop, contextMenuRect.Width - 24, 22);
                            buttonTop += 26;
                        }

                        contextEquipButtonRect = new Rectangle(contextMenuRect.X + 12, buttonTop, contextMenuRect.Width - 24, 22);
                        contextExamineButtonRect = new Rectangle(contextMenuRect.X + 12, contextEquipButtonRect.Bottom + 4, contextMenuRect.Width - 24, 22);
                        contextCloseButtonRect = new Rectangle(contextMenuRect.X + 12, contextExamineButtonRect.Bottom + 4, contextMenuRect.Width - 24, 16);
                    }

                    showContextMenu = true;
                    showExaminePopup = false;
                    PlayUiSound(uiClickSound, 0.45f);
                }
                else
                {
                    showContextMenu = false;
                    contextMenuForEquippedFlashlight = false;
                    contextMenuHasOpenAction = false;
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
                        TryPlaceItemInNearbyLootGrid(contextMenuItem.Data, out _);
                    }

                    showContextMenu = false;
                    PlayUiSound(uiClickSound, 0.4f);
                }
                else if (!contextMenuForEquippedFlashlight && contextMenuHasOpenAction && contextOpenButtonRect.Contains(mouse.Position))
                {
                    if (TryOpenContainerPopup(contextMenuItem, unit, mouse.Position))
                    {
                        showContextMenu = false;
                        PlayUiSound(uiClickSound, 0.48f);
                    }
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
                    contextMenuHasOpenAction = false;
                    PlayUiSound(uiClickSound, 0.4f);
                }
                else if (!contextMenuRect.Contains(mouse.Position))
                {
                    showContextMenu = false;
                    contextMenuForEquippedFlashlight = false;
                    contextMenuHasOpenAction = false;
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

        private void HandleContainerPopup(MouseState mouse, bool leftClick, bool rightClick)
        {
            if (containerPopups.Count == 0)
                return;

            ContainerPopupState topPopup = GetTopPopupAt(mouse.Position);
            if (leftClick && topPopup != null)
            {
                BringPopupToFront(topPopup);
                Rectangle popupHeader = new Rectangle(topPopup.Rect.X, topPopup.Rect.Y, topPopup.Rect.Width, 30);
                Rectangle popupCloseButton = new Rectangle(topPopup.Rect.Right - 26, topPopup.Rect.Y + 4, 20, 20);

                if (popupCloseButton.Contains(mouse.Position))
                {
                    ClosePopup(topPopup);
                    return;
                }

                if (popupHeader.Contains(mouse.Position))
                {
                    topPopup.IsDragging = true;
                    topPopup.DragOffset = new Point(mouse.X - topPopup.Rect.X, mouse.Y - topPopup.Rect.Y);
                }
            }

            foreach (ContainerPopupState popup in containerPopups)
            {
                if (!popup.IsDragging)
                    continue;

                if (mouse.LeftButton == ButtonState.Pressed)
                {
                    int maxX = Math.Max(8, graphicsDevice.Viewport.Width - popup.Rect.Width - 8);
                    int maxY = Math.Max(8, graphicsDevice.Viewport.Height - popup.Rect.Height - 8);
                    int targetX = Math.Clamp(mouse.X - popup.DragOffset.X, 8, maxX);
                    int targetY = Math.Clamp(mouse.Y - popup.DragOffset.Y, 8, maxY);
                    popup.Rect = new Rectangle(targetX, targetY, popup.Rect.Width, popup.Rect.Height);
                    UpdateContainerPopupGridRect(popup);
                }
                else
                {
                    popup.IsDragging = false;
                }
            }

            if (rightClick && GetTopPopupAt(mouse.Position) == null)
            {
                foreach (ContainerPopupState popup in containerPopups)
                    popup.IsDragging = false;
            }
        }

        private bool TryOpenContainerPopup(ItemContextInfo info, Unit unit, Point openAnchor)
        {
            if (!TryBuildContainerPopupContent(info, unit, out string title, out ItemSize gridSize, out List<GridItem> gridItems))
                return false;

            ContainerPopupState popup = new ContainerPopupState
            {
                Id = nextContainerPopupId++,
                Title = title,
                GridSize = new ItemSize(Math.Max(1, gridSize.Width), Math.Max(1, gridSize.Height)),
                SourceInfo = info,
                Rect = BuildContainerPopupWindow(gridSize, openAnchor)
            };

            popup.Items.AddRange(gridItems);
            popup.Grid = new InventoryGrid(popup.GridSize.Width, popup.GridSize.Height);
            foreach (GridItem item in popup.Items)
            {
                if (item != null)
                    popup.Grid.PlaceItem(item);
            }

            UpdateContainerPopupGridRect(popup);
            containerPopups.Add(popup);
            return true;
        }

        private bool TryBuildContainerPopupContent(ItemContextInfo info, Unit unit, out string title, out ItemSize gridSize, out List<GridItem> items)
        {
            title = info.Data?.Name ?? "CONTENEUR";
            List<GridItem> popupItems = new List<GridItem>();
            gridSize = new ItemSize(1, 1);

            void AddPocketItems(List<Item> pocketItems, int width)
            {
                if (pocketItems == null)
                    return;

                int safeWidth = Math.Max(1, width);
                for (int i = 0; i < pocketItems.Count; i++)
                {
                    Item pocketItem = pocketItems[i];
                    if (pocketItem?.Data == null)
                        continue;

                    Point position = new Point(i % safeWidth, i / safeWidth);
                    popupItems.Add(new GridItem(pocketItem.Data, position, new ItemSize(1, 1), false));
                }
            }

            void AddGridItems(List<GridItem> gridItems)
            {
                if (gridItems == null)
                    return;

                foreach (GridItem item in gridItems)
                {
                    if (item?.Data == null)
                        continue;

                    popupItems.Add(new GridItem(item.Data, item.GridPosition, item.Size, item.IsRotated, item.Payload));
                }
            }

            switch (info.Source)
            {
                case "grid":
                    AddPocketItems(inventoryGrid.GetItemAt(info.GridPosition)?.Payload?.PantsItems, 2);
                    AddPocketItems(inventoryGrid.GetItemAt(info.GridPosition)?.Payload?.ChestRigItems, 2);
                    AddGridItems(inventoryGrid.GetItemAt(info.GridPosition)?.Payload?.BackpackItems);
                    break;
                case "nearbyloot":
                    AddPocketItems(nearbyLootGrid.GetItemAt(info.GridPosition)?.Payload?.PantsItems, 2);
                    AddPocketItems(nearbyLootGrid.GetItemAt(info.GridPosition)?.Payload?.ChestRigItems, 2);
                    AddGridItems(nearbyLootGrid.GetItemAt(info.GridPosition)?.Payload?.BackpackItems);
                    break;
                case "backpackutility":
                    unit.EnsureBackpackInventoryGrid();
                    AddPocketItems(unit.BackpackInventory.GetItemAt(info.GridPosition)?.Payload?.PantsItems, 2);
                    AddPocketItems(unit.BackpackInventory.GetItemAt(info.GridPosition)?.Payload?.ChestRigItems, 2);
                    AddGridItems(unit.BackpackInventory.GetItemAt(info.GridPosition)?.Payload?.BackpackItems);
                    break;
                case "containerpopup":
                    InventoryGrid sourcePopupGrid = GetContainerGridBySource(info);
                    AddPocketItems(sourcePopupGrid?.GetItemAt(info.GridPosition)?.Payload?.PantsItems, 2);
                    AddPocketItems(sourcePopupGrid?.GetItemAt(info.GridPosition)?.Payload?.ChestRigItems, 2);
                    AddGridItems(sourcePopupGrid?.GetItemAt(info.GridPosition)?.Payload?.BackpackItems);
                    break;
                case "pants":
                    AddPocketItems(unit.PantsInventory, 2);
                    break;
                case "chestrig":
                    AddPocketItems(unit.ChestRigInventory, 2);
                    break;
                case "backpack":
                    unit.EnsureBackpackInventoryGrid();
                    AddGridItems(unit.BackpackInventory.GetAllItems());
                    break;
            }

            int widthFromItem = GetContainerGridWidth(info.Data);
            int maxX = 0;
            int maxY = 0;
            foreach (GridItem popupItem in popupItems)
            {
                ItemSize size = popupItem.GetCurrentSize();
                maxX = Math.Max(maxX, popupItem.GridPosition.X + size.Width);
                maxY = Math.Max(maxY, popupItem.GridPosition.Y + size.Height);
            }

            int baseCapacity = GetContainerCellCapacity(info.Data);
            if (baseCapacity > 0)
            {
                int width = Math.Max(1, widthFromItem);
                int height = Math.Max(1, (int)Math.Ceiling(baseCapacity / (float)width));
                gridSize = new ItemSize(width, height);
            }
            else
            {
                gridSize = new ItemSize(Math.Max(1, Math.Max(widthFromItem, maxX)), Math.Max(1, maxY));
            }

            items = popupItems;

            if (items.Count > 0)
                return true;

            return baseCapacity > 0;
        }

        private static bool IsContainerData(ItemData data)
        {
            if (data == null)
                return false;

            if (data.ArmorSlot == ArmorSlot.Backpack ||
                data.ArmorSlot == ArmorSlot.ChestRig ||
                data.ArmorSlot == ArmorSlot.Pants)
                return true;

            return data.BonusInventorySlots > 0;
        }

        private bool TryStartDragFromContainerPopup(Point mousePosition)
        {
            ContainerPopupState targetPopup = null;
            for (int i = containerPopups.Count - 1; i >= 0; i--)
            {
                if (containerPopups[i].Grid != null && containerPopups[i].GridRect.Contains(mousePosition))
                {
                    targetPopup = containerPopups[i];
                    break;
                }
            }

            if (targetPopup == null)
                return false;

            int cellX = (mousePosition.X - targetPopup.GridRect.X) / CONTAINER_POPUP_CELL_SIZE;
            int cellY = (mousePosition.Y - targetPopup.GridRect.Y) / CONTAINER_POPUP_CELL_SIZE;
            GridItem popupItem = targetPopup.Grid.GetItemAt(new Point(cellX, cellY));
            if (popupItem == null)
                return false;

            ItemContextInfo pickedItemContext = new ItemContextInfo
            {
                Data = popupItem.Data,
                Source = "containerpopup",
                GridPosition = popupItem.GridPosition,
                Index = targetPopup.Id
            };

            bool hasOpenChild = containerPopups.Any(p =>
                string.Equals(p.SourceInfo.Source, "containerpopup", StringComparison.Ordinal) &&
                p.SourceInfo.Index == targetPopup.Id &&
                AreSameItemContext(p.SourceInfo, pickedItemContext));
            if (hasOpenChild)
                return false;

            ItemSize popupSize = popupItem.GetCurrentSize();
            Rectangle popupItemRect = new Rectangle(
                targetPopup.GridRect.X + popupItem.GridPosition.X * CONTAINER_POPUP_CELL_SIZE,
                targetPopup.GridRect.Y + popupItem.GridPosition.Y * CONTAINER_POPUP_CELL_SIZE,
                popupSize.Width * CONTAINER_POPUP_CELL_SIZE,
                popupSize.Height * CONTAINER_POPUP_CELL_SIZE);

            draggedItem = new GridItem(popupItem.Data, Point.Zero, popupItem.Size, popupItem.IsRotated, popupItem.Payload);
            draggedItemSourceInfo = pickedItemContext;
            hasDraggedItemSourceInfo = true;
            targetPopup.Grid.RemoveItem(popupItem);
            targetPopup.Items.Remove(popupItem);
            SyncContainerPopupItemsToSource(targetPopup);

            int maxWidth = popupSize.Width * CONTAINER_POPUP_CELL_SIZE - 1;
            int maxHeight = popupSize.Height * CONTAINER_POPUP_CELL_SIZE - 1;
            int offsetX = Math.Clamp(mousePosition.X - popupItemRect.X, 0, maxWidth);
            int offsetY = Math.Clamp(mousePosition.Y - popupItemRect.Y, 0, maxHeight);

            dragPixelOffset = new Point(offsetX, offsetY);
            dragGridOffset = new Point(offsetX / CONTAINER_POPUP_CELL_SIZE, offsetY / CONTAINER_POPUP_CELL_SIZE);
            draggedItemFromNearbyLoot = false;
            hasDraggedNearbyLootSourcePosition = false;
            PlayUiSound(uiClickSound, 0.45f);
            return true;
        }

        private bool TryPlaceDraggedItemInContainerPopup(Point mousePosition)
        {
            if (draggedItem == null)
                return false;

            ContainerPopupState targetPopup = null;
            for (int i = containerPopups.Count - 1; i >= 0; i--)
            {
                if (containerPopups[i].Grid != null && containerPopups[i].GridRect.Contains(mousePosition))
                {
                    targetPopup = containerPopups[i];
                    break;
                }
            }

            if (targetPopup == null)
                return false;

            if (IsDraggedItemCurrentlyOpenedContainer())
                return false;

            if (!CanMoveItemIntoContainer(targetPopup.SourceInfo, draggedItem, activeUnit))
                return false;

            if (IsSingleCellOnlyContainer(targetPopup.SourceInfo) && !IsSingleCellItem(draggedItem))
                return false;

            int popupGridX = (mousePosition.X - targetPopup.GridRect.X) / CONTAINER_POPUP_CELL_SIZE - dragGridOffset.X;
            int popupGridY = (mousePosition.Y - targetPopup.GridRect.Y) / CONTAINER_POPUP_CELL_SIZE - dragGridOffset.Y;
            Point popupPos = new Point(popupGridX, popupGridY);

            if (!targetPopup.Grid.CanPlaceItem(popupPos, draggedItem.GetCurrentSize()))
                return false;

            GridItem placedItem = new GridItem(draggedItem.Data, popupPos, draggedItem.Size, draggedItem.IsRotated, draggedItem.Payload);
            targetPopup.Grid.PlaceItem(placedItem);
            targetPopup.Items.Add(placedItem);
            SyncContainerPopupItemsToSource(targetPopup);
            PlayUiSound(uiClickSound, 0.5f);
            return true;
        }

        private void SyncContainerPopupItemsToSource(ContainerPopupState popup)
        {
            if (popup == null)
                return;

            ApplyContainerItemsToSource(popup.SourceInfo, popup.Items);
        }

        private void ApplyContainerItemsToSource(ItemContextInfo info, List<GridItem> popupItems)
        {
            if (popupItems == null)
                return;

            List<Item> pocketItems = popupItems
                .OrderBy(item => item.GridPosition.Y)
                .ThenBy(item => item.GridPosition.X)
                .Select(item => new Item(item.Data, Point.Zero))
                .ToList();

            List<GridItem> gridItems = popupItems
                .Select(item => new GridItem(item.Data, item.GridPosition, item.Size, item.IsRotated, item.Payload))
                .ToList();

            GridItem sourceGridItem = null;
            switch (info.Source)
            {
                case "grid":
                    sourceGridItem = inventoryGrid.GetItemAt(info.GridPosition);
                    break;
                case "nearbyloot":
                    sourceGridItem = nearbyLootGrid.GetItemAt(info.GridPosition);
                    break;
                case "backpackutility":
                    sourceGridItem = activeUnit?.BackpackInventory?.GetItemAt(info.GridPosition);
                    break;
                case "containerpopup":
                    sourceGridItem = GetContainerGridBySource(info)?.GetItemAt(info.GridPosition);
                    break;
            }

            if (sourceGridItem != null)
            {
                sourceGridItem.Payload ??= new GridItem.ContainerPayload();
                switch (sourceGridItem.Data?.ArmorSlot)
                {
                    case ArmorSlot.Pants:
                        sourceGridItem.Payload.PantsItems = pocketItems;
                        break;
                    case ArmorSlot.ChestRig:
                        sourceGridItem.Payload.ChestRigItems = pocketItems;
                        break;
                    case ArmorSlot.Backpack:
                        sourceGridItem.Payload.BackpackItems = gridItems;
                        break;
                }
                return;
            }

            if (activeUnit == null)
                return;

            switch (info.Source)
            {
                case "pants":
                    activeUnit.PantsInventory = pocketItems;
                    break;
                case "chestrig":
                    activeUnit.ChestRigInventory = pocketItems;
                    break;
                case "backpack":
                    activeUnit.EnsureBackpackInventoryGrid();
                    activeUnit.BackpackInventory.Clear();
                    foreach (GridItem item in gridItems)
                        activeUnit.BackpackInventory.PlaceItem(new GridItem(item.Data, item.GridPosition, item.Size, item.IsRotated, item.Payload));
                    break;
            }

            activeUnit.RefreshGrenadeInventoryFromEquipment();
        }

        private bool TryAddItemToContainerSource(ItemContextInfo info, GridItem itemToInsert, Unit unit)
        {
            if (itemToInsert?.Data == null || !IsContainerData(info.Data))
                return false;

            if (IsSingleCellOnlyContainer(info) && !IsSingleCellItem(itemToInsert))
                return false;

            if (!CanMoveItemIntoContainer(info, itemToInsert, unit))
                return false;

            if (!TryBuildContainerPopupContent(info, unit, out _, out ItemSize gridSize, out List<GridItem> existingItems))
                return false;

            InventoryGrid tempGrid = new InventoryGrid(Math.Max(1, gridSize.Width), Math.Max(1, gridSize.Height));
            List<GridItem> tempItems = new List<GridItem>();
            foreach (GridItem existing in existingItems)
            {
                if (existing == null)
                    continue;

                GridItem copy = new GridItem(existing.Data, existing.GridPosition, existing.Size, existing.IsRotated, existing.Payload);
                if (tempGrid.CanPlaceItem(copy.GridPosition, copy.GetCurrentSize()))
                {
                    tempGrid.PlaceItem(copy);
                    tempItems.Add(copy);
                }
            }

            Point? freePos = tempGrid.FindFreePosition(itemToInsert.GetCurrentSize(), true);
            if (!freePos.HasValue)
                return false;

            GridItem inserted = new GridItem(itemToInsert.Data, freePos.Value, itemToInsert.Size, itemToInsert.IsRotated, itemToInsert.Payload);
            tempGrid.PlaceItem(inserted);
            tempItems.Add(inserted);

            ApplyContainerItemsToSource(info, tempItems);

            foreach (ContainerPopupState popup in containerPopups)
            {
                if (!AreSameItemContext(popup.SourceInfo, info))
                    continue;

                popup.Items.Clear();
                popup.Items.AddRange(tempItems.Select(i => new GridItem(i.Data, i.GridPosition, i.Size, i.IsRotated, i.Payload)));
                popup.Grid = new InventoryGrid(Math.Max(1, gridSize.Width), Math.Max(1, gridSize.Height));
                foreach (GridItem popupItem in popup.Items)
                    popup.Grid.PlaceItem(popupItem);
            }

            unit.RefreshGrenadeInventoryFromEquipment();
            return true;
        }

        private static bool IsSingleCellOnlyContainer(ItemContextInfo info)
        {
            if (info.Data?.ArmorSlot == ArmorSlot.Pants)
                return true;

            return string.Equals(info.Source, "pants", StringComparison.Ordinal);
        }

        private static bool IsSingleCellItem(GridItem item)
        {
            if (item == null)
                return false;

            ItemSize size = item.GetCurrentSize();
            return size.Width == 1 && size.Height == 1;
        }

        private bool CanMoveItemIntoContainer(ItemContextInfo targetInfo, GridItem itemToInsert, Unit unit)
        {
            if (hasDraggedItemSourceInfo && AreSameItemContext(draggedItemSourceInfo, targetInfo))
                return false;

            if (IsDraggedItemCurrentlyOpenedContainer())
                return false;

            if (!IsContainerData(itemToInsert.Data))
                return true;

            if (!TryBuildContainerPopupContent(targetInfo, unit, out _, out _, out List<GridItem> targetItems))
                return true;

            string targetSignature = BuildContainerSignature(targetInfo.Data, targetItems);
            return !ContainsContainerSignature(itemToInsert, targetSignature, includeRoot: false);
        }

        private bool IsDraggedItemCurrentlyOpenedContainer()
        {
            if (draggedItem?.Data == null || !hasDraggedItemSourceInfo || !IsContainerData(draggedItem.Data))
                return false;

            return containerPopups.Any(popup => AreSameItemContext(draggedItemSourceInfo, popup.SourceInfo));
        }

        private static bool AreSameItemContext(ItemContextInfo a, ItemContextInfo b)
        {
            return string.Equals(a.Source, b.Source, StringComparison.Ordinal) &&
                   a.Index == b.Index &&
                   a.GridPosition == b.GridPosition &&
                   string.Equals(a.Data?.Name, b.Data?.Name, StringComparison.Ordinal);
        }

        private static string BuildContainerSignature(ItemData data, IEnumerable<GridItem> gridItems)
        {
            List<string> itemSignatures = new List<string>();
            if (gridItems != null)
            {
                foreach (GridItem gridItem in gridItems)
                    itemSignatures.Add(BuildGridItemSignature(gridItem));
            }

            itemSignatures.Sort(StringComparer.Ordinal);
            return $"{data?.Name ?? string.Empty}|{data?.ArmorSlot}|[{string.Join(";", itemSignatures)}]";
        }

        private static string BuildGridItemSignature(GridItem item)
        {
            if (item == null)
                return "null";

            List<string> childSignatures = new List<string>();
            if (item.Payload?.BackpackItems != null)
            {
                foreach (GridItem child in item.Payload.BackpackItems)
                    childSignatures.Add(BuildGridItemSignature(child));
            }

            childSignatures.Sort(StringComparer.Ordinal);
            return $"{item.Data?.Name ?? string.Empty}|{item.Data?.ArmorSlot}|[{string.Join(";", childSignatures)}]";
        }

        private static bool ContainsContainerSignature(GridItem root, string targetSignature, bool includeRoot = true)
        {
            if (root == null)
                return false;

            if (includeRoot && IsContainerData(root.Data))
            {
                string rootSignature = BuildGridItemSignature(root);
                if (string.Equals(rootSignature, targetSignature, StringComparison.Ordinal))
                    return true;
            }

            if (root.Payload?.BackpackItems == null)
                return false;

            foreach (GridItem child in root.Payload.BackpackItems)
            {
                if (ContainsContainerSignature(child, targetSignature))
                    return true;
            }

            return false;
        }

        private static int GetContainerGridWidth(ItemData data)
        {
            if (data == null)
                return 2;

            if (data.ArmorSlot == ArmorSlot.Backpack)
            {
                if (data.Name.Contains("Small", StringComparison.OrdinalIgnoreCase))
                    return 2;
                return 3;
            }

            if (data.ArmorSlot == ArmorSlot.ChestRig)
                return 2;

            if (data.ArmorSlot == ArmorSlot.Pants)
                return 2;

            return 2;
        }

        private static int GetContainerCellCapacity(ItemData data)
        {
            if (data == null)
                return 0;

            if (data.ArmorSlot == ArmorSlot.Backpack)
            {
                if (data.Name.Contains("XL", StringComparison.OrdinalIgnoreCase))
                    return 12;
                if (data.Name.Contains("Medium", StringComparison.OrdinalIgnoreCase))
                    return 8;
                if (data.Name.Contains("Small", StringComparison.OrdinalIgnoreCase))
                    return 4;
                return 6;
            }

            return Math.Max(0, data.BonusInventorySlots);
        }

        private Rectangle BuildContainerPopupWindow(ItemSize gridSize, Point openAnchor)
        {
            int width = gridSize.Width * CONTAINER_POPUP_CELL_SIZE + 24;
            int height = 30 + gridSize.Height * CONTAINER_POPUP_CELL_SIZE + 20;

            int maxX = Math.Max(8, graphicsDevice.Viewport.Width - width - 8);
            int maxY = Math.Max(8, graphicsDevice.Viewport.Height - height - 8);

            int x = Math.Clamp(openAnchor.X + 12, 8, maxX);
            int y = Math.Clamp(openAnchor.Y - 10, 8, maxY);

            return new Rectangle(x, y, width, height);
        }

        private void UpdateContainerPopupGridRect(ContainerPopupState popup)
        {
            if (popup == null)
                return;

            popup.GridRect = new Rectangle(
                popup.Rect.X + 12,
                popup.Rect.Y + 36,
                popup.GridSize.Width * CONTAINER_POPUP_CELL_SIZE,
                popup.GridSize.Height * CONTAINER_POPUP_CELL_SIZE);
        }

        private ContainerPopupState FindPopupById(int id)
        {
            return containerPopups.FirstOrDefault(p => p.Id == id);
        }

        private ContainerPopupState GetTopPopupAt(Point position)
        {
            for (int i = containerPopups.Count - 1; i >= 0; i--)
            {
                if (containerPopups[i].Rect.Contains(position))
                    return containerPopups[i];
            }

            return null;
        }

        private void BringPopupToFront(ContainerPopupState popup)
        {
            if (popup == null)
                return;

            int index = containerPopups.IndexOf(popup);
            if (index < 0 || index == containerPopups.Count - 1)
                return;

            containerPopups.RemoveAt(index);
            containerPopups.Add(popup);
        }

        private void ClosePopup(ContainerPopupState popup)
        {
            if (popup == null)
                return;

            for (int i = containerPopups.Count - 1; i >= 0; i--)
            {
                ContainerPopupState child = containerPopups[i];
                if (child.Id == popup.Id)
                    continue;

                if (string.Equals(child.SourceInfo.Source, "containerpopup", StringComparison.Ordinal) && child.SourceInfo.Index == popup.Id)
                    containerPopups.RemoveAt(i);
            }

            containerPopups.Remove(popup);
        }

        private InventoryGrid GetContainerGridBySource(ItemContextInfo info)
        {
            if (!string.Equals(info.Source, "containerpopup", StringComparison.Ordinal))
                return null;

            return FindPopupById(info.Index)?.Grid;
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
                if (IsHandUtilityItem(data))
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
            for (int i = containerPopups.Count - 1; i >= 0; i--)
            {
                ContainerPopupState popup = containerPopups[i];
                if (popup.Grid == null || !popup.GridRect.Contains(mousePos))
                    continue;

                int popupX = (mousePos.X - popup.GridRect.X) / CONTAINER_POPUP_CELL_SIZE;
                int popupY = (mousePos.Y - popup.GridRect.Y) / CONTAINER_POPUP_CELL_SIZE;
                GridItem popupItem = popup.Grid.GetItemAt(new Point(popupX, popupY));
                if (popupItem != null)
                {
                    return new ItemContextInfo
                    {
                        Data = popupItem.Data,
                        Source = "containerpopup",
                        GridPosition = popupItem.GridPosition,
                        Index = popup.Id
                    };
                }
            }

            int gridX = (mousePos.X - gridStartX) / CELL_SIZE;
            int gridY = (mousePos.Y - gridStartY) / CELL_SIZE;
            if (IsMainInventoryGridVisible && gridX >= 0 && gridX < GRID_WIDTH && gridY >= 0 && gridY < GRID_HEIGHT)
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

            if (TryGetNearbyLootEntryAt(mousePos, out GridItem nearbyLootItem, out _))
            {
                return new ItemContextInfo
                {
                    Data = nearbyLootItem.Data,
                    Source = "nearbyloot",
                    Index = -1,
                    GridPosition = nearbyLootItem.GridPosition
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
                case "containerpopup":
                    var sourcePopup = FindPopupById(info.Index);
                    var popupItem = sourcePopup?.Grid?.GetItemAt(info.GridPosition);
                    if (sourcePopup != null && popupItem != null)
                    {
                        sourcePopup.Grid.RemoveItem(popupItem);
                        sourcePopup.Items.Remove(popupItem);
                        SyncContainerPopupItemsToSource(sourcePopup);
                    }
                    break;
                case "nearbyloot":
                    var nearbyItem = nearbyLootGrid.GetItemAt(info.GridPosition);
                    if (nearbyItem != null)
                        nearbyLootGrid.RemoveItem(nearbyItem);
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
                case "containerpopup":
                    var sourcePopup = FindPopupById(info.Index);
                    var restoredPopupItem = new GridItem(info.Data, info.GridPosition, ItemSizeDatabase.GetItemSize(info.Data.Name), false);
                    if (sourcePopup != null && sourcePopup.Grid != null && sourcePopup.Grid.CanPlaceItem(restoredPopupItem.GridPosition, restoredPopupItem.GetCurrentSize()))
                    {
                        sourcePopup.Grid.PlaceItem(restoredPopupItem);
                        sourcePopup.Items.Add(restoredPopupItem);
                        SyncContainerPopupItemsToSource(sourcePopup);
                    }
                    else
                    {
                        ReturnItemToGrid(restored);
                    }
                    break;
                case "nearbyloot":
                    var restoredNearbyItem = new GridItem(info.Data, info.GridPosition, ItemSizeDatabase.GetItemSize(info.Data.Name), false);
                    if (nearbyLootGrid.CanPlaceItem(restoredNearbyItem.GridPosition, restoredNearbyItem.GetCurrentSize()))
                        nearbyLootGrid.PlaceItem(restoredNearbyItem);
                    else
                        TryPlaceItemInNearbyLootGrid(info.Data, out _);
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
                if (IsMainInventoryGridVisible && gridArea.Contains(mouse.Position))
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
                    TryGetEquipmentPreviewRect(selectedUnit, mouse.Position, draggedItem, out Rectangle equipmentPreviewRect, out bool canEquip))
                {
                    Color ghostColor = canEquip
                        ? ParasiteEveTheme.HoverOverlay * 0.6f
                        : ParasiteEveTheme.TextDanger * 0.4f;

                    Color borderColor = canEquip
                        ? ParasiteEveTheme.SelectionOutline * 0.5f
                        : ParasiteEveTheme.TextDanger * 0.8f;

                    spriteBatch.Draw(pixel, equipmentPreviewRect, ghostColor);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, equipmentPreviewRect, borderColor, 1);
                }

                if (lootWindow.Contains(mouse.Position) &&
                    TryGetLootGridPlacement(mouse.Position, out Point lootGridPos))
                {
                    Rectangle lootPanel = GetLootPanelBounds();
                    Rectangle lootContent = new Rectangle(
                        lootPanel.X + SECTION_PADDING,
                        lootPanel.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                        lootPanel.Width - SECTION_PADDING * 2,
                        lootPanel.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);

                    Rectangle lootGridArea = GetNearbyLootGridArea(lootContent, GetNearbyLootVisibleRows());
                    Point alignedLootOrigin = GetAlignedLootGridOrigin(lootGridArea);

                    Rectangle previewRect = new Rectangle(
                        alignedLootOrigin.X + lootGridPos.X * LOOT_GRID_CELL_SIZE,
                        alignedLootOrigin.Y + (lootGridPos.Y - nearbyLootScrollRow) * LOOT_GRID_CELL_SIZE,
                        draggedItem.GetCurrentSize().Width * LOOT_GRID_CELL_SIZE,
                        draggedItem.GetCurrentSize().Height * LOOT_GRID_CELL_SIZE);

                    bool canPlaceInLoot = nearbyLootGrid.CanPlaceItem(lootGridPos, draggedItem.GetCurrentSize());
                    Color ghostColor = canPlaceInLoot
                        ? ParasiteEveTheme.HoverOverlay * 0.6f
                        : ParasiteEveTheme.TextDanger * 0.4f;

                    Color borderColor = canPlaceInLoot
                        ? ParasiteEveTheme.SelectionOutline * 0.5f
                        : ParasiteEveTheme.TextDanger * 0.8f;

                    spriteBatch.Draw(pixel, previewRect, ghostColor);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, previewRect, borderColor, 1);
                }

                ContainerPopupState activePopup = null;
                for (int i = containerPopups.Count - 1; i >= 0; i--)
                {
                    if (containerPopups[i].GridRect.Contains(mouse.Position) && containerPopups[i].Grid != null)
                    {
                        activePopup = containerPopups[i];
                        break;
                    }
                }

                if (activePopup != null)
                {
                    Rectangle activePopupGridRect = activePopup.GridRect;
                    InventoryGrid activePopupGrid = activePopup.Grid;
                    int popupGridX = (mouse.X - activePopupGridRect.X) / CONTAINER_POPUP_CELL_SIZE - dragGridOffset.X;
                    int popupGridY = (mouse.Y - activePopupGridRect.Y) / CONTAINER_POPUP_CELL_SIZE - dragGridOffset.Y;

                    Rectangle previewRect = new Rectangle(
                        activePopupGridRect.X + popupGridX * CONTAINER_POPUP_CELL_SIZE,
                        activePopupGridRect.Y + popupGridY * CONTAINER_POPUP_CELL_SIZE,
                        draggedItem.GetCurrentSize().Width * CONTAINER_POPUP_CELL_SIZE,
                        draggedItem.GetCurrentSize().Height * CONTAINER_POPUP_CELL_SIZE);

                    bool canPlaceInContainer = activePopupGrid.CanPlaceItem(new Point(popupGridX, popupGridY), draggedItem.GetCurrentSize());
                    if (canPlaceInContainer && IsSingleCellOnlyContainer(activePopup.SourceInfo) && !IsSingleCellItem(draggedItem))
                        canPlaceInContainer = false;
                    Color ghostColor = canPlaceInContainer
                        ? ParasiteEveTheme.HoverOverlay * 0.6f
                        : ParasiteEveTheme.TextDanger * 0.4f;
                    Color borderColor = canPlaceInContainer
                        ? ParasiteEveTheme.SelectionOutline * 0.5f
                        : ParasiteEveTheme.TextDanger * 0.8f;

                    spriteBatch.Draw(pixel, previewRect, ghostColor);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, previewRect, borderColor, 1);
                }
            }

            // ✅ Texte d'aide avec ombre
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "DOUBLE CLICK: EQUIP | RIGHT CLICK: ACTIONS | R: ROTATE",
                new Vector2(inventoryWindow.X, inventoryWindow.Bottom + 8), ParasiteEveTheme.TextWarning, 0.8f);

            DrawContextMenuAndExamine();

            // ✅ Item en cours de drag (avec transparence)
            // Doit être rendu en dernier pour rester visible au-dessus des fenêtres contextuelles/popup.
            if (draggedItem != null)
            {
                DrawGridItem(draggedItem, 0.7f);
            }
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

        private void DrawContextMenuLayer()
        {
            if (!showContextMenu)
                return;

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
                DrawItemComparisonIndicators(contextMenuItem.Data, contextImageRect, 0.95f);

                string line1 = contextMenuItem.Data.Name;
                string line2 = $"Type: {contextMenuItem.Data.Type}";
                string line3 = $"Poids: {contextMenuItem.Data.WeightLbs:0.##} lbs";
                string line4 = contextMenuItem.Data.Type == ItemType.Armor
                    ? $"Résistance éclats: {contextMenuItem.Data.GetEffectiveFragmentationProtectionPercent()}%"
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
                if (contextMenuHasOpenAction)
                {
                    spriteBatch.Draw(pixel, contextOpenButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextOpenButtonRect, ParasiteEveTheme.BorderColor, 1);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "OUVRIR", new Vector2(contextOpenButtonRect.X + 8, contextOpenButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                }

                spriteBatch.Draw(pixel, contextEquipButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                spriteBatch.Draw(pixel, contextExamineButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextEquipButtonRect, ParasiteEveTheme.BorderColor, 1);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextExamineButtonRect, ParasiteEveTheme.BorderColor, 1);

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EQUIPER", new Vector2(contextEquipButtonRect.X + 8, contextEquipButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EXAMINER", new Vector2(contextExamineButtonRect.X + 8, contextExamineButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
            }

            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "Fermer", new Vector2(contextCloseButtonRect.X, contextCloseButtonRect.Y - 2), ParasiteEveTheme.TextWarning, 0.58f);

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
                    DrawItemComparisonIndicators(contextMenuItem.Data, contextImageRect, 0.95f);

                    string line1 = contextMenuItem.Data.Name;
                    string line2 = $"Type: {contextMenuItem.Data.Type}";
                    string line3 = $"Poids: {contextMenuItem.Data.WeightLbs:0.##} lbs";
                    string line4 = contextMenuItem.Data.Type == ItemType.Armor
                        ? $"Résistance éclats: {contextMenuItem.Data.GetEffectiveFragmentationProtectionPercent()}%"
                        : string.Empty;
                    string advantagesLine = string.Empty;
                    string drawbacksLine = string.Empty;

                    if (TryGetArmorComparisonSummary(contextMenuItem.Data, out string advantages, out string drawbacks))
                    {
                        advantagesLine = $"Avantages: {advantages}";
                        drawbacksLine = $"Inconvénients: {drawbacks}";
                    }

                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line1,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 40), ParasiteEveTheme.TextHighlight, 0.68f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line2,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 62), ParasiteEveTheme.TextNormal, 0.62f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line3,
                        new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 82), ParasiteEveTheme.TextDim, 0.58f);

                    if (TryGetWeightDeltaText(contextMenuItem.Data, out string weightDeltaText, out Color weightDeltaColor))
                    {
                        float weightWidth = font.MeasureString(line3).X * 0.58f;
                        Vector2 weightDeltaPos = new Vector2(contextMenuRect.X + 12 + weightWidth + 6f, contextMenuRect.Y + 82);
                        ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, weightDeltaText, weightDeltaPos, weightDeltaColor, 0.58f);
                    }

                    if (!string.IsNullOrEmpty(line4))
                    {
                        Vector2 fragPos = new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 102);
                        float fragScale = 0.56f;
                        ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, line4, fragPos, ParasiteEveTheme.TextDim, fragScale);

                        if (TryGetFragmentationDeltaText(contextMenuItem.Data, out string fragDeltaText, out Color fragDeltaColor))
                        {
                            float line4Width = font.MeasureString(line4).X * fragScale;
                            Vector2 deltaPos = new Vector2(fragPos.X + line4Width + 6f, fragPos.Y);
                            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, fragDeltaText, deltaPos, fragDeltaColor, fragScale);
                        }

                        if (!string.IsNullOrEmpty(advantagesLine))
                            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, advantagesLine,
                                new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 122), Color.LimeGreen, 0.5f);

                        if (!string.IsNullOrEmpty(drawbacksLine))
                            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, drawbacksLine,
                                new Vector2(contextMenuRect.X + 12, contextMenuRect.Y + 138), Color.Red, 0.5f);
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
                    if (contextMenuHasOpenAction)
                    {
                        spriteBatch.Draw(pixel, contextOpenButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                        ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextOpenButtonRect, ParasiteEveTheme.BorderColor, 1);
                        ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "OUVRIR", new Vector2(contextOpenButtonRect.X + 8, contextOpenButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                    }

                    spriteBatch.Draw(pixel, contextEquipButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    spriteBatch.Draw(pixel, contextExamineButtonRect, ParasiteEveTheme.ButtonNormal * 0.7f);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextEquipButtonRect, ParasiteEveTheme.BorderColor, 1);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, contextExamineButtonRect, ParasiteEveTheme.BorderColor, 1);

                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EQUIPER", new Vector2(contextEquipButtonRect.X + 8, contextEquipButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "EXAMINER", new Vector2(contextExamineButtonRect.X + 8, contextExamineButtonRect.Y + 4), ParasiteEveTheme.TextNormal, 0.65f);
                }

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "Fermer", new Vector2(contextCloseButtonRect.X, contextCloseButtonRect.Y - 2), ParasiteEveTheme.TextWarning, 0.58f);
            }

            foreach (ContainerPopupState popup in containerPopups)
            {
                ParasiteEveTheme.DrawPanel(spriteBatch, pixel, popup.Rect);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, popup.Rect, ParasiteEveTheme.SelectionOutline, 1);

                Rectangle popupHeader = new Rectangle(popup.Rect.X, popup.Rect.Y, popup.Rect.Width, 30);
                ParasiteEveTheme.DrawSectionHeader(spriteBatch, pixel, font, popupHeader, $"CONTENU - {popup.Title.ToUpper()}");

                Rectangle popupCloseButton = new Rectangle(popup.Rect.Right - 26, popup.Rect.Y + 4, 20, 20);
                spriteBatch.Draw(pixel, popupCloseButton, ParasiteEveTheme.ButtonNormal * 0.85f);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, popupCloseButton, ParasiteEveTheme.BorderColor, 1);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "X", new Vector2(popupCloseButton.X + 6, popupCloseButton.Y + 1), ParasiteEveTheme.TextWarning, 0.62f);

                spriteBatch.Draw(pixel, popup.GridRect, ParasiteEveTheme.BackgroundMedium * 0.3f);
                for (int x = 0; x <= popup.GridSize.Width; x++)
                {
                    int drawX = popup.GridRect.X + x * CONTAINER_POPUP_CELL_SIZE;
                    spriteBatch.Draw(pixel, new Rectangle(drawX, popup.GridRect.Y, 1, popup.GridRect.Height), ParasiteEveTheme.TextDim * 0.2f);
                }

                for (int y = 0; y <= popup.GridSize.Height; y++)
                {
                    int drawY = popup.GridRect.Y + y * CONTAINER_POPUP_CELL_SIZE;
                    spriteBatch.Draw(pixel, new Rectangle(popup.GridRect.X, drawY, popup.GridRect.Width, 1), ParasiteEveTheme.TextDim * 0.2f);
                }

                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, popup.GridRect, ParasiteEveTheme.BorderColor, 1);

                foreach (GridItem popupItem in popup.Items)
                {
                    if (popupItem?.Data == null)
                        continue;

                    ItemSize popupSize = popupItem.GetCurrentSize();
                    Rectangle itemRect = new Rectangle(
                        popup.GridRect.X + popupItem.GridPosition.X * CONTAINER_POPUP_CELL_SIZE,
                        popup.GridRect.Y + popupItem.GridPosition.Y * CONTAINER_POPUP_CELL_SIZE,
                        popupSize.Width * CONTAINER_POPUP_CELL_SIZE,
                        popupSize.Height * CONTAINER_POPUP_CELL_SIZE);

                    spriteBatch.Draw(pixel, itemRect, ParasiteEveTheme.ButtonNormal * 0.72f);
                    ParasiteEveTheme.DrawBorder(spriteBatch, pixel, itemRect, ParasiteEveTheme.SelectionOutline * 0.85f, 1);

                    DrawItemPreviewImage(popupItem.Data, itemRect);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, popupItem.Data.Name,
                        new Vector2(itemRect.X + 4, itemRect.Y + 6), ParasiteEveTheme.TextNormal, 0.5f);
                }
            }

            DrawContextMenuLayer();

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
                string weightText = $"Poids: {examinedItemData.WeightLbs:0.##} lbs";
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, weightText, new Vector2(textX, textY + 24), ParasiteEveTheme.TextNormal, 0.7f);
                if (TryGetWeightDeltaText(examinedItemData, out string weightDeltaText, out Color weightDeltaColor))
                {
                    float weightWidth = font.MeasureString(weightText).X * 0.7f;
                    Vector2 weightDeltaPos = new Vector2(textX + weightWidth + 6f, textY + 24);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, weightDeltaText, weightDeltaPos, weightDeltaColor, 0.7f);
                }

                string slotBonusText = examinedItemData.BonusInventorySlots > 0
                    ? $"+{examinedItemData.BonusInventorySlots}"
                    : examinedItemData.BonusInventorySlots < 0
                        ? examinedItemData.BonusInventorySlots.ToString()
                        : "0";
                string mobilityText = examinedItemData.MobilityPenalty > 0
                    ? $"-{examinedItemData.MobilityPenalty}"
                    : "0";

                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Slots bonus: {slotBonusText}", new Vector2(textX, textY + 48), ParasiteEveTheme.TextNormal, 0.7f);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Mobilite: {mobilityText}", new Vector2(textX, textY + 72), ParasiteEveTheme.TextNormal, 0.7f);

                float detailsBottomY = textY + 96f;

                if (examinedItemData.Type == ItemType.Weapon && examinedItemData.WeaponData != null)
                {
                    string rpmText = $"Cadence: {examinedItemData.WeaponData.RoundsPerMinute} RPM";
                    string magText = $"Chargeur: {examinedItemData.WeaponData.EffectiveMagazineCapacity} balles";

                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, rpmText, new Vector2(textX, textY + 96), ParasiteEveTheme.TextNormal, 0.7f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, magText, new Vector2(textX, textY + 120), ParasiteEveTheme.TextNormal, 0.7f);

                    detailsBottomY = Math.Max(detailsBottomY, textY + 120f);
                }

                if (TryGetArmorComparisonSummary(examinedItemData, out string advantages, out string drawbacks))
                {
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Avantages: {advantages}", new Vector2(textX, textY + 120), Color.LimeGreen, 0.62f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, $"Inconvénients: {drawbacks}", new Vector2(textX, textY + 140), Color.Red, 0.62f);
                    detailsBottomY = Math.Max(detailsBottomY, textY + 140f);
                }

                if (examinedItemData.Type == ItemType.Armor)
                {
                    string fragText = $"Résistance éclats: {examinedItemData.GetEffectiveFragmentationProtectionPercent()}%";
                    Vector2 fragPos = new Vector2(textX, textY + 96);
                    float fragScale = 0.7f;
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, fragText, fragPos, ParasiteEveTheme.TextNormal, fragScale);

                    string nijText = $"Protection NIJ pondérée: {examinedItemData.GetNijProtectionPercent()}% (couverture {examinedItemData.GetEffectiveBodyCoveragePercent()}%)";
                    Vector2 nijPos = new Vector2(textX, textY + 116);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, nijText, nijPos, ParasiteEveTheme.TextDim, 0.58f);

                    if (TryGetFragmentationDeltaText(examinedItemData, out string fragDeltaText, out Color fragDeltaColor))
                    {
                        float fragWidth = font.MeasureString(fragText).X * fragScale;
                        Vector2 deltaPos = new Vector2(fragPos.X + fragWidth + 6f, fragPos.Y);
                        ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, fragDeltaText, deltaPos, fragDeltaColor, fragScale);
                    }

                    detailsBottomY = Math.Max(detailsBottomY, nijPos.Y);
                }

                if (!string.IsNullOrWhiteSpace(examinedItemData.Description))
                {
                    float descriptionY = Math.Max(examinePopupRect.Y + 190f, detailsBottomY + 24f);
                    ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, examinedItemData.Description, new Vector2(examinePopupRect.X + 16, descriptionY), ParasiteEveTheme.TextDim, 0.6f);
                }

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
            bool highlightRightHand = isDragging && (draggedItem.Data.Type == ItemType.Weapon || IsHandUtilityItem(draggedItem.Data));
            DrawEquipmentSlot(GetWeaponSlotBounds(), "RIGHT HAND", rightHandItem,
                highlightRightHand,
                labelOnLeft: true);

            Item leftHandItem = unit.EquippedShield ?? unit.EquippedLeftHandFlashlight;
            bool highlightLeftHand = isDragging && ((draggedItem.Data.Type == ItemType.Armor && draggedItem.Data.ArmorSlot == ArmorSlot.Shield) || IsHandUtilityItem(draggedItem.Data));
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

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            int chestRigCapacity = unit.GetChestRigInventoryCapacity();
            // Pas de ghost vert sur les emplacements utilitaires (poches / chest rig).
            bool highlightPocket = false;
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
                Rectangle utilitySlot = GetBackpackUtilitySlotByIndex(i, unit);
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
            nearbyLootSlotItems.Clear();

            spriteBatch.Draw(pixel, content, ParasiteEveTheme.BackgroundDark * 0.35f);
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, content, ParasiteEveTheme.BorderColor, 1);

            List<GridItem> lootItems = nearbyLootGrid.GetAllItems();
            int lootCellSize = LOOT_GRID_CELL_SIZE;
            int visibleRows = GetNearbyLootVisibleRows();
            int totalRows = GetNearbyLootUsedRows();
            int maxScrollRows = Math.Max(0, totalRows - visibleRows);
            nearbyLootScrollRow = Math.Clamp(nearbyLootScrollRow, 0, maxScrollRows);

            Rectangle gridArea = GetNearbyLootGridArea(content, visibleRows);

            DrawLootGridBackdrop(gridArea);
            Point alignedGridOrigin = GetAlignedLootGridOrigin(gridArea);

            if (lootItems.Count == 0)
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
            }

            foreach (GridItem entry in lootItems)
            {
                ItemSize size = entry.GetCurrentSize();
                if (entry.GridPosition.Y + size.Height <= nearbyLootScrollRow || entry.GridPosition.Y >= nearbyLootScrollRow + visibleRows)
                    continue;

                int drawX = alignedGridOrigin.X + entry.GridPosition.X * lootCellSize;
                int drawY = alignedGridOrigin.Y + (entry.GridPosition.Y - nearbyLootScrollRow) * lootCellSize;
                Rectangle lootSlot = new Rectangle(drawX, drawY, Math.Max(1, size.Width * lootCellSize), Math.Max(1, size.Height * lootCellSize));

                nearbyLootSlotRects.Add(lootSlot);
                nearbyLootSlotItems.Add(entry);

                bool canPickup = IsMainInventoryGridVisible && inventoryGrid.FindFreePosition(size, true).HasValue;
                Color slotColor = canPickup ? ParasiteEveTheme.ButtonNormal * 0.28f : ParasiteEveTheme.TextDanger * 0.2f;
                spriteBatch.Draw(pixel, lootSlot, slotColor);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, lootSlot, ParasiteEveTheme.BorderColor, 1);

                DrawItemPreviewImage(entry.Data, lootSlot);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, entry.Data.Name, new Vector2(lootSlot.X + 4, lootSlot.Y + 4), ParasiteEveTheme.TextNormal, 0.4f);
                DrawItemComparisonIndicators(entry.Data, lootSlot);
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
                "Glissez/deposez pour organiser le loot.",
                new Vector2(content.X + 8, content.Bottom - 22),
                ParasiteEveTheme.TextDim,
                0.5f);

        }

        private Rectangle GetNearbyLootGridArea(Rectangle content, int visibleRows)
        {
            int maxGridWidth = Math.Max(1, nearbyLootGrid.Width * LOOT_GRID_CELL_SIZE);
            int requestedWidth = Math.Max(1, content.Width - 12);
            int gridWidth = Math.Min(requestedWidth, maxGridWidth);

            return new Rectangle(
                content.X + 6,
                content.Y + LootHeaderTextHeight,
                gridWidth,
                Math.Max(1, visibleRows * LOOT_GRID_CELL_SIZE));
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

        private bool TryGetLootGridPlacement(Point mousePosition, out Point targetCell)
        {
            Rectangle lootWindow = GetLootPanelBounds();
            Rectangle content = new Rectangle(
                lootWindow.X + SECTION_PADDING,
                lootWindow.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                lootWindow.Width - SECTION_PADDING * 2,
                lootWindow.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);

            Rectangle gridArea = GetNearbyLootGridArea(content, GetNearbyLootVisibleRows());

            if (!gridArea.Contains(mousePosition))
            {
                targetCell = Point.Zero;
                return false;
            }

            Point aligned = GetAlignedLootGridOrigin(gridArea);
            int x = (mousePosition.X - aligned.X) / LOOT_GRID_CELL_SIZE - dragGridOffset.X;
            int y = (mousePosition.Y - aligned.Y) / LOOT_GRID_CELL_SIZE - dragGridOffset.Y + nearbyLootScrollRow;
            targetCell = new Point(x, y);
            return true;
        }

        private int GetNearbyLootColumnCount()
        {
            Rectangle lootWindow = GetLootPanelBounds();
            Rectangle content = new Rectangle(
                lootWindow.X + SECTION_PADDING,
                lootWindow.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                lootWindow.Width - SECTION_PADDING * 2,
                lootWindow.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);
            return Math.Max(1, (content.Width - 12) / LOOT_GRID_CELL_SIZE);
        }

        private int GetNearbyLootVisibleRows()
        {
            Rectangle lootWindow = GetLootPanelBounds();
            Rectangle content = new Rectangle(
                lootWindow.X + SECTION_PADDING,
                lootWindow.Y + SECTION_HEADER_HEIGHT + SECTION_PADDING,
                lootWindow.Width - SECTION_PADDING * 2,
                lootWindow.Height - SECTION_HEADER_HEIGHT - SECTION_PADDING * 2);
            return Math.Max(1, (content.Height - LootHeaderTextHeight - LOOT_GRID_BOTTOM_INFO_HEIGHT) / LOOT_GRID_CELL_SIZE);
        }

        private int GetNearbyLootUsedRows()
        {
            int usedRows = 1;
            foreach (GridItem item in nearbyLootGrid.GetAllItems())
            {
                ItemSize size = item.GetCurrentSize();
                usedRows = Math.Max(usedRows, item.GridPosition.Y + size.Height);
            }
            return usedRows;
        }

        private bool TryPlaceItemInNearbyLootGrid(ItemData itemData, out Point placedPosition, GridItem.ContainerPayload payload = null)
        {
            ItemSize size = ItemSizeDatabase.GetItemSize(itemData.Name);
            Point? freePos = nearbyLootGrid.FindFreePosition(size, true);
            if (!freePos.HasValue)
            {
                placedPosition = Point.Zero;
                return false;
            }

            var placedItem = new GridItem(itemData, freePos.Value, size, false, payload);
            nearbyLootGrid.PlaceItem(placedItem);
            placedPosition = freePos.Value;
            return true;
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
            int visibleRows = GetNearbyLootVisibleRows();
            int totalRows = GetNearbyLootUsedRows();
            int maxScrollRows = Math.Max(0, totalRows - visibleRows);
            nearbyLootScrollRow = Math.Clamp(nearbyLootScrollRow, 0, maxScrollRows);
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

            DrawItemPreviewImage(item.Data, item.PixelBounds, alpha);

            // Bordure colorée selon le type (via ta DB)
            Color typeColor = ItemSizeDatabase.GetItemColor(item.Data.Type) * alpha;
            ParasiteEveTheme.DrawBorder(spriteBatch, pixel, item.PixelBounds, typeColor, 1);

            // ✅ Nom de l'item avec ombre
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, item.Data.Name,
                new Vector2(item.PixelBounds.X + 4, item.PixelBounds.Y + 4),
                ParasiteEveTheme.TextNormal * alpha, 0.6f);

            // ✅ Stats en bas
            string info = item.Data.Type switch
            {
                ItemType.Weapon => $"DMG:{item.Data.WeaponData?.Damage}",
                ItemType.Magazine => $"AMMO:{item.Data.AmmoCount}",
                _ => $"ARM:{item.Data.ArmorValue}"
            };
            ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, info,
                new Vector2(item.PixelBounds.X + 4, item.PixelBounds.Bottom - 15),
                ParasiteEveTheme.TextHighlight * alpha, 0.4f);

            if (item.Data.Type == ItemType.Weapon)
            {
                string weaponClass = GetWeaponClassLabel(item.Data);
                Color accent = GetWeaponAccentColor(item.Data) * alpha;

                Vector2 badgeSize = font.MeasureString(weaponClass) * 0.36f;
                Rectangle badgeRect = new Rectangle(
                    item.PixelBounds.Right - (int)badgeSize.X - 10,
                    item.PixelBounds.Y + 4,
                    (int)badgeSize.X + 6,
                    11);
                spriteBatch.Draw(pixel, badgeRect, accent * 0.55f);
                ParasiteEveTheme.DrawBorder(spriteBatch, pixel, badgeRect, accent, 1);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, weaponClass,
                    new Vector2(badgeRect.X + 3, badgeRect.Y + 1), Color.White * alpha, 0.36f);

                string caliber = string.IsNullOrWhiteSpace(item.Data.WeaponData?.Caliber)
                    ? "CAL:?"
                    : $"CAL:{item.Data.WeaponData.Caliber}";
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, caliber,
                    new Vector2(item.PixelBounds.X + 4, item.PixelBounds.Bottom - 25),
                    accent * 0.95f, 0.33f);
            }

            DrawItemComparisonIndicators(item, alpha);
        }

        private void DrawItemComparisonIndicators(GridItem item, float alpha)
        {
            DrawItemComparisonIndicators(item?.Data, item?.PixelBounds ?? Rectangle.Empty, alpha);
        }

        private void DrawItemComparisonIndicators(ItemData itemData, Rectangle bounds, float alpha = 1f)
        {
            if (activeUnit == null || itemData == null || itemData.Type != ItemType.Armor || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            ItemData equippedData = GetComparableEquippedItemData(activeUnit, itemData);
            if (equippedData == null)
                return;

            int candidateFrag = Math.Max(0, itemData.GetEffectiveFragmentationProtectionPercent());
            int equippedFrag = Math.Max(0, equippedData.GetEffectiveFragmentationProtectionPercent());
            float candidateWeight = Math.Max(0f, itemData.WeightLbs);
            float equippedWeight = Math.Max(0f, equippedData.WeightLbs);

            bool hasAdvantage = candidateFrag > equippedFrag || candidateWeight < equippedWeight;
            bool hasDrawback = candidateFrag < equippedFrag || candidateWeight > equippedWeight;

            if (!hasAdvantage && !hasDrawback)
                return;

            if (hasDrawback)
            {
                Vector2 minusPos = new Vector2(bounds.X + 4, bounds.Bottom - 16);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "-", minusPos, Color.Red * alpha, 0.8f);
            }

            if (hasAdvantage)
            {
                Vector2 plusPos = new Vector2(bounds.Right - 12, bounds.Bottom - 16);
                ParasiteEveTheme.DrawTextWithShadow(spriteBatch, font, "+", plusPos, Color.LimeGreen * alpha, 0.8f);
            }
        }

        private ItemData GetComparableEquippedItemData(Unit unit, ItemData candidate)
        {
            if (unit == null || candidate == null || candidate.Type != ItemType.Armor)
                return null;

            return candidate.ArmorSlot switch
            {
                ArmorSlot.Head => unit.EquippedHelmet?.Data,
                ArmorSlot.Neck => unit.EquippedNeck?.Data,
                ArmorSlot.Torso => unit.EquippedArmor?.Data,
                ArmorSlot.Shield => unit.EquippedShield?.Data,
                ArmorSlot.Shirt => unit.EquippedShirt?.Data,
                ArmorSlot.Pants => unit.EquippedPants?.Data,
                ArmorSlot.Knees => unit.EquippedKnees?.Data,
                ArmorSlot.Feet => unit.EquippedFeet?.Data,
                ArmorSlot.ChestRig => unit.EquippedChestRig?.Data,
                ArmorSlot.Belt => unit.EquippedBelt?.Data,
                ArmorSlot.Backpack => GetEquippedBackpackData(unit),
                _ => null
            };
        }

        private ItemData GetEquippedBackpackData(Unit unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.EquippedBackpack))
                return null;

            return ItemDatabase.TryGetValue(unit.EquippedBackpack, out ItemData data) ? data : null;
        }

        private bool TryGetFragmentationDeltaText(ItemData candidateData, out string deltaText, out Color deltaColor)
        {
            deltaText = string.Empty;
            deltaColor = ParasiteEveTheme.TextDim;

            if (activeUnit == null || candidateData == null || candidateData.Type != ItemType.Armor)
                return false;

            ItemData equippedData = GetComparableEquippedItemData(activeUnit, candidateData);
            if (equippedData == null)
                return false;

            int candidateFrag = Math.Max(0, candidateData.GetEffectiveFragmentationProtectionPercent());
            int equippedFrag = Math.Max(0, equippedData.GetEffectiveFragmentationProtectionPercent());
            int delta = candidateFrag - equippedFrag;
            if (delta == 0)
                return false;

            bool positive = delta > 0;
            deltaText = positive ? $"(+{delta}%)" : $"({delta}%)";
            deltaColor = positive ? Color.LimeGreen : Color.Red;
            return true;
        }

        private bool TryGetWeightDeltaText(ItemData candidateData, out string deltaText, out Color deltaColor)
        {
            deltaText = string.Empty;
            deltaColor = ParasiteEveTheme.TextDim;

            if (activeUnit == null || candidateData == null || candidateData.Type != ItemType.Armor)
                return false;

            ItemData equippedData = GetComparableEquippedItemData(activeUnit, candidateData);
            if (equippedData == null)
                return false;

            float delta = candidateData.WeightLbs - equippedData.WeightLbs;
            if (Math.Abs(delta) < 0.01f)
                return false;

            bool lighter = delta < 0f;
            string signedDelta = lighter
                ? $"-{Math.Abs(delta):0.##} lbs"
                : $"+{delta:0.##} lbs";

            deltaText = $"({signedDelta})";
            deltaColor = lighter ? Color.LimeGreen : Color.Red;
            return true;
        }

        private bool TryGetArmorComparisonSummary(ItemData candidateData, out string advantages, out string drawbacks)
        {
            advantages = "-";
            drawbacks = "-";

            if (activeUnit == null || candidateData == null || candidateData.Type != ItemType.Armor)
                return false;

            ItemData equippedData = GetComparableEquippedItemData(activeUnit, candidateData);
            if (equippedData == null)
                return false;

            List<string> advantageList = new List<string>();
            List<string> drawbackList = new List<string>();

            int fragDelta = Math.Max(0, candidateData.GetEffectiveFragmentationProtectionPercent()) - Math.Max(0, equippedData.GetEffectiveFragmentationProtectionPercent());
            if (fragDelta > 0)
                advantageList.Add($"+{fragDelta}% éclats");
            else if (fragDelta < 0)
                drawbackList.Add($"{fragDelta}% éclats");

            float weightDelta = candidateData.WeightLbs - equippedData.WeightLbs;
            if (weightDelta < -0.01f)
                advantageList.Add($"-{Math.Abs(weightDelta):0.##} lbs");
            else if (weightDelta > 0.01f)
                drawbackList.Add($"+{weightDelta:0.##} lbs");

            if (advantageList.Count == 0 && drawbackList.Count == 0)
                return false;

            if (advantageList.Count > 0)
                advantages = string.Join(", ", advantageList);

            if (drawbackList.Count > 0)
                drawbacks = string.Join(", ", drawbackList);

            return true;
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
            int totalRows = GetNearbyLootUsedRows();
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
            return GetMainEquipmentSlotBounds(2);
        }

        private Rectangle GetNeckSlotBounds()
        {
            return GetMainEquipmentSlotBounds(3);
        }

        private Rectangle GetArmorSlotBounds()
        {
            return GetMainEquipmentSlotBounds(4);
        }

        private Rectangle GetShieldSlotBounds()
        {
            return GetMainEquipmentSlotBounds(1);
        }

        private Rectangle GetShirtSlotBounds()
        {
            return GetMainEquipmentSlotBounds(6);
        }

        private Rectangle GetPantsSlotBounds()
        {
            return GetMainEquipmentSlotBounds(8);
        }

        private Rectangle GetKneesSlotBounds()
        {
            return GetMainEquipmentSlotBounds(9);
        }

        private Rectangle GetFeetSlotBounds()
        {
            return GetMainEquipmentSlotBounds(10);
        }

        private Rectangle GetChestRigSlotBounds()
        {
            return GetMainEquipmentSlotBounds(5);
        }

        private Rectangle GetBeltSlotBounds()
        {
            return GetMainEquipmentSlotBounds(11);
        }

        private Rectangle GetBackpackSlotBounds()
        {
            return GetMainEquipmentSlotBounds(7);
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

        private Rectangle GetBackpackUtilitySlotByIndex(int index, Unit unit)
        {
            int row = index / BACKPACK_UTILITY_COLUMNS;
            int column = index % BACKPACK_UTILITY_COLUMNS;
            Rectangle backpackSlot = GetBackpackSlotBounds();
            int capacity = unit.GetBackpackInventoryCapacity();
            int totalRows = Math.Max(1, (capacity + BACKPACK_UTILITY_COLUMNS - 1) / BACKPACK_UTILITY_COLUMNS);
            int startX = backpackSlot.Right;
            int startY = backpackSlot.Bottom - totalRows * CELL_SIZE - (totalRows - 1) * UTILITY_SLOT_GAP;

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

            Rectangle firstSlot = GetBackpackUtilitySlotByIndex(0, unit);
            int rows = (capacity + BACKPACK_UTILITY_COLUMNS - 1) / BACKPACK_UTILITY_COLUMNS;
            int width = BACKPACK_UTILITY_COLUMNS * CELL_SIZE + (BACKPACK_UTILITY_COLUMNS - 1) * UTILITY_SLOT_GAP;
            int height = rows * CELL_SIZE + (rows - 1) * UTILITY_SLOT_GAP;
            return new Rectangle(firstSlot.X, firstSlot.Y, width, height);
        }

        private Rectangle GetBackpackGridCellBounds(Point gridPosition, Unit unit)
        {
            Rectangle firstSlot = GetBackpackUtilitySlotByIndex(0, unit);
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

            return GetBackpackUtilitySlotByIndex(utilityCount - 1, unit).Bottom;
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
