using System.Collections.Generic;
using HarmonyLib;
using Settlers.Behaviors;
using Settlers.Managers;
using UnityEngine;

namespace Settlers.Settlers;

public static class BaseHuman
{
    private static GameObject? m_ragDoll;
    
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix(ZNetScene __instance)
        {
            CreateBaseHuman();
            Raids.AddRaidEvent(RandEventSystem.m_instance, CreateBaseRaider());
            CreateSpawners();
            BlueprintManager.CreateBaseTerrainObject(__instance);
            BlueprintManager.CreateBlueprintObject(__instance);
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class ObjectDB_Awake_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            if (!__instance || !ZNetScene.instance) return;
            RaiderSE status = ScriptableObject.CreateInstance<RaiderSE>();
            status.name = nameof(RaiderSE);
            status.m_name = "Vikings";
            if (!__instance.m_StatusEffects.Contains(status))
            {
                __instance.m_StatusEffects.Add(status);
            }
            RegisterSettlerPurchase();
        }
    }
    
    private static void CreateSpawners()
    {
        GameObject? prefab = ZNetScene.instance.GetPrefab("Spawner_Draugr");
        if (!prefab) return;
        CreateSpawner(prefab, "VikingRaider");
        CreateSpawner(prefab, "VikingSettler");
    }

    private static void CreateSpawner(GameObject original, string creature)
    {
        GameObject prefab = ZNetScene.instance.GetPrefab(creature);
        if (!prefab) return;
        GameObject? clone = Object.Instantiate(original, SettlersPlugin._Root.transform, false);
        clone.name = $"Spawner_{creature}";
        if (!clone.TryGetComponent(out CreatureSpawner spawnArea)) return;
        spawnArea.m_creaturePrefab = prefab;
        RegisterToZNetScene(clone);
    }
    
    private static void CreateBaseHuman()
    {
        GameObject player = ZNetScene.instance.GetPrefab("Player");
        if (!player) return;
        if (!player.TryGetComponent(out Player component)) return;
        GameObject human = Object.Instantiate(player, SettlersPlugin._Root.transform, false);
        human.name = "VikingSettler";
        DestroyPlayerComponents(human);
        SetZNetView(human);
        Companion companion = human.AddComponent<Companion>();
        SetCompanionValues(human, ref companion, component);
        AddDeathEffects(ref companion);
        AddDefaultItems(ref companion);
        AddAI(human, true, false);
        // AddRandomIdleAnimation(human);
        SetTameSettings(ref companion, "Boar");
        human.AddComponent<RandomHuman>();
        AddRandomTalk(human);
        RegisterToZNetScene(human);
        GlobalSpawn.AddToSpawnList(human, "Settler Spawn Settings");
    }
    
    private static GameObject? CreateBaseRaider()
    {
        GameObject player = ZNetScene.instance.GetPrefab("Player");
        if (!player) return null;
        if (!player.TryGetComponent(out Player component)) return null;
        GameObject raiderHuman = Object.Instantiate(player, SettlersPlugin._Root.transform, false);
        raiderHuman.name = "VikingRaider";
        DestroyPlayerComponents(raiderHuman);
        SetZNetView(raiderHuman);
        Companion raider = raiderHuman.AddComponent<Companion>();
        SetCompanionValues(raiderHuman, ref raider, component, true);
        AddDeathEffects(ref raider);
        AddDefaultItems(ref raider);
        AddAI(raiderHuman, true, true);
        // AddRandomIdleAnimation(raiderHuman);
        SetTameSettings(ref raider, "Boar");
        raiderHuman.AddComponent<RandomHuman>();
        AddRandomTalk(raiderHuman);
        raiderHuman.AddComponent<CharacterDrop>();
        RegisterToZNetScene(raiderHuman);
        GlobalSpawn.AddToSpawnList(raiderHuman, "Raider Spawn Settings");
        return raiderHuman;
    }

    private static void SetZNetView(GameObject prefab)
    {
        if (prefab.TryGetComponent(out ZNetView zNetView))
        {
            zNetView.m_persistent = true;
        }
    }

    private static void DestroyPlayerComponents(GameObject prefab)
    {
        Object.Destroy(prefab.GetComponent<PlayerController>());
        Object.Destroy(prefab.GetComponent<Player>());
        Object.Destroy(prefab.GetComponent<Talker>());
        Object.Destroy(prefab.GetComponent<Skills>());
    }

    private static void SetCompanionValues(GameObject prefab, ref Companion companion, Player player, bool startAsRaider = false)
    {
        companion.m_startAsRaider = startAsRaider;
        companion.name = prefab.name;
        companion.m_name = "Viking";
        companion.m_group = "Humans";
        companion.m_faction = startAsRaider ? SettlersPlugin._raiderFaction.Value : Character.Faction.Dverger;
        companion.m_crouchSpeed = player.m_crouchSpeed;
        companion.m_walkSpeed = player.m_walkSpeed;
        companion.m_speed = player.m_speed;
        companion.m_runSpeed = player.m_runSpeed;
        companion.m_runTurnSpeed = player.m_runTurnSpeed;
        companion.m_acceleration = player.m_acceleration;
        companion.m_jumpForce = player.m_jumpForce;
        companion.m_jumpForceForward = player.m_jumpForceForward;
        companion.m_jumpForceTiredFactor = player.m_jumpForceForward;
        companion.m_airControl = player.m_airControl;
        companion.m_canSwim = true;
        companion.m_swimDepth = player.m_swimDepth;
        companion.m_swimSpeed = player.m_swimSpeed;
        companion.m_swimTurnSpeed = player.m_swimTurnSpeed;
        companion.m_swimAcceleration = player.m_swimAcceleration;
        companion.m_groundTilt = player.m_groundTilt;
        companion.m_groundTiltSpeed = player.m_groundTiltSpeed;
        companion.m_jumpStaminaUsage = player.m_jumpStaminaUsage;
        companion.m_eye = Utils.FindChild(prefab.transform, "EyePos");
        companion.m_hitEffects = player.m_hitEffects;
        companion.m_critHitEffects = player.m_critHitEffects;
        companion.m_backstabHitEffects = player.m_backstabHitEffects;
        companion.m_equipStartEffects = player.m_equipStartEffects;
        companion.m_warpEffect = player.m_skillLevelupEffects;
        companion.m_tombstone = player.m_tombstone;
        companion.m_waterEffects = player.m_waterEffects;
        companion.m_tarEffects = player.m_tarEffects;
        companion.m_slideEffects = player.m_slideEffects;
        companion.m_jumpEffects = player.m_jumpEffects;
        companion.m_flyingContinuousEffect = player.m_flyingContinuousEffect;
        companion.m_tolerateWater = true;
        companion.m_health = 50f;
        companion.m_damageModifiers = player.m_damageModifiers;
        companion.m_staggerWhenBlocked = true;
        companion.m_staggerDamageFactor = player.m_staggerDamageFactor;
        companion.m_unarmedWeapon = player.m_unarmedWeapon;
        companion.m_pickupEffects = player.m_autopickupEffects;
        companion.m_dropEffects = player.m_dropEffects;
        companion.m_consumeItemEffects = player.m_consumeItemEffects;
        companion.m_equipEffects = player.m_equipStartEffects;
        companion.m_perfectBlockEffect = player.m_perfectBlockEffect;
        companion.m_killedEffects = new EffectList()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("sfx_dverger_vo_death"),
                },
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("vfx_fenring_death")
                }
            }
        };
    }

    private static void AddDeathEffects(ref Companion companion)
    {
        GameObject newRagDoll = GetRagDoll();
        companion.m_deathEffects = new()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("vfx_player_death"),
                    m_enabled = true,
                },
                new EffectList.EffectData()
                {
                    m_prefab = newRagDoll,
                    m_enabled = true
                }
            }
        };
    }

    private static GameObject GetRagDoll()
    {
        if (m_ragDoll != null) return m_ragDoll;
        GameObject newRagDoll = Object.Instantiate(ZNetScene.instance.GetPrefab("Player_ragdoll"), SettlersPlugin._Root.transform, false);
        if (newRagDoll.TryGetComponent(out Ragdoll rag))
        {
            rag.m_ttl = 8f;
            rag.m_removeEffect = new()
            {
                m_effectPrefabs = new[]
                {
                    new EffectList.EffectData()
                    {
                        m_prefab = ZNetScene.instance.GetPrefab("vfx_corpse_destruction_small"),
                        m_enabled = true
                    }
                }
            };
            rag.m_float = true;
            rag.m_dropItems = true;
        }
        newRagDoll.name = "viking_settler_ragdoll";
        RegisterToZNetScene(newRagDoll);
        m_ragDoll = newRagDoll;

        return m_ragDoll;
    }

    private static void AddDefaultItems(ref Companion companion)
    {
        companion.m_defaultItems = new []
        {
            ZNetScene.instance.GetPrefab("AxeStone"),
            ZNetScene.instance.GetPrefab("ShieldWood"),
            ZNetScene.instance.GetPrefab("ArmorRagsChest"),
            ZNetScene.instance.GetPrefab("ArmorRagsLegs"),
            ZNetScene.instance.GetPrefab("Torch")
        };
    }

    private static void AddAI(GameObject prefab, bool aggravatable, bool attackPlayerObjects)
    {
        CompanionAI raiderAI = prefab.AddComponent<CompanionAI>();
        raiderAI.m_viewRange = 30f;
        raiderAI.m_viewAngle = 90f;
        raiderAI.m_hearRange = 9999f;
        raiderAI.m_idleSoundInterval = 10f;
        raiderAI.m_idleSoundChance = 0f;
        raiderAI.m_pathAgentType = Pathfinding.AgentType.Humanoid;
        raiderAI.m_moveMinAngle = 90f;
        raiderAI.m_smoothMovement = true;
        raiderAI.m_jumpInterval = 10f;
        raiderAI.m_randomCircleInterval = 2f;
        raiderAI.m_randomMoveInterval = 30f;
        raiderAI.m_randomMoveRange = 3f;
        raiderAI.m_alertRange = 20f;
        raiderAI.m_circulateWhileCharging = true;
        raiderAI.m_interceptTimeMax = 2f;
        raiderAI.m_maxChaseDistance = 300f;
        raiderAI.m_circleTargetInterval = 8f;
        raiderAI.m_circleTargetDuration = 6f;
        raiderAI.m_circleTargetDistance = 8f;
        raiderAI.m_consumeRange = 2f;
        raiderAI.m_consumeSearchRange = 5f;
        raiderAI.m_consumeSearchInterval = 10f;
        raiderAI.m_consumeItems = new();
        raiderAI.m_aggravatable = aggravatable;
        raiderAI.m_attackPlayerObjects = attackPlayerObjects;
    }
    
    private static void AddRandomIdleAnimation(GameObject prefab)
    {
        RandomAnimation randomAnimation = prefab.AddComponent<RandomAnimation>();
        randomAnimation.m_values = new()
        {
            new RandomAnimation.RandomValue()
            {
                m_name = "idle",
                m_value = 5,
                m_interval = 3,
                m_floatTransition = 1f,
            }
        };
    }

    private static void SetTameSettings(ref Companion companion, string cloneFrom)
    {
        GameObject boar = ZNetScene.instance.GetPrefab(cloneFrom);
        if (boar.TryGetComponent(out Tameable tame))
        {
            companion.m_fedDuration = 600f;
            companion.m_tamedEffect = tame.m_tamedEffect;
            companion.m_sootheEffect = tame.m_sootheEffect;
            companion.m_petEffect = tame.m_petEffect;
        }
    }

    private static void AddRandomTalk(GameObject prefab)
    {
        CompanionTalk npcTalk = prefab.AddComponent<CompanionTalk>();
        npcTalk.m_maxRange = 20f;
        npcTalk.m_greetRange = 10f;
        npcTalk.m_byeRange = 15f;
        npcTalk.m_offset = 2f;
        npcTalk.m_minTalkInterval = 3f;
        npcTalk.m_hideDialogDelay = 9f;
        npcTalk.m_randomTalkInterval = 30f;
        npcTalk.m_randomTalkChance = 0.5f;
        npcTalk.m_randomTalk = new List<string>()
        {
            "$npc_settler_random_talk_1",
            "$npc_settler_random_talk_2",
            "$npc_settler_random_talk_3",
            "$npc_settler_random_talk_4",
            "$npc_settler_random_talk_5",
            "$npc_settler_random_talk_6",
            "$npc_settler_random_talk_7",
            "$npc_settler_random_talk_8",
        };
        npcTalk.m_randomTalkInPlayerBase = new List<string>()
        {
            "$npc_settler_in_base_talk_1",
            "$npc_settler_in_base_talk_2",
            "$npc_settler_in_base_talk_3",
            "$npc_settler_in_base_talk_4",
            "$npc_settler_in_base_talk_5",
            "$npc_settler_in_base_talk_6",
            "$npc_settler_in_base_talk_7",
            "$npc_settler_in_base_talk_8",
            
        };
        npcTalk.m_randomGreets = new List<string>()
        {
            "$npc_settler_random_greet_1",
            "$npc_settler_random_greet_2",
            "$npc_settler_random_greet_3",
            "$npc_settler_random_greet_4",
            "$npc_settler_random_greet_5",
            "$npc_settler_random_greet_6",
            "$npc_settler_random_greet_7",
            "$npc_settler_random_greet_8",
        };
        npcTalk.m_randomGoodbye = new List<string>()
        {
            "$npc_settler_random_bye_1",
            "$npc_settler_random_bye_2",
            "$npc_settler_random_bye_3",
            "$npc_settler_random_bye_4",
            "$npc_settler_random_bye_5",
            "$npc_settler_random_bye_6",
            "$npc_settler_random_bye_7",
            "$npc_settler_random_bye_8",
        };
        npcTalk.m_aggravated = new List<string>()
        {
            "$npc_settler_aggravated_1",
            "$npc_settler_aggravated_2",
            "$npc_settler_aggravated_3",
            "$npc_settler_aggravated_4",
            "$npc_settler_aggravated_5",
            "$npc_settler_aggravated_6",
            "$npc_settler_aggravated_7",
            "$npc_settler_aggravated_8",
        };
        npcTalk.m_randomGreetFX = new EffectList()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("sfx_haldor_greet"),
                }
            }
        };
        npcTalk.m_randomGoodbyeFX = new EffectList()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("sfx_haldor_laugh"),
                }
            }
        };
        npcTalk.m_randomTalkFX = new EffectList()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("sfx_dverger_vo_idle")
                }
            }
        };
        npcTalk.m_alertedFX = new EffectList()
        {
            m_effectPrefabs = new[]
            {
                new EffectList.EffectData()
                {
                    m_prefab = ZNetScene.instance.GetPrefab("sfx_dverger_vo_attack")
                }
            }
        };
    }
    
    private static void RegisterSettlerPurchase()
    {
        var haldor = ZNetScene.instance.GetPrefab("Haldor");
        if (!haldor.TryGetComponent(out Trader component)) return;
        var sword = ObjectDB.instance.GetItemPrefab("SwordBronze");
        if (!sword) return;
        var clone = Object.Instantiate(sword, SettlersPlugin._Root.transform, false);
        clone.name = "SettlerSword";
        if (!clone.TryGetComponent(out ItemDrop itemDrop)) return;
        itemDrop.m_itemData.m_shared.m_name = "Viking Settler";
        RegisterToZNetScene(clone);
        RegisterToDatabase(clone);
        var tradeItem = new Trader.TradeItem()
        {
            m_price = 999,
            m_stack = 1,
            m_prefab = itemDrop,
            m_requiredGlobalKey = "defeated_vikingraider"
        };
        if (!component.m_items.Contains(tradeItem)) component.m_items.Add(tradeItem);
    }

    private static void RegisterToDatabase(GameObject prefab)
    {
        if (!ObjectDB.instance) return;
        if (ObjectDB.instance.m_items.Contains(prefab)) return;
        ObjectDB.instance.m_items.Add(prefab);
        ObjectDB.instance.m_itemByHash[prefab.name.GetStableHashCode()] = prefab;
    }

    private static void RegisterToZNetScene(GameObject prefab)
    {
        if (!ZNetScene.instance) return;
        if (ZNetScene.instance.m_prefabs.Contains(prefab)) return;
        if (ZNetScene.instance.m_namedPrefabs.ContainsKey(prefab.name.GetStableHashCode())) return;
        ZNetScene.instance.m_prefabs.Add(prefab);
        ZNetScene.instance.m_namedPrefabs[prefab.name.GetStableHashCode()] = prefab;
    }
}