using System;
using System.Collections.Generic;
using UnityEngine;

public class Shop : MonoBehaviour
{
    [Header("상점에서 구매할 수 있는 유틸리티 아이템")]
    [SerializeField] UtilityItemData[] utilityItems;

    [Header("판매 가격이 정해지지 않은 아이템의 기본 매입 가격")]
    [SerializeField, Min(0f)] float defaultSellPrice = 1f;

    UtilityItemData[] _saleItems = Array.Empty<UtilityItemData>();
    bool _saleItemsPrepared;
    bool[] _soldOut = Array.Empty<bool>();

    public event Action<int, bool> SoldOutChanged;

    public IReadOnlyList<UtilityItemData> SaleItems => _saleItems;
    public IReadOnlyList<UtilityItemData> UtilityItems => utilityItems ?? Array.Empty<UtilityItemData>();

    internal ShopSaveData CaptureSave()
    {
        var save = new ShopSaveData();
        for (int i = 0; i < _saleItems.Length; i++)
        {
            save.SaleItemIds.Add(_saleItems[i] != null ? _saleItems[i].ItemId : null);
            save.SoldOut.Add(IsSoldOut(i));
        }
        return save;
    }

    internal bool TryRestoreSave(ShopSaveData save, int slotCount)
    {
        if (save == null || save.SaleItemIds == null || save.SoldOut == null
            || save.SaleItemIds.Count != save.SoldOut.Count
            || save.SaleItemIds.Count > slotCount) return false;
        var result = new UtilityItemData[save.SaleItemIds.Count];
        var ids = new HashSet<string>();
        for (int i = 0; i < result.Length; i++)
        {
            string id = save.SaleItemIds[i];
            if (string.IsNullOrEmpty(id) || !ids.Add(id)) return false;
            foreach (UtilityItemData item in UtilityItems)
                if (item != null && item.ItemId == id) { result[i] = item; break; }
            if (result[i] == null) return false;
        }
        _saleItems = result;
        _soldOut = save.SoldOut.ToArray();
        _saleItemsPrepared = true;
        PublishSaleItemStates();
        return true;
    }

    public void PrepareSaleItems(int slotCount)
    {
        if (_saleItemsPrepared || slotCount <= 0) return;

        var candidates = new List<UtilityItemData>();
        var ids = new HashSet<string>();
        if (utilityItems != null)
        {
            foreach (UtilityItemData item in utilityItems)
            {
                if (item == null || candidates.Contains(item)) continue;
                if (!string.IsNullOrEmpty(item.ItemId) && !ids.Add(item.ItemId)) continue;
                candidates.Add(item);
            }
        }

        int count = Mathf.Min(slotCount, candidates.Count);
        _saleItems = new UtilityItemData[count];
        _soldOut = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, candidates.Count);
            UtilityItemData item = candidates[randomIndex];
            candidates[randomIndex] = candidates[i];
            candidates[i] = item;
            _saleItems[i] = item;
        }
        _saleItemsPrepared = true;
    }

    public bool IsSoldOut(int index)
    {
        return index >= 0 && index < _soldOut.Length && _soldOut[index];
    }

    public void PublishSaleItemStates()
    {
        for (int i = 0; i < _saleItems.Length; i++)
            SoldOutChanged?.Invoke(i, IsSoldOut(i));
    }

    public bool TryBuy(UtilityItemData item, int amount, Inventory inventory)
    {
        int index = Array.IndexOf(_saleItems, item);
        if (item == null || amount <= 0 || inventory == null || index < 0 || IsSoldOut(index)
            || !inventory.CanAdd(item, amount)
            || !TryGetTotal(item.PurchasePrice, amount, out float total)
            || !inventory.TrySpendGold(total)) return false;

        if (inventory.TryAdd(item, amount))
        {
            _soldOut[index] = true;
            SoldOutChanged?.Invoke(index, true);
            return true;
        }
        inventory.TryAddGold(total);
        return false;
    }

    public bool TrySell(ItemData item, int amount, Inventory inventory)
    {
        if (item == null || amount <= 0 || inventory == null
            || inventory.GetCount(item) < amount
            || !TryGetSellTotal(item, amount, out float total)
            || !inventory.CanAddGold(total)
            || !inventory.TryRemove(item, amount)) return false;

        if (inventory.TryAddGold(total)) return true;
        inventory.TryAdd(item, amount);
        return false;
    }

    public bool TryGetSellTotal(ItemData item, int amount, out float total)
    {
        total = 0f;
        return item != null && item.IsSellable && TryGetTotal(item.SellPrice > 0f ? item.SellPrice : defaultSellPrice,
            amount, out total);
    }

    public bool Sells(UtilityItemData item)
    {
        if (item == null) return false;
        foreach (UtilityItemData listed in _saleItems)
            if (listed == item) return true;
        return false;
    }

    static bool TryGetTotal(float unitPrice, int amount, out float total)
    {
        total = 0f;
        if (unitPrice < 0f || amount <= 0 || float.IsNaN(unitPrice)
            || float.IsInfinity(unitPrice)) return false;

        total = unitPrice * amount;
        return !float.IsInfinity(total) && !float.IsNaN(total);
    }
}
