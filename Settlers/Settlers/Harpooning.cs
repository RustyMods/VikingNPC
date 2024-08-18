using HarmonyLib;
using Settlers.Behaviors;
using UnityEngine;

namespace Settlers.Settlers;

public static class Harpooning
{
    private static float m_pullTo = 1f;
    private static float m_pullSpeed = 500f;

    [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.UpdateStatusEffect))]
    private static class SE_Harpooned_UpdateStatusEffect_Patch
    {
        private static bool Prefix(SE_Harpooned __instance, float dt)
        {
            if (!__instance.m_character) return true;
            if (!__instance.m_attacker) return true;
            if (__instance.m_attacker is not Companion) return true;
            BaseUpdateSE(__instance, dt);
            UpdateSE_Harpooned(__instance, dt);
            return false;
        }

        private static void UpdateSE_Harpooned(SE_Harpooned __instance, float dt)
        {
            if (!__instance.m_character.TryGetComponent(out Rigidbody rigidbody)) return;
            float v = Vector3.Distance(__instance.m_attacker.transform.position,
                __instance.m_character.transform.position);
            if (!__instance.m_character.IsAttached())
            {
                float num = Utils.Pull(rigidbody, __instance.m_attacker.transform.position, m_pullTo, SettlersPlugin._harpoonPullSpeed.Value, __instance.m_pullForce, __instance.m_smoothDistance, true, true, __instance.m_forcePower);
                __instance.m_drainStaminaTimer += dt;
                if (__instance.m_drainStaminaTimer > __instance.m_staminaDrainInterval && num > 0.0)
                {
                    __instance.m_drainStaminaTimer = 0.0f;
                    __instance.m_attacker.UseStamina(__instance.m_staminaDrain * num * __instance.m_character.GetMass());
                }
            }

            if (__instance.m_line)
            {
                __instance.m_line.SetSlack((1f - Utils.LerpStep(__instance.m_baseDistance / 2f, __instance.m_baseDistance, v)) * __instance.m_maxLineSlack);
            }

            if (v - __instance.m_baseDistance > __instance.m_breakDistance)
            {
                __instance.m_broken = true;
            }
        }

        private static void BaseUpdateSE(SE_Harpooned __instance, float dt)
        {
            __instance.m_time += dt;
            if (__instance.m_repeatInterval <= 0.0 || string.IsNullOrWhiteSpace(__instance.m_repeatMessage)) return;
            __instance.m_msgTimer += dt;
            if (__instance.m_msgTimer <= __instance.m_repeatInterval) return;
            __instance.m_msgTimer = 0.0f;
            __instance.m_character.Message(__instance.m_repeatMessageType, __instance.m_repeatMessage);
        }
    }
}