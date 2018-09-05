//Global Variables
//public readonly Vector2[] bigKegsInput = new Vector2[] { new Vector2(0,0), new Vector2(0,0), new Vector2(0,0), new Vector2(0,0) };
//public readonly Vector2[] bigKegsOutput = new Vector2[] { new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0) };


//Inside Entry Method
//InputEvents.ButtonPressed += InputEvents_ButtonPressed;

        public bool IsKegable(Item _item)
        {
            int _index = _item.ParentSheetIndex;
            int _category = _item.Category;
            return ( _index == 262 || _index == 304 || _index == 340 || _index == 433 || _category == -79 || _category == -75 );
        }

        public bool IsBigKegInput(Vector2 _position)
        {
            return this.bigKegsInput.Contains(_position);
        }

        public bool IsBigKegOutput(Vector2 _position)
        {
            return this.bigKegsOutput.Contains(_position);
        }

        private void InputEvents_ButtonPressed(object sender, EventArgsInput e)
        {
            if( Game1.currentLocation != null && Game1.currentLocation.mapPath.Value == "Maps\\Winery" && IsBigKegInput(e.Cursor.GrabTile) )
            {
                if( e.IsActionButton )
                {
                    GameLocation _winery = Game1.currentLocation;
                    Vector2 _chestLocation = new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 14);
                    if ( !_winery.Objects.ContainsKey( _chestLocation ) )
                    {
                        Chest _newChest = new Chest(true){TileLocation = _chestLocation};
                        _winery.Objects.Add(_chestLocation, _newChest);
                    }
                    if( Game1.player.CurrentItem != null && IsKegable(Game1.player.CurrentItem) )
                    {
                        Chest _chest = (Chest)_winery.Objects[_chestLocation];
                        Item _item = Game1.player.CurrentItem;
                        Item _remainder = _chest.addItem(_item);
                        if(_remainder == null)
                        {
                            Game1.player.removeItemFromInventory(_item);
                        }
                        foreach (Item _chestItem in _chest.items)
                        {
                            switch (_chestItem.ParentSheetIndex)
                            {
                                case 262:
                                    _chestItem = new SObject(Vector2.Zero, 346, "Beer", false, true, false, false) { Name = "Beer" };
                                    break;
                                case 304:
                                    _chestItem = new SObject(Vector2.Zero, 303, "Pale Ale", false, true, false, false) { Name = "Pale Ale" };
                                    break;
                                case 340:
                                    _chestItem = new SObject(Vector2.Zero, 459, "Mead", false, true, false, false) { Name = "Mead" };
                                    break;
                                case 433:
                                    _chestItem = new SObject(Vector2.Zero, 395, "Coffee", false, true, false, false) { Name = "Coffee" };
                                    _chestItem.Stack /= 5;
                                    break;
                                default:
                                    switch (_chestItem.Category)
                                    {
                                        case -79:
                                            _chestItem = new SObject(Vector2.Zero, 348, _chestItem.Name + " Wine", false, true, false, false) { Name = _chestItem.Name + " Wine" };
                                            helper.Reflection.GetField<SObject.PreserveType>(_chestItem, "preserve").SetValue( new SObject.PreserveType?(SObject.PreserveType.Wine) );
                                            helper.Reflection.GetField<int>(_chestItem, "preservedParentSheetIndex").SetValue( _chestItem.ParentSheetIndex );
                                            break;
                                        case -75:
                                            _chestItem = new SObject(Vector2.Zero, 350, _chestItem.Name + " Juice", false, true, false, false);
                                            break;
                                    }
                                    break;
                            }
                        }
                    }                    
                }                
            }
            else if ( Game1.currentLocation != null && Game1.currentLocation.mapPath.Value == "Maps\\Winery" && IsBigKegOutput(e.Cursor.GrabTile) )
            {
                if( e.IsActionButton)
                {
                    GameLocation _winery = Game1.currentLocation;
                    Vector2 _chestLocation = new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 24);
                    if (!_winery.Objects.ContainsKey(_chestLocation))
                    {
                        Chest _newChest = new Chest(true) { TileLocation = _chestLocation };
                        _winery.Objects.Add(_chestLocation, _newChest);
                    }
                    Chest _chest = (Chest)_winery.Objects[_chestLocation];
                    Game1.activeClickableMenu = new ItemGrabMenu(
                       inventory: _chest.items,
                       reverseGrab: false,
                       showReceivingMenu: true,
                       highlightFunction: InventoryMenu.highlightAllItems,
                       behaviorOnItemSelectFunction: _chest.grabItemFromInventory,
                       message: null,
                       behaviorOnItemGrab: _chest.grabItemFromChest,
                       canBeExitedWithKey: true, showOrganizeButton: true,
                       source: ItemGrabMenu.source_chest,
                       context: _chest
                   );
                }               
            }
        }
