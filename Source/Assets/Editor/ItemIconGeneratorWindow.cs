using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ItemIconTools
{
    public class ItemIconGeneratorWindow : EditorWindow
    {
        [Serializable]
        class PersonalSettings
        {
            public IconCaptureSettings capture;
            public List<IconCaptureEntry> entries;
        }

        [Header("촬영 설정")]
        [SerializeField] IconCaptureSettings _settings;
        [Header("프리팹 목록")]
        [SerializeField] List<IconCaptureEntry> _entries;
        [SerializeField] int _active;
        GameObject _single;
        Vector2 _scroll;
        IconCaptureResult _preview;
        string _message;
        Queue<IconCaptureEntry> _queue;
        int _total;
        int _done;
        int _failed;
        int _skipped;
        bool _cancel;
        bool _previewPending;
        double _nextPreviewTime;

        string PreferenceKey => "ItemIconGenerator." + Hash128.Compute(Application.dataPath);
        bool Busy => _queue != null;

        [MenuItem("Tools/Item Icon Generator")]
        public static void Open() => GetWindow<ItemIconGeneratorWindow>("Item Icon Generator");

        void OnEnable()
        {
            minSize = new Vector2(560, 650);
            _settings = IconCaptureSettings.Defaults();
            _entries = new List<IconCaptureEntry>();
            try
            {
                string json = EditorPrefs.GetString(PreferenceKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var saved = JsonUtility.FromJson<PersonalSettings>(json);
                    if (saved?.capture != null) _settings = saved.capture;
                    if (saved?.entries != null) _entries = saved.entries;
                }
            }
            catch (Exception e) { _message = "개인 설정 읽기 실패: " + e.Message; }
        }

        void OnDisable()
        {
            SavePreferences();
            EditorApplication.update -= ProcessNext;
            EditorApplication.update -= UpdatePreview;
            _previewPending = false;
            _queue = null;
            EditorUtility.ClearProgressBar();
            _preview?.Dispose(); _preview = null;
        }

        void SavePreferences()
        {
            if (_settings == null || _entries == null) return;
            EditorPrefs.SetString(PreferenceKey, JsonUtility.ToJson(new PersonalSettings
                { capture = _settings, entries = _entries }));
        }

        public void AddPrefab(GameObject prefab)
        {
            if (prefab == null || !EditorUtility.IsPersistent(prefab)
                || !PrefabUtility.IsPartOfPrefabAsset(prefab))
            { _message = "Project의 Prefab 또는 FBX 모델을 등록하세요."; return; }
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            if (_entries.Any(e => e.guid == guid)) return;
            var entry = new IconCaptureEntry { guid = guid, selected = true };
            entry.Reset(); _entries.Add(entry); _active = _entries.Count - 1;
            SavePreferences(); SchedulePreview(); Repaint();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("3D 프리팹 → 투명 PNG / UI Sprite", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("원본 에셋과 열린 씬은 수정하지 않습니다. 설정은 이 PC의 프로젝트별 EditorPrefs에 저장됩니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Busy))
            {
                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _single = (GameObject)EditorGUILayout.ObjectField("Prefab", _single, typeof(GameObject), false);
                    if (GUILayout.Button("Add", GUILayout.Width(65))) AddPrefab(_single);
                }
                Rect drop = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
                GUI.Box(drop, "여기에 여러 Prefab / FBX를 드래그하세요");
                var evt = Event.current;
                if (drop.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences) if (obj is GameObject go) AddPrefab(go);
                    }
                    evt.Use();
                }

                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(20));
                        var prefab = entry.Prefab;
                        if (GUILayout.Toggle(_active == i, prefab != null ? prefab.name : "(Missing prefab)", "Button"))
                            _active = i;
                        if (GUILayout.Button("Ping", GUILayout.Width(45))) EditorGUIUtility.PingObject(prefab);
                        if (GUILayout.Button("×", GUILayout.Width(25)))
                        { _entries.RemoveAt(i); _active = Mathf.Clamp(_active, 0, _entries.Count - 1); break; }
                    }
                    if (!string.IsNullOrEmpty(entry.status)) EditorGUILayout.LabelField(entry.status, EditorStyles.wordWrappedMiniLabel);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("공통 촬영 설정", EditorStyles.boldLabel);
                int[] sizes = { 128, 256, 512, 1024 };
                int sizeIndex = Array.IndexOf(sizes, _settings.resolution);
                _settings.resolution = sizes[EditorGUILayout.Popup("해상도", Mathf.Max(0, sizeIndex), new[] { "128", "256", "512", "1024" })];
                using (new EditorGUILayout.HorizontalScope())
                {
                    _settings.outputFolder = EditorGUILayout.TextField("출력 폴더", _settings.outputFolder);
                    if (GUILayout.Button("Browse", GUILayout.Width(70)))
                    {
                        string folder = EditorUtility.OpenFolderPanel("Assets 내부 출력 폴더", Application.dataPath, "");
                        if (!string.IsNullOrEmpty(folder))
                        {
                            try { _settings.outputFolder = IconCaptureService.ValidateFolder(folder); }
                            catch (Exception e) { _message = e.Message; }
                        }
                    }
                }
                _settings.fill = EditorGUILayout.Slider("점유율", _settings.fill, 0.5f, 0.95f);
                _settings.orthographic = EditorGUILayout.Toggle("Orthographic", _settings.orthographic);
                _settings.cameraAngles = EditorGUILayout.Vector3Field("카메라 방향", _settings.cameraAngles);
                _settings.lighting = (IconLighting)EditorGUILayout.EnumPopup("조명", _settings.lighting);
                bool shadowless = _settings.lighting == IconLighting.LitNoShadows;
                using (new EditorGUI.DisabledScope(shadowless))
                {
                    bool shadows = EditorGUILayout.Toggle("복제본 그림자", _settings.RenderShadows);
                    if (!shadowless) _settings.shadows = shadows;
                }
                _settings.removeGloss = EditorGUILayout.Toggle("광택 제거", _settings.removeGloss);
                _settings.blackSilhouette = EditorGUILayout.Toggle("검정 실루엣", _settings.blackSilhouette);
                _settings.overwrite = (IconOverwrite)EditorGUILayout.EnumPopup("중복 파일", _settings.overwrite);
                _settings.filter = (FilterMode)EditorGUILayout.EnumPopup("필터", _settings.filter);
                _settings.compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("압축", _settings.compression);

                if (_active >= 0 && _active < _entries.Count)
                {
                    var entry = _entries[_active];
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("선택 모델 보정", EditorStyles.boldLabel);
                    entry.autoFit = EditorGUILayout.Toggle("자동 맞춤", entry.autoFit);
                    entry.scale = Mathf.Max(0.01f, EditorGUILayout.FloatField("수동 배율", entry.scale));
                    entry.offset = EditorGUILayout.Vector2Field("화면 오프셋 (화면 비율)", entry.offset);
                    entry.rotation = EditorGUILayout.Vector3Field("모델 회전", entry.rotation);
                    if (GUILayout.Button("개별 설정 초기화")) { entry.Reset(); SchedulePreview(); }
                }
                if (EditorGUI.EndChangeCheck()) { SavePreferences(); SchedulePreview(); }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Preview")) RefreshPreview();
                    if (GUILayout.Button("Generate Selected")) StartBatch(true);
                    if (GUILayout.Button("Generate All")) StartBatch(false);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("공유 설정 내보내기")) ExportSettings();
                    if (GUILayout.Button("공유 설정 가져오기")) ImportSettings();
                }
            }
            if (Busy && GUILayout.Button("Cancel")) _cancel = true;
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            if (_preview?.texture != null)
            {
                float side = Mathf.Min(position.width - 40, 400);
                Rect rect = GUILayoutUtility.GetRect(side, side);
                EditorGUI.DrawTextureTransparent(rect, _preview.texture, ScaleMode.ScaleToFit);
                EditorGUILayout.LabelField($"픽셀 점유율: {_preview.occupancy:P0}");
                if (_preview.touchesEdge)
                    EditorGUILayout.HelpBox("가장자리 픽셀이 닿습니다. 배율 또는 오프셋을 줄이세요.", MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        void SchedulePreview()
        {
            _previewPending = true;
            EditorApplication.update -= UpdatePreview;
            EditorApplication.update += UpdatePreview;
        }

        void UpdatePreview()
        {
            if (!_previewPending || Busy || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.timeSinceStartup < _nextPreviewTime) return;
            RefreshPreview();
        }

        void RefreshPreview()
        {
            _previewPending = false;
            EditorApplication.update -= UpdatePreview;
            _nextPreviewTime = EditorApplication.timeSinceStartup + 0.1;
            if (_active < 0 || _active >= _entries.Count) { _message = "Prefab을 등록하세요."; return; }
            _preview?.Dispose(); _preview = null;
            try
            {
                var entry = _entries[_active];
                _preview = IconCaptureService.Capture(entry.Prefab, _settings, entry);
                _message = "미리보기 자동 갱신 중. 회전·배율·위치를 조절하면 바로 반영됩니다.";
            }
            catch (Exception e) { _message = e.Message; }
            Repaint();
        }

        void StartBatch(bool selectedOnly)
        {
            try { IconCaptureService.ValidateFolder(_settings.outputFolder); }
            catch (Exception e) { _message = e.Message; return; }
            var entries = _entries.Where(e => !selectedOnly || e.selected).ToList();
            if (entries.Count == 0) { _message = "생성할 항목을 선택하세요."; return; }
            _queue = new Queue<IconCaptureEntry>(entries);
            _total = _queue.Count; _done = 0; _failed = 0; _skipped = 0; _cancel = false;
            EditorApplication.update += ProcessNext;
        }

        void ProcessNext()
        {
            if (_queue == null) return;
            if (_cancel || _queue.Count == 0)
            {
                _message = $"{(_cancel ? "취소됨" : "완료")}: 성공 {_done}, 건너뜀 {_skipped}, 실패 {_failed}, 남음 {_queue.Count}";
                _queue = null; EditorApplication.update -= ProcessNext;
                EditorUtility.ClearProgressBar(); SavePreferences(); Repaint(); return;
            }
            var entry = _queue.Dequeue();
            var prefab = entry.Prefab;
            string name = prefab != null ? prefab.name : "(Missing)";
            if (EditorUtility.DisplayCancelableProgressBar("Item Icon Generator", name,
                (_total - _queue.Count - 1) / (float)_total))
            { _queue.Enqueue(entry); _cancel = true; return; }
            try
            {
                using (var capture = IconCaptureService.Capture(prefab, _settings, entry))
                {
                    string output = IconCaptureService.Save(capture, name, _settings);
                    if (output == null) { _skipped++; entry.status = "건너뜀: 기존 PNG"; }
                    else
                    {
                        _done++; entry.output = output;
                        entry.status = $"완료: {output} · 점유율 {capture.occupancy:P0}"
                            + (capture.touchesEdge ? " · 가장자리 잘림 주의" : "");
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Sprite>(output));
                    }
                }
            }
            catch (Exception e)
            { _failed++; entry.status = "실패: " + e.Message; Debug.LogWarning($"[Item Icon Generator] {name}: {e.Message}"); }
            _message = $"진행 {_total - _queue.Count}/{_total} · 성공 {_done} / 실패 {_failed}";
            Repaint();
        }

        void ExportSettings()
        {
            string path = EditorUtility.SaveFilePanel("공유 설정 저장", "", "ItemIconSettings", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(new PersonalSettings { capture = _settings, entries = _entries }, true));
                _message = "공유 JSON 저장 완료. Prefab은 GUID로 연결됩니다.";
            }
            catch (Exception e) { _message = e.Message; }
        }

        void ImportSettings()
        {
            string path = EditorUtility.OpenFilePanel("공유 설정 불러오기", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var data = JsonUtility.FromJson<PersonalSettings>(File.ReadAllText(path));
                if (data?.capture == null || data.entries == null) throw new FormatException("지원하지 않는 설정 파일입니다.");
                _settings = data.capture;
                _entries = data.entries.Where(e => e != null).GroupBy(e => e.guid).Select(g => g.First()).ToList();
                _active = 0; SavePreferences();
                _preview?.Dispose(); _preview = null;
                _message = "공유 설정 불러오기 완료.";
                SchedulePreview();
            }
            catch (Exception e) { _message = e.Message; }
        }
    }
}
