using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Settlers.Managers;

public static class RaidManager
{
    public static readonly RaidEvent Event = new RaidEvent("VikingRaidEvent");

    [HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.Awake))]
    private static class RandEventSystem_Awake_Patch
    {
        private static void Postfix() => RegisterEvent();
    }
    
    // [HarmonyPatch(typeof(RandEventSystem), nameof(RandEventSystem.GetCurrentSpawners))]
    // private static class RandEventSystem_GetCurrentSpawners_Patch
    // {
    //     private static void Postfix(RandEventSystem __instance, ref List<SpawnSystem.SpawnData>? __result)
    //     {
    //         if (__result == null) return;
    //         RandomEvent currentEvent = __instance.GetCurrentRandomEvent();
    //         if (currentEvent is not { m_name: "VikingRaidEvent" }) return;
    //         Debug.LogWarning(__result.Count);
    //         // Heightmap.Biome biome = WorldGenerator.instance.GetBiome(currentEvent.m_pos);
    //         // if (biome is not Heightmap.Biome.Ocean) return;
    //         // __result = new List<SpawnSystem.SpawnData>()
    //         // {
    //         //     new SpawnSystem.SpawnData()
    //         //     {
    //         //         m_name = "RaiderShips",
    //         //         m_enabled = true,
    //         //         m_biome = Heightmap.Biome.Ocean,
    //         //         m_prefab = ZNetScene.instance.GetPrefab("RaiderShip"),
    //         //     }
    //         // };
    //     }
    // }

    private static void RegisterEvent()
    {
        if (!RandEventSystem.instance) return;
        RandEventSystem.instance.m_events.Add(Event.m_randomEvent);
    }
    public class RaidEvent
    {
        private readonly RaidConfigs configs;
        public readonly RandomEvent m_randomEvent;

        public RaidEvent(string name)
        {
            configs = new RaidConfigs(name);
            m_randomEvent = new RandomEvent();
            m_randomEvent.m_name = name;
            m_randomEvent.m_enabled = configs.Enabled.Value is SettlersPlugin.Toggle.On;
            m_randomEvent.m_biome = configs.Biome.Value;
            m_randomEvent.m_duration = configs.Duration.Value;
            m_randomEvent.m_random = true;
            m_randomEvent.m_eventRange = configs.Range.Value;
            m_randomEvent.m_standaloneChance = configs.Chance.Value;
            m_randomEvent.m_standaloneInterval = configs.Interval.Value;
            m_randomEvent.m_nearBaseOnly = configs.NearBaseOnly.Value is SettlersPlugin.Toggle.On;
            m_randomEvent.m_requiredGlobalKeys = new SerializedKeys(configs.RequiredKeys.Value).Keys.Where(key => !key.IsNullOrWhiteSpace()).ToList();
            m_randomEvent.m_pauseIfNoPlayerInArea = false;
            m_randomEvent.m_spawnerDelay = 5f;
            
            void ConfigChanged(object sender, EventArgs args)
            {
                m_randomEvent.m_enabled = configs.Enabled.Value is SettlersPlugin.Toggle.On;
                m_randomEvent.m_biome = configs.Biome.Value;
                m_randomEvent.m_duration = configs.Duration.Value;
                m_randomEvent.m_random = true;
                m_randomEvent.m_eventRange = configs.Range.Value;
                m_randomEvent.m_standaloneChance = configs.Chance.Value;
                m_randomEvent.m_standaloneInterval = configs.Interval.Value;
                m_randomEvent.m_nearBaseOnly = configs.NearBaseOnly.Value is SettlersPlugin.Toggle.On;
                m_randomEvent.m_requiredGlobalKeys = new SerializedKeys(configs.RequiredKeys.Value).Keys.Where(key => !key.IsNullOrWhiteSpace()).ToList();
                m_randomEvent.m_pauseIfNoPlayerInArea = false;
                m_randomEvent.m_spawnerDelay = 5f;
            }

            void SpawnConfigChanged(object sender, EventArgs args)
            {
                SerializedMinMax minMax = new SerializedMinMax(configs.MinMax.Value);
                foreach (SpawnSystem.SpawnData spawner in m_randomEvent.m_spawn)
                {
                    spawner.m_spawnInterval = configs.SpawnInterval.Value;
                    spawner.m_groupSizeMin = minMax.Min;
                    spawner.m_groupSizeMax = minMax.Max;
                    spawner.m_maxSpawned = minMax.Max;
                }
            }

            configs.Enabled.SettingChanged += ConfigChanged;
            configs.Biome.SettingChanged += ConfigChanged;
            configs.Duration.SettingChanged += ConfigChanged;
            configs.Range.SettingChanged += ConfigChanged;
            configs.Chance.SettingChanged += ConfigChanged;
            configs.Interval.SettingChanged += ConfigChanged;
            configs.NearBaseOnly.SettingChanged += ConfigChanged;
            configs.RequiredKeys.SettingChanged += ConfigChanged;

            configs.SpawnInterval.SettingChanged += SpawnConfigChanged;
            configs.MinMax.SettingChanged += SpawnConfigChanged;
        }

        public void Add(SpawnSystem.SpawnData data)
        {
            SerializedMinMax minMax = new SerializedMinMax(configs.MinMax.Value);
            SpawnSystem.SpawnData? clone = data.Clone();
            clone.m_enabled = true;
            clone.m_groupRadius = 10f;
            clone.m_inForest = true;
            clone.m_inLava = false;
            clone.m_spawnAtDay = true;
            clone.m_spawnAtNight = true;
            clone.m_spawnInterval = configs.SpawnInterval.Value;
            clone.m_huntPlayer = true;
            clone.m_groupSizeMax = minMax.Max;
            clone.m_groupSizeMin = minMax.Min;
            clone.m_canSpawnCloseToPlayer = true;
            clone.m_spawnChance = 100f;
            clone.m_maxSpawned = minMax.Max;
            m_randomEvent.m_spawn.Add(clone);
        }

        private class RaidConfigs
        {
            public readonly ConfigEntry<SettlersPlugin.Toggle> Enabled;
            public readonly ConfigEntry<float> Duration;
            public readonly ConfigEntry<SettlersPlugin.Toggle> NearBaseOnly;
            public readonly ConfigEntry<float> Range;
            public readonly ConfigEntry<float> Interval;
            public readonly ConfigEntry<float> Chance;
            public readonly ConfigEntry<Heightmap.Biome> Biome;
            public readonly ConfigEntry<string> RequiredKeys;
            public readonly ConfigEntry<float> SpawnInterval;
            public readonly ConfigEntry<string> MinMax;

            public RaidConfigs(string name)
            {
                name = "2. " + name;
                Enabled = SettlersPlugin._Plugin.config(name, "Enabled", SettlersPlugin.Toggle.Off, "If on, raiders randomly raid players");
                Duration = SettlersPlugin._Plugin.config(name, "Duration", 60f, "Length of raid in seconds");
                NearBaseOnly = SettlersPlugin._Plugin.config(name, "Near Base Only", SettlersPlugin.Toggle.Off, "If on, raids occur only near player bases");
                Range = SettlersPlugin._Plugin.config(name, "Event Range", 96f, new ConfigDescription("Event radius", new AcceptableValueRange<float>(1f, 100f)));
                Interval = SettlersPlugin._Plugin.config(name, "Interval", 0f, "Interval between raids, 0 to use default interval");
                Chance = SettlersPlugin._Plugin.config(name, "Chance", 50f, new ConfigDescription("Chance for raid to start, 0 for default", new AcceptableValueRange<float>(0f, 100f)));
                Biome = SettlersPlugin._Plugin.config(name, "Biome", Heightmap.Biome.All, "Biome raids can happen");
                RequiredKeys = SettlersPlugin._Plugin.config(name, "Required Keys", new SerializedKeys().ToString(), new ConfigDescription("List of required keys", null,
                        new SettlersPlugin.ConfigurationManagerAttributes()
                        {
                            Order = 0,
                            Category = name,
                            CustomDrawer = SerializedKeys.DrawTable
                        }));
                SpawnInterval = SettlersPlugin._Plugin.config(name, "Spawn Interval", 10f, "Interval between spawns");
                MinMax = SettlersPlugin._Plugin.config(name, "Group Size", new SerializedMinMax(1, 1).ToString(),
                    new ConfigDescription("Size of event group", null,
                        new SettlersPlugin.ConfigurationManagerAttributes()
                        {
                            Order = 0,
                            Category = name,
                            CustomDrawer = SerializedMinMax.DrawTable
                        }));
            }
        }
    }

    private class SerializedKeys
    {
        public readonly List<string> Keys;

        public static void DrawTable(ConfigEntryBase cfg)
        {
            bool locked = cfg.Description.Tags
                .Select(a =>
                    a.GetType().Name == "ConfigurationManagerAttributes"
                        ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a)
                        : null).FirstOrDefault(v => v != null) ?? false;
            List<string> newKeys = new();
            bool wasUpdated = false;
            var keys = new SerializedKeys((string)cfg.BoxedValue).Keys;
            if (keys.Count <= 0) keys.Add("");
            GUILayout.BeginVertical();
            foreach (string key in keys)
            {
                GUILayout.BeginHorizontal();
                var newKey = locked ? key : GUILayout.TextField(key, new GUIStyle(GUI.skin.textField));
                wasUpdated = wasUpdated || newKey != key;
                if (GUILayout.Button("x", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
                {
                    wasUpdated = true;
                }
                else
                {
                    newKeys.Add(newKey);
                }

                if (GUILayout.Button("+", new GUIStyle(GUI.skin.button) { fixedWidth = 21 }) && !locked)
                {
                    wasUpdated = true;
                    newKeys.Add("");
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            if (wasUpdated)
            {
                cfg.BoxedValue = new SerializedKeys(newKeys).ToString();
            }
        }

        public SerializedKeys() => Keys = new List<string>();

        public SerializedKeys(string keys) => Keys = keys.Split(',').ToList();

        private SerializedKeys(List<string> keys) => Keys = keys;
        
        public override string ToString() => string.Join(",", Keys);
    }

    private class SerializedMinMax
    {
        public readonly int Min;
        public readonly int Max;

        public SerializedMinMax(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public SerializedMinMax(string input)
        {
            var parts = input.Split(':');
            Min = int.TryParse(parts[0], out int min) ? min : 1;
            Max = int.TryParse(parts[1], out int max) ? max : 1;
        }

        public override string ToString() => $"{Min}:{Max}";

        public static void DrawTable(ConfigEntryBase cfg)
        {
            bool locked = cfg.Description.Tags
                .Select(a =>
                    a.GetType().Name == "ConfigurationManagerAttributes"
                        ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a)
                        : null).FirstOrDefault(v => v != null) ?? false;
            var data = new SerializedMinMax((string)cfg.BoxedValue);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Min: ");
            int min = !locked
                ? int.TryParse(GUILayout.TextField(data.Min.ToString(), new GUIStyle(GUI.skin.textArea)),
                    out int newMin)
                    ? newMin
                    : data.Min
                : data.Min;
            GUILayout.Label("Max: ");
            int max = !locked
                ? int.TryParse(GUILayout.TextField(data.Max.ToString(), new GUIStyle(GUI.skin.textArea)),
                    out int newMax)
                    ? newMax
                    : data.Max
                : data.Max;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            if (min != data.Min || max != data.Max)
            {
                cfg.BoxedValue = new SerializedMinMax(min, max).ToString();
            }
        }
    }
}