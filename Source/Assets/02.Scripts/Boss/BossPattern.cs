using System;
using UnityEngine;

public abstract class BossPattern : MonoBehaviour, IDamageable
{
    [Header("패턴 공통 참조")]
    [SerializeField] Hero _hero;
    [SerializeField] Boss _boss;
    [SerializeField] Rigidbody _rigidbody;

    [Header("리그별 피격 콜라이더")]
    [SerializeField] Collider[] _hurtColliders;
    protected Collider[] HurtColliders => _hurtColliders;

    readonly System.Collections.Generic.Dictionary<Collider, Vector3> _hurtCenters = new System.Collections.Generic.Dictionary<Collider, Vector3>();

    // 모델과 감지 센서를 움직이지 않고, 등록된 피격 형상의 중심만 플레이 평면에 둔다.
    protected void UpdateHurtColliderPlane(bool active)
    {
        if (!active || Hero == null) { RestoreHurtColliderCenters(); return; }
        if (_hurtColliders == null) return;
        foreach (var hit in _hurtColliders)
        {
            if (hit == null || (!(hit is BoxCollider) && !(hit is CapsuleCollider))) continue;
            if (!_hurtCenters.TryGetValue(hit, out var center))
            {
                center = hit is BoxCollider box ? box.center : ((CapsuleCollider)hit).center;
                _hurtCenters.Add(hit, center);
            }
            Vector3 world = hit.transform.TransformPoint(center);
            world.z = Hero.transform.position.z;
            Vector3 local = hit.transform.InverseTransformPoint(world);
            if (hit is BoxCollider b) b.center = local;
            else ((CapsuleCollider)hit).center = local;
        }
    }

    void RestoreHurtColliderCenters()
    {
        foreach (var entry in _hurtCenters)
        {
            if (entry.Key is BoxCollider box && box != null) box.center = entry.Value;
            else if (entry.Key is CapsuleCollider capsule && capsule != null) capsule.center = entry.Value;
        }
        _hurtCenters.Clear();
    }

    protected virtual bool ManageHurtColliders => true;

    [Header("이펙트 전용 콜라이더")]
    [Tooltip("지형 접촉 이펙트의 위치와 범위. 피격·공격용과 별도 콜라이더를 연결합니다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("_terrainContactColliders")]
    [SerializeField] Collider[] _effectColliders;
    protected Collider[] EffectColliders => _effectColliders;

    protected Collider[] HeroColliders { get; private set; }
    bool _damageable;

    // 모든 콜라이더는 같은 패턴의 IDamageable로 연결되어 보스 체력 하나를 사용한다.
    protected void SetDamageable(bool active, Collider[] exposedColliders = null)
    {
        _damageable = active && IsActive;
        if (!ManageHurtColliders || _hurtColliders == null) return;
        foreach (var hit in _hurtColliders)
            if (hit != null) hit.enabled = _damageable
                && (exposedColliders == null || Array.IndexOf(exposedColliders, hit) >= 0);
    }

    protected virtual void OnEnable() => SetDamageable(false);

    public bool IsActive { get; protected set; }
    public Boss OwnerBoss => _boss;
    public virtual bool CanAttachHarpoon => true;
    public float CurrentHealth => _boss != null ? _boss.CurrentHealth : 0f;
    public bool CanReceiveDamage => _damageable && IsActive && isActiveAndEnabled && _boss != null && _boss.CanReceiveDamage;
    public bool CanReceiveHit(Collider hit) => CanReceiveDamage && hit != null && hit.enabled
        && IsRegisteredHurtCollider(hit) && IsHurtColliderInRange(hit);
    protected virtual bool IsRegisteredHurtCollider(Collider hit) => _hurtColliders != null
        && Array.IndexOf(_hurtColliders, hit) >= 0;
    protected virtual bool IsHurtColliderInRange(Collider hit) => true;
    public void TakeDamage(float damage) { if (CanReceiveDamage) _boss.TakeDamage(damage); }
    public event Action<BossPattern> PatternCompleted;
    protected Hero Hero => _hero;
    protected Vector3 Position => _rigidbody != null ? _rigidbody.position : transform.position;

    // 한 번의 물기에 여러 플레이어 콜라이더가 겹쳐도 피해는 한 번만 전달한다.
    protected bool DamageHeroInBox(Transform point, Vector3 worldSize, float damage)
    {
        if (!IsActive || Hero == null || point == null || _heroRunModel == null) return false;
        Vector3 center = point.position;
        center.z = Hero.transform.position.z;
        Quaternion rotation = point.rotation;
        Physics.SyncTransforms();
        foreach (var hit in Physics.OverlapBox(center, worldSize * .5f,
            rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.GetComponentInParent<Hero>() != Hero) continue;
            return DamageableSensor.TryApplyHit(hit, damage, center);
        }
        return false;
    }

    HeroRunModel _heroRunModel;
    bool _wasKinematic;
    RigidbodyConstraints _previousConstraints;

    public void Initialize(Hero hero, Boss boss = null)
    {
        CancelPattern();
        _hero = hero;
        HeroColliders = hero != null ? hero.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
        if (boss != null) _boss = boss;
        if (_boss == null) _boss = GetComponentInParent<Boss>();
    }

    public abstract void ActivatePattern();

    protected bool BeginPattern()
    {
        if (IsActive || !isActiveAndEnabled || _hero == null) return false;
        if (_boss == null || !_boss.CanReceiveDamage) return false;
        _heroRunModel = _hero.GetComponent<HeroRunModel>();
        if (_heroRunModel != null)
        {
            if (_heroRunModel.CurrentHealth <= 0f) return false;
            _heroRunModel.OxygenDepleted += CancelPattern;
        }
        if (_rigidbody != null)
        {
            _wasKinematic = _rigidbody.isKinematic;
            _previousConstraints = _rigidbody.constraints;
            if (!_wasKinematic) _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
        IsActive = true;
        SetDamageable(true);
        return true;
    }

    protected void Teleport(Transform destination)
    {
        if (destination == null) return;
        SetPosition(destination.position);
        if (_rigidbody != null)
        {
            _rigidbody.rotation = destination.rotation;
            _rigidbody.transform.rotation = destination.rotation;
        }
        else transform.rotation = destination.rotation;
    }

    protected void MovePosition(Vector3 position)
    {
        if (_rigidbody != null) _rigidbody.MovePosition(position);
        else transform.position = position;
    }

    protected void SetPosition(Vector3 position)
    {
        if (_rigidbody != null)
        {
            _rigidbody.position = position;
            // Teleports must update the visible rig immediately, before camera framing samples it.
            // MovePosition remains interpolated for ordinary pattern movement.
            _rigidbody.transform.position = position;
        }
        else transform.position = position;
    }

    public virtual void CancelPattern()
    {
        RestoreHurtColliderCenters();
        if (_heroRunModel != null) _heroRunModel.OxygenDepleted -= CancelPattern;
        _heroRunModel = null;
        SetDamageable(false);
        if (!IsActive) return;
        IsActive = false;
        if (_rigidbody != null)
        {
            _rigidbody.constraints = _previousConstraints;
            _rigidbody.isKinematic = _wasKinematic;
        }
    }

    protected void EndPattern()
    {
        if (!IsActive) return;
        CancelPattern();
        PatternCompleted?.Invoke(this);
    }

    protected virtual void OnDisable() => CancelPattern();
}
