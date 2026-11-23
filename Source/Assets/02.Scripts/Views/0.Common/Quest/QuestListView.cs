using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 목록, 상세창과 메모에 같은 표시 데이터를 제공하는 뷰입니다.
/// </summary>
public class QuestListView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [Header("목록과 탭")]
    // Quest 아래의 Quest_Main 패널 GameObject를 연결합니다. 목록 창 전체를 켜고 끕니다.
    [SerializeField] GameObject _mainpage; // 메인 페이지 값입니다.
    // Quest_Main의 CloseButton(뒤로) Button을 연결합니다. 퀘스트 앱을 닫고 기본 앱 선택 화면으로 돌아갑니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;
    // Tabs의 AllTab, CurrentTab, ClearTab Button을 이 순서로 연결합니다. 진행 중 탭에는 완료 가능 상태도 포함됩니다.
    [SerializeField] UnityEngine.UI.Button[] _questTypeButtons = new UnityEngine.UI.Button[3];
    // 목록 Content 아래의 QuestSlotView들을 위에서 아래 순서로 연결합니다. 비우면 자식 슬롯을 자동으로 찾습니다.
    [SerializeField] QuestSlotView[] _views;
    // Quest_Main/Page/List/Scroll View/Viewport/Content의 Transform을 연결합니다. 추가 슬롯이 이 부모 아래에 생성됩니다.
    [SerializeField] Transform _slotContent;
    // Scroll View의 EmptyMessage TMP_Text를 연결합니다. 현재 탭에 퀘스트가 없을 때 안내를 표시합니다.
    [SerializeField] TMPro.TMP_Text _emptyText;
    // Quest_Main/Page/List/Scroll View의 ScrollRect 컴포넌트를 연결합니다. 탭 전환 시 스크롤을 맨 위로 돌립니다.
    [SerializeField] UnityEngine.UI.ScrollRect _scrollRect;

    [Header("상세창")]
    // Quest 아래의 Quest_Details 패널 GameObject를 연결합니다. 상세창 표시 여부를 제어합니다.
    [SerializeField] GameObject _detailPage; // 상세 페이지 값입니다.
    // 같은 Quest_Details 오브젝트의 QuestDetailView 컴포넌트를 연결합니다. 선택한 퀘스트 정보를 전달합니다.
    [SerializeField] QuestDetailView _detailView;

    [Header("스마트폰 앱 이동")]
    // Canvas_Board/SmartPhonePanel GameObject를 연결합니다. 퀘스트 앱을 열거나 뒤로 갈 때 전환합니다.
    [SerializeField] GameObject _smartPhonePanel; // 스마트 스마트폰 패널 값입니다.
    // 같은 스마트폰 아래의 Inventory, Setting 등 다른 앱 페이지 GameObject들을 연결합니다. 퀘스트 앱을 열면 숨깁니다.
    [SerializeField] GameObject[] _otherAppPages;

    #endregion

    #region 임시 데이터 및 테스트 멤버

    [Header("View 전용 임시 데이터")]
    // UI를 연결하지 않습니다. 체크하면 최초 조회 때 임시 퀘스트를 준비하며, 실제 데이터만 받을 때는 체크를 해제합니다.
    [SerializeField] bool _usePreviewData = true; // 사용 미리보기 데이터 여부입니다.
    // UI를 연결하지 않습니다. 인스펙터에서 임시 퀘스트 내용을 입력하는 배열이며, 비어 있으면 CreatePreviewQuests의 샘플을 사용합니다.
    [SerializeField] QuestData[] _previewQuests;

    #endregion

    #region 런타임 상태

    // 목록·상세창·메모가 공유하는 실행 중 표시 데이터입니다. SetQuests로 전달된 외부 데이터도 여기에 복사됩니다.
    readonly List<QuestData> _quests = new List<QuestData>();
    // 같은 View에 외부 데이터가 다시 전달돼도 포기한 서브 퀘스트는 숨깁니다.
    readonly HashSet<string> _abandonedQuestIds = new HashSet<string>();
    readonly List<QuestSlotView> _slots = new List<QuestSlotView>(); // 슬롯 목록입니다.
    // 현재 상세창에서 선택한 표시 데이터입니다.
    QuestData _selectedQuest;
    bool _initialized; // 초기화 여부입니다.
    // 임시 데이터 준비와 외부 데이터 입력 양쪽에서 사용하는 중복 초기화 방지 상태입니다.
    bool _dataInitialized;
    int _tab; // 탭 값입니다.

    #endregion

    #region 이벤트 액션

    public event Action OnQuestsChanged; // 퀘스트 목록 변경 알림입니다.
    public event Action<QuestData> OnQuestAccepted; // 퀘스트 수락 알림입니다.
    // 실제 보상은 지급하지 않으며, 나중에 완료/보상 로직을 연결할 이벤트입니다.
    public event Action<QuestData> OnQuestCompleted;

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 현재 탭 값입니다.
    /// </summary>
    public int CurrentTab => _tab;

    /// <summary>
    /// 선택 퀘스트 값입니다.
    /// </summary>
    public QuestData SelectedQuest => _selectedQuest;

    /// <summary>
    /// 퀘스트 목록입니다.
    /// </summary>
    public IReadOnlyList<QuestData> Quests
    {
        get
        {
            EnsureData();
            return _quests.AsReadOnly();
        }
    }

    #endregion

    #region 표시 데이터 형식

    // 표시용 종류·상태입니다. 실제 퀘스트 데이터가 생기면 이 구조에 맞춰 View로 전달할 수 있습니다.

    /// <summary>
    /// 화면에 표시할 퀘스트 종류입니다.
    /// </summary>
    public enum QuestKind
    {
        Main,
        Side,
        Content
    }

    /// <summary>
    /// 화면에 표시할 퀘스트 진행 상태입니다.
    /// </summary>
    public enum QuestState
    {
        NotAccepted,
        InProgress,
        ReadyToComplete,
        Completed
    }

    /// <summary>
    /// 퀘스트 상세창과 메모에서 사용하는 표시용 세부 목표 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class Objective
    {

        #region 멤버

        #region 런타임 상태

        // 세부 목표 이름과 대상 아이콘 스프라이트입니다. UI 컴포넌트를 연결하는 항목은 아닙니다.
        public string description;
        public Sprite targetIcon; // 대상 아이콘 이미지입니다.
        // 현재 달성 수량과 목표 수량입니다. 임시 진행도 테스트에 사용합니다.
        [Min(0)]
        public int current;
        [Min(1)]
        public int target = 1; // 대상 값입니다.

        #endregion

        #region 프로퍼티

        /// <summary>
        /// 완료 여부입니다.
        /// </summary>
        public bool IsComplete => current >= Mathf.Max(1, target);

        #endregion

        #endregion

    }

    /// <summary>
    /// 퀘스트 보상 슬롯에 전달하는 표시용 보상 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class Reward
    {

        #region 멤버

        #region 런타임 상태

        // 보상 종류·이름·수량·아이콘의 표시값입니다. 실제 재화나 아이템을 지급하지 않습니다.
        public QuestRewardType type;
        public string name; // 이름 값입니다.
        [Min(0)]
        public float amount; // 수량 값입니다.
        public Sprite icon; // 아이콘 이미지입니다.

        #endregion

        #endregion

    }

    // 실제 퀘스트 모델이 생기기 전 View 안에서만 사용하는 표시 데이터입니다.

    /// <summary>
    /// 목록·상세창·메모가 공유하는 퀘스트 표시 데이터입니다. 실제 퀘스트 모델과 구분합니다.
    /// </summary>
    [Serializable]
    public sealed class QuestData
    {

        #region 멤버

        #region 런타임 상태

        // 퀘스트 구분용 ID, 목록에 표시할 제목과 종류입니다.
        public string id;
        public string title; // 제목 값입니다.
        public QuestKind kind; // 분류 값입니다.
        // 상세창 설명에 표시할 내용입니다.
        [TextArea(2, 5)]
        public string description;
        // View에서 수락·완료 상태와 NEW 표시를 테스트하는 값입니다.
        public bool accepted;
        public bool completed; // 완료 여부입니다.
        public bool isNew = true; // 신규 여부입니다.
        // 상세창의 목표 행과 보상 슬롯에 순서대로 표시할 데이터입니다.
        public Objective[] objectives;
        public Reward[] rewards; // 보상 목록입니다.

        #endregion

        #region 프로퍼티

        /// <summary>
        /// 목표 개수 값입니다.
        /// </summary>
        public int ObjectiveCount
        {
            get
            {
                int count = 0; // 개수 값입니다.
                if (objectives != null)
                {
                    // o: 대상 값입니다.
                    foreach (var o in objectives)
                    {
                        if (o != null)
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 완료 목표 개수 값입니다.
        /// </summary>
        public int CompletedObjectiveCount
        {
            get
            {
                int count = 0; // 개수 값입니다.
                if (objectives != null)
                {
                    // o: 대상 값입니다.
                    foreach (var o in objectives)
                    {
                        if (o != null && o.IsComplete)
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 상태 값입니다.
        /// </summary>
        public QuestState State => completed ? QuestState.Completed : !accepted ? QuestState.NotAccepted : ObjectiveCount > 0 && CompletedObjectiveCount == ObjectiveCount ? QuestState.ReadyToComplete : QuestState.InProgress;

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 목록과 상세창의 버튼 및 슬롯 이벤트를 초기화합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeButton);
        Initialize();
    }

    /// <summary>
    /// 퀘스트 창이 열리면 현재 데이터와 탭 표시를 갱신합니다.
    /// </summary>
    void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// 버튼, 상세창과 슬롯에 연결한 이벤트를 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        if (_questTypeButtons != null)
        {
            if (_questTypeButtons.Length > 0 && _questTypeButtons[0] != null)
            {
                _questTypeButtons[0].onClick.RemoveListener(ShowAll);
            }

            if (_questTypeButtons.Length > 1 && _questTypeButtons[1] != null)
            {
                _questTypeButtons[1].onClick.RemoveListener(ShowInProgress);
            }

            if (_questTypeButtons.Length > 2 && _questTypeButtons[2] != null)
            {
                _questTypeButtons[2].onClick.RemoveListener(ShowCompleted);
            }
        }

        if (_detailView != null)
        {
            _detailView.OnActionRequested -= ApplySelectedQuestAction;
            _detailView.OnAbandonRequested -= AbandonQuest;
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                slot.OnQuestSelected -= SelectQuest;
            }
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 버튼과 기존 슬롯의 이벤트를 중복 없이 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_questTypeButtons != null)
        {
            if (_questTypeButtons.Length > 0 && _questTypeButtons[0] != null)
            {
                _questTypeButtons[0].onClick.AddListener(ShowAll);
            }

            if (_questTypeButtons.Length > 1 && _questTypeButtons[1] != null)
            {
                _questTypeButtons[1].onClick.AddListener(ShowInProgress);
            }

            if (_questTypeButtons.Length > 2 && _questTypeButtons[2] != null)
            {
                _questTypeButtons[2].onClick.AddListener(ShowCompleted);
            }
        }

        if (_views != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _views)
            {
                RegisterSlot(slot);
            }
        }

        // slot: 슬롯 값입니다.
        foreach (var slot in GetComponentsInChildren<QuestSlotView>(true))
        {
            RegisterSlot(slot);
        }

        if (_detailView != null)
        {
            _detailView.OnActionRequested += ApplySelectedQuestAction;
            _detailView.OnAbandonRequested += AbandonQuest;
        }
    }

    /// <summary>
    /// 슬롯을 등록하고 퀘스트 선택 이벤트를 연결합니다.
    /// </summary>
    /// <param name="slot">슬롯 표시를 연결할 뷰입니다.</param>
    void RegisterSlot(QuestSlotView slot)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (slot == null || _slots.Contains(slot))
        {
            return;
        }

        _slots.Add(slot);
        slot.OnQuestSelected += SelectQuest;
        if (_slotContent == null)
        {
            _slotContent = slot.transform.parent;
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 외부 표시 데이터를 복사해 목록과 메모에 반영합니다.
    /// </summary>
    /// <param name="quests">퀘스트 목록입니다.</param>
    public void SetQuests(IReadOnlyList<QuestData> quests)
    {
        _dataInitialized = true;
        var copied = new List<QuestData>(); // 복사 값입니다.
        if (quests != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i] != null && !_abandonedQuestIds.Contains(quests[i].id ?? string.Empty))
                {
                    copied.Add(CopyQuest(quests[i]));
                }
            }
        }

        _quests.Clear();
        _quests.AddRange(copied);
        _selectedQuest = null;
        HideDetailPage();
        PublishChange();
    }

    // 임시 샘플 생성 전용 함수가 아닙니다. SetQuests 입력과 수락·완료 이벤트에서도 같은 복사 함수를 사용합니다.

    /// <summary>
    /// 임시 데이터 변경이 외부 원본에 영향을 주지 않도록 복사합니다.
    /// </summary>
    /// <param name="source">원본 값입니다.</param>
    static QuestData CopyQuest(QuestData source)
    {
        var copy = new QuestData
        {
            id = source.id,
            title = source.title,
            kind = source.kind,
            description = source.description,
            accepted = source.accepted,
            completed = source.completed,
            isNew = source.isNew
        }; // 복사 값입니다.
        copy.objectives = new Objective[source.objectives != null ? source.objectives.Length : 0];
        // i: 반복 값입니다.
        for (int i = 0; i < copy.objectives.Length; i++)
        {
            var o = source.objectives[i]; // 대상 값입니다.
            if (o != null)
            {
                copy.objectives[i] = new Objective
                {
                    description = o.description,
                    targetIcon = o.targetIcon,
                    current = o.current,
                    target = o.target
                };
            }
        }

        copy.rewards = new Reward[source.rewards != null ? source.rewards.Length : 0];
        // i: 반복 값입니다.
        for (int i = 0; i < copy.rewards.Length; i++)
        {
            var r = source.rewards[i]; // 대상 값입니다.
            if (r != null)
            {
                copy.rewards[i] = new Reward
                {
                    type = r.type,
                    name = r.name,
                    amount = r.amount,
                    icon = r.icon
                };
            }
        }

        return copy;
    }

    /// <summary>
    /// 기존 인스펙터 호출을 유지하며 목록을 다시 표시합니다.
    /// </summary>
    public void SetQeustDatas()
    {
        Refresh();
    }

    /// <summary>
    /// 목록과 상세창을 갱신하고 메모 구독자에게 변경을 알립니다.
    /// </summary>
    void PublishChange()
    {
        Refresh();
        OnQuestsChanged?.Invoke();
    }

    /// <summary>
    /// 필터링한 목록, 진행도, 탭 강조와 열린 상세창을 갱신합니다.
    /// </summary>
    public void Refresh()
    {
        Initialize();
        EnsureData();
        var filtered = new List<QuestData>(); // 필터 결과 값입니다.
        // quest: 퀘스트 값입니다.
        foreach (var quest in _quests)
        {
            if (MatchesTab(quest))
            {
                filtered.Add(quest);
            }
        }

        while (_slots.Count < filtered.Count && _slots.Count > 0 && _slotContent != null)
        {
            RegisterSlot(Instantiate(_slots[0], _slotContent, false));
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _slots.Count; i++)
        {
            bool visible = i < filtered.Count; // 노출 여부입니다.
            _slots[i].Bind(visible ? filtered[i] : null);
            _slots[i].gameObject.SetActive(visible);
        }

        if (_emptyText != null)
        {
            _emptyText.text = "표시할 퀘스트가 없습니다.";
            _emptyText.gameObject.SetActive(filtered.Count == 0);
        }

        if (_questTypeButtons != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _questTypeButtons.Length; i++)
            {
                var button = _questTypeButtons[i]; // 버튼 값입니다.
                if (button != null && button.targetGraphic != null)
                {
                    button.targetGraphic.color = i == _tab ? new Color(.72f, .87f, .82f) : Color.white;
                }
            }
        }

        if (_slotContent is RectTransform content)
        {
            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        if (_selectedQuest != null && _detailView != null && _detailView.gameObject.activeSelf)
        {
            _detailView.Bind(_selectedQuest);
        }

        var presentation = GetComponent<QuestScreenView>(); // 표시 데이터 값입니다.
        if (presentation != null)
        {
            presentation.RefreshPresentation();
        }
    }

    /// <summary>
    /// 선택한 퀘스트를 확인 상태로 바꾸고 상세창을 표시합니다.
    /// </summary>
    /// <param name="quest">퀘스트 값입니다.</param>
    public void SelectQuest(QuestData quest)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (quest == null || !_quests.Contains(quest))
        {
            return;
        }

        quest.isNew = false;
        _selectedQuest = quest;
        if (_detailView != null)
        {
            _detailView.Bind(quest);
            _detailView.Show();
        }

        if (_detailPage != null)
        {
            _detailPage.SetActive(true);
        }

        PublishChange();
    }

    /// <summary>
    /// 수락한 서브 퀘스트만 View 목록에서 제거하고 상세창·메모를 함께 갱신합니다.
    /// 실제 퀘스트 모델과 저장 데이터는 변경하지 않습니다.
    /// </summary>
    public void AbandonQuest(QuestData quest)
    {
        if (quest == null || quest.kind != QuestKind.Side || !quest.accepted ||
            quest.completed || !_quests.Contains(quest))
            return;
        if (!string.IsNullOrEmpty(quest.id)) _abandonedQuestIds.Add(quest.id);
        _quests.Remove(quest);
        if (_selectedQuest == quest)
        {
            _selectedQuest = null;
            HideDetailPage();
        }
        PublishChange();
    }

    // 아래 함수들은 View가 보관한 표시 상태만 바꿉니다. 실제 퀘스트 수락·완료·보상 처리는 외부 이벤트 연결이 필요합니다.

    /// <summary>
    /// 미수락 퀘스트는 수락하고, 목표를 달성한 퀘스트는 완료 처리합니다.
    /// </summary>
    /// <param name="quest">퀘스트 값입니다.</param>
    void ApplySelectedQuestAction(QuestData quest)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (quest == null || !_quests.Contains(quest))
        {
            return;
        }

        if (quest.State == QuestState.NotAccepted)
        {
            quest.accepted = true;
            quest.isNew = false;
            PublishChange();
            OnQuestAccepted?.Invoke(CopyQuest(quest));
        }
        else if (quest.State == QuestState.ReadyToComplete)
        {
            quest.completed = true;
            PublishChange();
            OnQuestCompleted?.Invoke(CopyQuest(quest));
        }
    }

    /// <summary>
    /// 표시용 목표 진행도를 변경하고 목록과 메모를 함께 갱신합니다.
    /// </summary>
    /// <param name="questId">퀘스트 식별자 값입니다.</param>
    /// <param name="objectiveIndex">목표 인덱스 값입니다.</param>
    /// <param name="amount">수량 값입니다.</param>
    public bool SetObjectiveProgress(string questId, int objectiveIndex, int amount)
    {
        EnsureData();
        // quest: 퀘스트 값입니다.
        foreach (var quest in _quests)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (quest.id != questId || !quest.accepted || quest.completed || quest.objectives == null || objectiveIndex < 0 || objectiveIndex >= quest.objectives.Length)
            {
                continue;
            }

            var objective = quest.objectives[objectiveIndex]; // 목표 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (objective == null)
            {
                return false;
            }

            objective.current = Mathf.Clamp(amount, 0, Mathf.Max(1, objective.target));
            PublishChange();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 퀘스트 종류를 한글 표시 이름으로 변환합니다.
    /// </summary>
    /// <param name="kind">분류 값입니다.</param>
    public static string KindLabel(QuestKind kind)
    {
        return kind == QuestKind.Main ? "메인" : kind == QuestKind.Side ? "서브" : "콘텐츠";
    }

    /// <summary>
    /// 퀘스트 상태를 한글 표시 이름으로 변환합니다.
    /// </summary>
    /// <param name="state">상태 값입니다.</param>
    public static string StateLabel(QuestState state)
    {
        return state == QuestState.NotAccepted ? "수락 전" : state == QuestState.InProgress ? "진행 중" : state == QuestState.ReadyToComplete ? "완료 가능" : "완료";
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 스마트폰의 다른 페이지를 숨기고 퀘스트 앱을 엽니다.
    /// </summary>
    public void Open()
    {
        if (_otherAppPages != null)
        {
            // page: 페이지 값입니다.
            foreach (var page in _otherAppPages)
            {
                if (page != null)
                {
                    page.SetActive(false);
                }
            }
        }

        if (_smartPhonePanel != null)
        {
            _smartPhonePanel.SetActive(true);
        }

        SetMainPage(true);
        HideDetailPage();
    }

    /// <summary>
    /// 퀘스트 앱을 닫고 기본 화면의 앱 선택 버튼으로 돌아갑니다.
    /// </summary>
    public void Close()
    {
        var fade = GetComponent<AppFadeView>(); // 페이드 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (fade != null && fade.TrySetVisible(false, Close))
        {
            return;
        }

        HideDetailPage();
        SetMainPage(false);
        if (_smartPhonePanel != null)
        {
            _smartPhonePanel.SetActive(false);
        }
    }

    /// <summary>
    /// 퀘스트 목록 창을 표시하거나 숨깁니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetMainPage(bool isOn)
    {
        var fade = GetComponent<AppFadeView>(); // 페이드 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (fade != null && fade.TrySetVisible(isOn, () => SetMainPage(isOn)))
        {
            return;
        }

        gameObject.SetActive(isOn);
        if (_mainpage != null)
        {
            _mainpage.SetActive(isOn);
        }

        if (isOn)
        {
            Refresh();
        }
        else
        {
            HideDetailPage();
        }
    }

    /// <summary>
    /// 수락 전부터 완료까지 모든 퀘스트를 표시합니다.
    /// </summary>
    public void ShowAll()
    {
        SetTab(0);
    }

    /// <summary>
    /// 진행 중과 완료 가능한 퀘스트를 표시합니다.
    /// </summary>
    public void ShowInProgress()
    {
        SetTab(1);
    }

    /// <summary>
    /// 완료 처리가 끝난 퀘스트만 표시합니다.
    /// </summary>
    public void ShowCompleted()
    {
        SetTab(2);
    }

    /// <summary>
    /// 선택한 탭으로 전환하고 스크롤을 맨 위로 돌립니다.
    /// </summary>
    /// <param name="tab">탭 값입니다.</param>
    void SetTab(int tab)
    {
        _tab = Mathf.Clamp(tab, 0, 2);
        HideDetailPage();
        Refresh();
        if (_scrollRect != null)
        {
            UnityEngine.Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
            _scrollRect.StopMovement();
            _scrollRect.verticalNormalizedPosition = 1f;
            if (isActiveAndEnabled) StartCoroutine(ResetScrollAfterLayout());
        }
    }

    /// <summary>
    /// 현재 탭 조건과 퀘스트 상태가 일치하는지 확인합니다.
    /// </summary>
    /// <param name="quest">퀘스트 값입니다.</param>
    System.Collections.IEnumerator ResetScrollAfterLayout()
    {
        yield return null;
        if (_scrollRect == null) yield break;
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
        _scrollRect.Rebuild(UnityEngine.UI.CanvasUpdate.PostLayout);
        _scrollRect.StopMovement();
        _scrollRect.verticalNormalizedPosition = 1;
    }

    bool MatchesTab(QuestData quest)
    {
        return _tab == 0 || (_tab == 1 ? quest.accepted && !quest.completed : quest.completed);
    }

    /// <summary>
    /// 상세창을 숨깁니다.
    /// </summary>
    void HideDetailPage()
    {
        if (_detailView != null)
        {
            _detailView.Hide();
        }

        if (_detailPage != null)
        {
            _detailPage.SetActive(false);
        }
    }

    #endregion

    #region 임시 데이터 및 테스트

    // 임시 데이터 사용이 꺼져 있거나 SetQuests로 외부 데이터를 받은 뒤에는 샘플을 자동으로 넣지 않습니다.

    /// <summary>
    /// 최초 조회 때만 임시 퀘스트 데이터를 준비합니다.
    /// </summary>
    void EnsureData()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_dataInitialized)
        {
            return;
        }

        _dataInitialized = true;
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_usePreviewData)
        {
            return;
        }

        var source = _previewQuests != null && _previewQuests.Length > 0 ? _previewQuests : CreatePreviewQuests(); // 원본 값입니다.
        // quest: 퀘스트 값입니다.
        foreach (var quest in source)
        {
            if (quest != null)
            {
                _quests.Add(CopyQuest(quest));
            }
        }
    }

    // UI 사전 디자인 확인용 샘플 8개입니다. 실제 데이터를 사용할 때는 _usePreviewData를 끄고 SetQuests로 전달합니다.

    /// <summary>
    /// 실제 데이터 없이 네 가지 상태와 세 종류를 확인할 임시 목록을 생성합니다.
    /// </summary>
    public static QuestData[] CreatePreviewQuests()
    {
        return new[]
        {
            new QuestData
            {
                id = "first-dive",
                title = "첫 잠수 준비",
                kind = QuestKind.Main,
                description = "잠수 장비를 확인하고 첫 탐사를 준비하세요.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "산소통 확인",
                        target = 1
                    },
                    new Objective
                    {
                        description = "선장과 대화",
                        target = 1
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Exp,
                        amount = 50
                    },
                    new Reward
                    {
                        type = QuestRewardType.Gold,
                        amount = 100
                    }
                }
            },
            new QuestData
            {
                id = "reef-survey",
                title = "산호초 조사",
                kind = QuestKind.Main,
                accepted = true,
                isNew = false,
                description = "산호초 주변을 둘러보고 생태 조사에 필요한 자료를 모아 주세요.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "조사 지점 방문",
                        current = 1,
                        target = 1
                    },
                    new Objective
                    {
                        description = "물고기 포획",
                        current = 1,
                        target = 3
                    },
                    new Objective
                    {
                        description = "산호 표본 수집",
                        target = 2
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Exp,
                        amount = 100
                    },
                    new Reward
                    {
                        type = QuestRewardType.Gold,
                        amount = 150
                    },
                    new Reward
                    {
                        type = QuestRewardType.Item,
                        name = "조사 보급품",
                        amount = 1
                    }
                }
            },
            new QuestData
            {
                id = "equipment-check",
                title = "장비 점검",
                kind = QuestKind.Content,
                accepted = true,
                isNew = false,
                description = "장비 점검 목표를 모두 달성했습니다. 완료 버튼으로 마무리하세요.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "무기 점검",
                        current = 1,
                        target = 1
                    },
                    new Objective
                    {
                        description = "잠수복 점검",
                        current = 1,
                        target = 1
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Exp,
                        amount = 70
                    },
                    new Reward
                    {
                        type = QuestRewardType.Item,
                        name = "장비 부품",
                        amount = 2
                    }
                }
            },
            new QuestData
            {
                id = "crew-greeting",
                title = "선원에게 인사하기",
                kind = QuestKind.Side,
                accepted = true,
                completed = true,
                isNew = false,
                description = "선원들에게 인사를 마쳤습니다.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "선원과 인사",
                        current = 3,
                        target = 3
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Gold,
                        amount = 50
                    }
                }
            },
            new QuestData
            {
                id = "kitchen-supply",
                title = "주방 재료 모으기",
                kind = QuestKind.Side,
                isNew = false,
                description = "주방에서 사용할 재료를 모아 주세요.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "생선 살 모으기",
                        target = 5
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Gold,
                        amount = 80
                    }
                }
            },
            new QuestData
            {
                id = "weapon-upgrade",
                title = "새로운 장비 익히기",
                kind = QuestKind.Content,
                accepted = true,
                isNew = false,
                description = "장비 담당자와 대화하고 무기의 사용법을 익히세요.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "담당자와 대화",
                        current = 1,
                        target = 1
                    },
                    new Objective
                    {
                        description = "무기 훈련",
                        target = 2
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Exp,
                        amount = 80
                    }
                }
            },
            new QuestData
            {
                id = "return-report",
                title = "탐사 결과 보고",
                kind = QuestKind.Main,
                accepted = true,
                isNew = false,
                description = "탐사 결과 정리가 끝났습니다. 보고를 완료하세요.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "탐사 기록 정리",
                        current = 1,
                        target = 1
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Gold,
                        amount = 120
                    }
                }
            },
            new QuestData
            {
                id = "lost-tool",
                title = "잃어버린 도구",
                kind = QuestKind.Side,
                description = "선원이 잃어버린 도구를 찾아 주세요.",
                objectives = new[]
                {
                    new Objective
                    {
                        description = "도구 찾기",
                        target = 1
                    }
                },
                rewards = new[]
                {
                    new Reward
                    {
                        type = QuestRewardType.Item,
                        name = "답례품",
                        amount = 1
                    }
                }
            }
        };
    }

    #endregion

}
