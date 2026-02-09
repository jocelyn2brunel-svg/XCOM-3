using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace XCOM_3
{
    /// <summary>
    /// UI de cible de tir
    /// </summary>
    public class FireTargetUI
    {
        public Unit Target;
        public Rectangle Bounds;
        public int HitChance;
    }

    /// <summary>
    /// Gère l'interface utilisateur du combat
    /// </summary>
    public class CombatUISystem
    {
        private GraphicsDevice graphicsDevice;
        private SpriteBatch spriteBatch;
        private SpriteFont font;
        private Texture2D pixel;

        // État UI
        public Unit SelectedFireTarget { get; set; }
        public Unit HoveredFireTarget { get; private set; }
        public bool ShowFireTargets { get; set; }
        public List<FireTargetUI> FireTargetsUI { get; private set; } = new List<FireTargetUI>();
        public List<Button> UnitActionButtons { get; private set; } = new List<Button>();

        // Boutons
        public Rectangle FireButton { get; private set; }
        public Rectangle EndTurnButton { get; private set; }
        public bool FireButtonHovered { get; private set; }
        public bool EndTurnHovered { get; private set; }

        // Constantes
        public const int ActionButtonWidth = 100;
        public const int ActionButtonHeight = 36;

        public CombatUISystem(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch,
            SpriteFont font, Texture2D pixel)
        {
            this.graphicsDevice = graphicsDevice;
            this.spriteBatch = spriteBatch;
            this.font = font;
            this.pixel = pixel;
        }


        /// <summary>
        /// Met à jour les cibles de tir
        /// </summary>
        public void UpdateFireTargets(Unit selectedUnit, List<Unit> validTargets)
        {
            FireTargetsUI.Clear();
            SelectedFireTarget = null;

            if (selectedUnit == null || selectedUnit.Team != Team.Player || selectedUnit.ActionPoints <= 0)
            {
                ShowFireTargets = false;
                return;
            }

            ShowFireTargets = validTargets.Count > 0;

            for (int i = 0; i < validTargets.Count; i++)
            {
                var target = validTargets[i];
                int distance = Math.Abs(target.Cell.X - selectedUnit.Cell.X) +
                              Math.Abs(target.Cell.Y - selectedUnit.Cell.Y);
                
                int chance = Math.Max(selectedUnit.WeaponData.Accuracy - distance * 5, 10);

                FireTargetsUI.Add(new FireTargetUI
                {
                    Target = target,
                    HitChance = chance,
                    Bounds = Rectangle.Empty // Sera mis à jour dans UpdateFireTargetsUIPositions
                });
            }

            UpdateFireTargetsUIPositions(selectedUnit);
        }

        /// <summary>
        /// Met à jour les positions des icônes de cibles
        /// </summary>
        public void UpdateFireTargetsUIPositions(Unit selectedUnit)
        {
            if (FireTargetsUI.Count == 0)
                return;

            int icon = 48, space = 10;
            int total = FireTargetsUI.Count * icon + (FireTargetsUI.Count - 1) * space;

            int startX, y;
            int bw = ActionButtonWidth, bh = ActionButtonHeight;
            int by = graphicsDevice.Viewport.Height - bh - 15;

            if (SelectedFireTarget != null && selectedUnit != null && selectedUnit.ActionPoints > 0)
            {
                // Au-dessus du bouton CONFIRMER
                int fireConfirmWidth = 140;
                int fireConfirmHeight = 50;
                int fireButtonX = graphicsDevice.Viewport.Width / 2 - fireConfirmWidth / 2;
                int fireButtonY = by - fireConfirmHeight - 10;

                startX = fireButtonX + (fireConfirmWidth - total) / 2;
                y = fireButtonY - icon - 10;
            }
            else
            {
                // Au-dessus du bouton TIRER
                int bx = (graphicsDevice.Viewport.Width - bw) / 2;
                startX = bx - 110 + (bw - total) / 2;
                y = by - icon - 10;
            }

            for (int i = 0; i < FireTargetsUI.Count; i++)
            {
                FireTargetsUI[i].Bounds = new Rectangle(startX + i * (icon + space), y, icon, icon);
            }
        }

        /// <summary>
        /// Dessine le panneau d'info de l'unité
        /// </summary>
        public void DrawUnitInfoPanel(Unit selectedUnit, Dictionary<string, GrenadeData> grenadeDatabase)
        {
            if (selectedUnit == null)
                return;

            int m = 10, w = 300, h = 200;
            int x = m, y = graphicsDevice.Viewport.Height - h - m;
            Rectangle panel = new Rectangle(x, y, w, h);

            spriteBatch.Draw(pixel, panel, new Color(0, 0, 0, 180));

            Vector2 p = new Vector2(x + 10, y + 10);
            spriteBatch.DrawString(font, $"Name: {selectedUnit.Name}", p, Color.White);
            spriteBatch.DrawString(font, $"Class: {selectedUnit.Class}", p + new Vector2(0, 20), Color.White);
            spriteBatch.DrawString(font, $"Weapon: {selectedUnit.Weapon}", p + new Vector2(0, 40), Color.White);
            spriteBatch.DrawString(font, $"AP: {selectedUnit.ActionPoints}", p + new Vector2(0, 60), Color.White);
            spriteBatch.DrawString(font, $"HP: {selectedUnit.Health} / {selectedUnit.MaxHealth}", p + new Vector2(0, 80), Color.White);

            int totalArmor = selectedUnit.GetTotalArmor();
            spriteBatch.DrawString(font, $"Armor: {totalArmor}", p + new Vector2(0, 100), Color.Cyan);

            int mobilityPenalty = selectedUnit.GetMobilityPenalty();
            if (mobilityPenalty > 0)
            {
                spriteBatch.DrawString(font, $"Mobilite: -{mobilityPenalty} PM",
                    p + new Vector2(0, 120), Color.Orange);
            }

            spriteBatch.DrawString(font, $"Niveau: {selectedUnit.Skills.OverallLevel}",
                p + new Vector2(0, 140), Color.Gold);

            // Grenades
            Vector2 grenadePos = p + new Vector2(0, 160);
            spriteBatch.DrawString(font, $"Grenades: {selectedUnit.Grenades.Count}/{selectedUnit.MaxGrenades}",
                grenadePos, Color.Orange);

            for (int i = 0; i < selectedUnit.Grenades.Count; i++)
            {
                var grenade = selectedUnit.Grenades[i];
                string symbol = GrenadeDatabase.GetGrenadeSymbol(grenade.Type);
                Color color = GrenadeDatabase.GetGrenadeColor(grenade.Type);

                spriteBatch.DrawString(font, symbol,
                    grenadePos + new Vector2(i * 30, 20),
                    color, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// Dessine les boutons d'action
        /// </summary>
        public void DrawActionButtons(Unit selectedUnit, MouseState mouse)
        {
            UnitActionButtons.Clear();

            int bw = ActionButtonWidth, bh = ActionButtonHeight;
            int by = graphicsDevice.Viewport.Height - bh - 15;
            int bx = (graphicsDevice.Viewport.Width - bw) / 2;

            var buttons = new List<Button>
            {
                new Button("COUVERT", new Vector2(bx - 220, by)),
                new Button("TIRER", new Vector2(bx - 110, by)),
                new Button("RECHARGER", new Vector2(bx, by))
            };

            if (selectedUnit != null && selectedUnit.Grenades.Count > 0)
            {
                buttons.Add(new Button("GRENADE", new Vector2(bx + 110, by)));
            }

            UnitActionButtons.AddRange(buttons);

            foreach (var btn in UnitActionButtons)
            {
                Rectangle r = new Rectangle((int)btn.Position.X, (int)btn.Position.Y, bw, bh);
                Color c = r.Contains(mouse.Position) ? Color.Orange : Color.DarkOrange;

                spriteBatch.Draw(pixel, r, c);

                Vector2 ts = font.MeasureString(btn.Text);
                spriteBatch.DrawString(font, btn.Text,
                    new Vector2(r.X + (bw - ts.X) / 2, r.Y + (bh - ts.Y) / 2),
                    Color.Black);
            }

            // Bouton CONFIRMER TIR
            if (SelectedFireTarget != null && selectedUnit != null && selectedUnit.ActionPoints > 0)
            {
                int fireConfirmWidth = 140;
                int fireConfirmHeight = 50;
                FireButton = new Rectangle(
                    graphicsDevice.Viewport.Width / 2 - fireConfirmWidth / 2,
                    by - fireConfirmHeight - 10,
                    fireConfirmWidth,
                    fireConfirmHeight
                );

                Color fireColor = FireButton.Contains(mouse.Position) ? Color.Red : Color.DarkRed;
                spriteBatch.Draw(pixel, FireButton, fireColor);

                string fireText = "CONFIRMER TIR";
                Vector2 fireTextSize = font.MeasureString(fireText);
                spriteBatch.DrawString(font, fireText,
                    new Vector2(FireButton.X + (fireConfirmWidth - fireTextSize.X) / 2,
                               FireButton.Y + (fireConfirmHeight - fireTextSize.Y) / 2),
                    Color.White);
            }
            else
            {
                FireButton = Rectangle.Empty;
            }
        }

        /// <summary>
        /// Dessine le bouton de fin de tour
        /// </summary>
        public void DrawEndTurnButton(MouseState mouse)
        {
            int w = 140, h = 36;
            EndTurnButton = new Rectangle(
                graphicsDevice.Viewport.Width - w - 20,
                graphicsDevice.Viewport.Height - h - 20,
                w, h
            );

            EndTurnHovered = EndTurnButton.Contains(mouse.Position);
            Color color = EndTurnHovered ? Color.DarkRed : Color.Red;
            spriteBatch.Draw(pixel, EndTurnButton, color);

            Vector2 txtSize = font.MeasureString("FIN DU TOUR");
            spriteBatch.DrawString(font, "FIN DU TOUR",
                new Vector2(EndTurnButton.X + (w - txtSize.X) / 2,
                           EndTurnButton.Y + (h - txtSize.Y) / 2),
                Color.White);
        }

        /// <summary>
        /// Dessine les icônes de cibles de tir
        /// </summary>
        public void DrawFireTargets(MouseState mouse)
        {
            HoveredFireTarget = null;

            foreach (var ui in FireTargetsUI)
            {
                if (ui.Bounds.Contains(mouse.Position))
                {
                    HoveredFireTarget = ui.Target;
                    break;
                }
            }

            foreach (var ui in FireTargetsUI)
            {
                Color bg = ui.Target == SelectedFireTarget ? Color.OrangeRed : Color.DarkRed;
                spriteBatch.Draw(pixel, ui.Bounds, bg);

                Vector2 size = font.MeasureString(ui.HitChance + "%");
                spriteBatch.DrawString(font, ui.HitChance + "%",
                    new Vector2(ui.Bounds.Center.X - size.X / 2, ui.Bounds.Center.Y - size.Y / 2),
                    Color.White);
            }

            Unit highlight = SelectedFireTarget ?? HoveredFireTarget;
            if (highlight != null)
            {
                var ui = FireTargetsUI.FirstOrDefault(f => f.Target == highlight);
                if (ui != null)
                {
                    spriteBatch.Draw(pixel, ui.Bounds, Color.Red * 0.4f);
                    DrawBorder(ui.Bounds, Color.Red, 3);
                }
            }
        }

        /// <summary>
        /// Vérifie si la souris est sur un bouton d'action
        /// </summary>
        public bool IsMouseOverActionButton(MouseState mouse)
        {
            foreach (var btn in UnitActionButtons)
            {
                Rectangle r = new Rectangle((int)btn.Position.X, (int)btn.Position.Y,
                    ActionButtonWidth, ActionButtonHeight);
                
                if (r.Contains(mouse.Position))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Vérifie si la souris est sur une icône de cible
        /// </summary>
        public bool IsMouseOverFireTargets(MouseState mouse)
        {
            if (!ShowFireTargets)
                return false;

            foreach (var ui in FireTargetsUI)
            {
                if (ui.Bounds.Contains(mouse.Position))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gère le clic sur les icônes de cibles
        /// </summary>
        public bool HandleFireTargetClick(MouseState mouse, Unit selectedUnit)
        {
            if (!ShowFireTargets)
                return false;

            foreach (var ui in FireTargetsUI)
            {
                if (ui.Bounds.Contains(mouse.Position))
                {
                    SelectedFireTarget = ui.Target;

                    if (selectedUnit != null && SelectedFireTarget != null)
                    {
                        float deltaX = SelectedFireTarget.Cell.X - selectedUnit.Cell.X;
                        float deltaZ = SelectedFireTarget.Cell.Y - selectedUnit.Cell.Y;
                        selectedUnit.Orientation = (float)Math.Atan2(deltaX, deltaZ);
                    }

                    return true;
                }
            }

            return false;
        }

        private void DrawBorder(Rectangle rect, Color color, int thickness)
        {
            DrawLine(new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), color, thickness);
            DrawLine(new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom), color, thickness);
            DrawLine(new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Bottom), color, thickness);
            DrawLine(new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom), color, thickness);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            float rotation = (float)Math.Atan2(delta.Y, delta.X);
            spriteBatch.Draw(pixel, start, null, color, rotation, Vector2.Zero,
                new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
    }
}
