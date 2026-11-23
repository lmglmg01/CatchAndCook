using UnityEngine;

[CreateAssetMenu(fileName = "FishCaptureQuestData", menuName = "Game/Quest/Fish Capture")]
public class FishCaptureQuestData : QuestData
{
    [Header("물고기 포획 목표")]
    [SerializeField] FishData _targetFish;
    [SerializeField, Min(1)] int _requiredCount;

    public FishData TargetFish => _targetFish;
    public int RequiredCount => _requiredCount;
}
