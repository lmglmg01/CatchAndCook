using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 획득 아이템 한 종류와 수량을 표시하고, 패배 시 선택 요청만 전달합니다.
/// </summary>
public class DiveResultSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] UnityEngine.UI.Image _backgroundImage; // 슬롯 배경입니다.
    [SerializeField] UnityEngine.UI.Image _itemImage; // 아이템 아이콘입니다.
    [SerializeField] TMP_Text _itemAmountText; // 해당 아이템의 획득 수량입니다.
    // 패배 시 선택되지 않은 아이템의 밝기입니다. 알파 대신 RGB를 낮춰 어둡게 표시합니다.
    [SerializeField, Range(0f, 1f)] float _failureBrightness = 0.28f;    [Header("가로형 결과 슬롯")]
    [SerializeField] bool _resultGridLayout;
    [SerializeField] UnityEngine.UI.Outline _selectionOutline;
    [SerializeField] Material _unselectedItemMaterial;

    #endregion

    #region 런타임 상태

    Color _originalBackgroundColor; // 원본 배경 색상 색상입니다.
    Color _originalItemColor; // 원본 아이템 색상 색상입니다.
    UnityEngine.UI.Button _button; // 버튼입니다.
    bool _initialized; // 초기화 여부입니다.
    bool _isClear; // 완료 여부입니다.
    bool _selected; // 선택 여부입니다.

    #endregion

    #region 이벤트 액션

    public event Action<DiveResultSlotView> OnSelected; // 선택 알림입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 아이템 값입니다.
    /// </summary>
    public ItemData Item { get; private set; }

    /// <summary>
    /// 수량 값입니다.
    /// </summary>
    public int Amount { get; private set; }

    /// <summary>
    /// 선택 여부입니다.
    /// </summary>
    public bool IsSelected => _selected;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 슬롯의 원래 색상과 버튼 이벤트를 초기화합니다.
    /// </summary>
    void Awake()
    {
        Initialize();
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
    /// 슬롯의 원래 색상을 저장하고 클릭 처리를 한 번만 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_backgroundImage == null)
        {
            _backgroundImage = GetComponent<UnityEngine.UI.Image>();
        }

        _originalBackgroundColor = _backgroundImage != null ? _backgroundImage.color : Color.white;
        _originalItemColor = _itemImage != null ? _itemImage.color : Color.white;
        _button = GetComponent<UnityEngine.UI.Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(Click);
        }
    }

    /// <summary>
    /// 획득 아이템과 수량, 성공 여부를 받아 슬롯 표시를 초기화합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    /// <param name="amount">수량 값입니다.</param>
    /// <param name="isClear">완료 여부입니다.</param>
    public void Bind(ItemData item, int amount, bool isClear)
    {
        Initialize();
        Item = item;
        Amount = item != null ? Mathf.Max(0, amount) : 0;
        _isClear = isClear;
        _selected = false;
        GetItemData();
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    // 기존 인스펙터 UnityEvent 연결을 유지합니다. 표시할 데이터는 Bind로 받습니다.

    /// <summary>
    /// 현재 아이템의 아이콘, 획득 수량과 선택 색상을 갱신합니다.
    /// </summary>
    public void GetItemData()
    {
        Initialize();
        if (_itemImage != null)
        {
            _itemImage.sprite = Item != null ? Item.ItemIcon : null;
            _itemImage.enabled = _itemImage.sprite != null;
        }

        if (_itemAmountText != null)
        {
            _itemAmountText.text = Amount > 0 ? Amount.ToString() : string.Empty;
        }

        RefreshColors();
    }

    /// <summary>
    /// 성공 여부를 반영하고 슬롯 선택과 색상을 초기화합니다.
    /// </summary>
    /// <param name="isClear">완료 여부입니다.</param>
    public void SetIsClear(bool isClear)
    {
        Initialize();
        _isClear = isClear;
        _selected = false;
        RefreshColors();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 패배 시 유효한 아이템의 선택 여부와 색상을 변경합니다.
    /// </summary>
    /// <param name="selected">선택 여부입니다.</param>
    public void SetSelected(bool selected)
    {
        Initialize();
        _selected = !_isClear && Item != null && Amount > 0 && selected;
        RefreshColors();
    }

    /// <summary>
    /// 패배한 미선택 슬롯은 어둡게, 나머지는 원래 색으로 표시합니다.
    /// </summary>
    void RefreshColors()
    {
        if (_resultGridLayout)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = new Color32(0, 40, 58, 255);
            if (_selectionOutline != null) _selectionOutline.enabled = !_isClear && _selected;
            if (_itemImage != null)
            {
                _itemImage.color = Color.white;
                _itemImage.material = !_isClear && !_selected ? _unselectedItemMaterial : null;
            }
            return;
        }
        bool dimmed = !_isClear && !_selected; // 어두운 표시 여부입니다.
        if (_backgroundImage != null)
        {
            _backgroundImage.color = dimmed ? Darken(_originalBackgroundColor) : _originalBackgroundColor;
        }

        if (_itemImage != null)
        {
            _itemImage.color = dimmed ? Darken(_originalItemColor) : _originalItemColor;
        }
    }

    /// <summary>
    /// 투명도를 유지하면서 원래 색의 밝기를 낮춥니다.
    /// </summary>
    /// <param name="original">원본 색상입니다.</param>
    Color Darken(Color original)
    {
        float brightness = Mathf.Clamp01(_failureBrightness); // 밝기 값입니다.
        return new Color(original.r * brightness, original.g * brightness, original.b * brightness, original.a);
    }

    /// <summary>
    /// 패배 상태의 유효한 슬롯에 대한 선택 요청을 전달합니다.
    /// </summary>
    void Click()
    {
        if (isActiveAndEnabled && !_isClear && Item != null && Amount > 0)
        {
            OnSelected?.Invoke(this);
        }
    }

    #endregion

    #region 포인터 입력 및 요청

    /// <summary>
    /// Button이 없는 결과 슬롯의 왼쪽 클릭을 처리합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null && eventData.button == PointerEventData.InputButton.Left)
        {
            Click();
        }
    }

    // 마우스를 올리거나 떼는 것만으로 선택과 색상이 바뀌지 않습니다.

    /// <summary>
    /// 포인터 진입 시 선택이나 색상을 변경하지 않습니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
    }

    /// <summary>
    /// 포인터 이탈 시 선택이나 색상을 변경하지 않습니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
    }

    #endregion

}
