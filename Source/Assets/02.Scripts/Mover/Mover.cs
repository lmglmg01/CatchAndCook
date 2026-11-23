using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Mover : MonoBehaviour
{
    Rigidbody _rigidbody;
    Vector2 _direction;
    float _moveSpeed;
    float _movementPlaneZ;
    bool _horizontalOnly;
    readonly System.Collections.Generic.Dictionary<UnityEngine.Object, Vector2> _externalVelocities = new System.Collections.Generic.Dictionary<UnityEngine.Object, Vector2>();

    public void SetExternalVelocity(UnityEngine.Object source, Vector2 velocity)
    {
        if (source == null || float.IsNaN(velocity.x) || float.IsNaN(velocity.y)
            || float.IsInfinity(velocity.x) || float.IsInfinity(velocity.y)) return;
        _externalVelocities[source] = velocity;
    }

    public void ClearExternalVelocity(UnityEngine.Object source)
    {
        if (!ReferenceEquals(source, null)) _externalVelocities.Remove(source);
    }

    public Vector2 ExternalVelocity
    {
        get
        {
            Vector2 sum = Vector2.zero;
            foreach (var entry in _externalVelocities) if (entry.Key != null) sum += entry.Value;
            return sum;
        }
    }

    void OnDisable() { _externalVelocities.Clear(); }

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _movementPlaneZ = _rigidbody.position.z;
        _rigidbody.constraints |= RigidbodyConstraints.FreezePositionZ;
    }

    public void Move(Vector2 direction, float moveSpeed)
    {
        _direction = Vector2.ClampMagnitude(direction, 1f);
        _moveSpeed = Mathf.Max(0f, moveSpeed);
        _horizontalOnly = false;
    }

    public void SetMovementPlaneZ(float planeZ)
    {
        _movementPlaneZ = planeZ;
    }

    public void MoveHorizontal(float horizontal, float moveSpeed)
    {
        _direction = new Vector2(Mathf.Clamp(horizontal, -1f, 1f), 0f);
        _moveSpeed = Mathf.Max(0f, moveSpeed);
        _horizontalOnly = true;
    }

    void FixedUpdate()
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = _direction.x * _moveSpeed;
        if (!_horizontalOnly) velocity.y = _direction.y * _moveSpeed;
        Vector2 external = ExternalVelocity;
        velocity.x += external.x;
        velocity.y += external.y;
        velocity.z = 0f;
        _rigidbody.linearVelocity = velocity;

        Vector3 position = _rigidbody.position;
        if (position.z == _movementPlaneZ) return;
        position.z = _movementPlaneZ;
        _rigidbody.position = position;
    }
}
