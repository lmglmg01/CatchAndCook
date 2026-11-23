using UnityEngine;

public class AnimatorHandler : MonoBehaviour
{
    static int _moveSpeedId = Animator.StringToHash("MoveSpeed");
    static int _startAimId = Animator.StringToHash("StartAim");
    static int _aimingId = Animator.StringToHash("Aiming");
    static int _meleeAttackId = Animator.StringToHash("MeleeAttack");
    static int _boatNPCInteractId = Animator.StringToHash("BoatNpcInteract");
    static int _boatJumpId = Animator.StringToHash("BoatJump");

    [Header("애니메이터")]
    [SerializeField] Animator _animator;

    [Header("근접 공격 잔상")]
    [SerializeField] TrailRenderer _meleeTrail;
    [SerializeField] Vector2 _meleeTrailNormalizedTime;

    bool _diveEntryPlaying;
    bool _previousRootMotion;
    bool _isAiming;
    bool _meleeAttackRequested;
    bool _meleeTrailEmitting;

    public bool IsMeleeAttacking
    {
        get
        {
            if (!isActiveAndEnabled || _animator == null || !_animator.isActiveAndEnabled) return false;
            int layer = _animator.GetLayerIndex("UpperBody");
            if (layer < 0) return false;
            return _meleeAttackRequested
                || _animator.GetCurrentAnimatorStateInfo(layer).IsName("MeleeAttack")
                || (_animator.IsInTransition(layer)
                    && _animator.GetNextAnimatorStateInfo(layer).IsName("MeleeAttack"));
        }
    }

    void OnEnable()
    {
        ClearMeleeTrail();
        if (_animator == null) return;

        foreach (var parameter in _animator.parameters)
        {
            if (parameter.nameHash != _aimingId || parameter.type != AnimatorControllerParameterType.Bool) continue;
            SetAiming(false);
            break;
        }
    }

    public void BeginDiveEntry(float normalizedTime, float speed)
    {
        if (_animator == null) return;
        SetAiming(false);
        ClearMeleeTrail();
        _diveEntryPlaying = true;
        _previousRootMotion = _animator.applyRootMotion;
        _animator.applyRootMotion = false;
        _animator.SetFloat("DiveEntrySpeed", speed);
        _animator.SetFloat(_moveSpeedId, 0f);
        _animator.Play("Base Layer.DiveEntry", 0, Mathf.Clamp01(normalizedTime));
    }

    public void SwimDiveEntry(float blendDuration)
    {
        if (!_diveEntryPlaying || _animator == null) return;
        _animator.SetFloat(_moveSpeedId, 1f);
        _animator.CrossFadeInFixedTime("Base Layer.Swim", blendDuration, 0);
    }

    public void EndDiveEntry(float blendDuration)
    {
        if (!_diveEntryPlaying) return;
        _diveEntryPlaying = false;
        if (_animator == null) return;
        _animator.applyRootMotion = _previousRootMotion;
        _animator.SetFloat(_moveSpeedId, 0f);
        if (_animator.isActiveAndEnabled)
            _animator.CrossFadeInFixedTime("Base Layer.Idle", blendDuration, 0);
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        if (_diveEntryPlaying) return;
        _animator.SetFloat(_moveSpeedId, moveSpeed);
    }

    public void SetAiming(bool aiming)
    {
        if (_diveEntryPlaying) return;
        _isAiming = aiming;
        _meleeAttackRequested = false;
        _animator.ResetTrigger(_meleeAttackId);
        SetAiming(_animator, aiming);
        int upperBodyLayer = _animator.GetLayerIndex("UpperBody");
        if (upperBodyLayer >= 0)
        {
            _animator.SetLayerWeight(upperBodyLayer, aiming ? 1f : 0f);
        }
    }

    public void SetBoatNPCInteract()
    {
        if (_animator == null) return;

        _animator.SetTrigger(_boatNPCInteractId);
    }

    public void SetBoatJump()
    {
        if (_animator == null) return;
        foreach (var parameter in _animator.parameters)
            if (parameter.nameHash == _boatNPCInteractId) _animator.ResetTrigger(_boatNPCInteractId);
        _animator.SetTrigger(_boatJumpId);
    }

    public void SetMeleeAttack()
    {
        if (_diveEntryPlaying) return;
        _meleeAttackRequested = true;
        _animator.SetTrigger(_meleeAttackId);
        int layer = _animator.GetLayerIndex("UpperBody");
        if (layer >= 0) _animator.SetLayerWeight(layer, 1f);
    }

    void LateUpdate()
    {
        if (_animator == null || _diveEntryPlaying) return;
        int layer = _animator.GetLayerIndex("UpperBody");
        if (layer < 0) return;

        var state = _animator.GetCurrentAnimatorStateInfo(layer);
        if (_animator.IsInTransition(layer))
        {
            var nextState = _animator.GetNextAnimatorStateInfo(layer);
            if (nextState.IsName("MeleeAttack")) state = nextState;
        }
        bool attacking = state.IsName("MeleeAttack");
        if (attacking) _meleeAttackRequested = false;
        _animator.SetLayerWeight(layer, _isAiming || attacking || _meleeAttackRequested ? 1f : 0f);
        SetMeleeTrail(attacking
            && state.normalizedTime >= _meleeTrailNormalizedTime.x
            && state.normalizedTime < _meleeTrailNormalizedTime.y);
    }

    void SetMeleeTrail(bool emitting)
    {
        if (_meleeTrail == null) return;
        // 이전 공격의 잔상과 새 칼 궤적이 연결되지 않게 한다.
        if (emitting && !_meleeTrailEmitting) _meleeTrail.Clear();
        _meleeTrail.emitting = emitting;
        _meleeTrailEmitting = emitting;
    }

    void ClearMeleeTrail()
    {
        SetMeleeTrail(false);
        if (_meleeTrail != null) _meleeTrail.Clear();
        _meleeTrailEmitting = false;
    }

    void OnDisable()
    {
        EndDiveEntry(0f);
        ClearMeleeTrail();
    }

    public static void SetAiming(Animator animator, bool aiming)
    {
        animator.SetBool(_aimingId, aiming);
        if (aiming)
        {
            animator.SetTrigger(_startAimId);
        }
        else
        {
            animator.ResetTrigger(_startAimId);
        }
    }
}
