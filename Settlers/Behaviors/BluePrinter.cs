using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Settlers.Managers;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Settlers.Behaviors;
public class BluePrinter : MonoBehaviour
{
    private ZNetView m_nview = null!;
    private static bool m_generated;

    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
    }

    public void SetGenerated(bool generated) => m_nview.GetZDO().Set("BlueprintGenerated".GetStableHashCode(), generated);

    public void GenerateLocation(BlueprintManager.BlueprintData data)
    {
        if (!ZoneSystem.instance) return;
        m_generated = m_nview.GetZDO().GetBool("BlueprintGenerated".GetStableHashCode());
        if (m_generated) return;
        if (!ZoneSystem.instance.m_zones.ContainsKey(ZoneSystem.instance.GetZone(transform.position)))
        {
            SettlersPlugin._Plugin.StartCoroutine(TryGenerate(this, data));
        }
        else
        {
            BuildTerrain(data.m_blueprint.m_terrain, data, transform);
            BuildObjects(data, transform);
            CreateSpawnArea(data, transform);
            m_generated = true;
            SetGenerated(true);
        }
    }

    private static IEnumerator TryGenerate(BluePrinter printer, BlueprintManager.BlueprintData data)
    {
        while (!m_generated)
        {
            if (!ZoneSystem.instance.m_zones.ContainsKey(ZoneSystem.instance.GetZone(printer.transform.position))) yield return new WaitForSeconds(1f);
            BuildTerrain(data.m_blueprint.m_terrain, data, printer.transform);
            BuildObjects(data, printer.transform);
            CreateSpawnArea(data, printer.transform);
            m_generated = true;
            printer.SetGenerated(true);
        }
    }

    private static void BuildTerrain(List<BlueprintManager.TerrainPiece> terrainPieces, BlueprintManager.BlueprintData data, Transform parent)
    {
        foreach (BlueprintManager.TerrainPiece? terrain in terrainPieces)
        {
            GameObject ghost = Instantiate(new GameObject("ghostterrain"), parent);
            ghost.transform.localPosition = new Vector3(terrain.m_position.x, 0f, terrain.m_position.z);
            
            GameObject mod = Instantiate(BlueprintManager.m_terrainObject, ghost.transform.position, Quaternion.identity);
            if (!mod.TryGetComponent(out TerrainModifier terrainModifier)) continue;
            terrainModifier.m_square = false;
            terrainModifier.m_levelRadius = terrain.m_radius;
            terrainModifier.m_smoothRadius = terrain.m_smooth;
            terrainModifier.m_paintType = Enum.TryParse(terrain.m_paint, true, out TerrainModifier.PaintType type)
                ? type
                : TerrainModifier.PaintType.Dirt;
            // terrainModifier.m_paintCleared = !terrain.m_paint.IsNullOrWhiteSpace();
            // terrainModifier.m_paintRadius = terrain.m_radius;
            // terrainModifier.m_paintType = Enum.TryParse(terrain.m_paint, out TerrainModifier.PaintType type)
            // ? type
            // : TerrainModifier.PaintType.Dirt;
        }
    }
    
    private static void BuildObjects(BlueprintManager.BlueprintData data, Transform parent)
    {
        if (!ZNetScene.instance) return;
        foreach (BlueprintManager.PlanPiece piece in data.m_blueprint.m_objects.OrderBy(x => x.m_position.y))
        {
            GameObject prefab = ZNetScene.instance.GetPrefab(piece.m_prefab);
            if (!prefab) continue;
            GameObject ghost = Instantiate(new GameObject(), parent);
            ghost.transform.localPosition = piece.m_position + data.m_adjustments;
            ghost.transform.localRotation = piece.m_rotation;
            ghost.transform.localScale = piece.m_scale;
            
            GameObject clone = Instantiate(prefab, ghost.transform.position, ghost.transform.rotation);
            clone.transform.localScale = piece.m_scale;
            
            SetProperties(clone, piece,  data);
        }
    }

    private static void SetProperties(GameObject prefab, BlueprintManager.PlanPiece piece, BlueprintManager.BlueprintData data)
    {
        SetWearNTear(prefab, data);
        SetBed(prefab, data);
        SetChair(prefab, data);
        SetContainer(prefab, data);
        SetFireplace(prefab);
        SetItemStand(prefab, piece);
    }
    
    private static void SetWearNTear(GameObject prefab, BlueprintManager.BlueprintData data)
    {
        if (!data.m_randomDamage) return;
        if (!prefab.TryGetComponent(out WearNTear component)) return;
        component.m_nview.GetZDO().Set(ZDOVars.s_health, Random.Range(component.m_health * 0.1f, component.m_health * 0.6f));
    }

    private static void SetItemStand(GameObject prefab, BlueprintManager.PlanPiece piece)
    {
        if (!prefab.TryGetComponent(out ItemStand component)) return;
        if (piece.m_data.IsNullOrWhiteSpace()) return;
        GameObject item = ZNetScene.instance.GetPrefab(piece.m_data);
        if (!item) return;
        if (!item.TryGetComponent(out ItemDrop itemDrop)) return;
        ItemDrop.ItemData itemData = itemDrop.m_itemData.Clone();
        int variant = -1;
        if (itemData.m_shared.m_icons.Length > 1)
        {
            variant = Random.Range(0, itemData.m_shared.m_icons.Length);
        }
        itemData.m_variant = variant;
        component.SetVisualItem(piece.m_data, variant, 1);
        itemData.m_stack = 1;
        component.m_nview.GetZDO().Set(ZDOVars.s_item, itemData.m_dropPrefab.name);
        ItemDrop.SaveToZDO(itemData, component.m_nview.GetZDO());
        component.UpdateVisual();
    }

    private static void SetBed(GameObject prefab, BlueprintManager.BlueprintData data)
    {
        if (!prefab.TryGetComponent(out Bed bed) || data.m_creature.m_creatureName.IsNullOrWhiteSpace()) return;
        GameObject? creature = ZNetScene.instance.GetPrefab(data.m_creature.m_creatureName);
        GameObject? spawnPrefab = ZNetScene.instance.GetPrefab(data.m_creature.m_creatureSpawnerPrefab);
        if (!creature || !spawnPrefab) return;
        var spawner = Object.Instantiate(spawnPrefab, prefab.transform.position, Quaternion.identity);
        if (!spawner.TryGetComponent(out CreatureSpawner creatureSpawner)) return;
        creatureSpawner.m_creaturePrefab = creature;
        creatureSpawner.m_minLevel = data.m_creature.m_minLevel;
        creatureSpawner.m_maxLevel = data.m_creature.m_maxLevel;
        creatureSpawner.m_levelupChance = data.m_creature.m_levelUpChance;
        creatureSpawner.m_respawnTimeMinuts = data.m_creature.m_respawnTimeMinutes;
        creatureSpawner.m_triggerDistance =data.m_creature.m_triggerDistance;
        creatureSpawner.m_spawnAtNight = data.m_creature.m_spawnAtNight;
        creatureSpawner.m_spawnAtDay = data.m_creature.m_spawnAtDay;
        creatureSpawner.m_spawnInterval = data.m_creature.m_spawnInterval;
        creatureSpawner.m_maxGroupSpawned = data.m_creature.m_maxGroupSpawned;
        creatureSpawner.m_wakeUpAnimation = data.m_creature.m_wakeupAnimation;
        creatureSpawner.m_spawnGroupRadius = data.m_creature.m_spawnGroupRadius;
        creatureSpawner.m_spawnerWeight = data.m_creature.m_spawnerWeight;
        creatureSpawner.m_setPatrolSpawnPoint = data.m_creature.m_setPatrolPoint;
        creatureSpawner.m_spawnInPlayerBase = data.m_creature.m_spawnInPlayerBase;
    }
    private static void SetChair(GameObject prefab, BlueprintManager.BlueprintData data)
    {
        if (!prefab.TryGetComponent(out Chair chair)) return;
        GameObject? creature = ZNetScene.instance.GetPrefab(data.m_creature.m_creatureName);
        GameObject? spawnPrefab = ZNetScene.instance.GetPrefab(data.m_creature.m_creatureSpawnerPrefab);
        if (!creature || !spawnPrefab) return;
        var spawner = Object.Instantiate(spawnPrefab, prefab.transform.position, Quaternion.identity);
        if (!spawner.TryGetComponent(out CreatureSpawner creatureSpawner)) return;
        creatureSpawner.m_creaturePrefab = creature;
        creatureSpawner.m_minLevel = data.m_creature.m_minLevel;
        creatureSpawner.m_maxLevel = data.m_creature.m_maxLevel;
        creatureSpawner.m_levelupChance = data.m_creature.m_levelUpChance;
        creatureSpawner.m_respawnTimeMinuts = data.m_creature.m_respawnTimeMinutes;
        creatureSpawner.m_triggerDistance =data.m_creature.m_triggerDistance;
        creatureSpawner.m_spawnAtNight = data.m_creature.m_spawnAtNight;
        creatureSpawner.m_spawnAtDay = data.m_creature.m_spawnAtDay;
        creatureSpawner.m_spawnInterval = data.m_creature.m_spawnInterval;
        creatureSpawner.m_maxGroupSpawned = data.m_creature.m_maxGroupSpawned;
        creatureSpawner.m_wakeUpAnimation = data.m_creature.m_wakeupAnimation;
        creatureSpawner.m_spawnGroupRadius = data.m_creature.m_spawnGroupRadius;
        creatureSpawner.m_spawnerWeight = data.m_creature.m_spawnerWeight;
        creatureSpawner.m_setPatrolSpawnPoint = data.m_creature.m_setPatrolPoint;
        creatureSpawner.m_spawnInPlayerBase = data.m_creature.m_spawnInPlayerBase;
    }

    private static void SetContainer(GameObject prefab, BlueprintManager.BlueprintData data)
    {
        if (!prefab.TryGetComponent(out Container containerComponent)) return;
        containerComponent.m_defaultItems = GetDropTable(data);
        containerComponent.GetInventory().RemoveAll();
        containerComponent.AddDefaultItems();
    }

    private static void CreateSpawnArea(BlueprintManager.BlueprintData data, Transform parent)
    {
        if (!ZNetScene.instance) return;
        if (!data.m_creature.m_addCreatureSpawners) return;
        if (data.m_creature.m_creatureSpawnerPrefab.IsNullOrWhiteSpace()) return;
        GameObject spawner = ZNetScene.instance.GetPrefab(data.m_creature.m_creatureSpawnerPrefab);
        if (!spawner) return;
        int count = 0;
        int max = data.m_creature.m_creatureSpawnerAmount;
        while (count < max)
        {
            Vector2 point = Random.insideUnitCircle * 15f;
            Vector3 pos = parent.transform.position + new Vector3(point.x, 0f, point.y);
            Instantiate(spawner, pos, Quaternion.identity);
            ++count;
        }
    }

    private static void SetFireplace(GameObject prefab)
    {
        if (prefab.TryGetComponent(out Fireplace fireplaceComponent))
        {
            fireplaceComponent.AddFuel(fireplaceComponent.m_maxFuel / 2);
        }
    }

    private static DropTable GetDropTable(BlueprintManager.BlueprintData data)
    {
        DropTable table = new DropTable
        {
            m_dropMin = data.m_containerData.m_min,
            m_dropMax = data.m_containerData.m_max,
            m_oneOfEach = data.m_containerData.m_oneOfEach,
            m_dropChance = data.m_containerData.m_chance
        };
        List<DropTable.DropData> list = new();
        foreach (BlueprintManager.BlueprintData.DropData info in data.m_containerData.m_drops)
        {
            GameObject? item = ZNetScene.instance.GetPrefab(info.m_itemName);
            if (!item) continue;
            list.Add(new DropTable.DropData()
            {
                m_item = item,
                m_stackMin = info.m_min,
                m_stackMax = info.m_max,
                m_weight = info.m_weight
            });
        }

        table.m_drops = list;
        return table;
    }
}