using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

public class DiveLoadView : MonoBehaviour
{
    [Header("영상 배경")]
    [SerializeField] VideoPlayer _videoPlayer;
    [SerializeField] UnityEngine.UI.RawImage _videoImage;
    [SerializeField] UnityEngine.UI.AspectRatioFitter _videoAspectRatio;

    [Header("페이드 연출")]
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField, Min(0f)] float _fadeInDuration;
    [SerializeField, Min(0f)] float _fadeOutDuration;

    [Header("로딩 표시")]
    [SerializeField] RectTransform _loadingSpinner;
    [SerializeField] TMP_Text _loadingText;

    [Header("탐험 팁")]
    [SerializeField] TMP_Text _tipTitle;
    [SerializeField] TMP_Text _tipDescription;
    [SerializeField] UnityEngine.UI.Image _tipIcon;
    [SerializeField] LoadingTipCatalog _tips;

    const float SpinnerBobSpeed = 3.4f;
    const float SpinnerBobHeight = 5f;
    const float SpinnerCycle = 2f * Mathf.PI / SpinnerBobSpeed;

    readonly struct SplashDrop
    {
        static readonly Color32 Tint = new Color32(252, 241, 234, 255);

        readonly UnityEngine.UI.RawImage _image;
        readonly float _delay;
        readonly float _duration;
        readonly float _reach;
        readonly float _height;
        readonly float _length;

        public SplashDrop(UnityEngine.UI.RawImage image, float delay, float duration, float reach, float height, float length)
        {
            _image = image;
            _delay = delay;
            _duration = duration;
            _reach = reach;
            _height = height;
            _length = length;
        }

        public void Animate(float elapsed, float fishWidth)
        {
            float progress = (elapsed - _delay) / _duration;
            _image.enabled = progress > 0f && progress < 1f;
            if (!_image.enabled) return;

            float remaining = 1f - progress;
            float scale = Mathf.SmoothStep(0f, 1f, progress / .28f)
                * Mathf.SmoothStep(0f, 1f, remaining / .58f);
            float flyOut = 1f - remaining * remaining;
            float arc = 4f * progress * remaining;

            var rect = _image.rectTransform;
            rect.anchoredPosition = new Vector2(
                Mathf.Lerp(.22f * Mathf.Sign(_reach), _reach, flyOut) * fishWidth,
                _height * arc * fishWidth);
            rect.sizeDelta = new Vector2(.53f, 1f) * (_length * fishWidth);
            rect.localScale = Vector3.one * scale;
            float launchAngle = Mathf.Atan2(-_reach, 2f * _height) * Mathf.Rad2Deg;
            rect.localRotation = Quaternion.Euler(0f, 0f, launchAngle - Mathf.Sign(_reach) * 22f * progress);

            Color color = Tint;
            color.a = Mathf.SmoothStep(0f, 1f, remaining / .45f);
            _image.color = color;
        }
    }

    Vector2 _spinnerOrigin;
    double _spinnerStartedAt;
    RectTransform _splashOrigin;
    Texture2D _splashTexture;
    Texture2D _tipBulbTexture;
    SplashDrop[] _splashDrops;
    Tween _fadeTween;
    Coroutine _fadeOutRoutine;
    bool _fadingOut;
    bool _keptForSceneTransition;

    public bool IsFadingIn => !_fadingOut && _fadeTween != null && _fadeTween.IsActive() && _fadeTween.IsPlaying();
    public bool IsKeptForSceneTransition => _keptForSceneTransition;

    void Awake()
    {
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        CreateGuideTipBulb();
        if (_loadingSpinner == null) return;
        _spinnerOrigin = _loadingSpinner.anchoredPosition;
        CreateSplash();
    }

    void OnEnable()
    {
        _spinnerStartedAt = Time.unscaledTimeAsDouble;
        if (_splashOrigin != null) _splashOrigin.gameObject.SetActive(true);
        AnimateSpinner(0f);

        if (_tips != null && _tips.TryPick(out var tip))
            UpdateTip(tip.Title, tip.Description, tip.Icon);

        BeginFadeIn();
        if (_videoImage != null)
        {
            _videoImage.texture = null;
            _videoImage.enabled = false;
        }
        if (_videoPlayer == null) return;

        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.renderMode = VideoRenderMode.APIOnly;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        _videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        _videoPlayer.sendFrameReadyEvents = true;
        _videoPlayer.prepareCompleted += OnVideoPrepared;
        _videoPlayer.frameReady += OnFirstFrameReady;
        _videoPlayer.errorReceived += OnVideoError;
        _videoPlayer.Prepare();
    }

    void Update()
    {
        float elapsed = (float)((Time.unscaledTimeAsDouble - _spinnerStartedAt) % SpinnerCycle);
        AnimateSpinner(elapsed);
    }

    void AnimateSpinner(float elapsed)
    {
        if (_loadingSpinner == null) return;

        float swim = -Mathf.Cos(elapsed * SpinnerBobSpeed);
        _loadingSpinner.anchoredPosition = _spinnerOrigin + Vector2.up * (swim * SpinnerBobHeight);
        _loadingSpinner.localRotation = Quaternion.Euler(0f, 0f, swim * 7f);

        if (_splashOrigin == null) return;

        Rect fish = _loadingSpinner.rect;
        _splashOrigin.anchoredPosition = _spinnerOrigin
            + new Vector2(fish.center.x, fish.yMin - SpinnerBobHeight + fish.height * .12f);

        foreach (SplashDrop drop in _splashDrops)
            drop.Animate(elapsed, fish.width);
    }

    void CreateSplash()
    {
        if (!(_loadingSpinner.parent is RectTransform parent)) return;

        var splash = new GameObject("FishSplash", typeof(RectTransform));
        splash.layer = _loadingSpinner.gameObject.layer;
        _splashOrigin = (RectTransform)splash.transform;
        _splashOrigin.SetParent(parent, false);
        _splashOrigin.SetSiblingIndex(_loadingSpinner.GetSiblingIndex());
        Vector2 anchor = _loadingSpinner.anchorMin
            + Vector2.Scale(_loadingSpinner.anchorMax - _loadingSpinner.anchorMin, _loadingSpinner.pivot);
        _splashOrigin.anchorMin = anchor;
        _splashOrigin.anchorMax = anchor;
        _splashOrigin.sizeDelta = Vector2.zero;

        _splashTexture = CreateSplashTexture();
        _splashDrops = new[]
        {
            new SplashDrop(CreateSplashImage("LeftDrop"), delay: .15f, duration: .52f, reach: -.81f, height: .17f, length: .23f),
            new SplashDrop(CreateSplashImage("HighRightDrop"), delay: .17f, duration: .59f, reach: .93f, height: .29f, length: .34f),
            new SplashDrop(CreateSplashImage("LowRightDrop"), delay: .23f, duration: .49f, reach: .81f, height: .12f, length: .27f)
        };
    }

    UnityEngine.UI.RawImage CreateSplashImage(string name)
    {
        var drop = new GameObject(name, typeof(RectTransform));
        drop.layer = _splashOrigin.gameObject.layer;
        drop.transform.SetParent(_splashOrigin, false);
        var image = drop.AddComponent<UnityEngine.UI.RawImage>();
        image.texture = _splashTexture;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    static Texture2D CreateSplashTexture()
    {
        const int width = 40;
        const int height = 64;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Loading Water Drop",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            // Taper the bottom into a curved tail and keep the wider end round.
            float t = Mathf.Clamp01((y - 2f) / (height - 5f));
            float center = .5f + .11f * (1f - t) * (1f - t);
            float halfWidth = 1.16f * t * Mathf.Sqrt(1f - t);
            for (int x = 0; x < width; x++)
            {
                float distance = Mathf.Abs((x + .5f) / width - center);
                float coverage = Mathf.Clamp01((halfWidth - distance) * width + .5f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    void OnDestroy()
    {
        if (_splashOrigin != null) Destroy(_splashOrigin.gameObject);
        if (_splashTexture != null) Destroy(_splashTexture);
        if (_tipBulbTexture != null) Destroy(_tipBulbTexture);
    }

    void BeginFadeIn()
    {
        _fadingOut = false;
        _fadeTween?.Kill();
        _fadeTween = null;
        if (_canvasGroup == null) return;

        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = _fadeInDuration > 0f ? 0f : 1f;
        if (_fadeInDuration <= 0f) return;
        _fadeTween = _canvasGroup.DOFade(1f, _fadeInDuration)
            .SetEase(Ease.InOutSine).SetUpdate(true)
            .OnComplete(() => _fadeTween = null);
    }

    void OnDisable()
    {
        if (_splashOrigin != null) _splashOrigin.gameObject.SetActive(false);

        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }
        _fadingOut = false;
        _fadeTween?.Kill();
        _fadeTween = null;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }
        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= OnVideoPrepared;
            _videoPlayer.frameReady -= OnFirstFrameReady;
            _videoPlayer.errorReceived -= OnVideoError;
            _videoPlayer.sendFrameReadyEvents = false;
            _videoPlayer.Stop();
        }
        if (_videoImage != null)
        {
            _videoImage.texture = null;
            _videoImage.enabled = false;
        }
    }

    void OnVideoPrepared(VideoPlayer player)
    {
        if (!isActiveAndEnabled) return;
        if (_videoAspectRatio != null && player.height > 0)
            _videoAspectRatio.aspectRatio = (float)player.width / player.height;
        player.Play();
    }

    void OnFirstFrameReady(VideoPlayer player, long frameIndex)
    {
        if (!isActiveAndEnabled || player.texture == null) return;
        if (_videoImage != null)
        {
            _videoImage.texture = player.texture;
            _videoImage.enabled = true;
        }
        player.sendFrameReadyEvents = false;
    }

    void OnVideoError(VideoPlayer player, string message)
    {
        Debug.LogWarning($"[DiveLoadView] 로딩 배경 영상을 재생하지 못했습니다: {message}", this);
    }

    // 투명 여백을 제외한 실제 그림이 210px 슬롯의 중앙 70% 안에 들어가도록 맞춥니다.
    void FitGuideTipIcon(Sprite icon)
    {
        if (icon == null || _tipIcon == null) return;
        Vector4 bounds;
        switch (icon.name)
        {
            case "Gig": bounds = new Vector4(33, 88, 225, 174); break;
            case "OxygenTankHigh": bounds = new Vector4(69, 44, 209, 224); break;
            case "FishBag": bounds = new Vector4(52, 45, 211, 213); break;
            case "GunHigh": bounds = new Vector4(27, 41, 227, 214); break;
            case "BlueTangNigiri": bounds = new Vector4(61, 88, 192, 171); break;
            default:
                _tipIcon.rectTransform.sizeDelta = new Vector2(147, 147);
                _tipIcon.rectTransform.anchoredPosition = new Vector2(155, 0);
                _tipIcon.preserveAspect = true;
                return;
        }
        float scale = 147f / Mathf.Max(bounds.z - bounds.x, bounds.w - bounds.y);
        _tipIcon.rectTransform.sizeDelta = Vector2.one * (256f * scale);
        _tipIcon.rectTransform.anchoredPosition = new Vector2(155, 0) + new Vector2(
            128f - (bounds.x + bounds.z) * .5f,
            (bounds.y + bounds.w) * .5f - 128f) * scale;
        _tipIcon.preserveAspect = true;
    }

    // 전용 전구 리소스가 없어 가이드의 전구 윤곽을 작은 UI 텍스처로 구성합니다.
    void CreateGuideTipBulb()
    {
        var holder = transform.Find("ExplorationTip/TipHighlight");
        if (holder == null || holder.Find("GuideBulb") != null) return;
        var original = holder.GetComponent<UnityEngine.UI.Image>();
        if (original != null) original.enabled = false;
        var child = new GameObject("GuideBulb", typeof(RectTransform));
        child.layer = holder.gameObject.layer;
        var rect = (RectTransform)child.transform;
        rect.SetParent(holder, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = child.AddComponent<UnityEngine.UI.RawImage>();
        image.raycastTarget = false;
        const int width = 160, height = 200;
        _tipBulbTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        { name = "Guide Tip Bulb", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[width * height];
        var strokes = new[] {
            new Vector4(20, 1, 20, 5), new Vector4(5, 8, 8, 11), new Vector4(1, 21, 5, 21),
            new Vector4(35, 8, 32, 11), new Vector4(39, 21, 35, 21),
            new Vector4(14, 43, 26, 43), new Vector4(16, 47, 24, 47) };
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            var point = new Vector2((x + .5f) / 4f, 50f - (y + .5f) / 4f);
            float body = 12.5f - Vector2.Distance(point, new Vector2(20, 21));
            if (point.y > 25 && point.y < 40)
                body = Mathf.Max(body, Mathf.Min(Mathf.Lerp(11, 6, (point.y - 25) / 15f) - Mathf.Abs(point.x - 20), Mathf.Min(point.y - 25, 40 - point.y)));
            foreach (var line in strokes) body = Mathf.Max(body, 1.4f - GuideLineDistance(point, line));
            float filament = Mathf.Min(GuideLineDistance(point, new Vector4(16, 23, 20, 38)),
                GuideLineDistance(point, new Vector4(24, 23, 20, 38)));
            body = Mathf.Min(body, filament - 1.1f);
            pixels[y * width + x] = new Color32(255, 187, 45, (byte)Mathf.RoundToInt(Mathf.Clamp01(body * 4f + .5f) * 255f));
        }
        _tipBulbTexture.SetPixels32(pixels); _tipBulbTexture.Apply(false, true);
        image.texture = _tipBulbTexture;
    }

    static float GuideLineDistance(Vector2 point, Vector4 line)
    {
        var a = new Vector2(line.x, line.y); var direction = new Vector2(line.z - line.x, line.w - line.y);
        return Vector2.Distance(point, a + direction * Mathf.Clamp01(Vector2.Dot(point - a, direction) / direction.sqrMagnitude));
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void KeepForSceneTransition()
    {
        if (!Application.isPlaying || _keptForSceneTransition) return;
        _keptForSceneTransition = true;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
    }

    public void FadeOutAfterSceneLoad()
    {
        if (!isActiveAndEnabled)
        {
            Hide();
            return;
        }
        if (_fadingOut) return;
        _fadingOut = true;
        _fadeOutRoutine = StartCoroutine(FadeOutWhenReady());
    }

    IEnumerator FadeOutWhenReady()
    {
        // 새 씬의 Start와 카메라 갱신을 거친 후 다음 화면을 드러냅니다.
        yield return null;
        yield return null;
        _fadeOutRoutine = null;
        _fadeTween?.Kill();
        _fadeTween = null;
        if (_canvasGroup == null || _fadeOutDuration <= 0f)
        {
            Hide();
            yield break;
        }

        _fadeTween = _canvasGroup.DOFade(0f, _fadeOutDuration)
            .SetEase(Ease.InOutSine).SetUpdate(true)
            .OnComplete(() =>
            {
                _fadeTween = null;
                Hide();
            });
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        if (_keptForSceneTransition) Destroy(gameObject);
    }

    public void UpdateLoadingText(string text)
    {
        if (_loadingText != null) _loadingText.text = text;
    }

    public void UpdateTip(string title, string description, Sprite icon)
    {
        if (_tipTitle != null) _tipTitle.text = title;
        if (_tipDescription != null) _tipDescription.text = description;
        if (_tipIcon == null) return;
        _tipIcon.sprite = icon;
        _tipIcon.enabled = icon != null;
        FitGuideTipIcon(icon);
    }
}
