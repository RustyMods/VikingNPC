﻿using System.Collections.Generic;
using BepInEx;
using UnityEngine;

namespace Settlers.Behaviors;

public class CompanionTalk : MonoBehaviour
{
    public float m_lastTargetUpdate;
    public float m_maxRange = 15f;
    public float m_greetRange = 10f;
    public float m_byeRange = 15f;
    public float m_offset = 2f;
    public float m_minTalkInterval = 1.5f;
    public float m_hideDialogDelay = 5f;
    public float m_randomTalkInterval = 10f;
    public float m_randomTalkChance = 1f;
    public List<string> m_randomTalk = new List<string>();
    public List<string> m_randomTalkInPlayerBase = new List<string>();
    public List<string> m_randomGreets = new List<string>();
    public List<string> m_randomGoodbye = new List<string>();
    public List<string> m_aggravated = new List<string>();
    public List<string> m_shipTalk = new();
    public EffectList m_randomTalkFX = new EffectList();
    public EffectList m_randomGreetFX = new EffectList();
    public EffectList m_randomGoodbyeFX = new EffectList();
    public EffectList m_alertedFX = new EffectList();
    public bool m_didGreet;
    public bool m_didGoodbye;
    public CompanionAI m_companionAI = null!;
    public Animator m_animator = null!;
    public Companion m_companion = null!;
    public ZNetView m_nview = null!;
    public Player? m_targetPlayer;
    public bool m_seeTarget;
    public bool m_hearTarget;
    public float m_shipTalkTimer;
    private static float m_lastShipTalk;
    private readonly Queue<NpcTalk.QueuedSay> m_queuedTexts = new Queue<NpcTalk.QueuedSay>();
    private readonly List<string> m_greetEmotes = new() { "emote_wave", "emote_bow" };
    private readonly List<string> m_randomEmote = new()
    {
        "emote_dance", "emote_despair", "emote_cry", "emote_point", "emote_flex", "emote_challenge", "emote_cheer",
        "emote_blowkiss", "emote_comehere", "emote_laugh", "emote_roar", "emote_shrug"
    };

    private bool m_isSitting;
    private static readonly int EmoteSit = Animator.StringToHash("emote_sit");

    public void Start()
    {
        m_companion = GetComponentInChildren<Companion>();
        m_companionAI = GetComponent<CompanionAI>();
        m_animator = GetComponentInChildren<Animator>();
        m_nview = GetComponent<ZNetView>();
        m_companionAI.m_onBecameAggravated += OnBecameAggravated;
        InvokeRepeating(nameof(RandomTalk), Random.Range(m_randomTalkInterval / 5f, m_randomTalkInterval), m_randomTalkInterval);
    }

    private bool ShouldUpdate()
    {
        if (m_companionAI is not SettlerAI settlerAI) return true;
        return settlerAI.m_treeTarget is null && settlerAI.m_rockTarget is null && settlerAI.m_fishTarget is null;
    }

    public void LookTowardsTarget()
    {
        if (m_targetPlayer == null) return;
        if (m_nview.IsOwner() && m_companion.GetVelocity().magnitude < 0.5 && !m_companion.InAttack())
        {
            m_companion.SetLookDir((m_targetPlayer.GetEyePoint() - m_companion.GetEyePoint()).normalized);
        }
    }
    public void Update()
    {
        if (m_companionAI.IsAlerted() || m_companionAI.GetTargetCreature() is not null || m_companionAI.GetStaticTarget() is not null || !ShouldUpdate() || !m_nview.IsValid()) return;
        
        UpdateTarget();
        if (m_targetPlayer is {} player)
        {
            if (m_seeTarget)
            {
                float num = Vector3.Distance(player.transform.position, transform.position);
                if (!m_didGreet && num < m_greetRange)
                {
                    m_didGreet = true;
                    QueueSay(m_randomGreets, m_greetEmotes[Random.Range(0, m_greetEmotes.Count)], m_randomGreetFX);
                }

                if (m_didGreet && !m_didGoodbye && num > m_byeRange)
                {
                    m_didGoodbye = true;
                    QueueSay(m_randomGoodbye, m_greetEmotes[Random.Range(0, m_greetEmotes.Count)], m_randomGoodbyeFX);
                }
            }
        }
        if (m_isSitting)
        {
            if (m_companionAI.GetFollowTarget() is not {} followTarget || Vector3.Distance(followTarget.transform.position, transform.position) > 10f)
            {
                m_animator.ResetTrigger(EmoteSit);
                m_isSitting = false;
            }
        }

        if (m_companion.IsTamed() && m_companion.IsAttachedToShip())
        {
            m_shipTalkTimer += Time.deltaTime;
            if (m_shipTalkTimer > 30f)
            {
                if (Time.time - m_lastShipTalk > 30f)
                {
                    m_shipTalkTimer = 0.0f;
                    QueueSay(m_shipTalk, "", null);
                    m_lastShipTalk = Time.time;
                }
            }
        }

        UpdateSayQueue();
    }

    public void UpdateTarget()
    {
        if (Time.time - m_lastTargetUpdate <= 1.0) return;
        m_lastTargetUpdate = Time.time;
        m_targetPlayer = null;
        if (Player.GetClosestPlayer(transform.position, m_maxRange) is not {} closestPlayer || m_companionAI.IsEnemy(closestPlayer)) return;
        m_seeTarget = m_companionAI.CanSeeTarget(closestPlayer);
        m_hearTarget = m_companionAI.CanHearTarget(closestPlayer);
        if (!m_seeTarget && !m_hearTarget) return;
        m_targetPlayer = closestPlayer;
    }

    public void OnBecameAggravated(BaseAI.AggravatedReason reason) => QueueSay(m_aggravated, "emote_flex", m_alertedFX);

    public void RandomTalk()
    {
        if (Time.time - NpcTalk.m_lastTalkTime < m_minTalkInterval || Random.Range(0.0f, 1f) > m_randomTalkChance) return;
        UpdateTarget();
        if (m_targetPlayer == null || !m_seeTarget) return;
        QueueSay(InPlayerBase() ? m_randomTalkInPlayerBase : m_randomTalk, m_randomEmote[Random.Range(0, m_randomEmote.Count)], m_randomTalkFX);
    }

    public bool QueueSay(List<string> texts, string trigger, EffectList? effect)
    {
        if (texts.Count == 0 || m_queuedTexts.Count >= 3) return false;
        m_queuedTexts.Enqueue(new NpcTalk.QueuedSay()
        {
            text = texts[Random.Range(0, texts.Count)],
            trigger = trigger,
            m_effect = effect
        });
        return true;
    }

    public void QueueEmote(string trigger)
    {
        m_queuedTexts.Enqueue(new NpcTalk.QueuedSay()
        {
            text = "",
            trigger = trigger
        });
    }

    public void UpdateSayQueue()
    {
        if (m_queuedTexts.Count == 0 || Time.time - NpcTalk.m_lastTalkTime < m_minTalkInterval) return;
        NpcTalk.QueuedSay queuedSay = m_queuedTexts.Dequeue();
        Say(queuedSay.text, queuedSay.trigger);
        queuedSay.m_effect?.Create(transform.position, Quaternion.identity, variant: m_companionAI.IsFemale() ? 1 : 0);
    }

    public void Say(string text, string trigger)
    {
        NpcTalk.m_lastTalkTime = Time.time;
        if (!text.IsNullOrWhiteSpace())
            Chat.instance.SetNpcText(gameObject, Vector3.up * m_offset, 20f, m_hideDialogDelay, "", text, false);
        if (trigger.IsNullOrWhiteSpace()) return;
        m_animator.SetTrigger(trigger);
        if (trigger == "emote_sit") m_isSitting = true;
    }

    public bool InPlayerBase() => EffectArea.IsPointInsideArea(transform.position, EffectArea.Type.PlayerBase, 30f);
    
}