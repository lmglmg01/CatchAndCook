using System.Collections.Generic;

public interface IQuestListShow
{
    // 전달받은 순서대로 목록을 표시하고, 사용하지 않는 슬롯은 숨긴다.
    void Show(IReadOnlyList<QuestController.QuestProgress> quests);

    void Hide();
}
