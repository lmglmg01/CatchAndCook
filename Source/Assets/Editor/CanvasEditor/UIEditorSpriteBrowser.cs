using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UIEditorSpriteBrowser : EditorWindow
{
    const int PageSize = 60;
    [SerializeField]
    string _folder = "Assets", _search = "";
    [SerializeField]
    Sprite _selected;
    readonly HashSet<string> _expanded = new();
    readonly List<Sprite> _sprites = new();
    Func<Sprite, bool> _choose;
    ScrollView _tree, _items;
    VisualElement _grid, _selectedFolderRow;
    Label _path, _count, _selection, _pageLabel;
    UnityEngine.UIElements.Image _selectedImage;
    Button _accept, _previous, _next, _up;
    int _page;
#region 탐색창 구성
    /// <summary>편집창을 열고 초기 대상과 선택 동작을 연결합니다.</summary>
    public static UIEditorSpriteBrowser Open(Sprite initial, Func<Sprite, bool> choose)
    {
        var window = CreateInstance<UIEditorSpriteBrowser>();
        window.titleContent = new GUIContent("Sprite 폴더 탐색");
        window._choose = choose;
        window._selected = initial;
        string path = initial != null ? AssetDatabase.GetAssetPath(initial) : "";
        string folder = string.IsNullOrEmpty(path) ? "Assets" : Path.GetDirectoryName(path).Replace('\\', '/');
        window._folder = AssetDatabase.IsValidFolder(folder) ? folder : "Assets";
        window.ExpandParents(window._folder);
        window.minSize = new Vector2(700, 460);
        window.position = new Rect(100, 80, 920, 630);
        window.ShowUtility();
        return window;
    }

    /// <summary>편집창의 화면과 조작부를 구성하고 현재 상태를 반영합니다.</summary>
    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.AddToClassList("canvas-editor");
        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/CanvasEditor/CanvasEditor.uss");
        if (sheet != null)
            root.styleSheets.Add(sheet);
        var toolbar = Row(root, "sprite-browser-toolbar");
        _up = Add(toolbar, "상위 폴더", () => Navigate(ParentFolder(_folder)));
        Add(toolbar, "새로고침", RefreshAssets);
        var search = new ToolbarSearchField();
        search.AddToClassList("sprite-browser-search");
        search.SetValueWithoutNotify(_search);
        search.RegisterValueChangedCallback(e =>
        {
            _search = e.newValue;
            _page = 0;
            DrawItems();
        });
        toolbar.Add(search);
        _path = new Label();
        _path.AddToClassList("sprite-browser-path");
        root.Add(_path);
        var body = Row(root, "sprite-browser-body");
        _tree = new ScrollView(ScrollViewMode.Vertical);
        _tree.AddToClassList("sprite-browser-tree");
        body.Add(_tree);
        var content = new VisualElement();
        content.AddToClassList("sprite-browser-content");
        body.Add(content);
        _count = new Label();
        _count.AddToClassList("secondary-label");
        content.Add(_count);
        _items = new ScrollView(ScrollViewMode.Vertical);
        _items.AddToClassList("sprite-browser-items");
        content.Add(_items);
        _grid = new VisualElement();
        _grid.AddToClassList("sprite-browser-grid");
        _items.Add(_grid);
        var pages = Row(content, "sprite-browser-pages");
        _previous = Add(pages, "이전", () =>
        {
            _page--;
            DrawItems();
        });
        _pageLabel = new Label();
        pages.Add(_pageLabel);
        _next = Add(pages, "다음", () =>
        {
            _page++;
            DrawItems();
        });
        var footer = Row(root, "sprite-browser-footer");
        _selectedImage = new UnityEngine.UIElements.Image{scaleMode = ScaleMode.ScaleToFit};
        _selectedImage.AddToClassList("asset-thumbnail");
        footer.Add(_selectedImage);
        _selection = new Label();
        _selection.AddToClassList("sprite-browser-selection");
        footer.Add(_selection);
        Add(footer, "취소", Close);
        _accept = Add(footer, "Sprite 선택", Choose);
        _accept.AddToClassList("primary-button");
        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                Close();
                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Return && e.target is not TextField)
            {
                Choose();
                e.StopPropagation();
            }
        });
        RefreshAssets();
    }

#endregion
#region 폴더 이동과 조회
    /// <summary>가로 배치용 컨테이너를 만들어 부모에 추가합니다.</summary>
    static VisualElement Row(VisualElement parent, string style)
    {
        var row = new VisualElement();
        row.AddToClassList("row");
        row.AddToClassList(style);
        parent.Add(row);
        return row;
    }

    /// <summary>지정한 동작을 실행하는 버튼을 부모에 추가합니다.</summary>
    static Button Add(VisualElement parent, string text, Action action)
    {
        var button = new Button(action)
        {text = text};
        parent.Add(button);
        return button;
    }

    /// <summary>Assets 경계를 벗어나지 않는 상위 폴더 경로를 구합니다.</summary>
    static string ParentFolder(string folder) => folder == "Assets" ? "Assets" : Path.GetDirectoryName(folder).Replace('\\', '/');
    /// <summary>현재 폴더까지의 상위 경로가 계층에 보이도록 펼칩니다.</summary>
    void ExpandParents(string folder)
    {
        while (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets", StringComparison.Ordinal))
        {
            _expanded.Add(folder);
            if (folder == "Assets")
                break;
            folder = ParentFolder(folder);
        }
    }

    /// <summary>선택한 폴더로 이동하고 Sprite 목록을 갱신합니다.</summary>
    void Navigate(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return;
        _folder = folder;
        _page = 0;
        ExpandParents(folder);
        RefreshAssets();
    }

    /// <summary>현재 폴더의 Sprite와 하위 폴더 목록을 다시 조회합니다.</summary>
    void RefreshAssets()
    {
        if (!AssetDatabase.IsValidFolder(_folder))
            _folder = "Assets";
        ExpandParents(_folder);
        _sprites.Clear();
        foreach (string path in AssetDatabase.FindAssets("t:Sprite", new[]{_folder}).Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == _folder).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            _sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
        _sprites.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name));
        DrawTree();
        DrawItems();
        UpdateSelection();
        _tree.schedule.Execute(() =>
        {
            if (_selectedFolderRow != null && _selectedFolderRow.panel != null)
                _tree.scrollOffset = new Vector2(0, Mathf.Max(0, _selectedFolderRow.layout.y - 80));
        }).StartingIn(150);
    }

#endregion
#region 계층과 썸네일 표시
    /// <summary>현재 대상의 계층을 펼침 상태에 맞춰 다시 표시합니다.</summary>
    void DrawTree()
    {
        _tree.Clear();
        _selectedFolderRow = null;
        DrawFolder("Assets", 0);
    }

    /// <summary>폴더의 펼침 상태와 선택 표시를 계층 행으로 그립니다.</summary>
    void DrawFolder(string path, int level)
    {
        var children = AssetDatabase.GetSubFolders(path).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var row = Row(_tree, "sprite-browser-tree-row");
        row.style.marginLeft = level * 12;
        var fold = Add(row, children.Length == 0 ? "" : _expanded.Contains(path) ? "▾" : "▸", () =>
        {
            if (!_expanded.Remove(path))
                _expanded.Add(path);
            DrawTree();
        });
        fold.AddToClassList("fold-button");
        fold.SetEnabled(children.Length > 0);
        var pick = Add(row, Path.GetFileName(path), () => Navigate(path));
        pick.AddToClassList("tree-button");
        pick.EnableInClassList("selected-item", path == _folder);
        pick.tooltip = path;
        if (path == _folder)
            _selectedFolderRow = row;
        if (_expanded.Contains(path))
            foreach (string child in children)
                DrawFolder(child, level + 1);
    }

    /// <summary>검색 결과와 현재 페이지의 폴더 및 Sprite 카드를 표시합니다.</summary>
    void DrawItems()
    {
        _grid.Clear();
        _path.text = _folder;
        _path.tooltip = _folder;
        _up.SetEnabled(_folder != "Assets");
        bool Match(string name) => string.IsNullOrWhiteSpace(_search) || name.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        var folders = AssetDatabase.GetSubFolders(_folder).Where(p => Match(Path.GetFileName(p))).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        var sprites = _sprites.Where(s => s != null && Match(s.name)).ToArray();
        int total = folders.Length + sprites.Length, pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)PageSize));
        _page = Mathf.Clamp(_page, 0, pages - 1);
        _count.text = $"폴더 {folders.Length}개 · Sprite {sprites.Length}개 · 현재 폴더에서 이름 검색 · 폴더 더블클릭으로 이동";
        _pageLabel.text = $"{_page + 1} / {pages}";
        _previous.SetEnabled(_page > 0);
        _next.SetEnabled(_page + 1 < pages);
        for (int i = _page * PageSize; i < Mathf.Min(total, (_page + 1) * PageSize); i++)
        {
            if (i < folders.Length)
            {
                string path = folders[i];
                var card = Add(_grid, "", () =>
                {
                });
                card.AddToClassList("sprite-browser-card");
                card.tooltip = path;
                var icon = new UnityEngine.UIElements.Image{image = AssetDatabase.GetCachedIcon(path), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore};
                icon.AddToClassList("sprite-browser-image");
                card.Add(icon);
                var name = new Label("폴더 · " + Path.GetFileName(path))
                {pickingMode = PickingMode.Ignore};
                name.AddToClassList("sprite-browser-name");
                card.Add(name);
                card.RegisterCallback<ClickEvent>(e =>
                {
                    if (e.clickCount == 2)
                        Navigate(path);
                });
            }
            else
            {
                var sprite = sprites[i - folders.Length];
                var card = Add(_grid, "", () =>
                {
                    _selected = sprite;
                    UpdateSelection();
                    HighlightCards();
                });
                card.userData = sprite;
                card.AddToClassList("sprite-browser-card");
                card.tooltip = sprite.name + "\n" + AssetDatabase.GetAssetPath(sprite) + $"\n{sprite.rect.width:0} × {sprite.rect.height:0}";
                var image = new UnityEngine.UIElements.Image{sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore};
                image.AddToClassList("sprite-browser-image");
                card.Add(image);
                var label = new Label(sprite.name)
                {pickingMode = PickingMode.Ignore};
                label.AddToClassList("sprite-browser-name");
                card.Add(label);
                card.RegisterCallback<ClickEvent>(e =>
                {
                    if (e.clickCount == 2)
                    {
                        _selected = sprite;
                        Choose();
                    }
                });
            }
        }

        if (total == 0)
            _grid.Add(new Label(string.IsNullOrWhiteSpace(_search) ? "이 폴더에는 Sprite가 없습니다. 다른 폴더를 선택하세요." : "검색 결과가 없습니다."));
        HighlightCards();
        _items.scrollOffset = Vector2.zero;
    }

    /// <summary>선택한 Sprite에 해당하는 카드만 강조합니다.</summary>
    void HighlightCards()
    {
        foreach (var card in _grid.Children())
            card.EnableInClassList("selected-item", _selected != null && ReferenceEquals(card.userData, _selected));
    }

    /// <summary>선택한 Sprite의 이름과 경로 및 확인 버튼 상태를 갱신합니다.</summary>
    void UpdateSelection()
    {
        _selectedImage.sprite = _selected;
        _selection.text = _selected != null ? _selected.name + "\n" + AssetDatabase.GetAssetPath(_selected) : "교체할 Sprite를 선택하세요.";
        _selection.tooltip = _selection.text;
        _accept.SetEnabled(_selected != null && _choose != null);
    }

#endregion
#region 선택 확정
    /// <summary>기존 사용 위치가 유지될 때만 Sprite 선택을 확정합니다.</summary>
    void Choose()
    {
        if (_selected == null || _choose == null)
            return;
        if (_choose(_selected))
            Close();
        else
            ShowNotification(new GUIContent("사용 위치가 변경되었습니다. 원래 창에서 다시 열어 주세요."));
    }
#endregion
}
