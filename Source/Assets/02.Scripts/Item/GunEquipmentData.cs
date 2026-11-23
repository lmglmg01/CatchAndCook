using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GunEquipmentData", menuName = "Game/Item/Equipment/Gun")]
public class GunEquipmentData : EquipmentData
{
    [Header("강화 단계별 능력치")]
    [SerializeField, Min(0f)] float[] _attackPowers;
    [SerializeField, Min(0)] int[] _bulletCounts;

    public override EquipType Type => EquipType.Gun;
    public IReadOnlyList<float> AttackPowers => _attackPowers ?? Array.Empty<float>();
    public IReadOnlyList<int> BulletCounts => _bulletCounts ?? Array.Empty<int>();
}
