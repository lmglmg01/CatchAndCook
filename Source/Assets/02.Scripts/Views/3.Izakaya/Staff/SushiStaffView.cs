using UnityEngine;
using TMPro;

/// <summary>
/// 직원 목록과 해금·미해금 상세 정보를 표시할 관리 화면 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiStaffView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 직원 목록의 슬롯을 위에서 아래 순서로 연결합니다.
    [SerializeField] SushiStaffSlotView[] _staffSlots;
    // 해금한 직원의 상세 정보 패널을 연결합니다.
    [SerializeField] GameObject _unlockedDetailRoot;
    // 미해금 직원의 잠금·해금 조건 패널을 연결합니다.
    [SerializeField] GameObject _lockedDetailRoot;
    // 해금한 직원의 초상 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _portraitImage;
    // 선택한 직원 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _nameText;
    // 선택한 직원의 간단한 설명 텍스트를 연결합니다.
    [SerializeField] TMP_Text _descriptionText;
    // 선택한 직원의 해금 조건과 상태를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _unlockConditionText;
    // 직원의 담당 업무를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _jobText;
    // 직원의 현재 레벨과 기본 능력치를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _statsText;
    // 능력치 게이지의 너비를 조절할 채움 영역 RectTransform을 연결합니다.
    [SerializeField] RectTransform _statGaugeFill;
    // 패시브 스킬 이름·효과·해금 레벨을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _skillText;
    // 직원 강화 비용을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _upgradeCostText;
    // 직원을 강화할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _upgradeButton;
    // 미해금 직원의 잠금 초상 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _lockedPortraitImage;
    // 미해금 직원 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _lockedNameText;
    // 미해금 직원의 해금 안내 설명 텍스트를 연결합니다.
    [SerializeField] TMP_Text _lockedDescriptionText;
    // 해금 조건의 현재 진행도를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _unlockProgressText;
    // 해금 진행 게이지의 너비를 조절할 채움 영역 RectTransform을 연결합니다.
    [SerializeField] RectTransform _unlockGaugeFill;
    // 직원 관리 화면의 뒤로가기 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;
    // 직원 강화 팝업과 외부 요청을 전달할 같은 Canvas_Sushi의 IjakayaView를 연결합니다.
    [SerializeField] IjakayaView _owner;
    // StaffUpgrade/Panel의 직원 강화 확인 View를 연결합니다.
    [SerializeField] SushiStaffUpgradeView _staffUpgradeView;

    #endregion

    #region 임시 데이터 및 테스트 멤버

    // 실제 데이터가 전달되기 전의 해금·미해금 시안입니다. 실제 직원 레벨을 변경하지 않습니다.
    [SerializeField] Presentation _previewPresentation;

    #endregion

    #region 런타임 상태

    Presentation _presentation; // 표시 데이터 값입니다.
    int _selectedStaffId = -1; // 선택 직원 식별자 값입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 직원 파트에서 현재 표시하는 직원 식별값입니다.
    /// </summary>
    public int SelectedStaffId => _selectedStaffId;
    public StaffData SelectedStaff => FindStaff(_selectedStaffId);
    public StaffData[] Staff => _presentation?.Staff;
    public bool CanManage => _presentation != null && _presentation.CanManage;

    #endregion

    #region 표시 데이터 형식

    /// <summary>
    /// 직원 한 명의 목록·상세·강화 확인 표시 데이터입니다.
    /// </summary>
    [System.Serializable]
    public sealed class StaffData
    {

        #region 멤버

        #region 런타임 상태

        public int StaffId; // 실제 직원 식별값입니다. UI 연결 오브젝트가 아닙니다.
        public Sprite Portrait; // 직원 초상입니다.
        // Name: 이름 값입니다.
        // RoleLevel: 역할 레벨 값입니다.
        // Description: 설명 값입니다.
        // UnlockCondition: 해금 조건 값입니다.
        // Job: 직무 값입니다.
        // Stats: 능력치 값입니다.
        // Perk: 특성 값입니다.
        // Cost: 비용 값입니다.
        public string Name, RoleLevel, Description, UnlockCondition, Job, Stats, Perk, Cost;
        public bool Unlocked, CanUpgrade; // 해금 및 강화 가능 판정 결과입니다.
        public float StatRatio, UnlockRatio; // 담당 로직이 계산한 0~1 표시 비율입니다.
        public string UnlockProgress; // 미해금 직원의 현재 진행 문구입니다.
        public int BeforeLevel, AfterLevel;
        public string ActiveEffectText; // 실제 강화 설정에서 계산한 스킬 효과입니다.
        /// <summary>현재 화면에 전달된 직원의 강화 레벨입니다.</summary>
        public int CurrentLevel => BeforeLevel;
        [UnityEngine.Serialization.FormerlySerializedAs("PassiveUnlockLevel")]
        public int ActiveUnlockLevel;
        [UnityEngine.Serialization.FormerlySerializedAs("PassiveUnlocked")]
        public bool ActiveUnlocked; // 강화 단계에 따른 Active 해금 여부입니다.
        public string BeforeStats, AfterStats, UpgradeStatus; // 강화 전후 작업 수치와 안내입니다.

        #endregion

        #endregion

    }

    /// <summary>
    /// 직원 파트 전체를 갱신할 표시 묶음입니다.
    /// </summary>
    [System.Serializable]
    public sealed class Presentation
    {

        #region 멤버

        #region 런타임 상태

        public StaffData[] Staff; // 직원 목록입니다.
        public int SelectedStaffId = -1; // -1이면 현재 선택을 유지하고, 없으면 첫 직원을 표시합니다.
        public bool CanManage = true; // 영업 중에는 false를 전달합니다.

        #endregion

        #endregion

    }

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
        if (_presentation == null && _previewPresentation != null && _previewPresentation.Staff != null && _previewPresentation.Staff.Length > 0)
        {
            Display(_previewPresentation);
        }
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.RemoveListener(ShowUpgradeConnectionNotice);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.RemoveListener(ShowUpgradeConnectionNotice);
            _upgradeButton.onClick.AddListener(ShowUpgradeConnectionNotice);
        }
    }

    /// <summary>
    /// 선택한 직원의 표시 정보와 강화 가능 상태만 갱신합니다.
    /// </summary>
    /// <param name="portrait">초상화 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="unlockCondition">해금 조건 값입니다.</param>
    /// <param name="job">직무 값입니다.</param>
    /// <param name="stats">능력치 값입니다.</param>
    /// <param name="perk">특성 값입니다.</param>
    /// <param name="cost">비용 값입니다.</param>
    /// <param name="unlocked">해금 여부입니다.</param>
    /// <param name="canUpgrade">가능 강화 여부입니다.</param>
    public void Bind(Sprite portrait, string name, string description, string unlockCondition, string job, string stats, string perk, string cost, bool unlocked, bool canUpgrade)
    {
        IjakayaView.SetDisplayIcon(_portraitImage, portrait);
        IjakayaView.SetDisplayIcon(_lockedPortraitImage, portrait);
        if (_nameText != null)
        {
            _nameText.text = name ?? string.Empty;
        }

        if (_lockedNameText != null)
        {
            _lockedNameText.text = name ?? string.Empty;
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = description ?? string.Empty;
        }

        if (_lockedDescriptionText != null)
        {
            _lockedDescriptionText.text = unlockCondition ?? string.Empty;
        }

        if (_unlockConditionText != null)
        {
            _unlockConditionText.text = unlockCondition ?? string.Empty;
        }

        if (_jobText != null)
        {
            _jobText.text = job ?? string.Empty;
        }

        if (_statsText != null)
        {
            _statsText.text = stats ?? string.Empty;
        }

        if (_skillText != null)
        {
            _skillText.text = perk ?? string.Empty;
        }

        if (_upgradeCostText != null)
        {
            _upgradeCostText.text = cost ?? string.Empty;
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.interactable = canUpgrade && unlocked;
        }

        ShowDetail(!unlocked);
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 다른 화면이나 데이터를 변경하지 않고 이 화면만 비활성화합니다.
    /// </summary>
    public void Close()
    {
        var book = GetComponentInParent<SushiMenuBookView>();
        if (book != null) { book.Close(); return; }
        if (_pageRoot != null)
        {
            _pageRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 선택한 직원의 해금 상태에 맞는 상세 패널만 표시합니다.
    /// </summary>
    /// <param name="isLocked">잠금 여부입니다.</param>
    public void ShowDetail(bool isLocked)
    {
        // 책에서는 잠금 직원도 같은 상세 배치로 보여주고 강화만 막습니다.
        if (GetComponentInParent<SushiMenuBookView>() != null) isLocked = false;
        if (_unlockedDetailRoot != null)
        {
            _unlockedDetailRoot.SetActive(!isLocked);
        }

        if (_lockedDetailRoot != null)
        {
            _lockedDetailRoot.SetActive(isLocked);
        }
    }

    /// <summary>
    /// 선택한 직원의 강화 확인 팝업을 요청하며 실제 데이터는 변경하지 않습니다.
    /// </summary>
    void ShowUpgradeConnectionNotice()
    {
        if (_owner != null)
        {
            _owner.OpenStaffUpgrade();
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 직원 능력치와 해금 게이지의 표시 비율만 갱신합니다.
    /// </summary>
    /// <param name="statRatio">능력치 비율 값입니다.</param>
    /// <param name="unlockRatio">해금 비율 값입니다.</param>
    /// <param name="unlockText">해금 텍스트 값입니다.</param>
    public void SetProgress(float statRatio, float unlockRatio, string unlockText)
    {
        if (_statGaugeFill != null && _statGaugeFill.parent is RectTransform statParent)
        {
            _statGaugeFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, statParent.rect.width - 48f) * Mathf.Clamp01(statRatio));
        }

        if (_unlockGaugeFill != null && _unlockGaugeFill.parent is RectTransform unlockParent)
        {
            _unlockGaugeFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, unlockParent.rect.width - 200f) * Mathf.Clamp01(unlockRatio));
        }

        if (_unlockProgressText != null)
        {
            _unlockProgressText.text = unlockText ?? string.Empty;
        }
    }

    /// <summary>
    /// 직원 목록과 선택 상세를 갱신합니다. 해금·강화 판정은 외부 결과를 사용합니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    public void Display(Presentation data)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (data == null)
        {
            return;
        }

        _presentation = data;
        // i: 반복 값입니다.
        for (int i = 0; _staffSlots != null && i < _staffSlots.Length; i++)
        {
            var slot = _staffSlots[i]; // 슬롯 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null)
            {
                continue;
            }

            var item = data.Staff != null && i < data.Staff.Length ? data.Staff[i] : null; // 아이템 값입니다.
            slot.gameObject.SetActive(item != null);
            if (item != null)
            {
                slot.Bind(item.StaffId, item.Portrait, item.Name, item.RoleLevel, item.Unlocked);
            }
        }

        if (data.SelectedStaffId >= 0)
        {
            _selectedStaffId = data.SelectedStaffId;
        }

        var selected = FindStaff(_selectedStaffId); // 선택 값입니다.
        if (selected == null && data.Staff != null)
        {
            selected = System.Array.Find(data.Staff, item => item != null);
        }

        if (selected != null)
        {
            SelectStaff(selected.StaffId);
        }
        else
        {
            _selectedStaffId = -1;
            if (_staffUpgradeView != null)
            {
                _staffUpgradeView.Bind(-1, null, string.Empty, 0, 0, string.Empty, string.Empty, string.Empty, string.Empty, false);
            }

            if (_unlockedDetailRoot != null)
            {
                _unlockedDetailRoot.SetActive(false);
            }

            if (_lockedDetailRoot != null)
            {
                _lockedDetailRoot.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 캐시된 직원 정보로 선택 상세를 전환합니다. 데이터가 없으면 false입니다.
    /// </summary>
    /// <param name="staffId">직원 식별자 값입니다.</param>
    public bool SelectStaff(int staffId)
    {
        var item = FindStaff(staffId); // 아이템 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (item == null)
        {
            return false;
        }

        _selectedStaffId = staffId;
        Bind(item.Portrait, item.Name, item.Description, item.UnlockCondition, item.Job, item.Stats, item.Perk, item.Cost, item.Unlocked, _presentation.CanManage && item.CanUpgrade);
        SetProgress(item.StatRatio, item.UnlockRatio, item.UnlockProgress);
        if (GetComponentInParent<SushiMenuBookView>()?.CurrentMode == 3)
            GetComponentInParent<SushiRestaurantStyleView>()?.ApplyStaff();
        return true;
    }

    /// <summary>
    /// 선택 직원의 강화 전후 정보를 강화 확인 View에 전달합니다.
    /// </summary>
    public void DisplaySelectedUpgrade()
    {
        var item = FindStaff(_selectedStaffId); // 아이템 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (item == null || _staffUpgradeView == null)
        {
            return;
        }

        _staffUpgradeView.Bind(item.StaffId, item.Portrait, item.Name, item.BeforeLevel, item.AfterLevel, item.BeforeStats, item.AfterStats, item.Perk, item.Cost, _presentation.CanManage && item.Unlocked && item.CanUpgrade, item.UpgradeStatus);
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 현재 표시 데이터에서 직원 식별값에 해당하는 정보를 찾습니다.
    /// </summary>
    /// <param name="staffId">직원 식별자 값입니다.</param>
    StaffData FindStaff(int staffId)
    {
        return _presentation?.Staff != null ? System.Array.Find(_presentation.Staff, item => item != null && item.StaffId == staffId) : null;
    }

    #endregion

}
