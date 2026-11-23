using DG.Tweening;
using UnityEngine;

/// <summary>게이지 완료 후 특전 슬롯의 자물쇠와 아이콘만 연출합니다.</summary>
[DisallowMultipleComponent]
public sealed class StaffSkillUnlockView : MonoBehaviour
{
    [SerializeField] RectTransform _lock;
    [SerializeField] CanvasGroup _lockGroup, _iconGroup;
    [SerializeField] GameObject _effect, _caption;
    bool _hasValue, _unlocked, _showEffect, _showCaption;
    int _staffId = -1;
    Sequence _sequence;

    public void Configure(RectTransform lockRoot, Transform icon, GameObject effect, GameObject caption)
    {
        _lock = lockRoot;
        _lockGroup = lockRoot.GetComponent<CanvasGroup>();
        if (_lockGroup == null) _lockGroup = lockRoot.gameObject.AddComponent<CanvasGroup>();
        _iconGroup = icon.GetComponent<CanvasGroup>();
        if (_iconGroup == null) _iconGroup = icon.gameObject.AddComponent<CanvasGroup>();
        _effect = effect; _caption = caption;
        _showEffect = effect != null && effect.activeSelf;
        _showCaption = caption != null && caption.activeSelf;
    }

    public void Display(int staffId, bool unlocked)
    {
        bool reveal = Application.isPlaying && _hasValue && _staffId == staffId && !_unlocked && unlocked;
        bool changing = !_hasValue || _staffId != staffId || _unlocked != unlocked;
        _hasValue = true; _staffId = staffId; _unlocked = unlocked;
        if (reveal)
        {
            Cancel();
            _lock.gameObject.SetActive(true); _lock.localRotation = Quaternion.identity; _lockGroup.alpha = 1;
            _iconGroup.gameObject.SetActive(true); _iconGroup.alpha = 0;
            if (_effect != null) _effect.SetActive(false);
            if (_caption != null) _caption.SetActive(false);
            _sequence = DOTween.Sequence().SetUpdate(true)
                .Append(_lock.DOShakeRotation(.4f, new Vector3(0, 0, 16), 12, 90))
                .Append(_lockGroup.DOFade(0, .18f))
                .AppendCallback(() => _lock.gameObject.SetActive(false))
                .Append(_iconGroup.DOFade(1, .2f))
                .OnComplete(() => { _sequence = null; ApplyStatic(); });
            return;
        }
        if (changing) Cancel();
        if (_sequence != null && _sequence.IsActive())
        {
            _iconGroup.gameObject.SetActive(true);
            if (_effect != null) _effect.SetActive(false);
            if (_caption != null) _caption.SetActive(false);
            return;
        }
        ApplyStatic();
    }

    void ApplyStatic()
    {
        if (_lock != null) { _lock.localRotation = Quaternion.identity; _lock.gameObject.SetActive(!_unlocked); }
        if (_lockGroup != null) _lockGroup.alpha = 1;
        if (_iconGroup != null) { _iconGroup.alpha = 1; _iconGroup.gameObject.SetActive(_unlocked); }
        if (_effect != null) _effect.SetActive(_unlocked && _showEffect);
        if (_caption != null) _caption.SetActive(_unlocked && _showCaption);
    }
    void Cancel() { _sequence?.Kill(); _sequence = null; }
    void OnDisable() { Cancel(); _hasValue = false; }
    void OnDestroy() => Cancel();
}
