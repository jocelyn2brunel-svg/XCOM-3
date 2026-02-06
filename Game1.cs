using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using NVorbis.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    public class Game1 : Game
    {
        // NOUVEAU: Import pour créer une console
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        // --- Gestion graphique et rendu 3D ---
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont font;

        // --- Modèles et effets 3D ---
        private BasicEffect basicEffect;
        private BasicEffect texturedEffect;
        private Model cubeModel;
        private Model planeModel;

        // Primitives 3D personnalisées
        private VertexPositionColor[] cubeVertices;
        private short[] cubeIndices;
        private VertexPositionColor[] planeVertices;
        private short[] planeIndices;

        // Primitives avec texture
        private VertexPositionNormalTexture[] planeTexturedVertices;
        private short[] planeTexturedIndices;

        // Textures
        private Texture2D tileTexture;

        // --- Cycle jour/nuit ---
        private float timeOfDay = 0f;
        private float dayNightSpeed = 0.01f;
        private Color ambientLight = Color.White;
        private Color directionalLight = Color.White;

        // --- NOUVEAU: Système d'inventaire ---
        private bool showInventory = false;
        private Rectangle inventoryPanel;
        private Dictionary<string, ItemData> itemDatabase;
        private List<Item> availableItems = new List<Item>();
        private Item draggedItem = null;
        private Point dragOffset;

        // --- Caméra 3D ---
        private Vector3 cameraPosition;
        private Vector3 cameraTarget;
        private Vector2 cameraOffset = Vector2.Zero;
        private Matrix viewMatrix;
        private Matrix projectionMatrix;
        private float cameraAngle = MathHelper.PiOver4;
        private float cameraDistance = 30f;
        private float cameraHeight = 20f;
        private float cameraRotationSpeed = 1.5f;
        private float cameraMoveSpeed = 35f;

        // Zoom
        private float zoomLevel = 1f;
        private float minZoom = 0.3f;
        private float maxZoom = 3f;
        private float previousScrollValue = 0f;

        // --- Menu principal ---
        private List<Button> menuButtons;
        private MouseState previousMouseState;
        private List<Song> menuSongs;
        private Song currentSong;
        private Random random = new Random();

        // --- Menu de sélection de mission ---
        private List<Button> missionButtons;
        private string selectedMission = "";

        // --- Système de grenades ---
        private Dictionary<string, GrenadeData> grenadeDatabase;
        private List<GrenadeItem> availableGrenades = new List<GrenadeItem>();
        private ExplosionManager explosionManager;

        // Grenades en vol et explosions
        private List<Grenade> activeGrenades = new List<Grenade>();
        private List<Crater> craters = new List<Crater>();

        // Mode lancer de grenade
        private bool throwMode = false;
        private GrenadeData selectedGrenade = null;
        private Point throwTarget = new Point(-1, -1);
        private List<Point> throwableCells = new List<Point>();
        private List<Point> explosionPreview = new List<Point>();
        private List<Vector3> trajectoryPreview = new List<Vector3>();

        // Constantes
        private const int MaxThrowRange = 8;

        // --- États du jeu ---
        enum GameState { MainMenu, MissionSelect, Playing, OptionsMenu, GameOver }
        private GameState currentState = GameState.MainMenu;

        // --- Grille 3D ---
        private int cellSize = 2;
        private int gridWidth = 20;
        private int gridHeight = 15;
        private Point hoveredCell = new Point(-1, -1);

        // --- Murs sur les edges des cases ---
        private HashSet<WallSegment> wallSegments = new HashSet<WallSegment>();
        private EdgeWallGenerator edgeWallGenerator;

        // --- Options / Volume ---
        private List<Button> optionsButtons;
        private float musicVolume = 0.5f;
        private Rectangle volumeBar;
        private Rectangle volumeFill;
        private Rectangle volumeHandle;
        private bool draggingVolume = false;

        // --- Unités et combat ---
        private List<Unit> playerUnits = new List<Unit>();
        private List<Unit> enemyUnits = new List<Unit>();
        private Unit selectedUnit = null;
        private List<Point> cachedMovableCells = new();
        private List<Unit> savedPlayerUnits;
        private List<Unit> savedEnemyUnits;
        private bool hasSavedGame = false;

        // --- Boutons et UI de combat ---
        private Rectangle fireButton;
        private bool fireButtonHovered = false;
        private List<Unit> validFireTargets = new();
        private Unit selectedFireTarget = null;
        private Unit hoveredFireTarget = null;

        private List<Button> unitActionButtons = new List<Button>();
        private const int ActionButtonWidth = 100;
        private const int ActionButtonHeight = 36;

        // --- A* Pathfinding ---
        private List<Point> currentPath = new();
        private Dictionary<Point, int> pathCosts = new();

        private Dictionary<string, WeaponData> weaponDatabase;

        // --- Gestion des tours ---
        enum TurnState { PlayerTurn, EnemyTurn, Busy }
        private TurnState currentTurn = TurnState.PlayerTurn;
        private int enemyTurnIndex = 0;
        private bool isActionInProgress = false;

        // --- Bouton Fin du tour ---
        private Rectangle endTurnButton;
        private bool endTurnHovered = false;

        // --- Interface Fire Targets ---
        List<FireTargetUI> fireTargetsUI = new();
        private bool showFireTargets = false;

        // --- Entrées clavier ---
        KeyboardState previousKeyboardState;

        // --- Raycast pour sélection 3D ---
        private Texture2D pixel;

        private HumanoidModelAdvanced humanoidModel;


        public Game1()
        {
            // NOUVEAU: Créer une console Windows
            AllocConsole();

            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
        }

        protected override void Initialize()
        {
            CreateCubePrimitive();
            CreatePlanePrimitive();
            CreateTexturedPlanePrimitive();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            font = Content.Load<SpriteFont>("Arial");

            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            tileTexture = Content.Load<Texture2D>("TileParchment32x32");

            basicEffect = new BasicEffect(GraphicsDevice);
            basicEffect.VertexColorEnabled = true;
            basicEffect.LightingEnabled = true;
            basicEffect.EnableDefaultLighting();

            texturedEffect = new BasicEffect(GraphicsDevice);
            texturedEffect.TextureEnabled = true;
            texturedEffect.LightingEnabled = true;
            texturedEffect.EnableDefaultLighting();

            humanoidModel = new HumanoidModelAdvanced();

            projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f),
                GraphicsDevice.Viewport.AspectRatio,
                0.1f,
                1000f
            );

            string[] mainMenuLabels = { "New Game", "Continue", "Encyclopedia", "Options", "Quit" };
            menuButtons = mainMenuLabels
                .Select((text, i) => new Button(text, new Vector2(0, 100 + i * 28)))
                .ToList();

            string[] songFiles = { "menu_music_1", "menu_music_2", "menu_music_3", "menu_music_4" };
            menuSongs = songFiles.Select(f => Content.Load<Song>(f)).ToList();
            currentSong = menuSongs[random.Next(menuSongs.Count)];
            MediaPlayer.Play(currentSong);
            MediaPlayer.Volume = 0.5f;

            string[] optionsLabels = { "Music Volume +", "Music Volume -", "Back" };
            int[] optionsY = { 100, 156, 184 };
            optionsButtons = optionsLabels
                .Select((text, i) => new Button(text, new Vector2(0, optionsY[i])))
                .ToList();

            string[] missionLabels = { "Tutorial", "Survival", "Assault", "Defense", "Back" };
            missionButtons = missionLabels
                .Select((text, i) => new Button(text, new Vector2(0, 100 + i * 28)))
                .ToList();

            volumeBar = new Rectangle(0, 134, 200, 8);
            previousScrollValue = Mouse.GetState().ScrollWheelValue;

            Window.ClientSizeChanged += (s, e) =>
            {
                UpdateFireTargetsUIPositions();
                projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                    MathHelper.ToRadians(45f),
                    GraphicsDevice.Viewport.AspectRatio,
                    0.1f,
                    1000f
                );
            };

            InitializeWeapons();
            InitializeItems();
            InitializeInventoryItems();

            InitializeGrenades();
            explosionManager = new ExplosionManager(random);

            // Initialiser le générateur de murs sur edges
            edgeWallGenerator = new EdgeWallGenerator(random);
        }

        protected override void Update(GameTime gameTime)
        {
            MouseState mouse = Mouse.GetState();
            KeyboardState keyboard = Keyboard.GetState();
            bool leftClick = mouse.LeftButton == ButtonState.Pressed &&
                             previousMouseState.LeftButton == ButtonState.Released;
            bool escapePressed = keyboard.IsKeyDown(Keys.Escape) &&
                                 previousKeyboardState.IsKeyUp(Keys.Escape);

            // Touche I pour ouvrir/fermer l'inventaire
            bool iPressed = keyboard.IsKeyDown(Keys.I) && previousKeyboardState.IsKeyUp(Keys.I);
            if (iPressed && currentState == GameState.Playing && selectedUnit != null && selectedUnit.Team == Team.Player)
            {
                showInventory = !showInventory;
                if (!showInventory)
                {
                    draggedItem = null;
                }
            }

            UpdateGrenades(gameTime);

            menuButtons[1].IsEnabled = hasSavedGame;

            switch (currentState)
            {
                case GameState.MainMenu:
                    HandleMainMenu(mouse);
                    break;

                case GameState.MissionSelect:
                    HandleMissionSelect(mouse);
                    if (escapePressed) currentState = GameState.MainMenu;
                    break;

                case GameState.Playing:
                    if (showInventory)
                    {
                        HandleInventory(mouse, leftClick);
                        if (escapePressed) showInventory = false;
                    }
                    else
                    {
                        if (currentTurn == TurnState.PlayerTurn) HandlePlayerTurn(mouse, leftClick, keyboard);
                        else if (currentTurn == TurnState.EnemyTurn) UpdateEnemyTurn();

                        HandleCameraControls(keyboard, mouse, gameTime);
                        UpdateFiringAnimations(gameTime);
                        UpdateDayNightCycle(gameTime);

                        if (escapePressed)
                        {
                            hasSavedGame = true;
                            savedPlayerUnits = playerUnits.Select(u => new Unit(u)).ToList();
                            savedEnemyUnits = enemyUnits.Select(u => new Unit(u)).ToList();
                            currentState = GameState.MainMenu;
                        }
                    }
                    break;

                case GameState.OptionsMenu:
                    HandleOptionsMenu(mouse);
                    if (escapePressed) currentState = GameState.MainMenu;
                    break;

                // NOUVEAU: État Game Over
                case GameState.GameOver:
                    if (escapePressed || leftClick)
                    {
                        currentState = GameState.MainMenu;
                    }
                    break;


            }

            previousMouseState = mouse;
            previousKeyboardState = keyboard;

            base.Update(gameTime);
        }

        private void UpdateDayNightCycle(GameTime gameTime)
        {
            timeOfDay += (float)gameTime.ElapsedGameTime.TotalSeconds * dayNightSpeed;
            if (timeOfDay >= 1f) timeOfDay -= 1f;

            float sunIntensity = CalculateSunIntensity(timeOfDay);

            ambientLight = new Color(
                sunIntensity * 0.8f,
                sunIntensity * 0.85f,
                sunIntensity
            );

            directionalLight = new Color(
                sunIntensity,
                sunIntensity * 0.95f,
                sunIntensity * 0.9f
            );

            basicEffect.AmbientLightColor = ambientLight.ToVector3();
            basicEffect.DirectionalLight0.DiffuseColor = directionalLight.ToVector3();

            texturedEffect.AmbientLightColor = ambientLight.ToVector3();
            texturedEffect.DirectionalLight0.DiffuseColor = directionalLight.ToVector3();
        }

        private float CalculateSunIntensity(float time)
        {
            float intensity;

            if (time < 0.25f)
            {
                intensity = MathHelper.Lerp(0.3f, 0.7f, time / 0.25f);
            }
            else if (time < 0.5f)
            {
                intensity = MathHelper.Lerp(0.7f, 1.0f, (time - 0.25f) / 0.25f);
            }
            else if (time < 0.75f)
            {
                intensity = MathHelper.Lerp(1.0f, 0.7f, (time - 0.5f) / 0.25f);
            }
            else
            {
                intensity = MathHelper.Lerp(0.7f, 0.3f, (time - 0.75f) / 0.25f);
            }

            return intensity;
        }

        protected override void Draw(GameTime gameTime)
        {
            Color backgroundColor = GetSkyColor(timeOfDay);
            GraphicsDevice.Clear(backgroundColor);

            if (currentState == GameState.Playing)
            {
                UpdateCamera();

                basicEffect.View = viewMatrix;
                basicEffect.Projection = projectionMatrix;

                texturedEffect.View = viewMatrix;
                texturedEffect.Projection = projectionMatrix;

                RasterizerState rasterizerState = new RasterizerState();
                rasterizerState.CullMode = CullMode.None;
                GraphicsDevice.RasterizerState = rasterizerState;

                GraphicsDevice.DepthStencilState = DepthStencilState.Default;

                DrawGrid3D();
                DrawWalls3D();
                DrawUnits3D();
                DrawMovableCells3D(gameTime);
                DrawPath3D(gameTime);
                DrawHoveredCell3D(gameTime);
                DrawCraters3D();
                DrawGrenades3D();
                DrawThrowMode3D(gameTime);

            }

            _spriteBatch.Begin();
            switch (currentState)
            {
                case GameState.MainMenu:
                    _spriteBatch.DrawString(font, "XCOM 3", Vector2.Zero, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);
                    MouseState mouse = Mouse.GetState();
                    foreach (var button in menuButtons) button.Draw(_spriteBatch, font, mouse);
                    break;

                case GameState.MissionSelect:
                    _spriteBatch.DrawString(font, "Select Mission", Vector2.Zero, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);
                    mouse = Mouse.GetState();
                    foreach (var button in missionButtons) button.Draw(_spriteBatch, font, mouse);
                    break;

                case GameState.Playing:
                    if (showInventory)
                    {
                        DrawInventory();
                    }
                    else
                    {
                        DrawEndTurnButton();
                        DrawUnitInfoPanel();
                        if (showFireTargets && selectedUnit != null && selectedUnit.Team == Team.Player)
                            DrawFireTargets();

                        _spriteBatch.DrawString(font, "Q/E: Rotation | Molette: Zoom | WASD/Middle: Deplacement | I: Inventaire",
                                                                new Vector2(10, 10), Color.White);

                        string timeStr = GetTimeOfDayString(timeOfDay);
                        _spriteBatch.DrawString(font, $"Heure: {timeStr} | Carte: {gridWidth}x{gridHeight}",
                            new Vector2(10, 30), Color.Yellow);
                    }
                    break;

                case GameState.OptionsMenu:
                    _spriteBatch.DrawString(font, "Options", Vector2.Zero, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);
                    mouse = Mouse.GetState();
                    foreach (var button in optionsButtons) button.Draw(_spriteBatch, font, mouse);
                    _spriteBatch.Draw(pixel, volumeBar, Color.Gray);
                    _spriteBatch.Draw(pixel, volumeFill, Color.Yellow);
                    _spriteBatch.Draw(pixel, volumeHandle, Color.White);
                    break;

                // NOUVEAU: Écran Game Over
                case GameState.GameOver:
                    // Fond semi-transparent rouge
                    _spriteBatch.Draw(pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height),
                                     new Color(100, 0, 0, 180));

                    // Texte GAME OVER
                    string gameOverText = "GAME OVER";
                    Vector2 gameOverSize = font.MeasureString(gameOverText);
                    Vector2 gameOverPos = new Vector2(
                        (GraphicsDevice.Viewport.Width - gameOverSize.X * 4f) / 2,
                        GraphicsDevice.Viewport.Height / 2 - 100
                    );
                    _spriteBatch.DrawString(font, gameOverText, gameOverPos, Color.Red, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);

                    // Texte instructions
                    string continueText = "Appuyez sur ESC ou cliquez pour retourner au menu";
                    Vector2 continueSize = font.MeasureString(continueText);
                    Vector2 continuePos = new Vector2(
                        (GraphicsDevice.Viewport.Width - continueSize.X) / 2,
                        GraphicsDevice.Viewport.Height / 2 + 50
                    );
                    _spriteBatch.DrawString(font, continueText, continuePos, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                    break;
            }
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private Color GetSkyColor(float time)
        {
            if (time < 0.25f)
            {
                return Color.Lerp(new Color(10, 10, 30), new Color(100, 120, 180), time / 0.25f);
            }
            else if (time < 0.5f)
            {
                return Color.Lerp(new Color(100, 120, 180), new Color(135, 206, 235), (time - 0.25f) / 0.25f);
            }
            else if (time < 0.75f)
            {
                return Color.Lerp(new Color(135, 206, 235), new Color(100, 120, 180), (time - 0.5f) / 0.25f);
            }
            else
            {
                return Color.Lerp(new Color(100, 120, 180), new Color(10, 10, 30), (time - 0.75f) / 0.25f);
            }
        }

        private string GetTimeOfDayString(float time)
        {
            int hours = (int)(time * 24);
            int minutes = (int)((time * 24 - hours) * 60);
            return $"{hours:D2}:{minutes:D2}";
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SYSTÈME D'INVENTAIRE
        // ═══════════════════════════════════════════════════════════════════════

        private void InitializeItems()
        {
            itemDatabase = new Dictionary<string, ItemData>
            {
                // Armes
                ["Rifle"] = new ItemData("Rifle", ItemType.Weapon, new WeaponData("Rifle", 25, 80, 5)),
                ["Plasma Rifle"] = new ItemData("Plasma Rifle", ItemType.Weapon, new WeaponData("Plasma Rifle", 30, 75, 5)),
                ["Plasma Sniper"] = new ItemData("Plasma Sniper", ItemType.Weapon, new WeaponData("Plasma Sniper", 50, 90, 8)),
                ["Shotgun"] = new ItemData("Shotgun", ItemType.Weapon, new WeaponData("Shotgun", 45, 70, 3)),
                ["SMG"] = new ItemData("SMG", ItemType.Weapon, new WeaponData("SMG", 20, 75, 4)),

                // Armures
                ["Helmet"] = new ItemData("Helmet", ItemType.Armor, armorValue: 10, armorSlot: ArmorSlot.Head),
                ["Ballistic Vest"] = new ItemData("Ballistic Vest", ItemType.Armor, armorValue: 25, armorSlot: ArmorSlot.Torso),
                ["Combat Helmet"] = new ItemData("Combat Helmet", ItemType.Armor, armorValue: 15, armorSlot: ArmorSlot.Head),
                ["Kevlar Vest"] = new ItemData("Kevlar Vest", ItemType.Armor, armorValue: 20, armorSlot: ArmorSlot.Torso),
                ["Tactical Vest"] = new ItemData("Tactical Vest", ItemType.Armor, armorValue: 30, armorSlot: ArmorSlot.Torso)
            };
        }

        private void InitializeInventoryItems()
        {
            availableItems.Add(new Item(itemDatabase["Plasma Rifle"], new Point(50, 50)));
            availableItems.Add(new Item(itemDatabase["Shotgun"], new Point(150, 50)));
            availableItems.Add(new Item(itemDatabase["SMG"], new Point(250, 50)));
            availableItems.Add(new Item(itemDatabase["Helmet"], new Point(50, 150)));
            availableItems.Add(new Item(itemDatabase["Ballistic Vest"], new Point(150, 150)));
            availableItems.Add(new Item(itemDatabase["Combat Helmet"], new Point(250, 150)));
            availableItems.Add(new Item(itemDatabase["Kevlar Vest"], new Point(350, 150)));
            availableItems.Add(new Item(itemDatabase["Tactical Vest"], new Point(450, 150)));
        }

        private void HandleInventory(MouseState mouse, bool leftClick)
        {
            if (selectedUnit == null) return;

            if (leftClick && draggedItem == null)
            {
                foreach (var item in availableItems)
                {
                    if (item.Bounds.Contains(mouse.Position))
                    {
                        draggedItem = item;
                        dragOffset = new Point(mouse.X - item.Position.X, mouse.Y - item.Position.Y);
                        break;
                    }
                }

                if (draggedItem == null && selectedUnit.EquippedWeapon != null)
                {
                    Rectangle weaponSlot = GetWeaponSlotBounds();
                    if (weaponSlot.Contains(mouse.Position))
                    {
                        draggedItem = selectedUnit.EquippedWeapon;
                        dragOffset = new Point(mouse.X - weaponSlot.X, mouse.Y - weaponSlot.Y);
                        selectedUnit.EquippedWeapon = null;
                    }
                }

                if (draggedItem == null && selectedUnit.EquippedHelmet != null)
                {
                    Rectangle helmetSlot = GetHelmetSlotBounds();
                    if (helmetSlot.Contains(mouse.Position))
                    {
                        draggedItem = selectedUnit.EquippedHelmet;
                        dragOffset = new Point(mouse.X - helmetSlot.X, mouse.Y - helmetSlot.Y);
                        selectedUnit.EquippedHelmet = null;
                    }
                }

                if (draggedItem == null && selectedUnit.EquippedArmor != null)
                {
                    Rectangle armorSlot = GetArmorSlotBounds();
                    if (armorSlot.Contains(mouse.Position))
                    {
                        draggedItem = selectedUnit.EquippedArmor;
                        dragOffset = new Point(mouse.X - armorSlot.X, mouse.Y - armorSlot.Y);
                        selectedUnit.EquippedArmor = null;
                    }
                }
            }

            if (draggedItem != null && mouse.LeftButton == ButtonState.Pressed)
            {
                draggedItem.Position = new Point(mouse.X - dragOffset.X, mouse.Y - dragOffset.Y);
            }

            if (draggedItem != null && mouse.LeftButton == ButtonState.Released)
            {
                bool equipped = false;

                if (draggedItem.Data.Type == ItemType.Weapon)
                {
                    Rectangle weaponSlot = GetWeaponSlotBounds();
                    if (weaponSlot.Contains(mouse.Position))
                    {
                        if (selectedUnit.EquippedWeapon != null && selectedUnit.EquippedWeapon != draggedItem)
                        {
                            selectedUnit.EquippedWeapon.Position = FindFreePosition();
                            if (!availableItems.Contains(selectedUnit.EquippedWeapon))
                                availableItems.Add(selectedUnit.EquippedWeapon);
                        }

                        selectedUnit.EquippedWeapon = draggedItem;
                        selectedUnit.Weapon = draggedItem.Data.Name;
                        selectedUnit.WeaponData = draggedItem.Data.WeaponData;
                        availableItems.Remove(draggedItem);
                        equipped = true;
                        UpdateFireTargets();
                    }
                }
                else if (draggedItem.Data.Type == ItemType.Armor)
                {
                    Rectangle targetSlot = Rectangle.Empty;

                    if (draggedItem.Data.ArmorSlot == ArmorSlot.Head)
                    {
                        targetSlot = GetHelmetSlotBounds();
                        if (targetSlot.Contains(mouse.Position))
                        {
                            if (selectedUnit.EquippedHelmet != null && selectedUnit.EquippedHelmet != draggedItem)
                            {
                                selectedUnit.EquippedHelmet.Position = FindFreePosition();
                                if (!availableItems.Contains(selectedUnit.EquippedHelmet))
                                    availableItems.Add(selectedUnit.EquippedHelmet);
                            }
                            selectedUnit.EquippedHelmet = draggedItem;
                            availableItems.Remove(draggedItem);
                            equipped = true;
                        }
                    }
                    else if (draggedItem.Data.ArmorSlot == ArmorSlot.Torso)
                    {
                        targetSlot = GetArmorSlotBounds();
                        if (targetSlot.Contains(mouse.Position))
                        {
                            if (selectedUnit.EquippedArmor != null && selectedUnit.EquippedArmor != draggedItem)
                            {
                                selectedUnit.EquippedArmor.Position = FindFreePosition();
                                if (!availableItems.Contains(selectedUnit.EquippedArmor))
                                    availableItems.Add(selectedUnit.EquippedArmor);
                            }
                            selectedUnit.EquippedArmor = draggedItem;
                            availableItems.Remove(draggedItem);
                            equipped = true;
                        }
                    }
                }

                if (!equipped)
                {
                    if (!availableItems.Contains(draggedItem))
                    {
                        draggedItem.Position = FindFreePosition();
                        availableItems.Add(draggedItem);
                    }
                }

                draggedItem = null;
            }
        }

        private Point FindFreePosition()
        {
            int gridX = 50;
            int gridY = 50;
            int cellSize = 60;

            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    Point testPos = new Point(gridX + x * cellSize, gridY + y * cellSize);
                    Rectangle testRect = new Rectangle(testPos.X, testPos.Y, 50, 50);

                    bool occupied = false;
                    foreach (var item in availableItems)
                    {
                        if (item.Bounds.Intersects(testRect))
                        {
                            occupied = true;
                            break;
                        }
                    }

                    if (!occupied) return testPos;
                }
            }

            return new Point(50, 50);
        }

        private Rectangle GetWeaponSlotBounds()
        {
            int panelX = GraphicsDevice.Viewport.Width / 2 - 300;
            int panelY = GraphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 450, panelY + 100, 80, 80);
        }

        private Rectangle GetHelmetSlotBounds()
        {
            int panelX = GraphicsDevice.Viewport.Width / 2 - 300;
            int panelY = GraphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 450, panelY + 200, 80, 80);
        }

        private Rectangle GetArmorSlotBounds()
        {
            int panelX = GraphicsDevice.Viewport.Width / 2 - 300;
            int panelY = GraphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 450, panelY + 300, 80, 80);
        }

        private void DrawInventory()
        {
            if (selectedUnit == null) return;

            int panelWidth = 650;
            int panelHeight = 450;
            int panelX = GraphicsDevice.Viewport.Width / 2 - panelWidth / 2;
            int panelY = GraphicsDevice.Viewport.Height / 2 - panelHeight / 2;
            inventoryPanel = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            _spriteBatch.Draw(pixel, inventoryPanel, new Color(20, 20, 20, 240));
            DrawRectangleBorder(inventoryPanel, Color.Gold, 3);

            string title = $"Inventaire - {selectedUnit.Name}";
            Vector2 titleSize = font.MeasureString(title);
            _spriteBatch.DrawString(font, title,
                new Vector2(inventoryPanel.Center.X - titleSize.X / 2, panelY + 10),
                Color.Gold, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);

            Rectangle itemsArea = new Rectangle(panelX + 20, panelY + 60, 400, 360);
            _spriteBatch.Draw(pixel, itemsArea, new Color(40, 40, 40, 200));
            DrawRectangleBorder(itemsArea, Color.Gray, 2);

            _spriteBatch.DrawString(font, "Items Disponibles:",
                new Vector2(itemsArea.X + 5, itemsArea.Y - 20), Color.White);

            foreach (var item in availableItems)
            {
                if (item != draggedItem)
                {
                    DrawItem(item);
                }
            }

            Rectangle equipArea = new Rectangle(panelX + 440, panelY + 60, 190, 360);
            _spriteBatch.Draw(pixel, equipArea, new Color(40, 40, 40, 200));
            DrawRectangleBorder(equipArea, Color.Gray, 2);

            _spriteBatch.DrawString(font, "Equipement:",
                new Vector2(equipArea.X + 5, equipArea.Y - 20), Color.White);

            DrawEquipmentSlot(GetWeaponSlotBounds(), "Arme", selectedUnit.EquippedWeapon);
            DrawEquipmentSlot(GetHelmetSlotBounds(), "Casque", selectedUnit.EquippedHelmet);
            DrawEquipmentSlot(GetArmorSlotBounds(), "Armure", selectedUnit.EquippedArmor);

            int totalArmor = 0;
            if (selectedUnit.EquippedHelmet != null) totalArmor += selectedUnit.EquippedHelmet.Data.ArmorValue;
            if (selectedUnit.EquippedArmor != null) totalArmor += selectedUnit.EquippedArmor.Data.ArmorValue;

            if (draggedItem != null)
            {
                DrawItem(draggedItem, 0.7f);
            }

            _spriteBatch.DrawString(font, "Glissez les items pour les equiper",
                new Vector2(panelX + 20, panelY + panelHeight - 30), Color.Yellow);
            _spriteBatch.DrawString(font, "Appuyez sur I ou ESC pour fermer",
                new Vector2(panelX + 20, panelY + panelHeight - 15), Color.Yellow);
        }

        private void DrawItem(Item item, float alpha = 1f)
        {
            Color itemColor = item.Data.Type == ItemType.Weapon ?
                new Color(100, 150, 255) : new Color(150, 100, 50);

            itemColor *= alpha;

            _spriteBatch.Draw(pixel, item.Bounds, itemColor);
            DrawRectangleBorder(item.Bounds, Color.White * alpha, 2);

            Vector2 nameSize = font.MeasureString(item.Data.Name);
            float scale = Math.Min(1f, (item.Bounds.Width - 10) / nameSize.X);

            _spriteBatch.DrawString(font, item.Data.Name,
                new Vector2(item.Bounds.X + 5, item.Bounds.Y + item.Bounds.Height / 2 - 10),
                Color.White * alpha, 0f, Vector2.Zero, scale * 0.6f, SpriteEffects.None, 0f);

            string info = item.Data.Type == ItemType.Weapon ?
                $"Dmg:{item.Data.WeaponData.Damage}" :
                $"Arm:{item.Data.ArmorValue}";

            _spriteBatch.DrawString(font, info,
                new Vector2(item.Bounds.X + 5, item.Bounds.Y + item.Bounds.Height / 2 + 5),
                Color.Yellow * alpha, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        private void DrawEquipmentSlot(Rectangle slot, string label, Item equippedItem)
        {
            _spriteBatch.Draw(pixel, slot, new Color(60, 60, 60, 200));
            DrawRectangleBorder(slot, Color.Gray, 2);

            _spriteBatch.DrawString(font, label,
                new Vector2(slot.X, slot.Y - 20), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

            if (equippedItem != null && equippedItem != draggedItem)
            {
                Color itemColor = equippedItem.Data.Type == ItemType.Weapon ?
                    new Color(100, 150, 255) : new Color(150, 100, 50);

                Rectangle itemRect = new Rectangle(slot.X + 5, slot.Y + 5, slot.Width - 10, slot.Height - 10);
                _spriteBatch.Draw(pixel, itemRect, itemColor);

                Vector2 nameSize = font.MeasureString(equippedItem.Data.Name);
                float scale = Math.Min(1f, (itemRect.Width - 4) / nameSize.X);

                _spriteBatch.DrawString(font, equippedItem.Data.Name,
                    new Vector2(itemRect.X + 2, itemRect.Y + itemRect.Height / 2 - 8),
                    Color.White, 0f, Vector2.Zero, scale * 0.5f, SpriteEffects.None, 0f);
            }
            else if (equippedItem == null)
            {
                _spriteBatch.DrawString(font, "Vide",
                    new Vector2(slot.Center.X - 20, slot.Center.Y - 8),
                    Color.Gray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            }
        }

        private void DrawRectangleBorder(Rectangle rect, Color color, int thickness)
        {
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            _spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PRIMITIVES 3D
        // ═══════════════════════════════════════════════════════════════════════

        private void CreateCubePrimitive()
        {
            cubeVertices = new VertexPositionColor[8];

            cubeVertices[0] = new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), Color.White);
            cubeVertices[1] = new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.5f), Color.White);
            cubeVertices[2] = new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.5f), Color.White);
            cubeVertices[3] = new VertexPositionColor(new Vector3(0.5f, -0.5f, -0.5f), Color.White);
            cubeVertices[4] = new VertexPositionColor(new Vector3(-0.5f, 0.5f, -0.5f), Color.White);
            cubeVertices[5] = new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0.5f), Color.White);
            cubeVertices[6] = new VertexPositionColor(new Vector3(0.5f, 0.5f, 0.5f), Color.White);
            cubeVertices[7] = new VertexPositionColor(new Vector3(0.5f, 0.5f, -0.5f), Color.White);

            cubeIndices = new short[]
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1,
                3, 2, 6, 3, 6, 7,
                1, 5, 6, 1, 6, 2,
                0, 3, 7, 0, 7, 4
            };
        }

        private void CreatePlanePrimitive()
        {
            planeVertices = new VertexPositionColor[4];
            planeVertices[0] = new VertexPositionColor(new Vector3(-0.5f, 0, -0.5f), Color.White);
            planeVertices[1] = new VertexPositionColor(new Vector3(-0.5f, 0, 0.5f), Color.White);
            planeVertices[2] = new VertexPositionColor(new Vector3(0.5f, 0, 0.5f), Color.White);
            planeVertices[3] = new VertexPositionColor(new Vector3(0.5f, 0, -0.5f), Color.White);

            planeIndices = new short[] { 0, 1, 2, 0, 2, 3 };
        }

        private void CreateTexturedPlanePrimitive()
        {
            planeTexturedVertices = new VertexPositionNormalTexture[4];

            Vector3 normal = Vector3.Up;

            planeTexturedVertices[0] = new VertexPositionNormalTexture(
                new Vector3(-0.5f, 0, -0.5f), normal, new Vector2(0, 0));
            planeTexturedVertices[1] = new VertexPositionNormalTexture(
                new Vector3(-0.5f, 0, 0.5f), normal, new Vector2(0, 1));
            planeTexturedVertices[2] = new VertexPositionNormalTexture(
                new Vector3(0.5f, 0, 0.5f), normal, new Vector2(1, 1));
            planeTexturedVertices[3] = new VertexPositionNormalTexture(
                new Vector3(0.5f, 0, -0.5f), normal, new Vector2(1, 0));

            planeTexturedIndices = new short[] { 0, 1, 2, 0, 2, 3 };
        }

        private void DrawCube(Vector3 position, Vector3 scale, Color color)
        {
            VertexPositionColor[] coloredVertices = new VertexPositionColor[8];
            for (int i = 0; i < 8; i++)
            {
                coloredVertices[i] = new VertexPositionColor(cubeVertices[i].Position, color);
            }

            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
            basicEffect.World = world;

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    coloredVertices,
                    0,
                    8,
                    cubeIndices,
                    0,
                    12
                );
            }
        }

        private void DrawPlane(Vector3 position, Vector3 scale, Color color)
        {
            VertexPositionColor[] coloredVertices = new VertexPositionColor[4];
            for (int i = 0; i < 4; i++)
            {
                coloredVertices[i] = new VertexPositionColor(planeVertices[i].Position, color);
            }

            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
            basicEffect.World = world;

            foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    coloredVertices,
                    0,
                    4,
                    planeIndices,
                    0,
                    2
                );
            }
        }

        private void DrawTexturedPlane(Vector3 position, Vector3 scale, Texture2D texture)
        {
            Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(position);
            texturedEffect.World = world;
            texturedEffect.Texture = texture;

            foreach (EffectPass pass in texturedEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    planeTexturedVertices,
                    0,
                    4,
                    planeTexturedIndices,
                    0,
                    2
                );
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CAMÉRA 3D
        // ═══════════════════════════════════════════════════════════════════════

        private void UpdateCamera()
        {
            float centerX = (gridWidth * cellSize) / 2f + cameraOffset.X;
            float centerZ = (gridHeight * cellSize) / 2f + cameraOffset.Y;

            cameraTarget = new Vector3(centerX, 0, centerZ);

            float adjustedDistance = cameraDistance / zoomLevel;
            float adjustedHeight = cameraHeight / zoomLevel;

            cameraPosition = new Vector3(
                centerX + (float)Math.Cos(cameraAngle) * adjustedDistance,
                adjustedHeight,
                centerZ + (float)Math.Sin(cameraAngle) * adjustedDistance
            );

            viewMatrix = Matrix.CreateLookAt(cameraPosition, cameraTarget, Vector3.Up);
        }

        private void HandleCameraControls(KeyboardState keyboard, MouseState mouse, GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            float rotationAmount = cameraRotationSpeed * deltaTime;

            if (keyboard.IsKeyDown(Keys.Q))
            {
                cameraAngle += rotationAmount;
            }
            if (keyboard.IsKeyDown(Keys.E))
            {
                cameraAngle -= rotationAmount;
            }

            float scrollDelta = (mouse.ScrollWheelValue - previousScrollValue) / 120f;
            previousScrollValue = mouse.ScrollWheelValue;

            if (scrollDelta != 0f)
            {
                zoomLevel = MathHelper.Clamp(zoomLevel + scrollDelta * 0.1f, minZoom, maxZoom);
            }

            Vector2 moveInput = Vector2.Zero;

            if (keyboard.IsKeyDown(Keys.W)) moveInput.Y += 1;
            if (keyboard.IsKeyDown(Keys.S)) moveInput.Y -= 1;
            if (keyboard.IsKeyDown(Keys.A)) moveInput.X += 1;
            if (keyboard.IsKeyDown(Keys.D)) moveInput.X -= 1;

            if (mouse.MiddleButton == ButtonState.Pressed && previousMouseState.MiddleButton == ButtonState.Pressed)
            {
                Vector2 mouseDelta = new Vector2(
                    mouse.X - previousMouseState.X,
                    mouse.Y - previousMouseState.Y
                );

                moveInput.X += mouseDelta.X * 0.5f;
                moveInput.Y += mouseDelta.Y * 0.5f;
            }

            if (moveInput != Vector2.Zero)
            {
                if (moveInput.LengthSquared() > 1f)
                    moveInput.Normalize();

                float moveAngle = cameraAngle + MathHelper.PiOver2;

                Vector2 rotatedMove = new Vector2(
                    moveInput.X * (float)Math.Cos(moveAngle) - moveInput.Y * (float)Math.Sin(moveAngle),
                    moveInput.X * (float)Math.Sin(moveAngle) + moveInput.Y * (float)Math.Cos(moveAngle)
                );

                cameraOffset += rotatedMove * cameraMoveSpeed * deltaTime;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RENDU 3D DE LA SCÈNE
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawGrid3D()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    Vector3 position = new Vector3(x * cellSize + cellSize / 2f, 0, z * cellSize + cellSize / 2f);
                    DrawTexturedPlane(position, new Vector3(cellSize * 0.95f, 1, cellSize * 0.95f), tileTexture);
                }
            }
        }

        private void DrawWalls3D()
        {
            foreach (var segment in wallSegments)
            {
                Vector3 start3D, end3D;

                if (segment.IsHorizontal)
                {
                    // Mur horizontal (sépare les cases verticalement)
                    start3D = new Vector3(
                        segment.Start.X * cellSize,
                        cellSize * 0.75f,
                        segment.Start.Y * cellSize
                    );
                    end3D = new Vector3(
                        segment.End.X * cellSize,
                        cellSize * 0.75f,
                        segment.End.Y * cellSize
                    );
                }
                else
                {
                    // Mur vertical (sépare les cases horizontalement)
                    start3D = new Vector3(
                        segment.Start.X * cellSize,
                        cellSize * 0.75f,
                        segment.Start.Y * cellSize
                    );
                    end3D = new Vector3(
                        segment.End.X * cellSize,
                        cellSize * 0.75f,
                        segment.End.Y * cellSize
                    );
                }

                // Position au centre du segment
                Vector3 center = (start3D + end3D) / 2f;

                // Dimensions du mur
                Vector3 scale;
                if (segment.IsHorizontal)
                {
                    // Mur horizontal : long en X, mince en Z
                    scale = new Vector3(cellSize, cellSize * 1.5f, cellSize * 0.1f);
                }
                else
                {
                    // Mur vertical : mince en X, long en Z
                    scale = new Vector3(cellSize * 0.1f, cellSize * 1.5f, cellSize);
                }

                // Dessiner le mur
                DrawCube(center, scale, new Color(120, 120, 120));
            }
        }

        private void DrawUnits3D()
        {
            foreach (var unit in playerUnits)
                DrawUnit3D(unit);

            foreach (var unit in enemyUnits)
                DrawUnit3D(unit);
        }

        private void DrawUnit3D(Unit unit)
        {
            Vector3 basePosition = new Vector3(
                unit.Cell.X * cellSize + cellSize / 2f,
                0,
                unit.Cell.Y * cellSize + cellSize / 2f
            );

            Vector3 visualOffset = Vector3.Zero;

            // Animation de tir existante
            if (unit.IsFiring && unit.FireTarget.HasValue)
            {
                Vector3 targetPos = new Vector3(
                    unit.FireTarget.Value.X * cellSize + cellSize / 2f,
                    cellSize * 0.75f,
                    unit.FireTarget.Value.Y * cellSize + cellSize / 2f
                );

                if (unit.Weapon == "Zombie Claws")
                {
                    Vector3 chargeVector = targetPos - basePosition;
                    float t = unit.FireProgress;

                    if (t < 0.5f)
                    {
                        float forwardT = t / 0.5f;
                        visualOffset = Vector3.Lerp(Vector3.Zero, chargeVector, forwardT);
                    }
                    else
                    {
                        float returnT = (t - 0.5f) / 0.5f;
                        visualOffset = Vector3.Lerp(chargeVector, Vector3.Zero, returnT);
                    }
                }
                else
                {
                    Vector3 projectilePos = Vector3.Lerp(basePosition, targetPos, unit.FireProgress);
                    DrawCube(projectilePos, new Vector3(cellSize * 0.2f), Color.Yellow);
                }
            }

            Vector3 finalPos = basePosition + visualOffset;
            Color unitColor = unit.Team == Team.Player ? Color.Blue : Color.Red;

            // Déterminer le type d'unité
            HumanoidModelAdvanced.UnitType unitType = HumanoidModelAdvanced.UnitType.Soldier;

            if (unit.Class == "Assault" || unit.Class == "Infantry")
                unitType = HumanoidModelAdvanced.UnitType.Soldier;
            else if (unit.Class == "Heavy")
                unitType = HumanoidModelAdvanced.UnitType.Heavy;
            else if (unit.Class == "Scout")
                unitType = HumanoidModelAdvanced.UnitType.Scout;
            else if (unit.Class == "Undead")
                unitType = HumanoidModelAdvanced.UnitType.Zombie;
            else if (unit.Team == Team.Enemy && unit.Name.Contains("Alien"))
                unitType = HumanoidModelAdvanced.UnitType.Alien;

            // MODIFIÉ: Dessiner le modèle humanoïde avec orientation
            humanoidModel.Draw(GraphicsDevice, basicEffect, finalPos, unitColor, cellSize * 0.8f, unitType, unit.Orientation);

            // Indicateurs de sélection (inchangés)
            if (unit == selectedUnit)
            {
                Vector3 selectionPos = new Vector3(basePosition.X, 0.05f, basePosition.Z);
                DrawPlane(selectionPos, new Vector3(cellSize * 1.1f, 1, cellSize * 1.1f),
                         new Color(0, 255, 255, 128));
            }

            Unit targetHighlight = selectedFireTarget ?? hoveredFireTarget;
            if (targetHighlight == unit)
            {
                Vector3 highlightPos = new Vector3(basePosition.X, 0.1f, basePosition.Z);
                DrawPlane(highlightPos, new Vector3(cellSize * 1.2f, 1, cellSize * 1.2f),
                         new Color(255, 0, 0, 128));
            }
        }

        private void DrawMovableCells3D(GameTime gameTime)
        {
            if (selectedUnit != null && selectedUnit.ActionPoints > 0 &&
                currentTurn == TurnState.PlayerTurn && selectedUnit.Team == Team.Player)
            {
                float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 3f) * 0.3f + 0.7f;

                foreach (var cell in cachedMovableCells)
                {
                    Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, 0.05f, cell.Y * cellSize + cellSize / 2f);
                    DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), Color.Green * pulse);
                }
            }
        }

        private void DrawPath3D(GameTime gameTime)
        {
            if (currentPath.Count == 0 || selectedUnit == null || selectedUnit.Team != Team.Player)
                return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.2f + 0.8f;

            for (int i = 0; i < currentPath.Count; i++)
            {
                Point cell = currentPath[i];
                Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, 0.1f, cell.Y * cellSize + cellSize / 2f);

                float intensity = 1f - (i / (float)currentPath.Count) * 0.5f;
                Color pathColor = new Color(100, 150, 255) * pulse * intensity;

                DrawPlane(position, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), pathColor);
            }
        }

        private void DrawHoveredCell3D(GameTime gameTime)
        {
            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f) * 0.3f + 0.7f;
            Vector3 position = new Vector3(hoveredCell.X * cellSize + cellSize / 2f, 0.15f, hoveredCell.Y * cellSize + cellSize / 2f);

            DrawPlane(position, new Vector3(cellSize, 1, cellSize), Color.Yellow * pulse);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RAYCAST POUR SÉLECTION 3D
        // ═══════════════════════════════════════════════════════════════════════

        private Point GetCellFromMouseRaycast(MouseState mouse)
        {
            Vector3 nearPoint = GraphicsDevice.Viewport.Unproject(
                new Vector3(mouse.X, mouse.Y, 0),
                projectionMatrix,
                viewMatrix,
                Matrix.Identity
            );

            Vector3 farPoint = GraphicsDevice.Viewport.Unproject(
                new Vector3(mouse.X, mouse.Y, 1),
                projectionMatrix,
                viewMatrix,
                Matrix.Identity
            );

            Vector3 direction = Vector3.Normalize(farPoint - nearPoint);

            if (Math.Abs(direction.Y) > 0.001f)
            {
                float t = -nearPoint.Y / direction.Y;
                Vector3 intersection = nearPoint + direction * t;

                int cellX = (int)(intersection.X / cellSize);
                int cellZ = (int)(intersection.Z / cellSize);

                if (cellX >= 0 && cellX < gridWidth && cellZ >= 0 && cellZ < gridHeight)
                {
                    return new Point(cellX, cellZ);
                }
            }

            return new Point(-1, -1);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UI 2D
        // ═══════════════════════════════════════════════════════════════════════

        private void DrawEndTurnButton()
        {
            int w = 140, h = 36;
            endTurnButton = new Rectangle(GraphicsDevice.Viewport.Width - w - 20,
                                          GraphicsDevice.Viewport.Height - h - 20, w, h);
            Color color = endTurnHovered ? Color.DarkRed : Color.Red;
            _spriteBatch.Draw(pixel, endTurnButton, color);

            Vector2 txtSize = font.MeasureString("FIN DU TOUR");
            _spriteBatch.DrawString(font, "FIN DU TOUR",
                new Vector2(endTurnButton.X + (w - txtSize.X) / 2, endTurnButton.Y + (h - txtSize.Y) / 2),
                Color.White);
        }

        private void DrawFireTargets()
        {
            MouseState mouse = Mouse.GetState();
            hoveredFireTarget = null;

            foreach (var ui in fireTargetsUI)
                if (ui.Bounds.Contains(mouse.Position)) { hoveredFireTarget = ui.Target; break; }

            foreach (var ui in fireTargetsUI)
            {
                Color bg = ui.Target == selectedFireTarget ? Color.OrangeRed : Color.DarkRed;
                _spriteBatch.Draw(pixel, ui.Bounds, bg);
                Vector2 size = font.MeasureString(ui.HitChance + "%");
                _spriteBatch.DrawString(font, ui.HitChance + "%",
                    new Vector2(ui.Bounds.Center.X - size.X / 2, ui.Bounds.Center.Y - size.Y / 2), Color.White);
            }

            Unit highlight = selectedFireTarget ?? hoveredFireTarget;
            if (highlight != null)
            {
                var ui = fireTargetsUI.FirstOrDefault(f => f.Target == highlight);
                if (ui != null)
                {
                    _spriteBatch.Draw(pixel, ui.Bounds, Color.Red * 0.4f);
                    int t = 3;
                    DrawLine(_spriteBatch, new Vector2(ui.Bounds.Left, ui.Bounds.Top), new Vector2(ui.Bounds.Right, ui.Bounds.Top), Color.Red, t);
                    DrawLine(_spriteBatch, new Vector2(ui.Bounds.Left, ui.Bounds.Bottom), new Vector2(ui.Bounds.Right, ui.Bounds.Bottom), Color.Red, t);
                    DrawLine(_spriteBatch, new Vector2(ui.Bounds.Left, ui.Bounds.Top), new Vector2(ui.Bounds.Left, ui.Bounds.Bottom), Color.Red, t);
                    DrawLine(_spriteBatch, new Vector2(ui.Bounds.Right, ui.Bounds.Top), new Vector2(ui.Bounds.Right, ui.Bounds.Bottom), Color.Red, t);
                }
            }
        }

        private void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            float rotation = (float)Math.Atan2(delta.Y, delta.X);
            sb.Draw(pixel, start, null, color, rotation, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private void DrawUnitInfoPanel()
        {
            if (selectedUnit == null) return;

            int m = 10, w = 300, h = 160;
            int x = m, y = GraphicsDevice.Viewport.Height - h - m;
            Rectangle panel = new(x, y, w, h);

            _spriteBatch.Draw(pixel, panel, new Color(0, 0, 0, 180));

            Vector2 p = new(x + 10, y + 10);
            _spriteBatch.DrawString(font, $"Name: {selectedUnit.Name}", p, Color.White);
            _spriteBatch.DrawString(font, $"Class: {selectedUnit.Class}", p + new Vector2(0, 20), Color.White);
            _spriteBatch.DrawString(font, $"Weapon: {selectedUnit.Weapon}", p + new Vector2(0, 40), Color.White);
            _spriteBatch.DrawString(font, $"AP: {selectedUnit.ActionPoints}", p + new Vector2(0, 60), Color.White);
            _spriteBatch.DrawString(font, $"HP: {selectedUnit.Health} / {selectedUnit.MaxHealth}", p + new Vector2(0, 80), Color.White);

            int totalArmor = selectedUnit.GetTotalArmor();
            _spriteBatch.DrawString(font, $"Armor: {totalArmor}", p + new Vector2(0, 100), Color.Cyan);

            unitActionButtons.Clear();

            int bw = ActionButtonWidth, bh = ActionButtonHeight;
            int by = GraphicsDevice.Viewport.Height - bh - 15;
            int bx = (GraphicsDevice.Viewport.Width - bw) / 2;

            // Créer les boutons d'action de base
            var buttons = new List<Button>
    {
        new Button("COUVERT", new Vector2(bx - 220, by)),
        new Button("TIRER", new Vector2(bx - 110, by)),
        new Button("RECHARGER", new Vector2(bx, by))
    };

            // Ajouter le bouton GRENADE si l'unité a des grenades
            if (selectedUnit.Grenades.Count > 0)
            {
                buttons.Add(new Button("GRENADE", new Vector2(bx + 110, by)));
            }

            unitActionButtons.AddRange(buttons);

            MouseState mouse = Mouse.GetState();

            foreach (var b in unitActionButtons)
            {
                Rectangle r = new((int)b.Position.X, (int)b.Position.Y, bw, bh);
                Color c = r.Contains(mouse.Position) ? Color.Orange : Color.DarkOrange;

                _spriteBatch.Draw(pixel, r, c);

                Vector2 ts = font.MeasureString(b.Text);
                _spriteBatch.DrawString(
                    font, b.Text,
                    new Vector2(r.X + (bw - ts.X) / 2, r.Y + (bh - ts.Y) / 2),
                    Color.Black
                );
            }

            // NOUVEAU: Dessiner le grand bouton TIRER CONFIRMER si une cible est sélectionnée
            if (selectedFireTarget != null && selectedUnit.ActionPoints > 0)
            {
                int fireConfirmWidth = 140;
                int fireConfirmHeight = 50;
                fireButton = new Rectangle(
                    GraphicsDevice.Viewport.Width / 2 - fireConfirmWidth / 2,
                    by - fireConfirmHeight - 10,
                    fireConfirmWidth,
                    fireConfirmHeight
                );

                Color fireColor = fireButton.Contains(mouse.Position) ? Color.Red : Color.DarkRed;
                _spriteBatch.Draw(pixel, fireButton, fireColor);

                string fireText = "CONFIRMER TIR";
                Vector2 fireTextSize = font.MeasureString(fireText);
                _spriteBatch.DrawString(
                    font, fireText,
                    new Vector2(fireButton.X + (fireConfirmWidth - fireTextSize.X) / 2,
                               fireButton.Y + (fireConfirmHeight - fireTextSize.Y) / 2),
                    Color.White
                );
            }
            else
            {
                // Pas de cible sélectionnée, pas de bouton CONFIRMER
                fireButton = Rectangle.Empty;
            }

            // Afficher les grenades disponibles
            Vector2 grenadePos = p + new Vector2(0, 120);
            _spriteBatch.DrawString(font, $"Grenades: {selectedUnit.Grenades.Count}/{selectedUnit.MaxGrenades}",
                                   grenadePos, Color.Orange);

            for (int i = 0; i < selectedUnit.Grenades.Count; i++)
            {
                var grenade = selectedUnit.Grenades[i];
                string symbol = GrenadeDatabase.GetGrenadeSymbol(grenade.Type);
                Color color = GrenadeDatabase.GetGrenadeColor(grenade.Type);

                _spriteBatch.DrawString(font, symbol,
                                       grenadePos + new Vector2(i * 30, 20),
                                       color, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            }

            UpdateFireTargetsUIPositions();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LOGIQUE DE JEU
        // ═══════════════════════════════════════════════════════════════════════

        private void LoadMap()
        {
            gridWidth = random.Next(15, 31);
            gridHeight = random.Next(12, 26);
            cellSize = 2;

            timeOfDay = (float)random.NextDouble();
            dayNightSpeed = 1f / 86400f;

            GenerateWalls(gridWidth * gridHeight / 10);
            Console.WriteLine($"Map loaded: {gridWidth}x{gridHeight}, Starting time: {GetTimeOfDayString(timeOfDay)}");
        }

        private class AStarNode
        {
            public Point Position;
            public int GCost;
            public int HCost;
            public int FCost => GCost + HCost;
            public AStarNode Parent;

            public AStarNode(Point pos)
            {
                Position = pos;
            }
        }

        private List<Point> FindPathAStar(Point start, Point goal, int maxDistance, Unit movingUnit = null)
        {
            List<AStarNode> openList = new List<AStarNode>();
            HashSet<Point> closedList = new HashSet<Point>();

            AStarNode startNode = new AStarNode(start) { GCost = 0, HCost = ManhattanDistance(start, goal) };
            openList.Add(startNode);

            while (openList.Count > 0)
            {
                AStarNode currentNode = openList[0];
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].FCost < currentNode.FCost ||
                        (openList[i].FCost == currentNode.FCost && openList[i].HCost < currentNode.HCost))
                    {
                        currentNode = openList[i];
                    }
                }

                openList.Remove(currentNode);
                closedList.Add(currentNode.Position);

                if (currentNode.Position == goal)
                {
                    return RetracePath(startNode, currentNode);
                }

                foreach (Point neighbor in GetNeighbors(currentNode.Position))
                {
                    if (closedList.Contains(neighbor))
                        continue;

                    if (neighbor != goal && !IsWalkable(neighbor, movingUnit))
                        continue;


                    int newGCost = currentNode.GCost + 1;



                    AStarNode neighborNode = openList.FirstOrDefault(n => n.Position == neighbor);

                    if (neighborNode == null)
                    {
                        neighborNode = new AStarNode(neighbor)
                        {
                            GCost = newGCost,
                            HCost = ManhattanDistance(neighbor, goal),
                            Parent = currentNode
                        };
                        openList.Add(neighborNode);
                    }
                    else if (newGCost < neighborNode.GCost)
                    {
                        neighborNode.GCost = newGCost;
                        neighborNode.Parent = currentNode;
                    }
                }
            }

            return new List<Point>();
        }


        private List<Point> RetracePath(AStarNode startNode, AStarNode endNode)
        {
            List<Point> path = new List<Point>();
            AStarNode currentNode = endNode;

            while (currentNode != startNode)
            {
                path.Add(currentNode.Position);
                currentNode = currentNode.Parent;
            }

            path.Reverse();
            return path;
        }

        private List<Point> GetNeighbors(Point cell)
        {
            List<Point> neighbors = new List<Point>();

            Point[] potentialNeighbors = new Point[]
            {
                new Point(cell.X, cell.Y - 1), // Nord
                new Point(cell.X, cell.Y + 1), // Sud
                new Point(cell.X - 1, cell.Y), // Ouest
                new Point(cell.X + 1, cell.Y)  // Est
            };

            foreach (var neighbor in potentialNeighbors)
            {
                // Vérifier que le voisin est dans la grille
                if (neighbor.X >= 0 && neighbor.X < gridWidth &&
                    neighbor.Y >= 0 && neighbor.Y < gridHeight)
                {
                    // Vérifier qu'il n'y a pas de mur entre les deux cases
                    if (!HasWallBetween(cell, neighbor))
                    {
                        neighbors.Add(neighbor);
                    }
                }
            }

            return neighbors;
        }

        private bool IsWalkable(Point cell, Unit movingUnit = null)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= gridWidth || cell.Y >= gridHeight)
                return false;

            var unit = GetUnitAtCell(cell);
            if (unit != null && unit != movingUnit)
                return false;

            return true;
        }




        /// <summary>
        /// Vérifie s'il y a un mur entre deux cases adjacentes
        /// </summary>
        private bool HasWallBetween(Point from, Point to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            // Doivent être adjacentes
            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return false;

            foreach (var w in wallSegments)
            {
                if (dy == 1 && w.IsHorizontal) // vers le bas
                {
                    if (w.Start.Y == to.Y &&
                        from.X >= w.Start.X && from.X < w.End.X)
                        return true;
                }
                if (dy == -1 && w.IsHorizontal) // vers le haut
                {
                    if (w.Start.Y == from.Y &&
                        from.X >= w.Start.X && from.X < w.End.X)
                        return true;
                }
                if (dx == 1 && !w.IsHorizontal) // vers la droite
                {
                    if (w.Start.X == to.X &&
                        from.Y >= w.Start.Y && from.Y < w.End.Y)
                        return true;
                }
                if (dx == -1 && !w.IsHorizontal) // vers la gauche
                {
                    if (w.Start.X == from.X &&
                        from.Y >= w.Start.Y && from.Y < w.End.Y)
                        return true;
                }
            }

            return false;
        }





        private int ManhattanDistance(Point a, Point b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        private List<Point> GetMovableCells(Unit u)
        {
            var cells = new List<Point>();
            if (u == null || u.MovementPoints <= 0) return cells;

            int maxRange = u.MovementPoints;

            for (int x = u.Cell.X - maxRange; x <= u.Cell.X + maxRange; x++)
            {
                for (int y = u.Cell.Y - maxRange; y <= u.Cell.Y + maxRange; y++)
                {
                    Point targetCell = new Point(x, y);

                    if (targetCell == u.Cell)
                        continue;

                    if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                        continue;

                    if (!IsWalkable(targetCell))
                        continue;

                    var path = FindPathAStar(u.Cell, targetCell, maxRange);

                    if (path.Count > 0 && path.Count <= maxRange)
                    {
                        cells.Add(targetCell);
                    }
                }
            }

            return cells;
        }

        private void CreateUnits(string missionType = "Tutorial")
        {
            playerUnits.Clear();
            enemyUnits.Clear();

            for (int i = 0; i < 6; i++)
                playerUnits.Add(new Unit(
                    new Point(2 + i, gridHeight - 2),
                    Team.Player,
                    "Soldier " + (i + 1),
                    "Assault",
                    "Rifle",
                    weaponDatabase["Rifle"]
                ));

            // Équiper les unités joueur avec des grenades
            foreach (var unit in playerUnits)
            {
                unit.AddGrenade(grenadeDatabase["Frag Grenade"]);

                if (random.Next(100) < 50)
                    unit.AddGrenade(grenadeDatabase["Smoke Grenade"]);
            }

            switch (missionType)
            {
                case "Tutorial":
                    for (int i = 0; i < 6; i++)
                    {
                        var template = enemyPool[random.Next(enemyPool.Count)];
                        enemyUnits.Add(new Unit(
                            new Point(2 + i, 1),
                            Team.Enemy,
                            template.Name,
                            template.Class,
                            template.Weapon,
                            weaponDatabase[template.Weapon]
                        )
                        {
                            ActionPoints = template.ActionPoints
                        });
                    }
                    break;

                case "Survival":
                    for (int i = 0; i < 10; i++)
                    {
                        var template = enemyPool[random.Next(enemyPool.Count)];
                        int x = 2 + (i % 8);
                        int y = i < 8 ? 1 : 2;
                        enemyUnits.Add(new Unit(
                            new Point(x, y),
                            Team.Enemy,
                            template.Name,
                            template.Class,
                            template.Weapon,
                            weaponDatabase[template.Weapon]
                        )
                        {
                            ActionPoints = template.ActionPoints
                        });
                    }
                    break;

                case "Assault":
                    for (int i = 0; i < 8; i++)
                    {
                        var alienTemplates = enemyPool.Where(e => e.Name != "Zombie").ToList();
                        var template = alienTemplates[random.Next(alienTemplates.Count)];
                        int x = 2 + i;
                        enemyUnits.Add(new Unit(
                            new Point(x, 1),
                            Team.Enemy,
                            template.Name,
                            template.Class,
                            template.Weapon,
                            weaponDatabase[template.Weapon]
                        )
                        {
                            ActionPoints = template.ActionPoints
                        });
                    }
                    break;

                case "Defense":
                    var zombieTemplate = enemyPool.First(e => e.Name == "Zombie");
                    for (int i = 0; i < 12; i++)
                    {
                        int x = 2 + (i % 8);
                        int y = i < 8 ? 1 : 2;
                        enemyUnits.Add(new Unit(
                            new Point(x, y),
                            Team.Enemy,
                            zombieTemplate.Name,
                            zombieTemplate.Class,
                            zombieTemplate.Weapon,
                            weaponDatabase[zombieTemplate.Weapon]
                        )
                        {
                            ActionPoints = zombieTemplate.ActionPoints
                        });
                    }
                    break;
            }

            Console.WriteLine($"Units created for {missionType}: 6 player, {enemyUnits.Count} enemy");
        }

        private IEnumerable<Unit> AllUnits()
        {
            foreach (var u in playerUnits) yield return u;
            foreach (var u in enemyUnits) yield return u;
        }

        private Unit GetUnitAtCell(Point cell)
        {
            foreach (var u in AllUnits())
                if (u.Cell == cell) return u;
            return null;
        }

        private void HandleFire(Unit s)
        {
            if (s.ActionPoints <= 0) return;

            foreach (var t in AllUnits())
            {
                if (t.Team == s.Team) continue;

                int d = Math.Abs(t.Cell.X - s.Cell.X) + Math.Abs(t.Cell.Y - s.Cell.Y);
                if (d > s.WeaponData.Range || !HasLineOfSight(s.Cell, t.Cell)) continue;

                s.IsFiring = true;
                s.FireTarget = t.Cell;
                s.FireProgress = 0f;
                s.PendingTarget = t;
                s.ActionPoints--;

                int acc = Math.Max(s.WeaponData.Accuracy - d * 5, 10);
                s.WillHit = random.Next(100) < acc;

                return;
            }

            Console.WriteLine("Aucune cible valide (portée + LOS)");
        }

        private void StartPlayerTurn()
        {
            foreach (var u in playerUnits) u.ActionPoints = 3;

            selectedUnit = null;
            cachedMovableCells.Clear();
            currentPath.Clear();
            pathCosts.Clear();
            currentTurn = TurnState.PlayerTurn;
        }

        private void StartEnemyTurn()
        {
            foreach (var u in enemyUnits) u.ActionPoints = 2;
            enemyTurnIndex = 0;
            currentTurn = TurnState.EnemyTurn;
        }

        private void UpdateEnemyTurn()
        {
            if (enemyTurnIndex >= enemyUnits.Count) { StartPlayerTurn(); return; }

            Unit enemy = enemyUnits[enemyTurnIndex];
            if (enemy.IsFiring) return;
            if (enemy.ActionPoints <= 0) { enemyTurnIndex++; return; }

            Unit closest = null;
            int bestDist = int.MaxValue;
            foreach (var p in playerUnits)
            {
                int dist = Math.Abs(p.Cell.X - enemy.Cell.X) + Math.Abs(p.Cell.Y - enemy.Cell.Y);
                if (dist < bestDist) { bestDist = dist; closest = p; }
            }
            if (closest == null) { enemyTurnIndex++; return; }

            if (bestDist <= enemy.WeaponData.Range && HasLineOfSight(enemy.Cell, closest.Cell))
            { HandleFire(enemy); return; }

            var path = FindPathAStar(enemy.Cell, closest.Cell, int.MaxValue, enemy);

            if (path.Count > 0)
            {
                int steps = Math.Min(enemy.MovementPoints, path.Count);

                // CORRECTION: Ne pas se déplacer sur la dernière case si elle est occupée
                Point targetCell = path[steps - 1];

                // Vérifier si la case cible est occupée
                if (GetUnitAtCell(targetCell) != null)
                {
                    // Trouver la case la plus proche non occupée sur le chemin
                    for (int i = steps - 2; i >= 0; i--)
                    {
                        if (GetUnitAtCell(path[i]) == null)
                        {
                            targetCell = path[i];
                            break;
                        }
                    }

                    // Si aucune case n'est libre sur le chemin, ne pas bouger
                    if (GetUnitAtCell(targetCell) != null)
                    {
                        enemy.ActionPoints = 0;
                        enemyTurnIndex++;
                        return;
                    }
                }

                enemy.Cell = targetCell;
                enemy.ActionPoints--;
            }
            else
            {
                // Essayer de se déplacer d'une case vers le joueur
                int dx = Math.Sign(closest.Cell.X - enemy.Cell.X);
                int dy = Math.Sign(closest.Cell.Y - enemy.Cell.Y);

                Point nextX = new Point(enemy.Cell.X + dx, enemy.Cell.Y);
                Point nextY = new Point(enemy.Cell.X, enemy.Cell.Y + dy);

                // Essayer le mouvement horizontal d'abord
                if (dx != 0 && IsWalkable(nextX, enemy) && !HasWallBetween(enemy.Cell, nextX))
                {
                    enemy.Cell = nextX;
                    enemy.ActionPoints--;
                }
                // Sinon essayer le mouvement vertical
                else if (dy != 0 && IsWalkable(nextY, enemy) && !HasWallBetween(enemy.Cell, nextY))
                {
                    enemy.Cell = nextY;
                    enemy.ActionPoints--;
                }
                else
                {
                    // Impossible de se déplacer → fin du tour
                    enemy.ActionPoints = 0;
                }
            }

            if (enemy.ActionPoints <= 0) enemyTurnIndex++;
        }

        private void FireAtTarget(Unit shooter, Unit target)
        {
            if (shooter.ActionPoints <= 0 || !HasLineOfSight(shooter.Cell, target.Cell)) return;

            int distance = Math.Abs(target.Cell.X - shooter.Cell.X) + Math.Abs(target.Cell.Y - shooter.Cell.Y);
            if (distance > shooter.WeaponData.Range) return;

            isActionInProgress = true;
            shooter.IsFiring = true;
            shooter.FireTarget = target.Cell;
            shooter.FireProgress = 0f;

            int roll = random.Next(100);
            int effectiveAccuracy = Math.Max(shooter.WeaponData.Accuracy - distance * 5, 10);
            shooter.WillHit = roll < effectiveAccuracy;
            shooter.PendingTarget = target;
            shooter.ActionPoints--;

            UpdateFireTargets();
            selectedFireTarget = null;
        }

        private void UpdateFiringAnimations(GameTime gameTime)
        {
            float fireSpeed = 3f;
            foreach (var u in AllUnits())
            {
                if (!u.IsFiring || !u.FireTarget.HasValue) continue;

                u.FireProgress += (float)gameTime.ElapsedGameTime.TotalSeconds * fireSpeed;
                if (u.FireProgress < 1f) continue;

                u.IsFiring = false;
                u.FireProgress = 0f;

                if (u.PendingTarget != null)
                {
                    if (u.WillHit)
                    {
                        var t = u.PendingTarget;
                        int damage = Math.Max(u.WeaponData.Damage - t.GetTotalArmor(), 1);
                        t.Health = Math.Max(t.Health - damage, 0);

                        if (t.Health <= 0)
                        {
                            if (t.Team == Team.Player)
                            {
                                playerUnits.Remove(t);

                                // NOUVEAU: Vérifier Game Over
                                if (playerUnits.Count == 0)
                                {
                                    currentState = GameState.GameOver;
                                    Console.WriteLine("GAME OVER - All player units eliminated!");
                                }
                            }
                            else
                            {
                                enemyUnits.Remove(t);

                                // NOUVEAU: Vérifier Victoire
                                if (enemyUnits.Count == 0)
                                {
                                    Console.WriteLine("VICTORY - All enemies eliminated!");
                                }
                            }
                        }
                    }
                    u.PendingTarget = null;
                    u.WillHit = false;
                }

                u.FireTarget = null;
                isActionInProgress = false;
                return;
            }
        }

        private void GenerateWalls(int count)
        {
            wallSegments.Clear();

            // Choisir le pattern selon le type de mission
            EdgeWallGenerator.WallPattern pattern;

            switch (selectedMission)
            {
                case "Tutorial":
                    pattern = EdgeWallGenerator.WallPattern.Scattered;
                    break;
                case "Survival":
                    pattern = EdgeWallGenerator.WallPattern.Bunker;
                    break;
                case "Assault":
                    pattern = EdgeWallGenerator.WallPattern.Urban;
                    break;
                case "Defense":
                    pattern = EdgeWallGenerator.WallPattern.Trenches;
                    break;
                default:
                    var patterns = Enum.GetValues(typeof(EdgeWallGenerator.WallPattern));
                    pattern = (EdgeWallGenerator.WallPattern)patterns.GetValue(random.Next(patterns.Length));
                    break;
            }

            // Générer les segments de murs
            wallSegments = edgeWallGenerator.GenerateWalls(gridWidth, gridHeight, pattern, count);

            // Nettoyer les zones de spawn
            edgeWallGenerator.ClearSpawnZones(wallSegments, gridWidth, gridHeight);

            Console.WriteLine($"Generated {wallSegments.Count} wall segments using pattern: {pattern}");
        }

        private bool HasLineOfSight(Point from, Point to)
        {
            // Algorithme de Bresenham pour ligne de vue
            int x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y;
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            Point current = new Point(x0, y0);
            Point previous = current;

            while (true)
            {
                if (current != from && HasWallBetween(previous, current))
                    return false;

                if (current.X == x1 && current.Y == y1)
                    break;

                previous = current;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    current.X += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    current.Y += sy;
                }
            }

            return true;
        }

        private List<Unit> GetValidFireTargets(Unit shooter)
        {
            List<Unit> targets = new();
            foreach (var u in enemyUnits)
            {
                int d = Math.Abs(u.Cell.X - shooter.Cell.X) + Math.Abs(u.Cell.Y - shooter.Cell.Y);
                if (d <= shooter.WeaponData.Range && HasLineOfSight(shooter.Cell, u.Cell))
                    targets.Add(u);
            }
            return targets;
        }

        void UpdateFireTargets()
        {
            fireTargetsUI.Clear();
            validFireTargets.Clear();
            selectedFireTarget = null;
            if (selectedUnit == null || selectedUnit.Team != Team.Player || selectedUnit.ActionPoints <= 0)
                return;

            validFireTargets = GetValidFireTargets(selectedUnit);
            showFireTargets = validFireTargets.Count > 0;

            for (int i = 0; i < validFireTargets.Count; i++)
            {
                var target = validFireTargets[i];
                int d = Math.Abs(target.Cell.X - selectedUnit.Cell.X) + Math.Abs(target.Cell.Y - selectedUnit.Cell.Y);
                int chance = Math.Max(selectedUnit.WeaponData.Accuracy - d * 5, 10);

                fireTargetsUI.Add(new FireTargetUI
                {
                    Target = target,
                    HitChance = chance,
                    Bounds = new Rectangle(fireButton.X, fireButton.Y - 40 * (i + 1), fireButton.Width, 32)
                });
            }
        }

        private void UpdateFireTargetsUIPositions()
        {
            if (fireTargetsUI.Count == 0) return;

            int icon = 48, space = 10;
            int total = fireTargetsUI.Count * icon + (fireTargetsUI.Count - 1) * space;

            int startX, y;

            // Si une cible est sélectionnée et le bouton de confirmation existe
            if (selectedFireTarget != null && selectedUnit != null && selectedUnit.ActionPoints > 0)
            {
                // Positionner au-dessus du bouton CONFIRMER TIR
                int fireConfirmWidth = 140;
                int fireConfirmHeight = 50;
                int bw = ActionButtonWidth, bh = ActionButtonHeight;
                int by = GraphicsDevice.Viewport.Height - bh - 15;

                int fireButtonX = GraphicsDevice.Viewport.Width / 2 - fireConfirmWidth / 2;
                int fireButtonY = by - fireConfirmHeight - 10;

                startX = fireButtonX + (fireConfirmWidth - total) / 2;
                y = fireButtonY - icon - 10;
            }
            else
            {
                // Sinon, positionner au-dessus du bouton TIRER des actions
                int bw = ActionButtonWidth, bh = ActionButtonHeight;
                int by = GraphicsDevice.Viewport.Height - bh - 15;
                int bx = (GraphicsDevice.Viewport.Width - bw) / 2;

                startX = bx - 110 + (bw - total) / 2;
                y = by - icon - 10;
            }

            for (int i = 0; i < fireTargetsUI.Count; i++)
                fireTargetsUI[i].Bounds = new Rectangle(startX + i * (icon + space), y, icon, icon);
        }

        private bool IsMouseOverActionButton(MouseState mouse)
        {
            foreach (var btn in unitActionButtons)
                if (new Rectangle((int)btn.Position.X, (int)btn.Position.Y, ActionButtonWidth, ActionButtonHeight)
                    .Contains(mouse.Position))
                    return true;
            return false;
        }

        private List<EnemyTemplate> enemyPool = new()
        {
            new("Alien Grunt","Infantry","Plasma Rifle",3),
            new("Alien Sniper","Sniper","Plasma Sniper",2),
            new("Alien Heavy","Heavy","Plasma Cannon",2),
            new("Alien Scout","Scout","SMG Plasma",4),
            new("Zombie","Undead","Zombie Claws",2)
        };

        private void InitializeWeapons() => weaponDatabase = new Dictionary<string, WeaponData>
        {
            ["Rifle"] = new("Rifle", 25, 80, 5),
            ["Plasma Rifle"] = new("Plasma Rifle", 30, 75, 5),
            ["Plasma Sniper"] = new("Plasma Sniper", 50, 90, 8),
            ["Plasma Cannon"] = new("Plasma Cannon", 40, 70, 4),
            ["SMG Plasma"] = new("SMG Plasma", 15, 60, 6),
            ["Zombie Claws"] = new("Zombie Claws", 35, 70, 1),
            ["Shotgun"] = new("Shotgun", 45, 70, 3),
            ["SMG"] = new("SMG", 20, 75, 4)
        };

        private void HandleMainMenu(MouseState mouse)
        {
            foreach (var btn in menuButtons)
                if (btn.IsClicked(mouse, previousMouseState))
                    switch (btn.Text)
                    {
                        case "New Game":
                            currentState = GameState.MissionSelect;
                            Console.WriteLine("Opening mission select...");
                            break;

                        case "Continue":
                            if (!hasSavedGame) { Console.WriteLine("No saved game to continue!"); break; }
                            playerUnits = savedPlayerUnits.Select(u => new Unit(u)).ToList();
                            enemyUnits = savedEnemyUnits.Select(u => new Unit(u)).ToList();
                            currentState = GameState.Playing;
                            Console.WriteLine("Game continued!");
                            break;

                        case "Encyclopedia":
                            Console.WriteLine("Opening encyclopedia...");
                            break;

                        case "Options":
                            currentState = GameState.OptionsMenu;
                            Console.WriteLine("Options");
                            break;

                        case "Quit":
                            Exit();
                            break;
                    }
        }

        private void HandleMissionSelect(MouseState mouse)
        {
            foreach (var btn in missionButtons)
                if (btn.IsClicked(mouse, previousMouseState))
                    switch (btn.Text)
                    {
                        case "Tutorial":
                            selectedMission = "Tutorial";
                            StartMission(selectedMission);
                            break;

                        case "Survival":
                            selectedMission = "Survival";
                            StartMission(selectedMission);
                            break;

                        case "Assault":
                            selectedMission = "Assault";
                            StartMission(selectedMission);
                            break;

                        case "Defense":
                            selectedMission = "Defense";
                            StartMission(selectedMission);
                            break;

                        case "Back":
                            currentState = GameState.MainMenu;
                            break;
                    }
        }

        private void StartMission(string missionType)
        {
            MediaPlayer.Stop();
            currentState = GameState.Playing;
            LoadMap();
            CreateUnits(missionType);
            Console.WriteLine($"Mission '{missionType}' launched in 3D!");
        }

        private void HandleOptionsMenu(MouseState mouse)
        {
            foreach (var btn in optionsButtons)
                if (btn.IsClicked(mouse, previousMouseState))
                    switch (btn.Text)
                    {
                        case "Music Volume +":
                            musicVolume = Math.Min(musicVolume + 0.1f, 1f);
                            MediaPlayer.Volume = musicVolume;
                            Console.WriteLine("Volume: " + musicVolume);
                            break;
                        case "Music Volume -":
                            musicVolume = Math.Max(musicVolume - 0.1f, 0f);
                            MediaPlayer.Volume = musicVolume;
                            Console.WriteLine("Volume: " + musicVolume);
                            break;
                        case "Back":
                            currentState = GameState.MainMenu;
                            break;
                    }

            draggingVolume = mouse.LeftButton == ButtonState.Pressed && volumeBar.Contains(mouse.Position) || draggingVolume;
            if (mouse.LeftButton == ButtonState.Released) draggingVolume = false;

            if (draggingVolume)
            {
                musicVolume = MathHelper.Clamp((mouse.X - volumeBar.X) / (float)volumeBar.Width, 0f, 1f);
                MediaPlayer.Volume = musicVolume;
            }

            volumeFill = new Rectangle(volumeBar.X, volumeBar.Y, (int)(volumeBar.Width * musicVolume), volumeBar.Height);
            volumeHandle = new Rectangle(volumeBar.X + volumeFill.Width - 5, volumeBar.Y - 4, 10, volumeBar.Height + 8);
        }

        private void HandlePlayerTurn(MouseState mouse, bool leftClick, KeyboardState keyboard)
        {
            hoveredCell = GetCellFromMouseRaycast(mouse);

            currentPath.Clear();
            pathCosts.Clear();

            if (selectedUnit != null && selectedUnit.ActionPoints > 0 && hoveredCell.X != -1 &&
                cachedMovableCells.Contains(hoveredCell) && selectedUnit.Team == Team.Player)
            {
                currentPath = FindPathAStar(selectedUnit.Cell, hoveredCell, selectedUnit.MovementPoints);

                pathCosts.Clear();
                for (int i = 0; i < currentPath.Count; i++)
                {
                    pathCosts[currentPath[i]] = i + 1;
                }
            }

            // Mode lancer de grenade
            if (throwMode)
            {
                HandleGrenadeThrow(mouse, leftClick);
            }

            // Vérifier tous les éléments d'UI qui peuvent être cliqués
            bool clickOnUI = endTurnButton.Contains(mouse.Position) ||
                             fireButton.Contains(mouse.Position) ||
                             IsMouseOverActionButton(mouse) ||
                             IsMouseOverFireTargets(mouse) ||
                             (showInventory && inventoryPanel.Contains(mouse.Position));

            // Gérer les clics sur les boutons d'action AVANT de bloquer les clics UI
            if (leftClick)
                HandleUnitActionButtons(mouse);

            // Gérer les clics sur les cibles ennemies AVANT de bloquer les clics UI
            if (leftClick && showFireTargets)
            {
                foreach (var ui in fireTargetsUI)
                {
                    if (ui.Bounds.Contains(mouse.Position))
                    {
                        selectedFireTarget = ui.Target;

                        // NOUVEAU: Faire tourner l'unité vers la cible
                        if (selectedUnit != null && selectedFireTarget != null)
                        {
                            // En 3D MonoGame: X = gauche/droite, Z = haut/bas de la grille
                            // L'axe Z pointe vers le BAS de l'écran (Y positif en 2D)
                            float deltaX = selectedFireTarget.Cell.X - selectedUnit.Cell.X;
                            float deltaZ = selectedFireTarget.Cell.Y - selectedUnit.Cell.Y;

                            // Calculer l'angle en utilisant Z,X au lieu de Y,X
                            // Et ajouter Pi/2 pour que Z+ soit "devant"
                            selectedUnit.Orientation = (float)Math.Atan2(deltaX, deltaZ);

                            // Debug détaillé
                            Console.WriteLine($"=== ORIENTATION DEBUG ===");
                            Console.WriteLine($"Unit: {selectedUnit.Name} à ({selectedUnit.Cell.X}, {selectedUnit.Cell.Y})");
                            Console.WriteLine($"Target: {selectedFireTarget.Name} à ({selectedFireTarget.Cell.X}, {selectedFireTarget.Cell.Y})");
                            Console.WriteLine($"Delta X (gauche/droite): {deltaX}");
                            Console.WriteLine($"Delta Z (haut/bas grille): {deltaZ}");
                            Console.WriteLine($"Orientation (radians): {selectedUnit.Orientation}");
                            Console.WriteLine($"Orientation (degrés): {MathHelper.ToDegrees(selectedUnit.Orientation)}");
                            Console.WriteLine($"========================");
                        }

                        break;
                    }
                }
            }

            // Clic sur la grille seulement si pas sur l'UI
            if (leftClick && !clickOnUI && hoveredCell.X != -1)
                HandleGridClick(hoveredCell);

            if (mouse.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released)
                CancelSelection();

            fireButtonHovered = fireButton.Contains(mouse.Position);
            if (fireButtonHovered && leftClick && selectedUnit != null && selectedFireTarget != null && selectedUnit.ActionPoints > 0)
            {
                FireAtTarget(selectedUnit, selectedFireTarget);
                UpdateFireTargets();
            }

            endTurnHovered = endTurnButton.Contains(mouse.Position);
            if (endTurnHovered && leftClick && !isActionInProgress)
                StartEnemyTurn();
        }

        private bool IsMouseOverFireTargets(MouseState mouse)
        {
            if (!showFireTargets) return false;

            foreach (var ui in fireTargetsUI)
            {
                if (ui.Bounds.Contains(mouse.Position))
                    return true;
            }

            return false;
        }

        private void HandleGridClick(Point clickedCell)
        {
            Unit clickedUnit = GetUnitAtCell(clickedCell);

            if (clickedUnit != null)
            {
                selectedUnit = clickedUnit;
                if (selectedUnit.Team == Team.Player)
                {
                    cachedMovableCells = GetMovableCells(selectedUnit);
                    UpdateFireTargets();
                }
                else
                {
                    cachedMovableCells.Clear();
                    currentPath.Clear();
                    pathCosts.Clear();
                }
            }
            else if (selectedUnit != null && selectedUnit.ActionPoints > 0)
            {
                var movable = GetMovableCells(selectedUnit);
                if (movable.Contains(clickedCell))
                {
                    var path = FindPathAStar(selectedUnit.Cell, clickedCell, selectedUnit.MovementPoints);

                    if (path.Count > 0 && path.Count <= selectedUnit.MovementPoints)
                    {
                        selectedUnit.Cell = clickedCell;
                        selectedUnit.ActionPoints--;
                        UpdateFireTargets();
                        cachedMovableCells = selectedUnit.ActionPoints > 0 ? GetMovableCells(selectedUnit) : new List<Point>();
                        currentPath.Clear();
                        pathCosts.Clear();
                    }
                }
            }
        }

        private void HandleUnitActionButtons(MouseState mouse)
        {
            if (mouse.LeftButton != ButtonState.Pressed || previousMouseState.LeftButton != ButtonState.Released)
                return;

            foreach (var btn in unitActionButtons)
            {
                var rect = new Rectangle((int)btn.Position.X, (int)btn.Position.Y, ActionButtonWidth, ActionButtonHeight);
                if (rect.Contains(mouse.Position))
                {
                    switch (btn.Text)
                    {
                        case "TIRER":
                            if (selectedUnit != null && selectedUnit.ActionPoints > 0)
                            {
                                UpdateFireTargets();
                                if (validFireTargets.Count > 0)
                                {
                                    Console.WriteLine($"Mode tir activé - {validFireTargets.Count} cibles disponibles");
                                }
                                else
                                {
                                    Console.WriteLine("Aucune cible à portée");
                                }
                            }
                            break;

                        case "GRENADE":
                            if (selectedUnit != null && selectedUnit.Grenades.Count > 0)
                            {
                                throwMode = true;
                                selectedGrenade = selectedUnit.Grenades[0];
                                throwableCells = ThrowTrajectoryCalculator.GetThrowableCells(
                                    selectedUnit.Cell,
                                    MaxThrowRange,
                                    gridWidth,
                                    gridHeight
                                );
                                Console.WriteLine($"Mode grenade activé: {selectedGrenade.Name}");
                            }
                            break;

                        case "COUVERT":
                            Console.WriteLine("Action future : COUVERT");
                            break;

                        case "RECHARGER":
                            Console.WriteLine("Action future : RECHARGER");
                            break;
                    }

                    return; // Sortir dès qu'un bouton est cliqué
                }
            }
        }

        private void CancelSelection()
        {
            validFireTargets.Clear();
            selectedUnit = null;
            selectedFireTarget = null;
            cachedMovableCells.Clear();
            currentPath.Clear();
            pathCosts.Clear();
        }

        private void InitializeGrenades()
        {
            grenadeDatabase = GrenadeDatabase.GetAllGrenades();

            // Ajouter quelques grenades disponibles dans l'inventaire
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Frag Grenade"], new Point(50, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["HE Grenade"], new Point(110, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Plasma Grenade"], new Point(170, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Smoke Grenade"], new Point(230, 300)));
            availableGrenades.Add(new GrenadeItem(grenadeDatabase["Demolition Charge"], new Point(290, 300)));
        }

        private void EquipGrenadeToUnit(Unit unit, GrenadeData grenade)
        {
            if (unit.AddGrenade(grenade))
            {
                Console.WriteLine($"{unit.Name} equipped {grenade.Name}");
            }
            else
            {
                Console.WriteLine($"{unit.Name} grenade slots full!");
            }
        }

        private void HandleGrenadeThrow(MouseState mouse, bool leftClick)
        {
            if (selectedUnit == null || selectedGrenade == null) return;

            // Calculer la case survolée
            throwTarget = GetCellFromMouseRaycast(mouse);

            if (throwTarget.X >= 0)
            {
                // Mettre à jour la prévisualisation de l'explosion
                explosionPreview = ThrowTrajectoryCalculator.GetExplosionPreview(
                    throwTarget,
                    selectedGrenade.Radius,
                    gridWidth,
                    gridHeight
                );

                // Calculer la trajectoire pour affichage
                Vector3 startPos = new Vector3(
                    selectedUnit.Cell.X * cellSize + cellSize / 2f,
                    cellSize * 1.5f,
                    selectedUnit.Cell.Y * cellSize + cellSize / 2f
                );

                Vector3 targetPos = new Vector3(
                    throwTarget.X * cellSize + cellSize / 2f,
                    0,
                    throwTarget.Y * cellSize + cellSize / 2f
                );

                trajectoryPreview = ThrowTrajectoryCalculator.CalculateArcPoints(startPos, targetPos);
            }

            // Clic gauche pour lancer
            if (leftClick && throwTarget.X >= 0)
            {
                if (ThrowTrajectoryCalculator.IsInThrowRange(selectedUnit.Cell, throwTarget, MaxThrowRange))
                {
                    LaunchGrenade(selectedUnit, selectedGrenade, throwTarget);

                    selectedUnit.ActionPoints -= selectedGrenade.AOCost;
                    selectedUnit.RemoveGrenade(selectedGrenade);

                    // Quitter le mode lancer
                    throwMode = false;
                    selectedGrenade = null;
                    throwableCells.Clear();
                    explosionPreview.Clear();
                    trajectoryPreview.Clear();
                }
            }

        }

        private void LaunchGrenade(Unit thrower, GrenadeData grenadeData, Point targetCell)
        {
            Vector3 startPos = new Vector3(
                thrower.Cell.X * cellSize + cellSize / 2f,
                cellSize * 1.5f,
                thrower.Cell.Y * cellSize + cellSize / 2f
            );

            Vector3 targetPos = new Vector3(
                targetCell.X * cellSize + cellSize / 2f,
                0,
                targetCell.Y * cellSize + cellSize / 2f
            );

            Grenade grenade = new Grenade(grenadeData, startPos, targetPos, thrower);
            activeGrenades.Add(grenade);

            Console.WriteLine($"{thrower.Name} threw {grenadeData.Name} at {targetCell}");
        }

        private void UpdateGrenades(GameTime gameTime)
        {
            float grenadeSpeed = 2.5f;

            for (int i = activeGrenades.Count - 1; i >= 0; i--)
            {
                var grenade = activeGrenades[i];
                grenade.Progress += (float)gameTime.ElapsedGameTime.TotalSeconds * grenadeSpeed;

                if (grenade.Progress >= 1f)
                {
                    // Explosion!
                    Point explosionCell = new Point(
                        (int)(grenade.TargetPosition.X / cellSize),
                        (int)(grenade.TargetPosition.Z / cellSize)
                    );

                    TriggerExplosion(explosionCell, grenade.Data);
                    activeGrenades.RemoveAt(i);
                }
                else
                {
                    grenade.Position = grenade.GetCurrentPosition();
                }
            }

            // Vieillir les cratères
            foreach (var crater in craters)
            {
                crater.Age += (float)gameTime.ElapsedGameTime.TotalSeconds;
            }
        }

        private void TriggerExplosion(Point center, GrenadeData grenadeData)
        {
            Console.WriteLine($"EXPLOSION at {center} - {grenadeData.Name}");

            // Appliquer les dégâts aux unités
            List<Point> affectedCells = explosionManager.GetExplosionCells(center, grenadeData.Radius);

            foreach (var cell in affectedCells)
            {
                Unit unit = GetUnitAtCell(cell);
                if (unit != null)
                {
                    int damage = explosionManager.CalculateExplosionDamage(
                        grenadeData.Damage,
                        center,
                        cell,
                        grenadeData.Radius
                    );

                    unit.Health = Math.Max(0, unit.Health - damage);
                    Console.WriteLine($"{unit.Name} took {damage} explosion damage! HP: {unit.Health}");

                    if (unit.Health <= 0)
                    {
                        (unit.Team == Team.Player ? playerUnits : enemyUnits).Remove(unit);
                        Console.WriteLine($"{unit.Name} killed by explosion!");
                    }
                }
            }

            // Détruire les murs
            if (grenadeData.DestroyWalls)
            {
                List<WallSegment> destroyedWalls = explosionManager.GetDestroyedWalls(
                    wallSegments,
                    center,
                    grenadeData.Radius
                );

                foreach (var wall in destroyedWalls)
                {
                    wallSegments.Remove(wall);
                }

                Console.WriteLine($"Destroyed {destroyedWalls.Count} wall segments");
            }

            // Créer des cratères
            if (grenadeData.DigsTerrain)
            {
                List<Crater> newCraters = explosionManager.CreateCraters(
                    center,
                    grenadeData.DigDepth,
                    grenadeData.Radius
                );

                craters.AddRange(newCraters);
                Console.WriteLine($"Created {newCraters.Count} craters");
            }
        }

        private void DrawGrenades3D()
        {
            foreach (var grenade in activeGrenades)
            {
                Color grenadeColor = GrenadeDatabase.GetGrenadeColor(grenade.Data.Type);
                DrawCube(grenade.Position, new Vector3(cellSize * 0.2f), grenadeColor);
            }
        }

        private void DrawCraters3D()
        {
            foreach (var crater in craters)
            {
                Vector3 position = new Vector3(
                    crater.Cell.X * cellSize + cellSize / 2f,
                    -crater.Depth * 0.2f, // Enfoncer dans le sol
                    crater.Cell.Y * cellSize + cellSize / 2f
                );

                // Cratère plus foncé selon la profondeur
                Color craterColor = new Color(60, 50, 40) * (0.5f + crater.Depth * 0.15f);

                DrawPlane(position,
                         new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f),
                         craterColor);
            }
        }

        private void DrawThrowMode3D(GameTime gameTime)
        {
            if (!throwMode) return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.3f + 0.7f;

            // Dessiner les cases où on peut lancer
            foreach (var cell in throwableCells)
            {
                Vector3 position = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0.2f,
                    cell.Y * cellSize + cellSize / 2f
                );
                DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), Color.Yellow * 0.3f * pulse);
            }

            // Dessiner la zone d'explosion prévisionnelle
            foreach (var cell in explosionPreview)
            {
                Vector3 position = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0.25f,
                    cell.Y * cellSize + cellSize / 2f
                );
                DrawPlane(position, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), Color.Red * 0.5f * pulse);
            }

            // Dessiner la trajectoire
            for (int i = 0; i < trajectoryPreview.Count - 1; i++)
            {
                DrawCube(trajectoryPreview[i], new Vector3(cellSize * 0.1f), Color.White * 0.7f);
            }
        }

    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLASSES AUXILIAIRES
    // ═══════════════════════════════════════════════════════════════════════

    public class Button
    {
        public string Text;
        public Vector2 Position;
        public int Width = 100, Height = 36;
        private Rectangle bounds;
        public bool IsEnabled = true;

        public Button(string text, Vector2 pos) { Text = text; Position = pos; }

        public void Draw(SpriteBatch sb, SpriteFont font, MouseState mouse)
        {
            Vector2 size = font.MeasureString(Text);
            bounds = new Rectangle((int)Position.X, (int)Position.Y, (int)size.X, (int)size.Y);
            Color c = !IsEnabled ? Color.Gray : bounds.Contains(mouse.Position) ? Color.Yellow : Color.White;
            sb.DrawString(font, Text, Position, c);
        }

        public bool IsClicked(MouseState cur, MouseState prev)
            => IsEnabled && bounds.Contains(cur.Position) &&
               cur.LeftButton == ButtonState.Pressed && prev.LeftButton == ButtonState.Released;
    }

    public enum Team { Player, Enemy }

    public class EnemyTemplate
    {
        public string Name, Class, Weapon;
        public int ActionPoints;
        public EnemyTemplate(string name, string unitClass, string weapon, int ap)
        { Name = name; Class = unitClass; Weapon = weapon; ActionPoints = ap; }
    }

    public class WeaponData
    {
        public string Name;
        public int Damage, Accuracy, Range;
        public WeaponData(string name, int damage, int accuracy, int range)
        { Name = name; Damage = damage; Accuracy = accuracy; Range = range; }
    }

    class FireTargetUI { public Unit Target; public Rectangle Bounds; public int HitChance; }

    public static class Extensions { public static Vector2 ToVector2(this Point p) => new(p.X, p.Y); }
}
