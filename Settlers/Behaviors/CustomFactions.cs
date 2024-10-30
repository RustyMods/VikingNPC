using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Settlers.Behaviors;

public static class CustomFactions
{
    private static readonly Dictionary<Character.Faction, CustomFaction> m_customs = new();
    
    public class CustomFaction
    {
        public readonly string m_name;
        public int m_hash = 0;
        public readonly bool m_friendly;
        public readonly Character.Faction m_faction;
        public CustomFaction(string name, bool friendly)
        {
            m_name = name;
            m_hash = name.GetStableHashCode();
            m_friendly = friendly;
            m_faction = GetFaction(m_name);
            m_customs[m_faction] = this;
        }
    }
    private static readonly Dictionary<string, Character.Faction> m_factions = new();

    private static Character.Faction GetFaction(string name)
    {
        if (Enum.TryParse(name, true, out Character.Faction faction)) return faction;
        if (m_factions.TryGetValue(name, out faction)) return faction;
        Dictionary<Character.Faction, string> factions = GetFactionMap();
        foreach (var kvp in factions)
        {
            if (kvp.Value == name)
            {
                faction = kvp.Key;
                m_factions[name] = faction;
                return faction;
            }
        }

        faction = (Character.Faction)name.GetStableHashCode();
        m_factions[name] = faction;
        return faction;
    }

    private static Dictionary<Character.Faction, string> GetFactionMap()
    {
        Array values = Enum.GetValues(typeof(Character.Faction));
        string[] names = Enum.GetNames(typeof(Character.Faction));
        Dictionary<Character.Faction, string> map = new();
        for (int i = 0; i < values.Length; ++i)
        {
            map[(Character.Faction)values.GetValue(i)] = names[i];
        }

        return map;
    }

    [HarmonyPatch(typeof(Enum), nameof(Enum.GetValues))]
    private static class Enum_GetValues_Patch
    {
        private static void Postfix(Type enumType, ref Array __result)
        {
            if (enumType != typeof(Character.Faction)) return;
            if (m_factions.Count == 0) return;
            Character.Faction[] factions = new Character.Faction[__result.Length + m_factions.Count];
            __result.CopyTo(factions, 0);
            m_factions.Values.CopyTo(factions, __result.Length);

            __result = factions;
        }
    }

    [HarmonyPatch(typeof(Enum), nameof(Enum.GetNames))]
    private static class Enum_GetNames_Patch
    {
        private static void Postfix(Type enumType, ref string[] __result)
        {
            if (enumType != typeof(Character.Faction)) return;
            if (m_factions.Count == 0) return;
            __result = __result.AddRangeToArray(m_factions.Keys.ToArray());
        }
    }
    
     [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), typeof(Character),typeof(Character))]
     private static class BaseAI_IsEnemy_Patch
     {
         private static bool Prefix(Character a, Character b, ref bool __result)
         {
             if (a is not Companion && b is not Companion) return true;
             __result = IsEnemy(a, b);
             return false;
         }
     }

    private static bool IsEnemy(Character a, Character b)
    {
        if (a == b) return false;
        if (a is Companion companionA) return IsEnemy(companionA, b);
        if (b is Companion companionB) return IsEnemy(companionB, a);
        return a.GetFaction() != b.GetFaction();
    }

    private static bool IsEnemy(Companion companion, Character character)
    {
        if (character is Companion companionB) return IsEnemyToCompanion(companion, companionB);
        if (character.GetFaction() is Character.Faction.Players) return IsEnemyToPlayers(companion);
        if (companion.IsTamed()) return HandleTamedCompanion(companion, character);
        return IsEnemyToCreatures(companion, character);
    }

    private static bool IsEnemyToCreatures(Companion companion, Character character)
    {
        var faction = character.GetFaction();
        var baseAI = character.GetBaseAI();
        if (!m_customs.TryGetValue(companion.GetFaction(), out CustomFaction data)) return true;
        if (data.m_friendly && character.m_tameable) return false;
        if (!data.m_friendly && faction is Character.Faction.Boss) return false;
        if (baseAI.m_aggravatable && !baseAI.IsAggravated()) return false;
        return true;
    }

    private static bool HandleTamedCompanion(Companion companion, Character character)
    {
        switch (companion.m_companionAI.m_behavior)
        {
            case "guard":
                if (companion.m_companionAI.GetFollowTarget() is not { } followTarget) return false;
                if (!followTarget.TryGetComponent(out Player player)) return false;
                return player.GetHealth() < player.GetMaxHealth() || player.InAttack();
            default:
                return IsEnemyToCreatures(companion, character);
        }
    }

    private static bool IsEnemyToPlayers(Companion a)
    {
        if (!m_customs.TryGetValue(a.GetFaction(), out CustomFaction data)) return true;
        if (!data.m_friendly) return true;
        if (a.m_companionAI.m_aggravated && !a.IsTamed()) return true;
        return false;
    }

    private static bool IsEnemyToCompanion(Companion a, Companion b)
    {
        if (!m_customs.TryGetValue(a.GetFaction(), out CustomFaction aFaction)) return true;
        if (!m_customs.TryGetValue(b.GetFaction(), out CustomFaction bFaction)) return true;
        if (aFaction.m_faction == bFaction.m_faction) return false;
        if (aFaction.m_name == "Elf") return IsEnemyToElf(b, a);
        if (bFaction.m_name == "Elf") return IsEnemyToElf(a, b);
        return true;
    }

    private static bool IsEnemyToElf(Companion companion, Companion elf)
    {
        if (elf.m_companionAI.IsAggravated()) return true;
        return companion.IsRaider();
    }
}