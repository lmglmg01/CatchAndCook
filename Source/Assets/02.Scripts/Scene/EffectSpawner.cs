using UnityEngine;

public class EffectSpawner : MonoBehaviour
{
    [Header("히트 이펙트")]
    [SerializeField] GameObject _hitEffectPrefab;

    [Header("자동 제거")]
    [Tooltip("0이면 자동으로 제거하지 않습니다. 프리팹의 자체 종료 설정을 사용하세요.")]
    [SerializeField, Min(0f)] float _hitEffectLifeTime;

    public void SpawnHitEffect(Vector3 worldPosition)
    {
        if (_hitEffectPrefab == null)
        {
            Debug.LogWarning("EffectSpawner에 히트 이펙트 프리팹을 연결하세요.", this);
            return;
        }

        GameObject effect = Instantiate(_hitEffectPrefab, worldPosition, _hitEffectPrefab.transform.rotation);
        foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
        }

        if (_hitEffectLifeTime > 0f)
            Destroy(effect, _hitEffectLifeTime);
    }
}
