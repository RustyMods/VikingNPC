using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using Settlers.Behaviors;
using Settlers.ExtraConfigs;
using Settlers.Managers;
using Settlers.Settlers;
using UnityEngine;

namespace Settlers
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class SettlersPlugin : BaseUnityPlugin
    {
        internal const string ModName = "VikingNPC";
        internal const string ModVersion = "0.2.4";
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
        public enum Toggle { On = 1, Off = 0 }
        
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<string> _maleNames = null!;
        public static ConfigEntry<string> _femaleNames = null!;
        public static ConfigEntry<string> _lastNames = null!;
        public static ConfigEntry<string> _elfMaleNames = null!;
        public static ConfigEntry<string> _elfFemaleNames = null!;
        public static ConfigEntry<string> _elfLastNames = null!;


        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            _maleNames = config("Names", "Male", string.Join(":", Randomizer.m_maleFirstNames), "List of first names seperated by :");
            _femaleNames = config("Names", "Female", string.Join(":", Randomizer.m_femaleFirstNames), "List out first names seperated by :");
            _lastNames = config("Names", "Last", string.Join(":", Randomizer.m_lastNames), "List of last names seperated by :");
            _elfMaleNames = config("Names", "Elf Male", string.Join(":", Randomizer.m_maleFirstNames), "List of first names seperated by :");
            _elfFemaleNames = config("Names", "Elf Female", string.Join(":", Randomizer.m_femaleFirstNames), "List out first names seperated by :");
            _elfLastNames = config("Names", "Elf Last", string.Join(":", Randomizer.m_lastNames), "List of last names seperated by :");
        }

        public void Awake()
        {
            RaiderLoadOut.Setup();
            RaiderDrops.Setup();
            SettlerGear.Setup();
            RaiderShipDrops.Setup();
            ElfLoadOut.Setup();
            Conversation.Setup();
            CompanionSkill.Setup();
            InitConfigs();
            m_settlerPin = _assetBundle.LoadAsset<Sprite>("mapicon_settler_32");
            _Plugin = this;
            _Root = new GameObject("root");
            DontDestroyOnLoad(_Root);
            _Root.SetActive(false);
            GlobalSpawn.SpawnList = _Root.AddComponent<SpawnSystemList>();
            Commands.LoadServerLocationChange();
            Localizer.Load();
            
            RaiderShipMan.RaiderShip MerchantShip = new RaiderShipMan.RaiderShip("VikingShip", "RaiderShip");
            MerchantShip.ObjectsToDestroy.Add("ashdamageeffects");
            MerchantShip.ObjectsToDestroy.Add("ControlGui");
            MerchantShip.ObjectsToDestroy.Add("ship/visual/unused");
            MerchantShip.ObjectsToDestroy.Add("ship/visual/Customize/ShipTentRight");
            MerchantShip.ObjectsToDestroy.Add("ship/visual/Customize/ShipTentLeft");
            MerchantShip.ObjectsToDestroy.Add("interactive/sit_box/box");
            MerchantShip.ObjectsToDestroy.Add("interactive/sit_box (1)/box");
            MerchantShip.ObjectsToDestroy.Add("interactive/sit_box (2)/box");
            MerchantShip.ObjectsToDestroy.Add("interactive/sit_box (3)/box");
            MerchantShip.ObjectsToDestroy.Add("interactive/sit_box (4)/box");
            MerchantShip.ObjectsToDestroy.Add("interactive/controlls/box");
            MerchantShip.ObjectsToDestroy.Add("interactive/controlls/rudder_button");
            MerchantShip.ObjectsToEnable.Add("ship/visual/Customize");
            MerchantShip.Biome = Heightmap.Biome.Ocean;
            MerchantShip.addOars = true;
            
            RaiderShipMan.RaiderShip AshlandShip = new RaiderShipMan.RaiderShip("VikingShip_Ashlands", "RaiderShip_Ashlands");
            AshlandShip.ObjectsToDestroy.Add("ControlGui");
            AshlandShip.ObjectsToDestroy.Add("ship/visual/unused");
            AshlandShip.ObjectsToDestroy.Add("ship/visual/Mast/Sail");
            AshlandShip.ObjectsToDestroy.Add("ship/visual/Customize");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (1)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (2)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (3)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (4)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (5)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (6)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (7)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (8)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/sit_box (9)/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/controls/box");
            AshlandShip.ObjectsToDestroy.Add("interactive/controls/rudder_button");
            AshlandShip.ObjectsToDestroy.Add("Hides_Plane.004");
            
            VikingManager.Settler Settler = new VikingManager.Settler("VikingSettler");
            Settler.SetBiome(Heightmap.Biome.Meadows);
            Settler.SetupConfigs();
            TraderManager.MerchantItem SettlerPurchase = new TraderManager.MerchantItem("SwordBronze", "SettlerSword");
            SettlerPurchase.SharedName = "$name_vikingsettler";
            SettlerPurchase.Description = "$purchase_settler_desc lvl 1";
            SettlerPurchase.RequiredGlobalKey = config("VikingSettler", "Purchase Required Key", "defeated_vikingraider", "Set required key to access trade item");
            SettlerPurchase.Cost = config("VikingSettler", "Cost", 999, "Set price of settler");
            SettlerPurchase.Enabled = config("VikingSettler", "Can Purchase", Toggle.Off, "If on, settlers can be purchased at haldor");
            SettlerPurchase.Action = (__instance) =>
            {
                if (ZNetScene.instance.GetPrefab("VikingSettler") is not { } prefab) return;
                Vector2 random = UnityEngine.Random.insideUnitCircle * 5f;
                Vector3 pos = Player.m_localPlayer.transform.position + new Vector3(random.x, 0f, random.y);
                GameObject clone = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                if (!clone.TryGetComponent(out Companion component) || !clone.TryGetComponent(out TameableCompanion tameableCompanion)) return;
                tameableCompanion.Tame();
                component.SetLevel(SettlerPurchase.Level);
                Player.m_localPlayer.GetInventory().RemoveItem(__instance.m_coinPrefab.m_itemData.m_shared.m_name, __instance.m_selectedItem.m_price);
                __instance.m_buyEffects.Create(__instance.transform.position, Quaternion.identity);
                __instance.FillList();
            };
            VikingManager.Sailor Sailor = new VikingManager.Sailor("VikingSailor");
            Sailor.SetupConfigs();
            foreach (Heightmap.Biome biome in Enum.GetValues(typeof(Heightmap.Biome)))
            {
                if (biome is Heightmap.Biome.None or Heightmap.Biome.All or Heightmap.Biome.Ocean) continue;
                var raider = "VikingRaider_" + biome;
                var elf = "VikingElf_" + biome;
                VikingManager.Elf Elf = new VikingManager.Elf(elf);
                Elf.SetBiome(biome);
                Elf.SetupConfigs();
                VikingManager.Raider Raider = new VikingManager.Raider(raider);
                Raider.SetBiome(biome);
                Raider.SetupConfigs();
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
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

        public ConfigEntry<T> config<T>(string group, string name, T value, string description, int order,
            bool synchronizedSetting = true)
        {
            return config(group, name, value,
                new ConfigDescription(description, null, new ConfigurationManagerAttributes() { Order = order }),
                synchronizedSetting);
        }
        
        public ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, int order,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), 
                    description.AcceptableValues, 
                    description.Tags,
                    new ConfigurationManagerAttributes(){ Order = order}
                    );
            return config(group, name, value, extendedDescription);
        }

        public class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }
    }
}