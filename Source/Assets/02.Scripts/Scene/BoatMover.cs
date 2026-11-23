using UnityEngine;

[DisallowMultipleComponent]
public class BoatMover : MonoBehaviour
{
    [Header("보트 이동 참조")]
    [SerializeField] Rigidbody _rigidbody;
    [SerializeField] Transform _movePoint;
    [Tooltip("파란색 Z축 화살표가 선수 방향을 향하도록 설정합니다.")]
    [SerializeField] Transform _bowDirection;

    [Header("항해 속도")]
    [SerializeField, Min(0.1f)] float _moveSpeed;
    [SerializeField, Min(0.1f)] float _acceleration;
    [SerializeField, Min(0.1f)] float _deceleration;

    [Header("선회 경로")]
    [Tooltip("출발 선수 방향과 도착 방향으로 뻗는 곡선 길이입니다. 크게 설정할수록 넓게 돌아갑니다. Move Point의 파란색 Z축이 도착할 때의 선수 방향입니다. 수면 높이는 유지합니다.")]
    [SerializeField, Min(0.1f)] float _turnDistance;

    [Header("출발 후 씬 전환")]
    [SerializeField] bool _loadNextScene;
    [Tooltip("보트 출발 후 씬 전환을 요청하기까지의 시간(초)")]
    [SerializeField, Min(0f)] float _sceneLoadDelay;
    bool _loadPending;
    float _elapsed;

    const int PathSamples = 128;
    readonly float[] _pathLengths = new float[PathSamples + 1];
    Vector3 _start;
    Vector3 _startControl;
    Vector3 _endControl;
    Vector3 _end;
    Quaternion _bowCorrection;
    float _pathLength;
    float _travelled;
    float _speed;

    public event System.Action SceneTransitionRequested;

    public bool IsMoving { get; private set; }

    public bool MoveToPoint()
    {
        if (!isActiveAndEnabled || IsMoving || _rigidbody == null || !_rigidbody.isKinematic
            || _movePoint == null || _bowDirection == null || _movePoint.IsChildOf(transform)
            || _moveSpeed <= 0f || _acceleration <= 0f || _deceleration <= 0f || _turnDistance <= 0f)
            return false;

        _start = _rigidbody.position;
        _end = new Vector3(_movePoint.position.x, _start.y, _movePoint.position.z);
        if ((_end - _start).sqrMagnitude < 0.0001f) return false;
        Vector3 forward = PlanarDirection(_bowDirection.forward, transform.forward);
        Vector3 arrival = PlanarDirection(_movePoint.forward, _end - _start);
        _startControl = _start + forward * _turnDistance;
        _endControl = _end - arrival * _turnDistance;
        _bowCorrection = Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up)) * _rigidbody.rotation;

        // 등간격 t 대신 누적 거리로 진행하여 곡선의 긴 구간에서도 속도가 튀지 않는다.
        _pathLengths[0] = 0f;
        Vector3 previous = _start;
        for (int i = 1; i <= PathSamples; i++)
        {
            Vector3 next = Evaluate((float)i / PathSamples);
            _pathLengths[i] = _pathLengths[i - 1] + Vector3.Distance(previous, next);
            previous = next;
        }
        _pathLength = _pathLengths[PathSamples];
        _travelled = 0f;
        _speed = 0f;
        _elapsed = 0f;
        _loadPending = _loadNextScene;
        IsMoving = true;
        return true;
    }

    void FixedUpdate()
    {
        if (!IsMoving) return;
        if (_rigidbody == null || !_rigidbody.isKinematic)
        {
            StopMovement();
            return;
        }

        float remaining = _pathLength - _travelled;
        float targetSpeed = Mathf.Min(_moveSpeed, Mathf.Sqrt(2f * _deceleration * remaining));
        _speed = Mathf.MoveTowards(_speed, targetSpeed,
            (_speed < targetSpeed ? _acceleration : _deceleration) * Time.fixedDeltaTime);
        _travelled = Mathf.Min(_pathLength, _travelled + _speed * Time.fixedDeltaTime);
        bool arrived = _pathLength - _travelled <= 0.01f;
        float t = arrived ? 1f : ProgressAtDistance(_travelled);
        Vector3 tangent = Tangent(t);
        _rigidbody.MovePosition(arrived ? _end : Evaluate(t));
        if (tangent.sqrMagnitude > 0.0001f)
            _rigidbody.MoveRotation(Quaternion.LookRotation(tangent, Vector3.up) * _bowCorrection);
        if (arrived) { IsMoving = false; _speed = 0f; }
    }

    public void StopMovement()
    {
        _loadPending = false;
        IsMoving = false;
        _speed = 0f;
    }

    void Update()
    {
        if (!_loadPending) return;
        _elapsed += Time.deltaTime;
        if (_elapsed < _sceneLoadDelay) return;
        _loadPending = false;
        SceneTransitionRequested?.Invoke();
    }

    void OnDisable() => StopMovement();

    float ProgressAtDistance(float distance)
    {
        int low = 1;
        int high = PathSamples;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (_pathLengths[middle] < distance) low = middle + 1;
            else high = middle;
        }
        float fraction = Mathf.InverseLerp(_pathLengths[low - 1], _pathLengths[low], distance);
        return (low - 1 + fraction) / PathSamples;
    }

    Vector3 Evaluate(float t) => Curve(_start, _startControl, _endControl, _end, t);

    Vector3 Tangent(float t)
    {
        float u = 1f - t;
        return 3f * u * u * (_startControl - _start)
            + 6f * u * t * (_endControl - _startControl) + 3f * t * t * (_end - _endControl);
    }

    static Vector3 PlanarDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = fallback;
            direction.y = 0f;
        }
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    static Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    void OnDrawGizmosSelected()
    {
        if (_movePoint == null || _bowDirection == null) return;
        Vector3 start = IsMoving ? _start : transform.position;
        Vector3 end = IsMoving ? _end : new Vector3(_movePoint.position.x, start.y, _movePoint.position.z);
        Vector3 a = IsMoving ? _startControl : start + PlanarDirection(_bowDirection.forward, transform.forward) * _turnDistance;
        Vector3 b = IsMoving ? _endControl : end - PlanarDirection(_movePoint.forward, end - start) * _turnDistance;
        Gizmos.color = Color.cyan;
        Vector3 previous = start;
        for (int i = 1; i <= 64; i++)
        {
            Vector3 next = Curve(start, a, b, end, (float)i / 64);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawWireSphere(end, 0.8f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, start + PlanarDirection(_bowDirection.forward, transform.forward) * 5f);
        Gizmos.DrawLine(end, end + PlanarDirection(_movePoint.forward, end - start) * 5f);
    }
}
