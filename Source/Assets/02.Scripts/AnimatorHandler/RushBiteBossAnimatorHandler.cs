using System;
using UnityEngine;

public class RushBiteBossAnimatorHandler : MonoBehaviour
{
    [Header("돌진 물기 애니메이터")]
    [SerializeField] Animator _animator;

    [Header("물기 타이밍")]
    [Tooltip("원본 RushBite 클립에서 턱이 닫히는 시각(초). 이동 도착 시각에 맞춰 재생 속도를 조절")]
    [SerializeField, Min(0f)] float _biteImpactTime;

    public event Action AttackRequested;

    // Animator와 같은 오브젝트가 클립의 Attack 이벤트를 받는다.
    public void Attack() => AttackRequested?.Invoke();

    static readonly int HoldingId = Animator.StringToHash("RushBiteHolding");
    static readonly int BiteSpeedId = Animator.StringToHash("RushBiteSpeed");
    static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
    static readonly int BiteId = Animator.StringToHash("RushBite");

    public void SetMoveSpeed(float speed)
    {
        if (_animator != null) _animator.SetFloat(MoveSpeedId, Mathf.Max(0f, speed));
    }

    public void Bite(float impactDelay)
    {
        if (_animator == null) return;
        float speed = _biteImpactTime > 0f && impactDelay > 0f ? _biteImpactTime / impactDelay : 1f;
        _animator.SetFloat(BiteSpeedId, speed);
        _animator.SetBool(HoldingId, true);
        _animator.SetTrigger(BiteId);
    }

    public void EndBite()
    {
        if (_animator != null) _animator.SetBool(HoldingId, false);
    }

    public void ResetState()
    {
        if (_animator == null) return;
        _animator.SetBool(HoldingId, false);
        _animator.ResetTrigger(BiteId);
        if (_animator.isActiveAndEnabled && _animator.HasState(0, Animator.StringToHash("Base Layer.Idle")))
            _animator.Play("Idle", 0, 0f);
        SetMoveSpeed(0f);
    }

    void OnDisable() => ResetState();
}
