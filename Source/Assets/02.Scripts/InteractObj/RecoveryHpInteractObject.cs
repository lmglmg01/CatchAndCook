using UnityEngine;

public class RecoveryHpInteractObject : DiveSupplyInteractObject
{
    [Header("데이터")]
    [SerializeField] RecoveryHpInteractObjectData _data;

    public override bool CanInteract(Hero interactor)
    {
        return base.CanInteract(interactor)
            && _data != null
            && _data.RecoveryHpValue > 0f
            && interactor is UnderSeaHero;
    }

    protected override bool ApplyInteraction(Hero interactor)
    {
        if (interactor is not UnderSeaHero hero) return false;

        hero.RecoverOxygen(_data.RecoveryHpValue);
        return true;
    }
}
