using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DivePlayScene : PlayScene
{
    [Header("첫 잠수 튜토리얼")]
    [SerializeField] DiveTutorialController _tutorial;
    [SerializeField] TalkView _tutorialTalkView;
    bool TutorialDialogue => _tutorial != null && _tutorial.IsActive && _tutorial.IsDialogue;
    bool IsPhoneOpen => _phoneView != null && _phoneView.isActiveAndEnabled
        && (_phoneView.CurrentAppIndex >= 0 || (_phoneAppGroup != null && _phoneAppGroup.activeInHierarchy));

    [Header("잠수 휴대폰")]
    [SerializeField] BaseView _phoneView;
    [SerializeField] GameObject _phoneAppGroup;

    [Header("잠수 복귀 로딩")]
    [SerializeField] DiveLoadView _morningBoatLoadViewPrefab;
    [SerializeField] DiveLoadView _afternoonBoatLoadViewPrefab;
    [SerializeField] DiveLoadView _nightBoatLoadViewPrefab;
    [SerializeField, Min(0f)] float _minimumBoatLoadDuration;
    [SerializeField] DiveLoadView _bossLoadViewPrefab;

    [Header("다이브 결과")]
    [SerializeField] ShipReturn _returnShip;
    [SerializeField] internal DiveResultView _diveResultViewPrefab;

    [Header("피격 연출")]
    [SerializeField] internal HeroRunModel _heroRunModel;
    [SerializeField] internal CameraController _cameraController;
    [SerializeField] EffectSpawner _effectSpawner;

    [Header("입수 연출")]
    [SerializeField] DiveEntryController _diveEntry;

    [Header("잠수 캐릭터 뷰")]
    [SerializeField] internal DiveCharacterView _diveCharacterView;
    internal override ShipReturn ReturnShip => _returnShip;
    internal override HeroRunModel RunModel => _heroRunModel;
    internal override EffectSpawner Effects => _effectSpawner;
    bool _heroEquipmentInitialized;
    bool _resumedUnderwater;
    bool _diveResultOpen;
    float _nextOxygenWarning;
    DiveResultView _diveResultView;

    bool CanHoldInteraction => isActiveAndEnabled && SceneEventsSubscribed
        && !_diveResultOpen && !_diveSceneLoading && !TutorialDialogue && !IsPhoneOpen
        && (_diveEntry == null || !_diveEntry.IsPlaying)
        && (_hero is not UnderSeaHero hero || hero.HarpoonShooter == null || !hero.HarpoonShooter.IsResisting)
        && RunModel != null && RunModel.isActiveAndEnabled && RunModel.CurrentOxygen > 0f && Time.timeScale > 0f;
    internal bool CanUseSupplies => CanHoldInteraction && (_tutorial == null || _tutorial.AllowsInteract)
        && (Base == null || Base.TalkView == null || !Base.TalkView.IsTalking);
    internal bool CanUseSurfaceReturn => CanHoldInteraction && (_tutorial == null || _tutorial.AllowsReturn)
        && !(_hero is UnderSeaHero hero && hero.HasSupplyTarget && CanUseSupplies);

    internal bool CanUseBossGate => isActiveAndEnabled && SceneEventsSubscribed
        && !_diveResultOpen && !_diveSceneLoading && !TutorialDialogue
        && RunModel != null && RunModel.isActiveAndEnabled && RunModel.CurrentOxygen > 0f;

    internal bool TryEnterBossScene(string sceneName)
    {
        if (!CanUseBossGate || !CanLoadScene(sceneName)) return false;
        if (_gameManager == null || _hero is not UnderSeaHero hero) return false;
        _gameManager.BossTravel.EnterBoss(this, hero, sceneName);
        LoadUnderwaterScene(sceneName);
        return true;
    }

    internal bool TryReturnFromBoss()
    {
        if (!CanUseSurfaceReturn || _gameManager == null || _hero is not UnderSeaHero hero) return false;
        string destination = _gameManager.BossTravel.ReturnScene;
        if (!CanLoadScene(destination)) return false;
        _gameManager.BossTravel.LeaveBoss(hero);
        LoadUnderwaterScene(destination);
        return true;
    }

    void LoadUnderwaterScene(string sceneName)
    {
        DiveLoadView view = _bossLoadViewPrefab != null ? Instantiate(_bossLoadViewPrefab) : null;
        BeginSceneLoad(sceneName, view, view != null, 0f);
    }

    DiveLoadView GetBoatLoadViewPrefab(string sceneName)
    {
        string name = TutorialSceneSettings.GameplaySceneName(System.IO.Path.GetFileNameWithoutExtension(sceneName));
        if (string.Equals(name, "MorningBoat", StringComparison.OrdinalIgnoreCase))
            return _morningBoatLoadViewPrefab;
        if (string.Equals(name, "AfterNoonBoat", StringComparison.OrdinalIgnoreCase))
            return _afternoonBoatLoadViewPrefab;
        // 현재 저녁 진행은 초밥집 씬으로 연결되어 있다.
        if (string.Equals(name, "NightBoat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, GameManager.NightScene, StringComparison.OrdinalIgnoreCase))
            return _nightBoatLoadViewPrefab;
        return null;
    }
    public override void ChangeScene(string sceneName)
    {
        if (_diveSceneLoading || !isActiveAndEnabled) return;
        if (_sceneType == SceneType.UnderSea)
        {
            DiveLoadView prefab = GetBoatLoadViewPrefab(sceneName);
            if (prefab != null)
            {
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Debug.LogError($"복귀할 씬을 빌드 목록에 연결해주세요: {sceneName}", this);
                    return;
                }
                BeginSceneLoad(sceneName, Instantiate(prefab), true, _minimumBoatLoadDuration);
                return;
            }
        }
        base.ChangeScene(sceneName);
    }

    protected override void Start()
    {
        base.Start();
        if (!_resumedUnderwater && _diveEntry != null) _diveEntry.PlayEntry();
    }

    void Update()
    {
        if (!SceneEventsSubscribed || _diveResultOpen || _diveSceneLoading || TutorialDialogue
            || Time.timeScale <= 0f || RunModel == null || !RunModel.isActiveAndEnabled
            || (_diveEntry != null && _diveEntry.IsPlaying)) return;

        float oxygen = RunModel.CurrentOxygen;
        if (oxygen <= 0f || oxygen > RunModel.MaxOxygen * 0.15f)
        {
            _nextOxygenWarning = 0f;
            return;
        }

        if (Time.time < _nextOxygenWarning) return;
        GameSfx.Play(GameSoundId.OxygenLow);
        _nextOxygenWarning = Time.time + (oxygen <= RunModel.MaxOxygen * 0.07f ? 1f : 2f);
    }

    internal override void InitializeScene()
    {
        base.InitializeScene();
        if (_phoneView != null && _phoneView.isActiveAndEnabled) EnsureUiEventSystem();
        InitializeHeroEquipment();
        if (_gameManager != null && _hero is UnderSeaHero arrivingHero)
            _resumedUnderwater = _gameManager.BossTravel.TryRestore(this, arrivingHero);
        if (_tutorial != null) _tutorial.Begin(this, _gameManager, _tutorialTalkView);
        if (_hero is UnderSeaHero hero && hero.HarpoonShooter != null)
        {
            GigShooter shooter = hero.HarpoonShooter;
            OnCameraAimChanged(hero.IsAiming);
            OnHarpoonFlightChanged(shooter.IsFlying);
            OnResistanceGaugeChanged(shooter.ResistanceGauge, shooter.ResistanceGaugeMax);
            OnResistanceChanged(shooter.IsResisting);
        }
    }

    internal override void ReleaseServices()
    {
        if (_tutorial != null) _tutorial.StopTutorial();
        if (_hero is UnderSeaHero hero) hero.HarpoonShooter?.CancelResistance();
        OnResistanceChanged(false);
        OnCameraAimChanged(false);
        OnHarpoonFlightChanged(false);
        base.ReleaseServices();
    }

    internal override void OnInteractInput()
    {
        if (_tutorialTalkView != null && _tutorialTalkView.IsTalking)
        {
            _tutorialTalkView.NextTalk();
            return;
        }
        if (Base != null && Base.TalkView != null && Base.TalkView.IsTalking)
        {
            Base.TalkView.NextTalk();
            return;
        }
        if (_tutorial != null && !_tutorial.AllowsInteract) return;
        if (_hero is UnderSeaHero hero) hero.Interact();
    }

    internal override void OnCancelInput()
    {
        if (_phoneView != null && _phoneView.isActiveAndEnabled && Keyboard.current?.escapeKey.isPressed == true)
        {
            if (!_diveResultOpen && !_diveSceneLoading && !_phoneView.IsTalkMode && !UIEscapeView.TryCloseTopmost())
                _phoneView.ToggleSmartPhoneFromEscape();
            return;
        }

        if (_hero is UnderSeaHero hero && hero.HarpoonShooter != null && hero.HarpoonShooter.IsResisting)
            hero.HarpoonShooter.CancelResistance();
    }

    public override void SubscribeSceneEvents()
    {
        if (!BeginSceneEventSubscription()) return;
        if (Inputs != null && SceneHero is UnderSeaHero hero)
        {
            Inputs.AimInput += OnTutorialAim;
            Inputs.SprintInput += OnTutorialSprint;
            Inputs.AttackInput += OnTutorialAttack;
            Inputs.MeleeModeInput += OnTutorialMeleeMode;
            Inputs.RangeModeInput += OnTutorialRangeMode;
            Inputs.ToggleRangeModeInput += OnTutorialToggleMode;
        }
        if (SceneHero is UnderSeaHero armedHero)
        {
            armedHero.GunFired += OnGunFired;
            armedHero.MeleeAttackRequested += OnMeleeAttack;
            armedHero.FishCollected += OnFishCollected;
        }

        if (SceneHero is UnderSeaHero diveHero && diveHero.HarpoonShooter != null)
        {
            diveHero.AimChanged += OnCameraAimChanged;
            diveHero.HarpoonShooter.Fired += OnHarpoonFired;
            diveHero.HarpoonShooter.Hit += OnHarpoonHit;
            diveHero.HarpoonShooter.FlightChanged += OnHarpoonFlightChanged;
            diveHero.HarpoonShooter.ResistanceChanged += OnResistanceChanged;
            diveHero.HarpoonShooter.ResistanceGaugeChanged += OnResistanceGaugeChanged;
        }

        if (Effects != null) DamageableSensor.HitDetected += Effects.SpawnHitEffect;
        if (RunModel != null)
        {
            RunModel.Damaged += OnHeroDamaged;
            RunModel.OxygenDepleted += OnOxygenDepleted;
        }
        if (ReturnShip != null) ReturnShip.ReturnRequested += OnNormalReturn;
    }

    public override void UnsubscribeSceneEvents()
    {
        if (!EndSceneEventSubscription()) return;
        if (Inputs != null && SceneHero is UnderSeaHero hero)
        {
            Inputs.AimInput -= OnTutorialAim;
            Inputs.SprintInput -= OnTutorialSprint;
            Inputs.AttackInput -= OnTutorialAttack;
            Inputs.MeleeModeInput -= OnTutorialMeleeMode;
            Inputs.RangeModeInput -= OnTutorialRangeMode;
            Inputs.ToggleRangeModeInput -= OnTutorialToggleMode;
        }
        if (SceneHero is UnderSeaHero armedHero)
        {
            armedHero.GunFired -= OnGunFired;
            armedHero.MeleeAttackRequested -= OnMeleeAttack;
            armedHero.FishCollected -= OnFishCollected;
        }

        if (SceneHero is UnderSeaHero diveHero && !ReferenceEquals(diveHero.HarpoonShooter, null))
        {
            diveHero.AimChanged -= OnCameraAimChanged;
            diveHero.HarpoonShooter.Fired -= OnHarpoonFired;
            diveHero.HarpoonShooter.Hit -= OnHarpoonHit;
            diveHero.HarpoonShooter.FlightChanged -= OnHarpoonFlightChanged;
            diveHero.HarpoonShooter.ResistanceChanged -= OnResistanceChanged;
            diveHero.HarpoonShooter.ResistanceGaugeChanged -= OnResistanceGaugeChanged;
        }

        if (!ReferenceEquals(Effects, null))
            DamageableSensor.HitDetected -= Effects.SpawnHitEffect;
        if (!ReferenceEquals(RunModel, null))
        {
            RunModel.Damaged -= OnHeroDamaged;
            RunModel.OxygenDepleted -= OnOxygenDepleted;
        }
        if (!ReferenceEquals(ReturnShip, null))
            ReturnShip.ReturnRequested -= OnNormalReturn;
    }

    internal override void OnMoveInput(Vector2 direction) => base.OnMoveInput(TutorialDialogue ? Vector2.zero : direction);
    void OnTutorialAim(bool aim)
    {
        if (_hero is UnderSeaHero hero)
            hero.SetAimInput(aim && !IsPhoneOpen && (_tutorial == null || _tutorial.AllowsHarpoon));
    }
    void OnTutorialSprint(bool sprint)
    {
        if (_hero is UnderSeaHero hero) hero.SetSprintInput(sprint && !TutorialDialogue);
    }
    void OnTutorialAttack(Vector2 position)
    {
        if (IsPhoneOpen || (_tutorial != null && !_tutorial.AllowsAttack)) return;
        if (_hero is UnderSeaHero hero) hero.Attack(position);
    }

    void OnTutorialMeleeMode()
    {
        if (TutorialDialogue) return;
        if (_hero is UnderSeaHero hero) hero.SelectMeleeMode();
    }
    void OnTutorialRangeMode()
    {
        if (_tutorial != null && !_tutorial.AllowsHarpoon) return;
        if (_hero is UnderSeaHero hero) hero.SelectRangeMode();
    }
    void OnTutorialToggleMode()
    {
        if (_tutorial != null && !_tutorial.AllowsGun) return;
        if (_hero is UnderSeaHero hero) hero.ToggleRangeMode();
    }

    internal void InitializeHeroEquipment()
    {
        if (_heroEquipmentInitialized || _equipController == null
            || !(_hero is UnderSeaHero underSeaHero)) return;

        underSeaHero.SetKnifeValues(_equipController.GetKnifeDamage(), _equipController.GetKnifeRange());
        underSeaHero.SetGigValues(_equipController.GetGigDamage(), _equipController.GetGigRange());
        underSeaHero.SetOxygenValue(_equipController.GetMaxOxygen());
        underSeaHero.SetSwimSuitValues(_equipController.GetSwimSuitMoveSpeed(), _equipController.GetSwimSuitDefense());
        underSeaHero.SetFishBagValue(_equipController.GetFishBagCapacity());
        underSeaHero.SetGunValues(_equipController.GetGunDamage(), _equipController.GetGunBulletCount());
        _heroEquipmentInitialized = true;
    }

    internal void OnCameraAimChanged(bool aiming)
    {
        if (_cameraController != null) _cameraController.SetAiming(aiming);
    }

    internal void OnHarpoonFired()
    {
        GameSfx.Play(GameSoundId.HarpoonShot);
        if (_cameraController != null) _cameraController.OnHarpoonFired();
    }

    internal void OnGunFired()
    {
        GameSfx.Play(GameSoundId.GunShot);
        if (_cameraController != null) _cameraController.OnGunFired();
    }

    void OnMeleeAttack() => GameSfx.Play(GameSoundId.KnifeSwing);

    void OnFishCollected(bool captured) => GameSfx.Play(GameSoundId.Pickup);

    internal void OnHarpoonHit()
    {
        if (_cameraController != null) _cameraController.OnHarpoonHit();
    }

    internal void OnHarpoonFlightChanged(bool flying)
    {
        if (_cameraController != null) _cameraController.SetHarpoonFlying(flying);
    }

    internal void OnResistanceChanged(bool resisting)
    {
        if (_diveCharacterView != null) _diveCharacterView.SetResistanceVisible(resisting);
        if (_cameraController != null) _cameraController.SetHarpoonResistance(resisting);
    }

    internal void OnResistanceGaugeChanged(float current, float max)
    {
        if (_diveCharacterView != null) _diveCharacterView.UpdateResistanceGauge(current, max);
    }

    internal void OnHeroDamaged(float damage)
    {
        GameSfx.PlayWithoutRestart(GameSoundId.DiverHit, RunModel);
        if (_cameraController != null) _cameraController.Hit();
    }

    internal void OnNormalReturn()
    {
        if (_diveResultOpen || (_tutorial != null && !_tutorial.AllowsReturn)) return;
        ShowDiveResult(_heroRunModel == null || _heroRunModel.CurrentOxygen > 0f);
        if (!_diveResultOpen) return;
        if (_tutorial != null) _tutorial.CompleteReturn();
    }

    internal void OnOxygenDepleted()
    {
        GameSfx.Play(GameSoundId.OxygenEmpty);
        var battle = GetComponent<BossBattleSession>();
        if (battle != null && battle.isActiveAndEnabled) battle.Defeat();
        ShowDiveResult(false);
    }

    internal void ShowDiveResult(bool isClear)
    {
        if (_sceneType != SceneType.UnderSea || _diveResultOpen) return;
        if (!(_hero is UnderSeaHero diver) || diver.FishBag == null
            || _diveResultViewPrefab == null || _gameManager == null || _gameManager.Inventory == null)
        {
            Debug.LogError("다이브 결과에 필요한 히어로, 가방, 결과 뷰 또는 인벤토리가 연결되지 않았습니다.", this);
            return;
        }

        if (!isClear && _tutorial != null) _tutorial.StopTutorial();
        _diveResultOpen = true;
        _hero.ResetInput();
        if (_inputHandler != null) _inputHandler.enabled = false;
        if (_heroRunModel != null) _heroRunModel.enabled = false;

        var canvasObject = new GameObject("DiveResultCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureUiEventSystem();

        _diveResultView = Instantiate(_diveResultViewPrefab, canvasObject.transform, false);
        GameUiSound.WatchPopup(_diveResultView.gameObject);
        _diveResultView.OnItemsConfirmed += OnDiveItemsConfirmed;
        var caughtItems = diver.FishBag.Items;
        _diveResultView.SetResult(isClear, caughtItems);
        int fishCount = 0;
        foreach (var item in caughtItems)
            fishCount += item.Count;
        _diveResultView.UpdateRecord(_gameManager.DayNumber, _gameManager.CurrentDate, fishCount, 0);
        _diveResultView.SetWindow(true);
        GameSfx.Play(GameSoundId.OpenUi);
        if (isClear) GameSfx.Play(GameSoundId.DiveComplete);
    }

    void EnsureUiEventSystem()
    {
        if (EventSystem.current != null) return;

        var root = new GameObject("DiveEventSystem");
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        root.AddComponent<EventSystem>();
        root.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    internal void OnDiveItemsConfirmed(System.Collections.Generic.IReadOnlyList<Inventory.BagItem> keptItems)
    {
        if (_diveSceneLoading) return;
        GameSfx.Play(GameSoundId.MenuClick);
        if (_diveResultView != null) _diveResultView.OnItemsConfirmed -= OnDiveItemsConfirmed;
        if (_inventory == null)
        {
            Debug.LogError("다이브 정산을 위한 공용 인벤토리가 없습니다.", this);
            return;
        }

        foreach (var item in keptItems)
        {
            if (!_inventory.TryAdd(item.ItemData, item.Count))
            {
                Debug.LogError($"다이브 획득물 정산 실패: {item.ItemData}", this);
                return;
            }
            _gameManager.QuestController?.ReportItemCollected(item.ItemData, item.Count);
            // Only retained fish fillets grant their configured experience per item.
            if (item.ItemData is FishFilletItemData fillet)
                _gameManager.PlayerProgress?.AddExperience(fillet.ExperienceOnAcquire * item.Count);
        }

        string nextScene = _gameManager.AdvanceAfterDive();
        if (string.IsNullOrEmpty(nextScene))
        {
            Debug.LogError("다이브 완료 후 이동할 시간대 씬을 결정하지 못했습니다.", this);
            return;
        }
        ChangeScene(nextScene);
    }
}
