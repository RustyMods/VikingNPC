using HarmonyLib;
using Settlers.Behaviors;
using UnityEngine;

namespace Settlers.Settlers;

public static class StorePatches
{
    [HarmonyPatch(typeof(StoreGui), nameof(StoreGui.BuySelectedItem))]
    private static class StoreGUI_BuySelectedItem_Patch
    {
        private static bool Prefix(StoreGui __instance)
        {
            if (__instance.m_selectedItem == null || !__instance.CanAfford(__instance.m_selectedItem)) return true;
            if (__instance.m_selectedItem.m_prefab.m_itemData.m_shared.m_name == "$name_vikingsettler")
            {
                var prefab = ZNetScene.instance.GetPrefab("VikingSettler");
                var random = Random.insideUnitCircle * 5f;
                var pos = Player.m_localPlayer.transform.position + new Vector3(random.x, 0f, random.y);
                var clone = Object.Instantiate(prefab, pos, Quaternion.identity);
                if (!clone.TryGetComponent(out Companion component)) return false;
                if (!clone.TryGetComponent(out TameableCompanion tameableCompanion)) return false;
                tameableCompanion.Tame();
                component.SetLevel(Random.Range(0, 3));
                Player.m_localPlayer.GetInventory().RemoveItem(__instance.m_coinPrefab.m_itemData.m_shared.m_name, __instance.m_selectedItem.m_price);
                __instance.m_buyEffects.Create(__instance.transform.position, Quaternion.identity);
                __instance.FillList();
                return false;
            }
            return true;
        }
    }
}