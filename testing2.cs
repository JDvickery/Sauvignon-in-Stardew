//Global Variables
public Texture2D Winery_outdoors;
public Map kegRoom_indoors;

//Inside Entry Method
Winery_outdoors = helper.Content.Load<Texture2D>($"assets/Winery_outside_{Game1.currentSeason}.png", ContentSource.ModFolder);
kegRoom_indoors = helper.Content.Load<Map>("assets/Winery2.tbin", ContentSource.ModFolder);
MenuEvents.MenuChanged += MenuEvents_MenuChanged;

//Inside Class
public static bool IsMagical(IClickableMenu carpenterMenu)
{
    return helper.Reflection.GetField<bool>(carpenterMenu, "magicalConstruction").GetValue();
}

public static bool HasBluePrint(IClickableMenu carpenterMenu, string blueprintName)
{
    return GetBluePrints(carpenterMenu).Exists(bluePrint => bluePrint.name == blueprintName);
}

public static void SetBluePrintField(BluePrint bluePrint, string field, object value)
{
    helper.Reflection.GetField<object>(bluePrint, field).SetValue(value);
}

if (e.NewMenu is CarpenterMenu)
{
  if (!Game1.getFarm().buildings.Any(building => building.buildingType.Value == "Winery") && !IsMagical(e.NewMenu) && !HasBluePrint(e.NewMenu, "Winery2"))
    {
        BluePrint kegRoomBluePrint = new BluePrint("Slime Hutch")
        {
            name = "Winery2",
            displayName = "Keg Room",
            description = "Adds a room to your Winery that houses huge kegs able to process large quantities of products.",
            daysToConstruct = 2,
            moneyRequired = 450000,
            blueprintType = "Upgrades",
            nameOfBuildingToUpgrade = "Winery"
        };
        kegRoomBluePrint.itemsRequired.Clear();
        kegRoomBluePrint.itemsRequired.Add(709, 200);//200
        kegRoomBluePrint.itemsRequired.Add(335, 150);//150
        kegRoomBluePrint.itemsRequired.Add(388, 500);//500

        SetBluePrintField(kegRoomBluePrint, "textureName", "Buildings\\Winery2");
        SetBluePrintField(kegRoomBluePrint, "texture", Game1.content.Load<Texture2D>(kegRoomBluePrint.textureName));
    }
}

//Loader
public bool CanLoad<T>(IAssetInfo asset)
{
    if (asset.AssetNameEquals("Buildings\\Winery2"))
    {
        return true;
    }
    else if (asset.AssetNameEquals("Maps/Winery2"))
    {
        return true;
    }
    return false;
}

public T Load<T>(IAssetInfo asset)
{
    if (asset.AssetNameEquals("Buildings\\Winery2"))
    {
        return (T)(object)Winery_outdoors;
    }
    else if (asset.AssetNameEquals("Maps/Winery2"))
    {
        return (T)(object)kegRoom_indoors;
    }
    return (T)(object)null;
}
