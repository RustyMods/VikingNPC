using BepInEx;
using Settlers.Settlers;
using UnityEngine;

namespace Settlers.Behaviors;

public class SettlerAI : CompanionAI
{
    private SettlerContainer? m_container;
    private ItemDrop.ItemData? m_axe;
    private ItemDrop.ItemData? m_pickaxe;
    private readonly float m_searchRange = 10f;
    private float m_searchTargetTimer;
    public GameObject? m_treeTarget;
    public GameObject? m_rockTarget;
    public GameObject? m_fishTarget;
    public float m_lastFishTime;


    public override void Awake()
    {
        base.Awake();
        m_container = GetComponent<SettlerContainer>();
    }

    public override bool UpdateAI(float dt)
    {
        if (!m_companion.IsTamed()) return base.UpdateAI(dt);
        if (m_container != null && m_container.m_inUse)
        {
            StopMoving();
            return true;
        }
        
        if (m_companion.IsQueueActive())
        {
            StopMoving();
            return true;
        }
        if (UpdateAttach(dt)) return true;
        if (m_behavior.IsNullOrWhiteSpace() && GetFollowTarget() != null)
        {
            ResetActions();
            return base.UpdateAI(dt);
        }

        if (IsAlerted() || m_companion.IsSwimming())
        {
            ResetActions();
            return base.UpdateAI(dt);
        }

        if (m_companionTalk.InPlayerBase())
        {
            ResetActions();
            return base.UpdateAI(dt);
        }

        if (m_companion.InAttack())
        {
            return base.UpdateAI(dt);
        }

        if (m_companion.IsInventoryFull())
        {
            ResetActions();
            return base.UpdateAI(dt);
        }
        if (m_tameableCompanion != null 
            && m_companion is Settler { configs.RequireFood.Value: SettlersPlugin.Toggle.On } && m_tameableCompanion.IsHungry())
        {
            ResetActions();
            return UpdateEatItem(dt) || base.UpdateAI(dt);
        }

        if (m_companion.InAttack()) return false;
        UpdateActionTargets(dt);
        if (UpdateLumber(dt)) return true;
        if (UpdateMining(dt)) return true;
        if (UpdateFishing(dt)) return true;
        if (m_tameableCompanion != null 
            && m_companion is Settler { configs.RequireFood.Value: SettlersPlugin.Toggle.Off } && m_tameableCompanion.IsHungry())
        {
            ResetActions();
            return UpdateEatItem(dt) || base.UpdateAI(dt);
        }
        ResetActions();
        return base.UpdateAI(dt);
    }

    private void ResetActions()
    {
        m_action = "";
        m_fishTarget = null;
        m_treeTarget = null;
        m_rockTarget = null;
    }
    
    private void UpdateActionTargets(float dt)
    {
        m_searchTargetTimer += dt;
        if (m_searchTargetTimer < Random.Range(0f, 2f)) return;
        m_searchTargetTimer = 0.0f;

        if (m_behavior.IsNullOrWhiteSpace())
        {
            if (m_treeTarget == null) m_treeTarget = FindClosestTree(m_searchRange);
            if (m_rockTarget == null) m_rockTarget = FindClosestRock(m_searchRange);
            if (m_fishTarget == null) m_fishTarget = GetNearestFish(100f);
            if (m_treeTarget != null && m_rockTarget != null)
            {
                Vector3 position = transform.position;
                float num1 = Vector3.Distance(position, m_treeTarget.transform.position);
                float num2 = Vector3.Distance(position, m_rockTarget.transform.position);
                if (num1 > num2)
                {
                    m_rockTarget = null;
                    m_fishTarget = null;
                }
                else
                {
                    m_treeTarget = null;
                    m_fishTarget = null;
                }
            }
        }
        else
        {
            switch (m_behavior)
            {
                case "mine":
                    if (m_rockTarget == null) m_rockTarget = FindClosestRock(m_searchRange);
                    break;
                case "lumber":
                    if (m_treeTarget == null) m_treeTarget = FindClosestTree(m_searchRange);
                    break;
                case "fish":
                    if (m_fishTarget == null) m_fishTarget = GetNearestFish(100f);
                    break;
            }
        }
        
        if (m_treeTarget == null && m_rockTarget == null && m_fishTarget == null)
        {
            m_action = "";
        }
    }
    private GameObject? FindClosestTree(float maxRange)
    {
        if (m_companion is Settler { configs.CanLumber.Value: SettlersPlugin.Toggle.Off }) return null;
        if (!HasAxe()) return null;
        return m_axe == null ? null : TargetFinder.FindNearestTreeTarget(transform.position, m_axe.m_shared.m_toolTier, maxRange);
    }
    
    private GameObject? FindClosestRock(float maxRange)
    {
        if (m_companion is Settler { configs.CanMine.Value: SettlersPlugin.Toggle.Off }) return null;        
        if (!HasPickaxe()) return null;
        return m_pickaxe == null ? null : TargetFinder.FindNearestRock(transform.position, m_pickaxe.m_shared.m_toolTier, maxRange);
    }
    private bool HasPickaxe()
    {
        ItemDrop.ItemData? bestPickaxe = null;
        foreach (ItemDrop.ItemData? item in m_companion.GetInventory().GetAllItems())
        {
            if (!item.IsWeapon() || !m_companion.m_baseAI.CanUseAttack(item)) continue;
            if (item.m_shared.m_damages.m_pickaxe > 0f)
            {
                if (bestPickaxe == null)
                {
                    bestPickaxe = item;
                }
                else
                {
                    if (item.m_shared.m_damages.m_pickaxe > bestPickaxe.m_shared.m_damages.m_pickaxe)
                    {
                        bestPickaxe = item;
                    }
                }
            }
        }

        m_pickaxe = bestPickaxe;
        return bestPickaxe != null;
    }
    private bool HasAxe()
    {
        ItemDrop.ItemData? bestAxe = null;
        foreach (ItemDrop.ItemData? item in m_companion.GetInventory().GetAllItems())
        {
            if (!item.IsWeapon()) continue;

            if (item.m_shared.m_damages.m_chop > 0f)
            {
                if (bestAxe == null)
                {
                    bestAxe = item;
                }
                else
                {
                    if (item.m_shared.m_damages.m_chop > bestAxe.m_shared.m_damages.m_chop)
                    {
                        bestAxe = item;
                    }
                }
            }
        }

        m_axe = bestAxe;

        return bestAxe != null;
    }
    private bool DoChop()
    {
        m_companion.EquipItem(m_axe);
        if (m_companion.m_rightItem == null) return false;
        if (!m_companion.StartAttack(null, false)) return false;
        m_timeSinceAttacking = 0.0f;
        m_treeTarget = null;
        m_rockTarget = null;
        return true;
    }

    private bool DoPickaxe()
    {
        m_companion.EquipItem(m_pickaxe);
        if (m_companion.m_rightItem == null) return false;
        m_companion.m_rightItem.m_shared.m_attack.m_attackRayWidth = 1f;
        m_companion.m_rightItem.m_shared.m_attack.m_hitTerrain = false;
        if (!m_companion.StartAttack(null, false)) return false;
        m_timeSinceAttacking = 0.0f;
        m_rockTarget = null;
        m_treeTarget = null;
        return true;
    }
    private MineRock5.HitArea GetRandomHitArea(MineRock5 component)
    {
        MineRock5.HitArea target = component.m_hitAreas[Random.Range(0, component.m_hitAreas.Count)];
        return target.m_health > 0.0 ? target : GetRandomHitArea(component);
    }
    private bool UpdateMining(float dt)
    {
        if (m_rockTarget == null) return false;
        if (m_tameableCompanion != null && m_companion is Settler { configs.RequireFood.Value: SettlersPlugin.Toggle.On } &&
            m_tameableCompanion.IsHungry()) return false;        m_action = "mining";
        if (m_rockTarget.TryGetComponent(out MineRock5 component))
        {
            var target = GetRandomHitArea(component);
            var center = target.m_collider.bounds.center;
            if (!MoveTo(dt, center, 1.5f, false)) return true;
            LookAt(center);
            if (!DoPickaxe())
            {
                RandomMovement(dt, center);
                return true;
            }
        }
        else
        {
            if (!MoveTo(dt, m_rockTarget.transform.position, 1.5f, false)) return true;
            LookAt(m_rockTarget.transform.position);
            if (!IsLookingAt(m_rockTarget.transform.position, 1f)) return true;
            if (!DoPickaxe())
            {
                RandomMovement(dt, m_rockTarget.transform.position);
                return true;
            }
        }
        return true;
    }
    private bool HasFishingRodAndBait()
    {
        if (m_companion is Settler { configs.CanFish.Value: SettlersPlugin.Toggle.Off }) return false;
        if (!m_companion.GetInventory().HaveItem("$item_fishingrod")) return false;

        string baitName = Heightmap.FindBiome(transform.position) switch
        {
            Heightmap.Biome.Meadows => "$item_fishingbait",
            Heightmap.Biome.BlackForest => "$item_fishingbait_forest",
            Heightmap.Biome.Swamp => "$item_fishingbait_swamp",
            Heightmap.Biome.Mountain => "$item_fishingbait_cave",
            Heightmap.Biome.Plains => "$item_fishingbait_plains",
            Heightmap.Biome.Ocean => "$item_fishingbait_ocean",
            Heightmap.Biome.Mistlands => "$item_fishingbait_mistlands",
            Heightmap.Biome.AshLands => "$item_fishingbait_ashlands",
            Heightmap.Biome.DeepNorth => "$item_fishingbait_deepnorth",
            _ => ""
        };

        if (baitName.IsNullOrWhiteSpace()) return false;
        return m_companion.GetInventory().HaveItem(baitName);
    }
    private GameObject? GetNearestFish(float searchRange)
    {
        if (!HasFishingRodAndBait()) return null;
        return TargetFinder.FindNearestFish(transform.position, searchRange);
    }
    private bool UpdateLumber(float dt)
    {
        if (m_treeTarget == null) return false;
        if (m_tameableCompanion != null && m_companion is Settler { configs.RequireFood.Value: SettlersPlugin.Toggle.On } &&
            m_tameableCompanion.IsHungry()) return false;
        m_action = "lumber";
        if (!MoveTo(dt, m_treeTarget.transform.position, 1.5f, false)) return true;
        StopMoving();
        LookAt(m_treeTarget.transform.position);
        if (!IsLookingAt(m_treeTarget.transform.position, 1f))
        {
            return true;
        }
        if (!DoChop())
        {
            RandomMovement(dt, m_treeTarget.transform.position);
            return true;
        }
        return true;
    }
    
    private bool UpdateFishing(float dt)
    {
        if (m_fishTarget == null) return false;
        if (Time.time - m_lastFishTime < 10f) return false;
        if (m_tameableCompanion != null && m_companion is Settler { configs.RequireFood.Value: SettlersPlugin.Toggle.On } &&
            m_tameableCompanion.IsHungry()) return false;
        m_action = "fishing";
        if (!MoveTo(dt, m_fishTarget.transform.position, 30f, false)) return true;
        LookAt(m_fishTarget.transform.position);
        if (!IsLookingAt(m_fishTarget.transform.position, 1f)) return true;
        StopMoving();
        if (m_companion.GetCurrentWeapon().m_shared.m_name != "$item_fishingrod")
            m_companion.EquipItem(m_companion.GetInventory().GetItem("$item_fishingrod"));
        m_companion.StartAttack(null, false);
        m_lastFishTime = Time.time;
        m_companion.PickupPrefab(m_fishTarget, 1);
        m_fishTarget = null;
        m_timeSinceAttacking = 0.0f;
        return true;
    }
}