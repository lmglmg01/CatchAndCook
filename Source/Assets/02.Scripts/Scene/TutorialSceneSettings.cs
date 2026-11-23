using UnityEngine;

// 전용 씬의 PlayScene에만 붙입니다. 일반 Build 씬에는 추가하지 않습니다.
[DisallowMultipleComponent]
public sealed class TutorialSceneSettings : MonoBehaviour
{
    public enum Chapter { MorningBoat, Dive, AfternoonBoat, Sushi, NightBoat }

    [Header("튜토리얼 구간")]
    [SerializeField] Chapter _chapter;
    [Header("완료 후 이동할 씬")]
    [SerializeField] string _nextSceneName;

    public Chapter CurrentChapter => _chapter;
    public string NextSceneName => _nextSceneName;
    public DayPhase Phase => (_chapter == Chapter.Sushi || _chapter == Chapter.NightBoat) ? DayPhase.Night
        : _chapter == Chapter.AfternoonBoat ? DayPhase.Afternoon : DayPhase.Morning;

    public static string GameplaySceneName(string sceneName) => sceneName switch
    {
        "TutorialMorningBoat" => "MorningBoat",
        "TutorialDive" => "Dive",
        "TutorialAfterNoonBoat" => "AfterNoonBoat",
        "TutorialSushi" => GameManager.NightScene,
        "TutorialNightBoat" => "NightBoat",
        _ => sceneName
    };
}
