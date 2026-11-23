using DG.Tweening;
using UnityEngine;

/// <summary>
/// 메모 상단과 버튼을 고정하고 본문을 아래 방향으로 접고 펼치는 연출입니다.
/// </summary>
public class MemoFoldView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] RectTransform _body; // 메모 배경과 목록을 모두 포함한 MemoBody를 연결합니다. 위쪽 피벗을 기준으로 접힙니다.
    [SerializeField] CanvasGroup _group; // MemoBody의 CanvasGroup을 연결합니다. 본문 투명도와 입력만 제어하며 접기 버튼은 포함하지 않습니다.
    [SerializeField] float _duration = .15f; // 펼침·접힘 연출 시간(초)입니다.

    #endregion

    #region 런타임 상태

    Sequence _tween; // 트윈 값입니다.
    RectTransform _toggleButtonRect; // 본문과 같은 시간표로 회전할 메모 버튼입니다.
    Vector3 _originalScale; // 원본 배율 값입니다.
    // _initialized: 초기화 여부입니다.
    // _expanded: 펼침 여부입니다.
    bool _initialized, _expanded;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 메모가 숨겨지면 진행 중인 연출을 종료하고 표시 상태를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
        ApplyState();
    }

    /// <summary>
    /// View가 제거되면 이 View가 생성한 연출을 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        _tween?.Kill();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 기존 펼침 함수를 유지하면서 같은 Sequence에서 회전할 버튼을 연결합니다.
    /// </summary>
    /// <param name="expanded">펼침 여부입니다.</param>
    /// <param name="toggleButton">기존 On/OffButton의 RectTransform입니다.</param>
    public void SetExpanded(bool expanded, RectTransform toggleButton)
    {
        _toggleButtonRect = toggleButton;
        SetExpanded(expanded);
    }

    /// <summary>
    /// 상단 위치를 유지하면서 본문 세로 크기와 투명도를 전환합니다.
    /// </summary>
    /// <param name="expanded">펼침 여부입니다.</param>
    public void SetExpanded(bool expanded)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_body == null || _group == null)
        {
            return;
        }

        // 같은 상태 갱신으로 진행 중인 접기 연출이 계속 처음부터 재시작되지 않게 합니다.
        if (_initialized && _expanded == expanded) return;
        bool initial = !_initialized; // 초기 여부입니다.
        if (initial)
        {
            SetTopPivot();
            _initialized = true;
            _originalScale = _body.localScale;
        }

        _expanded = expanded;
        _tween?.Kill();
        _tween = null;
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (initial || !Application.isPlaying || !isActiveAndEnabled)
        {
            ApplyState();
            return;
        }

        _body.gameObject.SetActive(true);
        _group.interactable = expanded;
        _group.blocksRaycasts = expanded;
        // DOTween 연출: 위쪽 피벗 기준 본문 펼침/접힘과 페이드를 OutQuint 0.15초로 함께 재생합니다.
        _tween = DOTween.Sequence().SetUpdate(true).Join(_body.DOScaleY(expanded ? _originalScale.y : 0, _duration).SetEase(Ease.OutQuint)).Join(_group.DOFade(expanded ? 1 : 0, _duration).SetEase(Ease.OutQuint)).OnComplete(() =>
        {
            _tween = null;
            ApplyState();
        });
        if (_toggleButtonRect != null)
        {
            Vector3 rotation = _toggleButtonRect.localEulerAngles; // 기존 X·Y 회전을 유지한 목표 각도입니다.
            rotation.z = expanded ? 270f : 90f;
            // DOTween 연출: 본문의 시간·OutQuint 이징과 함께 회전합니다. 펼칠 때는 왼쪽(+Z), 접을 때는 오른쪽(-Z)으로 돕니다.
            // FastBeyond360으로 180도 회전 방향을 명시하며, 연속 클릭은 현재 각도에서 반대 목표까지 이어집니다.
            _tween.Join(_toggleButtonRect.DOLocalRotate(rotation, _duration, RotateMode.FastBeyond360).SetEase(Ease.OutQuint));
        }
    }

    /// <summary>
    /// 현재 보이는 위치를 바꾸지 않고 본문의 피벗을 위쪽으로 맞춥니다.
    /// </summary>
    void SetTopPivot()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (Mathf.Approximately(_body.pivot.y, 1f))
        {
            return;
        }

        var corners = new Vector3[4]; // 모서리 값입니다.
        _body.GetWorldCorners(corners);
        Vector3 topLeft = corners[1]; // 위쪽 왼쪽 값입니다.
        _body.pivot = new Vector2(_body.pivot.x, 1f);
        _body.GetWorldCorners(corners);
        _body.position += topLeft - corners[1];
    }

    /// <summary>
    /// 연출 완료 또는 비활성화 시 본문의 최종 크기와 표시·입력 상태를 적용합니다.
    /// </summary>
    void ApplyState()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (!_initialized || _body == null || _group == null)
        {
            return;
        }

        _body.localScale = new Vector3(_originalScale.x, _expanded ? _originalScale.y : 0, _originalScale.z);
        _group.alpha = _expanded ? 1 : 0;
        _group.interactable = _expanded;
        _group.blocksRaycasts = _expanded;
        _body.gameObject.SetActive(_expanded);
        if (_toggleButtonRect != null)
        {
            Vector3 rotation = _toggleButtonRect.localEulerAngles; // 초기 표시·연출 종료·비활성화 시 최종 방향을 보장합니다.
            rotation.z = _expanded ? -90f : 90f;
            _toggleButtonRect.localEulerAngles = rotation;
        }
    }

    #endregion

}
