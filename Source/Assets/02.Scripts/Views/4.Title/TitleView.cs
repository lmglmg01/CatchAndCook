using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>타이틀의 화면 전환과 저장 데이터 표시를 담당하며 실제 저장 처리는 기존 서비스에 요청합니다.</summary>
public class TitleView : MonoBehaviour
{
    #region 멤버

    #region 저장 슬롯 UI 및 화면 종류

    /// <summary>저장 슬롯 한 줄에 연결되는 UI 참조를 묶습니다.</summary>
    [Serializable]
    public sealed class SlotWidgets
    {
        public Button button; // 저장 슬롯을 선택하는 버튼입니다.
        public Image selection; // 현재 선택한 슬롯을 강조하는 배경입니다.
        public TMP_Text playerName, savedAt, day, phase; // 플레이어 이름, 저장 일시, 일차, 시간대 표시입니다.
        public Image phaseIcon, thumbnailSymbol; // 슬롯의 시간대 및 썸네일 기호 이미지입니다.
    }

    /// <summary>타이틀에서 전환할 수 있는 화면과 팝업의 종류입니다.</summary>
    public enum Page { Main, NewGame, Continue, Load, Delete, Settings, Quit }

    #endregion

    #region UI 연결 및 연출 설정

    [Header("타이틀 설정")]
    [SerializeField, InspectorName("연출 모드")]
    [Tooltip("체크하면 타이틀 시작 연출과 새 게임 확인 후 암전을 재생합니다. 해제하면 두 과정 모두 즉시 진행합니다.")]
    bool _cinematicMode = true; // 시작 연출과 새 게임 암전의 재생 여부입니다.

    [SerializeField, Min(0f), InspectorName("등장 시작 대기 시간 (초)")]
    [Tooltip("첫 화면을 렌더링한 뒤 도마 낙하를 시작하기 전까지 기다리는 시간입니다.")]
    float _logoStartDelay = 1.5f; // 첫 화면 표시 후 로고가 출발하기까지의 대기 시간입니다.

    [Header("시작 메뉴")]
    [SerializeField] GameObject _mainMenu; // 기본 메뉴와 로고가 포함된 시작 화면입니다.
    [SerializeField] CanvasGroup _menuGroup; // 팝업이 열린 동안 기본 메뉴의 입력을 막습니다.
    [SerializeField] Button _newGameButton, _continueButton, _loadButton, _settingsButton, _quitButton; // 기본 메뉴에서 각 화면을 여는 버튼입니다.
    [SerializeField] TMP_Text _message; // 시작 화면의 안내 문구입니다.

    [Header("타이틀 등장 연출")]
    [SerializeField] RectTransform _titleLogo; // 조각별 등장 연출의 기준 로고입니다.
    [SerializeField] CanvasGroup _menuButtonsGroup; // 로고 등장 후 메뉴 버튼의 투명도와 입력을 제어합니다.
    [SerializeField, InspectorName("시작 암전 화면")] CanvasGroup _introBlackout; // 시작 화면 공개와 새 게임 전환에 쓰는 암전 화면입니다.

    [Header("타이틀 로고 조각")]
    [SerializeField] RectTransform _logoBoard, _logoCatch, _logoAnd, _logoCook, _logoFish, _logoHat;
    [SerializeField, Range(1f, 1.6f)] float _logoTextPeakScale = 1.28f;
    [SerializeField, Min(.5f)] float _logoTextHoldDuration = .6f;
    [SerializeField, Min(.1f)] float _logoTextReturnDuration = .44f;
    [SerializeField, Min(0f)] float _logoTextHoldDrift = 18f;
    [Header("물고기 팔딱임")]
    [SerializeField] Vector2 _fishIdleWaitRange = new Vector2(2.4f, 4.8f);

    const float NewGameFadeDuration = 1f;
    const float MenuFadeDuration = .3f;
    const float FishFlopDuration = .84f;
    Sequence _titleIntro;
    Coroutine _titleIntroStartRoutine;
    Tween _fishFlop;
    Coroutine _fishIdleRoutine;
    Tween _newGameFade;
    Coroutine _newGameStartRoutine;
    bool _introPlaying;
    LogoPartPose[] _logoParts;

    sealed class LogoPartPose
    {
        public RectTransform Target;
        public Vector2 Position;
        public Vector3 Scale;
        public Quaternion Rotation;
        public void Apply(Vector2 offset, float scaleX = 1f, float scaleY = 1f, float angle = 0f, bool applyRotation = true)
        {
            if (Target == null) return;
            Target.anchoredPosition = Position + offset;
            Target.localScale = Vector3.Scale(Scale, new Vector3(scaleX, scaleY, 1f));
            if (applyRotation) Target.localRotation = Rotation * Quaternion.Euler(0f, 0f, angle);
        }
    }


    [Header("불러오기")]
    [SerializeField] GameObject _loadPanel; // 저장 슬롯 목록 화면입니다.
    [SerializeField] CanvasGroup _loadGroup; // 삭제 확인 중 저장 목록 입력을 막습니다.
    [SerializeField] SlotWidgets[] _slots; // 저장 슬롯별 표시 UI입니다.
    [SerializeField] Button _loadSelectedButton, _deleteSelectedButton, _closeLoadButton; // 선택 슬롯 불러오기, 삭제 안내, 목록 닫기 버튼입니다.
    [SerializeField] Sprite _sunIcon, _moonIcon, _afternoonIcon; // 저장 시간대의 낮과 밤을 구분하는 아이콘입니다.

    [Header("새 게임")]
    [SerializeField] GameObject _newGamePopup; // 새 게임 이름 입력 팝업입니다.
    [SerializeField] TMP_InputField _nameInput; // 기존 저장 서비스의 이름 규칙을 적용할 입력칸입니다.
    [SerializeField] TMP_Text _nameError; // 이름 검사 및 새 게임 시작 실패 안내입니다.
    [SerializeField] Button _nameConfirmButton, _nameCancelButton; // 입력한 이름 확정 및 취소 버튼입니다.

    [Header("이어하기")]
    [SerializeField] GameObject _continuePopup; // 최근 저장의 이어하기 확인 팝업입니다.
    [SerializeField] TMP_Text _continueMessage, _continueNotice; // 이어할 플레이어 이름과 안내 문구입니다.
    [SerializeField] Button _continueConfirmButton, _continueCancelButton; // 이어하기 확정 및 취소 버튼입니다.

    [Header("저장 데이터 삭제")]
    [SerializeField] GameObject _deletePopup; // 저장 데이터 삭제 확인 팝업입니다.
    [SerializeField] TMP_Text _deleteMessage, _deleteNotice; // 삭제 대상 이름과 복구 불가 안내입니다.
    [SerializeField] Button _deleteConfirmButton, _deleteCancelButton; // 데이터 삭제 확정 및 취소 버튼입니다.

    [Header("설정 / 종료")]
    [SerializeField] SettingView _settings; // Canvas_Base에서 분리한 공용 설정 뷰입니다.
    [SerializeField] GameObject _quitPopup; // 게임 종료 확인 팝업입니다.
    [SerializeField] Button _quitConfirmButton, _quitCancelButton; // 게임 종료 확정 및 취소 버튼입니다.

    #endregion

    #region 런타임 상태

    readonly List<KeyValuePair<Button, UnityAction>> _listeners = new List<KeyValuePair<Button, UnityAction>>(); // 이 뷰가 등록한 버튼 이벤트를 보관해 해제할 때 사용합니다.
    SaveSlotInfo[] _slotInfo = new SaveSlotInfo[GameSaveService.MaxSlots]; // 저장 서비스에서 읽은 슬롯별 메타데이터입니다.
    int _selectedSlot, _continueSlot, _deleteSlot; // 목록 선택, 이어하기, 삭제 대상 슬롯 번호입니다.
    bool _initialized, _busy; // 초기화 완료 여부와 중복 요청을 막는 처리 상태입니다.
    bool _popupTransitioning, _restoringTitle;
    int _popupTransitionVersion;
    bool _loadFocusRequested; // 불러오기 창의 등장 완료 후 저장 파일을 자동 선택합니다.
    readonly List<Button> _loadNavigation = new(); // 저장 파일 다음에 하단 동작 버튼을 연결합니다.
    Page _page; // 현재 표시 중인 화면입니다.
    Button[] _mainMenuButtons; // 방향키로 이동할 시작 메뉴 순서입니다.
    TMP_Text[] _mainMenuLabels; // 선택된 메뉴의 글씨 색을 갱신할 텍스트입니다.
    ScrollRect _loadScroll;
    GameObject _lastLoadScrollFocus;
    Sprite _menuSelectedSprite, _menuNormalSprite; // 기존 버튼에서 읽어 온 선택 및 기본 배경입니다.
    Color _menuSelectedTextColor, _menuNormalTextColor; // 기존 버튼에서 읽어 온 선택 및 기본 글씨 색입니다.
    Button _lastMenuButton; // 팝업을 닫고 돌아왔을 때 복구할 메뉴입니다.
    bool _focusMenuRequested; // 메뉴가 완전히 표시된 뒤 선택을 복구할지 나타냅니다.
    static readonly Color Cream = new Color(253f / 255f, 247f / 255f, 228f / 255f); // 썸네일 기호의 공통 크림색입니다.
    static readonly Color Selected = new Color(60f / 255f, 216f / 255f, 238f / 255f); // 선택 슬롯 강조색입니다.
    static readonly Color Normal = new Color(49f / 255f, 84f / 255f, 105f / 255f); // 비선택 슬롯 기본색입니다.

    #endregion

    #region 프로퍼티

    public Page CurrentPage => _page;
    public int SelectedSlot => _selectedSlot;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>필수 참조를 검사하고 버튼과 입력 이벤트를 한 번만 등록합니다.</summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeLoadButton, _nameCancelButton, _continueCancelButton, _deleteCancelButton, _quitCancelButton);
        if (_mainMenu == null || _menuGroup == null || _loadPanel == null || _loadGroup == null ||
            _newGamePopup == null || _continuePopup == null || _deletePopup == null || _quitPopup == null ||
            _settings == null || _nameInput == null || _message == null || _nameError == null ||
            _continueMessage == null || _continueNotice == null || _deleteMessage == null || _deleteNotice == null ||
            _slots == null || _slots.Length != GameSaveService.MaxSlots)
        {
            Debug.LogError("[TitleView] 타이틀 UI 참조가 누락되었습니다.", this);
            enabled = false;
            return;
        }
        foreach (var slot in _slots)
            if (slot == null || slot.button == null || slot.selection == null || slot.playerName == null ||
                slot.savedAt == null || slot.day == null || slot.phase == null || slot.phaseIcon == null)
            {
                Debug.LogError("[TitleView] 저장 슬롯 UI 참조가 누락되었습니다.", this);
                enabled = false;
                return;
            }
        Wire(_newGameButton, OpenNewGame);
        Wire(_continueButton, OpenContinue);
        Wire(_loadButton, OpenLoad);
        Wire(_settingsButton, OpenSettings);
        Wire(_quitButton, OpenQuit);
        Wire(_closeLoadButton, ShowMain);
        Wire(_loadSelectedButton, LoadSelected);
        Wire(_deleteSelectedButton, OpenDelete);
        Wire(_nameConfirmButton, ConfirmNewGame);
        Wire(_nameCancelButton, ShowMain);
        Wire(_continueConfirmButton, ConfirmContinue);
        Wire(_continueCancelButton, ShowMain);
        Wire(_deleteConfirmButton, ConfirmDelete);
        Wire(_deleteCancelButton, CancelDelete);
        Wire(_quitConfirmButton, ConfirmQuit);
        Wire(_quitCancelButton, ShowMain);
        for (int i = 0; i < _slots.Length; i++)
        {
            int number = i + 1;
            Wire(_slots[i].button, () => SelectSlot(number));
        }
        _nameInput.characterLimit = GameSaveService.MaxPlayerNameLength;
        _nameInput.richText = false;
        _nameInput.onValueChanged.AddListener(ValidateName);
        _nameInput.onSubmit.AddListener(SubmitName);
        InitializeMainMenuNavigation();
        _initialized = true;
    }

    /// <summary>타이틀을 다시 열 때 저장 목록을 새로 읽고 시작 화면으로 돌아갑니다.</summary>
    void OnEnable()
    {
        if (!_initialized) return;
        _busy = false; // 초기화 완료 여부와 중복 요청을 막는 처리 상태입니다.
        _restoringTitle = true;
        try { ShowMain(); } finally { _restoringTitle = false; }
        PlayTitleIntro();
    }

    /// <summary>타이틀을 닫으면 연출을 중단하고 로고와 메뉴를 원래 상태로 복원합니다.</summary>
    void OnDisable()
    {
        _popupTransitionVersion++;
        _popupTransitioning = false;
        StopNewGameTransition();
        StopTitleIntro();
        ClearMainMenuSelection();
    }

    /// <summary>시작 연출 건너뛰기를 반영하고, 설정 창을 닫으면 타이틀 메뉴 입력을 복구합니다.</summary>
    void LateUpdate()
    {
        // 재생 중 연출 모드를 해제해도 현재 연출을 정리하고 메뉴를 즉시 표시합니다.
        if (_introPlaying && !_cinematicMode)
        {
            StopTitleIntro();
            StartLogoIdle();
        }

        if (_initialized && !_busy && _page == Page.Settings && !_settings.gameObject.activeSelf)
            ShowMain();
        RefreshMainMenuNavigation();
        RefreshNameInputNavigation();
        RefreshLoadNavigation();
    }

    /// <summary>이 뷰가 등록한 이벤트만 해제하여 중복 호출과 남은 참조를 방지합니다.</summary>
    void OnDestroy()
    {
        StopNewGameTransition();
        StopTitleIntro();
        foreach (var pair in _listeners)
            if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
        if (_nameInput != null)
        {
            _nameInput.onValueChanged.RemoveListener(ValidateName);
            _nameInput.onSubmit.RemoveListener(SubmitName);
        }
    }

    #endregion

    #region 버튼 연결 및 화면 전환

    /// <summary>버튼을 한 번 연결하고 씬 이동 중의 추가 클릭을 차단합니다.</summary>
    void Wire(Button button, UnityAction action)
    {
        if (button == null) throw new InvalidOperationException("[TitleView] 버튼 참조가 누락되었습니다.");
        UnityAction listener = () =>
        {
            if (_busy || _popupTransitioning || _introPlaying) return;
            GameSfx.Play(GameSoundId.MenuClick);
            action();
        };
        button.onClick.AddListener(listener);
        _listeners.Add(new KeyValuePair<Button, UnityAction>(button, listener));
    }

    /// <summary>한 화면만 입력을 받도록 팝업과 메뉴의 표시 상태를 맞춥니다.</summary>
    void ShowPage(Page page)
    {
        if (_popupTransitioning) return;
        if (!Application.isPlaying || !isActiveAndEnabled || _restoringTitle) { ApplyPage(page); return; }
        var closing = new List<GameObject>();
        foreach (var panel in new[] { _newGamePopup, _continuePopup, _loadPanel, _deletePopup, _settings.gameObject, _quitPopup })
        {
            bool keep = panel == _newGamePopup ? page == Page.NewGame : panel == _continuePopup ? page == Page.Continue :
                panel == _loadPanel ? page == Page.Load || page == Page.Delete : panel == _deletePopup ? page == Page.Delete :
                panel == _settings.gameObject ? page == Page.Settings : page == Page.Quit;
            if (panel.activeInHierarchy && !keep) closing.Add(panel);
        }
        // 설정 창 자체의 닫기가 진행 중이면 기존 완료 처리를 기다립니다.
        // 이미 실행 중인 퇴장에 새 콜백을 덮어쓰지 않아 메뉴 복귀가 멈추지 않습니다.
        if (closing.Exists(panel => panel.GetComponent<CommonWindowEntranceView>() is CommonWindowEntranceView effect && effect.IsExiting)) return;
        if (closing.Count == 0) { ApplyPage(page); return; }
        _popupTransitioning = true;
        _menuGroup.interactable = _menuGroup.blocksRaycasts = false;
        int version = ++_popupTransitionVersion, remaining = closing.Count;
        foreach (var panel in closing)
        {
            void Closed()
            {
                if (version != _popupTransitionVersion || !isActiveAndEnabled) return;
                panel.SetActive(false);
                if (--remaining != 0) return;
                _popupTransitioning = false;
                ApplyPage(page);
            }
            var effect = panel.GetComponent<CommonWindowEntranceView>();
            if (effect == null || !effect.TryPlayExit(Closed)) Closed();
        }
    }

    /// <summary>기존 페이지 판정을 유지하고 퇴장 완료 뒤 다음 화면만 활성화합니다.</summary>
    void ApplyPage(Page page)
    {
        if (page != Page.Main) ClearMainMenuSelection();
        _loadFocusRequested = page == Page.Load && _page != Page.Load;
        _page = page; // 현재 표시 중인 화면입니다.
        _focusMenuRequested = page == Page.Main;
        _mainMenu.SetActive(page == Page.Main);
        _menuGroup.interactable = _menuGroup.blocksRaycasts = page == Page.Main && !_introPlaying;
        _loadGroup.interactable = _loadGroup.blocksRaycasts = page == Page.Load;
        _loadPanel.SetActive(page == Page.Load || page == Page.Delete);
        _newGamePopup.SetActive(page == Page.NewGame);
        _continuePopup.SetActive(page == Page.Continue);
        _deletePopup.SetActive(page == Page.Delete);
        _quitPopup.SetActive(page == Page.Quit);
        _settings.gameObject.SetActive(page == Page.Settings);
        if (_mainMenu.activeInHierarchy) StartLogoIdle();
        else StopLogoIdle();
    }

    /// <summary>팝업을 닫고 최신 저장 유무를 반영한 시작 메뉴를 표시합니다.</summary>
    public void ShowMain()
    {
        if (_busy || _popupTransitioning || !_initialized) return;
        RefreshSlots();
        _message.text = string.Empty;
        ShowPage(Page.Main);
    }

    #endregion

    #region 시작 메뉴 방향키 선택

    /// <summary>기존 버튼의 순서와 선택 색상을 보관하며 새 입력 이벤트는 중복 등록하지 않습니다.</summary>
    public void RestoreOriginalButtonColors()
    {
        foreach (var button in new[] { _continueButton, _loadButton, _settingsButton, _quitButton })
        {
            if (button == null) continue;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            if (button.image != null) { button.image.color = Color.white; button.image.canvasRenderer.SetColor(Color.white); }
        }
    }

    void InitializeMainMenuNavigation()
    {
        _mainMenuButtons = new[] { _newGameButton, _continueButton, _loadButton, _settingsButton, _quitButton };
        RestoreOriginalButtonColors();
        _mainMenuLabels = new TMP_Text[_mainMenuButtons.Length];
        for (int i = 0; i < _mainMenuButtons.Length; i++)
            _mainMenuLabels[i] = _mainMenuButtons[i].GetComponentInChildren<TMP_Text>(true);
        _menuSelectedSprite = _newGameButton.image != null ? _newGameButton.image.sprite : null;
        _menuNormalSprite = _loadButton.image != null ? _loadButton.image.sprite : null;
        _menuSelectedTextColor = _mainMenuLabels[0] != null ? _mainMenuLabels[0].color : Color.black;
        _menuNormalTextColor = _mainMenuLabels[2] != null ? _mainMenuLabels[2].color : Cream;
        _lastMenuButton = _newGameButton;
        _focusMenuRequested = true;
    }

    /// <summary>연출과 팝업 입력을 보호하며 활성 메뉴 사이의 방향키 이동과 선택 표시를 갱신합니다.</summary>
    void RefreshMainMenuNavigation()
    {
        if (!_initialized || _mainMenuButtons == null) return;
        if (_busy || _popupTransitioning || _introPlaying || _page != Page.Main || !_mainMenu.activeInHierarchy ||
            !_menuGroup.interactable || (_menuButtonsGroup != null &&
            (!_menuButtonsGroup.interactable || _menuButtonsGroup.alpha < .99f)))
        {
            ClearMainMenuSelection();
            return;
        }
        for (int i = 0; i < _mainMenuButtons.Length; i++)
        {
            var navigation = _mainMenuButtons[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = FindMainMenuButton(i, -1);
            navigation.selectOnDown = FindMainMenuButton(i, 1);
            navigation.selectOnLeft = navigation.selectOnRight = null;
            _mainMenuButtons[i].navigation = navigation;
        }
        var events = EventSystem.current;
        if (events == null) return;
        var selected = events.currentSelectedGameObject;
        int index = Array.FindIndex(_mainMenuButtons, button => button != null && button.gameObject == selected);
        if (_focusMenuRequested || selected == null || (index >= 0 && !CanSelectMainMenuButton(_mainMenuButtons[index])))
        {
            var next = CanSelectMainMenuButton(_lastMenuButton) ? _lastMenuButton : FindMainMenuButton(-1, 1);
            if (next == null) return;
            events.SetSelectedGameObject(next.gameObject);
            _focusMenuRequested = false;
            index = Array.IndexOf(_mainMenuButtons, next);
        }
        if (index < 0) return;
        _lastMenuButton = _mainMenuButtons[index];
        for (int i = 0; i < _mainMenuButtons.Length; i++)
        {
            bool isSelected = i == index;
            var sprite = isSelected ? _menuSelectedSprite : _menuNormalSprite;
            var image = _mainMenuButtons[i].image;
            if (image != null && sprite != null && image.sprite != sprite) image.sprite = sprite;
            if (_mainMenuLabels[i] != null)
                _mainMenuLabels[i].color = isSelected ? _menuSelectedTextColor : _menuNormalTextColor;
        }
        // Enter는 기존 EventSystem의 Submit으로 선택된 Button을 실행합니다.
        // 별도 키 입력이나 onClick 호출을 추가하지 않아 한 번만 실행됩니다.
    }

    /// <summary>비활성 또는 사용 불가 메뉴를 제외하고 위아래로 순환할 다음 버튼을 찾습니다.</summary>
    Button FindMainMenuButton(int index, int direction)
    {
        for (int step = 1; step <= _mainMenuButtons.Length; step++)
        {
            int next = (index + direction * step + _mainMenuButtons.Length * 2) % _mainMenuButtons.Length;
            if (CanSelectMainMenuButton(_mainMenuButtons[next])) return _mainMenuButtons[next];
        }
        return null;
    }

    /// <summary>버튼과 상위 CanvasGroup의 활성 및 입력 가능 상태를 함께 확인합니다.</summary>
    static bool CanSelectMainMenuButton(Button button) => button != null && button.isActiveAndEnabled && button.IsInteractable();

    /// <summary>메뉴를 떠나거나 연출 중일 때 시작 메뉴의 선택만 해제해 다른 입력칸을 보호합니다.</summary>
    void ClearMainMenuSelection()
    {
        var events = EventSystem.current;
        if (events == null || _mainMenuButtons == null) return;
        var selected = events.currentSelectedGameObject;
        foreach (var button in _mainMenuButtons)
            if (button != null && button.gameObject == selected)
            {
                events.SetSelectedGameObject(null);
                return;
            }
    }

    #endregion

    #region 타이틀 로고 및 메뉴 등장 연출

    /// <summary>승인된 조각별 연출을 재생하고 완료 후 메뉴 입력을 복구합니다.</summary>
    [ContextMenu("타이틀 등장 연출 다시 재생 (플레이 중)")]
    public void PlayTitleIntro()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !_initialized || _page != Page.Main) return;
        StopTitleIntro();
        if (!_cinematicMode || _titleLogo == null || _menuButtonsGroup == null || !CacheLogoParts())
        {
            StartLogoIdle();
            return;
        }
        _introPlaying = true;
        _menuButtonsGroup.alpha = 0f;
        _menuButtonsGroup.interactable = _menuButtonsGroup.blocksRaycasts = false;
        _menuGroup.interactable = _menuGroup.blocksRaycasts = false;
        ApplyLogoEntrance(0f);
        _titleIntro = DOTween.Sequence().SetUpdate(true).SetTarget(this).Pause();
        // 낙하 → Canvas Editor 회전 트랙 완료 → 글씨 합류 순서로 실제 시퀀스를 연결합니다.
        _titleIntro.Append(DOVirtual.Float(.22f, .66f, .44f, ApplyLogoEntrance).SetEase(Ease.Linear));
        var boardDirector = GetComponent<UIDirectorView>();
        var boardBinding = boardDirector != null ? boardDirector.Bindings.Find(b => b.owner == this && b.target == _logoBoard && b.enterMethod == nameof(PlayBoardLandingWobble)) : null;
        if (boardBinding != null)
        {
            var boardSequence = UIDirectorView.BuildSequence(_logoBoard, boardBinding.enter, new UIDirectorView.Pose(_logoBoard));
            if (boardSequence != null) _titleIntro.Append(boardSequence);
        }
        // &를 먼저 짧게 찍은 뒤 Catch와 Cook만 합류시킵니다.
        _titleIntro.Append(DOVirtual.Float(0f, 1f, .14f, ApplyAndStamp).SetEase(Ease.InQuart));
        _titleIntro.Append(DOVirtual.Float(.92f, 1f, .08f,
            scale => _logoParts[2].Apply(Vector2.zero, scale, scale)).SetEase(Ease.OutCubic));
        _titleIntro.Append(DOVirtual.Float(.8f, LogoEntranceDuration, LogoEntranceDuration - .8f, ApplyLogoContent).SetEase(Ease.Linear));
        _titleIntro.AppendCallback(RestoreLogoParts);
        _titleIntro.Append(_menuButtonsGroup.DOFade(1f, MenuFadeDuration).SetEase(Ease.InOutSine));
        _titleIntro.OnComplete(() =>
        {
            _titleIntro = null;
            _introPlaying = false;
            RestoreMenuInput();
            StartLogoIdle(.02f);
        });
        _titleIntroStartRoutine = StartCoroutine(StartTitleIntroAfterFirstFrame(_titleIntro));
    }

    /// <summary>첫 화면이 그려진 뒤 실제 시간으로 대기하여 초기 로딩이 낙하 시간을 소모하지 않게 합니다.</summary>
    IEnumerator StartTitleIntroAfterFirstFrame(Sequence sequence)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, _logoStartDelay));
        _titleIntroStartRoutine = null;
        if (isActiveAndEnabled && _introPlaying && _page == Page.Main && ReferenceEquals(sequence, _titleIntro))
            sequence.Play();
    }

    /// <summary>도마 착지 시 Canvas Editor에 저장한 회전 연출을 실행합니다.</summary>
    void PlayBoardLandingWobble()
    {
        // <UIDirector:PlayBoardLandingWobble>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        {
    }
        global::UIDirectorView.Dispatch(this, "PlayBoardLandingWobble", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>&를 위에서 빠르게 눌러 찍고, 짧은 착지 반동 후 고정합니다.</summary>
    void ApplyAndStamp(float progress)
    {
        if (_logoParts == null) return;
        float scale = Mathf.Lerp(2.2f, .92f, progress);
        _logoParts[2].Apply(Vector2.up * Mathf.Lerp(85f, 0f, progress),
            scale, scale, Mathf.Lerp(-12f, 0f, progress));
    }

    float TextReturnStart => 1.22f + Mathf.Max(.5f, _logoTextHoldDuration);
    float FishPopStart => TextReturnStart + Mathf.Max(.1f, _logoTextReturnDuration) + .02f;
    float HatPopStart => FishPopStart + .12f;
    float LogoEntranceDuration => HatPopStart + .28f;

    /// <summary>기존 로고 배치는 유지하고 애니메이션에 사용할 각 조각의 기준 배치만 저장합니다.</summary>
    bool CacheLogoParts()
    {
        if (_logoParts != null) return true;
        if (_titleLogo == null) return false;
        if (_logoBoard == null) _logoBoard = _titleLogo.Find("LogoBoard") as RectTransform;
        if (_logoCatch == null) _logoCatch = _titleLogo.Find("LogoCatch") as RectTransform;
        if (_logoAnd == null) _logoAnd = _titleLogo.Find("LogoAnd") as RectTransform;
        if (_logoCook == null) _logoCook = _titleLogo.Find("LogoCook") as RectTransform;
        if (_logoFish == null) _logoFish = _titleLogo.Find("LogoFish") as RectTransform;
        if (_logoHat == null) _logoHat = _titleLogo.Find("LogoHat") as RectTransform;
        var targets = new[] { _logoBoard, _logoCatch, _logoAnd, _logoCook, _logoFish, _logoHat };
        foreach (var target in targets) if (target == null) return false;
        _logoParts = new LogoPartPose[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            _logoParts[i] = new LogoPartPose { Target = targets[i], Position = targets[i].anchoredPosition,
                Scale = targets[i].localScale, Rotation = targets[i].localRotation };
        return true;
    }

    static float LogoOutCubic(float value) => 1f - Mathf.Pow(1f - Mathf.Clamp01(value), 3f);
    static float LogoPop(float value)
    {
        float t = Mathf.Clamp01(value) - 1f;
        return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
    }

    /// <summary>승인된 GIF와 같은 시간축으로 낙하·글씨 합류·확대 유지·물고기와 모자 등장을 표시합니다.</summary>
    void ApplyLogoEntrance(float time)
    {
        if (_logoParts == null) return;
        float boardY = 0f, boardXScale = 1f, boardYScale = 1f;
        if (time < .22f) boardY = 780f;
        else if (time < .66f)
        {
            float p = Mathf.Clamp01((time - .22f) / .44f);
            boardY = 780f * (1f - p * p);
        }
        else if (time < .82f)
        {
            float p = Mathf.Sin((time - .66f) / .16f * Mathf.PI);
            boardXScale += .035f * p; boardYScale -= .07f * p;
        }
        else if (time < 1.02f) boardY = 13f * Mathf.Sin((time - .82f) / .2f * Mathf.PI);
        _logoParts[0].Apply(Vector2.up * boardY, boardXScale, boardYScale, applyRotation: time < .66f);

        ApplyLogoContent(time);
    }

    /// <summary>도마의 회전이 완전히 끝난 뒤 글씨·물고기·모자만 기존 시간표대로 재생합니다.</summary>
    void ApplyLogoContent(float time)
    {
        if (_logoParts == null) return;
        float distance = 1050f * (1f - LogoOutCubic((time - .8f) / .26f));
        float hit = time >= 1.06f && time < 1.27f ? -13f * Mathf.Sin((time - 1.06f) / .21f * Mathf.PI) : 0f;
        float textScale = 1f;
        if (time >= 1.06f && time < 1.22f) textScale = Mathf.Lerp(1f, _logoTextPeakScale, LogoOutCubic((time - 1.06f) / .16f));
        else if (time >= 1.22f && time < TextReturnStart) textScale = _logoTextPeakScale;
        else if (time >= TextReturnStart && time < FishPopStart - .02f)
        {
            float p = Mathf.Clamp01((time - TextReturnStart) / Mathf.Max(.1f, _logoTextReturnDuration));
            textScale = Mathf.Lerp(_logoTextPeakScale, 1f, p * p * (3f - 2f * p));
        }
        // 확대 유지 중 두 줄을 반대 방향으로 조금씩 이동하고 축소와 함께 원위치로 돌립니다.
        float textDrift = 0f;
        if (time >= 1.22f && time < TextReturnStart)
            textDrift = _logoTextHoldDrift * DOVirtual.EasedValue(0f, 1f,
                Mathf.Clamp01((time - 1.22f) / Mathf.Max(.5f, _logoTextHoldDuration)), Ease.OutCubic);
        else if (time >= TextReturnStart && time < FishPopStart - .02f)
        {
            float p = Mathf.Clamp01((time - TextReturnStart) / Mathf.Max(.1f, _logoTextReturnDuration));
            textDrift = _logoTextHoldDrift * (1f - p * p * (3f - 2f * p));
        }
        for (int i = 1; i <= 3; i += 2)
        {
            var part = _logoParts[i];
            float slide = i == 1 ? -distance - hit : distance + hit;
            Vector2 finalPosition = (part.Position + Vector2.right * slide) * textScale;
            finalPosition.x += i == 1 ? -textDrift : textDrift;
            part.Apply(finalPosition - part.Position, time < .8f ? 0f : textScale, time < .8f ? 0f : textScale);
        }
        // 도장 완료 후 &는 글씨 확대·이동에 참여하지 않고 최종 배치를 유지합니다.
        float andScale = time < .8f ? 0f : 1f;
        _logoParts[2].Apply(Vector2.zero, andScale, andScale);
        float fishScale = time < FishPopStart ? 0f : LogoPop((time - FishPopStart) / .30f);
        float hatScale = time < HatPopStart ? 0f : LogoPop((time - HatPopStart) / .28f);
        _logoParts[4].Apply(Vector2.zero, fishScale, fishScale);
        _logoParts[5].Apply(Vector2.zero, hatScale, hatScale);
    }

    /// <summary>로고 전체는 고정하고 물고기만 서로 다른 대기 간격으로 팔딱입니다.</summary>
    void StartLogoIdle(float firstDelay = -1f)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !_initialized || _introPlaying ||
            _menuButtonsGroup == null || _menuButtonsGroup.alpha < .999f ||
            _mainMenu == null || !_mainMenu.activeInHierarchy || _fishIdleRoutine != null || !CacheLogoParts()) return;
        _fishIdleRoutine = StartCoroutine(FishIdle(firstDelay));
    }

    IEnumerator FishIdle(float firstDelay)
    {
        float minimum = Mathf.Max(.1f, _fishIdleWaitRange.x);
        float maximum = Mathf.Max(minimum, _fishIdleWaitRange.y);
        float wait = firstDelay >= 0f ? firstDelay : UnityEngine.Random.Range(minimum, maximum);
        while (true)
        {
            yield return new WaitForSecondsRealtime(wait);
            _fishFlop = DOVirtual.Float(0f, 1f, FishFlopDuration, ApplyFishFlop)
                .SetEase(Ease.Linear).SetUpdate(true).SetTarget(this);
            yield return _fishFlop.WaitForCompletion();
            _fishFlop = null;
            if (_logoParts != null) _logoParts[4].Apply(Vector2.zero);
            wait = UnityEngine.Random.Range(minimum, maximum);
        }
    }

    /// <summary>짧은 준비 동작, 점프 중 뒤틀림, 착지 반동을 승인된 GIF와 같은 곡선으로 재생합니다.</summary>
    void ApplyFishFlop(float progress)
    {
        if (_logoParts == null) return;
        float x = 0f, y = 0f, angle = 0f, sx = 1f, sy = 1f;
        if (progress < .12f)
        {
            float p = Mathf.Sin(progress / .12f * Mathf.PI * .5f);
            sx += .10f * p; sy -= .16f * p; y = -4f * p;
        }
        else if (progress < .8f)
        {
            float p = (progress - .12f) / .68f;
            float arc = Mathf.Sin(p * Mathf.PI);
            y = 78f * arc; x = 10f * arc;
            angle = 23f * Mathf.Sin(p * 3f * Mathf.PI) * arc;
            sx += .05f * Mathf.Sin(p * 5f * Mathf.PI); sy -= .04f * Mathf.Sin(p * 5f * Mathf.PI);
        }
        else
        {
            float p = Mathf.Sin((progress - .8f) / .2f * Mathf.PI);
            sx += .12f * p; sy -= .13f * p; y = -4f * p;
        }
        _logoParts[4].Apply(new Vector2(x, y), sx, sy, angle);
    }

    void StopLogoIdle()
    {
        if (_fishIdleRoutine != null) StopCoroutine(_fishIdleRoutine);
        _fishIdleRoutine = null;
        _fishFlop?.Kill(); _fishFlop = null;
        if (_logoParts != null) _logoParts[4].Apply(Vector2.zero);
    }

    void RestoreLogoParts()
    {
        if (_logoParts == null) return;
        foreach (var part in _logoParts) part.Apply(Vector2.zero);
    }

    /// <summary>새 게임 전환 암전과 시작 연출 중단 시 검은 화면을 완전히 숨깁니다.</summary>
    void HideIntroBlackout()
    {
        if (_introBlackout == null) return;
        _introBlackout.alpha = 0f;
        _introBlackout.blocksRaycasts = false;
        var image = _introBlackout.GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.raycastTarget = false;
        _introBlackout.gameObject.SetActive(false);
    }

    void RestoreMenuInput()
    {
        if (_menuButtonsGroup != null)
        {
            _menuButtonsGroup.alpha = 1f;
            _menuButtonsGroup.interactable = _menuButtonsGroup.blocksRaycasts = true;
        }
        if (_menuGroup != null) _menuGroup.interactable = _menuGroup.blocksRaycasts = !_busy && _page == Page.Main;
    }

    void StopTitleIntro()
    {
        if (_titleIntroStartRoutine != null) StopCoroutine(_titleIntroStartRoutine);
        _titleIntroStartRoutine = null;
        _titleIntro?.Kill(); _titleIntro = null;
        _introPlaying = false;
        if (_logoBoard != null) GetComponent<UIDirectorView>()?.CancelEffects(this, _logoBoard);
        StopLogoIdle();
        HideIntroBlackout();
        RestoreLogoParts();
        RestoreMenuInput();
    }



    #endregion

    #region 저장 슬롯 조회 및 표시

    /// <summary>슬롯 메타데이터를 기존 서비스에서 읽고 빈 슬롯·손상된 슬롯도 구분합니다.</summary>
    public void RefreshSlots()
    {
        for (int i = 0; i < _slotInfo.Length; i++) _slotInfo[i] = GameSaveService.GetSlotInfo(i + 1);
        PresentSlots();
    }

    /// <summary>저장 이름·일시·위치·일차·시간대를 표시하고 선택 가능 상태를 갱신합니다.</summary>
    void PresentSlots()
    {
        bool canContinue = false;
        for (int i = 0; i < _slots.Length; i++)
        {
            var info = _slotInfo[i];
            var row = _slots[i];
            canContinue |= info.CanLoad;
            row.button.interactable = info.Exists;
            row.playerName.text = info.Exists ? $"슬롯 {i + 1}  {info.PlayerName}" : $"슬롯 {i + 1}  빈 슬롯";
            row.savedAt.text = !info.Exists ? "새 게임으로 플레이를 시작할 수 있습니다."
                : !info.CanLoad ? info.Error : $"저장 일시  {FormatTime(info.LastPlayedUtcTicks)} · {LocationName(info.SceneName)}";
            row.day.text = info.CanLoad ? $"{info.DayNumber}일 차" : string.Empty;
            row.phase.text = info.CanLoad ? PhaseName(info.Phase) : string.Empty;
            row.phaseIcon.enabled = info.CanLoad;
            row.phaseIcon.sprite = info.Phase == DayPhase.Night ? _moonIcon : info.Phase == DayPhase.Afternoon && _afternoonIcon != null ? _afternoonIcon : _sunIcon;
            var calendar = row.button.transform.Find("Background/DayIcon").GetComponent<Image>();
            calendar.enabled = info.CanLoad;
            var hover = row.button.GetComponent<TitleSlotIconHoverView>();
            if (hover == null) hover = row.button.gameObject.AddComponent<TitleSlotIconHoverView>();
            hover.Configure(calendar, row.phaseIcon, info.Phase == DayPhase.Morning);
            if (row.thumbnailSymbol != null)
            {
                row.thumbnailSymbol.enabled = false;
                row.thumbnailSymbol.sprite = row.phaseIcon.sprite;
                row.thumbnailSymbol.color = Cream;
            }
        }
        _continueButton.interactable = canContinue;
        RefreshSelection();
    }

    /// <summary>선택한 슬롯만 강조하고 불러오기와 삭제 버튼의 사용 가능 여부를 표시합니다.</summary>
    void RefreshSelection()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            var row=_slots[i];bool selected=i+1==_selectedSlot;
            Color navy=new Color32(0,40,58,255);
            row.selection.color=selected?Cream:navy;
            var background=row.button.transform.Find("Background")?.GetComponent<Image>();
            if(background!=null)background.color=selected?Cream:navy;
            row.playerName.color=selected?navy:Cream;
            row.button.GetComponent<TitleSlotIconHoverView>()?.SetSelected(selected);
            row.savedAt.color=row.day.color=row.phase.color=new Color32(152,148,134,255);
        }
        var info = _selectedSlot > 0 ? _slotInfo[_selectedSlot - 1] : default;
        _loadSelectedButton.interactable = info.CanLoad;
        _deleteSelectedButton.interactable = info.Exists;
    }

    #endregion

    #region 새 게임 입력 및 전환

    /// <summary>이름 입력 팝업을 비우고 입력칸에 포커스를 둡니다.</summary>
    public void OpenNewGame()
    {
        if (_page != Page.Main) return;
        _nameInput.SetTextWithoutNotify(string.Empty);
        _nameError.text = string.Empty;
        _nameConfirmButton.interactable = false;
        ShowPage(Page.NewGame);
        var effect = _newGamePopup.GetComponent<CommonWindowEntranceView>();
        if (effect != null) effect.PlayEntrance(FocusNewGameName);
        else FocusNewGameName();
    }

    /// <summary>이름 편집의 좌우 커서는 유지하고 위아래 방향키로 확인·취소 버튼을 선택합니다.</summary>
    void RefreshNameInputNavigation()
    {
        if (!_initialized || _busy || _popupTransitioning || _page != Page.NewGame || !_nameInput.isFocused) return;
        var effect = _newGamePopup.GetComponent<CommonWindowEntranceView>();
        if (effect != null && (effect.IsEntering || effect.IsExiting)) return;
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool down = keyboard != null && keyboard.downArrowKey.wasPressedThisFrame;
        bool up = keyboard != null && keyboard.upArrowKey.wasPressedThisFrame;
#else
        bool down = Input.GetKeyDown(KeyCode.DownArrow), up = Input.GetKeyDown(KeyCode.UpArrow);
#endif
        if (!down && !up) return;
        var next = down && _nameConfirmButton.IsInteractable() ? _nameConfirmButton : _nameCancelButton;
        _nameInput.DeactivateInputField();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(next.gameObject);
    }
    /// <summary>등장이 끝난 뒤에만 이름 입력을 활성화해 키보드 입력이 뒤 메뉴로 전달되지 않게 합니다.</summary>
    void FocusNewGameName()
    {
        if (_page != Page.NewGame || _busy || _popupTransitioning) return;
        _nameInput.Select(); _nameInput.ActivateInputField();
    }

    /// <summary>기존 서비스의 이름 검사 규칙으로 확인 버튼과 안내문을 갱신합니다.</summary>
    void ValidateName(string value)
    {
        bool valid = GameSaveService.TryValidatePlayerName(value, out _, out string error);
        _nameConfirmButton.interactable = valid;
        _nameError.text = string.IsNullOrWhiteSpace(value) ? string.Empty : error;
    }

    /// <summary>이름 입력 중 Enter는 확인, 입력 취소는 시작 화면 복귀로 처리합니다.</summary>
    void SubmitName(string value)
    {
        if (_busy || _popupTransitioning || _page != Page.NewGame) return;
        if (_nameInput.wasCanceled) ShowMain();
        else ConfirmNewGame();
    }

    /// <summary>이름을 확인하고 연출 모드에 따라 암전 후 또는 즉시 새 게임을 시작합니다.</summary>
    public void ConfirmNewGame()
    {
        if (_busy || _popupTransitioning || _page != Page.NewGame) return;
        if (!GameSaveService.TryValidatePlayerName(_nameInput.text, out string playerName, out string validationError))
        {
            _nameError.text = validationError;
            return;
        }
        _busy = true; // 초기화 완료 여부와 중복 요청을 막는 처리 상태입니다.
        SetNewGameInputEnabled(false);
        BeginNewGameTransition(playerName);
    }

    /// <summary>확정한 닉네임을 보관한 채 화면을 1초 동안 암전시킨 뒤 기존 새 게임 로직으로 연결합니다.</summary>
    void BeginNewGameTransition(string playerName)
    {
        if (!_cinematicMode || _introBlackout == null)
        {
            StartNewGameAfterFade(playerName);
            return;
        }

        _introBlackout.transform.SetAsLastSibling();
        _introBlackout.gameObject.SetActive(true);
        _introBlackout.alpha = 0f;
        _introBlackout.blocksRaycasts = true;
        var image = _introBlackout.GetComponent<UnityEngine.UI.Image>();
        if (image != null) image.raycastTarget = true;
        _newGameFade = _introBlackout.DOFade(1f, NewGameFadeDuration)
            .SetEase(Ease.InOutSine).SetUpdate(true).OnComplete(() =>
            {
                _newGameFade = null; // 새 게임 전환 암전의 트윈입니다.
                _newGameStartRoutine = StartCoroutine(WaitForBlackFrameAndStart(playerName)); // 완전히 어두운 프레임을 기다리는 코루틴입니다.
            });
    }

    /// <summary>완전히 어두운 화면을 한 프레임 그린 뒤 씬을 바꿔 전환 직전 화면이 번쩍이지 않도록 합니다.</summary>
    IEnumerator WaitForBlackFrameAndStart(string playerName)
    {
        yield return new WaitForEndOfFrame();
        _newGameStartRoutine = null; // 완전히 어두운 프레임을 기다리는 코루틴입니다.
        if (isActiveAndEnabled && _busy) StartNewGameAfterFade(playerName);
    }

    /// <summary>기존 서비스를 통해 새 게임을 시작하고 실패하면 암전을 해제하여 입력 화면을 복구합니다.</summary>
    void StartNewGameAfterFade(string playerName)
    {
        try
        {
            if (GameSaveService.TryStartNewGame(playerName, out string error)) return;
            RestoreNewGameAfterFailure(error);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            RestoreNewGameAfterFailure("새 게임을 시작하지 못했습니다. 잠시 후 다시 시도해주세요.");
        }
    }

    /// <summary>시작 실패 시 검은 화면과 입력 잠금을 해제하고 기존 안내 문구를 표시합니다.</summary>
    void RestoreNewGameAfterFailure(string error)
    {
        _busy = false; // 초기화 완료 여부와 중복 요청을 막는 처리 상태입니다.
        HideIntroBlackout();
        SetNewGameInputEnabled(true);
        _nameError.text = error;
    }

    /// <summary>씬 전환 도중 닉네임 변경과 확인·취소 버튼의 중복 입력을 막습니다.</summary>
    void SetNewGameInputEnabled(bool enabled)
    {
        if (_nameInput != null) _nameInput.interactable = enabled;
        if (_nameCancelButton != null) _nameCancelButton.interactable = enabled;
        if (_nameConfirmButton != null) _nameConfirmButton.interactable = enabled;
        if (enabled && _initialized && _nameInput != null && _nameConfirmButton != null && _nameError != null)
            ValidateName(_nameInput.text);
    }

    /// <summary>타이틀이 닫히면 남은 암전 트윈과 씬 전환 예약을 취소하고 입력 상태를 복원합니다.</summary>
    void StopNewGameTransition()
    {
        _newGameFade?.Kill();
        _newGameFade = null; // 새 게임 전환 암전의 트윈입니다.
        if (_newGameStartRoutine != null) StopCoroutine(_newGameStartRoutine);
        _newGameStartRoutine = null; // 완전히 어두운 프레임을 기다리는 코루틴입니다.
        SetNewGameInputEnabled(true);
    }

    #endregion

    #region 이어하기 및 불러오기

    /// <summary>현재 불러올 수 있는 최신 슬롯의 이름을 이어하기 확인 팝업에 표시합니다.</summary>
    public void OpenContinue()
    {
        if (_page != Page.Main) return;
        var latest = GameSaveService.GetLatestSlotInfo();
        if (!latest.CanLoad) { _message.text = "이어할 수 있는 저장 데이터가 없습니다."; return; }
        _continueSlot = latest.Slot;
        _continueMessage.text = $"‘{latest.PlayerName}’로 이어하시겠습니까?";
        _continueNotice.text = "가장 최근에 플레이한 저장 데이터로 게임을 이어합니다.";
        ShowPage(Page.Continue);
    }

    /// <summary>팝업에서 확인한 슬롯을 불러오며 실패하면 화면을 유지합니다.</summary>
    public void ConfirmContinue()
    {
        if (_busy || _popupTransitioning || _page != Page.Continue) return;
        LoadSlot(_continueSlot, _continueNotice);
    }

    /// <summary>실제 저장 목록을 열고 가장 위의 저장 파일을 기본 선택합니다.</summary>
    public void OpenLoad()
    {
        if (_page != Page.Main) return;
        RefreshSlots();
        _selectedSlot = 0;
        for (int i = 0; i < _slotInfo.Length; i++) if (_slotInfo[i].Exists) { _selectedSlot = i + 1; break; }
        RefreshSelection();
        _message.text = string.Empty;
        ShowPage(Page.Load);
    }

    /// <summary>저장 파일과 하단 버튼을 위아래로 순환하고, 호버한 파일을 실제 동작 대상으로 맞춥니다.</summary>
    void RefreshLoadNavigation()
    {
        if (!_initialized || _busy || _popupTransitioning || _page != Page.Load || !_loadPanel.activeInHierarchy || !_loadGroup.interactable) return;
        var effect = _loadPanel.GetComponent<CommonWindowEntranceView>();
        if (effect != null && (effect.IsEntering || effect.IsExiting)) return;
        var events = EventSystem.current;
        if (events == null) return;

        // 파일에 키보드 포커스가 가면 별도 클릭 없이 해당 파일의 불러오기·삭제 조건을 갱신합니다.
        for (int i = 0; i < _slots.Length; i++)
            if (events.currentSelectedGameObject == _slots[i].button.gameObject && _slotInfo[i].Exists && _selectedSlot != i + 1)
                SelectSlot(i + 1);

        _loadNavigation.Clear();
        foreach (var row in _slots) if (row.button.isActiveAndEnabled && row.button.IsInteractable()) _loadNavigation.Add(row.button);
        foreach (var button in new[] { _loadSelectedButton, _deleteSelectedButton, _closeLoadButton })
            if (button.isActiveAndEnabled && button.IsInteractable()) _loadNavigation.Add(button);
        if (_loadNavigation.Count == 0) return;
        for (int i = 0; i < _loadNavigation.Count; i++)
        {
            var navigation = _loadNavigation[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = _loadNavigation[(i + _loadNavigation.Count - 1) % _loadNavigation.Count];
            navigation.selectOnDown = _loadNavigation[(i + 1) % _loadNavigation.Count];
            navigation.selectOnLeft = navigation.selectOnRight = null;
            _loadNavigation[i].navigation = navigation;
        }
        if (_loadFocusRequested || !_loadNavigation.Exists(button => button.gameObject == events.currentSelectedGameObject))
        {
            var first = _selectedSlot > 0 ? _slots[_selectedSlot - 1].button : null;
            events.SetSelectedGameObject(first != null && _loadNavigation.Contains(first) ? first.gameObject : _loadNavigation[0].gameObject);
            _loadFocusRequested = false;
        }
        var focused=events.currentSelectedGameObject;
        if(focused!=null && focused!=_lastLoadScrollFocus)
        {
            _lastLoadScrollFocus=focused;
            if(_loadScroll==null)_loadScroll=_loadPanel.GetComponentInChildren<ScrollRect>(true);
            if(_loadScroll!=null && focused.transform.IsChildOf(_loadScroll.content))
            {
                Canvas.ForceUpdateCanvases();
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(_loadScroll.viewport,focused.transform);
                var viewport=_loadScroll.viewport.rect;
                float shift=bounds.min.y<viewport.yMin?viewport.yMin-bounds.min.y:bounds.max.y>viewport.yMax?viewport.yMax-bounds.max.y:0;
                _loadScroll.content.anchoredPosition+=Vector2.up*shift;
            }
        }
    }
    /// <summary>존재하는 저장 슬롯을 선택하고 선택 표시를 갱신합니다.</summary>
    public void SelectSlot(int slot)
    {
        if (_page != Page.Load || slot < 1 || slot > _slots.Length || !_slotInfo[slot - 1].Exists) return;
        _selectedSlot = slot;
        RefreshSelection();
    }

    /// <summary>선택 슬롯의 불러오기를 기존 저장 서비스에 요청합니다.</summary>
    public void LoadSelected()
    {
        if (_busy || _popupTransitioning || _page != Page.Load || _selectedSlot == 0 || !_slotInfo[_selectedSlot - 1].CanLoad) return;
        LoadSlot(_selectedSlot, _slots[_selectedSlot - 1].savedAt);
    }

    /// <summary>씬 이동 요청을 한 번만 보내고 실패 시 다시 조작할 수 있게 합니다.</summary>
    void LoadSlot(int slot, TMP_Text errorLabel)
    {
        _busy = true; // 초기화 완료 여부와 중복 요청을 막는 처리 상태입니다.
        if (!GameSaveService.TryLoadSlot(slot, out string error)) { _busy = false; errorLabel.text = error; }
    }

    #endregion

    #region 저장 데이터 삭제

    /// <summary>삭제할 슬롯 번호와 이름을 보관하여 확인 전에는 저장 파일을 변경하지 않습니다.</summary>
    public void OpenDelete()
    {
        if (_page != Page.Load || _selectedSlot == 0) return;
        var info = GameSaveService.GetSlotInfo(_selectedSlot);
        if (!info.Exists) { RefreshSlots(); return; }
        _deleteSlot = _selectedSlot; // 목록 선택, 이어하기, 삭제 대상 슬롯 번호입니다.
        _deleteMessage.text = $"‘{info.PlayerName}’의 저장 데이터를\n정말 삭제하시겠습니까?";
        _deleteNotice.text = "삭제한 데이터는 복구할 수 없습니다.";
        ShowPage(Page.Delete);
    }

    /// <summary>명시적인 삭제 확인에만 기존 삭제 함수를 호출하고 슬롯 목록을 다시 표시합니다.</summary>
    public void ConfirmDelete()
    {
        if (_busy || _popupTransitioning || _page != Page.Delete) return;
        if (!GameSaveService.TryDeleteSlot(_deleteSlot, out string error)) { _deleteNotice.text = error; return; }
        _selectedSlot = 0;
        RefreshSlots();
        ShowPage(Page.Load);
    }

    /// <summary>삭제를 취소하고 기존 슬롯 선택을 유지한 채 목록으로 돌아갑니다.</summary>
    public void CancelDelete()
    {
        if (_page == Page.Delete) ShowPage(Page.Load);
    }

    #endregion

    #region 설정 및 종료

    /// <summary>Canvas_Base에서 분리한 동일한 설정 화면을 엽니다.</summary>
    public void OpenSettings()
    {
        if (_page != Page.Main) return;
        ShowPage(Page.Settings);
    }

    /// <summary>종료 확인 팝업을 표시하며 이 단계에서는 게임을 종료하지 않습니다.</summary>
    public void OpenQuit()
    {
        if (_page == Page.Main) ShowPage(Page.Quit);
    }

    /// <summary>종료 확인 시 에디터에서는 플레이를 정지하고 빌드에서는 앱을 종료합니다.</summary>
    public void ConfirmQuit()
    {
        if (_busy || _popupTransitioning || _page != Page.Quit) return;
        _busy = true; // 초기화 완료 여부와 중복 요청을 막는 처리 상태입니다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region 표시 문자열 변환

    /// <summary>잘못된 저장 시각도 예외 없이 표시할 수 있는 문자열로 변환합니다.</summary>
    static string FormatTime(long ticks) => ticks > 0 && ticks <= DateTime.MaxValue.Ticks
        ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "알 수 없음";

    /// <summary>저장된 시간대를 화면 표시용 한글로 변환합니다.</summary>
    static string PhaseName(DayPhase phase) => phase == DayPhase.Morning ? "오전" : phase == DayPhase.Afternoon ? "오후" : "저녁";

    /// <summary>기존 저장 씬 이름을 표시용 장소 이름으로 변환합니다.</summary>
    static string LocationName(string scene)
    {
        switch (scene)
        {
            case "MorningBoat": case "AfterNoonBoat": case "EveningBoat": return "선상";
            case "Dive": case "Dive 1": case "BossTest": return "잠수";
            case "Sushi": case "Sushi 2": return "초밥";
            default: return string.IsNullOrEmpty(scene) ? "위치 미상" : scene;
        }
    }
    #endregion
}
