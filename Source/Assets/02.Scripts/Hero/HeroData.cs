using UnityEngine;

[CreateAssetMenu(fileName = "HeroData", menuName = "Game/Hero Data")]
public class HeroData : ScriptableObject
{
    [Header("체력")]
    [SerializeField, Min(0f)] float maxHealth;

    [Header("이동")]
    [SerializeField, Min(0f)] float moveSpeed;

    [Header("잠수 달리기")]
    [SerializeField, Min(1f)] float sprintMoveSpeedMultiplier;
    [SerializeField, Min(1f)] float sprintOxygenDrainMultiplier;

    public float SprintMoveSpeedMultiplier => sprintMoveSpeedMultiplier;
    public float SprintOxygenDrainMultiplier => sprintOxygenDrainMultiplier;

    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
}
