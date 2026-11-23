using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 보유 아이템과 장비 상태에 공통으로 사용하는 표시 전용 슬롯입니다.
/// </summary>
public class InventorySlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("슬롯 UI")]
    // 아이콘을 표시할 Image입니다. 슬롯 배경과는 별도의 자식 Image를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _iconImage; // 아이콘 이미지입니다.
    // 아이콘 미등록 상태를 기본 도형으로 표시할 자식 오브젝트입니다.
    [SerializeField] GameObject _iconPlaceholder;
    // 수량 표시용 텍스트입니다. 장비 슬롯에서는 숨깁니다.
    [SerializeField] TMP_Text _countText;
    // 장비 이름 또는 빈 슬롯 이름을 표시할 텍스트입니다. 생략할 수 있습니다.
    [SerializeField] TMP_Text _nameText;
    // 장비 강화 레벨 표시용 텍스트입니다. 레벨이 없는 슬롯에서는 숨깁니다.
    [SerializeField] TMP_Text _levelText;
    // 선택 상태를 표시할 테두리 등 별도의 자식 오브젝트를 연결합니다.
    [SerializeField] GameObject _selectionMark;
    // 유틸리티 목록에서 현재 장착 중임을 표시할 자식 오브젝트입니다.
    [SerializeField] GameObject _equippedMark;
    // Button이 있으면 마우스와 키보드 입력에 사용합니다. 없으면 슬롯의 포인터 클릭을 사용합니다.
    [SerializeField] UnityEngine.UI.Button _button;

    [Header("장비 슬롯 사전 표시 정보")]
    // 장비 데이터 연결 전 기본 도형 단계에서도 설명을 확인할 수 있는 표시값입니다.
    [SerializeField] string _displayName; // 표시 이름 값입니다.
    [SerializeField, TextArea(2, 5)] string _description; // 설명 값입니다.
    // 공격력/사거리, 최대 산소량, 방어력/이동 속도, 적재량, 탄환 수 등을 전달받아 표시합니다.
    [SerializeField, TextArea(2, 5)] string _stats;

    #endregion

    #region 이벤트 액션

    public event Action<InventorySlotView> OnClicked; // 클릭 알림입니다.
    public event Action<InventorySlotView> OnHovered; // 마우스 진입 알림입니다.
    public event Action<InventorySlotView> OnHoverExited; // 호버 이탈 알림입니다.

    #endregion

    #region 런타임 상태

    bool _interactable = true; // 상호작용 가능 여부입니다.
    bool _initialized; // 초기화 여부입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 아이템 값입니다.
    /// </summary>
    public ItemData Item { get; private set; }

    /// <summary>
    /// 개수 값입니다.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// 표시 이름 값입니다.
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 설명 값입니다.
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// 능력치 값입니다.
    /// </summary>
    public string Stats => _stats;

    /// <summary>
    /// 아이콘 이미지입니다.
    /// </summary>
    public Sprite Icon => _iconImage != null ? _iconImage.sprite : null;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 슬롯의 버튼 참조와 클릭 처리를 초기화합니다.
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 슬롯이 숨겨지면 포인터 이탈 이벤트를 전달합니다.
    /// </summary>
    void OnDisable()
    {
        OnHoverExited?.Invoke(this);
    }

    /// <summary>
    /// 슬롯 버튼에 연결한 클릭 처리를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(Click);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 버튼 참조를 찾고 클릭 처리를 한 번만 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_button == null)
        {
            _button = GetComponent<UnityEngine.UI.Button>();
        }

        if (_button != null)
        {
            _button.onClick.AddListener(Click);
        }
    }

    /// <summary>
    /// 아이템 아이콘과 보유 수량을 표시하고 슬롯 상태를 초기화합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    /// <param name="count">개수 값입니다.</param>
    /// <param name="interactable">상호작용 가능 여부입니다.</param>
    public void Bind(ItemData item, int count, bool interactable = true)
    {
        Item = item;
        Count = item != null ? Mathf.Max(0, count) : 0;
        SetDisplay(item != null ? item.ItemIcon : null, item != null ? (string.IsNullOrEmpty(item.ItemName) ? item.ItemId : item.ItemName) : string.Empty, item != null ? item.ItemDesc : string.Empty, string.Empty, -1);
        if (_countText != null)
        {
            _countText.text = Count > 0 ? $"x{Count}" : string.Empty;
            _countText.gameObject.SetActive(item != null && Count > 0);
        }

        SetSelected(false);
        SetEquipped(false);
        SetInteractable(interactable);
    }

    /// <summary>
    /// 장비의 이름, 설명, 스탯과 강화 레벨을 표시합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="itemName">아이템 이름 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="stats">능력치 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    public void BindEquipment(Sprite icon, string itemName, string description, string stats, int level = -1)
    {
        Item = null;
        Count = 0;
        SetDisplay(icon, itemName, description, stats, level);
        if (_countText != null)
        {
            _countText.gameObject.SetActive(false);
        }

        SetSelected(false);
        SetEquipped(false);
        SetInteractable(true);
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 슬롯의 아이콘, 이름, 설명과 레벨 표시를 갱신합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="itemName">아이템 이름 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="stats">능력치 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    void SetDisplay(Sprite icon, string itemName, string description, string stats, int level)
    {
        _displayName = itemName ?? string.Empty;
        _description = description ?? string.Empty;
        _stats = stats ?? string.Empty;
        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
            _iconImage.enabled = icon != null;
        }

        if (_iconPlaceholder != null)
        {
            _iconPlaceholder.SetActive(icon == null && !string.IsNullOrEmpty(_displayName));
        }

        if (_nameText != null)
        {
            _nameText.text = _displayName;
        }

        if (_levelText != null)
        {
            _levelText.text = level >= 0 ? $"Lv. {level}" : string.Empty;
            _levelText.gameObject.SetActive(level >= 0);
        }
    }

    /// <summary>
    /// 슬롯의 선택 표시를 켜거나 끕니다.
    /// </summary>
    /// <param name="selected">선택 여부입니다.</param>
    public void SetSelected(bool selected)
    {
        if (_selectionMark != null)
        {
            _selectionMark.SetActive(selected);
        }
    }

    /// <summary>
    /// 슬롯의 장착 표시를 켜거나 끕니다.
    /// </summary>
    /// <param name="equipped">장착 여부입니다.</param>
    public void SetEquipped(bool equipped)
    {
        if (_equippedMark != null)
        {
            _equippedMark.SetActive(equipped);
        }
    }

    /// <summary>
    /// 슬롯과 버튼의 입력 가능 상태를 변경합니다.
    /// </summary>
    /// <param name="interactable">상호작용 가능 여부입니다.</param>
    public void SetInteractable(bool interactable)
    {
        Initialize();
        _interactable = interactable;
        if (_button != null)
        {
            _button.interactable = interactable;
        }
    }

    /// <summary>
    /// 입력 가능한 슬롯의 클릭 이벤트를 전달합니다.
    /// </summary>
    void Click()
    {
        if (isActiveAndEnabled && _interactable)
        {
            OnClicked?.Invoke(this);
        }
    }

    #endregion

    #region 포인터 입력 및 요청

    /// <summary>
    /// 별도 Button이 없는 슬롯의 왼쪽 클릭을 처리합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null && eventData.button == PointerEventData.InputButton.Left)
        {
            Click();
        }
    }

    /// <summary>
    /// 포인터가 입력 가능한 슬롯에 진입했음을 알립니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_interactable)
        {
            OnHovered?.Invoke(this);
        }
    }

    /// <summary>
    /// 포인터가 슬롯에서 벗어났음을 알립니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        OnHoverExited?.Invoke(this);
    }

    #endregion

}
