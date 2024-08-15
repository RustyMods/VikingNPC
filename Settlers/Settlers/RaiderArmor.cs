
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using YamlDotNet.Serialization;
using Random = UnityEngine.Random;

namespace Settlers.Settlers;

public static class RaiderArmor
{
    private static readonly CustomSyncedValue<string> ServerRaiderEquipment = new(SettlersPlugin.ConfigSync, "ServerRaiderEquipment", "");
    public static readonly string m_folderPath = Paths.ConfigPath + Path.DirectorySeparatorChar + "VikingNPC";
    private static readonly string m_fileName = "VikingRaiderEquipment.yml";
    private static readonly string m_filePath = m_folderPath + Path.DirectorySeparatorChar + m_fileName;

    private static Dictionary<Heightmap.Biome, RaiderEquipment> m_equipment = GetDefaultEquipment();

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class RegisterServerRaiderEquipment
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            UpdateServerRaiderEquipment();
        }
    }

    public static void Setup()
    {
        LoadRaiderArmors();
        SetupWatcher();
        LoadServerRaiderEquipmentWatcher();
    }

    private static void UpdateServerRaiderEquipment()
    {
        ISerializer serializer = new SerializerBuilder().Build();
        ServerRaiderEquipment.Value = serializer.Serialize(m_equipment);
    }

    private static void LoadServerRaiderEquipmentWatcher()
    {
        ServerRaiderEquipment.ValueChanged += () =>
        {
            if (ServerRaiderEquipment.Value.IsNullOrWhiteSpace()) return;
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                m_equipment = deserializer.Deserialize<Dictionary<Heightmap.Biome, RaiderEquipment>>(ServerRaiderEquipment.Value);
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse server raider equipment data");
            }
        };
    }

    public static GameObject[]? GetRaiderEquipment(Heightmap.Biome biome, bool isElf, bool isSailor)
    {
        if (!ZNetScene.instance) return null;
        List<GameObject> result = new();
        var sailorBiome = Heightmap.Biome.BlackForest;
        if (isSailor)
        {
            Array biomes = Enum.GetValues(typeof(Heightmap.Biome));
            var random = Random.Range(1, biomes.Length - 1);
            sailorBiome = (Heightmap.Biome)biomes.GetValue(random);
            if (sailorBiome is Heightmap.Biome.None or Heightmap.Biome.All) sailorBiome = Heightmap.Biome.BlackForest;
        }
        RaiderEquipment data = GetEquipment(isSailor ? sailorBiome : biome);
        if (data.Armors.Count > 0)
        {
            var armor = data.Armors[Random.Range(0, data.Armors.Count)];
            result.AddRange(GetPrefabs(armor));
        }

        if (data.Capes.Count > 0)
        {
            var cape = data.Capes[Random.Range(0, data.Capes.Count)];
            var capePrefab = ZNetScene.instance.GetPrefab(cape);
            if (capePrefab) result.Add(capePrefab);
        }

        if (data.Melee.Count > 0)
        {
            var melee = data.Melee[Random.Range(0, data.Melee.Count)];
            var meleePrefab = ZNetScene.instance.GetPrefab(melee);
            if (meleePrefab) result.Add(meleePrefab);
        }

        if (data.Ranged.Count > 0)
        {
            var range = data.Ranged[Random.Range(0, data.Ranged.Count)];
            var rangePrefab = ZNetScene.instance.GetPrefab(range);
            if (rangePrefab) result.Add(rangePrefab);
        }

        if (data.Utility.Count > 0)
        {
            var utility = data.Utility[Random.Range(0, data.Utility.Count)];
            var utilityPrefab = ZNetScene.instance.GetPrefab(utility);
            if (utilityPrefab) result.Add(utilityPrefab);
        }
        if (data.Misc.Count > 0) result.AddRange(GetPrefabs(data.Misc));
        if (data.Shields.Count > 0) result.AddRange(GetPrefabs(data.Shields));
        if (isElf) result.Add(ZNetScene.instance.GetPrefab("ElvenEars"));
        if (isSailor) result.Add(ZNetScene.instance.GetPrefab("SpearChitin"));
        return result.ToArray();
    }

    private static List<GameObject> GetPrefabs(List<string> names) => names.Select(prefabName => ZNetScene.instance.GetPrefab(prefabName)).Where(prefab => prefab).ToList();
    
    private static RaiderEquipment GetEquipment(Heightmap.Biome biome)
    {
        return m_equipment.TryGetValue(biome, out RaiderEquipment equipment) ? equipment : GetFallBackEquipment();
    }
    private static RaiderEquipment GetFallBackEquipment()
    {
        return new RaiderEquipment()
        {
            Armors = new() { new() { "HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs" } },
            Capes = new() { "CapeDeerHide" },
            Melee = new() { "KnifeFlint", "SpearFlint", "AxeFlint" },
            Ranged = new() { "Bow" },
            Shields = new() { "ShieldWoodTower", "ShieldWood" },
            Misc = new() { "Torch" }
        };
    }

    private static void LoadRaiderArmors()
    {
        if (!Directory.Exists(m_folderPath)) Directory.CreateDirectory(m_folderPath);
        if (!File.Exists(m_filePath))
        {
            var serializer = new SerializerBuilder().Build();
            File.WriteAllText(m_filePath, serializer.Serialize(m_equipment));
        }
        else
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                m_equipment = deserializer.Deserialize<Dictionary<Heightmap.Biome, RaiderEquipment>>(File.ReadAllText(m_filePath));
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse file: " + Path.GetFileName(m_filePath));
            }
        }

        if (ZNet.instance && ZNet.instance.IsServer())
        {
            UpdateServerRaiderEquipment();
        }
    }

    private static void SetupWatcher()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(m_folderPath, m_fileName);
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
        watcher.Changed += OnFileChange;
        watcher.Deleted += OnFileChange;
        watcher.Created += OnFileChange;
    }

    private static void OnFileChange(object sender, FileSystemEventArgs args)
    {
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        SettlersPlugin.SettlersLogger.LogDebug("Viking Raider Equipment Settings Changed");
        LoadRaiderArmors();
    }

    private static Dictionary<Heightmap.Biome, RaiderEquipment> GetDefaultEquipment()
    {
        return new Dictionary<Heightmap.Biome, RaiderEquipment>()
        {
            [Heightmap.Biome.Meadows] = new RaiderEquipment()
            {
                Armors = new() { new() { "HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs" } },
                Capes = new() { "CapeDeerHide" },
                Melee = new() { "KnifeFlint", "SpearFlint", "AxeFlint" },
                Ranged = new() { "Bow" },
                Shields = new() { "ShieldWoodTower", "ShieldWood" },
                Misc = new() { "Torch" }
            },
            [Heightmap.Biome.BlackForest] = new RaiderEquipment()
            {
                Armors = new()
                {
                    new() { "HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs" },
                    new() { "HelmetTrollLeather", "ArmorTrollLeatherChest", "ArmorTrollLeatherLegs" }
                },
                Melee = new() { "AtgeirBronze", "SwordBronze", "KnifeCopper", "MaceBronze" },
                Shields = new() { "ShieldBronzeBuckler", "ShieldBoneTower" },
                Ranged = new() { "BowFineWood" },
                Capes = new() { "CapeTrollHide" }
            },
            [Heightmap.Biome.Swamp] = new RaiderEquipment()
            {
                Armors = new()
                {
                    new() { "HelmetIron", "ArmorIronChest", "ArmorIronLegs" },
                    new() { "HelmetRoot", "ArmorRootChest", "ArmorRootLegs" }
                },
                Melee = new() { "SledgeIron", "SwordIron", "MaceIron", "Battleaxe" },
                Shields = new() { "ShieldBronzeBuckler", "ShieldBoneTower" },
                Ranged = new() { "BowHuntsman" },
                Capes = new() { "CapeTrollHide" },
            },
            [Heightmap.Biome.Mountain] = new RaiderEquipment()
            {
                Armors = new()
                {
                    new() { "HelmetDrake", "ArmorWolfChest", "ArmorWolfLegs" },
                    new() { "HelmetFenring", "ArmorFenringChest", "ArmorFenringLegs" }
                },
                Melee = new() { "SwordSilver", "MaceSilver", "FistFenrirClaw", "BattleaxeCrystal" },
                Shields = new() { "ShieldSilver", "ShieldSerpentscale" },
                Ranged = new() { "BowDraugrFang" },
                Capes = new() { "CapeWolf" },
            },
            [Heightmap.Biome.Plains] = new RaiderEquipment()
            {
                Armors = new() { new() { "HelmetPadded", "ArmorPaddedCuirass", "ArmorPaddedGreaves" } },
                Capes = new() { "CapeLox", "CapeLinen" },
                Melee = new() { "SwordBlackmetal", "KnifeBlackmetal", "AtgeirBlackmetal", "AxeBlackMetal", "MaceNeedle" },
                Shields = new() { "ShieldBlackmetal", "ShieldBlackmetalTower" },
                Ranged = new() { "BowDraugrFang" },
            },
            [Heightmap.Biome.Mistlands] = new RaiderEquipment()
            {
                Armors = new()
                {
                    new() { "HelmetCarapace", "ArmorCarapaceChest", "ArmorCarapaceLegs" },
                    new() { "HelmetMage", "ArmorMageChest", "ArmorMageLegs" }
                },
                Melee = new() { "SwordMistwalker", "AtgeirHimminAfl", "SledgeDemolisher", "KnifeSkollAndHati", "THSwordKrom" },
                Ranged = new() { "BowSpineSnap", "StaffFireball", "CrossbowArbalest", "StaffShield" },
                Shields = new() { "ShieldCarapaceBuckler", "ShieldCarapace" },
                Capes = new() { "CapeFeather" },
                Utility = new() { "Demister" }
            },
            [Heightmap.Biome.AshLands] = new RaiderEquipment()
            {
                Armors = new()
                {
                    new() { "HelmetFlametal", "ArmorFlametalChest", "ArmorFlametalLegs" },
                    new() { "HelmetMage_Ashlands", "ArmorMageChest_Ashlands", "ArmorMageLegs_Ashlands" },
                    new() { "HelmetAshlandsMediumHood", "ArmorAshlandsMediumChest", "ArmorAshlandsMediumlegs" }
                },
                Capes = new() { "CapeAsh", "CapeAskvin" },
                Melee = new(){"AxeBerzerkr", "THSwordSlayer"},
                Ranged = new(){"StaffClusterbomb", "StaffLightning", "StaffGreenRoots", "BowAshlands"},
                Shields = new(){"ShieldFlametal", "ShieldFlametalTower"},
            },
            [Heightmap.Biome.DeepNorth] = new RaiderEquipment()
            {
                Armors = new()
                {
                    new() { "HelmetFlametal", "ArmorFlametalChest", "ArmorFlametalLegs" },
                    new() { "HelmetMage_Ashlands", "ArmorMageChest_Ashlands", "ArmorMageLegs_Ashlands" },
                    new() { "HelmetAshlandsMediumHood", "ArmorAshlandsMediumChest", "ArmorAshlandsMediumlegs" }
                },
                Capes = new() { "CapeAsh", "CapeAskvin" },
                Melee = new(){"AxeBerzerkr", "THSwordSlayer"},
                Ranged = new(){"StaffClusterbomb", "StaffLightning", "StaffGreenRoots", "BowAshlands"},
                Shields = new(){"ShieldFlametal", "ShieldFlametalTower"},
            }
        };
    }
    
    [Serializable]
    public class RaiderEquipment
    {
        public List<List<string>> Armors = new();
        public List<string> Capes = new();
        public List<string> Melee = new();
        public List<string> Ranged = new();
        public List<string> Utility = new();
        public List<string> Shields = new();
        public List<string> Misc = new();
    }
    
}