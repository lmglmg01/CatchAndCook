using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 상점의 구매·판매 정보를 표시하고 거래 요청을 전달하는 뷰입니다.
/// </summary>
public class StoreView : MonoBehaviour
{

    [Serializable]
    public struct IngredientOffer
    {
        public IngredientItemData item;
        public Sprite icon;
        public UnityEngine.UI.Image image;
    }
    [SerializeField] IngredientOffer[] _ingredientOffers;
    int _ingredientIndex = -1;
    bool _ingredientTradeBusy;
    public bool IsIngredientPurchase => _ingredientOffers != null && _ingredientIndex >= 0 && _ingredientIndex < _ingredientOffers.Length && _selectedItem != null && _selectedItem == _ingredientOffers[_ingredientIndex].item;
    float _testTradeGold;
    Inventory IngredientInventory => GameManager.Instance != null ? GameManager.Instance.Inventory : null;

    void InitializeIngredients()
    {
        if (_ingredientOffers == null) return;
        for (int i = 0; i < _ingredientOffers.Length; i++)
        {
            var offer = _ingredientOffers[i];
            if (offer.image == null) continue;
            offer.image.sprite = offer.icon;
            offer.image.preserveAspect = true;
            offer.image.type = UnityEngine.UI.Image.Type.Simple;
            offer.image.color = offer.item != null ? Color.white : new Color(.55f, .55f, .55f, 1);
            offer.image.raycastTarget = true;
            var button = offer.image.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = offer.image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = offer.image;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.interactable = true;
            int index = i;
            button.onClick.AddListener(() => SelectIngredient(index));
            var badge = offer.image.transform.Find("RarityBadge") ?? offer.image.transform.parent?.Find("RarityBadge");
            if (badge != null)
            {
                badge.gameObject.SetActive(offer.item == null);
                var label = badge.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "소진";
            }
        }
    }

    public void SelectIngredient(int index)
    {
        if (!_isPurchasePage || _ingredientOffers == null || index < 0 || index >= _ingredientOffers.Length) return;
        var offer = _ingredientOffers[index];
        if (offer.item == null)
        {
            ClearSelection();
            if (_detailsWindow != null) _detailsWindow.SetActive(true);
            if (_selectedItemNameText != null) _selectedItemNameText.text = string.Empty;
            if (_selectedItemContentText != null) _selectedItemContentText.text = string.Empty;
            if (_tradeResultText != null) _tradeResultText.text = "아이템 데이터 연결이 필요합니다.";
            return;
        }
        ClearSelection();
        SelectItem(offer.item);
        _ingredientIndex = index;
        _selectedUnitPrice = offer.item.SellPrice;
        RefreshSelectedItemIcon();
        RefreshAmount();
    }

    // 미리보기 거래는 표시용 목록과 골드만 변경하고 실제 인벤토리는 사용하지 않습니다.
    void BuyTestIngredient()
    {
        float total = _selectedUnitPrice * _amount;
        if (!IsIngredientPurchase || _amount <= 0 || _amount > MaximumAmount ||
            total < 0 || float.IsNaN(total) || float.IsInfinity(total) || total > _testTradeGold)
        {
            ShowTradeResult(false);
            return;
        }
        var item = _selectedItem;
        int index = _ingredientIndex;
        var updated = new List<Inventory.BagItem>(_sellItems);
        int existing = updated.FindIndex(entry => entry.ItemData == item);
        int count = GetOwnedCount(item) + _amount;
        if (existing >= 0) updated[existing] = new Inventory.BagItem(item, count);
        else updated.Add(new Inventory.BagItem(item, count));
        _testTradeGold -= total;
        SetSellItems(updated);
        SelectIngredient(index);
        SetGold(_testTradeGold);
        ShowTradeResult(true);
        if (_tradeResultText != null) _tradeResultText.text = "테스트 구매 완료";
    }

    void BuyIngredient()
    {
        if (_ingredientTradeBusy) return;
        if (_testPreviewActive) { BuyTestIngredient(); return; }
        var inventory = IngredientInventory;
        var item = _selectedItem as IngredientItemData;
        float total = _selectedUnitPrice * _amount;
        if (inventory == null || item == null || _amount <= 0 || _amount > MaximumAmount ||
            total < 0 || float.IsNaN(total) || float.IsInfinity(total) || !inventory.CanAdd(item, _amount))
        { ShowTradeResult(false); return; }
        // Changed 콜백이 선택 상태를 갱신할 수 있으므로 거래 정보를 먼저 보관합니다.
        int amount = _amount;
        int index = _ingredientIndex;
        _ingredientTradeBusy = true;
        bool success = false;
        try
        {
            if (inventory.TrySpendGold(total))
            {
                success = inventory.TryAdd(item, amount);
                if (!success) inventory.TryAddGold(total);
            }
            SelectIngredient(index);
            SetGold(inventory.Gold);
            ShowTradeResult(success);
        }
        finally { _ingredientTradeBusy = false; }
    }

    #region 멤버

    #region UI 연결 및 표시 설정

    // 상점 전체 창을 열고 닫을 때 활성화/비활성화할 를 연결합니다.
    [SerializeField] GameObject _mainWindow;
    // 구매 탭으로 전환하는 Button 컴포넌트를 연결합니다.
    [SerializeField] UnityEngine.UI.Button _purchasePageButton;
    // 구매 탭에서 보여 줄 상품 목록 패널 GameObject를 연결합니다.
    [SerializeField] GameObject _purchasePage;
    // 구매 상품 아이콘을 표시할 Image들을 슬롯 순서대로 연결합니다. 각 Image 자신 또는 부모에 상품 선택용 Button이 있어야 하며, 배열 길이가 구매 슬롯 수가 됩니다.
    [SerializeField] UnityEngine.UI.Image[] _itemIconImages = new UnityEngine.UI.Image[3];
    // 판매 탭으로 전환하는 Button 컴포넌트를 연결합니다.
    [SerializeField] UnityEngine.UI.Button _sellPageButton;
    // 판매 탭에서 보여 줄 보유 아이템 목록 패널 GameObject를 연결합니다.
    [SerializeField] GameObject _sellPage;
    // 씬에 미리 배치한 판매 슬롯의 SellItemSlotView들을 표시 순서대로 연결합니다. 복제 원본은 제외하며, 전부 동적 생성할 경우 비워 둘 수 있습니다.
    [SerializeField] SellItemSlotView[] _sellItemSlots;
    // 기존 판매 슬롯이 부족할 때 복제할 SellItemSlotView 프리팹 또는 템플릿을 연결합니다. 원본은 코드에서 비활성화되므로 실제 표시 슬롯과 구분합니다.
    [SerializeField] SellItemSlotView _sellItemSlotPrefab;
    // 추가 판매 슬롯을 생성해 자식으로 배치할 Transform을 연결합니다. 스크롤 목록을 사용한다면 해당 Scroll View의 Content를 연결합니다.
    [SerializeField] Transform _sellItemContent;
    // 상점 전체 창을 닫는 Button 컴포넌트를 연결합니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;
    // 아이템 선택 시 열리고 선택 취소 시 닫히는 상세 정보 패널 GameObject를 연결합니다.
    [SerializeField] GameObject _detailsWindow;
    // 상세창 아이템 아이콘 Image를 연결합니다. 미연결 시 상세창 안의 ItemIcon 오브젝트에서 찾습니다.
    [SerializeField] UnityEngine.UI.Image _selectedItemIconImage;
    // 선택한 아이템의 이름을 표시할 TMP_Text(TextMeshProUGUI)를 연결합니다.
    [SerializeField] TMP_Text _selectedItemNameText;
    // 선택한 아이템의 설명을 표시할 TMP_Text(TextMeshProUGUI)를 연결합니다.
    [SerializeField] TMP_Text _selectedItemContentText;
    // 선택한 아이템의 단가에 거래 수량을 곱한 총액을 표시할 TMP_Text를 연결합니다. 선택이나 유효한 가격이 없으면 하이픈(-)을 표시합니다.
    [SerializeField] TMP_Text _selectedItemPrice;
    // 현재 탭에 따라 선택한 아이템과 수량의 구매 또는 판매 요청을 보내는 확정 Button을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _purchaseOrSellButton;
    // 거래 확정 버튼의 문구를 구매/판매로 바꿔 표시할 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _purchaseOrSellText;
    // 아이템 선택을 해제하고 상세 정보 창을 닫는 취소 Button을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _cancelButton;
    // 구매/판매 수량을 직접 입력할 TMP_InputField를 연결합니다. 수량은 1~99이며 판매 시 보유 수량 이내로 제한됩니다.
    [SerializeField] TMP_InputField _sellAmountInputField;
    // 현재 구매/판매 수량을 별도로 표시할 TMP_Text를 연결합니다. 입력 필드와 별개의 수량 표시가 필요할 때 사용합니다.
    [SerializeField] TMP_Text _sellAmountText;
    // 구매/판매 수량을 1개 줄이는 Button을 연결합니다. 선택한 아이템이 없거나 수량이 1이면 비활성화됩니다.
    [SerializeField] UnityEngine.UI.Button _decreaseAmountButton;
    // 구매/판매 수량을 1개 늘리는 Button을 연결합니다. 선택한 아이템이 없거나 최대 수량에 도달하면 비활성화됩니다.
    [SerializeField] UnityEngine.UI.Button _increaseAmountButton;
    // 현재 보유 골드를 G 단위로 표시할 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _goldText;
    // 거래 완료 또는 거래 불가 결과 메시지를 표시할 TMP_Text를 연결합니다. 아이템을 선택하면 메시지가 지워집니다.
    [SerializeField] TMP_Text _tradeResultText;

    #endregion

    #region 런타임 상태

    readonly List<UtilityItemData> _purchaseItems = new List<UtilityItemData>(); // 구매 아이템 목록입니다.
    readonly List<Inventory.BagItem> _sellItems = new List<Inventory.BagItem>(); // 판매 아이템 목록입니다.
    readonly List<SellItemSlotView> _slots = new List<SellItemSlotView>(); // 슬롯 목록입니다.
    readonly List<UnityEngine.UI.Button> _purchaseButtons = new List<UnityEngine.UI.Button>(); // 구매 버튼 목록입니다.
    readonly List<UnityAction> _purchaseCallbacks = new List<UnityAction>(); // 구매 콜백 목록입니다.
    bool _isPurchasePage = true; // 구매 페이지 여부입니다.
    ItemData _selectedItem; // 선택 아이템 값입니다.
    int _amount = 1; // 수량 값입니다.
    float _selectedUnitPrice; // 선택 단위 가격 값입니다.

    #endregion

    #region 이벤트 액션

    public event Action<UtilityItemData, int> OnPurchase; // 구매 알림입니다.
    public event Action<ItemData, int> OnSell; // 판매 알림입니다.
    public event Action OnOpened; // 열림 알림입니다.
    public event Action<ItemData> OnSellItemSelected; // 판매 아이템 선택 알림입니다.

    #endregion

    #region 런타임 상태

    /// <summary>
    /// 상점 버튼과 슬롯 입력을 연결하고 초기 표시 상태를 설정합니다.
    /// </summary>
    bool _storeInitialized;

    #endregion

    #region 임시 데이터 및 테스트 멤버

    // 테스트 객체와 원본 목록은 메모리에만 보관합니다. 실제 아이템 에셋은 수정하지 않습니다.
    // 실제 플레이와 같은 상품 문구·아이콘을 사용하는 미리보기 전용 원본입니다.
    [SerializeField] UtilityItemData[] _previewPurchaseItems;
    [SerializeField] Inventory.BagItem[] _previewSellItems;
    [SerializeField, Min(0f)] float _previewGold = 10000f;
    [NonSerialized] bool _testPreviewActive;
    bool _testSavedRootActive; // 테스트 저장 루트 활성 여부입니다.
    UtilityItemData[] _testSavedPurchase; // 테스트 저장 구매 목록입니다.
    Inventory.BagItem[] _testSavedSell; // 테스트 저장 판매 목록입니다.
    ItemData _testSavedSelection; // 테스트 저장 선택 값입니다.
    int _testSavedAmount; // 테스트 저장 수량 값입니다.
    float _testSavedUnitPrice; // 테스트 저장 단위 가격 값입니다.
    // _testSavedPurchasePage: 테스트 저장 구매 페이지 여부입니다.
    // _testSavedMainActive: 테스트 저장 메인 활성 여부입니다.
    // _testSavedDetailActive: 테스트 저장 상세 활성 여부입니다.
    bool _testSavedPurchasePage, _testSavedMainActive, _testSavedDetailActive;
    // _testSavedGold: 테스트 저장 골드 값입니다.
    // _testSavedResult: 테스트 저장 결과 값입니다.
    string _testSavedGold, _testSavedResult;
    readonly List<ScriptableObject> _testCreatedItems = new List<ScriptableObject>(); // 테스트 생성 아이템 목록입니다.
    readonly List<SellItemSlotView> _testEditorSlots = new List<SellItemSlotView>(); // 테스트 에디터 슬롯 목록입니다.
    readonly List<SellItemSlotView> _testPreviewSlots = new List<SellItemSlotView>(); // 테스트 미리보기 슬롯 목록입니다.
    SellItemSlotView[] _testOriginalSlots; // 테스트 원본 슬롯 목록입니다.
    ItemData[] _testOriginalSlotItems; // 테스트 원본 슬롯 아이템 목록입니다.
    int[] _testOriginalSlotCounts; // 테스트 원본 슬롯 개수 목록입니다.
    bool[] _testOriginalSlotActive; // 테스트 원본 슬롯 활성 목록입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 구매 슬롯 개수 값입니다.
    /// </summary>
    public int PurchaseSlotCount => _itemIconImages == null ? 0 : _itemIconImages.Length;
    // 표시 전용 읽기 API. 거래 상태를 변경하지 않습니다.
    public bool IsPurchasePage => _isPurchasePage;

    /// <summary>
    /// 선택 아이템 값입니다.
    /// </summary>
    public ItemData SelectedItem => _selectedItem;

    /// <summary>
    /// 선택 보유 개수 값입니다.
    /// </summary>
    public int SelectedOwnedCount => GetOwnedCount(_selectedItem);

    /// <summary>
    /// 선택 수량 값입니다.
    /// </summary>
    public int SelectedAmount => _amount;

    /// <summary>
    /// 구매 아이템 목록입니다.
    /// </summary>
    public IReadOnlyList<UtilityItemData> PurchaseItems => _purchaseItems.AsReadOnly();

    /// <summary>
    /// 최대 수량 값입니다.
    /// </summary>
    int MaximumAmount => IsIngredientPurchase ? IngredientMaximumAmount : _isPurchasePage ? 1 : Mathf.Min(99, GetOwnedCount(_selectedItem));

    int IngredientMaximumAmount
    {
        get
        {
            var inventory = IngredientInventory;
            if (_selectedItem == null || (!_testPreviewActive && inventory == null)) return 0;
            float price = _selectedItem.SellPrice;
            if (price < 0 || float.IsNaN(price) || float.IsInfinity(price)) return 0;
            int affordable = price > 0 ? (int)Mathf.Min(99f, Mathf.Floor((_testPreviewActive ? _testTradeGold : inventory.Gold) / price)) : 99;
            return Mathf.Min(affordable, int.MaxValue - (_testPreviewActive ? GetOwnedCount(_selectedItem) : inventory.GetCount(_selectedItem)));
        }
    }
    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// UI 참조와 입력 연결을 초기화합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeButton, _cancelButton);
        InitializeStore();
        if (_tradeResultText != null) _tradeResultText.gameObject.SetActive(false);
    }

    // Play 종료·씬 전환·컴포넌트 비활성화 때도 테스트 객체와 원본 상태를 정리합니다.

    /// <summary>
    /// 뷰가 꺼질 때 이벤트와 진행 중인 표시를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        EndStoreTestPreview();
    }

    /// <summary>
    /// 상점 버튼과 슬롯에 연결한 이벤트 처리를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        EndStoreTestPreview();
        if (_purchasePageButton != null)
        {
            _purchasePageButton.onClick.RemoveListener(ShowPurchasePage);
        }

        if (_sellPageButton != null)
        {
            _sellPageButton.onClick.RemoveListener(ShowSellPage);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(ClearSelection);
        }

        if (_purchaseOrSellButton != null)
        {
            _purchaseOrSellButton.onClick.RemoveListener(Submit);
        }

        if (_decreaseAmountButton != null)
        {
            _decreaseAmountButton.onClick.RemoveListener(DecreaseAmount);
        }

        if (_increaseAmountButton != null)
        {
            _increaseAmountButton.onClick.RemoveListener(IncreaseAmount);
        }

        if (_sellAmountInputField != null)
        {
            _sellAmountInputField.onValueChanged.RemoveListener(OnAmountChanged);
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _purchaseButtons.Count; i++)
        {
            if (_purchaseButtons[i] != null)
            {
                _purchaseButtons[i].onClick.RemoveListener(_purchaseCallbacks[i]);
            }
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                slot.OnItemSelected -= ToggleSellItem;
            }
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 상점 디자인 뷰에 현재 표시 상태의 갱신을 요청합니다.
    /// </summary>
    void RefreshStorePresentation()
    {
        var screen = GetComponentInChildren<StoreScreenView>(true); // 화면 값입니다.
        if (screen != null)
        {
            screen.RefreshPresentation();
        }
    }

    /// <summary>
    /// 상점 페이지와 아이템 선택을 초기화합니다.
    /// </summary>
    public void Init()
    {
        SetPage();
        ClearSelection();
    }

    /// <summary>
    /// 구매 상품 목록을 저장하고 상품 아이콘과 버튼을 갱신합니다.
    /// </summary>
    /// <param name="items">아이템 목록입니다.</param>
    public void SetPurchaseItems(IReadOnlyList<UtilityItemData> items)
    {
        _purchaseItems.Clear();
        if (items != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < items.Count; i++)
            {
                _purchaseItems.Add(items[i]);
            }
        }

        // i: 반복 값입니다.
        for (int i = 0; i < PurchaseSlotCount; i++)
        {
            var icon = _itemIconImages[i]; // 아이콘 값입니다.
            var item = i < _purchaseItems.Count ? _purchaseItems[i] : null; // 아이템 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (icon == null)
            {
                continue;
            }

            icon.sprite = item != null ? item.ItemIcon : null;
            icon.enabled = icon.sprite != null;
            if (i < _purchaseButtons.Count && _purchaseButtons[i] != null)
            {
                _purchaseButtons[i].gameObject.SetActive(item != null);
            }
        }

        if (_isPurchasePage)
        {
            ClearSelection();
        }

        RefreshStorePresentation();
    }

    /// <summary>
    /// 판매 가능한 보유 목록을 저장하고 판매 슬롯을 재사용하거나 생성합니다.
    /// </summary>
    /// <param name="items">아이템 목록입니다.</param>
    public void SetSellItems(IReadOnlyList<Inventory.BagItem> items)
    {
        _sellItems.Clear();
        if (items != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ItemData != null && items[i].Count > 0)
                {
                    _sellItems.Add(items[i]);
                }
            }
        }

        if (_sellItemSlotPrefab != null)
        {
            _sellItemSlotPrefab.gameObject.SetActive(false);
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _sellItems.Count; i++)
        {
            if (i >= _slots.Count)
            {
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (_sellItemSlotPrefab == null || _sellItemContent == null)
                {
                    Debug.LogWarning("Assign StoreView sell slot prefab and content to display additional items.", this);
                    break;
                }

                RegisterSlot(Instantiate(_sellItemSlotPrefab, _sellItemContent, false));
            }

            _slots[i].Bind(_sellItems[i].ItemData, _sellItems[i].Count);
            _slots[i].gameObject.SetActive(true);
        }

        // i: 반복 값입니다.
        for (int i = _sellItems.Count; i < _slots.Count; i++)
        {
            _slots[i].gameObject.SetActive(false);
        }

        if (!_isPurchasePage)
        {
            ClearSelection();
        }

        RefreshStorePresentation();
    }

    /// <summary>
    /// 현재 보유 골드를 표시합니다.
    /// </summary>
    /// <param name="gold">골드 값입니다.</param>
    public void SetGold(float gold)
    {
        if (_goldText != null)
        {
            _goldText.text = $"{gold:0.##}";
        }
    }

    /// <summary>
    /// 선택된 판매 아이템의 단가를 반영해 총액을 갱신합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    /// <param name="unitPrice">단위 가격 값입니다.</param>
    public void SetSellUnitPrice(ItemData item, float unitPrice)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_isPurchasePage || _selectedItem != item)
        {
            return;
        }

        _selectedUnitPrice = unitPrice;
        RefreshAmount();
    }

    /// <summary>
    /// 상점 창을 열거나 닫고 페이지와 선택 상태를 정리합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetStore(bool isOn)
    {
        // 버튼·ESC·외부 닫기를 같은 퇴장 경로로 연결하고 원래 처리 순서는 보존합니다.
        var common = GetComponent<CommonWindowEntranceView>();
        if (!isOn && common != null && common.TryPlayExit(() => SetStore(false))) return;

        if (isOn)
        {
            InitializeStore();
            gameObject.SetActive(true);
            common?.PlayEntrance();
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_testPreviewActive && !isOn)
        {
            EndStoreTestPreview();
            return;
        }

        if (isOn)
        {
            _isPurchasePage = true;
            SetPage();
        }

        if (_mainWindow != null)
        {
            _mainWindow.SetActive(isOn);
        }

        if (isOn && !_testPreviewActive)
        {
            OnOpened?.Invoke();
        }
        else
        {
            ClearSelection();
        }

        if (!isOn)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 구매 상품을 선택하고 단가와 수량 표시를 갱신합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    // 같은 상품을 다시 클릭하면 상세창을 닫습니다. 거래 후 갱신은 기존 선택 함수를 사용합니다.
    void TogglePurchaseItem(int index)
    {
        if (!_isPurchasePage || index < 0 || index >= _purchaseItems.Count || index >= PurchaseSlotCount)
            return;
        var item = _purchaseItems[index];
        if (item != null && item == _selectedItem)
            ClearSelection();
        else
            SelectPurchaseItem(index);
    }

    void ToggleSellItem(ItemData item)
    {
        if (_isPurchasePage || item == null)
            return;
        if (item == _selectedItem)
            ClearSelection();
        else
            SelectSellItem(item);
    }

    public void SelectPurchaseItem(int index)
    {
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (!_isPurchasePage || index < 0 || index >= _purchaseItems.Count || index >= PurchaseSlotCount)
        {
            return;
        }

        var item = _purchaseItems[index]; // 아이템 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (item == null)
        {
            return;
        }

        SelectItem(item);
        _selectedUnitPrice = item.PurchasePrice;
        RefreshAmount();
    }

    /// <summary>
    /// 보유 아이템을 선택하고 실제 판매 단가를 외부에 요청합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    void SelectSellItem(ItemData item)
    {
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (_isPurchasePage || GetOwnedCount(item) <= 0)
        {
            return;
        }

        ClearSelection();
        SelectItem(item);
        // PlayScene supplies the actual price, including Shop's default sell price.
        _selectedUnitPrice = float.NaN;
        RefreshAmount();
        if (_testPreviewActive)
        {
            SetSellUnitPrice(item, item.SellPrice);
        }
        else
        {
            OnSellItemSelected?.Invoke(item);
        }
    }

    /// <summary>
    /// 선택 아이템의 이름, 설명과 아이콘을 상세창에 표시합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    void SelectItem(ItemData item)
    {
        _ingredientIndex = -1;
        _selectedItem = item;
        _amount = 1;
        if (_detailsWindow != null)
        {
            _detailsWindow.SetActive(true);
        }

        if (_selectedItemNameText != null)
        {
            _selectedItemNameText.text = item.ItemName;
        }

        if (_selectedItemContentText != null)
        {
            _selectedItemContentText.text = item.ItemDesc;
        }

        RefreshSelectedItemIcon();
        if (_tradeResultText != null)
        {
            _tradeResultText.text = string.Empty;
        }
    }

    /// <summary>
    /// 아이템 선택을 해제하고 상세창과 수량 표시를 초기화합니다.
    /// </summary>
    void ClearSelection()
    {
        _ingredientIndex = -1;
        _selectedItem = null;
        _amount = 1;
        RefreshSelectedItemIcon();
        if (_detailsWindow != null)
        {
            _detailsWindow.SetActive(false);
        }

        RefreshAmount();
    }

    // 구매/판매 공통 상세창에 선택한 아이템의 아이콘을 표시하고, 선택 해제 시 이전 아이콘을 지웁니다.

    /// <summary>
    /// 상세창 아이콘을 찾아 선택 아이템의 이미지 또는 빈 상태를 표시합니다.
    /// </summary>
    void RefreshSelectedItemIcon()
    {
        if (_selectedItemIconImage == null && _detailsWindow != null)
        {
            // image: 이미지 값입니다.
            foreach (var image in _detailsWindow.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (image.name != "ItemIcon")
                {
                    continue;
                }

                _selectedItemIconImage = image;
                break;
            }
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_selectedItemIconImage == null)
        {
            return;
        }

        _selectedItemIconImage.sprite = IsIngredientPurchase ? _ingredientOffers[_ingredientIndex].icon : _selectedItem != null ? _selectedItem.ItemIcon : null;
        _selectedItemIconImage.enabled = _selectedItemIconImage.sprite != null;
        _selectedItemIconImage.preserveAspect = true;
    }

    /// <summary>
    /// 입력 수량을 유효 범위로 제한하고 거래 표시를 갱신합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    void OnAmountChanged(string value)
    {
        if (!int.TryParse(value, out _amount))
        {
            _amount = 1;
        }

        _amount = Mathf.Clamp(_amount, 1, Mathf.Max(1, MaximumAmount));
        RefreshAmount();
    }

    /// <summary>
    /// 거래 수량을 최소 1개까지 줄이고 표시를 갱신합니다.
    /// </summary>
    void DecreaseAmount()
    {
        _amount = Mathf.Max(1, _amount - 1);
        RefreshAmount();
    }

    /// <summary>
    /// 거래 수량을 허용된 최대값까지 늘리고 표시를 갱신합니다.
    /// </summary>
    void IncreaseAmount()
    {
        _amount = Mathf.Min(Mathf.Max(1, MaximumAmount), _amount + 1);
        RefreshAmount();
    }

    /// <summary>
    /// 거래 수량, 총액과 거래 및 증감 버튼 상태를 갱신합니다.
    /// </summary>
    void RefreshAmount()
    {
        if (_purchaseOrSellText != null) _purchaseOrSellText.text = (_isPurchasePage || IsIngredientPurchase) ? "구매하기" : "판매하기";
        // 유틸리티 구매 요청은 항상 1개입니다. 판매는 기존 보유 수량 제한을 유지합니다.
        _amount = Mathf.Clamp(_amount, 1, Mathf.Max(1, MaximumAmount));
        if (_sellAmountInputField != null)
        {
            _sellAmountInputField.SetTextWithoutNotify(_amount.ToString());
        }

        if (_sellAmountText != null)
        {
            _sellAmountText.text = _amount.ToString();
        }

        float total = _selectedUnitPrice * _amount; // 전체 값입니다.
        bool valid = total >= 0f && !float.IsNaN(total) && !float.IsInfinity(total); // 유효 여부입니다.
        if (_selectedItemPrice != null)
        {
            _selectedItemPrice.text = _selectedItem != null && valid ? $"{total:0.##}" : "-";
        }

        if (_purchaseOrSellButton != null)
        {
            _purchaseOrSellButton.interactable = _selectedItem != null && valid && _amount <= MaximumAmount;
        }

        if (_decreaseAmountButton != null)
        {
            _decreaseAmountButton.interactable = _selectedItem != null && _amount > 1;
        }

        if (_increaseAmountButton != null)
        {
            _increaseAmountButton.interactable = _selectedItem != null && _amount < MaximumAmount;
        }

        RefreshStorePresentation();
    }

    #endregion

    #region 초기화 및 연결

    // 비활성 오브젝트의 첫 ContextMenu 호출에서도 Awake보다 먼저 안전하게 준비할 수 있습니다.

    /// <summary>
    /// 상점 슬롯과 수량 입력·거래 버튼을 연결합니다.
    /// </summary>
    void InitializeStore()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_storeInitialized)
        {
            return;
        }

        _storeInitialized = true;
        InitializeIngredients();
        if (_purchasePageButton != null)
        {
            _purchasePageButton.onClick.AddListener(ShowPurchasePage);
        }

        if (_sellPageButton != null)
        {
            _sellPageButton.onClick.AddListener(ShowSellPage);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.AddListener(ClearSelection);
        }

        if (_purchaseOrSellButton != null)
        {
            _purchaseOrSellButton.onClick.AddListener(Submit);
        }

        if (_decreaseAmountButton != null)
        {
            _decreaseAmountButton.onClick.AddListener(DecreaseAmount);
        }

        if (_increaseAmountButton != null)
        {
            _increaseAmountButton.onClick.AddListener(IncreaseAmount);
        }

        if (_sellAmountInputField != null)
        {
            _sellAmountInputField.onValueChanged.AddListener(OnAmountChanged);
        }

        // i: 반복 값입니다.
        for (int i = 0; i < PurchaseSlotCount; i++)
        {
            var icon = _itemIconImages[i]; // 아이콘 값입니다.
            var button = icon != null ? icon.GetComponentInParent<UnityEngine.UI.Button>(true) : null; // 버튼 값입니다.
            int index = i; // 인덱스 값입니다.
            UnityAction callback = () => TogglePurchaseItem(index); // 콜백 값입니다.
            _purchaseButtons.Add(button);
            _purchaseCallbacks.Add(callback);
            if (button != null)
            {
                DisableLegacyPurchaseSelection(button);
                button.onClick.AddListener(callback);
            }
        }

        if (_sellItemSlots != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _sellItemSlots)
            {
                if (slot != null && slot != _sellItemSlotPrefab && !_slots.Contains(slot))
                {
                    RegisterSlot(slot);
                }
            }
        }

        Init();
        SetStore(false);
    }

    /// <summary>
    /// 프리팹에 남은 구매 선택 이벤트만 꺼서 한 클릭에 선택과 해제가 연속 실행되지 않도록 합니다.
    /// 다른 대상과 기능에 연결된 이벤트는 유지하고, 구매 선택은 TogglePurchaseItem에서 처리합니다.
    /// </summary>
    /// <param name="button">구매 상품을 선택하는 버튼입니다.</param>
    void DisableLegacyPurchaseSelection(UnityEngine.UI.Button button)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == nameof(SelectPurchaseItem))
            {
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
        }
    }

    /// <summary>
    /// 판매 슬롯을 등록하고 아이템 선택 이벤트를 연결합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void RegisterSlot(SellItemSlotView slot)
    {
        _slots.Add(slot);
        slot.OnItemSelected += ToggleSellItem;
    }

    /// <summary>
    /// Canvas_Base의 OwnGoldWindow에 있는 재화 텍스트를 상점에 연결합니다.
    /// </summary>
    /// <param name="goldText">골드 텍스트입니다.</param>
    public void BindGoldText(TMP_Text goldText)
    {
        // BoardCanvasView가 같은 씬의 공통 재화 UI를 찾아 전달합니다.
        _goldText = goldText;
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 판매 목록에서 지정한 아이템의 보유 수량을 조회합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    int GetOwnedCount(ItemData item)
    {
        // entry: 항목 값입니다.
        foreach (var entry in _sellItems)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (entry.ItemData == item)
            {
                return entry.Count;
            }
        }

        return 0;
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 거래 결과를 표시하고 구매 성공 시 수량을 1로 초기화합니다.
    /// </summary>
    /// <param name="succeeded">성공 여부입니다.</param>
    public void ShowTradeResult(bool succeeded)
    {
        if (_tradeResultText != null)
        {
            _tradeResultText.text = succeeded ? "거래 완료" : "거래할 수 없습니다.";
        }

        // 구매 성공을 확인한 뒤에만 수량과 총액을 최소 수량 기준으로 갱신합니다.
        if (succeeded && _isPurchasePage)
        {
            _amount = 1;
            RefreshAmount();
        }
    }

    /// <summary>
    /// 상점 전체 창을 닫습니다.
    /// </summary>
    void Close()
    {
        SetStore(false);
    }

    /// <summary>
    /// 구매 페이지로 전환합니다.
    /// </summary>
    void ShowPurchasePage()
    {
        _isPurchasePage = true;
        SetPage();
    }

    /// <summary>
    /// 판매 페이지로 전환합니다.
    /// </summary>
    void ShowSellPage()
    {
        _isPurchasePage = false;
        SetPage();
    }

    /// <summary>
    /// 선택한 구매 또는 판매 페이지를 표시하고 선택을 해제합니다.
    /// </summary>
    void SetPage()
    {
        if (_purchasePage != null)
        {
            _purchasePage.SetActive(_isPurchasePage);
        }

        if (_sellPage != null)
        {
            _sellPage.SetActive(!_isPurchasePage);
        }

        if (_purchaseOrSellText != null)
        {
            _purchaseOrSellText.text = _isPurchasePage ? "구매하기" : "판매하기";
        }

        ClearSelection();
    }

    #endregion

    #region 선택 및 외부 요청

    /// <summary>
    /// 유효한 선택과 수량으로 구매 또는 판매 요청 이벤트를 전달합니다.
    /// </summary>
    void Submit()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_ingredientTradeBusy) return;
        if (IsIngredientPurchase) { BuyIngredient(); return; }
        if (_selectedItem == null || _amount <= 0 || _amount > MaximumAmount || _selectedUnitPrice < 0f || float.IsNaN(_selectedUnitPrice) || float.IsInfinity(_selectedUnitPrice))
        {
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_testPreviewActive)
        {
            if (!_isPurchasePage)
            {
                ApplyTestSale();
            }

            ShowTradeResult(true);
            if (_tradeResultText != null)
            {
                _tradeResultText.text = "테스트 거래 완료";
            }

            return; // 실제 구매·판매 요청 이벤트는 호출하지 않습니다.
        }

        if (_isPurchasePage)
        {
            if (_selectedItem is UtilityItemData item)
            {
                OnPurchase?.Invoke(item, _amount);
            }
        }
        else
        {
            OnSell?.Invoke(_selectedItem, _amount);
        }
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// Inspector 컨텍스트 메뉴에서 실제 데이터와 분리한 임시 상품 상점을 엽니다.
    /// </summary>
    [ContextMenu("테스트/상점 열기")]
    public void TestOpenStore()
    {
        BeginStoreTestPreview();
    }

    /// <summary>
    /// Inspector 컨텍스트 메뉴에서 기존 상점 닫기 경로를 테스트합니다.
    /// </summary>
    [ContextMenu("테스트/상점 닫기")]
    public void TestCloseStore()
    {
        SetStore(false);
    }

    /// <summary>
    /// 임시 데이터가 있는 구매 화면을 표시합니다. 실제 구매 요청은 전달하지 않습니다.
    /// </summary>
    [ContextMenu("테스트/구매 화면 보기")]
    public void TestShowPurchasePage()
    {
        if (!_testPreviewActive)
        {
            TestOpenStore();
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_testPreviewActive)
        {
            return;
        }

        ShowPurchasePage();
        if (_purchaseItems.Count > 0)
        {
            SelectPurchaseItem(0);
        }
    }

    /// <summary>
    /// 임시 보유 목록이 있는 판매 화면을 표시합니다.
    /// </summary>
    [ContextMenu("테스트/판매 화면 보기")]
    public void TestShowSellPage()
    {
        if (!_testPreviewActive)
        {
            TestOpenStore();
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_testPreviewActive)
        {
            return;
        }

        ShowSellPage();
        if (_sellItems.Count > 0)
        {
            SelectSellItem(_sellItems[Mathf.Min(4, _sellItems.Count - 1)].ItemData);
        }
    }

    /// <summary>
    /// 원래 표시를 보관하고 메모리의 임시 상품으로 상점을 엽니다.
    /// </summary>
    void BeginStoreTestPreview()
    {
#if UNITY_EDITOR
        // 프로젝트 창의 프리팹 에셋이나 Prefab Mode에 임시 표시값을 저장하지 않습니다.
        if (UnityEditor.EditorUtility.IsPersistent(this) || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning("상점 테스트는 씬의 StoreView에서 실행해 주세요.", this);
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_testPreviewActive)
        {
            return;
        }

        InitializeStore();
        _testSavedRootActive = gameObject.activeSelf;
        _testSavedPurchase = _purchaseItems.ToArray();
        _testSavedSell = _sellItems.ToArray();
        _testSavedSelection = _selectedItem;
        _testSavedAmount = _amount;
        _testSavedUnitPrice = _selectedUnitPrice;
        _testSavedPurchasePage = _isPurchasePage;
        _testSavedMainActive = _mainWindow != null && _mainWindow.activeSelf;
        _testSavedDetailActive = _detailsWindow != null && _detailsWindow.activeSelf;
        _testSavedGold = _goldText != null ? _goldText.text : null;
        _testSavedResult = _tradeResultText != null ? _tradeResultText.text : null;
        _testOriginalSlots = GetComponentsInChildren<SellItemSlotView>(true);
        _testOriginalSlotItems = new ItemData[_testOriginalSlots.Length];
        _testOriginalSlotCounts = new int[_testOriginalSlots.Length];
        _testOriginalSlotActive = new bool[_testOriginalSlots.Length];
        // i: 반복 값입니다.
        for (int i = 0; i < _testOriginalSlots.Length; i++)
        {
            var slot = _testOriginalSlots[i]; // 슬롯 값입니다.
            _testOriginalSlotItems[i] = slot.Item;
            _testOriginalSlotCounts[i] = slot.Count;
            _testOriginalSlotActive[i] = slot.gameObject.activeSelf;
        }

        // Edit Mode에서는 Awake가 실행되지 않으므로 기존 슬롯을 표시용으로만 등록합니다.
        if (!Application.isPlaying && _sellItemSlots != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _sellItemSlots)
            {
                if (slot != null && slot != _sellItemSlotPrefab && !_slots.Contains(slot))
                {
                    _slots.Add(slot);
                    _testEditorSlots.Add(slot);
                }
            }
        }

        _testPreviewActive = true;
        try
        {
            gameObject.SetActive(true); // 루트가 비활성이어도 테스트 상점을 표시합니다.
            // 플레이 중에는 현재 상품을, 편집 모드에서는 같은 에셋을 연결한 목록을 복사합니다.
            var purchase = new List<UtilityItemData>();
            var purchaseSource = _testSavedPurchase != null && _testSavedPurchase.Length > 0
                ? _testSavedPurchase : _previewPurchaseItems;
            if (purchaseSource != null)
                foreach (var item in purchaseSource)
                    if (item != null) purchase.Add(CloneStorePreviewItem(item));
            var sell = new List<Inventory.BagItem>();
            IReadOnlyList<Inventory.BagItem> sellSource = Application.isPlaying && IngredientInventory != null
                ? IngredientInventory.Items : Application.isPlaying ? _testSavedSell : _previewSellItems;
            if (sellSource != null)
                foreach (var entry in sellSource)
                    if (entry.ItemData != null && entry.Count > 0)
                        sell.Add(new Inventory.BagItem(CloneStorePreviewItem(entry.ItemData), entry.Count));
            SetStore(true);
            SetPurchaseItems(purchase);
            SetSellItems(sell);
            // slot: 슬롯 값입니다.
            foreach (var slot in _slots)
            {
                if (slot != null && !_testPreviewSlots.Contains(slot))
                {
                    slot.SetTestPreview(true);
                    slot.OnTestItemSelected += ToggleSellItem;
                    _testPreviewSlots.Add(slot);
                }
            }

            _testTradeGold = Application.isPlaying && IngredientInventory != null ? IngredientInventory.Gold : _previewGold;
            SetGold(_testTradeGold);
            if (_purchaseItems.Count > 0)
            {
                SelectPurchaseItem(0);
            }
        }
        catch
        {
            EndStoreTestPreview();
            throw;
        }
#endif
    }

    /// <summary>
    /// 실제 상품의 문구·아이콘을 복사하되 테스트 거래가 원본에 영향을 주지 않게 합니다.
    /// </summary>
    /// <param name="source">플레이에서 사용하는 원본 상품입니다.</param>
#if UNITY_EDITOR
    T CloneStorePreviewItem<T>(T source) where T : ItemData
    {
        var item = Instantiate(source);
        item.hideFlags = HideFlags.HideAndDontSave;
        _testCreatedItems.Add(item);
        return item;
    }
#endif

    /// <summary>
    /// 테스트 세션의 표시용 보유 목록만 차감합니다. 실제 인벤토리와 거래 이벤트는 사용하지 않습니다.
    /// </summary>
    void ApplyTestSale()
    {
        var soldItem = _selectedItem; // 판매 아이템 값입니다.
        int soldAmount = _amount; // 판매 수량 값입니다.
        int remaining = 0; // 남은 값입니다.
        bool applied = false; // 적용 여부입니다.
        var updated = new List<Inventory.BagItem>(); // 갱신 값입니다.
        // entry: 항목 값입니다.
        foreach (var entry in _sellItems)
        {
            if (!applied && entry.ItemData == soldItem)
            {
                applied = true;
                remaining = Mathf.Max(0, entry.Count - soldAmount);
                if (remaining > 0)
                {
                    updated.Add(new Inventory.BagItem(entry.ItemData, remaining));
                }
            }
            else
            {
                updated.Add(entry);
            }
        }

        SetSellItems(updated); // 수량이 0인 상품은 목록에서 제거되고 남은 슬롯들이 다시 채워집니다.
        if (remaining > 0)
        {
            SelectSellItem(soldItem);
            _amount = Mathf.Min(soldAmount, remaining);
            RefreshAmount();
        }
    }

    /// <summary>
    /// 테스트 상품을 정리하고 기존 상점 표시를 복원합니다.
    /// </summary>
    void EndStoreTestPreview()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_testPreviewActive)
        {
            return;
        }

        _testPreviewActive = false;
        // slot: 슬롯 값입니다.
        foreach (var slot in _testPreviewSlots)
        {
            if (slot != null)
            {
                slot.OnTestItemSelected -= ToggleSellItem;
                slot.SetTestPreview(false);
            }
        }

        _testPreviewSlots.Clear();
        SetPurchaseItems(_testSavedPurchase);
        SetSellItems(_testSavedSell);
        _isPurchasePage = _testSavedPurchasePage;
        SetPage();
        if (_testSavedSelection != null)
        {
            SelectItem(_testSavedSelection);
        }

        _selectedUnitPrice = _testSavedUnitPrice;
        _amount = _testSavedAmount;
        RefreshAmount();
        if (_mainWindow != null)
        {
            _mainWindow.SetActive(_testSavedMainActive);
        }

        if (_detailsWindow != null)
        {
            _detailsWindow.SetActive(_testSavedDetailActive);
        }

        if (_goldText != null)
        {
            _goldText.text = _testSavedGold;
        }

        if (_tradeResultText != null)
        {
            _tradeResultText.text = _testSavedResult;
        }

        if (_testOriginalSlots != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _testOriginalSlots.Length; i++)
            {
                if (_testOriginalSlots[i] != null)
                {
                    _testOriginalSlots[i].Bind(_testOriginalSlotItems[i], _testOriginalSlotCounts[i]);
                    _testOriginalSlots[i].gameObject.SetActive(_testOriginalSlotActive[i]);
                }
            }
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in _testEditorSlots)
        {
            _slots.Remove(slot);
        }

        _testEditorSlots.Clear();
        // item: 아이템 값입니다.
        foreach (var item in _testCreatedItems)
        {
            if (item != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(item);
                }
                else
                {
                    DestroyImmediate(item);
                }
            }
        }

        _testCreatedItems.Clear();
        _testSavedPurchase = null;
        _testSavedSell = null;
        _testOriginalSlots = null;
        _testOriginalSlotItems = null;
        _testOriginalSlotCounts = null;
        _testOriginalSlotActive = null;
        if (!_testSavedRootActive && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    #endregion

}

