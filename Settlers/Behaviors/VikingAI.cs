namespace Settlers.Behaviors;

public class VikingAI : CompanionAI
{
    public override bool UpdateAI(float dt)
    {
        if (!m_companion.IsTamed()) return base.UpdateAI(dt);
        if (m_companion.IsQueueActive())
        {
            StopMoving();
            return true;
        }
        if (UpdateAttach(dt)) return true;
        
        if (m_companion.InAttack())
        {
            return base.UpdateAI(dt);
        }
        
        if (m_companion.InAttack()) return false;

        return UpdateEatItem(dt) || base.UpdateAI(dt);
    }
}