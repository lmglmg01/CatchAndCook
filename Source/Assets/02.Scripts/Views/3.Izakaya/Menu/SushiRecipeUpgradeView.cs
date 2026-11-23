using UnityEngine;
using TMPro;

/// <summary>
/// 요리 강화 전후 정보와 재료를 표시할 확인 팝업 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiRecipeUpgradeView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 강화 대상 요리와 확인 문구를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _promptText;
    // 강화 재료 소모 안내 텍스트를 연결합니다.
    [SerializeField] TMP_Text _consumptionNoticeText;
    // 강화 전 요리 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _beforeIconImage;
    // 강화 전 레벨과 수치를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _beforeStatsText;
    // 강화 후 요리 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _afterIconImage;
    // 강화 후 예상 레벨과 수치를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _afterStatsText;
    // 강화 재료의 보유·필요 수량을 표시할 슬롯을 연결합니다.
    [SerializeField] SushiIngredientSlotView[] _ingredientSlots;
    // 요리 강화를 확정할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _upgradeButton;
    // 강화하지 않고 팝업을 닫을 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;
    // 같은 Canvas_Sushi 루트의 IjakayaView를 연결합니다. Sushi 화면 표시 전환만 요청합니다.
    [SerializeField] IjakayaView _owner;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;
    // 강화 팝업 하단의 필요 골드 텍스트를 연결합니다.
    [SerializeField] TMP_Text _goldCostText;

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
        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.RemoveListener(ShowUpgradeResult);
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
        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.RemoveListener(ShowUpgradeResult);
            _upgradeButton.onClick.AddListener(ShowUpgradeResult);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 담당 로직에서 계산한 강화 비교 정보와 실행 가능 여부를 표시합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="prompt">입력 안내 값입니다.</param>
    /// <param name="before">이전 값입니다.</param>
    /// <param name="after">이후 값입니다.</param>
    /// <param name="cost">비용 값입니다.</param>
    /// <param name="canUpgrade">가능 강화 여부입니다.</param>
    public void Bind(Sprite icon, string prompt, string before, string after, string cost, bool canUpgrade)
    {
        IjakayaView.SetDisplayIcon(_beforeIconImage, icon);
        IjakayaView.SetDisplayIcon(_afterIconImage, icon);
        if (_promptText != null)
        {
            _promptText.text = prompt ?? string.Empty;
        }

        if (_beforeStatsText != null)
        {
            _beforeStatsText.text = before ?? string.Empty;
        }

        if (_afterStatsText != null)
        {
            _afterStatsText.text = after ?? string.Empty;
        }

        if (_goldCostText != null)
        {
            _goldCostText.text = cost ?? string.Empty;
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
    /// 강화 확인 팝업을 닫고 준비된 완료 화면만 표시합니다. 실제 강화·재료 차감은 실행하지 않습니다.
    /// </summary>
    void ShowUpgradeResult()
    {
        // 담당 로직이 연결되면 실제 성공 통보 전에는 완료 팝업을 자동으로 표시하지 않습니다.
        if (_owner != null && _owner.RequestRecipeUpgrade())
        {
            return;
        }

        Close();
        if (_owner != null)
        {
            _owner.OpenRecipeUpgradeResult();
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 강화 재료 한 줄을 표시합니다.
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
                // 강화 재료 정보와 획득처를 함께 전달합니다.
                slot.Bind(data.Icon, data.Name, data.Owned, data.Required, data.AcquisitionSource);
            }
        }
    }

    #endregion

}
