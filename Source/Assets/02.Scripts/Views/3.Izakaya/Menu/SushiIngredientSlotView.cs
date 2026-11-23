using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 요리에 필요한 재료의 한 줄 표시를 담당할 뷰입니다.
/// 현재는 인스펙터 연결용 변수만 선언하며 초기화·갱신·버튼 동작은 구현하지 않습니다.
/// </summary>
public class SushiIngredientSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 재료 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _iconImage;
    // 재료 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _nameText;
    // 재료의 보유 수량과 필요 수량을 함께 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _amountText;
    [SerializeField] GameObject _sourceTooltip;

    #endregion

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 재료의 이름·아이콘·수량과 획득처 툴팁을 표시합니다. 실제 수량은 변경하지 않습니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="owned">보유 값입니다.</param>
    /// <param name="required">필요 값입니다.</param>
    /// <param name="acquisitionSource">재료의 획득처 안내 문구입니다.</param>
    public void Bind(Sprite icon, string name, int owned, int required, string acquisitionSource = "")
    {
        // 재료 아이콘을 갱신합니다.
        IjakayaView.SetDisplayIcon(_iconImage, icon);
        // 재료 이름을 갱신합니다.
        if (_nameText != null)
        {
            _nameText.text = name ?? string.Empty;
        }

        // 화면 스타일에 맞게 보유량과 필요량을 표시합니다.
        if (_amountText != null)
        {
            _amountText.text = GetComponentInParent<SushiRestaurantStyleView>() != null
                ? $"<color={(owned >= required ? "#9FD83F" : "#FF5059")}>{owned}</color>/{required}" : $"{owned} / {required}";
        }

        // 툴팁이 연결되지 않은 슬롯은 텍스트 갱신을 생략합니다.
        if (_sourceTooltip == null)
        {
            return;
        }

        // 비활성 툴팁에서도 안내 텍스트를 찾습니다.
        TMP_Text tooltipText = _sourceTooltip.GetComponentInChildren<TMP_Text>(true);

        // 정보가 없을 때도 기본 문구로 덮어써 이전 재료의 안내가 남지 않게 합니다.
        if (tooltipText != null)
        {
            tooltipText.text = string.IsNullOrWhiteSpace(acquisitionSource)
                ? "획득처 정보 미등록"
                : acquisitionSource;
        }
    }

    #endregion

    void OnDisable() => HideTooltip();

    void HideTooltip()
    {
        if (_sourceTooltip != null) _sourceTooltip.SetActive(false);
    }
}
