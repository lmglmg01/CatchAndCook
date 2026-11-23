using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>물고기 저항 수치를 전달받아 원본 곡선과 이동 표시만 갱신합니다. 판정과 입력은 기존 담당자가 처리합니다.</summary>
[DisallowMultipleComponent]
public sealed class FishResistanceGaugeView : MonoBehaviour
{
    #region 표시 연결
    [SerializeField] UnityEngine.UI.Image _backplate, _gradient, _marker, _key;
    [SerializeField] UnityEngine.InputSystem.InputActionReference _interactAction;
    [SerializeField] TMPro.TMP_Text _keyText;
    [SerializeField] Vector3[] _curveSamples; // 아래에서 위 순서의 정규화된 중심 X·Y·너비입니다.
    [Header("캐릭터 등 뒤 배치")]
    [Tooltip("몸통과 게이지 사이에 추가할 간격 (Canvas 단위)")]
    [SerializeField, Min(0f)] float _backSpacing;
    float _size, _progress;
    public float Progress => _progress;
    #endregion

    void OnEnable()
    {
        UnityEngine.InputSystem.InputSystem.onActionChange += OnActionChanged;
        RefreshKeyPrompt();
    }
    void OnDisable() => UnityEngine.InputSystem.InputSystem.onActionChange -= OnActionChanged;
    void OnActionChanged(object source, UnityEngine.InputSystem.InputActionChange change)
    {
        if (change == UnityEngine.InputSystem.InputActionChange.BoundControlsChanged) RefreshKeyPrompt();
    }
    /// <summary>실제 키 바인딩을 읽어 Space만 그림으로, 나머지 키는 이름·방향 기호로 표시합니다.</summary>
    public void RefreshKeyPrompt()
    {
        if (_key == null || _keyText == null) return;
        var action = _interactAction != null ? _interactAction.action
            : UnityEngine.InputSystem.InputSystem.actions?.FindAction("Hero/Interact", false);
        string path = null, label = "—";
        if (action != null)
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)
                    || !binding.effectivePath.StartsWith("<Keyboard>/", System.StringComparison.OrdinalIgnoreCase)) continue;
                path = binding.effectivePath;
                label = action.GetBindingDisplayString(i);
                string key = path.Substring(path.LastIndexOf('/') + 1).ToLowerInvariant();
                label = key switch { "uparrow" => "↑", "downarrow" => "↓", "leftarrow" => "←", "rightarrow" => "→", _ => label };
                break;
            }
        bool space = string.Equals(path, "<Keyboard>/space", System.StringComparison.OrdinalIgnoreCase);
        _key.enabled = space;
        _keyText.text = string.IsNullOrWhiteSpace(label) ? "—" : label;
        _keyText.enabled = !space;
    }

    #region 외부 표시 요청
    /// <summary>기존 게이지 크기를 사용해 곡선과 왼쪽 키 아이콘을 배치합니다.</summary>
    public void Configure(float size)
    {
        RefreshKeyPrompt();
        if (_size == size || size <= 0f || _backplate == null || _gradient == null || _marker == null || _key == null) return;
        _size = size;
        Place(_backplate.rectTransform, new Vector2(-size * .35f, 0), Vector2.one * size);
        Place(_gradient.rectTransform, new Vector2(-size * .35f, 0), Vector2.one * size);
        Place(_key.rectTransform, new Vector2(-size * .86f, size * .1f), new Vector2(size * .22f, size * .16f));
        if (_keyText != null) Place(_keyText.rectTransform, _key.rectTransform.anchoredPosition, _key.rectTransform.sizeDelta);
        foreach (var image in new[] { _backplate, _gradient, _marker, _key })
        {
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
        }
        SetProgress(_progress);
    }

    /// <summary>몸통의 화면 위치를 기준으로 원호와 표시 막대를 등 쪽에 놓고 키 아이콘은 바르게 유지합니다.</summary>
    public void PlaceBehind(Vector2 bodyPosition, bool facingScreenLeft)
    {
        float direction = facingScreenLeft ? -1f : 1f;
        transform.localPosition = bodyPosition + Vector2.left * (direction * _backSpacing);
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        if (transform.localScale != scale) transform.localScale = scale;
        if (_key != null)
        {
            Vector3 keyScale = _key.rectTransform.localScale;
            keyScale.x = Mathf.Abs(keyScale.x) * direction;
            if (_key.rectTransform.localScale != keyScale) _key.rectTransform.localScale = keyScale;
            if (_keyText != null) _keyText.rectTransform.localScale = keyScale;
        }
    }

    /// <summary>기존 저항 비율을 받아 파란 표시를 빨간색 하단부터 민트색 상단까지 이동시킵니다.</summary>
    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp01(progress);
        if (_size <= 0 || _curveSamples == null || _curveSamples.Length < 2 || _marker == null) return;
        float sample = _progress * (_curveSamples.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(sample), _curveSamples.Length - 2);
        Vector3 point = Vector3.Lerp(_curveSamples[index], _curveSamples[index + 1], sample - index);
        // 원본 1000px 캔버스 안 파란 막대의 실제 영역은 x302~506, y703~730입니다.
        // PNG를 잘라내지 않고 투명 여백까지 보존한 상태로 막대의 중심과 너비를 맞춥니다.
        float width = _size * point.z * 1000f / 204f;
        var rect = _marker.rectTransform;
        Place(rect, new Vector2((point.x - .5f) * _size - (404f / 1000f - .5f) * width,
            (point.y - .5f) * _size - (716.5f / 1000f - .5f) * _size), new Vector2(width, _size));
    }
    #endregion

    #region 배치 보조
    /// <summary>입력이나 상위 위치 추적을 바꾸지 않고 표시용 사각형의 로컬 배치만 맞춥니다.</summary>
    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }
    #endregion
}

