using UnityEngine;

// 애니메이션 자세에 몸통별 회전 지연을 더한다. 물리 관절은 사용하지 않는다.
[DefaultExecutionOrder(100)]
public class BossBodyFollow : MonoBehaviour
{
    [Header("몸통 연결")]
    [SerializeField] Animator _animator;
    [SerializeField] Transform _visualRoot;
    [SerializeField] Transform[] _bodyBones;
    [Header("몸통 휘어짐")]
    [SerializeField, Min(0f)] float _tailDelay;
    [SerializeField, Range(0f, 80f)] float _maxBendAngle;
    [SerializeField, Min(0f)] float _blendSpeed;

    const int HistorySize = 256;
    readonly Quaternion[] _rotations = new Quaternion[HistorySize];
    readonly float[] _times = new float[HistorySize];
    Quaternion[] _animatedWorld;
    Quaternion[] _animatedLocal;
    int _newest;
    int _count;
    float _weight;
    bool _applied;

    void Awake()
    {
        int count = _bodyBones == null ? 0 : _bodyBones.Length;
        _animatedWorld = new Quaternion[count];
        _animatedLocal = new Quaternion[count];
    }

    void Update() { RestorePose(); }

    void RestorePose()
    {
        if (!_applied) return;
        for (int i = 0; i < _bodyBones.Length; i++)
            if (_bodyBones[i] != null) _bodyBones[i].localRotation = _animatedLocal[i];
        _applied = false;
    }

    void LateUpdate()
    {
        if (_visualRoot == null || _animator == null || _bodyBones == null || _bodyBones.Length == 0) return;
        Quaternion current = _visualRoot.rotation;
        if (_count == 0 || Time.time - _times[_newest] >= .02f)
        {
            _newest = (_newest + 1) % HistorySize;
            _rotations[_newest] = current;
            _times[_newest] = Time.time;
            _count = Mathf.Min(_count + 1, HistorySize);
        }
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        bool swimming = state.IsName("Idle") || state.IsName("Swim");
        if (_animator.IsInTransition(0))
        {
            var next = _animator.GetNextAnimatorStateInfo(0);
            swimming &= next.IsName("Idle") || next.IsName("Swim");
        }
        _weight = Mathf.MoveTowards(_weight, swimming ? 1f : 0f, _blendSpeed * Time.deltaTime);
        for (int i = 0; i < _bodyBones.Length; i++)
        {
            if (_bodyBones[i] == null) continue;
            _animatedWorld[i] = _bodyBones[i].rotation;
            _animatedLocal[i] = _bodyBones[i].localRotation;
        }
        // 모든 원본 자세를 먼저 저장해 부모 보정이 자식에 중복 적용되지 않게 한다.
        for (int i = 0; i < _bodyBones.Length; i++)
        {
            if (_bodyBones[i] == null) continue;
            float alongBody = (float)i / Mathf.Max(1, _bodyBones.Length - 1);
            Quaternion delayed = ReadRotation(Time.time - Mathf.Min(_tailDelay, 4f) * alongBody);
            Quaternion bend = delayed * Quaternion.Inverse(current);
            bend = Quaternion.RotateTowards(Quaternion.identity, bend, _maxBendAngle);
            _bodyBones[i].rotation = Quaternion.Slerp(Quaternion.identity, bend, _weight) * _animatedWorld[i];
        }
        _applied = true;
    }

    Quaternion ReadRotation(float time)
    {
        int newer = _newest;
        for (int i = 1; i < _count; i++)
        {
            int older = (_newest - i + HistorySize) % HistorySize;
            if (_times[older] <= time)
                return Quaternion.Slerp(_rotations[older], _rotations[newer], Mathf.InverseLerp(_times[older], _times[newer], time));
            newer = older;
        }
        return _rotations[newer];
    }

    void OnDisable()
    {
        RestorePose();
        _count = 0;
        _weight = 0f;
    }
}
