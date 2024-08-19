using System.Collections.Generic;

namespace Settlers.Settlers;

public static class FoodSay
{
    private static readonly Dictionary<string, List<string>> m_foodSayMap = new()
    {
        ["$item_raspberries"] = new List<string>(){"$item_raspberries $item_raspberries_say"},
        ["$item_boar_meat_cooked"] = new List<string>(){"$item_boar_meat_cooked $item_boar_meat_cooked_say"},
        ["$item_honey"] = new List<string>(){"$item_honey $item_honey_say"},
        ["$item_deer_meat_cooked"] = new List<string>(){"$item_deer_meat_cooked $item_deer_meat_cooked_say"},
        ["$item_necktailgrilled"] = new List<string>(){"$item_necktailgrilled $item_necktailgrilled_say"},
        ["$item_blueberries"] = new List<string>(){"$item_blueberries $item_blueberries_say"},
        ["$item_carrot"] = new List<string>(){"$item_carrot $item_carrot_say"},
        ["$item_deerstew"] = new List<string>(){"$item_deerstew $item_deerstew_say"},
        ["$item_boarjerky"] = new List<string>(){"$item_boarjerky $item_boarjerky_say"},
        ["$item_queensjam"] = new List<string>(){"$item_queensjam $item_queensjam_say"},
        ["$item_fish_cooked"] = new List<string>(){"$item_fish_cooked $item_fish_cooked_say"},
        ["$item_pukeberries"] = new List<string>(){"$item_pukeberries $item_pukeberries_say"}
    };

    public static List<string> GetFoodSay(string sharedItemName, List<string> defaultValue) => m_foodSayMap.TryGetValue(sharedItemName, out List<string> value) ? value : defaultValue;

    public static List<string> GetConsumeSay(string sharedItemName)
    {
        var say = $"{sharedItemName} {sharedItemName}_say";
        var localized = Localization.instance.Localize(say);
    
        return localized.Contains("[") ? new List<string>()
        {
            $"{sharedItemName} $consume_say_1",
            $"{sharedItemName} $consume_say_2",
            $"{sharedItemName} $consume_say_3",
            $"{sharedItemName} $consume_say_4",
            $"{sharedItemName} $consume_say_5"
        } : new List<string>() { say };
    }
}