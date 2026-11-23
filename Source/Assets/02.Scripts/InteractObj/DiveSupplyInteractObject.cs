using System.Collections;
using UnityEngine;

public abstract class DiveSupplyInteractObject : InteractObject
{
    [Header("보급품 소멸")]
    [SerializeField] Renderer _supplyRenderer;
    [SerializeField] Shader _dissolveShader;
    [SerializeField, Min(0.01f)] float _fadeSeconds;

    static readonly int Dissolve = Shader.PropertyToID("_Dissolve");
    static readonly int ParticleColor = Shader.PropertyToID("_Color");

    Material _dissolvingMaterial;

    protected override void FinishUse(GameObject effect)
    {
        if (_supplyRenderer == null || _dissolveShader == null || _fadeSeconds <= 0f)
        {
            base.FinishUse(effect);
            Destroy(gameObject);
            return;
        }

        _dissolvingMaterial = new Material(_supplyRenderer.sharedMaterial);
        _dissolvingMaterial.shader = _dissolveShader;
        _dissolvingMaterial.renderQueue = -1;
        _supplyRenderer.sharedMaterial = _dissolvingMaterial;
        StartCoroutine(FadeOut(effect));
    }

    IEnumerator FadeOut(GameObject effect)
    {
        var particles = effect != null
            ? effect.GetComponentsInChildren<ParticleSystemRenderer>(true)
            : System.Array.Empty<ParticleSystemRenderer>();
        var blocks = new MaterialPropertyBlock[particles.Length];
        var colors = new Color[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            var renderer = particles[i];
            renderer.GetComponent<ParticleSystem>().Stop(false, ParticleSystemStopBehavior.StopEmitting);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            blocks[i] = block;
            colors[i] = block.HasColor(ParticleColor) ? block.GetColor(ParticleColor)
                : renderer.sharedMaterial.GetColor(ParticleColor);
        }

        float elapsed = 0f;
        while (true)
        {
            float progress = Mathf.Clamp01(elapsed / _fadeSeconds);
            _dissolvingMaterial.SetFloat(Dissolve, progress);
            for (int i = 0; i < particles.Length; i++)
            {
                Color color = colors[i];
                color.a *= 1f - progress;
                blocks[i].SetColor(ParticleColor, color);
                particles[i].SetPropertyBlock(blocks[i]);
            }

            yield return null;
            if (progress >= 1f) break;
            elapsed += Time.deltaTime;
        }

        Destroy(gameObject);
    }

    void OnEnable()
    {
        if (IsInteracted) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (_dissolvingMaterial != null) Destroy(_dissolvingMaterial);
    }
}
