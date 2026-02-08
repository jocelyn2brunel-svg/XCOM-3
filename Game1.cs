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

        private Model cubeModel;
        private Model planeModel;        

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
        private InventoryGrid inventoryGrid;
        private GridItem draggedItem = null;
        private Point dragGridOffset;  // Offset en cases de grille
        private const int GRID_WIDTH = 10;   // Largeur de la grille en cases
        private const int GRID_HEIGHT = 8;   // Hauteur de la grille en cases
        private const int CELL_SIZE = 40;    // Taille d'une case en pixels    

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
        enum GameState { MainMenu, MissionSelect, Playing, OptionsMenu, GameOver, Encyclopedia }
        private GameState currentState = GameState.MainMenu;

        // --- Grille 3D ---
        private int cellSize = 2;
        private int gridWidth = 50;
        private int gridHeight = 50;
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

        // --- Encyclopédie ---
        private List<Button> encyclopediaButtons;
        private string encyclopediaCategory = "Weapons"; // "Weapons", "Armor", "Units"

        // ═══ NOUVEAUX CHAMPS POUR OPTIMISATIONS ═══

        // Batch renderer pour les unités (remplace les draw calls multiples)
        private HumanoidBatchRenderer humanoidBatcher;

        // Système de spatial hash et cache
        private OptimizedUnitManager unitManager;

        // FPS counter pour mesurer les performances
        private int frameCount = 0;
        private float fpsElapsedTime = 0f;
        private float currentFPS = 60f;

        // --- Systèmes séparés ---
        private CameraController camera;
        private Renderer3D renderer3D;
        private PathfindingSystem pathfinding;


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
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            font = Content.Load<SpriteFont>("Arial");

            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            tileTexture = Content.Load<Texture2D>("TileParchment32x32");

            // Initialiser les systèmes
            renderer3D = new Renderer3D(GraphicsDevice);
            camera = new CameraController(
                gridWidth,
                gridHeight,
                cellSize,
                GraphicsDevice.Viewport.AspectRatio
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

            string[] encyclopediaLabels = { "Armes", "Armures", "Unités", "Retour" };
            encyclopediaButtons = encyclopediaLabels
                .Select((text, i) => new Button(text, new Vector2(0, 100 + i * 28)))
                .ToList();

            volumeBar = new Rectangle(0, 134, 200, 8);

            Window.ClientSizeChanged += (s, e) =>
            {
                UpdateFireTargetsUIPositions();
                camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio); // ← MODIFIER
            };

            InitializeWeapons();
            InitializeItems();
            // Initialiser la grille d'inventaire
            inventoryGrid = new InventoryGrid(GRID_WIDTH, GRID_HEIGHT);

            InitializeInventoryItems();

            InitializeGrenades();
            explosionManager = new ExplosionManager(random);

            // Initialiser le générateur de murs sur edges
            edgeWallGenerator = new EdgeWallGenerator(random);

            // ═══ INITIALISATION DES OPTIMISATIONS ═══

            humanoidBatcher = new HumanoidBatchRenderer();
            unitManager = new OptimizedUnitManager();

            Console.WriteLine("[OPTIMIZATION] Batch renderer and spatial hash initialized");

        }

        protected override void Update(GameTime gameTime)
        {

            // ═══ CALCULER FPS ═══
            fpsElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            frameCount++;

            if (fpsElapsedTime >= 1.0f)
            {
                currentFPS = frameCount / fpsElapsedTime;
                frameCount = 0;
                fpsElapsedTime = 0f;

                // Optionnel : log dans la console
                Console.WriteLine($"FPS: {currentFPS:F1}");
            }

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
                        HandleInventory(mouse, leftClick, keyboard);
                        if (escapePressed) showInventory = false;
                    }
                    else
                    {
                        UpdateUnitAnimations(gameTime);

                        if (currentTurn == TurnState.PlayerTurn) HandlePlayerTurn(mouse, leftClick, keyboard);
                        else if (currentTurn == TurnState.EnemyTurn) UpdateEnemyTurn();

                        camera.HandleControls(keyboard, mouse, previousMouseState, gameTime);
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

                case GameState.Encyclopedia:
                    HandleEncyclopedia(mouse);
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

        private void UpdateUnitAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var unit in AllUnits())
            {
                unit.UpdateAnimation(deltaTime);
            }
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
            // L'éclairage sera appliqué dans Draw() via renderer3D.SetLighting()

        }

        private void HandleEncyclopedia(MouseState mouse)
        {
            foreach (var btn in encyclopediaButtons)
                if (btn.IsClicked(mouse, previousMouseState))
                    switch (btn.Text)
                    {
                        case "Armes":
                            encyclopediaCategory = "Weapons";
                            Console.WriteLine("Affichage des armes");
                            break;

                        case "Armures":
                            encyclopediaCategory = "Armor";
                            Console.WriteLine("Affichage des armures");
                            break;

                        case "Unités":
                            encyclopediaCategory = "Units";
                            Console.WriteLine("Affichage des unités");
                            break;

                        case "Retour":
                            currentState = GameState.MainMenu;
                            break;
                    }
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
                camera.UpdateCamera();
                renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);
                renderer3D.SetLighting(ambientLight, directionalLight);

                RasterizerState rasterizerState = new RasterizerState();
                rasterizerState.CullMode = CullMode.None;
                GraphicsDevice.RasterizerState = rasterizerState;

                GraphicsDevice.DepthStencilState = DepthStencilState.Default;

                renderer3D.DrawGrid(gridWidth, gridHeight, cellSize, tileTexture);
                renderer3D.DrawWalls(wallSegments, cellSize);

                // Dessiner toutes les unités
                foreach (var unit in playerUnits)
                    renderer3D.DrawUnit(unit, cellSize);

                foreach (var unit in enemyUnits)
                    renderer3D.DrawUnit(unit, cellSize);

                // Indicateur de sélection
                if (selectedUnit != null)
                {
                    renderer3D.DrawSelectionIndicator(selectedUnit, cellSize,
                        new Color(0, 255, 255, 128));
                }

                Unit targetHighlight = selectedFireTarget ?? hoveredFireTarget;
                if (targetHighlight != null)
                {
                    renderer3D.DrawSelectionIndicator(targetHighlight, cellSize,
                        new Color(255, 0, 0, 128), 1.2f);
                }

                renderer3D.DrawCraters(craters, cellSize);
                renderer3D.DrawGrenades(activeGrenades, cellSize);
                DrawMovableCells3D(gameTime);
                DrawPath3D(gameTime);
                DrawHoveredCell3D(gameTime);
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

                case GameState.Encyclopedia:
                    DrawEncyclopedia();
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

            // ═══ AFFICHER LE FPS EN OVERLAY (haut à droite) ═══

            // Texte FPS
            string fpsText = $"FPS: {currentFPS:F0}";
            Vector2 fpsSize = font.MeasureString(fpsText);

            // Largeur de l’écran / fenêtre
            int screenWidth = GraphicsDevice.Viewport.Width;

            // Position : coin haut droit avec marge de 10px
            Vector2 fpsPosition = new Vector2(
                screenWidth - fpsSize.X - 10,
                10
            );

            _spriteBatch.DrawString(font, fpsText, fpsPosition, Color.Yellow);

            // Stats optionnelles (juste en dessous)
            string statsText = $"Units: {playerUnits.Count + enemyUnits.Count}";
            Vector2 statsSize = font.MeasureString(statsText);

            Vector2 statsPosition = new Vector2(
                screenWidth - statsSize.X - 10,
                10 + fpsSize.Y + 5
            );

            _spriteBatch.DrawString(font, statsText, statsPosition, Color.White);


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
            itemDatabase = new Dictionary<string, ItemData>();

            // Armes (code existant)
            itemDatabase["Rifle"] = new ItemData("Rifle", ItemType.Weapon,
                new WeaponData("Rifle", 25, 80, 5));
            itemDatabase["Plasma Rifle"] = new ItemData("Plasma Rifle", ItemType.Weapon,
                new WeaponData("Plasma Rifle", 30, 75, 5));
            itemDatabase["Plasma Sniper"] = new ItemData("Plasma Sniper", ItemType.Weapon,
                new WeaponData("Plasma Sniper", 50, 90, 8));
            itemDatabase["Shotgun"] = new ItemData("Shotgun", ItemType.Weapon,
                new WeaponData("Shotgun", 45, 70, 3));
            itemDatabase["SMG"] = new ItemData("SMG", ItemType.Weapon,
                new WeaponData("SMG", 20, 75, 4));

            // NOUVEAU: Charger toutes les armures de la base de données
            foreach (var armor in ArmorDatabase.GetAllArmors())
            {
                itemDatabase[armor.Name] = armor;
            }
        }

        private void InitializeInventoryItems()
        {
            inventoryGrid.Clear();

            // Ajouter des items avec leurs tailles réelles
            var items = new List<(string name, Point pos, bool rotated)>
    {
        // Armes (longues, placées verticalement ou horizontalement)
        ("Plasma Rifle", new Point(0, 0), false),
        ("Shotgun", new Point(2, 0), false),
        ("SMG", new Point(4, 0), false),
        
        // Casques (2x2)
        ("PASGT Helmet", new Point(0, 5), false),
        ("ACH", new Point(2, 5), false),
        ("ECH", new Point(4, 5), false),
        
        // Gilets (2x3)
        ("PASGT Vest", new Point(6, 0), false),
        ("OTV (IBA)", new Point(8, 0), false),
        ("MTV", new Point(6, 3), false),
        
        // Gilets avec plaques
        ("OTV + SAPI", new Point(8, 3), false),
        ("IOTV + ESAPI", new Point(6, 6), false),
        
        // Équipement spécial
        ("Riot Shield", new Point(0, 4), true),  // Tourné
        ("Army Combat Shirt", new Point(8, 6), false),
    };

            foreach (var (name, pos, rotated) in items)
            {
                if (itemDatabase.ContainsKey(name))
                {
                    ItemSize size = ItemSizeDatabase.GetItemSize(name);
                    GridItem gridItem = new GridItem(itemDatabase[name], pos, size, rotated);
                    gridItem.UpdatePixelBounds();

                    if (!inventoryGrid.PlaceItem(gridItem))
                    {
                        Console.WriteLine($"Impossible de placer {name} à {pos}");
                    }
                }
            }
        }

        private void HandleInventory(MouseState mouse, bool leftClick, KeyboardState keyboard)
        {
            if (selectedUnit == null) return;

            // Position de départ de la grille en pixels
            int gridStartX = GraphicsDevice.Viewport.Width / 2 - 300 + 20;
            int gridStartY = GraphicsDevice.Viewport.Height / 2 - 200 + 60;

            // ROTATION avec touche R
            bool rPressed = keyboard.IsKeyDown(Keys.R) && previousKeyboardState.IsKeyUp(Keys.R);
            if (rPressed && draggedItem != null)
            {
                draggedItem.Rotate();
                Console.WriteLine($"Item tourné: {draggedItem.Data.Name}");
            }

            // ═══════════════════════════════════════════════════════════════════
            // DÉMARRER LE DRAG
            // ═══════════════════════════════════════════════════════════════════
            if (leftClick && draggedItem == null)
            {
                // Convertir position souris en position grille
                int gridX = (mouse.X - gridStartX) / CELL_SIZE;
                int gridY = (mouse.Y - gridStartY) / CELL_SIZE;

                if (gridX >= 0 && gridX < GRID_WIDTH && gridY >= 0 && gridY < GRID_HEIGHT)
                {
                    GridItem clickedItem = inventoryGrid.GetItemAt(new Point(gridX, gridY));

                    if (clickedItem != null)
                    {
                        draggedItem = clickedItem;
                        dragGridOffset = new Point(gridX - clickedItem.GridPosition.X,
                                                  gridY - clickedItem.GridPosition.Y);
                        inventoryGrid.RemoveItem(draggedItem);
                    }
                }

                // Vérifier les slots équipés
                if (draggedItem == null)
                {
                    CheckEquipmentSlotDrag(mouse, selectedUnit.EquippedWeapon, GetWeaponSlotBounds());
                    CheckEquipmentSlotDrag(mouse, selectedUnit.EquippedHelmet, GetHelmetSlotBounds());
                    CheckEquipmentSlotDrag(mouse, selectedUnit.EquippedArmor, GetArmorSlotBounds());
                    CheckEquipmentSlotDrag(mouse, selectedUnit.EquippedShield, GetShieldSlotBounds());
                    CheckEquipmentSlotDrag(mouse, selectedUnit.EquippedShirt, GetShirtSlotBounds());
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // DRAG EN COURS
            // ═══════════════════════════════════════════════════════════════════
            if (draggedItem != null && mouse.LeftButton == ButtonState.Pressed)
            {
                // Mettre à jour la position du drag (en cases de grille)
                int newGridX = (mouse.X - gridStartX) / CELL_SIZE - dragGridOffset.X;
                int newGridY = (mouse.Y - gridStartY) / CELL_SIZE - dragGridOffset.Y;

                draggedItem.GridPosition = new Point(
                    Math.Max(0, Math.Min(GRID_WIDTH - 1, newGridX)),
                    Math.Max(0, Math.Min(GRID_HEIGHT - 1, newGridY))
                );

                draggedItem.UpdatePixelBounds(gridStartX, gridStartY);
            }

            // ═══════════════════════════════════════════════════════════════════
            // TERMINER LE DRAG
            // ═══════════════════════════════════════════════════════════════════
            if (draggedItem != null && mouse.LeftButton == ButtonState.Released)
            {
                bool equipped = false;

                // Essayer d'équiper dans les slots
                equipped = TryEquipInSlot(mouse, draggedItem, selectedUnit);

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
                            Console.WriteLine("Pas de place dans l'inventaire!");
                            // L'item est perdu... ou remettre à l'ancienne position
                        }
                    }
                }

                draggedItem = null;
            }
        }

        // Méthodes helper
        private void CheckEquipmentSlotDrag(MouseState mouse, Item equippedItem, Rectangle slot)
        {
            if (equippedItem != null && slot.Contains(mouse.Position) && draggedItem == null)
            {
                ItemSize size = ItemSizeDatabase.GetItemSize(equippedItem.Data.Name);
                draggedItem = new GridItem(equippedItem.Data, new Point(0, 0), size, false);

                // Déséquiper
                if (equippedItem == selectedUnit.EquippedWeapon) selectedUnit.EquippedWeapon = null;
                else if (equippedItem == selectedUnit.EquippedHelmet) selectedUnit.EquippedHelmet = null;
                else if (equippedItem == selectedUnit.EquippedArmor) selectedUnit.EquippedArmor = null;
                else if (equippedItem == selectedUnit.EquippedShield) selectedUnit.EquippedShield = null;
                else if (equippedItem == selectedUnit.EquippedShirt) selectedUnit.EquippedShirt = null;
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
                UpdateFireTargets();
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

        private Rectangle GetShieldSlotBounds()
        {
            int panelX = GraphicsDevice.Viewport.Width / 2 - 300;
            int panelY = GraphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 550, panelY + 100, 80, 80);
        }

        private Rectangle GetShirtSlotBounds()
        {
            int panelX = GraphicsDevice.Viewport.Width / 2 - 300;
            int panelY = GraphicsDevice.Viewport.Height / 2 - 200;
            return new Rectangle(panelX + 550, panelY + 200, 80, 80);
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
                    foreach (var item in inventoryGrid.GetAllItems())
                    {
                        if (item.PixelBounds.Intersects(testRect))
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

            int panelWidth = 750;
            int panelHeight = 500;
            int panelX = GraphicsDevice.Viewport.Width / 2 - panelWidth / 2;
            int panelY = GraphicsDevice.Viewport.Height / 2 - panelHeight / 2;

            // Fond du panneau
            Rectangle panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);
            _spriteBatch.Draw(pixel, panel, new Color(20, 20, 20, 240));
            DrawRectangleBorder(panel, Color.Gold, 3);

            // Titre
            string title = $"Inventaire - {selectedUnit.Name}";
            Vector2 titleSize = font.MeasureString(title);
            _spriteBatch.DrawString(font, title,
                new Vector2(panel.Center.X - titleSize.X * 0.75f, panelY + 10),
                Color.Gold, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);

            // ═══════════════════════════════════════════════════════════════════
            // GRILLE D'INVENTAIRE (style Diablo)
            // ═══════════════════════════════════════════════════════════════════
            int gridStartX = panelX + 20;
            int gridStartY = panelY + 60;
            int gridPixelWidth = GRID_WIDTH * CELL_SIZE;
            int gridPixelHeight = GRID_HEIGHT * CELL_SIZE;

            Rectangle gridArea = new Rectangle(gridStartX, gridStartY, gridPixelWidth, gridPixelHeight);

            // Fond de la grille
            _spriteBatch.Draw(pixel, gridArea, new Color(40, 40, 40, 200));

            // Dessiner les lignes de la grille
            for (int x = 0; x <= GRID_WIDTH; x++)
            {
                Rectangle vertLine = new Rectangle(
                    gridStartX + x * CELL_SIZE,
                    gridStartY,
                    1,
                    gridPixelHeight
                );
                _spriteBatch.Draw(pixel, vertLine, new Color(60, 60, 60));
            }

            for (int y = 0; y <= GRID_HEIGHT; y++)
            {
                Rectangle horizLine = new Rectangle(
                    gridStartX,
                    gridStartY + y * CELL_SIZE,
                    gridPixelWidth,
                    1
                );
                _spriteBatch.Draw(pixel, horizLine, new Color(60, 60, 60));
            }

            DrawRectangleBorder(gridArea, Color.Gray, 2);

            // Label
            _spriteBatch.DrawString(font, "Items Disponibles:",
                new Vector2(gridStartX, gridStartY - 20), Color.White);

            // Dessiner les items dans la grille
            foreach (var item in inventoryGrid.GetAllItems())
            {
                if (item != draggedItem)
                {
                    item.UpdatePixelBounds(gridStartX, gridStartY);
                    DrawGridItem(item);
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // SLOTS D'ÉQUIPEMENT
            // ═══════════════════════════════════════════════════════════════════
            int equipX = gridStartX + gridPixelWidth + 30;
            int equipY = gridStartY;

            Rectangle equipArea = new Rectangle(equipX, equipY, 190, 400);
            _spriteBatch.Draw(pixel, equipArea, new Color(40, 40, 40, 200));
            DrawRectangleBorder(equipArea, Color.Gray, 2);

            _spriteBatch.DrawString(font, "Equipement:",
                new Vector2(equipX + 5, equipY - 20), Color.White);

            DrawEquipmentSlot(GetWeaponSlotBounds(), "Arme", selectedUnit.EquippedWeapon);
            DrawEquipmentSlot(GetHelmetSlotBounds(), "Casque", selectedUnit.EquippedHelmet);
            DrawEquipmentSlot(GetArmorSlotBounds(), "Gilet", selectedUnit.EquippedArmor);
            DrawEquipmentSlot(GetShieldSlotBounds(), "Bouclier", selectedUnit.EquippedShield);
            DrawEquipmentSlot(GetShirtSlotBounds(), "Chemise", selectedUnit.EquippedShirt);

            // ═══════════════════════════════════════════════════════════════════
            // ITEM EN COURS DE DRAG
            // ═══════════════════════════════════════════════════════════════════
            if (draggedItem != null)
            {
                DrawGridItem(draggedItem, 0.7f);
            }

            // ═══════════════════════════════════════════════════════════════════
            // INSTRUCTIONS
            // ═══════════════════════════════════════════════════════════════════
            _spriteBatch.DrawString(font, "Glissez pour équiper | R: Tourner",
                new Vector2(panelX + 20, panelY + panelHeight - 30), Color.Yellow);
            _spriteBatch.DrawString(font, "I ou ESC pour fermer",
                new Vector2(panelX + 20, panelY + panelHeight - 15), Color.Yellow);
        }

        private void DrawGridItem(GridItem item, float alpha = 1f)
        {
            Color itemColor = ItemSizeDatabase.GetItemColor(item.Data.Type) * alpha;

            // Fond de l'item
            _spriteBatch.Draw(pixel, item.PixelBounds, itemColor);
            DrawRectangleBorder(item.PixelBounds, Color.White * alpha, 2);

            // Nom de l'item (réduit pour s'adapter)
            string name = item.Data.Name;
            Vector2 nameSize = font.MeasureString(name);
            float scale = Math.Min(0.6f, (item.PixelBounds.Width - 10) / nameSize.X);

            _spriteBatch.DrawString(font, name,
                new Vector2(item.PixelBounds.X + 5, item.PixelBounds.Y + 5),
                Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Info (armure ou dégâts)
            string info = item.Data.Type == ItemType.Weapon ?
                $"Dmg:{item.Data.WeaponData?.Damage}" :
                $"Arm:{item.Data.ArmorValue}";

            _spriteBatch.DrawString(font, info,
                new Vector2(item.PixelBounds.X + 5, item.PixelBounds.Bottom - 20),
                Color.Yellow * alpha, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
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

            if (equippedItem != null && draggedItem == null)
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

                // NOUVEAU: Afficher la description au survol
                MouseState mouse = Mouse.GetState();
                if (slot.Contains(mouse.Position) && !string.IsNullOrEmpty(equippedItem.Data.Description))
                {
                    // Afficher un tooltip avec la description
                    // (À implémenter selon vos préférences UI)
                }
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

        private void DrawMovableCells3D(GameTime gameTime)
        {
            if (selectedUnit != null && selectedUnit.ActionPoints > 0 &&
                currentTurn == TurnState.PlayerTurn && selectedUnit.Team == Team.Player)
            {
                float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 3f) * 0.3f + 0.7f;

                foreach (var cell in cachedMovableCells)
                {
                    Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, 0.05f, cell.Y * cellSize + cellSize / 2f);
                    renderer3D.DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), Color.Green * pulse);
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

                renderer3D.DrawPlane(position, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), pathColor);
            }
        }

        private void DrawHoveredCell3D(GameTime gameTime)
        {
            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f) * 0.3f + 0.7f;
            Vector3 position = new Vector3(hoveredCell.X * cellSize + cellSize / 2f, 0.15f, hoveredCell.Y * cellSize + cellSize / 2f);

            renderer3D.DrawPlane(position, new Vector3(cellSize, 1, cellSize), Color.Yellow * pulse);
        }

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

            int m = 10, w = 300, h = 200;
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

            int mobilityPenalty = selectedUnit.GetMobilityPenalty();
            if (mobilityPenalty > 0)
            {
                _spriteBatch.DrawString(font, $"Mobilite: -{mobilityPenalty} PM",
                                       p + new Vector2(0, 120), Color.Orange);
            }

            // Afficher le niveau global
            _spriteBatch.DrawString(font, $"Niveau: {selectedUnit.Skills.OverallLevel}",
                                   p + new Vector2(0, 140), Color.Gold);

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
            Vector2 grenadePos = p + new Vector2(0, 160);
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

        private void DrawEncyclopedia()
        {
            MouseState mouse = Mouse.GetState();

            // Titre
            _spriteBatch.DrawString(font, "ENCYCLOPEDIE", Vector2.Zero, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);

            // Boutons de catégorie à gauche
            foreach (var button in encyclopediaButtons)
                button.Draw(_spriteBatch, font, mouse);

            // Zone de contenu à droite
            int contentX = 250;
            int contentY = 100;
            int contentWidth = GraphicsDevice.Viewport.Width - contentX - 20;
            int lineHeight = 25;
            int y = contentY;

            switch (encyclopediaCategory)
            {
                case "Weapons":
                    _spriteBatch.DrawString(font, "=== ARMES ===", new Vector2(contentX, y), Color.Yellow, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                    y += 40;

                    foreach (var weapon in weaponDatabase.Values.OrderBy(w => w.Name))
                    {
                        _spriteBatch.DrawString(font, weapon.Name, new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
                        y += lineHeight;

                        _spriteBatch.DrawString(font, $"  Dégâts: {weapon.Damage}", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight;

                        _spriteBatch.DrawString(font, $"  Précision: {weapon.Accuracy}%", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight;

                        _spriteBatch.DrawString(font, $"  Portée: {weapon.Range} cases", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight + 10;
                    }
                    break;

                case "Armor":
                    _spriteBatch.DrawString(font, "=== ARMURES ===", new Vector2(contentX, y), Color.Yellow, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                    y += 40;

                    var armors = itemDatabase.Values.Where(item => item.Type == ItemType.Armor).OrderBy(a => a.Name);

                    foreach (var armor in armors)
                    {
                        _spriteBatch.DrawString(font, armor.Name, new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
                        y += lineHeight;

                        _spriteBatch.DrawString(font, $"  Protection: {armor.ArmorValue}", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight;

                        string slot = armor.ArmorSlot == ArmorSlot.Head ? "Tête" : "Torse";
                        _spriteBatch.DrawString(font, $"  Emplacement: {slot}", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight + 10;
                    }
                    break;

                case "Units":
                    _spriteBatch.DrawString(font, "=== UNITÉS ===", new Vector2(contentX, y), Color.Yellow, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                    y += 40;

                    // Unités joueur
                    _spriteBatch.DrawString(font, "ÉQUIPE JOUEUR:", new Vector2(contentX, y), Color.Blue, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);
                    y += 35;

                    _spriteBatch.DrawString(font, "Soldat", new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
                    y += lineHeight;
                    _spriteBatch.DrawString(font, "  Classe: Assault", new Vector2(contentX + 20, y), Color.White);
                    y += lineHeight;
                    _spriteBatch.DrawString(font, "  Arme de base: Rifle", new Vector2(contentX + 20, y), Color.White);
                    y += lineHeight;
                    _spriteBatch.DrawString(font, "  PV: 100", new Vector2(contentX + 20, y), Color.White);
                    y += lineHeight;
                    _spriteBatch.DrawString(font, "  PA: 3 par tour", new Vector2(contentX + 20, y), Color.White);
                    y += lineHeight + 20;

                    // Unités ennemies
                    _spriteBatch.DrawString(font, "ENNEMIS:", new Vector2(contentX, y), Color.Red, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);
                    y += 35;

                    foreach (var enemy in enemyPool)
                    {
                        _spriteBatch.DrawString(font, enemy.Name, new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
                        y += lineHeight;

                        _spriteBatch.DrawString(font, $"  Classe: {enemy.Class}", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight;

                        _spriteBatch.DrawString(font, $"  Arme: {enemy.Weapon}", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight;

                        var weapon = weaponDatabase[enemy.Weapon];
                        _spriteBatch.DrawString(font, $"  Dégâts: {weapon.Damage} | Portée: {weapon.Range}", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight;

                        _spriteBatch.DrawString(font, $"  PA: {enemy.ActionPoints} par tour", new Vector2(contentX + 20, y), Color.White);
                        y += lineHeight + 15;
                    }
                    break;
            }

            // Instructions
            _spriteBatch.DrawString(font, "Appuyez sur ESC pour retourner au menu",
                new Vector2(10, GraphicsDevice.Viewport.Height - 30), Color.Yellow);
        }


        // ═══════════════════════════════════════════════════════════════════════
        // LOGIQUE DE JEU
        // ═══════════════════════════════════════════════════════════════════════

        private void LoadMap()
        {
            gridWidth = random.Next(20, 50);
            gridHeight = random.Next(20, 50);
            cellSize = 2;

            timeOfDay = (float)random.NextDouble();
            dayNightSpeed = 1f / 86400f;

            GenerateWalls(gridWidth * gridHeight / 10);
            Console.WriteLine($"Map loaded: {gridWidth}x{gridHeight}, Starting time: {GetTimeOfDayString(timeOfDay)}");

            // Mettre à jour le pathfinding pour la nouvelle grille
            if (pathfinding != null)
                pathfinding.UpdateGrid(gridWidth, gridHeight);
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
            // Initialiser les positions visuelles de toutes les unités
            foreach (var unit in playerUnits)
            {
                unit.UpdateVisualPosition(cellSize);
                unit.TargetPosition = unit.VisualPosition;
            }

            foreach (var unit in enemyUnits)
            {
                unit.UpdateVisualPosition(cellSize);
                unit.TargetPosition = unit.VisualPosition;
            }

            Console.WriteLine($"Units created for {missionType}: 6 player, {enemyUnits.Count} enemy");
        }

        private IEnumerable<Unit> AllUnits()
        {
            foreach (var u in playerUnits) yield return u;
            foreach (var u in enemyUnits) yield return u;
        }

        Unit GetUnitAtCell(Point cell)
        {
            return unitManager.SpatialHash.GetUnitAt(cell);
        }

        private void HandleFire(Unit s)
        {
            if (s.ActionPoints <= 0) return;

            foreach (var t in AllUnits())
            {
                if (t.Team == s.Team) continue;

                int d = Math.Abs(t.Cell.X - s.Cell.X) + Math.Abs(t.Cell.Y - s.Cell.Y);
                if (d > s.WeaponData.Range || !pathfinding.HasLineOfSight(s.Cell, t.Cell)) continue;

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
            foreach (var u in playerUnits)
            {
                u.ActionPoints = 3;
                u.MovementPoints = u.GetMaxMovementPoints(); // Avec bonus d'Endurance
            }
            selectedUnit = null;
            cachedMovableCells.Clear();
            currentPath.Clear();
            pathCosts.Clear();
            currentTurn = TurnState.PlayerTurn;
            // Rafraîchir le cache car les PM ont été restaurés
            unitManager.OnNewTurn();
        }

        private void StartEnemyTurn()
        {
            foreach (var u in enemyUnits) u.ActionPoints = 2;
            enemyTurnIndex = 0;
            currentTurn = TurnState.EnemyTurn;
            unitManager.OnNewTurn();

        }

        private void UpdateEnemyTurn()
        {
            if (enemyTurnIndex >= enemyUnits.Count) { StartPlayerTurn(); return; }

            Unit enemy = enemyUnits[enemyTurnIndex];
            if (enemy.IsFiring) return;

            // Attendre que l'animation de déplacement soit terminée
            if (enemy.IsMoving) return;

            if (enemy.ActionPoints <= 0) { enemyTurnIndex++; return; }

            // ═══════════════════════════════════════════════════════════════
            // NOUVELLE LOGIQUE DE CIBLAGE - Intelligente et variée
            // ═══════════════════════════════════════════════════════════════

            Unit bestTarget = SelectBestTarget(enemy);

            if (bestTarget == null)
            {
                enemyTurnIndex++;
                return;
            }

            // 10% de chance de choisir une cible aléatoire (comportement imprévisible)
            if (random.Next(100) < 10 && playerUnits.Count > 1)
            {
                int randomIndex = random.Next(playerUnits.Count);
                bestTarget = playerUnits[randomIndex];
                Console.WriteLine($"{enemy.Name} change de cible de manière imprévisible!");
            }

            // Calculer distance vers la cible
            int distanceToTarget = Math.Abs(bestTarget.Cell.X - enemy.Cell.X) +
                                  Math.Abs(bestTarget.Cell.Y - enemy.Cell.Y);

            // Si à portée ET avec ligne de vue → TIRER
            if (distanceToTarget <= enemy.WeaponData.Range &&
                pathfinding.HasLineOfSight(enemy.Cell, bestTarget.Cell))
            {
                // Tourner vers la cible avant de tirer
                float deltaX = bestTarget.Cell.X - enemy.Cell.X;
                float deltaZ = bestTarget.Cell.Y - enemy.Cell.Y;
                enemy.TargetOrientation = (float)Math.Atan2(deltaX, deltaZ);

                HandleFire(enemy);
                return;
            }

            // Sinon → SE DÉPLACER vers la cible
            // ═══════════════════════════════════════════════════════════════
            // FIX: Pathfinding qui évite les autres unités
            // ═══════════════════════════════════════════════════════════════

            var path = pathfinding.FindPath(enemy.Cell, bestTarget.Cell, int.MaxValue, enemy);

            if (path.Count > 0)
            {
                int steps = Math.Min(enemy.MovementPoints, path.Count);

                // ═══ FIX CRITIQUE : Vérifier CHAQUE case du chemin ═══
                Point? validTargetCell = null;

                // Parcourir le chemin de la fin vers le début pour trouver 
                // la case la plus éloignée qui soit libre
                for (int i = steps - 1; i >= 0; i--)
                {
                    Point candidateCell = path[i];

                    // Vérifier si la case est libre (pas d'autre unité)
                    if (GetUnitAtCell(candidateCell) == null)
                    {
                        validTargetCell = candidateCell;
                        break;
                    }
                }

                // Si on a trouvé une case libre sur le chemin
                if (validTargetCell.HasValue)
                {
                    Point oldCell = enemy.Cell;
                    enemy.StartMoveTo(validTargetCell.Value, cellSize);
                    unitManager.OnUnitMoved(enemy, validTargetCell.Value); // ← AJOUTEZ CETTE LIGNE
                    enemy.ActionPoints--;
                }
                else
                {
                    // Aucune case libre sur le chemin → essayer un déplacement simple
                    TrySimpleMove(enemy, bestTarget);
                }
            }
            else
            {
                // Pas de chemin A* trouvé → essayer un déplacement simple
                TrySimpleMove(enemy, bestTarget);
            }

            if (enemy.ActionPoints <= 0) enemyTurnIndex++;
        }

        // ═══════════════════════════════════════════════════════════════
        // NOUVELLE MÉTHODE : Sélection intelligente de cible
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Sélectionne la meilleure cible pour un ennemi
        /// Prend en compte: portée, ligne de vue, santé, distance
        /// </summary>
        private Unit SelectBestTarget(Unit enemy)
        {
            if (playerUnits.Count == 0)
                return null;

            Unit bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var player in playerUnits)
            {
                float score = 0f;

                int distance = Math.Abs(player.Cell.X - enemy.Cell.X) +
                              Math.Abs(player.Cell.Y - enemy.Cell.Y);

                // ═══ FACTEUR 1 : Distance (plus proche = mieux) ═══
                // Score entre 0 et 100 (plus proche = score plus élevé)
                score += Math.Max(0, 100 - distance * 5);

                // ═══ FACTEUR 2 : Ligne de vue (voir = très important) ═══
                bool hasLOS = pathfinding.HasLineOfSight(enemy.Cell, player.Cell);
                if (hasLOS)
                    score += 50; // Bonus important si on peut voir la cible

                // ═══ FACTEUR 3 : À portée de tir (peut tirer maintenant = très bon) ═══
                if (distance <= enemy.WeaponData.Range && hasLOS)
                    score += 75; // Bonus énorme si on peut tirer maintenant

                // ═══ FACTEUR 4 : Cible blessée (achever les blessés) ═══
                float healthPercent = (float)player.Health / player.MaxHealth;
                if (healthPercent < 0.5f)
                    score += (1.0f - healthPercent) * 30; // Bonus jusqu'à +30 si très blessé

                // ═══ FACTEUR 5 : Variété (éviter que tous ciblent le même) ═══
                // Compter combien d'ennemis ciblent déjà ce joueur
                int alreadyTargeting = CountEnemiesTargeting(player);
                score -= alreadyTargeting * 20; // Pénalité si déjà ciblé

                // Garder la meilleure cible
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = player;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Compte combien d'ennemis ciblent déjà un joueur spécifique
        /// </summary>
        private int CountEnemiesTargeting(Unit targetPlayer)
        {
            int count = 0;

            foreach (var enemy in enemyUnits)
            {
                if (enemy.PendingTarget == targetPlayer)
                    count++;
            }

            return count;
        }

        // ═══════════════════════════════════════════════════════════════
        // NOUVELLE MÉTHODE : Déplacement simple vers cible
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Essaie un déplacement simple d'une case vers la cible
        /// Utilisé quand le A* échoue ou que le chemin est bloqué
        /// </summary>
        private void TrySimpleMove(Unit enemy, Unit target)
        {
            int dx = Math.Sign(target.Cell.X - enemy.Cell.X);
            int dy = Math.Sign(target.Cell.Y - enemy.Cell.Y);

            // Liste de mouvements possibles par ordre de préférence
            List<Point> possibleMoves = new List<Point>();

            // Diagonale (si les deux axes sont différents de 0)
            if (dx != 0 && dy != 0)
            {
                // Essayer diagonale d'abord
                Point diagonal = new Point(enemy.Cell.X + dx, enemy.Cell.Y + dy);
                possibleMoves.Add(diagonal);
            }

            // Mouvement horizontal
            if (dx != 0)
            {
                Point horizontal = new Point(enemy.Cell.X + dx, enemy.Cell.Y);
                possibleMoves.Add(horizontal);
            }

            // Mouvement vertical
            if (dy != 0)
            {
                Point vertical = new Point(enemy.Cell.X, enemy.Cell.Y + dy);
                possibleMoves.Add(vertical);
            }

            // Essayer chaque mouvement dans l'ordre
            foreach (Point move in possibleMoves)
            {
                // Vérifier si le mouvement est valide
                if (pathfinding.IsWalkable(move, enemy) &&
                    !pathfinding.HasWallBetween(enemy.Cell, move) &&
                    GetUnitAtCell(move) == null) // IMPORTANT : vérifier qu'aucune unité n'est là
                {
                    enemy.StartMoveTo(move, cellSize);
                    unitManager.OnUnitMoved(enemy, move); // ← AJOUTEZ CETTE LIGNE
                    enemy.ActionPoints--;
                    return;
                }
            }

            // Aucun mouvement possible → fin du tour
            enemy.ActionPoints = 0;
        }

        /// <summary>
        /// Ajuste la cible en fonction de la stratégie d'escouade
        /// </summary>
        private Unit ApplySquadStrategy(Unit enemy, Unit initialTarget)
        {
            // Si c'est le premier ennemi de l'escouade, garder sa cible
            if (enemyTurnIndex == 0)
                return initialTarget;

            // Stratégie : si un allié a déjà blessé une cible, l'achever
            foreach (var player in playerUnits.OrderBy(p => p.Health))
            {
                // Si cette cible est déjà blessée (moins de 70% HP)
                if ((float)player.Health / player.MaxHealth < 0.7f)
                {
                    // Et qu'elle est à portée
                    int distance = Math.Abs(player.Cell.X - enemy.Cell.X) +
                                  Math.Abs(player.Cell.Y - enemy.Cell.Y);

                    if (distance <= enemy.WeaponData.Range * 1.5f)
                    {
                        Console.WriteLine($"{enemy.Name} cible {player.Name} pour l'achever!");
                        return player;
                    }
                }
            }

            return initialTarget;
        }

        private void FireAtTarget(Unit shooter, Unit target)
        {
            if (shooter.ActionPoints <= 0 || !pathfinding.HasLineOfSight(shooter.Cell, target.Cell)) return;

            int distance = Math.Abs(target.Cell.X - shooter.Cell.X) + Math.Abs(target.Cell.Y - shooter.Cell.Y);
            if (distance > shooter.WeaponData.Range) return;

            isActionInProgress = true;
            shooter.IsFiring = true;
            shooter.FireTarget = target.Cell;
            shooter.FireProgress = 0f;

            int roll = random.Next(100);
            int baseAccuracy = shooter.WeaponData.Accuracy + shooter.Skills.GetAccuracyBonus();
            int effectiveAccuracy = Math.Max(baseAccuracy - distance * 5, 10); shooter.WillHit = roll < effectiveAccuracy;
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
                        int baseDamage = u.WeaponData.Damage + u.Skills.GetDamageBonus();
                        int damage = Math.Max(baseDamage - t.GetTotalArmor(), 1);
                        t.Health = Math.Max(t.Health - damage, 0);

                        // Donner de l'XP de survie à la cible
                        if (t.Team == Team.Player)
                        {
                            t.Skills.GainSurvivalXP(damage, t.Health > 0);
                        }

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

                    // Donner de l'XP pour le tir
                    if (u.Team == Team.Player)
                    {
                        int distance = Math.Abs(u.Cell.X - u.PendingTarget.Cell.X) +
                                      Math.Abs(u.Cell.Y - u.PendingTarget.Cell.Y);

                        if (u.WillHit)
                        {
                            int damage = Math.Max(u.WeaponData.Damage - u.PendingTarget.GetTotalArmor(), 1);
                            u.Skills.GainShootingXP(true, distance, damage);

                            // Bonus si kill
                            if (u.PendingTarget.Health <= 0)
                            {
                                u.Skills.GainKillXP(u.PendingTarget.Class);
                            }
                        }
                        else
                        {
                            u.Skills.GainShootingXP(false, distance, 0);
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

        

        private List<Unit> GetValidFireTargets(Unit shooter)
        {
            List<Unit> targets = new();
            foreach (var u in enemyUnits)
            {
                int d = Math.Abs(u.Cell.X - shooter.Cell.X) + Math.Abs(u.Cell.Y - shooter.Cell.Y);
                if (d <= shooter.WeaponData.Range && pathfinding.HasLineOfSight(shooter.Cell, u.Cell))
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
                            encyclopediaCategory = "Weapons";
                            currentState = GameState.Encyclopedia;
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

            // Initialiser le pathfinding
            pathfinding = new PathfindingSystem(
                gridWidth,
                gridHeight,
                wallSegments,
                GetUnitAtCell
            );

            Console.WriteLine($"Mission '{missionType}' launched in 3D!");

            // ═══ INITIALISER LE SPATIAL HASH ═══
            unitManager.InitializeForMission(playerUnits, enemyUnits);

            Console.WriteLine($"[OPTIMIZATION] Spatial hash initialized with {playerUnits.Count + enemyUnits.Count} units");

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
            hoveredCell = camera.GetCellFromMouse(mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);
            currentPath.Clear();
            pathCosts.Clear();

            if (selectedUnit != null && selectedUnit.ActionPoints > 0 && hoveredCell.X != -1 &&
                cachedMovableCells.Contains(hoveredCell) && selectedUnit.Team == Team.Player)
            {
                currentPath = pathfinding.FindPath(selectedUnit.Cell, hoveredCell, selectedUnit.MovementPoints, selectedUnit);

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

            bool kPressed = keyboard.IsKeyDown(Keys.K) && previousKeyboardState.IsKeyUp(Keys.K);
            if (kPressed && selectedUnit != null)
            {
                Console.WriteLine(selectedUnit.Skills.GetSkillsSummary());
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
                    // ═══ AJOUTER CETTE VÉRIFICATION ═══
                    if (pathfinding != null)
                    {
                        cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);
                        UpdateFireTargets();
                    }
                    else
                    {
                        cachedMovableCells.Clear();
                        Console.WriteLine("WARNING: Pathfinding not initialized!");
                    }
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
                // ═══ AJOUTER CETTE VÉRIFICATION ═══
                if (pathfinding == null) return;

                var movable = pathfinding.GetMovableCells(selectedUnit);
                if (movable.Contains(clickedCell))
                {
                    var path = pathfinding.FindPath(selectedUnit.Cell, clickedCell, selectedUnit.MovementPoints, selectedUnit);

                    if (path.Count > 0 && path.Count <= selectedUnit.MovementPoints)
                    {
                        Point oldCell = selectedUnit.Cell;
                        selectedUnit.StartMoveTo(clickedCell);
                        unitManager.OnUnitMoved(selectedUnit, clickedCell);
                        selectedUnit.ActionPoints--;
                        UpdateFireTargets();
                        cachedMovableCells = selectedUnit.ActionPoints > 0 ? pathfinding.GetMovableCells(selectedUnit) : new List<Point>();
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
            // Quitter le mode lancer de grenade
            throwMode = false;
            selectedGrenade = null;
            throwableCells.Clear();
            explosionPreview.Clear();
            trajectoryPreview.Clear();
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
            throwTarget = camera.GetCellFromMouse(mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height);
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
                    CancelSelection(); // Si tu cliques bouton souris droite, annule l'intention de lancer une grenade.
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

                    TriggerExplosion(explosionCell, grenade.Data, grenade.Thrower);
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

        private void TriggerExplosion(Point center, GrenadeData grenadeData, Unit thrower = null)
        {
            Console.WriteLine($"EXPLOSION at {center} - {grenadeData.Name}");

            // Suivre les stats pour l'XP
            int enemiesHit = 0;
            int totalDamage = 0;

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

                    // Compter pour l'XP si c'est un ennemi
                    if (unit.Team == Team.Enemy && thrower != null && thrower.Team == Team.Player)
                    {
                        enemiesHit++;
                        totalDamage += damage;
                    }

                    if (unit.Health <= 0)
                    {
                        (unit.Team == Team.Player ? playerUnits : enemyUnits).Remove(unit);
                        unitManager.OnUnitDied(unit);

                        Console.WriteLine($"{unit.Name} killed by explosion!");
                    }
                }

                // Détruire les murs
                if (grenadeData.DestroyWalls)
                {
                    List<WallSegment> destroyedWalls = explosionManager.GetDestroyedWalls(
                        wallSegments, center, grenadeData.Radius
                    );

                    if (destroyedWalls.Count > 0)
                    {
                        foreach (var wall in destroyedWalls)
                            wallSegments.Remove(wall);

                        // ═══ INVALIDER LE CACHE ═══
                        unitManager.OnWallsDestroyed();

                        Console.WriteLine($"Destroyed {destroyedWalls.Count} walls - cache invalidated");
                    }
                }

            }

            // Donner l'XP au lanceur
            if (thrower != null && thrower.Team == Team.Player && enemiesHit > 0)
            {
                thrower.Skills.GainGrenadeXP(enemiesHit, totalDamage);
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
                renderer3D.DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), Color.Yellow * 0.3f * pulse);
            }

            // Dessiner la zone d'explosion prévisionnelle
            foreach (var cell in explosionPreview)
            {
                Vector3 position = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    0.25f,
                    cell.Y * cellSize + cellSize / 2f
                );
                renderer3D.DrawPlane(position, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), Color.Red * 0.5f * pulse);
            }

            // Dessiner la trajectoire
            for (int i = 0; i < trajectoryPreview.Count - 1; i++)
            {
                renderer3D.DrawCube(trajectoryPreview[i], new Vector3(cellSize * 0.1f), Color.White * 0.7f);
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