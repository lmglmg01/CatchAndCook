using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Effect = UIDirectorView.Effect;

public sealed partial class CanvasEditor
{
    const string DirectorPresetFolder = "Assets/Editor/CanvasEditor/Presets";
    [SerializeField] string _directorLibraryGuid;
    readonly List<UIEditorDirectorPreset> _directorLibraryAssets = new();
    PopupField<string> _directorLibrary, _directorSaveScope;
    TextField _directorSaveName;
    Label _directorLibraryInfo;
    Button _directorSaveNew, _directorOverwrite, _directorLoad, _directorAppend;

    CanvasEditorPresetWindow _directorPresetWindow;

    /// <summary>4번 효과 설정 영역 하단에 저장과 불러오기 버튼을 배치합니다.</summary>
    void BuildDirectorLibraryButtons(VisualElement details)
    {
        var row = Row(details, "director-library-actions");
        AddButton(row, "연출 불러오기", () => OpenDirectorPresetDialog(false)).name = "director-open-presets";
        AddButton(row, "연출 저장하기", () => OpenDirectorPresetDialog(true)).name = "director-save-presets";
    }

    /// <summary>기존 보조창을 정리하고 선택한 용도의 창 하나만 엽니다.</summary>
    void OpenDirectorPresetDialog(bool save)
    {
        CloseDirectorPresetDialog();
        _directorPresetWindow = ScriptableObject.CreateInstance<CanvasEditorPresetWindow>();
        _directorPresetWindow.Initialize(this, save, _directorOriginal, _directorExtraTrack, _directorExit);
        _directorPresetWindow.ShowUtility();
    }

    /// <summary>열 때의 대상과 유형이 유지되는지 확인합니다.</summary>
    internal bool MatchesPresetContext(RectTransform target, int track, bool exit) =>
        target == _directorOriginal && track == _directorExtraTrack && exit == _directorExit;

    /// <summary>보조창이 현재 편집 상태에 맞게 버튼 사용 여부를 갱신하도록 합니다.</summary>
    internal void RefreshDirectorPresetDialog() => RefreshDirectorLibraryState();

    /// <summary>닫힌 보조창의 참조와 해당 창의 입력 필드를 해제합니다.</summary>
    internal void ReleaseDirectorPresetDialog(CanvasEditorPresetWindow window)
    {
        if (_directorPresetWindow != window) return;
        _directorPresetWindow = null;
        _directorLibraryInfo = null;
        _directorLibrary = null;
    }

    /// <summary>편집기 종료나 다른 용도 창을 열 때 연결된 보조창을 닫습니다.</summary>
    void CloseDirectorPresetDialog()
    {
        if (_directorPresetWindow != null) _directorPresetWindow.Close();
    }

    #region 저장 연출 보조창
    /// <summary>보조창 용도에 맞는 저장·불러오기 설정을 구성합니다.</summary>
    internal void BuildDirectorLibraryDialog(VisualElement parent, bool saveMode, CanvasEditorPresetWindow window)
    {
        _directorPresetWindow = window;
        var panel = Panel(parent, saveMode ? "내 저장 연출 · 연출 저장하기" : "내 저장 연출 · 연출 불러오기", "director-library");
        Note(panel, "대상: " + (_directorOriginal != null ? _directorOriginal.name : "선택 없음") + " · 유형: " + DirectorTrackName);
        var choose = Row(panel, "director-library-row");
        _directorLibrary = new PopupField<string>("저장한 연출", new List<string>{"저장된 연출 없음"}, 0);
        _directorLibrary.name = "director-saved-presets";
        _directorLibrary.AddToClassList("director-library-picker");
        choose.Add(_directorLibrary);
        _directorLibrary.RegisterValueChangedCallback(_ =>
        {
            var selected = SelectedDirectorPreset();
            _directorLibraryGuid = selected != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selected)) : "";
            if (selected != null) _directorSaveName.SetValueWithoutNotify(selected.displayName);
            RefreshDirectorLibraryState();
        });
        _directorLoad = AddButton(choose, "교체하여 불러오기", () => LoadDirectorPreset(false));
        _directorAppend = AddButton(choose, "기존 효과 뒤에 추가", () => LoadDirectorPreset(true));
        AddButton(choose, "목록 새로고침", RefreshDirectorLibrary);
        AddButton(choose, "에셋 위치", () => { var asset = SelectedDirectorPreset(); if (asset != null) EditorGUIUtility.PingObject(asset); });
        var save = Row(panel, "director-library-row");
        _directorSaveName = new TextField("저장 이름");
        _directorSaveName.name = "director-preset-name";
        _directorSaveName.AddToClassList("director-library-picker");
        save.Add(_directorSaveName);
        _directorSaveScope = new PopupField<string>(new List<string>{"현재 유형만", "전체 4유형"}, 0);
        save.Add(_directorSaveScope);
        _directorSaveName.RegisterValueChangedCallback(_ => RefreshDirectorLibraryState());
        _directorSaveScope.RegisterValueChangedCallback(_ => RefreshDirectorLibraryState());
        _directorSaveNew = AddButton(save, "새 연출로 저장", () => StoreDirectorPreset(null));
        _directorOverwrite = AddButton(save, "선택한 저장본 덮어쓰기", () =>
        {
            var asset = SelectedDirectorPreset();
            if (asset != null && EditorUtility.DisplayDialog("저장 연출 갱신", asset.displayName + "의 저장된 조합을 현재 구성으로 바꿀까요?", "덮어쓰기", "취소"))
                StoreDirectorPreset(asset);
        });
        Display(save, saveMode);
        Display(_directorLoad, !saveMode);
        Display(_directorAppend, !saveMode);
        _directorLibraryInfo = new Label();
        _directorLibraryInfo.AddToClassList("director-library-info");
        panel.Add(_directorLibraryInfo);
        RefreshDirectorLibrary();
    }

    /// <summary>프로젝트에 저장한 모든 사용자 조합을 다시 찾아 창 재실행 후에도 선택할 수 있게 합니다.</summary>
    void RefreshDirectorLibrary()
    {
        if (_directorLibrary == null) return;
        _directorLibraryAssets.Clear();
        _directorLibraryAssets.AddRange(AssetDatabase.FindAssets("t:UIEditorDirectorPreset")
            .Select(g => AssetDatabase.LoadAssetAtPath<UIEditorDirectorPreset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null).OrderBy(a => a.displayName).ThenBy(AssetDatabase.GetAssetPath));
        var labels = _directorLibraryAssets.Select(a => $"{a.displayName} · {(a.allTracks ? "전체 4유형" : a.sourceTrack)} · {AssetDatabase.GetAssetPath(a)}").ToList();
        if (labels.Count == 0) labels.Add("저장된 연출 없음");
        _directorLibrary.choices = labels;
        int index = _directorLibraryAssets.FindIndex(a => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a)) == _directorLibraryGuid);
        index = Mathf.Max(0, index);
        _directorLibrary.SetValueWithoutNotify(labels[index]);
        var selected = SelectedDirectorPreset();
        _directorLibraryGuid = selected != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(selected)) : "";
        RefreshDirectorLibraryState();
    }

    /// <summary>드롭다운에서 선택한 실제 저장 에셋을 반환합니다.</summary>
    UIEditorDirectorPreset SelectedDirectorPreset() => _directorLibrary != null && _directorLibrary.index >= 0 &&
        _directorLibrary.index < _directorLibraryAssets.Count ? _directorLibraryAssets[_directorLibrary.index] : null;

    /// <summary>저장 대상과 조합 상태에 따라 버튼 사용 가능 여부와 저장 범위를 안내합니다.</summary>
    void RefreshDirectorLibraryState()
    {
        if (_directorLibraryInfo == null || _directorPresetWindow == null) return;
        var asset = SelectedDirectorPreset();
        bool editable = _directorPresetWindow.ContextValid && _directorDraft != null && _directorOriginal != null;
        bool hasEffects = editable && (_directorSaveScope.index == 1
            ? new[]{_directorDraft.enter, _directorDraft.exit, _directorDraft.loop, _directorDraft.reaction}.Any(list => list.Any(e => e.enabled))
            : DirectorEffects.Any(e => e.enabled));
        bool save = editable && !IsTestCanvas && hasEffects && !string.IsNullOrWhiteSpace(_directorSaveName.value);
        _directorSaveNew.SetEnabled(save);
        _directorOverwrite.SetEnabled(save && asset != null);
        _directorLoad.SetEnabled(editable && asset != null);
        _directorAppend.SetEnabled(editable && asset != null);
        _directorLibrary.SetEnabled(_directorLibraryAssets.Count > 0);
        _directorLibraryInfo.text = !_directorPresetWindow.ContextValid ? "대상 또는 유형이 바뀌었습니다. 이 창을 닫고 현재 대상에서 다시 열어 주세요."
            : IsTestCanvas ? "테스트 UI: 저장 불가 · 저장본 불러오기와 미리보기만 가능합니다."
            : asset == null ? "조합에 이름을 붙여 저장하세요. 대상·함수·버튼 연결은 저장하지 않습니다."
            : (asset.allTracks ? "전체 4유형을 각각 불러옵니다." : "선택한 저장 조합을 현재 ‘" + DirectorTrackName + "’ 유형에 불러옵니다.")
              + " 기존 함수·버튼 연결 유지 · 미리보기 확인 후 변경 적용";
    }
    #endregion

    #region 조합 저장과 재사용
    /// <summary>조합을 독립 에셋으로 저장하고 기존 대상의 함수나 버튼 참조는 복사하지 않습니다.</summary>
    void StoreDirectorPreset(UIEditorDirectorPreset existing)
    {
        if (_directorPresetWindow == null || !_directorPresetWindow.ContextValid || _directorDraft == null || IsTestCanvas || string.IsNullOrWhiteSpace(_directorSaveName.value)) return;
        bool all = _directorSaveScope.index == 1;
        var lists = all ? new[]{_directorDraft.enter, _directorDraft.exit, _directorDraft.loop, _directorDraft.reaction} : new[]{DirectorEffects};
        if (!lists.Any(list => list.Any(e => e.enabled))) return;
        foreach (var list in lists)
        {
            string error = UIDirectorView.Validate(list);
            if (error != null) { DirectorMessage(error); return; }
        }
        var asset = existing != null ? existing : ScriptableObject.CreateInstance<UIEditorDirectorPreset>();
        if (existing != null) Undo.RecordObject(asset, "저장 연출 덮어쓰기");
        asset.displayName = _directorSaveName.value.Trim();
        asset.allTracks = all; asset.sourceTrack = DirectorTrackName; asset.loopYoyo = _directorDraft.loopYoyo;
        asset.effects = all ? new List<Effect>() : UIEditorDirectorPreset.Copy(DirectorEffects);
        asset.enter = all ? UIEditorDirectorPreset.Copy(_directorDraft.enter) : new List<Effect>();
        asset.exit = all ? UIEditorDirectorPreset.Copy(_directorDraft.exit) : new List<Effect>();
        asset.loop = all ? UIEditorDirectorPreset.Copy(_directorDraft.loop) : new List<Effect>();
        asset.reaction = all ? UIEditorDirectorPreset.Copy(_directorDraft.reaction) : new List<Effect>();
        if (existing == null)
        {
            if (!AssetDatabase.IsValidFolder(DirectorPresetFolder)) AssetDatabase.CreateFolder("Assets/Editor/CanvasEditor", "Presets");
            string filename = string.Concat(asset.displayName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(filename)) filename = "MyAnimation";
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(DirectorPresetFolder + "/" + filename + ".asset"));
        }
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        _directorLibraryGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
        RefreshDirectorLibrary();
        DirectorMessage("연출 저장 완료: " + asset.displayName);
    }

    /// <summary>설정만 깊은 복사하여 편집 상태로 불러옵니다. 적용 전에는 원본 UI에 기록하지 않습니다.</summary>
    void LoadDirectorPreset(bool append)
    {
        var asset = SelectedDirectorPreset();
        if (_directorPresetWindow == null || !_directorPresetWindow.ContextValid || asset == null || _directorDraft == null || _directorOriginal == null) return;
        DirectorRemember();
        void Load(List<Effect> destination, List<Effect> source)
        {
            if (append) UIEditorDirectorPreset.Append(destination, source);
            else { destination.Clear(); destination.AddRange(UIEditorDirectorPreset.Copy(source)); }
        }
        if (asset.allTracks)
        {
            Load(_directorDraft.enter, asset.enter); Load(_directorDraft.exit, asset.exit);
            Load(_directorDraft.loop, asset.loop); Load(_directorDraft.reaction, asset.reaction);
            _directorDraft.loopYoyo = asset.loopYoyo;
        }
        else
        {
            Load(DirectorEffects, asset.effects);
            if (_directorExtraTrack == 1) _directorDraft.loopYoyo = asset.loopYoyo;
        }
        _directorSelected = 0;
        RefreshDirector();
    }
    #endregion
}
