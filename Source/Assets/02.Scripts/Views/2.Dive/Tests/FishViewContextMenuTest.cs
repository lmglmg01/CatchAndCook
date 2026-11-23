using UnityEngine;

/// <summary>캡슐의 FishView 표시 함수를 수동 호출하는 에디터 전용 테스트입니다. 자동 실행이나 게임 상태 변경은 없습니다.</summary>
[DisallowMultipleComponent]
public sealed class FishViewContextMenuTest : MonoBehaviour
{
#if UNITY_EDITOR
    #region 테스트 연결

    [Header("캡슐에 연결된 표시 View")]
    [SerializeField] FishView _view;
    FishView _boundView;
    Coroutine _repeatRoutine;
    Coroutine _noticeRoutine;
    int _noticeCount;

    /// <summary>플레이 모드에서 이 캡슐의 자식 View만 연결합니다. 다른 대상의 실제 UI는 제어하지 않습니다.</summary>
    bool TryPrepareView()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            Debug.Log("[FishView 테스트] 플레이 모드에서 캡슐의 ContextMenu를 실행해주세요.", this);
            return false;
        }
        if (_view == null) _view = GetComponentInChildren<FishView>(true);
        if (_view == null || !_view.isActiveAndEnabled || _view.gameObject.scene != gameObject.scene
            || !_view.transform.IsChildOf(transform))
        {
            Debug.LogWarning("[FishView 테스트] 이 캡슐의 활성 자식 FishView를 연결해주세요.", this);
            return false;
        }
        if (_boundView != _view)
        {
            _view.BindTarget(transform);
            _boundView = _view;
        }
        return true;
    }

    #endregion

    #region 공격형 표시 테스트

    /// <summary>공격형 상시 표시만 켭니다.</summary>
    [ContextMenu("FishView 테스트/1. 공격형 표시 켜기")]
    void ShowAggressive()
    {
        if (!TryPrepareView()) return;
        _view.SetVisible(true);
        _view.SetAggressiveVisible(true);
    }

    /// <summary>공격형 상시 표시만 끕니다.</summary>
    [ContextMenu("FishView 테스트/2. 공격형 표시 끄기")]
    void HideAggressive()
    {
        if (TryPrepareView()) _view.SetAggressiveVisible(false);
    }

    #endregion

    #region 감지·피격 공통 알림 테스트

    /// <summary>감지·피격 알림을 한 번 재생합니다. 실제 감지·피해 처리는 실행하지 않습니다.</summary>
    [ContextMenu("FishView 테스트/3. 감지·피격 알림 재생")]
    void PlayNotice()
    {
        if (!TryPrepareView()) return;
        // 네이티브 ContextMenu를 닫은 첫 프레임에는 메뉴 대기 시간이 unscaledDeltaTime에 포함됩니다.
        // 그 프레임의 DOTween 갱신이 끝난 다음 호출해야 1초짜리 연출이 즉시 종료되지 않습니다.
        if (_noticeRoutine != null) StopCoroutine(_noticeRoutine);
        _noticeRoutine = StartCoroutine(PlayNoticeAfterMenu());
    }

    /// <summary>ContextMenu가 닫힌 뒤 큰 프레임 시간이 처리된 다음 실제 View를 호출합니다.</summary>
    System.Collections.IEnumerator PlayNoticeAfterMenu()
    {
        yield return null;
        _noticeRoutine = null;
        if (!TryPrepareView()) yield break;
        _view.SetVisible(true);
        _view.ShowDetection();
        Debug.Log($"[FishView 테스트] 감지·피격 알림 호출 #{++_noticeCount}", this);
    }

    /// <summary>재생 중인 공통 알림을 즉시 숨깁니다.</summary>
    [ContextMenu("FishView 테스트/4. 감지·피격 알림 숨기기")]
    void HideNotice()
    {
        CancelPendingNotice();
        if (TryPrepareView()) _view.HideDetection();
    }

    #endregion

    #region 전체 표시 정리 테스트

    /// <summary>공격형·공통 알림과 전체 Canvas를 숨깁니다.</summary>
    [ContextMenu("FishView 테스트/5. 전체 UI 숨기기")]
    void HideAll()
    {
        CancelPendingNotice();
        if (TryPrepareView()) _view.SetVisible(false);
    }

    /// <summary>두 아이콘과 연출을 초기 표시 상태로 복원합니다.</summary>
    [ContextMenu("FishView 테스트/6. 표시 초기화")]
    void ResetDisplay()
    {
        CancelPendingNotice();
        if (TryPrepareView()) _view.ResetPresentation();
    }

    #endregion

    #region 재진입 반복 확인

    /// <summary>실제 센서 없이 외부 재진입 알림을 세 번 전달해 View의 반복 재생만 확인합니다.</summary>
    [ContextMenu("FishView 테스트/7. 재진입 알림 3회 반복")]
    void RepeatReentryNotices()
    {
        if (!TryPrepareView()) return;
        if (_repeatRoutine != null) StopCoroutine(_repeatRoutine);
        _repeatRoutine = StartCoroutine(ReplayReentryNotices());
    }

    /// <summary>이전 연출의 종료를 기다린 후 다음 진입 알림을 전달합니다. 게임의 감지 상태는 변경하지 않습니다.</summary>
    System.Collections.IEnumerator ReplayReentryNotices()
    {
        var serializedView = new UnityEditor.SerializedObject(_view);
        float duration = Mathf.Max(.1f, serializedView.FindProperty("_detectionDuration").floatValue);
        for (int i = 0; i < 3; i++)
        {
            if (!TryPrepareView()) break;
            PlayNotice();
            yield return new WaitForSecondsRealtime(duration + .5f);
        }
        _repeatRoutine = null;
    }

    /// <summary>메뉴 종료를 기다리는 이 테스트 컴포넌트의 알림 요청만 취소합니다.</summary>
    void CancelPendingNotice()
    {
        if (_noticeRoutine != null) StopCoroutine(_noticeRoutine);
        _noticeRoutine = null;
    }

    /// <summary>테스트 컴포넌트를 끄면 직접 실행했던 반복 확인과 대기 중인 요청만 중단합니다.</summary>
    void OnDisable()
    {
        CancelPendingNotice();
        if (_repeatRoutine != null) StopCoroutine(_repeatRoutine);
        _repeatRoutine = null;
    }

    #endregion
#endif
}
