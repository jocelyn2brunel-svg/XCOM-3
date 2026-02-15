using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    /// <summary>
    /// Éditeur de carte visuel avec outils de dessin
    /// </summary>
    public class MapEditor
    {
        // État de l'éditeur
        public MapData CurrentMap { get; private set; }
        public bool IsActive { get; private set; }

        // Outils d'édition
        public enum EditorTool
        {
            Select,         // Sélectionner/déplacer
            Wall,           // Dessiner des murs
            EraseWall,      // Effacer des murs
            PlayerSpawn,    // Placer zone spawn joueur
            EnemySpawn,     // Placer zone spawn ennemi
            Objective       // Placer objectif
        }

        public EditorTool CurrentTool { get; set; } = EditorTool.Wall;

        // État du dessin
        private bool isDrawing = false;
        private Point dragStart = Point.Zero;
        private Point hoveredCell = new Point(-1, -1);

        // Caméra et rendu
        private CameraController camera;
        private Renderer3D renderer;
        private int cellSize;

        // ✅ NOUVEAU : Viewport size pour calcul précis
        private int viewportWidth = 1280;
        private int viewportHeight = 720;

        // UI
        private SpriteFont font;
        private Texture2D pixel;
        private SpriteBatch spriteBatch;
        private List<Button> toolButtons = new List<Button>();
        private List<Button> actionButtons = new List<Button>();

        // Historique (undo/redo)
        private Stack<MapData> undoStack = new Stack<MapData>();
        private Stack<MapData> redoStack = new Stack<MapData>();
        private const int MaxUndoSteps = 50;

        public MapEditor(CameraController camera, Renderer3D renderer,
                         SpriteFont font, Texture2D pixel, SpriteBatch spriteBatch)
        {
            this.camera = camera;
            this.renderer = renderer;
            this.font = font;
            this.pixel = pixel;
            this.spriteBatch = spriteBatch;
            this.cellSize = 2;

            InitializeButtons();
        }

        /// <summary>
        /// Met à jour la taille du viewport (appeler quand la fenêtre change de taille)
        /// </summary>
        public void UpdateViewportSize(int width, int height)
        {
            viewportWidth = width;
            viewportHeight = height;
            camera?.UpdateProjection(GetViewportAspectRatio());
            Console.WriteLine($"[MAP EDITOR] Viewport updated: {width}x{height}");
        }

        private void InitializeButtons()
        {
            int y = 60;
            int spacing = 35;

            toolButtons.Add(new Button("Wall", new Vector2(10, y))); y += spacing;
            toolButtons.Add(new Button("Erase", new Vector2(10, y))); y += spacing;
            toolButtons.Add(new Button("Player Spawn", new Vector2(10, y))); y += spacing;
            toolButtons.Add(new Button("Enemy Spawn", new Vector2(10, y))); y += spacing;

            y += 20;
            actionButtons.Add(new Button("Save", new Vector2(10, y))); y += spacing;
            actionButtons.Add(new Button("Load", new Vector2(10, y))); y += spacing;
            actionButtons.Add(new Button("Clear", new Vector2(10, y))); y += spacing;
            actionButtons.Add(new Button("Exit", new Vector2(10, y)));
        }

        /// <summary>
        /// Démarre l'édition d'une nouvelle carte
        /// </summary>
        public void StartNewMap(int width, int height)
        {
            MapGenerator generator = new MapGenerator(new Random());
            CurrentMap = generator.GenerateEmptyMap(width, height, "New Map");

            cellSize = CurrentMap.CellSize;
            camera = new CameraController(width, height, cellSize, GetViewportAspectRatio());

            IsActive = true;
            ClearHistory();

            Console.WriteLine($"[MAP EDITOR] Started editing new map: {width}x{height}");
        }

        /// <summary>
        /// Charge une carte existante pour édition
        /// </summary>
        public void LoadMap(MapData map)
        {
            CurrentMap = map;
            cellSize = map.CellSize;
            camera = new CameraController(map.GridWidth, map.GridHeight, cellSize, GetViewportAspectRatio());

            IsActive = true;
            ClearHistory();

            Console.WriteLine($"[MAP EDITOR] Loaded map: {map.Name}");
        }

        /// <summary>
        /// Quitte l'éditeur
        /// </summary>
        public void Exit()
        {
            IsActive = false;
            Console.WriteLine("[MAP EDITOR] Exited");
        }

        /// <summary>
        /// Update de l'éditeur
        /// </summary>
        public void Update(GameTime gameTime, MouseState mouse, KeyboardState keyboard,
                          KeyboardState prevKeyboard, MouseState prevMouse,
                          int viewportWidth, int viewportHeight)
        {
            if (!IsActive || CurrentMap == null) return;

            // ✅ Mettre à jour la taille du viewport
            this.viewportWidth = viewportWidth;
            this.viewportHeight = viewportHeight;
            camera?.UpdateProjection(GetViewportAspectRatio());

            // Contrôles caméra
            camera.HandleControls(keyboard, mouse, prevMouse, gameTime);

            // ✅ Obtenir la cellule survolée avec les bonnes dimensions
            hoveredCell = camera.GetCellFromMouse(mouse.Position, viewportWidth, viewportHeight);

            // Debug: afficher les coordonnées pour vérifier
            if (hoveredCell.X >= 0 && hoveredCell.Y >= 0 &&
                hoveredCell.X < CurrentMap.GridWidth && hoveredCell.Y < CurrentMap.GridHeight)
            {
                // Position valide
            }
            else
            {
                // Hors de la grille
                hoveredCell = new Point(-1, -1);
            }

            // Gestion des outils
            HandleToolSelection(mouse, prevMouse);
            HandleToolUsage(mouse, prevMouse, keyboard, prevKeyboard);

            // Raccourcis clavier
            HandleKeyboardShortcuts(keyboard, prevKeyboard);
        }

        private void HandleToolSelection(MouseState mouse, MouseState prevMouse)
        {
            if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
            {
                foreach (var btn in toolButtons)
                {
                    if (btn.IsClicked(mouse, prevMouse))
                    {
                        CurrentTool = btn.Text switch
                        {
                            "Wall" => EditorTool.Wall,
                            "Erase" => EditorTool.EraseWall,
                            "Player Spawn" => EditorTool.PlayerSpawn,
                            "Enemy Spawn" => EditorTool.EnemySpawn,
                            _ => CurrentTool
                        };
                        Console.WriteLine($"[MAP EDITOR] Tool: {CurrentTool}");
                        return;
                    }
                }

                foreach (var btn in actionButtons)
                {
                    if (btn.IsClicked(mouse, prevMouse))
                    {
                        HandleAction(btn.Text);
                        return;
                    }
                }
            }
        }

        private void HandleToolUsage(MouseState mouse, MouseState prevMouse,
                                     KeyboardState keyboard, KeyboardState prevKeyboard)
        {
            bool leftClick = mouse.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftClick && prevMouse.LeftButton == ButtonState.Released;
            bool leftJustReleased = mouse.LeftButton == ButtonState.Released && prevMouse.LeftButton == ButtonState.Pressed;

            // ✅ Ne pas dessiner sur l'UI (panneau de gauche élargi à 220)
            if (mouse.X < 220) return;

            if (hoveredCell.X < 0 || hoveredCell.Y < 0 ||
                hoveredCell.X >= CurrentMap.GridWidth || hoveredCell.Y >= CurrentMap.GridHeight)
                return;

            switch (CurrentTool)
            {
                case EditorTool.Wall:
                case EditorTool.EraseWall:
                    HandleWallTool(leftJustPressed, leftClick, leftJustReleased);
                    break;

                case EditorTool.PlayerSpawn:
                case EditorTool.EnemySpawn:
                    if (leftJustPressed)
                        HandleSpawnZoneTool();
                    break;
            }
        }

        private void HandleWallTool(bool justPressed, bool held, bool released)
        {
            if (justPressed)
            {
                isDrawing = true;
                dragStart = hoveredCell;
                SaveToUndoStack();
                Console.WriteLine($"[MAP EDITOR] Start drawing from ({dragStart.X}, {dragStart.Y})");
            }

            if (held && isDrawing)
            {
                // Dessiner un mur entre dragStart et hoveredCell
                DrawWallLine(dragStart, hoveredCell, CurrentTool == EditorTool.Wall);
            }

            if (released)
            {
                isDrawing = false;
                Console.WriteLine($"[MAP EDITOR] Finished drawing to ({hoveredCell.X}, {hoveredCell.Y})");
            }
        }

        private void DrawWallLine(Point start, Point end, bool addWall)
        {
            // Détecter direction (horizontal ou vertical prioritaire)
            int dx = Math.Abs(end.X - start.X);
            int dy = Math.Abs(end.Y - start.Y);

            if (dx > dy)
            {
                // Ligne horizontale
                int y = start.Y;
                int minX = Math.Min(start.X, end.X);
                int maxX = Math.Max(start.X, end.X);

                for (int x = minX; x < maxX; x++)
                {
                    ModifyWall(x, y, x + 1, y, true, addWall);
                }
            }
            else
            {
                // Ligne verticale
                int x = start.X;
                int minY = Math.Min(start.Y, end.Y);
                int maxY = Math.Max(start.Y, end.Y);

                for (int y = minY; y < maxY; y++)
                {
                    ModifyWall(x, y, x, y + 1, false, addWall);
                }
            }
        }

        private void ModifyWall(int x1, int y1, int x2, int y2, bool isHorizontal, bool add)
        {
            WallSegment wall = new WallSegment(new Point(x1, y1), new Point(x2, y2), isHorizontal);

            var walls = CurrentMap.GetWalls();

            if (add)
            {
                if (!walls.Contains(wall))
                {
                    walls.Add(wall);
                    CurrentMap.SetWalls(walls);
                }
            }
            else
            {
                if (walls.Remove(wall))
                {
                    CurrentMap.SetWalls(walls);
                }
            }
        }

        private void HandleSpawnZoneTool()
        {
            // Place une petite zone de spawn 3x3 centrée sur la cellule
            SpawnZone zone = new SpawnZone
            {
                MinX = Math.Max(0, hoveredCell.X - 1),
                MinY = Math.Max(0, hoveredCell.Y - 1),
                MaxX = Math.Min(CurrentMap.GridWidth - 1, hoveredCell.X + 1),
                MaxY = Math.Min(CurrentMap.GridHeight - 1, hoveredCell.Y + 1),
                MaxUnits = 6
            };

            SaveToUndoStack();

            if (CurrentTool == EditorTool.PlayerSpawn)
            {
                CurrentMap.PlayerSpawnZones.Clear();
                CurrentMap.PlayerSpawnZones.Add(zone);
                Console.WriteLine($"[MAP EDITOR] Set player spawn at ({hoveredCell.X}, {hoveredCell.Y})");
            }
            else
            {
                CurrentMap.EnemySpawnZones.Clear();
                CurrentMap.EnemySpawnZones.Add(zone);
                Console.WriteLine($"[MAP EDITOR] Set enemy spawn at ({hoveredCell.X}, {hoveredCell.Y})");
            }
        }

        private void HandleKeyboardShortcuts(KeyboardState keyboard, KeyboardState prevKeyboard)
        {
            // Ctrl+Z = Undo
            if (keyboard.IsKeyDown(Keys.LeftControl) && keyboard.IsKeyDown(Keys.Z) &&
                prevKeyboard.IsKeyUp(Keys.Z))
            {
                Undo();
            }

            // Ctrl+Y = Redo
            if (keyboard.IsKeyDown(Keys.LeftControl) && keyboard.IsKeyDown(Keys.Y) &&
                prevKeyboard.IsKeyUp(Keys.Y))
            {
                Redo();
            }

            // Ctrl+S = Save
            if (keyboard.IsKeyDown(Keys.LeftControl) && keyboard.IsKeyDown(Keys.S) &&
                prevKeyboard.IsKeyUp(Keys.S))
            {
                HandleAction("Save");
            }

            // 1-4 = Sélection rapide d'outils
            if (keyboard.IsKeyDown(Keys.D1) && prevKeyboard.IsKeyUp(Keys.D1)) CurrentTool = EditorTool.Wall;
            if (keyboard.IsKeyDown(Keys.D2) && prevKeyboard.IsKeyUp(Keys.D2)) CurrentTool = EditorTool.EraseWall;
            if (keyboard.IsKeyDown(Keys.D3) && prevKeyboard.IsKeyUp(Keys.D3)) CurrentTool = EditorTool.PlayerSpawn;
            if (keyboard.IsKeyDown(Keys.D4) && prevKeyboard.IsKeyUp(Keys.D4)) CurrentTool = EditorTool.EnemySpawn;
        }

        private void HandleAction(string action)
        {
            switch (action)
            {
                case "Save":
                    MapCatalog.SaveMap(CurrentMap);
                    Console.WriteLine($"[MAP EDITOR] Saved: {CurrentMap.Name}");
                    break;

                case "Load":
                    // TODO: UI de sélection de carte
                    Console.WriteLine("[MAP EDITOR] Load UI not implemented yet");
                    break;

                case "Clear":
                    SaveToUndoStack();
                    CurrentMap.SetWalls(new HashSet<WallSegment>());
                    Console.WriteLine("[MAP EDITOR] Cleared all walls");
                    break;

                case "Exit":
                    Exit();
                    break;
            }
        }

        private void SaveToUndoStack()
        {
            // Sérialiser la carte actuelle pour l'undo
            string json = System.Text.Json.JsonSerializer.Serialize(CurrentMap);
            MapData copy = System.Text.Json.JsonSerializer.Deserialize<MapData>(json);

            undoStack.Push(copy);
            redoStack.Clear();

            if (undoStack.Count > MaxUndoSteps)
            {
                // Limiter la taille de l'historique
                var temp = undoStack.ToArray();
                undoStack.Clear();
                for (int i = 0; i < MaxUndoSteps; i++)
                    undoStack.Push(temp[i]);
            }
        }

        private void Undo()
        {
            if (undoStack.Count == 0)
            {
                Console.WriteLine("[MAP EDITOR] Nothing to undo");
                return;
            }

            string currentJson = System.Text.Json.JsonSerializer.Serialize(CurrentMap);
            MapData currentCopy = System.Text.Json.JsonSerializer.Deserialize<MapData>(currentJson);
            redoStack.Push(currentCopy);

            CurrentMap = undoStack.Pop();
            Console.WriteLine("[MAP EDITOR] Undo");
        }

        private void Redo()
        {
            if (redoStack.Count == 0)
            {
                Console.WriteLine("[MAP EDITOR] Nothing to redo");
                return;
            }

            string currentJson = System.Text.Json.JsonSerializer.Serialize(CurrentMap);
            MapData currentCopy = System.Text.Json.JsonSerializer.Deserialize<MapData>(currentJson);
            undoStack.Push(currentCopy);

            CurrentMap = redoStack.Pop();
            Console.WriteLine("[MAP EDITOR] Redo");
        }

        private void ClearHistory()
        {
            undoStack.Clear();
            redoStack.Clear();
        }

        /// <summary>
        /// Rendu 3D de l'éditeur
        /// </summary>
        public void Draw3D(GameTime gameTime)
        {
            if (!IsActive || CurrentMap == null) return;

            camera.UpdateCamera();
            renderer.SetMatrices(camera.ViewMatrix, camera.ProjectionMatrix);
            renderer.SetLighting(Color.White, Color.White);

            // ✅ GRILLE VISIBLE - lignes épaisses tous les 5 cases
            DrawVisibleGrid(CurrentMap.GridWidth, CurrentMap.GridHeight);

            // Grille fine de base
            renderer.DrawGrid(CurrentMap.GridWidth, CurrentMap.GridHeight, cellSize, null);

            // Murs
            renderer.DrawWalls(CurrentMap.GetWalls(), cellSize, editorMode: true);

            // Zones de spawn
            DrawSpawnZones(CurrentMap.PlayerSpawnZones, new Color(0, 255, 0, 100));
            DrawSpawnZones(CurrentMap.EnemySpawnZones, new Color(255, 0, 0, 100));

            // Ligne de drag en cours
            if (isDrawing && CurrentTool == EditorTool.Wall)
            {
                DrawDragPreview();
            }

            // Cellule survolée
            if (hoveredCell.X >= 0 && hoveredCell.Y >= 0)
            {
                Vector3 pos = new Vector3(
                    hoveredCell.X * cellSize + cellSize / 2f,
                    0.1f,
                    hoveredCell.Y * cellSize + cellSize / 2f
                );

                Color hoverColor = CurrentTool == EditorTool.Wall ? new Color(0, 255, 0, 180) :
                                   CurrentTool == EditorTool.EraseWall ? new Color(255, 0, 0, 180) :
                                   new Color(255, 255, 0, 180);

                renderer.DrawPlane(pos, new Vector3(cellSize * 0.95f, 1, cellSize * 0.95f), hoverColor);
            }
        }

        /// <summary>
        /// Dessine une grille visible avec lignes épaisses tous les 5 cases
        /// </summary>
        private void DrawVisibleGrid(int width, int height)
        {
            // Lignes verticales tous les 5 cases
            for (int x = 0; x <= width; x += 5)
            {
                for (int z = 0; z < height; z++)
                {
                    Vector3 start = new Vector3(x * cellSize, 0.02f, z * cellSize);
                    Vector3 end = new Vector3(x * cellSize, 0.02f, (z + 1) * cellSize);

                    // Ligne épaisse (cube très fin)
                    Vector3 mid = (start + end) / 2f;
                    Vector3 dir = end - start;
                    float length = dir.Length();

                    renderer.DrawCube(mid, new Vector3(0.1f, 0.05f, length), new Color(200, 200, 200, 255));
                }
            }

            // Lignes horizontales tous les 5 cases
            for (int z = 0; z <= height; z += 5)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 start = new Vector3(x * cellSize, 0.02f, z * cellSize);
                    Vector3 end = new Vector3((x + 1) * cellSize, 0.02f, z * cellSize);

                    Vector3 mid = (start + end) / 2f;
                    Vector3 dir = end - start;
                    float length = dir.Length();

                    renderer.DrawCube(mid, new Vector3(length, 0.05f, 0.1f), new Color(200, 200, 200, 255));
                }
            }

            // Bordures de la carte (lignes jaunes épaisses)
            // Bord gauche
            for (int z = 0; z < height; z++)
            {
                Vector3 pos = new Vector3(0, 0.03f, z * cellSize + cellSize / 2f);
                renderer.DrawCube(pos, new Vector3(0.15f, 0.06f, cellSize), Color.Yellow);
            }

            // Bord droit
            for (int z = 0; z < height; z++)
            {
                Vector3 pos = new Vector3(width * cellSize, 0.03f, z * cellSize + cellSize / 2f);
                renderer.DrawCube(pos, new Vector3(0.15f, 0.06f, cellSize), Color.Yellow);
            }

            // Bord haut
            for (int x = 0; x < width; x++)
            {
                Vector3 pos = new Vector3(x * cellSize + cellSize / 2f, 0.03f, 0);
                renderer.DrawCube(pos, new Vector3(cellSize, 0.06f, 0.15f), Color.Yellow);
            }

            // Bord bas
            for (int x = 0; x < width; x++)
            {
                Vector3 pos = new Vector3(x * cellSize + cellSize / 2f, 0.03f, height * cellSize);
                renderer.DrawCube(pos, new Vector3(cellSize, 0.06f, 0.15f), Color.Yellow);
            }
        }

        /// <summary>
        /// Dessine la preview du mur en cours de création
        /// </summary>
        private void DrawDragPreview()
        {
            if (dragStart.X < 0 || hoveredCell.X < 0) return;

            int dx = Math.Abs(hoveredCell.X - dragStart.X);
            int dy = Math.Abs(hoveredCell.Y - dragStart.Y);

            if (dx > dy)
            {
                // Ligne horizontale
                int y = dragStart.Y;
                int minX = Math.Min(dragStart.X, hoveredCell.X);
                int maxX = Math.Max(dragStart.X, hoveredCell.X);

                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 pos = new Vector3(x * cellSize, 0.5f, y * cellSize);
                    renderer.DrawCube(pos, new Vector3(cellSize * 0.1f, 1f, 0.1f), new Color(0, 255, 0, 150));
                }
            }
            else
            {
                // Ligne verticale
                int x = dragStart.X;
                int minY = Math.Min(dragStart.Y, hoveredCell.Y);
                int maxY = Math.Max(dragStart.Y, hoveredCell.Y);

                for (int y = minY; y <= maxY; y++)
                {
                    Vector3 pos = new Vector3(x * cellSize, 0.5f, y * cellSize);
                    renderer.DrawCube(pos, new Vector3(0.1f, 1f, cellSize * 0.1f), new Color(0, 255, 0, 150));
                }
            }
        }

        private void DrawSpawnZones(List<SpawnZone> zones, Color color)
        {
            foreach (var zone in zones)
            {
                for (int x = zone.MinX; x <= zone.MaxX; x++)
                {
                    for (int y = zone.MinY; y <= zone.MaxY; y++)
                    {
                        Vector3 pos = new Vector3(
                            x * cellSize + cellSize / 2f,
                            0.05f,
                            y * cellSize + cellSize / 2f
                        );
                        renderer.DrawPlane(pos, new Vector3(cellSize * 0.8f, 1, cellSize * 0.8f), color);
                    }
                }
            }
        }

        /// <summary>
        /// Rendu UI 2D de l'éditeur
        /// </summary>
        public void DrawUI(MouseState mouse)
        {
            if (!IsActive || CurrentMap == null) return;

            // Panneau de gauche
            Rectangle panel = new Rectangle(0, 0, 220, 720);
            spriteBatch.Draw(pixel, panel, new Color(30, 30, 30, 240));

            // Bordure
            DrawBorder(panel, Color.Yellow, 2);

            // Titre
            spriteBatch.DrawString(font, "MAP EDITOR", new Vector2(15, 10), Color.Yellow,
                                  0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);

            // Outil actuel (grand et visible)
            Rectangle toolBox = new Rectangle(10, 40, 200, 30);
            spriteBatch.Draw(pixel, toolBox, new Color(50, 50, 50));
            DrawBorder(toolBox, Color.Cyan, 2);

            string toolText = CurrentTool.ToString();
            spriteBatch.DrawString(font, $"Tool: {toolText}", new Vector2(15, 45), Color.Cyan,
                                  0f, Vector2.Zero, 1.1f, SpriteEffects.None, 0f);

            // ✅ NOUVEAU : Afficher les coordonnées de la cellule survolée
            if (hoveredCell.X >= 0 && hoveredCell.Y >= 0)
            {
                Rectangle coordBox = new Rectangle(10, 75, 200, 25);
                spriteBatch.Draw(pixel, coordBox, new Color(40, 80, 40));
                spriteBatch.DrawString(font, $"Cell: ({hoveredCell.X}, {hoveredCell.Y})",
                                      new Vector2(15, 80), Color.LightGreen);
            }

            // Boutons d'outils
            int btnY = 110;
            foreach (var btn in toolButtons)
            {
                Rectangle btnRect = new Rectangle(10, btnY, 200, 30);

                bool isActive = (btn.Text == "Wall" && CurrentTool == EditorTool.Wall) ||
                               (btn.Text == "Erase" && CurrentTool == EditorTool.EraseWall) ||
                               (btn.Text == "Player Spawn" && CurrentTool == EditorTool.PlayerSpawn) ||
                               (btn.Text == "Enemy Spawn" && CurrentTool == EditorTool.EnemySpawn);

                Color btnBg = isActive ? new Color(0, 100, 0) : new Color(50, 50, 50);
                Color btnText = isActive ? Color.Yellow : Color.White;

                spriteBatch.Draw(pixel, btnRect, btnBg);
                DrawBorder(btnRect, isActive ? Color.Yellow : Color.Gray, 2);

                spriteBatch.DrawString(font, btn.Text, new Vector2(15, btnY + 5), btnText);

                btnY += 35;
            }

            // Séparateur
            btnY += 10;
            spriteBatch.Draw(pixel, new Rectangle(10, btnY, 200, 2), Color.Gray);
            btnY += 15;

            // Boutons d'action
            foreach (var btn in actionButtons)
            {
                Rectangle btnRect = new Rectangle(10, btnY, 200, 30);

                Color btnBg = btnRect.Contains(mouse.Position) ? new Color(80, 80, 80) : new Color(50, 50, 50);

                spriteBatch.Draw(pixel, btnRect, btnBg);
                DrawBorder(btnRect, Color.White, 1);

                Color textColor = btn.Text == "Save" ? Color.LightGreen :
                                 btn.Text == "Exit" ? Color.Orange : Color.White;

                spriteBatch.DrawString(font, btn.Text, new Vector2(15, btnY + 5), textColor);

                btnY += 35;
            }

            // Info carte
            int infoY = 450;
            spriteBatch.DrawString(font, "--- Map Info ---", new Vector2(15, infoY), Color.Cyan);
            infoY += 25;

            spriteBatch.DrawString(font, $"Name: {CurrentMap.Name}",
                                  new Vector2(15, infoY), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            infoY += 20;

            spriteBatch.DrawString(font, $"Size: {CurrentMap.GridWidth} x {CurrentMap.GridHeight}",
                                  new Vector2(15, infoY), Color.White, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
            infoY += 22;

            spriteBatch.DrawString(font, $"Walls: {CurrentMap.Walls.Count}",
                                  new Vector2(15, infoY), new Color(255, 200, 100), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
            infoY += 22;

            spriteBatch.DrawString(font, $"P.Spawn: {CurrentMap.PlayerSpawnZones.Count}",
                                  new Vector2(15, infoY), new Color(100, 255, 100));
            infoY += 20;

            spriteBatch.DrawString(font, $"E.Spawn: {CurrentMap.EnemySpawnZones.Count}",
                                  new Vector2(15, infoY), new Color(255, 100, 100));
            infoY += 30;

            // Raccourcis
            spriteBatch.DrawString(font, "--- Controls ---", new Vector2(15, infoY), Color.Cyan);
            infoY += 25;

            DrawKeyHint("1", "Wall Tool", infoY); infoY += 18;
            DrawKeyHint("2", "Erase Tool", infoY); infoY += 18;
            DrawKeyHint("3", "Player Spawn", infoY); infoY += 18;
            DrawKeyHint("4", "Enemy Spawn", infoY); infoY += 18;
            infoY += 5;
            DrawKeyHint("Ctrl+Z", "Undo", infoY); infoY += 18;
            DrawKeyHint("Ctrl+Y", "Redo", infoY); infoY += 18;
            DrawKeyHint("Ctrl+S", "Save", infoY); infoY += 18;
            DrawKeyHint("Q/E", "Rotate Camera", infoY); infoY += 18;
            DrawKeyHint("Scroll", "Zoom", infoY);
        }

        private void DrawKeyHint(string key, string action, int y)
        {
            // Touche
            Vector2 keySize = font.MeasureString(key);
            Rectangle keyBox = new Rectangle(15, y, (int)keySize.X + 8, 16);
            spriteBatch.Draw(pixel, keyBox, new Color(70, 70, 70));
            DrawBorder(keyBox, Color.Gray, 1);
            spriteBatch.DrawString(font, key, new Vector2(19, y), Color.Yellow,
                                  0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

            // Action
            spriteBatch.DrawString(font, action, new Vector2(keyBox.Right + 8, y), Color.LightGray,
                                  0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }

        private float GetViewportAspectRatio()
        {
            return viewportHeight <= 0 ? 16f / 9f : (float)viewportWidth / viewportHeight;
        }

        private void DrawBorder(Rectangle rect, Color color, int thickness)
        {
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
