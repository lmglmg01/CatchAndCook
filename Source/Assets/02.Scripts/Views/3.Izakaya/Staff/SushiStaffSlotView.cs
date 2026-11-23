using UnityEngine;
using TMPro;

/// <summary>
/// 직원 목록에서 한 직원의 간단한 정보를 표시할 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiStaffSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 직원을 선택할 슬롯 자체의 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _selectButton;
    // 직원 선택·잠금 상태를 표현할 슬롯 배경 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _backgroundImage;
    // 직원 초상 또는 잠금 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _portraitImage;
    // 직원 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _nameText;
    // 담당 업무·현재 레벨 또는 미해금 상태를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _roleLevelText;
    // 같은 Canvas_Sushi 루트의 IjakayaView를 연결합니다. Sushi 화면 표시 전환만 요청합니다.
    [SerializeField] IjakayaView _owner;

    #endregion

    #region 임시 데이터 및 테스트 멤버

    // 이 직원 슬롯이 미해금 표시용인지 설정합니다. 직원 데이터와 무관한 UI 시안 값입니다.
    [SerializeField] bool _isLockedPreview;

    #endregion

    #region UI 연결 및 표시 설정

    // 표시할 직원의 식별값입니다. 임시 시안에서는 서빙 0, 요리 1로 지정합니다.
    [SerializeField] int _staffId;

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
        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(SelectStaff);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(SelectStaff);
            _selectButton.onClick.AddListener(SelectStaff);
        }
    }

    /// <summary>
    /// 직원 식별값과 목록 표시를 갱신합니다. 해금 여부는 담당 로직에서 전달합니다.
    /// </summary>
    /// <param name="staffId">직원 식별자 값입니다.</param>
    /// <param name="portrait">초상화 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="roleLevel">역할 레벨 값입니다.</param>
    /// <param name="unlocked">해금 여부입니다.</param>
    public void Bind(int staffId, Sprite portrait, string name, string roleLevel, bool unlocked)
    {
        _staffId = staffId;
        _isLockedPreview = !unlocked;
        IjakayaView.SetDisplayIcon(_portraitImage, portrait);
        if (_nameText != null)
        {
            _nameText.text = name ?? string.Empty;
        }

        if (_roleLevelText != null)
        {
            _roleLevelText.text = roleLevel ?? string.Empty;
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 직원 슬롯의 임시 해금 상태에 맞는 상세 패널을 표시합니다.
    /// </summary>
    void SelectStaff()
    {
        if (_owner != null)
        {
            _owner.SelectStaff(_staffId, _isLockedPreview);
        }
    }

    #endregion

}
