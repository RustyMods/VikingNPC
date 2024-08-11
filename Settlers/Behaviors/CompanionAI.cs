using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Settlers.Behaviors;

public class CompanionAI : MonsterAI
{
    public static readonly Dictionary<Chair, Companion> m_occupiedChairs = new();
    private Companion m_companion = null!;
    private CompanionTalk m_companionTalk = null!;
    private ItemDrop.ItemData? m_axe;
    private ItemDrop.ItemData? m_pickaxe;
    private readonly float m_searchRange = 10f;
    private float m_searchTargetTimer;
    public GameObject? m_treeTarget;
    public GameObject? m_rockTarget;
    public GameObject? m_fishTarget;
    private float m_searchAttachTimer;
    private readonly float m_searchAttachRange = 50f;
    public bool m_resting;
    public string m_action = "";
    public float m_lastFishTime;
    public Piece? m_repairPiece;
    public float m_repairTimer;
    public double m_timeSinceLastSeedConversion;
    public override void Awake()
    {
        base.Awake();
        m_companion = GetComponent<Companion>();
        m_companionTalk = GetComponent<CompanionTalk>();
        m_consumeItems = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Consumable, "");
    }
    public override bool UpdateAI(float dt)
    {
        if (m_companion.IsSailor())
        {
            if (!m_companion.m_attached) return base.UpdateAI(dt);
            Humanoid? character = m_character as Humanoid;
            if (character == null) return true;
            UpdateTarget(character, dt, out bool canHearTarget, out bool canSeeTarget);
            ItemDrop.ItemData itemData = SelectBestAttack(character, dt);
            if (itemData == null) return true;
            bool flag = (double) Time.time - itemData.m_lastAttackTime >itemData.m_shared.m_aiAttackInterval && (double) m_character.GetTimeSinceLastAttack() >= m_minAttackInterval && !IsTakingOff();
            if (m_targetCreature != null)
            {
                SetAlerted(true);
                var targetCreaturePos = m_targetCreature.transform.position;
                m_lastKnownTargetPos = targetCreaturePos;
                LookAt(m_targetCreature.GetTopPoint());
                var distance = Vector3.Distance(targetCreaturePos, transform.position);
                if (distance > itemData.m_shared.m_aiAttackRange) return true;
                if (flag)
                {
                    DoAttack(m_targetCreature, false);
                }
            }
            return true;
        }
        if (!m_companion.IsTamed()) return base.UpdateAI(dt);
        if (m_companion.m_inUse)
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
        if (GetFollowTarget() != null)
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
        if (m_companion.IsHungry())
        {
            ResetActions();
            if (UpdateConsumeItem(m_character as Humanoid, dt)) return true;
            return base.UpdateAI(dt);
        }

        UpdateTarget(dt);
        if (UpdateLumber(dt)) return true;
        if (UpdateMining(dt)) return true;
        if (UpdateFishing(dt)) return true;
        if (UpdateRepair(dt)) return true;
        ResetActions();
        return base.UpdateAI(dt);
    }

    private void UpdateFarming()
    {
        if (!HasCultivator()) return;
        if (m_companion.GetCurrentWeapon() == null || m_companion.GetCurrentWeapon().m_shared.m_name != "$item_cultivator") return;
        if (ZNet.instance.GetTimeSeconds() - m_timeSinceLastSeedConversion < 100f) return;
        ConvertSeeds();
        m_timeSinceLastSeedConversion = ZNet.instance.GetTimeSeconds();
    }

    private void ConvertSeeds()
    {
        ItemDrop.ItemData? itemToConvert = null;
        ItemDrop.ItemData? itemResult = null;
        foreach (ItemDrop.ItemData? item in m_companion.GetInventory().GetAllItems())
        {
            if (item.m_shared.m_itemType is not ItemDrop.ItemData.ItemType.Material) continue;
            ItemDrop.ItemData? conversion = GetSeedConversion(item);
            if (conversion == null) continue;
            itemToConvert = item;
            itemResult = conversion;
            break;
        }

        m_companion.GetInventory().RemoveOneItem(itemToConvert);
        m_companion.GetInventory().AddItem(itemResult);
    }

    private ItemDrop.ItemData? GetSeedConversion(ItemDrop.ItemData seed)
    {
        var itemName = seed.m_shared.m_name switch
        {
            "$item_carrotseeds" => "Carrot",
            "$item_turnipseeds" => "Turnip",
            "$item_onionseeds" => "Onion",
            "$item_barley" => "Barley",
            "$item_flax" => "Flax",
            _ => ""
        };
        if (itemName.IsNullOrWhiteSpace()) return null;
        var prefab = ObjectDB.instance.GetItemPrefab(itemName);
        if (!prefab) return null;
        return prefab.TryGetComponent(out ItemDrop component) ? component.m_itemData : null;
    }
    private bool HasCultivator() => m_companion.GetInventory().GetAllItems().Any(item => item.m_shared.m_name == "$item_cultivator");
    private bool UpdateRepair(float dt)
    {
        ItemDrop.ItemData? hammer = GetHammer();
        if (hammer == null) return false;
        m_repairTimer += dt;
        if (m_repairTimer < 5f) return false;
        m_repairTimer = 0.0f;
        m_repairPiece = FindRepairPiece();
        if (m_repairPiece == null) return false;

        if (m_repairPiece.TryGetComponent(out WearNTear component))
        {
            if (!MoveTo(dt, m_repairPiece.transform.position, 10f, false)) return true;
            LookAt(m_repairPiece.transform.position);
            if (!IsLookingAt(m_repairPiece.transform.position, 50f)) return true;
            if (m_companion.GetCurrentWeapon() != hammer) m_companion.EquipItem(hammer);
            m_animator.SetTrigger(hammer.m_shared.m_attack.m_attackAnimation);
            component.Repair();
            m_repairPiece.m_placeEffect.Create(m_repairPiece.transform.position, Quaternion.identity);
            m_repairPiece = null;
        }
        return false;
    }

    private ItemDrop.ItemData? GetHammer() => m_companion.GetInventory().GetAllItems().FirstOrDefault(item => item.m_shared.m_name == "$item_hammer");
    private Piece? FindRepairPiece()
    {
        if (GetHammer() == null)
        {
            m_repairPiece = null;
            return null;
        }
        if (m_repairPiece != null) return m_repairPiece;
        Piece? result = null;
        float num1 = m_searchRange;
        foreach (var collider in Physics.OverlapSphere(transform.position, m_searchRange, LayerMask.GetMask("piece")))
        {
            Piece piece = collider.GetComponentInParent<Piece>();
            if (piece == null) continue;
            if (!piece.TryGetComponent(out WearNTear component)) continue;
            if (component.GetHealthPercentage() > 0.5f) continue;
            float distance = Vector3.Distance(transform.position, piece.transform.position);
            if (result == null || num1 < distance)
            {
                result = piece;
                num1 = distance;
            }
        }

        return result;
    }

    private void ResetActions()
    {
        m_action = "";
        m_fishTarget = null;
        m_treeTarget = null;
        m_rockTarget = null;
    }

    private bool HasFishingRodAndBait()
    {
        if (SettlersPlugin._SettlerCanFish.Value is SettlersPlugin.Toggle.Off) return false;
        if (!m_companion.GetInventory().HaveItem("$item_fishingrod")) return false;

        string baitName = m_companion.GetCurrentBiome() switch
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

    public string GetCurrentAction() => m_action;
    
    private void UpdateTarget(float dt)
    {
        m_searchTargetTimer += dt;
        if (m_searchTargetTimer < Random.Range(0f, 2f)) return;
        m_searchTargetTimer = 0.0f;
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

        if (m_treeTarget == null && m_rockTarget == null && m_fishTarget == null)
        {
            m_action = "";
        }
    }

    private bool UpdateFishing(float dt)
    {
        if (m_fishTarget == null) return false;
        if (Time.time - m_lastFishTime < 10f) return false;
        if (m_companion.IsHungry()) return false;
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

    private GameObject? GetNearestFish(float searchRange)
    {
        if (!HasFishingRodAndBait()) return null;
        GameObject? result = null;
        float num1 = 200f;
        foreach (Collider collider in Physics.OverlapSphere(transform.position, searchRange, LayerMask.GetMask("character")))
        {
            Fish component = collider.GetComponentInParent<Fish>();
            if (component == null) continue;
            if (component.gameObject.tag != "spawned") continue;
            var distance = Vector3.Distance(transform.position, component.gameObject.transform.position);
            if (num1 > distance)
            {
                result = component.gameObject;
                num1 = distance;
            }
        }

        return result;
    }

    private bool UpdateLumber(float dt)
    {
        if (m_treeTarget == null) return false;
        if (m_companion.IsHungry()) return false;
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

    private bool UpdateMining(float dt)
    {
        if (m_rockTarget == null) return false;
        if (m_companion.IsHungry()) return false;
        m_action = "mining";
        if (!MoveTo(dt, m_rockTarget.transform.position, 1.5f, false)) return true;
        LookAt(m_rockTarget.transform.position);
        if (!IsLookingAt(m_rockTarget.transform.position, 1f)) return true;
        if (!DoPickaxe())
        {
            RandomMovement(dt, m_rockTarget.transform.position);
            return true;
        }
        return true;
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
        m_companion.m_rightItem.m_shared.m_attack.m_hitTerrain = false;
        if (!m_companion.StartAttack(null, false)) return false;
        m_timeSinceAttacking = 0.0f;
        m_rockTarget = null;
        m_treeTarget = null;
        return true;
    }

    private bool HasAxe()
    {
        ItemDrop.ItemData? bestAxe = null;
        foreach (ItemDrop.ItemData? item in m_companion.GetInventory().GetAllItems())
        {
            if (!item.IsWeapon() || !m_companion.m_baseAI.CanUseAttack(item)) continue;
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
    
    private bool UpdateAttach(float dt)
    {
        if (!m_companion.IsTamed()) return false;
        m_searchAttachTimer += dt;
        if (m_searchAttachTimer < 5f) return false;
        m_searchAttachTimer = 0.0f;
        var follow = GetFollowTarget();
        if (!m_companion.m_attached && follow != null)
        {
            Chair? chair = FindNearestChair();
            if (chair == null) return false;
            if (!MoveTo(dt, chair.transform.position, 10f, true)) return true;
            LookAt(chair.transform.position);
            chair.Interact(m_companion, false, false);
            return true;
        }
        if (m_companion.m_attached)
        {
            if (follow != null && follow.TryGetComponent(out Player player))
            {
                if (Vector3.Distance(follow.transform.position, transform.position) < 25f) return true;
                m_companion.AttachStop();
                m_companion.Warp(player);
            }
            else
            {
                m_companion.AttachStop();
            }
            return true;
        }

        return false;
    }

    private Chair? FindNearestChair()
    {
        Chair? closestChair = null;

        float num1 = m_searchAttachRange;
        foreach (Collider? collider in Physics.OverlapSphere(transform.position, m_searchAttachRange, LayerMask.GetMask("piece", "piece_nonsolid")))
        {
            Chair chair = collider.GetComponentInParent<Chair>();
            if (!chair) continue;
            if (m_occupiedChairs.ContainsKey(chair)) continue;
            var distance = Vector3.Distance(chair.transform.position, transform.position);
            if (distance < num1 || closestChair == null)
            {
                closestChair = chair;
                num1 = distance;
            }
        }

        return closestChair;
    }

    private GameObject? FindClosestTree(float maxRange)
    {
        if (SettlersPlugin._SettlersCanLumber.Value is SettlersPlugin.Toggle.Off) return null;
        if (!HasAxe()) return null;
        if (m_axe == null) return null;
        GameObject? result = null;
        float num1 = maxRange;
        Collider[] colliders = Physics.OverlapSphere(transform.position, maxRange, LayerMask.GetMask("Default"));
        foreach (var collider in colliders)
        {
            Destructible destructible = collider.GetComponentInParent<Destructible>();
            if (destructible)
            {
                if (destructible.m_destructibleType is not DestructibleType.Tree) continue;
                if (destructible.m_minToolTier > m_axe.m_shared.m_toolTier) continue;
                float num2 = Vector3.Distance(transform.position, destructible.gameObject.transform.position);
                if (num2 > num1) continue;
                result = destructible.gameObject;
                num1 = num2;
                continue;
            }

            TreeLog treeLog = collider.GetComponentInParent<TreeLog>();
            if (treeLog)
            {
                if (treeLog.m_minToolTier > m_axe.m_shared.m_toolTier) continue;
                float num2 = Vector3.Distance(transform.position, treeLog.gameObject.transform.position);
                if (num2 > num1) continue;
                result = treeLog.gameObject;
                num1 = num2;
                continue;
            }

            TreeBase treeBase = collider.GetComponentInParent<TreeBase>();
            if (treeBase)
            {
                if (treeBase.m_minToolTier > m_axe.m_shared.m_toolTier) continue;
                float num2 = Vector3.Distance(transform.position, treeBase.gameObject.transform.position);
                if (num2 > num1) continue;
                result = treeBase.gameObject;
                num1 = num2;
            }
        }
        return result;
    }
    
    private GameObject? FindClosestRock(float maxRange)
    {
        if (SettlersPlugin._SettlersCanMine.Value is SettlersPlugin.Toggle.Off) return null;
        if (!HasPickaxe()) return null;
        if (m_pickaxe == null) return null;
        GameObject? result = null;
        float num1 = maxRange;
        Collider[] colliders = Physics.OverlapSphere(transform.position, maxRange, LayerMask.GetMask("Default", "static_solid", "Default_small"));
        foreach (var collider in colliders)
        {
            MineRock mineRock = collider.GetComponentInParent<MineRock>();
            if (mineRock)
            {
                if (mineRock.m_minToolTier > m_pickaxe.m_shared.m_toolTier) continue;
                if (!mineRock.m_dropItems.m_drops.Exists(x =>
                        x.m_item.name.EndsWith("Ore") || x.m_item.name.EndsWith("Scrap"))) continue;
                float num2 = Vector3.Distance(transform.position, mineRock.gameObject.transform.position);
                if (num2 > num1) continue;
                result = mineRock.gameObject;
                num1 = num2;
                continue;
            }

            MineRock5 mineRock5 = collider.GetComponentInParent<MineRock5>();
            if (mineRock5)
            {
                if (mineRock5.m_minToolTier > m_pickaxe.m_shared.m_toolTier) continue;
                if (!mineRock5.m_dropItems.m_drops.Exists(x =>
                        x.m_item.name.EndsWith("Ore") || x.m_item.name.EndsWith("Scrap"))) continue;
                float num2 = Vector3.Distance(transform.position, mineRock5.gameObject.transform.position);
                if (num2 > num1) continue;
                result = mineRock5.gameObject;
                num1 = num2;
                continue;
            }

            Destructible destructible = collider.GetComponentInParent<Destructible>();
            if (destructible)
            {
                if (destructible.m_destructibleType is DestructibleType.Tree) continue;
                if (destructible.m_minToolTier > m_pickaxe.m_shared.m_toolTier) continue;
                float num2 = Vector3.Distance(transform.position, destructible.gameObject.transform.position);
                if (destructible.m_spawnWhenDestroyed)
                {
                    MineRock5? spawnRock = destructible.m_spawnWhenDestroyed.GetComponent<MineRock5>();
                    if (spawnRock)
                    {
                        if (!spawnRock.m_dropItems.m_drops.Exists(x =>
                                x.m_item.name.EndsWith("Ore") || x.m_item.name.EndsWith("Scrap"))) continue;
                        if (num2 > num1) continue;
                        result = destructible.gameObject;
                        num1 = num2;
                        continue;
                    }
                }

                DropOnDestroyed dropOnDestroyed = destructible.GetComponent<DropOnDestroyed>();
                if (!dropOnDestroyed) continue;
                if (!dropOnDestroyed.m_dropWhenDestroyed.m_drops.Exists(x =>
                        x.m_item.name.EndsWith("Ore") || x.m_item.name.EndsWith("Scrap"))) continue;
                if (num2 > num1) continue;
                result = destructible.gameObject;
                num1 = num2;
            }
        }
        return result;
    }
    
    [HarmonyPatch(typeof(BaseAI), nameof(UpdateRegeneration))]
    private static class BaseAI_UpdateRegeneration_Patch
    {
        private static bool Prefix(BaseAI __instance, float dt)
        {
            if (__instance is not CompanionAI component) return true;
            component.m_regenTimer += dt;
            if (component.m_regenTimer <= 10.0) return false;
            component.m_regenTimer = 0.0f;
            if (!component.m_companion.IsTamed() && component.m_companion.IsHungry()) return false;
            float worldTimeDelta = component.GetWorldTimeDelta();
            float amount = component.m_companion.GetMaxHealth() / 3600f * worldTimeDelta;
            foreach (Player.Food? food in component.m_companion.m_foods)
            {
                amount += food.m_item.m_shared.m_foodRegen;
            }
            component.m_companion.Heal(amount, true);
            return false;
        }
    }

    [HarmonyPatch(typeof(SE_Puke), nameof(SE_Puke.UpdateStatusEffect))]
    private static class SE_Puke_Update_Patch
    {
        private static bool Prefix(SE_Puke __instance, float dt)
        {
            if (__instance.m_character.IsPlayer()) return true;
            UpdateStatus(__instance, dt);
            UpdateStats(__instance, dt);
            return false;
        }
        
        private static void UpdateStatus(SE_Puke __instance, float dt)
        {
            __instance.m_removeTimer += dt;
            if (__instance.m_removeTimer <= __instance.m_removeInterval) return;
            __instance.m_removeTimer = 0.0f;
        }

        private static void UpdateStats(SE_Puke __instance, float dt)
        {
            __instance.m_tickTimer += dt;
            if (!(__instance.m_tickTimer >= __instance.m_tickInterval)) return;
            __instance.m_tickTimer = 0.0f;
            __instance.m_character.Damage(new HitData()
            {
                m_damage =
                {
                    m_damage = 1f
                },
                m_point = __instance.m_character.GetTopPoint(),
                m_hitType = HitData.HitType.PlayerHit
            });
        }
    }

    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.GetOwner))]
    private static class FishingFloat_GetOwner_Patch
    {
        private static void Postfix(FishingFloat __instance, ref Character __result)
        {
            if (__result != null) return;
            long num = __instance.m_nview.GetZDO().GetLong(ZDOVars.s_rodOwner);
            foreach (Companion companion in Companion.m_instances)
            {
                if (companion.m_nview.GetZDO().m_uid.UserID != num) continue;
                __result = companion;
                return;
            }
        }
    }
    
    [HarmonyPatch(typeof(Chair), nameof(Chair.Interact))]
    private static class Chair_Interact_Patch
    {
        private static bool Prefix(Chair __instance, Humanoid human, bool hold, bool alt)
        {
            if (human is not Companion companion) return true;
            if (hold) return false;
            
            companion.AttachStart(__instance.m_attachPoint, null, false, false, __instance.m_inShip,
                __instance.m_attachAnimation, __instance.m_detachOffset, null);
            m_occupiedChairs[__instance] = companion;
            companion.m_attachedChair = __instance;
            return false;
        }
    }

    [HarmonyPatch(typeof(BaseAI), nameof(IsEnemy), typeof(Character),typeof(Character))]
    private static class BaseAI_IsEnemy_Patch
    {
        private static void Postfix(Character a, Character b, ref bool __result)
        {
            if (a is not Companion companionA || b is not Companion companionB) return;
            if (companionA.IsRaider() && !companionB.IsRaider())
            {
                __result = true;
            }

            if (companionB.IsRaider() && !companionA.IsRaider())
            {
                __result = true;
            }

            if (companionA.IsElf() && companionB.IsRaider())
            {
                __result = true;
            }

            if (companionB.IsElf() && companionA.IsRaider())
            {
                __result = true;
            }

            if (companionA.IsSailor() && !companionB.IsSailor())
            {
                __result = true;
            }

            if (companionB.IsSailor() && !companionA.IsSailor())
            {
                __result = true;
            }
        }
    }
}