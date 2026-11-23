using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>타이틀 잠수부의 고개를 Inspector에서 설정한 각도로 조정합니다.</summary>
[DisallowMultipleComponent]
public sealed class TitleHeroRig : MonoBehaviour
{
    [Header("머리 리그")]
    [SerializeField] Transform _neck;
    [SerializeField] Transform _head;
    [SerializeField] Transform _lookFrame;

    [Header("고개 들기")]
    [Tooltip("X는 위아래(음수는 위), Y는 몸통 방향 기준 좌우, Z는 고개 기울기입니다.")]
    [SerializeField] Vector3 _raisedLookEuler;
    [SerializeField, Range(0f, 0.5f)] float _neckFollow;

    public AnimationScriptPlayable CreateLookPlayable(PlayableGraph graph, Animator animator, Transform view)
    {
        if (!_neck || !_head || !_lookFrame) return default;

        var hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

        return AnimationScriptPlayable.Create(graph, new LookJob
        {
            Hips = hips ? animator.BindStreamTransform(hips) : default,
            Neck = animator.BindStreamTransform(_neck),
            Head = animator.BindStreamTransform(_head),
            View = animator.BindSceneTransform(view),
            LookEuler = _raisedLookEuler,
            LookFrameInverse = Quaternion.Inverse(_lookFrame.localRotation),
            NeckFollow = _neckFollow
        }, 1);
    }

    public void ApplyLook(AnimationScriptPlayable look, float lift)
    {
        var job = look.GetJobData<LookJob>();
        if (Mathf.Approximately(job.Lift, lift) &&
            job.LookEuler == _raisedLookEuler &&
            Mathf.Approximately(job.NeckFollow, _neckFollow)) return;

        job.Lift = lift;
        job.LookEuler = _raisedLookEuler;
        job.NeckFollow = _neckFollow;
        look.SetJobData(job);
    }

    struct LookJob : IAnimationJob
    {
        public TransformStreamHandle Hips;
        public TransformStreamHandle Neck;
        public TransformStreamHandle Head;
        public TransformSceneHandle View;
        public Vector3 LookEuler;
        public Quaternion LookFrameInverse;
        public float NeckFollow;
        public float Lift;

        public void ProcessAnimation(AnimationStream stream)
        {
            if (Lift <= 0f || !Head.IsValid(stream) || !View.IsValid(stream)) return;

            var swimHead = Head.GetRotation(stream);
            var viewRotation = View.GetRotation(stream);
            var raisedHead = viewRotation * GetRaisedHead(stream, viewRotation);
            var headRotation = Quaternion.Slerp(swimHead, raisedHead, Lift);
            if (Neck.IsValid(stream))
            {
                var turn = headRotation * Quaternion.Inverse(swimHead);
                var neckTurn = Quaternion.Slerp(Quaternion.identity, turn, NeckFollow);
                Neck.SetRotation(stream, neckTurn * Neck.GetRotation(stream));
            }
            Head.SetRotation(stream, headRotation);
        }

        Quaternion GetRaisedHead(AnimationStream stream, Quaternion viewRotation)
        {
            var lookEuler = LookEuler;
            if (Hips.IsValid(stream) && Neck.IsValid(stream))
            {
                var torso = Quaternion.Inverse(viewRotation) * (Neck.GetPosition(stream) - Hips.GetPosition(stream));
                if (torso.x * torso.x + torso.z * torso.z > 1e-8f)
                    lookEuler.y += Mathf.Atan2(torso.x, torso.z) * Mathf.Rad2Deg;
            }

            return Quaternion.Euler(lookEuler) * LookFrameInverse;
        }

        public void ProcessRootMotion(AnimationStream stream) { }
    }
}
