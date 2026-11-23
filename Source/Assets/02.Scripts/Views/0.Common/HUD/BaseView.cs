using System;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다중 씬 공통 뷰
/// </summary>
public class BaseView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("베이스 패널")]
    [SerializeField] GameObject _basePanel; // 베이스 패널

    [Header("보유 재화")]
    [SerializeField] GameObject _goldObject; // 보유 골드 오브젝트
    [SerializeField] TMP_Text _goldText; // 보유 골드 텍스트

    [Header("퀘스트(메모)")]
    [SerializeField] Button _questMemoButton; // 퀘스트 온오프 버튼

    [Header("날짜 / 날씨")]
    [SerializeField] TMP_Text _dateText; // 현재 날짜 텍스트
    [SerializeField] Image _weatherIconImage; // 현재 날씨 아이콘 이미지
    [SerializeField] Sprite _cloudWeatherIcon;
    [SerializeField] Sprite _rainWeatherIcon; // 비 전용 아이콘이며 정식 리소스로 교체할 수 있습니다.
    [SerializeField] Sprite _dryWeatherIcon; // 비 연출이 있는 씬에서 비가 그쳤을 때 표시할 아이콘입니다.
    SushiRainEffect _sceneRainEffect;
    Sprite _requestedWeatherIcon; // 기존 씬에서 마지막으로 전달한 날씨 아이콘입니다.
    float _nextWeatherSourceSearch;

    [Header("시간")]
    [SerializeField] TMP_Text _currentTimeText; // 현재 시간 텍스트
    [SerializeField] Image _currnetTimeImage; // 현재 시간 표시 이미지
    [SerializeField] Button _timeSkipButton; // 시간 스킵 버튼
    bool _businessTimeSkipBlocked;
    bool _timeSkipVisible = true; // 이 UI를 사용하는 씬 View가 표시 여부를 지정합니다.

    [Header("스마트폰")]
    [SerializeField] Button _smartPhoneButton; // 스마트폰 버튼
    // BasePanel/SmartPhone/AppGroup의 앱 Button들을 순서대로 연결합니다. 클릭 이벤트는 코드가 자동으로 연결합니다.
    [SerializeField] Button[] _appButtons;
    // BasePanel/SmartPhone/AppGroup의 Transform입니다. 버튼 배열이 비어 있으면 여기서 자식 Button을 찾습니다.
    [SerializeField] Transform _appGroup;
    // Canvas_Board/SmartPhonePanel GameObject를 연결합니다. 앱 페이지들의 공통 부모입니다.
    [SerializeField] GameObject _smartPhonePanel;
    // SmartPhonePanel의 검은 반투명 Image를 연결합니다. 앱이 하나라도 열려 있으면 켭니다.
    [SerializeField] Image _smartPhoneBackground;
    // AppButtons와 같은 순서로 Inventory, Quest, Setting 페이지 GameObject를 연결합니다.
    [SerializeField] GameObject[] _appPages = new GameObject[3];

    #endregion

    #region 런타임 상태

    Button[] _wiredAppButtons; // 입력 연결 앱 버튼 목록입니다.
    UnityEngine.Events.UnityAction[] _appCallbacks; // 앱 콜백 목록입니다.
    readonly List<Button> _suppressedButtons = new List<Button>(); // 억제 버튼 목록입니다.
    readonly List<int> _suppressedIndices = new List<int>(); // 억제 인덱스 목록입니다.
    readonly List<UnityEngine.Events.UnityEventCallState> _suppressedStates = new List<UnityEngine.Events.UnityEventCallState>(); // 억제 상태 목록입니다.
    readonly List<Image> _pageBackgrounds = new List<Image>(); // 페이지 배경 목록입니다.
    readonly List<bool> _pageBackgroundEnabledStates = new List<bool>(); // 페이지 배경 활성 상태 목록입니다.
    bool _appsInitialized; // 앱 목록 초기화 여부입니다.
    bool _originalBackgroundEnabled; // 원본 배경 활성 여부입니다.
    bool _originalBackgroundRaycastTarget; // 원본 배경 포인터 감지 대상 여부입니다.
    int _currentAppIndex = -1; // 현재 앱 인덱스 값입니다.

    #endregion

    #region UI 연결 및 표시 설정

    // 별도 UI 연결은 필요 없습니다. 기존 SmartPhoneIcon Button과 AppGroup Transform을 사용합니다.

    [Header("스마트폰 앱 목록 슬라이드")]
    [Tooltip("AppGroup이 숨겨지거나 펼쳐지는 데 걸리는 시간(초). 0이면 즉시 이동합니다.")]
    [SerializeField, Min(0f)] float _appGroupSlideDuration = 0.2f;
    // 화면 아래쪽 끝보다 AppGroup을 더 밀어 넣을 여백입니다. Canvas 기준 단위입니다.
    [SerializeField, Min(0f)] float _appGroupHiddenMargin = 20f;

    #endregion

    [SerializeField, HideInInspector] bool _appGroupShownPositionSaved;
    [SerializeField, HideInInspector] Vector2 _appGroupSavedShownPosition;
    readonly Vector3[] _phoneCorners = new Vector3[4];

    #region 런타임 상태

    RectTransform _appGroupRect; // 앱 그룹 사각 영역 값입니다.
    Button _wiredSmartPhoneButton; // 입력 연결 스마트 스마트폰 버튼입니다.
    Vector2 _appGroupShownPosition; // 앱 그룹 노출 위치 값입니다.
    Vector2 _appGroupHiddenPosition; // 앱 그룹 숨김 위치 값입니다.
    Vector2 _appGroupSlideStart; // 앱 그룹 슬라이드 시작 값입니다.
    float _appGroupSlideElapsed; // 앱 그룹 슬라이드 경과 값입니다.
    bool _appGroupExpanded; // 앱 그룹 펼침 여부입니다.
    bool _appGroupSliding; // 앱 그룹 슬라이드 중 여부입니다.
    bool _appGroupSlideInitialized; // 앱 그룹 슬라이드 초기화 여부입니다.

    #endregion

    #region UI 연결 및 표시 설정

    [SerializeField] bool _boardMotion; // Board Variant에서만 이번 연출 규칙을 활성화합니다.

    #endregion

    #region 런타임 상태

    int _appSwitchVersion; // 앱 전환 회차 값입니다.
    int _requestedAppIndex = -1; // 요청 앱 인덱스 값입니다.
    bool _appSwitchPending; // 앱 전환 대기 여부입니다.
    Tween _appGroupSlideTween; // 이 View에서 만든 스마트폰 연출만 관리합니다.

    #endregion

    #region UI 연결 및 표시 설정

    [Header("대화")]
    [SerializeField] TalkView _talkView; // 대화 뷰 표시를 연결할 뷰입니다.

    #endregion

    #region 이벤트 액션

    /// <summary>
    /// 시간 스킵 이벤트
    /// </summary>
    public event Action OnSkipTime;

    #endregion

    #region 런타임 상태

    TalkView _talkModeOwner;
    GameObject _talkPreviousSelection;
    readonly Dictionary<CanvasGroup, TalkUiState> _talkUiStates = new Dictionary<CanvasGroup, TalkUiState>();

    #endregion

    #region UI 연결 및 표시 설정

    [Header("반원 시계 색 영역 경계")]
    // Main_Clock_Fill의 오전(파랑) 종료 위치입니다. _currnetTimeImage에는 DailyInfo/Background/ClockFill을 연결합니다.
    [SerializeField, Range(0f, 1f)] float _morningClockEnd = 0.30f; // 오전 시계 종료 값입니다.
    // 오후(주황) 종료 위치입니다. 이미지 색 경계가 바뀌면 오전 종료값보다 크게 조정합니다.
    [SerializeField, Range(0f, 1f)] float _afternoonClockEnd = 0.70f;
    // ClockFill 아래 ClockBlue, ClockOrange, ClockPurple Image를 오전·오후·저녁 순서로 연결합니다.
    [SerializeField] Image[] _clockPhaseImages = new Image[3];
    // 현재 파트가 아닌 두 영역에 곱할 색입니다. 원본 스프라이트는 변경하지 않습니다.
    [SerializeField] Color _inactiveClockPartColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    #endregion

    #region 런타임 상태

    GameManager _clockTimeSource; // 시간대 변경만 읽는 대상입니다. 실제 시간이나 날짜는 변경하지 않습니다.
    DayPhase _clockPhase; // 현재 시계에 표시한 시간대입니다.
    float _clockPartProgress; // 현재 파트 안에서의 표시 진행률입니다.
    [SerializeField] RectTransform _clockNeedle;
    [SerializeField, Min(0f)] float _clockNeedleEntranceDuration = 1.5f;
    Tween _clockNeedleEntrance;
    bool _clockNeedlePresented;
    bool _clockBusinessRunning;
    float _clockNeedleTargetAngle;
    bool _clockHasProgress; // 외부 또는 초기화에서 시계 진행률을 받은 상태입니다.

    #endregion

    #region 임시 데이터 및 테스트 멤버

    [Header("시계 UI 테스트")]
    // 테스트할 오전·오후·저녁을 선택합니다. ContextMenu의 선택 파트 시작으로 30초 동안 재생합니다.
    [SerializeField] ClockTestPart _clockTestPart = ClockTestPart.Morning; // 시계 테스트 구간 값입니다.
    // 기존 전체 흐름 테스트의 파트당 시간입니다. 선택한 단일 파트 테스트는 30초로 고정합니다.
    [SerializeField, Min(0.1f)] float _clockTestSecondsPerPart = 30f;
    System.Collections.IEnumerator _clockTestRoutine; // 이 View에서 실행한 시계 테스트 루틴입니다.
    bool _clockTestActive; // 테스트 전 표시를 한 번만 보관하기 위한 상태입니다.
    float _clockTestPreviousFill, _clockTestPreviousProgress; // 테스트 전의 전체 채움값과 파트 진행률입니다.
    DayPhase _clockTestPreviousPhase; // 테스트 전 표시 시간대입니다.
    bool _clockTestPreviousHasProgress; // 테스트 전 데이터 수신 상태입니다.
    Color _clockTestPreviousColor; // 테스트 전 ClockFill의 색입니다.
    bool[] _clockTestPreviousPartActive; // 테스트 전 파트별 이미지의 활성 상태입니다.
    Color[] _clockTestPreviousPartColors; // 테스트 전 파트별 이미지의 색입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 현재 앱 인덱스 값입니다.
    /// </summary>
    public int CurrentAppIndex => _currentAppIndex;

    /// <summary>
    /// 대화 뷰 표시를 연결할 뷰입니다.
    /// </summary>
    public TalkView TalkView => _talkView;
    public bool IsTalkMode => _talkModeOwner != null;

    /// <summary>
    /// 현재 시계에 표시 중인 시간대를 읽습니다.
    /// </summary>
    public DayPhase ClockPhase => _clockPhase;

    /// <summary>
    /// 현재 파트 안의 시계 표시 진행률을 0~1로 읽습니다.
    /// </summary>
    public float ClockPartProgress => _clockPartProgress;

    #endregion

    #region 표시 데이터 형식

    /// <summary>
    /// 대화 시작 전의 UI 입력 상태입니다.
    /// </summary>
    struct TalkUiState
    {

        #region 멤버

        #region 런타임 상태

        public bool Interactable, BlocksRaycasts;
        public float Alpha;

        #endregion

        #endregion

    }

    /// <summary>
    /// 인스펙터에서 선택할 시계 테스트 파트입니다. 실제 게임 시간대와 분리되어 있습니다.
    /// </summary>
    public enum ClockTestPart
    {
        [InspectorName("오전")]
        Morning,
        [InspectorName("오후")]
        Afternoon,
        [InspectorName("저녁")]
        Evening
    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 시간 스킵 버튼과 스마트폰 앱 열기·닫기 이벤트를 연결합니다.
    /// </summary>
    private void Awake()
    {
        if (_timeSkipButton != null)
        {
            _timeSkipButton.onClick.AddListener(SkipTime);
        }

        InitializeSmartPhoneApps();
        // 추가: 배치해 둔 위치를 펼침 위치로 기억하고, 시작할 때는 화면 밖으로 숨깁니다.
        InitializeAppGroupSlide();
    }

    /// <summary>HUD가 켜지면 시간스킵을 복원합니다. 튜토리얼 등 개별 제한은 해당 씬에서 적용합니다.</summary>
    void RefreshTimeSkipForScene()
    {
        SetTimeSkipVisible(true);
    }

    /// <summary>HUD를 다시 켜면 씬별 시간스킵과 현재 시간대 표시를 복원합니다.</summary>
    void OnEnable()
    {
        RefreshTimeSkipForScene();
        if (_clockHasProgress) ApplyClockProgress(_clockPhase, _clockPartProgress);
    }

    /// <summary>영업이 확정된 뒤에는 HUD 재활성화로도 시간스킵이 다시 표시되지 않게 합니다.</summary>
    public void SetTimeSkipBusinessActive(bool active)
    {
        if (_businessTimeSkipBlocked == active) return;
        _businessTimeSkipBlocked = active;
        SetTimeSkipVisible(!active);
    }
    /// <summary>씬 View가 시간스킵 배경·문구·아이콘과 입력을 함께 켜거나 끕니다.</summary>
    public void SetTimeSkipVisible(bool visible)
    {
        visible &= !_businessTimeSkipBlocked;
        _timeSkipVisible = visible;
        if (_timeSkipButton != null) _timeSkipButton.interactable = visible;
        var root = transform.Find("DailyInfo/TimeSkip");
        if (root == null && _timeSkipButton != null)
        {
            root = _timeSkipButton.transform;
            while (root.parent != null && root.parent != transform && root.name != "TimeSkip") root = root.parent;
            // 부모를 찾지 못하면 다른 HUD까지 숨기지 않고 버튼만 전환합니다.
            if (root.name != "TimeSkip") root = _timeSkipButton.transform;
        }
        if (root != null) root.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 편집 중 꺼 둔 스마트폰을 첫 실행 시 한 번 켭니다. 앱 목록의 기본 숨김 상태는 유지합니다.
    /// </summary>
    void Start()
    {

        RefreshTimeSkipForScene();
        ActivateSmartPhoneAtStartup();
        Canvas.ForceUpdateCanvases();
        ResetAppGroupSlide();
        InitializeClockPresentation();
        RefreshSceneWeatherIcon();
    }

    /// <summary>
    /// 앱 자체의 닫기 버튼이나 외부 코드로 바뀐 표시 상태를 공통 배경에 반영합니다.
    /// </summary>
    void LateUpdate()
    {
        UpdateAppBackground();
        RefreshSceneWeatherIcon();
        UpdateAppGroupSlide(); // 추가: 일시정지 상태에서도 스마트폰 목록을 부드럽게 이동합니다.
    }

    /// <summary>
    /// 공통 베이스 화면이 숨겨지면 열려 있는 앱과 반투명 배경을 닫습니다.
    /// </summary>
    void OnDisable()
    {
        StopClockNeedleEntrance();
        StopClockTest(true); // 추가: 숨겨질 때 이 View의 시계 테스트만 종료합니다.
        if (_appsInitialized)
        {
            CloseAllApps();
        }

        // 추가: 베이스 화면을 다시 열어도 앱 목록은 기본 숨김 상태로 시작합니다.
        ResetAppGroupSlide();
    }

    /// <summary>
    /// 버튼 이벤트를 해제하고 실행 중 조정했던 기존 UI 설정을 복원합니다.
    /// </summary>
    void OnDestroy()
    {
        ReleaseClockPresentation(); // 추가: 시계가 구독한 시간대 이벤트만 해제합니다.
        _appGroupSlideTween?.Kill();
        if (_timeSkipButton != null)
        {
            _timeSkipButton.onClick.RemoveListener(SkipTime);
        }

        if (_wiredSmartPhoneButton != null)
        {
            _wiredSmartPhoneButton.onClick.RemoveListener(ToggleAppGroup);
        }

        if (_wiredAppButtons != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _wiredAppButtons.Length; i++)
            {
                if (_wiredAppButtons[i] != null && _appCallbacks[i] != null)
                {
                    _wiredAppButtons[i].onClick.RemoveListener(_appCallbacks[i]);
                }
            }
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _suppressedButtons.Count; i++)
        {
            if (_suppressedButtons[i] != null && _suppressedIndices[i] < _suppressedButtons[i].onClick.GetPersistentEventCount())
            {
                _suppressedButtons[i].onClick.SetPersistentListenerState(_suppressedIndices[i], _suppressedStates[i]);
            }
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _pageBackgrounds.Count; i++)
        {
            if (_pageBackgrounds[i] != null)
            {
                _pageBackgrounds[i].enabled = _pageBackgroundEnabledStates[i];
            }
        }

        if (_smartPhoneBackground != null && _appsInitialized)
        {
            _smartPhoneBackground.enabled = _originalBackgroundEnabled;
            _smartPhoneBackground.raycastTarget = _originalBackgroundRaycastTarget;
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 시간 스킵 요청을 외부 구독자에게 전달합니다.
    /// </summary>
    void SkipTime()
    {
        // 비활성 버튼을 코드로 호출하더라도 초밥집의 진행을 건너뛰지 않습니다.
        if (!_timeSkipVisible) return;
        OnSkipTime?.Invoke();
    }

    /// <summary>
    /// 전달받은 시간 문자열을 현재 연결된 날짜 텍스트에 표시합니다.
    /// </summary>
    /// <param name="msg">메시지 값입니다.</param>
    public void UpdateCurrnetTimeText(string msg)
    {
        _dateText.text = msg;
    }

    /// <summary>
    /// 현재 시간을 나타내는 이미지를 변경합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    public void UpdateCurrentTimeImage(Sprite icon)
    {
        _currnetTimeImage.sprite = icon;
    }

    /// <summary>
    /// 같은 앱 버튼은 닫고, 다른 앱 버튼은 기존 앱을 닫은 뒤 선택한 앱을 엽니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    public void ToggleApp(int index)
    {
        InitializeSmartPhoneApps();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_appPages == null || index < 0 || index >= _appPages.Length || _appPages[index] == null)
        {
            return;
        }

        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (_boardMotion && Application.isPlaying)
        {
            ToggleAnimatedApp(index);
            return;
        }

        bool wasOpen = _appPages[index].activeInHierarchy; // 이전 열기 여부입니다.
        CloseAllApps();
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (wasOpen)
        {
            return;
        }

        if (_smartPhonePanel != null)
        {
            _smartPhonePanel.SetActive(true);
        }

        SetAppPage(index, true);
        UpdateAppBackground();
    }

    /// <summary>
    /// 현재 실제로 보이는 앱을 확인해 배경과 선택 앱 인덱스를 동기화합니다.
    /// </summary>
    void UpdateAppBackground()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_appsInitialized)
        {
            return;
        }

        _currentAppIndex = -1;
        if (_appPages != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _appPages.Length; i++)
            {
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (_appPages[i] != null && _appPages[i].activeInHierarchy && (_appPages[i].GetComponent<AppFadeView>() == null || _appPages[i].GetComponent<AppFadeView>().IsDisplaying))
                {
                    _currentAppIndex = i;
                    break;
                }
            }
        }

        if (_smartPhoneBackground != null)
        {
            _smartPhoneBackground.enabled = _currentAppIndex >= 0;
        }
    }

    /// <summary>
    /// 두 스마트폰 아이콘을 누를 때마다 앱 목록의 숨김과 펼침을 전환합니다.
    /// </summary>
    public void ToggleAppGroup()
    {
        if (UIEscapeView.ConsumeEscapeShortcut()) return;
        ToggleAppGroupCore();
    }

    /// <summary>화면에 표시된 스마트폰 버튼만 공통 ESC 처리의 대상으로 제공합니다.</summary>
    internal Button SmartPhoneEscapeButton => !IsTalkMode && _appGroup != null ? _smartPhoneButton : null;

    /// <summary>공통 ESC 처리에서 중복 입력 검사 후 기존 스마트폰 슬라이드를 실행합니다.</summary>
    internal void ToggleSmartPhoneFromEscape() => ToggleAppGroupCore();

    /// <summary>마우스 클릭과 ESC가 같은 펼침·접힘 상태와 슬라이드 연출을 사용합니다.</summary>
    void ToggleAppGroupCore()
    {
        InitializeAppGroupSlide();
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_appGroupSlideInitialized)
        {
            return;
        }

        _appGroupSlideTween?.Kill();
        // 비활성 상태에서도 펼침 위치는 별도로 보관하며, 목록을 켜기 전에 닫힘 위치를 확정합니다.
        _appGroupHiddenPosition = CalculateAppGroupHiddenPosition();
        _appGroupRect.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        _appGroupHiddenPosition = CalculateAppGroupHiddenPosition();
        _appGroupExpanded = !_appGroupExpanded;
        // 연속 클릭 시 현재 위치부터 반대 방향으로 움직여 위치가 튀지 않게 합니다.
        _appGroupSlideStart = _appGroupRect.anchoredPosition;
        _appGroupSlideElapsed = 0f;
        _appGroupSliding = true;
        _appGroupSlideTween?.Kill();
        Vector2 target = _appGroupExpanded ? _appGroupShownPosition : _appGroupHiddenPosition; // 대상 값입니다.
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_appGroupSlideDuration <= 0f)
        {
            _appGroupRect.anchoredPosition = target;
            _appGroupSliding = false;
            _appGroupSlideTween = null;
            if (!_appGroupExpanded) ResetAppGroupSlide();
            return;
        }

        // DOTween 연출: 화면 아래에서 위로 살짝 튀어나오고, 닫을 때는 아래로 내려갑니다.
        // 빠르게 다시 눌러도 현재 위치에서 이어지며, 일시정지 중에도 재생합니다.
        _appGroupSlideTween = _appGroupRect.DOAnchorPos(target, _appGroupSlideDuration).SetEase(_boardMotion ? Ease.OutBack : _appGroupExpanded ? Ease.OutBack : Ease.InCubic, .7f).SetUpdate(true).OnComplete(() =>
        {
            _appGroupSliding = false;
            _appGroupSlideTween = null;
            if (!_appGroupExpanded) ResetAppGroupSlide();
        });
    }

    /// <summary>
    /// 시간 배율에 영향받지 않고 앱 목록을 목표 위치까지 부드럽게 이동합니다.
    /// </summary>
    void UpdateAppGroupSlide()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (!_appGroupSlideInitialized || _appGroupRect == null) return;
        // 해상도와 Canvas 배치가 시작 이후 바뀌어도 화면 밖 위치를 계속 유지합니다.
        if (!_appGroupExpanded && !_appGroupSliding)
        {
            _appGroupHiddenPosition = CalculateAppGroupHiddenPosition();
            _appGroupRect.anchoredPosition = _appGroupHiddenPosition;
            if (_appGroupRect.gameObject.activeSelf) _appGroupRect.gameObject.SetActive(false);
            return;
        }
        if (!_appGroupSliding) return;
        _appGroupSlideElapsed += Time.unscaledDeltaTime;
    // 이동 좌표는 DOTween이 갱신합니다. 기존 함수와 진행 상태 필드는 호환을 위해 유지합니다.
    }

    /// <summary>
    /// 진행 중인 이동을 끝내고 앱 목록을 아래쪽 화면 밖에 즉시 숨깁니다.
    /// </summary>
    void ResetAppGroupSlide()
    {
        // DOTween 연출 정리: 비활성화 시 진행 중인 스마트폰 Tween을 종료합니다.
        _appGroupSlideTween?.Kill();
        _appGroupSlideTween = null;
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (!_appGroupSlideInitialized || _appGroupRect == null)
        {
            return;
        }

        _appGroupExpanded = false;
        _appGroupSliding = false;
        _appGroupHiddenPosition = CalculateAppGroupHiddenPosition();
        _appGroupRect.anchoredPosition = _appGroupHiddenPosition;
        _appGroupRect.gameObject.SetActive(false);
    }

    /// <summary>
    /// 보유 골드 표시 영역을 켜거나 끕니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetGoldObject(bool isOn)
    {
        _goldObject.SetActive(isOn);
    }

    /// <summary>
    /// 보유 골드를 정수 형식의 텍스트로 표시합니다.
    /// </summary>
    /// <param name="amount">수량 값입니다.</param>
    public void UpdateGoldText(float amount)
    {
        _goldText.text = $"{amount:F0}";
    }

    /// <summary>
    /// 전달받은 날짜 문자열을 별도 변환 없이 그대로 표시합니다.
    /// </summary>
    /// <param name="date">외부에서 표시 형식을 정한 문자열입니다.</param>
    public void UpdateDateText(string date)
    {
        _dateText.text = date;
    }

    /// <summary>
    /// 기존 DateTime 호출의 호환성을 유지하고 문자열 표시 함수로 전달합니다.
    /// </summary>
    /// <param name="date">기존 호출부에서 전달한 날짜 값입니다.</param>
    public void UpdateDateText(DateTime date)
    {
        UpdateDateText(date.ToString());
    }

    /// <summary>
    /// 현재 날씨 아이콘을 변경합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    public void UpdateWeatherIconImage(Sprite icon)
    {
        if(icon!=null){
            var latest=icon.name.IndexOf("rain",StringComparison.OrdinalIgnoreCase)>=0?_rainWeatherIcon:icon.name.IndexOf("cloud",StringComparison.OrdinalIgnoreCase)>=0?_cloudWeatherIcon:_dryWeatherIcon;
            _requestedWeatherIcon=latest!=null?latest:icon;
            if(_weatherIconImage!=null)_weatherIconImage.sprite=_requestedWeatherIcon;
        }
        RefreshSceneWeatherIcon();
    }

    /// <summary>
    /// 같은 씬의 실제 비 생성 상태를 읽어 HUD만 갱신합니다. 날씨나 파티클 자체는 변경하지 않습니다.
    /// 비 연출이 없는 씬은 기존 PlayScene의 날씨 아이콘 전달 방식을 그대로 사용합니다.
    /// </summary>
    void RefreshSceneWeatherIcon()
    {
        if (_weatherIconImage == null) return;
        var manager=GameManager.Instance;
        if(manager!=null && manager.CurrentWeather!=WeatherType.Count){
            var weather=manager.CurrentWeather;
            var current=weather==WeatherType.Rainy?_rainWeatherIcon:weather==WeatherType.Cloudy?_cloudWeatherIcon:_dryWeatherIcon;
            if(current!=null)_weatherIconImage.sprite=current;
            return;
        }
        if (_sceneRainEffect == null && Time.unscaledTime >= _nextWeatherSourceSearch)
        {
            _nextWeatherSourceSearch = Time.unscaledTime + 1f;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                _sceneRainEffect = root.GetComponentInChildren<SushiRainEffect>(true);
                if (_sceneRainEffect != null) break;
            }
        }
        if (_sceneRainEffect == null) return;
        // 연출 오브젝트가 꺼져 있으면 비가 내리지 않는 화면으로 간주합니다.
        bool raining = _sceneRainEffect.isActiveAndEnabled && _sceneRainEffect.IsRaining;
        var icon = raining ? _rainWeatherIcon : _dryWeatherIcon;
        if (icon == null) icon = _requestedWeatherIcon;
        if (icon != null && _weatherIconImage.sprite != icon) _weatherIconImage.sprite = icon;
    }

    /// <summary>
    /// 대화창에 입력을 우선 배정하고 이전 포커스를 기억합니다.
    /// </summary>
    /// <param name="owner">소유자 표시를 연결할 뷰입니다.</param>
    public void BeginTalkMode(TalkView owner)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (owner == null || _talkModeOwner == owner)
        {
            return;
        }

        if (_talkModeOwner != null)
        {
            _talkModeOwner.EndTalk();
        }

        _talkModeOwner = owner;
        var eventSystem = UnityEngine.EventSystems.EventSystem.current; // 이벤트 시스템 값입니다.
        _talkPreviousSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(null);
        }

        RefreshTalkMode(owner);
    }

    /// <summary>
    /// 대화창 바깥의 Canvas 입력을 차단합니다.
    /// </summary>
    /// <param name="owner">소유자 표시를 연결할 뷰입니다.</param>
    public void RefreshTalkMode(TalkView owner)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (owner == null || _talkModeOwner != owner)
        {
            return;
        }

        // 일반 대화와 튜토리얼은 배경 UI의 표시·입력 상태를 유지한다.
        // 특정 단계의 버튼 잠금은 해당 튜토리얼에서 별도로 관리한다.
        if (owner.KeepsBackgroundVisible) return;

        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None); // 캔버스 목록 값입니다.
        // canvas: 캔버스 값입니다.
        foreach (var canvas in canvases)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (canvas.rootCanvas != canvas || canvas.transform.IsChildOf(owner.transform))
            {
                continue;
            }

            BlockOtherTalkUiInput(canvas.transform, owner.transform);
        }
    }

    /// <summary>
    /// 대화가 끝나면 이전 UI 입력 상태를 복원합니다.
    /// </summary>
    /// <param name="owner">소유자 표시를 연결할 뷰입니다.</param>
    public void EndTalkMode(TalkView owner)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_talkModeOwner != owner)
        {
            return;
        }

        _talkModeOwner = null;
        // pair: pair 값입니다.
        foreach (var pair in _talkUiStates)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (pair.Key == null)
            {
                continue;
            }

            if (!owner.KeepsBackgroundVisible) pair.Key.alpha = pair.Value.Alpha;
            pair.Key.interactable = pair.Value.Interactable;
            pair.Key.blocksRaycasts = pair.Value.BlocksRaycasts;
        }

        _talkUiStates.Clear();
        var eventSystem = UnityEngine.EventSystems.EventSystem.current; // 이벤트 시스템 값입니다.
        if (eventSystem != null && _talkPreviousSelection != null && _talkPreviousSelection.activeInHierarchy)
        {
            eventSystem.SetSelectedGameObject(_talkPreviousSelection);
        }

        _talkPreviousSelection = null;
    }

    /// <summary>
    /// BasePanel 바로 아래 SmartPhone을 찾아 활성화합니다. 추가 인스펙터 연결은 필요 없습니다.
    /// </summary>
    void ActivateSmartPhoneAtStartup()
    {
        // 추가: 기존 초기화 함수와 참조를 유지하고 SmartPhone 루트만 켭니다.
        Transform smartPhone = transform.Find("SmartPhone");
        if (smartPhone == null && _basePanel != null)
        {
            smartPhone = _basePanel.transform.Find("SmartPhone");
        }

        if (smartPhone != null && !smartPhone.gameObject.activeSelf)
        {
            smartPhone.gameObject.SetActive(true);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// AppGroup 버튼과 앱 페이지를 찾고 중복 없는 토글 이벤트를 한 번만 연결합니다.
    /// </summary>
    void InitializeSmartPhoneApps()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_appsInitialized)
        {
            return;
        }

        _appsInitialized = true;
        if (_appGroup == null)
        {
            _appGroup = transform.Find("SmartPhone/AppGroup");
        }

        if ((_appButtons == null || _appButtons.Length == 0) && _appGroup != null)
        {
            _appButtons = _appGroup.GetComponentsInChildren<Button>(true);
        }

        if (_smartPhonePanel == null && transform.parent != null)
        {
            var panel = transform.parent.Find("SmartPhonePanel"); // 패널 값입니다.
            if (panel != null)
            {
                _smartPhonePanel = panel.gameObject;
            }
        }

        if (_smartPhoneBackground == null && _smartPhonePanel != null)
        {
            _smartPhoneBackground = _smartPhonePanel.GetComponent<Image>();
        }

        int count = _appButtons != null ? _appButtons.Length : 0; // 개수 값입니다.
        if (_appPages == null || _appPages.Length < count)
        {
            Array.Resize(ref _appPages, count);
        }

        string[] pageNames =
        { "Inventory", "Quest", "Setting" }; // 페이지 이름 목록입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < count; i++)
        {
            if (_appPages[i] == null && _smartPhonePanel != null && i < pageNames.Length)
            {
                var page = _smartPhonePanel.transform.Find(pageNames[i]); // 페이지 값입니다.
                if (page != null)
                {
                    _appPages[i] = page.gameObject;
                }
            }

            // 앱 루트의 전체 화면 배경은 숨기고 공통 검은 Image를 사용합니다. 자식 창과 버튼은 유지합니다.
            var image = _appPages[i] != null ? _appPages[i].GetComponent<Image>() : null;
            if (image != null && image != _smartPhoneBackground)
            {
                _pageBackgrounds.Add(image);
                _pageBackgroundEnabledStates.Add(image.enabled);
                image.enabled = false;
            }
        }

        if (_smartPhoneBackground != null)
        {
            _originalBackgroundEnabled = _smartPhoneBackground.enabled;
            _originalBackgroundRaycastTarget = _smartPhoneBackground.raycastTarget;
            // 배경 이미지가 아래쪽 AppGroup 버튼의 재클릭을 가리지 않게 합니다.
            _smartPhoneBackground.raycastTarget = false;
        }

        _wiredAppButtons = _appButtons != null ? (Button[])_appButtons.Clone() : Array.Empty<Button>();
        _appCallbacks = new UnityEngine.Events.UnityAction[_wiredAppButtons.Length];
        // i: 반복 값입니다.
        for (int i = 0; i < _wiredAppButtons.Length; i++)
        {
            var button = _wiredAppButtons[i]; // 버튼 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (button == null)
            {
                continue;
            }

            SuppressOldAppOpenEvents(button, _appPages[i]);
            int index = i; // 인덱스 값입니다.
            _appCallbacks[i] = () => ToggleApp(index);
            button.onClick.AddListener(_appCallbacks[i]);
        }

        CloseAllApps();
    }

    /// <summary>
    /// 아이콘 클릭을 연결하고 AppGroup의 원래 위치를 저장한 뒤 기본 숨김 상태로 둡니다.
    /// </summary>
    void InitializeAppGroupSlide()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_appGroupSlideInitialized)
        {
            return;
        }

        _appGroupRect = _appGroup as RectTransform;
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_appGroupRect == null)
        {
            return;
        }

        if (_smartPhoneButton == null)
        {
            var icon = transform.Find("SmartPhone/SmartPhoneIcon"); // 아이콘 값입니다.
            if (icon != null)
            {
                _smartPhoneButton = icon.GetComponent<Button>();
            }
        }

        _appGroupShownPosition = _appGroupShownPositionSaved
            ? _appGroupSavedShownPosition : _appGroupRect.anchoredPosition;
        _appGroupSlideInitialized = true;
        _wiredSmartPhoneButton = _smartPhoneButton;
        if (_wiredSmartPhoneButton != null)
        {
            _wiredSmartPhoneButton.onClick.AddListener(ToggleAppGroup);
        }

        // SmartPhoneSubIcon은 SmartPhoneIcon의 자식 Image입니다.
        // 자식 Image의 클릭도 부모 Button으로 전달되므로 같은 이벤트를 한 번만 실행합니다.
        // 두 아이콘은 AppGroup 바깥에 있으므로 목록을 숨긴 뒤에도 계속 누를 수 있습니다.
        Canvas.ForceUpdateCanvases();
        ResetAppGroupSlide();
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 해당 앱에 직접 연결된 기존 열기 이벤트만 실행 중 잠시 꺼서 이중 토글을 방지합니다.
    /// </summary>
    /// <param name="button">버튼입니다.</param>
    /// <param name="page">페이지 값입니다.</param>
    void SuppressOldAppOpenEvents(Button button, GameObject page)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (page == null)
        {
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            var target = button.onClick.GetPersistentTarget(i); // 대상 값입니다.
            var component = target as Component; // 컴포넌트 값입니다.
            bool samePage = target == page || component != null && component.gameObject == page; // 동일 페이지 여부입니다.
            string method = button.onClick.GetPersistentMethodName(i); // 방식 값입니다.
            bool opensPage = method == "Open" || method == "SetInventory" || method == "SetMainPage" || method == "SetMainSetting" || method == "SetActive"; // 열기 목록 페이지 여부입니다.
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (!samePage || !opensPage)
            {
                continue;
            }

            _suppressedButtons.Add(button);
            _suppressedIndices.Add(i);
            _suppressedStates.Add(button.onClick.GetPersistentListenerState(i));
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
        }
    }

    /// <summary>
    /// 각 앱 View의 기존 열기·닫기 기능으로 페이지와 내부 상세 상태를 전환합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetAppPage(int index, bool isOn)
    {
        var page = _appPages[index]; // 페이지 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (page == null)
        {
            return;
        }

        var fade = page.GetComponent<AppFadeView>(); // 페이드 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (fade != null && fade.TrySetVisible(isOn, () => SetAppPage(index, isOn)))
        {
            return;
        }

        var inventory = page.GetComponent<InventoryView>(); // 인벤토리 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (inventory != null)
        {
            inventory.SetInventory(isOn);
            return;
        }

        var quest = page.GetComponent<QuestListView>(); // 퀘스트 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (quest != null)
        {
            quest.SetMainPage(isOn);
            return;
        }

        page.SetActive(isOn);
    }

    /// <summary>
    /// 모든 앱 페이지를 닫고 공통 반투명 배경을 끕니다.
    /// </summary>
    public void CloseAllApps()
    {
        _appSwitchVersion++;
        _appSwitchPending = false;
        _requestedAppIndex = -1;
        if (_appPages != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _appPages.Length; i++)
            {
                if (_appPages[i] != null)
                {
                    int index = i; // 인덱스 값입니다.
                    var fade = _appPages[i].GetComponent<AppFadeView>(); // 페이드 값입니다.
                    if (fade != null)
                    {
                        fade.ForceHide(() => SetAppPage(index, false));
                    }
                    else
                    {
                        SetAppPage(i, false);
                    }
                }
            }
        }

        _currentAppIndex = -1;
        if (_smartPhoneBackground != null)
        {
            _smartPhoneBackground.enabled = false;
        }
    }

    /// <summary>
    /// 공통 베이스 패널을 표시하거나 숨깁니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetBasePanel(bool isOn)
    {
        _basePanel.SetActive(isOn);
    }

    /// <summary>
    /// 대화창 바깥의 UI에서 클릭과 선택을 차단합니다.
    /// </summary>
    /// <param name="branch">분기 값입니다.</param>
    /// <param name="talkRoot">대화 루트 값입니다.</param>
    void BlockOtherTalkUiInput(Transform branch, Transform talkRoot)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (branch == talkRoot)
        {
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (talkRoot.IsChildOf(branch))
        {
            // i: 반복 값입니다.
            for (int i = 0; i < branch.childCount; i++)
            {
                BlockOtherTalkUiInput(branch.GetChild(i), talkRoot);
            }

            return;
        }

        var group = branch.GetComponent<CanvasGroup>(); // 그룹 값입니다.
        if (group == null)
        {
            group = branch.gameObject.AddComponent<CanvasGroup>();
        }

        foreach (var childGroup in branch.GetComponentsInChildren<CanvasGroup>(true))
        {
            if (!_talkUiStates.ContainsKey(childGroup))
            {
                _talkUiStates.Add(childGroup, new TalkUiState
                {
                    Alpha = childGroup.alpha,
                    Interactable = childGroup.interactable,
                    BlocksRaycasts = childGroup.blocksRaycasts
                });
            }

            if (!_talkModeOwner.KeepsBackgroundVisible) childGroup.alpha = 0f;
            childGroup.interactable = false;
            childGroup.blocksRaycasts = false;
        }
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 선택한 앱의 열기·닫기를 연출과 함께 전환합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    void ToggleAnimatedApp(int index)
    {
        var target = _appPages[index].GetComponent<AppFadeView>(); // 대상 값입니다.
        bool open = target != null ? target.IsShown : _appPages[index].activeInHierarchy; // 열기 여부입니다.
        int desired = open || (_appSwitchPending && _requestedAppIndex == index) ? -1 : index; // 목표 값입니다.
        int version = ++_appSwitchVersion; // 회차 값입니다.
        _requestedAppIndex = desired;
        _appSwitchPending = true;
        var active = new List<int>(); // 활성 값입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < _appPages.Length; i++)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (_appPages[i] == null)
            {
                continue;
            }

            var fade = _appPages[i].GetComponent<AppFadeView>(); // 페이드 값입니다.
            if (fade != null ? fade.IsDisplaying : _appPages[i].activeInHierarchy)
            {
                active.Add(i);
            }
        }

        int remaining = active.Count; // 남은 값입니다.
        Action showNext = () =>
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (version != _appSwitchVersion || !isActiveAndEnabled)
            {
                return;
            }

            _appSwitchPending = false;
            if (desired >= 0)
            {
                if (_smartPhonePanel != null)
                {
                    _smartPhonePanel.SetActive(true);
                }

                SetAppPage(desired, true);
            }

            UpdateAppBackground();
        }; // 표시 다음 값입니다.
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (remaining == 0)
        {
            showNext();
            return;
        }

        // item: 아이템 값입니다.
        foreach (int item in active)
        {
            int closing = item; // 닫는 중 값입니다.
            var fade = _appPages[item].GetComponent<AppFadeView>(); // 페이드 값입니다.
            Action closed = () =>
            {
                if (--remaining == 0)
                {
                    showNext();
                }
            }; // 닫힘 값입니다.
            if (fade != null)
            {
                fade.TrySetVisible(false, () => SetAppPage(closing, false), closed);
            }
            else
            {
                SetAppPage(item, false);
                closed();
            }
        }
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// Canvas의 아래쪽 경계와 자식 UI의 크기를 고려해 목록 전체가 화면 밖에 놓일 위치를 구합니다.
    /// </summary>
    Vector2 CalculateAppGroupHiddenPosition()
    {
        var canvas = _appGroupRect.GetComponentInParent<Canvas>(); // 캔버스 값입니다.
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null; // 캔버스 사각 영역 값입니다.
        var parentRect = _appGroupRect.parent as RectTransform; // 부모 사각 영역 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (canvasRect == null || parentRect == null)
        {
            return _appGroupShownPosition + Vector2.down * (_appGroupRect.rect.height + _appGroupHiddenMargin);
        }

        // 초기 비활성 자식과 프레임까지 포함해 실제 전체 영역의 가장 높은 점을 계산합니다.
        float top = float.NegativeInfinity;
        foreach (var rect in _appGroupRect.GetComponentsInChildren<RectTransform>(true))
        {
            rect.GetWorldCorners(_phoneCorners);
            for (int i = 0; i < _phoneCorners.Length; i++)
                top = Mathf.Max(top, canvasRect.InverseTransformPoint(_phoneCorners[i]).y);
        }
        float offsetY = canvasRect.rect.yMin - _appGroupHiddenMargin - top;
        Vector3 parentOffset = parentRect.InverseTransformVector(canvasRect.TransformVector(Vector3.up * offsetY));
        return _appGroupRect.anchoredPosition + (Vector2)parentOffset;
    }

    #endregion

    #region 시계 표시 및 시간 연결

    /// <summary>
    /// 반원 채움 방식을 준비하고 현재 시간대의 시작 위치를 표시합니다.
    /// </summary>
    void InitializeClockPresentation()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!ConfigureClockFill())
        {
            return;
        }

        _clockTimeSource = GameManager.Instance;
        if (_clockTimeSource != null)
        {
            _clockTimeSource.DayPhaseChanged += HandleClockPhaseChanged;
        }

        // 실제 파트 진행률이 전달되기 전에는 임의 타이머를 실행하지 않습니다.
        if (!_clockHasProgress)
        {
            ApplyClockProgress(_clockTimeSource != null ? _clockTimeSource.CurrentDayPhase : DayPhase.Morning, 0f);
        }
    }

    /// <summary>
    /// 기존 시간 이미지 참조를 사용해 하단 중앙에서 시작하는 반원 채움을 설정합니다.
    /// </summary>
    bool ConfigureClockFill()
    {
        if (_currnetTimeImage == null)
        {
            var clock = transform.Find("DailyInfo/Background/ClockFill"); // 시계 값입니다.
            if (clock != null)
            {
                _currnetTimeImage = clock.GetComponent<Image>();
            }
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_currnetTimeImage == null)
        {
            return false;
        }

        _currnetTimeImage.type = Image.Type.Filled;
        _currnetTimeImage.fillMethod = Image.FillMethod.Radial180;
        _currnetTimeImage.fillOrigin = (int)Image.Origin180.Bottom;
        _currnetTimeImage.fillClockwise = true;
        if (_clockPhaseImages == null || _clockPhaseImages.Length != 3)
        {
            Array.Resize(ref _clockPhaseImages, 3);
        }

        string[] names =
        { "ClockBlue", "ClockOrange", "ClockPurple" }; // 이름 목록입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < names.Length; i++)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (_clockPhaseImages[i] != null)
            {
                continue;
            }

            var part = _currnetTimeImage.transform.Find(names[i]); // 구간 값입니다.
            if (part != null)
            {
                _clockPhaseImages[i] = part.GetComponent<Image>();
            }
        }

        return true;
    }

    /// <summary>
    /// 담당 로직이 전달한 파트 내부 진행률로 시계를 갱신합니다. 이전 파트는 채워진 상태로 남습니다.
    /// </summary>
    /// <param name="phase">시간대 값입니다.</param>
    /// <param name="progress">표시할 진행 비율입니다. 0은 시작, 1은 완료입니다.</param>
    public void UpdateClockProgress(DayPhase phase, float progress)
    {
        StopClockTest(false); // 실제 데이터가 오면 테스트 표시를 덮어쓰고 실제 표시로 복귀합니다.
        ApplyClockProgress(phase, progress);
    }

    /// <summary>
    /// 담당 로직이 계산한 경과 시간과 파트 전체 시간을 진행률로 변환해 표시합니다.
    /// </summary>
    /// <param name="phase">시간대 값입니다.</param>
    /// <param name="elapsedTime">경과 시간 값입니다.</param>
    /// <param name="duration">지속 시간 값입니다.</param>
    public void UpdateClockElapsedTime(DayPhase phase, float elapsedTime, float duration)
    {
        float progress = duration > 0f && !float.IsNaN(duration) && !float.IsInfinity(duration) ? elapsedTime / duration : 0f; // 진행도 값입니다.
        UpdateClockProgress(phase, progress);
    }

    /// <summary>
    /// 시간대 영역은 고정 표시하고 하단 중앙 바늘로 현재 구간과 영업 진행률을 표시합니다.
    /// </summary>
    /// <param name="phase">시간대 값입니다.</param>
    /// <param name="progress">표시할 진행 비율입니다. 0은 시작, 1은 완료입니다.</param>
    void ApplyClockProgress(DayPhase phase, float progress)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!ConfigureClockFill())
        {
            return;
        }

        _clockPhase = phase;
        _clockPartProgress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
        _clockHasProgress = true;
        bool evening = phase == DayPhase.Night;
        int currentPart = phase == DayPhase.Morning ? 0 : phase == DayPhase.Afternoon ? 1 : 2;
        _currnetTimeImage.fillAmount = 1f;
        _currnetTimeImage.color = Color.white;
        for (int i = 0; i < _clockPhaseImages.Length; i++)
        {
            var part = _clockPhaseImages[i];
            if (part == null) continue;
            part.gameObject.SetActive(false);
        }
        float morningEnd = Mathf.Clamp01(_morningClockEnd);
        float afternoonEnd = Mathf.Clamp(_afternoonClockEnd, morningEnd, 1f);
        float position = phase == DayPhase.Morning ? morningEnd * .5f
            : phase == DayPhase.Afternoon ? (morningEnd + afternoonEnd) * .5f
            : Mathf.Lerp(afternoonEnd, 1f, (_clockBusinessRunning || _clockTestActive) ? _clockPartProgress : .5f);
        PresentClockNeedle(90f - 180f * position);
    }

    /// <summary>영업 담당이 전달한 실제 진행률과 시작 상태로 저녁 바늘을 표시합니다.</summary>
    public void UpdateBusinessClockProgress(float progress, bool running)
    {
        _clockBusinessRunning = running;
        UpdateClockProgress(DayPhase.Night, progress);
    }

    void PresentClockNeedle(float angle)
    {
        if (_clockNeedle == null && _currnetTimeImage != null)
            _clockNeedle = _currnetTimeImage.transform.Find("ClockNeedle") as RectTransform;
        if (_clockNeedle == null) return;
        _clockNeedleTargetAngle = angle;
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            _clockNeedle.localRotation = Quaternion.Euler(0f, 0f, angle);
            return;
        }
        if (!_clockNeedlePresented)
        {
            _clockNeedlePresented = true;
            _clockNeedle.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // 매 프레임 갱신되는 목표를 읽어 등장 중에도 실제 영업 진행률을 반영합니다.
            _clockNeedleEntrance = DOVirtual.Float(0f, 1f, _clockNeedleEntranceDuration, value =>
            {
                if (_clockNeedle != null)
                    _clockNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(90f, _clockNeedleTargetAngle, value));
            }).SetEase(Ease.InOutElastic).SetUpdate(true).OnComplete(() =>
            {
                _clockNeedleEntrance = null;
                if (_clockNeedle != null) _clockNeedle.localRotation = Quaternion.Euler(0f, 0f, _clockNeedleTargetAngle);
            });
        }
        else if (_clockNeedleEntrance == null)
            _clockNeedle.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    void StopClockNeedleEntrance()
    {
        _clockNeedleEntrance?.Kill(false);
        _clockNeedleEntrance = null;
        _clockNeedlePresented = false;
    }

    /// <summary>
    /// 시간대가 바뀌면 새 파트의 시작 경계로 이동합니다. 게임 시간은 변경하지 않습니다.
    /// </summary>
    /// <param name="phase">시간대 값입니다.</param>
    void HandleClockPhaseChanged(DayPhase phase)
    {
        _clockBusinessRunning = false;
        UpdateClockProgress(phase, 0f);
    }

    /// <summary>
    /// 시계 테스트와 이 View의 시간대 이벤트 구독만 정리합니다.
    /// </summary>
    void ReleaseClockPresentation()
    {
        StopClockNeedleEntrance();
        StopClockTest(false);
        if (_clockTimeSource != null)
        {
            _clockTimeSource.DayPhaseChanged -= HandleClockPhaseChanged;
        }

        _clockTimeSource = null;
    }

    /// <summary>
    /// 선택 시점의 파트를 고정하고 실제 게임 시간과 무관한 30초 표시 타이머를 진행합니다.
    /// </summary>
    /// <param name="phase">시간대 값입니다.</param>
    System.Collections.IEnumerator PlaySelectedClockPart(DayPhase phase)
    {
        const float duration = 30f; // 지속 시간 값입니다.
        float elapsed = 0f; // 경과 값입니다.
        ApplyClockProgress(phase, 0f);
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
            ApplyClockProgress(phase, elapsed / duration);
        }

        ApplyClockProgress(phase, 1f);
        _clockTestRoutine = null;
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// 플레이 모드에서만 시계 테스트를 준비하고 기존 표시를 보관합니다.
    /// </summary>
    bool BeginClockTest()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying || !isActiveAndEnabled || !ConfigureClockFill())
        {
            return false;
        }

        if (_clockTestRoutine != null)
        {
            StopCoroutine(_clockTestRoutine);
        }

        _clockTestRoutine = null;
        if (!_clockTestActive)
        {
            _clockTestPreviousFill = _currnetTimeImage.fillAmount;
            _clockTestPreviousProgress = _clockPartProgress;
            _clockTestPreviousPhase = _clockPhase;
            _clockTestPreviousHasProgress = _clockHasProgress;
            _clockTestPreviousColor = _currnetTimeImage.color;
            _clockTestPreviousPartActive = new bool[_clockPhaseImages.Length];
            _clockTestPreviousPartColors = new Color[_clockPhaseImages.Length];
            // i: 반복 값입니다.
            for (int i = 0; i < _clockPhaseImages.Length; i++)
            {
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (_clockPhaseImages[i] == null)
                {
                    continue;
                }

                _clockTestPreviousPartActive[i] = _clockPhaseImages[i].gameObject.activeSelf;
                _clockTestPreviousPartColors[i] = _clockPhaseImages[i].color;
            }
        }

        _clockTestActive = true;
        return true;
    }

    /// <summary>
    /// 오전 파트를 선택하고 30초 동안 처음부터 끝까지 재생합니다.
    /// </summary>
    [ContextMenu("시계 테스트/오전 30초 시작 (플레이 모드)")]
    public void TestClockMorning()
    {
        _clockTestPart = ClockTestPart.Morning;
        TestSelectedClockPart();
    }

    /// <summary>
    /// 오후 파트를 선택하고 30초 동안 처음부터 끝까지 재생합니다.
    /// </summary>
    [ContextMenu("시계 테스트/오후 30초 시작 (플레이 모드)")]
    public void TestClockAfternoon()
    {
        _clockTestPart = ClockTestPart.Afternoon;
        TestSelectedClockPart();
    }

    /// <summary>
    /// 저녁 파트를 선택하고 30초 동안 처음부터 끝까지 재생합니다.
    /// </summary>
    [ContextMenu("시계 테스트/저녁 30초 시작 (플레이 모드)")]
    public void TestClockNight()
    {
        _clockTestPart = ClockTestPart.Evening;
        TestSelectedClockPart();
    }

    /// <summary>
    /// 인스펙터에서 고른 파트를 0%부터 100%까지 30초 동안 표시합니다.
    /// </summary>
    [ContextMenu("시계 테스트/선택 파트 30초 시작 (플레이 모드)")]
    public void TestSelectedClockPart()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!BeginClockTest())
        {
            return;
        }

        DayPhase phase = _clockTestPart == ClockTestPart.Morning ? DayPhase.Morning : _clockTestPart == ClockTestPart.Afternoon ? DayPhase.Afternoon : DayPhase.Night; // 시간대 값입니다.
        _clockTestRoutine = PlaySelectedClockPart(phase);
        StartCoroutine(_clockTestRoutine);
    }

    /// <summary>
    /// 실제 게임 상태를 바꾸지 않고 오전부터 저녁까지 채워지는 연출을 확인합니다.
    /// </summary>
    [ContextMenu("시계 테스트/전체 시간 흐름 재생 (플레이 모드)")]
    public void TestClockFlow()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!BeginClockTest())
        {
            return;
        }

        _clockTestRoutine = PlayClockTest();
        StartCoroutine(_clockTestRoutine);
    }

    /// <summary>
    /// 시간 배율과 무관하게 각 파트의 표시 진행률만 순서대로 갱신합니다.
    /// </summary>
    System.Collections.IEnumerator PlayClockTest()
    {
        float seconds = float.IsNaN(_clockTestSecondsPerPart) || float.IsInfinity(_clockTestSecondsPerPart) ? 30f : Mathf.Max(0.1f, _clockTestSecondsPerPart); // 초 값입니다.
        // phase: 시간대 값입니다.
        foreach (var phase in new[]
        {
            DayPhase.Morning,
            DayPhase.Afternoon,
            DayPhase.Night
        }

        )
        {
            float elapsed = 0f; // 경과 값입니다.
            ApplyClockProgress(phase, 0f);
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                ApplyClockProgress(phase, elapsed / seconds);
            }

            ApplyClockProgress(phase, 1f);
        }

        _clockTestRoutine = null;
    }

    /// <summary>
    /// 테스트 연출을 멈추고 시작 전의 시계 표시를 복원합니다.
    /// </summary>
    [ContextMenu("시계 테스트/종료 및 원래 표시 복원")]
    public void RestoreClockAfterTest()
    {
        StopClockTest(true);
    }

    /// <summary>
    /// 이 View의 테스트만 종료하며 필요할 때 보관한 표시 상태를 복원합니다.
    /// </summary>
    /// <param name="restore">복원 여부입니다.</param>
    void StopClockTest(bool restore)
    {
        if (_clockTestRoutine != null)
        {
            StopCoroutine(_clockTestRoutine);
        }

        _clockTestRoutine = null;
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_clockTestActive)
        {
            return;
        }

        if (restore)
        {
            if (_currnetTimeImage != null)
            {
                _currnetTimeImage.fillAmount = _clockTestPreviousFill;
                _currnetTimeImage.color = _clockTestPreviousColor;
            }

            // i: 반복 값입니다.
            for (int i = 0; _clockPhaseImages != null && _clockTestPreviousPartActive != null && i < _clockPhaseImages.Length && i < _clockTestPreviousPartActive.Length; i++)
            {
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (_clockPhaseImages[i] == null)
                {
                    continue;
                }

                _clockPhaseImages[i].color = _clockTestPreviousPartColors[i];
                _clockPhaseImages[i].gameObject.SetActive(_clockTestPreviousPartActive[i]);
            }

            _clockPhase = _clockTestPreviousPhase;
            _clockPartProgress = _clockTestPreviousProgress;
            _clockHasProgress = _clockTestPreviousHasProgress;
        }

        _clockTestActive = false;
        if (restore && isActiveAndEnabled && _clockHasProgress) ApplyClockProgress(_clockPhase, _clockPartProgress);
    }

    #endregion

}
