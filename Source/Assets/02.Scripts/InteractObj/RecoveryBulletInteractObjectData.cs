using UnityEngine;

[CreateAssetMenu(fileName = "RecoveryBulletInteractObjectData", menuName = "Game/Interact Object/Recovery Bullet")]
public class RecoveryBulletInteractObjectData : InteractObjectData
{
    [Header("총알 회복")]
    [SerializeField, Min(0)] int _recoveryBulletValue;

    public int RecoveryBulletValue => _recoveryBulletValue;
}
