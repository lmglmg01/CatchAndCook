using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestDataMap", menuName = "Game/Quest/Quest Data Map")]
public class QuestDataMap : ScriptableObject
{
    [Header("전체 퀘스트 목록")]
    [SerializeField] QuestData[] _quests;

    [Header("전체 퀘스트 묶음")]
    [SerializeField] QuestGroupData[] _groups;

    [Header("보상 표시")]
    [SerializeField] Sprite _goldRewardIcon;

    public Sprite GoldRewardIcon => _goldRewardIcon;
    public IReadOnlyList<QuestData> Quests => _quests ?? Array.Empty<QuestData>();
    public IReadOnlyList<QuestGroupData> Groups => _groups ?? Array.Empty<QuestGroupData>();

    public QuestGroupData GetQuestGroup(QuestGroupData.QuestType type, string groupId)
    {
        if (string.IsNullOrEmpty(groupId)) return null;
        foreach (QuestGroupData group in Groups)
        {
            if (group != null && group.Type == type && group.GroupId == groupId) return group;
        }
        return null;
    }

    public QuestGroupData GetGroupContainingQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;
        foreach (QuestGroupData group in Groups)
        {
            if (group == null) continue;
            foreach (QuestData quest in group.Quests)
            {
                if (quest != null && quest.QuestId == questId) return group;
            }
        }
        return null;
    }

    public QuestData GetQuestData(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;

        foreach (QuestData quest in Quests)
        {
            if (quest != null && quest.QuestId == questId) return quest;
        }

        QuestGroupData group = GetGroupContainingQuest(questId);
        if (group != null)
        {
            foreach (QuestData quest in group.Quests)
            {
                if (quest != null && quest.QuestId == questId) return quest;
            }
        }
        return null;
    }
}
