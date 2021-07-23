using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Harmony;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using xTile;
using xTile.Layers;
using xTile.Tiles;
using SObject = StardewValley.Object;

namespace SauvignonInStardew
{
    internal class ModEntry : Mod, IAssetLoader
    {
        /*********
        ** Fields
        *********/
        private ModConfig Config;
        private SaveData SaveData;

        private Texture2D WineryOutdoorTexture;
        private Map WineryIndoorMap;
        private Map KegRoomMap;

        private const int TileID = 131;

        private string CurrentSeason;

        private int BedTime;
        private int HoursSlept;

        private Texture2D DistillerIcon;

        private string SleepBox;
        private bool RanOnce;

        /// <summary>Whether custom mod data was just restored.</summary>
        private bool JustRestoredWineries;

        private readonly Vector2[] BigKegsInput = new[] { new Vector2(20, 3), new Vector2(23, 3), new Vector2(26, 3), new Vector2(29, 3), new Vector2(32, 3) };
        private readonly Vector2[] BigKegsOutput = new[] { new Vector2(20, 6), new Vector2(23, 6), new Vector2(26, 6), new Vector2(29, 6), new Vector2(32, 6) };


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            //Loaded Textures for outside and indside the Winery
            this.WineryOutdoorTexture = helper.Content.Load<Texture2D>($"assets/Winery_outside_{Game1.currentSeason}.png");
            this.WineryIndoorMap = helper.Content.Load<Map>("assets/Winery.tbin");

            this.KegRoomMap = helper.Content.Load<Map>("assets/Winery2.tbin");

            //Loaded texture for Distiller Icon
            this.DistillerIcon = helper.Content.Load<Texture2D>("assets/Distiller_icon.png");

            //Load string for sleeping dialogue
            this.SleepBox = Game1.content.LoadString("Strings\\Locations:FarmHouse_Bed_GoToSleep");

            //Event for using big kegs
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            //Event for adding blueprint to carpenter menu & preventing building overlays
            helper.Events.Display.MenuChanged += this.OnMenuChanged;

            //Event for Keg Speed
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;

            //Event for Cask Speed
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;

            //Event for showing time remaining on hover
            if (this.Config.ShowHelperInsideWinery)
            {
                helper.Events.Display.Rendered += this.OnRendered;
            }

            //Events for editing Winery width
            helper.Events.World.BuildingListChanged += this.OnBuildingListChanged;

            //Event for fixing skills menu
            helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;

            //Event for Bonus Price and Bed Time if dead or faint
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;

            //Events for save and loading
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.GameLoop.Saved += this.OnSaved;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;

            /*
             * HARMONY PATCHING
             * 
             */
            var harmony = HarmonyInstance.Create("com.jesse.winery");
            Type type = typeof(Cask);
            MethodInfo method = type.GetMethod("performObjectDropInAction");
            HarmonyMethod patchMethod = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_performObjectDropInAction)));
            harmony.Patch(method, patchMethod, null);

            if (this.Config.DistillerProfessionBool)
            {
                Type type2 = typeof(SObject);
                MethodInfo method2 = type2.GetMethod("getCategoryColor");
                HarmonyMethod patchMethod2 = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_getCategoryColor)));
                harmony.Patch(method2, patchMethod2, null);

                MethodInfo method3 = type2.GetMethod("getCategoryName");
                HarmonyMethod patchMethod3 = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_getCategoryName)));
                harmony.Patch(method3, patchMethod3, null);
            }
        }

        /// <summary>Get whether this instance can load the initial version of the given asset.</summary>
        /// <param name="asset">Basic metadata about the asset being loaded.</param>
        public bool CanLoad<T>(IAssetInfo asset)
        {
            return
                asset.AssetNameEquals("Buildings\\Winery")
                || asset.AssetNameEquals("Buildings\\Winery2")
                || asset.AssetNameEquals("Maps/Winery");
        }

        /// <summary>Load a matched asset.</summary>
        /// <param name="asset">Basic metadata about the asset being loaded.</param>
        public T Load<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Buildings\\Winery") || asset.AssetNameEquals("Buildings\\Winery2"))
                return (T)(object)this.WineryOutdoorTexture;

            if (asset.AssetNameEquals("Maps/Winery"))
                return (T)(object)this.WineryIndoorMap;

            if (asset.AssetNameEquals("Maps/Winery2"))
                return (T)(object)this.KegRoomMap;

            return (T)(object)null;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get all game locations, including any custom locations.</summary>
        /// <remarks>From Pathoschild.</remarks>
        private static IEnumerable<GameLocation> GetLocations()
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
         * BIG KEG USAGE
         *
         */
        private bool IsWineJuiceOrCoffee(Item item)
        {
            return (item.ParentSheetIndex == 395 || item.ParentSheetIndex == 348 || item.ParentSheetIndex == 350);
        }

        private bool IsKegable(Item item)
        {
            int index = item.ParentSheetIndex;
            int category = item.Category;
            return (index == 262 || index == 304 || index == 340 || index == 433 || category == -79 || category == -75);
        }

        private bool IsBigKegInput(Vector2 position)
        {
            return this.BigKegsInput.Contains(position);
        }

        private bool IsBigKegOutput(Vector2 position)
        {
            return this.BigKegsOutput.Contains(position);
        }

        private void SetKegAnimation(Layer layer, Vector2 tileLocation, TileSheet tilesheet, int[] tileIDs, long interval)
        {
            layer.Tiles[(int)tileLocation.X, (int)tileLocation.Y] = new AnimatedTile(layer, this.MakeAnimatedTile(layer, tilesheet, tileIDs), interval);
        }

        private StaticTile[] MakeAnimatedTile(Layer layer, TileSheet tilesheet, int[] tileIDs)
        {
            StaticTile[] output = new StaticTile[tileIDs.Length];
            for (int i = 0; i < tileIDs.Length; i++)
            {
                output[i] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileIDs[i]);
            }
            return output;
        }

        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (Game1.currentLocation?.mapPath.Value == "Maps\\Winery" && this.IsBigKegInput(e.Cursor.GrabTile))
            {
                if (e.Button.IsActionButton())
                {
                    GameLocation winery = Game1.currentLocation;
                    Vector2 chestLocation = new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y - 34);
                    Vector2 outputChestLocation = new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y - 24);

                    Layer layerBuildings = winery.map.GetLayer("Buildings");
                    Layer layerFront = winery.map.GetLayer("Front");
                    TileSheet tilesheet = winery.map.GetTileSheet("bucket_anim");


                    if (!winery.Objects.ContainsKey(chestLocation))
                    {
                        Chest newChest = new Chest(true) { TileLocation = chestLocation, Name = "|ignore| input chest for big ol kego" };
                        winery.Objects.Add(chestLocation, newChest);
                    }
                    if (!winery.Objects.ContainsKey(outputChestLocation))
                    {
                        Chest newChest = new Chest(true) { TileLocation = outputChestLocation, Name = "|ignore| output chest for big ol kego" };
                        winery.Objects.Add(outputChestLocation, newChest);
                    }
                    if (Game1.player.CurrentItem != null && this.IsKegable(Game1.player.CurrentItem))
                    {
                        Chest inputChest = (Chest)winery.Objects[chestLocation];
                        Chest outputChest = (Chest)winery.Objects[outputChestLocation];
                        Item item = Game1.player.CurrentItem;
                        Item remainder = null;
                        switch (item.ParentSheetIndex)
                        {
                            case 262:
                                item = new SObject(Vector2.Zero, 346, "Beer", false, true, false, false) { Name = "Beer" };
                                ((SObject)item).setHealth(1750);
                                remainder = inputChest.addItem(item);
                                this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 4, 5, 6 }, 250);
                                this.SetKegAnimation(layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 18, 19, 20 }, 250);
                                if (outputChest.items.Count <= 0)
                                {
                                    this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), tilesheet, new[] { 11, 12, 13 }, 250);
                                }
                                break;
                            case 304:
                                item = new SObject(Vector2.Zero, 303, "Pale Ale", false, true, false, false) { Name = "Pale Ale" };
                                ((SObject)item).setHealth(2250);
                                remainder = inputChest.addItem(item);
                                this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 4, 5, 6 }, 250);
                                this.SetKegAnimation(layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 18, 19, 20 }, 250);
                                if (outputChest.items.Count <= 0)
                                {
                                    this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), tilesheet, new[] { 11, 12, 13 }, 250);
                                }
                                break;
                            case 340:
                                item = new SObject(Vector2.Zero, 459, "Mead", false, true, false, false) { Name = "Mead" };
                                ((SObject)item).setHealth(600);
                                remainder = inputChest.addItem(item);
                                this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 4, 5, 6 }, 250);
                                this.SetKegAnimation(layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 18, 19, 20 }, 250);
                                if (outputChest.items.Count <= 0)
                                {
                                    this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), tilesheet, new[] { 11, 12, 13 }, 250);
                                }
                                break;
                            case 433:
                                item = new SObject(Vector2.Zero, 395, "Coffee", false, true, false, false) { Name = "Coffee" };
                                item.Stack = (item.Stack / 5) - (item.Stack % 5);
                                ((SObject)item).setHealth(120);
                                remainder = inputChest.addItem(item);
                                this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 1, 2, 3 }, 250);
                                this.SetKegAnimation(layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 15, 16, 17 }, 250);
                                if (outputChest.items.Count <= 0)
                                {
                                    this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), tilesheet, new[] { 8, 9, 10 }, 250);
                                }
                                break;
                            default:
                                switch (item.Category)
                                {
                                    case -79:
                                        item = new SObject(Vector2.Zero, 348, item.Name + " Wine", false, true, false, false) { Name = item.Name + " Wine" };
                                        ((SObject)item).Price = ((SObject)item).Price * 3;
                                        this.Helper.Reflection.GetField<SObject.PreserveType>(item, "preserve").SetValue(SObject.PreserveType.Wine);
                                        this.Helper.Reflection.GetField<int>(item, "preservedParentSheetIndex").SetValue(item.ParentSheetIndex);
                                        ((SObject)item).setHealth(10000);
                                        remainder = inputChest.addItem(item);
                                        this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 1, 2, 3 }, 250);
                                        this.SetKegAnimation(layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 15, 16, 17 }, 250);
                                        if (outputChest.items.Count <= 0)
                                        {
                                            this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), tilesheet, new[] { 8, 9, 10 }, 250);
                                        }
                                        break;
                                    case -75:
                                        item = new SObject(Vector2.Zero, 350, item.Name + " Juice", false, true, false, false) { Name = item.Name + " Juice" };
                                        ((SObject)item).Price = (int)(((SObject)item).Price * 2.5);
                                        this.Helper.Reflection.GetField<SObject.PreserveType>(item, "preserve").SetValue(SObject.PreserveType.Juice);
                                        this.Helper.Reflection.GetField<int>(item, "preservedParentSheetIndex").SetValue(item.ParentSheetIndex);
                                        ((SObject)item).setHealth(6000);
                                        remainder = inputChest.addItem(item);
                                        this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 1, 2, 3 }, 250);
                                        this.SetKegAnimation(layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), tilesheet, new[] { 15, 16, 17 }, 250);
                                        if (outputChest.items.Count <= 0)
                                        {
                                            this.SetKegAnimation(layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), tilesheet, new[] { 8, 9, 10 }, 250);
                                        }
                                        break;
                                }
                                break;
                        }
                        if (remainder == null)
                        {
                            Game1.player.removeItemFromInventory(item);
                        }
                    }
                }
            }
            else if (Game1.currentLocation?.mapPath.Value == "Maps\\Winery" && this.IsBigKegOutput(e.Cursor.GrabTile))
            {
                if (e.Button.IsActionButton())
                {
                    GameLocation winery = Game1.currentLocation;
                    Vector2 chestLocation = new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y - 27);
                    Chest chest = (Chest)winery.Objects[chestLocation];
                    Game1.activeClickableMenu = new ItemGrabMenu(
                       inventory: chest.items,
                       reverseGrab: false,
                       showReceivingMenu: true,
                       highlightFunction: InventoryMenu.highlightAllItems,
                       behaviorOnItemSelectFunction: chest.grabItemFromInventory,
                       message: null,
                       behaviorOnItemGrab: chest.grabItemFromChest,
                       canBeExitedWithKey: true, showOrganizeButton: true,
                       source: ItemGrabMenu.source_chest,
                       context: chest
                   );
                }
            }
        }
        /*
         * END BIG KEG USAGE
         *
         */


        /*
         * Set Sleep and Prices on death or faint
         * Set ranOnce back to false at start of day
         * Remove Distiller profession if player is level 10 and does not have tiller
         */
        /// <summary>Raised after the game state is updated (≈60 times per second).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            // fix winery buildings
            // This needs to happen on the next update tick, so we can override the game setting the exit warp target to the map file's default.
            if (Context.IsWorldReady && this.JustRestoredWineries)
            {
                this.JustRestoredWineries = false;

                foreach (Building b in Game1.getFarm().buildings.Where(this.IsWinery))
                {
                    b.indoors.Value.mapPath.Value = "Maps\\Winery";
                    b.indoors.Value.updateMap();
                    b.updateInteriorWarps();
                    this.Helper.Reflection
                        .GetField<NetArray<bool, NetBool>>(b.indoors.Value, "waterSpots", required: false)
                        ?.GetValue().Clear(); // avoid lag due to the game trying to set a non-existent tile's property in SlimeHutch::UpdateWhenCurrentLocation
                    this.SetArch(b, true);
                }
            }
            if ((Game1.player.health <= 0 || Game1.player.stamina <= 0) && !this.RanOnce)
            {
                this.BedTime = Game1.timeOfDay;
                this.SetBonusPrice();
            }

            if (this.Config.DistillerProfessionBool && Game1.player.professions.Contains(77) && Game1.player.FarmingLevel > 9 && !(Game1.player.professions.Contains(1)))
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
        private void DisplayDistillerInfo()
        {
            if (this.Config.DistillerProfessionBool)
            {
                this.Monitor.Log("Distiller Profession is Active", LogLevel.Info);
                if (Game1.player.professions.Contains(77))
                {
                    this.Monitor.Log("You are a Distiller", LogLevel.Info);
                }
                else
                {
                    this.Monitor.Log("You are not a Distiller. Reach Level 10 Farming or go to the Statue in the Sewers to reset your Farming Professions.", LogLevel.Info);
                }
            }
            else
            {
                this.Monitor.Log("Distiller Profession is Inactive", LogLevel.Info);
            }
        }


        /*
        * DRAW TO SKILLS PAGE
        * draw icon and Distiller profession info in Skills Page
        */
        /// <summary>When a menu is open (<see cref="Game1.activeClickableMenu"/> isn't null), raised after that menu is drawn to the sprite batch but before it's rendered to the screen.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
        {
            if (Game1.activeClickableMenu is GameMenu menu && menu.currentTab == 1 && Game1.player.professions.Contains(77) && !Game1.player.professions.Contains(4))
            {
                List<IClickableMenu> pages = this.Helper.Reflection.GetField<List<IClickableMenu>>(menu, "pages").GetValue();
                foreach (IClickableMenu page in pages)
                {
                    if (page is SkillsPage || page.GetType().FullName == "SpaceCore.Interface.NewSkillsPage")
                    {
                        List<ClickableTextureComponent> skillBars = this.Helper.Reflection.GetField<List<ClickableTextureComponent>>(page, "skillBars").GetValue();
                        foreach (ClickableTextureComponent skillBar in skillBars)
                        {
                            if (this.Config.DistillerProfessionBool && skillBar.containsPoint(Game1.getMouseX(), Game1.getMouseY()) && skillBar.myID == 200)
                            {
                                //local variables
                                string textTitle = this.Helper.Translation.Get("professionTitle_Distiller");
                                string textDescription = this.Helper.Translation.Get("professionHoverDescription");

                                //draw
                                //icon
                                IClickableMenu.drawTextureBox(Game1.spriteBatch, skillBar.bounds.X - 16 - 8, skillBar.bounds.Y - 16 - 16, 96, 96, Color.White);
                                Game1.spriteBatch.Draw(this.DistillerIcon, new Vector2(skillBar.bounds.X - 8, skillBar.bounds.Y - 32 + 16), new Rectangle(0, 0, 16, 16), Color.White, 0.0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
                                //box
                                IClickableMenu.drawHoverText(Game1.spriteBatch, textDescription, Game1.smallFont, 0, 0, -1, textTitle.Length > 0 ? textTitle : null);

                                if (!Game1.options.hardwareCursor)
                                {
                                    Game1.spriteBatch.Draw(Game1.mouseCursors, new Vector2(Game1.getMouseX(), Game1.getMouseY()), Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, Game1.options.SnappyMenus ? 44 : 0, 16, 16), Color.White * Game1.mouseCursorTransparency, 0.0f, Vector2.Zero, Game1.pixelZoom + Game1.dialogueButtonScale / 150f, SpriteEffects.None, 1f);
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
         * RESTORE WINERY DATA AFTER MENU EXITS
         * 
         * ADD WINERY TO CARPENTER MENU
         * 
         * CHANGE FARMING LEVEL UP 10 MENU TO DISTILLER MENU
         * 
         * SAVE BED TIME FOR KEG BONUS OVERNIGHT
         */
        /// <summary>Raised after a game menu is opened, closed, or replaced.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            //sets back Winery widths to 8 for Archway walkthrough and add back invisible tiles
            if (e.NewMenu == null)
            {
                if (e.OldMenu is CarpenterMenu)
                {
                    foreach (Building building in Game1.getFarm().buildings)
                    {
                        if (building.indoors.Value != null && building.buildingType.Value.Equals("Winery"))
                            this.SetArch(building, true);
                    }
                }

                if (e.OldMenu is ItemGrabMenu && Game1.currentLocation != null && Game1.currentLocation.mapPath.Value == "Maps\\Winery2")
                {
                    GameLocation winery = Game1.currentLocation;
                    Layer layerBuildings = winery.map.GetLayer("Buildings");
                    TileSheet tilesheet = winery.map.GetTileSheet("bucket_anim");
                    foreach (SObject o in winery.Objects.Values)
                    {
                        if (o is Chest chest && chest.Name.Equals("|ignore| output chest for big ol kego") && chest.items.Count <= 0)
                        {
                            layerBuildings.Tiles[(int)chest.TileLocation.X, (int)chest.TileLocation.Y + 27] = new StaticTile(layerBuildings, tilesheet, BlendMode.Alpha, 21);
                        }
                    }
                }
                return;
            }

            if (e.NewMenu is DialogueBox box && box.getCurrentString() == this.SleepBox)
            {
                this.BedTime = Game1.timeOfDay;
                this.SetBonusPrice();
            }

            //monitor.Log($"Current menu type is " + e.NewMenu);
            if (this.Config.DistillerProfessionBool)
            {
                if (!(Game1.activeClickableMenu is DistillerMenu) && Game1.activeClickableMenu is LevelUpMenu lvlMenu && lvlMenu.isProfessionChooser)
                {
                    int skill = this.Helper.Reflection.GetField<int>(lvlMenu, "currentSkill").GetValue();
                    int level = this.Helper.Reflection.GetField<int>(lvlMenu, "currentLevel").GetValue();
                    if (skill == 0 && level == 0)
                    {
                        Game1.activeClickableMenu = new DistillerMenu(this.DistillerIcon, this.Helper);
                    }
                }
            }

            if (e.NewMenu.GetType().FullName.Contains("CarpenterMenu"))
            {
                //Sets current Winery buildings to 11 width to stop overlay and removes invisible tiles for building moving 
                foreach (Building building in Game1.getFarm().buildings)
                {
                    if (building.indoors.Value != null && building.buildingType.Value.Equals("Winery"))
                        this.SetArch(building, false);
                }
                if (!this.IsMagical(e.NewMenu) && !this.HasBluePrint(e.NewMenu, "Winery"))
                {
                    BluePrint wineryBluePrint = new BluePrint("Slime Hutch")
                    {
                        name = "Winery",
                        displayName = this.Helper.Translation.Get("wineryName"),
                        description = this.Helper.Translation.Get("wineryDescription"),
                        daysToConstruct = 4,//4
                        moneyRequired = 40000 //40000
                    };
                    wineryBluePrint.itemsRequired.Clear();
                    wineryBluePrint.itemsRequired.Add(709, 200);//200
                    wineryBluePrint.itemsRequired.Add(330, 100);//100
                    wineryBluePrint.itemsRequired.Add(390, 100);//100

                    this.SetBluePrintField(wineryBluePrint, "textureName", "Buildings\\Winery");
                    this.SetBluePrintField(wineryBluePrint, "texture", Game1.content.Load<Texture2D>(wineryBluePrint.textureName));

                    this.GetBluePrints(e.NewMenu).Add(wineryBluePrint);
                }

                if (Game1.getFarm().isBuildingConstructed("Winery") && !this.IsMagical(e.NewMenu) && !this.HasBluePrint(e.NewMenu, "Winery2"))
                {
                    BluePrint kegRoomBluePrint = new BluePrint("Slime Hutch")
                    {
                        name = "Winery2",
                        displayName = this.Helper.Translation.Get("kegRoomDislplayName"),
                        description = this.Helper.Translation.Get("kegRoomDescription"),
                        daysToConstruct = 0,
                        moneyRequired = 450000,
                        blueprintType = "Upgrades",
                        nameOfBuildingToUpgrade = "Winery"
                    };
                    kegRoomBluePrint.itemsRequired.Clear();
                    kegRoomBluePrint.itemsRequired.Add(709, 0);//200
                    kegRoomBluePrint.itemsRequired.Add(335, 0);//150
                    kegRoomBluePrint.itemsRequired.Add(388, 0);//500

                    this.SetBluePrintField(kegRoomBluePrint, "textureName", "Buildings\\Winery2");
                    this.SetBluePrintField(kegRoomBluePrint, "texture", Game1.content.Load<Texture2D>(kegRoomBluePrint.textureName));
                }
            }
        }

        private bool IsMagical(IClickableMenu menu)
        {
            return this.Helper.Reflection.GetField<bool>(menu, "magicalConstruction").GetValue();
            //return (bool)typeof(CarpenterMenu).GetField("magicalConstruction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        private bool HasBluePrint(IClickableMenu menu, string blueprintName)
        {
            return this.GetBluePrints(menu).Exists(bluePrint => bluePrint.name == blueprintName);
        }

        private List<BluePrint> GetBluePrints(IClickableMenu menu)
        {
            return this.Helper.Reflection.GetField<List<BluePrint>>(menu, "blueprints").GetValue();
            //return (List<BluePrint>)typeof(CarpenterMenu).GetField("blueprints", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        private void SetBluePrintField(BluePrint bluePrint, string field, object value)
        {
            this.Helper.Reflection.GetField<object>(bluePrint, field).SetValue(value);
            //typeof(BluePrint).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(bluePrint, value);
        }
        /*
         * END OF ADDING WINERY TO CARPENTER MENU
         * 
         */



        /*
        * ADD AND REMOVE ARCHWAY
        * Change width of building and add/remove invisible tiles
        */
        private void SetArch(Building building, bool enable)
        {
            // collect info
            Farm farm = Game1.getFarm();
            Point minOffset = new Point(9, 0);
            Point maxOffset = new Point(11, 6);
            var layer = farm.map.GetLayer("Buildings");
            var tilesheet = farm.map.TileSheets.FirstOrDefault(sheet => sheet.ImageSource != null && (sheet.ImageSource.Contains("outdoor") || sheet.ImageSource.Contains("Outdoor")));

            // validate
            if ((building.tileX.Value + maxOffset.X) >= layer.LayerWidth || (building.tileY.Value + maxOffset.Y) >= layer.LayerHeight)
            {
                this.Monitor.Log($"Didn't apply map changes for winery at ({building.tileX.Value}, {building.tileY.Value}) because it's outside the map bounds.", LogLevel.Warn);
                return;
            }

            // apply changes
            if (enable)
                building.tilesWide.Value = 8;
            for (int x = building.tileX.Value + minOffset.X; x < building.tileX.Value + maxOffset.X; x++)
            {
                for (int y = building.tileY.Value + maxOffset.Y; y < building.tileY.Value + maxOffset.Y; y++)
                {
                    if (enable)
                        layer.Tiles[x, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, TileID);
                    else
                        farm.removeTile(x, y, "Buildings");
                }
            }
            if (!enable || building.daysOfConstructionLeft.Value > 0)
                building.tilesWide.Value = 11;
        }
        /*
        * END ADD AND REMOVE ARCHWAY
        * 
        */



        /*
         * EDIT WINERY WIDTH
         * calls AddArch or RemoveArch depending on what the player did.
         */
        /// <summary>Raised after buildings are added or removed in a location.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnBuildingListChanged(object sender, BuildingListChangedEventArgs e)
        {
            foreach (Building building in e.Added.Where(this.IsWinery))
                this.SetArch(building, true);
            foreach (Building building in e.Removed.Where(this.IsWinery))
                this.SetArch(building, false);
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
        /// <summary>Raised after the game finishes writing data to the save file (except the initial save creation).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSaved(object sender, SavedEventArgs e)
        {
            // delete legacy data file (migrated into save file at this point)
            FileInfo legacyFile = new FileInfo(Path.Combine($"{Constants.CurrentSavePath}", "Winery_Coords.json"));
            if (legacyFile.Exists)
                legacyFile.Delete();

            // restore data
            this.RestoreStashedData(this.SaveData);
        }

        /// <summary>Raised after the player loads a save slot and the world is initialised.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            // restore data
            this.SaveData = this.ReadSaveData();
            this.RestoreStashedData(this.SaveData);

            // show distiller info
            this.DisplayDistillerInfo();
        }

        /// <summary>Raised before the game begins writes data to the save file (except the initial save creation).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            //Add Artisan Profession
            if (this.Config.DistillerProfessionBool && Game1.player.professions.Contains(77) && !Game1.player.professions.Contains(5))
            {
                Game1.player.professions.Add(4);
            }
            this.SetItemCategory(-26);

            //calculate time slept
            if (this.BedTime > 0)
            {
                this.HoursSlept = ((2400 - this.BedTime) + Game1.timeOfDay);
            }

            if (Context.IsMainPlayer)
            {
                //reduce time for kegs overnight
                foreach (Building b in Game1.getFarm().buildings)
                {
                    if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                    {
                        foreach (SObject o in b.indoors.Value.Objects.Values)
                        {
                            if (o.Name.Equals("Keg"))
                            {
                                o.MinutesUntilReady -= (int)Math.Round(this.HoursSlept * 0.3, 0);
                            }
                        }
                    }
                }

                //save coordinates to json file and replace with slime hutch
                this.SaveData.WineryCoords.Clear();
                foreach (Building b in Game1.getFarm().buildings)
                {
                    if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                    {
                        this.SaveData.WineryCoords.Add(new Point(b.tileX.Value, b.tileY.Value));
                        b.buildingType.Value = "Slime Hutch";
                        b.indoors.Value.mapPath.Value = "Maps\\SlimeHutch";
                        b.indoors.Value.updateMap();
                        this.SetArch(b, false);
                    }
                }
                this.Helper.Data.WriteSaveData("data", this.SaveData);
            }
        }

        /// <summary>Read mod data stored in the save file.</summary>
        private SaveData ReadSaveData()
        {
            if (Context.IsMainPlayer)
            {
                // from save file
                {
                    SaveData data = this.Helper.Data.ReadSaveData<SaveData>("data");
                    if (data != null)
                        return data;
                }

                // from legacy JSON file
                {
                    FileInfo legacyFile = new FileInfo(Path.Combine($"{Constants.CurrentSavePath}", "Winery_Coords.json"));
                    var data = legacyFile.Exists
                        ? JsonConvert.DeserializeObject<List<KeyValuePair<int, int>>>(File.ReadAllText(legacyFile.FullName))
                        : null;
                    if (data != null)
                    {
                        return new SaveData
                        {
                            WineryCoords = data.Select(p => new Point(p.Key, p.Value)).ToList()
                        };
                    }
                }
            }

            // new data
            return new SaveData();
        }

        /// <summary>Restore game data based on the given save data.</summary>
        /// <param name="data">The save data to restore.</param>
        private void RestoreStashedData(SaveData data)
        {
            // remove Artisan Profession if they have selected Distiller Profession
            if (this.Config.DistillerProfessionBool && Game1.player.professions.Contains(77) && !Game1.player.professions.Contains(5))
                Game1.player.professions.Remove(4);

            // mark winery buildings
            foreach (Building b in Game1.getFarm().buildings)
            {
                foreach (var pair in data.WineryCoords)
                {
                    if (b.tileX.Value == pair.X && b.tileY.Value == pair.Y && b.buildingType.Value.Equals("Slime Hutch"))
                    {
                        b.buildingType.Value = "Winery";
                        this.JustRestoredWineries = true;
                    }
                }
            }
        }
        /*
         * END SAVE AND LOADING
         * 
         */




        /*
         * SPEED UP KEG INSIDE WINERY
         * 
         */
        /// <summary>Raised after the in-game clock time changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            if (this.Config.DistillerProfessionBool)
            {
                this.SetItemCategory(-77);
            }
            //monitor.Log($"Time is " + Game1.timeOfDay + " and it is " + Game1.dayOrNight());
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                {
                    GameLocation winery = b.indoors.Value;
                    Layer layerBuildings = winery.map.GetLayer("Buildings");
                    Layer layerFront = winery.map.GetLayer("Front");
                    TileSheet tilesheet = winery.map.GetTileSheet("bucket_anim");
                    foreach (SObject o in b.indoors.Value.Objects.Values)
                    {
                        if (o.Name.Equals("Keg"))
                        {
                            o.MinutesUntilReady -= 3;
                        }

                        if (o is Chest chest && chest.Name.Equals("|ignore| input chest for big ol kego"))
                        {
                            foreach (SObject item in chest.items.OfType<SObject>())
                            {
                                if (item.getHealth() > 0)
                                {
                                    item.setHealth(item.getHealth() - 10);
                                }
                                else if (item.getHealth() <= 0)
                                {
                                    Chest outputChest = ((Chest)b.indoors.Value.Objects[new Vector2(chest.TileLocation.X, chest.TileLocation.Y + 10)]);
                                    Item remainder = outputChest.addItem(item);
                                    if (remainder == null)
                                    {
                                        chest.items.Remove(item);
                                    }

                                    this.SetKegAnimation(layerBuildings, new Vector2(chest.TileLocation.X, chest.TileLocation.Y + 37), tilesheet, this.IsWineJuiceOrCoffee(item) ? new[] { 22, 23, 24 } : new[] { 25, 26, 27 }, 250);
                                }
                            }
                            if (chest.items.Count <= 0)
                            {
                                layerBuildings.Tiles[(int)chest.TileLocation.X, (int)chest.TileLocation.Y + 36] = new StaticTile(layerBuildings, tilesheet, BlendMode.Alpha, 0);
                                layerFront.Tiles[(int)chest.TileLocation.X, (int)chest.TileLocation.Y + 36] = new StaticTile(layerFront, tilesheet, BlendMode.Alpha, 14);
                            }
                        }
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
        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (this.Config.DistillerProfessionBool && !Game1.player.professions.Contains(77) && (Game1.player.professions.Contains(4) && Game1.player.professions.Contains(5)))
            {
                Game1.player.professions.Add(77);
            }

            this.RanOnce = false;

            if (this.Config.DistillerProfessionBool)
            {
                this.SetItemCategory(-77);
            }
            //Game1.activeClickableMenu = new LevelUpMenu(0, 10);

            //set seasonal building and reload texture
            if (this.CurrentSeason != Game1.currentSeason)
            {
                //monitor.Log($"Current season is " + CurrentSeason);
                this.CurrentSeason = Game1.currentSeason;
                //monitor.Log($"Current season is now " + CurrentSeason);
                this.WineryOutdoorTexture = this.Helper.Content.Load<Texture2D>($"assets/Winery_outside_{Game1.currentSeason}.png");
                this.Helper.Content.InvalidateCache("Buildings/Winery");
            }

            this.Helper.Content.InvalidateCache("Maps/Winery");

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
        private bool IsAlcohol(Item item)
        {
            return (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459);
        }

        private bool IsDistiller()
        {
            return (this.Config.DistillerProfessionBool && Game1.player.professions.Contains(77));
        }

        private void SetBonusPrice()
        {
            foreach (Item item in Game1.getFarm().getShippingBin(Game1.player))
            {
                if (this.IsDistiller() && item != null && item is SObject booze && this.IsAlcohol(item) && booze.getHealth() != booze.Price)
                {
                    booze.Price = (int)(Math.Ceiling(booze.Price * 1.4));
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
        private void SetItemCategory(int catID)
        {
            //check for old alcohol in player inventory
            foreach (Item item in Game1.player.Items)
            {
                if (item != null && item is SObject booze && this.IsAlcohol(item) && item.Category != catID)
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
                            if (item is SObject booze && this.IsAlcohol(item) && item.Category != catID)
                            {
                                booze.Category = catID;
                            }
                        }
                    }
                    else if (obj.ParentSheetIndex == 165 && obj.heldObject.Value is Chest autoGrabberStorage)
                    {
                        foreach (Item item in autoGrabberStorage.items)
                        {
                            if (item is SObject booze && this.IsAlcohol(item) && item.Category != catID)
                            {
                                booze.Category = catID;
                            }
                        }
                    }
                    else if (obj is Cask cask)
                    {
                        if (cask.heldObject.Value != null && this.IsAlcohol(cask.heldObject.Value) && cask.heldObject.Value.Category != catID)
                        {
                            cask.heldObject.Value.Category = catID;
                        }
                    }
                    else if (obj.Name.Equals("keg"))
                    {
                        if (obj.heldObject.Value != null && this.IsAlcohol(obj.heldObject.Value) && obj.heldObject.Value.Category != catID)
                        {
                            obj.heldObject.Value.Category = catID;
                        }
                    }
                }
                if (location is FarmHouse house)
                {
                    foreach (Item item in house.fridge.Value.items)
                    {
                        if (item is SObject booze && this.IsAlcohol(item) && item.Category != catID)
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
                                if (item is SObject booze && this.IsAlcohol(item) && item.Category != catID)
                                {
                                    booze.Category = catID;
                                }
                            }
                        }
                        else if (building is JunimoHut hut)
                        {
                            foreach (Item item in hut.output.Value.items)
                            {
                                if (item is SObject booze && this.IsAlcohol(item) && item.Category != catID)
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
        /// <summary>Raised after the game draws to the sprite patch in a draw tick, just before the final sprite batch is rendered to the screen.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnRendered(object sender, RenderedEventArgs e)
        {
            if (Game1.hasLoadedGame)
            {
                //monitor.Log($"" + Game1.currentLocation.Name);
                if (Game1.currentLocation.mapPath.Value == "Maps\\Winery" && Game1.currentLocation != null)
                {
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
                                    textTimeRemaining = $"{Math.Round((obj.MinutesUntilReady * 0.6) / 84, 1)} {this.Helper.Translation.Get("minutes")}";

                                    IClickableMenu.drawHoverText(Game1.spriteBatch, textTimeRemaining, Game1.smallFont, 0, 0, -1, obj.heldObject.Value.Name.Length > 0 ? obj.heldObject.Value.Name : null);
                                }

                                if (obj is Cask c && c.daysToMature.Value > 0)
                                {
                                    textTimeRemaining = $"{Math.Round(c.daysToMature.Value * 0.6, 1)} {this.Helper.Translation.Get("days")}";

                                    IClickableMenu.drawHoverText(Game1.spriteBatch, textTimeRemaining, Game1.smallFont, 0, 0, -1, obj.heldObject.Value.Name.Length > 0 ? obj.heldObject.Value.Name : null);
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

        /// <summary>Get whether a building is a winery.</summary>
        /// <param name="building">The building to check.</param>
        private bool IsWinery(Building building)
        {
            return
                building.indoors.Value != null
                && building.buildingType.Value == "Winery";
        }

        /*
         * PATCH METHOD FOR HARMONY
         * Has instance of Cask and reference result manipulation. Always returns false to suppress original method.
         */
        public static bool Patch_performObjectDropInAction(Cask __instance, ref bool __result, Item dropIn, bool probe, Farmer who)
        {
            if (dropIn is SObject obj && obj.bigCraftable.Value || __instance.heldObject.Value != null)
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
                who.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(256, 1856, 64, 128), 80f, 6, 999999, __instance.TileLocation * 64f + new Vector2(0.0f, sbyte.MinValue), false, false, (float)((__instance.TileLocation.Y + 1.0) * 64.0 / 10000.0 + 9.99999974737875E-05), 0.0f, Color.Yellow * 0.75f, 1f, 0.0f, 0.0f, 0.0f)
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
                        __result = new Color(byte.MaxValue, 0, 100);
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
