using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OxygenCylinderEquipmentData", menuName = "Game/Item/Equipment/OxygenCylinder")]
public class OxygenCylinderEquipmentData : EquipmentData
{
    [Header("강화 단계별 능력치")]
    [SerializeField, Min(0f)] float[] _oxygenAmounts;

    public override EquipType Type => EquipType.OxygenCylinder;
    public IReadOnlyList<float> OxygenAmounts => _oxygenAmounts ?? Array.Empty<float>();
}
