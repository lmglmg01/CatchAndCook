using UnityEngine;
using TMPro;

/// <summary>
/// 직원 강화 전후 정보를 표시하고 실행 요청만 전달하는 확인 팝업 뷰입니다.
/// 실제 골드 차감, 레벨 상승과 특전 해금은 담당 로직에서 처리합니다.
/// </summary>
public class SushiStaffUpgradeView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 같은 Canvas_Sushi 루트의 요청 전달용 IjakayaView를 연결합니다.
    [SerializeField] IjakayaView _owner;
    // 반투명 배경까지 포함한 StaffUpgrade 루트 GameObject를 연결합니다.
    [SerializeField] GameObject _pageRoot;
    // 강화할 직원의 초상 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _portraitImage;
    // 직원 이름과 강화 안내를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _nameText;
    // 강화 전 레벨을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _beforeLevelText;
    // 강화 후 예상 레벨을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _afterLevelText;
    // 강화 전 작업 속도 또는 소요 시간을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _beforeStatsText;
    // 강화 후 작업 속도 또는 소요 시간을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _afterStatsText;
    // Lv.5 특전의 설명과 해금 상태를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _perkText;
    // 강화에 필요한 골드를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _costText;
    // 재화 부족·최대 레벨 등 담당 로직의 안내 문구를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _statusText;
    // 강화 실행을 요청할 확인 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _confirmButton;
    // 데이터를 변경하지 않고 닫을 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;

    #endregion

    #region 런타임 상태

    int _staffId; // 직원 식별자 값입니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼을 연결합니다.
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
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(Confirm);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 버튼 이벤트를 유지하면서 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(Confirm);
            _confirmButton.onClick.AddListener(Confirm);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// 담당 로직이 계산한 직원 강화 비교 정보와 버튼 상태를 표시합니다.
    /// </summary>
    /// <param name="staffId">직원 식별자 값입니다.</param>
    /// <param name="portrait">초상화 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="beforeLevel">이전 레벨 값입니다.</param>
    /// <param name="afterLevel">이후 레벨 값입니다.</param>
    /// <param name="beforeStats">이전 능력치 값입니다.</param>
    /// <param name="afterStats">이후 능력치 값입니다.</param>
    /// <param name="perk">특성 값입니다.</param>
    /// <param name="cost">비용 값입니다.</param>
    /// <param name="canUpgrade">가능 강화 여부입니다.</param>
    /// <param name="status">상태 값입니다.</param>
    public void Bind(int staffId, Sprite portrait, string name, int beforeLevel, int afterLevel, string beforeStats, string afterStats, string perk, string cost, bool canUpgrade, string status = "")
    {
        _staffId = staffId;
        IjakayaView.SetDisplayIcon(_portraitImage, portrait);
        if (_nameText != null)
        {
            _nameText.text = name ?? string.Empty;
        }

        if (_beforeLevelText != null)
        {
            _beforeLevelText.text = $"현재 Lv.{beforeLevel}";
        }

        if (_afterLevelText != null)
        {
            _afterLevelText.text = $"강화 후 Lv.{afterLevel}";
        }

        if (_beforeStatsText != null)
        {
            _beforeStatsText.text = beforeStats ?? string.Empty;
        }

        if (_afterStatsText != null)
        {
            _afterStatsText.text = afterStats ?? string.Empty;
        }

        if (_perkText != null)
        {
            _perkText.text = perk ?? string.Empty;
        }

        if (_costText != null)
        {
            _costText.text = cost ?? string.Empty;
        }

        if (_statusText != null)
        {
            _statusText.text = status ?? string.Empty;
        }

        if (_confirmButton != null)
        {
            _confirmButton.interactable = canUpgrade;
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 표시할 직원의 식별값만 지정합니다.
    /// </summary>
    /// <param name="staffId">직원 식별자 값입니다.</param>
    public void SetTarget(int staffId)
    {
        _staffId = staffId;
    }

    #endregion

    #region 선택 및 외부 요청

    /// <summary>
    /// 실제 강화 요청만 전달하며 담당 로직이 연결되지 않았으면 시안 팝업만 닫습니다.
    /// </summary>
    void Confirm()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_owner != null && _owner.RequestStaffUpgrade(_staffId))
        {
            return;
        }

        Close();
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 다른 화면과 데이터를 변경하지 않고 이 팝업만 비활성화합니다.
    /// </summary>
    public void Close()
    {
        if (_pageRoot != null)
        {
            _pageRoot.SetActive(false);
        }
    }

    #endregion

}
