using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIEditorButtonBinding : MonoBehaviour
{
    [SerializeField] UnityEvent _action = new();
    Button _button;

    public UnityEvent Action => _action;

    void OnEnable()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(InvokeAction);
    }

    void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(InvokeAction);
    }

    void InvokeAction() => _action.Invoke();
}

