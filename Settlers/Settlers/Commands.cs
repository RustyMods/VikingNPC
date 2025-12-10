// using System.Collections.Generic;
// using System.Linq;
// using HarmonyLib;
// using ServerSync;
// using Settlers.Behaviors;
// using UnityEngine;
//
// namespace Settlers.Settlers;
//
// public static class Commands
// {
//     private static readonly CustomSyncedValue<List<string>> m_locationPositions = new(SettlersPlugin.ConfigSync, "SettlersLocationsData", new());
//
//     private static List<string> m_locations = new();
//
//     [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.GenerateLocationsIfNeeded))]
//     private static class RegisterBlueprintLocations
//     {
//         private static void Postfix() => UpdateServerLocationData();
//     }
//     
//     public static void LoadServerLocationChange()
//     {
//         m_locationPositions.ValueChanged += () =>
//         {
//             m_locations = m_locationPositions.Value;
//         };
//     }
//     
//     private static void UpdateServerLocationData()
//     {
//         if (!ZNet.instance || !ZNet.instance.IsServer()) return;
//         List<ZoneSystem.LocationInstance> waypoints = ZoneSystem.instance.GetLocationList().Where(location => location.m_location.m_prefab.Name.ToLower().Contains("blueprint")).ToList();
//
//         var data = new List<string>();
//         int count = 0;
//         foreach (ZoneSystem.LocationInstance waypoint in waypoints)
//         {
//             data.Add(FormatPosition(waypoint.m_position));
//             ++count;
//         }
//
//         m_locationPositions.Value = data;
//         SettlersPlugin.SettlersLogger.LogDebug($"Registered {count} settler locations on the server");
//     }
//
//     private static string FormatPosition(Vector3 position) => $"{position.x},{position.y},{position.z}";
//
//     private static void RevealLocations(Terminal.ConsoleEventArgs args)
//     {
//         if (!Player.m_localPlayer) return;
//         if (!Terminal.m_cheat)
//         {
//             Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Only admin can use this command");
//             return;
//         }
//
//         int count = 0;
//         if (ZNet.instance.IsServer())
//         {
//             List<ZoneSystem.LocationInstance> waypoints = ZoneSystem.instance.GetLocationList().Where(location => location.m_location.m_prefab.Name.ToLower().Contains("waypoint")).ToList();
//
//             foreach (Minimap.PinData pin in args.Context.m_findPins) Minimap.instance.RemovePin(pin);
//             
//             foreach (ZoneSystem.LocationInstance waypoint in waypoints)
//             {
//                 if (waypoint.m_placed) continue;
//                 args.Context.m_findPins.Add(Minimap.instance.AddPin(waypoint.m_position, Minimap.PinType.Icon4, FormatPosition(waypoint.m_position), false, false));
//                 ++count;
//             }
//             
//         }
//         else
//         {
//             if (m_locationPositions.Value.Count == 0) return;
//             foreach (var position in m_locations)
//             {
//                 if (!GetVector(position, out Vector3 pos)) continue;
//                  args.Context.m_findPins.Add(Minimap.instance.AddPin(pos, Minimap.PinType.Icon4, FormatPosition(pos), false, false));
//                 ++count;
//             }
//         }
//         SettlersPlugin.SettlersLogger.LogInfo($"Revealed {count} un-placed settler locations on map");
//     }
//     
//     private static bool GetVector(string input, out Vector3 output)
//     {
//         output = Vector3.zero;
//         string[] info = input.Split(',');
//         if (info.Length != 3) return false;
//         float x = float.Parse(info[0]);
//         float y = float.Parse(info[1]);
//         float z = float.Parse(info[2]);
//         output = new Vector3(x, y, z);
//         return true;
//     }
//
//     [HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
//     private static class RegisterSettlerCommands
//     {
//         private static void Postfix()
//         {
//             Terminal.ConsoleCommand commands = new("settlers", "Use help to list commands",
//                 (Terminal.ConsoleEventFailable)(
//                     args =>
//                     {
//                         if (args.Length < 2) return false;
//                         switch (args[1])
//                         {
//                             case "help":
//                                 foreach (var info in new List<string>()
//                                          {
//                                              "help: list of settler commands",
//                                          })
//                                 {
//                                     SettlersPlugin.SettlersLogger.LogInfo(info);
//                                 }
//                                 break;
//                             case "find":
//                                 RevealLocations(args);
//                                 break;
//                         }
//                         return true;
//                     }), onlyAdmin: true, optionsFetcher:()=>new(){"help", "find"});
//         }
//     }
//
// }