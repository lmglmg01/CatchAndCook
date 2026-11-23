using System;

public interface IEquipUpgradeView
{
    event Action OnOpened;
    event Action<int> OnUpgradeRequested;

    void SetEquipmentCount(int count);
    // equipType identifies the equipment. Only basic display values cross this interface.
    void SetEquipment(int equipType, string equipName, string description,
        int level, int maxLevel, bool isMaxLevel);
    void UpdateUpgradeGold(int equipType, float needGold, bool hasCost);
    void UpdateDamage(int equipType, float currentValue, float nextValue);
    void UpdateRange(int equipType, float currentValue, float nextValue);
    void UpdateMaxOxygen(int equipType, float currentValue, float nextValue);
    void UpdateMoveSpeed(int equipType, float currentValue, float nextValue);
    void UpdateDefense(int equipType, float currentValue, float nextValue);
    void UpdateCarryCapacity(int equipType, float currentValue, float nextValue);
    void UpdateBulletCount(int equipType, int currentValue, int nextValue);
    void SetGold(float gold);
    void ShowUpgradeResult(bool succeeded);
}
