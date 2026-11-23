using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CameraController : MonoBehaviour
{
    [Header("조준·작살 줌")]
    [SerializeField] CinemachineCamera _cinemachineCamera;
    [SerializeField, Min(0f)] float _aimZoomAmount;
    [SerializeField, Min(0f)] float _harpoonZoomAmount;
    [SerializeField, Min(0.01f)] float _aimZoomTime;
    [SerializeField, Min(0.01f)] float _releaseZoomTime;
    [SerializeField, Min(0.01f)] float _harpoonZoomTime;

    [Header("작살 발사 흔들림")]
    [SerializeField, Min(0f)] float _fireShakeStrength;
    [SerializeField, Min(0f)] float _fireShakeDuration;

    [Header("총 발사 흔들림")]
    [SerializeField, Min(0f)] float _gunFireShakeStrength;
    [SerializeField, Min(0f)] float _gunFireShakeDuration;

    [Header("작살 명중 흔들림")]
    [SerializeField, Min(0f)] float _impactShakeStrength;
    [SerializeField, Min(0f)] float _impactShakeDuration;

    [Header("물고기 저항 흔들림")]
    [SerializeField, Min(0f)] float _resistanceShakeStrength;
    [SerializeField, Min(0f)] float _resistanceShakeDuration;
    [SerializeField, Min(0.01f)] float _resistanceShakeInterval;

    [Header("카메라 흔들림")]
    [SerializeField] CinemachineImpulseSource _impulseSource;
    [SerializeField, Min(0f)] float _shakeStrength;
    [SerializeField, Min(0f)] float _shakeDuration;

    [Header("피격 비네트")]
    [Tooltip("피격 효과 전용 Volume을 연결합니다.")]
    [SerializeField] Volume _hitVolume;
    [SerializeField] Color _vignetteColor;
    [SerializeField, Range(0f, 1f)] float _vignetteIntensity;
    [SerializeField, Range(0.01f, 1f)] float _vignetteSmoothness;
    [SerializeField, Min(0f)] float _vignetteDuration;

    VolumeProfile _runtimeProfile;
    VolumeProfile _previousProfile;
    Vignette _vignette;
    Coroutine _vignetteRoutine;
    bool _zoomInitialized;
    bool _aiming;
    bool _harpoonFlying;
    bool _resisting;
    bool _harpoonHit;
    float _baseFieldOfView;
    float _zoomVelocity;
    float _nextResistanceShakeTime;
    CinemachineConfiner2D _cameraConfiner;
    float _confinerFieldOfView;
    bool _battleFraming;
    float _battleFieldOfView, _battleMinimumFieldOfView;

    // Boss scenes can reserve enough space for the attack without competing with aim zoom.
    public void SetBattleFraming(float fieldOfView, float minimumFieldOfView)
    {
        _battleFraming = true;
        _battleFieldOfView = fieldOfView;
        _battleMinimumFieldOfView = minimumFieldOfView;
    }

    public void ClearBattleFraming() => _battleFraming = false;

    void Awake()
    {
        InitializeVignette();
    }

    void OnEnable() => InitializeZoom();

    void Update()
    {
        UpdateZoom();
        if ((_resisting || (_harpoonFlying && _harpoonHit)) && Time.time >= _nextResistanceShakeTime)
        {
            ShakeCamera(_resistanceShakeStrength, _resistanceShakeDuration);
            _nextResistanceShakeTime = Time.time + Mathf.Max(0.01f, _resistanceShakeInterval);
        }
    }

    void OnDisable()
    {
        _battleFraming = false;
        _aiming = false;
        _harpoonFlying = false;
        _harpoonHit = false;
        _resisting = false;
        if (_zoomInitialized && _cinemachineCamera != null)
        {
            var lens = _cinemachineCamera.Lens;
            lens.FieldOfView = _baseFieldOfView;
            _cinemachineCamera.Lens = lens;
            if (_cameraConfiner != null) _cameraConfiner.InvalidateLensCache();
        }
        _zoomInitialized = false;
        _zoomVelocity = 0f;
        if (_vignetteRoutine != null)
        {
            StopCoroutine(_vignetteRoutine);
            _vignetteRoutine = null;
        }

        if (_vignette != null) _vignette.intensity.Override(0f);
    }

    void OnDestroy()
    {
        if (_hitVolume != null && _hitVolume.HasInstantiatedProfile()
            && _hitVolume.profile == _runtimeProfile)
            _hitVolume.profile = _previousProfile;

        if (_vignette != null) Destroy(_vignette);
        if (_runtimeProfile != null) Destroy(_runtimeProfile);
    }

    public void SetAiming(bool aiming) => _aiming = aiming;

    public void SetHarpoonFlying(bool flying)
    {
        _harpoonFlying = flying;
        if (!flying)
        {
            _resisting = false;
            _harpoonHit = false;
        }
    }

    public void OnHarpoonFired()
    {
        _harpoonHit = false;
        _harpoonFlying = true;
        ShakeCamera(_fireShakeStrength, _fireShakeDuration);
    }

    public void OnGunFired()
    {
        ShakeCamera(_gunFireShakeStrength, _gunFireShakeDuration);
    }

    public void OnHarpoonHit()
    {
        // 명중 후 저항이 끝나도 작살이 회수될 때까지 약한 흔들림을 이어간다.
        _harpoonHit = true;
        _nextResistanceShakeTime = Time.time + _impactShakeDuration;
        ShakeCamera(_impactShakeStrength, _impactShakeDuration);
    }

    public void SetHarpoonResistance(bool resisting)
    {
        _resisting = resisting;
        _nextResistanceShakeTime = Time.time;
    }

    bool InitializeZoom()
    {
        if (_cinemachineCamera == null || _cinemachineCamera.Lens.Orthographic) return false;
        if (!_zoomInitialized)
        {
            _baseFieldOfView = _cinemachineCamera.Lens.FieldOfView;
            _cameraConfiner = _cinemachineCamera.GetComponent<CinemachineConfiner2D>();
            _confinerFieldOfView = _baseFieldOfView;
            _zoomInitialized = true;
        }
        return true;
    }

    void UpdateZoom()
    {
        if (!InitializeZoom()) return;
        // 발사 후 AimChanged(false)가 와도 작살 회수 전까지 발사 줌을 유지한다.
        float amount = _harpoonFlying ? _harpoonZoomAmount : _aiming ? _aimZoomAmount : 0f;
        float duration = _harpoonFlying ? _harpoonZoomTime : _aiming ? _aimZoomTime : _releaseZoomTime;
        var lens = _cinemachineCamera.Lens;
        float targetFieldOfView = _battleFraming
            ? Mathf.Max(_battleMinimumFieldOfView, _battleFieldOfView - amount)
            : _baseFieldOfView - amount;
        if (_battleFraming) duration = targetFieldOfView > lens.FieldOfView ? .28f : .65f;
        lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView,
            Mathf.Clamp(targetFieldOfView, 1f, 179f), ref _zoomVelocity,
            Mathf.Max(0.01f, duration), Mathf.Infinity, Time.unscaledDeltaTime);
        _cinemachineCamera.Lens = lens;
        if (_cameraConfiner != null && Mathf.Abs(lens.FieldOfView - _confinerFieldOfView) > 0.01f)
        {
            _cameraConfiner.InvalidateLensCache();
            _confinerFieldOfView = lens.FieldOfView;
        }
    }

    public void Hit()
    {
        ShakeCamera();
        ShowHitVignette();
    }

    public void ShakeCamera()
    {
        ShakeCamera(_shakeStrength, _shakeDuration);
    }

    public void ShakeCamera(float strength, float duration)
    {
        if (!isActiveAndEnabled || _impulseSource == null
            || _impulseSource.ImpulseDefinition == null || strength <= 0f || duration <= 0f) return;

        _impulseSource.ImpulseDefinition.ImpulseDuration = duration;
        _impulseSource.GenerateImpulseWithForce(strength);
    }

    public void ShowHitVignette()
    {
        if (!isActiveAndEnabled || _vignetteIntensity <= 0f
            || _vignetteDuration <= 0f || !InitializeVignette()) return;

        if (_vignetteRoutine != null) StopCoroutine(_vignetteRoutine);

        _vignette.color.Override(_vignetteColor);
        _vignette.smoothness.Override(_vignetteSmoothness);
        _vignetteRoutine = StartCoroutine(FadeVignette());
    }

    bool InitializeVignette()
    {
        if (_vignette != null) return true;
        if (_hitVolume == null) return false;

        _previousProfile = _hitVolume.HasInstantiatedProfile() ? _hitVolume.profile : null;
        _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        _vignette = _runtimeProfile.Add<Vignette>();
        _vignette.intensity.Override(0f);
        _hitVolume.profile = _runtimeProfile;
        return true;
    }

    IEnumerator FadeVignette()
    {
        float elapsed = 0f;
        float duration = _vignetteDuration;
        float intensity = _vignetteIntensity;

        while (elapsed < duration)
        {
            _vignette.intensity.Override(Mathf.Lerp(intensity, 0f, elapsed / duration));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        _vignette.intensity.Override(0f);
        _vignetteRoutine = null;
    }
}
