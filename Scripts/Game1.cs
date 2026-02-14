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

        // --- Systèmes ---
        private CombatSystem combatSystem;
        private CombatUISystem combatUI;

        // --- Cycle jour/nuit ---
        private float timeOfDay = 0f;
        private float dayNightSpeed = 0.01f;
        private Color ambientLight = Color.White;
        private Color directionalLight = Color.White;

        // --- NOUVEAU: Système d'inventaire ---
        private bool showInventory = false;

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
        private const int BaseThrowRange = 20;
        private const int TacticalFlashlightRangeCells = 40;
        private const float Mk2WeightLbs = 1.3228f; // 600 grammes

        // --- Système de cartes ---
        private MapData currentMap;
        private MapGenerator mapGenerator;
        private MapEditor mapEditor;

        // --- États du jeu ---
        enum GameState { MainMenu, MissionSelect, Playing, OptionsMenu, GameOver, Encyclopedia, MapEditor }
        private GameState currentState = GameState.MainMenu;

        // --- Grille 3D ---
        private int cellSize = 2;
        private int gridWidth = 50;
        private int gridHeight = 50;
        private Point hoveredCell = new Point(-1, -1);

        // --- Murs sur les edges des cases ---
        private HashSet<WallSegment> wallSegments = new HashSet<WallSegment>();
        private EdgeWallGenerator edgeWallGenerator;

        // --- Unités et combat ---
        private List<Unit> playerUnits = new List<Unit>();
        private List<Unit> enemyUnits = new List<Unit>();
        private Unit selectedUnit = null;
        private List<Point> cachedMovableCells = new();
        private List<Unit> savedPlayerUnits;
        private List<Unit> savedEnemyUnits;
        private bool hasSavedGame = false;

        // --- A* Pathfinding ---
        private List<Point> currentPath = new();
        private Dictionary<Point, int> pathCosts = new();

        private Dictionary<string, WeaponData> weaponDatabase;

        // --- Entrées clavier ---
        KeyboardState previousKeyboardState;

        // --- Raycast pour sélection 3D ---
        private Texture2D pixel;

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
        private InventorySystem inventorySystem;

        // ✅ NOUVEAU CODE - Managers
        private MainMenuManager mainMenuManager;
        private MissionSelectManager missionSelectManager;
        private OptionsMenuManager optionsMenuManager;
        private EncyclopediaManager encyclopediaManager;

        // Garder ces champs (toujours utilisés ailleurs)
        private MouseState previousMouseState;
        private Random random = new Random();
        private string selectedMission = ""; // Utilisé dans CreateUnits et StartMission

        private StatsPanel statsPanel;
        private CharacterInfoPanel characterInfoPanel;

        private bool showCoverIndicators = false;

        private Point lastHoveredCell = new Point(-1, -1);
        private int viewedFloor = 0;
        private HashSet<Point> upperFloorCells = new();
        private Unit movementCinematicUnit = null;
        private HashSet<Unit> currentlySpottedEnemies = new HashSet<Unit>();

        private enum UnitPageTab { Inventory, Skills, Info }
        private const int TabWidth = 170;
        private const int TabHeight = 42;
        private const int TabSpacing = 8;
        private const int TabTopMargin = 12;
        private const float WallHeightRatio = 0.92f;
        private const int HoverRevealRadius = 2;
        private const int UnitWireframeRevealRadius = 1;
        private RasterizerState hoveredCellWireframeState;


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
            pixel = new Texture2D(GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
            hoveredCellWireframeState = new RasterizerState
            {
                CullMode = CullMode.None,
                FillMode = FillMode.WireFrame
            };

            // ✅ INITIALISATION DES MANAGERS

            // 1. Main Menu Manager
            mainMenuManager = new MainMenuManager(_graphics.GraphicsDevice, _spriteBatch, font, random);
            mainMenuManager.LoadContent(Content);
            mainMenuManager.OnNewGameRequested += () => currentState = GameState.MissionSelect;
            mainMenuManager.OnContinueRequested += HandleContinue;
            mainMenuManager.OnMapEditorRequested += () =>
            {
                mapEditor.StartNewMap(50, 50);
                currentState = GameState.MapEditor;
            };
            mainMenuManager.OnEncyclopediaRequested += () =>
            {
                currentState = GameState.Encyclopedia;
            };
            mainMenuManager.OnOptionsRequested += () => currentState = GameState.OptionsMenu;
            mainMenuManager.OnQuitRequested += () => Exit();

            // 2. Mission Select Manager
            missionSelectManager = new MissionSelectManager(GraphicsDevice, _spriteBatch, font, pixel);
            missionSelectManager.OnMissionSelected += (missionType) =>
            {
                selectedMission = missionType;
                StartMission(missionType);
            };
            missionSelectManager.OnBackToMainMenu += () => currentState = GameState.MainMenu;

            // 3. Options Menu Manager
            optionsMenuManager = new OptionsMenuManager(_graphics.GraphicsDevice, _spriteBatch, font, pixel);
            optionsMenuManager.OnBackToMainMenu += () => currentState = GameState.MainMenu;

            // 4. Encyclopedia Manager (nécessite weaponDatabase et inventorySystem)
            // On l'initialise APRÈS InitializeWeapons() et la création de inventorySystem

            // tileTexture = Content.Load<Texture2D>("TileParchment32x32");

            renderer3D = new Renderer3D(GraphicsDevice);
            camera = new CameraController(gridWidth, gridHeight, cellSize, GraphicsDevice.Viewport.AspectRatio);
            inventorySystem = new InventorySystem(GraphicsDevice, _spriteBatch, font, pixel);
            unitManager = new OptimizedUnitManager();

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, 1, new HashSet<WallSegment>(), new List<StairConnectionData>(), new List<RampTileData>(), GetUnitAtCell, GetUnitAtCellOnFloor, IsCellAvailableOnFloor);
            statsPanel = new StatsPanel(
                Content.Load<SpriteFont>("Arial"),
                GraphicsDevice);
            characterInfoPanel = new CharacterInfoPanel(font, GraphicsDevice);

            combatSystem = new CombatSystem(random, pathfinding, GetUnitAtCell, unitManager);
            combatUI = new CombatUISystem(GraphicsDevice, _spriteBatch, font, pixel);
            combatSystem.OnUnitKilled += HandleUnitKilled;
            combatSystem.OnFireCompleted += HandleFireCompleted;

            Window.ClientSizeChanged += (_, _) =>
            {
                combatUI.UpdateFireTargetsUIPositions(selectedUnit);
                camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);
            };
            mapEditor?.UpdateViewportSize(
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height
            );

            // ✅ NOUVEAU : Initialiser le système de cartes
            mapGenerator = new MapGenerator(random);
            mapEditor = new MapEditor(camera, renderer3D, font, pixel, _spriteBatch);

            // ✅ NOUVEAU : Générer les cartes prédéfinies au premier lancement
            try
            {
                var maps = MapCatalog.GetAvailableMaps();
                if (maps.Count == 0)
                {
                    Console.WriteLine("[GAME] No maps found, generating premade maps...");
                    MapGenerator.GeneratePremadeMaps();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GAME] Error checking maps: {ex.Message}");
            }

            InitializeWeapons();
            InitializeGrenades();

            // ✅ ENCYCLOPEDIA MANAGER (nécessite weaponDatabase et inventorySystem)
            encyclopediaManager = new EncyclopediaManager(
                _graphics.GraphicsDevice,
                _spriteBatch,
                font,
                weaponDatabase,
                inventorySystem,
                enemyPool
            );
            encyclopediaManager.OnBackToMainMenu += () => currentState = GameState.MainMenu;

            explosionManager = new ExplosionManager(random);
            edgeWallGenerator = new EdgeWallGenerator(random);
            humanoidBatcher = new HumanoidBatchRenderer();

            Console.WriteLine("[OPTIMIZATION] Batch renderer and spatial hash initialized");
        }

        protected override void Update(GameTime gameTime)
        {
            UpdateFPS(gameTime);
            KeyboardState currentKeyboardState = Keyboard.GetState();

            ReadInputs(out bool leftClick, out bool escapePressed, out bool iPressed,
                       out MouseState mouse, out KeyboardState keyboard);

            // INVENTAIRE
            if (iPressed && currentState == GameState.Playing && selectedUnit?.Team == Team.Player)
            {
                if (showInventory)
                    CloseUnitPages();
                else
                    OpenUnitPage(UnitPageTab.Inventory);
            }

            // SKILLS
            if (keyboard.IsKeyDown(Keys.K) &&
                !previousKeyboardState.IsKeyDown(Keys.K) &&
                currentState == GameState.Playing &&
                selectedUnit?.Team == Team.Player)
            {
                if (statsPanel.IsVisible)
                    CloseUnitPages();
                else
                    OpenUnitPage(UnitPageTab.Skills);
            }

            // FICHE PERSONNAGE
            if (keyboard.IsKeyDown(Keys.C) &&
                !previousKeyboardState.IsKeyDown(Keys.C) &&
                currentState == GameState.Playing &&
                selectedUnit?.Team == Team.Player)
            {
                if (characterInfoPanel.IsVisible)
                    CloseUnitPages();
                else
                    OpenUnitPage(UnitPageTab.Info);
            }

            if (leftClick && currentState == GameState.Playing)
            {
                if (TryHandleUnitTabClick(mouse.Position))
                    leftClick = false;
            }

            statsPanel.Update(gameTime, mouse, previousMouseState);

            renderer3D.Update(gameTime);

            UpdateGrenades(gameTime);

            switch (currentState)
            {
                case GameState.MainMenu:
                    mainMenuManager.Update(mouse, previousMouseState);
                    break;

                case GameState.MissionSelect:
                    missionSelectManager.Update(mouse, previousMouseState);
                    if (escapePressed) currentState = GameState.MainMenu;
                    break;

                case GameState.Playing:
                    UpdatePlaying(gameTime, mouse, keyboard, leftClick, escapePressed);
                    combatUI.Update(gameTime);

                    break;

                case GameState.MapEditor:
                    mapEditor.Update(
                        gameTime,
                        mouse,
                        keyboard,
                        previousKeyboardState,
                        previousMouseState,
                        GraphicsDevice.Viewport.Width,
                        GraphicsDevice.Viewport.Height
                    );

                    if (!mapEditor.IsActive)
                        currentState = GameState.MainMenu;
                    if (escapePressed)
                    {
                        mapEditor.Exit();
                        currentState = GameState.MainMenu;
                    }
                    break;

                case GameState.OptionsMenu:
                    optionsMenuManager.Update(mouse, previousMouseState);
                    if (escapePressed) currentState = GameState.MainMenu;
                    break;

                case GameState.Encyclopedia:
                    encyclopediaManager.Update(mouse, previousMouseState);
                    if (escapePressed) currentState = GameState.MainMenu;
                    break;

                case GameState.GameOver:
                    if (escapePressed || leftClick) currentState = GameState.MainMenu;
                    break;
            }

            previousMouseState = mouse;
            previousKeyboardState = keyboard;

            VisualEffects.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);

            GraphicsDevice.Clear(GetSkyColor(timeOfDay));

            if (currentState == GameState.MapEditor)
                mapEditor.Draw3D(gameTime);
            if (currentState == GameState.Playing)
                DrawWorld3D(gameTime); // monde + unités + murs

            if (currentState == GameState.Playing && characterInfoPanel.IsVisible && selectedUnit?.Team == Team.Player)
                characterInfoPanel.DrawPreview3D(selectedUnit);

            // --- EFFETS VISUELS 3D ---
            VisualEffects.Draw(); // explosions et particules

            _spriteBatch.Begin();

            switch (currentState)
            {
                case GameState.MainMenu:
                    mainMenuManager.Draw();
                    break;

                case GameState.MissionSelect:
                    missionSelectManager.Draw();
                    break;

                case GameState.Playing:
                    if (showInventory)
                        inventorySystem.Draw(selectedUnit);
                    else
                        DrawPlayingUI();
                    break;

                case GameState.MapEditor:
                    mapEditor.DrawUI(Mouse.GetState());
                    break;

                case GameState.OptionsMenu:
                    optionsMenuManager.Draw();
                    break;

                case GameState.Encyclopedia:
                    encyclopediaManager.Draw();
                    break;

                case GameState.GameOver:
                    DrawGameOver();
                    break;
            }
            statsPanel.Draw(_spriteBatch, selectedUnit);
            characterInfoPanel.Draw(_spriteBatch, selectedUnit);
            DrawUnitPageTabs();

            DrawOverlay();

            _spriteBatch.End();

            base.Draw(gameTime);
        }


        private void HandleUnitKilled(Unit unit)
        {
            if (unit.Team == Team.Player) { playerUnits.Remove(unit); if (playerUnits.Count == 0) currentState = GameState.GameOver; }
            else enemyUnits.Remove(unit);
            unitManager.OnUnitDied(unit);
        }

        private void HandleFireCompleted()
        {
            if (selectedUnit != null && selectedUnit.Team == Team.Player && selectedUnit.Floor == viewedFloor)
                combatUI.UpdateFireTargets(selectedUnit, FilterTargetsByPerception(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit)));
        }

        private void UpdateFPS(GameTime gameTime)
        {
            fpsElapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            frameCount++;
            if (fpsElapsedTime >= 1f)
            {
                currentFPS = frameCount / fpsElapsedTime;
                frameCount = 0; fpsElapsedTime = 0f;
                Console.WriteLine($"FPS: {currentFPS:F1}");
            }
        }

        private void ReturnToMainMenuWithSave()
        {
            hasSavedGame = true;
            savedPlayerUnits = playerUnits.Select(u => new Unit(u)).ToList();
            savedEnemyUnits = enemyUnits.Select(u => new Unit(u)).ToList();
            currentState = GameState.MainMenu;

            // ✅ NOUVEAU : Notifier le manager
            mainMenuManager.SetHasSavedGame(true);
        }

        private void UpdatePlaying(GameTime gameTime, MouseState mouse, KeyboardState keyboard,
            bool leftClick, bool escapePressed)
        {
            if (showInventory)
            {
                inventorySystem.Update(mouse, previousMouseState, leftClick, keyboard, selectedUnit);
                if (escapePressed) showInventory = false;
                return;
            }

            if (characterInfoPanel.IsVisible)
            {
                characterInfoPanel.Update(gameTime, keyboard, mouse, previousMouseState);
                if (escapePressed) characterInfoPanel.Hide();
                return;
            }

            UpdateUnitAnimations(gameTime);
            if (combatSystem.CurrentTurn == TurnState.PlayerTurn) HandlePlayerTurn(mouse, leftClick, keyboard);
            else if (combatSystem.CurrentTurn == TurnState.EnemyTurn) combatSystem.UpdateEnemyTurn(cellSize);

            UpdateEnemyPerceptionVisibility();

            combatSystem.UpdateFiringAnimations(gameTime);
            UpdateAimCameraAndPose();
            camera.HandleControls(keyboard, mouse, previousMouseState, gameTime, allowZoom: !statsPanel.IsVisible); UpdateDayNightCycle(gameTime);
            HandleFloorViewControls(keyboard);

            if (escapePressed) ReturnToMainMenuWithSave();
        }


        private void UpdateAimCameraAndPose()
        {
            if (movementCinematicUnit != null)
            {
                if (!movementCinematicUnit.IsMoving || combatSystem.CurrentTurn != TurnState.PlayerTurn)
                {
                    movementCinematicUnit = null;
                }
                else
                {
                    Vector3 moveDirection = movementCinematicUnit.TargetPosition - movementCinematicUnit.VisualPosition;
                    moveDirection.Y = 0f;

                    if (moveDirection.LengthSquared() < 0.001f)
                    {
                        moveDirection = new Vector3(
                            (float)Math.Sin(movementCinematicUnit.Orientation),
                            0f,
                            (float)Math.Cos(movementCinematicUnit.Orientation));
                    }

                    if (moveDirection.LengthSquared() > 0.001f)
                    {
                        moveDirection.Normalize();
                        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, moveDirection));
                        // Le système de coordonnées caméra inverse latéralement la perception écran.
                        // On inverse donc le côté d'épaule pour que droitier/gaucher corresponde visuellement.
                        float shoulderSide = movementCinematicUnit.DominantHand == Unit.Handedness.Right ? -1f : 1f;
                        Vector3 shoulderOrigin = movementCinematicUnit.VisualPosition + new Vector3(0f, cellSize * 0.75f, 0f);

                        Vector3 cameraPos = shoulderOrigin
                            - moveDirection * (cellSize * 1.15f)
                            + right * (cellSize * 0.4f * shoulderSide)
                            + Vector3.Up * (cellSize * 0.28f);

                        Vector3 lookTarget = shoulderOrigin
                            + moveDirection * (cellSize * 1.35f)
                            + Vector3.Up * (cellSize * 0.12f);

                        camera.SetShoulderCamera(cameraPos, lookTarget);
                        return;
                    }
                }
            }

            foreach (var unit in playerUnits)
            {
                unit.IsAiming = false;
            }

            Unit aimingUnit = null;
            Unit targetUnit = null;

            if (selectedUnit != null && selectedUnit.Team == Team.Player && selectedUnit.ActionPoints > 0 && !selectedUnit.IsMoving)
            {
                targetUnit = combatUI.SelectedFireTarget ?? combatUI.HoveredFireTarget;

                if (targetUnit != null && combatUI.ShowFireTargets)
                {
                    float deltaX = targetUnit.Cell.X - selectedUnit.Cell.X;
                    float deltaZ = targetUnit.Cell.Y - selectedUnit.Cell.Y;
                    selectedUnit.TargetOrientation = Unit.ComputeOrientationFromDelta(deltaX, deltaZ);
                    selectedUnit.IsAiming = true;
                    aimingUnit = selectedUnit;
                }
            }

            if (aimingUnit == null)
            {
                aimingUnit = playerUnits.FirstOrDefault(u => u.IsFiring && u.PendingTarget != null);
                targetUnit = aimingUnit?.PendingTarget;

                if (aimingUnit != null)
                {
                    aimingUnit.IsAiming = true;
                }
            }

            if (aimingUnit != null && targetUnit != null)
            {
                Vector3 shooterPos = aimingUnit.VisualPosition + new Vector3(0f, cellSize * 0.75f, 0f);
                Vector3 toTarget = targetUnit.VisualPosition - aimingUnit.VisualPosition;
                toTarget.Y = 0f;

                if (toTarget.LengthSquared() > 0.001f)
                {
                    toTarget.Normalize();
                    Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, toTarget));
                    // Même correction de latéralité pour la caméra d'épaule pendant la visée/tir.
                    float shoulderSide = aimingUnit.DominantHand == Unit.Handedness.Right ? -1f : 1f;

                    Vector3 cameraPos = shooterPos
                        - toTarget * (cellSize * 1.2f)
                        + right * (cellSize * 0.42f * shoulderSide)
                        + Vector3.Up * (cellSize * 0.3f);

                    Vector3 lookTarget = targetUnit.VisualPosition + new Vector3(0f, cellSize * 0.65f, 0f);
                    camera.SetShoulderCamera(cameraPos, lookTarget);
                    return;
                }
            }

            camera.ClearShoulderCamera();
        }

        private void HandleFloorViewControls(KeyboardState keyboard)
        {
            int minFloor = GetMinimumViewFloor();
            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);

            if (keyboard.IsKeyDown(Keys.PageUp) && previousKeyboardState.IsKeyUp(Keys.PageUp))
                viewedFloor = Math.Min(viewedFloor + 1, maxFloor);

            if (keyboard.IsKeyDown(Keys.PageDown) && previousKeyboardState.IsKeyUp(Keys.PageDown))
                viewedFloor = Math.Max(viewedFloor - 1, minFloor);
        }

        private void ReadInputs(out bool leftClick, out bool escapePressed, out bool iPressed,
            out MouseState mouse, out KeyboardState keyboard)
        {
            mouse = Mouse.GetState();
            keyboard = Keyboard.GetState();

            leftClick = mouse.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released;
            escapePressed = keyboard.IsKeyDown(Keys.Escape) && previousKeyboardState.IsKeyUp(Keys.Escape);
            iPressed = keyboard.IsKeyDown(Keys.I) && previousKeyboardState.IsKeyUp(Keys.I);
        }

        private bool IsTabPressed(KeyboardState keyboard) =>
            keyboard.IsKeyDown(Keys.Tab) && previousKeyboardState.IsKeyUp(Keys.Tab);

        private void OpenUnitPage(UnitPageTab tab)
        {
            showInventory = tab == UnitPageTab.Inventory;

            if (tab == UnitPageTab.Skills)
                statsPanel.Show();
            else
                statsPanel.Hide();

            if (tab == UnitPageTab.Info)
                characterInfoPanel.Show();
            else
                characterInfoPanel.Hide();
        }

        private void CloseUnitPages()
        {
            showInventory = false;
            statsPanel.Hide();
            characterInfoPanel.Hide();
        }

        private bool IsAnyUnitPageOpen() => showInventory || statsPanel.IsVisible || characterInfoPanel.IsVisible;

        private Rectangle GetUnitTabRect(int tabIndex)
        {
            int totalWidth = TabWidth * 3 + TabSpacing * 2;
            int startX = (GraphicsDevice.Viewport.Width - totalWidth) / 2;
            int x = startX + tabIndex * (TabWidth + TabSpacing);
            return new Rectangle(x, TabTopMargin, TabWidth, TabHeight);
        }

        private bool TryHandleUnitTabClick(Point mousePosition)
        {
            if (currentState != GameState.Playing || selectedUnit?.Team != Team.Player || !IsAnyUnitPageOpen())
                return false;

            if (GetUnitTabRect(0).Contains(mousePosition))
            {
                OpenUnitPage(UnitPageTab.Inventory);
                return true;
            }

            if (GetUnitTabRect(1).Contains(mousePosition))
            {
                OpenUnitPage(UnitPageTab.Skills);
                return true;
            }

            if (GetUnitTabRect(2).Contains(mousePosition))
            {
                OpenUnitPage(UnitPageTab.Info);
                return true;
            }

            return false;
        }

        private void DrawUnitPageTabs()
        {
            if (currentState != GameState.Playing || selectedUnit?.Team != Team.Player || !IsAnyUnitPageOpen())
                return;

            DrawUnitTab(GetUnitTabRect(0), "Inventaire (I)", showInventory);
            DrawUnitTab(GetUnitTabRect(1), "Compétences (K)", statsPanel.IsVisible);
            DrawUnitTab(GetUnitTabRect(2), "Information (C)", characterInfoPanel.IsVisible);
        }

        private void DrawUnitTab(Rectangle rect, string label, bool isActive)
        {
            Color background = isActive ? new Color(24, 72, 128, 230) : new Color(22, 22, 22, 210);
            Color border = isActive ? new Color(255, 220, 130) : new Color(110, 110, 110);
            Color text = isActive ? Color.White : new Color(210, 210, 210);

            _spriteBatch.Draw(pixel, rect, background);

            int borderThickness = 2;
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, borderThickness), border);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - borderThickness, rect.Width, borderThickness), border);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, borderThickness, rect.Height), border);
            _spriteBatch.Draw(pixel, new Rectangle(rect.Right - borderThickness, rect.Y, borderThickness, rect.Height), border);

            Vector2 textSize = font.MeasureString(label);
            Vector2 textPos = new Vector2(
                rect.X + (rect.Width - textSize.X) / 2f,
                rect.Y + (rect.Height - textSize.Y) / 2f);
            _spriteBatch.DrawString(font, label, textPos, text);
        }

        private void SelectNextActiveUnit()
        {
            if (playerUnits.Count == 0) return;

            var availableUnits = playerUnits
                .Where(u => u.ActionPoints > 0)
                .OrderBy(u => u.Cell.Y).ThenBy(u => u.Cell.X)
                .ToList();

            if (availableUnits.Count == 0) { Console.WriteLine("[TAB] Aucune unité avec des PA disponibles"); return; }

            int currentIndex = selectedUnit != null ? availableUnits.IndexOf(selectedUnit) : -1;
            selectedUnit = availableUnits[(currentIndex + 1) % availableUnits.Count];

            if (pathfinding != null)
            {
                cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);
                combatUI.UpdateFireTargets(selectedUnit, FilterTargetsByPerception(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit)));
            }

            CenterCameraOnUnit(selectedUnit);
            Console.WriteLine($"[TAB] Sélection: {selectedUnit.Name} (PA: {selectedUnit.ActionPoints})");
        }

        private void CenterCameraOnUnit(Unit unit)
        {
            if (unit == null || camera == null) return;
            camera.CenterOnPosition(unit.Cell.X * cellSize, unit.Cell.Y * cellSize);
        }

        private void UpdateUnitAnimations(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (var unit in AllUnits()) unit.UpdateAnimation(dt);
        }

        private void UpdateDayNightCycle(GameTime gameTime)
        {
            timeOfDay += (float)gameTime.ElapsedGameTime.TotalSeconds * dayNightSpeed;
            if (timeOfDay >= 1f) timeOfDay -= 1f;

            float sunIntensity = CalculateSunIntensity(timeOfDay);

            ambientLight = new Color(sunIntensity * 0.8f, sunIntensity * 0.85f, sunIntensity);
            directionalLight = new Color(sunIntensity, sunIntensity * 0.95f, sunIntensity * 0.9f);
        }

        private float CalculateSunIntensity(float time)
        {
            if (time < 0.25f) return MathHelper.Lerp(0.3f, 0.7f, time / 0.25f);
            else if (time < 0.5f) return MathHelper.Lerp(0.7f, 1.0f, (time - 0.25f) / 0.25f);
            else if (time < 0.75f) return MathHelper.Lerp(1.0f, 0.7f, (time - 0.5f) / 0.25f);
            else return MathHelper.Lerp(0.7f, 0.3f, (time - 0.75f) / 0.25f);
        }

        private void DrawGameOver()
        {
            _spriteBatch.Draw(pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), new Color(100, 0, 0, 180));

            string title = "GAME OVER";
            Vector2 size = font.MeasureString(title);
            Vector2 pos = new((GraphicsDevice.Viewport.Width - size.X * 4f) / 2, GraphicsDevice.Viewport.Height / 2 - 100);
            _spriteBatch.DrawString(font, title, pos, Color.Red, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);

            string hint = "Appuyez sur ESC ou cliquez pour retourner au menu";
            Vector2 hintSize = font.MeasureString(hint);
            Vector2 hintPos = new((GraphicsDevice.Viewport.Width - hintSize.X) / 2, GraphicsDevice.Viewport.Height / 2 + 50);
            _spriteBatch.DrawString(font, hint, hintPos, Color.White);
        }

        private void DrawOverlay()
        {
            string fpsText = $"FPS: {currentFPS:F0}";
            Vector2 fpsSize = font.MeasureString(fpsText);
            int screenWidth = GraphicsDevice.Viewport.Width;
            Vector2 fpsPos = new(screenWidth - fpsSize.X - 10, 10);
            _spriteBatch.DrawString(font, fpsText, fpsPos, Color.Yellow);

            string statsText = $"Units: {playerUnits.Count + enemyUnits.Count}";
            Vector2 statsSize = font.MeasureString(statsText);
            Vector2 statsPos = new(screenWidth - statsSize.X - 10, fpsPos.Y + fpsSize.Y + 5);
            _spriteBatch.DrawString(font, statsText, statsPos, Color.White);
        }

        private void DrawPlayingUI()
        {
            MouseState mouse = Mouse.GetState();

            combatUI.DrawEndTurnButton(mouse);
            combatUI.DrawUnitInfoPanel(selectedUnit, grenadeDatabase);
            combatUI.DrawActionButtons(selectedUnit, mouse);

            if (combatUI.ShowFireTargets && selectedUnit?.Team == Team.Player) combatUI.DrawFireTargets(mouse);

            if (selectedUnit != null && selectedUnit.Team == Team.Player && selectedUnit.Floor == viewedFloor)
            {
                DrawMovementDestinationInfoBillboard();
            }

            _spriteBatch.DrawString(font, "Q/E: Rotation | Molette: Zoom | WASD/Middle: Deplacement | PgUp/PgDn: Etage | I: Inventaire | C: Fiche perso", new Vector2(10, 10), Color.White);
            _spriteBatch.DrawString(font, "Escaliers: balises orange/bleu sur la grille", new Vector2(10, 70), new Color(255, 190, 90));

            string timeStr = GetTimeOfDayString(timeOfDay);
            _spriteBatch.DrawString(font, $"Heure: {timeStr} | Carte: {gridWidth}x{gridHeight}", new Vector2(10, 30), Color.Yellow);
            string floorLabel = viewedFloor == 0 ? "RDC" : viewedFloor > 0 ? $"+{viewedFloor}" : viewedFloor.ToString();
            int maxBasements = Math.Abs(GetMinimumViewFloor());
            _spriteBatch.DrawString(font, $"Etage affiche: {floorLabel} (Sous-sols: {maxBasements} | Etages: {Math.Max(1, currentMap?.FloorCount ?? 1)})", new Vector2(10, 50), Color.LightGreen);
        }

        private void DrawMovementDestinationInfoBillboard()
        {
            if (currentPath == null || currentPath.Count == 0 || selectedUnit == null)
                return;

            Point destinationCell = currentPath[currentPath.Count - 1];
            int distance = currentPath.Count;

            if (!TryGetMovementPreviewCosts(selectedUnit, distance, out int actionPointCost, out int phosphocreatineCost))
                return;

            Vector3 destinationCenter = new Vector3(
                destinationCell.X * cellSize + cellSize / 2f,
                viewedFloor * cellSize,
                destinationCell.Y * cellSize + cellSize / 2f
            );

            float baseHeight = destinationCenter.Y + 0.15f;
            float lineHeight = cellSize * 2f;
            Vector3 lineTop = new Vector3(destinationCenter.X, baseHeight + lineHeight, destinationCenter.Z);

            DrawWorldSpaceMovementMarker(baseHeight, lineHeight, destinationCenter, lineTop);
            DrawMovementInfoPanel(lineTop, distance, actionPointCost, phosphocreatineCost);
        }

        private void DrawWorldSpaceMovementMarker(float baseHeight, float lineHeight, Vector3 destinationCenter, Vector3 lineTop)
        {
            float connectorLength = cellSize * 0.45f;
            Vector3 cameraForward = Vector3.Normalize(camera.Target - camera.Position);
            Vector3 cameraRight = Vector3.Normalize(Vector3.Cross(cameraForward, Vector3.Up));

            Color markerColor = new Color(255, 230, 120, 215);

            Vector3 verticalLineCenter = new Vector3(destinationCenter.X, baseHeight + lineHeight * 0.5f, destinationCenter.Z);
            renderer3D.DrawCube(verticalLineCenter, new Vector3(cellSize * 0.035f, lineHeight * 0.5f, cellSize * 0.035f), markerColor);

            Vector3 connectorCenter = lineTop + cameraRight * (connectorLength * 0.5f);
            renderer3D.DrawCube(connectorCenter, new Vector3(connectorLength * 0.5f, cellSize * 0.03f, cellSize * 0.03f), markerColor);
        }

        private void DrawMovementInfoPanel(Vector3 lineTop, int distance, int actionPointCost, int phosphocreatineCost)
        {
            Vector3 projectedTop = GraphicsDevice.Viewport.Project(
                lineTop,
                camera.ProjectionMatrix,
                camera.ViewMatrix,
                Matrix.Identity);

            if (projectedTop.Z <= 0f || projectedTop.Z >= 1f)
                return;

            string infoText = $"Cases: {distance}  |  PA: -{actionPointCost}  |  PCr: -{phosphocreatineCost}%";
            Vector2 textSize = font.MeasureString(infoText);
            Vector2 panelPadding = new Vector2(14f, 8f);
            Vector2 panelSize = textSize + panelPadding * 2f;

            Vector2 panelPos = new Vector2(
                projectedTop.X + 24f,
                projectedTop.Y - panelSize.Y * 0.5f);

            Rectangle panelRect = new Rectangle(
                (int)panelPos.X,
                (int)panelPos.Y,
                (int)panelSize.X,
                (int)panelSize.Y);

            _spriteBatch.Draw(pixel, panelRect, new Color(12, 25, 18, 230));
            DrawPanelBorder(panelRect, new Color(110, 220, 170));

            Vector2 textPos = panelPos + panelPadding;
            _spriteBatch.DrawString(font, infoText, textPos, new Color(220, 255, 235));
        }

        private void DrawPanelBorder(Rectangle rect, Color color)
        {
            int thickness = 2;
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            _spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private bool TryGetMovementPreviewCosts(Unit unit, int distance, out int actionPointCost, out int phosphocreatineCost)
        {
            actionPointCost = 0;
            phosphocreatineCost = 0;

            if (unit == null || distance <= 0)
                return false;

            int shortRange = unit.GetShortMoveRange();
            int maxRange = unit.GetMaxMoveRange();
            int sprintRange = unit.GetSprintRange();

            if (distance <= shortRange && unit.ActionPoints >= 1)
            {
                actionPointCost = 1;
                phosphocreatineCost = unit.GetMovementPhosphocreatineCost(distance);
                return true;
            }

            if (distance <= maxRange && unit.ActionPoints >= 2)
            {
                actionPointCost = 2;
                phosphocreatineCost = unit.GetMovementPhosphocreatineCost(distance);
                return true;
            }

            if (distance <= sprintRange && unit.CanSprint(distance))
            {
                actionPointCost = 2;
                phosphocreatineCost = unit.GetMovementPhosphocreatineCost(distance);
                return true;
            }

            return false;
        }

        private void DrawWorld3D(GameTime gameTime)
        {
            camera.UpdateCamera();
            renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);
            renderer3D.SetLighting(ambientLight, directionalLight);

            GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            int floorCount = Math.Max(1, currentMap?.FloorCount ?? 1);
            int floorToRender = Math.Clamp(viewedFloor, GetMinimumViewFloor(), floorCount - 1);
            float yOffset = floorToRender * cellSize;

            List<Unit> unitsOnFloor = playerUnits.Where(u => u.Floor == viewedFloor)
                .Concat(enemyUnits.Where(u => u.Floor == viewedFloor && IsEnemyVisibleToPlayers(u)))
                .ToList();

            var wallsForFloor = GetWallsForFloor(floorToRender);
            HashSet<WallSegment> fadedWalls = new HashSet<WallSegment>();
            HashSet<WallSegment> hoverRevealWalls = new HashSet<WallSegment>();
            HashSet<Unit> occludedUnits = new HashSet<Unit>();
            ComputeOcclusionFromWalls(wallsForFloor, unitsOnFloor, yOffset, fadedWalls, occludedUnits);
            ComputeOcclusionFromVisibleUnitsArea(wallsForFloor, unitsOnFloor, yOffset, fadedWalls);
            ComputeOcclusionFromHoveredArea(wallsForFloor, yOffset, hoverRevealWalls);
            ComputeOcclusionFromPathArea(wallsForFloor, yOffset, hoverRevealWalls);
            fadedWalls.UnionWith(hoverRevealWalls);

            if (floorToRender == 0)
            {
                renderer3D.DrawGrid(gridWidth, gridHeight, cellSize, tileTexture, yOffset);
            }
            else if (floorToRender < 0)
            {
                var basementCells = GetCellsForFloor(floorToRender);

                if (basementCells.Count > 0)
                {
                    // Conserver le sol du RDC visible quand on explore les sous-sols,
                    // sauf au-dessus des cases qui correspondent au sous-sol affiché.
                    var groundVisibleCells = GetExteriorCells(basementCells);
                    if (groundVisibleCells.Count > 0)
                        renderer3D.DrawGridCells(groundVisibleCells, cellSize, tileTexture, 0f);
                }

                if (basementCells.Count > 0)
                    renderer3D.DrawGridCells(basementCells, cellSize, tileTexture, yOffset);
            }
            else
            {
                var floorCells = GetCellsForFloor(floorToRender);
                var exteriorCells = GetExteriorCells(floorCells);

                if (exteriorCells.Count > 0)
                    renderer3D.DrawGridCells(exteriorCells, cellSize, tileTexture, 0f);

                if (floorCells.Count > 0)
                    renderer3D.DrawGridCells(floorCells, cellSize, tileTexture, yOffset);
            }

            var opaqueWalls = new HashSet<WallSegment>(wallsForFloor.Where(w => !fadedWalls.Contains(w)));
            if (opaqueWalls.Count > 0)
                renderer3D.DrawWalls(opaqueWalls, cellSize, editorMode: false, floorHeightOffset: yOffset);

            if (hoverRevealWalls.Count > 0)
                DrawWireframeWalls(hoverRevealWalls, yOffset, new Color(255, 235, 130, 165));

            if (floorToRender < floorCount - 1)
            {
                for (int upperFloor = floorToRender + 1; upperFloor < floorCount; upperFloor++)
                {
                    float upperFloorOffset = upperFloor * cellSize;
                    var wallsForUpperFloor = FilterUpperFloorWallsForLowerView(
                        upperFloor,
                        floorToRender,
                        GetWallsForFloor(upperFloor));
                    var fadedUpperWalls = new HashSet<WallSegment>();

                    if (unitsOnFloor.Count > 0)
                    {
                        ComputeOcclusionFromVisibleUnitsArea(wallsForUpperFloor, unitsOnFloor, upperFloorOffset, fadedUpperWalls);
                        ComputeOcclusionFromWalls(wallsForUpperFloor, unitsOnFloor, upperFloorOffset, fadedUpperWalls, new HashSet<Unit>());
                    }
                    ComputeOcclusionFromHoveredArea(wallsForUpperFloor, upperFloorOffset, fadedUpperWalls);
                    ComputeOcclusionFromPathArea(wallsForUpperFloor, upperFloorOffset, fadedUpperWalls);

                    var opaqueUpperWalls = new HashSet<WallSegment>(wallsForUpperFloor.Where(w => !fadedUpperWalls.Contains(w)));
                    if (opaqueUpperWalls.Count > 0)
                    {
                        renderer3D.DrawWalls(
                            opaqueUpperWalls,
                            cellSize,
                            editorMode: false,
                            floorHeightOffset: upperFloorOffset,
                            wallOverrideColor: new Color(165, 150, 130));
                    }

                    if (fadedUpperWalls.Count > 0)
                    {
                        DrawWireframeWalls(fadedUpperWalls, upperFloorOffset, new Color(205, 190, 170, 115));
                    }
                }
            }

            if (floorToRender > GetMinimumViewFloor())
            {
                for (int lowerFloor = GetMinimumViewFloor(); lowerFloor < floorToRender; lowerFloor++)
                {
                    float lowerFloorOffset = lowerFloor * cellSize;
                    var wallsForLowerFloor = GetWallsForFloor(lowerFloor);
                    var lowerFloorUnits = GetVisibleUnitsForFloor(lowerFloor);
                    var fadedLowerWalls = new HashSet<WallSegment>();

                    if (lowerFloorUnits.Count > 0)
                    {
                        ComputeOcclusionFromVisibleUnitsArea(wallsForLowerFloor, lowerFloorUnits, lowerFloorOffset, fadedLowerWalls);
                        ComputeOcclusionFromWalls(wallsForLowerFloor, lowerFloorUnits, lowerFloorOffset, fadedLowerWalls, new HashSet<Unit>());
                    }
                    ComputeOcclusionFromHoveredArea(wallsForLowerFloor, lowerFloorOffset, fadedLowerWalls);
                    ComputeOcclusionFromPathArea(wallsForLowerFloor, lowerFloorOffset, fadedLowerWalls);

                    var opaqueLowerWalls = new HashSet<WallSegment>(wallsForLowerFloor.Where(w => !fadedLowerWalls.Contains(w)));
                    opaqueLowerWalls = FilterCameraFacingWallsForNonViewedFloor(opaqueLowerWalls);
                    if (opaqueLowerWalls.Count > 0)
                    {
                        renderer3D.DrawWalls(
                            opaqueLowerWalls,
                            cellSize,
                            editorMode: false,
                            floorHeightOffset: lowerFloorOffset,
                            wallOverrideColor: lowerFloor < 0 ? new Color(85, 105, 130) : new Color(95, 140, 170));
                    }

                    if (fadedLowerWalls.Count > 0)
                    {
                        Color lowerWireColor = lowerFloor < 0
                            ? new Color(105, 140, 180, 115)
                            : new Color(120, 180, 215, 115);
                        DrawWireframeWalls(fadedLowerWalls, lowerFloorOffset, lowerWireColor);
                    }
                }
            }

            renderer3D.DrawRampTiles(currentMap?.RampTiles, floorToRender, cellSize);
            renderer3D.DrawStairConnections(currentMap?.StairConnections, floorToRender, cellSize);

            foreach (var unit in unitsOnFloor)
                renderer3D.DrawUnit(unit, cellSize);

            DrawAlliedTacticalFlashlightBeams(floorToRender);

            var unitsBelowViewedFloor = playerUnits.Where(u => u.Floor < viewedFloor)
                .Concat(enemyUnits.Where(u => u.Floor < viewedFloor && IsEnemyVisibleToPlayers(u)))
                .Where(u => u.Health > 0)
                .ToList();

            var unitsAboveViewedFloor = playerUnits.Where(u => u.Floor > viewedFloor)
                .Concat(enemyUnits.Where(u => u.Floor > viewedFloor && IsEnemyVisibleToPlayers(u)))
                .Where(u => u.Health > 0)
                .ToList();

            if (unitsBelowViewedFloor.Count > 0)
            {
                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                foreach (var unit in unitsBelowViewedFloor)
                {
                    Color belowFloorColor = unit.Team == Team.Player
                        ? new Color(80, 200, 255, 135)
                        : new Color(255, 120, 90, 115);

                    renderer3D.DrawUnitGhost(unit, cellSize, belowFloorColor);
                }

                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            }

            if (unitsAboveViewedFloor.Count > 0)
            {
                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                foreach (var unit in unitsAboveViewedFloor)
                {
                    Color aboveFloorColor = unit.Team == Team.Player
                        ? new Color(80, 200, 255, 135)
                        : new Color(255, 120, 90, 115);

                    renderer3D.DrawUnitGhost(unit, cellSize, aboveFloorColor);
                }

                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            }

            var occlusionWireWalls = new HashSet<WallSegment>(fadedWalls);
            occlusionWireWalls.ExceptWith(hoverRevealWalls);
            if (occlusionWireWalls.Count > 0)
            {
                DrawWireframeWalls(occlusionWireWalls, yOffset, new Color(120, 190, 240, 120));
            }

            if (occludedUnits.Count > 0)
            {
                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                foreach (var unit in occludedUnits)
                {
                    renderer3D.DrawUnitSilhouette(unit, cellSize, new Color(70, 220, 255, 150));
                }

                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            }

            if (selectedUnit != null && selectedUnit.Floor == viewedFloor) renderer3D.DrawSelectionIndicator(selectedUnit, cellSize, new Color(0, 255, 255, 128));

            Unit target = combatUI.SelectedFireTarget ?? combatUI.HoveredFireTarget;
            if (target != null && target.Floor == viewedFloor && (target.Team != Team.Enemy || IsEnemyVisibleToPlayers(target))) renderer3D.DrawSelectionIndicator(target, cellSize, new Color(255, 0, 0, 128), 1.2f);

            renderer3D.DrawCraters(craters, cellSize);
            renderer3D.DrawGrenades(activeGrenades, cellSize);

            DrawHoveredCell3D(gameTime);
            DrawThrowMode3D(gameTime);

            if (showCoverIndicators)
            {
                renderer3D.DrawCoverIndicators(
                    combatSystem.GetCoverSystem(),
                    gridWidth,
                    gridHeight,
                    cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds
                );
            }

            foreach (var unit in playerUnits.Where(u => u.Floor == viewedFloor))
            {
                if (unit.CoverType != CoverType.None)
                {
                    renderer3D.DrawUnitCoverIcon(unit, cellSize,
                        (float)gameTime.TotalGameTime.TotalSeconds);
                }
            }

            foreach (var unit in enemyUnits.Where(u => u.Floor == viewedFloor && IsEnemyVisibleToPlayers(u)))
            {
                if (unit.CoverType != CoverType.None)
                {
                    renderer3D.DrawUnitCoverIcon(unit, cellSize,
                        (float)gameTime.TotalGameTime.TotalSeconds);
                }
            }

            if (selectedUnit != null && combatUI.SelectedFireTarget != null &&
                selectedUnit.Floor == viewedFloor && combatUI.SelectedFireTarget.Floor == viewedFloor)
            {
                var coverSystem = combatSystem.GetCoverSystem();
                if (coverSystem.IsUnitFlanked(combatUI.SelectedFireTarget, selectedUnit))
                {
                    renderer3D.DrawFlankingIndicator(
                        combatUI.SelectedFireTarget,
                        cellSize,
                        (float)gameTime.TotalGameTime.TotalSeconds
                    );
                }
            }

            if (selectedUnit != null && selectedUnit.Team == Team.Player && selectedUnit.Floor == viewedFloor)
            {
                var zones = pathfinding.GetMovementZones(selectedUnit);
                renderer3D.DrawMovementZones(zones, cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds);
            }

            if (currentPath.Count > 0 && selectedUnit != null && selectedUnit.Floor == viewedFloor)
            {
                renderer3D.DrawMovementPath(currentPath, selectedUnit, cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds);
            }

        }

        private void DrawAlliedTacticalFlashlightBeams(int floorToRender)
        {
            var alliedUnitsOnFloor = playerUnits
                .Where(u => u.Health > 0 && u.Floor == floorToRender)
                .ToList();

            if (alliedUnitsOnFloor.Count == 0)
                return;

            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            foreach (var ally in alliedUnitsOnFloor)
            {
                DrawTacticalFlashlightBeam(ally, floorToRender);
            }

            GraphicsDevice.BlendState = BlendState.Opaque;
        }

        private void DrawTacticalFlashlightBeam(Unit ally, int floorToRender)
        {
            if (ally == null)
                return;

            const float halfConeAngleRadians = MathHelper.Pi / 9f; // 20°
            float cosHalfConeAngle = (float)Math.Cos(halfConeAngleRadians);

            Vector2 beamOrigin = new Vector2(ally.VisualPosition.X / cellSize, ally.VisualPosition.Z / cellSize);
            Vector2 forward = new Vector2((float)Math.Sin(ally.Orientation), (float)Math.Cos(ally.Orientation));
            if (forward.LengthSquared() < 0.0001f)
                return;

            forward.Normalize();

            int minX = Math.Max(0, (int)Math.Floor(beamOrigin.X - TacticalFlashlightRangeCells));
            int maxX = Math.Min(gridWidth - 1, (int)Math.Ceiling(beamOrigin.X + TacticalFlashlightRangeCells));
            int minY = Math.Max(0, (int)Math.Floor(beamOrigin.Y - TacticalFlashlightRangeCells));
            int maxY = Math.Min(gridHeight - 1, (int)Math.Ceiling(beamOrigin.Y + TacticalFlashlightRangeCells));

            float floorYOffset = floorToRender * cellSize + 0.06f;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Point cell = new Point(x, y);
                    if (!pathfinding.HasLineOfSight(ally.Cell, cell))
                        continue;

                    Vector2 toCell = new Vector2(x + 0.5f, y + 0.5f) - beamOrigin;
                    float distance = toCell.Length();
                    if (distance < 0.05f || distance > TacticalFlashlightRangeCells)
                        continue;

                    Vector2 toCellDir = toCell / distance;
                    float angleDot = Vector2.Dot(forward, toCellDir);
                    if (angleDot < cosHalfConeAngle)
                        continue;

                    float distanceFactor = 1f - (distance / TacticalFlashlightRangeCells);
                    float coneFactor = (angleDot - cosHalfConeAngle) / (1f - cosHalfConeAngle);
                    float intensity = MathHelper.Clamp(distanceFactor * coneFactor, 0f, 1f);
                    if (intensity <= 0.02f)
                        continue;

                    Color beamColor = Color.Lerp(new Color(255, 230, 150, 30), new Color(255, 250, 220, 190), intensity);
                    renderer3D.DrawPlane(
                        new Vector3(x * cellSize + cellSize / 2f, floorYOffset, y * cellSize + cellSize / 2f),
                        new Vector3(cellSize * 0.9f, 1f, cellSize * 0.9f),
                        beamColor * (0.35f + intensity * 0.65f));
                }
            }
        }

        private Color GetSkyColor(float time)
        {
            if (time < 0.25f) return Color.Lerp(new Color(10, 10, 30), new Color(100, 120, 180), time / 0.25f);
            else if (time < 0.5f) return Color.Lerp(new Color(100, 120, 180), new Color(135, 206, 235), (time - 0.25f) / 0.25f);
            else if (time < 0.75f) return Color.Lerp(new Color(135, 206, 235), new Color(100, 120, 180), (time - 0.5f) / 0.25f);
            else return Color.Lerp(new Color(100, 120, 180), new Color(10, 10, 30), (time - 0.75f) / 0.25f);
        }

        private string GetTimeOfDayString(float time)
        {
            int hours = (int)(time * 24);
            int minutes = (int)((time * 24 - hours) * 60);
            return $"{hours:D2}:{minutes:D2}";
        }

        private void DrawMovableCells3D(GameTime gameTime)
        {
            if (selectedUnit != null && selectedUnit.ActionPoints > 0 &&
                combatSystem.CurrentTurn == TurnState.PlayerTurn && selectedUnit.Team == Team.Player &&
                selectedUnit.Floor == viewedFloor)
            {
                float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 3f) * 0.3f + 0.7f;

                foreach (var cell in cachedMovableCells)
                {
                    Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, viewedFloor * cellSize + 0.05f, cell.Y * cellSize + cellSize / 2f);
                    renderer3D.DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), Color.Green * pulse);
                }
            }
        }

        private void DrawPath3D(GameTime gameTime)
        {
            if (currentPath.Count == 0 || selectedUnit == null || selectedUnit.Team != Team.Player || selectedUnit.Floor != viewedFloor) return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.2f + 0.8f;

            for (int i = 0; i < currentPath.Count; i++)
            {
                Point cell = currentPath[i];
                Vector3 pos = new Vector3(cell.X * cellSize + cellSize / 2f, viewedFloor * cellSize + 0.1f, cell.Y * cellSize + cellSize / 2f);
                float intensity = 1f - (i / (float)currentPath.Count) * 0.5f;
                renderer3D.DrawPlane(pos, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), new Color(100, 150, 255) * pulse * intensity);
            }
        }

        private void DrawHoveredCell3D(GameTime gameTime)
        {
            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f) * 0.3f + 0.7f;
            float floorYOffset = viewedFloor * cellSize;
            List<Point> revealCells = GetHoveredAreaCells(HoverRevealRadius);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;

            foreach (Point cell in revealCells)
            {
                float distance = Vector2.Distance(new Vector2(cell.X, cell.Y), new Vector2(hoveredCell.X, hoveredCell.Y));
                float intensity = MathHelper.Clamp(1f - (distance / (HoverRevealRadius + 0.5f)), 0.2f, 1f);
                Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, floorYOffset + 0.12f, cell.Y * cellSize + cellSize / 2f);
                renderer3D.DrawPlane(position, new Vector3(cellSize * 0.95f, 1, cellSize * 0.95f), new Color(255, 230, 120, 120) * pulse * intensity);
            }

            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }


        private void DrawWireframeWalls(HashSet<WallSegment> walls, float floorHeightOffset, Color wireColor)
        {
            if (walls == null || walls.Count == 0)
                return;

            RasterizerState previousRasterizer = GraphicsDevice.RasterizerState;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = hoveredCellWireframeState;

            renderer3D.DrawWalls(walls, cellSize, editorMode: false, floorHeightOffset: floorHeightOffset, wallOverrideColor: wireColor);

            GraphicsDevice.RasterizerState = previousRasterizer;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        private void ComputeOcclusionFromHoveredArea(
            IEnumerable<WallSegment> walls,
            float floorHeightOffset,
            HashSet<WallSegment> fadedWalls)
        {
            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return;

            Vector3 cameraPos = camera.Position;
            float hoverY = viewedFloor * cellSize + cellSize * 0.35f;
            List<Point> revealCells = GetHoveredAreaCells(HoverRevealRadius);

            foreach (Point cell in revealCells)
            {
                Vector3 revealPoint = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    hoverY,
                    cell.Y * cellSize + cellSize / 2f);

                foreach (var wall in walls)
                {
                    if (wall.Type == WallType.Door)
                        continue;

                    if (IsWallBetweenCameraAndUnit(wall, floorHeightOffset, cameraPos, revealPoint))
                        fadedWalls.Add(wall);
                }
            }
        }

        private void ComputeOcclusionFromPathArea(
            IEnumerable<WallSegment> walls,
            float floorHeightOffset,
            HashSet<WallSegment> fadedWalls)
        {
            if (currentPath == null || currentPath.Count == 0 || selectedUnit == null)
                return;

            if (selectedUnit.Team != Team.Player || selectedUnit.Floor != viewedFloor)
                return;

            Vector3 cameraPos = camera.Position;
            float pathY = viewedFloor * cellSize + cellSize * 0.25f;

            foreach (Point cell in currentPath)
            {
                Vector3 revealPoint = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    pathY,
                    cell.Y * cellSize + cellSize / 2f);

                foreach (var wall in walls)
                {
                    if (wall.Type == WallType.Door)
                        continue;

                    if (IsWallBetweenCameraAndUnit(wall, floorHeightOffset, cameraPos, revealPoint))
                        fadedWalls.Add(wall);
                }
            }
        }

        private List<Point> GetHoveredAreaCells(int radius)
        {
            List<Point> cells = new List<Point>();

            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return cells;

            for (int x = hoveredCell.X - radius; x <= hoveredCell.X + radius; x++)
            {
                for (int y = hoveredCell.Y - radius; y <= hoveredCell.Y + radius; y++)
                {
                    if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                        continue;

                    float distance = Vector2.Distance(new Vector2(hoveredCell.X, hoveredCell.Y), new Vector2(x, y));
                    if (distance <= radius)
                        cells.Add(new Point(x, y));
                }
            }

            return cells;
        }

        private void ComputeOcclusionFromVisibleUnitsArea(
            IEnumerable<WallSegment> walls,
            IEnumerable<Unit> units,
            float floorHeightOffset,
            HashSet<WallSegment> fadedWalls)
        {
            if (units == null)
                return;

            Vector3 cameraPos = camera.Position;

            foreach (var unit in units)
            {
                if (unit == null || unit.Health <= 0)
                    continue;

                List<Point> revealCells = new List<Point>();
                for (int x = unit.Cell.X - UnitWireframeRevealRadius; x <= unit.Cell.X + UnitWireframeRevealRadius; x++)
                {
                    for (int y = unit.Cell.Y - UnitWireframeRevealRadius; y <= unit.Cell.Y + UnitWireframeRevealRadius; y++)
                    {
                        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                            continue;

                        revealCells.Add(new Point(x, y));
                    }
                }

                foreach (Point revealCell in revealCells)
                {
                    Vector3 revealPoint = new Vector3(
                        revealCell.X * cellSize + cellSize / 2f,
                        floorHeightOffset + cellSize * 0.35f,
                        revealCell.Y * cellSize + cellSize / 2f);

                    foreach (var wall in walls)
                    {
                        if (wall.Type == WallType.Door)
                            continue;

                        if (IsWallBetweenCameraAndUnit(wall, floorHeightOffset, cameraPos, revealPoint))
                            fadedWalls.Add(wall);
                    }
                }

                // Double-check la case exacte de l'unité avec plusieurs hauteurs pour éviter
                // qu'un mur fil de fer n'empiète partiellement sur elle.
                float[] revealHeights = { 0.2f, 0.5f, 0.8f };
                foreach (float heightRatio in revealHeights)
                {
                    Vector3 revealPoint = new Vector3(
                        unit.Cell.X * cellSize + cellSize / 2f,
                        floorHeightOffset + cellSize * heightRatio,
                        unit.Cell.Y * cellSize + cellSize / 2f);

                    foreach (var wall in walls)
                    {
                        if (wall.Type == WallType.Door)
                            continue;

                        if (IsWallBetweenCameraAndUnit(wall, floorHeightOffset, cameraPos, revealPoint))
                            fadedWalls.Add(wall);
                    }
                }
            }
        }

        private void LoadMap(MapData map = null)
        {
            // Si aucune carte fournie, générer une carte aléatoire
            if (map == null)
            {
                map = mapGenerator.GenerateRandomMap(
                    selectedMission,
                    minWidth: 20,
                    maxWidth: 100,
                    minHeight: 20,
                    maxHeight: 100
                );
            }

            // Appliquer les données de la carte
            currentMap = map;
            currentMap.RampTiles ??= new List<RampTileData>();

            if (currentMap.RampTiles.Count == 0 && currentMap.StairConnections != null)
            {
                foreach (var stair in currentMap.StairConnections)
                {
                    if (stair.ToFloor == stair.FromFloor + 1 && stair.ToX == stair.FromX && stair.ToY == stair.FromY - 1)
                    {
                        currentMap.RampTiles.Add(new RampTileData
                        {
                            X = stair.FromX,
                            Y = stair.FromY,
                            Floor = stair.FromFloor,
                            Bidirectional = stair.Bidirectional
                        });
                    }
                }
            }
            viewedFloor = 0;
            gridWidth = map.GridWidth;
            gridHeight = map.GridHeight;
            cellSize = map.CellSize;
            timeOfDay = map.TimeOfDay;
            dayNightSpeed = 1f / 86400f;

            // Charger les murs
            wallSegments = map.GetWalls();
            upperFloorCells = ComputeUpperFloorCells();

            Console.WriteLine($"[GAME] Loaded map: {map.Name} ({gridWidth}x{gridHeight})");

            // Réinitialiser la caméra
            if (camera != null)
            {
                camera = new CameraController(gridWidth, gridHeight, cellSize,
                                             GraphicsDevice.Viewport.AspectRatio);
                camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);

                if (selectedUnit != null)
                    camera.CenterOnPosition(selectedUnit.Cell.X * cellSize,
                                           selectedUnit.Cell.Y * cellSize);
            }

            // Mise à jour du pathfinding
            if (pathfinding != null)
                pathfinding.UpdateGrid(gridWidth, gridHeight, wallSegments);

            // Réinitialiser les unités
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

            // Recalcul des cellules navigables
            if (selectedUnit != null && pathfinding != null)
                cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);

            currentPath.Clear();
            pathCosts.Clear();
            hoveredCell = new Point(-1, -1);
            throwTarget = new Point(-1, -1);

            // Réinitialiser spatial hash
            if (unitManager != null)
                unitManager.InitializeForMission(playerUnits, enemyUnits);
        }

        private HashSet<WallSegment> GetWallsForFloor(int floor)
        {
            if (floor == 0 || currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return wallSegments;

            var filteredWalls = new HashSet<WallSegment>();

            foreach (var building in currentMap.Buildings)
            {
                if (floor > 0)
                {
                    if (building.FloorCount <= floor)
                        continue;
                }
                else
                {
                    if (building.BasementCount < Math.Abs(floor))
                        continue;
                }

                int minX = building.X;
                int minY = building.Y;
                int maxX = building.X + building.Width;
                int maxY = building.Y + building.Height;

                int setback = GetFloorSetback(building, floor);
                minX += setback;
                minY += setback;
                maxX -= setback;
                maxY -= setback;

                if (maxX - minX < 3 || maxY - minY < 3)
                    continue;

                AddExteriorWallsForFloor(filteredWalls, minX, minY, maxX, maxY);

                foreach (var wall in wallSegments)
                {
                    bool inBounds = wall.IsHorizontal
                        ? wall.Start.X >= minX && wall.End.X <= maxX && wall.Start.Y >= minY && wall.Start.Y <= maxY
                        : wall.Start.X >= minX && wall.Start.X <= maxX && wall.Start.Y >= minY && wall.End.Y <= maxY;

                    if (!inBounds)
                        continue;

                    if (ShouldSkipWallForFloor(building, wall, floor))
                        continue;

                    filteredWalls.Add(wall);
                }
            }

            return filteredWalls;
        }

        private static void AddExteriorWallsForFloor(HashSet<WallSegment> target, int minX, int minY, int maxX, int maxY)
        {
            if (target == null)
                return;

            if (maxX - minX < 2 || maxY - minY < 2)
                return;

            target.Add(new WallSegment(new Point(minX, minY), new Point(maxX, minY), true, WallType.Full));
            target.Add(new WallSegment(new Point(minX, maxY), new Point(maxX, maxY), true, WallType.Full));
            target.Add(new WallSegment(new Point(minX, minY), new Point(minX, maxY), false, WallType.Full));
            target.Add(new WallSegment(new Point(maxX, minY), new Point(maxX, maxY), false, WallType.Full));
        }

        private HashSet<WallSegment> FilterUpperFloorWallsForLowerView(int sourceFloor, int viewedFloor, HashSet<WallSegment> walls)
        {
            if (sourceFloor <= viewedFloor || currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return walls;

            var filteredWalls = new HashSet<WallSegment>();
            foreach (var wall in walls)
            {
                if (!IsInteriorWallOnFloor(wall, sourceFloor))
                    filteredWalls.Add(wall);
            }

            return filteredWalls;
        }

        private bool IsInteriorWallOnFloor(WallSegment wall, int floor)
        {
            if (currentMap?.Buildings == null)
                return false;

            foreach (var building in currentMap.Buildings)
            {
                if (!BuildingHasFloor(building, floor))
                    continue;

                int setback = GetFloorSetback(building, floor);
                int minX = building.X + setback;
                int minY = building.Y + setback;
                int maxX = building.X + building.Width - setback;
                int maxY = building.Y + building.Height - setback;

                bool inBounds = wall.IsHorizontal
                    ? wall.Start.X >= minX && wall.End.X <= maxX && wall.Start.Y >= minY && wall.Start.Y <= maxY
                    : wall.Start.X >= minX && wall.Start.X <= maxX && wall.Start.Y >= minY && wall.End.Y <= maxY;

                if (!inBounds)
                    continue;

                if (wall.IsHorizontal)
                    return wall.Start.Y > minY && wall.Start.Y < maxY;

                return wall.Start.X > minX && wall.Start.X < maxX;
            }

            return false;
        }

        private static bool BuildingHasFloor(BuildingFootprintData building, int floor)
        {
            if (floor >= 0)
                return building.FloorCount > floor;

            return building.BasementCount >= Math.Abs(floor);
        }

        private int GetFloorSetback(BuildingFootprintData building, int floor)
        {
            if (floor <= 1)
                return 0;

            int seed = building.X * 73856093 ^ building.Y * 19349663 ^ floor * 83492791;
            int roll = Math.Abs(seed % 100);

            // Quelques étages prennent du retrait pour créer terrasses et toits variés.
            return roll < 40 ? 1 : 0;
        }

        private bool ShouldSkipWallForFloor(BuildingFootprintData building, WallSegment wall, int floor)
        {
            if (floor <= 0)
                return false;

            int seed =
                building.X * 92821 +
                building.Y * 68917 +
                floor * 15401 +
                wall.Start.X * 733 +
                wall.Start.Y * 547 +
                wall.End.X * 389 +
                wall.End.Y * 277;

            int roll = Math.Abs(seed % 100);

            // Retirer ponctuellement des cloisons intérieures sur les étages.
            bool interiorHorizontal = wall.IsHorizontal && wall.Start.Y > building.Y && wall.Start.Y < building.Y + building.Height;
            bool interiorVertical = !wall.IsHorizontal && wall.Start.X > building.X && wall.Start.X < building.X + building.Width;

            if ((interiorHorizontal || interiorVertical) && roll < 18 + floor * 2)
                return true;

            // Ne retire plus les façades en étage: cela créait des murs manquants sur certaines générations.
            return false;
        }

        private int GetMinimumViewFloor()
        {
            if (currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return 0;

            int deepestBasement = currentMap.Buildings.Max(b => Math.Max(0, b.BasementCount));
            return -deepestBasement;
        }

        private HashSet<WallSegment> FilterCameraFacingWallsForNonViewedFloor(HashSet<WallSegment> walls)
        {
            if (walls == null || walls.Count == 0)
                return walls;

            var filteredWalls = new HashSet<WallSegment>(walls.Where(w => !IsWallFacingCamera(w, camera.Position)));

            // Garde-fou: ne jamais vider complètement les murs d'un étage.
            return filteredWalls.Count > 0 ? filteredWalls : walls;
        }

        private bool IsWallFacingCamera(WallSegment wall, Vector3 cameraPos)
        {
            float wallCenterX = (wall.Start.X + wall.End.X) * 0.5f * cellSize;
            float wallCenterZ = (wall.Start.Y + wall.End.Y) * 0.5f * cellSize;
            float sideTolerance = cellSize * 0.1f;

            if (wall.IsHorizontal)
            {
                float dz = cameraPos.Z - wallCenterZ;
                if (Math.Abs(dz) <= sideTolerance)
                    return false;

                return dz > 0f;
            }

            float dx = cameraPos.X - wallCenterX;
            if (Math.Abs(dx) <= sideTolerance)
                return false;

            return dx > 0f;
        }

        private void ComputeOcclusionFromWalls(
            IEnumerable<WallSegment> walls,
            IEnumerable<Unit> units,
            float floorHeightOffset,
            HashSet<WallSegment> fadedWalls,
            HashSet<Unit> occludedUnits)
        {
            Vector3 cameraPos = camera.Position;

            foreach (var unit in units)
            {
                Vector3 unitPosition = unit.VisualPosition + new Vector3(0f, cellSize * 0.9f, 0f);
                bool blocked = false;

                foreach (var wall in walls)
                {
                    if (wall.Type == WallType.Door)
                        continue;

                    if (!IsWallBetweenCameraAndUnit(wall, floorHeightOffset, cameraPos, unitPosition))
                        continue;

                    fadedWalls.Add(wall);
                    blocked = true;
                }

                if (blocked)
                    occludedUnits.Add(unit);
            }
        }

        private List<Unit> GetVisibleUnitsForFloor(int floor)
        {
            return playerUnits.Where(u => u.Floor == floor && u.Health > 0)
                .Concat(enemyUnits.Where(u => u.Floor == floor && u.Health > 0 && IsEnemyVisibleToPlayers(u)))
                .ToList();
        }

        private bool IsWallBetweenCameraAndUnit(WallSegment wall, float floorHeightOffset, Vector3 cameraPos, Vector3 unitPos)
        {
            Vector2 camera2D = new Vector2(cameraPos.X, cameraPos.Z);
            Vector2 unit2D = new Vector2(unitPos.X, unitPos.Z);

            Vector2 wallStart = new Vector2(wall.Start.X * cellSize, wall.Start.Y * cellSize);
            Vector2 wallEnd = new Vector2(wall.End.X * cellSize, wall.End.Y * cellSize);

            if (!TryGetSegmentIntersectionParam(camera2D, unit2D, wallStart, wallEnd, out float rayT))
                return false;

            float wallHeight = cellSize * WallHeightRatio;
            float wallBottom = floorHeightOffset;
            float wallTop = floorHeightOffset + wallHeight;

            // Évaluer la hauteur réelle du rayon caméra->cible au point d'intersection avec le mur.
            // Cela évite de partir du "pied" de caméra (projection au sol) et respecte la vraie hauteur Y de la caméra.
            float rayHeightAtWall = MathHelper.Lerp(cameraPos.Y, unitPos.Y, rayT);
            const float verticalTolerance = 0.5f;
            return rayHeightAtWall >= wallBottom - verticalTolerance &&
                   rayHeightAtWall <= wallTop + verticalTolerance;
        }

        private static bool TryGetSegmentIntersectionParam(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out float rayT)
        {
            const float epsilon = 0.0001f;
            rayT = 0f;

            Vector2 r = p2 - p1;
            Vector2 s = q2 - q1;
            float denominator = Cross(r, s);
            Vector2 delta = q1 - p1;

            if (Math.Abs(denominator) <= epsilon)
            {
                // Segments parallèles (ou colinéaires).
                if (Math.Abs(Cross(delta, r)) > epsilon)
                    return false;

                float rLenSq = r.LengthSquared();
                if (rLenSq <= epsilon)
                    return false;

                float t0 = Vector2.Dot(q1 - p1, r) / rLenSq;
                float t1 = Vector2.Dot(q2 - p1, r) / rLenSq;
                float minT = Math.Max(0f, Math.Min(t0, t1));
                float maxT = Math.Min(1f, Math.Max(t0, t1));

                if (minT > maxT)
                    return false;

                rayT = minT;
                return true;
            }

            float t = Cross(delta, s) / denominator;
            float u = Cross(delta, r) / denominator;

            if (t < -epsilon || t > 1f + epsilon || u < -epsilon || u > 1f + epsilon)
                return false;

            rayT = MathHelper.Clamp(t, 0f, 1f);
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        private HashSet<Point> GetCellsForFloor(int floor)
        {
            if (floor <= 0)
            {
                if (floor == 0)
                    return new HashSet<Point>();

                var basementCells = new HashSet<Point>();
                if (currentMap?.Buildings == null)
                    return basementCells;

                int basementLevel = Math.Abs(floor);
                foreach (var building in currentMap.Buildings)
                {
                    if (building.BasementCount < basementLevel)
                        continue;

                    int minX = Math.Max(0, building.X);
                    int minY = Math.Max(0, building.Y);
                    int maxX = Math.Min(gridWidth, building.X + building.Width);
                    int maxY = Math.Min(gridHeight, building.Y + building.Height);

                    for (int x = minX; x < maxX; x++)
                    {
                        for (int y = minY; y < maxY; y++)
                        {
                            basementCells.Add(new Point(x, y));
                        }
                    }
                }

                return basementCells;
            }

            if (currentMap?.Buildings != null && currentMap.Buildings.Count > 0)
            {
                var cells = new HashSet<Point>();
                foreach (var building in currentMap.Buildings)
                {
                    if (building.FloorCount <= floor)
                        continue;

                    int setback = GetFloorSetback(building, floor);
                    int minX = Math.Max(0, building.X + setback);
                    int minY = Math.Max(0, building.Y + setback);
                    int maxX = Math.Min(gridWidth, building.X + building.Width - setback);
                    int maxY = Math.Min(gridHeight, building.Y + building.Height - setback);

                    for (int x = minX; x < maxX; x++)
                    {
                        for (int y = minY; y < maxY; y++)
                        {
                            cells.Add(new Point(x, y));
                        }
                    }
                }

                return cells;
            }

            return upperFloorCells;
        }

        private HashSet<Point> GetExteriorCells(HashSet<Point> blockedCells)
        {
            var exteriorCells = new HashSet<Point>();

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Point cell = new Point(x, y);
                    if (!blockedCells.Contains(cell))
                        exteriorCells.Add(cell);
                }
            }

            return exteriorCells;
        }

        private HashSet<Point> ComputeUpperFloorCells()
        {
            var outsideCells = new HashSet<Point>();
            var queue = new Queue<Point>();

            for (int x = 0; x < gridWidth; x++)
            {
                EnqueueBoundaryCell(new Point(x, 0), outsideCells, queue);
                EnqueueBoundaryCell(new Point(x, gridHeight - 1), outsideCells, queue);
            }

            for (int y = 1; y < gridHeight - 1; y++)
            {
                EnqueueBoundaryCell(new Point(0, y), outsideCells, queue);
                EnqueueBoundaryCell(new Point(gridWidth - 1, y), outsideCells, queue);
            }

            while (queue.Count > 0)
            {
                Point current = queue.Dequeue();
                foreach (var neighbor in GetCardinalNeighbors(current))
                {
                    if (outsideCells.Contains(neighbor) || IsBlockedByWall(current, neighbor))
                        continue;

                    outsideCells.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            var interiorCells = new HashSet<Point>();
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    Point cell = new Point(x, y);
                    if (!outsideCells.Contains(cell))
                    {
                        interiorCells.Add(cell);
                    }
                }
            }

            return interiorCells;
        }

        private void EnqueueBoundaryCell(Point cell, HashSet<Point> visited, Queue<Point> queue)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= gridWidth || cell.Y >= gridHeight || visited.Contains(cell))
                return;

            visited.Add(cell);
            queue.Enqueue(cell);
        }

        private IEnumerable<Point> GetCardinalNeighbors(Point cell)
        {
            Point[] neighbors =
            {
                new Point(cell.X, cell.Y - 1),
                new Point(cell.X, cell.Y + 1),
                new Point(cell.X - 1, cell.Y),
                new Point(cell.X + 1, cell.Y)
            };

            foreach (var neighbor in neighbors)
            {
                if (neighbor.X >= 0 && neighbor.X < gridWidth && neighbor.Y >= 0 && neighbor.Y < gridHeight)
                    yield return neighbor;
            }
        }

        private bool IsBlockedByWall(Point a, Point b)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;

            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return true;

            foreach (var wall in wallSegments)
            {
                bool isBetweenCells =
                    (dy == 1 && wall.IsHorizontal && wall.Start.Y == b.Y && a.X >= wall.Start.X && a.X < wall.End.X) ||
                    (dy == -1 && wall.IsHorizontal && wall.Start.Y == a.Y && a.X >= wall.Start.X && a.X < wall.End.X) ||
                    (dx == 1 && !wall.IsHorizontal && wall.Start.X == b.X && a.Y >= wall.Start.Y && a.Y < wall.End.Y) ||
                    (dx == -1 && !wall.IsHorizontal && wall.Start.X == a.X && a.Y >= wall.Start.Y && a.Y < wall.End.Y);

                if (isBetweenCells && (wall.Type == WallType.Full || wall.Type == WallType.Window))
                    return true;
            }

            return false;
        }

        private class AStarNode
        {
            public Point Position;
            public int GCost, HCost;
            public int FCost => GCost + HCost;
            public AStarNode Parent;
            public AStarNode(Point pos) { Position = pos; }
        }

        private void CreateUnits(string missionType = "Tutorial")
        {
            playerUnits.Clear(); enemyUnits.Clear();

            List<Point> playerSpawnCells = missionType == "Centre-Ville"
                ? GetCityCenterSpawnCells(6)
                : Enumerable.Range(0, 6).Select(i => new Point(2 + i, gridHeight - 2)).ToList();

            string[] femaleNames = { "Nadia", "Maya", "Elena", "Sofia", "Leila", "Iris" };
            string[] maleNames = { "Alex", "Victor", "Jonas", "Marco", "Ethan", "Hugo" };

            for (int i = 0; i < playerSpawnCells.Count; i++)
            {
                bool useFemale = i % 2 == 0;
                int nameIndex = (i / 2) % femaleNames.Length;
                string callSign = useFemale ? femaleNames[nameIndex] : maleNames[nameIndex];
                playerUnits.Add(new Unit(playerSpawnCells[i], Team.Player, callSign, "Assault", string.Empty, null));
            }

            foreach (var unit in playerUnits)
            {
                unit.AddGrenade(grenadeDatabase["Frag Grenade"]);
                if (random.Next(100) < 50) unit.AddGrenade(grenadeDatabase["Smoke Grenade"]);
            }

            AssignRandomPants(playerUnits);
            EquipMk2GrenadeToAlliedPockets(playerUnits);

            switch (missionType)
            {
                case "Tutorial":
                    for (int i = 0; i < 6; i++)
                    {
                        var t = enemyPool[random.Next(enemyPool.Count)];
                        enemyUnits.Add(new Unit(new Point(2 + i, 1), Team.Enemy, t.Name, t.Class, string.Empty, null) { ActionPoints = t.ActionPoints });
                    }
                    break;

                case "Survival":
                    for (int i = 0; i < 10; i++)
                    {
                        var t = enemyPool[random.Next(enemyPool.Count)];
                        enemyUnits.Add(new Unit(new Point(2 + (i % 8), i < 8 ? 1 : 2), Team.Enemy, t.Name, t.Class, string.Empty, null) { ActionPoints = t.ActionPoints });
                    }
                    break;

                case "Assault":
                    var aliens = enemyPool.Where(e => e.Name != "Zombie").ToList();
                    for (int i = 0; i < 8; i++)
                    {
                        var t = aliens[random.Next(aliens.Count)];
                        enemyUnits.Add(new Unit(new Point(2 + i, 1), Team.Enemy, t.Name, t.Class, string.Empty, null) { ActionPoints = t.ActionPoints });
                    }
                    break;

                case "Defense":
                    {
                        var zombie = enemyPool.First(e => e.Name == "Zombie");
                        int zombieCount = 30;

                        var occupiedCells = new HashSet<Point>(playerUnits.Select(u => u.Cell));
                        var availableCells = new List<Point>(gridWidth * gridHeight - occupiedCells.Count);

                        for (int x = 0; x < gridWidth; x++)
                        {
                            for (int y = 0; y < gridHeight; y++)
                            {
                                var cell = new Point(x, y);
                                if (!occupiedCells.Contains(cell))
                                    availableCells.Add(cell);
                            }
                        }

                        int spawnCount = Math.Min(zombieCount, availableCells.Count);
                        for (int i = 0; i < spawnCount; i++)
                        {
                            int swapIndex = random.Next(i, availableCells.Count);
                            (availableCells[i], availableCells[swapIndex]) = (availableCells[swapIndex], availableCells[i]);

                            var spawn = availableCells[i];
                            enemyUnits.Add(new Unit(
                                spawn,
                                Team.Enemy,
                                zombie.Name,
                                zombie.Class,
                                string.Empty,
                                null)
                            { ActionPoints = zombie.ActionPoints });
                        }

                        break;
                    }

                case "Centre-Ville":
                    {
                        var zombie = enemyPool.First(e => e.Name == "Zombie");
                        var edgeSpawns = GetPerimeterSpawnCells(40);

                        foreach (var spawn in edgeSpawns)
                        {
                            enemyUnits.Add(new Unit(
                                spawn,
                                Team.Enemy,
                                zombie.Name,
                                zombie.Class,
                                string.Empty,
                                null)
                            { ActionPoints = zombie.ActionPoints });
                        }

                        break;
                    }
            }

            AssignRandomPants(enemyUnits);

            foreach (var unit in playerUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }
            foreach (var unit in enemyUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }

            Console.WriteLine($"Units created for {missionType}: 6 player, {enemyUnits.Count} enemy");
        }

        private void AssignRandomPants(List<Unit> units)
        {
            if (units == null || units.Count == 0)
                return;

            var availablePants = ArmorDatabase.GetArmorsBySlot(ArmorSlot.Pants);
            if (availablePants == null || availablePants.Count == 0)
                return;

            foreach (var unit in units)
            {
                var pantsData = availablePants[random.Next(availablePants.Count)];
                unit.EquippedPants = new Item(pantsData, Point.Zero);
                unit.PantsInventory = new List<Item>();

                int pantsCapacity = unit.GetPantsInventoryCapacity();
                int movedGrenades = Math.Min(unit.Grenades.Count, pantsCapacity);
                for (int i = 0; i < movedGrenades; i++)
                {
                    GrenadeData grenade = unit.Grenades[i];
                    unit.PantsInventory.Add(new Item(new ItemData(grenade.Name, grenade), Point.Zero));
                }

                unit.RefreshGrenadeInventoryFromEquipment();
            }
        }

        private void EquipMk2GrenadeToAlliedPockets(List<Unit> alliedUnits)
        {
            if (alliedUnits == null || alliedUnits.Count == 0 || !grenadeDatabase.ContainsKey("MK 2"))
                return;

            GrenadeData mk2Data = grenadeDatabase["MK 2"];

            foreach (var unit in alliedUnits)
            {
                if (unit == null)
                    continue;

                int pantsCapacity = unit.GetPantsInventoryCapacity();
                bool alreadyHasMk2 = unit.PantsInventory.Any(i => i?.Data?.Name == "MK 2")
                    || unit.ChestRigInventory.Any(i => i?.Data?.Name == "MK 2");

                if (alreadyHasMk2)
                    continue;

                var mk2Item = new Item(new ItemData("MK 2", mk2Data, Mk2WeightLbs, "Grenade MK2 (1x1) - 600g"), Point.Zero);

                if (pantsCapacity > unit.PantsInventory.Count)
                    unit.PantsInventory.Add(mk2Item);
                else if (unit.GetChestRigInventoryCapacity() > unit.ChestRigInventory.Count)
                    unit.ChestRigInventory.Add(mk2Item);

                unit.RefreshGrenadeInventoryFromEquipment();
            }
        }

        private List<Point> GetCityCenterSpawnCells(int count)
        {
            int centerX = gridWidth / 2;
            int centerY = gridHeight / 2;
            var offsets = new[]
            {
                new Point(0, 0),
                new Point(1, 0),
                new Point(-1, 0),
                new Point(0, 1),
                new Point(0, -1),
                new Point(1, 1),
                new Point(-1, -1),
                new Point(1, -1),
                new Point(-1, 1)
            };

            var cells = new List<Point>();
            foreach (var offset in offsets)
            {
                if (cells.Count >= count)
                    break;

                int x = Math.Clamp(centerX + offset.X, 0, gridWidth - 1);
                int y = Math.Clamp(centerY + offset.Y, 0, gridHeight - 1);
                var point = new Point(x, y);

                if (!cells.Contains(point))
                    cells.Add(point);
            }

            return cells;
        }

        private List<Point> GetPerimeterSpawnCells(int requestedCount)
        {
            var perimeter = new List<Point>();

            for (int x = 0; x < gridWidth; x++)
            {
                perimeter.Add(new Point(x, 0));
                if (gridHeight > 1)
                    perimeter.Add(new Point(x, gridHeight - 1));
            }

            for (int y = 1; y < gridHeight - 1; y++)
            {
                perimeter.Add(new Point(0, y));
                if (gridWidth > 1)
                    perimeter.Add(new Point(gridWidth - 1, y));
            }

            perimeter = perimeter
                .Distinct()
                .Where(p => !playerUnits.Any(u => u.Cell == p))
                .ToList();

            int count = Math.Min(requestedCount, perimeter.Count);
            return perimeter
                .OrderBy(_ => random.Next())
                .Take(count)
                .ToList();
        }

        private IEnumerable<Unit> AllUnits()
        {
            foreach (var u in playerUnits) yield return u;
            foreach (var u in enemyUnits) yield return u;
        }

        private bool IsCellAvailableOnFloor(Point cell, int floor)
        {
            if (floor == 0)
                return true;

            if (GetCellsForFloor(floor).Contains(cell))
                return true;

            if (currentMap?.RampTiles != null)
            {
                foreach (var ramp in currentMap.RampTiles)
                {
                    if (ramp.Floor == floor && ramp.X == cell.X && ramp.Y == cell.Y)
                        return true;

                    if (ramp.Floor + 1 == floor && ramp.X == cell.X && ramp.Y - 1 == cell.Y)
                        return true;
                }
            }

            return false;
        }

        private bool IsCellHoverableOnViewedFloor(Point cell, int floor)
        {
            return IsCellAvailableOnFloor(cell, floor) || IsGroundExteriorCell(cell);
        }

        private bool IsGroundExteriorCell(Point cell)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= gridWidth || cell.Y >= gridHeight)
                return false;

            if (currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return true;

            foreach (var building in currentMap.Buildings)
            {
                if (cell.X >= building.X && cell.X < building.X + building.Width &&
                    cell.Y >= building.Y && cell.Y < building.Y + building.Height)
                {
                    return false;
                }
            }

            return true;
        }

        Unit GetUnitAtCell(Point cell)
        {
            return unitManager.SpatialHash.GetUnitAt(cell, 0);
        }

        Unit GetUnitAtCellOnFloor(Point cell, int floor)
        {
            return unitManager.SpatialHash.GetUnitAt(cell, floor);
        }

        Unit GetUnitAtCellAnyFloor(Point cell)
        {
            for (int floor = 0; floor < Math.Max(1, currentMap?.FloorCount ?? 1); floor++)
            {
                var unit = unitManager.SpatialHash.GetUnitAt(cell, floor);
                if (unit != null)
                    return unit;
            }

            return null;
        }



        private List<EnemyTemplate> enemyPool = new()
        {
            new("Alien Grunt","Infantry","Franchi PA3",3),
            new("Alien Sniper","Sniper","M2010 ESR",2),
            new("Alien Heavy","Heavy","M16A1",2),
            new("Alien Scout","Scout","H&K MP5K",4),
            new("Zombie","Undead","Zombie Claws",2)
        };

        private void InitializeWeapons()
        {
            // ✅ Charger toutes les nouvelles armes
            weaponDatabase = WeaponDatabase.GetAllWeapons();

            Console.WriteLine($"[WEAPONS] Loaded {weaponDatabase.Count} weapons");
        }

        private void StartMission(string missionType)
        {
            MediaPlayer.Stop();
            currentState = GameState.Playing;

            // ✅ NOUVEAU : Charger une carte (générée aléatoirement)
            LoadMap(); // Génère automatiquement une carte selon selectedMission

            CreateUnits(missionType);
            wallSegments = currentMap.GetWalls();
            pathfinding = new PathfindingSystem(gridWidth, gridHeight, currentMap.FloorCount, wallSegments, currentMap.StairConnections, currentMap.RampTiles, GetUnitAtCell, GetUnitAtCellOnFloor, IsCellAvailableOnFloor);
            combatSystem.SetPathfinding(pathfinding);
            Console.WriteLine($"Mission '{missionType}' launched in 3D!");
            unitManager.InitializeForMission(playerUnits, enemyUnits);
            combatSystem.SetUnits(playerUnits, enemyUnits);
            combatSystem.StartPlayerTurn();
            // Initialiser le système de couverture
            combatSystem.InitializeCoverSystem(gridWidth, gridHeight, wallSegments);
            combatSystem.RefreshAllUnitsCover();
            Console.WriteLine($"[OPTIMIZATION] Spatial hash initialized with {playerUnits.Count + enemyUnits.Count} units");
        }


        private void HandlePlayerTurn(MouseState mouse, bool leftClick, KeyboardState keyboard)
        {
            if (IsTabPressed(keyboard)) SelectNextActiveUnit();

            hoveredCell = camera.GetCellFromMouse(
                mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                viewedFloor * cellSize);

            if (hoveredCell.X != -1 && !IsCellHoverableOnViewedFloor(hoveredCell, viewedFloor))
                hoveredCell = new Point(-1, -1);

            // 1. Check if we have a valid unit and valid cell
            if (selectedUnit != null && selectedUnit.ActionPoints > 0 && hoveredCell.X != -1 &&
                cachedMovableCells.Contains(hoveredCell) && selectedUnit.Team == Team.Player)
            {
                // 2. Define maxRange here
                int maxRange = selectedUnit.CanSprint() ?
                    selectedUnit.GetSprintRange() : selectedUnit.GetMaxMoveRange();

                // 3. ONLY recalculate the path if the mouse moved to a new cell
                if (hoveredCell != lastHoveredCell)
                {
                    Point previewGoal = hoveredCell;
                    int previewFloor = selectedUnit.Floor;
                    if (TryResolveVerticalTransition(selectedUnit.Floor, hoveredCell, out Point transitionGoal, out int transitionFloor))
                    {
                        previewGoal = transitionGoal;
                        previewFloor = transitionFloor;
                    }

                    currentPath = pathfinding.FindPathDetailed(selectedUnit.Cell, selectedUnit.Floor, previewGoal, previewFloor, maxRange, selectedUnit).Cells;
                    lastHoveredCell = hoveredCell;

                    pathCosts.Clear();

                    for (int i = 0; i < currentPath.Count; i++)
                    {
                        pathCosts[currentPath[i]] = i + 1;
                    }
                }
            }
            else
            {
                currentPath.Clear();
                pathCosts.Clear();

                // If the mouse isn't on a valid movement cell, update the last hovered cell anyway
                // so it recalculates correctly when it re-enters a valid cell
                lastHoveredCell = hoveredCell;
            }

            if (throwMode) HandleGrenadeThrow(mouse, leftClick);

            bool clickOnUI = combatUI.EndTurnButton.Contains(mouse.Position) ||
                combatUI.FireButton.Contains(mouse.Position) ||
                combatUI.IsMouseOverActionButton(mouse) ||
                combatUI.IsMouseOverFireTargets(mouse) || showInventory;

            if (leftClick) HandleUnitActionButtons(mouse);
            if (leftClick && combatUI.ShowFireTargets) combatUI.HandleFireTargetClick(mouse, selectedUnit);
            if (leftClick && !clickOnUI && hoveredCell.X != -1) HandleGridClick(hoveredCell);
            if (mouse.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released) CancelSelection();

            if (combatUI.FireButton.Contains(mouse.Position) && leftClick &&
                selectedUnit != null && combatUI.SelectedFireTarget != null && selectedUnit.ActionPoints > 0)
            {
                combatSystem.InitiateFire(selectedUnit, combatUI.SelectedFireTarget);
                UpdateEnemyPerceptionVisibility();
                var validTargets = FilterTargetsByPerception(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit));
                combatUI.UpdateFireTargets(selectedUnit, validTargets);
            }

            bool kPressed = keyboard.IsKeyDown(Keys.K) && previousKeyboardState.IsKeyUp(Keys.K);
            if (kPressed && selectedUnit != null) Console.WriteLine(selectedUnit.Skills.GetSkillsSummary());

            if (combatUI.EndTurnButton.Contains(mouse.Position) && leftClick && !combatSystem.IsActionInProgress)
                combatSystem.StartEnemyTurn();
        }

        private bool TryResolveVerticalTransition(int fromFloor, Point clickedCell, out Point movementGoal, out int goalFloor)
        {
            movementGoal = clickedCell;
            goalFloor = fromFloor;

            if (currentMap?.RampTiles != null)
            {
                foreach (var ramp in currentMap.RampTiles)
                {
                    if (ramp.Floor == fromFloor && ramp.X == clickedCell.X && ramp.Y == clickedCell.Y)
                    {
                        movementGoal = new Point(ramp.X, ramp.Y - 1);
                        goalFloor = fromFloor + 1;
                        return true;
                    }

                    if (ramp.Bidirectional && ramp.Floor + 1 == fromFloor && ramp.X == clickedCell.X && ramp.Y - 1 == clickedCell.Y)
                    {
                        movementGoal = new Point(ramp.X, ramp.Y);
                        goalFloor = fromFloor - 1;
                        return true;
                    }
                }
            }

            var stair = currentMap?.StairConnections?.FirstOrDefault(st =>
                (st.FromFloor == fromFloor && st.FromX == clickedCell.X && st.FromY == clickedCell.Y) ||
                (st.Bidirectional && st.ToFloor == fromFloor && st.ToX == clickedCell.X && st.ToY == clickedCell.Y));

            if (stair == null)
                return false;

            if (stair.FromFloor == fromFloor)
            {
                movementGoal = new Point(stair.ToX, stair.ToY);
                goalFloor = stair.ToFloor;
            }
            else
            {
                movementGoal = new Point(stair.FromX, stair.FromY);
                goalFloor = stair.FromFloor;
            }

            return true;
        }

        private void HandleGridClick(Point clickedCell)
        {
            if (!IsCellAvailableOnFloor(clickedCell, viewedFloor))
                return;

            Unit clickedUnit = GetUnitAtCellOnFloor(clickedCell, viewedFloor);

            if (clickedUnit != null && clickedUnit.Team == Team.Enemy && !IsEnemyVisibleToPlayers(clickedUnit))
            {
                clickedUnit = null;
            }

            if (clickedUnit != null)
            {
                selectedUnit = clickedUnit;
                if (selectedUnit.Team == Team.Player)
                {
                    if (pathfinding != null)
                    {
                        cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);
                        UpdateEnemyPerceptionVisibility();
                        var validTargets = FilterTargetsByPerception(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit));
                        combatUI.UpdateFireTargets(selectedUnit, validTargets);
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
                if (pathfinding == null) return;

                // ✅ NOUVEAU : Vérifier dans quelle zone se trouve la cellule cliquée
                var zones = pathfinding.GetMovementZones(selectedUnit);

                // Calculer le chemin
                Point movementGoal = clickedCell;
                int goalFloor = selectedUnit.Floor;

                if (TryResolveVerticalTransition(selectedUnit.Floor, clickedCell, out Point transitionGoal, out int transitionFloor))
                {
                    movementGoal = transitionGoal;
                    goalFloor = transitionFloor;
                }

                if (!IsCellAvailableOnFloor(movementGoal, goalFloor))
                    return;

                var detailedPath = pathfinding.FindPathDetailed(selectedUnit.Cell, selectedUnit.Floor, movementGoal, goalFloor,
                                               selectedUnit.GetSprintRange(), selectedUnit);
                var path = detailedPath.Cells;

                if (path.Count == 0) return;

                int distance = path.Count;
                int shortRange = selectedUnit.GetShortMoveRange();
                int maxRange = selectedUnit.GetMaxMoveRange();
                int sprintRange = selectedUnit.GetSprintRange();

                // Déterminer le coût
                int apCost = 0;
                bool consumesPhosphocreatine = false;
                int actionPointsBeforeMove = selectedUnit.ActionPoints;
                int phosphocreatineCost = selectedUnit.GetMovementPhosphocreatineCost(distance);

                if (distance <= shortRange && selectedUnit.ActionPoints >= 1 && selectedUnit.Phosphocreatine >= phosphocreatineCost)
                {
                    // Zone verte (1 AP)
                    apCost = 1;
                    consumesPhosphocreatine = true;
                    Console.WriteLine($"[MOVEMENT] Short move: {distance} cells (1 AP + {phosphocreatineCost}% phosphocreatine)");
                }
                else if (distance <= maxRange && selectedUnit.ActionPoints >= 2 && selectedUnit.Phosphocreatine >= phosphocreatineCost)
                {
                    // Zone bleue (2 AP)
                    apCost = 2;
                    consumesPhosphocreatine = true;
                    Console.WriteLine($"[MOVEMENT] Max move: {distance} cells (2 AP + {phosphocreatineCost}% phosphocreatine)");
                }
                else if (distance <= sprintRange && selectedUnit.CanSprint(distance))
                {
                    // Zone jaune (2 AP + phosphocréatine)
                    apCost = 2;
                    consumesPhosphocreatine = true;
                    Console.WriteLine($"[MOVEMENT] SPRINT: {distance} cells (2 AP + {phosphocreatineCost}% phosphocreatine)");
                }
                else
                {
                    // Hors de portée ou pas assez de ressources
                    Console.WriteLine($"[MOVEMENT] Cannot reach: {distance} cells (out of range or insufficient resources)");
                    return;
                }

                // Effectuer le déplacement
                selectedUnit.SetMovementStyle(apCost, distance > maxRange);
                selectedUnit.StartMoveAlongPath(path, cellSize);
                selectedUnit.Floor = detailedPath.EndFloor;
                unitManager.OnUnitMoved(selectedUnit, movementGoal, detailedPath.EndFloor);
                selectedUnit.ActionPoints -= apCost;

                bool isLastAlliedUnitWithActions = playerUnits.Count(u => u.ActionPoints > 0) == 0;
                bool movementSpentAllActionPoints = actionPointsBeforeMove == apCost;
                if (isLastAlliedUnitWithActions && movementSpentAllActionPoints)
                {
                    movementCinematicUnit = selectedUnit;
                }

                combatSystem.UpdateUnitCover(selectedUnit);
                if (consumesPhosphocreatine)
                {
                    selectedUnit.ConsumeSprint(distance);
                }

                // Mettre à jour l'UI
                UpdateEnemyPerceptionVisibility();
                var validTargets = FilterTargetsByPerception(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit));
                combatUI.UpdateFireTargets(selectedUnit, validTargets);
                cachedMovableCells = selectedUnit.ActionPoints > 0 ?
                    pathfinding.GetMovableCells(selectedUnit) : new List<Point>();
                currentPath.Clear();
                pathCosts.Clear();
            }
        }


        private void HandleUnitActionButtons(MouseState mouse)
        {
            if (mouse.LeftButton != ButtonState.Pressed || previousMouseState.LeftButton != ButtonState.Released) return;

            foreach (var btn in combatUI.UnitActionButtons)
            {
                var rect = new Rectangle((int)btn.Position.X, (int)btn.Position.Y, CombatUISystem.ActionButtonWidth, CombatUISystem.ActionButtonHeight);
                if (!rect.Contains(mouse.Position)) continue;

                switch (btn.Text)
                {
                    case "TIRER":
                        if (selectedUnit != null && selectedUnit.ActionPoints > 0)
                        {
                            UpdateEnemyPerceptionVisibility();
                            var validTargets = FilterTargetsByPerception(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit));
                            combatUI.UpdateFireTargets(selectedUnit, validTargets);
                            Console.WriteLine(validTargets.Count > 0 ? $"Mode tir activé - {validTargets.Count} cibles disponibles" : "Aucune cible à portée");
                        }
                        break;

                    case "GRENADE":
                        if (selectedUnit != null && selectedUnit.Grenades.Count > 0)
                        {
                            throwMode = true;
                            selectedGrenade = selectedUnit.Grenades[0];
                            int throwRange = GetUnitThrowRange(selectedUnit);
                            throwableCells = ThrowTrajectoryCalculator.GetThrowableCells(selectedUnit.Cell, throwRange, gridWidth, gridHeight);
                            Console.WriteLine($"Mode grenade activé: {selectedGrenade.Name}");
                        }
                        break;

                    case "RECHARGER":
                        Console.WriteLine("Action future : RECHARGER");
                        break;
                }
                return;
            }
        }

        /// <summary>
        /// Gère la reprise d'une partie sauvegardée
        /// </summary>
        private void HandleContinue()
        {
            if (!hasSavedGame)
            {
                Console.WriteLine("[GAME] No saved game to continue!");
                return;
            }

            playerUnits = savedPlayerUnits.Select(u => new Unit(u)).ToList();
            enemyUnits = savedEnemyUnits.Select(u => new Unit(u)).ToList();
            currentState = GameState.Playing;

            Console.WriteLine("[GAME] Game continued!");
        }

        private void CancelSelection()
        {
            combatUI.SelectedFireTarget = null;
            combatUI.ShowFireTargets = false;

            selectedUnit = null;
            cachedMovableCells.Clear();
            currentPath.Clear();
            pathCosts.Clear();

            // Grenade - reste identique
            throwMode = false;
            selectedGrenade = null;
            throwableCells.Clear();
            explosionPreview.Clear();
            trajectoryPreview.Clear();
        }

        private void UpdateEnemyPerceptionVisibility()
        {
            currentlySpottedEnemies.Clear();

            foreach (var enemy in enemyUnits)
            {
                bool spotted = playerUnits.Any(player => CanUnitPerceiveTarget(player, enemy));
                enemy.IsSpottedByPlayerTeam = spotted;

                if (spotted)
                    currentlySpottedEnemies.Add(enemy);
            }

            if (combatUI.SelectedFireTarget?.Team == Team.Enemy && !IsEnemyVisibleToPlayers(combatUI.SelectedFireTarget))
            {
                combatUI.SelectedFireTarget = null;
            }
        }

        private bool CanUnitPerceiveTarget(Unit observer, Unit target)
        {
            if (observer == null || target == null || pathfinding == null)
                return false;

            if (observer.Health <= 0 || target.Health <= 0)
                return false;

            if (observer.Floor != target.Floor)
                return false;

            float distanceCells = Vector2.Distance(new Vector2(observer.Cell.X, observer.Cell.Y), new Vector2(target.Cell.X, target.Cell.Y));
            if (distanceCells > GetEffectivePerceptionRange(observer))
                return false;

            // Vision 360°: pas de contrainte d'angle, uniquement portée + ligne de vue.
            return pathfinding.HasLineOfSight(observer.Cell, target.Cell);
        }

        private int GetEffectivePerceptionRange(Unit observer)
        {
            float basePerception = observer?.PerceptionRangeCells ?? 0;
            float lightMultiplier = MathHelper.Lerp(0.55f, 1.05f, CalculateSunIntensity(timeOfDay));
            float fatigueMultiplier = observer != null && observer.Phosphocreatine < observer.MaxPhosphocreatine * 0.25f ? 0.9f : 1f;
            return Math.Max(8, (int)Math.Round(basePerception * lightMultiplier * fatigueMultiplier));
        }

        private List<Unit> FilterTargetsByPerception(Unit shooter, List<Unit> targets)
        {
            if (targets == null)
                return new List<Unit>();

            if (shooter == null || shooter.Team != Team.Player)
                return targets;

            return targets.Where(t => t.Team != Team.Enemy || IsEnemyVisibleToPlayers(t) || CanUnitPerceiveTarget(shooter, t)).ToList();
        }

        private bool IsEnemyVisibleToPlayers(Unit enemy)
        {
            return enemy != null && enemy.IsSpottedByPlayerTeam && currentlySpottedEnemies.Contains(enemy);
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

        private int GetUnitThrowRange(Unit unit)
        {
            if (unit == null)
                return BaseThrowRange;

            return BaseThrowRange + unit.Skills.GetGrenadeThrowRangeBonus();
        }

        private void HandleGrenadeThrow(MouseState mouse, bool leftClick)
        {
            if (selectedUnit == null || selectedGrenade == null) return;
            throwTarget = camera.GetCellFromMouse(
                mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                viewedFloor * cellSize);
            if (throwTarget.X >= 0)
            {
                explosionPreview = ThrowTrajectoryCalculator.GetExplosionPreview(throwTarget, selectedGrenade.Radius, gridWidth, gridHeight);
                Vector3 startPos = new Vector3(selectedUnit.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, selectedUnit.Cell.Y * cellSize + cellSize / 2f);
                Vector3 targetPos = new Vector3(throwTarget.X * cellSize + cellSize / 2f, 0, throwTarget.Y * cellSize + cellSize / 2f);
                trajectoryPreview = ThrowTrajectoryCalculator.CalculateArcPoints(startPos, targetPos);
            }
            int throwRange = GetUnitThrowRange(selectedUnit);
            if (leftClick && throwTarget.X >= 0 && ThrowTrajectoryCalculator.IsInThrowRange(selectedUnit.Cell, throwTarget, throwRange))
            {
                LaunchGrenade(selectedUnit, selectedGrenade, throwTarget);
                selectedUnit.ActionPoints -= selectedGrenade.AOCost;
                selectedUnit.RemoveGrenade(selectedGrenade);
                CancelSelection();
            }
        }

        private void LaunchGrenade(Unit thrower, GrenadeData grenadeData, Point targetCell)
        {
            Vector3 startPos = new Vector3(thrower.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, thrower.Cell.Y * cellSize + cellSize / 2f);
            Vector3 targetPos = new Vector3(targetCell.X * cellSize + cellSize / 2f, 0, targetCell.Y * cellSize + cellSize / 2f);
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
                    Point explosionCell = new Point((int)(grenade.TargetPosition.X / cellSize), (int)(grenade.TargetPosition.Z / cellSize));
                    TriggerExplosion(explosionCell, grenade.Data, grenade.Thrower);
                    activeGrenades.RemoveAt(i);
                }
                else grenade.Position = grenade.GetCurrentPosition();
            }

            foreach (var crater in craters) crater.Age += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        private void TriggerExplosion(Point center, GrenadeData grenadeData, Unit thrower = null)
        {
            Console.WriteLine($"EXPLOSION at {center} - {grenadeData.Name}");

            Vector3 explosionPos = new Vector3(
                center.X * cellSize + cellSize / 2f, // centre de la cellule X
                0,                                   // hauteur sol
                center.Y * cellSize + cellSize / 2f  // centre de la cellule Z
            );
            VisualEffects.PlayExplosion(explosionPos, grenadeData.Radius, renderer3D);

            int enemiesHit = 0, totalDamage = 0;

            if (grenadeData.Name == "MK 2")
            {
                ApplyMk2Explosion(center, thrower, ref enemiesHit, ref totalDamage);

                if (thrower != null && thrower.Team == Team.Player && enemiesHit > 0)
                {
                    thrower.Skills.GainGrenadeXP(enemiesHit, totalDamage);
                }

                return;
            }

            List<Point> affectedCells = explosionManager.GetExplosionCells(center, grenadeData.Radius);

            foreach (var cell in affectedCells)
            {
                Unit unit = GetUnitAtCellAnyFloor(cell);
                if (unit != null)
                {
                    int damage = explosionManager.CalculateExplosionDamage(grenadeData.Damage, center, cell, grenadeData.Radius);
                    unit.Health = Math.Max(0, unit.Health - damage);
                    Console.WriteLine($"{unit.Name} took {damage} explosion damage! HP: {unit.Health}");
                    if (unit.Team == Team.Enemy && thrower != null && thrower.Team == Team.Player) { enemiesHit++; totalDamage += damage; }
                    if (unit.Health <= 0)
                    {
                        (unit.Team == Team.Player ? playerUnits : enemyUnits).Remove(unit);
                        unitManager.OnUnitDied(unit);
                        Console.WriteLine($"{unit.Name} killed by explosion!");
                    }
                }

                if (grenadeData.DestroyWalls)
                {
                    List<WallSegment> destroyedWalls = explosionManager.GetDestroyedWalls(wallSegments, center, grenadeData.Radius);
                    if (destroyedWalls.Count > 0)
                    {
                        foreach (var wall in destroyedWalls) wallSegments.Remove(wall);
                        unitManager.OnWallsDestroyed();
                        Console.WriteLine($"Destroyed {destroyedWalls.Count} walls - cache invalidated");
                    }
                }
            }

            if (thrower != null && thrower.Team == Team.Player && enemiesHit > 0) thrower.Skills.GainGrenadeXP(enemiesHit, totalDamage);

            if (grenadeData.DestroyWalls)
            {
                List<WallSegment> destroyedWalls = explosionManager.GetDestroyedWalls(wallSegments, center, grenadeData.Radius);
                foreach (var wall in destroyedWalls) wallSegments.Remove(wall);
                Console.WriteLine($"Destroyed {destroyedWalls.Count} wall segments");
            }

            if (grenadeData.DigsTerrain)
            {
                List<Crater> newCraters = explosionManager.CreateCraters(center, grenadeData.DigDepth, grenadeData.Radius);
                craters.AddRange(newCraters);
                Console.WriteLine($"Created {newCraters.Count} craters");
            }
        }

        private void ApplyMk2Explosion(Point center, Unit thrower, ref int enemiesHit, ref int totalDamage)
        {
            const float lethalRadius = 2f;
            const float fragmentationStart = 3f;
            const float fragmentationEnd = 9f;

            List<Unit> unitsToEvaluate = new List<Unit>(playerUnits.Count + enemyUnits.Count);
            unitsToEvaluate.AddRange(playerUnits);
            unitsToEvaluate.AddRange(enemyUnits);

            foreach (var unit in unitsToEvaluate)
            {
                float distance = Vector2.Distance(new Vector2(center.X, center.Y), new Vector2(unit.Cell.X, unit.Cell.Y));

                if (distance <= lethalRadius)
                {
                    KillUnitFromMk2(unit, "blast radius", thrower, ref enemiesHit, ref totalDamage);
                    continue;
                }

                if (distance < fragmentationStart || distance > fragmentationEnd)
                    continue;

                if (HasMk2FragmentationProtection(unit))
                    continue;

                float hitChance = 0.8f * (fragmentationEnd - distance) / (fragmentationEnd - fragmentationStart);
                hitChance = MathHelper.Clamp(hitChance, 0f, 0.8f);

                if (random.NextDouble() <= hitChance)
                {
                    KillUnitFromMk2(unit, $"fragmentation ({hitChance * 100f:0}% chance)", thrower, ref enemiesHit, ref totalDamage);
                }
            }
        }

        private bool HasMk2FragmentationProtection(Unit unit)
        {
            bool hasFragmentationHelmet = unit.EquippedHelmet?.Data?.ProtectionLevel >= ProtectionLevel.Fragmentation;
            bool hasFragmentationSuit = unit.EquippedArmor?.Data?.ProtectionLevel >= ProtectionLevel.Fragmentation;
            return hasFragmentationHelmet && hasFragmentationSuit;
        }

        private void KillUnitFromMk2(Unit unit, string reason, Unit thrower, ref int enemiesHit, ref int totalDamage)
        {
            int hpBefore = unit.Health;
            unit.Health = 0;
            Console.WriteLine($"{unit.Name} killed by MK 2 {reason}.");

            if (unit.Team == Team.Enemy && thrower != null && thrower.Team == Team.Player)
            {
                enemiesHit++;
                totalDamage += hpBefore;
            }

            (unit.Team == Team.Player ? playerUnits : enemyUnits).Remove(unit);
            unitManager.OnUnitDied(unit);
        }

        private void DrawThrowMode3D(GameTime gameTime)
        {
            if (!throwMode) return;
            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.3f + 0.7f;
            foreach (var cell in throwableCells)
            {
                Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, 0.2f, cell.Y * cellSize + cellSize / 2f);
                renderer3D.DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), Color.Yellow * 0.3f * pulse);
            }
            foreach (var cell in explosionPreview)
            {
                Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, 0.25f, cell.Y * cellSize + cellSize / 2f);
                renderer3D.DrawPlane(position, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), Color.Red * 0.5f * pulse);
            }
            for (int i = 0; i < trajectoryPreview.Count - 1; i++)
            {
                Vector3 a = trajectoryPreview[i];
                Vector3 b = trajectoryPreview[i + 1];
                float dist = Vector3.Distance(a, b);
                int steps = Math.Max(1, (int)(dist / (cellSize * 0.05f)));
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 p = Vector3.Lerp(a, b, t);
                    renderer3D.DrawCube(p, new Vector3(cellSize * 0.08f), Color.White * 0.85f);
                }
            }
        }
    }

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
            Color c = !IsEnabled
                ? UIThemeManager.DisabledColor
                : bounds.Contains(mouse.Position)
                    ? UIThemeManager.HoverColor
                    : UIThemeManager.PrimaryColor;
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



    public static class Extensions { public static Vector2 ToVector2(this Point p) => new(p.X, p.Y); }
}
