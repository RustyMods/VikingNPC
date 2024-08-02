using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Settlers.Managers;
using UnityEngine;

namespace Settlers.Settlers;

public static class BaseHuman
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix(ZNetScene __instance)
        {
            CreateBaseHuman();
            Raids.AddRaidEvent(RandEventSystem.m_instance, CreateBaseRaider());
            CreateSpawners();
            BlueprintManager.CreateBaseTerrainObject(__instance);
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
    
    [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Awake))]
    private static class SpawnSystem_AddSettlers
    {
        private static void Postfix(SpawnSystem __instance)
        {
            if (!SettlersPlugin._Root.TryGetComponent(out SpawnSystemList component)) return;
            __instance.m_spawnLists.Add(component);
        }
    }

    private static void AddToSpawnList(GameObject prefab)
    {
        if (!SettlersPlugin._Root.TryGetComponent(out SpawnSystemList component))
        {
            component = SettlersPlugin._Root.AddComponent<SpawnSystemList>();
        }
        
        component.m_spawners.Clear();
        SpawnSystem.SpawnData data = new SpawnSystem.SpawnData
        {
            m_name = "VikingSettlers"
        };

        ConfigEntry<bool> enabled = SettlersPlugin._Plugin.config("Spawn Settings", "_Enabled", true, "If true, viking settlers can spawn");
        data.m_enabled = enabled.Value;
        enabled.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_enabled = enabled.Value;
        };
        data.m_prefab = ZNetScene.instance.GetPrefab(prefab.name);
        ConfigEntry<Heightmap.Biome> biome = SettlersPlugin._Plugin.config("Spawn Settings", "Biomes", Heightmap.Biome.Meadows, "Set biomes settlers can spawn in");
        data.m_biome = biome.Value;
        biome.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_biome = biome.Value;
        };
        ConfigEntry<Heightmap.BiomeArea> area = SettlersPlugin._Plugin.config("Spawn Settings", "Biome Area", Heightmap.BiomeArea.Everything, "Set particular part of biome to spawn in");
        data.m_biomeArea = area.Value;
        area.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_biomeArea = area.Value;
        };
        ConfigEntry<int> max = SettlersPlugin._Plugin.config("Spawn Settings", "Max Spawned", 1, "Total number of instances, if near player is set, only instances within the max spawn radius is counted");
        data.m_maxSpawned = max.Value;
        max.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_maxSpawned = max.Value;
        };
        ConfigEntry<float> interval = SettlersPlugin._Plugin.config("Spawn Settings", "Spawn Interval", 1000f, "How often settler spawns");
        data.m_spawnInterval = interval.Value;
        interval.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnInterval = interval.Value;
        };
        ConfigEntry<float> chance = SettlersPlugin._Plugin.config("Spawn Settings", "Spawn Chance", 50f, new ConfigDescription("Chance to spawn each spawn interval", new AcceptableValueRange<float>(0f, 100f)));
        data.m_spawnChance = chance.Value;
        chance.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnChance = chance.Value;
        };
        ConfigEntry<float> distance = SettlersPlugin._Plugin.config("Spawn Settings", "Spawn Distance", 35f, "Spawn range, 0 = use global settings");
        data.m_spawnDistance = distance.Value;
        distance.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnDistance = distance.Value;
        };
        ConfigEntry<string> key = SettlersPlugin._Plugin.config("Spawn Settings", "Required Global Key", "", "Only spawn if this key is set");
        data.m_requiredGlobalKey = key.Value;
        key.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_requiredGlobalKey = key.Value;
        };
        ConfigEntry<string> environments = SettlersPlugin._Plugin.config("Spawn Settings", "Required Environments", "", "[environment]:[environment]:etc..., only spawn if this environment is active");
        data.m_requiredEnvironments = environments.Value.IsNullOrWhiteSpace() ? new List<string>() : environments.Value.Split(':').ToList();
        environments.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_requiredEnvironments = environments.Value.IsNullOrWhiteSpace() ? new List<string>() : environments.Value.Split(':').ToList();
        };
        ConfigEntry<bool> spawnNight = SettlersPlugin._Plugin.config("Spawn Settings", "Spawn At Night", true, "If can spawn during night");
        data.m_spawnAtNight = spawnNight.Value;
        spawnNight.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnAtNight = spawnNight.Value;
        };
        ConfigEntry<bool> spawnDay = SettlersPlugin._Plugin.config("Spawn Settings", "Spawn At Day", true, "If can spawn during day");
        data.m_spawnAtDay = spawnDay.Value;
        spawnDay.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnAtDay = spawnDay.Value;
        };
        ConfigEntry<float> minAltitude = SettlersPlugin._Plugin.config("Spawn Settings", "Minimum Altitude", -1000f, "Set minimum altitude allowed to spawn");
        data.m_minAltitude = minAltitude.Value;
        minAltitude.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_minAltitude = minAltitude.Value;
        };
        ConfigEntry<float> maxAltitude = SettlersPlugin._Plugin.config("Spawn Settings", "Maximum Altitude", 1000f, "Set maximum altitude allowed to spawn");
        data.m_maxAltitude = maxAltitude.Value;
        maxAltitude.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_maxAltitude = maxAltitude.Value;
        };
        ConfigEntry<bool> inForest = SettlersPlugin._Plugin.config("Spawn Settings", "In Forest", true, "If can spawn in forest");
        data.m_inForest = inForest.Value;
        inForest.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_inForest = inForest.Value;
        };
        ConfigEntry<bool> outForest = SettlersPlugin._Plugin.config("Spawn Settings", "Outside Forest", true, "If can spawn outside forest");
        data.m_outsideForest = outForest.Value;
        outForest.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_outsideForest = outForest.Value;
        };
        data.m_inLava = false;
        data.m_canSpawnCloseToPlayer = true;
        data.m_insidePlayerBase = false;
        ConfigEntry<int> maxLevel = SettlersPlugin._Plugin.config("Spawn Settings", "Level Max", 3, "Set max level");
        data.m_maxLevel = maxLevel.Value;
        maxLevel.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_maxLevel = maxLevel.Value;
        }; 
        ConfigEntry<int> minLevel = SettlersPlugin._Plugin.config("Spawn Settings", "Level Min", 1, "Set minimum level");
        data.m_minLevel = minLevel.Value;
        minLevel.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_minLevel = minLevel.Value;
        };
        ConfigEntry<float> levelChance = SettlersPlugin._Plugin.config("Spawn Settings", "Level Up Chance", 50f, new ConfigDescription("Set chance to level up", new AcceptableValueRange<float>(0f, 100f)));
        data.m_overrideLevelupChance = levelChance.Value;
        levelChance.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_overrideLevelupChance = levelChance.Value;
        };
        data.m_groupSizeMin = 0;
        data.m_groupSizeMax = 1;
        data.m_levelUpMinCenterDistance = 1f;
        data.m_minTilt = 0f;
        data.m_maxTilt = 50f;
        data.m_groupRadius = 50f;
        component.m_spawners.Add(data);
        component.enabled = true;
    }

    private static void CreateBaseHuman()
    {
        GameObject player = ZNetScene.instance.GetPrefab("Player");
        if (!player) return;
        if (!player.TryGetComponent(out Player component)) return;
        GameObject human = Object.Instantiate(player, SettlersPlugin._Root.transform, false);
        human.name = "VikingSettler";
        Object.Destroy(human.GetComponent<PlayerController>());
        Object.Destroy(human.GetComponent<Player>());
        Object.Destroy(human.GetComponent<Talker>());
        Object.Destroy(human.GetComponent<Skills>());
        if (human.TryGetComponent(out ZNetView zNetView))
        {
            zNetView.m_persistent = true;
        }
        Companion companion = human.AddComponent<Companion>();
        companion.name = human.name;
        companion.m_name = "Human";
        companion.m_group = "Humans";
        companion.m_faction = Character.Faction.Dverger;
        companion.m_crouchSpeed = component.m_crouchSpeed;
        companion.m_walkSpeed = component.m_walkSpeed;
        companion.m_speed = component.m_speed;
        companion.m_runSpeed = component.m_runSpeed;
        companion.m_runTurnSpeed = component.m_runTurnSpeed;
        companion.m_acceleration = component.m_acceleration;
        companion.m_jumpForce = component.m_jumpForce;
        companion.m_jumpForceForward = component.m_jumpForceForward;
        companion.m_jumpForceTiredFactor = component.m_jumpForceForward;
        companion.m_airControl = component.m_airControl;
        companion.m_canSwim = true;
        companion.m_swimDepth = component.m_swimDepth;
        companion.m_swimSpeed = component.m_swimSpeed;
        companion.m_swimTurnSpeed = component.m_swimTurnSpeed;
        companion.m_swimAcceleration = component.m_swimAcceleration;
        companion.m_groundTilt = component.m_groundTilt;
        companion.m_groundTiltSpeed = component.m_groundTiltSpeed;
        companion.m_jumpStaminaUsage = component.m_jumpStaminaUsage;
        companion.m_eye = Utils.FindChild(human.transform, "EyePos");
        companion.m_hitEffects = component.m_hitEffects;
        companion.m_critHitEffects = component.m_critHitEffects;
        companion.m_backstabHitEffects = component.m_backstabHitEffects;
        companion.m_drownEffects = component.m_drownEffects;
        companion.m_equipStartEffects = component.m_equipStartEffects;
        companion.m_warpEffect = component.m_skillLevelupEffects;
        companion.m_tombstone = component.m_tombstone;
        companion.m_dodgeEffects = component.m_dodgeEffects;

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
        companion.m_waterEffects = component.m_waterEffects;
        companion.m_tarEffects = component.m_tarEffects;
        companion.m_slideEffects = component.m_slideEffects;
        companion.m_jumpEffects = component.m_jumpEffects;
        companion.m_flyingContinuousEffect = component.m_flyingContinuousEffect;
        companion.m_tolerateWater = true;
        companion.m_health = 50f;
        companion.m_damageModifiers = component.m_damageModifiers;
        companion.m_staggerWhenBlocked = true;
        companion.m_staggerDamageFactor = component.m_staggerDamageFactor;
        companion.m_defaultItems = new []
        {
            ZNetScene.instance.GetPrefab("AxeStone"),
            ZNetScene.instance.GetPrefab("ShieldWood"),
            ZNetScene.instance.GetPrefab("ArmorRagsChest"),
            ZNetScene.instance.GetPrefab("ArmorRagsLegs"),
            ZNetScene.instance.GetPrefab("Torch")
        };
        companion.m_unarmedWeapon = component.m_unarmedWeapon;
        companion.m_pickupEffects = component.m_autopickupEffects;
        companion.m_dropEffects = component.m_dropEffects;
        companion.m_consumeItemEffects = component.m_consumeItemEffects;
        companion.m_equipEffects = component.m_equipStartEffects;
        companion.m_perfectBlockEffect = component.m_perfectBlockEffect;

        CompanionAI AI = human.AddComponent<CompanionAI>();
        AI.m_viewRange = 30f;
        AI.m_viewAngle = 90f;
        AI.m_hearRange = 9999f;
        AI.m_idleSoundInterval = 10f;
        AI.m_idleSoundChance = 0f;
        AI.m_pathAgentType = Pathfinding.AgentType.Humanoid;
        AI.m_moveMinAngle = 90f;
        AI.m_smoothMovement = true;
        AI.m_jumpInterval = 10f;
        AI.m_randomCircleInterval = 2f;
        AI.m_randomMoveInterval = 30f;
        AI.m_randomMoveRange = 3f;
        AI.m_alertRange = 20f;
        AI.m_circulateWhileCharging = true;
        AI.m_interceptTimeMax = 2f;
        AI.m_maxChaseDistance = 300f;
        AI.m_circleTargetInterval = 8f;
        AI.m_circleTargetDuration = 6f;
        AI.m_circleTargetDistance = 8f;
        AI.m_consumeRange = 2f;
        AI.m_consumeSearchRange = 5f;
        AI.m_consumeSearchInterval = 10f;
        AI.m_consumeItems = new();
        AI.m_aggravatable = true;
        AI.m_attackPlayerObjects = false;
        RandomAnimation randomAnimation = human.AddComponent<RandomAnimation>();
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
        GameObject boar = ZNetScene.instance.GetPrefab("Boar");
        if (boar.TryGetComponent(out Tameable tame))
        {
            companion.m_fedDuration = 600f;
            // companion.m_tamingTime = SettlersPlugin._settlerTamingTime.Value;
            companion.m_tamedEffect = tame.m_tamedEffect;
            companion.m_sootheEffect = tame.m_sootheEffect;
            companion.m_petEffect = tame.m_petEffect;
        }

        human.AddComponent<RandomHuman>();

        CompanionTalk npcTalk = human.AddComponent<CompanionTalk>();
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
        RegisterToZNetScene(human);
        AddToSpawnList(human);
    }
    
    private static GameObject? CreateBaseRaider()
    {
        GameObject player = ZNetScene.instance.GetPrefab("Player");
        if (!player) return null;
        if (!player.TryGetComponent(out Player component)) return null;
        GameObject raiderHuman = Object.Instantiate(player, SettlersPlugin._Root.transform, false);
        raiderHuman.name = "VikingRaider";
        Object.Destroy(raiderHuman.GetComponent<PlayerController>());
        Object.Destroy(raiderHuman.GetComponent<Player>());
        Object.Destroy(raiderHuman.GetComponent<Talker>());
        Object.Destroy(raiderHuman.GetComponent<Skills>());
        if (raiderHuman.TryGetComponent(out ZNetView zNetView))
        {
            zNetView.m_persistent = true;
        }
        Companion raider = raiderHuman.AddComponent<Companion>();
        raider.m_startAsRaider = true;
        raider.name = raiderHuman.name;
        raider.m_name = "Human";
        raider.m_group = "Humans";
        raider.m_faction = Character.Faction.SeaMonsters;
        raider.m_crouchSpeed = component.m_crouchSpeed;
        raider.m_walkSpeed = component.m_walkSpeed;
        raider.m_speed = component.m_speed;
        raider.m_runSpeed = component.m_runSpeed;
        raider.m_runTurnSpeed = component.m_runTurnSpeed;
        raider.m_acceleration = component.m_acceleration;
        raider.m_jumpForce = component.m_jumpForce;
        raider.m_jumpForceForward = component.m_jumpForceForward;
        raider.m_jumpForceTiredFactor = component.m_jumpForceForward;
        raider.m_airControl = component.m_airControl;
        raider.m_canSwim = true;
        raider.m_swimDepth = component.m_swimDepth;
        raider.m_swimSpeed = component.m_swimSpeed;
        raider.m_swimTurnSpeed = component.m_swimTurnSpeed;
        raider.m_swimAcceleration = component.m_swimAcceleration;
        raider.m_groundTilt = component.m_groundTilt;
        raider.m_groundTiltSpeed = component.m_groundTiltSpeed;
        raider.m_jumpStaminaUsage = component.m_jumpStaminaUsage;
        raider.m_eye = Utils.FindChild(raiderHuman.transform, "EyePos");
        raider.m_hitEffects = component.m_hitEffects;
        raider.m_critHitEffects = component.m_critHitEffects;
        raider.m_backstabHitEffects = component.m_backstabHitEffects;
        raider.m_drownEffects = component.m_drownEffects;
        raider.m_equipStartEffects = component.m_equipStartEffects;
        raider.m_warpEffect = component.m_skillLevelupEffects;
        raider.m_tombstone = component.m_tombstone;
        raider.m_dodgeEffects = component.m_dodgeEffects;

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
        raider.m_deathEffects = new()
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
        raider.m_waterEffects = component.m_waterEffects;
        raider.m_tarEffects = component.m_tarEffects;
        raider.m_slideEffects = component.m_slideEffects;
        raider.m_jumpEffects = component.m_jumpEffects;
        raider.m_flyingContinuousEffect = component.m_flyingContinuousEffect;
        raider.m_tolerateWater = true;
        raider.m_health = 50f;
        raider.m_damageModifiers = component.m_damageModifiers;
        raider.m_staggerWhenBlocked = true;
        raider.m_staggerDamageFactor = component.m_staggerDamageFactor;
        raider.m_defaultItems = new []
        {
            ZNetScene.instance.GetPrefab("AxeStone"),
            ZNetScene.instance.GetPrefab("ShieldWood"),
            ZNetScene.instance.GetPrefab("ArmorRagsChest"),
            ZNetScene.instance.GetPrefab("ArmorRagsLegs"),
            ZNetScene.instance.GetPrefab("Torch")
        };
        raider.m_unarmedWeapon = component.m_unarmedWeapon;
        raider.m_pickupEffects = component.m_autopickupEffects;
        raider.m_dropEffects = component.m_dropEffects;
        raider.m_consumeItemEffects = component.m_consumeItemEffects;
        raider.m_equipEffects = component.m_equipStartEffects;
        raider.m_perfectBlockEffect = component.m_perfectBlockEffect;

        CompanionAI raiderAI = raiderHuman.AddComponent<CompanionAI>();
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
        raiderAI.m_aggravatable = true;
        raiderAI.m_attackPlayerObjects = true;
        RandomAnimation randomAnimation = raiderHuman.AddComponent<RandomAnimation>();
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
        GameObject boar = ZNetScene.instance.GetPrefab("Boar");
        if (boar.TryGetComponent(out Tameable tame))
        {
            raider.m_fedDuration = 600f;
            // companion.m_tamingTime = 1800f;
            raider.m_tamedEffect = tame.m_tamedEffect;
            raider.m_sootheEffect = tame.m_sootheEffect;
            raider.m_petEffect = tame.m_petEffect;
        }

        raiderHuman.AddComponent<RandomHuman>();

        CompanionTalk npcTalk = raiderHuman.AddComponent<CompanionTalk>();
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
        raiderHuman.AddComponent<CharacterDrop>();
        RegisterToZNetScene(raiderHuman);
        return raiderHuman;
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