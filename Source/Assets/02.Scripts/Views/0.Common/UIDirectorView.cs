using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

/// <summary>기존 View의 등장·퇴장 호출에 저장된 DOTween 연출만 덧붙입니다.</summary>
[DisallowMultipleComponent, AddComponentMenu("UI/UIDirector View")]
public sealed class UIDirectorView : MonoBehaviour
{
    public enum EffectKind { Fade, Scale, Move, Rotate }
    public enum StartMode { First, WithPrevious, AfterPrevious }

    [Serializable]
    public sealed class Effect
    {
        public bool enabled = true;
        public EffectKind kind;
        public StartMode startMode;
        public float duration = .3f, delay;
        public Ease ease = Ease.OutQuad;
        public float from, to = 1, peak = 1.04f;
        public bool usePeak;
        public bool absoluteAlpha; // 기존 저장값은 원본 대비 배율, 새 편집값은 실제 CanvasGroup 투명도입니다.
        public Vector2 fromOffset, toOffset;
    }

    [Serializable]
    public sealed class Binding
    {
        public string id;
        public MonoBehaviour owner;
        public RectTransform target;
        public string enterMethod, exitMethod, loopMethod, reactionMethod;
        public bool blockInput = true, ignoreRepeated = true;
        public List<Effect> enter = new(), exit = new();
        public List<Effect> loop = new(), reaction = new();
        public bool loopYoyo = true;
        public UnityEngine.UI.Button reactionButton;
    }

    [SerializeField] List<Binding> _bindings = new();
    public List<Binding> Bindings => _bindings;
    readonly Dictionary<string, Running> _running = new();
    readonly Dictionary<RectTransform, Pose> _poses = new();
    readonly Dictionary<string, Running> _loops = new(), _reactions = new();
    readonly HashSet<string> _armedLoops = new();
    readonly Dictionary<UnityEngine.UI.Button, UnityEngine.Events.UnityAction> _clicks = new();
    Tween _pendingLoops;

    sealed class Running
    {
        public Sequence sequence;
        public Pose pose;
        public bool entering;
        public Action cancel;
    }

    /// <summary>위치·크기·회전과 기존 CanvasGroup 상태를 연출 전 값으로 복원합니다.</summary>
    public sealed class Pose
    {
        public readonly RectTransform target;
        public readonly Vector2 position;
        public readonly Vector3 scale, angles;
        public readonly CanvasGroup group;
        public readonly float alpha;
        public readonly bool interactable, raycasts;
        readonly CanvasGroup[] _children;
        readonly bool[] _ignoreParents;
        public Pose(RectTransform rect)
        {
            target = rect; position = rect.anchoredPosition; scale = rect.localScale; angles = rect.localEulerAngles;
            group = rect.GetComponent<CanvasGroup>();
            alpha = group != null ? group.alpha : 1;
            interactable = group == null || group.interactable; raycasts = group == null || group.blocksRaycasts;
            _children=rect.GetComponentsInChildren<CanvasGroup>(true).Where(g=>g!=group).ToArray();
            _ignoreParents=_children.Select(g=>g.ignoreParentGroups).ToArray();
        }
        public void IncludeChildren()
        {
            // 부모 연출에서 자식 그룹만 빠지는 일이 없도록 재생 중에만 상속을 적용합니다.
            foreach(var child in _children)if(child!=null)child.ignoreParentGroups=false;
        }
        public void Restore()
        {
            if (target == null) return;
            target.anchoredPosition = position; target.localScale = scale; target.localEulerAngles = angles;
            RestoreInput(); if (group != null) group.alpha = alpha;
            for(int i=0;i<_children.Length;i++)if(_children[i]!=null)_children[i].ignoreParentGroups=_ignoreParents[i];
        }
        public void RestoreInput()
        {
            if (group == null) return;
            group.interactable = interactable; group.blocksRaycasts = raycasts;
        }
    }

    /// <summary>기존 함수는 이름·인자·버튼 연결을 유지한 채 이 진입점만 한 번 참조합니다.</summary>
    public static void Dispatch(MonoBehaviour owner, string method, bool entering, Action original)
    {
        if (owner == null) return;
        var director = owner.GetComponent<UIDirectorView>();
        if (!Application.isPlaying || director == null || !director.enabled)
        { original?.Invoke(); return; }
        var extra=director._bindings.Where(b=>b.owner==owner&&b.target!=null&&
            ((!string.IsNullOrEmpty(b.loopMethod)&&b.loopMethod==method)||(!string.IsNullOrEmpty(b.reactionMethod)&&b.reactionMethod==method))).ToArray();
        if(extra.Length>0)
        {
            original?.Invoke();
            if(director==null||!director.isActiveAndEnabled)return;
            foreach(var binding in extra)
                if(binding.loopMethod==method){director._armedLoops.Add(binding.id);director.StartLoop(binding);}
                else director.PlayReaction(binding);
            return;
        }
        director.Play(owner, method, entering, original);
    }

    void Play(MonoBehaviour owner, string method, bool entering, Action original)
    {
        var matches = _bindings.Where(b => b.owner == owner && b.target != null &&
            (entering ? b.enterMethod : b.exitMethod) == method &&
            (entering ? b.enter : b.exit).Any(e => e.enabled)).ToArray();
        if (matches.Length == 0) { original?.Invoke(); return; }
        if (matches.Any(b => b.ignoreRepeated && _running.TryGetValue(b.id, out var run) &&
            run.entering == entering && run.sequence != null && run.sequence.IsActive())) return;

        bool cancelled = false;
        // 다른 방향의 대기 중인 숨김 콜백도 함께 취소하여, 다시 연 창이 뒤늦게 닫히지 않게 합니다.
        foreach (var binding in matches)
        {
            StopExtra(binding);
            if (_running.TryGetValue(binding.id, out var previous))
            { previous.cancel?.Invoke(); previous.sequence?.Kill(false); previous.pose.Restore(); _running.Remove(binding.id); }
            if (_poses.TryGetValue(binding.target, out var previousPose)) previousPose.Restore();
        }
        if (entering) original?.Invoke();
        if (this == null || !gameObject.activeInHierarchy)
        { if (!entering) original?.Invoke(); return; }
        var active = matches.Where(b => b.target != null && b.target.gameObject.activeInHierarchy).ToArray();
        if (active.Length == 0) { if (!entering) original?.Invoke(); return; }
        int remaining = active.Length;
        foreach (var binding in active)
        {
            if (!_poses.TryGetValue(binding.target, out var pose) || entering)
                _poses[binding.target] = pose = new Pose(binding.target);
            var list = entering ? binding.enter : binding.exit;
            var sequence = BuildSequence(binding.target, list, pose);
            if (sequence == null) { Complete(); continue; }
            if (binding.blockInput && pose.group != null)
            { pose.group.interactable = false; pose.group.blocksRaycasts = true; }
            _running[binding.id] = new Running { sequence = sequence, pose = pose, entering = entering, cancel = () => cancelled = true };
            sequence.OnComplete(() =>
            {
                _running.Remove(binding.id);
                sequence.Kill(false);
                if (entering) { pose.RestoreInput(); StartLoop(binding); } else pose.Restore();
                Complete();
            });
            sequence.Play();
        }
        void Complete()
        {
            if (--remaining == 0 && !cancelled && !entering) original?.Invoke();
        }
    }

    /// <summary>대상 활성화 이후 상시 반복을 시작합니다. 등장 연출이 있으면 그 완료까지 기다립니다.</summary>
    void Start()
    {
        foreach(var binding in _bindings) QueueLoop(binding);
    }
    void OnEnable()
    {
        foreach(var binding in _bindings){binding.loop??=new();binding.reaction??=new();}
        foreach(var binding in _bindings)
        {
            if(!string.IsNullOrEmpty(binding.reactionMethod)||binding.reactionButton==null||_clicks.ContainsKey(binding.reactionButton))continue;
            var button=binding.reactionButton;
            UnityEngine.Events.UnityAction callback=()=>{
                foreach(var item in _bindings.Where(b=>b.reactionButton==button&&string.IsNullOrEmpty(b.reactionMethod)))PlayReaction(item);
            };
            _clicks.Add(button,callback);button.onClick.AddListener(callback);
        }
        foreach(var binding in _bindings)QueueLoop(binding);
    }
    void QueueLoop(Binding binding)
    {
        if(!Application.isPlaying||binding.target==null)return;
        var watcher=binding.target.GetComponent<UIDirectorTargetView>();
        if(watcher==null)watcher=binding.target.gameObject.AddComponent<UIDirectorTargetView>();
        watcher.Register(this);
        _pendingLoops?.Kill(false);
        _pendingLoops=DOVirtual.DelayedCall(0,()=>{
            foreach(var item in _bindings)StartLoop(item);
        }).SetUpdate(true);
    }
    Pose Baseline(Binding binding)
    {
        if(!_poses.TryGetValue(binding.target,out var pose))_poses[binding.target]=pose=new Pose(binding.target);
        return pose;
    }
    void StartLoop(Binding binding)
    {
        if(!Application.isPlaying||!isActiveAndEnabled||binding.target==null||!binding.target.gameObject.activeInHierarchy||
            (!string.IsNullOrEmpty(binding.loopMethod)&&!_armedLoops.Contains(binding.id))||!binding.loop.Any(e=>e.enabled)||_running.ContainsKey(binding.id)||_reactions.ContainsKey(binding.id)||_loops.ContainsKey(binding.id))return;
        var pose=Baseline(binding);pose.Restore();
        var sequence=BuildSequence(binding.target,binding.loop,pose);
        if(sequence==null)return;
        sequence.SetLoops(-1,binding.loopYoyo?LoopType.Yoyo:LoopType.Restart);
        _loops[binding.id]=new Running{sequence=sequence,pose=pose};sequence.Play();
    }
    /// <summary>버튼의 기존 이벤트를 보존하며, 클릭할 때마다 반응 효과를 처음부터 재생합니다.</summary>
    public void PlayReaction(Binding binding)
    {
        if(binding==null||binding.target==null||!binding.target.gameObject.activeInHierarchy||!isActiveAndEnabled||
            !binding.reaction.Any(e=>e.enabled)||_running.ContainsKey(binding.id))return;
        StopExtra(binding);var pose=Baseline(binding);pose.Restore();
        var sequence=BuildSequence(binding.target,binding.reaction,pose);if(sequence==null)return;
        _reactions[binding.id]=new Running{sequence=sequence,pose=pose};
        sequence.OnComplete(()=>{_reactions.Remove(binding.id);sequence.Kill(false);pose.Restore();StartLoop(binding);});sequence.Play();
    }
    void StopExtra(Binding binding)
    {
        foreach(var collection in new[]{_loops,_reactions})
            if(collection.TryGetValue(binding.id,out var run)){collection.Remove(binding.id);run.sequence?.Kill(false);run.pose?.Restore();}
    }
    /// <summary>대상의 활성 상태 이벤트만 받아 반복 시작 및 개별 연출 해제를 처리합니다.</summary>
    public void TargetVisibilityChanged(RectTransform target,bool visible)
    {
        foreach(var binding in _bindings.Where(b=>b.target==target))
        {
            if(visible){if(isActiveAndEnabled)QueueLoop(binding);continue;}
            _armedLoops.Remove(binding.id);
            StopExtra(binding);
            if(_running.TryGetValue(binding.id,out var run)){_running.Remove(binding.id);run.cancel?.Invoke();run.sequence?.Kill(false);run.pose?.Restore();}
        }
    }

    /// <summary>이전 배율 방식과 실제 투명도 방식을 구분하고 항상 0~1 안으로 제한합니다.</summary>
    public static float ResolveAlpha(float original, Effect effect, float value) =>
        Mathf.Clamp01(effect.absoluteAlpha ? value : original * Mathf.Clamp01(value));

    /// <summary>저장된 효과 순서로 하나의 시퀀스를 구성합니다. 편집기 미리보기도 같은 함수를 사용합니다.</summary>
    public static Sequence BuildSequence(RectTransform target, IList<Effect> effects, Pose pose)
    {
        if (target == null || effects == null || !effects.Any(e => e.enabled)) return null;
        pose.IncludeChildren();
        var sequence = DOTween.Sequence().SetUpdate(true).SetAutoKill(false).Pause();
        float previousStart = 0, previousEnd = 0;
        var initialized = new HashSet<EffectKind>();
        foreach (var effect in effects)
        {
            if (!effect.enabled) continue;
            float start = Start(effect, previousStart, previousEnd);
            float duration = Mathf.Max(.01f, effect.duration);
            // 같은 속성의 후속 효과가 첫 효과의 시작값을 덮지 않도록 최초 한 번만 초기화합니다.
            if (initialized.Add(effect.kind)) SetStart(target, effect, pose);
            Tween tween = null;
            switch (effect.kind)
            {
                case EffectKind.Fade:
                    if (pose.group != null)
                        tween = pose.group.DOFade(ResolveAlpha(pose.alpha, effect, effect.to), duration)
                            .From(ResolveAlpha(pose.alpha, effect, effect.from), false);
                    break;
                case EffectKind.Scale:
                    if (effect.usePeak)
                    {
                        tween = DOTween.Sequence()
                            .Append(target.DOScale(pose.scale * effect.peak, duration * .6f).From(pose.scale * effect.from, false).SetEase(effect.ease))
                            .Append(target.DOScale(pose.scale * effect.to, duration * .4f).SetEase(effect.ease));
                    }
                    else tween = target.DOScale(pose.scale * effect.to, duration).From(pose.scale * effect.from, false);
                    break;
                case EffectKind.Move:
                    tween = target.DOAnchorPos(pose.position + effect.toOffset, duration).From(pose.position + effect.fromOffset, false);
                    break;
                case EffectKind.Rotate:
                    tween = target.DOLocalRotate(pose.angles + Vector3.forward * effect.to, duration, RotateMode.FastBeyond360)
                        .From(pose.angles + Vector3.forward * effect.from, false);
                    break;
            }
            if (tween != null)
            {
                if (!(effect.kind == EffectKind.Scale && effect.usePeak)) tween.SetEase(effect.ease);
                sequence.Insert(start, tween);
            }
            previousStart = start; previousEnd = start + duration;
        }
        return sequence;
    }

    static void SetStart(RectTransform target, Effect effect, Pose pose)
    {
        switch (effect.kind)
        {
            case EffectKind.Fade: if (pose.group != null) pose.group.alpha = ResolveAlpha(pose.alpha, effect, effect.from); break;
            case EffectKind.Scale: target.localScale = pose.scale * effect.from; break;
            case EffectKind.Move: target.anchoredPosition = pose.position + effect.fromOffset; break;
            case EffectKind.Rotate: target.localEulerAngles = pose.angles + Vector3.forward * effect.from; break;
        }
    }
    public static float Start(Effect effect, float previousStart, float previousEnd) =>
        Mathf.Max(0, effect.delay) + (effect.startMode == StartMode.First ? 0 :
            effect.startMode == StartMode.WithPrevious ? previousStart : previousEnd);

    /// <summary>같은 속성에 겹치는 효과는 적용 전에 알려주고, 의도하지 않은 경쟁을 방지합니다.</summary>
    public static string Validate(IList<Effect> effects)
    {
        var ranges = new List<(EffectKind kind, float start, float end)>();
        float previousStart = 0, previousEnd = 0;
        foreach (var effect in effects)
        {
            if (!effect.enabled) continue;
            if (!float.IsFinite(effect.duration) || effect.duration < .01f || !float.IsFinite(effect.delay) || effect.delay < 0 ||
                !float.IsFinite(effect.from) || !float.IsFinite(effect.to) || !float.IsFinite(effect.peak) ||
                !float.IsFinite(effect.fromOffset.x) || !float.IsFinite(effect.fromOffset.y) ||
                !float.IsFinite(effect.toOffset.x) || !float.IsFinite(effect.toOffset.y)) return "시간과 값은 유효한 숫자로 입력하세요 (지속시간 0.01초 이상).";
            float start = Start(effect, previousStart, previousEnd), end = start + effect.duration;
            if (ranges.Any(r => r.kind == effect.kind && start < r.end - .0001f && end > r.start + .0001f))
                return $"{effect.kind} 효과의 시간이 겹칩니다. 앞 효과 다음에 실행하도록 바꾸세요.";
            ranges.Add((effect.kind, start, end)); previousStart = start; previousEnd = end;
        }
        return null;
    }

    /// <summary>지정한 View와 대상의 연출만 취소하여 다른 UI의 반복·반응 효과는 유지합니다.</summary>
    public void CancelEffects(MonoBehaviour owner, RectTransform target)
    {
        foreach (var binding in _bindings.Where(b => b.owner == owner && b.target == target))
        {
            _armedLoops.Remove(binding.id); // Explicit cancellation must not rearm a static target.
            StopExtra(binding);
            if (_running.TryGetValue(binding.id, out var run))
            {
                run.cancel?.Invoke(); run.sequence?.Kill(false); run.pose.Restore();
                _running.Remove(binding.id);
            }
        }
        if (_poses.TryGetValue(target, out var pose)) { pose.Restore(); _poses.Remove(target); }
    }
    /// <summary>이 컴포넌트가 만든 시퀀스만 중지하며, 기존 View의 다른 DOTween 연출에는 관여하지 않습니다.</summary>
    void StopAll()
    {
        _armedLoops.Clear();
        _pendingLoops?.Kill(false);_pendingLoops=null;
        foreach(var binding in _bindings)StopExtra(binding);
        foreach (var run in _running.Values.ToArray())
        { run.cancel?.Invoke(); run.sequence?.Kill(false); run.pose?.Restore(); }
        _running.Clear();
        // 완료된 등장 연출도 기본 배치를 남기지 않습니다. 다음 퇴장은 저장한 기준에서 시작합니다.
        foreach (var pose in _poses.Values) pose.Restore();
    }
    void OnDisable()
    {
        foreach(var pair in _clicks)if(pair.Key!=null)pair.Key.onClick.RemoveListener(pair.Value);
        _clicks.Clear();StopAll();
    }
    void OnDestroy() { StopAll(); }
}

