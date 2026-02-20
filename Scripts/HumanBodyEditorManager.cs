using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace XCOM_3
{
    public class HumanBodyEditorManager
    {
        private class SliderBinding
        {
            public string Label;
            public float Min;
            public float Max;
            public Func<float> Getter;
            public Action<float> Setter;
            public Rectangle Bar;
            public Rectangle Fill;
            public Rectangle Handle;

            public float Value => Getter();
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private readonly Texture2D _pixel;
        private readonly BasicEffect _previewEffect;
        private readonly HumanoidModelAdvanced _previewModel;
        private RenderTarget2D _previewRenderTarget;
        private readonly Unit _previewUnit;

        private readonly List<SliderBinding> _sliders = new();
        private SliderBinding _draggedSlider;

        private readonly Button _backButton;
        private readonly Button _resetButton;

        private const float PreviewModelScale = 1.75f;
        private const float PreviewRotationSensitivity = 0.015f;
        private const float PreviewPanSensitivity = 0.003f;
        private const float PreviewZoomSensitivity = 1f / 1200f;
        private const float PreviewZoomMin = 0.3f;
        private const float PreviewZoomMax = 3.0f;
        private const float PreviewPanYMin = -1.5f;
        private const float PreviewPanYMax = 1.5f;
        private float _previewRotation = MathHelper.Pi;
        private float _previewZoom = 1.0f;
        private float _previewPanY = 0.0f;
        private bool _isDraggingPreview;
        private int _lastDragMouseX;
        private int _lastDragMouseY;

        public event Action OnBackToMainMenu;

        public HumanBodyEditorManager(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = font;
            _pixel = pixel;

            _previewEffect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false,
                AmbientLightColor = new Vector3(0.5f),
                DiffuseColor = Vector3.One
            };
            _previewModel = new HumanoidModelAdvanced();
            _previewUnit = new Unit(Point.Zero, Team.Player, "Nadia", "Assault", "Rifle", null)
            {
                VisualPosition = Vector3.Zero,
                IsMoving = false,
                IsAiming = false,
                IsFiring = false
            };
            StripPreviewUnitInventoryState();

            _backButton = new Button("Back", new Vector2(0, 695));
            _resetButton = new Button("Reset Defaults", new Vector2(0, 655));

            CreateSliders();
            UpdateSliderVisuals();
        }

        public void Update(MouseState mouseState, MouseState previousMouseState)
        {
            if (_backButton.IsClicked(mouseState, previousMouseState))
            {
                OnBackToMainMenu?.Invoke();
                return;
            }

            if (_resetButton.IsClicked(mouseState, previousMouseState))
            {
                HumanBodyMorphSettings.ResetDefaults();
                UpdateSliderVisuals();
                Console.WriteLine("[BODY EDITOR] Morph defaults restored.");
                return;
            }

            HandleSliders(mouseState);
            HandlePreviewRotation(mouseState, previousMouseState);
            UpdateSliderVisuals();
        }

        public void Draw()
        {
            MouseState mouse = Mouse.GetState();

            _spriteBatch.DrawString(_font, "Human Body Editor", Vector2.Zero, UIThemeManager.PrimaryColor, 0f, Vector2.Zero, 2.2f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, "Ajuste les proportions globales des corps humains (soldats).", new Vector2(0, 42), UIThemeManager.PrimaryColor);

            for (int i = 0; i < _sliders.Count; i++)
            {
                DrawSlider(_sliders[i], i);
            }

            DrawPreviewPanel();

            _resetButton.Draw(_spriteBatch, _font, mouse);
            _backButton.Draw(_spriteBatch, _font, mouse);
        }

        public void DrawPreview3D()
        {
            Rectangle previewRect = GetPreviewRect();
            EnsurePreviewRenderTarget(previewRect.Width, previewRect.Height);
            if (_previewRenderTarget == null)
                return;

            _previewUnit.LegSwing = 0f;
            _previewUnit.ArmSwing = 0f;
            _previewUnit.BodyBob = 0f;
            _previewUnit.IdleBobOffset = 0f;

            Viewport originalViewport = _graphicsDevice.Viewport;
            DepthStencilState originalDepth = _graphicsDevice.DepthStencilState;
            BlendState originalBlend = _graphicsDevice.BlendState;
            RasterizerState originalRasterizer = _graphicsDevice.RasterizerState;
            RenderTargetBinding[] originalRenderTargets = _graphicsDevice.GetRenderTargets();

            _graphicsDevice.SetRenderTarget(_previewRenderTarget);
            _graphicsDevice.Viewport = new Viewport(0, 0, _previewRenderTarget.Width, _previewRenderTarget.Height);
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.BlendState = BlendState.Opaque;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            _graphicsDevice.Clear(new Color(12, 18, 25));

            Vector3 lookAt = new Vector3(0f, 1.7f + _previewPanY, 0f);
            Vector3 eyePos = new Vector3(0f, lookAt.Y + 0.7f * _previewZoom, 5.5f * _previewZoom);
            _previewEffect.View = Matrix.CreateLookAt(eyePos, lookAt, Vector3.Up);
            _previewEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f),
                Math.Max(0.2f, previewRect.Width / (float)previewRect.Height),
                0.1f,
                100f);

            _previewModel.DrawWithEquipment(
                _graphicsDevice,
                _previewEffect,
                _previewUnit,
                PreviewModelScale,
                _previewRotation,
                drawEquipment: false);

            _graphicsDevice.SetRenderTargets(originalRenderTargets);
            _graphicsDevice.Viewport = originalViewport;
            _graphicsDevice.DepthStencilState = originalDepth;
            _graphicsDevice.BlendState = originalBlend;
            _graphicsDevice.RasterizerState = originalRasterizer;
        }

        private void CreateSliders()
        {
            _sliders.Clear();

            AddSlider("Head Scale", 0.6f, 1.6f, () => HumanBodyMorphSettings.HeadScale, v => HumanBodyMorphSettings.HeadScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.6f));
            AddSlider("Torso Width", 0.6f, 1.6f, () => HumanBodyMorphSettings.TorsoWidthScale, v => HumanBodyMorphSettings.TorsoWidthScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.6f));
            AddSlider("Torso Height", 0.6f, 1.6f, () => HumanBodyMorphSettings.TorsoHeightScale, v => HumanBodyMorphSettings.TorsoHeightScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.6f));
            AddSlider("Torso Depth", 0.6f, 1.6f, () => HumanBodyMorphSettings.TorsoDepthScale, v => HumanBodyMorphSettings.TorsoDepthScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.6f));
            AddSlider("Limb Width", 0.6f, 1.6f, () => HumanBodyMorphSettings.LimbWidthScale, v => HumanBodyMorphSettings.LimbWidthScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.6f));
            AddSlider("Arm Length", 0.6f, 1.6f, () => HumanBodyMorphSettings.ArmLengthScale, v => HumanBodyMorphSettings.ArmLengthScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.6f));
            AddSlider("Leg Length", 0.6f, 1.6f, () => HumanBodyMorphSettings.LegLengthScale, v => HumanBodyMorphSettings.LegLengthScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.6f));
            // "Shoulder Size" ne doit pas déplacer la hauteur d'épaule.
            // On le fait piloter uniquement la largeur globale des épaules
            // (la hauteur reste dédiée au slider "Shoulder Height").
            AddSlider("Shoulder Size", 0.7f, 1.3f,
                () => HumanBodyMorphSettings.ShoulderWidthScale,
                v => HumanBodyMorphSettings.ShoulderWidthScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Shoulder Height", 0.7f, 1.3f, () => HumanBodyMorphSettings.ShoulderHeightScale, v => HumanBodyMorphSettings.ShoulderHeightScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Shoulder Slope", 0.7f, 1.3f, () => HumanBodyMorphSettings.ShoulderSlopeScale, v => HumanBodyMorphSettings.ShoulderSlopeScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Shoulder Sphere Volume", 0.6f, 2.2f,
                () => HumanBodyMorphSettings.ShoulderSphereScale,
                v => HumanBodyMorphSettings.ShoulderSphereScale = HumanBodyMorphSettings.ClampScale(v, 0.6f, 2.2f));

            AddSlider("Fem Head", 0.7f, 1.3f, () => HumanBodyMorphSettings.FeminineHeadScale, v => HumanBodyMorphSettings.FeminineHeadScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Fem Torso Width", 0.7f, 1.3f, () => HumanBodyMorphSettings.FeminineTorsoWidthScale, v => HumanBodyMorphSettings.FeminineTorsoWidthScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Fem Torso Depth", 0.7f, 1.3f, () => HumanBodyMorphSettings.FeminineTorsoDepthScale, v => HumanBodyMorphSettings.FeminineTorsoDepthScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Fem Limb Width", 0.7f, 1.3f, () => HumanBodyMorphSettings.FeminineLimbWidthScale, v => HumanBodyMorphSettings.FeminineLimbWidthScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Fem Torso Height", 0.7f, 1.3f, () => HumanBodyMorphSettings.FeminineTorsoHeightScale, v => HumanBodyMorphSettings.FeminineTorsoHeightScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));
            AddSlider("Fem Leg Length", 0.7f, 1.3f, () => HumanBodyMorphSettings.FeminineLegLengthScale, v => HumanBodyMorphSettings.FeminineLegLengthScale = HumanBodyMorphSettings.ClampScale(v, 0.7f, 1.3f));

            AddSlider("Rib Ratio", 0.4f, 1.8f, () => HumanBodyMorphSettings.RibRatio, v => HumanBodyMorphSettings.RibRatio = HumanBodyMorphSettings.ClampRatio(v));
            AddSlider("Abdomen Ratio", 0.4f, 1.8f, () => HumanBodyMorphSettings.AbdomenRatio, v => HumanBodyMorphSettings.AbdomenRatio = HumanBodyMorphSettings.ClampRatio(v));
            AddSlider("Pelvis Ratio", 0.4f, 1.8f, () => HumanBodyMorphSettings.PelvisRatio, v => HumanBodyMorphSettings.PelvisRatio = HumanBodyMorphSettings.ClampRatio(v));

            AddSlider("Fem Rib Top", 0.6f, 1.4f, () => HumanBodyMorphSettings.FeminineRibTopWidthFactor, v => HumanBodyMorphSettings.FeminineRibTopWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));
            AddSlider("Masc Rib Top", 0.6f, 1.4f, () => HumanBodyMorphSettings.MasculineRibTopWidthFactor, v => HumanBodyMorphSettings.MasculineRibTopWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));
            AddSlider("Fem Rib Bottom", 0.6f, 1.4f, () => HumanBodyMorphSettings.FeminineRibBottomWidthFactor, v => HumanBodyMorphSettings.FeminineRibBottomWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));
            AddSlider("Masc Rib Bottom", 0.6f, 1.4f, () => HumanBodyMorphSettings.MasculineRibBottomWidthFactor, v => HumanBodyMorphSettings.MasculineRibBottomWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));
            AddSlider("Fem Pelvis Top", 0.6f, 1.4f, () => HumanBodyMorphSettings.FemininePelvisTopWidthFactor, v => HumanBodyMorphSettings.FemininePelvisTopWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));
            AddSlider("Masc Pelvis Top", 0.6f, 1.4f, () => HumanBodyMorphSettings.MasculinePelvisTopWidthFactor, v => HumanBodyMorphSettings.MasculinePelvisTopWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));
            AddSlider("Fem Pelvis Bottom", 0.6f, 1.4f, () => HumanBodyMorphSettings.FemininePelvisBottomWidthFactor, v => HumanBodyMorphSettings.FemininePelvisBottomWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));
            AddSlider("Masc Pelvis Bottom", 0.6f, 1.4f, () => HumanBodyMorphSettings.MasculinePelvisBottomWidthFactor, v => HumanBodyMorphSettings.MasculinePelvisBottomWidthFactor = HumanBodyMorphSettings.ClampScale(v, 0.6f, 1.4f));

            int startY = 90;
            int spacing = 22;
            for (int i = 0; i < _sliders.Count; i++)
            {
                _sliders[i].Bar = new Rectangle(170, startY + spacing * i, 230, 8);
            }
        }

        private void AddSlider(string label, float min, float max, Func<float> getter, Action<float> setter)
        {
            _sliders.Add(new SliderBinding
            {
                Label = label,
                Min = min,
                Max = max,
                Getter = getter,
                Setter = setter
            });
        }

        private void HandleSliders(MouseState mouseState)
        {
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                if (_draggedSlider == null)
                {
                    foreach (var slider in _sliders)
                    {
                        if (slider.Bar.Contains(mouseState.Position))
                        {
                            _draggedSlider = slider;
                            break;
                        }
                    }
                }

                if (_draggedSlider != null)
                {
                    float normalized = MathHelper.Clamp((mouseState.X - _draggedSlider.Bar.X) / (float)_draggedSlider.Bar.Width, 0f, 1f);
                    float newValue = MathHelper.Lerp(_draggedSlider.Min, _draggedSlider.Max, normalized);
                    _draggedSlider.Setter(newValue);
                }
            }
            else
            {
                _draggedSlider = null;
            }
        }

        private void StripPreviewUnitInventoryState()
        {
            _previewUnit.EquippedWeapon = null;
            _previewUnit.EquippedHelmet = null;
            _previewUnit.EquippedNeck = null;
            _previewUnit.EquippedArmor = null;
            _previewUnit.EquippedShield = null;
            _previewUnit.EquippedAccessory = null;
            _previewUnit.EquippedRightHandFlashlight = null;
            _previewUnit.EquippedLeftHandFlashlight = null;
            _previewUnit.IsRightHandFlashlightOn = false;
            _previewUnit.IsLeftHandFlashlightOn = false;
            _previewUnit.EquippedShirt = null;
            _previewUnit.EquippedPants = null;
            _previewUnit.EquippedKnees = null;
            _previewUnit.EquippedFeet = null;
            _previewUnit.EquippedChestRig = null;
            _previewUnit.EquippedBelt = null;
            _previewUnit.EquippedBackpack = null;
            _previewUnit.PantsInventory.Clear();
            _previewUnit.ChestRigInventory.Clear();
            _previewUnit.BackpackInventory.Clear();
            _previewUnit.Grenades.Clear();
            _previewUnit.MaxGrenades = 0;
        }

        private void UpdateSliderVisuals()
        {
            foreach (var slider in _sliders)
            {
                float t = (slider.Value - slider.Min) / (slider.Max - slider.Min);
                t = MathHelper.Clamp(t, 0f, 1f);

                slider.Fill = new Rectangle(slider.Bar.X, slider.Bar.Y, (int)(slider.Bar.Width * t), slider.Bar.Height);
                slider.Handle = new Rectangle(slider.Bar.X + slider.Fill.Width - 4, slider.Bar.Y - 3, 8, slider.Bar.Height + 6);
            }
        }

        private void DrawSlider(SliderBinding slider, int index)
        {
            _spriteBatch.DrawString(_font, slider.Label, new Vector2(0, slider.Bar.Y - 7), UIThemeManager.PrimaryColor);
            _spriteBatch.Draw(_pixel, slider.Bar, Color.Gray);
            _spriteBatch.Draw(_pixel, slider.Fill, UIThemeManager.PrimaryColor);
            _spriteBatch.Draw(_pixel, slider.Handle, Color.White);
            _spriteBatch.DrawString(_font, slider.Value.ToString("0.00"), new Vector2(slider.Bar.Right + 12, slider.Bar.Y - 7), UIThemeManager.PrimaryColor);
        }

        private void DrawPreviewPanel()
        {
            Rectangle previewRect = GetPreviewRect();
            _spriteBatch.Draw(_pixel, previewRect, new Color(0, 0, 0, 170));

            if (_previewRenderTarget != null)
                _spriteBatch.Draw(_previewRenderTarget, previewRect, Color.White);

            _spriteBatch.DrawString(_font, "Unit Preview", new Vector2(previewRect.X + 10, previewRect.Y + 8), UIThemeManager.PrimaryColor);
            _spriteBatch.DrawString(_font, "Glisser: rotation/pan | Molette: zoom", new Vector2(previewRect.X + 10, previewRect.Bottom - 24), UIThemeManager.PrimaryColor);
        }

        private Rectangle GetPreviewRect() => new Rectangle(450, 90, 420, 560);

        private void HandlePreviewRotation(MouseState mouseState, MouseState previousMouseState)
        {
            Rectangle previewRect = GetPreviewRect();
            bool mouseInsidePreview = previewRect.Contains(mouseState.Position);
            bool isMousePressed = mouseState.LeftButton == ButtonState.Pressed;

            // Zoom via molette (uniquement quand la souris survole l'aperçu)
            int scrollDelta = mouseState.ScrollWheelValue - previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0 && mouseInsidePreview)
                _previewZoom = MathHelper.Clamp(_previewZoom - scrollDelta * PreviewZoomSensitivity, PreviewZoomMin, PreviewZoomMax);

            if (!isMousePressed)
            {
                _isDraggingPreview = false;
                return;
            }

            if (!_isDraggingPreview && previousMouseState.LeftButton != ButtonState.Pressed && mouseInsidePreview)
            {
                _isDraggingPreview = true;
                _lastDragMouseX = mouseState.X;
                _lastDragMouseY = mouseState.Y;
                return;
            }

            if (_isDraggingPreview)
            {
                int deltaX = mouseState.X - _lastDragMouseX;
                int deltaY = mouseState.Y - _lastDragMouseY;
                _previewRotation += deltaX * PreviewRotationSensitivity;
                _previewPanY = MathHelper.Clamp(_previewPanY - deltaY * PreviewPanSensitivity, PreviewPanYMin, PreviewPanYMax);
                _lastDragMouseX = mouseState.X;
                _lastDragMouseY = mouseState.Y;
            }
        }

        private void EnsurePreviewRenderTarget(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;

            if (_previewRenderTarget != null && (_previewRenderTarget.Width != width || _previewRenderTarget.Height != height))
            {
                _previewRenderTarget.Dispose();
                _previewRenderTarget = null;
            }

            if (_previewRenderTarget == null)
            {
                _previewRenderTarget = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.Depth24);
            }
        }
    }
}
