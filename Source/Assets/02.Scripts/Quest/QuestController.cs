using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestController : MonoBehaviour
{
    public sealed class GroupProgress
    {
        public QuestGroupData Data { get; }
        public int CurrentIndex { get; internal set; }
        public bool IsCompleted => CurrentIndex >= Data.Quests.Count;
        public QuestData CurrentQuest => IsCompleted ? null : Data.Quests[CurrentIndex];

        internal GroupProgress(QuestGroupData data)
        {
            Data = data;
        }
    }

    public sealed class QuestProgress
    {
        public QuestData Data { get; }
        public int RequiredCount { get; }
        public int CurrentCount { get; internal set; }
        public bool IsCompleted => CurrentCount >= RequiredCount;
        public bool IsClaimed { get; internal set; }
        internal bool QuestItemRewardGranted { get; set; }

        internal QuestProgress(QuestData data, int requiredCount)
        {
            Data = data;
            RequiredCount = requiredCount;
        }
    }

    [Header("퀘스트 데이터")]
    [SerializeField] QuestDataMap _questDataMap;

    readonly List<QuestProgress> _quests = new List<QuestProgress>();
    readonly List<GroupProgress> _groups = new List<GroupProgress>();

    public QuestDataMap DataMap => _questDataMap;
    public IReadOnlyList<QuestProgress> Quests => _quests.AsReadOnly();
    public IReadOnlyList<GroupProgress> Groups => _groups.AsReadOnly();
    public event Action<QuestProgress> QuestAccepted;
    public event Action<QuestProgress> QuestProgressChanged;
    public event Action<QuestProgress> QuestCompleted;
    public event Action<QuestProgress> QuestClaimed;
    public event Action<GroupProgress> QuestGroupCompleted;

    // QuestController와 PlayerProgress는 GameManager에 함께 배치됩니다.
    int CurrentLevel
    {
        get
        {
            PlayerProgress progress = GetComponent<PlayerProgress>();
            return progress != null ? Mathf.Max(1, progress.Level) : 1;
        }
    }

    internal QuestSaveData CaptureSave()
    {
        var save = new QuestSaveData();
        foreach (GroupProgress group in _groups)
            save.Groups.Add(new QuestGroupSaveEntry { Type = group.Data.Type,
                GroupId = group.Data.GroupId, CurrentIndex = group.CurrentIndex });
        foreach (QuestProgress quest in _quests)
            save.Quests.Add(new QuestSaveEntry { QuestId = quest.Data.QuestId,
                CurrentCount = quest.CurrentCount, IsClaimed = quest.IsClaimed,
                QuestItemRewardGranted = quest.QuestItemRewardGranted });
        return save;
    }

    internal bool CanRestoreSave(QuestSaveData save, int playerLevel)
    {
        if (save == null || save.Groups == null || save.Quests == null || _questDataMap == null)
            return false;
        var groupIds = new HashSet<string>();
        var questIds = new HashSet<string>();
        var savedQuests = new Dictionary<string, QuestSaveEntry>();
        foreach (QuestSaveEntry entry in save.Quests)
        {
            if (entry == null || string.IsNullOrEmpty(entry.QuestId) || !questIds.Add(entry.QuestId))
                return false;
            QuestData data = _questDataMap.GetQuestData(entry.QuestId);
            int required = GetRequiredCount(data);
            if (data == null || data.RequiredLevel > playerLevel || required <= 0
                || entry.CurrentCount < 0 || entry.CurrentCount > required
                || entry.IsClaimed && entry.CurrentCount < required
                || entry.QuestItemRewardGranted && !entry.IsClaimed) return false;
            savedQuests.Add(entry.QuestId, entry);
        }
        int activeMainCount = 0;
        foreach (QuestGroupSaveEntry entry in save.Groups)
        {
            if (entry == null || string.IsNullOrEmpty(entry.GroupId)
                || !groupIds.Add(((int)entry.Type) + ":" + entry.GroupId)) return false;
            QuestGroupData group = _questDataMap.GetQuestGroup(entry.Type, entry.GroupId);
            if (group == null || group.RequiredLevel > playerLevel || group.Quests.Count == 0
                || entry.CurrentIndex < 0 || entry.CurrentIndex > group.Quests.Count) return false;
            if (entry.Type == QuestGroupData.QuestType.Main && entry.CurrentIndex < group.Quests.Count)
                activeMainCount++;
            for (int i = 0; i < group.Quests.Count; i++)
            {
                QuestData data = group.Quests[i];
                if (data == null || string.IsNullOrEmpty(data.QuestId)) return false;
                bool shouldExist = i <= entry.CurrentIndex && i < group.Quests.Count;
                if (shouldExist != savedQuests.TryGetValue(data.QuestId, out QuestSaveEntry progress))
                    return false;
                if (shouldExist && i < entry.CurrentIndex
                    && progress.CurrentCount != GetRequiredCount(data)) return false;
                if (shouldExist && i == entry.CurrentIndex
                    && progress.CurrentCount >= GetRequiredCount(data)) return false;
            }
        }
        if (activeMainCount > 1) return false;
        foreach (QuestSaveEntry entry in save.Quests)
        {
            QuestGroupData parent = _questDataMap.GetGroupContainingQuest(entry.QuestId);
            if (parent != null && !groupIds.Contains(((int)parent.Type) + ":" + parent.GroupId))
                return false;
        }
        return true;
    }

    internal void RestoreSave(QuestSaveData save, Inventory inventory)
    {
        _groups.Clear();
        _quests.Clear();
        foreach (QuestGroupSaveEntry entry in save.Groups)
            _groups.Add(new GroupProgress(_questDataMap.GetQuestGroup(entry.Type, entry.GroupId))
                { CurrentIndex = entry.CurrentIndex });
        foreach (QuestSaveEntry entry in save.Quests)
        {
            QuestData data = _questDataMap.GetQuestData(entry.QuestId);
            _quests.Add(new QuestProgress(data, GetRequiredCount(data))
                { CurrentCount = entry.CurrentCount, IsClaimed = entry.IsClaimed,
                    QuestItemRewardGranted = entry.QuestItemRewardGranted });
        }

        if (inventory?.ItemMap == null) return;
        foreach (QuestProgress quest in _quests)
        {
            QuestItemData item = quest.Data.RewardQuestItem;
            if (!quest.IsClaimed || quest.QuestItemRewardGranted || item == null
                || inventory.ItemMap.GetItemData(item.ItemId) != item) continue;

            // 이전 저장에서 일반 보상을 이미 받은 경우 퀘스트 아이템만 복원합니다.
            quest.QuestItemRewardGranted = inventory.GetCount(item) > 0 || inventory.TryAdd(item, 1);
        }
    }

    public GroupProgress GetGroupProgress(QuestGroupData.QuestType type, string groupId)
    {
        foreach (GroupProgress group in _groups)
        {
            if (group.Data.Type == type && group.Data.GroupId == groupId) return group;
        }
        return null;
    }

    public bool IsGroupUnlocked(QuestGroupData data)
    {
        if (_questDataMap == null || data == null || CurrentLevel < data.RequiredLevel)
            return false;

        if (data.Type != QuestGroupData.QuestType.Main) return true;

        // Earlier main groups must be completed even if the player has already reached a higher level.
        foreach (QuestGroupData previous in _questDataMap.Groups)
        {
            if (previous == null || previous.Type != QuestGroupData.QuestType.Main
                || previous.RequiredLevel >= data.RequiredLevel) continue;
            GroupProgress progress = GetGroupProgress(previous.Type, previous.GroupId);
            if (progress == null || !progress.IsCompleted) return false;
        }
        return true;
    }

    public bool AcceptQuestGroup(QuestGroupData.QuestType type, string groupId)
    {
        if (_questDataMap == null || GetGroupProgress(type, groupId) != null) return false;
        QuestGroupData data = _questDataMap.GetQuestGroup(type, groupId);
        if (data == null || data.Quests.Count == 0 || !IsGroupUnlocked(data)) return false;

        // 잘못된 목표나 중복 ID로 묶음 진행이 중간에 막히지 않도록 검사한다.
        var ids = new HashSet<string>();
        foreach (QuestData quest in data.Quests)
        {
            if (quest == null || string.IsNullOrEmpty(quest.QuestId)
                || !ids.Add(quest.QuestId) || GetRequiredCount(quest) <= 0
                || GetQuestProgress(quest.QuestId) != null) return false;
            foreach (GroupProgress running in _groups)
            {
                foreach (QuestData reserved in running.Data.Quests)
                {
                    if (reserved != null && reserved.QuestId == quest.QuestId) return false;
                }
            }
        }

        if (type == QuestGroupData.QuestType.Main)
        {
            foreach (GroupProgress group in _groups)
            {
                if (group.Data.Type == type && !group.IsCompleted) return false;
            }
        }

        GroupProgress progress = new GroupProgress(data);
        _groups.Add(progress);
        AcceptQuestData(progress.CurrentQuest);
        return true;
    }

    [ContextMenu("테스트/0번 묶음 수락")]
    public void TestAcceptFirstGroup()
    {
        if (!Application.isPlaying || _questDataMap == null
            || _questDataMap.Groups.Count == 0 || _questDataMap.Groups[0] == null) return;
        QuestGroupData group = _questDataMap.Groups[0];
        Debug.Log(AcceptQuestGroup(group.Type, group.GroupId)
            ? $"퀘스트 묶음 수락: {group.GroupName}"
            : "묶음을 수락할 수 없습니다. ID, 목표 설정, 진행 중인 묶음을 확인하세요.", this);
    }

    [ContextMenu("테스트/0번 묶음의 현재 퀘스트 완료")]
    public void TestCompleteFirstGroupQuest()
    {
        if (!Application.isPlaying || _questDataMap == null
            || _questDataMap.Groups.Count == 0 || _questDataMap.Groups[0] == null) return;
        QuestGroupData data = _questDataMap.Groups[0];
        GroupProgress group = GetGroupProgress(data.Type, data.GroupId);
        if (group == null || group.IsCompleted) return;
        QuestProgress quest = GetQuestProgress(group.CurrentQuest.QuestId);
        if (quest == null) return;
        AddProgress(target => target == quest.Data, quest.RequiredCount - quest.CurrentCount);
    }

    [ContextMenu("테스트/0번 퀘스트 수락")]
    public void TestAcceptFirstQuest()
    {
        if (!Application.isPlaying) return;
        QuestData quest = GetFirstQuest();
        if (quest == null) return;

        bool accepted = AcceptQuest(quest.QuestId);
        Debug.Log(accepted
            ? $"퀘스트 수락: {quest.QuestName}"
            : "퀘스트를 수락할 수 없습니다. 중복 수락 여부와 목표 설정을 확인하세요.", this);
    }

    [ContextMenu("테스트/0번 퀘스트 완료")]
    public void TestCompleteFirstQuest()
    {
        if (!Application.isPlaying) return;
        QuestData quest = GetFirstQuest();
        if (quest == null) return;

        QuestProgress progress = GetQuestProgress(quest.QuestId);
        if (progress == null)
        {
            Debug.Log("먼저 0번 퀘스트를 수락하세요.", this);
            return;
        }
        if (progress.IsCompleted) return;

        AddProgress(data => data == progress.Data,
            progress.RequiredCount - progress.CurrentCount);
        Debug.Log($"퀘스트 완료: {quest.QuestName}", this);
    }

    QuestData GetFirstQuest()
    {
        if (_questDataMap == null || _questDataMap.Quests.Count == 0
            || _questDataMap.Quests[0] == null)
        {
            Debug.LogWarning("QuestDataMap의 0번 퀘스트를 연결하세요.", this);
            return null;
        }

        return _questDataMap.Quests[0];
    }

    // 수락 전 발생한 행동은 진행 수량에 포함하지 않는다.
    public bool AcceptQuest(string questId)
    {
        if (_questDataMap == null || string.IsNullOrEmpty(questId)
            || GetQuestProgress(questId) != null) return false;

        QuestGroupData group = _questDataMap.GetGroupContainingQuest(questId);
        if (group != null)
        {
            return group.Quests.Count > 0 && group.Quests[0] != null
                && group.Quests[0].QuestId == questId
                && AcceptQuestGroup(group.Type, group.GroupId);
        }

        QuestData data = _questDataMap.GetQuestData(questId);
        int requiredCount = GetRequiredCount(data);
        if (requiredCount <= 0 || CurrentLevel < data.RequiredLevel) return false;

        AcceptQuestData(data);
        return true;
    }

    void AcceptQuestData(QuestData data)
    {
        QuestProgress progress = new QuestProgress(data, GetRequiredCount(data));
        _quests.Add(progress);
        QuestAccepted?.Invoke(progress);
    }

    public QuestProgress GetQuestProgress(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return null;

        foreach (QuestProgress progress in _quests)
        {
            if (progress.Data.QuestId == questId) return progress;
        }

        return null;
    }

    public bool TryClaimQuest(string questId)
    {
        QuestProgress progress = GetQuestProgress(questId);
        if (progress == null || !progress.IsCompleted || progress.IsClaimed) return false;
        // 보상 지급 이벤트에서 다시 수령을 요청해도 보상을 한 번만 지급한다.
        progress.IsClaimed = true;
        progress.QuestItemRewardGranted = progress.Data.RewardQuestItem != null;
        if (!TryGrantReward(progress.Data))
        {
            progress.IsClaimed = false;
            progress.QuestItemRewardGranted = false;
            return false;
        }

        QuestClaimed?.Invoke(progress);
        return true;
    }

    bool TryGrantReward(QuestData quest)
    {
        ItemData item = quest.RewardItem;
        int count = quest.RewardItemCount;
        int gold = quest.RewardGold;
        int experience = quest.RewardExperience;
        if (gold < 0 || experience < 0 || count < 0 || (item == null) != (count == 0))
            return false;

        QuestItemData questItem = quest.RewardQuestItem;
        bool hasInventoryReward = gold > 0 || item != null || questItem != null;
        if (!hasInventoryReward && experience == 0) return true;

        GameManager manager = GetComponent<GameManager>();
        if (manager == null) return false;
        Inventory inventory = manager.Inventory;
        if (hasInventoryReward && (inventory == null || !inventory.CanAddGold(gold))) return false;

        var items = new Dictionary<ItemData, int>(2);
        if (item != null) items.Add(item, count);
        if (questItem != null)
        {
            items.TryGetValue(questItem, out int amount);
            if (amount == int.MaxValue) return false;
            items[questItem] = amount + 1;
        }

        foreach (var reward in items)
            if (inventory.ItemMap == null || inventory.ItemMap.GetItemData(reward.Key.ItemId) != reward.Key
                || !inventory.CanAdd(reward.Key, reward.Value)) return false;

        PlayerProgress player = manager.PlayerProgress;
        if (experience > 0 && player == null) return false;
        PlayerProgressSaveData previousProgress = null;
        if (experience > 0 && !player.IsMaxLevel)
        {
            if (hasInventoryReward) previousProgress = player.CaptureSave();
            if (!player.AddExperience(experience)) return false;
        }
        if (!hasInventoryReward) return true;

        bool granted = items.Count == 0 ? inventory.TryAddGold(gold) : inventory.TryAddItems(items, gold);
        // 경험치 이벤트로 인벤토리 상태가 달라진 경우에도 지급을 다시 시도할 수 있다.
        if (!granted && previousProgress != null) player.RestoreSave(previousProgress);
        return granted;
    }

    public void ReportFishCaptured(FishData fish, int amount)
    {
        if (fish == null || string.IsNullOrEmpty(fish.FishId)) return;
        AddProgress(data => data is FishCaptureQuestData capture
            && capture.TargetFish != null
            && capture.TargetFish.FishId == fish.FishId, amount);
    }

    // 물고기는 FishId, 보스는 보스의 고유 ID를 전달한다.
    public void ReportKilled(string targetId, int amount)
    {
        if (string.IsNullOrEmpty(targetId)) return;
        AddProgress(data => data is KillQuestData kill
            && kill.TargetId == targetId, amount);
    }

    public void ReportItemCollected(ItemData item, int amount)
    {
        if (item == null || string.IsNullOrEmpty(item.ItemId)) return;
        AddProgress(data => data is ItemCollectQuestData collect
            && collect.TargetItem != null
            && collect.TargetItem.ItemId == item.ItemId, amount);
    }

    public void ReportAreaEntered(string areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return;
        AddProgress(data => data is ExploreQuestData explore
            && explore.TargetAreaId == areaId, 1);
    }

    public bool HasActiveObjectTarget(string objectId)
    {
        if (string.IsNullOrEmpty(objectId)) return false;
        foreach (QuestProgress progress in _quests)
            if (!progress.IsCompleted && progress.Data is ObjectInteractQuestData target
                && target.TargetObjectId == objectId) return true;
        return false;
    }

    public void ReportObjectInteracted(string objectId)
    {
        if (string.IsNullOrEmpty(objectId)) return;
        AddProgress(data => data is ObjectInteractQuestData interact
            && interact.TargetObjectId == objectId, 1);
    }

    void AddProgress(Predicate<QuestData> matches, int amount)
    {
        if (amount <= 0) return;

        // 이벤트 구독자가 새 퀘스트를 수락해도 이번 알림에는 포함하지 않는다.
        foreach (QuestProgress progress in _quests.ToArray())
        {
            if (progress.IsCompleted || !matches(progress.Data)) continue;

            int remaining = progress.RequiredCount - progress.CurrentCount;
            progress.CurrentCount += Math.Min(amount, remaining);
            bool completed = progress.IsCompleted;
            QuestProgressChanged?.Invoke(progress);
            if (completed)
            {
                QuestCompleted?.Invoke(progress);
                AdvanceGroup(progress.Data);
            }
        }
    }

    void AdvanceGroup(QuestData completedQuest)
    {
        foreach (GroupProgress group in _groups.ToArray())
        {
            if (group.IsCompleted || group.CurrentQuest != completedQuest) continue;
            group.CurrentIndex++;
            if (group.IsCompleted) QuestGroupCompleted?.Invoke(group);
            else AcceptQuestData(group.CurrentQuest);
        }
    }

    public int GetRequiredCount(QuestData data)
    {
        switch (data)
        {
            case FishCaptureQuestData capture:
                return capture.TargetFish != null
                    && !string.IsNullOrEmpty(capture.TargetFish.FishId)
                    ? capture.RequiredCount : 0;
            case KillQuestData kill:
                return !string.IsNullOrEmpty(kill.TargetId) ? kill.RequiredCount : 0;
            case ItemCollectQuestData collect:
                return collect.TargetItem != null
                    && !string.IsNullOrEmpty(collect.TargetItem.ItemId)
                    ? collect.RequiredCount : 0;
            case ExploreQuestData explore:
                return !string.IsNullOrEmpty(explore.TargetAreaId) ? 1 : 0;
            case ObjectInteractQuestData interact:
                return !string.IsNullOrEmpty(interact.TargetObjectId) ? 1 : 0;
            default:
                return 0;
        }
    }
}
