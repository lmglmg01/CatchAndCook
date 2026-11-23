using UnityEngine;

[CreateAssetMenu(fileName = "QuestItemData", menuName = "Game/Quest Item Data")]
public sealed class QuestItemData : ItemData
{
    public override ItemCategory Category => ItemCategory.Quest;
}
