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

        private bool showCoverIndicators = false;

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
            missionSelectManager = new MissionSelectManager(_spriteBatch, font);
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

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, new HashSet<WallSegment>(), GetUnitAtCell);
            statsPanel = new StatsPanel(
                Content.Load<SpriteFont>("Arial"),
                GraphicsDevice);

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
                    statsPanel.Hide(); // ferme skills si inventaire ouvert
            }

            // SKILLS
            if (keyboard.IsKeyDown(Keys.K) &&
                !previousKeyboardState.IsKeyDown(Keys.K))
            {
                bool newState = !statsPanel.IsVisible;

                if (newState)
                    showInventory = false; // ferme inventaire si skills ouvert

                if (newState)
                    statsPanel.Show();
                else
                    statsPanel.Hide();
            }

            statsPanel.Update(gameTime);

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
            if (selectedUnit != null && selectedUnit.Team == Team.Player)
                combatUI.UpdateFireTargets(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit));
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

            UpdateUnitAnimations(gameTime);
            if (combatSystem.CurrentTurn == TurnState.PlayerTurn) HandlePlayerTurn(mouse, leftClick, keyboard);
            else if (combatSystem.CurrentTurn == TurnState.EnemyTurn) combatSystem.UpdateEnemyTurn(cellSize);

            combatSystem.UpdateFiringAnimations(gameTime);
            camera.HandleControls(keyboard, mouse, previousMouseState, gameTime);
            UpdateDayNightCycle(gameTime);

            if (escapePressed) ReturnToMainMenuWithSave();
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
                combatUI.UpdateFireTargets(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit));
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

            _spriteBatch.DrawString(font, "Q/E: Rotation | Molette: Zoom | WASD/Middle: Deplacement | I: Inventaire", new Vector2(10, 10), Color.White);

            string timeStr = GetTimeOfDayString(timeOfDay);
            _spriteBatch.DrawString(font, $"Heure: {timeStr} | Carte: {gridWidth}x{gridHeight}", new Vector2(10, 30), Color.Yellow);
        }

        private void DrawWorld3D(GameTime gameTime)
        {
            camera.UpdateCamera();
            renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);
            renderer3D.SetLighting(ambientLight, directionalLight);

            GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            renderer3D.DrawGrid(gridWidth, gridHeight, cellSize, tileTexture);
            renderer3D.DrawWalls(wallSegments, cellSize, editorMode: false);

            foreach (var unit in playerUnits) renderer3D.DrawUnit(unit, cellSize);
            foreach (var unit in enemyUnits) renderer3D.DrawUnit(unit, cellSize);

            if (selectedUnit != null) renderer3D.DrawSelectionIndicator(selectedUnit, cellSize, new Color(0, 255, 255, 128));

            Unit target = combatUI.SelectedFireTarget ?? combatUI.HoveredFireTarget;
            if (target != null) renderer3D.DrawSelectionIndicator(target, cellSize, new Color(255, 0, 0, 128), 1.2f);

            renderer3D.DrawCraters(craters, cellSize);
            renderer3D.DrawGrenades(activeGrenades, cellSize);

            DrawMovableCells3D(gameTime);
            DrawPath3D(gameTime);
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
            foreach (var unit in playerUnits.Concat(enemyUnits))
            {
                if (unit.CoverType != CoverType.None)
                {
                    renderer3D.DrawUnitCoverIcon(unit, cellSize,
                        (float)gameTime.TotalGameTime.TotalSeconds);
                }
            }

            if (selectedUnit != null && combatUI.SelectedFireTarget != null)
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
                combatSystem.CurrentTurn == TurnState.PlayerTurn && selectedUnit.Team == Team.Player)
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
            if (currentPath.Count == 0 || selectedUnit == null || selectedUnit.Team != Team.Player) return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.2f + 0.8f;

            for (int i = 0; i < currentPath.Count; i++)
            {
                Point cell = currentPath[i];
                Vector3 pos = new Vector3(cell.X * cellSize + cellSize / 2f, 0.1f, cell.Y * cellSize + cellSize / 2f);
                float intensity = 1f - (i / (float)currentPath.Count) * 0.5f;
                renderer3D.DrawPlane(pos, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), new Color(100, 150, 255) * pulse * intensity);
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
            gridWidth = map.GridWidth;
            gridHeight = map.GridHeight;
            cellSize = map.CellSize;
            timeOfDay = map.TimeOfDay;
            dayNightSpeed = 1f / 86400f;

            // Charger les murs
            wallSegments = map.GetWalls();

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

            for (int i = 0; i < 6; i++)
                playerUnits.Add(new Unit(new Point(2 + i, gridHeight - 2), Team.Player, "Soldier " + (i + 1), "Assault", "Rifle", weaponDatabase["M16A1"]));

            foreach (var unit in playerUnits)
            {
                unit.AddGrenade(grenadeDatabase["Frag Grenade"]);
                if (random.Next(100) < 50) unit.AddGrenade(grenadeDatabase["Smoke Grenade"]);
            }

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

            }

            foreach (var unit in playerUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }
            foreach (var unit in enemyUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }

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

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, wallSegments, GetUnitAtCell);

            Console.WriteLine($"Mission '{missionType}' launched in 3D!");

            unitManager.InitializeForMission(playerUnits, enemyUnits);
            combatSystem.SetUnits(playerUnits, enemyUnits);
            combatSystem.StartPlayerTurn();
            // Initialiser le système de couverture
            combatSystem.InitializeCoverSystem(gridWidth, gridHeight, wallSegments);

            Console.WriteLine($"[OPTIMIZATION] Spatial hash initialized with {playerUnits.Count + enemyUnits.Count} units");
        }       

        private void HandlePlayerTurn(MouseState mouse, bool leftClick, KeyboardState keyboard)
        {
            if (IsTabPressed(keyboard)) SelectNextActiveUnit();

            hoveredCell = camera.GetCellFromMouse(mouse.Position, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            currentPath.Clear(); pathCosts.Clear();

            if (selectedUnit != null && selectedUnit.ActionPoints > 0 && hoveredCell.X != -1 &&
                cachedMovableCells.Contains(hoveredCell) && selectedUnit.Team == Team.Player)
            {
                currentPath = pathfinding.FindPath(selectedUnit.Cell, hoveredCell, selectedUnit.MovementPoints, selectedUnit);
                pathCosts.Clear();
                for (int i = 0; i < currentPath.Count; i++) pathCosts[currentPath[i]] = i + 1;
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
                var validTargets = combatSystem.GetValidFireTargets(selectedUnit);
                combatUI.UpdateFireTargets(selectedUnit, validTargets);
            }

            bool kPressed = keyboard.IsKeyDown(Keys.K) && previousKeyboardState.IsKeyUp(Keys.K);
            if (kPressed && selectedUnit != null) Console.WriteLine(selectedUnit.Skills.GetSkillsSummary());

            if (combatUI.EndTurnButton.Contains(mouse.Position) && leftClick && !combatSystem.IsActionInProgress)
                combatSystem.StartEnemyTurn();
        }

        private void HandleGridClick(Point clickedCell)
        {
            Unit clickedUnit = GetUnitAtCell(clickedCell);

            if (clickedUnit != null)
            {
                selectedUnit = clickedUnit;
                if (selectedUnit.Team == Team.Player)
                {
                    if (pathfinding != null)
                    {
                        cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);
                        var validTargets = combatSystem.GetValidFireTargets(selectedUnit);
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
                var movable = pathfinding.GetMovableCells(selectedUnit);
                if (movable.Contains(clickedCell))
                {
                    var path = pathfinding.FindPath(selectedUnit.Cell, clickedCell, selectedUnit.MovementPoints, selectedUnit);
                    if (path.Count > 0 && path.Count <= selectedUnit.MovementPoints)
                    {
                        selectedUnit.StartMoveTo(clickedCell);
                        unitManager.OnUnitMoved(selectedUnit, clickedCell);
                        selectedUnit.ActionPoints--;
                        var validTargets = combatSystem.GetValidFireTargets(selectedUnit);
                        combatUI.UpdateFireTargets(selectedUnit, validTargets);
                        cachedMovableCells = selectedUnit.ActionPoints > 0 ? pathfinding.GetMovableCells(selectedUnit) : new List<Point>();
                        currentPath.Clear();
                        pathCosts.Clear();
                    }
                }
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
                            var validTargets = combatSystem.GetValidFireTargets(selectedUnit);
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

                    case "COVER":
                        if (selectedUnit != null && selectedUnit.ActionPoints > 0)
                        {
                            bool success = combatSystem.TakeCover(selectedUnit);
                            if (success)
                            {
                                Console.WriteLine($"{selectedUnit.Name} took cover!");
                            }
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
                Unit unit = GetUnitAtCell(cell);
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