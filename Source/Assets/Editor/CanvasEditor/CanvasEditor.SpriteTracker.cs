using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using U = UnityEngine.UI;

public sealed partial class CanvasEditor
{
    sealed class TrackerUsage
    {
        public bool Prefab, Active;
        public string AssetPath, SceneName, CanvasName, ObjectName, ObjectPath, Field, TypeName, TypeId;
        public int[] Indices;
        public int ComponentIndex;
        public Component SceneComponent;
        public string Group => Prefab ? "프리팹 · " + AssetPath : "열린 씬 · " + SceneName + " / " + CanvasName;
    }

    static readonly List<string> TrackerScopes = new()
    { "열린 씬 + 프로젝트 프리팹", "현재 Canvas", "열린 씬", "프로젝트 프리팹" };
    const int TrackerPageSize = 50;
    [SerializeField] Sprite _trackerSprite;
    [SerializeField] bool _trackerPinned;
    readonly List<TrackerUsage> _trackerUsages = new();
    readonly Stack<TrackerUsage> _trackerBack = new();
    VisualElement _trackerPage, _trackerHelpPage;
    Button _trackerTab, _trackerFind, _trackerCancel, _trackerPrevious, _trackerNext;
    ObjectField _trackerSpriteField;
    UnityEngine.UIElements.Image _trackerThumbnail;
    Label _trackerSource, _trackerAsset, _trackerCount, _trackerStatus, _trackerPages, _trackerBreadcrumb, _trackerDetail, _trackerZoomLabel;
    PopupField<string> _trackerScope;
    Toggle _trackerFollow, _trackerScenes, _trackerPrefabs, _trackerInactive;
    ToolbarSearchField _trackerSearch;
    ScrollView _trackerResults;
    IMGUIContainer _trackerSurface;
    Canvas _trackerSourceCanvas;
    IEnumerator _trackerScan;
    bool _trackerScanning, _trackerStale, _trackerFocus, _trackerSpace;
    int _trackerReadErrors;
    int _trackerPageNumber, _trackerProcessed, _trackerTotal;
    double _trackerNextRefresh;
    string _trackerQuery = "";
    TrackerUsage _trackerCurrent;
    PreviewRenderUtility _trackerPreview;
    GameObject _trackerCopy;
    RectTransform _trackerMarked;
    Texture _trackerTexture;
    Bounds _trackerCanvasBounds, _trackerObjectBounds;
    Vector2 _trackerPan;
    float _trackerZoom = 1f;

    void BuildSpriteTracker(VisualElement parent)
    {
        _trackerPage = parent;
        parent.AddToClassList("tracker-page");
        var source = Row(parent, "tracker-source");
        _trackerThumbnail = new UnityEngine.UIElements.Image { scaleMode = ScaleMode.ScaleToFit };
        _trackerThumbnail.AddToClassList("tracker-thumbnail");
        source.Add(_trackerThumbnail);
        var summary = new VisualElement();
        summary.AddToClassList("tracker-summary");
        source.Add(summary);
        _trackerSpriteField = new ObjectField("선택한 Sprite") { objectType = typeof(Sprite), allowSceneObjects = false };
        _trackerSpriteField.RegisterValueChangedCallback(e => SetTrackerSprite(e.newValue as Sprite, "직접 선택한 Sprite", null));
        summary.Add(_trackerSpriteField);
        _trackerSource = new Label("Image 또는 Sprite를 선택하세요.");
        _trackerSource.AddToClassList("tracker-path");
        summary.Add(_trackerSource);
        _trackerAsset = new Label();
        _trackerAsset.AddToClassList("tracker-path");
        summary.Add(_trackerAsset);
        var actions = new VisualElement();
        actions.AddToClassList("tracker-source-actions");
        source.Add(actions);
        _trackerFollow = new Toggle("이미지 선택 따라가기");
        _trackerFollow.SetValueWithoutNotify(!_trackerPinned);
        _trackerFollow.RegisterValueChangedCallback(e => { _trackerPinned = !e.newValue; if (e.newValue) TrackerFollowSelection(); });
        actions.Add(_trackerFollow);
        var buttons = Row(actions);
        AddButton(buttons, "Sprite 고정", () => { _trackerPinned = true; _trackerFollow.SetValueWithoutNotify(false); });
        _trackerFind = AddButton(buttons, "사용처 찾기 / 다시 검색", StartTrackerScan);
        _trackerFind.AddToClassList("primary-button");
        _trackerCancel = AddButton(buttons, "검색 취소", () => { CancelTrackerScan(); _trackerStale = true; TrackerStatus("검색을 취소했습니다. 현재까지의 결과만 표시합니다."); DrawTrackerResults(); });

        var body = Row(parent, "tracker-body");
        var left = TrackerPanel(body, "01  Sprite 사용처", "tracker-list-panel");
        var scopeRow = Row(left, "tracker-filters");
        _trackerCount = new Label();
        _trackerCount.AddToClassList("tracker-count");
        scopeRow.Add(_trackerCount);
        _trackerScope = new PopupField<string>(TrackerScopes, 0);
        _trackerScope.name = "sprite-tracker-scope";
        _trackerScope.RegisterValueChangedCallback(_ => { _trackerStale = true; TrackerStatus("검색 범위가 바뀌었습니다. 사용처 찾기를 눌러 주세요."); });
        scopeRow.Add(_trackerScope);
        _trackerSearch = new ToolbarSearchField();
        _trackerSearch.tooltip = "게임오브젝트, Canvas, 프리팹, 경로, 참조 필드 검색";
        _trackerSearch.RegisterValueChangedCallback(e => { _trackerQuery = e.newValue; _trackerPageNumber = 0; DrawTrackerResults(); });
        left.Add(_trackerSearch);
        var filters = Row(left, "tracker-filters");
        _trackerScenes = TrackerFilter(filters, "열린 씬");
        _trackerPrefabs = TrackerFilter(filters, "프리팹");
        _trackerInactive = TrackerFilter(filters, "비활성 포함");
        _trackerResults = new ScrollView(ScrollViewMode.Vertical);
        _trackerResults.name = "sprite-tracker-results";
        _trackerResults.AddToClassList("tracker-results");
        left.Add(_trackerResults);
        var pages = Row(left, "tracker-pagination");
        _trackerPrevious = AddButton(pages, "이전", () => { _trackerPageNumber--; DrawTrackerResults(); });
        _trackerPages = new Label();
        pages.Add(_trackerPages);
        _trackerNext = AddButton(pages, "다음", () => { _trackerPageNumber++; DrawTrackerResults(); });

        var right = TrackerPanel(body, "02  사용처 미리보기 · 에디터 내부 전용", "tracker-preview-panel");
        _trackerBreadcrumb = new Label("사용처의 미리보기 버튼을 누르세요.");
        _trackerBreadcrumb.AddToClassList("tracker-breadcrumb");
        right.Add(_trackerBreadcrumb);
        var navigation = Row(right, "tracker-filters");
        AddButton(navigation, "전체 맞춤", () => FrameTrackerPreview(false));
        AddButton(navigation, "대상 확대", () => FrameTrackerPreview(true));
        AddButton(navigation, "이전 위치", () => { if (_trackerBack.Count > 0) OpenTrackerUsage(_trackerBack.Pop(), false); });
        _trackerZoomLabel = new Label("100%");
        navigation.Add(_trackerZoomLabel);
        _trackerSurface = new IMGUIContainer(DrawTrackerPreview);
        _trackerSurface.name = "sprite-tracker-preview";
        _trackerSurface.AddToClassList("tracker-preview-surface");
        _trackerSurface.focusable = true;
        _trackerSurface.RegisterCallback<PointerDownEvent>(_ => _trackerSurface.Focus());
        _trackerSurface.RegisterCallback<BlurEvent>(_ => _trackerSpace = false);
        right.Add(_trackerSurface);
        _trackerDetail = new Label("원본 씬과 Prefab Mode를 열지 않고 이 영역에서만 확인합니다.\n휠 확대 · Space/휠클릭 드래그 이동");
        _trackerDetail.AddToClassList("tracker-detail");
        right.Add(_trackerDetail);
        _trackerStatus = new Label();
        _trackerStatus.AddToClassList("tracker-status");
        parent.Add(_trackerStatus);
        RefreshTrackerSource();
        DrawTrackerResults();
    }

    static VisualElement TrackerPanel(VisualElement parent, string title, string style)
    {
        var panel = new VisualElement();
        panel.AddToClassList("tool-panel");
        panel.AddToClassList(style);
        var label = new Label(title);
        label.AddToClassList("panel-title");
        panel.Add(label);
        parent.Add(panel);
        return panel;
    }

    Toggle TrackerFilter(VisualElement parent, string text)
    {
        var toggle = new Toggle(text);
        toggle.SetValueWithoutNotify(true);
        toggle.RegisterValueChangedCallback(_ => { _trackerPageNumber = 0; DrawTrackerResults(); });
        parent.Add(toggle);
        return toggle;
    }

    void BuildTrackerHelp(VisualElement parent)
    {
        _trackerHelpPage = new ScrollView();
        parent.Add(_trackerHelpPage);
        Help(_trackerHelpPage, "Sprite Tracker · 사용처 찾기", "Project에서 Sprite를 선택하고 Ctrl + Shift + F12를 누르면 Sprite Tracker가 열립니다. Hierarchy나 Canvas Editor에서 Image를 선택한 경우에는 Sprite Tracker 탭을 직접 누릅니다. Sprite 에셋을 직접 지정할 수도 있습니다. Sprite 고정은 다른 이미지를 선택해도 검색 대상을 유지합니다.");
        Help(_trackerHelpPage, "조회 범위", "현재 Canvas, 열린 씬, 프로젝트 프리팹 중 범위를 고릅니다. 비활성·중첩 프리팹·Variant와 컴포넌트의 저장된 Sprite 필드를 조회합니다. 같은 텍스처의 다른 Sprite는 별도로 구분합니다. 코드에서만 동적으로 로드되는 참조와 닫힌 씬은 검색하지 않습니다. 검색을 취소하거나 결과 이름·경로·필드로 좁힐 수 있습니다.");
        Help(_trackerHelpPage, "에디터 내부 미리보기", "사용처의 미리보기 버튼은 원본과 격리한 복제본만 표시합니다. 비활성 대상과 부모는 복제본에서만 표시하며 파란 테두리로 강조합니다. 전체 맞춤·대상 확대·이전 위치와 휠 확대·Space/휠클릭 이동을 사용할 수 있습니다. 이 탭은 원본 선택, 씬, Prefab Mode와 미적용 편집 내용을 바꾸지 않습니다. 스크립트 필드와 버튼 상태 이미지는 참조 소유 오브젝트를 강조하며 게임 코드를 실행하지 않습니다.");
    }

    void EnterSpriteTracker()
    {
        if (!_trackerPinned)
        {
            if (_picked != null && _nodes.TryGetValue(_picked, out var node))
            {
                var image = node.Original != null ? node.Original.GetComponent<U.Image>() : null;
                if (image != null) { SetTrackerSprite(image.sprite, image.name, OutermostTrackerCanvas(image.transform)); return; }
            }
            TrackerFollowSelection();
        }
        RefreshTrackerSource();
    }

    static bool TryGetTrackerSelection(out Sprite sprite, out string source, out Canvas canvas)
    {
        sprite = null;
        source = "";
        canvas = null;
        if (Selection.activeObject is Sprite selected)
        {
            sprite = selected;
            source = "Project에서 선택한 Sprite";
            return true;
        }
        var go = Selection.activeGameObject;
        var image = go != null ? go.GetComponent<U.Image>() : null;
        if (image != null)
        {
            sprite = image.sprite;
            source = image.name;
            canvas = OutermostTrackerCanvas(image.transform);
            return true;
        }
        // Project에서 단일 Sprite PNG를 선택하면 Texture2D가 선택되는 경우도 처리합니다.
        if (Selection.activeObject is Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single)
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                source = "Project에서 선택한 단일 Sprite 이미지";
                return sprite != null;
            }
        }
        return false;
    }

    void OpenTrackerShortcut(Sprite sprite, string source, Canvas canvas)
    {
        if (sprite == null || _trackerPage == null) return;
        bool pinned = _trackerPinned;
        _trackerPinned = true;
        _help = false;
        try
        {
            SetMode(ToolMode.SpriteTracker);
            SetTrackerSprite(sprite, source, canvas);
        }
        finally
        {
            _trackerPinned = pinned;
            RefreshTrackerSource();
        }
        Focus();
    }

    void TrackerFollowSelection()
    {
        if (_trackerPinned || _mode != ToolMode.SpriteTracker || _trackerPage == null) return;
        if (TryGetTrackerSelection(out var sprite, out var source, out var canvas))
            SetTrackerSprite(sprite, source, canvas);
    }

    static Canvas OutermostTrackerCanvas(Transform item) => item.GetComponentsInParent<Canvas>(true).LastOrDefault();

    void SetTrackerSprite(Sprite sprite, string source, Canvas canvas)
    {
        bool changed = !SameTrackerSprite(_trackerSprite, sprite) || _trackerSourceCanvas != canvas;
        _trackerSourceCanvas = canvas;
        _trackerSprite = sprite;
        if (_trackerSource != null) _trackerSource.text = "선택한 Image: " + source + (canvas != null ? " / " + canvas.name : "");
        RefreshTrackerSource();
        if (!changed) return;
        CancelTrackerScan();
        ReleaseTrackerPreview();
        _trackerCurrent = null;
        if (_trackerBreadcrumb != null) _trackerBreadcrumb.text = "사용처의 미리보기 버튼을 누르세요.";
        if (_trackerDetail != null) _trackerDetail.text = "목록에서 대상을 고르면 이 영역에만 표시합니다.";
        _trackerBack.Clear();
        _trackerUsages.Clear();
        _trackerPageNumber = 0;
        DrawTrackerResults();
        if (sprite != null) StartTrackerScan();
        else TrackerStatus("선택한 Image에 Sprite가 없습니다.");
    }

    static bool SameTrackerSprite(Sprite a, Sprite b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string ag, out long aid) &&
               AssetDatabase.TryGetGUIDAndLocalFileIdentifier(b, out string bg, out long bid) && ag == bg && aid == bid;
    }

    void RefreshTrackerSource()
    {
        _trackerFollow?.SetValueWithoutNotify(!_trackerPinned);
        _trackerSpriteField?.SetValueWithoutNotify(_trackerSprite);
        if (_trackerThumbnail != null) _trackerThumbnail.sprite = _trackerSprite;
        if (_trackerAsset != null) _trackerAsset.text = _trackerSprite != null ? AssetDatabase.GetAssetPath(_trackerSprite) + $" · {_trackerSprite.rect.width:0} × {_trackerSprite.rect.height:0} px" : "Image 또는 Sprite를 선택하세요.";
        _trackerFind?.SetEnabled(_trackerSprite != null && !EditorApplication.isPlayingOrWillChangePlaymode);
        _trackerCancel?.SetEnabled(_trackerScanning);
    }

    void StartTrackerScan()
    {
        CancelTrackerScan();
        if (_trackerSprite == null || EditorApplication.isPlayingOrWillChangePlaymode)
        { TrackerStatus("편집 모드에서 Image 또는 Sprite를 선택하세요."); return; }
        _trackerUsages.Clear();
        _trackerPageNumber = 0;
        _trackerProcessed = _trackerTotal = _trackerReadErrors = 0;
        _trackerStale = false;
        _trackerScanning = true;
        _trackerScan = ScanTrackerReferences();
        _trackerNextRefresh = 0;
        EditorApplication.update += UpdateTrackerScan;
        RefreshTrackerSource();
        TrackerStatus("저장된 Sprite 참조를 조회하고 있습니다…");
        DrawTrackerResults();
    }

    IEnumerator ScanTrackerReferences()
    {
        int scope = _trackerScope != null ? _trackerScope.index : 0;
        var roots = new List<GameObject>();
        if (scope == 1)
        {
            if (_trackerSourceCanvas != null) roots.Add(_trackerSourceCanvas.gameObject);
            else { TrackerStatus("현재 Canvas 범위는 먼저 Canvas 안의 Image를 선택해야 합니다."); yield break; }
        }
        else if (scope != 3)
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene)) roots.AddRange(scene.GetRootGameObjects());
            }
        var prefabs = scope == 0 || scope == 3 ? AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        _trackerTotal = roots.Count + prefabs.Length;
        foreach (var root in roots)
        {
            if (root != null)
            {
                bool persistent = EditorUtility.IsPersistent(root);
                string asset = persistent ? AssetDatabase.GetAssetPath(root) : root.scene.path;
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                { ReadTrackerComponent(component, root.transform, persistent, asset); yield return null; }
            }
            _trackerProcessed++;
        }
        string spritePath = AssetDatabase.GetAssetPath(_trackerSprite);
        foreach (string path in prefabs)
        {
            if (string.IsNullOrEmpty(spritePath) || !spritePath.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.GetDependencies(path, true).Contains(spritePath))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null)
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    { ReadTrackerComponent(component, root.transform, true, path); yield return null; }
            }
            _trackerProcessed++;
            yield return null;
        }
    }

    void ReadTrackerComponent(Component component, Transform root, bool prefab, string asset)
    {
        if (component == null) return;
        try
        {
            using var data = new SerializedObject(component);
            var p = data.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || !SameTrackerSprite(p.objectReferenceValue as Sprite, _trackerSprite)) continue;
                var sameType = component.GetComponents(component.GetType()).Where(c => c != null && c.GetType() == component.GetType()).ToArray();
                var canvas = component.GetComponentInParent<Canvas>(true);
                _trackerUsages.Add(new TrackerUsage
                {
                    Prefab = prefab, AssetPath = asset, SceneName = prefab ? "" : component.gameObject.scene.name,
                    CanvasName = canvas != null ? canvas.name : "Canvas 없음", ObjectName = component.name,
                    ObjectPath = TrackerObjectPath(root, component.transform),
                    Field = p.propertyPath, TypeName = component.GetType().Name + (sameType.Length > 1 ? $"[{Array.IndexOf(sameType, component)}]" : ""), TypeId = component.GetType().AssemblyQualifiedName,
                    ComponentIndex = Array.IndexOf(sameType, component), Indices = TrackerIndices(root, component.transform),
                    Active = component.gameObject.activeInHierarchy, SceneComponent = prefab ? null : component
                });
            }
        }
        catch (Exception ex) { _trackerReadErrors++; TrackerStatus("일부 참조를 읽지 못했습니다: " + component.name + " · " + ex.Message); }
    }

    static string TrackerObjectPath(Transform root, Transform target)
    {
        var names = new Stack<string>();
        for (var t = target; t != null; t = t.parent)
        {
            bool duplicate = t.parent != null && t.parent.Cast<Transform>().Count(s => s.name == t.name) > 1;
            names.Push(t.name + (duplicate ? $"[{t.GetSiblingIndex()}]" : ""));
            if (t == root) break;
        }
        return string.Join("/", names);
    }

    static int[] TrackerIndices(Transform root, Transform target)
    {
        var indices = new Stack<int>();
        for (var t = target; t != root; t = t.parent)
        {
            if (t == null) throw new InvalidOperationException("참조 대상이 원본 계층 밖에 있습니다.");
            indices.Push(t.GetSiblingIndex());
        }
        return indices.ToArray();
    }

    void UpdateTrackerScan()
    {
        if (!_trackerScanning || _trackerScan == null) { CancelTrackerScan(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) { CancelTrackerScan(); return; }
        double until = EditorApplication.timeSinceStartup + .012;
        try
        {
            do
            {
                if (_trackerScan.MoveNext()) continue;
                CancelTrackerScan();
                DrawTrackerResults();
                TrackerStatus($"조회 완료 · 참조 {_trackerUsages.Count}곳 · 읽기 실패 {_trackerReadErrors}개 · 원본을 변경하지 않습니다.");
                return;
            } while (EditorApplication.timeSinceStartup < until);
            if (EditorApplication.timeSinceStartup >= _trackerNextRefresh)
            {
                _trackerNextRefresh = EditorApplication.timeSinceStartup + .3;
                DrawTrackerResults();
                TrackerStatus($"조회 중 {_trackerProcessed}/{_trackerTotal} · 참조 {_trackerUsages.Count}곳");
            }
        }
        catch (Exception ex) { CancelTrackerScan(); TrackerStatus("조회 실패: " + ex.Message); }
    }

    void CancelTrackerScan()
    {
        EditorApplication.update -= UpdateTrackerScan;
        (_trackerScan as IDisposable)?.Dispose();
        _trackerScan = null;
        _trackerScanning = false;
        RefreshTrackerSource();
    }

    void DrawTrackerResults()
    {
        if (_trackerResults == null) return;
        var query = _trackerQuery.Trim();
        var filtered = _trackerUsages.Where(u => (u.Prefab ? _trackerPrefabs.value : _trackerScenes.value) &&
            (_trackerInactive.value || u.Active) && (query.Length == 0 ||
            (u.ObjectPath + " " + u.AssetPath + " " + u.CanvasName + " " + u.SceneName + " " + u.TypeName + "." + u.Field).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(u => u.Prefab).ThenBy(u => u.Group, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.ObjectPath, StringComparer.OrdinalIgnoreCase).ThenBy(u => u.Field, StringComparer.Ordinal).ToList();
        int pages = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)TrackerPageSize));
        _trackerPageNumber = Mathf.Clamp(_trackerPageNumber, 0, pages - 1);
        _trackerResults.Clear();
        _trackerCount.text = $"참조 {_trackerUsages.Count}곳 · 표시 {filtered.Count}곳";
        _trackerPages.text = $"{_trackerPageNumber + 1} / {pages}";
        _trackerPrevious.SetEnabled(_trackerPageNumber > 0);
        _trackerNext.SetEnabled(_trackerPageNumber + 1 < pages);
        string group = null;
        foreach (var usage in filtered.Skip(_trackerPageNumber * TrackerPageSize).Take(TrackerPageSize))
        {
            if (group != usage.Group)
            {
                group = usage.Group;
                var heading = new Label(group);
                heading.AddToClassList("tracker-result-heading");
                _trackerResults.Add(heading);
            }
            var card = Row(_trackerResults, "tracker-result");
            card.EnableInClassList("selected-item", usage == _trackerCurrent);
            var content = new VisualElement();
            content.AddToClassList("tracker-result-content");
            card.Add(content);
            var title = new Label(usage.ObjectName + (usage.Active ? "" : "  [비활성]"));
            title.AddToClassList("tracker-result-name");
            content.Add(title);
            var field = new Label(usage.CanvasName + " · " + usage.TypeName + "." + usage.Field);
            field.AddToClassList("tracker-path");
            content.Add(field);
            var path = new Label(usage.ObjectPath);
            path.AddToClassList("tracker-path");
            content.Add(path);
            card.tooltip = usage.AssetPath + "\n" + usage.ObjectPath + "\n" + usage.TypeName + "." + usage.Field;
            var button = AddButton(card, "미리보기 →", () => OpenTrackerUsage(usage));
            button.name = "sprite-tracker-open";
            button.AddToClassList("tracker-open-button");
        }
        if (filtered.Count == 0)
            _trackerResults.Add(new Label(_trackerScanning ? "참조를 조회하고 있습니다…" : _trackerSprite == null ? "Image 또는 Sprite를 선택하세요." : "이 범위/필터에서 사용처가 없습니다."));
    }

    void TrackerStatus(string message)
    {
        if (_trackerStatus != null) _trackerStatus.text = (_trackerStale ? "[다시 검색 필요] " : "") + message;
    }

    Component ResolveTrackerUsage(TrackerUsage usage)
    {
        if (!usage.Prefab) return usage.SceneComponent;
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(usage.AssetPath);
        if (root == null) return null;
        Transform target = root.transform;
        foreach (int i in usage.Indices)
        {
            if (i < 0 || i >= target.childCount) return null;
            target = target.GetChild(i);
        }
        return target.GetComponents<Component>().Where(c => c != null && c.GetType().AssemblyQualifiedName == usage.TypeId).ElementAtOrDefault(usage.ComponentIndex);
    }

    void OpenTrackerUsage(TrackerUsage usage, bool remember = true)
    {
        if (usage == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var component = ResolveTrackerUsage(usage);
        if (component == null) { TrackerStatus("대상이 변경되거나 삭제됐습니다. 다시 검색해 주세요."); return; }
        using (var data = new SerializedObject(component))
        {
            var p = data.FindProperty(usage.Field);
            if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || !SameTrackerSprite(p.objectReferenceValue as Sprite, _trackerSprite))
            { TrackerStatus("Sprite 참조가 변경됐습니다. 다시 검색해 주세요."); return; }
        }
        try
        {
            ReleaseTrackerPreview();
            var canvas = OutermostTrackerCanvas(component.transform);
            var source = canvas != null ? canvas.gameObject : usage.Prefab ? AssetDatabase.LoadAssetAtPath<GameObject>(usage.AssetPath) : component.transform.root.gameObject;
            var indices = TrackerIndices(source.transform, component.transform);
            _trackerPreview = new PreviewRenderUtility();
            var holder = new GameObject("Sprite Tracker Preview") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
            _trackerPreview.AddSingleGO(holder);
            var copy = Instantiate(source, holder.transform, false);
            copy.name = source.name;
            SanitizeCopy(copy);
            Transform marked = copy.transform;
            foreach (int index in indices) marked = marked.GetChild(index);
            var renderCanvas = copy.GetComponent<Canvas>();
            RectTransform canvasRect;
            if (renderCanvas != null)
            {
                canvasRect = (RectTransform)copy.transform;
                var original = (RectTransform)source.transform;
                var size = original.rect.size;
                if (size.x <= 1 || size.y <= 1)
                {
                    var scaler = source.GetComponent<U.CanvasScaler>();
                    size = scaler != null && scaler.uiScaleMode == U.CanvasScaler.ScaleMode.ScaleWithScreenSize ? scaler.referenceResolution : new Vector2(1920, 1080);
                }
                canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(.5f, .5f);
                canvasRect.sizeDelta = new Vector2(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
                renderCanvas.renderMode = RenderMode.WorldSpace;
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one;
            }
            else
            {
                var wrapper = new GameObject("Sprite Tracker Canvas", typeof(RectTransform), typeof(Canvas));
                wrapper.transform.SetParent(holder.transform, false);
                canvasRect = (RectTransform)wrapper.transform;
                canvasRect.sizeDelta = new Vector2(1920, 1080);
                renderCanvas = wrapper.GetComponent<Canvas>();
                renderCanvas.renderMode = RenderMode.WorldSpace;
                copy.transform.SetParent(wrapper.transform, false);
                copy.transform.localPosition = Vector3.zero;
            }
            var camera = _trackerPreview.camera;
            camera.cameraType = CameraType.Game;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(21, 28, 39, 255);
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 4000;
            camera.scene = copy.scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(copy.scene);
            renderCanvas.worldCamera = camera;
            holder.SetActive(true);
            copy.SetActive(true);
            for (var t = marked; t != null && t.IsChildOf(holder.transform); t = t.parent)
            {
                t.gameObject.SetActive(true);
                foreach (var group in t.GetComponents<CanvasGroup>()) group.alpha = 1;
            }
            _trackerCopy = copy;
            _trackerMarked = marked as RectTransform;
            Canvas.ForceUpdateCanvases();
            _trackerCanvasBounds = TrackerBounds(canvasRect);
            _trackerObjectBounds = _trackerMarked != null ? TrackerBounds(_trackerMarked) : new Bounds(marked.position, Vector3.one * 100);
            if (remember && _trackerCurrent != null && _trackerCurrent != usage) _trackerBack.Push(_trackerCurrent);
            _trackerCurrent = usage;
            FrameTrackerPreview(false);
            _trackerBreadcrumb.text = usage.CanvasName + " > " + usage.ObjectName;
            _trackerDetail.text = $"GameObject  {usage.ObjectName}\n소속 Canvas  {usage.CanvasName}\n참조 위치  {usage.TypeName}.{usage.Field}\n{(usage.Active ? "활성" : "비활성 · 미리보기에서만 표시")}\n{usage.AssetPath}\n{usage.ObjectPath}\n휠 확대 · Space/휠클릭 드래그 이동";
            TrackerStatus("에디터 내부 미리보기 · 원본 선택/씬/Prefab Mode를 변경하지 않았습니다.");
            DrawTrackerResults();
        }
        catch (Exception ex) { ReleaseTrackerPreview(); TrackerStatus("미리보기를 열 수 없습니다: " + ex.Message); }
    }

    static Bounds TrackerBounds(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var bounds = new Bounds(corners[0], Vector3.zero);
        for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(corners[i]);
        return bounds;
    }

    void FrameTrackerPreview(bool focus)
    {
        _trackerFocus = focus;
        _trackerZoom = 1;
        _trackerPan = Vector2.zero;
        _trackerSurface?.MarkDirtyRepaint();
    }

    void DrawTrackerPreview()
    {
        var rect = _trackerSurface.contentRect;
        if (rect.width < 1 || rect.height < 1) return;
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { _trackerSpace = true; e.Use(); }
        if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Space) { _trackerSpace = false; e.Use(); }
        if (_trackerPreview == null || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
        { _trackerZoom = Mathf.Clamp(_trackerZoom * Mathf.Pow(1.1f, -e.delta.y), .05f, 20f); e.Use(); _trackerSurface.MarkDirtyRepaint(); }
        if (e.type == EventType.MouseDrag && rect.Contains(e.mousePosition) && (e.button == 2 || e.button == 0 && _trackerSpace))
        {
            float perPixel = _trackerPreview.camera.orthographicSize * 2 / rect.height;
            _trackerPan += new Vector2(-e.delta.x, e.delta.y) * perPixel;
            e.Use(); _trackerSurface.MarkDirtyRepaint();
        }
        if (e.type != EventType.Repaint) return;
        try
        {
            var camera = _trackerPreview.camera;
            var bounds = _trackerFocus ? _trackerObjectBounds : _trackerCanvasBounds;
            float aspect = rect.width / rect.height;
            camera.orthographicSize = Mathf.Max(1, bounds.size.y * .6f, bounds.size.x / aspect * .6f) / _trackerZoom;
            camera.transform.SetPositionAndRotation(bounds.center + new Vector3(_trackerPan.x, _trackerPan.y, -1000), Quaternion.identity);
            camera.aspect = aspect;
            _trackerPreview.BeginPreview(new Rect(0, 0, Mathf.Min(1600, rect.width), Mathf.Min(1200, rect.height)), GUIStyle.none);
            try { Canvas.ForceUpdateCanvases(); _trackerPreview.Render(true, false); }
            finally { _trackerTexture = _trackerPreview.EndPreview(); }
            if (_trackerTexture != null) GUI.DrawTexture(rect, _trackerTexture, ScaleMode.StretchToFill, false);
            if (_trackerMarked != null)
            {
                var corners = new Vector3[4];
                _trackerMarked.GetWorldCorners(corners);
                var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (var corner in corners)
                {
                    var p = camera.WorldToViewportPoint(corner);
                    var local = new Vector2(rect.x + p.x * rect.width, rect.y + (1 - p.y) * rect.height);
                    min = Vector2.Min(min, local); max = Vector2.Max(max, local);
                }
                var color = new Color32(112, 198, 255, 255);
                EditorGUI.DrawRect(new Rect(min.x, min.y, max.x - min.x, 2), color);
                EditorGUI.DrawRect(new Rect(min.x, max.y - 2, max.x - min.x, 2), color);
                EditorGUI.DrawRect(new Rect(min.x, min.y, 2, max.y - min.y), color);
                EditorGUI.DrawRect(new Rect(max.x - 2, min.y, 2, max.y - min.y), color);
            }
            if (_trackerZoomLabel != null) _trackerZoomLabel.text = $"{_trackerZoom * 100:0}%";
        }
        catch (Exception ex) { ReleaseTrackerPreview(); TrackerStatus("미리보기 렌더링 실패: " + ex.Message); }
    }

    void ReleaseTrackerPreview()
    {
        _trackerMarked = null;
        _trackerCopy = null;
        _trackerTexture = null;
        _trackerSpace = false;
        if (_trackerPreview != null) { _trackerPreview.Cleanup(); _trackerPreview = null; }
        _trackerSurface?.MarkDirtyRepaint();
    }

    void ReleaseSpriteTracker()
    {
        CancelTrackerScan();
        ReleaseTrackerPreview();
    }

    void TrackerProjectChanged()
    {
        if (_trackerSprite == null) return;
        _trackerStale = true;
        TrackerStatus("프로젝트가 변경됐습니다. 최신 사용처는 다시 검색해 주세요.");
    }

    void TrackerPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) { ReleaseSpriteTracker(); TrackerStatus("플레이 중에는 조회와 미리보기를 중지합니다."); }
        RefreshTrackerSource();
    }
}
