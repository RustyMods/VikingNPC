using UnityEngine;

namespace Settlers.Behaviors;

public class SailorAI : CompanionAI
{
    public override bool UpdateAI(float dt)
    {
        if (!m_companion.m_attached) return base.UpdateAI(dt);
        if (m_character is not Sailor character) return false;
        UpdateTarget(character, dt, out bool _, out bool _);
        ItemDrop.ItemData itemData = SelectBestAttack(character, dt);
        if (itemData == null) return true;
        bool flag = (double)Time.time - itemData.m_lastAttackTime > itemData.m_shared.m_aiAttackInterval &&
                    (double)m_character.GetTimeSinceLastAttack() >= m_minAttackInterval && !IsTakingOff();
        if (m_targetCreature != null)
        {
            SetAlerted(true);
            var targetCreaturePos = m_targetCreature.transform.position;
            m_lastKnownTargetPos = targetCreaturePos;
            LookAt(m_targetCreature.GetTopPoint());
            var distance = Vector3.Distance(targetCreaturePos, transform.position);
            if (distance > itemData.m_shared.m_aiAttackRange) return true;
            if (flag)
            {
                DoAttack(m_targetCreature, false);
            }
        }

        return true;
    }
}