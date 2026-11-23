using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class AssetMoverWindow : EditorWindow
{
    [Header("이동할 에셋")]
    [SerializeField] List<UnityEngine.Object> _assets;
    [Header("목적지 폴더")]
    [SerializeField] DefaultAsset _destinationFolder;

    sealed class MoveEntry
    {
        public UnityEngine.Object Asset;
        public string Source;
        public string Destination;
        public string Message;
        public bool CanMove;
        public bool IsError;
    }

    readonly List<MoveEntry> _plan = new List<MoveEntry>();
    ObjectField _folderField;
    ListView _list;
    Button _moveButton;
    Label _summary;
    HelpBox _message;

    [MenuItem("Tools/Asset Mover")]
    public static void OpenWindow()
    {
        var window = GetWindow<AssetMoverWindow>("에셋 파일 이동");
        window.minSize = new Vector2(740f, 420f);
        window.Show();
    }

    public void CreateGUI()
    {
        _assets ??= new List<UnityEngine.Object>();
        rootVisualElement.Clear();
        rootVisualElement.Add(new HelpBox(
            "Project에서 파일을 여러 개 선택해 추가하거나 이 창으로 드래그하세요. 목적지를 지정하면 이동 경로가 표시됩니다.",
            HelpBoxMessageType.Info));

        var toolbar = new Toolbar();
        toolbar.Add(new ToolbarButton(() => AddAssets(Selection.objects)) { text = "선택한 에셋 추가" });
        toolbar.Add(new ToolbarButton(() => { _assets.Clear(); RefreshPlan(); }) { text = "목록 비우기" });
        toolbar.Add(new ToolbarButton(RefreshPlan) { text = "경로 다시 확인" });
        rootVisualElement.Add(toolbar);

        _folderField = new ObjectField("목적지 폴더")
        {
            name = "destinationFolder", objectType = typeof(DefaultAsset),
            allowSceneObjects = false, value = _destinationFolder
        };
        _folderField.RegisterValueChangedCallback(evt =>
        {
            _destinationFolder = evt.newValue as DefaultAsset;
            RefreshPlan();
        });
        rootVisualElement.Add(_folderField);
        rootVisualElement.Add(new Button(ChooseFolder) { text = "목적지 폴더 찾아보기" });
        _summary = new Label();
        _message = new HelpBox(string.Empty, HelpBoxMessageType.Info);
        _moveButton = new Button(MoveAssets) { text = "파일 이동", name = "moveAssets" };
        rootVisualElement.Add(_summary);
        rootVisualElement.Add(_moveButton);
        _list = new ListView(_plan, 66f, MakeRow, BindRow)
        {
            name = "movePlan", selectionType = SelectionType.None,
            showAlternatingRowBackgrounds = AlternatingRowBackground.All
        };
        rootVisualElement.Add(_list);
        rootVisualElement.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
        rootVisualElement.RegisterCallback<DragPerformEvent>(OnDragPerform);
        RefreshPlan();
    }

    void AddAssets(IEnumerable<UnityEngine.Object> assets)
    {
        int rejected = 0;
        var paths = new HashSet<string>(_assets.Where(a => a != null).Select(AssetDatabase.GetAssetPath),
            StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!IsMovableFile(asset, path)) { rejected++; continue; }
            if (paths.Add(path)) _assets.Add(asset);
        }
        RefreshPlan();
        ShowMessage(rejected > 0 ? $"{rejected}개 제외: Assets 안의 파일만 추가할 수 있습니다. 폴더·씬 오브젝트·파일 내부 하위 에셋은 제외합니다." : string.Empty);
    }

    static bool IsMovableFile(UnityEngine.Object asset, string path)
    {
        return asset != null && path.StartsWith("Assets/", StringComparison.Ordinal)
            && !AssetDatabase.IsValidFolder(path) && AssetDatabase.IsMainAsset(asset);
    }

    void ChooseFolder()
    {
        string chosen = EditorUtility.OpenFolderPanel("Assets 안의 목적지 폴더", Application.dataPath, string.Empty);
        if (string.IsNullOrEmpty(chosen)) return;
        string relative = FileUtil.GetProjectRelativePath(chosen.Replace('\\', '/'));
        if (!IsDestinationFolder(relative))
        {
            ShowMessage("현재 프로젝트의 Assets 안에 있는 폴더를 선택하세요.");
            return;
        }
        _folderField.value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
    }

    static bool IsDestinationFolder(string path)
    {
        return (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal))
            && AssetDatabase.IsValidFolder(path);
    }

    void RefreshPlan()
    {
        if (_list == null) return;
        ShowMessage(string.Empty);
        BuildPlan();
        _list.RefreshItems();
        int ready = _plan.Count(e => e.CanMove);
        int blocked = _plan.Count(e => e.IsError);
        _summary.text = $"등록 {_plan.Count}개 · 이동 가능 {ready}개 · 확인 필요 {blocked}개";
        _moveButton.text = $"파일 {ready}개 이동";
        _moveButton.SetEnabled(ready > 0 && blocked == 0);
    }

    void BuildPlan()
    {
        _plan.Clear();
        string folder = AssetDatabase.GetAssetPath(_destinationFolder);
        bool validFolder = IsDestinationFolder(folder);
        foreach (var asset in _assets)
        {
            string source = AssetDatabase.GetAssetPath(asset);
            var entry = new MoveEntry { Asset = asset, Source = source, Destination = string.Empty };
            _plan.Add(entry);
            if (!IsMovableFile(asset, source))
            { entry.IsError = true; entry.Message = "이동할 수 없는 파일 또는 삭제된 에셋"; continue; }
            if (!validFolder)
            { entry.IsError = true; entry.Message = "Assets 안의 목적지 폴더를 지정하세요."; continue; }
            entry.Destination = folder + "/" + Path.GetFileName(source);
            if (string.Equals(source, entry.Destination, StringComparison.OrdinalIgnoreCase))
            { entry.Message = "이미 목적지에 있음 (건너뜀)"; continue; }
            string error = AssetDatabase.ValidateMoveAsset(source, entry.Destination);
            if (string.IsNullOrEmpty(error) && (File.Exists(entry.Destination) || Directory.Exists(entry.Destination)))
                error = "목적지에 같은 이름의 파일이 있습니다.";
            entry.IsError = !string.IsNullOrEmpty(error);
            entry.CanMove = !entry.IsError;
            entry.Message = entry.IsError ? error : "이동 가능";
        }
        foreach (var group in _plan.Where(e => !string.IsNullOrEmpty(e.Destination))
            .GroupBy(e => e.Destination, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            foreach (var entry in group)
            {
                entry.IsError = true;
                entry.CanMove = false;
                entry.Message = "목록에 같은 파일 이름이 중복됩니다. 하나를 제외하세요.";
            }
        }
    }

    void MoveAssets()
    {
        RefreshPlan();
        if (_plan.Any(e => e.IsError)) return;
        var moves = _plan.Where(e => e.CanMove).ToArray();
        if (moves.Length == 0) return;
        int moved = 0;
        string failure = string.Empty;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var entry in moves)
            {
                string error = AssetDatabase.MoveAsset(entry.Source, entry.Destination);
                if (!string.IsNullOrEmpty(error)) { failure = entry.Source + ": " + error; break; }
                moved++;
            }
        }
        catch (Exception exception) { failure = exception.Message; }
        finally { AssetDatabase.StopAssetEditing(); }
        RefreshPlan();
        ShowMessage(string.IsNullOrEmpty(failure)
            ? $"{moved}개 파일 이동 완료. GUID를 유지한 채 Unity에서 이동했습니다."
            : $"{moved}개 이동 후 중단됐습니다. {failure}");
    }

    VisualElement MakeRow()
    {
        var row = new VisualElement();
        var toolbar = new Toolbar();
        toolbar.Add(new ToolbarButton(() =>
        {
            var entry = row.userData as MoveEntry;
            if (entry == null) return;
            _assets.Remove(entry.Asset);
            RefreshPlan();
        }) { text = "제외" });
        toolbar.Add(new Label { name = "fileName" });
        row.Add(toolbar);
        row.Add(new Label { name = "filePath" });
        row.Add(new Label { name = "fileStatus" });
        return row;
    }

    void BindRow(VisualElement row, int index)
    {
        var entry = _plan[index];
        row.userData = entry;
        row.Q<Label>("fileName").text = string.IsNullOrEmpty(entry.Source) ? "삭제된 에셋" : Path.GetFileName(entry.Source);
        row.Q<Label>("filePath").text = entry.Source + " → " + entry.Destination;
        row.Q<Label>("fileStatus").text = entry.Message;
        row.tooltip = entry.Source + "\n→ " + entry.Destination + "\n" + entry.Message;
    }

    bool IsFolderFieldTarget(EventBase evt)
    {
        var target = evt.target as VisualElement;
        return target != null && (target == _folderField || _folderField.Contains(target));
    }

    void OnDragUpdated(DragUpdatedEvent evt)
    {
        if (IsFolderFieldTarget(evt) || DragAndDrop.objectReferences.Length == 0) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        evt.StopPropagation();
    }

    void OnDragPerform(DragPerformEvent evt)
    {
        if (IsFolderFieldTarget(evt)) return;
        DragAndDrop.AcceptDrag();
        AddAssets(DragAndDrop.objectReferences);
        evt.StopPropagation();
    }

    void ShowMessage(string text)
    {
        _message.text = text;
        if (string.IsNullOrEmpty(text)) _message.RemoveFromHierarchy();
        else if (_message.parent == null) rootVisualElement.Add(_message);
    }
}
