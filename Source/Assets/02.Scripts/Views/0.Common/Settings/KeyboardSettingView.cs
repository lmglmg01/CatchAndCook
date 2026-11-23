using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// 상황별 조작 목록과 키 변경 대기를 표시하고 실제 입력 바인딩 변경을 적용하는 뷰입니다.
/// </summary>
public class KeyboardSettingView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] SettingPreferencesView _preferences; // 공통 Canvas의 설정 저장 뷰를 연결합니다.
    [SerializeField] SettingView _owner; // 같은 Setting 루트의 메인 설정 뷰를 연결합니다.
    [SerializeField] KeyBindingSlotView[] _rows; // 사진 배치는 왼쪽 열부터 4개씩 12개를 연결합니다. 기존 탭 배치는 열별 7개씩 연결합니다.
    [SerializeField] TMP_Text[] _groupTitles; // 스크롤 Content 안의 세 열 제목을 왼쪽부터 연결합니다.
    [SerializeField] Button[] _pageButtons; // 선상·잠수·인벤토리 탭 Button을 순서대로 연결합니다.
    [SerializeField] TMP_Text _pageTitleText; // 현재 상황을 표시할 제목 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _noticeText; // 스크롤 밖 하단의 입력 대기·중복·완료 안내 TMP_Text를 연결합니다.
    [SerializeField] ScrollRect _scrollRect; // 키 목록 Scroll View의 ScrollRect를 연결합니다.
    [SerializeField] Button _resetButton; // 하단 기본값 복원 Button을 연결합니다.
    [SerializeField] Button _backButton; // 하단 뒤로 Button을 연결합니다.
    [SerializeField] Button _closeButton; // 상단 닫기 Button을 연결합니다. 메인 설정으로 돌아갑니다.
    [SerializeField] bool _mergedLayout; // 기존 통합 키 목록을 사용합니다. 사진 배치는 아래 _referenceLayout 설정을 우선합니다.
    [SerializeField] bool _referenceLayout; // 사진 기준 3열×4행 설정 화면에서 켭니다. _rows에는 열별로 위→아래 순서로 12개를 연결합니다.

    #endregion

    #region 런타임 상태

    const string ReferenceKeyPrefix = "CTeam.Settings.ReferenceKeys.v1."; // 기존 버전의 표시용 키 저장값을 정리할 때 사용하는 접두사입니다.
    readonly List<Entry> _entries = new List<Entry>(); // 기획 목록과 실제 입력 연결을 구분해 보관합니다.
    readonly List<InputAction> _suspendedActions = new List<InputAction>(); // 키 변경 전 활성화되어 있던 입력만 복원합니다.
    readonly List<Action<int>> _rowCallbacks = new List<Action<int>>(); // 행 제거 시 선택 이벤트를 해제할 콜백입니다.
    readonly List<UnityEngine.Events.UnityAction> _pageCallbacks = new List<UnityEngine.Events.UnityAction>(); // 탭 이벤트의 중복 등록을 방지합니다.
    int _page, _selected = -1, _listenAfterFrame; // 현재 탭·선택 항목·선택 클릭과 키 입력을 구분할 프레임입니다.
    bool _initialized; // 이벤트와 목록의 최초 준비 여부입니다.

    #endregion

    #region 프로퍼티

    public bool IsListening => _selected >= 0; // 키 변경 입력 대기 여부입니다.
    public int PageIndex => _page; // 현재 선상·잠수·인벤토리 탭 인덱스입니다.

    #endregion

    #region 표시 데이터 형식

    struct Entry
    {

        #region 멤버

        #region 런타임 상태

        public int Page, Column; // 상황 탭과 표시 열입니다.
        public string Role, Description, Action, Binding, FixedKey; // 역할·설명·실제 액션 경로·원래 바인딩 경로·고정 안내 문구입니다.

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 실제 UI 행과 기획 조작 목록을 초기화합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _backButton, _closeButton);
        Initialize();
    }

    /// <summary>
    /// 키 설정을 열 때 목록을 갱신하고 안내를 표시합니다.
    /// </summary>
    void OnEnable()
    {
        Initialize();
        if (_preferences != null)
        {
            _preferences.OnBindingsChanged += Refresh;
        }

        SelectPage(_page);
    }

    /// <summary>입력칸을 선택하고 누른 키를 적용합니다. ESC는 키 변경 대기만 취소합니다.</summary>
    void Update()
    {
        if (Keyboard.current == null || !IsListening || Time.frameCount <= _listenAfterFrame) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame) { CancelRebind(); return; }
        foreach (KeyControl key in Keyboard.current.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            TryApplyKey("<Keyboard>/" + key.name);
            break;
        }
    }

    /// <summary>
    /// 페이지가 숨겨지거나 다른 앱으로 전환되면 입력 대기를 취소합니다.
    /// </summary>
    void OnDisable()
    {
        CancelRebind();
        if (_preferences != null)
        {
            _preferences.OnBindingsChanged -= Refresh;
        }
    }

    /// <summary>
    /// View 제거 시 코드에서 등록한 UI 이벤트를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        CancelRebind();
        if (_rows != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _rows.Length && i < _rowCallbacks.Count; i++)
            {
                if (_rows[i] != null)
                {
                    _rows[i].OnSelected -= _rowCallbacks[i];
                }
            }
        }

        if (_pageButtons != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _pageButtons.Length && i < _pageCallbacks.Count; i++)
            {
                if (_pageButtons[i] != null)
                {
                    _pageButtons[i].onClick.RemoveListener(_pageCallbacks[i]);
                }
            }
        }

        if (_resetButton != null)
        {
            _resetButton.onClick.RemoveListener(ResetBindings);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(Back);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Back);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 조작 목록과 버튼을 한 번만 준비합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        BuildEntries();

        if (_rows != null)
        {
            // row: 행 값입니다.
            foreach (var row in _rows)
            {
                Action<int> callback = BeginRebind; // 콜백 값입니다.
                _rowCallbacks.Add(callback);
                if (row != null)
                {
                    row.OnSelected += callback;
                }
            }
        }

        if (_pageButtons != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _pageButtons.Length; i++)
            {
                int page = i; // 페이지 값입니다.
                UnityEngine.Events.UnityAction callback = () => SelectPage(page); // 콜백 값입니다.
                _pageCallbacks.Add(callback);
                if (_pageButtons[i] != null)
                {
                    _pageButtons[i].onClick.AddListener(callback);
                }
            }
        }

        if (_resetButton != null)
        {
            _resetButton.onClick.AddListener(ResetBindings);
        }

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(Back);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Back);
        }
    }

    /// <summary>
    /// 추가한 키 변경을 해제하고 기존 프로젝트 설정으로 되돌립니다.
    /// </summary>
    public void ResetBindings()
    {
        CancelRebind();
        SuspendInputs();
        try
        {
            if (_preferences != null)
            {
                _preferences.ResetBindings();
            }
        }
        finally
        {
            RestoreInputs();
        }

        if (_referenceLayout)
        {
            // entry: 항목 값입니다.
            foreach (var entry in _entries)
            {
                PlayerPrefs.DeleteKey(ReferenceKeyPrefix + entry.Role);
            }

            PlayerPrefs.Save();
        }

        Refresh();
        Notice("기본 키 설정으로 돌아갔습니다. WASD 외의 키는 현재 임시 설정입니다.");
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 하나의 역할을 추가합니다. 액션이 없는 미정 항목은 가짜 키 기능을 만들지 않습니다.
    /// </summary>
    /// <param name="page">페이지 값입니다.</param>
    /// <param name="column">열 값입니다.</param>
    /// <param name="role">역할 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="action">동작 값입니다.</param>
    /// <param name="binding">키 할당 값입니다.</param>
    /// <param name="fixedKey">고정 키 값입니다.</param>
    void Add(int page, int column, string role, string description, string action = null, string binding = null, string fixedKey = null)
    {
        _entries.Add(new Entry { Page = page, Column = column, Role = role, Description = description, Action = action, Binding = binding, FixedKey = fixedKey });
    }

    /// <summary>
    /// 통합된 키 설정 항목의 현재 할당과 선택 상태를 갱신합니다.
    /// </summary>
    void RefreshMerged()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_rows == null)
        {
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i]; // 행 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (row == null)
            {
                continue;
            }

            // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
            if (i >= _entries.Count)
            {
                row.gameObject.SetActive(false);
                continue;
            }

            var entry = _entries[i]; // 항목 값입니다.
            var action = Resolve(entry, out int binding); // 동작 값입니다.
            bool editable = action != null && binding >= 0; // 편집 가능 여부입니다.
            string key = editable ? DisplayKey(action.bindings[binding].effectivePath) : entry.FixedKey ?? "미정"; // 키 값입니다.
            row.Bind(i, entry.Role, entry.Description, key, editable, i == _selected);
            row.gameObject.SetActive(true);
        }

        if (_pageButtons != null)
        {
            // button: 버튼 값입니다.
            foreach (var button in _pageButtons)
            {
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }
        }

        if (_groupTitles != null)
        {
            // title: 제목 값입니다.
            foreach (var title in _groupTitles)
            {
                if (title != null)
                {
                    title.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// 선택한 상황의 역할을 세 열에 표시하고 남는 행은 숨깁니다.
    /// </summary>
    public void Refresh()
    {
        Initialize();
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_referenceLayout)
        {
            RefreshReference();
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_mergedLayout)
        {
            RefreshMerged();
            return;
        }

        string[] pages =
        { "선상 조작", "잠수 조작", "인벤토리 조작" }; // 페이지 목록입니다.
        string[][] titles =
        {
            new[]
            { "이동 · 상호작용", "선택 · 취소", "마우스 조작 안내" },
            new[]
            { "이동 · 상호작용", "무기", "아이템 · 메뉴" },
            new[]
            { "열기 · 닫기", "아이템 조작 안내", "기획 안내" }
        }; // 제목 목록입니다.
        if (_pageTitleText != null)
        {
            _pageTitleText.text = pages[_page];
        }

        if (_groupTitles != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _groupTitles.Length && i < 3; i++)
            {
                if (_groupTitles[i] != null)
                {
                    _groupTitles[i].text = titles[_page][i];
                }
            }
        }

        if (_pageButtons != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _pageButtons.Length; i++)
            {
                if (_pageButtons[i] != null)
                {
                    _pageButtons[i].interactable = i != _page;
                }
            }
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_rows == null)
        {
            return;
        }

        // row: 행 값입니다.
        foreach (var row in _rows)
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
            }
        }

        int[] counts = new int[3]; // 개수 목록입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i]; // 항목 값입니다.
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (entry.Page != _page)
            {
                continue;
            }

            int rowIndex = entry.Column * 7 + counts[entry.Column]++; // 행 인덱스 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (rowIndex >= _rows.Length || _rows[rowIndex] == null)
            {
                continue;
            }

            var action = Resolve(entry, out int binding); // 동작 값입니다.
            bool editable = action != null && binding >= 0; // 편집 가능 여부입니다.
            string key = editable ? DisplayKey(action.bindings[binding].effectivePath) : entry.FixedKey ?? "미정"; // 키 값입니다.
            _rows[rowIndex].Bind(i, entry.Role, entry.Description, key, editable, i == _selected);
            _rows[rowIndex].gameObject.SetActive(true);
        }

        if (_scrollRect != null && _scrollRect.content != null)
        {
            int largest = Mathf.Max(counts[0], Mathf.Max(counts[1], counts[2])); // 가장 큰 값입니다.
            float viewportHeight = _scrollRect.viewport != null ? _scrollRect.viewport.rect.height : 440f; // 표시 영역 높이 값입니다.
            float height = Mathf.Max(viewportHeight, 70f + largest * 76f); // 높이 값입니다.
            _scrollRect.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (_groupTitles != null)
            {
                // title: 제목 값입니다.
                foreach (var title in _groupTitles)
                {
                    if (title != null && title.transform.parent is RectTransform area)
                    {
                        area.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 키보드와 마우스의 현재 할당을 키 아이콘 안의 짧은 이름으로 변환합니다.
    /// </summary>
    /// <param name="path">경로 값입니다.</param>
    static string DisplayKey(string path)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (string.IsNullOrEmpty(path))
        {
            return "미정";
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (path == "<Mouse>/leftButton")
        {
            return "좌클릭";
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (path == "<Mouse>/rightButton")
        {
            return "우클릭";
        }

        string name = path.Substring(path.LastIndexOf('/') + 1); // 이름 값입니다.
        switch (name)
        {
            case "{Submit}":
                return "Enter";
            case "space":
                return "Space";
            case "leftAlt":
                return "Alt L";
            case "rightAlt":
                return "Alt R";
            case "leftShift":
                return "Shift L";
            case "rightShift":
                return "Shift R";
            case "upArrow":
                return "↑";
            case "downArrow":
                return "↓";
            case "leftArrow":
                return "←";
            case "rightArrow":
                return "→";
            default:
                return name.Length == 1 ? name.ToUpperInvariant() : name;
        }
    }

    /// <summary>
    /// 하나의 키 아이콘만 활성화하고 기존 게임 입력을 잠시 중지합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    public void BeginRebind(int index)
    {
        Initialize();
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (_selected == index) return;

        CancelRebind();
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (Resolve(_entries[index], out int binding) == null || binding < 0)
        {
            return;
        }

        _selected = index;
        // 아이콘을 선택한 현재 프레임만 제외하고 다음 프레임부터 새 키를 받습니다.
        _listenAfterFrame = Time.frameCount;
        SuspendInputs();
        Notice(_entries[index].Role + "에 사용할 키를 누르세요.");
        Refresh();
    }

    /// <summary>
    /// 새 키가 다른 게임 조작에 사용되지 않을 때만 변경하고 저장합니다.
    /// </summary>
    /// <param name="path">경로 값입니다.</param>
    public bool TryApplyKey(string path)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (!IsListening || Keyboard.current == null || string.IsNullOrEmpty(path) || !path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool known = false; // 확인된 대상 여부입니다.
        // key: 키 값입니다.
        foreach (var key in Keyboard.current.allKeys)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (string.Equals("<Keyboard>/" + key.name, path, StringComparison.OrdinalIgnoreCase))
            {
                path = "<Keyboard>/" + key.name;
                known = true;
                break;
            }
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!known)
        {
            return false;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_referenceLayout)
        {
            return TryApplyReferenceKey(path);
        }

        var action = Resolve(_entries[_selected], out int binding); // 동작 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (action == null || binding < 0)
        {
            CancelRebind();
            return false;
        }

        var map = action.actionMap; // 매핑 값입니다.
        var checkedActions = new List<InputAction>(); // 확인 동작 목록 값입니다.
        if (_mergedLayout)
        {
            // candidate: 후보 값입니다.
            foreach (var candidate in _preferences.InputActions.actionMaps)
            {
                if (candidate.name == "Hero" || candidate.name == "UI")
                {
                    checkedActions.AddRange(candidate.actions);
                }
            }
        }
        else
        {
            checkedActions.AddRange(map.actions);
        }

        // other: 다른 대상 값입니다.
        foreach (var other in checkedActions)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < other.bindings.Count; i++)
            {
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (other == action && i == binding)
                {
                    continue;
                }

                // 이동과 메뉴 이동은 의도적으로 같은 방향 키를 공유합니다.
                if (_mergedLayout && action.name == "Move" && other.actionMap.name == "UI" && other.name == "Navigate" && other.bindings[i].path == _entries[_selected].Binding)
                {
                    continue;
                }

                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (!string.Equals(other.bindings[i].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Notice("이미 할당된 키입니다. 다른 키를 선택해주세요.", true);
                return false;
            }
        }

        string role = _entries[_selected].Role; // 역할 값입니다.
        action.ApplyBindingOverride(binding, path);
        if (_mergedLayout && _entries[_selected].Action == "Hero/Move")
        {
            var navigation = _preferences.InputActions.FindAction("UI/Navigate", false); // 이동 값입니다.
            if (navigation != null)
            {
                // i: 반복 값입니다.
                for (int i = 0; i < navigation.bindings.Count; i++)
                {
                    if (navigation.bindings[i].path == _entries[_selected].Binding)
                    {
                        navigation.ApplyBindingOverride(i, path);
                    }
                }
            }
        }

        _selected = -1;
        RestoreInputs();
        if (_preferences != null)
        {
            _preferences.SaveBindings();
        }

        Refresh();
        Notice(role + " 키를 " + DisplayKey(path) + "(으)로 변경했습니다.");
        return true;
    }

    /// <summary>
    /// 키 설정이 게임 행동이나 메뉴 닫기로 전달되지 않도록 원래 켜진 입력만 기록해 중지합니다.
    /// </summary>
    void SuspendInputs()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_preferences == null || _preferences.InputActions == null)
        {
            return;
        }

        // map: 매핑 값입니다.
        foreach (var map in _preferences.InputActions.actionMaps)
        {
            // action: 동작 값입니다.
            foreach (var action in map.actions)
            {
                bool pointerUI = map.name == "UI" && (action.name == "Point" || action.name == "Click" || action.name == "RightClick" || action.name == "MiddleClick" || action.name == "ScrollWheel" || action.name.StartsWith("TrackedDevice", StringComparison.Ordinal)); // 포인터 UI 여부입니다.
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (!action.enabled || pointerUI)
                {
                    continue;
                }

                _suspendedActions.Add(action);
                action.Disable();
            }
        }
    }

    /// <summary>
    /// 메인 설정으로 돌아가기 전에 입력 대기를 종료합니다.
    /// </summary>
    void Back()
    {
        CancelRebind();
        if (_owner != null)
        {
            if (_mergedLayout)
            {
                _owner.CloseInlineSettings();
            }
            else
            {
                _owner.ReturnToMain();
            }
        }
    }

    /// <summary>
    /// 화면 하단의 고정 안내 영역에 조작 상태나 중복 메시지를 표시합니다.
    /// </summary>
    /// <param name="text">텍스트 값입니다.</param>
    /// <param name="error">오류 여부입니다.</param>
    void Notice(string text, bool error = false)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_noticeText == null)
        {
            return;
        }

        _noticeText.text = text;
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_mergedLayout)
        {
            _noticeText.color = error ? new Color(1f, .55f, .35f) : new Color(.7f, .85f, .88f);
            return;
        }

        _noticeText.color = error ? new Color(.75f, .18f, .12f) : new Color(.16f, .38f, .35f);
    }

    /// <summary>
    /// 저장한 사용자 키를 우선하고 실제 액션에 연결된 기존 사용자 변경도 유지합니다.
    /// </summary>
    /// <param name="entry">항목 값입니다.</param>
    string ReferencePath(Entry entry)
    {
        var action = Resolve(entry, out int binding);
        return action != null && binding >= 0 ? action.bindings[binding].effectivePath : entry.FixedKey;
    }

    /// <summary>
    /// 사진의 12개 행만 표시하고 현재 키와 변경 대기 상태를 갱신합니다.
    /// </summary>
    void RefreshReference()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_rows == null)
        {
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i]; // 행 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (row == null)
            {
                continue;
            }

            row.gameObject.SetActive(i < _entries.Count);
            // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
            if (i >= _entries.Count)
            {
                continue;
            }

            var entry = _entries[i]; // 항목 값입니다.
            string key = DisplayKey(ReferencePath(entry)).Replace(" L", string.Empty).ToUpperInvariant(); // 키 값입니다.
            if (key == "ESCAPE")
            {
                key = "ESC";
            }

            bool editable = Resolve(entry, out int binding) != null && binding >= 0;
            row.Bind(i, entry.Role, string.Empty, key, editable, i == _selected);
        }
    }

    /// <summary>
    /// 표시 중인 다른 역할과 키가 겹치지 않을 때만 저장하고 연결된 실행 중 액션도 변경합니다.
    /// </summary>
    /// <param name="path">경로 값입니다.</param>
    bool TryApplyReferenceKey(string path)
    {
        // i: 반복 값입니다.
        for (int i = 0; i < _entries.Count; i++)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (i != _selected && Resolve(_entries[i], out int otherBinding) != null && otherBinding >= 0 && string.Equals(ReferencePath(_entries[i]), path, StringComparison.OrdinalIgnoreCase))
            {
                Notice("이미 할당된 키입니다. 다른 키를 선택해주세요.", true);
                return false;
            }
        }

        var entry = _entries[_selected]; // 항목 값입니다.
        var action = Resolve(entry, out int binding); // 동작 값입니다.
        if (action != null && binding >= 0)
        {
            action.ApplyBindingOverride(binding, path);
        }

        if (entry.Action == "Hero/Move" && _preferences != null && _preferences.InputActions != null)
        {
            var navigation = _preferences.InputActions.FindAction("UI/Navigate", false); // 이동 값입니다.
            if (navigation != null)
            {
                // i: 반복 값입니다.
                for (int i = 0; i < navigation.bindings.Count; i++)
                {
                    if (navigation.bindings[i].path == entry.Binding)
                    {
                        navigation.ApplyBindingOverride(i, path);
                    }
                }
            }
        }

        _selected = -1;
        RestoreInputs();
        if (_preferences != null && action != null)
        {
            _preferences.SaveBindings();
        }

        PlayerPrefs.Save();
        Refresh();
        Notice("키 설정을 변경했습니다.");
        return true;
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 첨부 기획의 선상·잠수·인벤토리 역할을 현재 연결 키와 미정·마우스 안내로 구분합니다.
    /// </summary>
    void BuildEntries()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_referenceLayout)
        {
            BuildReferenceEntries();
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_mergedLayout)
        {
            BuildMergedEntries();
            return;
        }

        Add(0, 0, "왼쪽 이동", "기본 이동키 A", "Hero/Move", "<Keyboard>/a");
        Add(0, 0, "오른쪽 이동", "기본 이동키 D", "Hero/Move", "<Keyboard>/d");
        Add(0, 0, "상호작용", "현재 연결된 임시 키", "Hero/Interact", "<Keyboard>/f");
        Add(0, 1, "선택지 이동", "상하 이동키 · 방향키 병행", fixedKey: "W/S · ↑/↓");
        Add(0, 1, "선택", "현재 연결된 UI 기본 조작", fixedKey: "Enter / 클릭");
        Add(0, 1, "취소 / 닫기", "키 변경 대기 중에는 취소로 사용", fixedKey: "Esc");
        Add(0, 2, "UI 조작", "장비 강화 · 상점 · 판매 UI", fixedKey: "좌클릭");
        Add(0, 2, "아이템 장착", "기획 조작 안내", fixedKey: "좌클릭 / 드래그");
        Add(0, 2, "장착 해제", "기획 조작 안내", fixedKey: "우클릭");
        Add(0, 2, "판매 아이템 이동", "기획 조작 안내", fixedKey: "드래그");
        Add(1, 0, "위로 이동", "기본 이동키 W · 방향키 병행", "Hero/Move", "<Keyboard>/w");
        Add(1, 0, "아래로 이동", "기본 이동키 S · 방향키 병행", "Hero/Move", "<Keyboard>/s");
        Add(1, 0, "왼쪽 이동", "기본 이동키 A · 방향키 병행", "Hero/Move", "<Keyboard>/a");
        Add(1, 0, "오른쪽 이동", "기본 이동키 D · 방향키 병행", "Hero/Move", "<Keyboard>/d");
        Add(1, 0, "빠른 수영", "누르는 동안 이동 속도·산소 소모 증가", "Hero/Sprint", "<Keyboard>/leftShift");
        Add(1, 0, "상호작용 / 획득", "현재 연결된 임시 키", "Hero/Interact", "<Keyboard>/f");
        Add(1, 1, "조준", "현재 마우스 조작 · 키보드로 변경 가능", "Hero/Aim", "<Mouse>/rightButton");
        Add(1, 1, "공격", "현재 마우스 조작 · 키보드로 변경 가능", "Hero/Attack", "<Mouse>/leftButton");
        Add(1, 1, "근접 무기 전환", "현재 연결된 임시 키", "Hero/MeleeMode", "<Keyboard>/q");
        Add(1, 1, "원거리 무기 전환", "현재 연결된 임시 키", "Hero/RangeMode", "<Keyboard>/e");
        Add(1, 1, "작살 / 총 전환", "현재 연결된 임시 키 · 오른쪽 Alt 병행", "Hero/ToggleRangeMode", "<Keyboard>/leftAlt");
        Add(1, 1, "작살 포획", "조작키 미정");
        Add(1, 2, "유틸리티 슬롯 1", "", "Hero/UtilitySlot1", "<Keyboard>/1");
        Add(1, 2, "유틸리티 슬롯 2", "", "Hero/UtilitySlot2", "<Keyboard>/2");
        Add(1, 2, "적재함 목록", "조작키 미정");
        Add(1, 2, "인벤토리 열기 / 닫기", "조작키 미정");
        Add(1, 2, "취소 / 닫기", "현재 연결된 UI 기본 조작", fixedKey: "Esc");
        Add(2, 0, "인벤토리 열기 / 닫기", "조작키 미정");
        Add(2, 0, "취소 / 닫기", "현재 연결된 UI 기본 조작", fixedKey: "Esc");
        Add(2, 0, "닫기 버튼", "인벤토리 창의 닫기 버튼", fixedKey: "좌클릭");
        Add(2, 1, "아이템 정보 확인", "기획 조작 안내", fixedKey: "마우스 호버");
        Add(2, 1, "아이템 위치 변경", "기획 조작 안내", fixedKey: "드래그");
        Add(2, 1, "아이템 위치 교환", "기획 조작 안내", fixedKey: "슬롯에 드롭");
        Add(2, 2, "키보드 조작", "미정 항목은 조작 확정 후 설정합니다.", fixedKey: "미정");
        Add(2, 2, "마우스 조작", "이 페이지는 기획 조작을 안내합니다.", fixedKey: "조작 안내");
    }

    /// <summary>
    /// 설정 화면에 합쳐서 표시할 키 항목을 구성합니다.
    /// </summary>
    void BuildMergedEntries()
    {
        Add(0, 0, "이동 · 선택지 이동", "W/A/S/D 순서 · 방향키 병행", "Hero/Move", "<Keyboard>/w");
        Add(0, 0, "왼쪽 이동", "", "Hero/Move", "<Keyboard>/a");
        Add(0, 0, "아래로 이동", "", "Hero/Move", "<Keyboard>/s");
        Add(0, 0, "오른쪽 이동", "", "Hero/Move", "<Keyboard>/d");
        Add(0, 0, "빠른 수영", "잠수 중에만 적용", "Hero/Sprint", "<Keyboard>/leftShift");
        Add(0, 0, "조준", "", "Hero/Aim", "<Mouse>/rightButton");
        Add(0, 0, "공격", "", "Hero/Attack", "<Mouse>/leftButton");
        Add(0, 0, "근접 무기 전환", "", "Hero/MeleeMode", "<Keyboard>/q");
        Add(0, 0, "원거리 / 작살·총 전환", "원거리 선택 / 작살·총 교체", "Hero/RangeMode", "<Keyboard>/e");
        Add(0, 0, "작살·총 전환", "", "Hero/ToggleRangeMode", "<Keyboard>/leftAlt");
        Add(0, 0, "작살 포획", "연결 대기", fixedKey: "미정");
        Add(0, 1, "상호작용", "", "Hero/Interact", "<Keyboard>/f");
        Add(0, 1, "유틸리티 슬롯 1 사용", "", "Hero/UtilitySlot1", "<Keyboard>/1");
        Add(0, 1, "유틸리티 슬롯 2 사용", "", "Hero/UtilitySlot2", "<Keyboard>/2");
        Add(0, 1, "인벤토리 열기 / 닫기", "연결 대기", fixedKey: "미정");
        Add(0, 1, "적재량 목록 열기 / 닫기", "연결 대기", fixedKey: "미정");
        Add(0, 1, "선택", "UI 선택 확정", "UI/Submit", "*/{Submit}");
        Add(0, 1, "취소 / 닫기", "고정 조작", fixedKey: "Esc");
    }

    /// <summary>
    /// 키 변경 후에도 원래 바인딩 경로를 기준으로 대상을 찾습니다.
    /// </summary>
    /// <param name="entry">항목 값입니다.</param>
    /// <param name="binding">키 할당 값입니다.</param>
    InputAction Resolve(Entry entry, out int binding)
    {
        binding = -1;
        var asset = _preferences != null ? _preferences.InputActions : null; // 에셋 값입니다.
        var action = asset != null && entry.Action != null ? asset.FindAction(entry.Action, false) : null; // 동작 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (action == null)
        {
            return null;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < action.bindings.Count; i++)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (action.bindings[i].path == entry.Binding)
            {
                binding = i;
                break;
            }
        }

        return action;
    }

    /// <summary>
    /// 키 변경을 중단하고 선택과 입력 일시 중지를 해제합니다.
    /// </summary>
    public void CancelRebind()
    {
        bool selected = IsListening; // 선택 여부입니다.
        _selected = -1;
        RestoreInputs();
        if (selected && _initialized)
        {
            Refresh();
        }
    }

    /// <summary>
    /// 사진의 12개 역할을 열별로 구성합니다. 실행 액션이 없는 역할은 읽기 전용으로 표시합니다.
    /// </summary>
    void BuildReferenceEntries()
    {
        Add(0, 0, "이동 (위쪽)", "", "Hero/Move", "<Keyboard>/w", "<Keyboard>/w");
        Add(0, 0, "이동 (아래)", "", "Hero/Move", "<Keyboard>/s", "<Keyboard>/s");
        Add(0, 0, "이동 (왼쪽)", "", "Hero/Move", "<Keyboard>/a", "<Keyboard>/a");
        Add(0, 0, "이동 (오른쪽)", "", "Hero/Move", "<Keyboard>/d", "<Keyboard>/d");
        Add(0, 1, "상호작용", "", "Hero/Interact", "<Keyboard>/f", "<Keyboard>/f");
        Add(0, 1, "달리기", "잠수 중 빠른 수영", "Hero/Sprint", "<Keyboard>/leftShift");
        Add(0, 1, "유틸리티 1", "", "Hero/UtilitySlot1", "<Keyboard>/1");
        Add(0, 1, "유틸리티 2", "", "Hero/UtilitySlot2", "<Keyboard>/2");
        Add(0, 2, "할 일", "스마트폰 열기 / 닫기", "Hero/SmartPhone", "<Keyboard>/escape");
        Add(0, 2, "근접무기 전환", "", "Hero/MeleeMode", "<Keyboard>/q", "<Keyboard>/q");
        Add(0, 2, "작살 전환", "", "Hero/RangeMode", "<Keyboard>/e", "<Keyboard>/e");
        Add(0, 2, "원거리무기 전환", "", "Hero/ToggleRangeMode", "<Keyboard>/leftAlt", "<Keyboard>/leftAlt");
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 상황 탭을 바꾸고 스크롤을 맨 위로 되돌립니다.
    /// </summary>
    /// <param name="page">페이지 값입니다.</param>
    public void SelectPage(int page)
    {
        Initialize();
        CancelRebind();
        _page = _mergedLayout ? 0 : Mathf.Clamp(page, 0, 2);
        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        Notice(_referenceLayout ? string.Empty : "키 입력칸을 선택한 뒤 새 키를 누르세요.");
        Refresh();
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 키 변경 전에 활성화되어 있던 액션만 원래대로 되돌립니다.
    /// </summary>
    void RestoreInputs()
    {
        // action: 동작 값입니다.
        foreach (var action in _suspendedActions)
        {
            action.Enable();
        }

        _suspendedActions.Clear();
    }

    #endregion

}
