using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>책 안쪽 끝을 고정하고 원화의 기울기를 따라 늘리며 아이콘의 비율을 유지합니다.</summary>
public sealed class BookBookmarkMotionView : UnityEngine.UI.BaseMeshEffect, IPointerEnterHandler, IPointerExitHandler
{
    public const float HiddenBackgroundLength = 96f;
    public const float SlideDistance = 80f; // Normalized motion range; layout dimensions are specified below.
    public static readonly Vector2 RestTabSize = new Vector2(96f, 96f);
    public static readonly Vector2 SelectedTabSize = new Vector2(140f, 110f);
    public static readonly Vector2 RestIconBounds = new Vector2(74f, 74f);
    public static readonly Vector2 SelectedIconBounds = RestIconBounds * SelectedIconScale;
    public const float SelectDuration = .28f;
    public const float ReturnDuration = .22f;
    const float HoverScale = 1.18f;
    const float SelectedIconScale = 1.30f;
    RectTransform _rect, _icon;
    Vector2 _restPosition, _direction;
    Vector3 _iconBaseScale = Vector3.one;
    float _extension, _iconBaseAngle, _idleStarted;
    Tween _motion, _hover;
    bool _configured, _needsReveal = true, _selected;
    public bool IsSelected => _selected;
    public float Extension => _extension;
    public Vector2 RestPosition => _restPosition;
    public Vector2 Destination => _restPosition + StretchOffset(_selected ? 1f : 0f) * .5f - AlongBookmark(HiddenBackgroundLength * .5f);
    Vector2 AlongBookmark(float x) => new Vector2(x, x * _direction.y / Mathf.Max(.001f, _direction.x));
    Vector2 StretchOffset(float progress)
    {
        float x = (SelectedTabSize.x - RestTabSize.x) * progress;
        return new Vector2(x, x * _direction.y / Mathf.Max(.001f, _direction.x));
    }

    void Cache()
    {
        if (_rect == null) _rect = transform as RectTransform;
        if (_icon != null) return;
        _icon = transform.Find("BookmarkIcon") as RectTransform;
        if (_icon != null) _iconBaseScale = _icon.localScale;
    }

    public void Animate(bool selected, Vector2 restPosition, Vector2 direction)
    {
        Cache();
        if (_rect == null || _icon == null) return;
        bool changed = !_configured || _selected != selected;
        _restPosition = restPosition; _direction = direction;
        _iconBaseAngle = _icon.localEulerAngles.z;
        _selected = selected;
        if (changed || _needsReveal) { _idleStarted = Time.unscaledTime + SelectDuration; SetHover(false, false); }
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            _motion?.Kill(false); _motion = null;
            _extension = Application.isPlaying ? 0f : selected ? SlideDistance : 0f;
            _configured = true; _needsReveal = true; ApplyExtension(); return;
        }
        if (_needsReveal) _extension = 0f;
        bool animate = changed || _needsReveal;
        _configured = true; _needsReveal = false;
        ApplyExtension();
        if (!animate) return;
        _motion?.Kill(false);
        var target = selected ? SlideDistance : 0f;
        _motion = DOTween.To(() => _extension, x => { _extension = x; ApplyExtension(); }, target,
            selected ? SelectDuration : ReturnDuration).SetEase(Ease.OutCubic).SetUpdate(true)
            .OnComplete(() => _motion = null);
    }

    void ApplyExtension()
    {
        if (_rect == null) return;
        float progress = Mathf.Clamp01(_extension / SlideDistance);
        var visibleSize = Vector2.Lerp(RestTabSize, SelectedTabSize, progress);
        // The specified tab dimensions describe the exposed portion. Keep a continuous
        // 96 px shaft under BookFront, moving only the outer end when it is selected.
        _rect.sizeDelta = visibleSize + new Vector2(HiddenBackgroundLength, 0f);
        var buriedHalf = AlongBookmark(HiddenBackgroundLength * .5f);
        _rect.anchoredPosition = _restPosition + StretchOffset(progress) * .5f - buriedHalf;
        // Keep the icon near the tip as the long bookmark is drawn out of the book.
        var iconBox = Vector2.Lerp(RestIconBounds, SelectedIconBounds, progress);
        float tipInset = 11f;
        float iconOffset = visibleSize.x * .5f - iconBox.x * .5f - tipInset;
        if (_icon != null) _icon.anchoredPosition = buriedHalf + AlongBookmark(iconOffset);
        FitIcon();
        if (graphic != null) graphic.SetVerticesDirty();
    }

    void FitIcon()
    {
        if (_icon == null) return;
        var image = _icon.GetComponent<UnityEngine.UI.Image>();
        if (image == null || image.sprite == null) return;
        var source = image.sprite.rect.size;
        // 기본 크기는 상태와 무관하게 고정합니다. 확대는 아이콘의 localScale만 담당합니다.
        var box = RestIconBounds;
        float fit = float.MaxValue;
        // Include the entire idle arc so enlarged artwork never exceeds its box.
        for (int i = -3; i <= 3; i++)
        {
            float angle = (_iconBaseAngle + i) * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(angle)), sin = Mathf.Abs(Mathf.Sin(angle));
            fit = Mathf.Min(fit, box.x / (source.x * c + source.y * sin),
                box.y / (source.x * sin + source.y * c));
        }
        _icon.sizeDelta = source * fit;
        image.preserveAspect = true;
    }

    public override void ModifyMesh(UnityEngine.UI.VertexHelper vertices)
    {
        if (!IsActive() || _rect == null || vertices.currentVertCount == 0) return;
        var bounds = _rect.rect;
        var image = graphic as UnityEngine.UI.Image;
        // Normalize the opaque shaft, rather than the PNG rectangle including empty margins.
        float artworkScale = image != null && image.sprite != null ? image.sprite.name switch
        {
            "Index_Todays" => 100f / 96f,
            "Index_Upgradee" => 130f / 110f,
            "Index_Drink" => 105f / 97f,
            "Index_Staff" => 130f / 110f,
            _ => 1f
        } : 1f;
        var vertex = new UIVertex();
        for (int i = 0; i < vertices.currentVertCount; i++)
        {
            vertices.PopulateUIVertex(ref vertex, i);
            float u = Mathf.InverseLerp(bounds.xMin, bounds.xMax, vertex.position.x);
            // Keep the artwork centerline angle measured from the approved 150 x 96 layout.
            float slope = _direction.y / Mathf.Max(.001f, _direction.x);
            float shear = slope * (bounds.width - 150f * bounds.height * artworkScale / 96f);
            vertex.position.y = vertex.position.y * artworkScale + shear * (u - .5f);
            vertices.SetUIVertex(vertex, i);
        }
    }

    void LateUpdate()
    {
        if (_icon == null || !_configured) return;
        float t = Mathf.Max(0f, Time.unscaledTime - _idleStarted);
        float idle = _selected ? Mathf.Sin(t * Mathf.PI * 2f / .9f) * 3f * Mathf.SmoothStep(0f, 1f, t / .25f) : 0f;
        _icon.localRotation = Quaternion.Euler(0f, 0f, _iconBaseAngle + idle);
        FitIcon();
    }

    void SetHover(bool hovered, bool instant)
    {
        Cache(); if (_icon == null) return;
        _hover?.Kill(false);
        var target = _iconBaseScale * (_selected ? SelectedIconScale : hovered ? HoverScale : 1f);
        if (instant || !Application.isPlaying || !isActiveAndEnabled) { _icon.localScale = target; _hover = null; return; }
        _hover = _icon.DOScale(target, hovered ? .12f : .10f).SetEase(Ease.OutCubic).SetUpdate(true);
    }
    public void OnPointerEnter(PointerEventData eventData) { if (!_selected) SetHover(true, false); }
    public void OnPointerExit(PointerEventData eventData) { SetHover(false, false); }
    protected override void OnEnable() { base.OnEnable(); _needsReveal = true; }
    protected override void OnDisable()
    {
        _motion?.Kill(false); _motion = null; _hover?.Kill(false); _hover = null;
        _extension = 0f; if (_configured) ApplyExtension();
        if (_icon != null) { _icon.localScale = _iconBaseScale; _icon.localRotation = Quaternion.Euler(0f, 0f, _iconBaseAngle); }
        _needsReveal = true; base.OnDisable();
    }
    protected override void OnDestroy() { _motion?.Kill(false); _hover?.Kill(false); base.OnDestroy(); }
}

