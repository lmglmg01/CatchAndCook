using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class HeroDetectionSensor : MonoBehaviour
{
    [Header("감지 콜라이더")]
    [SerializeField] BoxCollider _collider;

    public bool IsDetecting => _detecting && _collider != null && _collider.enabled;
    public event Action<Hero> HeroDetected;
    bool _detecting;

    void Awake()
    {
        if (_collider == null) _collider = GetComponent<BoxCollider>();
        SetDetecting(false);
    }

    public void SetDetectionBounds(Vector3 center, Vector3 size)
    {
        if (_collider == null) _collider = GetComponent<BoxCollider>();
        if (_collider == null) return;
        _collider.center = center;
        _collider.size = size;
    }

    public void SetDetecting(bool detecting)
    {
        _detecting = detecting;
        if (_collider == null) return;
        _collider.isTrigger = true;
        _collider.enabled = detecting && isActiveAndEnabled;
    }

    void OnTriggerEnter(Collider other) => Detect(other);
    void OnTriggerStay(Collider other) => Detect(other);

    void Detect(Collider other)
    {
        if (!IsDetecting || other == null || other.isTrigger) return;
        var hero = other.GetComponentInParent<Hero>();
        if (hero != null && hero.isActiveAndEnabled) HeroDetected?.Invoke(hero);
    }

    void OnDisable() => SetDetecting(false);

    void OnDrawGizmosSelected()
    {
        if (_collider == null) return;
        var previous = Gizmos.matrix;
        Gizmos.matrix = _collider.transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(_collider.center, _collider.size);
        Gizmos.matrix = previous;
    }
}
