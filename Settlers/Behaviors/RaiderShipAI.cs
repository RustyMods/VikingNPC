using System;
using System.Collections.Generic;
using UnityEngine;

namespace Settlers.Behaviors;

public class RaiderShipAI : MonoBehaviour
{
    public bool m_forwardPressed;
    public bool m_backwardPressed;
    public float m_sendRudderTime;
    [Header("Objects")]
    public GameObject m_sailObject;
    public GameObject m_mastObject;
    public GameObject m_rudderObject;
    [Header("Misc")]
    public BoxCollider m_floatCollider;
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
    public float m_upsideDownDmgInterval = 1f;
    public float m_upsideDownDmg = 20f;
    public EffectList m_waterImpactEffect = new EffectList();
    public bool m_sailWasInPosition;
    public Vector3 m_windChangeVelocity = Vector3.zero;
    public float m_speed;
    public float m_rudder;
    public float m_rudderValue;
    public Vector3 m_sailForce = Vector3.zero;
    public readonly List<Companion> m_raiders = new List<Companion>();
    public List<AudioSource> m_ashlandsFxAudio;
    public WaterVolume m_previousCenter;
    public WaterVolume m_previousLeft;
    public WaterVolume m_previousRight;
    public WaterVolume m_previousForward;
    public WaterVolume m_previousBack;
    public static readonly List<Ship> s_currentShips = new List<Ship>();
    public GlobalWind m_globalWind;
    public Rigidbody m_body;
    public ZNetView m_nview;
    public IDestructible m_destructible;
    public Cloth m_sailCloth;
    public float m_lastDepth = -9999f;
    public float m_lastWaterImpactTime;
    public float m_upsideDownDmgTimer;
    public float m_ashlandsDmgTimer;
    public float m_rudderPaddleTimer;
    public float m_lastUpdateWaterForceTime;

    public static List<RaiderShipAI> m_instances = new();
    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_body = GetComponent<Rigidbody>();
        m_destructible = GetComponent<IDestructible>();
    }
    
    
}