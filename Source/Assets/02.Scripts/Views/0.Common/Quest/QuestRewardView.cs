using TMPro;
using UnityEngine;

/// <summary>
/// 퀘스트 보상 슬롯에 표시할 보상 종류입니다.
/// </summary>
public enum QuestRewardType
{
    Exp,
    Gold,
    Item
}

/// <summary>
/// 경험치, 골드 또는 아이템 보상 정보를 표시하며 실제 지급은 하지 않습니다.
/// </summary>
public class QuestRewardView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    // UI를 연결하지 않습니다. 이 슬롯의 보상 종류(경험치·골드·아이템)를 선택하며, Bind 호출 시 전달받은 종류로 갱신됩니다.
    [SerializeField] QuestRewardType _type;
    // RewardSlot의 RewardIcon Image를 연결합니다. 이미지가 없으면 어두운 동전으로 표시합니다.
    [SerializeField] UnityEngine.UI.Image _rewardImage;
    // RewardSlot의 RewardName TMP_Text를 연결합니다. 경험치·골드 또는 보상 아이템 이름을 표시합니다.
    [SerializeField] TMP_Text _rewardNameText;
    // RewardSlot의 RewardAmount TMP_Text를 연결합니다. 보상 수량을 +수량 형식으로 표시합니다.
    [SerializeField] TMP_Text _rewardAmountText;

    #endregion

    #region 프로퍼티

    /// <summary>
    /// 보상 종류 값입니다.
    /// </summary>
    public QuestRewardType RewardType => _type;

    #endregion

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 전달받은 보상 종류와 이름, 수량을 표시합니다.
    /// </summary>
    /// <param name="reward">보상 값입니다.</param>
    public void Bind(QuestListView.Reward reward)
    {
        if (reward != null) _type = reward.type;
        var screen = GetComponentInParent<QuestScreenView>(true);
        if (_rewardImage != null)
        {
            QuestRewardIconView.Configure(_rewardImage, reward, screen != null ? screen.RewardFallbackSprite : null);
        }
        if (_rewardNameText != null)
        {
            _rewardNameText.text = QuestRewardPresentation.IsConfigured(reward) ? QuestRewardPresentation.GetName(reward) : "보상 미정";
        }
        if (_rewardAmountText != null)
        {
            _rewardAmountText.text = QuestRewardPresentation.IsConfigured(reward) ? $"+{reward.amount:0.##}" : "-";
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 현재 종류에 맞는 보상 이름, 아이콘과 수량을 표시합니다.
    /// </summary>
    /// <param name="rewardSprite">보상 스프라이트 이미지입니다.</param>
    /// <param name="rewardAmount">보상 수량 값입니다.</param>
    public void SetReward(Sprite rewardSprite, float rewardAmount)
    {
        Bind(new QuestListView.Reward { type = _type, icon = rewardSprite, amount = Mathf.Max(0, rewardAmount) });
    }

    #endregion

}
