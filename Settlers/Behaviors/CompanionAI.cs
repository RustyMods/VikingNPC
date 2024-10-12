using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Settlers.Settlers;
using UnityEngine;

namespace Settlers.Behaviors;

public class CompanionAI : MonsterAI
{
    public static readonly Dictionary<Chair, Companion> m_occupiedChairs = new();
    public static readonly Dictionary<Sadle, Companion> m_occupiedSaddles = new();
    private Companion m_companion = null!;
    private CompanionTalk m_companionTalk = null!;
    private CompanionContainer m_container = null!;
    private ItemDrop.ItemData? m_axe;
    private ItemDrop.ItemData? m_pickaxe;
    private readonly float m_searchRange = 10f;
    private float m_searchTargetTimer;
    public GameObject? m_treeTarget;
    public GameObject? m_rockTarget;
    public GameObject? m_fishTarget;
    private float m_searchAttachTimer;
    private const float m_searchAttachRange = 50f;
    public bool m_resting;
    public string m_action = "";
    public float m_lastFishTime;
    public Piece? m_repairPiece;
    public float m_repairTimer;
    public int m_seekAttempts;
    public string m_behavior = "";
    public static readonly List<string> m_acceptableBehaviors = new() { "mine", "lumber", "fish", "defend", "anything" };

    public override void Awake()
    {
        base.Awake();
        m_companion = GetComponent<Companion>();
        m_companionTalk = GetComponent<CompanionTalk>();
        m_container = GetComponent<CompanionContainer>();
        m_consumeItems = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Consumable, "")
            .Where(item => !item.m_itemData.m_shared.m_consumeStatusEffect).ToList();;
        m_nview.Register<string>(nameof(RPC_SetBehavior), RPC_SetBehavior);
    }

    private bool UpdateSailorAI(float dt)
    {
        if (m_character is not Humanoid character) return false;
        UpdateTarget(character, dt, out bool canHearTarget, out bool canSeeTarget);
        ItemDrop.ItemData itemData = SelectBestAttack(character, dt);
        if (itemData == null) return true;
        bool flag = (double)Time.time - itemData.m_lastAttackTime > itemData.m_shared.m_aiAttackInterval &&
                    (double)m_character.GetTimeSinceLastAttack() >= m_minAttackInterval && !IsTakingOff();
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
    public override bool UpdateAI(float dt)
    {
        if (m_companion.IsSailor())
        {
            if (!m_companion.m_attached) return base.UpdateAI(dt);
            if (UpdateSailorAI(dt)) return true;
        }
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
        if (SettlersPlugin._SettlerRequireFood.Value is SettlersPlugin.Toggle.On && m_companion.IsHungry())
        {
            ResetActions();
            return UpdateEatItem(m_companion, dt) || base.UpdateAI(dt);
        }

        if (m_companion.InAttack()) return false;
        UpdateActionTargets(dt);
        if (UpdateLumber(dt)) return true;
        if (UpdateMining(dt)) return true;
        if (UpdateFishing(dt)) return true;
        // if (UpdateRepair(dt)) return true;
        if (SettlersPlugin._SettlerRequireFood.Value is SettlersPlugin.Toggle.Off && m_companion.IsHungry())
        {
            ResetActions();
            return UpdateEatItem(m_companion, dt) || base.UpdateAI(dt);
        }
        ResetActions();
        return base.UpdateAI(dt);
    }
    // private bool UpdateRepair(float dt)
    // {
    //     ItemDrop.ItemData? hammer = GetHammer();
    //     if (hammer == null) return false;
    //     m_repairTimer += dt;
    //     if (m_repairTimer < 5f) return false;
    //     m_repairTimer = 0.0f;
    //     m_repairPiece = FindRepairPiece();
    //     if (m_repairPiece == null) return false;
    //
    //     if (m_repairPiece.TryGetComponent(out WearNTear component))
    //     {
    //         if (!MoveTo(dt, m_repairPiece.transform.position, 10f, false)) return true;
    //         LookAt(m_repairPiece.transform.position);
    //         if (!IsLookingAt(m_repairPiece.transform.position, 50f)) return true;
    //         if (m_companion.GetCurrentWeapon() != hammer) m_companion.EquipItem(hammer);
    //         m_animator.SetTrigger(hammer.m_shared.m_attack.m_attackAnimation);
    //         component.Repair();
    //         m_repairPiece.m_placeEffect.Create(m_repairPiece.transform.position, Quaternion.identity);
    //         m_repairPiece = null;
    //         return true;
    //     }
    //     return false;
    // }

    private bool UpdateEatItem(Companion companion, float dt)
    {
        if (m_consumeItems == null || m_consumeItems.Count == 0) return false;
        if (!companion.IsHungry()) return false;
        m_consumeSearchTimer += dt;
        if (m_consumeSearchTimer > m_consumeSearchInterval)
        {
            m_consumeSearchTimer = 0.0f;
            m_consumeTarget = FindClosestConsumableItem(m_consumeSearchRange);
        }

        if (!m_consumeTarget) return false;
        if (!MoveTo(dt, m_consumeTarget.transform.position, m_consumeRange, false)) return true;
        LookAt(m_consumeTarget.transform.position);
        if (!IsLookingAt(m_consumeTarget.transform.position, 20f) || !m_consumeTarget.RemoveOne()) return true;
        
        m_onConsumedItem?.Invoke(m_consumeTarget);
        companion.m_consumeItemEffects.Create(transform.position, Quaternion.identity);

        m_consumeTarget = null;
        return true;
    }

    public void RPC_SetBehavior(long sender, string behavior)
    {
        if (m_behavior == behavior) return;
        if (!m_acceptableBehaviors.Contains(behavior)) return;
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"{m_companion.m_name} $msg_setbehavior {behavior}");
        m_behavior = behavior;
        if (behavior == "anything") m_behavior = "";
    }

    // private ItemDrop.ItemData? GetHammer() => m_companion.GetInventory().GetAllItems().FirstOrDefault(item => item.m_shared.m_name == "$item_hammer");
    // private Piece? FindRepairPiece()
    // {
    //     if (GetHammer() == null)
    //     {
    //         m_repairPiece = null;
    //         return null;
    //     }
    //     if (m_repairPiece != null) return m_repairPiece;
    //
    //     return TargetFinder.FindNearestPiece(transform.position, m_searchRange);
    //     
    //     Piece? result = null;
    //     float num1 = m_searchRange;
    //     foreach (var collider in Physics.OverlapSphere(transform.position, m_searchRange, LayerMask.GetMask("piece")))
    //     {
    //         Piece piece = collider.GetComponentInParent<Piece>();
    //         if (piece == null) continue;
    //         if (!piece.TryGetComponent(out WearNTear component)) continue;
    //         if (component.GetHealthPercentage() > 0.5f) continue;
    //         float distance = Vector3.Distance(transform.position, piece.transform.position);
    //         if (result == null || num1 < distance)
    //         {
    //             result = piece;
    //             num1 = distance;
    //         }
    //     }
    //
    //     return result;
    // }

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

    private bool UpdateFishing(float dt)
    {
        if (m_fishTarget == null) return false;
        if (Time.time - m_lastFishTime < 10f) return false;
        if (SettlersPlugin._SettlerRequireFood.Value is SettlersPlugin.Toggle.On && m_companion.IsHungry()) return false;
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
        return TargetFinder.FindNearestFish(transform.position, searchRange);
    }

    private bool UpdateLumber(float dt)
    {
        if (m_treeTarget == null) return false;
        if (SettlersPlugin._SettlerRequireFood.Value is SettlersPlugin.Toggle.On && m_companion.IsHungry()) return false;
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

    private MineRock5.HitArea GetRandomHitArea(MineRock5 component)
    {
        MineRock5.HitArea target = component.m_hitAreas[Random.Range(0, component.m_hitAreas.Count)];
        return target.m_health > 0.0 ? target : GetRandomHitArea(component);
    }

    private bool UpdateMining(float dt)
    {
        if (m_rockTarget == null) return false;
        if (SettlersPlugin._SettlerRequireFood.Value is SettlersPlugin.Toggle.On && m_companion.IsHungry()) return false;
        m_action = "mining";
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

    public bool SeekChair()
    {
        Chair? chair = FindNearestChair();
        if (chair == null)
        {
            m_seekAttempts += 1;
            return m_seekAttempts <= 3 && SeekChair();
        }

        chair.Interact(m_companion, false, false);
        m_seekAttempts = 0;
        return true;
    }

    public bool BreakSit()
    {
        if (!m_companion.IsAttached()) return false;
        m_companion.AttachStop();
        return true;
    }

    public bool SeekSaddle()
    {
        Sadle? sadle = FindNearestSaddle();
        if (sadle == null)
        {
            m_seekAttempts += 1;
            return m_seekAttempts <= 3 && SeekSaddle();
        }

        sadle.Interact(m_companion, false, false);
        m_seekAttempts = 0;
        return true;
    }
    
    private bool UpdateAttach(float dt)
    {
        if (!m_companion.IsTamed()) return false;
        m_searchAttachTimer += dt;
        if (m_searchAttachTimer < 5f) return false;
        m_searchAttachTimer = 0.0f;
        
        if (m_companion.m_attached)
        {
            var follow = GetFollowTarget();
            if (follow != null && follow.TryGetComponent(out Player player))
            {
                if (Vector3.Distance(follow.transform.position, transform.position) < 15f) return true;
                m_companion.AttachStop();
                m_companion.Warp(player);
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
    
    private Sadle? FindNearestSaddle()
    {
        Sadle? closestSaddle = null;
        float num1 = m_searchAttachRange;
        foreach (Collider collider in Physics.OverlapSphere(transform.position, m_searchAttachRange, LayerMask.GetMask("piece_nonsolid")))
        {
            Sadle sadle = collider.GetComponentInParent<Sadle>();
            if (!sadle) continue;
            if (m_occupiedSaddles.ContainsKey(sadle)) continue;
            if (!sadle.m_nview.GetZDO().GetBool(ZDOVars.s_haveSaddleHash)) continue;
            if (sadle.HaveValidUser()) continue;
            var distance = Vector3.Distance(sadle.transform.position, transform.position);
            if (distance < num1 || closestSaddle == null)
            {
                closestSaddle = sadle;
                num1 = distance;
            }
        }
        return closestSaddle;
    }

    private GameObject? FindClosestTree(float maxRange)
    {
        if (SettlersPlugin._SettlersCanLumber.Value is SettlersPlugin.Toggle.Off) return null;
        if (!HasAxe()) return null;
        if (m_axe == null) return null;

        return TargetFinder.FindNearestTreeTarget(transform.position, m_axe.m_shared.m_toolTier, maxRange);
    }
    
    private GameObject? FindClosestRock(float maxRange)
    {
        if (SettlersPlugin._SettlersCanMine.Value is SettlersPlugin.Toggle.Off) return null;
        if (!HasPickaxe()) return null;
        if (m_pickaxe == null) return null;

        return TargetFinder.FindNearestRock(transform.position, m_pickaxe.m_shared.m_toolTier, maxRange);
    }

    public bool IsFemale() => m_nview.IsValid() && m_nview.GetZDO().GetInt(ZDOVars.s_modelIndex) == 1;
    
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

    [HarmonyPatch(typeof(Sadle), nameof(Sadle.Interact))]
    private static class Sadle_Interact_Patch
    {
        private static bool Prefix(Sadle __instance, Humanoid character, bool repeat, bool alt)
        {
            if (character is not Companion companion) return true;
            if (!__instance.m_character.IsTamed()) return false;
            if (!__instance.m_nview.GetZDO().GetBool(ZDOVars.s_haveSaddleHash)) return false;
            companion.AttachStart(__instance.m_attachPoint, __instance.m_character.gameObject, false, false, false, __instance.m_attachAnimation, __instance.m_detachOffset, null);
            m_occupiedSaddles[__instance] = companion;
            companion.m_attachedSadle = __instance;
            var follow = companion.m_companionAI.GetFollowTarget();
            if (follow != null)
            {
                if (follow.TryGetComponent(out Player player))
                {
                    __instance.m_tambable.Command(player);
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Awake))]
    private static class Tamable_Awake_Patch
    {
        private static void Postfix(Tameable __instance)
        {
            if (__instance.m_commandable) return;
            if (!__instance.m_nview.IsOwner()) return;
            var follow = __instance.m_nview.GetZDO().GetString(ZDOVars.s_follow);
            if (follow.IsNullOrWhiteSpace()) return;
            __instance.m_monsterAI.SetFollowTarget(null);
            __instance.m_monsterAI.SetPatrolPoint();
            __instance.m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            
        }
    }

    [HarmonyPatch(typeof(BaseAI), nameof(IsEnemy), typeof(Character),typeof(Character))]
    private static class BaseAI_IsEnemy_Patch
    {
        private static void Postfix(Character a, Character b, ref bool __result)
        {
            if (a is not Companion companionA || b is not Companion companionB) return;

            if (companionA.IsRaider() != companionB.IsRaider()) __result = true;

            if (SettlersPlugin._pvp.Value is SettlersPlugin.Toggle.On)
            {
                if (companionA.IsTamed() && companionB.IsTamed())
                {
                    var owner1 = companionA.GetOwnerName();
                    var owner2 = companionB.GetOwnerName();
                    if (owner1 != owner2) __result = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Sadle), nameof(Sadle.RPC_RequestRespons))]
    private static class Sadle_RPC_RequestResponse_Patch
    {
        private static void Postfix(Sadle __instance, bool granted)
        {
            if (!granted) return;
    
            if (!m_occupiedSaddles.TryGetValue(__instance, out Companion companion)) return;
            
            companion.AttachStop();
        }
    }
}