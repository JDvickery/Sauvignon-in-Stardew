//Global Variables
public readonly Vector2[] bigKegsInput = new Vector2[] { new Vector2(20,3), new Vector2(23,3), new Vector2(26,3), new Vector2(29,3), new Vector2(32,3) };
public readonly Vector2[] bigKegsOutput = new Vector2[] { new Vector2(20,6), new Vector2(23,6), new Vector2(26,6), new Vector2(29,6), new Vector2(32,6) };


//Inside Entry Method
InputEvents.ButtonPressed += InputEvents_ButtonPressed;

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

    public void SetKegAnimation(Layer _layer, Vector2 _tileLocation, TileSheet _tilesheet, int[] _tileIDs, long _interval)
    {
        _layer.Tiles[(int)_tileLocation.X, (int)_tileLocation.Y] = new AnimatedTile(_layer, MakeAnimatedTile(_layer, _tilesheet, _tileIDs), _interval);
    }

    public StaticTile[] MakeAnimatedTile(Layer _layer, TileSheet _tilesheet, int[] _tileIDs)
    {
        StaticTile[] _output = new StaticTile[_tileIDs.Count()];
        for(int i = 0; i < _tileIDs.Count(); i++)
        {
            _output[i] = new StaticTile(_layer, _tilesheet, BlendMode.Alpha, _tileIDs[i]);
        }
        return _output;
    }

    private void InputEvents_ButtonPressed(object sender, EventArgsInput e)
    {
        if( Game1.currentLocation != null && Game1.currentLocation.mapPath.Value == "Maps\\Winery" && IsBigKegInput(e.Cursor.GrabTile) )
        {
            if( e.IsActionButton )
            {
                GameLocation _winery = Game1.currentLocation;                    
                Vector2 _chestLocation = new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 14);

                Layer _layerBuildings = _winery.map.GetLayer("Buildings");
                Layer _layerFront = _winery.map.GetLayer("Front");
                TileSheet _tilesheet = _winery.map.GetTileSheet("bucket_anim");


                if ( !_winery.Objects.ContainsKey( _chestLocation ) )
                {
                    Chest _newChest = new Chest(true){TileLocation = _chestLocation};
                    _winery.Objects.Add(_chestLocation, _newChest);
                }
                if( Game1.player.CurrentItem != null && IsKegable(Game1.player.CurrentItem) )
                {
                    Chest _chest = (Chest)_winery.Objects[_chestLocation];
                    Item _item = Game1.player.CurrentItem;
                    Item _remainder = null;
                    switch (_item.ParentSheetIndex)
                    {
                        case 262:
                            _item = new SObject(Vector2.Zero, 346, "Beer", false, true, false, false) { Name = "Beer" };
                            ((SObject)_item).setHealth(1750);
                            _remainder = _chest.addItem(_item);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 4, 5, 6 }, 250);
                            SetKegAnimation(_layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 18, 19, 20 }, 250);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), _tilesheet, new int[] { 11, 12, 13 }, 250);
                            break;
                        case 304:
                            _item = new SObject(Vector2.Zero, 303, "Pale Ale", false, true, false, false) { Name = "Pale Ale" };
                            ((SObject)_item).setHealth(2250);
                            _remainder = _chest.addItem(_item);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 4, 5, 6 }, 250);
                            SetKegAnimation(_layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 18, 19, 20 }, 250);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), _tilesheet, new int[] { 11, 12, 13 }, 250);
                            break;
                        case 340:
                            _item = new SObject(Vector2.Zero, 459, "Mead", false, true, false, false) { Name = "Mead" };
                            ((SObject)_item).setHealth(600);
                            _remainder = _chest.addItem(_item);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 4, 5, 6 }, 250);
                            SetKegAnimation(_layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 18, 19, 20 }, 250);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), _tilesheet, new int[] { 11, 12, 13 }, 250);
                            break;
                        case 433:
                            _item = new SObject(Vector2.Zero, 395, "Coffee", false, true, false, false) { Name = "Coffee" };
                            _item.Stack = (_item.Stack / 5) - (_item.Stack % 5);
                            ((SObject)_item).setHealth(120);
                            _remainder = _chest.addItem(_item);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 1, 2, 3 }, 250);
                            SetKegAnimation(_layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 15, 16, 17 }, 250);
                            SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), _tilesheet, new int[] { 8, 9, 10 }, 250);
                            break;
                        default:
                            switch (_item.Category)
                            {
                                case -79:
                                    _item = new SObject(Vector2.Zero, 348, _item.Name + " Wine", false, true, false, false) { Name = _item.Name + " Wine" };
                                    ((SObject)_item).Price = ((SObject)_item).Price * 3; 
                                    helper.Reflection.GetField<SObject.PreserveType>(_item, "preserve").SetValue( SObject.PreserveType.Wine );
                                    helper.Reflection.GetField<int>(_item, "preservedParentSheetIndex").SetValue(_item.ParentSheetIndex );
                                    ((SObject)_item).setHealth(10000);
                                    _remainder = _chest.addItem(_item);
                                    SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 1, 2, 3 }, 250);
                                    SetKegAnimation(_layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 15, 16, 17 }, 250);
                                    SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), _tilesheet, new int[] { 8, 9, 10 }, 250);
                                    break;
                                case -75:
                                    _item = new SObject(Vector2.Zero, 350, _item.Name + " Juice", false, true, false, false) { Name = _item.Name + " Juice" };
                                    ((SObject)_item).Price = (int)((double)((SObject)_item).Price * 2.5);
                                    helper.Reflection.GetField<SObject.PreserveType>(_item, "preserve").SetValue(SObject.PreserveType.Juice);
                                    helper.Reflection.GetField<int>(_item, "preservedParentSheetIndex").SetValue(_item.ParentSheetIndex);
                                    ((SObject)_item).setHealth(6000);
                                    _remainder = _chest.addItem(_item);
                                    SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 1, 2, 3 }, 250);
                                    SetKegAnimation(_layerFront, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 2), _tilesheet, new int[] { 15, 16, 17 }, 250);
                                    SetKegAnimation(_layerBuildings, new Vector2(e.Cursor.GrabTile.X, e.Cursor.GrabTile.Y + 3), _tilesheet, new int[] { 8, 9, 10 }, 250);
                                    break;
                            }
                            break;
                    }
                    if (_remainder == null)
                    {
                        Game1.player.removeItemFromInventory(_item);
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

