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
        private bool isHoveringValidCell;

        // --- Murs sur les edges des cases ---
        private HashSet<WallSegment> wallSegments = new HashSet<WallSegment>();
        private readonly HashSet<WindowInstance> shatteredWindows = new HashSet<WindowInstance>();
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
        private int viewedFloor = 0;
        private enum FloorViewMode { AutoFollow, Manual, AbilityLocked }
        private FloorViewMode floorViewMode = FloorViewMode.AutoFollow;
        private double manualFloorViewUntilSeconds = 0d;
        private bool explicitUpperFloorTargeting = false;
        private const double ManualFloorViewHoldSeconds = 6d;
        private HashSet<Point> upperFloorCells = new();
        private HashSet<Point> roadCells = new();
        private HashSet<Point> sidewalkCells = new();
        private Dictionary<Point, float> terrainHeights = new Dictionary<Point, float>();
        private readonly Dictionary<int, HashSet<WallSegment>> wallsByFloorCache = new Dictionary<int, HashSet<WallSegment>>();
        private Unit movementCinematicUnit = null;
        private readonly Dictionary<Unit, bool> firingShoulderCameraDecisions = new Dictionary<Unit, bool>();
        private HashSet<Unit> currentlySpottedEnemies = new HashSet<Unit>();
        private readonly string[] gameplaySongAssetNames = { "menu_music_1", "menu_music_2", "menu_music_3", "menu_music_4" };
        private readonly Dictionary<string, Song> gameplaySongCache = new();
        private Song currentGameplaySong;
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
            camera = new CameraController(gridWidth, gridHeight, cellSize, GraphicsDevice.Viewport.AspectRatio);
            inventorySystem = new InventorySystem(GraphicsDevice, _spriteBatch, font, pixel);
            unitManager = new OptimizedUnitManager();

            pathfinding = new PathfindingSystem(gridWidth, gridHeight, 1, new HashSet<WallSegment>(), new List<StairConnectionData>(), new List<RampTileData>(), GetUnitAtCell, GetUnitAtCellOnFloor, IsCellAvailableOnFloor);
            statsPanel = new StatsPanel(
                Content.Load<SpriteFont>("Arial"),
                GraphicsDevice);
            characterInfoPanel = new CharacterInfoPanel(font, GraphicsDevice);

            combatSystem = new CombatSystem(random, pathfinding, GetUnitAtCell, GetFurnitureAtCellOnFloor, unitManager);
            combatSystem.SetEnemyVisibilityEvaluator((enemy, cell, floor) => IsEnemyCellVisibleToPlayers(enemy, cell, floor));
            combatUI = new CombatUISystem(GraphicsDevice, _spriteBatch, font, pixel);
            combatSystem.OnUnitKilled += HandleUnitKilled;
            combatSystem.OnFireCompleted += HandleFireCompleted;
            combatSystem.OnShotFired += HandleShotFired;

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
            centreVilleMusicEffect = CreateCentreVilleCyberpunkLoop();
            casingClingSoundEffect = CreateProceduralCasingClingSound();
            grenadeExplosionSoundEffect = CreateProceduralGrenadeExplosionSound();
            VisualEffects.OnSpentCasingLanded += HandleSpentCasingLanded;
        }

        private SoundEffect CreateCentreVilleCyberpunkLoop()
        {
            const int sampleRate = 44100;
            const float durationSeconds = 8f;
            int sampleCount = (int)(sampleRate * durationSeconds);
            short[] samples = new short[sampleCount];

            float[] rootPattern = { 55f, 65.41f, 73.42f, 82.41f };
            float[] leadIntervals = { 0f, 3f, 7f, 10f, 12f, 15f, 19f, 22f };

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float beatPhase = t % 0.5f;
                float sectionPhase = t % 4f;
                int progressionIndex = (int)(sectionPhase / 1f) % rootPattern.Length;
                float root = rootPattern[progressionIndex];

                float kickEnvelope = MathF.Max(0f, 1f - beatPhase / 0.16f);
                float kickFreq = 48f + (beatPhase < 0.08f ? (1f - beatPhase / 0.08f) * 84f : 0f);
                float kick = MathF.Sin(2f * MathF.PI * kickFreq * beatPhase) * kickEnvelope;

                float snarePhase = (t + 0.25f) % 0.5f;
                float snareEnvelope = snarePhase < 0.055f ? MathF.Exp(-38f * snarePhase) : 0f;
                float snareNoise = ((float)random.NextDouble() * 2f - 1f) * snareEnvelope;
                float snareTone = MathF.Sin(2f * MathF.PI * 185f * snarePhase) * snareEnvelope * 0.6f;
                float snare = snareNoise * 0.75f + snareTone;

                float bassGate = MathF.Pow(MathF.Max(0f, 1f - (beatPhase / 0.34f)), 1.3f);
                float bass = (MathF.Sin(2f * MathF.PI * root * t)
                    + 0.42f * MathF.Sin(2f * MathF.PI * root * 2f * t)
                    + 0.16f * MathF.Sin(2f * MathF.PI * root * 3f * t)) * bassGate;

                float hatPhase = t % 0.125f;
                float hatEnvelope = hatPhase < 0.022f ? MathF.Exp(-95f * hatPhase) : 0f;
                float hatNoise = ((float)random.NextDouble() * 2f - 1f) * hatEnvelope;
                float hatOpenPhase = t % 0.5f;
                float hatOpenEnvelope = hatOpenPhase > 0.37f && hatOpenPhase < 0.5f
                    ? MathF.Exp(-12f * (hatOpenPhase - 0.37f)) * 0.3f
                    : 0f;
                float hatOpen = ((float)random.NextDouble() * 2f - 1f) * hatOpenEnvelope;

                int arpStep = (int)(t / 0.125f) % leadIntervals.Length;
                float leadFreq = root * MathF.Pow(2f, leadIntervals[arpStep] / 12f);
                float leadGate = MathF.Pow(MathF.Max(0f, 1f - ((t % 0.125f) / 0.12f)), 2.2f);
                float leadDetune = MathF.Sin(2f * MathF.PI * (leadFreq * 1.005f) * t);
                float leadMain = MathF.Sin(2f * MathF.PI * leadFreq * t);
                float lead = (leadMain * 0.75f + leadDetune * 0.25f) * leadGate;

                float padPump = 0.55f + 0.45f * MathF.Pow(MathF.Min(1f, beatPhase / 0.48f), 1.2f);
                float padLfo = 0.65f + 0.35f * MathF.Sin(2f * MathF.PI * 0.13f * t + 0.8f);
                float minorThird = root * MathF.Pow(2f, 3f / 12f);
                float fifth = root * MathF.Pow(2f, 7f / 12f);
                float octave = root * 2f;
                float pad = (
                    0.32f * MathF.Sin(2f * MathF.PI * root * t)
                    + 0.26f * MathF.Sin(2f * MathF.PI * minorThird * t)
                    + 0.22f * MathF.Sin(2f * MathF.PI * fifth * t)
                    + 0.18f * MathF.Sin(2f * MathF.PI * octave * t)
                ) * padPump * padLfo;

                float riser = MathF.Sin(2f * MathF.PI * (620f + sectionPhase * 120f) * t) * MathF.Pow(sectionPhase / 4f, 2f) * 0.08f;

                float mix = kick * 0.46f
                    + snare * 0.18f
                    + bass * 0.27f
                    + (hatNoise + hatOpen) * 0.11f
                    + lead * 0.22f
                    + pad * 0.17f
                    + riser;

                mix = MathF.Tanh(mix * 1.55f);

                samples[i] = (short)(MathHelper.Clamp(mix, -1f, 1f) * short.MaxValue);
            }

            return new SoundEffect(ConvertPcm16ToBytes(samples), sampleRate, AudioChannels.Mono);
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
            if (gunshotSoundEffectInstance == null)
                return;

            float volume = MathHelper.Clamp(0.4f + optionsMenuManager.GetMusicVolume() * 0.25f, 0.2f, 0.8f);
            float pitch = MathHelper.Clamp((float)(random.NextDouble() * 0.16 - 0.08), -1f, 1f);

            gunshotSoundEffectInstance.Stop();
            gunshotSoundEffectInstance.Volume = volume;
            gunshotSoundEffectInstance.Pitch = pitch;
            gunshotSoundEffectInstance.Play();

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

        private void PlayGameplaySongForMission(string missionType)
        {
            if (string.Equals(missionType, "Centre-Ville", StringComparison.OrdinalIgnoreCase))
            {
                MediaPlayer.Stop();
                if (centreVilleMusicEffectInstance == null && centreVilleMusicEffect != null)
                    centreVilleMusicEffectInstance = centreVilleMusicEffect.CreateInstance();

                if (centreVilleMusicEffectInstance != null)
                {
                    centreVilleMusicEffectInstance.IsLooped = true;
                    centreVilleMusicEffectInstance.Volume = MathHelper.Clamp(optionsMenuManager?.GetMusicVolume() ?? 0.5f, 0f, 1f);
                    centreVilleMusicEffectInstance.Play();
                    Console.WriteLine("[AUDIO] In-game music: procedural_centre_ville_cyberpunk_loop");
                }

                return;
            }

            StopCentreVilleMusicLoop();
            MediaPlayer.Stop();
            Console.WriteLine("[AUDIO] In-game music disabled for this mission");
        }

        private void PlayRandomGameplaySong()
        {
            if (gameplaySongAssetNames.Length == 0)
                return;

            string songAssetName = gameplaySongAssetNames[random.Next(gameplaySongAssetNames.Length)];
            PlayGameplaySong(songAssetName);
        }

        private void PlayGameplaySong(string songAssetName)
        {
            StopCentreVilleMusicLoop();

            if (!gameplaySongCache.TryGetValue(songAssetName, out currentGameplaySong))
            {
                currentGameplaySong = Content.Load<Song>(songAssetName);
                gameplaySongCache[songAssetName] = currentGameplaySong;
            }

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = optionsMenuManager.GetMusicVolume();
            MediaPlayer.Play(currentGameplaySong);
            Console.WriteLine($"[AUDIO] In-game music: {songAssetName}");
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
            DropUnitLootToGround(unit);
            RegisterDeadUnitRemains(unit, kineticImpulse);
            if (unit.Team == Team.Player) { playerUnits.Remove(unit); if (playerUnits.Count == 0) currentState = GameState.GameOver; }
            else enemyUnits.Remove(unit);
            unitManager.OnUnitDied(unit);
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

            UpdateEnemyPerceptionVisibility();

            combatSystem.UpdateFiringAnimations(gameTime);
            UpdateAimCameraAndPose();
            camera.HandleControls(keyboard, mouse, previousMouseState, gameTime, allowZoom: !statsPanel.IsVisible);
            UpdateDayNightCycle(gameTime);
            HandleFloorViewControls(keyboard, gameTime);

            if (escapePressed) ReturnToMainMenuWithSave();
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

            if (floorViewMode == FloorViewMode.Manual && gameTime.TotalGameTime.TotalSeconds >= manualFloorViewUntilSeconds)
                floorViewMode = FloorViewMode.AutoFollow;

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
            manualFloorViewUntilSeconds = gameTime.TotalGameTime.TotalSeconds + ManualFloorViewHoldSeconds;
            viewedFloor = Math.Clamp(targetFloor, minFloor, maxFloor);
        }

        private int ResolveInteractionFloor(int baseFloor)
        {
            if (!explicitUpperFloorTargeting)
                return baseFloor;

            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);
            return Math.Clamp(baseFloor + 1, GetMinimumViewFloor(), maxFloor);
        }

        private bool TryResolveAvailableClickedFloor(Point cell, int preferredFloor, out int resolvedFloor)
        {
            resolvedFloor = preferredFloor;
            if (IsCellAvailableOnFloor(cell, preferredFloor))
                return true;

            // Qualité de vie: depuis un étage supérieur, autoriser explicitement
            // le clic vers le sol extérieur (RDC), même si l'étage visé n'a pas
            // de cellule navigable à ces coordonnées.
            if (preferredFloor != 0 && IsGroundExteriorCell(cell) && IsCellAvailableOnFloor(cell, 0))
            {
                resolvedFloor = 0;
                return true;
            }

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
                if (floorViewMode == FloorViewMode.Manual)
                    manualFloorViewUntilSeconds = gameTime.TotalGameTime.TotalSeconds + ManualFloorViewHoldSeconds;
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
            _spriteBatch.DrawString(font, "Escaliers: balises orange/bleu sur la grille", new Vector2(10, 70), new Color(255, 190, 90));
            _spriteBatch.DrawString(font, $"Mode etage: {floorViewMode} | Ciblage: {(explicitUpperFloorTargeting ? "+1" : "Normal")}", new Vector2(10, 100), Color.LightBlue);

            string timeStr = GetTimeOfDayString(timeOfDay);
            _spriteBatch.DrawString(font, $"Heure: {timeStr} | Carte: {gridWidth}x{gridHeight}", new Vector2(10, 30), Color.Yellow);
            string floorLabel = viewedFloor == 0 ? "RDC" : viewedFloor > 0 ? $"+{viewedFloor}" : viewedFloor.ToString();
            int maxBasements = Math.Abs(GetMinimumViewFloor());
            _spriteBatch.DrawString(font, $"Etage affiche: {floorLabel} (Sous-sols: {maxBasements} | Etages: {Math.Max(1, currentMap?.FloorCount ?? 1)})", new Vector2(10, 50), Color.LightGreen);
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

        private void DrawWorld3D(GameTime gameTime)
        {
            UpdateDiscreetAntiOcclusionCamera();
            camera.UpdateCamera();
            renderer3D.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);
            renderer3D.SetLighting(ambientLight, directionalLight);

            GraphicsDevice.RasterizerState = new RasterizerState { CullMode = CullMode.None };
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            int floorCount = Math.Max(1, currentMap?.FloorCount ?? 1);
            int minFloor = GetMinimumViewFloor();

            bool useUpperFloorCutout = TryGetHoverOcclusionFocus(out Point focusCellForCutout, out int focusFloorForCutout);

            for (int floor = minFloor; floor < floorCount; floor++)
            {
                float yOffset = WorldMetrics.FloorToWorldY(floor, cellSize);
                bool applyUpperFloorCutout = useUpperFloorCutout && floor > focusFloorForCutout;

                if (floor == 0)
                {
                    renderer3D.DrawGridWithTerrain(gridWidth, gridHeight, cellSize, tileTexture, terrainHeights, yOffset);

                    if (sidewalkCells.Count > 0)
                        renderer3D.DrawTerrainCells(sidewalkCells, cellSize, sidewalkTexture, terrainHeights, yOffset + 0.005f);

                    if (roadCells.Count > 0)
                        renderer3D.DrawTerrainCells(roadCells, cellSize, asphaltTexture, terrainHeights, yOffset + 0.01f);
                }
                else
                {
                    var floorCells = GetCellsForFloor(floor);
                    if (applyUpperFloorCutout && floorCells.Count > 0)
                        floorCells = floorCells.Where(c => !IsPointInsideUpperFloorCutout(c, focusCellForCutout, UpperFloorCutoutRadius)).ToHashSet();

                    if (floorCells.Count > 0)
                        renderer3D.DrawGridCells(floorCells, cellSize, tileTexture, yOffset);
                }

                var hescoBarriersForFloor = GetHescoBarriersForFloor(floor);
                if (applyUpperFloorCutout && hescoBarriersForFloor.Count > 0)
                    hescoBarriersForFloor = hescoBarriersForFloor.Where(b => !IsPointInsideUpperFloorCutout(new Point(b.X, b.Y), focusCellForCutout, UpperFloorCutoutRadius)).ToList();

                if (hescoBarriersForFloor.Count > 0)
                    renderer3D.DrawHescoBarriers(hescoBarriersForFloor, cellSize, yOffset, hescoWallTexture);

                var furnituresForFloor = GetFurnitureForFloor(floor);
                if (applyUpperFloorCutout && furnituresForFloor.Count > 0)
                    furnituresForFloor = furnituresForFloor.Where(f => !IsPointInsideUpperFloorCutout(new Point(f.X, f.Y), focusCellForCutout, UpperFloorCutoutRadius)).ToList();

                if (furnituresForFloor.Count > 0)
                    renderer3D.DrawFurniture(furnituresForFloor, cellSize, yOffset);

                var wallsForFloor = GetWallsForFloor(floor);
                if (wallsForFloor.Count > 0)
                {
                    HashSet<WallSegment> renderedWalls = new HashSet<WallSegment>(wallsForFloor);

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

                        ComputeOcclusionFromWalls(renderedWalls, unitsOnFloor, yOffset, fadedWalls, new HashSet<Unit>());

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

                        renderer3D.DrawWalls(renderedWalls, cellSize, editorMode: false, floorHeightOffset: yOffset, brickWallTexture: wallTextureForFloor, hescoWallTexture: hescoWallTexture);

                        if (fadedWalls.Count > 0)
                            DrawWireframeWalls(fadedWalls, yOffset, new Color(245, 225, 140, 170));
                    }
                }

                renderer3D.DrawRampTiles(currentMap?.RampTiles, floor, cellSize);
                renderer3D.DrawStairConnections(currentMap?.StairConnections, floor, cellSize);
            }

            var visibleUnits = playerUnits.Where(u => u.Health > 0)
                .Concat(enemyUnits.Where(u => u.Health > 0 && IsEnemyVisibleToPlayers(u)))
                .ToList();

            DrawDeadUnitRemains();

            foreach (var unit in visibleUnits)
                renderer3D.DrawUnit(unit, cellSize);

            DrawActiveProjectiles3D();

            DrawAlliedTacticalFlashlightBeams(minFloor, floorCount);

            if (selectedUnit != null)
                renderer3D.DrawSelectionIndicator(selectedUnit, cellSize, new Color(0, 255, 255, 128));

            Unit target = combatUI.SelectedFireTarget ?? combatUI.HoveredFireTarget;
            if (target != null && (target.Team != Team.Enemy || IsEnemyVisibleToPlayers(target))) renderer3D.DrawSelectionIndicator(target, cellSize, new Color(255, 0, 0, 128), 1.2f);

            renderer3D.DrawCraters(craters, cellSize);
            renderer3D.DrawGrenades(activeGrenades, cellSize);
            DrawPlantedSatchelCharges3D(gameTime);
            DrawFlashlightLootHighlights(gameTime);

            DrawHoveredCell3D(gameTime);
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
                    (float)gameTime.TotalGameTime.TotalSeconds
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

            if (!throwMode && selectedUnit != null && selectedUnit.Team == Team.Player)
            {
                var zones = pathfinding.GetMovementZones(selectedUnit);
                renderer3D.DrawMovementZones(zones, cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds,
                    viewedFloor);
            }

            if (!throwMode && currentPathNodes.Count > 0 && selectedUnit != null && currentPathNodes.Any(n => n.Floor == viewedFloor))
            {
                BlendState previousBlend = GraphicsDevice.BlendState;
                DepthStencilState previousDepth = GraphicsDevice.DepthStencilState;

                GraphicsDevice.BlendState = BlendState.AlphaBlend;
                GraphicsDevice.DepthStencilState = DepthStencilState.None;

                renderer3D.DrawMovementPath(currentPathNodes, selectedUnit, cellSize,
                    (float)gameTime.TotalGameTime.TotalSeconds);

                GraphicsDevice.BlendState = previousBlend;
                GraphicsDevice.DepthStencilState = previousDepth;
            }

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

                renderer3D.DrawUnit(
                    remains.UnitSnapshot,
                    cellSize,
                    bodyColorOverride: new Color(100, 100, 100),
                    drawEquipment: true,
                    positionOverride: remains.Position,
                    modelRotationOverride: rotation);
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

            float pulse = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * 6f) * 0.3f + 0.7f;
            float floorYOffset = WorldMetrics.FloorToWorldY(viewedFloor, cellSize);

            float pulseBoost = 0.75f + pulse * 0.25f;
            renderer3D.DrawZoneOutline(new[] { hoveredCell }, cellSize, floorYOffset + 0.14f, new Color(255, 220, 90, 230) * pulseBoost);
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

            List<Point> playerSpawnCells = missionType == "Centre-Ville" || missionType == "Sabotage"
                ? GetCityCenterSpawnCells(6)
                : Enumerable.Range(0, 6).Select(i => new Point(2 + i, gridHeight - 2)).ToList();

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
                        var edgeSpawns = GetPerimeterSpawnCells(40);

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
            }

            DistributeEnemiesAcrossUpperFloors();

            AssignRandomPants(enemyUnits);
            AssignRandomEquipmentToUnits(enemyUnits);
            AssignRandomInventoryToUnits(enemyUnits);
            EnsureUnitsHaveCompatibleMagazines(enemyUnits, minimumMagazinesPerUnit: 2);

            foreach (var unit in playerUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }
            foreach (var unit in enemyUnits) { unit.UpdateVisualPosition(cellSize); unit.TargetPosition = unit.VisualPosition; }

            Console.WriteLine($"Units created for {missionType}: 6 player, {enemyUnits.Count} enemy");
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

        private void DistributeEnemiesAcrossUpperFloors()
        {
            int maxFloor = Math.Max(0, (currentMap?.FloorCount ?? 1) - 1);
            if (maxFloor <= 0 || enemyUnits.Count == 0)
                return;

            var upperFloorAssignments = new List<(Point Cell, int Floor)>();
            var occupied = new HashSet<(int Floor, Point Cell)>();

            foreach (var player in playerUnits)
                occupied.Add((player.Floor, player.Cell));

            foreach (var enemy in enemyUnits)
                occupied.Add((enemy.Floor, enemy.Cell));

            for (int floor = 1; floor <= maxFloor; floor++)
            {
                var floorCells = GetCellsForFloor(floor)
                    .Where(c => !occupied.Contains((floor, c)))
                    .ToList();

                if (floorCells.Count == 0)
                    continue;

                for (int i = floorCells.Count - 1; i > 0; i--)
                {
                    int swapIndex = random.Next(i + 1);
                    (floorCells[i], floorCells[swapIndex]) = (floorCells[swapIndex], floorCells[i]);
                }

                int desiredForFloor = Math.Max(1, enemyUnits.Count / (maxFloor + 2));
                int assignCount = Math.Min(desiredForFloor, floorCells.Count);
                for (int i = 0; i < assignCount; i++)
                {
                    var cell = floorCells[i];
                    upperFloorAssignments.Add((cell, floor));
                    occupied.Add((floor, cell));
                }
            }

            if (upperFloorAssignments.Count == 0)
                return;

            var movableEnemies = enemyUnits.OrderBy(_ => random.Next()).ToList();
            int moved = Math.Min(upperFloorAssignments.Count, movableEnemies.Count);

            for (int i = 0; i < moved; i++)
            {
                var enemy = movableEnemies[i];
                var assignment = upperFloorAssignments[i];
                enemy.Cell = assignment.Cell;
                enemy.Floor = assignment.Floor;
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

            for (int i = 0; i < count; i++)
            {
                int swapIndex = random.Next(i, perimeter.Count);
                (perimeter[i], perimeter[swapIndex]) = (perimeter[swapIndex], perimeter[i]);
            }

            return perimeter.Take(count).ToList();
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
                f.X == cell.X &&
                f.Y == cell.Y &&
                IsMovementBlockingFurnitureType(f.Type));
        }

        private static bool IsMovementBlockingFurnitureType(FurnitureType type)
        {
            return type is
                FurnitureType.SedanToyotaCorolla or
                FurnitureType.SedanBmwSeries3 or
                FurnitureType.SedanMercedesEClass or
                FurnitureType.PickupToyotaTacoma or
                FurnitureType.PickupFordF150 or
                FurnitureType.PickupRam3500;
        }

        private bool IsCellHoverableOnViewedFloor(Point cell, int floor)
        {
            if (IsCellAvailableOnFloor(cell, floor))
                return true;

            // Les cellules extérieures au sol restent valides pour le ciblage/mouvement
            // seulement au rez-de-chaussée. En étage, on évite le "survol dans le vide"
            // tant qu'aucune mécanique d'unités volantes dédiée n'est implémentée.
            return floor == 0 && IsGroundExteriorCell(cell);
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

        FurnitureData GetFurnitureAtCellOnFloor(Point cell, int floor)
        {
            if (currentMap?.Furnitures == null)
                return null;

            return currentMap.Furnitures.FirstOrDefault(f =>
                f.Floor == floor &&
                f.X == cell.X &&
                f.Y == cell.Y);
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
            EnsurePremadeMapsGenerated();
            PlayGameplaySongForMission(missionType);
            currentState = GameState.Playing;

            // ✅ NOUVEAU : Charger une carte (générée aléatoirement)
            LoadMap(); // Génère automatiquement une carte selon selectedMission

            CreateUnits(missionType);
            floorViewMode = FloorViewMode.AutoFollow;
            manualFloorViewUntilSeconds = 0d;
            explicitUpperFloorTargeting = false;
            wallSegments = currentMap.GetWalls();
            shatteredWindows.Clear();
            InvalidateWallsByFloorCache();
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

            isHoveringValidCell = rawHoveredCell.X != -1 && IsCellHoverableOnViewedFloor(rawHoveredCell, interactionFloor);
            if (isHoveringValidCell)
                hoveredCell = rawHoveredCell;

            // 1. Check if we have a valid unit and valid cell
            if (!grappleMode && !c4PlacementMode && selectedUnit != null && selectedUnit.ActionPoints > 0 && isHoveringValidCell &&
                selectedUnit.Team == Team.Player)
            {
                // 2. Define maxRange here
                int maxRange = selectedUnit.CanSprint() ?
                    selectedUnit.GetSprintRange() : selectedUnit.GetMaxMoveRange();

                // 3. ONLY recalculate the path if the mouse moved to a new cell
                if (hoveredCell != lastHoveredCell)
                {
                    Point previewGoal = hoveredCell;
                    int previewFloor = interactionFloor;
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
            if (leftClick && !clickOnUI && !throwMode && !c4PlacementMode && !grappleMode && isHoveringValidCell) HandleGridClick(hoveredCell, interactionFloor, allowSmartFallback: !explicitUpperFloorTargeting);

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
