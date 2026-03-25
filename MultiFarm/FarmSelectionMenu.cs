using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// In-game menu shown to a new player so they can pick their farm type and name their farm.
    ///
    /// Phase 1: Player picks a farm type card.
    /// Phase 2: Player enters a name for their farm (required).
    ///
    /// Usage:
    ///   Game1.activeClickableMenu = new FarmSelectionMenu(player, onSelected);
    /// </summary>
    public class FarmSelectionMenu : IClickableMenu
    {
        private readonly Farmer _player;
        private readonly Action<int, string> _onSelected;   // (farmTypeId, farmName)

        private readonly List<FarmCard> _cards = new();
        private int _hoveredIndex = -1;

        // Name-entry phase
        private bool _nameEntryPhase = false;
        private int _chosenTypeId = 0;
        private TextBox? _nameBox;
        private Rectangle _okButton;
        private string _errorMessage = "";

        // Vanilla farm type data
        private static readonly (string Name, string Description, int TypeId)[] FarmTypes =
        {
            ("Standard",     "A balanced farm with plenty of open space.",              0),
            ("Riverland",    "More water than land. Great for fishing.",                1),
            ("Forest",       "A forested farm. Forage and stumps respawn daily.",       2),
            ("Hill-top",     "Rocky terrain with a quarry for mining.",                 3),
            ("Wilderness",   "Monsters roam at night. For the adventurous.",            4),
            ("Four Corners", "Four distinct sections with varied resources.",            5),
            ("Meadowlands",  "Wide open pastures. Perfect for animal farming.",         6),
        };

        public FarmSelectionMenu(Farmer player, Action<int, string> onSelected)
            : base(Game1.uiViewport.Width / 2 - 500, Game1.uiViewport.Height / 2 - 300, 1000, 600)
        {
            _player     = player;
            _onSelected = onSelected;
            BuildCards();
        }

        private void BuildCards()
        {
            var allowed = ModEntry.Instance.Config.AllowedFarmTypes;
            int x = xPositionOnScreen + 40;
            int y = yPositionOnScreen + 80;
            int cardW = 130, cardH = 160, gap = 10;
            int col = 0;

            foreach (var (name, desc, id) in FarmTypes)
            {
                if (!System.Array.Exists(allowed, t => t == id)) continue;

                _cards.Add(new FarmCard
                {
                    TypeId      = id,
                    Name        = name,
                    Description = desc,
                    Bounds      = new Rectangle(x + col * (cardW + gap), y, cardW, cardH),
                });
                col++;
                if (col >= 4) { col = 0; y += cardH + gap; }
            }
        }

        private void EnterNamePhase(int typeId)
        {
            _chosenTypeId  = typeId;
            _nameEntryPhase = true;
            _errorMessage   = "";

            int boxX = xPositionOnScreen + width / 2 - 200;
            int boxY = yPositionOnScreen + height / 2 - 10;

            _nameBox = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null,
                Game1.smallFont,
                Game1.textColor)
            {
                X        = boxX,
                Y        = boxY,
                Width    = 400,
                Selected = true,
            };

            _okButton = new Rectangle(boxX + 150, boxY + 56, 100, 44);
        }

        private void TryConfirmName()
        {
            string name = _nameBox?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                _errorMessage = "Please enter a name for your farm.";
                return;
            }
            Game1.playSound("select");
            exitThisMenu();
            _onSelected(_chosenTypeId, name);
        }

        // Prevent the menu from being dismissed by any means other than completing the flow.
        public override bool readyToClose() => false;
        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        public override void receiveKeyPress(Keys key)
        {
            if (_nameEntryPhase)
            {
                if (key == Keys.Escape) { _nameEntryPhase = false; _errorMessage = ""; }
                if (key == Keys.Enter)  TryConfirmName();
                // TextBox handles character input internally via Update()
            }
            // Suppress all other key presses so the menu cannot be closed
        }

        public override void update(GameTime time)
        {
            base.update(time);
            _nameBox?.Update();
        }

        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds,
                   Color.Black * 0.6f);

            // Panel
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                xPositionOnScreen, yPositionOnScreen, width, height, Color.White, 4f);

            if (_nameEntryPhase)
                DrawNameEntry(b);
            else
                DrawTypeSelection(b);

            drawMouse(b);
        }

        private void DrawTypeSelection(SpriteBatch b)
        {
            // Title
            b.DrawString(Game1.dialogueFont, $"Choose Your Farm, {_player.Name}:",
                new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 24),
                Game1.textColor);

            // Cards
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool hovered = (i == _hoveredIndex);
                Color bg = hovered ? Color.LightGoldenrodYellow : Color.White;

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    card.Bounds.X, card.Bounds.Y, card.Bounds.Width, card.Bounds.Height,
                    bg);

                b.DrawString(Game1.smallFont, card.Name,
                    new Vector2(card.Bounds.X + 8, card.Bounds.Y + 8), Game1.textColor);

                string wrapped = WrapText(card.Description, card.Bounds.Width - 16);
                b.DrawString(Game1.tinyFont, wrapped,
                    new Vector2(card.Bounds.X + 8, card.Bounds.Y + 32),
                    hovered ? Color.DarkGoldenrod : Color.DimGray);
            }
        }

        private void DrawNameEntry(SpriteBatch b)
        {
            // Title
            b.DrawString(Game1.dialogueFont, $"Name Your Farm, {_player.Name}:",
                new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 24),
                Game1.textColor);

            // Prompt
            string prompt = "What would you like to call your farm?";
            b.DrawString(Game1.smallFont, prompt,
                new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 80),
                Game1.textColor);

            // TextBox
            _nameBox?.Draw(b);

            // OK button
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                _okButton.X, _okButton.Y, _okButton.Width, _okButton.Height, Color.White, 4f);
            Utility.drawTextWithShadow(b, "OK", Game1.smallFont,
                new Vector2(
                    _okButton.X + (_okButton.Width  - Game1.smallFont.MeasureString("OK").X) / 2f,
                    _okButton.Y + (_okButton.Height - Game1.smallFont.MeasureString("OK").Y) / 2f),
                Game1.textColor);

            // Error message
            if (!string.IsNullOrEmpty(_errorMessage))
                b.DrawString(Game1.smallFont, _errorMessage,
                    new Vector2(xPositionOnScreen + 40, _okButton.Y + _okButton.Height + 8),
                    Color.Red);

            // Back hint
            b.DrawString(Game1.tinyFont, "Press Escape to go back",
                new Vector2(xPositionOnScreen + 40, yPositionOnScreen + height - 40),
                Color.DimGray);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_nameEntryPhase)
            {
                if (_okButton.Contains(x, y))
                    TryConfirmName();
                return;
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Bounds.Contains(x, y))
                {
                    if (playSound) Game1.playSound("select");
                    EnterNamePhase(_cards[i].TypeId);
                    return;
                }
            }
        }

        public override void performHoverAction(int x, int y)
        {
            if (_nameEntryPhase) return;

            _hoveredIndex = -1;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Bounds.Contains(x, y))
                {
                    _hoveredIndex = i;
                    break;
                }
            }
        }

        private static string WrapText(string text, int maxWidth)
        {
            var words  = text.Split(' ');
            var result = new System.Text.StringBuilder();
            var line   = new System.Text.StringBuilder();
            foreach (var word in words)
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (Game1.tinyFont.MeasureString(candidate).X > maxWidth)
                {
                    result.AppendLine(line.ToString().TrimEnd());
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            if (line.Length > 0) result.Append(line);
            return result.ToString();
        }

        private class FarmCard
        {
            public int TypeId      { get; set; }
            public string Name        { get; set; } = "";
            public string Description { get; set; } = "";
            public Rectangle Bounds  { get; set; }
        }
    }
}
