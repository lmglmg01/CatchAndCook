using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>할당된 제목 영역 안에서 긴 퀘스트 이름을 한 글자씩 왼쪽으로 흘려 보여줍니다.</summary>
[DisallowMultipleComponent]
public class QuestTitleMarqueeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(.01f)] private float _secondsPerCharacter = .5f;
    [SerializeField, Min(0)] private float _repeatDelay = 1f;
    // 동적으로 복제된 목록 슬롯에서도 기존 마스크를 그대로 재사용합니다.
    [SerializeField, HideInInspector] private RectTransform _viewport;
    [SerializeField, HideInInspector] private RectTransform _rightBoundary;
    [SerializeField, HideInInspector] private float _allocatedWidth;
    private readonly Vector3[] _boundaryCorners = new Vector3[4];
    private TMP_Text _text;
    private Sequence _sequence;
    private bool _initialized;
    private bool _hovered;
    private bool _needsRebuild = true;
    private string _lastText;
    private Vector2 _lastSize;
    private float _fontSize;
    private float _pixelsPerSecond;
    private TextAlignmentOptions _alignment;

    /// <summary>기존 제목 컴포넌트 참조를 유지한 채 흐름 연출을 연결합니다.</summary>
    public static void Apply(TMP_Text title, RectTransform rightBoundary = null)
    {
        if (title == null || !Application.isPlaying) return;
        var view = title.GetComponent<QuestTitleMarqueeView>();
        if (view == null) view = title.gameObject.AddComponent<QuestTitleMarqueeView>();
        view._rightBoundary = rightBoundary;
        // 같은 내용으로 반복 갱신될 때 재생 위치가 처음으로 돌아가지 않도록 합니다.
        if (view._lastText != title.text) view._needsRebuild = true;
    }

    /// <summary>앱이나 슬롯을 다시 열면 처음 글자부터 표시합니다.</summary>
    private void OnEnable() => _needsRebuild = true;

    /// <summary>실제 제목이나 영역 크기가 달라졌을 때만 글자별 이동 거리를 계산합니다.</summary>
    private void LateUpdate()
    {
        Initialize();
        if (_text == null || _viewport == null) return;
        ConstrainViewport();
        Vector2 size = _viewport.rect.size;
        if (size.x <= 0 || size.y <= 0) return;
        if (_needsRebuild || _lastText != _text.text || (size - _lastSize).sqrMagnitude > .01f)
            Rebuild(size);
    }

    /// <summary>원래 제목 사각형을 마스크 영역으로 옮기고 그 안에 기존 텍스트를 배치합니다.</summary>
    private void Initialize()
    {
        if (_initialized) return;
        _text = GetComponent<TMP_Text>();
        if (_text == null) return;
        _initialized = true;
        _fontSize = _text.enableAutoSizing ? _text.fontSizeMax : _text.fontSize;
        _alignment = _text.alignment;
        var rect = _text.rectTransform;
        if (_viewport == null)
        {
            var go = new GameObject(_text.name + "Viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
            _viewport = go.GetComponent<RectTransform>();
            _viewport.SetParent(rect.parent, false);
            _viewport.SetSiblingIndex(rect.GetSiblingIndex());
            _viewport.anchorMin = rect.anchorMin;
            _viewport.anchorMax = rect.anchorMax;
            _viewport.pivot = rect.pivot;
            _viewport.sizeDelta = rect.sizeDelta;
            _viewport.anchoredPosition3D = rect.anchoredPosition3D;
            _viewport.localRotation = rect.localRotation;
            _viewport.localScale = rect.localScale;
            _allocatedWidth = _viewport.rect.width;
            Vector3 leftOffset = rect.localRotation * Vector3.Scale(
                new Vector3(_allocatedWidth * rect.pivot.x, 0, 0), rect.localScale);
            _viewport.pivot = new Vector2(0, _viewport.pivot.y);
            _viewport.anchoredPosition3D = rect.anchoredPosition3D - leftOffset;
            rect.SetParent(_viewport, false);
        }
        rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
        rect.pivot = new Vector2(0, .5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition3D = Vector3.zero;
        _text.enableAutoSizing = false;
        _text.fontSize = _fontSize;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.overflowMode = TextOverflowModes.Overflow;
        // 긴 텍스트의 사각형은 이동 중에도 마스크 영역 전체를 덮으므로 호버가 끊기지 않습니다.
        _text.raycastTarget = true;
    }

    /// <summary>제목의 시작 위치는 유지하고 진행도 영역 앞에서 표시 폭을 제한합니다.</summary>
    private void ConstrainViewport()
    {
        if (_allocatedWidth <= 0) _allocatedWidth = _viewport.rect.width;
        float width = _allocatedWidth;
        if (_rightBoundary != null)
        {
            float boundary = float.PositiveInfinity;
            if (_rightBoundary.parent == _viewport.parent)
            {
                // 프리팹 편집 중 Canvas의 월드 스케일이 0이어도 로컬 좌표로 정확히 계산합니다.
                _rightBoundary.GetLocalCorners(_boundaryCorners);
                Matrix4x4 toViewport = Matrix4x4.TRS(_viewport.localPosition, _viewport.localRotation, _viewport.localScale).inverse *
                    Matrix4x4.TRS(_rightBoundary.localPosition, _rightBoundary.localRotation, _rightBoundary.localScale);
                foreach (Vector3 corner in _boundaryCorners)
                    boundary = Mathf.Min(boundary, toViewport.MultiplyPoint3x4(corner).x);
            }
            else
            {
                _rightBoundary.GetWorldCorners(_boundaryCorners);
                foreach (Vector3 corner in _boundaryCorners)
                    boundary = Mathf.Min(boundary, _viewport.InverseTransformPoint(corner).x);
            }
            width = Mathf.Clamp(boundary - 8f, 1f, width);
        }
        if (Mathf.Abs(_viewport.rect.width - width) > .01f)
            _viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }
    /// <summary>
    /// 평균 글자 폭을 기준으로 0.5초에 한 글자 정도 보이는 일정한 속도로 이동합니다.
    /// 마지막 글자까지 보여준 뒤 1초 대기하고 처음 위치에서 반복합니다.
    /// </summary>
    private void Rebuild(Vector2 size)
    {
        StopAndReset();
        _needsRebuild = false;
        _lastText = _text.text;
        _lastSize = size;
        _text.alignment = _alignment;
        _text.fontSize = _fontSize;
        var rect = _text.rectTransform;
        float contentWidth = _text.GetPreferredValues(_text.text, Mathf.Infinity, size.y).x;
        rect.sizeDelta = new Vector2(Mathf.Max(size.x, contentWidth), size.y);
        if (contentWidth <= size.x + .01f || string.IsNullOrEmpty(_text.text)) return;
        _text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        _text.ForceMeshUpdate(true, true);
        if (!_hovered) return;
        float right = 0;
        int count = _text.textInfo.characterCount;
        for (int i = 0; i < count; i++)
        {
            var character = _text.textInfo.characterInfo[i];
            right = Mathf.Max(right, character.xAdvance, character.topRight.x);
        }
        float distance = right - size.x;
        if (distance <= .01f || count == 0) return;
        float averageAdvance = right / count;
        _pixelsPerSecond = averageAdvance / Mathf.Max(.01f, _secondsPerCharacter);
        _sequence = DOTween.Sequence().SetUpdate(true).Pause();
        _sequence.Append(rect.DOAnchorPosX(-distance, distance / _pixelsPerSecond).SetEase(Ease.Linear));
        _sequence.AppendInterval(_repeatDelay);
        _sequence.AppendCallback(ResetPosition);
        _sequence.SetLoops(-1, LoopType.Restart).Play();
    }

    /// <summary>오른쪽으로 돌아가는 역재생 없이 처음 글자의 표시 위치로 돌아갑니다.</summary>
    private void ResetPosition()
    {
        if (_text == null) return;
        Vector2 position = _text.rectTransform.anchoredPosition;
        position.x = 0;
        _text.rectTransform.anchoredPosition = position;
    }

    /// <summary>이 제목이 소유한 Tween만 중지하고 재사용할 때의 표시 위치를 초기화합니다.</summary>
    private void StopAndReset()
    {
        _sequence?.Kill();
        _sequence = null;
        ResetPosition();
    }

    /// <summary>탭 전환이나 슬롯 비활성화 시 연출이 남지 않도록 정리합니다.</summary>
    private void OnDisable()
    {
        _hovered = false;
        StopAndReset();
    }

    /// <summary>제목 영역에 마우스가 올라온 동안만 처음부터 흐름 연출을 시작합니다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        _needsRebuild = true;
    }

    /// <summary>마우스가 제목 영역을 벗어나면 이동을 멈추고 처음 글자로 돌아갑니다.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        StopAndReset();
    }

    /// <summary>목록 또는 씬이 제거될 때 이 제목의 Tween을 해제합니다.</summary>
    private void OnDestroy() => StopAndReset();
}
