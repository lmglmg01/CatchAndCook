using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class TutorialView : MonoBehaviour
{
    [Header("안내 문구")]
    [SerializeField] GameObject _instructionPanel;
    [SerializeField] TMP_Text _instructionText;

    [Header("안내 문구 크기 연출")]
    [SerializeField, Min(0.01f)] float _instructionMinScale;
    [SerializeField, Min(0.01f)] float _instructionMaxScale;
    [SerializeField, Min(0f), Tooltip("최소 크기에서 커졌다가 다시 최소 크기로 돌아오기까지 걸리는 초입니다. 0이면 연출을 끕니다.")]
    float _instructionPulsePeriod;

    [Header("안내 문구 외곽선")]
    [SerializeField] Color _instructionOutlineColor;
    [SerializeField, Range(0f, 1f)] float _instructionOutlineWidth;

    Material _instructionMaterial;
    Tween _instructionPulse;
    Vector3 _instructionOriginalScale;

    [Header("슬롯 강조")]
    [SerializeField] Color _highlightColor;
    [SerializeField] float _highlightWidth;
    [Header("단일 대상 강조: 첫 패널로 화면 전체 가림")]
    [SerializeField] UnityEngine.UI.Image[] _focusPanels;
    [SerializeField] Color _focusDimColor;
    [SerializeField, Range(0f, 0.3f)] float _focusPulseAmount;
    [SerializeField, Min(0f)] float _focusPulseDuration;
    [SerializeField] bool _focusBlocksOtherClicks;

    RectTransform _focusTarget;
    Vector3 _focusOriginalScale;
    float _focusElapsed;
    Image _focusPage, _focusBookmark;
    Image _bookmarkMask, _pageMask, _bookmarkMaskReset;
    Material _bookmarkMaskMaterial, _pageMaskMaterial, _bookmarkDimMaterial, _regularDimMaterial;
    const int BookmarkStencilBit = 1 << 7;
    FocusCanvasState _focusCanvas;
    UnityEngine.UI.GraphicRaycaster _addedFocusRaycaster;
    readonly System.Collections.Generic.Dictionary<RectTransform, FocusCanvasState> _focusCanvases = new();

    // 원래 버튼의 그림과 입력을 가림막 위에 표시하고, 강조가 끝나면 설정을 복원한다.
    sealed class FocusCanvasState
    {
        public Canvas Canvas;
        public UnityEngine.UI.GraphicRaycaster Raycaster;
        public bool AddedCanvas, AddedRaycaster;
        public bool ReleasePending;
        public bool WasEnabled, WasOverrideSorting, WasRaycasterEnabled;
        public int SortingLayer, SortingOrder;
    }

    // 안내 문구를 표시한 뒤 호출한다. 대상은 복제하지 않고 원래 버튼을 그대로 사용한다.
    public void ShowFocus(RectTransform target) => ShowFocus(target, null);

    internal void ShowFocus(RectTransform target, Image page)
    {
        HideFocus();
        if (target == null) return;
        _focusTarget = target;
        _focusOriginalScale = target.localScale;
        _focusPage = page;
        _focusBookmark = page != null ? target.GetComponent<Image>() : null;
        _focusElapsed = 0f;
        UpdateFocus();
    }

    public void HideFocus()
    {
        if (_focusTarget != null) _focusTarget.localScale = _focusOriginalScale;
        RestoreFocusCanvas();
        _focusTarget = null;
        _focusPage = _focusBookmark = null;
        if (_bookmarkMask != null)
        {
            _bookmarkMask.gameObject.SetActive(false);
            _pageMask.gameObject.SetActive(false);
            _bookmarkMaskReset.gameObject.SetActive(false);
            if (_focusPanels[0] != null) _focusPanels[0].material = _regularDimMaterial;
        }
        SetFocusPanelsVisible(false);
    }

    void RaiseFocusTarget()
    {
        var overlayCanvas = GetComponentInParent<Canvas>();
        if (overlayCanvas == null || _focusPanels == null || _focusPanels.Length == 0 || _focusPanels[0] == null) return;
        if (overlayCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            _addedFocusRaycaster = overlayCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        if (_focusCanvases.TryGetValue(_focusTarget, out var state) && state.ReleasePending)
        {
            // Destroy는 프레임 끝에 처리되므로 같은 버튼을 다시 강조하면 제거 완료를 기다린다.
            if ((state.AddedCanvas && state.Canvas != null) ||
                (state.AddedRaycaster && state.Raycaster != null)) return;
            _focusCanvases.Remove(_focusTarget);
            state = null;
        }
        if (state == null)
        {
            state = new FocusCanvasState();
            state.Canvas = _focusTarget.GetComponent<Canvas>();
            state.AddedCanvas = state.Canvas == null;
            if (state.AddedCanvas) state.Canvas = _focusTarget.gameObject.AddComponent<Canvas>();
            state.Raycaster = _focusTarget.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            state.AddedRaycaster = state.Raycaster == null;
            if (state.AddedRaycaster) state.Raycaster = _focusTarget.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            _focusCanvases.Add(_focusTarget, state);
        }
        if (state.Canvas == null || state.Raycaster == null) return;
        state.WasEnabled = state.Canvas.enabled;
        state.WasOverrideSorting = state.Canvas.overrideSorting;
        state.SortingLayer = state.Canvas.sortingLayerID;
        state.SortingOrder = state.Canvas.sortingOrder;
        state.WasRaycasterEnabled = state.Raycaster.enabled;
        _focusCanvas = state;
        state.Canvas.enabled = true;
        state.Canvas.overrideSorting = true;
        state.Canvas.sortingLayerID = overlayCanvas.sortingLayerID;
        state.Canvas.sortingOrder = overlayCanvas.sortingOrder + 1;
        state.Raycaster.enabled = true;
    }

    void RestoreFocusCanvas()
    {
        if (_focusCanvas == null) return;
        var state = _focusCanvas;
        if (state.Canvas != null)
        {
            state.Canvas.sortingLayerID = state.SortingLayer;
            state.Canvas.sortingOrder = state.SortingOrder;
            state.Canvas.overrideSorting = state.WasOverrideSorting;
            if (!state.AddedCanvas) state.Canvas.enabled = state.WasEnabled;
        }
        if (state.Raycaster != null)
        {
            if (state.AddedRaycaster) Destroy(state.Raycaster);
            else state.Raycaster.enabled = state.WasRaycasterEnabled;
        }
        // 비활성 Canvas를 버튼에 남기면 원래 부모 Canvas에서 아이콘과 입력이 빠질 수 있다.
        if (state.AddedCanvas && state.Canvas != null) Destroy(state.Canvas);
        state.ReleasePending = state.AddedCanvas || state.AddedRaycaster;
        if (!state.ReleasePending) _focusCanvases.Remove(_focusTarget);
        _focusCanvas = null;
    }

    void SetFocusPanelsVisible(bool visible)
    {
        if (_focusPanels == null) return;
        foreach (var panel in _focusPanels)
            if (panel != null) panel.gameObject.SetActive(visible);
    }

    void UpdateFocus()
    {
        if (_focusTarget == null || !_focusTarget.gameObject.activeInHierarchy)
        {
            HideFocus();
            return;
        }
        if (!(transform is RectTransform parent)) return;
        if (_focusPage == null && _focusCanvas == null) RaiseFocusTarget();
        _focusElapsed += Time.unscaledDeltaTime;
        float pulse = _focusPulseDuration > 0f
            ? 0.5f - 0.5f * Mathf.Cos(_focusElapsed / _focusPulseDuration * Mathf.PI * 2f) : 0f;
        _focusTarget.localScale = _focusOriginalScale * (1f + _focusPulseAmount * pulse);

        if (_focusPage != null)
        {
            UpdateBookmarkDim(parent);
            return;
        }

        if (_focusCanvas == null) return;
        // 사각형 구멍을 뚫지 않아 Sprite의 투명 모서리와 여백도 같은 농도로 어두워진다.
        Rect rect = parent.rect;
        SetFocusPanel(0, rect.min, rect.max);
        for (int i = 1; i < _focusPanels.Length; i++)
            if (_focusPanels[i] != null) _focusPanels[i].gameObject.SetActive(false);
    }

    void UpdateBookmarkDim(RectTransform parent)
    {
        if (_focusPanels == null || _focusPanels.Length == 0 || _focusPanels[0] == null) return;
        var panel = _focusPanels[0];
        if (_bookmarkMask == null)
        {
            _regularDimMaterial = panel.material;
            var material = Image.defaultGraphicMaterial;
            _bookmarkMaskMaterial = StencilMaterial.Add(material, BookmarkStencilBit,
                StencilOp.Replace, CompareFunction.Always, 0, BookmarkStencilBit, BookmarkStencilBit);
            _pageMaskMaterial = StencilMaterial.Add(material, BookmarkStencilBit,
                StencilOp.Zero, CompareFunction.Always, 0, BookmarkStencilBit, BookmarkStencilBit);
            _bookmarkDimMaterial = StencilMaterial.Add(_regularDimMaterial, BookmarkStencilBit,
                StencilOp.Keep, CompareFunction.NotEqual, ColorWriteMask.All, BookmarkStencilBit, 0);
            _bookmarkMask = CreateBookmarkMask("BookmarkFocusMask", _bookmarkMaskMaterial, panel.transform.GetSiblingIndex());
            _pageMask = CreateBookmarkMask("BookmarkPageMask", _pageMaskMaterial, panel.transform.GetSiblingIndex());
            _bookmarkMaskReset = CreateBookmarkMask("BookmarkFocusMaskReset", _pageMaskMaterial, panel.transform.GetSiblingIndex() + 1);
        }

        // 책갈피의 스프라이트에서 책 페이지가 덮는 부분을 제외하고 가림막을 그린다.
        CopyBookmarkMask(_bookmarkMask, _focusBookmark, parent);
        CopyBookmarkMask(_pageMask, _focusPage, parent);
        CopyBookmarkMask(_bookmarkMaskReset, _focusBookmark, parent);
        SetFocusPanel(0, parent.rect.min, parent.rect.max);
        panel.material = _bookmarkDimMaterial;
        // 책갈피는 원래 캔버스에서 입력을 받으며 다른 버튼은 튜토리얼이 잠근다.
        panel.raycastTarget = false;
    }

    Image CreateBookmarkMask(string name, Material material, int siblingIndex)
    {
        var mask = new GameObject(name, typeof(RectTransform), typeof(Image));
        mask.SetActive(false);
        mask.transform.SetParent(transform, false);
        mask.transform.SetSiblingIndex(siblingIndex);
        var image = mask.GetComponent<Image>();
        image.material = material;
        image.maskable = false;
        image.raycastTarget = false;
        return image;
    }

    void CopyBookmarkMask(Image mask, Image source, RectTransform parent)
    {
        bool visible = source != null && source.isActiveAndEnabled && source.overrideSprite != null;
        mask.gameObject.SetActive(visible);
        if (!visible) return;

        mask.sprite = source.overrideSprite;
        mask.type = source.type;
        mask.preserveAspect = source.preserveAspect;
        mask.useSpriteMesh = source.useSpriteMesh;
        mask.fillCenter = source.fillCenter;
        mask.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;

        var from = source.rectTransform;
        var to = mask.rectTransform;
        to.anchorMin = to.anchorMax = parent.pivot;
        to.pivot = from.pivot;
        to.sizeDelta = from.rect.size;
        to.anchoredPosition3D = (Vector2)parent.InverseTransformPoint(from.position);
        to.localRotation = Quaternion.Inverse(parent.rotation) * from.rotation;
        var scale = from.lossyScale;
        var parentScale = parent.lossyScale;
        to.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, 1f);
    }

    void SetFocusPanel(int index, Vector2 min, Vector2 max)
    {
        var panel = _focusPanels[index];
        if (panel == null) return;
        var rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = ((RectTransform)transform).pivot;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = (min + max) * 0.5f;
        rect.sizeDelta = Vector2.Max(Vector2.zero, max - min);
        panel.color = _focusDimColor;
        panel.raycastTarget = _focusBlocksOtherClicks;
        panel.gameObject.SetActive(rect.sizeDelta.x > 0f && rect.sizeDelta.y > 0f);
    }
    UnityEngine.UI.Button[] _highlightTargets;
    readonly System.Collections.Generic.List<RectTransform> _highlightEdges = new System.Collections.Generic.List<RectTransform>();
    readonly Vector3[] _corners = new Vector3[4];

    public void ShowHighlights(UnityEngine.UI.Button[] targets)
    {
        HideHighlights();
        _highlightTargets = targets;
        if (targets == null) return;
        while (_highlightEdges.Count < targets.Length * 4)
        {
            var edge = new GameObject("TutorialHighlight", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            edge.transform.SetParent(transform, false);
            var image = edge.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            _highlightEdges.Add(edge.GetComponent<RectTransform>());
        }
        LateUpdate();
    }
    void HideHighlights()
    {
        _highlightTargets = null;
        foreach (var edge in _highlightEdges) if (edge != null) edge.gameObject.SetActive(false);
    }
    void LateUpdate()
    {
        UpdateFocus();
        if (_highlightTargets == null || !(transform is RectTransform parent)) return;
        var canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        for (int i = 0; i < _highlightTargets.Length; i++)
        {
            var button = _highlightTargets[i];
            bool visible = button != null && button.gameObject.activeInHierarchy && button.IsInteractable();
            for (int j = 0; j < 4; j++) _highlightEdges[i * 4 + j].gameObject.SetActive(visible);
            if (!visible) continue;
            var source = button.GetComponentInParent<Canvas>();
            Camera sourceCamera = source != null && source.renderMode != RenderMode.ScreenSpaceOverlay ? source.worldCamera : null;
            ((RectTransform)button.transform).GetWorldCorners(_corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(sourceCamera, _corners[0]), camera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(sourceCamera, _corners[2]), camera, out Vector2 max);
            Vector2 center = (min + max) * 0.5f;
            float width = Mathf.Max(1f, _highlightWidth);
            for (int j = 0; j < 4; j++)
            {
                var edge = _highlightEdges[i * 4 + j];
                edge.anchorMin = edge.anchorMax = parent.pivot;
                edge.anchoredPosition = j < 2 ? new Vector2(center.x, j == 0 ? min.y : max.y) : new Vector2(j == 2 ? min.x : max.x, center.y);
                edge.sizeDelta = j < 2 ? new Vector2(max.x - min.x + width, width) : new Vector2(width, max.y - min.y);
                var color = _highlightColor;
                color.a *= 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                edge.GetComponent<UnityEngine.UI.Image>().color = color;
            }
        }
    }

    GameObject _currentMarker;
    string _instructionTemplate;
    TMP_Text _interactKeyLabel;

    void OnEnable()
    {
        InputSystem.onActionChange += OnInputActionChanged;
    }

    void OnInputActionChanged(object changed, InputActionChange change)
    {
        if (change != InputActionChange.BoundControlsChanged || string.IsNullOrEmpty(_instructionTemplate)
            || !_instructionTemplate.Contains("{Interact}")) return;
        var action = InputSystem.actions?.FindAction("Hero/Interact", false);
        if (action != null && (ReferenceEquals(changed, action) || ReferenceEquals(changed, action.actionMap)
            || ReferenceEquals(changed, action.actionMap?.asset))) RefreshInstructionText();
    }

    void RefreshInstructionText()
    {
        if (_instructionText == null) return;
        string message = _instructionTemplate ?? string.Empty;
        if (message.Contains("{Interact}"))
        {
            // 실제 플레이와 설정 화면이 공유하는 액션의 현재 바인딩을 표시한다.
            var action = InputSystem.actions?.FindAction("Hero/Interact", false);
            string key = "미지정";
            if (action != null)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)
                        || !binding.effectivePath.StartsWith("<Keyboard>/", System.StringComparison.OrdinalIgnoreCase)) continue;
                    string display = action.GetBindingDisplayString(i);
                    if (string.IsNullOrEmpty(display)) continue;
                    key = display;
                    break;
                }
            }
            message = message.Replace("{Interact}", key);
            if (_interactKeyLabel != null) _interactKeyLabel.text = key;
        }
        _instructionText.text = message;
    }

    float _sentenceInstructionFontSize;
    /// <summary>한 문장씩 줄을 나누고 문장 끝의 한 글자가 별도 줄로 떨어지지 않게 표시합니다.</summary>
    public static string FormatSentenceLines(string message)
    {
        return System.Text.RegularExpressions.Regex.Replace(message ?? string.Empty,
            @"(?<=[.!?。！？])[\t ]+", "\n");
    }
    /// <summary>초밥 튜토리얼의 문장 단위 안내만 한 줄씩 표시합니다.</summary>
    public void ShowSentenceInstruction(string message)
    {
        if (_instructionText != null)
        {
            if (_sentenceInstructionFontSize <= 0f) _sentenceInstructionFontSize = _instructionText.fontSize;
            _instructionText.fontSizeMin = Mathf.Min(20f, _sentenceInstructionFontSize);
            _instructionText.fontSizeMax = _sentenceInstructionFontSize;
            _instructionText.enableAutoSizing = true;
            _instructionText.textWrappingMode = TextWrappingModes.NoWrap;
        }
        ShowInstruction(FormatSentenceLines(message));
    }
    public void ShowInstruction(string message, GameObject marker = null, TMP_Text interactKeyLabel = null)
    {
        HideInstruction();
        _interactKeyLabel = interactKeyLabel;
        _currentMarker = marker;
        if (_currentMarker != null) _currentMarker.SetActive(true);
        _instructionTemplate = message;
        RefreshInstructionText();
        if (_instructionPanel != null) _instructionPanel.SetActive(true);
        if (_instructionText == null) return;

        if (_instructionMaterial == null && _instructionOutlineWidth > 0f)
        {
            _instructionMaterial = _instructionText.fontMaterial;
            _instructionText.outlineColor = _instructionOutlineColor;
            _instructionText.outlineWidth = _instructionOutlineWidth;
        }

        if (_instructionPulsePeriod <= 0f || _instructionMinScale <= 0f
            || _instructionMaxScale <= _instructionMinScale) return;

        var label = _instructionText.rectTransform;
        _instructionOriginalScale = label.localScale;
        label.localScale = _instructionOriginalScale * _instructionMinScale;
        _instructionPulse = label.DOScale(_instructionOriginalScale * _instructionMaxScale, _instructionPulsePeriod * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    public void HideInstruction()
    {
        _instructionTemplate = null;
        _interactKeyLabel = null;
        if (_instructionPulse != null)
        {
            _instructionPulse.Kill();
            _instructionPulse = null;
            if (_instructionText != null) _instructionText.transform.localScale = _instructionOriginalScale;
        }
        HideFocus();
        HideHighlights();
        if (_currentMarker != null) _currentMarker.SetActive(false);
        _currentMarker = null;
        if (_instructionPanel != null) _instructionPanel.SetActive(false);
    }

    void OnDisable()
    {
        InputSystem.onActionChange -= OnInputActionChanged;
        HideInstruction();
    }

    void OnDestroy()
    {
        if (_instructionMaterial != null) Destroy(_instructionMaterial);
        HideFocus();
        foreach (var state in _focusCanvases.Values)
        {
            if (state.ReleasePending) continue;
            if (state.AddedRaycaster && state.Raycaster != null) Destroy(state.Raycaster);
            if (state.AddedCanvas && state.Canvas != null) Destroy(state.Canvas);
        }
        _focusCanvases.Clear();
        StencilMaterial.Remove(_bookmarkMaskMaterial);
        StencilMaterial.Remove(_pageMaskMaterial);
        StencilMaterial.Remove(_bookmarkDimMaterial);
        if (_addedFocusRaycaster != null) Destroy(_addedFocusRaycaster);
    }
}
