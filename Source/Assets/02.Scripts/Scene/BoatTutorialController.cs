using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BoatTutorialController : MonoBehaviour
{
    [Header("모닝보트 대화: 인사, 상점, 강화, 휴대폰, 잠수")]
    [SerializeField] TutorialDialogueData _greetingDialogue;
    [SerializeField] TutorialDialogueData _shopDialogue;
    [SerializeField] TutorialDialogueData _upgradeDialogue;
    [SerializeField] TutorialDialogueData _phoneDialogue;
    [SerializeField] TutorialDialogueData _diveDialogue;
    [Header("상점 구매 실습")]
    [SerializeField] TutorialDialogueData _shopCompletedDialogue;
    [SerializeField] IngredientItemData _shopTutorialItem;
    [SerializeField, Min(0f)] float _shopTutorialGold;
    [SerializeField] Button _riceButton;
    [SerializeField] Button _shopPurchaseButton;
    [Header("장비 강화 실습")]
    [SerializeField] TutorialDialogueData _upgradeCompletedDialogue;
    [SerializeField] EquipmentData.EquipType _upgradeTutorialType;
    [SerializeField, Min(0f)] float _upgradeTutorialGold;
    [SerializeField] UpgradeEquipScreenView _upgradeScreen;
    [SerializeField] UpgradeConfirmationPopupView _upgradeConfirmation;
    [SerializeField] Button _equipmentSlotButton;
    [SerializeField] Button _equipmentUpgradeButton;
    [SerializeField] Button _equipmentConfirmButton;
    [Header("휴대폰 앱 안내")]
    [SerializeField] TutorialDialogueData _inventoryDialogue;
    [SerializeField] TutorialDialogueData _questDialogue;
    [SerializeField] InventoryView _inventoryView;
    [SerializeField] QuestListView _questView;
    [SerializeField] Transform _phoneAppGroup;
    [SerializeField] Button _inventoryAppButton;
    [SerializeField] Button _questAppButton;
    [SerializeField] Button _inventoryCloseButton;
    [SerializeField] Button _questCloseButton;
    [Header("오후 보트 대화")]
    [SerializeField] TutorialDialogueData _skipDialogue;
    [Header("저녁 보트 대화")]
    [SerializeField] TutorialDialogueData _sushiTravelDialogue;
    [Header("버튼과 안내 뷰")]
    [SerializeField] Button _shopCloseButton;
    [SerializeField] Button _equipmentCloseButton;
    [SerializeField] Button _phoneButton;
    [SerializeField] Button _skipButton;
    [SerializeField] Button _npcButton;
    [SerializeField] TutorialView _tutorialView;
    [SerializeField] GameObject _diveMarker;
    [SerializeField] TMPro.TMP_Text _diveHoldKeyText;

    BoatPlayScene _scene;
    GameManager _manager;
    TalkView _talk;
    Coroutine _routine;
    bool _afternoon, _night, _ownsDialogue, _menuOpened, _skipReady;
    bool _diveWasEnabled, _phoneWasEnabled, _skipWasEnabled;
    ViewActivationRelay _inventoryActivation, _questActivation;
    enum PhoneApp { None, Inventory, Quest }
    PhoneApp _pendingPhoneApp;
    enum PhoneLesson { None, OpenPhone, Inventory, InventoryDialogue, CloseInventory, Quest, QuestDialogue, CloseQuest }
    PhoneLesson _phoneLesson;
    int _step;
    enum ShopLesson { None, Opening, Dialogue, SelectRice, Purchase, Complete }
    ShopLesson _shopLesson;
    int _riceCountBeforePurchase;
    float _goldBeforePurchase;
    enum UpgradeLesson { None, Opening, SelectEquipment, Upgrade, Confirm, Complete }
    UpgradeLesson _upgradeLesson;
    int _levelBeforeUpgrade;
    float _goldBeforeUpgrade, _upgradeCost;
    readonly Dictionary<UIKeyboardFocusView, bool> _lessonKeyboardFocus = new Dictionary<UIKeyboardFocusView, bool>();
    readonly Dictionary<Selectable, (CanvasGroup group, bool added, bool interactable, Navigation navigation)> _lessonInputStates
        = new Dictionary<Selectable, (CanvasGroup, bool, bool, Navigation)>();

    public bool IsActive { get; private set; }
    public bool BlocksMovement => IsActive && _ownsDialogue;
    public bool AllowsNPC => !IsActive || !_afternoon && !_night && !_ownsDialogue && _step < 4;
    public bool AllowsDive => !IsActive || !_afternoon && !_ownsDialogue && (_night || _step == 7);
    public bool AllowsSmartPhone => !IsActive || !_afternoon && !_night && !_ownsDialogue && _step >= 5
        && (_phoneLesson == PhoneLesson.None || _phoneLesson == PhoneLesson.OpenPhone);
    public bool AllowsTimeSkip => !IsActive || _afternoon && _skipReady && !_ownsDialogue;

    public void Begin(BoatPlayScene scene, GameManager manager)
    {
        if (IsActive || !isActiveAndEnabled || scene == null || manager == null) return;
        var settings = scene.GetComponent<TutorialSceneSettings>();
        if (settings == null || !settings.isActiveAndEnabled || manager.PlayerProgress == null) return;
        _afternoon = settings.CurrentChapter == TutorialSceneSettings.Chapter.AfternoonBoat;
        _night = settings.CurrentChapter == TutorialSceneSettings.Chapter.NightBoat;
        if (!_afternoon && !_night && settings.CurrentChapter != TutorialSceneSettings.Chapter.MorningBoat) return;
        if (scene.Base == null || scene.Base.TalkView == null || _tutorialView == null
            || _skipButton == null || (_night ? _sushiTravelDialogue == null || scene.SushiObject == null || scene.SushiObject.BoatMover == null
            : _afternoon ? _skipDialogue == null
            : scene.Store == null || scene.UpgradeView == null || _phoneButton == null
            || _shopCloseButton == null || _equipmentCloseButton == null || _shopDialogue == null
            || _upgradeDialogue == null || _phoneDialogue == null || _diveDialogue == null
            || _inventoryDialogue == null || _questDialogue == null || _inventoryView == null || _questView == null))
        {
            Debug.LogWarning("보트 튜토리얼의 대화·버튼·뷰 연결을 확인해주세요.", this);
            return;
        }
        if (!_afternoon && !_night && manager.PlayerProgress.BoatCommerceTutorialStep < 2
            && (_riceButton == null || _shopPurchaseButton == null || _shopTutorialItem == null
            || _shopCompletedDialogue == null || manager.Inventory == null
            || !manager.Inventory.CanAddGold(_shopTutorialGold) || _shopTutorialGold < _shopTutorialItem.SellPrice))
        {
            Debug.LogWarning("상점 튜토리얼의 쌀·구매 버튼·완료 대화와 지급 골드를 확인해주세요.", this);
            return;
        }
        if (!_afternoon && !_night && manager.PlayerProgress.BoatCommerceTutorialStep < 4
            && (_upgradeCompletedDialogue == null || _upgradeScreen == null || _upgradeConfirmation == null
            || _equipmentSlotButton == null || _equipmentUpgradeButton == null || _equipmentConfirmButton == null
            || manager.EquipController == null || manager.Inventory == null))
        {
            Debug.LogWarning("강화 튜토리얼의 대상 장비·버튼·완료 대화 연결을 확인해주세요.", this);
            return;
        }
        if (!_afternoon && !_night && manager.PlayerProgress.BoatCommerceTutorialStep < 6
            && (_phoneAppGroup == null || _inventoryAppButton == null || _questAppButton == null
            || _inventoryCloseButton == null || _questCloseButton == null))
        {
            Debug.LogWarning("휴대폰 튜토리얼의 앱 목록·인벤토리·퀘스트·뒤로가기 버튼 연결을 확인해주세요.", this);
            return;
        }
        _scene = scene; _manager = manager; _talk = scene.Base.TalkView;
        _step = Mathf.Clamp(manager.PlayerProgress.BoatCommerceTutorialStep, 0, 7);
        _ownsDialogue = true; IsActive = true; _skipReady = false;
        _pendingPhoneApp = PhoneApp.None;
        _diveWasEnabled = scene.DiveObject != null && scene.DiveObject.enabled;
        if (scene.DiveObject != null) scene.DiveObject.enabled = false;
        _phoneWasEnabled = _phoneButton != null && _phoneButton.interactable;
        _skipWasEnabled = _skipButton.interactable;
        if (_phoneButton != null) _phoneButton.interactable = false;
        _skipButton.interactable = false;
        if (!_afternoon && !_night)
        {
            scene.Store.OnOpened += OnShopOpened;
            scene.UpgradeView.OnOpened += OnEquipmentOpened;
            _inventoryActivation = GetActivationRelay(_inventoryView);
            _questActivation = GetActivationRelay(_questView);
            _inventoryActivation.Opened += OnInventoryOpened;
            _questActivation.Opened += OnQuestOpened;
        }
        scene.SceneHero.ResetInput();
        _routine = StartCoroutine(_night ? RunNight() : _afternoon ? RunAfternoon() : RunMorning());
    }

    IEnumerator RunMorning()
    {
        yield return null;
        if (_step == 0 && _manager.PlayerProgress.BoatTutorialStep == 0 && _greetingDialogue != null
            && _greetingDialogue.Dialogue != null && _greetingDialogue.Dialogue.HasLines)
        {
            yield return PlayDialogue(_greetingDialogue);
            if (!IsActive) yield break;
            _manager.PlayerProgress.SetBoatTutorialStep(1);
        }
        if (_step < 2)
        {
            yield return RunShopLesson();
            if (!IsActive) yield break;
        }
        if (_step < 4)
        {
            yield return RunUpgradeLesson();
            if (!IsActive) yield break;
        }
        if (_step == 4)
        {
            yield return PlayDialogue(_phoneDialogue);
            if (!IsActive) yield break;
            SetStep(5);
        }
        _ownsDialogue = false;
        RefreshNPC();
        // 앱 안내가 끝나도 버튼을 잠그지 않는다. 공통 ESC 처리도 이 버튼 상태를 사용한다.
        _phoneButton.interactable = _phoneWasEnabled;
        if (_step == 5)
        {
            yield return ExplainPhoneApps();
            if (!IsActive) yield break;
            SetStep(6);
        }
        if (_step == 6)
        {
            yield return PlayDialogue(_diveDialogue);
            if (!IsActive) yield break;
            SetStep(7);
        }
        _ownsDialogue = false;
        if (_scene.DiveObject != null) _scene.DiveObject.enabled = _diveWasEnabled;
        _tutorialView.ShowInstruction("보트 끝의 잠수 영역에서 {Interact}키를 길게 누르세요.", _diveMarker, _diveHoldKeyText);
        _routine = null;
    }

    IEnumerator RunNight()
    {
        yield return null;
        yield return PlayDialogue(_sushiTravelDialogue);
        if (!IsActive) yield break;
        if (_diveMarker != null)
            _diveMarker.transform.position = _scene.SushiObject.transform.position + Vector3.up * 0.05f;
        _tutorialView.ShowInstruction("승선 영역에서 {Interact}키를 눌러 초밥집으로 출발하세요.", _diveMarker);
        _routine = null;
    }

    IEnumerator RunAfternoon()
    {
        yield return null;
        yield return PlayDialogue(_skipDialogue);
        if (!IsActive) yield break;
        _skipReady = true;
        _skipButton.interactable = true;
        _tutorialView.ShowInstruction("시간 스킵 버튼을 눌러 저녁 보트로 이동하세요.");
        _tutorialView.ShowFocus(_skipButton.transform as RectTransform);
        _routine = null;
    }

    IEnumerator PlayDialogue(TutorialDialogueData data, bool keepMenuOpen = false)
    {
        _ownsDialogue = true;
        _tutorialView.HideInstruction();
        if (!keepMenuOpen) _scene._boatNPCInteraction?.CloseView();
        _scene.SceneHero.ResetInput();
        if (!keepMenuOpen) RefreshNPC();
        while (_talk.IsTalking) yield return null;
        var dialogue = data.Dialogue;
        var lines = new TalkView.TalkLine[dialogue.Lines.Length];
        for (int i = 0; i < lines.Length; i++)
            lines[i] = new TalkView.TalkLine(dialogue.Lines[i].PlayerSpeaks ? 0 : 1,
                dialogue.Lines[i].Content.Replace("{gold}", _shopTutorialGold.ToString("0.##", CultureInfo.InvariantCulture))
                    .Replace("{upgradeGold}", _upgradeTutorialGold.ToString("0.##", CultureInfo.InvariantCulture))
                    .Replace("{equipment}", GetTutorialEquipmentName()));
        _talk.BeginTalk(_manager.PlayerName, dialogue.NpcName, lines, dialogue.PlayerIllustration, dialogue.NpcIllustration, keepBackgroundVisible: true);
        if (!_talk.IsTalking) { StopTutorial(); yield break; }
        while (_talk.IsTalking) yield return null;
        _ownsDialogue = false;
    }

    void SetStep(int step)
    {
        _step = step;
        _manager.PlayerProgress.SetBoatCommerceTutorialStep(step);
    }
    void RefreshNPC()
    {
        _scene._boatNPCInteraction?.SetAllowedInteraction(IsActive && !_afternoon && !_night && _step < 4
            ? (_step < 2 ? NPCInteractType.Store : NPCInteractType.Upgrade) : NPCInteractType.None);
        _scene.SetBoatNPCNearby(_scene.SceneBoatNPC != null && _scene.SceneBoatNPC.IsHeroDetected(_scene.SceneHero));
    }

    IEnumerator RunShopLesson()
    {
        _ownsDialogue = false;
        _menuOpened = false;
        _shopLesson = ShopLesson.Opening;
        RefreshNPC();
        _tutorialView.ShowInstruction("NPC에게 다가가 {Interact}키를 눌러 상점을 열어보세요.");
        while (IsActive && !_menuOpened) yield return null;
        if (!IsActive) yield break;
        // 등장 연출이 잠근 입력을 대화가 이전 상태로 기억하지 않도록 연출 완료를 기다린다.
        yield return null;
        var entrance = _scene.Store.GetComponentInParent<CommonWindowEntranceView>();
        while (IsActive && entrance != null && entrance.IsEntering) yield return null;
        if (!IsActive) yield break;
        _shopLesson = ShopLesson.Dialogue;
        if (_step == 0)
        {
            yield return PlayDialogue(_shopDialogue, true);
            if (!IsActive) yield break;
            if (!_manager.Inventory.TryAddGold(_shopTutorialGold))
            {
                Debug.LogWarning("튜토리얼 골드를 지급하지 못했습니다.", this);
                StopTutorial();
                yield break;
            }
            // 저장된 1단계는 이미 골드를 받은 상태. 재진입 시 중복 지급하지 않는다.
            SetStep(1);
        }
        _ownsDialogue = false;
        _shopLesson = ShopLesson.SelectRice;
        FocusLessonButton(_riceButton, "초밥 재료로 사용할 쌀을 선택해보세요.");
        while (IsActive && _shopLesson != ShopLesson.Complete) yield return null;
        if (!IsActive) yield break;
        // 구매 버튼 콜백이 끝난 다음 상점의 거래/선택 상태를 정리한다.
        _tutorialView.HideInstruction();
        RestoreLessonInputs();
        SetStep(2);
        yield return PlayDialogue(_shopCompletedDialogue, true);
        if (!IsActive) yield break;
        _scene._boatNPCInteraction?.CloseView();
        _shopLesson = ShopLesson.None;
    }

    void OnShopOpened()
    {
        if (!IsActive || _step >= 2) return;
        _menuOpened = true;
        // StoreView.InitializeIngredients/InitializeStore가 등록한 실제 거래 리스너 뒤에 연결한다.
        _riceButton.onClick.RemoveListener(OnRiceSelected);
        _riceButton.onClick.AddListener(OnRiceSelected);
        _shopPurchaseButton.onClick.RemoveListener(OnShopPurchaseSubmitted);
        _shopPurchaseButton.onClick.AddListener(OnShopPurchaseSubmitted);
        LockLessonInputs(_scene.Store);
    }

    void LockLessonInputs(Component root)
    {
        // 확인 팝업을 포함해 일반 선택 테두리를 잠시 끄고 튜토리얼 강조만 사용한다.
        foreach (var focus in root.GetComponentsInChildren<UIKeyboardFocusView>(true))
        {
            if (_lessonKeyboardFocus.ContainsKey(focus)) continue;
            _lessonKeyboardFocus.Add(focus, focus.enabled);
            focus.enabled = false;
        }
        // 뷰가 Button.interactable을 갱신해도 다른 카드·탭·취소 입력은 열리지 않는다.
        foreach (var input in root.GetComponentsInChildren<Selectable>(true))
        {
            if (_lessonInputStates.ContainsKey(input)) continue;
            var group = input.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added) group = input.gameObject.AddComponent<CanvasGroup>();
            _lessonInputStates.Add(input, (group, added, group.interactable, input.navigation));
        }
        SetLessonInputTarget(null);
    }

    void OnRiceSelected()
    {
        if (!IsActive || _shopLesson != ShopLesson.SelectRice || _scene.Store.SelectedItem != _shopTutorialItem) return;
        _riceCountBeforePurchase = _manager.Inventory.GetCount(_shopTutorialItem);
        _goldBeforePurchase = _manager.Inventory.Gold;
        _shopLesson = ShopLesson.Purchase;
        FocusLessonButton(_shopPurchaseButton, "구매 버튼을 눌러 쌀을 구매해보세요.");
    }

    void OnShopPurchaseSubmitted()
    {
        if (!IsActive || _shopLesson != ShopLesson.Purchase) return;
        if (_manager.Inventory.GetCount(_shopTutorialItem) > _riceCountBeforePurchase
            && _manager.Inventory.Gold <= _goldBeforePurchase - _shopTutorialItem.SellPrice)
        {
            _shopLesson = ShopLesson.Complete;
            SetLessonInputTarget(null);
        }
    }

    void FocusLessonButton(Button button, string instruction)
    {
        SetLessonInputTarget(button);
        _tutorialView.ShowInstruction(instruction);
        _tutorialView.ShowFocus(button.transform as RectTransform);
    }

    void SetLessonInputTarget(Selectable target)
    {
        foreach (var entry in _lessonInputStates)
        {
            if (entry.Key == null || entry.Value.group == null) continue;
            entry.Value.group.interactable = entry.Key == target;
            entry.Key.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        // 이전에 선택된 다른 버튼으로 Enter/키보드 입력이 새지 않게 한다.
        var system = UnityEngine.EventSystems.EventSystem.current;
        if (system != null) system.SetSelectedGameObject(null);
    }

    void RestoreLessonInputs()
    {
        foreach (var entry in _lessonInputStates)
        {
            if (entry.Key != null) entry.Key.navigation = entry.Value.navigation;
            if (entry.Value.group == null) continue;
            entry.Value.group.interactable = entry.Value.interactable;
            if (entry.Value.added) Destroy(entry.Value.group);
        }
        _lessonInputStates.Clear();
        foreach (var entry in _lessonKeyboardFocus)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        _lessonKeyboardFocus.Clear();
    }

    IEnumerator RunUpgradeLesson()
    {
        if (!_manager.EquipController.TryGetUpgradeGold(_upgradeTutorialType, out _upgradeCost)
            || _step == 2 && (!_manager.Inventory.CanAddGold(_upgradeTutorialGold)
                || _manager.Inventory.Gold + _upgradeTutorialGold < _upgradeCost))
        {
            Debug.LogWarning("지정 장비의 강화 가능 레벨·비용과 튜토리얼 지급 골드를 확인해주세요.", this);
            StopTutorial();
            yield break;
        }
        if (_step == 2)
        {
            yield return PlayDialogue(_upgradeDialogue);
            if (!IsActive) yield break;
            if (!_manager.Inventory.TryAddGold(_upgradeTutorialGold))
            {
                StopTutorial();
                yield break;
            }
            // 3단계부터 재개하면 안내용 골드를 다시 지급하지 않는다.
            SetStep(3);
        }
        _ownsDialogue = false;
        _menuOpened = false;
        _upgradeLesson = UpgradeLesson.Opening;
        RefreshNPC();
        _tutorialView.ShowInstruction("NPC의 장비 강화 메뉴를 열어보세요.");
        while (IsActive && !_menuOpened) yield return null;
        if (!IsActive) yield break;
        yield return null;
        ConnectUpgradeLesson();
        _upgradeLesson = UpgradeLesson.SelectEquipment;
        FocusLessonButton(_equipmentSlotButton, GetTutorialEquipmentName() + " 슬롯을 선택해보세요.");
        while (IsActive && _upgradeLesson != UpgradeLesson.Complete) yield return null;
        if (!IsActive) yield break;
        _tutorialView.HideInstruction();
        RestoreLessonInputs();
        SetStep(4);
        yield return PlayDialogue(_upgradeCompletedDialogue, true);
        if (!IsActive) yield break;
        _scene._boatNPCInteraction?.CloseView();
        _upgradeLesson = UpgradeLesson.None;
    }

    string GetTutorialEquipmentName()
    {
        var data = _manager != null && _manager.EquipController != null ? _manager.EquipController.EquipmentDatas : null;
        int index = (int)_upgradeTutorialType;
        return data != null && index >= 0 && index < data.Count && data[index] != null ? data[index].EquipName : "지정 장비";
    }

    int GetTutorialEquipmentLevel()
    {
        var levels = _manager.EquipController.EquipmentLevels;
        int index = (int)_upgradeTutorialType;
        return index >= 0 && index < levels.Count ? levels[index] : 0;
    }

    void OnEquipmentOpened()
    {
        if (!IsActive || _step != 3 || _ownsDialogue) return;
        _menuOpened = true;
        LockLessonInputs(_scene.UpgradeView);
    }

    void ConnectUpgradeLesson()
    {
        // 화면의 Awake/OnEnable에서 선택 버튼을 연결한 다음 순서로 구독한다.
        _equipmentSlotButton.onClick.RemoveListener(OnEquipmentSelected);
        _equipmentSlotButton.onClick.AddListener(OnEquipmentSelected);
        _equipmentUpgradeButton.onClick.RemoveListener(OnUpgradeButtonPressed);
        _equipmentUpgradeButton.onClick.AddListener(OnUpgradeButtonPressed);
        // PlayScene의 실제 거래 리스너 뒤에서 결과만 확인한다. 강화를 중복 실행하지 않는다.
        _scene.UpgradeView.OnUpgradeRequested -= OnEquipmentUpgradeRequested;
        _scene.UpgradeView.OnUpgradeRequested += OnEquipmentUpgradeRequested;
    }

    void OnEquipmentSelected()
    {
        if (!IsActive || _upgradeLesson != UpgradeLesson.SelectEquipment
            || _upgradeScreen.SelectedEquipmentType != (int)_upgradeTutorialType) return;
        _levelBeforeUpgrade = GetTutorialEquipmentLevel();
        _upgradeLesson = UpgradeLesson.Upgrade;
        FocusLessonButton(_equipmentUpgradeButton, "강화 버튼을 눌러 " + GetTutorialEquipmentName() + "을 강화해보세요.");
    }

    void OnUpgradeButtonPressed()
    {
        if (!IsActive || _upgradeLesson != UpgradeLesson.Upgrade || !_upgradeConfirmation.IsOpen) return;
        _goldBeforeUpgrade = _manager.Inventory.Gold;
        if (!_manager.EquipController.TryGetUpgradeGold(_upgradeTutorialType, out _upgradeCost)) return;
        _upgradeLesson = UpgradeLesson.Confirm;
        // Show로 팝업 Awake가 끝난 후 기존 확인 리스너 뒤에 연결한다.
        _equipmentConfirmButton.onClick.RemoveListener(OnUpgradeConfirmed);
        _equipmentConfirmButton.onClick.AddListener(OnUpgradeConfirmed);
        FocusLessonButton(_equipmentConfirmButton, "비용을 확인하고 확인 버튼을 눌러보세요.");
    }

    void OnEquipmentUpgradeRequested(int equipmentType)
    {
        if (!IsActive || _upgradeLesson != UpgradeLesson.Confirm || equipmentType != (int)_upgradeTutorialType) return;
        if (GetTutorialEquipmentLevel() > _levelBeforeUpgrade
            && _manager.Inventory.Gold <= _goldBeforeUpgrade - _upgradeCost + .001f)
        {
            _upgradeLesson = UpgradeLesson.Complete;
            SetLessonInputTarget(null);
        }
    }

    void OnUpgradeConfirmed()
    {
        if (!IsActive || _upgradeLesson != UpgradeLesson.Confirm) return;
        // 잔액 변화 등으로 실제 강화가 거절되면 완료하지 않고 다시 시도하게 한다.
        _upgradeLesson = UpgradeLesson.Upgrade;
        FocusLessonButton(_equipmentUpgradeButton, "강화 비용을 확인하고 다시 강화해보세요.");
    }

    ViewActivationRelay GetActivationRelay(Component view)
    {
        var relay = view.GetComponent<ViewActivationRelay>();
        return relay != null ? relay : view.gameObject.AddComponent<ViewActivationRelay>();
    }

    void OnInventoryOpened()
    {
        if (IsActive && _step == 5 && _phoneLesson == PhoneLesson.Inventory)
            _pendingPhoneApp = PhoneApp.Inventory;
    }

    void OnQuestOpened()
    {
        if (IsActive && _step == 5 && _phoneLesson == PhoneLesson.Quest)
            _pendingPhoneApp = PhoneApp.Quest;
    }

    IEnumerator ExplainPhoneApps()
    {
        // 기존 앱 열기/닫기는 BaseView에 맡기고, 튜토리얼은 순서와 허용 입력만 관리한다.
        _scene.Base.CloseAllApps();
        LockLessonInputs(_phoneButton);
        LockLessonInputs(_phoneAppGroup);
        LockLessonInputs(_inventoryView);
        LockLessonInputs(_questView);

        _phoneLesson = PhoneLesson.OpenPhone;
        FocusLessonButton(_phoneButton, "휴대폰 버튼을 눌러 앱 목록을 열어보세요.");
        while (IsActive && !_phoneAppGroup.gameObject.activeInHierarchy) yield return null;
        if (!IsActive) yield break;

        yield return ExplainPhoneApp(PhoneApp.Inventory, _inventoryAppButton, _inventoryView, _inventoryCloseButton, _inventoryDialogue);
        if (!IsActive) yield break;
        yield return ExplainPhoneApp(PhoneApp.Quest, _questAppButton, _questView, _questCloseButton, _questDialogue);
        if (!IsActive) yield break;

        _tutorialView.HideInstruction();
        _phoneLesson = PhoneLesson.None;
        RestoreLessonInputs();
        if (_phoneAppGroup.gameObject.activeInHierarchy) _scene.Base.ToggleAppGroup();
    }

    IEnumerator ExplainPhoneApp(PhoneApp app, Button appButton, Component view, Button closeButton, TutorialDialogueData dialogue)
    {
        bool inventory = app == PhoneApp.Inventory;
        string title = inventory ? "인벤토리" : "퀘스트";
        _pendingPhoneApp = PhoneApp.None;
        _phoneLesson = inventory ? PhoneLesson.Inventory : PhoneLesson.Quest;
        FocusLessonButton(appButton, title + " 버튼을 눌러 확인해보세요.");
        while (IsActive && _pendingPhoneApp != app) yield return null;
        if (!IsActive) yield break;

        _pendingPhoneApp = PhoneApp.None;
        SetLessonInputTarget(null);
        _tutorialView.HideInstruction();
        _phoneLesson = inventory ? PhoneLesson.InventoryDialogue : PhoneLesson.QuestDialogue;
        var page = view.gameObject;
        var pageGroup = page.GetComponent<CanvasGroup>();
        var entrance = page.GetComponent<CommonWindowEntranceView>();
        var motionGroup = entrance != null && entrance.WindowMotion != null
            ? entrance.WindowMotion.GetComponent<CanvasGroup>() : null;
        // 앱 루트와 내부 창이 모두 입력을 복구한 뒤에 대화를 열어 정상 상태를 저장한다.
        while (IsActive && page.activeInHierarchy &&
               ((entrance != null && entrance.IsEntering) ||
                (pageGroup != null && (!pageGroup.interactable || !pageGroup.blocksRaycasts)) ||
                (motionGroup != null && (!motionGroup.interactable || !motionGroup.blocksRaycasts))))
            yield return null;
        if (!IsActive) yield break;
        LockLessonInputs(view); // 앱을 열 때 생성된 슬롯도 안내 중에는 선택하지 못하게 한다.
        yield return PlayDialogue(dialogue, true);
        if (!IsActive) yield break;

        _phoneLesson = inventory ? PhoneLesson.CloseInventory : PhoneLesson.CloseQuest;
        FocusLessonButton(closeButton, title + " 안내를 확인했으면 뒤로가기 버튼을 눌러주세요.");
        while (IsActive && page.activeInHierarchy) yield return null;
        _tutorialView.HideInstruction();
    }

    public void CompleteDiveAttempt()
    {
        if (!IsActive || !AllowsDive) return;
        if (!_night)
        {
            SetStep(8);
            _manager.PlayerProgress.SetBoatTutorialStep(5);
        }
        StopTutorial();
    }
    public void CompleteTimeSkip() { if (IsActive && AllowsTimeSkip) StopTutorial(); }

    public void StopTutorial()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (!IsActive) return;
        IsActive = false;
        if (_ownsDialogue && _talk != null) _talk.EndTalk();
        _ownsDialogue = false;
        _shopLesson = ShopLesson.None;
        _upgradeLesson = UpgradeLesson.None;
        _phoneLesson = PhoneLesson.None;
        RestoreLessonInputs();
        if (_riceButton != null) _riceButton.onClick.RemoveListener(OnRiceSelected);
        if (_shopPurchaseButton != null) _shopPurchaseButton.onClick.RemoveListener(OnShopPurchaseSubmitted);
        _scene._boatNPCInteraction?.SetAllowedInteraction(NPCInteractType.None);
        if (_inventoryActivation != null) _inventoryActivation.Opened -= OnInventoryOpened;
        if (_questActivation != null) _questActivation.Opened -= OnQuestOpened;
        _inventoryActivation = null; _questActivation = null; _pendingPhoneApp = PhoneApp.None;
        _tutorialView.HideInstruction();
        if (_diveMarker != null) _diveMarker.SetActive(false);
        if (_scene.Store != null) _scene.Store.OnOpened -= OnShopOpened;
        if (_scene.UpgradeView != null) _scene.UpgradeView.OnOpened -= OnEquipmentOpened;
        if (_scene.UpgradeView != null) _scene.UpgradeView.OnUpgradeRequested -= OnEquipmentUpgradeRequested;
        if (_equipmentSlotButton != null) _equipmentSlotButton.onClick.RemoveListener(OnEquipmentSelected);
        if (_equipmentUpgradeButton != null) _equipmentUpgradeButton.onClick.RemoveListener(OnUpgradeButtonPressed);
        if (_equipmentConfirmButton != null) _equipmentConfirmButton.onClick.RemoveListener(OnUpgradeConfirmed);
        if (_scene.DiveObject != null) _scene.DiveObject.enabled = _diveWasEnabled;
        if (_phoneButton != null) _phoneButton.interactable = _phoneWasEnabled;
        if (_skipButton != null) _skipButton.interactable = _skipWasEnabled;
    }
    void OnDisable() => StopTutorial();
}

