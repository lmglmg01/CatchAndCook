using UnityEngine;

public class SweepBossAnimatorHandler : MonoBehaviour
{
    [Header("휩쓸기 애니메이터")]
    [SerializeField] Animator _animator;
    static readonly int PhaseId = Animator.StringToHash("SweepPhase");
    static readonly int SpeedId = Animator.StringToHash("BurrowSpeed");
    static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
    static readonly int DirectionId = Animator.StringToHash("SweepDirection");
    public void SetDirection(float direction)
    {
        if (_animator != null) _animator.SetFloat(DirectionId, direction);
    }
    static readonly int EnterId = Animator.StringToHash("Base Layer.Burrow_Enter");
    static readonly int ExitId = Animator.StringToHash("Base Layer.Burrow_Exit");

    public void SetPhase(int phase, float duration = 0f)
    {
        if (_animator == null) return;
        _animator.applyRootMotion = false;
        _animator.SetFloat(SpeedId, duration > 0f ? 2.2f / duration : 1f);
        _animator.SetInteger(PhaseId, phase);
    }

    public float BurrowProgress(bool exit)
    {
        if (_animator == null || _animator.IsInTransition(0)) return 0f;
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        return state.fullPathHash == (exit ? ExitId : EnterId) ? Mathf.Clamp01(state.normalizedTime) : 0f;
    }

    public void SetMoveSpeed(float speed)
    {
        if (_animator != null) _animator.SetFloat(MoveSpeedId, Mathf.Max(0f, speed));
    }

    public void ResetState()
    {
        if (_animator == null) return;
        SetMoveSpeed(0f);
        SetPhase(0);
        if (_animator.isActiveAndEnabled) _animator.Play("Idle", 0, 0f);
    }

    void OnDisable() => ResetState();
}
