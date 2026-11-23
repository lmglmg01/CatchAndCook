using UnityEngine;
using TMPro;

/// <summary>
/// 초밥집의 영업 준비 화면과 각 관리 화면을 연결하는 씬 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 모델 처리는 Presenter에 요청합니다.
/// </summary>
public class IjakayaView : MonoBehaviour
{
    [Header("실제 직원 표시")]
    [SerializeField] Sprite _servingPortrait;
    [SerializeField] Sprite _cookingPortrait;
    [Header("직원 머리 위 아이콘")]
    // 서빙 직원의 머리 위에 표시할 전용 아이콘입니다.
    [SerializeField] Sprite _servingLevelIcon;
    // 요리사의 머리 위에 표시할 전용 아이콘입니다.
    [SerializeField] Sprite _cookingLevelIcon;
    [SerializeField, TextArea] string _servingDescription = "완성된 요리와 음료를 손님에게 제공합니다.";
    [SerializeField, TextArea] string _cookingDescription = "접수된 음식 주문을 조리합니다.";
    [Header("영업 중 월드 UI")]
    [SerializeField] NPCOrderView _guestOrderPrefab;
    [SerializeField] InteractToGuestView _carrierDisplayPrefab;
    [SerializeField] NPCCookingView _cookingDisplayPrefab;
    [SerializeField] SushiChairView _chairCleaningPrefab;
    public SushiChairView ChairCleaningPrefab => _chairCleaningPrefab;
    [SerializeField] SushiStaffLevelView _staffLevelPrefab;
    public SushiStaffLevelView StaffLevelPrefab => _staffLevelPrefab;
    /// <summary>영업 시작 요청에서는 접기 버튼을 포함한 메모 전체를 숨기며 다시 표시할 때 펼침 상태를 복원합니다.</summary>
    public void SetMissionMemoExpanded(bool expanded)
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var memo in root.GetComponentsInChildren<QuestMemoView>(true))
            {
                // 기존 Presenter의 영업 확정 호출을 그대로 사용하며 영업 로직은 변경하지 않습니다.
                memo.gameObject.SetActive(expanded);
                if (expanded) memo.SetExpanded(true);
            }
    }

    [Header("접시 투척 임시 패널티 문구")]
    [SerializeField] string _guestComplaintMessage = "운영비 +10 (임시)";
    [SerializeField] string _guestPlateThrownMessage = "시간 -5초 (임시)";
    SushiPenaltyView _guestPenaltyView;

    /// <summary>접시 투척 시 표시용 임시 문구를 전달합니다. 실제 차감량을 계산하거나 변경하지 않습니다.</summary>
    public void ShowGuestComplaint() => ShowGuestPenalty(_guestComplaintMessage, _guestPlateThrownMessage);

    /// <summary>담당자가 전달한 실제 운영비·시간 변경 문구를 같은 씬의 기존 패널티 표시 View에 전달합니다.</summary>
    public void ShowGuestPenalty(string operationCostMessage, string timeMessage)
    {
        if (!isActiveAndEnabled) return;
        if (_guestPenaltyView == null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                _guestPenaltyView = root.GetComponentInChildren<SushiPenaltyView>(true);
                if (_guestPenaltyView != null) break;
            }
        }
        if (_guestPenaltyView == null) return;
        // 운영비 증가는 보유 Gold 즉시 차감과 다르므로 전달받은 문구를 그대로 사용합니다.
        _guestPenaltyView.ShowOperationCostPenalty(operationCostMessage);
        _guestPenaltyView.ShowTimePenalty(timeMessage);
    }
    BaseView _businessClockView;
    /// <summary>같은 씬의 공통 시계에 저녁 구간 진행률을 전달합니다.</summary>
    public void SetBusinessClockProgress(float progress, bool running = true)
    {
        if (_businessClockView == null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                _businessClockView = root.GetComponentInChildren<BaseView>(true);
                if (_businessClockView != null) break;
            }
        }
        if (_businessClockView != null) _businessClockView.UpdateBusinessClockProgress(progress, running);
    }

    public NPCCookingView CookingDisplayPrefab => _cookingDisplayPrefab;
    public NPCOrderView GuestOrderPrefab => _guestOrderPrefab;
    public InteractToGuestView CarrierDisplayPrefab => _carrierDisplayPrefab;
    public Sprite StaffPortrait(StaffType role) => role == StaffType.Serving ? _servingPortrait : _cookingPortrait;
    /// <summary>직원 종류에 맞는 머리 위 레벨 아이콘을 반환합니다.</summary>
    public Sprite StaffLevelIcon(StaffType type)
    {
        // 서빙 직원에게는 서빙 전용 아이콘을 반환합니다.
        if (type == StaffType.Serving)
        {
            return _servingLevelIcon;
        }

        // 요리사에게는 요리 전용 아이콘을 반환합니다.
        return _cookingLevelIcon;
    }
    public string StaffDescription(StaffType role) => role == StaffType.Serving ? _servingDescription : _cookingDescription;


    #region 멤버

    #region UI 연결 및 표시 설정

    // 오늘의 메뉴 등록 화면을 연결합니다.

    // 등록 가능한 요리 목록과 상세 화면을 연결합니다.
    [SerializeField] GameObject _recipeRegistrationPage;
    // 등록 메뉴의 교체·수량 변경·삭제 팝업을 연결합니다.
    [SerializeField] GameObject _registeredMenuActionsPage;
    // 메뉴 등록 및 수량 변경 팝업을 연결합니다.
    [SerializeField] GameObject _menuQuantityPage;
    // 요리 강화 확인 팝업을 연결합니다.
    [SerializeField] GameObject _recipeUpgradePage;
    // 요리 강화 완료 팝업을 연결합니다.
    [SerializeField] GameObject _recipeUpgradeResultPage;
    [SerializeField] GameObject _staffUpgradeResultPage;
    [SerializeField] SushiRecipeUpgradeResultView _staffUpgradeResultView;
    // 직원 관리 화면을 연결합니다.
    [SerializeField] GameObject _staffManagementPage;
    // 영업 정산 화면을 연결합니다.
    [SerializeField] GameObject _businessResultPage;
    // 영업 시작 안내 전체 영역을 연결합니다.
    [SerializeField] GameObject _businessStartGuide;
    // 영업 시작 안내 문구를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _businessStartGuideText;
    [SerializeField] TMP_Text _businessWarningText; // 영업 준비가 부족할 때 표시할 경고입니다.
    // 영업 시작 단축키 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _businessStartKeyImage;
    // 단축키 아이콘 완성 전 키 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _businessStartKeyText;
    // 영업 시작 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _startBusinessButton;
    // 오늘의 메뉴 화면을 여는 메뉴 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _openTodayMenuButton;
    // 직원 관리 화면을 여는 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _openStaffButton;
    // 직원의 해금·미해금 상세 영역 표시를 바꿀 StaffManagement의 SushiStaffView를 연결합니다.
    [SerializeField] SushiStaffView _staffView;
    // 직원 강화 확인 팝업 Panel의 View를 연결합니다.
    [SerializeField] SushiStaffUpgradeView _staffUpgradeView;
    // 수량 팝업 Panel의 View를 연결합니다.
    [SerializeField] SushiMenuQuantityView _menuQuantityView;
    // 등록 가능 요리 목록 영역의 View를 연결합니다.
    [SerializeField] SushiRecipeListView _recipeListView;
    // 요리 상세 영역의 View를 연결합니다.
    [SerializeField] SushiRecipeDetailView _recipeDetailView;
    // 요리 강화 확인 Panel의 View를 연결합니다.
    [SerializeField] SushiRecipeUpgradeView _recipeUpgradeView;
    // 요리 강화 결과 Panel의 View를 연결합니다.
    [SerializeField] SushiRecipeUpgradeResultView _recipeUpgradeResultView;
    // 영업 정산 화면의 View를 연결합니다.
    [SerializeField] SushiBusinessResultView _businessResultView;
    // 직원 강화 팝업의 반투명 배경까지 포함한 루트를 연결합니다.
    [SerializeField] GameObject _staffUpgradePage;
    [SerializeField] UnityEngine.UI.Image[] _basicMenuSummaryIcons; // 기본 음식과 음료의 표시 순서입니다.
    [SerializeField] TMP_Text[] _basicMenuSummaryNames; // 같은 순서의 이름 라벨입니다.
    UnityEngine.UI.ScrollRect _recipeScroll;
    SushiRecipeSlotView _recipeSlotTemplate;
    readonly System.Collections.Generic.List<SushiRecipeSlotView> _additionalRecipeSlots = new();
    int _initialRecipeSlotCount;
    int _menuSlotCapacity;
    [SerializeField] Sprite _businessStartNormalSprite, _businessStartHoverSprite;
    SushiBusinessStartButtonView _businessStartAppearance;
    string _defaultBusinessGuide;
    Color _defaultBusinessGuideColor;
    // 기본 메뉴 영역의 음료 아이콘 두 개를 순서대로 연결합니다.

    // 기본 메뉴 영역의 음료 이름 두 개를 같은 순서로 연결합니다.


    #endregion

    #region 런타임 상태

    int _selectedMenuSlotIndex = -1; // 선택 메뉴 슬롯 인덱스 값입니다.
    int _selectedRecipeId = -1; // 선택 요리 식별자 값입니다.
    int _selectedStaffId; // 선택 직원 식별자 값입니다.
    readonly System.Collections.Generic.List<CanvasGroup> _commonHudGroups = new System.Collections.Generic.List<CanvasGroup>(); // 공통 HUD 그룹 목록입니다.
    readonly System.Collections.Generic.Dictionary<CanvasGroup, Vector3> _hiddenHudStates = new System.Collections.Generic.Dictionary<CanvasGroup, Vector3>(); // 숨김 HUD 상태 목록에 사용하는 대상별 매핑입니다.

    #endregion

    #region 이벤트 액션

    /// <summary>
    /// 메뉴 또는 직원 관리 화면을 열기 직전에 표시 데이터 갱신을 요청합니다.
    /// </summary>
    public event System.Action OnManagementOpened;

    // 영업 시작 버튼의 요청을 Presenter에 전달합니다.
    public event System.Action OnBusinessStartRequested;

    /// <summary>
    /// 슬롯 위치와 관리 메뉴 선택 여부를 전달합니다.
    /// </summary>
    public event System.Action<int, bool> OnMenuSlotSelected;

    /// <summary>
    /// 선택한 요리 ID를 전달합니다. 담당 로직에서 상세·수량 데이터를 갱신합니다.
    /// </summary>
    public event System.Action<int> OnRecipeSelected;

    /// <summary>
    /// 교체를 시작할 메뉴 슬롯 위치를 전달합니다.
    /// </summary>
    public event System.Action<int> OnMenuReplaceRequested;

    /// <summary>
    /// 수량을 변경할 메뉴 슬롯 위치를 전달합니다.
    /// </summary>
    public event System.Action<int> OnMenuQuantityEditRequested;

    /// <summary>
    /// 삭제를 요청할 메뉴 슬롯 위치를 전달합니다. View는 실제 데이터를 삭제하지 않습니다.
    /// </summary>
    public event System.Action<int> OnMenuDeleteRequested;

    /// <summary>
    /// 메뉴 슬롯 위치·요리 ID·입력 수량을 전달합니다. 검증 성공 후 팝업을 닫아 주세요.
    /// </summary>
    public event System.Action<int, int, int> OnMenuQuantityConfirmed;

    /// <summary>
    /// 강화 전후 표시를 준비할 요리 ID를 전달합니다.
    /// </summary>
    public event System.Action<int> OnRecipeUpgradePreviewRequested;

    /// <summary>
    /// 요리 강화 실행 요청입니다. 실제 성공 시 결과 View를 갱신하고 표시합니다.
    /// </summary>
    public event System.Action<int> OnRecipeUpgradeRequested;

    /// <summary>
    /// 선택한 직원 ID를 전달합니다.
    /// </summary>
    public event System.Action<int> OnStaffSelected;

    /// <summary>
    /// 강화 비교 정보를 준비할 직원 ID를 전달합니다.
    /// </summary>
    public event System.Action<int> OnStaffUpgradePreviewRequested;

    /// <summary>
    /// 직원 강화 실행 요청입니다. 실제 성공 여부는 담당 로직에서 처리합니다.
    /// </summary>
    public event System.Action<int> OnStaffUpgradeRequested;

    /// <summary>
    /// 정산 확인 요청입니다. 다음 씬 이동은 담당 로직에서 처리합니다.
    /// </summary>
    public event System.Action OnSettlementConfirmed;

    #endregion

    #region UI 연결 및 표시 설정

    // TodayMenu/TodayMenuPanel의 대표 메뉴 View를 연결합니다. 메뉴 파트의 세부 분배는 이 View가 담당합니다.
    [SerializeField] SushiTodayMenuView _menuView;
    [SerializeField] TMP_Text _goldHudText;
    // 테스트 후 복원할 BusinessPreparation 화면을 연결합니다. 기존 인스펙터 연결을 유지합니다.
    [SerializeField] GameObject _businessPreparationPage;

    #endregion

    #region 프로퍼티

    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiTodayMenuView MenuView => _menuView;
    public SushiStaffView StaffView => _staffView;
    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiStaffUpgradeView StaffUpgradeView => _staffUpgradeView;
    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiMenuQuantityView QuantityView => _menuQuantityView;
    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiRecipeListView RecipeListView => _recipeListView;
    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiRecipeDetailView RecipeDetailView => _recipeDetailView;
    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiRecipeUpgradeView RecipeUpgradeView => _recipeUpgradeView;
    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiRecipeUpgradeResultView RecipeUpgradeResultView => _recipeUpgradeResultView;
    // 담당 로직에서 데이터를 전달할 표시 View입니다. 이 속성은 게임 상태를 변경하지 않습니다.
    public SushiBusinessResultView BusinessResultView => _businessResultView;

    /// <summary>
    /// 외부에서 현재 선택한 메뉴 슬롯을 읽습니다. 실제 메뉴 데이터는 변경하지 않습니다.
    /// </summary>
    public bool IsMenuBookTest => GetComponentInChildren<SushiMenuBookView>(true)?.IsTesting == true;
    public int MenuSlotCapacity => _menuSlotCapacity;
    public int SelectedMenuSlotIndex => _selectedMenuSlotIndex;

    /// <summary>
    /// 외부에서 현재 선택한 요리 식별값을 읽습니다.
    /// </summary>
    public int SelectedRecipeId => _selectedRecipeId;

    /// <summary>
    /// 외부에서 현재 선택한 직원 식별값을 읽습니다.
    /// </summary>
    public int SelectedStaffId => _selectedStaffId;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        if (_businessStartGuideText != null)
        {
            _defaultBusinessGuide = _businessStartGuideText.text;
            _defaultBusinessGuideColor = _businessStartGuideText.color;
        }
        _menuSlotCapacity = _menuView != null ? _menuView.GetComponentsInChildren<SushiTodayMenuSlotView>(true).Length : 0;
        _recipeScroll = _recipeListView?.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        if (_recipeScroll != null && _recipeScroll.content != null)
        {
            var slots = _recipeScroll.content.GetComponentsInChildren<SushiRecipeSlotView>(true);
            _initialRecipeSlotCount = slots.Length;
            if (slots.Length > 0) _recipeSlotTemplate = slots[0];
        }
        InitializeButtons();
        GetComponentInChildren<SushiMenuBookView>(true)?.SyncBackdropVisibility();
        if(_startBusinessButton!=null && _businessStartGuide!=null)
        {
            _businessStartAppearance=_startBusinessButton.GetComponent<SushiBusinessStartButtonView>()??_startBusinessButton.gameObject.AddComponent<SushiBusinessStartButtonView>();
            _businessStartAppearance.Configure(_startBusinessButton,transform,_businessStartGuide,_businessStartNormalSprite,_businessStartHoverSprite);
        }
    }

    /// <summary>초밥집 View가 켜질 때 같은 씬의 시간스킵을 표시합니다.</summary>
    void OnEnable() => RestoreTimeSkipUi();

    /// <summary>다른 HUD의 초기화가 끝난 뒤에도 시간스킵 표시를 복원합니다.</summary>
    void Start() => RestoreTimeSkipUi();

    /// <summary>씬 이름 대신 현재 초밥집 View와 같은 씬의 BaseView를 찾아 표시 함수를 호출합니다.</summary>
    void RestoreTimeSkipUi()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var view in root.GetComponentsInChildren<BaseView>(true))
                view.SetTimeSkipVisible(true);
    }

    /// <summary>
    /// 관리창 표시 상태에 맞춰 공통 HUD를 숨기거나 복원합니다.
    /// </summary>
    void LateUpdate()
    {
        bool visible = false; // 노출 여부입니다.
        // page: 페이지 값입니다.
        foreach (var page in new[]
        {

            _recipeRegistrationPage,
            _registeredMenuActionsPage,
            _menuQuantityPage,
            _recipeUpgradePage,
            _recipeUpgradeResultPage,
            _staffUpgradeResultPage,
            _staffManagementPage,
            _staffUpgradePage,
            _businessResultPage
        }

        )
        {
            visible |= page != null && page.activeInHierarchy;
        }

        if(_businessStartAppearance!=null)
        {
            bool blocked=false;
            Transform modalLayer=null;
            foreach(var popup in new[]{_registeredMenuActionsPage,_menuQuantityPage,_recipeUpgradePage,_recipeUpgradeResultPage,_staffUpgradeResultPage,_staffUpgradePage,_businessResultPage})
            {
                if(popup==null||!popup.activeInHierarchy)continue;
                blocked=true;
                var layer=popup.transform;
                while(layer.parent!=null&&layer.parent!=transform)layer=layer.parent;
                if(layer.parent==transform&&(modalLayer==null||layer.GetSiblingIndex()<modalLayer.GetSiblingIndex()))modalLayer=layer;
            }
            var book=transform.Find("MenuBook");
            _businessStartAppearance.Refresh(_businessStartGuide!=null&&_businessStartGuide.activeInHierarchy,book!=null&&book.gameObject.activeInHierarchy,blocked,modalLayer);
        }
        if (visible)
        {
            HideCommonHud();
        }
        else
        {
            RestoreCommonHud();
        }
    }

    /// <summary>
    /// 이 Canvas가 꺼질 때 자신이 숨긴 공통 HUD만 복원합니다.
    /// </summary>
    void OnDisable()
    {
        if (_guestPenaltyView != null) _guestPenaltyView.ClearNotifications();
        RestoreCommonHud();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_startBusinessButton != null)
        {
            _startBusinessButton.onClick.RemoveListener(StartBusinessPreview);
        }

        if (_openTodayMenuButton != null)
        {
            _openTodayMenuButton.onClick.RemoveListener(OpenTodayMenu);
        }

        if (_openStaffButton != null)
        {
            _openStaffButton.onClick.RemoveListener(OpenStaff);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_startBusinessButton != null)
        {
            _startBusinessButton.onClick.RemoveListener(StartBusinessPreview);
            _startBusinessButton.onClick.AddListener(StartBusinessPreview);
        }

        if (_openTodayMenuButton != null)
        {
            _openTodayMenuButton.onClick.RemoveListener(OpenTodayMenu);
            _openTodayMenuButton.onClick.AddListener(OpenTodayMenu);
        }

        if (_openStaffButton != null)
        {
            _openStaffButton.onClick.RemoveListener(OpenStaff);
            _openStaffButton.onClick.AddListener(OpenStaff);
        }
    }

    /// <summary>
    /// 연결된 _registeredMenuActionsPage 화면을 켜며, 아래의 기존 화면 상태는 유지합니다.
    /// </summary>
    public void OpenRegisteredMenuActions()
    {
        if (_registeredMenuActionsPage != null)
        {
            GetComponent<SushiRestaurantStyleView>()?.ApplyRegisteredMenuActions();
            _registeredMenuActionsPage.SetActive(true);
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 통합 메뉴 책의 오늘의 메뉴 탭을 엽니다.
    /// </summary>
    public void OpenTodayMenu()
    {
        if (!IsMenuBookTest) ClearMenuSlotSelection();
        OpenRecipeRegistration();
    }

    /// <summary>
    /// 기존 등록 화면 진입 요청을 통합 메뉴 책으로 연결합니다.
    /// </summary>
    public void OpenRecipeRegistration()
    {
        if (!IsMenuBookTest) OnManagementOpened?.Invoke();
        GetComponentInChildren<SushiMenuBookView>(true)?.ShowToday();
    }

    /// <summary>
    /// 연결된 _menuQuantityPage 화면을 켜며, 아래의 기존 화면 상태는 유지합니다.
    /// </summary>
    public void OpenMenuQuantity()
    {
        if (_selectedMenuSlotIndex < 0 && _menuView != null) _selectedMenuSlotIndex = _menuView.FirstEmptySlot();
        GetComponentInChildren<SushiMenuBookView>(true)?.RefreshQuantityInfo();
        if (_menuQuantityPage != null)
        {
            GetComponent<SushiRestaurantStyleView>()?.ApplyPopups();
            _menuQuantityPage.SetActive(true);
        }
    }

    /// <summary>
    /// 연결된 _recipeUpgradePage 화면을 켜며, 아래의 기존 화면 상태는 유지합니다.
    /// </summary>
    public void OpenRecipeUpgrade()
    {
        if (_menuView != null)
        {
            _menuView.DisplaySelectedUpgrade();
        }

        if (!IsMenuBookTest) OnRecipeUpgradePreviewRequested?.Invoke(_selectedRecipeId);
        if (_recipeUpgradePage != null)
        {
            GetComponent<SushiRestaurantStyleView>()?.ApplyPopups();
            _recipeUpgradePage.SetActive(true);
        }
    }

    /// <summary>
    /// 연결된 _recipeUpgradeResultPage 화면을 켜며, 아래의 기존 화면 상태는 유지합니다.
    /// </summary>
    public void OpenRecipeUpgradeResult()
    {
        if (_recipeUpgradeResultPage != null)
        {
            _recipeUpgradeResultPage.SetActive(true);
        }
    }

    /// <summary>
    /// 연결된 _staffManagementPage 화면을 켜며, 아래의 기존 화면 상태는 유지합니다.
    /// </summary>
    public void OpenStaff()
    {
        OnManagementOpened?.Invoke();
        var book = GetComponentInChildren<SushiMenuBookView>(true);
        if (book != null) { book.ShowStaff(); return; }
        if (_staffManagementPage != null)
        {
            _staffManagementPage.SetActive(true);
        }
    }

    /// <summary>
    /// 직원 선택에 따라 준비된 해금 또는 미해금 상세 영역만 전환합니다.
    /// </summary>
    /// <param name="isLocked">잠금 여부입니다.</param>
    public void ShowStaffDetail(bool isLocked)
    {
        if (_staffView != null)
        {
            _staffView.ShowDetail(isLocked);
        }
    }

    /// <summary>
    /// 직원 강화 정보를 요청하고 확인 팝업을 표시합니다.
    /// </summary>
    public void OpenStaffUpgrade()
    {
        if (_staffView != null && _staffView.SelectedStaffId >= 0)
        {
            _selectedStaffId = _staffView.SelectedStaffId;
        }

        if (_staffUpgradeView != null)
        {
            _staffUpgradeView.SetTarget(_selectedStaffId);
        }

        if (_staffView != null)
        {
            _staffView.DisplaySelectedUpgrade();
        }

        OnStaffUpgradePreviewRequested?.Invoke(_selectedStaffId);
        if (_staffUpgradePage != null)
        {
            _staffUpgradePage.SetActive(true);
        }
        GetComponent<SushiRestaurantStyleView>()?.ApplyStaffUpgradePopup();
    }

    /// <summary>
    /// 같은 씬의 공통 HUD와 스마트폰 영역의 기존 상태를 보관하고 숨깁니다.
    /// </summary>
    void HideCommonHud()
    {
        if (_commonHudGroups.Count == 0)
        {
            // view: UI 동작과 데이터를 연결하는 뷰입니다.
            foreach (var view in FindObjectsByType<BaseView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (view.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                var group = view.GetComponent<CanvasGroup>(); // 그룹 값입니다.
                if (group != null)
                {
                    _commonHudGroups.Add(group);
                }

                // canvas: 캔버스 값입니다.
                foreach (var canvas in view.GetComponentsInChildren<Canvas>(true))
                {
                    var nested = canvas.GetComponent<CanvasGroup>(); // 중첩 값입니다.
                    if (nested == null)
                    {
                        nested = canvas.gameObject.AddComponent<CanvasGroup>();
                    }

                    if (!_commonHudGroups.Contains(nested))
                    {
                        _commonHudGroups.Add(nested);
                    }
                }
            }
        }

        // group: 그룹 값입니다.
        foreach (var group in _commonHudGroups)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (group == null)
            {
                continue;
            }

            if (!_hiddenHudStates.ContainsKey(group))
            {
                _hiddenHudStates[group] = new Vector3(group.alpha, group.interactable ? 1f : 0f, group.blocksRaycasts ? 1f : 0f);
            }

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 슬롯 선택 문맥을 보관하고 외부에 선택을 알립니다. 게임 메뉴 데이터는 수정하지 않습니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    /// <param name="manage">관리 여부입니다.</param>
    public void SelectMenuSlot(int index, int recipeId, bool manage)
    {
        _selectedMenuSlotIndex = index;
        _selectedRecipeId = recipeId;
        if (_menuView != null)
        {
            _menuView.SelectMenuSlot(index, recipeId);
        }

        if (!IsMenuBookTest) OnMenuSlotSelected?.Invoke(index, manage);
    }

    /// <summary>
    /// 선택한 요리의 상세 정보를 갱신합니다. 수량 팝업은 등록 버튼에서 엽니다.
    /// </summary>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    public void SelectRecipe(int recipeId)
    {
        _selectedRecipeId = recipeId;
        if (_menuView != null)
        {
            _menuView.SelectRecipe(recipeId);
        }

        if (!IsMenuBookTest) OnRecipeSelected?.Invoke(recipeId);
        GetComponentInChildren<SushiMenuBookView>(true)?.RefreshMode();
    }

    /// <summary>
    /// 직원 선택을 알리고 준비된 해금 상태를 표시합니다.
    /// </summary>
    /// <param name="staffId">직원 식별자 값입니다.</param>
    /// <param name="isLocked">잠금 여부입니다.</param>
    public void SelectStaff(int staffId, bool isLocked)
    {
        _selectedStaffId = staffId;
        if (_staffView == null || !_staffView.SelectStaff(staffId))
        {
            ShowStaffDetail(isLocked);
        }

        OnStaffSelected?.Invoke(staffId);
    }

    /// <summary>
    /// 영업 준비 안내와 하단 버튼의 표시 여부만 변경합니다.
    /// </summary>
    /// <param name="visible">화면이나 항목을 표시할지 여부입니다.</param>
    /// <summary>시작 안내 중에는 시작 버튼만 잠그며 기존 클릭 연결은 유지합니다.</summary>
    public void SetBusinessStartInteractable(bool interactable)
    {
        if (_startBusinessButton != null) _startBusinessButton.interactable = interactable;
    }
    public void SetPreparationVisible(bool visible)
    {
        if (_businessStartGuide != null)
        {
            _businessStartGuide.SetActive(visible);
        }

        if (_openTodayMenuButton != null)
        {
            _openTodayMenuButton.gameObject.SetActive(visible);
        }

        if (_openStaffButton != null)
        {
            _openStaffButton.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 두 메뉴 화면에 같은 슬롯 표시 데이터를 전달합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="information">정보 값입니다.</param>
    /// <param name="registered">등록 여부입니다.</param>
    /// <param name="interactable">상호작용 가능 여부입니다.</param>
    public void DisplayMenuSlot(int index, int recipeId, Sprite icon, string name, string information, bool registered, bool interactable = true)
    {
        if (_menuView != null)
        {
            _menuView.DisplayMenuSlot(index, recipeId, icon, name, information, registered, interactable);
        }
        else
        {
            // view: UI 동작과 데이터를 연결하는 뷰입니다.
            foreach (var view in GetComponentsInChildren<SushiTodayMenuView>(true))
            {
                view.DisplaySlot(index, recipeId, icon, name, information, registered, interactable);
            }
        }
    }

    /// <summary>
    /// 메뉴 다섯 칸과 독립된 음료 두 종류의 표시만 갱신합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    public void DisplayDrink(int index, Sprite icon, string name)
    {
        if (_menuView != null) _menuView.DisplayDrink(index, icon, name);
    }

    /// <summary>요리 원본을 변경하지 않고 화면에 사용할 아이콘을 반환합니다.</summary>
    public static Sprite ResolveMenuIcon(Sushi menu, Component context)
    {
        return menu == null ? null : ResolveMenuIcon(menu.Icon, menu.Name, context);
    }

    /// <summary>원본 아이콘을 우선 사용하고, 비어 있는 에이드만 같은 씬의 공통 View 이미지로 보완합니다.</summary>
    public static Sprite ResolveMenuIcon(Sprite original, string menuName, Component context)
    {
        if (original != null || context == null || string.IsNullOrEmpty(menuName)) return original;
        foreach (var book in UnityEngine.Object.FindObjectsByType<SushiMenuBookView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (book.gameObject.scene != context.gameObject.scene) continue;
            var icon = book.ResolveFallbackIcon(original, menuName);
            if (icon != null) return icon;
        }
        return null;
    }

    /// <summary>아이콘과 대체 표시를 전환합니다.</summary>
    public static void SetDisplayIcon(UnityEngine.UI.Image image, Sprite icon)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (image == null)
        {
            return;
        }

        image.sprite = icon;
        image.color = icon != null ? Color.white : new Color(.27f, .40f, .43f);
        // text: 텍스트 값입니다.
        foreach (var text in image.GetComponentsInChildren<TMP_Text>(true))
        {
            text.gameObject.SetActive(icon == null);
        }
    }

    /// <summary>
    /// 메뉴 표시 묶음을 대표 메뉴 View에 전달합니다. 계산이나 슬롯별 분배는 루트에서 하지 않습니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    public void DisplayMenu(SushiTodayMenuView.Presentation data)
    {

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (data == null || _menuView == null)
        {
            return;
        }

        _menuView.Display(data);
        DisplayAdditionalRecipeSlots(data);
        _selectedRecipeId = _menuView.SelectedRecipeId;
        GetComponentInChildren<SushiMenuBookView>(true)?.RefreshMode();
    }

    /// <summary>
    /// 직원 표시 묶음을 직원 관리 View에 전달합니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    public void DisplayStaff(SushiStaffView.Presentation data)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (data == null || _staffView == null)
        {
            return;
        }

        _staffView.Display(data);
        _selectedStaffId = _staffView.SelectedStaffId;
    }

    /// <summary>
    /// 계산이 끝난 정산 결과를 정산 View에 전달합니다. 창은 자동으로 열지 않습니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    public void DisplayBusinessResult(SushiBusinessResultView.Presentation data)
    {
        if (_businessResultView != null)
        {
            _businessResultView.Display(data);
        }
    }

    /// <summary>
    /// 선택 요리의 상세·수량 데이터만 갱신할 때 사용합니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    public void DisplayRecipeDetail(SushiTodayMenuView.RecipeData data)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (data == null || _menuView == null)
        {
            return;
        }

        _selectedRecipeId = data.RecipeId;
        _menuView.DisplayRecipeDetail(data);
    }

    /// <summary>직원 강화 성공 후 확정된 레벨만 표시합니다.</summary>
    public void DisplayStaffUpgradeResult(int level)
    {
        var staff=_staffView?.SelectedStaff;
        DisplayStaffUpgradeResult(new SushiRecipeUpgradeResultView.ResultData{IsStaff=true,Icon=staff?.Portrait,Name=staff?.Job+"직원",Level=level,AfterValue=staff?.Stats??$"레벨 {level}"});
    }
    public void DisplayStaffUpgradeResult(SushiRecipeUpgradeResultView.ResultData result)
    {
        if(_staffUpgradeResultView==null||_staffUpgradeResultPage==null)return;
        _staffUpgradeResultView.BindResult(result);
        _staffUpgradeResultPage.SetActive(true);_staffUpgradeResultPage.transform.SetAsLastSibling();
    }
    public void DisplayRecipeUpgradeResult(SushiRecipeUpgradeResultView.ResultData result)
    {
        _recipeUpgradeResultView?.BindResult(result);
    }

    /// <summary>
    /// 실제 요리 강화 성공 결과를 메뉴 파트에 전달합니다. 팝업 표시는 별도로 요청합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="message">메시지 값입니다.</param>
    /// <param name="stats">능력치 값입니다.</param>
    public void DisplayRecipeUpgradeResult(Sprite icon, string message, string stats)
    {
        if (_menuView != null)
        {
            _menuView.DisplayUpgradeResult(icon, message, stats);
        }
    }

    #endregion

    /// <summary>기존 스크롤 그리드에 실제 목록 길이만큼 요리 슬롯을 늘려 재사용합니다.</summary>
    void DisplayAdditionalRecipeSlots(SushiTodayMenuView.Presentation data)
    {
        if (_recipeSlotTemplate == null || _recipeScroll == null) return;
        int extraCount = Mathf.Max(0, (data.Recipes?.Length ?? 0) - _initialRecipeSlotCount);
        while (_additionalRecipeSlots.Count < extraCount)
        {
            var slot = Instantiate(_recipeSlotTemplate, _recipeScroll.content);
            slot.name = $"RecipeSlot{_initialRecipeSlotCount + _additionalRecipeSlots.Count + 1}";
            _additionalRecipeSlots.Add(slot);
        }
        for (int i = 0; i < _additionalRecipeSlots.Count; i++)
        {
            var slot = _additionalRecipeSlots[i];
            var recipe = i < extraCount ? data.Recipes[_initialRecipeSlotCount + i] : null;
            slot.gameObject.SetActive(recipe != null);
            if (recipe != null)
                slot.Bind(recipe.RecipeId, recipe.Icon, recipe.Name, recipe.Level, data.CanManage && recipe.CanSelect);
        }
        UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(_recipeScroll.content);
    }

    /// <summary>기본 음식과 음료를 레시피 탭의 기본 제공 영역에 표시합니다.</summary>
    public void DisplayBasicMenu(Sprite foodIcon, string foodName, SushiTodayMenuView.DrinkData[] drinks, string foodPrice = "") =>
        DisplayBasicMenu(foodIcon, foodName, drinks, foodPrice, string.Empty);

    /// <summary>기존 호출을 보존하면서 원본 기본 음식 설명을 함께 전달합니다.</summary>
    public void DisplayBasicMenu(Sprite foodIcon, string foodName, SushiTodayMenuView.DrinkData[] drinks, string foodPrice, string foodDescription) =>
        DisplayBasicMenu(foodIcon, foodName, drinks, foodPrice, foodDescription, null);

    /// <summary>레시피의 기본 음식 설명과 재료를 기존 표시 경로로 전달합니다.</summary>
    public void DisplayBasicMenu(Sprite foodIcon, string foodName, SushiTodayMenuView.DrinkData[] drinks, string foodPrice, string foodDescription, SushiTodayMenuView.IngredientData[] foodIngredients)
    {
        var book = GetComponentInChildren<SushiMenuBookView>(true);
        if (book != null)
        {
            var food = string.IsNullOrEmpty(foodName)
                ? null : new SushiTodayMenuView.DrinkData { Icon = foodIcon, Name = foodName, Price = foodPrice, Description = foodDescription, Ingredients = foodIngredients };
            book.DisplayBasicEntries(food, drinks);
            return;
        }
        for (int i = 0; _basicMenuSummaryIcons != null && i < _basicMenuSummaryIcons.Length; i++)
        {
            var drink = i > 0 && drinks != null && i <= drinks.Length ? drinks[i - 1] : null;
            var icon = i == 0 ? foodIcon : drink?.Icon;
            var name = i == 0 ? foodName : drink?.Name;
            SetDisplayIcon(_basicMenuSummaryIcons[i], icon);
            if (_basicMenuSummaryNames != null && i < _basicMenuSummaryNames.Length &&
                _basicMenuSummaryNames[i] != null)
                _basicMenuSummaryNames[i].text = name ?? string.Empty;
        }
    }

    /// <summary>씬의 공용 골드 HUD에 실제 보유 골드를 표시합니다.</summary>
    public void DisplayMenuWallet(float gold)
    {
        if (_goldHudText != null) _goldHudText.text = gold.ToString("N0");
    }

    public void ClearMenuSlotSelection()
    {
        _selectedMenuSlotIndex = -1;
        _menuView?.SelectMenuSlot(-1, _selectedRecipeId);
    }

    /// <summary>영업 안내 영역에 준비 부족 사유를 표시합니다.</summary>
    public void ShowBusinessWarning(string message)
    {
        if (_businessWarningText != null)
        {
            _businessWarningText.text = message ?? string.Empty;
            _businessWarningText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        else if (_businessStartGuideText != null)
        {
            _businessStartGuideText.text = message ?? _defaultBusinessGuide;
            _businessStartGuideText.color = string.IsNullOrEmpty(message)
                ? _defaultBusinessGuideColor : new Color32(255, 203, 110, 255);
        }
        if (!string.IsNullOrEmpty(message))
        {
            if (_businessStartGuide != null) _businessStartGuide.SetActive(true);
            Debug.LogWarning(message, this);
        }
    }

    /// <summary>실제 영업이 시작되면 열려 있는 준비 화면을 닫습니다.</summary>
    public void CloseManagement()
    {
        GetComponentInChildren<SushiMenuBookView>(true)?.Close();
        foreach (var page in new[] { _registeredMenuActionsPage, _menuQuantityPage, _recipeUpgradePage,
            _recipeUpgradeResultPage,
            _staffUpgradeResultPage, _staffManagementPage, _staffUpgradePage })
            if (page != null) page.SetActive(false);
    }

    #region 선택 및 외부 요청

    /// <summary>
    /// 선택한 메뉴 슬롯에 대한 요청만 외부에 전달합니다.
    /// </summary>
    public void RequestMenuReplacement()
    {
        if (!IsMenuBookTest) OnMenuReplaceRequested?.Invoke(_selectedMenuSlotIndex);
    }

    /// <summary>
    /// 선택한 메뉴 슬롯에 대한 요청만 외부에 전달합니다.
    /// </summary>
    public void RequestMenuQuantityEdit()
    {
        if (!IsMenuBookTest) OnMenuQuantityEditRequested?.Invoke(_selectedMenuSlotIndex);
    }

    /// <summary>
    /// 선택한 메뉴 슬롯에 대한 요청만 외부에 전달합니다.
    /// </summary>
    public void RequestMenuDeletion()
    {
        if (!IsMenuBookTest) OnMenuDeleteRequested?.Invoke(_selectedMenuSlotIndex);
    }

    /// <summary>
    /// 연결된 담당 로직에 요청을 전달합니다. true이면 자동 시안 완료 처리를 하지 않습니다.
    /// </summary>
    /// <param name="quantity">수량 값입니다.</param>
    public bool RequestQuantityConfirmation(int quantity)
    {
        var testBook = GetComponentInChildren<SushiMenuBookView>(true);
        if (testBook != null && testBook.IsTesting) return testBook.ConfirmTestQuantity(quantity);
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (OnMenuQuantityConfirmed == null)
        {
            return false;
        }

        OnMenuQuantityConfirmed.Invoke(_selectedMenuSlotIndex, _selectedRecipeId, quantity);
        return true;
    }

    /// <summary>
    /// 연결된 담당 로직에 요청을 전달합니다. true이면 자동 시안 완료 처리를 하지 않습니다.
    /// </summary>
    public bool RequestRecipeUpgrade()
    {
        var testBook = GetComponentInChildren<SushiMenuBookView>(true);
        if (testBook != null && testBook.IsTesting) return testBook.ConfirmTestUpgrade();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (OnRecipeUpgradeRequested == null)
        {
            return false;
        }

        OnRecipeUpgradeRequested.Invoke(_selectedRecipeId);
        return true;
    }

    /// <summary>
    /// 연결된 담당 로직에 요청을 전달합니다. true이면 자동 시안 완료 처리를 하지 않습니다.
    /// </summary>
    /// <param name="staffId">직원 식별자 값입니다.</param>
    public bool RequestStaffUpgrade(int staffId)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (OnStaffUpgradeRequested == null)
        {
            return false;
        }

        OnStaffUpgradeRequested.Invoke(staffId);
        return true;
    }

    /// <summary>
    /// 정산 확인 요청을 전달합니다. View는 씬을 직접 변경하지 않습니다.
    /// </summary>
    public void RequestSettlementConfirmation()
    {
        OnSettlementConfirmed?.Invoke();
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 공통 HUD의 원래 투명도와 입력 상태를 되돌립니다.
    /// </summary>
    void RestoreCommonHud()
    {
        // pair: pair 값입니다.
        foreach (var pair in _hiddenHudStates)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (pair.Key == null)
            {
                continue;
            }

            pair.Key.alpha = pair.Value.x;
            pair.Key.interactable = pair.Value.y > .5f;
            pair.Key.blocksRaycasts = pair.Value.z > .5f;
        }

        _hiddenHudStates.Clear();
    }

    #endregion

    #region 임시 데이터 및 테스트

    // 영업 시작 버튼은 Presenter에 요청을 전달합니다.
    // OpenBusinessResult는 ContextMenu 테스트와 실제 정산 화면 열기에 공통으로 사용합니다.

    /// <summary>
    /// 시작 버튼에서 Presenter에 영업 준비 검증을 요청합니다.
    /// </summary>
    void StartBusinessPreview()
    {
        if (OnBusinessStartRequested != null)
            OnBusinessStartRequested.Invoke();
        else
            ShowBusinessWarning("영업 준비 데이터를 연결하세요.");
    }

    /// <summary>
    /// 실제 영업 시스템 연결 전 정산 창의 표시를 확인할 수 있도록 엽니다.
    /// </summary>
    [ContextMenu("UI 테스트/영업 정산 열기")]
    public void OpenBusinessResult()
    {
        if (_businessResultPage != null)
        {
            _businessResultPage.SetActive(true);
        }
    }

    /// <summary>
    /// 화면 테스트 후 영업 시작 안내와 메뉴·직원 관리 버튼만 다시 표시합니다.
    /// </summary>
    [ContextMenu("UI 테스트/영업 전 안내 복원")]
    public void RestorePreparationUI()
    {
        if (_businessPreparationPage != null)
        {
            _businessPreparationPage.SetActive(true);
        }

        if (_businessStartGuide != null)
        {
            _businessStartGuide.SetActive(true);
        }

        if (_openTodayMenuButton != null)
        {
            _openTodayMenuButton.gameObject.SetActive(true);
        }

        if (_openStaffButton != null)
        {
            _openStaffButton.gameObject.SetActive(true);
        }
    }

    #endregion

}
