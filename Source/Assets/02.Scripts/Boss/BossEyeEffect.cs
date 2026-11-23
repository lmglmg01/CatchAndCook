using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BossEyeEffect : MonoBehaviour
{
    [Header("눈 오브젝트")]
    [SerializeField] Renderer[] _eyeRenderers;
    [Header("눈 발광")]
    [SerializeField] Color _eyeColor;
    [SerializeField, Min(0)] float _idleEmission;
    [SerializeField, Min(0)] float _telegraphEmission;
    [Header("발광 번짐")]
    [SerializeField] bool _enableBloom;
    [SerializeField, Min(0)] float _bloomIntensity;
    [SerializeField, Min(0)] float _bloomThreshold;
    [SerializeField, Range(0, 1)] float _bloomScatter;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    Material[] _originalMaterials;
    Material[] _runtimeMaterials;
    Volume _volume;
    VolumeProfile _profile;
    bool _dead;
    bool _visible = true;

    void Awake()
    {
        int count = _eyeRenderers == null ? 0 : _eyeRenderers.Length;
        _originalMaterials = new Material[count];
        _runtimeMaterials = new Material[count];
        for (int i = 0; i < count; i++)
        {
            var eye = _eyeRenderers[i];
            if (eye == null || eye.sharedMaterial == null) continue;
            _originalMaterials[i] = eye.sharedMaterial;
            var material = new Material(eye.sharedMaterial);
            material.name = eye.name + "_Emission_Runtime";
            material.hideFlags = HideFlags.DontSave;
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, _eyeColor);
            _runtimeMaterials[i] = material;
        }
        if (_enableBloom)
        {
            var go = new GameObject("BossEyeBloom_Runtime");
            go.transform.SetParent(transform, false);
            go.layer = 0;
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 20;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.hideFlags = HideFlags.DontSave;
            var bloom = _profile.Add<Bloom>();
            bloom.intensity.Override(_bloomIntensity);
            bloom.threshold.Override(_bloomThreshold);
            bloom.scatter.Override(_bloomScatter);
            _volume.sharedProfile = _profile;
        }
    }

    void OnEnable()
    {
        _dead = false;
        if (_runtimeMaterials != null)
            for (int i = 0; i < _runtimeMaterials.Length; i++)
                if (_eyeRenderers[i] != null && _runtimeMaterials[i] != null)
                    _eyeRenderers[i].sharedMaterial = _runtimeMaterials[i];
        if (_volume != null) _volume.enabled = _visible;
        SetTelegraph(0);
    }

    internal void SetVisible(bool visible)
    {
        _visible = visible;
        if (_eyeRenderers != null)
            foreach (var eye in _eyeRenderers)
                if (eye != null) eye.forceRenderingOff = !visible;
        if (_volume != null) _volume.enabled = visible && isActiveAndEnabled;
    }

    public void SetTelegraph(float amount)
    {
        float strength = Mathf.Clamp01(amount);
        Color emission = _dead ? Color.black : _eyeColor * Mathf.Lerp(_idleEmission, _telegraphEmission, strength);
        if (_runtimeMaterials != null)
            foreach (var material in _runtimeMaterials)
                if (material != null && material.HasProperty(EmissionColorId))
                    material.SetColor(EmissionColorId, emission);
    }

    public void SetDead()
    {
        _dead = true;
        SetTelegraph(0);
    }

    void OnDisable()
    {
        if (_originalMaterials != null)
            for (int i = 0; i < _originalMaterials.Length; i++)
                if (_eyeRenderers[i] != null) _eyeRenderers[i].sharedMaterial = _originalMaterials[i];
        if (_volume != null) _volume.enabled = false;
    }

    void OnDestroy()
    {
        if (_runtimeMaterials != null)
            foreach (var material in _runtimeMaterials) if (material != null) Destroy(material);
        if (_profile != null)
        {
            foreach (var component in _profile.components) if (component != null) Destroy(component);
            Destroy(_profile);
        }
    }
}
