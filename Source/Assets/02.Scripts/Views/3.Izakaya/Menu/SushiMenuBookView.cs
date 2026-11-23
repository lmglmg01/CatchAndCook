using UnityEngine;
using TMPro;
using System.Linq;

/// <summary>메뉴 책의 오늘 메뉴·요리 강화·레시피 조회 페이지 배치와 전환을 담당합니다.</summary>
public class SushiMenuBookView : MonoBehaviour
{
    [SerializeField] GameObject _staffPage; // 기존 직원관리 화면을 책 안에서 재사용합니다.
    [SerializeField] UnityEngine.UI.Button _staffTab;
    [SerializeField] RectTransform _basicMenuSummary; // 레시피 탭의 기본 요리·음료 표시입니다.
    [SerializeField] RectTransform _today, _list, _detail;
    [SerializeField] GameObject[] _ingredients;
    [SerializeField] UnityEngine.UI.Button _todayTab, _upgradeTab, _recipesTab, _close, _register, _upgrade;
    [SerializeField] RectTransform _foodIcon, _name, _levelPrice;
    [SerializeField] TMP_Text _pageTitle;
    [SerializeField] TMP_Text _quantityLevelPrice;
    [SerializeField] GameObject _basicDetail;
    [SerializeField] UnityEngine.UI.Button[] _basicCards;
    [SerializeField] TMP_Text[] _basicPrices, _recipeCardNames;
    [SerializeField] UnityEngine.UI.Image[] _recipeCardIcons;
    [SerializeField] UnityEngine.UI.ScrollRect _recipeCardScroll;

    [SerializeField] Sprite _lemonadeIcon, _grapefruitIcon, _grapeIcon;

    sealed class RecipeCard
    {
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image Icon;
        public TMP_Text Name, Price, Kind;
    }

    readonly struct RecipeEntry
    {
        public readonly Sprite Icon;
        public readonly string Name, Price, Kind, Description;

        public RecipeEntry(Sprite icon, string name, string price, string kind, string description)
        {
            Icon = icon;
            Name = name;
            Price = price;
            Kind = kind;
            Description = description;
        }
    }
    [SerializeField] UnityEngine.UI.Image _basicPreview;
    [SerializeField] TMP_Text _basicName, _basicPrice, _basicDescription;
    [SerializeField] UnityEngine.UI.Button _addMenu;
    [SerializeField] GameObject _upgradeIngredientBackgrounds;
    [SerializeField] GameObject _upgradeFoodBackdrop, _upgradeDescriptionBackdrop;
    [SerializeField] GameObject _todayDetailBackdrop, _todayFoodBackdrop;
    [SerializeField] RectTransform _recipePriceCoin;
    [SerializeField] TMP_Text _recipePrice, _quantityPrice;
    [SerializeField] UnityEngine.UI.Image _emptyRecipeCell;
    readonly System.Collections.Generic.List<UnityEngine.UI.Image> _emptyCells = new();
    readonly System.Collections.Generic.List<RecipeCard> _recipeCards = new();
    readonly System.Collections.Generic.List<RecipeEntry> _recipeEntries = new();
    SushiTodayMenuView.DrinkData _basicFood;
    SushiTodayMenuView.DrinkData[] _drinks;
    int _selectedRecipeCard;
    int _mode;
    public int CurrentMode => _mode;
    public string BasicFoodPrice => _basicFood?.Price;

    /// <summary>각 탭의 전환 동작을 한 번만 등록합니다.</summary>
    void Awake()
    {
        UIEscapeView.Register(this, _close);
        _todayTab.onClick.AddListener(RequestToday);
        _upgradeTab.onClick.AddListener(RequestUpgrade);
        _recipesTab.onClick.AddListener(RequestRecipes);
        _close.onClick.AddListener(Close);
        if (_staffTab != null) _staffTab.onClick.AddListener(RequestStaff);
        if (_addMenu != null) _addMenu.onClick.AddListener(SelectFirstEmptySlot);
        for (int i = 0; i < _basicCards.Length; i++)
            BindRecipeCard(_basicCards[i], _recipeCardIcons[i], _recipeCardNames[i], _basicPrices[i]);
    }

    /// <summary>오늘의 메뉴는 왼쪽, 요리 목록과 등록 정보는 오른쪽에 표시합니다.</summary>
    /// <summary>직원관리 책갈피는 기존 소유 View의 데이터 갱신 경로를 사용합니다.</summary>
    void RequestToday()=>RequestBookmark(0);
    void RequestUpgrade()=>RequestBookmark(1);
    void RequestRecipes()=>RequestBookmark(2);
    void RequestStaff()=>RequestBookmark(3);
    void RequestBookmark(int mode)
    {
        // 페이지는 클릭 즉시 바꾸고, 책갈피 연출은 각 페이지의 스타일 갱신에서 별도로 재생합니다.
        if(mode==3)OpenStaff();else if(mode==2)ShowRecipes();else Show(mode);
    }
    void OpenStaff() => Owner?.OpenStaff();
    /// <summary>기존 직원관리 오브젝트를 책의 네 번째 페이지로 표시합니다.</summary>
    public void ShowStaff() => Show(3);

    public void ShowToday() { Show(0); }
    /// <summary>요리 목록과 강화 재료·강화 요청 버튼을 표시합니다.</summary>
    public void ShowUpgrade() { Show(1); }
    /// <summary>요리 목록과 큰 요리 이미지를 조회 전용으로 표시합니다.</summary>
    public void ShowRecipes()
    {
        _selectedRecipeCard = 0;
        Show(2);
        ResetRecipeCardScroll();
    }

    /// <summary>한 페이지 안에서 패널을 재배치하며 기존 선택 데이터와 수량 규칙을 보존합니다.</summary>
    /// <summary>어두운 배경이 책의 클릭을 가로채지 않도록 책 바로 뒤에 둡니다.</summary>
    /// <summary>독립된 책 배경은 책이 실제로 열려 있는 동안만 표시합니다.</summary>
    public void SyncBackdropVisibility()
    {
        var backdrop=transform.parent!=null?transform.parent.Find("MenuBookBackdrop"):null;
        if(backdrop!=null && backdrop.gameObject.activeSelf!=gameObject.activeInHierarchy)
            backdrop.gameObject.SetActive(gameObject.activeInHierarchy);
    }
    void OnEnable(){EnsureBackdropBehindBook();SyncBackdropVisibility();}
    void EnsureBackdropBehindBook()
    {
        var backdrop = transform.parent != null ? transform.parent.Find("MenuBookBackdrop") : null;
        if (backdrop != null && backdrop.GetSiblingIndex() > transform.GetSiblingIndex())
            backdrop.SetSiblingIndex(transform.GetSiblingIndex());
    }
    void Show(int mode)
    {
        EnsureBackdropBehindBook();
        _mode = mode;
        if (_staffPage != null) _staffPage.SetActive(mode == 3);
        if (mode == 3)
        {
            gameObject.SetActive(true);
            _close.gameObject.SetActive(true);
            GetComponent<UIVisibilityEffectView>()?.Show();
            GetComponentInParent<SushiRestaurantStyleView>()?.ApplyBook(3);
            foreach (var name in new[] { "TodayMenu", "RecipeCatalog", "SelectedRecipeDetail", "BasicMenuSummary", "BasicRecipeDetail", "RecipeCategory", "RecipeCategoryDivider", "AddMenuButton" })
            { var child = transform.Find(name); if (child != null) child.gameObject.SetActive(false); }
            _today.gameObject.SetActive(false); _list.gameObject.SetActive(false); _detail.gameObject.SetActive(false);
            if (_basicDetail != null) _basicDetail.SetActive(false);
            if (_basicMenuSummary != null) _basicMenuSummary.gameObject.SetActive(false);
            if (_addMenu != null) _addMenu.gameObject.SetActive(false);
            _pageTitle.text = "직원";
            GetComponentInParent<SushiRestaurantStyleView>()?.ApplyStaff();
            return;
        }
        bool showRecipes = mode == 2;
        foreach (var slot in _list.GetComponentsInChildren<SushiRecipeSlotView>(true))
        {
            slot.SetRecipeBrowsing(mode == 2);
            slot.SetSelected(Owner != null ? Owner.SelectedRecipeId : -1);
        }
        _list.gameObject.SetActive(!showRecipes);
        _detail.gameObject.SetActive(!showRecipes);
        if (_basicDetail != null) _basicDetail.SetActive(showRecipes);
        if (_upgradeIngredientBackgrounds != null) _upgradeIngredientBackgrounds.SetActive(mode == 1);
        if (_upgradeFoodBackdrop != null) _upgradeFoodBackdrop.SetActive(mode == 1);
        if (_upgradeDescriptionBackdrop != null) _upgradeDescriptionBackdrop.SetActive(mode == 1);
        if (_basicMenuSummary != null) _basicMenuSummary.gameObject.SetActive(showRecipes);
        gameObject.SetActive(true);
            _close.gameObject.SetActive(true);
            GetComponent<UIVisibilityEffectView>()?.Show();
        _today.gameObject.SetActive(mode == 0);
        if (_todayDetailBackdrop != null)
        {
            _todayDetailBackdrop.SetActive(mode == 0);
            _detail.GetComponent<UnityEngine.UI.Image>().enabled = mode != 0;
        }
        if (_todayFoodBackdrop != null) _todayFoodBackdrop.SetActive(mode == 0);
        Place(_list, new Vector2(mode == 0 ? 350 : -350, mode == 0 ? 150 : 0), new Vector2(600, mode == 0 ? 410 : 660));
        Place(_detail, new Vector2(350, mode == 0 ? -225 : 0), new Vector2(600, mode == 0 ? 250 : 660));
        var listTitle = _list.Find("Title") as RectTransform;
        if (listTitle != null) listTitle.anchoredPosition = new Vector2(0, mode == 0 ? 176 : 300);
        var scroll = _list.Find("RecipeScroll") as RectTransform;
        if (scroll != null)
        {
            Place(scroll, new Vector2(0,mode==2?90:-10),new Vector2(580,mode==0?330:mode==2?360:570));
            bool hasScrollbar = scroll.GetComponent<UnityEngine.UI.ScrollRect>().verticalScrollbar != null;
            Place(scroll.Find("Viewport") as RectTransform, hasScrollbar ? new Vector2(-8, 0) : Vector2.zero,
                hasScrollbar ? scroll.sizeDelta - new Vector2(16, 0) : scroll.sizeDelta);
            RefreshEmptyCells(scroll);
        }
        var grid = _list.GetComponentInChildren<UnityEngine.UI.GridLayoutGroup>(true);
        if (grid != null) grid.constraintCount = 5;
        Place(_foodIcon, mode == 0 ? new Vector2(-186,39) : mode == 1 ? new Vector2(-165,180) : new Vector2(0,90), mode == 0 ? new Vector2(143,103) : mode == 1 ? new Vector2(180,150) : new Vector2(320,270));
        Place(_name, mode == 0 ? new Vector2(-186,-40) : mode == 1 ? new Vector2(100,215) : new Vector2(0,270), mode == 0 ? new Vector2(199,35) : new Vector2(mode == 2 ? 530 : 320,64));
        Place(_levelPrice, mode == 0 ? new Vector2(-227,-82) : mode == 1 ? new Vector2(100,151) : new Vector2(0,-95),mode == 0 ? new Vector2(116,36) : new Vector2(mode == 2 ? 530 : 320,60));
        var description = _detail.Find("Description") as RectTransform;
        if (description != null)
            Place(description, mode == 0 ? new Vector2(97, 43) : mode == 1 ? new Vector2(0, 56) : new Vector2(0, -173),
                mode == 0 ? new Vector2(320, 96) : new Vector2(mode == 1 ? 464 : 480, 50));
        if (description != null && description.TryGetComponent<TMP_Text>(out var descriptionLabel))
            RefreshDescription(descriptionLabel, mode);
        var nameLabel = _name.GetComponent<TMP_Text>();
        nameLabel.fontSizeMin = mode == 0 ? 14 : 16;
        nameLabel.fontSizeMax = mode == 0 ? 23 : 28;
        nameLabel.enableWordWrapping = mode != 0;
        nameLabel.overflowMode = mode == 0 ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        if (description != null && description.TryGetComponent<TMP_Text>(out var detailText))
            detailText.alignment = mode == 0 ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        if (mode == 0) Place((RectTransform)_register.transform, new Vector2(97, -60), new Vector2(214, 58));
        RefreshPriceBadge();
        if (_addMenu != null)
        {
            // 고정 메뉴를 포함한 여섯 카드와 겹치는 별도 추가 카드는 숨깁니다. 각 빈 칸의 등록 버튼은 유지합니다.
            _addMenu.gameObject.SetActive(mode == 0 && (_today == null || _today.GetComponentsInChildren<SushiTodayMenuSlotView>(true).Length < 6));
            _addMenu.interactable = FindAddableSlot() >= 0;
        }

        _register.gameObject.SetActive(mode == 0);
        _upgrade.gameObject.SetActive(mode == 1);
        var owner = GetComponentInParent<IjakayaView>();
        var menuView = owner != null ? owner.MenuView : null;
        if (menuView != null) menuView.RefreshBookIngredients(mode == 1);
        var detailView = _detail.GetComponent<SushiRecipeDetailView>();
        for (int i=0; i<_ingredients.Length; i++)
            if (_ingredients[i]!=null) _ingredients[i].SetActive(mode==1 && detailView != null && i<detailView.IngredientCount);
        _pageTitle.text = mode == 0 ? "오늘의 메뉴" : mode == 1 ? "요리강화" : "기본요리 & 음료";
        if (showRecipes)
        {
            RefreshRecipeCards();
        }
        _todayTab.GetComponent<UnityEngine.UI.Image>().color = mode==0 ? new Color(.25f,.43f,1) : new Color(.35f,.48f,.76f);
        _upgradeTab.GetComponent<UnityEngine.UI.Image>().color = mode==1 ? new Color(1,.29f,.34f) : new Color(.75f,.42f,.43f);
        _recipesTab.GetComponent<UnityEngine.UI.Image>().color = mode==2 ? new Color(1,.74f,0) : new Color(.78f,.65f,.35f);
        GetComponentInParent<SushiRestaurantStyleView>()?.ApplyBook(mode);
    }

    /// <summary>요리 선택 데이터 갱신 후 현재 탭에 필요한 항목만 표시합니다.</summary>
    public void RefreshMode() { if(gameObject.activeInHierarchy) Show(_mode); }

    /// <summary>등록 팝업에 선택 요리의 레벨과 판매가를 표시합니다.</summary>
    public void RefreshQuantityInfo()
    {
        if (_quantityLevelPrice != null) _quantityLevelPrice.text = _levelPrice.GetComponent<TMP_Text>().text;
        if (_quantityPrice != null) _quantityPrice.text = _recipePrice != null ? _recipePrice.text : string.Empty;
    }

    /// <summary>기본 요리와 음료의 실제 표시 데이터를 각각 보관합니다.</summary>
    public void DisplayBasicEntries(SushiTodayMenuView.DrinkData food, SushiTodayMenuView.DrinkData[] drinks)
    {
        _basicFood = food;
        _drinks = drinks;
        if (_mode == 2 && isActiveAndEnabled) RefreshRecipeCards();
    }

    /// <summary>기본 요리·음료와 일반 요리를 하나의 스크롤 목록에 표시합니다.</summary>
    void RefreshRecipeCards()
    {
        _recipeEntries.Clear();
        if (_basicFood != null)
            _recipeEntries.Add(new RecipeEntry(_basicFood.Icon, _basicFood.Name, _basicFood.Price,
                "기본 요리", BasicDescription(_basicFood, "기본 음식 설명 미등록")));
        if (_drinks != null)
            foreach (var drink in _drinks.OrderBy(DrinkDisplayOrder))
                if (drink != null)
                    _recipeEntries.Add(new RecipeEntry(ResolveDrinkIcon(drink), drink.Name, drink.Price,
                        "음료", BasicDescription(drink, "음료 설명 미등록")));
        var recipes = MainMenu?.CurrentPresentation?.Recipes;
        if (recipes != null)
            foreach (var recipe in recipes)
                if (recipe != null)
                    _recipeEntries.Add(new RecipeEntry(recipe.Icon, recipe.Name, recipe.SalePrice,
                        $"Lv.{recipe.Level}", recipe.Description));

        while (_recipeCards.Count < _recipeEntries.Count)
        {
            var template = _recipeCards[0];
            var card = Instantiate(template.Button, _recipeCardScroll.content);
            card.name = $"RecipeCard{_recipeCards.Count + 1}";
            BindRecipeCard(card, card.transform.Find(template.Icon.name).GetComponent<UnityEngine.UI.Image>(),
                card.transform.Find(template.Name.name).GetComponent<TMP_Text>(),
                card.transform.Find(template.Price.name).GetComponent<TMP_Text>());
        }
        for (int i = 0; i < _recipeCards.Count; i++)
        {
            var card = _recipeCards[i];
            card.Button.gameObject.SetActive(i < _recipeEntries.Count);
            if (i >= _recipeEntries.Count) continue;
            var entry = _recipeEntries[i];
            IjakayaView.SetDisplayIcon(card.Icon, entry.Icon);
            card.Name.text = entry.Name;
            card.Price.text = entry.Price;
            card.Kind.text = entry.Kind;
            card.Button.interactable = true;
        }
        SelectRecipeCard(Mathf.Clamp(_selectedRecipeCard, 0, Mathf.Max(0, _recipeEntries.Count - 1)));
        GetComponentInParent<SushiRestaurantStyleView>()?.ApplyRecipeCards();
        UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(_recipeCardScroll.content);
    }

    // 구상도 순서: 차·맥주 다음에 레몬·자몽·청포도 에이드를 표시합니다.
    static int DrinkDisplayOrder(SushiTodayMenuView.DrinkData drink)
    {
        string name = drink?.Name ?? string.Empty;
        if (name.Contains("레몬")) return 2;
        if (name.Contains("자몽")) return 3;
        if (name.Contains("청포도")) return 4;
        if (name.Contains("맥주")) return 1;
        return 0;
    }

    /// <summary>원본 설명이 없는 항목에만 안내 문구를 표시합니다.</summary>
    static string BasicDescription(SushiTodayMenuView.DrinkData item, string fallback) =>
        string.IsNullOrWhiteSpace(item?.Description) ? fallback : item.Description;

    /// <summary>데이터의 아이콘이 비어 있는 음료만 기존 이미지로 보완합니다.</summary>
    Sprite ResolveDrinkIcon(SushiTodayMenuView.DrinkData drink)
    {
        return drink == null ? null : ResolveFallbackIcon(drink.Icon, drink.Name);
    }

    /// <summary>기존 참조를 유지하며 각 화면에서 공유할 에이드 대체 이미지를 반환합니다.</summary>
    public Sprite ResolveFallbackIcon(Sprite original, string menuName)
    {
        if (original != null) return original;
        if (string.IsNullOrEmpty(menuName) || !menuName.Contains("에이드")) return null;
        if (menuName.Contains("레몬")) return _lemonadeIcon;
        if (menuName.Contains("자몽")) return _grapefruitIcon;
        if (menuName.Contains("청포도")) return _grapeIcon;
        return null;
    }

    void BindRecipeCard(UnityEngine.UI.Button button, UnityEngine.UI.Image icon, TMP_Text name, TMP_Text price)
    {
        int index = _recipeCards.Count;
        button.onClick.AddListener(() => SelectRecipeCard(index));
        _recipeCards.Add(new RecipeCard
        {
            Button = button,
            Icon = icon,
            Name = name,
            Price = price,
            Kind = button.transform.Find("Kind").GetComponent<TMP_Text>()
        });
    }

    /// <summary>선택한 카드의 상세 정보와 강조 상태를 갱신합니다.</summary>
    void SelectRecipeCard(int index)
    {
        bool hasEntry = index >= 0 && index < _recipeEntries.Count;
        _basicDetail.SetActive(hasEntry);
        if (!hasEntry) return;

        _selectedRecipeCard = index;
        var entry = _recipeEntries[index];
        IjakayaView.SetDisplayIcon(_basicPreview, entry.Icon);
        GetComponentInParent<SushiRestaurantStyleView>()?.FitBasicRecipePreview();
        _basicName.text = entry.Name;
        _basicPrice.text = entry.Price;
        var words = (entry.Description ?? string.Empty).Split(' ');
        _basicDescription.text = string.Join(" ", System.Array.ConvertAll(words, word => "<nobr>" + word + "</nobr>"));
        for (int i = 0; i < _recipeEntries.Count; i++)
        {
            var button = _recipeCards[i].Button;
            button.targetGraphic.color = Color.white;
            var outline = button.GetComponent<UnityEngine.UI.Outline>() ?? button.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color32(253, 247, 228, 255);
            outline.effectDistance = new Vector2(4, 4);
            outline.enabled = i == index;
            GetComponentInParent<SushiRestaurantStyleView>()?.SetRecipeCardSelected(button, i == index);
        }
    }

    void ResetRecipeCardScroll()
    {
        _recipeCardScroll.StopMovement();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_recipeCardScroll.content);
        _recipeCardScroll.verticalNormalizedPosition = 1;
    }

    int FindAddableSlot()
    {
        var data = MainMenu != null ? MainMenu.CurrentPresentation : null;
        if (data?.Menus == null || !data.CanManage) return -1;
        return System.Array.FindIndex(data.Menus, row => row == null || (row.Interactable && !row.Registered));
    }

    void SelectFirstEmptySlot()
    {
        int slot = FindAddableSlot();
        if (slot < 0) return;
        Owner.SelectMenuSlot(slot, -1, false);
        ShowToday();
    }

    /// <summary>장비 강화와 같은 순서로 현재 수치, 화살표, 다음 수치를 배치합니다.</summary>
    void RefreshDescription(TMP_Text label, int mode)
    {
        var recipe = MainMenu != null ? MainMenu.SelectedRecipe : null;
        string text = recipe?.Description ?? string.Empty;
        if(mode==0)
            text=string.Join(" ",System.Array.ConvertAll(text.Split(' '),word=>"<nobr>"+word+"</nobr>"));
        if (GetComponentInParent<SushiRestaurantStyleView>() == null && mode == 1 && recipe != null && !string.IsNullOrEmpty(recipe.SaleExperience))
        {
            const string arrow = "<pos=246><color=#286A50>››</color><pos=326>";
            text = $"판매가<pos=152>{recipe.SalePrice}{arrow}<color=#286A50>{recipe.NextSalePrice}</color>\n" +
                $"경험치<pos=152>{recipe.SaleExperience}{arrow}<color=#286A50>{recipe.NextSaleExperience}</color>";
        }
        label.text = text;
        label.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    void RefreshPriceBadge()
    {
        if (_recipePriceCoin == null || _recipePrice == null) return;
        var recipe = MainMenu != null ? MainMenu.SelectedRecipe : null;
        bool hasPrice = recipe != null && !string.IsNullOrEmpty(recipe.SalePrice);
        _recipePriceCoin.gameObject.SetActive(hasPrice);
        _recipePrice.text = hasPrice ? recipe.SalePrice : string.Empty;
        if (hasPrice) _levelPrice.GetComponent<TMP_Text>().text = $"Lv.{recipe.Level}";
        _recipePriceCoin.anchoredPosition = _mode == 0 ? new Vector2(-178, -82) : _levelPrice.anchoredPosition + new Vector2(88 - _levelPrice.sizeDelta.x * .5f, 0);
    }

    void RefreshEmptyCells(RectTransform scroll)
    {
        if (_emptyRecipeCell == null) return;
        var content = scroll.GetComponent<UnityEngine.UI.ScrollRect>().content;
        int count = 0;
        foreach (var slot in content.GetComponentsInChildren<SushiRecipeSlotView>(true))
            if (slot.gameObject.activeSelf) count++;
        int padding = count == 0 ? 0 : (5 - count % 5) % 5;
        while (_emptyCells.Count < padding)
            _emptyCells.Add(Instantiate(_emptyRecipeCell, content));
        for (int i = 0; i < _emptyCells.Count; i++)
        {
            _emptyCells[i].transform.SetAsLastSibling();
            _emptyCells[i].gameObject.SetActive(i < padding);
        }
    }

    /// <summary>메뉴 전체 화면을 닫습니다.</summary>
    public void Close()
    {
        if (_close != null) _close.gameObject.SetActive(false);
        var effect = GetComponent<UIVisibilityEffectView>();
        if (effect != null && effect.HideAfter(() => gameObject.SetActive(false))) return;
        gameObject.SetActive(false);
    }

    /// <summary>페이지 중앙을 기준으로 위치와 크기를 지정합니다.</summary>
    static void Place(RectTransform r,Vector2 pos,Vector2 size)
    { r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size; }

    /// <summary>이 뷰가 등록한 이벤트만 해제합니다.</summary>
    void OnDestroy()
    {
        if (_addMenu != null) _addMenu.onClick.RemoveListener(SelectFirstEmptySlot);
        _todayTab.onClick.RemoveListener(RequestToday); _upgradeTab.onClick.RemoveListener(RequestUpgrade);
        if (_staffTab != null) _staffTab.onClick.RemoveListener(RequestStaff);
        _recipesTab.onClick.RemoveListener(RequestRecipes); _close.onClick.RemoveListener(Close);
    }
    bool _testing;
    SushiTodayMenuView.Presentation _testData, _savedData;
    int _savedSlot, _savedRecipe;
    public bool IsTesting => _testing;
    IjakayaView Owner => GetComponentInParent<IjakayaView>();
    SushiTodayMenuView MainMenu => Owner.MenuView;

    /// <summary>테스트 창을 닫을 때 원래 표시 데이터와 선택을 복원합니다.</summary>
    void OnDisable() { SyncBackdropVisibility(); EndTest(); }

    /// <summary>테스트 수량 확인은 표시용 메뉴만 갱신하며 실제 등록 이벤트는 전달하지 않습니다.</summary>
    public bool ConfirmTestQuantity(int quantity)
    {
        if (!_testing || _testData == null) return false;
        var recipe = System.Array.Find(_testData.Recipes,r=>r.RecipeId==Owner.SelectedRecipeId);
        int slot = Owner.SelectedMenuSlotIndex;
        if (recipe == null || slot<0 || slot>=_testData.Menus.Length) return true;
        quantity=Mathf.Clamp(quantity,1,recipe.MaximumQuantity);
        _testData.Menus[slot]=new SushiTodayMenuView.MenuSlotData{RecipeId=recipe.RecipeId,Icon=recipe.Icon,Name=recipe.Name,Registered=true,Information="테스트 · 등록 "+quantity+"개"};
        Owner.DisplayMenu(_testData);
        HideTestPopups(); RefreshMode();
        return true;
    }

    /// <summary>테스트 강화는 표시 레벨만 올리고 골드·재료·실제 요리 에셋은 변경하지 않습니다.</summary>
    public bool ConfirmTestUpgrade()
    {
        if (!_testing || _testData == null) return false;
        var recipe=System.Array.Find(_testData.Recipes,r=>r.RecipeId==Owner.SelectedRecipeId);
        if(recipe!=null){recipe.Level++;recipe.LevelPrice="테스트 레벨 "+recipe.Level+"    1,500";recipe.BeforeStats="레벨 "+recipe.Level;recipe.AfterStats="레벨 "+(recipe.Level+1);}
        Owner.DisplayMenu(_testData);HideTestPopups();RefreshMode();return true;
    }

    /// <summary>테스트 중 열린 메뉴 관련 팝업을 닫습니다.</summary>
    void HideTestPopups()
    {
        if (Owner == null) return; // 플레이 종료 시 부모가 먼저 파괴될 수 있습니다.
        foreach(var name in new[]{"MenuRegistrationPopup","RecipeUpgradeConfirmPopup","RecipeUpgradeResultPopup","RegisteredMenuActionsPopup"})
        {var page=Owner.transform.Find(name);if(page!=null)page.gameObject.SetActive(false);}
    }

    /// <summary>테스트 이전의 메뉴 데이터와 선택 항목을 복원합니다.</summary>
    [ContextMenu("메뉴 책 테스트/5. 테스트 종료 · 원래 데이터 복원")]
    public void EndTest()
    {
        if(!_testing)return;
        if (Owner == null) { _testing=false; _testData=null; _savedData=null; return; }
        HideTestPopups();
        Owner.DisplayMenu(_savedData??new SushiTodayMenuView.Presentation());
        Owner.SelectMenuSlot(_savedSlot,_savedRecipe,false);
        _testing=false;_testData=null;_savedData=null;
        if(gameObject.activeInHierarchy)RefreshMode();
    }

#if UNITY_EDITOR
    /// <summary>등록된 메뉴와 빈 슬롯이 섞인 오늘의 메뉴 예시를 엽니다.</summary>
    [ContextMenu("메뉴 책 테스트/1. 오늘의 메뉴 탭")]
    void TestToday(){BeginTest(0,false);}
    /// <summary>강화 재료와 강화 요청 팝업을 확인할 예시를 엽니다.</summary>
    [ContextMenu("메뉴 책 테스트/2. 요리 강화 탭")]
    void TestUpgrade(){BeginTest(1,false);}
    /// <summary>요리 선택과 큰 이미지 조회를 확인할 예시를 엽니다.</summary>
    [ContextMenu("메뉴 책 테스트/3. 레시피 탭")]
    void TestRecipes(){BeginTest(2,false);}
    /// <summary>최대20개 예시 수량으로 등록 팝업을 엽니다.</summary>
    [ContextMenu("메뉴 책 테스트/4. 메뉴 등록 수량 팝업")]
    void TestQuantity(){BeginTest(0,true);}

    /// <summary>플레이 모드에서만 실제 에셋의 이름·아이콘으로 임시 표시 데이터를 만듭니다.</summary>
    void BeginTest(int mode,bool quantityPopup)
    {
        if(!Application.isPlaying){Debug.Log("Play 모드에서 메뉴 책 테스트를 실행해주세요.",this);return;}
        if(!_testing){_savedData=MainMenu.CurrentPresentation;_savedSlot=Owner.SelectedMenuSlotIndex;_savedRecipe=Owner.SelectedRecipeId;}
        _testing=true;HideTestPopups();
        var drinks=new System.Collections.Generic.List<SushiTodayMenuView.DrinkData>();
        var recipes=new System.Collections.Generic.List<SushiTodayMenuView.RecipeData>();
        var guids=UnityEditor.AssetDatabase.FindAssets("t:Sushi");System.Array.Sort(guids,System.StringComparer.Ordinal);
        foreach(var guid in guids)
        {
            var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<Sushi>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if(asset==null||asset.Icon==null)continue;
            if(asset.Type==MenuType.Drink){if(drinks.Count<2)drinks.Add(new SushiTodayMenuView.DrinkData{Icon=asset.Icon,Name=asset.Name,Price=asset.Gold.ToString("N0")});continue;}
            var ingredients=new System.Collections.Generic.List<SushiTodayMenuView.IngredientData>();
            foreach(var item in asset.Ingredients)
            {if(item==null)continue;ingredients.Add(new SushiTodayMenuView.IngredientData{Icon=item.Item!=null?item.Item.ItemIcon:null,Name=item.Item!=null?item.Item.ItemName:"재료",Owned=20,Required=item.Amount});if(ingredients.Count==2)break;}
            recipes.Add(new SushiTodayMenuView.RecipeData{RecipeId=asset.Id,Name=asset.Name,Icon=asset.Icon,Level=1,SalePrice=asset.Gold.ToString("N0"),LevelPrice="테스트 레벨 1    1,500",CanSelect=true,CanRegister=true,CanUpgrade=true,CurrentQuantity=5,MaximumQuantity=20,CanConfirmQuantity=true,Ingredients=ingredients.ToArray(),UpgradeIngredients=ingredients.ToArray(),UpgradePrompt="테스트 강화 · 실제 재료와 골드는 변경되지 않습니다.",BeforeStats="레벨 1",AfterStats="레벨 2",UpgradeCost="100"});
            // 기본 음료도 찾을 수 있도록 에셋 검색은 끝까지 진행합니다.
        }
        if(recipes.Count==0){recipes.Add(new SushiTodayMenuView.RecipeData{RecipeId=0,Name="테스트 요리",LevelPrice="테스트 레벨 1",CanSelect=true,CanRegister=true,CanUpgrade=true,MaximumQuantity=20,CurrentQuantity=5,CanConfirmQuantity=true});}
        var first=recipes[0];var menus=new SushiTodayMenuView.MenuSlotData[5];menus[0]=new SushiTodayMenuView.MenuSlotData{RecipeId=first.RecipeId,Icon=first.Icon,Name=first.Name,Information="테스트 · 등록 5개",Registered=true};
        _testData=new SushiTodayMenuView.Presentation{Menus=menus,Recipes=recipes.ToArray(),Drinks=drinks.ToArray(),SelectedRecipeId=first.RecipeId,CanManage=true};
        _selectedRecipeCard = 0;
        Owner.DisplayMenu(_testData);Owner.SelectMenuSlot(1,first.RecipeId,false);Show(mode);
        if (mode == 2) ResetRecipeCardScroll();
        if(quantityPopup)Owner.OpenMenuQuantity();
        Debug.Log("메뉴 책 테스트: "+new[]{"오늘의 메뉴","요리 강화","레시피"}[mode]+". 실제 데이터는 변경하지 않습니다.",this);
    }
#endif
}



