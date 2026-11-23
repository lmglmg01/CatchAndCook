using UnityEngine;

[CreateAssetMenu(fileName = "ItemCollectQuestData", menuName = "Game/Quest/Item Collect")]
public class ItemCollectQuestData : QuestData
{
    [Header("재료 수집 목표")]
    [SerializeField] ItemData _targetItem;
    [SerializeField, Min(1)] int _requiredCount;

    public ItemData TargetItem => _targetItem;
    public int RequiredCount => _requiredCount;
}
