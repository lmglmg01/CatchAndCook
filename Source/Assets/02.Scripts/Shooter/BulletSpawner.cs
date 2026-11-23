using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BulletSpawner : MonoBehaviour
{
    [Header("히어로 데이터")]
    [SerializeField] HeroRunModel _heroRunModel;

    [Header("총알 생성")]
    [SerializeField] Bullet _bulletPrefab;
    [SerializeField] Transform _spawnPoint;

    [Header("총알 풀링")]
    [Tooltip("미연결 시 같은 씬의 풀을 사용하고, 없으면 씬 전용 풀을 생성합니다.")]
    [SerializeField] PoolManager _poolManager;
    [SerializeField, Min(0)] int _prewarmCount;

    [Header("발사 횟수")]
    [SerializeField, Min(0)] int _maxSpawnCount;
    [SerializeField, Min(0)] int _currentSpawnCount;

    [Header("발사")]
    [SerializeField, Min(0f)] float _bulletSpeed;
    [SerializeField, Range(0f, 360f)] float _sectorAngle;

    [Header("부채꼴 탄 간격")]
    [SerializeField, Range(0f, 180f)] float _bulletAngleSpacing;

    [Header("총알 방향 (프리팹 루트 기준 머리 축)")]
    [SerializeField] Vector3 _bulletForwardAxis;

    float _damage;
    int _bulletCount;
    Coroutine _prepareRoutine;

    public Vector3 LaunchPosition => _spawnPoint != null ? _spawnPoint.position : transform.position;
    public float AimSectorAngle => _sectorAngle;
    public int MaxSpawnCount => _maxSpawnCount;
    public int CurrentSpawnCount => _currentSpawnCount;

    void Awake()
    {
        SetMaxCount();
        EnsurePool();
    }

    void OnEnable()
    {
        if (_bulletPrefab != null && _prewarmCount > 0)
            _prepareRoutine = StartCoroutine(PrepareBullets());
    }

    void OnDisable()
    {
        if (_prepareRoutine == null) return;
        StopCoroutine(_prepareRoutine);
        _prepareRoutine = null;
    }

    IEnumerator PrepareBullets()
    {
        EnsurePool();
        yield return _poolManager.Prepare(_bulletPrefab.gameObject, _prewarmCount);
        _prepareRoutine = null;
    }

    void EnsurePool()
    {
        if (_poolManager != null) return;
        foreach (PoolManager candidate in FindObjectsByType<PoolManager>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != gameObject.scene) continue;
            _poolManager = candidate;
            return;
        }

        // 히어로 자식으로 만들면 이미 발사된 총알까지 히어로 이동을 따라갑니다.
        var poolObject = new GameObject("BulletPool");
        SceneManager.MoveGameObjectToScene(poolObject, gameObject.scene);
        _poolManager = poolObject.AddComponent<PoolManager>();
    }

    public void SetMaxCount()
    {
        _currentSpawnCount = Mathf.Max(0, _maxSpawnCount);
    }

    internal void RestoreBulletCount(int amount) => _currentSpawnCount = Mathf.Clamp(amount, 0, _maxSpawnCount);

    public bool RecoverBullet(int amount)
    {
        if (amount <= 0 || _currentSpawnCount >= _maxSpawnCount) return false;

        int recoveryAmount = Mathf.Min(amount, _maxSpawnCount - _currentSpawnCount);
        _currentSpawnCount += recoveryAmount;
        return recoveryAmount > 0;
    }

    public void SetDamage(float damage)
    {
        if (damage < 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return;
        _damage = damage;
    }

    public void SetBulletCount(int bulletCount)
    {
        _bulletCount = Mathf.Max(0, bulletCount);
    }

    public void Shoot(Vector3 targetPosition, Vector2 facingDirection)
    {
        Shoot(targetPosition, facingDirection, _damage, _bulletCount);
    }

    public void Shoot(Vector3 targetPosition, Vector2 facingDirection, float damage, int bulletCount)
    {
        if (_spawnPoint == null || facingDirection.sqrMagnitude == 0f) return;
        Vector3 direction = GigShooter.GetAimDirection(_spawnPoint.position, targetPosition, facingDirection, _sectorAngle);
        Launch(_spawnPoint.position + direction, facingDirection, _sectorAngle, damage, bulletCount);
    }

    public void ShootAt(Vector3 targetPosition, Vector2 facingDirection, float sectorAngle)
    {
        ShootAt(targetPosition, facingDirection, sectorAngle, 1f);
    }

    public void ShootAt(Vector3 targetPosition, Vector2 facingDirection, float sectorAngle, float damageMultiplier)
    {
        Launch(targetPosition, facingDirection, sectorAngle, _damage * damageMultiplier, _bulletCount);
    }

    void Launch(Vector3 targetPosition, Vector2 facingDirection, float sectorAngle, float damage, int bulletCount)
    {
        if (!isActiveAndEnabled || _currentSpawnCount <= 0 || bulletCount <= 0
            || damage < 0f || float.IsNaN(damage) || float.IsInfinity(damage)
            || _bulletPrefab == null || _spawnPoint == null
            || _bulletSpeed <= 0f || _bulletForwardAxis.sqrMagnitude == 0f
            || facingDirection.sqrMagnitude == 0f) return;

        Vector3 spawnPosition = _spawnPoint.position;
        spawnPosition.z = targetPosition.z;
        Vector3 direction = targetPosition - spawnPosition;
        float lengthSquared = direction.sqrMagnitude;
        if (!(lengthSquared > 0f) || float.IsInfinity(lengthSquared)) return;
        direction = GigShooter.GetAimDirection(Vector3.zero, direction, facingDirection, sectorAngle);

        float startAngle = -(bulletCount - 1) * _bulletAngleSpacing * 0.5f;
        EnsurePool();
        Transform owner = _heroRunModel != null ? _heroRunModel.transform : transform;
        for (int i = 0; i < bulletCount; i++)
        {
            float angle = startAngle + i * _bulletAngleSpacing;
            Vector3 spreadDirection = Quaternion.AngleAxis(angle, Vector3.forward) * direction;
            Vector3 shotDirection = GigShooter.GetAimDirection(Vector3.zero, spreadDirection, facingDirection, sectorAngle);
            Quaternion rotation = Quaternion.FromToRotation(_bulletForwardAxis.normalized, shotDirection);
            _poolManager.Get(_bulletPrefab.gameObject, spawnPosition, rotation,
                initialize: instance => instance.GetComponent<Bullet>().Initialize(
                    shotDirection, _bulletSpeed, damage, owner, _poolManager));
        }
        _currentSpawnCount--;
    }
}
