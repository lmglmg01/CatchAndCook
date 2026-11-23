using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class ShipReturn : MonoBehaviour, IInteractable
{
    [Header("수면 복귀")]
    [SerializeField] DivePlayScene _diveScene;
    [SerializeField] Collider _heroCollider;
    [SerializeField] InputActionReference _interactAction;
    [SerializeField, Min(0.01f)] float _holdSeconds;

    [Header("캐릭터 복귀 안내")]
    [SerializeField] Canvas _prompt;
    [SerializeField] Image _fill;
    [SerializeField] TMP_Text _keyLabel;

    bool _inSurface;
    bool _awaitRelease;
    float _heldSeconds;

    public event Action ReturnRequested;

    void Awake() => HidePrompt();

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    // 수면 복귀 UI가 연결된 씬은 트리거와 길게 누르기로만 복귀한다.
    public bool CanInteract(Hero interactor)
    {
        return _prompt == null && isActiveAndEnabled && interactor != null && interactor.isActiveAndEnabled;
    }

    public void Interact(Hero interactor)
    {
        if (!CanInteract(interactor)) return;
        ReturnRequested?.Invoke();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isActiveAndEnabled && other == _heroCollider) _inSurface = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other == _heroCollider) ClearContact();
    }

    void Update()
    {
        if (!_inSurface || _prompt == null) return;
        if (!_heroCollider.enabled || !_heroCollider.gameObject.activeInHierarchy)
        {
            ClearContact();
            return;
        }

        var action = _interactAction != null ? _interactAction.action : null;
        if (_diveScene == null || !_diveScene.CanUseSurfaceReturn || action == null || !action.enabled)
        {
            HidePrompt();
            return;
        }

        bool held = action.IsPressed();
        if (!_prompt.gameObject.activeSelf)
        {
            if (_keyLabel != null) _keyLabel.text = action.GetBindingDisplayString();
            _awaitRelease = held;
            _prompt.gameObject.SetActive(true);
        }

        if (!held)
        {
            _awaitRelease = false;
            ResetProgress();
            return;
        }

        if (_awaitRelease) return;
        _heldSeconds += Time.deltaTime;
        _fill.fillAmount = Mathf.Clamp01(_heldSeconds / _holdSeconds);
        if (_fill.fillAmount < 1f) return;

        _awaitRelease = true;
        ReturnRequested?.Invoke();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        _awaitRelease = true;
        ResetProgress();
    }

    void OnDisable() => ClearContact();

    void ClearContact()
    {
        _inSurface = false;
        HidePrompt();
    }

    void HidePrompt()
    {
        if (_prompt != null && _prompt.gameObject.activeSelf) _prompt.gameObject.SetActive(false);
        ResetProgress();
    }

    void ResetProgress()
    {
        _heldSeconds = 0f;
        if (_fill != null) _fill.fillAmount = 0f;
    }
}
