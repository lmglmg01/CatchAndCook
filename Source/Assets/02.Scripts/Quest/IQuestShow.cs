public interface IQuestShow
{
    // 전달받은 퀘스트의 데이터와 진행 상태를 표시하거나 갱신한다.
    void Show(QuestController.QuestProgress quest);

    void Hide();
}
