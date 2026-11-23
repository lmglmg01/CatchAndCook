using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 스마트폰 아이콘의 호버 기울임만 처리합니다. 버튼 클릭 함수는 변경하지 않습니다.
/// </summary>
public class PhoneIconHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] RectTransform _target; // 호버 시 기울일 스마트폰 아이콘의 RectTransform을 연결합니다.
    [SerializeField] float _angle = 2f; // 호버 연출에서 좌우로 기울일 각도입니다.
    [SerializeField] float _roundTripDuration = .5f; // 한 번 좌우로 기울였다 돌아오는 데 걸리는 시간(초)입니다.
    [SerializeField] float _restDuration = 1.5f; // 기울임 반복 사이의 대기 시간(초)입니다.

    #endregion

    #region 런타임 상태

    Sequence _loop; // 반복 값입니다.
    Tween _return; // 반환 값입니다.
    Vector3 _originalAngles; // 원본 각도 값입니다.
    // _initialized: 초기화 여부입니다.
    // _hovering: 마우스 진입 중 여부입니다.
    bool _initialized, _hovering;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 호버 중 버튼이 비활성화되면 기울임을 정리합니다.
    /// </summary>
    void LateUpdate()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_hovering)
        {
            return;
        }

        var button = GetComponent<UnityEngine.UI.Button>(); // 버튼 값입니다.
        if (button != null && !button.IsInteractable())
        {
            ResetMotion();
        }
    }

    /// <summary>
    /// 아이콘이 꺼지면 기울임 연출을 초기화합니다.
    /// </summary>
    void OnDisable()
    {
        ResetMotion();
    }

    /// <summary>
    /// 아이콘이 제거되면 남은 트윈을 정리합니다.
    /// </summary>
    void OnDestroy()
    {
        ResetMotion();
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 아이콘의 기울임 대상과 원래 회전값을 보관합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        if (_target == null)
        {
            _target = transform as RectTransform;
        }

        _originalAngles = _target.localEulerAngles;
        _initialized = true;
    }

    #endregion

    #region 포인터 입력 및 요청

    /// <summary>
    /// 마우스가 진입하면 아이콘을 좌우로 기울입니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        Initialize();
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_hovering || !isActiveAndEnabled)
        {
            return;
        }

        var button = GetComponent<UnityEngine.UI.Button>(); // 버튼 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (button != null && !button.IsInteractable())
        {
            return;
        }

        _hovering = true;
        _return?.Kill();
        _loop?.Kill();
        _target.localEulerAngles = _originalAngles;
        // DOTween 연출: 원래 각도 → -2도 → +2도 → 원래 각도, 총 0.5초 왕복 후 1.5초 대기합니다.
        _loop = DOTween.Sequence().SetUpdate(true).Append(_target.DOLocalRotate(_originalAngles + Vector3.back * _angle, _roundTripDuration * .25f).SetEase(Ease.InOutCubic)).Append(_target.DOLocalRotate(_originalAngles + Vector3.forward * _angle, _roundTripDuration * .5f).SetEase(Ease.InOutCubic)).Append(_target.DOLocalRotate(_originalAngles, _roundTripDuration * .25f).SetEase(Ease.InOutCubic)).AppendInterval(_restDuration).SetLoops(-1, LoopType.Restart);
    }

    /// <summary>
    /// 마우스가 벗어나면 기울임을 끝내고 원래 각도로 돌아갑니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (!_initialized)
        {
            return;
        }

        _hovering = false;
        _loop?.Kill();
        _loop = null;
        _return?.Kill();
        // DOTween 연출: 호버를 벗어나면 원래 각도로 짧게 복귀합니다.
        _return = _target.DOLocalRotate(_originalAngles, .125f).SetEase(Ease.InOutCubic).SetUpdate(true);
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 기울임 트윈을 정리하고 아이콘 회전을 복원합니다.
    /// </summary>
    void ResetMotion()
    {
        _loop?.Kill();
        _return?.Kill();
        _loop = null;
        _return = null;
        _hovering = false;
        if (_initialized && _target != null)
        {
            _target.localEulerAngles = _originalAngles;
        }
    }

    #endregion

}
