using System;
using System.Collections.Generic;
using HarmonyLib;
using Settlers.Behaviors;

namespace Settlers.Settlers;

public static class InputCommands
{
    private static readonly Dictionary<string, Action> m_actions = new();

    private static void LoadCommandActions()
    {
        m_actions["sit"] = () =>
        {
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                companion.m_companionAI.m_seekAttempts = 0;
                companion.m_companionAI.SeekChair();
            }
        };
        m_actions["standup"] = () =>
        {
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                companion.m_companionAI.BreakSit();
            }
        };
        m_actions["ride"] = () =>
        {
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                companion.m_companionAI.m_seekAttempts = 0;
                companion.m_companionAI.SeekSaddle();
            }
        };
        m_actions["dance"] = () =>
        {
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                companion.m_companionTalk.QueueEmote("emote_dance");
            }
        };
        m_actions["follow"] = () =>
        {
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                if (companion.m_companionAI.GetFollowTarget() != null) continue;
                companion.Command(Player.m_localPlayer);
            }
        };
        m_actions["stay"] = () =>
        {
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                var follow = companion.m_companionAI.GetFollowTarget();
                if (!follow.TryGetComponent(out Player player)) continue;
                if (player != Player.m_localPlayer) continue;
                companion.Command(Player.m_localPlayer);
            }
        };
    }

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
    private static class Terminal_Awake_Patch
    {
        private static void Postfix() => LoadCommandActions();
    }

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.AddString), typeof(string), typeof(string), typeof(Talker.Type), typeof(bool))]
    private static class Terminal_AddString_Patch
    {
        private static void Postfix(string text)
        {
            SettlersPlugin.SettlersLogger.LogWarning(text);
            if (!m_actions.TryGetValue(text, out Action command)) return;
            command();
        }
    }
}