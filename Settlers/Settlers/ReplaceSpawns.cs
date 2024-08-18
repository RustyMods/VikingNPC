using HarmonyLib;
using Settlers.Behaviors;
using UnityEngine;

namespace Settlers.Settlers;

public static class ReplaceSpawns
{
    [HarmonyPatch(typeof(CreatureSpawner), nameof(CreatureSpawner.Spawn))]
    private static class CreatureSpawner_Spawn_Patch
    {
        private static bool Prefix(CreatureSpawner __instance, ref ZNetView __result)
        {
            if (SettlersPlugin._replaceSpawns.Value is SettlersPlugin.Toggle.Off) return true;
            if (__instance.m_creaturePrefab.name == "VikingSettler") return true;
            Vector3 position = __instance.transform.position;
            if (ZoneSystem.instance.FindFloor(position, out var height))
            {
                position.y = height;
            }

            Quaternion rotation = Quaternion.Euler(0.0f, Random.Range(0.0f, 360f), 0.0f);
            GameObject viking = Object.Instantiate(ZNetScene.instance.GetPrefab("VikingSettler"), position, rotation);
            ZNetView component1 = viking.GetComponent<ZNetView>();
            viking.GetComponent<ZSyncAnimation>()?.SetBool("wakeup", true);
            if (viking.TryGetComponent(out CompanionAI companionAI))
            {
                companionAI.SetPatrolPoint();
            }

            if (viking.TryGetComponent(out Companion companion))
            {
                companion.SetRaider(true);
                companion.SetLevel(Random.Range(0, 3));
            }
            __instance.m_nview.GetZDO().SetConnection(ZDOExtraData.ConnectionType.Spawned, component1.GetZDO().m_uid);
            __instance.m_nview.GetZDO().Set(ZDOVars.s_aliveTime, ZNet.instance.GetTime().Ticks);
            __instance.SpawnEffect(viking);
            __result = component1;
            return false;
        }
    }
}