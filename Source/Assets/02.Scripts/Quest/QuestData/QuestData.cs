using System;
using UnityEngine;

[Serializable]
public struct QuestDialogueLine
{
    [SerializeField] bool _playerSpeaks;
    [SerializeField, TextArea(2, 5)] string _content;

    public bool PlayerSpeaks => _playerSpeaks;
    public string Content => _content;
}

[Serializable]
public sealed class QuestDialogue
{
    [SerializeField] string _playerName = "데이브";
    [SerializeField] string _npcName = "NPC";
    [SerializeField] Sprite _playerIllustration;
    [SerializeField] Sprite _npcIllustration;
    [SerializeField] QuestDialogueLine[] _lines;

    public string PlayerName => _playerName;
    public string NpcName => _npcName;
    public Sprite PlayerIllustration => _playerIllustration;
    public Sprite NpcIllustration => _npcIllustration;
    public QuestDialogueLine[] Lines => _lines ?? Array.Empty<QuestDialogueLine>();
    public bool HasLines => Lines.Length > 0;
}

public abstract class QuestData : ScriptableObject
{
    [Header("퀘스트 정보")]
    [SerializeField] string _questId;
    [SerializeField] string _questName;
    [SerializeField, Min(1)] int _requiredLevel = 1;
    [SerializeField] QuestDialogue _acceptDialogue = new QuestDialogue();

    [Header("보상")]
    [SerializeField, Min(0)] int _rewardGold;
    [SerializeField, Min(0)] int _rewardExperience;
    [SerializeField] ItemData _rewardItem;
    [SerializeField, Min(0)] int _rewardItemCount;
    [SerializeField] QuestItemData _rewardQuestItem;

    public int RewardGold => _rewardGold;
    public int RewardExperience => _rewardExperience;
    public ItemData RewardItem => _rewardItem;
    public int RewardItemCount => _rewardItemCount;
    public QuestItemData RewardQuestItem => _rewardQuestItem;

    public string QuestId => _questId;
    public string QuestName => _questName;
    // 묶음 퀘스트의 해금 조건은 QuestGroupData.RequiredLevel을 사용합니다.
    public int RequiredLevel => Mathf.Max(1, _requiredLevel);
    public QuestDialogue AcceptDialogue => _acceptDialogue;
}
