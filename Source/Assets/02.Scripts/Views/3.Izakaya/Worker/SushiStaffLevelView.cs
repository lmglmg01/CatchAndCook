using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>전달받은 직원 레벨과 아이콘을 표시합니다. 강화 판정·비용·경험치 계산은 하지 않습니다.</summary>
[DefaultExecutionOrder(250)]
public sealed class SushiStaffLevelView : MonoBehaviour
{
    #region 표시 연결
    [SerializeField] RectTransform _badge;
    [SerializeField] UnityEngine.UI.Image _background, _icon;
    [SerializeField] bool _preserveTagArtworkColor;
    [SerializeField] TMP_Text _levelText;
    [SerializeField] GameObject _levelUpRoot;
    [SerializeField] Transform _follow;
    [SerializeField, Min(8)] float _screenHeight = 26;
    [SerializeField, Min(0)] float _headMargin = 8;
    [SerializeField] Color[] _backgroundColors = {
        new Color32(60,86,100,255), new Color32(36,123,112,255), new Color32(50,104,166,255),
        new Color32(113,86,162,255), new Color32(172,114,44,255)
    };
    [Tooltip("Element 0~4는 각각 강화 1~5레벨의 아이콘 색상입니다. 흰색은 원본 색상을 유지합니다.")]
    [SerializeField] Color[] _iconColors = {
        Color.white, new Color32(189,246,223,255), new Color32(191,223,255,255),
        new Color32(232,210,255,255), Color.white
    };
    Renderer[] _bodyRenderers = System.Array.Empty<Renderer>();
    RectTransform[] _avoidRects = System.Array.Empty<RectTransform>();
    Transform _avoidRoot;
    int _shownLevel;
    bool _hasLevel;
    Sequence _colorTween;
    Coroutine _levelUpRoutine;
    Color _targetBackground, _targetIcon;
    Vector2 _iconOriginPosition;
    bool _iconOriginCached;
    #endregion

    #region 표시 데이터 전달
    /// <summary>움직이는 직원과 기존 주문·운반 UI를 연결합니다. 머리 높이는 외형 Renderer에서만 읽습니다.</summary>
    public void BindTarget(Transform follow, Transform avoidRoot)
    {
        if (_follow != follow || _bodyRenderers.Length == 0)
        {
            _follow = follow;
            _bodyRenderers = follow != null ? System.Array.FindAll(follow.GetComponentsInChildren<Renderer>(true),
                r => (r is SkinnedMeshRenderer || r is MeshRenderer) && r.GetComponentInParent<Canvas>() == null)
                : System.Array.Empty<Renderer>();
        }
        if (_avoidRoot != avoidRoot)
        {
            _avoidRoot = avoidRoot;
            _avoidRects = avoidRoot != null ? System.Array.FindAll(avoidRoot.GetComponentsInChildren<RectTransform>(true),
                r => r.name == "BubbleBackground" || r.name == "DisplayRoot") : System.Array.Empty<RectTransform>();
        }
    }

    /// <summary>처음 받은 값은 즉시 표시하고, 이후 실제 상승한 경우에만 상승 연출을 요청합니다.</summary>
    public void Display(Sprite icon, int level, int maximum, bool animate)
    {
        maximum = Mathf.Max(1, maximum);
        level = Mathf.Clamp(level, 1, maximum);
        bool increased = animate && _hasLevel && level > _shownLevel;
        bool changed = !_hasLevel || level != _shownLevel;
        _shownLevel = level; _hasLevel = true;
        if (_badge != null) _badge.gameObject.SetActive(true);
        if (_levelText != null) _levelText.text = $"Lv.{level}";
        if (_icon != null) { _icon.sprite = icon; _icon.enabled = icon != null; }
        AlignIconArtwork();
        float ratio = maximum > 1 ? (float)(level - 1) / (maximum - 1) : 0;
        _targetBackground = _preserveTagArtworkColor ? Color.white : Sample(_backgroundColors, ratio);
        // 강화 레벨에 해당하는 Inspector 색상을 직접 선택합니다.
        _targetIcon = GetIconColor(level);
        if (changed)
        {
            _colorTween?.Kill();
            if (increased && isActiveAndEnabled && _background != null && _icon != null)
                _colorTween = DOTween.Sequence().SetUpdate(true)
                    .Join(_background.DOColor(_targetBackground, .35f))
                    .Join(_icon.DOColor(_targetIcon, .35f));
            else ApplyColors();
        }
        if (increased && isActiveAndEnabled) PlayLevelUp();
    }

    /// <summary>요리 스킬 아이콘의 아래 투명 여백을 제외해 실제 그림을 세로 중앙에 맞춥니다.</summary>
    void AlignIconArtwork()
    {
        if (_icon == null) return;
        var rect = _icon.rectTransform;
        if (!_iconOriginCached) { _iconOriginPosition = rect.anchoredPosition; _iconOriginCached = true; }
        var position = _iconOriginPosition;
        var sprite = _icon.sprite;
        if (sprite != null && sprite.name == "Icon_Cooking_Skill1")
        {
            // 85×98 원본의 실제 그림은 위에서 y=0~78에 있어 중심이 10px 위에 있습니다.
            float scale = _icon.preserveAspect
                ? Mathf.Min(rect.rect.width / sprite.rect.width, rect.rect.height / sprite.rect.height)
                : rect.rect.height / sprite.rect.height;
            position.y -= 10f * scale;
        }
        rect.anchoredPosition = position;
    }

    /// <summary>강화 레벨에 대응하는 아이콘 색상을 반환하며, 설정이 없으면 원본 색상을 유지합니다.</summary>
    Color GetIconColor(int level)
    {
        // 색상이 지정되지 않았으면 원본 스프라이트 색상을 표시합니다.
        if (_iconColors == null || _iconColors.Length == 0)
        {
            return Color.white;
        }

        // 1레벨을 첫 번째 요소에 대응시키고 배열의 유효 범위로 제한합니다.
        int index = Mathf.Clamp(level - 1, 0, _iconColors.Length - 1);

        // 해당 강화 레벨에 설정된 색상을 반환합니다.
        return _iconColors[index];
    }

    static Color Sample(Color[] colors, float ratio)
    {
        if (colors == null || colors.Length == 0) return Color.white;
        float at = Mathf.Clamp01(ratio) * (colors.Length - 1);
        int low = Mathf.FloorToInt(at), high = Mathf.Min(low + 1, colors.Length - 1);
        return Color.Lerp(colors[low], colors[high], at - low);
    }

    void ApplyColors()
    {
        if (_background != null) _background.color = _targetBackground;
        if (_icon != null) _icon.color = _targetIcon;
    }
    #endregion

    #region Canvas Editor 연출 연결
    /// <summary>Canvas Editor에 저장한 확대·복귀와 LEVEL UP 상승·페이드 연출을 재생합니다.</summary>
    public void PlayLevelUp()
    {
        // <UIDirector:PlayLevelUp>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        {
            if (_levelUpRoot != null) _levelUpRoot.SetActive(true);
        }
        global::UIDirectorView.Dispatch(this, "PlayLevelUp", true, __UIDirectorOriginal);
        // </UIDirector>
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (_levelUpRoutine != null) StopCoroutine(_levelUpRoutine);
        _levelUpRoutine = StartCoroutine(FinishLevelUp());
    }

    IEnumerator FinishLevelUp()
    {
        yield return new WaitForSecondsRealtime(1.25f);
        if (_levelUpRoot != null) _levelUpRoot.SetActive(false);
        _levelUpRoutine = null;
    }

    /// <summary>Presenter 재연결 시 이전 상승을 다시 재생하지 않도록 표시 이력만 초기화합니다.</summary>
    public void ResetPresentation()
    {
        CancelPresentation();
        _hasLevel = false;
        if (_badge != null) _badge.gameObject.SetActive(false);
    }

    void CancelPresentation()
    {
        _colorTween?.Kill(); _colorTween = null;
        if (_levelUpRoutine != null) StopCoroutine(_levelUpRoutine);
        _levelUpRoutine = null;
        var director = GetComponent<UIDirectorView>();
        if (director != null)
        {
            director.CancelEffects(this, _badge);
            if (_levelUpRoot != null) director.CancelEffects(this, _levelUpRoot.transform as RectTransform);
        }
        if (_badge != null) _badge.localScale = Vector3.one;
        if (_levelUpRoot != null) _levelUpRoot.SetActive(false);
        if (_hasLevel) ApplyColors();
    }

    void Awake() { if (_levelUpRoot != null) _levelUpRoot.SetActive(false); }
    void OnDisable() => CancelPresentation();
    void OnDestroy() => _colorTween?.Kill();
    #endregion

    #region 머리 위 배치
    /// <summary>모델 상태를 조회하지 않고 직원 외형과 카메라의 투영 위치만 갱신합니다.</summary>
    void LateUpdate()
    {
        if (_follow == null || _badge == null) return;
        var camera = Camera.main;
        if (camera == null) return;
        Vector3 head = _follow.position + Vector3.up * 2.7f;
        bool hasRenderer = false;
        foreach (var renderer in _bodyRenderers)
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                if (!hasRenderer || renderer.bounds.max.y > head.y) head.y = renderer.bounds.max.y;
                hasRenderer = true;
            }
        Vector3 screen = camera.WorldToScreenPoint(head);
        if (screen.z <= .001f) return;
        float uiRatio = camera.pixelHeight / 1080f;
        float height = _screenHeight * uiRatio, gap = _headMargin * uiRatio;
        float width = height * _badge.rect.width / Mathf.Max(1, _badge.rect.height);
        screen.y += height * .5f + gap;
        foreach (var avoid in _avoidRects)
        {
            if (avoid == null || !avoid.gameObject.activeInHierarchy) continue;
            avoid.GetWorldCorners(_corners);
            float left = float.PositiveInfinity, right = float.NegativeInfinity, top = float.NegativeInfinity, bottom = float.PositiveInfinity;
            foreach (var corner in _corners)
            {
                var p = camera.WorldToScreenPoint(corner);
                left = Mathf.Min(left, p.x); right = Mathf.Max(right, p.x);
                top = Mathf.Max(top, p.y); bottom = Mathf.Min(bottom, p.y);
            }
            if (screen.x + width * .5f > left && screen.x - width * .5f < right &&
                screen.y - height * .5f < top + gap && screen.y + height * .5f > bottom - gap)
                screen.y = top + gap + height * .5f;
        }
        transform.position = camera.ScreenToWorldPoint(screen);
        transform.rotation = camera.transform.rotation;
        float worldHeight = camera.orthographic ? camera.orthographicSize * 2f :
            2f * screen.z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
        float scale = worldHeight * height / Mathf.Max(1, camera.pixelHeight) / Mathf.Max(1, _badge.rect.height);
        var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(scale / Safe(parentScale.x), scale / Safe(parentScale.y), scale / Safe(parentScale.z));
    }
    readonly Vector3[] _corners = new Vector3[4];
    static float Safe(float value) => Mathf.Abs(value) > .00001f ? value : 1f;
    #endregion
}
