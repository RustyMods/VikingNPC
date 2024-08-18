using System.Collections.Generic;
using HarmonyLib;
using Settlers.Behaviors;

namespace Settlers.Settlers;

public static class PukePatches
{
    [HarmonyPatch(typeof(SE_Puke), nameof(SE_Puke.Setup))]
    private static class SE_Puke_Setup_Patch
    {
        private static void Postfix(SE_Puke __instance)
        {
            if (__instance.m_character is not Companion companion) return;
            companion.m_companionTalk.QueueSay(GetPukeTalk(), "emote_despair", GetPukeEffects());
        }
    }

    private static List<string> GetPukeTalk()
    {
        return new List<string>()
        {
            "$npc_settler_puke_1","$npc_settler_puke_2","$npc_settler_puke_3","$npc_settler_puke_4","$npc_settler_puke_5",
            "$npc_settler_puke_6","$npc_settler_puke_7","$npc_settler_puke_8","$npc_settler_puke_9","$npc_settler_puke_10",
        };
    }

    private static EffectList GetPukeEffects()
    {
        return new EffectList
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("fx_Puke"),
                    m_enabled = true,
                    m_variant = -1,
                    m_attach = true,
                    m_inheritParentRotation = true,
                    m_childTransform = "Jaw"
                },
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("sfx_Puke_male"),
                    m_enabled = true,
                    m_variant = 0,
                    m_attach = true
                },
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("sfx_Puke_female"),
                    m_enabled = true,
                    m_variant = 1,
                    m_attach = true
                }
            }
        };
    }

    [HarmonyPatch(typeof(SE_Puke), nameof(SE_Puke.UpdateStatusEffect))]
    private static class SE_Puke_Update_Patch
    {
        private static bool Prefix(SE_Puke __instance, float dt)
        {
            if (__instance.m_character is not Companion) return true;
            __instance.m_time += dt;
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
}