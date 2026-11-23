using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDataMap", menuName = "Game/Item Data Map")]
public class ItemDataMap : ScriptableObject
{
    [Header("전체 아이템 목록")]
    [SerializeField] ItemData[] items;

    public IReadOnlyList<ItemData> Items => items ?? Array.Empty<ItemData>();

    public ItemData GetItemData(string itemId)
    {
        if (items == null || string.IsNullOrEmpty(itemId)) return null;

        foreach (ItemData item in items)
        {
            if (item != null && item.ItemId == itemId)
            {
                return item;
            }
        }

        return null;
    }
}
