using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(300)]
public class SweepBossPattern : BossPattern
{
    public enum StartSide { Random, Left, Right }
    public enum PatternPhase { Inactive, Ready, BurrowEnter, HiddenRelocate, BurrowExit, SweepWindup, Sweeping, SweepRecovery, BurrowRetreat, MovingOffscreen, TailFlail, RetreatZ, TurningToBackground }
    [Header("좌우 시작 위치와 방향")]
    [SerializeField] Transform _leftStartPosition;
    [SerializeField] Transform _rightStartPosition;
    [SerializeField] StartSide _startSide;
    [Header("좌우 도착 위치 — 도면 모드는 머리 기준")]
    [SerializeField] Transform _leftEndPosition;
    [SerializeField] Transform _rightEndPosition;
    [Header("횡단 시간과 곡선")]
    [SerializeField, Min(.1f)] float _sweepDuration;
    [SerializeField] AnimationCurve _sweepMovementCurve;
    [SerializeField, Min(0f)] float _endMargin;
    [SerializeField, Min(0f)] float _sweepWindupDuration;
    [Header("땅파기 시간")]
    [SerializeField, Min(.1f)] float _burrowEnterDuration;
    [SerializeField, Min(.1f)] float _burrowExitDuration;
    [Header("바닥과 가림")]
    [SerializeField] Transform _floorPoint;
    [SerializeField] Renderer _burrowOcclusionRenderer;
    [SerializeField] Renderer[] _modelRenderers;
    [Header("휩쓸기 애니메이터")]
    [SerializeField] SweepBossAnimatorHandler _animatorHandler;
    [Header("박힌 상태 전용 전체 피격")]
    [SerializeField] CapsuleCollider[] _buriedHurtColliders;
    [SerializeField, Min(0f)] float _buriedHitPadding;
    [Header("몸통 접촉 공격 — 피격 대상 제외")]
    [SerializeField] Collider[] _bodyContactColliders;
    [Header("지형 접촉 더스트")]
    [SerializeField] LayerMask _terrainLayers = 1;
    [SerializeField] ParticleSystem _dustEffectPrefab;
    [Header("보스 접촉 먼지 크기")]
    [SerializeField, Min(.1f)] float _dustScale;
    [SerializeField, Min(.05f)] float _dustInterval = .15f;
    readonly Collider[] _terrainHits = new Collider[32];
    ParticleSystem[] _dustEffects;
    float _nextDustTime;
    [SerializeField, Min(0f)] float _sweepDamage;
    [Header("모래·기포 이벤트 위치")]
    [SerializeField] Transform _burrowVfxSpawnPoint;

    [Header("휩쓸기 피격 평면 보정")]
    [SerializeField] float _modelPlaneOffset;
    [SerializeField, Min(0f)] float _maximumDepthDifference;


    struct BuriedHitboxPose
    {
        public CapsuleCollider collider;
        public Transform parent;
        public Vector3 localPosition, localScale, center;
        public Quaternion localRotation;
        public Matrix4x4 localMatrix;
        public float radius, height;
        public int direction;
    }
    BuriedHitboxPose[] _buriedHitboxPoses;

    [Header("휩쓸기·복귀 카메라 흔들림")]
    [SerializeField] CameraController _cameraController;
    [SerializeField, Min(0f)] float _sweepShakeStrength = .07f;
    [SerializeField, Min(0f)] float _retreatShakeStrength = .09f;
    [SerializeField, Min(.02f)] float _cameraShakeDuration = .22f;
    [SerializeField, Min(.02f)] float _cameraShakeInterval = .18f;
    float _nextCameraShakeTime;
    float _phaseStartedTime;

    public PatternPhase Phase { get; private set; }
    public override bool CanAttachHarpoon => false;
    public float CombatPlaneZ => _combatPlaneZ;
    public bool AttackActive => IsActive && Phase == PatternPhase.Sweeping;
    public event Action<Vector3> BurrowStarted;
    public event Action<Vector3> EmergenceStarted;
    public event Action<PatternPhase> PhaseChanged;
    public event Action SweepStarted;
    public event Action SweepFinished;
    [Header("꼬리 노출과 반복")]
    [SerializeField] Transform _headAnchor;
    [SerializeField] Transform _tailAnchor;
    [SerializeField, Min(0f)] float _buriedHeadDepth;
    [SerializeField, Min(0f)] float _tailFlailDuration;
    [Header("배경 후퇴")]
    [SerializeField, Min(.1f)] float _retreatDuration;
    [SerializeField, Min(0f)] float _retreatDistance;
    [SerializeField, Min(0f)] float _retreatTurnDuration;
    [SerializeField] AnimationCurve _retreatMovementCurve;
    [Header("탑뷰 도면 경로 — 기존 본 추종")]
    [SerializeField] SweepBossPathPose _pathPose;
    [SerializeField, Min(.1f)] float _backgroundTurnDuration = 1.1f;
    [SerializeField, Min(1f)] float _backgroundTurnRadius = 10f;
    [SerializeField, Min(0f)] float _backgroundDiveDistance = 8f;
    [SerializeField, Min(0f)] float _referenceBuriedDepth = 12f;
    [Header("현재 위치에서 대각선 잠행")]
    [SerializeField, Range(60f, 90f)] float _burrowDiveAngle = 90f;
    [SerializeField, Min(8f)] float _burrowBendRadius = 18f;
    [SerializeField, Range(.5f, .85f)] float _buriedBodyFraction = .75f;
    [SerializeField, Min(0f)] float _sweepWaveAmplitude = 3f;
    [SerializeField, Min(1f)] float _sweepWaveLength = 26f;
    float _combatPlaneZ;
    Coroutine _routine;
    readonly WaitForFixedUpdate _fixedWait = new WaitForFixedUpdate();
    Collider[] _colliders;
    bool[] _colliderEnabled;
    bool[] _rendererEnabled;
    bool _cached;

    public override void ActivatePattern()
    {
        if (IsActive) return;
        if (_leftStartPosition == null || _rightStartPosition == null || _leftEndPosition == null
            || _rightEndPosition == null || _floorPoint == null || _animatorHandler == null
            || _headAnchor == null || _tailAnchor == null || !(_retreatDuration > 0f)
            || _modelRenderers == null || _modelRenderers.Length == 0
            || !(_sweepDuration > 0f) || !(_burrowEnterDuration > 0f) || !(_burrowExitDuration > 0f))
        { Debug.LogWarning("휩쓸기 패턴의 위치·바닥·렌더러·애니메이터·시간을 연결해 주세요.", this); return; }
        if (!BeginPattern()) return;
        if (_cameraController == null)
            _cameraController = FindFirstObjectByType<CameraController>();
        CacheBuriedHitboxPoses();
        RefreshHurtboxes();
        CacheState();
        bool left = _startSide == StartSide.Left || (_startSide == StartSide.Random && UnityEngine.Random.Range(0, 2) == 0);
        Transform start = left ? _leftStartPosition : _rightStartPosition;
        Vector3 end = left ? _rightEndPosition.position : _leftEndPosition.position;
        Vector3 delta = end - start.position;
        end -= delta.normalized * Mathf.Min(_endMargin, delta.magnitude);
        SetCollisions(false);
        // 재활성화 시 이전 지하 위치에서 지상으로 튀어나오지 않도록 숨긴다.
        SetVisible(false);
        _animatorHandler.ResetState();
        _routine = StartCoroutine(_pathPose != null ? RunReferencePattern(start, end) : RunPattern(start, end));
    }

    void CacheState()
    {
        var body = _animatorHandler.GetComponentInParent<Rigidbody>();
        _colliders = body != null ? body.GetComponentsInChildren<Collider>(true) : GetComponentsInChildren<Collider>(true);
        _colliderEnabled = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++) _colliderEnabled[i] = _colliders[i].enabled;
        _rendererEnabled = new bool[_modelRenderers.Length];
        for (int i = 0; i < _modelRenderers.Length; i++) _rendererEnabled[i] = _modelRenderers[i] != null && _modelRenderers[i].enabled;
        _cached = true;
    }

    IEnumerator RunPattern(Transform start, Vector3 end)
    {
        SetPhase(PatternPhase.Ready);
        Teleport(start);
        var body = _animatorHandler.GetComponentInParent<Rigidbody>();
        Quaternion facing = Quaternion.Euler(0f, end.x >= start.position.x ? 0f : 180f, 0f);
        if (body != null) body.rotation = facing;
        _combatPlaneZ = start.position.z;
        _animatorHandler.SetPhase(4);
        SetVisible(true);
        if (_sweepWindupDuration > 0f) yield return new WaitForSeconds(_sweepWindupDuration);
        SetPhase(PatternPhase.Sweeping);
        SetHitboxes(true);
        SweepStarted?.Invoke();
        if (!IsActive) yield break;
        yield return SweepTo(end);
        if (!IsActive) yield break;
        SetHitboxes(false);
        _animatorHandler.SetMoveSpeed(0f);
        SweepFinished?.Invoke();
        if (!IsActive) yield break;
        Vector3 surface = Position;
        SetPhase(PatternPhase.BurrowEnter);
        yield return Burrow(false, surface);
        if (!IsActive) yield break;
        SetPhase(PatternPhase.TailFlail);
        _animatorHandler.SetPhase(5);
        float elapsed = 0f;
        while (IsActive && elapsed < _tailFlailDuration)
        {
            MoveOnTailPlane(Position);
            elapsed += Time.fixedDeltaTime;
            yield return _fixedWait;
        }
        if (!IsActive) yield break;
        SetPhase(PatternPhase.BurrowExit);
        yield return Burrow(true, surface);
        if (!IsActive) yield break;
        SetPhase(PatternPhase.RetreatZ);
        _animatorHandler.SetPhase(6);
        yield return Retreat();
        if (!IsActive) yield break;
        _routine = null;
        EndPattern();
    }

    IEnumerator Burrow(bool exit, Vector3 surface)
    {
        float duration = exit ? _burrowExitDuration : _burrowEnterDuration;
        Vector3 from = Position;
        Vector3 to = exit ? surface : new Vector3(from.x,
            _floorPoint.position.y - _buriedHeadDepth - (_headAnchor.position.y - from.y), from.z);
        _animatorHandler.SetMoveSpeed(0f);
        _animatorHandler.SetPhase(exit ? 3 : 1, duration);
        Vector3 effect = _burrowVfxSpawnPoint != null ? _burrowVfxSpawnPoint.position : from;
        effect.y = _floorPoint.position.y;
        if (exit) EmergenceStarted?.Invoke(effect); else BurrowStarted?.Invoke(effect);
        float timeout = duration + 3f;
        float previous = 0f;
        while (IsActive)
        {
            float t = Mathf.Max(previous, _animatorHandler.BurrowProgress(exit));
            previous = t;
            float u = BurrowTravelProgress(exit, t);
            MoveOnTailPlane(Vector3.LerpUnclamped(from, to, u));
            if (t >= 1f) break;
            timeout -= Time.fixedDeltaTime;
            if (timeout <= 0f) { Debug.LogError("땅파기 Animator 전환을 확인해 주세요.", this); EndPattern(); yield break; }
            yield return _fixedWait;
        }
        yield return _fixedWait;
    }

    // Normalized timings match Burrow_Enter/Exit (2.2 s). Bones bend first,
    // then the rigidbody strikes; emergence lets the head lead the trailing body.
    static float BurrowTravelProgress(bool exit, float t)
    {
        t = Mathf.Clamp01(t);
        if (exit)
        {
            // A small bracing motion, followed by a continuous pull out.
            if (t < .15f) return -.025f * Mathf.Sin(Mathf.PI * t / .15f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.15f, .82f, t));
        }
        if (t < .15f) return -.055f * Mathf.SmoothStep(0f, 1f, t / .15f);
        if (t < .43f)
        {
            float strike = Mathf.InverseLerp(.15f, .43f, t);
            return Mathf.LerpUnclamped(-.055f, 1f, strike * strike);
        }
        // Brief recoil at contact; keep the head planted during the body follow-through.
        return 1f - .035f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.43f, .64f, t));
    }

    void MoveOnTailPlane(Vector3 position)
    {
        // 이동은 Rigidbody가 담당하고, 클립은 기존 본의 회전만 담당한다.
        position.z = Position.z + _combatPlaneZ - _tailAnchor.position.z;
        MovePosition(position);
    }

    IEnumerator Retreat()
    {
        Vector3 from = Position;
        Vector3 to = from + Vector3.forward * _retreatDistance;
        var body = _animatorHandler.GetComponentInParent<Rigidbody>();
        Quaternion initialRotation = body != null ? body.rotation : Quaternion.identity;
        Quaternion retreatRotation = Quaternion.Euler(0f, 270f, 0f);
        float elapsed = 0f, previous = 0f;
        while (IsActive && elapsed < _retreatDuration)
        {
            float step = Mathf.Min(Time.fixedDeltaTime, _retreatDuration - elapsed);
            elapsed += step;
            float t = Mathf.Clamp01(elapsed / _retreatDuration);
            float value = _retreatMovementCurve == null ? Mathf.SmoothStep(0, 1, t) : _retreatMovementCurve.Evaluate(t);
            if (float.IsNaN(value) || float.IsInfinity(value)) value = t;
            float progress = t >= 1f ? 1f : Mathf.Clamp(value, previous, .99999f);
            _animatorHandler.SetMoveSpeed(Mathf.Abs(_retreatDistance) * (progress - previous) / step);
            if (body != null)
            {
                float turn = _retreatTurnDuration > 0f ? Mathf.Clamp01(elapsed / _retreatTurnDuration) : 1f;
                body.MoveRotation(Quaternion.Slerp(initialRotation, retreatRotation, Mathf.SmoothStep(0, 1, turn)));
            }
            MovePosition(Vector3.Lerp(from, to, progress));
            previous = progress;
            yield return _fixedWait;
        }
        SetPosition(to);
    }

    IEnumerator SweepTo(Vector3 end)
    {
        Vector3 start = Position;
        var curve = _sweepMovementCurve == null ? null : new AnimationCurve(_sweepMovementCurve.keys);
        float elapsed = 0f, previous = 0f;
        while (elapsed < _sweepDuration)
        {
            float step = Mathf.Min(Time.fixedDeltaTime, _sweepDuration - elapsed);
            elapsed += step;
            float t = Mathf.Clamp01(elapsed / _sweepDuration);
            float value = curve == null ? t * t * (3f - 2f * t) : curve.Evaluate(t);
            if (float.IsNaN(value) || float.IsInfinity(value)) value = t;
            float progress = t >= 1f ? 1f : Mathf.Clamp(value, previous, .99999f);
            _animatorHandler.SetMoveSpeed(Vector3.Distance(start, end) * (progress - previous) / step);
            MovePosition(Vector3.Lerp(start, end, progress));
            previous = progress;
            yield return _fixedWait;
        }
        SetPosition(end);
    }

    public void TryHit(Collider target)
    {
        // Interaction/aim sensors extend beyond the hero's body and must not
        // cause contact damage before the character actually touches the boss.
        if (!AttackActive || target == null || target.isTrigger || Hero == null) return;
        if (target.GetComponentInParent<Hero>() != Hero) return;
        // 반복 접촉의 피해 간격은 HeroRunModel의 피격 무적 시간이 결정한다.
        DamageableSensor.TryApplyHit(target, _sweepDamage, target.ClosestPoint(Position));
    }

    void SetBuriedDamageable(bool active)
    {
        SetDamageable(active, Array.Empty<Collider>());
        SetBuriedHurtboxes(active);
    }

    void UpdateCameraShake()
    {
        if (!IsActive || _cameraController == null || Time.time < _nextCameraShakeTime) return;
        float elapsed = Time.time - _phaseStartedTime;
        float strength;
        if (Phase == PatternPhase.Sweeping || Phase == PatternPhase.BurrowEnter)
        {
            // 횡단 후에도 몸통이 땅에 박힐 때까지 같은 강도로 이어진다.
            float ramp = Phase == PatternPhase.BurrowEnter ? 1f : Mathf.SmoothStep(0f, 1f, elapsed / .35f);
            strength = _sweepShakeStrength * Mathf.Lerp(.35f, 1f, ramp);
        }
        else if (Phase == PatternPhase.BurrowRetreat || Phase == PatternPhase.RetreatZ)
        {
            // 꼬리가 사라지며 후퇴하는 동안 점차 약해진다.
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.1f, _retreatDuration));
            strength = _retreatShakeStrength * Mathf.Lerp(1f, .2f, progress);
        }
        else return;
        _cameraController.ShakeCamera(strength, _cameraShakeDuration);
        _nextCameraShakeTime = Time.time + Mathf.Max(.02f, _cameraShakeInterval);
    }

    void LateUpdate()
    {
        if (!IsActive) return;
        UpdateCameraShake();
        RefreshHurtboxes();
        if (Phase == PatternPhase.TailFlail) RefreshBuriedHurtboxes();
        // 본의 최종 포즈 이후 실제 캐릭터 콜라이더와 접촉을 판정한다.
        CheckHeroContacts(_bodyContactColliders);
        UpdateTerrainDust();
    }


    // BodyContact의 자식 캡슐만 히어로 평면에 맞춘다. 모델·공격·이펙트 콜라이더는 이동하지 않는다.
    void RefreshHurtboxes()
    {
        if (Hero == null || HurtColliders == null) return;
        foreach (var hit in HurtColliders)
        {
            if (!(hit is CapsuleCollider capsule) || hit.transform.parent == null) continue;
            var source = hit.transform.parent.GetComponent<CapsuleCollider>();
            if (source == null) continue;
            capsule.center = source.center;
            capsule.direction = source.direction;
            capsule.radius = source.radius;
            capsule.height = source.height;
            hit.transform.localRotation = Quaternion.identity;
            hit.transform.localScale = Vector3.one;
            Vector3 center = source.transform.TransformPoint(source.center);
            hit.transform.position = source.transform.position
                + Vector3.forward * (Hero.transform.position.z - center.z);
        }
    }

    protected override bool IsHurtColliderInRange(Collider hit)
    {
        if (Hero == null) return false;
        if (Phase == PatternPhase.TailFlail) return IsRegisteredHurtCollider(hit);
        if (hit.transform.parent == null) return false;
        var source = hit.transform.parent.GetComponent<CapsuleCollider>();
        if (source == null) return false;
        float depth = source.transform.TransformPoint(source.center).z;
        return Mathf.Abs(depth - (Hero.transform.position.z + _modelPlaneOffset)) <= _maximumDepthDifference;
    }


    protected override bool IsRegisteredHurtCollider(Collider hit)
    {
        return Phase == PatternPhase.TailFlail
            ? _buriedHurtColliders != null && Array.IndexOf(_buriedHurtColliders, hit) >= 0
            : base.IsRegisteredHurtCollider(hit);
    }

    void SetBuriedHurtboxes(bool active)
    {
        if (_buriedHurtColliders == null) return;
        if (active) RefreshBuriedHurtboxes();
        foreach (var hit in _buriedHurtColliders)
            if (hit != null) hit.enabled = active && IsActive;
    }

    Vector3 ProjectToHeroPlane(Vector3 point, Camera camera)
    {
        float z = Hero.transform.position.z;
        if (camera != null && camera.WorldToViewportPoint(point).z > 0f)
        {
            Ray ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(point));
            if (Mathf.Abs(ray.direction.z) > .0001f)
                point = ray.GetPoint((z - ray.origin.z) / ray.direction.z);
        }
        point.z = z;
        return point;
    }

    float ProjectRadius(Vector3 center, float radius, Camera camera)
    {
        if (camera == null) return radius;
        Vector3 projected = ProjectToHeroPlane(center, camera);
        return Mathf.Max(Vector3.Distance(projected, ProjectToHeroPlane(center + camera.transform.right * radius, camera)),
            Vector3.Distance(projected, ProjectToHeroPlane(center + camera.transform.up * radius, camera)));
    }


    void CacheBuriedHitboxPoses()
    {
        if (_buriedHurtColliders == null) return;
        _buriedHitboxPoses = new BuriedHitboxPose[_buriedHurtColliders.Length];
        for (int i = 0; i < _buriedHurtColliders.Length; i++)
        {
            var hit = _buriedHurtColliders[i];
            if (hit == null) continue;
            var t = hit.transform;
            _buriedHitboxPoses[i] = new BuriedHitboxPose
            {
                collider = hit, parent = t.parent,
                localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale,
                localMatrix = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale),
                center = hit.center, radius = hit.radius, height = hit.height, direction = hit.direction
            };
        }
    }

    void RestoreBuriedHitboxPoses()
    {
        if (_buriedHitboxPoses == null) return;
        foreach (var pose in _buriedHitboxPoses)
        {
            var hit = pose.collider;
            if (hit == null) continue;
            hit.enabled = false;
            hit.transform.localPosition = pose.localPosition;
            hit.transform.localRotation = pose.localRotation;
            hit.transform.localScale = pose.localScale;
            hit.center = pose.center;
            hit.radius = pose.radius;
            hit.height = pose.height;
            hit.direction = pose.direction;
        }
        _buriedHitboxPoses = null;
    }

    // 직접 배치한 각 콜라이더의 로컬 모양을 사용한다. 몸통 배열의 순서와 대응시키지 않는다.
    void RefreshBuriedHurtboxes()
    {
        if (Hero == null || _buriedHurtColliders == null) return;
        bool changed = _buriedHitboxPoses == null || _buriedHitboxPoses.Length != _buriedHurtColliders.Length;
        if (!changed)
            for (int i = 0; i < _buriedHurtColliders.Length; i++)
                if (_buriedHitboxPoses[i].collider != _buriedHurtColliders[i]) { changed = true; break; }
        if (changed) { RestoreBuriedHitboxPoses(); CacheBuriedHitboxPoses(); }
        Camera camera = Camera.main;
        foreach (var pose in _buriedHitboxPoses)
        {
            var hit = pose.collider;
            if (hit == null) continue;
            Matrix4x4 matrix = pose.parent != null ? pose.parent.localToWorldMatrix * pose.localMatrix : pose.localMatrix;
            Vector3 scale = new Vector3(matrix.MultiplyVector(Vector3.right).magnitude,
                matrix.MultiplyVector(Vector3.up).magnitude, matrix.MultiplyVector(Vector3.forward).magnitude);
            int d = pose.direction;
            float radius = pose.radius * Mathf.Max(scale[(d + 1) % 3], scale[(d + 2) % 3]);
            float halfSegment = Mathf.Max(0f, pose.height * scale[d] * .5f - radius);
            Vector3 localAxis = d == 0 ? Vector3.right : d == 1 ? Vector3.up : Vector3.forward;
            Vector3 center = matrix.MultiplyPoint3x4(pose.center);
            Vector3 axis = matrix.MultiplyVector(localAxis).normalized * halfSegment;
            Vector3 a = ProjectToHeroPlane(center - axis, camera);
            Vector3 b = ProjectToHeroPlane(center + axis, camera);
            float projectedRadius = Mathf.Max(ProjectRadius(center - axis, radius, camera),
                ProjectRadius(center + axis, radius, camera)) + _buriedHitPadding;
            Vector3 direction = b - a;
            hit.transform.SetPositionAndRotation((a + b) * .5f,
                direction.sqrMagnitude > .000001f ? Quaternion.FromToRotation(Vector3.up, direction) : Quaternion.identity);
            Vector3 parentScale = hit.transform.parent != null ? hit.transform.parent.lossyScale : Vector3.one;
            hit.transform.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
            hit.center = Vector3.zero;
            hit.direction = 1;
            hit.radius = Mathf.Max(.01f, projectedRadius);
            hit.height = direction.magnitude + hit.radius * 2f;
        }
    }

    void CheckHeroContacts(Collider[] contacts)
    {
        if (!IsActive || !AttackActive || HeroColliders == null || contacts == null) return;
        foreach (var hit in contacts)
        {
            if (hit == null || !hit.enabled || !hit.gameObject.activeInHierarchy) continue;
            foreach (var hero in HeroColliders)
            {
                if (hero == null || !hero.enabled || hero.isTrigger || !hero.gameObject.activeInHierarchy) continue;
                Vector3 attackPosition = hit.transform.position;
                // Keep contact damage in the hero plane without moving the model's colliders.
                attackPosition.z += Hero.transform.position.z - hit.bounds.center.z;
                if (Physics.ComputePenetration(hit, attackPosition, hit.transform.rotation,
                    hero, hero.transform.position, hero.transform.rotation, out _, out _))
                {
                    TryHit(hero);
                    if (!IsActive) return;
                }
            }
        }
    }

    void SetBodyContacts(bool active)
    {
        if (_bodyContactColliders == null) return;
        foreach (var collider in _bodyContactColliders)
            if (collider != null) collider.enabled = active;
    }


    void UpdateTerrainDust()
    {
        if (_dustEffectPrefab == null) { StopDust(); return; }
        if (Phase == PatternPhase.Inactive || Phase == PatternPhase.Ready || Phase == PatternPhase.RetreatZ)
        { StopDust(false); return; }
        if (Time.time < _nextDustTime) return;
        _nextDustTime = Time.time + _dustInterval;
        if (_dustEffects == null) _dustEffects = new ParticleSystem[4];
        int used = 0;
        bool burial = Phase == PatternPhase.BurrowEnter || Phase == PatternPhase.TailFlail
            || Phase == PatternPhase.BurrowRetreat || Phase == PatternPhase.BurrowExit;
        if (burial)
        {
            // 지하의 캡슐 접점 대신 몸통과 가림 지면의 경계에서 방출한다.
            if (_floorPoint != null && _pathPose != null && _pathPose.TryGetBurialContact(_floorPoint.position.y, out var point))
                PlayTerrainDust(used++, point, Vector3.up);
        }
        else if (EffectColliders != null)
        foreach (var body in EffectColliders)
        {
            if (body == null || !body.gameObject.activeInHierarchy) continue;
            // 피격·공격 배열과 별개로, Inspector에 지정한 이펙트 전용 형상만 사용한다.
            Vector3 localCenter, localExtents;
            if (body is CapsuleCollider capsule)
            {
                localCenter = capsule.center;
                localExtents = Vector3.one * capsule.radius;
                localExtents[capsule.direction] = Mathf.Max(capsule.radius, capsule.height * .5f);
            }
            else if (body is BoxCollider box)
            { localCenter = box.center; localExtents = box.size * .5f; }
            else continue;
            Vector3 center = body.transform.TransformPoint(localCenter);
            Vector3 x = body.transform.TransformVector(Vector3.right * localExtents.x);
            Vector3 y = body.transform.TransformVector(Vector3.up * localExtents.y);
            Vector3 z = body.transform.TransformVector(Vector3.forward * localExtents.z);
            Vector3 extents = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            int count = Physics.OverlapBoxNonAlloc(center, extents, _terrainHits,
                Quaternion.identity, _terrainLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && used < _dustEffects.Length; i++)
            {
                var terrain = _terrainHits[i];
                if (terrain == null || terrain.attachedRigidbody != null || terrain.transform.IsChildOf(transform)) continue;
                if (!Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    terrain, terrain.transform.position, terrain.transform.rotation, out var normal, out var depth)) continue;
                // 관통을 해소한 중심에서 지형의 실제 표면 위치를 얻는다.
                Vector3 point = terrain.ClosestPoint(center + normal * (depth + .02f));
                bool duplicate = false;
                for (int j = 0; j < used; j++)
                    if ((_dustEffects[j].transform.position - point).sqrMagnitude < 4f) { duplicate = true; break; }
                if (duplicate) continue;
                PlayTerrainDust(used, point, normal);
                used++;
            }
            if (used == _dustEffects.Length) break;
        }
        for (int i = used; i < _dustEffects.Length; i++)
            if (_dustEffects[i] != null) _dustEffects[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }


    void PlayTerrainDust(int index, Vector3 point, Vector3 normal)
    {
        var effect = _dustEffects[index];
        if (effect == null)
        {
            // 패턴 오브젝트가 꺼져도 남은 먼지가 수명대로 사라지도록 공통 부모 아래에 둔다.
            effect = Instantiate(_dustEffectPrefab, transform.parent);
            effect.name = "TerrainContactDust";
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.stopAction = ParticleSystemStopAction.None;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, 1.2f);
                main.startSizeMultiplier *= .45f * Mathf.Max(.1f, _dustScale);
                var shape = particles.shape;
                shape.scale = new Vector3(1.4f, .4f, 1.4f) * Mathf.Max(.1f, _dustScale);
                var emission = particles.emission;
                emission.rateOverTimeMultiplier *= .35f;
            }
            _dustEffects[index] = effect;
        }
        effect.transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, normal));
        effect.transform.localScale = Vector3.one;
        if (!effect.isEmitting) effect.Play(true);
    }

    void OnDestroy()
    {
        if (_dustEffects == null) return;
        foreach (var effect in _dustEffects)
            if (effect != null) Destroy(effect.gameObject);
    }

    void StopDust(bool clear = true)
    {
        if (_dustEffects == null) return;
        foreach (var effect in _dustEffects)
            if (effect != null) effect.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
    }

    void MovePathHead(Vector3 head)
    {
        MovePosition(Position + head - _pathPose.HeadTarget);
        _pathPose.SetHead(head);
    }

    IEnumerator RunReferencePattern(Transform start, Vector3 end)
    {
        SetPhase(PatternPhase.Ready);
        Teleport(start);
        var body = _animatorHandler.GetComponentInParent<Rigidbody>();
        float direction = end.x >= start.position.x ? 1f : -1f;
        _animatorHandler.SetDirection(direction);
        if (body != null)
        {
            Quaternion facing = Quaternion.Euler(0, direction > 0 ? 0 : 180, 0);
            // Interpolated rigidbodies can retain the previous rendered transform for a frame.
            // Seed the trail only after the visual hierarchy is at this spawn pose.
            body.position = start.position; body.rotation = facing;
            body.transform.SetPositionAndRotation(start.position, facing);
            Physics.SyncTransforms();
        }
        _combatPlaneZ = start.position.z;
        _animatorHandler.SetPhase(4);
        var animator = _animatorHandler.GetComponent<Animator>();
        if (animator != null) { animator.Play("S_Sweep", 0, 0); animator.Update(0); }
        // Fit complete waves between the authored endpoints. The head crosses the
        // same fixed centreline at both ends, with a mirrored path from either side.
        float plannedDistance = Mathf.Abs(end.x - direction * _backgroundTurnRadius - _headAnchor.position.x);
        int waveCount = Mathf.Max(1, Mathf.RoundToInt(plannedDistance / Mathf.Max(1f, _sweepWaveLength)));
        float waveLength = Mathf.Max(1f, plannedDistance / waveCount);
        if (!_pathPose.Begin(Vector3.right * direction, _combatPlaneZ, _sweepWaveAmplitude, waveLength))
        { Debug.LogError("휩쓸기 경로의 기존 몸통 본 연결을 확인해 주세요.", this); EndPattern(); yield break; }
        Vector3 headStart = _pathPose.HeadTarget;
        // Start bending before the edge so the exposed tail remains in the arena.
        float diveAngle = _burrowDiveAngle * Mathf.Deg2Rad;
        Vector3 headEnd = new Vector3(end.x - direction * _backgroundTurnRadius, headStart.y, _combatPlaneZ);
        SetBuriedDamageable(false);
        SetVisible(true);
        if (_sweepWindupDuration > 0) yield return new WaitForSeconds(_sweepWindupDuration);
        SetPhase(PatternPhase.Sweeping);
        SetHitboxes(true); SweepStarted?.Invoke();
        if (!IsActive) yield break;
        // Accelerate once, then carry the same nonzero speed through the dive boundary.
        float accelerationTime = _sweepDuration * .2f;
        // Parameterize the S curve by travelled arc length, not horizontal distance.
        // This keeps speed even through the crests and troughs.
        const int pathSteps = 512;
        var sweepPoints = new Vector3[pathSteps + 1];
        var sweepLengths = new float[pathSteps + 1];
        float horizontalDistance = Mathf.Abs(headEnd.x - headStart.x);
        // Keep the visible S stroke at full amplitude. Only settle the final short
        // section before the horizontal background turn, rather than flattening
        // the head's movement as soon as it reaches the centre of the arena.
        float exitLength = Mathf.Min(horizontalDistance * .1f, waveLength * .18f);
        for (int i = 0; i <= pathSteps; i++)
        {
            float u = i / (float)pathSteps;
            float x = horizontalDistance * u;
            float envelope = SmoothWaveExit(Mathf.Clamp01((horizontalDistance - x) / Mathf.Max(.001f, exitLength)));
            sweepPoints[i] = Vector3.Lerp(headStart, headEnd, u);
            sweepPoints[i].y += _sweepWaveAmplitude * Mathf.Sin(u * waveCount * Mathf.PI * 2f) * envelope;
            if (i > 0) sweepLengths[i] = sweepLengths[i-1] + Vector3.Distance(sweepPoints[i-1], sweepPoints[i]);
        }
        float sweepDistance = sweepLengths[pathSteps];
        int pathIndex = 0;
        float entrySpeed = sweepDistance / (_sweepDuration - accelerationTime*.5f);
        float elapsed = 0f;
        while (IsActive && elapsed < _sweepDuration - .0001f)
        {
            elapsed = Mathf.Min(elapsed + Time.fixedDeltaTime, _sweepDuration);
            float t = Mathf.Clamp01(elapsed / _sweepDuration);
            float ramp = Mathf.Clamp01(elapsed / accelerationTime);
            float distance = elapsed < accelerationTime
                ? entrySpeed * accelerationTime * (ramp*ramp*ramp - .5f*ramp*ramp*ramp*ramp)
                : entrySpeed * (elapsed - accelerationTime*.5f);
            distance = Mathf.Min(distance, sweepDistance);
            while (pathIndex < pathSteps - 1 && sweepLengths[pathIndex + 1] < distance) pathIndex++;
            float segment = Mathf.InverseLerp(sweepLengths[pathIndex], sweepLengths[pathIndex + 1], distance);
            Vector3 head = Vector3.Lerp(sweepPoints[pathIndex], sweepPoints[pathIndex + 1], segment);
            head.z = _combatPlaneZ;
            MovePathHead(head);
            yield return _fixedWait;
        }
        if (!IsActive) yield break;
        SetHitboxes(false); SweepFinished?.Invoke();
        if (!IsActive) yield break;
        // First make the mirrored quarter turn in XZ toward the background;
        // only after that turn does the head descend into the ground.
        SetPhase(PatternPhase.BurrowEnter); _animatorHandler.SetPhase(1, _burrowEnterDuration);
        _pathPose.BeginHorizontalEntry();
        Vector3 diveStart = _pathPose.HeadTarget;
        float radius = _burrowBendRadius;
        float arcLength = radius * diveAngle;
        float arcDrop = radius * (1f - Mathf.Cos(diveAngle));
        float surfaceDrop = Mathf.Max(0f, diveStart.y - _floorPoint.position.y);
        float horizontalArc = _backgroundTurnRadius * Mathf.PI * .5f;
        float surfaceDistance = horizontalArc + (surfaceDrop <= arcDrop
            ? radius * Mathf.Acos(Mathf.Clamp(1f-surfaceDrop/radius, -1f, 1f))
            : arcLength + (surfaceDrop-arcDrop) / Mathf.Sin(diveAngle));
        float diveDistance = surfaceDistance + _pathPose.BodyLength * _buriedBodyFraction;
        _pathPose.SetBurialSurface(_floorPoint.position.y);
        Vector3 effect = DiagonalDivePoint(diveStart, direction, surfaceDistance, radius, diveAngle);
        bool impacted = false; elapsed = 0;
        // Keep the Hermite travel monotonic even with short/custom arena markers.
        float entrySlope = Mathf.Clamp(entrySpeed * _burrowEnterDuration / Mathf.Max(.001f, diveDistance), 0f, 2.5f);
        while (IsActive && elapsed < _burrowEnterDuration - .0001f)
        {
            elapsed = Mathf.Min(elapsed + Time.fixedDeltaTime, _burrowEnterDuration);
            float t = Mathf.Clamp01(elapsed / _burrowEnterDuration);
            // Quintic Hermite: matched starting velocity, zero acceleration at both ends,
            // and a gentle stop only after the body has reached its buried pose.
            float progress = entrySlope*t + (10f-6f*entrySlope)*t*t*t
                + (-15f+8f*entrySlope)*t*t*t*t + (6f-3f*entrySlope)*t*t*t*t*t;
            MovePathHead(DiagonalDivePoint(diveStart, direction, diveDistance * progress, radius, diveAngle));
            _pathPose.SetTailPresentation(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .98f, progress)));
            if (!impacted && _pathPose.HeadTarget.y <= _floorPoint.position.y)
            { impacted = true; BurrowStarted?.Invoke(effect); }
            yield return _fixedWait;
        }
        if (!IsActive) yield break;
        // Let the pose catch up, then require roughly three quarters of the centreline underground.
        yield return new WaitForSeconds(.2f);
        float settling = 0f;
        while (IsActive && _pathPose.BuriedFraction(_floorPoint.position.y) < _buriedBodyFraction - .02f && settling < .6f)
        {
            diveDistance += _pathPose.BodyLength * .05f * Time.fixedDeltaTime;
            MovePathHead(DiagonalDivePoint(diveStart, direction, diveDistance, radius, diveAngle));
            settling += Time.fixedDeltaTime;
            yield return _fixedWait;
        }
        if (!IsActive) yield break;
        _pathPose.SetTailPresentation(1f);
        SetPhase(PatternPhase.TailFlail); _animatorHandler.SetPhase(5);
        SetBuriedDamageable(true); _pathPose.SetTailSwing(true);
        elapsed = 0;
        while (IsActive && elapsed < _tailFlailDuration)
        { elapsed += Time.fixedDeltaTime; yield return _fixedWait; }
        if (!IsActive) yield break;
        SetBuriedDamageable(false); _pathPose.SetTailSwing(false); _pathPose.SetTailPresentation(0f);
        // Finish sinking along the same diagonal, without a background relocation or resurfacing.
        SetPhase(PatternPhase.BurrowRetreat); _animatorHandler.SetPhase(3, _burrowExitDuration);
        float withdrawDistance = _pathPose.BodyLength * (1f-_buriedBodyFraction) + 12f;
        elapsed = 0;
        while (IsActive && elapsed < _retreatDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / _retreatDuration);
            MovePathHead(DiagonalDivePoint(diveStart, direction,
                diveDistance + withdrawDistance * Mathf.SmoothStep(0, 1, t), radius, diveAngle));
            yield return _fixedWait;
        }
        if (!IsActive) yield break;
        SetPhase(PatternPhase.RetreatZ); SetVisible(false);
        _routine = null; EndPattern();
    }

    static float SmoothWaveExit(float t)
    {
        t = Mathf.Clamp01(t);
        return t*t*t*(t*(6f*t-15f)+10f);
    }

    Vector3 DiagonalDivePoint(Vector3 origin, float direction, float distance, float radius, float angle)
    {
        float turnRadius = Mathf.Max(1f, _backgroundTurnRadius);
        float turnLength = turnRadius * Mathf.PI * .5f;
        float yaw = Mathf.Min(distance / turnRadius, Mathf.PI * .5f);
        Vector3 turn = new Vector3(direction * turnRadius * Mathf.Sin(yaw), 0f,
            turnRadius * (1f - Mathf.Cos(yaw)));
        if (distance <= turnLength) return origin + turn;
        distance -= turnLength;
        float theta = Mathf.Min(distance / radius, angle);
        float straight = Mathf.Max(0f, distance - radius * angle);
        return origin + turn + new Vector3(0f,
            -radius * (1f - Mathf.Cos(theta)) - straight * Mathf.Sin(angle),
            radius * Mathf.Sin(theta) + straight * Mathf.Cos(angle));
    }

    void SetPhase(PatternPhase phase)
    {
        Phase = phase;
        _phaseStartedTime = Time.time;
        _nextCameraShakeTime = Time.time;
        bool terrainContact = IsActive && phase != PatternPhase.Inactive
            && phase != PatternPhase.Ready && phase != PatternPhase.RetreatZ;
        if (EffectColliders != null)
            foreach (var collider in EffectColliders)
                if (collider != null) collider.enabled = terrainContact;
        if (phase == PatternPhase.TailFlail) SetBuriedDamageable(true);
        else
        {
            SetBuriedHurtboxes(false);
            SetDamageable(phase == PatternPhase.Sweeping);
        }
        SetBodyContacts(AttackActive);
        PhaseChanged?.Invoke(phase);
    }
    void SetVisible(bool visible)
    {
        for (int i = 0; i < _modelRenderers.Length; i++) if (_modelRenderers[i] != null)
            _modelRenderers[i].enabled = visible && (!_cached || _rendererEnabled[i]);
    }
    void SetCollisions(bool enabled)
    {
        if (_colliders == null) return;
        foreach (var c in _colliders) if (c != null) c.enabled = enabled;
    }
    void SetHitboxes(bool enabled) => SetDamageable(enabled);

    public override void CancelPattern()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        StopDust(IsActive && Phase != PatternPhase.RetreatZ);
        _nextDustTime = 0f;
        SetHitboxes(false); SetBuriedDamageable(false);
        RestoreBuriedHitboxPoses();
        if (_pathPose != null) _pathPose.StopFollowing();
        if (IsActive && _animatorHandler != null) _animatorHandler.ResetState();
        if (_cached)
        {
            for (int i = 0; i < _colliders.Length; i++) if (_colliders[i] != null) _colliders[i].enabled = _colliderEnabled[i];
            SetVisible(true);
            _cached = false;
        }
        SetBodyContacts(false);
        SetBuriedHurtboxes(false);
        Phase = PatternPhase.Inactive;
        _nextCameraShakeTime = 0f;
        base.CancelPattern();
    }

    void OnDrawGizmosSelected()
    {
        DrawPath(_leftStartPosition, _rightEndPosition, Color.cyan);
        DrawPath(_rightStartPosition, _leftEndPosition, Color.yellow);
    }
    void DrawPath(Transform start, Transform end, Color color)
    {
        if (start == null || end == null) return;
        Gizmos.color = color; Gizmos.DrawWireSphere(start.position, .5f);
        Gizmos.DrawLine(start.position, end.position); Gizmos.DrawWireSphere(end.position, .5f);
        Gizmos.DrawLine(start.position, start.position + Vector3.down * _buriedHeadDepth);
    }
}
