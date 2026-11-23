using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-20)]
public class DiveEntryController : MonoBehaviour
{
    [Header("입수 캐릭터")]
    [SerializeField] UnderSeaHero _hero;
    [SerializeField] AnimatorHandler _animatorHandler;
    [SerializeField] Mover _mover;
    [SerializeField] Rigidbody _body;
    [SerializeField] Transform _visualRoot;

    [Header("입수 경로 (월드 좌표)")]
    [SerializeField] Transform _startPoint;
    [SerializeField] Transform _endPoint;
    [SerializeField] Transform _waterSurface;
    [SerializeField, Min(0.01f)] float _duration;
    [SerializeField] AnimationCurve _horizontalProgress;
    [SerializeField] AnimationCurve _verticalProgress;

    [Header("입수 애니메이션")]
    [SerializeField, Range(0f, 1f)] float _clipStartNormalizedTime;
    [SerializeField, Min(0.01f)] float _clipSpeed;
    [SerializeField, Min(0f)] float _divePoseDuration;
    [SerializeField, Min(0.01f)] float _blendDuration;
    [SerializeField] Vector3 _diveRotation;
    [SerializeField] Vector3 _swimRotation;

    [Header("수면 통과 및 기포")]
    [SerializeField] Collider[] _surfaceBarriers;
    [SerializeField] ParticleSystem _entryBubbles;
    [SerializeField, Min(0)] int _bubbleBurstCount;

    readonly Dictionary<(Collider, Collider), bool> _previousCollisions = new();
    Vector3 _start;
    Vector3 _end;
    float _elapsed;
    bool _enteredWater;
    bool _swimming;
    bool _played;

    public bool IsPlaying { get; private set; }

    public void PlayEntry()
    {
        if (_played || !isActiveAndEnabled) return;
        if (_hero == null || _animatorHandler == null || _mover == null || !_mover.enabled
            || _body == null || _body.isKinematic || _visualRoot == null || _startPoint == null
            || _endPoint == null || _waterSurface == null || _duration <= 0f)
        {
            Debug.LogWarning("입수 연출의 캐릭터·경로·시간 참조를 확인해주세요.", this);
            return;
        }

        _start = _startPoint.position;
        _end = _endPoint.position;
        _start.z = _end.z = _body.position.z;
        _elapsed = 0f;
        _enteredWater = _swimming = false;
        _played = true;
        IsPlaying = true;
        _hero.SetDiveEntryActive(true);
        SetSurfaceCollisionIgnored();
        _body.linearVelocity = Vector3.zero;
        _body.position = _start;
        _hero.transform.position = _start;
        _visualRoot.localRotation = Quaternion.Euler(_diveRotation);
        if (_entryBubbles != null) _entryBubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _animatorHandler.BeginDiveEntry(_clipStartNormalizedTime, _clipSpeed);
    }

    void FixedUpdate()
    {
        if (!IsPlaying) return;
        if (_hero == null || !_hero.isActiveAndEnabled || _elapsed >= _duration)
        {
            FinishEntry();
            return;
        }

        _elapsed = Mathf.Min(_duration, _elapsed + Time.fixedDeltaTime);
        Vector3 target = EvaluatePosition(_elapsed / _duration);
        // Mover가 이 속도를 적용하므로 수면 외의 기존 물리 충돌은 유지된다.
        _mover.SetExternalVelocity(this, (Vector2)(target - _body.position) / Time.fixedDeltaTime);
        if (!_enteredWater && _body.position.y <= _waterSurface.position.y)
        {
            _enteredWater = true;
            GameSfx.Play(GameSoundId.WaterEntry);
            if (_entryBubbles != null)
            {
                _entryBubbles.Play(true);
                _entryBubbles.Emit(_bubbleBurstCount);
            }
        }

        if (!_swimming && _elapsed >= _divePoseDuration)
        {
            _swimming = true;
            _animatorHandler.SwimDiveEntry(_blendDuration);
        }
    }

    void LateUpdate()
    {
        if (!IsPlaying || _visualRoot == null) return;
        float swimBlend = Mathf.Clamp01((_elapsed - _divePoseDuration) / _blendDuration);
        Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(_diveRotation),
            Quaternion.Euler(_swimRotation), Mathf.SmoothStep(0f, 1f, swimBlend));
        float settle = Mathf.Clamp01((_elapsed - (_duration - _blendDuration)) / _blendDuration);
        _visualRoot.localRotation = Quaternion.Slerp(rotation,
            Quaternion.Euler(_diveRotation), Mathf.SmoothStep(0f, 1f, settle));
    }

    Vector3 EvaluatePosition(float time)
    {
        float x = _horizontalProgress != null && _horizontalProgress.length > 0
            ? _horizontalProgress.Evaluate(time) : time;
        float y = _verticalProgress != null && _verticalProgress.length > 0
            ? _verticalProgress.Evaluate(time) : time;
        return new Vector3(Mathf.LerpUnclamped(_start.x, _end.x, x),
            Mathf.LerpUnclamped(_start.y, _end.y, y), _start.z);
    }

    void SetSurfaceCollisionIgnored()
    {
        if (_surfaceBarriers == null) return;
        foreach (Collider heroCollider in _body.GetComponentsInChildren<Collider>())
        {
            if (!heroCollider.enabled || heroCollider.isTrigger) continue;
            foreach (Collider barrier in _surfaceBarriers)
            {
                if (barrier == null || barrier == heroCollider) continue;
                var pair = (heroCollider, barrier);
                if (_previousCollisions.ContainsKey(pair)) continue;
                _previousCollisions.Add(pair, Physics.GetIgnoreCollision(heroCollider, barrier));
                Physics.IgnoreCollision(heroCollider, barrier, true);
            }
        }
    }

    void FinishEntry()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        if (_mover != null) _mover.ClearExternalVelocity(this);
        if (_body != null && !_body.isKinematic) _body.linearVelocity = Vector3.zero;
        foreach (var pair in _previousCollisions)
            if (pair.Key.Item1 != null && pair.Key.Item2 != null)
                Physics.IgnoreCollision(pair.Key.Item1, pair.Key.Item2, pair.Value);
        _previousCollisions.Clear();
        if (_entryBubbles != null) _entryBubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_animatorHandler != null) _animatorHandler.EndDiveEntry(_blendDuration);
        if (_hero != null) _hero.SetDiveEntryActive(false);
    }

    void OnDisable() => FinishEntry();

    void OnDrawGizmosSelected()
    {
        if (_startPoint == null || _endPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_startPoint.position, .3f);
        Gizmos.DrawWireSphere(_endPoint.position, .3f);
        Vector3 start = _startPoint.position;
        Vector3 end = _endPoint.position;
        Vector3 previous = start;
        for (int i = 1; i <= 24; i++)
        {
            float t = i / 24f;
            float x = _horizontalProgress != null && _horizontalProgress.length > 0 ? _horizontalProgress.Evaluate(t) : t;
            float y = _verticalProgress != null && _verticalProgress.length > 0 ? _verticalProgress.Evaluate(t) : t;
            Vector3 point = new Vector3(Mathf.LerpUnclamped(start.x, end.x, x), Mathf.LerpUnclamped(start.y, end.y, y), start.z);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
        if (_waterSurface != null)
            Gizmos.DrawLine(_waterSurface.position + Vector3.left * 3f, _waterSurface.position + Vector3.right * 3f);
    }
}
