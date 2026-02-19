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
        private float _globeZoom = 1f;
        private bool _isDragging;
        private Point _lastMousePosition;

        private const float MinGlobeZoom = 0.65f;
        private const float MaxGlobeZoom = 1.75f;
        private const float ZoomStepPerWheelNotch = 0.08f;

        // --- UI ---
        private Rectangle _backButtonBounds;

        // --- Missions disponibles ---
        private readonly List<MissionPoint> _missionPoints = new();
        private readonly List<GeoEllipse> _continentMasks = new();

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

            CreateContinentMasks();
            CreateMissionPoints();
        }

        /// <summary>
        /// Met à jour l'écran de sélection de mission
        /// </summary>
        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            HandleGlobeRotation(mouseState, previousMouseState);
            HandleGlobeZoom(mouseState, previousMouseState);
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
            _missionPoints.Add(new MissionPoint("Tutorial", "Paris", 48.8566f, 2.3522f, Color.LawnGreen));
            _missionPoints.Add(new MissionPoint("Survival", "New York", 40.7128f, -74.0060f, Color.Orange));
            _missionPoints.Add(new MissionPoint("Assault", "Tokyo", 35.6762f, 139.6503f, Color.Red));
            _missionPoints.Add(new MissionPoint("Defense", "Sydney", -33.8688f, 151.2093f, Color.DeepSkyBlue));
            _missionPoints.Add(new MissionPoint("Centre-Ville", "Montreal", 45.5017f, -73.5673f, Color.MediumPurple));
            _missionPoints.Add(new MissionPoint("Extraction", "Reykjavik", 64.1466f, -21.9426f, Color.Gold));
            _missionPoints.Add(new MissionPoint("Sabotage", "Le Caire", 30.0444f, 31.2357f, Color.Crimson));
            _missionPoints.Add(new MissionPoint("Blackout", "Sao Paulo", -23.5505f, -46.6333f, Color.Chartreuse));
            _missionPoints.Add(new MissionPoint("Sprint", "Tampa Bay", 27.9506f, -82.4572f, Color.LightSalmon));
        }

        private void CreateContinentMasks()
        {
            _continentMasks.Clear();

            // Ellipses géographiques (lat/lon) pour dessiner une carte terrestre stylisée.
            _continentMasks.AddRange(new[]
            {
                new GeoEllipse(52f, -105f, 34f, 56f), // Amérique du Nord
                new GeoEllipse(22f, -100f, 16f, 22f), // Mexique
                new GeoEllipse(-14f, -60f, 28f, 18f), // Amérique du Sud
                new GeoEllipse(3f, 20f, 36f, 34f),    // Afrique
                new GeoEllipse(54f, 18f, 20f, 14f),   // Europe
                new GeoEllipse(46f, 82f, 30f, 82f),   // Asie
                new GeoEllipse(21f, 78f, 9f, 8f),     // Inde
                new GeoEllipse(64f, 100f, 8f, 18f),   // Sibérie nord
                new GeoEllipse(-25f, 134f, 14f, 17f), // Australie
                new GeoEllipse(72f, -40f, 10f, 12f),  // Groenland
                new GeoEllipse(-78f, 0f, 11f, 180f),  // Antarctique
            });
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

        private void HandleGlobeZoom(MouseState mouseState, MouseState previousMouseState)
        {
            int wheelDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (wheelDelta == 0)
                return;

            float wheelNotches = wheelDelta / 120f;
            _globeZoom += wheelNotches * ZoomStepPerWheelNotch;
            _globeZoom = MathHelper.Clamp(_globeZoom, MinGlobeZoom, MaxGlobeZoom);
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
                _spriteBatch.DrawString(_font, $"{missionRenderData.Mission.Name} ({missionRenderData.Mission.City})", labelPos, hovered ? Color.Yellow : Color.White);
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
            string hint = "Clique-glisse: tourner | Molette: zoom | Clique un point d'interet: lancer une mission"; Vector2 hintSize = _font.MeasureString(hint);
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
            Matrix rotation = CreateGlobeRotationMatrix();

            DrawSphere(center, radius);
            DrawEarthTexture(center, radius, rotation);
            DrawLatitudeLongitudeGrid(center, radius, rotation, false);
            DrawLatitudeLongitudeGrid(center, radius, rotation, true);
            DrawCircleOutline(center, radius, 2f, new Color(100, 170, 255));

            foreach (var missionRenderData in globeData.Missions.Where(m => !m.IsFront).OrderBy(m => m.Depth))
            {
                DrawCircle(missionRenderData.ScreenPosition, missionRenderData.Radius, missionRenderData.Mission.Color * 0.4f);
            }
        }

        private GlobeRenderData ComputeGlobeData()
        {
            float baseRadius = MathF.Min(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height) * 0.28f;
            float radius = baseRadius * _globeZoom;
            Vector2 center = new Vector2(_graphicsDevice.Viewport.Width * 0.5f, _graphicsDevice.Viewport.Height * 0.5f + 20f);

            Matrix rotation = CreateGlobeRotationMatrix();
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

        private Matrix CreateGlobeRotationMatrix()
        {
            return Matrix.CreateFromYawPitchRoll(_globeYaw, 0f, 0f);
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
            Vector3 viewDirection = Vector3.UnitZ;
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

                float lighting = ComputeLighting(normal, lightDirection, viewDirection, ambient: 0.22f, diffuseStrength: 0.72f, specularStrength: 0.42f, shininess: 42f);
                Color rowColor = Color.Lerp(deepOcean, brightOcean, lighting);

                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle((int)center.X - halfWidth, (int)center.Y + y, halfWidth * 2, 1),
                    rowColor);
            }
        }

        private void DrawEarthTexture(Vector2 center, float radius, Matrix rotation)
        {
            const int latitudeStep = 2;
            const int longitudeStep = 2;
            Vector3 lightDirection = Vector3.Normalize(new Vector3(-0.65f, -0.35f, 0.67f));
            Vector3 viewDirection = Vector3.UnitZ;
            Color coastColor = new Color(201, 198, 140);

            for (int latitude = -88; latitude <= 88; latitude += latitudeStep)
            {
                for (int longitude = -180; longitude <= 180; longitude += longitudeStep)
                {
                    if (!IsLand(latitude, longitude))
                        continue;

                    Vector3 world = Vector3.Transform(LatLonToSphere(latitude, longitude), rotation);
                    if (world.Z < 0f)
                        continue;

                    float lighting = ComputeLighting(world, lightDirection, viewDirection, ambient: 0.24f, diffuseStrength: 0.70f, specularStrength: 0.08f, shininess: 14f);
                    float roughness = ComputeLandRoughness(latitude, longitude);
                    Color landBase = GetLandColor(latitude, roughness);
                    Color terrainTint = GetTerrainTint(latitude, roughness);

                    Color landColor = Color.Lerp(new Color(44, 80, 40), landBase, lighting);
                    landColor = Color.Lerp(landColor, terrainTint, 0.22f + roughness * 0.12f);

                    if (IsNearCoast(latitude, longitude))
                    {
                        landColor = Color.Lerp(landColor, coastColor, 0.3f + roughness * 0.15f);
                    }

                    Vector2 screen = new Vector2(
                        center.X + world.X * radius,
                        center.Y - world.Y * radius
                    );

                    int patchSize = world.Z > 0.65f ? 3 : 2;
                    _spriteBatch.Draw(_pixel, new Rectangle((int)screen.X, (int)screen.Y, patchSize, patchSize), landColor);
                }
            }
        }

        private static float ComputeLighting(
            Vector3 normal,
            Vector3 lightDirection,
            Vector3 viewDirection,
            float ambient,
            float diffuseStrength,
            float specularStrength,
            float shininess)
        {
            float diffuse = MathF.Max(0f, Vector3.Dot(normal, lightDirection));
            Vector3 halfVector = Vector3.Normalize(lightDirection + viewDirection);
            float specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, halfVector)), shininess);

            float fresnel = MathF.Pow(1f - MathF.Max(0f, Vector3.Dot(normal, viewDirection)), 3f) * 0.12f;
            float lighting = ambient + diffuse * diffuseStrength + specular * specularStrength + fresnel;
            return MathHelper.Clamp(lighting, 0f, 1f);
        }

        private static Color GetLandColor(float latitude, float roughness)
        {
            if (MathF.Abs(latitude) > 62f)
                return Color.Lerp(new Color(215, 226, 210), new Color(185, 199, 176), roughness * 0.45f);
            if (MathF.Abs(latitude) > 45f)
                return Color.Lerp(new Color(122, 155, 90), new Color(104, 134, 74), roughness * 0.5f);
            if (MathF.Abs(latitude) < 18f)
                return Color.Lerp(new Color(79, 141, 71), new Color(94, 158, 84), roughness * 0.35f);

            return Color.Lerp(new Color(104, 146, 82), new Color(89, 123, 69), roughness * 0.5f);
        }

        private static Color GetTerrainTint(float latitude, float roughness)
        {
            if (MathF.Abs(latitude) < 24f)
                return Color.Lerp(new Color(90, 120, 62), new Color(121, 153, 89), roughness);

            if (MathF.Abs(latitude) > 55f)
                return Color.Lerp(new Color(145, 155, 134), new Color(198, 208, 190), roughness);

            return Color.Lerp(new Color(103, 127, 77), new Color(152, 170, 118), roughness);
        }

        private float ComputeLandRoughness(float latitude, float longitude)
        {
            float waveA = MathF.Sin(MathHelper.ToRadians(longitude * 2.1f + latitude * 0.9f));
            float waveB = MathF.Cos(MathHelper.ToRadians(longitude * 0.85f - latitude * 2.6f));
            float waveC = MathF.Sin(MathHelper.ToRadians(longitude * 3.7f + latitude * 1.4f));

            float blended = waveA * 0.45f + waveB * 0.35f + waveC * 0.2f;
            return MathHelper.Clamp(blended * 0.5f + 0.5f, 0f, 1f);
        }

        private bool IsNearCoast(float latitude, float longitude)
        {
            const float coastBand = 3.2f;
            foreach (var continent in _continentMasks)
            {
                float latitudeOffset = latitude - continent.CenterLatitude;
                float longitudeOffset = WrapLongitudeDifference(longitude - continent.CenterLongitude);

                float latFactor = latitudeOffset / continent.HalfLatitudeSpan;
                float lonFactor = longitudeOffset / continent.HalfLongitudeSpan;
                float distance = latFactor * latFactor + lonFactor * lonFactor;

                float latBand = coastBand / MathF.Max(6f, continent.HalfLatitudeSpan);
                float lonBand = coastBand / MathF.Max(6f, continent.HalfLongitudeSpan);
                float outerDistance = MathF.Pow(MathF.Abs(latFactor) + latBand, 2f) + MathF.Pow(MathF.Abs(lonFactor) + lonBand, 2f);

                if (distance <= 1f && outerDistance >= 1f)
                    return true;
            }

            return false;
        }

        private bool IsLand(float latitude, float longitude)
        {
            foreach (var continent in _continentMasks)
            {
                float latitudeOffset = latitude - continent.CenterLatitude;
                float longitudeOffset = WrapLongitudeDifference(longitude - continent.CenterLongitude);

                float latFactor = latitudeOffset / continent.HalfLatitudeSpan;
                float lonFactor = longitudeOffset / continent.HalfLongitudeSpan;
                if (latFactor * latFactor + lonFactor * lonFactor <= 1f)
                    return true;
            }

            return false;
        }

        private static float WrapLongitudeDifference(float diff)
        {
            while (diff > 180f)
                diff -= 360f;

            while (diff < -180f)
                diff += 360f;

            return diff;
        }

        private void DrawLatitudeLongitudeGrid(Vector2 center, float radius, Matrix rotation, bool frontHemisphere)
        {
            Color latitudeColor = frontHemisphere ? new Color(138, 188, 245, 80) : new Color(70, 94, 128, 45);
            Color longitudeColor = frontHemisphere ? new Color(108, 162, 228, 85) : new Color(60, 84, 120, 50);

            for (int latitude = -60; latitude <= 60; latitude += 30)
            {
                DrawLatitudeLine(center, radius, rotation, latitude, latitudeColor, frontHemisphere);
            }

            for (int longitude = -150; longitude <= 180; longitude += 30)
            {
                DrawLongitudeLine(center, radius, rotation, longitude, longitudeColor, frontHemisphere);
            }
        }

        private void DrawLatitudeLine(Vector2 center, float radius, Matrix rotation, int latitude, Color color, bool frontHemisphere)
        {
            const int step = 8;
            for (int longitude = -180; longitude < 180; longitude += step)
            {
                Vector3 start = Vector3.Transform(LatLonToSphere(latitude, longitude), rotation);
                Vector3 end = Vector3.Transform(LatLonToSphere(latitude, longitude + step), rotation);
                DrawGridSegment(center, radius, start, end, color, frontHemisphere);
            }
        }

        private void DrawLongitudeLine(Vector2 center, float radius, Matrix rotation, int longitude, Color color, bool frontHemisphere)
        {
            const int step = 8;
            for (int latitude = -88; latitude < 88; latitude += step)
            {
                Vector3 start = Vector3.Transform(LatLonToSphere(latitude, longitude), rotation);
                Vector3 end = Vector3.Transform(LatLonToSphere(latitude + step, longitude), rotation);
                DrawGridSegment(center, radius, start, end, color, frontHemisphere);
            }
        }

        private void DrawGridSegment(Vector2 center, float radius, Vector3 start, Vector3 end, Color color, bool frontHemisphere)
        {
            bool segmentIsFront = (start.Z + end.Z) * 0.5f >= 0f;
            if (segmentIsFront != frontHemisphere)
                return;

            Vector2 startScreen = new Vector2(center.X + start.X * radius, center.Y - start.Y * radius);
            Vector2 endScreen = new Vector2(center.X + end.X * radius, center.Y - end.Y * radius);
            DrawLine(startScreen, endScreen, 1f, color);
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

        private record MissionPoint(string Name, string City, float Latitude, float Longitude, Color Color);
        private record GeoEllipse(float CenterLatitude, float CenterLongitude, float HalfLatitudeSpan, float HalfLongitudeSpan);
        private record MissionRenderData(MissionPoint Mission, Vector2 ScreenPosition, float Radius, float Depth, bool IsFront);
        private record GlobeRenderData(Vector2 Center, float Radius, List<MissionRenderData> Missions);
    }
}
