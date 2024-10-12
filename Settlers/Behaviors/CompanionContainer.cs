using HarmonyLib;
using UnityEngine;

namespace Settlers.Behaviors;

public class CompanionContainer : MonoBehaviour
{
    private static CompanionContainer? m_currentCompanion;

    private static readonly int m_ownerKey = "VikingSettlerOwner".GetStableHashCode();
    private static readonly int Visible = Animator.StringToHash("visible");

    public ZNetView m_nview = null!;
    public Companion m_companion = null!;
    
    public bool m_inUse;

    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_companion = GetComponent<Companion>();

        if (!m_nview.IsValid()) return;
        
        m_nview.Register<long>(nameof(RPC_RequestOpen), RPC_RequestOpen);
        m_nview.Register<bool>(nameof(RPC_OpenResponse), RPC_OpenResponse);
        m_nview.Register<long>(nameof(RPC_RequestStack), RPC_RequestStack);
        m_nview.Register<bool>(nameof(RPC_StackResponse), RPC_StackResponse);
    }

    public void RequestOpen(long playerId)
    {
        if (!m_nview.IsValid()) return;
        m_nview.InvokeRPC(nameof(RPC_RequestOpen), playerId);
    }
    
    public bool CheckAccess(long playerID)
    {
        if (SettlersPlugin._ownerLock.Value is SettlersPlugin.Toggle.Off) return true;
        var owner = m_nview.GetZDO().GetLong(m_ownerKey);
        if (owner == 0L) return true;
        return playerID == owner;
    }
    
    public void RPC_RequestOpen(long uid, long playerID)
    {
        if (m_inUse)
        {
            m_nview.InvokeRPC(uid, nameof(RPC_OpenResponse), false);
        }
        else
        {
            if (!m_nview.IsOwner()) m_nview.ClaimOwnership();
            ZDOMan.instance.ForceSendZDO(uid, m_nview.GetZDO().m_uid);
            m_nview.GetZDO().SetOwner(uid);
            m_nview.InvokeRPC(nameof(RPC_OpenResponse), true);
        }
    }
    
    public void RPC_OpenResponse(long uid, bool granted)
    {
        if (!Player.m_localPlayer) return;
        if (granted)
        {
            Hud.HidePieceSelection();
            InventoryGui.instance.m_animator.SetBool(Visible, true);
            InventoryGui.instance.SetActiveGroup(1, false);
            InventoryGui.instance.SetupCrafting();
            m_currentCompanion = this;
        }
        else
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
        }
    }
    
    public void StackAll() => m_nview.InvokeRPC(nameof(RPC_RequestStack), Game.instance.GetPlayerProfile().GetPlayerID());
    
    public void RPC_RequestStack(long uid, long playerID)
    {
        if (!m_nview.IsOwner()) return;
        if (m_inUse || uid != ZNet.GetUID())
        {
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse), false);
        }
        else if (!CheckAccess(playerID))
        {
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse), false);
        }
        else
        {
            ZDOMan.instance.ForceSendZDO(uid, m_nview.GetZDO().m_uid);
            m_nview.GetZDO().SetOwner(uid);
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse), true);
        }
    }

    public void RPC_StackResponse(long uid, bool granted)
    {
        if (!Player.m_localPlayer) return;
        if (granted)
        {
            if (m_companion.GetInventory().StackAll(Player.m_localPlayer.GetInventory(), true) <= 0) return;
            InventoryGui.instance.m_moveItemEffects.Create(transform.position, Quaternion.identity);
            m_companion.UpdateEquipment();
        }
        else
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
        }
    }
    
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    private static class Companion_ContainerOverride
    {
        private static bool Prefix(InventoryGui __instance, Player player)
        {
            if (!__instance.m_animator.GetBool(Visible)) return true;
            if (__instance.m_currentContainer)
            {
                m_currentCompanion = null;
                return true;
            }

            if (m_currentCompanion == null) return true;
            if (m_currentCompanion.m_companion.IsOwner())
            {
                m_currentCompanion.m_inUse = true;
                __instance.m_container.gameObject.SetActive(true);
                __instance.m_containerGrid.UpdateInventory(m_currentCompanion.m_companion.GetInventory(), null, __instance.m_dragItem);
                __instance.m_containerName.text = m_currentCompanion.m_companion.GetHoverName();
                if (__instance.m_firstContainerUpdate)
                {
                    __instance.m_containerGrid.ResetView();
                    __instance.m_firstContainerUpdate = false;
                    __instance.m_containerHoldTime = 0.0f;
                    __instance.m_containerHoldState = 0;
                }

                if (Vector3.Distance(m_currentCompanion.transform.position, player.transform.position) >
                    __instance.m_autoCloseDistance)
                {
                    if (__instance.m_dragInventory != null &&
                        __instance.m_dragInventory != Player.m_localPlayer.GetInventory())
                    {
                        __instance.SetupDragItem(null, null, 1);
                    }
                    CloseCompanionInventory(m_currentCompanion.m_companion.m_inventoryChanged);
                    __instance.m_splitPanel.gameObject.SetActive(false);
                    __instance.m_firstContainerUpdate = true;
                    __instance.m_container.gameObject.SetActive(false);
                }

                if (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
                {
                    __instance.m_containerHoldTime += Time.deltaTime;
                    if (__instance.m_containerHoldTime > __instance.m_containerHoldPlaceStackDelay &&
                        __instance.m_containerHoldState == 0)
                    {
                        m_currentCompanion.StackAll();
                        __instance.m_containerHoldState = 1;
                    }
                    else
                    {
                        if (__instance.m_containerHoldTime <= __instance.m_containerHoldPlaceStackDelay +
                            __instance.m_containerHoldExitDelay || __instance.m_containerHoldState != 1)
                        {
                            return false;
                        }
                        __instance.Hide();
                    }
                }
                else
                {
                    if (__instance.m_containerHoldState < 0) return false;
                    __instance.m_containerHoldState = -1;
                }
            }
            else
            {
                __instance.m_container.gameObject.SetActive(false);
                if (__instance.m_dragInventory == null ||
                    __instance.m_dragInventory == Player.m_localPlayer.GetInventory()) return false;
                __instance.SetupDragItem(null, null, 1);
            }

            return false;
        }
    }
    
    private static void CloseCompanionInventory(bool updateEquipment = true)
    {
        if (m_currentCompanion == null) return;
        m_currentCompanion.m_companion.SaveInventory();
        if (updateEquipment)
        {
            m_currentCompanion.m_companion.UpdateEquipment();
        }
        m_currentCompanion.m_inUse = false;
        m_currentCompanion = null;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static class InventoryGUI_Hide_Patch
    {
        private static void Postfix()
        {
            CloseCompanionInventory(m_currentCompanion != null && m_currentCompanion.m_companion.m_inventoryChanged);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsContainerOpen))]
    private static class InventoryGUI_IsContainerOpen_Patch
    {
        private static void Postfix(ref bool __result)
        {
            if (m_currentCompanion != null) __result = true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTakeAll))]
    private static class InventoryGUI_OnTakeAll_Patch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            if (Player.m_localPlayer.IsTeleporting() || m_currentCompanion == null) return true;
            __instance.SetupDragItem(null, null, 1);
            Inventory inventory = m_currentCompanion.m_companion.GetInventory();
            Player.m_localPlayer.GetInventory().MoveAll(inventory);
            m_currentCompanion.m_companion.UpdateEquipment();
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnStackAll))]
    private static class InventoryGUI_OnStackAll_Patch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            if (Player.m_localPlayer.IsTeleporting() || m_currentCompanion == null) return true;
            __instance.SetupDragItem(null, null, 1);
            m_currentCompanion.m_companion.GetInventory().StackAll(Player.m_localPlayer.GetInventory());
            m_currentCompanion.m_companion.UpdateEquipment();
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainerWeight))]
    private static class InventoryGUI_UpdateContainerWeight_Patch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (m_currentCompanion == null) return;

            __instance.m_containerWeight.text = string.Format("{0}/{1}", (int)m_currentCompanion.m_companion.GetWeight(),
                (int)m_currentCompanion.m_companion.GetMaxCarryWeight());
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    private static class InventoryGUI_OnSelectedItem_Patch
    {
        private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos, InventoryGrid.Modifier mod)
        {
            if (m_currentCompanion == null) return true;
            if (mod is InventoryGrid.Modifier.Drop or InventoryGrid.Modifier.Select
                or InventoryGrid.Modifier.Split) return true;
            if (__instance.m_currentContainer != null) return true;
            if (item == null) return true;
            if (__instance.m_dragGo) return true;
            Player localPlayer = Player.m_localPlayer;
            if (localPlayer.IsTeleporting()) return true;
            if (item.m_shared.m_questItem) return true;
            localPlayer.RemoveEquipAction(item);
            localPlayer.UnequipItem(item);
            if (grid.GetInventory() == m_currentCompanion.m_companion.GetInventory())
            {
                localPlayer.GetInventory().MoveItemToThis(grid.GetInventory(), item);
            }
            else
            {
                m_currentCompanion.m_companion.GetInventory().MoveItemToThis(localPlayer.GetInventory(), item);
            }
            __instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            return false;
        }
    }
}