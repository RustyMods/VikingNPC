﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Settlers.Settlers;

public class Companion : Humanoid, Interactable
{
    private static readonly int Visible = Animator.StringToHash("visible");
    private static Companion? m_currentCompanion;
    private static readonly int m_ownerKey = "VikingSettlerOwner".GetStableHashCode();
    private static readonly int m_ownerNameKey = "VikingSettlerOwnerName".GetStableHashCode();
    public static readonly List<Companion> m_instances = new();
    private static readonly int Consume = Animator.StringToHash("consume");
    
    public CompanionAI? m_companionAI;
    public bool m_inUse;
    private uint m_lastRevision;
    private string m_lastDataString = "";
    private bool m_loading;
    public float m_autoPickupRange = 2f;
    public int m_autoPickupMask;
    public float m_checkDistanceTimer;
    public string m_followTargetName = "";
    public const float m_playerMaxDistance = 50f;
    public float m_fedDuration = 30f;
    public float m_tamingTime = 1800f;
    public float m_baseHealth = 50f;
    public EffectList m_tamedEffect = new EffectList();
    public EffectList m_sootheEffect = new EffectList();
    public EffectList m_petEffect = new EffectList();
    public EffectList m_warpEffect = new EffectList();
    public EffectList m_drownEffects = new EffectList();
    public EffectList m_equipStartEffects = new();
    public float m_lastPetTime;
    public GameObject? m_tombstone;
    public readonly List<Player.Food> m_foods = new();
    private float m_foodUpdateTimer;
    private Heightmap.Biome m_currentBiome = Heightmap.Biome.None;
    private float m_biomeTimer;
    private readonly List<MinorActionData> m_actionQueue = new();
    private float m_actionQueuePause;
    public string m_actionAnimation = "";
    public ItemDrop.ItemData? m_weaponLoaded;
    public bool m_inventoryChanged;
    private float m_maxEitr;
    private float m_eitr;
    private float m_statsTimer;
    public bool m_attached;
    private bool m_attachedToShip;
    private Transform? m_attachPoint;
    private Vector3 m_detachOffset;
    private string m_attachAnimation = "";
    private Collider[]? m_attachColliders;
    public override void Awake()
    {
        base.Awake();
        m_autoPickupMask = LayerMask.GetMask("item");
        m_companionAI = GetComponent<CompanionAI>();
        m_companionAI.m_onConsumedItem += OnConsumedItem;
        m_nview.Register<long>(nameof(RPC_RequestOpen), RPC_RequestOpen);
        m_nview.Register<bool>(nameof(RPC_OpenResponse), RPC_OpenResponse);
        m_nview.Register<ZDOID, bool>(nameof(RPC_Command), RPC_Command);
        m_nview.Register<long>(nameof(RPC_RequestStack), RPC_RequestStack);
        m_nview.Register<bool>(nameof(RPC_StackResponse), RPC_StackResponse);
        m_name = m_nview.GetZDO().GetString(ZDOVars.s_tamedName);
        m_instances.Add(this);
        m_visEquipment.m_isPlayer = true;
        if (!IsTamed()) InvokeRepeating(nameof(TamingUpdate), 3f, 3f);
        GetFollowTargetName();
        if (IsTamed())
        {
            m_companionAI.m_aggravatable = false;
            m_faction = Faction.Players;
        }
        m_baseHealth *= m_level;
    }
    
    public override void Start()
    {
        LoadInventory();
        if (m_inventory.GetAllItems().Count == 0)
        {
            GiveDefaultItems();
        }
        m_inventory.m_onChanged += SaveInventory;
    }
    
    public void FixedUpdate()
    {
        float fixedDeltaTime = Time.deltaTime;
        if (!m_nview) return;
        if (m_nview.GetZDO() == null) return;
        if (!m_nview.IsOwner()) return;
        if (IsDead()) return;
        if (!IsTamed()) return;
        if (UpdateAttach()) return;
        UpdateActionQueue(fixedDeltaTime);
        AutoPickup(fixedDeltaTime);
        Warp(fixedDeltaTime);
        UpdateFood(fixedDeltaTime, false);
        UpdateBiome(fixedDeltaTime);
        UpdateWeaponLoading(GetCurrentWeapon(), fixedDeltaTime);
        UpdateStats(fixedDeltaTime);
    }

    private void UpdateEncumbered()
    {
        if (GetWeight() < GetMaxCarryWeight()) m_seman.AddStatusEffect(SEMan.s_statusEffectEncumbered);
        else
        {
            m_seman.RemoveStatusEffect(SEMan.s_statusEffectEncumbered);
        }
    }

    private void GetFollowTargetName()
    {
        if (m_companionAI == null) return;
        if (m_companionAI.GetFollowTarget() == null) return;
        if (!m_companionAI.GetFollowTarget().TryGetComponent(out Player player)) return;
        m_followTargetName = player.GetHoverName();
    }

    private void UpdateBiome(float dt)
    {
        m_biomeTimer += dt;
        if (m_biomeTimer < 1f) return;
        m_biomeTimer = 0.0f;

        m_currentBiome = Heightmap.FindBiome(transform.position);
    }

    public Heightmap.Biome GetCurrentBiome() => m_currentBiome;
    public void Warp(float dt)
    {
        if (IsEncumbered()) return;
        m_checkDistanceTimer += dt;
        if (m_checkDistanceTimer < 5f) return;
        m_checkDistanceTimer = 0.0f;
        
        if (m_companionAI == null) return;
        if (m_companionAI.GetFollowTarget() == null) return;
        if (!m_companionAI.GetFollowTarget().TryGetComponent(out Player component)) return;
        if (Vector3.Distance(transform.position, component.transform.position) < m_playerMaxDistance) return;

        Vector3 location = component.GetLookDir() * 5f + component.transform.position;
        ZoneSystem.instance.GetSolidHeight(location, out float height, 1000);
        if (height >= 0.0 && Mathf.Abs(height - location.y) <= 2f &&
            Vector3.Distance(location, component.transform.position) >= 2f)
        {
            location.y = height;
        }

        if (!IsOwner()) m_nview.ClaimOwnership();
        m_companionAI.StopMoving();
        Transform transform1 = transform;
        transform1.position = location;
        transform1.rotation = Quaternion.identity;
        m_body.velocity = Vector3.zero;
        m_warpEffect.Create(transform1.position, Quaternion.identity);
    }

    public bool IsInventoryFull() => GetInventory().NrOfItems() >= m_inventory.m_width * m_inventory.m_height;
    public void AutoPickup(float dt)
    {
        if (SettlersPlugin._autoPickup.Value is SettlersPlugin.Toggle.Off) return;
        if (IsInventoryFull() || IsEncumbered()) return;
        Vector3 vector3_1 = transform.position + Vector3.up;
        foreach (Collider collider in Physics.OverlapSphere(vector3_1, m_autoPickupRange, m_autoPickupMask))
        {
            if (!collider.attachedRigidbody) continue;
            ItemDrop component = collider.attachedRigidbody.GetComponent<ItemDrop>();
            FloatingTerrainDummy? floatingTerrainDummy = collider.attachedRigidbody.gameObject.GetComponent<FloatingTerrainDummy>();
            if (component == null && floatingTerrainDummy != null) component = floatingTerrainDummy.m_parent.gameObject.GetComponent<ItemDrop>();
            if (component == null || !component.m_autoPickup || !component.GetComponent<ZNetView>().IsValid()) continue;
            if (component.m_itemData.GetWeight() + GetWeight() > GetMaxCarryWeight()) continue;
            if (!component.CanPickup()) component.RequestOwn();
            else if (!component.InTar())
            {
                component.Load();
                if (!m_inventory.CanAddItem(component.m_itemData)) continue;
                float num = Vector3.Distance(component.transform.position, vector3_1);
                if (num > m_autoPickupRange) continue;
                if (num < 0.30000001192092896) Pickup(component.gameObject);
                else
                {
                    Vector3 position = component.transform.position;
                    Vector3 vector3_2 = Vector3.Normalize(vector3_1 - position) * (15f * dt);
                    position += vector3_2;
                    component.transform.position = position;
                    if (floatingTerrainDummy != null) floatingTerrainDummy.transform.position += vector3_2;
                }
            }
        }
    }

    public override void OnDeath()
    {
        PlayerProfile playerProfile = Game.instance.GetPlayerProfile();
        bool flag = m_lastHit != null && m_lastHit.GetAttacker() == Player.m_localPlayer;
        if(m_localPlayerHasHit) playerProfile.IncrementStat(PlayerStatType.EnemyKills);
        if (flag) playerProfile.IncrementStat(PlayerStatType.EnemyKillsLastHits);
        playerProfile.m_enemyStats.IncrementOrSet<string>("HumanSettler");
        if (!string.IsNullOrEmpty(m_defeatSetGlobalKey)) Player.m_addUniqueKeyQueue.Add(m_defeatSetGlobalKey);
        if (m_nview && !m_nview.IsOwner()) return;
        foreach (GameObject prefab in m_deathEffects.Create(transform.position, transform.rotation, transform))
        {
            if (!prefab.TryGetComponent(out Ragdoll component)) continue;
            Vector3 velocity = m_body.velocity;
            if (m_pushForce.magnitude * 0.5 > velocity.magnitude)
            {
                velocity = m_pushForce * 0.5f;
            }
            component.Setup(velocity, 0.0f, 0.0f, 0.0f, null);
            OnRagdollCreated(component);
        }
        CreateTombStone();
        ZNetScene.instance.Destroy(gameObject);
        Gogan.LogEvent("Game", "Killed", m_name, 0L);
    }

    public override bool HaveEitr(float amount = 0) => amount < m_eitr;

    public override bool StartAttack(Character? target, bool secondaryAttack)
    {
        if (InAttack() && !HaveQueuedChain() || InDodge() || !CanMove() || IsKnockedBack() || IsStaggering() ||
            InMinorAction()) return false;
    
        ItemDrop.ItemData currentWeapon = GetCurrentWeapon();
        if (currentWeapon == null || (!currentWeapon.HaveSecondaryAttack() && !currentWeapon.HavePrimaryAttack())) return false;

        bool secondary = currentWeapon.HavePrimaryAttack() && currentWeapon.HaveSecondaryAttack() && Random.value > 0.5 || !currentWeapon.HaveSecondaryAttack();
        if (currentWeapon.m_shared.m_skillType is Skills.SkillType.Spears) secondary = false;
        
        if (m_currentAttack != null)
        {
            m_currentAttack.Stop();
            m_previousAttack = m_currentAttack;
            m_currentAttack = null;
        }
        
        Attack? attack = secondary ? currentWeapon.m_shared.m_attack.Clone() : currentWeapon.m_shared.m_secondaryAttack.Clone();
        if (!attack.Start(this, m_body, m_zanim, m_animEvent, m_visEquipment, currentWeapon, m_previousAttack,
                m_timeSinceLastAttack, Random.Range(0.5f, 1f))) return false;

        if (currentWeapon.m_shared.m_attack.m_requiresReload)
        {
            SetWeaponLoaded(null);
        }

        currentWeapon.m_durability -= 5f;
        
        ClearActionQueue();
        StartAttackGroundCheck();
        m_currentAttack = attack;
        m_currentAttackIsSecondary = secondary;
        m_lastCombatTimer = 0.0f;
        if (currentWeapon.m_shared.m_name == "$item_stafficeshards")
        {
            Invoke(nameof(StopCurrentAttack), 5f);
        }
        return true;
    }

    public override void ClearActionQueue() => m_actionQueue.Clear();

    public void UpdateActionQueue(float dt)
    {
        if (m_actionQueuePause > 0.0)
        {
            --m_actionQueuePause;
            if (m_actionAnimation.IsNullOrWhiteSpace()) return;
            m_animator.SetBool(m_actionAnimation, false);
            m_actionAnimation = "";
        }
        else if (InAttack())
        {
            if (m_actionAnimation.IsNullOrWhiteSpace()) return;
            m_animator.SetBool(m_actionAnimation, false);
            m_actionAnimation = "";
        }
        else if (m_actionQueue.Count == 0)
        {
            if (m_actionAnimation.IsNullOrWhiteSpace()) return;
            m_animator.SetBool(m_actionAnimation, false);
            m_actionAnimation = "";
        }
        else
        {
            var action = m_actionQueue[0];
            if (!m_actionAnimation.IsNullOrWhiteSpace() && m_actionAnimation != action.m_animation)
            {
                m_animator.SetBool(m_actionAnimation, false);
                m_actionAnimation = "";
            }
            m_animator.SetBool(action.m_animation, true);
            m_actionAnimation = action.m_animation;
            if (action.m_time == 0.0 && action.m_startEffect != null)
            {
                action.m_startEffect.Create(transform.position, Quaternion.identity);
            }

            // if (action.m_staminaDrain > 0.0)
            // {
            //     UseStamina(action.m_staminaDrain * dt, false);
            // }
            //
            if (action.m_eitrDrain > 0.0)
            {
                UseEitr(action.m_eitrDrain * dt);
            }

            action.m_time += dt;
            if (action.m_time <= action.m_duration) return;
            m_actionQueue.RemoveAt(0);
            m_animator.SetBool(m_actionAnimation, false);
            m_actionAnimation = "";
            if (!action.m_doneAnimation.IsNullOrWhiteSpace())
            {
                m_animator.SetTrigger(action.m_doneAnimation);
            }

            switch (action.m_type)
            {
                case MinorActionData.ActionType.Equip:
                    EquipItem(action.m_item);
                    break;
                case MinorActionData.ActionType.Unequip:
                    UnequipItem(action.m_item);
                    break;
                case MinorActionData.ActionType.Reload:
                    SetWeaponLoaded(action.m_item);
                    break;
            }

            m_actionQueuePause = 0.3f;
        }
    }

    public override void DamageArmorDurability(HitData hit)
    {
        List<ItemDrop.ItemData> itemDataList = new();
        if (m_chestItem != null) itemDataList.Add(m_chestItem);
        if (m_legItem != null) itemDataList.Add(m_leftItem);
        if (m_helmetItem != null) itemDataList.Add(m_helmetItem);
        if (m_shoulderItem != null) itemDataList.Add(m_shoulderItem);
        if (itemDataList.Count == 0) return;
        float num = hit.GetTotalPhysicalDamage() + hit.GetTotalElementalDamage();
        if (num <= 0.0) return;
        int index = Random.Range(0, itemDataList.Count);
        var itemData = itemDataList[index];
        itemData.m_durability = Mathf.Max(0.0f, itemData.m_durability - num);
    }

    public override bool ToggleEquipped(ItemDrop.ItemData item)
    {
        if (!item.IsEquipable()) return false;
        // if (InAttack()) return true;
        if (item.m_shared.m_equipDuration <= 0.0)
        {
            if (IsItemEquiped(item)) UnequipItem(item);
            else EquipItem(item);
        }
        else if (IsItemEquiped(item)) QueueUnequipAction(item);
        else QueueEquipAction(item);
        return true;
    }

    public void QueueEquipAction(ItemDrop.ItemData? item)
    {
        if (item == null) return;
        if (IsEquipActionQueued(item))
        {
            RemoveEquipAction(item);
        }
        else
        {
            CancelReloadAction();
            MinorActionData minorActionData = new MinorActionData
            {
                m_item = item,
                m_type = MinorActionData.ActionType.Equip,
                m_duration = item.m_shared.m_equipDuration,
                m_animation = "equipping"
            };
            if (minorActionData.m_duration >= 1.0)
            {
                minorActionData.m_startEffect = m_equipStartEffects;
            }
            m_actionQueue.Add(minorActionData);
        }
    }

    private void QueueUnequipAction(ItemDrop.ItemData? item)
    {
        if (item == null) return;
        if (IsEquipActionQueued(item))
        {
            RemoveEquipAction(item);
        }
        else
        {
            CancelReloadAction();
            m_actionQueue.Add(new MinorActionData()
            {
                m_item = item,
                m_type = MinorActionData.ActionType.Unequip,
                m_duration = item.m_shared.m_equipDuration,
                m_animation = "equipping"
            });
        }
    }

    public void UpdateWeaponLoading(ItemDrop.ItemData? weapon, float dt)
    {
        if (weapon == null || !weapon.m_shared.m_attack.m_requiresReload) return;
        if (m_weaponLoaded == weapon || !weapon.m_shared.m_attack.m_requiresReload || IsReloadActionQueued()) return;
        QueueReloadAction();
    }

    public override bool IsWeaponLoaded() => m_nview.GetZDO().GetBool(ZDOVars.s_weaponLoaded);
    private void SetWeaponLoaded(ItemDrop.ItemData? weapon)
    {
        if (weapon == m_weaponLoaded) return;
        m_weaponLoaded = weapon;
        m_nview.GetZDO().Set(ZDOVars.s_weaponLoaded, weapon != null);
    }

    public bool IsQueueActive() => m_actionQueue.Count > 0;

    private void QueueReloadAction()
    {
        if (IsReloadActionQueued()) return;
        ItemDrop.ItemData? currentWeapon = GetCurrentWeapon();
        if (currentWeapon == null || !currentWeapon.m_shared.m_attack.m_requiresReload) return;
        m_actionQueue.Add(new MinorActionData()
        {
            m_item = currentWeapon,
            m_type = MinorActionData.ActionType.Reload,
            m_duration = currentWeapon.GetWeaponLoadingTime(),
            m_animation = currentWeapon.m_shared.m_attack.m_reloadAnimation,
            m_doneAnimation = currentWeapon.m_shared.m_attack.m_reloadAnimation + "_done",
            m_staminaDrain = currentWeapon.m_shared.m_attack.m_reloadStaminaDrain,
            m_eitrDrain = currentWeapon.m_shared.m_attack.m_reloadEitrDrain
        });
    }

    public void CancelReloadAction()
    {
        foreach (MinorActionData? action in m_actionQueue)
        {
            if (action.m_type is not MinorActionData.ActionType.Reload) continue;
            m_actionQueue.Remove(action);
            break;
        }
    }

    public override void RemoveEquipAction(ItemDrop.ItemData? item)
    {
        if (item == null) return;
        foreach (MinorActionData action in m_actionQueue)
        {
            if (action.m_item == item)
            {
                m_actionQueue.Remove(action);
                break;
            }
        }
    }

    private bool IsEquipActionQueued(ItemDrop.ItemData? item)
    {
        if (item == null) return false;
        foreach (var action in m_actionQueue)
        {
            if (action.m_type is MinorActionData.ActionType.Equip or MinorActionData.ActionType.Unequip && action.m_item == item)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsReloadActionQueued()
    {
        foreach (var action in m_actionQueue)
        {
            if (action.m_type is MinorActionData.ActionType.Reload) return true;
        }

        return false;
    }

    private void StopCurrentAttack()
    {
        if (m_currentAttack == null) return;
        m_currentAttack.Stop();
        m_previousAttack = m_currentAttack;
        m_currentAttack = null;
    }

    private void CreateTombStone()
    {
        if (GetInventory().NrOfItems() == 0 || m_tombstone == null) return;
        if (!ShouldCreateTombStone()) return;
        GameObject tomb = Instantiate(m_tombstone, GetCenterPoint(), transform.rotation);
        if (!tomb.TryGetComponent(out TombStone component)) return;
        component.Setup(m_name, 0L);
        component.m_container.GetInventory().MoveAll(GetInventory());
    }

    private bool ShouldCreateTombStone()
    {
        return m_nview.GetZDO().GetBool("InventoryChanged".GetStableHashCode());
    }

    public override void SetupVisEquipment(VisEquipment visEq, bool isRagdoll)
    {
        if (!isRagdoll)
        {
            visEq.SetLeftItem(m_leftItem != null ? m_leftItem.m_dropPrefab.name : "", m_leftItem?.m_variant ?? 0);
            visEq.SetRightItem(m_rightItem != null ? m_rightItem.m_dropPrefab.name : "");
            visEq.SetLeftBackItem(m_hiddenLeftItem != null ? m_hiddenLeftItem.m_dropPrefab.name : "", m_hiddenLeftItem?.m_variant ?? 0);
            visEq.SetRightBackItem(m_hiddenRightItem != null ? m_hiddenRightItem.m_dropPrefab.name : "");
        }
        visEq.SetChestItem(m_chestItem != null ? m_chestItem.m_dropPrefab.name : "");
        visEq.SetLegItem(m_legItem != null ? m_legItem.m_dropPrefab.name : "");
        visEq.SetHelmetItem(m_helmetItem != null ? m_helmetItem.m_dropPrefab.name : "");
        visEq.SetShoulderItem(m_shoulderItem != null ? m_shoulderItem.m_dropPrefab.name : "", m_shoulderItem?.m_variant ?? 0);
        visEq.SetUtilityItem(m_utilityItem != null ? m_utilityItem.m_dropPrefab.name : "");
        visEq.SetBeardItem(m_beardItem);
        visEq.SetHairItem(m_hairItem);
        if (TryGetComponent(out RandomHuman randomHuman))
        {
            visEq.SetHairColor(randomHuman.m_hairColor);
        }
    }

    private void SaveInventory()
    {
        if (m_loading || !IsOwner()) return;
        ZPackage pkg = new ZPackage();
        m_inventory.Save(pkg);
        string? data = pkg.GetBase64();
        m_nview.GetZDO().Set(ZDOVars.s_items, data);
        m_lastRevision = m_nview.GetZDO().DataRevision;
        m_lastDataString = data;
        m_nview.GetZDO().Set("InventoryChanged".GetStableHashCode(), true);
        m_inventoryChanged = true;
    }

    private void LoadInventory()
    {
        if (m_nview.GetZDO().DataRevision == m_lastRevision) return;
        string? data = m_nview.GetZDO().GetString(ZDOVars.s_items);
        if (data.IsNullOrWhiteSpace() || m_lastDataString == data) return;
        ZPackage pkg = new ZPackage(data);
        m_loading = true;
        m_inventory.Load(pkg);
        m_loading = false;
        m_lastRevision = m_nview.GetZDO().DataRevision;
        m_lastDataString = data;
        UpdateEquipment(false);
    }

    private void CheckEquipment(ItemDrop.ItemData? item, List<ItemDrop.ItemData> inventory)
    {
        if (item == null) return;
        if (inventory.Any(x => x.m_shared.m_name == item.m_shared.m_name)) return;
        UnequipItem(item);
    }

    private void UpdateEquipment(bool toggle = true)
    {
        m_inventoryChanged = false;
        if (m_companionAI != null)
        {
            m_companionAI.m_fishTarget = null;
            m_companionAI.m_rockTarget = null;
            m_companionAI.m_treeTarget = null;
        }

        List<ItemDrop.ItemData>? items = m_inventory.GetAllItems();
        CheckEquipment(m_helmetItem, items);
        CheckEquipment(m_chestItem, items);
        CheckEquipment(m_legItem, items);
        CheckEquipment(m_shoulderItem, items);
        CheckEquipment(m_rightItem, items);
        CheckEquipment(m_leftItem, items);
        CheckEquipment(m_utilityItem, items);
        foreach (ItemDrop.ItemData? item in items.OrderBy(x => x.m_shared.m_armor).ToList())
        {
            if (!item.IsEquipable()) continue;
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Helmet:
                    if (m_helmetItem == null ||
                        (m_helmetItem != null && item.m_shared.m_armor > m_helmetItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Chest:
                    if (m_chestItem == null ||
                        (m_chestItem != null && item.m_shared.m_armor > m_chestItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Legs:
                    if (m_legItem == null || (m_legItem != null && item.m_shared.m_armor > m_legItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                case ItemDrop.ItemData.ItemType.Shoulder:
                    if (m_shoulderItem == null ||
                        (m_shoulderItem != null && item.m_shared.m_armor > m_shoulderItem.m_shared.m_armor))
                    {
                        if (toggle) ToggleEquipped(item);
                        else EquipItem(item);
                    }
                    break;
                default:
                    if (toggle) ToggleEquipped(item);
                    else EquipItem(item);
                    break;
            }
        }
    }
    public override void OnDestroy()
    {
        base.OnDestroy();
        m_instances.Remove(this);
    }

    public void TamingUpdate()
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner() || IsTamed() || IsHungry()) return;
        if (m_companionAI == null) return;
        if (m_companionAI.IsAlerted()) return;
        m_companionAI.SetDespawnInDay(false);
        m_companionAI.SetEventCreature(false);
        DecreaseRemainingTime(3f);
        if (GetRemainingTamingTime() <= 0.0)
        {
            Tame();
            CancelInvoke(nameof(TamingUpdate));
        }
        else
        {
            Transform transform1 = transform;
            m_sootheEffect.Create(transform1.position, transform1.rotation);
        }
    }

    public override void ApplyArmorDamageMods(ref HitData.DamageModifiers mods)
    {
        if (m_chestItem != null) mods.Apply(m_chestItem.m_shared.m_damageModifiers);
        if (m_legItem != null) mods.Apply(m_legItem.m_shared.m_damageModifiers);
        if (m_helmetItem != null) mods.Apply(m_helmetItem.m_shared.m_damageModifiers);
        if (m_shoulderItem != null) mods.Apply(m_shoulderItem.m_shared.m_damageModifiers);
    }

    public override float GetBodyArmor()
    {
        float bodyArmor = 0.0f;
        if (m_chestItem != null) bodyArmor += m_chestItem.GetArmor();
        if (m_legItem != null) bodyArmor += m_legItem.GetArmor();
        if (m_helmetItem != null) bodyArmor += m_helmetItem.GetArmor();
        if (m_shoulderItem != null) bodyArmor += m_shoulderItem.GetArmor();
        return bodyArmor;
    }

    public void ResetFeedingTimer() => m_nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, ZNet.instance.GetTime().Ticks);

    public void OnConsumedItem(ItemDrop item)
    {
        if (m_companionAI == null) return;
        if (IsHungry()) m_sootheEffect.Create(m_companionAI.m_character.GetCenterPoint(), Quaternion.identity);
        m_fedDuration = item.m_itemData.m_shared.m_foodBurnTime;
        ResetFeedingTimer();
        
        if (item.m_itemData.m_shared.m_consumeStatusEffect)
        {
            m_seman.AddStatusEffect(item.m_itemData.m_shared.m_consumeStatusEffect, true);
        }

        if (item.m_itemData.m_shared.m_food > 0.0) EatFood(item.m_itemData);
    }

    private void UpdateFood(float dt, bool forceUpdate)
    {
        if (!IsTamed()) return;
        m_foodUpdateTimer += dt;
        if (m_foodUpdateTimer < 1f && !forceUpdate) return;
        m_foodUpdateTimer = 0.0f;
        if (!forceUpdate) UpdateConsumeFromInventory();
        UpdateFoodTimers();
        GetTotalFoodValue(out float hp, out float stamina, out float eitr);
        SetMaxHealth(hp);
        SetMaxEitr(eitr);
    }

    private void SetMaxEitr(float amount)
    {
        m_maxEitr = amount;
        m_eitr = Mathf.Clamp(m_eitr, 0.0f, m_maxEitr);
    }

    private void UpdateStats(float dt)
    {
        m_statsTimer += dt;
        if (m_statsTimer < 1f) return;
        m_statsTimer = 0.0f;
        UpdateEncumbered();
        if (m_maxEitr <= 0) return;
        float eitrRegen = 2f;
        float eitrMultiplier = 1f;
        m_seman.ModifyEitrRegen(ref eitrMultiplier);
        float amount = eitrRegen * eitrMultiplier;
        m_eitr = Mathf.Clamp(m_eitr + amount, 0f, m_maxEitr);
    }
    private void UpdateFoodTimers()
    {
        foreach (Player.Food? food in m_foods)
        {
            --food.m_time;
            float num = Mathf.Pow(Mathf.Clamp01(food.m_time / food.m_item.m_shared.m_foodBurnTime), 0.3f);
            food.m_health = food.m_item.m_shared.m_food * num;
            food.m_stamina = food.m_item.m_shared.m_foodStamina * num;
            food.m_eitr = food.m_item.m_shared.m_foodEitr * num;
            if (food.m_time <= 0.0)
            {
                m_foods.Remove(food);
                break;
            }
        }
    }

    private void UpdateConsumeFromInventory()
    {
        if (m_foods.Count >= 3) return;
        ItemDrop.ItemData? consumedItem = null;
        foreach (ItemDrop.ItemData? item in GetInventory().GetAllItems())
        {
            if (m_foods.Count >= 3) break;
            if (item.m_shared.m_itemType is not ItemDrop.ItemData.ItemType.Consumable) continue;
            if (item.m_shared.m_consumeStatusEffect)
            {
                m_seman.AddStatusEffect(item.m_shared.m_consumeStatusEffect);
            }
            if (EatFood(item))
            {
                Transform transform1 = transform;
                m_sootheEffect.Create(transform1.position, transform1.rotation);
                consumedItem = item;
                m_fedDuration = item.m_shared.m_foodBurnTime;
                ResetFeedingTimer();
                m_animator.SetTrigger(Consume);
                break;
            }
        }

        if (consumedItem != null) GetInventory().RemoveOneItem(consumedItem);
    }

    private void GetTotalFoodValue(out float hp, out float stamina, out float eitr)
    {
        hp = m_baseHealth;
        stamina = 50f;
        eitr = 0.0f;
        foreach (Player.Food? food in m_foods)
        {
            hp += food.m_health;
            stamina += food.m_stamina;
            eitr += food.m_eitr;
        }
    }

    private bool EatFood(ItemDrop.ItemData item)
    {
        if (!IsTamed()) return false;
        if (!CanEat(item)) return false;
        foreach (Player.Food food in m_foods)
        {
            if (food.m_item.m_shared.m_name == item.m_shared.m_name)
            {
                if (!food.CanEatAgain()) return false;
                food.m_time = item.m_shared.m_foodBurnTime;
                food.m_health = item.m_shared.m_food;
                food.m_stamina = item.m_shared.m_foodStamina;
                food.m_eitr = item.m_shared.m_foodEitr;
                UpdateFood(0.0f, true);
                return true;
            }
        }

        if (m_foods.Count < 3)
        {
            m_foods.Add(new Player.Food()
            {
                m_name = item.m_dropPrefab.name,
                m_item = item,
                m_time = item.m_shared.m_foodBurnTime,
                m_health = item.m_shared.m_food,
                m_stamina = item.m_shared.m_foodStamina,
                m_eitr = item.m_shared.m_foodEitr
            });
            UpdateFood(0.0f, true);
            return true;
        }

        Player.Food? mostDepletedFood = GetMostDepletedFood();
        if (mostDepletedFood == null) return false;
        mostDepletedFood.m_name = item.m_dropPrefab.name;
        mostDepletedFood.m_item = item;
        mostDepletedFood.m_time = item.m_shared.m_foodBurnTime;
        mostDepletedFood.m_health = item.m_shared.m_food;
        mostDepletedFood.m_stamina = item.m_shared.m_foodStamina;
        UpdateFood(0.0f, true);
        return true;

    }

    private Player.Food? GetMostDepletedFood()
    {
        Player.Food? result = null;
        foreach (var food in m_foods)
        {
            if (food.CanEatAgain() && (result == null || food.m_time < result.m_time)) result = food;
        }

        return result;
    }

    private bool CanEat(ItemDrop.ItemData item)
    {
        foreach (Player.Food food in m_foods)
        {
            if (food.m_item.m_shared.m_name == item.m_shared.m_name)
            {
                return food.CanEatAgain();
            }
        }

        foreach (Player.Food food in m_foods)
        {
            if (food.CanEatAgain()) return true;
        }

        return m_foods.Count < 3;
    }

    public void Tame()
    {
        if (!m_nview.IsValid() || !m_nview.IsOwner() || IsTamed()) return;
        if (m_companionAI == null) return;
        m_companionAI.MakeTame();
        Transform transform1 = transform;
        Vector3 position = transform1.position;
        m_tamedEffect.Create(position, transform1.rotation);
        Player closest = Player.GetClosestPlayer(position, 30f);
        m_companionAI.m_aggravatable = false;
        if (!closest) return;
        closest.Message(MessageHud.MessageType.Center, $"{m_name} $hud_tamedone");
        SetOwner(closest);
        m_faction = Faction.Players;
    }

    private void SetOwner(Player player)
    {
        if (m_nview.GetZDO().GetLong(m_ownerKey) != 0L) return;
        m_nview.GetZDO().Set(m_ownerKey, player.GetPlayerID());
        m_nview.GetZDO().Set(m_ownerNameKey, player.GetPlayerName());
    }

    private static void TameAll(Vector3 point, float radius)
    {
        foreach (Companion companion in m_instances)
        {
            if (Vector3.Distance(companion.transform.position, point) > radius) continue;
            companion.Tame();
        }
    }

    public static void MakeAllFollow(Player player, float radius)
    {
        int count = 0;
        foreach (Companion companion in m_instances)
        {
            if (!companion.IsTamed()) continue;
            if (companion.m_companionAI == null) continue;
            if (companion.m_companionAI.GetFollowTarget() != null) continue;
            if (Vector3.Distance(player.transform.position, companion.transform.position) > radius) continue;
            companion.Command(player, false);
            ++count;
        }
        player.Message(MessageHud.MessageType.Center, count + " $msg_settlerfollows");
    }
    
    public void DecreaseRemainingTime(float time)
    {
        if (!m_nview.IsValid()) return;
        float num = GetRemainingTamingTime() - time;
        if (num < 0.0)
        {
            num = 0.0f;
        }
        m_nview.GetZDO().Set(ZDOVars.s_tameTimeLeft, num);
    }

    private float GetRemainingTamingTime() => !m_nview.IsValid() ? 0.0f : m_nview.GetZDO().GetFloat(ZDOVars.s_tameTimeLeft, m_tamingTime);

    private string GetStatus()
    {
        if (m_baseAI.IsAlerted()) return "$hud_tamefrightened";
        if (IsHungry()) return "$hud_tamehungry";
        if (IsEncumbered()) return "$hud_encumbered";
        if (m_companionAI != null && m_companionAI.m_resting) return "$hud_tametired";
        if (m_companionAI != null && !m_companionAI.GetCurrentAction().IsNullOrWhiteSpace())
            return $"$hud_{m_companionAI.GetCurrentAction()}";
        return IsTamed() ? "$hud_tamehappy" : "$hud_tameinprogress";
    }

    public bool IsHungry()
    {
        if (m_nview == null) return false;
        if (!m_nview.IsValid()) return false;
        DateTime dateTime = new DateTime(m_nview.GetZDO().GetLong(ZDOVars.s_tameLastFeeding));
        return (ZNet.instance.GetTime() - dateTime).TotalSeconds > m_fedDuration;
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        if (!m_nview.IsValid() || hold) return false;
        if (!IsTamed()) return false;
        long playerID = Game.instance.GetPlayerProfile().GetPlayerID();
        if (alt)
        {
            if (!CheckAccess(playerID))
            {
                user.Message(MessageHud.MessageType.Center, "$msg_cantopen");
                    
                return true;
            }
            m_nview.InvokeRPC(nameof(RPC_RequestOpen), playerID);
        }
        else
        {
            if (Time.time - m_lastPetTime <= 1.0) return false;
            if (user is Player player) SetOwner(player);
            m_lastPetTime = Time.time;

            Transform transform1 = transform;
            m_petEffect.Create(transform1.position, transform1.rotation);
            if (m_followTargetName.IsNullOrWhiteSpace() || m_followTargetName == user.GetHoverName())
            {
                Command(user);
            }
            else
            {
                user.Message(MessageHud.MessageType.Center, $"$msg_already_following {m_followTargetName}");
            }
        }
        return true;
    }

    private string GetOwnerName()
    {
        long ownerID = m_nview.GetZDO().GetLong(m_ownerKey);
        return ownerID == 0L ? "" : m_nview.GetZDO().GetString(m_ownerNameKey);
    }

    public float GetEitr() => m_eitr;
    public override float GetMaxEitr() => m_maxEitr;
    public override void UseEitr(float eitr)
    {
        if (eitr == 0) return;
        m_eitr -= eitr;
        if (m_eitr < 0) m_eitr = 0;
    }

    public override string GetHoverText()
    {
        if (!m_nview.IsValid()) return "";
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(m_name);
        if (IsTamed())
        {
            stringBuilder.AppendFormat(" ( {0} )", GetStatus());
            stringBuilder.AppendFormat("\n[<color=yellow><b>{0}</b></color>] {1}", 
                "$KEY_Use", 
                m_companionAI != null && m_companionAI.GetFollowTarget() == null ? "$hud_follow" : "$hud_stay");
            string ownerName = GetOwnerName();
            if (m_nview.GetZDO().GetLong(m_ownerKey) == 0L || m_nview.GetZDO().GetLong(m_ownerKey) == Player.m_localPlayer.GetPlayerID())
                stringBuilder.Append("\n[<color=yellow><b>L.Shift + $KEY_Use</b></color>] $hud_interact");
            if (!string.IsNullOrWhiteSpace(ownerName)) stringBuilder.AppendFormat("\n$hud_owner: {0}", ownerName);
            stringBuilder.AppendFormat("\n$se_health: {0}/{1}", (int)GetHealth(), (int)GetMaxHealth());
            stringBuilder.AppendFormat("\n$item_armor: {0}", (int)GetBodyArmor());
            if (GetMaxEitr() > 0) stringBuilder.AppendFormat("\n$se_eitr: <color=#E6E6FA>{0}</color>/<color=#B19CD9>{1}</color>", (int)GetEitr(), (int)GetMaxEitr());
        }
        else
        {
            int tameness = GetTameness();
            if (tameness <= 0)
            {
                stringBuilder.AppendFormat(" ( $hud_wild, {0} )", GetStatus());
            }
            else
            {
                stringBuilder.AppendFormat(" ( $hud_tameness {0}%, {1} )", tameness.ToString(), GetStatus());
            }
        }

        return Localization.instance.Localize(stringBuilder.ToString());
    }

    private int GetTameness() => (int)((1.0 - Mathf.Clamp01(GetRemainingTamingTime() / m_tamingTime)) * 100.0);

    public override string GetHoverName() => m_name;
    
    public void Command(Humanoid user, bool message = true) => m_nview.InvokeRPC(nameof(RPC_Command), user.GetZDOID(), message);

    public void RPC_Command(long sender, ZDOID characterID, bool message)
    {
        GameObject instance = ZNetScene.instance.FindInstance(characterID);
        if (!instance) return;
        if (!instance.TryGetComponent(out Player player)) return;
        if (m_companionAI == null) return;
        if (!m_nview.IsOwner()) m_nview.ClaimOwnership();
        if (m_companionAI.GetFollowTarget())
        {
            m_companionAI.SetFollowTarget(null);
            m_companionAI.SetPatrolPoint();
            m_nview.GetZDO().Set(ZDOVars.s_follow, "");
            m_followTargetName = "";
            if (message) player.Message(MessageHud.MessageType.Center, $"{m_name} $hud_tamestay");
        }
        else
        {
            m_companionAI.ResetPatrolPoint();
            m_companionAI.SetFollowTarget(player.gameObject);
            m_nview.GetZDO().Set(ZDOVars.s_follow, player.GetPlayerName());
            m_followTargetName = player.GetHoverName();
            if (message) player.Message(MessageHud.MessageType.Center, $"{m_name} $hud_tamefollow");
        }
    }

    public void RPC_RequestOpen(long uid, long playerID)
    {
        if (m_inUse)
        {
            m_nview.InvokeRPC(uid, nameof(RPC_OpenResponse), false);
        }
        else
        {
            if (!m_nview.IsOwner()) m_nview.ClaimOwnership();
            ZDOMan.instance.ForceSendZDO(uid, m_nview.GetZDO().m_uid);
            m_nview.GetZDO().SetOwner(uid);
            m_nview.InvokeRPC(nameof(RPC_OpenResponse), true);
        }
    }

    public void RPC_OpenResponse(long uid, bool granted)
    {
        if (!Player.m_localPlayer) return;
        if (granted)
        {
            Hud.HidePieceSelection();
            InventoryGui.instance.m_animator.SetBool(Visible, true);
            InventoryGui.instance.SetActiveGroup(1, false);
            InventoryGui.instance.SetupCrafting();
            m_currentCompanion = this;
        }
        else
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
        }
    }

    public void StackAll() => m_nview.InvokeRPC(nameof(RPC_RequestStack), Game.instance.GetPlayerProfile().GetPlayerID());

    public void RPC_RequestStack(long uid, long playerID)
    {
        if (!m_nview.IsOwner()) return;
        if (m_inUse || uid != ZNet.GetUID())
        {
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse), false);
        }
        else if (!CheckAccess(playerID))
        {
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse), false);
        }
        else
        {
            ZDOMan.instance.ForceSendZDO(uid, m_nview.GetZDO().m_uid);
            m_nview.GetZDO().SetOwner(uid);
            m_nview.InvokeRPC(uid, nameof(RPC_StackResponse), true);
        }
        
    }

    public void RPC_StackResponse(long uid, bool granted)
    {
        if (!Player.m_localPlayer) return;
        if (granted)
        {
            if (GetInventory().StackAll(Player.m_localPlayer.GetInventory(), true) <= 0) return;
            InventoryGui.instance.m_moveItemEffects.Create(transform.position, Quaternion.identity);
            UpdateEquipment();
        }
        else
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$msg_inuse");
        }
    }

    private bool CheckAccess(long playerID)
    {
        var owner = m_nview.GetZDO().GetLong(m_ownerKey);
        if (owner == 0L) return true;
        return playerID == owner;
    }

    public override bool IsEncumbered() => GetInventory().GetTotalWeight() > GetMaxCarryWeight();

    private float GetMaxCarryWeight()
    {
        float max = SettlersPlugin._baseMaxCarryWeight.Value;
        m_seman.ModifyMaxCarryWeight(max, ref max);
        return max;
    }

    public float GetWeight() => GetInventory().GetTotalWeight();

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    public override void RaiseSkill(Skills.SkillType skill, float value = 1)
    {
        if (m_companionAI == null) return;
        GameObject followTarget = m_companionAI.GetFollowTarget();
        if (followTarget == null) return;
        if (!followTarget.TryGetComponent(out Character component)) return;
        Skills skills = component.GetSkills();
        if (skills == null) return;
        skills.RaiseSkill(Skills.SkillType.BloodMagic, value * 0.5f);
    }

    

    public override void AttachStart(Transform attachPoint, GameObject colliderRoot, bool hideWeapons, bool isBed, bool onShip,
        string attachAnimation, Vector3 detachOffset, Transform cameraPos = null)
    {
        if (m_attached) return;
        m_attached = true;
        m_attachedToShip = onShip;
        m_attachPoint = attachPoint;
        m_detachOffset = detachOffset;
        m_attachAnimation = attachAnimation;
        m_animator.SetBool(attachAnimation, true);
        if (colliderRoot != null)
        {
            m_attachColliders = colliderRoot.GetComponentsInChildren<Collider>();
            foreach (Collider collider in m_attachColliders)
            {
                Physics.IgnoreCollision(m_collider, collider, true);
            }
        }

        if (hideWeapons) HideHandItems();
        else
        {
            if (m_leftItem == null && m_rightItem == null) return;
            ItemDrop.ItemData? leftItem = m_leftItem;
            ItemDrop.ItemData? rightItem = m_rightItem;
            if (m_rightItem?.m_shared.m_itemType is not ItemDrop.ItemData.ItemType.Torch)
            {
                UnequipItem(m_rightItem);
                m_hiddenRightItem = rightItem;
            }

            if (m_leftItem?.m_shared.m_itemType is not ItemDrop.ItemData.ItemType.Torch)
            {
                UnequipItem(m_leftItem);
                m_hiddenLeftItem = leftItem;
            }
            SetupVisEquipment(m_visEquipment, false);
        }
        UpdateAttach();
        ResetCloth();
    }

    private bool UpdateAttach()
    {
        if (!m_attached) return false;
        if (m_attachPoint == null)
        {
            AttachStop();
            return false;
        }
        transform.position = m_attachPoint.position;
        transform.rotation = m_attachPoint.rotation;
        Rigidbody component = m_attachPoint.GetComponentInParent<Rigidbody>();
        m_body.useGravity = false;
        m_body.velocity = component ? component.GetPointVelocity(transform.position) : Vector3.zero;
        m_body.angularVelocity = Vector3.zero;
        m_maxAirAltitude = transform.position.y;
        return true;
    }

    public override bool IsAttachedToShip() => m_attached && m_attachedToShip;

    public override void AttachStop()
    {
        if (!m_attached) return;
        if (m_attachPoint != null)
        {
            transform.position = m_attachPoint.TransformPoint(m_detachOffset);
        }

        if (m_attachColliders != null)
        {
            foreach (Collider collider in m_attachColliders)
            {
                if (collider)
                {
                    Physics.IgnoreCollision(m_collider, collider, false);
                }
            }

            m_attachColliders = null;
        }

        m_body.useGravity = true;
        m_attached = false;
        m_attachPoint = null;
        m_animator.SetBool(m_attachAnimation, false);
        ResetCloth();
    }
    
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    private static class Companion_ContainerOverride
    {
        private static bool Prefix(InventoryGui __instance, Player player)
        {
            if (!__instance.m_animator.GetBool(Visible)) return true;
            if (__instance.m_currentContainer)
            {
                m_currentCompanion = null;
                return true;
            }

            if (m_currentCompanion == null) return true;
            if (m_currentCompanion != null && m_currentCompanion.IsOwner())
            {
                m_currentCompanion.m_inUse = true;
                __instance.m_container.gameObject.SetActive(true);
                __instance.m_containerGrid.UpdateInventory(m_currentCompanion.GetInventory(), null, __instance.m_dragItem);
                __instance.m_containerName.text = m_currentCompanion.GetHoverName();
                if (__instance.m_firstContainerUpdate)
                {
                    __instance.m_containerGrid.ResetView();
                    __instance.m_firstContainerUpdate = false;
                    __instance.m_containerHoldTime = 0.0f;
                    __instance.m_containerHoldState = 0;
                }

                if (Vector3.Distance(m_currentCompanion.transform.position, player.transform.position) >
                    __instance.m_autoCloseDistance)
                {
                    if (__instance.m_dragInventory != null &&
                        __instance.m_dragInventory != Player.m_localPlayer.GetInventory())
                    {
                        __instance.SetupDragItem(null, null, 1);
                    }
                    CloseCompanionInventory(m_currentCompanion.m_inventoryChanged);
                    __instance.m_splitPanel.gameObject.SetActive(false);
                    __instance.m_firstContainerUpdate = true;
                    __instance.m_container.gameObject.SetActive(false);
                }

                if (ZInput.GetButton("Use") || ZInput.GetButton("JoyUse"))
                {
                    __instance.m_containerHoldTime += Time.deltaTime;
                    if (__instance.m_containerHoldTime > __instance.m_containerHoldPlaceStackDelay &&
                        __instance.m_containerHoldState == 0)
                    {
                        m_currentCompanion.StackAll();
                        __instance.m_containerHoldState = 1;
                    }
                    else
                    {
                        if (__instance.m_containerHoldTime <= __instance.m_containerHoldPlaceStackDelay +
                            __instance.m_containerHoldExitDelay || __instance.m_containerHoldState != 1)
                        {
                            return false;
                        }
                        __instance.Hide();
                    }
                }
                else
                {
                    if (__instance.m_containerHoldState < 0) return false;
                    __instance.m_containerHoldState = -1;
                }
            }
            else
            {
                __instance.m_container.gameObject.SetActive(false);
                if (__instance.m_dragInventory == null ||
                    __instance.m_dragInventory == Player.m_localPlayer.GetInventory()) return false;
                __instance.SetupDragItem(null, null, 1);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static class InventoryGUI_Hide_Patch
    {
        private static void Postfix()
        {
            CloseCompanionInventory(m_currentCompanion != null && m_currentCompanion.m_inventoryChanged);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsContainerOpen))]
    private static class InventoryGUI_IsContainerOpen_Patch
    {
        private static void Postfix(ref bool __result)
        {
            if (m_currentCompanion != null) __result = true;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTakeAll))]
    private static class InventoryGUI_OnTakeAll_Patch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            if (Player.m_localPlayer.IsTeleporting() || m_currentCompanion == null) return true;
            __instance.SetupDragItem(null, null, 1);
            Inventory inventory = m_currentCompanion.GetInventory();
            Player.m_localPlayer.GetInventory().MoveAll(inventory);
            m_currentCompanion.UpdateEquipment();
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnStackAll))]
    private static class InventoryGUI_OnStackAll_Patch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            if (Player.m_localPlayer.IsTeleporting() || m_currentCompanion == null) return true;
            __instance.SetupDragItem(null, null, 1);
            m_currentCompanion.GetInventory().StackAll(Player.m_localPlayer.GetInventory());
            m_currentCompanion.UpdateEquipment();
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainerWeight))]
    private static class InventoryGUI_UpdateContainerWeight_Patch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (m_currentCompanion == null) return;

            __instance.m_containerWeight.text = string.Format("{0}/{1}", (int)m_currentCompanion.GetWeight(),
                (int)m_currentCompanion.GetMaxCarryWeight());
        }
    }
    
    private static void CloseCompanionInventory(bool updateEquipment = true)
    {
        if (m_currentCompanion == null) return;
        if (updateEquipment) m_currentCompanion.UpdateEquipment();
        m_currentCompanion.m_inUse = false;
        m_currentCompanion = null;
    }

    [HarmonyPatch(typeof(Character), nameof(RPC_Damage))]
    public static class Character_RPC_Damage_Patch
    {
        private static bool Prefix(Character __instance, HitData hit)
        {
            if (__instance is not Companion companion) return true;

            if (hit.GetAttacker() == Player.m_localPlayer)
            {
                Game.instance.IncrementPlayerStat(PlayerStatType.EnemyHits);
                companion.m_localPlayerHasHit = true;
            }

            if (!companion.m_nview.IsOwner() || companion.GetHealth() <= 0.0 || companion.IsDead() ||
                companion.IsTeleporting() || hit.m_dodgeable && companion.IsDodgeInvincible()) return false;

            Character attacker = hit.GetAttacker();
            if (hit.HaveAttacker() && attacker == null) return false;
            if (attacker != null && !attacker.IsPlayer())
            {
                float damageScalePlayer = Game.instance.GetDifficultyDamageScalePlayer(__instance.transform.position);
                hit.ApplyModifier(damageScalePlayer);
                hit.ApplyModifier(Game.m_enemyDamageRate);
            }
            companion.m_seman.OnDamaged(hit, attacker);
            if (companion.m_baseAI != null && companion.m_baseAI.IsAggravatable() &&
                !companion.m_baseAI.IsAggravated() && attacker != null && attacker.IsPlayer() &&
                hit.GetTotalDamage() > 0.0)
            {
                BaseAI.AggravateAllInArea(__instance.transform.position, 20f, BaseAI.AggravatedReason.Damage);
            }

            if (companion.m_baseAI != null && !companion.m_baseAI.IsAlerted() && hit.m_backstabBonus > 1.0 &&
                Time.time - companion.m_backstabTime > 300.0 &&
                (!ZoneSystem.instance.GetGlobalKey(GlobalKeys.PassiveMobs) ||
                 !companion.m_baseAI.CanSeeTarget(attacker)))
            {
                companion.m_backstabTime = Time.time;
                hit.ApplyModifier(hit.m_backstabBonus);
                companion.m_backstabHitEffects.Create(hit.m_point, Quaternion.identity, companion.transform);
            }

            if (companion.IsStaggering())
            {
                hit.ApplyModifier(2f);
                companion.m_critHitEffects.Create(hit.m_point, Quaternion.identity, companion.transform);
            }

            if (hit.m_blockable && companion.IsBlocking())
            {
                companion.BlockAttack(hit, attacker);
            }
            companion.ApplyPushback(hit);
            if (hit.m_statusEffectHash != 0)
            {
                StatusEffect statusEffect = companion.m_seman.GetStatusEffect(hit.m_statusEffectHash);
                if (statusEffect == null)
                {
                    statusEffect = companion.m_seman.AddStatusEffect(hit.m_statusEffectHash,
                        itemLevel: hit.m_itemLevel, skillLevel: hit.m_skillLevel);
                }
                else
                {
                    statusEffect.ResetTime();
                    statusEffect.SetLevel(hit.m_itemLevel, hit.m_skillLevel);
                }

                if (statusEffect != null && attacker != null)
                {
                    statusEffect.SetAttacker(attacker);
                }
            }

            WeakSpot weakSpot = companion.GetWeakSpot(hit.m_weakSpot);
            HitData.DamageModifiers damageModifiers = companion.GetDamageModifiers(weakSpot);
            hit.ApplyResistance(damageModifiers, out var significantModifier);
            float bodyArmor = companion.GetBodyArmor();
            hit.ApplyArmor(bodyArmor);
            companion.DamageArmorDurability(hit);
            float poison = hit.m_damage.m_poison;
            float fire = hit.m_damage.m_fire;
            float spirit = hit.m_damage.m_spirit;
            hit.m_damage.m_poison = 0.0f;
            hit.m_damage.m_fire = 0.0f;
            hit.m_damage.m_spirit = 0.0f;
            companion.ApplyDamage(hit, true, true, significantModifier);
            companion.AddFireDamage(fire);
            companion.AddSpiritDamage(spirit);
            companion.AddPoisonDamage(poison);
            companion.AddFrostDamage(hit.m_damage.m_frost);
            companion.AddLightningDamage(hit.m_damage.m_lightning);
            return false;
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(EquipBestWeapon))]
    private static class EquipBestWeapon_Patch
    {
        private static bool Prefix(Humanoid __instance)
        {
            if (__instance is not Companion component) return true;

            List<ItemDrop.ItemData> allItems = component.GetInventory().GetAllItems();
            if (allItems.Count == 0 || component.InAttack()) return false;
            
            ModifyRangedItems(allItems);
            return false;
        }

        private static void ModifyRangedItems(List<ItemDrop.ItemData> allItems)
        {
            foreach (ItemDrop.ItemData? item in allItems)
            {
                if (!item.IsWeapon()) continue;

                switch (item.m_shared.m_skillType)
                {
                    case Skills.SkillType.Bows:
                        item.m_shared.m_attack.m_projectileVel = 30f;
                        item.m_shared.m_attack.m_projectileVelMin = 2f;
                        item.m_shared.m_attack.m_projectileAccuracy = 2f;
                        item.m_shared.m_attack.m_projectileAccuracyMin = 20f;
                        item.m_shared.m_attack.m_useCharacterFacingYAim = true;
                        item.m_shared.m_attack.m_launchAngle = 0f;
                        item.m_shared.m_attack.m_projectiles = 1;
                        item.m_shared.m_aiAttackRange = 30f;
                        item.m_shared.m_aiAttackRangeMin = 1f;
                        item.m_shared.m_aiAttackInterval = 12f;
                        item.m_shared.m_aiAttackMaxAngle = 15f;
                        item.m_shared.m_aiWhenFlying = true;
                        item.m_shared.m_aiWhenWalking = true;
                        item.m_shared.m_aiWhenSwiming = true;
                        break;
                    case Skills.SkillType.ElementalMagic:
                        item.m_shared.m_aiAttackRange = 20f;
                        item.m_shared.m_aiAttackRangeMin = 1f;
                        item.m_shared.m_aiAttackInterval = item.m_shared.m_name == "$item_stafficeshards" ? 30f : 12f;
                        item.m_shared.m_aiAttackMaxAngle = 15f;
                        item.m_shared.m_aiWhenFlying = true;
                        item.m_shared.m_aiWhenWalking = true;
                        item.m_shared.m_aiWhenSwiming = true;
                        break;
                    case Skills.SkillType.BloodMagic:
                        item.m_shared.m_aiAttackRange = 20f;
                        item.m_shared.m_aiAttackRangeMin = 1f;
                        item.m_shared.m_aiAttackInterval = 30f;
                        item.m_shared.m_aiAttackMaxAngle = 15f;
                        item.m_shared.m_aiWhenFlying = true;
                        item.m_shared.m_aiWhenWalking = true;
                        item.m_shared.m_aiWhenSwiming = true;
                        break;
                    case Skills.SkillType.Crossbows:
                        item.m_shared.m_attack.m_projectileVel = 200f;
                        item.m_shared.m_attack.m_projectileVelMin = 2f;
                        item.m_shared.m_attack.m_projectileAccuracy = 2f;
                        item.m_shared.m_attack.m_projectileAccuracyMin = 20f;
                        item.m_shared.m_attack.m_useCharacterFacingYAim = true;
                        item.m_shared.m_attack.m_launchAngle = 0f;
                        item.m_shared.m_attack.m_projectiles = 1;
                        item.m_shared.m_aiAttackRange = 30f;
                        item.m_shared.m_aiAttackRangeMin = 1f;
                        item.m_shared.m_aiAttackInterval = 12f;
                        item.m_shared.m_aiAttackMaxAngle = 15f;
                        item.m_shared.m_aiWhenFlying = true;
                        item.m_shared.m_aiWhenWalking = true;
                        item.m_shared.m_aiWhenSwiming = true;
                        item.m_shared.m_attack.m_attackProjectile = ZNetScene.instance.GetPrefab("DvergerArbalest_projectile");
                        break;
                    case Skills.SkillType.None:
                        if (item.m_shared.m_attack.m_attackType is Attack.AttackType.Projectile)
                        {
                            item.m_shared.m_attack.m_projectileVel = 30f;
                            item.m_shared.m_attack.m_projectileVelMin = 2f;
                            item.m_shared.m_attack.m_projectileAccuracy = 2f;
                            item.m_shared.m_attack.m_projectileAccuracyMin = 20f;
                            item.m_shared.m_attack.m_useCharacterFacingYAim = true;
                            item.m_shared.m_attack.m_launchAngle = 0f;
                            item.m_shared.m_attack.m_projectiles = 1;
                            item.m_shared.m_aiAttackRange = 30f;
                            item.m_shared.m_aiAttackRangeMin = 1f;
                            item.m_shared.m_aiAttackInterval = 12f;
                            item.m_shared.m_aiAttackMaxAngle = 15f;
                            item.m_shared.m_aiWhenFlying = true;
                            item.m_shared.m_aiWhenWalking = true;
                            item.m_shared.m_aiWhenSwiming = true;
                        }
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.TameAllInArea))]
    private static class Tameable_TameAllInArea_Patch
    {
        private static void Postfix(Vector3 point, float radius) => TameAll(point, 30f);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TeleportTo))]
    private static class Player_TeleportTo_Patch
    {
        private static void Postfix(Player __instance, Vector3 pos, Quaternion rot)
        {
            if (!__instance) return;
            List<Companion> companions = new();
            foreach (Companion instance in m_instances)
            {
                if (Vector3.Distance(instance.transform.position, __instance.transform.position) > 20f) continue;
                if (!instance.IsTamed()) continue;
                if (instance.m_followTargetName != __instance.GetPlayerName()) continue;
                if (!instance.IsTeleportable()) continue;
                companions.Add(instance);
            }

            foreach (Companion companion in companions)
            {
                Vector3 random = Random.insideUnitSphere * 10f;
                Vector3 location = pos + new Vector3(random.x, 0f, random.z);
                if (!companion.m_nview.IsOwner()) companion.m_nview.ClaimOwnership();
                Transform transform = companion.transform;
                location.y = ZoneSystem.instance.GetSolidHeight(location) + 0.5f;
                transform.position = location;
                transform.rotation = rot;
                companion.m_body.velocity = Vector3.zero;
            }
        }
    }
    
    
    public class MinorActionData
    {
        public ActionType m_type;
        public ItemDrop.ItemData? m_item;
        // public string m_progressText = "";
        public float m_time;
        public float m_duration;
        public string m_animation = "";
        public string m_doneAnimation = "";
        public float m_staminaDrain;
        public float m_eitrDrain;
        public EffectList? m_startEffect;

        public enum ActionType
        {
            Equip,
            Unequip,
            Reload
        }
    }
}