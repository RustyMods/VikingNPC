using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using Settlers.Managers;
using Settlers.Settlers;
using UnityEngine;

namespace Settlers
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class SettlersPlugin : BaseUnityPlugin
    {
        internal const string ModName = "Settlers";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource SettlersLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        
        public static SettlersPlugin _Plugin = null!;
        public static GameObject _Root = null!;
        public static AssetBundle _assetBundle = GetAssetBundle("settlerbundle");
        public static Sprite m_settlerPin = null!;
        public enum Toggle { On = 1, Off = 0 }
        
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> _SettlersCanLumber = null!;
        public static ConfigEntry<Toggle> _SettlerCanFish = null!;
        public static ConfigEntry<Toggle> _SettlersCanMine = null!;
        public static ConfigEntry<int> _strikesUntilTired = null!;
        public static ConfigEntry<Toggle> _autoPickup = null!;
        public static ConfigEntry<string> _firstNames = null!;
        public static ConfigEntry<string> _lastNames = null!;
        public static ConfigEntry<float> _baseMaxCarryWeight = null!;
        public static ConfigEntry<KeyCode> _makeAllFollowKey = null!;
        private static ConfigEntry<KeyCode> _makeAllUnfollowKey = null!;
        public static ConfigEntry<Toggle> _spawnRaiders = null!;
        public static ConfigEntry<float> _raiderDropChance = null!;
        public static ConfigEntry<Character.Faction> _raiderFaction = null!;
        public static ConfigEntry<float> _raiderBaseHealth = null!;
        public static ConfigEntry<Toggle> _addMinimapPin = null!;
        public static ConfigEntry<float> _settlerTamingTime = null!;
        public static ConfigEntry<Toggle> _ownerLock = null!;
        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            _SettlersCanLumber = config("Behavior Settings", "Can Lumber", Toggle.On,
                "If on, settlers will lumber trees, logs and stumps, if has axe");
            _SettlersCanMine = config("Behavior Settings", "Can Mine", Toggle.On,
                "If on, settlers will mine any rocks that drop ore or scraps if has pickaxe");
            _SettlerCanFish = config("Behavior Settings", "Can Fish", Toggle.On,
                "If on, settlers will fish if has rod and bait");
            _strikesUntilTired = config("Behavior Settings", "Strikes Until Tired", 50,
                "Set how many lumber or mining strikes until settler is tired");
            _autoPickup = config("Behavior Settings", "Pickup Items", Toggle.On,
                "If on, settler picks up items around him");
            _settlerTamingTime = config("2 - Settings", "Tame Duration", 1800f,
                "Set amount of time required to tame settler");
            _baseMaxCarryWeight = config("Behavior Settings", "Base Carry Weight", 300f,
                "Set base carry weight for settlers");
            _makeAllFollowKey = config("2 - Settings", "Make All Follow Key", KeyCode.None,
                "Set the key that will make all tamed settlers follow you, if they aren't following");
            _makeAllUnfollowKey = config("2 - Settings", "Make All Unfollow Key", KeyCode.None,
                "Set the key that will make all tamed settlers unfollow, if they are following");
            _spawnRaiders = config("2 - Settings", "Raiders", Toggle.Off, "If on, raiders have taken over the world");
            _raiderDropChance = config("2 - Settings", "Raider Item Drop Chance", 0.2f,
                new ConfigDescription("Set chance to drop items", new AcceptableValueRange<float>(0f, 1f)));
            _raiderFaction = config("2 - Settings", "Raider Faction", Character.Faction.SeaMonsters,
                "Set raider faction");
            _raiderBaseHealth = config("2 - Settings", "Raider Base Health", 75f,
                "Set raider base health, multiplied by level");
            _addMinimapPin = config("2 - Settings", "Add Pin", Toggle.On,
                "If on, when settler is following a pin will be added on the minimap to track them");
            _ownerLock = config("2 - Settings", "Inventory Locked", Toggle.On,
                "If on, only owner can access settler inventory");
            var m_firstNames = new List<string>()
            {
                "Bjorn", "Harald", "Bo", "Frode", 
                "Birger", "Arne", "Erik", "Kare", 
                "Loki", "Thor", "Odin", "Ragnar", 
                "Sigurd", "Ivar", "Gunnar", "Sven",
                "Hakon", "Leif", "Magnus", "Rolf", 
                "Ulf", "Vidar", "Ingvar", "Gudrun",
                "Hilda", "Ingrid", "Freya", "Astrid", 
                "Sigrid", "Thora", "Runa", "Ylva"
            };
            var m_lastNames = new List<string>()
            {
                "Ironside", "Fairhair", "Thunderfist", "Bloodaxe",
                "Longsword", "Ravenheart", "Dragonslayer", "Stormborn",
                "Shadowblade", "Thunderstruck", "Allfather", "Lothbrok",
                "Snake-in-the-Eye", "the Boneless", "Ironhand", "Forkbeard",
                "the Good", "the Lucky", "the Strong", "the Walker",
                "Ironbeard", "the Silent", "the Fearless", "Shieldmaiden",
                "Bloodfury", "Snowdrift", "Wildheart", "Battleborn",
                "Stormshield", "Frosthammer", "Moonshadow", "Wolfsbane"
            };
            _firstNames = config("Names", "First", string.Join(":", m_firstNames), "List of first names seperated by :");
            _lastNames = config("Names", "Last", string.Join(":", m_lastNames), "List of last names seperated by :");
        }

        public void Awake()
        {
            m_settlerPin = _assetBundle.LoadAsset<Sprite>("mapicon_settler_32");
            _Plugin = this;
            _Root = new GameObject("root");
            DontDestroyOnLoad(_Root);
            _Root.SetActive(false);
            InitConfigs();
            Localizer.Load();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        public void Update()
        {
            if (!Player.m_localPlayer) return;
            if (Input.GetKeyDown(_makeAllFollowKey.Value))
            {
                Companion.MakeAllFollow(Player.m_localPlayer, 30f, true);
            }

            if (Input.GetKeyDown(_makeAllUnfollowKey.Value))
            {
                Companion.MakeAllFollow(Player.m_localPlayer, 30f, false);
            }
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                SettlersLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                SettlersLogger.LogError($"There was an issue loading your {ConfigFileName}");
                SettlersLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        
        private static AssetBundle GetAssetBundle(string fileName)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
            using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }
        
        public ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        public ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }
    }
}