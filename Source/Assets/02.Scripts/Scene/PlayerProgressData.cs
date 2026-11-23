using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerProgressData", menuName = "Game/Player Progress Data")]
public class PlayerProgressData : ScriptableObject
{
    [Header("최대 레벨")]
    [SerializeField, Min(1)] int _maxLevel;

    [Header("레벨별 필요 경험치: 0번은 Lv.1 → Lv.2")]
    [SerializeField, Min(0f)] float[] _requiredExperiences;

    public int MaxLevel => _maxLevel;
    public IReadOnlyList<float> RequiredExperiences => _requiredExperiences ?? Array.Empty<float>();

    public bool TryGetRequiredExperience(int level, out float experience)
    {
        experience = 0f;
        int index = level - 1;
        if (level < 1 || level >= _maxLevel || _requiredExperiences == null
            || index >= _requiredExperiences.Length) return false;

        float required = _requiredExperiences[index];
        if (!(required > 0f) || float.IsInfinity(required)) return false;
        experience = required;
        return true;
    }
}
