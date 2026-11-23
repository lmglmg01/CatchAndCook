using UnityEngine;

[CreateAssetMenu(fileName = "TutorialDialogue", menuName = "Game/Tutorial Dialogue")]
public class TutorialDialogueData : ScriptableObject
{
    [Header("튜토리얼 대화")]
    [SerializeField] QuestDialogue _dialogue;

    public QuestDialogue Dialogue => _dialogue;
}
