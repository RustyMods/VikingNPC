using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Settlers.Settlers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Settlers.Behaviors;

public class Randomizer : MonoBehaviour
{
    public static List<string> m_maleFirstNames = new()
    {
        "Bjorn", "Harald", "Bo", "Frode", 
        "Birger", "Arne", "Erik", "Kare", 
        "Loki", "Thor", "Odin", "Ragnar", 
        "Sigurd", "Ivar", "Gunnar", "Sven",
        "Hakon", "Leif", "Magnus", "Rolf", 
        "Ulf", "Vidar", "Ingvar"
    };

    public static List<string> m_femaleFirstNames = new()
    {
        "Gudrun", "Hilda", "Ingrid", "Freya", 
        "Astrid", "Sigrid", "Thora", "Runa", 
        "Ylva", "Sif", "Helga", "Eira", 
        "Brynja", "Ragnhild", "Solveig", "Bodil", 
        "Signy", "Frida", "Alva", "Liv", 
        "Estrid", "Jorunn", "Aslaug", "Torunn"
    };
    
    public static List<string> m_lastNames = new()
    {
        "Ironside", "Fairhair", "Thunderfist", "Bloodaxe", 
        "Longsword", "Ravenheart", "Dragonslayer", "Stormborn", 
        "Shadowblade", "Thunderstruck", "Allfather", "Lothbrok", 
        "Snake-in-the-Eye", "the Boneless", "Ironhand", "Forkbeard",
        "the Good", "the Lucky", "the Strong", "the Walker", 
        "Ironbeard", "the Silent", "the Fearless", "Shieldmaiden",
        "Bloodfury", "Snowdrift", "Wildheart", "Battleborn", 
        "Stormshield", "Frosthammer", "Moonshadow", "Wolfsbane"
    };
    public Vector3 m_hairColor = Vector3.zero;
    public Vector3 m_skinColor = Vector3.one;
    
    public ZNetView m_nview = null!;
    public Companion m_companion = null!;
    public VisEquipment m_visEquipment = null!;
    public bool m_isElf = false;
    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        m_companion = GetComponent<Companion>();
        m_visEquipment = GetComponent<VisEquipment>();
        
        if (m_companion == null || m_visEquipment == null || !m_nview.IsValid()) return;
        GetConfigNames();
        int modelIndex = m_nview.GetZDO().GetInt(ZDOVars.s_modelIndex, Random.Range(0, 2));
        int hairIndex = m_nview.GetZDO().GetInt("HairIndex".GetStableHashCode(),  Random.Range(0, 20));
        int beardIndex = m_nview.GetZDO().GetInt("BeardIndex".GetStableHashCode(), Random.Range(0, 20));
        Vector3 hairColor = m_nview.GetZDO().GetVec3(ZDOVars.s_hairColor, HairColors.GetHairColor());
        string tamedName = m_nview.GetZDO().GetString(ZDOVars.s_tamedName, GenerateName(modelIndex));
        Vector3 skinColor = m_nview.GetZDO().GetVec3(ZDOVars.s_skinColor, m_companion is Elf ? new Vector3(0.4f, 0.6f, 0.57f) : Vector3.one);

        m_nview.GetZDO().Set(ZDOVars.s_tamedName, tamedName);
        m_nview.GetZDO().Set(ZDOVars.s_skinColor, skinColor);
        m_nview.GetZDO().Set(ZDOVars.s_modelIndex, modelIndex);

        m_companion.m_hairItem = "Hair" + hairIndex;
        m_visEquipment.SetHairItem("Hair" + hairIndex);
        
        if (modelIndex == 0)
        {
            m_companion.m_beardItem = "Beard" + beardIndex;
            m_visEquipment.SetBeardItem("Beard" + beardIndex);
        }
        
        m_visEquipment.SetHairColor(hairColor);
        m_visEquipment.SetModel(modelIndex);
        m_companion.m_name = tamedName;

        m_hairColor = hairColor;
        m_skinColor = skinColor;
    }

    private void GetConfigNames()
    {
        if (m_companion is Elf)
        {
            if (!SettlersPlugin._elfMaleNames.Value.IsNullOrWhiteSpace()) m_maleFirstNames = SettlersPlugin._elfMaleNames.Value.Split(':').ToList();
            if (!SettlersPlugin._elfFemaleNames.Value.IsNullOrWhiteSpace()) m_femaleFirstNames = SettlersPlugin._elfFemaleNames.Value.Split(':').ToList();
            if (!SettlersPlugin._elfLastNames.Value.IsNullOrWhiteSpace()) m_lastNames = SettlersPlugin._elfLastNames.Value.Split(':').ToList();
        }
        else
        {
            if (!SettlersPlugin._maleNames.Value.IsNullOrWhiteSpace()) m_maleFirstNames = SettlersPlugin._maleNames.Value.Split(':').ToList();
            if (!SettlersPlugin._femaleNames.Value.IsNullOrWhiteSpace()) m_femaleFirstNames = SettlersPlugin._femaleNames.Value.Split(':').ToList();
            if (!SettlersPlugin._lastNames.Value.IsNullOrWhiteSpace()) m_lastNames = SettlersPlugin._lastNames.Value.Split(':').ToList();
        }
    }

    private static string GenerateName(int modelIndex)
    {
        var firstName = modelIndex != 0
            ? m_femaleFirstNames[Random.Range(0, m_femaleFirstNames.Count)]
            : m_maleFirstNames[Random.Range(0, m_maleFirstNames.Count)];
        var lastName = m_lastNames[Random.Range(0, m_lastNames.Count)];

        return $"{firstName} {lastName}";
    }
}