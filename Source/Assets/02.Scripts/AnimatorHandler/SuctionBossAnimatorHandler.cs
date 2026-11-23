using System;
using System.Collections.Generic;
using UnityEngine;

public class SuctionBossAnimatorHandler : MonoBehaviour
{
    [Header("흡입 패턴 애니메이터")]
    [SerializeField] Animator _animator;

    [Header("물기 클립 공격 이벤트")]
    [SerializeField] AnimationClip _biteClip;
    [Tooltip("물기 클립에서 입이 닫히는 시각(초)")]
    [SerializeField, Min(0f)] float _biteAttackTime;

    public event Action AttackRequested;
    public float BiteDuration => _biteClip != null ? _biteClip.length : 0f;
    AnimationClip _eventBiteClip;
    AnimatorOverrideController _biteController;
    RuntimeAnimatorController _originalController;

    // Animator와 같은 오브젝트에서 AnimationEvent를 받는다.
    public void Attack() => AttackRequested?.Invoke();

    void Awake()
    {
        if (_animator == null || _biteClip == null || _animator.runtimeAnimatorController == null) return;
        _originalController = _animator.runtimeAnimatorController;
        _eventBiteClip = Instantiate(_biteClip);
        _eventBiteClip.name = _biteClip.name + " (Suction Attack)";
        _eventBiteClip.hideFlags = HideFlags.HideAndDontSave;
        var events = new List<AnimationEvent>(_eventBiteClip.events);
        events.RemoveAll(e => e.functionName == nameof(Attack));
        events.Add(new AnimationEvent
        {
            time = Mathf.Clamp(_biteAttackTime, 0f, _eventBiteClip.length),
            functionName = nameof(Attack),
            messageOptions = SendMessageOptions.RequireReceiver
        });
        _eventBiteClip.events = events.ToArray();
        _biteController = new AnimatorOverrideController(_originalController);
        _biteController.hideFlags = HideFlags.HideAndDontSave;
        _biteController[_biteClip.name] = _eventBiteClip;
        _animator.runtimeAnimatorController = _biteController;
    }

    void OnDestroy()
    {
        if (_animator != null && _biteController != null && _animator.runtimeAnimatorController == _biteController)
            _animator.runtimeAnimatorController = _originalController;
        if (_biteController != null) Destroy(_biteController);
        if (_eventBiteClip != null) Destroy(_eventBiteClip);
    }

    static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
    static readonly int InhaleStartId = Animator.StringToHash("inhalation");
    static readonly int BiteId = Animator.StringToHash("Bite");
    static readonly int IsInhalingId = Animator.StringToHash("IsInhaling");

    public void SetMoveSpeed(float speed)
    {
        if (_animator != null) _animator.SetFloat(MoveSpeedId, Mathf.Max(0f, speed));
    }

    public void SetInhaling(bool inhaling)
    {
        if (_animator == null) return;
        _animator.SetBool(IsInhalingId, inhaling);
        if (inhaling) _animator.SetTrigger(InhaleStartId);
        else _animator.ResetTrigger(InhaleStartId);
    }

    public void Bite()
    {
        if (_animator == null) return;
        SetInhaling(false);
        _animator.SetTrigger(BiteId);
    }

    public void ResetBite()
    {
        if (_animator != null) _animator.ResetTrigger(BiteId);
    }

    void OnDisable()
    {
        SetInhaling(false);
        ResetBite();
        SetMoveSpeed(0f);
    }
}
