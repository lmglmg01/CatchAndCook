using UnityEngine;
using TMPro;

/// <summary>
/// 요리 등록 및 수량 변경 팝업의 표시 요소를 보관하는 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiMenuQuantityView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 등록 또는 수량 변경 대상 요리의 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _foodIconImage;
    // 대상 요리 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _dishNameText;
    // 보유 재료로 등록 가능한 최대 수량을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _maxAmountText;
    // 수량 계산에 필요한 재료의 보유·필요 수량 슬롯을 연결합니다.
    [SerializeField] SushiIngredientSlotView[] _ingredientSlots;
    // 등록할 수량을 직접 입력하는 TMP 입력 필드를 연결합니다.
    [SerializeField] TMP_InputField _quantityInput;
    // 등록 수량을 한 개 줄이는 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _minusButton;
    // 등록 수량을 한 개 늘리는 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _plusButton;
    // 최소·최대 수량과 등록 제한 안내 텍스트를 연결합니다.
    [SerializeField] TMP_Text _quantityNoticeText;
    // 등록 또는 수량 변경을 확정할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _confirmButton;
    // 변경을 취소하고 이전 화면으로 돌아갈 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _cancelButton;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;

    #endregion

    #region 임시 데이터 및 테스트 멤버

    // 실제 재료 계산 연결 전 UI 입력 테스트 상한입니다. 현재 임시 최대 수량 문구의 20개와 맞춥니다.
    [SerializeField, Min(1)] int _previewMaxQuantity = 20;

    #endregion

    #region UI 연결 및 표시 설정

    // 같은 Canvas_Sushi의 요청 전달용 IjakayaView를 연결합니다.
    [SerializeField] IjakayaView _owner;

    #endregion

    #region 런타임 상태

    [SerializeField] TMP_Text _previewQuantityText;
    SushiTodayMenuView.IngredientData[] _displayIngredients;

    int _maximumQuantity = -1; // 최대 수량 값입니다.
    bool _canConfirm = true; // 가능 확인 여부입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 수량 제한 값입니다.
    /// </summary>
    int QuantityLimit => _maximumQuantity >= 0 ? _maximumQuantity : Mathf.Max(1, _previewMaxQuantity);

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _cancelButton);
        InitializeButtons();
        RefreshQuantityButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_quantityInput != null)
        {
            _quantityInput.onEndEdit.RemoveListener(ValidateQuantity);
            _quantityInput.onValueChanged.RemoveListener(OnQuantityTextChanged);
        }

        if (_minusButton != null)
        {
            _minusButton.onClick.RemoveListener(DecreaseQuantity);
        }

        if (_plusButton != null)
        {
            _plusButton.onClick.RemoveListener(IncreaseQuantity);
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(ConfirmPreview);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(Close);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_quantityInput != null)
        {
            _quantityInput.onEndEdit.RemoveListener(ValidateQuantity);
            _quantityInput.onEndEdit.AddListener(ValidateQuantity);
            _quantityInput.onValueChanged.RemoveListener(OnQuantityTextChanged);
            _quantityInput.onValueChanged.AddListener(OnQuantityTextChanged);
        }

        if (_minusButton != null)
        {
            _minusButton.onClick.RemoveListener(DecreaseQuantity);
            _minusButton.onClick.AddListener(DecreaseQuantity);
        }

        if (_plusButton != null)
        {
            _plusButton.onClick.RemoveListener(IncreaseQuantity);
            _plusButton.onClick.AddListener(IncreaseQuantity);
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(ConfirmPreview);
            _confirmButton.onClick.AddListener(ConfirmPreview);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(Close);
            _cancelButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 담당 로직이 계산한 최대 수량과 현재 수량을 표시합니다. View에서는 재료를 계산하지 않습니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="current">게이지에 표시할 현재 수치입니다.</param>
    /// <param name="maximum">최대 값입니다.</param>
    /// <param name="canConfirm">가능 확인 여부입니다.</param>
    public void Bind(Sprite icon, string name, int current, int maximum, bool canConfirm)
    {
        IjakayaView.SetDisplayIcon(_foodIconImage, icon);
        if (_dishNameText != null)
        {
            _dishNameText.text = name ?? string.Empty;
        }

        _maximumQuantity = Mathf.Max(0, maximum);
        _canConfirm = canConfirm;
        if (_maxAmountText != null)
        {
            _maxAmountText.text = $"[최대 {_maximumQuantity}접시 등록 가능]";
        }

        if (_quantityInput != null)
        {
            _quantityInput.SetTextWithoutNotify((_maximumQuantity > 0 ? Mathf.Clamp(current, 1, _maximumQuantity) : 0).ToString());
        }

        RefreshQuantityButtons();
    }

    /// <summary>
    /// 최소·최대 수량에서 증감 버튼을 비활성화합니다.
    /// </summary>
    void RefreshQuantityButtons()
    {
        RefreshIngredientAmounts();
        int value = 0; // 값 값입니다.
        bool valid = _quantityInput != null && int.TryParse(_quantityInput.text, out value); // 유효 여부입니다.
        if (!valid)
        {
            value = 0;
        }

        if (_minusButton != null)
        {
            _minusButton.interactable = valid && value > 1;
        }

        if (_plusButton != null)
        {
            _plusButton.interactable = valid && value < QuantityLimit;
        }

        if (_confirmButton != null)
        {
            _confirmButton.interactable = _canConfirm && valid && value >= 1 && value <= QuantityLimit;
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

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 입력된 임시 수량을 한 개 줄입니다.
    /// </summary>
    void DecreaseQuantity()
    {
        ChangeQuantity(-1);
    }

    /// <summary>
    /// 입력된 임시 수량을 한 개 늘립니다.
    /// </summary>
    void IncreaseQuantity()
    {
        ChangeQuantity(1);
    }

    /// <summary>
    /// 입력값만 허용 범위로 보정합니다. 재료나 메뉴 데이터는 변경하지 않습니다.
    /// </summary>
    /// <param name="change">변경 값입니다.</param>
    void ChangeQuantity(int change)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_quantityInput == null)
        {
            return;
        }

        int current; // 현재 값입니다.
        if (!int.TryParse(_quantityInput.text, out current))
        {
            current = 1;
        }

        long next = (long)current + change; // 다음 값입니다.
        int limit = QuantityLimit; // 제한 값입니다.
        int value = limit > 0 ? (int)System.Math.Max(1L, System.Math.Min(next, limit)) : 0; // 값 값입니다.
        _quantityInput.SetTextWithoutNotify(value.ToString());
        RefreshQuantityButtons();
    }

    /// <summary>
    /// 숫자 입력이 끝나면 표시 범위를 정리합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    void ValidateQuantity(string value)
    {
        ChangeQuantity(0);
    }

    /// <summary>
    /// 수량 입력 중 증감·확인 버튼의 표시 상태를 갱신합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    void OnQuantityTextChanged(string value)
    {
        RefreshQuantityButtons();
    }

    /// <summary>
    /// 수량 팝업의 재료 한 줄을 표시합니다.
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
        _displayIngredients = ingredients;
        RefreshIngredientAmounts();
    }

    /// <summary>현재 입력 수량의 표시만 갱신하며 실제 재료나 등록 초안은 변경하지 않습니다.</summary>
    void RefreshIngredientAmounts()
    {
        int quantity = 0;
        if (_quantityInput != null) int.TryParse(_quantityInput.text, out quantity);
        quantity = Mathf.Max(0, quantity);
        if (_previewQuantityText != null) _previewQuantityText.text = $"x{quantity}";
        for (int i = 0; _ingredientSlots != null && i < _ingredientSlots.Length; i++)
        {
            var slot = _ingredientSlots[i];
            if (slot == null) continue;
            var data = _displayIngredients != null && i < _displayIngredients.Length ? _displayIngredients[i] : null;
            slot.gameObject.SetActive(data != null);
            if (data != null)
            {
                int required = (int)System.Math.Min(int.MaxValue, (long)data.Required * quantity);
                // 선택 수량으로 계산된 필요량을 유지하며 획득처를 전달합니다.
                slot.Bind(data.Icon, data.Name, data.Owned, required, data.AcquisitionSource);
            }
        }
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// 확인 동작으로 수량 팝업만 닫습니다. 실제 등록과 재료 차감은 실행하지 않습니다.
    /// </summary>
    void ConfirmPreview()
    {
        ChangeQuantity(0);
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_confirmButton != null && !_confirmButton.interactable)
        {
            return;
        }

        int quantity; // 수량 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_quantityInput != null && int.TryParse(_quantityInput.text, out quantity) && _owner != null && _owner.RequestQuantityConfirmation(quantity))
        {
            return;
        }

        Close();
    }

    #endregion

}
