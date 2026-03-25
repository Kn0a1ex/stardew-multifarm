using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace MultiFarm
{
    /// <summary>
    /// In-game menu shown to a new player so they can pick their farm type.
    ///
    /// Displayed when a player joins for the first time (no slot assigned).
    /// Renders farm type cards — same types available in the vanilla new-game screen.
    ///
    /// Usage:
    ///   Game1.activeClickableMenu = new FarmSelectionMenu(player, onSelected);
    /// </summary>
    public class FarmSelectionMenu : IClickableMenu
    {
        private readonly Farmer _player;
        private readonly Action<int> _onSelected;   // called with the chosen farm type ID

        private readonly List<FarmCard> _cards = new();
        private int _hoveredIndex = -1;

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

        public FarmSelectionMenu(Farmer player, Action<int> onSelected)
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

        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds,
                   Color.Black * 0.6f);

            // Panel
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                xPositionOnScreen, yPositionOnScreen, width, height, Color.White, 4f);

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
                    new Microsoft.Xna.Framework.Rectangle(0, 256, 60, 60),
                    card.Bounds.X, card.Bounds.Y, card.Bounds.Width, card.Bounds.Height,
                    bg);

                b.DrawString(Game1.smallFont, card.Name,
                    new Vector2(card.Bounds.X + 8, card.Bounds.Y + 8), Game1.textColor);

                // Wrap description text
                string wrapped = WrapText(card.Description, card.Bounds.Width - 16);
                b.DrawString(Game1.tinyFont, wrapped,
                    new Vector2(card.Bounds.X + 8, card.Bounds.Y + 32),
                    hovered ? Color.DarkGoldenrod : Color.DimGray);
            }

            drawMouse(b);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i].Bounds.Contains(x, y))
                {
                    if (playSound) Game1.playSound("select");
                    exitThisMenu();
                    _onSelected(_cards[i].TypeId);
                    return;
                }
            }
        }

        public override void performHoverAction(int x, int y)
        {
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
