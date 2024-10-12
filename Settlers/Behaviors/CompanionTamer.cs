﻿using BepInEx;
using Settlers.Settlers;
using UnityEngine;

namespace Settlers.Behaviors;

public class TameableCompanion : MonoBehaviour, Interactable, TextReceiver
{
    public EffectList m_tamedEffect = new EffectList();
    public EffectList m_sootheEffect = new EffectList();
    public EffectList m_petEffect = new EffectList();
    public EffectList m_unSummonEffect = new EffectList();

    public ZNetView m_nview = null!;
    public Companion m_companion = null!;
    public CompanionAI m_companionAI = null!;
    public CompanionTalk m_companionTalk = null!;
    public CompanionContainer m_companionContainer = null!;
    
    public float m_lastPetTime;
    public float m_unsummonTime;

    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_companion = GetComponent<Companion>();
        m_companionAI = GetComponent<CompanionAI>();
        m_companionTalk = GetComponent<CompanionTalk>();
        m_companionContainer = GetComponent<CompanionContainer>();

        m_companionAI.m_onConsumedItem += OnConsumedItem;

        if (!m_nview.IsValid()) return;
        
        m_nview.Register<ZDOID, bool>(nameof(RPC_Command), RPC_Command);
        m_nview.Register<string, string>(nameof(RPC_SetName), RPC_SetName);
        
        InvokeRepeating(nameof(TamingUpdate), 3f, 3f);

        if (SettlersPlugin._settlerTamingTime.Value <= 0f)
        {
            m_companion.SetTamed(true);
        }

    }
    
    public void TamingUpdate()
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner() || m_companion.IsTamed() || m_companion.IsHungry()) return;
        if (m_companionAI == null) return;
        if (m_companionAI.IsAlerted()) return;
        m_companionAI.SetDespawnInDay(false);
        m_companionAI.SetEventCreature(false);
        DecreaseRemainingTime(3f);
        if (GetRemainingTamingTime() <= 0.0)
        {
            Tame();
            CancelInvoke(nameof(TamingUpdate));
        }
        else
        {
            Transform transform1 = transform;
            m_sootheEffect.Create(transform1.position, transform1.rotation);
        }
    }
    
    public void Tame()
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner() || m_companion.IsTamed()) return;
        if (m_companionAI == null) return;
        if (m_companion.IsRaider() || m_companion.IsSailor()) return;
        if (m_companion.IsElf() && SettlersPlugin._elfTamable.Value is SettlersPlugin.Toggle.Off) return;
        m_companionAI.MakeTame();
        Transform transform1 = transform;
        Vector3 position = transform1.position;
        m_tamedEffect.Create(position, transform1.rotation);
        Player closest = Player.GetClosestPlayer(position, 30f);
        m_companionAI.m_aggravatable = false;
        if (!closest) return;
        closest.Message(MessageHud.MessageType.Center, $"{m_companion.m_name} $hud_tamedone");
        m_companion.SetOwner(closest);
        m_companion.m_faction = Character.Faction.Players;
    }
    
    public void DecreaseRemainingTime(float time)
    {
        if (!m_nview.IsValid()) return;
        float num = GetRemainingTamingTime() - time;
        if (num < 0.0) num = 0.0f;
        m_nview.GetZDO().Set(ZDOVars.s_tameTimeLeft, num);
    }
    
    private float GetRemainingTamingTime() => !m_nview.IsValid() ? 0.0f : m_nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, SettlersPlugin._settlerTamingTime.Value);


    public void RPC_SetName(long sender, string input, string authorId)
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner() || !m_companion.IsTamed()) return;
        m_nview.GetZDO().Set(ZDOVars.s_tamedName, input);
        m_nview.GetZDO().Set(ZDOVars.s_tamedNameAuthor, authorId);
    }

    public void SetName()
    {
        if (!m_companion.IsTamed()) return;
        TextInput.instance.RequestText( this, "$hud_rename", 100);
    }
    
    public void Command(Humanoid user, bool message = true)
    {
        m_companion.RemovePins();
        m_nview.InvokeRPC(nameof(RPC_Command), user.GetZDOID(), message);
        if (!m_companion.m_followTargetName.IsNullOrWhiteSpace()) m_companion.AddPin();
    }
    
    public void RPC_Command(long sender, ZDOID characterID, bool message)
    {
        GameObject instance = ZNetScene.instance.FindInstance(characterID);
        if (!instance) return;
        if (!instance.TryGetComponent(out Player player)) return;
        if (m_companionAI == null) return;
        if (!m_nview.IsOwner()) m_nview.ClaimOwnership();
        if (m_companionAI.GetFollowTarget())
        {
            m_companionAI.SetFollowTarget(null);
            m_companionAI.SetPatrolPoint();
            m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            m_companion.m_followTargetName = "";
            if (message) player.Message(MessageHud.MessageType.Center, $"{m_companion.m_name} $hud_tamestay");
        }
        else
        {
            m_companionAI.ResetPatrolPoint();
            m_companionAI.SetFollowTarget(player.gameObject);
            m_nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
            m_companion.m_followTargetName = player.GetHoverName();
            if (message) player.Message(MessageHud.MessageType.Center, $"{m_companion.m_name} $hud_tamefollow");
        }
    }
    
    public void OnConsumedItem(ItemDrop item) => OnConsumedItemData(item.m_itemData);
    
    public void OnConsumedItemData(ItemDrop.ItemData item)
    {
        if (m_companion.IsHungry()) m_sootheEffect.Create(m_companion.GetCenterPoint(), Quaternion.identity);
        ResetFeedingTimer();
        
        if (item.m_shared.m_consumeStatusEffect)
        {
            m_companion.GetSEMan().AddStatusEffect(item.m_shared.m_consumeStatusEffect, true);
        }

        if (!m_companionTalk.QueueSay(ItemSay.GetConsumeSay(item.m_shared.m_name), "eat", null))
        {
            m_companion.m_animator.SetTrigger("eat");
        }
    }
    
    public void ResetFeedingTimer() => m_nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, ZNet.instance.GetTime().Ticks);

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (!m_nview.IsValid() || hold) return false;
        if (!m_companion.IsTamed()) return false;

        long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
        if (alt)
        {
            if (m_companionContainer == null) return false;
            if (!m_companionContainer.CheckAccess(playerId))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_cantopen");
                return false;
            }

            m_companionContainer.RequestOpen(playerId);
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftAlt))
            {
                TextInput.instance.RequestText(this, "$hud_rename", 100);
            }
            else
            {
                if (Time.time - m_lastPetTime <= 1.0) return false;
                if (user is Player player) m_companion.SetOwner(player);
                m_lastPetTime = Time.time;

                Transform transform1 = transform;
                m_petEffect.Create(transform1.position, transform1.rotation);
                if (m_companion.m_followTargetName.IsNullOrWhiteSpace() || m_companion.m_followTargetName == user.GetHoverName())
                {
                    Command(user);
                }
                else
                {
                    user.Message(MessageHud.MessageType.Center, $"$msg_already_following {m_companion.m_followTargetName}");
                }
            }
        }
        return true;
    }
    
    private bool Feed(ItemDrop.ItemData item)
    {
        if (item.m_shared.m_itemType is not ItemDrop.ItemData.ItemType.Consumable) return false;
        if (item.m_shared.m_consumeStatusEffect != null) return false;
        if (item.m_shared.m_food <= 0.0) return false;

        ResetFeedingTimer();
        
        m_sootheEffect.Create(m_companion.GetCenterPoint(), Quaternion.identity);
        m_companion.m_consumeItemEffects.Create(transform.position, Quaternion.identity);
        if (!m_companionTalk.QueueSay(ItemSay.GetConsumeSay(item.m_shared.m_name), "eat", null))
        {
            m_companion.m_animator.SetTrigger("eat");
        }
        
        return true;
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item)
    {
        switch (item.m_shared.m_itemType)
        {
            case ItemDrop.ItemData.ItemType.Consumable:
                if (m_companion.IsTamed())
                {
                    if (!m_companion.EatFood(item)) return false;
                    OnConsumedItemData(item);
                }
                else
                {
                    if (!Feed(item)) return false;
                }
                break;
            default:
                if (!m_companion.IsTamed()) return false;
                m_companion.GetInventory().AddItem(item);
                m_companion.UpdateEquipment();
                m_companionTalk.QueueSay(ItemSay.GetItemSay(item), "", null);
                m_companion.SaveInventory();
                break;
        }
        user.GetInventory().RemoveOneItem(item);
        return true;
    }
    
    public string GetStatus()
    {
        if (m_companionAI.IsAlerted()) return "$hud_tamefrightened";
        if (m_companion.IsHungry()) return "$hud_tamehungry";
        if (m_companion.IsEncumbered()) return "$hud_encumbered";
        if (m_companionAI != null && m_companionAI.m_resting) return "$hud_tametired";
        if (m_companionAI != null && !m_companionAI.GetCurrentAction().IsNullOrWhiteSpace())
            return $"$hud_{m_companionAI.GetCurrentAction()}";
        return m_companion.IsTamed() ? "$hud_tamehappy" : "$hud_tameinprogress";
    }

    public string GetText()
    {
        if (m_companion == null) return "";
        if (!m_nview.IsValid()) return m_companion.m_name;
        return m_nview.GetZDO().GetString(ZDOVars.s_tamedName, m_companion.m_name);
    }

    public void SetText(string text)
    {
        if (!m_nview.IsValid()) return;
        m_nview.GetZDO().Set(ZDOVars.s_tamedName, text);
        m_companion.m_name = text;
    }
    
    public int GetTameness() => (int)((1.0 - Mathf.Clamp01(GetRemainingTamingTime() / SettlersPlugin._settlerTamingTime.Value)) * 100.0);

    
}