using System.Collections;
using UnityEngine;

public class RushBiteBossPattern : BossPattern
{
    [Header("애니메이션 이벤트 공격")]
    [SerializeField] DamageableSensor _attackSensor;
    [SerializeField, Min(0f)] float _biteDamage;
    public enum PatternPhase { Inactive, Preparing, Biting, Returning, Rushing }

    [Header("돌진 물기 애니메이터")]
    [SerializeField] RushBiteBossAnimatorHandler _animatorHandler;
    [Header("패턴 위치 (월드 좌표)")]
    [Tooltip("시작 Y, Z 위치. X는 패턴 시작 시 히어로 위치로 고정")]
    [SerializeField] Transform _startPosition;
    [Tooltip("예고 지점의 Z 위치. Y는 시작 위치로 유지")]
    [SerializeField] Transform _readyBitePoint;
    [Tooltip("물기 지점의 Z 위치. Y는 시작 위치로 유지")]
    [SerializeField] Transform _attackPoint;
    [Tooltip("복귀 Y, Z 위치. X는 패턴 시작 시 고정한 값을 유지")]
    [SerializeField] Transform _returnPosition;
    [Header("이동 속도 (가감속 구간의 평균 속도)")]
    [SerializeField, Min(0f)] float _readyMoveSpeed;
    [Header("돌진 이동 곡선")]
    [SerializeField, Min(.05f)] float _rushDuration;
    [Tooltip("X: 경과 시간 비율, Y: 이동 진행률. 시작(0,0), 끝(1,1), 단조 증가 곡선 사용")]
    [SerializeField] AnimationCurve _rushProgressCurve;
    [SerializeField, Min(0f)] float _returnSpeed;
    [Header("대기 및 물기 시간")]
    [Tooltip("Ready Bite 지점에 도착한 뒤 돌진 전 대기 시간")]
    [SerializeField, Min(0f)] float _prepareDuration;
    [Tooltip("돌진 시작부터 물기 완료까지의 최소 시간")]
    [SerializeField, Min(0f)] float _biteDuration;

    [Header("물기 도착 후 대기")]
    [SerializeField, Min(0f)] float _arrivalWaitDuration;

    [Header("물기 순간 카메라 흔들림")]
    [SerializeField] CameraController _cameraController;
    [SerializeField, Min(0f)] float _biteShakeStrength = .24f;
    [SerializeField, Min(0f)] float _biteShakeDuration = .28f;

    public PatternPhase Phase { get; private set; }
    public event System.Action RushStarted;
    public event System.Action BiteReached;
    public event System.Action RushFinished;
    Coroutine _routine;
    RushBiteBossAnimatorHandler _activeAnimatorHandler;
    bool _attackExecuted;

    // 직접 배치한 콜라이더의 활성 상태는 유지하고, 패턴 상태로 피격 허용 여부를 결정한다.
    protected override bool ManageHurtColliders => false;
    readonly WaitForFixedUpdate _fixedWait = new WaitForFixedUpdate();

    public override void ActivatePattern()
    {
        if (IsActive) return;
        if (Hero == null || _animatorHandler == null
            || _startPosition == null || _readyBitePoint == null || _attackPoint == null || _returnPosition == null
            || _readyMoveSpeed <= 0f || _rushDuration <= 0f || _returnSpeed <= 0f)
        {
            Debug.LogWarning("돌진 물기의 히어로, 애니메이터 핸들러, 시작·예고·물기·복귀 위치와 각 이동 속도를 설정해 주세요.", this);
            return;
        }
        if (!BeginPattern()) return;
        if (_cameraController == null)
            _cameraController = FindFirstObjectByType<CameraController>();
        _attackExecuted = false;
        _activeAnimatorHandler = _animatorHandler;
        _activeAnimatorHandler.AttackRequested += Attack;
        float lockedX = Hero.transform.position.x;
        Vector3 start = new Vector3(lockedX, _startPosition.position.y, _startPosition.position.z);
        Vector3 ready = new Vector3(lockedX, start.y, _readyBitePoint.position.z);
        Vector3 attack = new Vector3(lockedX, start.y, _attackPoint.position.z);
        Vector3 destination = new Vector3(lockedX, _returnPosition.position.y, _returnPosition.position.z);
        SetPosition(start);
        _animatorHandler.ResetState();
        _routine = StartCoroutine(RunPattern(ready, attack, destination));
    }

    IEnumerator RunPattern(Vector3 ready, Vector3 attack, Vector3 destination)
    {
        Phase = PatternPhase.Preparing;
        SetDamageable(false);
        yield return null;
        _animatorHandler.SetMoveSpeed(_readyMoveSpeed);
        yield return MoveTo(ready, _readyMoveSpeed);
        _animatorHandler.SetMoveSpeed(0f);
        if (_prepareDuration > 0f) yield return new WaitForSeconds(_prepareDuration);
        Phase = PatternPhase.Rushing;
        SetDamageable(true);
        float rushDuration = Mathf.Max(.05f, _rushDuration);
        _animatorHandler.Bite(rushDuration);
        RushStarted?.Invoke();
        if (!IsActive) yield break;
        float startedAt = Time.time;
        yield return RushTo(attack, rushDuration);
        Phase = PatternPhase.Biting;
        if (!IsActive) yield break;
        BiteReached?.Invoke();
        if (!IsActive) yield break;
        _animatorHandler.SetMoveSpeed(0f);
        float remaining = _biteDuration - (Time.time - startedAt);
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
        if (_arrivalWaitDuration > 0f) yield return new WaitForSeconds(_arrivalWaitDuration);
        _animatorHandler.EndBite();
        RushFinished?.Invoke();
        if (!IsActive) yield break;
        Phase = PatternPhase.Returning;
        SetDamageable(false);
        _animatorHandler.SetMoveSpeed(_returnSpeed);
        yield return MoveTo(destination, _returnSpeed);
        _routine = null;
        EndPattern();
    }

    IEnumerator RushTo(Vector3 destination, float duration)
    {
        Vector3 start = Position;
        AnimationCurve curve = _rushProgressCurve == null ? null : new AnimationCurve(_rushProgressCurve.keys);
        float from = curve == null ? 0f : curve.Evaluate(0f);
        float to = curve == null ? 1f : curve.Evaluate(1f);
        bool valid = curve != null && curve.length >= 2 && to - from > .0001f;
        float elapsed = 0f;
        float previous = 0f;
        while (elapsed < duration)
        {
            float step = Mathf.Min(Time.fixedDeltaTime, duration - elapsed);
            elapsed += step;
            float t = Mathf.Clamp01(elapsed / duration);
            float value = valid ? (curve.Evaluate(t) - from) / (to - from) : t * t * (3f - 2f * t);
            if (float.IsNaN(value) || float.IsInfinity(value)) value = t;
            float progress = t >= 1f ? 1f : Mathf.Clamp(value, previous, .99999f);
            _animatorHandler.SetMoveSpeed(Vector3.Distance(start, destination) * (progress - previous) / step);
            MovePosition(Vector3.LerpUnclamped(start, destination, progress));
            previous = progress;
            yield return _fixedWait;
        }
        SetPosition(destination);
    }

    IEnumerator MoveTo(Vector3 destination, float speed)
    {
        Vector3 start = Position;
        float duration = Vector3.Distance(start, destination) / speed;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float progress = Phase == PatternPhase.Returning ? 1f - (1f - t) * (1f - t)
                : t * t * (3f - 2f * t);
            MovePosition(Vector3.LerpUnclamped(start, destination, progress));
            yield return _fixedWait;
        }
        SetPosition(destination);
    }

    // RushBite 클립의 입이 닫히는 AnimationEvent에서만 호출한다.
    public void Attack()
    {
        if (!IsActive || _attackExecuted
            || (Phase != PatternPhase.Rushing && Phase != PatternPhase.Biting)) return;
        _attackExecuted = true;
        // 명중 여부와 무관하게 입이 닫히는 애니메이션 이벤트에서 한 번만 흔든다.
        if (_cameraController != null) _cameraController.ShakeCamera(_biteShakeStrength, _biteShakeDuration);
        if (_attackSensor == null || !_attackSensor.isActiveAndEnabled || Hero == null) return;
        Physics.SyncTransforms();
        _attackSensor.AttackOnPlane(_biteDamage, Hero.transform.position.z);
    }

    public override void CancelPattern()
    {
        if (_activeAnimatorHandler != null) _activeAnimatorHandler.AttackRequested -= Attack;
        _activeAnimatorHandler = null;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (IsActive && _animatorHandler != null) _animatorHandler.ResetState();
        Phase = PatternPhase.Inactive;
        base.CancelPattern();
    }

    void LateUpdate() => UpdateHurtColliderPlane(CanReceiveDamage && Phase == PatternPhase.Biting);

    void OnDrawGizmosSelected()
    {
        if (_startPosition == null || _readyBitePoint == null || _attackPoint == null || _returnPosition == null) return;
        float x = IsActive ? Position.x : Hero != null ? Hero.transform.position.x : Position.x;
        Vector3 start = new Vector3(x, _startPosition.position.y, _startPosition.position.z);
        Vector3 ready = new Vector3(x, start.y, _readyBitePoint.position.z);
        Vector3 attack = new Vector3(x, start.y, _attackPoint.position.z);
        Vector3 destination = new Vector3(x, _returnPosition.position.y, _returnPosition.position.z);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(start, .5f);
        Gizmos.DrawLine(start, ready);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(ready, .5f);
        Gizmos.DrawLine(ready, attack);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attack, .5f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(attack, destination);
        Gizmos.DrawWireSphere(destination, .5f);
    }
}
