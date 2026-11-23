using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum DayPhase
{
    Morning,
    Afternoon,
    Night
}

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    [Header("공통 데이터 관리 객체: GameManager 자신 또는 자식에 배치")]
    [SerializeField] Inventory _inventory;
    [SerializeField] EquipController _equipController;
    [SerializeField] QuestController _questController;
    [SerializeField] PlayerProgress _playerProgress;

    [Header("플레이어 정보")]
    [SerializeField] string _playerName;

    [Header("진행 일수와 시간대")]
    [SerializeField, Min(1)] int _dayNumber;
    [SerializeField] DayPhase _dayPhase;

    const string MorningBoatScene = "MorningBoat";
    const string AfternoonBoatScene = "AfterNoonBoat";
    const string NightBoatScene = "NightBoat";
    internal const string NightScene = "Sushi 2";

    bool _hasStartedDay;
    WeatherType _dailyWeather = WeatherType.Count;
    bool _isTutorialScene;
    int _morningSceneHandle = -1;
    SushiManager _nightManager;
    bool _nightTransitionStarted;
    bool _diveTransitionCompleted;
    PlayScene _playScene;
    GameplayViewConnector _viewConnector;
    int _sceneHandle;
    bool _started;

    public const string DefaultPlayerName = "Player001";
    public string PlayerName => string.IsNullOrWhiteSpace(_playerName) ? DefaultPlayerName : _playerName;

    public static GameManager Instance { get; private set; }
    internal BossDiveTravel BossTravel { get; } = new BossDiveTravel();
    public Inventory Inventory => _inventory;
    public EquipController EquipController => _equipController;
    public QuestController QuestController => _questController;
    public PlayerProgress PlayerProgress => _playerProgress;
    public PlayScene CurrentPlayScene => _playScene;
    public int DayNumber => _dayNumber;
    public string CurrentDate => $"{_dayNumber}day";
    public DayPhase CurrentDayPhase => _dayPhase;
    public WeatherType CurrentWeather => _isTutorialScene ? WeatherType.Sunny : _dailyWeather;
    public event Action<DayPhase> DayPhaseChanged;

    // 물고기 이벤트 구독/해제는 스포너가 소유한다.
    public bool ReportFishKilled(FishData data)
    {
        if (Instance != this || !isActiveAndEnabled || _questController == null || data == null) return false;
        _questController.ReportKilled(data.FishId, 1);
        return true;
    }

    public bool ReportFishCollected(FishData data)
    {
        if (Instance != this || !isActiveAndEnabled || _questController == null || data == null) return false;
        _questController.ReportFishCaptured(data, 1);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _dayNumber = Mathf.Max(1, _dayNumber);
        if (gameObject.scene.name == AfternoonBoatScene) _dayPhase = DayPhase.Afternoon;
        else if (gameObject.scene.name == MorningBoatScene) _dayPhase = DayPhase.Morning;
        else if (gameObject.scene.name == NightScene || gameObject.scene.name == NightBoatScene) _dayPhase = DayPhase.Night;
        if (_inventory == null) _inventory = GetComponentInChildren<Inventory>(true);
        if (_equipController == null) _equipController = GetComponentInChildren<EquipController>(true);
        if (_questController == null) _questController = GetComponentInChildren<QuestController>(true);
        if (_playerProgress == null) _playerProgress = GetComponentInChildren<PlayerProgress>(true);

        GameSaveService.RestorePending(this);
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        if (_started) ConnectLoadedScene(SceneManager.GetActiveScene());
    }

    void Start()
    {
        if (Instance != this) return;
        _started = true;
        ConnectLoadedScene(SceneManager.GetActiveScene());
    }

    public string AdvanceAfterDive()
    {
        if (_diveTransitionCompleted) return null;
        if (_dayPhase == DayPhase.Morning)
        {
            _diveTransitionCompleted = true;
            SetDayPhase(DayPhase.Afternoon);
            return TutorialDestination(TutorialSceneSettings.Chapter.Dive) ?? AfternoonBoatScene;
        }
        if (_dayPhase == DayPhase.Afternoon)
        {
            _diveTransitionCompleted = true;
            SetDayPhase(DayPhase.Night);
            return NightBoatScene;
        }
        return null;
    }

    public string AdvanceBySkipping()
    {
        if (_dayPhase == DayPhase.Morning)
        {
            SetDayPhase(DayPhase.Afternoon);
            return AfternoonBoatScene;
        }
        if (_dayPhase == DayPhase.Afternoon)
        {
            SetDayPhase(DayPhase.Night);
            return NightBoatScene;
        }
        return null;
    }

    public bool CompleteNight()
    {
        if (_dayPhase != DayPhase.Night || _nightTransitionStarted) return false;
        _nightTransitionStarted = true;
        if (_nightManager == null) _nightManager = FindFirstObjectByType<SushiManager>();
        if (_nightManager != null && _nightManager.IsMenuConfirmed) _nightManager.EndBusiness();
        _nightManager = null;
        string tutorialDestination = TutorialDestination(TutorialSceneSettings.Chapter.Sushi);
        if (_playScene is SushiPlayScene sushiScene)
        {
            sushiScene.CompleteTutorial();
            if (tutorialDestination != null && sushiScene.TryLoadAfterTutorial(tutorialDestination)) return true;
        }
        SceneManager.LoadScene(tutorialDestination ?? MorningBoatScene);
        return true;
    }

    string TutorialDestination(TutorialSceneSettings.Chapter chapter)
    {
        var tutorial = _playScene != null ? _playScene.GetComponent<TutorialSceneSettings>() : null;
        return tutorial != null && tutorial.isActiveAndEnabled && tutorial.CurrentChapter == chapter
            && !string.IsNullOrWhiteSpace(tutorial.NextSceneName) ? tutorial.NextSceneName : null;
    }

    internal void SetPlayerName(string value)
    {
        _playerName = string.IsNullOrWhiteSpace(value) ? DefaultPlayerName : value.Trim();
    }

    internal void EndSession()
    {
        if (Instance != this) return;
        enabled = false;
        Instance = null;
        Destroy(gameObject);
    }

    internal void RestoreDayState(int dayNumber, DayPhase phase)
    {
        _dayNumber = dayNumber;
        _dayPhase = phase;
        _hasStartedDay = true;
        if (gameObject.scene.name == MorningBoatScene)
            _morningSceneHandle = gameObject.scene.handle;
    }

    void SetDayPhase(DayPhase phase)
    {
        _dayPhase = phase;
        DayPhaseChanged?.Invoke(phase);
    }

    void OnDisable()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        DisconnectScene(_playScene);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        DisconnectScene(_playScene);
        Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ConnectLoadedScene(scene);

    void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == NightScene)
        {
            _nightManager = null;
        }
        if (scene.handle == _morningSceneHandle) _morningSceneHandle = -1;
        if (scene.handle == _sceneHandle) DisconnectScene(_playScene);
    }

    void ConnectLoadedScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        BeginSceneDay(scene);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (PlayScene playScene in root.GetComponentsInChildren<PlayScene>(true))
            {
                if (!playScene.isActiveAndEnabled) continue;
                ConnectScene(playScene);
                return;
            }
        }
        GameSaveService.SaveAtSceneEntry(this, scene);
    }

    // MorningBoat의 sceneLoaded와 Start가 모두 실행돼도 같은 씬에서는 한 번만 처리한다.
    void BeginSceneDay(Scene scene)
    {
        if (scene.name != MorningBoatScene)
        {
            if (scene.name == AfternoonBoatScene || scene.name == NightScene || scene.name == NightBoatScene)
                _hasStartedDay = true;
            return;
        }
        if (scene.handle == _morningSceneHandle) return;

        _morningSceneHandle = scene.handle;
        if (_hasStartedDay) _dayNumber++;
        _hasStartedDay = true;
        _nightTransitionStarted = false;
        _diveTransitionCompleted = false;
        SelectDailyWeather();
        SetDayPhase(DayPhase.Morning);
    }

    void SelectDailyWeather()
    {
        _dailyWeather = (WeatherType)UnityEngine.Random.Range(0, (int)WeatherType.Count);
    }

    // 같은 씬의 Start와 sceneLoaded가 모두 호출되어도 한 번만 연결한다.
    public void ConnectScene(PlayScene scene)
    {
        if (Instance != this || !isActiveAndEnabled || scene == null || !scene.isActiveAndEnabled
            || ReferenceEquals(_playScene, scene)) return;

        BeginSceneDay(scene.gameObject.scene);
        if (scene.CurrentSceneType == SceneType.UnderSea) _hasStartedDay = true;
        else BossTravel.Clear();
        // 초밥씬을 직접 실행해도 밤 영업을 하루의 진행으로 기록합니다.
        if (scene is SushiPlayScene)
        {
            _hasStartedDay = true;
            _nightTransitionStarted = false;
            SetDayPhase(DayPhase.Night);
        }
        DisconnectScene(_playScene);
        _playScene = scene;
        _sceneHandle = scene.gameObject.scene.handle;
        if (scene.CurrentSceneType == SceneType.UnderSea) _diveTransitionCompleted = false;
        var tutorial = scene.GetComponent<TutorialSceneSettings>();
        _isTutorialScene = tutorial != null && tutorial.isActiveAndEnabled;
        if (_isTutorialScene)
        {
            _hasStartedDay = true;
            _nightTransitionStarted = false;
            SetDayPhase(tutorial.Phase);
        }
        else if (_dailyWeather == WeatherType.Count)
        {
            // 이어하기나 씬 직접 실행으로 아침 처리를 건너뛴 경우에만 초기화한다.
            SelectDailyWeather();
        }

        scene.BindServices(this);

        scene.SubscribeSceneEvents();

        scene.InitializeScene();
        _viewConnector = new GameplayViewConnector(scene, this);
        _viewConnector.TryAcceptMainQuestAtBoatEntry(scene is BoatPlayScene boat && boat.IsTutorialActive);
        GameSaveService.SaveAtSceneEntry(this, scene.gameObject.scene);
    }

    public void DisconnectScene(PlayScene scene)
    {
        if (ReferenceEquals(_playScene, null) || !ReferenceEquals(_playScene, scene)) return;

        _viewConnector?.Dispose();
        _viewConnector = null;

        scene.UnsubscribeSceneEvents();

        _playScene = null;
        _sceneHandle = 0;
        _isTutorialScene = false;
        scene.ReleaseServices();
    }
}


