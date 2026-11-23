using System;
using System.Linq;
using DG.Tweening;
using UnityEngine;

/// <summary>결과판, 기존 퀘스트 도장, 획득 목록, 확인 버튼을 차례로 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class DiveResultIntroView : MonoBehaviour
{
    [SerializeField] RectTransform _board;
    [SerializeField] CanvasGroup _items, _confirm;
    [SerializeField] RectTransform _successStamp, _failedStamp;
    [SerializeField] Vector2 _centerOffset = new Vector2(400, 0);
    [SerializeField] float _popDuration = .55f, _stampHold = .15f, _slideDuration = .55f, _buttonDuration = .2f;
    Sequence _sequence;
    Action _finished;
    public bool IsReady { get; private set; } = true;

    public void Play(bool success, Action finished)
    {
        Cancel();
        _finished = finished;
        if (!Application.isPlaying || _board == null || _items == null || _confirm == null)
        { IsReady = true; _finished = null; finished?.Invoke(); return; }
        IsReady = false;
        _board.anchoredPosition = _centerOffset + new Vector2(0, -1100);
        _items.alpha = _confirm.alpha = 0;
        _items.interactable = _items.blocksRaycasts = false;
        _confirm.interactable = _confirm.blocksRaycasts = false;
        _successStamp.gameObject.SetActive(false);
        _failedStamp.gameObject.SetActive(false);
        var stamp = success ? _successStamp : _failedStamp;
        string method = success ? "StampSuccess" : "StampFailure";
        var director = GetComponent<UIDirectorView>();
        var binding = director != null ? director.Bindings.FirstOrDefault(b => b.owner == this && b.enterMethod == method) : null;
        float stampDuration = 0, previousStart = 0, previousEnd = 0;
        if (binding != null)
            foreach (var effect in binding.enter.Where(e => e.enabled))
            {
                previousStart = UIDirectorView.Start(effect, previousStart, previousEnd);
                previousEnd = previousStart + Mathf.Max(.01f, effect.duration);
                stampDuration = Mathf.Max(stampDuration, previousEnd);
            }
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Append(_board.DOAnchorPos(_centerOffset, _popDuration).SetEase(Ease.OutBack));
        _sequence.AppendCallback(() => UIDirectorView.Dispatch(this, method, true, () => stamp.gameObject.SetActive(true)));
        _sequence.AppendInterval(stampDuration + _stampHold);
        _sequence.Append(_board.DOAnchorPos(Vector2.zero, _slideDuration).SetEase(Ease.InOutCubic));
        _sequence.Join(_items.DOFade(1, _slideDuration).SetEase(Ease.OutCubic));
        _sequence.Append(_confirm.DOFade(1, _buttonDuration).SetEase(Ease.OutQuad));
        _sequence.OnComplete(() =>
        {
            _sequence = null;
            IsReady = true;
            _items.interactable = _items.blocksRaycasts = true;
            _confirm.interactable = _confirm.blocksRaycasts = true;
            var callback = _finished; _finished = null;
            callback?.Invoke();
        });
    }

    public void Cancel()
    {
        _sequence?.Kill(false); _sequence = null; _finished = null;
        var director = GetComponent<UIDirectorView>();
        if (director != null)
        {
            director.CancelEffects(this, _successStamp);
            director.CancelEffects(this, _failedStamp);
        }
        if (_board != null) _board.anchoredPosition = Vector2.zero;
        foreach (var group in new[] { _items, _confirm })
            if (group != null) { group.alpha = 1; group.interactable = group.blocksRaycasts = true; }
        IsReady = true;
    }
    void OnDisable() { Cancel(); }
}
