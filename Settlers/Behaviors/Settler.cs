using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using Settlers.ExtraConfigs;
using Settlers.Settlers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Settlers.Behaviors;

public class Settler : Companion
{
    private bool m_teleporting;

    public override void Awake()
    {
        base.Awake();
        m_nview.Register<Vector3, Vector3>(nameof(RPC_Warp), RPC_Warp);
        m_nview.Register<bool>(nameof(RPC_UpdateEquipment), RPC_UpdateEquipment);
        GetFollowTargetName();
        m_onDeath += CreateTombStone;
    }

    public override void Start()
    {
        LoadInventory();
        if (m_inventory.GetAllItems().Count == 0)
        {
            m_defaultItems = SettlerGear.GetSettlerGear();
            GiveDefaultItems();
            SetGearQuality(m_level);
        }
        base.Start();
    }

    public override void SetupVikingHealth() => SetMaxHealth(configs.BaseHealth.Value * m_level * GetSkillModifier());
    private float GetSkillModifier() => m_nview.GetZDO().GetFloat("SkillModifier".GetStableHashCode(), 1f);
    public void SetSkillModifier(float modifier)
    {
        if (modifier <= 0f) modifier = 1f;
        m_nview.ClaimOwnership();
        m_nview.GetZDO().Set("SkillModifier".GetStableHashCode(), modifier);
    }

    protected override bool UpdateViking(float dt)
    {
        if (!base.UpdateViking(dt)) return false;
        if (!IsTamed())
        {
            UpdateAggravated();
            AutoPickup(dt);
        }
        else
        {
            if (UpdateAttach()) return true;
            AutoPickup(dt);
            UpdateWarp(dt);
            UpdateFood(dt, false);
            UpdateStats(dt);
            UpdatePin(dt);
        }
        return true;
    }

    private void LoadInventory()
    {
        string? data = m_nview.GetZDO().GetString(ZDOVars.s_items);
        if (data.IsNullOrWhiteSpace()) return;
        ZPackage pkg = new ZPackage(data);
        m_loading = true;
        m_inventory.Load(pkg);
        m_loading = false;
        UpdateEquipment(false);
    }
    
    private bool ShouldCreateTombStone() => m_nview.GetZDO().GetBool("InventoryChanged".GetStableHashCode());

    private void CreateTombStone()
    {
        if (GetInventory().NrOfItems() == 0 || m_tombstone == null || !ShouldCreateTombStone()) return;
        GameObject tomb = Instantiate(m_tombstone, GetCenterPoint(), transform.rotation);
        if (!tomb.TryGetComponent(out TombStone component)) return;
        component.Setup(m_name, 0L);
        component.m_container.GetInventory().MoveAll(GetInventory());
    }
    
    private void CheckEquipment(ItemDrop.ItemData? item, List<ItemDrop.ItemData> inventory)
    {
        if (item == null) return;
        if (inventory.Any(x => x.m_shared.m_name == item.m_shared.m_name)) return;
        UnequipItem(item);
    }
    
    public void UpdateEquipment(bool toggle = true)
    {
        if (m_nview.IsOwner()) RPC_UpdateEquipment(0L, toggle);
        else m_nview.InvokeRPC(nameof(RPC_UpdateEquipment), toggle);
    }
    
    private void RPC_UpdateEquipment(long sender, bool toggle = true)
    {
        m_inventoryChanged = false;
        if (m_companionAI is SettlerAI settlerAI)
        {
            settlerAI.m_fishTarget = null;
            settlerAI.m_rockTarget = null;
            settlerAI.m_treeTarget = null;
        }

        List<ItemDrop.ItemData>? items = m_inventory.GetAllItems();
        CheckEquipment(m_helmetItem, items);
        CheckEquipment(m_chestItem, items);
        CheckEquipment(m_legItem, items);
        CheckEquipment(m_shoulderItem, items);
        CheckEquipment(m_rightItem, items);
        CheckEquipment(m_leftItem, items);
        CheckEquipment(m_utilityItem, items);
        CheckEquipment(m_hiddenLeftItem, items);
        CheckEquipment(m_hiddenRightItem, items);
        foreach (ItemDrop.ItemData? item in items.OrderBy(x => x.m_shared.m_armor).ToList())
        {
            if (!item.IsEquipable()) continue;
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet:
                    if (m_helmetItem == null ||
                        (m_helmetItem != null && item.m_shared.m_armor > m_helmetItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Chest:
                    if (m_chestItem == null ||
                        (m_chestItem != null && item.m_shared.m_armor > m_chestItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Legs:
                    if (m_legItem == null || (m_legItem != null && item.m_shared.m_armor > m_legItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Shoulder:
                    if (m_shoulderItem == null ||
                        (m_shoulderItem != null && item.m_shared.m_armor > m_shoulderItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                default:
                    if (toggle) ToggleEquipped(item);
                    else EquipItem(item);
                    break;
            }
        }
    }

    public override bool TeleportTo(Vector3 pos, Quaternion rot, bool distantTeleport)
    {
        if (!m_nview.IsOwner())
        {
            // Tells everyone in the area to run this command, only the owner of the companion will successfully teleport
            m_nview.InvokeRPC(nameof(RPC_TeleportTo), pos, rot, distantTeleport);
            return false;
        }
        Teleport(pos, rot, distantTeleport);
        return true;
    }
    
    public void Teleport(Vector3 pos, Quaternion rot, bool distantTeleport)
    {
        m_teleporting = true;
        Vector3 location;
        if (distantTeleport)
        {
            location = pos + new Vector3(0f, 2f, 0f);
        }
        else
        {
            Vector3 random = Random.insideUnitSphere * 10f;
            location = pos + new Vector3(random.x, 0f, random.z);
            location.y = ZoneSystem.instance.GetSolidHeight(location) + 0.5f;
        }

        var transform1 = transform;
        transform1.position = location;
        transform1.rotation = rot;
        m_body.velocity = Vector3.zero;
        m_teleporting = false;
    }
    public void UpdateWarp(float dt)
    {
        if (IsEncumbered()) return;
        m_checkDistanceTimer += dt;
        if (m_checkDistanceTimer < 5f) return;
        m_checkDistanceTimer = 0.0f;
        if (m_companionAI.GetFollowTarget() == null) return;
        if (!m_companionAI.GetFollowTarget().TryGetComponent(out Player component)) return;
        if (Vector3.Distance(transform.position, component.transform.position) < m_playerMaxDistance) return;
        Warp(component);
    }
    public void Warp(Player player)
    {
        if (m_companionAI == null) return;
        if (player.IsTeleporting()) return;
        Vector3 location = player.GetLookDir() * 5f + player.transform.position;
        if (!m_nview.IsOwner())
        {
            m_nview.InvokeRPC(nameof(RPC_Warp), player.transform.position, location);
        }
        else
        {
            ZoneSystem.instance.GetSolidHeight(location, out float height, 1000);
            if (height >= 0.0 && Mathf.Abs(height - location.y) <= 2f && Vector3.Distance(location, player.transform.position) >= 2f)
            {
                location.y = height;
            }
        
            if (!IsOwner()) m_nview.ClaimOwnership();
            m_companionAI.StopMoving();
            Transform transform1 = transform;
            transform1.position = location;
            transform1.rotation = Quaternion.identity;
            m_body.velocity = Vector3.zero;
            m_warpEffect.Create(transform1.position, Quaternion.identity);
        }
    }
    public void RPC_Warp(long sender, Vector3 playerPos, Vector3 pos)
    {
        if (m_teleporting) return;
        ZoneSystem.instance.GetSolidHeight(pos, out float height, 1000);
        if (height >= 0.0 && Mathf.Abs(height - pos.y) <= 2f && Vector3.Distance(pos, playerPos) >= 2f)
        {
            pos.y = height;
        }

        if (!IsOwner()) m_nview.ClaimOwnership();
        if (m_companionAI != null) m_companionAI.StopMoving();
        Transform transform1 = transform;
        transform1.position = pos;
        transform1.rotation = Quaternion.identity;
        m_body.velocity = Vector3.zero;
        m_warpEffect.Create(transform1.position, Quaternion.identity);
    }

    public override bool HaveEitr(float amount = 0) => amount < m_eitr;
    
    public override void UseEitr(float eitr)
    {
        if (eitr == 0) return;
        m_eitr -= eitr;
        if (m_eitr < 0) m_eitr = 0;
    }

    public override void GetTotalFoodValue(out float hp, out float stamina, out float eitr)
    {
        hp = configs.BaseHealth.Value * m_level * GetSkillModifier();
        stamina = 50f;
        eitr = 0.0f;
        foreach (Player.Food? food in m_foods)
        {
            hp += food.m_health;
            stamina += food.m_stamina;
            eitr += food.m_eitr;
        }
    }

    public override string GetHoverText()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(m_name);
        if (IsTamed())
        {
            stringBuilder.AppendFormat(" ( {0} )", m_tameableCompanion.GetStatus());
            stringBuilder.AppendFormat("\n[<color=yellow><b>{0}</b></color>] {1}", 
                "$KEY_Use", 
                m_companionAI != null && m_companionAI.GetFollowTarget() == null ? "$hud_follow" : "$hud_stay");
            stringBuilder.Append("\n[<color=yellow><b>L.Shift + $KEY_Use</b></color>] $hud_interact");
            if (configs.Locked?.Value is SettlersPlugin.Toggle.On)
            {
                string ownerName = GetOwnerName();
                if (!string.IsNullOrWhiteSpace(ownerName)) stringBuilder.AppendFormat("\n$hud_owner: {0}", ownerName);
            }
            stringBuilder.Append("\n[<color=yellow><b>L.Alt + $KEY_Use</b></color>] $hud_rename");
            stringBuilder.Append("\n[<color=yellow>1-8</color>] $hud_give");
            stringBuilder.AppendFormat("\n$se_health: {0}/{1}", (int)GetHealth(), (int)GetMaxHealth());
            stringBuilder.AppendFormat("\n$item_armor: {0}", (int)GetBodyArmor());
            if (GetMaxEitr() > 0) stringBuilder.AppendFormat("\n$se_eitr: <color=#E6E6FA>{0}</color>/<color=#B19CD9>{1}</color>", (int)m_eitr, (int)GetMaxEitr());
        }
        else
        {
            int tameness = m_tameableCompanion.GetTameness();

            stringBuilder.AppendFormat("\n({0}, {1})", 
                tameness <= 0 ? "$hud_wild" : "$hud_tameness",
                m_tameableCompanion.GetStatus() + (tameness <= 0 ? "" : "%"));

            stringBuilder.Append("\n[<color=yellow>1-8</color>] $hud_give");
        }

        return Localization.instance.Localize(stringBuilder.ToString());
    }

    public override void AddPin()
    {
        if (configs.Track?.Value is SettlersPlugin.Toggle.Off) return;
        Minimap.PinData pin = new Minimap.PinData()
        {
            m_type = Minimap.PinType.Player,
            m_name = GetHoverName(),
            m_pos = transform.position,
            m_icon = SettlersPlugin.m_settlerPin,
            m_save = false,
            m_checked = false,
            m_ownerID = Player.m_localPlayer.GetPlayerID(),
            m_author = "SettlerPlugin"
        };
        pin.m_NamePinData = new Minimap.PinNameData(pin);
        Minimap.instance.m_pins.Add(pin);
        if (pin.m_type < (Minimap.PinType)Minimap.instance.m_visibleIconTypes.Length && !Minimap.instance.m_visibleIconTypes[(int)pin.m_type])
        {
            Minimap.instance.ToggleIconFilter(pin.m_type);
        }

        Minimap.instance.m_pinUpdateRequired = true;
        m_pin = pin;
    }
    
    public override float GetMaxCarryWeight()
    {
        float max = configs.BaseCarryWeight?.Value ?? 300f;
        m_seman.ModifyMaxCarryWeight(max, ref max);
        return max;
    }
}