using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>공통 화면의 배경 → 창 등장과 창 → 배경 퇴장을 Canvas Editor의 DOTween 설정으로 표시합니다.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(5000)]
public sealed class CommonWindowEntranceView : MonoBehaviour
{
    #region UI 연결 및 상태

    [SerializeField] UnityEngine.UI.Image _backdrop;
    [SerializeField] CanvasGroup _backdropGroup;
    [SerializeField] CanvasGroup _pageGroup;
    [SerializeField] RectTransform _windowMotion;
    UIDirectorView _director;
    Coroutine _completion;
    readonly List<Action> _finished = new();
    bool _entering, _played, _exiting, _applyingExit;
    bool _originalInteractable, _originalRaycasts;

    public bool IsEntering => _entering;
    public bool IsExiting => _exiting;
    public RectTransform WindowMotion => _windowMotion;

    #endregion

    #region 프리팹 구성과 Canvas Editor 연결

    /// <summary>기존 오브젝트를 재사용하여 표시용 묶음과 연출 설정을 준비합니다. 버튼 참조는 변경하지 않습니다.</summary>
    public void Configure(UnityEngine.UI.Image backdrop)
    {
        if (backdrop != null) _backdrop = backdrop;
        if (_backdrop == null) return;
        _backdrop.color = new Color(0f, 0f, 0f, .6f);
        _pageGroup = GetComponent<CanvasGroup>();
        if (_pageGroup == null) _pageGroup = gameObject.AddComponent<CanvasGroup>();
        _backdropGroup = _backdrop.GetComponent<CanvasGroup>();
        if (_backdropGroup == null) _backdropGroup = _backdrop.gameObject.AddComponent<CanvasGroup>();
        _director = GetComponent<UIDirectorView>();
        if (_director == null) _director = gameObject.AddComponent<UIDirectorView>();

        if (_windowMotion == null) _windowMotion = transform.Find("WindowMotion") as RectTransform;
        if (_windowMotion == null)
        {
            var root = new GameObject("WindowMotion", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.LayoutElement));
            root.layer = gameObject.layer;
            _windowMotion = root.GetComponent<RectTransform>(); _windowMotion.SetParent(transform, false);
            _windowMotion.anchorMin = Vector2.zero; _windowMotion.anchorMax = Vector2.one;
            _windowMotion.pivot = new Vector2(.5f, 0f);
            _windowMotion.offsetMin = _windowMotion.offsetMax = Vector2.zero;
            root.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        }
        if (_windowMotion.GetComponent<CanvasGroup>() == null) _windowMotion.gameObject.AddComponent<CanvasGroup>();

        var children = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child == _windowMotion || child == _backdrop.transform) continue;
            if (child.name.EndsWith("Backdrop", StringComparison.Ordinal)) continue;
            children.Add(child);
        }
        foreach (var child in children) child.SetParent(_windowMotion, false);

        var background = _director.Bindings.Find(b => b.owner == this && b.target == (RectTransform)_backdrop.transform)
            ?? _director.Bindings.Find(b => b.id == "common-window-background");
        if (background == null)
        {
            background = new UIDirectorView.Binding { id = "common-window-background", blockInput = false };
            background.enter.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Fade,
                duration = .2f / 1.5f, from = 0f, to = 1f, ease = Ease.Linear });
            _director.Bindings.Add(background);
        }
        // 여러 공통 창을 같은 Canvas에서 편집해도 서로의 연출을 덮어쓰지 않습니다.
        if (string.IsNullOrEmpty(background.id) || background.id == "common-window-background") background.id = Guid.NewGuid().ToString("N");
        background.owner = this; background.target = (RectTransform)_backdrop.transform;
        background.enterMethod = nameof(EnterCommonWindow);

        var window = _director.Bindings.Find(b => b.owner == this && b.target == _windowMotion)
            ?? _director.Bindings.Find(b => b.id == "common-window-content");
        if (window == null)
        {
            window = new UIDirectorView.Binding { id = "common-window-content" };
            window.enter.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Scale,
                delay = .2f / 1.5f, duration = .3f / 1.5f, from = 0f, to = 1f, ease = Ease.OutCubic });
            window.enter.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Fade,
                delay = .2f / 1.5f, duration = .3f / 1.5f, from = 0f, to = 1f, ease = Ease.OutSine });
            _director.Bindings.Add(window);
        }
        if (string.IsNullOrEmpty(window.id) || window.id == "common-window-content") window.id = Guid.NewGuid().ToString("N");
        window.owner = this; window.target = _windowMotion; window.enterMethod = nameof(EnterCommonWindow);
        background.exitMethod = window.exitMethod = nameof(ExitCommonWindow);
        // Canvas Editor의 퇴장 목록으로 저장합니다. 사용자가 편집한 효과는 다시 만들지 않습니다.
        if (background.exit.Count == 0)
            background.exit.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Fade,
                delay = .3f / 1.5f, duration = .2f / 1.5f, from = 1f, to = 0f, ease = Ease.Linear });
        if (window.exit.Count == 0)
        {
            window.exit.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Scale,
                duration = .3f / 1.5f, from = 1f, to = 0f, ease = Ease.InCubic });
            window.exit.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Fade,
                duration = .3f / 1.5f, from = 1f, to = 0f, ease = Ease.InSine });
            // 배경 퇴장 중 창이 원본 크기로 먼저 복원되어 다시 보이지 않도록 숨김을 유지합니다.
            window.exit.Add(new UIDirectorView.Effect { kind = UIDirectorView.EffectKind.Fade,
                delay = .3f / 1.5f, duration = .2f / 1.5f, from = 0f, to = 0f, ease = Ease.Linear });
        }
    }

    /// <summary>Canvas Editor에 저장한 배경과 창 효과를 함께 시작하는 연결 함수입니다.</summary>
    public void EnterCommonWindow()
    {
        // <UIDirector:EnterCommonWindow>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        { if (_windowMotion != null) _windowMotion.gameObject.SetActive(true); }
        global::UIDirectorView.Dispatch(this, "EnterCommonWindow", true, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>편집기에서 저장한 실제 효과의 끝 시간을 읽습니다.</summary>
    public float EntranceDuration
    {
        get
        {
            if (_director == null || !_director.enabled) return 0f;
            float total = 0f;
            foreach (var binding in _director.Bindings)
            {
                if (binding.owner != this || binding.enterMethod != nameof(EnterCommonWindow)) continue;
                float start = 0f, end = 0f;
                foreach (var effect in binding.enter)
                {
                    if (!effect.enabled) continue;
                    start = UIDirectorView.Start(effect, start, end); end = start + Mathf.Max(.01f, effect.duration);
                    total = Mathf.Max(total, end);
                }
            }
            return total;
        }
    }

    #endregion

    #region 표시와 입력 제어

    /// <summary>한 번의 활성화에서는 중복 재생하지 않고, 이미 진행 중이면 완료 요청만 추가합니다.</summary>
    public void PlayEntrance(Action finished = null)
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (_exiting) { CancelEntrance(); _played = false; }
        if (_played)
        {
            if (_entering) { if (finished != null) _finished.Add(finished); }
            else finished?.Invoke();
            return;
        }
        Configure(_backdrop);
        if (_backdrop == null || _windowMotion == null) { finished?.Invoke(); return; }
        _played = true; _entering = true;
        if (finished != null) _finished.Add(finished);
        _originalInteractable = _pageGroup.interactable; _originalRaycasts = _pageGroup.blocksRaycasts;
        _pageGroup.alpha = 1f; _pageGroup.interactable = false; _pageGroup.blocksRaycasts = true;
        _backdrop.enabled = true;
        EnterCommonWindow();
        _completion = StartCoroutine(CompleteEntrance());
    }

    /// <summary>기존 닫기 함수에서 호출합니다. 중복 닫기는 무시하고 원래 비활성화는 연출 뒤 한 번만 실행합니다.</summary>
    public bool TryPlayExit(Action finished)
    {
        if (_applyingExit || !Application.isPlaying || !isActiveAndEnabled) return false;
        if (_exiting) return true;
        Configure(_backdrop);
        if (_backdrop == null || _windowMotion == null) return false;
        CancelEntrance();
        _originalInteractable = _pageGroup.interactable; _originalRaycasts = _pageGroup.blocksRaycasts;
        _exiting = true;
        _pageGroup.interactable = false; _pageGroup.blocksRaycasts = true;
        _exitFinished = finished;
        ExitCommonWindow();
        return true;
    }

    Action _exitFinished;

    /// <summary>Canvas Editor의 퇴장 효과를 모두 마친 뒤 기존 닫기를 실행하는 연결 함수입니다.</summary>
    public void ExitCommonWindow()
    {
        // <UIDirector:ExitCommonWindow>
        // 기존 본문과 인자·버튼 연결을 유지합니다. 효과는 UIDirector에서 갱신합니다.
        void __UIDirectorOriginal()
        {
            var finished = _exitFinished; _exitFinished = null;
            _exiting = false; _played = false;
            RestoreInput();
            _applyingExit = true;
            try { finished?.Invoke(); }
            finally { _applyingExit = false; }
        }
        global::UIDirectorView.Dispatch(this, "ExitCommonWindow", false, __UIDirectorOriginal);
        // </UIDirector>
    }

    /// <summary>화면이 완전히 나타난 뒤에만 입력과 호출자의 완료 알림을 허용합니다.</summary>
    IEnumerator CompleteEntrance()
    {
        yield return new WaitForSecondsRealtime(EntranceDuration);
        _completion = null; _entering = false;
        RestoreInput();
        var callbacks = _finished.ToArray(); _finished.Clear();
        foreach (var callback in callbacks) callback?.Invoke();
    }

    /// <summary>닫기·앱 전환·부모 비활성화 때 이 화면의 효과만 중지하고 기본 배치를 복원합니다.</summary>
    public void CancelEntrance()
    {
        if (_completion != null) StopCoroutine(_completion);
        _completion = null;
        if (_director != null)
        {
            if (_windowMotion != null) _director.CancelEffects(this, _windowMotion);
            if (_backdrop != null) _director.CancelEffects(this, (RectTransform)_backdrop.transform);
        }
        if (_entering || _exiting) RestoreInput();
        _entering = false; _exiting = false; _played = false; _exitFinished = null; _finished.Clear();
    }

    /// <summary>기존 화면 루트의 입력 허용값을 복원합니다.</summary>
    void RestoreInput()
    {
        if (_pageGroup == null) return;
        _pageGroup.interactable = _originalInteractable; _pageGroup.blocksRaycasts = _originalRaycasts;
    }

    /// <summary>창 배경의 최종 불투명도를 통일합니다. 스마트폰 본체와 메뉴 책의 재질·배경은 보존합니다.</summary>
    public static void ApplyBackgroundOpacity(Transform root)
    {
        if (root == null) return;
        foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if (image.sprite == null || image.color.a <= 0f) continue;
            bool excluded = false;
            for (var t = image.transform; t != null; t = t.parent)
                if (t.name == "MenuBook" || t.name == "SmartPhone" || t.name == "AppGroup") { excluded = true; break; }
            if (excluded || image.name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var rect = image.rectTransform.rect;
            string name = image.name.ToLowerInvariant(), sprite = image.sprite.name.ToLowerInvariant();
            if (rect.width <= 200f || rect.height <= 100f || (rect.width >= 1800f && rect.height >= 1000f)) continue;
            if (!name.Contains("background") && !name.Contains("panel") && !sprite.Contains("background") && !sprite.StartsWith("rd45_") && !sprite.StartsWith("rd35_")) continue;
            var color = image.color; color.a = .98f; image.color = color;
        }
    }

    void OnEnable()
    {
        ApplyBackgroundOpacity(transform);
        if (Application.isPlaying) PlayEntrance();
    }
    void OnDisable() { CancelEntrance(); _played = false; }

    #endregion
}
