using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>휴대폰의 홈·종료 확인과 암전만 담당합니다. 씬 이동과 앱 종료는 외부 요청으로 전달합니다.</summary>
[DisallowMultipleComponent]
public sealed class PhoneNavigationView : MonoBehaviour
{
    [SerializeField] BaseView _baseView;
    [SerializeField] UnityEngine.UI.Button _homeButton, _quitButton, _confirmButton, _cancelButton;
    [SerializeField] GameObject _confirmation;
    [SerializeField] TMP_Text _title, _message;
    [SerializeField] CanvasGroup _blackout;
    [SerializeField, Min(0f)] float _returnFadeDuration = 1f;
    enum Request { None, Title, Quit }
    Request _request;
    bool _open, _transitioning;
    Tween _fade;
    GameObject _previousSelection;
    public event Action ReturnToTitleRequested;
    public event Action QuitRequested;
    public bool IsConfirmationOpen => _open;
    public bool IsTransitioning => _transitioning;

    void Awake()
    {
        _homeButton.onClick.AddListener(ShowReturnToTitle);
        _quitButton.onClick.AddListener(ShowQuit);
        _confirmButton.onClick.AddListener(Confirm);
        _cancelButton.onClick.AddListener(Cancel);
        UIEscapeView.Register(this, _cancelButton);
        ResetPresentation();
    }
    public void ShowReturnToTitle() { Show(Request.Title); }
    public void ShowQuit() { Show(Request.Quit); }
    void Show(Request request)
    {
        if (_open || _transitioning) return;
        _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        _baseView?.CloseAllApps();
        _request = request; _open = true;
        bool title = request == Request.Title;
        _title.text = title ? "시작화면" : "게임 종료";
        _message.text = title ? "시작화면으로 돌아가시겠습니까?" : "게임을 종료하시겠습니까?";
        _confirmation.transform.SetAsLastSibling();
        _confirmation.SetActive(true);
    }
    public void Cancel()
    {
        if (!_open || _transitioning) return;
        _open = false; _request = Request.None;
        _confirmation.SetActive(false);
        if (EventSystem.current != null && _previousSelection != null && _previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(_previousSelection);
    }
    public void Confirm()
    {
        if (!_open || _transitioning) return;
        var request = _request;
        _open = false; _request = Request.None;
        _confirmation.SetActive(false);
        if (request == Request.Quit) { QuitRequested?.Invoke(); return; }
        if (request != Request.Title) return;
        _transitioning = true;
        _blackout.gameObject.SetActive(true);
        _blackout.alpha = 0f; _blackout.blocksRaycasts = true; _blackout.interactable = true;
        _blackout.transform.SetAsLastSibling();
        _fade = _blackout.DOFade(1f, _returnFadeDuration).SetEase(Ease.InOutSine).SetUpdate(true)
            .OnComplete(() => { _fade = null; ReturnToTitleRequested?.Invoke(); });
    }
    /// <summary>외부 연결이 취소되거나 미리보기를 종료할 때 화면 표시만 초기화합니다.</summary>
    public void ResetPresentation()
    {
        _fade?.Kill(false); _fade = null;
        _open = _transitioning = false; _request = Request.None;
        if (_confirmation != null) _confirmation.SetActive(false);
        if (_blackout != null) { _blackout.alpha = 0; _blackout.blocksRaycasts = false; _blackout.gameObject.SetActive(false); }
    }
    void OnDisable() { ResetPresentation(); }
    void OnDestroy()
    {
        if (_homeButton != null) _homeButton.onClick.RemoveListener(ShowReturnToTitle);
        if (_quitButton != null) _quitButton.onClick.RemoveListener(ShowQuit);
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
        if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Cancel);
    }
}



