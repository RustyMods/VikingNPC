// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
//
// namespace Settlers.Managers;
//
// public class FakeZDO
// {
//     public class ZDOData
//     {
//         public Dictionary<int, string> Strings = new();
//         public Dictionary<int, float> Floats = new();
//         public Dictionary<int, int> Ints = new();
//         public Dictionary<int, long> Longs = new();
//         public Dictionary<int, Vector3> Vectors = new();
//         public Dictionary<int, Quaternion> Rotations = new();
//         public Dictionary<int, byte[]> ByteArrays = new();
//         public int ConnectionHash = 0;
//         public ZDOExtraData.ConnectionType ConnectionType = ZDOExtraData.ConnectionType.None;
//         
//         public string GetString(int hash, string defaultValue = "") => Strings.TryGetValue(hash, out string value) ? value : defaultValue;
//         public string GetString(string key, string defaultValue = "") => GetString(key.GetStableHashCode(), defaultValue);
//         public float GetFloat(int hash, float defaultValue = 0) => Floats.TryGetValue(hash, out float value) ? value : defaultValue;
//         public int GetInt(int hash, int defaultValue = 0) => Ints.TryGetValue(hash, out int value) ? value : defaultValue;
//         public int GetInt(string key, int defaultValue = 0) => GetInt(key.GetStableHashCode(), defaultValue);
//         public long GetLong(int hash, long defaultValue = 0L) => Longs.TryGetValue(hash, out long value) ? value : defaultValue;
//         public Vector3 GetVector3(int hash, Vector3 defaultValue) => Vectors.TryGetValue(hash, out Vector3 value) ? value : defaultValue;
//         public Quaternion GetQuaternion(int hash, Quaternion defaultValue) => Rotations.TryGetValue(hash, out Quaternion value) ? value : defaultValue;
//         public byte[] GetByteArray(int hash, byte[] defaultValue) => ByteArrays.TryGetValue(hash, out byte[] value) ? value : defaultValue;
//         
//         public void Set(int hash, string value) => Strings[hash] = value;
//         public void Set(string key, string value) => Set(key.GetStableHashCode(), value);
//         public void Set(int hash, float value) => Floats[hash] = value;
//         public void Set(string key, float value) => Set(key.GetStableHashCode(), value);
//         public void Set(int hash, int value) => Ints[hash] = value;
//         public void Set(string key, int value) => Set(key.GetStableHashCode(), value);
//         public void Set(int hash, bool value) => Ints[hash] = value ? 1 : 0;
//         public void Set(int hash, long value) => Longs[hash] = value;
//         public void Set(string key, long value) => Set(key.GetStableHashCode(), value);
//         public void Set(int hash, Vector3 value) => Vectors[hash] = value;
//         public void Set(int hash, Quaternion value) => Rotations[hash] = value;
//         public void Set(int hash, byte[] value) => ByteArrays[hash] = value;
//
//         public void Set(KeyValuePair<int, int> hashPair, ZDOID id)
//         {
//             Set(hashPair.Key, id.UserID);
//             Set(hashPair.Value, (long)(ulong)id.ID);
//         }
//
//         public ZDOID GetZDOID(KeyValuePair<int, int> hashPair)
//         {
//             var value = GetLong(hashPair.Key, 0L);
//             var num = (uint)GetLong(hashPair.Value, 0L);
//             if (value == 0L || num == 0L) return ZDOID.None;
//             return new ZDOID(value, num);
//         }
//
//         public ZPackage Save()
//         {
//             ZPackage pkg = new();
//             Vectors.Remove(ZDOVars.s_scaleHash);
//             Vectors.Remove(ZDOVars.s_spawnPoint);
//             if (Strings.ContainsKey("override_items".GetStableHashCode()))
//             {
//                 Ints.Remove(ZDOVars.s_addedDefaultItems);
//                 Strings.Remove(ZDOVars.s_items);
//             }
//
//             var num = 0;
//             if (Floats.Count > 0)
//                 num |= 1;
//             if (Vectors.Count > 0)
//                 num |= 2;
//             if (Rotations.Count > 0)
//                 num |= 4;
//             if (Ints.Count > 0)
//                 num |= 8;
//             if (Strings.Count > 0)
//                 num |= 16;
//             if (Longs.Count > 0)
//                 num |= 64;
//             if (ByteArrays.Count > 0)
//                 num |= 128;
//             if (ConnectionType != ZDOExtraData.ConnectionType.None && ConnectionHash != 0)
//                 num |= 256;
//
//             pkg.Write(num);
//             if (Floats.Count > 0)
//             {
//                 pkg.Write((byte)Floats.Count);
//                 foreach (var kvp in Floats)
//                 {
//                     pkg.Write(kvp.Key);
//                     pkg.Write(kvp.Value);
//                 }
//             }
//
//             if (Vectors.Count > 0)
//             {
//                 pkg.Write((byte)Vectors.Count);
//                 foreach (var kvp in Vectors)
//                 {
//                     pkg.Write(kvp.Key);
//                     pkg.Write(kvp.Value);
//                 }
//             }
//
//             if (Rotations.Count > 0)
//             {
//                 pkg.Write((byte)Rotations.Count);
//                 foreach (var kvp in Rotations)
//                 {
//                     pkg.Write(kvp.Key);
//                     pkg.Write(kvp.Value);
//                 }
//             }
//
//             if (Ints.Count > 0)
//             {
//                 pkg.Write((byte)Ints.Count);
//                 foreach (var kvp in Ints)
//                 {
//                     pkg.Write(kvp.Key);
//                     pkg.Write(kvp.Value);
//                 }
//             }
//
//             // Intended to come before strings (changing would break existing data).
//             if (Longs.Count > 0)
//             {
//                 pkg.Write((byte)Longs.Count);
//                 foreach (var kvp in Longs)
//                 {
//                     pkg.Write(kvp.Key);
//                     pkg.Write(kvp.Value);
//                 }
//             }
//
//             if (Strings.Count > 0)
//             {
//                 pkg.Write((byte)Strings.Count);
//                 foreach (var kvp in Strings)
//                 {
//                     pkg.Write(kvp.Key);
//                     pkg.Write(kvp.Value);
//                 }
//             }
//
//             if (ByteArrays.Count > 0)
//             {
//                 pkg.Write((byte)ByteArrays.Count);
//                 foreach (var kvp in ByteArrays)
//                 {
//                     pkg.Write(kvp.Key);
//                     pkg.Write(kvp.Value);
//                 }
//             }
//
//             if ((num & 256) != 0)
//             {
//                 pkg.Write((byte)ConnectionType);
//                 pkg.Write(ConnectionHash);
//             }
//
//             return pkg;
//         }
//
//         public void Load(ZPackage pkg)
//         {
//             pkg.SetPos(0);
//             var num = pkg.ReadInt();
//             if ((num & 1) != 0)
//             {
//                 var count = pkg.ReadByte();
//                 for (var i = 0; i < count; ++i)
//                     Floats[pkg.ReadInt()] = pkg.ReadSingle();
//             }
//             if ((num & 2) != 0)
//             {
//                 var count = pkg.ReadByte();
//                 for (var i = 0; i < count; ++i)
//                     Vectors[pkg.ReadInt()] = pkg.ReadVector3();
//             }
//             if ((num & 4) != 0)
//             {
//                 var count = pkg.ReadByte();
//                 for (var i = 0; i < count; ++i)
//                     Rotations[pkg.ReadInt()] = pkg.ReadQuaternion();
//             }
//             if ((num & 8) != 0)
//             {
//                 var count = pkg.ReadByte();
//                 for (var i = 0; i < count; ++i)
//                     Ints[pkg.ReadInt()] = pkg.ReadInt();
//             }
//             // Intended to come before strings (changing would break existing data).
//             if ((num & 64) != 0)
//             {
//                 var count = pkg.ReadByte();
//                 for (var i = 0; i < count; ++i)
//                     Longs[pkg.ReadInt()] = pkg.ReadLong();
//             }
//             if ((num & 16) != 0)
//             {
//                 var count = pkg.ReadByte();
//                 for (var i = 0; i < count; ++i)
//                     Strings[pkg.ReadInt()] = pkg.ReadString();
//             }
//             if ((num & 128) != 0)
//             {
//                 var count = pkg.ReadByte();
//                 for (var i = 0; i < count; ++i)
//                     ByteArrays[pkg.ReadInt()] = pkg.ReadByteArray();
//             }
//             if ((num & 256) != 0)
//             {
//                 ConnectionType = (ZDOExtraData.ConnectionType)pkg.ReadByte();
//                 ConnectionHash = pkg.ReadInt();
//             }
//         }
//     
//         public ZDOData() {}
//
//         public ZDOData(ZDO zdo)
//         {
//             var id = zdo.m_uid;
//             Floats = ZDOExtraData.s_floats.ContainsKey(id)
//                 ? ZDOExtraData.s_floats[id].ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
//                 : new();
//             Ints = ZDOExtraData.s_ints.ContainsKey(id)
//                 ? ZDOExtraData.s_ints[id].ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
//                 : new();
//             Longs = ZDOExtraData.s_longs.ContainsKey(id)
//                 ? ZDOExtraData.s_longs[id].ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
//                 : new();
//             Strings = ZDOExtraData.s_strings.ContainsKey(id)
//                 ? ZDOExtraData.s_strings[id].ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
//                 : new();
//             Vectors = ZDOExtraData.s_vec3.ContainsKey(id)
//                 ? ZDOExtraData.s_vec3[id].ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
//                 : new();
//             Rotations = ZDOExtraData.s_quats.ContainsKey(id)
//                 ? ZDOExtraData.s_quats[id].ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
//                 : new();
//             ByteArrays = ZDOExtraData.s_byteArrays.ContainsKey(id)
//                 ? ZDOExtraData.s_byteArrays[id].ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
//                 : new();
//             if (ZDOExtraData.s_connectionsHashData.TryGetValue(id, out var conn))
//             {
//                 ConnectionType = conn.m_type;
//                 ConnectionHash = conn.m_hash;
//             }
//         }
//
//         public ZDOData(ZPackage? pkg)
//         {
//             if (pkg == null) return;
//             Load(pkg);
//         }
//
//         public void Add(ZDOData data)
//         {
//             foreach (var kvp in data.Floats) Floats[kvp.Key] = kvp.Value;
//             foreach (var kvp in data.Ints) Ints[kvp.Key] = kvp.Value;
//             foreach (var kvp in data.Longs) Longs[kvp.Key] = kvp.Value;
//             foreach (var kvp in data.Strings) Strings[kvp.Key] = kvp.Value;
//             foreach (var kvp in data.Vectors) Vectors[kvp.Key] = kvp.Value;
//             foreach (var kvp in data.Rotations) Rotations[kvp.Key] = kvp.Value;
//             foreach (var kvp in data.ByteArrays) ByteArrays[kvp.Key] = kvp.Value;
//             if (data.ConnectionType != ZDOExtraData.ConnectionType.None && data.ConnectionHash != 0)
//             {
//                 ConnectionType = data.ConnectionType;
//                 ConnectionHash = data.ConnectionHash;
//             }
//         }
//
//         public bool HasData() => Floats.Count > 0 || Ints.Count > 0 || Longs.Count > 0 || Strings.Count > 0 ||
//                                  Vectors.Count > 0 || Rotations.Count > 0 || ByteArrays.Count > 0 ||
//                                  ConnectionType != ZDOExtraData.ConnectionType.None && ConnectionHash != 0;
//
//         public void Copy(ZDO zdo)
//         {
//             var id = zdo.m_uid;
//             if (Floats.Count > 0)
//             {
//                 ZDOExtraData.s_floats.Release(id);
//                 foreach (var kvp in Floats) ZDOExtraData.Set(id, kvp.Key, kvp.Value);
//             }
//
//             if (Ints.Count > 0)
//             {
//                 ZDOExtraData.s_ints.Release(id);
//                 foreach (var kvp in Ints) ZDOExtraData.Set(id, kvp.Key, kvp.Value);
//             }
//             if (Longs.Count > 0)
//             {
//                 ZDOExtraData.s_longs.Release(id);
//                 foreach (var kvp in Longs) ZDOExtraData.Set(id, kvp.Key, kvp.Value);
//             }
//             if (Strings.Count > 0)
//             {
//                 ZDOExtraData.s_strings.Release(id);
//                 foreach (var kvp in Strings) ZDOExtraData.Set(id, kvp.Key, kvp.Value);
//             }
//             if (Vectors.Count > 0)
//             {
//                 ZDOExtraData.s_vec3.Release(id);
//                 foreach (var kvp in Vectors) ZDOExtraData.Set(id, kvp.Key, kvp.Value);
//             }
//             if (Rotations.Count > 0)
//             {
//                 ZDOExtraData.s_quats.Release(id);
//                 foreach (var kvp in Rotations) ZDOExtraData.Set(id, kvp.Key, kvp.Value);
//             }
//             if (ByteArrays.Count > 0)
//             {
//                 ZDOExtraData.s_byteArrays.Release(id);
//                 foreach (var kvp in ByteArrays) ZDOExtraData.Set(id, kvp.Key, kvp.Value);
//             }
//             if (ConnectionType != ZDOExtraData.ConnectionType.None && ConnectionHash != 0)
//                 ZDOExtraData.SetConnectionData(id, ConnectionType, ConnectionHash);
//         }
//     }
// }