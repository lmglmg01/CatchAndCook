using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 다이빙 결과와 회수할 아이템 선택을 표시합니다. 인벤토리에는 직접 접근하지 않습니다.
/// </summary>
public class DiveResultView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] GameObject _clearIcon; // 클리어 결과 표시입니다.
    [SerializeField] GameObject _failIcon; // 패배 결과 표시입니다.
    [SerializeField] TMP_Text _numberText; // 다이빙 회차입니다.
    [SerializeField] TMP_Text _dateText; // 결과 날짜입니다.
    [SerializeField] TMP_Text _fishingText; // 포획 수입니다.
    [SerializeField] TMP_Text _farmingText; // 획득 아이템 수입니다.
    // 미리 배치한 슬롯입니다. 비어 있으면 자식 슬롯을 찾아 사용하고, 부족한 슬롯은 복제 원본으로 추가합니다.
    [SerializeField] DiveResultSlotView[] _views;
    [SerializeField] TMP_Text _guideText; // 빈 목록 또는 선택 안내 문구입니다.
    [SerializeField] UnityEngine.UI.Button _acceptButton; // 선택을 확정하는 확인 버튼입니다.
    [SerializeField] GameObject _slotPrefab; // DiveResultSlotView가 있는 프리팹 또는 씬의 복제용 슬롯입니다.
    // 새 슬롯을 배치할 부모입니다. 비워 두면 기존 슬롯 또는 씬의 복제 원본 부모를 사용합니다.
    [SerializeField] Transform _slotContent;
    [Header("가로형 결과 화면")]
    [SerializeField] bool _useResultLayout;
    [SerializeField] DiveResultIntroView _intro;
    [SerializeField] Sprite _successNoticeSprite, _failedNoticeSprite;
    [SerializeField, Min(1)] int _minimumVisibleSlots = 20;
    [SerializeField] TMP_Text _resultTitle, _titleShadow, _stateHint;
    [SerializeField] UnityEngine.UI.Image _screenBackground, _itemPanel, _hintBackground;
    [SerializeField] UnityEngine.UI.Image[] _recordBackgrounds;
    [SerializeField] UnityEngine.UI.Image _scrollHandle, _scrollTrack;
    [SerializeField] UnityEngine.UI.ScrollRect _resultScroll;

    #endregion

    #region 런타임 상태

    readonly List<Inventory.BagItem> _items = new List<Inventory.BagItem>(); // 아이템 목록입니다.
    readonly List<DiveResultSlotView> _slots = new List<DiveResultSlotView>(); // 슬롯 목록입니다.
    DiveResultSlotView _selectedSlot; // 선택 슬롯 표시를 연결할 뷰입니다.
    bool _isClear = true; // 완료 여부입니다.
    bool _initialized; // 초기화 여부입니다.
    bool _confirmed; // 확인 완료 여부입니다.
    bool _allItemsVisible; // 전체 아이템 목록 노출 여부입니다.

    #endregion

    #region 이벤트 액션

    /// <summary>
    /// 확인을 누른 뒤에만 발생합니다. 클리어는 전체 목록, 패배는 선택한 한 슬롯의 아이템/수량,
    /// 획득 내역이 없으면 빈 목록을 전달합니다. 인벤토리 반영은 향후 외부에서 연결합니다.
    /// </summary>
    public event Action<IReadOnlyList<Inventory.BagItem>> OnItemsConfirmed;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 확인 버튼과 기존 결과 슬롯을 초기화합니다.
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 확인 버튼과 슬롯에 연결한 이벤트 처리를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_acceptButton != null)
        {
            _acceptButton.onClick.RemoveListener(Confirm);
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                slot.OnSelected -= SelectSlot;
            }
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 기존 슬롯과 배치 부모를 찾고 확인 및 선택 이벤트를 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_acceptButton != null)
        {
            _acceptButton.onClick.AddListener(Confirm);
        }

        if (_views != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _views)
            {
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (slot == null || slot.gameObject == _slotPrefab || _slots.Contains(slot))
                {
                    continue;
                }

                if (_slotContent == null)
                {
                    _slotContent = slot.transform.parent;
                }

                RegisterSlot(slot);
            }
        }

        // 기존 씬처럼 배열이 비어 있어도 Content 아래에 배치된 슬롯과 그 부모를 재사용합니다.
        foreach (var slot in GetComponentsInChildren<DiveResultSlotView>(true))
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (slot.gameObject == _slotPrefab || _slots.Contains(slot))
            {
                continue;
            }

            if (_slotContent == null)
            {
                _slotContent = slot.transform.parent;
            }

            RegisterSlot(slot);
        }

        // 씬에 배치한 템플릿은 원본을 숨기고, 복제본만 결과 목록에 사용합니다.
        if (_slotPrefab != null && _slotPrefab.scene.IsValid())
        {
            if (_slotContent == null)
            {
                _slotContent = _slotPrefab.transform.parent;
            }

            _slotPrefab.SetActive(false);
        }
    }

    /// <summary>
    /// 결과 슬롯을 등록하고 선택 이벤트를 연결합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void RegisterSlot(DiveResultSlotView slot)
    {
        _slots.Add(slot);
        slot.OnSelected += SelectSlot;
    }

    /// <summary>
    /// 빈 목록 또는 선택 안내와 확인 버튼의 사용 가능 여부를 갱신합니다.
    /// </summary>
    void UpdateGuideAndButton()
    {
        if (_guideText != null)
        {
            _guideText.gameObject.SetActive(true);
            _guideText.text = _items.Count == 0 ? "획득한 아이템이 없습니다. 확인을 눌러주세요." : !_allItemsVisible ? "아이템 슬롯 연결을 확인해 주세요." : _isClear ? "다음으로 넘어가려면 확인을 눌러주세요." : _selectedSlot == null ? "가져갈 아이템 한 가지를 선택해주세요." : "선택한 아이템을 가져가려면 확인을 눌러주세요.";
        }

        if (_acceptButton != null)
        {
            _acceptButton.interactable = (_intro == null || _intro.IsReady) && !_confirmed && _allItemsVisible && (_items.Count == 0 || _isClear || _selectedSlot != null);
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 성공 여부와 유효한 획득 목록을 복사하고 결과 표시를 초기화합니다.
    /// </summary>
    /// <param name="isClear">완료 여부입니다.</param>
    /// <param name="items">아이템 목록입니다.</param>
    public void SetResult(bool isClear, IReadOnlyList<Inventory.BagItem> items)
    {
        Initialize();
        _isClear = isClear;
        _items.Clear();
        if (items != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].ItemData != null && items[i].Count > 0)
                {
                    _items.Add(items[i]);
                }
            }
        }

        _selectedSlot = null;
        _confirmed = false;
        RefreshResult();
    }

    /// <summary>
    /// 결과 아이콘과 획득 개수에 맞는 슬롯, 안내와 버튼을 갱신합니다.
    /// </summary>
    /// <summary>획득 목록과 회수 규칙은 유지하고 성공·실패의 제목, 배경과 안내 색을 바꿉니다.</summary>
    void ApplyResultAppearance()
    {
        if (!_useResultLayout) return;
        Color accent = new Color32(33,174,183,255);
        Color brown = new Color32(116,76,65,255);
        Color cream = new Color32(253,247,228,255);
        if (_resultTitle != null) _resultTitle.color = brown;
        if (_titleShadow != null) _titleShadow.gameObject.SetActive(false);
        if (_numberText != null) _numberText.color = brown;
        if (_dateText != null) _dateText.color = new Color32(166,120,107,255);
        if (_screenBackground != null) _screenBackground.color = new Color(0,0,0,.7f);
        if (_itemPanel != null) _itemPanel.color = new Color32(0,58,82,255);
        if (_recordBackgrounds != null) foreach(var image in _recordBackgrounds) if(image!=null) image.color=Color.white;
        if (_hintBackground != null) { _hintBackground.sprite=_isClear?_successNoticeSprite:_failedNoticeSprite;_hintBackground.color=Color.white; }        if (_stateHint != null)
        {
            _stateHint.text = _isClear ? "아래의 아이템들을 모두 가져갑니다!" : "아래의 항목 중 한 가지 아이템만 가져갈 수 있습니다.";
            _stateHint.color = cream;
        }
        if (_guideText != null) _guideText.color = cream;
        if (_scrollHandle != null) _scrollHandle.color = new Color32(22,91,120,255);
        if (_scrollTrack != null) _scrollTrack.color = new Color32(0,40,58,255);
    }
    void RefreshResult()
    {
        if (_clearIcon != null)
        {
            _clearIcon.SetActive(_isClear);
        }

        if (_failIcon != null)
        {
            _failIcon.SetActive(!_isClear);
        }

        while (_slots.Count < Mathf.Max(_items.Count, _useResultLayout ? _minimumVisibleSlots : 0))
        {
            var prefabView = _slotPrefab != null ? _slotPrefab.GetComponent<DiveResultSlotView>() : null; // 프리팹 뷰 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (prefabView == null || _slotContent == null)
            {
                Debug.LogWarning("DiveResultView의 슬롯 복제 원본과 배치 부모를 연결해 주세요.", this);
                break;
            }

            RegisterSlot(Instantiate(prefabView, _slotContent, false));
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i]; // 슬롯 값입니다.
            if (i < _items.Count)
            {
                slot.Bind(_items[i].ItemData, _items[i].Count, _isClear);
                slot.SetSelected(slot == _selectedSlot);
                slot.gameObject.SetActive(true);
            }
            else
            {
                slot.Bind(null, 0, _isClear);
                slot.gameObject.SetActive(_useResultLayout);
            }
        }

        ApplyResultAppearance();
        if (_useResultLayout && _resultScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_slotContent);
            _resultScroll.verticalNormalizedPosition = 1f;
        }
        _allItemsVisible = _slots.Count >= _items.Count;
        UpdateGuideAndButton();
    }

    /// <summary>
    /// 회차, 날짜, 포획 수와 획득 아이템 수를 표시합니다.
    /// </summary>
    /// <param name="number">숫자 값입니다.</param>
    /// <param name="date">날짜 값입니다.</param>
    /// <param name="fishingAmount">낚시 수량 값입니다.</param>
    /// <param name="farmingAmount">파밍 수량 값입니다.</param>
    public void UpdateRecord(int number, string date, int fishingAmount, int farmingAmount)
    {
        if (_numberText != null)
        {
            _numberText.text = _useResultLayout ? $"잠수 {number}회차" : $"회차: {number}";
        }

        if (_dateText != null)
        {
            _dateText.text = _useResultLayout ? date : $"날짜: {date}";
        }

        if (_fishingText != null)
        {
            _fishingText.text = $"포획: {fishingAmount}";
        }

        if (_farmingText != null)
        {
            _farmingText.text = $"획득 아이템: {farmingAmount}";
        }
    }

    /// <summary>
    /// 패배 시 가장 최근 클릭한 슬롯 하나만 선택 상태로 유지합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void SelectSlot(DiveResultSlotView slot)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_isClear || _confirmed || !_slots.Contains(slot) || slot.Item == null || slot.Amount <= 0)
        {
            return;
        }

        if (_selectedSlot != null)
        {
            _selectedSlot.SetSelected(false);
        }

        _selectedSlot = slot;
        _selectedSlot.SetSelected(true);
        UpdateGuideAndButton();
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    // 기존 인스펙터 UnityEvent 연결을 유지하는 갱신 진입점입니다. 실제 결과는 SetResult로 전달합니다.

    /// <summary>
    /// 현재 저장된 결과 데이터로 결과창을 다시 표시합니다.
    /// </summary>
    public void GetResultData()
    {
        Initialize();
        RefreshResult();
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 다이빙 결과창을 표시하거나 숨깁니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetWindow(bool isOn)
    {
        gameObject.SetActive(isOn);
        if (isOn) { transform.SetAsLastSibling(); if (_intro != null) _intro.Play(_isClear, UpdateGuideAndButton); UpdateGuideAndButton(); }
    }

    #endregion

    #region 선택 및 외부 요청

    /// <summary>
    /// 결과를 한 번만 확정해 창을 닫고 회수 대상 목록을 이벤트로 전달합니다.
    /// </summary>
    public void Confirm()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if ((_intro != null && !_intro.IsReady) || _confirmed || !_allItemsVisible || (!_isClear && _items.Count > 0 && _selectedSlot == null))
        {
            return;
        }

        Inventory.BagItem[] confirmedItems; // 확인 완료 아이템 목록입니다.
        if (_isClear)
        {
            confirmedItems = _items.ToArray();
        }
        else if (_selectedSlot != null)
        {
            confirmedItems = new[]
            {
                new Inventory.BagItem(_selectedSlot.Item, _selectedSlot.Amount)
            };
        }
        else
        {
            confirmedItems = Array.Empty<Inventory.BagItem>();
        }

        _confirmed = true;
        UpdateGuideAndButton();
        SetWindow(false);
        OnItemsConfirmed?.Invoke(Array.AsReadOnly(confirmedItems));
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// 컴포넌트 ContextMenu에서 성공 결과창 테스트를 실행합니다.
    /// </summary>

#if UNITY_EDITOR
    [ContextMenu("테스트/성공 결과창")]
    public void TestClearResult()
    {
        ShowTestResult(true);
    }
#endif

    /// <summary>
    /// 컴포넌트 ContextMenu에서 실패 결과창 테스트를 실행합니다.
    /// </summary>

#if UNITY_EDITOR
    [ContextMenu("테스트/실패 결과창")]
    public void TestFailResult()
    {
        ShowTestResult(false);
    }
#endif

    /// <summary>
    /// 기존 아이템 에셋을 읽어 임시 획득 내역을 만들고 기존 결과 표시 함수로 창을 엽니다.
    /// </summary>
    /// <param name="isClear">완료 여부입니다.</param>

#if UNITY_EDITOR
    void ShowTestResult(bool isClear)
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            Debug.Log("결과창 테스트는 플레이 모드에서 ContextMenu로 실행해주세요.", this);
            return;
        }

        var items = CreateTestItems(); // 아이템 목록 값입니다.
        SetResult(isClear, items);
        int amount = 0; // 수량 값입니다.
        // item: 아이템 값입니다.
        foreach (var item in items)
        {
            amount += item.Count;
        }

        UpdateRecord(1, DateTime.Now.ToString("yyyy.MM.dd"), items.Count, amount);
        SetWindow(true);
        transform.SetAsLastSibling();
    }
#endif

    /// <summary>
    /// 기존 에셋에서 아이콘이 있는 아이템 최대 3개를 우선 선택합니다. 에셋과 인벤토리는 수정하지 않습니다.
    /// </summary>

#if UNITY_EDITOR
    List<Inventory.BagItem> CreateTestItems()
    {
        var candidates = new List<ItemData>(); // 후보 목록 값입니다.
        // guid: guid 값입니다.
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/05.Configs" }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid); // 경로 값입니다.
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path); // 아이템 값입니다.
            if (item != null)
            {
                candidates.Add(item);
            }
        }

        candidates.Sort((left, right) =>
        {
            bool leftFish = UnityEditor.AssetDatabase.GetAssetPath(left).Contains("/FishFilet/");
            bool rightFish = UnityEditor.AssetDatabase.GetAssetPath(right).Contains("/FishFilet/");
            if (leftFish != rightFish) return rightFish.CompareTo(leftFish);
            int icons = (right.ItemIcon != null).CompareTo(left.ItemIcon != null); // 아이콘 목록 값입니다.
            return icons != 0 ? icons : string.CompareOrdinal(UnityEditor.AssetDatabase.GetAssetPath(left), UnityEditor.AssetDatabase.GetAssetPath(right));
        });
        var items = new List<Inventory.BagItem>(); // 아이템 목록 값입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < candidates.Count && i < 8; i++)
        {
            items.Add(new Inventory.BagItem(candidates[i], i + 1));
        }

        return items;
    }
#endif

    #endregion

}
