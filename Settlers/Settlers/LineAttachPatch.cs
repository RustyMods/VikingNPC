using HarmonyLib;
using UnityEngine;

namespace Settlers.Settlers;

public static class LineAttachPatch
{
    [HarmonyPatch(typeof(LineAttach), nameof(LineAttach.CustomLateUpdate))]
    private static class LineAttach_CustomLateUpdate_Patch
    {
        private static bool Prefix(LineAttach __instance)
        {
            if (__instance.m_attachments == null) return false;
            for (int index = 0; index < __instance.m_attachments.Count; ++index)
            {
                try
                {
                    Transform attachment = __instance.m_attachments[index];
                    if (attachment)
                        __instance.m_lineRenderer.SetPosition(index,
                            __instance.transform.InverseTransformPoint(attachment.position));
                }
                catch
                {
                    //
                }
            }

            return false;
        }
    }
}