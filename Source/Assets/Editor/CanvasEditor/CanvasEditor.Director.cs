using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Effect = UIDirectorView.Effect;
using Binding = UIDirectorView.Binding;

public sealed partial class CanvasEditor
{
    VisualElement _previewPanel, _resourcePreviewHost, _directorPreviewHost, _directorSettings, _directorCode;
    ScrollView _directorList;
    ScrollView _directorTargetSprites;
    Label _directorStatus, _directorSummary, _directorTime;
    ObjectField _directorTargetField;
    PopupField<string> _directorViewPopup, _directorEnterPopup, _directorExitPopup, _directorLoopPopup, _directorReactionPopup;
    PopupField<string> _directorPreset;
    Toggle _directorBlock, _directorIgnore;
    Button _directorApply, _directorUndo, _directorPrefab;
    Slider _directorScrub;
    readonly List<MonoBehaviour> _directorViews = new();
    List<UIDirectorSource.Method> _directorMethods = new();
    [SerializeField]
    MonoBehaviour _directorOwner;
    [SerializeField]
    Binding _directorDraft;
    [SerializeField]
    RectTransform _directorOriginal;
    [SerializeField]
    bool _directorDirty;
    [SerializeField]
    bool _directorExit;
    [SerializeField]
    int _directorExtraTrack;
    int _directorSelected;
    bool _directorRefreshing, _directorLoop, _directorPlaying;
    readonly Stack<string> _directorHistory = new();
    Sequence _directorSequence;
    UIDirectorView.Pose _directorPose;
    double _directorStarted;
    float _directorTotal;
    List<Effect> DirectorEffects => _directorDraft == null ? null : _directorExtraTrack == 1 ? _directorDraft.loop : _directorExtraTrack == 2 ? _directorDraft.reaction : _directorExit ? _directorDraft.exit : _directorDraft.enter;
    string DirectorTrackName => _directorExtraTrack == 1 ? "상시 반복" : _directorExtraTrack == 2 ? "상호작용 반응" : _directorExit ? "퇴장" : "등장";
#region 연출 화면 구성
    /// <summary>함수 연결과 연출 목록 및 설정 화면을 구성합니다.</summary>
    void BuildDirector(VisualElement parent)
    {
        parent.AddToClassList("director-page");
        var upper = Row(parent, "director-upper");
        _directorPreviewHost = new VisualElement();
        _directorPreviewHost.AddToClassList("director-preview-host");
        upper.Add(_directorPreviewHost);
        var right = Panel(upper, "02  기존 함수 연결 · DOTween", "director-functions");
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        right.Add(scroll);
        scroll.AddToClassList("director-function-scroll");
        _directorTargetField = new ObjectField("연출 대상")
        {objectType = typeof(RectTransform), allowSceneObjects = true};
        _directorTargetField.AddToClassList("stacked-field");
        scroll.Add(_directorTargetField);
        _directorTargetField.RegisterValueChangedCallback(e =>
        {
            if (_directorRefreshing)
                return;
            var original = e.newValue as RectTransform;
            var node = _nodes.Values.FirstOrDefault(n => n.Original == original);
            if (node != null)
            {
                SelectNode(node.Copy);
                RefreshDirector();
            }
            else
            {
                DirectorMessage("현재 Canvas 내부의 UI를 선택하세요.");
                RefreshDirector();
            }
        });
        BuildPreviewVisibilityControl(scroll);
        Note(scroll, "선택한 계층의 Sprite · 보기 전용");
        _directorTargetSprites = new ScrollView(ScrollViewMode.Horizontal);
        _directorTargetSprites.AddToClassList("director-target-sprites");
        scroll.Add(_directorTargetSprites);
        _directorViewPopup = new PopupField<string>("제어 View", new List<string>{"선택 안 함"}, 0);
        _directorViewPopup.AddToClassList("stacked-field");
        scroll.Add(_directorViewPopup);
        _directorViewPopup.RegisterValueChangedCallback(_ =>
        {
            if (_directorRefreshing || _directorDraft == null)
                return;
            DirectorRemember();
            _directorOwner = _directorViewPopup.index > 0 ? _directorViews[_directorViewPopup.index - 1] : null;
            _directorDraft.enterMethod = _directorDraft.exitMethod = _directorDraft.loopMethod = _directorDraft.reactionMethod = "";
            RefreshDirectorFunctions();
        });
        var primaryFunctions = Row(scroll, "director-function-pair");
        _directorEnterPopup = new PopupField<string>("등장 함수", new List<string>{"선택 안 함"}, 0);
        _directorEnterPopup.AddToClassList("stacked-field");
        primaryFunctions.Add(_directorEnterPopup);
        _directorExitPopup = new PopupField<string>("퇴장 함수", new List<string>{"선택 안 함"}, 0);
        _directorExitPopup.AddToClassList("stacked-field");
        primaryFunctions.Add(_directorExitPopup);
        _directorEnterPopup.RegisterValueChangedCallback(_ => DirectorChooseMethod(false));
        _directorExitPopup.RegisterValueChangedCallback(_ => DirectorChooseMethod(true));
        var extraFunctions = Row(scroll, "director-function-pair");
        _directorLoopPopup = new PopupField<string>("상시 반복 시작 함수", new List<string>{"자동 · 대상 활성화"}, 0);
        _directorLoopPopup.name = "director-loop-function";
        _directorLoopPopup.AddToClassList("stacked-field");
        extraFunctions.Add(_directorLoopPopup);
        _directorReactionPopup = new PopupField<string>("상호작용 반응 함수", new List<string>{"버튼 클릭 방식"}, 0);
        _directorReactionPopup.name = "director-reaction-function";
        _directorReactionPopup.AddToClassList("stacked-field");
        extraFunctions.Add(_directorReactionPopup);
        _directorLoopPopup.RegisterValueChangedCallback(_ => DirectorChooseExtraMethod(false));
        _directorReactionPopup.RegisterValueChangedCallback(_ => DirectorChooseExtraMethod(true));
        Note(scroll, "상시 반복: 함수 미지정 시 활성화 후 자동 시작합니다.\n상호작용 반응: 함수 미지정 시 효과 설정의 반응할 버튼을 사용합니다.");
        Note(scroll, "선택한 대상과 모든 자식 UI가 함께 연출됩니다.\n기존 함수의 인자·버튼 이벤트는 유지합니다. 같은 bool 함수는 true=등장 / false=퇴장으로 연결됩니다.");
        _directorBlock = new Toggle("재생 중 입력 차단");
        scroll.Add(_directorBlock);
        _directorIgnore = new Toggle("재생 중 중복 요청 무시");
        scroll.Add(_directorIgnore);
        _directorBlock.RegisterValueChangedCallback(e =>
        {
            if (!_directorRefreshing && _directorDraft != null)
            {
                DirectorRemember();
                _directorDraft.blockInput = e.newValue;
                RefreshDirectorStatus();
            }
        });
        _directorIgnore.RegisterValueChangedCallback(e =>
        {
            if (!_directorRefreshing && _directorDraft != null)
            {
                DirectorRemember();
                _directorDraft.ignoreRepeated = e.newValue;
                RefreshDirectorStatus();
            }
        });
        Note(scroll, "퇴장 완료 → 기존 숨김 처리\n미리보기에서는 원본 함수·버튼을 실행하지 않습니다.");
        AddButton(scroll, "선택한 연출 함수 코드 보기", () => ShowDirectorCode(true));
        var bar = Row(parent, "director-direction");
        var typeLabel = new Label("유형") { name = "director-type-label" };
        typeLabel.style.marginRight = 8;
        typeLabel.style.flexShrink = 0;
        bar.Add(typeLabel);
        var enterType = AddButton(bar, "등장", () => SetDirectorDirection(false));
        enterType.name = "director-track-0";
        enterType.AddToClassList("director-type-button");
        var exitType = AddButton(bar, "퇴장", () => SetDirectorDirection(true));
        exitType.name = "director-track-1";
        exitType.AddToClassList("director-type-button");
        var loopType = AddButton(bar, "상시 반복", () => SetDirectorExtra(1));
        loopType.name = "director-track-2";
        loopType.AddToClassList("director-type-button");
        var reactionType = AddButton(bar, "상호작용 반응", () => SetDirectorExtra(2));
        reactionType.name = "director-track-3";
        reactionType.AddToClassList("director-type-button");
        _directorSummary = new Label();
        _directorSummary.AddToClassList("director-summary");
        bar.Add(_directorSummary);
        AddButton(bar, "삭제 / 초기화…", OpenDirectorReset);
        var lower = Row(parent, "director-lower");
        var effects = Panel(lower, "03  효과 목록 · 같은 효과 중복 사용 가능", "director-effects");
        _directorList = new ScrollView(ScrollViewMode.Vertical);
        _directorList.AddToClassList("director-effect-list");
        effects.Add(_directorList);
        var presets = Row(effects, "director-preset-row");
        _directorPreset = new PopupField<string>(DirectorPresetNames(), 0);
        presets.Add(_directorPreset);
        AddButton(presets, "연출 추가", () => AddDirectorEffect(_directorPreset.index));
        var details = Panel(lower, "04  효과 설정 / 연출 코드", "director-details");
        var detailTabs = Row(details, "director-detail-tabs");
        AddButton(detailTabs, "효과 설정", () => ShowDirectorCode(false));
        AddButton(detailTabs, "연출 코드", () => ShowDirectorCode(true));
        _directorSettings = new ScrollView(ScrollViewMode.Vertical);
        _directorSettings.AddToClassList("director-settings");
        details.Add(_directorSettings);
        _directorCode = new ScrollView(ScrollViewMode.Vertical);
        _directorCode.AddToClassList("director-code");
        details.Add(_directorCode);
        Display(_directorCode, false);
        BuildDirectorLibraryButtons(details);
        var footer = Row(parent, "apply-toolbar");
        _directorStatus = new Label();
        _directorStatus.AddToClassList("status-label");
        footer.Add(_directorStatus);
        _directorUndo = AddButton(footer, "실행 취소", UndoDirector);
        AddButton(footer, "저장된 구성 불러오기", () =>
        {
            if (CanDiscardDirector())
                LoadDirector(_directorOriginal);
        });
        _directorApply = AddButton(footer, "변경 적용", ApplyDirector);
        _directorApply.AddToClassList("primary-button");
        _directorPrefab = AddButton(footer, "프리팹 열기", OpenPrefab);
        EditorApplication.update -= DirectorTick;
        EditorApplication.update += DirectorTick;
    }

    /// <summary>복제본의 연출 재생과 정지 및 재생 위치 조작부를 구성합니다.</summary>
    void BuildDirectorPlayback(VisualElement preview)
    {
        var toolbar = Row(preview, "director-playback");
        AddButton(toolbar, "등장 재생", () =>
        {
            SetDirectorDirection(false);
            PlayDirector();
        });
        AddButton(toolbar, "퇴장 재생", () =>
        {
            SetDirectorDirection(true);
            PlayDirector();
        });
        AddButton(toolbar, "현재 효과 재생", PlayDirector);
        AddButton(toolbar, "정지 · 원상 복구", StopDirectorPreview);
        var loop = new Toggle("반복");
        loop.RegisterValueChangedCallback(e => _directorLoop = e.newValue);
        toolbar.Add(loop);
        _directorTime = new Label("0.00초");
        toolbar.Add(_directorTime);
        _directorScrub = new Slider("재생 위치", 0, 1);
        _directorScrub.AddToClassList("director-scrub");
        preview.Add(_directorScrub);
        _directorScrub.RegisterValueChangedCallback(e =>
        {
            if (_directorRefreshing || _mode != ToolMode.UIDirector)
                return;
            if (_directorSequence == null)
                PrepareDirectorPreview();
            _directorPlaying = false;
            if (_directorSequence != null)
            {
                _directorSequence.Goto(e.newValue * _directorTotal, false);
                _renderNeeded = true;
                UpdateDirectorTime(e.newValue * _directorTotal);
            }
        });
    }

    /// <summary>연출 설정에 필요한 안내 문구를 추가합니다.</summary>
    static void Note(VisualElement parent, string text)
    {
        var note = new Label(text);
        note.AddToClassList("director-note");
        parent.Add(note);
    }

#endregion
#region 선택과 편집 이력
    /// <summary>원본을 변경하지 않도록 연출 연결 설정을 복사합니다.</summary>
    static Binding CopyBinding(Binding value)
    {
        var copy = JsonUtility.FromJson<Binding>(JsonUtility.ToJson(value));
        copy.loop ??= new();
        copy.reaction ??= new();
        return copy;
    }

    /// <summary>효과 설정을 독립적으로 편집할 수 있게 복사합니다.</summary>
    static Effect CopyEffect(Effect value) => JsonUtility.FromJson<Effect>(JsonUtility.ToJson(value));
    /// <summary>적용 전 연출 변경을 버려도 되는지 확인합니다.</summary>
    bool CanDiscardDirector() => IsTestCanvas || !_directorDirty || EditorUtility.DisplayDialog("UIDirector", "아직 적용하지 않은 연출 변경을 버릴까요?", "변경 버리기", "계속 편집");
    /// <summary>선택 변경 전에 연출 변경과 미리보기 정리 여부를 확인합니다.</summary>
    bool DirectorBeforePick(Transform next)
    {
        if (next == _picked)
            return true;
        if (!CanDiscardDirector())
            return false;
        StopDirectorPreview();
        return true;
    }

    /// <summary>선택한 계층의 연출 연결을 불러오거나 갱신합니다.</summary>
    void DirectorPicked()
    {
        var original = _picked != null && _nodes.TryGetValue(_picked, out var node) ? node.Original as RectTransform : null;
        if (original != _directorOriginal)
            LoadDirector(original);
        else
            RefreshDirector();
    }

    /// <summary>저장된 연출 설정을 복사하고 편집 이력을 초기화합니다.</summary>
    void LoadDirector(RectTransform original)
    {
        StopDirectorPreview();
        _directorOriginal = original;
        _directorHistory.Clear();
        _directorDirty = false;
        _directorSelected = 0;
        _directorOwner = null;
        _directorDraft = null;
        if (original != null)
        {
            var saved = _target.GetComponentsInChildren<UIDirectorView>(true).SelectMany(d => d.Bindings.Select(b => (driver: d, binding: b))).FirstOrDefault(pair => pair.binding.target == original);
            if (saved.driver != null)
            {
                _directorOwner = saved.binding.owner;
                _directorDraft = CopyBinding(saved.binding);
            }

            if (_directorDraft == null)
                _directorDraft = new Binding{target = original, id = Guid.NewGuid().ToString("N")};
        }

        RefreshDirector();
    }

    /// <summary>선택 대상과 제어 View 및 연출 설정을 화면에 반영합니다.</summary>
    void RefreshDirector()
    {
        if (_directorTargetField == null)
            return;
        _directorRefreshing = true;
        _directorTargetField.SetValueWithoutNotify(_directorOriginal);
        RefreshDirectorTargetSprites();
        _directorViewPopup.SetEnabled(_directorDraft != null);
        _directorViews.Clear();
        if (_target != null)
            _directorViews.AddRange(_target.GetComponentsInChildren<MonoBehaviour>(true).Where(v => v != null && v is not UIDirectorView && v.GetType().Name.EndsWith("View", StringComparison.Ordinal)).OrderBy(v => _directorOriginal != null && _directorOriginal.IsChildOf(v.transform) ? 0 : 1).ThenBy(v => v.GetType().Name));
        if (_directorOwner != null && !_directorViews.Contains(_directorOwner))
            _directorViews.Add(_directorOwner);
        _directorViewPopup.choices = new List<string>{"선택 안 함"}.Concat(_directorViews.Select(v => v.GetType().Name + " · " + v.name)).ToList();
        _directorViewPopup.index = _directorOwner != null ? _directorViews.IndexOf(_directorOwner) + 1 : 0;
        _directorBlock.SetValueWithoutNotify(_directorDraft?.blockInput ?? true);
        _directorIgnore.SetValueWithoutNotify(_directorDraft?.ignoreRepeated ?? true);
        _directorRefreshing = false;
        RefreshDirectorFunctions();
        DrawDirectorEffects();
        DrawDirectorSettings();
        RefreshDirectorStatus();
    }

    /// <summary>연출 대상 계층에서 사용하는 Sprite를 조회용 카드로 표시합니다.</summary>
    void RefreshDirectorTargetSprites()
    {
        if (_directorTargetSprites == null)
            return;
        _directorTargetSprites.Clear();
        if (_directorOriginal == null)
        {
            Note(_directorTargetSprites, "대상을 선택하면 사용 이미지를 표시합니다.");
            return;
        }

        var uses = _directorOriginal.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(img => img.overrideSprite != null || img.sprite != null).GroupBy(img => img.overrideSprite != null ? img.overrideSprite : img.sprite).ToArray();
        if (uses.Length == 0)
        {
            Note(_directorTargetSprites, "이 계층에는 Image Sprite가 없습니다.");
            return;
        }

        foreach (var use in uses)
        {
            var card = new VisualElement();
            card.AddToClassList("director-sprite-card");
            _directorTargetSprites.Add(card);
            card.tooltip = use.Key.name + "\n" + string.Join("\n", use.Select(img => AnimationUtility.CalculateTransformPath(img.transform, _directorOriginal) + " · " + (img.gameObject.activeInHierarchy ? "표시 중" : "비활성")));
            var image = new UnityEngine.UIElements.Image{sprite = use.Key, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore};
            image.AddToClassList("director-sprite-image");
            card.Add(image);
            var name = new Label(use.Key.name + (use.Count() > 1 ? $" ×{use.Count()}" : ""))
            {pickingMode = PickingMode.Ignore};
            name.AddToClassList("director-sprite-name");
            card.Add(name);
        }
    }

    /// <summary>제어 View의 연결 가능한 함수를 네 선택 칸에 표시합니다.</summary>
    void RefreshDirectorFunctions()
    {
        _directorRefreshing = true;
        _directorMethods = UIDirectorSource.Methods(_directorOwner);
        var choices = new List<string>{"선택 안 함"}.Concat(_directorMethods.Select(m => m.label + (m.Supported ? "" : " [자동 연결 불가]"))).ToList();
        _directorEnterPopup.choices = new List<string>(choices);
        _directorExitPopup.choices = new List<string>(choices);
        _directorEnterPopup.index = Mathf.Max(0, _directorMethods.FindIndex(m => m.key == _directorDraft?.enterMethod) + 1);
        _directorExitPopup.index = Mathf.Max(0, _directorMethods.FindIndex(m => m.key == _directorDraft?.exitMethod) + 1);
        _directorLoopPopup.choices = new List<string>{"자동 · 대상 활성화"}.Concat(choices.Skip(1)).ToList();
        _directorReactionPopup.choices = new List<string>{"버튼 클릭 방식"}.Concat(choices.Skip(1)).ToList();
        _directorLoopPopup.index = Mathf.Max(0, _directorMethods.FindIndex(m => m.key == _directorDraft?.loopMethod) + 1);
        _directorReactionPopup.index = Mathf.Max(0, _directorMethods.FindIndex(m => m.key == _directorDraft?.reactionMethod) + 1);
        _directorRefreshing = false;
        DrawDirectorCode();
        RefreshDirectorStatus();
    }

    /// <summary>등장 또는 퇴장 함수 선택을 편집 설정에 기록합니다.</summary>
    void DirectorChooseMethod(bool exit)
    {
        if (_directorRefreshing || _directorDraft == null)
            return;
        DirectorRemember();
        var popup = exit ? _directorExitPopup : _directorEnterPopup;
        var method = popup.index > 0 ? _directorMethods[popup.index - 1] : null;
        if (exit)
            _directorDraft.exitMethod = method?.key ?? "";
        else
            _directorDraft.enterMethod = method?.key ?? "";
        if (method != null && !method.Supported)
            DirectorMessage(method.reason);
        DrawDirectorCode();
        RefreshDirectorStatus();
    }

    /// <summary>상시 반복 또는 상호작용 반응 함수 선택을 기록합니다.</summary>
    void DirectorChooseExtraMethod(bool reaction)
    {
        if (_directorRefreshing || _directorDraft == null)
            return;
        DirectorRemember();
        var popup = reaction ? _directorReactionPopup : _directorLoopPopup;
        var method = popup.index > 0 ? _directorMethods[popup.index - 1] : null;
        if (reaction)
            _directorDraft.reactionMethod = method?.key ?? "";
        else
            _directorDraft.loopMethod = method?.key ?? "";
        if (method != null && !method.Supported)
            DirectorMessage(method.reason);
        DrawDirectorSettings();
        DrawDirectorCode();
        RefreshDirectorStatus();
    }

    [Serializable]
    sealed class DirectorUndo
    {
        public Binding binding;
        public MonoBehaviour owner;
    }

    /// <summary>연출 설정을 바꾸기 전 실행 취소용 상태를 기록합니다.</summary>
    void DirectorRemember()
    {
        if (_directorDraft == null)
            return;
        StopDirectorPreview();
        _directorHistory.Push(JsonUtility.ToJson(new DirectorUndo{binding = _directorDraft, owner = _directorOwner}));
        _directorDirty = true;
    }

    /// <summary>직전 연출 설정을 복원하고 화면을 갱신합니다.</summary>
    void UndoDirector()
    {
        if (_directorHistory.Count == 0)
            return;
        StopDirectorPreview();
        var state = JsonUtility.FromJson<DirectorUndo>(_directorHistory.Pop());
        _directorDraft = state.binding;
        _directorOwner = state.owner;
        _directorDirty = true;
        RefreshDirector();
    }

#endregion
#region 프리셋과 상세 효과
    /// <summary>등장 또는 퇴장 효과 목록으로 전환합니다.</summary>
    void SetDirectorDirection(bool exit)
    {
        StopDirectorPreview();
        _directorExtraTrack = 0;
        _directorExit = exit;
        _directorSelected = 0;
        DrawDirectorEffects();
        DrawDirectorSettings();
        DrawDirectorCode();
        RefreshDirectorStatus();
    }

    /// <summary>상시 반복 또는 상호작용 반응 효과 목록으로 전환합니다.</summary>
    void SetDirectorExtra(int track)
    {
        StopDirectorPreview();
        _directorExtraTrack = track;
        _directorSelected = 0;
        DrawDirectorEffects();
        DrawDirectorSettings();
        DrawDirectorCode();
        RefreshDirectorStatus();
    }

    /// <summary>현재 연출 유형에 맞는 기본 프리셋 이름을 제공합니다.</summary>
    List<string> DirectorPresetNames() => _directorExtraTrack switch
    {
    1 => new List<string>{"천천히 깜빡이기", "위아래 둥실거리기", "좌우 흔들리기", "숨 쉬듯 커졌다 작아지기", "살짝 기울이기"}, 2 => new List<string>{"클릭 팝", "눌렀다 돌아오기", "오류 알림 · 좌우 흔들기", "살짝 까딱이기", "가볍게 점프", "반짝임"}, _ => _directorExit ? new List<string>{"서서히 사라지기", "살짝 줄어들기", "아래로 내려가기", "기울이며 퇴장", "위로 슬라이드 + 페이드", "왼쪽으로 슬라이드 + 페이드", "오른쪽으로 슬라이드 + 페이드", "축소 + 페이드"} : new List<string>{"서서히 나타나기", "살짝 튀기기", "아래에서 올라오기", "기울기 복원", "위에서 슬라이드 + 페이드", "왼쪽에서 슬라이드 + 페이드", "오른쪽에서 슬라이드 + 페이드", "확대 + 페이드"}}

    ;
    // 게임 UI에서 자주 사용하는 기본값만 구성하며 실제 재생은 기존 DOTween 경로를 사용합니다.
    /// <summary>선택한 게임 UI 프리셋의 DOTween 효과 설정을 구성합니다.</summary>
    static List<Effect> CreateDirectorPreset(int track, int kind, bool exit)
    {
        Effect Alpha(float from, float to, float duration) => new Effect{kind = UIDirectorView.EffectKind.Fade, from = from, to = to, duration = duration, ease = Ease.InOutSine};
        Effect Scale(float peak, float duration) => new Effect{kind = UIDirectorView.EffectKind.Scale, from = 1, to = 1, peak = peak, usePeak = true, duration = duration, ease = Ease.OutQuad};
        Effect Move(Vector2 from, Vector2 to, float duration, Ease ease) => new Effect{kind = UIDirectorView.EffectKind.Move, fromOffset = from, toOffset = to, duration = duration, ease = ease};
        Effect Rotate(float from, float to, float duration) => new Effect{kind = UIDirectorView.EffectKind.Rotate, from = from, to = to, duration = duration, ease = Ease.InOutSine};
        if (track == 1)
            return kind switch
            {
            0 => new List<Effect>{Alpha(1, .3f, .65f)}, 1 => new List<Effect>{Move(new Vector2(0, -6), new Vector2(0, 6), 1.6f, Ease.InOutSine)}, 2 => new List<Effect>{Move(new Vector2(-4, 0), new Vector2(4, 0), 1.2f, Ease.InOutSine)}, 3 => new List<Effect>{new Effect{kind = UIDirectorView.EffectKind.Scale, from = .98f, to = 1.02f, duration = 1.4f, ease = Ease.InOutSine}}, _ => new List<Effect>{Rotate(-2, 2, 1.5f)}}

            ;
        if (track == 2)
            return kind switch
            {
            0 => new List<Effect>{Scale(1.08f, .28f)}, 1 => new List<Effect>{Scale(.92f, .18f)}, 2 => new List<Effect>{Move(Vector2.zero, new Vector2(-8, 0), .05f, Ease.OutQuad), Move(new Vector2(-8, 0), new Vector2(8, 0), .08f, Ease.InOutSine), Move(new Vector2(8, 0), new Vector2(-4, 0), .07f, Ease.InOutSine), Move(new Vector2(-4, 0), Vector2.zero, .06f, Ease.OutQuad)}, 3 => new List<Effect>{Rotate(0, -5, .06f), Rotate(-5, 5, .12f), Rotate(5, 0, .08f)}, 4 => new List<Effect>{Move(Vector2.zero, new Vector2(0, 12), .12f, Ease.OutQuad), Move(new Vector2(0, 12), Vector2.zero, .18f, Ease.OutBounce)}, _ => new List<Effect>{Alpha(1, .35f, .08f), Alpha(.35f, 1, .16f)}}

            ;
        if (kind >= 4)
        {
            var fade = Alpha(exit ? 1 : 0, exit ? 0 : 1, .3f);
            var transform = kind == 7 ? new Effect{kind = UIDirectorView.EffectKind.Scale, from = exit ? 1 : .8f, to = exit ? .8f : 1, duration = .3f, ease = exit ? Ease.InQuad : Ease.OutBack} : Move(exit ? Vector2.zero : kind == 4 ? new Vector2(0, 80) : kind == 5 ? new Vector2(-100, 0) : new Vector2(100, 0), exit ? kind == 4 ? new Vector2(0, 80) : kind == 5 ? new Vector2(-100, 0) : new Vector2(100, 0) : Vector2.zero, .3f, exit ? Ease.InQuad : Ease.OutCubic);
            transform.startMode = UIDirectorView.StartMode.WithPrevious;
            return new List<Effect>{fade, transform};
        }

        var effect = new Effect{kind = (UIDirectorView.EffectKind)kind};
        if (kind == 0)
        {
            effect.from = exit ? 1 : 0;
            effect.to = exit ? 0 : 1;
        }

        if (kind == 1)
        {
            effect.from = exit ? 1 : .92f;
            effect.to = exit ? .9f : 1;
            effect.usePeak = !exit;
        }

        if (kind == 2)
        {
            effect.fromOffset = exit ? Vector2.zero : new Vector2(0, -80);
            effect.toOffset = exit ? new Vector2(0, -80) : Vector2.zero;
        }

        if (kind == 3)
        {
            effect.from = exit ? 0 : -10;
            effect.to = exit ? 10 : 0;
        }

        return new List<Effect>{effect};
    }

    /// <summary>선택한 프리셋을 기존 효과 뒤에 추가합니다.</summary>
    void AddDirectorEffect(int kind)
    {
        if (DirectorEffects == null)
            return;
        DirectorRemember();
        var effects = CreateDirectorPreset(_directorExtraTrack, kind, _directorExit);
        if (_directorExtraTrack == 1)
            _directorDraft.loopYoyo = true;
        int first = DirectorEffects.Count;
        foreach (var effect in effects)
        {
            if (effect.kind == UIDirectorView.EffectKind.Fade)
            {
                effect.from = Mathf.Clamp01(DirectorOriginalAlpha() * effect.from);
                effect.to = Mathf.Clamp01(DirectorOriginalAlpha() * effect.to);
                effect.absoluteAlpha = true;
            }
            effect.startMode = DirectorEffects.Count == first ? (first == 0 ? UIDirectorView.StartMode.First : UIDirectorView.StartMode.AfterPrevious) : effect.startMode == UIDirectorView.StartMode.WithPrevious ? UIDirectorView.StartMode.WithPrevious : UIDirectorView.StartMode.AfterPrevious;
            DirectorEffects.Add(effect);
        }

        _directorSelected = first;
        DrawDirectorEffects();
        DrawDirectorSettings();
        RefreshDirectorStatus();
    }

    /// <summary>현재 유형의 효과와 편집용 조작 버튼을 표시합니다.</summary>
    void DrawDirectorEffects()
    {
        if (_directorList == null)
            return;
        _directorList.Clear();
        if (DirectorEffects == null)
        {
            Note(_directorList, "미리보기나 Hierarchy에서 UI를 선택하세요.");
            return;
        }

        if (DirectorEffects.Count == 0)
            Note(_directorList, "아래에서 효과를 추가하세요. 효과가 없으면 기존 함수가 즉시 실행됩니다.");
        _directorSelected = Mathf.Clamp(_directorSelected, 0, Mathf.Max(0, DirectorEffects.Count - 1));
        for (int i = 0; i < DirectorEffects.Count; i++)
        {
            int index = i;
            var effect = DirectorEffects[i];
            var card = Row(_directorList, "director-effect-card");
            card.EnableInClassList("selected-item", i == _directorSelected);
            var enabled = new Toggle{value = effect.enabled};
            card.Add(enabled);
            enabled.RegisterValueChangedCallback(e =>
            {
                DirectorRemember();
                effect.enabled = e.newValue;
                RefreshDirectorStatus();
            });
            var pick = AddButton(card, $"{i + 1}. {EffectName(effect.kind)}\n{effect.duration:0.00}초 · {effect.ease}", () =>
            {
                _directorSelected = index;
                DrawDirectorEffects();
                DrawDirectorSettings();
                DrawDirectorCode();
            });
            pick.AddToClassList("director-effect-pick");
            var actions = new VisualElement();
            actions.AddToClassList("director-effect-actions");
            card.Add(actions);
            var top = Row(actions);
            AddButton(top, "복제", () =>
            {
                DirectorRemember();
                DirectorEffects.Insert(index + 1, CopyEffect(effect));
                _directorSelected = index + 1;
                DrawDirectorEffects();
                DrawDirectorSettings();
                RefreshDirectorStatus();
            });
            AddButton(top, "삭제", () =>
            {
                DirectorRemember();
                DirectorEffects.RemoveAt(index);
                DrawDirectorEffects();
                DrawDirectorSettings();
                RefreshDirectorStatus();
            });
            var order = Row(actions);
            AddButton(order, "↑", () => MoveDirectorEffect(index, -1));
            AddButton(order, "↓", () => MoveDirectorEffect(index, 1));
        }
    }

    /// <summary>효과 종류를 읽기 쉬운 이름으로 변환합니다.</summary>
    static string EffectName(UIDirectorView.EffectKind kind) => new[]{"투명도", "크기 · 살짝 튀기기", "이동", "회전"}[(int)kind];
    /// <summary>선택한 효과의 실행 순서를 변경합니다.</summary>
    void MoveDirectorEffect(int index, int delta)
    {
        int next = index + delta;
        if (next < 0 || next >= DirectorEffects.Count)
            return;
        DirectorRemember();
        (DirectorEffects[index], DirectorEffects[next]) = (DirectorEffects[next], DirectorEffects[index]);
        _directorSelected = next;
        DrawDirectorEffects();
        DrawDirectorSettings();
        RefreshDirectorStatus();
    }

    /// <summary>반복 방식과 반응 대상 및 효과별 상세 설정을 표시합니다.</summary>
    void DrawDirectorSettings()
    {
        if (_directorSettings == null)
            return;
        _directorSettings.Clear();
        if (_directorDraft != null && _directorExtraTrack == 1)
        {
            var yoyo = new Toggle("왕복 반복 (끄면 처음부터 반복)")
            {value = _directorDraft.loopYoyo};
            _directorSettings.Add(yoyo);
            yoyo.RegisterValueChangedCallback(e =>
            {
                DirectorRemember();
                _directorDraft.loopYoyo = e.newValue;
                RefreshDirectorStatus();
            });
            Note(_directorSettings, string.IsNullOrEmpty(_directorDraft.loopMethod) ? "등장 완료 후 자동 반복합니다. 퇴장·비활성 시 중지합니다." : "상단에 지정한 함수가 호출되면 반복을 시작합니다. 퇴장·비활성 시 중지합니다.");
        }

        if (_directorDraft != null && _directorExtraTrack == 2)
        {
            var button = new ObjectField("반응할 버튼")
            {objectType = typeof(UnityEngine.UI.Button), allowSceneObjects = true, value = _directorDraft.reactionButton};
            button.SetEnabled(string.IsNullOrEmpty(_directorDraft.reactionMethod));
            button.AddToClassList("stacked-field");
            _directorSettings.Add(button);
            button.RegisterValueChangedCallback(e =>
            {
                DirectorRemember();
                _directorDraft.reactionButton = e.newValue as UnityEngine.UI.Button;
                RefreshDirectorStatus();
            });
            Note(_directorSettings, "버튼의 기존 동작은 유지합니다. 클릭할 때마다 반응 효과를 다시 재생하고, 끝나면 상시 반복을 재개합니다.");
        }

        var effect = DirectorEffects?.ElementAtOrDefault(_directorSelected);
        if (effect == null)
        {
            Note(_directorSettings, "효과 목록에서 편집할 연출을 선택하세요.");
            return;
        }

        Note(_directorSettings, EffectName(effect.kind));
        var times = Row(_directorSettings, "director-field-row");
        DirectorFloat(times, "지속시간 (초)", effect.duration, v => effect.duration = v);
        DirectorFloat(times, "시작 지연 (초)", effect.delay, v => effect.delay = v);
        var mode = new PopupField<string>("실행 방식", new List<string>{"처음부터", "이전 효과와 함께", "이전 효과 다음"}, (int)effect.startMode);
        mode.AddToClassList("stacked-field");
        _directorSettings.Add(mode);
        mode.RegisterValueChangedCallback(_ =>
        {
            DirectorRemember();
            effect.startMode = (UIDirectorView.StartMode)mode.index;
            RefreshDirectorStatus();
        });
        if (effect.kind == UIDirectorView.EffectKind.Move)
        {
            DirectorVector(_directorSettings, "시작 위치 차이", effect.fromOffset, v => effect.fromOffset = v);
            DirectorVector(_directorSettings, "끝 위치 차이", effect.toOffset, v => effect.toOffset = v);
        }
        else
        {
            var values = Row(_directorSettings, "director-field-row");
            if (effect.kind == UIDirectorView.EffectKind.Fade)
            {
                float original = DirectorOriginalAlpha();
                DirectorSlider(values, "시작 투명도", UIDirectorView.ResolveAlpha(original, effect, effect.from), 1,
                    v => SetDirectorAlpha(effect, true, v, original));
                DirectorSlider(values, "끝 투명도", UIDirectorView.ResolveAlpha(original, effect, effect.to), 1,
                    v => SetDirectorAlpha(effect, false, v, original));
                Note(_directorSettings, $"원본 CanvasGroup 투명도: {original:0.###} · 0=안 보임, 1=완전 불투명");
            }
            else if (effect.kind == UIDirectorView.EffectKind.Scale)
            {
                DirectorSlider(values, "시작 크기 배율", effect.from, 5, v => effect.from = v);
                DirectorSlider(values, "끝 크기 배율", effect.to, 5, v => effect.to = v);
            }
            else
            {
                DirectorFloat(values, "시작 각도", effect.from, v => effect.from = v);
                DirectorFloat(values, "끝 각도", effect.to, v => effect.to = v);
            }
            if (effect.kind == UIDirectorView.EffectKind.Scale)
            {
                var peak = new Toggle("중간에 커졌다가 돌아오기")
                {value = effect.usePeak};
                _directorSettings.Add(peak);
                peak.RegisterValueChangedCallback(e =>
                {
                    DirectorRemember();
                    effect.usePeak = e.newValue;
                    DrawDirectorSettings();
                    RefreshDirectorStatus();
                });
                if (effect.usePeak)
                    DirectorSlider(_directorSettings, "중간 크기 배율", effect.peak, 5, v => effect.peak = v);
            }
        }

        var ease = AddButton(_directorSettings, "움직임 곡선: " + effect.ease + "   ·   곡선 선택…", () => UIEditorEaseWindow.Open(effect.ease, effect.duration, value =>
        {
            if (this == null || DirectorEffects == null || !DirectorEffects.Contains(effect))
                return;
            DirectorRemember();
            effect.ease = value;
            DrawDirectorEffects();
            DrawDirectorSettings();
            RefreshDirectorStatus();
        }));
        ease.AddToClassList("director-ease-button");
        Note(_directorSettings, "투명도: 0~1 · 크기: 0~5배 (1=원본 크기)\n이동: 원래 anchoredPosition 대비 차이 · 회전: 원래 Z 대비 각도\n정지·선택 변경·창 닫기 시 복제본은 원상 복구됩니다.");
    }

    /// <summary>복제본의 재생 상태가 아닌 실제 대상에 저장된 투명도를 읽습니다.</summary>
    float DirectorOriginalAlpha()
    {
        var group = _directorOriginal != null ? _directorOriginal.GetComponent<CanvasGroup>() : null;
        return group != null ? Mathf.Clamp01(group.alpha) : 1;
    }

    /// <summary>기존 배율 설정은 편집하는 순간 실제 투명도로 변환하며 반대쪽 값도 유지합니다.</summary>
    static void SetDirectorAlpha(Effect effect, bool start, float value, float original)
    {
        if (!effect.absoluteAlpha)
        {
            effect.from = UIDirectorView.ResolveAlpha(original, effect, effect.from);
            effect.to = UIDirectorView.ResolveAlpha(original, effect, effect.to);
            effect.absoluteAlpha = true;
        }
        if (start) effect.from = Mathf.Clamp01(value);
        else effect.to = Mathf.Clamp01(value);
    }

    /// <summary>슬라이더와 숫자 직접 입력을 모두 지정 범위 안으로 제한합니다.</summary>
    void DirectorSlider(VisualElement parent, string label, float value, float maximum, Action<float> set)
    {
        var slider = new Slider(label, 0, maximum) { showInputField = true };
        slider.SetValueWithoutNotify(Mathf.Clamp(value, 0, maximum));
        slider.AddToClassList("stacked-field");
        slider.AddToClassList("director-number");
        parent.Add(slider);
        slider.RegisterValueChangedCallback(e =>
        {
            float limited = float.IsFinite(e.newValue) ? Mathf.Clamp(e.newValue, 0, maximum) : 0;
            slider.SetValueWithoutNotify(limited);
            DirectorRemember();
            set(limited);
            DrawDirectorEffects();
            RefreshDirectorStatus();
        });
    }

    /// <summary>실수형 연출 설정 필드와 변경 이력을 연결합니다.</summary>
    void DirectorFloat(VisualElement parent, string label, float value, Action<float> set)
    {
        var field = new FloatField(label)
        {value = value};
        field.AddToClassList("stacked-field");
        field.AddToClassList("director-number");
        parent.Add(field);
        field.isDelayed = true;
        field.RegisterValueChangedCallback(e =>
        {
            DirectorRemember();
            set(e.newValue);
            DrawDirectorEffects();
            RefreshDirectorStatus();
        });
    }

    /// <summary>위치 차이를 편집하는 벡터 설정 필드를 구성합니다.</summary>
    void DirectorVector(VisualElement parent, string label, Vector2 value, Action<Vector2> set)
    {
        var field = new Vector2Field(label)
        {value = value};
        field.AddToClassList("stacked-field");
        parent.Add(field);
        field.RegisterValueChangedCallback(e =>
        {
            DirectorRemember();
            set(e.newValue);
            RefreshDirectorStatus();
        });
    }

#endregion
#region 코드 표시와 검증
    /// <summary>상세 설정과 읽기 전용 연출 코드 화면을 전환합니다.</summary>
    void ShowDirectorCode(bool show)
    {
        Display(_directorSettings, !show);
        Display(_directorCode, show);
        if (show)
            DrawDirectorCode();
    }

    /// <summary>기존 함수 연결과 효과 설정에 대응하는 코드를 표시합니다.</summary>
    void DrawDirectorCode()
    {
        if (_directorCode == null)
            return;
        _directorCode.Clear();
        Note(_directorCode, "읽기 전용 · 기존 함수 연결 + 현재 설정의 DOTween 코드");
        var selected = _directorMethods.FirstOrDefault(m => m.key == (_directorExtraTrack == 1 ? _directorDraft?.loopMethod : _directorExtraTrack == 2 ? _directorDraft?.reactionMethod : _directorExit ? _directorDraft?.exitMethod : _directorDraft?.enterMethod));
        string body = "View와 등장·퇴장 함수를 지정하세요.";
        if (_directorExtraTrack > 0)
            body = _directorExtraTrack == 1 ? "// UIDirectorView가 대상의 활성 상태에 맞춰 재생합니다.\nsequence.SetLoops(-1, LoopType." + (_directorDraft?.loopYoyo == true ? "Yoyo" : "Restart") + ");" : "// 기존 버튼 이벤트에 이 View의 콜백만 추가합니다.\nbutton.onClick.AddListener(() => director.PlayReaction(binding));\n// 비활성 시 추가한 콜백만 해제합니다.";
        if (selected != null)
        {
            try
            {
                string rewritten = UIDirectorSource.Rewrite(selected, _directorDraft.enterMethod == _directorDraft.exitMethod ? null : !_directorExit);
                var located = UIDirectorSource.Locate(selected.info, selected.path, rewritten);
                body = "// " + selected.path + "\n// " + selected.label + "\n" + rewritten.Substring(located.open, located.close - located.open + 1);
            }
            catch (Exception ex)
            {
                body = ex.Message;
            }
        }

        var text = new TextField{multiline = true, isReadOnly = true, value = body};
        text.AddToClassList("director-source");
        _directorCode.Add(text);
        Note(_directorCode, "아래는 저장된 설정을 같은 BuildSequence 함수가 해석하는 코드입니다. 효과를 수정해도 새 함수를 계속 만들지 않습니다.");
        var list = DirectorEffects;
        if (list == null)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            var effect = list[i];
            string code = $"// 효과 {i + 1}: {EffectName(effect.kind)} · {(effect.enabled ? "사용" : "꺼짐")}\n" + EffectCode(effect);
            var line = new TextField{multiline = true, isReadOnly = true, value = code};
            line.AddToClassList("director-source");
            line.EnableInClassList("director-highlight", i == _directorSelected);
            _directorCode.Add(line);
        }

        AddButton(_directorCode, "실제 공통 DOTween 함수 소스 열기", () =>
        {
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/02.Scripts/Views/0.Common/UIDirectorView.cs");
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
        });
    }

    /// <summary>효과 설정을 설명하는 DOTween 코드 문자열을 만듭니다.</summary>
    static string EffectCode(Effect e)
    {
        string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "f";
        string ease = ".SetEase(Ease." + e.ease + ")";
        return e.kind switch
        {
        UIDirectorView.EffectKind.Fade => $"group.DOFade({(e.absoluteAlpha ? F(Mathf.Clamp01(e.to)) : "baseAlpha * " + F(Mathf.Clamp01(e.to)))}, {F(e.duration)})\n    .From({(e.absoluteAlpha ? F(Mathf.Clamp01(e.from)) : "baseAlpha * " + F(Mathf.Clamp01(e.from)))}, false){ease};", UIDirectorView.EffectKind.Scale when e.usePeak => $"DOTween.Sequence()\n .Append(rect.DOScale(baseScale * {F(e.peak)}, {F(e.duration * .6f)})\n  .From(baseScale * {F(e.from)}, false){ease})\n .Append(rect.DOScale(baseScale * {F(e.to)}, {F(e.duration * .4f)}){ease});", UIDirectorView.EffectKind.Scale => $"rect.DOScale(baseScale * {F(e.to)}, {F(e.duration)})\n .From(baseScale * {F(e.from)}, false){ease};", UIDirectorView.EffectKind.Move => $"rect.DOAnchorPos(basePosition + new Vector2({F(e.toOffset.x)}, {F(e.toOffset.y)}), {F(e.duration)})\n .From(basePosition + new Vector2({F(e.fromOffset.x)}, {F(e.fromOffset.y)}), false){ease};", _ => $"rect.DOLocalRotate(baseAngles + Vector3.forward * {F(e.to)}, {F(e.duration)}, RotateMode.FastBeyond360)\n .From(baseAngles + Vector3.forward * {F(e.from)}, false){ease};"}

        ;
    }

    /// <summary>함수 선택과 효과 시간 및 대상 충돌을 검증합니다.</summary>
    string DirectorValidation()
    {
        if (_directorDraft == null || _directorOriginal == null)
            return "Canvas 내부 UI를 선택하세요.";
        if (_directorOwner == null)
            return "제어 View를 선택하세요.";
        if (EditorUtility.IsPersistent(_directorOwner) || EditorUtility.IsPersistent(_directorOriginal))
            return "프리팹 편집 모드에서 열어 적용하세요.";
        if (_directorOwner.gameObject.scene != _directorOriginal.gameObject.scene)
            return "같은 씬 또는 같은 프리팹 안의 View를 지정하세요.";
        foreach (bool exit in new[]{false, true})
        {
            var list = exit ? _directorDraft.exit : _directorDraft.enter;
            string error = UIDirectorView.Validate(list);
            if (error != null)
                return error;
            string key = exit ? _directorDraft.exitMethod : _directorDraft.enterMethod;
            if (!list.Any(e => e.enabled))
                continue;
            var method = _directorMethods.FirstOrDefault(m => m.key == key);
            if (method == null)
                return (exit ? "퇴장" : "등장") + " 함수를 선택하세요.";
            if (!method.Supported)
                return method.reason;
        }

        foreach (var extra in new[]{_directorDraft.loop, _directorDraft.reaction})
        {
            string error = UIDirectorView.Validate(extra);
            if (error != null)
                return error;
        }

        if (_directorDraft.reaction.Any(e => e.enabled) && string.IsNullOrEmpty(_directorDraft.reactionMethod) && _directorDraft.reactionButton == null)
            return "상호작용 반응을 받을 버튼을 선택하세요.";
        if (_directorDraft.reactionButton != null && _directorDraft.reactionButton.gameObject.scene != _directorOriginal.gameObject.scene)
            return "같은 씬의 버튼을 지정하세요.";
        if (_directorDraft.enterMethod == _directorDraft.exitMethod && !string.IsNullOrEmpty(_directorDraft.enterMethod) && string.IsNullOrEmpty(_directorMethods.FirstOrDefault(m => m.key == _directorDraft.enterMethod)?.boolArgument))
            return "등장·퇴장을 같은 함수로 연결하려면 bool 인자 하나가 필요합니다.";
        if (_target.GetComponentsInChildren<UIDirectorView>(true).SelectMany(d => d.Bindings).Any(b => b.owner == _directorOwner && b.id != _directorDraft.id && b.target != null && b.target != _directorOriginal && (b.target.IsChildOf(_directorOriginal) || _directorOriginal.IsChildOf(b.target))))
            return "상위·하위 UI에 중복 연출이 연결되어 있습니다. 대상이 겹치지 않게 지정하세요.";
        foreach (var key in new[]{_directorDraft.loopMethod, _directorDraft.reactionMethod}.Where(k => !string.IsNullOrEmpty(k)))
        {
            var method = _directorMethods.FirstOrDefault(m => m.key == key);
            if (method == null)
                return "상시 반복·상호작용 반응 함수를 다시 지정하세요.";
            if (!method.Supported)
                return method.reason;
            if (key == _directorDraft.enterMethod || key == _directorDraft.exitMethod)
                return "상시 반복·상호작용 반응은 등장·퇴장과 다른 함수를 지정하세요.";
        }

        if (!string.IsNullOrEmpty(_directorDraft.loopMethod) && _directorDraft.loopMethod == _directorDraft.reactionMethod)
            return "상시 반복과 상호작용 반응은 서로 다른 함수를 지정하세요.";
        return null;
    }

    /// <summary>연출 요약과 적용 가능 여부 및 저장 상태를 갱신합니다.</summary>
    void RefreshDirectorStatus()
    {
        RefreshDirectorLibraryState();
        if (_directorStatus == null)
            return;
        var presets = DirectorPresetNames();
        if (_directorPreset != null && !_directorPreset.choices.SequenceEqual(presets))
        {
            _directorPreset.choices = presets;
            _directorPreset.index = 0;
        }

        _directorPrefab?.SetEnabled(!IsTestCanvas && _target != null && !string.IsNullOrEmpty(PrefabPath()));
        int count = DirectorEffects?.Count ?? 0;
        float total = 0, previousStart = 0, previousEnd = 0;
        foreach (var e in DirectorEffects ?? new List<Effect>())
            if (e.enabled)
            {
                float start = UIDirectorView.Start(e, previousStart, previousEnd);
                previousStart = start;
                previousEnd = start + Mathf.Max(.01f, e.duration);
                total = Mathf.Max(total, previousEnd);
            }

        _directorSummary.text = DirectorTrackName + $" · {(_directorExtraTrack == 1 ? "1회" : "전체")} {total:0.00}초 · 효과 {count}개";
        int activeTrack = _directorExtraTrack > 0 ? _directorExtraTrack + 1 : _directorExit ? 1 : 0;
        foreach (var button in rootVisualElement.Query<Button>(className: "director-type-button").ToList())
            button.EnableInClassList("selected-tab", button.name == "director-track-" + activeTrack);
        string error = DirectorValidation();
        _directorApply.text = IsTestCanvas ? "테스트 UI · 저장 불가" : "변경 적용";
        _directorStatus.text = IsTestCanvas ? "테스트 UI · 미리보기 전용 / 저장 불가" : error ?? (_directorDirty ? "미적용 연출 변경 있음 · 기존 함수/버튼 연결 유지" : "저장된 연출 또는 빈 구성 · 원본 버튼 이벤트 유지");
        _directorApply.SetEnabled(CanSaveSource() && _directorDirty && error == null);
        _directorUndo.SetEnabled(_directorHistory.Count > 0);
        hasUnsavedChanges = !IsTestCanvas && (ResourceChangeCount > 0 || _directorDirty);
        saveChangesMessage = "아직 반영하지 않은 UI 변경이 있습니다.";
        DrawDirectorCode();
    }

    /// <summary>연출 편집 안내를 표시하고 콘솔에 기록합니다.</summary>
    void DirectorMessage(string text)
    {
        if (_directorStatus != null)
            _directorStatus.text = text;
        Debug.Log("[UIDirector] " + text);
    }

#endregion
#region 초기화와 원본 적용
    /// <summary>선택 효과 또는 유형 전체를 초기화하는 메뉴를 엽니다.</summary>
    void OpenDirectorReset()
    {
        if (_directorDraft == null)
            return;
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("선택한 효과 삭제"), false, () =>
        {
            if (DirectorEffects.Count == 0)
                return;
            DirectorRemember();
            DirectorEffects.RemoveAt(_directorSelected);
            DirectorResetFinished();
        });
        menu.AddItem(new GUIContent("현재 방향 초기화"), false, () =>
        {
            DirectorRemember();
            DirectorEffects.Clear();
            DirectorResetFinished();
        });
        menu.AddItem(new GUIContent("모든 연출 초기화"), false, () =>
        {
            if (!EditorUtility.DisplayDialog("UIDirector", "등장·퇴장·상시 반복·상호작용 반응을 모두 초기화할까요? 함수 선택과 기존 버튼 이벤트는 유지됩니다.", "초기화", "취소"))
                return;
            DirectorRemember();
            _directorDraft.enter.Clear();
            _directorDraft.exit.Clear();
            _directorDraft.loop.Clear();
            _directorDraft.reaction.Clear();
            DirectorResetFinished();
        });
        menu.ShowAsContext();
    }

    /// <summary>초기화 뒤 효과와 설정 및 코드 화면을 갱신합니다.</summary>
    void DirectorResetFinished()
    {
        _directorSelected = 0;
        DrawDirectorEffects();
        DrawDirectorSettings();
        RefreshDirectorStatus();
    }

    /// <summary>기존 함수 연결과 직렬화된 연출 설정을 원본 View에 반영합니다.</summary>
    void ApplyDirector()
    {
        if (!CanSaveSource())
            return;
        StopDirectorPreview();
        string error = DirectorValidation();
        if (error != null)
        {
            DirectorMessage(error);
            return;
        }

        var enter = _directorMethods.FirstOrDefault(m => m.key == _directorDraft.enterMethod);
        var exit = _directorMethods.FirstOrDefault(m => m.key == _directorDraft.exitMethod);
        var loop = _directorDraft.loop.Any(e => e.enabled) ? _directorMethods.FirstOrDefault(m => m.key == _directorDraft.loopMethod) : null;
        var reaction = _directorDraft.reaction.Any(e => e.enabled) ? _directorMethods.FirstOrDefault(m => m.key == _directorDraft.reactionMethod) : null;
        var files = new Dictionary<string, string>();
        var before = new Dictionary<string, byte[]>();
        int group = -1;
        try
        {
            // 초기화 시 연결 코드는 남겨 다른 씬·프리팹의 같은 View 연결을 보존합니다.
            if (_directorDraft.enter.Any(e => e.enabled) || _directorDraft.exit.Any(e => e.enabled) || loop != null || reaction != null)
                files = UIDirectorSource.Prepare(_directorOwner, enter, exit, loop, reaction);
            foreach (var pair in files)
                before[pair.Key] = File.ReadAllBytes(pair.Key);
            _applying = true;
            AssetDatabase.StartAssetEditing();
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply UIDirector Animation");
            var driver = _directorOwner.GetComponent<UIDirectorView>();
            if (driver == null)
                driver = Undo.AddComponent<UIDirectorView>(_directorOwner.gameObject);
            Undo.RecordObject(driver, "Apply UIDirector Animation");
            foreach (var previous in _target.GetComponentsInChildren<UIDirectorView>(true).Where(d => d != driver && d.Bindings.Any(b => b.id == _directorDraft.id || b.target == _directorOriginal)))
            {
                Undo.RecordObject(previous, "Move UIDirector Binding");
                previous.Bindings.RemoveAll(b => b.id == _directorDraft.id || b.target == _directorOriginal);
                EditorUtility.SetDirty(previous);
                PrefabUtility.RecordPrefabInstancePropertyModifications(previous);
            }

            if ((_directorDraft.blockInput || _directorDraft.enter.Concat(_directorDraft.exit).Concat(_directorDraft.loop).Concat(_directorDraft.reaction).Any(e => e.kind == UIDirectorView.EffectKind.Fade)) && _directorOriginal.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(_directorOriginal.gameObject);
            // 같은 대상의 설정 한 건만 갱신하며 함수와 이벤트를 중복 추가하지 않습니다.
            _directorDraft.owner = _directorOwner;
            driver.Bindings.RemoveAll(b => b.id == _directorDraft.id || b.target == _directorOriginal);
            driver.Bindings.Add(CopyBinding(_directorDraft));
            UIDirectorSource.Write(files);
            EditorUtility.SetDirty(driver);
            PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
            EditorSceneManager.MarkSceneDirty(driver.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            _directorDirty = false;
            _directorHistory.Clear();
            DirectorMessage("연출 적용 완료 · 씬/열린 프리팹은 Unity 저장 기능으로 저장하세요. 기존 버튼 연결은 유지했습니다.");
            Debug.Log("[UIDirector] 연출 설정 적용 완료: " + _directorOriginal.name + " (기존 버튼 이벤트 변경 없음)", _directorOriginal);
        }
        catch (Exception ex)
        {
            foreach (var pair in before)
                File.WriteAllBytes(pair.Key, pair.Value);
            if (group >= 0)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
            }

            _directorDirty = true;
            DirectorMessage("적용 실패로 이번 변경을 복원했습니다. " + ex.Message);
            Debug.LogException(ex);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            _applying = false;
            AssetDatabase.Refresh();
            RefreshDirectorStatus();
        }
    }

#endregion
#region 복제본 연출 재생
    /// <summary>복제본의 상태를 보관하고 연출 시퀀스를 준비합니다.</summary>
    void PrepareDirectorPreview()
    {
        StopDirectorPreview();
        if (_picked is not RectTransform rect || DirectorEffects == null || DirectorEffects.Count == 0)
            return;
        string error = UIDirectorView.Validate(DirectorEffects);
        if (error != null)
        {
            DirectorMessage(error);
            return;
        }

        RestorePreviewVisibility();
        // 복제본의 중립 CanvasGroup은 창을 닫을 때 함께 제거합니다. 매번 추가/삭제하면
        // PreviewScene의 UI 렌더 등록이 끊길 수 있으므로 정지할 때는 값만 복원합니다.
        if (rect.GetComponent<CanvasGroup>() == null)
        {
            rect.gameObject.AddComponent<CanvasGroup>();
            if (_canvas != null)
            {
                _canvas.enabled = false;
                _canvas.enabled = true;
            }
        }

        _directorPose = new UIDirectorView.Pose(rect);
        _directorSequence = UIDirectorView.BuildSequence(rect, DirectorEffects, _directorPose);
        if (_directorSequence == null)
            return;
        _directorSequence.SetUpdate(UpdateType.Manual);
        _directorSequence.ForceInit();
        _directorSequence.Pause();
        _directorTotal = _directorSequence.Duration();
        _directorStarted = EditorApplication.timeSinceStartup;
        _renderNeeded = true;
    }

    /// <summary>선택한 연출을 복제본에서 미리 재생합니다.</summary>
    void PlayDirector()
    {
        PrepareDirectorPreview();
        _directorPlaying = _directorSequence != null;
    }

    /// <summary>미리보기 시퀀스를 중지하고 복제본의 원래 표시를 복원합니다.</summary>
    void StopDirectorPreview()
    {
        _directorPlaying = false;
        _directorSequence?.Kill(false);
        _directorSequence = null;
        _directorPose?.Restore();
        _directorPose = null;
        if (_canvas != null)
            foreach (var graphic in _canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.SetAllDirty();
        _directorScrub?.SetValueWithoutNotify(0);
        UpdateDirectorTime(0);
        _renderNeeded = true;
    }

    /// <summary>편집기 시간에 맞춰 연출 미리보기를 진행합니다.</summary>
    void DirectorTick()
    {
        if (!_directorPlaying || _directorSequence == null || _mode != ToolMode.UIDirector)
            return;
        float elapsed = (float)(EditorApplication.timeSinceStartup - _directorStarted);
        if (_directorExtraTrack == 1 && _directorTotal > 0)
        {
            int cycle = Mathf.FloorToInt(elapsed / _directorTotal);
            elapsed %= _directorTotal;
            if (_directorDraft.loopYoyo && cycle % 2 == 1)
                elapsed = _directorTotal - elapsed;
            _directorSequence.Goto(elapsed, false);
            _directorScrub.SetValueWithoutNotify(elapsed / _directorTotal);
            UpdateDirectorTime(elapsed);
            _renderNeeded = true;
            return;
        }

        if (elapsed > _directorTotal && _directorLoop)
        {
            _directorStarted = EditorApplication.timeSinceStartup;
            elapsed = 0;
        }

        elapsed = Mathf.Min(elapsed, _directorTotal);
        _directorSequence.Goto(elapsed, false);
        _directorScrub.SetValueWithoutNotify(_directorTotal > 0 ? elapsed / _directorTotal : 1);
        UpdateDirectorTime(elapsed);
        _renderNeeded = true;
        if (elapsed >= _directorTotal && !_directorLoop)
            _directorPlaying = false;
    }

    /// <summary>재생 시간과 재생 위치 조작부를 갱신합니다.</summary>
    void UpdateDirectorTime(float elapsed)
    {
        if (_directorTime != null)
            _directorTime.text = $"{elapsed:0.00} / {_directorTotal:0.00}초";
    }

    /// <summary>연출 미리보기와 편집기 갱신 이벤트를 해제합니다.</summary>
    void ReleaseDirector()
    {
        CloseDirectorPresetDialog();
        StopDirectorPreview();
        EditorApplication.update -= DirectorTick;
    }
#endregion
}
