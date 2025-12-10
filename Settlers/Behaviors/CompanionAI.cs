using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Settlers.Behaviors;

public class CompanionAI : MonsterAI
{
    public static readonly Dictionary<Chair, Companion> m_occupiedChairs = new();
    public static readonly Dictionary<Sadle, Companion> m_occupiedSaddles = new();
    public Companion m_companion = null!;
    public CompanionTalk m_companionTalk = null!;
    public TameableCompanion? m_tameableCompanion;
    private float m_searchAttachTimer;
    private const float m_searchAttachRange = 50f;
    public bool m_resting;
    public string m_action = "";
    public int m_seekAttempts;
    public string m_behavior = "";
    public static readonly List<string> m_acceptableBehaviors = new() { "mine", "lumber", "fish", "defend", "anything", "guard" };

    public override void Awake()
    {
        base.Awake();
        m_companion = GetComponent<Companion>();
        m_companionTalk = GetComponent<CompanionTalk>();
        m_tameableCompanion = GetComponent<TameableCompanion>();
        m_nview.Register<string>(nameof(RPC_SetBehavior), RPC_SetBehavior);
    }

    protected bool UpdateEatItem(float dt)
    {
        if (m_tameableCompanion == null) return false;
        if (m_consumeItems == null || m_consumeItems.Count == 0) return false;
        if (!m_tameableCompanion.IsHungry()) return false;
        m_consumeSearchTimer += dt;
        if (m_consumeSearchTimer > m_consumeSearchInterval)
        {
            m_consumeSearchTimer = 0.0f;
            m_consumeTarget = FindClosestConsumableItem(m_consumeSearchRange);
        }
        if (!m_consumeTarget) return false;

        if (!m_companion.CanEat(m_consumeTarget.m_itemData)) return false;

        if (!MoveTo(dt, m_consumeTarget.transform.position, m_consumeRange, false)) return true;
        LookAt(m_consumeTarget.transform.position);
        if (!IsLookingAt(m_consumeTarget.transform.position, 20f) || !m_consumeTarget.RemoveOne()) return true;
        
        m_onConsumedItem?.Invoke(m_consumeTarget);
        m_companion.m_consumeItemEffects.Create(transform.position, Quaternion.identity);

        m_consumeTarget = null;
        return true;
    }
    private static void UpdateHealthRegeneration(CompanionAI component, float dt)
    {
        component.m_regenTimer += dt;
        if (component.m_regenTimer <= 10.0) return;
        component.m_regenTimer = 0.0f;

        if (!component.m_companion.IsTamed()) return;
        if (component.m_tameableCompanion != null && component.m_tameableCompanion.IsHungry()) return;
        
        float worldTimeDelta = component.GetWorldTimeDelta();
        float amount = component.m_companion.GetMaxHealth() / 3600f * worldTimeDelta;
        foreach (Player.Food? food in component.m_companion.m_foods) amount += food.m_item.m_shared.m_foodRegen;

        float regenMultiplier = 1f;
        component.m_companion.m_seman.ModifyHealthRegen(ref regenMultiplier);
        component.m_companion.Heal(amount * regenMultiplier);
    }
    public void RPC_SetBehavior(long sender, string behavior)
    {
        if (m_behavior == behavior) return;
        if (!m_acceptableBehaviors.Contains(behavior)) return;
        Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"{m_companion.m_name} $msg_setbehavior {behavior}");
        m_behavior = behavior;
        if (behavior == "anything") m_behavior = "";
    }
    public string GetCurrentAction() => m_action;
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
        if (FindNearestSaddle() is not {} sadle)
        {
            m_seekAttempts += 1;
            return m_seekAttempts <= 3 && SeekSaddle();
        }

        sadle.Interact(m_companion, false, false);
        m_seekAttempts = 0;
        return true;
    }
    protected virtual bool UpdateAttach(float dt)
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
                if (m_companion is Settler settler) settler.Warp(player);
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
            if (collider.GetComponentInParent<Chair>() is not { } chair) continue;
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
            if (collider.GetComponentInParent<Sadle>() is not { } sadle) continue;
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
    public bool IsFemale() => m_nview.IsValid() && m_nview.GetZDO().GetInt(ZDOVars.s_modelIndex) == 1;
    
    [HarmonyPatch(typeof(BaseAI), nameof(UpdateRegeneration))]
    private static class BaseAI_UpdateRegeneration_Patch
    {
        private static bool Prefix(BaseAI __instance, float dt)
        {
            // only runs if owner
            if (__instance is not CompanionAI component) return true;
            UpdateHealthRegeneration(component, dt);
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
        private static bool Prefix(Chair __instance, Humanoid human)
        {
            if (human is not Companion companion) return true;
            companion.AttachStart(__instance.m_attachPoint, null, false, false, __instance.m_inShip, __instance.m_attachAnimation, __instance.m_detachOffset, null);
            m_occupiedChairs[__instance] = companion;
            companion.m_attachedChair = __instance;
            return false;
        }
    }

    [HarmonyPatch(typeof(Sadle), nameof(Sadle.Interact))]
    private static class Sadle_Interact_Patch
    {
        private static bool Prefix(Sadle __instance, Humanoid character)
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
            if (!__instance.m_nview.IsOwner() || __instance.m_commandable) return;
            string? follow = __instance.m_nview.GetZDO().GetString(ZDOVars.s_follow);
            if (follow.IsNullOrWhiteSpace()) return;
            __instance.m_monsterAI.SetFollowTarget(null);
            __instance.m_monsterAI.SetPatrolPoint();
            __instance.m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            
        }
    }

    [HarmonyPatch(typeof(Sadle), nameof(Sadle.RPC_RequestRespons))]
    private static class Sadle_RPC_RequestResponse_Patch
    {
        private static void Postfix(Sadle __instance, bool granted)
        {
            if (!granted || !m_occupiedSaddles.TryGetValue(__instance, out Companion companion)) return;
            
            companion.AttachStop();
        }
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(UpdateConsumeItem))]
    private static class MonsterAI_UpdateConsumeItem_Patch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            if (!__instance || __instance is not CompanionAI companionAI) return true;
            __result = companionAI.UpdateEatItem(dt);
            return false;
        }
    }
    
    private static readonly int Blocking = Animator.StringToHash("blocking");

    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    private static class Attack_Start_Patch
    {
        private static void Postfix(Attack __instance)
        {
            if (__instance.m_character == null) return;
            foreach (var companion in Companion.m_instances)
            {
                if (__instance.m_character is Companion viking && viking == companion) continue;
                if (companion is Sailor || companion.IsBlocking()) continue;
                if (Random.value > 0.5f) continue;
                float distance = Vector3.Distance(__instance.m_character.transform.position, companion.transform.position);
                companion.m_animator.SetBool(Blocking, distance < 5f);
                companion.m_blocking = distance < 5f;
                companion.m_blockTimer = 30f;
                if (!companion.m_nview.IsValid()) continue;
                companion.m_nview.GetZDO().Set(ZDOVars.s_isBlockingHash, distance < 5f);
            }
        }
    }
}