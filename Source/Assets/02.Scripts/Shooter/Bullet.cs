using UnityEngine;

public class Bullet : MonoBehaviour, IPoolable
{
    [Header("수명")]
    [SerializeField, Min(0f)] float _lifeTime;

    [Header("파괴 판정")]
    [SerializeField] LayerMask _destroyLayers;
    [SerializeField, Min(0f)] float _collisionRadius;

    Vector3 _direction;
    float _speed;
    float _damage;
    Transform _owner;
    bool _hasHit;
    bool _initialized;
    float _remainingLifeTime;
    PoolManager _poolManager;

    public void Initialize(Vector3 direction, float speed)
    {
        Initialize(direction, speed, 0f, null);
    }

    public void Initialize(Vector3 direction, float speed, float damage, Transform owner)
    {
        Initialize(direction, speed, damage, owner, null);
    }

    public void Initialize(Vector3 direction, float speed, float damage, Transform owner, PoolManager poolManager)
    {
        _direction = direction.normalized;
        _speed = speed;
        _damage = damage;
        _owner = owner;
        _hasHit = false;
        _poolManager = poolManager;
        _remainingLifeTime = _lifeTime;
        _initialized = true;
    }

    public void OnSpawn()
    {
        ResetShot();
        _hasHit = false;
    }

    public void OnDespawn()
    {
        ResetShot();
        _hasHit = true;
    }

    void ResetShot()
    {
        _initialized = false;
        _direction = Vector3.zero;
        _speed = 0f;
        _damage = 0f;
        _owner = null;
        _remainingLifeTime = 0f;
        _poolManager = null;
    }

    void Release()
    {
        _initialized = false;
        if (_poolManager != null) _poolManager.Return(gameObject);
        else Destroy(gameObject);
    }

    bool IsIgnored(Collider target)
    {
        if (target.transform.IsChildOf(transform)
            || (_owner != null && target.transform.IsChildOf(_owner))) return true;
        IDamageable damageable = target.GetComponentInParent(typeof(IDamageable)) as IDamageable;
        return damageable != null && !DamageableSensor.CanReceiveHit(target);
    }

    void Hit(Collider target)
    {
        if (!_initialized || _hasHit) return;
        _hasHit = true;
        DamageableSensor.TryApplyHit(target, _damage, transform.position);
        Release();
    }

    void Update()
    {
        if (!_initialized || _hasHit) return;
        _remainingLifeTime -= Time.deltaTime;
        if (_remainingLifeTime <= 0f)
        {
            Release();
            return;
        }
        float distance = _speed * Time.deltaTime;
        if (distance <= 0f) return;

        foreach (Collider target in Physics.OverlapSphere(transform.position, _collisionRadius, _destroyLayers, QueryTriggerInteraction.Collide))
        {
            if (IsIgnored(target)) continue;
            Hit(target);
            return;
        }

        RaycastHit[] hits = _collisionRadius > 0f
            ? Physics.SphereCastAll(transform.position, _collisionRadius, _direction, distance, _destroyLayers, QueryTriggerInteraction.Collide)
            : Physics.RaycastAll(transform.position, _direction, distance, _destroyLayers, QueryTriggerInteraction.Collide);
        Collider nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (IsIgnored(hit.collider) || hit.distance >= nearestDistance) continue;
            nearest = hit.collider;
            nearestDistance = hit.distance;
        }
        if (nearest != null)
        {
            Hit(nearest);
            return;
        }
        transform.position += _direction * distance;
    }
}
