using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Settlers.Settlers;

public static class Raids
{
    public static void AddRaidEvent(RandEventSystem __instance, GameObject? critter)
    {
        if (critter == null) return;
        ConfigEntry<bool> enabled = SettlersPlugin._Plugin.config("Raid Event", "Enabled", true, "If true, raiders randomly raid player");
        ConfigEntry<float> duration = SettlersPlugin._Plugin.config("Raid Event", "Duration", 60f, "Set length of raid");
        ConfigEntry<bool> nearBaseOnly = SettlersPlugin._Plugin.config("Raid Event", "Near Base Only", false, "If true, viking raid only happens near bases");
        ConfigEntry<bool> pause = SettlersPlugin._Plugin.config("Raid Event", "Pause If No Player", false, "If true, raid pauses while no player in area");
        ConfigEntry<float> range = SettlersPlugin._Plugin.config("Raid Event", "Event Range", 96f, "Set range of event");
        ConfigEntry<float> interval = SettlersPlugin._Plugin.config("Raid Event", "Interval", 1300f, "Set interval between raids");
        ConfigEntry<float> chance = SettlersPlugin._Plugin.config("Raid Event", "Chance", 75f, new ConfigDescription("Set chance for raid to start", new AcceptableValueRange<float>(0f, 100f)));
        ConfigEntry<float> spawnDelay = SettlersPlugin._Plugin.config("Raid Event", "Spawn Delay", 30f, "Set delay between spawns");
        ConfigEntry<Heightmap.Biome> biome = SettlersPlugin._Plugin.config("Raid Event", "Biomes", Heightmap.Biome.All, "Set biomes raid can happen");
        ConfigEntry<string> key = SettlersPlugin._Plugin.config("Raid Event", "Required Keys", "defeated_vikingraider", "Set required keys to have raids, many can be set by seperated with :");
        ConfigEntry<string> startMessage = SettlersPlugin._Plugin.config("Raid Event", "Start Message", "", "Set start message");
        ConfigEntry<string> stopMessage = SettlersPlugin._Plugin.config("Raid Event", "Stop Message", "", "Set stop message");
        ConfigEntry<string> music = SettlersPlugin._Plugin.config("Raid Event", "Force Music", "", "Set the music");
        ConfigEntry<string> environment = SettlersPlugin._Plugin.config("Raid Event", "Force Environment", "", "Set the environment");
        ConfigEntry<int> spawnMax = SettlersPlugin._Plugin.config("Raid Event", "Spawn Max", 5, "Set the max amount spawned at a time");

        SpawnSystem.SpawnData data = new SpawnSystem.SpawnData()
        {
            m_biome = Heightmap.Biome.All,
            m_enabled = true,
            m_name = "VikingRaider",
            m_prefab = critter,
            m_groupRadius = 10f,
            m_huntPlayer = true,
            m_inForest = true,
            m_inLava = false,
            m_spawnChance = 100f,
            m_spawnAtDay = true,
            m_spawnInterval = 10f,
            m_spawnAtNight = true,
            m_groupSizeMax = 5,
            m_groupSizeMin = 3,
            m_maxSpawned = spawnMax.Value,
            m_canSpawnCloseToPlayer = true,
            m_insidePlayerBase = false,
            m_minLevel = 1,
            m_maxLevel = 3
        };

        spawnMax.SettingChanged += (sender, args) => data.m_maxSpawned = spawnMax.Value;
        
        RandomEvent raidEvent = new RandomEvent()
        {
            m_spawn = new List<SpawnSystem.SpawnData>() { data },
            m_name = "VikingRaidEvent",
            m_enabled = enabled.Value,
            m_biome = biome.Value,
            m_duration = duration.Value,
            m_random = true,
            m_endMessage = stopMessage.Value,
            m_eventRange = range.Value,
            m_forceEnvironment = environment.Value,
            m_forceMusic = music.Value,
            m_standaloneChance = chance.Value,
            m_standaloneInterval = interval.Value,
            m_startMessage = startMessage.Value,
            m_nearBaseOnly = nearBaseOnly.Value,
            m_requiredGlobalKeys = key.Value.Split(':').ToList(),
            m_pauseIfNoPlayerInArea = pause.Value,
            m_spawnerDelay = spawnDelay.Value
        };

        enabled.SettingChanged += (sender, args) => raidEvent.m_enabled = enabled.Value;
        duration.SettingChanged += (sender, args) => raidEvent.m_duration = duration.Value;
        nearBaseOnly.SettingChanged += (sender, args) => raidEvent.m_nearBaseOnly = nearBaseOnly.Value;
        pause.SettingChanged += (sender, args) => raidEvent.m_pauseIfNoPlayerInArea = pause.Value;
        range.SettingChanged += (sender, args) => raidEvent.m_eventRange = range.Value;
        interval.SettingChanged += (sender, args) => raidEvent.m_standaloneInterval = interval.Value;
        chance.SettingChanged += (sender, args) => raidEvent.m_standaloneChance = chance.Value;
        spawnDelay.SettingChanged += (sender, args) => raidEvent.m_spawnerDelay = spawnDelay.Value;
        biome.SettingChanged += (sender, args) => raidEvent.m_biome = biome.Value;
        key.SettingChanged += (sender, args) => raidEvent.m_requiredGlobalKeys = key.Value.Split(':').ToList();
        startMessage.SettingChanged += (sender, args) => raidEvent.m_startMessage = startMessage.Value;
        stopMessage.SettingChanged += (sender, args) => raidEvent.m_endMessage = stopMessage.Value;
        music.SettingChanged += (sender, args) => raidEvent.m_forceMusic = music.Value;
        environment.SettingChanged += (sender, args) => raidEvent.m_forceEnvironment = environment.Value;
        __instance.m_events.Add(raidEvent);
    }

    [HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.GetCurrentSpawners))]
    private static class RandEventSystem_GetCurrentSpawners_Patch
    {
        private static void Postfix(RandEventSystem __instance, ref List<SpawnSystem.SpawnData>? __result)
        {
            if (__result == null) return;
            var currentEvent = __instance.GetCurrentRandomEvent();
            if (currentEvent == null) return;
            if (currentEvent.m_name != "VikingRaidEvent") return;
            var biome = WorldGenerator.instance.GetBiome(currentEvent.m_pos);
            if (biome is not Heightmap.Biome.Ocean) return;
            __result = new List<SpawnSystem.SpawnData>()
            {
                new SpawnSystem.SpawnData()
                {
                    m_name = "RaiderShips",
                    m_enabled = true,
                    m_biome = Heightmap.Biome.Ocean,
                    m_prefab = ZNetScene.instance.GetPrefab("RaiderShip"),
                }
            };
        }
    }
}