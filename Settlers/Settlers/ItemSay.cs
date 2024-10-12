using System.Collections.Generic;

namespace Settlers.Settlers;

public static class ItemSay
{
    public static List<string> GetItemSay(ItemDrop.ItemData item)
    {
        var say = $"{item.m_shared.m_name} $item_type_say_{item.m_shared.m_skillType.ToString().ToLower()}";
        if (say.Contains("[")) return new List<string>();
        return new List<string>()
        {
            say
        };
    }
    
    public static List<string> GetConsumeSay(string sharedName)
    {
        var say = $"{sharedName} {sharedName}_say";
        var localized = Localization.instance.Localize(say);
    
        return localized.Contains("[") ? new List<string>()
        {
            $"{sharedName} $consume_say_1",
            $"{sharedName} $consume_say_2",
            $"{sharedName} $consume_say_3",
            $"{sharedName} $consume_say_4",
            $"{sharedName} $consume_say_5"
        } : new List<string>() { say };
    }
}