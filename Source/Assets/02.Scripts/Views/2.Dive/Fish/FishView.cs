using DG.Tweening;
using UnityEngine;

/// <summary>외부에서 전달한 표시 요청에 따라 물고기 머리 위의 아이콘과 연출만 처리합니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(2000)]
public class FishView : MonoBehaviour
{
    #region UI 연결 및 표시 설정

    [Header("표시 대상")]
    [SerializeField] Transform _ownerRoot; // UI가 따라갈 대상입니다. 비어 있으면 최초 부모를 사용합니다.
    [SerializeField] Transform _bodyRoot; // 실제 몸체의 경계를 계산할 대상입니다.

    [Header("월드 UI 연결")]
    [SerializeField] Canvas _worldCanvas;
    [SerializeField] UnityEngine.UI.Image _aggressiveIcon;
    [SerializeField] RectTransform _detectionRoot;
    [SerializeField] CanvasGroup _detectionGroup;
    [SerializeField] Camera _viewCamera;

    [Header("머리 위 배치")]
    [SerializeField, Min(0f)] float _topMargin = 0.2f;
    [SerializeField, Min(0.0001f)] float _worldUnitsPerPixel = 0.01f;
    [SerializeField, Min(0f)] float _iconGap = 12f;

    [Header("감지 아이콘 연출")]
    [SerializeField, Min(0.1f)] float _detectionDuration = 1f;
    [SerializeField, Min(0f)] float _appearDuration = 0.15f;
    [SerializeField, Min(0f)] float _fadeDuration = 0.2f;

    #endregion

    #region 런타임 표시 상태

    Renderer[] _bodyRenderers;
    Collider[] _bodyColliders;
    Sequence _detectionSequence;
    Vector2 _detectionPosition;
    float _detectionOffsetY;
    bool _isAggressive; // AI 상태가 아닌, 외부에서 요청한 아이콘 표시값입니다.
    bool _isVisible = true;
    bool _hasOwner;

    /// <summary>공격형 아이콘이 활성 Canvas에서 표시 중인지 반환합니다.</summary>
    public bool IsAggressiveVisible => IsCanvasVisible && _aggressiveIcon != null && _aggressiveIcon.gameObject.activeInHierarchy;

    /// <summary>감지 아이콘의 표시 연출이 활성 Canvas에서 진행 중인지 반환합니다.</summary>
    public bool IsDetectionVisible => IsCanvasVisible && _detectionRoot != null && _detectionRoot.gameObject.activeInHierarchy;

    bool IsCanvasVisible => isActiveAndEnabled && _worldCanvas != null && _worldCanvas.isActiveAndEnabled;

    #endregion

    #region 수명주기

    /// <summary>UI 참조와 최초 배치 대상을 준비하고 아이콘을 기본 숨김 상태로 만듭니다.</summary>
    void Awake()
    {
        if (_worldCanvas == null) _worldCanvas = GetComponent<Canvas>();
        if (_ownerRoot == null)
            _ownerRoot = _bodyRoot != null && _bodyRoot != transform
                ? _bodyRoot : _worldCanvas != null ? _worldCanvas.transform.parent : transform.parent;
        _hasOwner = _ownerRoot != null;
        CacheReferences();
        // Presenter가 먼저 전달한 표시 요청은 초기화 순서 때문에 지우지 않습니다.
        if (_detectionSequence == null) HideDetection();
        RefreshIconLayout();
    }

    /// <summary>재활성화된 UI의 배치를 준비합니다. 표시 조건은 외부에서 다시 전달합니다.</summary>
    void OnEnable()
    {
        CacheReferences();
        RefreshIconLayout();
        UpdateWorldPosition();
    }

    /// <summary>대상의 이동·애니메이션 이후 월드 위치만 따라갑니다. 감지나 게임 상태를 판단하지 않습니다.</summary>
    void LateUpdate()
    {
        if (_hasOwner && _ownerRoot == null)
        {
            SetVisible(false);
            return;
        }
        if (_isVisible) UpdateWorldPosition();
    }

    /// <summary>풀링이나 비활성화 시 이전 아이콘 상태와 이 View의 Tween을 정리합니다.</summary>
    void OnDisable() => ResetPresentation();

    /// <summary>제거되는 UI에 속한 Tween만 중단합니다.</summary>
    void OnDestroy() => _detectionSequence?.Kill();

    #endregion

    #region 외부 표시 요청

    /// <summary>따라갈 Transform과 선택적인 몸체·카메라를 연결합니다. 물고기 초기화는 수행하지 않습니다.</summary>
    public void BindTarget(Transform owner, Transform bodyRoot = null, Camera viewCamera = null)
    {
        // UI 자신이나 자신의 자식을 부모로 지정하는 순환 연결을 방지합니다.
        if (owner != null && (owner == transform || owner.IsChildOf(transform))) return;
        ResetPresentation();
        _ownerRoot = owner;
        _hasOwner = owner != null;
        _bodyRoot = bodyRoot;
        if (viewCamera != null) _viewCamera = viewCamera;
        CacheReferences();
        RefreshIconLayout();
        UpdateWorldPosition();
    }

    /// <summary>외부에서 판단한 공격형 아이콘 표시 여부를 적용합니다. 타입·전투 상태는 조회하지 않습니다.</summary>
    public void SetAggressiveVisible(bool visible)
    {
        _isAggressive = visible;
        RefreshIconLayout();
    }

    /// <summary>전체 월드 UI의 표시 여부를 적용하며, 숨길 때 감지 연출도 중단합니다.</summary>
    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (!visible) HideDetection();
        RefreshIconLayout();
        if (visible) UpdateWorldPosition();
    }

    /// <summary>외부의 감지·피격 알림을 같은 아이콘으로 표시하며, 설정된 시간 동안 등장·유지·퇴장 연출을 재생합니다.</summary>
    public void ShowDetection()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !_isVisible || _worldCanvas == null
            || (_hasOwner && _ownerRoot == null) || _detectionRoot == null || _detectionGroup == null) return;

        HideDetection();
        RefreshIconLayout();
        _detectionRoot.gameObject.SetActive(true);
        _detectionOffsetY = -10f;
        ApplyDetectionPosition();
        _detectionRoot.localScale = Vector3.one * 0.5f;
        _detectionGroup.alpha = 0f;
        float duration = Mathf.Max(0.1f, _detectionDuration);
        float appear = Mathf.Clamp(_appearDuration, 0f, duration * 0.5f);
        float fade = Mathf.Clamp(_fadeDuration, 0f, duration - appear);

        _detectionSequence = DOTween.Sequence().SetUpdate(true);
        _detectionSequence.Append(_detectionRoot.DOScale(1f, appear).SetEase(Ease.OutBack));
        // 등장 도중 공격형 표시가 바뀌어도 최신 아이콘 기준 위치에 연출 높이를 더합니다.
        _detectionSequence.Join(DOTween.To(() => _detectionOffsetY, offset =>
        {
            _detectionOffsetY = offset;
            ApplyDetectionPosition();
        }, 0f, appear).SetEase(Ease.OutCubic));
        _detectionSequence.Join(_detectionGroup.DOFade(1f, appear));
        _detectionSequence.AppendInterval(Mathf.Max(0f, duration - appear - fade));
        _detectionSequence.Append(_detectionGroup.DOFade(0f, fade));
        _detectionSequence.OnComplete(() =>
        {
            _detectionSequence = null;
            if (_detectionRoot != null) _detectionRoot.gameObject.SetActive(false);
        });
    }

    /// <summary>감지 연출을 즉시 중단하고 위치·크기·투명도를 기본값으로 복원합니다.</summary>
    public void HideDetection()
    {
        _detectionSequence?.Kill();
        _detectionSequence = null;
        _detectionOffsetY = 0f;
        if (_detectionGroup != null) _detectionGroup.alpha = 0f;
        if (_detectionRoot == null) return;
        ApplyDetectionPosition();
        _detectionRoot.localScale = Vector3.one;
        _detectionRoot.gameObject.SetActive(false);
    }

    /// <summary>전체 표시를 기본 상태로 복원합니다. 재사용 시 공격형·감지 표시를 남기지 않습니다.</summary>
    public void ResetPresentation()
    {
        _isAggressive = false;
        _isVisible = true;
        HideDetection();
        RefreshIconLayout();
    }

    #endregion

    #region UI 배치

    /// <summary>UI와 몸체 경계 계산에 필요한 참조만 수집합니다. 게임 데이터나 플레이어는 검색하지 않습니다.</summary>
    void CacheReferences()
    {
        if (_worldCanvas == null) _worldCanvas = GetComponent<Canvas>();
        if (_bodyRoot == null && _ownerRoot != null)
            _bodyRoot = _ownerRoot.Find("Facing/Visual") ?? _ownerRoot;
        var body = _bodyRoot != null ? _bodyRoot : transform;
        _bodyRenderers = body.GetComponentsInChildren<Renderer>(true);
        _bodyColliders = body.GetComponentsInChildren<Collider>(true);
        if (_viewCamera == null) _viewCamera = Camera.main;
        if (_worldCanvas != null) _worldCanvas.renderMode = RenderMode.WorldSpace;
        KeepCanvasUnderTarget();
    }

    /// <summary>요청한 표시 상태를 반영하고 아이콘 사이의 간격을 갱신합니다.</summary>
    void RefreshIconLayout()
    {
        bool visible = _isVisible && isActiveAndEnabled && (!_hasOwner || _ownerRoot != null);
        if (_worldCanvas != null) _worldCanvas.enabled = visible;
        float height = 0f;
        if (_aggressiveIcon != null)
        {
            _aggressiveIcon.gameObject.SetActive(_isAggressive && visible);
            var rect = _aggressiveIcon.rectTransform;
            float angle = rect.localEulerAngles.z * Mathf.Deg2Rad;
            height = Mathf.Abs(Mathf.Sin(angle)) * rect.rect.width + Mathf.Abs(Mathf.Cos(angle)) * rect.rect.height;
            rect.anchoredPosition = new Vector2(0f, height * 0.5f);
        }
        float detectionHalfHeight = _detectionRoot != null ? _detectionRoot.rect.height * 0.5f : 0f;
        _detectionPosition = new Vector2(0f, (_isAggressive && _aggressiveIcon != null ? height + _iconGap : 0f) + detectionHalfHeight);
        ApplyDetectionPosition();
    }

    /// <summary>아이콘 사이의 기준 간격과 감지 등장 연출의 높이를 합쳐 적용합니다.</summary>
    void ApplyDetectionPosition()
    {
        if (_detectionRoot != null)
            _detectionRoot.anchoredPosition = _detectionPosition + Vector2.up * _detectionOffsetY;
    }

    /// <summary>몸체 경계의 중앙 위에 Canvas를 배치하고 카메라 방향과 일정한 월드 크기를 유지합니다.</summary>
    void UpdateWorldPosition()
    {
        // 대상이 없는 독립 UI는 수동 위치를 그대로 유지합니다.
        if (_worldCanvas == null || _ownerRoot == null) return;
        if (_viewCamera == null) _viewCamera = Camera.main;
        Bounds bounds = GetBodyBounds();
        KeepCanvasUnderTarget();
        var ui = _worldCanvas.transform;
        ui.position = new Vector3(bounds.center.x, bounds.max.y + _topMargin, bounds.center.z);
        ui.rotation = _viewCamera != null ? _viewCamera.transform.rotation : Quaternion.identity;
        ui.localScale = Vector3.one;
        Vector3 inherited = ui.lossyScale;
        ui.localScale = new Vector3(CompensatedScale(inherited.x), CompensatedScale(inherited.y), CompensatedScale(inherited.z));
        _worldCanvas.worldCamera = _viewCamera;
    }

    /// <summary>표시 중인 렌더러의 경계를 합산하고, 렌더러가 없을 때만 실체 Collider를 사용합니다.</summary>
    Bounds GetBodyBounds()
    {
        Bounds bounds = new Bounds(_bodyRoot != null ? _bodyRoot.position : _ownerRoot.position, Vector3.zero);
        bool hasBounds = false;
        if (_bodyRenderers != null)
            foreach (var renderer in _bodyRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasBounds) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                hasBounds = true;
            }
        if (!hasBounds && _bodyColliders != null)
            foreach (var collider in _bodyColliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
                if (!hasBounds) bounds = collider.bounds;
                else bounds.Encapsulate(collider.bounds);
                hasBounds = true;
            }
        return bounds;
    }

    /// <summary>부모 배율을 상쇄해 UI 크기를 유지하고 0 배율의 나눗셈을 방지합니다.</summary>
    float CompensatedScale(float inheritedScale)
    {
        return Mathf.Abs(inheritedScale) > 0.00001f ? _worldUnitsPerPixel / inheritedScale : 0f;
    }

    /// <summary>Canvas를 대상의 자식으로 유지하여 대상과 함께 비활성화·제거되도록 합니다.</summary>
    void KeepCanvasUnderTarget()
    {
        if (_worldCanvas == null || _ownerRoot == null) return;
        var ui = _worldCanvas.transform;
        if (ui == _ownerRoot || _ownerRoot.IsChildOf(ui)) return;
        if (ui.parent != _ownerRoot) ui.SetParent(_ownerRoot, true);
    }

    #endregion
}
