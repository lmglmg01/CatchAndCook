using UnityEngine;

public enum UtilityEffectType
{
    OxygenRecovery,
    AmmoRecovery,
    MoveSpeedIncrease,
    AttackPowerIncrease
}

[CreateAssetMenu(fileName = "UtilityItemData", menuName = "Game/Item/Utility")]
public class UtilityItemData : ItemData
{
    [Header("유틸리티 효과")]
    [SerializeField] UtilityEffectType effectType;
    [SerializeField, Min(0f)] float effectValue;

    [Header("지속 효과")]
    [SerializeField, Min(0f)] float duration;

    [Header("구매 가격")]
    [SerializeField, Min(0f)] float purchasePrice;
    

    public override ItemCategory Category => ItemCategory.Consumable;
    public UtilityEffectType EffectType => effectType;
    public float EffectValue => effectValue;
    public float Duration => duration;
    public float PurchasePrice => purchasePrice;

}
