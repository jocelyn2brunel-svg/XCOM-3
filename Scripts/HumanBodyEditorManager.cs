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

        private readonly List<SliderBinding> _sliders = new();
        private SliderBinding _draggedSlider;

        private readonly Button _backButton;
        private readonly Button _resetButton;

        public event Action OnBackToMainMenu;

        public HumanBodyEditorManager(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = font;
            _pixel = pixel;

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

            _resetButton.Draw(_spriteBatch, _font, mouse);
            _backButton.Draw(_spriteBatch, _font, mouse);
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
    }
}
