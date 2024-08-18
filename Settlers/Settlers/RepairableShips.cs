using HarmonyLib;

namespace Settlers.Settlers;

public static class RepairableShips
{
    [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
    private static class Player_Repair_Patch
    {
        private static bool Prefix(Player __instance, ItemDrop.ItemData toolItem)
        {
            if (SettlersPlugin._repairShips.Value is SettlersPlugin.Toggle.Off) return true;
            if (!__instance.InPlaceMode()) return true;
            Piece hoveringPiece = __instance.GetHoveringPiece();
            if (!hoveringPiece) return true;
            if (!hoveringPiece.m_waterPiece) return true;
            
            if (!hoveringPiece.TryGetComponent(out WearNTear wearNTear)) return false;

            if (SettlersPlugin._repairShipValue.Value > 0)
            {
                var health = wearNTear.GetHealthPercentage();
                var difference = 1f - health;

                if (difference <= 0f)
                {
                    __instance.Message(MessageHud.MessageType.TopLeft, hoveringPiece.m_name + " $msg_doesnotneedrepair");
                    return false;
                }

                var material = ZNetScene.instance.GetPrefab(SettlersPlugin._repairShipMat.Value);
                if (!material)
                {
                    SettlersPlugin.SettlersLogger.LogWarning("Invalid prefab used for ship repair material, using wood as default");
                    material = ZNetScene.instance.GetPrefab("Wood");
                }

                if (!material.TryGetComponent(out ItemDrop itemDrop)) return false;

                var name = itemDrop.m_itemData.m_shared.m_name;
                
                var amount = (int)(difference * 100 / SettlersPlugin._costModifier.Value);

                if (!__instance.GetInventory().HaveItem(name))
                {
                    __instance.Message(MessageHud.MessageType.Center, "$msg_repairship: " + $" {amount}x {name}");
                    return false;
                }

                var playerAmount = __instance.GetInventory().CountItems(name);
                if (playerAmount < amount)
                {
                    __instance.Message(MessageHud.MessageType.Center, "$msg_repairship: " + $" {amount}x {name}");
                    return false;
                }

                __instance.GetInventory().RemoveItem(name, amount);
            }
            
            if (!wearNTear.Repair())
            {
                __instance.Message(MessageHud.MessageType.TopLeft, hoveringPiece.m_name + " $msg_doesnotneedrepair");
                return false;
            }
            __instance.FaceLookDirection();
            __instance.m_zanim.SetTrigger(toolItem.m_shared.m_attack.m_attackAnimation);
            var transform = hoveringPiece.transform;
            hoveringPiece.m_placeEffect.Create(transform.position, transform.rotation);
            __instance.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$msg_repaired", hoveringPiece.m_name));
            __instance.UseStamina(toolItem.m_shared.m_attack.m_attackStamina, true);
            __instance.UseEitr(toolItem.m_shared.m_attack.m_attackEitr);
            if (!toolItem.m_shared.m_useDurability) return false;
            toolItem.m_durability -= toolItem.m_shared.m_useDurabilityDrain;
            return false;
        }
    }
}