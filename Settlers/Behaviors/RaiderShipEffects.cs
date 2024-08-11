using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Settlers.Behaviors;

public class RaiderShipEffects : MonoBehaviour, IMonoUpdater
{
    public Transform m_shadow;
    public float m_offset = 0.01f;
    public float m_minimumWakeVel = 5f;
    public GameObject m_speedWakeRoot;
    public GameObject m_wakeSoundRoot;
    public GameObject m_inWaterSoundRoot;
    public float m_audioFadeDuration = 2f;
    public AudioSource m_sailSound;
    public float m_sailFadeDuration = 1f;
    public GameObject m_splashEffects;
    public ParticleSystem[] m_wakeParticles;
    public float m_sailBaseVol = 1f;
    public readonly List<KeyValuePair<AudioSource, float>> m_wakeSounds = new List<KeyValuePair<AudioSource, float>>();
    public readonly List<KeyValuePair<AudioSource, float>> m_inWaterSounds = new List<KeyValuePair<AudioSource, float>>();
    public WaterVolume m_previousWaterVolume;
    public Rigidbody m_body;
    public ShipAI m_shipAI;

    public static List<IMonoUpdater> Instances = new();
    public void Awake()
    {
        ZNetView componentInParent = GetComponentInParent<ZNetView>();
        if (componentInParent && componentInParent.GetZDO() == null) enabled = false;
        else
        {
            m_body = GetComponentInParent<Rigidbody>();
            m_shipAI = GetComponentInParent<ShipAI>();
            if (m_speedWakeRoot)
            {
                m_wakeParticles = m_speedWakeRoot.GetComponentsInChildren<ParticleSystem>();
            }

            if (m_wakeSoundRoot)
            {
                foreach (var componentInChild in m_wakeSoundRoot.GetComponentsInChildren<AudioSource>())
                {
                    componentInChild.pitch = Random.Range(0.9f, 1.1f);
                    m_wakeSounds.Add(new KeyValuePair<AudioSource, float>(componentInChild, componentInChild.volume));
                }
            }

            if (m_inWaterSoundRoot)
            {
                foreach (var componentInChild in m_inWaterSoundRoot.GetComponentsInChildren<AudioSource>())
                {
                    componentInChild.pitch = Random.Range(0.9f, 1.1f);
                    m_inWaterSounds.Add(new KeyValuePair<AudioSource, float>(componentInChild, componentInChild.volume));
                }
            }

            if (!m_sailSound) return;
            m_sailBaseVol = m_sailSound.volume;
            m_sailSound.pitch = Random.Range(0.9f, 1.1f);
        }
    }

    private void OnEnable()
    {
        Instances.Add(this);
    }

    private void OnDisable()
    {
        Instances.Remove(this);
    }

    public void CustomFixedUpdate(float deltaTime)
    {
        throw new System.NotImplementedException();
    }

    public void CustomUpdate(float deltaTime, float time)
    {
        throw new System.NotImplementedException();
    }

    public void CustomLateUpdate(float deltaTime)
    {
        if (!Floating.IsUnderWater(transform.position, ref m_previousWaterVolume))
        {
            m_shadow.gameObject.SetActive(false);
            SetWake(false, deltaTime);
            FadeSounds(m_inWaterSounds, false, deltaTime);
        }
        else
        {
            m_shadow.gameObject.SetActive(true);
            bool flag = m_body.velocity.magnitude > (double) m_minimumWakeVel;
            FadeSounds(m_inWaterSounds, true, deltaTime);
            SetWake(flag, deltaTime);
            if (m_sailSound) ShipEffects.FadeSound(m_sailSound, m_shipAI.m_speed is not Ship.Speed.Stop ? m_sailBaseVol : 0.0f, m_sailFadeDuration, deltaTime);
            if (m_splashEffects == null) return;
            m_splashEffects.SetActive(m_shipAI.m_sailors.Count <= 0);
        }
    }
    
    public void SetWake(bool wakeEnabled, float dt)
    {
        foreach (ParticleSystem wakeParticle in m_wakeParticles)
        {
            ParticleSystem.EmissionModule wakeParticleEmission = wakeParticle.emission;
            wakeParticleEmission.enabled = wakeEnabled;
        }

        FadeSounds(m_wakeSounds, wakeEnabled, dt);
    }

    public void FadeSounds(List<KeyValuePair<AudioSource, float>> sources, bool enableSounds, float dt)
    {
        foreach (KeyValuePair<AudioSource, float> source in sources)
        {
            ShipEffects.FadeSound(source.Key, enableSounds ? source.Value : 0.0f, m_audioFadeDuration, dt);
        }
    }

    public void FadeSound(AudioSource source, float target, float fadeDuration, float dt)
    {
        float maxDelta = dt / fadeDuration;
        if (target > 0.0)
        {
            if (!source.isPlaying) source.Play();
            source.volume = Mathf.MoveTowards(source.volume, target, maxDelta);
        }
        else
        {
            if (!source.isPlaying) return;
            source.volume = Mathf.MoveTowards(source.volume, 0.0f, maxDelta);
            if (source.volume > 0.0) return;
            source.Stop();
        }
    }

    [HarmonyPatch(typeof(MonoUpdaters), nameof(MonoUpdaters.LateUpdate))]
    private static class MonoUpdaters_LateUpdate_Patch
    {
        private static void Postfix(MonoUpdaters __instance)
        {
            float deltaTime = Time.deltaTime;
            __instance.m_update.CustomLateUpdate(Instances, "MonoUpdaters.LateUpdate.RaiderShipEffects", deltaTime);
        }
    }
    
}