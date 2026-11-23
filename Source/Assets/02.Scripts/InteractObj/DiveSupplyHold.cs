using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DiveSupplyHold : MonoBehaviour
{
    [Header("보급 안내")]
    [SerializeField] Canvas _prompt;
    [SerializeField] Image _fill;
    [SerializeField] TMP_Text _keyLabel;

    [Header("보급 입력")]
    [SerializeField] InputActionReference _interactAction;
    [SerializeField, Min(0.01f)] float _holdSeconds;

    InteractObject _supply;
    float _elapsed;
    bool _awaitRelease;

    internal bool CanUse(UnderSeaHero hero) => _supply != null && _supply.CanInteract(hero);

    void Awake()
    {
        _supply = GetComponent<InteractObject>();
        Cancel();
    }

    internal void Tick(UnderSeaHero hero)
    {
        var action = _interactAction.action;
        if (GameManager.Instance == null || GameManager.Instance.CurrentPlayScene is not DivePlayScene scene
            || !scene.CanUseSupplies || !action.enabled || !_supply.CanInteract(hero))
        {
            Cancel();
            return;
        }

        bool held = action.IsPressed();
        if (!_prompt.gameObject.activeSelf)
        {
            _keyLabel.text = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
            _awaitRelease = held;
            _prompt.gameObject.SetActive(true);
            _supply.SetDetected(true);
        }

        if (!held)
        {
            _awaitRelease = false;
            if (_elapsed > 0f) ResetProgress();
            return;
        }
        if (_awaitRelease) return;

        _elapsed += Time.deltaTime;
        _fill.fillAmount = Mathf.Clamp01(_elapsed / _holdSeconds);
        if (_elapsed < _holdSeconds) return;

        _supply.Interact(hero);
        Cancel();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) Cancel();
    }

    void OnDisable() => Cancel();

    internal void Cancel()
    {
        if (_prompt != null) _prompt.gameObject.SetActive(false);
        ResetProgress();
    }

    void ResetProgress()
    {
        _elapsed = 0f;
        if (_fill != null) _fill.fillAmount = 0f;
    }
}
