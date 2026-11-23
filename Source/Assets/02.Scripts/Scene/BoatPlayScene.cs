using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BoatPlayScene : PlayScene
{
    [Header("첫 보트 튜토리얼")]
    [SerializeField] BoatTutorialController _tutorial;
    internal bool IsTutorialActive => _tutorial != null && _tutorial.IsActive;

    [Header("상점") ]
    [SerializeField] internal Shop _shop;
    [SerializeField] internal StoreView _storeView;
    [SerializeField] internal StoreScreenView _storeScreenView;

    [Header("장비 강화")]
    [SerializeField] UpgradeEquipView _upgradeEquipView;

    [Header("보트 NPC")]
    [SerializeField] BoatNPC _boatNPC;
    [SerializeField] internal BoatNPCInteractionController _boatNPCInteraction;

    [Header("씬 이동 영역")]
    [SerializeField] DiveInteractableObject _diveInteractableObject;

    [Header("초밥집 이동 영역")]
    [SerializeField] SushiInteractableObject _sushiInteractableObject;

    [Header("시간 스킵")]
    [SerializeField] string _skipSceneName;
    [SerializeField] DiveLoadView _skipLoadView;
    [SerializeField, Min(0f)] float _minimumSkipLoadDuration;

    [Header("다음 플레이")]
    [UnityEngine.Serialization.FormerlySerializedAs("_nextSceneName")]
    [SerializeField] string _nextPlaySceneName;
    [UnityEngine.Serialization.FormerlySerializedAs("_diveLoadView")]
    [SerializeField] DiveLoadView _nextPlayLoadView;
    [UnityEngine.Serialization.FormerlySerializedAs("_minimumDiveLoadDuration")]
    [SerializeField, Min(0f)] float _minimumNextPlayLoadDuration;

    [Header("보트 뷰")]
    [SerializeField] internal BoardView _boardView;
    [SerializeField] Sprite[] _weatherIcons;

    IEquipUpgradeView _equipmentUpgradeView;
    internal override Shop SceneShop => _shop;
    internal override StoreView Store => _storeView;
    internal override UpgradeEquipView UpgradeView => _upgradeEquipView;
    internal override BoatNPC SceneBoatNPC => _boatNPC;
    internal override DiveInteractableObject DiveObject => _diveInteractableObject;
    internal override SushiInteractableObject SushiObject => _sushiInteractableObject;
    internal override BoardView Board => _boardView;
    internal void OnDiveRequested()
    {
        if (_diveSceneLoading || _sceneType != SceneType.Ground || _gameManager == null
            || _gameManager.CurrentDayPhase == DayPhase.Night) return;
        MoveToNextPlayScene();
    }

    public void CompleteDiveTransition() => MoveToNextPlayScene();

    public void MoveToNextPlayScene()
    {
        if (_tutorial != null && !_tutorial.AllowsDive) return;
        if (!CanLoadScene(_nextPlaySceneName)) return;
        _tutorial?.CompleteDiveAttempt();
        BeginSceneLoad(_nextPlaySceneName, _nextPlayLoadView, false, _minimumNextPlayLoadDuration);
    }

    internal void OnTimeSkipRequested() => SkipScene();

    public void SkipScene()
    {
        if (IsTutorialActive && !_tutorial.AllowsTimeSkip) return;
        if (_sceneType != SceneType.Ground || _gameManager == null
            || !CanLoadScene(_skipSceneName)) return;
        // 저녁을 스킵하면 MorningBoat에 도착할 때 GameManager가 새 날짜를 시작합니다.
        if (_gameManager.CurrentDayPhase != DayPhase.Night
            && string.IsNullOrEmpty(_gameManager.AdvanceBySkipping())) return;
        _tutorial?.CompleteTimeSkip();
        BeginSceneLoad(_skipSceneName, _skipLoadView, false, _minimumSkipLoadDuration);
    }

    internal override void BindServices(GameManager manager)
    {
        base.BindServices(manager);
        if (_boardView != null) _boardView.BindDiveHero(_hero != null ? _hero.transform : null);
        if (_boardView != null) _boardView.BindNPCAnchor(_boatNPC != null ? _boatNPC.transform : null);
        if (_boatNPCInteraction != null) _boatNPCInteraction.BindBaseView(Base);
        _equipmentUpgradeView = _upgradeEquipView as IEquipUpgradeView;
        if (_storeScreenView == null && _storeView != null)
            _storeScreenView = _storeView.GetComponentInChildren<StoreScreenView>(true);
    }

    internal override void ReleaseServices()
    {
        _tutorial?.StopTutorial();
        SetBoatNPCNearby(false);
        if (_boatNPCInteraction != null) _boatNPCInteraction.BindBaseView(null);
        _equipmentUpgradeView = null;
        base.ReleaseServices();
    }

    internal override void InitializeScene()
    {
        base.InitializeScene();
        Base?.SetTimeSkipVisible(!string.IsNullOrWhiteSpace(_skipSceneName)
            && Application.CanStreamedLevelBeLoaded(_skipSceneName));
        _tutorial?.Begin(this, _gameManager);
        SetBoatNPCNearby(_boatNPC != null && _boatNPC.IsHeroDetected(_hero));
        if (_storeView != null && _storeView.isActiveAndEnabled) RefreshStore();
        if (_upgradeEquipView != null && _upgradeEquipView.isActiveAndEnabled) RefreshEquipmentUpgrade();
        int index = (int)WeatherType;
        if (_baseView != null && _weatherIcons != null && index >= 0 && index < _weatherIcons.Length)
            _baseView.UpdateWeatherIconImage(_weatherIcons[index]);
    }

    internal override void OnMoveInput(Vector2 direction)
    {
        if (_tutorial != null && _tutorial.BlocksMovement) direction = Vector2.zero;
        base.OnMoveInput(_boatNPCInteraction != null && _boatNPCInteraction.IsViewOpen ? Vector2.zero : direction);
    }

    internal override void OnInteractInput()
    {
        // 대화 입력은 월드 상호작용보다 먼저 소비한다.
        if (Base != null && Base.TalkView != null && Base.TalkView.IsTalking)
        {
            Base.TalkView.NextTalk();
            return;
        }
        if (IsTutorialActive && !_tutorial.AllowsDive && !_tutorial.AllowsNPC) return;
        if (_boatNPCInteraction != null && _boatNPCInteraction.IsViewOpen) return;
        if (_boatNPCInteraction != null && _boatNPCInteraction.IsNearby)
        {
            OnBoatNPCInteractionRequested(_hero);
            return;
        }
        base.OnInteractInput();
    }

    internal override void OnSmartPhoneInput()
    {
        if (!IsTutorialActive || _tutorial.AllowsSmartPhone) base.OnSmartPhoneInput();
    }

    internal override void OnCancelInput()
    {
        if (IsTutorialActive) return;
        if (_boatNPCInteraction != null) _boatNPCInteraction.CloseView();
    }

    public override void SubscribeSceneEvents()
    {
        if (!BeginSceneEventSubscription()) return;
        if (DiveObject != null) DiveObject.DiveRequested += OnDiveRequested;
        if (SushiObject != null && SushiObject.BoatMover != null)
            SushiObject.BoatMover.SceneTransitionRequested += CompleteDiveTransition;
        if (SceneBoatNPC != null)
        {
            SceneBoatNPC.HeroDetectionChanged += OnBoatNPCHeroDetectionChanged;
            SceneBoatNPC.InteractionRequested += OnBoatNPCInteractionRequested;
        }

        if (Store != null)
        {
            if (SceneShop != null)
            {
                SceneShop.SoldOutChanged += OnShopSoldOutChanged;
                GameSaveService.RestoreShopAtSceneEntry(this);
                SceneShop.PrepareSaleItems(Store.PurchaseSlotCount);
            }
            Store.OnOpened += RefreshStore;
            Store.OnPurchase += OnStorePurchase;
            if (_inventory != null) _inventory.ItemAdded += OnStoreItemAdded;
            Store.OnSell += OnStoreSell;
            Store.OnSellItemSelected += OnStoreSellItemSelected;
        }

        if (_equipmentUpgradeView != null)
        {
            _equipmentUpgradeView.OnOpened += RefreshEquipmentUpgrade;
            _equipmentUpgradeView.OnUpgradeRequested += OnUpgradeEquipmentRequested;
            if (_equipController != null)
            {
                _equipController.EquipmentCountChanged += _equipmentUpgradeView.SetEquipmentCount;
                _equipController.EquipmentInfoChanged += _equipmentUpgradeView.SetEquipment;
                _equipController.UpgradeGoldChanged += _equipmentUpgradeView.UpdateUpgradeGold;
                _equipController.DamageChanged += _equipmentUpgradeView.UpdateDamage;
                _equipController.RangeChanged += _equipmentUpgradeView.UpdateRange;
                _equipController.MaxOxygenChanged += _equipmentUpgradeView.UpdateMaxOxygen;
                _equipController.MoveSpeedChanged += _equipmentUpgradeView.UpdateMoveSpeed;
                _equipController.DefenseChanged += _equipmentUpgradeView.UpdateDefense;
                _equipController.CarryCapacityChanged += _equipmentUpgradeView.UpdateCarryCapacity;
                _equipController.BulletCountChanged += _equipmentUpgradeView.UpdateBulletCount;
                _equipController.PublishEquipmentCount();
            }
        }

        if (Base != null && CurrentSceneType != SceneType.UnderSea)
            Base.OnSkipTime += OnTimeSkipRequested;

    }

    public override void UnsubscribeSceneEvents()
    {
        if (!EndSceneEventSubscription()) return;
        if (!ReferenceEquals(DiveObject, null)) DiveObject.DiveRequested -= OnDiveRequested;
        if (SushiObject != null && SushiObject.BoatMover != null)
            SushiObject.BoatMover.SceneTransitionRequested -= CompleteDiveTransition;
        if (!ReferenceEquals(SceneBoatNPC, null))
        {
            SceneBoatNPC.HeroDetectionChanged -= OnBoatNPCHeroDetectionChanged;
            SceneBoatNPC.InteractionRequested -= OnBoatNPCInteractionRequested;
        }

        if (!ReferenceEquals(Store, null))
        {
            if (!ReferenceEquals(SceneShop, null))
                SceneShop.SoldOutChanged -= OnShopSoldOutChanged;
            Store.OnOpened -= RefreshStore;
            Store.OnPurchase -= OnStorePurchase;
            if (_inventory != null) _inventory.ItemAdded -= OnStoreItemAdded;
            Store.OnSell -= OnStoreSell;
            Store.OnSellItemSelected -= OnStoreSellItemSelected;
        }

        if (_equipmentUpgradeView != null)
        {
            _equipmentUpgradeView.OnOpened -= RefreshEquipmentUpgrade;
            _equipmentUpgradeView.OnUpgradeRequested -= OnUpgradeEquipmentRequested;
            if (!ReferenceEquals(_equipController, null))
            {
                _equipController.EquipmentCountChanged -= _equipmentUpgradeView.SetEquipmentCount;
                _equipController.EquipmentInfoChanged -= _equipmentUpgradeView.SetEquipment;
                _equipController.UpgradeGoldChanged -= _equipmentUpgradeView.UpdateUpgradeGold;
                _equipController.DamageChanged -= _equipmentUpgradeView.UpdateDamage;
                _equipController.RangeChanged -= _equipmentUpgradeView.UpdateRange;
                _equipController.MaxOxygenChanged -= _equipmentUpgradeView.UpdateMaxOxygen;
                _equipController.MoveSpeedChanged -= _equipmentUpgradeView.UpdateMoveSpeed;
                _equipController.DefenseChanged -= _equipmentUpgradeView.UpdateDefense;
                _equipController.CarryCapacityChanged -= _equipmentUpgradeView.UpdateCarryCapacity;
                _equipController.BulletCountChanged -= _equipmentUpgradeView.UpdateBulletCount;
            }
        }

        if (!ReferenceEquals(Base, null)) Base.OnSkipTime -= OnTimeSkipRequested;

    }

    internal void OnBoatNPCHeroDetectionChanged(Hero hero, bool detected)
    {
        if (hero == _hero) SetBoatNPCNearby(detected);
    }

    internal void SetBoatNPCNearby(bool nearby)
    {
        nearby = nearby && (!IsTutorialActive || _tutorial.AllowsNPC);
        if (_boatNPCInteraction != null) _boatNPCInteraction.SetNearby(nearby);
        else if (_boardView != null) _boardView.SetInteractSelect(nearby);
    }

    internal override void OnNavigateInput(int direction)
    {
        if (IsTutorialActive && !_tutorial.AllowsNPC) return;
        if (_boatNPCInteraction != null) _boatNPCInteraction.Navigate(direction);
    }

    internal void OnBoatNPCInteractionRequested(Hero hero)
    {
        if ((IsTutorialActive && !_tutorial.AllowsNPC) || hero != _hero || _boatNPCInteraction == null) return;
        _boatNPCInteraction.OpenSelectedView();
        if (_boatNPCInteraction.IsViewOpen) _hero.ResetInput();
    }

    internal void RefreshStore()
    {
        if (_storeView == null) return;

        if (_shop != null) _shop.PrepareSaleItems(_storeView.PurchaseSlotCount);
        _storeView.SetPurchaseItems(_shop != null ? _shop.SaleItems : Array.Empty<UtilityItemData>());
        RefreshStoreSellItems();
        _storeView.SetGold(_inventory != null ? _inventory.Gold : 0f);
        if (_shop != null) _shop.PublishSaleItemStates();
    }

    void RefreshStoreSellItems()
    {
        var sellable = new List<Inventory.BagItem>();
        if (_inventory != null)
            foreach (Inventory.BagItem entry in _inventory.Items)
                if (entry.ItemData != null && entry.ItemData.IsSellable)
                    sellable.Add(entry);

        _storeView.SetSellItems(sellable);
    }

    internal void OnShopSoldOutChanged(int index, bool soldOut)
    {
        if (_storeScreenView != null)
            _storeScreenView.SetPurchasePresentation(index, soldOut, string.Empty);
    }

    void OnStoreItemAdded(ItemData item)
    {
        if (item is IngredientItemData && Store != null && Store.isActiveAndEnabled &&
            Store.IsIngredientPurchase && Store.SelectedItem == item)
            GameSfx.Play(GameSoundId.Trade);
    }

    internal void OnStorePurchase(UtilityItemData item, int amount)
    {
        bool succeeded = _shop != null && _inventory != null
            && _shop.TryBuy(item, amount, _inventory);
        UpdateStoreAfterTrade(succeeded);
    }

    internal void OnStoreSell(ItemData item, int amount)
    {
        bool succeeded = _shop != null && _inventory != null
            && _shop.TrySell(item, amount, _inventory);
        UpdateStoreAfterTrade(succeeded);
    }

    internal void OnStoreSellItemSelected(ItemData item)
    {
        if (_storeView == null) return;

        float unitPrice = float.NaN;
        if (_shop != null && _shop.TryGetSellTotal(item, 1, out float price))
            unitPrice = price;
        _storeView.SetSellUnitPrice(item, unitPrice);
    }

    internal void UpdateStoreAfterTrade(bool succeeded)
    {
        if (_inventory != null)
        {
            if (_storeView != null)
            {
                if (succeeded) RefreshStoreSellItems();
                _storeView.SetGold(_inventory.Gold);
            }
            if (_baseView != null) _baseView.UpdateGoldText(_inventory.Gold);
        }
        if (_storeView != null) _storeView.ShowTradeResult(succeeded);
        GameSfx.Play(succeeded ? GameSoundId.Trade : GameSoundId.Rejected);
        RefreshEquipmentUpgrade();
    }

    internal void RefreshEquipmentUpgrade()
    {
        if (_equipmentUpgradeView == null || _equipController == null) return;
        _equipController.PublishEquipmentValues();
        _equipmentUpgradeView.SetGold(_inventory != null ? _inventory.Gold : 0f);
    }

    internal void OnUpgradeEquipmentRequested(int equipType)
    {
        OnUpgradeEquipment((EquipmentData.EquipType)equipType);
    }

    public void OnUpgradeEquipment(EquipmentData.EquipType type)
    {
        bool succeeded = _equipController != null
            && _equipController.TryUpgradeEquipment(type, _inventory);

        RefreshEquipmentUpgrade();
        if (_inventory != null)
        {
            if (_baseView != null) _baseView.UpdateGoldText(_inventory.Gold);
            if (_storeView != null) _storeView.SetGold(_inventory.Gold);
        }
        _equipmentUpgradeView?.ShowUpgradeResult(succeeded);
        GameSfx.Play(succeeded ? GameSoundId.Trade : GameSoundId.Rejected);
    }
}
