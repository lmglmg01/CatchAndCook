using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 보유 목록, 고정 장비 상태, 유틸리티 장착 선택을 표시합니다.
/// 장착 선택은 이 View가 살아 있는 동안 유지되는 UI 상태이며 게임 효과/저장은 수행하지 않습니다.
/// </summary>
public class InventoryView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("창과 고정 장비 상태")]
    // Inventory 전체 패널입니다. 비워 두면 이 컴포넌트의 GameObject를 사용합니다.
    [SerializeField] GameObject _mainWindow; // 메인 창 값입니다.
    // 캐릭터 일러스트, 장비 6칸, 유틸리티 2칸을 포함하는 항상 노출할 영역입니다.
    // 아이템 목록과 별도 영역으로 배치하고 탭별 패널 안에는 넣지 않습니다.
    [SerializeField] GameObject _equipmentStatusPanel;
    // 캐릭터 일러스트 Image입니다. 인스펙터에서 직접 스프라이트를 지정해도 됩니다.
    [SerializeField] UnityEngine.UI.Image _characterImage;
    // 순서: 근접 무기, 작살, 원거리 무기, 산소통, 잠수복, 적재량. 표시와 설명 확인만 가능합니다.
    [SerializeField] InventorySlotView[] _equipmentSlots = new InventorySlotView[6];
    // 고정 장비 상태 영역의 유틸리티 두 칸입니다. 목록에서 선택 후 이 칸을 누르면 장착/교환합니다.
    [SerializeField] InventorySlotView[] _utilitySlots = new InventorySlotView[2];
    // 인벤토리 전체 창을 닫는 Button입니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;

    [Header("보유 아이템과 탭")]
    // 선택 사항: 실제 보유 목록을 읽을 Inventory입니다. 변경은 하지 않으며, 미연결 시 SetItems로 데이터를 받습니다.
    [SerializeField] Inventory _inventory; // 인벤토리 값입니다.

    #endregion

    #region 임시 데이터 및 테스트 멤버

    [Header("UI 사전 디자인 미리보기")]
    // 실제 Inventory가 없는 UI 테스트 씬에서만 사용하는 보유 목록입니다. 실제 보유 데이터는 수정하지 않습니다.
    [SerializeField] bool _usePreviewItems; // 사용 미리보기 아이템 목록 여부입니다.
    [SerializeField] Inventory.BagItem[] _previewItems; // 미리보기 아이템 목록입니다.

    #endregion

    #region UI 연결 및 표시 설정

    // 일반 아이템 목록으로 전환할 Button입니다.
    [SerializeField] UnityEngine.UI.Button _normalTabButton;
    // 유틸리티 아이템 목록으로 전환할 Button입니다.
    [SerializeField] UnityEngine.UI.Button _utilityTabButton;
    // 현재 탭을 강조할 테두리 등의 자식 오브젝트입니다. 버튼 자체를 연결하지 않습니다.
    [SerializeField] GameObject _normalTabSelectedMark;
    // 유틸리티 탭의 선택 상태를 표시할 자식 오브젝트입니다.
    [SerializeField] GameObject _utilityTabSelectedMark;
    // 한 페이지에 미리 배치한 목록 슬롯을 좌상단부터 순서대로 연결합니다. 배열 길이가 페이지당 칸 수입니다.
    [SerializeField] InventorySlotView[] _itemSlots;
    // 현재 탭에 보유 아이템이 없을 때 표시할 안내 오브젝트입니다.
    [SerializeField] GameObject _emptyMessage;

    [Header("하단 페이지 이동")]
    // 이전 페이지로 이동하는 왼쪽 화살표 Button입니다.
    [SerializeField] UnityEngine.UI.Button _previousPageButton; // 이전 페이지 버튼입니다.
    // 다음 페이지로 이동하는 오른쪽 화살표 Button입니다.
    [SerializeField] UnityEngine.UI.Button _nextPageButton;
    // 현재 페이지 / 전체 페이지를 표시할 TMP_Text입니다.
    [SerializeField] TMP_Text _pageText;

    [Header("아이템 및 장비 설명")]
    // 보유 아이템을 클릭한 뒤에만 열리는 고정 상세페이지입니다. 커서를 옮겨도 유지됩니다.
    [SerializeField] GameObject _detailsPanel; // 상세 패널 값입니다.
    // 설명 영역에 표시할 아이템/장비 이름입니다.
    [SerializeField] TMP_Text _detailNameText;
    // 설명 영역에 표시할 아이템/장비 설명입니다.
    [SerializeField] TMP_Text _detailDescriptionText;
    // 설명 영역에 표시할 현재 장비 스탯입니다.
    [SerializeField] TMP_Text _detailStatsText;
    // 상세페이지의 아이콘과 보유 수량입니다.
    [SerializeField] UnityEngine.UI.Image _detailIconImage;
    [SerializeField] TMP_Text _detailCountText; // 상세 개수 텍스트입니다.

    [Header("1차 UI 에셋 표시")]
    [SerializeField] bool _assetInventoryLayout; // 새 배치를 사용할 때 켭니다. 일반 탭은 상세, 유틸리티 탭은 장비 영역을 표시합니다.
    [SerializeField] TMP_Text _detailPriceText; // 상세창의 아이템 판매가 표시 TMP_Text를 연결합니다. 거래 기능은 없습니다.
    [SerializeField] CanvasGroup _assetHiddenHud; // 인벤토리 뒤의 BasePanel CanvasGroup을 연결합니다. 창을 닫으면 이전 표시 상태를 복원합니다.

    #endregion

    #region 런타임 상태

    Vector3 _assetHudState; // 인벤토리를 열기 전 HUD의 투명도와 입력 상태입니다.
    bool _assetHudHidden; // HUD 상태를 중복 저장하지 않도록 관리합니다.

    #endregion

    #region UI 연결 및 표시 설정

    [SerializeField] Sprite[] _normalTabIcons = new Sprite[2]; // 일반 아이템, 유틸리티 순서의 기본 아이콘입니다.
    [SerializeField] Sprite[] _selectedTabIcons = new Sprite[2]; // 선택 시 사용할 반전 아이콘입니다.
    [SerializeField] Sprite _selectedTabSprite; // 선택된 탭의 밝은 배경 Sprite를 연결합니다.
    [SerializeField] Sprite _normalTabSprite; // 선택되지 않은 탭의 어두운 배경 Sprite를 연결합니다.
    // 상세페이지만 닫는 Button입니다.
    [SerializeField] UnityEngine.UI.Button _detailCloseButton;

    [Header("장비 및 유틸리티 간단한 툴팁")]
    // 장비, 장착 유틸리티와 보유 유틸리티 목록의 호버 툴팁 패널을 연결합니다. 클릭 상세창과 별개입니다.
    [SerializeField] GameObject _equipmentTooltipPanel; // 장비 툴팁 패널 값입니다.
    [SerializeField] TMP_Text _tooltipNameText; // 툴팁 이름 텍스트입니다.
    [SerializeField] TMP_Text _tooltipDescriptionText; // 툴팁 설명 텍스트입니다.
    // 마우스에서 툴팁이 떨어질 간격입니다. Canvas 기준 오른쪽·아래 방향으로 표시합니다.
    [SerializeField] Vector2 _tooltipPointerOffset = new Vector2(24f, -24f);
    // 툴팁이 화면 테두리에 붙지 않도록 확보할 여백입니다. UI 오브젝트 연결은 필요 없습니다.
    [SerializeField, Min(0f)] float _tooltipScreenPadding = 12f;

    #endregion

    #region 런타임 상태

    // 툴팁의 화면 경계 보정에 재사용하는 좌표 배열입니다. 인스펙터 연결은 필요 없습니다.
    readonly Vector3[] _tooltipCorners = new Vector3[4];
    readonly Vector3[] _tooltipCanvasCorners = new Vector3[4]; // 툴팁 캔버스 모서리 목록입니다.

    #endregion

    #region UI 연결 및 표시 설정

    // 선택한 유틸리티를 장착할 위치를 안내할 텍스트입니다.
    [SerializeField] TMP_Text _selectionHintText;

    #endregion

    #region 런타임 상태

    readonly List<Inventory.BagItem> _items = new List<Inventory.BagItem>(); // 아이템 목록입니다.
    readonly List<Inventory.BagItem> _filteredItems = new List<Inventory.BagItem>(); // 필터 결과 아이템 목록입니다.
    readonly HashSet<InventorySlotView> _registeredSlots = new HashSet<InventorySlotView>(); // 등록 슬롯 목록 값입니다.
    readonly UtilityItemData[] _equippedUtilities = new UtilityItemData[2]; // 장착 유틸리티 목록입니다.
    UtilityItemData _selectedUtility; // 선택 유틸리티 값입니다.
    ItemData _detailItem; // 상세 아이템 값입니다.
    InventorySlotView _tooltipSlot; // 툴팁 슬롯 표시를 연결할 뷰입니다.
    bool _utilityTab; // 유틸리티 탭 여부입니다.
    int _page; // 페이지 값입니다.

    #endregion

    #region 이벤트 액션

    /// <summary>
    /// UI 장착 상태가 바뀐 뒤 두 칸의 최종 선택을 알립니다. 인벤토리 수량은 차감하지 않습니다.
    /// </summary>
    public event Action<UtilityItemData, UtilityItemData> OnUtilityEquipmentChanged;

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 현재 페이지 값입니다.
    /// </summary>
    public int CurrentPage => _page + 1;

    /// <summary>
    /// 페이지 개수 값입니다.
    /// </summary>
    public int PageCount => Mathf.Max(1, (_filteredItems.Count + PageSize - 1) / PageSize);

    /// <summary>
    /// 페이지 크기 값입니다.
    /// </summary>
    int PageSize => Mathf.Max(1, _itemSlots != null ? _itemSlots.Length : 0);

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 탭, 페이지, 닫기 버튼과 슬롯 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeButton, _detailCloseButton);
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_normalTabButton != null)
        {
            _normalTabButton.onClick.AddListener(ShowNormalItems);
        }

        if (_utilityTabButton != null)
        {
            _utilityTabButton.onClick.AddListener(ShowUtilityItems);
        }

        if (_previousPageButton != null)
        {
            _previousPageButton.onClick.AddListener(PreviousPage);
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.onClick.AddListener(NextPage);
        }

        if (_detailCloseButton != null)
        {
            _detailCloseButton.onClick.AddListener(HideDetails);
        }

        RegisterSlots(_itemSlots);
        RegisterSlots(_equipmentSlots);
        RegisterSlots(_utilitySlots);
    }

    /// <summary>
    /// 설명 창을 숨기고 미리보기 또는 실제 보유 목록을 갱신합니다.
    /// </summary>
    void OnEnable()
    {
        // 전신 이미지 에셋을 Inspector에 연결하면 다음 열기부터 자동으로 표시합니다.
        if (_characterImage != null)
        {
            SetCharacterIllustration(_characterImage.sprite);
        }

        SetAssetHudHidden(true);
        HideDetails();
        HideTooltip();
        if (_inventory == null && _usePreviewItems)
        {
            SetItems(_previewItems);
        }

        Refresh();
    }

    // 현재 Inventory에는 변경 이벤트가 없으므로 창이 보일 때 원본과 비교하고, 바뀐 경우에만 다시 그립니다.

    /// <summary>
    /// 창이 보일 때 보유 목록의 변경을 감지해 다시 표시합니다.
    /// </summary>
    void LateUpdate()
    {
        // 미리보기처럼 실제 Inventory가 없어도 열린 툴팁은 매 프레임 마우스를 추적합니다.
        FollowTooltipPointer();
        if (_assetInventoryLayout && _assetHiddenHud != null)
        {
            _assetHiddenHud.alpha = 0;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_inventory == null || (_mainWindow != null && !_mainWindow.activeInHierarchy))
        {
            return;
        }

        var source = _inventory.Items; // 원본 값입니다.
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (source.Count != _items.Count)
        {
            SetItems(source);
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < source.Count; i++)
        {
            // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
            if (source[i].ItemData != _items[i].ItemData || source[i].Count != _items[i].Count)
            {
                SetItems(source);
                return;
            }
        }
    }

    /// <summary>
    /// 창이 숨겨지면 선택, 상세페이지와 툴팁을 정리합니다.
    /// </summary>
    void OnDisable()
    {
        SetAssetHudHidden(false);
        ClearSelection();
        HideDetails();
        HideTooltip();
    }

    /// <summary>
    /// 버튼과 슬롯에 연결한 모든 이벤트 처리를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        if (_normalTabButton != null)
        {
            _normalTabButton.onClick.RemoveListener(ShowNormalItems);
        }

        if (_utilityTabButton != null)
        {
            _utilityTabButton.onClick.RemoveListener(ShowUtilityItems);
        }

        if (_previousPageButton != null)
        {
            _previousPageButton.onClick.RemoveListener(PreviousPage);
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.onClick.RemoveListener(NextPage);
        }

        if (_detailCloseButton != null)
        {
            _detailCloseButton.onClick.RemoveListener(HideDetails);
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in _registeredSlots)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null)
            {
                continue;
            }

            slot.OnClicked -= OnSlotClicked;
            slot.OnHovered -= ShowEquipmentTooltip;
            slot.OnHoverExited -= HideSlotTooltip;
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 에셋 배치에서 HUD를 숨기고 닫을 때 이전 표시와 입력 상태를 복원합니다.
    /// </summary>
    /// <param name="hidden">숨김 여부입니다.</param>
    void SetAssetHudHidden(bool hidden)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (!_assetInventoryLayout || _assetHiddenHud == null)
        {
            return;
        }

        if (hidden && !_assetHudHidden)
        {
            _assetHudState = new Vector3(_assetHiddenHud.alpha, _assetHiddenHud.interactable ? 1 : 0, _assetHiddenHud.blocksRaycasts ? 1 : 0);
            _assetHudHidden = true;
            _assetHiddenHud.alpha = 0;
            _assetHiddenHud.interactable = _assetHiddenHud.blocksRaycasts = false;
        }
        else if (!hidden && _assetHudHidden)
        {
            _assetHiddenHud.alpha = _assetHudState.x;
            _assetHiddenHud.interactable = _assetHudState.y > .5f;
            _assetHiddenHud.blocksRaycasts = _assetHudState.z > .5f;
            _assetHudHidden = false;
        }
    }

    /// <summary>
    /// 인벤토리 창을 열거나 닫고 표시 상태를 정리합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetInventory(bool isOn)
    {
        var fade = GetComponent<AppFadeView>(); // 페이드 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (fade != null && fade.TrySetVisible(isOn, () => SetInventory(isOn)))
        {
            return;
        }

        var window = _mainWindow != null ? _mainWindow : gameObject; // 창 값입니다.
        window.SetActive(isOn);
        if (isOn)
        {
            Refresh();
        }
        else
        {
            ClearSelection();
            HideDetails();
            HideTooltip();
        }
    }

    /// <summary>
    /// 아이템 상세 표시 또는 유틸리티 장착 대상 선택을 처리합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void OnSlotClicked(InventorySlotView slot)
    {
        int target = _utilitySlots != null ? Array.IndexOf(_utilitySlots, slot) : -1; // 대상 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (target >= 0 && target < 2 && _selectedUtility != null)
        {
            EquipSelectedUtility(target);
            return;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_itemSlots == null || Array.IndexOf(_itemSlots, slot) < 0 || slot.Item == null)
        {
            return;
        }

        if (_utilityTab && slot.Item is UtilityItemData utility)
        {
            _selectedUtility = utility;
            RenderPage();
        }

        if (!_assetInventoryLayout || !_utilityTab)
        {
            ShowDetails(slot.Item);
        }
    }

    /// <summary>
    /// 전신 일러스트를 원본 비율로 표시하고 미연결 시 숨깁니다. 전달된 Sprite는 중앙 Image에 연결합니다.
    /// </summary>
    /// <param name="illustration">전신 이미지입니다.</param>
    public void SetCharacterIllustration(Sprite illustration)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_characterImage == null)
        {
            return;
        }

        _characterImage.sprite = illustration;
        _characterImage.preserveAspect = true;
        _characterImage.color = Color.white;
        _characterImage.enabled = illustration != null;
        _characterImage.gameObject.SetActive(illustration != null);
    }

    /// <summary>
    /// 장착할 유틸리티 선택과 안내 문구를 초기화합니다.
    /// </summary>
    void ClearSelection()
    {
        _selectedUtility = null;
        if (_itemSlots != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _itemSlots)
            {
                if (slot != null)
                {
                    slot.SetSelected(false);
                }
            }
        }

        if (_selectionHintText != null)
        {
            _selectionHintText.text = string.Empty;
        }
    }

    /// <summary>
    /// 전달받은 보유 목록을 복사하고 장착 상태와 페이지를 갱신합니다.
    /// </summary>
    /// <param name="items">아이템 목록입니다.</param>
    public void SetItems(IReadOnlyList<Inventory.BagItem> items)
    {
        _items.Clear();
        if (items != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < items.Count; i++)
            {
                _items.Add(items[i]);
            }
        }

        bool equipmentChanged = false; // 장비 변경 여부입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < 2; i++)
        {
            if (_equippedUtilities[i] != null && GetOwnedCount(_equippedUtilities[i]) <= 0)
            {
                _equippedUtilities[i] = null;
                equipmentChanged = true;
            }
        }

        if (_selectedUtility != null && GetOwnedCount(_selectedUtility) <= 0)
        {
            ClearSelection();
        }

        RebuildPage();
        if (equipmentChanged)
        {
            NotifyEquipmentChanged();
        }
    }

    /// <summary>
    /// 고정 장비 영역과 현재 보유 목록을 다시 표시합니다.
    /// </summary>
    public void Refresh()
    {
        if (_equipmentStatusPanel != null)
        {
            _equipmentStatusPanel.SetActive(true);
        }

        if (_inventory != null)
        {
            SetItems(_inventory.Items);
        }
        else
        {
            RebuildPage();
        }
    }

    /// <summary>
    /// 유틸리티 두 칸의 장착 표시와 선택 안내를 갱신합니다.
    /// </summary>
    void RenderUtilities()
    {
        // i: 반복 값입니다.
        for (int i = 0; i < 2; i++)
        {
            var slot = _utilitySlots != null && i < _utilitySlots.Length ? _utilitySlots[i] : null; // 슬롯 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null)
            {
                continue;
            }

            slot.Bind(_equippedUtilities[i], _equippedUtilities[i] != null ? 1 : 0);
        }

        if (_selectionHintText != null)
        {
            _selectionHintText.text = _selectedUtility != null ? $"{_selectedUtility.ItemName}: 위쪽 유틸리티 슬롯 1 또는 2를 선택하세요." : (_utilityTab ? "아이템을 선택한 뒤 위쪽 유틸리티 슬롯을 누르면 장착·교환합니다." : string.Empty);
        }
    }

    /// <summary>
    /// 선택한 유틸리티를 대상 칸에 장착하거나 두 칸을 교환합니다.
    /// </summary>
    /// <param name="targetSlot">대상 슬롯 값입니다.</param>
    public bool EquipSelectedUtility(int targetSlot)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (targetSlot < 0 || targetSlot >= 2 || _selectedUtility == null || GetOwnedCount(_selectedUtility) <= 0)
        {
            return false;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_equippedUtilities[targetSlot] == _selectedUtility)
        {
            return false;
        }

        int other = 1 - targetSlot; // 다른 대상 값입니다.
        if (_equippedUtilities[other] == _selectedUtility)
        {
            _equippedUtilities[other] = _equippedUtilities[targetSlot];
        }

        _equippedUtilities[targetSlot] = _selectedUtility;
        ClearSelection();
        RenderPage();
        NotifyEquipmentChanged();
        return true;
    }

    /// <summary>
    /// 외부에서 전달한 유틸리티 장착 상태를 이벤트 없이 반영합니다.
    /// </summary>
    /// <param name="first">첫 값입니다.</param>
    /// <param name="second">두 번째 값입니다.</param>
    public bool SetUtilityEquipment(UtilityItemData first, UtilityItemData second)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if ((first != null && GetOwnedCount(first) <= 0) || (second != null && GetOwnedCount(second) <= 0) || (first != null && first == second))
        {
            return false;
        }

        _equippedUtilities[0] = first;
        _equippedUtilities[1] = second;
        ClearSelection();
        RenderPage();
        return true;
    }

    /// <summary>
    /// 유틸리티 두 칸의 최종 장착 상태를 외부에 알립니다.
    /// </summary>
    void NotifyEquipmentChanged()
    {
        OnUtilityEquipmentChanged?.Invoke(_equippedUtilities[0], _equippedUtilities[1]);
    }

    /// <summary>
    /// 지정한 장비 칸의 아이콘, 설명, 스탯과 레벨을 갱신합니다.
    /// </summary>
    /// <param name="slotIndex">슬롯 인덱스 값입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="itemName">아이템 이름 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="stats">능력치 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    public void SetEquipment(int slotIndex, Sprite icon, string itemName, string description, string stats, int level = -1)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_equipmentSlots == null || slotIndex < 0 || slotIndex >= Mathf.Min(6, _equipmentSlots.Length))
        {
            return;
        }

        if (_equipmentSlots[slotIndex] != null)
        {
            // 실제 데이터에 아이콘이 없을 때는 프리팹에 연결한 기본 장비 이미지를 유지합니다.
            _equipmentSlots[slotIndex].BindEquipment(icon != null ? icon : _equipmentSlots[slotIndex].Icon, itemName, description, stats, level);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 중복을 제외한 슬롯의 클릭과 포인터 이벤트를 연결합니다.
    /// </summary>
    /// <param name="slots">슬롯 목록입니다.</param>
    void RegisterSlots(InventorySlotView[] slots)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (slots == null)
        {
            return;
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in slots)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null || !_registeredSlots.Add(slot))
            {
                continue;
            }

            slot.OnClicked += OnSlotClicked;
            slot.OnHovered += ShowEquipmentTooltip;
            slot.OnHoverExited += HideSlotTooltip;
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 인벤토리 전체 창을 닫습니다.
    /// </summary>
    void Close()
    {
        SetInventory(false);
    }

    /// <summary>
    /// 일반 아이템 탭의 첫 페이지로 전환합니다.
    /// </summary>
    public void ShowNormalItems()
    {
        SetTab(false);
    }

    /// <summary>
    /// 탭과 페이지를 변경하고 이전 선택과 설명을 초기화합니다.
    /// </summary>
    /// <param name="utilityTab">유틸리티 탭 여부입니다.</param>
    void SetTab(bool utilityTab)
    {
        _utilityTab = utilityTab;
        _page = 0;
        ClearSelection();
        HideDetails();
        HideTooltip();
        RebuildPage();
    }

    /// <summary>
    /// 이전 페이지로 이동하고 선택과 상세페이지를 초기화합니다.
    /// </summary>
    public void PreviousPage()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_page <= 0)
        {
            return;
        }

        _page--;
        ClearSelection();
        HideDetails();
        RenderPage();
    }

    /// <summary>
    /// 다음 페이지로 이동하고 선택과 상세페이지를 초기화합니다.
    /// </summary>
    public void NextPage()
    {
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (_page >= PageCount - 1)
        {
            return;
        }

        _page++;
        ClearSelection();
        HideDetails();
        RenderPage();
    }

    /// <summary>
    /// 현재 탭에 맞는 아이템을 추려 유효한 페이지를 표시합니다.
    /// </summary>
    void RebuildPage()
    {
        _filteredItems.Clear();
        // entry: 항목 값입니다.
        foreach (var entry in _items)
        {
            if (entry.ItemData != null && entry.Count > 0 && (entry.ItemData is UtilityItemData) == _utilityTab)
            {
                _filteredItems.Add(entry);
            }
        }

        _page = Mathf.Clamp(_page, 0, PageCount - 1);
        RenderPage();
    }

    /// <summary>
    /// 현재 페이지의 슬롯, 탭 강조와 이동 버튼을 갱신합니다.
    /// </summary>
    void RenderPage()
    {
        HideTooltip();
        if (_assetInventoryLayout)
        {
            if (_equipmentStatusPanel != null)
            {
                _equipmentStatusPanel.SetActive(_utilityTab);
            }

            ApplyTabArt(_normalTabButton, !_utilityTab);
            ApplyTabArt(_utilityTabButton, _utilityTab);
        }

        if (_normalTabSelectedMark != null)
        {
            _normalTabSelectedMark.SetActive(!_utilityTab);
        }

        if (_utilityTabSelectedMark != null)
        {
            _utilityTabSelectedMark.SetActive(_utilityTab);
        }

        if (_itemSlots != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _itemSlots.Length; i++)
            {
                var slot = _itemSlots[i]; // 슬롯 값입니다.
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (slot == null)
                {
                    continue;
                }

                int index = _page * PageSize + i; // 인덱스 값입니다.
                var entry = index < _filteredItems.Count ? _filteredItems[index] : default; // 항목 값입니다.
                slot.gameObject.SetActive(_items.Exists(owned => owned.ItemData != null && owned.Count > 0));
                slot.Bind(entry.ItemData, entry.Count, entry.ItemData != null);
                slot.SetSelected(entry.ItemData != null && entry.ItemData == _selectedUtility);
                slot.SetEquipped(entry.ItemData != null && IsEquipped(entry.ItemData));
            }
        }

        if (_previousPageButton != null)
        {
            _previousPageButton.gameObject.SetActive(PageCount > 1); // 한 페이지에서는 이동 UI를 숨깁니다.
            _previousPageButton.interactable = _page > 0;
        }

        if (_nextPageButton != null)
        {
            _nextPageButton.gameObject.SetActive(PageCount > 1);
            _nextPageButton.interactable = _page < PageCount - 1;
        }

        if (_pageText != null)
        {
            _pageText.gameObject.SetActive(PageCount > 1);
            _pageText.text = $"{CurrentPage} / {PageCount}";
        }

        if (_emptyMessage != null)
        {
            _emptyMessage.SetActive(_filteredItems.Count == 0);
        }

        RenderUtilities();
        if (_detailItem != null)
        {
            if (GetOwnedCount(_detailItem) > 0)
            {
                ShowDetails(_detailItem);
            }
            else
            {
                HideDetails();
            }
        }
    }

    /// <summary>
    /// 유틸리티 아이템 탭의 첫 페이지로 전환합니다.
    /// </summary>
    public void ShowUtilityItems()
    {
        SetTab(true);
    }

    /// <summary>
    /// 클릭한 보유 아이템의 상세페이지와 선택 표시를 유지합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    void ShowDetails(ItemData item)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (item == null)
        {
            HideDetails();
            return;
        }

        _detailItem = item;
        if (_detailNameText != null)
        {
            _detailNameText.text = string.IsNullOrEmpty(item.ItemName) ? item.ItemId : item.ItemName;
        }

        if (_detailDescriptionText != null)
        {
            _detailDescriptionText.text = string.IsNullOrEmpty(item.ItemDesc) ? "등록된 설명이 없습니다." : item.ItemDesc;
        }

        if (_detailStatsText != null)
        {
            _detailStatsText.text = item is UtilityItemData ? "유틸리티 아이템 · 위쪽 슬롯에 장착 가능" : "일반 아이템 · 보유 목록";
        }

        if (_detailCountText != null)
        {
            _detailCountText.text = _assetInventoryLayout ? $"x{GetOwnedCount(item)}" : $"보유 수량  x{GetOwnedCount(item)}";
        }

        if (_detailPriceText != null)
        {
            _detailPriceText.text = item.SellPrice.ToString("0.##");
        }

        if (_itemSlots != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _itemSlots)
            {
                if (slot != null)
                {
                    slot.SetSelected(slot.Item == item);
                }
            }
        }

        if (_detailIconImage != null)
        {
            _detailIconImage.sprite = item.ItemIcon;
            _detailIconImage.enabled = item.ItemIcon != null;
        }

        if (_detailsPanel != null)
        {
            _detailsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 아이템 상세페이지를 닫고 관련 선택 표시를 정리합니다.
    /// </summary>
    void HideDetails()
    {
        _detailItem = null;
        if (_detailsPanel != null)
        {
            _detailsPanel.SetActive(false);
        }

        if (_itemSlots != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _itemSlots)
            {
                if (slot != null)
                {
                    slot.SetSelected(_selectedUtility != null && slot.Item == _selectedUtility);
                }
            }
        }
    }

    /// <summary>
    /// 1차 UI 에셋의 탭 배경과 글자색을 선택 상태에 맞춰 표시합니다.
    /// </summary>
    /// <param name="button">버튼입니다.</param>
    /// <param name="selected">선택 여부입니다.</param>
    void ApplyTabArt(UnityEngine.UI.Button button, bool selected)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (button == null)
        {
            return;
        }

        var image = button.targetGraphic as UnityEngine.UI.Image; // 이미지 값입니다.
        var sprite = selected ? _selectedTabSprite : _normalTabSprite; // 스프라이트 값입니다.
        if (image != null && sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }

        // text: 텍스트 값입니다.
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
        {
            text.color = selected ? new Color(0, .18f, .25f) : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
        }

        var icon = button.transform.Find("TabIcon") ?? button.transform.Find("ReplaceableIcon"); // 아이콘 값입니다.
        if (icon != null && icon.TryGetComponent<UnityEngine.UI.Image>(out var iconImage))
        {
            int index = button == _utilityTabButton ? 1 : 0;
            Sprite normalIcon = _normalTabIcons != null && index < _normalTabIcons.Length ? _normalTabIcons[index] : null;
            Sprite selectedIcon = _selectedTabIcons != null && index < _selectedTabIcons.Length ? _selectedTabIcons[index] : null;
            // 반전 이미지가 있으면 색을 곱하지 않고 원본 색 그대로 교체합니다.
            if (normalIcon != null)
                iconImage.sprite = selected && selectedIcon != null ? selectedIcon : normalIcon;
            iconImage.color = selected && selectedIcon == null ? new Color(0f, .2117647f, .3333333f, 1f) : Color.white;
        }
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 보유 목록에서 지정한 아이템의 수량을 조회합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    int GetOwnedCount(ItemData item)
    {
        // entry: 항목 값입니다.
        foreach (var entry in _items)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (entry.ItemData == item)
            {
                return entry.Count;
            }
        }

        return 0;
    }

    /// <summary>
    /// 지정한 칸에 표시 중인 유틸리티 아이템을 반환합니다.
    /// </summary>
    /// <param name="slotIndex">슬롯 인덱스 값입니다.</param>
    public UtilityItemData GetEquippedUtility(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < 2 ? _equippedUtilities[slotIndex] : null;
    }

    /// <summary>
    /// 지정한 아이템이 유틸리티 칸에 장착되어 있는지 확인합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    bool IsEquipped(ItemData item)
    {
        return _equippedUtilities[0] == item || _equippedUtilities[1] == item;
    }

    #endregion

    #region 마우스 위치 및 툴팁

    /// <summary>
    /// 장비, 장착 유틸리티와 보유 유틸리티의 툴팁을 마우스 옆에 표시합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void ShowEquipmentTooltip(InventorySlotView slot)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (slot == null)
        {
            return;
        }

        bool equipment = _equipmentSlots != null && Array.IndexOf(_equipmentSlots, slot) >= 0; // 장비 여부입니다.
        bool equippedUtility = _utilitySlots != null && Array.IndexOf(_utilitySlots, slot) >= 0 && slot.Item is UtilityItemData; // 장착 유틸리티 여부입니다.
        bool ownedUtility = _utilityTab && _itemSlots != null && Array.IndexOf(_itemSlots, slot) >= 0 && slot.Item is UtilityItemData && slot.Count > 0; // 보유 유틸리티 여부입니다.
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if ((!equipment && !equippedUtility && !ownedUtility) || string.IsNullOrEmpty(slot.DisplayName))
        {
            HideTooltip();
            return;
        }

        _tooltipSlot = slot;
        if (_tooltipNameText != null)
        {
            _tooltipNameText.text = slot.DisplayName;
        }

        if (_tooltipDescriptionText != null)
        {
            _tooltipDescriptionText.text = slot.Description + (string.IsNullOrEmpty(slot.Stats) ? string.Empty : "\n" + slot.Stats);
        }

        if (_equipmentTooltipPanel != null)
        {
            _equipmentTooltipPanel.transform.SetAsLastSibling();
            // 툴팁 자체가 호버 대상의 입력을 가로채지 않도록 합니다.
            foreach (var graphic in _equipmentTooltipPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            _equipmentTooltipPanel.SetActive(true);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_equipmentTooltipPanel.transform as RectTransform);
            FollowTooltipPointer();
        }
    }

    /// <summary>
    /// 현재 마우스 위치를 읽어 툴팁을 이동하며 비활성화된 슬롯의 툴팁은 숨깁니다.
    /// </summary>
    void FollowTooltipPointer()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_tooltipSlot == null || _equipmentTooltipPanel == null || !_equipmentTooltipPanel.activeSelf)
        {
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_tooltipSlot.isActiveAndEnabled)
        {
            HideTooltip();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current; // 마우스 값입니다.
        if (mouse != null)
        {
            PositionTooltip(mouse.position.ReadValue());
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        PositionTooltip(Input.mousePosition);
#endif
    }

    /// <summary>
    /// 화면 좌표를 Canvas 좌표로 변환하고 툴팁 전체가 화면 안에 들어오도록 보정합니다.
    /// </summary>
    /// <param name="screenPosition">화면 위치 값입니다.</param>
    void PositionTooltip(Vector2 screenPosition)
    {
        var rect = _equipmentTooltipPanel != null ? _equipmentTooltipPanel.transform as RectTransform : null; // 사각 영역 값입니다.
        var parent = rect != null ? rect.parent as RectTransform : null; // 부모 값입니다.
        var canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null; // 캔버스 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (rect == null || parent == null || canvas == null)
        {
            return;
        }

        var rootCanvas = canvas.rootCanvas; // 루트 캔버스 값입니다.
        var canvasRect = rootCanvas.transform as RectTransform; // 캔버스 사각 영역 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (canvasRect == null)
        {
            return;
        }

        var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera; // 카메라 값입니다.
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, camera, out var local))
        {
            return;
        }

        rect.pivot = new Vector2(0f, 1f);
        local += _tooltipPointerOffset;
        rect.position = parent.TransformPoint(new Vector3(local.x, local.y, 0f));
        rect.GetWorldCorners(_tooltipCorners);
        canvasRect.GetWorldCorners(_tooltipCanvasCorners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity); // 최소 값입니다.
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity); // 최대 값입니다.
        Vector2 screenMin = min, screenMax = max; // 화면 최소 값입니다. / 화면 최대 값입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < 4; i++)
        {
            Vector2 point = parent.InverseTransformPoint(_tooltipCorners[i]); // 좌표 값입니다.
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
            point = parent.InverseTransformPoint(_tooltipCanvasCorners[i]);
            screenMin = Vector2.Min(screenMin, point);
            screenMax = Vector2.Max(screenMax, point);
        }

        float padding = Mathf.Max(0f, _tooltipScreenPadding); // 안쪽 여백 값입니다.
        Vector2 shift = Vector2.zero; // 보정 값입니다.
        if (max.x > screenMax.x - padding)
        {
            shift.x = screenMax.x - padding - max.x;
        }

        if (min.x + shift.x < screenMin.x + padding)
        {
            shift.x = screenMin.x + padding - min.x;
        }

        if (min.y < screenMin.y + padding)
        {
            shift.y = screenMin.y + padding - min.y;
        }

        if (max.y + shift.y > screenMax.y - padding)
        {
            shift.y = screenMax.y - padding - max.y;
        }

        local += shift;
        rect.position = parent.TransformPoint(new Vector3(local.x, local.y, 0f));
    }

    /// <summary>
    /// 포인터가 벗어난 슬롯의 툴팁을 숨깁니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void HideSlotTooltip(InventorySlotView slot)
    {
        if (_tooltipSlot == slot)
        {
            HideTooltip();
        }
    }

    /// <summary>
    /// 장비 툴팁과 툴팁 대상 참조를 정리합니다.
    /// </summary>
    void HideTooltip()
    {
        _tooltipSlot = null;
        if (_equipmentTooltipPanel != null)
        {
            _equipmentTooltipPanel.SetActive(false);
        }
    }

    #endregion

}
