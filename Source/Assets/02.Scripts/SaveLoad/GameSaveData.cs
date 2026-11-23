using System;
using System.Collections.Generic;

[Serializable]
public sealed class GameSaveData
{
    public int Version = 1;
    public string SceneName;
    public string PlayerName;
    public int DayNumber;
    public DayPhase DayPhase;
    public long LastPlayedUtcTicks;
    public InventorySaveData Inventory = new InventorySaveData();
    public EquipmentSaveData Equipment = new EquipmentSaveData();
    public PlayerProgressSaveData Player = new PlayerProgressSaveData();
    public QuestSaveData Quests = new QuestSaveData();
    public ShopSaveData Shop;
    public string SushiShopJson;
    public string KeyBindingsJson;
}

[Serializable]
public sealed class InventorySaveData
{
    public float Gold;
    public List<ItemSaveEntry> Items = new List<ItemSaveEntry>();
    public string FirstUtilityId;
    public string SecondUtilityId;
}

[Serializable]
public sealed class ItemSaveEntry
{
    public string ItemId;
    public int Count;
}

[Serializable]
public sealed class EquipmentSaveData
{
    public List<EquipmentSaveEntry> Entries = new List<EquipmentSaveEntry>();
}

[Serializable]
public sealed class EquipmentSaveEntry
{
    public string EquipId;
    public EquipmentData.EquipType Type;
    public int Level;
}

[Serializable]
public sealed class PlayerProgressSaveData
{
    public int BoatTutorialStep;
    public int DiveTutorialStep;
    public int BoatCommerceTutorialStep;
    public bool SushiTutorialIngredientsGranted;
    public bool SushiTutorialUpgradeSuppliesGranted;
    public bool SushiTutorialStaffUpgradeFundsGranted;
    public bool SushiTutorialCompleted;
    public bool BossGateOpened;
    public int Level;
    public float Experience;
}

[Serializable]
public sealed class QuestSaveData
{
    public List<QuestGroupSaveEntry> Groups = new List<QuestGroupSaveEntry>();
    public List<QuestSaveEntry> Quests = new List<QuestSaveEntry>();
}

[Serializable]
public sealed class QuestGroupSaveEntry
{
    public QuestGroupData.QuestType Type;
    public string GroupId;
    public int CurrentIndex;
}

[Serializable]
public sealed class QuestSaveEntry
{
    public string QuestId;
    public int CurrentCount;
    public bool IsClaimed;
    public bool QuestItemRewardGranted;
}

[Serializable]
public sealed class ShopSaveData
{
    public List<string> SaleItemIds = new List<string>();
    public List<bool> SoldOut = new List<bool>();
}
