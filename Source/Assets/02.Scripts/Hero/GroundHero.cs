using UnityEngine;

[RequireComponent(typeof(Mover))]
public class GroundHero : Hero
{
    [Header("이동")]
    [SerializeField, Min(0f)] float _moveSpeed;

    [Header("방향 전환")]
    [SerializeField] Transform _model;
    [SerializeField, Min(0f), Tooltip("좌우 방향 전환 시간(초). 0이면 즉시 회전합니다.")] float _turnDuration;

    [Header("상호작용")]
    [SerializeField] InteractableSensor _interactableSensor;

    [Header("참조")]
    [SerializeField] SushiCarrier _sushiCarrier;        // 스시 운반 클래스

    
    public SushiCarrier SushiCarrier => _sushiCarrier;  // 스시 운반 프로퍼티
    // 음료 미니게임의 준비, 진행 및 결과 표시 중 일반 조작을 제한합니다.
    internal bool IsDrinkMinigameOpen { get; set; }

    public event System.Action<IInteractable> InteractionTargetChanged;
    public IInteractable InteractionTarget => _interactionTarget;

    IInteractable _interactionTarget;
    Quaternion _turnStartRotation;
    Quaternion _targetRotation;
    float _turnElapsed;
    bool _isTurning;

    protected override bool HorizontalOnly => true;

    protected override void OnEnable()
    {
        base.OnEnable();
        _isTurning = false;
        if (_model != null) _targetRotation = _model.localRotation;
    }

    public override void SetMoveInput(Vector2 direction)
    {
        // 입력 시스템의 이동 값도 미니게임 중에는 정지로 바꿉니다.
        if (IsDrinkMinigameOpen) direction = Vector2.zero;
        if (!isActiveAndEnabled) return;

        float horizontal = direction.x == 0f ? 0f : Mathf.Sign(direction.x);
        if (_model != null && horizontal != 0f)
        {
            Quaternion target = Quaternion.LookRotation(Vector3.right * horizontal, Vector3.up);
            if (Quaternion.Angle(_targetRotation, target) > 0.01f)
            {
                _turnStartRotation = _model.localRotation;
                _targetRotation = target;
                _turnElapsed = 0f;
                _isTurning = true;
            }
        }

        MoveHero(new Vector2(horizontal, 0f), _moveSpeed);
    }

    protected override void OnDisable()
    {
        SetInteractionTarget(null);
        base.OnDisable();
    }

    void Update()
    {
        UpdateRotation(Time.deltaTime);
        RefreshInteractionTarget();
    }

    void UpdateRotation(float deltaTime)
    {
        if (!_isTurning || _model == null) return;

        _turnElapsed += deltaTime;
        float progress = _turnDuration > 0f ? Mathf.Clamp01(_turnElapsed / _turnDuration) : 1f;
        _model.localRotation = Quaternion.Slerp(_turnStartRotation, _targetRotation,
            Mathf.SmoothStep(0f, 1f, progress));
        if (progress >= 1f) _isTurning = false;
    }

    IInteractable RefreshInteractionTarget()
    {
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

        _interactionTarget = target;

        if (_interactionTarget is InteractObject current && current != null)
            current.SetDetected(true);

        InteractionTargetChanged?.Invoke(_interactionTarget);
    }

    public void Interact()
    {
        // 미니게임의 F 연타가 월드 상호작용으로 중복 소비되지 않게 합니다.
        if (IsDrinkMinigameOpen) return;
        if (!isActiveAndEnabled) return;

        IInteractable target = RefreshInteractionTarget();
        if (target == null) return;

        target.Interact(this);
        RefreshInteractionTarget();
    }
}
