using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 혹은 서빙 보조 NPC가 운반하는 요리와 상호작용·폐기 안내를 표시합니다.
/// 실제 전달, 주문 취소 판정과 폐기는 담당 로직에서 처리합니다.
/// </summary>
public class InteractToGuestView : MonoBehaviour
{
    // 에디터 전용 영역은 기존 미리보기 테스트를 유지하며 실제 게임 빌드에서는 제외합니다.

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("상호작용 이미지")]
    [SerializeField] Image _foodImage; // 말풍선 안의 FoodIcon Image를 연결합니다.

    [Header("상호작용 키")]
    [SerializeField] Image _KeyImage; // Actions 아래 KeyIcon Image를 연결합니다.
    [SerializeField] Image _DiscardImage; // Actions 아래 DiscardIcon Image를 연결합니다. 취소된 요리일 때만 보입니다.
    [SerializeField] TMP_Text _keyText; // KeyIcon 안에서 상호작용 키를 표시할 KeyText를 연결합니다.
    // 말풍선·음식·키·폐기 아이콘을 모두 포함한 DisplayRoot를 연결합니다. Canvas 자체는 끄지 않습니다.
    [SerializeField] GameObject _displayRoot;
    [Header("음식 폐기 표시")]
    [SerializeField] UnityEngine.InputSystem.InputActionReference _discardAction; // 실제 폐기 액션의 표시 키만 읽습니다.
    [SerializeField] GameObject _discardGaugeRoot; // 기존 잔반처리와 같은 진행 표시를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _discardGaugeImage;
    [SerializeField] TMP_Text _discardNoticeText;
    [SerializeField, Min(.01f)] float _discardEffectDuration = .35f;
    SushiCleanupBubbleView _bubblePresentation;
    SushiCleanupBubbleView BubblePresentation => _bubblePresentation != null ? _bubblePresentation : (_bubblePresentation = GetComponent<SushiCleanupBubbleView>());
    Sequence _discardEffect;
    UnityEngine.UI.Image _discardEffectImage;

    // 현재 표시 중인 요리 원본입니다. 실행 중 입력받으며 숨길 때 제거합니다.
    [SerializeField] Sushi _foodData;

    #endregion

    #region 런타임 상태

    SushiCarrier _carrier;
    SushiFoodDiscarder _discarder;
    bool _wasDiscarding; // 실제 폐기 완료 뒤 한 번만 연출하기 위한 이전 표시 상태입니다.
    /// <summary>운반 데이터는 조회만 하고 실제 제공·폐기 입력은 기존 로직에 맡깁니다.</summary>
    public void BindCarrier(SushiCarrier carrier)
    {
        UnsubscribeCarrier();
        _carrier = carrier;
        ApplyServingCanvasOrder();
        _discarder = carrier != null ? carrier.GetComponent<SushiFoodDiscarder>() : null;
        SubscribeCarrier();
        RefreshFromModel();
    }

    /// <summary>정상 서빙은 음식 폐기 연출로 표시하지 않도록 기존 성공 알림만 구독합니다.</summary>
    void SubscribeCarrier()
    {
        if (_carrier == null || !isActiveAndEnabled) return;
        _carrier.FoodServed -= HandleFoodServed;
        _carrier.FoodServed += HandleFoodServed;
    }

    void UnsubscribeCarrier() { if (_carrier != null) _carrier.FoodServed -= HandleFoodServed; }
    void HandleFoodServed(GuestNPC guest) => _wasDiscarding = false;

    bool _isVisible; // 노출 여부입니다.
    bool _requiresDiscard; // 필요 폐기 여부입니다.
    bool _manualDiscardRequired; // 수동 폐기 필요 여부입니다.
#if UNITY_EDITOR
    bool _proximityDiscardRequired; // 근접 폐기 필요 여부입니다.
    NPCOrderView _targetOrder; // 대상 주문 표시를 연결할 뷰입니다.
    int _targetOrderVersion; // 대상 주문 회차 값입니다.
    bool _targetCanceled; // 대상 취소 여부입니다.
    Vector3 _targetPosition; // 대상 위치 값입니다.
    float _targetRadius; // 대상 반경 값입니다.
#endif

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트 멤버

    bool _useProximityPreview; // 사용 근접 미리보기 여부입니다.
    bool _previewNearby; // 미리보기 근처 여부입니다.
    // ContextMenu에서만 읽을 Sushi 에셋을 연결합니다. 원본 요리 데이터는 변경하지 않습니다.
    [SerializeField] Sushi _testFoodData;

    #endregion
#endif

    #region 프로퍼티

    /// <summary>
    /// 이 운반 요리의 대상 손님 주문 View입니다.
    /// </summary>
#if UNITY_EDITOR
    public NPCOrderView TargetOrder => _targetOrder;
#endif

    /// <summary>
    /// Canvas 높이가 아닌 운반 주체의 몸체 위치를 반환합니다.
    /// </summary>
#if UNITY_EDITOR
    public Vector3 InteractorPosition => transform.parent != null ? transform.parent.position : transform.position;
#endif

    /// <summary>
    /// 현재 표시 중인 요리 데이터를 반환합니다. 기존 Sprite 호출에서는 null입니다.
    /// </summary>
    public Sushi FoodData => _foodData;

    /// <summary>
    /// 현재 안내가 표시 중인지 반환합니다.
    /// </summary>
    public bool IsVisible => _isVisible;

    /// <summary>
    /// 전달받은 상태가 요리 폐기를 요구하는지 반환합니다.
    /// </summary>
    public bool IsDiscardRequired => _requiresDiscard;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 처음에는 빈 상태로 상호작용 안내 전체를 숨깁니다.
    /// </summary>
    void ApplyServingCanvasOrder()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        canvas.overrideSorting = true;
        canvas.sortingOrder = GetComponentInParent<Hero>() != null ? 1 : 0;
    }

    void Awake()
    {
        ApplyServingCanvasOrder();
        Hide();
    }

    /// <summary>
    /// 대상 주문의 취소 여부와 거리를 읽어 폐기 안내를 갱신합니다.
    /// </summary>
    /// <summary>Canvas를 다시 켰을 때 비활성화 동안 놓친 현재 표시를 한 번 복원합니다.</summary>
    void OnEnable() { SubscribeCarrier(); RefreshFromModel(); }

    bool _pollModelState = true; // TODO: 획득·전달·폐기·주문 유효성 알림 연결 전에는 기존 조회를 유지합니다.
    /// <summary>필요한 모든 모델 알림이 연결되면 임시 상태 조회를 끄고 외부 갱신을 사용합니다.</summary>
    public void SetExternalUpdates(bool enabled) { _pollModelState = !enabled; RefreshFromModel(); }

    /// <summary>Presenter가 운반 상태 변경 알림을 받으면 내용만 갱신합니다.</summary>
    public void RefreshFromModel()
    {
        if (!isActiveAndEnabled) return;
        if (_carrier != null)
        {
            var food = _carrier.CurrentSushi;
            if (food == null)
            {
                if (_isVisible)
                {
                    // 기존 폐기 로직이 실제 음식을 비운 뒤에만 복제 아이콘을 한 번 떨어뜨립니다.
                    if (_wasDiscarding && _foodData != null) PlayDiscardEffect(IjakayaView.ResolveMenuIcon(_foodData, this));
                    Hide();
                }
            }
            else if (!_isVisible || _foodData != food) Show(food);
            if (_isVisible)
            {
                bool player = _carrier.GetComponentInParent<Hero>() != null;
                SetKeyImage(player);
                bool available = player && _discarder != null && _discarder.isActiveAndEnabled;
                DisplayDiscardProgress(available, available && _discarder.IsDiscarding,
                    available ? _discarder.DiscardProgress : 0f, DiscardKeyLabel());
            }
        }
        RefreshOrderTarget();
    }

    /// <summary>모델 알림 연결 전의 호환용 조회와 카메라 방향 정렬을 구분합니다.</summary>
    void LateUpdate()
    {
        // TODO(UI-EVENT-08 / 운반 상태 임시 조회): SushiCarrier.ContentChanged(carrier) 알림이 필요합니다(추가 제안).
        // 연결할 Presenter 함수: NotifyCarrierDisplayChanged(carrier). 수령·전달·폐기·초기화 및 안내 조건 변경 후 알립니다.
        // FoodServed는 기존 전달 성공 연결만 담당합니다. 획득·폐기·주문 유효성 등 모든 표시 조건의 알림을 보완해야 합니다.
        // 모두 연결한 뒤 SetCarrierNotificationsEnabled(carrier, true)로 끄고 검증 후 이 조회만 제거합니다. OnEnable/BindCarrier 복원과 회전 정렬은 유지합니다.
        if (_pollModelState) RefreshFromModel();
        if (_carrier == null) return;
        var camera = Camera.main;
        if (camera != null) transform.rotation = camera.transform.rotation;
    }

    /// <summary>
    /// 플레이어·직원 또는 Canvas가 꺼질 때 이전 요리와 취소 표시를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        UnsubscribeCarrier();
        Hide();
        ClearDiscardEffect();
    }

    void OnDestroy() { UnsubscribeCarrier(); ClearDiscardEffect(); }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 현재 음식과 상호작용 표시를 켜고 이전 폐기 안내는 해제합니다.
    /// </summary>
    public void Show()
    {
        ClearOrderTarget();
        _isVisible = true;
        if (_displayRoot != null)
        {
            _displayRoot.SetActive(true);
        }

        SetDiscardFoodImage(true);
        SetKeyImage(true);
        SetDiscardImage(false);
        BubblePresentation?.Present(true,_foodImage!=null?_foodImage.sprite:null,false,false,false,0f,DiscardKeyLabel());
    }

    /// <summary>
    /// 요리 데이터와 폐기 필요 여부를 표시합니다. 원본 요리와 주문 데이터는 변경하지 않습니다.
    /// </summary>
    /// <param name="foodData">요리 데이터 값입니다.</param>
    /// <param name="requiresDiscard">필요 폐기 여부입니다.</param>
    public void Show(Sushi foodData, bool requiresDiscard = false)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (foodData == null)
        {
            Hide();
            return;
        }

        SetFoodImageSprite(IjakayaView.ResolveMenuIcon(foodData, this));
        _foodData = foodData;
        Show();
        SetDiscardImage(requiresDiscard);
    }

    /// <summary>
    /// 음식과 모든 안내를 숨기고 이전 요리·폐기 상태를 비웁니다.
    /// </summary>
    public void Hide()
    {
        DisplayDiscardProgress(false, false, 0f);
        ClearOrderTarget();
        _isVisible = false;
        SetDiscardFoodImage(false);
        SetKeyImage(false);
        SetDiscardImage(false);
        _foodData = null;
        if (_foodImage != null)
        {
            _foodImage.sprite = null;
            _foodImage.color = Color.clear;
        }

        if (_displayRoot != null)
        {
            _displayRoot.SetActive(false);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 음식과 대상 손님·현재 주문 회차를 함께 연결합니다.
    /// </summary>
    /// <param name="foodData">요리 데이터 값입니다.</param>
    /// <param name="targetOrder">대상 주문 표시를 연결할 뷰입니다.</param>
    public void Show(Sushi foodData, NPCOrderView targetOrder)
    {
        Show(foodData);
        BindOrderTarget(targetOrder);
    }
#endif

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 음식 아이콘의 표시 여부만 변경합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetDiscardFoodImage(bool isOn)
    {
        if (_foodImage != null)
        {
            _foodImage.enabled = isOn;
        }
    }

    /// <summary>
    /// 기존 Sprite 전달 방식을 유지하며 이전 요리 데이터 참조를 비웁니다.
    /// </summary>
    /// <param name="sprite">스프라이트 이미지입니다.</param>
    public void SetFoodImageSprite(Sprite sprite)
    {
        ClearOrderTarget();
        SetDiscardImage(false);
        _foodData = null;
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_foodImage == null)
        {
            return;
        }

        _foodImage.sprite = sprite;
        _foodImage.color = sprite != null ? Color.white : Color.clear;
    }

    /// <summary>
    /// 상호작용 키 이미지와 키 문구를 함께 켜거나 끕니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetKeyImage(bool isOn)
    {
        if (_KeyImage != null)
        {
            _KeyImage.enabled = isOn;
            _KeyImage.gameObject.SetActive(isOn);
        }

        if (_keyText != null)
        {
            _keyText.enabled = isOn;
        }
    }

    /// <summary>
    /// 폐기 필요 여부를 저장하고, 안내가 보이는 동안에만 휴지통 아이콘을 표시합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetDiscardImage(bool isOn)
    {
        _manualDiscardRequired = isOn;
        RefreshDiscardImage();
    }

    /// <summary>담당 로직이 전달한 폐기 가능 여부와 실제 2초 진행 비율만 표시합니다. 입력·시간·폐기 판정은 하지 않습니다.</summary>
    public void DisplayDiscardProgress(bool available, bool holding, float progress, string keyLabel = null)
    {
        progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
        _wasDiscarding = available && holding;
        SetDiscardImage(available);
        bool visible = available && _isVisible;
        if (_discardGaugeRoot != null) _discardGaugeRoot.SetActive(visible && holding);
        if (_discardGaugeImage != null) _discardGaugeImage.fillAmount = visible && holding ? progress : 0f;
        if (_discardNoticeText != null)
        {
            _discardNoticeText.gameObject.SetActive(visible);
            _discardNoticeText.text = holding ? "음식 버리는 중" : "2초간 눌러 버리기";
        }
        if (visible && !string.IsNullOrEmpty(keyLabel)) SetInteractionKey(keyLabel);
        BubblePresentation?.Present(_isVisible,_foodImage!=null?_foodImage.sprite:null,false,visible&&holding,visible,progress,
            string.IsNullOrEmpty(keyLabel)?DiscardKeyLabel():keyLabel);
    }

    /// <summary>키 재설정이 있으면 실제 폐기 액션의 현재 키 이름을 표시합니다. 입력은 소비하지 않습니다.</summary>
    string DiscardKeyLabel()
    {
        var action = _discardAction != null ? _discardAction.action : null;
        if (action != null)
            for (int i = 0; i < action.bindings.Count; i++)
                if (!action.bindings[i].isPartOfComposite && action.bindings[i].effectivePath != null &&
                    action.bindings[i].effectivePath.Contains("<Keyboard>")) return action.GetBindingDisplayString(i);
        return "G";
    }

    /// <summary>실제 폐기 성공 통보를 받으면 음식 아이콘이 아래로 떨어지며 사라집니다. 실제 음식 데이터는 변경하지 않습니다.</summary>
    public void PlayDiscardEffect(Sprite icon)
    {
        if (icon == null || _foodImage == null || !isActiveAndEnabled) return;
        ClearDiscardEffect();
        var go = new GameObject("FoodDiscardEffect", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.layer = gameObject.layer;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = _foodImage.rectTransform.rect.size;
        rect.position = _foodImage.rectTransform.TransformPoint(_foodImage.rectTransform.rect.center);
        var sourceScale = _foodImage.transform.lossyScale; var parentScale = transform.lossyScale;
        rect.localScale = new Vector3(sourceScale.x / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            sourceScale.y / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1f);
        _discardEffectImage = go.GetComponent<UnityEngine.UI.Image>();
        _discardEffectImage.sprite = icon; _discardEffectImage.color = Color.white;
        _discardEffectImage.preserveAspect = true; _discardEffectImage.raycastTarget = false;
        float duration = Mathf.Max(.01f, _discardEffectDuration);
        _discardEffect = DOTween.Sequence();
        _discardEffect.Join(rect.DOAnchorPos(rect.anchoredPosition + new Vector2(-22f, -48f), duration).SetEase(Ease.InQuad));
        _discardEffect.Join(rect.DOLocalRotate(new Vector3(0, 0, -25f), duration).SetEase(Ease.OutSine));
        _discardEffect.Join(_discardEffectImage.DOFade(0f, duration));
        _discardEffect.OnComplete(ClearDiscardEffect);
    }

    /// <summary>완료·비활성화 시 이 View가 만든 폐기 연출과 복제 아이콘만 정리합니다.</summary>
    void ClearDiscardEffect()
    {
        var sequence = _discardEffect; _discardEffect = null;
        if (sequence != null && sequence.IsActive()) sequence.Kill(false);
        if (_discardEffectImage != null) Destroy(_discardEffectImage.gameObject);
        _discardEffectImage = null;
    }

    /// <summary>
    /// 확정된 입력 설정의 키 문구를 전달받아 표시합니다. 실제 입력 처리는 하지 않습니다.
    /// </summary>
    /// <param name="label">문구 값입니다.</param>
    public void SetInteractionKey(string label)
    {
        if (_keyText != null)
        {
            _keyText.text = label ?? string.Empty;
        }
    }

    /// <summary>
    /// 현재 주문에 대한 취소 상태만 읽고, 가까운 동안에만 휴지통 표시를 켭니다.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void RefreshOrderTarget()
    {
#if UNITY_EDITOR
        if (_targetOrder != null && _targetOrder.OrderVersion == _targetOrderVersion)
        {
            _targetPosition = _targetOrder.DetectionPosition;
            _targetRadius = _targetOrder.DetectionRadius;
        }

        bool near = _useProximityPreview ? _previewNearby : (InteractorPosition - _targetPosition).sqrMagnitude <= Mathf.Max(0f, _targetRadius) * Mathf.Max(0f, _targetRadius); // 근처 여부입니다.
        _proximityDiscardRequired = _isVisible && _foodData != null && _targetCanceled && near;
        RefreshDiscardImage();
#endif
    }

    /// <summary>
    /// 직접 전달받은 폐기 상태와 대상 주문의 근접 안내를 합쳐 표시합니다.
    /// </summary>
    void RefreshDiscardImage()
    {
        _requiresDiscard = _manualDiscardRequired;
#if UNITY_EDITOR
        _requiresDiscard |= _proximityDiscardRequired;
#endif
        if (_DiscardImage != null)
        {
            _DiscardImage.gameObject.SetActive(_isVisible && _requiresDiscard);
        }
    }

    /// <summary>
    /// 이전에 구독한 주문과 근접 테스트 상태를 정리합니다.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void ClearOrderTarget()
    {
#if UNITY_EDITOR
        if (_targetOrder != null)
        {
            _targetOrder.OnOrderCanceled -= HandleOrderCanceled;
        }

        _targetOrder = null;
        _targetOrderVersion = 0;
        _targetCanceled = false;
        _proximityDiscardRequired = false;
        _useProximityPreview = false;
        RefreshDiscardImage();
#endif
    }

    #endregion

    #region 표시 데이터 및 보조 기능

#if UNITY_EDITOR
    /// <summary>
    /// 담당 로직이 판단한 주문 취소 여부만 표시합니다. 만족도나 주문 상태는 계산하지 않습니다.
    /// </summary>
    /// <param name="canceled">취소 여부입니다.</param>
    public void SetOrderCanceled(bool canceled)
    {
        SetDiscardImage(canceled);
    }

    /// <summary>
    /// 이 운반 요리와 정확히 연결된 손님·회차의 취소 통보만 보관합니다.
    /// </summary>
    /// <param name="order">주문 표시를 연결할 뷰입니다.</param>
    /// <param name="version">회차 값입니다.</param>
    void HandleOrderCanceled(NPCOrderView order, int version)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (order != _targetOrder || version != _targetOrderVersion || _foodData == null || order.OrderData != _foodData)
        {
            return;
        }

        _targetCanceled = true;
        _targetPosition = order.DetectionPosition;
        _targetRadius = order.DetectionRadius;
        RefreshOrderTarget();
    }
#endif

    #endregion

    #region 초기화 및 연결

#if UNITY_EDITOR
    /// <summary>
    /// 같은 메뉴의 다른 손님과 혼동하지 않도록 대상 주문을 명시적으로 연결합니다.
    /// </summary>
    /// <param name="targetOrder">대상 주문 표시를 연결할 뷰입니다.</param>
    public void BindOrderTarget(NPCOrderView targetOrder)
    {
        ClearOrderTarget();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (targetOrder == null || _foodData == null)
        {
            return;
        }

        _targetOrder = targetOrder;
        _targetOrderVersion = targetOrder.OrderVersion;
        _targetPosition = targetOrder.DetectionPosition;
        _targetRadius = targetOrder.DetectionRadius;
        targetOrder.OnOrderCanceled += HandleOrderCanceled;
        if (targetOrder.IsCanceled)
        {
            HandleOrderCanceled(targetOrder, _targetOrderVersion);
        }

        RefreshOrderTarget();
    }
#endif

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트

    /// <summary>
    /// 대상 주문이 연결된 상태에서만 근접 여부를 모의 표시합니다. 실제 주체를 이동시키지 않습니다.
    /// </summary>
    /// <param name="nearby">근처 여부입니다.</param>
    public void SetProximityPreview(bool nearby)
    {
        _useProximityPreview = true;
        _previewNearby = nearby;
        RefreshOrderTarget();
    }

    /// <summary>
    /// 근접 모의 표시를 끝내고 실제 월드 거리로 안내를 갱신합니다.
    /// </summary>
    public void ClearProximityPreview()
    {
        _useProximityPreview = false;
        RefreshOrderTarget();
    }

    /// <summary>
    /// 요리를 수령해 서빙하는 상태로 표시하며 폐기 아이콘은 숨깁니다.
    /// </summary>
    [ContextMenu("테스트/요리 수령·서빙")]
    public void TestServing()
    {
        Show(_testFoodData);
    }

    /// <summary>
    /// 취소된 요리를 운반하는 상태로 표시하고 폐기 아이콘을 켭니다.
    /// </summary>
    [ContextMenu("테스트/폐기할 요리")]
    public void TestDiscard()
    {
        Show(_testFoodData, true);
    }

    /// <summary>
    /// 서빙 중 외부에서 주문 취소 통보를 받은 상황을 표시합니다.
    /// </summary>
    [ContextMenu("테스트/서빙 중 주문 취소")]
    public void TestOrderCanceled()
    {
        TestServing();
        SetOrderCanceled(true);
    }

    /// <summary>
    /// 폐기 표시 후 새 요리를 수령해 정상 서빙 상태로 돌아가는 상황을 표시합니다.
    /// </summary>
    [ContextMenu("테스트/폐기 상태에서 일반 서빙 복귀")]
    public void TestRestoreServing()
    {
        TestDiscard();
        TestServing();
    }

    /// <summary>
    /// 서빙 또는 폐기 완료 통보를 받은 상황으로 안내 전체를 숨깁니다.
    /// </summary>
    [ContextMenu("테스트/서빙 혹은 처분 완료")]
    public void TestClear()
    {
        Hide();
    }

    #endregion
#endif

}
