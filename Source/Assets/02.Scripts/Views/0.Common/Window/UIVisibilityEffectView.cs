using System;
using UnityEngine;

/// <summary>Canvas Editor에 저장한 등장·퇴장·반복 설정을 기존 View의 표시 함수에 연결합니다. 게임 판정은 하지 않습니다.</summary>
[DisallowMultipleComponent]
public sealed class UIVisibilityEffectView : MonoBehaviour
{
    [SerializeField] RectTransform _target;
    [SerializeField] GameObject[] _companions = Array.Empty<GameObject>();
    [SerializeField] bool _exitFromCurrentAlpha;
    bool _shown, _hiding;
    Action _afterHide;
    public bool IsShown => _shown && !_hiding;
    public bool IsHiding => _hiding;
    public RectTransform Target => _target;

    /// <summary>같은 표시 상태의 반복 호출은 무시하고, 퇴장 중 재등장하면 이전 닫기 요청을 취소합니다.</summary>
    public void Show()
    {
        if (_shown && !_hiding) return;
        _shown = true; _hiding = false; _afterHide = null;
        // <UIDirector:Show>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        { SetActive(true); }
        global::UIDirectorView.Dispatch(this, "Show", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>원래 숨김 처리는 연출이 끝난 뒤 한 번 실행합니다.</summary>
    public bool HideAfter(Action finished)
    {
        if (_hiding) return true;
        if (!_shown || !isActiveAndEnabled || _target == null || !_target.gameObject.activeInHierarchy) return false;
        _afterHide = finished;
        Hide();
        return true;
    }

    /// <summary>Canvas Editor의 퇴장을 재생하고 원래 비활성화·데이터 정리를 완료 시점에 실행합니다.</summary>
    public void Hide()
    {
        if (_hiding || (!_shown && (_target == null || !_target.gameObject.activeSelf))) return;
        _shown = false; _hiding = true;
        if (_exitFromCurrentAlpha && _target != null && _target.TryGetComponent<CanvasGroup>(out var group))
        {
            // 반복 효과를 멈추기 직전의 실제 밝기에서 자연스럽게 퇴장합니다. 저장된 설정은 변경하지 않습니다.
            var director = GetComponent<UIDirectorView>();
            if (director != null)
                foreach (var binding in director.Bindings)
                    if (binding.owner == this && binding.target == _target)
                        foreach (var effect in binding.exit)
                            if (effect.kind == UIDirectorView.EffectKind.Fade) { effect.from = group.alpha; effect.absoluteAlpha = true; }
        }
        // <UIDirector:Hide>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        {
            var finished = _afterHide; _afterHide = null;
            _hiding = false;
            SetActive(false);
            finished?.Invoke();
        }
        global::UIDirectorView.Dispatch(this, "Hide", false, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>이미 완료된 미션을 다시 열 때처럼 연출 없이 최종 표시 상태만 복원합니다.</summary>
    public void ShowStatic()
    {
        Cancel(); _shown = true;
        SetActive(true);
        if (_target != null && _target.TryGetComponent<CanvasGroup>(out var group)) group.alpha = 1f;
    }

    /// <summary>저산소처럼 실제 조건이 시작될 때만 Canvas Editor의 상시 반복을 실행합니다.</summary>
    public void PlayLoop()
    {
        if (_shown && !_hiding) return;
        ShowStatic();
        // <UIDirector:PlayLoop>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        { }
        global::UIDirectorView.Dispatch(this, "PlayLoop", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>씬 종료·대상 교체에서는 다른 View의 Tween에 영향 없이 이 표시만 즉시 정리합니다.</summary>
    public void HideImmediate() { Cancel(); SetActive(false); }
    void Cancel()
    {
        GetComponent<UIDirectorView>()?.CancelEffects(this, _target);
        _shown = _hiding = false; _afterHide = null;
    }
    void SetActive(bool visible)
    {
        if (_target != null) _target.gameObject.SetActive(visible);
        foreach (var companion in _companions) if (companion != null) companion.SetActive(visible);
    }
    void OnDisable() { Cancel(); foreach (var companion in _companions) if (companion != null) companion.SetActive(false); }
}
