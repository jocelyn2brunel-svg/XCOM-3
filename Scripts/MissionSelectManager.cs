using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// Gère l'écran de sélection de mission
    /// </summary>
    public class MissionSelectManager
    {
        // --- Références externes ---
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;

        // --- Contrôle caméra du globe ---
        private float _globeYaw = -0.25f;
        private float _globePitch = 0.15f;
        private bool _isDragging;
        private Point _lastMousePosition;

        // --- UI ---
        private Rectangle _backButtonBounds;

        // --- Missions disponibles ---
        private readonly List<MissionPoint> _missionPoints = new();

        // --- Mission sélectionnée ---
        private string _selectedMission = "";

        // --- Événements ---
        public event Action<string> OnMissionSelected;
        public event Action OnBackToMainMenu;

        public MissionSelectManager(
           GraphicsDevice graphicsDevice,
           SpriteBatch spriteBatch,
           SpriteFont font,
           Texture2D pixel)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = font;
            _pixel = pixel;

            CreateMissionPoints();
        }

        /// <summary>
        /// Met à jour l'écran de sélection de mission
        /// </summary>
        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            HandleGlobeRotation(mouseState, previousMouseState);
            HandleMissionClicks(mouseState, previousMouseState);
        }

        public void Draw3D()
        {
            // Le rendu SpriteBatch doit rester dans la passe 2D entre Begin/End.
        }

        /// <summary>
        /// Dessine l'écran de sélection de mission
        /// </summary>
        public void Draw()
        {
            MouseState mouse = Mouse.GetState();
            DrawGlobe();
            // Titre
            DrawTitle("Strategy Layer - Mission Select");
            // Boutons
            DrawMissionLabels(mouse);
            DrawBackButton(mouse);
            DrawHintText();
        }

        // ==================== MÉTHODES PRIVÉES ====================

        private void CreateMissionPoints()
        {
            _missionPoints.Clear();
            _missionPoints.Add(new MissionPoint("Tutorial", 48.8f, 2.3f, Color.LawnGreen));     // Paris
            _missionPoints.Add(new MissionPoint("Survival", 40.7f, -74.0f, Color.Orange));     // New York
            _missionPoints.Add(new MissionPoint("Assault", 35.7f, 139.7f, Color.Red));          // Tokyo
            _missionPoints.Add(new MissionPoint("Defense", -33.9f, 151.2f, Color.DeepSkyBlue)); // Sydney
        }

        private void HandleGlobeRotation(MouseState mouseState, MouseState previousMouseState)
        {
            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool leftWasReleased = previousMouseState.LeftButton == ButtonState.Released;

            if (leftPressed && leftWasReleased)
            {
                _isDragging = true;
                _lastMousePosition = mouseState.Position;
            }
            else if (mouseState.LeftButton == ButtonState.Released)
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                Point delta = mouseState.Position - _lastMousePosition;
                _globeYaw += delta.X * 0.01f;
                _globePitch -= delta.Y * 0.01f;
                _globePitch = MathHelper.Clamp(_globePitch, -1.1f, 1.1f);
                _lastMousePosition = mouseState.Position;
            }
        }

        private void HandleMissionClicks(MouseState mouseState, MouseState previousMouseState)
        {
            bool click = mouseState.LeftButton == ButtonState.Pressed &&
                                   previousMouseState.LeftButton == ButtonState.Released;

            if (!click)
                return;

            if (_backButtonBounds.Contains(mouseState.Position))
            {
                Console.WriteLine("[MISSION SELECT] Back to main menu");
                OnBackToMainMenu?.Invoke();
                return;
            }

            var globeData = ComputeGlobeData();
            foreach (var missionRenderData in globeData.Missions)
            {
                if (missionRenderData.IsFront &&
                    Vector2.Distance(missionRenderData.ScreenPosition, mouseState.Position.ToVector2()) <= missionRenderData.Radius + 4f)
                {
                    _selectedMission = missionRenderData.Mission.Name;
                    Console.WriteLine($"[MISSION SELECT] Mission selected: {_selectedMission}");
                    OnMissionSelected?.Invoke(_selectedMission);
                    return;
                }
            }
        }

        private void DrawTitle(string text)
        {
            _spriteBatch.DrawString(
                _font,
                text,
                Vector2.Zero,
                Color.White,
                0f,
                Vector2.Zero,
                3f,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawMissionLabels(MouseState mouse)
        {
            var globeData = ComputeGlobeData();

            foreach (var missionRenderData in globeData.Missions.OrderBy(m => m.Depth))
            {
                if (!missionRenderData.IsFront)
                    continue;

                bool hovered = Vector2.Distance(missionRenderData.ScreenPosition, mouse.Position.ToVector2()) <= missionRenderData.Radius + 6f;
                Color markerColor = hovered ? Color.White : missionRenderData.Mission.Color;
                DrawCircle(missionRenderData.ScreenPosition, missionRenderData.Radius, markerColor);

                Vector2 labelPos = missionRenderData.ScreenPosition + new Vector2(10f, -10f);
                _spriteBatch.DrawString(_font, missionRenderData.Mission.Name, labelPos, hovered ? Color.Yellow : Color.White);
            }
        }

        private void DrawBackButton(MouseState mouse)
        {
            Vector2 pos = new Vector2(30, _graphicsDevice.Viewport.Height - 60);
            Vector2 size = _font.MeasureString("Back");
            _backButtonBounds = new Rectangle((int)pos.X - 8, (int)pos.Y - 4, (int)size.X + 16, (int)size.Y + 8);

            bool hovered = _backButtonBounds.Contains(mouse.Position);
            _spriteBatch.Draw(_pixel, _backButtonBounds, hovered ? new Color(60, 60, 60, 220) : new Color(30, 30, 30, 200));
            _spriteBatch.DrawString(_font, "Back", pos, hovered ? Color.Yellow : Color.White);
        }

        private void DrawHintText()
        {
            string hint = "Clique-glisse pour tourner la Terre | Clique un point d'interet pour lancer une mission";
            Vector2 hintSize = _font.MeasureString(hint);
            Vector2 hintPos = new Vector2((_graphicsDevice.Viewport.Width - hintSize.X) / 2f, _graphicsDevice.Viewport.Height - 35f);
            _spriteBatch.DrawString(_font, hint, hintPos, Color.LightGray);

            if (!string.IsNullOrEmpty(_selectedMission))
            {
                _spriteBatch.DrawString(_font, $"Mission cible: {_selectedMission}", new Vector2(30, 70), Color.Cyan);
            }
        }

        private void DrawGlobe()
        {
            var globeData = ComputeGlobeData();
            Vector2 center = globeData.Center;
            float radius = globeData.Radius;

            DrawSphere(center, radius);
            DrawCircleOutline(center, radius, 2f, new Color(100, 170, 255));

            foreach (var missionRenderData in globeData.Missions.Where(m => !m.IsFront).OrderBy(m => m.Depth))
            {
                DrawCircle(missionRenderData.ScreenPosition, missionRenderData.Radius, missionRenderData.Mission.Color * 0.4f);
            }
        }

        private GlobeRenderData ComputeGlobeData()
        {
            float radius = MathF.Min(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height) * 0.28f;
            Vector2 center = new Vector2(_graphicsDevice.Viewport.Width * 0.5f, _graphicsDevice.Viewport.Height * 0.5f + 20f);

            Matrix rotation = Matrix.CreateFromYawPitchRoll(_globeYaw, _globePitch, 0f);
            List<MissionRenderData> missions = new();

            foreach (var mission in _missionPoints)
            {
                Vector3 basePosition = LatLonToSphere(mission.Latitude, mission.Longitude);
                Vector3 rotated = Vector3.Transform(basePosition, rotation);

                Vector2 screen = new Vector2(
                    center.X + rotated.X * radius,
                    center.Y - rotated.Y * radius
                );

                float pointScale = MathHelper.Lerp(4f, 8f, (rotated.Z + 1f) * 0.5f);
                missions.Add(new MissionRenderData(mission, screen, pointScale, rotated.Z, rotated.Z >= 0f));
            }

            return new GlobeRenderData(center, radius, missions);
        }

        private static Vector3 LatLonToSphere(float latitude, float longitude)
        {
            float lat = MathHelper.ToRadians(latitude);
            float lon = MathHelper.ToRadians(longitude);

            float cosLat = MathF.Cos(lat);
            return new Vector3(
                cosLat * MathF.Sin(lon),
                MathF.Sin(lat),
                cosLat * MathF.Cos(lon)
            );
        }

        private void DrawCircle(Vector2 center, float radius, Color color)
        {
            int intRadius = Math.Max(1, (int)MathF.Ceiling(radius));
            for (int y = -intRadius; y <= intRadius; y++)
            {
                float normalizedY = y / radius;
                if (normalizedY * normalizedY > 1f)
                    continue;

                int halfWidth = (int)MathF.Sqrt(radius * radius - y * y);
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle((int)center.X - halfWidth, (int)center.Y + y, halfWidth * 2, 1),
                    color);
            }
        }

        private void DrawCircleOutline(Vector2 center, float radius, float thickness, Color color)
        {
            const int segmentCount = 96;
            Vector2 previousPoint = center + new Vector2(radius, 0f);

            for (int i = 1; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                float angle = MathHelper.TwoPi * t;
                Vector2 currentPoint = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                DrawLine(previousPoint, currentPoint, thickness, color);
                previousPoint = currentPoint;
            }
        }

        private void DrawSphere(Vector2 center, float radius)
        {
            int intRadius = Math.Max(1, (int)MathF.Ceiling(radius));
            Vector3 lightDirection = Vector3.Normalize(new Vector3(-0.65f, -0.35f, 0.67f));
            Color deepOcean = new Color(12, 26, 64);
            Color brightOcean = new Color(44, 108, 189);

            for (int y = -intRadius; y <= intRadius; y++)
            {
                float normalizedY = y / radius;
                float ySquared = normalizedY * normalizedY;
                if (ySquared > 1f)
                    continue;

                float normalizedXEdge = MathF.Sqrt(1f - ySquared);
                int halfWidth = Math.Max(1, (int)(normalizedXEdge * radius));

                float sampleX = -normalizedXEdge * 0.35f;
                float sampleZSquared = MathF.Max(0f, 1f - sampleX * sampleX - ySquared);
                Vector3 normal = Vector3.Normalize(new Vector3(sampleX, normalizedY, MathF.Sqrt(sampleZSquared)));
                float light = MathHelper.Clamp(Vector3.Dot(normal, lightDirection) * 0.7f + 0.3f, 0f, 1f);
                Color rowColor = Color.Lerp(deepOcean, brightOcean, light);

                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle((int)center.X - halfWidth, (int)center.Y + y, halfWidth * 2, 1),
                    rowColor);
            }

            DrawCircle(center + new Vector2(-radius * 0.25f, -radius * 0.22f), radius * 0.52f, new Color(170, 220, 255, 40));
        }

        private void DrawLine(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);

            _spriteBatch.Draw(
                _pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness),
                null,
                color,
                angle,
                Vector2.Zero,
                SpriteEffects.None,
                0f
            );
        }

        private record MissionPoint(string Name, float Latitude, float Longitude, Color Color);
        private record MissionRenderData(MissionPoint Mission, Vector2 ScreenPosition, float Radius, float Depth, bool IsFront);
        private record GlobeRenderData(Vector2 Center, float Radius, List<MissionRenderData> Missions);
    }
}
