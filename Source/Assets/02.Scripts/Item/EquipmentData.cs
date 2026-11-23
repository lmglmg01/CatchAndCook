using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class EquipmentData : ScriptableObject
{
    public enum EquipType
    {
        Knife,
        Gig,
        OxygenCylinder,
        SwimSuit,
        FishBag,
        Gun
    }

    [Header("장비 정보")]
    [SerializeField, FormerlySerializedAs("weaponId")] string _equipId;

    [Header("장비 이미지")]
    [SerializeField] Sprite _equipIcon;

    [Header("장비 설명")]
    [SerializeField] string _equipName;
    [TextArea(3, 6)] [SerializeField] string _equipDescription;
    [TextArea(3, 6)][SerializeField] string _equipUpgradeDescription;
    [SerializeField] string _equipEffectText;
    [SerializeField] float[] _upgradeGolds;

    public string EquipId => _equipId;
    public abstract EquipType Type { get; }
    public string EquipName => _equipName;
    public Sprite EquipIcon => _equipIcon;
    public string EquipDescription => _equipDescription;
    public string EquipUpgradeDescription => _equipUpgradeDescription;
    public string EquipEffectText => _equipEffectText;
    public IReadOnlyList<float> UpGradeGolds => _upgradeGolds ?? Array.Empty<float>();
}
