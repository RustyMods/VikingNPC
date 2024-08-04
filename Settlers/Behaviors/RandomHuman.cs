using System.Collections.Generic;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Settlers.Behaviors;

public class RandomHuman : MonoBehaviour
{
    public List<string> m_maleFirstNames = new()
    {
        "Bjorn", "Harald", "Bo", "Frode", 
        "Birger", "Arne", "Erik", "Kare", 
        "Loki", "Thor", "Odin", "Ragnar", 
        "Sigurd", "Ivar", "Gunnar", "Sven",
        "Hakon", "Leif", "Magnus", "Rolf", 
        "Ulf", "Vidar", "Ingvar"
    };

    public List<string> m_femaleFirstNames = new()
    {
        "Gudrun", "Hilda", "Ingrid", "Freya", 
        "Astrid", "Sigrid", "Thora", "Runa", 
        "Ylva", "Sif", "Helga", "Eira", 
        "Brynja", "Ragnhild", "Solveig", "Bodil", 
        "Signy", "Frida", "Alva", "Liv", 
        "Estrid", "Jorunn", "Aslaug", "Torunn"
    };
    
    public List<string> m_lastNames = new()
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
    
    public ZNetView m_nview = null!;
    public Vector3 m_hairColor = Vector3.one;
    public void Awake()
    {
        m_nview = GetComponent<ZNetView>();
        if (!TryGetComponent(out Companion component)) return;
        if (!TryGetComponent(out VisEquipment visEquipment)) return;

        if (!SettlersPlugin._maleNames.Value.IsNullOrWhiteSpace())
        {
            m_maleFirstNames = SettlersPlugin._maleNames.Value.Split(':').ToList();
        }

        if (!SettlersPlugin._femaleNames.Value.IsNullOrWhiteSpace())
        {
            m_femaleFirstNames = SettlersPlugin._femaleNames.Value.Split(':').ToList();
        }

        if (!SettlersPlugin._lastNames.Value.IsNullOrWhiteSpace())
        {
            m_lastNames = SettlersPlugin._lastNames.Value.Split(':').ToList();
        }
        Randomize(component, visEquipment, out bool female);
        RandomName(component, female);
        m_nview.GetZDO().Set("RandomHuman", true);
    }

    public void RandomName(Companion component, bool isFemale)
    {
        if (!m_nview.IsValid()) return;
        string? vikingName = m_nview.GetZDO().GetString("RandomName".GetStableHashCode());
        if (vikingName.IsNullOrWhiteSpace())
        {
            var firstName = isFemale
                ? m_femaleFirstNames[Random.Range(0, m_femaleFirstNames.Count)]
                : m_maleFirstNames[Random.Range(0, m_femaleFirstNames.Count)];
            component.m_name = $"{firstName} {m_lastNames[Random.Range(0, m_lastNames.Count)]}";
            m_nview.GetZDO().Set("RandomName".GetStableHashCode(), component.m_name);
        }
        else
        {
            component.m_name = vikingName;
        }
    }

    public void Randomize(Companion humanoid, VisEquipment visEquipment, out bool female)
    {
        int modelIndex = Random.Range(0, 2);
        int random = Random.Range(0, 20);
        Vector3 color = new Vector3(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
        if (m_nview.GetZDO().GetBool("RandomHuman"))
        {
            modelIndex = m_nview.GetZDO().GetInt("ModelIndex");
            random = m_nview.GetZDO().GetInt("HairNumber");
            m_nview.GetZDO().GetVec3("HairColor", color);
        }
        else
        {
            m_nview.GetZDO().Set("ModelIndex", modelIndex);
            m_nview.GetZDO().Set("HairNumber", random);
            m_nview.GetZDO().Set("HairColor", color);
        }
        
        if (humanoid.m_beardItem.IsNullOrWhiteSpace())
        {
            if (modelIndex == 0)
            {
                visEquipment.SetBeardItem("Beard" + random);
            }
        }
        else
        {
            if (modelIndex == 0)
            {
                visEquipment.SetBeardItem(humanoid.m_beardItem);
            }
        }

        if (humanoid.m_hairItem.IsNullOrWhiteSpace())
        {
            visEquipment.SetHairItem("Hair" + random);
        }
        else
        {
            visEquipment.SetHairItem(humanoid.m_hairItem);
        }
        humanoid.m_beardItem = visEquipment.m_beardItem;
        humanoid.m_hairItem = visEquipment.m_hairItem;
        m_hairColor = color;
        visEquipment.SetHairColor(color);
        visEquipment.SetModel(modelIndex);
        female = modelIndex == 1;
    }
}