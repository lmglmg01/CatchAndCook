using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 퀘스트 설명, 세부 목표와 보상을 표시하고 수락 또는 완료 요청을 전달합니다.
/// </summary>
public class QuestDetailView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // Quest_Details의 QuestNameText TMP_Text를 연결합니다. 선택한 퀘스트 제목을 표시합니다.
    [SerializeField] TMP_Text _questNameText;
    // 상세창 상단 StateBox/StateText TMP_Text를 연결합니다. 현재 퀘스트 상태를 표시합니다.
    [SerializeField] TMP_Text _questStateText;
    // ContentArea의 QuestTypeIcon Image를 연결합니다. 퀘스트 종류에 따라 배경색을 바꿉니다.
    [SerializeField] UnityEngine.UI.Image _questTypeImage;
    // QuestTypeIcon 아래 QuestTypeText TMP_Text를 연결합니다. 메인·서브·콘텐츠 퀘스트 문구를 표시합니다.
    [SerializeField] TMP_Text _questTypeText;
    [SerializeField] Sprite[] _kindSprites = new Sprite[3]; // 메인, 서브, 콘텐츠 순서의 제목 아이콘입니다.
    // ContentArea의 QuestContent TMP_Text를 연결합니다. 선택한 퀘스트의 설명을 표시합니다.
    [SerializeField] TMP_Text _questContentText;
    // ConditionArea 아래 Condition들의 QuestConditionView를 위에서 아래 순서로 연결합니다. 목표가 있는 행만 표시합니다.
    [SerializeField] QuestConditionView[] _questConditionView = new QuestConditionView[4];
    // RewardArea/Rewards 아래 RewardSlot들의 QuestRewardView를 왼쪽부터 순서대로 연결합니다. 보상이 있는 칸만 표시합니다.
    [SerializeField] QuestRewardView[] _rewardView = new QuestRewardView[3];
    // 상세창의 AcceptButton을 연결합니다. 수락 전에는 수락, 완료 가능한 상태에서는 완료 요청을 보냅니다.
    [SerializeField] UnityEngine.UI.Button _acceptButton;
    // AcceptButton 안의 문구 TMP_Text를 연결합니다. 수락/완료로 바뀌며, 미연결 시 버튼 자식에서 자동으로 찾습니다.
    [SerializeField] TMP_Text _acceptButtonText;
    // 상세창의 CloseButton을 연결합니다. 상세창만 닫고 퀘스트 목록은 유지합니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;
    [SerializeField] Sprite _acceptSprite, _claimSprite; // 수락 및 보상받기 버튼의 배경입니다.
    [SerializeField] UnityEngine.UI.Button _abandonButton; // 수락한 서브 퀘스트를 View 목록에서 제거합니다.
    // 상세창 상단의 ClearMark GameObject를 연결합니다. 퀘스트가 완료된 경우에만 CLEAR 표시를 켭니다.
    [SerializeField] GameObject _clearMark;

    #endregion

    #region 런타임 상태

    // 현재 상세창에 표시할 View 데이터 참조입니다. 임시 데이터 원본과 생성 설정은 QuestListView에서 관리합니다.
    QuestListView.QuestData _quest;
    string _stampQuestId;
    bool _stampCompleted;
    readonly System.Collections.Generic.HashSet<string> _stampedQuests = new();
    bool _initialized; // 초기화 여부입니다.

    #endregion

    #region 이벤트 액션

    public event Action<QuestListView.QuestData> OnActionRequested; // 동작 요청 알림입니다.
    public event Action<QuestListView.QuestData> OnAbandonRequested; // 서브 퀘스트 표시 제거 요청입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 닫기 버튼입니다.
    /// </summary>
    public UnityEngine.UI.Button CloseBtton => _closeButton;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 상세창의 수락·완료 버튼과 닫기 버튼을 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeButton);
        Initialize();
    }

    /// <summary>
    /// 상세창 버튼 이벤트를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_abandonButton != null) _abandonButton.onClick.RemoveListener(RequestAbandon);
        if (_acceptButton != null)
        {
            _acceptButton.onClick.RemoveListener(RequestAction);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Hide);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 상세창 버튼 이벤트를 중복 없이 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_abandonButton != null) _abandonButton.onClick.AddListener(RequestAbandon);
        if (_acceptButton != null)
        {
            _acceptButton.onClick.AddListener(RequestAction);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Hide);
        }

        if (_acceptButtonText == null && _acceptButton != null)
        {
            _acceptButtonText = _acceptButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    /// <summary>
    /// 퀘스트 상태에 맞는 설명, 목표, 보상과 행동 버튼을 표시합니다.
    /// </summary>
    /// <param name="quest">퀘스트 값입니다.</param>
    public void Bind(QuestListView.QuestData quest)
    {
        Initialize();
        bool sameQuest = quest != null && _stampQuestId == quest.id;
        bool firstCompletion = sameQuest && !_stampCompleted && quest.completed;
        _stampQuestId = quest != null ? quest.id : null;
        _stampCompleted = quest != null && quest.completed;
        _quest = quest;
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (quest == null)
        {
            Hide();
            return;
        }

        if (_questNameText != null)
        {
            _questNameText.GetComponent<QuestTitleHoverView>()?.OnPointerExit(null);
            _questNameText.text = quest.title;
            _questNameText.raycastTarget=true;
            var tooltip=_questNameText.GetComponent<QuestTitleHoverView>();
            if(tooltip!=null)tooltip.enabled=false;
            QuestTitleMarqueeView.Apply(_questNameText);
        }

        if (_questStateText != null)
        {
            _questStateText.text = QuestListView.StateLabel(quest.State);
        }

        if (_questTypeText != null)
        {
            _questTypeText.gameObject.SetActive(false);
        }

        if (_questContentText != null)
        {
            _questContentText.text = quest.description;
        }

        if (_questTypeImage != null)
        {
            _questTypeImage.overrideSprite = null;
            _questTypeImage.sprite = _kindSprites != null && (int)quest.kind < _kindSprites.Length ? _kindSprites[(int)quest.kind] : null;
            _questTypeImage.enabled = _questTypeImage.sprite != null;
            _questTypeImage.color = Color.white;
            _questTypeImage.preserveAspect = true;
            _questTypeImage.rectTransform.sizeDelta=quest.kind==QuestListView.QuestKind.Main?new Vector2(70.1f,70.1f):new Vector2(67,67);
        }

        if (_clearMark != null)
        {
            var effect = GetComponent<UIVisibilityEffectView>();
            if (effect == null) _clearMark.SetActive(quest.completed);
            else if (!quest.completed) effect.HideImmediate();
            else
            {
                bool unseen = _stampedQuests.Add(quest.id ?? quest.title ?? string.Empty);
                if (firstCompletion && unseen) effect.Show();
                else if (!sameQuest || !effect.IsShown) effect.ShowStatic();
            }
        }

        if (_questConditionView != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _questConditionView.Length; i++)
            {
                var view = _questConditionView[i]; // UI 동작과 데이터를 연결하는 뷰입니다.
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (view == null)
                {
                    continue;
                }

                var objective = quest.objectives != null && i < quest.objectives.Length ? quest.objectives[i] : null; // 목표 값입니다.
                view.gameObject.SetActive(objective != null);
                if (objective != null)
                {
                    view.Bind(objective);
                    view.SetCompletedQuest(quest.completed);
                }
            }
        }

        if (_rewardView != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _rewardView.Length; i++)
            {
                var view = _rewardView[i]; // UI 동작과 데이터를 연결하는 뷰입니다.
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (view == null)
                {
                    continue;
                }

                var reward = quest.rewards != null && i < quest.rewards.Length ? quest.rewards[i] : null; // 보상 값입니다.
                view.gameObject.SetActive(quest.rewards != null && quest.rewards.Length > 0 ? i < quest.rewards.Length : i == 0);
                if (view.gameObject.activeSelf)
                {
                    view.Bind(reward);
                }
            }
        }

        bool readyToClaim = quest.State == QuestListView.QuestState.ReadyToComplete;
        if (_questContentText != null) _questContentText.color = readyToClaim ? new Color(.56f,.60f,.60f) : new Color32(183,178,163,255);
        if (_acceptButton != null)
        {
            // 미수락 메인퀘스트는 포기 버튼이 없으므로 수락 버튼을 가운데에 표시합니다.
            bool centerAction = readyToClaim || (quest.State == QuestListView.QuestState.NotAccepted);
            _acceptButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(centerAction ? 0 : 150, -263);
            var image = _acceptButton.targetGraphic as UnityEngine.UI.Image;
            if (image != null) { image.sprite = readyToClaim ? _claimSprite : _acceptSprite; image.color = Color.white; }
        }
        if (_closeButton != null) _closeButton.gameObject.SetActive(false);
        if (_abandonButton != null)
        {
            // 수락한 진행 중 퀘스트는 포기 버튼만 보이므로 가운데에 배치합니다.
            var abandonRect = _abandonButton.GetComponent<RectTransform>();
            var position = abandonRect.anchoredPosition;
            position.x = quest.State == QuestListView.QuestState.InProgress ? 0 : -136;
            abandonRect.anchoredPosition = position;
            _abandonButton.gameObject.SetActive(quest.kind != QuestListView.QuestKind.Main && quest.accepted && !quest.completed && !readyToClaim);
            _abandonButton.interactable = quest.kind == QuestListView.QuestKind.Side && quest.accepted && !quest.completed;
        }

        bool actionable = quest.State == QuestListView.QuestState.NotAccepted || quest.State == QuestListView.QuestState.ReadyToComplete; // 실행 가능 여부입니다.
        if (_acceptButton != null)
        {
            _acceptButton.gameObject.SetActive(actionable);
            _acceptButton.interactable = actionable;
        }

        if (_acceptButtonText != null)
        {
            _acceptButtonText.text = quest.State == QuestListView.QuestState.NotAccepted ? "수락" : "보상받기";
            _acceptButtonText.enableAutoSizing = false;
            _acceptButtonText.fontSize = readyToClaim ? 36 : 40;
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 퀘스트 상세창을 표시합니다.
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(RefreshConditionScroll());
    }

    /// <summary>
    /// 퀘스트 상세창만 숨깁니다.
    /// </summary>
    System.Collections.IEnumerator RefreshConditionScroll()
    {
        yield return null;
        var scroll=GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        if(scroll==null)yield break;
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.Rebuild(UnityEngine.UI.CanvasUpdate.PostLayout);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition=1;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    #endregion

    #region 선택 및 외부 요청

    /// <summary>수락한 서브 퀘스트일 때만 목록 View로 포기 요청을 전달합니다.</summary>
    private void RequestAbandon()
    {
        if (_quest != null && _quest.kind == QuestListView.QuestKind.Side && _quest.accepted && !_quest.completed)
            OnAbandonRequested?.Invoke(_quest);
    }

    /// <summary>
    /// 수락 전 또는 완료 가능한 상태에서만 행동 요청을 전달합니다.
    /// </summary>
    void RequestAction()
    {
        if (_quest != null && (_quest.State == QuestListView.QuestState.NotAccepted || _quest.State == QuestListView.QuestState.ReadyToComplete))
        {
            OnActionRequested?.Invoke(_quest);
        }
    }

    #endregion

}
