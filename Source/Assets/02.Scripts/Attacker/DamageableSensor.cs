using System;
using UnityEngine;

public class DamageableSensor : MonoBehaviour
{
    [Header("공격 위치")]
    [SerializeField] Transform positionPoint;

    [Header("공격 범위")]
    [Tooltip("근접 공격에서는 X 길이를 장비 사거리로 전달받고, Y 높이와 Z 두께는 이 설정을 사용합니다.")]
    [SerializeField] Vector3 areaSize;

    [Header("공격 대상")]
    [SerializeField] LayerMask targetLayer;

    float _meleeRange;
    Vector2 _meleeDirection;
    Transform _meleeOwner;

    public static event Action<Vector3> HitDetected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHitEvent()
    {
        HitDetected = null;
    }

    /// <summary>
    /// 공격
    /// </summary>
    /// <param name="damage">데미지</param>

    public void Attack(float damage)
    {
        Transform point = positionPoint != null ? positionPoint : transform;
        if (AttackAt(damage, point.position, point.rotation) && GetComponentInParent<Fish>() is Fish fish)
            GameSfx.PlayWithoutRestart(GameSoundId.Bite, fish, point.position);
    }

    // 모델의 깊이를 유지한 채 2.5D 공격 검사만 지정한 전투 평면에서 수행한다.
    public void AttackOnPlane(float damage, float planeZ)
    {
        Transform point = positionPoint != null ? positionPoint : transform;
        Vector3 position = point.position;
        position.z = planeZ;
        AttackAt(damage, position, point.rotation);
    }

    bool AttackAt(float damage, Vector3 position, Quaternion rotation)
    {
        if (!(damage > 0f) || float.IsInfinity(damage)) return false;

        Vector3 halfExtents = new Vector3(
            Mathf.Abs(areaSize.x),
            Mathf.Abs(areaSize.y),
            Mathf.Abs(areaSize.z)) * 0.5f;

        Collider[] targets = Physics.OverlapBox(
            position,
            halfExtents,
            rotation,
            targetLayer,
            QueryTriggerInteraction.Collide);
        foreach (Collider target in targets)
        {
            IDamageable damageable = GetDamageable(target);
            if (CanReceiveHit(damageable)) return TryApplyHit(target, damage, position);
        }

        return false;
    }

    // 히어로 근접 공격은 전용 센서의 위치와 레이어를 사용한다.
    public void Attack(float damage, float range, Vector2 direction, Transform owner)
    {
        if (!isActiveAndEnabled || positionPoint == null || owner == null
            || !(damage > 0f) || float.IsInfinity(damage)
            || !(range > 0f) || float.IsInfinity(range)
            || direction.sqrMagnitude == 0f || targetLayer.value == 0
            || areaSize.x <= 0f || areaSize.y <= 0f || areaSize.z <= 0f) return;

        _meleeRange = range;
        _meleeDirection = direction.normalized;
        _meleeOwner = owner;
        Vector3 origin = positionPoint.position;
        origin.z = owner.position.z;
        Vector3 forward = new Vector3(_meleeDirection.x, _meleeDirection.y, 0f);
        Quaternion rotation = Quaternion.FromToRotation(Vector3.right, forward);
        Vector3 halfExtents = new Vector3(range, areaSize.y, areaSize.z) * 0.5f;

        Collider nearestCollider = null;
        float nearestDistance = float.PositiveInfinity;
        Physics.SyncTransforms();
        foreach (Collider target in Physics.OverlapBox(origin, halfExtents, rotation,
            targetLayer, QueryTriggerInteraction.Collide))
        {
            if (target.transform.IsChildOf(owner)) continue;
            Vector3 point = target.ClosestPoint(origin);
            if (Vector3.Dot(forward, point - owner.position) < 0f) continue;
            IDamageable damageable = GetDamageable(target);
            if (!CanReceiveHit(damageable)) continue;
            float distance = (point - origin).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestCollider = target;
            nearestDistance = distance;
        }

        if (nearestCollider != null) TryApplyHit(nearestCollider, damage, origin);
    }

    static Vector3 GetHitEffectPosition(Collider target, IDamageable damageable, Vector3 attackPosition)
    {
        Component targetComponent = damageable as Component;
        Transform targetRoot = targetComponent != null ? targetComponent.transform : target.transform;
        Vector3 effectPosition = target.bounds.center;
        float nearestDistance = float.PositiveInfinity;
        foreach (Renderer modelRenderer in targetRoot.GetComponentsInChildren<Renderer>())
        {
            if (!modelRenderer.enabled
                || (!(modelRenderer is MeshRenderer) && !(modelRenderer is SkinnedMeshRenderer) && !(modelRenderer is SpriteRenderer))) continue;
            if (targetComponent != null && modelRenderer.GetComponentInParent(typeof(IDamageable)) != targetComponent) continue;
            Bounds bounds = modelRenderer.bounds;
            if (bounds.size.sqrMagnitude <= Mathf.Epsilon) continue;
            Vector3 point = bounds.ClosestPoint(attackPosition);
            float distance = (point - attackPosition).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            effectPosition = point;
        }
        return effectPosition;
    }

    static IDamageable GetDamageable(Collider target)
    {
        if (target == null) return null;
        var damageable = target.GetComponentInParent(typeof(IDamageable)) as IDamageable;
        // 감지 센서나 지형 접촉용 콜라이더가 공격 대상으로 처리되지 않게 한다.
        if (damageable is BossPattern pattern && !pattern.CanReceiveHit(target)) return null;
        return damageable;
    }

    public static bool CanReceiveHit(Collider target) => target != null && target.enabled
        && target.gameObject.activeInHierarchy && CanReceiveHit(GetDamageable(target));

    public static bool CanReceiveHit(IDamageable damageable)
    {
        if (damageable == null) return false;
        if (damageable is Component component)
        {
            if (component == null || !component.gameObject.activeInHierarchy) return false;
            if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) return false;
        }
        if (damageable is FishModel fish && (fish.IsCollected || fish.State == FishState.Dead)) return false;
        if (damageable is BossPattern pattern && !pattern.CanReceiveDamage) return false;
        if (damageable is Boss boss && !boss.CanReceiveDamage) return false;
        return !TryReadHealth(damageable, out float health) || health > 0f;
    }

    public static bool TryApplyHit(Collider target, float damage, Vector3 attackPosition)
    {
        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy
            || !(damage > 0f) || float.IsInfinity(damage)) return false;
        IDamageable damageable = GetDamageable(target);
        if (!CanReceiveHit(damageable)) return false;

        Vector3 effectPosition = GetHitEffectPosition(target, damageable, attackPosition);
        bool hasHealth = TryReadHealth(damageable, out float previousHealth);
        damageable.TakeDamage(damage);
        if (hasHealth && (!TryReadHealth(damageable, out float currentHealth) || !(currentHealth < previousHealth))) return false;
        ReportHit(damageable, effectPosition);
        return true;
    }

    public static FishHarpoonResult ApplyHarpoonHit(Collider target, float damage, Vector3 attackPosition)
    {
        if (target == null || damage < 0f || float.IsNaN(damage) || float.IsInfinity(damage))
            return FishHarpoonResult.Unavailable;
        Fish fish = target.GetComponentInParent<Fish>();
        IDamageable damageable = GetDamageable(target);
        if (fish == null || !CanReceiveHit(damageable)) return FishHarpoonResult.Unavailable;

        // 상태 전환으로 콜라이더가 꺼지기 전에 이펙트 위치를 계산한다.
        Vector3 effectPosition = GetHitEffectPosition(target, damageable, attackPosition);
        bool hasHealth = TryReadHealth(damageable, out float previousHealth);
        FishHarpoonResult result = fish.ReceiveHarpoonHit(damage);
        if (hasHealth && TryReadHealth(damageable, out float currentHealth) && currentHealth < previousHealth)
            ReportHit(damageable, effectPosition);
        return result;
    }

    static void ReportHit(IDamageable damageable, Vector3 effectPosition)
    {
        HitDetected?.Invoke(effectPosition);
        if (damageable is HeroRunModel) return;
        Component owner = damageable is BossPattern pattern ? pattern.OwnerBoss : damageable as Component;
        GameSfx.PlayWithoutRestart(GameSoundId.WeaponHit, owner, effectPosition);
    }

    static bool TryReadHealth(IDamageable damageable, out float health)
    {
        if (damageable is Boss boss) { health = boss.CurrentHealth; return true; }
        if (damageable is BossPattern pattern) { health = pattern.CurrentHealth; return true; }
        if (damageable is FishModel fish)
        {
            health = fish.CurrentHp;
            return true;
        }
        if (damageable is HeroRunModel hero)
        {
            health = hero.CurrentHealth;
            return true;
        }
        health = 0f;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Transform point = positionPoint != null ? positionPoint : transform;
        Vector3 center = point.position;
        Vector3 size = areaSize;
        Quaternion rotation = point.rotation;
        if (_meleeOwner != null && _meleeRange > 0f)
        {
            center.z = _meleeOwner.position.z;
            size.x = _meleeRange;
            rotation = Quaternion.FromToRotation(Vector3.right,
                new Vector3(_meleeDirection.x, _meleeDirection.y, 0f));
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = previousMatrix;
    }
}
