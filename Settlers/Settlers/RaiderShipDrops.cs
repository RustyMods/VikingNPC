using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using ServerSync;
using YamlDotNet.Serialization;

namespace Settlers.Settlers;

public static class RaiderShipDrops
{
    private static readonly CustomSyncedValue<string> ServerRaiderShipLoot = new(SettlersPlugin.ConfigSync, "ServerRaiderShipLoot", "");
    private static readonly string m_fileName = "VikingRaiderShipLoot.yml";
    private static readonly string m_filePath = RaiderArmor.m_folderPath + Path.DirectorySeparatorChar + m_fileName;
    private static Dictionary<string, List<RaiderShipDrop>> m_keyDrops = GetDefaultDrops();

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class RegisterServerRaiderShipLoot
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            UpdateServerRaiderShipLoot();
        }
    }

    public static void Setup()
    {
        LoadRaiderShipLoot();
        SetupWatcher();
        LoadServerRaiderShipLootWatcher();
    }

    private static void SetupWatcher()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(RaiderArmor.m_folderPath, m_fileName);
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
        watcher.Changed += OnFileChange;
        watcher.Deleted += OnFileChange;
        watcher.Created += OnFileChange;
    }

    private static void OnFileChange(object sender, FileSystemEventArgs args)
    {
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        SettlersPlugin.SettlersLogger.LogDebug("Viking Raider ship Loot Settings Changed");
        LoadRaiderShipLoot();
    }
    
    private static void LoadServerRaiderShipLootWatcher()
    {
        ServerRaiderShipLoot.ValueChanged += () =>
        {
            if (ServerRaiderShipLoot.Value.IsNullOrWhiteSpace()) return;
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                m_keyDrops =
                    deserializer.Deserialize<Dictionary<string, List<RaiderShipDrop>>>(ServerRaiderShipLoot.Value);
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse server raider ship loot");
            }
        };
    }

    private static void LoadRaiderShipLoot()
    {
        if (!Directory.Exists(RaiderArmor.m_folderPath)) Directory.CreateDirectory(RaiderArmor.m_folderPath);
        if (!File.Exists(m_filePath))
        {
            var serializer = new SerializerBuilder().Build();
            File.WriteAllText(m_filePath, serializer.Serialize(m_keyDrops));
        }
        else
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                m_keyDrops = deserializer.Deserialize<Dictionary<string, List<RaiderShipDrop>>>(File.ReadAllText(m_filePath));
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse file: " + m_fileName);
            }
        }

        if (ZNet.instance && ZNet.instance.IsServer())
        {
            UpdateServerRaiderShipLoot();
        }
    }

    private static void UpdateServerRaiderShipLoot()
    {
        var serializer = new SerializerBuilder().Build();
        ServerRaiderShipLoot.Value = serializer.Serialize(m_keyDrops);
    }
    

    public static List<DropTable.DropData> GetRaiderShipLoot()
    {
        if (!Player.m_localPlayer || !ZoneSystem.instance || !ObjectDB.instance) return new();
        var keys = ZoneSystem.instance.GetGlobalKeys();
        var tempKeys = Player.m_localPlayer.GetUniqueKeys();
        foreach (var key in tempKeys.Where(key => !keys.Contains(key)))
        {
            keys.Add(key);
        }

        List<DropTable.DropData> output = new();
        foreach (var key in keys)
        {
            if (!m_keyDrops.TryGetValue(key, out List<RaiderShipDrop> drops)) continue;
            foreach (var data in drops)
            {
                var prefab = ObjectDB.instance.GetItemPrefab(data.m_itemName);
                if (!prefab) continue;
                output.Add(new DropTable.DropData()
                {
                    m_item = prefab,
                    m_weight = data.m_weight,
                    m_stackMin = data.m_minStack,
                    m_stackMax = data.m_maxStack
                });
            }
        }
        return output;
    }

    private static Dictionary<string, List<RaiderShipDrop>> GetDefaultDrops()
    {
        var output = new Dictionary<string, List<RaiderShipDrop>>();

        output["defeated_eikthyr"] = new List<RaiderShipDrop>()
        {
            Create("BoneFragments", 1, 10),
            Create("Coins", 25, 50),
            Create("Coins", 25, 50),
            Create("SurtlingCore", 5, 10),
            Create("MeadStaminaMinor", 5, 10)
        };
        output["defeated_gdking"] = new List<RaiderShipDrop>()
        {
            Create("Sausages", 1, 10),
            Create("CoreWood", 10, 50),
            Create("TinOre", 10, 20),
            Create("CopperOre", 10, 20)
        };
        output["defeated_bonemass"] = new List<RaiderShipDrop>()
        {
            Create("IronScrap", 10, 20),
            Create("IronScrap", 10, 20),
            Create("Onion", 10, 20),
            Create("MeadHealthMinor", 10, 20),
            Create("ArrowIron", 50, 100)
        };
        output["defeated_dragon"] = new List<RaiderShipDrop>()
        {
            Create("SilverOre", 10, 20),
            Create("SilverOre", 10, 20),
            Create("LoxMeatPie", 5, 10),
            Create("Bread", 10, 20),
            Create("IronScrap", 10, 20)
        };
        output["defeated_goblinking"] = new List<RaiderShipDrop>()
        {
            Create("BlackMetalScrap", 10, 20),
            Create("BlackMetalScrap", 10, 20),
            Create("MeadEitrMinor", 5, 10),
            Create("OozeBomb", 10, 20)
        };
        output["defeated_queen"] = new List<RaiderShipDrop>()
        {
            Create("SoftTissue", 10, 20),
            Create("BlackCore", 1, 5),
            Create("Eitr", 10, 20)
        };
        output["defeated_fader"] = new List<RaiderShipDrop>()
        {
            Create("IronScrap", 10, 20)
        };
        
        return output;
    }

    private static RaiderShipDrop Create(string itemName, int min, int max, float weight = 1f)
    {
        return new()
        {
            m_itemName = itemName, m_minStack = min, m_maxStack = max, m_weight = weight
        };
    }

    private class RaiderShipDrop
    {
        public string m_itemName = "";
        public float m_weight = 1f;
        public int m_minStack = 1;
        public int m_maxStack = 1;
    }
}