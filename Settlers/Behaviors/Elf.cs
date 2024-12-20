using System.Text;
using Settlers.Settlers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Settlers.Behaviors;

public class Elf : Companion
{
    public override void Awake()
    {
        base.Awake();
        GetFollowTargetName();
        m_onDeath += OnElfDeath;
    }

    public override void Start()
    {
        if (IsTamed()) return;
        GetLoadOut();
        SetGearQuality(m_level);
        GiveDefaultItems();
        base.Start();
    }

    protected override bool UpdateViking(float dt)
    {
        if (!base.UpdateViking(dt)) return false;
        if (!IsTamed())
        {
            UpdateAggravated();
        }
        else
        {
            if (UpdateAttach()) return true;
            AutoPickup(dt);
            UpdateFood(dt, false);
            UpdateStats(dt);
            UpdatePin(dt);
        }
        return true;
    }

    public override void GetLoadOut()
    {
        m_defaultItems = ElfLoadOut.GetElfEquipment(Tier);
    }

    private void OnElfDeath()
    {
        ZoneSystem.instance.SetGlobalKey("defeated_vikingelf");
        if (TryGetComponent(out CharacterDrop characterDrop))
        {
            characterDrop.m_drops.Clear();
            characterDrop.m_drops = RaiderDrops.GetRaiderDrops(Tier);
        }
        if (configs.DropChance?.Value == 0f) return;
        foreach (GameObject item in m_defaultItems)
        {
            if (item.name == "ElvenEars") continue;
            if (!item.TryGetComponent(out ItemDrop itemDrop) || Random.value > configs.DropChance?.Value) continue;
            ItemDrop.ItemData data = itemDrop.m_itemData.Clone();
            if (data.m_dropPrefab is null) continue;
            data.m_quality = m_level;
            ItemDrop drop = ItemDrop.DropItem(data, 1, transform.position, Quaternion.identity);
            m_dropEffects.Create(drop.transform.position, Quaternion.identity);
        }
    }

    public override string GetHoverText()
    {
        if (configs.Tameable?.Value is SettlersPlugin.Toggle.Off) return "";
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(m_name);
        if (IsTamed())
        {
            stringBuilder.AppendFormat(" ( {0} )", m_tameableCompanion.GetStatus());
            stringBuilder.AppendFormat("\n[<color=yellow><b>{0}</b></color>] {1}", 
                "$KEY_Use", 
                m_companionAI != null && m_companionAI.GetFollowTarget() == null ? "$hud_follow" : "$hud_stay");
            stringBuilder.Append("\n[<color=yellow><b>L.Shift + $KEY_Use</b></color>] $hud_interact");
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
}