using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SwimSuitEquipmentData", menuName = "Game/Item/Equipment/SwimSuit")]
public class SwimSuitEquipmentData : EquipmentData
{
    [Header("강화 단계별 능력치")]
    [SerializeField, Min(0f)] float[] _moveSpeeds;
    [SerializeField, Min(0f)] float[] _defenses;

    public override EquipType Type => EquipType.SwimSuit;
    public IReadOnlyList<float> MoveSpeeds => _moveSpeeds ?? Array.Empty<float>();
    public IReadOnlyList<float> Defenses => _defenses ?? Array.Empty<float>();
}
