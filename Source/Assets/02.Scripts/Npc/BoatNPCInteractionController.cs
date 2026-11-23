using System.Collections.Generic;
using UnityEngine;

public class BoatNPCInteractionController : MonoBehaviour
{
    [Header("NPC 선택지")]
    [SerializeField] BoardView _boardView;
    [SerializeField] UnityEngine.UI.Button _confirmButton;
    [SerializeField] UnityEngine.UI.Graphic[] _optionLabels;
    [SerializeField] Color _selectedColor;
    [SerializeField] Color _normalColor;

    [Header("대화")]
    [SerializeField] TalkView _talkView;
    [SerializeField] GameObject _talkPanel;
    [SerializeField] string _playerName = "데이브";
    [SerializeField] string _npcName = "선장";
    [SerializeField] TalkView.TalkLine[] _talkLines =
    {
        new TalkView.TalkLine(1, "준비되면 잠수를 시작하자.")
    };

    [Header("상점")]
    [SerializeField] StoreView _storeView;
    [SerializeField] GameObject _storePanel;

    [Header("강화")]
    [SerializeField] UpgradeEquipView _upgradeEquipView;
    [SerializeField] GameObject _upgradePanel;

    [Header("NPC 창 표시 중 숨길 뷰")]
    [SerializeField] CanvasGroup[] _otherViewGroups;

    Dictionary<CanvasGroup, (float alpha, bool interactable, bool blocksRaycasts)> _hiddenViewStates
        = new Dictionary<CanvasGroup, (float, bool, bool)>();

    int _selectedIndex;
    bool _isNearby;
    BaseView _baseView;
    Transform _phoneAppGroup;
    bool _wasOtherViewOpen;
    NPCInteractType _openedView = NPCInteractType.None;
    NPCInteractType _allowedInteraction = NPCInteractType.None;

    public bool IsNearby => _isNearby;
    public NPCInteractType SelectedInteraction => (NPCInteractType)_selectedIndex;
    public bool IsViewOpen => IsOpen(_talkPanel) || IsOpen(_storePanel) || IsOpen(_upgradePanel);

    bool IsOtherViewOpen => _baseView != null && (_baseView.IsTalkMode || _baseView.CurrentAppIndex >= 0 ||
        (_phoneAppGroup != null && _phoneAppGroup.gameObject.activeInHierarchy));

    internal bool IsInteractionBlocked => IsViewOpen || IsOtherViewOpen;

    internal void BindBaseView(BaseView view)
    {
        _baseView = view;
        _phoneAppGroup = view != null ? view.transform.Find("SmartPhone/AppGroup") : null;
    }

    void OnEnable()
    {
        if (_confirmButton != null) _confirmButton.onClick.AddListener(OpenSelectedView);
    }

    void Start()
    {
        CloseView();
        RefreshSelection();
    }

    void LateUpdate()
    {
        // Existing view close buttons can hide their panels independently.
        if (_openedView != NPCInteractType.None && !IsViewOpen) CloseView();
        bool otherViewOpen = IsOtherViewOpen;
        if (_wasOtherViewOpen == otherViewOpen) return;
        _wasOtherViewOpen = otherViewOpen;
        RefreshSelection();
    }

    void OnDisable()
    {
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OpenSelectedView);
        _isNearby = false;
        CloseView();
    }

    public void SetNearby(bool nearby)
    {
        if (nearby && !_isNearby)
            _selectedIndex = _allowedInteraction == NPCInteractType.None ? 0 : (int)_allowedInteraction;
        _isNearby = nearby;
        if (!nearby) CloseView();
        RefreshSelection();
    }

    public void Navigate(int direction)
    {
        if (!_isNearby || IsInteractionBlocked || direction == 0) return;
        if (_allowedInteraction != NPCInteractType.None) return;

        int count = (int)NPCInteractType.None;
        _selectedIndex = (_selectedIndex + (direction > 0 ? -1 : 1) + count) % count;
        RefreshSelection();
        GameSfx.Play(GameSoundId.MenuClick);
    }

    public void OpenSelectedView()
    {
        if (!_isNearby || IsInteractionBlocked) return;
        if (_allowedInteraction != NPCInteractType.None && SelectedInteraction != _allowedInteraction) return;

        switch (SelectedInteraction)
        {
            case NPCInteractType.Talk:
                if (_talkView == null || _talkPanel == null) return;
                _talkView.gameObject.SetActive(true);
                var lines = _talkLines != null && _talkLines.Length > 0
                    ? _talkLines
                    : new[] { new TalkView.TalkLine(1, "준비되면 잠수를 시작하자.") };
                _talkView.BeginTalk(_playerName, _npcName, lines);
                break;
            case NPCInteractType.Store:
                if (_storeView == null || _storePanel == null) return;
                // Activate first so Awake finishes before SetStore opens the window.
                _storeView.gameObject.SetActive(true);
                _storeView.SetStore(true);
                break;
            case NPCInteractType.Upgrade:
                if (_upgradeEquipView == null || _upgradePanel == null) return;
                _upgradeEquipView.SetUpgradeEquip(true);
                break;
            default:
                return;
        }

        _openedView = SelectedInteraction;
        GameSfx.Play(GameSoundId.MenuClick);
        if (_openedView != NPCInteractType.Talk)
            HideOtherViews();
        RefreshSelection();
    }

    public void CloseView()
    {
        if (_talkView != null && IsOpen(_talkPanel)) _talkView.SetTalkPanel(false);
        if (_storeView != null && _storeView.gameObject.activeSelf)
        {
            _storeView.SetStore(false);
            _storeView.gameObject.SetActive(false);
        }
        if (_upgradeEquipView != null && IsOpen(_upgradePanel))
            _upgradeEquipView.SetUpgradeEquip(false);

        _openedView = NPCInteractType.None;
        RestoreOtherViews();
        RefreshSelection();
    }

    // None이면 일반 NPC의 모든 선택지를 허용한다. 튜토리얼이 종료되면 반드시 해제한다.
    public void SetAllowedInteraction(NPCInteractType interaction)
    {
        _allowedInteraction = interaction;
        if (interaction != NPCInteractType.None) _selectedIndex = (int)interaction;
        RefreshSelection();
    }

    void HideOtherViews()
    {
        if (_otherViewGroups == null) return;

        foreach (CanvasGroup group in _otherViewGroups)
        {
            if (group == null || _hiddenViewStates.ContainsKey(group)) continue;
            _hiddenViewStates.Add(group, (group.alpha, group.interactable, group.blocksRaycasts));
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    void RestoreOtherViews()
    {
        foreach (var entry in _hiddenViewStates)
        {
            if (entry.Key == null) continue;
            entry.Key.alpha = entry.Value.alpha;
            entry.Key.interactable = entry.Value.interactable;
            entry.Key.blocksRaycasts = entry.Value.blocksRaycasts;
        }
        _hiddenViewStates.Clear();
    }

    void RefreshSelection()
    {
        if (_boardView != null) _boardView.SetInteractSelect(_isNearby && !IsInteractionBlocked);
        if (_optionLabels == null) return;

        for (int i = 0; i < _optionLabels.Length; i++)
        {
            if (_optionLabels[i] != null)
            {
                Color color = i == _selectedIndex ? _selectedColor : _normalColor;
                if (_allowedInteraction != NPCInteractType.None && i != (int)_allowedInteraction)
                    color.a *= 0.35f;
                _optionLabels[i].color = color;
            }
        }
    }

    static bool IsOpen(GameObject panel) => panel != null && panel.activeInHierarchy;
}
