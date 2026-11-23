using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GigEquipmentData", menuName = "Game/Item/Equipment/Gig")]
public class GigEquipmentData : EquipmentData
{
    [Header("강화 단계별 능력치")]
    [SerializeField, Min(0f)] float[] _attackPowers;
    [SerializeField, Min(0f)] float[] _ranges;

    public override EquipType Type => EquipType.Gig;
    public IReadOnlyList<float> AttackPowers => _attackPowers ?? Array.Empty<float>();
    public IReadOnlyList<float> Ranges => _ranges ?? Array.Empty<float>();
}
