using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class CanvasEditor
{
#region 미리보기 시점 상태
    ScrollView _previewViewport;
    Label _previewZoomLabel;
    float _previewZoom = 1f;
    Vector2 _previewPan, _previewDragLast, _previewZoomAnchor, _previewZoomPoint;
    bool _previewSpaceHeld, _previewDragging, _previewZoomPending, _previewZoomingOut, _previewHovered;
    int _previewDragPointer, _previewDragButton;
#endregion
#region 입력 연결
    /// <summary>미리보기 전용 휠 확대와 Space 드래그 입력을 연결합니다.</summary>
    void SetupPreviewNavigation(ScrollView viewport)
    {
        _previewViewport = viewport;
        viewport.focusable = true;
        viewport.RegisterCallback<WheelEvent>(OnPreviewWheel, TrickleDown.TrickleDown);
        viewport.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove);
        viewport.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp);
        viewport.RegisterCallback<PointerCaptureOutEvent>(_ => _previewDragging = false);
        viewport.RegisterCallback<PointerEnterEvent>(_ => _previewHovered = true);
        viewport.RegisterCallback<PointerLeaveEvent>(_ => _previewHovered = false);
        rootVisualElement.UnregisterCallback<PointerDownEvent>(OnPreviewPointerDown, TrickleDown.TrickleDown);
        rootVisualElement.UnregisterCallback<PointerMoveEvent>(OnPreviewPointerMove, TrickleDown.TrickleDown);
        rootVisualElement.UnregisterCallback<PointerUpEvent>(OnPreviewPointerUp, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<PointerDownEvent>(OnPreviewPointerDown, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<PointerMoveEvent>(OnPreviewPointerMove, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<PointerUpEvent>(OnPreviewPointerUp, TrickleDown.TrickleDown);
        rootVisualElement.UnregisterCallback<KeyDownEvent>(OnPreviewKeyDown, TrickleDown.TrickleDown);
        rootVisualElement.UnregisterCallback<KeyUpEvent>(OnPreviewKeyUp, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnPreviewKeyDown, TrickleDown.TrickleDown);
        rootVisualElement.RegisterCallback<KeyUpEvent>(OnPreviewKeyUp, TrickleDown.TrickleDown);
        _frame.RegisterCallback<GeometryChangedEvent>(OnPreviewFrameGeometry);
    }

    /// <summary>Space가 눌린 동안 미리보기 이동 모드를 준비합니다.</summary>
    void OnPreviewKeyDown(KeyDownEvent e)
    {
        if (_mode == ToolMode.SpriteTracker) return;
        if (e.keyCode != KeyCode.Space)
            return;
        _previewSpaceHeld = true;
        if (_previewHovered)
            e.StopPropagation();
    }

    /// <summary>Space를 놓으면 왼쪽 드래그만 종료하고 휠 버튼 이동은 유지합니다.</summary>
    void OnPreviewKeyUp(KeyUpEvent e)
    {
        if (_mode == ToolMode.SpriteTracker) return;
        if (e.keyCode != KeyCode.Space)
            return;
        _previewSpaceHeld = false;
        if (_previewDragButton == 0)
            EndPreviewPan();
        if (_previewHovered)
            e.StopPropagation();
    }

    /// <summary>창의 포커스를 잃으면 눌림 상태가 남지 않도록 정리합니다.</summary>
    void OnLostFocus()
    {
        _trackerSpace = false;
        _previewSpaceHeld = false;
        EndPreviewPan();
    }

#endregion
#region 휠 확대와 위치 보정
    /// <summary>마우스 위치를 기준으로 미리보기 배율을 5~400퍼센트에서 조절합니다.</summary>
    void OnPreviewWheel(WheelEvent e)
    {
        if (_frame == null || _canvas == null || _frame.layout.width <= 0)
            return;
        var resolution = Resolutions[_resolution];
        float current = _zoomed ? _previewZoom : _frame.layout.width / resolution.x;
        float next = Mathf.Clamp(current * Mathf.Pow(1.1f, -e.delta.y), .05f, 4f);
        if (Mathf.Approximately(next, current))
        {
            e.StopImmediatePropagation();
            return;
        }

        var local = _frame.WorldToLocal(e.mousePosition);
        _previewZoomAnchor = new Vector2(local.x / _frame.layout.width, local.y / _frame.layout.height);
        _previewZoomPoint = e.mousePosition;
        _previewZoomPending = true;
        _previewZoomingOut = next < current;
        _previewZoom = next;
        _zoomed = true;
        FitPreview();
        e.StopImmediatePropagation();
    }

    /// <summary>확대 뒤 마우스 아래에 있던 지점이 유지되도록 화면 위치를 보정합니다.</summary>
    void OnPreviewFrameGeometry(GeometryChangedEvent e)
    {
        if (!_previewZoomPending || _frame == null)
            return;
        _previewZoomPending = false;
        var after = _frame.LocalToWorld(new Vector2(_frame.layout.width * _previewZoomAnchor.x, _frame.layout.height * _previewZoomAnchor.y));
        _previewPan += _previewZoomPoint - after;
        ApplyPreviewPan();
        if (_previewZoomingOut)
            KeepZoomedOutPreviewVisible();
    }

    /// <summary>축소한 화면이 표시 영역보다 작으면 가장자리에 잘리지 않도록 위치를 보정합니다.</summary>
    void KeepZoomedOutPreviewVisible()
    {
        if (_frame == null || _previewViewport == null)
            return;
        Rect bounds = _frame.worldBound;
        Rect viewport = _previewViewport.contentViewport.worldBound;
        Vector2 correction = Vector2.zero;
        if (bounds.width <= viewport.width)
            correction.x = bounds.xMin < viewport.xMin ? viewport.xMin - bounds.xMin : bounds.xMax > viewport.xMax ? viewport.xMax - bounds.xMax : 0;
        if (bounds.height <= viewport.height)
            correction.y = bounds.yMin < viewport.yMin ? viewport.yMin - bounds.yMin : bounds.yMax > viewport.yMax ? viewport.yMax - bounds.yMax : 0;
        _previewPan += correction;
        ApplyPreviewPan();
    }
    /// <summary>맞춤 또는 실제 배율로 돌아가며 이동한 미리보기 시점을 초기화합니다.</summary>
    void ResetPreviewView(bool fit)
    {
        _zoomed = !fit;
        _previewZoom = 1f;
        ClearPreviewPan();
        if (_previewViewport != null)
            _previewViewport.scrollOffset = Vector2.zero;
        FitPreview();
    }

    /// <summary>현재 배율을 미리보기 도구 모음에 표시합니다.</summary>
    void RefreshPreviewZoomLabel()
    {
        if (_previewZoomLabel != null)
            _previewZoomLabel.text = _zoomed ? $"{_previewZoom * 100:0}%" : "맞춤";
    }

#endregion
#region Space 및 휠 버튼 드래그 이동
    /// <summary>Space와 왼쪽 버튼 또는 휠 버튼 드래그로 오브젝트 선택 대신 화면 이동을 시작합니다.</summary>
    void OnPreviewPointerDown(PointerDownEvent e)
    {
        if (_mode == ToolMode.SpriteTracker) return;
        if ((e.button != 0 && e.button != 2) || _previewViewport == null || !_previewViewport.worldBound.Contains(e.position))
            return;
        if (e.button == 0 && !_previewSpaceHeld)
        {
            _previewViewport.Focus();
            return;
        }

        _previewDragging = true;
        _previewDragPointer = e.pointerId;
        _previewDragButton = e.button;
        _previewDragLast = e.position;
        _previewViewport.CapturePointer(e.pointerId);
        e.StopImmediatePropagation();
    }

    /// <summary>캡처한 포인터의 이동량만큼 미리보기 화면을 옮깁니다.</summary>
    void OnPreviewPointerMove(PointerMoveEvent e)
    {
        if (_mode == ToolMode.SpriteTracker) return;
        if (!_previewDragging || e.pointerId != _previewDragPointer)
            return;
        Vector2 position = e.position;
        _previewPan += position - _previewDragLast;
        _previewDragLast = position;
        ApplyPreviewPan();
        e.StopImmediatePropagation();
    }

    /// <summary>이동에 사용한 마우스 버튼을 놓으면 화면 이동을 종료합니다.</summary>
    void OnPreviewPointerUp(PointerUpEvent e)
    {
        if (!_previewDragging || e.pointerId != _previewDragPointer || e.button != _previewDragButton)
            return;
        EndPreviewPan();
        e.StopImmediatePropagation();
    }

    /// <summary>미리보기 이동에 사용한 포인터 캡처만 해제합니다.</summary>
    void EndPreviewPan()
    {
        _previewDragging = false;
        if (_previewViewport != null && _previewViewport.HasPointerCapture(_previewDragPointer))
            _previewViewport.ReleasePointer(_previewDragPointer);
    }

    /// <summary>Canvas 원본 대신 미리보기 프레임의 표시 위치만 변경합니다.</summary>
    void ApplyPreviewPan()
    {
        if (_frame != null)
            _frame.transform.position = new Vector3(_previewPan.x, _previewPan.y, 0);
    }

    /// <summary>시점 이동과 입력 대기 상태를 원래 값으로 되돌립니다.</summary>
    void ClearPreviewPan()
    {
        EndPreviewPan();
        _previewSpaceHeld = false;
        _previewZoomPending = false;
        _previewPan = Vector2.zero;
        ApplyPreviewPan();
    }
#endregion
}

