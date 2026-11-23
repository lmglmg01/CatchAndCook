using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>열린 창의 기존 UI 이동·확인 입력을 활용하며 선택이 뒤 창으로 빠지지 않도록 보완합니다.</summary>
[DefaultExecutionOrder(10000)]
public sealed class UIKeyboardFocusView : MonoBehaviour
{
    #region 상태 및 연결
    [SerializeField] Selectable _firstSelection; // 선택이 없을 때 우선 선택할 기존 UI입니다.
    [SerializeField] bool _waitForDirectionInput; // 방향키를 누르기 전에는 자동 선택하지 않는 창입니다.
    [SerializeField] Sprite _focusSprite; // 조작키로 선택한 버튼에 표시할 공통 스프라이트입니다.
    [SerializeField] Selectable[] _hiddenFocusControls = System.Array.Empty<Selectable>(); // 이동·선택은 유지하고 공통 선택 표시만 숨길 항목입니다.
    static readonly List<UIKeyboardFocusView> Windows = new();
    static UIKeyboardFocusView _front;
    static int _updatedFrame = -1;
    readonly List<Selectable> _controls = new();
    readonly List<CanvasGroup> _groups = new();
    readonly Dictionary<Selectable, Navigation> _originalNavigation = new();
    Selectable _lastSelection;
    RectTransform _focusFrame;
    Sprite _focusSlicedSprite;
    bool _navigationUsed; // 현재 창에서 방향 조작을 한 뒤에만 선택 테두리를 표시합니다.

    /// <summary>빠른 재생 진입에서도 이전 실행의 창 목록을 남기지 않습니다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetWindows() { Windows.Clear(); _front = null; _updatedFrame = -1; }

    /// <summary>열린 창을 탐색 대상으로 등록합니다. 버튼 이벤트는 추가하지 않습니다.</summary>
    void OnEnable()
    {
        _navigationUsed = false;
        ShowFocus(null);
        if (!Windows.Contains(this)) Windows.Add(this);
    }

    /// <summary>창이 닫히면 기존 탐색 설정을 복구하고 해당 창의 선택 표시를 정리합니다.</summary>
    void OnDisable()
    {
        Windows.Remove(this);
        RestoreNavigation();
        if (_front == this) _front = null;
        if (_focusFrame != null) Destroy(_focusFrame.gameObject);
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>기존 입력 모듈이 처리한 선택을 프레임마다 한 번 확인합니다.</summary>
    void LateUpdate()
    {
        if (!Application.isPlaying || _updatedFrame == Time.frameCount) return;
        _updatedFrame = Time.frameCount;
        RefreshFocus();
    }
    #endregion

    #region 앞 창 선택과 입력 보호
    /// <summary>보이는 창 중 맨 앞 창만 선택을 관리하고 기존 ESC·Enter 처리는 그대로 둡니다.</summary>
    static void RefreshFocus()
    {
        var events = EventSystem.current;
        if (events == null) return;
        UIKeyboardFocusView front = null;
        foreach (var window in Windows)
        {
            if (window == null || !window.isActiveAndEnabled) continue;
            window.GetComponentsInChildren(false, window._controls);
            window._controls.RemoveAll(control => !window.CanUse(control) || (control.navigation.mode == Navigation.Mode.None && control is not Button));
            if (window._controls.Count == 0) continue;
            if (front == null || CompareWindows(window.transform, front.transform) > 0) front = window;
        }
        if (_front != front)
        {
            if (_front != null)
            {
                _front.RestoreNavigation(); _front.ShowFocus(null);
                if (front == null && events.currentSelectedGameObject != null &&
                    events.currentSelectedGameObject.transform.IsChildOf(_front.transform))
                    events.SetSelectedGameObject(null);
            }
            _front = front;
            if (front != null) front._navigationUsed = false;
        }
        if (front == null) return;
        // 키 재설정 중에는 키 입력과 비워 둔 EventSystem 선택을 건드리지 않습니다.
        var keyboardSettings = front.GetComponentInChildren<KeyboardSettingView>();
        if (keyboardSettings != null && keyboardSettings.IsListening) return;
        var editingObject = events.currentSelectedGameObject;
        if (editingObject != null && editingObject.transform.IsChildOf(front.transform))
        {
            var tmp = editingObject.GetComponent<TMP_InputField>();
            var input = editingObject.GetComponent<InputField>();
            if ((tmp != null && tmp.isFocused) || (input != null && input.isFocused))
            {
                front.ShowFocus(null);
                return;
            }
        }
        // 자동 기본 선택과 마우스 클릭은 키보드 조작으로 취급하지 않습니다.
        if (HasNavigationInput(events)) front._navigationUsed = true;
        front.ConfigureNavigation();
        if (front._waitForDirectionInput && !front._navigationUsed) { front.ShowFocus(null); return; }
        var selectedObject = events.currentSelectedGameObject;
        var selected = selectedObject != null ? selectedObject.GetComponent<Selectable>() : null;
        if (selected == null || !front._controls.Contains(selected))
        {
            selected = front.CanUse(front._lastSelection) ? front._lastSelection :
                front.CanUse(front._firstSelection) ? front._firstSelection : front._controls[0];
            events.SetSelectedGameObject(selected.gameObject);
        }
        if (front._lastSelection != selected) front.RevealSelection(selected);
        front._lastSelection = selected;
        front.ShowFocus(front._navigationUsed ? selected : null);
        // Submit은 기존 EventSystem이 선택한 Button에 전달합니다.
        // onClick 호출이나 별도 Enter 액션을 추가하지 않아 기존 동작이 한 번만 실행됩니다.
    }

    /// <summary>기존 UI 입력 모듈에 실제 방향 입력이 들어온 경우에만 선택 표시를 시작합니다.</summary>
    static bool HasNavigationInput(EventSystem events)
    {
#if ENABLE_INPUT_SYSTEM
        if (events.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule module)
        {
            var move = module.move != null ? module.move.action : null;
            return move != null && move.enabled && move.ReadValue<Vector2>().sqrMagnitude > .01f;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (events.currentInputModule is StandaloneInputModule legacy)
            return Mathf.Abs(Input.GetAxisRaw(legacy.horizontalAxis)) > .1f ||
                Mathf.Abs(Input.GetAxisRaw(legacy.verticalAxis)) > .1f;
#endif
        return false;
    }

    /// <summary>꺼진 버튼, 비활성 버튼, 투명한 페이지와 숨긴 입력 요소를 제외합니다.</summary>
    bool CanUse(Selectable control)
    {
        if (control == null || !control.isActiveAndEnabled || !control.IsInteractable() ||
            !control.transform.IsChildOf(transform)) return false;
        control.GetComponentsInParent(false, _groups);
        foreach (var group in _groups) if (group.alpha <= .01f) return false;
        return true;
    }

    /// <summary>Canvas 정렬과 실제 형제·부모 순서로 앞에 표시되는 창을 판단합니다.</summary>
    static int CompareWindows(Transform a, Transform b)
    {
        Canvas SortingCanvas(Transform target)
        {
            foreach (var canvas in target.GetComponentsInParent<Canvas>())
                if (canvas.overrideSorting || canvas.isRootCanvas) return canvas;
            return null;
        }
        var ca = SortingCanvas(a); var cb = SortingCanvas(b);
        if (ca != null && cb != null)
        {
            int result = ca.renderMode.CompareTo(cb.renderMode);
            if (ca.renderMode != cb.renderMode)
                result = (ca.renderMode == RenderMode.ScreenSpaceOverlay ? 1 : 0).CompareTo(cb.renderMode == RenderMode.ScreenSpaceOverlay ? 1 : 0);
            if (result != 0) return result;
            result = SortingLayer.GetLayerValueFromID(ca.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(cb.sortingLayerID));
            if (result != 0) return result;
            result = ca.sortingOrder.CompareTo(cb.sortingOrder);
            if (result != 0) return result;
        }
        var pa = new List<Transform>(); var pb = new List<Transform>();
        for (var t = a; t != null; t = t.parent) pa.Add(t);
        for (var t = b; t != null; t = t.parent) pb.Add(t);
        pa.Reverse(); pb.Reverse();
        for (int i = 0; i < Mathf.Min(pa.Count, pb.Count); i++)
            if (pa[i] != pb[i]) return pa[i].GetSiblingIndex().CompareTo(pb[i].GetSiblingIndex());
        return pa.Count.CompareTo(pb.Count);
    }
    #endregion

    #region 기존 방향키 탐색 보완
    /// <summary>기존 명시적 연결은 유지하고 자동 탐색만 현재 창의 보이는 요소로 제한합니다.</summary>
    void ConfigureNavigation()
    {
        foreach (var control in _controls)
        {
            if (!_originalNavigation.TryGetValue(control, out var original))
            {
                original = control.navigation;
                if (original.mode != Navigation.Mode.Automatic && !(original.mode == Navigation.Mode.None && control is Button)) continue;
                _originalNavigation.Add(control, original);
            }
            var navigation = original;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = Neighbour(control, Vector2.up);
            navigation.selectOnDown = Neighbour(control, Vector2.down);
            navigation.selectOnLeft = Neighbour(control, Vector2.left);
            navigation.selectOnRight = Neighbour(control, Vector2.right);
            // 슬라이더의 축 방향 키는 기존 수치 조절에 사용합니다.
            bool horizontal = control is Slider slider && (slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft) ||
                control is Scrollbar bar && (bar.direction == Scrollbar.Direction.LeftToRight || bar.direction == Scrollbar.Direction.RightToLeft);
            if (control is Slider || control is Scrollbar)
            {
                if (horizontal) navigation.selectOnLeft = navigation.selectOnRight = null;
                else navigation.selectOnUp = navigation.selectOnDown = null;
            }
            control.navigation = navigation;
        }
    }

    /// <summary>실제 배치에서 지정 방향에 가까운 같은 창의 선택 요소를 찾습니다.</summary>
    Selectable Neighbour(Selectable source, Vector2 direction)
    {
        var rect = source.transform as RectTransform;
        Vector3 origin = rect != null ? rect.TransformPoint(rect.rect.center) : source.transform.position;
        Vector3 axis = transform.TransformDirection(direction);
        Selectable best = null; float bestScore = 0;
        foreach (var candidate in _controls)
        {
            if (candidate == source) continue;
            var target = candidate.transform as RectTransform;
            Vector3 delta = (target != null ? target.TransformPoint(target.rect.center) : candidate.transform.position) - origin;
            float dot = Vector3.Dot(axis, delta);
            if (dot <= .001f || delta.sqrMagnitude <= .001f) continue;
            float score = dot / delta.sqrMagnitude;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }

    /// <summary>창을 떠나면 이 View가 임시로 보완한 탐색 설정만 원래 값으로 복구합니다.</summary>
    void RestoreNavigation()
    {
        foreach (var item in _originalNavigation) if (item.Key != null) item.Key.navigation = item.Value;
        _originalNavigation.Clear();
    }

    /// <summary>스크롤 목록의 선택 항목이 보이도록 기존 콘텐츠의 위치만 보정합니다.</summary>
    void RevealSelection(Selectable selected)
    {
        var scroll = selected.GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.content == null || !scroll.transform.IsChildOf(transform)) return;
        var viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected.transform);
        var rect = viewport.rect;
        Vector2 offset = Vector2.zero;
        if (scroll.vertical) offset.y = bounds.max.y > rect.yMax ? rect.yMax - bounds.max.y : bounds.min.y < rect.yMin ? rect.yMin - bounds.min.y : 0;
        if (scroll.horizontal) offset.x = bounds.max.x > rect.xMax ? rect.xMax - bounds.max.x : bounds.min.x < rect.xMin ? rect.xMin - bounds.min.x : 0;
        scroll.StopMovement();
        scroll.content.anchoredPosition += (Vector2)scroll.content.parent.InverseTransformVector(viewport.TransformVector(offset));
    }
    #endregion

    #region 선택 표시
    /// <summary>조작키 선택을 반투명 스프라이트로 표시하며 버튼 글자와 클릭 입력은 유지합니다.</summary>
    void ShowFocus(Selectable selected)
    {
        if (selected is not Button || _focusSprite == null || System.Array.IndexOf(_hiddenFocusControls, selected) >= 0)
        {
            if (_focusFrame != null) _focusFrame.gameObject.SetActive(false);
            return;
        }
        if (_focusFrame == null)
        {
            var root = new GameObject("KeyboardFocus", typeof(RectTransform), typeof(LayoutElement), typeof(UnityEngine.UI.Image));
            root.hideFlags = HideFlags.HideAndDontSave;
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = root.GetComponent<UnityEngine.UI.Image>();
            // 원본 리소스는 유지하고 표시용 9분할 스프라이트만 생성합니다.
            if (_focusSlicedSprite == null)
                _focusSlicedSprite = Sprite.Create(_focusSprite.texture, _focusSprite.rect, new Vector2(.5f, .5f),
                    _focusSprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(54, 54, 54, 54));
            image.sprite = _focusSlicedSprite;
            image.color = new Color(1f, 1f, 1f, .5f);
            image.raycastTarget = false;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = 10f / 3f; // 원본의 10px 선을 Canvas 기준 3px로 고정합니다.
            _focusFrame = root.GetComponent<RectTransform>();
        }
        if (_focusFrame.parent != selected.transform) _focusFrame.SetParent(selected.transform, false);
        _focusFrame.SetAsFirstSibling();
        _focusFrame.anchorMin = Vector2.zero; _focusFrame.anchorMax = Vector2.one;
        // 버튼의 별도 그림자까지 포함한 외곽에서 4px 띄워 표시합니다.
        var buttonRect = (RectTransform)selected.transform;
        Vector2 min = buttonRect.rect.min, max = buttonRect.rect.max;
        var shadow = selected.transform.parent != null ? selected.transform.parent.Find(selected.name + "Shadow") as RectTransform : null;
        if (shadow != null && shadow.gameObject.activeInHierarchy)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(buttonRect, shadow);
            min = Vector2.Min(min, bounds.min); max = Vector2.Max(max, bounds.max);
        }
        _focusFrame.offsetMin = min - buttonRect.rect.min - Vector2.one * 4f;
        _focusFrame.offsetMax = max - buttonRect.rect.max + Vector2.one * 4f;
        _focusFrame.gameObject.SetActive(true);
    }
    void OnDestroy()
    {
        if (_focusSlicedSprite != null) Destroy(_focusSlicedSprite);
    }
    #endregion
}



