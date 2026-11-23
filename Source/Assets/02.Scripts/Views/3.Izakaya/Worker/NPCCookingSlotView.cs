using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 요리의 조리 진행도, 완료 표시와 수령 전 대기 상태를 보여주는 슬롯 뷰입니다.
/// </summary>
public class NPCCookingSlotView : MonoBehaviour
{
    // 에디터 전용 영역은 기존 미리보기 테스트를 유지하며 실제 게임 빌드에서는 제외합니다.

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("배경색")]
    [SerializeField] Image _backgroundImage; // 슬롯 전체 배경 Image를 연결합니다.
    [SerializeField] Sprite _processingBackground, _finishedBackground, _processingFill;
    [SerializeField] Color _originColor = new Color(.04f, .37f, .43f, .96f); // 조리 중 청록색입니다.
    [SerializeField] Color _endColor = new Color(.50f, .16f, .62f, .98f); // 완료 후 수령 대기 중 보라색입니다.

    [Header("요리중/완료 아이콘")]
    [SerializeField] Image _cookingImage; // 왼쪽 CookingIcon 영역을 연결합니다.
    [SerializeField] Image _clearImage; // 슬롯 왼쪽 KeyIcon 상호작용 표시를 연결합니다.

    [Header("요리 게이지")]
    [SerializeField] Image _cookingGuageImage; // Gauge를 연결합니다. 둥근9-slice의 폭으로 진행률을 표시합니다.

    [Header("음식 아이콘")]
    [SerializeField] Image _foodImage; // 조리 중 표시할 FoodIcon을 연결합니다.

    [Header("요리중 여부(읽기용)")]
    [SerializeField] bool _isCooking; // 현재 조리 표시가 진행 중인지 나타냅니다.

    #endregion

    #region 런타임 상태

    bool _isReady; // 준비 완료 여부입니다.
    bool _capsuleGaugeReady;
    float _gaugeWidth, _gaugeHeight;
    float _gaugeBorder;
    Sprite _runtimeProcessingFill;


    #endregion

    #region 코루틴

#if UNITY_EDITOR
    Coroutine _cookingRoutine; // 에디터의 가상 조리 테스트에서만 사용합니다.
#endif

    #endregion

    #region UI 연결 및 표시 설정

    // 이 슬롯에 표시 중인 요리 원본입니다. 실행 중 입력받으며 슬롯이 비워질 때 제거됩니다.
    [SerializeField] Sushi _cookingData;

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트 멤버

    // 이 슬롯의 ContextMenu에서만 사용할 임시 음식 이미지입니다.
    [SerializeField] Sprite _testFoodSprite;
    // 이 슬롯의 ContextMenu에서 읽을 Sushi 에셋입니다. 미연결 시 기존 임시 이미지 테스트를 사용합니다.
    [SerializeField] Sushi _testCookingData;

    #endregion
#endif

    #region 프로퍼티

    /// <summary>
    /// 현재 표시 중인 요리 데이터를 반환합니다. Sprite만 받은 기존 호출에서는 null입니다.
    /// </summary>
    public Sushi CookingData => _cookingData;
    ICookingOrderInfo _orderInfo;
    public ICookingOrderInfo OrderInfo => _orderInfo;

    /// <summary>실제 주문의 진행도와 수령 상태를 읽기 전용으로 표시합니다. 자체 타이머는 사용하지 않습니다.</summary>
    public void DisplayOrder(ICookingOrderInfo order)
    {
        if (order == null || order.Sushi == null) { ResetSlot(); return; }
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        StopCookingRoutine();
        _orderInfo = order;
        _cookingData = order.Sushi;
        SetFoodImage(IjakayaView.ResolveMenuIcon(order.Icon, order.Sushi.Name, this));
        UpdateCookingGauge(order.CookingProgress);
        SetCookingState(!order.IsCompleted);
        // 완료 목록에 있어도 실제 수령이 불가능하면 수령 키를 표시하지 않습니다.
        _isReady = order.IsCompleted && order.IsReceivable;
        if (_clearImage != null) _clearImage.gameObject.SetActive(_isReady);
    }

    /// <summary>표시용 진행률만 반영합니다. 조리 완료와 수령 가능 여부를 자체적으로 확정하지 않습니다.</summary>
    public void DisplayProgress(float progress)
    {
        if (_orderInfo == null || !isActiveAndEnabled) return;
        UpdateCookingGauge(float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : Mathf.Clamp01(progress));
    }

    /// <summary>
    /// 현재 요리 이름을 반환합니다. 화면 외부의 표시·조회에도 사용할 수 있습니다.
    /// </summary>
    public string CookingName => _cookingData != null ? _cookingData.Name : string.Empty;

    /// <summary>
    /// 현재 조리 진행 표시 중인지 반환합니다.
    /// </summary>
    public bool isCooking => _isCooking;

    /// <summary>
    /// 조리가 끝나 수령 가능한 표시 상태인지 반환합니다.
    /// </summary>
    public bool IsReady => _isReady;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 슬롯이나 부모가 꺼지면 코루틴과 표시 상태를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        StopCookingRoutine();
        ResetVisuals();
    }

    #endregion

    #region UI 표시 및 갱신


#if UNITY_EDITOR
    /// <summary>
    /// 빈 슬롯에 음식과 조리 시간을 표시합니다. 진행 중이거나 완료 대기 중인 요리는 덮어쓰지 않습니다.
    /// </summary>
    /// <param name="sprite">스프라이트 이미지입니다.</param>
    /// <param name="maxCooltime">최대 재사용 대기 값입니다.</param>
    public void StartCook(Sprite sprite, float maxCooltime)
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying || _isCooking || _isReady)
        {
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!isActiveAndEnabled)
        {
            return;
        }

        StopCookingRoutine();
        _cookingData = null;
        SetFoodImage(sprite);
        UpdateCookingGauge(0f);
        SetCookingState(true);
        // 0초와 잘못된 시간은 나눗셈이나 코루틴 없이 즉시 완료 표시합니다.
        if (maxCooltime <= 0f || float.IsNaN(maxCooltime) || float.IsInfinity(maxCooltime))
        {
            CompleteCooking();
            return;
        }

        _cookingRoutine = StartCoroutine(CookingRoutine(maxCooltime));
    }

    /// <summary>
    /// 요리 원본의 이미지와 조리 시간으로 표시를 시작합니다.
    /// </summary>
    /// <param name="cookingData">조리 데이터 값입니다.</param>
    public void StartCook(Sushi cookingData)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (cookingData == null)
        {
            return;
        }

        StartCookData(cookingData, cookingData.CookTime);
    }

    /// <summary>
    /// 요리 원본과 별도로 계산된 시간을 표시하며, 이미 사용 중인 슬롯의 데이터는 유지합니다.
    /// </summary>
    /// <param name="cookingData">조리 데이터 값입니다.</param>
    /// <param name="maxCooltime">최대 재사용 대기 값입니다.</param>
    public void StartCookData(Sushi cookingData, float maxCooltime)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (cookingData == null || !Application.isPlaying || _isCooking || _isReady)
        {
            return;
        }

        StartCook(IjakayaView.ResolveMenuIcon(cookingData, this), maxCooltime);
        if (_isCooking || _isReady)
        {
            _cookingData = cookingData;
        }
    }

    /// <summary>
    /// 시간에 맞춰 fillAmount를 채우고 마지막에는 정확히 100%로 완료합니다.
    /// </summary>
    /// <param name="maxCooltime">최대 재사용 대기 값입니다.</param>
    IEnumerator CookingRoutine(float maxCooltime)
    {
        float timer = 0f; // 타이머 값입니다.
        while (timer < maxCooltime)
        {
            yield return null;
            timer += Time.deltaTime;
            UpdateCookingGauge(timer / maxCooltime);
        }

        _cookingRoutine = null;
        CompleteCooking();
    }

    /// <summary>
    /// 완성된 요리의 표시만 정리하고 슬롯을 재사용 가능하게 숨깁니다.
    /// </summary>
    public void TakeFood()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_isReady)
        {
            return;
        }

        ResetSlot();
    }
#endif

    /// <summary>
    /// 진행 또는 완료 표시를 강제로 비우고 슬롯을 숨깁니다.
    /// </summary>
    public void ResetSlot()
    {
        StopCookingRoutine();
        ResetVisuals();
        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 게이지를 끝까지 채우고 완료 배경과 수령 표시로 바꿉니다.
    /// </summary>
    void CompleteCooking()
    {
        UpdateCookingGauge(1f);
        SetCookingState(false);
    }
#endif

    /// <summary>
    /// 음식 이미지는 유지하고 왼쪽 조리 아이콘을 완료 후 수령 표시로 바꿉니다.
    /// </summary>
    /// <param name="isCooking">조리 여부입니다.</param>
    void SetCookingState(bool isCooking)
    {
        _isCooking = isCooking;
        _isReady = !isCooking;
        if (_cookingImage != null)
        {
            _cookingImage.gameObject.SetActive(isCooking && _processingBackground == null);
        }

        if (_clearImage != null)
        {
            _clearImage.gameObject.SetActive(!isCooking);
        }

        if (_foodImage != null)
        {
            _foodImage.gameObject.SetActive(true);
        }

        if (_backgroundImage != null)
        {
            if (_processingBackground != null && _finishedBackground != null)
            {
                _backgroundImage.sprite = isCooking ? _processingBackground : _finishedBackground;
                _backgroundImage.type = Image.Type.Simple;
                _backgroundImage.color = Color.white;
                _cookingGuageImage.gameObject.SetActive(isCooking);
            }
            else _backgroundImage.color = isCooking ? _originColor : _endColor;
        }
    }

    /// <summary>
    /// 빈 슬롯의 아이콘·진행도·완료 상태를 초기화합니다.
    /// </summary>
    void ResetVisuals()
    {
        _orderInfo = null;
        _isCooking = false;
        _isReady = false;
        _cookingData = null;
        UpdateCookingGauge(0f);
        if (_foodImage != null)
        {
            _foodImage.sprite = null;
            _foodImage.gameObject.SetActive(false);
        }

        if (_clearImage != null)
        {
            _clearImage.gameObject.SetActive(false);
        }

        if (_cookingImage != null)
        {
            _cookingImage.gameObject.SetActive(false);
        }

        if (_backgroundImage != null)
        {
            _backgroundImage.color = _originColor;
        }
    }

    /// <summary>
    /// 음식 스프라이트를 갱신하며 이전 음식 이미지가 남지 않도록 처리합니다.
    /// </summary>
    /// <param name="foodSprite">요리 스프라이트 이미지입니다.</param>
    void SetFoodImage(Sprite foodSprite)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_foodImage == null)
        {
            return;
        }

        _foodImage.sprite = foodSprite;
        _foodImage.color = foodSprite != null ? Color.white : new Color(.95f, .91f, .75f, .5f);
        if (_processingBackground != null)
            SushiRestaurantStyleView.FitTodayMenuIcon(_foodImage.transform, 68f, 0f, 50f);
    }

    /// <summary>배경과 채움 모두 높이의 절반을 반지름으로 삼아 긴 캡슐형을 유지합니다.</summary>
    void PrepareCapsuleGauge()
    {
        if (_capsuleGaugeReady || _cookingGuageImage == null) return;
        var track = transform.Find("GaugeTrack")?.GetComponent<Image>();
        if (_processingBackground != null && _processingFill != null)
        {
            // 전달받은 이미지의 투명 여백을 제외하고 게이지만 사용합니다.
            _runtimeProcessingFill = Sprite.Create(_processingFill.texture,
                new Rect(_processingFill.rect.x + 17f, _processingFill.rect.y + 25f, 119f, 18f),
                new Vector2(.5f, .5f), _processingFill.pixelsPerUnit, 0, SpriteMeshType.FullRect,
                new Vector4(9f, 9f, 0f, 9f));
            _gaugeWidth = 119f; _gaugeHeight = 18f; _gaugeBorder = 9f;
            _cookingGuageImage.sprite = _runtimeProcessingFill;
            _cookingGuageImage.type = Image.Type.Sliced;
            _cookingGuageImage.color = Color.white;
            _cookingGuageImage.preserveAspect = false;
            _cookingGuageImage.fillCenter = true;
            var fillRect = _cookingGuageImage.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(.5f, .5f);
            fillRect.pivot = new Vector2(0f, .5f);
            fillRect.anchoredPosition = new Vector2(-85f, 0f);
            if (track != null) track.gameObject.SetActive(false);
            _capsuleGaugeReady = true;
            return;
        }
        if (track == null) return;
        var style = FindFirstObjectByType<SushiRestaurantStyleView>(FindObjectsInactive.Include);
        var sprite = style != null ? style.RoundedPanelSprite : _cookingGuageImage.sprite;
        if (sprite == null || sprite.border.x <= 0f) return;
        var tr = track.rectTransform;
        _gaugeWidth = tr.rect.width; _gaugeHeight = tr.rect.height;
        if (_gaugeWidth <= 0f || _gaugeHeight <= 0f) return;
        _gaugeBorder = sprite.border.x;
        foreach (var image in new[] { track, _cookingGuageImage })
        {
            image.sprite = sprite; image.type = Image.Type.Sliced;
            image.fillCenter = true; image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = _gaugeBorder / (_gaugeHeight * .5f);
        }
        var r = _cookingGuageImage.rectTransform;
        var parent = r.parent as RectTransform;
        var center = (Vector2)tr.localPosition + tr.rect.center;
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(0f, .5f);
        r.anchoredPosition = center - (parent != null ? parent.rect.center : Vector2.zero)
            - new Vector2(_gaugeWidth * .5f, 0f);
        _capsuleGaugeReady = true;
    }

    void OnDestroy()
    {
        if (_runtimeProcessingFill != null) Destroy(_runtimeProcessingFill);
    }

    /// <summary>실제 진행률을 바꾸지 않고 둥근 끝을 자르지 않는 폭 표시로 반영합니다.</summary>
    void UpdateCookingGauge(float gauge)
    {
        if (_cookingGuageImage == null) return;
        float progress = float.IsNaN(gauge) || float.IsInfinity(gauge) ? 0f : Mathf.Clamp01(gauge);
        PrepareCapsuleGauge();
        if (!_capsuleGaugeReady)
        {
            _cookingGuageImage.fillAmount = progress;
            return;
        }
        float width = _gaugeWidth * progress;
        _cookingGuageImage.rectTransform.sizeDelta = new Vector2(width, _gaugeHeight);
        _cookingGuageImage.enabled = progress > 0f;
        // 낮은 진행률에서도 폭을 늘리지 않고 짧은 변 안에 곡률을 맞춥니다.
        _cookingGuageImage.pixelsPerUnitMultiplier = _gaugeBorder
            / Mathf.Max(.01f, Mathf.Min(width, _gaugeHeight) * .5f);
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 이 슬롯이 시작한 조리 표시 코루틴만 중지합니다.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    void StopCookingRoutine()
    {
#if UNITY_EDITOR
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_cookingRoutine == null)
        {
            return;
        }

        StopCoroutine(_cookingRoutine);
        _cookingRoutine = null;
#endif
    }

    #endregion

#if UNITY_EDITOR
    #region 임시 데이터 및 테스트

    /// <summary>
    /// 플레이 모드에서 요리 데이터로 테스트 조리를 시작합니다. 데이터가 없으면 5초 이미지 테스트를 사용합니다.
    /// </summary>
    [ContextMenu("테스트/요리 시작 (플레이 모드)")]
    public void Test_StartCook()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            Debug.Log("플레이 모드에서 테스트하세요.", this);
            return;
        }

        ResetSlot();
        if (_testCookingData != null)
        {
            StartCook(_testCookingData);
        }
        else
        {
            StartCook(_testFoodSprite, 5f);
        }
    }

    /// <summary>
    /// 플레이 모드에서 이 슬롯을 즉시 완료 상태로 표시합니다.
    /// </summary>
    [ContextMenu("테스트/즉시 완료 (플레이 모드)")]
    public void Test_CompleteCook()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            Debug.Log("플레이 모드에서 테스트하세요.", this);
            return;
        }

        ResetSlot();
        if (_testCookingData != null)
        {
            StartCookData(_testCookingData, 0f);
        }
        else
        {
            StartCook(_testFoodSprite, 0f);
        }
    }

    /// <summary>
    /// 완료된 슬롯만 수령 표시로 정리합니다.
    /// </summary>
    [ContextMenu("테스트/완성 요리 수령")]
    public void Test_TakeFood()
    {
        TakeFood();
    }

    #endregion
#endif

}
