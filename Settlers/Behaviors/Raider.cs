using Settlers.Settlers;
using UnityEngine;

namespace Settlers.Behaviors;

public class Raider : Companion
{
    public override void Awake()
    {
        base.Awake();
        m_onDeath += OnRaiderDeath;
    }

    public override void Start()
    {
        GetLoadOut();
        SetGearQuality(m_level);
        GiveDefaultItems();
        base.Start();
    }

    public override void GetLoadOut()
    {
        m_defaultItems = RaiderLoadOut.GetRaiderEquipment(Tier, false);
    }

    private void OnRaiderDeath()
    {
        ZoneSystem.instance.SetGlobalKey("defeated_vikingraider");
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
}