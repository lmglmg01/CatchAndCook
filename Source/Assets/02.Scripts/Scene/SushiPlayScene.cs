using UnityEngine;

[DisallowMultipleComponent]
public class SushiPlayScene : PlayScene
{
    [Header("초밥집 연결")]
    [SerializeField] SushiManager _sushiManager;
    [SerializeField] IjakayaPresenter _presenter;
    [SerializeField] SushiTutorialController _tutorial;

    [Header("튜토리얼 완료 로딩")]
    [SerializeField] DiveLoadView _tutorialCompletionLoadViewPrefab;
    [SerializeField, Min(0f)] float _minimumTutorialCompletionLoadDuration;

    bool _settlementPresented;

    internal bool AllowsBusinessStart => _tutorial == null || _tutorial.AllowsBusinessStart;

    public override void SubscribeSceneEvents()
    {
        if (!BeginSceneEventSubscription()) return;
        ResolveSushiReferences();
    }

    public override void UnsubscribeSceneEvents()
    {
        if (!EndSceneEventSubscription()) return;
        _tutorial?.StopTutorial();
    }

    internal override void InitializeScene()
    {
        base.InitializeScene();
        _tutorial?.Begin(this, _gameManager, _sushiManager, _presenter);
    }

    internal override void OnMoveInput(Vector2 direction)
    {
        base.OnMoveInput(_tutorial != null && _tutorial.IsDialogue ? Vector2.zero : direction);
    }

    internal override void OnInteractInput()
    {
        if (_tutorial != null && _tutorial.IsDialogue) _tutorial.AdvanceDialogue();
        else if (_tutorial == null || _tutorial.AllowsWorldInteract) base.OnInteractInput();
    }

    internal void CompleteTutorial() => _tutorial?.Complete();

    internal bool TryLoadAfterTutorial(string sceneName)
    {
        if (_tutorialCompletionLoadViewPrefab == null || !CanLoadScene(sceneName)) return false;
        BeginSceneLoad(sceneName, Instantiate(_tutorialCompletionLoadViewPrefab), true, _minimumTutorialCompletionLoadDuration);
        return true;
    }

    void ResolveSushiReferences()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (_sushiManager == null) _sushiManager = root.GetComponentInChildren<SushiManager>(true);
            if (_presenter == null) _presenter = root.GetComponentInChildren<IjakayaPresenter>(true);
        }
    }

    void LateUpdate()
    {
        if (!SceneEventsSubscribed || _sushiManager == null || _presenter == null || !_presenter.isActiveAndEnabled) return;
        if (!_sushiManager.IsMenuConfirmed || _sushiManager.RemainingOperationTime > 0f)
        {
            _settlementPresented = false;
            return;
        }
        if (_settlementPresented) return;
        _settlementPresented = true;
        if (_tutorial == null || !_tutorial.TryShowSettlement(_presenter.PresentSettlement))
            _presenter.PresentSettlement();
    }
}
