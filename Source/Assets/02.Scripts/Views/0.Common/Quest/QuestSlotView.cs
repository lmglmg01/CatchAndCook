using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 퀘스트 이름, 종류, 세부 목표 진행도와 상태를 표시하는 목록 슬롯입니다.
/// </summary>
public class QuestSlotView : MonoBehaviour, IPointerClickHandler
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 슬롯 안의 QuestName TMP_Text를 연결합니다. 목록에 퀘스트 제목을 표시합니다.
    [SerializeField] TMP_Text _questNameText;
    // QuestTypeIcon의 Image를 연결합니다. 메인·서브·콘텐츠 종류에 따라 배경색을 바꿉니다.
    [SerializeField] UnityEngine.UI.Image _questTypeImage;
    // QuestTypeIcon 아래 TypeText TMP_Text를 연결합니다. 메인·서브·콘텐츠 문구를 표시합니다.
    [SerializeField] TMP_Text _questTypeText;
    // ProgressText TMP_Text를 연결합니다. 완료한 세부 목표 수 / 전체 세부 목표 수를 표시합니다.
    [SerializeField] TMP_Text _questProgressText;
    // ProgressText/ProgressBackground 아래 PrograssImage의 Image를 연결합니다. 가로 Filled 방식의 진행도 채움 이미지입니다.
    [SerializeField] UnityEngine.UI.Image _questProgressImage;
    // StateBox/StateText TMP_Text를 연결합니다. 수락 전·진행 중·완료 가능·완료 상태를 표시합니다.
    [SerializeField] TMP_Text _questStateText;
    // 슬롯의 NewText TMP_Text를 연결합니다. 아직 상세창에서 확인하지 않은 퀘스트에 NEW를 표시합니다.
    [SerializeField] TMP_Text _newText;

    #endregion

    #region 런타임 상태

    // 이 슬롯이 표시 중인 View 데이터 참조입니다. 임시 데이터 원본과 생성 설정은 QuestListView에서 관리합니다.
    QuestListView.QuestData _quest;
    UnityEngine.UI.Button _button; // 버튼입니다.
    bool _initialized; // 초기화 여부입니다.
    UnityEngine.UI.Image[] _rewardIcons; // 동적으로 복제한 슬롯도 같은 보상 표시를 사용합니다.

    #endregion

    #region 이벤트 액션

    public event Action OnClickQuest; // 클릭 퀘스트 알림입니다.
    public event Action<QuestListView.QuestData> OnQuestSelected; // 퀘스트 선택 알림입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 퀘스트 값입니다.
    /// </summary>
    public QuestListView.QuestData Quest => _quest;

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 키보드와 마우스 선택에 사용할 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 슬롯의 클릭 이벤트 연결을 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(Click);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 같은 클릭 이벤트를 한 번만 연결합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ApplyReferenceLayout();
        _button = GetComponent<UnityEngine.UI.Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(Click);
        }
    }

    /// <summary>
    /// 퀘스트 표시 데이터를 받아 슬롯 전체를 갱신합니다.
    /// </summary>
    /// <param name="quest">퀘스트 값입니다.</param>
    public void Bind(QuestListView.QuestData quest)
    {
        Initialize();
        _quest = quest;
        GetQuestData();
        RefreshRewardIcons();
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>미설정 보상에도 동전 하나를 표시하고 보상 아이콘에 툴팁을 연결합니다.</summary>
    public void RefreshRewardIcons(Sprite fallback = null)
    {
        var screen = GetComponentInParent<QuestScreenView>(true);
        if (fallback == null && screen != null) fallback = screen.RewardFallbackSprite;
        if (_rewardIcons == null)
        {
            var icons = new System.Collections.Generic.List<UnityEngine.UI.Image>();
            foreach (var image in GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if (image.name.StartsWith("RewardIcon", StringComparison.Ordinal)) icons.Add(image);
            icons.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _rewardIcons = icons.ToArray();
        }
        int count = _quest == null || _quest.completed ? 0 :
            Mathf.Min(Mathf.Max(1, _quest.rewards != null ? _quest.rewards.Length : 0), _rewardIcons.Length);
        for (int i = 0; i < _rewardIcons.Length; i++)
        {
            var image = _rewardIcons[i];
            image.gameObject.SetActive(i < count);
            if (i >= count) continue;
            var reward = _quest.rewards != null && i < _quest.rewards.Length ? _quest.rewards[i] : null;
            QuestRewardIconView.Configure(image, reward, fallback);
            image.rectTransform.sizeDelta=new Vector2(65,65);
            image.rectTransform.anchoredPosition = new Vector2(325 - 70 * (count - 1 - i), 0);
        }
    }

    /// <summary>
    /// 현재 퀘스트의 이름, 종류, 상태와 완료한 세부 목표 수를 표시합니다.
    /// </summary>
    public void GetQuestData()
    {
        if (_questNameText != null)
        {
            _questNameText.text = _quest != null ? _quest.title : string.Empty;
            QuestTitleMarqueeView.Apply(_questNameText, _questProgressText != null ? _questProgressText.rectTransform : null);
        }

        if (_questTypeText != null)
        {
            _questTypeText.text = _quest != null ? QuestListView.KindLabel(_quest.kind) : string.Empty;
        }

        if (_questProgressText != null)
        {
            _questProgressText.text = _quest != null ? $"{_quest.CompletedObjectiveCount} / {_quest.ObjectiveCount}" : string.Empty;
        }

        if (_questProgressImage != null)
        {
            _questProgressImage.fillAmount = _quest != null && _quest.ObjectiveCount > 0 ? (float)_quest.CompletedObjectiveCount / _quest.ObjectiveCount : 0f;
        }

        if (_questStateText != null)
        {
            _questStateText.text = _quest != null ? QuestListView.StateLabel(_quest.State) : string.Empty;
        }

        if (_newText != null)
        {
            _newText.text = "NEW";
            _newText.gameObject.SetActive(_quest != null && _quest.isNew);
        }

        if (_questTypeImage != null && _quest != null)
        {
            _questTypeImage.color = Color.white; // 퀘스트 종류 이미지의 원본 색을 유지합니다.
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 유효한 퀘스트의 선택 이벤트를 전달합니다.
    /// </summary>
    void Click()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_quest == null)
        {
            return;
        }

        OnQuestSelected?.Invoke(_quest);
        OnClickQuest?.Invoke();
    }

    #endregion

    /// <summary>780×110 목록 행의 제목·진행도·아이콘 영역을 유지합니다.</summary>
    public void ApplyReferenceLayout()
    {
        void Rect(Transform t,float x,float y,float w,float h){if(t is RectTransform r){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}}
        var layout=GetComponent<UnityEngine.UI.LayoutElement>()??gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.minWidth=780;layout.preferredWidth=780;layout.preferredHeight=110;layout.minHeight=110;
        var viewport=transform.Find("QuestNameViewport") as RectTransform;
        if(viewport!=null){viewport.anchorMin=viewport.anchorMax=new Vector2(.5f,.5f);viewport.pivot=new Vector2(0,.5f);viewport.anchoredPosition=new Vector2(-270,0);viewport.sizeDelta=new Vector2(260,40);}
        else if(_questNameText!=null)Rect(_questNameText.transform,-140,0,260,40);
        if(_questNameText!=null)_questNameText.fontSize=30;
        Rect(transform.Find("QuestTypeIcon"),-325.5f,0,70,70);Rect(transform.Find("ProgressText"),56,0,80,40);if(_questProgressText!=null)_questProgressText.fontSize=30;
        Rect(transform.Find("ActionBadge"),379,46,30,30);
        foreach(var image in GetComponentsInChildren<UnityEngine.UI.Image>(true))if(image.name.StartsWith("RewardIcon",StringComparison.Ordinal))image.rectTransform.sizeDelta=new Vector2(65,65);
    }

    #region 포인터 입력 및 요청

    /// <summary>
    /// Button이 없는 슬롯의 왼쪽 클릭만 처리합니다.
    /// </summary>
    /// <param name="eventData">마우스 좌표와 버튼 정보를 담은 포인터 이벤트입니다.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null && eventData.button == PointerEventData.InputButton.Left)
        {
            Click();
        }
    }

    #endregion

}
