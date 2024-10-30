using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using Settlers.Settlers;
using SkillManager;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Settlers.Behaviors;

public class Companion : Humanoid
{
    public static readonly List<Companion> m_instances = new();
    private static readonly int m_ownerKey = "VikingSettlerOwner".GetStableHashCode();
    private static readonly int m_ownerNameKey = "VikingSettlerOwnerName".GetStableHashCode();
    private static readonly int m_raider = "VikingRaider".GetStableHashCode();
    private static readonly int m_elf = "VikingElf".GetStableHashCode();
    private static readonly int m_sailor = "VikingSailor".GetStableHashCode();
    private static readonly int Blocking = Animator.StringToHash("blocking");

    public CompanionAI m_companionAI = null!;
    public CompanionTalk m_companionTalk = null!;
    public TameableCompanion tameableCompanion = null!;
    
    // private uint m_lastRevision;
    // private string m_lastDataString = "";
    private bool m_loading;
    public float m_autoPickupRange = 2f;
    public int m_autoPickupMask;
    public float m_checkDistanceTimer;
    public string m_followTargetName = "";
    public float m_playerMaxDistance = 50f;

    public EffectList m_warpEffect = new EffectList();
    public EffectList m_equipStartEffects = new();
    public EffectList m_killedEffects = new();
    public GameObject? m_tombstone;
    public readonly List<Player.Food> m_foods = new();
    private float m_foodUpdateTimer;
    private Heightmap.Biome m_currentBiome = Heightmap.Biome.None;
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
    public Chair? m_attachedChair;
    public Sadle? m_attachedSadle;
    public Transform? m_attachPoint;
    private Vector3 m_detachOffset;
    private string m_attachAnimation = "";
    private Collider[]? m_attachColliders;
    public bool m_startAsRaider;
    private Minimap.PinData? m_pin;
    private float m_pinTimer;
    public bool m_startAsElf;
    public bool m_startAsSailor;
    private bool m_teleporting;

    public float[]? m_equipmentModifierValues;

    public override void Awake()
    {
        
        base.Awake();
        m_equipmentModifierValues = new float[Player.s_equipmentModifierSources.Length];
        if (m_startAsRaider) SetRaider(true);
        if (m_startAsElf) SetElf(true);
        if (m_startAsSailor) SetSailor(true);
        
        m_autoPickupMask = LayerMask.GetMask("item");
        m_companionAI = GetComponent<CompanionAI>();
        m_companionTalk = GetComponent<CompanionTalk>();
        tameableCompanion = GetComponent<TameableCompanion>();

        m_nview.Register<Vector3, Vector3>(nameof(RPC_Warp), RPC_Warp);
        m_nview.Register<bool>(nameof(RPC_UpdateEquipment), RPC_UpdateEquipment);
        m_name = m_nview.GetZDO().GetString(ZDOVars.s_tamedName);
        m_instances.Add(this);
        m_visEquipment.m_isPlayer = true;

        GetFollowTargetName();
        if (IsTamed())
        {
            m_companionAI.m_aggravatable = false;
            // m_faction = Faction.Players;
        }
        
        SetMaxHealth(SettlersPlugin._SettlerBaseHealth.Value * m_level * GetSkillModifier());
        GetSEMan().AddStatusEffect(nameof(RaiderSE).GetStableHashCode());
    }
    public override void Start()
    {
        bool isRaider = IsRaider();
        bool isElf = IsElf();
        bool isSailor = IsSailor();
        if (isRaider || isSailor)
        {
            GetLoadOut(isElf, isSailor);
            SetGearQuality(m_level);
            SetMaxHealth(SettlersPlugin._raiderBaseHealth.Value * m_level * GetSkillModifier());
            // if (isRaider) m_faction = SettlersPlugin._raiderFaction.Value;
            GiveDefaultItems();
        }
        else
        {
            if (isElf && !IsTamed())
            {
                GetLoadOut(isElf, isSailor);
                SetGearQuality(m_level);
                SetMaxHealth(SettlersPlugin._raiderBaseHealth.Value * m_level * GetSkillModifier());
                GiveDefaultItems();
            }
            else
            {
                SetMaxHealth(SettlersPlugin._SettlerBaseHealth.Value * m_level * GetSkillModifier());
                LoadInventory();
                if (m_inventory.GetAllItems().Count == 0)
                {
                    m_defaultItems = SettlerGear.GetSettlerGear();
                    GiveDefaultItems();
                    SetGearQuality(m_level);
                }
            }
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
        if (!m_companionAI.IsAlerted() && IsBlocking())
        {
            m_animator.SetBool(Blocking, false);
            m_blocking = false;
            m_nview.GetZDO().Set(ZDOVars.s_isBlockingHash, false);
        }
        bool isSailor = IsSailor();
        if (IsRaider() || isSailor)
        {
            if (!isSailor) AutoPickup(fixedDeltaTime);
            UpdateActionQueue(fixedDeltaTime);
            UpdateWeaponLoading(GetCurrentWeapon(), fixedDeltaTime);
            if (isSailor)
            {
                UpdateAttach();
            }
        }
        else
        {
            if (!IsTamed())
            {
                UpdateAggravated();
                if (!IsElf()) return;
                AutoPickup(fixedDeltaTime);
                UpdateActionQueue(fixedDeltaTime);
                UpdateWeaponLoading(GetCurrentWeapon(), fixedDeltaTime);
                return;
            }
            if (UpdateAttach()) return;
            UpdateActionQueue(fixedDeltaTime);
            AutoPickup(fixedDeltaTime);
            UpdateWarp(fixedDeltaTime);
            UpdateFood(fixedDeltaTime, false);
            UpdateWeaponLoading(GetCurrentWeapon(), fixedDeltaTime);
            UpdateStats(fixedDeltaTime);
            UpdatePins(fixedDeltaTime);
        }
    }
    
    public void SetSkillModifier(float modifier)
    {
        if (modifier <= 0f) modifier = 1f;
        m_nview.ClaimOwnership();
        m_nview.GetZDO().Set("SkillModifier".GetStableHashCode(), modifier);
    }

    public float GetSkillModifier() => m_nview.GetZDO().GetFloat("SkillModifier".GetStableHashCode(), 1f);

    private void UpdateAggravated()
    {
        if (!m_companionAI.m_aggravated) return;
        if (m_companionAI.IsAlerted()) return;
        m_companionAI.SetAggravated(false, BaseAI.AggravatedReason.Damage);
    }

    public void SetGearQuality(int quality)
    {
        foreach (ItemDrop.ItemData? item in GetInventory().GetAllItems())
        {
            if (!item.IsEquipable()) continue;
            item.m_quality = quality;
        }
    }
    
    public override bool TeleportTo(Vector3 pos, Quaternion rot, bool distantTeleport)
    {
        if (!m_nview.IsOwner())
        {
            // Tells everyone in the area to run this command, only the owner of the companion will successfully teleport
            m_nview.InvokeRPC(nameof(RPC_TeleportTo), pos, rot, distantTeleport);
            return false;
        }
        Teleport(pos, rot, distantTeleport);
        return true;
    }
    public void Teleport(Vector3 pos, Quaternion rot, bool distantTeleport)
    {
        m_teleporting = true;
        Vector3 location;
        if (distantTeleport)
        {
            location = pos + new Vector3(0f, 2f, 0f);
        }
        else
        {
            Vector3 random = Random.insideUnitSphere * 10f;
            location = pos + new Vector3(random.x, 0f, random.z);
            location.y = ZoneSystem.instance.GetSolidHeight(location) + 0.5f;
        }

        var transform1 = transform;
        transform1.position = location;
        transform1.rotation = rot;
        m_body.velocity = Vector3.zero;
        m_teleporting = false;
    }

    public void UpdatePins(float dt)
    {
        if (m_pin == null) return;
        m_pinTimer += dt;
        if (m_pinTimer < 1f) return;
        m_pinTimer = 0.0f;

        m_pin.m_pos = transform.position;
    }

    public bool IsRaider() => m_nview.IsValid() && m_nview.GetZDO().GetBool(m_raider);
    public void SetRaider(bool enable)
    {
        m_nview.GetZDO().Set(m_raider, enable);
        if (enable) m_defeatSetGlobalKey = "defeated_vikingraider";
    }

    public bool IsSailor() => m_nview.IsValid() && m_nview.GetZDO().GetBool(m_sailor);

    public void SetSailor(bool enable)
    {
        m_nview.GetZDO().Set(m_sailor, enable);
        if (enable) m_defeatSetGlobalKey = "defeated_vikingsailor";
    }

    public bool IsElf() => m_nview.IsValid() && m_nview.GetZDO().GetBool(m_elf);

    public void SetElf(bool enable)
    {
        m_nview.GetZDO().Set(m_elf, enable);
        if (enable) m_defeatSetGlobalKey = "defeated_vikingelf";
    }
    private void GetLoadOut(bool isElf, bool isSailor)
    {
        m_currentBiome = Heightmap.FindBiome(transform.position);
        GameObject[]? raiderItems = isElf ? ElfLoadOut.GetElfEquipment(m_currentBiome) : RaiderLoadOut.GetRaiderEquipment(m_currentBiome, isSailor);
        if (raiderItems != null)
        {
            m_defaultItems = raiderItems;
        }
    }
    private void UpdateEncumbered()
    {
        if (IsEncumbered()) m_seman.AddStatusEffect(SEMan.s_statusEffectEncumbered);
        else m_seman.RemoveStatusEffect(SEMan.s_statusEffectEncumbered);
    }
    private void GetFollowTargetName()
    {
        if (m_companionAI == null) return;
        if (m_companionAI.GetFollowTarget() == null) return;
        if (!m_companionAI.GetFollowTarget().TryGetComponent(out Player player)) return;
        m_followTargetName = player.GetHoverName();
    }
    public Heightmap.Biome GetCurrentBiome()
    {
        m_currentBiome = Heightmap.FindBiome(transform.position);
        return m_currentBiome;
    }
    public void UpdateWarp(float dt)
    {
        if (IsEncumbered()) return;
        m_checkDistanceTimer += dt;
        if (m_checkDistanceTimer < 5f) return;
        m_checkDistanceTimer = 0.0f;
        if (m_companionAI.GetFollowTarget() == null) return;
        if (!m_companionAI.GetFollowTarget().TryGetComponent(out Player component)) return;
        if (Vector3.Distance(transform.position, component.transform.position) < m_playerMaxDistance) return;
        Warp(component);
    }

    public void Warp(Player player)
    {
        if (m_companionAI == null) return;
        if (player.IsTeleporting()) return;
        Vector3 location = player.GetLookDir() * 5f + player.transform.position;
        if (!m_nview.IsOwner())
        {
            m_nview.InvokeRPC(nameof(RPC_Warp), player.transform.position, location);
        }
        else
        {
            ZoneSystem.instance.GetSolidHeight(location, out float height, 1000);
            if (height >= 0.0 && Mathf.Abs(height - location.y) <= 2f && Vector3.Distance(location, player.transform.position) >= 2f)
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
    }
    public void RPC_Warp(long sender, Vector3 playerPos, Vector3 pos)
    {
        if (m_teleporting) return;
        ZoneSystem.instance.GetSolidHeight(pos, out float height, 1000);
        if (height >= 0.0 && Mathf.Abs(height - pos.y) <= 2f && Vector3.Distance(pos, playerPos) >= 2f)
        {
            pos.y = height;
        }

        if (!IsOwner()) m_nview.ClaimOwnership();
        if (m_companionAI != null) m_companionAI.StopMoving();
        Transform transform1 = transform;
        transform1.position = pos;
        transform1.rotation = Quaternion.identity;
        m_body.velocity = Vector3.zero;
        m_warpEffect.Create(transform1.position, Quaternion.identity);
    }
    public bool IsInventoryFull() => GetInventory().NrOfItems() >= m_inventory.m_width * m_inventory.m_height;
    public void AutoPickup(float dt)
    {
        if (SettlersPlugin._autoPickup.Value is SettlersPlugin.Toggle.Off) return;
        if (IsDead() || IsInventoryFull() || IsEncumbered() || m_companionTalk.InPlayerBase()) return;
        Vector3 vector3_1 = transform.position + Vector3.up;
        foreach (Collider collider in Physics.OverlapSphere(vector3_1, m_autoPickupRange, m_autoPickupMask))
        {
            if (!collider.attachedRigidbody) continue;
            collider.attachedRigidbody.TryGetComponent(out ItemDrop component);
            collider.attachedRigidbody.gameObject.TryGetComponent(out FloatingTerrainDummy floatingTerrainDummy);
            if (component == null && floatingTerrainDummy != null)
            {
                if (floatingTerrainDummy.m_parent.gameObject.TryGetComponent(out ItemDrop floatingItemDrop))
                {
                    component = floatingItemDrop;
                }
            }
            if (component == null || !component.m_autoPickup || !component.m_nview.IsValid()) continue;
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
        Transform transform1 = transform;
        m_killedEffects.Create(transform1.position, transform1.rotation, transform1);
        bool isRaider = IsRaider();
        bool isElf = IsElf();
        if (isRaider || (isElf && !IsTamed()))
        {
            ZoneSystem.instance.SetGlobalKey(IsRaider() ? "defeated_vikingraider" : IsElf() ? "defeated_vikingelf" : "defeated_viking");
            DropDefaultItems();
            if (TryGetComponent(out CharacterDrop characterDrop))
            {
                characterDrop.m_drops.Clear();
                characterDrop.m_drops = RaiderDrops.GetRaiderDrops(m_currentBiome);
            }
        }
        else if (IsTamed()) CreateTombStone();
        RemovePins();
        base.OnDeath();
    }

    private void DropDefaultItems()
    {
        if (SettlersPlugin._raiderDropChance.Value == 0f) return;
        foreach (GameObject item in m_defaultItems)
        {
            if (item.name == "ElvenEars") continue;
            if (!item.TryGetComponent(out ItemDrop itemDrop)) continue;
            if (Random.value > SettlersPlugin._raiderDropChance.Value) continue;
            ItemDrop.ItemData data = itemDrop.m_itemData.Clone();
            if (data.m_dropPrefab == null) continue;
            data.m_quality = m_level;
            ItemDrop drop = ItemDrop.DropItem(data, 1, transform.position, Quaternion.identity);
            m_dropEffects.Create(drop.transform.position, Quaternion.identity);
        }
    }
    
    public void RemovePins()
    {
        if (m_pin == null || !Minimap.instance) return;
        Minimap.instance.RemovePin(m_pin);
    }

    public override bool HaveEitr(float amount = 0)
    {
        if (IsRaider() || IsElf() || IsSailor()) return true;
        return amount < m_eitr;
    }

    public override bool StartAttack(Character? target, bool secondaryAttack)
    {
        if (InAttack() && !HaveQueuedChain() || InDodge() || !CanMove() || IsKnockedBack() || IsStaggering() ||
            InMinorAction()) return false;
    
        ItemDrop.ItemData currentWeapon = GetCurrentWeapon();
        if (currentWeapon == null || (!currentWeapon.HaveSecondaryAttack() && !currentWeapon.HavePrimaryAttack())) return false;

        bool secondary = currentWeapon.HaveSecondaryAttack() && Random.value > 0.5;
        if (currentWeapon.m_shared.m_skillType is Skills.SkillType.Spears) secondary = false;
        
        if (m_currentAttack != null)
        {
            m_currentAttack.Stop();
            m_previousAttack = m_currentAttack;
            m_currentAttack = null;
        }
        Attack? attack = !secondary ? currentWeapon.m_shared.m_attack.Clone() : currentWeapon.m_shared.m_secondaryAttack.Clone();
        if (!attack.Start(this, m_body, m_zanim, m_animEvent, m_visEquipment, currentWeapon, m_previousAttack,
                m_timeSinceLastAttack, Random.Range(0.5f, 1f))) return false;

        if (currentWeapon.m_shared.m_attack.m_requiresReload) SetWeaponLoaded(null);
        if (currentWeapon.m_shared.m_attack.m_bowDraw) currentWeapon.m_shared.m_attack.m_attackDrawPercentage = 0.0f;
        if (currentWeapon.m_shared.m_itemType is not ItemDrop.ItemData.ItemType.Torch) currentWeapon.m_durability -= 1.5f;
        
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
            MinorActionData? action = m_actionQueue[0];
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

    public override bool ToggleEquipped(ItemDrop.ItemData item)
    {
        if (!item.IsEquipable()) return false;
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
            if (action.m_item != item) continue;
            m_actionQueue.Remove(action);
            break;
        }
    }

    private bool IsEquipActionQueued(ItemDrop.ItemData? item)
    {
        return item != null && m_actionQueue.Any(action => action.m_type is MinorActionData.ActionType.Equip or MinorActionData.ActionType.Unequip && action.m_item == item);
    }

    private bool IsReloadActionQueued() => m_actionQueue.Any(action => action.m_type is MinorActionData.ActionType.Reload);
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

    private bool ShouldCreateTombStone() => m_nview.GetZDO().GetBool("InventoryChanged".GetStableHashCode());
    
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
        if (TryGetComponent(out Randomizer randomHuman))
        {
            visEq.SetHairColor(randomHuman.m_hairColor);
            if (IsElf()) visEq.SetSkinColor(randomHuman.m_skinColor);
        }
    }

    public void SaveInventory()
    {
        if (m_loading) return;
        ZPackage pkg = new ZPackage();
        m_inventory.Save(pkg);
        string? data = pkg.GetBase64();
        m_nview.GetZDO().Set(ZDOVars.s_items, data);
        // m_lastRevision = m_nview.GetZDO().DataRevision;
        // m_lastDataString = data;
        m_nview.GetZDO().Set("InventoryChanged".GetStableHashCode(), true);
        m_inventoryChanged = true;
    }

    private void LoadInventory()
    {
        // if (m_nview.GetZDO().DataRevision == m_lastRevision) return;
        string? data = m_nview.GetZDO().GetString(ZDOVars.s_items);
        if (data.IsNullOrWhiteSpace()) return;
        // if (data.IsNullOrWhiteSpace() || m_lastDataString == data) return;
        ZPackage pkg = new ZPackage(data);
        m_loading = true;
        m_inventory.Load(pkg);
        m_loading = false;
        // m_lastRevision = m_nview.GetZDO().DataRevision;
        // m_lastDataString = data;
        UpdateEquipment(false);
    }

    private void CheckEquipment(ItemDrop.ItemData? item, List<ItemDrop.ItemData> inventory)
    {
        if (item == null) return;
        if (inventory.Any(x => x.m_shared.m_name == item.m_shared.m_name)) return;
        UnequipItem(item);
    }

    public void UpdateEquipment(bool toggle = true)
    {
        if (m_nview.IsOwner()) RPC_UpdateEquipment(0L, toggle);
        else m_nview.InvokeRPC(nameof(RPC_UpdateEquipment), toggle);
    }

    private void RPC_UpdateEquipment(long sender, bool toggle = true)
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
        CheckEquipment(m_hiddenLeftItem, items);
        CheckEquipment(m_hiddenRightItem, items);
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
        RemovePins();
        m_instances.Remove(this);
        base.OnDestroy();
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
        UpdateModifiers();
        UpdateEncumbered();
        UpdateEitrRegen();
    }
    public override float GetRunSpeedFactor() => 1f + GetEquipmentMovementModifier();
    public override float GetJogSpeedFactor() => 1f + GetEquipmentMovementModifier();
    private float GetEquipmentModifier(int index) => m_equipmentModifierValues != null ? m_equipmentModifierValues[index] : 0.0f;
    public override float GetEquipmentMovementModifier() => GetEquipmentModifier(0);
    public override float GetEquipmentHomeItemModifier() => GetEquipmentModifier(1);
    public override float GetEquipmentHeatResistanceModifier() => GetEquipmentModifier(2);
    public override float GetEquipmentJumpStaminaModifier() => GetEquipmentModifier(3);
    public override float GetEquipmentAttackStaminaModifier() => GetEquipmentModifier(4);
    public override float GetEquipmentBlockStaminaModifier() => GetEquipmentModifier(5);
    public override float GetEquipmentDodgeStaminaModifier() => GetEquipmentModifier(6);
    public override float GetEquipmentSwimStaminaModifier() => GetEquipmentModifier(7);
    public override float GetEquipmentSneakStaminaModifier() => GetEquipmentModifier(8);
    public override float GetEquipmentRunStaminaModifier() => GetEquipmentModifier(9);

    private void UpdateModifiers()
    {
        if (Player.s_equipmentModifierSourceFields == null) return;
        if (m_equipmentModifierValues == null) return; 
        for (int index = 0; index < m_equipmentModifierValues.Length; ++index)
        {
            float num = 0.0f;
            if (m_rightItem != null)
            {
                num += (float)Player.s_equipmentModifierSourceFields[index].GetValue(m_rightItem.m_shared);
            }

            if (m_leftItem != null)
            {
                num += (float)Player.s_equipmentModifierSourceFields[index].GetValue(m_leftItem.m_shared);
            }

            if (m_chestItem != null)
            {
                num += (float)Player.s_equipmentModifierSourceFields[index].GetValue(m_chestItem.m_shared);
            }

            if (m_legItem != null)
            {
                num += (float)Player.s_equipmentModifierSourceFields[index].GetValue(m_legItem.m_shared);
            }

            if (m_helmetItem != null)
            {
                num += (float)Player.s_equipmentModifierSourceFields[index].GetValue(m_helmetItem.m_shared);
            }

            if (m_shoulderItem != null)
            {
                num += (float)Player.s_equipmentModifierSourceFields[index].GetValue(m_shoulderItem.m_shared);
            }

            if (m_utilityItem != null)
            {
                num += (float)Player.s_equipmentModifierSourceFields[index].GetValue(m_utilityItem.m_shared);
            }

            m_equipmentModifierValues[index] = num;
        }
    }

    private void UpdateEitrRegen()
    {
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
            
            if (EatFood(item))
            {
                consumedItem = item;
                break;
            }
        }

        if (consumedItem == null) return;
        tameableCompanion.OnConsumedItemData(consumedItem);
        GetInventory().RemoveOneItem(consumedItem);
    }

    private void GetTotalFoodValue(out float hp, out float stamina, out float eitr)
    {
        hp = SettlersPlugin._SettlerBaseHealth.Value * m_level * GetSkillModifier();
        stamina = 50f;
        eitr = 0.0f;
        foreach (Player.Food? food in m_foods)
        {
            hp += food.m_health;
            stamina += food.m_stamina;
            eitr += food.m_eitr;
        }
    }

    public bool EatFood(ItemDrop.ItemData item)
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

    public bool CanEat(ItemDrop.ItemData item)
    {
        if (item.m_shared.m_consumeStatusEffect is { } statusEffect)
        {
            if (GetSEMan().HaveStatusEffect(statusEffect.m_nameHash)) return false;
        }
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

    public void SetOwner(Player player)
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
            if (companion.tameableCompanion == null) continue;
            companion.tameableCompanion.Tame();
        }
    }

    public static void MakeAllFollow(Player player, float radius, bool follow = true)
    {
        int count = 0;
        if (follow)
        {
            foreach (Companion companion in m_instances)
            {
                if (!companion.IsTamed()) continue;
                if (companion.m_companionAI is not { } companionAI) continue;
                // if (companion.m_companionAI == null) continue;
                if (companionAI.GetFollowTarget() is null) continue;
                if (Vector3.Distance(player.transform.position, companion.transform.position) > radius) continue;
                companion.tameableCompanion.Command(player, false);
                ++count;
            }
        }
        else
        {
            foreach (Companion companion in m_instances)
            {
                if (!companion.IsTamed()) continue;
                if (companion.m_companionAI is not { } companionAI) continue;
                // if (companion.m_companionAI == null) continue;
                if (companionAI.GetFollowTarget() is null) continue;
                if (companion.m_followTargetName != player.GetPlayerName()) continue;
                companion.tameableCompanion.Command(player, false);
                ++count;
            }
        }
        player.Message(MessageHud.MessageType.Center, count + " $msg_settlerfollows");
    }
    
    public string GetOwnerName()
    {
        long ownerID = m_nview.GetZDO().GetLong(m_ownerKey);
        return ownerID == 0L ? "" : m_nview.GetZDO().GetString(m_ownerNameKey);
    }

    private float GetEitr() => m_eitr;
    public override float GetMaxEitr() => !IsTamed() ? 9999f : m_maxEitr;
    
    public override void UseEitr(float eitr)
    {
        if (IsRaider() || IsElf()) return;
        if (eitr == 0) return;
        m_eitr -= eitr;
        if (m_eitr < 0) m_eitr = 0;
    }

    public override string GetHoverText()
    {
        if (!m_nview.IsValid()) return "";
        if (IsRaider() || IsSailor()) return "";
        if (IsElf() && SettlersPlugin._elfTamable.Value is SettlersPlugin.Toggle.Off) return "";
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(m_name);
        if (IsTamed())
        {
            stringBuilder.AppendFormat(" ( {0} )", tameableCompanion.GetStatus());
            stringBuilder.AppendFormat("\n[<color=yellow><b>{0}</b></color>] {1}", 
                "$KEY_Use", 
                m_companionAI != null && m_companionAI.GetFollowTarget() == null ? "$hud_follow" : "$hud_stay");
            stringBuilder.Append("\n[<color=yellow><b>L.Shift + $KEY_Use</b></color>] $hud_interact");
            if (SettlersPlugin._ownerLock.Value is SettlersPlugin.Toggle.On)
            {
                string ownerName = GetOwnerName();
                if (!string.IsNullOrWhiteSpace(ownerName)) stringBuilder.AppendFormat("\n$hud_owner: {0}", ownerName);
            }

            stringBuilder.Append("\n[<color=yellow><b>L.Alt + $KEY_Use</b></color>] $hud_rename");
            stringBuilder.Append("\n[<color=yellow>1-8</color>] $hud_give");
            stringBuilder.AppendFormat("\n$se_health: {0}/{1}", (int)GetHealth(), (int)GetMaxHealth());
            stringBuilder.AppendFormat("\n$item_armor: {0}", (int)GetBodyArmor());
            if (GetMaxEitr() > 0) stringBuilder.AppendFormat("\n$se_eitr: <color=#E6E6FA>{0}</color>/<color=#B19CD9>{1}</color>", (int)GetEitr(), (int)GetMaxEitr());
        }
        else
        {
            int tameness = tameableCompanion.GetTameness();

            stringBuilder.AppendFormat("\n({0}, {1})", 
                tameness <= 0 ? "$hud_wild" : "$hud_tameness",
                tameableCompanion.GetStatus() + (tameness <= 0 ? "" : "%"));

            stringBuilder.Append("\n[<color=yellow>1-8</color>] $hud_give");
        }

        return Localization.instance.Localize(stringBuilder.ToString());
    }
    
    public override string GetHoverName() => m_name;

    public static Companion? GetNearestCompanion()
    {
        if (!Player.m_localPlayer) return null;
        var num = 9999f;
        Companion? companion = null;
        foreach (var instance in m_instances)
        {
            var distance = Vector3.Distance(Player.m_localPlayer.transform.position, instance.transform.position);
            if (companion == null || distance < num)
            {
                companion = instance;
                num = distance;
            }
        }

        return companion;
    }

    public void AddPin()
    {
        if (SettlersPlugin._addMinimapPin.Value is SettlersPlugin.Toggle.Off) return;
        var pin = new Minimap.PinData()
        {
            m_type = Minimap.PinType.Player,
            m_name = GetHoverName(),
            m_pos = transform.position,
            m_icon = SettlersPlugin.m_settlerPin,
            m_save = false,
            m_checked = false,
            m_ownerID = Player.m_localPlayer.GetPlayerID(),
            m_author = "SettlerPlugin"
        };
        pin.m_NamePinData = new Minimap.PinNameData(pin);
        Minimap.instance.m_pins.Add(pin);
        if (pin.m_type < (Minimap.PinType)Minimap.instance.m_visibleIconTypes.Length && !Minimap.instance.m_visibleIconTypes[(int)pin.m_type])
        {
            Minimap.instance.ToggleIconFilter(pin.m_type);
        }

        Minimap.instance.m_pinUpdateRequired = true;
        m_pin = pin;
    }

    public override bool IsEncumbered() => GetInventory().GetTotalWeight() > GetMaxCarryWeight();

    public float GetMaxCarryWeight()
    {
        float max = SettlersPlugin._baseMaxCarryWeight.Value;
        m_seman.ModifyMaxCarryWeight(max, ref max);
        return max;
    }

    public float GetWeight() => GetInventory().GetTotalWeight();
    public override void RaiseSkill(Skills.SkillType skill, float value = 1)
    {
        if (m_companionAI == null) return;
        if (m_companionAI.GetFollowTarget() is not { } followTarget) return;
        if (!followTarget.TryGetComponent(out Character component)) return;
        Skills skills = component.GetSkills();
        if (skills == null) return;
        skills.RaiseSkill(Skill.fromName("Companion"), value * 0.5f);
    }
    
    public override void AttachStart(Transform attachPoint, GameObject? colliderRoot, bool hideWeapons, bool isBed, bool onShip,
        string attachAnimation, Vector3 detachOffset, Transform? cameraPos = null)
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

            if (m_rightItem == null)
            {
                foreach (var item in GetInventory().GetAllItems())
                {
                    if (item.m_shared.m_itemType is not ItemDrop.ItemData.ItemType.Torch) continue;
                    EquipItem(item);
                    break;
                }
            }
            
            SetupVisEquipment(m_visEquipment, false);
        }
        UpdateAttach();
        ResetCloth();

        if (m_attachedToShip)
        {
            m_companionTalk.QueueSay(new List<string>()
            {
                "$npc_attach_ship_say_1", "$npc_attach_ship_say_2", "$npc_attach_ship_say_3", "$npc_attach_ship_say_4", "$npc_attach_ship_say_5",
                "$npc_attach_ship_say_6", "$npc_attach_ship_say_7", "$npc_attach_ship_say_8", "$npc_attach_ship_say_9", "$npc_attach_ship_say_10"
            }, "", null);
        }
    }

    // public override bool IsDead()
    // {
    //          Careful this made them never die!!!!
    //     return m_nview.IsValid() && GetHealth() <= 0;
    // }

    private bool UpdateAttach()
    {
        if (!m_attached)
        {
            if (IsSailor()) OnDeath();
            return false;
        }
        
        if (m_attachPoint == null)
        {
            AttachStop();
            return false;
        }

        var transform1 = transform;
        transform1.position = m_attachPoint.position;
        transform1.rotation = m_attachPoint.rotation;
        Rigidbody component = m_attachPoint.GetComponentInParent<Rigidbody>();
        m_body.useGravity = false;
        m_body.velocity = component ? component.GetPointVelocity(transform.position) : Vector3.zero;
        m_body.angularVelocity = Vector3.zero;
        m_maxAirAltitude = transform.position.y;
        return true;
    }

    public override bool IsAttached() => m_attached || base.IsAttached();
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
                if (collider) Physics.IgnoreCollision(m_collider, collider, false);
            }
            m_attachColliders = null;
        }

        m_body.useGravity = true;
        m_attached = false;
        m_attachPoint = null;
        m_animator.SetBool(m_attachAnimation, false);
        if (m_attachedChair != null)
        {
            if (CompanionAI.m_occupiedChairs.ContainsKey(m_attachedChair))
            {
                CompanionAI.m_occupiedChairs.Remove(m_attachedChair);
            }
            m_attachedChair = null;
        }

        if (m_attachedSadle != null)
        {
            if (CompanionAI.m_occupiedSaddles.ContainsKey(m_attachedSadle))
            {
                m_attachedSadle.m_monsterAI.SetFollowTarget(null);
                m_attachedSadle.m_monsterAI.SetPatrolPoint();
                if (m_attachedSadle.m_monsterAI.m_nview.IsOwner())
                {
                    m_attachedSadle.m_monsterAI.m_nview.GetZDO().Set(ZDOVars.s_follow, "");
                }
                CompanionAI.m_occupiedSaddles.Remove(m_attachedSadle);
            }
        
            m_attachedSadle = null;
        }
        ResetCloth();
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
            if (allItems.Count == 0 || component.InAttack()) return true;
            ModifyRangedItems(allItems, component.IsSailor());
            return true;
        }

        private static void ModifyRangedItems(List<ItemDrop.ItemData> allItems, bool isSailor)
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
                        item.m_shared.m_attack.m_useCharacterFacingYAim = !isSailor;
                        item.m_shared.m_attack.m_launchAngle = 0f;
                        item.m_shared.m_attack.m_projectiles = 1;
                        item.m_shared.m_aiAttackRange = isSailor ? 60f : 30f;
                        item.m_shared.m_aiAttackRangeMin = 5f;
                        item.m_shared.m_aiAttackInterval = 12f;
                        item.m_shared.m_aiAttackMaxAngle = 15f;
                        item.m_shared.m_aiWhenFlying = true;
                        item.m_shared.m_aiWhenWalking = true;
                        item.m_shared.m_aiWhenSwiming = true;
                        break;
                    case Skills.SkillType.ElementalMagic:
                        item.m_shared.m_aiAttackRange = 20f;
                        item.m_shared.m_aiAttackRangeMin = 5f;
                        item.m_shared.m_aiAttackInterval = item.m_shared.m_name == "$item_stafficeshards" ? 30f : 12f;
                        item.m_shared.m_aiAttackMaxAngle = 15f;
                        item.m_shared.m_aiWhenFlying = true;
                        item.m_shared.m_aiWhenWalking = true;
                        item.m_shared.m_aiWhenSwiming = true;
                        break;
                    case Skills.SkillType.BloodMagic:
                        item.m_shared.m_aiAttackRange = 20f;
                        item.m_shared.m_aiAttackRangeMin = 5f;
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
                        item.m_shared.m_attack.m_useCharacterFacingYAim = !isSailor;
                        item.m_shared.m_attack.m_launchAngle = 0f;
                        item.m_shared.m_attack.m_projectiles = 1;
                        item.m_shared.m_aiAttackRange = isSailor ? 60f : 40f;
                        item.m_shared.m_aiAttackRangeMin = 5f;
                        item.m_shared.m_aiAttackInterval = 12f;
                        item.m_shared.m_aiAttackMaxAngle = 15f;
                        item.m_shared.m_aiWhenFlying = true;
                        item.m_shared.m_aiWhenWalking = true;
                        item.m_shared.m_aiWhenSwiming = true;
                        item.m_shared.m_attack.m_attackProjectile = ZNetScene.instance.GetPrefab("DvergerArbalest_projectile");
                        break;
                    default:
                        if (item.m_shared.m_attack.m_attackType is Attack.AttackType.Projectile)
                        {
                            item.m_shared.m_attack.m_projectileVel = 30f;
                            item.m_shared.m_attack.m_projectileVelMin = 2f;
                            item.m_shared.m_attack.m_projectileAccuracy = 2f;
                            item.m_shared.m_attack.m_projectileAccuracyMin = 20f;
                            item.m_shared.m_attack.m_useCharacterFacingYAim = !isSailor;
                            item.m_shared.m_attack.m_launchAngle = 0f;
                            item.m_shared.m_attack.m_projectiles = 1;
                            item.m_shared.m_aiAttackRange = isSailor ? 60f : 30f;
                            item.m_shared.m_aiAttackRangeMin = 5f;
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
        private static void Postfix(Player __instance, Vector3 pos, Quaternion rot, bool distantTeleport)
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
                companion.TeleportTo(pos, rot, distantTeleport);
            }
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.HaveAmmo))]
    private static class Attack_HaveAmmo_Patch
    {
        private static void Postfix(Humanoid character, ref bool __result)
        {
            if (character is not Companion companion) return;
            if (companion.IsRaider() || companion.IsSailor() || companion.IsElf())
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.EquipAmmoItem))]
    private static class Attack_EquipAmmoItem_Patch
    {
        private static void Prefix(Humanoid character, ItemDrop.ItemData weapon, ref bool __result)
        {
            if (character is not Companion companion) return;
            if (companion.IsRaider() || companion.IsSailor() || companion.IsElf())
            {
                switch (weapon.m_shared.m_ammoType)
                {
                    case "$ammo_arrows":
                        var arrow = ObjectDB.instance.GetItemPrefab("ArrowWood");
                        if (arrow.TryGetComponent(out ItemDrop arrowComponent))
                        {
                            ItemDrop.ItemData? cloneArrow = arrowComponent.m_itemData.Clone();
                            character.GetInventory().AddItem(cloneArrow);
                        }

                        break;
                    case "$ammo_bolts":
                        var bolt = ObjectDB.instance.GetItemPrefab("BoltBone");
                        if (bolt.TryGetComponent(out ItemDrop boltComponent))
                        {
                            ItemDrop.ItemData? cloneArrow = boltComponent.m_itemData.Clone();
                            character.GetInventory().AddItem(cloneArrow);
                        }

                        break;
                }

                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ActivateGuardianPower))]
    private static class Player_ActivateGuardianPower_Patch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance.m_guardianSE == null) return;
            foreach (Companion companion in m_instances)
            {
                if (!companion.IsTamed()) continue;
                if (Vector3.Distance(__instance.transform.position, companion.transform.position) > 10f) continue;
                companion.GetSEMan().AddStatusEffect(__instance.m_guardianSE.NameHash(), true);
            }
        }
    }

    public class MinorActionData
    {
        public ActionType m_type;
        public ItemDrop.ItemData? m_item;
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
            Reload,
        }
    }
}
