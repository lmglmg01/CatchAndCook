using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(400)]
public class GigShooter : MonoBehaviour
{
    [Header("발사체")]
    [SerializeField] Transform _projectile;

    [Header("발사 범위")]
    [SerializeField, Range(0f, 360f)] float _sectorAngle;

    [Header("발사와 복귀")]
    [Tooltip("가속이 끝난 뒤의 최대 비행 속도입니다.")]
    [SerializeField, Min(0f)] float _shootSpeed;
    [SerializeField, Min(0f)] float _returnSpeed;
    [SerializeField, Min(0f)] float _holdTime;

    [Header("발사 가속")]
    [SerializeField, Min(0f)] float _shootAccelerationTime;
    [Tooltip("가로축은 가속 진행도(0~1), 세로축은 최대 속도 대비 비율(0~1)입니다.")]
    [SerializeField] AnimationCurve _shootSpeedCurve;

    [Header("발사 쿨타임")]
    [SerializeField, Min(0f)] float _cooldown;

    [Header("작살 적중")]
    [SerializeField] Transform _hitPoint;
    [SerializeField, Min(0f)] float _hitRadius;
    [SerializeField] LayerMask _hitLayers;

    [Header("포획 회수")]
    [SerializeField] Collider _collectorCollider;
    [SerializeField, Min(0f)] float _collectionDistance;

    [Header("물고기 회수 리듬")]
    [Tooltip("배열 크기 = 당김 횟수. 각 원소는 해당 회차의 당김 속도입니다. 비워두면 바로 마지막 회수를 합니다.")]
    [SerializeField, Min(0f)] float[] _reelPullSpeeds;
    [SerializeField, Min(0f)] float _reelPullDuration;
    [SerializeField, Min(0f)] float _reelPauseDuration;

    [Header("마지막 빠른 회수")]
    [Tooltip("설정한 당김과 대기를 모두 마친 뒤의 회수 속도입니다. 0이면 기본 복귀 속도를 사용합니다.")]
    [SerializeField, Min(0f)] float _finalReelSpeed;

    [Header("물고기 움직임")]
    [SerializeField, Min(0f)] float _fishWriggleFrequency;
    [SerializeField, Range(0f, 45f)] float _reelWriggleAngle;
    [SerializeField, Range(0f, 45f)] float _resistanceWriggleAngle;
    [SerializeField, Min(0f)] float _resistanceTugDistance;
    [SerializeField, Min(0f)] float _resistanceTugFrequency;

    [Header("물고기 저항")]
    [SerializeField, Min(0.01f)] float _resistanceGaugeMax;
    [SerializeField, Min(0.01f)] float _resistanceGaugePerPress;
    [Tooltip("저항 시작 시의 게이지입니다. 0보다 크고 목표량보다 작게 설정합니다.")]
    [SerializeField, Min(0.01f)] float _resistanceGaugeStart;
    [Tooltip("저항 중 초당 감소하는 게이지입니다.")]
    [SerializeField, Min(0.01f)] float _resistanceGaugeDecayPerSecond;
    [Tooltip("저항 시작 후 이 시간을 넘기면 물고기를 놓칩니다.")]
    [SerializeField, Min(0.01f)] float _resistanceTimeLimit;

    [Header("연결선")]
    [SerializeField] LineRenderer _lineRenderer;
    [SerializeField] Transform _lineStart;
    [SerializeField] Transform _lineEnd;
    [SerializeField, Min(0f)] float _lineSlack;
    [SerializeField, Range(2, 32)] int _lineSegments;

    [Header("발사 초반 줄 풀림")]
    [SerializeField, Min(0f)] float _launchRopeWaveAmplitude;
    [SerializeField, Min(0f)] float _launchRopeWaveDuration;
    [SerializeField, Min(0f)] float _launchRopeWaveCount;
    [SerializeField, Min(0f)] float _launchRopeWaveFrequency;

    float _damage;
    float _maxDistance;
    float _flightDamage;
    Coroutine _flight;
    float _nextShootTime;
    Transform _mount;
    Vector3 _mountPosition;
    Quaternion _mountRotation;
    Vector3 _mountScale;
    ICatchableFish _capturedFish;
    Component _capturedComponent;
    bool _isResisting;
    float _resistanceGauge;
    float _resistanceUpdatedTime;
    Fish _heldFishController;
    Vector3 _captureDirection;
    Vector3 _resistanceAnchor;
    float _captureStartTime;
    float _launchTime;
    bool _hasImpact;
    bool _hitDetectionActive;
    Vector3 _previousHitPoint;
    Material _ropeMaterial;
    Material _previousRopeMaterial;

    public event Action Fired;
    public event Action Hit;
    public event Action<bool> FlightChanged;
    public event Action<ICatchableFish> FishReturned;
    public event Action<bool> ResistanceChanged;
    public event Action<float, float> ResistanceGaugeChanged;

    public float AimSectorAngle => _sectorAngle;
    public bool IsFlying => _flight != null;
    public bool IsResisting => _isResisting;
    public Vector3 CapturePosition => _capturedComponent != null ? _capturedComponent.transform.position : HookPosition;
    public float ResistanceGauge => _resistanceGauge;
    public float ResistanceGaugeMax => _resistanceGaugeMax;
    public bool CanShoot => !IsFlying && Time.time >= _nextShootTime;
    public Vector3 LaunchPosition => _projectile != null ? _projectile.position : transform.position;

    void OnEnable()
    {
        if (!Application.isPlaying || _lineRenderer == null || _ropeMaterial != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        _previousRopeMaterial = _lineRenderer.sharedMaterial;
        _ropeMaterial = new Material(shader) { name = "Harpoon Rope (Runtime)", hideFlags = HideFlags.DontSave };
        _lineRenderer.sharedMaterial = _ropeMaterial;
    }

    void OnDestroy()
    {
        if (_lineRenderer != null && _lineRenderer.sharedMaterial == _ropeMaterial)
            _lineRenderer.sharedMaterial = _previousRopeMaterial;
        if (_ropeMaterial != null) Destroy(_ropeMaterial);
    }

    void LateUpdate()
    {
        // 본과 2.5D 피격 콜라이더의 최종 보정 이후에 작살의 이동 구간을 검사한다.
        if (_hitDetectionActive && _projectile != null)
        {
            Vector3 point = HookPosition;
            if (TryHit(_previousHitPoint, point)) _hitDetectionActive = false;
            _previousHitPoint = point;
        }
        if (_bossHitCollider != null) UpdateBossAttachment();
        UpdateHeldFish();
        if (_lineRenderer == null || _lineStart == null || _lineEnd == null) return;

        _lineRenderer.useWorldSpace = true;
        Vector3 start = _lineStart.position;
        Vector3 end = _lineEnd.position;
        bool loose = IsFlying && !_hasImpact && !HasCapturedFish();
        float distance = Vector3.Distance(start, end);
        float slack = loose ? Mathf.Min(_lineSlack, distance * 0.1f) : 0f;
        float elapsed = Time.time - _launchTime;
        float fade = loose && _launchRopeWaveDuration > 0f && _launchRopeWaveCount > 0f
            ? Mathf.Clamp01(1f - elapsed / _launchRopeWaveDuration) : 0f;
        float waveAmplitude = Mathf.Min(_launchRopeWaveAmplitude, distance * 0.3f) * fade;
        Vector3 delta = end - start;
        Vector3 perpendicular = new Vector3(-delta.y, delta.x, 0f).normalized;
        int count = slack > 0f || waveAmplitude > 0f ? Mathf.Max(2, _lineSegments) : 2;
        _lineRenderer.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);
            float envelope = Mathf.Sin(t * Mathf.PI);
            Vector3 point = Vector3.Lerp(start, end, t);
            float phase = (t * _launchRopeWaveCount - elapsed * _launchRopeWaveFrequency) * Mathf.PI * 2f;
            float wave = (Mathf.Sin(phase) + 0.2f * Mathf.Sin(phase * 2.3f + elapsed)) / 1.2f;
            point += perpendicular * (wave * waveAmplitude * envelope);
            point.y -= envelope * slack;
            _lineRenderer.SetPosition(i, point);
        }
    }

    // 입력 이벤트 한 번에 한 번만 호출한다. 누르고 있는 시간으로 증가시키지 않는다.
    public bool IncreaseResistanceGauge()
    {
        if (!_isResisting || !isActiveAndEnabled) return false;
        UpdateResistanceGauge();
        if (!_isResisting) return false;
        if (!HasCapturedFish() || _projectile == null || _collectorCollider == null
            || !_collectorCollider.enabled)
        {
            ReleaseCapture();
            return false;
        }

        _resistanceGauge = Mathf.Min(_resistanceGaugeMax, _resistanceGauge + _resistanceGaugePerPress);
        ResistanceGaugeChanged?.Invoke(_resistanceGauge, _resistanceGaugeMax);
        if (_resistanceGauge >= _resistanceGaugeMax)
        {
            _isResisting = false;
            ResistanceChanged?.Invoke(false);
        }
        return true;
    }

    void UpdateResistanceGauge()
    {
        if (!_isResisting) return;
        float now = Time.time;
        float elapsed = Mathf.Max(0f, now - _resistanceUpdatedTime);
        _resistanceUpdatedTime = now;
        _resistanceGauge = Mathf.Max(0f, _resistanceGauge - _resistanceGaugeDecayPerSecond * elapsed);
        if (_resistanceGauge <= 0f || now - _captureStartTime >= _resistanceTimeLimit)
        {
            ReleaseCapture();
            return;
        }
        if (elapsed > 0f) ResistanceGaugeChanged?.Invoke(_resistanceGauge, _resistanceGaugeMax);
    }

    public void CancelResistance()
    {
        if (_isResisting) ReleaseCapture();
    }

    public void SetDamage(float damage)
    {
        if (damage < 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        _damage = damage;
    }

    public void SetRange(float range)
    {
        if (!(range > 0f) || float.IsInfinity(range)) return;
        _maxDistance = range;
    }

    public void Shoot(Vector3 targetPosition, Vector2 facingDirection)
    {
        Shoot(targetPosition, facingDirection, _damage, _maxDistance);
    }

    public void Shoot(Vector3 targetPosition, Vector2 facingDirection, float damage, float range)
    {
        if (_projectile == null || facingDirection.sqrMagnitude == 0f) return;
        Vector3 direction = GetAimDirection(_projectile.position, targetPosition, facingDirection, _sectorAngle);
        Launch(_projectile.position + direction, facingDirection, damage, range);
    }

    public void ShootAt(Vector3 targetPosition, Vector2 facingDirection, float sectorAngle)
    {
        ShootAt(targetPosition, facingDirection, sectorAngle, 1f);
    }

    public void ShootAt(Vector3 targetPosition, Vector2 facingDirection, float sectorAngle, float damageMultiplier)
    {
        Vector3 origin = LaunchPosition;
        origin.z = targetPosition.z;
        Vector3 direction = GetAimDirection(origin, targetPosition, facingDirection, sectorAngle);
        Launch(origin + direction, facingDirection, _damage * damageMultiplier, _maxDistance);
    }

    void Launch(Vector3 targetPosition, Vector2 facingDirection, float damage, float range)
    {
        if (!isActiveAndEnabled || !CanShoot || _projectile == null
            || !(range > 0f) || float.IsInfinity(range)
            || damage < 0f || float.IsNaN(damage) || float.IsInfinity(damage)
            || _shootSpeed <= 0f || _returnSpeed <= 0f
            || facingDirection.sqrMagnitude == 0f)
        {
            return;
        }

        // 발사체를 조준점과 같은 플레이 평면에 놓고 그 좌표를 향해 발사한다.
        Vector3 launchPosition = _projectile.position;
        launchPosition.z = targetPosition.z;
        Vector3 direction = targetPosition - launchPosition;
        float lengthSquared = direction.sqrMagnitude;
        if (!(lengthSquared > 0f) || float.IsInfinity(lengthSquared)) return;
        direction.Normalize();

        _mount = _projectile.parent;
        _mountPosition = _projectile.localPosition;
        _mountRotation = _projectile.localRotation;
        _mountScale = _projectile.localScale;
        _flightDamage = damage;
        _captureDirection = direction;
        _launchTime = Time.time;
        _hasImpact = false;
        _returnAfterHit = false;
        _bossHitCollider = null;
        Vector3 destination = launchPosition + direction * range;
        _projectile.SetParent(null, true);
        _projectile.position = launchPosition;
        // 작살 모델의 끝은 로컬 +X축이다. 조준 애니메이션 중의 각도와 관계없이 실제 앞쪽을 발사 방향에 맞춘다.
        _projectile.rotation = Quaternion.FromToRotation(_projectile.right, direction) * _projectile.rotation;
        _previousHitPoint = HookPosition;
        _hitDetectionActive = true;
        _flight = StartCoroutine(Fly(destination));
        _nextShootTime = Time.time + _cooldown;
        FlightChanged?.Invoke(true);
        Fired?.Invoke();
    }

    float GetFlightSpeed(float elapsed)
    {
        if (_shootAccelerationTime <= 0f || float.IsInfinity(_shootAccelerationTime)
            || _shootSpeedCurve == null || _shootSpeedCurve.length < 2 || elapsed >= _shootAccelerationTime)
            return _shootSpeed;
        float ratio = _shootSpeedCurve.Evaluate(Mathf.Clamp01(elapsed / _shootAccelerationTime));
        if (float.IsNaN(ratio) || float.IsInfinity(ratio)) return _shootSpeed;
        return _shootSpeed * Mathf.Clamp01(ratio);
    }

    public static Vector3 GetAimDirection(Vector3 origin, Vector3 targetPosition, Vector2 facingDirection, float sectorAngle)
    {
        Vector2 facing = facingDirection.normalized;
        Vector2 targetDirection = targetPosition - origin;
        float angle = targetDirection.sqrMagnitude > 0f
            ? Vector2.SignedAngle(facing, targetDirection)
            : 0f;
        float halfAngle = sectorAngle * 0.5f;
        angle = Mathf.Clamp(angle, -halfAngle, halfAngle);
        return Quaternion.AngleAxis(angle, Vector3.forward) * (Vector3)facing;
    }

    IEnumerator Fly(Vector3 destination)
    {
        bool hasHit = false;
        yield return null;
        while (_projectile != null && Vector3.Distance(_projectile.position, destination) > 0.001f)
        {
            if (_hasImpact)
            {
                hasHit = true;
                break;
            }
            float speed = GetFlightSpeed(Time.time - _launchTime);
            _projectile.position = Vector3.MoveTowards(_projectile.position, destination, speed * Time.deltaTime);
            yield return null;
        }

        hasHit |= _hasImpact;
        if (_capturedComponent == null && !_returnAfterHit)
        {
            bool attachedToBoss = _bossHitCollider != null;
            float remaining = _holdTime;
            while (_projectile != null && remaining > 0f)
            {
                if (!hasHit)
                {
                    hasHit = _hasImpact;
                    if (_capturedComponent != null) break;
                }
                if (attachedToBoss && !UpdateBossAttachment()) break;
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        _hitDetectionActive = false;

        // 저항 중의 왕복은 적중 위치를 기준으로만 적용하여 회수 방향으로 누적되지 않게 한다.
        while (_isResisting && _projectile != null)
        {
            UpdateResistanceGauge();
            if (!_isResisting) break;
            UpdateResistanceMotion();
            UpdateHeldFish();
            if (_collectorCollider == null || !_collectorCollider.enabled) ReleaseCapture();
            yield return null;
        }

        _bossHitCollider = null;
        float reelElapsed = 0f;
        float initialReelDistance = _projectile != null
            ? Vector3.Distance(_projectile.position, GetReturnPosition()) : 0f;
        while (_projectile != null)
        {
            bool returningFish = HasCapturedFish() && _collectorCollider != null;
            Vector3 home = GetReturnPosition();
            bool finalReel = IsFinalReel(reelElapsed);
            float speed = returningFish ? GetReelSpeed(reelElapsed) : _returnSpeed;
            float step = speed * Time.deltaTime;
            if (returningFish && !finalReel)
            {
                // 예비 당김만으로 도착하지 않도록 마지막에 당길 거리를 남긴다.
                float reserve = initialReelDistance * 0.25f;
                step = Mathf.Min(step, Mathf.Max(0f, Vector3.Distance(_projectile.position, home) - reserve));
            }
            _projectile.position = Vector3.MoveTowards(_projectile.position, home, step);
            reelElapsed += Time.deltaTime;
            if (!returningFish || finalReel) UpdateCaptureReturn();
            if (returningFish && _capturedComponent == null)
            {
                yield return null;
                continue;
            }
            if ((!returningFish || finalReel) && Vector3.Distance(_projectile.position, home) <= 0.001f) break;
            yield return null;
        }

        ReleaseCapture();
        RestoreMount();
        _flight = null;
        FlightChanged?.Invoke(false);
    }

    Vector3 HookPosition => _hitPoint != null ? _hitPoint.position : LaunchPosition;

    Vector3 GetReturnPosition()
    {
        if (HasCapturedFish() && _collectorCollider != null && _projectile != null)
            return _collectorCollider.bounds.center - (HookPosition - _projectile.position);
        return _mount != null ? _mount.TransformPoint(_mountPosition) : _mountPosition;
    }

    bool IsFinalReel(float elapsed)
    {
        if (_reelPullSpeeds == null || _reelPullSpeeds.Length == 0 || _reelPullDuration <= 0f) return true;
        float period = _reelPullDuration + Mathf.Max(0f, _reelPauseDuration);
        return elapsed >= period * _reelPullSpeeds.Length;
    }

    float GetReelSpeed(float elapsed)
    {
        if (IsFinalReel(elapsed)) return _finalReelSpeed > 0f && !float.IsInfinity(_finalReelSpeed)
            ? _finalReelSpeed : _returnSpeed;
        float period = _reelPullDuration + Mathf.Max(0f, _reelPauseDuration);
        int index = Mathf.Clamp(Mathf.FloorToInt(elapsed / period), 0, _reelPullSpeeds.Length - 1);
        float speed = _reelPullSpeeds[index];
        if (!(speed >= 0f) || float.IsInfinity(speed)) return 0f;
        return elapsed - index * period < _reelPullDuration ? speed : 0f;
    }

    void UpdateResistanceMotion()
    {
        if (!HasCapturedFish() || _projectile == null) return;
        Vector3 direction = _resistanceAnchor - GetReturnPosition();
        direction.z = 0f;
        float distance = direction.magnitude;
        if (distance <= 0.001f) return;
        float offset = Mathf.Sin((Time.time - _captureStartTime) * _resistanceTugFrequency * Mathf.PI * 2f)
            * Mathf.Min(_resistanceTugDistance, distance * 0.1f);
        _projectile.position = _resistanceAnchor + direction / distance * offset;
    }

    void OnDisable()
    {
        if (_flight != null)
        {
            StopCoroutine(_flight);
            RestoreMount();
            _flight = null;
            FlightChanged?.Invoke(false);
        }
        ReleaseCapture();
        if (Application.isPlaying) LateUpdate();
    }

    Collider _bossHitCollider;
    bool _returnAfterHit;
    Vector3 _bossHitLocalPosition;
    Quaternion _bossHitLocalRotation;

    void AttachToBoss(Collider hit, Vector3 from)
    {
        var pattern = hit.GetComponentInParent<BossPattern>();
        if (_projectile == null || pattern == null || !pattern.CanAttachHarpoon) return;
        // Align the harpoon tip to the impacted surface before caching the attachment.
        _projectile.position += hit.ClosestPoint(from) - HookPosition;
        _bossHitCollider = hit;
        _bossHitLocalPosition = hit.transform.InverseTransformPoint(_projectile.position);
        _bossHitLocalRotation = Quaternion.Inverse(hit.transform.rotation) * _projectile.rotation;
    }

    bool UpdateBossAttachment()
    {
        if (_projectile == null || _bossHitCollider == null || !DamageableSensor.CanReceiveHit(_bossHitCollider))
        {
            _bossHitCollider = null;
            return false;
        }
        _projectile.SetPositionAndRotation(_bossHitCollider.transform.TransformPoint(_bossHitLocalPosition),
            _bossHitCollider.transform.rotation * _bossHitLocalRotation);
        return true;
    }

    bool IsHitTarget(Collider target)
    {
        if (target.transform.IsChildOf(transform) || target.transform.IsChildOf(_projectile)) return false;
        IDamageable damageable = target.GetComponentInParent(typeof(IDamageable)) as IDamageable;
        if (damageable != null) return DamageableSensor.CanReceiveHit(target);
        return target.GetComponentInParent(typeof(ICatchableFish)) != null;
    }

    bool TryHit(Vector3 from, Vector3 to)
    {
        // Transform으로 이동한 콜라이더를 최신 위치에서 조회한다.
        Physics.SyncTransforms();
        Collider nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider target in Physics.OverlapSphere(from, _hitRadius, _hitLayers, QueryTriggerInteraction.Collide))
        {
            if (!IsHitTarget(target)) continue;
            float distance = Vector3.Distance(from, target.ClosestPoint(from));
            if (distance >= nearestDistance) continue;
            nearest = target;
            nearestDistance = distance;
        }

        Vector3 delta = to - from;
        if (delta.sqrMagnitude > 0f)
        {
            RaycastHit[] hits = _hitRadius > 0f
                ? Physics.SphereCastAll(from, _hitRadius, delta.normalized, delta.magnitude, _hitLayers, QueryTriggerInteraction.Collide)
                : Physics.RaycastAll(from, delta.normalized, delta.magnitude, _hitLayers, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                if (!IsHitTarget(hit.collider) || hit.distance >= nearestDistance) continue;
                nearest = hit.collider;
                nearestDistance = hit.distance;
            }
        }

        // 이동 끝점에서 막 겹친 작은 콜라이더도 검사한다.
        if (nearest == null && delta.sqrMagnitude > 0f)
        {
            foreach (Collider target in Physics.OverlapSphere(to, _hitRadius, _hitLayers, QueryTriggerInteraction.Collide))
            {
                if (!IsHitTarget(target)) continue;
                float distance = Vector3.Distance(to, target.ClosestPoint(to));
                if (distance >= nearestDistance) continue;
                nearest = target;
                nearestDistance = distance;
            }
        }

        if (nearest == null) return false;
        _hasImpact = true;
        Hit?.Invoke();
        ICatchableFish fish = nearest.GetComponentInParent(typeof(ICatchableFish)) as ICatchableFish;
        Component fishComponent = fish as Component;
        Fish fishController = nearest.GetComponentInParent<Fish>();
        if (fishController == null)
        {
            var pattern = nearest.GetComponentInParent<BossPattern>();
            bool damaged = DamageableSensor.TryApplyHit(nearest, _flightDamage, from);
            if (pattern != null && !pattern.CanAttachHarpoon) _returnAfterHit = true;
            else if (damaged) AttachToBoss(nearest, from);
            if (_collectorCollider != null && fishComponent != null && fish.TryBeginCapture())
                HoldFish(fish, fishComponent);
            return true;
        }

        // 새 물고기 API가 피해와 포획 가능 여부를 함께 판정한다.
        FishHarpoonResult result = DamageableSensor.ApplyHarpoonHit(nearest, _flightDamage, from);
        if (result == FishHarpoonResult.Unavailable) return true;
        if (result == FishHarpoonResult.TooHealthy || _collectorCollider == null
            || !_collectorCollider.enabled || fishComponent == null)
        {
            fishController.EndHarpoonAction();
            return true;
        }

        HoldFish(fish, fishComponent);
        if (result == FishHarpoonResult.Resistant)
        {
            if (!(_resistanceGaugeMax > 0f) || float.IsInfinity(_resistanceGaugeMax)
                || !(_resistanceGaugePerPress > 0f) || float.IsInfinity(_resistanceGaugePerPress)
                || !(_resistanceGaugeStart > 0f && _resistanceGaugeStart < _resistanceGaugeMax)
                || !(_resistanceGaugeDecayPerSecond > 0f) || float.IsInfinity(_resistanceGaugeDecayPerSecond)
                || !(_resistanceTimeLimit > 0f) || float.IsInfinity(_resistanceTimeLimit))
            {
                Debug.LogWarning("작살의 저항 게이지 목표량·시작량·입력 증가량·초당 감소량·제한 시간을 Inspector에서 설정해주세요.", this);
                ReleaseCapture();
                return true;
            }
            _resistanceGauge = _resistanceGaugeStart;
            _resistanceUpdatedTime = Time.time;
            _isResisting = true;
            ResistanceGaugeChanged?.Invoke(_resistanceGauge, _resistanceGaugeMax);
            ResistanceChanged?.Invoke(true);
        }
        return true;
    }

    void HoldFish(ICatchableFish fish, Component component)
    {
        _capturedFish = fish;
        _capturedComponent = component;
        _heldFishController = component.GetComponent<Fish>();
        _captureStartTime = Time.time;
        _resistanceAnchor = _projectile.position;
        UpdateHeldFish();
    }

    bool HasCapturedFish()
    {
        return _capturedComponent != null && _capturedComponent.gameObject.activeInHierarchy
            && _capturedFish != null && _capturedFish.IsCaptured;
    }

    void UpdateHeldFish()
    {
        if (_capturedFish == null) return;
        if (!HasCapturedFish() || _projectile == null)
        {
            ReleaseCapture();
            return;
        }
        if (_heldFishController != null && _captureDirection.sqrMagnitude > 0.0001f)
        {
            float amplitude = _isResisting ? _resistanceWriggleAngle : _reelWriggleAngle;
            float angle = Mathf.Sin((Time.time - _captureStartTime) * _fishWriggleFrequency * Mathf.PI * 2f) * amplitude;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.forward) * _captureDirection;
            _heldFishController.AlignToHarpoon(HookPosition, direction);
        }
        else _capturedFish.MoveCaptured(HookPosition);
    }

    void UpdateCaptureReturn()
    {
        if (_isResisting || _capturedFish == null || _projectile == null) return;
        if (!HasCapturedFish())
        {
            ReleaseCapture();
            return;
        }

        UpdateHeldFish();
        if (_collectorCollider == null || !_collectorCollider.enabled) return;
        // 물고기 몸통은 작살 끝에서 떨어져 있을 수 있으므로 연결 지점으로 도착을 판정한다.
        Vector3 position = HookPosition;
        if (Vector3.Distance(position, _collectorCollider.ClosestPoint(position)) > _collectionDistance) return;

        FishReturned?.Invoke(_capturedFish);
        ReleaseCapture();
    }

    void ReleaseCapture()
    {
        ICatchableFish fish = _capturedFish;
        Component component = _capturedComponent;
        bool wasResisting = _isResisting;
        bool hadGauge = _resistanceGauge > 0f;
        _capturedFish = null;
        _capturedComponent = null;
        _heldFishController = null;
        _isResisting = false;
        _resistanceGauge = 0f;
        if (component != null) fish?.CancelCapture();
        if (wasResisting) ResistanceChanged?.Invoke(false);
        if (wasResisting || hadGauge) ResistanceGaugeChanged?.Invoke(0f, _resistanceGaugeMax);
    }

    void RestoreMount()
    {
        _hitDetectionActive = false;
        _returnAfterHit = false;
        _bossHitCollider = null;
        if (_projectile == null) return;
        _projectile.SetParent(_mount, false);
        _projectile.localPosition = _mountPosition;
        _projectile.localRotation = _mountRotation;
        _projectile.localScale = _mountScale;
    }
}
