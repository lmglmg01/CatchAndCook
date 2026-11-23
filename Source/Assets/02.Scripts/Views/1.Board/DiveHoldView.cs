using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>잠수 버튼의 길게 누르기 표시와 완료 요청만 담당합니다. 게임 진행 조건은 기존 연결에 맡깁니다.</summary>
public class DiveHoldView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] UnityEngine.UI.Button _button;
    [SerializeField] UnityEngine.UI.Image _fill;
    [SerializeField] InputActionReference _interactAction;
    [SerializeField, Min(1f)] float _holdSeconds = 1f;
    [SerializeField] string _previewNextScene = "Dive"; // PlayScene이 없는 UI 테스트 씬에서만 사용합니다.
    float _elapsed;
    bool _pointerHeld, _completed, _buttonWasEnabled, _restoreAction, _awaitRelease;
    InputAction _ownedAction;
    Coroutine _completion;
    public float Progress => _fill != null ? _fill.fillAmount : 0f;

    /// <summary>일반 클릭에 의한 즉시 이동을 막고 길게 누르기 입력만 사용합니다.</summary>
    void OnEnable()
    {
        if (_button == null) _button = GetComponent<UnityEngine.UI.Button>();
        _buttonWasEnabled = _button != null && _button.enabled;
        if (_button != null) _button.enabled = false;
        ResetHold();
        if (CanHold()) AcquireInput();
    }

    /// <summary>화면에 표시된 동안 연결된 상호작용 키의 실제 눌림 시간을 누적합니다.</summary>
    void Update()
    {
        if (!CanHold()) { CancelHold(); ReleaseInput(); return; }
        AcquireInput();
        bool held = _pointerHeld || IsInteractionPressed();
        if (_awaitRelease) { if (!held) _awaitRelease = false; return; }
        if (_completed) { if (!held && _completion == null) ResetHold(); return; }
        if (!held) { ResetHold(); return; }
        _elapsed += Time.unscaledDeltaTime;
        if (_fill != null) _fill.fillAmount = Mathf.Clamp01(_elapsed / Mathf.Max(1f, _holdSeconds));
        if (_elapsed >= Mathf.Max(1f, _holdSeconds))
        {
            _completed = true;
            _completion = StartCoroutine(CompleteAfterFrame());
        }
    }

    /// <summary>완료된 테두리를 한 프레임 표시한 뒤 기존 잠수 클릭 연결을 한 번 호출합니다.</summary>
    IEnumerator CompleteAfterFrame()
    {
        yield return null;
        _completion = null;
        if (!CanHold()) { CancelHold(); yield break; }
        var scenes = FindObjectsByType<PlayScene>(FindObjectsSortMode.None);
        foreach (var scene in scenes)
        {
            if (scene.gameObject.scene == gameObject.scene)
            {
                _button.onClick.Invoke(); // GameplayViewConnector가 거리·잠수 가능 여부를 검사하고 기존 다음 씬으로 이동합니다.
                yield break;
            }
        }
        if (!string.IsNullOrEmpty(_previewNextScene) && Application.CanStreamedLevelBeLoaded(_previewNextScene))
            SceneManager.LoadSceneAsync(_previewNextScene);
        else
            Debug.LogWarning("잠수 미리보기의 다음 씬이 빌드 목록에 없습니다.", this);
    }

    /// <summary>숨김·모달 창·일시정지 상태에서는 채우기와 이동을 허용하지 않습니다.</summary>
    bool CanHold()
    {
        if (_button == null || !_button.interactable || Time.timeScale <= 0f) return false;
        foreach (var group in GetComponentsInParent<CanvasGroup>())
            if (group.alpha <= .001f || !group.interactable || !group.blocksRaycasts) return false;
        return true;
    }

    /// <summary>기존 액션의 짧은 Hold가 먼저 씬을 이동시키지 않도록 잠수 UI가 입력을 잠시 소유합니다.</summary>
    void AcquireInput()
    {
        var action = _interactAction != null ? _interactAction.action : null;
        if (action == null) return;
        if (_ownedAction == null)
        {
            _ownedAction = action;
            _restoreAction = action.enabled;
            _awaitRelease = IsInteractionPressed(); // 이미 누른 채로 범위에 들어온 입력은 새 누름으로 처리하지 않습니다.
        }
        if (action.enabled) { _restoreAction = true; action.Disable(); }
    }

    /// <summary>입력 자산의 바인딩을 그대로 읽으므로 E키와 게임패드 및 재설정된 키를 지원합니다.</summary>
    bool IsInteractionPressed()
    {
        var action = _interactAction != null ? _interactAction.action : null;
        if (action == null) return false;
        foreach (var control in action.controls)
            if (control is ButtonControl button && button.isPressed) return true;
        return false;
    }

    /// <summary>잠수 UI가 입력 소유권을 놓을 때 원래 활성 상태를 복원합니다.</summary>
    void ReleaseInput()
    {
        if (_ownedAction != null && _restoreAction) _ownedAction.Enable();
        _ownedAction = null; _restoreAction = false;
    }

    /// <summary>버튼에서 마우스를 누른 경우에도 동일한 1초 누르기를 시작합니다.</summary>
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && CanHold()) _pointerHeld = true; }
    /// <summary>마우스를 떼면 입력을 해제합니다.</summary>
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) _pointerHeld = false; }
    /// <summary>버튼 밖으로 이동하면 마우스 누르기를 취소합니다.</summary>
    public void OnPointerExit(PointerEventData e) { _pointerHeld = false; }
    /// <summary>포커스를 잃으면 진행과 예약된 이동을 취소합니다.</summary>
    void OnApplicationFocus(bool focused) { if (!focused) { CancelHold(); _awaitRelease = true; } }
    /// <summary>누적 시간과 채움 표시를 초기화합니다.</summary>
    void ResetHold() { _elapsed = 0f; _completed = false; if (_fill != null) _fill.fillAmount = 0f; }
    /// <summary>미완료 입력과 다음 프레임으로 예약된 이동을 함께 취소합니다.</summary>
    void CancelHold() { if (_completion != null) StopCoroutine(_completion); _completion = null; _pointerHeld = false; ResetHold(); }
    /// <summary>버튼이 숨겨지거나 씬이 바뀌면 입력과 버튼 상태를 복원합니다.</summary>
    void OnDisable() { CancelHold(); ReleaseInput(); if (_button != null) _button.enabled = _buttonWasEnabled; }
}
