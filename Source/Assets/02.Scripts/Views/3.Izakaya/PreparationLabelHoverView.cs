using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>하단 준비 버튼에 마우스를 올리면 이름을 아래로 펼칩니다.</summary>
[DisallowMultipleComponent]
public sealed class PreparationLabelHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] RectTransform _label;
    [SerializeField] CanvasGroup _group;
    [SerializeField] Vector2 _hiddenPosition = new Vector2(0, -68);
    [SerializeField] Vector2 _shownPosition = new Vector2(0, -96);
    Sequence _motion;
    UnityEngine.UI.Button _button;
    bool _hovering;

    void Awake() { _button = GetComponent<UnityEngine.UI.Button>(); }
    void OnEnable() { ResetLabel(); }
    void OnDisable() { ResetLabel(); }
    void OnDestroy() { _motion?.Kill(); }
    void LateUpdate()
    {
        if (_hovering && _button != null && !_button.IsInteractable()) ResetLabel();
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && !_button.IsInteractable()) return;
        Animate(true);
    }
    public void OnPointerExit(PointerEventData eventData) { Animate(false); }
    void Animate(bool show)
    {
        if (_label == null || _group == null) return;
        _hovering = show;
        _motion?.Kill();
        _motion = DOTween.Sequence().SetUpdate(true);
        _motion.Join(_label.DOAnchorPos(show ? _shownPosition : _hiddenPosition, show ? .22f : .14f)
            .SetEase(show ? Ease.OutBack : Ease.InCubic));
        _motion.Join(_group.DOFade(show ? 1 : 0, show ? .12f : .1f));
    }
    void ResetLabel()
    {
        _motion?.Kill(); _motion = null; _hovering = false;
        if (_label != null) _label.anchoredPosition = _hiddenPosition;
        if (_group != null) { _group.alpha = 0; _group.blocksRaycasts = false; _group.interactable = false; }
    }
}
