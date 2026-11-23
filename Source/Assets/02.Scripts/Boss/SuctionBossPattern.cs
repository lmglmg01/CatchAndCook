using System.Collections;
using UnityEngine;

public class SuctionBossPattern : BossPattern
{
    [Header("애니메이션 이벤트 물기 공격")]
    [SerializeField] DamageableSensor _attackSensor;
    [SerializeField, Min(0f)] float _biteDamage;
    public enum PatternPhase { Inactive, MovingIn, Inhaling, Biting, Waiting, MovingOut }

    [Header("시작 위치")]
    [SerializeField] Transform _leftStartPosition;
    [SerializeField] Transform _rightStartPosition;
    [Header("최종 이동 위치")]
    [SerializeField] Transform _leftEndPosition;
    [SerializeField] Transform _rightEndPosition;
    [Header("이동")]
    [SerializeField, Min(0f)] float _moveSpeed;
    [Header("등장·복귀 이동 이펙트")]
    [Tooltip("왼쪽에서 등장·복귀할 때 켤 자식 이펙트")]
    [SerializeField] GameObject _leftMoveEffect;
    [Tooltip("오른쪽에서 등장·복귀할 때 켤 자식 이펙트")]
    [SerializeField] GameObject _rightMoveEffect;
    [Tooltip("이동 먼지의 방출을 멈춘 뒤 남은 입자가 사라지는 최대 시간(초)")]
    [SerializeField, Min(.05f)] float _moveEffectFadeDuration = .45f;
    ParticleSystem.Particle[] _moveEffectParticles;
    [Header("흡입 연결")]
    [SerializeField] SuctionBossAnimatorHandler _animatorHandler;
    [SerializeField] Transform _suctionPoint;
    [Header("흡입 설정")]
    [SerializeField, Min(0f)] float _suctionDuration;
    [SerializeField, Min(0f)] float _suctionRadius;
    [SerializeField, Min(0f)] float _suctionStrength;
    [SerializeField, Min(0f)] float _maxSuctionSpeed;
    [Header("흡입 종료 후 대기")]
    [SerializeField, Min(0f)] float _waitDuration;

    [Header("흡입 중 물기 감지")]
    [SerializeField] HeroDetectionSensor _detectionSensor;
    [Tooltip("물기 감지 콜라이더의 로컬 중심 위치")]
    [SerializeField] Vector3 _biteDetectionCenter;
    [Tooltip("물기 감지 박스의 X, Y, Z 크기")]
    [SerializeField] Vector3 _biteDetectionSize;
    [Header("좌우 공격 지점")]
    [SerializeField] Transform _leftAttackPoint;
    [SerializeField] Transform _rightAttackPoint;
    [Header("물기 이동과 재생 시간")]
    [SerializeField, Min(0f)] float _biteMoveSpeed;
    [SerializeField, Min(0f)] float _biteDuration;

    [Header("입으로 빨려 들어가는 이펙트")]
    [SerializeField] ParticleSystem _suctionEffectPrefab;
    [SerializeField, Min(1f)] float _suctionEffectLength = 24f;
    [SerializeField, Min(0f)] float _suctionEffectWidth = 5f;
    [SerializeField, Min(1f)] float _suctionParticleRate = 55f;
    ParticleSystem _suctionEffect;
    readonly ParticleSystem.Particle[] _suctionParticles = new ParticleSystem.Particle[512];
    float _particleRemainder;

    [Header("카메라 흔들림 — 기존 CameraController")]
    [SerializeField] CameraController _cameraController;
    [Header("시작점 → 이동 지점")]
    [SerializeField, Min(0f)] float _moveInShakeStrength = .045f;
    [Header("흡입 중")]
    [SerializeField, Min(0f)] float _inhaleShakeStrength = .065f;
    [Header("이동 지점 → 시작점")]
    [SerializeField, Min(0f)] float _moveOutShakeStrength = .035f;
    [Header("이동·흡입 흔들림 주기")]
    [SerializeField, Min(.02f)] float _moveShakeDuration = .22f;
    [SerializeField, Min(.02f)] float _moveShakeInterval = .2f;
    [SerializeField, Min(.02f)] float _inhaleShakeDuration = .16f;
    [SerializeField, Min(.02f)] float _inhaleShakeInterval = .12f;
    [Header("히어로 감지 후 물기 — 입이 닫히는 순간")]
    [SerializeField, Min(0f)] float _biteShakeStrength = .18f;
    [SerializeField, Min(0f)] float _biteShakeDuration = .25f;
    float _nextCameraShakeTime;
    float _phaseStartedTime;
    float _moveProgress;

    public PatternPhase Phase { get; private set; }

    Coroutine _routine;
    Mover _heroMover;
    bool _biteRequested;
    bool _attackExecuted;
    bool _startsFromLeft;
    SuctionBossAnimatorHandler _activeAnimatorHandler;
    readonly WaitForFixedUpdate _fixedWait = new WaitForFixedUpdate();

    protected override void OnEnable()
    {
        base.OnEnable();
        SetMoveEffect(false);
    }

    void SetMoveEffect(bool active)
    {
        SetMoveEffect(_leftMoveEffect, active && _startsFromLeft);
        SetMoveEffect(_rightMoveEffect, active && !_startsFromLeft);
    }


    void SetMoveEffect(GameObject effect, bool active)
    {
        if (effect == null) return;
        var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        if (particles.Length == 0)
        {
            effect.SetActive(active);
            return;
        }
        if (active && !effect.activeSelf) effect.SetActive(true);
        foreach (var system in particles)
        {
            if (active)
            {
                if (!system.isEmitting) system.Play(false);
            }
            else
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                FadeMoveParticles(system);
            }
        }
    }

    void FadeMoveParticles(ParticleSystem system)
    {
        int count = system.particleCount;
        if (count == 0) return;
        if (_moveEffectParticles == null || _moveEffectParticles.Length < count)
            _moveEffectParticles = new ParticleSystem.Particle[count];
        count = system.GetParticles(_moveEffectParticles);
        float remainingLimit = Mathf.Max(.05f, _moveEffectFadeDuration)
            * Mathf.Max(.01f, system.main.simulationSpeed);
        for (int i = 0; i < count; i++)
        {
            float remaining = _moveEffectParticles[i].remainingLifetime;
            if (remaining <= remainingLimit) continue;
            // 수명 비율을 유지해 현재 색·크기는 그대로 두고 기존 페이드만 빨리 마친다.
            _moveEffectParticles[i].startLifetime *= remainingLimit / remaining;
            _moveEffectParticles[i].remainingLifetime = remainingLimit;
        }
        system.SetParticles(_moveEffectParticles, count);
    }

    public override void ActivatePattern()
    {
        if (IsActive) return;
        if (Hero == null || _leftStartPosition == null || _rightStartPosition == null
            || _leftEndPosition == null || _rightEndPosition == null
            || _animatorHandler == null || _suctionPoint == null || !(_moveSpeed > 0f)
            || _detectionSensor == null || _leftAttackPoint == null || _rightAttackPoint == null || !(_biteMoveSpeed > 0f))
        {
            Debug.LogWarning("흡입 패턴의 히어로·좌우 위치·애니메이터·흡입점·감지 센서·공격 지점·이동 속도를 설정해 주세요.", this);
            return;
        }
        _heroMover = Hero.GetComponent<Mover>();
        if (_heroMover == null || !BeginPattern()) return;

        if (_cameraController == null)
            _cameraController = FindFirstObjectByType<CameraController>();
        _biteRequested = false;
        _attackExecuted = false;
        _activeAnimatorHandler = _animatorHandler;
        _activeAnimatorHandler.AttackRequested += Attack;
        ApplyDetectionBounds();
        _detectionSensor.SetDetecting(false);
        _detectionSensor.HeroDetected -= OnHeroDetected;
        _detectionSensor.HeroDetected += OnHeroDetected;
        Vector2 heroPosition = Hero.transform.position;
        bool left = ((Vector2)_leftStartPosition.position - heroPosition).sqrMagnitude
            <= ((Vector2)_rightStartPosition.position - heroPosition).sqrMagnitude;
        _startsFromLeft = left;
        Transform start = left ? _leftStartPosition : _rightStartPosition;
        Transform end = left ? _leftEndPosition : _rightEndPosition;
        Transform attack = left ? _leftAttackPoint : _rightAttackPoint;
        Teleport(start);
        _routine = StartCoroutine(RunPattern(start, end, attack));
    }

    IEnumerator RunPattern(Transform start, Transform end, Transform attack)
    {
        SetPhase(PatternPhase.MovingIn);
        SetMoveEffect(true);
        SetDamageable(false);
        _animatorHandler.SetMoveSpeed(_moveSpeed);
        yield return MoveTo(end.position, _moveSpeed);
        _animatorHandler.SetMoveSpeed(0f);

        SetPhase(PatternPhase.Inhaling);
        SetMoveEffect(false);
        SetDamageable(true);
        StartSuctionEffect();
        _animatorHandler.SetInhaling(true);
        _detectionSensor.SetDetecting(true);
        float elapsed = 0f;
        while (elapsed < _suctionDuration && !_biteRequested)
        {
            UpdateSuction();
            yield return _fixedWait;
            elapsed += Time.fixedDeltaTime;
        }
        StopSuction();

        if (_biteRequested)
        {
            SetPhase(PatternPhase.Biting);
            _animatorHandler.Bite();
            _animatorHandler.SetMoveSpeed(_biteMoveSpeed);
            float biteStarted = Time.time;
            yield return MoveTo(attack.position, _biteMoveSpeed);
            if (!IsActive) yield break;
            _animatorHandler.SetMoveSpeed(0f);
            float remaining = Mathf.Max(_biteDuration, _animatorHandler.BiteDuration) - (Time.time - biteStarted);
            if (remaining > 0f) yield return new WaitForSeconds(remaining);
        }
        else
        {
            SetPhase(PatternPhase.Waiting);
            if (_waitDuration > 0f) yield return new WaitForSeconds(_waitDuration);
        }

        SetPhase(PatternPhase.MovingOut);
        SetMoveEffect(true);
        SetDamageable(false);
        _animatorHandler.SetMoveSpeed(_moveSpeed);
        yield return MoveTo(start.position, _moveSpeed);
        _animatorHandler.SetMoveSpeed(0f);
        _routine = null;
        EndPattern();
    }

    IEnumerator MoveTo(Vector3 destination, float speed)
    {
        float distance = Vector3.Distance(Position, destination);
        _moveProgress = 0f;
        while ((Position - destination).sqrMagnitude > .0001f)
        {
            _moveProgress = distance > .001f
                ? Mathf.Clamp01(1f - Vector3.Distance(Position, destination) / distance) : 1f;
            MovePosition(Vector3.MoveTowards(Position, destination, speed * Time.fixedDeltaTime));
            yield return _fixedWait;
        }
        _moveProgress = 1f;
        SetPosition(destination);
    }

    void UpdateSuction()
    {
        if (_heroMover == null || Hero == null) return;
        ApplyDetectionBounds();
        Vector2 delta = (Vector2)_suctionPoint.position - (Vector2)Hero.transform.position;
        Vector2 velocity = delta.sqrMagnitude <= _suctionRadius * _suctionRadius
            ? delta.normalized * Mathf.Min(_suctionStrength, _maxSuctionSpeed)
            : Vector2.zero;
        _heroMover.SetExternalVelocity(this, velocity);
    }

    void ApplyDetectionBounds()
    {
        if (_detectionSensor == null) return;
        Vector3 center = _biteDetectionCenter;
        if (Application.isPlaying && IsActive && Hero != null)
        {
            Vector3 worldCenter = _detectionSensor.transform.TransformPoint(center);
            worldCenter.z = Hero.transform.position.z;
            center = _detectionSensor.transform.InverseTransformPoint(worldCenter);
        }
        _detectionSensor.SetDetectionBounds(center, _biteDetectionSize);
    }

    void OnValidate()
    {
        _biteDetectionSize = Vector3.Max(Vector3.zero, _biteDetectionSize);
        ApplyDetectionBounds();
    }

    void OnDrawGizmosSelected()
    {
        if (_detectionSensor == null) return;
        var collider = _detectionSensor.GetComponent<BoxCollider>();
        if (collider == null) return;
        var previous = Gizmos.matrix;
        Gizmos.matrix = collider.transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(collider.center, collider.size);
        Gizmos.matrix = previous;
    }

    void OnHeroDetected(Hero detected)
    {
        if (!IsActive || Phase != PatternPhase.Inhaling || _biteRequested || detected != Hero) return;
        _biteRequested = true;
        ApplyDetectionBounds();
        _detectionSensor.SetDetecting(false);
    }


    public void Attack()
    {
        if (!IsActive || Phase != PatternPhase.Biting || _attackExecuted) return;
        _attackExecuted = true;
        _cameraController?.ShakeCamera(_biteShakeStrength, _biteShakeDuration);
        if (_attackSensor == null || !_attackSensor.isActiveAndEnabled || Hero == null) return;
        Physics.SyncTransforms();
        _attackSensor.AttackOnPlane(_biteDamage, Hero.transform.position.z);
    }

    void StartSuctionEffect()
    {
        if (_suctionEffectPrefab == null) return;
        if (_suctionEffect == null)
        {
            _suctionEffect = Instantiate(_suctionEffectPrefab, transform);
            _suctionEffect.name = "SuctionFlow";
            _suctionEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var particles in _suctionEffect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = particles.emission; emission.enabled = false;
                var velocity = particles.velocityOverLifetime; velocity.enabled = false;
                var force = particles.forceOverLifetime; force.enabled = false;
                var noise = particles.noise; noise.enabled = false;
                var shape = particles.shape; shape.enabled = false;
                var main = particles.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.stopAction = ParticleSystemStopAction.None;
                main.playOnAwake = false;
                main.gravityModifier = 0f;
                main.maxParticles = _suctionParticles.Length;
            }
        }
        _particleRemainder = 0f;
        _suctionEffect.Play(false);
    }

    void SetPhase(PatternPhase phase)
    {
        Phase = phase;
        _phaseStartedTime = Time.time;
        _nextCameraShakeTime = Time.time;
    }

    void UpdateCameraShake()
    {
        if (!IsActive || _cameraController == null || Time.time < _nextCameraShakeTime) return;
        float strength, duration, interval;
        switch (Phase)
        {
            case PatternPhase.MovingIn:
                strength = _moveInShakeStrength * Mathf.Lerp(.35f, 1f, _moveProgress);
                duration = _moveShakeDuration;
                interval = _moveShakeInterval;
                break;
            case PatternPhase.Inhaling:
                float progress = Mathf.Clamp01((Time.time - _phaseStartedTime) / Mathf.Max(.01f, _suctionDuration));
                strength = _inhaleShakeStrength * Mathf.Lerp(.6f, 1f, progress);
                duration = _inhaleShakeDuration;
                interval = _inhaleShakeInterval;
                break;
            case PatternPhase.MovingOut:
                strength = _moveOutShakeStrength * Mathf.Lerp(1f, .2f, _moveProgress);
                duration = _moveShakeDuration;
                interval = _moveShakeInterval;
                break;
            default:
                return;
        }
        // 짧은 임펄스만 이어 주므로 종료·취소 뒤 긴 흔들림이 남지 않는다.
        // 다른 피격·작살 흔들림을 취소하지 않고 같은 CameraController를 사용한다.
        _cameraController.ShakeCamera(strength, duration);
        _nextCameraShakeTime = Time.time + Mathf.Max(.02f, interval);
    }

    void LateUpdate()
    {
        UpdateCameraShake();
        UpdateHurtColliderPlane(CanReceiveDamage);
        if (!IsActive || Phase != PatternPhase.Inhaling || _suctionEffect == null || _suctionPoint == null) return;
        Vector3 mouth = _suctionPoint.position;
        // 입은 몸체 중심보다 진행 방향 앞에 있으므로 좌우 접근에 맞춰 방출 방향도 반전된다.
        float direction = Mathf.Sign(mouth.x - Position.x);
        if (Mathf.Abs(mouth.x - Position.x) < .01f && Hero != null)
            direction = Mathf.Sign(Hero.transform.position.x - mouth.x);
        float length = Mathf.Min(_suctionEffectLength, _suctionRadius);
        _suctionEffect.transform.position = mouth;
        _particleRemainder += _suctionParticleRate * Time.deltaTime;
        int emitCount = Mathf.FloorToInt(_particleRemainder);
        _particleRemainder -= emitCount;
        for (int i = 0; i < emitCount; i++)
        {
            Vector2 spread = Random.insideUnitCircle * _suctionEffectWidth;
            Vector3 position = mouth + new Vector3(direction * Random.Range(length * .65f, length), spread.x, spread.y * .35f);
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = 3f,
                startSize = Random.Range(.10f, .28f)
            };
            _suctionEffect.Emit(emit, 1);
        }
        int count = _suctionEffect.GetParticles(_suctionParticles);
        for (int i = 0; i < count; i++)
        {
            Vector3 delta = mouth - _suctionParticles[i].position;
            float distance = delta.magnitude;
            float speed = Mathf.Lerp(28f, 12f, Mathf.Clamp01(distance / Mathf.Max(1f, length)));
            if (distance <= speed * Time.deltaTime + .3f) _suctionParticles[i].remainingLifetime = 0f;
            else
            {
                _suctionParticles[i].position += delta / distance * speed * Time.deltaTime;
                _suctionParticles[i].velocity = Vector3.zero;
            }
        }
        _suctionEffect.SetParticles(_suctionParticles, count);
    }

    void StopSuction()
    {
        if (_suctionEffect != null) _suctionEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_detectionSensor != null) _detectionSensor.SetDetecting(false);
        if (_heroMover != null) _heroMover.ClearExternalVelocity(this);
        if (IsActive && _animatorHandler != null) _animatorHandler.SetInhaling(false);
    }

    public override void CancelPattern()
    {
        SetMoveEffect(false);
        if (_activeAnimatorHandler != null) _activeAnimatorHandler.AttackRequested -= Attack;
        _activeAnimatorHandler = null;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        _biteRequested = false;
        if (_detectionSensor != null) _detectionSensor.HeroDetected -= OnHeroDetected;
        StopSuction();
        if (IsActive && _animatorHandler != null)
        {
            _animatorHandler.ResetBite();
            _animatorHandler.SetMoveSpeed(0f);
        }
        SetPhase(PatternPhase.Inactive);
        base.CancelPattern();
    }
}
