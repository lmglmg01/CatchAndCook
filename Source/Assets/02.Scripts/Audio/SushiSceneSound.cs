using System;
using SushiPouringV3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Keeps restaurant audio attached to the existing gameplay and presentation state.
internal sealed class SushiSceneSound : IDisposable
{
    static readonly string[] Popups =
    {
        "MenuBook", "RegisteredMenuActionsPopup", "MenuRegistrationPopup",
        "RecipeUpgradeConfirmPopup", "RecipeUpgradeResultPopup", "StaffUpgrade",
        "SushiMinigame/MinigamePanel"
    };

    readonly Scene _scene;
    readonly SushiManager _shop;
    readonly EnforceManager _upgrades;
    readonly GuestReactionEffects _reactions;
    readonly SushiCarrier _playerCarrier;
    readonly SushiInteractor _interactor;
    readonly DrinkMinigameController _drinkController;
    readonly RectTransform _sign;
    readonly Image _signImage;
    readonly Transform _closingSignText;

    Sushi _heldMeal;
    AudioSource _cleanLoop;
    AudioSource _dripLoop;
    FluidPourSession _lastDrinkSession;
    double _lastPouredVolume;
    string _upgradeProgressSnapshot;
    int _finishedCooking;
    bool _businessSignOpen;
    bool _openingSign;
    bool _signLightsPlayed;
    bool _signExitPlayed;
    float _signRestY;

    internal SushiSceneSound(Scene scene, Action<GameObject, bool> bindPopup)
    {
        _scene = scene;
        IjakayaView sceneView = null;
        GroundHero player = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (sceneView == null) sceneView = root.GetComponentInChildren<IjakayaView>(true);
            if (player == null) player = root.GetComponentInChildren<GroundHero>(true);
            if (_shop == null) _shop = root.GetComponentInChildren<SushiManager>(true);
            if (_upgrades == null) _upgrades = root.GetComponentInChildren<EnforceManager>(true);
            if (_reactions == null) _reactions = root.GetComponentInChildren<GuestReactionEffects>(true);
            if (_drinkController == null) _drinkController = root.GetComponentInChildren<DrinkMinigameController>(true);
            foreach (var penalty in root.GetComponentsInChildren<SushiPenaltyView>(true))
            {
                var director = penalty.GetComponent<UIDirectorView>();
                if (director == null) continue;
                foreach (var binding in director.Bindings)
                    if (binding.owner == penalty && binding.target != null) bindPopup(binding.target.gameObject, true);
            }
        }

        if (sceneView != null)
        {
            foreach (string path in Popups)
            {
                var popup = sceneView.transform.Find(path);
                if (popup != null) bindPopup(popup.gameObject, true);
            }
            // Settlement already sends its opening cue from the presenter.
            if (sceneView.BusinessResultView != null) bindPopup(sceneView.BusinessResultView.gameObject, false);
            _sign = sceneView.transform.Find("BusinessSign/SignVisual") as RectTransform;
            _signImage = _sign != null ? _sign.Find("Sign")?.GetComponent<Image>() : null;
            _closingSignText = _sign != null ? _sign.Find("ClosingText") : null;
            if (_sign != null) _signRestY = _sign.anchoredPosition.y;
        }

        _interactor = player != null ? player.GetComponent<SushiInteractor>() : null;
        _playerCarrier = player != null ? player.SushiCarrier : null;
        if (_playerCarrier == null && player != null) _playerCarrier = player.GetComponent<SushiCarrier>();
        if (_playerCarrier != null)
        {
            _heldMeal = _playerCarrier.CurrentSushi;
            _playerCarrier.FoodServed += OnFoodServed;
        }
        if (_shop != null && _shop.Cooker != null) _finishedCooking = _shop.Cooker.CompletedCookingCount;
        if (_upgrades != null)
        {
            _upgradeProgressSnapshot = _upgrades.Progress?.ExportJson();
            _upgrades.UpgradeStateChanged += OnUpgrade;
        }
        if (_reactions != null)
        {
            _reactions.PlateThrown += OnPlateThrown;
            _reactions.ScreenHit += OnScreenHit;
        }
    }

    public void Dispose()
    {
        StopActionLoops();
        if (_cleanLoop != null) UnityEngine.Object.Destroy(_cleanLoop.gameObject);
        if (_dripLoop != null) UnityEngine.Object.Destroy(_dripLoop.gameObject);
        if (_playerCarrier != null) _playerCarrier.FoodServed -= OnFoodServed;
        if (_upgrades != null) _upgrades.UpgradeStateChanged -= OnUpgrade;
        if (_reactions != null)
        {
            _reactions.PlateThrown -= OnPlateThrown;
            _reactions.ScreenHit -= OnScreenHit;
        }
    }

    internal void Update()
    {
        if (!_scene.isLoaded || SceneManager.GetActiveScene() != _scene)
        {
            StopActionLoops();
            return;
        }

        UpdateActionLoops();
        if (_upgradeProgressSnapshot == null && _upgrades != null)
            _upgradeProgressSnapshot = _upgrades.Progress?.ExportJson();
        if (_playerCarrier != null)
        {
            Sushi current = _playerCarrier.CurrentSushi;
            if (_heldMeal != null && current == null) GameSfx.Play(GameSoundId.FoodDiscard);
            _heldMeal = current;
        }

        var cooker = _shop != null ? _shop.Cooker : null;
        if (cooker != null)
        {
            int completed = cooker.CompletedCookingCount;
            for (int i = _finishedCooking; i < completed; i++)
                GameSfx.PlayAt(GameSoundId.FinishCook, cooker.transform.position);
            _finishedCooking = completed;
        }

        UpdateBusinessSign();
    }

    void UpdateActionLoops()
    {
        bool cleaning = Time.timeScale > 0f && _interactor != null && _interactor.isActiveAndEnabled
            && _interactor.ActiveTarget is Chair chair && chair != null && chair.CleaningElapsed > 0f;
        SetActionLoop(ref _cleanLoop, GameSoundId.CleanLoop, _interactor, cleaning);

        FluidPourSession session = null;
        if (_drinkController != null && _drinkController.isActiveAndEnabled)
            _drinkController.TryGetSession(out session);

        if (!ReferenceEquals(session, _lastDrinkSession))
        {
            _lastDrinkSession = session;
            _lastPouredVolume = 0d;
        }

        // The fluid can keep pouring between key taps. Follow emitted volume rather than input.
        double volume = session?.GeneratedVolume ?? 0d;
        bool pouring = session != null && session.IsRunning && volume > _lastPouredVolume;
        _lastPouredVolume = volume;
        SetActionLoop(ref _dripLoop, GameSoundId.DrinkDrip, _drinkController, pouring);
    }

    static void SetActionLoop(ref AudioSource source, GameSoundId id, Component owner, bool playing)
    {
        if (!playing)
        {
            StopLoop(source);
            return;
        }

        if (source == null) source = GameSfx.CreateLoop(id, owner.transform);
        if (source != null && !source.isPlaying) source.Play();
    }

    static void StopLoop(AudioSource source)
    {
        if (source != null && source.isPlaying) source.Stop();
    }

    void StopActionLoops()
    {
        StopLoop(_cleanLoop);
        StopLoop(_dripLoop);
    }

    void OnUpgrade()
    {
        string progress = _upgrades.Progress?.ExportJson();
        if (progress == null || progress == _upgradeProgressSnapshot) return;

        bool hasPreviousState = _upgradeProgressSnapshot != null;
        _upgradeProgressSnapshot = progress;
        if (hasPreviousState) GameSfx.Play(GameSoundId.Trade);
    }

    void OnFoodServed(GuestNPC guest)
    {
        // Serving clears the same slot as discarding, but already has its own cue.
        _heldMeal = null;
    }

    void OnPlateThrown() => GameSfx.Play(GameSoundId.KnifeSwing);
    void OnScreenHit() => GameSfx.Play(GameSoundId.ScreenCrack);

    void UpdateBusinessSign()
    {
        if (_sign == null || _signImage == null) return;
        bool active = _sign.gameObject.activeInHierarchy;
        if (active != _businessSignOpen)
        {
            _businessSignOpen = active;
            if (!active)
            {
                if (!_signExitPlayed) GameSfx.Play(GameSoundId.CloseUi);
                _signRestY = _sign.anchoredPosition.y;
                return;
            }

            // Closing starts with the OPEN lamps lit before the sign switches to CLOSED.
            _openingSign = !IsSignLit();
            _signLightsPlayed = false;
            _signExitPlayed = false;
            GameSfx.Play(GameSoundId.OpenUi);
        }

        if (!active) return;
        if (!_signLightsPlayed)
        {
            // The closing text fades in with the final lights-off transition.
            bool lightsChanged = _openingSign ? IsSignLit() :
                _closingSignText != null ? _closingSignText.gameObject.activeInHierarchy : !IsSignLit();
            if (!lightsChanged) return;
            _signLightsPlayed = true;
            GameSfx.Play(GameSoundId.SignLights);
        }
        else if (!_signExitPlayed && _sign.anchoredPosition.y > _signRestY + 1f)
        {
            _signExitPlayed = true;
            GameSfx.Play(GameSoundId.CloseUi);
        }
    }

    bool IsSignLit()
        => _signImage.sprite != null && _signImage.sprite.name == "restaurant_open_temporary" && _signImage.color.a > 0f;
}
