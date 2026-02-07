using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace XCOM_3
{
    /// <summary>
    /// Gère la caméra 3D isométrique avec rotation, zoom et déplacement
    /// </summary>
    public class CameraController
    {
        // ═══════════════════════════════════════════════════════════════════════
        // PROPRIÉTÉS PUBLIQUES
        // ═══════════════════════════════════════════════════════════════════════

        public Vector3 Position { get; private set; }
        public Vector3 Target { get; private set; }
        public Matrix ViewMatrix { get; private set; }
        public Matrix ProjectionMatrix { get; private set; }
        public BoundingFrustum ViewFrustum { get; private set; }

        // ═══════════════════════════════════════════════════════════════════════
        // PARAMÈTRES DE CAMÉRA
        // ═══════════════════════════════════════════════════════════════════════

        private float cameraAngle = MathHelper.PiOver4;
        private float cameraDistance = 30f;
        private float cameraHeight = 20f;
        private Vector2 cameraOffset = Vector2.Zero;

        // Vitesses
        private float cameraRotationSpeed = 1.5f;
        private float cameraMoveSpeed = 35f;

        // Zoom
        private float zoomLevel = 1f;
        private float minZoom = 0.3f;
        private float maxZoom = 3f;
        private float previousScrollValue = 0f;

        // Dimensions de la grille
        private int gridWidth;
        private int gridHeight;
        private int cellSize;

        // ═══════════════════════════════════════════════════════════════════════
        // CONSTRUCTEUR
        // ═══════════════════════════════════════════════════════════════════════

        public CameraController(int gridWidth, int gridHeight, int cellSize, float aspectRatio)
        {
            this.gridWidth = gridWidth;
            this.gridHeight = gridHeight;
            this.cellSize = cellSize;

            InitializeProjection(aspectRatio);
            UpdateCamera();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // INITIALISATION
        // ═══════════════════════════════════════════════════════════════════════

        private void InitializeProjection(float aspectRatio)
        {
            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f),
                aspectRatio,
                0.1f,
                1000f
            );
        }

        /// <summary>
        /// Met à jour la matrice de projection (appelé lors du redimensionnement)
        /// </summary>
        public void UpdateProjection(float aspectRatio)
        {
            InitializeProjection(aspectRatio);
            UpdateCamera(); // Recalculer le frustum
        }

        /// <summary>
        /// Réinitialise la caméra pour une nouvelle grille
        /// </summary>
        public void SetGrid(int width, int height, int size)
        {
            gridWidth = width;
            gridHeight = height;
            cellSize = size;

            // Réinitialiser position
            cameraOffset = Vector2.Zero;
            cameraAngle = MathHelper.PiOver4;
            zoomLevel = 1f;

            UpdateCamera();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UPDATE - Appelé chaque frame
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Met à jour les matrices de vue et de frustum
        /// </summary>
        public void UpdateCamera()
        {
            // Centre de la grille
            float centerX = (gridWidth * cellSize) / 2f + cameraOffset.X;
            float centerZ = (gridHeight * cellSize) / 2f + cameraOffset.Y;

            Target = new Vector3(centerX, 0, centerZ);

            // Appliquer le zoom
            float adjustedDistance = cameraDistance / zoomLevel;
            float adjustedHeight = cameraHeight / zoomLevel;

            // Position de la caméra en orbite autour du centre
            Position = new Vector3(
                centerX + (float)Math.Cos(cameraAngle) * adjustedDistance,
                adjustedHeight,
                centerZ + (float)Math.Sin(cameraAngle) * adjustedDistance
            );

            // Matrice de vue
            ViewMatrix = Matrix.CreateLookAt(Position, Target, Vector3.Up);

            // Frustum culling
            ViewFrustum = new BoundingFrustum(ViewMatrix * ProjectionMatrix);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONTRÔLES - Gestion des inputs
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gère les contrôles de la caméra (clavier + souris)
        /// </summary>
        public void HandleControls(KeyboardState keyboard, MouseState mouse, MouseState previousMouse, GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // ═══ ROTATION (Q/E) ═══
            HandleRotation(keyboard, deltaTime);

            // ═══ ZOOM (Molette) ═══
            HandleZoom(mouse);

            // ═══ DÉPLACEMENT (WASD + Clic molette) ═══
            HandleMovement(keyboard, mouse, previousMouse, deltaTime);

            // Mettre à jour les matrices
            UpdateCamera();
        }

        private void HandleRotation(KeyboardState keyboard, float deltaTime)
        {
            float rotationAmount = cameraRotationSpeed * deltaTime;

            if (keyboard.IsKeyDown(Keys.Q))
                cameraAngle += rotationAmount;

            if (keyboard.IsKeyDown(Keys.E))
                cameraAngle -= rotationAmount;
        }

        private void HandleZoom(MouseState mouse)
        {
            float scrollDelta = (mouse.ScrollWheelValue - previousScrollValue) / 120f;
            previousScrollValue = mouse.ScrollWheelValue;

            if (scrollDelta != 0f)
            {
                zoomLevel = MathHelper.Clamp(zoomLevel + scrollDelta * 0.1f, minZoom, maxZoom);
            }
        }

        private void HandleMovement(KeyboardState keyboard, MouseState mouse, MouseState previousMouse, float deltaTime)
        {
            Vector2 moveInput = Vector2.Zero;

            // WASD
            if (keyboard.IsKeyDown(Keys.W)) moveInput.Y += 1;
            if (keyboard.IsKeyDown(Keys.S)) moveInput.Y -= 1;
            if (keyboard.IsKeyDown(Keys.A)) moveInput.X += 1;
            if (keyboard.IsKeyDown(Keys.D)) moveInput.X -= 1;

            // Clic molette pour déplacer
            if (mouse.MiddleButton == ButtonState.Pressed && 
                previousMouse.MiddleButton == ButtonState.Pressed)
            {
                Vector2 mouseDelta = new Vector2(
                    mouse.X - previousMouse.X,
                    mouse.Y - previousMouse.Y
                );

                moveInput.X += mouseDelta.X * 0.5f;
                moveInput.Y += mouseDelta.Y * 0.5f;
            }

            // Appliquer le mouvement
            if (moveInput != Vector2.Zero)
            {
                // Normaliser pour éviter la vitesse diagonale excessive
                if (moveInput.LengthSquared() > 1f)
                    moveInput.Normalize();

                // Rotation du mouvement selon l'angle de la caméra
                float moveAngle = cameraAngle + MathHelper.PiOver2;

                Vector2 rotatedMove = new Vector2(
                    moveInput.X * (float)Math.Cos(moveAngle) - moveInput.Y * (float)Math.Sin(moveAngle),
                    moveInput.X * (float)Math.Sin(moveAngle) + moveInput.Y * (float)Math.Cos(moveAngle)
                );

                cameraOffset += rotatedMove * cameraMoveSpeed * deltaTime;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UTILITAIRES
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Convertit une position écran en rayon 3D
        /// </summary>
        public Ray ScreenPointToRay(Point screenPosition, int viewportWidth, int viewportHeight)
        {
            Vector3 nearPoint = new Vector3(screenPosition.X, screenPosition.Y, 0);
            Vector3 farPoint = new Vector3(screenPosition.X, screenPosition.Y, 1);

            nearPoint = UnprojectPoint(nearPoint, viewportWidth, viewportHeight);
            farPoint = UnprojectPoint(farPoint, viewportWidth, viewportHeight);

            Vector3 direction = Vector3.Normalize(farPoint - nearPoint);

            return new Ray(nearPoint, direction);
        }

        private Vector3 UnprojectPoint(Vector3 point, int viewportWidth, int viewportHeight)
        {
            Matrix inverseViewProjection = Matrix.Invert(ViewMatrix * ProjectionMatrix);

            // Normaliser les coordonnées écran en [-1, 1]
            Vector3 normalized = new Vector3(
                (point.X / viewportWidth) * 2f - 1f,
                -((point.Y / viewportHeight) * 2f - 1f), // Y inversé
                point.Z
            );

            // Transformer dans l'espace monde
            Vector4 worldPos = Vector4.Transform(normalized, inverseViewProjection);

            if (worldPos.W != 0)
                worldPos /= worldPos.W;

            return new Vector3(worldPos.X, worldPos.Y, worldPos.Z);
        }

        /// <summary>
        /// Raycast pour obtenir la case de grille sous la souris
        /// </summary>
        public Point GetCellFromMouse(Point mousePosition, int viewportWidth, int viewportHeight)
        {
            Ray ray = ScreenPointToRay(mousePosition, viewportWidth, viewportHeight);

            // Intersection avec le plan Y=0 (sol)
            if (Math.Abs(ray.Direction.Y) > 0.001f)
            {
                float t = -ray.Position.Y / ray.Direction.Y;
                Vector3 intersection = ray.Position + ray.Direction * t;

                int cellX = (int)(intersection.X / cellSize);
                int cellZ = (int)(intersection.Z / cellSize);

                if (cellX >= 0 && cellX < gridWidth && cellZ >= 0 && cellZ < gridHeight)
                {
                    return new Point(cellX, cellZ);
                }
            }

            return new Point(-1, -1);
        }

        /// <summary>
        /// Vérifie si une sphère est visible par la caméra (frustum culling)
        /// </summary>
        public bool IsVisible(Vector3 position, float radius)
        {
            BoundingSphere sphere = new BoundingSphere(position, radius);
            return ViewFrustum.Intersects(sphere);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ACCESSEURS
        // ═══════════════════════════════════════════════════════════════════════

        public float GetZoomLevel() => zoomLevel;
        public float GetAngle() => cameraAngle;
        public Vector2 GetOffset() => cameraOffset;
    }
}
