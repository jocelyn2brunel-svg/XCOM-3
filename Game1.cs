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
        private const int MaxThrowRange = 5;

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
                
        // --- A* Pathfinding ---
        private List<Point> currentPath = new();
        private Dictionary<Point, int> pathCosts = new();

        private Dictionary<string, WeaponData> weaponDatabase;
                
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
        private InventorySystem inventorySystem;

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
            tileTexture = Content.Load<Texture2D>("TileParchment32x32");

            renderer3D = new Renderer3D(GraphicsDevice);
            camera = new CameraController(gridWidth, gridHeight, cellSize, GraphicsDevice.Viewport.AspectRatio);
            inventorySystem = new InventorySystem(GraphicsDevice, _spriteBatch, font, pixel);
            unitManager = new OptimizedUnitManager();

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, new HashSet<WallSegment>(), GetUnitAtCell);

            combatSystem = new CombatSystem(random, pathfinding, GetUnitAtCell, unitManager);
            combatUI = new CombatUISystem(GraphicsDevice, _spriteBatch, font, pixel);
            combatSystem.OnUnitKilled += HandleUnitKilled;
            combatSystem.OnFireCompleted += HandleFireCompleted;

            menuButtons = CreateMenu(new[] { "New Game", "Continue", "Encyclopedia", "Options", "Quit" }, 100);
            missionButtons = CreateMenu(new[] { "Tutorial", "Survival", "Assault", "Defense", "Back" }, 100);
            encyclopediaButtons = CreateMenu(new[] { "Armes", "Armures", "Unités", "Retour" }, 100);

            optionsButtons = new List<Button>
            {
                new("Music Volume +", new Vector2(0,100)),
                new("Music Volume -", new Vector2(0,156)),
                new("Back", new Vector2(0,184))
            };
            volumeBar = new Rectangle(0, 134, 200, 8);

            menuSongs = new[] { "menu_music_1", "menu_music_2", "menu_music_3", "menu_music_4" }
                .Select(Content.Load<Song>).ToList();
            currentSong = menuSongs[random.Next(menuSongs.Count)];
            MediaPlayer.Play(currentSong); MediaPlayer.Volume = 0.5f;

            Window.ClientSizeChanged += (_, _) =>
            {
                combatUI.UpdateFireTargetsUIPositions(selectedUnit);
                camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);
            };

            InitializeWeapons();
            InitializeGrenades();
            explosionManager = new ExplosionManager(random);
            edgeWallGenerator = new EdgeWallGenerator(random);
            humanoidBatcher = new HumanoidBatchRenderer();

            Console.WriteLine("[OPTIMIZATION] Batch renderer and spatial hash initialized");
        }

        protected override void Update(GameTime gameTime)
        {
            UpdateFPS(gameTime);

            ReadInputs(out bool leftClick, out bool escapePressed, out bool iPressed,
                       out MouseState mouse, out KeyboardState keyboard);

            if (iPressed && currentState == GameState.Playing && selectedUnit?.Team == Team.Player)
                showInventory = !showInventory;

            UpdateGrenades(gameTime);
            menuButtons[1].IsEnabled = hasSavedGame;

            switch (currentState)
            {
                case GameState.MainMenu: HandleMainMenu(mouse); break;
                case GameState.MissionSelect: HandleMissionSelect(mouse); if (escapePressed) currentState = GameState.MainMenu; break;
                case GameState.Playing: UpdatePlaying(gameTime, mouse, keyboard, leftClick, escapePressed); break;
                case GameState.OptionsMenu: HandleOptionsMenu(mouse); if (escapePressed) currentState = GameState.MainMenu; break;
                case GameState.Encyclopedia: HandleEncyclopedia(mouse); if (escapePressed) currentState = GameState.MainMenu; break;
                case GameState.GameOver: if (escapePressed || leftClick) currentState = GameState.MainMenu; break;
            }

            previousMouseState = mouse;
            previousKeyboardState = keyboard;

            base.Update(gameTime);
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

        private List<Button> CreateMenu(string[] labels, int startY, int step = 28) =>
            labels.Select((t, i) => new Button(t, new Vector2(0, startY + i * step))).ToList();

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

        private void HandleEncyclopedia(MouseState mouse)
        {
            foreach (var btn in encyclopediaButtons)
                if (btn.IsClicked(mouse, previousMouseState))
                    switch (btn.Text)
                    {
                        case "Armes": encyclopediaCategory = "Weapons"; Console.WriteLine("Affichage des armes"); break;
                        case "Armures": encyclopediaCategory = "Armor"; Console.WriteLine("Affichage des armures"); break;
                        case "Unités": encyclopediaCategory = "Units"; Console.WriteLine("Affichage des unités"); break;
                        case "Retour": currentState = GameState.MainMenu; break;
                    }
        }

        private float CalculateSunIntensity(float time)
        {
            if (time < 0.25f) return MathHelper.Lerp(0.3f, 0.7f, time / 0.25f);
            else if (time < 0.5f) return MathHelper.Lerp(0.7f, 1.0f, (time - 0.25f) / 0.25f);
            else if (time < 0.75f) return MathHelper.Lerp(1.0f, 0.7f, (time - 0.5f) / 0.25f);
            else return MathHelper.Lerp(0.7f, 0.3f, (time - 0.75f) / 0.25f);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(GetSkyColor(timeOfDay));
            if (currentState == GameState.Playing) DrawWorld3D(gameTime);

            _spriteBatch.Begin();

            switch (currentState)
            {
                case GameState.MainMenu:
                    DrawTitle("XCOM 3"); DrawButtons(menuButtons); break;
                case GameState.MissionSelect:
                    DrawTitle("Select Mission"); DrawButtons(missionButtons); break;
                case GameState.Playing:
                    if (showInventory) inventorySystem.Draw(selectedUnit);
                    else DrawPlayingUI();
                    break;
                case GameState.OptionsMenu:
                    DrawTitle("Options"); DrawButtons(optionsButtons); DrawVolumeControls(); break;
                case GameState.Encyclopedia: DrawEncyclopedia(); break;
                case GameState.GameOver: DrawGameOver(); break;
            }

            DrawOverlay();
            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void DrawButtons(List<Button> buttons)
        {
            MouseState mouse = Mouse.GetState();
            foreach (var button in buttons) button.Draw(_spriteBatch, font, mouse);
        }

        private void DrawVolumeControls()
        {
            _spriteBatch.Draw(pixel, volumeBar, Color.Gray);
            _spriteBatch.Draw(pixel, volumeFill, Color.Yellow);
            _spriteBatch.Draw(pixel, volumeHandle, Color.White);
        }

        private void DrawTitle(string text)
        {
            _spriteBatch.DrawString(font, text, Vector2.Zero, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);
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
            renderer3D.DrawWalls(wallSegments, cellSize);

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

        private void DrawEncyclopedia()
        {
            MouseState mouse = Mouse.GetState();
            _spriteBatch.DrawString(font, "ENCYCLOPEDIE", Vector2.Zero, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);

            foreach (var button in encyclopediaButtons) button.Draw(_spriteBatch, font, mouse);

            int contentX = 250, contentY = 100, lineHeight = 25, y = contentY;

            switch (encyclopediaCategory)
            {
                case "Weapons":
                    _spriteBatch.DrawString(font, "=== ARMES ===", new Vector2(contentX, y), Color.Yellow, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                    y += 40;
                    foreach (var weapon in weaponDatabase.Values.OrderBy(w => w.Name))
                    {
                        _spriteBatch.DrawString(font, weapon.Name, new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f); y += lineHeight;
                        _spriteBatch.DrawString(font, $"  Dégâts: {weapon.Damage}", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                        _spriteBatch.DrawString(font, $"  Précision: {weapon.Accuracy}%", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                        _spriteBatch.DrawString(font, $"  Portée: {weapon.Range} cases", new Vector2(contentX + 20, y), Color.White); y += lineHeight + 10;
                    }
                    break;

                case "Armor":
                    _spriteBatch.DrawString(font, "=== ARMURES ===", new Vector2(contentX, y), Color.Yellow, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                    y += 40;
                    var armors = inventorySystem.ItemDatabase.Values.Where(i => i.Type == ItemType.Armor).OrderBy(a => a.Name);
                    foreach (var armor in armors)
                    {
                        _spriteBatch.DrawString(font, armor.Name, new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f); y += lineHeight;
                        _spriteBatch.DrawString(font, $"  Protection: {armor.ArmorValue}", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                        string slot = armor.ArmorSlot == ArmorSlot.Head ? "Tête" : "Torse";
                        _spriteBatch.DrawString(font, $"  Emplacement: {slot}", new Vector2(contentX + 20, y), Color.White); y += lineHeight + 10;
                    }
                    break;

                case "Units":
                    _spriteBatch.DrawString(font, "=== UNITÉS ===", new Vector2(contentX, y), Color.Yellow, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
                    y += 40;

                    _spriteBatch.DrawString(font, "ÉQUIPE JOUEUR:", new Vector2(contentX, y), Color.Blue, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);
                    y += 35;
                    _spriteBatch.DrawString(font, "Soldat", new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f); y += lineHeight;
                    _spriteBatch.DrawString(font, "  Classe: Assault", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                    _spriteBatch.DrawString(font, "  Arme de base: Rifle", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                    _spriteBatch.DrawString(font, "  PV: 100", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                    _spriteBatch.DrawString(font, "  PA: 3 par tour", new Vector2(contentX + 20, y), Color.White); y += lineHeight + 20;

                    _spriteBatch.DrawString(font, "ENNEMIS:", new Vector2(contentX, y), Color.Red, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);
                    y += 35;

                    foreach (var enemy in enemyPool)
                    {
                        _spriteBatch.DrawString(font, enemy.Name, new Vector2(contentX, y), Color.Cyan, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f); y += lineHeight;
                        _spriteBatch.DrawString(font, $"  Classe: {enemy.Class}", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                        _spriteBatch.DrawString(font, $"  Arme: {enemy.Weapon}", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                        var w = weaponDatabase[enemy.Weapon];
                        _spriteBatch.DrawString(font, $"  Dégâts: {w.Damage} | Portée: {w.Range}", new Vector2(contentX + 20, y), Color.White); y += lineHeight;
                        _spriteBatch.DrawString(font, $"  PA: {enemy.ActionPoints} par tour", new Vector2(contentX + 20, y), Color.White); y += lineHeight + 15;
                    }
                    break;
            }

            _spriteBatch.DrawString(font, "Appuyez sur ESC pour retourner au menu",
                new Vector2(10, GraphicsDevice.Viewport.Height - 30), Color.Yellow);
        }

        private void LoadMap()
        {
            // --- Génération aléatoire de la taille de la carte ---
            gridWidth = random.Next(20, 100);
            gridHeight = random.Next(20, 100);
            cellSize = 2;

            timeOfDay = (float)random.NextDouble();
            dayNightSpeed = 1f / 86400f;

            // --- Génération des murs ---
            GenerateWalls(gridWidth * gridHeight / 10);
            Console.WriteLine($"Map loaded: {gridWidth}x{gridHeight}, Starting time: {GetTimeOfDayString(timeOfDay)}");

            // --- Réinitialisation de la caméra pour la nouvelle carte ---
            if (camera != null)
            {
                camera = new CameraController(gridWidth, gridHeight, cellSize, GraphicsDevice.Viewport.AspectRatio);
                camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);

                // Si une unité est déjà sélectionnée, centrer la caméra dessus
                if (selectedUnit != null)
                    camera.CenterOnPosition(selectedUnit.Cell.X * cellSize, selectedUnit.Cell.Y * cellSize);
            }

            // --- Mise à jour du pathfinding pour la nouvelle carte ---
            if (pathfinding != null)
            {
                pathfinding.UpdateGrid(gridWidth, gridHeight, wallSegments);
            }

            // --- Réinitialisation des unités pour la nouvelle taille de cellule ---
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

            // --- Recalcul des cellules navigables et du hover ---
            if (selectedUnit != null && pathfinding != null)
                cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);

            currentPath.Clear();
            pathCosts.Clear();
            hoveredCell = new Point(-1, -1);
            throwTarget = new Point(-1, -1);

            // --- Réinitialisation de la spatial hash pour le nouveau setup ---
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
                playerUnits.Add(new Unit(new Point(2 + i, gridHeight - 2), Team.Player, "Soldier " + (i + 1), "Assault", "Rifle", weaponDatabase["Rifle"]));

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
                    var zombie = enemyPool.First(e => e.Name == "Zombie");
                    for (int i = 0; i < 12; i++)
                        enemyUnits.Add(new Unit(new Point(2 + (i % 8), i < 8 ? 1 : 2), Team.Enemy, zombie.Name, zombie.Class, zombie.Weapon, weaponDatabase[zombie.Weapon]) { ActionPoints = zombie.ActionPoints });
                    break;
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
                        case "Tutorial": selectedMission = "Tutorial"; StartMission(selectedMission); break;
                        case "Survival": selectedMission = "Survival"; StartMission(selectedMission); break;
                        case "Assault": selectedMission = "Assault"; StartMission(selectedMission); break;
                        case "Defense": selectedMission = "Defense"; StartMission(selectedMission); break;
                        case "Back": currentState = GameState.MainMenu; break;
                    }
        }

        private void StartMission(string missionType)
        {
            MediaPlayer.Stop();
            currentState = GameState.Playing;
            LoadMap();
            CreateUnits(missionType);

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, wallSegments, GetUnitAtCell);

            Console.WriteLine($"Mission '{missionType}' launched in 3D!");

            unitManager.InitializeForMission(playerUnits, enemyUnits);

            combatSystem.SetUnits(playerUnits, enemyUnits);
            combatSystem.StartPlayerTurn();

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

                    case "COUVERT":
                        Console.WriteLine("Action future : COUVERT");
                        break;

                    case "RECHARGER":
                        Console.WriteLine("Action future : RECHARGER");
                        break;
                }
                return;
            }
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

    public class WeaponData
    {
        public string Name;
        public int Damage, Accuracy, Range;
        public WeaponData(string name, int damage, int accuracy, int range)
        { Name = name; Damage = damage; Accuracy = accuracy; Range = range; }
    }

    public static class Extensions { public static Vector2 ToVector2(this Point p) => new(p.X, p.Y); }
}