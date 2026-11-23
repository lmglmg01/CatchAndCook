using UnityEngine;

/// <summary>퀘스트 보상의 확정 여부와 툴팁 문구를 목록·상세창에서 동일하게 계산합니다.</summary>
public static class QuestRewardPresentation
{
    /// <summary>실제로 표시할 양수가 설정된 보상인지 확인합니다.</summary>
    public static bool IsConfigured(QuestListView.Reward reward)
    {
        return reward != null && reward.amount > 0 &&
               !float.IsNaN(reward.amount) && !float.IsInfinity(reward.amount);
    }

    /// <summary>보상 이름이 없으면 보상 종류에 맞는 기본 이름을 반환합니다.</summary>
    public static string GetName(QuestListView.Reward reward)
    {
        if (reward == null) return "보상 미정";
        if (!string.IsNullOrWhiteSpace(reward.name)) return reward.name;
        return reward.type == QuestRewardType.Gold ? "골드" :
               reward.type == QuestRewardType.Exp ? "경험치" : "아이템";
    }

    /// <summary>이미지 유무에 관계없이 확정된 보상에는 동일한 이름과 수량 문구를 만듭니다.</summary>
    public static string GetTooltip(QuestListView.Reward reward)
    {
        if (!IsConfigured(reward)) return "확정된 보상이 없습니다.";
        string unit = reward.type == QuestRewardType.Gold ? " 골드" :
                      reward.type == QuestRewardType.Exp ? " EXP" : "개";
        return GetName(reward) + "\n보상 수량: " + reward.amount.ToString("#,##0.##") + unit;
    }
}