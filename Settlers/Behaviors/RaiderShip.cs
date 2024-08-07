// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.Serialization;
//
// namespace Settlers.Behaviors;
//
// public class RaiderShip : MonoBehaviour, IDestructible, Hoverable, IWaterInteractable, IMonoUpdater
// {
//     public static List<RaiderShip> m_instances = new();
//
//     public float m_underWorldCheckTimer;
//     public float currentRotSpeedFactor;
//     [FormerlySerializedAs("m_view")] public ZNetView m_nview = null!;
//     public Action<float, Character?>? m_onDamaged;
//     public Action? m_onDeath;
//     public string m_name = "";
//     public string m_group = "RaiderShips";
//     public bool m_boss = true;
//     public bool m_dontHideBossHud;
//     public string m_bossEvent = "";
//     public string m_defeatSetGlobalKey = "";
//     public bool m_aiSkipTarget;
//     public float m_speed = 10f;
//     public float m_turnSpeed = 300f;
//     public float m_runSpeed = 20f;
//     public float m_runTurnSpeed = 300f;
//     public float m_acceleration = 1f;
//     public EffectList m_hitEffects = new();
//     public EffectList m_deathEffects = new();
//     public float m_health = 1000f;
//     public HitData.DamageModifiers m_damageModifiers;
//     public Vector3 m_moveDir = Vector3.zero;
//     public Vector3 m_lookDir = Vector3.forward;
//     public Vector3 m_lookTransitionStart;
//     public Vector3 m_lookTransitionTarget;
//     public float m_lookTransitionTime;
//     public float m_lookTransitionTimeTotal;
//     public float m_backstabTime;
//     public bool m_attack;
//     public bool m_attackHold;
//     public LODGroup m_lodGroup = null!;
//     public GameObject m_new = null!;
//     public GameObject? m_worn;
//     public GameObject? m_broken;
//     public RaiderShipAI m_raiderShipAI = null!;
//     public List<Companion> m_riders = new();
//     public HitData m_lastHit;
//     public Vector3 m_currentVel = Vector3.zero;
//     public float m_currentTurnVel;
//     public float m_currentTurnVelChange;
//     public bool m_lodVisible = true;
//     public Vector3 m_originalLocalRef;
//     public Rigidbody m_rigidbody;
//     public List<Transform> m_attachPoints = new();
//     public int m_level;
//     public float m_waterLevel;
//     public float m_liquidLevel;
//     public int[] m_liquids = new int[2];
//     public void Awake()
//     {
//         m_instances.Add(this);
//         m_rigidbody = GetComponent<Rigidbody>();
//         m_nview = GetComponent<ZNetView>();
//         m_raiderShipAI = GetComponent<RaiderShipAI>();
//         m_lodGroup = GetComponent<LODGroup>();
//         if (m_lodGroup)
//         {
//             m_originalLocalRef = m_lodGroup.localReferencePoint;
//         }
//
//         if (m_nview.IsOwner())
//         {
//             SetupMaxHealth();
//         }
//         m_level = m_nview.GetZDO().GetInt(ZDOVars.s_level, 1);
//     }
//
//     public void OnDestroy()
//     {
//         m_instances.Remove(this);
//     }
//
//     public void SetupMaxHealth()
//     {
//         int level = GetLevel();
//         SetMaxHealth(GetMaxHealthBase() * level);
//     }
//
//     public void SetMaxHealth(float health)
//     {
//         if (!m_nview.IsValid()) return;
//         m_nview.GetZDO().Set(ZDOVars.s_maxHealth, health);
//         if (GetHealth() <= health) return;
//         SetHealth(health);
//     }
//
//     public void SetHealth(float health)
//     {
//         if (!m_nview.IsValid()) return;
//         if (health >= GetMaxHealth()) return;
//         if (health < 0.0) health = 0.0f;
//         m_nview.GetZDO().Set(ZDOVars.s_health, health);
//     }
//
//     public void SetLevel(int level)
//     {
//         if (level < 1) return;
//         m_level = level;
//         m_nview.GetZDO().Set(ZDOVars.s_level, level, false);
//         SetupMaxHealth();
//     }
//
//     public string GetGroup() => m_group;
//
//     public void UpdateWorldCheck(float dt)
//     {
//         if (IsDead()) return;
//         m_underWorldCheckTimer += dt;
//         if (m_underWorldCheckTimer <= 5.0) return;
//         m_underWorldCheckTimer = 0.0f;
//         float groundHeight = ZoneSystem.instance.GetGroundHeight(transform.position);
//         if (transform.position.y >= groundHeight - 1.0) return;
//         Vector3 position = transform.position with
//         {
//             y = groundHeight + 0.5f
//         };
//         transform.position = position;
//         m_rigidbody.position = position;
//         m_rigidbody.velocity = Vector3.zero;
//     }
//
//     public bool IsDead() => false;
//
//     public float GetMaxHealthBase()
//     {
//         return m_health * Game.m_worldLevel * Game.instance.m_worldLevelEnemyHPMultiplier;
//     }
//
//     public int GetLevel() => 1;
//
//     public Vector3 GetCenterPoint()
//     {
//         return Vector3.zero;
//     }
//     public float GetMaxHealth() => 3000f;
//     
//     public void Heal(float hp, bool showText)
//     {
//         if (hp <= 0.0) return;
//         if (!m_nview.IsOwner())
//         {
//             m_nview.InvokeRPC(nameof(RPC_Heal), hp, showText);
//         }
//         else
//         {
//             RPC_Heal(0L, hp, showText);
//         }
//     }
//
//     public void RPC_Heal(long sender, float hp, bool showText)
//     {
//         if (!m_nview.IsOwner()) return;
//         float health1 = GetHealth();
//         float health2 = Mathf.Min(health1 + hp, GetMaxHealth());
//         if (health2 <= health1) return;
//         SetHealth(health2);
//         if (!showText) return;
//         DamageText.instance.ShowText(DamageText.TextType.Heal, GetCenterPoint(), hp, false);
//     }
//
//     public void SetMoveDir(Vector3 pos)
//     {
//         m_moveDir = pos;
//     }
//
//     public bool HaveRider() => m_riders.Count > 0;
//
//     public float GetHealth()
//     {
//         if (!m_nview.IsValid()) return GetMaxHealth();
//         return m_nview.GetZDO().GetFloat(ZDOVars.s_health, GetMaxHealth());
//     }
//
//     public void SetLookDir(Vector3 dir, float transitionTime = 0.0f)
//     {
//         if (transitionTime > 0.0f)
//         {
//             
//         }
//         else
//         {
//             if (dir.magnitude <= Mathf.Epsilon)
//             {
//                 dir = transform.forward;
//             }
//             else
//             {
//                 dir.Normalize();
//                 m_lookDir = dir;
//                 dir.y = 0.0f;
//             }
//         }
//     }
//
//     public bool InAttack()
//     {
//         return false;
//     }
//
//     public float GetRadius()
//     {
//         return 10f;
//     }
//     
//     public void Damage(HitData hit)
//     {
//         if (!m_nview.IsValid()) return;
//         m_nview.InvokeRPC(nameof(RPC_Damage), hit);
//     }
//
//     public void RPC_Damage(long sender, HitData hit)
//     {
//         if (hit.GetAttacker() == Player.m_localPlayer)
//         {
//             Game.instance.IncrementPlayerStat(PlayerStatType.EnemyHits);
//         }
//         if (m_raiderShipAI == null) return;
//
//         if (!m_nview.IsOwner() || GetHealth() <= 0.0 || IsDead()) return;
//
//         Character attacker = hit.GetAttacker();
//         if (hit.HaveAttacker() && attacker == null) return;
//         if (attacker != null && !attacker.IsPlayer())
//         {
//             float damageScalePlayer = Game.instance.GetDifficultyDamageScalePlayer(transform.position);
//             hit.ApplyModifier(damageScalePlayer);
//             hit.ApplyModifier(Game.m_enemyDamageRate);
//         }
//
//         if (attacker != null)
//         {
//             if (m_raiderShipAI.IsAggravatable() && !m_raiderShipAI.IsAggravated() && attacker.IsPlayer() && hit.GetTotalDamage() > 0.0)
//             {
//                 RaiderShipAI.AggravateAllInArea(transform.position, 20f, BaseAI.AggravatedReason.Damage);
//             }
//             if (!m_raiderShipAI.IsAlerted() && hit.m_backstabBonus > 1.0 &&
//                 Time.time - m_backstabTime > 300.0 && 
//                 (!ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs) || !m_raiderShipAI.CanSeeTarget(attacker)))
//             {
//                 m_backstabTime = Time.time;
//                 hit.ApplyModifier(hit.m_backstabBonus);
//             }
//         }
//
//         float total = hit.GetTotalDamage();
//         float poison = hit.m_damage.m_poison;
//         float fire = hit.m_damage.m_fire;
//         float spirit = hit.m_damage.m_spirit;
//         
//         ApplyDamage(hit, true, true);
//         if (m_onDamaged != null) m_onDamaged(total, attacker);
//         m_raiderShipAI.OnDamaged(total, attacker);
//     }
//
//     public void ApplyDamage(HitData hit, bool showDamageText, bool triggerEffects, HitData.DamageModifier mod = HitData.DamageModifier.Normal)
//     {
//         
//     }
//
//     public DestructibleType GetDestructibleType() => DestructibleType.Default;
//
//     public string GetHoverText() => "";
//
//     public string GetHoverName() => m_name;
//
//     public void SetLiquidLevel(float level, LiquidType type, Component liquidObj)
//     {
//         m_liquidLevel = level;
//     }
//
//     public Transform GetTransform() => transform;
//
//     public int Increment(LiquidType type)
//     {
//         return ++m_liquids[(int)type];
//     }
//
//     public int Decrement(LiquidType type)
//     {
//         return --m_liquids[(int)type];
//     }
//
//     public void CustomFixedUpdate(float deltaTime)
//     {
//
//     }
//
//     public void CustomUpdate(float deltaTime, float time)
//     {
//
//     }
//
//     public void CustomLateUpdate(float deltaTime)
//     {
//         
//     }
// }