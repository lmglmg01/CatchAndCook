using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestGroupData", menuName = "Game/Quest/Quest Group")]
public class QuestGroupData : ScriptableObject
{
    public enum QuestType
    {
        Main,
        ContentUnlock,
        Sub
    }

    [Header("퀘스트 묶음 정보")]
    [SerializeField] string _groupId;
    [SerializeField] string _groupName;
    [SerializeField] QuestType _type;
    [SerializeField, Min(1)] int _requiredLevel = 1;

    [Header("퀘스트 진행 순서")]
    [SerializeField] QuestData[] _quests;

    public string GroupId => _groupId;
    public string GroupName => _groupName;
    public QuestType Type => _type;
    public int RequiredLevel => Mathf.Max(1, _requiredLevel);
    public IReadOnlyList<QuestData> Quests => _quests ?? Array.Empty<QuestData>();
}
