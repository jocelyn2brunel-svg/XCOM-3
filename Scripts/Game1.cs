using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using NVorbis.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XCOM_3
{
    public partial class Game1 : Game
    {
        private readonly struct WindowInstance : IEquatable<WindowInstance>
        {
            public int Floor { get; }
            public WallSegment Segment { get; }

            public WindowInstance(int floor, WallSegment segment)
            {
                Floor = floor;
                Segment = segment;
            }

            public bool Equals(WindowInstance other)
            {
                return Floor == other.Floor && Segment.Equals(other.Segment);
            }

            public override bool Equals(object obj)
            {
                return obj is WindowInstance other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Floor, Segment);
            }
        }

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
        private Texture2D asphaltTexture;
        private Texture2D sidewalkTexture;
        private Texture2D brickWallTexture;
        private Texture2D upperWallTexture;
        private Texture2D hescoWallTexture;

        // --- Systèmes ---
        private CombatSystem combatSystem;
        private CombatUISystem combatUI;

        // --- Cycle jour/nuit ---
        private float timeOfDay = 0f;
        private float dayNightSpeed = 0.01f;
        private Color ambientLight = Color.White;
        private Color directionalLight = Color.White;
        private float uiAnimationTimeSeconds = 0f;

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
        private bool throwModeUsesFlashlight = false;
        private bool throwFlashlightFromRightHand = false;
        private Point throwTarget = new Point(-1, -1);
        private List<Point> throwableCells = new List<Point>();
        private Unit throwableCellsCachedUnit = null;
        private int throwableCellsCachedFloor = -1;
        private int throwableCellsCachedRange = -1;
        private bool throwableCellsCacheValid = false;
        private List<Point> explosionPreview = new List<Point>();
        private List<Vector3> trajectoryPreview = new List<Vector3>();
        private List<Vector3> ricochetPreview = new List<Vector3>();
        private readonly List<FlashlightLootMarker> flashlightLootMarkers = new List<FlashlightLootMarker>();
        private bool grappleMode = false;
        private bool c4PlacementMode = false;
        private int grappleTargetFloor = -1;
        private List<GrappleAnchor> grappleAnchors = new List<GrappleAnchor>();
        private int lastProcessedGrapplePlayerTurn = -1;
        private readonly List<PlantedSatchelCharge> plantedSatchelCharges = new List<PlantedSatchelCharge>();

        // Constantes
        private const int BaseThrowRange = 20;
        private const int TacticalFlashlightRangeCells = 40;
        private const int TacticalFlashlightThrowApCost = 1;
        private const string TacticalFlashlightItemName = "Lampe tactique aluminium";
        private const string GrapplingHookItemName = "Grappin tactique";
        private const int GrappleActionPointCost = 1;
        private const int GrappleBaseThrowStrengthFeet = 15;
        private const int GrappleMaxThrowStrengthFeet = 30;
        private const int GrappleFloorHeightFeet = 10;
        private const int GrappleBaseAccuracyPercent = 78;
        private const int GrappleHeightPenaltyPercentPerFloor = 14;
        private const int GrappleConcentrationClimbFloorsPerTurn = 2;
        private const float Mk2WeightLbs = 1.3228f; // 600 grammes
        private const float OverwatchShotIntervalSeconds = 3f;
        private const int SatchelPlacementRange = 1;
        private const int SatchelDetonationActionPointCost = 1;
        private const float UpperFloorWallOpacityWhenLookingBelow = 0.01f;

        // Options avancées de lancer de grenade (activables/désactivables facilement).
        private bool grenadeOptionWallAwareTargeting = true;      // Option 1: validation par murs/fenêtres.
        private bool grenadeOptionArcCollisionSampling = true;    // Option 2: collisions en échantillonnant l'arc.
        private bool grenadeOptionRicochet = true;                // Option 3: ricochet sur murs pleins.
        private bool grenadeOptionThrowFeedback = true;           // Option 4: feedback visuel (arc/ricochet).

        // --- Système de cartes ---
        private MapData currentMap;
        private MapGenerator mapGenerator;
        private MapEditor mapEditor;

        // --- États du jeu ---
        enum GameState { MainMenu, CharacterCreation, MissionSelect, Playing, HumanBodyEditor, OptionsMenu, GameOver, Encyclopedia, MapEditor }
        private GameState currentState = GameState.MainMenu;

        // --- Grille 3D ---
        private int cellSize = 2;
        private int gridWidth = 50;
        private int gridHeight = 50;
        private Point hoveredCell = new Point(-1, -1);
        private int hoveredCellFloor = 0;
        private bool isHoveringValidCell;

        // --- Murs sur les edges des cases ---
        private HashSet<WallSegment> wallSegments = new HashSet<WallSegment>();
        private readonly HashSet<WindowInstance> shatteredWindows = new HashSet<WindowInstance>();
        private readonly HashSet<(int Floor, int X, int Y)> shatteredVehicleWindows = new HashSet<(int Floor, int X, int Y)>();
        private EdgeWallGenerator edgeWallGenerator;

        // --- Unités et combat ---
        private List<Unit> playerUnits = new List<Unit>();
        private List<Unit> enemyUnits = new List<Unit>();
        private readonly List<DeadUnitRemains> deadUnitRemains = new List<DeadUnitRemains>();
        private Unit selectedUnit = null;
        private List<Point> cachedMovableCells = new();
        private List<Unit> savedPlayerUnits;
        private List<Unit> savedEnemyUnits;
        private bool hasSavedGame = false;

        // --- A* Pathfinding ---
        private List<Point> currentPath = new();
        private List<GridNode> currentPathNodes = new();
        private int currentPathEndFloor = 0;
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

        private bool visibilityDirty = true;
        private bool exploredCachesDirty = true;
        private TurnState lastTurnState = TurnState.Busy;

        private Dictionary<int, List<Point>> exploredHescoCache = new();
        private Dictionary<int, List<FurnitureData>> exploredFurnitureCache = new();
        private Dictionary<int, List<WallSegment>> exploredWallsCache = new();
        private int[,] buildingIndexGrid;

        // ✅ NOUVEAU CODE - Managers
        private MainMenuManager mainMenuManager;
        private CharacterCreationManager characterCreationManager;
        private MissionSelectManager missionSelectManager;
        private OptionsMenuManager optionsMenuManager;
        private HumanBodyEditorManager humanBodyEditorManager;
        private EncyclopediaManager encyclopediaManager;

        // Garder ces champs (toujours utilisés ailleurs)
        private MouseState previousMouseState;
        private Random random = new Random();
        private string selectedMission = ""; // Utilisé dans CreateUnits et StartMission
        private List<CharacterCreationProfile> createdSquadProfiles = new List<CharacterCreationProfile>();

        private StatsPanel statsPanel;
        private CharacterInfoPanel characterInfoPanel;

        private bool showCoverIndicators = false;
        private bool premadeMapsChecked = false;

        private Point lastHoveredCell = new Point(-1, -1);
        private int lastHoveredInteractionFloor = -1;
        private int viewedFloor = 0;
        private enum FloorViewMode { AutoFollow, Manual, AbilityLocked }
        private FloorViewMode floorViewMode = FloorViewMode.AutoFollow;
        private bool explicitUpperFloorTargeting = false;
        private HashSet<Point> upperFloorCells = new();
        private HashSet<Point> roadCells = new();
        private HashSet<Point> sidewalkCells = new();
        private Dictionary<Point, float> terrainHeights = new Dictionary<Point, float>();
        private readonly Dictionary<int, HashSet<Point>> cellsByFloorCache = new Dictionary<int, HashSet<Point>>();
        private readonly Dictionary<int, bool[,]> slabMasks = new();
        private readonly Dictionary<int, bool[,]> coveredMasks = new();

        private Unit movementCinematicUnit = null;
        private readonly Dictionary<Unit, bool> firingShoulderCameraDecisions = new Dictionary<Unit, bool>();
        private HashSet<Unit> currentlySpottedEnemies = new HashSet<Unit>();

        // --- Système de brouillard de guerre ---
        private Dictionary<int, bool[,]> exploredCells = new();
        private Dictionary<int, bool[,]> visibleCells = new();
        private List<Unit> enemyGhosts = new();

        private SoundEffect centreVilleMusicEffect;
        private SoundEffectInstance centreVilleMusicEffectInstance;
        private SoundEffect gunshotSoundEffect;
        private SoundEffectInstance gunshotSoundEffectInstance;
        private SoundEffect casingClingSoundEffect;
        private SoundEffect grenadeExplosionSoundEffect;

        private enum UnitPageTab { Inventory, Skills, Info }
        private const int TabWidth = 170;
        private const int TabHeight = 42;
        private const int TabSpacing = 8;
        private const int TabTopMargin = 12;
        private const float WallHeightRatio = 2.0f;
        private const int HoverRevealRadius = 2;
        private const int UpperFloorCutoutRadius = 2;
        private const bool AlwaysDrawUnitGhostOutline = true;
        private const bool AntiOcclusionCameraEnabled = false;
        private const float AntiOcclusionCameraMaxHeightCells = 1.0f;
        private const float AntiOcclusionCameraMaxOrbitDegrees = 8f;
        private const int AntiOcclusionOccluderThreshold = 6;
        private const int FloorUiButtonY = 88;
        private const int FloorUiButtonSize = 28;
        private RasterizerState hoveredCellWireframeState;
        private float antiOcclusionCameraHeight;
        private float antiOcclusionCameraOrbit;

        private sealed class DeadUnitRemains
        {
            public Unit UnitSnapshot;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Yaw;
            public float Pitch;
            public float Roll;
            public float AngularPitchVelocity;
            public float AngularRollVelocity;
            public bool IsGrounded;
            public float SettledTimer;
        }

        private struct FlashlightLootMarker
        {
            public Point Cell;
            public int Floor;
            public int Quantity;
            public float PulseSeed;
            public bool IsOn;
        }

        private struct PlantedSatchelCharge
        {
            public Point Cell;
            public int Floor;
            public Team Team;
            public Unit Owner;
        }

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
            tileTexture = LoadTileTexture();
            asphaltTexture = LoadAsphaltTexture();
            sidewalkTexture = LoadSidewalkTexture();
            brickWallTexture = LoadBrickWallTexture();
            upperWallTexture = LoadUpperWallTexture();
            hescoWallTexture = LoadHescoWallTexture();
            hoveredCellWireframeState = new RasterizerState
            {
                CullMode = CullMode.None,
                FillMode = FillMode.WireFrame
            };

            InitializeMenuManagers();
            InitializeGameplaySystems();
            InitializeMapSystems();
            InitializeDatabasesAndEncyclopedia();
            InitializeAudioSystems();

            explosionManager = new ExplosionManager(random);
            edgeWallGenerator = new EdgeWallGenerator(random);
            humanoidBatcher = new HumanoidBatchRenderer();

            Console.WriteLine("[OPTIMIZATION] Batch renderer and spatial hash initialized");
        }

        private Texture2D LoadTileTexture()
        {
            return LoadFirstAvailableTexture(
                new[] { "TileParchment32x32.png", "Crate32x32.jpg" },
                "tile",
                pixel);
        }

        private Texture2D LoadBrickWallTexture()
        {
            return LoadFirstAvailableTexture(
                new[] { "BrickWall32x32.jpeg", "BrickWall32x32.jpg", "BrickWall32x32.png" },
                "brick wall",
                pixel);
        }

        private Texture2D LoadUpperWallTexture()
        {
            return LoadFirstAvailableTexture(
                new[] { "BrickWall32x32.jpeg", "BrickWall32x32.jpg", "BrickWall32x32.png" },
                "upper wall",
                brickWallTexture ?? pixel);
        }

        private Texture2D LoadAsphaltTexture()
        {
            return LoadFirstAvailableTexture(
                new[] { "Asphalt32x32.jpg", "Asphalt32x32.jpeg", "Asphalt32x32.png" },
                "asphalt",
                tileTexture ?? pixel);
        }

        private Texture2D LoadSidewalkTexture()
        {
            return LoadFirstAvailableTexture(
                new[] { "Sidewalk32x32.jpg", "Sidewalk32x32.jpeg", "Sidewalk32x32.png" },
                "sidewalk",
                asphaltTexture ?? tileTexture ?? pixel);
        }

        private Texture2D LoadHescoWallTexture()
        {
            return LoadFirstAvailableTexture(
                new[] { "HescoBarrier32x32.jpg", "HescoBarrier32x32.jpeg", "HescoBarrier32x32.png" },
                "hesco wall",
                pixel);
        }

        private Texture2D LoadFirstAvailableTexture(string[] textureFileNames, string textureLabel, Texture2D fallback)
        {
            var searchRoots = new List<string>
            {
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory()
            };

            DirectoryInfo rootProbe = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && rootProbe.Parent != null; i++)
            {
                rootProbe = rootProbe.Parent;
                searchRoots.Add(rootProbe.FullName);
            }

            foreach (string root in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (string fileName in textureFileNames)
                {
                    foreach (string path in EnumerateCandidateTexturePaths(root, fileName))
                    {
                        if (!File.Exists(path))
                            continue;

                        using var stream = File.OpenRead(path);
                        Console.WriteLine($"[RENDER] Loaded {textureLabel} texture: {Path.GetFileName(path)} ({path})");
                        return Texture2D.FromStream(GraphicsDevice, stream);
                    }
                }
            }

            Console.WriteLine($"[RENDER] {textureLabel} texture missing, using fallback texture.");
            return fallback;
        }

        private static IEnumerable<string> EnumerateCandidateTexturePaths(string root, string fileName)
        {
            yield return Path.Combine(root, fileName);
            yield return Path.Combine(root, "Content", fileName);
            yield return Path.Combine(root, "Content", "Textures", fileName);
            yield return Path.Combine(root, "Assets", fileName);
            yield return Path.Combine(root, "Assets", "Textures", fileName);
            yield return Path.Combine(root, "Textures", fileName);
        }

        private void InitializeMenuManagers()
        {

            // ✅ INITIALISATION DES MANAGERS

            // 1. Main Menu Manager
            mainMenuManager = new MainMenuManager(_graphics.GraphicsDevice, _spriteBatch, font, random);
            mainMenuManager.LoadContent(Content);
            mainMenuManager.OnNewGameRequested += StartNewGame;
            mainMenuManager.OnCharacterCreationRequested += OpenCharacterCreation;
            mainMenuManager.OnContinueRequested += HandleContinue;
            mainMenuManager.OnMapEditorRequested += OpenMapEditor;
            mainMenuManager.OnEncyclopediaRequested += OpenEncyclopedia;
            mainMenuManager.OnBodyEditorRequested += OpenHumanBodyEditor;
            mainMenuManager.OnOptionsRequested += OpenOptionsMenu;
            mainMenuManager.OnQuitRequested += () => Exit();

            characterCreationManager = new CharacterCreationManager(_spriteBatch, font, random);
            characterCreationManager.LoadContent();
            characterCreationManager.OnCharacterCreationCompleted += HandleCharacterCreationCompleted;
            characterCreationManager.OnBackToMainMenu += ReturnToMainMenu;

            // 2. Mission Select Manager
            missionSelectManager = new MissionSelectManager(GraphicsDevice, _spriteBatch, font, pixel);
            missionSelectManager.OnMissionSelected += HandleMissionSelected;
            missionSelectManager.OnBackToMainMenu += ReturnToMainMenu;

            // 3. Options Menu Manager
            optionsMenuManager = new OptionsMenuManager(_graphics.GraphicsDevice, _spriteBatch, font, pixel);
            optionsMenuManager.OnBackToMainMenu += ReturnToMainMenu;

            humanBodyEditorManager = new HumanBodyEditorManager(_graphics.GraphicsDevice, _spriteBatch, font, pixel);
            humanBodyEditorManager.OnBackToMainMenu += ReturnToMainMenu;

            // 4. Encyclopedia Manager (nécessite weaponDatabase et inventorySystem)
            // On l'initialise APRÈS InitializeWeapons() et la création de inventorySystem
        }


        protected override void UnloadContent()
        {
            VisualEffects.OnSpentCasingLanded -= HandleSpentCasingLanded;
            gunshotSoundEffectInstance?.Dispose();
            centreVilleMusicEffectInstance?.Dispose();
            centreVilleMusicEffect?.Dispose();
            grenadeExplosionSoundEffect?.Dispose();
            casingClingSoundEffect?.Dispose();
            gunshotSoundEffect?.Dispose();

            base.UnloadContent();
        }
        private void InitializeGameplaySystems()
        {
            // tileTexture = Content.Load<Texture2D>("TileParchment32x32");

            renderer3D = new Renderer3D(GraphicsDevice);
            renderer3D.IsVehicleWindowShattered = (f, x, y) => shatteredVehicleWindows.Contains((f, x, y));
            camera = new CameraController(gridWidth, gridHeight, cellSize, GraphicsDevice.Viewport.AspectRatio);
            inventorySystem = new InventorySystem(GraphicsDevice, _spriteBatch, font, pixel);
            unitManager = new OptimizedUnitManager();

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, 1, new Dictionary<int, HashSet<WallSegment>>(), new List<RampTileData>(), GetUnitAtCell, GetUnitAtCellOnFloor, IsCellAvailableOnFloor);
            statsPanel = new StatsPanel(
                Content.Load<SpriteFont>("Arial"),
                GraphicsDevice);
            characterInfoPanel = new CharacterInfoPanel(font, GraphicsDevice);

            combatSystem = new CombatSystem(random, pathfinding, GetUnitAtCell, GetFurnitureAtCellOnFloor, unitManager);
            combatSystem.SetEnemyVisibilityEvaluator((enemy, cell, floor) => IsEnemyCellVisibleToPlayers(enemy, cell, floor));
            combatUI = new CombatUISystem(GraphicsDevice, _spriteBatch, font, pixel);
            combatSystem.OnUnitKilled += HandleUnitKilled;
            combatSystem.OnFireCompleted += HandleFireCompleted;
            combatSystem.OnRoundFired += HandleShotFired;

            Window.ClientSizeChanged += (_, _) =>
            {
                combatUI.UpdateFireTargetsUIPositions(selectedUnit);
                camera.UpdateProjection(GraphicsDevice.Viewport.AspectRatio);
            };
            mapEditor?.UpdateViewportSize(
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height
            );
        }

        private void InitializeMapSystems()
        {
            // ✅ NOUVEAU : Initialiser le système de cartes
            mapGenerator = new MapGenerator(random);
            mapEditor = new MapEditor(camera, renderer3D, font, pixel, _spriteBatch);
        }

        private void InitializeAudioSystems()
        {
            gunshotSoundEffect = CreateProceduralGunshotSound();
            gunshotSoundEffectInstance = gunshotSoundEffect?.CreateInstance();
            casingClingSoundEffect = CreateProceduralCasingClingSound();
            grenadeExplosionSoundEffect = CreateProceduralGrenadeExplosionSound();
            VisualEffects.OnSpentCasingLanded += HandleSpentCasingLanded;
        }

        private SoundEffect CreateProceduralGrenadeExplosionSound()
        {
            const int sampleRate = 44100;
            const float durationSeconds = 0.58f;
            int sampleCount = (int)(sampleRate * durationSeconds);
            short[] samples = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float rumble = (float)Math.Sin(2f * Math.PI * 62f * t);
                float snap = (float)Math.Sin(2f * Math.PI * 430f * t) * (float)Math.Exp(-42f * t);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float envelope = (float)Math.Exp(-4.6f * t);
                float value = (rumble * 0.45f + snap * 0.25f + noise * 0.65f) * envelope;
                samples[i] = (short)(MathHelper.Clamp(value, -1f, 1f) * short.MaxValue);
            }

            return new SoundEffect(ConvertPcm16ToBytes(samples), sampleRate, AudioChannels.Mono);
        }

        private SoundEffect CreateProceduralGunshotSound()
        {
            const int sampleRate = 44100;
            const float durationSeconds = 0.18f;
            int sampleCount = (int)(sampleRate * durationSeconds);
            short[] samples = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = (float)Math.Exp(-18f * t);
                float bass = (float)Math.Sin(2f * Math.PI * 95f * t);
                float crack = (float)(random.NextDouble() * 2.0 - 1.0);
                float value = (0.35f * bass + 0.65f * crack) * envelope;
                samples[i] = (short)(MathHelper.Clamp(value, -1f, 1f) * short.MaxValue);
            }

            return new SoundEffect(ConvertPcm16ToBytes(samples), sampleRate, AudioChannels.Mono);
        }

        private SoundEffect CreateProceduralCasingClingSound()
        {
            const int sampleRate = 44100;
            const float durationSeconds = 0.22f;
            int sampleCount = (int)(sampleRate * durationSeconds);
            short[] samples = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = (float)Math.Exp(-16f * t);
                float ringA = (float)Math.Sin(2f * Math.PI * 1750f * t);
                float ringB = (float)Math.Sin(2f * Math.PI * 2380f * t + 0.35f);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float value = (ringA * 0.45f + ringB * 0.35f + noise * 0.2f) * envelope;
                samples[i] = (short)(MathHelper.Clamp(value, -1f, 1f) * short.MaxValue);
            }

            return new SoundEffect(ConvertPcm16ToBytes(samples), sampleRate, AudioChannels.Mono);
        }

        private static byte[] ConvertPcm16ToBytes(short[] samples)
        {
            if (samples == null || samples.Length == 0)
                return Array.Empty<byte>();

            byte[] bytes = new byte[samples.Length * sizeof(short)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private void HandleShotFired(Unit shooter)
        {
            if (gunshotSoundEffect == null)
                return;

            float volume = MathHelper.Clamp(0.4f + optionsMenuManager.GetMusicVolume() * 0.25f, 0.2f, 0.8f);
            float pitch = MathHelper.Clamp((float)(random.NextDouble() * 0.16 - 0.08), -1f, 1f);
            float pan = MathHelper.Clamp((shooter.VisualPosition.X - camera.Position.X) / Math.Max(1f, cellSize * 16f), -0.7f, 0.7f);

            gunshotSoundEffect.Play(volume, pitch, pan);

            VisualEffects.PlaySpentCasingEjection(shooter, cellSize, renderer3D);
        }

        private void HandleSpentCasingLanded(Vector3 landingPosition)
        {
            if (casingClingSoundEffect == null)
                return;

            float music = optionsMenuManager?.GetMusicVolume() ?? 0.5f;
            float volume = MathHelper.Clamp(0.16f + music * 0.24f, 0.1f, 0.45f);
            float pitch = MathHelper.Clamp((float)(random.NextDouble() * 0.26 - 0.13), -1f, 1f);
            float pan = MathHelper.Clamp((landingPosition.X - camera.Position.X) / Math.Max(1f, cellSize * 16f), -0.7f, 0.7f);

            casingClingSoundEffect.Play(volume, pitch, pan);
        }

        private void PlayGrenadeExplosionSound(Vector3 explosionPosition, float explosionRadius)
        {
            if (grenadeExplosionSoundEffect == null)
                return;

            float distance = Vector3.Distance(explosionPosition, camera.Position);
            float attenuation = 1f / (1f + distance / Math.Max(cellSize * 8f, 1f));
            float radiusBoost = MathHelper.Clamp(explosionRadius / 6f, 0.75f, 1.35f);
            float music = optionsMenuManager?.GetMusicVolume() ?? 0.5f;
            float volume = MathHelper.Clamp((0.2f + music * 0.4f) * attenuation * radiusBoost, 0.05f, 0.95f);
            float pitch = MathHelper.Clamp((float)(random.NextDouble() * 0.08 - 0.04), -1f, 1f);
            float pan = MathHelper.Clamp((explosionPosition.X - camera.Position.X) / Math.Max(1f, cellSize * 14f), -0.85f, 0.85f);

            grenadeExplosionSoundEffect.Play(volume, pitch, pan);
        }

        private void StopCentreVilleMusicLoop()
        {
            if (centreVilleMusicEffectInstance != null)
                centreVilleMusicEffectInstance.Stop();
        }

        private void EnsurePremadeMapsGenerated()
        {
            if (premadeMapsChecked)
                return;

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

            premadeMapsChecked = true;
        }

        private void InitializeDatabasesAndEncyclopedia()
        {
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
            encyclopediaManager.OnBackToMainMenu += ReturnToMainMenu;
        }

        private void OpenCharacterCreation() => currentState = GameState.CharacterCreation;

        private void StartNewGame()
        {
            createdSquadProfiles.Clear();
            currentState = GameState.MissionSelect;
        }

        private void OpenMapEditor()
        {
            EnsurePremadeMapsGenerated();
            mapEditor.StartNewMap(50, 50);
            currentState = GameState.MapEditor;
        }

        private void OpenEncyclopedia() => currentState = GameState.Encyclopedia;

        private void OpenHumanBodyEditor() => currentState = GameState.HumanBodyEditor;

        private void OpenOptionsMenu() => currentState = GameState.OptionsMenu;

        private void ReturnToMainMenu()
        {
            StopCentreVilleMusicLoop();
            currentState = GameState.MainMenu;
            mainMenuManager.ResetToRootMenu();
            mainMenuManager.PlayRandomMenuSong();
        }

        private void HandleCharacterCreationCompleted(List<CharacterCreationProfile> profiles)
        {
            createdSquadProfiles = profiles;
            currentState = GameState.MissionSelect;
        }

        private void HandleMissionSelected(string missionType)
        {
            selectedMission = missionType;
            StartMission(missionType);
        }

        protected override void Update(GameTime gameTime)
        {
            UpdateFPS(gameTime);

            ReadInputs(out bool leftClick, out bool escapePressed, out bool iPressed,
                       out MouseState mouse, out KeyboardState keyboard);

            HandleUnitPageShortcuts(iPressed, keyboard);

            if (leftClick && currentState == GameState.Playing)
            {
                if (TryHandleUnitTabClick(mouse.Position))
                    leftClick = false;
            }

            statsPanel.Update(gameTime, mouse, previousMouseState);

            renderer3D.Update(gameTime);

            UpdateGrenades(gameTime);

            UpdateCurrentState(gameTime, mouse, keyboard, leftClick, escapePressed);

            previousMouseState = mouse;
            previousKeyboardState = keyboard;

            VisualEffects.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            uiAnimationTimeSeconds = (float)gameTime.TotalGameTime.TotalSeconds;

            renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);

            GraphicsDevice.Clear(GetSkyColor(timeOfDay));

            if (currentState == GameState.MapEditor)
                mapEditor.Draw3D(gameTime);
            if (currentState == GameState.Playing)
                DrawWorld3D(gameTime); // monde + unités + murs

            if (currentState == GameState.Playing && characterInfoPanel.IsVisible && selectedUnit?.Team == Team.Player)
                characterInfoPanel.DrawPreview3D(selectedUnit);

            if (currentState == GameState.Playing && showInventory && selectedUnit?.Team == Team.Player)
                inventorySystem.DrawPreview3D(selectedUnit);

            if (currentState == GameState.HumanBodyEditor)
                humanBodyEditorManager.DrawPreview3D();

            // --- EFFETS VISUELS 3D ---
            VisualEffects.Draw(); // explosions et particules

            _spriteBatch.Begin();

            DrawCurrentStateUI();
            statsPanel.Draw(_spriteBatch, selectedUnit);
            characterInfoPanel.Draw(_spriteBatch, selectedUnit);
            DrawUnitPageTabs();

            DrawOverlay();

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void UpdateCurrentState(GameTime gameTime, MouseState mouse, KeyboardState keyboard, bool leftClick, bool escapePressed)
        {
            switch (currentState)
            {
                case GameState.MainMenu:
                    mainMenuManager.Update(mouse, previousMouseState);
                    break;

                case GameState.CharacterCreation:
                    characterCreationManager.Update(mouse, previousMouseState);
                    if (escapePressed) ReturnToMainMenu();
                    break;

                case GameState.MissionSelect:
                    missionSelectManager.Update(mouse, previousMouseState);
                    if (escapePressed) ReturnToMainMenu();
                    break;

                case GameState.Playing:
                    UpdatePlaying(gameTime, mouse, keyboard, leftClick, escapePressed);
                    combatUI.Update(gameTime);
                    break;

                case GameState.MapEditor:
                    UpdateMapEditorState(gameTime, mouse, keyboard, escapePressed);
                    break;

                case GameState.HumanBodyEditor:
                    humanBodyEditorManager.Update(mouse, previousMouseState);
                    if (escapePressed) ReturnToMainMenu();
                    break;

                case GameState.OptionsMenu:
                    optionsMenuManager.Update(mouse, previousMouseState);
                    if (escapePressed) ReturnToMainMenu();
                    break;

                case GameState.Encyclopedia:
                    encyclopediaManager.Update(mouse, previousMouseState);
                    if (escapePressed) ReturnToMainMenu();
                    break;

                case GameState.GameOver:
                    if (escapePressed || leftClick) ReturnToMainMenu();
                    break;
            }
        }

        private void UpdateMapEditorState(GameTime gameTime, MouseState mouse, KeyboardState keyboard, bool escapePressed)
        {
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
                ReturnToMainMenu();

            if (escapePressed)
            {
                mapEditor.Exit();
                ReturnToMainMenu();
            }
        }

        private void DrawCurrentStateUI()
        {
            switch (currentState)
            {
                case GameState.MainMenu:
                    mainMenuManager.Draw();
                    break;

                case GameState.CharacterCreation:
                    characterCreationManager.Draw();
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

                case GameState.HumanBodyEditor:
                    humanBodyEditorManager.Draw();
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
        }


        private void HandleUnitKilled(Unit unit, Vector3 kineticImpulse)
        {
            unit.OnCellEntered -= HandleUnitCellEntered;
            DropUnitLootToGround(unit);
            RegisterDeadUnitRemains(unit, kineticImpulse);
            if (unit.Team == Team.Player) { playerUnits.Remove(unit); if (playerUnits.Count == 0) currentState = GameState.GameOver; }
            else enemyUnits.Remove(unit);
            unitManager.OnUnitDied(unit);
            visibilityDirty = true;
        }


        private void HandleUnitKilled(Unit unit)
        {
            HandleUnitKilled(unit, Vector3.Zero);
        }

        private void RegisterDeadUnitRemains(Unit unit, Vector3 kineticImpulse)
        {
            if (unit == null)
                return;

            Unit snapshot = new Unit(unit)
            {
                Health = 0,
                IsMoving = false,
                IsAiming = false,
                IsFiring = false,
                IdleBobOffset = 0f,
                BodyBob = 0f,
                ArmSwing = 0f,
                LegSwing = 0f
            };

            Vector3 impulse = kineticImpulse;
            if (impulse.LengthSquared() < 0.0001f)
            {
                impulse = new Vector3(
                    (float)(random.NextDouble() * 2.0 - 1.0),
                    0.25f,
                    (float)(random.NextDouble() * 2.0 - 1.0));
            }

            float impulseStrength = MathHelper.Clamp(impulse.Length(), 0.6f, 4.2f);
            Vector3 impulseDirection = impulse;
            impulseDirection.Normalize();

            deadUnitRemains.Add(new DeadUnitRemains
            {
                UnitSnapshot = snapshot,
                Position = snapshot.VisualPosition,
                Velocity = impulseDirection * (cellSize * (0.55f + impulseStrength * 0.35f)),
                Yaw = snapshot.Orientation + (float)Math.Atan2(impulseDirection.X, impulseDirection.Z) * 0.18f,
                Pitch = 0f,
                Roll = 0f,
                AngularPitchVelocity = -impulseDirection.Z * (1.2f + impulseStrength * 1.15f),
                AngularRollVelocity = impulseDirection.X * (1.2f + impulseStrength * 1.15f),
                IsGrounded = false,
                SettledTimer = 0f
            });

            if (deadUnitRemains.Count > 60)
                deadUnitRemains.RemoveAt(0);
        }

        private void DropUnitLootToGround(Unit unit)
        {
            if (unit == null || inventorySystem == null)
                return;

            foreach (string itemName in CollectUnitLootNames(unit))
            {
                RegisterGroundLoot(itemName, unit.Cell, unit.Floor);
            }
        }

        private IEnumerable<string> CollectUnitLootNames(Unit unit)
        {
            if (unit?.EquippedWeapon?.Data?.Name != null) yield return unit.EquippedWeapon.Data.Name;
            if (unit?.EquippedHelmet?.Data?.Name != null) yield return unit.EquippedHelmet.Data.Name;
            if (unit?.EquippedNeck?.Data?.Name != null) yield return unit.EquippedNeck.Data.Name;
            if (unit?.EquippedArmor?.Data?.Name != null) yield return unit.EquippedArmor.Data.Name;
            if (unit?.EquippedShield?.Data?.Name != null) yield return unit.EquippedShield.Data.Name;
            if (unit?.EquippedAccessory?.Data?.Name != null) yield return unit.EquippedAccessory.Data.Name;
            if (unit?.EquippedRightHandFlashlight?.Data?.Name != null) yield return unit.EquippedRightHandFlashlight.Data.Name;
            if (unit?.EquippedLeftHandFlashlight?.Data?.Name != null) yield return unit.EquippedLeftHandFlashlight.Data.Name;
            if (unit?.EquippedShirt?.Data?.Name != null) yield return unit.EquippedShirt.Data.Name;
            if (unit?.EquippedPants?.Data?.Name != null) yield return unit.EquippedPants.Data.Name;
            if (unit?.EquippedKnees?.Data?.Name != null) yield return unit.EquippedKnees.Data.Name;
            if (unit?.EquippedFeet?.Data?.Name != null) yield return unit.EquippedFeet.Data.Name;
            if (unit?.EquippedChestRig?.Data?.Name != null) yield return unit.EquippedChestRig.Data.Name;
            if (unit?.EquippedBelt?.Data?.Name != null) yield return unit.EquippedBelt.Data.Name;

            if (!string.IsNullOrWhiteSpace(unit?.EquippedBackpack))
                yield return unit.EquippedBackpack;

            if (unit?.PantsInventory != null)
            {
                foreach (Item item in unit.PantsInventory)
                    if (item?.Data?.Name != null)
                        yield return item.Data.Name;
            }

            if (unit?.ChestRigInventory != null)
            {
                foreach (Item item in unit.ChestRigInventory)
                    if (item?.Data?.Name != null)
                        yield return item.Data.Name;
            }

            if (unit?.BackpackInventory != null)
            {
                foreach (GridItem gridItem in unit.BackpackInventory.GetAllItems())
                    if (gridItem?.Data?.Name != null)
                        yield return gridItem.Data.Name;
            }
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
            ReturnToMainMenu();

            // ✅ NOUVEAU : Notifier le manager
            mainMenuManager.SetHasSavedGame(true);
        }

        private void UpdatePlaying(GameTime gameTime, MouseState mouse, KeyboardState keyboard,
            bool leftClick, bool escapePressed)
        {
            if (showInventory)
            {
                inventorySystem.SetNearbyLootAccess(IsSelectedUnitStandingOnLoot());
                inventorySystem.Update(mouse, previousMouseState, leftClick, keyboard, selectedUnit);
                if (inventorySystem.TryConsumeFlashlightThrowRequest(out bool isRightHand))
                {
                    ActivateFlashlightThrowMode(isRightHand);
                    showInventory = false;
                }
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
            if (combatSystem.CurrentTurn == TurnState.PlayerTurn)
            {
                ProcessGrappleConcentrationAtTurnStart();
                HandlePlayerTurn(mouse, leftClick, keyboard, gameTime);
            }
            else if (combatSystem.CurrentTurn == TurnState.EnemyTurn)
            {
                UpdateOverwatchDuringEnemyTurn(gameTime);
                if (!combatSystem.IsActionInProgress)
                {
                    combatSystem.UpdateEnemyTurn(cellSize);
                }
            }

            if (combatSystem.CurrentTurn != lastTurnState)
            {
                visibilityDirty = true;
                lastTurnState = combatSystem.CurrentTurn;
            }

            if (visibilityDirty)
            {
                UpdateEnemyPerceptionVisibility();
                visibilityDirty = false;
                exploredCachesDirty = true;
            }

            combatSystem.UpdateFiringAnimations(gameTime);
            UpdateAimCameraAndPose();
            Point? rotationPivotCell = selectedUnit?.Cell;
            if (!rotationPivotCell.HasValue && isHoveringValidCell)
                rotationPivotCell = hoveredCell;
            camera.HandleControls(keyboard, mouse, previousMouseState, gameTime, allowZoom: !statsPanel.IsVisible, rotationPivotCell: rotationPivotCell);
            UpdateDayNightCycle(gameTime);
            HandleFloorViewControls(keyboard, gameTime);

            if (escapePressed) ReturnToMainMenuWithSave();
        }

        private bool IsSelectedUnitStandingOnLoot()
        {
            if (selectedUnit == null)
                return false;

            for (int i = 0; i < flashlightLootMarkers.Count; i++)
            {
                FlashlightLootMarker marker = flashlightLootMarkers[i];
                if (marker.Quantity <= 0)
                    continue;

                if (marker.Floor == selectedUnit.Floor && marker.Cell == selectedUnit.Cell)
                    return true;
            }

            return false;
        }


        private void UpdateOverwatchDuringEnemyTurn(GameTime gameTime)
        {
            float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

            foreach (var shooter in playerUnits)
            {
                if (shooter == null || !shooter.IsOnOverwatch)
                    continue;

                shooter.OverwatchCooldownRemainingSeconds = Math.Max(0f, shooter.OverwatchCooldownRemainingSeconds - deltaSeconds);
            }

            if (combatSystem.IsActionInProgress)
                return;

            foreach (var shooter in playerUnits)
            {
                if (shooter == null || !shooter.IsOnOverwatch || shooter.Health <= 0)
                    continue;

                if (shooter.IsFiring || shooter.IsMoving || shooter.OverwatchCooldownRemainingSeconds > 0f)
                    continue;

                if (TryTriggerOverwatchShot(shooter))
                    return;
            }
        }

        private bool TryTriggerOverwatchShot(Unit shooter)
        {
            if (shooter == null || shooter.OverwatchShotsRemaining <= 0 || shooter.WeaponData == null)
            {
                shooter?.ClearOverwatch();
                return false;
            }

            if (shooter.WeaponData.UsesAmmo)
            {
                shooter.EnsureAmmoState();
                int possibleShots = GetActionShotsFromAmmo(shooter.CurrentAmmoInMagazine, shooter.WeaponData.GetRoundsConsumedPerActionPoint());
                shooter.OverwatchShotsRemaining = Math.Min(shooter.OverwatchShotsRemaining, possibleShots);

                if (shooter.OverwatchShotsRemaining <= 0)
                {
                    Console.WriteLine($"[OVERWATCH] {shooter.Name} annule l'overwatch: munitions insuffisantes.");
                    shooter.ClearOverwatch();
                    return false;
                }
            }

            Unit target = SelectOverwatchTarget(shooter);
            if (target == null)
                return false;

            combatSystem.InitiateFire(shooter, target);

            if (!shooter.IsFiring)
                return false;

            shooter.OverwatchShotsRemaining--;
            shooter.OverwatchCooldownRemainingSeconds = OverwatchShotIntervalSeconds;
            shooter.LastOverwatchTarget = target;

            if (shooter.OverwatchShotsRemaining <= 0)
            {
                shooter.ClearOverwatch();
            }

            return true;
        }

        private Unit SelectOverwatchTarget(Unit shooter)
        {
            if (shooter == null)
                return null;

            var validTargets = FilterTargetsByPerception(shooter, combatSystem.GetValidFireTargets(shooter))
                .Where(u => u != null && u.Team == Team.Enemy && u.Health > 0)
                .ToList();

            if (validTargets.Count == 0)
                return null;

            if (shooter.LastOverwatchTarget != null &&
                shooter.LastOverwatchTarget.Health > 0 &&
                validTargets.Contains(shooter.LastOverwatchTarget))
            {
                return shooter.LastOverwatchTarget;
            }

            return validTargets.FirstOrDefault(u => u != shooter.LastOverwatchTarget) ?? validTargets[0];
        }

        private void ActivateOverwatch(Unit unit)
        {
            if (unit == null || unit.Team != Team.Player || unit.ActionPoints <= 0)
                return;

            bool hasFirearmEquipped = unit.EquippedWeapon?.Data?.WeaponData != null
                && unit.EquippedWeapon.Data.WeaponData.Type != WeaponType.Melee;

            if (!hasFirearmEquipped || unit.WeaponData == null)
            {
                Console.WriteLine("[OVERWATCH] Aucune arme à feu équipée.");
                return;
            }

            int apSpent = Math.Min(2, unit.ActionPoints);
            int requestedShots = apSpent >= 2 ? 2 : 1;
            int availableShots = requestedShots;

            if (unit.WeaponData.UsesAmmo)
            {
                unit.EnsureAmmoState();
                int shotsFromAmmo = GetActionShotsFromAmmo(unit.CurrentAmmoInMagazine, unit.WeaponData.GetRoundsConsumedPerActionPoint());
                availableShots = Math.Min(availableShots, shotsFromAmmo);
            }

            if (availableShots <= 0)
            {
                Console.WriteLine($"[OVERWATCH] {unit.Name} ne peut pas activer l'overwatch: pas assez de balles.");
                return;
            }

            unit.ActivateOverwatch(apSpent, availableShots);

            combatUI.SelectedFireTarget = null;
            combatUI.ShowFireTargets = false;
            currentPath.Clear();
            currentPathNodes.Clear();
            pathCosts.Clear();

            Console.WriteLine($"[OVERWATCH] {unit.Name} entre en overwatch ({availableShots} tir(s), coût {apSpent} AP).");
        }

        private static int GetActionShotsFromAmmo(int currentAmmo, int roundsPerShot)
        {
            int safeAmmo = Math.Max(0, currentAmmo);
            int safeRoundsPerShot = Math.Max(1, roundsPerShot);

            // Arrondi au plus proche pour traduire les munitions restantes en "actions de tir".
            // Exemple: 30/38 ≈ 0.79 -> 1 tir possible, ce qui évite de bloquer l'overwatch sur les SMG.
            return (safeAmmo + (safeRoundsPerShot / 2)) / safeRoundsPerShot;
        }


        private void UpdateAimCameraAndPose()
        {
            CleanupFiringShoulderCameraDecisions();

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
            bool aimingFromFiringSequence = false;

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
                    aimingFromFiringSequence = true;
                }
            }

            if (aimingUnit != null && targetUnit != null)
            {
                if (aimingFromFiringSequence && !ShouldUseFiringShoulderCamera(aimingUnit))
                {
                    camera.ClearShoulderCamera();
                    return;
                }

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

        private bool ShouldUseFiringShoulderCamera(Unit shooter)
        {
            if (shooter == null)
            {
                return false;
            }

            if (!firingShoulderCameraDecisions.TryGetValue(shooter, out bool useShoulderCamera))
            {
                float probability = optionsMenuManager?.GetShooterCameraProbability() ?? 0.5f;
                useShoulderCamera = random.NextDouble() < probability;
                firingShoulderCameraDecisions[shooter] = useShoulderCamera;
            }

            return useShoulderCamera;
        }

        private void CleanupFiringShoulderCameraDecisions()
        {
            if (firingShoulderCameraDecisions.Count == 0)
            {
                return;
            }

            List<Unit> staleShooters = new List<Unit>();
            foreach (var kvp in firingShoulderCameraDecisions)
            {
                if (kvp.Key == null || !kvp.Key.IsFiring)
                {
                    staleShooters.Add(kvp.Key);
                }
            }

            foreach (var shooter in staleShooters)
            {
                firingShoulderCameraDecisions.Remove(shooter);
            }
        }

        private void HandleFloorViewControls(KeyboardState keyboard, GameTime gameTime)
        {
            int minFloor = GetMinimumViewFloor();
            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);

            bool pageUpPressed = keyboard.IsKeyDown(Keys.PageUp) && previousKeyboardState.IsKeyUp(Keys.PageUp);
            bool pageDownPressed = keyboard.IsKeyDown(Keys.PageDown) && previousKeyboardState.IsKeyUp(Keys.PageDown);

            if (pageUpPressed)
                SetManualViewedFloor(viewedFloor + 1, minFloor, maxFloor, gameTime);
            if (pageDownPressed)
                SetManualViewedFloor(viewedFloor - 1, minFloor, maxFloor, gameTime);

            if (floorViewMode == FloorViewMode.AbilityLocked)
            {
                if (grappleMode && grappleTargetFloor >= minFloor)
                    viewedFloor = grappleTargetFloor;

                viewedFloor = Math.Clamp(viewedFloor, minFloor, maxFloor);
                return;
            }

            if (selectedUnit != null && floorViewMode == FloorViewMode.AutoFollow)
                viewedFloor = selectedUnit.Floor;

            viewedFloor = Math.Clamp(viewedFloor, minFloor, maxFloor);
        }

        private void SetManualViewedFloor(int targetFloor, int minFloor, int maxFloor, GameTime gameTime)
        {
            floorViewMode = FloorViewMode.Manual;
            viewedFloor = Math.Clamp(targetFloor, minFloor, maxFloor);
        }

        private int ResolveInteractionFloor(int baseFloor)
        {
            if (!explicitUpperFloorTargeting)
                return baseFloor;

            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);
            return Math.Clamp(baseFloor + 1, GetMinimumViewFloor(), maxFloor);
        }

        private int ResolveHoveredCellPreferredFloor(Point hoveredCell, int defaultInteractionFloor)
        {
            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return defaultInteractionFloor;

            // En dehors d'un bâtiment, le survol reste toujours au rez-de-chaussée.
            if (!IsInsideBuildingFootprint(hoveredCell))
                return 0;

            return defaultInteractionFloor;
        }

        private bool TryResolveAvailableClickedFloor(Point cell, int preferredFloor, out int resolvedFloor)
        {
            resolvedFloor = preferredFloor;
            if (IsCellAvailableOnFloor(cell, preferredFloor))
                return true;

            int minFloor = GetMinimumViewFloor();
            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);
            int upFloor = Math.Clamp(preferredFloor + 1, minFloor, maxFloor);
            int downFloor = Math.Clamp(preferredFloor - 1, minFloor, maxFloor);

            if (upFloor != preferredFloor && IsCellAvailableOnFloor(cell, upFloor))
            {
                resolvedFloor = upFloor;
                return true;
            }

            if (downFloor != preferredFloor && IsCellAvailableOnFloor(cell, downFloor))
            {
                resolvedFloor = downFloor;
                return true;
            }

            return false;
        }

        private void GetFloorControlButtonRects(out Rectangle downButton, out Rectangle upButton, out Rectangle modeButton)
        {
            int x = 10;
            downButton = new Rectangle(x, FloorUiButtonY, FloorUiButtonSize, FloorUiButtonSize);
            upButton = new Rectangle(x + FloorUiButtonSize + 4, FloorUiButtonY, FloorUiButtonSize, FloorUiButtonSize);
            modeButton = new Rectangle(x + (FloorUiButtonSize + 4) * 2, FloorUiButtonY, 90, FloorUiButtonSize);
        }

        private bool HandleFloorControlButtonClicks(MouseState mouse, GameTime gameTime)
        {
            GetFloorControlButtonRects(out Rectangle downButton, out Rectangle upButton, out Rectangle modeButton);
            if (!downButton.Contains(mouse.Position) && !upButton.Contains(mouse.Position) && !modeButton.Contains(mouse.Position))
                return false;

            int minFloor = GetMinimumViewFloor();
            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);

            if (downButton.Contains(mouse.Position))
            {
                SetManualViewedFloor(viewedFloor - 1, minFloor, maxFloor, gameTime);
            }
            else if (upButton.Contains(mouse.Position))
            {
                SetManualViewedFloor(viewedFloor + 1, minFloor, maxFloor, gameTime);
            }
            else if (modeButton.Contains(mouse.Position))
            {
                floorViewMode = floorViewMode == FloorViewMode.AutoFollow ? FloorViewMode.Manual : FloorViewMode.AutoFollow;
            }

            return true;
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
                combatUI.ShowFireTargets = false;
                combatUI.SelectedFireTarget = null;
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

            const float gravity = -22f;
            const float linearDrag = 3.6f;
            const float angularDamping = 4.8f;
            const float groundFriction = 8.5f;
            const float bounceFactor = 0.16f;
            float groundY = cellSize * 0.11f;

            foreach (var remains in deadUnitRemains)
            {
                if (remains == null)
                    continue;

                remains.Velocity += new Vector3(0f, gravity * cellSize * dt, 0f);
                remains.Velocity -= remains.Velocity * Math.Min(0.9f, linearDrag * dt);

                remains.Position += remains.Velocity * dt;

                remains.Pitch += remains.AngularPitchVelocity * dt;
                remains.Roll += remains.AngularRollVelocity * dt;

                float angularDamp = Math.Max(0f, 1f - angularDamping * dt);
                remains.AngularPitchVelocity *= angularDamp;
                remains.AngularRollVelocity *= angularDamp;

                if (remains.Position.Y <= groundY)
                {
                    remains.Position = new Vector3(remains.Position.X, groundY, remains.Position.Z);
                    if (Math.Abs(remains.Velocity.Y) > 0.2f * cellSize)
                    {
                        remains.Velocity = new Vector3(remains.Velocity.X * 0.7f, -remains.Velocity.Y * bounceFactor, remains.Velocity.Z * 0.7f);
                    }
                    else
                    {
                        remains.Velocity = new Vector3(remains.Velocity.X, 0f, remains.Velocity.Z);
                    }

                    remains.IsGrounded = true;

                    float friction = Math.Max(0f, 1f - groundFriction * dt);
                    remains.Velocity = new Vector3(remains.Velocity.X * friction, remains.Velocity.Y, remains.Velocity.Z * friction);
                }

                if (remains.IsGrounded && remains.Velocity.LengthSquared() < 0.01f * cellSize * cellSize &&
                    Math.Abs(remains.AngularPitchVelocity) < 0.08f && Math.Abs(remains.AngularRollVelocity) < 0.08f)
                {
                    remains.SettledTimer = Math.Min(1f, remains.SettledTimer + dt * 2.2f);
                    remains.Pitch = MathHelper.Lerp(remains.Pitch, MathHelper.Clamp(remains.Pitch, -MathHelper.PiOver2 * 0.95f, MathHelper.PiOver2 * 0.95f), remains.SettledTimer);
                    remains.Roll = MathHelper.Lerp(remains.Roll, MathHelper.Clamp(remains.Roll, -MathHelper.PiOver2 * 0.95f, MathHelper.PiOver2 * 0.95f), remains.SettledTimer);
                }
            }
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
            if (combatSystem.CurrentTurn == TurnState.EnemyTurn)
            {
                DrawEnemyTurnScreenBorder();
            }

            MouseState mouse = Mouse.GetState();

            combatUI.DrawEndTurnButton(mouse);
            combatUI.DrawUnitInfoPanel(selectedUnit, grenadeDatabase);
            combatUI.DrawActionButtons(selectedUnit, mouse, HasDetonatableSatchelCharges(selectedUnit));

            if (combatUI.ShowFireTargets && selectedUnit?.Team == Team.Player) combatUI.DrawFireTargets(mouse);

            if (selectedUnit != null && selectedUnit.Team == Team.Player && selectedUnit.Floor == viewedFloor)
            {
                DrawMovementDestinationInfoBillboard();
            }

            DrawMk2FragmentationHitChanceLabels();

            if (grappleMode)
            {
                _spriteBatch.DrawString(font, "Mode grappin: ciblez une fenetre/demi-mur en hauteur", new Vector2(10, 120), new Color(110, 240, 255));
            }

            GetFloorControlButtonRects(out Rectangle floorDownButton, out Rectangle floorUpButton, out Rectangle floorModeButton);
            DrawFloorControlButton(floorDownButton, "-", Color.IndianRed);
            DrawFloorControlButton(floorUpButton, "+", Color.CadetBlue);
            string floorModeLabel = floorViewMode == FloorViewMode.AutoFollow ? "AUTO" : floorViewMode == FloorViewMode.Manual ? "MAN" : "LOCK";
            DrawFloorControlButton(floorModeButton, floorModeLabel, floorViewMode == FloorViewMode.AutoFollow ? Color.ForestGreen : Color.DarkGoldenrod);

            _spriteBatch.DrawString(font, "Q/E: Rotation | Molette: Zoom | WASD/Middle: Deplacement | PgUp/PgDn: Etage | Shift: Cible +1 etage | I: Inventaire | C: Fiche perso", new Vector2(10, 10), Color.White);
            _spriteBatch.DrawString(font, "Escaliers: montez/descendez en cliquant la case d'acces", new Vector2(10, 70), new Color(255, 190, 90));
            _spriteBatch.DrawString(font, $"Mode etage: {floorViewMode} | Ciblage: {(explicitUpperFloorTargeting ? "+1" : "Normal")}", new Vector2(10, 100), Color.LightBlue);

            string timeStr = GetTimeOfDayString(timeOfDay);
            _spriteBatch.DrawString(font, $"Heure: {timeStr} | Carte: {gridWidth}x{gridHeight}", new Vector2(10, 30), Color.Yellow);
            string floorLabel = viewedFloor == 0 ? "RDC" : viewedFloor > 0 ? $"+{viewedFloor}" : viewedFloor.ToString();
            int maxBasements = Math.Abs(GetMinimumViewFloor());
            _spriteBatch.DrawString(font, $"Etage affiche: {floorLabel} (Sous-sols: {maxBasements} | Etages: {Math.Max(1, currentMap?.FloorCount ?? 1)})", new Vector2(10, 50), Color.LightGreen);
        }

        private void DrawEnemyTurnScreenBorder()
        {
            Viewport viewport = GraphicsDevice.Viewport;
            Rectangle screen = viewport.Bounds;

            const int borderThickness = 44;
            Color borderFill = new Color(140, 15, 20, 150);
            Color borderLine = new Color(255, 90, 90, 220);
            string enemyTurnText = "TOUR ENNEMI";
            float scrollSpeed = 120f;
            float scrollOffset = (uiAnimationTimeSeconds * scrollSpeed) % (font.MeasureString(enemyTurnText).X + 30f);
            float textPulse = 0.68f + 0.32f * (float)Math.Sin(uiAnimationTimeSeconds * 6f);
            Color textColor = Color.White * textPulse;

            Rectangle top = new Rectangle(screen.X, screen.Y, screen.Width, borderThickness);
            Rectangle bottom = new Rectangle(screen.X, screen.Bottom - borderThickness, screen.Width, borderThickness);
            Rectangle left = new Rectangle(screen.X, screen.Y, borderThickness, screen.Height);
            Rectangle right = new Rectangle(screen.Right - borderThickness, screen.Y, borderThickness, screen.Height);

            _spriteBatch.Draw(pixel, top, borderFill);
            _spriteBatch.Draw(pixel, bottom, borderFill);
            _spriteBatch.Draw(pixel, left, borderFill);
            _spriteBatch.Draw(pixel, right, borderFill);

            _spriteBatch.Draw(pixel, new Rectangle(screen.X, screen.Y, screen.Width, 2), borderLine);
            _spriteBatch.Draw(pixel, new Rectangle(screen.X, screen.Bottom - 2, screen.Width, 2), borderLine);
            _spriteBatch.Draw(pixel, new Rectangle(screen.X, screen.Y, 2, screen.Height), borderLine);
            _spriteBatch.Draw(pixel, new Rectangle(screen.Right - 2, screen.Y, 2, screen.Height), borderLine);

            DrawRepeatedBorderText(enemyTurnText, top, horizontal: true, scrollOffset, textColor);
            DrawRepeatedBorderText(enemyTurnText, bottom, horizontal: true, -scrollOffset, textColor);
            DrawRepeatedBorderText(enemyTurnText, left, horizontal: false, -scrollOffset, textColor);
            DrawRepeatedBorderText(enemyTurnText, right, horizontal: false, scrollOffset, textColor);
        }

        private void DrawRepeatedBorderText(string text, Rectangle area, bool horizontal, float scrollOffset, Color textColor)
        {
            Vector2 textSize = font.MeasureString(text);

            if (horizontal)
            {
                float step = textSize.X + 30f;
                float y = area.Y + (area.Height - textSize.Y) * 0.5f;
                float startX = area.X + 8f - step + (scrollOffset % step);

                for (float x = startX; x < area.Right; x += step)
                {
                    _spriteBatch.DrawString(font, text, new Vector2(x, y), textColor);
                }

                return;
            }

            float rotatedHeight = textSize.X;
            float stepVertical = rotatedHeight + 30f;
            float startY = area.Y + 8f - stepVertical + (scrollOffset % stepVertical);

            for (float y = startY; y < area.Bottom; y += stepVertical)
            {
                Vector2 position = new Vector2(area.X + area.Width * 0.5f, y);
                _spriteBatch.DrawString(
                    font,
                    text,
                    position,
                    textColor,
                    -MathHelper.PiOver2,
                    new Vector2(textSize.X * 0.5f, 0f),
                    1f,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void DrawFloorControlButton(Rectangle rect, string label, Color accent)
        {
            _spriteBatch.Draw(pixel, rect, accent * 0.45f);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), accent);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), accent);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), accent);
            _spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), accent);

            Vector2 textSize = font.MeasureString(label);
            Vector2 textPos = new Vector2(rect.X + (rect.Width - textSize.X) * 0.5f, rect.Y + (rect.Height - textSize.Y) * 0.5f);
            _spriteBatch.DrawString(font, label, textPos, Color.White);
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
                WorldMetrics.FloorToWorldY(currentPathEndFloor, cellSize),
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

        private bool TryGetHoverOcclusionFocus(out Point focusCell, out int focusFloor)
        {
            focusCell = hoveredCell;
            focusFloor = viewedFloor;

            if (focusCell.X < 0 || focusCell.Y < 0)
                return false;

            if (focusCell.X >= gridWidth || focusCell.Y >= gridHeight)
                return false;

            return true;
        }

        private bool IsPointInsideUpperFloorCutout(Point cell, Point centerCell, int radius)
        {
            return Math.Abs(cell.X - centerCell.X) <= radius && Math.Abs(cell.Y - centerCell.Y) <= radius;
        }

        private bool IsWallInsideUpperFloorCutout(WallSegment wall, Point centerCell, int radius)
        {
            int minX = Math.Min(wall.Start.X, wall.End.X) - radius;
            int maxX = Math.Max(wall.Start.X, wall.End.X) + radius;
            int minY = Math.Min(wall.Start.Y, wall.End.Y) - radius;
            int maxY = Math.Max(wall.Start.Y, wall.End.Y) + radius;

            return centerCell.X >= minX && centerCell.X <= maxX && centerCell.Y >= minY && centerCell.Y <= maxY;
        }

        private int CountUpperFloorOccludersNearCell(Point focusCell, int focusFloor)
        {
            int floorCount = Math.Max(1, currentMap?.FloorCount ?? 1);
            int totalOccluders = 0;

            for (int floor = Math.Max(focusFloor + 1, 0); floor < floorCount; floor++)
            {
                totalOccluders += GetCellsForFloor(floor).Count(c => IsPointInsideUpperFloorCutout(c, focusCell, UpperFloorCutoutRadius));
                totalOccluders += GetFurnitureForFloor(floor).Count(f => IsPointInsideUpperFloorCutout(new Point(f.X, f.Y), focusCell, UpperFloorCutoutRadius));
                totalOccluders += GetWallsForFloor(floor).Count(w => IsWallInsideUpperFloorCutout(w, focusCell, UpperFloorCutoutRadius));

                if (totalOccluders >= AntiOcclusionOccluderThreshold)
                    return totalOccluders;
            }

            return totalOccluders;
        }

        private void UpdateDiscreetAntiOcclusionCamera()
        {
            float targetHeight = 0f;
            float targetOrbit = 0f;

            if (!AntiOcclusionCameraEnabled)
            {
                antiOcclusionCameraHeight = 0f;
                antiOcclusionCameraOrbit = 0f;
                camera.SetAntiOcclusionOffsets(0f, 0f);
                return;
            }

            if (TryGetHoverOcclusionFocus(out Point focusCell, out int focusFloor))
            {
                int occluderCount = CountUpperFloorOccludersNearCell(focusCell, focusFloor);
                if (occluderCount >= AntiOcclusionOccluderThreshold)
                {
                    float intensity = MathHelper.Clamp((occluderCount - AntiOcclusionOccluderThreshold + 1) / 8f, 0f, 1f);
                    targetHeight = cellSize * AntiOcclusionCameraMaxHeightCells * intensity;

                    float orbitMax = MathHelper.ToRadians(AntiOcclusionCameraMaxOrbitDegrees);
                    float side = focusCell.X >= gridWidth / 2 ? 1f : -1f;
                    targetOrbit = orbitMax * intensity * side;
                }
            }

            antiOcclusionCameraHeight = MathHelper.Lerp(antiOcclusionCameraHeight, targetHeight, 0.12f);
            antiOcclusionCameraOrbit = MathHelper.Lerp(antiOcclusionCameraOrbit, targetOrbit, 0.12f);
            camera.SetAntiOcclusionOffsets(antiOcclusionCameraHeight, antiOcclusionCameraOrbit);
        }

        private void UpdateExploredCaches()
        {
            if (!exploredCachesDirty) return;

            exploredHescoCache.Clear();
            exploredFurnitureCache.Clear();
            exploredWallsCache.Clear();

            int floorCount = Math.Max(1, currentMap?.FloorCount ?? 1);
            int minF = GetMinimumViewFloor();

            for (int floor = minF; floor < floorCount; floor++)
            {
                var hescos = GetHescoBarriersForFloor(floor)
                    .Where(b => IsCellExplored(new Point(b.X, b.Y), floor))
                    .ToList();
                if (hescos.Count > 0) exploredHescoCache[floor] = hescos;

                var furns = GetFurnitureForFloor(floor)
                    .Where(f => IsCellExplored(new Point(f.X, f.Y), floor))
                    .ToList();
                if (furns.Count > 0) exploredFurnitureCache[floor] = furns;

                var walls = GetWallsForFloor(floor);
                var exploredWallsList = new List<WallSegment>();
                foreach (var wall in walls)
                {
                    bool wallExplored = false;
                    var adj = GetCellsAdjacentToWall(wall).ToList();
                    foreach (var cell in adj)
                    {
                        if (IsCellExplored(cell, floor)) { wallExplored = true; break; }
                    }

                    if (!wallExplored && floor > 0 && IsWallExterior(wall))
                    {
                        foreach (var cell in adj)
                        {
                            if (!IsInsideBuildingFootprint(cell) && IsCellExplored(cell, 0))
                            {
                                wallExplored = true;
                                break;
                            }
                        }
                    }

                    if (wallExplored) exploredWallsList.Add(wall);
                }
                if (exploredWallsList.Count > 0) exploredWallsCache[floor] = exploredWallsList;
            }

            exploredCachesDirty = false;
        }

        private void DrawWorld3D(GameTime gameTime)
        {
            UpdateExploredCaches();
            UpdateDiscreetAntiOcclusionCamera();
            camera.UpdateCamera();
            renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);
            renderer3D.SetLighting(ambientLight, directionalLight);

            GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            int floorCount = Math.Max(1, currentMap?.FloorCount ?? 1);
            int minFloor = GetMinimumViewFloor();

            bool useUpperFloorCutout = TryGetHoverOcclusionFocus(out Point focusCellForCutout, out int focusFloorForCutout);

            var allOccludedUnits = new HashSet<Unit>();

            for (int floor = minFloor; floor < floorCount; floor++)
            {
                float yOffset = WorldMetrics.FloorToWorldY(floor, cellSize);
                bool applyUpperFloorCutout = useUpperFloorCutout && floor > focusFloorForCutout;
                float upperFloorOpacity = floor > viewedFloor ? UpperFloorWallOpacityWhenLookingBelow : 1f;

                if (floor == 0)
                {
                    // En vue sous-sol, ne dessine pas la dalle du RDC au-dessus de la caméra
                    // pour éviter de masquer visuellement les niveaux négatifs.
                    if (viewedFloor >= 0)
                    {
                        renderer3D.DrawGridWithTerrain(gridWidth, gridHeight, cellSize, tileTexture, terrainHeights, yOffset);

                        if (sidewalkCells.Count > 0)
                            renderer3D.DrawTerrainCells(sidewalkCells, cellSize, sidewalkTexture, terrainHeights, yOffset + 0.005f);

                        if (roadCells.Count > 0)
                            renderer3D.DrawTerrainCells(roadCells, cellSize, asphaltTexture, terrainHeights, yOffset + 0.01f);
                    }
                }
                else
                {
                    var floorCells = GetCellsForFloor(floor);
                    if (applyUpperFloorCutout && floorCells.Count > 0)
                        floorCells = floorCells.Where(c => !IsPointInsideUpperFloorCutout(c, focusCellForCutout, UpperFloorCutoutRadius)).ToHashSet();

                    if (floorCells.Count > 0)
                        renderer3D.DrawGridCells(floorCells, cellSize, tileTexture, yOffset, upperFloorOpacity);
                }

                if (exploredHescoCache.TryGetValue(floor, out var hescoBarriersForFloor))
                {
                    if (applyUpperFloorCutout)
                        hescoBarriersForFloor = hescoBarriersForFloor.Where(b => !IsPointInsideUpperFloorCutout(new Point(b.X, b.Y), focusCellForCutout, UpperFloorCutoutRadius)).ToList();

                    if (hescoBarriersForFloor.Count > 0)
                        renderer3D.DrawHescoBarriers(hescoBarriersForFloor, cellSize, yOffset, hescoWallTexture, upperFloorOpacity);
                }

                if (exploredFurnitureCache.TryGetValue(floor, out var furnituresForFloor))
                {
                    if (applyUpperFloorCutout)
                        furnituresForFloor = furnituresForFloor.Where(f => !IsPointInsideUpperFloorCutout(new Point(f.X, f.Y), focusCellForCutout, UpperFloorCutoutRadius)).ToList();

                    if (furnituresForFloor.Count > 0)
                        renderer3D.DrawFurniture(furnituresForFloor, cellSize, yOffset, upperFloorOpacity);
                }

                if (exploredWallsCache.TryGetValue(floor, out var exploredWallsList))
                {
                    HashSet<WallSegment> renderedWalls = new HashSet<WallSegment>(exploredWallsList);

                    if (floor > viewedFloor)
                        renderedWalls = FilterUpperFloorWallsForLowerView(floor, viewedFloor, renderedWalls);

                    if (applyUpperFloorCutout && renderedWalls.Count > 0)
                        renderedWalls.RemoveWhere(w => IsWallInsideUpperFloorCutout(w, focusCellForCutout, UpperFloorCutoutRadius));

                    if (renderedWalls.Count > 0)
                    {
                        HashSet<WallSegment> fadedWalls = new HashSet<WallSegment>();

                        List<Unit> unitsOnFloor = playerUnits.Where(u => u.Health > 0 && u.Floor == floor)
                            .Concat(enemyUnits.Where(u => u.Health > 0 && u.Floor == floor && IsEnemyVisibleToPlayers(u)))
                            .ToList();

                        ComputeOcclusionFromWalls(renderedWalls, unitsOnFloor, yOffset, fadedWalls, allOccludedUnits);

                        if (floor == viewedFloor)
                        {
                            ComputeOcclusionFromHoveredArea(renderedWalls, yOffset, fadedWalls);
                            ComputeOcclusionFromPathArea(renderedWalls, yOffset, fadedWalls);
                        }

                        if (fadedWalls.Count > 0)
                            renderedWalls.ExceptWith(fadedWalls);

                        Texture2D wallTextureForFloor = floor > 0
                            ? upperWallTexture ?? brickWallTexture
                            : brickWallTexture;

                        renderer3D.DrawWalls(renderedWalls, cellSize, editorMode: false, floorHeightOffset: yOffset, brickWallTexture: wallTextureForFloor, hescoWallTexture: hescoWallTexture, wallOpacity: upperFloorOpacity);

                        if (fadedWalls.Count > 0)
                            DrawWireframeWalls(fadedWalls, yOffset, new Color(245, 225, 140, 170));
                    }
                }

                renderer3D.DrawRampTiles(currentMap?.RampTiles, floor, cellSize, upperFloorOpacity);

                // Dessiner le brouillard de guerre pour cet étage
                if (currentState == GameState.Playing && floor <= viewedFloor)
                {
                    renderer3D.DrawFogMesh(gridWidth, gridHeight, cellSize, yOffset,
                        visibleCells.TryGetValue(floor, out var v) ? v : null,
                        exploredCells.TryGetValue(floor, out var e) ? e : null,
                        floor == 0 ? terrainHeights : null,
                        GetSlabMaskForFloor(floor));
                }

                // Draw pulsing arrow markers above ramp entry cells so the player can
                // identify staircases and knows to click them to change floor.
                {
                    BlendState previousBlend = GraphicsDevice.BlendState;
                    DepthStencilState previousDepth = GraphicsDevice.DepthStencilState;
                    GraphicsDevice.BlendState = BlendState.AlphaBlend;
                    GraphicsDevice.DepthStencilState = DepthStencilState.None;
                    renderer3D.DrawRampMarkers(currentMap?.RampTiles, floor, cellSize,
                        (float)gameTime.TotalGameTime.TotalSeconds, upperFloorOpacity);
                    GraphicsDevice.BlendState = previousBlend;
                    GraphicsDevice.DepthStencilState = previousDepth;
                }
            }

            var visibleUnits = playerUnits.Where(u => u.Health > 0)
                .Concat(enemyUnits.Where(u => u.Health > 0 && IsEnemyVisibleToPlayers(u)))
                .ToList();

            DrawDeadUnitRemains();

            if (AlwaysDrawUnitGhostOutline)
                DrawVisibleUnitGhostOutlines(visibleUnits);

            if (currentMap?.Objectives != null)
                renderer3D.DrawObjectives(currentMap.Objectives, cellSize, viewedFloor);

            // --- OUTLINES ET TRACÉS (doivent être sous les unités actives) ---
            DrawHoveredCell3D(gameTime);

            if (selectedUnit != null)
                renderer3D.DrawSelectionIndicator(selectedUnit, cellSize, new Color(0, 255, 255, 128));

            Unit targetUI = combatUI.SelectedFireTarget ?? combatUI.HoveredFireTarget;
            if (targetUI != null && (targetUI.Team != Team.Enemy || IsEnemyVisibleToPlayers(targetUI)))
                renderer3D.DrawSelectionIndicator(targetUI, cellSize, new Color(255, 0, 0, 128), 1.2f);

            if (!throwMode && selectedUnit != null && selectedUnit.Team == Team.Player)
            {
                BlendState previousBlend = GraphicsDevice.BlendState;
                DepthStencilState previousDepth = GraphicsDevice.DepthStencilState;

                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                var zones = pathfinding.GetMovementZones(selectedUnit);
                renderer3D.DrawMovementZones(zones, cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds,
                    viewedFloor,
                    selectedUnit.Floor,
                    terrainHeights,
                    currentMap?.Buildings,
                    camera.Position,
                    null);

                GraphicsDevice.BlendState = previousBlend;
                GraphicsDevice.DepthStencilState = previousDepth;
            }

            if (!throwMode && currentPathNodes.Count > 0 && selectedUnit != null)
            {
                BlendState previousBlend = GraphicsDevice.BlendState;
                DepthStencilState previousDepth = GraphicsDevice.DepthStencilState;

                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                renderer3D.DrawMovementPath(currentPathNodes, selectedUnit, cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds,
                    terrainHeights,
                    null);

                GraphicsDevice.BlendState = previousBlend;
                GraphicsDevice.DepthStencilState = previousDepth;
            }

            DrawPerceivedUnitOutlines3D(gameTime);

            foreach (var unit in visibleUnits)
            {
                if (!allOccludedUnits.Contains(unit))
                {
                    float opacity = unit.Floor > viewedFloor ? UpperFloorWallOpacityWhenLookingBelow : 1.0f;
                    renderer3D.DrawUnit(unit, cellSize, opacity: opacity);
                }
            }

            // Dessiner les fantômes d'ennemis (dernière position connue)
            foreach (var ghost in enemyGhosts)
            {
                // On ne dessine le fantôme que si sa cellule est explorée mais non visible
                if (IsCellExplored(ghost.Cell, ghost.Floor) && !IsCellVisible(ghost.Cell, ghost.Floor))
                {
                    float opacity = ghost.Floor > viewedFloor ? UpperFloorWallOpacityWhenLookingBelow : 1.0f;
                    renderer3D.DrawUnitSilhouette(ghost, cellSize, new Color(150, 150, 150, 80), opacity: opacity);
                }
            }

            DrawActiveProjectiles3D();

            DrawAlliedTacticalFlashlightBeams(minFloor, floorCount);


            DrawSpiderWebs3D();

            renderer3D.DrawCraters(craters.Where(c => IsCellExplored(c.Cell, 0)).ToList(), cellSize);
            renderer3D.DrawGrenades(activeGrenades.Where(g => IsCellVisible(new Point((int)(g.TargetPosition.X / cellSize), (int)(g.TargetPosition.Z / cellSize)), g.TargetFloor)).ToList(), cellSize);
            DrawPlantedSatchelCharges3D(gameTime);
            DrawFlashlightLootHighlights(gameTime);

            DrawThrowMode3D(gameTime);
            DrawSatchelPlacementMode3D(gameTime);
            DrawGrappleMode3D(gameTime);

            if (showCoverIndicators)
            {
                renderer3D.DrawCoverIndicators(
                    combatSystem.GetCoverSystem(),
                    gridWidth,
                    gridHeight,
                    cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds,
                    IsCellExplored
                );
            }

            foreach (var unit in playerUnits.Where(u => u.Health > 0))
            {
                if (unit.CoverType != CoverType.None)
                {
                    renderer3D.DrawUnitCoverIcon(unit, cellSize,
                        (float)gameTime.TotalGameTime.TotalSeconds);
                }
            }

            foreach (var unit in enemyUnits.Where(u => u.Health > 0 && IsEnemyVisibleToPlayers(u)))
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

        private void DrawPerceivedUnitOutlines3D(GameTime gameTime)
        {
            float time = (float)gameTime.TotalGameTime.TotalSeconds;
            float allyPulse = 0.62f + 0.38f * (float)Math.Sin(time * 6f);
            float enemyPulse = 0.62f + 0.38f * (float)Math.Sin(time * 6f + 0.9f);

            BlendState previousBlend = GraphicsDevice.BlendState;
            DepthStencilState previousDepth = GraphicsDevice.DepthStencilState;

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;

            foreach (var ally in playerUnits.Where(u => u.Health > 0))
            {
                float height = ally.Floor * cellSize + 0.14f;
                renderer3D.DrawZoneOutline(
                    new[] { ally.Cell },
                    cellSize,
                    height,
                    new Color(60, 255, 80, 240) * allyPulse);
            }

            foreach (var enemy in enemyUnits.Where(u => u.Health > 0 && IsEnemyVisibleToPlayers(u)))
            {
                float height = enemy.Floor * cellSize + 0.14f;
                renderer3D.DrawZoneOutline(
                    new[] { enemy.Cell },
                    cellSize,
                    height,
                    new Color(255, 50, 50, 245) * enemyPulse);
            }

            GraphicsDevice.BlendState = previousBlend;
            GraphicsDevice.DepthStencilState = previousDepth;
        }

        private void DrawDeadUnitRemains()
        {
            if (deadUnitRemains.Count == 0)
                return;

            foreach (var remains in deadUnitRemains)
            {
                if (remains?.UnitSnapshot == null)
                    continue;

                Matrix rotation =
                    Matrix.CreateRotationZ(remains.Roll) *
                    Matrix.CreateRotationX(remains.Pitch) *
                    Matrix.CreateRotationY(remains.Yaw);

                float opacity = remains.UnitSnapshot.Floor > viewedFloor ? UpperFloorWallOpacityWhenLookingBelow : 1.0f;
                renderer3D.DrawUnit(
                    remains.UnitSnapshot,
                    cellSize,
                    bodyColorOverride: new Color(100, 100, 100),
                    drawEquipment: true,
                    positionOverride: remains.Position,
                    modelRotationOverride: rotation,
                    opacity: opacity);
            }
        }

        private static bool HasTacticalFlashlightEquipped(Unit unit)
        {
            bool rightOn = string.Equals(unit?.EquippedRightHandFlashlight?.Data?.Name, TacticalFlashlightItemName, StringComparison.OrdinalIgnoreCase)
                && unit.IsRightHandFlashlightOn;
            bool leftOn = string.Equals(unit?.EquippedLeftHandFlashlight?.Data?.Name, TacticalFlashlightItemName, StringComparison.OrdinalIgnoreCase)
                && unit.IsLeftHandFlashlightOn;
            return rightOn || leftOn;
        }

        /// <summary>
        /// Dessine les toiles d'araignée sur les cases occupées.
        /// Chaque toile est représentée par des fils entrecroisés (cubes fins) posés au sol.
        /// </summary>
        private void DrawSpiderWebs3D()
        {
            if (spiderWebTiles.Count == 0)
                return;

            Color webColor = new Color(220, 220, 210, 140);
            float yOffset = WorldMetrics.FloorToWorldY(viewedFloor, cellSize);
            float webY = yOffset + cellSize * 0.03f;
            float threadW = cellSize * 0.04f;
            float threadH = cellSize * 0.02f;

            foreach (var (cell, floor) in spiderWebTiles)
            {
                if (floor != viewedFloor) continue;
                if (!IsCellExplored(cell, floor)) continue;

                float cx = cell.X * cellSize + cellSize * 0.5f;
                float cz = cell.Y * cellSize + cellSize * 0.5f;

                // Fils horizontaux et verticaux de la toile
                renderer3D.DrawCube(
                    new Vector3(cx, webY, cz),
                    new Vector3(cellSize * 0.9f, threadH, threadW),
                    webColor);
                renderer3D.DrawCube(
                    new Vector3(cx, webY, cz),
                    new Vector3(threadW, threadH, cellSize * 0.9f),
                    webColor);
                // Diagonales
                renderer3D.DrawCube(
                    new Vector3(cx, webY, cz),
                    new Vector3(cellSize * 0.65f, threadH, threadW * 0.7f),
                    webColor,
                    Matrix.CreateRotationY(MathHelper.ToRadians(45f)));
                renderer3D.DrawCube(
                    new Vector3(cx, webY, cz),
                    new Vector3(cellSize * 0.65f, threadH, threadW * 0.7f),
                    webColor,
                    Matrix.CreateRotationY(MathHelper.ToRadians(-45f)));
            }
        }

        private void DrawAlliedTacticalFlashlightBeams(int fromFloor, int floorCount)
        {
            int minFloor = Math.Max(GetMinimumViewFloor(), fromFloor);
            int maxFloor = Math.Max(minFloor, floorCount - 1);

            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            for (int floor = minFloor; floor <= maxFloor; floor++)
            {
                var alliedUnitsOnFloor = playerUnits
                    .Where(u => u.Health > 0 && u.Floor == floor)
                    .ToList();

                if (alliedUnitsOnFloor.Count == 0)
                    continue;

                var wallsOnFloor = GetWallsForFloor(floor);

                foreach (var ally in alliedUnitsOnFloor)
                {
                    if (!HasTacticalFlashlightEquipped(ally))
                        continue;

                    DrawTacticalFlashlightBeam(ally, floor);
                    if (wallsOnFloor.Count > 0)
                        DrawTacticalFlashlightWallHighlights(ally, floor, wallsOnFloor);
                }
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
                    if (!pathfinding.HasLineOfSight(ally.Cell, ally.Floor, cell, floorToRender))
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

        private void DrawActiveProjectiles3D()
        {
            foreach (var shooter in playerUnits.Concat(enemyUnits))
            {
                if (!shooter.IsFiring || !shooter.FireTarget.HasValue)
                    continue;

                Unit targetUnit = shooter.PendingTarget;
                int targetFloor = targetUnit?.Floor ?? shooter.Floor;

                if (shooter.Floor != viewedFloor && targetFloor != viewedFloor)
                    continue;

                Vector3 muzzlePosition = shooter.VisualPosition + new Vector3(0f, cellSize * 0.72f, 0f);
                Vector3 targetPosition;

                if (targetUnit != null)
                {
                    targetPosition = targetUnit.VisualPosition + new Vector3(0f, cellSize * 0.62f, 0f);
                }
                else
                {
                    Point targetCell = shooter.FireTarget.Value;
                    targetPosition = new Vector3(
                        targetCell.X * cellSize + cellSize / 2f,
                        WorldMetrics.FloorToWorldY(shooter.Floor, cellSize) + cellSize * 0.62f,
                        targetCell.Y * cellSize + cellSize / 2f);
                }

                Vector3 shotDirection = targetPosition - muzzlePosition;
                float shotLength = shotDirection.Length();

                if (shotLength < 0.001f)
                    continue;

                shotDirection /= shotLength;

                int roundsToAnimate = Math.Max(1, shooter.FireRoundsToAnimate);
                float fireDuration = Math.Max(0.01f, shooter.FireAnimationDurationSeconds);
                float elapsedFireSeconds = MathHelper.Clamp(shooter.FireProgress, 0f, 1f) * fireDuration;
                float roundInterval = fireDuration / roundsToAnimate;
                float projectileTravelTime = MathHelper.Clamp(roundInterval * 1.4f, 0.05f, 0.22f);

                for (int roundIndex = 0; roundIndex < roundsToAnimate; roundIndex++)
                {
                    float shotStart = roundIndex * roundInterval;
                    float bulletProgress = (elapsedFireSeconds - shotStart) / projectileTravelTime;
                    if (bulletProgress < 0f || bulletProgress > 1f)
                        continue;

                    // Effet de muzzle flash au début du trajet de chaque balle
                    if (bulletProgress < 0.18f)
                    {
                        float flashScale = 1f - (bulletProgress / 0.18f);
                        DrawMuzzleFlashEffects(shooter, muzzlePosition, shotDirection, flashScale);
                    }

                    Vector3 projectilePosition = Vector3.Lerp(muzzlePosition, targetPosition, bulletProgress);
                    renderer3D.DrawCube(projectilePosition, new Vector3(cellSize * 0.09f), new Color(255, 210, 80, 235));

                    float tracerLength = Math.Min(cellSize * 1.5f, shotLength * bulletProgress);
                    if (tracerLength <= 0.02f)
                        continue;

                    Vector3 tracerCenter = projectilePosition - shotDirection * (tracerLength * 0.5f);
                    float tracerYaw = (float)Math.Atan2(shotDirection.X, shotDirection.Z);
                    float tracerPitch = (float)Math.Asin(MathHelper.Clamp(-shotDirection.Y, -1f, 1f));

                    renderer3D.DrawPlane(
                        tracerCenter,
                        new Vector3(cellSize * 0.09f, 1f, tracerLength),
                        new Color(255, 140, 40, 175),
                        tracerPitch,
                        tracerYaw,
                        0f);
                }
            }
        }

        private void DrawTacticalFlashlightWallHighlights(Unit ally, int floorToRender, HashSet<WallSegment> wallsOnFloor)
        {
            if (ally == null || wallsOnFloor == null || wallsOnFloor.Count == 0)
                return;

            const float halfConeAngleRadians = MathHelper.Pi / 9f; // 20°
            float cosHalfConeAngle = (float)Math.Cos(halfConeAngleRadians);

            Vector2 beamOrigin = new Vector2(ally.VisualPosition.X / cellSize, ally.VisualPosition.Z / cellSize);
            Vector2 forward = new Vector2((float)Math.Sin(ally.Orientation), (float)Math.Cos(ally.Orientation));
            if (forward.LengthSquared() < 0.0001f)
                return;

            forward.Normalize();

            float wallHeight = cellSize * WallHeightRatio;
            float floorHeightOffset = floorToRender * cellSize;

            foreach (var wall in wallsOnFloor)
            {
                Vector2 wallCenter = new Vector2((wall.Start.X + wall.End.X) * 0.5f, (wall.Start.Y + wall.End.Y) * 0.5f);
                Vector2 toWall = wallCenter - beamOrigin;
                float distance = toWall.Length();
                if (distance < 0.05f || distance > TacticalFlashlightRangeCells)
                    continue;

                Vector2 toWallDir = toWall / distance;
                float angleDot = Vector2.Dot(forward, toWallDir);
                if (angleDot < cosHalfConeAngle)
                    continue;

                if (!HasLineOfSightToWall(ally.Cell, wall, wallsOnFloor))
                    continue;

                float distanceFactor = 1f - (distance / TacticalFlashlightRangeCells);
                float coneFactor = (angleDot - cosHalfConeAngle) / (1f - cosHalfConeAngle);
                float intensity = MathHelper.Clamp(distanceFactor * coneFactor, 0f, 1f);
                if (intensity <= 0.04f)
                    continue;

                Color highlightColor = Color.Lerp(
                    new Color(255, 220, 140, 40),
                    new Color(255, 245, 210, 185),
                    intensity);

                float highlightAlpha = 0.35f + intensity * 0.65f;
                float surfaceInset = cellSize * 0.06f;
                float surfaceYOffset = floorHeightOffset + wallHeight * 0.52f;

                if (wall.IsHorizontal)
                {
                    float litFaceZ = toWallDir.Y >= 0f
                        ? wall.Start.Y - surfaceInset
                        : wall.Start.Y + surfaceInset;

                    renderer3D.DrawPlane(
                        new Vector3(wallCenter.X * cellSize, surfaceYOffset, litFaceZ * cellSize),
                        new Vector3(cellSize * 0.96f, 1f, wallHeight * 0.92f),
                        highlightColor * highlightAlpha,
                        rotationX: MathHelper.PiOver2,
                        rotationY: 0f,
                        rotationZ: 0f);
                }
                else
                {
                    float litFaceX = toWallDir.X >= 0f
                        ? wall.Start.X - surfaceInset
                        : wall.Start.X + surfaceInset;

                    renderer3D.DrawPlane(
                        new Vector3(litFaceX * cellSize, surfaceYOffset, wallCenter.Y * cellSize),
                        new Vector3(cellSize * 0.96f, 1f, wallHeight * 0.92f),
                        highlightColor * highlightAlpha,
                        rotationX: MathHelper.PiOver2,
                        rotationY: MathHelper.PiOver2,
                        rotationZ: 0f);
                }
            }
        }

        private void DrawMuzzleFlashEffects(Unit shooter, Vector3 muzzlePosition, Vector3 shotDirection, float flashScale)
        {
            // Point lumineux au canon
            renderer3D.DrawCube(muzzlePosition, new Vector3(cellSize * 0.22f * flashScale), new Color(255, 255, 180) * flashScale);

            // Illumination de l'environnement (murs et sol)
            float range = 3.5f;
            int floor = shooter.Floor;
            if (currentMap == null || !currentMap.WallsPerFloor.TryGetValue(floor, out var walls))
                return;

            Vector2 muzzlePos2D = new Vector2(muzzlePosition.X / cellSize, muzzlePosition.Z / cellSize);
            Color lightColor = new Color(255, 230, 150) * (flashScale * 0.35f);

            foreach (var wall in walls)
            {
                Vector2 wallCenter = new Vector2((wall.Start.X + wall.End.X) * 0.5f, (wall.Start.Y + wall.End.Y) * 0.5f);
                float distSq = Vector2.DistanceSquared(muzzlePos2D, wallCenter);
                if (distSq < range * range)
                {
                    float dist = (float)Math.Sqrt(distSq);
                    float intensity = (1f - dist / range);

                    float wallHeight = cellSize * WallHeightRatio;
                    float surfaceYOffset = floor * cellSize + wallHeight * 0.52f;
                    float surfaceInset = cellSize * 0.05f;

                    if (wall.IsHorizontal)
                    {
                        float litFaceZ = (wallCenter.Y > muzzlePos2D.Y) ? wall.Start.Y - surfaceInset : wall.Start.Y + surfaceInset;
                        renderer3D.DrawPlane(new Vector3(wallCenter.X * cellSize, surfaceYOffset, litFaceZ * cellSize),
                            new Vector3(cellSize * 0.95f, 1f, wallHeight * 0.9f), lightColor * intensity, MathHelper.PiOver2, 0, 0);
                    }
                    else
                    {
                        float litFaceX = (wallCenter.X > muzzlePos2D.X) ? wall.Start.X - surfaceInset : wall.Start.X + surfaceInset;
                        renderer3D.DrawPlane(new Vector3(litFaceX * cellSize, surfaceYOffset, wallCenter.Y * cellSize),
                            new Vector3(cellSize * 0.95f, 1f, wallHeight * 0.9f), lightColor * intensity, MathHelper.PiOver2, MathHelper.PiOver2, 0);
                    }
                }
            }

            // Sol
            int minX = (int)Math.Floor(muzzlePos2D.X - range);
            int maxX = (int)Math.Ceiling(muzzlePos2D.X + range);
            int minY = (int)Math.Floor(muzzlePos2D.Y - range);
            int maxY = (int)Math.Ceiling(muzzlePos2D.Y + range);

            for (int x = Math.Max(0, minX); x <= Math.Min(gridWidth - 1, maxX); x++)
            {
                for (int y = Math.Max(0, minY); y <= Math.Min(gridHeight - 1, maxY); y++)
                {
                    float dx = x + 0.5f - muzzlePos2D.X;
                    float dy = y + 0.5f - muzzlePos2D.Y;
                    float distSq = dx * dx + dy * dy;
                    if (distSq < range * range)
                    {
                        float intensity = (1f - (float)Math.Sqrt(distSq) / range);
                        renderer3D.DrawPlane(
                            new Vector3(x * cellSize + cellSize / 2f, floor * cellSize + 0.02f, y * cellSize + cellSize / 2f),
                            new Vector3(cellSize * 0.92f),
                            lightColor * intensity,
                            0f, 0f, 0f);
                    }
                }
            }
        }

        private bool HasLineOfSightToWall(Point originCell, WallSegment targetWall, HashSet<WallSegment> candidateWalls)
        {
            Vector2 origin = new Vector2(originCell.X + 0.5f, originCell.Y + 0.5f);
            Vector2 targetCenter = new Vector2((targetWall.Start.X + targetWall.End.X) * 0.5f, (targetWall.Start.Y + targetWall.End.Y) * 0.5f);

            foreach (var wall in candidateWalls)
            {
                if (wall.Equals(targetWall))
                    continue;

                Vector2 wallStart = new Vector2(wall.Start.X, wall.Start.Y);
                Vector2 wallEnd = new Vector2(wall.End.X, wall.End.Y);

                if (!TryGetSegmentIntersectionParam(origin, targetCenter, wallStart, wallEnd, out float rayT))
                    continue;

                if (rayT > 0.999f)
                    continue;

                return false;
            }

            return true;
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
                    Vector3 position = new Vector3(cell.X * cellSize + cellSize / 2f, WorldMetrics.FloorToWorldY(viewedFloor, cellSize) + 0.05f, cell.Y * cellSize + cellSize / 2f);
                    renderer3D.DrawPlane(position, new Vector3(cellSize * 0.9f, 1, cellSize * 0.9f), Color.Green * pulse);
                }
            }
        }

        private void DrawPath3D(GameTime gameTime)
        {
            if (currentPathNodes.Count == 0 || selectedUnit == null || selectedUnit.Team != Team.Player || !currentPathNodes.Any(n => n.Floor == viewedFloor)) return;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 4f) * 0.2f + 0.8f;

            int visibleNodeCount = currentPathNodes.Count(n => n.Floor == viewedFloor);
            int visibleIndex = 0;

            for (int i = 0; i < currentPathNodes.Count; i++)
            {
                GridNode node = currentPathNodes[i];
                if (node.Floor != viewedFloor)
                    continue;

                Point cell = node.Cell;
                Vector3 pos = new Vector3(cell.X * cellSize + cellSize / 2f, WorldMetrics.FloorToWorldY(node.Floor, cellSize) + 0.1f, cell.Y * cellSize + cellSize / 2f);
                float intensity = 1f - (visibleIndex / (float)Math.Max(1, visibleNodeCount)) * 0.5f;
                visibleIndex++;
                renderer3D.DrawPlane(pos, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), new Color(100, 150, 255) * pulse * intensity);
            }
        }

        private void DrawHoveredCell3D(GameTime gameTime)
        {
            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return;

            if (!IsCellExplored(hoveredCell, hoveredCellFloor))
                return;

            BlendState previousBlend = GraphicsDevice.BlendState;
            DepthStencilState previousDepth = GraphicsDevice.DepthStencilState;

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f) * 0.3f + 0.7f;
            float floorYOffset = WorldMetrics.FloorToWorldY(hoveredCellFloor, cellSize);
            float terrainOffset = GetTerrainHeightOffset(hoveredCell);

            float pulseBoost = 0.75f + pulse * 0.25f;

            // Contour au sol : suit la géométrie de la tuile (coins interpolés) pour rester collé au relief.
            DrawHoveredCellTerrainOutline(floorYOffset, pulseBoost);

            GraphicsDevice.BlendState = previousBlend;
            GraphicsDevice.DepthStencilState = previousDepth;
        }

        private float GetTerrainHeightOffset(Point cell)
        {
            if (terrainHeights != null && terrainHeights.TryGetValue(cell, out float height))
                return height;

            return 0f;
        }

        // Retourne la hauteur de terrain interpolée bilinéairement au point monde (wx, wz),
        // en utilisant la même moyenne de coins que le renderer (ComputeHoveredCornerHeight).
        // C'est la hauteur *visuelle* réelle de la surface à cet endroit précis.
        private float GetBilinearTerrainHeight(float wx, float wz)
        {
            float fcx = wx / cellSize;
            float fcz = wz / cellSize;
            int ix = (int)MathF.Floor(fcx);
            int iz = (int)MathF.Floor(fcz);
            float u = fcx - ix;
            float v = fcz - iz;

            float yNW = ComputeHoveredCornerHeight(ix,     iz);
            float yNE = ComputeHoveredCornerHeight(ix + 1, iz);
            float ySW = ComputeHoveredCornerHeight(ix,     iz + 1);
            float ySE = ComputeHoveredCornerHeight(ix + 1, iz + 1);

            return yNW * (1 - u) * (1 - v)
                 + yNE * u       * (1 - v)
                 + ySW * (1 - u) * v
                 + ySE * u       * v;
        }

        private void DrawHoveredCellTerrainOutline(float floorYOffset, float pulseBoost)
        {
            const float outlineLift = 0.14f;
            Color outlineColor = new Color(255, 220, 90, 230) * pulseBoost;

            float xMin = hoveredCell.X * cellSize;
            float xMax = (hoveredCell.X + 1) * cellSize;
            float zMin = hoveredCell.Y * cellSize;
            float zMax = (hoveredCell.Y + 1) * cellSize;

            // Même règle que le rendu de terrain: chaque sommet prend la moyenne des tuiles adjacentes.
            float yNW = floorYOffset + ComputeHoveredCornerHeight(hoveredCell.X, hoveredCell.Y) + outlineLift;
            float ySW = floorYOffset + ComputeHoveredCornerHeight(hoveredCell.X, hoveredCell.Y + 1) + outlineLift;
            float ySE = floorYOffset + ComputeHoveredCornerHeight(hoveredCell.X + 1, hoveredCell.Y + 1) + outlineLift;
            float yNE = floorYOffset + ComputeHoveredCornerHeight(hoveredCell.X + 1, hoveredCell.Y) + outlineLift;

            float lw = cellSize * 0.12f;

            // Chaque arête est un quad incliné entre ses deux coins exacts,
            // épousant parfaitement la pente du terrain au lieu d'un cube plat à hauteur moyenne.
            // Nord : NW → NE
            renderer3D.DrawSlopedEdge(new Vector3(xMin, yNW, zMin), new Vector3(xMax, yNE, zMin), outlineColor, lw);
            // Est  : NE → SE
            renderer3D.DrawSlopedEdge(new Vector3(xMax, yNE, zMin), new Vector3(xMax, ySE, zMax), outlineColor, lw);
            // Sud  : SE → SW
            renderer3D.DrawSlopedEdge(new Vector3(xMax, ySE, zMax), new Vector3(xMin, ySW, zMax), outlineColor, lw);
            // Ouest: SW → NW
            renderer3D.DrawSlopedEdge(new Vector3(xMin, ySW, zMax), new Vector3(xMin, yNW, zMin), outlineColor, lw);
        }

        private float ComputeHoveredCornerHeight(int vertexX, int vertexZ)
        {
            float sum = 0f;
            int count = 0;

            for (int cellX = vertexX - 1; cellX <= vertexX; cellX++)
            {
                for (int cellZ = vertexZ - 1; cellZ <= vertexZ; cellZ++)
                {
                    if (terrainHeights != null && terrainHeights.TryGetValue(new Point(cellX, cellZ), out float height))
                    {
                        sum += height;
                        count++;
                    }
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        private void DrawWireframeWalls(HashSet<WallSegment> walls, float floorHeightOffset, Color wireColor)
        {
            if (walls == null || walls.Count == 0)
                return;

            RasterizerState previousRasterizer = GraphicsDevice.RasterizerState;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = hoveredCellWireframeState;

            renderer3D.DrawWalls(walls, cellSize, editorMode: false, floorHeightOffset: floorHeightOffset, wallOverrideColor: wireColor, brickWallTexture: brickWallTexture, hescoWallTexture: hescoWallTexture);

            GraphicsDevice.RasterizerState = previousRasterizer;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        private void DrawVisibleUnitGhostOutlines(IEnumerable<Unit> units)
        {
            if (units == null)
                return;

            BlendState previousBlend = GraphicsDevice.BlendState;
            DepthStencilState previousDepth = GraphicsDevice.DepthStencilState;

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.None;

            foreach (Unit unit in units)
            {
                if (unit == null || unit.Health <= 0)
                    continue;

                if (unit.Floor != viewedFloor)
                    continue;

                Color ghostColor = unit.Team == Team.Player
                    ? new Color(90, 210, 255, 70)
                    : new Color(255, 120, 120, 65);

                renderer3D.DrawUnitSilhouette(unit, cellSize, ghostColor);
            }

            GraphicsDevice.BlendState = previousBlend;
            GraphicsDevice.DepthStencilState = previousDepth;
        }

        private void ComputeOcclusionFromHoveredArea(
            IEnumerable<WallSegment> walls,
            float floorHeightOffset,
            HashSet<WallSegment> fadedWalls)
        {
            if (hoveredCell.X < 0 || hoveredCell.Y < 0)
                return;

            Vector3 cameraPos = camera.Position;
            float hoverY = WorldMetrics.FloorToWorldY(viewedFloor, cellSize) + cellSize * 0.35f;
            List<Point> revealCells = GetHoveredAreaCells(HoverRevealRadius);

            foreach (Point cell in revealCells)
            {
                Vector3 revealPoint = new Vector3(
                    cell.X * cellSize + cellSize / 2f,
                    hoverY,
                    cell.Y * cellSize + cellSize / 2f);

                foreach (var wall in walls)
                {
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
            if (currentPathNodes == null || currentPathNodes.Count == 0 || selectedUnit == null)
                return;

            if (selectedUnit.Team != Team.Player)
                return;

            Vector3 cameraPos = camera.Position;
            float pathY = WorldMetrics.FloorToWorldY(viewedFloor, cellSize) + cellSize * 0.25f;

            foreach (GridNode node in currentPathNodes)
            {
                if (node.Floor != viewedFloor)
                    continue;

                Vector3 revealPoint = new Vector3(
                    node.Cell.X * cellSize + cellSize / 2f,
                    pathY,
                    node.Cell.Y * cellSize + cellSize / 2f);

                foreach (var wall in walls)
                {
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

        private (string Name, string Job) GenerateRandomRecruitProfile()
        {
            string[] firstNames = { "Nadia", "Alex", "Maya", "Victor", "Elena", "Jonas", "Iris", "Noah", "Leila", "Marco", "Sofia", "Ethan" };
            string[] lastNames = { "Vega", "Mercer", "Khan", "Duval", "Ortega", "Novak", "Sato", "Bauer", "Silva", "Petrov", "Rossi", "Tanaka" };
            string[] jobs = { "Assault", "Support", "Sniper", "Scout", "Heavy" };

            string name = $"{firstNames[random.Next(firstNames.Length)]} {lastNames[random.Next(lastNames.Length)]}";
            string job = jobs[random.Next(jobs.Length)];
            return (name, job);
        }

        private void AssignWeaponToUnit(Unit unit, WeaponData weaponData)
        {
            if (unit == null)
                return;

            if (weaponData == null)
            {
                unit.Weapon = string.Empty;
                unit.WeaponData = null;
                unit.EquippedWeapon = null;
                return;
            }

            WeaponData weaponInstance = weaponData.Clone();
            ApplyRandomWeaponUpgrades(weaponInstance);
            unit.Weapon = weaponInstance.Name;
            unit.WeaponData = weaponInstance;
            unit.EquippedWeapon = new Item(new ItemData(weaponInstance.Name, ItemType.Weapon, weaponInstance), Point.Zero);
        }

        private void ApplyRandomWeaponUpgrades(WeaponData weapon)
        {
            if (weapon == null || weapon.UpgradeSlots == null)
                return;

            // Une partie des armes reste "stock" pour préserver de la variété.
            if (random.NextDouble() > 0.72)
                return;

            if (weapon.UsesAmmo && random.NextDouble() < 0.45)
            {
                int extraRounds = weapon.Type switch
                {
                    WeaponType.Pistol or WeaponType.Revolver => random.Next(2, 5),
                    WeaponType.Shotgun => random.Next(1, 3),
                    WeaponType.MachineGun => random.Next(12, 31),
                    _ => random.Next(4, 13)
                };
                weapon.TryInstallUpgrade(WeaponUpgradeData.ExtendedMagazine(extraRounds));
            }

            if (random.NextDouble() < 0.40)
            {
                int opticBonus = weapon.Type == WeaponType.SniperRifle || weapon.Type == WeaponType.DMR
                    ? random.Next(8, 14)
                    : random.Next(4, 10);
                weapon.TryInstallUpgrade(WeaponUpgradeData.RedDotSight(opticBonus));
            }

            if (weapon.UsesAmmo && random.NextDouble() < 0.36)
            {
                int laserBonus = weapon.Type == WeaponType.SMG || weapon.Type == WeaponType.Pistol
                    ? random.Next(6, 11)
                    : random.Next(3, 8);
                weapon.TryInstallUpgrade(WeaponUpgradeData.LaserSight(laserBonus));
            }
        }

        private WeaponData GetRandomWeaponData(string preferredWeaponName = null, bool enforcePreferred = false)
        {
            if (weaponDatabase == null || weaponDatabase.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredWeaponName) &&
                weaponDatabase.TryGetValue(preferredWeaponName, out WeaponData preferredWeapon) &&
                (enforcePreferred || random.Next(100) < 65))
            {
                return preferredWeapon.Clone();
            }

            return weaponDatabase.Values.ElementAt(random.Next(weaponDatabase.Count)).Clone();
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
            deadUnitRemains.Clear();

            int playerUnitCount = missionType == "Sprint" ? 1 : 6;
            List<Point> playerSpawnCells = missionType == "Centre-Ville" || missionType == "Sabotage"
                ? GetCityCenterSpawnCells(playerUnitCount)
                : Enumerable.Range(0, playerUnitCount).Select(i => new Point(2 + i, gridHeight - 2)).ToList();

            for (int i = 0; i < playerSpawnCells.Count; i++)
            {
                CharacterCreationProfile profile = i < createdSquadProfiles.Count
                    ? createdSquadProfiles[i]
                    : null;

                if (profile != null)
                {
                    WeaponData weaponData = weaponDatabase.TryGetValue(profile.Weapon, out var data)
                        ? data
                        : GetRandomWeaponData();
                    string weaponName = weaponData?.Name ?? profile.Weapon;
                    Unit unit = new Unit(playerSpawnCells[i], Team.Player, profile.Name, profile.Job, string.Empty, null)
                    {
                        EyeColor = Unit.ParseEyeColor(profile.EyeColor)
                    };
                    AssignWeaponToUnit(unit, weaponData);
                    playerUnits.Add(unit);
                }
                else
                {
                    (string callSign, string job) = GenerateRandomRecruitProfile();
                    var randomWeapon = GetRandomWeaponData();
                    var playerUnit = new Unit(playerSpawnCells[i], Team.Player, callSign, job, string.Empty, null);
                    AssignWeaponToUnit(playerUnit, randomWeapon);
                    playerUnits.Add(playerUnit);
                }
            }

            for (int i = 0; i < playerUnits.Count; i++)
            {
                CharacterCreationProfile profile = i < createdSquadProfiles.Count
                    ? createdSquadProfiles[i]
                    : null;

                if (profile != null)
                {
                    ApplyStartingEquipment(playerUnits[i], profile);
                    continue;
                }

                playerUnits[i].AddGrenade(grenadeDatabase["MK 2"]);
            }

            AssignRandomPants(playerUnits);
            AssignRandomEquipmentToUnits(playerUnits);
            EquipMk2GrenadeToAlliedPockets(playerUnits);
            RemoveDuplicateMk2GrenadesFromAlliedUnits(playerUnits);
            AssignRandomInventoryToUnits(playerUnits);
            EnsureUnitsHaveCompatibleMagazines(playerUnits, minimumMagazinesPerUnit: 3);

            switch (missionType)
            {
                case "Tutorial":
                    for (int i = 0; i < 6; i++)
                    {
                        var t = enemyPool[random.Next(enemyPool.Count)];
                        var enemy = new Unit(new Point(2 + i, 1), Team.Enemy, t.Name, t.Class, string.Empty, null) { ActionPoints = t.ActionPoints };
                        AssignWeaponToUnit(enemy, GetRandomWeaponData(t.Weapon));
                        enemyUnits.Add(enemy);
                    }
                    break;

                case "Survival":
                    for (int i = 0; i < 10; i++)
                    {
                        var t = enemyPool[random.Next(enemyPool.Count)];
                        var enemy = new Unit(new Point(2 + (i % 8), i < 8 ? 1 : 2), Team.Enemy, t.Name, t.Class, string.Empty, null) { ActionPoints = t.ActionPoints };
                        AssignWeaponToUnit(enemy, GetRandomWeaponData(t.Weapon));
                        enemyUnits.Add(enemy);
                    }
                    break;

                case "Assault":
                    var aliens = enemyPool.Where(e => e.Name != "Zombie").ToList();
                    for (int i = 0; i < 8; i++)
                    {
                        var t = aliens[random.Next(aliens.Count)];
                        var enemy = new Unit(new Point(2 + i, 1), Team.Enemy, t.Name, t.Class, string.Empty, null) { ActionPoints = t.ActionPoints };
                        AssignWeaponToUnit(enemy, GetRandomWeaponData(t.Weapon));
                        enemyUnits.Add(enemy);
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
                            var enemy = new Unit(
                                spawn,
                                Team.Enemy,
                                zombie.Name,
                                zombie.Class,
                                string.Empty,
                                null)
                            { ActionPoints = zombie.ActionPoints };
                            AssignWeaponToUnit(enemy, GetRandomWeaponData(zombie.Weapon, enforcePreferred: true));
                            enemyUnits.Add(enemy);
                        }

                        break;
                    }

                case "Centre-Ville":
                    {
                        var zombie = enemyPool.First(e => e.Name == "Zombie");
                        var spider = enemyPool.First(e => e.Name == "Giant Spider");

                        // Zombies errants sur le périmètre de la carte
                        var edgeSpawns = GetPerimeterSpawnCells(2);
                        foreach (var spawn in edgeSpawns)
                        {
                            var enemy = new Unit(
                                spawn,
                                Team.Enemy,
                                zombie.Name,
                                zombie.Class,
                                string.Empty,
                                null)
                            { ActionPoints = zombie.ActionPoints };
                            AssignWeaponToUnit(enemy, GetRandomWeaponData(zombie.Weapon, enforcePreferred: true));
                            enemyUnits.Add(enemy);
                        }

                        // Araignées géantes tapies contre les murs extérieurs des bâtiments
                        var wallSpawns = GetBuildingExteriorAdjacentCells(6);
                        foreach (var spawn in wallSpawns)
                        {
                            var enemy = new Unit(
                                spawn,
                                Team.Enemy,
                                spider.Name,
                                spider.Class,
                                string.Empty,
                                null)
                            { ActionPoints = spider.ActionPoints };
                            AssignWeaponToUnit(enemy, GetRandomWeaponData(spider.Weapon, enforcePreferred: true));
                            enemyUnits.Add(enemy);
                        }

                        break;
                    }

                case "Extraction":
                    {
                        var hostiles = enemyPool.Where(e => e.Name != "Zombie").ToList();
                        var edgeSpawns = GetPerimeterSpawnCells(12);

                        foreach (var spawn in edgeSpawns)
                        {
                            var t = hostiles[random.Next(hostiles.Count)];
                            var enemy = new Unit(spawn, Team.Enemy, t.Name, t.Class, string.Empty, null)
                            { ActionPoints = t.ActionPoints };
                            AssignWeaponToUnit(enemy, GetRandomWeaponData(t.Weapon));
                            enemyUnits.Add(enemy);
                        }

                        break;
                    }

                case "Sabotage":
                    {
                        var hostiles = enemyPool.Where(e => e.Name != "Zombie").ToList();
                        var centerSpawns = GetCityCenterSpawnCells(10);

                        foreach (var spawn in centerSpawns)
                        {
                            var t = hostiles[random.Next(hostiles.Count)];
                            var enemy = new Unit(spawn, Team.Enemy, t.Name, t.Class, string.Empty, null)
                            { ActionPoints = t.ActionPoints };
                            AssignWeaponToUnit(enemy, GetRandomWeaponData(t.Weapon));
                            enemyUnits.Add(enemy);
                        }

                        break;
                    }

                case "Blackout":
                    {
                        var zombie = enemyPool.First(e => e.Name == "Zombie");
                        var edgeSpawns = GetPerimeterSpawnCells(18);

                        foreach (var spawn in edgeSpawns)
                        {
                            var enemy = new Unit(spawn, Team.Enemy, zombie.Name, zombie.Class, string.Empty, null)
                            { ActionPoints = zombie.ActionPoints };
                            AssignWeaponToUnit(enemy, GetRandomWeaponData(zombie.Weapon, enforcePreferred: true));
                            enemyUnits.Add(enemy);
                        }

                        break;
                    }

                case "The Hive":
                    {
                        var zombie = enemyPool.First(e => e.Name == "Zombie");
                        int zombieCount = 35;

                        for (int i = 0; i < zombieCount; i++)
                        {
                            // On les place n'importe où, DistributeEnemiesAcrossFloors s'en occupera
                            var enemy = new Unit(new Point(0, 0), Team.Enemy, zombie.Name, zombie.Class, string.Empty, null)
                            { ActionPoints = zombie.ActionPoints };
                            AssignWeaponToUnit(enemy, GetRandomWeaponData(zombie.Weapon, enforcePreferred: true));
                            enemyUnits.Add(enemy);
                        }

                        break;
                    }
            }

            DistributeEnemiesAcrossFloors();

            AssignRandomPants(enemyUnits);
            AssignRandomEquipmentToUnits(enemyUnits);
            AssignRandomInventoryToUnits(enemyUnits);
            EnsureUnitsHaveCompatibleMagazines(enemyUnits, minimumMagazinesPerUnit: 2);

            foreach (var unit in playerUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }
            foreach (var unit in enemyUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }

            Console.WriteLine($"Units created for {missionType}: {playerUnits.Count} player, {enemyUnits.Count} enemy");
        }

        private void ApplyStartingEquipment(Unit unit, CharacterCreationProfile profile)
        {
            if (unit == null || profile == null)
                return;

            unit.Grenades.Clear();

            foreach (string itemName in profile.StartingEquipment)
            {
                if (grenadeDatabase.TryGetValue(itemName, out GrenadeData grenade))
                    unit.AddGrenade(grenade);

                ItemData armorData = ArmorDatabase.GetArmor(itemName);
                if (armorData != null)
                    EquipArmorItemToSlot(unit, armorData);
            }

            if (unit.Grenades.Count == 0)
                unit.AddGrenade(grenadeDatabase["MK 2"]);

            if (grenadeDatabase.ContainsKey("Satchel Charge (C4)") &&
                !unit.Grenades.Any(g => g.Type == GrenadeType.SatchelC4))
            {
                unit.AddGrenade(grenadeDatabase["Satchel Charge (C4)"]);
            }
        }

        private void AssignRandomEquipmentToUnits(List<Unit> units)
        {
            if (units == null || units.Count == 0)
                return;

            var slotCandidates = new Dictionary<ArmorSlot, IReadOnlyList<ItemData>>
            {
                [ArmorSlot.Head] = ArmorDatabase.GetArmorsBySlot(ArmorSlot.Head),
                [ArmorSlot.Neck] = ArmorDatabase.GetArmorsBySlot(ArmorSlot.Neck),
                [ArmorSlot.Torso] = ArmorDatabase.GetArmorsBySlot(ArmorSlot.Torso),
                [ArmorSlot.Knees] = ArmorDatabase.GetArmorsBySlot(ArmorSlot.Knees),
                [ArmorSlot.Feet] = ArmorDatabase.GetArmorsBySlot(ArmorSlot.Feet),
                [ArmorSlot.ChestRig] = ArmorDatabase.GetArmorsBySlot(ArmorSlot.ChestRig),
                [ArmorSlot.Backpack] = ArmorDatabase.GetArmorsBySlot(ArmorSlot.Backpack)
            };

            foreach (Unit unit in units)
            {
                if (unit == null)
                    continue;

                foreach (var slotGroup in slotCandidates)
                {
                    var candidates = slotGroup.Value;
                    if (candidates == null || candidates.Count == 0)
                        continue;

                    ItemData selected = candidates[random.Next(candidates.Count)];
                    EquipArmorItemToSlot(unit, selected);
                }
            }
        }

        private static void EquipArmorItemToSlot(Unit unit, ItemData armorData)
        {
            if (unit == null || armorData == null)
                return;

            Item equippedItem = new Item(armorData, Point.Zero);

            switch (armorData.ArmorSlot)
            {
                case ArmorSlot.Head:
                    unit.EquippedHelmet = equippedItem;
                    break;
                case ArmorSlot.Neck:
                    unit.EquippedNeck = equippedItem;
                    break;
                case ArmorSlot.Torso:
                    unit.EquippedArmor = equippedItem;
                    break;
                case ArmorSlot.Pants:
                    unit.EquippedPants = equippedItem;
                    break;
                case ArmorSlot.Knees:
                    unit.EquippedKnees = equippedItem;
                    break;
                case ArmorSlot.Feet:
                    unit.EquippedFeet = equippedItem;
                    break;
                case ArmorSlot.ChestRig:
                    unit.EquippedChestRig = equippedItem;
                    break;
                case ArmorSlot.Backpack:
                    unit.EquippedBackpack = armorData.Name;
                    unit.EnsureBackpackInventoryGrid();
                    break;
            }
        }

        private void DistributeEnemiesAcrossFloors()
        {
            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);
            int minFloor = GetMinimumViewFloor();
            if (enemyUnits.Count == 0)
                return;

            var possibleAssignments = new List<(Point Cell, int Floor)>();
            var occupied = new HashSet<(int Floor, Point Cell)>();

            foreach (var player in playerUnits)
                occupied.Add((player.Floor, player.Cell));

            // Recueillir toutes les cellules disponibles à tous les étages (sauf RDC hors bâtiments pour éviter spawn à découvert)
            for (int floor = minFloor; floor <= maxFloor; floor++)
            {
                var floorCells = GetCellsForFloor(floor);
                foreach (var cell in floorCells)
                {
                    if (occupied.Contains((floor, cell))) continue;

                    // Si on est au RDC, on ne veut spawner que dans les bâtiments
                    if (floor == 0 && !IsInsideBuildingFootprint(cell)) continue;

                    possibleAssignments.Add((cell, floor));
                }
            }

            if (possibleAssignments.Count == 0)
            {
                // Fallback : utiliser le périmètre si vraiment rien d'autre (uniquement RDC)
                var fallbackSpawns = GetPerimeterSpawnCells(enemyUnits.Count);
                for (int i = 0; i < Math.Min(fallbackSpawns.Count, enemyUnits.Count); i++)
                {
                    enemyUnits[i].Cell = fallbackSpawns[i];
                    enemyUnits[i].Floor = 0;
                }
                return;
            }

            // Mélanger les assignations possibles
            for (int i = possibleAssignments.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (possibleAssignments[i], possibleAssignments[swapIndex]) = (possibleAssignments[swapIndex], possibleAssignments[i]);
            }

            for (int i = 0; i < enemyUnits.Count; i++)
            {
                var assignment = possibleAssignments[i % possibleAssignments.Count];
                enemyUnits[i].Cell = assignment.Cell;
                enemyUnits[i].Floor = assignment.Floor;
            }
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

        private void AssignRandomInventoryToUnits(List<Unit> units)
        {
            if (units == null || units.Count == 0 || inventorySystem?.ItemDatabase == null)
                return;

            List<ItemData> pocketCandidates = inventorySystem.ItemDatabase.Values
                .Where(data => data != null
                    && data.Type != ItemType.Weapon
                    && !data.Name.Contains("Backpack", StringComparison.OrdinalIgnoreCase)
                    && ItemSizeDatabase.IsPocketSized(data.Name))
                .ToList();

            List<ItemData> backpackCandidates = inventorySystem.ItemDatabase.Values
                .Where(data => data != null
                    && !data.Name.Contains("Backpack", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (pocketCandidates.Count == 0 && backpackCandidates.Count == 0)
                return;

            bool grapplingHookAssigned = false;

            foreach (var unit in units)
            {
                if (unit == null)
                    continue;

                unit.PantsInventory ??= new List<Item>();
                unit.ChestRigInventory ??= new List<Item>();

                int pantsCapacity = unit.GetPantsInventoryCapacity();
                int pantsItemsTarget = Math.Min(pantsCapacity, random.Next(1, 3));
                while (unit.PantsInventory.Count < pantsItemsTarget && pocketCandidates.Count > 0)
                {
                    ItemData randomPocketItem = pocketCandidates[random.Next(pocketCandidates.Count)];
                    unit.PantsInventory.Add(new Item(randomPocketItem, Point.Zero));
                }

                int chestRigCapacity = unit.GetChestRigInventoryCapacity();
                int chestRigItemsTarget = Math.Min(chestRigCapacity, random.Next(0, 3));
                while (unit.ChestRigInventory.Count < chestRigItemsTarget && pocketCandidates.Count > 0)
                {
                    ItemData randomPocketItem = pocketCandidates[random.Next(pocketCandidates.Count)];
                    unit.ChestRigInventory.Add(new Item(randomPocketItem, Point.Zero));
                }

                unit.EnsureBackpackInventoryGrid();
                int backpackItemsTarget = random.Next(1, 5);
                for (int i = 0; i < backpackItemsTarget && backpackCandidates.Count > 0; i++)
                {
                    ItemData randomBackpackItem = backpackCandidates[random.Next(backpackCandidates.Count)];
                    ItemSize itemSize = ItemSizeDatabase.GetItemSize(randomBackpackItem.Name);
                    Point? position = unit.BackpackInventory.FindFreePosition(itemSize, true);

                    if (!position.HasValue)
                        continue;

                    bool canPlaceDefault = unit.BackpackInventory.CanPlaceItem(position.Value, itemSize);
                    bool canPlaceRotated = itemSize.Width != itemSize.Height
                        && unit.BackpackInventory.CanPlaceItem(position.Value, itemSize.Rotated());
                    bool rotate = !canPlaceDefault && canPlaceRotated;

                    unit.BackpackInventory.PlaceItem(new GridItem(
                        randomBackpackItem,
                        position.Value,
                        itemSize,
                        rotate));
                }

                if (inventorySystem.ItemDatabase.TryGetValue(GrapplingHookItemName, out ItemData grapplingHookData))
                {
                    bool hasGrapplingHook = HasItemInUnitInventory(unit, GrapplingHookItemName);
                    if (!hasGrapplingHook)
                    {
                        bool shouldTryAssignHook = random.NextDouble() < 0.35;
                        if (!grapplingHookAssigned || shouldTryAssignHook)
                        {
                            grapplingHookAssigned = TryAddPocketItemPreferChestRig(unit, grapplingHookData)
                                || TryAddMagazineToBackpack(unit, grapplingHookData);
                        }
                    }
                    else
                    {
                        grapplingHookAssigned = true;
                    }
                }

                unit.RefreshGrenadeInventoryFromEquipment();
            }

            if (!grapplingHookAssigned && units.Count > 0
                && inventorySystem.ItemDatabase.TryGetValue(GrapplingHookItemName, out ItemData fallbackGrapplingHookData))
            {
                foreach (Unit unit in units.Where(u => u != null))
                {
                    if (HasItemInUnitInventory(unit, GrapplingHookItemName))
                    {
                        grapplingHookAssigned = true;
                        break;
                    }

                    if (TryAddPocketItemPreferChestRig(unit, fallbackGrapplingHookData)
                        || TryAddMagazineToBackpack(unit, fallbackGrapplingHookData))
                    {
                        unit.RefreshGrenadeInventoryFromEquipment();
                        grapplingHookAssigned = true;
                        break;
                    }
                }
            }
        }

        private static bool HasItemInUnitInventory(Unit unit, string itemName)
        {
            if (unit == null || string.IsNullOrWhiteSpace(itemName))
                return false;

            bool inPants = unit.PantsInventory.Any(item => string.Equals(item?.Data?.Name, itemName, StringComparison.OrdinalIgnoreCase));
            if (inPants)
                return true;

            bool inChestRig = unit.ChestRigInventory.Any(item => string.Equals(item?.Data?.Name, itemName, StringComparison.OrdinalIgnoreCase));
            if (inChestRig)
                return true;

            unit.EnsureBackpackInventoryGrid();
            return unit.BackpackInventory.GetAllItems().Any(item => string.Equals(item?.Data?.Name, itemName, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureUnitsHaveCompatibleMagazines(List<Unit> units, int minimumMagazinesPerUnit)
        {
            if (units == null || units.Count == 0)
                return;

            int targetMagazines = Math.Max(1, minimumMagazinesPerUnit);

            foreach (Unit unit in units)
            {
                if (unit?.WeaponData == null || !unit.WeaponData.UsesAmmo)
                    continue;

                int existingMags = CountCompatibleMagazines(unit);
                int toAdd = Math.Max(0, targetMagazines - existingMags);

                for (int i = 0; i < toAdd; i++)
                {
                    ItemData magazine = CreateMagazineForWeapon(unit.WeaponData);
                    if (!TryAddPocketItemPreferChestRig(unit, magazine))
                        TryAddMagazineToBackpack(unit, magazine);
                }
            }
        }

        private int CountCompatibleMagazines(Unit unit)
        {
            if (unit?.WeaponData == null)
                return 0;

            int count = 0;
            count += unit.PantsInventory.Count(item => item?.Data?.IsCompatibleMagazineFor(unit.WeaponData) == true);
            count += unit.ChestRigInventory.Count(item => item?.Data?.IsCompatibleMagazineFor(unit.WeaponData) == true);

            unit.EnsureBackpackInventoryGrid();
            count += unit.BackpackInventory.GetAllItems().Count(item => item?.Data?.IsCompatibleMagazineFor(unit.WeaponData) == true);
            return count;
        }

        private ItemData CreateMagazineForWeapon(WeaponData weapon)
        {
            int rounds = Math.Max(1, weapon?.EffectiveMagazineCapacity ?? 1);
            string caliber = weapon?.Caliber ?? "Unknown";
            string name = $"Chargeur {caliber} ({rounds})";
            string description = $"Chargeur 1x1 compatible {caliber}. Contient {rounds} cartouches.";
            float weight = MathF.Max(0.2f, rounds * 0.03f);
            return new ItemData(name, caliber, rounds, weight, description);
        }

        private bool TryAddPocketItemPreferChestRig(Unit unit, ItemData itemData)
        {
            if (unit == null || itemData == null)
                return false;

            unit.ChestRigInventory ??= new List<Item>();
            unit.PantsInventory ??= new List<Item>();

            int chestCapacity = unit.GetChestRigInventoryCapacity();
            if (unit.ChestRigInventory.Count < chestCapacity)
            {
                unit.ChestRigInventory.Add(new Item(itemData, Point.Zero));
                return true;
            }

            int pantsCapacity = unit.GetPantsInventoryCapacity();
            if (unit.PantsInventory.Count < pantsCapacity)
            {
                unit.PantsInventory.Add(new Item(itemData, Point.Zero));
                return true;
            }

            return false;
        }

        private bool TryAddMagazineToBackpack(Unit unit, ItemData itemData)
        {
            if (unit == null || itemData == null)
                return false;

            unit.EnsureBackpackInventoryGrid();
            ItemSize size = ItemSizeDatabase.GetItemSize(itemData.Name);
            Point? freePos = unit.BackpackInventory.FindFreePosition(size, true);
            if (!freePos.HasValue)
                return false;

            unit.BackpackInventory.PlaceItem(new GridItem(itemData, freePos.Value, size, false));
            return true;
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

        private void RemoveDuplicateMk2GrenadesFromAlliedUnits(List<Unit> alliedUnits)
        {
            if (alliedUnits == null || alliedUnits.Count == 0)
                return;

            foreach (var unit in alliedUnits)
            {
                if (unit == null)
                    continue;

                bool keptOneMk2 = false;

                void DeduplicatePocket(List<Item> inventory)
                {
                    for (int i = inventory.Count - 1; i >= 0; i--)
                    {
                        bool isMk2 = string.Equals(inventory[i]?.Data?.Name, "MK 2", StringComparison.OrdinalIgnoreCase);
                        if (!isMk2)
                            continue;

                        if (!keptOneMk2)
                        {
                            keptOneMk2 = true;
                            continue;
                        }

                        inventory.RemoveAt(i);
                    }
                }

                DeduplicatePocket(unit.PantsInventory);
                DeduplicatePocket(unit.ChestRigInventory);

                unit.EnsureBackpackInventoryGrid();
                var backpackMk2Items = unit.BackpackInventory.GetAllItems()
                    .Where(item => string.Equals(item?.Data?.Name, "MK 2", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var backpackMk2Item in backpackMk2Items)
                {
                    if (!keptOneMk2)
                    {
                        keptOneMk2 = true;
                        continue;
                    }

                    unit.BackpackInventory.RemoveItem(backpackMk2Item);
                }

                unit.RefreshGrenadeInventoryFromEquipment();
            }
        }

        private List<Point> GetCityCenterSpawnCells(int count)
        {
            int centerX = gridWidth / 2;
            int centerY = gridHeight / 2;

            var cells = new List<Point>();
            int maxRadius = Math.Max(gridWidth, gridHeight);

            for (int radius = 0; radius <= maxRadius && cells.Count < count; radius++)
            {
                for (int dx = -radius; dx <= radius && cells.Count < count; dx++)
                {
                    for (int dy = -radius; dy <= radius && cells.Count < count; dy++)
                    {
                        // Ne parcourir que l'anneau extérieur du rayon courant
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                            continue;

                        int x = Math.Clamp(centerX + dx, 0, gridWidth - 1);
                        int y = Math.Clamp(centerY + dy, 0, gridHeight - 1);
                        var point = new Point(x, y);

                        if (!cells.Contains(point) && !HasBlockingFurnitureOnFloor(point, 0))
                            cells.Add(point);
                    }
                }
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

            for (int i = 0; i < count; i++)
            {
                int swapIndex = random.Next(i, perimeter.Count);
                (perimeter[i], perimeter[swapIndex]) = (perimeter[swapIndex], perimeter[i]);
            }

            return perimeter.Take(count).ToList();
        }

        // Retourne des cases vides directement adjacentes (4 directions) à un mur extérieur de bâtiment.
        private List<Point> GetBuildingExteriorAdjacentCells(int requestedCount)
        {
            var cells = new List<Point>();
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridHeight; y++)
                {
                    var p = new Point(x, y);
                    if (IsInsideBuildingFootprint(p)) continue;
                    if (playerUnits.Any(u => u.Cell == p)) continue;

                    bool adjacentToBuilding = false;
                    for (int d = 0; d < 4; d++)
                    {
                        if (IsInsideBuildingFootprint(new Point(x + dx[d], y + dy[d])))
                        {
                            adjacentToBuilding = true;
                            break;
                        }
                    }

                    if (adjacentToBuilding)
                        cells.Add(p);
                }
            }

            for (int i = 0; i < Math.Min(requestedCount, cells.Count); i++)
            {
                int swapIndex = random.Next(i, cells.Count);
                (cells[i], cells[swapIndex]) = (cells[swapIndex], cells[i]);
            }

            return cells.Take(requestedCount).ToList();
        }

        private IEnumerable<Unit> AllUnits()
        {
            foreach (var u in playerUnits) yield return u;
            foreach (var u in enemyUnits) yield return u;
        }

        private bool IsCellAvailableOnFloor(Point cell, int floor)
        {
            if (HasBlockingFurnitureOnFloor(cell, floor))
                return false;

            if (floor == 0)
                return true;

            if (GetCellsForFloor(floor).Contains(cell))
                return true;

            if (currentMap?.RampTiles != null)
            {
                foreach (var ramp in currentMap.RampTiles)
                {
                    int rampDx = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDx : 0;
                    int rampDy = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDy : -1;

                    if (ramp.Floor == floor && ramp.X == cell.X && ramp.Y == cell.Y)
                        return true;

                    if (ramp.Floor + 1 == floor && ramp.X + rampDx == cell.X && ramp.Y + rampDy == cell.Y)
                        return true;
                }
            }

            return false;
        }

        private bool HasBlockingFurnitureOnFloor(Point cell, int floor)
        {
            if (currentMap?.Furnitures == null)
                return false;

            return currentMap.Furnitures.Any(f =>
                f.Floor == floor &&
                IsMovementBlockingFurnitureType(f.Type) &&
                FurnitureData.GetOccupiedCells(f).Contains(cell));
        }

        private static bool IsMovementBlockingFurnitureType(FurnitureType type)
        {
            return FurnitureData.IsVehicle(type);
        }

        private bool TryResolveHoverableCellFloor(Point cell, int preferredFloor, out int resolvedFloor)
        {
            resolvedFloor = preferredFloor;

            if (IsCellAvailableOnFloor(cell, preferredFloor))
                return true;

            return TryResolveAvailableClickedFloor(cell, preferredFloor, out resolvedFloor);
        }

        Unit GetUnitAtCell(Point cell)
        {
            return unitManager.SpatialHash.GetUnitAt(cell, 0);
        }

        Unit GetUnitAtCellOnFloor(Point cell, int floor)
        {
            return unitManager.SpatialHash.GetUnitAt(cell, floor);
        }

        FurnitureData GetFurnitureAtCellOnFloor(Point cell, int floor)
        {
            if (currentMap?.Furnitures == null)
                return null;

            return currentMap.Furnitures.FirstOrDefault(f =>
                f.Floor == floor &&
                FurnitureData.GetOccupiedCells(f).Contains(cell));
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
            new("Zombie","Undead","Zombie Claws",2),
            new("Giant Spider","Spider","Spider Fangs",3)
        };

        // Toiles d'araignée : cases recouvertes de toile qui ralentissent les unités non-araignées.
        private readonly HashSet<(Point cell, int floor)> spiderWebTiles = new();

        private void InitializeWeapons()
        {
            // ✅ Charger toutes les nouvelles armes
            weaponDatabase = WeaponDatabase.GetAllWeapons();

            Console.WriteLine($"[WEAPONS] Loaded {weaponDatabase.Count} weapons");
        }

        private void StartMission(string missionType)
        {
            EnsurePremadeMapsGenerated();
            currentState = GameState.Playing;

            // ✅ NOUVEAU : Charger une carte (générée aléatoirement)
            LoadMap(); // Génère automatiquement une carte selon selectedMission

            ResetFogOfWar();
            ResetLootSystem();
            InitializeCrateLoot();

            CreateUnits(missionType);
            foreach (var unit in playerUnits.Concat(enemyUnits))
            {
                unit.OnCellEntered += HandleUnitCellEntered;
            }

            floorViewMode = FloorViewMode.AutoFollow;
            explicitUpperFloorTargeting = false;
            wallSegments = currentMap.GetWalls();
            shatteredWindows.Clear();
            shatteredVehicleWindows.Clear();
            InvalidateWallsByFloorCache();
            var wallsByFloor = new Dictionary<int, HashSet<WallSegment>>();
            for (int f = GetMinimumViewFloor(); f < currentMap.FloorCount; f++) wallsByFloor[f] = GetWallsForFloor(f);
            spiderWebTiles.Clear();
            if (missionType == "Centre-Ville")
                SpawnCentreVilleSpiderWebs();
            pathfinding = new PathfindingSystem(gridWidth, gridHeight, currentMap.FloorCount, wallsByFloor, currentMap.RampTiles, GetUnitAtCell, GetUnitAtCellOnFloor, IsCellAvailableOnFloor);
            // Toiles d'araignée : coût +1 pour traverser une case couverte de toile (non-araignées seulement).
            pathfinding.GetExtraCellCost = (cell, floor) => spiderWebTiles.Contains((cell, floor)) ? 1 : 0;
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

        // Tisse des toiles d'araignée sur toutes les rues étroites entre deux bâtiments face à face.
        private void SpawnCentreVilleSpiderWebs()
        {
            if (currentMap?.Buildings == null || currentMap.Buildings.Count == 0) return;

            const int maxStreetWidth = 8;

            // Corridors horizontaux : bâtiment → rue → bâtiment sur une même rangée
            for (int y = 0; y < gridHeight; y++)
            {
                int streetStart = -1;
                bool prevWasBuilding = false;

                for (int x = 0; x < gridWidth; x++)
                {
                    bool inBuilding = IsInsideBuildingFootprint(new Point(x, y));

                    if (!inBuilding && prevWasBuilding)
                        streetStart = x;
                    else if (inBuilding && streetStart >= 0)
                    {
                        int width = x - streetStart;
                        if (width <= maxStreetWidth)
                            for (int wx = streetStart; wx < x; wx++)
                                spiderWebTiles.Add((new Point(wx, y), 0));
                        streetStart = -1;
                    }

                    prevWasBuilding = inBuilding;
                }
            }

            // Corridors verticaux : bâtiment → rue → bâtiment sur une même colonne
            for (int x = 0; x < gridWidth; x++)
            {
                int streetStart = -1;
                bool prevWasBuilding = false;

                for (int y = 0; y < gridHeight; y++)
                {
                    bool inBuilding = IsInsideBuildingFootprint(new Point(x, y));

                    if (!inBuilding && prevWasBuilding)
                        streetStart = y;
                    else if (inBuilding && streetStart >= 0)
                    {
                        int width = y - streetStart;
                        if (width <= maxStreetWidth)
                            for (int wy = streetStart; wy < y; wy++)
                                spiderWebTiles.Add((new Point(x, wy), 0));
                        streetStart = -1;
                    }

                    prevWasBuilding = inBuilding;
                }
            }
        }

        private void HandleUnitCellEntered(Unit unit, Point cell, int floor)
        {
            unitManager.OnUnitMoved(unit, cell, floor);
            visibilityDirty = true;

            // Les araignées déposent une toile sur chaque case traversée.
            if (unit.CanWalkOnWalls)
                spiderWebTiles.Add((cell, floor));
        }

        private void ResetFogOfWar()
        {
            exploredCells.Clear();
            visibleCells.Clear();
            enemyGhosts.Clear();
            currentlySpottedEnemies.Clear();
        }

        private void ResetLootSystem()
        {
            flashlightLootMarkers.Clear();
            inventorySystem?.ClearNearbyLoot();
        }

        private static readonly string[] CrateLootPool = new[]
        {
            "M1 Helmet",
            "MICH",
            "M-1952 Flak Jacket",
            "Chargeur 5.56x45mm (30)",
            "Chargeur 9x19mm (30)",
            "Chargeur 7.62x39mm (30)",
            "Chargeur 12 Gauge (30)",
            "MK 2",
            "Lampe tactique aluminium",
            "Chest Rig Léger",
            "Chest Rig Assaut",
            "Genouilleres Souples",
            "Bottes de Patrouille",
        };

        private void InitializeCrateLoot()
        {
            if (currentMap?.Furnitures == null || inventorySystem == null)
                return;

            foreach (FurnitureData furniture in currentMap.Furnitures)
            {
                if (furniture.Type != FurnitureType.LootCrate)
                    continue;

                Point crateCell = new Point(furniture.X, furniture.Y);
                int itemCount = random.Next(2, 5);
                var pool = new List<string>(CrateLootPool);

                for (int i = 0; i < itemCount && pool.Count > 0; i++)
                {
                    int idx = random.Next(pool.Count);
                    string itemName = pool[idx];
                    pool.RemoveAt(idx);
                    RegisterGroundLoot(itemName, crateCell, furniture.Floor);
                }
            }

            Console.WriteLine($"[LOOT] Crate loot initialized for {currentMap.Furnitures.Count(f => f.Type == FurnitureType.LootCrate)} crates.");
        }

        private bool IsCellExplored(Point cell, int floor)
        {
            if (exploredCells.TryGetValue(floor, out var grid))
            {
                if (cell.X >= 0 && cell.X < grid.GetLength(0) && cell.Y >= 0 && cell.Y < grid.GetLength(1))
                    return grid[cell.X, cell.Y];
            }
            return false;
        }

        private bool[,] GetVisibilityGrid(int floor)
        {
            if (!visibleCells.TryGetValue(floor, out var grid))
            {
                grid = new bool[gridWidth, gridHeight];
                visibleCells[floor] = grid;
            }
            return grid;
        }

        private bool[,] GetExplorationGrid(int floor)
        {
            if (!exploredCells.TryGetValue(floor, out var grid))
            {
                grid = new bool[gridWidth, gridHeight];
                exploredCells[floor] = grid;
            }
            return grid;
        }

        private bool IsCellVisible(Point cell, int floor)
        {
            if (visibleCells.TryGetValue(floor, out var grid))
            {
                if (cell.X >= 0 && cell.X < grid.GetLength(0) && cell.Y >= 0 && cell.Y < grid.GetLength(1))
                    return grid[cell.X, cell.Y];
            }
            return false;
        }


        private void HandlePlayerTurn(MouseState mouse, bool leftClick, KeyboardState keyboard, GameTime gameTime)
        {
            if (IsTabPressed(keyboard)) SelectNextActiveUnit();

            explicitUpperFloorTargeting = !grappleMode &&
                (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
            int interactionFloor = ResolveInteractionFloor(viewedFloor);

            if (leftClick && HandleFloorControlButtonClicks(mouse, gameTime))
                leftClick = false;

            Point rawHoveredCell = camera.GetCellFromMouse(
                mouse.Position,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                WorldMetrics.FloorToWorldY(interactionFloor, cellSize));

            // Correction de parallaxe terrain itérative : le renderer dessine chaque tuile
            // avec des hauteurs de coins *interpolées bilinéairement* (moyenne des cellules
            // voisines). Une projection one-shot sur la hauteur discrète du centre de la
            // case initiale rate les bords de tranchée, où la surface visuelle est à mi-
            // chemin entre les deux hauteurs. On itère jusqu'à convergence en utilisant la
            // même interpolation bilinéaire que le renderer pour éliminer ce décalage résiduel.
            if (rawHoveredCell.X >= 0 && rawHoveredCell.Y >= 0)
            {
                float baseY = WorldMetrics.FloorToWorldY(interactionFloor, cellSize);
                Ray mouseRay = camera.ScreenPointToRay(
                    mouse.Position,
                    GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height);

                for (int iter = 0; iter < 4; iter++)
                {
                    if (MathF.Abs(mouseRay.Direction.Y) < 0.001f) break;

                    // Point d'intersection avec le plan de la case courante
                    float planeY = baseY + GetTerrainHeightOffset(rawHoveredCell);
                    float t = (planeY - mouseRay.Position.Y) / mouseRay.Direction.Y;
                    if (t < 0f) break;

                    Vector3 hitPoint = mouseRay.Position + mouseRay.Direction * t;

                    // Hauteur visuelle réelle au point de contact (interpolation bilinéaire
                    // identique à celle du renderer), relative à baseY
                    float interpH = GetBilinearTerrainHeight(hitPoint.X, hitPoint.Z);

                    // Convergé si la surface interpolée coïncide avec le plan courant
                    if (MathF.Abs(interpH - GetTerrainHeightOffset(rawHoveredCell)) < 0.05f)
                        break;

                    // Re-projeter sur le plan de la vraie surface visuelle
                    Point refined = camera.GetCellFromMouse(
                        mouse.Position,
                        GraphicsDevice.Viewport.Width,
                        GraphicsDevice.Viewport.Height,
                        baseY + interpH);

                    if (refined.X < 0 || refined.Y < 0 || refined == rawHoveredCell)
                        break;

                    rawHoveredCell = refined;
                }
            }

            // Quand on vise un étage différent du rez-de-chaussée (au-dessus ou en dessous),
            // le raycast sur ce plan décale la cellule au sol (effet de parallaxe) pour les
            // zones extérieures. Reprojeter sur le sol corrige l'alignement curseur/case
            // hors empreinte des bâtiments.
            if (rawHoveredCell.X >= 0 && rawHoveredCell.Y >= 0 &&
                interactionFloor != 0 &&
                !IsInsideBuildingFootprint(rawHoveredCell))
            {
                Point groundHoveredCell = camera.GetCellFromMouse(
                    mouse.Position,
                    GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height,
                    WorldMetrics.FloorToWorldY(0, cellSize));

                if (groundHoveredCell.X >= 0 && groundHoveredCell.Y >= 0)
                    rawHoveredCell = groundHoveredCell;
            }

            int hoveredInteractionFloor = ResolveHoveredCellPreferredFloor(rawHoveredCell, interactionFloor);
            isHoveringValidCell = rawHoveredCell.X != -1 &&
                TryResolveHoverableCellFloor(rawHoveredCell, hoveredInteractionFloor, out hoveredInteractionFloor);
            if (isHoveringValidCell)
            {
                hoveredCell = rawHoveredCell;
                hoveredCellFloor = hoveredInteractionFloor;
            }

            // 1. Check if we have a valid unit and valid cell
            if (!grappleMode && !c4PlacementMode && selectedUnit != null && selectedUnit.ActionPoints > 0 && isHoveringValidCell &&
                selectedUnit.Team == Team.Player)
            {
                // 2. Define maxRange here
                int maxRange = selectedUnit.CanSprint() ?
                    selectedUnit.GetSprintRange() : selectedUnit.GetMaxMoveRange();

                // 3. ONLY recalculate the path if the mouse moved to a new cell
                if (hoveredCell != lastHoveredCell || hoveredInteractionFloor != lastHoveredInteractionFloor)
                {
                    Point previewGoal = hoveredCell;
                    int previewFloor = hoveredInteractionFloor;

                    if (!IsCellAvailableOnFloor(previewGoal, previewFloor))
                        TryResolveAvailableClickedFloor(previewGoal, previewFloor, out previewFloor);

                    if (TryResolveVerticalTransition(selectedUnit.Floor, hoveredCell, out Point transitionGoal, out int transitionFloor))
                    {
                        previewGoal = transitionGoal;
                        previewFloor = transitionFloor;
                    }

                    var previewPath = pathfinding.FindPathDetailed(selectedUnit.Cell, selectedUnit.Floor, previewGoal, previewFloor, maxRange, selectedUnit);
                    currentPath = previewPath.Cells;
                    currentPathNodes = previewPath.Nodes;
                    currentPathEndFloor = previewPath.EndFloor;
                    lastHoveredCell = hoveredCell;
                    lastHoveredInteractionFloor = hoveredInteractionFloor;

                    pathCosts.Clear();

                    int cumulativeCost = 0;
                    GridNode previousNode = new GridNode(selectedUnit.Cell, selectedUnit.Floor);
                    foreach (var node in currentPathNodes)
                    {
                        cumulativeCost += 1;
                        if (node.Floor != previousNode.Floor)
                            cumulativeCost += PathfindingSystem.VerticalTransitionExtraCost;

                        pathCosts[node.Cell] = cumulativeCost;
                        previousNode = node;
                    }
                }
            }
            else
            {
                currentPath.Clear();
                currentPathNodes.Clear();
                currentPathEndFloor = selectedUnit?.Floor ?? viewedFloor;
                pathCosts.Clear();

                // If the mouse isn't on a valid movement cell, update the last hovered cell anyway
                // so it recalculates correctly when it re-enters a valid cell
                lastHoveredCell = isHoveringValidCell ? hoveredCell : new Point(-1, -1);
                lastHoveredInteractionFloor = isHoveringValidCell ? hoveredInteractionFloor : -1;
            }

            if (throwMode) HandleGrenadeThrow(mouse, leftClick);
            if (c4PlacementMode) HandleSatchelPlacement(mouse, leftClick);
            if (grappleMode) HandleGrappleAction(mouse, leftClick);

            if (selectedUnit != null && selectedUnit.Team == Team.Player && combatUI.ShowFireTargets)
            {
                // En mode sélection de cible (et en mode grenade), la probabilité affichée
                // reste figée sur la position actuelle de l'unité.
                combatUI.UpdateFireTargetHitChances(selectedUnit, selectedUnit.Cell);
            }

            GetFloorControlButtonRects(out Rectangle floorDownButton, out Rectangle floorUpButton, out Rectangle floorModeButton);
            bool clickOnUI = combatUI.EndTurnButton.Contains(mouse.Position) ||
                combatUI.FireButton.Contains(mouse.Position) ||
                combatUI.IsMouseOverActionButton(mouse) ||
                combatUI.IsMouseOverFireTargets(mouse) ||
                floorDownButton.Contains(mouse.Position) ||
                floorUpButton.Contains(mouse.Position) ||
                floorModeButton.Contains(mouse.Position) || showInventory;

            if (leftClick) HandleUnitActionButtons(mouse);
            if (leftClick && combatUI.ShowFireTargets) combatUI.HandleFireTargetClick(mouse, selectedUnit);
            if (leftClick && !clickOnUI && !throwMode && !c4PlacementMode && !grappleMode && isHoveringValidCell) HandleGridClick(hoveredCell, hoveredInteractionFloor, allowSmartFallback: !explicitUpperFloorTargeting);

            bool rightClick = mouse.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released;
            if (rightClick)
            {
                if (combatUI.TryCycleWeaponFireModeAt(mouse.Position, selectedUnit))
                {
                    Console.WriteLine($"[COMBAT] {selectedUnit.Name} switched fire mode: {selectedUnit.WeaponData.CurrentFireMode}");
                }
                else
                {
                    CancelSelection();
                }
            }

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

            // Passage automatique au tour ennemi quand toutes les unités du joueur
            // ont terminé leurs actions et qu'aucune animation d'action n'est en cours.
            if (!combatSystem.IsActionInProgress &&
                combatSystem.CurrentTurn == TurnState.PlayerTurn &&
                playerUnits.All(u => u.ActionPoints <= 0) &&
                playerUnits.All(u => !u.IsMoving && !u.IsFiring))
            {
                combatSystem.StartEnemyTurn();
            }
        }

        private bool TryResolveVerticalTransition(int fromFloor, Point clickedCell, out Point movementGoal, out int goalFloor)
        {
            movementGoal = clickedCell;
            goalFloor = fromFloor;

            if (currentMap?.RampTiles != null)
            {
                foreach (var ramp in currentMap.RampTiles)
                {
                    int rampDx = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDx : 0;
                    int rampDy = (Math.Abs(ramp.AscendDx) + Math.Abs(ramp.AscendDy) == 1) ? ramp.AscendDy : -1;

                    if (ramp.Floor == fromFloor && ramp.X == clickedCell.X && ramp.Y == clickedCell.Y)
                    {
                        movementGoal = new Point(ramp.X + rampDx, ramp.Y + rampDy);
                        goalFloor = fromFloor + 1;
                        return true;
                    }

                    if (ramp.Bidirectional && ramp.Floor + 1 == fromFloor && ramp.X + rampDx == clickedCell.X && ramp.Y + rampDy == clickedCell.Y)
                    {
                        movementGoal = new Point(ramp.X, ramp.Y);
                        goalFloor = fromFloor - 1;
                        return true;
                    }
                }
            }

            return false;
        }

        private void HandleGridClick(Point clickedCell, int clickedFloor, bool allowSmartFallback = true)
        {
            int interactionFloor = clickedFloor;
            if (!IsCellAvailableOnFloor(clickedCell, interactionFloor))
            {
                if (!allowSmartFallback || !TryResolveAvailableClickedFloor(clickedCell, interactionFloor, out interactionFloor))
                    return;
            }

            Unit clickedUnit = GetUnitAtCellOnFloor(clickedCell, interactionFloor);

            if (clickedUnit != null && clickedUnit.Team == Team.Enemy && !IsEnemyVisibleToPlayers(clickedUnit))
            {
                clickedUnit = null;
            }

            if (clickedUnit != null)
            {
                ExitThrowMode();
                selectedUnit = clickedUnit;
                if (selectedUnit.Team == Team.Player)
                {
                    if (pathfinding != null)
                    {
                        cachedMovableCells = pathfinding.GetMovableCells(selectedUnit);
                        UpdateEnemyPerceptionVisibility();
                        var validTargets = FilterTargetsByPerception(selectedUnit, combatSystem.GetValidFireTargets(selectedUnit));
                        combatUI.UpdateFireTargets(selectedUnit, validTargets);
                        combatUI.ShowFireTargets = false;
                        combatUI.SelectedFireTarget = null;
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
                    currentPathNodes.Clear();
                    currentPathEndFloor = interactionFloor;
                    pathCosts.Clear();
                }
            }
            else if (selectedUnit != null && selectedUnit.ActionPoints > 0)
            {
                if (pathfinding == null) return;

                // Calculer le chemin
                Point movementGoal = clickedCell;
                int goalFloor = interactionFloor;

                // Conserver le comportement existant: cliquer directement sur une rampe/
                // un escalier depuis l'étage de l'unité déclenche la transition immédiate.
                if (selectedUnit.Floor == interactionFloor &&
                    TryResolveVerticalTransition(selectedUnit.Floor, clickedCell, out Point transitionGoal, out int transitionFloor))
                {
                    movementGoal = transitionGoal;
                    goalFloor = transitionFloor;
                }

                if (!IsCellAvailableOnFloor(movementGoal, goalFloor))
                    return;

                var detailedPath = pathfinding.FindPathDetailed(selectedUnit.Cell, selectedUnit.Floor, movementGoal, goalFloor,
                                               selectedUnit.GetSprintRange(), selectedUnit);
                var path = detailedPath.Cells;
                var pathNodes = detailedPath.Nodes;

                if (path.Count == 0) return;

                int distance = pathfinding.GetPathCost(pathNodes, new GridNode(selectedUnit.Cell, selectedUnit.Floor));
                int verticalTransitions = pathfinding.GetVerticalTransitionCount(pathNodes, new GridNode(selectedUnit.Cell, selectedUnit.Floor));
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
                    Console.WriteLine($"[MOVEMENT] Short move: {distance} cost ({path.Count} cells, {verticalTransitions} transitions) (1 AP + {phosphocreatineCost}% phosphocreatine)");
                }
                else if (distance <= maxRange && selectedUnit.ActionPoints >= 2 && selectedUnit.Phosphocreatine >= phosphocreatineCost)
                {
                    // Zone bleue (2 AP)
                    apCost = 2;
                    consumesPhosphocreatine = true;
                    Console.WriteLine($"[MOVEMENT] Max move: {distance} cost ({path.Count} cells, {verticalTransitions} transitions) (2 AP + {phosphocreatineCost}% phosphocreatine)");
                }
                else if (distance <= sprintRange && selectedUnit.CanSprint(distance))
                {
                    // Zone jaune (2 AP + phosphocréatine)
                    apCost = 2;
                    consumesPhosphocreatine = true;
                    Console.WriteLine($"[MOVEMENT] SPRINT: {distance} cost ({path.Count} cells, {verticalTransitions} transitions) (2 AP + {phosphocreatineCost}% phosphocreatine)");
                }
                else
                {
                    // Hors de portée ou pas assez de ressources
                    Console.WriteLine($"[MOVEMENT] Cannot reach: {distance} cost ({path.Count} cells, {verticalTransitions} transitions) (out of range or insufficient resources)");
                    return;
                }

                // Effectuer le déplacement
                selectedUnit.SetMovementStyle(apCost, distance > maxRange);
                selectedUnit.StartMoveAlongPath(pathNodes, cellSize);
                // La position logique et le spatial hash sont maintenant mis à jour progressivement via OnCellEntered
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
                currentPathNodes.Clear();
                currentPathEndFloor = selectedUnit.Floor;
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
                    case "FIRE":
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
                            GrenadeData throwableGrenade = selectedUnit.Grenades.FirstOrDefault(g => g.Type != GrenadeType.SatchelC4);
                            if (throwableGrenade == null)
                                break;

                            ExitSatchelPlacementMode();
                            ExitGrappleMode();
                            throwMode = true;
                            throwModeUsesFlashlight = false;
                            throwFlashlightFromRightHand = false;
                            selectedGrenade = throwableGrenade;
                            throwableCellsCacheValid = false;
                            Console.WriteLine($"Mode grenade activé: {selectedGrenade.Name}");
                        }
                        break;

                    case "C4-POSE":
                        ActivateSatchelPlacementMode();
                        break;

                    case "DETONATE":
                        TriggerSatchelDetonation(selectedUnit);
                        break;

                    case "RELOAD":
                        Console.WriteLine("Action future : RELOAD");
                        break;

                    case "OVERWATCH":
                        ActivateOverwatch(selectedUnit);
                        break;

                    case "ANAEROBIC":
                        if (!selectedUnit.ActivateAnaerobicEffort())
                            Console.WriteLine("[ANAEROBIC] Effort anaérobie indisponible (déjà utilisé ce tour ou AP insuffisants).");
                        break;

                    case "GRAPPLIN":
                        ActivateGrappleMode();
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

            foreach (var unit in playerUnits.Concat(enemyUnits))
            {
                unit.OnCellEntered += HandleUnitCellEntered;
            }

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
            currentPathNodes.Clear();
            currentPathEndFloor = viewedFloor;
            pathCosts.Clear();

            ExitThrowMode();
            ExitSatchelPlacementMode();
            ExitGrappleMode();
        }

        private void ExitThrowMode()
        {
            throwMode = false;
            selectedGrenade = null;
            throwModeUsesFlashlight = false;
            throwFlashlightFromRightHand = false;
            throwableCells.Clear();
            explosionPreview.Clear();
            trajectoryPreview.Clear();
            ricochetPreview.Clear();
        }

        private void ExitGrappleMode()
        {
            grappleMode = false;
            grappleTargetFloor = -1;
            grappleAnchors.Clear();
            floorViewMode = FloorViewMode.AutoFollow;
        }

        private void ActivateFlashlightThrowMode(bool fromRightHand)
        {
            if (selectedUnit == null || selectedUnit.Team != Team.Player)
                return;

            Item equippedFlashlight = fromRightHand ? selectedUnit.EquippedRightHandFlashlight : selectedUnit.EquippedLeftHandFlashlight;
            if (equippedFlashlight?.Data == null)
                return;

            if (selectedUnit.ActionPoints < TacticalFlashlightThrowApCost)
                return;

            ExitSatchelPlacementMode();
            ExitGrappleMode();
            throwMode = true;
            throwModeUsesFlashlight = true;
            throwFlashlightFromRightHand = fromRightHand;
            selectedGrenade = new GrenadeData(TacticalFlashlightItemName, GrenadeType.Flashbang, 0, 0, aoCost: TacticalFlashlightThrowApCost);

            throwableCellsCacheValid = false;
            Console.WriteLine($"Mode lancer lampe activé ({(fromRightHand ? "main droite" : "main gauche")}).");
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
