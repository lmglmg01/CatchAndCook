using UnityEngine;
using TMPro;

/// <summary>
/// 영업 정산에서 요리별 판매 실적 한 줄을 표시할 뷰입니다.
/// 현재는 인스펙터 연결용 변수만 선언하며 초기화·갱신·버튼 동작은 구현하지 않습니다.
/// </summary>
public class SushiMenuSalesSlotView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // 판매한 요리의 아이콘 이미지를 연결합니다.
    [SerializeField] UnityEngine.UI.Image _foodIconImage;
    // 판매한 요리 이름을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _nameText;
    // 판매한 요리 수량을 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _countText;
    // 해당 요리의 판매 금액 합계를 표시할 텍스트를 연결합니다.
    [SerializeField] TMP_Text _amountText;

    #endregion

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 등록 여부와 판매 수량·금액을 표시합니다. 미등록 칸은 배경만 남깁니다.
    /// </summary>
    /// <param name="icon">아이콘 이미지입니다.</param>
    /// <param name="name">이름 값입니다.</param>
    /// <param name="count">개수 값입니다.</param>
    /// <param name="amount">수량 값입니다.</param>
    /// <param name="registered">등록 여부입니다.</param>
    public void Bind(Sprite icon, string name, int count, float amount, bool registered)
    {
        if (_foodIconImage != null)
        {
            _foodIconImage.gameObject.SetActive(registered);
        }

        IjakayaView.SetDisplayIcon(_foodIconImage, icon);
        if (_nameText != null)
        {
            _nameText.text = registered ? name ?? string.Empty : string.Empty;
        }

        if (_countText != null)
        {
            _countText.text = registered ? count.ToString() + "개" : string.Empty;
        }

        if (_amountText != null)
        {
            _amountText.text = registered ? $"{amount:0}" : string.Empty;
        }
    }

    #endregion

}
