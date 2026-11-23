using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class DiveDepthFog : MonoBehaviour
{
    [Header("Dive Scene References")]
    [SerializeField] MeshRenderer _waterForeground;
    [SerializeField] MeshRenderer _waterBackdrop;
    [SerializeField] Volume _deepWaterVolume;

    [Header("Water Depth")]
    [SerializeField, Min(0f)] float _fogStartDepth;
    [SerializeField, Min(0f)] float _fullFogDepth;

    [Header("Fog At Full Depth")]
    [SerializeField, Min(0f)] float _nearDistance;
    [SerializeField, Min(0f)] float _farDistance;
    [SerializeField, Range(0f, 1f)] float _fogStrength;

    [Header("Fog Gradient")]
    [SerializeField] Color _upperFogTint;
    [SerializeField] Color _lowerFogTint;
    [SerializeField, Range(0f, 1f)] float _gradientStrength;

    static readonly int SurfaceY = Shader.PropertyToID("_SurfaceY");
    static readonly int GameplayPlaneZ = Shader.PropertyToID("_GameplayPlaneZ");
    static readonly int FogStart = Shader.PropertyToID("_DistanceFogStart");
    static readonly int FogEnd = Shader.PropertyToID("_DistanceFogEnd");
    static readonly int FogStrength = Shader.PropertyToID("_DistanceFogStrength");
    static readonly int ShallowColor = Shader.PropertyToID("_ShallowColor");
    static readonly int MiddleColor = Shader.PropertyToID("_MiddleColor");
    static readonly int DeepColor = Shader.PropertyToID("_DeepColor");
    static readonly int MiddleDepth = Shader.PropertyToID("_MiddleDepth");
    static readonly int DeepDepth = Shader.PropertyToID("_DeepDepth");

    Camera _camera;
    MaterialPropertyBlock _originalForegroundProperties;
    MaterialPropertyBlock _originalBackdropProperties;
    MaterialPropertyBlock _fogProperties;
    MeshRenderer _activeForeground;
    MeshRenderer _activeBackdrop;
    Volume _activeVolume;
    float _originalVolumeWeight;

    void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _originalForegroundProperties = new MaterialPropertyBlock();
        _originalBackdropProperties = new MaterialPropertyBlock();
        _fogProperties = new MaterialPropertyBlock();
        RenderPipelineManager.beginCameraRendering += ApplyFog;
        RenderPipelineManager.endCameraRendering += EndFog;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= ApplyFog;
        RenderPipelineManager.endCameraRendering -= EndFog;
        RestoreFog();
    }

    void ApplyFog(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera || _activeForeground || !_waterForeground || !_waterForeground.enabled
            || !_deepWaterVolume || !_deepWaterVolume.isActiveAndEnabled
            || !camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData)
            || !cameraData.renderPostProcessing)
            return;

        var material = _waterForeground.sharedMaterial;
        if (!material || !material.HasProperty(SurfaceY) || !material.HasProperty(FogStart)
            || !material.HasProperty(FogEnd) || !material.HasProperty(FogStrength))
            return;

        float surface = material.GetFloat(SurfaceY);
        float depth = surface - camera.transform.position.y;
        float fullDepth = Mathf.Max(_fogStartDepth + 0.01f, _fullFogDepth);
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_fogStartDepth, fullDepth, depth));
        if (blend <= 0f)
            return;

        _activeForeground = _waterForeground;
        _activeVolume = _deepWaterVolume;
        _originalVolumeWeight = _activeVolume.weight;
        _activeForeground.GetPropertyBlock(_originalForegroundProperties);
        _activeForeground.GetPropertyBlock(_fogProperties);

        float farDistance = Mathf.Max(_nearDistance + 0.01f, _farDistance);
        _fogProperties.SetFloat(FogStart, Mathf.Lerp(material.GetFloat(FogStart), _nearDistance, blend));
        _fogProperties.SetFloat(FogEnd, Mathf.Lerp(material.GetFloat(FogEnd), farDistance, blend));
        _fogProperties.SetFloat(FogStrength, Mathf.Lerp(material.GetFloat(FogStrength), _fogStrength, blend));

        float gradient = Mathf.Sqrt(blend) * _gradientStrength;
        if (gradient > 0f && material.HasProperty(GameplayPlaneZ))
        {
            float plane = material.GetFloat(GameplayPlaneZ);
            float upper = Mathf.Max(0.01f, DepthAtViewport(camera, 0.86f, surface, plane, depth));
            float lower = Mathf.Max(upper + 0.01f, DepthAtViewport(camera, 0.12f, surface, plane, depth));
            SetGradient(material, upper, lower, gradient);
            _activeForeground.SetPropertyBlock(_fogProperties);
            ApplyBackdropGradient(upper, lower, gradient);
        }
        else
        {
            _activeForeground.SetPropertyBlock(_fogProperties);
        }

        _activeVolume.weight = Mathf.Lerp(_originalVolumeWeight, 1f, blend);
    }

    void ApplyBackdropGradient(float upper, float lower, float amount)
    {
        if (!_waterBackdrop || !_waterBackdrop.enabled || _waterBackdrop == _activeForeground)
            return;

        var material = _waterBackdrop.sharedMaterial;
        if (!material || !material.HasProperty(MiddleDepth) || !material.HasProperty(DeepDepth))
            return;

        _activeBackdrop = _waterBackdrop;
        _activeBackdrop.GetPropertyBlock(_originalBackdropProperties);
        _activeBackdrop.GetPropertyBlock(_fogProperties);
        SetGradient(material, upper, lower, amount);
        _activeBackdrop.SetPropertyBlock(_fogProperties);
    }

    void SetGradient(Material material, float upper, float lower, float amount)
    {
        if (!material.HasProperty(ShallowColor) || !material.HasProperty(MiddleColor)
            || !material.HasProperty(DeepColor) || !material.HasProperty(MiddleDepth)
            || !material.HasProperty(DeepDepth))
            return;

        float originalMiddle = material.GetFloat(MiddleDepth);
        float originalDeep = material.GetFloat(DeepDepth);
        Color shallowColor = material.GetColor(ShallowColor);
        Color middleColor = material.GetColor(MiddleColor);
        Color deepColor = material.GetColor(DeepColor);
        Color upperColor = SampleWaterColor(shallowColor, middleColor, deepColor, originalMiddle, originalDeep, upper);
        Color lowerColor = SampleWaterColor(shallowColor, middleColor, deepColor, originalMiddle, originalDeep, lower);
        float bandBlend = Mathf.SmoothStep(0f, 1f, amount);
        _fogProperties.SetFloat(MiddleDepth, Mathf.Lerp(originalMiddle, upper, bandBlend));
        _fogProperties.SetFloat(DeepDepth, Mathf.Lerp(originalDeep, lower, bandBlend));
        _fogProperties.SetColor(MiddleColor, Color.Lerp(middleColor, upperColor * _upperFogTint, amount));
        _fogProperties.SetColor(DeepColor, Color.Lerp(deepColor, lowerColor * _lowerFogTint, amount));
    }

    static float DepthAtViewport(Camera camera, float y, float surface, float plane, float fallback)
    {
        var ray = camera.ViewportPointToRay(new Vector3(0.5f, y, 0f));
        if (Mathf.Abs(ray.direction.z) < 0.001f)
            return fallback;

        float distance = (plane - ray.origin.z) / ray.direction.z;
        return distance > 0f ? surface - ray.GetPoint(distance).y : fallback;
    }

    static Color SampleWaterColor(Color shallow, Color middle, Color deep, float middleDepth, float deepDepth, float depth)
    {
        float middleBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Mathf.Max(middleDepth, 0.01f), depth));
        float deepBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(middleDepth, Mathf.Max(deepDepth, middleDepth + 0.01f), depth));
        return Color.Lerp(Color.Lerp(shallow, middle, middleBlend), deep, deepBlend);
    }

    void EndFog(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _camera)
            RestoreFog();
    }

    void RestoreFog()
    {
        if (_activeForeground)
            _activeForeground.SetPropertyBlock(_originalForegroundProperties.isEmpty ? null : _originalForegroundProperties);
        if (_activeBackdrop)
            _activeBackdrop.SetPropertyBlock(_originalBackdropProperties.isEmpty ? null : _originalBackdropProperties);
        if (_activeVolume)
            _activeVolume.weight = _originalVolumeWeight;

        _activeForeground = null;
        _activeBackdrop = null;
        _activeVolume = null;
    }
}
