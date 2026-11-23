using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 손님의 음식·음료 주문과 대기 만족도를 표시합니다. 실제 주문 취소와 서빙 처리는 하지 않습니다.
/// </summary>
public class NPCOrderView : MonoBehaviour
{
    // 에디터 전용 영역은 기존 미리보기 테스트를 유지하며 실제 게임 빌드에서는 제외합니다.

    // 주문 데이터는 유지하고 투명 여백을 제외한 그림에 사방15% 여백을 적용합니다.
    static readonly System.Collections.Generic.Dictionary<string,Rect> OrderIconBounds = new()
    {
        ["EightbandedButterflyfish_Nigiri"]=new Rect(54,74,148,106),
        ["MantaRay_Fry"]=new Rect(26,60,212,144),
        ["SkunkTilefish_Nigiri"]=new Rect(56,88,151,85),
        ["WhaleShark_Nigiri"]=new Rect(45,67,166,119),
        ["RedSeahorse_Nigiri"]=new Rect(54,77,148,100),
        ["BlueTangNigiri"]=new Rect(61,88,131,83),
        ["Watanabe_Nigiri"]=new Rect(57,81,142,96),
        ["IndusRiverDolphin_Nigiri"]=new Rect(63,79,143,96),
        ["BlueTang_Gunkan"]=new Rect(64,81,128,99),
        ["BlueTang_Grilled"]=new Rect(67,57,120,143),
        ["IndusRiverDolphin_Grilled"]=new Rect(67,59,123,142),
        ["IndusRiverDolphin_Gunkan"]=new Rect(64,69,128,116),
        ["EightbandedButterflyfish_Grilled"]=new Rect(58,58,139,142),
        ["RedSeaHorse_Grilled"]=new Rect(63,45,130,169),
        ["MantaRay_Nigiri"]=new Rect(31,67,195,121),
        ["WhaleShark_Fry"]=new Rect(23,55,205,163),
        ["WhaleShark_Grilled"]=new Rect(19,84,215,120),
        ["WhaleShark_Stake"]=new Rect(39,78,186,118),
        ["SkunkTileFish_Gunkan"]=new Rect(66,77,124,103),
        ["SkunkTileFish_Roll"]=new Rect(75,68,106,115),
        ["SkunkTileFish_Grilled"]=new Rect(60,59,134,136),
        ["Watanabe_Grilled"]=new Rect(65,58,124,141),
        ["Eel_Fry_Lit"]=new Rect(75,186,490,293),
        ["Eel_Skewer_Lit"]=new Rect(85,236,476,167),
        ["CucumberPlate"]=new Rect(32,69,187,106),
        ["Tea"]=new Rect(54,37,148,173),
        ["BeerMug"]=new Rect(45,21,147,219),
    };
    bool _roundedGaugeReady;
    float _guideGaugeWidth, _guideGaugeHeight;

    void FitOrderIcon()
    {
        if(_OrderImage==null||_OrderImage.sprite==null||_orderBackground==null)return;
        var sprite=_OrderImage.sprite;var box=_orderBackground.rectTransform;
        if(!OrderIconBounds.TryGetValue(sprite.name,out var pixels))pixels=new Rect(0,0,sprite.rect.width,sprite.rect.height);
        float scale=Mathf.Min(box.rect.width*.7f/pixels.width,box.rect.height*.7f/pixels.height);
        var r=_OrderImage.rectTransform;r.anchorMin=box.anchorMin;r.anchorMax=box.anchorMax;r.pivot=new Vector2(.5f,.5f);
        r.anchoredPosition=box.anchoredPosition+new Vector2((sprite.rect.width*.5f-pixels.center.x)*scale,(pixels.center.y-sprite.rect.height*.5f)*scale);
        r.sizeDelta=sprite.rect.size*scale;_OrderImage.preserveAspect=true;_OrderImage.type=Image.Type.Simple;
    }

    void ApplyGuideSatisfactionLayout()
    {
        if(_satisfactionGaugeRoot==null||_orderBackground==null||_satisFactionImage==null)return;
        var track=_satisfactionGaugeRoot.transform.Find("SatisfactionTrack")?.GetComponent<Image>();
        var style=FindFirstObjectByType<SushiRestaurantStyleView>(FindObjectsInactive.Include);
        var rounded=style!=null?style.RoundedPanelSprite:null;
        if(track==null||rounded==null)return;
        float unit=_orderBackground.rectTransform.rect.width/110f;
        _guideGaugeWidth=96f*unit;_guideGaugeHeight=20f*unit;
        float y=_orderBackground.rectTransform.anchoredPosition.y-50f*unit;
        var tr=track.rectTransform;tr.anchorMin=tr.anchorMax=new Vector2(.5f,0);tr.pivot=new Vector2(.5f,.5f);
        tr.anchoredPosition=new Vector2(5f*unit,y);tr.sizeDelta=new Vector2(_guideGaugeWidth,_guideGaugeHeight);
        track.sprite=rounded;track.type=Image.Type.Sliced;track.fillCenter=true;track.preserveAspect=false;
        track.pixelsPerUnitMultiplier=22f/(10f*unit);track.color=new Color32(107,57,52,255);
        var fr=_satisFactionImage.rectTransform;fr.anchorMin=fr.anchorMax=new Vector2(.5f,0);fr.pivot=new Vector2(0,.5f);
        fr.anchoredPosition=new Vector2(-43f*unit,y);
        _satisFactionImage.sprite=rounded;_satisFactionImage.type=Image.Type.Sliced;_satisFactionImage.fillCenter=true;_satisFactionImage.preserveAspect=false;
        _satisFactionImage.pixelsPerUnitMultiplier=22f/(10f*unit);_satisFactionImage.color=new Color32(173,246,77,255);
        var outline=_satisfactionGaugeRoot.transform.Find("SatisfactionOutline");if(outline!=null)outline.gameObject.SetActive(false);
        if(_satisfactionStatusImage!=null)
        {
            var face=_satisfactionStatusImage.rectTransform;face.anchorMin=face.anchorMax=new Vector2(.5f,0);face.pivot=new Vector2(.5f,.5f);
            face.anchoredPosition=new Vector2(-54f*unit,y);face.sizeDelta=Vector2.one*46f*unit;
        }
        _roundedGaugeReady=true;
    }

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("상호작용 이미지")]
    [SerializeField] Image _OrderImage; // OrderIcon 음식·음료 이미지를 연결합니다.
    [SerializeField] Image _satisFactionImage; // 남은 만족도를 표시하는 연속 게이지입니다. Filled / Horizontal / Left입니다.

    [Header("만족도 게이지 및 임시 단계 표시")]
    [SerializeField] GameObject _satisfactionGaugeRoot;
    [SerializeField] UnityEngine.UI.Image _orderBackground;
    [SerializeField] UnityEngine.UI.Image _satisfactionStatusImage;
    [SerializeField] Color _highSatisfactionBackground = new Color(.98f, .89f, .79f);
    [SerializeField] Color _lowSatisfactionBackground = new Color(.60f, .36f, .30f);

    [SerializeField] Sprite _happyFace, _neutralFace, _angryFace;
    [SerializeField] Sprite _normalOrderSprite, _angryOrderSprite;
    // 게이지는 연속으로 표시하고 단계 경계는 표정에만 사용합니다.
    void RefreshSatisfactionPresentation()
    {
        if (_satisFactionImage != null)
        {
            if(_roundedGaugeReady)
            {
                // Filled의 가로 자르기 대신 둥근9-slice 자체의 폭을 변경해 양끝 곡률을 보존합니다.
                _satisFactionImage.rectTransform.sizeDelta=new Vector2(_guideGaugeWidth*_satisfactionRatio,_guideGaugeHeight);
                _satisFactionImage.enabled=_satisfactionRatio>0f;
            }
            else _satisFactionImage.fillAmount = _satisfactionGaugeRoot != null ? _satisfactionRatio : 1f - _satisfactionRatio;
        }
        if (_orderBackground != null)
        {
            var sprite=_satisfactionRatio<=1f/3f?_angryOrderSprite:_normalOrderSprite;
            if(sprite!=null){_orderBackground.sprite=sprite;_orderBackground.color=Color.white;}
            else _orderBackground.color=Color.Lerp(_highSatisfactionBackground,_lowSatisfactionBackground,1f-_satisfactionRatio);
        }
        if (_satisfactionStatusImage != null)
        {
            var face=_satisfactionRatio>2f/3f?_happyFace:_satisfactionRatio>1f/3f?_neutralFace:_angryFace;
            if(face!=null){_satisfactionStatusImage.sprite=face;_satisfactionStatusImage.color=Color.white;}
        }
    }

    [Header("상호작용 키")]
    [SerializeField] Image _KeyImage; // 말풍선 아래 KeyIcon 이미지를 연결합니다.
    [SerializeField] TMP_Text _keyText; // KeyIcon의 키 안내 텍스트를 연결합니다.

    #endregion

    #region 코루틴

#if UNITY_EDITOR
    Coroutine _watingRoutine; // 에디터의 가상 주문 대기에서만 사용합니다.
#endif

    #endregion

    #region UI 연결 및 표시 설정

    // 말풍선 배경·주문·게이지·키 안내를 포함하는 DisplayRoot를 연결합니다.
    [SerializeField] GameObject _displayRoot;
    // 실제 손님 상태를 읽을 GuestNPC입니다. 미연결이면 시작할 때 부모에서 찾고, 없으면 외부 표시 함수만 사용합니다.
    [SerializeField] GuestNPC _guest;
    // 근처 운반 대상에게 취소된 주문의 폐기를 안내할 월드 거리입니다.
#if UNITY_EDITOR
    [SerializeField, Min(0f)] float _detectionRadius = 2f;
#endif

    #endregion

    #region 결제 금액 연출

    [Header("손님 결제 금액")]
    [SerializeField] RectTransform _paymentRoot;
    [SerializeField] CanvasGroup _paymentCanvasGroup;
    [SerializeField] TMP_Text _paymentText;
    [SerializeField, Min(.1f)] float _paymentDuration = 1.25f;
    [SerializeField, Min(0f)] float _paymentRise = 35f;
    Sequence _paymentSequence;
    Vector2 _paymentOrigin;
    bool _paymentOriginCached;
    bool _wasGuestEating;

    #endregion

    #region 런타임 상태

    Sushi _orderData; // 주문 데이터 값입니다.
    // _isVisible: 노출 여부입니다.
    // _isWaiting: 대기 여부입니다.
    // _isCanceled: 취소 여부입니다.
    // _wasGuestWaiting: 이전 손님 대기 여부입니다.
    bool _isVisible, _isWaiting, _isCanceled, _wasGuestWaiting;
    float _satisfactionRatio = 1f; // 만족도 비율 값입니다.
#if UNITY_EDITOR
    float _waitingDuration; // 대기 지속 시간 값입니다.
#endif
#if UNITY_EDITOR
    int _orderVersion; // 주문 회차 값입니다.
#endif

    #endregion

    #region 이벤트 액션

    /// <summary>
    /// 취소된 표시의 손님과 주문 회차를 알립니다. 실제 주문 데이터는 변경하지 않습니다.
    /// </summary>
#if UNITY_EDITOR
    public event System.Action<NPCOrderView, int> OnOrderCanceled;
#endif

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트 멤버

    // 음식 주문 테스트에서 읽을 Sushi 에셋입니다. 원본 데이터는 변경하지 않습니다.
    [SerializeField] Sushi _testOrderData;
    // 음료 주문 테스트에서 읽을 Sushi 에셋입니다.
    [SerializeField] Sushi _testDrinkData;
    // 대기 진행 테스트 시간(초)입니다. 실제 손님의 대기시간에는 사용하지 않습니다.
    [SerializeField, Min(0f)] float _testWaitingSeconds = 5f;
    // 근접 폐기 테스트에 사용할 서빙 View를 현재 씬에서 연결합니다. 원본 프리팹에서는 비워둡니다.
    [SerializeField] InteractToGuestView _testServingView;
    bool _previewMode; // 미리보기 모드 여부입니다.

    #endregion
#endif

    #region 프로퍼티

    /// <summary>
    /// 현재 표시 중인 주문 요리 데이터를 반환합니다.
    /// </summary>
    public Sushi OrderData => _orderData;

    /// <summary>
    /// 현재 주문 안내의 표시 여부를 반환합니다.
    /// </summary>
    public bool IsVisible => _isVisible;

    /// <summary>
    /// 현재 주문의 취소 표시 상태를 반환합니다.
    /// </summary>
    public bool IsCanceled => _isCanceled;

    /// <summary>
    /// 남은 만족도 비율입니다. 1은 충분함, 0은 대기 종료입니다.
    /// </summary>
    public float SatisfactionRatio => _satisfactionRatio;

    /// <summary>
    /// 같은 손님의 다음 주문을 구분하는 View 내부 회차입니다.
    /// </summary>
#if UNITY_EDITOR
    public int OrderVersion => _orderVersion;
#endif

    /// <summary>
    /// 운반 대상 근접 안내 거리입니다.
    /// </summary>
#if UNITY_EDITOR
    public float DetectionRadius => _detectionRadius;
#endif

    /// <summary>
    /// Canvas 높이가 아닌 손님 몸체를 기준으로 근접 거리를 측정합니다.
    /// </summary>
#if UNITY_EDITOR
    public Vector3 DetectionPosition => _guest != null ? _guest.transform.position : transform.parent != null ? transform.parent.position : transform.position;
#endif

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 부모 손님을 찾고 주문 표시를 초기화합니다.
    /// </summary>
    void Awake()
    {
        if (_guest == null)
        {
            _guest = GetComponentInParent<GuestNPC>();
        }

        ApplyGuideSatisfactionLayout();
        ResetServingPayment();
        Hide();
    }

    /// <summary>
    /// 실제 손님이 연결되면 해당 주문·만족도를 읽어 표시합니다.
    /// </summary>
    /// <summary>Canvas를 다시 켰을 때 비활성화 동안 놓친 현재 표시를 한 번 복원합니다.</summary>
    void OnEnable() { RefreshFromModel(); }

    bool _pollModelState = true; // TODO: 담당자의 상태·만족도 알림 연결 후 외부 갱신 모드로 전환합니다.
    /// <summary>필요한 모든 모델 알림이 연결되면 임시 상태 조회를 끄고 외부 갱신을 사용합니다.</summary>
    public void SetExternalUpdates(bool enabled) { _pollModelState = !enabled; RefreshFromModel(); }
    /// <summary>Presenter가 손님 상태 변경 알림을 받았을 때 현재 표시만 반영합니다.</summary>
    public void RefreshFromModel()
    {
        if (!isActiveAndEnabled || _guest == null) return;
#if UNITY_EDITOR
        if (_previewMode) return;
#endif
        RefreshGuestDisplay();
    }
    void LateUpdate()
    {
        if (_guest != null)
        {
#if UNITY_EDITOR
            if (_previewMode) return;
#endif
            // TODO(UI-EVENT-07 / 손님 상태 임시 조회): GuestNPC.DisplayStateChanged(guest) 알림이 필요합니다(추가 제안).
            // 연결할 Presenter 함수: NotifyGuestDisplayChanged(guest). 주문·대기·식사·퇴장, 메뉴·만족도·대기 수치·재사용 초기화를 알립니다.
            // 모두 연결한 뒤 SetGuestNotificationsEnabled(guest, true)로 끄고 검증 후 이 조회만 제거합니다. 기존 OrderStarted 업무 구독은 유지합니다.
            // OnEnable/BindGuest의 최초 상태 복원과 아래 카메라 방향 정렬은 임시 조회가 아니므로 유지합니다.
            if (_pollModelState) RefreshFromModel();
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }

    /// <summary>
    /// 손님이나 Canvas가 꺼질 때 대기 연출과 UI를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        ResetServingPayment();
        Hide();
    }

    #endregion

    #region UI 표시 및 갱신


#if UNITY_EDITOR
    /// <summary>
    /// 요리 데이터와 별도의 대기 시간을 받아 UI 연출만 시작합니다.
    /// </summary>
    /// <param name="orderData">주문 데이터 값입니다.</param>
    /// <param name="waitingSeconds">대기 초 값입니다.</param>
    public void StartWaiting(Sushi orderData, float waitingSeconds)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (orderData == null)
        {
            Hide();
            return;
        }

        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        BeginOrder(orderData);
        BeginWaiting(waitingSeconds);
    }

    /// <summary>
    /// 이전 연출을 중지하고 새 대기 시간을 적용합니다.
    /// </summary>
    /// <param name="waitingSeconds">대기 초 값입니다.</param>
    void BeginWaiting(float waitingSeconds)
    {
        ShowWaiting();
        _waitingDuration = float.IsNaN(waitingSeconds) || float.IsInfinity(waitingSeconds) ? 0f : Mathf.Max(0f, waitingSeconds);
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_waitingDuration <= 0f)
        {
            CancelOrderDisplay();
            return;
        }

        _watingRoutine = StartCoroutine(WaitingRoutine());
    }

    /// <summary>
    /// 유한한 대기 시간 동안 만족도가 줄고 덮개가 차오르도록 표시합니다.
    /// </summary>
    IEnumerator WaitingRoutine()
    {
        float timer = 0f; // 타이머 값입니다.
        while (timer < _waitingDuration)
        {
            yield return null;
            timer += Time.deltaTime;
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (timer >= _waitingDuration)
            {
                break;
            }

            UpdateSatisFaction(1f - timer / _waitingDuration);
        }

        _watingRoutine = null;
        CancelOrderDisplay();
    }
#endif

    /// <summary>
    /// 새 주문 회차를 시작하고 이전 취소 상태를 해제합니다.
    /// </summary>
    /// <param name="orderData">주문 데이터 값입니다.</param>
    void BeginOrder(Sushi orderData)
    {
        StopWaitingRoutine();
        _orderData = orderData;
#if UNITY_EDITOR
        _orderVersion++;
#endif
        _isCanceled = false;
        _satisfactionRatio = 1f;
        if (orderData != null)
        {
            SetOrderImageSprite(IjakayaView.ResolveMenuIcon(orderData, this));
        }
    }

    /// <summary>
    /// 주문 아이콘의 표시만 변경합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetOrderImage(bool isOn)
    {
        if (_OrderImage != null)
        {
            _OrderImage.enabled = isOn;
        }
    }

    /// <summary>
    /// 주문 위에 겹치는 만족도 덮개의 표시만 변경합니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void SetSatisFactionImage(bool isOn)
    {
        if (_satisfactionGaugeRoot != null) _satisfactionGaugeRoot.SetActive(isOn);
        if (_satisFactionImage != null)
        {
            _satisFactionImage.enabled = isOn;
        }
    }

    /// <summary>
    /// 음식·음료 주문 아이콘을 지정합니다.
    /// </summary>
    /// <param name="sprite">스프라이트 이미지입니다.</param>
    public void SetOrderImageSprite(Sprite sprite)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_OrderImage == null)
        {
            return;
        }

        _OrderImage.sprite = sprite;
        FitOrderIcon();
        if (_satisFactionImage != null && _satisfactionGaugeRoot == null)
        {
            _satisFactionImage.sprite = sprite;
            _satisFactionImage.preserveAspect = true;
        }

        _OrderImage.color = sprite != null ? Color.white : Color.clear;
    }

    /// <summary>
    /// 남은 만족도(0~1)를 연속 게이지와 배경색에 반영하며 0이면 취소 표시합니다.
    /// </summary>
    /// <param name="satisFaction">만족도 값입니다.</param>
    public void UpdateSatisFaction(float satisFaction)
    {
        _satisfactionRatio = _isCanceled || float.IsNaN(satisFaction) ? 0f : Mathf.Clamp01(satisFaction);
        RefreshSatisfactionPresentation();

        if (_isVisible && _isWaiting && _satisfactionRatio <= 0f)
        {
            CancelOrderDisplay();
        }
    }

    /// <summary>
    /// 키 안내 전체를 켜거나 끕니다. 실제 키 입력은 처리하지 않습니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    void HideLegacyKeyIndicator()
    {
        if (_KeyImage != null)
        {
            _KeyImage.enabled = false;
            _KeyImage.gameObject.SetActive(false);
        }

        if (_keyText != null)
        {
            _keyText.enabled = false;
        }
    }

    /// <summary>
    /// 실제 손님의 공개 상태만 읽어 UI를 갱신하며 내부 로직은 호출하지 않습니다.
    /// </summary>
    void RefreshGuestDisplay()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_guest == null || !_guest.isActiveAndEnabled)
        {
            ResetServingPayment();
            Hide();
            return;
        }

        // 기존 상태 갱신 경로를 이용해 식사로 전환될 때만 확정 결제 금액을 표시합니다.
        // 금액 표시는 보상을 변경하지 않으며 동전·하트는 기존 파티클 연출을 재사용합니다.
        bool eating = _guest.IsEating;
        if (eating && !_wasGuestEating) ShowServingPayment(_guest.ServedSaleGold);
        _wasGuestEating = eating;

        var data = _guest.SelectedSushi; // 데이터 값입니다.
        // 주문 동작이 끝난 뒤 Waiting으로 전환되면 음식 아이콘을 처음 표시합니다.
        // 별도 타이머를 만들지 않아 손님에게 설정된 실제 Ordering 시간을 그대로 따릅니다.
        if (_guest.IsOrdering)
        {
            if (_isVisible) Hide();
            _wasGuestWaiting = false;
        }
        else if (_guest.IsWaiting && data != null)
        {
            BindOrder(data, _guest.SatisfactionRatio);
            _wasGuestWaiting = true;
        }
        else if (data != null && (_wasGuestWaiting || _isCanceled) && _guest.SatisfactionRatio <= 0f && (_guest.State == GuestState.LeavingIdle || _guest.State == GuestState.Leaving))
        {
            CancelOrderDisplay();
            // 취소 상태와 통보는 유지하되 떠나는 손님에게 오래된 요리 아이콘을 붙여 두지 않습니다.
            if (_displayRoot != null)
            {
                var effect = GetComponent<UIVisibilityEffectView>();
                if (effect != null) effect.Hide(); else _displayRoot.SetActive(false);
            }
        }
        else if (_isVisible)
        {
            Hide();
        }
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 실제 지급된 판매금액을 한 번 띄우고 위로 움직이며 서서히 숨깁니다.
    /// 동전·하트 파티클과 실제 골드 지급은 기존 GuestNPC가 담당합니다.
    /// </summary>
    public void ShowServingPayment(float paidGold)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _paymentRoot == null ||
            _paymentCanvasGroup == null || _paymentText == null ||
            float.IsNaN(paidGold) || float.IsInfinity(paidGold) || paidGold <= 0f) return;

        CachePaymentOrigin();
        _paymentSequence?.Kill();
        _paymentRoot.anchoredPosition = _paymentOrigin;
        _paymentText.text = "+" + paidGold.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture);
        _paymentRoot.gameObject.SetActive(true);
        _paymentCanvasGroup.alpha = 1f;

        float duration = Mathf.Max(.1f, _paymentDuration);
        var sequence = DOTween.Sequence();
        _paymentSequence = sequence;
        sequence.Append(_paymentRoot.DOAnchorPosY(_paymentOrigin.y + _paymentRise, duration).SetEase(Ease.OutSine));
        sequence.Insert(duration * .35f, _paymentCanvasGroup.DOFade(0f, duration * .65f).SetEase(Ease.InSine));
        sequence.OnComplete(() =>
        {
            _paymentSequence = null;
            if (_paymentRoot != null) _paymentRoot.gameObject.SetActive(false);
        });
    }

    /// <summary>처음 연결된 결제 UI 위치를 기억하여 반복 재생 때 위치가 누적되지 않게 합니다.</summary>
    void CachePaymentOrigin()
    {
        if (_paymentOriginCached || _paymentRoot == null) return;
        _paymentOrigin = _paymentRoot.anchoredPosition;
        _paymentOriginCached = true;
    }

    /// <summary>연결 해제·풀 반환 때 이 View가 만든 결제 연출과 식사 전환 기록만 초기화합니다.</summary>
    void ResetServingPayment()
    {
        _paymentSequence?.Kill();
        _paymentSequence = null;
        _wasGuestEating = false;
        CachePaymentOrigin();
        if (_paymentRoot != null)
        {
            _paymentRoot.anchoredPosition = _paymentOrigin;
            _paymentRoot.gameObject.SetActive(false);
        }
        if (_paymentCanvasGroup != null) _paymentCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 이 View가 시작한 대기 코루틴만 중지합니다.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void StopWaitingRoutine()
    {
#if UNITY_EDITOR
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_watingRoutine == null)
        {
            return;
        }

        StopCoroutine(_watingRoutine);
        _watingRoutine = null;
#endif
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 주문 이미지만 표시하고 이전 게이지·키 안내는 숨깁니다.
    /// </summary>
    public void ShowOrder()
    {
        StopWaitingRoutine();
        _isVisible = true;
        _isWaiting = false;
        _isCanceled = false;
        if (_displayRoot != null)
        {
            _displayRoot.SetActive(true);
            GetComponent<UIVisibilityEffectView>()?.Show();
        }

        _satisfactionRatio = 1f;
        RefreshSatisfactionPresentation();
        SetOrderImage(true);
        SetSatisFactionImage(false);
        HideLegacyKeyIndicator();
    }

    /// <summary>
    /// 주문 대기를 표시하고 만족도를 충분한 상태로 초기화합니다.
    /// </summary>
    public void ShowWaiting()
    {
        _isVisible = true;
        _isWaiting = true;
        _isCanceled = false;
        if (_displayRoot != null)
        {
            _displayRoot.SetActive(true);
            GetComponent<UIVisibilityEffectView>()?.Show();
        }

        SetOrderImage(true);
        SetSatisFactionImage(true);
        HideLegacyKeyIndicator();
        UpdateSatisFaction(1f);
    }

    /// <summary>
    /// 전체 안내·코루틴·주문 참조를 정리합니다.
    /// </summary>
    public void Hide()
    {
        var effect = GetComponent<UIVisibilityEffectView>();
        if (effect != null && effect.HideAfter(ClearOrderPresentation)) return;
        ClearOrderPresentation();
    }

    void ClearOrderPresentation()
    {
        StopWaitingRoutine();
        _isVisible = _isWaiting = _isCanceled = _wasGuestWaiting = false;
        _satisfactionRatio = 1f;
        RefreshSatisfactionPresentation();
        _orderData = null;
        SetOrderImage(false);
        SetSatisFactionImage(false);
        HideLegacyKeyIndicator();
        if (_satisFactionImage != null)
        {
            _satisFactionImage.fillAmount = 0f;
        }

        if (_OrderImage != null)
        {
            _OrderImage.sprite = null;
            _OrderImage.color = Color.clear;
        }

        if (_displayRoot != null)
        {
            _displayRoot.SetActive(false);
        }
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 주문을 취소된 모습으로 표시하고 연결된 운반 View에 한 번 알립니다.
    /// </summary>
    public void CancelOrderDisplay()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_isVisible)
        {
            return;
        }

        StopWaitingRoutine();
#if UNITY_EDITOR
        bool notify = !_isCanceled; // 테스트용 운반 UI 통보 여부입니다.
#endif
        _isCanceled = true;
        _isWaiting = false;
        _satisfactionRatio = 0f;
        SetSatisFactionImage(true);
        RefreshSatisfactionPresentation();

        HideLegacyKeyIndicator();
#if UNITY_EDITOR
        if (notify)
        {
            OnOrderCanceled?.Invoke(this, _orderVersion);
        }
#endif
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 담당 로직에서 주문과 남은 만족도·취소 여부를 전달해 표시합니다.
    /// </summary>
    /// <param name="orderData">주문 데이터 값입니다.</param>
    /// <param name="satisfactionRatio">만족도 비율 값입니다.</param>
    /// <param name="canceled">취소 여부입니다.</param>
    public void BindOrder(Sushi orderData, float satisfactionRatio, bool canceled = false)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (orderData == null)
        {
            Hide();
            return;
        }

        StopWaitingRoutine();
        if (!_isVisible || _orderData != orderData || (_isCanceled && !canceled && satisfactionRatio > 0f))
        {
            BeginOrder(orderData);
        }

        _isVisible = true;
        _isWaiting = !canceled && !_isCanceled;
        if (_displayRoot != null)
        {
            _displayRoot.SetActive(true);
            GetComponent<UIVisibilityEffectView>()?.Show();
        }

        SetOrderImage(true);
        SetSatisFactionImage(true);
        HideLegacyKeyIndicator();
        UpdateSatisFaction(satisfactionRatio);
        if (canceled)
        {
            CancelOrderDisplay();
        }
    }

    /// <summary>
    /// 실제 손님 연결을 바꾸고 해당 상태를 읽는 모드로 복귀합니다.
    /// </summary>
    /// <param name="guest">손님 값입니다.</param>
    public void BindGuest(GuestNPC guest)
    {
        ResetServingPayment();
        Hide();
        _guest = guest;
#if UNITY_EDITOR
        _previewMode = false;
#endif
        if (_guest != null)
        {
            RefreshGuestDisplay();
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트

    /// <summary>
    /// 테스트 표시가 실제 손님 상태 읽기와 충돌하지 않도록 전환합니다.
    /// </summary>
    void BeginPreview()
    {
        _previewMode = true;
        StopWaitingRoutine();
    }

    /// <summary>
    /// 플레이 모드에서 음식 주문 대기를 테스트합니다.
    /// </summary>
    [ContextMenu("테스트/음식 주문 대기 (플레이 모드)")]
    public void TestOrder()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            Debug.Log("플레이 모드에서 대기 진행을 테스트하세요.", this);
            return;
        }

        BeginPreview();
        StartWaiting(_testOrderData, _testWaitingSeconds);
    }

    /// <summary>
    /// 플레이 모드에서 음료 아이콘과 대기 진행을 테스트합니다.
    /// </summary>
    [ContextMenu("테스트/음료 주문 대기 (플레이 모드)")]
    public void TestDrinkOrder()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            Debug.Log("플레이 모드에서 대기 진행을 테스트하세요.", this);
            return;
        }

        BeginPreview();
        StartWaiting(_testDrinkData, _testWaitingSeconds);
    }

    /// <summary>
    /// 남은 만족도가 절반인 모습을 고정 표시합니다.
    /// </summary>
    [ContextMenu("테스트/만족도 50% 표시")]
    public void TestHalfSatisfaction()
    {
        BeginPreview();
        BindOrder(_testOrderData, .5f);
    }

    /// <summary>
    /// 만족도 게이지가 비고 키가 꺼진 취소 상태를 표시합니다.
    /// </summary>
    [ContextMenu("테스트/즉시 주문 취소")]
    public void TestCancelOrder()
    {
        BeginPreview();
        BindOrder(_testOrderData, 0f, true);
    }

    /// <summary>
    /// 지정한 서빙 View를 이 주문과 연결하고 가까이 온 상황을 모의 표시합니다. 실제 NPC는 이동하지 않습니다.
    /// </summary>
    [ContextMenu("테스트/취소 후 근접 폐기 표시")]
    public void TestNearbyDiscard()
    {
        TestCancelOrder();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_testServingView == null)
        {
            Debug.Log("씬의 Test Serving View에 서빙 UI를 연결하세요.", this);
            return;
        }

        _testServingView.Show(_testOrderData, this);
        _testServingView.SetProximityPreview(true);
    }

    /// <summary>
    /// 서빙 대상이 멀어져 근접 폐기 아이콘만 사라진 상황을 모의 표시합니다.
    /// </summary>
    [ContextMenu("테스트/근접 범위 이탈")]
    public void TestLeaveDetectionRange()
    {
        if (_testServingView != null)
        {
            _testServingView.SetProximityPreview(false);
        }
    }

    /// <summary>
    /// 임시 주문·서빙 표시를 정리하고 실제 손님 상태를 읽는 모드로 복귀합니다.
    /// </summary>
    [ContextMenu("테스트/전체 초기화 및 실제 상태 복귀")]
    public void TestResetOrder()
    {
        if (_testServingView != null && _testServingView.TargetOrder == this)
        {
            _testServingView.Hide();
        }

        Hide();
        _previewMode = false;
    }

    #endregion
#endif

}
