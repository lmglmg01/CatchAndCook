using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [Header("이동과 조준 액션")]
    [SerializeField] InputActionReference _moveAction;
    [SerializeField] InputActionReference _aimAction;
    [SerializeField] InputActionReference _sprintAction;
    [SerializeField] InputActionReference _pointerPositionAction;

    [Header("행동 액션")]
    [SerializeField] InputActionReference _attackAction;
    [SerializeField] InputActionReference _interactAction;

    [Header("유틸리티 액션")]
    [SerializeField] InputActionReference _utilitySlot1Action;
    [SerializeField] InputActionReference _utilitySlot2Action;

    [Header("모드 변경 액션")]
    [SerializeField] InputActionReference _meleeModeAction;
    [SerializeField] InputActionReference _rangeModeAction;
    [SerializeField] InputActionReference _toggleRangeModeAction;

    [Header("UI 선택 액션")]
    [SerializeField] InputActionReference _navigateAction;
    [SerializeField] InputActionReference _cancelAction;
    [SerializeField] InputActionReference _smartPhoneAction;

    bool _attackRequested;
    int _lastNavigateDirection;
    bool _enabledNavigate;
    bool _enabledCancel;

    public event Action<Vector2> MoveInput;
    public event Action<bool> AimInput;
    public event Action<bool> SprintInput;
    public event Action<Vector2> AttackInput;
    public event Action InteractInput;
    public event Action<int> UtilityInput;
    public event Action<int> NavigateInput;
    public event Action CancelInput;
    public event Action SmartPhoneInput;
    public event Action MeleeModeInput;
    public event Action RangeModeInput;
    public event Action ToggleRangeModeInput;

    void OnEnable()
    {
        _moveAction?.action?.Enable();
        _aimAction?.action?.Enable();
        _sprintAction?.action?.Enable();
        _pointerPositionAction?.action?.Enable();
        Connect(_attackAction, OnAttack);
        Connect(_interactAction, OnInteract);
        Connect(_utilitySlot1Action, OnUtilitySlot1);
        Connect(_utilitySlot2Action, OnUtilitySlot2);
        Connect(_smartPhoneAction, OnSmartPhone);
        Connect(_meleeModeAction, OnMeleeMode);
        Connect(_rangeModeAction, OnRangeMode);
        Connect(_toggleRangeModeAction, OnToggleRangeMode);
        _enabledNavigate = EnableUIAction(_navigateAction);
        _enabledCancel = EnableUIAction(_cancelAction);
        if (_cancelAction != null && _cancelAction.action != null)
            _cancelAction.action.performed += OnCancel;
    }

    void Update()
    {
        Vector2 movement = _moveAction != null && _moveAction.action != null
            ? _moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        bool aiming = _aimAction != null && _aimAction.action != null && _aimAction.action.IsPressed();
        AimInput?.Invoke(aiming);
        SprintInput?.Invoke(_sprintAction != null && _sprintAction.action != null && _sprintAction.action.IsPressed());
        MoveInput?.Invoke(Vector2.ClampMagnitude(movement, 1f));

        ReadNavigation();

        if (!_attackRequested) return;
        _attackRequested = false;
        if (_pointerPositionAction != null && _pointerPositionAction.action != null)
            AttackInput?.Invoke(_pointerPositionAction.action.ReadValue<Vector2>());
    }

    void OnDisable()
    {
        Disconnect(_attackAction, OnAttack);
        Disconnect(_interactAction, OnInteract);
        Disconnect(_utilitySlot1Action, OnUtilitySlot1);
        Disconnect(_utilitySlot2Action, OnUtilitySlot2);
        Disconnect(_smartPhoneAction, OnSmartPhone);
        Disconnect(_meleeModeAction, OnMeleeMode);
        Disconnect(_rangeModeAction, OnRangeMode);
        Disconnect(_toggleRangeModeAction, OnToggleRangeMode);
        if (_cancelAction != null && _cancelAction.action != null)
            _cancelAction.action.performed -= OnCancel;
        if (_enabledNavigate) _navigateAction?.action?.Disable();
        if (_enabledCancel) _cancelAction?.action?.Disable();
        _moveAction?.action?.Disable();
        _aimAction?.action?.Disable();
        _sprintAction?.action?.Disable();
        _pointerPositionAction?.action?.Disable();
        ResetInput();
    }

    void Connect(InputActionReference reference, Action<InputAction.CallbackContext> callback)
    {
        if (reference == null || reference.action == null) return;
        reference.action.performed += callback;
        reference.action.Enable();
    }

    void Disconnect(InputActionReference reference, Action<InputAction.CallbackContext> callback)
    {
        if (reference == null || reference.action == null) return;
        reference.action.performed -= callback;
        reference.action.Disable();
    }

    bool EnableUIAction(InputActionReference reference)
    {
        if (reference == null || reference.action == null || reference.action.enabled) return false;
        reference.action.Enable();
        return true;
    }

    void ReadNavigation()
    {
        float vertical = _navigateAction != null && _navigateAction.action != null
            ? _navigateAction.action.ReadValue<Vector2>().y : 0f;
        int direction = vertical > 0.5f ? 1 : vertical < -0.5f ? -1 : 0;
        if (direction != 0 && direction != _lastNavigateDirection) NavigateInput?.Invoke(direction);
        _lastNavigateDirection = direction;
    }

    void OnSmartPhone(InputAction.CallbackContext context) => SmartPhoneInput?.Invoke();
    void OnCancel(InputAction.CallbackContext context) => CancelInput?.Invoke();
    void OnAttack(InputAction.CallbackContext context) => _attackRequested = true;
    void OnInteract(InputAction.CallbackContext context) => InteractInput?.Invoke();
    void OnUtilitySlot1(InputAction.CallbackContext context) => UtilityInput?.Invoke(0);
    void OnUtilitySlot2(InputAction.CallbackContext context) => UtilityInput?.Invoke(1);

    public string GetUtilityBindingDisplayString(int slotIndex)
    {
        InputAction action = slotIndex == 0 ? _utilitySlot1Action?.action : slotIndex == 1 ? _utilitySlot2Action?.action : null;
        return action != null && action.bindings.Count > 0 ? action.GetBindingDisplayString(0) : string.Empty;
    }
    void OnMeleeMode(InputAction.CallbackContext context) => MeleeModeInput?.Invoke();
    void OnRangeMode(InputAction.CallbackContext context) => RangeModeInput?.Invoke();
    void OnToggleRangeMode(InputAction.CallbackContext context) => ToggleRangeModeInput?.Invoke();

    void OnApplicationFocus(bool focused)
    {
        if (!focused) ResetInput();
    }

    void ResetInput()
    {
        _attackRequested = false;
        _lastNavigateDirection = 0;
        SprintInput?.Invoke(false);
        AimInput?.Invoke(false);
        MoveInput?.Invoke(Vector2.zero);
    }
}
