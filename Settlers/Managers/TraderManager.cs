using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Settlers.Managers;

public static class TraderManager
{
    private static readonly Dictionary<string, MerchantItem> TradeItems = new();
    private static readonly Dictionary<string, MerchantItem> SharedTradeItems = new();
    public enum Merchant
    {
        None = 0, 
        Haldor = 1 << 0, 
        Hildir = 1 << 1
    }

    [HarmonyPatch(typeof(Trader), nameof(Trader.GetAvailableItems))]
    private static class Trader_GetAvailableItems_Patch
    {
        private static void Postfix(Trader __instance, ref List<Trader.TradeItem> __result)
        {
            Merchant merchant = Utils.GetPrefabName(__instance.gameObject) switch
            {
                "Haldor" => Merchant.Haldor,
                "Hildir" => Merchant.Hildir,
                _ => 0,
            };
            __result.AddRange(TradeItems.Values.Where(item => item.Enabled.Value is SettlersPlugin.Toggle.On && item.Trader == merchant).Select(item => item.TradeItem));
        }
    }

    public class MerchantItem
    {
        public string OriginalItem;
        public string PrefabName;
        public string SharedName;
        public string Description;
        public Action<StoreGui>? Action;
        public Merchant Trader = Merchant.Haldor;
        public GameObject Prefab = null!;
        public Trader.TradeItem TradeItem = null!;
        public ConfigEntry<string> RequiredGlobalKey = null!;
        public ConfigEntry<int> Cost = null!;
        public ConfigEntry<SettlersPlugin.Toggle> Enabled = null!;
        public int Level = 1;

        public MerchantItem(string originalItem, string prefabName)
        {
            OriginalItem = originalItem;
            PrefabName = prefabName;
            SharedName = $"$item_{prefabName.ToLower()}";
            Description = $"$item_{prefabName.ToLower()}_desc";
            TradeItems[prefabName] = this;
        }

        public void Load()
        {
            if (!ObjectDB.instance || ObjectDB.instance.GetItemPrefab(OriginalItem) is not {} original) return;
            Prefab = Object.Instantiate(original, SettlersPlugin._Root.transform, false);
            Prefab.name = PrefabName;
            if (!Prefab.TryGetComponent(out ItemDrop itemDrop)) return;
            itemDrop.m_itemData.m_shared.m_name = SharedName;
            itemDrop.m_itemData.m_shared.m_description = Description;
            itemDrop.m_itemData.m_shared.m_movementModifier = 0f;
            itemDrop.m_itemData.m_shared.m_useDurability = false;
            itemDrop.m_itemData.m_shared.m_weight = 0f;
            itemDrop.m_itemData.m_shared.m_maxQuality = 0;
            itemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            VikingManager.Register(Prefab);
            VikingManager.RegisterToDB(Prefab);
            TradeItem = new Trader.TradeItem()
            {
                m_price = Cost.Value,
                m_stack = 1,
                m_prefab = itemDrop,
                m_requiredGlobalKey = RequiredGlobalKey.Value
            };
            Cost.SettingChanged += (_, _) => TradeItem.m_price = Cost.Value;
            RequiredGlobalKey.SettingChanged += (_, _) => TradeItem.m_requiredGlobalKey = RequiredGlobalKey.Value;
            SharedTradeItems[SharedName] = this;
        }

    }

    public static void Setup()
    {
        foreach(var item in TradeItems.Values) item.Load();
    }
    
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.BuySelectedItem))]
    private static class StoreGUI_BuySelectedItem_Patch
    {
        private static bool Prefix(StoreGui __instance)
        {
            if (__instance.m_selectedItem == null || !__instance.CanAfford(__instance.m_selectedItem)) return true;
            if (!SharedTradeItems.TryGetValue(__instance.m_selectedItem.m_prefab.m_itemData.m_shared.m_name, out MerchantItem merchantItem)) return true;
            merchantItem.Action?.Invoke(__instance);
            return false;
        }
    }
}