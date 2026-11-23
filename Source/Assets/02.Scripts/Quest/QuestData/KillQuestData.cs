using UnityEngine;

[CreateAssetMenu(fileName = "KillQuestData", menuName = "Game/Quest/Kill")]
public class KillQuestData : QuestData
{
    [Header("처치 목표 (물고기 또는 보스의 고유 ID)")]
    [SerializeField] string _targetId;
    [SerializeField, Min(1)] int _requiredCount;

    public string TargetId => _targetId;
    public int RequiredCount => _requiredCount;
}
