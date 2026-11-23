using UnityEngine;
using TMPro;

/// <summary>
/// 오늘의 메뉴 한 칸에서 등록 상태와 요리 정보를 표시할 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiTodayMenuSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 메뉴 슬롯 자체의 배경 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _backgroundImage;
    // 요리가 등록되었을 때 표시할 아이콘·정보·관리 버튼 영역을 연결합니다.
    [SerializeField] GameObject _registeredContent;
    // 등록된 요리 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _foodIconImage;
    // 등록된 요리 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _dishNameText;
    // 요리 레벨·등록 수량·판매 금액을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _dishInfoText;
    // 빈 메뉴 슬롯에서 메뉴를 등록할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _addMenuButton;
    // 등록된 요리의 관리 메뉴를 여는 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _manageButton;
    // 같은 Canvas_Sushi 루트의 IjakayaView를 연결합니다. Sushi 화면 표시 전환만 요청합니다.
    [SerializeField] IjakayaView _owner;
    // 이 메뉴 슬롯의 위치입니다. 위에서 아래로 0부터 4까지 설정합니다.
    [SerializeField] int _slotIndex;

    #endregion

    #region 런타임 상태

    int _recipeId = -1; // 요리 식별자 값입니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        // 단독 슬롯 프리팹은 부모 Canvas_Sushi에서 화면 전환용 View를 찾습니다.
        if (_owner == null)
        {
            _owner = GetComponentInParent<IjakayaView>();
        }

        InitializeButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_addMenuButton != null)
        {
            _addMenuButton.onClick.RemoveListener(OpenRegistration);
        }

        if (_manageButton != null)
        {
            _manageButton.onClick.RemoveListener(OpenManagement);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_addMenuButton != null)
        {
            _addMenuButton.onClick.RemoveListener(OpenRegistration);
            _addMenuButton.onClick.AddListener(OpenRegistration);
        }

        if (_manageButton != null)
        {
            _manageButton.onClick.RemoveListener(OpenManagement);
            _manageButton.onClick.AddListener(OpenManagement);
        }
    }

    /// <summary>
    /// 요리 등록 상태와 표시 정보만 전달받습니다.
    /// </summary>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="information">정보 값입니다.</param>
    /// <param name="registered">등록 여부입니다.</param>
    /// <param name="interactable">상호작용 가능 여부입니다.</param>
    public void Bind(int recipeId, Sprite icon, string name, string information, bool registered, bool interactable = true)
    {
        _recipeId = recipeId;
        IjakayaView.SetDisplayIcon(_foodIconImage, icon);
        if (_dishNameText != null)
        {
            _dishNameText.text = name ?? string.Empty;
        }

        if (_dishInfoText != null)
        {
            _dishInfoText.text = information ?? string.Empty;
        }

        if (_registeredContent != null)
        {
            _registeredContent.SetActive(registered);
        }

        if (_addMenuButton != null)
        {
            _addMenuButton.gameObject.SetActive(!registered);
            _addMenuButton.interactable = interactable;
        }

        if (_manageButton != null)
        {
            _manageButton.interactable = interactable;
        }
        GetComponentInParent<SushiRestaurantStyleView>()?.ApplyTodayEmptyCard(
            _addMenuButton != null ? _addMenuButton.transform : null,
            !registered && !interactable && !string.IsNullOrEmpty(information),information);
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 빈 메뉴 슬롯에서 등록 가능한 요리 목록을 엽니다.
    /// </summary>
    void OpenRegistration()
    {
        if (_owner != null)
        {
            _owner.SelectMenuSlot(_slotIndex, -1, false);
            _owner.OpenRecipeRegistration();
        }
    }

    /// <summary>
    /// 등록된 메뉴의 관리 팝업을 엽니다.
    /// </summary>
    void OpenManagement()
    {
        if (_owner != null)
        {
            _owner.SelectMenuSlot(_slotIndex, _recipeId, true);
            _owner.OpenRegisteredMenuActions();
        }
    }

    #endregion

}
