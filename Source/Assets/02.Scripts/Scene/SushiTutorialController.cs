using System;
using System.Collections;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SushiTutorialController : MonoBehaviour
{
    const int StarterPortions = 3;
    enum ServiceStep { Food, Discard, Drink, Cleaning, Complete }

    [Header("튜토리얼 시작 메뉴")]
    [SerializeField] Sushi _starterRecipe;
    [Header("튜토리얼 손님 및 지정 음료")]
    [SerializeField] GuestSpawner _guestSpawner;
    [SerializeField] GuestNPC _practiceGuestPrefab;
    [SerializeField] Transform _guestSpawnPoint;
    [SerializeField] Transform _guestExitPoint;
    [SerializeField] Chair[] _practiceSeats;
    [SerializeField] Sushi _practiceDrink;
    [SerializeField] GuestReactionEffects _reactionEffects;
    [Header("안내 및 대화 뷰")]
    [SerializeField] TutorialView _guidancePrefab;
    [SerializeField] TalkView _dialogueView;
    [Header("순서별 튜토리얼 대화")]
    [SerializeField] TutorialDialogueData _menuDialogue;
    [SerializeField] TutorialDialogueData _upgradeDialogue;
    [SerializeField] TutorialDialogueData _staffUpgradeDialogue;
    [SerializeField] TutorialDialogueData _openingDialogue;
    [SerializeField] TutorialDialogueData _servingDialogue;
    [SerializeField] TutorialDialogueData _drinkDialogue;
    [SerializeField] TutorialDialogueData _cleaningDialogue;
    [SerializeField] TutorialDialogueData _settlementDialogue;
    [Header("실습 완료 후 대화")]
    [SerializeField] TutorialDialogueData _servingCompletedDialogue;
    [SerializeField] TutorialDialogueData _discardDialogue;
    [SerializeField] TutorialDialogueData _drinkCompletedDialogue;
    [SerializeField] TutorialDialogueData _cleaningCompletedDialogue;
    [Header("메뉴 및 영업 화면 연결")]
    [SerializeField] Button _todayMenuButton;
    [SerializeField] Button _startBusinessButton;
    [SerializeField] GameObject _recipeUpgradePage;

    [Header("튜토리얼 메뉴 선택·등록 강조")]
    [SerializeField] IjakayaView _managementView;
    [SerializeField] Button _registerRecipeButton;
    [SerializeField] Button _quantityConfirmButton;
    [SerializeField] Button _menuCloseButton;
    [Header("튜토리얼 요리 강화 강조")]
    [SerializeField] Button _recipeUpgradeTab;
    [SerializeField] Button _recipeUpgradeButton;
    [SerializeField] Button _recipeUpgradeConfirmButton;
    [SerializeField] Button _recipeUpgradeResultButton;
    [Header("튜토리얼 직원 강화 강조")]
    [SerializeField] Button _staffTab;
    [SerializeField] Button _cookingStaffButton;
    [SerializeField] Button _staffUpgradeButton;
    [SerializeField] Button _staffUpgradeConfirmButton;
    [SerializeField] Button _staffUpgradeResultButton;
    [Header("단계 중 다른 입력을 잠글 UI")]
    [SerializeField] Component[] _lessonInputRoots;

    readonly System.Collections.Generic.Dictionary<Selectable, (CanvasGroup group, bool added, bool interactable, Navigation navigation)> _lessonInputs = new();
    readonly System.Collections.Generic.Dictionary<UIKeyboardFocusView, bool> _keyboardFocusStates = new();
    Button _lessonButton;
    bool _lessonClicked;

    SushiMenuBookView _menuBook;
    GameManager _manager;
    SushiManager _shop;
    SushiListManager _menus;
    Sushi _practiceFood;
    SushiFoodDiscarder _discarder;
    SushiDrinkMinigameCoordinator _drinkMinigame;
    IjakayaPresenter _presenter;
    GroundHero _hero;
    SushiCarrier _carrier;
    SushiInteractor _holdInteractor;
    Chair _drinkSeat;
    Chair _foodSeat;
    readonly System.Collections.Generic.List<Chair> _lockedCleaningSeats = new();
    GuestNPC _practiceGuest;
    Sushi _expectedOrder;
    bool _orderReceived;
    bool _spawnerWasEnabled;
    bool _servingWasEnabled;
    bool _discarderWasEnabled;
    TalkView _talk;
    TutorialView _guidance;
    Coroutine _routine;
    RectTransform _worldMarker;
    Transform _markedTarget;
    Camera _camera;
    string _servingInstruction;
    string _lastServiceInstruction;
    bool _active;
    bool _businessAllowed;
    bool _businessButtonWasInteractable;
    ServiceStep _serviceStep;
    bool _settlementStarted;
    bool _ownsDialogue;
    bool _servicePausedForDialogue;
    bool _holdInteractorWasEnabled;
    float _serviceTimeScale;

    internal bool IsDialogue => _ownsDialogue && _talk != null && _talk.IsTalking;
    internal bool AllowsBusinessStart => !_active || _businessAllowed;
    internal bool AllowsWorldInteract => !_active || _serviceStep != ServiceStep.Discard || _carrier.CurrentSushi == null;

    internal void Begin(SushiPlayScene scene, GameManager manager, SushiManager shop, IjakayaPresenter presenter)
    {
        if (_active || !isActiveAndEnabled || manager.PlayerProgress == null ||
            manager.PlayerProgress.SushiTutorialCompleted) return;

        var settings = scene.GetComponent<TutorialSceneSettings>();
        if (settings == null || !settings.isActiveAndEnabled || settings.CurrentChapter != TutorialSceneSettings.Chapter.Sushi) return;

        _menuBook = null;
        foreach (var root in scene.gameObject.scene.GetRootGameObjects())
        {
            _menuBook = root.GetComponentInChildren<SushiMenuBookView>(true);
            if (_menuBook != null) break;
        }
        if (_menuBook == null)
        {
            Debug.LogWarning("초밥집 튜토리얼의 메뉴 책 연결을 확인해주세요.", this);
            return;
        }
        var talk = _dialogueView;
        var hero = scene.SceneHero as GroundHero;
        var menus = shop != null ? shop.GetComponent<SushiListManager>() : null;
        var practiceFood = menus?.BasicProvidedMenu.FirstOrDefault(menu => menu != null && menu.Type == MenuType.Food);
        var discarder = hero != null ? hero.GetComponent<SushiFoodDiscarder>() : null;
        var drinkMinigame = hero != null ? hero.GetComponent<SushiDrinkMinigameCoordinator>() : null;
        if (talk == null || hero == null || hero.SushiCarrier == null || shop == null || presenter == null ||
            _guidancePrefab == null || _todayMenuButton == null || _startBusinessButton == null ||
            _recipeUpgradePage == null || _guestSpawner == null || _practiceGuestPrefab == null ||
            _guestSpawnPoint == null || _guestExitPoint == null ||
            _practiceSeats == null || _practiceSeats.Length < 2 || hero.GetComponentInChildren<SushiInteractor>() == null ||
            _starterRecipe == null || _practiceDrink == null || _practiceDrink.Type != MenuType.Drink ||
            practiceFood == null || discarder == null || drinkMinigame == null || _reactionEffects == null)
        {
            Debug.LogWarning("초밥집 튜토리얼의 화면과 씬 참조를 확인해주세요.", this);
            return;
        }

        if (_managementView == null || _registerRecipeButton == null ||
            _quantityConfirmButton == null || _menuCloseButton == null || _recipeUpgradeTab == null ||
            _recipeUpgradeButton == null || _recipeUpgradeConfirmButton == null || _recipeUpgradeResultButton == null ||
            _staffTab == null || _cookingStaffButton == null || _staffUpgradeButton == null ||
            _staffUpgradeConfirmButton == null || _staffUpgradeResultButton == null)
        {
            Debug.LogWarning("초밥집 튜토리얼의 단계별 강조 버튼 연결을 확인해주세요.", this);
            return;
        }

        bool grantNeeded = !manager.PlayerProgress.SushiTutorialIngredientsGranted;
        if (grantNeeded && !TryGrantIngredients(manager.Inventory, menus))
        {
            Debug.LogWarning("초밥집 튜토리얼의 시작 레시피와 인벤토리를 확인해주세요.", this);
            return;
        }

        // GameManager는 InitializeScene 직후 재료와 지급 여부를 같은 슬롯 파일에 저장합니다.
        if (grantNeeded) manager.PlayerProgress.MarkSushiTutorialIngredientsGranted();

        _manager = manager;
        _shop = shop;
        _menus = menus;
        _practiceFood = practiceFood;
        _discarder = discarder;
        _discarderWasEnabled = discarder.enabled;
        discarder.enabled = false;
        _drinkMinigame = drinkMinigame;
        _presenter = presenter;
        _hero = hero;
        _carrier = hero.SushiCarrier;
        _holdInteractor = hero.GetComponentInChildren<SushiInteractor>();
        _talk = talk;
        _guidance = Instantiate(_guidancePrefab);
        _guidance.transform.localScale = Vector3.one;
        _businessButtonWasInteractable = _startBusinessButton.interactable;
        _startBusinessButton.interactable = false;
        _carrier.FoodServed += OnFoodServed;
        _drinkMinigame.DrinkServed += OnDrinkServed;
        if (_holdInteractor != null) _holdInteractor.HoldInteractionCompleted += OnHoldInteractionCompleted;
        // 일반 영업 스포너와 자동 서빙은 이 튜토리얼 실행 동안만 멈춘다.
        _spawnerWasEnabled = _guestSpawner.enabled;
        _guestSpawner.enabled = false;
        _servingWasEnabled = _shop.Serving != null && _shop.Serving.enabled;
        if (_shop.Serving != null) _shop.Serving.enabled = false;
        _shop.RegisterSeats(_practiceSeats);
        LockPracticeCleaning();
        _active = true;
        _businessAllowed = false;
        _serviceStep = ServiceStep.Food;
        _drinkSeat = null;
        _foodSeat = null;
        _settlementStarted = false;
        _routine = StartCoroutine(Run(grantNeeded));
    }

    bool TryGrantIngredients(Inventory inventory, SushiListManager menus)
    {
        if (inventory == null || menus == null || _starterRecipe == null ||
            !menus.IsSelectableTodayMenu(_starterRecipe) ||
            !menus.TryGetIngredientAmounts(_starterRecipe, out var ingredients)) return false;

        var portions = new System.Collections.Generic.Dictionary<ItemData, int>();
        foreach (var ingredient in ingredients)
        {
            if (ingredient.Value > int.MaxValue / StarterPortions) return false;
            portions.Add(ingredient.Key, ingredient.Value * StarterPortions);
        }
        return inventory.TryAddItems(portions);
    }

    IEnumerator Run(bool granted)
    {
        yield return null;
        LockLessonInputs();
        yield return GuideToMenu(_menuDialogue, 0, "오늘의 메뉴 버튼을 눌러 메뉴 등록 화면을 열어보세요.");
        if (!HasStarterMenu())
        {
            _managementView.ClearMenuSlotSelection();
            yield return SelectStarterRecipe();
            while (!HasStarterMenu())
            {
                yield return WaitForLessonClick(_registerRecipeButton, "등록 버튼을 눌러 수량을 확인해보세요.",
                    () => _managementView.QuantityView.gameObject.activeInHierarchy);
                yield return WaitForLessonClick(_quantityConfirmButton, "연습할 초밥 1접시를 등록해보세요.", HasStarterMenu);
            }
        }

        _guidance.HideInstruction();
        yield return RunRecipeUpgrade();
        yield return RunStaffUpgrade();
        yield return WaitForManagementClose();

        _guidance.HideInstruction();
        yield return PlayDialogue(_openingDialogue);
        _businessAllowed = true;
        _startBusinessButton.interactable = true;
        yield return WaitForLessonClick(_startBusinessButton, "영업 시작을 눌러 첫 손님을 맞이하세요.");
        while (!_shop.IsMenuConfirmed) yield return null;
        RestoreLessonInputs();
        yield return RunService();
        _routine = null;
    }

    IEnumerator RunService()
    {
        _servingInstruction = "주문한 초밥을 손님에게 가져가 {Interact}키를 눌러 전달하세요.";
        _camera = Camera.main;
        yield return RunPracticeOrder(_starterRecipe, _servingDialogue, ServiceStep.Food);
        if (_serviceStep != ServiceStep.Discard || !_shop.IsOpen) yield break;

        yield return PlayServiceDialogue(_servingCompletedDialogue);
        yield return RunDiscardLesson();
        if (_serviceStep != ServiceStep.Drink || !_shop.IsOpen) yield break;

        yield return RunPracticeOrder(_practiceDrink, _drinkDialogue, ServiceStep.Drink);
        if (_serviceStep != ServiceStep.Cleaning || !_shop.IsOpen) yield break;

        // 응답 결과를 닫고 미니게임이 조작을 돌려준 뒤에 대화를 연다.
        HideGuidance();
        while (_drinkMinigame.IsBusy && _shop.IsOpen) yield return null;
        if (!_shop.IsOpen) yield break;
        yield return PlayServiceDialogue(_drinkCompletedDialogue);
        if (!_shop.IsOpen) yield break;

        _guidance.ShowSentenceInstruction("손님이 식사를 마치고 자리를 비우면 청소를 시작합니다.");
        // 청소를 잠근 동안에는 CanHoldInteract가 false이므로 실제 퇴장을 기다린다.
        while (_shop.IsOpen && _practiceGuest != null)
            yield return null;
        if (!_shop.IsOpen || _drinkSeat == null) yield break;
        yield return PlayServiceDialogue(_cleaningDialogue);
        RestorePracticeCleaning();
        while (_serviceStep == ServiceStep.Cleaning && _shop.IsOpen)
        {
            ShowServiceInstruction();
            yield return null;
        }
        HideGuidance();
        if (_serviceStep != ServiceStep.Complete || !_shop.IsOpen) yield break;
        yield return PlayServiceDialogue(_cleaningCompletedDialogue);
        // 정해진 실습을 마친 뒤에만 기존 정산 경로로 넘어간다.
        if (_serviceStep == ServiceStep.Complete && _shop.IsOpen) _shop.EndBusiness();
    }

    void LockPracticeCleaning()
    {
        foreach (var seat in _practiceSeats)
        {
            if (seat == null || !seat.enabled) continue;
            // Chair의 예약/착석은 유지하고 CanInteract/CanHoldInteract만 차단한다.
            _lockedCleaningSeats.Add(seat);
            seat.enabled = false;
        }
    }

    void RestorePracticeCleaning()
    {
        foreach (var seat in _lockedCleaningSeats)
            if (seat != null) seat.enabled = true;
        _lockedCleaningSeats.Clear();
    }

    IEnumerator RunPracticeOrder(Sushi menu, TutorialDialogueData dialogue, ServiceStep step)
    {
        while (_serviceStep == step && _shop.IsOpen)
        {
            // 실습 접시가 팔리기 전에 소진돼도 기본 메뉴로 다시 연습할 수 있다.
            var order = step == ServiceStep.Food && !_menus.GetOrderableMenu().Contains(menu) ? _practiceFood : menu;
            yield return WaitForPracticeOrder(order);
            if (!_shop.IsOpen || !_orderReceived) yield break;
            yield return PlayServiceDialogue(dialogue);
            dialogue = null;
            _lastServiceInstruction = null;
            while (_serviceStep == step && _practiceGuest != null && _shop.IsOpen)
            {
                ShowServiceInstruction();
                yield return null;
            }
        }
    }

    IEnumerator WaitForPracticeOrder(Sushi menu)
    {
        _orderReceived = false;
        if (_practiceGuest != null) _guidance.ShowSentenceInstruction("앞 손님이 퇴장하면 다음 실습을 시작합니다.");
        while (_practiceGuest != null && _shop.IsOpen) yield return null;
        if (!_shop.IsOpen) yield break;
        if (!TrySpawnPracticeGuest(menu))
        {
            _guidance.ShowSentenceInstruction($"{menu.Name} 실습 주문을 준비할 수 없습니다.");
            yield break;
        }

        _guidance.ShowSentenceInstruction("손님이 자리에 앉아 주문할 때까지 기다리세요.");
        while (_practiceGuest != null && !_orderReceived && _shop.IsOpen) yield return null;
    }

    IEnumerator RunDiscardLesson()
    {
        while (_shop.IsOpen)
        {
            yield return WaitForPracticeOrder(_practiceFood);
            if (!_shop.IsOpen || !_orderReceived) yield break;
            _lastServiceInstruction = null;
            while (_practiceGuest != null && _practiceGuest.IsWaiting && _shop.IsOpen)
            {
                ShowServiceInstruction();
                yield return null;
            }
            if (!_shop.IsOpen) yield break;
            if (_practiceGuest != null && _practiceGuest.State == GuestState.Eating) continue;

            HideGuidance();
            while (_practiceGuest != null && _practiceGuest.State == GuestState.Angry && _shop.IsOpen)
                yield return null;
            if (!_shop.IsOpen) yield break;

            // 손님이 먼저 떠나 조리대에서 접시가 사라졌다면 연습용 기본 메뉴를 쥐여 준다.
            if (_carrier.CurrentSushi == null) _carrier.TryReceive(_practiceFood, null);
            yield return PlayServiceDialogue(_discardDialogue);
            if (!_shop.IsOpen) yield break;
            _discarder.enabled = true;
            _presenter.NotifyCarrierDisplayChanged(_carrier);
            _lastServiceInstruction = null;
            while (_carrier.CurrentSushi != null && _shop.IsOpen)
            {
                ShowServiceInstruction();
                yield return null;
            }
            HideGuidance();
            _serviceStep = ServiceStep.Drink;
            yield break;
        }
    }

    /// <summary>지정 메뉴를 주문할 연습 손님을 생성하고 좌석과 퇴장 위치로 직접 이동하도록 초기화합니다.</summary>
    bool TrySpawnPracticeGuest(Sushi menu)
    {
        var guest = Instantiate(_practiceGuestPrefab, _guestSpawnPoint.position, _guestSpawnPoint.rotation);
        if (!_menus.TryAllocatePracticeGuest(guest, menu))
        {
            Destroy(guest.gameObject);
            Debug.LogWarning($"튜토리얼 지정 주문 배정 실패: {menu.Name}", this);
            return false;
        }
        if (guest.TryGetComponent(out GuestAngryReaction reaction)) reaction.Initialize(_reactionEffects);
        _practiceGuest = guest;
        _expectedOrder = menu;
        _orderReceived = false;
        guest.OrderStarted += OnPracticeOrderStarted;
        if (!guest.Initialize(_shop, _guestExitPoint))
        {
            ReleasePracticeGuest();
            return false;
        }
        guest.WaitForTutorialServing = _serviceStep == ServiceStep.Food;
        _shop.RegisterSpawnedGuest(guest);
        return true;
    }

    void OnPracticeOrderStarted(GuestNPC guest)
    {
        if (!_active || guest != _practiceGuest || guest.SelectedSushi != _expectedOrder) return;
        guest.OrderStarted -= OnPracticeOrderStarted;
        if (!_shop.ReceiveOrder(guest)) return;
        _orderReceived = true;
        if (_serviceStep == ServiceStep.Food) _foodSeat = guest.Seat;
        else if (_serviceStep == ServiceStep.Drink) _drinkSeat = guest.Seat;
    }

    void ShowServiceInstruction()
    {
        string instruction = GetServingInstruction(out _markedTarget);
        if (instruction != _lastServiceInstruction)
        {
            _guidance.ShowSentenceInstruction(instruction);
            _lastServiceInstruction = instruction;
        }
        UpdateWorldMarker();
    }

    void ReleasePracticeGuest()
    {
        if (_practiceGuest != null)
        {
            _practiceGuest.OrderStarted -= OnPracticeOrderStarted;
            if (_shop != null)
            {
                _shop.CancelOrder(_practiceGuest);
                _shop.ReleaseSeat(_practiceGuest);
            }
            Destroy(_practiceGuest.gameObject);
        }
        _practiceGuest = null;
        _expectedOrder = null;
        _orderReceived = false;
    }

    IEnumerator RunRecipeUpgrade()
    {
        var upgrades = _shop.Enforce;
        if (upgrades == null || _starterRecipe == null)
        {
            Debug.LogWarning("초밥집 튜토리얼의 강화 매니저와 시작 레시피를 확인해주세요.", this);
            yield break;
        }
        if (upgrades.GetMenuLevel(_starterRecipe) > 1) yield break;

        var progress = _manager.PlayerProgress;
        if (!progress.SushiTutorialUpgradeSuppliesGranted)
        {
            var inventory = _manager.Inventory;
            if (!upgrades.TryGetMenuUpgradeRequirements(_starterRecipe, out float cost, out var materials) ||
                !inventory.TryAddItems(materials, Mathf.Max(0f, cost - inventory.Gold)))
            {
                Debug.LogWarning("초밥집 튜토리얼의 강화 재료를 지급할 수 없습니다.", this);
                yield break;
            }

            progress.MarkSushiTutorialUpgradeSuppliesGranted();
            GameSaveService.SaveSushiTutorialProgress(_manager);
        }

        yield return GuideToMenu(_upgradeDialogue, 1, "메뉴 화면을 열고 요리 강화 탭을 선택하세요.");
        yield return SelectStarterRecipe();
        while (upgrades.GetMenuLevel(_starterRecipe) < 2)
        {
            yield return WaitForLessonClick(_recipeUpgradeButton, "강화 버튼을 눌러 필요한 재료와 비용을 확인하세요.",
                () => _recipeUpgradePage.activeInHierarchy);
            yield return WaitForLessonClick(_recipeUpgradeConfirmButton, "확인 버튼을 눌러 요리를 레벨 2로 강화하세요.",
                () => upgrades.GetMenuLevel(_starterRecipe) >= 2);
        }
        if (_presenter.IsRecipeUpgradeResultOpen)
            yield return WaitForLessonClick(_recipeUpgradeResultButton, "강화 결과를 확인하고 확인 버튼을 누르세요.",
                () => !_presenter.IsRecipeUpgradeResultOpen);

        GameSaveService.SaveSushiTutorialProgress(_manager);
        _guidance.HideInstruction();
    }

    IEnumerator RunStaffUpgrade()
    {
        var upgrades = _shop.Enforce;
        if (upgrades == null || upgrades.CookingLevel > 1) yield break;

        var progress = _manager.PlayerProgress;
        if (!progress.SushiTutorialStaffUpgradeFundsGranted)
        {
            if (!upgrades.TryGetStaffUpgradeCost(StaffType.Cooking, out float cost))
            {
                Debug.LogWarning("초밥집 튜토리얼의 요리 직원 강화 비용을 확인해주세요.", this);
                yield break;
            }

            var inventory = _manager.Inventory;
            float shortfall = Mathf.Max(0f, cost - inventory.Gold);
            if (shortfall > 0f && !inventory.TryAddGold(shortfall))
            {
                Debug.LogWarning("초밥집 튜토리얼의 직원 강화 비용을 지급할 수 없습니다.", this);
                yield break;
            }

            progress.MarkSushiTutorialStaffUpgradeFundsGranted();
            GameSaveService.SaveSushiTutorialProgress(_manager);
        }

        yield return GuideToMenu(_staffUpgradeDialogue, 3, "직원 관리 버튼을 눌러 직원 화면을 열어보세요.");

        yield return WaitForLessonClick(_cookingStaffButton, "조리를 담당하는 직원을 선택해보세요.",
            () => _managementView.SelectedStaffId == (int)StaffType.Cooking);
        while (upgrades.CookingLevel < 2)
        {
            yield return WaitForLessonClick(_staffUpgradeButton, "강화 버튼을 눌러 직원의 성장 정보를 확인하세요.",
                () => _managementView.StaffUpgradeView.gameObject.activeInHierarchy);
            yield return WaitForLessonClick(_staffUpgradeConfirmButton, "확인 버튼을 눌러 요리 직원을 레벨 2로 강화하세요.",
                () => upgrades.CookingLevel >= 2);
        }

        if (_staffUpgradeResultButton.gameObject.activeInHierarchy)
            yield return WaitForLessonClick(_staffUpgradeResultButton, "강화 완료 창을 닫고 다음 단계로 넘어가세요.",
                () => !_staffUpgradeResultButton.gameObject.activeInHierarchy);

        GameSaveService.SaveSushiTutorialProgress(_manager);
        _guidance.HideInstruction();
    }

    string GetServingInstruction(out Transform target)
    {
        if (_serviceStep == ServiceStep.Discard)
        {
            bool holdingDish = _carrier.CurrentSushi != null;
            target = holdingDish ? (_practiceGuest != null ? _practiceGuest.transform : null) : _shop.Cooker?.transform;
            if (!holdingDish) return "요리사에게 다가가 {Interact}키를 눌러 연습용 접시를 받으세요.";
            return _discarder.enabled ? DiscardInstruction()
                : "접시를 든 채 주문 시간이 끝나면 손님이 어떻게 반응하는지 확인하세요.";
        }

        if (_serviceStep == ServiceStep.Drink)
        {
            var guest = _practiceGuest;
            target = guest != null ? guest.transform : null;
            if (_carrier.CurrentSushi != null) return DiscardInstruction();
            if (_drinkMinigame.IsBusy) return "Start가 지나면 F키를 짧게 반복해서 게이지를 노란 구간에 유지하세요.";
            return guest != null && guest.IsWaitingForDrink
                ? "빈손으로 손님에게 다가가 {Interact}키를 한 번 눌러 음료 미니게임을 시작하세요."
                : "음료를 주문하는 손님을 기다리세요.";
        }

        if (_serviceStep == ServiceStep.Cleaning)
        {
            var seat = _holdInteractor.ActiveTarget as Chair;
            if (seat == null && _drinkSeat != null && _drinkSeat.CanHoldInteract(_carrier)) seat = _drinkSeat;
            if (seat == null) seat = _shop.FindNearestCleanableSeat(_carrier);
            target = seat != null ? seat.ServingPoint : null;
            if (_carrier.CurrentSushi != null) return "초밥을 전달해 손을 비운 뒤 자리를 청소하세요.";
            return seat != null
                ? "손님이 비운 자리로 가서 {Interact}키를 길게 눌러 청소하세요."
                : "손님이 자리를 비울 때까지 기다리세요.";
        }

        target = null;
        var dish = _carrier.CurrentSushi;
        if (dish != null)
        {
            var guest = _shop.FindMostUrgentGuest(dish);
            if (guest != null) target = guest.transform;
            return guest != null ? _servingInstruction : "접시를 들고 주문할 손님을 기다리세요.";
        }

        var cooker = _shop.Cooker;
        if (cooker != null && cooker.CanReceiveBy(_carrier))
        {
            target = cooker.transform;
            return "요리가 완성됐습니다. 요리사에게 다가가 {Interact}키를 눌러 초밥을 받으세요.";
        }
        if (cooker != null && cooker.CookingOrderCount > 0)
        {
            if (cooker.CookingOrders[0] is CookerNPC.CookingOrder order && order.Guest != null)
                target = order.Guest.transform;
            return "손님 머리 위의 주문을 확인하세요. 요리사가 초밥을 만들고 있습니다.";
        }
        return "손님이 초밥을 주문하면 머리 위에 메뉴가 표시됩니다.";
    }

    string DiscardInstruction()
    {
        var action = InputSystem.actions?.FindAction("Hero/TrashFood", false);
        string key = action?.GetBindingDisplayString();
        if (string.IsNullOrEmpty(key)) key = "G";
        return $"들고 있는 접시는 {key}키를 {_discarder.DiscardDuration:0.##}초 동안 눌러 폐기하세요.";
    }

    void UpdateWorldMarker()
    {
        if (_markedTarget == null || _camera == null)
        {
            if (_worldMarker != null) _worldMarker.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = _camera.WorldToScreenPoint(_markedTarget.position + Vector3.up * 2f);
        if (screen.z <= 0f || !_camera.pixelRect.Contains(screen))
        {
            if (_worldMarker != null) _worldMarker.gameObject.SetActive(false);
            return;
        }

        var canvas = (RectTransform)_guidance.transform;
        if (_worldMarker == null)
        {
            var marker = new GameObject("Sushi Tutorial Target", typeof(RectTransform), typeof(Image));
            _worldMarker = (RectTransform)marker.transform;
            _worldMarker.SetParent(canvas, false);
            _worldMarker.anchorMin = _worldMarker.anchorMax = canvas.pivot;
            _worldMarker.sizeDelta = new Vector2(18f, 18f);
            _worldMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = marker.GetComponent<Image>();
            image.color = new Color(1f, 0.85f, 0.1f);
            image.raycastTarget = false;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
        _worldMarker.anchoredPosition = local;
        _worldMarker.gameObject.SetActive(true);
    }

    void OnFoodServed(GuestNPC guest)
    {
        if (_serviceStep != ServiceStep.Food || guest != _practiceGuest || !_orderReceived) return;
        _serviceStep = ServiceStep.Discard;
    }

    void OnDrinkServed(GuestNPC guest)
    {
        if (_serviceStep == ServiceStep.Drink && guest == _practiceGuest && _orderReceived)
            _serviceStep = ServiceStep.Cleaning;
    }

    void OnHoldInteractionCompleted(ISushiHoldInteraction target)
    {
        if (_serviceStep == ServiceStep.Cleaning && target is Chair chair &&
            (chair == _foodSeat || chair == _drinkSeat))
        {
            if ((_foodSeat == null || !_foodSeat.IsUncleaned) &&
                (_drinkSeat == null || !_drinkSeat.IsUncleaned)) _serviceStep = ServiceStep.Complete;
        }
    }

    internal void AdvanceDialogue()
    {
        if (IsDialogue) _talk.NextTalk();
    }

    IEnumerator WaitForManagementClose()
    {
        if (_menuBook.gameObject.activeInHierarchy)
            yield return WaitForLessonClick(_menuCloseButton, "강화를 마쳤다면 메뉴 책을 닫아 다음 안내를 들어보세요.",
                () => !_menuBook.gameObject.activeInHierarchy);
    }

    IEnumerator GuideToMenu(TutorialDialogueData data, int mode, string instruction)
    {
        // 첫 대사는 진입 안내, 나머지는 직접 해당 화면을 연 뒤 설명한다.
        yield return PlayDialogue(data, 0, 1);
        if (!_menuBook.gameObject.activeInHierarchy)
            yield return WaitForLessonClick(_todayMenuButton, "오늘의 메뉴 버튼을 눌러 메뉴 책을 여세요.",
                () => _menuBook.gameObject.activeInHierarchy);
        if (_menuBook.CurrentMode != mode)
            yield return WaitForLessonClick(mode == 1 ? _recipeUpgradeTab : _staffTab, instruction,
                () => _menuBook.CurrentMode == mode, _menuBook.transform.Find("BookFront")?.GetComponent<Image>());

        // 대화가 메뉴 등장 연출의 입력 잠금을 복원하지 않도록 메뉴가 열릴 때까지 기다린다.
        if (_menuBook.TryGetComponent<CanvasGroup>(out var menuInput))
            while (menuInput != null && _menuBook.gameObject.activeInHierarchy && !menuInput.interactable)
                yield return null;
        yield return PlayDialogue(data, 1);
    }

    bool HasStarterMenu()
    {
        var menus = _managementView.MenuView.CurrentPresentation?.Menus;
        if (menus == null) return false;
        foreach (var menu in menus)
            if (menu != null && menu.Registered && menu.RecipeId == _starterRecipe.Id) return true;
        return false;
    }

    IEnumerator SelectStarterRecipe()
    {
        // 목록은 View가 동적으로 구성하므로 단계 진입 시에만 공개 RecipeId로 찾는다.
        Button target = null;
        foreach (var slot in _managementView.RecipeListView.GetComponentsInChildren<SushiRecipeSlotView>(true))
            if (slot.RecipeId == _starterRecipe.Id)
            {
                target = slot.GetComponentInChildren<Button>(true);
                break;
            }
        if (target == null)
        {
            Debug.LogWarning("튜토리얼 지정 요리 슬롯을 찾을 수 없습니다.", this);
            StopTutorial();
            yield break;
        }
        // 스크롤 아래의 슬롯도 실제 표시 영역으로 가져온다. 뷰 데이터/목록 순서는 바꾸지 않는다.
        var scroll = target.GetComponentInParent<ScrollRect>();
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target.transform);
            float hidden = scroll.content.rect.height - viewport.rect.height;
            if (hidden > 0f)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition -
                    (viewport.rect.center.y - bounds.center.y) / hidden);
            scroll.StopMovement();
        }
        yield return WaitForLessonClick(target, _starterRecipe.Name + " 아이콘을 선택해보세요.",
            () => _managementView.SelectedRecipeId == _starterRecipe.Id);
    }

    IEnumerator WaitForLessonClick(Button button, string instruction, Func<bool> succeeded = null, Image page = null)
    {
        // 화면의 Awake/OnEnable에서 등록한 실제 기능 다음에 결과만 확인한다.
        yield return null;
        LockLessonInputs();
        _lessonButton = button;
        _lessonClicked = false;
        button.onClick.AddListener(OnLessonClicked);
        SetLessonTarget(button);
        _guidance.ShowSentenceInstruction(instruction);
        _guidance.ShowFocus(button.transform as RectTransform, page);
        while (_active)
        {
            // 책갈피 전환 등의 비동기 연출이 끝날 때까지 같은 클릭의 결과를 확인한다.
            if (_lessonClicked && (succeeded == null || succeeded())) break;
            yield return null;
        }
        SetLessonTarget(null);
        ClearLessonButton();
        _guidance.HideInstruction();
    }

    void OnLessonClicked()
    {
        // 같은 onClick의 다른 기능이 실행되기 전에 입력을 잠그지 않는다.
        _lessonClicked = true;
    }

    void ClearLessonButton()
    {
        if (_lessonButton != null) _lessonButton.onClick.RemoveListener(OnLessonClicked);
        _lessonButton = null;
    }

    void LockLessonInputs()
    {
        LockLessonRoot(_managementView);
        if (_lessonInputRoots != null)
            foreach (var root in _lessonInputRoots) LockLessonRoot(root);
        SetLessonTarget(null);
    }

    void LockLessonRoot(Component root)
    {
        if (root == null) return;
        foreach (var focus in root.GetComponentsInChildren<UIKeyboardFocusView>(true))
        {
            if (_keyboardFocusStates.ContainsKey(focus)) continue;
            _keyboardFocusStates.Add(focus, focus.enabled);
            focus.enabled = false;
        }
        foreach (var input in root.GetComponentsInChildren<Selectable>(true))
        {
            if (_lessonInputs.ContainsKey(input)) continue;
            var group = input.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added) group = input.gameObject.AddComponent<CanvasGroup>();
            _lessonInputs.Add(input, (group, added, group.interactable, input.navigation));
        }
    }

    void SetLessonTarget(Selectable target)
    {
        foreach (var entry in _lessonInputs)
        {
            if (entry.Key == null || entry.Value.group == null) continue;
            entry.Value.group.interactable = entry.Key == target;
            entry.Key.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        var system = UnityEngine.EventSystems.EventSystem.current;
        if (system != null) system.SetSelectedGameObject(null);
    }

    void RestoreLessonInputs()
    {
        ClearLessonButton();
        foreach (var entry in _lessonInputs)
        {
            if (entry.Key != null) entry.Key.navigation = entry.Value.navigation;
            if (entry.Value.group == null) continue;
            entry.Value.group.interactable = entry.Value.interactable;
            if (entry.Value.added) Destroy(entry.Value.group);
        }
        _lessonInputs.Clear();
        foreach (var entry in _keyboardFocusStates)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        _keyboardFocusStates.Clear();
    }

    IEnumerator PlayDialogue(TutorialDialogueData data, int startLine = 0, int count = int.MaxValue)
    {
        var dialogue = data != null ? data.Dialogue : null;
        if (dialogue == null || !dialogue.HasLines) yield break;

        while (_talk.IsTalking) yield return null;
        _hero.ResetInput();
        int length = Mathf.Min(count, dialogue.Lines.Length - startLine);
        if (length <= 0) yield break;
        var lines = new TalkView.TalkLine[length];
        for (int i = 0; i < lines.Length; i++)
        {
            var line = dialogue.Lines[startLine + i];
            lines[i] = new TalkView.TalkLine(line.PlayerSpeaks ? 0 : 1, TutorialView.FormatSentenceLines(line.Content));
        }

        _ownsDialogue = true;
        string npc = string.IsNullOrWhiteSpace(dialogue.NpcName) || dialogue.NpcName == "???"
            ? "요리사" : dialogue.NpcName;
        _talk.BeginTalk(_manager.PlayerName, npc, lines, dialogue.PlayerIllustration, dialogue.NpcIllustration, keepBackgroundVisible: true);
        while (_talk.IsTalking) yield return null;
        _ownsDialogue = false;
    }

    IEnumerator PlayServiceDialogue(TutorialDialogueData data)
    {
        if (data == null || data.Dialogue == null || !data.Dialogue.HasLines) yield break;

        HideGuidance();
        _serviceTimeScale = Time.timeScale;
        _holdInteractorWasEnabled = _holdInteractor != null && _holdInteractor.enabled;
        _servicePausedForDialogue = true;
        Time.timeScale = 0f;
        if (_holdInteractorWasEnabled) _holdInteractor.enabled = false;

        yield return PlayDialogue(data);
        // 대화를 닫는 입력이 같은 프레임에 음료 제공이나 청소를 시작하지 않도록 합니다.
        yield return null;
        ResumeService();
    }

    void ResumeService()
    {
        if (!_servicePausedForDialogue) return;
        _servicePausedForDialogue = false;
        Time.timeScale = _serviceTimeScale;
        if (_holdInteractor != null) _holdInteractor.enabled = _holdInteractorWasEnabled;
    }

    void EndTutorialDialogue()
    {
        if (IsDialogue) _talk.EndTalk();
        _ownsDialogue = false;
        ResumeService();
    }

    internal bool TryShowSettlement(Action showResult)
    {
        if (!_active || _settlementStarted) return false;
        _settlementStarted = true;
        if (_routine != null) StopCoroutine(_routine);
        RestoreLessonInputs();
        EndTutorialDialogue();
        HideGuidance();
        _routine = StartCoroutine(ShowSettlement(showResult));
        return true;
    }

    IEnumerator ShowSettlement(Action showResult)
    {
        yield return PlayDialogue(_settlementDialogue);
        _routine = null;
        showResult();
    }

    internal void Complete()
    {
        if (_active && _settlementStarted) _manager.PlayerProgress.CompleteSushiTutorial();
    }

    void HideGuidance()
    {
        if (_guidance != null) _guidance.HideInstruction();
        if (_worldMarker != null) _worldMarker.gameObject.SetActive(false);
        _markedTarget = null;
    }

    internal void StopTutorial()
    {
        if (!_active) return;
        _active = false;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        RestoreLessonInputs();
        if (_carrier != null) _carrier.FoodServed -= OnFoodServed;
        if (_drinkMinigame != null) _drinkMinigame.DrinkServed -= OnDrinkServed;
        if (_holdInteractor != null) _holdInteractor.HoldInteractionCompleted -= OnHoldInteractionCompleted;
        EndTutorialDialogue();
        ReleasePracticeGuest();
        RestorePracticeCleaning();
        if (_discarder != null) _discarder.enabled = _discarderWasEnabled;
        if (_guestSpawner != null) _guestSpawner.enabled = _spawnerWasEnabled;
        if (_shop != null && _shop.Serving != null) _shop.Serving.enabled = _servingWasEnabled;
        if (_startBusinessButton != null && !_businessAllowed)
            _startBusinessButton.interactable = _businessButtonWasInteractable;
        HideGuidance();
        if (_guidance != null) Destroy(_guidance.gameObject);
        _guidance = null;
        _worldMarker = null;
    }

    void OnDisable() => StopTutorial();
}
