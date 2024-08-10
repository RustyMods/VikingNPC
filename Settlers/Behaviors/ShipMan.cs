using System;
using System.Collections.Generic;
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
    public HitData.DamageModifiers m_damages;
    public EffectList m_destroyedEffect = new();
    public EffectList m_hitEffect = new();
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
        m_instances.Add(this);
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
        UpdateVisual(false);
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
        return 1000f;
    }

    private void UpdateBurn(float dt)
    {
        if (m_burnDamageTime <= 0.0) return;
        m_burnDamageTime -= dt;
        HitData hit = new HitData();
        hit.m_damage.m_fire = 1f;
        Damage(hit);
    }

    private void UpdateCurrentBiome(float dt)
    {
        m_updateBiomeTimer += dt;
        if (m_updateBiomeTimer < 30f) return;
        m_updateBiomeTimer = 0.0f;
        m_biome = Heightmap.FindBiome(transform.position);
    }

    public void OnDestroy()
    {
        m_instances.Remove(this);
    }
    
    public bool IsUnderWater() => Floating.IsUnderWater(transform.position, ref m_previousWaterVolume);

    public float GetHealth() => m_nview.GetZDO().GetFloat(ZDOVars.s_health, m_health);

    public void RPC_Damage(long sender, HitData hit)
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner()) return;
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
        m_burnDamageTime = 3f;
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
        if (health > 0.75)
        {
            if (m_worn != m_new) m_worn.SetActive(false);
            if (m_broken != m_new) m_broken.SetActive(false);
            m_new.SetActive(true);
        }
        else if (health > 0.25)
        {
            if (triggerEffects && !m_worn.activeSelf) 
                if (m_new != m_worn) m_new.SetActive(false);
            if (m_broken != m_worn) m_broken.SetActive(false);
            m_worn.SetActive(true);
        }
        else
        {
            if (triggerEffects && !m_broken.activeSelf)
                if (m_new != m_broken) m_new.SetActive(false);
            if (m_worn != m_broken) m_worn.SetActive(false);
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