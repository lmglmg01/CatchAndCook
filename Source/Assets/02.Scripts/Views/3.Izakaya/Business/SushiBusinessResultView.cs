using UnityEngine;
using TMPro;

/// <summary>
/// 영업 정산의 매출·손님·직원 활동을 표시할 뷰입니다.
/// Presenter에서 매출·판매·손님·직원 통계를 받아 표시하며 실제 골드는 처리하지 않습니다.
/// </summary>
public class SushiBusinessResultView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 판매 금액 합계를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _totalSalesText;
    // 영업 운영비를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _runningCostText;
    // 판매 금액에서 운영비를 뺀 순이익을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _netProfitText;
    // 메뉴별 판매 내역 슬롯을 위에서 아래 순서로 연결합니다.
    [SerializeField] SushiMenuSalesSlotView[] _menuSalesSlots;
    // 총 방문 손님 수를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _totalGuestsText;
    // 요리를 받고 만족한 손님 수를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _satisfiedGuestsText;
    // 주문하지 않고 떠난 손님 수를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _leftGuestsText;
    // 직원별 활동 내역 슬롯을 표시 순서대로 연결합니다.
    [SerializeField] SushiStaffResultSlotView[] _staffResultSlots;
    // 정산 확인 후 다음 진행에 사용할 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _confirmButton;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;
    // 정산 확인 요청을 전달할 같은 Canvas_Sushi의 IjakayaView를 연결합니다.
    [SerializeField] IjakayaView _owner;

    #endregion

    [Header("정산 데이터 조회")]
    [SerializeField] SushiManager _source;
    [SerializeField] TMP_Text _headlineText;
    [SerializeField] TMP_Text _emptySalesText;
    readonly System.Collections.Generic.List<SushiMenuSalesSlotView> _rows = new();
    float _nextRefresh;
    readonly System.Collections.Generic.HashSet<UnityEngine.InputSystem.Key> _heldKeys = new(); // 열릴 때 누른 키와 새 입력을 구분합니다.
    float _continueAfter; // 열린 프레임의 입력으로 즉시 닫히는 것을 방지합니다.
    bool _confirming; // 다음 진행 중복 요청을 막습니다.
    bool _testMode; // 임시 표시 중에는 실제 정산 조회와 확인 요청을 차단합니다.
    string _titleBeforeTest;

    [Header("정산 등장 연출")]
    [SerializeField] RectTransform _resultVisual;
    bool _entering;
    Coroutine _entranceRoutine;

    /// <summary>기존 참조를 유지하며 표시 오브젝트만 묶고 Canvas Editor 등장 효과를 준비합니다.</summary>
    public void PrepareResultEntrance()
    {
        if (_resultVisual == null)
        {
            _resultVisual = transform.Find("ResultVisual") as RectTransform;
            if (_resultVisual == null)
            {
                var root = new GameObject("ResultVisual", typeof(RectTransform), typeof(CanvasGroup));
                _resultVisual = root.GetComponent<RectTransform>(); _resultVisual.SetParent(transform, false);
                _resultVisual.anchorMin = Vector2.zero; _resultVisual.anchorMax = Vector2.one;
                _resultVisual.offsetMin = _resultVisual.offsetMax = Vector2.zero;
                // 배경의 입력 차단과 어두움은 원래 루트에 남겨두고 영수증·내용만 함께 움직입니다.
                var children = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in transform) if (child != _resultVisual) children.Add(child);
                foreach (var child in children) child.SetParent(_resultVisual, false);
            }
        }
        if (_resultVisual.GetComponent<CanvasGroup>() == null) _resultVisual.gameObject.AddComponent<CanvasGroup>();
        var director = GetComponent<UIDirectorView>() ?? gameObject.AddComponent<UIDirectorView>();
        var binding = director.Bindings.Find(b => b.id == "sushi-settlement-enter");
        if (binding == null)
        {
            binding = new UIDirectorView.Binding { id = "sushi-settlement-enter" };
            binding.enter.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Move,
                duration = .8f, fromOffset = new Vector2(0, -1200), toOffset = new Vector2(0, 45), ease = DG.Tweening.Ease.OutSine });
            binding.enter.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Move,
                delay = .8f, duration = .2f, fromOffset = new Vector2(0, 45), toOffset = Vector2.zero, ease = DG.Tweening.Ease.InOutSine });
            director.Bindings.Add(binding);
        }
        binding.owner = this; binding.target = _resultVisual; binding.enterMethod = nameof(EnterBusinessResult);
    }

    /// <summary>Canvas Editor에서 저장한 아래쪽 등장과 짧은 내려앉기 효과를 재생합니다.</summary>
    public void EnterBusinessResult()
    {
        // <UIDirector:EnterBusinessResult>
        void __UIDirectorOriginal() { if (_resultVisual != null) _resultVisual.gameObject.SetActive(true); }
        global::UIDirectorView.Dispatch(this, "EnterBusinessResult", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>편집기에서 변경한 실제 등장 길이를 읽어 연출 중 확인 입력을 차단합니다.</summary>
    float ResultEntranceDuration()
    {
        var director = GetComponent<UIDirectorView>();
        var binding = director != null ? director.Bindings.Find(b => b.id == "sushi-settlement-enter") : null;
        if (binding == null || !director.enabled) return 0f;
        float start = 0f, end = 0f, duration = 0f;
        foreach (var effect in binding.enter)
        {
            if (!effect.enabled) continue;
            start = UIDirectorView.Start(effect, start, end); end = start + Mathf.Max(.01f, effect.duration);
            duration = Mathf.Max(duration, end);
        }
        return duration;
    }

    /// <summary>등장이 끝난 뒤 새로 누른 키만 정산 확인에 사용합니다.</summary>
    System.Collections.IEnumerator FinishResultEntrance(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        _entering = false; _entranceRoutine = null; _continueAfter = Time.unscaledTime + .1f;
    }

    [Header("판매 내역 스크롤")]
    [SerializeField,Range(1.0f,5000.0f), Tooltip("마우스 휠 한 칸당 이동 거리입니다. 값이 클수록 빠르게 이동합니다.")]
    float _wheelScrollSpeed = 2500f;
    UnityEngine.UI.ScrollRect _salesScroll;
    float _originalScrollSensitivity;
    bool _wheelConfigured;

    /// <summary>메뉴 목록의 휠 입력을 이 뷰에서 처리하여 공통 UI 입력 설정에 영향받지 않게 합니다.</summary>
    void ConfigureWheelScrolling()
    {
        if (_wheelConfigured) return;
        _salesScroll = GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        if (_salesScroll == null) return;
        _originalScrollSensitivity = _salesScroll.scrollSensitivity;
        // EventSystem의 기본 휠 처리와 이 뷰의 처리가 중복되는 것을 방지합니다.
        _salesScroll.scrollSensitivity = 0;
        _wheelConfigured = true;
    }

    /// <summary>마우스가 판매 목록 위에 있을 때 휠을 인스펙터에서 지정한 거리만큼 이동합니다.</summary>
    void LateUpdate()
    {
        ConfigureWheelScrolling();
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null || _salesScroll == null || !_salesScroll.isActiveAndEnabled || !_salesScroll.vertical) return;
        var viewport = _salesScroll.viewport;
        if (viewport == null || _salesScroll.content == null) return;
        var canvas = viewport.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, mouse.position.ReadValue(), camera)) return;
        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(wheel, 0)) return;
        float range = _salesScroll.content.rect.height - viewport.rect.height;
        if (range <= 0) return;
        _salesScroll.StopMovement();
        _salesScroll.verticalNormalizedPosition = Mathf.Clamp01(_salesScroll.verticalNormalizedPosition + wheel / 120f * _wheelScrollSpeed / range);
    }

    /// <summary>정산창을 닫으면 ScrollRect의 원래 휠 설정을 복원합니다.</summary>
    void RestoreWheelScrolling()
    {
        if (_wheelConfigured && _salesScroll != null) _salesScroll.scrollSensitivity = _originalScrollSensitivity;
        _wheelConfigured = false;
    }
    /// <summary>정산창을 열 때 실제 누적 데이터를 읽고 미구현 영역은 빈 틀로 표시합니다.</summary>
    void OnEnable()
    { 
        _continueAfter = Time.unscaledTime + .3f; _confirming = false;
        _heldKeys.Clear();
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null) foreach (var key in keyboard.allKeys) if (key.isPressed) _heldKeys.Add(key.keyCode);
        ConfigureWheelScrolling();
        PrepareResultEntrance();
        GetComponentInParent<SushiRestaurantStyleView>()?.ApplySettlementGuide(_resultVisual);
        RefreshFromSource();
        _entering = true; EnterBusinessResult();
        _entranceRoutine = StartCoroutine(FinishResultEntrance(ResultEntranceDuration()));
    }

    /// <summary>다른 컴포넌트의 초기화가 완료된 뒤 정산 값을 다시 읽습니다.</summary>
    void Start()
    {
        RefreshFromSource();
    }

    /// <summary>정산창이 열린 동안 늦게 도착하는 판매 기록도 반영합니다.</summary>
    void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool pressed = false;
        if (keyboard != null) foreach (var key in keyboard.allKeys)
        {
            if (key.isPressed) { if (_heldKeys.Add(key.keyCode)) pressed = true; }
            else _heldKeys.Remove(key.keyCode);
        }
        else _heldKeys.Clear();
        if (!_confirming && Time.unscaledTime >= _continueAfter && pressed)
        { ConfirmSettlement(); return; }
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .25f;
        RefreshFromSource();
    }

    /// <summary>현재 씬의 매니저를 읽기 전용으로 조회합니다. 순이익은 표시용으로만 계산합니다.</summary>
    public void RefreshFromSource()
    {
        if (_testMode) return;
        if (_source == null)
            foreach (var candidate in FindObjectsByType<SushiManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene) { _source = candidate; break; }
        var presenter = _owner != null ? _owner.GetComponent<IjakayaPresenter>() : GetComponentInParent<IjakayaPresenter>();
        if (presenter != null && presenter.TryBuildSettlement(out var data)) { Display(data); return; }
        PrepareRows();
        ShowUnimplementedSections();
        if (_source == null)
        {
            if (_headlineText != null) _headlineText.text = "—";
            if (_totalSalesText != null) _totalSalesText.text = "—";
            if (_runningCostText != null) _runningCostText.text = "—";
            if (_netProfitText != null) _netProfitText.text = "—";
            foreach (var row in _rows) row.gameObject.SetActive(false);
            if (_emptySalesText != null) { _emptySalesText.text = "영업 데이터를 기다리는 중입니다."; _emptySalesText.gameObject.SetActive(true); }
            return;
        }
        Bind(_source.TotalSalesGold, _source.AccumulatedOperationCost,
            _source.TotalSalesGold - _source.AccumulatedOperationCost,
            _source.TotalSpawnedGuestCount, _source.SatisfiedGuestCount, _source.UnservedGuestCount);
        DisplayStaffActivity(0, _owner != null ? _owner.StaffPortrait(StaffType.Serving) : null, (_source.Serving != null ? _source.Serving.ServedCount : 0).ToString());
        DisplayStaffActivity(1, _owner != null ? _owner.StaffPortrait(StaffType.Cooking) : null, (_source.Cooker != null ? _source.Cooker.CompletedCookingCount : 0).ToString());
        var sales = _source.MenuSalesResults;
        int index = 0;
        foreach (var sale in sales)
        {
            if (sale == null) continue;
            EnsureRow(index);
            if (index >= _rows.Count) break;
            var menu = sale.Menu;
            _rows[index].gameObject.SetActive(true);
            BindSale(_rows[index], menu != null ? menu.Icon : null, menu != null ? menu.Name : "메뉴 정보 없음", sale.SoldCount, sale.EarnedGold, true);
            index++;
        }
        for (int i = index; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        if (_emptySalesText != null) { _emptySalesText.text = "판매 내역이 없습니다."; _emptySalesText.gameObject.SetActive(index == 0); }
        GetComponentInParent<SushiRestaurantStyleView>()?.LayoutSalesCards();
    }

    /// <summary>기존 슬롯에 판매 내역을 전달하고 금액은 통화 접미사 없이 표시합니다.</summary>
    void BindSale(SushiMenuSalesSlotView row, Sprite icon, string menuName, int count, float amount, bool registered)
    {
        row.Bind(IjakayaView.ResolveMenuIcon(icon, menuName, this), menuName, count, amount, registered);
        var amountLabel = row.transform.Find("Amount");
        if (amountLabel != null && amountLabel.TryGetComponent<TMP_Text>(out var text))
            text.text = registered ? amount.ToString("0") : string.Empty;
    }
    /// <summary>기존 행을 한 번만 등록하여 재열기 때 행이 중복되지 않게 합니다.</summary>
    void PrepareRows()
    {
        if (_rows.Count != 0 || _menuSalesSlots == null) return;
        foreach (var row in _menuSalesSlots) if (row != null) _rows.Add(row);
    }

    /// <summary>기본 다섯 메뉴를 초과한 음료 등도 스크롤 목록에 빠짐없이 표시합니다.</summary>
    void EnsureRow(int index)
    {
        PrepareRows();
        if (_rows.Count == 0) return;
        while (_rows.Count <= index)
        {
            var row = Instantiate(_rows[0], _rows[0].transform.parent);
            row.name = "MenuSalesSlot" + (_rows.Count + 1);
            _rows.Add(row);
        }
    }

    /// <summary>손님과 직원은 실제 통계에 연결하지 않고 자리만 표시합니다.</summary>
    void ShowUnimplementedSections()
    {
        if (_totalGuestsText != null) _totalGuestsText.text = "—";
        if (_satisfiedGuestsText != null) _satisfiedGuestsText.text = "—";
        if (_leftGuestsText != null) _leftGuestsText.text = "—";
    }
    #region 표시 데이터 형식

    /// <summary>
    /// 메뉴 판매 내역 한 행의 표시 데이터입니다.
    /// </summary>
    [System.Serializable]
    public sealed class MenuSaleData
    {

        #region 멤버

        #region 런타임 상태

        public Sprite Icon; // 아이콘 이미지입니다.
        public string Name; // 이름 값입니다.
        public int Count; // 개수 값입니다.
        public float Amount; // 수량 값입니다.
        public bool Registered; // 등록 여부입니다.

        #endregion

        #endregion

    }

    /// <summary>
    /// 직원 활동 내역 한 행의 표시 데이터입니다.
    /// </summary>
    [System.Serializable]
    public sealed class StaffActivityData
    {

        #region 멤버

        #region 런타임 상태

        public Sprite Portrait; // 초상화 이미지입니다.
        public string Activity; // 활동 값입니다.

        #endregion

        #endregion

    }

    /// <summary>
    /// 외부 호출과의 호환을 위한 영업 정산 표시 데이터입니다. 순이익은 매출에서 운영비를 빼 표시합니다.
    /// </summary>
    [System.Serializable]
    public sealed class Presentation
    {

        #region 멤버

        #region 런타임 상태

        // TotalSales: 전체 판매 값입니다.
        // RunningCost: 실행 중 비용 값입니다.
        // NetProfit: 그물 수익 값입니다.
        public float TotalSales, RunningCost, NetProfit;
        // TotalGuests: 전체 손님 목록 값입니다.
        // SatisfiedGuests: 만족 손님 목록 값입니다.
        // LeftGuests: 왼쪽 손님 목록 값입니다.
        public int TotalGuests, SatisfiedGuests, LeftGuests;
        public MenuSaleData[] MenuSales; // 메뉴 판매 목록입니다.
        public StaffActivityData[] StaffActivities; // 직원 활동 목록입니다.

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
        InitializeButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(ConfirmSettlement);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(ConfirmSettlement);
            _confirmButton.onClick.AddListener(ConfirmSettlement);
        }
    }

    /// <summary>
    /// 정산 금액을 표시합니다. 손님 통계를 함께 표시하며 실제 골드를 지급하거나 차감하지 않습니다.
    /// </summary>
    /// <param name="totalSales">전체 판매 값입니다.</param>
    /// <param name="runningCost">실행 중 비용 값입니다.</param>
    /// <param name="netProfit">그물 수익 값입니다.</param>
    /// <param name="totalGuests">전체 손님 목록 값입니다.</param>
    /// <param name="satisfiedGuests">만족 손님 목록 값입니다.</param>
    /// <param name="leftGuests">왼쪽 손님 목록 값입니다.</param>
    public void Bind(float totalSales, float runningCost, float netProfit, int totalGuests, int satisfiedGuests, int leftGuests)
    {
        if (_headlineText != null) _headlineText.text = $"{totalSales:0}";
        if (_totalSalesText != null) _totalSalesText.text = $"{totalSales:0}";
        if (_runningCostText != null) _runningCostText.text = runningCost > 0 ? $"-{runningCost:0}" : "0";
        if (_netProfitText != null) _netProfitText.text = $"{netProfit:0}";
        if (_totalGuestsText != null) _totalGuestsText.text = totalGuests.ToString();
        if (_satisfiedGuestsText != null) _satisfiedGuestsText.text = satisfiedGuests.ToString();
        if (_leftGuestsText != null) _leftGuestsText.text = leftGuests.ToString();
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
    /// 정산 메뉴 다섯 칸 중 한 칸의 판매 내역을 표시합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="count">개수 값입니다.</param>
    /// <param name="amount">수량 값입니다.</param>
    /// <param name="registered">등록 여부입니다.</param>
    public void DisplayMenuSale(int index, Sprite icon, string name, int count, float amount, bool registered)
    {
        if (_menuSalesSlots != null && index >= 0 && index < _menuSalesSlots.Length && _menuSalesSlots[index] != null)
        {
            BindSale(_menuSalesSlots[index], icon, name, count, amount, registered);
        }
    }

    /// <summary>
    /// 직원별 활동 내역 한 칸을 표시합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="portrait">초상화 이미지입니다.</param>
    /// <param name="activity">활동 값입니다.</param>
    public void DisplayStaffActivity(int index, Sprite portrait, string activity)
    {
        if (_staffResultSlots != null && index >= 0 && index < _staffResultSlots.Length && _staffResultSlots[index] != null)
            _staffResultSlots[index].Bind(portrait, activity);
    }

    /// <summary>기존 호출부와의 호환성을 유지하고 매출 데이터만 표시합니다.</summary>
    public void Display(Presentation data)
    {
        if (data == null) return;
        Bind(data.TotalSales, data.RunningCost, data.TotalSales - data.RunningCost, data.TotalGuests, data.SatisfiedGuests, data.LeftGuests);
        for (int i = 0; _staffResultSlots != null && i < _staffResultSlots.Length; i++)
        {
            var activity = data.StaffActivities != null && i < data.StaffActivities.Length ? data.StaffActivities[i] : null;
            DisplayStaffActivity(i, activity?.Portrait, activity?.Activity ?? "—");
        }
        PrepareRows();
        int count = data.MenuSales != null ? data.MenuSales.Length : 0;
        for (int i = 0; i < count; i++)
        {
            EnsureRow(i);
            if (i >= _rows.Count) break;
            var item = data.MenuSales[i];
            _rows[i].gameObject.SetActive(item != null && item.Registered);
            if (item != null) BindSale(_rows[i], item.Icon, item.Name, item.Count, item.Amount, item.Registered);
        }
        for (int i = count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        if (_emptySalesText != null) { _emptySalesText.text = "판매 내역이 없습니다."; _emptySalesText.gameObject.SetActive(count == 0); }
        GetComponentInParent<SushiRestaurantStyleView>()?.LayoutSalesCards();
    }
    #endregion

    #region 선택 및 외부 요청

    /// <summary>
    /// 정산 화면을 닫은 뒤 외부에 확인 요청을 전달합니다.
    /// </summary>
    void ConfirmSettlement()
    {
        if (_entering || _confirming || !gameObject.activeInHierarchy || Time.unscaledTime < _continueAfter) return;
        _confirming = true;
        bool wasTest = _testMode;
        Close();
        if (wasTest) return;
        if (_owner != null)
        {
            _owner.RequestSettlementConfirmation();
        }
    }

    #endregion

    /// <summary>화면을 닫으면 임시 표시 모드와 테스트 제목을 해제합니다.</summary>
    void OnDisable()
    {
        if (_entranceRoutine != null) StopCoroutine(_entranceRoutine);
        _entranceRoutine = null; _entering = false;
        var director = GetComponent<UIDirectorView>();
        if (director != null && _resultVisual != null) director.CancelEffects(this, _resultVisual);
        RestoreWheelScrolling(); ResetTestMode();
    }

    /// <summary>임시 데이터 사용을 끝내고 기존 화면 제목을 복원합니다.</summary>
    void ResetTestMode()
    {
        _testMode = false;
        var title = transform.Find("Title");
        if (_titleBeforeTest != null && title != null && title.TryGetComponent<TMP_Text>(out var label))
            label.text = _titleBeforeTest;
        _titleBeforeTest = null;
    }

#if UNITY_EDITOR
    /// <summary>실제 게임 데이터를 변경하지 않는 기본 다섯 메뉴의 정산 예시를 엽니다.</summary>
    [ContextMenu("정산 테스트/1. 기본 판매 내역 (5개)")]
    void TestNormalSales() { ShowTestSettlement(5, 1460f, "기본 판매"); }

    /// <summary>열두 메뉴를 표시하여 마우스 휠과 드래그 스크롤을 확인합니다.</summary>
    [ContextMenu("정산 테스트/2. 긴 판매 내역 (12개·스크롤)")]
    void TestLongSales() { ShowTestSettlement(12, 1460f, "스크롤 12개"); }

    /// <summary>매출과 운영비가 없는 빈 정산 화면을 표시합니다.</summary>
    [ContextMenu("정산 테스트/3. 판매 내역 없음")]
    void TestEmptySales() { ShowTestSettlement(0, 0f, "판매 없음"); }

    /// <summary>운영비가 매출보다 클 때 음수 순이익을 확인합니다.</summary>
    [ContextMenu("정산 테스트/4. 적자 정산")]
    void TestLoss() { ShowTestSettlement(1, 5000f, "적자 정산"); }

    /// <summary>테스트를 끝내고 현재 씬의 실제 누적 데이터를 다시 표시합니다.</summary>
    [ContextMenu("정산 테스트/5. 실제 데이터로 복귀")]
    void ExitSettlementTest()
    {
        ResetTestMode();
        if (Application.isPlaying) RefreshFromSource();
    }

    /// <summary>플레이 모드에서만 표시용 데이터를 구성하며 매니저와 인벤토리에는 쓰지 않습니다.</summary>
    void ShowTestSettlement(int count, float cost, string scenario)
    {
        if (!Application.isPlaying)
        {
            Debug.Log("정산 테스트는 Play 모드에서 실행해주세요. 씬에 테스트 데이터를 저장하지 않습니다.", this);
            return;
        }
        _testMode = true;
        if (_pageRoot != null) _pageRoot.SetActive(true);
        gameObject.SetActive(true);
        InitializeButtons();
        var title = transform.Find("Title");
        if (title != null && title.TryGetComponent<TMP_Text>(out var label))
        {
            if (_titleBeforeTest == null) _titleBeforeTest = label.text;
            label.text = "정산 테스트 · " + scenario;
        }
        var menus = new System.Collections.Generic.List<Sushi>();
        var guids = UnityEditor.AssetDatabase.FindAssets("t:Sushi");
        System.Array.Sort(guids, System.StringComparer.Ordinal);
        foreach (var guid in guids)
        {
            var menu = UnityEditor.AssetDatabase.LoadAssetAtPath<Sushi>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (menu != null && menu.Icon != null) menus.Add(menu);
        }
        int[] quantities = { 12, 10, 8, 6, 4 };
        float[] amounts = { 3600, 3000, 1600, 5600, 2400 };
        var items = new MenuSaleData[count];
        float total = 0;
        for (int i = 0; i < count; i++)
        {
            var menu = menus.Count > 0 ? menus[i % menus.Count] : null;
            float amount = amounts[i % amounts.Length];
            total += amount;
            items[i] = new MenuSaleData { Icon = menu != null ? menu.Icon : null,
                Name = menu != null ? menu.Name : "테스트 메뉴 " + (i + 1),
                Count = quantities[i % quantities.Length], Amount = amount, Registered = true };
        }
        Display(new Presentation { TotalSales = total, RunningCost = cost, MenuSales = items });
        Canvas.ForceUpdateCanvases();
        var scroll = GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        if (scroll != null)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }
        Debug.Log("정산 테스트: " + scenario + ". 실제 골드·판매 기록은 변경하지 않으며 확인 버튼은 테스트 창만 닫습니다.", this);
    }
#endif
}
