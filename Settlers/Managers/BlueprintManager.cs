using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using Settlers.Behaviors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Settlers.Managers;

public static class BlueprintManager
{
    public static GameObject m_terrainObject = null!;
    public static GameObject m_blueprintObject = null!;
    private static readonly List<BlueprintData> m_data = new();
    
    public static List<BlueprintData> GetBiomeBlueprints(Heightmap.Biome biome)
    {
        return m_data.Where(data => data.m_biome == biome || data.m_biome is Heightmap.Biome.All).ToList();
    }

    public static BlueprintData? GetBlueprint(string key)
    {
        return m_data.FirstOrDefault(data => data.m_blueprint.m_name == key);
    }

    public static void CreateBlueprintObject(ZNetScene __instance)
    {
        m_blueprintObject = Object.Instantiate(new GameObject("blueprint_mock"), SettlersPlugin._Root.transform, false);
        ZNetView znv = m_blueprintObject.AddComponent<ZNetView>();
        znv.m_persistent = true;
        znv.m_type = ZDO.ObjectType.Default;
        m_blueprintObject.AddComponent<BluePrinter>();
        if (!__instance.m_prefabs.Contains(m_blueprintObject))
        {
            __instance.m_prefabs.Add(m_blueprintObject);
        }

        __instance.m_namedPrefabs[m_blueprintObject.name.GetStableHashCode()] = m_blueprintObject;
    }
    public static void CreateBaseTerrainObject(ZNetScene __instance)
    {
        m_terrainObject = Object.Instantiate(new GameObject("blueprint_terrain"), SettlersPlugin._Root.transform, false);
        m_terrainObject.name = "Blueprint_Terrain";
        ZNetView znv = m_terrainObject.AddComponent<ZNetView>();
        znv.m_persistent = true;
        znv.m_type = ZDO.ObjectType.Solid;
        TerrainModifier? mod = m_terrainObject.AddComponent<TerrainModifier>();
        mod.m_smooth = true;
        mod.m_levelRadius = 14;
        mod.m_smoothRadius = 20f;
        mod.m_smoothPower = 3f;
        mod.m_paintCleared = true;
        mod.m_paintType = TerrainModifier.PaintType.Dirt;
        mod.m_paintRadius = 2.5f;
        if (!__instance.m_prefabs.Contains(m_terrainObject))
        {
            __instance.m_prefabs.Add(m_terrainObject);
        }

        __instance.m_namedPrefabs[m_terrainObject.name.GetStableHashCode()] = m_terrainObject;
    }
    
    public static void LoadBlueprints()
    {
        BlueprintData RuinedTower = new BlueprintData("RuinedTower.blueprint", Heightmap.Biome.BlackForest);
        RuinedTower.SetCritter("VikingRaider");
        RuinedTower.AddChestItem("DeerStew", 3, 5);
        RuinedTower.AddChestItem("Blueberries", 10, 15);
        RuinedTower.AddChestItem("TinOre", 5, 10, 0.5f);
        RuinedTower.SetRandomDamage(true);
        RuinedTower.m_creature.m_addCreatureSpawners = false;

        BlueprintData MeadowTown1 = new BlueprintData("MeadowSettlerTown1.blueprint", Heightmap.Biome.Meadows);
        MeadowTown1.SetCritter("VikingSettler");
        MeadowTown1.AddChestItem("CookedMeat", 3, 5);
        MeadowTown1.AddChestItem("Honey", 10, 15);
        MeadowTown1.AddChestItem("Wood", 30, 50, 0.5f);
        MeadowTown1.AddChestItem("Flint", 5, 10, 0.5f);
        MeadowTown1.AddChestItem("Raspberry", 5, 10);
        MeadowTown1.AddChestItem("LeatherScrap", 10, 15);
        MeadowTown1.SetChestData(4, 8, true);
        MeadowTown1.SetAdjustment(new Vector3(0f, -5.5f, 0f));
        MeadowTown1.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingSettler";
        
        BlueprintData MeadowTown3 = new BlueprintData("MeadowSettlerTown2.blueprint", Heightmap.Biome.Meadows);
        MeadowTown3.SetCritter("VikingSettler");
        MeadowTown3.AddChestItem("CookedMeat", 3, 5);
        MeadowTown3.AddChestItem("Honey", 2, 3);
        MeadowTown3.AddChestItem("Wood", 5, 10, 0.5f);
        MeadowTown3.AddChestItem("Flint", 5, 10, 0.5f);
        MeadowTown3.AddChestItem("Raspberry", 5, 10);
        MeadowTown3.AddChestItem("LeatherScrap", 1, 5);
        MeadowTown3.SetChestData(1, 2, false);
        MeadowTown3.SetAdjustment(new Vector3(0f, -1f, 0f));
        MeadowTown3.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingSettler";

        BlueprintData MeadowTown2 = new BlueprintData("MeadowRaiderTown1.blueprint", Heightmap.Biome.Meadows);
        MeadowTown2.SetCritter("VikingRaider");
        MeadowTown2.AddChestItem("CookedMeat", 3, 5);
        MeadowTown2.AddChestItem("Honey", 10, 15);
        MeadowTown2.AddChestItem("Wood", 30, 50);
        MeadowTown2.AddChestItem("Flint", 5, 10);
        MeadowTown2.AddChestItem("Raspberry", 5, 10);
        MeadowTown2.AddChestItem("LeatherScrap", 10, 15);
        MeadowTown2.SetChestData(3, 8, true);
        MeadowTown2.SetAdjustment(new Vector3(0f, -28f, 0f));
        MeadowTown2.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingRaider";
        
        BlueprintData BlackForest1 = new BlueprintData("BlackForestRaiderTown1.blueprint", Heightmap.Biome.BlackForest);
        BlackForest1.SetCritter("VikingRaider");
        BlackForest1.AddChestItem("DeerStew", 3, 5);
        BlackForest1.AddChestItem("Blueberries", 10, 15);
        BlackForest1.AddChestItem("TinOre", 5, 10, 0.5f);
        BlackForest1.AddChestItem("MeadHealthPotion", 1, 3, 0.5f);
        BlackForest1.AddChestItem("CopperOre", 5, 10);
        BlackForest1.AddChestItem("Bronze", 1, 5, 0.5f);
        BlackForest1.AddChestItem("BoneFragments", 5, 10, 0.5f);
        BlackForest1.AddChestItem("Wood", 30, 50);
        BlackForest1.SetAdjustment(new Vector3(0f, -1.2f, 0f));
        BlackForest1.SetChestData(4, 8, true);
        BlackForest1.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingRaider";
        BlueprintData SwampTown1 = new BlueprintData("swamptown1.blueprint", Heightmap.Biome.Swamp);
        SwampTown1.SetCritter("VikingRaider");
        SwampTown1.AddChestItem("Sausages", 3, 5);
        SwampTown1.AddChestItem("Thistle", 10, 15);
        SwampTown1.AddChestItem("IronScrap", 5, 10, 0.5f);
        SwampTown1.AddChestItem("Bloodbag", 5, 10);
        SwampTown1.AddChestItem("Wood", 30, 50);
        SwampTown1.AddChestItem("TurnipStew", 5, 10, 0.5f);
        SwampTown1.AddChestItem("MeadPoisonResist", 1, 3, 0.5f);
        SwampTown1.SetChestData(1, 4, true);
        SwampTown1.SetAdjustment(new Vector3(0f, -2.3f, 0f));
        SwampTown1.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingRaider";
        BlueprintData MountTown1 = new BlueprintData("MountainRaiderTown1.blueprint", Heightmap.Biome.Mountain);
        MountTown1.SetCritter("VikingRaider");
        MountTown1.AddChestItem("Sausages", 3, 5);
        MountTown1.AddChestItem("Obsidian", 10, 15);
        MountTown1.AddChestItem("SilvoreOre", 5, 10, 0.5f);
        MountTown1.AddChestItem("FreezeGland", 5, 10);
        MountTown1.AddChestItem("Wood", 30, 50);
        MountTown1.AddChestItem("TurnipStew", 5, 10, 0.5f);
        MountTown1.AddChestItem("MeadStaminaMinor", 1, 3, 0.5f);
        MountTown1.SetChestData(1, 4, true);
        MountTown1.SetAdjustment(new Vector3(0f, -8f, 0f));
        MountTown1.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingRaider";
        BlueprintData PlainsTown1 = new BlueprintData("PlainsRaiderTown1.blueprint", Heightmap.Biome.Plains);
        PlainsTown1.SetCritter("VikingRaider");
        PlainsTown1.AddChestItem("Wood", 30, 50);
        PlainsTown1.AddChestItem("CookedLoxMeat", 3, 5);
        PlainsTown1.AddChestItem("Flax", 10, 15);
        PlainsTown1.AddChestItem("MeadHealthMinor", 3, 5, 0.5f);
        PlainsTown1.AddChestItem("BlackMetalScrap", 5, 10, 0.5f);
        PlainsTown1.AddChestItem("Barley", 10, 15);
        PlainsTown1.AddChestItem("Bread", 3, 5, 0.5f);
        PlainsTown1.AddChestItem("LoxMeatPie", 1, 3, 0.5f);
        PlainsTown1.SetChestData(3, 8, true);
        PlainsTown1.SetAdjustment(new Vector3(0f, -5f, 0f));
        PlainsTown1.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingRaider";
        BlueprintData PlainsRaiderTower1 = new BlueprintData("PlainsRaiderTower1.blueprint", Heightmap.Biome.Plains);
        PlainsRaiderTower1.SetCritter("VikingRaider");
        PlainsRaiderTower1.AddChestItem("Wood", 30, 50);
        PlainsRaiderTower1.AddChestItem("CookedLoxMeat", 3, 5);
        PlainsRaiderTower1.AddChestItem("Flax", 10, 15);
        PlainsRaiderTower1.AddChestItem("MeadHealthMinor", 3, 5, 0.5f);
        PlainsRaiderTower1.AddChestItem("BlackMetalScrap", 5, 10, 0.5f);
        PlainsRaiderTower1.AddChestItem("Barley", 10, 15);
        PlainsRaiderTower1.AddChestItem("Bread", 3, 5, 0.5f);
        PlainsRaiderTower1.AddChestItem("LoxMeatPie", 1, 3, 0.5f);
        PlainsRaiderTower1.SetChestData(3, 8, true);
        PlainsRaiderTower1.SetAdjustment(new Vector3(0f, -1f, 0f));
        PlainsRaiderTower1.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingRaider";
        BlueprintData MistTown1 = new BlueprintData("MistlandRaiderTown1.blueprint", Heightmap.Biome.Mistlands);
        MistTown1.SetCritter("VikingRaider");
        MistTown1.AddChestItem("Sausages", 3, 5);
        MistTown1.AddChestItem("MeadStaminaLingering", 10, 15);
        MistTown1.AddChestItem("CopperScrap", 5, 10, 0.5f);
        MistTown1.AddChestItem("Wood", 30, 50);
        MistTown1.AddChestItem("Bronze", 10, 20, 0.5f);
        MistTown1.AddChestItem("Bread", 10, 20, 0.5f);
        MistTown1.SetChestData(3, 8, true);
        MistTown1.SetAdjustment(new Vector3(0f, -2.5f, 0f));
        MistTown1.m_creature.m_creatureSpawnerPrefab = "Spawner_VikingRaider";
    }

    public class BlueprintData
    {
        public Heightmap.Biome m_biome;
        public Blueprint m_blueprint;
        public ContainerData m_containerData = new();
        public CreatureData m_creature = new();
        public bool m_randomDamage;
        public Vector3 m_adjustments = Vector3.zero;
        public BlueprintData(string fileName, Heightmap.Biome biome)
        {
            m_blueprint = RegisterBlueprint(fileName);
            m_biome = biome;
            m_data.Add(this);
        }
        public void SetAdjustment(Vector3 vector) => m_adjustments = vector;

        public void SetRandomDamage(bool enable) => m_randomDamage = enable;

        public void SetCritter(string creatureName, float levelUpChance = 10f)
        {
            m_creature.m_creatureName = creatureName;
            m_creature.m_levelUpChance = levelUpChance;
        }

        public void AddChestItem(string itemName, int min, int max, float weight = 1f)
        {
            m_containerData.m_drops.Add(new(){m_itemName = itemName, m_min = min, m_max = max, m_weight = weight});
        }

        public void SetChestData(int min, int max, bool oneOfEach, float chance = 1f)
        {
            m_containerData.m_min = min;
            m_containerData.m_max = max;
            m_containerData.m_oneOfEach = oneOfEach;
            m_containerData.m_chance = chance;
        }

        public class CreatureData
        {
            public string m_creatureName = "";
            public int m_minLevel = 1;
            public int m_maxLevel = 3;
            public float m_levelUpChance = 10f;
            public float m_respawnTimeMinutes = 5f;
            public float m_triggerDistance = 120f;
            public bool m_spawnAtNight = true;
            public bool m_spawnAtDay = true;
            public int m_spawnInterval = 120;
            public int m_maxGroupSpawned = 3;
            public bool m_wakeupAnimation = true;
            public float m_spawnGroupRadius = 30f;
            public float m_spawnerWeight = 1f;
            public bool m_setPatrolPoint = true;
            public bool m_spawnInPlayerBase = true;
            public string m_creatureSpawnerPrefab = "";
            public bool m_addCreatureSpawners = true;
            public int m_creatureSpawnerAmount = 3;
        }
        public class ContainerData
        {
            public List<DropData> m_drops = new();
            public int m_min = 3;
            public int m_max = 8;
            public bool m_oneOfEach;
            public float m_chance = 1f;
        }
        public class DropData
        {
            public string m_itemName = "";
            public int m_min = 1;
            public int m_max = 1;
            public float m_weight = 1f;
        }
    }

    private static Blueprint RegisterBlueprint(string fileName)
    {
        string[] data = GetLinesFromFile(fileName);
        if (data.Length == 0) return new Blueprint();
        return ParseFile(data);
    }
    
    private static string[] GetLinesFromFile(string fileName)
    {
        List<string> result = new List<string>();
        TextAsset asset = GetText(fileName);
        if (asset.text.IsNullOrWhiteSpace()) return result.ToArray();
        StringReader reader = new StringReader(asset.text);
        string line;
        while (!(line = reader.ReadLine() ?? string.Empty).IsNullOrWhiteSpace())
        {
            result.Add(line);
        }
        return result.ToArray();
    }
    
    private static Blueprint ParseFile(string[] texts)
    {
        Blueprint blueprint = new();
        bool isPiece = true;
        bool isTerrain = false;
        for (int index = 0; index < texts.Length; index++)
        {
            string text = texts[index];
            if (text.StartsWith("#Name")) blueprint.m_name = ParseData(text);
            else if (text.StartsWith("#Creator")) blueprint.m_creator = ParseData(text);
            else if (text.StartsWith("#Description")) blueprint.m_description = ParseData(text);
            else if (text.StartsWith("#Center")) blueprint.m_center = ParseData(text);
            else if (text.StartsWith("#Coordinates")) blueprint.m_coordinates = ParseVector3(text);
            else if (text.StartsWith("#SnapPoints"))
            {
                isPiece = false;
                isTerrain = false;
            }
            else if (text.StartsWith("#Terrain"))
            {
                isPiece = false;
                isTerrain = true;
            }
            else if (text.StartsWith("#Pieces"))
            {
                isPiece = true;
                isTerrain = false;
            }
            else if (text.StartsWith("#")) { }
            else if (isPiece)
            {
                PlanPiece planPiece = ParsePiece(text);
                blueprint.m_objects.Add(planPiece);
            }
            else if (isTerrain)
            {
                TerrainPiece terrainPiece = ParseTerrain(text);
                blueprint.m_terrain.Add(terrainPiece);
            }
            else
            {
                SnapPoint snapPoint = ParseSnapPoint(text, index);
                blueprint.m_snapPoints.Add(snapPoint);
            }
        }

        return blueprint;
    }
    
    private static SnapPoint ParseSnapPoint(string text, int index)
    {
        SnapPoint snapPoint = new();
        string[] data = text.Split(';');
        snapPoint.m_name = $"snappoint_{index}";
        snapPoint.m_position = new Vector3(
            float.TryParse(data[0], out float x) ? x : 0f, 
            float.TryParse(data[1], out float y) ? y : 0f, 
            float.TryParse(data[2], out float z) ? z : 0f);

        try
        {
            snapPoint.m_name = data[3];
        }
        catch
        {
            // ignored
        }

        return snapPoint;
    }

    private static TerrainPiece ParseTerrain(string text)
    {
        TerrainPiece terrainPiece = new();
        string[] data = text.Split(';');
        terrainPiece.m_shape = data[0];
        terrainPiece.m_position = ParsePieceVector3(data[2], data[3], data[4]);
        terrainPiece.m_radius = data[0] == "FirTree_oldLog" ? 10f : 20f;
        terrainPiece.m_paint = data[0] == "lox_ribs" ? "cultivate" : "dirt";
        // terrainPiece.m_radius = float.TryParse(data[4], out float radius) ? radius : 0f;
        // terrainPiece.m_radius = 10f;
        // terrainPiece.m_rotation = int.TryParse(data[5], out int rotation) ? rotation : 0;
        // terrainPiece.m_smooth = float.TryParse(data[6], out float smooth) ? smooth : 0f;
        // terrainPiece.m_paint = data[7];
        return terrainPiece;
    }
    

    private static PlanPiece ParsePiece(string text)
    {
        PlanPiece planPiece = new();
        string[] data = text.Split(';');
        
        planPiece.m_prefab = data[0];
        planPiece.m_category = data[1];
        planPiece.m_position = ParsePieceVector3(data[2], data[3], data[4]);
        planPiece.m_rotation = ParsePieceRotation(data[5], data[6], data[7], data[8]);

        try
        {
            planPiece.m_scale = ParsePieceVector3(data[10], data[11], data[12]);
        }
        catch
        {
            planPiece.m_scale = Vector3.one;
        }
        planPiece.m_data = data[9];
        if (planPiece.m_data.IsNullOrWhiteSpace())
        {
            try
            {
                planPiece.m_data = data[13];
            }
            catch
            {
                planPiece.m_data = "";
            }
        }
        return planPiece;
    }
    
    private static string ParseData(string text) => text.Split(':')[1];

    private static Vector3 ParsePieceVector3(string strX, string strY, string strZ)
    {
        return new Vector3(
            float.TryParse(strX, out float x) ? x : 0f, 
            float.TryParse(strY, out float y) ? y : 0f, 
            float.TryParse(strZ, out float z) ? z : 0f);
    }

    private static Quaternion ParsePieceRotation(string strX, string strY, string strZ, string strW)
    {
        return new Quaternion(
            float.TryParse(strX, out float x) ? x : 0f, 
            float.TryParse(strY, out float y) ? y : 0f, 
            float.TryParse(strZ, out float z) ? z : 0f, 
            float.TryParse(strW, out float w) ? w : 0f
        );
    }

    private static Vector3 ParseVector3(string text)
    {
        var data = text.Split(':')[1];
        string[] values = data.Split(',');
        return new Vector3(
            float.TryParse(values[0], out float x) ? x : 0f, 
            float.TryParse(values[1], out float y) ? y : 0f, 
            float.TryParse(values[2], out float z) ? z : 0f);
    }
    
    private static TextAsset GetText(string fileName)
    {
        Assembly execAssembly = Assembly.GetExecutingAssembly();
        string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
        using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
        if (stream == null) return new TextAsset();
        using StreamReader reader = new StreamReader(stream);
        string content = reader.ReadToEnd();
        return new TextAsset(content);
    }
    
    public class Blueprint
    {
        public string m_name = null!;
        public string m_creator = "";
        public string m_description = "";
        public string m_center = "";
        public Vector3 m_coordinates = new();
        public Vector3 m_rotation = new();
        public readonly List<PlanPiece> m_objects = new();
        public readonly List<SnapPoint> m_snapPoints = new();
        public readonly List<TerrainPiece> m_terrain = new();
    }
    
    public class TerrainPiece
    {
        public string m_shape = "circle";
        public Vector3 m_position = Vector3.zero;
        public float m_radius = 10f;
        public int m_rotation = 0;
        public float m_smooth = 20f;
        public string m_paint = "Dirt";
    }

    public class PlanPiece
    {
        public string m_prefab = "";
        public string m_category = "";
        public Vector3 m_position = Vector3.zero;
        public Quaternion m_rotation = Quaternion.identity;
        public Vector3 m_scale = Vector3.one;
        public string m_data = "";

        public ZPackage Deserialize()
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(m_data);
            return pkg;
        }
    }

    public class SnapPoint
    {
        public string m_name = "";
        public Vector3 m_position = new();
    }
}