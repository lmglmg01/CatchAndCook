using UnityEngine;

[CreateAssetMenu(fileName = "ExploreQuestData", menuName = "Game/Quest/Explore")]
public class ExploreQuestData : QuestData
{
    // 지역 트리거가 Hero 진입 시 이 ID를 퀘스트 컨트롤러에 전달한다.
    [Header("탐색 목표")]
    [SerializeField] string _targetAreaId;

    public string TargetAreaId => _targetAreaId;
}
