using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 판매할 보유 아이템을 표시하고 선택·드래그 입력을 전달하는 슬롯 뷰입니다.
/// </summary>
public class SellItemSlotView : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] UnityEngine.UI.Image _itemIconImage; // 아이템 아이콘 이미지입니다.
    [SerializeField] TMP_Text _itemAmountText; // 아이템 수량 텍스트입니다.

    #endregion

    #region 런타임 상태

    ItemData _item; // 아이템 값입니다.
    int _count; // 개수 값입니다.
    bool _dragging; // 드래그 여부입니다.

    #endregion

    #region 이벤트 액션

    public event Action<ItemData> OnItemSelected; // 아이템 선택 알림입니다.

    #endregion

    #region 임시 데이터 및 테스트 멤버

    // 임시 상점 테스트 입력은 실제 선택·레거시 이벤트와 분리합니다.
    [NonSerialized]
    bool _testPreview;

    #endregion

    #region 이벤트 액션

    internal event Action<ItemData> OnTestItemSelected; // 테스트 아이템 선택 알림입니다.
    // Kept for the existing PlayScene subscriptions; new code should use OnItemSelected.
    public event Action<string, string> OnClickItemSlot; // 클릭 아이템 슬롯 알림입니다.

    #endregion

    #region 런타임 상태

    UnityEngine.UI.ScrollRect _scrollRect; // 스크롤 사각 영역 값입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 아이템 값입니다.
    /// </summary>
    public ItemData Item => _item;

    /// <summary>
    /// 개수 값입니다.
    /// </summary>
    public int Count => _count;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 상위 스크롤 영역의 ScrollRect를 찾아 저장합니다.
    /// </summary>
    void Awake()
    {
        _scrollRect = GetComponentInParent<UnityEngine.UI.ScrollRect>();
    }

    /// <summary>
    /// 슬롯이 숨겨지면 드래그 판정 상태를 초기화합니다.
    /// </summary>
    void OnDisable()
    {
        _dragging = false;
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 판매할 아이템의 아이콘과 보유 수량을 표시합니다.
    /// </summary>
    /// <param name="item">아이템 값입니다.</param>
    /// <param name="count">개수 값입니다.</param>
    public void Bind(ItemData item, int count)
    {
        _item = item;
        _count = Mathf.Max(0, count);
        _dragging = false;
        if (_itemIconImage != null)
        {
            _itemIconImage.sprite = item != null ? item.ItemIcon : null;
            _itemIconImage.enabled = _itemIconImage.sprite != null;
        }

        if (_itemAmountText != null)
        {
            _itemAmountText.text = "×" + _count;
        }
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    // Retained for any serialized UnityEvent references. Does not fetch model data.

    /// <summary>
    /// 현재 저장된 아이템과 수량으로 슬롯을 다시 표시합니다.
    /// </summary>
    public void GetItemData()
    {
        Bind(_item, _count);
    }

    #endregion

    #region 포인터 입력 및 요청

    /// <summary>
    /// 포인터를 누르면 드래그 판정 상태를 초기화합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        _dragging = false;
    }

    /// <summary>
    /// 드래그가 아닌 왼쪽 클릭에 아이템 선택 이벤트를 전달합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_dragging || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_testPreview)
        {
            if (_item != null && _count > 0)
            {
                OnTestItemSelected?.Invoke(_item);
            }

            return;
        }

        if (_item != null && _count > 0)
        {
            OnItemSelected?.Invoke(_item);
            OnClickItemSlot?.Invoke(_item.ItemName, _item.ItemDesc);
        }
        else if (_item == null)
        {
            // The unchanged PlayScene binds its own item through a closure.
            OnClickItemSlot?.Invoke(string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// 드래그를 표시하고 상위 스크롤 영역에 시작 입력을 전달합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragging = true;
        if (_scrollRect != null)
        {
            _scrollRect.OnBeginDrag(eventData);
        }
    }

    /// <summary>
    /// 드래그 입력을 상위 스크롤 영역으로 전달합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (_scrollRect != null)
        {
            _scrollRect.OnDrag(eventData);
        }
    }

    /// <summary>
    /// 드래그 종료 입력을 상위 스크롤 영역으로 전달합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (_scrollRect != null)
        {
            _scrollRect.OnEndDrag(eventData);
        }
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// 판매 슬롯의 임시 테스트 입력 여부를 설정합니다.
    /// </summary>
    /// <param name="enabled">활성 여부입니다.</param>
    internal void SetTestPreview(bool enabled)
    {
        _testPreview = enabled;
    }

    #endregion

}
