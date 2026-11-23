using UnityEngine;

[CreateAssetMenu(fileName = "ObjectInteractQuestData", menuName = "Game/Quest/Object Interact")]
public class ObjectInteractQuestData : QuestData
{
    // 상호작용이 완료된 오브젝트가 이 ID를 퀘스트 컨트롤러에 전달한다.
    [Header("오브젝트 조사 목표")]
    [SerializeField] string _targetObjectId;

    public string TargetObjectId => _targetObjectId;
}
