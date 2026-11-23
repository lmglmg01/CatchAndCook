using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 주문의 음식 이미지와 조리 시간을 빈 슬롯에 전달하는 화면 표시용 뷰입니다.
/// 실제 주문 목록은 Presenter가 전달하며 재료 차감과 요리 수령은 담당 로직을 유지합니다.
/// </summary>
public class NPCCookingView : MonoBehaviour
{
    // 에디터 전용 영역은 기존 미리보기 테스트를 유지하며 실제 게임 빌드에서는 제외합니다.

    #region 멤버

    #region UI 연결 및 표시 설정

    // CookingList 하위의 조리 슬롯을 표시 순서대로 연결합니다. 기본 구성은 5칸입니다.
    [SerializeField] NPCCookingSlotView[] _views = new NPCCookingSlotView[5];

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트 멤버

    // ContextMenu 테스트에서만 사용할 음식 이미지입니다. 기존 초밥 스프라이트를 연결합니다.
    [SerializeField] Sprite[] _testFoodSprites;
    // 단일 주문 테스트의 표시 시간(초)입니다. 실제 조리 시간에는 사용하지 않습니다.
    [SerializeField, Min(0f)] float _testCookingSeconds = 5f;
    int _testFoodIndex; // 테스트 요리 인덱스 값입니다.
    // ContextMenu에서만 읽을 요리 원본 목록입니다. Sushi 에셋을 연결하며 원본은 변경하지 않습니다.
    [SerializeField] Sushi[] _testCookingDatas;
    int _testCookingDataIndex; // 테스트 조리 데이터 인덱스 값입니다.

    #endregion
#endif

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 모든 조리 슬롯을 비어 있는 상태로 초기화하고 숨깁니다.
    /// </summary>
    private void Awake()
    {
        ResetSlots();
    }

    /// <summary>
    /// NPC 또는 Canvas가 꺼지면 남은 조리 표시를 비워 다시 사용할 수 있게 합니다.
    /// </summary>
    void OnDisable()
    {
        ResetSlots();
    }

    #endregion

    public bool NeedsOrderSync { get; private set; } = true;
    readonly Dictionary<ICookingOrderInfo, NPCCookingSlotView> _orderSlots = new();
    readonly HashSet<ICookingOrderInfo> _currentOrders = new();
    readonly List<ICookingOrderInfo> _displayOrders = new();
    readonly List<ICookingOrderInfo> _removedOrders = new();

    /// <summary>두 실제 주문 목록을 합쳐 동기화합니다. 동일 주문은 조리 완료 후에도 같은 슬롯을 유지합니다.</summary>
    public void DisplayOrders(IReadOnlyList<ICookingOrderInfo> cooking, IReadOnlyList<ICookingOrderInfo> completed)
    {
        if (!isActiveAndEnabled) return;
        _currentOrders.Clear();
        _displayOrders.Clear();
        void Add(IReadOnlyList<ICookingOrderInfo> orders)
        {
            if (orders == null) return;
            foreach (var order in orders)
                if (order != null && order.Sushi != null && _currentOrders.Add(order)) _displayOrders.Add(order);
        }
        Add(cooking); Add(completed);
        _removedOrders.Clear();
        foreach (var pair in _orderSlots)
            if (!_currentOrders.Contains(pair.Key) || pair.Value == null) _removedOrders.Add(pair.Key);
        foreach (var order in _removedOrders)
        {
            if (_orderSlots[order] != null) _orderSlots[order].ResetSlot();
            _orderSlots.Remove(order);
        }
        foreach (var order in _displayOrders)
        {
            if (!_orderSlots.TryGetValue(order, out var slot))
            {
                slot = FindFreeOrderSlot();
                if (slot == null) continue;
                _orderSlots.Add(order, slot);
            }
            slot.DisplayOrder(order);
        }
        // 수령 대기 바를 위에, 제작 중 바를 아래에 모아 가이드의 순서로 표시합니다.
        int displayIndex = 0;
        for (int phase = 0; phase < 2; phase++)
            foreach (var order in _displayOrders)
                if (order.IsCompleted == (phase == 0) && _orderSlots.TryGetValue(order, out var slot))
                    slot.transform.SetSiblingIndex(displayIndex++);
        NeedsOrderSync = false;
    }

    /// <summary>이벤트 또는 호환용 조회가 전달한 진행도만 갱신합니다. 목록과 아이콘은 다시 만들지 않습니다.</summary>
    public void DisplayOrderProgress(ICookingOrderInfo order, float progress)
    {
        if (!isActiveAndEnabled || order == null) return;
        if (_orderSlots.TryGetValue(order, out var slot) && slot != null) slot.DisplayProgress(progress);
    }

    /// <summary>주문 목록 변경과 무관한 카메라 방향 정렬만 처리합니다.</summary>
    void LateUpdate()
    {
        if (_orderSlots.Count == 0) return;
        var camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
    }

    /// <summary>기존 슬롯을 우선 사용하고 주문이 더 많을 때만 같은 형태의 슬롯을 늘립니다.</summary>
    NPCCookingSlotView FindFreeOrderSlot()
    {
        if (_views == null || _views.Length == 0) return null;
        NPCCookingSlotView template = null;
        foreach (var slot in _views)
        {
            if (slot == null) continue;
            if (template == null) template = slot;
            if (!_orderSlots.ContainsValue(slot)) return slot;
        }
        if (template == null) return null;
        var extra = Instantiate(template, template.transform.parent, false);
        extra.name = "CookingSlot" + (_views.Length + 1);
        extra.ResetSlot();
        System.Array.Resize(ref _views, _views.Length + 1);
        _views[_views.Length - 1] = extra;
        return extra;
    }

    #region UI 표시 및 갱신


#if UNITY_EDITOR
    /// <summary>
    /// 빈 슬롯에 음식 이미지와 조리 시간을 전달하며, 가득 차면 기존 주문을 유지합니다.
    /// </summary>
    /// <param name="foodImageSprite">요리 이미지 스프라이트 이미지입니다.</param>
    /// <param name="maxCooltime">최대 재사용 대기 값입니다.</param>
    public void TakeAnOrder(Sprite foodImageSprite, float maxCooltime)
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        int index = GetAciveSlotIndex(); // 인덱스 값입니다.
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (index < 0)
        {
            return;
        }

        _views[index].gameObject.SetActive(true);
        _views[index].StartCook(foodImageSprite, maxCooltime);
    }

    /// <summary>
    /// 요리 데이터의 Icon과 CookTime을 읽어 빈 슬롯에 표시합니다. 원본 데이터는 변경하지 않습니다.
    /// </summary>
    /// <param name="cookingData">조리 데이터 값입니다.</param>
    public void TakeAnOrder(Sushi cookingData)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (cookingData == null)
        {
            return;
        }

        TakeAnOrderData(cookingData, cookingData.CookTime);
    }

    /// <summary>
    /// 요리 데이터와 담당 로직이 계산한 조리 시간을 전달받습니다. 원본 CookTime은 수정하지 않습니다.
    /// </summary>
    /// <param name="cookingData">조리 데이터 값입니다.</param>
    /// <param name="maxCooltime">최대 재사용 대기 값입니다.</param>
    public void TakeAnOrderData(Sushi cookingData, float maxCooltime)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (cookingData == null || !Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        int index = GetAciveSlotIndex(); // 인덱스 값입니다.
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (index < 0)
        {
            return;
        }

        _views[index].gameObject.SetActive(true);
        _views[index].StartCookData(cookingData, maxCooltime);
    }
#endif

    /// <summary>
    /// 조리 표시를 중단하고 모든 슬롯을 비웁니다. 실제 요리 데이터는 변경하지 않습니다.
    /// </summary>
    public void ResetSlots()
    {
        NeedsOrderSync = true;
        _orderSlots.Clear();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_views == null)
        {
            return;
        }

        // view: UI 동작과 데이터를 연결하는 뷰입니다.
        foreach (var view in _views)
        {
            if (view != null)
            {
                view.ResetSlot();
            }
        }
    }

    #endregion

    #region 표시 데이터 및 보조 기능

#if UNITY_EDITOR
    /// <summary>
    /// 비어 있는 유효한 슬롯의 인덱스를 반환하며, 없으면 -1을 반환합니다.
    /// </summary>
    int GetAciveSlotIndex()
    {
        if (_views != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _views.Length; i++)
            {
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (_views[i] != null && !_views[i].gameObject.activeSelf)
                {
                    return i;
                }
            }
        }

        Debug.Log("현재 가용 가능한 요리 슬롯이 없습니다.", this);
        return -1;
    }
#endif

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트

    /// <summary>
    /// 테스트용 요리 데이터를 순서대로 반환하며 빈 항목은 건너뜁니다.
    /// </summary>
    Sushi GetTestCookingData()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_testCookingDatas == null || _testCookingDatas.Length == 0)
        {
            return null;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _testCookingDatas.Length; i++)
        {
            int index = _testCookingDataIndex; // 인덱스 값입니다.
            _testCookingDataIndex = (_testCookingDataIndex + 1) % _testCookingDatas.Length;
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (_testCookingDatas[index] != null)
            {
                return _testCookingDatas[index];
            }
        }

        return null;
    }

    /// <summary>
    /// 테스트 이미지를 순서대로 반환하며 실제 음식 데이터는 참조하지 않습니다.
    /// </summary>
    Sprite GetTestFood()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_testFoodSprites == null || _testFoodSprites.Length == 0)
        {
            return null;
        }

        return _testFoodSprites[_testFoodIndex++ % _testFoodSprites.Length];
    }

    /// <summary>
    /// 시간이 흐르는 테스트는 플레이 모드에서만 실행하도록 안내합니다.
    /// </summary>
    bool CanTest()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (Application.isPlaying && isActiveAndEnabled)
        {
            return true;
        }

        Debug.Log("플레이 모드에서 활성화된 Canvas_Worker_Cook의 ContextMenu를 사용하세요.", this);
        return false;
    }

    /// <summary>
    /// 빈 슬롯 한 칸에 테스트 요리를 주문합니다.
    /// </summary>
    [ContextMenu("테스트/단일 주문 (플레이 모드)")]
    public void TestOrder()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!CanTest())
        {
            return;
        }

        var cookingData = GetTestCookingData(); // 조리 데이터 값입니다.
        if (cookingData != null)
        {
            TakeAnOrder(cookingData);
        }
        else
        {
            TakeAnOrder(GetTestFood(), _testCookingSeconds);
        }
    }

    /// <summary>
    /// 다섯 요리를 각각의 데이터에 지정된 시간으로 동시에 표시합니다. 데이터가 없으면 기존 이미지 테스트를 사용합니다.
    /// </summary>
    [ContextMenu("테스트/여러 요리 동시 진행 (플레이 모드)")]
    public void TestMultipleOrders()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!CanTest())
        {
            return;
        }

        ResetSlots();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_views == null)
        {
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _views.Length; i++)
        {
            var cookingData = GetTestCookingData(); // 조리 데이터 값입니다.
            if (cookingData != null)
            {
                TakeAnOrder(cookingData);
            }
            else
            {
                TakeAnOrder(GetTestFood(), 2f + i * 2f);
            }
        }
    }

    /// <summary>
    /// 0초 주문으로 완료 배경과 수령 아이콘을 즉시 확인합니다.
    /// </summary>
    [ContextMenu("테스트/즉시 완료 주문 (플레이 모드)")]
    public void TestCompletedOrder()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!CanTest())
        {
            return;
        }

        var cookingData = GetTestCookingData(); // 조리 데이터 값입니다.
        if (cookingData != null)
        {
            TakeAnOrderData(cookingData, 0f);
        }
        else
        {
            TakeAnOrder(GetTestFood(), 0f);
        }
    }

    /// <summary>
    /// 완료된 슬롯만 수령 표시로 정리하며 조리 중인 슬롯은 유지합니다.
    /// </summary>
    [ContextMenu("테스트/완성 요리 수령 (플레이 모드)")]
    public void TestTakeCompletedFood()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (!CanTest() || _views == null)
        {
            return;
        }

        // view: UI 동작과 데이터를 연결하는 뷰입니다.
        foreach (var view in _views)
        {
            if (view != null && view.IsReady)
            {
                view.TakeFood();
            }
        }
    }

    /// <summary>
    /// 테스트 조리를 중단하고 모든 슬롯을 숨깁니다.
    /// </summary>
    [ContextMenu("테스트/전체 초기화")]
    public void TestResetOrders()
    {
        ResetSlots();
        _testFoodIndex = 0;
        _testCookingDataIndex = 0;
    }

    #endregion
#endif

}
