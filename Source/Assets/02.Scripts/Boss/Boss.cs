using UnityEngine;

[RequireComponent(typeof(BossRunModel))]
public class Boss : MonoBehaviour, IDamageable
{
    [Header("추격 대상 및 컴포넌트")]
    [SerializeField] Hero _hero;
    [SerializeField] Mover _mover;
    [SerializeField] BossRunModel _runModel;
    [SerializeField] Animator _animator;
    [SerializeField] Transform _visualRoot;
    [SerializeField] Transform _head;

    [Header("추격 제어")]
    [SerializeField, InspectorName("추격 정지")] bool _stopChasing;

    [Header("선회 및 접근")]
    [SerializeField, Min(0f)] float _turnSpeed;
    [SerializeField, Min(0f)] float _stopDistance;
    [SerializeField, Min(0f)] float _resumeDistance;
    [SerializeField, Min(0f)] float _slowDistance;
    [SerializeField, Range(0f, 1f)] float _turnMoveRatio;

    static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
    Quaternion _baseRotation;
    Vector3 _headAnchor;
    float _heading;
    float _visualYaw;
    float _visualPitch;
    bool _facingLeft;
    bool _initialized;
    bool _chasing = true;
    bool _nearTarget;
    public BossRunModel RunModel => _runModel != null ? _runModel : (_runModel = GetComponent<BossRunModel>());
    public float CurrentHealth => RunModel != null ? RunModel.CurrentHealth : 0f;
    public bool CanReceiveDamage => isActiveAndEnabled && RunModel != null && RunModel.IsInitialized && !RunModel.IsDead;

    public void TakeDamage(float damage)
    {
        if (CanReceiveDamage) RunModel.TakeDamage(damage);
    }

    void Awake()
    {
        if (_mover == null) _mover = GetComponent<Mover>();
        if (_runModel == null) _runModel = GetComponent<BossRunModel>();
        if (_visualRoot != null) _baseRotation = _visualRoot.localRotation;
        if (_head != null) _headAnchor = transform.InverseTransformPoint(_head.position);
    }

    void Start()
    {
        if (!_initialized && _hero != null) Initialize(_hero);
    }

    public void Initialize(Hero hero)
    {
        if (_runModel == null) _runModel = GetComponent<BossRunModel>();
        _hero = hero;
        _initialized = _runModel != null && (_runModel.IsInitialized || _runModel.Initialize());
        _nearTarget = false;
    }

    // 공격·스턴 상태 진입 시 false, 추격 재개 시 true를 전달한다.
    public void SetChasing(bool chasing)
    {
        _chasing = chasing;
        if (!chasing) StopMoving();
    }

    void Update()
    {
        if (!_initialized || _hero == null || _mover == null || _runModel.IsDead || !_chasing || _stopChasing)
        {
            StopMoving();
            return;
        }
        // 공격 모션 중에는 추격 회전으로 자세를 덮어쓰지 않는다.
        if (_animator != null)
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0) || (!state.IsName("Idle") && !state.IsName("Swim")))
            {
                StopMoving();
                return;
            }
        }
        Vector3 origin = transform.TransformPoint(_headAnchor);
        Vector2 delta = _hero.transform.position - origin;
        float distance = delta.magnitude;
        if (_nearTarget) _nearTarget = distance < Mathf.Max(_stopDistance, _resumeDistance);
        else _nearTarget = distance <= _stopDistance;
        if (_nearTarget) { StopMoving(); return; }

        float desired = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        _heading = Mathf.MoveTowardsAngle(_heading, desired, _turnSpeed * Time.deltaTime);
        float radians = _heading * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        float alignment = Mathf.Clamp01(Vector2.Dot(direction, delta.normalized));
        float approach = Mathf.Clamp01((distance - _stopDistance) / Mathf.Max(.01f, _slowDistance));
        float speed = _runModel.MoveSpeed * Mathf.Lerp(_turnMoveRatio, 1f, alignment) * approach;
        _mover.Move(direction, speed);
        if (_animator != null) _animator.SetFloat(MoveSpeedId, speed);
    }

    void LateUpdate()
    {
        if (!_initialized || !_chasing || _stopChasing || _runModel.IsDead || _visualRoot == null || _head == null) return;
        if (_animator != null)
        {
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0) || (!state.IsName("Idle") && !state.IsName("Swim"))) return;
        }
        // 긴 모델의 중앙이 아니라 머리를 중심으로 선회한다.
        Vector3 anchor = transform.TransformPoint(_headAnchor);
        float radians = _heading * Mathf.Deg2Rad;
        float horizontal = Mathf.Cos(radians);
        // 수직 방향 부근에서 좌우 전환이 반복되지 않도록 여유 구간을 둔다.
        if (horizontal < -0.15f) _facingLeft = true;
        else if (horizontal > 0.15f) _facingLeft = false;
        // 머리 중심 뒤의 몸통·꼬리가 배경(+Z)으로 돌도록 0~180도 경로를 고정한다.
        float targetYaw = _facingLeft ? 180f : 0f;
        float targetPitch = Mathf.Clamp(Mathf.Asin(Mathf.Sin(radians)) * Mathf.Rad2Deg, -75f, 75f);
        _visualYaw = Mathf.MoveTowards(_visualYaw, targetYaw, _turnSpeed * Time.deltaTime);
        _visualPitch = Mathf.MoveTowards(_visualPitch, targetPitch, _turnSpeed * Time.deltaTime);
        // 좌우는 Y축, 상하는 제한된 기울기로 분리해 배가 위로 뒤집히지 않게 한다.
        _visualRoot.localRotation = Quaternion.AngleAxis(_visualYaw, Vector3.up)
            * Quaternion.AngleAxis(_visualPitch, Vector3.forward) * _baseRotation;
        _visualRoot.position += anchor - _head.position;
    }

    void StopMoving()
    {
        if (_mover != null) _mover.Move(Vector2.zero, 0f);
        if (_animator != null) _animator.SetFloat(MoveSpeedId, 0f);
    }

    void OnDisable() { StopMoving(); }
}
