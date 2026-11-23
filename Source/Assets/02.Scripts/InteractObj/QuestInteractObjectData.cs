using UnityEngine;

[CreateAssetMenu(fileName = "QuestInteractObjectData", menuName = "Game/Interact Object/Quest")]
public class QuestInteractObjectData : InteractObjectData
{
    [Header("퀘스트")]
    [SerializeField] QuestData _questData;

    public QuestData QuestData => _questData;
}
