using UnityEngine;

[DisallowMultipleComponent]
public class Boat : MonoBehaviour
{
    [Header("흔들림 강도")]
    [Tooltip("0은 정지, 1은 기본 강도, 2는 두 배 강도입니다.")]
    [SerializeField, Range(0f, 3f), InspectorName("흔들림 강도")] float _strength;

    [Header("부유 중심")]
    [Tooltip("배 루트 기준 수면 위 선체 중심입니다.")]
    [SerializeField] Vector3 _localPivot;

    [Header("상하 움직임")]
    [SerializeField, Min(0f)] float _heightAmplitude;
    [SerializeField, Min(0.1f)] float _heightPeriod;

    [Header("기울기")]
    [Tooltip("로컬 X축과 Z축의 최대 기울기(도)입니다.")]
    [SerializeField] Vector2 _tiltAngles;
    [Tooltip("로컬 X축과 Z축이 흔들리는 주기(초)입니다.")]
    [SerializeField] Vector2 _tiltPeriods;

    [Header("자연스러운 변화")]
    [SerializeField, Range(0f, 0.5f)] float _secondaryWaveWeight;
    [SerializeField, Min(0f)] float _blendInDuration;

    Vector3 _restLocalPosition;
    Quaternion _restLocalRotation;
    Vector3 _scaledPivot;
    float _elapsed;
    bool _hasRestPose;

    void OnEnable()
    {
        _restLocalPosition = transform.localPosition;
        _restLocalRotation = transform.localRotation;
        _scaledPivot = Vector3.Scale(transform.localScale, _localPivot);
        _elapsed = 0f;
        _hasRestPose = true;
    }

    void LateUpdate()
    {
        _elapsed += Time.deltaTime;
        float blend = _blendInDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, _elapsed / _blendInDuration)
            : 1f;

        float intensity = blend * Mathf.Max(0f, _strength);
        float height = SampleWave(_heightPeriod, 0f) * _heightAmplitude * intensity;
        float tiltX = SampleWave(_tiltPeriods.x, 0.8f) * _tiltAngles.x * intensity;
        float tiltZ = SampleWave(_tiltPeriods.y, 2.1f) * _tiltAngles.y * intensity;
        Quaternion rotation = _restLocalRotation * Quaternion.Euler(tiltX, 0f, tiltZ);

        // 원래 배치와 수면 위 회전 중심을 기준으로 계산해 누적 이동을 방지합니다.
        Vector3 restPivot = _restLocalRotation * _scaledPivot;
        Vector3 position = _restLocalPosition + restPivot - rotation * _scaledPivot;
        position += Vector3.up * height;
        transform.SetLocalPositionAndRotation(position, rotation);
    }

    float SampleWave(float period, float phase)
    {
        float angle = _elapsed * (Mathf.PI * 2f / Mathf.Max(0.1f, period));
        float weight = Mathf.Clamp(_secondaryWaveWeight, 0f, 0.5f);
        float primary = Mathf.Sin(angle + phase);
        float secondary = Mathf.Sin(angle * 1.67f + phase + 1.3f);
        return (primary + secondary * weight) / (1f + weight);
    }

    void OnDisable()
    {
        if (!_hasRestPose) return;
        transform.SetLocalPositionAndRotation(_restLocalPosition, _restLocalRotation);
        _hasRestPose = false;
    }
}
