﻿using System.Collections.Generic;
using HarmonyLib;
using Settlers.Behaviors;
using SoftReferenceableAssets;
using UnityEngine;

namespace Settlers.Managers;

public class LocationManager
{
    private static readonly Dictionary<string, LocationData> m_locations = new();

    static LocationManager()
    {
        Harmony harmony = new Harmony("org.bepinex.helpers.RustyLocationManager");
        harmony.Patch(AccessTools.DeclaredMethod(typeof(ZoneSystem), nameof(ZoneSystem.SetupLocations)),
            prefix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(LocationManager), nameof(SetupLocations_Prefix))));
        harmony.Patch(AccessTools.DeclaredMethod(typeof(TerrainModifier), nameof(TerrainModifier.Awake)),
            postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(LocationManager), nameof(TerrainModAwake))));
    }

    internal static void TerrainModAwake(TerrainModifier __instance)
    {
        string name = __instance.name.Replace("(Clone)", string.Empty);
        if (name != "BlueprintTerrain") return;
        // Heightmap.Biome biome = Heightmap.FindBiome(__instance.transform.position);
        // List<BlueprintManager.BlueprintData> list = BlueprintManager.GetBiomeBlueprints(biome);
        // if (list.Count == 0) return;
        // BlueprintManager.BlueprintData data = list[Random.Range(0, list.Count)];
        // if (!__instance.TryGetComponent(out BluePrinter component))
        // {
        //     component = __instance.gameObject.AddComponent<BluePrinter>();
        // }
        // component.GenerateLocation(data);

        if (!__instance.GetComponent<BluePrinter>())
        {
            __instance.gameObject.AddComponent<BluePrinter>();
        }
    }
    private static void SetupLocations_Prefix(ZoneSystem __instance)
    {
        if (SettlersPlugin._locationEnabled.Value is SettlersPlugin.Toggle.Off) return;
        List<ZoneSystem.ZoneLocation> locations = new();
        foreach (LocationData? location in m_locations.Values)
        {
            ZoneSystem.ZoneLocation data = location.GetLocation();
            if (data.m_prefab.IsValid)
            {
                locations.Add(data);
            }
            else
            {
                SettlersPlugin.SettlersLogger.LogDebug(data.m_prefabName + " is not valid");
            }
        }
        __instance.m_locations.AddRange(locations);
        ZLog.Log($"Added {locations.Count} settler locations");
    }

    public class LocationData
    {
        private readonly AssetID AssetID;

        public LocationData(string name, AssetBundle bundle)
        {
            GameObject prefab = bundle.LoadAsset<GameObject>(name);
            SettlersPlugin.m_assetLoaderManager.AddAsset(prefab, out AssetID assetID);
            AssetID = assetID;
            m_data.m_prefabName = prefab.name;
            m_locations[prefab.name] = this;
            
            SettlersPlugin.SettlersLogger.LogDebug("Registered location: " + name);
        }

        public readonly ZoneData m_data = new();
        public class ZoneData
        {
            public Heightmap.Biome m_biome = Heightmap.Biome.All;
            public bool m_enabled = true;
            public string m_prefabName = null!;
            public Heightmap.BiomeArea m_biomeArea = Heightmap.BiomeArea.Everything;
            public int m_quantity = 100;
            public bool m_prioritized = false;
            public bool m_centerFirst = false;
            public bool m_unique = false;
            public string m_group = "";
            public float m_minDistanceFromSimilar = 300f;
            public string m_groupMax = "";
            public float m_maxDistanceFromSimilar = 0f;
            public bool m_iconAlways = false;
            public bool m_iconPlaced = false;
            public bool m_randomRotation = false;
            public bool m_slopeRotation = false;
            public bool m_snapToWater = false;
            public float m_interiorRadius = 0f;
            public float m_exteriorRadius = 100f;
            public bool m_clearArea = true;
            public float m_minTerrainDelta = 0f; 
            public float m_maxTerrainDelta = 100f;
            public float m_minimumVegetation;
            public float m_maximumVegetation = 1f;
            public bool m_surroundCheckVegetation = false;
            public float m_surroundCheckDistance = 20f;
            public int m_surroundCheckLayers = 2;
            public float m_surroundBetterThanAverage;
            public bool m_inForest = true;
            public float m_forestThresholdMin = 0f;
            public float m_forestThresholdMax = 1f;
            public float m_minDistance = 0f;
            public float m_maxDistance = 10000f;
            public float m_minAltitude = 0f;
            public float m_maxAltitude = 1000f;
            public bool m_foldout = false;
        }
        
        public ZoneSystem.ZoneLocation GetLocation()
        {
            return new ZoneSystem.ZoneLocation()
            {
                m_enable = m_data.m_enabled,
                m_prefabName = m_data.m_prefabName,
                m_prefab = SettlersPlugin.m_assetLoaderManager.GetSoftReference(AssetID),
                m_biome = m_data.m_biome,
                m_biomeArea = m_data.m_biomeArea,
                m_quantity = m_data.m_quantity,
                m_prioritized = m_data.m_prioritized,
                m_centerFirst = m_data.m_centerFirst,
                m_unique = m_data.m_unique,
                m_group = m_data.m_group,
                m_minDistanceFromSimilar = m_data.m_minDistanceFromSimilar,
                m_groupMax = m_data.m_groupMax,
                m_maxDistanceFromSimilar = m_data.m_maxDistanceFromSimilar,
                m_iconAlways = m_data.m_iconAlways,
                m_iconPlaced = m_data.m_iconPlaced,
                m_randomRotation = m_data.m_randomRotation,
                m_slopeRotation = m_data.m_slopeRotation,
                m_snapToWater = m_data.m_snapToWater,
                m_interiorRadius = m_data.m_interiorRadius,
                m_exteriorRadius = m_data.m_exteriorRadius,
                m_clearArea = m_data.m_clearArea,
                m_minTerrainDelta = m_data.m_minTerrainDelta,
                m_maxTerrainDelta = m_data.m_maxTerrainDelta,
                m_minimumVegetation = m_data.m_minimumVegetation,
                m_maximumVegetation = m_data.m_maximumVegetation,
                m_surroundCheckVegetation = m_data.m_surroundCheckVegetation,
                m_surroundCheckDistance = m_data.m_surroundCheckDistance,
                m_surroundCheckLayers = m_data.m_surroundCheckLayers,
                m_surroundBetterThanAverage = m_data.m_surroundBetterThanAverage,
                m_inForest = m_data.m_inForest,
                m_forestTresholdMin = m_data.m_forestThresholdMin,
                m_forestTresholdMax = m_data.m_forestThresholdMax,
                m_minDistance = m_data.m_minDistance,
                m_maxDistance = m_data.m_maxDistance,
                m_minAltitude = m_data.m_minAltitude,
                m_maxAltitude = m_data.m_maxAltitude,
                m_foldout = m_data.m_foldout
            };
        }
    }
}

