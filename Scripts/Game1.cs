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
        private const int MaxThrowRange = 5;

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

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, 1, new HashSet<WallSegment>(), new List<StairConnectionData>(), GetUnitAtCell, GetUnitAtCellOnFloor);
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
                showInventory = !showInventory;

                if (showInventory)
                {
                    statsPanel.Hide(); // ferme skills si inventaire ouvert
                    characterInfoPanel.Hide();
                }
            }

            // SKILLS
            if (keyboard.IsKeyDown(Keys.K) &&
                !previousKeyboardState.IsKeyDown(Keys.K))
            {
                bool newState = !statsPanel.IsVisible;

                if (newState)
                    characterInfoPanel.Hide(); // ferme fiche perso si skills ouvert

                if (newState)
                    showInventory = false; // ferme inventaire si skills ouvert

                if (newState)
                    statsPanel.Show();
                else
                    statsPanel.Hide();
            }

            // FICHE PERSONNAGE
            if (keyboard.IsKeyDown(Keys.C) &&
                !previousKeyboardState.IsKeyDown(Keys.C) &&
                currentState == GameState.Playing &&
                selectedUnit?.Team == Team.Player)
            {
                bool newState = !characterInfoPanel.IsVisible;

                if (newState)
                {
                    showInventory = false;
                    statsPanel.Hide();
                }

                if (newState)
                    characterInfoPanel.Show();
                else
                    characterInfoPanel.Hide();
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
                inventorySystem.Update(mouse, leftClick, keyboard, selectedUnit);
                if (escapePressed) showInventory = false;
                return;
            }

            if (characterInfoPanel.IsVisible)
            {
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
            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);

            if (keyboard.IsKeyDown(Keys.PageUp) && previousKeyboardState.IsKeyUp(Keys.PageUp))
                viewedFloor = Math.Min(viewedFloor + 1, maxFloor);

            if (keyboard.IsKeyDown(Keys.PageDown) && previousKeyboardState.IsKeyUp(Keys.PageDown))
                viewedFloor = Math.Max(viewedFloor - 1, 0);
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
                combatUI.DrawMovementInfo(selectedUnit, hoveredCell, currentPath);
            }

            _spriteBatch.DrawString(font, "Q/E: Rotation | Molette: Zoom | WASD/Middle: Deplacement | PgUp/PgDn: Etage | I: Inventaire | C: Fiche perso", new Vector2(10, 10), Color.White);
            _spriteBatch.DrawString(font, "Escaliers: balises orange/bleu sur la grille", new Vector2(10, 70), new Color(255, 190, 90));

            string timeStr = GetTimeOfDayString(timeOfDay);
            _spriteBatch.DrawString(font, $"Heure: {timeStr} | Carte: {gridWidth}x{gridHeight}", new Vector2(10, 30), Color.Yellow);
            _spriteBatch.DrawString(font, $"Etage affiche: {viewedFloor + 1}/{Math.Max(1, currentMap?.FloorCount ?? 1)}", new Vector2(10, 50), Color.LightGreen);
        }

        private void DrawWorld3D(GameTime gameTime)
        {
            camera.UpdateCamera();
            renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);
            renderer3D.SetLighting(ambientLight, directionalLight);

            GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            int floorCount = Math.Max(1, currentMap?.FloorCount ?? 1);
            int floorToRender = Math.Clamp(viewedFloor, 0, floorCount - 1);
            float yOffset = floorToRender * cellSize;

            if (floorToRender == 0)
            {
                renderer3D.DrawGrid(gridWidth, gridHeight, cellSize, tileTexture, yOffset);
            }
            else
            {
                var floorCells = GetCellsForFloor(floorToRender);
                if (floorCells.Count > 0)
                    renderer3D.DrawGridCells(floorCells, cellSize, tileTexture, yOffset);
            }

            var wallsForFloor = GetWallsForFloor(floorToRender);
            renderer3D.DrawWalls(wallsForFloor, cellSize, editorMode: false, floorHeightOffset: yOffset);
            renderer3D.DrawStairConnections(currentMap?.StairConnections, floorToRender, cellSize);

            foreach (var unit in playerUnits.Where(u => u.Floor == viewedFloor)) renderer3D.DrawUnit(unit, cellSize);
            foreach (var unit in enemyUnits.Where(u => u.Floor == viewedFloor && IsEnemyVisibleToPlayers(u))) renderer3D.DrawUnit(unit, cellSize);

            if (selectedUnit != null && selectedUnit.Floor == viewedFloor) renderer3D.DrawSelectionIndicator(selectedUnit, cellSize, new Color(0, 255, 255, 128));

            Unit target = combatUI.SelectedFireTarget ?? combatUI.HoveredFireTarget;
            if (target != null && target.Floor == viewedFloor && (target.Team != Team.Enemy || IsEnemyVisibleToPlayers(target))) renderer3D.DrawSelectionIndicator(target, cellSize, new Color(255, 0, 0, 128), 1.2f);

            renderer3D.DrawCraters(craters, cellSize);
            renderer3D.DrawGrenades(activeGrenades, cellSize);

            DrawHoveredCell3D(gameTime);
            DrawThrowMode3D(gameTime);

            // Dessiner les indicateurs de couverture (en mode debug)
            if (showCoverIndicators) // Variable bool à ajouter
            {
                renderer3D.DrawCoverIndicators(
                    combatSystem.GetCoverSystem(),
                    gridWidth,
                    gridHeight,
                    cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds
                );
            }

            // Dessiner l'icône de couverture sur les unités
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

            // Afficher le chemin
            if (currentPath.Count > 0 && selectedUnit != null && selectedUnit.Floor == viewedFloor)
            {
                renderer3D.DrawMovementPath(currentPath, selectedUnit, cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds);
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
            Vector3 position = new Vector3(hoveredCell.X * cellSize + cellSize / 2f, viewedFloor * cellSize + 0.15f, hoveredCell.Y * cellSize + cellSize / 2f);

            renderer3D.DrawPlane(position, new Vector3(cellSize, 1, cellSize), Color.Yellow * pulse);
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
            if (floor <= 0 || currentMap?.Buildings == null || currentMap.Buildings.Count == 0)
                return wallSegments;

            var filteredWalls = new HashSet<WallSegment>();

            foreach (var building in currentMap.Buildings)
            {
                if (building.FloorCount <= floor)
                    continue;

                int minX = building.X;
                int minY = building.Y;
                int maxX = building.X + building.Width;
                int maxY = building.Y + building.Height;

                foreach (var wall in wallSegments)
                {
                    bool inBounds = wall.IsHorizontal
                        ? wall.Start.X >= minX && wall.End.X <= maxX && wall.Start.Y >= minY && wall.Start.Y <= maxY
                        : wall.Start.X >= minX && wall.Start.X <= maxX && wall.Start.Y >= minY && wall.End.Y <= maxY;

                    if (inBounds)
                        filteredWalls.Add(wall);
                }
            }

            return filteredWalls.Count > 0 ? filteredWalls : wallSegments;
        }

        private HashSet<Point> GetCellsForFloor(int floor)
        {
            if (floor <= 0)
                return new HashSet<Point>();

            if (currentMap?.Buildings != null && currentMap.Buildings.Count > 0)
            {
                var cells = new HashSet<Point>();
                foreach (var building in currentMap.Buildings)
                {
                    if (building.FloorCount <= floor)
                        continue;

                    int minX = Math.Max(0, building.X);
                    int minY = Math.Max(0, building.Y);
                    int maxX = Math.Min(gridWidth, building.X + building.Width);
                    int maxY = Math.Min(gridHeight, building.Y + building.Height);

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
                playerUnits.Add(new Unit(playerSpawnCells[i], Team.Player, callSign, "Assault", "Rifle", weaponDatabase["M16A1"]));
            }

            foreach (var unit in playerUnits)
            {
                unit.AddGrenade(grenadeDatabase["Frag Grenade"]);
                if (random.Next(100) < 50) unit.AddGrenade(grenadeDatabase["Smoke Grenade"]);
            }

            AssignRandomPants(playerUnits);

            switch (missionType)
            {
                case "Tutorial":
                    for (int i = 0; i < 6; i++)
                    {
                        var t = enemyPool[random.Next(enemyPool.Count)];
                        enemyUnits.Add(new Unit(new Point(2 + i, 1), Team.Enemy, t.Name, t.Class, t.Weapon, weaponDatabase[t.Weapon]) { ActionPoints = t.ActionPoints });
                    }
                    break;

                case "Survival":
                    for (int i = 0; i < 10; i++)
                    {
                        var t = enemyPool[random.Next(enemyPool.Count)];
                        enemyUnits.Add(new Unit(new Point(2 + (i % 8), i < 8 ? 1 : 2), Team.Enemy, t.Name, t.Class, t.Weapon, weaponDatabase[t.Weapon]) { ActionPoints = t.ActionPoints });
                    }
                    break;

                case "Assault":
                    var aliens = enemyPool.Where(e => e.Name != "Zombie").ToList();
                    for (int i = 0; i < 8; i++)
                    {
                        var t = aliens[random.Next(aliens.Count)];
                        enemyUnits.Add(new Unit(new Point(2 + i, 1), Team.Enemy, t.Name, t.Class, t.Weapon, weaponDatabase[t.Weapon]) { ActionPoints = t.ActionPoints });
                    }
                    break;

                case "Defense":
                    {
                        var zombie = enemyPool.First(e => e.Name == "Zombie");
                        int zombieCount = 30;

                        Random rnd = new Random();
                        for (int i = 0; i < zombieCount; i++)
                        {
                            Point spawn;
                            bool valid;

                            do
                            {
                                spawn = new Point(rnd.Next(0, gridWidth), rnd.Next(0, gridHeight));

                                // Vérifie qu'aucune unité n'est déjà sur cette case
                                valid = !enemyUnits.Any(u => u.Cell == spawn)
                                        && !playerUnits.Any(u => u.Cell == spawn);

                            } while (!valid);

                            enemyUnits.Add(new Unit(
                                spawn,
                                Team.Enemy,
                                zombie.Name,
                                zombie.Class,
                                zombie.Weapon,
                                weaponDatabase[zombie.Weapon])
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
                                zombie.Weapon,
                                weaponDatabase[zombie.Weapon])
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
            pathfinding = new PathfindingSystem(gridWidth, gridHeight, currentMap.FloorCount, wallSegments, currentMap.StairConnections, GetUnitAtCell, GetUnitAtCellOnFloor);
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

            hoveredCell = camera.GetCellFromMouse(mouse.Position, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            // We moved the clears here to ensure a clean slate, but only populate if needed
            currentPath.Clear();
            pathCosts.Clear();

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
                    currentPath = pathfinding.FindPathDetailed(selectedUnit.Cell, selectedUnit.Floor, hoveredCell, selectedUnit.Floor, maxRange, selectedUnit).Cells;
                    lastHoveredCell = hoveredCell;

                    for (int i = 0; i < currentPath.Count; i++)
                    {
                        pathCosts[currentPath[i]] = i + 1;
                    }
                }
            }
            else
            {
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

        private void HandleGridClick(Point clickedCell)
        {
            Unit clickedUnit = GetUnitAtCellAnyFloor(clickedCell);

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

                var stair = currentMap?.StairConnections?.FirstOrDefault(st =>
                    (st.FromFloor == selectedUnit.Floor && st.FromX == clickedCell.X && st.FromY == clickedCell.Y) ||
                    (st.Bidirectional && st.ToFloor == selectedUnit.Floor && st.ToX == clickedCell.X && st.ToY == clickedCell.Y));

                if (stair != null)
                {
                    if (stair.FromFloor == selectedUnit.Floor)
                    {
                        movementGoal = new Point(stair.ToX, stair.ToY);
                        goalFloor = stair.ToFloor;
                    }
                    else
                    {
                        movementGoal = new Point(stair.FromX, stair.FromY);
                        goalFloor = stair.FromFloor;
                    }
                }

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
                bool isSprint = false;
                int actionPointsBeforeMove = selectedUnit.ActionPoints;

                if (distance <= shortRange && selectedUnit.ActionPoints >= 1)
                {
                    // Zone verte (1 AP)
                    apCost = 1;
                    Console.WriteLine($"[MOVEMENT] Short move: {distance} cells (1 AP)");
                }
                else if (distance <= maxRange && selectedUnit.ActionPoints >= 2)
                {
                    // Zone bleue (2 AP)
                    apCost = 2;
                    Console.WriteLine($"[MOVEMENT] Max move: {distance} cells (2 AP)");
                }
                else if (distance <= sprintRange && selectedUnit.CanSprint())
                {
                    // Zone jaune (2 AP + stamina)
                    apCost = 2;
                    isSprint = true;
                    Console.WriteLine($"[MOVEMENT] SPRINT: {distance} cells (2 AP + {Unit.SPRINT_STAMINA_COST} stamina)");
                }
                else
                {
                    // Hors de portée ou pas assez de ressources
                    Console.WriteLine($"[MOVEMENT] Cannot reach: {distance} cells (out of range or insufficient resources)");
                    return;
                }

                // Effectuer le déplacement
                selectedUnit.SetMovementStyle(apCost, isSprint);
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
                // Consommer stamina si sprint
                if (isSprint)
                {
                    selectedUnit.ConsumeSprint();
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
                            throwableCells = ThrowTrajectoryCalculator.GetThrowableCells(selectedUnit.Cell, MaxThrowRange, gridWidth, gridHeight);
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

            return pathfinding.HasLineOfSight(observer.Cell, target.Cell);
        }

        private int GetEffectivePerceptionRange(Unit observer)
        {
            float basePerception = observer?.PerceptionRangeCells ?? 0;
            float lightMultiplier = MathHelper.Lerp(0.55f, 1.05f, CalculateSunIntensity(timeOfDay));
            float fatigueMultiplier = observer != null && observer.Stamina < observer.MaxStamina * 0.25f ? 0.9f : 1f;
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

        private void HandleGrenadeThrow(MouseState mouse, bool leftClick)
        {
            if (selectedUnit == null || selectedGrenade == null) return;
            throwTarget = camera.GetCellFromMouse(mouse.Position, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            if (throwTarget.X >= 0)
            {
                explosionPreview = ThrowTrajectoryCalculator.GetExplosionPreview(throwTarget, selectedGrenade.Radius, gridWidth, gridHeight);
                Vector3 startPos = new Vector3(selectedUnit.Cell.X * cellSize + cellSize / 2f, cellSize * 1.5f, selectedUnit.Cell.Y * cellSize + cellSize / 2f);
                Vector3 targetPos = new Vector3(throwTarget.X * cellSize + cellSize / 2f, 0, throwTarget.Y * cellSize + cellSize / 2f);
                trajectoryPreview = ThrowTrajectoryCalculator.CalculateArcPoints(startPos, targetPos);
            }
            if (leftClick && throwTarget.X >= 0 && ThrowTrajectoryCalculator.IsInThrowRange(selectedUnit.Cell, throwTarget, MaxThrowRange))
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



    public static class Extensions { public static Vector2 ToVector2(this Point p) => new(p.X, p.Y); }
}
