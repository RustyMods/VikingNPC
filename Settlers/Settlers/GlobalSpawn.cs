using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Settlers.Behaviors;
using UnityEngine;

namespace Settlers.Settlers;

public static class GlobalSpawn
{
    [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Awake))]
    private static class SpawnSystem_AddSettlers
    {
        private static void Postfix(SpawnSystem __instance)
        {
            if (!SettlersPlugin._Root.TryGetComponent(out SpawnSystemList component)) return;
            __instance.m_spawnLists.Add(component);
        }
    }

    [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Spawn))]
    private static class SpawnSystem_Spawn_Patch
    {
        private static bool Prefix(SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner)
        {
            if (!critter.m_prefab.TryGetComponent(out ShipMan shipMan)) return true;
            spawnPoint.y = ZoneSystem.instance.m_waterLevel;
            foreach (var ship in ShipMan.m_instances)
            {
                var distance = Vector3.Distance(ship.transform.position, spawnPoint);
                if (distance < 50f) return false;
            }
            if (!ZoneSystem.instance.IsZoneLoaded(spawnPoint)) return false;
            GameObject gameObject = Object.Instantiate(critter.m_prefab, spawnPoint, Quaternion.identity);
            if (Terminal.m_showTests && Terminal.m_testList.ContainsKey("spawns"))
            {
                Terminal.Log($"Spawning {critter.m_prefab.name} at {spawnPoint}");
                Chat.instance.SendPing(spawnPoint);
            }

            if (eventSpawner)
            {
                shipMan.EventSpawnSetAggravated();
            }
            return false;
        }
    }

    public static void AddToSpawnList(GameObject prefab, string configKey, Heightmap.Biome biomeSetting, SettlersPlugin.Toggle isEnabled = SettlersPlugin.Toggle.On, float spawnInterval = 1000f, float spawnChance = 50f, float spawnDistance = 35f)
    {
        if (!SettlersPlugin._Root.TryGetComponent(out SpawnSystemList component))
        {
            component = SettlersPlugin._Root.AddComponent<SpawnSystemList>();
        }
        
        SpawnSystem.SpawnData data = new SpawnSystem.SpawnData
        {
            m_name = prefab.name
        };

        ConfigEntry<SettlersPlugin.Toggle> enabled = SettlersPlugin._Plugin.config(configKey, "_Enabled", isEnabled, "If true, viking settlers can spawn");
        data.m_enabled = enabled.Value is SettlersPlugin.Toggle.On;
        enabled.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_enabled = enabled.Value is SettlersPlugin.Toggle.On;
        };
        data.m_prefab = ZNetScene.instance.GetPrefab(prefab.name);
        ConfigEntry<Heightmap.Biome> biome = SettlersPlugin._Plugin.config(configKey, "Biomes", biomeSetting, "Set biomes settlers can spawn in");
        data.m_biome = biome.Value;
        biome.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_biome = biome.Value;
        };
        ConfigEntry<Heightmap.BiomeArea> area = SettlersPlugin._Plugin.config(configKey, "Biome Area", Heightmap.BiomeArea.Everything, "Set particular part of biome to spawn in");
        data.m_biomeArea = area.Value;
        area.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_biomeArea = area.Value;
        };
        ConfigEntry<int> max = SettlersPlugin._Plugin.config(configKey, "Max Spawned", 1, "Total number of instances, if near player is set, only instances within the max spawn radius is counted");
        data.m_maxSpawned = max.Value;
        max.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_maxSpawned = max.Value;
        };
        ConfigEntry<float> interval = SettlersPlugin._Plugin.config(configKey, "Spawn Interval", spawnInterval, "How often settler spawns");
        data.m_spawnInterval = interval.Value;
        interval.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnInterval = interval.Value;
        };
        ConfigEntry<float> chance = SettlersPlugin._Plugin.config(configKey, "Spawn Chance", spawnChance, new ConfigDescription("Chance to spawn each spawn interval", new AcceptableValueRange<float>(0f, 100f)));
        data.m_spawnChance = chance.Value;
        chance.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnChance = chance.Value;
        };
        ConfigEntry<float> distance = SettlersPlugin._Plugin.config(configKey, "Spawn Distance", spawnDistance, "Spawn range, 0 = use global settings");
        data.m_spawnDistance = distance.Value;
        distance.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnDistance = distance.Value;
        };
        ConfigEntry<string> key = SettlersPlugin._Plugin.config(configKey, "Required Global Key", "", "Only spawn if this key is set");
        data.m_requiredGlobalKey = key.Value;
        key.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_requiredGlobalKey = key.Value;
        };
        ConfigEntry<string> environments = SettlersPlugin._Plugin.config(configKey, "Required Environments", "", "[environment]:[environment]:etc..., only spawn if this environment is active");
        data.m_requiredEnvironments = environments.Value.IsNullOrWhiteSpace() ? new List<string>() : environments.Value.Split(':').ToList();
        environments.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_requiredEnvironments = environments.Value.IsNullOrWhiteSpace() ? new List<string>() : environments.Value.Split(':').ToList();
        };
        ConfigEntry<bool> spawnNight = SettlersPlugin._Plugin.config(configKey, "Spawn At Night", true, "If can spawn during night");
        data.m_spawnAtNight = spawnNight.Value;
        spawnNight.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnAtNight = spawnNight.Value;
        };
        ConfigEntry<bool> spawnDay = SettlersPlugin._Plugin.config(configKey, "Spawn At Day", true, "If can spawn during day");
        data.m_spawnAtDay = spawnDay.Value;
        spawnDay.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_spawnAtDay = spawnDay.Value;
        };
        ConfigEntry<float> minAltitude = SettlersPlugin._Plugin.config(configKey, "Minimum Altitude", -1000f, "Set minimum altitude allowed to spawn");
        data.m_minAltitude = minAltitude.Value;
        minAltitude.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_minAltitude = minAltitude.Value;
        };
        ConfigEntry<float> maxAltitude = SettlersPlugin._Plugin.config(configKey, "Maximum Altitude", 1000f, "Set maximum altitude allowed to spawn");
        data.m_maxAltitude = maxAltitude.Value;
        maxAltitude.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_maxAltitude = maxAltitude.Value;
        };
        ConfigEntry<bool> inForest = SettlersPlugin._Plugin.config(configKey, "In Forest", true, "If can spawn in forest");
        data.m_inForest = inForest.Value;
        inForest.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_inForest = inForest.Value;
        };
        ConfigEntry<bool> outForest = SettlersPlugin._Plugin.config(configKey, "Outside Forest", true, "If can spawn outside forest");
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
        ConfigEntry<int> maxLevel = SettlersPlugin._Plugin.config(configKey, "Level Max", 3, "Set max level");
        data.m_maxLevel = maxLevel.Value;
        maxLevel.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_maxLevel = maxLevel.Value;
        }; 
        ConfigEntry<int> minLevel = SettlersPlugin._Plugin.config(configKey, "Level Min", 1, "Set minimum level");
        data.m_minLevel = minLevel.Value;
        minLevel.SettingChanged += (sender, args) =>
        {
            var info = component.m_spawners.Find(x => x.m_name == data.m_name);
            if (info == null) return;
            info.m_minLevel = minLevel.Value;
        };
        ConfigEntry<float> levelChance = SettlersPlugin._Plugin.config(configKey, "Level Up Chance", 50f, new ConfigDescription("Set chance to level up", new AcceptableValueRange<float>(0f, 100f)));
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
}