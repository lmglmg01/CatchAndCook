using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 상점의 교체 가능한 그래픽과 DOTween 연출만 담당합니다. 재고·일일 제한을 판정하지 않습니다.
/// </summary>
[DefaultExecutionOrder(1200)]
public class StoreScreenView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] StoreView _store; // 상점 표시를 연결할 뷰입니다.
    [SerializeField] PurchaseArt[] _purchaseCards; // 구매 카드 목록입니다.
    [SerializeField] UnityEngine.UI.Button[] _tabs; // 탭 목록입니다.
    [SerializeField] UnityEngine.UI.Image[] _tabIcons; // 탭 아이콘 목록입니다.
    [SerializeField] Sprite[] _selectedTabIcons; // 탭 순서에 맞춘 선택 상태의 반전 아이콘입니다. 없는 항목은 기존 색상 전환을 사용합니다.
    [SerializeField] Sprite[] _tabSprites = new Sprite[2]; // 탭 스프라이트 목록입니다.
    [SerializeField] Sprite _normalCardSprite, _selectedCardSprite; // 구매 상품 카드의 미선택/선택 배경 에셋을 연결합니다.
    [SerializeField] Sprite _normalTabSprite, _selectedTabSprite; // 구매/판매 탭의 미선택/선택 배경 에셋을 연결합니다.
    [SerializeField] Sprite _buyButtonSprite, _sellButtonSprite; // 구매/판매 실행 버튼의 배경 에셋을 연결합니다.
    [SerializeField] Transform _sellContent; // 판매 내용 값입니다.
    [SerializeField] CanvasGroup _hud; // HUD 값입니다.
    [SerializeField] CanvasGroup[] _boardGroups; // 선상 그룹 목록입니다.
    [SerializeField] CanvasGroup _screenGroup; // 화면 그룹 값입니다.
    // _ownedCountText: 보유 개수 텍스트입니다.
    // _selectedIconPlaceholder: 선택 아이콘 임시 표시 표시 텍스트입니다.
    // _limitText: 제한 텍스트입니다.
    [SerializeField] TMP_Text _ownedCountText, _selectedIconPlaceholder, _limitText;
    // _quantityControls: 수량 조작 값입니다.
    // _purchaseLimitPanel: 구매 제한 패널 값입니다.
    [SerializeField] GameObject _quantityControls, _purchaseLimitPanel;
    [SerializeField] UnityEngine.UI.Image _confirmBackground; // 확인 배경 이미지입니다.

    [Header("표시 데이터 연결 전에는 제한·등급을 숨깁니다")]
    [SerializeField] Material _soldOutItemMaterial; // 품절 아이템 그림에만 적용할 흑백 머티리얼입니다.
    [SerializeField] bool[] _soldOut = new bool[3]; // 판매 출력 목록입니다.
    [SerializeField] string[] _purchaseLimitLabels = new string[3]; // 구매 제한 문구 목록입니다.
    [SerializeField] GameObject[] _extraRarityBadges; // 추가 희귀도 표식 목록입니다.

    [Header("DOTween 연출")]
    [SerializeField, Min(0)] float _openDuration = .2f; // 열기 지속 시간 값입니다.
    [SerializeField, Min(0)] float _selectionDuration = .12f; // 선택 지속 시간 값입니다.

    #endregion

    #region 런타임 상태

    readonly Dictionary<CanvasGroup, Vector3> _hidden = new Dictionary<CanvasGroup, Vector3>(); // 숨김에 사용하는 대상별 매핑입니다.
    readonly Dictionary<UnityEngine.UI.Image, Tween> _tweens = new Dictionary<UnityEngine.UI.Image, Tween>(); // 트윈 목록에 사용하는 대상별 매핑입니다.
    readonly Dictionary<UnityEngine.UI.Image, Color> _targets = new Dictionary<UnityEngine.UI.Image, Color>(); // 대상 목록에 사용하는 대상별 매핑입니다.
    Tween _openTween; // 열기 트윈 값입니다.
    readonly Color _navy = new Color(.01f, .18f, .25f, .96f); // 남색 색상입니다.
    readonly Color _cream = new Color(1, .97f, .88f); // 크림색 색상입니다.

    #endregion

    #region 표시 데이터 형식

    [Serializable]
    public struct PurchaseArt
    {

        #region 멤버

        #region 런타임 상태

        public UnityEngine.UI.Button button; // 버튼입니다.
        // background: 배경 이미지입니다.
        // selectionBorder: 선택 테두리 이미지입니다.
        public UnityEngine.UI.Image background, selectionBorder;
        // title: 제목 표시 텍스트입니다.
        // iconPlaceholder: 아이콘 임시 표시 표시 텍스트입니다.
        public TMP_Text title, iconPlaceholder;
        public UnityEngine.UI.Image itemIcon;
        public Material normalItemMaterial;
        public GameObject soldOutOverlay; // 판매 출력 겹침 표시 값입니다.

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 뷰가 켜질 때 표시와 이벤트 연결을 갱신합니다.
    /// </summary>
    void OnEnable()
    {
        Hide(_hud);
        if (_boardGroups != null)
        {
            // group: 그룹 값입니다.
            foreach (var group in _boardGroups)
            {
                Hide(group);
            }
        }

        // 공통 등장 연출이 있으면 기존 화면 전체 페이드를 중복 재생하지 않습니다.
        if (_screenGroup != null && GetComponentInParent<CommonWindowEntranceView>() == null)
        {
            _openTween?.Kill();
            _screenGroup.alpha = 0;
            _openTween = _screenGroup.DOFade(1, _openDuration).SetUpdate(true);
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
        // DOTween 연출 정리: 이 View가 만든 Tween만 종료하고 원래 HUD 상태를 복원합니다.
        _openTween?.Kill();
        _openTween = null;
        // tween: 트윈 값입니다.
        foreach (var tween in _tweens.Values)
        {
            tween?.Kill();
        }

        _tweens.Clear();
        _targets.Clear();
        if (_screenGroup != null)
        {
            _screenGroup.alpha = 1;
        }

        // entry: 항목 값입니다.
        foreach (var entry in _hidden)
        {
            if (entry.Key != null)
            {
                entry.Key.alpha = entry.Value.x;
                entry.Key.interactable = entry.Value.y > .5f;
                entry.Key.blocksRaycasts = entry.Value.z > .5f;
            }
        }

        _hidden.Clear();
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 기존 상태를 보관하고 상점 뒤의 캔버스 그룹을 숨깁니다.
    /// </summary>
    /// <param name="group">그룹 값입니다.</param>
    void Hide(CanvasGroup group)
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

    // DOTween 연출: 선택 카드와 탭의 색상 전환. 반복 갱신은 Tween을 재시작하지 않습니다.

    /// <summary>
    /// 상점 이미지의 선택 색상을 트윈으로 전환합니다.
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

        if (_tweens.TryGetValue(image, out var tween))
        {
            tween?.Kill();
        }

        _targets[image] = target;
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying || !isActiveAndEnabled || _selectionDuration <= 0)
        {
            image.color = target;
            return;
        }

        _tweens[image] = image.DOColor(target, _selectionDuration).SetUpdate(true);
    }

    /// <summary>
    /// 구매 카드의 품절과 구매 제한 문구를 갱신합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="soldOut">판매 출력 여부입니다.</param>
    /// <param name="limitLabel">제한 문구 값입니다.</param>
    public void SetPurchasePresentation(int index, bool soldOut, string limitLabel)
    {
        // 항목 수나 인덱스 조건이 맞지 않으면 범위를 벗어난 처리를 막습니다.
        if (index < 0 || index >= _soldOut.Length)
        {
            return;
        }

        // 전달받은 상태만 표시합니다. 실제 재고 및 구매 횟수는 외부에서 관리합니다.
        _soldOut[index] = soldOut;
        if (index < _purchaseLimitLabels.Length)
        {
            _purchaseLimitLabels[index] = limitLabel;
        }

        RefreshPresentation();
    }

    /// <summary>
    /// 추가 판매 상품의 희귀도 표식을 켜거나 끕니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="visible">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetExtraRarityVisible(int index, bool visible)
    {
        if (_extraRarityBadges != null && index >= 0 && index < _extraRarityBadges.Length && _extraRarityBadges[index] != null)
        {
            _extraRarityBadges[index].SetActive(visible);
        }
    }

    /// <summary>
    /// 상점의 구매·판매 탭과 선택 카드 표시를 갱신합니다.
    /// </summary>
    [SerializeField] TMP_Text _totalPriceLabel; // 구상도에 없는 총액 보조 문구입니다.
    [SerializeField] TMP_Text _detailTitle, _detailDescription, _confirmLabel; // 구매·판매별 배치를 조정할 상세 텍스트입니다.
    /// <summary>
    /// 판매 구상도의 글씨와 여백을 적용하고 구매 탭으로 돌아오면 승인된 구매 배치를 복원합니다.
    /// </summary>
    void RefreshDetailLayout()
    {
        bool purchase = _store.IsPurchasePage;
        if (_detailTitle != null)
        {
            _detailTitle.fontSize = 40;
            _detailTitle.fontStyle = purchase ? FontStyles.Bold : FontStyles.Normal;
            _detailTitle.rectTransform.anchoredPosition = new Vector2(purchase ? 119 : 122, 168);
        }
        if (_detailDescription != null)
        {
            _detailDescription.fontSize = 24;
            _detailDescription.rectTransform.anchoredPosition = new Vector2(0, -46);
        }
        if (_ownedCountText != null)
            _ownedCountText.rectTransform.anchoredPosition = purchase ? new Vector2(-10, 6) : new Vector2(-10, 6);
        if (_confirmBackground != null)
            _confirmBackground.rectTransform.anchoredPosition = purchase ? new Vector2(164, -185) : new Vector2(164, -185);
        if (_confirmLabel != null)
        {
            _confirmLabel.fontSize = 36;
            _confirmLabel.rectTransform.anchoredPosition = new Vector2(0, 5);
        }
    }
    public void RefreshPresentation()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_store == null)
        {
            return;
        }

        if (_totalPriceLabel != null) _totalPriceLabel.gameObject.SetActive(false);
        RefreshDetailLayout();
        int selectedIndex = -1; // 선택 인덱스 값입니다.
        if (_purchaseCards != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _purchaseCards.Length; i++)
            {
                var art = _purchaseCards[i]; // 디자인 값입니다.
                var item = i < _store.PurchaseItems.Count ? _store.PurchaseItems[i] : null; // 아이템 값입니다.
                bool selected = item != null && item == _store.SelectedItem; // 선택 여부입니다.
                if (selected)
                {
                    selectedIndex = i;
                }

                if (art.button != null)
                {
                    art.button.gameObject.SetActive(true);
                    art.button.interactable = item != null;
                }

                if (art.title != null)
                {
                    art.title.text = item != null ? item.ItemName : "상품 미등록";
                    art.title.color = selected ? _navy : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
                }

                if (art.iconPlaceholder != null)
                {
                    art.iconPlaceholder.gameObject.SetActive(item == null || item.ItemIcon == null);
                }

                var cardSprite = selected ? _selectedCardSprite : _normalCardSprite; // 카드 스프라이트 값입니다.
                if (art.background != null && cardSprite != null)
                {
                    art.background.sprite = cardSprite;
                }

                Tint(art.background, cardSprite != null ? Color.white : selected ? _cream : _navy);
                if (art.selectionBorder != null)
                {
                    art.selectionBorder.gameObject.SetActive(selected);
                }

                bool soldOut = item != null && i < _soldOut.Length && _soldOut[i];
                if (art.itemIcon != null)
                    art.itemIcon.material = soldOut && _soldOutItemMaterial != null ? _soldOutItemMaterial : art.normalItemMaterial;

                if (art.soldOutOverlay != null)
                {
                    art.soldOutOverlay.SetActive(soldOut);
                }
            }
        }

        if (_tabs != null)
        {
            // i: 반복 값입니다.
            for (int i = 0; i < _tabs.Length; i++)
            {
                var button = _tabs[i]; // 버튼 값입니다.
                // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
                if (button == null)
                {
                    continue;
                }

                bool selected = _store.IsPurchasePage ? (i == 0) : (i == 1); // 선택 여부입니다.
                var tabImage = button.targetGraphic as UnityEngine.UI.Image; // 탭 이미지 값입니다.
                var tabSprite = selected ? _selectedTabSprite : _normalTabSprite; // 탭 스프라이트 값입니다.
                if (tabImage != null && tabSprite != null)
                {
                    tabImage.sprite = tabSprite;
                }

                Tint(tabImage, tabSprite != null ? Color.white : selected ? _cream : _navy);
                // text: 텍스트 값입니다.
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.color = selected ? _navy : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f);
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

        if (_ownedCountText != null)
        {
            _ownedCountText.gameObject.SetActive(!_store.IsPurchasePage && _store.SelectedItem != null);
            _ownedCountText.text = "×" + _store.SelectedOwnedCount;
        }

        if (_selectedIconPlaceholder != null)
        {
            _selectedIconPlaceholder.gameObject.SetActive(_store.SelectedItem != null && !_store.IsIngredientPurchase && _store.SelectedItem.ItemIcon == null);
            _selectedIconPlaceholder.text = _store.IsPurchasePage ? "상품 아이콘" : "아이템 아이콘";
        }

        if (_quantityControls != null)
        {
            _quantityControls.SetActive(!_store.IsPurchasePage || _store.IsIngredientPurchase);
        }

        bool showLimit = _store.IsPurchasePage && !_store.IsIngredientPurchase && _store.SelectedItem != null; // 표시 제한 여부입니다.
        if (_purchaseLimitPanel != null)
        {
            _purchaseLimitPanel.SetActive(showLimit);
        }

        if (_limitText != null)
        {
            string label = selectedIndex >= 0 && selectedIndex < _purchaseLimitLabels.Length ? _purchaseLimitLabels[selectedIndex] : string.Empty; // 문구 값입니다.
            _limitText.text = string.IsNullOrEmpty(label) ? "하루 1개 한정" : label;
            _limitText.gameObject.SetActive(showLimit);
        }

        if (_confirmBackground != null)
        {
            var buttonSprite = (_store.IsPurchasePage || _store.IsIngredientPurchase) ? _buyButtonSprite : _sellButtonSprite; // 버튼 스프라이트 값입니다.
            if (buttonSprite != null)
            {
                _confirmBackground.sprite = buttonSprite;
            }

            Tint(_confirmBackground, buttonSprite != null ? Color.white : _store.IsPurchasePage ? new Color(1, .46f, .18f) : new Color(.27f, .43f, 1));
        }

        if (_sellContent != null)
        {
            // slot: 슬롯 값입니다.
            foreach (var slot in _sellContent.GetComponentsInChildren<SellItemSlotView>(true))
            {
                var image = slot.GetComponent<UnityEngine.UI.Image>(); // 이미지 값입니다.
                Tint(image, _normalCardSprite != null ? Color.white : (slot.Item != null && slot.Item == _store.SelectedItem ? _cream : _navy));
                var placeholder = slot.transform.Find("ItemPlaceholder"); // 임시 표시 값입니다.
                if (placeholder != null)
                {
                    placeholder.gameObject.SetActive(slot.Item != null && slot.Item.ItemIcon == null);
                    var label = placeholder.GetComponent<TMP_Text>(); // 문구 값입니다.
                    if (label != null && slot.Item != null)
                    {
                        label.text = slot.Item.ItemName;
                    }
                }

                var amount = slot.transform.Find("AmountBackground/AmountText"); // 수량 값입니다.
                if (amount != null)
                {
                    amount.GetComponent<TMP_Text>().color = _normalCardSprite != null ? new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f) : (slot.Item != null && slot.Item == _store.SelectedItem ? _navy : new Color(253f / 255f, 247f / 255f, 228f / 255f, 1f));
                }
            }
        }
    }

    #endregion

}
