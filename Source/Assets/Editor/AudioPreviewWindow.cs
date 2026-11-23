using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class AudioPreviewWindow : EditorWindow
{
    [Header("미리보기 오디오")]
    [SerializeField] AudioClip _clip;
    [SerializeField] bool _loop;

    // Inspector와 같은 에디터 전용 재생 기능을 사용한다. 씬에 AudioSource를 만들지 않는다.
    static readonly Type AudioUtilType = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    static readonly BindingFlags AudioFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly MethodInfo PlayMethod = AudioUtilType?.GetMethod("PlayPreviewClip", AudioFlags,
        null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
    static readonly MethodInfo StopMethod = AudioUtilType?.GetMethod("StopAllPreviewClips", AudioFlags);
    static readonly MethodInfo PlayingMethod = AudioUtilType?.GetMethod("IsPreviewClipPlaying", AudioFlags);
    static readonly MethodInfo PositionMethod = AudioUtilType?.GetMethod("GetPreviewClipPosition", AudioFlags);
    static readonly MethodInfo LoopMethod = AudioUtilType?.GetMethod("LoopPreviewClip", AudioFlags);
    static readonly MethodInfo ShowSelectorMethod = typeof(ObjectField).GetMethod("ShowObjectSelector",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    ObjectField _clipField;
    ToolbarButton _playButton;
    ToolbarButton _stopButton;
    Label _details;
    Label _status;
    HelpBox _error;
    Label _path;
    IVisualElementScheduledItem _refresh;
    bool _ownsPreview;

    bool IsSupported => PlayMethod != null && StopMethod != null && PlayingMethod != null
        && PositionMethod != null && LoopMethod != null;

    [MenuItem("Tools/Audio Preview")]
    public static void OpenWindow()
    {
        var window = GetWindow<AudioPreviewWindow>("오디오 미리보기");
        window.minSize = new Vector2(420f, 230f);
        window.Show();
    }

    public void CreateGUI()
    {
        _refresh?.Pause();
        rootVisualElement.Clear();
        rootVisualElement.Add(new HelpBox(
            "오디오 에셋 선택칸을 클릭하면 프로젝트 전체 AudioClip 목록이 열립니다. 선택한 뒤 재생하세요.",
            HelpBoxMessageType.Info));

        _clipField = new ObjectField("오디오 에셋")
        {
            objectType = typeof(AudioClip),
            allowSceneObjects = false,
            value = _clip,
            tooltip = "클릭해서 프로젝트의 모든 효과음을 검색하고 선택하세요. 드래그로 넣어도 됩니다."
        };
        _clipField.Q(className: ObjectField.inputUssClassName)?.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0 || ShowSelectorMethod == null) return;
            ShowSelectorMethod.Invoke(_clipField, null);
            evt.StopImmediatePropagation();
        }, TrickleDown.TrickleDown);
        _clipField.RegisterValueChangedCallback(evt =>
        {
            StopPreview();
            _clip = evt.newValue as AudioClip;
            SetError(string.Empty);
            RefreshDisplay();
        });
        rootVisualElement.Add(_clipField);
        var selectionButton = new Button(() =>
        {
            if (Selection.activeObject is AudioClip selected) _clipField.value = selected;
            else SetError("Project 창에서 AudioClip 에셋을 먼저 선택하세요.");
        }) { text = "Project에서 선택한 오디오 가져오기" };
        rootVisualElement.Add(selectionButton);

        var toolbar = new Toolbar();
        _playButton = new ToolbarButton(PlayPreview) { text = "재생 / 처음부터" };
        _stopButton = new ToolbarButton(StopPreview) { text = "정지" };
        var loop = new ToolbarToggle { text = "반복 재생", value = _loop };
        loop.RegisterValueChangedCallback(evt =>
        {
            _loop = evt.newValue;
            if (_ownsPreview && IsSupported) LoopMethod.Invoke(null, new object[] { _loop });
        });
        toolbar.Add(_playButton);
        toolbar.Add(_stopButton);
        toolbar.Add(loop);
        rootVisualElement.Add(toolbar);
        _details = new Label();
        _status = new Label();
        _path = new Label();
        _error = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
        rootVisualElement.Add(_details);
        rootVisualElement.Add(_status);
        rootVisualElement.Add(_path);

        if (!IsSupported) SetError("이 Unity 버전에서는 오디오 미리보기 API를 사용할 수 없습니다.");
        RefreshDisplay();
        _refresh = rootVisualElement.schedule.Execute(RefreshDisplay).Every(100);
    }

    void PlayPreview()
    {
        if (_clip == null || !IsSupported) return;
        try
        {
            StopPreview();
            // Unity의 미리보기 채널을 사용하므로 Inspector에서 듣던 미리보기는 함께 정지한다.
            StopMethod.Invoke(null, null);
            PlayMethod.Invoke(null, new object[] { _clip, 0, _loop });
            _ownsPreview = true;
            SetError(string.Empty);
        }
        catch (Exception exception)
        {
            _ownsPreview = false;
            SetError("오디오 재생 실패: " + (exception.InnerException ?? exception).Message);
        }
        RefreshDisplay();
    }

    void StopPreview()
    {
        if (_ownsPreview && StopMethod != null) StopMethod.Invoke(null, null);
        _ownsPreview = false;
        RefreshDisplay();
    }

    void RefreshDisplay()
    {
        if (_status == null) return;
        if (_clip == null && _ownsPreview)
        {
            StopPreview();
            return;
        }
        bool playing = IsSupported && _ownsPreview && (bool)PlayingMethod.Invoke(null, null);
        if (!playing) _ownsPreview = false;
        _playButton.SetEnabled(_clip != null && IsSupported);
        _stopButton.SetEnabled(playing);
        _details.text = _clip != null
            ? $"길이 {_clip.length:0.00}초 · {_clip.channels}채널 · {_clip.frequency:N0} Hz"
            : "오디오 에셋을 선택하세요.";
        _path.text = _clip != null ? AssetDatabase.GetAssetPath(_clip) : string.Empty;
        _status.text = playing
            ? $"재생 중  {(float)PositionMethod.Invoke(null, null):0.00} / {_clip.length:0.00}초"
            : "정지";
    }

    void SetError(string message)
    {
        _error.text = message;
        if (string.IsNullOrEmpty(message)) _error.RemoveFromHierarchy();
        else if (_error.parent == null) rootVisualElement.Add(_error);
    }

    void OnDisable()
    {
        _refresh?.Pause();
        StopPreview();
    }
}
