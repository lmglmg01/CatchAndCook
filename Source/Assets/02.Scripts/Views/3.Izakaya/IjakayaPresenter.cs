using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MenuSelection = SushiListManager.MenuSelection;
using RecipeData = SushiTodayMenuView.RecipeData;
using IngredientData = SushiTodayMenuView.IngredientData;
using Reservations = System.Collections.Generic.IReadOnlyDictionary<int, SushiListManager.MenuSelection>;

/// <summary>이자카야 화면 요청을 기존 영업·강화 모델에 연결합니다.</summary>
[DefaultExecutionOrder(100)]
public sealed class IjakayaPresenter : MonoBehaviour
{
    [SerializeField] IjakayaView _view;
    [SerializeField] SushiListManager _menuList;
    [SerializeField] SushiSpawnDifficultyData _menuSlotDifficulty; // 매니저와 같은 설정에서 표시용 해금 레벨만 읽습니다.
    [SerializeField] SushiManager _shop;
    [SerializeField] Sushi _fixedTodayMenu; // 기본 제공 오이절임을 오늘의 메뉴 첫 칸에 고정 표시합니다.
    [SerializeField] SushiUpgradeMap _upgradePreviewMap; // 강화 담당 매니저와 같은 원본 에셋입니다.
    [SerializeField, Tooltip("씬에 미리 배치한 의자 UI만 연결합니다. 구형 씬의 보완 생성은 사용하지 않습니다.")]
    bool _usePreplacedChairUI;

    // 등록 원본은 SushiListManager의 예약이며 화면에서는 조회한 스냅샷만 사용합니다.
    readonly Dictionary<int, Sushi> _recipes = new();
    Inventory _inventory;
    BaseView _timeSkipHud;
    bool _timeSkipInProgress;
    EnforceManager _enforce;
    // 씬 공용 효과 관리자의 접시 투척 이벤트를 구독하기 위한 참조입니다.
    GuestReactionEffects _reactionEffects;
    bool _settlementRequested, _settlementReady, _settlementConfirmed;
    int _settlementTicket;
    NPCCookingView _cookingDisplay;
    SushiStaffLevelView _servingLevelDisplay, _cookingLevelDisplay;
    bool _staffLevelSnapshotPending = true;
    GroundHero _groundHero;
    readonly Dictionary<Chair, SushiChairView> _chairDisplays = new();
    readonly List<GuestNPC> _seatGuests = new(); // 등록된 손님만 의자 변경을 확인합니다.
    Transform _seatUIReference; // 카메라에 가장 가까운 실제 의자를 앞줄 크기 기준으로 사용합니다.
    readonly List<Chair> _removedChairs = new();
    readonly List<GameObject> _createdWorldDisplays = new();
    bool _openingBusiness; // 시작 안내가 끝날 때까지 중복 요청을 차단합니다.
    int _openingTicket; // 취소된 안내의 완료 콜백을 무효화합니다.
    bool _initialized;
    bool _refreshPending;
    bool _wasBusinessConfirmed;
    float _businessClockDuration; // 실제 영업이 확정된 순간의 남은 시간을 기준으로 보관합니다.
    string _menuNotice;

    internal bool HasRegisteredMenu => _menuList != null && _menuList.ReservedMenu.Count > 0;
    internal bool IsRecipeUpgradeResultOpen => _view.RecipeUpgradeResultView != null &&
        _view.RecipeUpgradeResultView.gameObject.activeInHierarchy;

    internal void OpenTutorialRecipeUpgrade(Sushi menu)
    {
        _view.SelectRecipe(menu.Id);
        _view.OpenRecipeUpgrade();
    }

    internal bool OpenTutorialStaffUpgrade()
    {
        if (_view.StaffView == null || _view.StaffUpgradeView == null) return false;

        _view.CloseManagement();
        _view.OpenStaff();
        _view.SelectStaff((int)StaffType.Cooking, false);
        _view.OpenStaffUpgrade();
        return _view.StaffUpgradeView.isActiveAndEnabled;
    }

    internal void CloseTutorialManagement() => _view.CloseManagement();

    bool CanEditMenu => _shop != null && !_shop.IsMenuConfirmed;
    bool CanEditReservation => CanEditMenu && _menuList != null && _menuList.CanEditMenuReservation;
    int _upgradePreviewRecipeId = -1; // 열린 강화 확인창의 대상을 갱신할 때 유지합니다.
    const string MinimumMenuMaterialNotice = "영업에 필요한 메뉴 1개를 등록할 재료를 남겨야 합니다.";
    Sushi FixedTodayMenu => _fixedTodayMenu != null && _menuList.IsBasicProvidedMenu(_fixedTodayMenu) ? _fixedTodayMenu : null;
    int FixedMenuSlotCount => FixedTodayMenu != null ? 1 : 0;
    int SlotLimit => Mathf.Min(_menuList.TodayMenuLimit, Mathf.Max(0, _view.MenuSlotCapacity - FixedMenuSlotCount));
    // 화면 첫 칸만 고정 메뉴이며 모델에 전달할 일반 메뉴의 인덱스·수량은 기존 그대로 유지합니다.
    int ToDraftSlot(int displaySlot) => displaySlot - FixedMenuSlotCount;

    string LockedMenuSlotNotice(int slot)
    {
        if (_menuSlotDifficulty != null)
            for (int level = 1; level <= 1000; level++)
                if (_menuSlotDifficulty.GetTodayMenuLimit(level) > slot)
                    return "플레이어\n레벨 " + level;
        return "잠김";
    }

    #region 초기화와 갱신

    /// <summary>매니저 초기화 뒤 실제 인벤토리와 화면 이벤트를 연결합니다.</summary>
    void Start()
    {
        // 프리팹은 씬 오브젝트를 참조할 수 없으므로 이 씬의 모델을 찾습니다.
        if (_view == null) _view = GetComponent<IjakayaView>();
        if (_shop == null) _shop = FindSceneModel<SushiManager>();
        if (_menuList == null && _shop != null) _menuList = _shop.GetComponent<SushiListManager>();
        if (_menuList == null) _menuList = FindSceneModel<SushiListManager>();

        if (_view == null || _menuList == null || _shop == null)
        {
            Debug.LogError("이자카야 Presenter에 화면과 두 매니저를 연결하세요.", this);
            return;
        }

        _inventory = _shop.Inventory;
        _enforce = _shop.Enforce;
        // 현재 씬의 공용 효과 관리자를 초기화 시 한 번 찾아 보관합니다.
        _reactionEffects = FindSceneModel<GuestReactionEffects>();
        _initialized = true;
        Subscribe();
        SubscribeWorldCanvases();
        BootstrapWorldCanvases();
        ConnectWorldDisplays();
        Refresh();
        _view.SetPreparationVisible(CanEditMenu);
    }

    /// <summary>같은 씬에 모델이 하나만 있는 경우 프리팹의 빈 참조를 채웁니다.</summary>
    T FindSceneModel<T>() where T : MonoBehaviour
    {
        var matches = gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    void OnEnable()
    {
        if (!_initialized) return;
        Subscribe();
        SubscribeWorldCanvases();
        ConnectWorldDisplays();
        QueueRefresh();
        if (_settlementRequested && !_settlementReady) { _settlementRequested = false; PresentSettlement(); }
    }

    void OnDisable()
    {
        if (_timeSkipHud != null) _timeSkipHud.OnSkipTime -= SkipNight;
        UnsubscribeWorldCanvases();
        _staffLevelSnapshotPending = true;
        if (_servingLevelDisplay != null) _servingLevelDisplay.ResetPresentation();
        if (_cookingLevelDisplay != null) _cookingLevelDisplay.ResetPresentation();
        CancelBusinessOpening();
        foreach (var display in _chairDisplays.Values) if (display != null) display.Hide();
        if (_settlementRequested && !_settlementReady)
        {
            ++_settlementTicket;
            _view?.GetComponent<SushiRestaurantStyleView>()?.CancelBusinessSign();
        }
        if (!_initialized) return;
        _view.OnManagementOpened -= Refresh;
        _view.OnMenuSlotSelected -= OnMenuSlotSelected;
        _view.OnRecipeSelected -= OnRecipeSelected;
        _view.OnMenuReplaceRequested -= BeginReplacement;
        _view.OnMenuQuantityEditRequested -= BeginQuantityEdit;
        _view.OnMenuDeleteRequested -= DeleteMenu;
        _view.OnMenuQuantityConfirmed -= RegisterMenu;
        _view.OnBusinessStartRequested -= StartBusiness;
        _view.OnRecipeUpgradePreviewRequested -= PreviewRecipeUpgrade;
        _view.OnRecipeUpgradeRequested -= UpgradeRecipe;
        _view.OnStaffUpgradeRequested -= UpgradeStaff;
        _view.OnSettlementConfirmed -= ConfirmSettlement;
        _menuList.TodayMenuChanged -= QueueRefresh;
        _menuList.MenuReservationChanged -= QueueRefresh;
        if (_inventory != null) _inventory.Changed -= QueueRefresh;
        if (_enforce != null) _enforce.UpgradeStateChanged -= QueueRefresh;
        // Presenter가 비활성화되면 접시 투척 알림을 받지 않도록 해제합니다.
        if (_reactionEffects != null)
        {
            // 구독할 때 등록한 동일한 메서드를 제거합니다.
            _reactionEffects.PlateThrown -= HandlePlateThrown;
        }
    }

    /// <summary>화면 입력과 모델 변경 알림을 구독합니다.</summary>
    void Subscribe()
    {
        if (_timeSkipHud == null) _timeSkipHud = FindSceneModel<BaseView>();
        if (_timeSkipHud != null)
        {
            _timeSkipHud.OnSkipTime -= SkipNight;
            _timeSkipHud.OnSkipTime += SkipNight;
        }
        _view.OnManagementOpened += Refresh;
        _view.OnMenuSlotSelected += OnMenuSlotSelected;
        _view.OnRecipeSelected += OnRecipeSelected;
        _view.OnMenuReplaceRequested += BeginReplacement;
        _view.OnMenuQuantityEditRequested += BeginQuantityEdit;
        _view.OnMenuDeleteRequested += DeleteMenu;
        _view.OnMenuQuantityConfirmed += RegisterMenu;
        _view.OnBusinessStartRequested += StartBusiness;
        _view.OnRecipeUpgradePreviewRequested += PreviewRecipeUpgrade;
        _view.OnRecipeUpgradeRequested += UpgradeRecipe;
        _view.OnStaffUpgradeRequested += UpgradeStaff;
        _view.OnSettlementConfirmed += ConfirmSettlement;
        _menuList.TodayMenuChanged += QueueRefresh;
        _menuList.MenuReservationChanged += QueueRefresh;
        if (_inventory != null) _inventory.Changed += QueueRefresh;
        if (_enforce != null) _enforce.UpgradeStateChanged += QueueRefresh;
        // 씬의 공용 효과 관리자가 연결된 경우에만 투척 알림을 구독합니다.
        if (_reactionEffects != null)
        {
            // 재구독 시 같은 핸들러가 중복 등록되지 않도록 제거합니다.
            _reactionEffects.PlateThrown -= HandlePlateThrown;
            // 접시가 발사되면 화면 연출 요청을 처리할 핸들러를 등록합니다.
            _reactionEffects.PlateThrown += HandlePlateThrown;
        }
    }

    /// <summary>시간스킵을 기존 밤 종료·다음 날 이동 경로에 연결합니다.</summary>
    void SkipNight()
    {
        var manager = GameManager.Instance;
        if (_timeSkipInProgress || manager == null || !(manager.CurrentPlayScene is SushiPlayScene)) return;
        _timeSkipInProgress = true;
        if (!manager.CompleteNight()) _timeSkipInProgress = false;
    }

    /// <summary>접시 투척 알림을 받아 관련 화면 연출을 연결할 진입점입니다.</summary>
    void HandlePlateThrown()
    {
        if (!_initialized || _view == null || _shop == null) return;
        _view.ShowGuestPenalty($"운영비 +{_shop.WaitingFailureCostPenalty:0.##} G",
            $"영업시간 -{_shop.WaitingFailureTimePenalty:0.##}초");
    }

    // 재료 차감 도중의 이벤트는 모델 처리가 끝난 프레임에 한 번만 반영합니다.
    void QueueRefresh() => _refreshPending = true;

    void LateUpdate()
    {
        if (_refreshPending) Refresh();
        if (_initialized && _shop != null)
        {
            // TODO(UI-EVENT-01 / 영업 상태 임시 조회): SushiManager.OperationStateChanged 이벤트가 필요합니다(제안 이름, 모델에 아직 없음).
            // 연결할 Presenter 함수: HandleOperationStateChanged() 신규 구현 필요. 아래 확정 전환·미션 메모 처리와 시계 초기화를 옮깁니다.
            // 시작·종료·재초기화 알림과 최초 스냅샷을 연결한 뒤 이 매 프레임 상태 조회만 제거합니다. 영업 시작 검증은 유지합니다.
            bool confirmed = _shop.IsMenuConfirmed;
            _timeSkipHud?.SetTimeSkipBusinessActive(confirmed);
            // 실제 영업 시작 전환에서 한 번만 접습니다. 이후 수동 펼침은 유지합니다.
            if (confirmed && !_wasBusinessConfirmed) _view.SetMissionMemoExpanded(false);
            _wasBusinessConfirmed = confirmed;
            RefreshBusinessClock(confirmed);
        }
        if (_initialized)
        {
            RefreshGuestSeatBindings();
            if (_worldPresentationDirty) RefreshRegisteredWorldViews();
            RefreshCookingDisplays();
            // TODO(UI-EVENT-02 / 의자 임시 조회): Chair.DisplayStateChanged + 감지 담당의 CleaningInteractionChanged가 필요합니다(둘 다 추가 제안).
            // 연결할 Presenter 함수: NotifyChairDisplayChanged(chair). 잔반·청소 시작/진행/중단/완료, 영역 진입/이탈·이전/새 대상을 모두 알립니다.
            // 모두 연결한 뒤 SetChairNotificationsEnabled(true)로 끄고 검증 후 이 호출만 제거합니다. 초기 스냅샷과 View의 위치·회전 갱신은 유지합니다.
            if (!_chairNotificationsEnabled) RefreshChairDisplays();
        }
    }

    /// <summary>메뉴, 등록 초안과 직원 정보를 화면용 데이터로 변환합니다.</summary>
    void Refresh()
    {
        if (!_initialized || _view.IsMenuBookTest) return;
        _refreshPending = false;
        _recipes.Clear();
        foreach (var menu in _menuList.GetAvailableMenu())
            if (menu != null) _recipes.TryAdd(menu.Id, menu);

        var basicMenu = _menuList.BasicProvidedMenu;
        var drinks = basicMenu.Where(menu => menu != null && menu.Type == MenuType.Drink)
            .Select(menu => new SushiTodayMenuView.DrinkData { Icon = IjakayaView.ResolveMenuIcon(menu, _view), Name = menu.Name, Price = menu.Gold.ToString("N0"), Description = menu.Description, Ingredients = CatalogIngredients(menu) })
            .ToArray();
        var food = basicMenu.FirstOrDefault(menu => menu != null && menu.Type == MenuType.Food);
        _view.DisplayBasicMenu(food?.Icon, food?.Name, drinks, food?.Gold.ToString("N0"), food?.Description, CatalogIngredients(food));
        if (_inventory != null) _view.DisplayMenuWallet(_inventory.Gold);

        int selection = _view.SelectedRecipeId;
        if (!_recipes.ContainsKey(selection) && _recipes.Count > 0)
            selection = _recipes.Keys.First();
        var confirmed = _menuList.TodayMenu;
        var reservations = _menuList.ReservedMenu; // 이 화면 구성에서는 한 번만 조회합니다.
        var slots = new SushiTodayMenuView.MenuSlotData[_view.MenuSlotCapacity];
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < FixedMenuSlotCount)
            {
                var fixedMenu = FixedTodayMenu;
                slots[i] = new SushiTodayMenuView.MenuSlotData
                {
                    RecipeId = fixedMenu.Id, Icon = fixedMenu.Icon, Name = fixedMenu.Name,
                    Information = "기본 제공 · 고정", Registered = true, Interactable = false
                };
                continue;
            }
            int slot = ToDraftSlot(i);
            reservations.TryGetValue(slot, out var entry);
            if (!CanEditMenu) entry = slot < confirmed.Count ? confirmed[slot] : null;
            slots[i] = new SushiTodayMenuView.MenuSlotData
            {
                RecipeId = entry?.Menu.Id ?? -1,
                Icon = entry?.Menu.Icon,
                Name = entry?.Menu.Name,
                Information = entry == null ? (slot < SlotLimit ? "" : LockedMenuSlotNotice(slot))
                    : CanEditMenu ? $"등록 {entry.Amount}개" : $"남은 수량 {entry.Remaining}개",
                Registered = entry != null,
                Interactable = slot < SlotLimit
            };
        }

        _view.DisplayMenu(new SushiTodayMenuView.Presentation
        {
            Menus = slots,
            Recipes = _recipes.Values.Select(menu => BuildRecipe(menu, reservations)).ToArray(),
            Drinks = drinks,
            SelectedRecipeId = selection,
            CanManage = CanEditReservation,
            RegistrationNotice = _menuNotice ?? (FixedMenuSlotCount > 0 ? $"고정 메뉴 1종 · 일반 메뉴 {reservations.Count}/{SlotLimit}종" : $"오늘의 메뉴 {reservations.Count}/{SlotLimit}종"),
            UnavailableNotice = _recipes.Count == 0 ? "등록 가능한 요리가 없습니다." : string.Empty
        });
        RefreshStaff();
        if (_view.RecipeUpgradeView != null && _view.RecipeUpgradeView.isActiveAndEnabled)
            ShowRecipeUpgrade(_upgradePreviewRecipeId >= 0 ? _upgradePreviewRecipeId : selection, null);
    }

    #endregion

    /// <summary>별도 타이머 없이 실제 영업 잔여 시간으로 저녁 시계만 매 프레임 갱신합니다.</summary>
    void RefreshBusinessClock(bool confirmed)
    {
        if (!confirmed)
        {
            _businessClockDuration = 0f;
            _view.SetBusinessClockProgress(0f, false);
            return;
        }
        // TODO(UI-EVENT-03 / 영업 시간 임시 조회): SushiManager.OperationTimeChanged(remaining, total) 알림이 필요합니다(제안 계약, 아직 없음).
        // 연결할 Presenter 함수: HandleOperationTimeChanged(float remaining, float total) 신규 구현 필요. 이 함수의 게이지 계산·초기화 처리를 분리합니다.
        // 최초 시간, 실제 경과, 시간 차감, 종료·초기화까지 전달된 뒤 LateUpdate의 RefreshBusinessClock 호출과 이 반복 조회를 제거할 수 있습니다.
        // SetCooking*NotificationsEnabled/SetChairNotificationsEnabled는 이 조회를 끄지 않습니다. 기존 완료·정산 조건 검사는 유지합니다.
        float remaining = Mathf.Max(0f, _shop.RemainingOperationTime);
        // 외부의 기존 메뉴 확정 API로 영업을 시작한 경우에도 연결합니다.
        if (_businessClockDuration <= 0f && remaining > 0f) _businessClockDuration = remaining;
        float progress = _businessClockDuration > 0f ? Mathf.Clamp01(1f - remaining / _businessClockDuration) : 1f;
        _view.SetBusinessClockProgress(progress);
    }

    #region 메뉴 준비

    /// <summary>지정된 슬롯 또는 첫 번째 빈 등록 슬롯을 찾습니다.</summary>
    /// <summary>비활성 안내에 필요한 제한 사유만 읽습니다. 재료나 등록 초안은 변경하지 않습니다.</summary>
    public string GetDisabledMenuReason(string kind, int requested = -1)
    {
        if (!_initialized || _view == null || _shop == null) return "매장 정보를 불러오는 중입니다.";
        if (!CanEditMenu) return "오늘의 메뉴가 확정되어 변경할 수 없습니다.";
        if (!CanEditReservation) return "재료를 처리하는 중입니다. 잠시 후 다시 시도하세요.";
        if (!_recipes.TryGetValue(_view.SelectedRecipeId, out var menu)) return "요리를 먼저 선택하세요.";
        var lines = new List<string>();
        if (kind == "upgrade")
        {
            if (_enforce == null || !_enforce.TryGetMenuUpgradeRequirements(menu, out float cost, out var requirements))
                return "최대 레벨이거나 다음 강화 정보가 없습니다.";
            if (!_shop.CanManageStaff) lines.Add("현재 영업 상태에서는 강화할 수 없습니다.");
            if (_inventory == null) return "보유 재료 정보를 확인할 수 없습니다.";
            if (_inventory.Gold < cost) lines.Add($"골드 부족: {_inventory.Gold:N0} / {cost:N0}");
            foreach (var r in requirements)
            {
                int available = _shop.GetAvailableIngredientCount(r.Key);
                if (available < r.Value)
                    lines.Add($"{r.Key.ItemName} 부족: 강화 사용 가능 {available} / {r.Value} (보유 {_inventory.GetCount(r.Key)}, 메뉴 예약분 제외)");
            }
            if (lines.Count == 0 && WouldExhaustLastRegistrableMenu(menu)) lines.Add(MinimumMenuMaterialNotice);
            return lines.Count > 0 ? string.Join("\n",lines) : "강화 상태 갱신 중입니다. 잠시 후 다시 확인하세요.";
        }
        var reservations = _menuList.ReservedMenu;
        int slot = FindTargetSlot(reservations);
        if (slot < 0 || slot >= SlotLimit) return $"등록 가능한 메뉴 슬롯 {SlotLimit}개를 모두 사용했습니다.";
        if (reservations.Any(e => e.Key != slot && e.Value.Menu == menu)) return "이미 등록된 요리입니다. 왼쪽 메뉴를 선택해 수량을 변경하세요.";
        if (!TryGetRecipeRequirements(menu, out var recipe)) return "요리의 재료 정보가 올바르게 설정되지 않았습니다.";
        int maximum = MaximumQuantity(menu, slot);
        if (requested == 0) lines.Add("등록 수량은 1개 이상의 숫자로 입력하세요.");
        if (requested > maximum) lines.Add($"등록 가능 수량 초과: 입력 {requested}개 / 최대 {maximum}개");
        foreach (var r in recipe)
        {
            int available = AvailableIngredient(r.Key, slot);
            if (available < r.Value) lines.Add($"{r.Key.ItemName} 부족: 사용 가능 {available} / 1접시당 {r.Value} (보유 {_inventory?.GetCount(r.Key) ?? 0}, 다른 메뉴 예약분 제외)");
        }
        if (lines.Count == 0) lines.Add($"현재 등록 가능 수량은 최대 {maximum}개입니다.");
        return string.Join("\n", lines);
    }
    int FindTargetSlot(Reservations reservations = null)
    {
        reservations ??= _menuList.ReservedMenu;
        int selected = _view.SelectedMenuSlotIndex;
        if (selected >= 0) selected = ToDraftSlot(selected);
        if (_view.SelectedMenuSlotIndex >= 0) return selected >= 0 && selected < SlotLimit ? selected : -1;
        for (int i = 0; i < SlotLimit; i++)
            if (!reservations.ContainsKey(i)) return i;
        return -1;
    }

    /// <summary>동일한 재료가 여러 줄에 있을 때 한 접시의 총 요구량을 합칩니다.</summary>
    static bool TryGetRecipeRequirements(Sushi menu, out Dictionary<ItemData, int> requirements)
    {
        requirements = new Dictionary<ItemData, int>();
        foreach (var ingredient in menu.Ingredients)
        {
            if (ingredient == null || ingredient.Item == null || ingredient.Amount <= 0) return false;
            requirements.TryGetValue(ingredient.Item, out int amount);
            if (amount > int.MaxValue - ingredient.Amount) return false;
            requirements[ingredient.Item] = amount + ingredient.Amount;
        }
        return requirements.Count > 0;
    }

    /// <summary>편집 중인 슬롯을 제외한 다른 등록 수량에 필요한 재료를 뺍니다.</summary>
    int AvailableIngredient(ItemData item, int editedSlot)
    {
        return _menuList != null ? _menuList.GetAvailableIngredientCount(item, editedSlot) : 0;
    }

    /// <summary>공유 재료까지 고려해 등록 가능한 수량을 구합니다.</summary>
    int MaximumQuantity(Sushi menu, int slot)
    {
        // 백엔드의 매장 한도 외에 실제 책 UI가 보유한 슬롯 용량도 유지합니다.
        if (!CanEditReservation || slot < 0 || slot >= SlotLimit) return 0;
        return _menuList.GetMaximumReservableQuantity(slot, menu);
    }

    /// <summary>제작용 재료와 강화용 재료를 각각 화면에 전달합니다.</summary>
    RecipeData BuildRecipe(Sushi menu, Reservations reservations = null)
    {
        reservations ??= _menuList.ReservedMenu;
        int slot = FindTargetSlot(reservations);
        int maximum = CanEditMenu ? MaximumQuantity(menu, slot) : 0;
        int level = _enforce != null ? _enforce.GetMenuLevel(menu) : 1;
        float gold = 0;
        int experience = 0;
        bool hasReward = _enforce != null && _enforce.TryGetReward(menu, out gold, out experience);
        string price = hasReward
            ? gold.ToString("N0") : "—";
        if (!TryGetRecipeRequirements(menu, out var ingredients)) ingredients.Clear();
        var data = new RecipeData
        {
            RecipeId = menu.Id,
            Icon = menu.Icon,
            Name = menu.Name,
            Level = level,
            LevelPrice = $"Lv.{level}    {price}",
            SalePrice = price,
            SaleExperience = hasReward ? experience.ToString("N0") : "—",
            NextSalePrice = level >= SushiUpgradeData.MaxLevel ? "최대" : "—",
            NextSaleExperience = level >= SushiUpgradeData.MaxLevel ? "최대" : "—",
            Description = string.IsNullOrWhiteSpace(menu.Description) ? "요리 설명 미등록" : menu.Description,
            CatalogIngredients = CatalogIngredients(menu),
            CanSelect = true,
            CanRegister = maximum > 0,
            MaximumQuantity = maximum,
            CurrentQuantity = reservations.TryGetValue(slot, out var current) && current.Menu == menu
                ? current.Amount : 1,
            CanConfirmQuantity = maximum > 0,
            Ingredients = ingredients.Select(entry => Ingredient(entry.Key, entry.Value,
                AvailableIngredient(entry.Key, slot))).ToArray()
        };
        if (_enforce != null && _enforce.TryGetMenuUpgradeRequirements(menu, out float cost, out var upgrade))
        {
            data.CanUpgrade = CanUpgradeRecipeForUI(menu);
            data.UpgradePrompt = $"{menu.Name} 강화";
            data.UpgradeCost = cost.ToString("N0");
            data.BeforeStats = data.LevelPrice;
            data.AfterStats = $"Lv.{level + 1}";
            // 매니저에 연결된 것과 같은 강화 에셋의 보상 계산을 재사용합니다.
            var matching = _upgradePreviewMap?.Datas.Where(entry => entry != null && entry.TargetSushi == menu)
                .Take(2).ToArray();
            if (matching?.Length == 1 && matching[0].TryGetReward(menu, level + 1, out float nextGold, out int nextExperience))
            {
                data.NextSalePrice = nextGold.ToString("N0");
                data.NextSaleExperience = nextExperience.ToString("N0");
                data.AfterStats += $"    {data.NextSalePrice}";
            }
            data.UpgradeIngredients = upgrade.Select(entry => Ingredient(entry.Key, entry.Value,
                _shop.GetAvailableIngredientCount(entry.Key))).ToArray();
        }
        return data;
    }

    /// <summary>제작 재료를 화면용으로 전달하며 모든 예약분을 제외합니다. 재료나 예약을 변경하지 않습니다.</summary>
    IngredientData[] CatalogIngredients(Sushi menu)
    {
        if (menu == null || _shop == null || !TryGetRecipeRequirements(menu, out var recipe))
            return Array.Empty<IngredientData>();
        return recipe.Select(entry => Ingredient(entry.Key, entry.Value,
            _shop.GetAvailableIngredientCount(entry.Key))).ToArray();
    }

    /// <summary>
    /// 재료 정보와 수량을 획득처가 포함된 화면용 데이터로 변환합니다.
    /// </summary>
    static IngredientData Ingredient(ItemData item, int amount, int owned)
    {
        // 아이템 정보와 계산된 수량을 화면용 데이터에 담습니다.
        return new IngredientData
        {
            Icon = item.ItemIcon, // 재료 아이콘입니다.
            Name = item.ItemName, // 재료 이름입니다.
            Required = amount, // 필요한 수량입니다.
            Owned = owned, // 사용 가능한 보유량입니다.
            AcquisitionSource = GetAcquisitionSource(item) // 획득처 안내입니다.
        };
    }

    /// <summary>
    /// 물고기의 출현 수심 또는 일반 재료의 획득처 설명을 툴팁 문구로 반환합니다.
    /// </summary>
    static string GetAcquisitionSource(ItemData item)
    {
        // 물고기 아이템이면 기존 출현 수심 정보를 사용합니다.
        if (item is FishFilletItemData fishItem)
        {
            // 수심 정보가 없으면 미등록 상태로 전달합니다.
            if (string.IsNullOrWhiteSpace(fishItem.Depth))
            {
                return string.Empty;
            }

            // 여러 출현 구간은 줄바꿈하여 읽기 쉽게 표시합니다.
            return "출현 수심\n" + fishItem.Depth.Replace(", ", "\n");
        }

        // 일반 재료 아이템이면 에셋에 입력된 획득처 설명을 사용합니다.
        if (item is IngredientItemData ingredientItem)
        {
            // 설명이 비어 있으면 미등록 상태로 전달합니다.
            if (string.IsNullOrWhiteSpace(ingredientItem.GetDesc))
            {
                return string.Empty;
            }

            // 설명의 앞뒤 공백을 제거하여 툴팁으로 전달합니다.
            return ingredientItem.GetDesc.Trim();
        }

        // 획득처를 지원하지 않는 아이템은 미등록 상태로 전달합니다.
        return string.Empty;
    }

    void OnMenuSlotSelected(int slot, bool manage)
    {
        _menuNotice = null;
        Refresh();
    }

    void OnRecipeSelected(int id)
    {
        if (_recipes.TryGetValue(id, out var menu)) _view.DisplayRecipeDetail(BuildRecipe(menu));
    }

    void BeginReplacement(int slot)
    {
        if (ToDraftSlot(slot) < 0) return;
        _menuNotice = "교체할 요리를 선택하고 등록 수량을 확인하세요.";
        Refresh();
    }

    void BeginQuantityEdit(int slot)
    {
        slot = ToDraftSlot(slot);
        if (_menuList.ReservedMenu.TryGetValue(slot, out var entry)) OnRecipeSelected(entry.Menu.Id);
    }

    /// <summary>재료를 소비하기 전에 등록 내용을 검증하고 초안에 반영합니다.</summary>
    void RegisterMenu(int slot, int id, int amount)
    {
        if (!CanEditReservation || !_recipes.TryGetValue(id, out var menu)) return;
        slot = slot < 0 ? FindTargetSlot() : ToDraftSlot(slot);
        if (slot < 0 || slot >= SlotLimit || !_menuList.TryReserveMenu(slot, menu, amount))
        {
            _menuNotice = "등록을 변경할 수 없습니다. 사용 가능한 재료와 최신 최대 수량을 확인하세요.";
            Refresh();
            return;
        }
        _menuNotice = $"{menu.Name} {amount}개 등록";
        _view.QuantityView?.Close();
        _view.ClearMenuSlotSelection();
        _view.ShowBusinessWarning(null);
        Refresh();
    }

    void DeleteMenu(int slot)
    {
        slot = ToDraftSlot(slot);
        if (!CanEditReservation || slot < 0 || slot >= SlotLimit) return;
        if (!_menuList.TryRemoveReservedMenu(slot))
        {
            _menuNotice = "메뉴 예약을 삭제할 수 없습니다. 현재 상태를 확인하세요.";
            Refresh();
            return;
        }
        _menuNotice = "등록한 메뉴를 삭제했습니다.";
        _view.ClearMenuSlotSelection();
        Refresh();
    }

    /// <summary>준비 검증 후 2초 안내를 표시합니다. 안내 도중에는 추가 시작 요청을 받지 않습니다.</summary>
    void StartBusiness()
    {
        if (GameManager.Instance?.CurrentPlayScene is SushiPlayScene scene && !scene.AllowsBusinessStart) return;
        if (_openingBusiness || !ValidateBusinessPreparation()) return;
        var style = _view.GetComponent<SushiRestaurantStyleView>();
        if (style == null || !style.isActiveAndEnabled)
        {
            _view.ShowBusinessWarning("영업 시작 안내 UI 연결을 확인하세요.");
            return;
        }
        _openingBusiness = true;
        int ticket = ++_openingTicket;
        _view.SetBusinessStartInteractable(false);
        _view.ShowBusinessWarning(null);
        _view.CloseManagement();
        style.ShowBusinessOpening(() =>
        {
            if (!isActiveAndEnabled || !_openingBusiness || ticket != _openingTicket) return;
            try
            {
                // 안내 중 변경된 재료·직원·등록 수량을 다시 검사한 뒤 기존 API로 한 번만 확정합니다.
                if (!ValidateBusinessPreparation()) return;
                if (!_shop.ConfirmReservedTodayMenu())
                {
                    _view.ShowBusinessWarning("영업을 시작할 수 없습니다. 메뉴와 직원 설정을 확인하세요.");
                    return;
                }
                _view.ShowBusinessWarning(null);
                // 같은 프레임에 확정 직후 값을 읽어 최초 조리 틱 전의 영업시간을 보관합니다.
                _businessClockDuration = Mathf.Max(0f, _shop.RemainingOperationTime);
                _timeSkipHud?.SetTimeSkipBusinessActive(true);
                _view.SetPreparationVisible(false);
                _view.CloseManagement();
                // 예약은 확정에 성공한 백엔드에서 해제합니다. 여기서 다시 지우지 않습니다.
                _menuNotice = null;
                Refresh();
            }
            finally
            {
                _openingBusiness = false;
                _view.SetBusinessStartInteractable(true);
            }
        });
    }

    /// <summary>영업 전 필수 조건을 읽기만 하며 실패 사유를 안내합니다.</summary>
    bool ValidateBusinessPreparation()
    {
        if (!_initialized || !CanEditReservation || _view.IsMenuBookTest) return false;
        var reservations = _menuList.ReservedMenu;
        if (reservations.Count < _menuList.MinimumTodayMenuCount)
        {
            _view.ShowBusinessWarning($"오늘의 메뉴를 {_menuList.MinimumTodayMenuCount}종 이상 등록하세요.");
            return false;
        }
        if (reservations.Count > SlotLimit || reservations.Keys.Any(slot => slot < 0 || slot >= SlotLimit))
        {
            _view.ShowBusinessWarning($"현재 매장에서는 메뉴를 최대 {SlotLimit}종까지 등록할 수 있습니다.");
            return false;
        }
        if (_inventory == null || !_shop.CanManageStaff || _shop.Cooker == null || !_shop.Cooker.isActiveAndEnabled)
        {
            _view.ShowBusinessWarning("인벤토리와 요리 직원의 준비 상태를 확인하세요.");
            return false;
        }
        if (reservations.Any(entry => entry.Value.Amount < 1 || MaximumQuantity(entry.Value.Menu, entry.Key) < entry.Value.Amount))
        {
            _view.ShowBusinessWarning("등록한 메뉴를 준비할 재료가 부족합니다.");
            return false;
        }

        return true;
    }

    /// <summary>뷰가 비활성화되면 대기 요청과 안내를 취소하고 버튼을 복원합니다.</summary>
    void CancelBusinessOpening()
    {
        if (!_openingBusiness) return;
        _openingBusiness = false;
        ++_openingTicket;
        if (_view != null)
        {
            _view.SetBusinessStartInteractable(true);
            _view.GetComponent<SushiRestaurantStyleView>()?.CancelBusinessSign();
        }
    }
    #endregion

    #region 강화와 직원

    /// <summary>백엔드의 강화 조건에 영업 준비 재료 보호 조건을 더한 화면용 판단입니다.</summary>
    bool CanUpgradeRecipeForUI(Sushi menu)
    {
        return CanEditReservation && _enforce != null && _enforce.CanUpgradeMenu(menu) &&
            !WouldExhaustLastRegistrableMenu(menu);
    }

    /// <summary>예약 메뉴가 없으면 강화 후에도 일반 요리 한 접시를 등록할 재료가 남는지 읽기만 합니다.</summary>
    bool WouldExhaustLastRegistrableMenu(Sushi upgradedMenu)
    {
        if (_menuList == null || _inventory == null || _enforce == null || upgradedMenu == null) return false;
        // 이미 확보한 메뉴는 백엔드의 기존 예약 보호를 따릅니다.
        if (_menuList.ReservedMenu.Count > 0) return false;
        if (!_enforce.TryGetMenuUpgradeRequirements(upgradedMenu, out _, out var consumption)) return false;

        foreach (var candidate in _menuList.GetAvailableMenu())
        {
            if (candidate == null || !TryGetRecipeRequirements(candidate, out var recipe)) continue;
            bool canRegisterOne = true;
            foreach (var ingredient in recipe)
            {
                consumption.TryGetValue(ingredient.Key, out int spent);
                long afterUpgrade = (long)_inventory.GetCount(ingredient.Key) - spent;
                if (afterUpgrade < ingredient.Value) { canRegisterOne = false; break; }
            }
            if (canRegisterOne) return false;
        }
        return true;
    }
    void PreviewRecipeUpgrade(int id) => ShowRecipeUpgrade(id, null);

    /// <summary>강화 조건을 다시 읽어 확인 버튼과 안내를 갱신합니다.</summary>
    void ShowRecipeUpgrade(int id, string message)
    {
        if (!_recipes.TryGetValue(id, out var menu) || _view.RecipeUpgradeView == null) return;
        _upgradePreviewRecipeId = id;
        var data = BuildRecipe(menu);
        bool canUpgrade = CanUpgradeRecipeForUI(menu);
        _view.RecipeUpgradeView.Bind(data.Icon, message ?? data.UpgradePrompt, data.BeforeStats,
            data.AfterStats, data.UpgradeCost, canUpgrade);
        _view.RecipeUpgradeView.DisplayIngredients(data.UpgradeIngredients);
    }

    /// <summary>모델에서 성공을 반환하면 실제 강화 결과를 표시합니다.</summary>
    void UpgradeRecipe(int id)
    {
        if (!_recipes.TryGetValue(id, out var menu) || _enforce == null) return;
        if (!CanEditReservation) return;
        // 화면 표시 이후 재료·예약이 바뀔 수 있으므로 실제 요청 직전에도 같은 조건을 확인합니다.
        if (_enforce.CanUpgradeMenu(menu) && WouldExhaustLastRegistrableMenu(menu))
        {
            ShowRecipeUpgrade(id, MinimumMenuMaterialNotice);
            return;
        }
        int beforeLevel=_enforce.GetMenuLevel(menu);
        string beforePrice=_enforce.TryGetReward(menu,out float previousGold,out _)?previousGold.ToString("N0"):"—";
        if (!_enforce.TryUpgradeMenu(menu))
        {
            ShowRecipeUpgrade(id, "강화에 필요한 골드와 재료를 확인하세요.");
            return;
        }
        Refresh();
        _view.RecipeUpgradeView?.Close();
        _view.DisplayRecipeUpgradeResult(new SushiRecipeUpgradeResultView.ResultData
        {
            Icon=menu.Icon,Name=menu.Name,BeforeLevel=beforeLevel,Level=_enforce.GetMenuLevel(menu),BeforeValue=beforePrice,
            AfterValue=_enforce.TryGetReward(menu,out float updatedGold,out _)?updatedGold.ToString("N0"):"—"
        });
        _view.OpenRecipeUpgradeResult();
    }

    /// <summary>서빙·요리 직원의 현재 데이터로 관리 화면을 갱신합니다.</summary>
    void RefreshStaff()
    {
        if (_enforce == null) return;
        var staff = new List<SushiStaffView.StaffData>(2);
        var serving = _shop.Serving?.UpgradeData;
        var cooking = _shop.Cooker?.UpgradeData;
        if (serving != null)
        {
            int level = _enforce.ServingLevel;
            string Stats(int at) => serving.TryGetMoveSpeed(at, out float speed) ? $"이동 속도 {speed:0.##}" : "—";
            staff.Add(BuildStaff(StaffType.Serving, serving.StaffName, "서빙", level,
                serving.MaxLevel, serving.UnlockPlayerLevel, Stats(level), Stats(level + 1),
                serving.CanServeDrink(level) ? "음료 서빙 가능" : $"Lv.{serving.ActiveUnlockLevel} 음료 서빙 해금"));
        }
        if (cooking != null)
        {
            int level = _enforce.CookingLevel;
            string Stats(int at) => cooking.TryGetCookTimeMultiplier(at, out float time) ? $"조리 시간 {time:P0}" : "—";
            staff.Add(BuildStaff(StaffType.Cooking, cooking.StaffName, "조리", level,
                cooking.MaxLevel, cooking.UnlockPlayerLevel, Stats(level), Stats(level + 1),
                level >= cooking.ActiveUnlockLevel ? "추가 조리 가속 적용" : $"Lv.{cooking.ActiveUnlockLevel} 추가 조리 가속 해금"));
        }
        foreach (var item in staff)
        {
            item.ActiveUnlockLevel = item.StaffId == (int)StaffType.Serving ? serving.ActiveUnlockLevel : cooking.ActiveUnlockLevel;
            item.ActiveUnlocked = item.Unlocked && item.BeforeLevel >= item.ActiveUnlockLevel;
            item.ActiveEffectText = item.StaffId == (int)StaffType.Cooking && cooking.ActiveSpeedPercent > 0f ? $"{cooking.ActiveSpeedPercent:0.#}%" : string.Empty;
        }
        _view.DisplayStaff(new SushiStaffView.Presentation { Staff = staff.ToArray(), CanManage = _shop.CanManageStaff });
        RefreshStaffLevelDisplays(!_staffLevelSnapshotPending);
        _staffLevelSnapshotPending = false;
    }

    /// <summary>기존 강화 변경 알림으로 갱신한 현재 레벨만 머리 위 View에 전달합니다.</summary>
    void RefreshStaffLevelDisplays(bool animate)
    {
        Transform carrierRoot = null;
        if (_shop.Serving != null && _shop.Serving.Carrier != null &&
            _carrierDisplays.TryGetValue(_shop.Serving.Carrier, out var carrierView) && carrierView != null)
            carrierRoot = carrierView.transform;
        RefreshStaffLevel(_shop.Serving, StaffType.Serving, _enforce.ServingLevel,
            _shop.Serving?.UpgradeData?.MaxLevel ?? 1, carrierRoot, ref _servingLevelDisplay, animate);
        RefreshStaffLevel(_shop.Cooker, StaffType.Cooking, _enforce.CookingLevel,
            _shop.Cooker?.UpgradeData?.MaxLevel ?? 1, _cookingDisplay != null ? _cookingDisplay.transform : null,
            ref _cookingLevelDisplay, animate);
    }

    /// <summary>직원의 기존 레벨 UI를 재사용하며 처음 연결할 때에는 상승 연출을 생략합니다.</summary>
    void RefreshStaffLevel(MonoBehaviour owner, StaffType type, int level, int maximum,
        Transform avoid, ref SushiStaffLevelView display, bool animate)
    {
        if (owner == null) { if (display != null) display.ResetPresentation(); return; }
        if (display == null || !display.transform.IsChildOf(owner.transform))
        {
            if (display != null) display.ResetPresentation();
            display = EnsureWorldView(owner, _view.StaffLevelPrefab);
            animate = false;
        }
        if (display == null) return;
        display.BindTarget(owner.transform, avoid);
        // 머리 위 전용 아이콘과 현재 강화 레벨을 표시합니다.
        display.Display(_view.StaffLevelIcon(type), level, maximum, animate);
    }

    SushiStaffView.StaffData BuildStaff(StaffType type, string name, string job, int level,
        int maxLevel, int unlockLevel, string stats, string nextStats, string perk)
    {
        // 실제 NPC가 가진 직원 데이터의 설명을 우선 사용하며 기존 임시 문구는 빈 데이터에만 사용합니다.
        global::StaffData profile = type == StaffType.Serving ? _shop.Serving?.StaffData : _shop.Cooker?.StaffData;
        bool unlocked = _shop.CurrentPlayerLevel >= unlockLevel;
        bool hasCost = _enforce.TryGetStaffUpgradeCost(type, out float cost);
        return new SushiStaffView.StaffData
        {
            StaffId = (int)type,
            // 직원 관리에서만 초상이 없는 직원은 공통 임시 초상을 사용합니다.
            Portrait = _view.StaffPortrait(type) != null ? _view.StaffPortrait(type) : _view.StaffPortrait(StaffType.Serving),
            Description = string.IsNullOrWhiteSpace(profile?.Description) ? _view.StaffDescription(type) : profile.Description,
            Name = name,
            Job = job,
            RoleLevel = unlocked ? $"{job} Lv.{level}" : "미해금",
            Unlocked = unlocked,
            CanUpgrade = _enforce.CanUpgradeStaff(type),
            UnlockCondition = $"플레이어 Lv.{unlockLevel} 달성",
            UnlockProgress = $"{_shop.CurrentPlayerLevel}/{unlockLevel}",
            UnlockRatio = Mathf.Clamp01((float)_shop.CurrentPlayerLevel / Mathf.Max(1, unlockLevel)),
            StatRatio = Mathf.Clamp01((float)level / Mathf.Max(1, maxLevel)),
            BeforeLevel = level,
            AfterLevel = Mathf.Min(level + 1, Mathf.Max(level, maxLevel)),
            BeforeStats = stats,
            AfterStats = nextStats,
            Stats = stats,
            Perk = perk,
            Cost = hasCost ? cost.ToString("N0") : "—",
            UpgradeStatus = level >= maxLevel ? "최대 레벨" : string.Empty
        };
    }

    /// <summary>기존 강화표를 읽어 성공 전후의 수치만 전달합니다. 최대 단계의 실제 해금에만 추가 행을 표시합니다.</summary>
    SushiRecipeUpgradeResultView.ResultData BuildStaffUpgradeCompletion(StaffType type,int before,int after)
    {
        var result=new SushiRecipeUpgradeResultView.ResultData{IsStaff=true,Icon=_view.StaffPortrait(type),Name=type==StaffType.Serving?"서빙직원":"조리직원",BeforeLevel=before,Level=after};
        if(type==StaffType.Serving)
        {
            var data=_shop.Serving?.UpgradeData;if(data==null)return result;
            if(!string.IsNullOrWhiteSpace(data.StaffName))result.Name=data.StaffName;
            result.StatIcon=data.PassiveIcon;result.UnlockIcon=data.ActiveIcon;result.StatLabel="이동 속도";
            result.BeforeValue=data.TryGetMoveSpeed(before,out float a)?a.ToString("0.##"):"—";
            result.AfterValue=data.TryGetMoveSpeed(after,out float b)?b.ToString("0.##"):"—";
            result.ShowUnlock=after==data.MaxLevel&&before<data.MaxLevel&&before<data.ActiveUnlockLevel&&data.CanServeDrink(after);
            result.UnlockMessage="음료 서빙 특전 해금!";
        }
        else
        {
            var data=_shop.Cooker?.UpgradeData;if(data==null)return result;
            if(!string.IsNullOrWhiteSpace(data.StaffName))result.Name=data.StaffName;
            result.StatIcon=data.PassiveIcon;result.UnlockIcon=data.ActiveIcon;result.StatLabel="조리 시간";
            result.BeforeValue=data.TryGetCookTimeMultiplier(before,out float a)?a.ToString("P0"):"—";
            result.AfterValue=data.TryGetCookTimeMultiplier(after,out float b)?b.ToString("P0"):"—";
            result.ShowUnlock=after==data.MaxLevel&&before<data.MaxLevel&&before<data.ActiveUnlockLevel&&after>=data.ActiveUnlockLevel;
            result.UnlockMessage="조리 가속 특전 해금!";
        }
        return result;
    }

    void UpgradeStaff(int id)
    {
        if (_enforce == null || (id != (int)StaffType.Serving && id != (int)StaffType.Cooking)) return;
        int beforeLevel=_enforce.GetStaffLevel((StaffType)id);
        if (_enforce.TryUpgrade((StaffType)id))
        {
            RefreshStaff();
            _view.StaffUpgradeView?.Close();
            int level = id == (int)StaffType.Serving ? _enforce.ServingLevel : _enforce.CookingLevel;
            _view.DisplayStaffUpgradeResult(BuildStaffUpgradeCompletion((StaffType)id,beforeLevel,level));
        }
        else
        {
            RefreshStaff();
            _view.StaffView?.DisplaySelectedUpgrade();
        }
    }

    #endregion

    #region 월드 Canvas 생명주기와 표시 알림
    // TODO: 모델 담당자가 변경 알림을 연결한 뒤 Set*NotificationsEnabled(true)로 해당 호환 조회만 해제합니다.
    [SerializeField] bool _cookingOrderNotificationsEnabled;
    [SerializeField] bool _cookingProgressNotificationsEnabled;
    [SerializeField] bool _chairNotificationsEnabled;
    bool _worldEventsSubscribed, _worldPresentationDirty, _cookingOrdersDirty = true;
    CookerNPC _cookingSource;
    readonly Dictionary<GuestNPC, NPCOrderView> _guestDisplays = new();
    readonly Dictionary<SushiCarrier, InteractToGuestView> _carrierDisplays = new();
    readonly Dictionary<ICookingOrderInfo, CookingDisplayStamp> _cookingStamps = new();
    readonly HashSet<ICookingOrderInfo> _seenCookingOrders = new();
    readonly List<ICookingOrderInfo> _removedCookingOrders = new();

    struct CookingDisplayStamp
    {
        public Sushi menu;
        public Sprite icon;
        public bool completed, receivable;
        public bool Matches(ICookingOrderInfo order) => menu == order.Sushi && icon == order.Icon &&
            completed == order.IsCompleted && receivable == order.IsReceivable;
        public CookingDisplayStamp(ICookingOrderInfo order)
        { menu = order.Sushi; icon = order.Icon; completed = order.IsCompleted; receivable = order.IsReceivable; }
    }

    /// <summary>등록 View의 활성화·비활성화 알림을 구독합니다. 활성화 이전에 만들어진 대상도 이후 스냅샷으로 연결합니다.</summary>
    void SubscribeWorldCanvases()
    {
        if (_worldEventsSubscribed) return;
        SushiWorldCanvasPresenter.Available += RegisterWorldCanvas;
        SushiWorldCanvasPresenter.Unavailable += UnregisterWorldCanvas;
        _worldEventsSubscribed = true;
    }

    /// <summary>Presenter가 꺼지면 정적 생명주기 알림과 기존 모델 이벤트를 대칭적으로 해제합니다.</summary>
    void UnsubscribeWorldCanvases()
    {
        if (_worldEventsSubscribed)
        {
            SushiWorldCanvasPresenter.Available -= RegisterWorldCanvas;
            SushiWorldCanvasPresenter.Unavailable -= UnregisterWorldCanvas;
            _worldEventsSubscribed = false;
        }
        foreach (var guest in _guestDisplays.Keys) if (guest != null) guest.OrderStarted -= NotifyGuestDisplayChanged;
        foreach (var carrier in _carrierDisplays.Keys) if (carrier != null) carrier.FoodServed -= HandleFoodServed;
    }

    /// <summary>구버전 씬의 고정 대상을 초기화 때 한 번만 보완합니다. 이후 전체 검색은 하지 않습니다.</summary>
    void BootstrapWorldCanvases()
    {
        var camera = Camera.main;
        var chairs = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Chair>(true))
            .Where(chair => chair.SeatPos != null).ToArray();
        var reference = camera != null ? chairs.OrderBy(chair => camera.WorldToScreenPoint(chair.SeatPos.position).z)
            .ThenBy(chair => Mathf.Abs(chair.SeatPos.position.x)).FirstOrDefault() : chairs.FirstOrDefault();
        _seatUIReference = reference != null ? reference.SeatPos : null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var owner in root.GetComponentsInChildren<GuestNPC>(true)) RegisterWorldTarget(owner);
            foreach (var owner in root.GetComponentsInChildren<CookerNPC>(true)) RegisterWorldTarget(owner);
            foreach (var owner in root.GetComponentsInChildren<SushiCarrier>(true)) RegisterWorldTarget(owner);
            foreach (var owner in root.GetComponentsInChildren<Chair>(true)) RegisterWorldTarget(owner);
        }
    }

    /// <summary>기존 네 종류 대상에 등록 View가 누락된 경우 연결을 보완합니다. 같은 대상은 재사용합니다.</summary>
    public void RegisterWorldTarget(MonoBehaviour owner)
    {
        if (owner == null || owner.gameObject.scene != gameObject.scene) return;
        var binding = owner.GetComponent<SushiWorldCanvasPresenter>();
        if (binding == null) binding = owner.gameObject.AddComponent<SushiWorldCanvasPresenter>();
        binding.BindOwner(owner);
        if (binding.isActiveAndEnabled) RegisterWorldCanvas(binding);
    }

    /// <summary>활성 등록 목록을 한 번 연결합니다. 0.25초 간격의 씬 전체 검색·생성은 제거했습니다.</summary>
    void ConnectWorldDisplays()
    {
        if (_groundHero == null) _groundHero = FindSceneModel<GroundHero>();
        foreach (var binding in SushiWorldCanvasPresenter.GetActiveViews()) RegisterWorldCanvas(binding);
        _worldPresentationDirty = true;
        _cookingOrdersDirty = true;
    }

    /// <summary>기존 Canvas를 우선 사용하고 구형 씬에 UI가 없는 경우에만 한 번 생성합니다.</summary>
    T EnsureWorldView<T>(MonoBehaviour owner, T prefab) where T : Component
    {
        var display = owner.GetComponentInChildren<T>(true);
        if (display != null || prefab == null) return display;
        display = Instantiate(prefab, owner.transform, false);
        _createdWorldDisplays.Add(display.gameObject);
        return display;
    }

    /// <summary>한 대상의 Canvas만 연결합니다. UI 존재 여부와 데이터 연결을 구분해 기존 운반 View도 빠짐없이 Bind합니다.</summary>
    void RegisterWorldCanvas(SushiWorldCanvasPresenter binding)
    {
        if (!_initialized || !isActiveAndEnabled || binding == null || binding.Owner == null || binding.gameObject.scene != gameObject.scene) return;
        switch (binding.Owner)
        {
            case GuestNPC guest:
                // 구형 손님 프리팹의 주문 UI는 보존하되 의자 UI와 중복 표시하지 않습니다.
                foreach (var legacyView in guest.GetComponentsInChildren<NPCOrderView>(true))
                    if (legacyView.gameObject.activeSelf) legacyView.gameObject.SetActive(false);
                if (!_seatGuests.Contains(guest)) _seatGuests.Add(guest);
                guest.OrderStarted -= NotifyGuestDisplayChanged;
                guest.OrderStarted += NotifyGuestDisplayChanged;
                BindGuestSeat(guest);
                break;
            case SushiCarrier carrier:
                var carrierView = EnsureWorldView(carrier, _view.CarrierDisplayPrefab);
                if (carrierView == null) return;
                _carrierDisplays[carrier] = carrierView;
                var hero = carrier.GetComponentInParent<GroundHero>();
                if (hero != null) _groundHero = hero;
                carrierView.BindCarrier(carrier);
                carrier.FoodServed -= HandleFoodServed;
                carrier.FoodServed += HandleFoodServed;
                break;
            case CookerNPC cooker:
                if (_shop == null || cooker != _shop.Cooker) return;
                var cookingView = EnsureWorldView(cooker, _view.CookingDisplayPrefab);
                if (cookingView == null) return;
                if (_cookingDisplay != cookingView || _cookingSource != cooker) _cookingStamps.Clear();
                _cookingSource = cooker; _cookingDisplay = cookingView; _cookingOrdersDirty = true;
                break;
            case Chair chair:
                if (!_chairDisplays.TryGetValue(chair, out var chairView) || chairView == null)
                {
                    chairView = chair.GetComponentInChildren<SushiChairView>(true);
                    if (chairView == null && _usePreplacedChairUI)
                    {
                        Debug.LogError("의자 하위에 미리 배치한 SushiChairView가 없습니다. 씬 연결을 확인하세요.", chair);
                        return;
                    }
                    if (chairView == null && _view.ChairCleaningPrefab != null)
                    {
                        // 의자의 중립 SeatUI 부모에서 배율을 보정한 뒤 고정합니다.
                        chairView = Instantiate(_view.ChairCleaningPrefab);
                        chairView.name = "ChairUI_" + chair.name;
                        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chairView.gameObject, gameObject.scene);
                        _createdWorldDisplays.Add(chairView.gameObject);
                    }
                    if (chairView == null) return;
                    _chairDisplays[chair] = chairView;
                }
                chairView.BindSeat(chair, _seatUIReference);
                NotifyChairDisplayChanged(chair);
                break;
        }
        _worldPresentationDirty = true;
    }

    /// <summary>대상이 풀로 돌아갈 때 Canvas는 유지하고 연결·표시만 해제합니다.</summary>
    void UnregisterWorldCanvas(SushiWorldCanvasPresenter binding)
    {
        if (binding == null) return;
        switch (binding.Owner)
        {
            case GuestNPC guest:
                guest.OrderStarted -= NotifyGuestDisplayChanged;
                if (_guestDisplays.TryGetValue(guest, out var guestView) && guestView != null)
                    guestView.GetComponentInParent<SushiChairView>()?.ReleaseGuest(guest);
                _guestDisplays.Remove(guest); _seatGuests.Remove(guest); break;
            case SushiCarrier carrier:
                carrier.FoodServed -= HandleFoodServed;
                if (_carrierDisplays.TryGetValue(carrier, out var carrierView) && carrierView != null) carrierView.Hide();
                _carrierDisplays.Remove(carrier);
                if (_groundHero != null && _groundHero.SushiCarrier == carrier) _groundHero = null;
                break;
            case CookerNPC cooker:
                if (_cookingSource != cooker) break;
                if (_cookingDisplay != null) _cookingDisplay.ResetSlots();
                _cookingSource = null; _cookingDisplay = null; _cookingStamps.Clear(); _cookingOrdersDirty = true; break;
            case Chair chair:
                if (_chairDisplays.TryGetValue(chair, out var chairView) && chairView != null) chairView.Hide();
                // 독립 Canvas는 다음 활성화 때 재사용합니다.
                break;
        }
    }

    /// <summary>손님 주문·상태·만족도 이벤트 연결 지점입니다. 모델은 변경하지 않습니다.</summary>
    public void NotifyGuestDisplayChanged(GuestNPC guest)
    {
        if (guest == null) return;
        BindGuestSeat(guest);
        if (_guestDisplays.TryGetValue(guest, out var display) && display != null) display.RefreshFromModel();
    }

    /// <summary>손님의 주문 표시 내용만 해당 의자 뷰에 연결합니다. Canvas는 의자 뷰가 소유하며 사전 배치된 씬에서는 생성하지 않습니다.</summary>
    void BindGuestSeat(GuestNPC guest)
    {
        _guestDisplays.TryGetValue(guest, out var previous);
        var seat = guest.Seat;
        if (seat == null)
        {
            if (previous != null) previous.GetComponentInParent<SushiChairView>()?.ReleaseGuest(guest);
            _guestDisplays[guest] = null;
            return;
        }
        if (!_chairDisplays.TryGetValue(seat, out var chairView) || chairView == null)
        {
            RegisterWorldTarget(seat);
            _chairDisplays.TryGetValue(seat, out chairView);
        }
        if (chairView == null) return;
        if (previous != null && previous != chairView.OrderView)
            previous.GetComponentInParent<SushiChairView>()?.ReleaseGuest(guest);
        chairView.BindGuest(guest);
        _guestDisplays[guest] = chairView.OrderView;
    }

    /// <summary>등록된 손님의 의자 연결만 확인하며 주문·청소 상태와 위치 보정은 각 담당자에게 맡깁니다.</summary>
    void RefreshGuestSeatBindings()
    {
        // TODO(UI-EVENT-09): GuestNPC.SeatChanged(guest, previous, current) 알림이 제공되면
        // NotifyGuestDisplayChanged(guest)에서 BindGuestSeat(guest)를 호출하고 이 임시 연결 조회를 제거합니다.
        // 발행 시점: 좌석 예약·변경·반환·풀 재사용. 전체 씬 탐색이나 UI 생성 반복은 하지 않습니다.
        for (int i = _seatGuests.Count - 1; i >= 0; i--)
        {
            var guest = _seatGuests[i];
            if (guest == null) { _seatGuests.RemoveAt(i); continue; }
            BindGuestSeat(guest);
        }
    }
    /// <summary>운반 음식·전달·폐기 상태 이벤트 연결 지점입니다.</summary>
    public void NotifyCarrierDisplayChanged(SushiCarrier carrier)
    { if (carrier != null && _carrierDisplays.TryGetValue(carrier, out var display) && display != null) display.RefreshFromModel(); }
    /// <summary>기존 전달 완료 이벤트를 재사용하여 관련 표시를 갱신합니다.</summary>
    void HandleFoodServed(GuestNPC guest)
    {
        NotifyGuestDisplayChanged(guest);
        foreach (var display in _carrierDisplays.Values) if (display != null) display.RefreshFromModel();
        NotifyCookingOrdersChanged();
    }
    /// <summary>손님의 모든 표시 이벤트 연결이 완료된 뒤에만 임시 조회를 끕니다.</summary>
    public void SetGuestNotificationsEnabled(GuestNPC guest, bool enabled)
    { if (guest != null && _guestDisplays.TryGetValue(guest, out var display) && display != null) display.SetExternalUpdates(enabled); }
    /// <summary>운반자의 모든 표시 이벤트 연결이 완료된 뒤에만 임시 조회를 끕니다.</summary>
    public void SetCarrierNotificationsEnabled(SushiCarrier carrier, bool enabled)
    { if (carrier != null && _carrierDisplays.TryGetValue(carrier, out var display) && display != null) display.SetExternalUpdates(enabled); }
    /// <summary>추가·완료·수령·취소·주문자 인계·수령 가능 상태 변경 시 호출합니다.</summary>
    public void NotifyCookingOrdersChanged() { _cookingOrdersDirty = true; }
    /// <summary>모델 알림 연결 완료 후 목록 비교만 끕니다. 첫 스냅샷은 다시 반영합니다.</summary>
    public void SetCookingOrderNotificationsEnabled(bool enabled)
    { _cookingOrderNotificationsEnabled = enabled; _cookingStamps.Clear(); _cookingOrdersDirty = true; }
    /// <summary>진행도 알림 연결 완료 후 진행률 조회만 끕니다.</summary>
    public void SetCookingProgressNotificationsEnabled(bool enabled) { _cookingProgressNotificationsEnabled = enabled; }
    /// <summary>조리 진행 이벤트가 전달한 수치만 해당 슬롯에 반영합니다.</summary>
    public void NotifyCookingProgressChanged(ICookingOrderInfo order, float progress)
    { if (_cookingDisplay != null) _cookingDisplay.DisplayOrderProgress(order, progress); }
    /// <summary>잔반·청소·상호작용 가능 상태의 모든 알림을 연결한 뒤에만 기존 조회를 끕니다.</summary>
    public void SetChairNotificationsEnabled(bool enabled) { _chairNotificationsEnabled = enabled; RefreshChairDisplays(); }

    /// <summary>아직 없는 모델 이벤트를 보완하는 읽기 전용 비교입니다. 바뀐 경우에만 전체 슬롯을 동기화합니다.</summary>
    void RefreshCookingDisplays()
    {
        // TODO(UI-EVENT-04 / 요리사 참조 임시 확인): SushiManager.CookerChanged(CookerNPC) 알림이 필요합니다(제안 계약, 아직 없음).
        // 연결할 Presenter 함수: HandleCookerChanged(CookerNPC) 신규 구현 필요. 이전 대상 해제·슬롯 정리·null 처리와 새 대상 RegisterWorldTarget을 포함합니다.
        // 초기 요리사 연결과 교체·해제 알림이 모두 보장된 뒤 이 참조 비교 블록을 제거합니다. 기존 SetCooking* 플래그로는 해제되지 않습니다.
        if (_shop != null && _shop.Cooker != _cookingSource && _shop.Cooker != null)
        {
            var binding = _shop.Cooker.GetComponent<SushiWorldCanvasPresenter>();
            if (binding != null && binding.isActiveAndEnabled) RegisterWorldCanvas(binding);
        }
        if (_cookingSource == null || _cookingDisplay == null || !_cookingDisplay.isActiveAndEnabled) return;
        // TODO(UI-EVENT-05 / 조리 목록·상태 임시 비교): CookerNPC.OrdersChanged 알림이 필요합니다(추가 제안).
        // 연결할 Presenter 함수: NotifyCookingOrdersChanged(). 접수·완료·수령·취소·무효 제거·주문자 인계·IsReceivable 변경 후 발행해야 합니다.
        // 모두 연결한 뒤 SetCookingOrderNotificationsEnabled(true)로 끄고 검증 후 이 비교 블록과 전용 기록을 제거합니다.
        // 아래 dirty/NeedsOrderSync에 따른 DisplayOrders 호출과 최초·재활성화 스냅샷은 유지합니다. 진행률 알림은 별도입니다.
        if (!_cookingOrderNotificationsEnabled)
        {
            _seenCookingOrders.Clear();
            void Observe(IReadOnlyList<ICookingOrderInfo> orders)
            {
                if (orders == null) return;
                foreach (var order in orders)
                {
                    if (order == null || order.Sushi == null || !_seenCookingOrders.Add(order)) continue;
                    if (!_cookingStamps.TryGetValue(order, out var stamp) || !stamp.Matches(order))
                    { _cookingStamps[order] = new CookingDisplayStamp(order); _cookingOrdersDirty = true; }
                }
            }
            Observe(_cookingSource.CookingOrders); Observe(_cookingSource.CompletedOrders);
            _removedCookingOrders.Clear();
            foreach (var order in _cookingStamps.Keys) if (!_seenCookingOrders.Contains(order)) _removedCookingOrders.Add(order);
            foreach (var order in _removedCookingOrders) { _cookingStamps.Remove(order); _cookingOrdersDirty = true; }
        }
        if (_cookingOrdersDirty || _cookingDisplay.NeedsOrderSync)
        {
            _cookingDisplay.DisplayOrders(_cookingSource.CookingOrders, _cookingSource.CompletedOrders);
            _cookingOrdersDirty = false;
        }
        // TODO(UI-EVENT-06 / 조리 진행률 임시 조회): CookerNPC.CookingProgressChanged(order, progress) 알림이 필요합니다(추가 제안, progress는 0~1).
        // 연결할 Presenter 함수: NotifyCookingProgressChanged(ICookingOrderInfo order, float progress). 실제 모델 진행률 갱신 후 주문별로 전달합니다.
        // 연결 후 SetCookingProgressNotificationsEnabled(true)로 끄고 검증 후 이 조회 루프만 제거합니다. 완료·취소는 OrdersChanged로 따로 확정합니다.
        if (!_cookingProgressNotificationsEnabled)
            foreach (var order in _cookingSource.CookingOrders)
                if (order != null) _cookingDisplay.DisplayOrderProgress(order, order.CookingProgress);
    }

    /// <summary>초기화·재활성화 직후 한 번 현재 상태를 반영하여 앞서 발생한 이벤트를 놓치지 않습니다.</summary>
    void RefreshRegisteredWorldViews()
    {
        _worldPresentationDirty = false;
        foreach (var display in _guestDisplays.Values) if (display != null) display.RefreshFromModel();
        foreach (var display in _carrierDisplays.Values) if (display != null) display.RefreshFromModel();
        RefreshChairDisplays();
    }

    /// <summary>잔반 상태와 실제 청소 세션의 호환용 조회입니다. 위치·카메라 정렬은 View가 담당합니다.</summary>
    void RefreshChairDisplays()
    {
        _removedChairs.Clear();
        foreach (var pair in _chairDisplays)
        {
            if (pair.Key == null || pair.Value == null)
            {
                if (pair.Value != null)
                {
                    // 씬에 배치한 UI의 생명주기는 의자에 맡기고, 보완 생성한 표시만 직접 정리합니다.
                    if (_createdWorldDisplays.Contains(pair.Value.gameObject)) Destroy(pair.Value.gameObject);
                    else pair.Value.Hide();
                }
                _removedChairs.Add(pair.Key); continue;
            }
            NotifyChairDisplayChanged(pair.Key);
        }
        foreach (var chair in _removedChairs) _chairDisplays.Remove(chair);
    }

    /// <summary>의자 상태·청소 진행·상호작용 대상 변경 알림이 호출할 표시 진입점입니다.</summary>
    public void NotifyChairDisplayChanged(Chair chair)
    {
        if (chair == null || !_chairDisplays.TryGetValue(chair, out var display) || display == null) return;
        bool uncleaned = chair.isActiveAndEnabled && chair.IsUncleaned;
        bool canClean = uncleaned && _groundHero != null && chair.CanInteract(_groundHero);
        float progress = chair.CleaningTime > 0f ? chair.CleaningElapsed / chair.CleaningTime : 0f;
        display.Display(IjakayaView.ResolveMenuIcon(chair.ServedMenu, _view), uncleaned, progress, canClean);
    }

    /// <summary>이 Presenter가 구형 씬에서 보완 생성한 UI만 정리합니다. 프리팹에 배치된 Canvas는 파괴하지 않습니다.</summary>
    void OnDestroy()
    {
        UnsubscribeWorldCanvases();
        _staffLevelSnapshotPending = true;
        if (_servingLevelDisplay != null) _servingLevelDisplay.ResetPresentation();
        if (_cookingLevelDisplay != null) _cookingLevelDisplay.ResetPresentation();
        foreach (var display in _createdWorldDisplays) if (display != null) Destroy(display);
        _createdWorldDisplays.Clear();
        _guestDisplays.Clear(); _seatGuests.Clear(); _carrierDisplays.Clear(); _chairDisplays.Clear();
    }
    #endregion


    #region 정산 연결 지점
    /// <summary>담당 GameManager가 호출합니다. 종료 안내 후 정산을 한 번만 표시합니다.</summary>
    public void PresentSettlement()
    {
        if (!_initialized || !_shop.IsMenuConfirmed || _shop.RemainingOperationTime > 0f || _settlementRequested) return;
        _settlementRequested = true;
        int ticket = ++_settlementTicket;
        _view.CloseManagement();
        _view.SetPreparationVisible(false);
        void OpenResult()
        {
            if (!isActiveAndEnabled || ticket != _settlementTicket || _settlementReady) return;
            _settlementReady = true;
            _view.OpenBusinessResult();
            GameSfx.Play(GameSoundId.Settlement);
        }
        var style = _view.GetComponent<SushiRestaurantStyleView>();
        // 종료 상승의 끝부분에서 열고, 완료 콜백도 남겨 연출이 생략되어도 정산을 복원합니다.
        // OpenResult의 기존 티켓·중복 검사가 두 호출 중 최초 한 번만 허용합니다.
        if (style != null && style.isActiveAndEnabled) style.ShowBusinessClosing(OpenResult, OpenResult);
        else OpenResult();
    }

    /// <summary>기존 모델의 표시값만 읽으며 보상이나 통계는 변경하지 않습니다.</summary>
    public bool TryBuildSettlement(out SushiBusinessResultView.Presentation data)
    {
        data = null;
        if (!_initialized || _shop == null) return false;
        data = new SushiBusinessResultView.Presentation
        {
            TotalSales = _shop.TotalSalesGold,
            RunningCost = _shop.AccumulatedOperationCost,
            NetProfit = _shop.TotalSalesGold - _shop.AccumulatedOperationCost,
            TotalGuests = _shop.TotalSpawnedGuestCount,
            SatisfiedGuests = _shop.SatisfiedGuestCount,
            // 미서빙 퇴장만 표시하며 잔류 손님이나 강제 종료를 합치지 않습니다.
            LeftGuests = _shop.UnservedGuestCount,
            MenuSales = _shop.MenuSalesResults.Where(item => item != null).Select(item => new SushiBusinessResultView.MenuSaleData
            {
                Icon = IjakayaView.ResolveMenuIcon(item.Menu, _view),
                Name = item.Menu != null ? item.Menu.Name : "메뉴 정보 없음",
                Count = item.SoldCount, Amount = item.EarnedGold, Registered = true
            }).ToArray(),
            StaffActivities = new[]
            {
                new SushiBusinessResultView.StaffActivityData { Portrait = _view.StaffPortrait(StaffType.Serving), Activity = (_shop.Serving != null ? _shop.Serving.ServedCount : 0).ToString() },
                new SushiBusinessResultView.StaffActivityData { Portrait = _view.StaffPortrait(StaffType.Cooking), Activity = (_shop.Cooker != null ? _shop.Cooker.CompletedCookingCount : 0).ToString() }
            }
        };
        return true;
    }

    /// <summary>종료 안내 중 입력과 중복 확인을 막고 기존 다음 날 이동만 호출합니다.</summary>
    void ConfirmSettlement()
    {
        if (!_settlementReady || _settlementConfirmed || !_shop.IsMenuConfirmed || _shop.RemainingOperationTime > 0f) return;
        _settlementConfirmed = true;
        if (GameManager.Instance == null || !GameManager.Instance.CompleteNight()) _settlementConfirmed = false;
    }
    #endregion
}
