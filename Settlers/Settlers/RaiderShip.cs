using System.Collections.Generic;
using BepInEx;
using Settlers.Behaviors;
using UnityEngine;

namespace Settlers.Settlers;

public static class RaiderShipManager
{
    public static void LoadRaiderShips()
    {
        // var ship = new RaiderShip(SettlersPlugin._raiderShipBundle, "RaiderShip");
    }
}


public class RaiderShip
{
    private static readonly Dictionary<string, RaiderShip> m_ships = new();


    private static void SetRaiderShipAI(GameObject prefab)
    {
        if (!prefab.TryGetComponent(out RaiderShipAI component)) return;
        var sail = Utils.FindChild(prefab.transform, "$part_sail");
        var mast = Utils.FindChild(prefab.transform, "$part_mast");
        var rudder = Utils.FindChild(prefab.transform, "$part_rudder");
        var floatCollider = Utils.FindChild(prefab.transform, "$part_floatCollider");
        if (!floatCollider.TryGetComponent(out BoxCollider collider)) return;
        component.m_sailObject = sail.gameObject;
        component.m_mastObject = mast.gameObject;
        component.m_rudderObject = rudder.gameObject;
        component.m_floatCollider = collider;
    }
    private static void CopyShipEffects(GameObject prefab, RaiderShip data)
    {
        if (data.m_copyShipEffectsFrom.IsNullOrWhiteSpace()) return;
        var original = ZNetScene.instance.GetPrefab(data.m_copyShipEffectsFrom);
        if (!original) return;
        var originalShipEffects = original.GetComponentInChildren<ShipEffects>();
        if (originalShipEffects == null) return;
        var raiderShipEffects = prefab.GetComponentInChildren<ShipEffects>();
        if (raiderShipEffects == null) return;

        raiderShipEffects.m_sailSound = originalShipEffects.m_sailSound;

        var originalContainer = original.GetComponentInChildren<Container>();
        if (originalContainer == null) return;
        var raiderShipContainer = prefab.GetComponentInChildren<Container>();
        if (raiderShipContainer == null) return;

        raiderShipContainer.m_bkg = originalContainer.m_bkg;
        raiderShipContainer.m_openEffects = originalContainer.m_openEffects;
        raiderShipContainer.m_closeEffects = originalContainer.m_closeEffects;
        raiderShipContainer.m_destroyedLootPrefab = originalContainer.m_destroyedLootPrefab;

        Transform originalSailSoundTransform = Utils.FindChild(original.transform, "SailSound");
        if (originalSailSoundTransform == null) return;
        Transform raiderShipSailSoundTransform = Utils.FindChild(prefab.transform, "SailSound");
        if (raiderShipSailSoundTransform == null) return;
        if (!originalSailSoundTransform.TryGetComponent(out AudioSource originalSailSound)) return;
        if (!raiderShipSailSoundTransform.TryGetComponent(out AudioSource raiderShipSailSound)) return;

        raiderShipSailSound.clip = originalSailSound.clip;
        raiderShipSailSound.outputAudioMixerGroup = originalSailSound.outputAudioMixerGroup;

        Transform originalWakeSoundsTransform = Utils.FindChild(original.transform, "WakeSounds");
        if (originalWakeSoundsTransform == null) return;
        Transform raiderShipWakeSoundsTransform = Utils.FindChild(prefab.transform, "WakeSounds");
        Transform originalFrontSounds = originalWakeSoundsTransform.Find("front");
        Transform originalAftSounds = originalWakeSoundsTransform.Find("aft");
        if (originalFrontSounds == null || originalAftSounds == null) return;
        Transform raiderShipFrontSounds = raiderShipWakeSoundsTransform.Find("front");
        Transform raiderShipAftSounds = raiderShipWakeSoundsTransform.Find("aft");
        if (raiderShipFrontSounds == null || raiderShipAftSounds == null) return;
        if (!originalFrontSounds.TryGetComponent(out AudioSource originalFrontAudio)) return;
        if (!originalAftSounds.TryGetComponent(out AudioSource originalAftAudio)) return;
        if (!raiderShipFrontSounds.TryGetComponent(out AudioSource raiderShipFrontAudio)) return;
        if (!raiderShipAftSounds.TryGetComponent(out AudioSource raiderShipAftAudio)) return;

        raiderShipFrontAudio.clip = originalFrontAudio.clip;
        raiderShipFrontAudio.outputAudioMixerGroup = originalFrontAudio.outputAudioMixerGroup;
        raiderShipAftAudio.clip = originalAftAudio.clip;
        raiderShipAftAudio.outputAudioMixerGroup = originalAftAudio.outputAudioMixerGroup;

        Transform originalDeckSoundTransform = Utils.FindChild(original.transform, "Decksound");
        if (originalDeckSoundTransform == null) return;
        Transform raiderShipDeckSoundTransform = Utils.FindChild(prefab.transform, "Decksound");
        if (raiderShipDeckSoundTransform == null) return;
        if (!originalDeckSoundTransform.TryGetComponent(out AudioSource originalDeckSound)) return;
        if (!raiderShipDeckSoundTransform.TryGetComponent(out AudioSource raiderShipDeckSound)) return;
        raiderShipDeckSound.clip = originalDeckSound.clip;
        raiderShipDeckSound.outputAudioMixerGroup = originalDeckSound.outputAudioMixerGroup;
        var originalWaterTriggerEffects = original.GetComponentInChildren<WaterTrigger>();
        if (originalWaterTriggerEffects == null) return;
        foreach (var waterTrigger in prefab.GetComponentsInChildren<WaterTrigger>())
        {
            waterTrigger.m_effects = originalWaterTriggerEffects.m_effects;
        }

        if (!original.TryGetComponent(out ImpactEffect originalImpactEffect)) return;
        if (!prefab.TryGetComponent(out ImpactEffect raiderShipImpactEffect)) return;
        raiderShipImpactEffect.m_hitEffect = originalImpactEffect.m_hitEffect;
        if (!original.TryGetComponent(out Ship originalShip)) return;
        if (!prefab.TryGetComponent(out RaiderShipAI raiderShipAI)) return;
        raiderShipAI.m_waterImpactEffect = originalShip.m_waterImpactEffect;
    }

    private AssetBundle m_assetBundle;
    private GameObject m_prefab;
    private string m_copyShipEffectsFrom = "";
    public float m_waterLevelOffset = 1.5f;
    public float m_forceDistance = 3f;
    public float m_force = 1f;
    public float m_damping = 0.05f;
    public float m_dampingSideway = 0.15f;
    public float m_dampingForward = 0.001f;
    public float m_angularDamping = 0.3f;
    public float m_disableLevel = -0.5f;
    public float m_sailForceOffset = 2f;
    public float m_sailForceFactor = 0.05f;
    public float m_rudderSpeed = 1f;

    public void CopyShipEffectsFrom(string prefabName) => m_copyShipEffectsFrom = prefabName;

    public RaiderShip(AssetBundle assetBundle, string prefabName)
    {
        m_assetBundle = assetBundle;
        m_prefab = m_assetBundle.LoadAsset<GameObject>(prefabName);
        m_prefab.AddComponent<RaiderShipAI>();
        m_ships[prefabName] = this;
    }
}