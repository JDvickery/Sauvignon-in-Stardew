using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using StardewModdingAPI;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley;

using xTile;

using SObject = StardewValley.Object;
using StardewModdingAPI.Events;
using System.Reflection;

using Harmony;
using Microsoft.Xna.Framework.Input;
using StardewValley.Objects;
using System.Linq;
using xTile.Layers;
using xTile.Tiles;
using SauvignonInStardew;

namespace Sauvignon_in_Stardew
{
    class ModEntry : Mod, IAssetLoader, IAssetEditor
    {
        //Config
        private ModConfig Config;
        public bool DistillerProfessionActive;
        /*
         * FIELDS
         * 
         */
        public static IModHelper helper;
        public static IMonitor monitor;

        public Texture2D Winery_outdoors;
        public Map Winery_indoors;

        public TileSheet tileSheet;
        public Layer layer;
        public const int tileID = 131;

        public List<KeyValuePair<int, int>> wineryCoords;

        public readonly Dictionary<string, string> dataForBlueprint = new Dictionary<string, string>() { ["Winery"] = "709 200 330 100 390 100/11/6/5/5/-1/-1/Winery/Winery/Kegs and Casks inside work 30% faster and display time remaining./Buildings/none/96/96/20/null/Farm/20000/false" };

        public readonly Vector2 TooltipOffset = new Vector2(Game1.tileSize / 2);
        public readonly Rectangle TooltipSourceRect = new Rectangle(0, 256, 60, 60);

        public string CurrentSeason;

        public int bedTime;
        public int hoursSlept;

        public Texture2D distillerIcon;

        public string sleepBox;
        public bool ranOnce = false;

        public bool spaceCore;
        /*
         * END FIELDS
         * 
         */



        /*
         * ENTRY
         * 
         */
        public override void Entry(IModHelper helper)
        {
            ModEntry.monitor = this.Monitor;
            ModEntry.helper = helper;

            this.Config = helper.ReadConfig<ModConfig>();
            this.DistillerProfessionActive = this.Config.DistillerProfessionBool;

            //Loaded Textures for outside and indside the Winery
            Winery_outdoors = helper.Content.Load<Texture2D>($"assets/Winery_outside_{Game1.currentSeason}.png", ContentSource.ModFolder);
            Winery_indoors = helper.Content.Load<Map>("assets/Winery.tbin", ContentSource.ModFolder);

            //Loaded texture for Distiller Icon
            distillerIcon = helper.Content.Load<Texture2D>("assets/Distiller_icon.png", ContentSource.ModFolder);

            //Load string for sleeping dialogue
            sleepBox = Game1.content.LoadString("Strings\\Locations:FarmHouse_Bed_GoToSleep");

            //Event for adding blueprint to carpenter menu
            MenuEvents.MenuChanged += MenuEvents_MenuChanged;

            //Event for Keg Speed
            TimeEvents.TimeOfDayChanged += TimeEvents_TimeOfDayChanged;

            //Event for Cask Speed
            TimeEvents.AfterDayStarted += TimeEvents_AfterDayStarted;

            //Event for showing time remaining on hover
            GraphicsEvents.OnPostRenderEvent += GraphicsEvents_OnPostRenderEvent;
            //GraphicsEvents.OnPreRenderEvent += GraphicsEvents_OnPreRenderEvent;

            //Events for editing Winery width
            LocationEvents.BuildingsChanged += LocationEvents_BuildingsChanged;

            //Event for proventing buidling overlays
            MenuEvents.MenuClosed += MenuEvents_MenuClosed;

            //Event for fixing skills menu
            GraphicsEvents.OnPostRenderGuiEvent += GraphicsEvents_OnPostRenderGuiEvent;

            //Event for Bonus Price and Bed Time if dead or faint
            GameEvents.UpdateTick += GameEvents_UpdateTick;

            /*
             * Events for save and loading
             * 
             */
            SaveEvents.BeforeSave += SaveEvents_BeforeSave;

            SaveEvents.AfterSave += SaveEvents_AfterSaveLoad;
            SaveEvents.AfterLoad += SaveEvents_AfterSaveLoad;
            SaveEvents.AfterLoad += DisplayDistillerInfo;
            /*
             * End of Events for save and loading
             * 
             */

            spaceCore = helper.ModRegistry.IsLoaded("spacechase0.SpaceCore");


            /*
             * HARMONY PATCHING
             * 
             */
            var harmony = HarmonyInstance.Create("com.jesse.winery");
            Type type = typeof(Cask);
            MethodInfo method = type.GetMethod("performObjectDropInAction");
            HarmonyMethod patchMethod = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_performObjectDropInAction)));
            harmony.Patch(method, patchMethod, null);

            if (this.DistillerProfessionActive)
            {
                Type type2 = typeof(SObject);
                MethodInfo method2 = type2.GetMethod("getCategoryColor");
                HarmonyMethod patchMethod2 = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_getCategoryColor)));
                harmony.Patch(method2, patchMethod2, null);

                MethodInfo method3 = type2.GetMethod("getCategoryName");
                HarmonyMethod patchMethod3 = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_getCategoryName)));
                harmony.Patch(method3, patchMethod3, null);
            }
            /*
             * END OF HARMONY PATCHING
             * 
             */
        }
        /*
        * END ENTRY
        * 
        */


        /*
         * GET ALL GAME LOCATIONS INCLUDING CUSTOM ONES
         * From PathosChild
         */
        public static IEnumerable<GameLocation> GetLocations()
        {
            return Game1.locations
                .Concat(
                    from location in Game1.locations.OfType<BuildableGameLocation>()
                    from building in location.buildings
                    where building.indoors.Value != null
                    select building.indoors.Value
                );
        }
        /*
         * END GET GAME LOCATIONS
         * 
         */


        /*
         * Set Sleep and Prices on death or faint
         * Set ranOnce back to false at start of day
         * Remove Distiller profession if player is level 10 and does not have tiller
         */
        private void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            if ((Game1.player.health <= 0 || Game1.player.stamina <= 0) && !ranOnce)
            {
                bedTime = Game1.timeOfDay;
                SetBonusPrice();
            }

            if (this.DistillerProfessionActive && Game1.player.professions.Contains(77) && Game1.player.FarmingLevel > 9 && !(Game1.player.professions.Contains(1)))
            {
                Game1.player.professions.Remove(77);
            }
        }
        /*
         * End Sleep and Faint
         * 
         */


        /*
        * Log Distiller info
        */
        public void DisplayDistillerInfo(object sender, EventArgs e)
        {
            if (DistillerProfessionActive)
            {
                monitor.Log("Distiller Profession is Active", LogLevel.Info);
                if (Game1.player.professions.Contains(77))
                {
                    monitor.Log("You are a Distiller", LogLevel.Info);
                }
                else
                {
                    monitor.Log("You are not a Distiller. Reach Level 10 Farming or go to the Statue in the Sewers to reset your Farming Professions.", LogLevel.Info);
                }
            }
            else
            {
                monitor.Log("Distiller Profession is Inactive", LogLevel.Info);
            }
        }


        /*
        * DRAW TO SKILLS PAGE 
        * draw icon and Distiller profession info in Skills Page
        */
        private void GraphicsEvents_OnPostRenderGuiEvent(object sender, EventArgs e)
        {
            if (Game1.activeClickableMenu is GameMenu menu && menu.currentTab == 1 && Game1.player.professions.Contains(77) && !Game1.player.professions.Contains(4))
            {
                List<IClickableMenu> pages = helper.Reflection.GetField<List<IClickableMenu>>(menu, "pages").GetValue();
                foreach (IClickableMenu page in pages)
                {
                    if ( page is SkillsPage || page.GetType().FullName == "SpaceCore.Interface.NewSkillsPage" )
                    {
                        List<ClickableTextureComponent> skillBars = helper.Reflection.GetField<List<ClickableTextureComponent>>(page, "skillBars").GetValue();
                        foreach (ClickableTextureComponent skillBar in skillBars)
                        {
                            if (this.DistillerProfessionActive && skillBar.containsPoint(Game1.getMouseX(), Game1.getMouseY()) && skillBar.myID == 200)
                            {
                                //local variables                                
                                string textTitle = "Distiller";
                                string textDescription = "Alcohol worth 40% more.";

                                //draw    
                                //icon
                                IClickableMenu.drawTextureBox(Game1.spriteBatch, skillBar.bounds.X - 16 - 8, skillBar.bounds.Y - 16 - 16, 96, 96, Color.White);
                                Game1.spriteBatch.Draw(distillerIcon, new Vector2((float)(skillBar.bounds.X - 8), (float)(skillBar.bounds.Y - 32 + 16)), new Rectangle(0, 0, 16, 16), Color.White, 0.0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
                                //box
                                IClickableMenu.drawHoverText(Game1.spriteBatch, textDescription, Game1.smallFont, 0, 0, -1, textTitle.Length > 0 ? textTitle : (string)null, -1, (string[])null, (Item)null, 0, -1, -1, -1, -1, 1f, (CraftingRecipe)null);

                                if (!Game1.options.hardwareCursor)
                                {
                                    Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2(Game1.getMouseX(), Game1.getMouseY()), Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, Game1.options.SnappyMenus ? 44 : 0 , 16, 16), Color.White * Game1.mouseCursorTransparency, 0.0f, Vector2.Zero, Game1.pixelZoom + Game1.dialogueButtonScale / 150f, SpriteEffects.None, 1f);
                                }
                            }
                        }
                    }
                }
            }
        }
        /*
        * END DRAW TO SKILLS PAGE 
        * 
        */



        /*
         * ADD WINERY TO CARPENTER MENU
         * 
         * CHANGE FARMING LEVEL UP 10 MENU TO DISTILLER MENU
         * 
         * SAVE BED TIME FOR KEG BONUS OVERNIGHT
         */
        public void MenuEvents_MenuChanged(object sender, EventArgsClickableMenuChanged e)
        {
            if(e.NewMenu is DialogueBox box && box.getCurrentString() == sleepBox)
            {
                bedTime = Game1.timeOfDay;
                SetBonusPrice();
            }

            //monitor.Log($"Current menu type is " + e.NewMenu);            
            if (this.DistillerProfessionActive)
            {
                if (!(Game1.activeClickableMenu is DistillerMenu) && Game1.activeClickableMenu is LevelUpMenu lvlMenu && lvlMenu.isProfessionChooser == true && typeof(LevelUpMenu).GetField("currentSkill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lvlMenu).Equals(0) && typeof(LevelUpMenu).GetField("currentLevel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lvlMenu).Equals(10))
                {
                    Game1.activeClickableMenu = new DistillerMenu(0, 10, distillerIcon);
                    //monitor.Log($"Player professions are "+Game1.player.professions.ToString());
                }
            }           

            if (e.NewMenu is CarpenterMenu)
            {
                //Sets current Winery buildings to 11 width to stop overlay and removes invisible tiles for building moving 
                foreach (Building building in Game1.getFarm().buildings)
                {
                    if (building.indoors.Value != null && building.buildingType.Value.Equals("Winery"))
                    {
                        RemoveArch(building);
                    }
                }
                if (!IsMagical(e.NewMenu) && !HasBluePrint(e.NewMenu))
                {
                    BluePrint wineryBluePrint = new BluePrint("Slime Hutch")
                    {
                        name = "Winery",
                        displayName = "Winery",
                        description = "Kegs and Casks inside work 30% faster and display time remaining.",
                        daysToConstruct = 4,//4
                        moneyRequired = 40000 //40000
                    };
                    wineryBluePrint.itemsRequired.Clear();
                    wineryBluePrint.itemsRequired.Add(709, 200);//200
                    wineryBluePrint.itemsRequired.Add(330, 100);//100
                    wineryBluePrint.itemsRequired.Add(390, 100);//100

                    SetBluePrintField(wineryBluePrint, "textureName", "Buildings\\Winery");
                    SetBluePrintField(wineryBluePrint, "texture", Game1.content.Load<Texture2D>(wineryBluePrint.textureName));

                    GetBluePrints(e.NewMenu).Add(wineryBluePrint);
                }
            }
        }

        public static bool IsMagical(IClickableMenu menu)
        {
            return helper.Reflection.GetField<bool>(menu, "magicalConstruction").GetValue();
            //return (bool)typeof(CarpenterMenu).GetField("magicalConstruction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        public static bool HasBluePrint(IClickableMenu menu)
        {
            return GetBluePrints(menu).Exists(bluePrint => bluePrint.name == "Winery");
        }

        public static List<BluePrint> GetBluePrints(IClickableMenu menu)
        {
            return helper.Reflection.GetField<List<BluePrint>>(menu, "blueprints").GetValue();
            //return (List<BluePrint>)typeof(CarpenterMenu).GetField("blueprints", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        public static void SetBluePrintField(BluePrint bluePrint, string field, object value)
        {
            helper.Reflection.GetField<object>(bluePrint, field).SetValue(value);
            //typeof(BluePrint).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(bluePrint, value);
        }

        //sets back Winery widths to 8 for Archway walkthrough and add back invisible tiles
        private void MenuEvents_MenuClosed(object sender, EventArgsClickableMenuClosed e)
        {           
            if (e.PriorMenu is CarpenterMenu)
            {
                foreach(Building building in Game1.getFarm().buildings)
                {
                    if(building.indoors.Value != null && building.buildingType.Value.Equals("Winery"))
                    {
                        AddArch(building);
                    }
                }
            }            
        }
        /*
         * END OF ADDING WINERY TO CARPENTER MENU
         * 
         */



        /*
        * ADD AND REMOVE ARCHWAY
        * Change width of building and add/remove invisible tiles
        */
        public void AddArch(Building building)
        {
            building.tilesWide.Value = 8;
            layer = Game1.getFarm().map.GetLayer("Buildings");
            foreach(TileSheet sheet in Game1.getFarm().map.TileSheets)
            {
                if (sheet.ImageSource != null && ( sheet.ImageSource.Contains("outdoor") || sheet.ImageSource.Contains("Outdoor")))
                {
                    tileSheet = sheet;
                }
            }            
            //tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
            //tileID = 131;
            for (int x = building.tileX.Value + 9; x < building.tileX.Value + 11; x++)
            {
                for (int y = building.tileY.Value; y < building.tileY.Value + 6; y++)
                {
                    layer.Tiles[x, y] = new StaticTile(layer, tileSheet, BlendMode.Alpha, tileID);
                }
            }
            if (building.daysOfConstructionLeft.Value > 0)
            {
                building.tilesWide.Value = 11;
            }
        }

        public void RemoveArch(Building building)
        {
            building.tilesWide.Value = 11;
            for (int x = building.tileX.Value + 9; x < building.tileX.Value + 11; x++)
            {
                for (int y = building.tileY.Value; y < building.tileY.Value + 6; y++)
                {
                    Game1.getFarm().removeTile(x, y, "Buildings");
                }
            }
        }
        /*
        * END ADD AND REMOVE ARCHWAY
        * 
        */




        /*
         * EDIT WINERY WIDTH
         * calls AddArch or RemoveArch depending on what the player did.
         */
        public void LocationEvents_BuildingsChanged(object sender, EventArgsLocationBuildingsChanged e)
        {
            foreach(Building building in e.Added)
            {
                if(building.indoors.Value != null && building.buildingType.Value == "Winery")
                {
                    AddArch(building);
                }                
            }
            foreach (Building building in e.Removed)
            {
                if (building.indoors.Value != null && building.buildingType.Value == "Winery")
                {
                    RemoveArch(building);
                }
            }
        }
        /*
         * END EDIT WINERY WIDTH
         * 
         */



        /*
         * SAVE AND LOADING
         * Removing the Wineries, saving their coordinates, replacing with Slime Hutches, 
         * adding the Artisan profession, changing the category back to Artisan Good,
         * and reloading the wineries, changing back the category, and removing the Artisan profession
         * if they have the Distiller profession.
         */
        public void SaveEvents_AfterSaveLoad(object sender, EventArgs e)
        {            
            //Remove Artisan Profession if the have selected Distiller Profession
            if (this.DistillerProfessionActive && Game1.player.professions.Contains(77) && !Game1.player.professions.Contains(5))
            {
                Game1.player.professions.Remove(4);
            }            

            wineryCoords = this.Helper.ReadJsonFile<List<KeyValuePair<int, int>>>($"{Constants.CurrentSavePath}/Winery_Coords.json") ?? new List<KeyValuePair<int, int>>();
            foreach (Building b in Game1.getFarm().buildings)
            {
                foreach (var pair in wineryCoords)
                {
                    if (b.tileX.Value == pair.Key && b.tileY.Value == pair.Value && b.buildingType.Value.Equals("Slime Hutch"))
                    {
                        b.buildingType.Value = "Winery";
                        b.indoors.Value.mapPath.Value = "Maps\\Winery";                        
                        b.indoors.Value.updateMap();
                        AddArch(b);                        
                    }
                }
            }
        }

        public void SaveEvents_BeforeSave(object sender, EventArgs e)
        {
            //Add Artisan Profession
            if (this.DistillerProfessionActive && Game1.player.professions.Contains(77) && !Game1.player.professions.Contains(5))
            {
                Game1.player.professions.Add(4);               
            }            
            SetItemCategory(-26);

            //calculate time slept            
            if (bedTime > 0)
            {
                hoursSlept = ((2400 - bedTime) + Game1.timeOfDay);
            }

            //reduce time for kegs overnight
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                {
                    foreach (SObject o in b.indoors.Value.Objects.Values)
                    {
                        if (o.Name.Equals("Keg"))
                        {
                            o.MinutesUntilReady -= (int)Math.Round(hoursSlept * 0.3, 0);
                        }
                    }
                }
            }

            //save coordinates to json file and replace with slime hutch
            wineryCoords.Clear();
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                {
                    wineryCoords.Add(new KeyValuePair<int, int>(b.tileX.Value, b.tileY.Value));
                    b.buildingType.Value = "Slime Hutch";
                    b.indoors.Value.mapPath.Value = "Maps\\SlimeHutch";                    
                    b.indoors.Value.updateMap();
                    RemoveArch(b);
                }
            }
            this.Helper.WriteJsonFile($"{Constants.CurrentSavePath}/Winery_Coords.json", wineryCoords);
        }
        /*
         * END SAVE AND LOADING
         * 
         */




        /*
         * SPEED UP KEG INSIDE WINERY
         * 
         */
        public void TimeEvents_TimeOfDayChanged(object sender, EventArgsIntChanged e)
        {
            if (this.DistillerProfessionActive)
            {
                SetItemCategory(-77);
            }
            //monitor.Log($"Time is " + Game1.timeOfDay + " and it is " + Game1.dayOrNight());
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                    foreach (SObject o in b.indoors.Value.Objects.Values)
                    {
                        if (o.Name.Equals("Keg"))
                        {
                            o.MinutesUntilReady -= 3;
                        }
                    }
            }
        }
        /*
         * END SPEED UP KEG INSIDE WINERY
         * 
         */



        /*
         * SPEED UP CASK INSIDE WINERY
         * Also set the item category to Distilled Craft & change seasonal texture
         */
        public void TimeEvents_AfterDayStarted(object sender, EventArgs e)
        {
            if( this.DistillerProfessionActive && !Game1.player.professions.Contains(77) && ( Game1.player.professions.Contains(4) && Game1.player.professions.Contains(5) ))
            {
                Game1.player.professions.Add(77);
            }

            ranOnce = false;

            if (this.DistillerProfessionActive)
            {
                SetItemCategory(-77);
            }
            //Game1.activeClickableMenu = new LevelUpMenu(0, 10);

            //set seasonal building and reload texture
            if (CurrentSeason != Game1.currentSeason)
            {
                //monitor.Log($"Current season is " + CurrentSeason);
                CurrentSeason = Game1.currentSeason;
                //monitor.Log($"Current season is now " + CurrentSeason);
                Winery_outdoors = helper.Content.Load<Texture2D>($"assets/Winery_outside_{Game1.currentSeason}.png", ContentSource.ModFolder);
                helper.Content.InvalidateCache("Buildings/Winery");
            }

            helper.Content.InvalidateCache("Maps/Winery");

            //reduce time for casks
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                    foreach (SObject o in b.indoors.Value.Objects.Values)
                    {
                        if (o is Cask c)
                            c.daysToMature.Value -= (float)(.3 * c.agingRate.Value);
                    }
            }
        }
        /*
         * END SPEED UP CASK INSIDE WINERY
         * 
         */


        /*
		* SET BONUS PRICE
		*
		*/
        public bool IsAlcohol(Item item)
        {
            return (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459);
        }

        public bool IsDistiller()
        {
            return (this.DistillerProfessionActive && Game1.player.professions.Contains(77));
        }

        public void SetBonusPrice()
        {
            foreach (Item item in Game1.getFarm().shippingBin)
            {
                if ( IsDistiller() && item != null && item is SObject booze && IsAlcohol(item) && booze.getHealth() != booze.Price )
                {
                    booze.Price = (int)(Math.Ceiling((float)booze.Price * 1.4));                    
                    booze.setHealth(booze.Price);
                    //monitor.Log(booze.Name + " price is " + booze.Price+" and health is "+booze.getHealth());
                }
            }
        }
        /*
		* END SET BONUS PRICE
		*
		*/

        /*
        * SET ITEM CATEGORY
        * 
        */
        public void SetItemCategory(int catID)
        {
            //check for old alcohol in player inventory
            foreach (Item item in Game1.player.Items)
            {
                if (item != null && item is SObject booze && IsAlcohol(item) && item.Category != catID)
                {
                    booze.Category = catID;
                }
            }

            //check for old alcohol everywhere else
            foreach (GameLocation location in ModEntry.GetLocations())
            {
                foreach (SObject obj in location.Objects.Values)
                {
                    if (obj is Chest c)
                    {
                        foreach (Item item in c.items)
                        {
                            if (item != null && item is SObject booze && IsAlcohol(item) && item.Category != catID)
                            {
                                booze.Category = catID;
                            }
                        }
                    }
                    else if (obj.ParentSheetIndex == 165 && obj.heldObject.Value is Chest autoGrabberStorage)
                    {
                        foreach (Item item in autoGrabberStorage.items)
                        {
                            if (item != null && item is SObject booze && IsAlcohol(item) && item.Category != catID)
                            {
                                booze.Category = catID;
                            }
                        }
                    }
                    else if (obj is Cask cask)
                    {
                        if (cask.heldObject.Value != null && cask.heldObject.Value is SObject booze && IsAlcohol(cask.heldObject.Value) && cask.heldObject.Value.Category != catID)
                        {
                            cask.heldObject.Value.Category = catID;
                        }
                    }
                    else if (obj.Name.Equals("keg"))
                    {
                        if (obj.heldObject.Value != null && obj.heldObject.Value is SObject booze && IsAlcohol(obj.heldObject.Value) && obj.heldObject.Value.Category != catID)
                        {
                            obj.heldObject.Value.Category = catID;
                        }
                    }
                }
                if (location is FarmHouse house)
                {
                    foreach (Item item in house.fridge.Value.items)
                    {
                        if (item != null && item is SObject booze && IsAlcohol(item) && item.Category != catID)
                        {
                            booze.Category = catID;
                        }
                    }
                }
                if (location is Farm farm)
                {
                    foreach (Building building in farm.buildings)
                    {
                        if (building is Mill mill)
                        {
                            foreach (Item item in mill.output.Value.items)
                            {
                                if (item != null && item is SObject booze && IsAlcohol(item) && item.Category != catID)
                                {
                                    booze.Category = catID;
                                }
                            }
                        }
                        else if (building is JunimoHut hut)
                        {
                            foreach (Item item in hut.output.Value.items)
                            {
                                if (item != null && item is SObject booze && IsAlcohol(item) && item.Category != catID)
                                {
                                    booze.Category = catID;
                                }
                            }
                        }
                    }
                }
            }
            //end old alcohol check
        }
        /*
         * END SET ITEM CATEGORY
         * 
         */




        /*
         * TIME REMAINING ON HOVER INSIDE WINERY
         * 
         */
        public static float ToFloat(double value)
        {
            return (float)value;
        }

        public void GraphicsEvents_OnPostRenderEvent(object sender, EventArgs e)
        {
            if (Game1.hasLoadedGame)
            {
                //monitor.Log($"" + Game1.currentLocation.Name);
                if (Game1.currentLocation.mapPath.Value == "Maps\\Winery" && Game1.currentLocation != null)
                {
                    var timeRemaining = Game1.spriteBatch;
                    ICursorPosition cursorPos = this.Helper.Input.GetCursorPosition();
                    foreach (var entry in Game1.currentLocation.objects)
                    {
                        if (Game1.currentLocation.Objects.TryGetValue(cursorPos.Tile, out SObject obj))
                        {
                            if (obj.heldObject.Value != null)
                            {
                                string textTimeRemaining;

                                if (obj.Name.Equals("Keg") && obj.MinutesUntilReady > 0)
                                {
                                    textTimeRemaining = Math.Round((obj.MinutesUntilReady * 0.6) / 84, 1).ToString() + " minutes";

                                    IClickableMenu.drawHoverText(Game1.spriteBatch, textTimeRemaining, Game1.smallFont, 0, 0, -1, obj.heldObject.Value.Name.Length > 0 ? obj.heldObject.Value.Name : (string)null, -1, (string[])null, (Item)null, 0, -1, -1, -1, -1, 1f, (CraftingRecipe)null);
                                }

                                if (obj is Cask c && c.daysToMature.Value > 0)
                                {
                                    textTimeRemaining = Math.Round(c.daysToMature.Value * 0.6, 1).ToString() + " days";

                                    IClickableMenu.drawHoverText(Game1.spriteBatch, textTimeRemaining, Game1.smallFont, 0, 0, -1, obj.heldObject.Value.Name.Length > 0 ? obj.heldObject.Value.Name : (string)null, -1, (string[])null, (Item)null, 0, -1, -1, -1, -1, 1f, (CraftingRecipe)null);
                                }
                            }
                        }
                    }
                }
            }
        }
        /*
         * END OF TIME REMAINING INSIDE WINERY
         * 
         */



        /*
         * ASSET LOADER
         * 
         */
        public bool CanLoad<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Buildings\\Winery"))
            {
                return true;
            }
            else if (asset.AssetNameEquals("Maps/Winery"))
            {
                return true;
            }
            return false;
        }

        public T Load<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Buildings\\Winery"))
            {
                return (T)(object)Winery_outdoors;
            }
            else if (asset.AssetNameEquals("Maps/Winery"))
            {
                return (T)(object)Winery_indoors;
            }
            return (T)(object)null;
        }
        /*
         * END ASSET LOADER
         * 
         */



        /*
         * ASSET EDITOR
         * 
         */
        public bool CanEdit<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Data/ObjectInformation"))
            {
                return true;
            }
            return false;
        }

        public void Edit<T>(IAssetData asset)
        {
            if (asset.AssetNameEquals("Data/ObjectInformation"))
            {
                IDictionary<int, string> objectInfo = asset.AsDictionary<int, string>().Data;

                objectInfo[303] = objectInfo[303].Replace("-26", "-77");
                objectInfo[346] = objectInfo[346].Replace("-26", "-77");
                objectInfo[348] = objectInfo[348].Replace("-26", "-77");
                objectInfo[459] = objectInfo[459].Replace("-26", "-77");
            }
        }
        /*
         * END ASSET EDITOR
         * 
         */


        /*
         * PATCH METHOD FOR HARMONY
         * Has instance of Cask and reference result manipulation. Always returns false to suppress original method.
         */
        public static bool Patch_performObjectDropInAction(Cask __instance, ref bool __result, Item dropIn, bool probe, Farmer who)
        {
            if (dropIn != null && dropIn is SObject && (dropIn as SObject).bigCraftable.Value || __instance.heldObject.Value != null)
            {
                __result = false;
                //monitor.Log($"1, returning " + __result);
                return false;
            }

            if (!probe && (who == null || !(who.currentLocation is Cellar || who.currentLocation.mapPath.Value == "Maps\\Winery")))
            {
                Game1.showRedMessageUsingLoadString("Strings\\Objects:CaskNoCellar");
                __result = false;
                //monitor.Log($"2, returning " + __result);
                return false;
            }

            if (__instance.Quality >= 4)
            {
                __result = false;
                //monitor.Log($"3, returning " + __result);
                return false;
            }

            bool flag = false;
            float num = 1f;

            switch (dropIn.ParentSheetIndex)
            {
                case 303:
                    flag = true;
                    //monitor.Log($"303, flag is " + flag);
                    num = 1.66f;
                    break;

                case 346:
                    flag = true;
                    //monitor.Log($"346, flag is " + flag);
                    num = 2f;
                    break;

                case 348:
                    flag = true;
                    //monitor.Log($"348, flag is " + flag);
                    num = 1f;
                    break;

                case 424:
                    flag = true;
                    //monitor.Log($"424, flag is " + flag);
                    num = 4f;
                    break;

                case 426:
                    flag = true;
                    //monitor.Log($"426, flag is " + flag);
                    num = 4f;
                    break;
                case 459:
                    flag = true;
                    //monitor.Log($"459, flag is "+flag);
                    num = 2f;
                    break;
            }

            if (!flag)
            {
                __result = false;
                //monitor.Log($"4, returning "+__result);
                return false;
            }

            __instance.heldObject.Value = dropIn.getOne() as SObject;
            //monitor.Log($"Setting Cask's Held Object to "+__instance.heldObject.Value.DisplayName);

            if (!probe)
            {
                __instance.agingRate.Value = num;
                __instance.daysToMature.Value = 56f;
                __instance.MinutesUntilReady = 999999;

                if (__instance.heldObject.Value.Quality == 1)
                {
                    __instance.daysToMature.Value = 42f;
                }
                else if (__instance.heldObject.Value.Quality == 2)
                {
                    __instance.daysToMature.Value = 28f;
                }
                else if (__instance.heldObject.Value.Quality == 4)
                {
                    __instance.daysToMature.Value = 0.0f;
                    __instance.MinutesUntilReady = 1;
                }

                who.currentLocation.playSound("Ship");
                who.currentLocation.playSound("bubbles");
                who.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(256, 1856, 64, 128), 80f, 6, 999999, __instance.TileLocation * 64f + new Vector2(0.0f, (float)sbyte.MinValue), false, false, (float)(((double)__instance.TileLocation.Y + 1.0) * 64.0 / 10000.0 + 9.99999974737875E-05), 0.0f, Color.Yellow * 0.75f, 1f, 0.0f, 0.0f, 0.0f, false)
                {
                    alphaFade = 0.005f
                });
            }
            __result = true;
            //monitor.Log($"5, returning " + __result);
            return false;
        }

        public static bool Patch_getCategoryColor(SObject __instance, ref Color __result)
        {
            if (__instance != null)
            {
                if (__instance is Furniture)
                {
                    __result = new Color(100, 25, 190);
                    return false;
                }

                if (__instance.Type != null && __instance.Type.Equals((object)"Arch"))
                {
                    __result = new Color(110, 0, 90);
                    return false;
                }

                switch (__instance.Category)
                {
                    case -81:
                        __result = new Color(10, 130, 50);
                        return false;
                    case -80:
                        __result = new Color(219, 54, 211);
                        return false;
                    case -79:
                        __result = Color.DeepPink;
                        return false;
                    case -75:
                        __result = Color.Green;
                        return false;
                    case -74:
                        __result = Color.Brown;
                        return false;
                    case -28:
                        __result = new Color(50, 10, 70);
                        return false;
                    case -27:
                    case -26:
                        __result = new Color(0, 155, 111);
                        return false;
                    case -24:
                        __result = Color.Plum;
                        return false;
                    case -22:
                        __result = Color.DarkCyan;
                        return false;
                    case -21:
                        __result = Color.DarkRed;
                        return false;
                    case -20:
                        __result = Color.DarkGray;
                        return false;
                    case -19:
                        __result = Color.SlateGray;
                        return false;
                    case -18:
                    case -14:
                    case -6:
                    case -5:
                        __result = new Color((int)byte.MaxValue, 0, 100);
                        return false;
                    case -16:
                    case -15:
                        __result = new Color(64, 102, 114);
                        return false;
                    case -12:
                    case -2:
                        __result = new Color(110, 0, 90);
                        return false;
                    case -8:
                        __result = new Color(148, 61, 40);
                        return false;
                    case -7:
                        __result = new Color(220, 60, 0);
                        return false;
                    case -4:
                        __result = Color.DarkBlue;
                        return false;
                    case -77:
                        __result = new Color(255, 153, 0);
                        return false;
                    default:
                        __result = Color.Black;
                        return false;
                }
            }
            return false;
        }

        public static bool Patch_getCategoryName(SObject __instance, ref string __result)
        {
            if (__instance != null)
            {
                if (__instance is Furniture)
                {
                    __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12847");
                    return false;
                }

                if (__instance.Type != null && __instance.Type.Equals("Arch"))
                {
                    __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12849");
                    return false;
                }

                switch (__instance.Category)
                {
                    case -81:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12869");
                        return false;
                    case -80:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12866");
                        return false;
                    case -79:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12854");
                        return false;
                    case -75:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12851");
                        return false;
                    case -74:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12855");
                        return false;
                    case -28:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12867");
                        return false;
                    case -27:
                    case -26:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12862");
                        return false;
                    case -25:
                    case -7:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12853");
                        return false;
                    case -24:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12859");
                        return false;
                    case -22:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12858");
                        return false;
                    case -21:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12857");
                        return false;
                    case -20:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12860");
                        return false;
                    case -19:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12856");
                        return false;
                    case -18:
                    case -14:
                    case -6:
                    case -5:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12864");
                        return false;
                    case -16:
                    case -15:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12868");
                        return false;
                    case -12:
                    case -2:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12850");
                        return false;
                    case -8:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12863");
                        return false;
                    case -4:
                        __result = Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12852");
                        return false;
                    case -77:
                        __result = "Distilled Craft";
                        return false;
                    default:
                        __result = "";
                        return false;
                }
            }
            return false;
        }
        /*
        * END OF PATCH METHOD FOR HARMONY
        * 
        */
    }
}
