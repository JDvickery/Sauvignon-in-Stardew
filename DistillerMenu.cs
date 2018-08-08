using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System.Reflection;

namespace Sauvignon_in_Stardew
{
    class DistillerMenu : LevelUpMenu
    {

        private Color thirdProfessionColor = Game1.textColor;
        private List<string> thirdProfessionDescription = new List<string>();
        public ClickableComponent thirdProfession;
        private List<int> professionsToChoose = new List<int>();

        public DistillerMenu() : base()
        {

        }

        public DistillerMenu(int skill, int level) : base(skill, level)
        {         
            this.xPositionOnScreen = 100;
            this.width = (int)Math.Round(base.width * 1.4, 0);

            this.professionsToChoose.Clear();

            this.professionsToChoose.Add(4);
            this.professionsToChoose.Add(5);
            this.professionsToChoose.Add(77);

            this.thirdProfessionDescription.Add("");
            this.thirdProfessionDescription.Add("All alcohol (beer, wine, etc.)");
            this.thirdProfessionDescription.Add("worth 40% more.");

            this.thirdProfessionColor = Game1.textColor;
            
            this.leftProfession = new ClickableComponent(new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen + 128, this.width / 3, this.height), "")
            {
                myID = 102,
                rightNeighborID = 103
            };

            this.rightProfession = new ClickableComponent(new Rectangle(this.width / 3 + this.xPositionOnScreen, this.yPositionOnScreen + 128, this.width / 3, this.height), "")
            {
                myID = 103,
                leftNeighborID = 102
            };
            

            this.thirdProfession = new ClickableComponent(new Rectangle( (this.width / 3) * 2 + this.xPositionOnScreen, this.yPositionOnScreen + 128, (this.width / 3) * 2, this.height), "")
            {
                myID = 104,
                leftNeighborID = 103
            };

            this.populateClickableComponentList();
            this.snapToDefaultClickableComponent();

            for(int i = 0; i < this.allClickableComponents.Count; i++)
            {
                if (this.allClickableComponents[i].myID == 102 || this.allClickableComponents[i].myID == 103 || this.allClickableComponents[i].myID == 104)
                {
                    ModEntry.monitor.Log($"Clickable Components are " + this.allClickableComponents[i].myID);
                }
            }            
        }

        public override void snapToDefaultClickableComponent()
        {
            this.currentlySnappedComponent = this.getComponentWithID(104);
            Game1.setMousePosition(this.xPositionOnScreen + this.width + 64, this.yPositionOnScreen + this.height + 64);
        }

        public override void draw(SpriteBatch b)
        {
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true, (string)null, false);

            this.drawHorizontalPartition(b, this.yPositionOnScreen + 192, false);

            this.drawVerticalIntersectingPartition(b, this.xPositionOnScreen + this.width / 3 - 32, this.yPositionOnScreen + 192);
            this.drawVerticalIntersectingPartition(b, this.xPositionOnScreen + this.width / 3 - 32 + this.width / 3, this.yPositionOnScreen + 192);

            Utility.drawWithShadow(b, Game1.buffsIcons, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 16)), (Rectangle)typeof(LevelUpMenu).GetField("sourceRectForLevelIcon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this), Color.White, 0.0f, Vector2.Zero, 4f, false, 0.88f, -1, -1, 0.35f);

            b.DrawString(Game1.dialogueFont, (string)typeof(LevelUpMenu).GetField("title", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this), new Vector2((float)(this.xPositionOnScreen + this.width / 2) - Game1.dialogueFont.MeasureString((string)typeof(LevelUpMenu).GetField("title", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this)).X / 2f, (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 16)), Game1.textColor);

            Utility.drawWithShadow(b, Game1.buffsIcons, new Vector2((float)(this.xPositionOnScreen + this.width - IClickableMenu.spaceToClearSideBorder - IClickableMenu.borderWidth - 64), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 16)), (Rectangle)typeof(LevelUpMenu).GetField("sourceRectForLevelIcon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this), Color.White, 0.0f, Vector2.Zero, 4f, false, 0.88f, -1, -1, 0.35f);

            string text = Game1.content.LoadString("Strings\\UI:LevelUp_ChooseProfession");

            b.DrawString(Game1.smallFont, text, new Vector2((float)(this.xPositionOnScreen + this.width / 2) - Game1.smallFont.MeasureString(text).X / 2f, (float)(this.yPositionOnScreen + 64 + IClickableMenu.spaceToClearTopBorder)), Game1.textColor);

            //first profession
            List<string> leftProf_temp = (List<string>)typeof(LevelUpMenu).GetField("leftProfessionDescription", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
            b.DrawString(Game1.dialogueFont, leftProf_temp[0], new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + 32), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 160)), (Color)typeof(LevelUpMenu).GetField("leftProfessionColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this));

            b.Draw(Game1.mouseCursors, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width / 3 - 112), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 160 - 16)), new Rectangle?(new Rectangle(this.professionsToChoose[0] % 6 * 16, 624 + this.professionsToChoose[0] / 6 * 16, 16, 16)), Color.White, 0.0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            for (int index = 1; index < ( (List<string>)typeof(LevelUpMenu).GetField("leftProfessionDescription", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this) ).Count; ++index)
            {
                b.DrawString(Game1.smallFont, Game1.parseText(leftProf_temp[index], Game1.smallFont, this.width / 3 - 64), new Vector2((float)(this.xPositionOnScreen - 4 + IClickableMenu.spaceToClearSideBorder + 32), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 128 + 8 + 64 * (index + 1))), (Color)typeof(LevelUpMenu).GetField("leftProfessionColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this));
            }

            //second profession
            List<string> rightProf_temp = (List<string>)typeof(LevelUpMenu).GetField("rightProfessionDescription", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
            b.DrawString(Game1.dialogueFont, rightProf_temp[0], new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width / 3), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 160)), (Color)typeof(LevelUpMenu).GetField("rightProfessionColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this));

            b.Draw(Game1.mouseCursors, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width - 128), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 160 - 16)), new Rectangle?(new Rectangle(this.professionsToChoose[1] % 6 * 16, 624 + this.professionsToChoose[1] / 6 * 16, 16, 16)), Color.White, 0.0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            for (int index = 1; index < ( (List<string>)typeof(LevelUpMenu).GetField("rightProfessionDescription", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this) ).Count; ++index)
            {
                b.DrawString(Game1.smallFont, Game1.parseText(rightProf_temp[index], Game1.smallFont, this.width / 3 - 48), new Vector2((float)(this.xPositionOnScreen - 4 + IClickableMenu.spaceToClearSideBorder + this.width / 3), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 128 + 8 + 64 * (index + 1))), (Color)typeof(LevelUpMenu).GetField("rightProfessionColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this));
            }
            
            //third profession
            b.DrawString(Game1.dialogueFont, "Distiller", new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width / 3 + this.width / 3), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 160)), this.thirdProfessionColor);

            b.Draw(Game1.mouseCursors, new Vector2((float)(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + this.width - 128), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 160 - 16)), new Rectangle?(new Rectangle(this.professionsToChoose[2] % 6 * 16, 624 + this.professionsToChoose[2] / 6 * 16, 16, 16)), Color.White, 0.0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            for (int index = 1; index < this.thirdProfessionDescription.Count; ++index)
            {
                b.DrawString(Game1.smallFont, Game1.parseText(this.thirdProfessionDescription[index], Game1.smallFont, (this.width / 3 - 48) + this.width / 3 - 48), new Vector2((float)(this.xPositionOnScreen - 4 + IClickableMenu.spaceToClearSideBorder + this.width / 3 + this.width / 3), (float)(this.yPositionOnScreen + IClickableMenu.spaceToClearTopBorder + 128 + 8 + 64 * (index + 1))), this.thirdProfessionColor);
            }

            this.drawMouse(b);
        }
    }
}
