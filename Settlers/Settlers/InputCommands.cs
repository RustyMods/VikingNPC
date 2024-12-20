using System;
using System.Collections.Generic;
using System.Linq;
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
        m_actions["board"] = companion =>
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
            if (companion.m_tameableCompanion == null) return;
            companion.m_tameableCompanion.Command(Player.m_localPlayer);
        };
        m_actions["stay"] = companion =>
        {
            if (companion.m_tameableCompanion == null) return;
            var follow = companion.m_companionAI.GetFollowTarget();
            if (!follow.TryGetComponent(out Player player)) return;
            if (player != Player.m_localPlayer) return;
            companion.m_tameableCompanion.Command(Player.m_localPlayer);
        };
        m_actions["come"] = companion =>
        {
            if (!companion.IsTamed() || !Player.m_localPlayer || companion is not Settler settler) return;
            settler.Warp(Player.m_localPlayer);
        };

        m_actions["hide"] = companion =>
        {
            if (!companion.IsTamed() || !Player.m_localPlayer) return;
            companion.HideHandItems();
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

    private static bool HandleBarberShop(string text)
    {
        string[] words = text.Split(' ');
        int index = GetNumberInString(words);
        if (index > 20) return false;
        bool updated = false;
        foreach (var companion in Companion.m_instances)
        {
            string[] names = companion.m_name.Split(' ');
            if (!IsNamePartOfSentence(text, names)) continue;
            
            if (text.ToLower().Contains("hair"))
            {
                companion.m_visEquipment.SetHairItem("Hair" + index);
                companion.m_hairItem = "Hair" + index;
                companion.m_nview.GetZDO().Set("HairIndex".GetStableHashCode(), index);
                updated = true;
            }
            else if (text.ToLower().Contains("beard"))
            {
                companion.m_visEquipment.SetBeardItem("Beard" + index);
                companion.m_beardItem = "Beard" + index;
                companion.m_nview.GetZDO().Set("BeardIndex".GetStableHashCode(), index);
                updated = true;
            }
        }

        return updated;
    }

    private static void HandleSexChange(string text)
    {
        string[] words = text.Split(' ');
        int modelIndex = -1;
        if (words.Contains("female")) modelIndex = 1;
        else if (words.Contains("male")) modelIndex = 0;
        if (modelIndex != -1)
        {
            foreach (var companion in Companion.m_instances)
            {
                string[] names = companion.m_name.Split(' ');
                if (!IsNamePartOfSentence(text, names)) continue;
                companion.m_nview.GetZDO().Set(ZDOVars.s_modelIndex, modelIndex);
                companion.m_visEquipment.UpdateBaseModel();
            }
        }
    }

    private static int GetNumberInString(string[] words)
    {
        foreach (string word in words)
        {
            if (int.TryParse(word, out int number)) return number;
        }

        return 0;
    }
    
    private static bool HandleCommands(string text, string[] words)
    {
        Action<Companion>? command = null;
        foreach (string word in words)
        {
            if (m_actions.TryGetValue(word, out command)) break;
        }

        if (command == null) return false;

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

        return true;
    }

    private static void HandleConversations(string text)
    {
        var companion = Companion.GetNearestCompanion();
        if (companion == null) return;
        List<string>? retorts = null;
        foreach (var kvp in Conversation.m_speech)
        {
            var key = kvp.Key.ToLower();
            if (!text.ToLower().Contains(key)) continue;
            retorts = kvp.Value;
            break;
        }

        if (retorts == null) return;

        companion.m_companionTalk.QueueSay(retorts, "", null);
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
            if (HandleCommands(text, words)) return;
            HandleConversations(text);
            if (HandleBarberShop(text)) return;
            HandleSexChange(text);
        }
        
    }
}