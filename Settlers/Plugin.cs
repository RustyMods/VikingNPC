using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BlueprintLocations.Managers;
using HarmonyLib;
using ServerSync;
using Settlers.Behaviors;
using Settlers.Managers;
using Settlers.Settlers;
using UnityEngine;

namespace Settlers
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class SettlersPlugin : BaseUnityPlugin
    {
        internal const string ModName = "VikingNPC";
        internal const string ModVersion = "0.1.5";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource SettlersLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        public static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        
        private static readonly AssetBundle _assetBundle = GetAssetBundle("settlerbundle");
        public static readonly AssetBundle _locationBundle = GetAssetBundle("blueprintlocationbundle");
        public static readonly AssetBundle _elfBundle = GetAssetBundle("elfassets");
        public static readonly AssetBundle _oarsBundle = GetAssetBundle("oarsbundle");
        
        public static SettlersPlugin _Plugin = null!;
        public static GameObject _Root = null!;
        public static Sprite m_settlerPin = null!;
        public static AssetLoaderManager m_assetLoaderManager = null!;

        public static ConfigEntry<Toggle> _locationEnabled = null!;
        private static ConfigEntry<int> _quantity = null!;
        private static ConfigEntry<Heightmap.Biome> _biomes = null!;
        public enum Toggle { On = 1, Off = 0 }
        
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> _SettlersCanLumber = null!;
        public static ConfigEntry<Toggle> _SettlerCanFish = null!;
        public static ConfigEntry<Toggle> _SettlersCanMine = null!;
        public static ConfigEntry<Toggle> _autoPickup = null!;
        public static ConfigEntry<string> _maleNames = null!;
        public static ConfigEntry<string> _femaleNames = null!;
        public static ConfigEntry<string> _lastNames = null!;
        public static ConfigEntry<float> _SettlerBaseHealth = null!;
        public static ConfigEntry<Toggle> _SettlerRequireFood = null!;

        public static ConfigEntry<string> _elfMaleNames = null!;
        public static ConfigEntry<string> _elfFemaleNames = null!;
        public static ConfigEntry<string> _elfLastNames = null!;

        public static ConfigEntry<float> _baseMaxCarryWeight = null!;
        private static ConfigEntry<KeyCode> _makeAllFollowKey = null!;
        private static ConfigEntry<KeyCode> _makeAllUnfollowKey = null!;
        public static ConfigEntry<Toggle> _replaceSpawns = null!;
        public static ConfigEntry<float> _raiderDropChance = null!;
        public static ConfigEntry<Character.Faction> _raiderFaction = null!;
        public static ConfigEntry<float> _raiderBaseHealth = null!;
        public static ConfigEntry<Toggle> _addMinimapPin = null!;
        public static ConfigEntry<float> _settlerTamingTime = null!;
        public static ConfigEntry<Toggle> _ownerLock = null!;
        public static ConfigEntry<float> _attackModifier = null!;
        public static ConfigEntry<float> _onDamagedModifier = null!;
        private static ConfigEntry<bool> _centerFirst = null!;
        public static ConfigEntry<Toggle> _colorfulHair = null!;
        // public static ConfigEntry<Toggle> _settlersCanRide = null!;
        public static ConfigEntry<int> _settlerPurchasePrice = null!;
        
        public static ConfigEntry<float> _harpoonPullSpeed = null!;
        public static ConfigEntry<float> _shipHealth = null!;
        
        public static ConfigEntry<Toggle> _repairShips = null!;
        public static ConfigEntry<string> _repairShipMat = null!;
        public static ConfigEntry<int> _repairShipValue = null!;
        public static ConfigEntry<float> _costModifier = null!;

        public static ConfigEntry<Toggle> _elfTamable = null!;

        public static ConfigEntry<Toggle> _pvp = null!;
        public static ConfigEntry<float> _locationLootChance = null!;
        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            _autoPickup = config("2 - Settings", "Pickup Items", Toggle.On, "If on, settler and raiders picks up nearby items");
            _attackModifier = config("2 - Settings", "Attack Modifier", 1f, new ConfigDescription("Make settlers and raiders weaker or stronger", new AcceptableValueRange<float>(0f, 2f)));
            _onDamagedModifier = config("2 - Settings", "On Damaged Modifier", 1f, new ConfigDescription("Make settlers and raiders take more or less damage", new AcceptableValueRange<float>(0f, 2f)));
            _colorfulHair = config("2 - Settings", "Colorful Hair", Toggle.Off, "If on, the hair color generation is completely random");

            _locationEnabled = config("3 - Locations", "Enabled", Toggle.On, "If on, blueprint locations will generate");
            _quantity = config("3 - Locations", "Quantity", 600, "Set amount of blueprint locations to generate");
            _biomes = config("3 - Locations", "Biomes", Heightmap.Biome.All, "Set biomes settler locations can generate");
            _centerFirst = config("3 - Locations", "Center First", true, "If true, locations will be placed center of map and expand");
            _locationLootChance = config("3 - Locations", "Loot Chance", 1f,
                new ConfigDescription("Set chance for treasure in locations", new AcceptableValueRange<float>(0f, 1f)));
            
            _raiderBaseHealth = config("4 - Raiders", "Raider Base Health", 75f, "Set raider base health, multiplied by level");
            _raiderFaction = config("4 - Raiders", "Raider Faction", Character.Faction.SeaMonsters, "Set raider faction");
            _replaceSpawns = config("4 - Raiders", "Replace Creature Spawners", Toggle.Off, "If on, raiders replace creature spawners");
            _raiderDropChance = config("4 - Raiders", "Raider Gear Drop Chance", 0.2f, new ConfigDescription("Set chance to drop items", new AcceptableValueRange<float>(0f, 1f)));
            
            _SettlersCanLumber = config("5 - Settlers", "Can Lumber", Toggle.On, "If on, settlers will lumber trees, logs and stumps, if has axe");
            _SettlersCanMine = config("5 - Settlers", "Can Mine", Toggle.On, "If on, settlers will mine any rocks that drop ore or scraps if has pickaxe");
            _SettlerCanFish = config("5 - Settlers", "Can Fish", Toggle.On, "If on, settlers will fish if has rod and bait");
            // _settlersCanRide = config("5 - Settlers", "Can Ride", Toggle.On, "If on, settlers will ride nearest rideable tame, and will make tame follow user");
            _settlerPurchasePrice = config("5 - Settlers", "Purchase Price", 999, "Set price to purchase tamed settler from haldor");
            _settlerTamingTime = config("5 - Settlers", "Tame Duration", 1800f, "Set amount of time required to tame settler");
            _baseMaxCarryWeight = config("5 - Settlers", "Base Carry Weight", 300f, "Set base carry weight for settlers");
            _makeAllFollowKey = config("5 - Settlers", "Make All Follow Key", KeyCode.None, "Set the key that will make all tamed settlers follow you, if they aren't following");
            _makeAllUnfollowKey = config("5 - Settlers", "Make All Unfollow Key", KeyCode.None, "Set the key that will make all tamed settlers unfollow, if they are following");
            _addMinimapPin = config("5 - Settlers", "Add Pin", Toggle.On, "If on, when settler is following a pin will be added on the minimap to track them");
            _ownerLock = config("5 - Settlers", "Inventory Locked", Toggle.Off, "If on, only owner can access settler inventory");
            _elfTamable = config("5 - Settlers", "Tamable Elves", Toggle.Off, "If on, elves are also tamable");
            _pvp = config("5 - Settlers", "PVP", Toggle.Off, "If on, settlers attack other settlers if tamed and not owned by same player");
            _SettlerBaseHealth = config("5 - Settlers", "Base Health", 50f, "Set settlers base health");
            _SettlerRequireFood = config("5 - Settlers", "Require Food", Toggle.On, "If on, settlers require food to perform tasks");
            
            _harpoonPullSpeed = config("6 - Raider Ships", "Harpoon pull speed", 25f, "Set the speed of harpoon pull+");
            _shipHealth = config("6 - Raider Ships", "Ship Health", 5000f, "Set the health of the raider ship");
            
            _repairShips = config("7 - Player Ships", "Repair On Water", Toggle.Off, "If on, players can repair their ships without a crafting station");
            _repairShipMat = config("7 - Player Ships", "Material", "Wood", "Set the material requirements to repair while on water");
            _repairShipValue = config("7 - Player Ships", "Material Amount", 1, "Set the amount of material needed to repair ship, multiplied by ship health");
            _costModifier = config("7 - Player Ships", "Cost Modifier", 2f, new ConfigDescription("Set the divider of the total cost amount, larger number makes it cheaper", new AcceptableValueRange<float>(1, 10)));
            
            List<string> m_maleFirstNames = new()
            {
                "Bjorn", "Harald", "Bo", "Frode", 
                "Birger", "Arne", "Erik", "Kare", 
                "Loki", "Thor", "Odin", "Ragnar", 
                "Sigurd", "Ivar", "Gunnar", "Sven",
                "Hakon", "Leif", "Magnus", "Rolf", 
                "Ulf", "Vidar", "Ingvar"
            };

            List<string> m_femaleFirstNames = new()
            {
                "Gudrun", "Hilda", "Ingrid", "Freya", 
                "Astrid", "Sigrid", "Thora", "Runa", 
                "Ylva", "Sif", "Helga", "Eira", 
                "Brynja", "Ragnhild", "Solveig", "Bodil", 
                "Signy", "Frida", "Alva", "Liv", 
                "Estrid", "Jorunn", "Aslaug", "Torunn"
            };
            List<string> m_lastNames = new List<string>()
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
            _maleNames = config("Names", "Male", string.Join(":", m_maleFirstNames), "List of first names seperated by :");
            _femaleNames = config("Names", "Female", string.Join(":", m_femaleFirstNames), "List out first names seperated by :");
            _lastNames = config("Names", "Last", string.Join(":", m_lastNames), "List of last names seperated by :");
            
            _elfMaleNames = config("Names", "Elf Male", string.Join(":", m_maleFirstNames), "List of first names seperated by :");
            _elfFemaleNames = config("Names", "Elf Female", string.Join(":", m_femaleFirstNames), "List out first names seperated by :");
            _elfLastNames = config("Names", "Elf Last", string.Join(":", m_lastNames), "List of last names seperated by :");
        }

        public void Awake()
        {
            RaiderLoadOut.Setup();
            RaiderDrops.Setup();
            SettlerGear.Setup();
            RaiderShipDrops.Setup();
            ElfLoadOut.Setup();
            Conversation.Setup();
            
            InitConfigs();
            m_settlerPin = _assetBundle.LoadAsset<Sprite>("mapicon_settler_32");
            _Plugin = this;
            m_assetLoaderManager = new AssetLoaderManager(_Plugin.Info.Metadata, SettlersLogger);
            _Root = new GameObject("root");
            DontDestroyOnLoad(_Root);
            _Root.SetActive(false);
            LoadMockLocation();
            AssetMan.RegisterElfEars();
            Commands.LoadServerLocationChange();
            BlueprintManager.LoadBlueprints();
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
        
        private void LoadMockLocation()
        {
            LocationManager.LocationData location = new LocationManager.LocationData("BlueprintLocation", _locationBundle)
            {
                m_data =
                {
                    m_quantity = _quantity.Value,
                    m_clearArea = true,
                    m_biome = _biomes.Value,
                    m_centerFirst = _centerFirst.Value
                }
            };
        }

        private void OnDestroy() => Config.Save();
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
    }
}