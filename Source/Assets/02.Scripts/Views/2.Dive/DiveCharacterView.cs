using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
public class DiveCharacterView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("화면 HUD")]
    [SerializeField] RectTransform _hudRoot; // 잠수 HUD의 기준 RectTransform을 연결합니다.
    [SerializeField] float _waterSurfaceY; // 현재 수심을 계산할 수면의 월드 Y 좌표입니다.

    [Header("캐릭터 조준 표시")]
    [SerializeField] InputActionReference _aimPointerPositionAction; // 공격 입력과 동일한 PointerPosition 액션입니다.
    [SerializeField, Range(.2f, 1f)] float _aimDisplayScale = .55f; // 발사 각도를 유지하며 곡선을 가까이 표시할 기본 반경을 조절합니다. 상하 범위는 실제 발사 각도를 따릅니다.
    [SerializeField, Min(0.001f)] float _lineWidth; // 조준 원호와 화살표 선의 두께입니다.
    [SerializeField, Min(0.01f)] float _arrowSize; // 조준 화살표의 크기입니다.
    [SerializeField, Min(0f)] float _arrowGap; // 조준 원호와 화살표 사이의 간격입니다.
    [SerializeField, Range(8, 64)] int _arcSegments; // 조준 원호를 구성하는 선 분할 수입니다.

    [Header("작살 저항 게이지")]
    [SerializeField] GameObject _resistanceRoot; // 작살 저항 게이지 전체 표시 영역을 연결합니다.
    [SerializeField] UnityEngine.UI.Image _resistanceFill; // 현재 저항 비율을 채울 원호 이미지를 연결합니다.
    [SerializeField] TMP_Text _resistanceValueText; // 저항 게이지의 조작 키 안내 텍스트를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _resistanceBorder; // 저항 게이지의 바깥 테두리 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _resistanceTrack; // 저항 게이지의 비어 있는 구간 배경을 연결합니다.
    [SerializeField] RectTransform _resistanceKeyBadge; // 저항 게이지 옆 키 안내 배지의 RectTransform을 연결합니다.
    [SerializeField] Camera _resistanceCamera; // 플레이어 월드 좌표를 게이지 화면 좌표로 변환할 카메라입니다.
    [SerializeField, Min(1f)] float _resistanceGaugeSize; // 저항 게이지 원호의 표시 크기입니다.
    [SerializeField, Range(1f, 180f)] float _resistanceArcAngle; // 저항 게이지가 차지하는 원호의 각도입니다.
    [SerializeField] Vector2 _resistanceScreenOffset; // 플레이어 기준 게이지의 화면 위치 보정값입니다.
    [SerializeField] string _resistanceKeyLabel; // 저항 게이지 옆에 표시할 조작 키 문구입니다.

    #endregion

    #region 런타임 상태

    Sprite _resistanceBorderSprite; // 저항 테두리 스프라이트 이미지입니다.
    Sprite _resistanceFillSprite; // 저항 채움 스프라이트 이미지입니다.
    Canvas _resistanceCanvas; // 저항 캔버스 값입니다.
    FishResistanceGaugeView _resistancePresentation;
    Transform _resistanceHeroAnchor; // 저항 주인공 앵커 값입니다.

    #endregion

    #region UI 연결 및 표시 설정

    [Header("무기 그림의 실제 영역 (투명 여백 제외)")]
    [SerializeField] Rect _harpoonIconVisibleRect = new Rect(.12890625f, .3203125f, .75f, .3359375f);
    [SerializeField] Rect _gunIconVisibleRect = new Rect(.10546875f, .1640625f, .78125f, .67578125f);

    [Header("유틸리티 선택 표시")]
    [SerializeField] UnityEngine.UI.RawImage[] _utilitySlotCards = new UnityEngine.UI.RawImage[2]; // Utilities의 UtilitySlot1, UtilitySlot2 배경을 순서대로 연결합니다.
    [SerializeField] CanvasGroup[] _utilitySlotGroups = new CanvasGroup[2]; // 각 유틸리티 슬롯의 CanvasGroup을 연결해 선택하지 않은 칸을 어둡게 표시합니다.
    [SerializeField] TMP_Text[] _utilityKeyTexts = new TMP_Text[2]; // 각 슬롯의 SlotNumber/Key 텍스트를 순서대로 연결합니다.
    [SerializeField] TMP_Text _utilityStatusText; // Utilities/UtilityStatus 텍스트를 연결해 즉시 사용 안내와 사용 결과를 표시합니다.
    [SerializeField, Range(0.1f, 1f)] float _utilityInactiveAlpha = 0.45f; // 다른 무기나 유틸리티를 선택 중인 슬롯의 불투명도입니다.

    #endregion

    #region 런타임 상태

    int _selectedUtilitySlot = -1; // -1은 무기 선택 상태이며 0·1은 유틸리티 슬롯 선택 상태입니다.
    UtilityItemData _selectedUtilityItem; // 선택 이후 장착 아이템이 바뀌었는지 확인하는 참조입니다.
    InputHandler _utilityInputs; // 기존 무기 선택 이벤트를 구독하는 입력 담당자이며 별도 UI 연결은 필요 없습니다.
    UnderSeaHero.HeroMode _lastUtilityHeroMode; // 외부에서 무기 모드가 바뀌어도 유틸리티 선택을 해제하기 위한 이전 값입니다.
    UnderSeaHero.RangeMode _lastUtilityRangeMode; // 이전 원거리 무기 선택 상태입니다.
    string _utilityFeedback; // 마지막 선택·사용 결과 안내입니다.
    LineRenderer _aimArc; // 조준 원호 값입니다.
    [SerializeField] Sprite _aimLineSprite; // 투명 여백을 포함한 새 곡선 조준선입니다.
    [SerializeField] Sprite _aimArrowSprite; // fishtail_arrow_icon을 연결합니다.
    SpriteRenderer _aimArrowRenderer; // 곡선과 분리된 조준 화살표입니다.
    [SerializeField] Mesh _aimCurveMeshTemplate; // 원본 곡선의 UV를 보관한 표시용 메시입니다.
    MeshRenderer _aimCurveRenderer; // 실제 조준 각도로 변형되는 곡선입니다.
    Mesh _aimCurveMesh; // 이 View가 소유한 런타임 곡선 메시입니다.
    Material _aimCurveMaterial; // 원본 조준선 텍스처를 표시하는 전용 머티리얼입니다.
    Vector3[] _aimCurveVertices; // 반복 재사용하는 곡선 정점입니다.
    SpriteRenderer _aimLineRenderer; // 조준점의 스프라이트 표시입니다.
    LineRenderer _aimArrow; // 조준 화살표 값입니다.
    Material _aimMaterial; // 조준 머티리얼 값입니다.
    UnderSeaHero _hero; // 현재 HUD에 상태와 입력 이벤트를 제공하는 잠수 플레이어입니다.
    HeroRunModel _runModel; // 현재 잠수의 체력·산소 등 상태를 읽을 실행 데이터입니다.
    Inventory _inventory; // 장착 유틸리티와 보유 수량을 읽을 인벤토리입니다.
    BulletSpawner _bullets; // 탄약과 원거리 공격 상태를 확인할 발사 컴포넌트입니다.
    GigShooter _gig; // 작살 상태와 저항 게이지 값을 제공하는 컴포넌트입니다.
    RectTransform _canvasRect; // 캔버스 사각 영역 값입니다.
    RectTransform _crosshair; // 조준점 값입니다.
    TMP_Text _depthText; // 깊이 텍스트입니다.
    Sprite _oxygenNormalSprite, _oxygenWarningSprite; // 기본 및 산소 부족 게이지 리소스입니다.
    UnityEngine.UI.Image _oxygenFillImage; // 산소 채움 이미지입니다.
    UnityEngine.UI.RawImage _meleeCard; // 근접 카드 이미지입니다.
    UnityEngine.UI.RawImage _gigCard; // 작살 카드 이미지입니다.
    UnityEngine.UI.RawImage _gunCard; // 원거리 무기 카드 이미지입니다.
    readonly UnityEngine.UI.Image[] _utilityIcons = new UnityEngine.UI.Image[2]; // 유틸리티 아이콘 목록입니다.
    readonly Sprite[] _emptyUtilityIcons = new Sprite[2]; // 비어 있음 유틸리티 아이콘 목록입니다.
    readonly TMP_Text[] _utilityNames = new TMP_Text[2]; // 유틸리티 이름 목록입니다.
    readonly TMP_Text[] _utilityCounts = new TMP_Text[2]; // 유틸리티 개수 목록입니다.
    TMP_Text _oxygenText; // 산소 텍스트입니다.
    TMP_Text _gigStateText; // 작살 상태 텍스트입니다.
    TMP_Text _ammoText; // 탄약 텍스트입니다.
    CanvasGroup _lowOxygenNotice;
    Tween _lowOxygenBlink; // 배경의 초당 1회 점멸입니다.
    Sequence _lowOxygenTween;
    bool _lowOxygenFading; // 페이드를 매 프레임 재시작하지 않도록 합니다.
    [SerializeField] UIVisibilityEffectView _oxygenVignetteEffect; // Canvas Editor가 재생할 가장자리 경고입니다.
    bool _wasLowOxygen, _oxygenVignetteStatic;
    float _lastNoticeOxygen;
    Sequence _rangeSwapTween; // 이 View가 생성한 원거리 슬롯 전환 연출입니다.
    bool? _lastRangeFront; // 최초 배치는 즉시 적용하고 이후 ALT 변경만 연출합니다.
    bool _aiming; // 조준 중 여부입니다.
    bool _firedDuringAim; // 발사 진행 중 조준 여부입니다.
    static readonly Color Gold = new Color(1f, 0.61f, 0.16f, 1f); // 골드 색상입니다.
    static readonly Color Dark = new Color(0.09f, 0.22f, 0.29f, 0.95f); // 어두운 색 색상입니다.
    static readonly Color Blue = new Color(0.10f, 0.58f, 0.75f, 0.96f); // 파란색 색상입니다.
    static readonly Color Backup = new Color(0.40f, 0.27f, 0.48f, 0.82f); // 백업 색상입니다.

    #endregion

    #region 프로퍼티

    public int SelectedUtilitySlot => _selectedUtilitySlot; // 외부 UI가 현재 선택을 읽을 때 사용하며 값 변경은 선택 함수로만 처리합니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// UI 참조와 입력 연결을 초기화합니다.
    /// </summary>
    void Awake()
    {
        CacheUI();
    }

    /// <summary>
    /// HUD 재활성화 시 기존 입력 담당자의 무기 선택 이벤트를 다시 연결합니다.
    /// </summary>
    void OnEnable()
    {
        ConnectUtilityInputs();
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnAimCameraRendering;
        Camera.onPreCull += OnAimCameraPreCull;
    }

    /// <summary>
    /// 매 프레임 입력과 표시 상태를 갱신합니다.
    /// </summary>
    void Update()
    {
        UpdateUtilitySelection();
        Refresh();
    }

    /// <summary>
    /// 다른 갱신이 끝난 뒤 화면 표시를 맞춥니다.
    /// </summary>
    void LateUpdate()
    {
        RefreshAim();
        UpdateResistancePosition();
    }

    /// <summary>
    /// 뷰가 꺼질 때 이벤트와 진행 중인 표시를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnAimCameraRendering;
        Camera.onPreCull -= OnAimCameraPreCull;
        ResetLowOxygenNotice();
        _rangeSwapTween?.Kill(); _rangeSwapTween = null; _lastRangeFront = null;
        SetAimVisible(false);
        SetResistanceVisible(false);
        ClearUtilitySelection();
        DisconnectUtilityInputs();
    }

    /// <summary>
    /// 뷰가 제거될 때 등록한 이벤트와 연출을 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        _rangeSwapTween?.Kill();
        Unbind();
        if (_aimCurveMesh != null) Destroy(_aimCurveMesh);
        if (_aimCurveMaterial != null) Destroy(_aimCurveMaterial);
        if (_aimMaterial != null)
        {
            Destroy(_aimMaterial);
        }

        if (_resistanceBorderSprite != null)
        {
            Destroy(_resistanceBorderSprite.texture);
            Destroy(_resistanceBorderSprite);
        }

        if (_resistanceFillSprite != null)
        {
            Destroy(_resistanceFillSprite.texture);
            Destroy(_resistanceFillSprite);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 잠수 플레이어와 실행 데이터·인벤토리를 HUD에 연결합니다.
    /// </summary>
    /// <param name="hero">주인공 값입니다.</param>
    /// <param name="runModel">실행 모델 값입니다.</param>
    /// <param name="inventory">인벤토리 값입니다.</param>
    public void Bind(UnderSeaHero hero, HeroRunModel runModel, Inventory inventory)
    {
        Unbind();
        CacheUI();
        _hero = hero;
        Animator bodyAnimator = hero != null ? hero.GetComponentInChildren<Animator>() : null; // 몸체 애니메이터 값입니다.
        if (bodyAnimator != null && bodyAnimator.isHuman)
        {
            _resistanceHeroAnchor = bodyAnimator.GetBoneTransform(HumanBodyBones.Chest)
                ?? bodyAnimator.GetBoneTransform(HumanBodyBones.Hips);
        }
        _runModel = runModel != null ? runModel : hero != null ? hero.GetComponentInChildren<HeroRunModel>(true) : null;
        if (_runModel != null) _runModel.OxygenDepleted += OnNoticeOxygenDepleted;
        _inventory = inventory;
        _bullets = hero != null ? hero.GetComponentInChildren<BulletSpawner>(true) : null;
        _gig = hero != null ? hero.HarpoonShooter : null;
        if (_hero != null)
        {
            _hero.AimChanged += OnAimChanged;
            _hero.RangeAttackFired += OnRangeAttackFired;
        }

        if (_gig != null)
        {
            UpdateResistanceGauge(_gig.ResistanceGauge, _gig.ResistanceGaugeMax);
            SetResistanceVisible(_gig.IsResisting);
        }

        ConnectUtilityInputs();
        Refresh();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 작살 저항 게이지의 표시와 플레이어 추적 위치를 갱신합니다.
    /// </summary>
    /// <param name="visible">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetResistanceVisible(bool visible)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_resistanceRoot == null)
        {
            return;
        }

        if (visible)
        {
            SetupResistanceGauge();
        }

        _resistanceRoot.SetActive(visible);
        if (visible)
        {
            UpdateResistancePosition();
        }
    }

    /// <summary>
    /// 현재 저항 수치를 원호 게이지의 채움 비율에 적용합니다.
    /// </summary>
    /// <param name="current">게이지에 표시할 현재 수치입니다.</param>
    /// <param name="max">게이지 비율 계산에 사용할 최대 수치입니다.</param>
    public void UpdateResistanceGauge(float current, float max)
    {
        float progress = max > 0f ? Mathf.Clamp01(current / max) : 0f; // 진행도 값입니다.
        SetupResistanceGauge();
        if (_resistancePresentation != null)
        {
            _resistancePresentation.SetProgress(progress);
            return;
        }
        if (_resistanceFill != null)
        {
            _resistanceFill.fillAmount = progress * _resistanceArcAngle / 360f;
        }
    }

    /// <summary>
    /// 작살 저항 게이지의 고리 이미지와 키 안내를 준비합니다.
    /// </summary>
    void SetupResistanceGauge()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_resistanceRoot == null || _resistanceFill == null || _resistanceBorder == null || _resistanceTrack == null || _resistanceGaugeSize <= 0f)
        {
            return;
        }

        // 새 리소스가 연결된 UI는 표시 전용 View에 맡기고 기존 입력·수치·위치 추적은 유지합니다.
        if (_resistancePresentation == null)
            _resistanceRoot.TryGetComponent(out _resistancePresentation);
        if (_resistancePresentation != null)
        {
            _resistancePresentation.Configure(_resistanceGaugeSize);
            _resistanceCanvas = _resistanceRoot.GetComponentInParent<Canvas>();
            return;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_resistanceBorderSprite != null)
        {
            return;
        }

        _resistanceBorderSprite = CreateRingSprite(0.35f, 0.48f);
        _resistanceFillSprite = CreateRingSprite(0.37f, 0.46f);
        ConfigureResistanceArc(_resistanceBorder, _resistanceBorderSprite, Color.white);
        ConfigureResistanceArc(_resistanceTrack, _resistanceFillSprite, new Color(0.17f, 0.22f, 0.28f, 0.95f));
        ConfigureResistanceArc(_resistanceFill, _resistanceFillSprite, new Color(1f, 0.76f, 0.1f, 1f));
        if (_resistanceValueText != null)
        {
            _resistanceValueText.text = _resistanceKeyLabel;
        }

        if (_resistanceKeyBadge != null)
        {
            float angle = (180f - _resistanceArcAngle * 0.5f) * Mathf.Deg2Rad; // 각도 값입니다.
            _resistanceKeyBadge.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (_resistanceGaugeSize * 0.415f) + Vector2.up * 19f;
        }

        _resistanceCanvas = _resistanceRoot.GetComponentInParent<Canvas>();
    }

    /// <summary>
    /// 게이지 이미지의 원호 각도·색상·크기를 설정합니다.
    /// </summary>
    /// <param name="image">이미지입니다.</param>
    /// <param name="sprite">스프라이트 이미지입니다.</param>
    /// <param name="color">이미지에 적용할 목표 색상입니다.</param>
    void ConfigureResistanceArc(UnityEngine.UI.Image image, Sprite sprite, Color color)
    {
        image.sprite = sprite;
        image.type = UnityEngine.UI.Image.Type.Filled;
        image.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        image.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        image.fillClockwise = true;
        image.fillAmount = _resistanceArcAngle / 360f;
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform; // 사각 영역 값입니다.
        rect.sizeDelta = Vector2.one * _resistanceGaugeSize;
        rect.localRotation = Quaternion.Euler(0f, 0f, 90f + _resistanceArcAngle * 0.5f);
    }

    /// <summary>
    /// 플레이어 위치를 기준으로 저항 게이지의 화면 위치를 맞춥니다.
    /// </summary>
    void UpdateResistancePosition()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_resistanceRoot == null || !_resistanceRoot.activeSelf || _hero == null || _gig == null || !_gig.IsResisting || _resistanceCamera == null || _resistanceCanvas == null)
        {
            return;
        }

        var rect = (RectTransform)_resistanceRoot.transform; // 사각 영역 값입니다.
        var parent = rect.parent as RectTransform; // 부모 값입니다.
        Vector3 screen = _resistanceCamera.WorldToScreenPoint(_resistanceHeroAnchor != null ? _resistanceHeroAnchor.position : _hero.transform.position); // 화면 값입니다.
        Camera uiCamera = _resistanceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _resistanceCanvas.worldCamera; // UI 카메라 값입니다.
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 point))
        {
            if (_resistancePresentation != null)
            {
                bool facingScreenLeft = _hero.FacingDirection.x * _resistanceCamera.transform.right.x < 0f;
                Vector2 offset = _resistanceScreenOffset;
                if (facingScreenLeft) offset.x = -offset.x;
                _resistancePresentation.PlaceBehind(point + offset, facingScreenLeft);
            }
            else
            {
                rect.localPosition = (Vector3)(point + _resistanceScreenOffset);
            }
        }
    }

    /// <summary>
    /// 기존 플레이어의 조준·공격 이벤트와 유틸리티 입력 연결을 해제합니다.
    /// </summary>
    public void Unbind()
    {
        DisconnectUtilityInputs();
        ClearUtilitySelection();
        if (!ReferenceEquals(_hero, null))
        {
            _hero.AimChanged -= OnAimChanged;
            _hero.RangeAttackFired -= OnRangeAttackFired;
        }

        if (!ReferenceEquals(_runModel, null)) _runModel.OxygenDepleted -= OnNoticeOxygenDepleted;
        ResetLowOxygenNotice();
        _hero = null;
        _resistanceHeroAnchor = null;
        _runModel = null;
        _inventory = null;
        _bullets = null;
        _gig = null;
        _aiming = false;
        _firedDuringAim = false;
        SetResistanceVisible(false);
        SetAimVisible(false);
        if (_crosshair != null)
        {
            _crosshair.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// HUD 하위의 텍스트·게이지·무기 슬롯 참조를 확보합니다.
    /// </summary>
    void CacheUI()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_canvasRect != null)
        {
            return;
        }

        _canvasRect = _hudRoot != null ? _hudRoot : GetComponent<RectTransform>();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_canvasRect == null)
        {
            return;
        }

        _crosshair = _canvasRect.Find("Crosshair") as RectTransform;
        if (_crosshair != null)
        {
            _crosshair.gameObject.SetActive(false);
        }

        var status = _canvasRect.Find("Status"); // 상태 값입니다.
        if (status != null)
        {
            _oxygenText = status.Find("OxygenValue")?.GetComponent<TMP_Text>();
            _oxygenFillImage = (status.Find("OxygenFill") ?? status.Find("OxygenTrack/OxygenFill"))?.GetComponent<UnityEngine.UI.Image>();
            _oxygenNormalSprite = _oxygenFillImage != null ? _oxygenFillImage.sprite : null;
            _oxygenWarningSprite = status.Find("OxygenWarningResource")?.GetComponent<UnityEngine.UI.Image>()?.sprite;
            _depthText = status.Find("DepthText")?.GetComponent<TMP_Text>();
        }

        var utilities = _canvasRect.Find("Utilities"); // 유틸리티 값입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < 2 && utilities != null; i++)
        {
            var slot = utilities.Find("UtilitySlot" + (i + 1)); // 슬롯 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null)
            {
                continue;
            }

            _utilityIcons[i] = slot.Find("Icon")?.GetComponent<UnityEngine.UI.Image>();
            _emptyUtilityIcons[i] = _utilityIcons[i] != null ? _utilityIcons[i].sprite : null;
            _utilityNames[i] = slot.Find("Name")?.GetComponent<TMP_Text>();
            _utilityCounts[i] = slot.Find("Count")?.GetComponent<TMP_Text>();
            if (i < _utilitySlotCards.Length && _utilitySlotCards[i] == null)
            {
                _utilitySlotCards[i] = slot.GetComponent<UnityEngine.UI.RawImage>();
            }

            if (i < _utilitySlotGroups.Length && _utilitySlotGroups[i] == null)
            {
                _utilitySlotGroups[i] = slot.GetComponent<CanvasGroup>();
            }

            if (i < _utilityKeyTexts.Length && _utilityKeyTexts[i] == null)
            {
                _utilityKeyTexts[i] = slot.Find("SlotNumber/Key")?.GetComponent<TMP_Text>();
            }
        }

        if (_utilityStatusText == null && utilities != null)
        {
            _utilityStatusText = utilities.Find("UtilityStatus")?.GetComponent<TMP_Text>();
        }

        var weapons = _canvasRect.Find("Weapons"); // 무기 목록 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (weapons == null)
        {
            return;
        }

        _meleeCard = weapons.Find("Melee")?.GetComponent<UnityEngine.UI.RawImage>();
        _gigCard = weapons.Find("Harpoon")?.GetComponent<UnityEngine.UI.RawImage>();
        _gunCard = weapons.Find("Gun")?.GetComponent<UnityEngine.UI.RawImage>();
        _gigStateText = weapons.Find("Harpoon/State")?.GetComponent<TMP_Text>();
        _ammoText = weapons.Find("Gun/Ammo")?.GetComponent<TMP_Text>();
    }

    /// <summary>
    /// 잠수 상태와 선택 무기·유틸리티의 HUD 표시를 갱신합니다.
    /// </summary>
    void Refresh()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_hero == null)
        {
            return;
        }

        float max = _runModel != null ? Mathf.Max(0f, _runModel.MaxOxygen) : 0f; // 최대 값입니다.
        float current = _runModel != null ? Mathf.Clamp(_runModel.CurrentOxygen, 0f, max) : 0f; // 현재 값입니다.
        UpdateLowOxygenNotice(current, max);
        float fraction = max > 0f ? current / max : 0f; // 비율 값입니다.
        if (_oxygenText != null)
        {
            _oxygenText.text = current.ToString("0");
        }

        if (_oxygenFillImage != null)
        {
            _oxygenFillImage.fillAmount = fraction;
            // 25% 미만일 때만 경고 리소스로 바꾸고 회복하면 원본으로 되돌립니다.
            _oxygenFillImage.sprite = fraction < 0.25f && _oxygenWarningSprite != null ? _oxygenWarningSprite : _oxygenNormalSprite;
            _oxygenFillImage.color = Color.white;
        }

        if (_depthText != null)
        {
            _depthText.text = Mathf.Max(0f, _waterSurfaceY - _hero.transform.position.y).ToString("0.0") + "m";
        }

        SetUtility(0, _inventory != null ? _inventory.FirstUtility : null);
        SetUtility(1, _inventory != null ? _inventory.SecondUtility : null);
        RefreshUtilitySelection();
        bool melee = _hero.Mode == UnderSeaHero.HeroMode.Melee; // 근접 여부입니다.
        bool harpoonFront = _hero.CurrentRangeMode == UnderSeaHero.RangeMode.Gig; // 작살 앞쪽 여부입니다.
        if (_meleeCard != null)
        {
            _meleeCard.color = melee && _selectedUtilitySlot < 0 ? Gold : Dark;
            RefreshSlotSkin(_meleeCard, melee && _selectedUtilitySlot < 0);
        }

        bool layoutChanged = !_lastRangeFront.HasValue || _lastRangeFront.Value != harpoonFront;
        if (layoutChanged)
        {
            _rangeSwapTween?.Kill();
            _rangeSwapTween = _lastRangeFront.HasValue ? DOTween.Sequence().SetUpdate(true) : null;
        }
        SetRangeCard(_gigCard, harpoonFront, !melee && harpoonFront && _selectedUtilitySlot < 0, layoutChanged, _rangeSwapTween);
        SetRangeCard(_gunCard, !harpoonFront, !melee && !harpoonFront && _selectedUtilitySlot < 0, layoutChanged, _rangeSwapTween);
        _lastRangeFront = harpoonFront;
        if (_selectedUtilitySlot >= 0)
        {
            if (_gigCard != null)
            {
                _gigCard.color = Dark;
            }

            if (_gunCard != null)
            {
                _gunCard.color = Dark;
            }
        }

        var front = harpoonFront ? _gigCard : _gunCard; // 앞쪽 값입니다.
        if (front != null && front.transform.GetSiblingIndex() != front.transform.parent.childCount - 1)
        {
            front.transform.SetAsLastSibling();
        }

        if (_gigStateText != null)
        {
            _gigStateText.text = _gig == null ? "--" : _gig.IsFlying ? "RETURNING" : "READY";
        }

        if (_ammoText != null)
        {
            _ammoText.text = _bullets == null ? "--" : _bullets.CurrentSpawnCount + " / " + _bullets.MaxSpawnCount;
        }
    }

    /// <summary>
    /// 원거리 무기 카드의 앞뒤 순서와 선택 색상을 적용합니다.
    /// </summary>
    /// <param name="card">카드 이미지입니다.</param>
    /// <param name="front">앞쪽 여부입니다.</param>
    /// <param name="selected">선택 여부입니다.</param>
    // 기존 슬롯 참조와 입력 처리는 유지하고 선택 테두리만 전환합니다.
    static void RefreshSlotSkin(UnityEngine.UI.RawImage card, bool selected)
    {
        if (card == null) return;
        var skin = card.transform.Find("Skin");
        if (skin == null) return;
        card.enabled = false;
        var outline = card.transform.Find("SelectionOutline");
        if (outline != null) outline.gameObject.SetActive(selected);
    }
    /// <summary>원거리 슬롯의 위치·크기·아이콘을 함께 0.3초간 교환합니다. 매 프레임 연출을 재시작하지 않습니다.</summary>
    void SetRangeCard(UnityEngine.UI.RawImage card, bool front, bool selected, bool layoutChanged, Sequence swap)
    {
        if (card == null) return;
        card.color = front ? selected ? Gold : Blue : Backup;
        RefreshSlotSkin(card, selected);
        var rect = card.rectTransform;
        var icon = card.transform.Find("WeaponIcon") as RectTransform;
        if (layoutChanged)
        {
            rect.localScale = Vector3.one;
            Vector2 position = front ? new Vector2(140f, 0f) : new Vector2(316f, -36f);
            Vector2 size = front ? new Vector2(190f, 110f) : new Vector2(160f, 90f);
            Vector2 iconPosition = Vector2.zero;
            Vector2 iconSize = size * .7f;
            var art = icon != null ? icon.GetComponent<UnityEngine.UI.Image>() : null;
            if (art != null && art.sprite != null)
            {
                var visible = card.name == "Gun" ? _gunIconVisibleRect : _harpoonIconVisibleRect;
                var pixels = art.sprite.rect.size;
                float scale = Mathf.Min(size.x * .7f / (pixels.x * visible.width), size.y * .7f / (pixels.y * visible.height));
                iconSize = pixels * scale;
                iconPosition = -Vector2.Scale(visible.center - Vector2.one * .5f, iconSize);
            }
            if (swap == null)
            {
                rect.anchoredPosition = position; rect.sizeDelta = size;
                if (icon != null) { icon.anchoredPosition = iconPosition; icon.sizeDelta = iconSize; }
            }
            else
            {
                swap.Join(rect.DOAnchorPos(position, .3f).SetEase(Ease.InOutSine));
                swap.Join(rect.DOSizeDelta(size, .3f).SetEase(Ease.InOutSine));
                if (icon != null)
                {
                    swap.Join(icon.DOAnchorPos(iconPosition, .3f).SetEase(Ease.InOutSine));
                    swap.Join(icon.DOSizeDelta(iconSize, .3f).SetEase(Ease.InOutSine));
                }
            }
        }
        var key = card.transform.Find("KeyPrompt");
        if (key != null) key.gameObject.SetActive(front);
        card.transform.Find("Title")?.gameObject.SetActive(false);
        card.transform.Find(card.name == "Harpoon" ? "State" : "Ammo")?.gameObject.SetActive(false);
    }
    /// <summary>
    /// 지정한 유틸리티 슬롯의 아이콘·이름·수량을 표시합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="item">아이템 값입니다.</param>
    /// <summary>산소 소진 사망과 같은 프레임에 비네트 반복을 종료하고 정지 표시합니다.</summary>
    void OnNoticeOxygenDepleted()
    {
        if (!isActiveAndEnabled || _runModel == null) return;
        UpdateLowOxygenNotice(0f, _runModel.MaxOxygen);
    }

    /// <summary>산소 25% 미만 진입 시 5초 표시하고 보충되면 즉시 0.5초 페이드를 시작합니다.</summary>
    void UpdateLowOxygenNotice(float current, float maximum)
    {
        if (_lowOxygenNotice == null && _canvasRect != null)
            _lowOxygenNotice = _canvasRect.Find("LowOxygenNotice")?.GetComponent<CanvasGroup>();
        if (_oxygenVignetteEffect == null && _canvasRect != null)
            _oxygenVignetteEffect = _canvasRect.Find("LowOxygenVignette")?.GetComponent<UIVisibilityEffectView>();
        bool low = maximum > 0f && current / maximum < .25f;
        bool replenished = current > _lastNoticeOxygen + .01f;
        bool depleted = maximum > 0f && current <= 0f;
        if (_oxygenVignetteEffect != null)
        {
            if (depleted)
            {
                if (!_oxygenVignetteStatic || !_oxygenVignetteEffect.IsShown)
                    _oxygenVignetteEffect.ShowStatic();
            }
            else if (low && (!_wasLowOxygen || _oxygenVignetteStatic)) _oxygenVignetteEffect.PlayLoop();
            else if (replenished || !low) _oxygenVignetteEffect.Hide();
        }
        if (_lowOxygenNotice != null)
        {
            if (low && !_wasLowOxygen)
            {
                _lowOxygenTween?.Kill();
                _lowOxygenNotice.gameObject.SetActive(true);
                _lowOxygenFading = false;
                _lowOxygenNotice.alpha = 0f;
                StartLowOxygenBlink();
                _lowOxygenTween = DOTween.Sequence().SetUpdate(true)
                    .Append(_lowOxygenNotice.DOFade(1f, .5f).SetEase(Ease.Linear)).AppendInterval(5f).AppendCallback(() => { _lowOxygenBlink?.Kill(); _lowOxygenFading = true; }).Append(_lowOxygenNotice.DOFade(0f, .5f).SetEase(Ease.Linear))
                    .OnComplete(() => { _lowOxygenNotice.gameObject.SetActive(false); _lowOxygenTween = null; });
            }
            else if (!_lowOxygenFading && _lowOxygenNotice.gameObject.activeSelf && (replenished || !low))
            {
                _lowOxygenBlink?.Kill();
                _lowOxygenFading = true;
                _lowOxygenTween?.Kill();
                _lowOxygenTween = DOTween.Sequence().SetUpdate(true)
                    .Append(_lowOxygenNotice.DOFade(0f, .5f).SetEase(Ease.Linear))
                    .OnComplete(() => { _lowOxygenNotice.gameObject.SetActive(false); _lowOxygenTween = null; });
            }
        }
        _oxygenVignetteStatic = depleted;
        _wasLowOxygen = low;
        _lastNoticeOxygen = current;
    }

    /// <summary>뷰 종료와 연결 해제 시 안내 연출 및 감지 상태를 초기화합니다.</summary>
    /// <summary>문구는 고정하고 띠 배경만 0.5초 어두워짐·0.5초 복귀로 초당 한 번 점멸합니다.</summary>
    void StartLowOxygenBlink()
    {
        _lowOxygenBlink?.Kill();
        var banner = _lowOxygenNotice.transform.Find("Banner")?.GetComponent<UnityEngine.UI.Image>();
        if (banner == null) return;
        var color = banner.color; color.a = .65f; banner.color = color;
        _lowOxygenBlink = banner.DOFade(.15f, .5f).SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }
    void ResetLowOxygenNotice()
    {
        if (_oxygenVignetteEffect != null) _oxygenVignetteEffect.HideImmediate();
        _lowOxygenBlink?.Kill(); _lowOxygenBlink = null;
        _lowOxygenTween?.Kill(); _lowOxygenTween = null;
        if (_lowOxygenNotice != null) { _lowOxygenNotice.alpha = 0f; _lowOxygenNotice.gameObject.SetActive(false); }
        _wasLowOxygen = false; _oxygenVignetteStatic = false; _lowOxygenFading = false; _lastNoticeOxygen = 0f;
    }
    void SetUtility(int index, UtilityItemData item)
    {
        var icon = _utilityIcons[index]; // 아이콘 값입니다.
        if (icon != null)
        {
            icon.sprite = item != null && item.ItemIcon != null ? item.ItemIcon : _emptyUtilityIcons[index];
            icon.enabled = icon.sprite != null;
            icon.color = item != null ? Color.white : new Color(1f, 1f, 1f, 0.38f);
        }

        if (_utilityNames[index] != null)
        {
            _utilityNames[index].text = item != null ? item.ItemName : "";
        }

        if (_utilityCounts[index] != null)
        {
            _utilityCounts[index].text = item != null && _inventory != null ? "x" + _inventory.GetCount(item) : "";
        }
    }

    /// <summary>
    /// 플레이어의 조준 상태 변경을 조준 UI에 반영합니다.
    /// </summary>
    /// <param name="aiming">조준 중 여부입니다.</param>
    void OnAimChanged(bool aiming)
    {
        _aiming = aiming;
        _firedDuringAim = false;
        if (!aiming)
        {
            SetAimVisible(false);
        }
    }

    /// <summary>
    /// 같은 플레이어의 입력 담당자를 찾아 유틸리티와 무기 선택 이벤트를 구독합니다.
    /// </summary>
    void ConnectUtilityInputs()
    {
        DisconnectUtilityInputs();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_hero == null || !isActiveAndEnabled)
        {
            return;
        }

        _lastUtilityHeroMode = _hero.Mode;
        _lastUtilityRangeMode = _hero.CurrentRangeMode;
        // scene: scene 값입니다.
        foreach (var scene in FindObjectsByType<PlayScene>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (scene.gameObject.scene == _hero.gameObject.scene && scene.SceneHero == _hero)
            {
                _utilityInputs = scene.Inputs;
                break;
            }
        }

        if (_utilityInputs == null)
        {
            _utilityInputs = _hero.GetComponentInParent<InputHandler>();
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_utilityInputs == null)
        {
            return;
        }

        _utilityInputs.UtilityInput += SelectOrUseUtility;
        _utilityInputs.MeleeModeInput += ClearUtilitySelection;
        _utilityInputs.RangeModeInput += ClearUtilitySelection;
        _utilityInputs.ToggleRangeModeInput += ClearUtilitySelection;
    }

    /// <summary>
    /// 이 HUD에서 구독한 유틸리티와 무기 선택 이벤트를 해제합니다.
    /// </summary>
    void DisconnectUtilityInputs()
    {
        if (!ReferenceEquals(_utilityInputs, null))
        {
            _utilityInputs.UtilityInput -= SelectOrUseUtility;
            _utilityInputs.MeleeModeInput -= ClearUtilitySelection;
            _utilityInputs.RangeModeInput -= ClearUtilitySelection;
            _utilityInputs.ToggleRangeModeInput -= ClearUtilitySelection;
        }

        _utilityInputs = null;
    }

    /// <summary>
    /// 장착 변경과 무기 전환에 맞춰 유틸리티 선택 상태를 갱신합니다.
    /// </summary>
    void UpdateUtilitySelection()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_hero == null)
        {
            return;
        }

        if (_hero.Mode != _lastUtilityHeroMode || _hero.CurrentRangeMode != _lastUtilityRangeMode)
        {
            ClearUtilitySelection();
        }

        _lastUtilityHeroMode = _hero.Mode;
        _lastUtilityRangeMode = _hero.CurrentRangeMode;
        if (_selectedUtilitySlot >= 0 && (GetUtilityItem(_selectedUtilitySlot) != _selectedUtilityItem || _inventory == null || _inventory.GetCount(_selectedUtilityItem) <= 0))
        {
            ClearUtilitySelection();
        }
    }

    /// <summary>
    /// 슬롯 번호 0·1에 장착된 아이템을 한 번의 입력으로 즉시 사용합니다.
    /// </summary>
    /// <param name="slotIndex">슬롯 인덱스 값입니다.</param>
    public void SelectOrUseUtility(int slotIndex)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!CanReadUtilityInput() || slotIndex < 0 || slotIndex >= 2)
        {
            return;
        }

        var item = GetUtilityItem(slotIndex); // 아이템 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (item == null || _inventory.GetCount(item) <= 0)
        {
            _utilityFeedback = "빈 슬롯";
            return;
        }

        _selectedUtilitySlot = slotIndex;
        _selectedUtilityItem = item;
        TryUseSelectedUtility();

        Refresh();
    }

    /// <summary>
    /// 히어로의 유틸리티 효과 적용에 성공했을 때만 보유 수량을 하나 차감합니다.
    /// </summary>
    public bool TryUseSelectedUtility()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!CanReadUtilityInput() || _selectedUtilitySlot < 0)
        {
            return false;
        }

        var item = GetUtilityItem(_selectedUtilitySlot); // 아이템 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (item == null || item != _selectedUtilityItem || _inventory.GetCount(item) <= 0)
        {
            ClearUtilitySelection();
            return false;
        }

        bool used = _hero.TryUseUtility(item); // 실제 효과 적용은 히어로가 처리합니다.

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!used)
        {
            _utilityFeedback = "사용 불가";
            return false;
        }

        _inventory.TryRemove(item, 1);
        _utilityFeedback = "사용 완료";
        if (GetUtilityItem(_selectedUtilitySlot) != item || _inventory.GetCount(item) <= 0)
        {
            ClearUtilitySelection();
        }

        Refresh();
        return true;
    }

    /// <summary>
    /// 무기 선택·바인딩 해제 시 유틸리티 선택과 안내를 초기화합니다.
    /// </summary>
    public void ClearUtilitySelection()
    {
        _selectedUtilitySlot = -1;
        _selectedUtilityItem = null;
        _utilityFeedback = "";
        RefreshUtilitySelection();
    }

    string GetUtilityKeyLabel(int slotIndex)
    {
        string label = _utilityInputs != null ? _utilityInputs.GetUtilityBindingDisplayString(slotIndex) : string.Empty;
        return string.IsNullOrEmpty(label) ? (slotIndex + 1).ToString() : label;
    }

    /// <summary>
    /// 선택한 유틸리티만 강조하며 나머지 슬롯은 무기 선택 상태와 동일하게 어둡게 표시합니다.
    /// </summary>
    void RefreshUtilitySelection()
    {
        // i: 반복 값입니다.
        for (int i = 0; i < 2; i++)
        {
            bool selected = i == _selectedUtilitySlot; // 선택 여부입니다.
            if (i < _utilitySlotCards.Length && _utilitySlotCards[i] != null)
            {
                _utilitySlotCards[i].color = selected ? Gold : Dark;
                RefreshSlotSkin(_utilitySlotCards[i], selected);
            }

            if (i < _utilitySlotGroups.Length && _utilitySlotGroups[i] != null)
            {
                _utilitySlotGroups[i].alpha = _utilitySlotCards[i] != null && _utilitySlotCards[i].transform.Find("Skin") != null ? 1f : selected ? 1f : _utilityInactiveAlpha;
            }

            if (i < _utilityKeyTexts.Length && _utilityKeyTexts[i] != null)
            {
                _utilityKeyTexts[i].text = GetUtilityKeyLabel(i);
            }
        }

        if (_utilityStatusText != null)
        {
            _utilityStatusText.text = !string.IsNullOrEmpty(_utilityFeedback) ? _utilityFeedback : GetUtilityKeyLabel(0) + "·" + GetUtilityKeyLabel(1) + " 사용";
        }
    }

    /// <summary>
    /// 조준 중 원거리 발사 여부를 기록합니다.
    /// </summary>
    void OnRangeAttackFired()
    {
        _firedDuringAim = true;
        SetAimVisible(false);
    }

    /// <summary>
    /// 조준 원호와 화살표의 표시 여부를 설정합니다.
    /// </summary>
    /// <param name="visible">화면이나 항목을 표시할지 여부입니다.</param>
    void SetAimVisible(bool visible)
    {
        if (_aimLineRenderer != null) _aimLineRenderer.enabled = false;
        if (_aimCurveRenderer != null) _aimCurveRenderer.enabled = visible;
        if (_aimArrowRenderer != null) _aimArrowRenderer.enabled = visible && _aimArrowSprite != null;
        if (_aimArc != null)
        {
            _aimArc.enabled = visible && (_aimLineSprite == null || _aimArrowSprite == null || _aimCurveMeshTemplate == null);
        }

        if (_aimArrow != null)
        {
            _aimArrow.enabled = visible && (_aimLineSprite == null || _aimArrowSprite == null || _aimCurveMeshTemplate == null);
        }
    }

    /// <summary>URP 카메라의 최종 위치·렌즈가 정해진 뒤 화면에 표시할 조준선을 갱신합니다.</summary>
    void OnAimCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if (isActiveAndEnabled && camera != null && camera == Camera.main) RefreshAim();
    }

    /// <summary>Built-in 렌더러에서도 카메라 렌더링 직전 표시를 같은 기준으로 맞춥니다.</summary>
    void OnAimCameraPreCull(Camera camera)
    {
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null && isActiveAndEnabled && camera == Camera.main)
            RefreshAim();
    }
    /// <summary>
    /// 현재 무기 사거리와 조준 방향에 맞춰 조준선을 갱신합니다.
    /// </summary>
    void RefreshAim()
    {
        bool show = _hero != null && _hero.isActiveAndEnabled && _aiming && !_firedDuringAim && _hero.Mode == UnderSeaHero.HeroMode.Range && Pointer.current != null && _hero.AimRadius > 0f && _lineWidth > 0f && (_hero.CurrentRangeMode != UnderSeaHero.RangeMode.Gig || _gig == null || !_gig.IsFlying); // 표시 여부입니다.
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!show)
        {
            SetAimVisible(false);
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        Vector2 pointerPosition = _aimPointerPositionAction != null && _aimPointerPositionAction.action != null && _aimPointerPositionAction.action.enabled
            ? _aimPointerPositionAction.action.ReadValue<Vector2>() : Pointer.current.position.ReadValue();
        if (!_hero.TryGetAimPoint(pointerPosition, out Vector3 aimPoint))
        {
            SetAimVisible(false);
            return;
        }

        Vector3 origin = _hero.AimPosition;
        Vector3 direction = (aimPoint - origin).normalized;
        Vector3 facing = _hero.FacingDirection; // 바라보는 방향 값입니다.
        float arcAngle = _hero.AimSectorAngle; // 원호 각도 값입니다.
        if (_aimLineSprite != null && _aimArrowSprite != null && _aimCurveMeshTemplate != null)
        {
            RefreshAimSprites(origin, direction, facing, arcAngle);
            return;
        }

        if (_aimMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default"); // 셰이더 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (shader == null)
            {
                return;
            }

            _aimMaterial = new Material(shader);
        }

        if (_aimArc == null)
        {
            _aimArc = CreateAimLine("AimArc");
        }

        if (_aimArrow == null)
        {
            _aimArrow = CreateAimLine("AimArrow");
        }

        int segments = Mathf.Clamp(_arcSegments, 8, 64); // 분할 구간 값입니다.
        _aimArc.positionCount = segments + 1;
        _aimArc.startWidth = _aimArc.endWidth = _lineWidth;
        _aimArrow.startWidth = _aimArrow.endWidth = _lineWidth;
        Vector3 side = new Vector3(-facing.y, facing.x, 0f); // 측면 값입니다.
        // i: 반복 값입니다.
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-arcAngle * 0.5f, arcAngle * 0.5f, (float)i / segments) * Mathf.Deg2Rad; // 각도 값입니다.
            _aimArc.SetPosition(i, origin + (facing * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * _hero.AimRadius);
        }

        Vector3 tip = origin + direction * (_hero.AimRadius + _arrowSize + _arrowGap);
        Vector3 arrowSide = new Vector3(-direction.y, direction.x, 0f); // 화살표 측면 값입니다.
        _aimArrow.positionCount = 3;
        _aimArrow.SetPosition(0, tip - direction * _arrowSize + arrowSide * _arrowSize * 0.55f);
        _aimArrow.SetPosition(1, tip);
        _aimArrow.SetPosition(2, tip - direction * _arrowSize - arrowSide * _arrowSize * 0.55f);
        SetAimVisible(true);

    }

    #endregion

    /// <summary>공통 조준 원점과 조준 반경·각도에 맞춰 곡선과 화살표를 표시합니다.</summary>
    void RefreshAimSprites(Vector3 origin, Vector3 direction, Vector3 facing, float arcAngle)
    {
        if (_aimCurveRenderer == null)
        {
            var go = new GameObject("AimCurveMesh");
            go.transform.SetParent(transform, false);
            _aimCurveMesh = Instantiate(_aimCurveMeshTemplate);
            _aimCurveMesh.name = "AimCurve (Runtime)";
            _aimCurveMesh.MarkDynamic();
            _aimCurveVertices = _aimCurveMesh.vertices;
            go.AddComponent<MeshFilter>().sharedMesh = _aimCurveMesh;
            _aimCurveMaterial = new Material(Shader.Find("Sprites/Default"));
            _aimCurveMaterial.mainTexture = _aimLineSprite.texture;
            _aimCurveRenderer = go.AddComponent<MeshRenderer>();
            _aimCurveRenderer.sharedMaterial = _aimCurveMaterial;
            _aimCurveRenderer.sortingOrder = 100;
            _aimCurveRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _aimCurveRenderer.receiveShadows = false;
        }
        if (_aimArrowRenderer == null) _aimArrowRenderer = CreateAimSprite("AimArrowSprite", 101);
        _aimArrowRenderer.sprite = _aimArrowSprite;

        // 곡선은 공통 조준 원점을 중심으로 허용 각도 전체를 표시합니다.
        // 캐릭터 높이나 아이콘 크기 때문에 각도 밖으로 길이를 늘리지 않습니다.
        float visualScale = Mathf.Clamp(_aimDisplayScale, .2f, 1f);
        float radius = _hero.AimRadius * visualScale;
        float lineWidth = _lineWidth * visualScale;
        float arrowGap = _arrowGap * visualScale;
        float arrowScale = _arrowSize * visualScale * 2f / Mathf.Max(.001f, _aimArrowSprite.bounds.size.y);
        float halfAngle = Mathf.Clamp(arcAngle * .5f, 0, 180);
        int segments = _aimCurveVertices.Length / 2 - 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments) * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            _aimCurveVertices[i * 2] = radial * Mathf.Max(0, radius - lineWidth * .5f);
            _aimCurveVertices[i * 2 + 1] = radial * (radius + lineWidth * .5f);
        }
        _aimCurveMesh.vertices = _aimCurveVertices;
        _aimCurveMesh.RecalculateBounds();
        float facingAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        _aimCurveRenderer.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, 0, facingAngle));
        SetAimSpriteWorldScale(_aimCurveRenderer.transform, 1);

        // 화살표의 뒤쪽 가장자리부터 곡선 바깥 테두리까지 지정된 간격을 확보합니다.
        // 화살표의 로컬 +X가 발사 방향이므로 어떤 각도로 회전해도 곡선을 침범하지 않습니다.
        float arrowBackExtent = Mathf.Max(0, -_aimArrowSprite.bounds.min.x * arrowScale);
        float arrowRadius = radius + lineWidth * .5f + arrowGap + arrowBackExtent;
        _aimArrowRenderer.transform.SetPositionAndRotation(origin + direction * arrowRadius,
            Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        SetAimSpriteWorldScale(_aimArrowRenderer.transform, arrowScale);
        SetAimVisible(true);

    }

    /// <summary>원본 색을 그대로 사용하는 표시 전용 SpriteRenderer를 만듭니다.</summary>
    SpriteRenderer CreateAimSprite(string objectName, int order)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.color = Color.white;
        sprite.sortingOrder = order;
        sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sprite.receiveShadows = false;
        return sprite;
    }

    /// <summary>부모 캐릭터나 Canvas의 배율이 달라도 지정한 월드 크기를 유지합니다.</summary>
    static void SetAimSpriteWorldScale(Transform target, float size)
    {
        Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(size / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            size / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(parentScale.z)));
    }

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 지정한 안쪽·바깥쪽 반경으로 런타임 고리 스프라이트를 만듭니다.
    /// </summary>
    /// <param name="inner">안쪽 값입니다.</param>
    /// <param name="outer">바깥쪽 값입니다.</param>
    static Sprite CreateRingSprite(float inner, float outer)
    {
        const int size = 128; // 크기 값입니다.
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Capture Ring (Runtime)",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        }; // 텍스처 값입니다.
        var pixels = new Color32[size * size]; // 픽셀 값입니다.
        // y: 세로 값입니다.
        for (int y = 0; y < size; y++)
        {
            // x: 가로 값입니다.
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / size; // 반경 값입니다.
                float alpha = Mathf.Clamp01((radius - inner) * size) * Mathf.Clamp01((outer - radius) * size); // 투명도 값입니다.
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect); // 스프라이트 값입니다.
        sprite.name = "Capture Ring (Runtime)";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    /// <summary>
    /// 일시정지·사망·입력 중지·텍스트 입력 중에는 유틸리티 단축키를 처리하지 않습니다.
    /// </summary>
    bool CanReadUtilityInput()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (!isActiveAndEnabled || _hero == null || !_hero.isActiveAndEnabled || _inventory == null || Time.timeScale <= 0f || _runModel != null && _runModel.CurrentOxygen <= 0f || _utilityInputs != null && !_utilityInputs.isActiveAndEnabled)
        {
            return false;
        }

        var selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null; // 선택 값입니다.
        return selected == null || selected.GetComponent<TMP_InputField>() == null && selected.GetComponent<UnityEngine.UI.InputField>() == null;
    }

    /// <summary>
    /// 현재 인벤토리에 장착된 슬롯의 아이템을 읽으며 장착 상태는 변경하지 않습니다.
    /// </summary>
    /// <param name="slotIndex">슬롯 인덱스 값입니다.</param>
    UtilityItemData GetUtilityItem(int slotIndex)
    {
        return _inventory == null ? null : slotIndex == 0 ? _inventory.FirstUtility : slotIndex == 1 ? _inventory.SecondUtility : null;
    }

    /// <summary>
    /// 조준 원호나 화살표에 사용할 선 렌더러를 생성합니다.
    /// </summary>
    /// <param name="objectName">오브젝트 이름 값입니다.</param>
    LineRenderer CreateAimLine(string objectName)
    {
        var lineObject = new GameObject(objectName); // 줄 오브젝트 값입니다.
        lineObject.transform.SetParent(transform, false);
        var line = lineObject.AddComponent<LineRenderer>(); // 줄 값입니다.
        line.sharedMaterial = _aimMaterial;
        line.useWorldSpace = true;
        line.startColor = line.endColor = Color.white;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    #endregion

}









