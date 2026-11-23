using UnityEngine;

public class EntranceBossAnimatorHandler : MonoBehaviour
{
    [Header("등장 연출 애니메이터")]
    [SerializeField] Animator _animator;
    [Header("등장 포효")]
    [SerializeField] AnimationClip _roarClip;

    static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
    static readonly int RoarId = Animator.StringToHash("Roar");

    public float RoarDuration => _roarClip != null ? _roarClip.length : 0f;
    public bool IsRoaring => _animator != null && _animator.isActiveAndEnabled
        && (_animator.GetCurrentAnimatorStateInfo(0).IsName("Roar")
            || (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName("Roar")));

    public void PlaySwim(float speed)
    {
        if (_animator == null) return;
        _animator.speed = Mathf.Max(0f, speed);
        _animator.ResetTrigger(RoarId);
        _animator.SetFloat(MoveSpeedId, 1f);
        _animator.CrossFadeInFixedTime("Swim", .15f, 0, 0f);
    }

    public void PlayRoar()
    {
        if (_animator == null) return;
        _animator.speed = 1f;
        _animator.SetFloat(MoveSpeedId, 0f);
        _animator.SetTrigger(RoarId);
    }

    void OnDisable()
    {
        if (_animator == null) return;
        _animator.speed = 1f;
        _animator.ResetTrigger(RoarId);
        _animator.SetFloat(MoveSpeedId, 0f);
    }
}
