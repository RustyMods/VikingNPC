using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Settlers.Behaviors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Settlers.Settlers;

public static class RaiderShipMan
{
    private static readonly Dictionary<string, RaiderShip> Ships = new();

    public static RaiderShip? GetShipData(string name) =>
        Ships.TryGetValue(name.Replace("(Clone)", String.Empty), out RaiderShip ship) ? ship : null;
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static class ObjectDB_Awake_Patch
    {
        private static void Postfix(ObjectDB __instance)
        {
            if (!__instance || !ZNetScene.instance) return;
            foreach (var ship in Ships.Values) ship.Load();
        }
    }
    
    private static void Register(GameObject prefab)
    {
        if (!ZNetScene.instance.m_prefabs.Contains(prefab)) ZNetScene.instance.m_prefabs.Add(prefab);
        ZNetScene.instance.m_namedPrefabs[prefab.name.GetStableHashCode()] = prefab;
    }

    public class RaiderShip
    {
        public readonly string Original;
        public readonly string PrefabName;
        public GameObject Prefab = null!;
        private ShipAI ShipAI = null!;
        public readonly List<string> ObjectsToDestroy = new();
        public readonly List<string> ObjectsToEnable = new();
        public Heightmap.Biome Biome = Heightmap.Biome.None;
        public ConfigEntry<float> m_shipHealth = null!;
        public float ShipHealth = 5000f;
        public bool addOars = false;

        public RaiderShip(string originalPrefab, string prefabName)
        {
            Original = originalPrefab;
            PrefabName = prefabName;
            Ships[PrefabName] = this;
        }

        public bool Load()
        {
            if (ZNetScene.instance.GetPrefab(Original) is not {} original || !original.TryGetComponent(out Ship ship)) return false;
            Prefab = Object.Instantiate(original, SettlersPlugin._Root.transform, false);
            Prefab.name = PrefabName;
            m_shipHealth = SettlersPlugin._Plugin.config(PrefabName, "Ship Health", ShipHealth, "Set the health of the ship");
            AddShipMan();
            AddAI(ship);
            AddShipEffects();
            if (addOars) AddOars();
            EnableObjects();
            DestroyObjects();
            DestroyOldComponents();
            GlobalSpawn.CustomSpawnData spawnData = new GlobalSpawn.CustomSpawnData(Prefab);
            int order = 0;
            spawnData.m_spawnInterval = 4000f;
            spawnData.m_spawnChance = 10f;
            spawnData.m_spawnDistance = 50f;
            spawnData.Biome = Biome;
            spawnData.SetupConfigs(ref order);
            Register(Prefab);
            return true;
        }

        private void AddOars()
        {
            var oars = SettlersPlugin._oarsBundle.LoadAsset<GameObject>("ShipOars");
            var oarsObj = Object.Instantiate(oars, Prefab.transform);
            oarsObj.name = "oars";
        }
        private void AddAI(Ship ship)
        {
            ShipAI = Prefab.AddComponent<ShipAI>();
            ShipAI.m_waterImpactEffect = ship.m_waterImpactEffect;
            ShipAI.m_rudderValue = ship.m_rudderValue;
            foreach (var chair in Prefab.GetComponentsInChildren<Chair>(true))
            {
                ShipAI.m_attachPoints.Add(chair.m_attachPoint);
                if (chair.TryGetComponent(out BoxCollider collider)) Object.Destroy(collider);
                Object.Destroy(chair);
            }
        }
        private void AddShipEffects()
        {
            ShipEffects shipEffects = Prefab.GetComponentInChildren<ShipEffects>();
            RaiderShipEffects raiderShipEffects = shipEffects.gameObject.AddComponent<RaiderShipEffects>();
            raiderShipEffects.m_shadow = shipEffects.m_shadow;
            raiderShipEffects.m_offset = shipEffects.m_offset;
            raiderShipEffects.m_minimumWakeVel = shipEffects.m_minimumWakeVel;
            raiderShipEffects.m_speedWakeRoot = shipEffects.m_speedWakeRoot;
            raiderShipEffects.m_wakeSoundRoot = shipEffects.m_wakeSoundRoot;
            raiderShipEffects.m_inWaterSoundRoot = shipEffects.m_inWaterSoundRoot;
            raiderShipEffects.m_audioFadeDuration = shipEffects.m_audioFadeDuration;
            raiderShipEffects.m_sailSound = shipEffects.m_sailSound;
            raiderShipEffects.m_sailFadeDuration = shipEffects.m_sailFadeDuration;
            raiderShipEffects.m_splashEffects = shipEffects.m_splashEffects;
            raiderShipEffects.m_wakeParticles = shipEffects.m_wakeParticles;
            raiderShipEffects.m_sailBaseVol = shipEffects.m_sailBaseVol;
        }
        private void EnableObjects()
        {
            foreach (string childPath in ObjectsToEnable)
            {
                if (Prefab.transform.Find(childPath) is {} child) child.gameObject.SetActive(true);
            }
        }
        private void AddShipMan()
        {
            if (!Prefab.TryGetComponent(out WearNTear wearNTear)) return;
            ShipMan ShipMan = Prefab.AddComponent<ShipMan>();
            ShipMan.m_broken = wearNTear.m_broken;
            ShipMan.m_new = wearNTear.m_new;
            ShipMan.m_worn = wearNTear.m_worn;
            ShipMan.m_destroyedEffect = wearNTear.m_destroyedEffect;
            ShipMan.m_destroyNoise = wearNTear.m_destroyNoise;
            ShipMan.m_hitEffect = wearNTear.m_hitEffect;
            StatusEffect burning = ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectBurning);
            ShipMan.m_fireEffect = burning.m_startEffects;
        }
        private void DestroyObjects()
        {
            foreach (string childPath in ObjectsToDestroy)
            {
                if (Prefab.transform.Find(childPath) is { } child) Object.Destroy(child.gameObject);
            }
        }
        private void DestroyOldComponents()
        {
            Object.Destroy(Prefab.GetComponent<Ship>());
            Object.Destroy(Prefab.GetComponent<Piece>());
            Object.Destroy(Prefab.GetComponent<WearNTear>());
            Object.Destroy(Prefab.GetComponentInChildren<ShipEffects>());
        }
    }
}