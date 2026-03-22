using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace XCOM_3
{
    /// <summary>
    /// Gère la caméra 3D isométrique avec rotation, zoom et déplacement
    /// VERSION CORRIGÉE - Rotation Q/E fonctionnelle
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
        private float maxZoom = 4f;
        private float previousScrollValue = 0f;

        // ✅ État de rotation pour lerp smooth
        private bool isRotatingToTarget = false;
        private float targetAngle = 0f;
        private float rotationLerpSpeed = 8f; // Vitesse du lerp (plus haut = plus rapide)

        // Débounce pour éviter les clics multiples
        private KeyboardState previousKeyboardState;

        // Dimensions de la grille
        private int gridWidth;
        private int gridHeight;
        private int cellSize;

        // Mode cinématique épaule (visée)
        private bool useShoulderCamera = false;
        private Vector3 shoulderCameraPosition;
        private Vector3 shoulderCameraTarget;
        private float antiOcclusionHeightOffset = 0f;
        private float antiOcclusionOrbitOffset = 0f;
        private float verticalFocusY = 0f;

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
            if (useShoulderCamera)
            {
                Position = shoulderCameraPosition;
                Target = shoulderCameraTarget;
                ViewMatrix = Matrix.CreateLookAt(Position, Target, Vector3.Up);
                ViewFrustum = new BoundingFrustum(ViewMatrix * ProjectionMatrix);
                return;
            }

            // Centre de la grille
            float centerX = (gridWidth * cellSize) / 2f + cameraOffset.X;
            float centerZ = (gridHeight * cellSize) / 2f + cameraOffset.Y;

            Target = new Vector3(centerX, verticalFocusY, centerZ);

            // Appliquer le zoom
            float adjustedDistance = cameraDistance / zoomLevel;
            float adjustedHeight = cameraHeight / zoomLevel + antiOcclusionHeightOffset;
            float effectiveAngle = cameraAngle + antiOcclusionOrbitOffset;

            // Position de la caméra en orbite autour du centre
            Position = new Vector3(
                centerX + (float)Math.Cos(effectiveAngle) * adjustedDistance,
                verticalFocusY + adjustedHeight,
                centerZ + (float)Math.Sin(effectiveAngle) * adjustedDistance
            );

            // Matrice de vue
            ViewMatrix = Matrix.CreateLookAt(Position, Target, Vector3.Up);

            // Frustum culling
            ViewFrustum = new BoundingFrustum(ViewMatrix * ProjectionMatrix);
        }

        public void SetVerticalFocus(float worldY)
        {
            verticalFocusY = worldY;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CONTRÔLES - Gestion des inputs
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gère les contrôles de la caméra (clavier + souris)
        /// </summary>
        public void HandleControls(KeyboardState keyboard, MouseState mouse, MouseState previousMouse, GameTime gameTime, bool allowZoom = true, Point? rotationPivotCell = null)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // ═══ ROTATION (Q/E) ═══
            HandleRotation(keyboard, deltaTime, rotationPivotCell);

            // ═══ ZOOM (Molette) ═══
            HandleZoom(mouse, allowZoom);

            // ═══ DÉPLACEMENT (WASD + Clic molette) ═══
            HandleMovement(keyboard, mouse, previousMouse, deltaTime);

            // Mettre à jour les matrices
            UpdateCamera();
        }

        private void HandleRotation(KeyboardState keyboard, float deltaTime, Point? rotationPivotCell)
        {
            float step = MathHelper.ToRadians(45f);

            // ✅ Détection d'appui (une seule fois par pression)
            bool qPressed = keyboard.IsKeyDown(Keys.Q) && previousKeyboardState.IsKeyUp(Keys.Q);
            bool ePressed = keyboard.IsKeyDown(Keys.E) && previousKeyboardState.IsKeyUp(Keys.E);

            // ✅ Appui sur Q → Tourner vers la gauche (angle suivant)
            if (qPressed)
            {
                ApplyRotationPivot(rotationPivotCell);
                float baseAngle = isRotatingToTarget ? targetAngle : SnapAngleToStep(cameraAngle, step);
                targetAngle = NormalizeAngle(baseAngle + step);
                isRotatingToTarget = true;
                Console.WriteLine($"[CAMERA] Rotating to {MathHelper.ToDegrees(targetAngle)}°");
            }

            // ✅ Appui sur E → Tourner vers la droite (angle suivant)
            if (ePressed)
            {
                ApplyRotationPivot(rotationPivotCell);
                float baseAngle = isRotatingToTarget ? targetAngle : SnapAngleToStep(cameraAngle, step);
                targetAngle = NormalizeAngle(baseAngle - step);
                isRotatingToTarget = true;
                Console.WriteLine($"[CAMERA] Rotating to {MathHelper.ToDegrees(targetAngle)}°");
            }

            // ✅ Lerp progressif vers l'angle cible
            if (isRotatingToTarget)
            {
                // Calculer la différence angulaire
                float angleDiff = GetShortestAngleDelta(cameraAngle, targetAngle);

                // Lerp vers la cible
                cameraAngle = NormalizeAngle(cameraAngle + angleDiff * rotationLerpSpeed * deltaTime);

                // Arrivé à destination ?
                if (Math.Abs(angleDiff) < 0.01f)
                {
                    cameraAngle = targetAngle; // Snap final pour éviter les oscillations
                    isRotatingToTarget = false;
                    Console.WriteLine($"[CAMERA] Rotation complete at {MathHelper.ToDegrees(cameraAngle)}°");
                }
            }

            // Sauvegarder l'état du clavier
            previousKeyboardState = keyboard;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
            while (angle < -MathHelper.Pi) angle += MathHelper.TwoPi;
            return angle;
        }

        private static float GetShortestAngleDelta(float from, float to)
        {
            return NormalizeAngle(to - from);
        }

        private static float SnapAngleToStep(float angle, float step)
        {
            return NormalizeAngle((float)Math.Round(angle / step) * step);
        }

        private void ApplyRotationPivot(Point? rotationPivotCell)
        {
            if (!rotationPivotCell.HasValue)
                return;

            Point pivotCell = rotationPivotCell.Value;
            if (pivotCell.X < 0 || pivotCell.Y < 0)
                return;

            float centerX = (gridWidth * cellSize) / 2f;
            float centerZ = (gridHeight * cellSize) / 2f;
            float pivotWorldX = pivotCell.X * cellSize + cellSize / 2f;
            float pivotWorldZ = pivotCell.Y * cellSize + cellSize / 2f;

            cameraOffset = new Vector2(
                pivotWorldX - centerX,
                pivotWorldZ - centerZ
            );
        }

        private void HandleZoom(MouseState mouse, bool allowZoom)
        {
            float scrollDelta = (mouse.ScrollWheelValue - previousScrollValue) / 120f;
            previousScrollValue = mouse.ScrollWheelValue;

            if (allowZoom && scrollDelta != 0f)
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
        public Point GetCellFromMouse(Point mousePosition, int viewportWidth, int viewportHeight, float planeY = 0f)
        {
            Ray ray = ScreenPointToRay(mousePosition, viewportWidth, viewportHeight);

            // Intersection avec le plan horizontal du niveau affiché
            if (Math.Abs(ray.Direction.Y) > 0.001f)
            {
                float t = (planeY - ray.Position.Y) / ray.Direction.Y;

                if (t < 0)
                    return new Point(-1, -1);

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

        public void SetShoulderCamera(Vector3 desiredPosition, Vector3 desiredTarget)
        {
            if (!useShoulderCamera)
            {
                shoulderCameraPosition = desiredPosition;
                shoulderCameraTarget = desiredTarget;
            }
            else
            {
                shoulderCameraPosition = Vector3.Lerp(shoulderCameraPosition, desiredPosition, 0.2f);
                shoulderCameraTarget = Vector3.Lerp(shoulderCameraTarget, desiredTarget, 0.25f);
            }

            useShoulderCamera = true;
            UpdateCamera();
        }

        public void ClearShoulderCamera()
        {
            useShoulderCamera = false;
            UpdateCamera();
        }

        public void SetAntiOcclusionOffsets(float heightOffset, float orbitOffset)
        {
            antiOcclusionHeightOffset = MathHelper.Clamp(heightOffset, 0f, cellSize * 1.5f);
            antiOcclusionOrbitOffset = MathHelper.Clamp(orbitOffset, MathHelper.ToRadians(-10f), MathHelper.ToRadians(10f));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ACCESSEURS
        // ═══════════════════════════════════════════════════════════════════════

        public float GetZoomLevel() => zoomLevel;
        public float GetAngle() => cameraAngle;
        public Vector2 GetOffset() => cameraOffset;

        /// <summary>
        /// Centre la caméra sur une position donnée (pour TAB selection)
        /// </summary>
        public void CenterOnPosition(float worldX, float worldZ)
        {
            // Calculer le décalage nécessaire
            float centerX = (gridWidth * cellSize) / 2f;
            float centerZ = (gridHeight * cellSize) / 2f;

            cameraOffset = new Vector2(
                worldX - centerX,
                worldZ - centerZ
            );

            UpdateCamera();

            Console.WriteLine($"[CAMERA] Centered on ({worldX}, {worldZ})");
        }
    }
}
