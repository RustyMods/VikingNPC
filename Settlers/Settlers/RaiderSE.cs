namespace Settlers.Settlers;

public class RaiderSE : StatusEffect
{
    public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
    {
        hitData.ApplyModifier(SettlersPlugin._attackModifier.Value);
    }

    public override void OnDamaged(HitData hit, Character attacker)
    {
        hit.ApplyModifier(SettlersPlugin._onDamagedModifier.Value);
    }
}