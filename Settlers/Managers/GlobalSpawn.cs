using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Settlers.Behaviors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Settlers.Managers;

public static class GlobalSpawn
{
    public static SpawnSystemList SpawnList = null!;

    [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Awake))]
    private static class SpawnSystem_Awake_Patch
    {
        private static void Postfix(SpawnSystem __instance) => __instance.m_spawnLists.Add(SpawnList);
    }

    [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Spawn))]
    private static class SpawnSystem_Spawn_Patch
    {
        private static bool Prefix(SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner,
            int minLevelOverride, float levelUpMultiplier)
        {
            if (critter is not CustomSpawnData data) return true;
            if (!data.IsShip)
            {
                GameObject gameObject = Object.Instantiate(data.m_prefab, spawnPoint, Quaternion.identity);
                if (Terminal.m_showTests && Terminal.m_testList.ContainsKey("spawns"))
                {
                    Terminal.Log($"Spawning {(object)data.m_prefab.name} at {spawnPoint}");
                    Chat.instance.SendPing(spawnPoint);
                }

                if (gameObject.TryGetComponent(out BaseAI component))
                {
                    if (data.m_huntPlayer && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs)) component.SetHuntPlayer(true);
                }
                
                if (data.m_levelUpMinCenterDistance <= 0.0 || spawnPoint.magnitude > (double) data.m_levelUpMinCenterDistance)
                {
                    var levelData = new CustomSpawnData.SerializedLevel(data.configs.Level?.Value ?? "1:1:10");
                    data.m_overrideLevelupChance = levelData.Chance;
                    int num = levelData.Min;
                    float levelUpChance = SpawnSystem.GetLevelUpChance(data);
                    if (minLevelOverride >= 0) num = minLevelOverride;
                    if (Math.Abs(levelUpMultiplier - 1.0) > 0.01f) levelUpChance *= levelUpMultiplier;
                    while (num < levelData.Max && UnityEngine.Random.Range(0.0f, 100f) <= (double) levelUpChance) ++num;
                    if (num > 1)
                    {
                        gameObject.GetComponent<Character>()?.SetLevel(num);
                        if (gameObject.GetComponent<Fish>() != null)
                            gameObject.GetComponent<ItemDrop>()?.SetQuality(num);
                    }
                }
                if (component is not MonsterAI monsterAi) return false;
                if (data.configs.TOD.Value is CustomSpawnData.TimeOfDay.Night) monsterAi.SetDespawnInDay(true);
                if (!eventSpawner) return false;
                monsterAi.SetEventCreature(true);
            }
            else
            {
                if (ShipMan.m_instances.Count > 0) return false;
                spawnPoint.y = ZoneSystem.instance.m_waterLevel;
                if (!ZoneSystem.instance.IsZoneLoaded(spawnPoint)) return false;
                GameObject spawn = Object.Instantiate(data.m_prefab, spawnPoint, Quaternion.identity);
                if (Terminal.m_showTests && Terminal.m_testList.ContainsKey("spawns"))
                {
                    Terminal.Log($"Spawning {data.m_prefab.name} at {spawnPoint}");
                    Chat.instance.SendPing(spawnPoint);
                }

                if (eventSpawner)
                {
                   spawn.GetComponent<ShipMan>().EventSpawnSetAggravated();
                }
            }
            return false;
        }
    }

    public class CustomSpawnData : SpawnSystem.SpawnData
    {
        private readonly string PrefabName;
        public Heightmap.Biome Biome = Heightmap.Biome.None;
        public readonly SpawnConfigs configs;
        public readonly bool IsShip;
        private int order;
        public class SpawnConfigs
        {
            public ConfigEntry<SettlersPlugin.Toggle> Enabled = null!;
            public ConfigEntry<Heightmap.Biome> Biome = null!;
            public ConfigEntry<Heightmap.BiomeArea> Area = null!;
            public ConfigEntry<int> MaxSpawned = null!;
            public ConfigEntry<float> Interval = null!;
            public ConfigEntry<float> Chance = null!;
            public ConfigEntry<float> Distance = null!;
            public ConfigEntry<string> RequiredKey = null!;
            public ConfigEntry<string> RequiredEnvs = null!;
            public ConfigEntry<TimeOfDay> TOD = null!;
            public ConfigEntry<string>? Altitude;
            public ConfigEntry<Region>? Forest;
            public ConfigEntry<string>? Level;
        }
        private int GetOrder()
        {
            ++order;
            return order;
        }

        private void ConfigChanged(object? o, EventArgs? e)
        {
            m_enabled = configs.Enabled.Value is SettlersPlugin.Toggle.On;
            m_biome = configs.Biome.Value;
            m_biomeArea = configs.Area.Value;
            m_maxSpawned = configs.MaxSpawned.Value;
            m_spawnInterval = configs.Interval.Value;
            m_spawnChance = configs.Chance.Value;
            m_spawnDistance = configs.Distance.Value;
            m_requiredGlobalKey = configs.RequiredKey.Value;
            m_requiredEnvironments = new SerializedEnvironments(configs.RequiredEnvs.Value).GetValidatedList();
            m_spawnAtDay = configs.TOD.Value is TimeOfDay.Both or TimeOfDay.Day;
            m_spawnAtNight = configs.TOD.Value is TimeOfDay.Both or TimeOfDay.Night;
            m_minAltitude = configs.Altitude != null ? new SerializedAltitude(configs.Altitude.Value).Min : -1000f;
            m_maxAltitude = configs.Altitude != null ? new SerializedAltitude(configs.Altitude.Value).Max : 1000f;
            m_inForest = configs.Forest == null || configs.Forest.Value is Region.Both or Region.InForest;
            m_outsideForest = configs.Forest == null || configs.Forest.Value is Region.Both or Region.OutForest;
            m_minLevel = configs.Level != null ? new SerializedLevel(configs.Level.Value).Min : 1;
            m_maxLevel = configs.Level != null ? new SerializedLevel(configs.Level.Value).Max : 1;
            m_overrideLevelupChance = configs.Level != null ? new SerializedLevel(configs.Level.Value).Chance : 0f;
        }
        public void SetupConfigs(ref int otherOrder)
        {
            order = otherOrder;
            configs.Enabled = SettlersPlugin._Plugin.config(PrefabName, "Enabled", SettlersPlugin.Toggle.On, "If on, viking can spawn", GetOrder());
            configs.Enabled.SettingChanged += ConfigChanged;
            configs.Biome = SettlersPlugin._Plugin.config(PrefabName, "Biome", Biome, "Set biomes viking can spawn in", GetOrder());
            configs.Biome.SettingChanged += ConfigChanged;
            configs.Area = SettlersPlugin._Plugin.config(PrefabName, "Biome Area", Heightmap.BiomeArea.Everything, "Set particular part of biome viking can spawn in", GetOrder());
            configs.Area.SettingChanged += ConfigChanged;
            configs.MaxSpawned = SettlersPlugin._Plugin.config(PrefabName, "Max Spawned", m_maxSpawned, "Set maximum amount allowed spawned in a zone", GetOrder());
            configs.MaxSpawned.SettingChanged += ConfigChanged;
            configs.Interval = SettlersPlugin._Plugin.config(PrefabName, "Spawn Interval", m_spawnInterval, "Set how often vikings will try to spawn", GetOrder());
            configs.Interval.SettingChanged += ConfigChanged;
            configs.Chance = SettlersPlugin._Plugin.config(PrefabName, "Spawn Chance", m_spawnChance, new ConfigDescription("Set chance to spawn", new AcceptableValueRange<float>(0f, 100f)), GetOrder());
            configs.Chance.SettingChanged += ConfigChanged;
            configs.Distance = SettlersPlugin._Plugin.config(PrefabName, "Spawn Distance", m_spawnDistance, "Spawn range, 0 = use global settings", GetOrder());
            configs.Distance.SettingChanged += ConfigChanged;
            configs.RequiredKey = SettlersPlugin._Plugin.config(PrefabName, "Required Key", "", "Only spawn if this key is present", GetOrder());
            configs.RequiredKey.SettingChanged += ConfigChanged;
            configs.RequiredEnvs = SettlersPlugin._Plugin.config(PrefabName, "Required Envs",
                new SerializedEnvironments().ToString(), new ConfigDescription(
                    "List of required environments for viking to spawn", null,
                    new SettlersPlugin.ConfigurationManagerAttributes()
                    {
                        Order = GetOrder(),
                        Category = PrefabName,
                        CustomDrawer = SerializedEnvironments.DrawTable,
                    }));
            configs.RequiredEnvs.SettingChanged += ConfigChanged;
            configs.TOD = SettlersPlugin._Plugin.config(PrefabName, "Spawn Time Of Day", TimeOfDay.Both, "Set time of day requirement", GetOrder());
            configs.TOD.SettingChanged += ConfigChanged;
            if (!IsShip)
            {
                configs.Altitude = SettlersPlugin._Plugin.config(PrefabName, "Spawn Altitude",
                    new SerializedAltitude(m_minAltitude, m_maxAltitude).ToString(), new ConfigDescription("Set [min]-[max] altitude", null,
                        new SettlersPlugin.ConfigurationManagerAttributes()
                        {
                            Order = GetOrder(),
                            Category = PrefabName,
                            CustomDrawer = SerializedAltitude.DrawTable
                        }));
                configs.Altitude.SettingChanged += ConfigChanged;
                configs.Forest = SettlersPlugin._Plugin.config(PrefabName, "Spawn Region", Region.Both, "Set which region viking can spawn in", GetOrder());
                configs.Forest.SettingChanged += ConfigChanged;
                configs.Level = SettlersPlugin._Plugin.config(PrefabName, "Spawn Level",
                    new SerializedLevel(m_minLevel, m_maxLevel, m_overrideLevelupChance).ToString(), new ConfigDescription("Set [min]:[max]:[chanceToLevel]",
                        null, new SettlersPlugin.ConfigurationManagerAttributes()
                        {
                            Order = GetOrder(),
                            Category = PrefabName,
                            CustomDrawer = SerializedLevel.DrawTable
                        }));
                configs.Level.SettingChanged += ConfigChanged;
            }
            ConfigChanged(null, null);
        }
        private class SerializedEnvironments
        {
            public readonly List<string> Envs;
            public SerializedEnvironments(List<string> envs)
            {
                Envs = envs;
                if (Envs.Count == 0) Envs.Add("");
            }
            public SerializedEnvironments(string envs) => Envs = envs.Split(',').ToList();
            public SerializedEnvironments() => Envs = new List<string>() { "" };
            public void Add(string env) => Envs.Add(env);
            public List<string> GetValidatedList() => EnvMan.instance ? Envs.Where(env => EnvMan.instance.GetEnv(env) is not null).ToList() : Envs.Where(env => !env.IsNullOrWhiteSpace()).ToList();
            public override string ToString() => string.Join(",", Envs);
            public static void DrawTable(ConfigEntryBase cfg)
            {
                bool locked = cfg.Description.Tags.Select(a => a.GetType().Name == "ConfigurationManagerAttributes" ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a) : null).FirstOrDefault(v => v != null) ?? false;
                List<string> newEnvs = new();
                bool wasUpdated = false;

                GUILayout.BeginVertical();
                foreach (string env in new SerializedEnvironments((string)cfg.BoxedValue).Envs)
                {
                    GUILayout.BeginHorizontal();
                    string newEnv = GUILayout.TextField(env, new GUIStyle(GUI.skin.textField));
                    string environment = locked ? env : newEnv;
                    bool wasDeleted = GUILayout.Button("x", new GUIStyle(GUI.skin.button) { fixedWidth = 21 });
                    bool wasAdded = GUILayout.Button("+", new GUIStyle(GUI.skin.button) { fixedWidth = 21 });
                    GUILayout.EndHorizontal();
                    if (wasDeleted && !locked)
                    {
                        wasUpdated = true;
                    }
                    else
                    {
                        newEnvs.Add(environment);
                    }

                    if (wasAdded && !locked)
                    {
                        wasUpdated = true;
                        newEnvs.Add("");
                    }
                }
                GUILayout.EndVertical();
                if (wasUpdated)
                {
                    cfg.BoxedValue = new SerializedEnvironments(newEnvs).ToString();
                }
            }
        }
        public class SerializedAltitude
        {
            public readonly float Min;
            public readonly float Max;

            public SerializedAltitude(float min, float max)
            {
                Min = min;
                Max = max;
            }

            public SerializedAltitude(string altitude)
            {
                var parts = altitude.Split('-');
                Min = float.TryParse(parts[0], out float min) ? min : -1000f;
                Max = float.TryParse(parts[1], out float max) ? max : 1000f;
            }

            public static void DrawTable(ConfigEntryBase cfg)
            {
                bool locked = cfg.Description.Tags.Select(a => a.GetType().Name == "ConfigurationManagerAttributes" ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a) : null).FirstOrDefault(v => v != null) ?? false;
                var data = new SerializedAltitude((string)cfg.BoxedValue);
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Min: ");
                float newMin = !locked ? float.TryParse(GUILayout.TextField(data.Min.ToString(CultureInfo.InvariantCulture), new GUIStyle(GUI.skin.textField)), out float newMinimum) ? newMinimum : data.Min : data.Min;
                GUILayout.Label("Max: ");
                float newMax = !locked ?float.TryParse(GUILayout.TextField(data.Max.ToString(CultureInfo.InvariantCulture), new GUIStyle(GUI.skin.textField)), out float newMaximum) ? newMaximum : data.Max : data.Max;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                if (Math.Abs(newMin - data.Min) > 0.1f || Math.Abs(newMax - data.Max) > 0.1f)
                {
                    cfg.BoxedValue = new SerializedAltitude(newMin, newMax).ToString();
                }
            }

            public override string ToString() => $"{Min}-{Max}";
        }
        public class SerializedLevel
        {
            public readonly int Min;
            public readonly int Max;
            public readonly float Chance;

            public SerializedLevel(int min, int max, float chance)
            {
                Min = min;
                Max = max;
                Chance = chance;
            }

            public SerializedLevel(string level)
            {
                var parts = level.Split(':');
                Min = int.TryParse(parts[0], out int min) ? min : 1;
                Max = int.TryParse(parts[1], out int max) ? max : 1;
                Chance = float.TryParse(parts[2], out float chance) ? chance : 0.5f;
            }

            public static void DrawTable(ConfigEntryBase cfg)
            {
                bool locked = cfg.Description.Tags.Select(a => a.GetType().Name == "ConfigurationManagerAttributes" ? (bool?)a.GetType().GetField("ReadOnly")?.GetValue(a) : null).FirstOrDefault(v => v != null) ?? false;
                SerializedLevel data = new SerializedLevel((string)cfg.BoxedValue);
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Min: ");
                int newMin = !locked ? int.TryParse(GUILayout.TextField(data.Min.ToString(), new GUIStyle(GUI.skin.textField)), out int newMinimum) ? newMinimum : data.Min : data.Min;
                GUILayout.Label("Max: ");
                int newMax = !locked ?int.TryParse(GUILayout.TextField(data.Max.ToString(), new GUIStyle(GUI.skin.textField)), out int newMaximum) ? newMaximum : data.Max : data.Max;
                GUILayout.Label("LevelUp Chance: ");
                float newChance = !locked
                    ? float.TryParse(
                        GUILayout.TextField(data.Chance.ToString(CultureInfo.InvariantCulture)), out float newCha)
                            ? newCha
                            : data.Chance : data.Chance;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                if (newMin != data.Min || newMax != data.Max || Math.Abs(newChance - data.Chance) > 0.01f)
                {
                    cfg.BoxedValue = new SerializedLevel(newMin, newMax, newChance).ToString();
                }
            }
            public override string ToString() => $"{Min}:{Max}:{Chance}";
        }
        public enum TimeOfDay { Both, Night, Day}
        public enum Region {Both, InForest, OutForest}
        public CustomSpawnData(VikingManager.Viking viking)
        {
            PrefabName = viking.PrefabName;
            m_name = viking.PrefabName;
            Biome = viking.Biome;
            configs = new SpawnConfigs();
            SpawnList.enabled = true;
            m_groupSizeMin = 0;
            m_groupSizeMax = 1;
            m_levelUpMinCenterDistance = 1f;
            m_minTilt = 0f;
            m_maxTilt = 50f;
            m_groupRadius = 50f;
            SpawnList.m_spawners.Add(this);
        }
        public CustomSpawnData(GameObject prefab)
        {
            PrefabName = prefab.name;
            m_prefab = prefab;
            m_name = PrefabName;
            configs = new SpawnConfigs();
            SpawnList.enabled = true;
            m_groupSizeMin = 0;
            m_groupSizeMax = 1;
            m_levelUpMinCenterDistance = 1f;
            m_minTilt = 0f;
            m_maxTilt = 50f;
            m_groupRadius = 50f;
            IsShip = true;
            SpawnList.m_spawners.Add(this);
            
        }
    }
}