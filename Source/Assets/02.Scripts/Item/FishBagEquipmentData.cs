using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FishBagEquipmentData", menuName = "Game/Item/Equipment/FishBag")]
public class FishBagEquipmentData : EquipmentData
{
    [Header("강화 단계별 능력치")]
    [SerializeField, Min(0f)] float[] _capacities;

    public override EquipType Type => EquipType.FishBag;
    public IReadOnlyList<float> Capacities => _capacities ?? Array.Empty<float>();
}
