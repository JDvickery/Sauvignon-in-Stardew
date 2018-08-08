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

namespace Sauvignon_in_Stardew
{
    class ModEntry : Mod, IAssetLoader, IAssetEditor
    {
        /*
         * FIELDS
         * 
         */
        public static IModHelper helper;
        public static IMonitor monitor;

        public Texture2D Winery_outdoors;
        public Map Winery_indoors;

        public List<KeyValuePair<int, int>> wineryCoords;

        public Layer layer;
        public TileSheet tilesheet;
        public int tileID;

        public readonly Dictionary<string, string> dataForBlueprint = new Dictionary<string, string>() { ["Winery"] = "709 200 330 100 390 100/11/6/5/5/-1/-1/Winery/Winery/Kegs and Casks inside work 30% faster and display time remaining./Buildings/none/96/96/20/null/Farm/20000/false" };

        public readonly Vector2 TooltipOffset = new Vector2(Game1.tileSize / 2);
        public readonly Rectangle TooltipSourceRect = new Rectangle(0, 256, 60, 60);

        public string CurrentSeason;

        public int bedTime;
        public int hoursSlept;
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

            //Loaded Textures for outside and indside the Winery
            Winery_outdoors = helper.Content.Load<Texture2D>($"assets/Winery_outside_{Game1.currentSeason}.png", ContentSource.ModFolder);
            Winery_indoors = helper.Content.Load<Map>("assets/Winery.tbin", ContentSource.ModFolder);

            //Event for adding blueprint to carpenter menu
            MenuEvents.MenuChanged += MenuEvents_MenuChanged;

            //Event for Keg Speed
            TimeEvents.TimeOfDayChanged += TimeEvents_TimeOfDayChanged;

            //Event for Cask Speed
            TimeEvents.AfterDayStarted += TimeEvents_AfterDayStarted;

            //Event for showing time remaining on hover
            GraphicsEvents.OnPostRenderEvent += GraphicsEvents_OnPostRenderEvent;
            GraphicsEvents.OnPreRenderEvent += GraphicsEvents_OnPreRenderEvent;

            //Events for editing Winery width
            LocationEvents.BuildingsChanged += LocationEvents_BuildingsChanged;

            //Event for proventing buidling overlays
            MenuEvents.MenuClosed += MenuEvents_MenuClosed;

            /*
             * Events for save and loading
             * 
             */
            SaveEvents.BeforeSave += SaveEvents_BeforeSave;

            SaveEvents.AfterSave += SaveEvents_AfterSaveLoad;
            SaveEvents.AfterLoad += SaveEvents_AfterSaveLoad;
            /*
             * End of Events for save and loading
             * 
             */


            /*
             * HARMONY PATCHING
             * 
             */
            var harmony = HarmonyInstance.Create("com.jesse.winery");
            Type type = typeof(Cask);
            MethodInfo method = type.GetMethod("performObjectDropInAction");
            HarmonyMethod patchMethod = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_performObjectDropInAction)));
            harmony.Patch(method, patchMethod, null);
            
            Type type2 = typeof(SObject);
            MethodInfo method2 = type2.GetMethod("getCategoryColor");
            HarmonyMethod patchMethod2 = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_getCategoryColor)));
            harmony.Patch(method2, patchMethod2, null);

            MethodInfo method3 = type2.GetMethod("getCategoryName");
            HarmonyMethod patchMethod3 = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_getCategoryName)));
            harmony.Patch(method3, patchMethod3, null);
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
         * ADD WINERY TO CARPENTER MENU
         * 
         */
        public void MenuEvents_MenuChanged(object sender, EventArgsClickableMenuChanged e)
        {
            if ( !(Game1.activeClickableMenu is DistillerMenu) && Game1.activeClickableMenu is LevelUpMenu lvlMenu && lvlMenu.isProfessionChooser == true && typeof(LevelUpMenu).GetField("currentSkill", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lvlMenu).Equals(0) && typeof(LevelUpMenu).GetField("currentLevel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lvlMenu).Equals(10))
            {
                Game1.activeClickableMenu = new DistillerMenu(0, 10);
            }

            if (e.NewMenu is DialogueBox box && box.getCurrentString().Contains("sleep for the night"))
            {
                bedTime = Game1.timeOfDay;
            }

            if (Game1.activeClickableMenu is CarpenterMenu carpenterMenu)
            {
                //Sets current Winery buildings to 11 width to stop overlay and removes invisible tiles for building moving 
                foreach (Building building in Game1.getFarm().buildings)
                {
                    if (building.buildingType.Value == "Winery")
                    {
                        for (int x = building.tileX.Value + 9; x < building.tileX.Value + 11; x++)
                        {
                            for (int y = building.tileY.Value; y < building.tileY.Value + 6; y++)
                            {
                                Game1.getFarm().removeTile(x, y, "Buildings");
                            }
                        }
                        building.tilesWide.Value = 11;
                    }
                }
                if (!IsMagical(carpenterMenu) && !HasBluePrint(carpenterMenu))
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

                    GetBluePrints(carpenterMenu).Add(wineryBluePrint);
                }
            }
        }

        public static bool IsMagical(CarpenterMenu carpenterMenu)
        {
            return (bool)typeof(CarpenterMenu).GetField("magicalConstruction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        public static bool HasBluePrint(CarpenterMenu carpenterMenu)
        {
            return GetBluePrints(carpenterMenu).Exists(bluePrint => bluePrint.name == "Winery");
        }

        public static List<BluePrint> GetBluePrints(CarpenterMenu carpenterMenu)
        {
            return (List<BluePrint>)typeof(CarpenterMenu).GetField("blueprints", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        public static void SetBluePrintField(BluePrint bluePrint, string field, object value)
        {
            typeof(BluePrint).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(bluePrint, value);
        }

        //sets back Winery widths to 8 for Archway walkthrough and add back invisible tiles
        private void MenuEvents_MenuClosed(object sender, EventArgsClickableMenuClosed e)
        {
            if (e.PriorMenu is LevelUpMenu)
            {
                monitor.Log($"" + Game1.player.professions.ToString());
            }
            if (e.PriorMenu is CarpenterMenu)
            {
                foreach (Building building in Game1.getFarm().buildings)
                {
                    if (building.buildingType.Value == "Winery")
                    {
                        building.tilesWide.Value = 8;
                        layer = Game1.getFarm().map.GetLayer("Buildings");
                        tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
                        tileID = 131;
                        for (int x = building.tileX.Value + 9; x < building.tileX.Value + 11; x++)
                        {
                            for (int y = building.tileY.Value; y < building.tileY.Value + 6; y++)
                            {
                                layer.Tiles[x, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileID);
                            }
                        }
                    }
                }
            }
        }
        /*
         * END OF ADDING WINERY TO CARPENTER MENU
         * 
         */




        /*
         * EDIT WINERY WIDTH
         * 
         */
        public void LocationEvents_BuildingsChanged(object sender, EventArgsLocationBuildingsChanged e)
        {
            foreach (Building building in e.Added)
            {
                if (building.buildingType.Value == "Winery")
                {
                    foreach (Building b in Game1.getFarm().buildings)
                    {
                        if (b.buildingType.Value == "Winery")
                        {
                            b.tilesWide.Value = 8;
                            //monitor.Log($"" + b.buildingType.Value + " has width of " + b.tilesWide.Value);
                            layer = Game1.getFarm().map.GetLayer("Buildings");
                            tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
                            tileID = 131;
                            for (int x = b.tileX.Value + 9; x < b.tileX.Value + 11; x++)
                            {
                                for (int y = b.tileY.Value; y < b.tileY.Value + 6; y++)
                                {
                                    layer.Tiles[x, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileID);
                                }
                            }
                            if (b.daysOfConstructionLeft.Value > 0)
                            {
                                b.tilesWide.Value = 11;
                            }
                        }
                    }
                }
            }
            foreach (Building building in e.Removed)
            {
                if (building.buildingType.Value == "Winery")
                {
                    tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
                    tileID = 131;
                    for (int x = building.tileX.Value + 9; x < building.tileX.Value + 11; x++)
                    {
                        for (int y = building.tileY.Value; y < building.tileY.Value + 6; y++)
                        {
                            Game1.getFarm().removeTile(x, y, "Buildings");
                        }
                    }
                }
            }

        }
        /*
         * END EDIT WINERY WIDTH
         * 
         */



        /*
         * SAVE AND LOADING
         * Removing the Wineries, saving their coordinates, replacing with Slime Hutches, and reloading the wineries.
         */
        public void SaveEvents_AfterSaveLoad(object sender, EventArgs e)
        {
            SetItemCategory(-77);

            wineryCoords = this.Helper.ReadJsonFile<List<KeyValuePair<int, int>>>($"{Constants.CurrentSavePath}/Winery_Coords.json") ?? new List<KeyValuePair<int, int>>();
            foreach (Building b in Game1.getFarm().buildings)
            {
                foreach (var pair in wineryCoords)
                {
                    if (b.tileX.Value == pair.Key && b.tileY.Value == pair.Value && b.buildingType.Value == "Slime Hutch")
                    {
                        b.tilesWide.Value = 8;
                        b.buildingType.Value = "Winery";
                        b.indoors.Value.mapPath.Value = "Maps\\Winery";
                        b.indoors.Value.updateMap();
                        layer = Game1.getFarm().map.GetLayer("Buildings");
                        tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
                        tileID = 131;
                        for (int x = b.tileX.Value + 9; x < b.tileX.Value + 11; x++)
                        {
                            for (int y = b.tileY.Value; y < b.tileY.Value + 6; y++)
                            {
                                layer.Tiles[x, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileID);
                            }
                        }
                        if (b.daysOfConstructionLeft.Value > 0)
                        {
                            b.tilesWide.Value = 11;
                        }
                    }
                }
            }
        }

        public void SaveEvents_BeforeSave(object sender, EventArgs e)
        {
            //monitor.Log($"Time is" + Game1.timeOfDay);

            //calculate time slept            
            if (bedTime > 0)
            {
                hoursSlept = ((2400 - bedTime) + Game1.timeOfDay);
                //monitor.Log($"BedTime was " + bedTime + ". Wake up time was " + Game1.timeOfDay + ". You slept " + hoursSlept/100 + ".");
                //monitor.Log($"Time to reduce is " + Math.Round(hoursSlept * 0.3, 0));
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

            SetItemCategory(-26);

            wineryCoords.Clear();
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                {
                    wineryCoords.Add(new KeyValuePair<int, int>(b.tileX.Value, b.tileY.Value));
                    b.tilesWide.Value = 11;
                    b.buildingType.Value = "Slime Hutch";
                    b.indoors.Value.mapPath.Value = "Maps\\SlimeHutch";
                    b.indoors.Value.updateMap();
                    for (int x = b.tileX.Value + 9; x < b.tileX.Value + 11; x++)
                    {
                        for (int y = b.tileY.Value; y < b.tileY.Value + 6; y++)
                        {
                            Game1.getFarm().removeTile(x, y, "Buildings");
                        }
                    }
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
            //monitor.Log($"Time is " + Game1.timeOfDay + " and it is " + Game1.dayOrNight());
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                    foreach (SObject o in b.indoors.Value.Objects.Values)
                    {
                        if (o.Name.Equals("Keg"))
                            o.MinutesUntilReady -= 3;
                    }
            }
        }
        /*
         * END SPEED UP KEG INSIDE WINERY
         * 
         */



        /*
         * SPEED UP CASK INSIDE WINERY
         * 
         */
        public void TimeEvents_AfterDayStarted(object sender, EventArgs e)
        {
            Game1.activeClickableMenu = new LevelUpMenu(0, 10);

            //set seasonal building and reload texture
            if(CurrentSeason != Game1.currentSeason)
            {
                //monitor.Log($"Current season is " + CurrentSeason);
                CurrentSeason = Game1.currentSeason;
                //monitor.Log($"Current season is now " + CurrentSeason);
                Winery_outdoors = helper.Content.Load<Texture2D>($"assets/Winery_outside_{Game1.currentSeason}.png", ContentSource.ModFolder);
                helper.Content.InvalidateCache("Buildings/Winery");
            }            

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
        * SET ITEM CATEGORY
        * 
        */ 
        public void SetItemCategory(int catID)
        {
            //check for old wine in player inventory
            foreach (Item item in Game1.player.Items)
            {
                if (item != null && (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459) && item.Category != catID)
                {
                    item.Category = catID;
                }
            }

            //check for old wine everywhere else
            foreach (GameLocation location in ModEntry.GetLocations())
            {
                foreach (SObject obj in location.Objects.Values)
                {
                    if (obj is Chest c)
                    {
                        foreach (Item item in c.items)
                        {
                            if (item != null && (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459) && item.Category != catID)
                            {
                                item.Category = catID;
                            }
                        }
                    }
                    else if (obj.ParentSheetIndex == 165 && obj.heldObject.Value is Chest autoGrabberStorage)
                    {
                        foreach (Item item in autoGrabberStorage.items)
                        {
                            if (item != null && (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459) && item.Category != catID)
                            {
                                item.Category = catID;
                            }
                        }
                    }
                    else if (obj is Cask cask)
                    {
                        if (cask.heldObject.Value != null && (cask.heldObject.Value.ParentSheetIndex == 348 || cask.heldObject.Value.ParentSheetIndex == 303 || cask.heldObject.Value.ParentSheetIndex == 346 || cask.heldObject.Value.ParentSheetIndex == 459) && cask.heldObject.Value.Category != catID)
                        {
                            cask.heldObject.Value.Category = catID;
                        }
                    }
                }
                if (location is FarmHouse house)
                {
                    foreach (Item item in house.fridge.Value.items)
                    {
                        if (item != null && (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459) && item.Category != catID)
                        {
                            item.Category = catID;
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
                                if (item != null && (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459) && item.Category != catID)
                                {
                                    item.Category = catID;
                                }
                            }
                        }
                        else if (building is JunimoHut hut)
                        {
                            foreach (Item item in hut.output.Value.items)
                            {
                                if (item != null && (item.ParentSheetIndex == 348 || item.ParentSheetIndex == 303 || item.ParentSheetIndex == 346 || item.ParentSheetIndex == 459) && item.Category != catID)
                                {
                                    item.Category = catID;
                                }
                            }
                        }
                    }
                }
            }
            //end old wine check
        }
        /*
         * END SET ITEM CATEGORY
         * 
         */ 




        /*
         * FIX ISSUE WITH WINERY MAP
         * 
         */
        public void GraphicsEvents_OnPreRenderEvent(object sender, EventArgs e)
        {
            if (Game1.hasLoadedGame)
            {
                if (Game1.currentLocation.mapPath.Value == "Maps\\Winery" && Game1.currentLocation != null)
                {
                    layer = Game1.currentLocation.map.GetLayer("Buildings");
                    tilesheet = Game1.currentLocation.map.GetTileSheet("untitled tile sheet");
                    tileID = 1367;
                    for (int y = 6; y < 10; y++)
                    {
                        Game1.currentLocation.removeTile(16, y, "Buildings");
                        layer.Tiles[16, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileID);
                    }
                }
            }
        }
        /*
         * END ISSUE FIX
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
                                Vector2 vector2;
                                Vector2 vector2_1;
                                string text1;
                                float num2;
                                float num3;
                                Vector2 tooltipOffset = this.TooltipOffset;

                                if (obj.Name.Equals("Keg") && obj.MinutesUntilReady > 0)
                                {
                                    text1 = Math.Round((obj.MinutesUntilReady * 0.6) / 84, 1).ToString() + " minutes";
                                    vector2 = Game1.smallFont.MeasureString(text1);
                                    vector2_1 = new Vector2(ToFloat(vector2.X + 5.0 + 5.0), ToFloat(vector2.Y + 5.0));
                                    num2 = (float)Mouse.GetState().X / Game1.options.zoomLevel - tooltipOffset.X - vector2_1.X;
                                    num3 = (float)((double)Mouse.GetState().Y / (double)Game1.options.zoomLevel + (double)tooltipOffset.Y + 12.0);
                                    IClickableMenu.drawTextureBox(Game1.spriteBatch, Game1.menuTexture, this.TooltipSourceRect, (int)num2, (int)num3, (int)vector2_1.X + 27, (int)vector2_1.Y + 20, Color.White, 1f, true);
                                    Utility.drawTextWithShadow(Game1.spriteBatch, text1, Game1.smallFont, new Vector2((float)((double)num2 + (double)vector2_1.X) - (vector2.X - 4), (float)((double)num3 + 6.0 + 5.0)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
                                }

                                if (obj is Cask c && c.daysToMature.Value > 0)
                                {
                                    text1 = Math.Round(c.daysToMature.Value * 0.6, 1).ToString() + " days";
                                    vector2 = Game1.smallFont.MeasureString(text1);
                                    vector2_1 = new Vector2(ToFloat(vector2.X + 5.0 + 5.0), ToFloat(vector2.Y + 5.0));
                                    num2 = (float)Mouse.GetState().X / Game1.options.zoomLevel - tooltipOffset.X - vector2_1.X;
                                    num3 = (float)((double)Mouse.GetState().Y / (double)Game1.options.zoomLevel + (double)tooltipOffset.Y + 12.0);
                                    IClickableMenu.drawTextureBox(Game1.spriteBatch, Game1.menuTexture, this.TooltipSourceRect, (int)num2, (int)num3, (int)vector2_1.X + 27, (int)vector2_1.Y + 20, Color.White, 1f, true);
                                    Utility.drawTextWithShadow(Game1.spriteBatch, text1, Game1.smallFont, new Vector2((float)((double)num2 + (double)vector2_1.X) - (vector2.X - 4), (float)((double)num3 + 6.0 + 5.0)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
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
            else if (asset.AssetNameEquals("Strings/UI"))
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
            if (asset.AssetNameEquals("Strings/UI"))
            {
                IDictionary<string, string> stringInfo = asset.AsDictionary<string, string>().Data;

                stringInfo["LevelUp_ProfessionDescription_Artisan"] = "Artisan goods (cheese, truffle oil, cloth, etc.) worth 40% more.";
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
            if (__instance is Furniture)
            {
                __result = new Color(100, 25, 190);
                return false;
            }

            if (__instance.Type == null && __instance.Type.Equals((object)"Arch"))
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

        public static bool Patch_getCategoryName(SObject __instance, ref string __result)
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
        /*
        * END OF PATCH METHOD FOR HARMONY
        * 
        */
    }
}
