using UnityEngine;
using TMPro;

/// <summary>
/// 등록 가능한 요리 목록의 개별 슬롯 뷰입니다.
/// 버튼은 Awake에서 연결하며 화면 표시만 전환합니다. 실제 게임 데이터 처리는 연결 전입니다.
/// </summary>
public class SushiRecipeSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 요리를 선택할 슬롯 자체의 버튼을 연결합니다.
    [SerializeField] UnityEngine.UI.Button _selectButton;
    // 선택·등록 가능 상태를 표현할 슬롯 배경 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _backgroundImage;
    // 요리 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _foodIconImage;
    // 요리의 현재 강화 레벨을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _levelText;
    // 요리 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _nameText;
    // 같은 Canvas_Sushi 루트의 IjakayaView를 연결합니다. Sushi 화면 표시 전환만 요청합니다.
    [SerializeField] IjakayaView _owner;

    #endregion

    #region 런타임 상태

    bool _canManageSelect; // 등록·강화 화면의 기존 선택 가능 조건입니다.
    [SerializeField] UnityEngine.UI.Outline _selectionOutline;
    int _recipeId = -1; // 요리 식별자 값입니다.
    public int RecipeId => _recipeId; // 이름 중복과 무관하게 표시 데이터를 찾습니다.

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 이 View가 처음 활성화될 때 버튼 이벤트를 연결합니다.
    /// </summary>
    void Awake()
    {
        // 단독 슬롯 프리팹은 부모 Canvas_Sushi에서 화면 전환용 View를 찾습니다.
        if (_owner == null)
        {
            _owner = GetComponentInParent<IjakayaView>();
        }

        InitializeButtons();
    }

    /// <summary>
    /// 이 View가 등록한 버튼 이벤트만 제거합니다.
    /// </summary>
    void OnDestroy()
    {
        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(SelectRecipe);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 다른 이벤트를 유지하면서 이 View의 버튼 콜백을 중복 없이 연결합니다.
    /// </summary>
    void InitializeButtons()
    {
        if (_selectButton != null)
        {
            _selectButton.onClick.RemoveListener(SelectRecipe);
            _selectButton.onClick.AddListener(SelectRecipe);
        }
    }

    /// <summary>
    /// 실제 요리 ID와 목록 표시·선택 가능 여부를 전달받습니다.
    /// </summary>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    /// <param name="canSelect">가능 선택 여부입니다.</param>
    public void Bind(int recipeId, Sprite icon, string name, int level, bool canSelect)
    {
        _recipeId = recipeId;
        IjakayaView.SetDisplayIcon(_foodIconImage, icon);
        if (_nameText != null)
        {
            _nameText.text = name ?? string.Empty;
        }

        if (_levelText != null)
        {
            _levelText.text = GetComponentInParent<SushiRestaurantStyleView>() != null ? level.ToString() : $"Lv.{level}";
        }

        if (_selectButton != null)
        {
            _canManageSelect = canSelect;
            var book = GetComponentInParent<SushiMenuBookView>();
            SetRecipeBrowsing(book != null && book.CurrentMode == 2);
        }
    }

    #endregion

    /// <summary>레시피 조회에서는 등록 가능 여부와 관계없이 알려진 요리를 선택할 수 있게 합니다.</summary>
    public void SetSelected(int recipeId)
    {
        bool selected = _recipeId >= 0 && _recipeId == recipeId;
        if (GetComponentInParent<SushiRestaurantStyleView>() != null)
        {
            if (_selectionOutline != null) _selectionOutline.enabled = false;
            UISelectionStrokeView.SetSelected(transform, selected, 30);
        }
        else if (_selectionOutline != null) _selectionOutline.enabled = selected;
    }

    public void SetRecipeBrowsing(bool browsing)
    {
        if (_selectButton != null) _selectButton.interactable = _recipeId >= 0 && (browsing || _canManageSelect);
    }
    #region UI 표시 및 갱신

    /// <summary>
    /// 선택한 요리 ID를 전달하여 오른쪽 상세 정보만 갱신합니다.
    /// </summary>
    void SelectRecipe()
    {
        if (_owner != null)
        {
            _owner.SelectRecipe(_recipeId);
        }
    }

    #endregion

}

