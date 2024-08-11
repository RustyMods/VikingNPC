using System;
using System.Collections.Generic;
using System.Linq;
using Settlers.Settlers;
using UnityEngine;

namespace Settlers.Behaviors;

public class ShipMan : MonoBehaviour, IDestructible
{
    public Action? m_onDestroyed;
    public Action? m_onDamaged;
    public GameObject? m_new;
    public GameObject? m_worn;
    public GameObject? m_broken;

    public float m_health = 1000f;
    public HitData.DamageModifiers m_damages = new HitData.DamageModifiers()
    {
        m_chop = HitData.DamageModifier.Weak,
        m_fire = HitData.DamageModifier.Resistant,
    };
    public EffectList m_destroyedEffect = new();
    public EffectList m_hitEffect = new();
    public EffectList m_fireEffect = new();
    public float m_destroyNoise;
    public List<ShipMan> m_instances = new();
    public ZNetView m_nview = null!;
    public WaterVolume m_previousWaterVolume;
    public Heightmap.Biome m_biome;
    private ShipAI m_shipAI = null!;
    private float m_burnDamageTime;
    private float m_updateBiomeTimer;
    private float m_healthPercentage;
    private float m_healthRegenTimer;
    private Container m_container = null!;
    private uint m_lastRevision;
    private string m_lastDataString = "";
    private bool m_loading;
    public List<Transform> m_burnObjects = new();
    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_shipAI = GetComponent<ShipAI>();
        if (m_nview.GetZDO() == null) return;
        m_nview.Register<HitData>(nameof(RPC_Damage), RPC_Damage);
        m_nview.Register<float>(nameof(RPC_HealthChanged), RPC_HealthChanged);
        m_nview.Register(nameof(RPC_CreateFragments), RPC_CreateFragments);
        m_nview.Register<float, bool>(nameof(RPC_Heal), RPC_Heal);
        m_health += Game.m_worldLevel * Game.instance.m_worldLevelPieceHPMultiplier;
        SetHealth(m_health);
        m_biome = Heightmap.FindBiome(transform.position);
        m_container = GetComponentInChildren<Container>();
        
        Transform customize = gameObject.transform.Find("ship/visual/Customize");
        foreach (Transform child in customize)
        {
            m_burnObjects.Add(child);
        }
        
        m_instances.Add(this);
    }

    private void Start()
    {
        AddLoot();
    }

    private void AddLoot()
    {
        if (!m_container) return;
        // LoadInventory();
        if (m_container.GetInventory().GetAllItems().Count > 0) return;
        List<DropTable.DropData> data = new();
        foreach (var item in RaiderDrops.GetRaiderDrops(Heightmap.Biome.Swamp))
        {
            data.Add(new DropTable.DropData()
            {
                m_item = item.m_prefab,
                m_weight = item.m_chance,
                m_stackMax = item.m_amountMax * 3,
                m_stackMin = item.m_amountMin * 3,
                m_dontScale = item.m_dontScale
            });
        }

        var iron = ZNetScene.instance.GetPrefab("IronScrap");
        int count = 0;
        int max = 3;
        while (count < max)
        {
            data.Add(new DropTable.DropData()
            {
                m_item = iron,
                m_weight = 1f,
                m_stackMax = 30,
                m_stackMin = 30,
                m_dontScale = true,
            });
            ++count;
        }
        m_container.m_defaultItems = new DropTable()
        {
            m_dropMin = 5,
            m_dropMax = m_container.m_height * m_container.m_width,
            m_oneOfEach = true,
            m_dropChance = 1f,
            m_drops = data
        };
            
        m_container.AddDefaultItems();
        // m_inventory.m_onChanged += SaveInventory;
    }
    
    private void LoadInventory()
    {
        if (m_nview.GetZDO().DataRevision == m_lastRevision) return;
        string? data = m_nview.GetZDO().GetString(ZDOVars.s_items);
        if (string.IsNullOrEmpty(data) || m_lastDataString == data) return;
        ZPackage pkg = new ZPackage(data);
        m_loading = true;
        m_container.m_inventory.Load(pkg);
        m_loading = false;
        m_lastRevision = m_nview.GetZDO().DataRevision;
        m_lastDataString = data;
    }
    
    private void SaveInventory()
    {
        if (m_loading || !m_nview.IsOwner()) return;
        ZPackage pkg = new ZPackage();
        m_container.m_inventory.Save(pkg);
        string? data = pkg.GetBase64();
        m_nview.GetZDO().Set(ZDOVars.s_items, data);
        m_lastRevision = m_nview.GetZDO().DataRevision;
        m_lastDataString = data;
    }
    
    public void Update()
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner()) return;
        float deltaTime = Time.deltaTime;
        UpdateCurrentBiome(deltaTime);
        UpdateBurn(deltaTime);
        UpdateHealthRegen(deltaTime);
    }
    
    private void UpdateHealthRegen(float dt)
    {
        m_healthRegenTimer += dt;
        if (m_healthRegenTimer < 5f) return;
        m_healthRegenTimer = 0.0f;
        if (GetHealth() >= GetMaxHealth()) return;
        Heal(10f);
        UpdateVisual(true);
    }

    private void Heal(float hp, bool showText = true)
    {
        if (hp <= 0.0) return;
        if (!m_nview.IsOwner())
        {
            m_nview.InvokeRPC(nameof(RPC_Heal), hp, showText);
        }
        else
        {
            RPC_Heal(0L, hp, showText);
        }
    }

    private void RPC_Heal(long sender, float hp, bool showText)
    {
        if (!m_nview.IsOwner()) return;
        float heath1 = GetHealth();
        if (heath1 <= 0.0) return;
        float health2 = Mathf.Min(heath1 + hp, GetMaxHealth());
        if (health2 <= heath1) return;
        SetHealth(health2);
        if (!showText) return;
        DamageText.instance.ShowText(DamageText.TextType.Heal, m_shipAI.m_body.worldCenterOfMass, hp);
    }

    private float GetMaxHealth()
    {
        return 10000f;
    }

    private readonly List<GameObject> m_fireEffects = new();
    private bool m_isBurning;

    private void ClearFireEffects()
    {
        foreach (var effect in m_fireEffects)
        {
            if (effect == null) continue;
            if (!effect.TryGetComponent(out ZNetView zNetView)) continue;
            zNetView.ClaimOwnership();
            zNetView.Destroy();
        }
        m_fireEffects.Clear();
    }
    private void UpdateBurn(float dt)
    {
        if (m_burnDamageTime <= 0.0)
        {
            ClearFireEffects();
            m_isBurning = false;
            return;
        }

        m_burnDamageTime -= dt;
        if (m_isBurning) return;
        m_fireEffects.AddRange(m_fireEffect.Create(m_shipAI.m_mastObject.transform.position, Quaternion.identity, m_shipAI.m_mastObject.transform).ToList());
        m_fireEffects.AddRange(m_fireEffect.Create(m_shipAI.m_sailObject.transform.position, Quaternion.identity, m_shipAI.m_sailObject.transform).ToList());
        foreach (var prefab in m_burnObjects)
        {
            if (prefab == null) continue;
            m_fireEffects.AddRange(m_fireEffect.Create(prefab.position, Quaternion.identity, prefab));
        }
        m_isBurning = true;
    }

    private void UpdateCurrentBiome(float dt)
    {
        m_updateBiomeTimer += dt;
        if (m_updateBiomeTimer < 30f) return;
        m_updateBiomeTimer = 0.0f;
        m_biome = Heightmap.FindBiome(transform.position);
    }

    public Heightmap.Biome GetCurrentBiome() => m_biome;

    public void OnDestroy()
    {
        m_instances.Remove(this);
    }
    
    public bool IsUnderWater() => Floating.IsUnderWater(transform.position, ref m_previousWaterVolume);

    public float GetHealth() => m_nview.GetZDO().GetFloat(ZDOVars.s_health, m_health);

    public void RPC_Damage(long sender, HitData hit)
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner()) return;
        if (hit.GetAttacker() && hit.GetAttacker() is Companion companion && companion.IsSailor()) return;
        if (GetHealth() <= 0.0) return;
        hit.ApplyResistance(m_damages, out HitData.DamageModifier significantModifier);
        float totalDamage = hit.GetTotalDamage();
        DamageText.instance.ShowText(significantModifier, hit.m_point, totalDamage);
        if (totalDamage <= 0.0) return;
        ApplyDamage(totalDamage, hit);
        m_hitEffect.Create(hit.m_point, Quaternion.identity, transform);
        m_onDamaged?.Invoke();
        UpdateVisual(true);
        if (hit.m_damage.m_fire <= 0) return;
        if (!m_isBurning) m_burnDamageTime = 100f;
    }
    
    private bool ApplyDamage(float totalDamage, HitData hit)
    {
        float num1 = GetHealth();
        if (num1 <= 0.0) return false;
        float num2 = num1 - totalDamage;
        SetHealth(num2);
        if (num2 <= 0.0)
        {
            Destroy(hit);
        }
        else
        {
            m_nview.InvokeRPC(ZNetView.Everybody, nameof(RPC_HealthChanged), num2);
        }
        return true;
    }

    private void Destroy(HitData? hit = null)
    {
        SetHealth(0.0f);
        m_health = 0.0f;
        // drop resources
        m_onDestroyed?.Invoke();
        var transform1 = transform;
        if (m_destroyNoise > 0.0 && hit is not { m_hitType: HitData.HitType.CinderFire })
        {
            Player closestPlayer = Player.GetClosestPlayer(transform1.position, 10f);
            closestPlayer.AddNoise(m_destroyNoise);
        }
        m_destroyedEffect.Create(transform1.position, transform1.rotation, transform1);
        m_nview.InvokeRPC(ZNetView.Everybody, nameof(RPC_CreateFragments));
        ZNetScene.instance.Destroy(gameObject);
    }

    private void SetHealth(float heath) => m_nview.GetZDO().Set(ZDOVars.s_health, heath);

    public void RPC_HealthChanged(long sender, float health)
    {
        float health1 = health / m_health;
        m_healthPercentage = Mathf.Clamp01(health / m_health);
        SetHealthVisual(health1, true);
    }

    public void UpdateVisual(bool triggerEffects)
    {
        if (!m_nview.IsValid()) return;
        SetHealthVisual(GetHealthPercentage(), triggerEffects);
    }

    private void SetHealthVisual(float health, bool triggerEffects)
    {
        if (m_worn == null || m_broken == null || m_new == null) return;
        if (!triggerEffects) return;
        if (health > 0.75)
        {
            m_worn.SetActive(false);
            m_broken.SetActive(false);
            m_new.SetActive(true);
        }
        else if (health > 0.25)
        {
            m_new.SetActive(false);
            m_broken.SetActive(false);
            m_worn.SetActive(true);
        }
        else
        {
            m_new.SetActive(false);
            m_worn.SetActive(false);
            m_broken.SetActive(true);
        }
    }

    public float GetHealthPercentage() => !m_nview.IsValid() ? 1f : m_healthPercentage;
    
    public void RPC_CreateFragments(long sender)
    {
        Destructible.CreateFragments(gameObject);
    }

    public void Damage(HitData hit)
    {
        if (!m_nview.IsValid()) return;
        m_nview.InvokeRPC(nameof(RPC_Damage), hit);
    }

    public DestructibleType GetDestructibleType() => DestructibleType.Default;
}