using UnityEngine;
using TMPro;

/// <summary>
/// 등록할 수 있는 요리 목록을 표시할 뷰입니다.
/// 현재는 인스펙터 연결용 변수만 선언하며 초기화·갱신·버튼 동작은 구현하지 않습니다.
/// </summary>
public class SushiRecipeListView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 요리 목록의 슬롯을 표시 순서대로 연결합니다.
    [SerializeField] SushiRecipeSlotView[] _recipeSlots;
    // 재료 부족 또는 등록 불가 상태를 안내할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _unavailableNoticeText;

    #endregion

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 목록의 지정한 슬롯에 표시 데이터를 전달합니다. 재료 조건은 담당 로직에서 판정합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    /// <param name="recipeId">요리 식별자 값입니다.</param>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="level">레벨 값입니다.</param>
    /// <param name="canSelect">가능 선택 여부입니다.</param>
    public void DisplaySlot(int index, int recipeId, Sprite icon, string name, int level, bool canSelect)
    {
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_recipeSlots == null || index < 0 || index >= _recipeSlots.Length || _recipeSlots[index] == null)
        {
            return;
        }

        _recipeSlots[index].Bind(recipeId, icon, name, level, canSelect);
    }

    /// <summary>
    /// 요리 목록을 갱신하고 데이터가 없는 슬롯은 숨깁니다.
    /// </summary>
    /// <param name="recipes">요리 목록입니다.</param>
    /// <param name="notice">안내 값입니다.</param>
    /// <param name="canManage">가능 관리 여부입니다.</param>
    public void DisplayRecipes(SushiTodayMenuView.RecipeData[] recipes, string notice, bool canManage = true)
    {
        if (_unavailableNoticeText != null)
        {
            _unavailableNoticeText.text = notice ?? string.Empty;
        }

        // i: 반복 값입니다.
        for (int i = 0; _recipeSlots != null && i < _recipeSlots.Length; i++)
        {
            var slot = _recipeSlots[i]; // 슬롯 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (slot == null)
            {
                continue;
            }

            var data = recipes != null && i < recipes.Length ? recipes[i] : null; // 데이터 값입니다.
            slot.gameObject.SetActive(data != null);
            if (data != null)
            {
                slot.Bind(data.RecipeId, data.Icon, data.Name, data.Level, canManage && data.CanSelect);
            }
        }
    }

    #endregion

}
