using DG.Tweening;
using UnityEngine;

/// <summary>강화 가능한 요리의 화살표를 두 번 가볍게 튀어 올립니다.</summary>
[DisallowMultipleComponent]
public sealed class RecipeUpgradeArrowMotion : MonoBehaviour
{
    [SerializeField] Vector2 _restPosition = new Vector2(30, 25);
    [SerializeField] float _jumpHeight = 7f;
    RectTransform _rect;
    Sequence _motion;
    void OnEnable()
    {
        if (!Application.isPlaying) return;
        _rect = transform as RectTransform;
        if (_rect == null) return;
        _motion?.Kill();
        _rect.anchoredPosition = _restPosition;
        _motion = DOTween.Sequence().SetUpdate(true);
        _motion.Append(_rect.DOAnchorPosY(_restPosition.y + _jumpHeight, .2f).SetEase(Ease.OutCubic));
        _motion.Append(_rect.DOAnchorPosY(_restPosition.y, .18f).SetEase(Ease.InCubic));
        _motion.Append(_rect.DOAnchorPosY(_restPosition.y + _jumpHeight * .55f, .15f).SetEase(Ease.OutCubic));
        _motion.Append(_rect.DOAnchorPosY(_restPosition.y, .15f).SetEase(Ease.InCubic));
        _motion.AppendInterval(.65f);
        _motion.SetLoops(-1);
    }
    void OnDisable()
    {
        _motion?.Kill(); _motion = null;
        if (_rect != null) _rect.anchoredPosition = _restPosition;
    }
    void OnDestroy() { _motion?.Kill(); }
}
