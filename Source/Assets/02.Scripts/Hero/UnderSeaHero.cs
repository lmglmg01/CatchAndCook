using System;
using UnityEngine;

public class UnderSeaHero : Hero
{
    public enum HeroMode
    {
        Melee,
        Range
    }

    public enum RangeMode
    {
        Gig,
        Pistol
    }

    [Header("모드")]
    [SerializeField] HeroMode _mode;
    [SerializeField] RangeMode _rangeMode;

    [Header("모델")]
    [SerializeField] HeroRunModel _runModel;

    [Header("총 애니메이션")]
    [SerializeField] Animator _gunAnimator;
    [SerializeField] Animator _pistolAnimator;

    [Header("발사")]
    [SerializeField] GigShooter _shooter;
    [SerializeField] BulletSpawner _bulletSpawner;
    [SerializeField] Camera _shootCamera;

    [Header("총 발사 반동")]
    [SerializeField, Min(0f)] float _gunRecoilSpeed;
    [SerializeField, Min(0f)] float _gunRecoilDuration;

    Mover _recoilMover;
    Vector2 _gunRecoilVelocity;
    float _gunRecoilElapsed;
    bool _gunRecoilActive;

    [Header("조준")]
    [SerializeField] Transform _aimPivot;
    [SerializeField] Vector2 _aimOriginOffset = new Vector2(0.42f, 1.24f);
    [SerializeField, Range(0f, 360f)] float _aimSectorAngle;
    [SerializeField, Min(0.1f)] float _aimRadius;

    [Header("이동 기포")]
    [SerializeField] ParticleSystem _swimBubbleEffect;
    [SerializeField, Range(0f, 1f)] float _swimBubbleStartRatio;
    [SerializeField, Min(0f)] float _swimBubbleRampTime;
    [SerializeField] AnimationCurve _swimBubbleRampCurve;

    ParticleSystem[] _swimBubbleSystems;
    float[] _swimBubbleMaxRates;
    bool _swimBubbleMoving;
    float _swimBubbleElapsed;

    [Header("달리기 이펙트")]
    [SerializeField] GameObject _sprintEffect;
    [SerializeField] Vector3 _sprintEffectCenter = new Vector3(0f, 0.49f, 0f);

    ParticleSystem[] _sprintEffectSystems;
    bool _sprintEffectEmitting;

    [Header("상호작용")]
    [SerializeField] InteractableSensor _interactableSensor;

    [Header("물고기 획득")]
    [SerializeField] FishBag _fishBag;
    [SerializeField] Transform _interactionPoint;
    [SerializeField, Min(0f)] float _interactionRadius;
    [SerializeField] LayerMask _interactionLayers;

    [Header("근접 공격")]
    [SerializeField, Min(0f)] float _meleeAttackCooldown;
    [SerializeField] DamageableSensor _meleeDamageableSensor;

    [Header("회전")]
    [SerializeField] Transform _visualRoot;
    [SerializeField, Min(0f)] float _rotationSpeed;

    [Header("8방향 회전 (모델의 Local Rotation)")]
    [SerializeField] Vector3 _upRotation;
    [SerializeField] Vector3 _upLeftRotation;
    [SerializeField] Vector3 _leftRotation;
    [SerializeField] Vector3 _downLeftRotation;
    [SerializeField] Vector3 _downRotation;
    [SerializeField] Vector3 _downRightRotation;
    [SerializeField] Vector3 _rightRotation;
    [SerializeField] Vector3 _upRightRotation;

    Vector2 _facingDirection;
    bool _facingLeft;
    bool _isAiming;
    bool _diveEntryActive;
    bool _aimHeld;
    bool _sprintHeld;
    bool _awaitAimRelease;
    bool _poseAiming;
    RangeMode _poseRangeMode;
    float _nextMeleeAttackTime;
    float _meleeDamage;
    float _meleeRange;
    IInteractable _interactionTarget;
    DiveSupplyHold _supplyHold;

    public event Action<bool> AimChanged;
    public event Action RangeAttackFired;
    public event Action GunFired;
    public event Action MeleeAttackRequested;
    public event Action<bool> FishCollected;
    public HeroMode Mode => _mode;
    public bool IsAiming => _isAiming;
    public bool IsEnteringWater => _diveEntryActive;
    internal bool HasSupplyTarget => _supplyHold != null && _supplyHold.CanUse(this);
    bool IsMeleeAttacking => HeroAnimator != null && HeroAnimator.IsMeleeAttacking;
    public bool IsSprinting => _runModel != null && _runModel.IsSprinting;
    float AttackPowerMultiplier => _runModel != null ? _runModel.AttackPowerMultiplier : 1f;
    public Vector2 FacingDirection => _facingLeft ? Vector2.left : Vector2.right;
    // 권총을 들어 올렸을 때의 총구 위치를 무기 애니메이션과 독립된 조준 기준으로 사용합니다.
    public Vector3 AimPosition => transform.TransformPoint(new Vector3(
        FacingDirection.x * _aimOriginOffset.x, _aimOriginOffset.y, 0f));
    float ProjectilePlaneDepth => _aimPivot != null ? _aimPivot.position.z : transform.position.z;
    public float AimRadius => _aimRadius;
    public float AimSectorAngle => Mathf.Clamp(_aimSectorAngle, 0f, 360f);
    public FishBag FishBag => _fishBag;
    public GigShooter HarpoonShooter => _shooter;
    public RangeMode CurrentRangeMode => _rangeMode;
    public bool CanRecoverOxygen => _runModel != null
        && _runModel.CurrentOxygen < _runModel.MaxOxygen;
    public bool CanRecoverBullet => _bulletSpawner != null
        && _bulletSpawner.CurrentSpawnCount < _bulletSpawner.MaxSpawnCount;

    public void SetKnifeValues(float damage, float range)
    {
        if (damage < 0f || float.IsNaN(damage) || float.IsInfinity(damage)
            || !(range > 0f) || float.IsInfinity(range)) return;
        _meleeDamage = damage;
        _meleeRange = range;
    }

    public void SetGigValues(float damage, float range)
    {
        if (_shooter == null) return;
        _shooter.SetDamage(damage);
        _shooter.SetRange(range);
    }

    public void SetOxygenValue(float maxOxygen)
    {
        if (_runModel != null) _runModel.SetMaxOxygen(maxOxygen);
    }

    public void SetSwimSuitValues(float moveSpeed, float defense)
    {
        if (_runModel == null) return;
        _runModel.SetMoveSpeed(moveSpeed);
        _runModel.SetDefense(defense);
    }

    public void SetFishBagValue(float maxWeight)
    {
        if (_fishBag != null) _fishBag.SetMaxWeight(maxWeight);
    }

    public void SetGunValues(float damage, int bulletCount)
    {
        if (_bulletSpawner == null) return;
        _bulletSpawner.SetDamage(damage);
        _bulletSpawner.SetBulletCount(bulletCount);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_sprintEffect != null && !_sprintEffect.scene.IsValid())
        {
            _sprintEffect = Instantiate(_sprintEffect, _visualRoot, false);
            _sprintEffect.transform.localPosition = _sprintEffectCenter;
        }
        if (_sprintEffect != null && _sprintEffectSystems == null)
            _sprintEffectSystems = _sprintEffect.GetComponentsInChildren<ParticleSystem>(true);
        HideSprintEffect();
        InitializeSwimBubbles();
        MoveSpeedChanged += SetSwimBubbles;
        SetSwimBubbles(0f);
        if (HeroAnimator != null)
        {
            MeleeAttackRequested += HeroAnimator.SetMeleeAttack;
        }
        AimChanged += OnAimPoseChanged;
        if (_shooter != null)
        {
            _shooter.FishReturned += CollectCapturedFish;
            _shooter.FlightChanged += OnHarpoonPoseChanged;
        }
        RefreshAimPose(true);
    }

    protected override void OnDisable()
    {
        ClearGunRecoil();
        HideSprintEffect();
        SetInteractionTarget(null);
        _sprintHeld = false;
        if (_runModel != null) _runModel.SetSprinting(false);
        _aimHeld = false;
        _awaitAimRelease = false;
        _isAiming = false;
        _facingDirection = Vector2.zero;
        AimChanged?.Invoke(false);
        base.OnDisable();
        MoveSpeedChanged -= SetSwimBubbles;
        _swimBubbleMoving = false;
        _swimBubbleElapsed = 0f;
        ApplySwimBubbleRate(1f);
        if (_swimBubbleEffect != null)
            _swimBubbleEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (HeroAnimator != null)
        {
            MeleeAttackRequested -= HeroAnimator.SetMeleeAttack;
        }
        AimChanged -= OnAimPoseChanged;
        if (_shooter != null)
        {
            _shooter.FishReturned -= CollectCapturedFish;
            _shooter.FlightChanged -= OnHarpoonPoseChanged;
        }
    }

    void InitializeSwimBubbles()
    {
        if (_swimBubbleEffect == null || _swimBubbleSystems != null) return;
        _swimBubbleSystems = _swimBubbleEffect.GetComponentsInChildren<ParticleSystem>(true);
        _swimBubbleMaxRates = new float[_swimBubbleSystems.Length];
        for (int i = 0; i < _swimBubbleSystems.Length; i++)
            _swimBubbleMaxRates[i] = _swimBubbleSystems[i].emission.rateOverTimeMultiplier;
    }

    void ApplySwimBubbleRate(float ratio)
    {
        if (_swimBubbleSystems == null) return;
        for (int i = 0; i < _swimBubbleSystems.Length; i++)
        {
            if (_swimBubbleSystems[i] == null) continue;
            var emission = _swimBubbleSystems[i].emission;
            emission.rateOverTimeMultiplier = _swimBubbleMaxRates[i] * ratio;
        }
    }

    void SetSwimBubbles(float moveSpeed)
    {
        if (_swimBubbleEffect == null) return;
        bool moving = isActiveAndEnabled && moveSpeed > 0f;
        if (moving == _swimBubbleMoving) return;
        _swimBubbleMoving = moving;
        _swimBubbleElapsed = 0f;
        if (moving)
        {
            InitializeSwimBubbles();
            ApplySwimBubbleRate(Mathf.Clamp01(_swimBubbleStartRatio));
            _swimBubbleEffect.Play(true);
            // 아주 짧게 입력해도 발끝에서 최소한의 거품은 보이게 한다.
            foreach (var system in _swimBubbleSystems)
                if (system != null && system.emission.enabled) system.Emit(1);
        }
        else
        {
            _swimBubbleEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void UpdateSwimBubbles()
    {
        if (!_swimBubbleMoving) return;
        _swimBubbleElapsed += Time.deltaTime;
        float progress = _swimBubbleRampTime > 0f
            ? Mathf.Clamp01(_swimBubbleElapsed / _swimBubbleRampTime) : 1f;
        float blend = progress >= 1f ? 1f
            : _swimBubbleRampCurve != null && _swimBubbleRampCurve.length > 0
                ? Mathf.Clamp01(_swimBubbleRampCurve.Evaluate(progress)) : progress;
        ApplySwimBubbleRate(Mathf.Lerp(Mathf.Clamp01(_swimBubbleStartRatio), 1f, blend));
    }
    void HideSprintEffect()
    {
        _sprintEffectEmitting = false;
        if (_sprintEffect != null && _sprintEffect.activeSelf)
            _sprintEffect.SetActive(false);
    }

    void UpdateSprintEffect(bool sprinting)
    {
        if (_sprintEffect == null) return;

        if (sprinting != _sprintEffectEmitting)
        {
            _sprintEffectEmitting = sprinting;
            if (sprinting && !_sprintEffect.activeSelf)
            {
                _sprintEffect.SetActive(true);
                return;
            }

            foreach (var system in _sprintEffectSystems)
            {
                if (system == null) continue;
                if (sprinting) system.Play(false);
                else system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (sprinting || !_sprintEffect.activeSelf) return;
        foreach (var system in _sprintEffectSystems)
        {
            if (system != null && system.IsAlive(false)) return;
        }

        HideSprintEffect();
    }

    void Update()
    {
        if (_diveEntryActive) return;
        UpdateSprintEffect(IsSprinting && _facingDirection.sqrMagnitude > 0f);
        UpdateSwimBubbles();
        RefreshInteractionTarget();
        if (_supplyHold != null) _supplyHold.Tick(this);
    }

    void FixedUpdate()
    {
        if (!_gunRecoilActive) return;
        if (_recoilMover == null || !_recoilMover.isActiveAndEnabled
            || _gunRecoilDuration <= 0f || (_runModel != null && _runModel.CurrentOxygen <= 0f))
        {
            ClearGunRecoil();
            return;
        }

        _gunRecoilElapsed += Time.fixedDeltaTime;
        if (_gunRecoilElapsed >= _gunRecoilDuration)
        {
            ClearGunRecoil();
            return;
        }
        float strength = 1f - _gunRecoilElapsed / _gunRecoilDuration;
        _recoilMover.SetExternalVelocity(this, _gunRecoilVelocity * strength);
    }

    void ApplyGunRecoil(Vector3 shotDirection)
    {
        ClearGunRecoil();
        if (_gunRecoilSpeed <= 0f || _gunRecoilDuration <= 0f) return;
        if (_recoilMover == null) _recoilMover = GetComponent<Mover>();
        if (_recoilMover == null || !_recoilMover.isActiveAndEnabled) return;

        // Z는 고정한 채 실제 발사 방향의 반대로 밀고 기존 물리 충돌을 유지합니다.
        _gunRecoilVelocity = -((Vector2)shotDirection).normalized * _gunRecoilSpeed;
        _gunRecoilActive = true;
        _recoilMover.SetExternalVelocity(this, _gunRecoilVelocity);
    }

    void ClearGunRecoil()
    {
        if (_recoilMover != null) _recoilMover.ClearExternalVelocity(this);
        _gunRecoilElapsed = 0f;
        _gunRecoilVelocity = Vector2.zero;
        _gunRecoilActive = false;
    }

    IInteractable RefreshInteractionTarget()
    {
        if (_shooter != null && _shooter.IsResisting)
        {
            SetInteractionTarget(null);
            return null;
        }
        IInteractable target = _interactableSensor != null
            ? _interactableSensor.FindNearestInteractable(this)
            : null;
        SetInteractionTarget(target);
        return target;
    }

    void SetInteractionTarget(IInteractable target)
    {
        if (ReferenceEquals(_interactionTarget, target)) return;

        if (_interactionTarget is InteractObject previous && previous != null)
            previous.SetDetected(false);

        if (_supplyHold != null) _supplyHold.Cancel();
        _interactionTarget = target;
        var current = target as InteractObject;
        _supplyHold = current != null ? current.GetComponent<DiveSupplyHold>() : null;
        if (current != null && _supplyHold == null) current.SetDetected(true);
    }

    void CollectCapturedFish(ICatchableFish fish)
    {
        CollectFish(fish, true);
    }

    bool CollectFish(ICatchableFish fish, bool captured)
    {
        if (_fishBag == null || !(fish is Component component) || component == null) return false;
        if (captured ? !fish.IsCaptured : !fish.CanInteract) return false;

        ItemData item = fish.FilletData;
        int amount = fish.FilletAmount;
        if (item == null || amount <= 0 || (captured && amount > int.MaxValue / 2)) return false;
        if (!fish.TryCollect()) return false;

        _fishBag.GetFishFillet(item, captured ? amount * 2 : amount);
        FishCollected?.Invoke(captured);
        return true;
    }

    public void Interact()
    {
        if (!isActiveAndEnabled || _diveEntryActive) return;
        if (_shooter != null && _shooter.IsResisting)
        {
            _shooter.IncreaseResistanceGauge();
            return;
        }

        IInteractable target = RefreshInteractionTarget();
        if (target != null)
        {
            if (_supplyHold != null) return;
            target.Interact(this);
            if (target is not InteractObject and not ShipReturn) GameSfx.Play(GameSoundId.UseInteractable);
            RefreshInteractionTarget();
            return;
        }

        InteractWithFish();
    }

    public bool TryUseUtility(UtilityItemData item)
    {
        if (!isActiveAndEnabled || _diveEntryActive || item == null
            || _runModel != null && _runModel.CurrentOxygen <= 0f) return false;
        float value = item.EffectValue;
        if (!(value > 0f) || float.IsInfinity(value)) return false;

        switch (item.EffectType)
        {
            case UtilityEffectType.OxygenRecovery:
                return RecoverOxygen(value);
            case UtilityEffectType.AmmoRecovery:
                return (double)value <= int.MaxValue && RecoverBullet(Mathf.FloorToInt(value));
            case UtilityEffectType.MoveSpeedIncrease:
                return _runModel != null && _runModel.ApplyMoveSpeedBuff(value, item.Duration);
            case UtilityEffectType.AttackPowerIncrease:
                return _runModel != null && _runModel.ApplyAttackPowerBuff(value, item.Duration);
            default:
                return false;
        }
    }

    public bool RecoverOxygen(float amount)
    {
        return isActiveAndEnabled && _runModel != null
            && _runModel.RecoverOxygen(amount);
    }

    public bool RecoverBullet(int amount)
    {
        return isActiveAndEnabled && _bulletSpawner != null
            && _bulletSpawner.RecoverBullet(amount);
    }

    public void InteractWithFish()
    {
        if (!isActiveAndEnabled || _fishBag == null || _interactionPoint == null) return;
        ICatchableFish nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider target in Physics.OverlapSphere(_interactionPoint.position, _interactionRadius,
            _interactionLayers, QueryTriggerInteraction.Collide))
        {
            ICatchableFish fish = target.GetComponentInParent(typeof(ICatchableFish)) as ICatchableFish;
            if (fish == null || !fish.CanInteract) continue;
            float distance = (target.ClosestPoint(_interactionPoint.position) - _interactionPoint.position).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = fish;
        }
        if (nearest != null) CollectFish(nearest, false);
    }

    void OnAimPoseChanged(bool aiming) => RefreshAimPose();

    void OnHarpoonPoseChanged(bool flying) => RefreshAimPose();

    void RefreshAimPose(bool force = false)
    {
        // 논리 조준과 화면 표시는 발사 직후 해제하고, 작살을 든 자세만 회수까지 유지한다.
        bool holdingHarpoon = isActiveAndEnabled && _shooter != null && _shooter.IsFlying;
        bool aiming = isActiveAndEnabled && (_isAiming || holdingHarpoon);
        RangeMode mode = holdingHarpoon ? RangeMode.Gig : _rangeMode;
        if (!force && _poseAiming == aiming && _poseRangeMode == mode) return;

        bool bodyChanged = _poseAiming != aiming;
        _poseAiming = aiming;
        _poseRangeMode = mode;
        if (HeroAnimator != null && (force || bodyChanged)) HeroAnimator.SetAiming(aiming);
        SetGunAiming(aiming, mode);
    }

    void SetGunAiming(bool aiming, RangeMode mode)
    {
        if (_gunAnimator != null)
        {
            AnimatorHandler.SetAiming(_gunAnimator, aiming && mode == RangeMode.Gig);
        }
        if (_pistolAnimator != null)
        {
            AnimatorHandler.SetAiming(_pistolAnimator, aiming && mode == RangeMode.Pistol);
        }
    }

    public void SelectMeleeMode()
    {
        if (!isActiveAndEnabled || _diveEntryActive) return;
        _mode = HeroMode.Melee;
        RefreshAim();
    }

    public void SelectRangeMode()
    {
        if (!isActiveAndEnabled || _diveEntryActive) return;
        _mode = HeroMode.Range;
        RefreshAim();
    }

    public void ToggleRangeMode()
    {
        if (!isActiveAndEnabled || _diveEntryActive || _mode != HeroMode.Range) return;
        _rangeMode = _rangeMode == RangeMode.Gig ? RangeMode.Pistol : RangeMode.Gig;
        RefreshAim(true);
    }

    public void SetAimInput(bool aiming)
    {
        if (this == null || !isActiveAndEnabled || _diveEntryActive) return;
        if (!aiming) _awaitAimRelease = false;
        else if (_awaitAimRelease) return;
        _aimHeld = aiming;
        RefreshAim();
    }

    void CompleteRangeAttack()
    {
        RangeAttackFired?.Invoke();
        _aimHeld = false;
        _awaitAimRelease = true;
        RefreshAim();
        if (_shooter != null && _shooter.IsFlying) SetMoveInput(Vector2.zero);
    }

    void RefreshAim(bool weaponChanged = false)
    {
        bool aiming = _mode == HeroMode.Range && _aimHeld;
        if (_isAiming != aiming || (aiming && weaponChanged))
        {
            _isAiming = aiming;
            AimChanged?.Invoke(_isAiming);
        }
    }

    public void SetDiveEntryActive(bool active)
    {
        if (_diveEntryActive == active) return;
        if (active)
        {
            ResetInput();
            SetInteractionTarget(null);
            HideSprintEffect();
            _facingDirection = Vector2.zero;
            _facingLeft = false;
        }
        _diveEntryActive = active;
        if (!active) ResetInput();
    }

    public void SetSprintInput(bool held)
    {
        if (this == null || !isActiveAndEnabled || _diveEntryActive) return;
        _sprintHeld = held;
        if (!held && _runModel != null) _runModel.SetSprinting(false);
    }

    public override void SetMoveInput(Vector2 direction)
    {
        if (this == null || !isActiveAndEnabled) return;
        if (_diveEntryActive || IsMeleeAttacking)
        {
            // 공격 방향은 유지하고, 이동 입력과 달리기만 멈춘다.
            if (_runModel != null) _runModel.SetSprinting(false);
            MoveHero(Vector2.zero, 0f);
            return;
        }
        bool movementLocked = _isAiming || (_shooter != null && _shooter.IsFlying);
        direction = movementLocked ? Vector2.zero : Vector2.ClampMagnitude(direction, 1f);
        _facingDirection = direction;
        if (direction.x != 0f)
        {
            _facingLeft = direction.x < 0f;
        }

        if (_runModel == null) return;
        _runModel.SetSprinting(_sprintHeld && direction.sqrMagnitude > 0f && _runModel.MoveSpeed > 0f);
        MoveHero(direction, _runModel.EffectiveMoveSpeed);
    }

    public override void ResetInput()
    {
        ClearGunRecoil();
        _sprintHeld = false;
        if (_runModel != null) _runModel.SetSprinting(false);
        SetAimInput(false);
        SetMoveInput(Vector2.zero);
    }

    public bool TryGetAimPoint(Vector2 screenPosition, out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        if (_shootCamera == null || _aimRadius <= 0f) return false;
        Vector3 origin = AimPosition;
        Ray ray = _shootCamera.ScreenPointToRay(screenPosition);
        if (!new Plane(Vector3.forward, origin).Raycast(ray, out float distance)) return false;
        Vector3 target = ray.GetPoint(distance);
        if ((target - origin).sqrMagnitude < 0.0001f) return false;
        Vector3 radial = GigShooter.GetAimDirection(origin, target, FacingDirection, AimSectorAngle);
        aimPoint = origin + radial * _aimRadius;
        return true;
    }

    public void Attack(Vector2 screenPosition)
    {
        if (!isActiveAndEnabled || _diveEntryActive || IsMeleeAttacking || (_shooter != null && _shooter.IsResisting)) return;
        if (_mode == HeroMode.Melee && Time.time >= _nextMeleeAttackTime)
        {
            _nextMeleeAttackTime = Time.time + _meleeAttackCooldown;
            MeleeAttackRequested?.Invoke();
            PerformMeleeAttack();
            SetMoveInput(Vector2.zero);
        }
        else if (_isAiming && _rangeMode == RangeMode.Gig && _shooter != null
            && _shooter.CanShoot && _shootCamera != null)
        {
            if (TryGetAimPoint(screenPosition, out Vector3 aimPoint))
            {
                // 조준점이 나타내는 방향을 총구로 옮겨 발사각을 유지한다.
                Vector3 shotTarget = _shooter.LaunchPosition + (aimPoint - AimPosition);
                shotTarget.z = ProjectilePlaneDepth;
                _shooter.ShootAt(shotTarget, FacingDirection, AimSectorAngle, AttackPowerMultiplier);
                if (_shooter.IsFlying) CompleteRangeAttack();
            }
        }
        else if (_isAiming && _rangeMode == RangeMode.Pistol
            && _bulletSpawner != null && _shootCamera != null)
        {
            if (TryGetAimPoint(screenPosition, out Vector3 aimPoint))
            {
                int previousCount = _bulletSpawner.CurrentSpawnCount;
                Vector3 shotTarget = _bulletSpawner.LaunchPosition + (aimPoint - AimPosition);
                shotTarget.z = ProjectilePlaneDepth;
                _bulletSpawner.ShootAt(shotTarget, FacingDirection, AimSectorAngle, AttackPowerMultiplier);
                if (_bulletSpawner.CurrentSpawnCount < previousCount)
                {
                    Vector3 direction = GigShooter.GetAimDirection(Vector3.zero,
                        aimPoint - AimPosition, FacingDirection, AimSectorAngle);
                    ApplyGunRecoil(direction);
                    GunFired?.Invoke();
                    CompleteRangeAttack();
                }
            }
        }
    }

    void PerformMeleeAttack()
    {
        if (_meleeDamageableSensor == null || !_meleeDamageableSensor.isActiveAndEnabled
            || _meleeDamage <= 0f || _meleeRange <= 0f) return;

        Vector2 direction = _facingDirection.sqrMagnitude > 0f
            ? _facingDirection.normalized : _facingLeft ? Vector2.left : Vector2.right;
        _meleeDamageableSensor.Attack(_meleeDamage * AttackPowerMultiplier, _meleeRange, direction, transform);
    }

    void LateUpdate()
    {
        if (_diveEntryActive) return;
        Vector3 targetAngles = _facingDirection.sqrMagnitude > 0f
            ? GetFacingRotation(_facingDirection)
            : (_facingLeft ? _leftRotation : _rightRotation);
        Quaternion targetRotation = Quaternion.Euler(targetAngles);
        _visualRoot.localRotation = Quaternion.Slerp(
            _visualRoot.localRotation,
            targetRotation,
            1f - Mathf.Exp(-_rotationSpeed * Time.deltaTime));
    }

    Vector3 GetFacingRotation(Vector2 direction)
    {
        if (direction.y > 0f)
        {
            if (direction.x < 0f) return _upLeftRotation;
            if (direction.x > 0f) return _upRightRotation;
            return _upRotation;
        }

        if (direction.y < 0f)
        {
            if (direction.x < 0f) return _downLeftRotation;
            if (direction.x > 0f) return _downRightRotation;
            return _downRotation;
        }

        return direction.x < 0f ? _leftRotation : _rightRotation;
    }
}
