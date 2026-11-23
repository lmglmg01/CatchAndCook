using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 한 장비의 현재·다음 능력치와 비용을 표시하고 강화 요청을 전달합니다.
/// </summary>
public class UpgradeEquipSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] UnityEngine.UI.Image _itemIcon; // 기존 슬롯의 장비 아이콘 Image를 연결합니다.
    [SerializeField] TMP_Text _itemLevelText; // 기존 슬롯의 레벨 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _itemNameText; // 기존 슬롯의 장비 이름 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _itemContentText; // 기존 슬롯의 장비 설명 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _currentStatsText; // 현재 능력치 영역 TMP_Text를 연결합니다.
    [SerializeField] TMP_Text _nextStatsText; // 다음 강화 능력치 영역 TMP_Text를 연결합니다.
    [SerializeField] UnityEngine.UI.Button _upgradeButton; // 기존 강화 Button을 연결합니다. 클릭 이벤트는 코드에서 연결합니다.
    [SerializeField] TMP_Text _payGoldText; // 강화 비용과 최대 강화 안내를 표시할 TMP_Text를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _goldIconImage; // 강화 비용 옆 골드 Image를 연결합니다.

    #endregion

    #region 런타임 상태

    DisplayData _display; // 현재 슬롯에 배정된 장비의 표시 상태입니다.
    EquipmentData _equipmentData; // 매개변수 없는 기존 갱신 함수를 위해 기억한 실제 장비 데이터입니다.
    Sprite _defaultIcon; // 아이콘이 미지정이면 사용하는 기존 Image의 원래 Sprite입니다.
    Color _defaultCostColor; // 골드 부족 표시를 해제할 때 복원할 텍스트 색상입니다.
    float _gold; // 외부 시스템에서 전달한 보유 골드이며 이 View에서는 차감하지 않습니다.
    bool _goldKnown, _hasUpgradeHandler, _initialized; // 골드 수신·강화 처리 연결·버튼 초기화 여부입니다.
    static readonly string[] StatLabels =
    { "공격력", "사거리", "최대 산소량", "이동 속도", "방어력", "최대 적재량", "탄 수" }; // 능력치 표시 순서입니다.

    #endregion

    #region 이벤트 액션

    /// <summary>
    /// 기존 공개 이벤트를 유지하며 강화 버튼을 누르면 요청을 한 번만 전달합니다.
    /// </summary>
    public event Action OnUpgradeItem;

    #endregion

    #region 프로퍼티

    // 읽기 전용 표시 정보와 기존 검증 경로를 상세 View에 제공합니다.
    public DisplayData PresentationData => _display;

    /// <summary>
    /// 표시 데이터 아이콘 이미지입니다.
    /// </summary>
    public Sprite PresentationIcon => _itemIcon != null ? _itemIcon.sprite : null;

    /// <summary>
    /// 가능 요청 강화 여부입니다.
    /// </summary>
    /// <summary>재화 부족 상태에서도 확인창을 열 수 있도록 장비와 처리 연결만 검사합니다.</summary>
    public bool CanOpenUpgradeConfirmation => _hasUpgradeHandler && _display.HasInfo && !_display.IsMaxLevel && _display.Level >= 1 && _display.Level < _display.MaxLevel && _display.HasCost && HasStats(_display.Current) && HasStats(_display.Next);

    public bool CanRequestUpgrade => _upgradeButton != null && _upgradeButton.interactable;

    /// <summary>
    /// 가능 강화 그리기 순서 여부입니다.
    /// </summary>
    public bool CanUpgradeForSorting => _display.HasInfo && !_display.IsMaxLevel && _display.Level >= 1 && _display.Level < _display.MaxLevel && _display.HasCost && HasStats(_display.Current) && HasStats(_display.Next) && (!_goldKnown || _gold >= _display.Cost);
    public int EquipmentType => _display.Type; // 부모 View가 강화 요청 대상을 식별할 장비 종류입니다.

    #endregion

    #region 표시 데이터 형식

    /// <summary>
    /// 두 View 사이에서만 사용하는 표시용 값 묶음이며 실제 장비나 재화를 변경하지 않습니다.
    /// </summary>
    public struct DisplayData
    {

        #region 멤버

        #region 런타임 상태

        public int Type; // EquipmentData.EquipType과 같은 장비 인덱스입니다.
        public string Name, Description; // 컨트롤러 또는 장비 데이터에서 받은 이름과 설명입니다.
        public int Level, MaxLevel; // 현재 레벨과 컨트롤러 기준 최대 레벨입니다.
        public bool HasInfo, IsMaxLevel, HasCost; // 장비 정보·최대 레벨·유효 비용 여부입니다.
        public float Cost; // 다음 단계로 강화할 때 필요한 비용입니다.
        public string[] Current, Next; // 공격력·사거리·산소·이동속도·방어력·적재량·탄수 순서의 표시값입니다.

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 기존 버튼과 아이콘·비용 색상을 초기화합니다.
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 슬롯 제거 시 코드에서 연결한 강화 버튼 이벤트를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.RemoveListener(UpgradeItem);
        }
    }

    #endregion

    #region 선택 및 외부 요청

    /// <summary>
    /// 외부 버튼에서 현재 슬롯의 강화 요청 함수를 실행합니다.
    /// </summary>
    public void RequestSelectedUpgrade()
    {
        UpgradeItem();
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 활성화 전 데이터가 전달되어도 버튼 이벤트를 중복 없이 한 번만 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_itemIcon != null)
        {
            _defaultIcon = _itemIcon.sprite;
        }

        if (_payGoldText != null)
        {
            _defaultCostColor = _payGoldText.color;
        }

        if (_upgradeButton != null)
        {
            _upgradeButton.onClick.AddListener(UpgradeItem);
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 장비 슬롯을 표시하거나 숨깁니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetEquipSlot(bool isOn)
    {
        gameObject.SetActive(isOn);
    }

    /// <summary>
    /// 장비 종류별 Sprite를 적용하고 미연결 시 기존 슬롯의 원래 Sprite를 사용합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    public void SetItemIcon(Sprite icon)
    {
        Initialize();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_itemIcon == null)
        {
            return;
        }

        _itemIcon.sprite = icon != null ? icon : _defaultIcon;
        _itemIcon.enabled = _itemIcon.sprite != null;
    }

    /// <summary>
    /// 외부 보유 골드와 강화 처리 연결 상태로 버튼 활성 여부를 갱신합니다.
    /// </summary>
    /// <param name="gold">골드 값입니다.</param>
    /// <param name="goldKnown">골드 확인된 대상 여부입니다.</param>
    /// <param name="hasUpgradeHandler">강화 처리자 여부입니다.</param>
    public void SetUpgradeAvailability(float gold, bool goldKnown, bool hasUpgradeHandler)
    {
        _gold = gold;
        _goldKnown = goldKnown;
        _hasUpgradeHandler = hasUpgradeHandler;
        RenderAvailability();
    }

    /// <summary>
    /// 실제 장비 정보를 기존 UI에 표시하며 최대 레벨에서는 다음 능력치를 숨깁니다.
    /// </summary>
    void Render()
    {
        Initialize();
        if (_itemNameText != null)
        {
            _itemNameText.text = _display.HasInfo ? _display.Name : "장비 정보 없음";
        }

        if (_itemContentText != null)
        {
            _itemContentText.text = _display.Description ?? string.Empty;
        }

        if (_itemLevelText != null)
        {
            _itemLevelText.text = !_display.HasInfo || _display.Level < 1 ? "레벨 정보 없음" : $"Lv.{_display.Level}" + (_display.IsMaxLevel ? " (최대)" : string.Empty);
        }

        if (_currentStatsText != null)
        {
            _currentStatsText.text = _display.HasInfo ? FormatStats(_display.Current) : string.Empty;
        }

        if (_nextStatsText != null)
        {
            _nextStatsText.text = _display.HasInfo && !_display.IsMaxLevel ? FormatStats(_display.Next) : string.Empty;
        }

        RenderAvailability();
    }

    /// <summary>
    /// 최대 강화·데이터 누락·골드 부족·미연결 상태를 반영해 강화 버튼과 비용을 표시합니다.
    /// </summary>
    void RenderAvailability()
    {
        bool validLevel = _display.Level >= 1 && _display.Level <= _display.MaxLevel; // 유효 레벨 여부입니다.
        bool hasCost = _display.HasInfo && validLevel && !_display.IsMaxLevel && _display.HasCost; // 비용 여부입니다.
        bool enough = !_goldKnown || _gold >= _display.Cost; // 충분 여부 여부입니다.
        if (_upgradeButton != null)
        {
            _upgradeButton.interactable = hasCost && HasStats(_display.Current) && HasStats(_display.Next) && enough && _hasUpgradeHandler;
        }

        if (_goldIconImage != null)
        {
            _goldIconImage.enabled = hasCost;
        }

        if (_payGoldText != null)
        {
            _payGoldText.text = _display.IsMaxLevel ? "최대 강화" : hasCost ? _display.Cost.ToString("0.##") : "강화 정보 없음";
            _payGoldText.color = hasCost && !enough ? new Color(.8f, .2f, .2f) : _defaultCostColor;
        }
    }

    /// <summary>
    /// 기존 매개변수 없는 호출을 유지하며 마지막으로 전달받은 장비 표시를 갱신합니다.
    /// </summary>
    public void UpdateItemData()
    {
        if (_equipmentData != null)
        {
            UpdateItemData(_equipmentData, _display.Level);
        }
        else
        {
            Render();
        }
    }

    /// <summary>
    /// 부모 View에서 받은 장비 데이터와 같은 인덱스의 레벨로 슬롯을 갱신합니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    public void UpdateItemData(EquipmentData data, int level)
    {
        _equipmentData = data;
        _display = ReadEquipment(data, level);
        Render();
    }

    /// <summary>
    /// 기존 IEquipUpgradeView 이벤트 경로에서 받은 표시값을 슬롯에 적용합니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    public void SetDisplayData(DisplayData data)
    {
        _equipmentData = null;
        _display = data;
        Render();
    }

    /// <summary>
    /// 장비 종류와 능력치 표시 배열을 초기화한 빈 표시 상태를 만듭니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    public static DisplayData EmptyDisplay(int type)
    {
        return new DisplayData
        {
            Type = type,
            MaxLevel = 5,
            Current = new string[7],
            Next = new string[7]
        };
    }

    /// <summary>
    /// 장비 종류별 실제 배열에서 현재 레벨과 다음 레벨의 표시값을 안전하게 읽습니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    public static DisplayData ReadEquipment(EquipmentData data, int level)
    {
        var result = EmptyDisplay(data != null ? (int)data.Type : -1); // 결과 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (data == null)
        {
            return result;
        }

        result.HasInfo = true;
        result.Name = data.EquipName;
        result.Description = data.EquipDescription;
        result.Level = level;
        result.IsMaxLevel = level >= result.MaxLevel;
        int current = level - 1, next = level; // 현재 값입니다. / 다음 값입니다.
        if (level >= 1 && level <= result.MaxLevel)
        {
            if (data is KnifeEquipmentData knife)
            {
                ReadStat(ref result, 0, knife.AttackPowers, current, next);
                ReadStat(ref result, 1, knife.Ranges, current, next);
            }
            else if (data is GigEquipmentData gig)
            {
                ReadStat(ref result, 0, gig.AttackPowers, current, next);
                ReadStat(ref result, 1, gig.Ranges, current, next);
            }
            else if (data is OxygenCylinderEquipmentData oxygen)
            {
                ReadStat(ref result, 2, oxygen.OxygenAmounts, current, next);
            }
            else if (data is SwimSuitEquipmentData suit)
            {
                ReadStat(ref result, 3, suit.MoveSpeeds, current, next);
                ReadStat(ref result, 4, suit.Defenses, current, next);
            }
            else if (data is FishBagEquipmentData bag)
            {
                ReadStat(ref result, 5, bag.Capacities, current, next);
            }
            else if (data is GunEquipmentData gun)
            {
                ReadStat(ref result, 0, gun.AttackPowers, current, next);
                result.Current[6] = ReadInt(gun.BulletCounts, current);
                if (!result.IsMaxLevel)
                {
                    result.Next[6] = ReadInt(gun.BulletCounts, next);
                }
            }

            if (!result.IsMaxLevel && current < data.UpGradeGolds.Count)
            {
                float cost = data.UpGradeGolds[current]; // 비용 값입니다.
                result.HasCost = IsValidValue(cost);
                result.Cost = result.HasCost ? cost : 0f;
            }
        }

        return result;
    }

    /// <summary>
    /// 실수 능력치 배열의 범위를 확인해 현재와 다음 단계 문자열을 채웁니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    /// <param name="stat">능력치 값입니다.</param>
    /// <param name="values">값 목록입니다.</param>
    /// <param name="current">게이지에 표시할 현재 수치입니다.</param>
    /// <param name="next">다음 값입니다.</param>
    static void ReadStat(ref DisplayData data, int stat, IReadOnlyList<float> values, int current, int next)
    {
        data.Current[stat] = ReadFloat(values, current);
        if (!data.IsMaxLevel)
        {
            data.Next[stat] = ReadFloat(values, next);
        }
    }

    /// <summary>
    /// 유효한 실수 배열 요소만 읽고 누락된 값은 빈 값으로 남깁니다.
    /// </summary>
    /// <param name="values">값 목록입니다.</param>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    static string ReadFloat(IReadOnlyList<float> values, int index)
    {
        return values != null && index >= 0 && index < values.Count && IsValidValue(values[index]) ? values[index].ToString("0.##") : null;
    }

    /// <summary>
    /// 유효한 정수 배열 요소만 읽고 누락된 값은 빈 값으로 남깁니다.
    /// </summary>
    /// <param name="values">값 목록입니다.</param>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    static string ReadInt(IReadOnlyList<int> values, int index)
    {
        return values != null && index >= 0 && index < values.Count && values[index] >= 0 ? values[index].ToString() : null;
    }

    /// <summary>
    /// 장비 종류에 필요한 능력치 인덱스를 반환합니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    static int[] RequiredStats(int type)
    {
        switch ((EquipmentData.EquipType)type)
        {
            case EquipmentData.EquipType.Knife:
            case EquipmentData.EquipType.Gig:
                return new[]
                { 0, 1 };
            case EquipmentData.EquipType.OxygenCylinder:
                return new[]
                { 2 };
            case EquipmentData.EquipType.SwimSuit:
                return new[]
                { 3, 4 };
            case EquipmentData.EquipType.FishBag:
                return new[]
                { 5 };
            case EquipmentData.EquipType.Gun:
                return new[]
                { 0, 6 };
            default:
                return Array.Empty<int>();
        }
    }

    /// <summary>
    /// 실제 강화나 자기 이벤트 재호출 없이 유효한 클릭 요청을 한 번만 보냅니다.
    /// </summary>
    void UpgradeItem()
    {
        if (_upgradeButton != null && _upgradeButton.interactable)
        {
            OnUpgradeItem?.Invoke();
        }
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 음수·NaN·무한대를 표시 가능한 수치로 취급하지 않습니다.
    /// </summary>
    /// <param name="value">현재 표시 항목에 적용할 값입니다.</param>
    public static bool IsValidValue(float value)
    {
        return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// 장비의 필수 능력치가 모두 있는지 검사해 잘못된 다음 단계 강화를 방지합니다.
    /// </summary>
    /// <param name="values">값 목록입니다.</param>
    bool HasStats(string[] values)
    {
        var required = RequiredStats(_display.Type); // 필요 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (values == null || required.Length == 0)
        {
            return false;
        }

        // index: 인덱스 값입니다.
        foreach (int index in required)
        {
            // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
            if (index >= values.Length || string.IsNullOrEmpty(values[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 종류별 능력치 이름과 값으로 현재 또는 다음 능력치 텍스트를 만듭니다.
    /// </summary>
    /// <param name="values">값 목록입니다.</param>
    string FormatStats(string[] values)
    {
        var text = new StringBuilder(); // 텍스트 값입니다.
        // index: 인덱스 값입니다.
        foreach (int index in RequiredStats(_display.Type))
        {
            if (text.Length > 0)
            {
                text.AppendLine();
            }

            text.Append(StatLabels[index]).Append(": ").Append(values != null && index < values.Length && values[index] != null ? values[index] : "정보 없음");
        }

        return text.ToString();
    }

    #endregion

}
