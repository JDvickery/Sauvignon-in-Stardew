using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Sauvignon_in_Stardew
{
    public class DistillerMenu : LevelUpMenu
    {
        public ClickableComponent middleProfession;
        public Color middleProfessionColor = Game1.textColor;
        public List<string> middleProfessionDescription = new List<string>();

        public DistillerMenu(int skill, int level) : base(skill, level)
        {
            this.width = (int)Math.Round(Game1.viewport.Width * 0.8, 0);
            this.xPositionOnScreen = 100;
            this.leftProfession = new ClickableComponent(new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen + 128, this.width / 2, this.height), "")
            {
                myID = 102,
                rightNeighborID = 104
            };
            this.middleProfession = new ClickableComponent(new Rectangle(this.width / 3 + this.xPositionOnScreen, this.yPositionOnScreen + 128, this.width / 3, this.height), "")
            {
                myID = 104,
                leftNeighborID = 102,
                rightNeighborID = 103
            };
            this.rightProfession = new ClickableComponent(new Rectangle(this.width / 3 + this.xPositionOnScreen + this.width /3, this.yPositionOnScreen + 128, this.width / 3 + this.width / 3, this.height), "")
            {
                myID = 103,
                leftNeighborID = 104
            };
        }
    }
}
