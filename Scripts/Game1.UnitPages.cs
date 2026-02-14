using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace XCOM_3
{
    public partial class Game1
    {
        private void HandleUnitPageShortcuts(bool inventoryPressed, KeyboardState keyboard)
        {
            if (!CanUseUnitPageShortcuts())
                return;

            ToggleUnitPageIfPressed(inventoryPressed, showInventory, UnitPageTab.Inventory);

            bool skillsPressed = keyboard.IsKeyDown(Keys.K) && previousKeyboardState.IsKeyUp(Keys.K);
            ToggleUnitPageIfPressed(skillsPressed, statsPanel.IsVisible, UnitPageTab.Skills);

            bool infoPressed = keyboard.IsKeyDown(Keys.C) && previousKeyboardState.IsKeyUp(Keys.C);
            ToggleUnitPageIfPressed(infoPressed, characterInfoPanel.IsVisible, UnitPageTab.Info);
        }

        private bool CanUseUnitPageShortcuts() =>
            currentState == GameState.Playing && selectedUnit?.Team == Team.Player;

        private void ToggleUnitPageIfPressed(bool keyPressed, bool pageVisible, UnitPageTab tab)
        {
            if (!keyPressed)
                return;

            if (pageVisible)
                CloseUnitPages();
            else
                OpenUnitPage(tab);
        }

        private bool IsTabPressed(KeyboardState keyboard) =>
            keyboard.IsKeyDown(Keys.Tab) && previousKeyboardState.IsKeyUp(Keys.Tab);

        private void OpenUnitPage(UnitPageTab tab)
        {
            showInventory = tab == UnitPageTab.Inventory;

            if (tab == UnitPageTab.Skills)
                statsPanel.Show();
            else
                statsPanel.Hide();

            if (tab == UnitPageTab.Info)
                characterInfoPanel.Show();
            else
                characterInfoPanel.Hide();
        }

        private void CloseUnitPages()
        {
            showInventory = false;
            statsPanel.Hide();
            characterInfoPanel.Hide();
        }

        private bool IsAnyUnitPageOpen() => showInventory || statsPanel.IsVisible || characterInfoPanel.IsVisible;

        private Rectangle GetUnitTabRect(int tabIndex)
        {
            int totalWidth = TabWidth * 3 + TabSpacing * 2;
            int startX = (GraphicsDevice.Viewport.Width - totalWidth) / 2;
            int x = startX + tabIndex * (TabWidth + TabSpacing);
            return new Rectangle(x, TabTopMargin, TabWidth, TabHeight);
        }

        private bool TryHandleUnitTabClick(Point mousePosition)
        {
            if (currentState != GameState.Playing || selectedUnit?.Team != Team.Player || !IsAnyUnitPageOpen())
                return false;

            if (GetUnitTabRect(0).Contains(mousePosition))
            {
                OpenUnitPage(UnitPageTab.Inventory);
                return true;
            }

            if (GetUnitTabRect(1).Contains(mousePosition))
            {
                OpenUnitPage(UnitPageTab.Skills);
                return true;
            }

            if (GetUnitTabRect(2).Contains(mousePosition))
            {
                OpenUnitPage(UnitPageTab.Info);
                return true;
            }

            return false;
        }

        private void DrawUnitPageTabs()
        {
            if (currentState != GameState.Playing || selectedUnit?.Team != Team.Player || !IsAnyUnitPageOpen())
                return;

            DrawUnitTab(GetUnitTabRect(0), "Inventaire (I)", showInventory);
            DrawUnitTab(GetUnitTabRect(1), "Compétences (K)", statsPanel.IsVisible);
            DrawUnitTab(GetUnitTabRect(2), "Information (C)", characterInfoPanel.IsVisible);
        }

        private void DrawUnitTab(Rectangle rect, string label, bool isActive)
        {
            Color background = isActive ? new Color(24, 72, 128, 230) : new Color(22, 22, 22, 210);
            Color border = isActive ? new Color(255, 220, 130) : new Color(110, 110, 110);
            Color text = isActive ? Color.White : new Color(210, 210, 210);

            _spriteBatch.Draw(pixel, rect, background);

            int borderThickness = 2;
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, borderThickness), border);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - borderThickness, rect.Width, borderThickness), border);
            _spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, borderThickness, rect.Height), border);
            _spriteBatch.Draw(pixel, new Rectangle(rect.Right - borderThickness, rect.Y, borderThickness, rect.Height), border);

            Vector2 textSize = font.MeasureString(label);
            Vector2 textPos = new Vector2(
                rect.X + (rect.Width - textSize.X) / 2f,
                rect.Y + (rect.Height - textSize.Y) / 2f);
            _spriteBatch.DrawString(font, label, textPos, text);
        }

    }
}
