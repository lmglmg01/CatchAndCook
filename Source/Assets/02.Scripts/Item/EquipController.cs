using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipController : MonoBehaviour
{
    const int MaxEquipmentLevel = 5;

    [Header("장비 데이터: 나이프, 작살, 산소통, 잠수복, 가방, 총 순서")]
    [SerializeField] EquipmentData[] _equipmentData;

    [Header("장비 레벨: 장비 데이터와 같은 인덱스, 1~5")]
    [SerializeField] int[] _equipmentLevels;

    public IReadOnlyList<EquipmentData> EquipmentDatas => _equipmentData ?? Array.Empty<EquipmentData>();
    public IReadOnlyList<int> EquipmentLevels => _equipmentLevels ?? Array.Empty<int>();

    public event Action<int> EquipmentCountChanged;
    public event Action<int, string, string, int, int, bool> EquipmentInfoChanged;
    public event Action<int, float, bool> UpgradeGoldChanged;
    public event Action<int, float, float> DamageChanged;
    public event Action<int, float, float> RangeChanged;
    public event Action<int, float, float> MaxOxygenChanged;
    public event Action<int, float, float> MoveSpeedChanged;
    public event Action<int, float, float> DefenseChanged;
    public event Action<int, float, float> CarryCapacityChanged;
    public event Action<int, int, int> BulletCountChanged;

    public float GetKnifeDamage() => GetKnifeDamage(GetLevel(EquipmentData.EquipType.Knife));
    public float GetKnifeRange() => GetKnifeRange(GetLevel(EquipmentData.EquipType.Knife));
    public float GetGigDamage() => GetGigDamage(GetLevel(EquipmentData.EquipType.Gig));
    public float GetGigRange() => GetGigRange(GetLevel(EquipmentData.EquipType.Gig));
    public float GetMaxOxygen() => GetMaxOxygen(GetLevel(EquipmentData.EquipType.OxygenCylinder));
    public float GetSwimSuitMoveSpeed() => GetSwimSuitMoveSpeed(GetLevel(EquipmentData.EquipType.SwimSuit));
    public float GetSwimSuitDefense() => GetSwimSuitDefense(GetLevel(EquipmentData.EquipType.SwimSuit));
    public float GetFishBagCapacity() => GetFishBagCapacity(GetLevel(EquipmentData.EquipType.FishBag));
    public float GetGunDamage() => GetGunDamage(GetLevel(EquipmentData.EquipType.Gun));
    public int GetGunBulletCount() => GetGunBulletCount(GetLevel(EquipmentData.EquipType.Gun));

    float GetKnifeDamage(int level)
    {
        var data = GetData(EquipmentData.EquipType.Knife) as KnifeEquipmentData;
        return ReadValue(data != null ? data.AttackPowers : null, level);
    }

    float GetKnifeRange(int level)
    {
        var data = GetData(EquipmentData.EquipType.Knife) as KnifeEquipmentData;
        return ReadValue(data != null ? data.Ranges : null, level);
    }

    float GetGigDamage(int level)
    {
        var data = GetData(EquipmentData.EquipType.Gig) as GigEquipmentData;
        return ReadValue(data != null ? data.AttackPowers : null, level);
    }

    float GetGigRange(int level)
    {
        var data = GetData(EquipmentData.EquipType.Gig) as GigEquipmentData;
        return ReadValue(data != null ? data.Ranges : null, level);
    }

    float GetMaxOxygen(int level)
    {
        var data = GetData(EquipmentData.EquipType.OxygenCylinder) as OxygenCylinderEquipmentData;
        return ReadValue(data != null ? data.OxygenAmounts : null, level);
    }

    float GetSwimSuitMoveSpeed(int level)
    {
        var data = GetData(EquipmentData.EquipType.SwimSuit) as SwimSuitEquipmentData;
        return ReadValue(data != null ? data.MoveSpeeds : null, level);
    }

    float GetSwimSuitDefense(int level)
    {
        var data = GetData(EquipmentData.EquipType.SwimSuit) as SwimSuitEquipmentData;
        return ReadValue(data != null ? data.Defenses : null, level);
    }

    float GetFishBagCapacity(int level)
    {
        var data = GetData(EquipmentData.EquipType.FishBag) as FishBagEquipmentData;
        return ReadValue(data != null ? data.Capacities : null, level);
    }

    float GetGunDamage(int level)
    {
        var data = GetData(EquipmentData.EquipType.Gun) as GunEquipmentData;
        return ReadValue(data != null ? data.AttackPowers : null, level);
    }

    int GetGunBulletCount(int level)
    {
        var data = GetData(EquipmentData.EquipType.Gun) as GunEquipmentData;
        return ReadValue(data != null ? data.BulletCounts : null, level);
    }

    internal EquipmentSaveData CaptureSave()
    {
        var save = new EquipmentSaveData();
        foreach (EquipmentData data in EquipmentDatas)
            if (data != null)
                save.Entries.Add(new EquipmentSaveEntry { EquipId = data.EquipId,
                    Type = data.Type, Level = GetLevel(data.Type) });
        return save;
    }

    internal bool CanRestoreSave(EquipmentSaveData save)
    {
        if (save == null || save.Entries == null || _equipmentData == null
            || save.Entries.Count != _equipmentData.Length) return false;
        var types = new HashSet<EquipmentData.EquipType>();
        foreach (EquipmentSaveEntry entry in save.Entries)
        {
            if (entry == null) return false;
            int index = (int)entry.Type;
            if (index < 0 || index >= _equipmentData.Length
                || !types.Add(entry.Type) || entry.Level < 1 || entry.Level > MaxEquipmentLevel
                || _equipmentData[index] == null || _equipmentData[index].EquipId != entry.EquipId)
                return false;
        }
        return true;
    }

    internal void RestoreSave(EquipmentSaveData save)
    {
        _equipmentLevels = new int[_equipmentData.Length];
        foreach (EquipmentSaveEntry entry in save.Entries)
            _equipmentLevels[(int)entry.Type] = entry.Level;
        PublishEquipmentValues();
    }

    public void PublishEquipmentCount()
    {
        EquipmentCountChanged?.Invoke(Enum.GetValues(typeof(EquipmentData.EquipType)).Length);
    }

    public void PublishEquipmentValues()
    {
        var types = (EquipmentData.EquipType[])Enum.GetValues(typeof(EquipmentData.EquipType));
        foreach (EquipmentData.EquipType type in types)
            PublishEquipmentValue(type);
    }

    void PublishEquipmentValue(EquipmentData.EquipType type)
    {
        EquipmentData data = GetData(type);
        int id = (int)type;
        int level = GetLevel(type);
        bool isMaxLevel = level >= MaxEquipmentLevel;
        int nextLevel = level >= 1 && !isMaxLevel ? level + 1 : 0;

        EquipmentInfoChanged?.Invoke(id, data != null ? data.EquipName : type.ToString(),
            data != null ? data.EquipDescription : string.Empty, level, MaxEquipmentLevel, isMaxLevel);
        bool hasCost = TryGetUpgradeGold(type, out float cost);
        UpgradeGoldChanged?.Invoke(id, cost, hasCost);

        switch (type)
        {
            case EquipmentData.EquipType.Knife:
                DamageChanged?.Invoke(id, GetKnifeDamage(level), GetKnifeDamage(nextLevel));
                RangeChanged?.Invoke(id, GetKnifeRange(level), GetKnifeRange(nextLevel));
                break;
            case EquipmentData.EquipType.Gig:
                DamageChanged?.Invoke(id, GetGigDamage(level), GetGigDamage(nextLevel));
                RangeChanged?.Invoke(id, GetGigRange(level), GetGigRange(nextLevel));
                break;
            case EquipmentData.EquipType.OxygenCylinder:
                MaxOxygenChanged?.Invoke(id, GetMaxOxygen(level), GetMaxOxygen(nextLevel));
                break;
            case EquipmentData.EquipType.SwimSuit:
                MoveSpeedChanged?.Invoke(id, GetSwimSuitMoveSpeed(level), GetSwimSuitMoveSpeed(nextLevel));
                DefenseChanged?.Invoke(id, GetSwimSuitDefense(level), GetSwimSuitDefense(nextLevel));
                break;
            case EquipmentData.EquipType.FishBag:
                CarryCapacityChanged?.Invoke(id, GetFishBagCapacity(level), GetFishBagCapacity(nextLevel));
                break;
            case EquipmentData.EquipType.Gun:
                DamageChanged?.Invoke(id, GetGunDamage(level), GetGunDamage(nextLevel));
                BulletCountChanged?.Invoke(id, GetGunBulletCount(level), GetGunBulletCount(nextLevel));
                break;
        }
    }

    float ReadValue(IReadOnlyList<float> values, int level)
    {
        int index = level - 1;
        if (values == null || level < 1 || level > MaxEquipmentLevel || index >= values.Count) return 0f;
        float value = values[index];
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    int ReadValue(IReadOnlyList<int> values, int level)
    {
        int index = level - 1;
        return values == null || level < 1 || level > MaxEquipmentLevel || index >= values.Count
            ? 0 : Mathf.Max(0, values[index]);
    }

    public bool TryGetUpgradeGold(EquipmentData.EquipType type, out float gold)
    {
        gold = 0f;
        EquipmentData data = GetData(type);
        int level = GetLevel(type);
        if (data == null || data.Type != type || level < 1 || level >= MaxEquipmentLevel) return false;

        int index = level - 1;
        if (index >= data.UpGradeGolds.Count) return false;
        float cost = data.UpGradeGolds[index];
        if (cost < 0f || float.IsNaN(cost) || float.IsInfinity(cost)) return false;

        gold = cost;
        return true;
    }

    // 표시와 실제 강화에서 같은 비용을 사용한다.
    public bool TryUpgradeEquipment(EquipmentData.EquipType type, Inventory inventory)
    {
        if (inventory == null || !TryGetUpgradeGold(type, out float cost)
            || !inventory.TrySpendGold(cost)) return false;

        UpgradeEquipment(type);
        return true;
    }

    EquipmentData GetData(EquipmentData.EquipType type)
    {
        int index = (int)type;
        if (_equipmentData == null || index < 0 || index >= _equipmentData.Length) return null;
        EquipmentData data = _equipmentData[index];
        return data != null && data.Type == type ? data : null;
    }

    int GetLevel(EquipmentData.EquipType type)
    {
        int index = (int)type;
        return _equipmentLevels == null || index < 0 || index >= _equipmentLevels.Length
            ? 0 : _equipmentLevels[index];
    }

    // 강화 검사가 통과한 장비의 같은 인덱스 레벨만 증가시킨다.
    void UpgradeEquipment(EquipmentData.EquipType type)
    {
        _equipmentLevels[(int)type]++;
    }
}
