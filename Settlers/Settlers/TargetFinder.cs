using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Settlers.Settlers;

public static class TargetFinder
{
    private static List<Destructible> m_destructible = new();
    private static List<TreeLog> m_treeLogs = new();
    private static List<TreeBase> m_treeBases = new();

    private static List<MineRock> m_mineRocks = new();
    private static List<MineRock5> m_mineRock5s = new();

    private static readonly List<Fish> m_fishes = new();

    // private static readonly List<Piece> m_pieces = new();
    
    public static GameObject? FindNearestTreeTarget(Vector3 position, int toolTier, float maxRange)
    {
        var treeBase = FindNearestTreeBase(position, toolTier, maxRange);
        var treeLog = FindNearestTreeLog(position, toolTier, maxRange);
        var destructible = FindNearestDestructible(position, true, toolTier, maxRange);
        List<GameObject> objects = new();
        if (treeBase != null) objects.Add(treeBase.gameObject);
        if (treeLog != null) objects.Add(treeLog.gameObject);
        if (destructible != null) objects.Add(destructible.gameObject);
        GameObject? output = null;
        var num = maxRange;
        foreach (var obj in objects)
        {
            var distance = Vector3.Distance(position, obj.transform.position);
            if (output == null || distance < num)
            {
                output = obj;
                num = distance;
            }
        }

        return output;
    }

    public static GameObject? FindNearestRock(Vector3 position, int toolTier, float maxRange)
    {
        var mineRock = FindNearestMineRock(position, toolTier, maxRange);
        var mineRock5 = FindNearestMineRock5(position, toolTier, maxRange);
        var destructible = FindNearestDestructible(position, false, toolTier, maxRange);

        List<GameObject> objects = new();
        if (mineRock != null) objects.Add(mineRock.gameObject);
        if (mineRock5 != null) objects.Add(mineRock5.gameObject);
        if (destructible != null) objects.Add(destructible.gameObject);
        GameObject? output = null;
        var num = maxRange;
        foreach (var obj in objects)
        {
            var distance = Vector3.Distance(position, obj.transform.position);
            if (output == null || distance < num)
            {
                output = obj;
                num = distance;
            }
        }

        return output;
    }

    public static GameObject? FindNearestFish(Vector3 position, float maxRange)
    {
        Fish? fish = null;
        var num = maxRange;
        foreach (var instance in m_fishes)
        {
            var distance = Vector3.Distance(position, instance.transform.position);
            if (fish == null || distance < num)
            {
                fish = instance;
                num = distance;
            }
        }
        return fish == null ? null : fish.gameObject;
    }

    // public static Piece? FindNearestPiece(Vector3 position, float maxRange)
    // {
    //     Piece? piece = null;
    //     var num = maxRange;
    //     foreach (var instance in m_pieces)
    //     {
    //         if (!instance.TryGetComponent(out WearNTear component)) continue;
    //         if (component.GetHealthPercentage() > 0.5f) continue;
    //         var distance = Vector3.Distance(position, instance.transform.position);
    //         if (piece == null || distance < num)
    //         {
    //             piece = instance;
    //             num = distance;
    //         }
    //     }
    //
    //     return piece;
    // }

    private static TreeBase? FindNearestTreeBase(Vector3 position, int toolTier, float maxRange)
    {
        List<TreeBase> toStay = new();
        var num = maxRange;
        TreeBase? treeBase = null;
        foreach (var instance in m_treeBases)
        {
            if (instance == null) continue;
            if (instance.m_nview == null) continue;
            if (!instance.m_nview.IsValid()) continue;
            toStay.Add(instance);
            if (instance.m_minToolTier > toolTier) continue;
            var distance = Vector3.Distance(position, instance.transform.position);
            if (treeBase == null || distance < num)
            {
                treeBase = instance;
                num = distance;
            }
        }

        m_treeBases = toStay;
        return treeBase;
    }

    private static TreeLog? FindNearestTreeLog(Vector3 position, int toolTier, float maxRange)
    {
        List<TreeLog> toStay = new();
        var num = maxRange;
        TreeLog? treeLog = null;
        foreach (var instance in m_treeLogs)
        {
            if (instance == null) continue;
            if (instance.m_nview == null) continue;
            if (!instance.m_nview.IsValid()) continue;
            toStay.Add(instance);
            if (instance.m_minToolTier > toolTier) continue;
            var distance = Vector3.Distance(position, instance.transform.position);
            if (treeLog == null || distance < num)
            {
                treeLog = instance;
                num = distance;
            }
        }

        m_treeLogs = toStay;

        return treeLog;
    }

    private static Destructible? FindNearestDestructible(Vector3 position, bool tree, int toolTier, float maxRange)
    {
        List<Destructible> toStay = new();
        var num = maxRange;
        Destructible? destructible = null;
        foreach (var instance in m_destructible)
        {
            if (instance == null) continue;
            if (instance.m_nview == null) continue;
            if (!instance.m_nview.IsValid()) continue;
            toStay.Add(instance);
            switch (tree)
            {
                case true when instance.m_destructibleType is not DestructibleType.Tree:
                    continue;
                case false:
                {
                    if (!instance.TryGetComponent(out DropOnDestroyed component)) continue;
                    if (!component.m_dropWhenDestroyed.m_drops.Exists(x =>
                            x.m_item.name.EndsWith("Ore") || x.m_item.name.EndsWith("Scrap"))) continue;
                    break;
                }
            }

            if (instance.m_minToolTier > toolTier) continue;
            var distance = Vector3.Distance(position, instance.transform.position);
            if (destructible == null || distance < num)
            {
                destructible = instance;
                num = distance;
            }
        }

        m_destructible = toStay;
        
        return destructible;
    }

    private static MineRock? FindNearestMineRock(Vector3 position, int toolTier, float maxRange)
    {
        List<MineRock> toStay = new();
        var num = maxRange;
        MineRock? mineRock = null;
        foreach (var instance in m_mineRocks)
        {
            if (instance == null) continue;
            if (instance.m_nview == null) continue;
            if (!instance.m_nview.IsValid()) continue;
            toStay.Add(instance);
            if (instance.m_minToolTier > toolTier) continue;
            var distance = Vector3.Distance(position, instance.transform.position);
            if (mineRock == null || distance < num)
            {
                mineRock = instance;
                num = distance;
            }
        }

        m_mineRocks = toStay;
        return mineRock;
    }

    private static MineRock5? FindNearestMineRock5(Vector3 position, int toolTier, float maxRange)
    {
        List<MineRock5> toStay = new();
        var num = maxRange;
        MineRock5? mineRock = null;
        foreach (var instance in m_mineRock5s)
        {
            if (instance == null) continue;
            if (instance.m_nview == null) continue;
            if (!instance.m_nview.IsValid()) continue;
            toStay.Add(instance);
            if (instance.m_minToolTier > toolTier) continue;
            var distance = Vector3.Distance(position, instance.transform.position);
            if (mineRock == null || distance < num)
            {
                mineRock = instance;
                num = distance;
            }
        }

        m_mineRock5s = toStay;
        return mineRock;
    }
    
    [HarmonyPatch(typeof(Destructible), nameof(Destructible.Awake))]
    private static class Destructible_Awake_Patch
    {
        private static void Postfix(Destructible __instance)
        {
            if (!__instance) return;
            if (m_destructible.Contains(__instance)) return;
            m_destructible.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Awake))]
    private static class TreeLog_Awake_Patch
    {
        private static void Postfix(TreeLog __instance)
        {
            if (!__instance) return;
            if (m_treeLogs.Contains(__instance)) return;
            m_treeLogs.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Awake))]
    private static class TreeBase_Awake_Patch
    {
        private static void Postfix(TreeBase __instance)
        {
            if (!__instance) return;
            if (m_treeBases.Contains(__instance)) return;
            m_treeBases.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(MineRock), nameof(MineRock.Start))]
    private static class MineRock_Start_Patch
    {
        private static void Postfix(MineRock __instance)
        {
            if (!__instance) return;
            if (m_mineRocks.Contains(__instance)) return;
            m_mineRocks.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Awake))]
    private static class MineRock5_Awake_Patch
    {
        private static void Postfix(MineRock5 __instance)
        {
            if (!__instance) return;
            if (m_mineRock5s.Contains(__instance)) return;
            m_mineRock5s.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(Fish), nameof(Fish.OnEnable))]
    private static class Fish_OnEnable_Patch
    {
        private static void Postfix(Fish __instance)
        {
            if (!__instance) return;
            if (m_fishes.Contains(__instance)) return;
            m_fishes.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(Fish), nameof(Fish.OnDisable))]
    private static class Fish_OnDisable_Patch
    {
        private static void Postfix(Fish __instance)
        {
            if (!__instance) return;
            m_fishes.Remove(__instance);
        }
    }

    // [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
    // private static class Piece_Awake_Patch
    // {
    //     private static void Postfix(Piece __instance)
    //     {
    //         if (!__instance) return;
    //         if (m_pieces.Contains(__instance)) return;
    //         m_pieces.Add(__instance);
    //     }
    // }
    //
    // [HarmonyPatch(typeof(Piece), nameof(Piece.OnDestroy))]
    // private static class Piece_OnDestroy_Patch
    // {
    //     private static void Postfix(Piece __instance)
    //     {
    //         if (!__instance) return;
    //         m_pieces.Remove(__instance);
    //     }
    // }
}