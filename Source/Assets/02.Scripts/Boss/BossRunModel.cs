using System;
using UnityEngine;

public class BossRunModel : MonoBehaviour
{
    [Header("보스 데이터")]
    [SerializeField] BossData _bossData;

    float _maxHealth;
    float _currentHealth;
    float _moveSpeed;
    float _attackPower;
    bool _isInitialized;

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public float MoveSpeed => _moveSpeed;
    public float AttackPower => _attackPower;
    public bool IsInitialized => _isInitialized;
    public bool IsDead => _isInitialized && _currentHealth <= 0f;
    public ItemData DropItemData => _bossData != null ? _bossData.DropItemData : null;

    public event Action<float> Damaged;
    public event Action Died;

    // Boss의 초기화 과정에서 호출한다. 재호출하면 시작 능력치로 초기화된다.
    public bool Initialize()
    {
        if (_bossData == null)
        {
            Debug.LogError("BossRunModel에 BossData가 연결되지 않았습니다.", this);
            return false;
        }

        _maxHealth = Mathf.Max(0f, _bossData.MaxHealth);
        _currentHealth = _maxHealth;
        _moveSpeed = Mathf.Max(0f, _bossData.MoveSpeed);
        _attackPower = Mathf.Max(0f, _bossData.AttackPower);
        _isInitialized = true;
        return true;
    }

    public void TakeDamage(float damage)
    {
        if (!_isInitialized || IsDead || !(damage > 0f) || float.IsInfinity(damage)) return;

        float appliedDamage = Mathf.Min(_currentHealth, damage);
        _currentHealth -= appliedDamage;
        bool died = _currentHealth <= 0f;
        Damaged?.Invoke(appliedDamage);
        if (died) Died?.Invoke();
    }
}
