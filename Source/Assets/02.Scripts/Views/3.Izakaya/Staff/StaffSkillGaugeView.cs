using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기본 1레벨 이후의 네 번 강화 진행만 표시합니다. 강화와 특전 해방 판정은 변경하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class StaffSkillGaugeView : MonoBehaviour
{
    [SerializeField] Texture _backgroundTexture, _fillTexture;
    [SerializeField] UnityEngine.UI.Image _skillIcon;
    [SerializeField] GameObject _completionPage;
    readonly UnityEngine.UI.RawImage[] _fills = new UnityEngine.UI.RawImage[4];
    public bool IsFull => _shown >= 3.999f;
    public System.Action OnFull { get; set; }
    bool _hasValue;
    int _staffId = -1;
    float _shown, _target;
    Tween _tween;
    Coroutine _pending;

    public void Configure(Texture background, Texture fill, UnityEngine.UI.Image icon, GameObject completionPage)
    {
        _backgroundTexture = background; _fillTexture = fill; _skillIcon = icon; _completionPage = completionPage;
        var old = transform.Find("Level5"); if (old != null) old.gameObject.SetActive(false);
        for (int i = 0; i < 4; i++)
        {
            var cell = transform.Find("Level" + (i + 1));
            if (cell == null)
            {
                cell = new GameObject("Level" + (i + 1), typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).transform;
                cell.SetParent(transform, false);
            }
            cell.gameObject.SetActive(true);
            var rect = (RectTransform)cell;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(-36 + 24 * i, 0); rect.sizeDelta = new Vector2(20, 14);
            var backgroundImage = cell.GetComponent<UnityEngine.UI.RawImage>();
            backgroundImage.texture = _backgroundTexture; backgroundImage.uvRect = new Rect(0, 0, 20f / 92f, 1);
            backgroundImage.color = Color.white; backgroundImage.raycastTarget = false;
            var front = cell.Find("AnimatedFill");
            if (front == null)
            {
                front = new GameObject("AnimatedFill", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).transform;
                front.SetParent(cell, false);
            }
            _fills[i] = front.GetComponent<UnityEngine.UI.RawImage>();
            _fills[i].texture = _fillTexture; _fills[i].color = Color.white; _fills[i].raycastTarget = false;
            var fillRect = _fills[i].rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0, .5f);
            fillRect.anchoredPosition = Vector2.zero;
        }
        Render(_shown);
    }

    public void Display(int staffId, int level, bool specialUnlocked)
    {
        float next = Mathf.Clamp(level - 1, 0, 4);
        if (!_hasValue || _staffId != staffId || !Application.isPlaying || next < _target)
        {
            Cancel(); _hasValue = true; _staffId = staffId; _target = next; Render(next); return;
        }
        if (Mathf.Approximately(_target, next)) { Render(_shown); return; }
        Cancel(); _target = next;
        _pending = StartCoroutine(AnimateAfterConfirmation());
    }

    IEnumerator AnimateAfterConfirmation()
    {
        // 강화 성공 팝업의 확인을 마친 뒤 게이지가 보이는 상태에서 채웁니다.
        yield return null;
        while (_completionPage != null && _completionPage.activeInHierarchy) yield return null;
        _pending = null;
        _tween = DOTween.To(() => _shown, Render, _target, .5f).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(() => { if (IsFull) OnFull?.Invoke(); });
    }

    void Render(float value)
    {
        _shown = value;
        for (int i = 0; i < 4; i++)
        {
            if (_fills[i] == null) continue;
            float amount = Mathf.Clamp01(value - i);
            _fills[i].enabled = amount > 0;
            _fills[i].rectTransform.sizeDelta = new Vector2(20 * amount, 14);
            _fills[i].uvRect = new Rect(0, 0, 20f / 92f * amount, 1);
        }
        if (_skillIcon != null) _skillIcon.color = Color.white;
    }

    void Cancel()
    {
        _tween?.Kill(); _tween = null;
        if (_pending != null) StopCoroutine(_pending);
        _pending = null;
    }
    void OnDisable() { Cancel(); _hasValue = false; }
    void OnDestroy() => Cancel();
}
