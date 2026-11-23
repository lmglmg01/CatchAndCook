using UnityEngine;

[CreateAssetMenu(fileName = "BossData", menuName = "Game/Boss Data")]
public class BossData : ScriptableObject
{
    [Header("보스 정보")]
    [SerializeField] string _bossId;
    [SerializeField] string _bossName;
    [Header("체력")]
    [SerializeField, Min(0f)] float _maxHealth;

    [Header("이동")]
    [SerializeField, Min(0f)] float _moveSpeed;

    [Header("공격")]
    [SerializeField, Min(0f)] float _attackPower;

    [Header("드롭 아이템")]
    [SerializeField] ItemData _dropItemData;

    public string BossId => _bossId;
    public string BossName => _bossName;
    public ItemData DropItemData => _dropItemData;
    public float MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public float AttackPower => _attackPower;
}
