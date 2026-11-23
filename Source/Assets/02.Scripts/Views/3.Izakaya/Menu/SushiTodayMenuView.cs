using UnityEngine;
using TMPro;

/// <summary>
/// 오늘의 메뉴 다섯 칸과 메뉴 등록 안내를 표시할 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiTodayMenuView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 오늘의 메뉴 슬롯 다섯 개를 위에서 아래 순서로 연결합니다.
    [SerializeField] SushiTodayMenuSlotView[] _menuSlots;
    // 영업 전 메뉴 변경 가능 여부를 안내할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _registrationNoticeText;
    // 이 패널이 속한 화면의 뒤로가기 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _closeButton;
    // 이 버튼으로 닫을 화면의 최상위 GameObject를 연결합니다. 팝업은 반투명 배경까지 포함한 루트입니다.
    [SerializeField] GameObject _pageRoot;
    // 통합 책 UI의 목록·상세·팝업 참조를 연결합니다.

    // 책 UI의 요리 목록·상세 View를 연결합니다.
    [SerializeField] SushiRecipeListView _recipeListView;
    [SerializeField] SushiRecipeDetailView _recipeDetailView; // 요리 상세 뷰 표시를 연결할 뷰입니다.
    // 각 팝업 Panel의 수량·등록 메뉴 관리·강화·결과 View를 연결합니다.
    [SerializeField] SushiMenuQuantityView _quantityView;
    [SerializeField] SushiRegisteredMenuActionsView _actionsView; // 동작 목록 뷰 표시를 연결할 뷰입니다.
    [SerializeField] SushiRecipeUpgradeView _upgradeView; // 강화 뷰 표시를 연결할 뷰입니다.
    [SerializeField] SushiRecipeUpgradeResultView _upgradeResultView; // 강화 결과 뷰 표시를 연결할 뷰입니다.
    // 기본 메뉴 영역의 음료 아이콘과 이름을 같은 순서로 연결합니다.
    [SerializeField] UnityEngine.UI.Image[] _drinkIcons;
    [SerializeField] TMP_Text[] _drinkNames; // 음료 이름 목록입니다.

    #endregion

    #region 런타임 상태

    Presentation _presentation; // 표시 데이터 값입니다.
    RecipeData _selectedRecipe; // 현재 상세에 전달된 요리 정보입니다. 실제 요리 데이터는 변경하지 않습니다.
    bool _showBookUpgradeIngredients; // 상세 데이터가 갱신되어도 강화 탭에서만 재료를 표시합니다.
    int _upgradePreviewRecipeId = -1; // 열린 강화 확인창이 표시하는 요리입니다.
    int _selectedRecipeId = -1; // 선택 요리 식별자 값입니다.
    int _selectedSlotIndex = -1; // 선택 슬롯 인덱스 값입니다.

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 메뉴 파트가 현재 표시하는 요리 식별값입니다.
    /// </summary>
    /// <summary>수량 팝업의 등록 대상이 없으면 첫 빈 메뉴 슬롯을 찾습니다.</summary>
    public int FirstEmptySlot()
    {
        for (int i = 0; _menuSlots != null && i < _menuSlots.Length; i++)
            if (_presentation?.Menus == null || i >= _presentation.Menus.Length || _presentation.Menus[i] == null || !_presentation.Menus[i].Registered) return i;
        return -1;
    }

    /// <summary>강화 탭에서만 재료를 표시하며 오늘의 메뉴·레시피에서는 숨깁니다.</summary>
    public void RefreshBookIngredients(bool upgrade)
    {
        _showBookUpgradeIngredients = upgrade;
        if (_recipeDetailView != null) _recipeDetailView.DisplayIngredients(upgrade && _selectedRecipe != null ? _selectedRecipe.UpgradeIngredients : null);
    }
    public Presentation CurrentPresentation => _presentation;
    public int SelectedRecipeId => _selectedRecipeId;
    public RecipeData SelectedRecipe => _selectedRecipe;

    #endregion

    #region 표시 데이터 형식

    /// <summary>
    /// 실제 재료를 변경하지 않는 재료 한 줄의 표시 데이터입니다.
    /// </summary>
    [System.Serializable]
    public sealed class IngredientData
    {

        #region 멤버

        #region 런타임 상태

        public Sprite Icon; // 재료 아이콘입니다. UI 오브젝트 연결 항목이 아닙니다.
        public string Name; // 표시할 재료 이름입니다.
        public string AcquisitionSource; // 재료의 획득처 안내 문구입니다.
        public int Owned, Required; // 담당 로직이 계산한 보유량과 필요량입니다.

        #endregion

        #endregion

    }

    /// <summary>
    /// 오늘의 메뉴 한 칸에 표시할 데이터입니다.
    /// </summary>
    [System.Serializable]
    public sealed class MenuSlotData
    {

        #region 멤버

        #region 런타임 상태

        public int RecipeId = -1; // 실제 요리 식별값입니다.
        public Sprite Icon; // 요리 아이콘입니다.
        public string Name, Information; // 이름과 레벨·수량·판매가 표시 문구입니다.
        public bool Registered, Interactable = true; // 등록 여부와 담당 로직의 조작 허용 결과입니다.

        #endregion

        #endregion

    }

    /// <summary>
    /// 요리 목록·상세·수량·강화 창이 함께 사용하는 표시 데이터입니다.
    /// </summary>
    [System.Serializable]
    public sealed class RecipeData
    {

        #region 멤버

        #region 런타임 상태

        public int RecipeId, Level; // 실제 요리 식별값과 표시 레벨입니다.
        // 테스트 또는 외부 전달용 잔여 수량입니다. 미지정 시 기존 오늘의 메뉴 재고를 조회합니다.
        public int RemainingQuantity = -1; // 등록 선택량·최대 제작량과 구분합니다.
        public Sprite Icon; // 요리 아이콘입니다.
        public string SalePrice; // 동전 배지에 표시할 판매가입니다.
        public string SaleExperience, NextSalePrice, NextSaleExperience; // 강화 비교 표시값입니다.
        public string Name, LevelPrice, Description; // 요리 이름·레벨/가격·설명입니다.
        public bool CanSelect, CanRegister, CanUpgrade; // 각 버튼의 실행 가능 여부입니다.
        public IngredientData[] Ingredients; // 상세·수량 창의 필요 재료입니다.
        public IngredientData[] CatalogIngredients; // 레시피 조회용입니다. 모든 메뉴 예약분을 제외한 사용 가능 수량입니다.
        public int CurrentQuantity = 1, MaximumQuantity; // 담당 로직이 계산한 현재·최대 수량입니다.
        public bool CanConfirmQuantity; // 수량 확인 허용 여부입니다.
        public string UpgradePrompt, BeforeStats, AfterStats, UpgradeCost; // 강화 확인 문구와 전후 수치·비용입니다.
        public IngredientData[] UpgradeIngredients; // 요리 강화에 필요한 재료입니다.

        #endregion

        #endregion

    }

    /// <summary>
    /// 기본 음료 한 종류의 표시 데이터입니다.
    /// </summary>
    [System.Serializable]
    public sealed class DrinkData
    {

        #region 멤버

        #region 런타임 상태

        public Sprite Icon; // 기본 음료 아이콘입니다.
        public string Price; // 실제 기본 메뉴의 판매가입니다.
        public string Description; // 기본 음식·음료의 원본 설명입니다.
        public IngredientData[] Ingredients; // 레시피 조회에 표시할 재료입니다.
        public string Name; // 기본 음료 이름입니다.

        #endregion

        #endregion

    }

    /// <summary>
    /// 메뉴 파트 전체를 갱신할 표시 묶음입니다. 배열 순서는 화면 슬롯 순서입니다.
    /// </summary>
    [System.Serializable]
    public sealed class Presentation
    {

        #region 멤버

        #region 런타임 상태

        public MenuSlotData[] Menus; // 메뉴 목록입니다.
        public RecipeData[] Recipes; // 요리 목록입니다.
        public DrinkData[] Drinks; // 음료 목록입니다.
        public bool CanManage = true; // 영업 중에는 false를 전달합니다.
        public string RegistrationNotice, UnavailableNotice; // 담당 로직의 안내 문구입니다.
        public int SelectedRecipeId = -1; // 상세에 표시할 요리 식별값이며 -1이면 기존 선택을 유지합니다.

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        UIEscapeView.Register(this, _closeButton);
        InitializeButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
            _closeButton.onClick.AddListener(Close);
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 다른 화면이나 데이터를 변경하지 않고 이 화면만 비활성화합니다.
    /// </summary>
    public void Close()
    {
        if (_pageRoot != null)
        {
            _pageRoot.SetActive(false);
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 지정한 메뉴 슬롯의 표시만 갱신합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="information">정보 값입니다.</param>
    /// <param name="registered">등록 여부입니다.</param>
    /// <param name="interactable">상호작용 가능 여부입니다.</param>
    public void DisplaySlot(int index, int recipeId, Sprite icon, string name, string information, bool registered, bool interactable = true)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_menuSlots == null || index < 0 || index >= _menuSlots.Length || _menuSlots[index] == null)
        {
            return;
        }

        _menuSlots[index].Bind(recipeId, icon, name, information, registered, interactable);
    }

    /// <summary>
    /// 메뉴 파트의 목록·슬롯·선택 상세를 갱신합니다. 재료 할당은 계산하지 않습니다.
    /// </summary>
    /// <param name="data">데이터 값입니다.</param>
    public void Display(Presentation data)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (data == null)
        {
            return;
        }

        _presentation = data;
        // i: 반복 값입니다.
        for (int i = 0; _menuSlots != null && i < _menuSlots.Length; i++)
        {
            var item = data.Menus != null && i < data.Menus.Length ? data.Menus[i] : null; // 아이템 값입니다.
            DisplayMenuSlot(i, item != null ? item.RecipeId : -1, item?.Icon, item?.Name, item?.Information, item != null && item.Registered, data.CanManage && (item == null || item.Interactable));
        }

        SetRegistrationNotice(data.RegistrationNotice);


        if (_recipeListView != null)
        {
            _recipeListView.DisplayRecipes(data.Recipes, data.UnavailableNotice, data.CanManage);
        }

        // i: 반복 값입니다.
        for (int i = 0; _drinkIcons != null && i < _drinkIcons.Length; i++)
        {
            var drink = data.Drinks != null && i < data.Drinks.Length ? data.Drinks[i] : null; // 음료 값입니다.
            DisplayDrink(i, drink?.Icon, drink?.Name);
        }

        if (data.SelectedRecipeId >= 0)
        {
            _selectedRecipeId = data.SelectedRecipeId;
        }

        SelectRecipe(_selectedRecipeId);
        RefreshManagedSlot();
    }

    /// <summary>
    /// 통합 책 UI의 지정한 메뉴 슬롯을 갱신합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="information">정보 값입니다.</param>
    /// <param name="registered">등록 여부입니다.</param>
    /// <param name="interactable">상호작용 가능 여부입니다.</param>
    public void DisplayMenuSlot(int index, int recipeId, Sprite icon, string name, string information, bool registered, bool interactable = true)
    {
        DisplaySlot(index, recipeId, icon, name, information, registered, interactable);
    }

    /// <summary>
    /// 기본 음료 한 종류의 이름과 아이콘만 갱신합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    public void DisplayDrink(int index, Sprite icon, string name)
    {
        if (_drinkIcons != null && index >= 0 && index < _drinkIcons.Length)
        {
            IjakayaView.SetDisplayIcon(_drinkIcons[index], IjakayaView.ResolveMenuIcon(icon, name, this));
        }

        if (_drinkNames != null && index >= 0 && index < _drinkNames.Length && _drinkNames[index] != null)
        {
            _drinkNames[index].text = name ?? string.Empty;
        }
    }

    /// <summary>
    /// 메뉴 관리 요청의 대상을 보관하고 해당 안내·수량을 표시합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    public void SelectMenuSlot(int index, int recipeId)
    {
        _selectedSlotIndex = index;
        _selectedRecipeId = recipeId;
        RefreshManagedSlot();
        SelectRecipe(recipeId);
    }

    /// <summary>
    /// 선택한 요리의 상세·수량 표시를 갱신하며 실제 선택 요청은 루트에서 전달합니다.
    /// </summary>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    public void SelectRecipe(int recipeId)
    {
        _selectedRecipeId = recipeId;
        _selectedRecipe = null;
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_presentation?.Recipes == null)
        {
            return;
        }

        var recipe = System.Array.Find(_presentation.Recipes, r => r != null && r.RecipeId == recipeId); // 요리 값입니다.
        if (recipe != null)
        {
            DisplayRecipeDetail(recipe);
        }
        else
        {
            if (_recipeDetailView != null)
            {
                _recipeDetailView.ClearPresentation();
            }

            if (_quantityView != null)
            {
                _quantityView.Bind(null, string.Empty, 0, 0, false);
                _quantityView.DisplayIngredients(null);
            }

            if (_upgradeView != null)
            {
                _upgradeView.Bind(null, string.Empty, string.Empty, string.Empty, string.Empty, false);
                _upgradeView.DisplayIngredients(null);
            }
        }
    }

    /// <summary>
    /// 선택한 요리 한 개의 상세와 수량을 전달받아 표시합니다.
    /// </summary>
    /// <param name="recipe">요리 값입니다.</param>
    public void DisplayRecipeDetail(RecipeData recipe)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (recipe == null)
        {
            return;
        }

        _selectedRecipeId = recipe.RecipeId;
        _selectedRecipe = recipe;
        bool manage = _presentation == null || _presentation.CanManage; // 관리 여부입니다.
        if (_recipeDetailView != null)
        {
            _recipeDetailView.Bind(recipe.Icon, recipe.Name, recipe.LevelPrice, recipe.Description, manage && recipe.CanRegister, manage && recipe.CanUpgrade);
            _recipeDetailView.DisplayIngredients(_showBookUpgradeIngredients ? recipe.UpgradeIngredients : null);
        }

        if (_quantityView != null)
        {
            _quantityView.Bind(recipe.Icon, recipe.Name, recipe.CurrentQuantity, recipe.MaximumQuantity, manage && recipe.CanConfirmQuantity);
            // 등록 수량에 맞춰 실제 제작 재료의 보유량과 필요량을 표시합니다.
            _quantityView.DisplayIngredients(recipe.Ingredients);
        }

        // 받은 최신 수량만 표시합니다. 실제 차감·강화 가능 여부는 여기서 계산하지 않습니다.
        if (_upgradeView != null && _upgradeView.isActiveAndEnabled && _upgradePreviewRecipeId == recipe.RecipeId)
            _upgradeView.DisplayIngredients(recipe.UpgradeIngredients);
    }

    /// <summary>
    /// 현재 선택 요리의 강화 전후 정보를 강화 파트에 전달합니다.
    /// </summary>
    public void DisplaySelectedUpgrade()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_upgradeView == null)
        {
            return;
        }

        var recipe = _selectedRecipe; // 요리 값입니다.
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (recipe == null)
        {
            return;
        }

        _upgradePreviewRecipeId = recipe.RecipeId;
        _upgradeView.Bind(recipe.Icon, recipe.UpgradePrompt, recipe.BeforeStats, recipe.AfterStats, recipe.UpgradeCost, (_presentation == null || _presentation.CanManage) && recipe.CanUpgrade);
        _upgradeView.DisplayIngredients(recipe.UpgradeIngredients);
    }

    /// <summary>
    /// 담당 로직이 확정한 요리 강화 결과만 결과 View에 표시합니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="message">메시지 값입니다.</param>
    /// <param name="stats">능력치 값입니다.</param>
    public void DisplayUpgradeResult(Sprite icon, string message, string stats)
    {
        if (_upgradeResultView != null)
        {
            _upgradeResultView.Bind(icon, message, stats);
        }
    }

    /// <summary>
    /// 선택된 등록 메뉴의 안내를 최신 표시 데이터로 갱신합니다.
    /// </summary>
    void RefreshManagedSlot()
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_presentation?.Menus == null || _selectedSlotIndex < 0 || _selectedSlotIndex >= _presentation.Menus.Length)
        {
            return;
        }

        var item = _presentation.Menus[_selectedSlotIndex]; // 아이템 값입니다.
        if (_actionsView != null)
        {
            _actionsView.Bind(item?.Icon, item?.Name, item?.Information);
        }
    }

    /// <summary>
    /// 등록 가능 시점 안내를 갱신합니다.
    /// </summary>
    /// <param name="message">메시지 값입니다.</param>
    void SetRegistrationNotice(string message)
    {
        if (_registrationNoticeText != null)
        {
            _registrationNoticeText.text = message ?? string.Empty;
        }
    }

    #endregion

}
