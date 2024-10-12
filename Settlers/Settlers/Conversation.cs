using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using ServerSync;
using YamlDotNet.Serialization;

namespace Settlers.Settlers;

public static class Conversation
{
    private static readonly CustomSyncedValue<string> m_serverData = new CustomSyncedValue<string>(SettlersPlugin.ConfigSync, "ServerConversationData", "");

    private static readonly string m_fileName = "Conversations.yml";
    private static readonly string m_filePath = MyPaths.GetFolderPath() + Path.DirectorySeparatorChar + m_fileName;


    public static Dictionary<string, List<string>> m_speech = GetDefaults();

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    private static class ZNet_Awake_Patch
    {
        private static void Postfix(ZNet __instance)
        {
            if (!__instance || !__instance.IsServer()) return;
            var serializer = new SerializerBuilder().Build();
            m_serverData.Value = serializer.Serialize(m_speech);
        }
    }

    public static void Setup()
    {
        if (!Directory.Exists(MyPaths.GetFolderPath())) Directory.CreateDirectory(MyPaths.GetFolderPath());
        if (!File.Exists(m_filePath))
        {
            var serializer = new SerializerBuilder().Build();
            File.WriteAllText(m_filePath, serializer.Serialize(m_speech));
            return;
        }
        var deserializer = new DeserializerBuilder().Build();
        try
        {
            var serial = File.ReadAllText(m_filePath);
            m_speech = deserializer.Deserialize<Dictionary<string, List<string>>>(serial);
        }
        catch
        {
            SettlersPlugin.SettlersLogger.LogDebug("Failed to parse file: " + Path.GetFileName(m_filePath));
        }

        m_serverData.ValueChanged += () =>
        {
            if (!ZNet.instance || ZNet.instance.IsServer()) return;
            try
            {
                m_speech = deserializer.Deserialize<Dictionary<string, List<string>>>(m_serverData.Value);
            }
            catch
            {
                SettlersPlugin.SettlersLogger.LogDebug("Failed to parse server conversations");
            }
        };
    }

    private static Dictionary<string, List<string>> GetDefaults()
    {
        return new Dictionary<string, List<string>>
        {
            ["what are you doing"] = new() { "Tending to my blade, it must always be ready.", "Keeping an eye on the skies, Odin's ravens are near." },
            ["where are you going"] = new() { "To the woods, where the trolls wander.", "I seek the shores, where the serpents swim." },
            ["what is your favorite color"] = new() { "The blue of the frozen fjords.", "The red of a battle-stained sky." },
            ["how are you feeling"] = new() { "Ready for battle, as always.", "The weight of fate presses upon me, but I stand strong." },
            ["who are you?"] = new() { "A humble warrior of the North.", "One who walks in the shadow of the gods." },
            ["do you like the gods"] = new() { "The gods are wise, but their ways are cruel.", "I honor the gods, but I carve my own path." },
            ["have you seen a giant"] = new() { "Once, in the far mountains. It shook the earth as it walked.", "Aye, they tower like trees, but they fall all the same." },
            ["do you fear death"] = new() { "Death is but the beginning of the warrior's true journey.", "Not when Valhalla awaits the brave." },
            ["what is your weapon"] = new() { "An axe, heavy as a bear’s paw, and twice as deadly.", "A bow, swift and silent as a wolf in the night." },
            ["do you have a family"] = new() { "The gods are my family, but my brothers are those who fight beside me.", "My kin are far, but their spirit is with me always." },
            ["what do you eat"] = new() { "Venison and mead, a meal fit for a warrior.", "Anything that fills the belly before battle." },
            ["where are you from"] = new() { "From the icy fjords of the North, where the winds bite.", "A village long gone, swallowed by the sea." },
            ["why are you here"] = new() { "I seek glory in the eyes of the gods.", "Fate called me to these lands, and I answered." },
            ["have you seen eikthyr"] = new() { "Aye, the stag of legend roams these lands. Its antlers crackle with thunder.", "I have, but beware, for Eikthyr's power is unmatched." }
        };
    }
}