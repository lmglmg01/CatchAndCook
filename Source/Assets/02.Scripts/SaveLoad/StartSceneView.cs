using System;
using TMPro;
using UnityEngine;

public sealed class StartSceneView : MonoBehaviour
{
    [Header("시작 화면")]
    [SerializeField] GameObject _logo;
    [SerializeField] GameObject _logoSubtitle;
    [SerializeField] GameObject _mainPanel;
    [SerializeField] GameObject _slotPanel;
    [SerializeField] GameObject _settingsPanel;
    [SerializeField] UnityEngine.UI.Button _newGameButton;
    [SerializeField] UnityEngine.UI.Button _loadButton;
    [SerializeField] UnityEngine.UI.Button _continueButton;
    [SerializeField] UnityEngine.UI.Button _backButton;
    [SerializeField] UnityEngine.UI.Button _settingsButton;
    [SerializeField] UnityEngine.UI.Button _settingsBackButton;
    [SerializeField] UnityEngine.UI.Button _quitButton;
    [SerializeField] TMP_Text _messageText;

    [Header("저장 슬롯")]
    [SerializeField] UnityEngine.UI.Button[] _slotButtons;
    [SerializeField] TMP_Text[] _slotLabels;
    [SerializeField] TMP_Text[] _slotDetails;
    [SerializeField] TMP_Text[] _slotStatusLabels;
    [SerializeField] GameObject[] _slotSelectionFrames;
    [SerializeField] UnityEngine.UI.Button _loadSelectedButton;
    [SerializeField] UnityEngine.UI.Button _deleteSelectedButton;

    [Header("플레이어 이름 입력")]
    [SerializeField] GameObject _namePopup;
    [SerializeField] TMP_InputField _nameInput;
    [SerializeField] UnityEngine.UI.Button _nameConfirmButton;
    [SerializeField] UnityEngine.UI.Button _nameCancelButton;
    [SerializeField] TMP_Text _nameErrorText;

    [Header("이어하기 확인")]
    [SerializeField] GameObject _continuePopup;
    [SerializeField] TMP_Text _continueMessage;
    [SerializeField] TMP_Text _continueErrorText;
    [SerializeField] UnityEngine.UI.Button _continueConfirmButton;
    [SerializeField] UnityEngine.UI.Button _continueCancelButton;

    [Header("저장 데이터 삭제 확인")]
    [SerializeField] GameObject _deletePopup;
    [SerializeField] TMP_Text _deleteMessage;
    [SerializeField] TMP_Text _deleteErrorText;
    [SerializeField] UnityEngine.UI.Button _deleteConfirmButton;
    [SerializeField] UnityEngine.UI.Button _deleteCancelButton;

    [Header("게임 종료 확인")]
    [SerializeField] GameObject _quitPopup;
    [SerializeField] UnityEngine.UI.Button _quitConfirmButton;
    [SerializeField] UnityEngine.UI.Button _quitCancelButton;

    int _pendingTransitions;
    bool _initializing;
    bool _leavingScene;

    int _selectedSlot;
    int _continueSlot;
    int _deleteSlot;

    void Awake()
    {
        UIEscapeView.Register(this, _backButton, _settingsBackButton, _nameCancelButton, _continueCancelButton, _deleteCancelButton, _quitCancelButton);
        if (_logo == null || _mainPanel == null || _slotPanel == null || _settingsPanel == null
            || _newGameButton == null || _loadButton == null || _continueButton == null
            || _backButton == null || _settingsButton == null || _settingsBackButton == null
            || _quitButton == null || _messageText == null
            || _namePopup == null || _nameInput == null || _nameConfirmButton == null
            || _nameCancelButton == null || _nameErrorText == null
            || _continuePopup == null || _continueMessage == null || _continueErrorText == null
            || _continueConfirmButton == null || _continueCancelButton == null
            || _deletePopup == null || _deleteMessage == null || _deleteErrorText == null
            || _deleteConfirmButton == null || _deleteCancelButton == null
            || _quitPopup == null || _quitConfirmButton == null || _quitCancelButton == null
            || _loadSelectedButton == null || _deleteSelectedButton == null
            || !HasSlots(_slotButtons) || !HasSlots(_slotLabels) || !HasSlots(_slotDetails)
            || !HasSlots(_slotStatusLabels) || !HasSlots(_slotSelectionFrames))
        {
            Debug.LogError("StartSceneView의 UI 참조가 누락되었습니다.", this);
            enabled = false;
            return;
        }

        _newGameButton.interactable = true;
        Bind(_newGameButton, StartNewGame);
        Bind(_loadButton, ShowSlots);
        Bind(_continueButton, ShowContinueConfirmation);
        Bind(_settingsButton, ShowSettings);
        Bind(_backButton, ShowMain);
        Bind(_quitButton, ShowQuitConfirmation);
        Bind(_loadSelectedButton, LoadSelectedSlot);
        Bind(_deleteSelectedButton, ShowDeleteConfirmation);
        _nameInput.richText = false;
        _nameInput.characterLimit = GameSaveService.MaxPlayerNameLength;
        _nameInput.onValueChanged.AddListener(UpdateNameValidation);
        _nameInput.onSubmit.AddListener(OnNameSubmitted);
        Bind(_nameConfirmButton, ConfirmNewGame);
        Bind(_nameCancelButton, CancelNewGame);
        Bind(_continueConfirmButton, ConfirmContinue);
        Bind(_continueCancelButton, CancelContinue);
        Bind(_deleteConfirmButton, ConfirmDelete);
        Bind(_deleteCancelButton, CancelDelete);
        Bind(_quitConfirmButton, QuitGame);
        Bind(_quitCancelButton, CancelQuit);
        for (int i = 0; i < GameSaveService.MaxSlots; i++)
        {
            int slot = i + 1;
            Bind(_slotButtons[i], () => SelectSlot(slot));
        }
        _initializing = true;
        ShowMain();
        _initializing = false;
        Refresh();
    }

    void Bind(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick.AddListener(() =>
        {
            if (_pendingTransitions == 0 && !_leavingScene) action();
        });
    }

    void SetPanelVisible(GameObject panel, bool visible, Action finished = null)
    {
        if (panel == null) { finished?.Invoke(); return; }
        if (_initializing || !Application.isPlaying)
        {
            panel.SetActive(visible);
            finished?.Invoke();
            return;
        }
        var fade = panel.GetComponent<AppFadeView>();
        if (!visible && !panel.activeSelf || visible && panel.activeSelf && fade != null && fade.IsShown)
        {
            finished?.Invoke();
            return;
        }
        if (fade != null)
        {
            _pendingTransitions++;
            if (fade.TrySetVisible(visible, null, () =>
            {
                _pendingTransitions = Mathf.Max(0, _pendingTransitions - 1);
                finished?.Invoke();
            })) return;
            _pendingTransitions = Mathf.Max(0, _pendingTransitions - 1);
        }
        panel.SetActive(visible);
        finished?.Invoke();
    }

    static bool HasSlots<T>(T[] values) where T : UnityEngine.Object
    {
        if (values == null || values.Length != GameSaveService.MaxSlots) return false;
        foreach (T value in values) if (value == null) return false;
        return true;
    }

    void ShowMain()
    {
        SetPanelVisible(_slotPanel, false);
        SetPanelVisible(_settingsPanel, false);
        SetPanelVisible(_namePopup, false);
        SetPanelVisible(_continuePopup, false);
        SetPanelVisible(_deletePopup, false);
        SetPanelVisible(_quitPopup, false);
        SetPanelVisible(_logo, true);
        if (_logoSubtitle != null) SetPanelVisible(_logoSubtitle, true);
        SetPanelVisible(_mainPanel, true);
        _messageText.text = string.Empty;
        _continueButton.interactable = GameSaveService.HasAnySave;
    }

    void ShowSlots()
    {
        SetPanelVisible(_mainPanel, false);
        SetPanelVisible(_logo, false);
        if (_logoSubtitle != null) SetPanelVisible(_logoSubtitle, false);
        SetPanelVisible(_settingsPanel, false);
        SetPanelVisible(_slotPanel, true);
        _selectedSlot = GameSaveService.GetLatestSlotInfo().Slot;
        _messageText.text = string.Empty;
        Refresh();
    }

    void ShowSettings()
    {
        SetPanelVisible(_settingsPanel, true);
        _messageText.text = string.Empty;
    }

    void StartNewGame()
    {
        _messageText.text = string.Empty;
        _nameInput.SetTextWithoutNotify(string.Empty);
        _nameErrorText.text = string.Empty;
        _nameConfirmButton.interactable = false;
        SetPanelVisible(_namePopup, true, FocusNameInput);
    }

    void FocusNameInput()
    {
        _nameInput.Select();
        _nameInput.ActivateInputField();
    }

    void UpdateNameValidation(string value)
    {
        bool valid = GameSaveService.TryValidatePlayerName(value, out _, out string error);
        _nameConfirmButton.interactable = valid;
        _nameErrorText.text = string.IsNullOrWhiteSpace(value) ? string.Empty : error ?? string.Empty;
    }

    void OnNameSubmitted(string value)
    {
        if (_pendingTransitions > 0 || _leavingScene) return;
        if (_nameInput.wasCanceled) CancelNewGame();
        else ConfirmNewGame();
    }

    void ConfirmNewGame()
    {
        if (!_namePopup.activeSelf) return;
        if (!GameSaveService.TryValidatePlayerName(_nameInput.text, out _, out string error))
        {
            _nameErrorText.text = error;
            return;
        }
        SetPanelVisible(_namePopup, false, () =>
        {
            if (GameSaveService.TryStartNewGame(_nameInput.text, out string startError)) _leavingScene = true;
            else
            {
                _nameErrorText.text = startError;
                SetPanelVisible(_namePopup, true, FocusNameInput);
            }
        });
    }

    void CancelNewGame()
    {
        SetPanelVisible(_namePopup, false, () => _newGameButton.Select());
    }

    void ShowContinueConfirmation()
    {
        SaveSlotInfo latest = GameSaveService.GetLatestSlotInfo();
        if (!latest.CanLoad) { _messageText.text = "이어할 수 있는 저장 데이터가 없습니다."; return; }
        _continueSlot = latest.Slot;
        _continueMessage.text = $"'{latest.PlayerName}'의 플레이를\n이어하시겠습니까?";
        _continueErrorText.text = string.Empty;
        SetPanelVisible(_continuePopup, true);
    }

    void ConfirmContinue()
    {
        SetPanelVisible(_continuePopup, false, () =>
        {
            if (GameSaveService.TryLoadSlot(_continueSlot, out string error)) _leavingScene = true;
            else
            {
                _continueErrorText.text = error;
                SetPanelVisible(_continuePopup, true);
            }
        });
    }

    void CancelContinue()
    {
        SetPanelVisible(_continuePopup, false, () => _continueButton.Select());
    }

    void SelectSlot(int slot)
    {
        _selectedSlot = slot;
        _messageText.text = string.Empty;
        RefreshSelection();
    }

    void LoadSelectedSlot()
    {
        if (_selectedSlot == 0) return;
        SetPanelVisible(_slotPanel, false, () =>
        {
            if (GameSaveService.TryLoadSlot(_selectedSlot, out string error)) _leavingScene = true;
            else
            {
                _messageText.text = error;
                SetPanelVisible(_slotPanel, true);
            }
        });
    }

    void ShowDeleteConfirmation()
    {
        if (_selectedSlot == 0) return;
        SaveSlotInfo info = GameSaveService.GetSlotInfo(_selectedSlot);
        if (!info.Exists) return;
        _deleteSlot = info.Slot;
        string name = info.CanLoad ? info.PlayerName : $"슬롯 {info.Slot}";
        _deleteMessage.text = $"'{name}'의 저장 데이터를\n삭제하시겠습니까?";
        _deleteErrorText.text = string.Empty;
        SetPanelVisible(_deletePopup, true);
    }

    void ConfirmDelete()
    {
        if (!GameSaveService.TryDeleteSlot(_deleteSlot, out string error))
        {
            _deleteErrorText.text = error;
            return;
        }
        SetPanelVisible(_deletePopup, false);
        _selectedSlot = 0;
        Refresh();
    }

    void CancelDelete()
    {
        SetPanelVisible(_deletePopup, false, () => _deleteSelectedButton.Select());
    }

    void ShowQuitConfirmation() => SetPanelVisible(_quitPopup, true);

    void CancelQuit()
    {
        SetPanelVisible(_quitPopup, false, () => _quitButton.Select());
    }

    void QuitGame()
    {
        SetPanelVisible(_quitPopup, false, () =>
        {
            _leavingScene = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    void Refresh()
    {
        _continueButton.interactable = GameSaveService.HasAnySave;
        for (int i = 0; i < GameSaveService.MaxSlots; i++)
        {
            SaveSlotInfo info = GameSaveService.GetSlotInfo(i + 1);
            _slotButtons[i].interactable = info.Exists;
            _slotLabels[i].text = !info.Exists ? $"슬롯 {info.Slot}    빈 슬롯" : $"슬롯 {info.Slot}    {info.PlayerName}";
            _slotDetails[i].text = !info.Exists ? "새 게임으로 플레이를 시작할 수 있습니다."
                : info.CanLoad ? $"저장 일시  {FormatSaveTime(info.LastPlayedUtcTicks)}    |    {LocationName(info.SceneName)}" : info.Error;
            _slotStatusLabels[i].text = info.CanLoad ? $"{info.DayNumber}일차       {PhaseName(info.Phase)}" : string.Empty;
            if (_selectedSlot == 0 && info.Exists) _selectedSlot = info.Slot;
        }
        RefreshSelection();
    }

    void RefreshSelection()
    {
        foreach (GameObject frame in _slotSelectionFrames) frame.SetActive(false);
        if (_selectedSlot > 0) _slotSelectionFrames[_selectedSlot - 1].SetActive(true);
        SaveSlotInfo selected = _selectedSlot > 0 ? GameSaveService.GetSlotInfo(_selectedSlot) : default;
        _loadSelectedButton.interactable = selected.CanLoad;
        _deleteSelectedButton.interactable = selected.Exists;
    }

    static string FormatSaveTime(long ticks)
        => ticks > 0 && ticks <= DateTime.MaxValue.Ticks
            ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "알 수 없음";

    static string PhaseName(DayPhase phase)
        => phase == DayPhase.Morning ? "오전" : phase == DayPhase.Afternoon ? "오후" : "저녁";

    static string LocationName(string sceneName)
    {
        switch (sceneName)
        {
            case "MorningBoat": case "AfterNoonBoat": return "선상";
            case "Dive": return "잠수";
            case "Sushi":
            case GameManager.NightScene: return "초밥";
            case "BossTest": return "보스전";
            default: return sceneName;
        }
    }
}
