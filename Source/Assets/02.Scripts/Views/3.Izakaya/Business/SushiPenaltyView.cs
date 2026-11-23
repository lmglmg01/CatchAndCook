using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>전달받은 패널티 문구만 표시합니다. 골드·영업시간 계산이나 차감은 수행하지 않습니다.</summary>
public sealed class SushiPenaltyView : MonoBehaviour
{
    #region 표시 연결
    [SerializeField] RectTransform _goldGroup, _timeGroup;
    [SerializeField] TMP_Text _goldText, _timeText;
    [SerializeField] Image _goldIcon, _timeIcon;
    readonly Queue<string> _goldMessages = new(), _timeMessages = new();
    Coroutine _goldRoutine, _timeRoutine;
    #endregion

    #region 문자열 표시 진입점
    /// <summary>이벤트에서 전달한 골드 패널티 문구를 그대로 표시합니다.</summary>
    public void ShowGoldPenalty(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !isActiveAndEnabled || _goldGroup == null || _goldText == null) return;
        _goldMessages.Enqueue(message);
        if (_goldRoutine == null)
            _goldRoutine = StartCoroutine(PlayMessages(_goldMessages, _goldGroup, _goldText,
                ShowGoldNotification, nameof(ShowGoldNotification), () => _goldRoutine = null));
    }

    /// <summary>운영비 누적 안내를 기존 상단 재화 팝업에 표시합니다. 보유 Gold 차감으로 해석하지 않습니다.</summary>
    public void ShowOperationCostPenalty(string message) => ShowGoldPenalty(message);
    /// <summary>이벤트에서 전달한 영업시간 패널티 문구를 그대로 표시합니다.</summary>
    public void ShowTimePenalty(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !isActiveAndEnabled || _timeGroup == null || _timeText == null) return;
        _timeMessages.Enqueue(message);
        if (_timeRoutine == null)
            _timeRoutine = StartCoroutine(PlayMessages(_timeMessages, _timeGroup, _timeText,
                ShowTimeNotification, nameof(ShowTimeNotification), () => _timeRoutine = null));
    }

    /// <summary>골드와 시간 문구가 함께 전달되면 두 표시를 동시에 요청합니다.</summary>
    public void ShowPenalty(string goldMessage, string timeMessage)
    {
        ShowGoldPenalty(goldMessage);
        ShowTimePenalty(timeMessage);
    }

    /// <summary>추후 전달받은 아이콘을 연결합니다. 아이콘 자리는 텍스트와 같은 부모로 연출됩니다.</summary>
    public void SetPenaltyIcons(Sprite gold, Sprite time)
    {
        if (_goldIcon != null) { _goldIcon.sprite = gold; _goldIcon.enabled = gold != null; }
        if (_timeIcon != null) { _timeIcon.sprite = time; _timeIcon.enabled = time != null; }
    }
    #endregion

    #region Canvas Editor 연출 연결
    /// <summary>Canvas Editor에서 골드 알림 묶음의 등장·하강·페이드 연출을 연결하는 함수입니다.</summary>
    void ShowGoldNotification() {
        // <UIDirector:ShowGoldNotification>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        { if (_goldGroup != null) _goldGroup.gameObject.SetActive(true); }
        global::UIDirectorView.Dispatch(this, "ShowGoldNotification", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>Canvas Editor에서 시간 알림 묶음의 등장·하강·페이드 연출을 연결하는 함수입니다.</summary>
    void ShowTimeNotification() {
        // <UIDirector:ShowTimeNotification>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        { if (_timeGroup != null) _timeGroup.gameObject.SetActive(true); }
        global::UIDirectorView.Dispatch(this, "ShowTimeNotification", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>같은 종류의 알림이 연속으로 와도 이전 문구를 덮지 않고 차례대로 표시합니다.</summary>
    IEnumerator PlayMessages(Queue<string> messages, RectTransform group, TMP_Text label,
        Action show, string method, Action finished)
    {
        while (messages.Count > 0 && group != null && label != null)
        {
            label.text = messages.Dequeue();
            show();
            yield return new WaitForSecondsRealtime(PresentationDuration(method));
            if (group != null) group.gameObject.SetActive(false);
        }
        messages.Clear(); finished();
    }

    /// <summary>편집기에 저장한 연출 길이를 읽어 연출이 끝난 뒤 표시를 정리합니다.</summary>
    float PresentationDuration(string method)
    {
        float result = 0;
        var director = GetComponent<UIDirectorView>();
        if (director != null)
            foreach (var binding in director.Bindings)
            {
                if (binding.owner != this || binding.enterMethod != method) continue;
                float previousStart = 0, previousEnd = 0;
                foreach (var effect in binding.enter)
                {
                    if (!effect.enabled) continue;
                    previousStart = UIDirectorView.Start(effect, previousStart, previousEnd);
                    previousEnd = previousStart + Mathf.Max(.01f, effect.duration);
                    result = Mathf.Max(result, previousEnd);
                }
            }
        return result > 0 ? result + .02f : 1.2f;
    }
    #endregion

    #region 표시 상태 정리
    /// <summary>기본 HUD에서는 두 알림 모두 숨겨 둡니다.</summary>
    void Awake()
    {
        SetPenaltyIcons(_goldIcon != null ? _goldIcon.sprite : null, _timeIcon != null ? _timeIcon.sprite : null);
        if (_goldGroup != null) _goldGroup.gameObject.SetActive(false);
        if (_timeGroup != null) _timeGroup.gameObject.SetActive(false);
    }

    /// <summary>씬이나 HUD가 닫히면 대기 알림과 자신이 실행한 표시를 정리합니다.</summary>
    void OnDisable() => ClearNotifications();

    /// <summary>연결한 초밥 View가 닫힐 때 대기 중인 표시와 자신이 실행한 연출을 정리합니다.</summary>
    public void ClearNotifications()
    {
        if (_goldRoutine != null) StopCoroutine(_goldRoutine);
        if (_timeRoutine != null) StopCoroutine(_timeRoutine);
        _goldRoutine = _timeRoutine = null;
        _goldMessages.Clear(); _timeMessages.Clear();
        if (_goldGroup != null) _goldGroup.gameObject.SetActive(false);
        if (_timeGroup != null) _timeGroup.gameObject.SetActive(false);
    }
    #endregion
}
