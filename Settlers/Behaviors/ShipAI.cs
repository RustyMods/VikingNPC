using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Settlers.Behaviors;

public class ShipAI : MonoBehaviour, IUpdateAI
{
    private readonly int m_spawnedSailorsKey = "SpawnedSailorsKey".GetStableHashCode();
    public float m_sendRudderTime;
    public GameObject m_sailObject = null!;
    public GameObject m_mastObject = null!;
    public GameObject m_rudderObject = null!;
    public BoxCollider m_floatCollider = null!;
    public float m_waterLevelOffset;
    public float m_forceDistance = 1f;
    public float m_force = 0.5f;
    public float m_damping = 0.05f;
    public float m_dampingSideway = 0.05f;
    public float m_dampingForward = 0.01f;
    public float m_angularDamping = 0.01f;
    public float m_disableLevel = -0.5f;
    public float m_sailForceOffset;
    public float m_sailForceFactor = 0.1f;
    public float m_rudderSpeed = 0.5f;
    public float m_stearForceOffset = -10f;
    public float m_stearForce = 0.5f;
    public float m_stearVelForceFactor = 0.1f;
    public float m_backwardForce = 50f;
    public float m_rudderRotationMax = 30f;
    public float m_minWaterImpactForce = 2.5f;
    public float m_minWaterImpactInterval = 2f;
    public float m_waterImpactDamage = 10f;
    public EffectList m_waterImpactEffect = new EffectList();
    public bool m_sailWasInPosition;
    public Vector3 m_windChangeVelocity = Vector3.zero;
    public Ship.Speed m_speed;
    public float m_rudder;
    public float m_rudderValue;
    public Vector3 m_sailForce = Vector3.zero;
    public WaterVolume m_previousCenter = null!;
    public WaterVolume m_previousLeft = null!;
    public WaterVolume m_previousRight = null!;
    public WaterVolume m_previousForward = null!;
    public WaterVolume m_previousBack = null!;
    public static readonly List<ShipAI> s_currentShipAIs = new List<ShipAI>();
    public GlobalWind m_globalWind = null!;
    public Rigidbody m_body = null!;
    public ZNetView m_nview = null!;
    public IDestructible m_destructible = null!;
    public Cloth m_sailCloth = null!;
    public float m_lastDepth = -9999f;
    public float m_lastWaterImpactTime;
    public float m_upsideDownDmgTimer;
    public float m_rudderPaddleTimer;
    public float m_lastUpdateWaterForceTime;
    public List<Transform> m_attachPoints = new();
    public readonly Dictionary<Transform, Companion> m_sailors = new();
    private float m_lastFindPathTime;
    private bool m_lastFindPathResult;
    private Vector3 m_lastFindPathTarget;
    private Ship? m_currentShipTarget;
    private readonly List<Vector3> m_path = new();
    public static readonly List<IUpdateAI> Instances = new();
    private bool m_attachedToTarget;
    private ShipMan m_shipMan;
    private readonly List<Player> m_players = new();
    private float m_checkSailorTimer;
    public void Awake()
    {
        m_sailObject = Utils.FindChild(transform, "Sail").gameObject;
        m_mastObject = Utils.FindChild(transform, "Mast").gameObject;
        m_rudderObject = Utils.FindChild(transform, "rudder").gameObject;
        m_floatCollider = transform.Find("FloatCollider").GetComponent<BoxCollider>();

        m_nview = GetComponent<ZNetView>();
        m_body = GetComponent<Rigidbody>();
        m_destructible = GetComponent<IDestructible>();
        m_shipMan = GetComponent<ShipMan>();

        if (m_nview.GetZDO() == null)
        {
            enabled = false;
        }
        m_body.maxDepenetrationVelocity = 2f;
        Heightmap.ForceGenerateAll();
        m_sailCloth = m_sailObject.GetComponentInChildren<Cloth>();
        if (m_sailCloth)
        {
            m_globalWind = m_sailCloth.gameObject.GetComponent<GlobalWind>();
        }

        if (m_nview.IsValid())
        {
            m_nview.GetZDO().Set(ZDOVars.s_patrolPoint, transform.position);
        }
        Invoke(nameof(GetSailors), 5f);
        s_currentShipAIs.Add(this);
    }
    public void OnEnable() => Instances.Add(this);
    public void OnDisable() => Instances.Remove(this);

    private void Destroy()
    {
        if (m_players.Count > 0) return;
        foreach (Companion sailor in m_sailors.Values)
        {
            sailor.m_nview.Destroy();
        }
        m_sailors.Clear();
        m_nview.Destroy();
    }

    private void OnDestroy()
    {
        s_currentShipAIs.Remove(this);
    }
    
    public bool UpdateAI(float fixedDeltaTime)
    {
        SailTo(fixedDeltaTime);
        UpdateSpeed(fixedDeltaTime);
        UpdateSail(fixedDeltaTime);
        UpdateRudder(fixedDeltaTime);
        if (m_nview && !m_nview.IsOwner()) return false;
        UpdateCheckSailors(fixedDeltaTime);
        CalculateForces(fixedDeltaTime);
        ApplyEdgeForce(fixedDeltaTime);
        UpdateUpsideDmg(fixedDeltaTime);
        return true;
    }

    public void UpdateCheckSailors(float dt)
    {
        m_checkSailorTimer += dt;
        if (m_checkSailorTimer < 10f) return;
        m_checkSailorTimer = 0.0f;
        List<Transform> keysToRemove = new();
        foreach (var kvp in m_sailors)
        {
            if (kvp.Value == null || kvp.Value.IsDead())
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            m_sailors.Remove(key);
        }
        
        SettlersPlugin.SettlersLogger.LogWarning("Current sailor count: " + m_sailors.Count);

        if (m_sailors.Count <= 0)
        {
            m_destructible.Damage(new HitData()
            {
                m_damage = new HitData.DamageTypes()
                {
                    m_fire = 5f,
                    m_blunt = 20f
                }
            });
        }
    }
    
    public void UpdateUpsideDmg(float dt)
    {
        if (transform.up.y >= 0.0) return;
        m_upsideDownDmgTimer += dt;
        if (m_upsideDownDmgTimer <= 1f) return;
        m_upsideDownDmgTimer = 0.0f;
        m_destructible.Damage(new HitData()
        {
            m_damage = {
                m_blunt = 20f
            },
            m_point = transform.position,
            m_dir = Vector3.up
        });
    }

    private void UpdateSailors()
    {
        if (m_sailors.Count > 4) return;
        var sailor = ZNetScene.instance.GetPrefab("VikingSailor");
        if (!sailor) return;
        foreach (var attachPoint in m_attachPoints)
        {
            if (m_sailors.ContainsKey(attachPoint)) continue;
            var raider = Instantiate(sailor, attachPoint.transform.position, Quaternion.identity);
            if (!raider.TryGetComponent(out Companion companion)) continue;
            companion.AttachStart(attachPoint, null, false, false, true, "", new Vector3(0.0f, 0.5f, 0.0f));
            m_sailors[attachPoint] = companion;
        }
    }
    private void GetSailors()
    {
        if (m_nview.GetZDO().GetBool(m_spawnedSailorsKey))
        {
            int index = 0;
            foreach (var companion in Companion.m_instances)
            {
                if (!companion.IsSailor()) continue;
                if (index > m_attachPoints.Count - 1) break;
                companion.AttachStart(m_attachPoints[index], null, false, false, true, "", new Vector3(0.0f, 0.5f, 0.0f));
                m_sailors[m_attachPoints[index]] = companion;
                ++index;
            }
            UpdateSailors();
        }
        else
        {
            var sailor = ZNetScene.instance.GetPrefab("VikingSailor");
            if (!sailor) return;
            foreach (Transform attachPoint in m_attachPoints)
            {
                var raider = Instantiate(sailor, attachPoint.transform.position, Quaternion.identity);
                if (!raider.TryGetComponent(out Companion companion)) continue;
                companion.AttachStart(attachPoint, null, false, false, true, "", new Vector3(0.0f, 0.5f, 0.0f));
                m_sailors[attachPoint] = companion;
            }
            m_nview.GetZDO().Set(m_spawnedSailorsKey, true);
        }
    }

    private Vector3 GetPatrolPoint() => m_nview.GetZDO().GetVec3(ZDOVars.s_patrolPoint, transform.position);
    private void SailTo(float dt)
    {
        if (!m_nview.IsValid()) return;
        Vector3 patrolPoint = GetPatrolPoint();
        Ship? target = FindShipTarget();
        bool flag = Vector3.Distance(patrolPoint, transform.position) > 400f || m_shipMan.GetCurrentBiome() != Heightmap.Biome.Ocean;
        var sailToPosition = target == null ? patrolPoint : flag ? patrolPoint : target.transform.position;

        if (!FindPath(sailToPosition))
        {
            return;
        }

        if (m_path.Count == 0)
        {
            return;
        }

        Vector3 vector3 = m_path[0];
        if (Utils.DistanceXZ(vector3, transform.position) < 5f)
        {
            m_path.RemoveAt(0);
            if (m_path.Count == 0)
            {
                return;
            }
        }
        RotateTowards(vector3, dt);
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Player player)) return;
        m_players.Add(player);
        SettlersPlugin.SettlersLogger.LogDebug("Player boarded raider ship, total: " + m_players.Count);
    }

    public void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out Player player)) return;
        m_players.Remove(player);
        SettlersPlugin.SettlersLogger.LogDebug("Player left raider ship, total: " + m_players.Count);
    }

    private void RotateTowards(Vector3 dir, float dt)
    {
        Vector3 targetDirection = dir - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        float rotateSpeed = 10f; 
        Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * dt);
        transform.rotation = newRotation;
    }

    private void CalculateForces(float fixedDeltaTime)
    {
        Vector3 worldCenterOfMass = m_body.worldCenterOfMass;
        var floatTransform = m_floatCollider.transform;
        var floatPosition = floatTransform.position;
        var floatForward = floatTransform.forward;
        var floatSize = m_floatCollider.size;
        var floatRight = floatTransform.right;
        
        Vector3 vector3_1 = floatPosition + floatForward * floatSize.z / 2f;
        Vector3 vector3_2 = floatPosition - floatForward * floatSize.z / 2f;
        Vector3 vector3_3 = floatPosition - floatRight * floatSize.x / 2f;
        Vector3 vector3_4 = floatPosition + floatRight * floatSize.x / 2f;
        
        double waterLevel1 = Floating.GetWaterLevel(worldCenterOfMass, ref m_previousCenter);
        float waterLevel2 = Floating.GetWaterLevel(vector3_3, ref m_previousLeft);
        float waterLevel3 = Floating.GetWaterLevel(vector3_4, ref m_previousRight);
        float waterLevel4 = Floating.GetWaterLevel(vector3_1, ref m_previousForward);
        float waterLevel5 = Floating.GetWaterLevel(vector3_2, ref m_previousBack);
        double num1 = waterLevel2;
        float num2 = (float)((waterLevel1 + num1 + waterLevel3 + waterLevel4 + waterLevel5) / 5.0);
        float num3 = worldCenterOfMass.y - num2 - m_waterLevelOffset;
        if (num3 > m_disableLevel) return;
        m_body.WakeUp();
        UpdateWaterForce(num3, Time.time);
        Vector3 vector3_5 = new Vector3(vector3_3.x, waterLevel2, vector3_3.z);
        Vector3 vector3_6 = new Vector3(vector3_4.x, waterLevel3, vector3_4.z);
        Vector3 vector3_7 = new Vector3(vector3_1.x, waterLevel4, vector3_1.z);
        Vector3 vector3_8 = new Vector3(vector3_2.x, waterLevel5, vector3_2.z);
        float num4 = fixedDeltaTime * 50f;
        float num5 = Mathf.Clamp01(Mathf.Abs(num3) / m_forceDistance);
        m_body.AddForceAtPosition(Vector3.up * (m_force * num5) * num4, worldCenterOfMass, ForceMode.VelocityChange);
        
        var transform1 = transform;
        var right = transform1.right;
        var forward = transform1.forward;

        float f1 = Vector3.Dot(m_body.velocity, forward);
        float f2 = Vector3.Dot(m_body.velocity, right);
        Vector3 velocity = m_body.velocity;
        float num6 = velocity.y * velocity.y * Mathf.Sign(velocity.y) * m_damping * num5;
        float num7 = f1 * f1 * Mathf.Sign(f1) * m_dampingForward * num5;
        float num8 = f2 * f2 * Mathf.Sign(f2) * m_dampingSideway * num5;
        velocity.y -= Mathf.Clamp(num6, -1f, 1f);
        Vector3 vector3_9 = velocity - forward * Mathf.Clamp(num7, -1f, 1f) - right * Mathf.Clamp(num8, -1f, 1f);
        if (vector3_9.magnitude > m_body.velocity.magnitude)
        {
            vector3_9 = vector3_9.normalized * m_body.velocity.magnitude;
        }

        if (m_sailors.Count == 0)
        {
            vector3_9.x *= 0.1f;
            vector3_9.z *= 0.1f;
        }

        AttachToTarget(fixedDeltaTime);

        m_body.velocity = vector3_9;
        var angularVelocity = m_body.angularVelocity;
        angularVelocity -= angularVelocity * (m_angularDamping * num5);
        m_body.angularVelocity = angularVelocity;
        float num9 = 0.15f;
        float max = 0.5f;
        float f3 = Mathf.Clamp((vector3_7.y - vector3_1.y) * num9, -max, max);
        float f4 = Mathf.Clamp((vector3_8.y - vector3_2.y) * num9, -max, max);
        float f5 = Mathf.Clamp((vector3_5.y - vector3_3.y) * num9, -max, max);
        float f6 = Mathf.Clamp((vector3_6.y - vector3_4.y) * num9, -max, max);
        float num10 = Mathf.Sign(f3) * Mathf.Abs(Mathf.Pow(f3, 2f));
        float num11 = Mathf.Sign(f4) * Mathf.Abs(Mathf.Pow(f4, 2f));
        float num12 = Mathf.Sign(f5) * Mathf.Abs(Mathf.Pow(f5, 2f));
        float num13 = Mathf.Sign(f6) * Mathf.Abs(Mathf.Pow(f6, 2f));
        m_body.AddForceAtPosition(Vector3.up * (num10 * num4), vector3_1, ForceMode.VelocityChange);
        m_body.AddForceAtPosition(Vector3.up * (num11 * num4), vector3_2, ForceMode.VelocityChange);
        m_body.AddForceAtPosition(Vector3.up * (num12 * num4), vector3_3, ForceMode.VelocityChange);
        m_body.AddForceAtPosition(Vector3.up * (num13 * num4), vector3_4, ForceMode.VelocityChange);
        float sailSize = m_speed switch
        {
            Ship.Speed.Full => 1f,
            Ship.Speed.Half => 0.5f,
            _ => m_rudderSpeed
        };
        m_body.AddForceAtPosition(GetSailForce(sailSize, fixedDeltaTime), worldCenterOfMass + transform.up * m_sailForceOffset, ForceMode.VelocityChange);
        Vector3 position = transform1.position + forward * m_stearForceOffset;
        m_body.AddForceAtPosition(right * (f1 * m_stearVelForceFactor) * -m_rudderValue * fixedDeltaTime, position, ForceMode.VelocityChange);
        Vector3 zero = Vector3.zero;
        switch (m_speed)
        {
            case Ship.Speed.Back:
                zero += -transform.forward * (m_backwardForce * (1f - Mathf.Abs(m_rudderValue)));
                break;
            case Ship.Speed.Slow:
                zero += transform.forward * (m_backwardForce * (1f - Mathf.Abs(m_rudderValue)));
                break;
        }
        if (m_speed is Ship.Speed.Back or Ship.Speed.Slow)
        {
            float num14 = m_speed == Ship.Speed.Back ? -1f : 1f;
            zero += floatRight * (m_stearForce * -m_rudderValue * num14);
        }
        m_body.AddForceAtPosition(zero * fixedDeltaTime, position, ForceMode.VelocityChange);
    }

    private bool AttachToTarget(float fixedDeltaTime)
    {
        if (m_currentShipTarget != null)
        {
            var targetVelocity = m_currentShipTarget.m_body.velocity;
            if (targetVelocity.magnitude > m_body.velocity.magnitude)
            {
                m_body.velocity = Vector3.Lerp(m_body.velocity, targetVelocity, 0.5f * fixedDeltaTime);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, m_currentShipTarget.transform.rotation, 10f * fixedDeltaTime);
            m_attachedToTarget = true;
        }
        else
        {
            m_attachedToTarget = false;
        }

        return m_attachedToTarget;
    }
    
    private bool FindPath(Vector3 target)
    {
        float time = Time.time;
        float num = time - m_lastFindPathTime;
        if (num < 1.0 || Vector3.Distance(target, transform.position) < 5.0 && num < 5.0) return m_lastFindPathResult;
        m_lastFindPathTarget = target;
        m_lastFindPathTime = time;
        m_lastFindPathResult = Pathfinding.instance.GetPath(transform.position, target, m_path, Pathfinding.AgentType.Humanoid);
        return m_lastFindPathResult;
    }
    

    private Ship? FindShipTarget()
    {
        if (m_currentShipTarget != null) return m_currentShipTarget;
        List<Ship> ships = Ship.s_currentShips;
        Ship? target = null;
        float num1 = 9999f;
        foreach (var ship in ships)
        {
            if (ship == null) continue;
            var distance = Vector3.Distance(ship.transform.position, transform.position);
            if (distance > num1) continue;
            target = ship;
            num1 = distance;
        }

        m_currentShipTarget = target;
        return target;
    }
    public void UpdateRudder(float dt)
    {
        if (!m_rudderObject) return;
        Quaternion b = Quaternion.Euler(0.0f, m_rudderRotationMax * -m_rudderValue, 0.0f);
        if (m_speed == Ship.Speed.Slow)
        {
            m_rudderPaddleTimer += dt;
            b *= Quaternion.Euler(0.0f, Mathf.Sin(m_rudderPaddleTimer * 6f) * 20f, 0.0f);
        }
        else if (m_speed == Ship.Speed.Back)
        {
            m_rudderPaddleTimer += dt;
            b *= Quaternion.Euler(0.0f, Mathf.Sin(m_rudderPaddleTimer * -3f) * 40f, 0.0f);
        }
        m_rudderObject.transform.localRotation = Quaternion.Slerp(m_rudderObject.transform.localRotation, b, 0.5f);
    }
    private void UpdateSpeed(float dt)
    {
        if (m_players.Count > 0) return;
        float angle = Vector3.Angle(transform.forward, EnvMan.instance.GetWindDir());
        switch (angle)
        {
            case < 45f:
                m_speed = Ship.Speed.Full;
                break;
            case >= 45f and < 135f:
                m_speed = Ship.Speed.Half;
                break;
            default:
                m_speed = Ship.Speed.Slow;
                break;
        }
    }
    public void UpdateSail(float dt)
    {
        var up = transform.up;
        var windDir = EnvMan.instance.GetWindDir();
        Vector3 vector3 = Vector3.Cross(Vector3.Cross(windDir, up), up);

        UpdateSailSize(dt);
        
        if (m_speed is Ship.Speed.Full or Ship.Speed.Half)
        {
            var forward = transform.forward;
            float t = (float) (0.5 + Vector3.Dot(forward, vector3) * 0.5);
            m_mastObject.transform.rotation = Quaternion.RotateTowards(m_mastObject.transform.rotation, Quaternion.LookRotation(-Vector3.Lerp(vector3, Vector3.Normalize(vector3 - forward), t), up), 30f * dt);
        }
        else
        {
            if (m_speed != Ship.Speed.Back) return;
            m_mastObject.transform.rotation = Quaternion.RotateTowards(m_mastObject.transform.rotation, Quaternion.RotateTowards(Quaternion.LookRotation(-transform.forward, up), Quaternion.LookRotation(-vector3, up), 80f), 30f * dt);
        }
    }

    private float GetWindAngle() => -Utils.YawFromDirection(transform.InverseTransformDirection(EnvMan.instance.GetWindDir()));
    public void UpdateSailSize(float dt)
    {
        float target = 0.0f;
        switch (m_speed)
        {
            case Ship.Speed.Stop:
                target = 0.1f;
                break;
            case Ship.Speed.Back:
                target = 0.1f;
                break;
            case Ship.Speed.Slow:
                target = 0.1f;
                break;
            case Ship.Speed.Half:
                target = 0.5f;
                break;
            case Ship.Speed.Full:
                target = 1f;
                break;
        }
        Vector3 localScale = m_sailObject.transform.localScale;
        bool flag = Mathf.Abs(localScale.y - target) < 0.009999999776482582;
        if (!flag)
        {
            localScale.y = Mathf.MoveTowards(localScale.y, target, dt);
            m_sailObject.transform.localScale = localScale;
        }
        if (m_sailCloth)
        {
            if (m_speed == Ship.Speed.Stop || m_speed == Ship.Speed.Slow || m_speed == Ship.Speed.Back)
            {
                if (flag && m_sailCloth.enabled)
                    m_sailCloth.enabled = false;
            }
            else if (flag)
            {
                if (!m_sailWasInPosition)
                {
                    Utils.RecreateComponent(ref m_sailCloth);
                    if (m_globalWind) m_globalWind.UpdateClothReference(m_sailCloth);
                }
            }
            else
                m_sailCloth.enabled = true;
        }
        m_sailWasInPosition = flag;
    }
    
    public void ApplyEdgeForce(float dt)
    {
        float magnitude = transform.position.magnitude;
        float l = 10420f;
        if (magnitude <= (double)l) return;
        m_body.AddForce(Vector3.Normalize(transform.position) * (Utils.LerpStep(l, 10500f, magnitude) * 8f) * dt, ForceMode.VelocityChange);
    }

    private Vector3 GetSailForce(float sailSize, float dt)
    {
        Vector3 windDir = EnvMan.instance.GetWindDir();
        float num1 = Mathf.Lerp(0.25f, 1f, EnvMan.instance.GetWindIntensity());
        float num2 = GetWindAngleFactor() * num1;
        Vector3 forward = transform.forward;
        m_sailForce = Vector3.SmoothDamp(m_sailForce, Vector3.Normalize(windDir + forward) * (num2 * m_sailForceFactor * sailSize), ref m_windChangeVelocity, 1f, 99f);
        return m_sailForce;
    }

    private float GetWindAngleFactor()
    {
        float num = Vector3.Dot(EnvMan.instance.GetWindDir(), -transform.forward);
        return Mathf.Lerp(0.7f, 1f, 1f - Mathf.Abs(num)) * (1f - Utils.LerpStep(0.75f, 0.8f, num));
    }
    
    public void UpdateWaterForce(float depth, float time)
    {
        double num1 = depth - (double)m_lastDepth;
        float num2 = time - m_lastUpdateWaterForceTime;
        m_lastDepth = depth;
        m_lastUpdateWaterForceTime = time;
        double num3 = num2;
        float f = (float) (num1 / num3);
        if (f > 0.0 || Utils.Abs(f) <= (double) m_minWaterImpactForce || time - (double) m_lastWaterImpactTime <= m_minWaterImpactInterval)
            return;
        m_lastWaterImpactTime = time;
        var transform1 = transform;
        m_waterImpactEffect.Create(transform1.position, transform1.rotation);
        if (m_sailors.Count <= 0) return;
        m_destructible.Damage(new HitData()
        {
            m_damage = { m_blunt = m_waterImpactDamage },
            m_point = transform.position,
            m_dir = Vector3.up
        });
    }
    

    [HarmonyPatch(typeof(MonoUpdaters), nameof(MonoUpdaters.FixedUpdate))]
    private static class MonoUpdaters_FixedUpdate_Patch
    {
        private static void Postfix(MonoUpdaters __instance)
        {
            float fixedTimeDelta = Time.fixedDeltaTime;
            __instance.m_ai.UpdateAI(Instances, "MonoUpdaters.FixedUpdate.ShipAI", fixedTimeDelta);
        }
    }
}