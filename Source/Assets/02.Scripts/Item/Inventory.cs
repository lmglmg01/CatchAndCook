using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Serializable]
    public struct BagItem
    {
        [SerializeField] ItemData itemData;
        [SerializeField, Min(0)] int count;

        public ItemData ItemData => itemData;
        public int Count => count;

        public BagItem(ItemData itemData, int count)
        {
            this.itemData = itemData;
            this.count = count;
        }
    }

    [Header("아이템 맵")]
    [SerializeField] ItemDataMap itemMap;

    [Header("보유 아이템")]
    [SerializeField] BagItem[] items;

    [Header("골드")]
    [SerializeField, Min(0f)] float gold;

    public ItemDataMap ItemMap => itemMap;
    public IReadOnlyList<BagItem> Items => items ?? Array.Empty<BagItem>();
    public float Gold => gold;
    [Header("장착한 유틸리티")]
    [SerializeField] UtilityItemData[] equippedUtilities = new UtilityItemData[2];

    public UtilityItemData FirstUtility => equippedUtilities != null && equippedUtilities.Length > 0 ? equippedUtilities[0] : null;
    public UtilityItemData SecondUtility => equippedUtilities != null && equippedUtilities.Length > 1 ? equippedUtilities[1] : null;
    public event Action Changed;
    internal event Action<ItemData> ItemAdded;

    internal InventorySaveData CaptureSave()
    {
        var save = new InventorySaveData { Gold = gold,
            FirstUtilityId = FirstUtility != null ? FirstUtility.ItemId : null,
            SecondUtilityId = SecondUtility != null ? SecondUtility.ItemId : null };
        foreach (BagItem entry in Items)
            if (entry.ItemData != null && entry.Count > 0)
                save.Items.Add(new ItemSaveEntry { ItemId = entry.ItemData.ItemId, Count = entry.Count });
        return save;
    }

    internal bool CanRestoreSave(InventorySaveData save)
    {
        if (save == null || save.Items == null || itemMap == null || !IsValidAmount(save.Gold)) return false;
        var ids = new HashSet<string>();
        foreach (ItemSaveEntry entry in save.Items)
            if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Count <= 0
                || !ids.Add(entry.ItemId) || itemMap.GetItemData(entry.ItemId) == null) return false;
        if (!string.IsNullOrEmpty(save.FirstUtilityId) &&
            (!ids.Contains(save.FirstUtilityId) || itemMap.GetItemData(save.FirstUtilityId) is not UtilityItemData)) return false;
        if (!string.IsNullOrEmpty(save.SecondUtilityId) &&
            (!ids.Contains(save.SecondUtilityId) || itemMap.GetItemData(save.SecondUtilityId) is not UtilityItemData)) return false;
        return string.IsNullOrEmpty(save.FirstUtilityId) || save.FirstUtilityId != save.SecondUtilityId;
    }

    internal void RestoreSave(InventorySaveData save)
    {
        gold = save.Gold;
        items = new BagItem[save.Items.Count];
        for (int i = 0; i < items.Length; i++)
            items[i] = new BagItem(itemMap.GetItemData(save.Items[i].ItemId), save.Items[i].Count);
        equippedUtilities = new UtilityItemData[2];
        if (!string.IsNullOrEmpty(save.FirstUtilityId))
            equippedUtilities[0] = itemMap.GetItemData(save.FirstUtilityId) as UtilityItemData;
        if (!string.IsNullOrEmpty(save.SecondUtilityId))
            equippedUtilities[1] = itemMap.GetItemData(save.SecondUtilityId) as UtilityItemData;
        Changed?.Invoke();
    }

    public bool TrySetUtilityEquipment(UtilityItemData first, UtilityItemData second)
    {
        if (first != null && GetCount(first) <= 0 || second != null && GetCount(second) <= 0
            || first != null && first == second) return false;
        if (FirstUtility == first && SecondUtility == second) return true;
        if (equippedUtilities == null || equippedUtilities.Length != 2) equippedUtilities = new UtilityItemData[2];
        equippedUtilities[0] = first;
        equippedUtilities[1] = second;
        Changed?.Invoke();
        return true;
    }

    void RemoveUnownedUtilities()
    {
        if (equippedUtilities == null) return;
        for (int i = 0; i < equippedUtilities.Length; i++)
            if (equippedUtilities[i] != null && GetCount(equippedUtilities[i]) <= 0)
                equippedUtilities[i] = null;
    }

    void OnValidate()
    {
        if (float.IsNaN(gold) || float.IsInfinity(gold) || gold < 0f) gold = 0f;
    }

    public int GetCount(ItemData item)
    {
        if (item == null || items == null) return 0;
        foreach (BagItem entry in items)
            if (entry.ItemData == item) return entry.Count;
        return 0;
    }

    public bool CanAdd(ItemData item, int amount)
    {
        return item != null && amount > 0 && GetCount(item) <= int.MaxValue - amount;
    }

    public bool TryAdd(ItemData item, int amount)
    {
        if (!CanAdd(item, amount)) return false;
        Add(item, amount);
        ItemAdded?.Invoke(item);
        Changed?.Invoke();
        return true;
    }

    internal bool TryAddItems(IReadOnlyDictionary<ItemData, int> amounts, float additionalGold = 0f)
    {
        if (!CanAddGold(additionalGold)) return false;
        foreach (var entry in amounts)
            if (!CanAdd(entry.Key, entry.Value)) return false;

        foreach (var entry in amounts)
            Add(entry.Key, entry.Value);
        gold += additionalGold;
        Changed?.Invoke();
        return true;
    }

    void Add(ItemData item, int amount)
    {
        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].ItemData != item) continue;
                items[i] = new BagItem(item, items[i].Count + amount);
                return;
            }
        }

        int index = items != null ? items.Length : 0;
        Array.Resize(ref items, index + 1);
        items[index] = new BagItem(item, amount);
    }

    public bool TryRemove(ItemData item, int amount)
    {
        if (!Remove(item, amount)) return false;
        RemoveUnownedUtilities();
        Changed?.Invoke();
        return true;
    }

    internal bool TryRemoveQuestItems(IReadOnlyList<QuestItemData> cost)
    {
        if (cost == null || cost.Count == 0) return false;
        var distinct = new HashSet<QuestItemData>();
        foreach (QuestItemData item in cost)
            if (item == null || !distinct.Add(item) || GetCount(item) == 0) return false;

        foreach (QuestItemData item in cost) Remove(item, 1);
        Changed?.Invoke();
        return true;
    }

    bool Remove(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || items == null) return false;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].ItemData != item || items[i].Count < amount) continue;

            int remaining = items[i].Count - amount;
            if (remaining > 0)
            {
                items[i] = new BagItem(item, remaining);
            }
            else
            {
                for (int j = i; j < items.Length - 1; j++) items[j] = items[j + 1];
                Array.Resize(ref items, items.Length - 1);
            }
            return true;
        }
        return false;
    }

    public bool CanAddGold(float amount)
    {
        return IsValidAmount(amount) && IsValidAmount(gold)
            && !float.IsInfinity(gold + amount);
    }

    public bool TryAddGold(float amount)
    {
        if (!CanAddGold(amount)) return false;
        gold += amount;
        Changed?.Invoke();
        return true;
    }

    public bool TrySpendGold(float amount)
    {
        if (!IsValidAmount(amount) || !IsValidAmount(gold) || gold < amount) return false;
        gold -= amount;
        Changed?.Invoke();
        return true;
    }

    static bool IsValidAmount(float amount)
    {
        return amount >= 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);
    }
}
