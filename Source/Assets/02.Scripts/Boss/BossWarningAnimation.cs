using UnityEngine;
using UnityEngine.UI;

/// <summary>보스 조우 알림이 활성화된 동안 배너와 화면 가장자리를 움직입니다.</summary>
[DisallowMultipleComponent]
public class BossWarningAnimation : MonoBehaviour
{
    [Header("경고 이미지")]
    [SerializeField] Image _vignette;
    [SerializeField] CanvasGroup _art;
    [SerializeField] RectTransform _fish;
    [SerializeField] RectTransform _lettering;

    [Header("반복 연출")]
    [SerializeField, Min(.05f)] float _pulsePeriod;
    [SerializeField, Range(0f, .5f)] float _letterScaleAmount;
    [SerializeField, Min(0f)] float _fishTravel;
    [SerializeField, Min(0f)] float _fishTilt;
    [SerializeField, Range(0f, 1f)] float _redIntensity;

    [Header("상하 화살표")]
    [SerializeField] Image _topArrows;
    [SerializeField] Image _bottomArrows;
    [SerializeField, Min(0f)] float _scrollSpeed;

    Vector2 _fishCenter;
    float _elapsed;

    void Awake()
    {
        _fishCenter = _fish.anchoredPosition;
    }

    void OnEnable()
    {
        _elapsed = 0f;
        _art.alpha = 0f;
        _vignette.canvasRenderer.SetAlpha(0f);
    }

    void LateUpdate()
    {
        _elapsed += Time.unscaledDeltaTime;
        float phase = _elapsed * (2f * Mathf.PI / Mathf.Max(.05f, _pulsePeriod));
        float wave = Mathf.Sin(phase);
        _fish.anchoredPosition = _fishCenter + new Vector2(wave, .35f * Mathf.Sin(phase * 2f)) * _fishTravel;
        _fish.localRotation = Quaternion.Euler(0f, 0f, wave * _fishTilt);
        _lettering.localScale = Vector3.one * (1f + wave * _letterScaleAmount);

        // BossView가 제어하는 등장·퇴장 페이드에 화면 점멸을 겹칩니다.
        _art.alpha = _vignette.color.a;
        _vignette.canvasRenderer.SetAlpha((.5f - .5f * Mathf.Cos(phase)) * _redIntensity);

        ScrollArrows(_topArrows, _elapsed * _scrollSpeed);
        ScrollArrows(_bottomArrows, -_elapsed * _scrollSpeed);
    }

    static void ScrollArrows(Image strip, float distance)
    {
        RectTransform rect = strip.rectTransform;
        Rect track = ((RectTransform)rect.parent).rect;
        if (track.height <= 0f) return;

        // 화살표 한 개의 폭만큼 이동한 뒤 같은 무늬에서 다시 시작합니다.
        float pitch = strip.sprite.rect.height / strip.sprite.rect.width * track.height * rect.localScale.y;
        float repeat = Mathf.Max(1f, pitch);
        float length = (Mathf.Ceil(track.width / repeat) + 4f) * repeat;
        float multiplier = strip.sprite.rect.width / (track.height * strip.pixelsPerUnit);
        if (!Mathf.Approximately(strip.pixelsPerUnitMultiplier, multiplier))
            strip.pixelsPerUnitMultiplier = multiplier;
        Vector2 size = new Vector2(track.height, length / rect.localScale.y);
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(Mathf.Repeat(distance, repeat), 0f);
    }
}

