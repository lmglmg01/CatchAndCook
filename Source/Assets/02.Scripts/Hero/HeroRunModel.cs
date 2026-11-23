using System;
using UnityEngine;

public class HeroRunModel : MonoBehaviour, IDamageable
{
    [Header("데이터")]
    [SerializeField] HeroData heroData;

    [Header("산소 소모")]
    [SerializeField, Min(0f)] float oxygenDrainPerSecond;

    [Header("현재 체력(읽기용)")]
    [SerializeField] float currentHealth;
    [Header("피격 보호")]
    [SerializeField, Min(0f)] float _damageInvulnerabilityDuration;
    float _nextDamageTime;

    float _maxOxygen;
    float _moveSpeed;
    float _defense;
    bool _isSprinting;
    float _moveSpeedBuffMultiplier;
    float _moveSpeedBuffEndTime;
    float _attackPowerBuffMultiplier;
    float _attackPowerBuffEndTime;

    public float MaxHealth => _maxOxygen;
    public float CurrentHealth => currentHealth;
    public float MaxOxygen => MaxHealth;
    public float CurrentOxygen => currentHealth;
    public float MoveSpeed => _moveSpeed;
    public float Defense => _defense;
    public bool IsSprinting => _isSprinting && isActiveAndEnabled && CurrentOxygen > 0f;
    public float SprintMoveSpeedMultiplier => heroData != null ? Mathf.Max(1f, heroData.SprintMoveSpeedMultiplier) : 1f;
    public float SprintOxygenDrainMultiplier => heroData != null ? Mathf.Max(1f, heroData.SprintOxygenDrainMultiplier) : 1f;
    public float MoveSpeedBuffMultiplier => isActiveAndEnabled && CurrentOxygen > 0f && Time.time < _moveSpeedBuffEndTime
        ? _moveSpeedBuffMultiplier : 1f;
    public float AttackPowerMultiplier => isActiveAndEnabled && CurrentOxygen > 0f && Time.time < _attackPowerBuffEndTime
        ? _attackPowerBuffMultiplier : 1f;
    public float EffectiveMoveSpeed => MoveSpeed * MoveSpeedBuffMultiplier * (IsSprinting ? SprintMoveSpeedMultiplier : 1f);
    public float EffectiveOxygenDrainPerSecond => oxygenDrainPerSecond * (IsSprinting ? SprintOxygenDrainMultiplier : 1f);
    public float OxygenDrainPerSecond => oxygenDrainPerSecond;

    public event Action<float> Damaged;
    public event Action OxygenDepleted;

    void Awake()
    {
        _maxOxygen = heroData != null ? heroData.MaxHealth : 0f;
        _moveSpeed = heroData != null ? heroData.MoveSpeed : 0f;
        currentHealth = MaxOxygen;
    }

    // 씬 시작 시 최대 산소와 현재 산소를 함께 초기화한다.
    public void SetMaxOxygen(float maxOxygen)
    {
        if (!(maxOxygen > 0f) || float.IsInfinity(maxOxygen)) return;
        _maxOxygen = maxOxygen;
        currentHealth = maxOxygen;
        _nextDamageTime = 0f;
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        if (moveSpeed < 0f || float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed)) return;
        _moveSpeed = moveSpeed;
    }

    public void SetDefense(float defense)
    {
        if (defense < 0f || float.IsNaN(defense) || float.IsInfinity(defense)) return;
        _defense = defense;
    }

    public void SetSprinting(bool sprinting)
    {
        _isSprinting = sprinting && isActiveAndEnabled && CurrentOxygen > 0f;
    }

    public bool ApplyMoveSpeedBuff(float multiplier, float duration)
    {
        if (!CanApplyUtilityBuff(multiplier, duration)) return false;
        _moveSpeedBuffMultiplier = multiplier;
        _moveSpeedBuffEndTime = Time.time + duration;
        return true;
    }

    public bool ApplyAttackPowerBuff(float multiplier, float duration)
    {
        if (!CanApplyUtilityBuff(multiplier, duration)) return false;
        _attackPowerBuffMultiplier = multiplier;
        _attackPowerBuffEndTime = Time.time + duration;
        return true;
    }

    bool CanApplyUtilityBuff(float multiplier, float duration)
    {
        return isActiveAndEnabled && CurrentOxygen > 0f && multiplier > 1f
            && !float.IsInfinity(multiplier) && duration > 0f
            && !float.IsInfinity(duration) && !float.IsInfinity(Time.time + duration);
    }

    void ClearUtilityBuffs()
    {
        _moveSpeedBuffEndTime = 0f;
        _attackPowerBuffEndTime = 0f;
    }

    void OnDisable()
    {
        _isSprinting = false;
        ClearUtilityBuffs();
    }

    void Update()
    {
        ConsumeOxygen(EffectiveOxygenDrainPerSecond * Time.deltaTime);
    }

    public void ConsumeOxygen(float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount) || currentHealth <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            ClearUtilityBuffs();
            OxygenDepleted?.Invoke();
        }
    }

    public bool RecoverOxygen(float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount) || MaxOxygen <= 0f || currentHealth >= MaxOxygen) return false;

        float previousOxygen = currentHealth;
        currentHealth = Mathf.Min(MaxOxygen, currentHealth + amount);
        return currentHealth > previousOxygen;
    }

    public void SetOxygenDrainPerSecond(float amount)
    {
        oxygenDrainPerSecond = Mathf.Max(0f, amount);
    }

    public void TakeDamage(float damage)
    {
        if (!(damage > 0f) || float.IsInfinity(damage) || currentHealth <= 0f || Time.time < _nextDamageTime) return;

        float reducedDamage = Mathf.Max(0f, damage - _defense);
        if (reducedDamage <= 0f) return;
        float appliedDamage = Mathf.Min(currentHealth, reducedDamage);
        _nextDamageTime = Time.time + _damageInvulnerabilityDuration;
        ConsumeOxygen(appliedDamage);
        Damaged?.Invoke(appliedDamage);
    }
}
