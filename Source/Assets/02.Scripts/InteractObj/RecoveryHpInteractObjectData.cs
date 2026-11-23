using UnityEngine;

[CreateAssetMenu(fileName = "RecoveryHpInteractObjectData", menuName = "Game/Interact Object/Recovery HP")]
public class RecoveryHpInteractObjectData : InteractObjectData
{
    [Header("HP 회복")]
    [SerializeField, Min(0f)] float _recoveryHpValue;

    public float RecoveryHpValue => _recoveryHpValue;
}
