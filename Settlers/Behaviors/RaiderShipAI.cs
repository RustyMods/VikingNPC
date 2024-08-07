// using System;
// using System.Collections.Generic;
// using UnityEngine;
// using Random = UnityEngine.Random;
//
// namespace Settlers.Behaviors;
//
// public class RaiderShipAI : MonoBehaviour, IUpdateAI
// {
//     [Header("Objects")]
//     public GameObject m_sailObject = null!;
//     public GameObject m_mastObject = null!;
//     public GameObject m_rudderObject = null!;
//     [Header("Misc")]
//     public BoxCollider m_floatCollider = null!;
//     public EffectList m_waterImpactEffect = new EffectList();
//     public readonly List<Companion> m_raiders = new List<Companion>();
//     public WaterVolume m_previousCenter;
//     public WaterVolume m_previousLeft;
//     public WaterVolume m_previousRight;
//     public WaterVolume m_previousForward;
//     public WaterVolume m_previousBack;
//     public GlobalWind m_globalWind = null!;
//     public Rigidbody m_body = null!;
//     public ZNetView m_nview = null!;
//     public Cloth m_sailCloth = null!;
//     [Header("BaseAI")] 
//     public float m_fleeTargetUpdateTime;
//     public Vector3 m_fleeTarget = Vector3.zero;
//     public float aroundPointUpdateTime;
//     public Vector3 aroundPointTarget = Vector3.zero;
//     public Vector3 m_lastMovementCheck;
//     public float m_lastMoveTime;
//     public Action<BaseAI.AggravatedReason>? m_onBecameAggravated;
//     public float m_viewRange = 50f;
//     public float m_viewAngle = 90f;
//     public float m_hearRange = 9999f;
//     public float m_despawnDistance = 80f;
//     public float m_regenAllHPTime = 3600f;
//     public Pathfinding.AgentType m_pathAgentType = Pathfinding.AgentType.HumanoidBig;
//     public float m_moveMinAngle = 10f;
//     public bool m_smoothMovement = false;
//     public float m_turnRadius = 20f;
//     public float m_randomCircleInterval = 10f;
//     public float m_randomMoveInterval = 10f;
//     public float m_randomMoveRange = 10f;
//     public bool m_aggravatable;
//     public bool m_passiveAggressive;
//     public string m_spawnMessage = "";
//     public string m_deathMessage = "";
//     public string m_alertedMessage = "";
//     public float m_fleeRange = 25f;
//     public float m_fleeAngle = 45f;
//     public float m_fleeInterval = 2f;
//     public bool m_patrol = true;
//     public Vector3 m_patrolPoint = Vector3.zero;
//     public float m_patrolPointUpdateTime;
//     public Vector3 m_randomMoveTarget = Vector3.zero;
//     public float m_randomMoveUpdateTimer;
//     public bool m_reachedRandomMoveTarget = true;
//     public bool m_aggravated;
//     public bool m_huntPlayer = true;
//     public float m_lastAggravatedCheck;
//     public Vector3 m_spawnPoint = Vector3.zero;
//     public Vector3 m_lastPosition = Vector3.zero;
//     public float m_stuckTimer;
//     public float m_timeSinceHurt = 99999f;
//     public float m_lastFlee;
//     public Vector3 m_lastFindPathTarget = new Vector3(-999999f, -999999f, -999999f);
//     public float m_lastFindPathTime;
//     public bool m_lastFindPathResult;
//     public bool m_alerted;
//     public float m_regenTimer;
//     public readonly List<Vector3> m_path = new List<Vector3>();
//     public RaiderShip m_ship = null!;
//     public Heightmap.Biome m_currentBiome;
//     public float m_updateBiomeTimer;
//
//     public static List<IUpdateAI> Instances = new();
//     public static List<RaiderShipAI> m_instances = new();
//     public static List<RaiderShipAI> RaiderShipAIInstances = new();
//     public void Awake()
//     {
//         m_nview = GetComponent<ZNetView>();
//         m_body = GetComponent<Rigidbody>();
//         m_ship = GetComponent<RaiderShip>();
//         m_sailObject = Utils.FindChild(transform, "Sail").gameObject;
//         m_mastObject = Utils.FindChild(transform, "Mast").gameObject;
//         m_rudderObject = Utils.FindChild(transform, "rudder").gameObject;
//
//         m_floatCollider = transform.Find("FloatCollider").GetComponent<BoxCollider>();
//
//         m_body.maxDepenetrationVelocity = 2f;
//         Heightmap.ForceGenerateAll();
//         m_sailCloth = m_sailObject.GetComponentInChildren<Cloth>();
//         m_globalWind = m_sailCloth.gameObject.GetComponent<GlobalWind>();
//
//         m_nview.Register(nameof(RPC_Alert), RPC_Alert);
//         m_nview.Register<bool, int>(nameof(RPC_SetAggravated), RPC_SetAggravated);
//         m_huntPlayer = m_nview.GetZDO().GetBool(ZDOVars.s_huntPlayer, m_huntPlayer);
//         m_spawnPoint = m_nview.GetZDO().GetVec3(ZDOVars.s_spawnPoint, transform.position);
//         if (m_nview.IsOwner())
//         {
//             m_nview.GetZDO().Set(ZDOVars.s_spawnPoint, m_spawnPoint);
//         }
//         m_instances.Add(this);
//     }
//
//     public void OnDestroy() => m_instances.Remove(this);
//
//     public void OnEnable()
//     {
//         Instances.Add(this);
//         RaiderShipAIInstances.Add(this);
//     }
//
//     public void OnDamaged(float damage, Character? attacker) => m_timeSinceHurt = 0.0f;
//
//     public void OnDisable()
//     {
//         Instances.Remove(this);
//         RaiderShipAIInstances.Remove(this);
//     }
//
//     public void SetPatrolPoint() => SetPatrolPoint(transform.position);
//
//     public void SetPatrolPoint(Vector3 point)
//     {
//         m_patrol = true;
//         m_patrolPoint = point;
//         m_nview.GetZDO().Set(ZDOVars.s_patrolPoint, point);
//         m_nview.GetZDO().Set(ZDOVars.s_patrol, true);
//     }
//
//     public void ResetPatrolPoint()
//     {
//         m_patrol = false;
//         m_nview.GetZDO().Set(ZDOVars.s_patrol, false);
//     }
//
//     public bool GetPatrolPoint(out Vector3 point)
//     {
//         if (Time.time - m_patrolPointUpdateTime > 1f)
//         {
//             m_patrolPointUpdateTime = Time.time;
//             m_patrol = m_nview.GetZDO().GetBool(ZDOVars.s_patrol);
//             if (m_patrol)
//             {
//                 m_patrolPoint = m_nview.GetZDO().GetVec3(ZDOVars.s_patrolPoint, m_patrolPoint);
//             }
//         }
//
//         point = m_patrolPoint;
//         return m_patrol;
//     }
//
//     public bool UpdateAI(float deltaTime)
//     {
//         if (!m_nview.IsValid()) return false;
//         if (!m_nview.IsOwner())
//         {
//             m_alerted = m_nview.GetZDO().GetBool(ZDOVars.s_alert);
//             return false;
//         }
//         UpdateRegeneration(deltaTime);
//         m_timeSinceHurt += deltaTime;
//         UpdateCurrentBiome(deltaTime);
//         
//         
//         return true;
//     }
//
//     public void UpdateRegeneration(float dt)
//     {
//         m_regenTimer += dt;
//         if (m_regenTimer < 2f) return;
//         m_regenTimer = 0.0f;
//         float worldTimeDelta = GetWorldTimeDelta();
//         m_ship.Heal(m_ship.GetMaxHealth() / 3600f * worldTimeDelta, true);
//     }
//
//     public void UpdateCurrentBiome(float dt)
//     {
//         m_updateBiomeTimer += dt;
//         if (m_updateBiomeTimer < 5.0) return;
//         m_updateBiomeTimer = 0.0f;
//         m_currentBiome = Heightmap.FindBiome(transform.position);
//     }
//
//     public bool MoveToWater(float dt, float maxRange)
//     {
//         
//
//
//         return true;
//     }
//
//     public TimeSpan GetTimeSinceSpawned()
//     {
//         if (!m_nview || !m_nview.IsValid()) return TimeSpan.Zero;
//         long ticks = m_nview.GetZDO().GetLong(ZDOVars.s_spawnTime);
//         if (ticks == 0L)
//         {
//             ticks = ZNet.instance.GetTime().Ticks;
//             m_nview.GetZDO().Set(ZDOVars.s_spawnTime, ticks);
//         }
//
//         DateTime dateTime = new DateTime(ticks);
//         return ZNet.instance.GetTime() - dateTime;
//     }
//
//     public void Follow(GameObject go, float dt)
//     {
//         double num = Vector3.Distance(go.transform.position, transform.position);
//         
//     }
//
//     public bool FindPath(Vector3 target)
//     {
//         float time = Time.time;
//         float num = time - m_lastFindPathTime;
//         if (num < 1.0 || Vector3.Distance(target, m_lastFindPathTarget) < 1.0 && num < 5.0) return m_lastFindPathResult;
//         m_lastFindPathTarget = target;
//         m_lastFindPathTime = time;
//         m_lastFindPathResult = Pathfinding.instance.GetPath(transform.position, target, m_path, m_pathAgentType);
//         return m_lastFindPathResult;
//     }
//
//     public bool FoundPath() => m_lastFindPathResult;
//
//     public bool MoveTo(float dt, Vector3 point, float dist)
//     {
//         float maxDist = 3f;
//         if (Utils.DistanceXZ(point, transform.position) < Mathf.Max(dist, maxDist))
//         {
//             StopMoving();
//             return true;
//         }
//
//         if (!FindPath(point))
//         {
//             StopMoving();
//             return true;
//         }
//
//         if (m_path.Count == 0)
//         {
//             StopMoving();
//             return true;
//         }
//
//         Vector3 vector3 = m_path[0];
//         var position = transform.position;
//         if (Utils.DistanceXZ(vector3, position) < maxDist)
//         {
//             m_path.RemoveAt(0);
//             if (m_path.Count == 0)
//             {
//                 StopMoving();
//                 return true;
//             }
//         }
//         else
//         {
//             float distance = Vector3.Distance(vector3, position);
//             MoveTowardsSwoop((vector3 - position).normalized, distance);
//         }
//
//         return false;
//     }
//
//     public bool MoveAndAvoid(float dt, Vector3 point, float dist)
//     {
//         Vector3 dir1 = point - transform.position;
//         dir1.y = 0.0f;
//         if (dir1.magnitude < dist)
//         {
//             StopMoving();
//             return true;
//         }
//         dir1.Normalize();
//         float radius = m_ship.GetRadius();
//         float distance = radius + 1f;
//         if (!m_ship.InAttack())
//         {
//             if (CanMove(dir1, radius, distance))
//             {
//                 MoveTowards(dir1);
//             }
//         }
//         else
//         {
//             Vector3 forward = transform.forward;
//             Vector3 vector3 = transform.right * radius * 0.75f;
//             float distance2 = distance * 1.5f;
//             Vector3 centerPoint = m_ship.GetCenterPoint();
//             float num1 = Raycast(centerPoint - vector3, forward, distance2, 0.1f);
//             float num2 = Raycast(centerPoint + vector3, forward, distance2, 0.1f);
//             if (num1 >= distance2 && num2 >= distance2)
//             {
//                 MoveTowards(forward);
//             }
//             else
//             {
//                 Vector3 dir2 = Quaternion.Euler(0.0f, -20f, 0.0f) * forward;
//                 Vector3 dir3 = Quaternion.Euler(0.0f, 20f, 0.0f) * forward;
//                 if (num1 > num2)
//                 {
//                     MoveTowards(dir2);
//                 }
//                 else
//                 {
//                     MoveTowards(dir3);
//                 }
//             }
//         }
//         return false;
//     }
//
//     public float Raycast(Vector3 p, Vector3 dir, float distance, float radius)
//     {
//         if (radius == 0.0)
//         {
//            return Physics.Raycast(p, dir, out RaycastHit hitInfo1, distance, BaseAI.m_solidRayMask) ? hitInfo1.distance : distance ;
//         }
//         return Physics.SphereCast(p, radius, dir, out RaycastHit hitInfo2, distance, BaseAI.m_solidRayMask)
//             ? hitInfo2.distance
//             : distance;
//     }
//
//     public void SetAggravated(bool aggro, BaseAI.AggravatedReason reason)
//     {
//         if (!m_aggravatable || !m_nview.IsValid() || m_aggravated == aggro) return;
//         m_nview.InvokeRPC(nameof(RPC_SetAggravated), aggro, (int)reason);
//     }
//
//     public void RPC_SetAggravated(long sender, bool aggro, int reason)
//     {
//         if (!m_nview.IsValid() || aggro == m_aggravated) return;
//         m_aggravated = aggro;
//         m_nview.GetZDO().Set(ZDOVars.s_aggravated, m_aggravated);
//         m_onBecameAggravated?.Invoke((BaseAI.AggravatedReason)reason);
//     }
//
//     public bool IsAggravatable() => m_aggravatable;
//
//     public bool IsAggravated()
//     {
//         if (!m_nview.IsValid() || !m_aggravatable) return false;
//         if (Time.time - m_lastAggravatedCheck > 1.0)
//         {
//             m_lastAggravatedCheck = Time.time;
//             m_aggravated = m_nview.GetZDO().GetBool(ZDOVars.s_aggravated, m_aggravated);
//         }
//
//         return m_aggravated;
//     }
//
//     public bool IsEnemy() => true;
//
//     public StaticTarget? FindRandomStaticTarget(float maxDistance)
//     {
//         int num = Physics.OverlapSphereNonAlloc(transform.position, m_ship.GetRadius() + maxDistance,
//             BaseAI.s_tempSphereOverlap);
//         if (num == 0) return null;
//         List<StaticTarget> staticTargetList = new List<StaticTarget>();
//         for (int index = 0; index < num; ++index)
//         {
//             StaticTarget componentInParent = BaseAI.s_tempSphereOverlap[index].GetComponentInParent<StaticTarget>();
//             if (componentInParent == null) continue;
//             if (componentInParent.IsRandomTarget() && CanSeeTarget(componentInParent))
//             {
//                 staticTargetList.Add(componentInParent);
//             }
//         }
//
//         return staticTargetList.Count == 0 ? null : staticTargetList[Random.Range(0, staticTargetList.Count)];
//     }
//
//     public StaticTarget? findClosestStaticPriorityTarget()
//     {
//         float radius = m_viewRange > 0.0 ? m_viewRange : m_hearRange;
//         int num = Physics.OverlapSphereNonAlloc(transform.position, radius, BaseAI.s_tempSphereOverlap);
//         if (num == 0) return null;
//         StaticTarget? staticTarget = null;
//         float num2 = radius;
//         for (int index = 0; index < num; ++index)
//         {
//             StaticTarget componentInParent = BaseAI.s_tempSphereOverlap[index].GetComponentInParent<StaticTarget>();
//             if (componentInParent == null) continue;
//             float num3 = Vector3.Distance(transform.position, componentInParent.GetCenter());
//             if (num3 < num2 && CanSeeTarget(componentInParent))
//             {
//                 staticTarget = componentInParent;
//                 num2 = num3;
//             }
//         }
//
//         return staticTarget;
//     }
//
//     public float StandStillDuration(float distanceThreshold)
//     {
//         if (Vector3.Distance(transform.position, m_lastMovementCheck) > distanceThreshold)
//         {
//             m_lastMovementCheck = transform.position;
//             m_lastMoveTime = Time.time;
//         }
//
//         return Time.time - m_lastMoveTime;
//     }
//
//     public static Character? FindClosestCreature(Transform me)
//     {
//         List<Character> characters = Character.GetAllCharacters();
//         Character? closestCharacter = null;
//         float num1 = 9999f;
//         foreach (var character in characters)
//         {
//             if (character.IsDead()) continue;
//             var baseAI = character.GetBaseAI();
//             if (baseAI == null) continue;
//             float num3 = Vector3.Distance(me.position, character.transform.position);
//             if (num3 < num1 || closestCharacter == null)
//             {
//                 closestCharacter = character;
//                 num1 = num3;
//             }
//         }
//
//         return closestCharacter;
//     }
//
//     public void SetHuntPlayer(bool hunt)
//     {
//         if (m_huntPlayer == hunt) return;
//         m_huntPlayer = hunt;
//         if (!m_nview.IsOwner()) return;
//         m_nview.GetZDO().Set(ZDOVars.s_huntPlayer, m_huntPlayer);
//     }
//
//     public bool HuntPlayer() => m_huntPlayer;
//
//     public bool HaveAlertedCreatureInRange(float range)
//     {
//         foreach (var instance in BaseAI.m_instances)
//         {
//             if (Vector3.Distance(transform.position, instance.transform.position) > range) continue;
//             if (instance.IsAlerted()) return true;
//         }
//
//         return false;
//     }
//
//     public void Alert()
//     {
//         if (!m_nview.IsValid() || IsAlerted()) return;
//         if (m_nview.IsOwner())
//         {
//             SetAlerted(true);
//         }
//         else
//         {
//             m_nview.InvokeRPC(nameof(RPC_Alert));
//         }
//     }
//
//     public void RPC_Alert(long sender)
//     {
//         if (!m_nview.IsOwner()) return;
//         SetAlerted(true);
//     }
//
//     public void SetAlerted(bool alerted)
//     {
//         if (m_alerted == alerted) return;
//         m_alerted = alerted;
//         if (m_nview.IsOwner())
//         {
//             m_nview.GetZDO().Set(ZDOVars.s_alert, alerted);
//         }
//     }
//
//     public bool IsAlerted() => m_alerted;
//
//     public static bool HaveEnemyInRange(RaiderShip me, Vector3 point, float range)
//     {
//         foreach (Character character in Character.GetAllCharacters())
//         {
//             if (Vector3.Distance(character.transform.position, me.transform.position) > range) continue;
//             return true;
//         }
//
//         return false;
//     }
//
//     public void SetTargetInfo(ZDOID targetID) => m_nview.GetZDO().Set(ZDOVars.s_haveTargetHash, !targetID.IsNone());
//
//     public bool HaveTarget() => m_nview.IsValid() && m_nview.GetZDO().GetBool(ZDOVars.s_haveTargetHash);
//
//     public float GetAltitude()
//     {
//         return Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo, BaseAI.m_solidRayMask)
//             ? m_ship.transform.position.y - hitInfo.point.y
//             : 1000f;
//     }
//
//     public static List<RaiderShipAI> GetAllInstances() => m_instances;
//     public void HaveFriendsInRange(float range, out RaiderShip? hurtFriend, out RaiderShip? friend)
//     {
//         List<RaiderShip> allShips = RaiderShip.m_instances;
//         friend = HaveFriendInRange(allShips, range);
//         hurtFriend = HaveHurtFriendInRange(allShips, range);
//     }
//
//     public RaiderShip? HaveFriendInRange(List<RaiderShip> raiderShips, float range)
//     {
//         foreach (var raiderShip in raiderShips)
//         {
//             if (raiderShip == m_ship ||
//                 Vector3.Distance(raiderShip.transform.position, transform.position) > range) continue;
//             return raiderShip;
//         }
//
//         return null;
//     }
//
//     public RaiderShip? HaveHurtFriendInRange(List<RaiderShip> raiderShips, float range)
//     {
//         foreach (var raiderShip in raiderShips)
//         {
//             if (raiderShip == m_ship || Vector3.Distance(raiderShip.transform.position, transform.position) > range) continue;
//             if (raiderShip.GetHealth() < raiderShip.GetMaxHealth()) return raiderShip;
//         }
//
//         return null;
//     }
//
//     public bool CanSeeTarget(Character character) => CanSeeTarget(transform, character.transform.position);
//
//     public static bool CanSeeTarget(Transform me, Vector3 target)
//     {
//         return true;
//     }
//     public bool CanSeeTarget(StaticTarget staticTarget)
//     {
//         return true;
//     }
//     public void MoveTowards(Vector3 dir)
//     {
//         dir = dir.normalized;
//         LookTowards(dir);
//         m_ship.SetMoveDir((transform.forward * (1f - Mathf.Clamp01(Vector3.Angle(new Vector3(dir.x, 0.0f, dir.z), transform.forward / m_moveMinAngle)))));
//     }
//
//     public bool CanMove(Vector3 dir, float radius, float dist)
//     {
//         return true;
//     }
//
//     public void MoveTowardsSwoop(Vector3 dir, float distance)
//     {
//         dir = dir.normalized;
//         float num1 = Mathf.Clamp01(Vector3.Dot(dir, m_ship.transform.forward));
//         float num2 = num1 * num1;
//         Vector3 dir1 = transform.forward * (float) ((1.0 - (1.0 - Mathf.Clamp01(distance / m_turnRadius)) * (1.0 - num2)) * 0.8999999761581421 + 0.10000000149011612);
//         LookTowards(dir1);
//         m_ship.SetMoveDir(dir);
//     }
//
//     public void LookTowards(Vector3 dir) => m_ship.SetLookDir(dir);
//     
//     public void StopMoving() => m_ship.SetMoveDir(Vector3.zero);
//     public float GetWorldTimeDelta()
//     {
//         DateTime time = ZNet.instance.GetTime();
//         long ticks = m_nview.GetZDO().GetLong(ZDOVars.s_worldTimeHash);
//         if (ticks == 0L)
//         {
//             m_nview.GetZDO().Set(ZDOVars.s_worldTimeHash, time.Ticks);
//             return 0.0f;
//         }
//         DateTime dateTime = new DateTime(ticks);
//         TimeSpan timeSpan = time - dateTime;
//         m_nview.GetZDO().Set(ZDOVars.s_worldTimeHash, time.Ticks);
//         return (float) timeSpan.TotalSeconds;
//     }
//
//     public bool HasZDOOwner() => m_nview.IsValid() && m_nview.GetZDO().HasOwner();
//
//     public bool HaveRider() => m_ship.HaveRider();
//
//     public static void AggravateAllInArea(Vector3 point, float radius, BaseAI.AggravatedReason reason)
//     {
//         foreach (var instance in GetAllInstances())
//         {
//             instance.SetAggravated(true, reason);
//             instance.Alert();
//         }
//     }
// }