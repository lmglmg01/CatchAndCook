using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using U = UnityEngine.UI;

/// <summary>
/// 만들어진 캔버스의 UI 배치를 바탕으로 기존의 리소스들을 교체 하거나
/// 오브젝트 단위로 움직임, 투명도 조절등의 연출을 추가해주는 UI편집기입니다.
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp-Editor", "UIEditor")]
public sealed partial class CanvasEditor : EditorWindow
{
    const string ToolTitle = "Canvas Editor";
    const string StylePath = "Assets/Editor/CanvasEditor/CanvasEditor.uss";
    const string DefaultSamplePath = "Assets/UIEditorPortable/Samples/Canvas_UIDirectorTest.prefab";
    static readonly Vector2Int[] Resolutions = {new(1920, 1080), new(1440, 1080), new(2560, 1080), new(1080, 1920)};

    enum ToolMode
    {
        ResourceChanger,
        UIDirector,
        SpriteTracker
    }

    sealed class Node
    {
        public Transform Original, Copy;
    }

    readonly Dictionary<Transform, Node> _nodes = new();
    readonly HashSet<Transform> _expanded = new();
    readonly Dictionary<Transform, bool> _previewVisibility = new();
    readonly List<Toggle> _previewVisibilityToggles = new();
    readonly Vector3[] _corners = new Vector3[4];

    Canvas _target, _canvas;
    Transform _picked;
    PreviewRenderUtility _preview;
    RenderTexture _texture;

    int _resolution;
    bool _follow = true, _zoomed, _renderNeeded, _help, _applying, _building, _revealSelection = true;
    ToolMode _mode;
    [SerializeField] ToolMode _lastEditingMode = ToolMode.ResourceChanger;
    string _message = "", _filter = "";

    VisualElement _resourcePage, _directorPage, _helpPage, _resourceHelpPage, _directorHelpPage, _frame, _outline, _targetControls;
    IMGUIContainer _previewSurface;
    ScrollView _tree;
    VisualElement _selectedTreeRow;
    Label _targetLabel, _selectionLabel, _status;
    Toggle _followToggle;
    ToolbarSearchField _treeSearch;
    VisualElement _hierarchyActions;
    Button _hierarchyExpandButton;
    Toggle _hierarchyVisibilityToggle;
    Label _hierarchyExpandLabel, _hierarchyVisibilityLabel;
    Button _resourceTab, _directorTab, _helpButton, _undoButton, _redoButton, _applyButton, _discardButton, _prefabButton;

    // Context shortcuts take precedence over global menu shortcuts such as TMP SDF creation.
    sealed class CanvasSelectionShortcutContext : IShortcutContext
    {
        public bool active
        {
            get
            {
                if ((Selection.activeObject is Sprite || Selection.activeObject is Texture2D) &&
                    TryGetTrackerSelection(out var sprite, out _, out _) && sprite != null)
                    return true;
                var selected = Selection.activeGameObject;
                return selected != null && (selected.transform is RectTransform ||
                    selected.GetComponentInParent<Canvas>(true) != null);
            }
        }
    }

    static readonly CanvasSelectionShortcutContext SelectionShortcutContext = new();

    [InitializeOnLoadMethod]
    static void RegisterSelectionShortcutContext()
    {
        ShortcutManager.RegisterContext(SelectionShortcutContext);
        AssemblyReloadEvents.beforeAssemblyReload -= UnregisterSelectionShortcutContext;
        AssemblyReloadEvents.beforeAssemblyReload += UnregisterSelectionShortcutContext;
    }

    static void UnregisterSelectionShortcutContext()
    {
        ShortcutManager.UnregisterContext(SelectionShortcutContext);
    }

#region 창 생명주기와 이벤트
    /// <summary>편집창을 열고 초기 대상과 선택 동작을 연결합니다.</summary>
    [MenuItem("Tools/Canvas Editor", false, 2200)]
    [Shortcut("CanvasEditor/Canvas Editor", typeof(CanvasSelectionShortcutContext), KeyCode.F12, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
    static void Open()
    {
        bool trackSprite = TryGetTrackerSelection(out var sprite, out var source, out var canvas) && sprite != null &&
            (Selection.activeObject is Sprite || Selection.activeObject is Texture2D);
        var window = GetWindow<CanvasEditor>();
        window.titleContent = new GUIContent(ToolTitle);
        window.Show();
        if (trackSprite)
        {
            // CreateGUI가 구성된 뒤 선택 Sprite로 열며 기존 편집 초안은 유지합니다.
            window.rootVisualElement.schedule.Execute(() =>
            {
                if (window != null) window.OpenTrackerShortcut(sprite, source, canvas);
            });
        }
        else
            window.rootVisualElement.schedule.Execute(() =>
            {
                if (window == null) return;
                if (window._mode == ToolMode.SpriteTracker) window.SetMode(window._lastEditingMode);
                window.OpenSelection();
            });
    }

    /// <summary>선택 변경과 편집기 상태 변경 이벤트를 등록합니다.</summary>
    void OnEnable()
    {
        titleContent = new GUIContent(ToolTitle);
        minSize = new Vector2(1000, 730);
        Selection.selectionChanged += FollowSelection;
        Undo.undoRedoPerformed += SourceUndoChanged;
        EditorApplication.hierarchyChanged += SourceChanged;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += ReleasePreview;
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseSpriteTracker;
        EditorApplication.projectChanged += TrackerProjectChanged;
    }

    /// <summary>등록한 이벤트와 미리보기 자원을 해제합니다.</summary>
    void OnDisable()
    {
        Selection.selectionChanged -= FollowSelection;
        Undo.undoRedoPerformed -= SourceUndoChanged;
        EditorApplication.hierarchyChanged -= SourceChanged;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= ReleasePreview;
        AssemblyReloadEvents.beforeAssemblyReload -= ReleaseSpriteTracker;
        EditorApplication.projectChanged -= TrackerProjectChanged;
        ReleaseSpriteTracker();
        ReleaseDirector();
        ReleasePreview();
    }

    /// <summary>창이 사라질 때 남은 미리보기 자원을 정리합니다.</summary>
    void OnDestroy() => ReleasePreview();
    /// <summary>플레이 전환에 맞춰 미리보기를 중지하거나 다시 구성합니다.</summary>
    void PlayModeChanged(PlayModeStateChange state)
    {
        TrackerPlayModeChanged(state);
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            ReleasePreview();
            SetMessage("플레이 중에는 리소스를 변경하지 않습니다. 편집 모드에서 다시 읽어 주세요.");
            RefreshUI();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            rootVisualElement.schedule.Execute(() =>
            {
                if (_target != null)
                    Rebuild(_target);
                else
                    FollowSelection();
            }).StartingIn(100);
    }

    /// <summary>편집창의 화면과 조작부를 구성하고 현재 상태를 반영합니다.</summary>
    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        _previewVisibilityToggles.Clear();
        root.AddToClassList("canvas-editor");
        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
        if (sheet != null && !root.styleSheets.Contains(sheet))
            root.styleSheets.Add(sheet);
        var tabs = Row(root, "mode-tabs");
        _resourceTab = AddButton(tabs, "ResourceChanger", () => SetMode(ToolMode.ResourceChanger));
        _directorTab = AddButton(tabs, "UIDirector", () => SetMode(ToolMode.UIDirector));
        _trackerTab = AddButton(tabs, "Sprite Tracker", () => SetMode(ToolMode.SpriteTracker));
        _trackerTab.name = "sprite-tracker-tab";
        _helpButton = AddButton(tabs, "사용 방법", () =>
        {
            _help = !_help;
            RefreshMode();
        });
        _helpButton.AddToClassList("help-button");
        var codeButton = AddButton(tabs, "코드보기", OpenEditorSource);
        codeButton.name = "open-editor-source";
        codeButton.tooltip = "이 편집창의 CanvasEditor.cs를 외부 코드 편집기로 엽니다.";
        var top = Row(root, "target-toolbar");
        _targetLabel = new Label("Canvas를 선택하세요");
        _targetLabel.AddToClassList("target-name");
        top.Add(_targetLabel);
        _followToggle = new Toggle("선택 따라가기");
        _followToggle.SetValueWithoutNotify(_follow);
        _followToggle.RegisterValueChangedCallback(e =>
        {
            _follow = e.newValue;
            if (_follow)
                FollowSelection();
        });
        _targetControls = Row(top);
        _targetControls.name = "target-controls";
        _targetControls.Add(_followToggle);
        AddButton(_targetControls, "대상 고정", () => _followToggle.value = false);
        AddButton(_targetControls, "다시 읽기", Reload);
        _resourcePage = new VisualElement();
        _resourcePage.AddToClassList("resource-page");
        root.Add(_resourcePage);
        var upper = Row(_resourcePage, "upper-area");
        _resourcePreviewHost = upper;
        var preview = Panel(upper, "01  Canvas 미리보기", "main-panel");
        _previewPanel = preview;
        var hierarchy = Panel(upper, "02  오브젝트 계층", "side-panel");
        BuildPreviewPanel(preview);
        BuildDirectorPlayback(preview);
        var search = new ToolbarSearchField();
        _treeSearch = search;
        search.AddToClassList("tree-search");
        search.RegisterValueChangedCallback(e =>
        {
            _filter = e.newValue;
            DrawTree();
        });
        hierarchy.Add(search);
        _hierarchyActions = Row(hierarchy, "hierarchy-actions");
        var expandGroup = Row(_hierarchyActions, "hierarchy-bulk-group");
        _hierarchyExpandButton = AddButton(expandGroup, "▸", () => SetHierarchyExpanded(!AllHierarchyExpanded()));
        _hierarchyExpandButton.AddToClassList("fold-button");
        _hierarchyExpandButton.tooltip = "계층 전체 펼치기/닫기 · 검색은 해제됩니다.";
        _hierarchyExpandLabel = new Label("모두 펼치기");
        expandGroup.Add(_hierarchyExpandLabel);
        var visibleGroup = Row(_hierarchyActions, "hierarchy-bulk-group");
        visibleGroup.AddToClassList("hierarchy-visibility-group");
        _hierarchyVisibilityToggle = new Toggle();
        _hierarchyVisibilityToggle.AddToClassList("preview-visibility-eye");
        _hierarchyVisibilityToggle.tooltip = "계층 전체 켜기/끄기 · 미리보기만 변경합니다.";
        _hierarchyVisibilityToggle.RegisterValueChangedCallback(e => SetAllPreviewVisible(e.newValue));
        _hierarchyVisibilityLabel = new Label("모두 켜기");
        visibleGroup.Add(_hierarchyVisibilityLabel);
        visibleGroup.Add(_hierarchyVisibilityToggle);
        _tree = new ScrollView(ScrollViewMode.Vertical);
        _tree.AddToClassList("hierarchy-tree");
        hierarchy.Add(_tree);
        _selectionLabel = new Label();
        _selectionLabel.AddToClassList("selection-info");
        hierarchy.Add(_selectionLabel);
        BuildPreviewVisibilityControl(hierarchy);
        _editTextButton = AddButton(hierarchy, "텍스트 편집", OpenTextEditor);
        _editTextButton.name = "edit-selected-text";
        _includeChildren = new Toggle("하위 오브젝트 포함");
        _includeChildren.SetValueWithoutNotify(true);
        _includeChildren.RegisterValueChangedCallback(_ =>
        {
            RefreshSpriteList();
            RefreshTextControl();
        });
        hierarchy.Add(_includeChildren);
        var inactive = new Label("비활성 오브젝트의 참조도 조회합니다.");
        inactive.AddToClassList("secondary-label");
        hierarchy.Add(inactive);
        var lower = Row(_resourcePage, "lower-area");
        BuildResourcePalette(Panel(lower, "03  사용 중인 Sprite", "main-panel"));
        BuildResourceDetails(Panel(lower, "04  리소스 확인 / 교체", "side-panel"));
        var bottom = Row(_resourcePage, "apply-toolbar");
        _status = new Label();
        _status.AddToClassList("status-label");
        bottom.Add(_status);
        _undoButton = AddButton(bottom, "실행 취소", UndoDraft);
        _redoButton = AddButton(bottom, "다시 실행", RedoDraft);
        _discardButton = AddButton(bottom, "변경 버리기", DiscardDraft);
        _applyButton = AddButton(bottom, "변경 적용", ApplyDraft);
        _applyButton.AddToClassList("primary-button");
        _prefabButton = AddButton(bottom, "원본 프리팹 열기", OpenPrefab);
        _directorPage = new VisualElement();
        root.Add(_directorPage);
        BuildDirector(_directorPage);
        var tracker = new VisualElement();
        root.Add(tracker);
        BuildSpriteTracker(tracker);
        BuildHelp(root);
        BuildTrackerHelp(_helpPage);
        RefreshMode();
        if (_follow && SelectedCanvas() != null)
            Rebuild(SelectedCanvas());
        else if (_target != null)
            Rebuild(_target);
        else
            ShowDefaultSample();
        RefreshUI();
        root.schedule.Execute(FollowSelection).StartingIn(100);
        root.schedule.Execute(RequestPreviewRepaint).Every(60);
    }

#endregion

#region 화면 구성과 도움말
    // 현재 편집창의 스크립트를 Unity에 설정된 외부 코드 편집기로 엽니다.
    /// <summary>현재 편집창의 스크립트를 외부 코드 편집기로 엽니다.</summary>
    void OpenEditorSource()
    {
        var script = MonoScript.FromScriptableObject(this);
        if (script == null || !AssetDatabase.OpenAsset(script, 1))
            SetMessage("편집기 스크립트를 열 수 없습니다. Unity의 외부 스크립트 편집기 설정을 확인하세요.");
    }

    /// <summary>화면 비율과 선택 테두리를 포함한 Canvas 미리보기를 구성합니다.</summary>
    void BuildPreviewPanel(VisualElement parent)
    {
        var toolbar = Row(parent, "preview-toolbar");
        var resolution = new PopupField<string>(new List<string>{"16:9", "4:3", "21:9", "9:16"}, _resolution);
        resolution.RegisterValueChangedCallback(_ =>
        {
            _resolution = resolution.index;
            RecreateTexture();
        });
        toolbar.Add(resolution);
        AddButton(toolbar, "맞춤", () => ResetPreviewView(true));
        AddButton(toolbar, "100%", () => ResetPreviewView(false));
        _previewZoomLabel = new Label();
        _previewZoomLabel.AddToClassList("secondary-label");
        toolbar.Add(_previewZoomLabel);
        var reveal = new Toggle("숨김 UI 미리보기");
        reveal.SetValueWithoutNotify(_revealSelection);
        reveal.tooltip = "선택한 오브젝트와 부모만 복제본에서 표시합니다. 원본 활성 상태는 변경하지 않습니다.";
        reveal.RegisterValueChangedCallback(e =>
        {
            _revealSelection = e.newValue;
            RestorePreviewVisibility();
            DrawTree();
        });
        toolbar.Add(reveal);
        var hint = new Label("휠 확대/축소 · Space/휠클릭 드래그");
        hint.AddToClassList("secondary-label");
        toolbar.Add(hint);
        var viewport = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        viewport.AddToClassList("preview-viewport");
        viewport.contentContainer.AddToClassList("preview-content");
        parent.Add(viewport);
        _frame = new VisualElement();
        _frame.AddToClassList("preview-frame");
        viewport.Add(_frame);
        _previewSurface = new IMGUIContainer(DrawPreviewGUI)
        {pickingMode = PickingMode.Ignore};
        _previewSurface.StretchToParentSize();
        _frame.Add(_previewSurface);
        _outline = new VisualElement{pickingMode = PickingMode.Ignore};
        _outline.AddToClassList("selection-outline");
        _frame.Add(_outline);
        viewport.RegisterCallback<GeometryChangedEvent>(_ => FitPreview());
        SetupPreviewNavigation(viewport);
        _frame.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || _canvas == null || _texture == null)
                return;
            var graphic = Hit(e.localPosition);
            SelectNode(graphic != null ? graphic.transform : _canvas.transform);
            e.StopPropagation();
        });
    }

    /// <summary>사용방법의 제목과 설명 문단을 추가합니다.</summary>
    static void Help(VisualElement parent, string title, string text)
    {
        var heading = new Label(title);
        heading.AddToClassList("help-heading");
        parent.Add(heading);
        var content = new Label(System.Text.RegularExpressions.Regex.Replace(text, @"(?<=[.!?])\s+", "\n\n"));
        content.AddToClassList("help-text");
        parent.Add(content);
    }

    /// <summary>가로 배치용 컨테이너를 만들어 부모에 추가합니다.</summary>
    static VisualElement Row(VisualElement parent, string className = null)
    {
        var row = new VisualElement();
        row.AddToClassList("row");
        if (className != null)
            row.AddToClassList(className);
        parent.Add(row);
        return row;
    }

    /// <summary>제목과 도움말 버튼이 있는 영역을 구성합니다.</summary>
    static VisualElement Panel(VisualElement parent, string title, string className)
    {
        var panel = new VisualElement();
        panel.AddToClassList("tool-panel");
        panel.AddToClassList(className);
        parent.Add(panel);
        var header = Row(panel, "panel-header");
        var label = new Label(title);
        label.AddToClassList("panel-title");
        header.Add(label);
        var help = AddButton(header, "?", () => UIEditorHelpWindow.Open(title));
        help.AddToClassList("area-help-button");
        help.tooltip = title + " 사용 방법";
        return panel;
    }

    /// <summary>지정한 동작을 실행하는 버튼을 부모에 추가합니다.</summary>
    static Button AddButton(VisualElement parent, string text, Action action)
    {
        var button = new Button(action)
        {text = text};
        parent.Add(button);
        return button;
    }

    /// <summary>요소의 화면 표시 여부를 변경합니다.</summary>
    static void Display(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>편집 모드를 전환하고 해당 화면과 설정을 갱신합니다.</summary>
    void SetMode(ToolMode mode)
    {
        StopDirectorPreview();
        if (_mode != ToolMode.SpriteTracker) _lastEditingMode = _mode;
        _mode = mode;
        if (mode != ToolMode.SpriteTracker) _lastEditingMode = mode;
        if (mode == ToolMode.SpriteTracker) EnterSpriteTracker();
        RefreshMode();
        RefreshDirector();
    }

    /// <summary>현재 편집 모드에 맞춰 영역과 탭의 표시 상태를 갱신합니다.</summary>
    void RefreshMode()
    {
        Display(_resourcePage, !_help && _mode == ToolMode.ResourceChanger);
        Display(_directorPage, !_help && _mode == ToolMode.UIDirector);
        Display(_trackerPage, !_help && _mode == ToolMode.SpriteTracker);
        Display(_trackerHelpPage, _mode == ToolMode.SpriteTracker);
        Display(_helpPage, _help);
        Display(_targetControls, !_help && _mode != ToolMode.SpriteTracker);
        Display(_targetLabel, !_help && _mode != ToolMode.SpriteTracker);
        Display(_resourceHelpPage, _mode == ToolMode.ResourceChanger);
        Display(_directorHelpPage, _mode == ToolMode.UIDirector);
        _resourceTab?.EnableInClassList("selected-tab", _mode == ToolMode.ResourceChanger);
        _directorTab?.EnableInClassList("selected-tab", _mode == ToolMode.UIDirector);
        _trackerTab?.EnableInClassList("selected-tab", _mode == ToolMode.SpriteTracker);
        _helpButton?.EnableInClassList("selected-tab", _help);
        if (_previewPanel != null && _directorPreviewHost != null)
        {
            if (_mode == ToolMode.UIDirector)
                _directorPreviewHost.Insert(0, _previewPanel);
            else
                _resourcePreviewHost.Insert(0, _previewPanel);
        }

        foreach (var element in _previewPanel?.Query<VisualElement>(className: "director-playback").ToList() ?? new List<VisualElement>())
            Display(element, _mode == ToolMode.UIDirector);
        Display(_directorScrub, _mode == ToolMode.UIDirector);
        FitPreview();
        _renderNeeded = true;
    }

#endregion

#region 대상 선택과 복제본 구성
    /// <summary>현재 선택한 오브젝트가 속한 Canvas를 찾습니다.</summary>
    static Canvas SelectedCanvas()
    {
        var selected = Selection.activeGameObject;
        return selected != null ? selected.GetComponentInParent<Canvas>(true) : null;
    }

    bool IsTestCanvas => IsDefaultSample || (_target != null && _target.GetComponent<UIDirectorTestView>() != null);
    bool IsDefaultSample => _target != null && AssetDatabase.GetAssetPath(_target) == DefaultSamplePath;
    // 기본 화면은 프리팹 에셋의 복제본만 표시하므로 현재 씬에는 오브젝트가 추가되지 않습니다.
    /// <summary>씬을 변경하지 않고 기본 테스트 프리팹을 미리보기로 표시합니다.</summary>
    void ShowDefaultSample()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSamplePath);
        var canvas = prefab != null ? prefab.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            Rebuild(canvas);
            var panel = _canvas != null ? _canvas.transform.Find("AnimatedPanel") : null;
            if (panel != null)
                SelectNode(panel);
        }
        else
            SetMessage("기본 테스트 UI가 없습니다. UIEditorPortable/Samples 폴더를 함께 가져오세요.");
    }

    /// <summary>열기 시점의 선택을 따르거나 기본 테스트 화면을 표시합니다.</summary>
    void OpenSelection()
    {
        if (_resourcePage == null || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        var canvas = SelectedCanvas();
        if (canvas != _target && !CanAbandonDraft())
            return;
        _follow = true;
        _followToggle?.SetValueWithoutNotify(true);
        if (canvas != null)
        {
            Rebuild(canvas);
            FollowSelection();
        }
        else
            ShowDefaultSample();
    }

    /// <summary>Hierarchy 선택을 따라 대상 Canvas와 선택 계층을 갱신합니다.</summary>
    void FollowSelection()
    {
        if (_mode == ToolMode.SpriteTracker) { TrackerFollowSelection(); return; }
        if (_resourcePage == null || EditorApplication.isPlayingOrWillChangePlaymode || (!_follow && _target != null && !IsDefaultSample))
            return;
        var selected = Selection.activeGameObject;
        var canvas = SelectedCanvas();
        if (canvas == null && _target == null)
        {
            ShowDefaultSample();
            return;
        }

        if (canvas != null && canvas != _target)
        {
            if (CanAbandonDraft())
                Rebuild(canvas);
            else
                return;
        }

        if (selected != null && _target != null)
        {
            var node = _nodes.Values.FirstOrDefault(n => n.Original == selected.transform);
            if (node != null && node.Copy != _picked)
                SelectNode(node.Copy);
        }
    }

    /// <summary>적용 전 변경을 버리고 대상을 바꿔도 되는지 확인합니다.</summary>
    bool CanAbandonDraft() => IsTestCanvas || (ResourceChangeCount == 0 || EditorUtility.DisplayDialog(ToolTitle, "반영하지 않은 Sprite·텍스트 변경을 버리시겠습니까?", "변경 버리기", "계속 편집")) && CanDiscardDirector();
    /// <summary>현재 대상의 원본 상태로 미리보기를 다시 구성합니다.</summary>
    void Reload()
    {
        if (_target != null && CanAbandonDraft())
            Rebuild(_target);
        else if (_target == null)
            FollowSelection();
    }

    /// <summary>원본 실행 취소 뒤 미적용 변경이 없으면 미리보기를 갱신합니다.</summary>
    void SourceUndoChanged()
    {
        if (_target != null && !_applying && ResourceChangeCount == 0)
        {
            if (!_directorDirty)
                _directorOriginal = null;
            Rebuild(_target);
        }
    }

    /// <summary>원본 계층 구성이 달라졌는지 확인하고 필요할 때 다시 읽습니다.</summary>
    void SourceChanged()
    {
        if (_building || _applying || _target == null || _canvas == null || ResourceChangeCount > 0 || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (_target.GetComponentsInChildren<Transform>(true).Length != _nodes.Count || _nodes.Values.Any(n => n.Original == null))
            Rebuild(_target);
    }

    /// <summary>원본과 격리된 복제본을 만들어 편집용 미리보기를 구성합니다.</summary>
    void Rebuild(Canvas target)
    {
        Transform selected = _picked != null && _nodes.TryGetValue(_picked, out var selectedNode) ? selectedNode.Original : null;
        _building = true;
        ReleasePreview();
        _changes.Clear();
        _textChanges.Clear();
        _history.Clear();
        _redo.Clear();
        _references.Clear();
        _selectedReference = null;
        _target = target;
        _message = "";
        try
        {
            if (target == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _preview = new PreviewRenderUtility();
            var holder = new GameObject("CanvasEditor Preview")
            {hideFlags = HideFlags.HideAndDontSave};
            holder.SetActive(false);
            _preview.AddSingleGO(holder);
            var copy = Instantiate(target.gameObject, holder.transform, false);
            copy.name = target.name;
            SanitizeCopy(copy);
            var camera = _preview.camera;
            // Preview 카메라는 화면 공간 Canvas를 생략하므로 격리된 복제본을 Game 카메라로 렌더링합니다.
            camera.cameraType = CameraType.Game;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(21, 28, 39, 255);
            camera.transform.SetPositionAndRotation(new Vector3(0, 0, -100), Quaternion.identity);
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1000;
            camera.scene = copy.scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(copy.scene);
            _canvas = copy.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = camera;
            _canvas.planeDistance = 100;
            if (target.renderMode == RenderMode.WorldSpace)
            {
                // 원본 월드 위치·배율은 보존하고 복제본만 정규화해 의자·NPC Canvas도 편집합니다.
                _canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)_canvas.transform;
                rect.localPosition = Vector3.zero; rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
                camera.orthographicSize = Mathf.Max(1f, rect.rect.height * .75f);
                camera.transform.position = rect.TransformPoint(rect.rect.center) - Vector3.forward * 100f;
            }
            MapOriginal(target.transform, copy.transform);
            _expanded.Add(copy.transform);
            _picked = _nodes.Values.FirstOrDefault(n => n.Original == selected)?.Copy ?? copy.transform;
            holder.SetActive(true);
            copy.SetActive(true);
            RestorePreviewVisibility();
            RecreateTexture();
        }
        catch (Exception ex)
        {
            ReleasePreview();
            SetMessage("미리보기를 열 수 없습니다. " + ex.Message);
            Debug.LogException(ex);
        }
        finally
        {
            _building = false;
            RefreshUI();
        }
    }

    /// <summary>복제본의 게임 스크립트와 이벤트 연결을 제거해 안전하게 미리봅니다.</summary>
    static void SanitizeCopy(GameObject copy)
    {
        // 비활성 복제본에서 프로젝트 스크립트와 기존 이벤트 연결을 먼저 제거합니다.
        foreach (var behaviour in copy.GetComponentsInChildren<Behaviour>(true).Reverse())
        {
            if (behaviour == null || behaviour is Canvas || behaviour is CanvasGroup)
                continue;
            var type = behaviour.GetType();
            bool standardUI = type.Assembly == typeof(U.Graphic).Assembly || type.Assembly == typeof(TMP_Text).Assembly;
            if (behaviour is MonoBehaviour && (!standardUI || behaviour is EventSystem || behaviour is BaseInputModule || behaviour is U.GraphicRaycaster || behaviour is U.CanvasScaler))
            {
                DestroyImmediate(behaviour);
                if (behaviour != null)
                    throw new InvalidOperationException($"안전하게 분리할 수 없는 컴포넌트가 있습니다 ({type.Name}).");
                continue;
            }

            if (!standardUI)
            {
                behaviour.enabled = false;
                continue;
            }

            var data = new SerializedObject(behaviour);
            var iterator = data.GetIterator();
            var eventArrays = new List<string>();
            while (iterator.Next(true))
            {
                if (iterator.isArray && iterator.propertyPath.EndsWith(".m_PersistentCalls.m_Calls", StringComparison.Ordinal))
                    eventArrays.Add(iterator.propertyPath);
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var target = iterator.objectReferenceValue;
                    if ((target is Component component && !component.transform.IsChildOf(copy.transform)) || (target is GameObject obj && !obj.transform.IsChildOf(copy.transform)))
                        iterator.objectReferenceValue = null;
                }
            }

            foreach (string path in eventArrays)
                data.FindProperty(path).arraySize = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (behaviour is U.Selectable selectable)
            {
                if (selectable.transition == U.Selectable.Transition.Animation)
                    selectable.transition = U.Selectable.Transition.None;
                var navigation = selectable.navigation;
                navigation.mode = U.Navigation.Mode.None;
                selectable.navigation = navigation;
                var colors = selectable.colors;
                colors.fadeDuration = 0;
                selectable.colors = colors;
            }
        }

        foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
    }

#endregion

#region 미리보기 렌더링
    /// <summary>원본 CanvasScaler 설정으로 미리보기 배율을 계산합니다.</summary>
    float PreviewScale(Vector2Int resolution)
    {
        var scaler = _target.GetComponent<U.CanvasScaler>();
        if (scaler == null || !scaler.isActiveAndEnabled)
            return _target.scaleFactor;
        _canvas.referencePixelsPerUnit = scaler.referencePixelsPerUnit;
        if (scaler.uiScaleMode != U.CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return _target.scaleFactor;
        var reference = scaler.referenceResolution;
        if (reference.x <= 0 || reference.y <= 0)
            return 1;
        float x = resolution.x / reference.x;
        float y = resolution.y / reference.y;
        return scaler.screenMatchMode switch
        {
        U.CanvasScaler.ScreenMatchMode.Expand => Mathf.Min(x, y), U.CanvasScaler.ScreenMatchMode.Shrink => Mathf.Max(x, y), _ => Mathf.Pow(2, Mathf.Lerp(Mathf.Log(x, 2), Mathf.Log(y, 2), scaler.matchWidthOrHeight))}

        ;
    }

    /// <summary>선택한 화면 비율에 맞춰 미리보기 표시 크기를 조정합니다.</summary>
    void FitPreview()
    {
        if (_frame == null || _frame.parent == null)
            return;
        var viewport = _frame.GetFirstAncestorOfType<ScrollView>();
        if (viewport == null)
            return;
        float maxWidth = viewport.layout.width;
        float maxHeight = viewport.layout.height;
        if (float.IsNaN(maxWidth) || float.IsNaN(maxHeight) || maxWidth <= 0 || maxHeight <= 0)
            return;
        Vector2Int resolution = Resolutions[_resolution];
        float width = _zoomed ? resolution.x * _previewZoom : Mathf.Min(maxWidth - 18, (maxHeight - 18) * resolution.x / resolution.y);
        _frame.style.width = Mathf.Max(1, width);
        _frame.style.height = Mathf.Max(1, width * resolution.y / resolution.x);
        RefreshPreviewZoomLabel();
        _renderNeeded = true;
    }

    /// <summary>선택한 RectTransform의 미리보기 화면상 경계를 계산합니다.</summary>
    Rect OutlineBounds(RectTransform rect)
    {
        rect.GetWorldCorners(_corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in _corners)
        {
            var screen = _preview.camera.WorldToViewportPoint(corner);
            var local = new Vector2(screen.x * _frame.layout.width, (1 - screen.y) * _frame.layout.height);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>미리보기에서 클릭한 위치의 최상단 UI 그래픽을 찾습니다.</summary>
    U.Graphic Hit(Vector2 local)
    {
        if (_texture == null)
            return null;
        Vector2 screen = ScreenPoint(local);
        U.Graphic hit = null;
        int bestOrder = int.MinValue;
        int bestDepth = int.MinValue;
        foreach (var graphic in _canvas.GetComponentsInChildren<U.Graphic>())
        {
            if (!graphic.isActiveAndEnabled || graphic.depth < 0 || !RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, screen, _preview.camera))
                continue;
            var canvas = graphic.canvas;
            int order = canvas != null && canvas.overrideSorting ? canvas.sortingOrder : 0;
            if (order < bestOrder || (order == bestOrder && graphic.depth < bestDepth))
                continue;
            hit = graphic;
            bestOrder = order;
            bestDepth = graphic.depth;
        }

        return hit;
    }

    /// <summary>원본과 복제본의 계층별 대응 관계를 기록합니다.</summary>
    void MapOriginal(Transform original, Transform copy)
    {
        _nodes.Add(copy, new Node{Original = original, Copy = copy});
        for (int i = 0; i < original.childCount; i++)
            MapOriginal(original.GetChild(i), copy.GetChild(i));
    }

    /// <summary>복제본과 렌더링 자원 및 임시 표시 상태를 해제합니다.</summary>
    void ReleasePreview()
    {
        ClearPreviewPan();
        StopDirectorPreview();
        _nodes.Clear();
        _expanded.Clear();
        _previewVisibility.Clear();
        _picked = null;
        _canvas = null;
        if (_preview != null)
        {
            _preview.Cleanup();
            _preview = null;
        }

        _texture = null;
    }

    /// <summary>화면 크기 변경에 맞춰 미리보기 렌더링을 다시 준비합니다.</summary>
    void RecreateTexture()
    {
        if (_preview == null)
            return;
        _texture = null;
        FitPreview();
        _renderNeeded = true;
    }

    /// <summary>미리보기 변경이 있을 때만 다시 그리기를 요청합니다.</summary>
    void RequestPreviewRepaint()
    {
        if (_renderNeeded && !_help && _preview != null)
            _previewSurface?.MarkDirtyRepaint();
    }

    /// <summary>복제 Canvas를 렌더링하고 미리보기 영역에 그립니다.</summary>
    void DrawPreviewGUI()
    {
        if (Event.current.type != EventType.Repaint || _preview == null || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (_renderNeeded)
        {
            _renderNeeded = false;
            try
            {
                var resolution = Resolutions[_resolution];
                _preview.BeginPreview(new Rect(0, 0, resolution.x / 2f, resolution.y / 2f), GUIStyle.none);
                try
                {
                    var target = _preview.camera.targetTexture;
                    _preview.camera.aspect = (float)target.width / target.height;
                    _canvas.scaleFactor = _target.renderMode == RenderMode.WorldSpace ? 1f : PreviewScale(resolution) * target.height / resolution.y;
                    Canvas.ForceUpdateCanvases();
                    _preview.Render(true, false);
                }
                finally
                {
                    _texture = _preview.EndPreview() as RenderTexture;
                }

                DrawOutline();
            }
            catch (Exception ex)
            {
                _texture = null;
                SetMessage("미리보기를 그리지 못했습니다. " + ex.Message);
                Debug.LogException(ex);
            }
        }

        if (_texture != null)
            GUI.DrawTexture(new Rect(0, 0, _frame.layout.width, _frame.layout.height), _texture, ScaleMode.StretchToFill, false);
    }

    /// <summary>미리보기 좌표를 렌더 텍스처의 화면 좌표로 변환합니다.</summary>
    Vector2 ScreenPoint(Vector2 local) => new(local.x / _frame.layout.width * _texture.width, (1 - local.y / _frame.layout.height) * _texture.height);
    /// <summary>선택한 오브젝트의 표시 상태에 맞춰 선택 테두리를 갱신합니다.</summary>
    void DrawOutline()
    {
        var rect = _picked as RectTransform;
        bool visible = rect != null && rect.gameObject.activeInHierarchy && _canvas != null && rect != _canvas.transform;
        Display(_outline, visible);
        if (!visible)
            return;
        Rect bounds = OutlineBounds(rect);
        _outline.style.left = bounds.x;
        _outline.style.top = bounds.y;
        _outline.style.width = bounds.width;
        _outline.style.height = bounds.height;
    }

#endregion

#region 계층 선택과 표시 제어
    /// <summary>현재 대상의 계층을 펼침 상태에 맞춰 다시 표시합니다.</summary>
    void DrawTree()
    {
        if (_tree == null)
            return;
        RefreshHierarchyActions();
        _selectedTreeRow = null;
        _tree.Clear();
        if (_canvas == null)
        {
            _tree.Add(new Label("Hierarchy에서 화면 공간 Canvas를 선택하세요."));
            return;
        }

        if (_filter.Trim().Length > 0)
        {
            foreach (var item in _canvas.GetComponentsInChildren<Transform>(true))
                if (item.name.IndexOf(_filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    AddTreeRow(item, 0, false);
        }
        else
            AddTreeBranch(_canvas.transform, 0);
    }

    /// <summary>모든 하위 계층이 펼쳐져 있는지 확인합니다.</summary>
    bool AllHierarchyExpanded() => _canvas != null && _canvas.GetComponentsInChildren<Transform>(true).Where(t => t.childCount > 0).All(t => _expanded.Contains(t));
    /// <summary>대응되는 모든 미리보기 오브젝트가 켜져 있는지 확인합니다.</summary>
    bool AllPreviewVisible() => _canvas != null && _nodes.Values.Where(n => n.Copy != null).All(n => n.Copy.gameObject.activeSelf);
    /// <summary>현재 계층 상태에 맞춰 두 일괄 버튼의 이름을 갱신합니다.</summary>
    void RefreshHierarchyActions()
    {
        _hierarchyActions?.SetEnabled(_canvas != null);
        bool expanded = AllHierarchyExpanded();
        bool visible = AllPreviewVisible();
        if (_hierarchyExpandButton != null)
            _hierarchyExpandButton.text = expanded ? "▾" : "▸";
        if (_hierarchyExpandLabel != null)
            _hierarchyExpandLabel.text = expanded ? "모두 닫기" : "모두 펼치기";
        if (_hierarchyVisibilityToggle != null)
        {
            _hierarchyVisibilityToggle.SetValueWithoutNotify(visible);
            _hierarchyVisibilityToggle.showMixedValue = !visible && _nodes.Values.Any(n => n.Copy != null && n.Copy.gameObject.activeSelf);
        }

        if (_hierarchyVisibilityLabel != null)
            _hierarchyVisibilityLabel.text = visible ? "모두 끄기" : "모두 켜기";
    }

    /// <summary>검색을 해제하고 전체 계층을 펼치거나 접습니다.</summary>
    void SetHierarchyExpanded(bool expanded)
    {
        if (_canvas == null)
            return;
        _filter = "";
        _treeSearch?.SetValueWithoutNotify("");
        _expanded.Clear();
        if (expanded)
            foreach (var item in _canvas.GetComponentsInChildren<Transform>(true))
                _expanded.Add(item);
        DrawTree();
    }

    /// <summary>원본을 유지한 채 전체 미리보기 계층을 켜거나 끕니다.</summary>
    void SetAllPreviewVisible(bool visible)
    {
        if (_canvas == null)
            return;
        StopDirectorPreview();
        foreach (var node in _nodes.Values)
            if (node.Original != null && node.Copy != null)
                _previewVisibility[node.Original] = visible;
        RestorePreviewVisibility();
        DrawTree();
    }

    /// <summary>펼쳐진 하위 계층을 재귀적으로 표시합니다.</summary>
    void AddTreeBranch(Transform item, int level)
    {
        AddTreeRow(item, level, item.childCount > 0);
        if (!_expanded.Contains(item))
            return;
        foreach (Transform child in item)
            AddTreeBranch(child, level + 1);
    }

    /// <summary>선택 버튼과 오른쪽 표시 체크를 가진 계층 행을 구성합니다.</summary>
    void AddTreeRow(Transform item, int level, bool expandable)
    {
        var row = Row(_tree, "tree-row");
        row.style.marginLeft = level * 13;
        if (expandable)
        {
            var fold = AddButton(row, _expanded.Contains(item) ? "▾" : "▸", () =>
            {
                if (!_expanded.Remove(item))
                    _expanded.Add(item);
                DrawTree();
            });
            fold.AddToClassList("fold-button");
        }

        var visibility = new Toggle();
        visibility.AddToClassList("preview-visibility-eye");
        visibility.SetValueWithoutNotify(item.gameObject.activeSelf);
        visibility.tooltip = "미리보기에서 켜기/끄기 · 원본 활성 상태는 유지합니다.";
        visibility.RegisterValueChangedCallback(e => SetPreviewVisible(item, e.newValue));
        var pick = AddButton(row, item.name + (item.gameObject.activeInHierarchy ? "" : "  [비활성]"), () => SelectNode(item));
        pick.AddToClassList("tree-button");
        pick.EnableInClassList("selected-item", _picked == item);
        if (_picked == item)
            _selectedTreeRow = row;
        row.Add(visibility);
    }

    /// <summary>미리보기 계층을 선택하고 관련 리소스와 연출 설정을 갱신합니다.</summary>
    void SelectNode(Transform item)
    {
        if (_canvas == null || item == null || !item.IsChildOf(_canvas.transform))
            return;
        if (!DirectorBeforePick(item))
            return;
        _picked = item;
        if (_filter.Trim().Length > 0 && item.name.IndexOf(_filter.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
        {
            _filter = "";
            _treeSearch?.SetValueWithoutNotify("");
        }
        for (var parent = item.parent; parent != null && parent.IsChildOf(_canvas.transform); parent = parent.parent)
            _expanded.Add(parent);
        _selectedReference = null;
        RestorePreviewVisibility();
        RefreshUI();
        DirectorPicked();
        RevealSelectedTreeRow();
    }

    /// <summary>계층 배치가 끝난 뒤 현재 선택한 행이 보이도록 스크롤합니다.</summary>
    void RevealSelectedTreeRow()
    {
        var tree = _tree;
        var row = _selectedTreeRow;
        var item = _picked;
        if (tree == null || row == null)
            return;
        tree.schedule.Execute(() =>
        {
            if (_tree == tree && _picked == item && _selectedTreeRow == row && row.panel != null)
                tree.ScrollTo(row);
        }).StartingIn(1);
    }

    /// <summary>선택 대상의 표시 체크와 초기화 버튼을 구성합니다.</summary>
    void BuildPreviewVisibilityControl(VisualElement parent)
    {
        var row = Row(parent, "preview-visibility-controls");
        var toggle = new Toggle("미리보기에서 표시");
        toggle.name = "preview-object-visible";
        toggle.tooltip = "선택한 계층과 자식의 표시를 조절합니다. 켜면 필요한 부모도 표시하고 원본은 변경하지 않습니다.";
        toggle.RegisterValueChangedCallback(e =>
        {
            if (_picked != null)
                SetPreviewVisible(_picked, e.newValue);
        });
        _previewVisibilityToggles.Add(toggle);
        row.Add(toggle);
        AddButton(row, "표시 초기화", () =>
        {
            StopDirectorPreview();
            _previewVisibility.Clear();
            RestorePreviewVisibility();
            DrawTree();
        });
    }

    /// <summary>선택 계층의 실제 미리보기 표시 상태를 체크에 반영합니다.</summary>
    void RefreshPreviewVisibilityControls()
    {
        foreach (var toggle in _previewVisibilityToggles)
        {
            toggle.SetEnabled(_picked != null && _canvas != null);
            toggle.SetValueWithoutNotify(_picked != null && _picked.gameObject.activeInHierarchy);
        }
    }

    /// <summary>선택 계층을 켜거나 끄고 필요한 부모도 미리보기에 표시합니다.</summary>
    void SetPreviewVisible(Transform item, bool visible)
    {
        if (item == null || !_nodes.TryGetValue(item, out var node) || node.Original == null)
            return;
        StopDirectorPreview();
        _previewVisibility[node.Original] = visible;
        if (visible)
            for (var parent = item.parent; parent != null && _nodes.TryGetValue(parent, out var ancestor); parent = parent.parent)
                if (ancestor.Original != null)
                    _previewVisibility[ancestor.Original] = true;
        RestorePreviewVisibility();
        DrawTree();
    }

    /// <summary>원본 상태와 임시 표시 설정을 복제본에 다시 반영합니다.</summary>
    void RestorePreviewVisibility()
    {
        if (_canvas == null)
            return;
        foreach (var node in _nodes.Values)
        {
            if (node.Original == null || node.Copy == null)
                continue;
            node.Copy.gameObject.SetActive(node.Copy == _canvas.transform || node.Original.gameObject.activeSelf);
            var sourceGroups = node.Original.GetComponents<CanvasGroup>();
            var copyGroups = node.Copy.GetComponents<CanvasGroup>();
            for (int i = 0; i < Mathf.Min(sourceGroups.Length, copyGroups.Length); i++)
                copyGroups[i].alpha = sourceGroups[i].alpha;
        }

        if (_revealSelection && _picked != null)
            for (var item = _picked; item != null && item.IsChildOf(_canvas.transform); item = item.parent)
            {
                item.gameObject.SetActive(true);
                foreach (var group in item.GetComponents<CanvasGroup>())
                    if (group.alpha <= .001f)
                        group.alpha = 1;
            }

        foreach (var node in _nodes.Values)
            if (node.Original != null && node.Copy != null && _previewVisibility.TryGetValue(node.Original, out bool visible))
            {
                node.Copy.gameObject.SetActive(visible);
                if (visible)
                    foreach (var group in node.Copy.GetComponents<CanvasGroup>())
                        if (group.alpha <= .001f)
                            group.alpha = 1;
            }

        RefreshPreviewVisibilityControls();
        _renderNeeded = true;
    }

#endregion

#region 상태 갱신과 원본 저장
    /// <summary>현재 대상의 계층과 리소스 및 텍스트 조작 상태를 갱신합니다.</summary>
    void RefreshUI()
    {
        if (_status == null)
            return;
        _targetLabel.text = _target == null ? "Canvas를 선택하세요" : IsDefaultSample ? "기본 테스트 UI · Canvas를 선택하면 전환됩니다" : _target.name + "  ·  " + (EditorUtility.IsPersistent(_target) ? "프리팹 미리보기" : _target.gameObject.scene.name);
        _selectionLabel.text = _picked == null ? "선택한 오브젝트 없음" : "선택: " + _picked.name;
        _hierarchyActions?.SetEnabled(_canvas != null);
        _includeChildren?.SetEnabled(_picked != null);
        DrawTree();
        RefreshSpriteList();
        RefreshTextControl();
        RefreshStatus();
        DirectorPicked();
        _renderNeeded = true;
    }

    /// <summary>편집기 상태 안내 문구를 설정합니다.</summary>
    void SetMessage(string message)
    {
        _message = message;
        RefreshStatus();
    }

    /// <summary>미적용 변경 수와 실행 취소 및 적용 버튼 상태를 갱신합니다.</summary>
    void RefreshStatus()
    {
        if (_status == null)
            return;
        _status.text = $"미리보기 변경 {ResourceChangeCount}건" + (string.IsNullOrEmpty(_message) ? "" : " · " + _message);
        hasUnsavedChanges = !IsTestCanvas && (ResourceChangeCount > 0 || _directorDirty);
        saveChangesMessage = "아직 반영하지 않은 UI 변경이 있습니다.";
        bool editable = CanSaveSource();
        _undoButton.SetEnabled(_history.Count > 0);
        _redoButton.SetEnabled(_redo.Count > 0);
        _discardButton.SetEnabled(ResourceChangeCount > 0);
        _applyButton.SetEnabled(editable && ResourceChangeCount > 0);
        _applyButton.text = IsTestCanvas ? "테스트 UI · 저장 불가" : _target != null && PrefabStageUtility.GetPrefabStage(_target.gameObject) != null ? "열린 프리팹에 적용" : "변경 적용";
        _prefabButton.SetEnabled(!IsTestCanvas && _target != null && !string.IsNullOrEmpty(PrefabPath()));
    }

    /// <summary>현재 대상에서 미리보기 변경이 가능한지 확인합니다.</summary>
    bool CanEditSource() => _target != null && _canvas != null && (!EditorUtility.IsPersistent(_target) || IsDefaultSample) && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
    /// <summary>테스트 UI를 제외한 원본에 변경을 적용할 수 있는지 확인합니다.</summary>
    bool CanSaveSource() => CanEditSource() && !IsTestCanvas;
    /// <summary>실제 UI의 미적용 변경을 반영하고 테스트 UI 저장은 차단합니다.</summary>
    public override void SaveChanges()
    {
        if (IsTestCanvas)
        {
            hasUnsavedChanges = false;
            SetMessage("테스트 UI의 변경은 미리보기에만 반영되며 저장되지 않습니다.");
            return;
        }

        if (ResourceChangeCount > 0)
            ApplyDraft();
        if (_directorDirty)
            ApplyDirector();
        if (ResourceChangeCount == 0 && !_directorDirty)
            base.SaveChanges();
    }

    /// <summary>적용하지 않은 변경을 버리고 원본 상태로 돌아갑니다.</summary>
    public override void DiscardChanges()
    {
        _directorDirty = false;
        _directorOriginal = null;
        if (_target != null)
            Rebuild(_target);
        base.DiscardChanges();
    }

    /// <summary>현재 대상의 원본 프리팹 경로를 찾습니다.</summary>
    string PrefabPath()
    {
        if (_target == null)
            return null;
        if (EditorUtility.IsPersistent(_target))
            return AssetDatabase.GetAssetPath(_target);
        var stage = PrefabStageUtility.GetPrefabStage(_target.gameObject);
        return stage != null ? stage.assetPath : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_target.gameObject);
    }

    /// <summary>원본 프리팹을 편집 모드로 열고 미리보기 대상을 전환합니다.</summary>
    void OpenPrefab()
    {
        if (IsTestCanvas)
            return;
        if (_target == null || !CanAbandonDraft())
            return;
        string path = PrefabPath();
        if (string.IsNullOrEmpty(path))
            return;
        var stage = PrefabStageUtility.OpenPrefab(path);
        var canvas = stage != null ? stage.prefabContentsRoot.GetComponentInChildren<Canvas>(true) : null;
        if (canvas != null)
            Rebuild(canvas);
    }
#endregion

}


