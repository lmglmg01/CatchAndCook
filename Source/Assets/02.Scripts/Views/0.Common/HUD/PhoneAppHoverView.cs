using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>앱의 클릭 가능 여부를 변경하지 않고 이름 안내와 호버 흔들림만 표시합니다.</summary>
public class PhoneAppHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] TMP_Text _nameLabel;
    [SerializeField] string _appName;
    RectTransform _rect;
    Quaternion _originalRotation;
    Sequence _motion;
    Tween _return;
    PointerEventData _pointer;
    bool _hovering;
    static readonly System.Collections.Generic.Dictionary<TMP_Text, PhoneAppHoverView> Owners = new();

    /// <summary>아이콘의 원래 회전을 기억합니다.</summary>
    void Awake() { _rect = (RectTransform)transform; _originalRotation = _rect.localRotation; }

    /// <summary>비활성 앱도 이름을 안내하며 기존 스마트폰과 같은 2도 흔들림을 시작합니다.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || _hovering) return;
        _hovering = true; _pointer = eventData;
        if (_nameLabel != null)
        {
            if (Owners.TryGetValue(_nameLabel, out var previous) && previous != null && previous != this) previous.StopHover(true);
            Owners[_nameLabel] = this;
            _nameLabel.text = _appName;
        }
        _motion?.Kill(); _return?.Kill(); _rect.localRotation = _originalRotation;
        var angles = _originalRotation.eulerAngles;
        _motion = DOTween.Sequence().SetUpdate(true)
            .Append(_rect.DOLocalRotate(angles + Vector3.back * 2f, .125f).SetEase(Ease.InOutCubic))
            .Append(_rect.DOLocalRotate(angles + Vector3.forward * 2f, .25f).SetEase(Ease.InOutCubic))
            .Append(_rect.DOLocalRotate(angles, .125f).SetEase(Ease.InOutCubic))
            .AppendInterval(1.5f).SetLoops(-1);
    }

    /// <summary>마우스가 벗어나면 이름을 지우고 원래 각도로 복귀합니다.</summary>
    public void OnPointerExit(PointerEventData eventData) { StopHover(false); }

    /// <summary>스마트폰이 이동하거나 숨겨져 포인터 밖으로 나가도 남은 안내를 지웁니다.</summary>
    void LateUpdate()
    {
        if (!_hovering) return;
        if (_pointer != null && !RectTransformUtility.RectangleContainsScreenPoint(_rect, _pointer.position, _pointer.enterEventCamera)) { StopHover(true); return; }
        foreach (var group in GetComponentsInParent<CanvasGroup>())
            if (group.alpha <= .001f || !group.blocksRaycasts) { StopHover(true); return; }
    }

    /// <summary>자신의 트윈과 자신이 표시한 이름만 정리해 빠른 아이콘 이동에도 충돌을 방지합니다.</summary>
    void StopHover(bool immediate)
    {
        _hovering = false; _pointer = null; _motion?.Kill(); _motion = null; _return?.Kill(); _return = null;
        if (_nameLabel != null && Owners.TryGetValue(_nameLabel, out var owner) && owner == this)
        { Owners.Remove(_nameLabel); _nameLabel.text = string.Empty; }
        if (_rect == null) return;
        if (immediate) _rect.localRotation = _originalRotation;
        else _return = _rect.DOLocalRotateQuaternion(_originalRotation, .125f).SetEase(Ease.InOutCubic).SetUpdate(true);
    }

    /// <summary>오브젝트가 비활성화되면 표시와 회전을 즉시 복원합니다.</summary>
    void OnDisable() { StopHover(true); }
    /// <summary>씬 전환으로 제거될 때 트윈과 소유권을 정리합니다.</summary>
    void OnDestroy() { StopHover(true); }
}
