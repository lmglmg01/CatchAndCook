using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 장비 강화 창의 기본·근접·원거리 탭 종류입니다.
/// </summary>
public enum EquipTabType
{
    Base,
    Melee,
    Ranged,
    Count
}

// 기존 코드 호환용 열거형을 유지합니다. 새 장비 식별에는 EquipmentData.EquipType을 사용합니다.

/// <summary>
/// 기존 코드 호환을 위해 유지하는 장비 종류입니다.
/// </summary>
public enum EquipType
{
    Knife,
    Gig,
    OxygenCylinder,
    SwimSuit,
    FishBag,
    Gun,
    Count
}

/// <summary>
/// 컨트롤러의 장비 정보·레벨을 종류별 슬롯에 전달하고 기존 강화 시스템과 UI를 연결합니다.
/// </summary>
public class UpgradeEquipView : MonoBehaviour, IEquipUpgradeView
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] EquipController _equipController; // 장비·레벨 관리자
    [SerializeField] EquipTabType _currentEquipTabType; // 처음 표시할 기본/근접/원거리 탭
    [SerializeField] UnityEngine.UI.Button[] _equipTypeButtons = new UnityEngine.UI.Button[3]; // 탭 버튼 배열
    [SerializeField] UpgradeEquipSlotView[] _slotViews = new UpgradeEquipSlotView[6]; // UpgradeList의 장비 슬롯 6개를 위에서 아래 순서로 연결합니다.
    [SerializeField] TMP_Text _guideText; // 안내 텍스트
    [SerializeField] UnityEngine.UI.Button _closeButton; // 닫기 버튼
    // UI 컴포넌트가 아닌 장비 아이콘 Sprite를 나이프·작살·산소통·잠수복·가방·총 순서로 연결합니다.
    // EquipmentData에 아이콘 필드가 없어 View에서 보관하며, 미연결 시 슬롯의 기존 Sprite를 유지합니다.
    [SerializeField] Sprite[] _equipmentIcons = new Sprite[6];

    [Header("6칸 고정 표시")]
    [SerializeField] bool _showEmptySlots; // 켜면 탭에 장비가 없어도 연결된 슬롯 6칸을 빈 상태로 유지합니다. 기존 프리팹의 기본 동작은 유지합니다.
    [SerializeField] GameObject[] _emptySlotPlaceholders = new GameObject[6]; // 각 UpgradeSlot의 EmptyState 오브젝트를 슬롯 순서대로 연결합니다.
    [SerializeField] TMP_Text[] _slotIconLabels = new TMP_Text[6]; // 각 ItemIcon/IconLabel을 연결합니다. 아이콘 미연결 시 장비 종류를 도형 위에 표시합니다.

    #endregion

    #region 임시 데이터 및 테스트 멤버

    [SerializeField] TMP_Text _previewNoticeText; // 창 하단의 임시 데이터/장비 비교 안내 TMP_Text를 연결합니다.

    #endregion

    #region UI 연결 및 표시 설정

    [SerializeField] TMP_Text _walletText; // 제목 오른쪽의 보유 골드 TMP_Text를 연결합니다.
    // Board 전용 화면이 연결된 경우에만 전체 탭과 정렬 표시를 사용합니다.
    [SerializeField] UpgradeFailurePopupView _failurePopup; // 강화 실패 시 표시하는 안내 팝업입니다.
    [SerializeField] UpgradeEquipScreenView _boardScreen;

    #endregion

    #region 런타임 상태

    bool _showAllEquipment = true; // 표시 전체 장비 여부입니다.
    const int EquipmentCount = 6; // 컨트롤러의 장비 종류 개수
    static readonly int[][] TabEquipment =
    {
        new[]
        { 1, 2, 3, 4 },
        new[]
        { 0 },
        new[]
        { 5 }
    }; // 각 탭에 표시할 EquipmentData.EquipType 인덱스
    readonly EquipmentData[] _equipmentDatas = new EquipmentData[EquipmentCount]; // 같은 인덱스의 실제 장비 데이터 캐시입니다.
    readonly int[] _equipmentLevels = new int[EquipmentCount]; // 컨트롤러의 레벨 변경을 감지할 캐시입니다.
    readonly UpgradeEquipSlotView.DisplayData[] _displayData = new UpgradeEquipSlotView.DisplayData[EquipmentCount]; // 기존 인터페이스로 전달받은 장비별 표시값입니다.
    UnityEngine.UI.Button[] _wiredTabButtons; // 제거할 때 정확한 버튼 이벤트를 해제하기 위한 연결 당시 버튼입니다.
    UnityEngine.Events.UnityAction[] _tabCallbacks; // 탭마다 한 번만 등록하는 콜백입니다.
    UpgradeEquipSlotView[] _wiredSlots; // 강화 이벤트를 연결했던 기존 슬롯 목록입니다.
    Action[] _slotCallbacks; // 슬롯별 강화 대상을 전달하는 콜백입니다.
    float _gold; // 외부에서 전달한 보유 골드이며 View에서는 변경하지 않습니다.
    bool _goldKnown, _initialized, _hadUpgradeHandler; // 골드 수신·초기화·강화 처리 연결 상태입니다.
    int _publishedCount; // 인터페이스로 수신한 장비 개수입니다.
    EquipController _readController; // 마지막으로 읽은 컨트롤러이며 참조 교체를 감지합니다.

    #endregion

    #region 임시 데이터 및 테스트 멤버

    bool _previewVisible; // 임시 표시 중 실제 데이터가 들어오면 전체 목록을 다시 그리기 위한 상태입니다.

    #endregion

    #region 이벤트 액션

    /// <summary>
    /// 창이 열리면 기존 PlayScene에 정보 갱신을 요청합니다.
    /// </summary>
    public event Action OnOpened;

    /// <summary>
    /// 장비 인덱스를 기존 PlayScene으로 전달하며 실제 강화는 해당 시스템이 처리합니다.
    /// </summary>
    public event Action<int> OnUpgradeRequested;

    #endregion

    #region 임시 데이터 및 테스트 멤버

    // ContextMenu 전용 비직렬화 표시 상태입니다. 컨트롤러·외부 표시 캐시·초기 프리뷰 설정은 보존합니다.
    bool _sortingPreview;
    int[] _sortingPreviewLevels; // 그리기 순서 미리보기 레벨 목록입니다.
    const float SortingPreviewGold = 500f; // 그리기 순서 미리보기 골드 값입니다.

    [Header("임시 표시 데이터 · 실제 장비와 재화는 변경하지 않음")]
    [SerializeField] bool _usePreviewWhenNoData = true; // 실제 컨트롤러와 외부 장비 표시값이 모두 없을 때만 임시 데이터를 표시합니다.
    [SerializeField, Min(0f)] float _previewGold = 5000f; // 임시 화면에만 표시할 보유 골드입니다. 별도 UI 연결은 필요 없습니다.
    // 실제 장비와 같은 이름·설명·단계별 표시값을 테스트에서도 읽습니다.
    [SerializeField] EquipmentData[] _previewEquipmentDatas = new EquipmentData[6];
    [SerializeField] int[] _previewLevels =
    { 1, 2, 2, 1, 3, 1 }; // 나이프·작살·산소통·잠수복·가방·총의 임시 레벨입니다.
    int[] _previewRuntimeLevels; // 플레이 중에만 변경하는 장비별 테스트 레벨입니다. 직렬화된 초기값·컨트롤러 레벨과 분리합니다.
    static readonly float[] PreviewBaseCosts =
    { 120f, 240f, 350f, 450f, 650f, 900f }; // 각 장비의 임시 초기 레벨에서 표시할 강화 비용입니다.
    static readonly string[] PreviewNames =
    { "다이버 나이프", "포획용 작살", "경량 산소통", "탐사 잠수복", "확장 물고기 가방", "수중 소총" }; // 임시 장비 이름입니다.
    static readonly string[] PreviewDescriptions =
    {
        "가까운 대상을 빠르게 베는 기본 장비입니다.",
        "물고기를 포획하는 작살입니다. 사거리를 늘릴 수 있습니다.",
        "잠수 중 사용할 수 있는 산소량을 늘립니다.",
        "몸을 보호하고 수영 성능을 높이는 잠수복입니다.",
        "탐사 중 획득한 아이템을 더 많이 담을 수 있습니다.",
        "멀리 있는 대상을 공격하는 원거리 장비입니다."
    }; // 실제 설명 데이터와 분리한 임시 안내입니다.
    static readonly string[] PreviewIconLabels =
    { "나이프", "작살", "산소", "잠수복", "가방", "소총" }; // 아이콘 미연결 시 도형 위에 사용할 종류별 문구입니다.
    static readonly Color[] PreviewIconColors =
    {
        new Color(.34f, .55f, .59f),
        new Color(.35f, .57f, .49f),
        new Color(.33f, .61f, .72f),
        new Color(.46f, .51f, .69f),
        new Color(.66f, .53f, .35f),
        new Color(.54f, .51f, .59f)
    }; // 임시 도형의 장비별 색상입니다.
    bool _contextUpgradePreview; // 대상 정보 강화 미리보기 여부입니다.

    #endregion

    #region 런타임 상태

    float _contextUpgradeGold; // 대상 정보 강화 골드 값입니다.
    int[] _contextUpgradeLevels; // 대상 정보 강화 레벨 목록입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 전체 장비 탭 여부입니다.
    /// </summary>
    public bool IsAllEquipmentTab => _showAllEquipment;

    /// <summary>
    /// 현재 장비 탭 값입니다.
    /// </summary>
    public EquipTabType CurrentEquipmentTab => _currentEquipTabType;

    /// <summary>
    /// 표시 골드 값입니다.
    /// </summary>
    public float DisplayGold => _contextUpgradePreview ? _contextUpgradeGold : _sortingPreview ? SortingPreviewGold : UsesPreviewData ? _previewGold : _gold;

    /// <summary>
    /// 표시 골드 여부입니다.
    /// </summary>
    public bool HasDisplayGold => UsesPreviewData || _goldKnown;

    /// <summary>
    /// 실제 데이터가 하나라도 연결·수신된 경우 임시 데이터를 사용하지 않습니다.
    /// </summary>
    bool UsesPreviewData
    {
        get
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (_sortingPreview || _contextUpgradePreview)
            {
                return true;
            }

            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (!_usePreviewWhenNoData || _equipController != null || _publishedCount > 0)
            {
                return false;
            }

            // data: 데이터 값입니다.
            foreach (var data in _displayData)
            {
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (data.HasInfo)
                {
                    return false;
                }
            }

            return true;
        }
    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 기존 탭·닫기 버튼과 슬롯 이벤트를 중복 없이 초기화합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeButton);
        Initialize();
    }

    /// <summary>
    /// 창을 다시 열 때 실제 장비를 읽고 기존 시스템에도 최신 표시값을 요청합니다.
    /// </summary>
    void OnEnable()
    {
        Initialize();
        if (_boardScreen != null)
        {
            _showAllEquipment = true;
        }

        GetEquipmentData();
        if (!_sortingPreview && !_contextUpgradePreview)
        {
            OnOpened?.Invoke();
        }

        RefreshAvailability();
    }

    /// <summary>
    /// 창이 보이는 동안 장비 참조·레벨·처리 연결 변경만 감지해 필요한 때에 갱신합니다.
    /// </summary>
    void LateUpdate()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_readController != _equipController)
        {
            GetEquipmentData();
            return;
        }

        if (_equipController != null)
        {
            var datas = _equipController.EquipmentDatas; // 데이터 목록 값입니다.
            var levels = _equipController.EquipmentLevels; // 레벨 목록 값입니다.
            // i: 반복 값입니다.
            for (int i = 0; i < EquipmentCount; i++)
            {
                var data = i < datas.Count && datas[i] != null && (int)datas[i].Type == i ? datas[i] : null; // 데이터 값입니다.
                int level = i < levels.Count ? levels[i] : 0; // 레벨 값입니다.
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (data != _equipmentDatas[i] || level != _equipmentLevels[i])
                {
                    GetEquipmentData();
                    break;
                }
            }
        }

        if (_hadUpgradeHandler != (OnUpgradeRequested != null))
        {
            RefreshAvailability();
        }
    }

    /// <summary>
    /// 뷰가 꺼질 때 이벤트와 진행 중인 표시를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        // 창을 닫으면 테스트 표시만 해제합니다. 저장된 실제 데이터는 건드리지 않습니다.
        EndContextUpgradePreview();
        EndSortingTestPreview();
    }

    /// <summary>
    /// View 제거 시 이 View가 등록한 탭·닫기·슬롯 이벤트만 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_wiredTabButtons != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _wiredTabButtons.Length; i++)
            {
                if (_wiredTabButtons[i] != null && _tabCallbacks[i] != null)
                {
                    _wiredTabButtons[i].onClick.RemoveListener(_tabCallbacks[i]);
                }
            }
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        if (_wiredSlots != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _wiredSlots.Length; i++)
            {
                if (_wiredSlots[i] != null)
                {
                    _wiredSlots[i].OnUpgradeItem -= _slotCallbacks[i];
                }
            }
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 장비 종류에 대응하는 표시용 아이콘을 반환합니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    public Sprite EquipmentIconForPresentation(int type)
    {
        return _equipmentIcons != null && type >= 0 && type < _equipmentIcons.Length ? _equipmentIcons[type] : null;
    }

    /// <summary>
    /// 현재 표시된 슬롯 하나만 인터페이스 표시 캐시와 동기화합니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    void RefreshPublishedSlot(int type)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_sortingPreview || _contextUpgradePreview)
        {
            SetItemSlots();
            return;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_equipController != null || _slotViews == null)
        {
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_previewVisible && !UsesPreviewData)
        {
            SetItemSlots();
            return;
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in _slotViews)
        {
            if (slot != null && slot.gameObject.activeSelf && slot.EquipmentType == type)
            {
                slot.SetDisplayData(_displayData[type]);
            }
        }

        if (_displayData[type].HasInfo)
        {
            SetGuideText(false);
        }

        RefreshAvailability();
    }

    /// <summary>
    /// 탭의 장비 개수만큼만 슬롯을 켜고 장비 데이터·레벨 또는 기존 인터페이스 표시값을 전달합니다.
    /// </summary>
    void SetItemSlots()
    {
        int tab = Mathf.Clamp((int)_currentEquipTabType, 0, TabEquipment.Length - 1); // 탭 값입니다.
        _currentEquipTabType = (EquipTabType)tab;
        int[] types = _boardScreen != null && _showAllEquipment ? new[]
        { 0, 1, 2, 3, 4, 5 }

        : TabEquipment[tab]; // 종류 목록입니다.
        int shown = 0; // 노출 값입니다.
        int slotIndex = 0; // 슬롯 인덱스 값입니다.
        _previewVisible = UsesPreviewData;
        if (_slotViews != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _slotViews)
            {
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (slot == null)
                {
                    slotIndex++;
                    continue;
                }

                // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
                if (shown >= types.Length)
                {
                    slot.SetDisplayData(UpgradeEquipSlotView.EmptyDisplay(-1));
                    SetEmptySlot(slotIndex, true);
                    slot.SetEquipSlot(_showEmptySlots);
                    slotIndex++;
                    continue;
                }

                int type = types[shown++]; // 종류 값입니다.
                SetEmptySlot(slotIndex, false);
                if (_sortingPreview || _contextUpgradePreview)
                {
                    slot.SetDisplayData(CreatePreviewDisplay(type));
                }
                else if (_equipmentDatas[type] != null)
                {
                    slot.UpdateItemData(_equipmentDatas[type], _equipmentLevels[type]);
                }
                else if (_previewVisible)
                {
                    slot.SetDisplayData(CreatePreviewDisplay(type));
                }
                else
                {
                    slot.SetDisplayData(_equipController == null ? _displayData[type] : UpgradeEquipSlotView.EmptyDisplay(type));
                }

                slot.SetItemIcon(_equipmentIcons != null && type < _equipmentIcons.Length ? _equipmentIcons[type] : null);
                UpdateSlotIllustration(slotIndex, type);
                slot.SetEquipSlot(true);
                slotIndex++;
            }
        }

        RefreshAvailability();
        UpdateWindowLabels();
        bool hasData = false; // 데이터 여부입니다.
        // type: 종류 값입니다.
        foreach (int type in types)
        {
            if (_equipmentDatas[type] != null || _equipController == null && _displayData[type].HasInfo)
            {
                hasData = true;
            }
        }

        if (!hasData && !_previewVisible)
        {
            ShowGuide(_equipController == null ? "EquipController를 연결하거나 장비 표시 데이터를 전달해주세요." : "현재 탭의 장비 데이터가 없습니다.");
        }
        else
        {
            SetGuideText(false);
        }
    }

    /// <summary>
    /// 최신 골드와 기존 강화 처리 구독 여부를 모든 슬롯의 버튼 상태에 반영합니다.
    /// </summary>
    void RefreshAvailability()
    {
        _hadUpgradeHandler = OnUpgradeRequested != null;
        bool preview = UsesPreviewData; // 미리보기 여부입니다.
        if (_slotViews != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _slotViews)
            {
                if (slot != null)
                {
                    // 임시 강화는 골드 잔액과 무관하게 플레이 모드에서만 허용하며, 실제 데이터의 기존 판정은 유지합니다.
                    slot.SetUpgradeAvailability(_contextUpgradePreview ? _contextUpgradeGold : _sortingPreview ? SortingPreviewGold : preview ? _previewGold : _gold, _contextUpgradePreview || _sortingPreview || !preview && _goldKnown, preview ? Application.isPlaying || _contextUpgradePreview || _sortingPreview : _hadUpgradeHandler);
                }
            }
        }

        if (_boardScreen != null)
        {
            _boardScreen.RefreshPresentation();
        }
    }

    /// <summary>
    /// 기존 장비 강화 창 표시 함수를 유지하며 이미 열린 창도 다시 갱신할 수 있습니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetUpgradeEquip(bool isOn)
    {
        // 버튼·ESC·외부 닫기를 같은 퇴장 경로로 연결하고 원래 처리 순서는 보존합니다.
        var common = GetComponent<CommonWindowEntranceView>();
        if (!isOn && common != null && common.TryPlayExit(() => SetUpgradeEquip(false))) return;

        if (!isOn)
        {
            EndContextUpgradePreview();
            if (_sortingPreview)
            {
                EndSortingTestPreview();
            }
        }

        bool wasActive = gameObject.activeSelf; // 이전 활성 여부입니다.
        gameObject.SetActive(isOn);
        if (isOn) common?.PlayEntrance();
        if (isOn && wasActive)
        {
            GetEquipmentData();
            if (!_sortingPreview && !_contextUpgradePreview)
            {
                OnOpened?.Invoke();
            }

            RefreshAvailability();
        }

        if (isOn)
        {
            transform.SetAsLastSibling(); // 열린 강화 창이 보유 골드 팝업 등 다른 공통 UI에 가리지 않도록 합니다.
        }
    }

    /// <summary>
    /// 외부 장비 개수를 받아 이전 장비 캐시를 지우고 최신 표시 수신을 준비합니다.
    /// </summary>
    /// <param name="count">개수 값입니다.</param>
    public void SetEquipmentCount(int count)
    {
        Initialize();
        _publishedCount = Mathf.Clamp(count, 0, EquipmentCount);
        // i: 반복 값입니다.
        for (int i = 0; i < EquipmentCount; i++)
        {
            _displayData[i] = UpgradeEquipSlotView.EmptyDisplay(i);
        }

        SetItemSlots();
    }

    /// <summary>
    /// 외부 장비 인덱스가 지원하는 표시 범위인지 확인합니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    bool ValidType(int type)
    {
        return type >= 0 && type < EquipmentCount && (_publishedCount == 0 || type < _publishedCount);
    }

    /// <summary>
    /// 장비의 이름·설명·레벨·최대 단계 정보를 종류별로 저장합니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="equipName">장비 이름 값입니다.</param>
    /// <param name="description">설명 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    /// <param name="maxLevel">최대 레벨 값입니다.</param>
    /// <param name="isMaxLevel">최대 레벨 여부입니다.</param>
    public void SetEquipment(int equipType, string equipName, string description, int level, int maxLevel, bool isMaxLevel)
    {
        Initialize();
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!ValidType(equipType))
        {
            return;
        }

        ref var data = ref _displayData[equipType]; // 데이터 값입니다.
        data.Name = equipName;
        data.Description = description;
        data.Level = level;
        data.MaxLevel = Mathf.Max(1, maxLevel);
        data.IsMaxLevel = isMaxLevel || level >= data.MaxLevel;
        data.HasInfo = true;
        RefreshPublishedSlot(equipType);
    }

    /// <summary>
    /// 컨트롤러가 검증한 다음 단계 강화 비용과 비용 유효 여부를 받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="needGold">필요 여부 골드 값입니다.</param>
    /// <param name="hasCost">비용 여부입니다.</param>
    public void UpdateUpgradeGold(int equipType, float needGold, bool hasCost)
    {
        Initialize();
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!ValidType(equipType))
        {
            return;
        }

        _displayData[equipType].HasCost = hasCost && UpgradeEquipSlotView.IsValidValue(needGold);
        _displayData[equipType].Cost = _displayData[equipType].HasCost ? needGold : 0f;
        RefreshPublishedSlot(equipType);
    }

    /// <summary>
    /// 종류별 능력치 한 쌍을 표시 캐시에 기록하고 보이는 슬롯만 갱신합니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    /// <param name="stat">능력치 값입니다.</param>
    /// <param name="current">게이지에 표시할 현재 수치입니다.</param>
    /// <param name="next">다음 값입니다.</param>
    void SetStat(int type, int stat, string current, string next)
    {
        Initialize();
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!ValidType(type))
        {
            return;
        }

        _displayData[type].Current[stat] = current;
        _displayData[type].Next[stat] = next;
        RefreshPublishedSlot(type);
    }

    /// <summary>
    /// 현재·다음 공격력을 전달받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="currentValue">현재 값 값입니다.</param>
    /// <param name="nextValue">다음 값 값입니다.</param>
    public void UpdateDamage(int equipType, float currentValue, float nextValue)
    {
        SetStat(equipType, 0, FormatValue(currentValue), FormatValue(nextValue));
    }

    /// <summary>
    /// 현재·다음 사거리를 전달받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="currentValue">현재 값 값입니다.</param>
    /// <param name="nextValue">다음 값 값입니다.</param>
    public void UpdateRange(int equipType, float currentValue, float nextValue)
    {
        SetStat(equipType, 1, FormatValue(currentValue), FormatValue(nextValue));
    }

    /// <summary>
    /// 현재·다음 최대 산소량을 전달받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="currentValue">현재 값 값입니다.</param>
    /// <param name="nextValue">다음 값 값입니다.</param>
    public void UpdateMaxOxygen(int equipType, float currentValue, float nextValue)
    {
        SetStat(equipType, 2, FormatValue(currentValue), FormatValue(nextValue));
    }

    /// <summary>
    /// 현재·다음 이동 속도를 전달받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="currentValue">현재 값 값입니다.</param>
    /// <param name="nextValue">다음 값 값입니다.</param>
    public void UpdateMoveSpeed(int equipType, float currentValue, float nextValue)
    {
        SetStat(equipType, 3, FormatValue(currentValue), FormatValue(nextValue));
    }

    /// <summary>
    /// 현재·다음 방어력을 전달받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="currentValue">현재 값 값입니다.</param>
    /// <param name="nextValue">다음 값 값입니다.</param>
    public void UpdateDefense(int equipType, float currentValue, float nextValue)
    {
        SetStat(equipType, 4, FormatValue(currentValue), FormatValue(nextValue));
    }

    /// <summary>
    /// 현재·다음 최대 적재량을 전달받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="currentValue">현재 값 값입니다.</param>
    /// <param name="nextValue">다음 값 값입니다.</param>
    public void UpdateCarryCapacity(int equipType, float currentValue, float nextValue)
    {
        SetStat(equipType, 5, FormatValue(currentValue), FormatValue(nextValue));
    }

    /// <summary>
    /// 현재·다음 탄 수를 전달받습니다.
    /// </summary>
    /// <param name="equipType">장비 종류 값입니다.</param>
    /// <param name="currentValue">현재 값 값입니다.</param>
    /// <param name="nextValue">다음 값 값입니다.</param>
    public void UpdateBulletCount(int equipType, int currentValue, int nextValue)
    {
        SetStat(equipType, 6, currentValue >= 0 ? currentValue.ToString() : null, nextValue >= 0 ? nextValue.ToString() : null);
    }

    /// <summary>
    /// 보유 골드를 받아 비용 부족 표시와 버튼 활성 상태를 갱신합니다.
    /// </summary>
    /// <param name="gold">골드 값입니다.</param>
    public void SetGold(float gold)
    {
        Initialize();
        _gold = UpgradeEquipSlotView.IsValidValue(gold) ? gold : 0f;
        _goldKnown = true;
        RefreshAvailability();
        UpdateWindowLabels();
    }

    /// <summary>
    /// 기존 안내 텍스트의 내용을 변경합니다.
    /// </summary>
    /// <param name="msg">메시지 값입니다.</param>
    void UpdateGuideText(string msg)
    {
        if (_guideText != null)
        {
            _guideText.text = msg;
        }
    }

    /// <summary>
    /// 기존 안내 텍스트를 표시하거나 숨깁니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetGuideText(bool isOn)
    {
        if (_guideText != null)
        {
            _guideText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 비어 있는 슬롯의 전용 안내 레이어를 켜거나 끕니다. 실제 장비 표시와 연결은 유지합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="empty">비어 있음 여부입니다.</param>
    void SetEmptySlot(int index, bool empty)
    {
        if (_emptySlotPlaceholders != null && index < _emptySlotPlaceholders.Length && _emptySlotPlaceholders[index] != null)
        {
            _emptySlotPlaceholders[index].SetActive(empty && _showEmptySlots);
        }
    }

    /// <summary>
    /// 실제 아이콘이 없을 때만 임시 도형에 장비 종류를 표시하고 실제 아이콘은 원래 색으로 표시합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="type">종류 값입니다.</param>
    void UpdateSlotIllustration(int index, int type)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_slotIconLabels == null || index >= _slotIconLabels.Length || _slotIconLabels[index] == null)
        {
            return;
        }

        var label = _slotIconLabels[index]; // 문구 값입니다.
        bool placeholder = _equipmentIcons == null || type >= _equipmentIcons.Length || _equipmentIcons[type] == null; // 임시 표시 여부입니다.
        label.gameObject.SetActive(placeholder);
        label.text = PreviewIconLabels[type];
        var image = label.transform.parent.GetComponent<UnityEngine.UI.Image>(); // 이미지 값입니다.
        if (image != null)
        {
            image.color = placeholder ? PreviewIconColors[type] : Color.white;
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 장비 전체 표시 상태로 슬롯 구성을 갱신합니다.
    /// </summary>
    public void ShowAllEquipment()
    {
        _showAllEquipment = true;
        GetEquipmentData();
    }

    /// <summary>
    /// 선택한 탭으로 변경한 뒤 장비 목록을 다시 배치합니다.
    /// </summary>
    /// <param name="tab">탭 값입니다.</param>
    void SelectTab(EquipTabType tab)
    {
        if (_boardScreen != null)
        {
            _showAllEquipment = false;
        }

        _currentEquipTabType = tab;
        GetEquipmentData();
    }

    /// <summary>
    /// 실제 강화 결과를 표시하고 컨트롤러의 최신 장비·레벨을 다시 읽습니다.
    /// </summary>
    /// <param name="succeeded">성공 여부입니다.</param>
    public void ShowUpgradeResult(bool succeeded)
    {
        GetEquipmentData();
        if (!succeeded && _failurePopup != null)
        {
            bool insufficientGold = false;
            bool knownGold = _goldKnown || UsesPreviewData || _contextUpgradePreview || _sortingPreview;
            int selectedType = _boardScreen != null ? _boardScreen.SelectedEquipmentType : -1;
            if (knownGold && _slotViews != null)
                foreach (var slot in _slotViews)
                    if (slot != null && slot.EquipmentType == selectedType)
                        insufficientGold = slot.PresentationData.HasCost && DisplayGold < slot.PresentationData.Cost;
            SetGuideText(false);
            _failurePopup.Show(insufficientGold);
        }
        else
        {
            if (_failurePopup != null) _failurePopup.Close();
            ShowGuide(succeeded ? "장비를 강화했습니다." : "강화할 수 없습니다. 보유 골드와 장비 상태를 확인해주세요.");
        }
        if (_boardScreen != null)
        {
            _boardScreen.PlayUpgradeFeedback(succeeded);
        }
    }

    /// <summary>
    /// 기존 닫기 버튼에서 강화 창을 숨깁니다.
    /// </summary>
    void Close()
    {
        SetUpgradeEquip(false);
    }

    /// <summary>
    /// 기존 안내 UI에 메시지를 쓰고 표시합니다.
    /// </summary>
    /// <param name="message">메시지 값입니다.</param>
    void ShowGuide(string message)
    {
        UpdateGuideText(message);
        SetGuideText(true);
    }

    /// <summary>
    /// 기존 세 탭의 선택 상태와 보유 골드·미리보기 안내를 갱신합니다.
    /// </summary>
    void UpdateWindowLabels()
    {
        bool preview = UsesPreviewData; // 미리보기 여부입니다.
        if (_walletText != null)
        {
            _walletText.text = _contextUpgradePreview ? $"보유 골드  {_contextUpgradeGold:N0} G" : _sortingPreview ? $"보유 골드  {SortingPreviewGold:N0} G" : preview ? $"보유 골드  {_previewGold:N0} G" : _goldKnown ? $"보유 골드  {_gold:N0} G" : "보유 골드  —";
        }

        if (_previewNoticeText != null)
        {
            _previewNoticeText.gameObject.SetActive(false);
            _previewNoticeText.text = _contextUpgradePreview ? "임시 강화 테스트 · 실제 장비와 재화는 변경하지 않습니다." : _sortingPreview ? "정렬 테스트 · 보유 골드 500 · 산소통 비용 900 · 최대 강화 2종 · 실제 데이터 변경 없음" : preview ? "임시 장비 정보 · 미리보기" : "현재 능력치와 다음 단계 능력치를 비교하세요.";
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_boardScreen != null)
        {
            _boardScreen.RefreshPresentation();
            return;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_equipTypeButtons == null)
        {
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _equipTypeButtons.Length; i++)
        {
            var button = _equipTypeButtons[i]; // 버튼 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (button == null)
            {
                continue;
            }

            bool selected = i == (int)_currentEquipTabType; // 선택 여부입니다.
            var image = button.targetGraphic; // 이미지 값입니다.
            if (image != null)
            {
                image.color = selected ? new Color(.09f, .28f, .29f) : new Color(.86f, .92f, .91f);
            }

            var text = button.GetComponentInChildren<TMP_Text>(true); // 텍스트 값입니다.
            if (text != null)
            {
                text.color = selected ? new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f) : new Color(.13f, .24f, .25f);
            }
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 비활성 슬롯을 포함해 UI 이벤트와 장비별 빈 표시 상태를 준비합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        // i: 반복 값입니다.
        for (int i = 0; i < EquipmentCount; i++)
        {
            _displayData[i] = UpgradeEquipSlotView.EmptyDisplay(i);
        }

        _wiredTabButtons = _equipTypeButtons != null ? (UnityEngine.UI.Button[])_equipTypeButtons.Clone() : Array.Empty<UnityEngine.UI.Button>();
        _tabCallbacks = new UnityEngine.Events.UnityAction[_wiredTabButtons.Length];
        // i: 반복 값입니다.
        for (int i = 0; i < Mathf.Min(3, _wiredTabButtons.Length); i++)
        {
            int tab = i; // 탭 값입니다.
            _tabCallbacks[i] = () => SelectTab((EquipTabType)tab);
            if (_wiredTabButtons[i] != null)
            {
                _wiredTabButtons[i].onClick.AddListener(_tabCallbacks[i]);
            }
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        _wiredSlots = _slotViews != null ? (UpgradeEquipSlotView[])_slotViews.Clone() : Array.Empty<UpgradeEquipSlotView>();
        _slotCallbacks = new Action[_wiredSlots.Length];
        // i: 반복 값입니다.
        for (int i = 0; i < _wiredSlots.Length; i++)
        {
            var slot = _wiredSlots[i]; // 슬롯 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null)
            {
                continue;
            }

            _slotCallbacks[i] = () => RequestUpgrade(slot);
            slot.OnUpgradeItem += _slotCallbacks[i];
        }

        SetGuideText(false);
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 컨트롤러의 같은 인덱스 장비·레벨을 검증해 캐시에 저장하고 현재 탭 슬롯에 전달합니다.
    /// </summary>
    public void GetEquipmentData()
    {
        Initialize();
        _readController = _equipController;
        IReadOnlyList<EquipmentData> datas = _equipController != null ? _equipController.EquipmentDatas : Array.Empty<EquipmentData>(); // 데이터 목록입니다.
        IReadOnlyList<int> levels = _equipController != null ? _equipController.EquipmentLevels : Array.Empty<int>(); // 레벨 목록입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < EquipmentCount; i++)
        {
            _equipmentDatas[i] = i < datas.Count && datas[i] != null && (int)datas[i].Type == i ? datas[i] : null;
            _equipmentLevels[i] = i < levels.Count ? levels[i] : 0;
        }

        SetItemSlots();
    }

    /// <summary>
    /// 유효한 수치만 표시용 문자열로 변환합니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    static string FormatValue(float value)
    {
        return UpgradeEquipSlotView.IsValidValue(value) ? value.ToString("0.##") : null;
    }

    #endregion

    #region 선택 및 외부 요청

    /// <summary>
    /// 실제 장비는 기존 시스템으로 요청하고, 임시 데이터는 View 내부 테스트 레벨만 변경합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void RequestUpgrade(UpgradeEquipSlotView slot)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (UsesPreviewData)
        {
            UpgradePreviewEquipment(slot);
            return;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (OnUpgradeRequested == null)
        {
            ShowGuide("강화 요청을 처리할 시스템이 연결되지 않았습니다.");
            return;
        }

        OnUpgradeRequested.Invoke(slot.EquipmentType);
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// 최대 레벨과 골드 부족 임시 상태로 카드 정렬을 확인합니다.
    /// </summary>
    [ContextMenu("테스트/정렬 테스트 표시 (최대 강화·골드 부족)")]
    public void ShowSortingTestPreview()
    {
#if UNITY_EDITOR
        // 프리팹 원본이나 Prefab Mode에는 테스트 표시값을 저장하지 않습니다.
        if (UnityEditor.EditorUtility.IsPersistent(this) || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            return;
        }

#endif
        EndContextUpgradePreview();
        _sortingPreview = true;
        // 나이프·작살·산소통·잠수복·가방·총. 최대 레벨 5와 기존 분류를 유지합니다.
        _sortingPreviewLevels = new[]
        { 4, 3, 4, 5, 5, 4 };
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowAllEquipment();
        ShowGuide("정렬 테스트: 강화 가능 → 최대 단계에 가까운 순 → 이름순 → 최대 강화. 실제 장비·골드는 변경되지 않습니다.");
    }

    /// <summary>
    /// 정렬 테스트 상태를 종료하고 보관한 원래 표시를 복원합니다.
    /// </summary>
    [ContextMenu("테스트/정렬 테스트 종료 (원래 데이터 복원)")]
    public void EndSortingTestPreview()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_sortingPreview)
        {
            return;
        }

        _sortingPreview = false;
        _sortingPreviewLevels = null;
        GetEquipmentData();
    }

    /// <summary>
    /// 기존 슬롯 표시 구조에 맞춘 임시 이름·레벨·비용·현재/다음 능력치를 만듭니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    UpgradeEquipSlotView.DisplayData CreatePreviewDisplay(int type)
    {
        if (!_sortingPreview && _previewEquipmentDatas != null && type >= 0 && type < _previewEquipmentDatas.Length && _previewEquipmentDatas[type] != null)
            return UpgradeEquipSlotView.ReadEquipment(_previewEquipmentDatas[type], GetPreviewLevel(type));
        var data = UpgradeEquipSlotView.EmptyDisplay(type); // 데이터 값입니다.
        data.HasInfo = true;
        data.Name = PreviewNames[type];
        data.Description = PreviewDescriptions[type];
        data.Level = GetPreviewLevel(type);
        data.IsMaxLevel = data.Level >= data.MaxLevel;
        data.HasCost = !data.IsMaxLevel;
        data.Cost = _sortingPreview ? (data.IsMaxLevel ? 0 : type == 2 ? 900 : 100) : Mathf.Round(PreviewBaseCosts[type] * Mathf.Pow(1.5f, data.Level - InitialPreviewLevel(type))); // 테스트 레벨이 오를 때 다음 강화 비용도 증가합니다.
        int level = data.Level; // 레벨 값입니다.
        switch ((EquipmentData.EquipType)type)
        {
            case EquipmentData.EquipType.Knife:
                PreviewStat(ref data, 0, 10 + level * 2, 12 + level * 2);
                PreviewStat(ref data, 1, 1.4f + level * .1f, 1.5f + level * .1f);
                break;
            case EquipmentData.EquipType.Gig:
                PreviewStat(ref data, 0, 12 + level * 3, 15 + level * 3);
                PreviewStat(ref data, 1, 4 + level * .5f, 4.5f + level * .5f);
                break;
            case EquipmentData.EquipType.OxygenCylinder:
                PreviewStat(ref data, 2, 60 + level * 30, 90 + level * 30);
                break;
            case EquipmentData.EquipType.SwimSuit:
                PreviewStat(ref data, 3, 3.9f + level * .3f, 4.2f + level * .3f);
                PreviewStat(ref data, 4, 4 + level * 4, 8 + level * 4);
                break;
            case EquipmentData.EquipType.FishBag:
                PreviewStat(ref data, 5, 10 + level * 10, 20 + level * 10);
                break;
            case EquipmentData.EquipType.Gun:
                PreviewStat(ref data, 0, 16 + level * 4, 20 + level * 4);
                PreviewStat(ref data, 6, 4 + level * 2, 6 + level * 2);
                break;
        }

        return data;
    }

    /// <summary>
    /// 임시 능력치 한 쌍을 실제 데이터 캐시와 분리된 표시 배열에 기록합니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="current">게이지에 표시할 현재 수치입니다.</param>
    /// <param name="next">다음 값입니다.</param>
    static void PreviewStat(ref UpgradeEquipSlotView.DisplayData data, int index, float current, float next)
    {
        data.Current[index] = current.ToString("0.##");
        data.Next[index] = data.IsMaxLevel ? null : next.ToString("0.##");
    }

    /// <summary>
    /// 인스펙터에 저장된 임시 초기 레벨을 변경하지 않고 읽습니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    int InitialPreviewLevel(int type)
    {
        return _previewLevels != null && type < _previewLevels.Length ? Mathf.Clamp(_previewLevels[type], 1, 5) : 1;
    }

    /// <summary>
    /// 편집 모드는 초기 레벨, 플레이 모드는 별도로 보관한 테스트 레벨을 반환합니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    int GetPreviewLevel(int type)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_contextUpgradePreview)
        {
            return _contextUpgradeLevels[type];
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_sortingPreview)
        {
            return _sortingPreviewLevels[type];
        }

        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            return InitialPreviewLevel(type);
        }

        if (_previewRuntimeLevels == null)
        {
            _previewRuntimeLevels = new int[EquipmentCount];
            // i: 반복 값입니다.
            for (int i = 0; i < EquipmentCount; i++)
            {
                _previewRuntimeLevels[i] = InitialPreviewLevel(i);
            }
        }

        return _previewRuntimeLevels[type];
    }

    /// <summary>
    /// 실제 강화 이벤트를 보내지 않고 선택 장비의 테스트 레벨만 올린 뒤 능력치·비용을 다시 표시합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void UpgradePreviewEquipment(UpgradeEquipSlotView slot)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if ((!Application.isPlaying && !_contextUpgradePreview && !_sortingPreview) || !UsesPreviewData || slot == null || _slotViews == null || Array.IndexOf(_slotViews, slot) < 0 || !slot.gameObject.activeInHierarchy)
        {
            return;
        }

        int type = slot.EquipmentType; // 종류 값입니다.
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (type < 0 || type >= EquipmentCount)
        {
            return;
        }

        int level = GetPreviewLevel(type); // 레벨 값입니다.
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (level >= 5)
        {
            return;
        }

        if (_contextUpgradePreview)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (!slot.CanRequestUpgrade || _contextUpgradeGold < slot.PresentationData.Cost)
            {
                return;
            }

            _contextUpgradeGold -= slot.PresentationData.Cost;
            _contextUpgradeLevels[type] = level + 1;
        }
        else if (_sortingPreview)
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (!slot.CanRequestUpgrade)
            {
                return;
            }

            _sortingPreviewLevels[type] = level + 1;
        }
        else
        {
            _previewRuntimeLevels[type] = level + 1;
        }

        if (_boardScreen != null)
        {
            _boardScreen.PlayUpgradeFeedback(true);
        }

        SetItemSlots();
        ShowGuide($"임시 강화: {PreviewNames[type]} Lv.{level} → Lv.{level + 1} · 실제 장비와 골드는 변경되지 않습니다.");
    }

    /// <summary>
    /// 컴포넌트 ContextMenu에서 실제 데이터와 분리한 임시 강화 창을 엽니다.
    /// </summary>
    [ContextMenu("테스트/장비 강화 창 열기")]
    public void OpenUpgradeWindowFromContextMenu()
    {
#if UNITY_EDITOR
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (UnityEditor.EditorUtility.IsPersistent(this) || UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            return;
        }

#endif
        EndSortingTestPreview();
        _contextUpgradePreview = true;
        _contextUpgradeGold = Application.isPlaying && _goldKnown ? _gold : _previewGold;
        _contextUpgradeLevels = new int[EquipmentCount];
        // i: 반복 값입니다.
        for (int i = 0; i < EquipmentCount; i++)
        {
            _contextUpgradeLevels[i] = Application.isPlaying && _displayData[i].HasInfo ? _displayData[i].Level : InitialPreviewLevel(i);
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ShowAllEquipment();
        ShowGuide(string.Empty);
    }

    /// <summary>
    /// 편집 모드에서도 선택 장비의 임시 강화를 실행할 수 있습니다.
    /// </summary>
    [ContextMenu("테스트/선택 장비 강화")]
    public void TestUpgradeSelectedEquipment()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_contextUpgradePreview && !_sortingPreview)
        {
            return;
        }

        int selected = _boardScreen != null ? _boardScreen.SelectedEquipmentType : -1; // 선택 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_slotViews == null)
        {
            return;
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in _slotViews)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot != null && slot.gameObject.activeInHierarchy && slot.EquipmentType == selected)
            {
                UpgradePreviewEquipment(slot);
                return;
            }
        }
    }

    /// <summary>
    /// ContextMenu 강화 테스트의 임시 데이터를 정리합니다.
    /// </summary>
    void EndContextUpgradePreview()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_contextUpgradePreview)
        {
            return;
        }

        _contextUpgradePreview = false;
        _contextUpgradeLevels = null;
        GetEquipmentData();
    }

    /// <summary>
    /// 컴포넌트 ContextMenu에서 강화 창을 닫습니다. 테스트 레벨은 같은 플레이 세션 동안 유지합니다.
    /// </summary>
    [ContextMenu("테스트/장비 강화 창 닫기")]
    public void CloseUpgradeWindowFromContextMenu()
    {
        SetUpgradeEquip(false);
    }

    /// <summary>
    /// 테스트 레벨만 인스펙터의 초기값으로 되돌립니다. 실제 장비·골드·초기 설정값은 변경하지 않습니다.
    /// </summary>
    [ContextMenu("미리보기/임시 강화 초기화")]
    public void ResetPreviewUpgrades()
    {
        _previewRuntimeLevels = null;
        PreviewTemporaryData();
        if (Application.isPlaying && UsesPreviewData)
        {
            ShowGuide("임시 강화 상태를 초기값으로 되돌렸습니다.");
        }
    }

    /// <summary>
    /// 편집 모드와 플레이 모드에서 동일한 탭·슬롯 표시 규칙으로 임시 화면을 갱신합니다.
    /// </summary>
    [ContextMenu("미리보기/임시 장비 데이터 표시")]
    public void PreviewTemporaryData()
    {
        if (Application.isPlaying)
        {
            GetEquipmentData();
        }
        else if (UsesPreviewData)
        {
            SetItemSlots();
        }
    }

    #endregion

}
