using UnityEngine;
using TMPro;

/// <summary>
/// 등록 메뉴의 교체·수량 변경·삭제 항목을 보관하는 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiRegisteredMenuActionsView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 관리할 요리 이름과 현재 등록 수량을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _selectedDishText;
    [SerializeField] UnityEngine.UI.Image _selectedDishIcon;
    [SerializeField] TMP_Text _registeredQuantityText;
    // 선택한 메뉴를 다른 요리로 교체할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _replaceButton;
    // 현재 메뉴의 등록 수량을 변경할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _quantityButton;
    // 현재 메뉴의 등록을 해제할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _deleteButton;
    // 같은 Canvas_Sushi 루트의 IjakayaView를 연결합니다. Sushi 화면 표시 전환만 요청합니다.
    [SerializeField] IjakayaView _owner;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;
    // Panel 왼쪽 아래의 임시 BackButton을 연결합니다. 등록 메뉴 관리 팝업만 닫습니다.
    [SerializeField] UnityEngine.UI.Button _backButton;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _backButton);
        InitializeButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_replaceButton != null)
        {
            _replaceButton.onClick.RemoveListener(OpenReplacement);
        }

        if (_quantityButton != null)
        {
            _quantityButton.onClick.RemoveListener(OpenQuantity);
        }

        if (_deleteButton != null)
        {
            _deleteButton.onClick.RemoveListener(DeletePreview);
        }

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(Close);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(Close);
            _backButton.onClick.AddListener(Close);
        }

        if (_replaceButton != null)
        {
            _replaceButton.onClick.RemoveListener(OpenReplacement);
            _replaceButton.onClick.AddListener(OpenReplacement);
        }

        if (_quantityButton != null)
        {
            _quantityButton.onClick.RemoveListener(OpenQuantity);
            _quantityButton.onClick.AddListener(OpenQuantity);
        }

        if (_deleteButton != null)
        {
            _deleteButton.onClick.RemoveListener(DeletePreview);
            _deleteButton.onClick.AddListener(DeletePreview);
        }
    }

    /// <summary>
    /// 관리할 메뉴의 표시 문구만 갱신합니다.
    /// </summary>
    /// <param name="information">정보 값입니다.</param>
    public void Bind(Sprite icon, string name, string information)
    {
        if (_selectedDishText != null)
        {
            _selectedDishText.text = name ?? string.Empty;
        }
        IjakayaView.SetDisplayIcon(_selectedDishIcon, icon);
        if (_registeredQuantityText != null) _registeredQuantityText.text = information ?? string.Empty;
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 다른 화면이나 데이터를 변경하지 않고 이 화면만 비활성화합니다.
    /// </summary>
    public void Close()
    {
        if (_pageRoot != null)
        {
            _pageRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 관리 팝업을 닫고 교체할 요리 목록을 엽니다.
    /// </summary>
    void OpenReplacement()
    {
        if (_owner != null)
        {
            _owner.RequestMenuReplacement();
        }

        Close();
        if (_owner != null)
        {
            _owner.OpenRecipeRegistration();
        }
    }

    /// <summary>
    /// 관리 팝업을 닫고 수량 설정 팝업을 엽니다.
    /// </summary>
    void OpenQuantity()
    {
        if (_owner != null)
        {
            _owner.RequestMenuQuantityEdit();
        }

        Close();
        if (_owner != null)
        {
            _owner.OpenMenuQuantity();
        }
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// 삭제 항목의 UI 확인으로 관리 팝업만 닫습니다. 등록 데이터는 삭제하지 않습니다.
    /// </summary>
    void DeletePreview()
    {
        if (_owner != null)
        {
            _owner.RequestMenuDeletion();
        }

        Close();
    }

    #endregion

}
