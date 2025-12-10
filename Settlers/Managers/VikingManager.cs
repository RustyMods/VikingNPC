using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Settlers.Managers;

public class VikingManager
{
    private static GameObject? VikingRagDoll;
    private static GameObject Player = null!;
    private static Player PlayerComponent = null!;
    private static readonly Dictionary<string, Viking> Vikings = new();

    static VikingManager()
    {
        var harmony = new Harmony("org.bepinex.helpers.viking.manager");
        harmony.Patch(AccessTools.DeclaredMethod(typeof(ObjectDB), nameof(ObjectDB.Awake)), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(VikingManager), nameof(Patch_ObjectDB_Awake))));
    }
    private static void Patch_ObjectDB_Awake(ObjectDB __instance)
    {
        if (!ZNetScene.instance || !__instance) return;
        Player = ZNetScene.instance.GetPrefab("Player");
        PlayerComponent = Player.GetComponent<Player>();
        RegisterElfEars();
        CreateRagDoll();
        TraderManager.Setup();
        foreach (var viking in Vikings.Values)
        {
            if (viking.Load()) viking.RegisterPrefab();
        }
    }
    private static void RegisterElfEars()
    {
        GameObject elfEars = SettlersPlugin._elfBundle.LoadAsset<GameObject>("ElvenEars");
        if (elfEars.TryGetComponent(out ItemDrop component))
        {
            component.m_itemData.m_shared.m_name = "Elf Ears";
        }
        Register(elfEars);
        RegisterToDB(elfEars);
    }
    private static void CreateRagDoll()
    {
        if (VikingRagDoll != null) return;
        GameObject prefab = Object.Instantiate(ZNetScene.instance.GetPrefab("Player_ragdoll"), SettlersPlugin._Root.transform, false);
        if (prefab.TryGetComponent(out Ragdoll rag))
        {
            rag.m_ttl = 8f;
            rag.m_removeEffect = new()
            {
                m_effectPrefabs = new[]
                {
                    new EffectList.EffectData()
                    {
                        m_prefab = ZNetScene.instance.GetPrefab("vfx_corpse_destruction_small"),
                        m_enabled = true
                    }
                }
            };
            rag.m_float = true;
            rag.m_dropItems = true;
        }
        prefab.name = "viking_npc_ragdoll";
        Register(prefab);
        VikingRagDoll = prefab;
    }
    public static void Register(GameObject prefab)
    {
        if (!ZNetScene.instance) return;
        if (!ZNetScene.instance.m_prefabs.Contains(prefab)) ZNetScene.instance.m_prefabs.Add(prefab);
        ZNetScene.instance.m_namedPrefabs[prefab.name.GetStableHashCode()] = prefab;
    }
    public static void RegisterToDB(GameObject prefab)
    {
        if (!ObjectDB.instance) return;
        if (!ObjectDB.instance.m_items.Contains(prefab)) ObjectDB.instance.m_items.Add(prefab);
        ObjectDB.instance.m_itemByHash[prefab.name.GetStableHashCode()] = prefab;
    }
    private static void UpdateEffects(List<string> effects, ref EffectList effectList)
    {
        if (effects.Count <= 0) return;
        List<EffectList.EffectData> data = effectList.m_effectPrefabs.ToList();
        foreach (string effect in effects)
        {
            GameObject? prefab = ZNetScene.instance.GetPrefab(effect);
            if (!prefab) continue;
            data.Add(new EffectList.EffectData() { m_prefab = prefab });
        }

        effectList.m_effectPrefabs = data.ToArray();
    }
    private static void CreateSpawner(GameObject original, string creature)
    {
        if (ZNetScene.instance.GetPrefab(creature) is { } prefab)
        {
            GameObject? clone = Object.Instantiate(original, SettlersPlugin._Root.transform, false);
            clone.name = $"Spawner_{creature}";
            if (!clone.TryGetComponent(out CreatureSpawner spawnArea)) return;
            spawnArea.m_creaturePrefab = prefab;
            
            Register(clone);
        }
    }

    public static Viking? GetData(string prefabName) => Vikings.TryGetValue(prefabName.Replace("(Clone)", string.Empty), out Viking viking) ? viking : null;
    
    public class Elf : Viking
    {
        private Behaviors.TameableCompanion TameableCompanion = null!;
        public string CloneTameEffectsFrom = "Boar";
        public SettlersPlugin.Toggle Tameable = SettlersPlugin.Toggle.Off;
        public float DropChance = 0.1f;
        public float TameTime = 1800f;
        public SettlersPlugin.Toggle AddPin = SettlersPlugin.Toggle.On;
        public GlobalSpawn.CustomSpawnData SpawnData = null!;
        public Elf(string prefabName) : base(prefabName) => VikingType = VikingType.Elf;
        
        public override void SetupConfigs()
        {
            base.SetupConfigs();
            configs.Tameable = SettlersPlugin._Plugin.config(PrefabName, "Tameable", Tameable, "If on, viking is tameable", GetOrder());
            configs.Track = SettlersPlugin._Plugin.config(PrefabName, "Add Pin", AddPin, "If on, when viking is following, a pin will be added on the minimap to track", GetOrder());
            configs.TameTime = SettlersPlugin._Plugin.config(PrefabName, "Tame Duration", TameTime, "Set amount of time required to tame viking", GetOrder());
            configs.DropChance = SettlersPlugin._Plugin.config(PrefabName, "Drop Chance", DropChance, new ConfigDescription("Set chance to drop weapon or armor items", new AcceptableValueRange<float>(0f, 1f)), GetOrder());
            SpawnData = new GlobalSpawn.CustomSpawnData(this)
            {
                m_spawnInterval = 1000f,
                m_spawnDistance = 50f,
                m_maxLevel = 3,
                m_minAltitude = 10f,
                m_spawnChance = 5f
            };
            SpawnData.SetupConfigs(ref order);
        }

        public override bool Load()
        {
            if (!base.Load()) return false;
            Behaviors.CustomFactions.CustomFaction customFaction = new("Elf", true);
            Companion.m_faction = customFaction.m_faction;
            Companion.configs = configs;
            AddTameable();
            SpawnData.m_prefab = Prefab;
            return true;
        }
        
        private void AddTameable()
        {
            TameableCompanion = Prefab.AddComponent<Behaviors.TameableCompanion>();
            if (ZNetScene.instance.GetPrefab(CloneTameEffectsFrom) is not { } prefab || !prefab.TryGetComponent(out Tameable tame)) return;
            TameableCompanion.m_tamedEffect = tame.m_tamedEffect;
            TameableCompanion.m_sootheEffect = tame.m_sootheEffect;
            TameableCompanion.m_petEffect = tame.m_petEffect;
        }
    }
    public class Settler : Viking
    {
        private Behaviors.TameableCompanion TameableCompanion = null!;
        public string CloneTameEffectsFrom = "Boar";
        public SettlersPlugin.Toggle CanLumber = SettlersPlugin.Toggle.On;
        public SettlersPlugin.Toggle CanMine = SettlersPlugin.Toggle.On;
        public SettlersPlugin.Toggle CanFish = SettlersPlugin.Toggle.On;
        public float TameTime = 1800f;
        public float BaseCarryWeight = 300f;
        public SettlersPlugin.Toggle AddPin = SettlersPlugin.Toggle.On;
        private SettlersPlugin.Toggle Locked = SettlersPlugin.Toggle.Off;
        private SettlersPlugin.Toggle RequireFood = SettlersPlugin.Toggle.On;
        public GlobalSpawn.CustomSpawnData SpawnData = null!;
        public Settler(string prefabName) : base(prefabName) => VikingType = VikingType.Settler;
        public override void SetupConfigs()
        {
            base.SetupConfigs();
            configs.CanLumber = SettlersPlugin._Plugin.config(PrefabName, "Can Lumber", CanLumber, "If on, viking will lumber trees, logs and stumps, if has axe", GetOrder());
            configs.CanMine = SettlersPlugin._Plugin.config(PrefabName, "Can Mine", CanMine, "If on, viking will mine rocks  that drop ore, if has pickaxe", GetOrder());
            configs.CanFish = SettlersPlugin._Plugin.config(PrefabName, "Can Fish", CanFish, "If on, viking will fish if has rod and bait, appropriate to biome", GetOrder());
            configs.TameTime = SettlersPlugin._Plugin.config(PrefabName, "Tame Duration", TameTime, "Set amount of time required to tame viking", GetOrder());
            configs.BaseCarryWeight = SettlersPlugin._Plugin.config(PrefabName, "Base Carry Weight", BaseCarryWeight, "Set base carry weight for viking", GetOrder());
            configs.Track = SettlersPlugin._Plugin.config(PrefabName, "Add Pin", AddPin, "If on, when viking is following, a pin will be added on the minimap to track", GetOrder());
            configs.Locked = SettlersPlugin._Plugin.config(PrefabName, "Inventory Private", Locked, "If on, only owners can access viking inventory", GetOrder());
            configs.RequireFood = SettlersPlugin._Plugin.config(PrefabName, "Require Food", RequireFood, "If on, viking require food to perform tasks", GetOrder());
            SpawnData = new GlobalSpawn.CustomSpawnData(this)
            {
                m_spawnInterval = 1000f,
                m_spawnDistance = 50f,
                m_maxLevel = 3,
                m_minAltitude = 10f
            };
            SpawnData.SetupConfigs(ref order);
        }

        public override bool Load()
        {
            if (!base.Load()) return false;
            Companion.m_group = "Settlers";
            Behaviors.CustomFactions.CustomFaction customFaction = new("Settlers", true);
            Companion.m_faction = customFaction.m_faction;
            Prefab.AddComponent<Behaviors.SettlerContainer>();
            AddTameable();
            SpawnData.m_prefab = Prefab;
            return true;
        }
        private void AddTameable()
        {
            TameableCompanion = Prefab.AddComponent<Behaviors.TameableCompanion>();
            if (ZNetScene.instance.GetPrefab(CloneTameEffectsFrom) is not { } prefab || !prefab.TryGetComponent(out Tameable tame)) return;
            TameableCompanion.m_tamedEffect = tame.m_tamedEffect;
            TameableCompanion.m_sootheEffect = tame.m_sootheEffect;
            TameableCompanion.m_petEffect = tame.m_petEffect;
        }
    }

    public class Sailor : Viking
    {
        public CharacterDrop CharacterDrop = null!;
        public Sailor(string prefabName) : base(prefabName)
        {
            VikingType = VikingType.Sailor;
        }

        public override bool Load()
        {
            if (!base.Load()) return false;
            Companion.m_group = "Raiders";
            Behaviors.CustomFactions.CustomFaction customFaction = new("Raiders", false);
            Companion.m_faction = customFaction.m_faction;
            CharacterDrop = Prefab.AddComponent<CharacterDrop>();
            return true;
        }
    }

    public class Raider : Viking
    {
        public float DropChance = 0.1f;
        public GlobalSpawn.CustomSpawnData SpawnData = null!;
        public Raider(string prefabName) : base(prefabName)
        {
            VikingType = VikingType.Raider;
        }
        public override void SetupConfigs()
        {
            base.SetupConfigs();
            configs.DropChance = SettlersPlugin._Plugin.config(PrefabName, "Drop Chance", DropChance, new ConfigDescription("Set chance to drop weapon or armor items", new AcceptableValueRange<float>(0f, 1f)), GetOrder());
            SpawnData = new GlobalSpawn.CustomSpawnData(this)
            {
                m_spawnInterval = 1000f,
                m_spawnDistance = 50f,
                m_maxLevel = 3,
                m_minAltitude = 10f,
                m_spawnChance = 10f
            };
            SpawnData.SetupConfigs(ref order);
        }

        public override bool Load()
        {
            if (!base.Load()) return false;
            Companion.m_group = "Raiders";
            Behaviors.CustomFactions.CustomFaction customFaction = new("Raiders", false);
            Companion.m_faction = customFaction.m_faction;
            Prefab.AddComponent<CharacterDrop>();
            SpawnData.m_prefab = Prefab;
            RaidManager.Event.Add(SpawnData);
            return true;
        }
    }
    
    public enum VikingType {None, Settler, Elf, Raider, Sailor}
    public class Viking
    {
        protected VikingType VikingType = VikingType.None;
        protected GameObject Prefab = null!;
        public readonly string PrefabName;
        protected Behaviors.Companion Companion = null!;
        private Behaviors.CompanionAI CompanionAI = null!;
        protected Behaviors.Randomizer Randomizer = null!;
        private Behaviors.CompanionTalk CompanionTalk = null!;
        private readonly SE_Viking StatusEffect;
        public bool Aggravatable = true;
        public bool AttackPlayerObjects;
        public List<string> KilledEffects = new();
        public List<string> DeathEffects = new();
        public List<string> DefaultItems = new();
        public List<string> RandomTalk = new();
        public List<string> RandomTalkInPlayerBase= new();
        public List<string> RandomGreets= new();
        public List<string> RandomGoodbye= new();
        public List<string> AggravatedTalk= new();
        public List<string> ShipTalk= new();
        public List<string> RandomGreetFX = new();
        public List<string> RandomGoodbyeFX = new();
        public List<string> RandomTalkFX = new();
        public List<string> RandomAlertedFX = new();
        public Heightmap.Biome Biome = Heightmap.Biome.None;
        public Heightmap.Biome Tier = Heightmap.Biome.Meadows;
        public readonly VikingConfigs configs = new();
        public int order = 0;
        public float BaseHealth = 50f;
        protected Viking(string prefabName)
        {
            PrefabName = prefabName;
            Vikings[PrefabName] = this;
            KilledEffects.Add("sfx_dverger_vo_death");
            KilledEffects.Add("vfx_fenring_death");
            DeathEffects.Add("vfx_player_death");
            StatusEffect = ScriptableObject.CreateInstance<SE_Viking>();
            StatusEffect.name = "SE_" + prefabName;
            LoadDefaultRandomTalkValues();
        }
        public virtual void SetupConfigs()
        {
            configs.BaseHealth = SettlersPlugin._Plugin.config(PrefabName, "Base Health", BaseHealth, "Define base health", 0);
            configs.BaseHealth.SettingChanged += (_,_) => { foreach (var companion in Behaviors.Companion.m_instances) companion.SetupMaxHealth(); };
            configs.AutoPickup = SettlersPlugin._Plugin.config(PrefabName, "Auto Pickup", SettlersPlugin.Toggle.On, "Set if viking should auto pickup items", GetOrder());
            StatusEffect.config.Attack = SettlersPlugin._Plugin.config(PrefabName, "Attack Modifier", 1f, new ConfigDescription("Set modifier for damage output", new AcceptableValueRange<float>(0f, 2f), GetOrder()));
            StatusEffect.config.OnDamaged = SettlersPlugin._Plugin.config(PrefabName, "On Damaged Modifier", 1f, new ConfigDescription("Set modifier for damage received", new AcceptableValueRange<float>(0f, 2f), GetOrder()));
        }

        public void SetBiome(Heightmap.Biome biome)
        {
            Biome = biome;
            Tier = biome;
        }

        protected int GetOrder()
        {
            ++order;
            return order;
        }
        public virtual bool Load()
        {
            Prefab = Object.Instantiate(Player, SettlersPlugin._Root.transform, false);
            Prefab.name = PrefabName;
            DestroyPlayerComponents();
            SetZNetView();
            if (!AddCompanionComponent()) return false;
            Companion.StatusEffect = StatusEffect;
            Companion.configs = configs;
            Companion.Tier = Tier;
            UpdateEffects(KilledEffects, ref Companion.m_killedEffects);
            AddRagDoll();
            UpdateEffects(DeathEffects, ref Companion.m_deathEffects);
            Companion.m_defaultItems = DefaultItems.Select(itemName => ObjectDB.instance.GetItemPrefab(itemName)).Where(item => item != null).ToArray();
            AddAI();
            Randomizer = Prefab.AddComponent<Behaviors.Randomizer>();
            AddRandomTalk();
            RegisterSE();
            return true;
        }
        public void RegisterPrefab() => Register(Prefab);
        private void DestroyPlayerComponents()
        {
            Object.Destroy(Prefab.GetComponent<PlayerController>());
            Object.Destroy(Prefab.GetComponent<Player>());
            Object.Destroy(Prefab.GetComponent<Talker>());
            Object.Destroy(Prefab.GetComponent<Skills>());
        }
        private void SetZNetView()
        {
            if (!Prefab.TryGetComponent(out ZNetView zNetView)) return;
            zNetView.m_persistent = true;
        }
        private bool AddCompanionComponent()
        {
            switch (VikingType)
            {
                case VikingType.Settler:
                    Companion = Prefab.AddComponent<Behaviors.Settler>();
                    break;
                case VikingType.Elf:
                    Companion = Prefab.AddComponent<Behaviors.Elf>();
                    break;
                case VikingType.Raider:
                    Companion = Prefab.AddComponent<Behaviors.Raider>();
                    break;
                case VikingType.Sailor:
                    Companion = Prefab.AddComponent<Behaviors.Sailor>();
                    break;
                default:
                    return false;
            }
            Companion.name = Prefab.name;
            Companion.m_name = "Viking";
            Companion.m_crouchSpeed = PlayerComponent.m_crouchSpeed;
            Companion.m_walkSpeed = PlayerComponent.m_walkSpeed;
            Companion.m_speed = PlayerComponent.m_speed;
            Companion.m_runSpeed = PlayerComponent.m_runSpeed;
            Companion.m_runTurnSpeed = PlayerComponent.m_runTurnSpeed;
            Companion.m_acceleration = PlayerComponent.m_acceleration;
            Companion.m_jumpForce = PlayerComponent.m_jumpForce;
            Companion.m_jumpForceForward = PlayerComponent.m_jumpForceForward;
            Companion.m_jumpForceTiredFactor = PlayerComponent.m_jumpForceForward;
            Companion.m_airControl = PlayerComponent.m_airControl;
            Companion.m_canSwim = true;
            Companion.m_swimDepth = PlayerComponent.m_swimDepth;
            Companion.m_swimSpeed = PlayerComponent.m_swimSpeed;
            Companion.m_swimTurnSpeed = PlayerComponent.m_swimTurnSpeed;
            Companion.m_swimAcceleration = PlayerComponent.m_swimAcceleration;
            Companion.m_groundTilt = PlayerComponent.m_groundTilt;
            Companion.m_groundTiltSpeed = PlayerComponent.m_groundTiltSpeed;
            Companion.m_jumpStaminaUsage = PlayerComponent.m_jumpStaminaUsage;
            Companion.m_eye = Utils.FindChild(Prefab.transform, "EyePos");
            Companion.m_hitEffects = PlayerComponent.m_hitEffects;
            Companion.m_critHitEffects = PlayerComponent.m_critHitEffects;
            Companion.m_backstabHitEffects = PlayerComponent.m_backstabHitEffects;
            Companion.m_equipStartEffects = PlayerComponent.m_equipStartEffects;
            Companion.m_warpEffect = PlayerComponent.m_skillLevelupEffects;
            Companion.m_tombstone = PlayerComponent.m_tombstone;
            Companion.m_waterEffects = PlayerComponent.m_waterEffects;
            Companion.m_tarEffects = PlayerComponent.m_tarEffects;
            Companion.m_slideEffects = PlayerComponent.m_slideEffects;
            Companion.m_jumpEffects = PlayerComponent.m_jumpEffects;
            Companion.m_flyingContinuousEffect = PlayerComponent.m_flyingContinuousEffect;
            Companion.m_tolerateWater = true;
            Companion.m_health = 50f;
            Companion.m_damageModifiers = PlayerComponent.m_damageModifiers;
            Companion.m_staggerWhenBlocked = true;
            Companion.m_staggerDamageFactor = PlayerComponent.m_staggerDamageFactor;
            Companion.m_unarmedWeapon = PlayerComponent.m_unarmedWeapon;
            Companion.m_pickupEffects = PlayerComponent.m_autopickupEffects;
            Companion.m_dropEffects = PlayerComponent.m_dropEffects;
            Companion.m_consumeItemEffects = PlayerComponent.m_consumeItemEffects;
            Companion.m_equipEffects = PlayerComponent.m_equipStartEffects;
            Companion.m_perfectBlockEffect = PlayerComponent.m_perfectBlockEffect;
            return true;
        }
        private void AddRagDoll()
        {
            Companion.m_deathEffects = new()
            {
                m_effectPrefabs = new[]
                {
                    new EffectList.EffectData()
                    {
                        m_prefab = VikingRagDoll,
                    }
                }
            };
        }
        private void AddAI()
        {
            switch (VikingType)
            {
                case VikingType.Settler:
                    CompanionAI = Prefab.AddComponent<Behaviors.SettlerAI>();
                    break;
                case VikingType.Elf:
                    CompanionAI = Prefab.AddComponent<Behaviors.VikingAI>();
                    break;
                case VikingType.Raider:
                    CompanionAI = Prefab.AddComponent<Behaviors.VikingAI>();
                    break;
                case VikingType.Sailor:
                    CompanionAI = Prefab.AddComponent<Behaviors.SailorAI>();
                    break;
                default:
                    CompanionAI = Prefab.AddComponent<Behaviors.VikingAI>();
                    break;
            }
            CompanionAI.m_viewRange = 30f;
            CompanionAI.m_viewAngle = 90f;
            CompanionAI.m_hearRange = 9999f;
            CompanionAI.m_idleSoundInterval = 10f;
            CompanionAI.m_idleSoundChance = 0f;
            CompanionAI.m_pathAgentType = Pathfinding.AgentType.Humanoid;
            CompanionAI.m_moveMinAngle = 90f;
            CompanionAI.m_smoothMovement = true;
            CompanionAI.m_jumpInterval = 30f;
            CompanionAI.m_randomCircleInterval = 2f;
            CompanionAI.m_randomMoveInterval = 30f;
            CompanionAI.m_randomMoveRange = 3f;
            CompanionAI.m_alertRange = 20f;
            CompanionAI.m_circulateWhileCharging = true;
            CompanionAI.m_interceptTimeMax = 2f;
            CompanionAI.m_maxChaseDistance = 300f;
            CompanionAI.m_circleTargetInterval = 8f;
            CompanionAI.m_circleTargetDuration = 6f;
            CompanionAI.m_circleTargetDistance = 8f;
            CompanionAI.m_consumeRange = 2f;
            CompanionAI.m_consumeSearchRange = 5f;
            CompanionAI.m_consumeSearchInterval = 10f;
            CompanionAI.m_consumeItems = new();
            CompanionAI.m_aggravatable = Aggravatable;
            CompanionAI.m_attackPlayerObjects = AttackPlayerObjects;
            CompanionAI.m_consumeItems = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Consumable, "").Where(item => !item.m_itemData.m_shared.m_consumeStatusEffect).ToList();;
        }
        private void AddRandomTalk()
        {
            CompanionTalk = Prefab.AddComponent<Behaviors.CompanionTalk>();
            CompanionTalk.m_maxRange = 20f;
            CompanionTalk.m_greetRange = 10f;
            CompanionTalk.m_byeRange = 15f;
            CompanionTalk.m_offset = 2f;
            CompanionTalk.m_minTalkInterval = 3f;
            CompanionTalk.m_hideDialogDelay = 9f;
            CompanionTalk.m_randomTalkInterval = 30f;
            CompanionTalk.m_randomTalkChance = 0.5f;
            CompanionTalk.m_randomTalk = RandomTalk;
            CompanionTalk.m_randomTalkInPlayerBase = RandomTalkInPlayerBase;
            CompanionTalk.m_randomGreets = RandomGreets;
            CompanionTalk.m_randomGoodbye = RandomGoodbye;
            CompanionTalk.m_aggravated = AggravatedTalk;
            CompanionTalk.m_shipTalk = ShipTalk;
            UpdateEffects(RandomGreetFX, ref CompanionTalk.m_randomGreetFX);
            UpdateEffects(RandomGoodbyeFX, ref CompanionTalk.m_randomGoodbyeFX);
            UpdateEffects(RandomTalkFX, ref CompanionTalk.m_randomTalkFX);
            UpdateEffects(RandomAlertedFX, ref CompanionTalk.m_alertedFX);
        }
        private void LoadDefaultRandomTalkValues()
        {
            RandomTalk = new List<string>()
            {
                "$npc_settler_random_talk_1",
                "$npc_settler_random_talk_2",
                "$npc_settler_random_talk_3",
                "$npc_settler_random_talk_4",
                "$npc_settler_random_talk_5",
                "$npc_settler_random_talk_6",
                "$npc_settler_random_talk_7",
                "$npc_settler_random_talk_8",
            };
            RandomTalkInPlayerBase = new List<string>()
            {
                "$npc_settler_in_base_talk_1",
                "$npc_settler_in_base_talk_2",
                "$npc_settler_in_base_talk_3",
                "$npc_settler_in_base_talk_4",
                "$npc_settler_in_base_talk_5",
                "$npc_settler_in_base_talk_6",
                "$npc_settler_in_base_talk_7",
                "$npc_settler_in_base_talk_8",
            };
            RandomGreets = new List<string>()
            {
                "$npc_settler_random_greet_1",
                "$npc_settler_random_greet_2",
                "$npc_settler_random_greet_3",
                "$npc_settler_random_greet_4",
                "$npc_settler_random_greet_5",
                "$npc_settler_random_greet_6",
                "$npc_settler_random_greet_7",
                "$npc_settler_random_greet_8",
            };
            RandomGoodbye = new List<string>()
            {
                "$npc_settler_random_bye_1",
                "$npc_settler_random_bye_2",
                "$npc_settler_random_bye_3",
                "$npc_settler_random_bye_4",
                "$npc_settler_random_bye_5",
                "$npc_settler_random_bye_6",
                "$npc_settler_random_bye_7",
                "$npc_settler_random_bye_8",
            };
            AggravatedTalk = new List<string>()
            {
                "$npc_settler_aggravated_1",
                "$npc_settler_aggravated_2",
                "$npc_settler_aggravated_3",
                "$npc_settler_aggravated_4",
                "$npc_settler_aggravated_5",
                "$npc_settler_aggravated_6",
                "$npc_settler_aggravated_7",
                "$npc_settler_aggravated_8",
            };
            ShipTalk = new List<string>()
            {
                "$npc_settler_shiptalk_1",
                "$npc_settler_shiptalk_2",
                "$npc_settler_shiptalk_3",
                "$npc_settler_shiptalk_4",
                "$npc_settler_shiptalk_5",
                "$npc_settler_shiptalk_6",
                "$npc_settler_shiptalk_7",
                "$npc_settler_shiptalk_8",
            };
            RandomGreetFX.Add("sfx_haldor_greet");
            RandomGoodbyeFX.Add("sfx_haldor_laugh");
            RandomTalkFX.Add("sfx_dverger_vo_idle");
            RandomAlertedFX.Add("sfx_dverger_vo_attack");
        }
        private void RegisterSE()
        {
            if (!ObjectDB.instance.m_StatusEffects.Contains(StatusEffect)) ObjectDB.instance.m_StatusEffects.Add(StatusEffect);
        }
    }
    public class VikingConfigs
    {
        public ConfigEntry<float> BaseHealth = null!;
        public ConfigEntry<SettlersPlugin.Toggle>? Tameable;
        public ConfigEntry<SettlersPlugin.Toggle>? CanLumber;
        public ConfigEntry<SettlersPlugin.Toggle>? CanMine;
        public ConfigEntry<SettlersPlugin.Toggle>? CanFish;
        public ConfigEntry<float>? TameTime;
        public ConfigEntry<SettlersPlugin.Toggle>? Track;
        public ConfigEntry<SettlersPlugin.Toggle>? Locked;
        public ConfigEntry<float>? BaseCarryWeight;
        public ConfigEntry<SettlersPlugin.Toggle>? RequireFood;
        public ConfigEntry<SettlersPlugin.Toggle> AutoPickup = null!;
        public ConfigEntry<float>? DropChance;
    }

    public class SE_Viking : StatusEffect
    {
        public readonly Config config = new();
        public class Config
        {
            public ConfigEntry<float>? Attack;
            public ConfigEntry<float>? OnDamaged;
        }
        public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
        {
            hitData.ApplyModifier(config.Attack?.Value ?? 1f);
        }

        public override void OnDamaged(HitData hit, Character attacker)
        {
            hit.ApplyModifier(config.OnDamaged?.Value ?? 1f);
        }
    }
}