using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum SceneType
{
    UnderSea,
    Ground
}

public enum WeatherType
{
    Sunny,
    Cloudy,
    Rainy,
    Count
}

public abstract class PlayScene : MonoBehaviour
{

    public abstract void SubscribeSceneEvents();
    public abstract void UnsubscribeSceneEvents();

    protected bool SceneEventsSubscribed { get; private set; }

    protected bool BeginSceneEventSubscription()
    {
        if (SceneEventsSubscribed) return false;
        SceneEventsSubscribed = true;
        InputHandler input = Inputs;
        if (input != null && SceneHero != null)
        {
            input.MoveInput += OnMoveInput;
            input.InteractInput += OnInteractInput;
            input.NavigateInput += OnNavigateInput;
            input.CancelInput += OnCancelInput;
            input.SmartPhoneInput += OnSmartPhoneInput;
        }

        return true;
    }

    protected bool EndSceneEventSubscription()
    {
        if (!SceneEventsSubscribed) return false;
        SceneEventsSubscribed = false;
        InputHandler input = Inputs;
        if (!ReferenceEquals(input, null))
        {
            input.MoveInput -= OnMoveInput;
            input.InteractInput -= OnInteractInput;
            input.NavigateInput -= OnNavigateInput;
            input.CancelInput -= OnCancelInput;
            input.SmartPhoneInput -= OnSmartPhoneInput;
        }

        return true;
    }

    [Header("씬 설정")]
    [SerializeField] internal SceneType _sceneType;

    [Header("플레이 객체")]
    [SerializeField] internal Hero _hero;
    [SerializeField] internal InputHandler _inputHandler;

    [Header("뷰")]
    [SerializeField] internal BaseView _baseView;

    public WeatherType WeatherType { get; private set; }
    internal SceneType CurrentSceneType => _sceneType;
    internal Hero SceneHero => _hero;
    internal InputHandler Inputs => _inputHandler;
    internal virtual Shop SceneShop => null;
    internal virtual StoreView Store => null;
    internal virtual UpgradeEquipView UpgradeView => null;
    internal virtual BoatNPC SceneBoatNPC => null;
    internal virtual DiveInteractableObject DiveObject => null;
    internal virtual SushiInteractableObject SushiObject => null;
    internal virtual ShipReturn ReturnShip => null;
    internal virtual HeroRunModel RunModel => null;
    internal virtual EffectSpawner Effects => null;
    internal BaseView Base => _baseView;
    internal virtual BoardView Board => null;

    internal GameManager _gameManager;
    internal Inventory _inventory;
    internal EquipController _equipController;

    bool _started;
    internal bool _diveSceneLoading;
    bool _diveInputWasEnabled;
    Coroutine _diveLoadRoutine;
    AsyncOperation _diveLoadOperation;
    DiveLoadView _activeLoadView;
    bool _ownsLoadView;

    protected virtual void OnEnable()
    {
        if (_started && GameManager.Instance != null) GameManager.Instance.ConnectScene(this);
    }

    protected virtual void Start()
    {
        _started = true;
        if (GameManager.Instance != null) GameManager.Instance.ConnectScene(this);
        else Debug.LogError("시작 씬에 공통 데이터가 연결된 GameManager를 배치해주세요.", this);
    }

    protected virtual void OnDisable()
    {
        ReleaseDiveLoading();
        if (_gameManager != null) _gameManager.DisconnectScene(this);
    }

    internal virtual void OnNavigateInput(int direction) { }

    internal virtual void OnSmartPhoneInput()
    {
        if (_sceneType != SceneType.Ground || _baseView == null || !_baseView.isActiveAndEnabled) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && (selected.GetComponent<TMPro.TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null)) return;
        _baseView.ToggleAppGroup();
    }

    // 기존 Inspector 및 애니메이션 이벤트 연결을 유지합니다.

    protected bool CanLoadScene(string sceneName)
    {
        if (_diveSceneLoading || !isActiveAndEnabled) return false;
        if (!string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName)) return true;
        Debug.LogWarning("이동할 씬 이름과 빌드 목록을 확인해주세요: " + sceneName, this);
        return false;
    }

    protected void BeginSceneLoad(string sceneName, DiveLoadView loadingView, bool ownsView, float minimumDuration)
    {
        _diveSceneLoading = true;
        _activeLoadView = loadingView;
        _ownsLoadView = ownsView;
        _diveInputWasEnabled = _inputHandler != null && _inputHandler.enabled;
        if (_hero != null) _hero.ResetInput();
        if (_inputHandler != null) _inputHandler.enabled = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (_activeLoadView != null) _activeLoadView.Show();
        _diveLoadRoutine = StartCoroutine(LoadDiveScene(sceneName, minimumDuration));
    }

    IEnumerator LoadDiveScene(string sceneName, float minimumDuration)
    {
        float shownAt = Time.realtimeSinceStartup;
        // 페이드가 끝난 뒤 로딩을 시작해 화면이 나타나는 중 끊기지 않게 합니다.
        yield return null;
        while (_activeLoadView != null && _activeLoadView.IsFadingIn)
            yield return null;

        try
        {
            _diveLoadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            Debug.LogError($"씬 로딩 실패 ({sceneName}): {exception.Message}", this);
        }

        if (_diveLoadOperation == null)
        {
            _diveLoadRoutine = null;
            ReleaseDiveLoading();
            yield break;
        }

        _diveLoadOperation.allowSceneActivation = false;
        minimumDuration = _activeLoadView != null ? Mathf.Max(0f, minimumDuration) : 0f;
        while (_diveLoadOperation.progress < 0.9f
            || Time.realtimeSinceStartup - shownAt < minimumDuration)
            yield return null;

        if (_activeLoadView != null)
        {
            DiveLoadView loadingView = _activeLoadView;
            loadingView.KeepForSceneTransition();
            // 이전 PlayScene이 제거돼도 새 씬 위의 로딩 캔버스가 퇴장 연출을 마칩니다.
            _diveLoadOperation.completed += operation =>
            {
                if (loadingView != null) loadingView.FadeOutAfterSceneLoad();
            };
        }
        _diveLoadOperation.allowSceneActivation = true;
        while (!_diveLoadOperation.isDone) yield return null;
        _diveLoadRoutine = null;
        ReleaseDiveLoading();
    }

    void ReleaseDiveLoading()
    {
        if (_diveLoadRoutine != null)
        {
            StopCoroutine(_diveLoadRoutine);
            _diveLoadRoutine = null;
        }
        // Unity 씬 로딩은 취소할 수 없으므로 활성화 대기로 큐가 잠기지 않게 합니다.
        if (_diveLoadOperation != null && !_diveLoadOperation.isDone)
            _diveLoadOperation.allowSceneActivation = true;
        _diveLoadOperation = null;
        if (!_diveSceneLoading) return;

        _diveSceneLoading = false;
        if (_activeLoadView != null && !_activeLoadView.IsKeptForSceneTransition)
        {
            _activeLoadView.Hide();
            if (_ownsLoadView) Destroy(_activeLoadView.gameObject);
        }
        _activeLoadView = null;
        _ownsLoadView = false;
        if (_inputHandler != null && isActiveAndEnabled)
            _inputHandler.enabled = _diveInputWasEnabled;
    }

    internal virtual void BindServices(GameManager manager)
    {
        _gameManager = manager;
        WeatherType = manager.CurrentWeather;
        _inventory = manager.Inventory;
        _equipController = manager.EquipController;
    }

    internal virtual void ReleaseServices()
    {
        ReleaseDiveLoading();
        if (_hero != null) _hero.ResetInput();
        _inventory = null;
        _equipController = null;
        _gameManager = null;
    }

    internal virtual void InitializeScene()
    {
        ApplySceneWeather();
        if (_baseView == null) return;
        _baseView.SetBasePanel(CurrentSceneType == SceneType.Ground);
        if (CurrentSceneType != SceneType.Ground) return;
        _baseView.UpdateDateText(_gameManager != null ? _gameManager.CurrentDate : "1day");
        if (_inventory != null) _baseView.UpdateGoldText(_inventory.Gold);
    }

    void ApplySceneWeather()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (SceneWeatherView view in root.GetComponentsInChildren<SceneWeatherView>())
            {
                if (view.isActiveAndEnabled) view.ApplyWeather(WeatherType);
            }
        }
    }

    public virtual void ChangeScene(string sceneName)
    {
        if (CanLoadScene(sceneName)) SceneManager.LoadScene(sceneName);
    }

    internal virtual void OnMoveInput(Vector2 direction)
    {
        if (_hero != null) _hero.SetMoveInput(direction);
    }

    internal virtual void OnInteractInput()
    {
        if (_hero is GroundHero hero) hero.Interact();
    }

    internal virtual void OnCancelInput() { }
}
