using UnityEngine;
using TMPro;

/// <summary>
/// 선택한 요리의 상세 정보와 필요 재료를 표시할 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiRecipeDetailView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 상세 화면의 요리 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _foodIconImage;
    // 선택한 요리 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _nameText;
    // 요리 레벨과 판매 금액을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _levelPriceText;
    // 요리에 대한 설명을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _descriptionText;
    // 등록에 필요한 재료 슬롯을 위에서 아래 순서로 연결합니다.
    [SerializeField] SushiIngredientSlotView[] _ingredientSlots;
    // 등록 수량 설정 팝업을 여는 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _quantityButton;
    // 요리 강화 확인 팝업을 여는 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _upgradeButton;
    // 요리 목록 화면의 뒤로가기 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;
    // 같은 Canvas_Sushi 루트의 IjakayaView를 연결합니다. Sushi 화면 표시 전환만 요청합니다.
    [SerializeField] IjakayaView _owner;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;
    public int IngredientCount { get; private set; }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeButton);
        InitializeButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_quantityButton != null)
        {
            _quantityButton.onClick.RemoveListener(OpenQuantity);
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.RemoveListener(OpenUpgrade);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_quantityButton != null)
        {
            _quantityButton.onClick.RemoveListener(OpenQuantity);
            _quantityButton.onClick.AddListener(OpenQuantity);
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.RemoveListener(OpenUpgrade);
            _upgradeButton.onClick.AddListener(OpenUpgrade);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 선택한 요리의 설명과 버튼 표시 상태를 갱신합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="levelPrice">레벨 가격 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="canRegister">가능 등록 여부입니다.</param>
    /// <param name="canUpgrade">가능 강화 여부입니다.</param>
    public void Bind(Sprite icon, string name, string levelPrice, string description, bool canRegister, bool canUpgrade)
    {
        IjakayaView.SetDisplayIcon(_foodIconImage, icon);
        if (_nameText != null)
        {
            _nameText.text = name ?? string.Empty;
        }

        if (_levelPriceText != null)
        {
            _levelPriceText.text = levelPrice ?? string.Empty;
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = description ?? string.Empty;
            _descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
        }

        if (_quantityButton != null)
        {
            _quantityButton.interactable = canRegister;
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.interactable = canUpgrade;
        }
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
    /// 선택한 요리의 수량 설정 팝업을 엽니다.
    /// </summary>
    void OpenQuantity()
    {
        if (_owner != null)
        {
            _owner.OpenMenuQuantity();
        }
    }

    /// <summary>
    /// 요리 강화 확인 팝업을 엽니다. 실제 강화는 실행하지 않습니다.
    /// </summary>
    void OpenUpgrade()
    {
        if (_owner != null)
        {
            _owner.OpenRecipeUpgrade();
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 요리 상세의 재료 표시를 순서대로 갱신합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="owned">보유 값입니다.</param>
    /// <param name="required">필요 값입니다.</param>
    public void DisplayIngredient(int index, Sprite icon, string name, int owned, int required)
    {
        if (_ingredientSlots != null && index >= 0 && index < _ingredientSlots.Length && _ingredientSlots[index] != null)
        {
            _ingredientSlots[index].Bind(icon, name, owned, required);
        }
    }

    /// <summary>
    /// 필요 재료 목록을 전달받아 표시하고 남는 재료 행을 숨깁니다.
    /// </summary>
    /// <param name="ingredients">재료 목록입니다.</param>
    public void DisplayIngredients(SushiTodayMenuView.IngredientData[] ingredients)
    {
        IngredientCount = ingredients != null ? ingredients.Length : 0;
        // i: 반복 값입니다.
        for (int i = 0; _ingredientSlots != null && i < _ingredientSlots.Length; i++)
        {
            var slot = _ingredientSlots[i]; // 슬롯 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null)
            {
                continue;
            }

            var data = ingredients != null && i < ingredients.Length ? ingredients[i] : null; // 데이터 값입니다.
            slot.gameObject.SetActive(data != null);
            if (data != null)
            {
                // 재료 정보와 획득처를 함께 전달합니다.
                slot.Bind(data.Icon, data.Name, data.Owned, data.Required, data.AcquisitionSource);
            }
        }
    }

    /// <summary>
    /// 새 목록에 선택 요리가 없으면 이전 상세와 재료를 지웁니다.
    /// </summary>
    public void ClearPresentation()
    {
        Bind(null, string.Empty, string.Empty, string.Empty, false, false);
        DisplayIngredients(null);
    }

    #endregion

}
