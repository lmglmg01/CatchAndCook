using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 퀘스트 화면의 에셋 교체용 표시와 HUD 숨김만 담당합니다.
/// </summary>
[DefaultExecutionOrder(1100)]
public class QuestScreenView : MonoBehaviour
{
    /// <summary>실행 빌드에서도 사용할 수 있도록 연결된 임시 동전 스프라이트입니다.</summary>
    public Sprite RewardFallbackSprite => _rewardFallbackSprite;

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] Sprite _rewardFallbackSprite; // 보상 아이콘이 없는 경우의 임시 표시입니다.
    [SerializeField] QuestListView _list; // 목록 표시를 연결할 뷰입니다.
    [SerializeField] SlotArt[] _slots; // 슬롯 목록입니다.
    [SerializeField] UnityEngine.UI.Button[] _tabs; // 탭 목록입니다.
    [SerializeField] UnityEngine.UI.Image[] _tabIcons; // 탭 아이콘 목록입니다.
    [Tooltip("메인, 서브, 콘텐츠 순서")]
    [SerializeField] Sprite[] _kindSprites = new Sprite[3]; // 분류 스프라이트 목록입니다.
    [Tooltip("수락 전, 진행 중, 보상 가능, 완료 순서")]
    [SerializeField] Sprite[] _stateSprites = new Sprite[4]; // 상태 스프라이트 목록입니다.
    [SerializeField] Sprite[] _selectedTabIcons; // 탭 순서에 맞춘 선택 상태의 반전 아이콘입니다. 없는 항목은 기존 색상 전환을 사용합니다.
    [SerializeField] Sprite[] _tabSprites = new Sprite[3]; // 탭 스프라이트 목록입니다.
    [SerializeField] Sprite _selectedCompletedSprite; // 선택된 완료 행의 진한 체크 아이콘입니다.
    [SerializeField] Sprite _selectionSprite; // 선택 스프라이트 이미지입니다.
    [SerializeField] Sprite _normalRowSprite, _selectedRowSprite; // 퀘스트 행의 미선택/선택 배경 에셋을 연결합니다.
    [SerializeField] Sprite _normalTabSprite, _selectedTabSprite; // 탭의 미선택/선택 배경 에셋을 연결합니다.
    [SerializeField] CanvasGroup _commonHud; // 공통 HUD 값입니다.
    [SerializeField] Color _normal = new Color(.01f, .17f, .23f, .96f); // 기본 색상입니다.
    [SerializeField] Color _selected = new Color(1f, .97f, .88f); // 선택 색상입니다.

    #endregion

    #region 런타임 상태

    readonly Dictionary<CanvasGroup, Vector3> _hidden = new Dictionary<CanvasGroup, Vector3>(); // 숨김에 사용하는 대상별 매핑입니다.

    #endregion

    #region UI 연결 및 표시 설정

    [Header("DOTween 연출")]
    [SerializeField] CanvasGroup _screenGroup; // 화면 그룹 값입니다.
    [SerializeField, Min(0)] float _fadeDuration = .2f; // 페이드 지속 시간 값입니다.
    [SerializeField, Min(0)] float _selectionDuration = .12f; // 선택 지속 시간 값입니다.

    #endregion

    #region 런타임 상태

    Tween _fadeTween; // 페이드 트윈 값입니다.
    readonly Dictionary<UnityEngine.UI.Image, Tween> _colorTweens = new Dictionary<UnityEngine.UI.Image, Tween>(); // 색상 트윈 목록에 사용하는 대상별 매핑입니다.
    readonly Dictionary<UnityEngine.UI.Image, Color> _colorTargets = new Dictionary<UnityEngine.UI.Image, Color>(); // 색상 대상 목록에 사용하는 대상별 매핑입니다.

    #endregion

    static void Place(Transform t,float x,float y,float w,float h)
    {
        if(t is RectTransform r){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
    }
    /// <summary>최신 구상도의 공통 퀘스트 창 크기와 여백을 적용합니다.</summary>
    public void ApplyReferenceLayout()
    {
        var motion=transform.Find("WindowMotion");if(motion==null)return;
        var page=motion.Find("Quest_Main/Page");if(page==null)return;
        var tabs=page.Find("Tabs");Place(tabs,0,420,940,90);
        for(int i=0;i<_tabs.Length;i++)Place(_tabs[i].transform,-327+i*327,0,286,84);
        var list=page.Find("List");Place(list,-360,-66,900,700);
        var scroll=list.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);Place(scroll.transform,0,0,900,700);Place(scroll.viewport,0,0,812,620);
        var grid=scroll.content.GetComponent<UnityEngine.UI.GridLayoutGroup>();if(grid!=null){grid.cellSize=new Vector2(780,110);grid.spacing=new Vector2(0,10);grid.padding=new RectOffset(16,16,16,14);grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=1;grid.childAlignment=TextAnchor.UpperLeft;}
        scroll.content.anchorMin=new Vector2(0,1);scroll.content.anchorMax=new Vector2(1,1);scroll.content.pivot=new Vector2(.5f,1);scroll.content.sizeDelta=new Vector2(0,scroll.content.sizeDelta.y);scroll.content.anchoredPosition=Vector2.zero;
        var layout=scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();if(layout!=null){layout.padding=new RectOffset(16,16,16,14);layout.spacing=10;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;}
        if(scroll.verticalScrollbar!=null){Place(scroll.verticalScrollbar.transform,416,14.5f,14,483);scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;}
        foreach(var slot in GetComponentsInChildren<QuestSlotView>(true))slot.ApplyReferenceLayout();
        var detail=motion.Find("Quest_Details");Place(detail,494,-66,700,700);
        Place(detail.Find("QuestTypeIcon"),-259,282,62,56);
        var titleTransform=detail.Find("QuestNameText");
        var titleViewport=detail.Find("QuestNameTextViewport");
        if(titleViewport!=null){Place(titleViewport,28,279,460,50);titleTransform=titleViewport.Find("QuestNameText");}
        else Place(titleTransform,28,279,460,50);
        if(titleTransform!=null){var title=titleTransform.GetComponent<TMP_Text>();title.fontSize=40;title.enableAutoSizing=false;title.fontSizeMax=40;}
        var content=detail.Find("ContentArea");Place(content,0,176,532,110);Place(content.Find("QuestContent"),0,0,532,110);
        var description=content.Find("QuestContent").GetComponent<TMP_Text>();description.fontSize=22;description.color=new Color32(183,178,163,255);
        var area=detail.Find("ConditionArea");Place(area,0,-44,600,283);int n=0;
        foreach(var condition in area.GetComponentsInChildren<QuestConditionView>(true)){
            if(area.GetComponent<UnityEngine.UI.ScrollRect>()==null)Place(condition.transform,0,109-n++*73,600,64);
            Place(condition.transform.Find("ConditionText"),-114,0,320,54);condition.transform.Find("ConditionText").GetComponent<TMP_Text>().fontSize=24;
            Place(condition.transform.Find("AmountText"),84,0,60,44);Place(condition.transform.Find("ProgressTrack"),170,0,120,22);
        }
        Place(detail.Find("AcceptButton"),0,-263,240,88);Place(detail.Find("AcceptButton/OpenText"),0,5,240,78);
        var label=detail.Find("AcceptButton/OpenText").GetComponent<TMP_Text>();label.fontSize=40;
        Place(detail.Find("AbandonButton"),0,-263,240,88);
        var back=motion.Find("CloseButton/Label");if(back!=null)back.GetComponent<TMP_Text>().text="퀘스트";
    }

    #region 표시 데이터 형식

    [Serializable]
    public struct SlotArt
    {

        #region 멤버

        #region 런타임 상태

        public UnityEngine.UI.Image[] rewardIcons; // 목록 오른쪽의 보상 아이콘입니다.
        public GameObject actionBadge; // 수락·보상 가능 상태의 알림 점입니다.
        public QuestSlotView view; // UI 동작과 데이터를 연결하는 뷰입니다.
        // background: 배경 이미지입니다.
        // kindIcon: 분류 아이콘 이미지입니다.
        // stateIcon: 상태 아이콘 이미지입니다.
        public UnityEngine.UI.Image background, kindIcon, stateIcon;
        // title: 제목 표시 텍스트입니다.
        // progress: 진행도 표시 텍스트입니다.
        // kindLabel: 분류 문구 표시 텍스트입니다.
        // stateLabel: 상태 문구 표시 텍스트입니다.
        public TMP_Text title, progress, kindLabel, stateLabel;
        public GameObject selectionMark; // 선택 표시 값입니다.

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    // DOTween 연출: 화면 진입 페이드. 게임의 시간 배율과 독립적으로 재생합니다.

    /// <summary>
    /// 뷰가 켜질 때 표시와 이벤트 연결을 갱신합니다.
    /// </summary>
    void OnEnable()
    {
        ApplyReferenceLayout();
        if (_screenGroup != null && GetComponentInParent<AppFadeView>() == null)
        {
            _fadeTween?.Kill();
            _screenGroup.alpha = 0;
            _fadeTween = _screenGroup.DOFade(1, _fadeDuration).SetUpdate(true);
        }

        HideHud(_commonHud);
        // 선상 전용 가지는 원본 공통 프리팹에 참조를 저장하지 않고 현재 Canvas에서만 찾습니다.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // name: 이름 값입니다.
            foreach (var name in new[]
            { "Interact_Dive", "Interact_NPC" }

            )
            {
                var branch = canvas.transform.Find(name); // 분기 값입니다.
                if (branch != null)
                {
                    HideHud(branch.GetComponent<CanvasGroup>());
                }
            }
        }

        RefreshPresentation();
    }

    /// <summary>
    /// 다른 갱신이 끝난 뒤 화면 표시를 맞춥니다.
    /// </summary>
    void LateUpdate()
    {
        // group: 그룹 값입니다.
        foreach (var group in _hidden.Keys)
        {
            if (group != null)
            {
                group.alpha = 0;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }
    }

    /// <summary>
    /// 뷰가 꺼질 때 이벤트와 진행 중인 표시를 정리합니다.
    /// </summary>
    void OnDisable()
    {
        // DOTween 연출 정리: 화면을 닫을 때 이 View가 소유한 Tween만 종료합니다.
        _fadeTween?.Kill();
        _fadeTween = null;
        // tween: 트윈 값입니다.
        foreach (var tween in _colorTweens.Values)
        {
            tween?.Kill();
        }

        _colorTweens.Clear();
        _colorTargets.Clear();
        if (_screenGroup != null)
        {
            _screenGroup.alpha = 1;
        }

        // pair: pair 값입니다.
        foreach (var pair in _hidden)
        {
            if (pair.Key != null)
            {
                pair.Key.alpha = pair.Value.x;
                pair.Key.interactable = pair.Value.y > .5f;
                pair.Key.blocksRaycasts = pair.Value.z > .5f;
            }
        }

        _hidden.Clear();
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 기존 HUD 표시값을 보관하고 퀘스트 화면 뒤에서 숨깁니다.
    /// </summary>
    /// <param name="group">그룹 값입니다.</param>
    void HideHud(CanvasGroup group)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (group == null || _hidden.ContainsKey(group))
        {
            return;
        }

        _hidden[group] = new Vector3(group.alpha, group.interactable ? 1 : 0, group.blocksRaycasts ? 1 : 0);
        group.alpha = 0;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 범위 안의 스프라이트를 반환하고 범위 밖이면 null을 반환합니다.
    /// </summary>
    /// <param name="sprites">스프라이트 목록입니다.</param>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    Sprite At(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    /// <summary>
    /// 종류·상태 아이콘을 원래 비율로 표시하고 에셋이 없을 때만 대체 문구를 켭니다.
    /// </summary>
    /// <param name="image">이미지입니다.</param>
    /// <param name="fallback">대체 표시 텍스트입니다.</param>
    /// <param name="sprite">스프라이트 이미지입니다.</param>
    /// <param name="label">문구 값입니다.</param>
    /// <param name="color">이미지에 적용할 목표 색상입니다.</param>
    void Icon(UnityEngine.UI.Image image, TMP_Text fallback, Sprite sprite, string label, Color color)
    {
        if (image != null)
        {
            // 이전 이미지의 대체 스프라이트와 배경용 Sliced 설정이 상태 아이콘을 가리지 않게 초기화합니다.
            image.overrideSprite = null;
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            image.color = sprite != null ? Color.white : color;
        }

        if (fallback != null)
        {
            fallback.text = label;
            fallback.gameObject.SetActive(sprite == null);
        }
    }

    // DOTween 연출: 선택 배경만 부드럽게 전환하며 데이터 갱신은 즉시 반영합니다.

    /// <summary>
    /// 선택 상태의 목표 색상으로 이미지를 전환합니다.
    /// </summary>
    /// <param name="image">이미지입니다.</param>
    /// <param name="target">이미지에 적용할 목표 색상입니다.</param>
    void SetSelectionColor(UnityEngine.UI.Image image, Color target)
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_colorTargets.TryGetValue(image, out var previous) && previous == target)
        {
            return;
        }

        if (_colorTweens.TryGetValue(image, out var tween))
        {
            tween?.Kill();
        }

        _colorTargets[image] = target;
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!isActiveAndEnabled || _selectionDuration <= 0)
        {
            image.color = target;
            return;
        }

        _colorTweens[image] = image.DOColor(target, _selectionDuration).SetUpdate(true);
    }

    /// <summary>
    /// 퀘스트 탭과 슬롯의 선택·상태 아이콘을 갱신합니다.
    /// </summary>
    public void RefreshPresentation()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_list == null)
        {
            return;
        }

        if (_slots != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _slots)
            {
                var quest = slot.view != null ? slot.view.Quest : null; // 퀘스트 값입니다.
                if (slot.view != null)
                {
                    var badge = slot.view.transform.Find("NewText"); // 표식 값입니다.
                    if (badge != null)
                    {
                        badge.gameObject.SetActive(false);
                    }
                }

                bool selected = quest != null && quest == _list.SelectedQuest; // 선택 여부입니다.
                if (slot.background != null)
                {
                    var art = selected ? _selectedRowSprite : _normalRowSprite; // 디자인 값입니다.
                    if (art != null)
                    {
                        slot.background.sprite = art;
                    }

                    SetSelectionColor(slot.background, art != null ? new Color(1f,1f,1f,.9f) : selected ? _selected : _normal);
                }

                if (slot.title != null)
                {
                    slot.title.color = selected ? _normal : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
                }

                if (slot.progress != null)
                {
                    slot.progress.gameObject.SetActive(quest != null && !quest.completed);
                    if (quest != null) slot.progress.text = quest.State == QuestListView.QuestState.NotAccepted ? "대기중" : quest.State == QuestListView.QuestState.ReadyToComplete ? "완료!" : quest.CompletedObjectiveCount + "/" + quest.ObjectiveCount;
                    slot.progress.color = selected ? new Color(.30f, .39f, .43f) : new Color(.55f, .69f, .73f);
                }

                if (slot.selectionMark != null)
                {
                    slot.selectionMark.SetActive(false);
                    var image = slot.selectionMark.GetComponent<UnityEngine.UI.Image>(); // 이미지 값입니다.
                    if (image != null)
                    {
                        // 선택 표시의 틴트는 선택 상태와 관계없이 항상 불투명한 흰색으로 유지합니다.
                        image.color = Color.white;
                        if (_selectionSprite != null)
                        {
                            image.sprite = _selectionSprite;
                        }
                    }

                    var fallback = slot.selectionMark.transform.Find("Placeholder"); // 대체 값입니다.
                    if (fallback != null)
                    {
                        fallback.gameObject.SetActive(_selectionSprite == null);
                    }
                }

                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (quest == null)
                {
                    continue;
                }

                Icon(slot.kindIcon, slot.kindLabel, At(_kindSprites, (int)quest.kind), QuestListView.KindLabel(quest.kind), quest.kind == QuestListView.QuestKind.Main ? new Color(1, .46f, .16f) : new Color(.27f, .46f, 1));
                if(slot.kindIcon!=null)slot.kindIcon.rectTransform.sizeDelta=quest.kind==QuestListView.QuestKind.Main?new Vector2(71.3f,71.3f):new Vector2(67,67);
                Icon(slot.stateIcon, slot.stateLabel, selected && quest.completed ? _selectedCompletedSprite : At(_stateSprites, (int)quest.State), quest.State == QuestListView.QuestState.ReadyToComplete ? "보상" : QuestListView.StateLabel(quest.State), selected ? _normal : _selected);
                if (slot.stateIcon != null && slot.stateIcon.sprite != null)
                {
                    slot.stateIcon.color = Color.white; // 상태 아이콘의 원본 색을 유지합니다.
                }

                // 완료 탭의 체크 표시는 유지하고, 나머지 상태에서는 목록에 보상을 표시합니다.
                if (slot.stateIcon != null) slot.stateIcon.gameObject.SetActive(quest.completed);
                if (slot.actionBadge != null) slot.actionBadge.SetActive(!quest.completed && (quest.State == QuestListView.QuestState.NotAccepted || quest.State == QuestListView.QuestState.ReadyToComplete));
                if (slot.view != null) slot.view.RefreshRewardIcons(_rewardFallbackSprite);
                if (slot.stateLabel != null)
                {
                    slot.stateLabel.color = selected ? new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f) : _normal;
                }
            }
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_tabs == null)
        {
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _tabs.Length; i++)
        {
            var button = _tabs[i]; // 버튼 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (button == null)
            {
                continue;
            }

            button.transition=UnityEngine.UI.Selectable.Transition.None;
            button.targetGraphic.canvasRenderer.SetColor(Color.white);
            bool selected = i == _list.CurrentTab; // 선택 여부입니다.
            var tabArt = selected ? _selectedTabSprite : _normalTabSprite; // 탭 디자인 값입니다.
            if (button.targetGraphic is UnityEngine.UI.Image bg && tabArt != null)
            {
                bg.sprite = tabArt;
            }

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = tabArt != null ? Color.white : selected ? _selected : _normal;
            }

            // text: 텍스트 값입니다.
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = selected ? _normal : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
            }

            if (_tabIcons != null && i < _tabIcons.Length && _tabIcons[i] != null)
            {
                var icon = _tabIcons[i]; // 아이콘 값입니다.
                Sprite normalIcon = _tabSprites != null && i < _tabSprites.Length ? _tabSprites[i] : null;
                Sprite selectedIcon = _selectedTabIcons != null && i < _selectedTabIcons.Length ? _selectedTabIcons[i] : null;
                // 반전 아이콘은 원본 색을 유지하고, 미제공 탭은 기존 색상 전환을 유지합니다.
                icon.sprite = selected && selectedIcon != null ? selectedIcon : normalIcon;
                icon.color = selected && selectedIcon == null ? new Color(0f, .2117647f, .3333333f, 1f) : Color.white;
            }
        }
    }

    #endregion

}

