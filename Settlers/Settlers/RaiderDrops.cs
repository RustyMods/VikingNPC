using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Settlers.Settlers;

public static class  RaiderDrops
{
    private static readonly CustomSyncedValue<string> ServerRaiderDrops = new(SettlersPlugin.ConfigSync, "ServerRaiderDrops", "");
    private static readonly string m_fileName = "VikingRaiderDrops.yml";
    private static readonly string m_filePath = MyPaths.GetFolderPath() + Path.DirectorySeparatorChar + m_fileName;

    private static Dictionary<Heightmap.Biome, List<Data>> m_raiderDrops = GetDefaultDrops();

    private static readonly Dictionary<Heightmap.Biome, List<CharacterDrop.Drop>> m_cachedDrops = new();
    
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class RegisterRaiderCharacterDrops
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            UpdateServerRaiderDrops();
        }
    }

    public static List<CharacterDrop.Drop> GetRaiderDrops(Heightmap.Biome biome)
    {
        if (m_cachedDrops.TryGetValue(biome, out List<CharacterDrop.Drop> drops)) return drops;
        if (!m_raiderDrops.TryGetValue(biome, out List<Data> data)) data = GetFallBackDrops();
        List<CharacterDrop.Drop> result = new();
        foreach (Data? drop in data)
        {
            GameObject? prefab = ZNetScene.instance.GetPrefab(drop.ItemName);
            if (!prefab) continue;
            result.Add(new CharacterDrop.Drop()
            {
                m_prefab = prefab,
                m_chance = drop.Chance,
                m_amountMin = drop.Minimum,
                m_amountMax = drop.Maximum,
                m_levelMultiplier = drop.LevelMultiplier,
            });
        }

        m_cachedDrops[biome] = result;
        return result;
    }

    public static void Setup()
    {
        LoadRaiderDrops();
        SetupRaiderDropWatcher();
        LoadServerRaiderDropWatcher();
    }

    private static void LoadRaiderDrops()
    {
        if (!Directory.Exists(MyPaths.GetFolderPath())) Directory.CreateDirectory(MyPaths.GetFolderPath());
        var serializer = new SerializerBuilder().Build();
        if (!File.Exists(m_filePath))
        {
            File.WriteAllText(m_filePath, serializer.Serialize(m_raiderDrops));
        }
        else
        {
            try
            {
                IDeserializer deserializer = new DeserializerBuilder().Build();
                Dictionary<Heightmap.Biome, List<Data>> data = deserializer.Deserialize<Dictionary<Heightmap.Biome, List<Data>>>(File.ReadAllText(m_filePath));
                if (data.Count < m_raiderDrops.Count)
                {
                    foreach (var kvp in m_raiderDrops)
                    {
                        if (data.ContainsKey(kvp.Key)) continue;
                        data[kvp.Key] = kvp.Value;
                    }

                    File.WriteAllText(m_filePath, serializer.Serialize(data));
                }

                m_raiderDrops = data;
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse file: " + Path.GetFileName(m_filePath));
            }
        }
    }

    private static void LoadServerRaiderDropWatcher()
    {
        ServerRaiderDrops.ValueChanged += () =>
        {
            if (ServerRaiderDrops.Value.IsNullOrWhiteSpace()) return;
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                m_raiderDrops =
                    deserializer.Deserialize<Dictionary<Heightmap.Biome, List<Data>>>(ServerRaiderDrops.Value);
                m_cachedDrops.Clear();
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse server raider drop data");
            }
        };
    }

    private static void UpdateServerRaiderDrops()
    {
        ISerializer serializer = new SerializerBuilder().Build();
        ServerRaiderDrops.Value = serializer.Serialize(m_raiderDrops);
    }

    private static void SetupRaiderDropWatcher()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(MyPaths.GetFolderPath(), m_fileName);
        watcher.EnableRaisingEvents = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.Created += OnFileChange;
        watcher.Changed += OnFileChange;
        watcher.Deleted += OnFileChange;
    }

    private static void OnFileChange(object sender, FileSystemEventArgs args)
    {
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        SettlersPlugin.SettlersLogger.LogDebug("Raider Drops file changed");
        LoadRaiderDrops();
        m_cachedDrops.Clear();
    }

    private static List<Data> GetFallBackDrops()
    {
        return new List<Data>()
        {
            CreateData("Wood", 1, 2, 1f, true),
            CreateData("LeatherScraps", 1, 2, 1f, true),
            CreateData("ArrowWood", 1, 2, 1f, true),
            CreateData("CookedMeat", 1, 2, 0.5f),
            CreateData("CookedDeerMeat", 1, 2, 0.5f),
            CreateData("Flint", 1, 2, 0.75f),
            CreateData("DeerHide", 1, 2, 0.25f),
            CreateData("NeckTailGrilled", 1, 2, 0.5f)
        };
    }

    private static Dictionary<Heightmap.Biome, List<Data>> GetDefaultDrops()
    {
        return new()
        {
            [Heightmap.Biome.Meadows] = new List<Data>()
            {
                CreateData("Wood", 1, 2, 1f, true),
                CreateData("LeatherScraps", 1, 2, 1f, true),
                CreateData("ArrowWood", 1, 2, 1f, true),
                CreateData("CookedMeat", 1, 2, 0.5f),
                CreateData("CookedDeerMeat", 1, 2, 0.5f),
                CreateData("Flint", 1, 2, 0.75f),
                CreateData("DeerHide", 1, 2, 0.25f),
                CreateData("NeckTailGrilled", 1, 2, 0.5f)
            },
            [Heightmap.Biome.BlackForest] = new List<Data>()
            {
                CreateData("CoreWood", 1, 2, 0.5f, true),
                CreateData("Wood", 1, 2, 1f, true),
                CreateData("BoneFragments", 1, 2, 0.5f),
                CreateData("Thistle", 1, 2, 0.5f),
                CreateData("Coal", 1, 2, 0.75f, true),
                CreateData("ArrowFlint", 1, 2, 0.5f, true),
                CreateData("SurtlingCore", 1, 1, 0.25f),
                CreateData("TinOre", 1, 2, 0.25f),
                CreateData("CopperOre", 1, 2, 0.25f),
                CreateData("DeerStew", 1, 2, 0.5f),
                CreateData("BoarJerky", 1, 2, 0.5f),
                CreateData("CarrotSoup", 1, 2, 0.25f),
                CreateData("QueensJam", 1, 2, 0.35f),
                CreateData("MinceMeatSauce", 1, 2, 0.3f),
                CreateData("CookedEgg", 1, 2, 0.1f),
                CreateData("MeadHealthMinor", 1, 2, 0.25f),
                CreateData("MeadStaminaMinor", 1, 2, 0.25f)
            },
            [Heightmap.Biome.Swamp] = new List<Data>()
            {
                CreateData("ElderBark", 1, 2, 0.5f),
                CreateData("Wood", 1, 2, 1f, true),
                CreateData("Entrails", 1, 2, 1f, true),
                CreateData("ArrowIron", 1, 2, 1f, true),
                CreateData("IronScrap", 1, 2, 0.5f),
                CreateData("Guck", 1, 2, 0.5f),
                CreateData("ShocklateSmoothie", 1, 2, 0.25f),
                CreateData("TurnipStew", 1, 2, 0.5f),
                CreateData("Sausages", 1, 2, 0.25f),
                CreateData("FishCooked", 1, 2, 0.25f),
                CreateData("BlackSoup", 1, 2, 0.25f)
            },
            [Heightmap.Biome.Mountain] = new List<Data>()
            {
                CreateData("WolfPelt", 1, 2, 0.5f),
                CreateData("Crystal", 1, 2, 0.5f),
                CreateData("Obsidian", 1, 2, 0.75f),
                CreateData("ArrowObsidian", 1, 2, 1f, true),
                CreateData("SilverOre", 1, 2, 0.5f),
                CreateData("WolfClaw", 1, 2, 0.5f),
                CreateData("WolfHairBundle", 1, 2, 0.25f),
                CreateData("SerpentMeatCooked", 1, 2, 0.5f),
                CreateData("OnionSoup", 1 , 2, 0.5f),
                CreateData("WolfJerky", 1 , 2, 0.5f),
                CreateData("WolfMeatSkewer", 1 , 2, 0.5f),
                CreateData("Eyescream", 1, 2, 0.5f)
            },
            [Heightmap.Biome.Plains] = new List<Data>()
            {
                CreateData("Barley", 1, 2, 0.5f, true),
                CreateData("Flax", 1, 2, 0.5f, true),
                CreateData("Needle", 1, 2, 0.5f, true),
                CreateData("ArrowNeedle", 1, 2, 1f, true),
                CreateData("BlackMetalScrap", 1, 2, 0.5f),
                CreateData("GoblinTotem", 1, 1, 0.1f),
                CreateData("CookedLoxMeat", 1, 2, 0.5f),
                CreateData("FishWraps", 1, 2, 0.25f),
                CreateData("LoxPie", 1, 2, 0.25f),
                CreateData("BloodPudding", 1, 2, 0.5f),
                CreateData("Bread", 1, 2, 0.5f)
            },
            [Heightmap.Biome.Mistlands] = new List<Data>()
            {
                CreateData("MagicallyStuffedShroom", 1, 2, 0.5f),
                CreateData("YggdrasilPorridge", 1, 2, 0.5f),
                CreateData("SeekerAspic", 1, 2, 0.5f),
                CreateData("BlackCore", 1, 1, 0.1f),
                CreateData("SoftTissue", 1, 2, 0.25f, true),
                CreateData("ChickenEgg", 1, 1, 0.1f),
                CreateData("CookedBugMeat", 1, 2, 0.5f),
                CreateData("CookedHareMeat", 1, 2, 0.5f),
                CreateData("Meatplatter", 1, 2, 0.5f),
                CreateData("HoneyGlazedChicken", 1, 2, 0.5f),
                CreateData("Mistharesupreme", 1, 2, 0.5f),
                CreateData("Salad", 1, 2, 0.5f),
                CreateData("MushroomOmelette", 1, 2, 0.5f)
            },
            [Heightmap.Biome.AshLands] = new List<Data>()
            {
                CreateData("FlametalOreNew", 1, 2, 0.1f),
                CreateData("CookedAskvinMeat", 1, 2, 0.5f),
                CreateData("CookedVoltureMeat", 1, 2, 0.5f),
                CreateData("MashedMeat", 1, 2, 0.5f),
                CreateData("PiquantPie", 1, 2, 0.5f),
                CreateData("SpicyMarmalade", 1, 2, 0.5f),
                CreateData("ScorchingMedley", 1, 2, 0.5f),
                CreateData("SparklingShroomshake", 1, 2, 0.5f),
                CreateData("MarinatedGreens", 1, 2, 0.5f),
                CreateData("CharredBone", 1, 2, 0.5f, true),
                CreateData("blackwood", 1, 2, 1f, true),
                CreateData("SulfureStone", 1, 2, 0.5f),
                CreateData("ArrowCharred", 1, 2, 1f, true)
            }
        };
    }

    private static Data CreateData(string itemName, int min, int max, float chance, bool levelMultiply = false)
    {
        return new Data
        {
            ItemName = itemName, Minimum = min, Maximum = max, Chance = chance, LevelMultiplier = levelMultiply
        };
    }

    [Serializable]
    public class Data
    {
        public string ItemName = "";
        public int Minimum = 1;
        public int Maximum = 1;
        public float Chance = 1f;
        public bool LevelMultiplier = false;
    }
}