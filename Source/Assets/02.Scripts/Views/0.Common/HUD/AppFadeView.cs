using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Board 앱의 등장·퇴장을 담당하며 실제 비활성화는 퇴장 후 기존 View에 맡깁니다.
/// </summary>
public class AppFadeView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] CanvasGroup _group; // 앱의 등장·퇴장 연출과 입력을 제어할 CanvasGroup을 연결합니다.
    [SerializeField] float _duration = .2f; // 앱 열기·닫기 페이드에 걸리는 시간(초)입니다.

    #endregion

    #region 런타임 상태

    Tween _tween; // 트윈 값입니다.
    // _applying: 적용 중 여부입니다.
    // _shown: 노출 여부입니다.
    // _closing: 닫는 중 여부입니다.
    bool _applying, _shown, _closing;
    Action _hideAction; // 퇴장 연출이 끝나면 실행할 기존 닫기 함수입니다.
    readonly List<Action> _closed = new List<Action>(); // 퇴장이 끝난 뒤 호출할 완료 함수 목록입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 닫는 중 여부입니다.
    /// </summary>
    public bool IsClosing => _closing;

    /// <summary>
    /// 표시 중 여부입니다.
    /// </summary>
    public bool IsDisplaying => _shown || _closing;

    /// <summary>
    /// 노출 여부입니다.
    /// </summary>
    public bool IsShown => _shown && !_closing;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 외부에서 활성화한 앱에도 등장 연출을 적용합니다.
    /// </summary>
    void OnEnable()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (_applying || !Application.isPlaying)
        {
            return;
        }

        // 외부에서 SetActive로 연 앱도 같은 등장 연출을 사용합니다.
        TrySetVisible(true, null);
    }

    /// <summary>
    /// 진행 중인 페이드와 대기 콜백을 정리합니다.
    /// </summary>
    void OnDisable()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_applying)
        {
            return;
        }

        _tween?.Kill();
        _tween = null;
        _closed.Clear();
        _hideAction = null;
        _shown = false;
        _closing = false;
        RestoreGroup();
    }

    /// <summary>
    /// 페이드와 남아 있는 완료 콜백을 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        _tween?.Kill();
        _closed.Clear();
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 기존 표시 함수를 유지하며 앱의 열기·닫기 페이드를 시도합니다.
    /// </summary>
    /// <param name="shown">화면이나 항목을 표시할지 여부입니다.</param>
    /// <param name="original">원래 뷰의 열기·닫기 처리를 실행할 함수입니다.</param>
    /// <param name="finished">연출 종료 후 실행할 완료 함수입니다.</param>
    public bool TrySetVisible(bool shown, Action original, Action finished = null)
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (_applying || !Application.isPlaying)
        {
            return false;
        }

        if (_group == null)
        {
            _group = GetComponent<CanvasGroup>();
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_group == null)
        {
            return false;
        }

        if (shown)
        {
            bool visible = IsDisplaying && gameObject.activeInHierarchy; // 노출 여부입니다.
            _tween?.Kill();
            _closed.Clear();
            _hideAction = null;
            _shown = true;
            _closing = false;
            var entrance = GetComponent<CommonWindowEntranceView>();
            if (entrance != null && entrance.enabled)
            {
                if (entrance.IsExiting) entrance.CancelEntrance();
                // 기존 열기 처리를 한 번 실행하고 표시만 공통 배경·확대 페이드에 맡깁니다.
                _group.alpha = 1f;
                if (!entrance.IsEntering) { _group.interactable = true; _group.blocksRaycasts = true; }
                Apply(() => { gameObject.SetActive(true); original?.Invoke(); });
                entrance.PlayEntrance(finished);
                return true;
            }
            if (!visible)
            {
                _group.alpha = 0;
            }

            Apply(() =>
            {
                gameObject.SetActive(true);
                original?.Invoke();
            });
            _group.interactable = false;
            _group.blocksRaycasts = false;
            // DOTween 연출: 앱 등장은 투명 → 선명, OutExpo 0.2초입니다.
            _tween = _group.DOFade(1, _duration).SetEase(Ease.OutExpo).SetUpdate(true).OnComplete(() =>
            {
                _tween = null;
                _group.interactable = true;
                _group.blocksRaycasts = true;
                finished?.Invoke();
            });
        }
        else
        {
            if (finished != null)
            {
                _closed.Add(finished);
            }

            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (_closing)
            {
                return true;
            }

            _tween?.Kill();
            _shown = false;
            _hideAction = original;
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (!gameObject.activeInHierarchy)
            {
                FinishHide();
                return true;
            }

            _closing = true;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            var common = GetComponent<CommonWindowEntranceView>();
            if (common != null && common.TryPlayExit(FinishHide)) return true;
            // DOTween 연출: 앱 퇴장은 선명 → 투명, 완료 후에만 기존 닫기 함수를 실행합니다.
            _tween = _group.DOFade(0, _duration).SetEase(Ease.OutExpo).SetUpdate(true).OnComplete(FinishHide);
        }

        return true;
    }

    /// <summary>
    /// 재진입을 방지하면서 기존 뷰의 표시 함수를 실행합니다.
    /// </summary>
    /// <param name="original">원래 뷰의 열기·닫기 처리를 실행할 함수입니다.</param>
    void Apply(Action original)
    {
        _applying = true;
        try
        {
            original?.Invoke();
        }
        finally
        {
            _applying = false;
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 퇴장 연출 뒤 앱을 끄고 대기 중인 완료 함수를 호출합니다.
    /// </summary>
    void FinishHide()
    {
        _tween = null;
        var action = _hideAction; // 동작 값입니다.
        _hideAction = null;
        Apply(() =>
        {
            action?.Invoke();
            gameObject.SetActive(false);
        });
        _shown = false;
        _closing = false;
        RestoreGroup();
        var callbacks = _closed.ToArray(); // 콜백 목록 값입니다.
        _closed.Clear();
        // callback: 콜백 값입니다.
        foreach (var callback in callbacks)
        {
            callback?.Invoke();
        }
    }

    /// <summary>
    /// 연출을 중지하고 앱을 즉시 닫습니다.
    /// </summary>
    /// <param name="original">원래 뷰의 열기·닫기 처리를 실행할 함수입니다.</param>
    public void ForceHide(Action original)
    {
        GetComponent<CommonWindowEntranceView>()?.CancelEntrance();
        // DOTween 연출 정리: 씬 종료·부모 비활성화·초기화는 대기 없이 즉시 닫습니다.
        _tween?.Kill();
        _tween = null;
        _closed.Clear();
        _hideAction = null;
        _shown = false;
        _closing = false;
        Apply(() =>
        {
            original?.Invoke();
            gameObject.SetActive(false);
        });
        RestoreGroup();
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 캔버스 그룹의 투명도와 입력 허용 상태를 복원합니다.
    /// </summary>
    void RestoreGroup()
    {
        if (_group != null)
        {
            _group.alpha = 1;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }
    }

    #endregion

}
