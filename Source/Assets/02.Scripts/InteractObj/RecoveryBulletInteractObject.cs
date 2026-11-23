using UnityEngine;

public class RecoveryBulletInteractObject : DiveSupplyInteractObject
{
    [Header("데이터")]
    [SerializeField] RecoveryBulletInteractObjectData _data;

    public override bool CanInteract(Hero interactor)
    {
        return base.CanInteract(interactor)
            && _data != null
            && _data.RecoveryBulletValue > 0
            && interactor is UnderSeaHero;
    }

    protected override bool ApplyInteraction(Hero interactor)
    {
        if (interactor is not UnderSeaHero hero) return false;

        hero.RecoverBullet(_data.RecoveryBulletValue);
        return true;
    }
}
