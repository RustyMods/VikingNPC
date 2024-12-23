using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using ServerSync;
using Settlers.Settlers;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Settlers.ExtraConfigs;

public static class SettlerGear
{
    private static readonly CustomSyncedValue<List<string>> ServerSettlerGear = new(SettlersPlugin.ConfigSync, "ServerSettlerGear", new());
    private static readonly string m_fileName = "VikingSettlerGear.yml";
    private static readonly string m_filePath = MyPaths.GetFolderPath() + Path.DirectorySeparatorChar + m_fileName;

    private static List<string> m_settlerStartGear = GetDefaultStartGear();
    private static GameObject[]? m_cachedSettlerGear;

    private static List<string> GetDefaultStartGear()
    {
        return new List<string>()
        {
            "AxeStone", "ShieldWood", "Torch", "ArmorRagsChest", "ArmorRagsLegs"
        };
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class RegisterSettlerGear
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance.IsServer()) return;
            UpdateServerSettlerGear();
        }
    }

    public static void Setup()
    {
        LoadFiles();
        LoadServerGearChange();
        SetupFileWatcher();
    }

    private static void LoadFiles()
    {
        if (!Directory.Exists(MyPaths.GetFolderPath())) Directory.CreateDirectory(MyPaths.GetFolderPath());
        var serializer = new SerializerBuilder().Build();
        if (!File.Exists(m_filePath))
        {
            File.WriteAllText(m_filePath, serializer.Serialize(m_settlerStartGear));
        }
        else
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                m_settlerStartGear = deserializer.Deserialize<List<string>>(File.ReadAllText(m_filePath));
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse file: " + m_fileName);
            }
        }
        
        if (ZNet.instance && ZNet.instance.IsServer())
        {
            UpdateServerSettlerGear();
        }
    }

    public static GameObject[] GetSettlerGear()
    {
        if (m_cachedSettlerGear != null) return m_cachedSettlerGear;
        List<GameObject> prefabs = new();
        foreach (string itemName in m_settlerStartGear)
        {
            if (ZNetScene.instance.GetPrefab(itemName) is {} prefab) prefabs.Add(prefab);
        }

        m_cachedSettlerGear = prefabs.ToArray();
        return m_cachedSettlerGear;
    }

    private static void LoadServerGearChange()
    {
        ServerSettlerGear.ValueChanged += () =>
        {
            if (ServerSettlerGear.Value.Count <= 0) return;
            m_settlerStartGear = ServerSettlerGear.Value;
            m_cachedSettlerGear = null;
        };
    }

    private static void SetupFileWatcher()
    {
        FileSystemWatcher watcher = new FileSystemWatcher(MyPaths.GetFolderPath(), m_fileName);
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
        watcher.Changed += OnFileChange;
        watcher.Deleted += OnFileChange;
        watcher.Created += OnFileChange;
    }

    private static void OnFileChange(object sender, FileSystemEventArgs args)
    {
        if (!ZNet.instance || !ZNet.instance.IsServer()) return;
        SettlersPlugin.SettlersLogger.LogDebug("Viking Settler Gear Settings Changed");
        LoadFiles();
    }
    
    private static void UpdateServerSettlerGear() => ServerSettlerGear.Value = m_settlerStartGear;
    
}