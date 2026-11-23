using TMPro;
using UnityEngine;

/// <summary>
/// 퀘스트 목록의 진행 중인 할 일을 간단히 표시하는 접이식 HUD 메모입니다.
/// </summary>
public class QuestMemoView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // SmartPhonePanel/Quest의 QuestListView 컴포넌트를 연결합니다. 메모 전용 샘플 없이 목록과 같은 표시 데이터를 읽습니다.
    [SerializeField] QuestListView _questListView;
    // QuestMemo/Background/MemoBody GameObject를 연결합니다. 목록 본문만 접히도록 헤더나 버튼이 포함된 부모는 연결하지 않습니다.
    [SerializeField] GameObject _memoBody;
    // Background 안의 이름이 'On/OffButton'인 Button을 연결합니다. 본문 밖에 있어 접은 뒤에도 누를 수 있어야 합니다.
    [SerializeField] UnityEngine.UI.Button _toggleButton;
    // On/OffButton 안의 Label TMP_Text를 연결합니다. 펼친 상태에서는 접기, 접은 상태에서는 펼치기를 표시합니다.
    [SerializeField] TMP_Text _toggleButtonText;
    // MemoBody 아래 MemoRow1~4 GameObject를 위에서 아래 순서로 연결합니다. 실제 메모 항목 수만큼만 표시합니다.
    [SerializeField] GameObject[] _rows;
    // 각 MemoRow의 Title TMP_Text를 _rows와 같은 순서로 연결합니다. [종류] 퀘스트 제목을 표시합니다.
    [SerializeField] TMP_Text[] _titleTexts;
    // 각 MemoRow의 Progress TMP_Text를 _rows와 같은 순서로 연결합니다. 완료한 목표 수나 완료 가능 안내를 표시합니다.
    [SerializeField] TMP_Text[] _progressTexts;
    // MemoBody의 EmptyMessage TMP_Text를 연결합니다. 진행 중인 할 일이 없을 때 표시합니다.
    [SerializeField] TMP_Text _emptyText;
    // UI를 연결하지 않습니다. 메모의 최초 펼침 여부를 설정하며, 체크하면 본문을 펼친 상태로 시작합니다.
    [SerializeField] bool _expanded = true;
    [SerializeField] bool _assetMemoStyle; // 1차 UI 에셋을 적용한 메모에서 제목/상세의 지정 글꼴 크기와 색상을 사용합니다.
    // 각 HUD 메모 행의 분류 아이콘과 최신 메인/콘텐츠 아이콘입니다.
    [SerializeField] UnityEngine.UI.Image[] _kindIcons;
    [SerializeField] Sprite _mainQuestIcon, _contentQuestIcon;

    #endregion

    #region 런타임 상태

    Coroutine _emptyFoldRoutine;
    bool _emptyFoldHandled;
    bool _initialized; // 초기화 여부입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 노출 퀘스트 개수 값입니다.
    /// </summary>
    public int VisibleQuestCount { get; private set; }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 메모 접기 버튼을 초기화합니다.
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 퀘스트 변경을 구독하고 현재 할 일 목록을 표시합니다.
    /// </summary>
    void OnEnable()
    {
        Initialize();
        if (_questListView != null)
        {
            _questListView.OnQuestsChanged -= Refresh;
            _questListView.OnQuestsChanged += Refresh;
        }

        Refresh();
    }

    /// <summary>
    /// 메모가 숨겨지면 퀘스트 변경 구독을 해제합니다.
    /// </summary>
    void OnDisable()
    {
        CancelEmptyFold();
        _emptyFoldHandled = false;
        if (_questListView != null)
        {
            _questListView.OnQuestsChanged -= Refresh;
        }
    }

    /// <summary>
    /// 메모의 버튼과 데이터 변경 이벤트 연결을 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        CancelEmptyFold();
        if (_questListView != null)
        {
            _questListView.OnQuestsChanged -= Refresh;
        }

        if (_toggleButton != null)
        {
            _toggleButton.onClick.RemoveListener(ToggleMemo);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 접기 버튼 이벤트를 한 번만 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_toggleButton != null)
        {
            _toggleButton.onClick.AddListener(ToggleMemo);
        }

        SetExpanded(_expanded);
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 진행 중과 완료 가능한 퀘스트만 메모에 간단히 표시합니다.
    /// </summary>
    public void Refresh()
    {
        int capacity = Mathf.Min(_rows != null ? _rows.Length : 0, Mathf.Min(_titleTexts != null ? _titleTexts.Length : 0, _progressTexts != null ? _progressTexts.Length : 0)); // 최대 용량 값입니다.
        int count = 0; // 개수 값입니다.
        if (_questListView != null)
        {
            // quest: 퀘스트 값입니다.
            foreach (var quest in _questListView.Quests)
            {
                // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
                if (!quest.accepted || quest.completed)
                {
                    continue;
                }

                if (count < capacity)
                {
                    if (_rows[count] != null)
                    {
                        _rows[count].SetActive(true);
                    }

                    if (_titleTexts[count] != null)
                    {
                        bool hasKindIcon = _kindIcons != null && count < _kindIcons.Length && _kindIcons[count] != null;
                        var kindSprite = quest.kind == QuestListView.QuestKind.Main ? _mainQuestIcon : _contentQuestIcon;
                        if (hasKindIcon)
                        {
                            _kindIcons[count].sprite = kindSprite;
                            _kindIcons[count].color = Color.white;
                            _kindIcons[count].enabled = kindSprite != null;
                        }
                        _titleTexts[count].text = hasKindIcon && kindSprite != null ? quest.title : $"[{QuestListView.KindLabel(quest.kind)}] {quest.title}";
                        if (_assetMemoStyle)
                        {
                            _titleTexts[count].fontSize = 24;
                            _titleTexts[count].color = quest.kind == QuestListView.QuestKind.Main ? new Color(1, .56f, .12f) : new Color(0, .76f, .88f);
                        }
                    }

                    if (_progressTexts[count] != null)
                    {
                        bool ready = quest.State == QuestListView.QuestState.ReadyToComplete; // 준비 완료 여부입니다.
                        _progressTexts[count].text = ready ? "완료 가능 · 스마트폰에서 완료" : $"완료 목표 {quest.CompletedObjectiveCount}/{quest.ObjectiveCount}";
                        _progressTexts[count].color = ready ? new Color(.16f, .48f, .35f) : new Color(.35f, .38f, .40f);
                        if (_assetMemoStyle)
                        {
                            _progressTexts[count].fontSize = 20;
                            _progressTexts[count].color = new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
                        }
                    }
                }

                count++;
            }
        }

        VisibleQuestCount = Mathf.Min(count, capacity);
        if(_assetMemoStyle && _memoBody!=null){
            int visible=Mathf.Max(1,VisibleQuestCount);float height=60+visible*82+Mathf.Max(0,visible-1)*8;
            var background=transform.Find("Background") as RectTransform;
            var body=_memoBody.transform as RectTransform;
            foreach(var rect in new[]{background,body})if(rect!=null){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(326,height);}
            var backdrop=body.Find("MemoBackdrop") as RectTransform;if(backdrop!=null){backdrop.anchorMin=Vector2.zero;backdrop.anchorMax=Vector2.one;backdrop.offsetMin=backdrop.offsetMax=Vector2.zero;}
            for(int i=0;i<_rows.Length;i++)if(_rows[i]!=null){var rect=(RectTransform)_rows[i].transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(0,-71-i*90);rect.sizeDelta=new Vector2(286,82);}
        }
        if (_rows != null)
        {
            // i: 반복 값입니다.
            for (int i = VisibleQuestCount; i < _rows.Length; i++)
            {
                if (_rows[i] != null)
                {
                    _rows[i].SetActive(false);
                }
            }
        }

        if (_emptyText != null)
        {
            _emptyText.text = "진행 중인 할 일이 없습니다.";
            _emptyText.gameObject.SetActive(count == 0);
        }
        if (VisibleQuestCount > 0)
        {
            _emptyFoldHandled = false;
            CancelEmptyFold();
        }
        else if (!_emptyFoldHandled && Application.isPlaying && isActiveAndEnabled)
        {
            _emptyFoldHandled = true;
            _emptyFoldRoutine = StartCoroutine(FoldEmptyMemoAfterLayout());
        }
    }

    /// <summary>초기 데이터·레이아웃 적용을 기다린 뒤 기존 접기 연출을 재생합니다.</summary>
    System.Collections.IEnumerator FoldEmptyMemoAfterLayout()
    {
        yield return null;
        _emptyFoldRoutine = null;
        if (isActiveAndEnabled && VisibleQuestCount == 0 && _expanded) SetExpanded(false);
    }

    void CancelEmptyFold()
    {
        if (_emptyFoldRoutine != null) StopCoroutine(_emptyFoldRoutine);
        _emptyFoldRoutine = null;
    }

    /// <summary>
    /// 메모 목록을 접거나 펼칩니다.
    /// </summary>
    public void ToggleMemo()
    {
        // 사용자가 직접 펼친 빈 메모를 매번 새로 접지는 않습니다.
        CancelEmptyFold();
        if (VisibleQuestCount == 0) _emptyFoldHandled = true;
        SetExpanded(!_expanded);
    }

    /// <summary>
    /// 헤더를 유지하면서 메모 본문과 버튼 문구를 전환합니다.
    /// </summary>
    /// <param name="expanded">펼침 여부입니다.</param>
    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        var fold = GetComponent<MemoFoldView>(); // 접기 값입니다.
        if (fold != null)
        {
            fold.SetExpanded(expanded, _toggleButton != null ? _toggleButton.transform as RectTransform : null);
        }
        else if (_memoBody != null)
        {
            _memoBody.SetActive(expanded);
        }

        // 연출용 View가 없는 기존 구성에서도 버튼의 최종 방향을 맞춥니다.
        if (fold == null && _toggleButton != null)
        {
            Vector3 rotation = _toggleButton.transform.localEulerAngles; // 기존 X·Y 회전을 유지합니다.
            rotation.z = expanded ? -90f : 90f;
            _toggleButton.transform.localEulerAngles = rotation;
        }

        if (_toggleButtonText != null)
        {
            _toggleButtonText.text = expanded ? "접기" : "펼치기";
        }
    }

    #endregion

}
