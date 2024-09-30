using System;
using System.Collections.Generic;
using HarmonyLib;
using Settlers.Behaviors;
namespace Settlers.Settlers;

public static class InputCommands
{
    private static readonly Dictionary<string, Action<Companion>> m_actions = new();

    private static readonly List<string> m_emotes = new()
    {
        "emote_dance", "emote_despair", "emote_cry", "emote_point", "emote_flex", "emote_challenge", "emote_cheer",
        "emote_blowkiss", "emote_comehere", "emote_laugh", "emote_roar", "emote_shrug", "emote_wave", "emote_bow"
    };

    private static void LoadCommandActions()
    {
        m_actions["sit"] = companion =>
        {
            companion.m_companionAI.m_seekAttempts = 0;
            companion.m_companionAI.SeekChair();
        };
        m_actions["standup"] = companion =>
        {
            companion.m_companionAI.BreakSit();
        };
        m_actions["ride"] = companion =>
        {
            companion.m_companionAI.m_seekAttempts = 0;
            companion.m_companionAI.SeekSaddle();
        };
        m_actions["follow"] = companion =>
        {
            if (companion.m_companionAI.GetFollowTarget() != null) return;
            companion.Command(Player.m_localPlayer);
        };
        m_actions["stay"] = companion =>
        {
            var follow = companion.m_companionAI.GetFollowTarget();
            if (!follow.TryGetComponent(out Player player)) return;
            if (player != Player.m_localPlayer) return;
            companion.Command(Player.m_localPlayer);
        };
        foreach (var behavior in CompanionAI.m_acceptableBehaviors)
        {
            m_actions[behavior] = companion =>
            {
                companion.m_nview.InvokeRPC(nameof(CompanionAI.RPC_SetBehavior), behavior);
            };
        }
        foreach (var emote in m_emotes)
        {
            var key = emote.Replace("emote_", string.Empty);
            m_actions[key] = companion =>
            {
                companion.m_companionTalk.QueueEmote(emote);
            };
        }
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
            string[] words = text.Split(' ');
            Action<Companion>? command = null;
            foreach (string word in words)
            {
                if (m_actions.TryGetValue(word, out command)) break;
            }

            if (command == null) return;

            foreach (Companion companion in Companion.m_instances)
            {
                if (!companion.IsTamed()) continue;
                if (text.Contains("everyone"))
                {
                    command(companion);
                }
                else
                {
                    string[] names = companion.m_name.Split(' ');
                    if (IsNamePartOfSentence(text, names))
                    {
                        command(companion);
                    }
                }
            }
        }

        private static bool IsNamePartOfSentence(string sentence, string[] names)
        {
            foreach (var name in names)
            {
                if (IsNamePartContained(sentence, name)) return true;
            }

            return false;
        }
        private static bool IsNamePartContained(string sentence, string namePart)
        {
            return sentence.ToLower().Contains(namePart.ToLower());
        }
    }
}