using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>타이틀 수중 배경의 잠수부 추격과 물고기 무리를 움직입니다.</summary>
[DisallowMultipleComponent]
public sealed class TitleChaseSequence : MonoBehaviour
{
    [Header("타이틀 카메라")]
    [SerializeField] Camera _titleCamera;

    [Header("잠수부와 흰동가리")]
    [SerializeField] Transform _diver;
    [SerializeField] Animator _diverAnimator;
    [SerializeField] AnimationClip _diverSwim;
    [SerializeField] ParticleSystem _bubbles;
    [SerializeField] Transform _fish;
    [SerializeField] Transform _fishSpine;
    [SerializeField] Transform _fishTail;

    [Header("추격 경로")]
    [Tooltip("뷰포트 좌표에서 왕복 경로의 가로 중심입니다.")]
    [SerializeField, Range(0f, 1f)] float _chaseCenterX;
    [Tooltip("뷰포트 중심에서 회전 지점까지의 거리입니다. 중심이 0.5일 때 이 값을 0.5보다 크게 설정하면 화면 밖에서 회전합니다.")]
    [SerializeField, Min(0.01f)] float _chaseHalfWidth;
    [SerializeField, Min(1f)] float _chasePeriod;
    [SerializeField, Min(0f)] float _followDelay;

    [Header("배경 물고기 무리")]
    [SerializeField] SwimSchool[] _schools;

    const float ReferenceAspect = 16f / 9f;
    const float ChaseStartAngle = -0.29f;
    const float TurnWindow = 0.23f;
    const float HeadingSample = 0.01f;

    Vector3 _diverScale;
    Vector3 _fishScale;
    Quaternion _diverModelRotation;
    Quaternion _fishModelRotation;
    Vector3 _diverLane;
    Vector3 _fishLane;
    Quaternion _spineRest;
    Quaternion _tailRest;
    Vector3 _spineUp;
    Vector3 _tailUp;
    PlayableGraph _swimGraph;
    AnimationClipPlayable _swimMotion;
    TitleHeroRig _heroRig;
    AnimationScriptPlayable _headTurn;
    float _elapsed;

    void Awake()
    {
        if (!_titleCamera || !_diver || !_diverAnimator || !_diverSwim || !_fish || !_fishSpine || !_fishTail)
        {
            Debug.LogError("타이틀 수중 배경의 카메라 또는 모델 참조가 누락되었습니다.", this);
            enabled = false;
            return;
        }

        _diverScale = _diver.localScale;
        _fishScale = _fish.localScale;
        _diverLane = _titleCamera.WorldToViewportPoint(_diver.position);
        _fishLane = _titleCamera.WorldToViewportPoint(_fish.position);
        _diverModelRotation = GetModelRotation(_diver, _diverAnimator.transform.forward);
        _fishModelRotation = GetModelRotation(_fish, _fishSpine.position - _fishTail.position);
        _spineRest = _fishSpine.localRotation;
        _tailRest = _fishTail.localRotation;
        _spineUp = _fishSpine.InverseTransformDirection(_fish.up);
        _tailUp = _fishTail.InverseTransformDirection(_fish.up);
        foreach (var school in _schools ?? System.Array.Empty<SwimSchool>())
            school.Initialize(_titleCamera);

        _swimGraph = PlayableGraph.Create("Title Diver Swim");
        _swimGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        _swimMotion = AnimationClipPlayable.Create(_swimGraph, _diverSwim);
        _swimMotion.SetApplyFootIK(false);

        Playable swimPose = _swimMotion;
        _heroRig = _diverAnimator.GetComponent<TitleHeroRig>();
        if (_heroRig) _headTurn = _heroRig.CreateLookPlayable(_swimGraph, _diverAnimator, _titleCamera.transform);
        if (_headTurn.IsValid())
        {
            _swimGraph.Connect(_swimMotion, 0, _headTurn, 0);
            _headTurn.SetInputWeight(0, 1f);
            swimPose = _headTurn;
        }

        var output = AnimationPlayableOutput.Create(_swimGraph, "Diver", _diverAnimator);
        output.SetSourcePlayable(swimPose);
    }

    void OnEnable() => Replay();

    [ContextMenu("타이틀 배경 연출 다시 재생 (플레이 중)")]
    public void Replay()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !_swimGraph.IsValid()) return;

        _elapsed = 0f;
        _swimMotion.SetTime(0);
        _swimGraph.Play();
        ApplyPose();
        if (_bubbles)
        {
            _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _bubbles.Play(true);
        }
    }

    void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        ApplyPose();
    }

    void OnDisable()
    {
        if (_bubbles) _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_swimGraph.IsValid()) _swimGraph.Stop();
    }

    void OnDestroy()
    {
        if (_swimGraph.IsValid()) _swimGraph.Destroy();
    }

    void ApplyPose()
    {
        float widthScale = Mathf.Min(1f, _titleCamera.aspect / ReferenceAspect);
        _diver.localScale = _diverScale * widthScale;
        _fish.localScale = _fishScale * widthScale;

        SwimChase(_fish, _fishLane, _fishModelRotation, _elapsed, 3f);
        SwimChase(_diver, _diverLane, _diverModelRotation, _elapsed - _followDelay, 5f);
        _swimMotion.SetSpeed(1.05f + 0.18f * Mathf.Cos(_elapsed * 1.65f));
        if (_heroRig && _headTurn.IsValid())
        {
            float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.23f, 0.57f, _elapsed));
            _heroRig.ApplyLook(_headTurn, lift);
        }

        float tailPhase = _elapsed * 13f;
        _fishSpine.localRotation = _spineRest * Quaternion.AngleAxis(Mathf.Sin(tailPhase) * 8f, _spineUp);
        _fishTail.localRotation = _tailRest * Quaternion.AngleAxis(Mathf.Sin(tailPhase - 0.8f) * 23f, _tailUp);

        foreach (var school in _schools ?? System.Array.Empty<SwimSchool>())
            school.Swim(_titleCamera, _elapsed, widthScale);
    }

    void SwimChase(Transform swimmer, Vector3 lane, Quaternion modelRotation, float time, float bankAngle)
    {
        float phase = ChaseStartAngle + time * (2f * Mathf.PI / Mathf.Max(1f, _chasePeriod));
        float across = Mathf.Sin(phase);
        lane.x = _chaseCenterX + _chaseHalfWidth * across;

        // 양끝에서 감속하는 동안 얼굴이 카메라 쪽을 거쳐 반대편으로 돌아갑니다.
        float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-TurnWindow, TurnWindow, Mathf.Cos(phase)));
        var facing = Quaternion.Euler(0f, Mathf.Lerp(252f, 108f, turn), 0f);
        var bank = Quaternion.AngleAxis(across * bankAngle, Vector3.forward);
        var position = _titleCamera.ViewportToWorldPoint(lane);
        swimmer.SetPositionAndRotation(position, _titleCamera.transform.rotation * facing * bank * modelRotation);
    }

    static Quaternion GetModelRotation(Transform swimmer, Vector3 forward)
    {
        var localForward = Vector3.ProjectOnPlane(swimmer.InverseTransformDirection(forward), Vector3.up);
        return Quaternion.Inverse(Quaternion.LookRotation(localForward, Vector3.up));
    }

    [System.Serializable]
    sealed class SwimSchool
    {
        [Header("무리와 수영 경로")]
        [SerializeField] Transform _formation;
        [SerializeField, Min(1f)] float _crossingDuration;
        [SerializeField, Range(0f, 1f)] float _startPhase;
        [SerializeField] bool _swimLeft;
        [SerializeField, Min(0f)] float _waveHeight;
        [SerializeField, Range(0f, 1f)] float _brightness;

        const float OffscreenMargin = 0.22f;

        Vector3 _lane;
        Vector3 _scale;

        public void Initialize(Camera camera)
        {
            if (!_formation) return;

            _lane = camera.WorldToViewportPoint(_formation.position);
            _scale = _formation.localScale;
            foreach (var fish in _formation.GetComponentsInChildren<BackgroundFish>())
                fish.Initialize(_brightness);
            foreach (var animator in _formation.GetComponentsInChildren<Animator>())
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        public void Swim(Camera camera, float time, float widthScale)
        {
            if (!_formation) return;

            float progress = Mathf.Repeat(_startPhase + time / Mathf.Max(1f, _crossingDuration), 1f);
            var position = Position(camera, progress);
            var heading = Position(camera, progress + HeadingSample) - position;
            _formation.localScale = _scale * widthScale;
            _formation.SetPositionAndRotation(position, Quaternion.LookRotation(heading, camera.transform.up));
        }

        Vector3 Position(Camera camera, float progress)
        {
            float x = Mathf.LerpUnclamped(-OffscreenMargin, 1f + OffscreenMargin, _swimLeft ? 1f - progress : progress);
            float y = _lane.y + Mathf.Sin(progress * 2f * Mathf.PI) * _waveHeight;
            return camera.ViewportToWorldPoint(new Vector3(x, y, _lane.z));
        }
    }
}
