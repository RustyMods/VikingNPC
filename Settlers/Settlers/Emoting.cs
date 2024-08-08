using HarmonyLib;
using Settlers.Behaviors;

namespace Settlers.Settlers;

public static class Emoting
{
    [HarmonyPatch(typeof(Player), nameof(Player.StartEmote))]
    private static class Player_StartEmote_Patch
    {
        private static void Postfix(string emote)
        {
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                companion.m_companionTalk.QueueEmote($"emote_{emote}");
            }
        }
    }
}