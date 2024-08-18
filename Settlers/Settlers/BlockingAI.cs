using BepInEx.Configuration;
using HarmonyLib;
using Settlers.Behaviors;
using UnityEngine;

namespace Settlers.Settlers;

public static class BlockingAI
{
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
                if (companion.IsSailor()) continue;
                if (companion.IsBlocking()) continue;
                float distance = Vector3.Distance(__instance.m_character.transform.position,
                    companion.transform.position);
                companion.m_animator.SetBool(Blocking, distance < 5f);
                companion.m_blocking = distance < 5f;
                companion.m_blockTimer = 30f;
                if (!companion.m_nview.IsValid()) continue;
                companion.m_nview.GetZDO().Set(ZDOVars.s_isBlockingHash, distance < 5f);
            }
        }
    }

}