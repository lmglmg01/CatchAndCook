using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Canvas_Board 전용 카드 선택·정렬·상세 표시 및 연출. 실제 장비·재화는 변경하지 않습니다.
/// </summary>
[DefaultExecutionOrder(1200)]
public class UpgradeEquipScreenView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] UpgradeEquipView _view; // UI 동작과 데이터를 연결하는 뷰입니다.
    [SerializeField] CardArt[] _cards; // 강화 카드별 슬롯·선택 버튼·배경·텍스트 참조를 연결합니다.
    [SerializeField] UnityEngine.UI.Button[] _tabs; // 장비 분류 탭 버튼을 기존 순서대로 연결합니다.
    [SerializeField] UnityEngine.UI.Image[] _tabIcons; // 각 장비 탭의 아이콘 이미지를 버튼과 같은 순서로 연결합니다.
    [SerializeField] Sprite[] _normalTabIcons; // 전체, 기본장비, 근접무기, 원거리무기 순서의 기본 아이콘입니다.
    [SerializeField] Sprite[] _selectedTabIcons; // 각 탭의 반전 아이콘이며, 미제공 항목은 기존 색상 전환을 사용합니다.
    [SerializeField] Sprite _placeholderSprite; // 임시 표시 스프라이트 이미지입니다.
    [SerializeField] Sprite _normalCardSprite, _selectedCardSprite; // 장비 카드의 미선택/선택 배경 에셋을 연결합니다.
    [SerializeField] Sprite _levelFrameSprite, _lockedCardSprite, _lockedIconSprite;
    [SerializeField] Sprite _normalTabSprite, _selectedTabSprite; // 장비 탭의 미선택/선택 배경 에셋을 연결합니다.
    [SerializeField] UnityEngine.UI.Image[] _replaceableIcons; // 장비 표시 상태에 따라 아이콘을 교체할 이미지 목록입니다.
    [SerializeField] UnityEngine.UI.Image _detailIcon; // 상세 아이콘 이미지입니다.
    // _iconPlaceholder: 아이콘 임시 표시 표시 텍스트입니다.
    // _name: 이름 표시 텍스트입니다.
    // _level: 레벨 표시 텍스트입니다.
    // _category: 분류 표시 텍스트입니다.
    // _description: 설명 표시 텍스트입니다.
    // _statLabels: 능력치 문구 목록 표시 텍스트입니다.
    // _current: 현재 표시 텍스트입니다.
    // _next: 다음 표시 텍스트입니다.
    // _arrows: 화살표 표시 텍스트입니다.
    // _cost: 비용 표시 텍스트입니다.
    // _gold: 골드 표시 텍스트입니다.
    [SerializeField] TMP_Text _iconPlaceholder, _name, _level, _category, _description, _statLabels, _current, _next, _arrows, _cost, _gold;
    [SerializeField] UpgradeConfirmationPopupView _confirmationPopup; // 강화 여부 확인창입니다.
    [SerializeField] UnityEngine.UI.Button _upgrade; // 강화 버튼입니다.
    // _screenGroup: 화면 그룹 값입니다.
    // _detailGroup: 상세 그룹 값입니다.
    [SerializeField] CanvasGroup _screenGroup, _detailGroup;
    [SerializeField] CanvasGroup[] _hideGroups; // 숨김 그룹 목록입니다.

    #endregion

    #region 런타임 상태

    readonly Dictionary<CanvasGroup, Vector3> _hidden = new Dictionary<CanvasGroup, Vector3>(); // 숨김에 사용하는 대상별 매핑입니다.
    readonly Dictionary<UnityEngine.UI.Image, Tween> _colors = new Dictionary<UnityEngine.UI.Image, Tween>(); // 색상 목록에 사용하는 대상별 매핑입니다.
    readonly Dictionary<UnityEngine.UI.Image, Color> _targets = new Dictionary<UnityEngine.UI.Image, Color>(); // 대상 목록에 사용하는 대상별 매핑입니다.
    UnityEngine.Events.UnityAction[] _selectCallbacks; // 선택 콜백 목록입니다.
    // _openTween: 열기 트윈 값입니다.
    // _detailTween: 상세 트윈 값입니다.
    // _feedbackTween: 피드백 트윈 값입니다.
    Tween _openTween, _detailTween, _feedbackTween;
    int _selectedType = -1; // 선택 종류 값입니다.
    bool _wired; // 입력 연결 여부입니다.
    // _navy: 남색 색상입니다.
    // _cream: 크림색 색상입니다.
    readonly Color _navy = new Color(.01f, .18f, .25f, .98f), _cream = new Color(1, .97f, .88f);
    static readonly string[] StatNames =
    { "공격력", "사거리", "최대 산소량", "이동 속도", "방어력", "최대 적재량", "탄 수" }; // 능력치 이름 목록입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 선택 장비 종류 값입니다.
    /// </summary>
    public int SelectedEquipmentType => _selectedType;

    #endregion

    #region 표시 데이터 형식

    [Serializable]
    public struct CardArt
    {

        #region 멤버

        #region 런타임 상태

        public UpgradeEquipSlotView slot; // 슬롯 표시를 연결할 뷰입니다.
        public UnityEngine.UI.Button selectButton; // 선택 버튼입니다.
        // background: 배경 이미지입니다.
        // border: 테두리 이미지입니다.
        public UnityEngine.UI.Image background, border;
        // title: 제목 표시 텍스트입니다.
        // level: 레벨 표시 텍스트입니다.
        public TMP_Text title, level;

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// UI 참조와 입력 연결을 초기화합니다.
    /// </summary>
    void Awake()
    {
        Wire();
    }

    /// <summary>
    /// 뷰가 켜질 때 표시와 이벤트 연결을 갱신합니다.
    /// </summary>
    void OnEnable()
    {
        Wire();
        ApplyReferenceLayout();
        _selectedType = -1;
        // group: 그룹 값입니다.
        foreach (var group in _hideGroups)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (group == null)
            {
                continue;
            }

            _hidden[group] = new Vector3(group.alpha, group.interactable ? 1 : 0, group.blocksRaycasts ? 1 : 0);
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        // 공통 등장 연출이 있으면 기존 화면 전체 페이드를 중복 재생하지 않습니다.
        if (Application.isPlaying && GetComponentInParent<CommonWindowEntranceView>() == null)
        {
            _screenGroup.alpha = 0;
            _openTween = _screenGroup.DOFade(1, .2f).SetUpdate(true);
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
        // DOTween 연출 정리: 이 화면에서 만든 Tween만 종료하고 HUD를 원래 상태로 돌립니다.
        _openTween?.Kill();
        _detailTween?.Kill();
        _feedbackTween?.Kill();
        // tween: 트윈 값입니다.
        foreach (var tween in _colors.Values)
        {
            tween?.Kill();
        }

        _colors.Clear();
        _targets.Clear();
        if (_screenGroup != null)
        {
            _screenGroup.alpha = 1;
        }

        if (_detailGroup != null)
        {
            _detailGroup.alpha = 1;
            _detailGroup.transform.localScale = Vector3.one;
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

    /// <summary>
    /// 뷰가 제거될 때 등록한 이벤트와 연출을 해제합니다.
    /// </summary>
    void OnDestroy()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (!_wired)
        {
            return;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _cards.Length; i++)
        {
            if (_cards[i].selectButton != null)
            {
                _cards[i].selectButton.onClick.RemoveListener(_selectCallbacks[i]);
            }
        }

        if (_tabs[0] != null && _view != null)
        {
            _tabs[0].onClick.RemoveListener(_view.ShowAllEquipment);
        }

        if (_upgrade != null)
        {
            _upgrade.onClick.RemoveListener(UpgradeSelected);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 장비 카드와 강화 버튼에 기존 뷰의 입력 함수를 연결합니다.
    /// </summary>
    void Wire()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_wired)
        {
            return;
        }

        _wired = true;
        _selectCallbacks = new UnityEngine.Events.UnityAction[_cards.Length];
        // i: 반복 값입니다.
        for (int i = 0; i < _cards.Length; i++)
        {
            var slot = _cards[i].slot; // 슬롯 값입니다.
            _selectCallbacks[i] = () => SelectEquipment(slot.EquipmentType);
            _cards[i].selectButton.onClick.AddListener(_selectCallbacks[i]);
        }

        _tabs[0].onClick.AddListener(_view.ShowAllEquipment);
        _upgrade.onClick.AddListener(UpgradeSelected);
    }

    #endregion

    #region UI 표시 및 갱신

    // DOTween 연출: 카드·탭 선택 색상 전환. 같은 상태 갱신에서는 재시작하지 않습니다.

    /// <summary>
    /// 같은 목표색으로 중복 실행하지 않고 색상 트윈을 적용합니다.
    /// </summary>
    /// <param name="image">이미지입니다.</param>
    /// <param name="target">이미지에 적용할 목표 색상입니다.</param>
    void Tint(UnityEngine.UI.Image image, Color target)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (image == null)
        {
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_targets.TryGetValue(image, out var old) && old == target)
        {
            return;
        }

        if (_colors.TryGetValue(image, out var tween))
        {
            tween?.Kill();
        }

        _targets[image] = target;
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            image.color = target;
            return;
        }

        _colors[image] = image.DOColor(target, .12f).SetUpdate(true);
    }

    static void SetRect(Transform t,float x,float y,float w,float h)
    {
        if(t is RectTransform r){r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
    }
    /// <summary>최신 장비 구상도의 크기와 간격을 기존 UI에 적용합니다.</summary>
    public void ApplyReferenceLayout()
    {
        var motion=transform.Find("WindowMotion");if(motion==null)return;
        var background=motion.Find("Background");var tabs=background.Find("Tabs").GetComponent<UnityEngine.UI.GridLayoutGroup>();
        tabs.cellSize=new Vector2(286,84);tabs.spacing=new Vector2(40,0);tabs.padding=new RectOffset(316,0,74,0);
        foreach(var tab in _tabs)foreach(var text in tab.GetComponentsInChildren<TMP_Text>(true))if(text.transform.parent==tab.transform){text.fontSize=36;text.enableAutoSizing=false;}
        var close=motion.Find("CloseButton/CloseLabel").GetComponent<TMP_Text>();close.text="장비강화";close.fontSize=36;
        SetRect(_detailGroup.transform,496,-64,700,600);
        var detail=_detailGroup.transform;SetRect(detail.Find("EquipmentIconBackground"),-161,105,266,266);
        SetRect(_name.transform,170,165,290,60);_name.fontSize=40;_name.enableAutoSizing=true;_name.fontSizeMin=24;_name.fontSizeMax=40;
        SetRect(detail.Find("LevelBadge"),67,102,86,46);SetRect(detail.Find("CategoryBadge"),203,102,154,46);
        _level.fontSize=_category.fontSize=20;
        SetRect(_description.transform,170,4,290,90);_description.fontSize=20;_description.color=new Color32(183,178,163,255);
        SetRect(detail.Find("StatComparison"),0,-91,600,100);
        foreach(var text in new[]{_statLabels,_current,_next})text.fontSize=24;
        _arrows.fontSize=48;_arrows.enableAutoSizing=false;_arrows.richText=true;_arrows.lineSpacing=0;
        SetRect(_arrows.transform,68,3,54,90);
        _next.color=_arrows.color=new Color32(54,209,140,255);
        SetRect(detail.Find("UpgradeCostBackground"),-136,-210,200,52);_cost.fontSize=30;
        SetRect(_upgrade.transform,156,-206,240,88);_upgrade.transition=UnityEngine.UI.Selectable.Transition.None;_upgrade.targetGraphic.canvasRenderer.SetColor(Color.white);
        var label=_upgrade.GetComponentInChildren<TMP_Text>(true);SetRect(label.transform,0,5,240,78);label.fontSize=36;
        foreach(var card in _cards)if(card.title!=null){card.title.fontSize=30;SetRect(card.title.transform,0,-105,232,75);var divider=card.slot.transform.Find("IconDivider");if(divider!=null)divider.gameObject.SetActive(false);}
    }

    /// <summary>
    /// 장비 종류를 선택하고 상세 정보를 갱신합니다.
    /// </summary>
    /// <param name="type">종류 값입니다.</param>
    public void SelectEquipment(int type)
    {
        _selectedType = type;
        RefreshPresentation();
        // DOTween 연출: 선택한 장비의 상세 내용을 짧게 페이드 인합니다.
        if (Application.isPlaying && isActiveAndEnabled)
        {
            _detailTween?.Kill();
            _detailGroup.alpha = .4f;
            _detailTween = _detailGroup.DOFade(1, .14f).SetUpdate(true);
        }
    }

    /// <summary>
    /// 장비 카드·탭·상세 정보와 재화 표시를 갱신합니다.
    /// </summary>
    public void RefreshPresentation()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_cards == null || _view == null)
        {
            return;
        }

        // 최대 강화는 최하단. 나머지는 강화 가능 → 최대 레벨까지 남은 단계 오름차순 → 이름순.
        var visible = _cards.Where(c => c.slot != null && c.slot.gameObject.activeSelf && c.slot.PresentationData.HasInfo).OrderBy(c => c.slot.PresentationData.IsMaxLevel ? 1 : 0).ThenByDescending(c => c.slot.CanUpgradeForSorting).ThenBy(c => Mathf.Max(0, c.slot.PresentationData.MaxLevel - c.slot.PresentationData.Level)).ThenBy(c => c.slot.PresentationData.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("ko-KR"), false)).ToArray();
        // i: 반복 값입니다.
        for (int i = 0; i < visible.Length; i++)
        {
            visible[i].slot.transform.SetSiblingIndex(i);
        }

        if (!visible.Any(c => c.slot.EquipmentType == _selectedType))
        {
            _selectedType = visible.Length > 0 ? visible[0].slot.EquipmentType : -1;
        }

        // art: 디자인 값입니다.
        foreach (var art in _cards)
        {
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (art.slot == null)
            {
                continue;
            }

            var data = art.slot.PresentationData; // 데이터 값입니다.
            bool maximum = data.HasInfo && (data.IsMaxLevel || data.MaxLevel > 0 && data.Level >= data.MaxLevel);
            bool selected = data.HasInfo && data.Type == _selectedType; // 선택 여부입니다.
            var cardSprite = !data.HasInfo && _lockedCardSprite != null ? _lockedCardSprite : selected ? _selectedCardSprite : _normalCardSprite; // 카드 스프라이트 값입니다.
            if (art.background != null && cardSprite != null)
            {
                art.background.sprite = cardSprite;
            }

            Tint(art.background, cardSprite != null ? Color.white : selected ? _cream : _navy);
            art.border.gameObject.SetActive(selected && _selectedCardSprite == null);
            if(art.background!=null)art.background.type=UnityEngine.UI.Image.Type.Simple;
            art.title.color = selected ? _navy : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
            art.level.text = data.HasInfo ? (maximum ? "최대" : data.Level.ToString()) : "—";
            art.level.enableAutoSizing = false;
            art.level.fontSize = 30f;
            art.level.alignment = maximum ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            art.level.margin = Vector4.zero;
            var frame=art.level.transform.parent;
            if(frame.parent!=art.slot.transform)frame.SetParent(art.slot.transform,false);
            // Keep the level ribbon inside the card artwork; selected frames have a thicker border.
            SetRect(frame,maximum?(selected?-68:-70):(selected?-63:-67),maximum?(selected?108.5f:110.5f):(selected?102.5f:107.5f),96,101);
            var frameImage=frame.GetComponent<UnityEngine.UI.Image>();
            if(frameImage!=null){frameImage.sprite=_levelFrameSprite;frameImage.type=UnityEngine.UI.Image.Type.Simple;frameImage.color=Color.white;frameImage.enabled=data.HasInfo&&!maximum;}
            SetRect(art.level.transform,maximum?18:-19,19, maximum?100:62,38);
            art.level.rectTransform.localRotation=Quaternion.identity;
            art.level.color=maximum?new Color32(255,80,89,255):new Color32(253,247,228,255);
            art.level.gameObject.SetActive(data.HasInfo);
            frame.SetAsLastSibling();
            var locked=art.slot.transform.Find("LockedIcon");
            if(locked==null){var go=new GameObject("LockedIcon",typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(art.slot.transform,false);locked=go.transform;}
            SetRect(locked,0,60,70,80);var lockImage=locked.GetComponent<UnityEngine.UI.Image>();lockImage.sprite=_lockedIconSprite;lockImage.preserveAspect=true;lockImage.raycastTarget=false;locked.gameObject.SetActive(!data.HasInfo);
            art.selectButton.interactable = data.HasInfo;
        }

        // i: 반복 값입니다.
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool selected = i == 0 ? _view.IsAllEquipmentTab : !_view.IsAllEquipmentTab && (int)_view.CurrentEquipmentTab == i - 1; // 선택 여부입니다.
            _tabs[i].transition=UnityEngine.UI.Selectable.Transition.None;
            _tabs[i].targetGraphic.canvasRenderer.SetColor(Color.white);
            var tabImage = _tabs[i].targetGraphic as UnityEngine.UI.Image; // 탭 이미지 값입니다.
            var tabSprite = selected ? _selectedTabSprite : _normalTabSprite; // 탭 스프라이트 값입니다.
            if (tabImage != null && tabSprite != null)
            {
                tabImage.sprite = tabSprite;
            }

            Tint(tabImage, tabSprite != null ? Color.white : selected ? _cream : _navy);
            // text: 텍스트 값입니다.
            foreach (var text in _tabs[i].GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = selected ? _navy : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
            }

            if (_tabIcons[i] != null)
            {
                Sprite normalIcon = _normalTabIcons != null && i < _normalTabIcons.Length ? _normalTabIcons[i] : null;
                Sprite selectedIcon = _selectedTabIcons != null && i < _selectedTabIcons.Length ? _selectedTabIcons[i] : null;
                if (normalIcon != null)
                    _tabIcons[i].sprite = selected && selectedIcon != null ? selectedIcon : normalIcon;
                _tabIcons[i].color = selected && selectedIcon == null ? new Color(0f, .2117647f, .3333333f, 1f) : Color.white;
            }
        }

        if (_replaceableIcons != null)
        {
            // image: 이미지 값입니다.
            foreach (var image in _replaceableIcons)
            {
                if (image != null)
                {
                    // label: 문구 값입니다.
                    foreach (var label in image.GetComponentsInChildren<TMP_Text>(true))
                    {
                        label.gameObject.SetActive(image.sprite == _placeholderSprite);
                    }
                }
            }
        }

        _gold.text = _view.HasDisplayGold ? _view.DisplayGold.ToString("N0") : "—";
        var slot = visible.Select(c => c.slot).FirstOrDefault(s => s.EquipmentType == _selectedType); // 슬롯 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (slot == null)
        {
            _name.text = "장비를 선택하세요";
            _level.text = "";
            _category.text = "";
            _description.text = "";
            _statLabels.text = _current.text = _next.text = _arrows.text = "";
            _cost.text = "—";
            _upgrade.interactable = false;
            _detailIcon.enabled = false;
            _iconPlaceholder.text = "";
            return;
        }

        var d = slot.PresentationData; // 대상 값입니다.
        _name.text = d.Name;
        _description.text = d.Description;
        _level.text = d.IsMaxLevel ? "최대" : "레벨 " + d.Level;
        _level.color = d.IsMaxLevel ? new Color32(255,80,89,255) : new Color32(253,247,228,255);
        var levelBadge = _level.transform.parent.GetComponent<UnityEngine.UI.Image>();
        if (levelBadge != null) levelBadge.enabled = !d.IsMaxLevel;
        _category.text = d.Type == 0 ? "근접무기" : d.Type == 5 ? "원거리무기" : "기본장비";
        _cost.text = d.IsMaxLevel ? "최대 강화" : d.HasCost ? d.Cost.ToString("N0") : "강화 정보 없음";
        _upgrade.interactable = slot.CanOpenUpgradeConfirmation;
        var icon = _view.EquipmentIconForPresentation(d.Type); // 아이콘 값입니다.
        _detailIcon.sprite = icon;
        _detailIcon.enabled = icon != null;
        _iconPlaceholder.gameObject.SetActive(icon == null);
        _iconPlaceholder.text = d.Name + "\n아이콘";
        var labels = new List<string>(); // 문구 목록 값입니다.
        var current = new List<string>(); // 현재 값입니다.
        var next = new List<string>(); // 다음 값입니다.
        var arrows = new List<string>(); // 화살표 값입니다.
        // i: 반복 값입니다.
        for (int i = 0; i < StatNames.Length; i++)
        {
            if (d.Current != null && i < d.Current.Length && !string.IsNullOrEmpty(d.Current[i]))
            {
                labels.Add(StatNames[i]);
                current.Add(d.Current[i]);
                next.Add(d.IsMaxLevel ? "최대" : d.Next != null && i < d.Next.Length ? d.Next[i] ?? "—" : "—");
                arrows.Add("››");
            }
        }

        _statLabels.text = string.Join("\n", labels);
        _current.text = string.Join("\n", current);
        _next.text = string.Join("\n", next);
        _arrows.text = "<line-height=32>" + string.Join("\n", arrows);
    }

    /// <summary>
    /// 현재 선택한 장비의 강화 확인창을 표시합니다. 실제 강화는 확인 버튼에서 요청합니다.
    /// </summary>
    void UpgradeSelected()
    {
        var slot = _cards.Select(c => c.slot).FirstOrDefault(s => s != null && s.gameObject.activeInHierarchy && s.EquipmentType == _selectedType);
        if (slot == null || !slot.CanOpenUpgradeConfirmation || _confirmationPopup == null) return;
        var expected = slot.PresentationData;
        _confirmationPopup.Show(_view.DisplayGold, _view.HasDisplayGold, expected.Cost, () => ConfirmUpgrade(slot, expected));
    }

    /// <summary>확인 시 재화와 장비 상태를 다시 검사하고 기존 슬롯 강화 요청으로 연결합니다.</summary>
    void ConfirmUpgrade(UpgradeEquipSlotView slot, UpgradeEquipSlotView.DisplayData expected)
    {
        if (slot == null || !slot.gameObject.activeInHierarchy) return;
        var current = slot.PresentationData;
        if (_selectedType != expected.Type || current.Level != expected.Level || current.Cost != expected.Cost)
        {
            RefreshPresentation();
            return;
        }
        if ((_view.HasDisplayGold && _view.DisplayGold < current.Cost) || !slot.CanRequestUpgrade)
        {
            _view.ShowUpgradeResult(false);
            return;
        }
        slot.RequestSelectedUpgrade();
        RefreshPresentation();
    }
    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 강화 성공 여부에 맞는 버튼 피드백 연출을 재생합니다.
    /// </summary>
    /// <param name="succeeded">성공 여부입니다.</param>
    public void PlayUpgradeFeedback(bool succeeded)
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        // DOTween 연출: 강화 성공은 상세 패널 펀치, 실패는 짧은 흔들림으로 표시합니다.
        _feedbackTween?.Kill();
        _detailGroup.transform.localScale = Vector3.one;
        _feedbackTween = succeeded ? _detailGroup.transform.DOPunchScale(Vector3.one * .025f, .25f, 3, .4f).SetUpdate(true) : _detailGroup.transform.DOShakeScale(.2f, .015f, 5).SetUpdate(true);
    }

    #endregion

}
