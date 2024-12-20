﻿
using Settlers.Settlers;

namespace Settlers.Behaviors;

public class Sailor : Companion
{
    public override void Start()
    {
        GetLoadOut();
        SetGearQuality(m_level);
        GiveDefaultItems();
        base.Start();
    }

    public override void GetLoadOut()
    {
        m_defaultItems = RaiderLoadOut.GetRaiderEquipment(Tier, true);
    }
    protected override bool UpdateViking(float dt)
    {
        if (!base.UpdateViking(dt)) return false;
        UpdateAttach();
        return true;
    }
}