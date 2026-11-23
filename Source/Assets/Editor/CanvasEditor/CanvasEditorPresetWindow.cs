using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>연출 불러오기와 저장하기를 각각 표시하는 편집기 보조창입니다.</summary>
public sealed class CanvasEditorPresetWindow : EditorWindow
{
    [SerializeField] CanvasEditor _owner;
    [SerializeField] bool _save;
    [SerializeField] RectTransform _target;
    [SerializeField] int _track;
    [SerializeField] bool _exit;
    internal bool ContextValid => _owner != null && _owner.MatchesPresetContext(_target, _track, _exit);

    /// <summary>연결한 편집기와 현재 대상을 기록하여 다른 UI에 잘못 적용하지 않도록 합니다.</summary>
    internal void Initialize(CanvasEditor owner, bool save, RectTransform target, int track, bool exit)
    {
        _owner = owner; _save = save; _target = target; _track = track; _exit = exit;
        titleContent = new GUIContent(save ? "연출 저장하기" : "연출 불러오기");
        minSize = new Vector2(600, 280);
        position = new Rect(owner.position.x + 80, owner.position.y + 90, 740, 330);
    }

    /// <summary>보조창 용도에 맞는 저장 또는 불러오기 설정만 구성합니다.</summary>
    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear(); root.AddToClassList("canvas-editor");
        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/CanvasEditor/CanvasEditor.uss");
        if (sheet != null) root.styleSheets.Add(sheet);
        root.style.paddingTop = root.style.paddingBottom = 10;
        root.style.paddingLeft = root.style.paddingRight = 10;
        if (_owner == null) { root.Add(new Label("Canvas Editor에서 다시 열어 주세요.")); return; }
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        root.Add(scroll);
        _owner.BuildDirectorLibraryDialog(scroll, _save, this);
        var close = new Button(Close) { text = "닫기" };
        close.style.alignSelf = Align.FlexEnd;
        root.Add(close);
        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape) { Close(); e.StopPropagation(); }
        });
    }

    /// <summary>대상 변경이나 원본 창 종료 상태를 안내창에 반영합니다.</summary>
    void OnInspectorUpdate()
    {
        if (_owner == null) { Close(); return; }
        _owner.RefreshDirectorPresetDialog();
    }

    /// <summary>닫힌 창의 UI 참조를 원본 편집기에 남기지 않습니다.</summary>
    void OnDisable() { if (_owner != null) _owner.ReleaseDirectorPresetDialog(this); }
}
