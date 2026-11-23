using System;
using System.Collections.Generic;
using UnityEngine;

// 기존 View의 공개 API와 게임 상태를 연결한다. View/프리팹에는 런타임 참조를 기록하지 않는다.
internal sealed class GameplayViewConnector : IDisposable
{
    readonly GameManager _manager;
    readonly PlayScene _scene;
    readonly InteractableSensor _diveSensor;
    readonly UnityEngine.UI.Button _diveButton;
    bool _diveWasNearby;
    InteractObject _holdInteraction;
    readonly InventoryView _inventoryView;
    readonly QuestListView _questView;
    readonly PhoneNavigationView _phoneNavigation;
    readonly PlayerStatusView _statusView;
    readonly DiveCharacterView _diveCharacterView;
    readonly TreasureView _treasureView;
    readonly List<TMPro.TMP_Text> _timeTexts = new();
    readonly List<TMPro.TMP_Text> _dateTexts = new();
    readonly List<UnityEngine.UI.Image> _timeImages = new();
    readonly GroundHero _groundHero;
    readonly ViewActivationRelay _inventoryActivation;

    public GameplayViewConnector(PlayScene scene, GameManager manager)
    {
        _manager = manager;
        _scene = scene;
        if (scene.SceneHero is GroundHero && (scene.DiveObject != null || scene.SushiObject != null) && scene.Board != null)
        {
            _diveSensor = scene.SceneHero.GetComponentInChildren<InteractableSensor>(true);
            foreach (UnityEngine.UI.Button button in scene.Board.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.gameObject.name == "Interact_Dive")
                {
                    _diveButton = button;
                    _diveButton.onClick.AddListener(OnDiveButtonClicked);
                    break;
                }
        }
        foreach (GameObject root in scene.gameObject.scene.GetRootGameObjects())
        {
            if (_inventoryView == null) _inventoryView = root.GetComponentInChildren<InventoryView>(true);
            if (_phoneNavigation == null) _phoneNavigation = root.GetComponentInChildren<PhoneNavigationView>(true);
            if (_questView == null) _questView = root.GetComponentInChildren<QuestListView>(true);
            if (_statusView == null) _statusView = root.GetComponentInChildren<PlayerStatusView>(true);
            if (_diveCharacterView == null) _diveCharacterView = root.GetComponentInChildren<DiveCharacterView>(true);
            if (_treasureView == null) _treasureView = root.GetComponentInChildren<TreasureView>(true);
            foreach (BaseView baseView in root.GetComponentsInChildren<BaseView>(true))
            {
                foreach (TMPro.TMP_Text label in baseView.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (label.gameObject.name == "TimeText") _timeTexts.Add(label);
                    else if (label.gameObject.name == "DateText") _dateTexts.Add(label);
                }
                foreach (UnityEngine.UI.Image icon in baseView.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if (icon.gameObject.name == "TimeImage") _timeImages.Add(icon);
            }
        }

        if (_phoneNavigation != null)
        {
            _phoneNavigation.ReturnToTitleRequested += ReturnToTitle;
            _phoneNavigation.QuitRequested += QuitGame;
        }
        if (_statusView != null) _statusView.BindCharacter(scene.SceneHero);
        if (scene.CurrentSceneType == SceneType.UnderSea && scene.SceneHero is UnderSeaHero diveHero)
        {
            _diveCharacterView?.Bind(diveHero, scene.RunModel, manager.Inventory);
            _treasureView?.Bind(diveHero.FishBag);
        }
        if (_inventoryView != null)
            _inventoryView.OnUtilityEquipmentChanged += OnUtilityEquipmentChanged;
        if (_questView != null)
        {
            _questView.OnQuestAccepted += OnQuestAccepted;
            _questView.OnQuestCompleted += OnQuestCompleted;
        }
        manager.DayPhaseChanged += RefreshDayPhase;
        RefreshDayPhase(manager.CurrentDayPhase);

        if (manager.Inventory != null) manager.Inventory.Changed += RefreshInventory;
        if (manager.QuestController != null)
        {
            manager.QuestController.QuestAccepted += OnQuestChanged;
            manager.QuestController.QuestProgressChanged += OnQuestProgressChanged;
            manager.QuestController.QuestClaimed += OnQuestChanged;
        }
        if (manager.EquipController != null)
            manager.EquipController.EquipmentInfoChanged += OnEquipmentInfoChanged;
        if (manager.PlayerProgress != null)
        {
            manager.PlayerProgress.LevelChanged += OnLevelChanged;
            manager.PlayerProgress.ExperienceChanged += OnExperienceChanged;
        }

        if (_inventoryView != null)
        {
            _inventoryActivation = _inventoryView.GetComponent<ViewActivationRelay>();
            if (_inventoryActivation == null)
                _inventoryActivation = _inventoryView.gameObject.AddComponent<ViewActivationRelay>();
            _inventoryActivation.Opened += RefreshInventory;
        }
        if (_diveSensor != null && scene.SceneHero is GroundHero groundHero)
        {
            _groundHero = groundHero;
            _groundHero.InteractionTargetChanged += OnInteractionTargetChanged;
            OnInteractionTargetChanged(_groundHero.InteractionTarget);
        }
        RefreshInventory();
        RefreshQuests();
        RefreshEquipment();
        RefreshPlayerStatus();
    }

    void OnInteractionTargetChanged(IInteractable target)
    {
        _holdInteraction = target != null && (ReferenceEquals(target, _scene.DiveObject)
            || ReferenceEquals(target, _scene.SushiObject)) ? target as InteractObject : null;
        bool nearby = _holdInteraction != null;
        if (nearby == _diveWasNearby) return;
        _diveWasNearby = nearby;
        if (_scene.Board != null) _scene.Board.SetDiveIcon(nearby);
    }
    public void Dispose()
    {
        if (!ReferenceEquals(_phoneNavigation, null))
        {
            _phoneNavigation.ReturnToTitleRequested -= ReturnToTitle;
            _phoneNavigation.QuitRequested -= QuitGame;
        }
        if (!ReferenceEquals(_groundHero, null))
            _groundHero.InteractionTargetChanged -= OnInteractionTargetChanged;
        if (!ReferenceEquals(_inventoryActivation, null))
            _inventoryActivation.Opened -= RefreshInventory;
        if (_diveCharacterView != null) _diveCharacterView.Unbind();
        if (_treasureView != null) _treasureView.Unbind();
        if (_statusView != null) _statusView.BindCharacter(null);
        if (_diveButton != null)
            _diveButton.onClick.RemoveListener(OnDiveButtonClicked);
        if (_scene.Board != null) _scene.Board.SetDiveIcon(false);
        if (!ReferenceEquals(_inventoryView, null))
            _inventoryView.OnUtilityEquipmentChanged -= OnUtilityEquipmentChanged;
        if (!ReferenceEquals(_questView, null))
        {
            _questView.OnQuestAccepted -= OnQuestAccepted;
            _questView.OnQuestCompleted -= OnQuestCompleted;
        }
        _manager.DayPhaseChanged -= RefreshDayPhase;
        if (!ReferenceEquals(_manager.Inventory, null))
            _manager.Inventory.Changed -= RefreshInventory;
        if (!ReferenceEquals(_manager.QuestController, null))
        {
            _manager.QuestController.QuestAccepted -= OnQuestChanged;
            _manager.QuestController.QuestProgressChanged -= OnQuestProgressChanged;
            _manager.QuestController.QuestClaimed -= OnQuestChanged;
        }
        if (!ReferenceEquals(_manager.EquipController, null))
            _manager.EquipController.EquipmentInfoChanged -= OnEquipmentInfoChanged;
        if (!ReferenceEquals(_manager.PlayerProgress, null))
        {
            _manager.PlayerProgress.LevelChanged -= OnLevelChanged;
            _manager.PlayerProgress.ExperienceChanged -= OnExperienceChanged;
        }
    }

    void ReturnToTitle()
    {
        if (_scene._diveSceneLoading || !GameSaveService.TryReturnToTitle())
            _phoneNavigation.ResetPresentation();
    }

    static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnDiveButtonClicked()
    {
        if (_diveWasNearby && _holdInteraction != null && _holdInteraction.CanInteract(_scene.SceneHero)
            && ReferenceEquals(_diveSensor.FindNearestInteractable(_scene.SceneHero), _holdInteraction))
            _holdInteraction.Interact(_scene.SceneHero);
    }

    void RefreshDayPhase(DayPhase phase)
    {
        foreach (TMPro.TMP_Text label in _timeTexts)
            if (label != null) label.text = phase.ToString();
        float fill = phase == DayPhase.Morning ? 0.35f
            : phase == DayPhase.Afternoon ? 0.75f : 1f;
        foreach (UnityEngine.UI.Image icon in _timeImages)
            if (icon != null) icon.fillAmount = fill;
        foreach (TMPro.TMP_Text label in _dateTexts)
            if (label != null) label.text = _manager.CurrentDate;
    }

    void RefreshInventory()
    {
        Inventory inventory = _manager.Inventory;
        if (inventory == null) return;
        if (_inventoryView != null)
        {
            _inventoryView.SetItems(inventory.Items);
            _inventoryView.SetUtilityEquipment(inventory.FirstUtility, inventory.SecondUtility);
        }

        PlayScene scene = _manager.CurrentPlayScene;
        if (scene == null) return;
        if (scene.Base != null && scene.CurrentSceneType == SceneType.Ground)
            scene.Base.UpdateGoldText(inventory.Gold);
        if (scene.Store != null && scene.Store.isActiveAndEnabled)
            scene.Store.SetGold(inventory.Gold);
        if (scene.UpgradeView != null && scene.UpgradeView.isActiveAndEnabled)
            scene.UpgradeView.SetGold(inventory.Gold);
    }

    void OnUtilityEquipmentChanged(UtilityItemData first, UtilityItemData second)
    {
        Inventory inventory = _manager.Inventory;
        if (inventory != null && !inventory.TrySetUtilityEquipment(first, second))
            RefreshInventory();
    }

    // 선상 진입 시 현재 레벨에서 열리는 메인 묶음 하나를 자동 수락합니다.
    // 진행 중인 메인 묶음은 QuestController가 중복 수락을 막습니다.
    public void TryAcceptMainQuestAtBoatEntry(bool suppressDialogue = false)
    {
        if (_scene.CurrentSceneType != SceneType.Ground
            || (_scene.gameObject.scene.name != "MorningBoat"
                && _scene.gameObject.scene.name != "AfterNoonBoat")) return;
        QuestController controller = _manager.QuestController;
        if (controller == null || controller.DataMap == null) return;
        var eligible = new List<QuestGroupData>();
        foreach (QuestGroupData group in controller.DataMap.Groups)
            if (group != null && group.Type == QuestGroupData.QuestType.Main
                && group.Quests.Count > 0 && controller.IsGroupUnlocked(group))
                eligible.Add(group);
        eligible.Sort((a, b) => a.RequiredLevel.CompareTo(b.RequiredLevel));
        foreach (QuestGroupData group in eligible)
        {
            if (controller.GetGroupProgress(group.Type, group.GroupId) != null) continue;
            if (!controller.AcceptQuestGroup(group.Type, group.GroupId)) continue;
            if (!suppressDialogue) PlayAcceptDialogue(group.Quests[0]);
            break;
        }
    }

    void PlayAcceptDialogue(QuestData quest)
    {
        QuestDialogue dialogue = quest != null ? quest.AcceptDialogue : null;
        TalkView talk = _scene.Base != null ? _scene.Base.TalkView : null;
        if (dialogue == null || !dialogue.HasLines || talk == null) return;
        var lines = new TalkView.TalkLine[dialogue.Lines.Length];
        for (int i = 0; i < lines.Length; i++)
            lines[i] = new TalkView.TalkLine(dialogue.Lines[i].PlayerSpeaks ? 0 : 1,
                dialogue.Lines[i].Content);
        talk.BeginTalk(dialogue.PlayerName, dialogue.NpcName, lines,
            dialogue.PlayerIllustration, dialogue.NpcIllustration);
    }

    void RefreshQuests()
    {
        QuestController controller = _manager.QuestController;
        if (_questView == null || controller == null || controller.DataMap == null) return;

        var display = new List<QuestListView.QuestData>();
        var seen = new HashSet<string>();
        int level = _manager.PlayerProgress != null ? _manager.PlayerProgress.Level : 1;
        foreach (QuestGroupData group in controller.DataMap.Groups)
        {
            if (group == null || group.RequiredLevel > level) continue;
            // Main quests enter the To Do list only after the boat scene accepts the group.
            if (group.Type == QuestGroupData.QuestType.Main
                && controller.GetGroupProgress(group.Type, group.GroupId) == null) continue;
            QuestListView.QuestKind kind = group.Type == QuestGroupData.QuestType.Main
                ? QuestListView.QuestKind.Main
                : group.Type == QuestGroupData.QuestType.ContentUnlock
                    ? QuestListView.QuestKind.Content : QuestListView.QuestKind.Side;
            for (int i = 0; i < group.Quests.Count; i++)
            {
                QuestData data = group.Quests[i];
                if (i > 0 && (data == null || controller.GetQuestProgress(data.QuestId) == null)) continue;
                AddQuest(data, kind, controller, display, seen);
            }
        }
        foreach (QuestData data in controller.DataMap.Quests)
            if (data != null && data.RequiredLevel <= level
                && controller.DataMap.GetGroupContainingQuest(data.QuestId) == null)
                AddQuest(data, QuestListView.QuestKind.Side, controller, display, seen);
        _questView.SetQuests(display);
    }

    void AddQuest(QuestData data, QuestListView.QuestKind kind, QuestController controller,
        List<QuestListView.QuestData> display, HashSet<string> seen)
    {
        if (data == null || string.IsNullOrEmpty(data.QuestId) || !seen.Add(data.QuestId)) return;
        QuestController.QuestProgress progress = controller.GetQuestProgress(data.QuestId);
        int required = progress != null ? progress.RequiredCount : controller.GetRequiredCount(data);
        if (required <= 0) return;
        string objective = ObjectiveText(data);
        bool canEarnExperience = _manager.PlayerProgress != null && !_manager.PlayerProgress.IsMaxLevel;
        display.Add(new QuestListView.QuestData
        {
            id = data.QuestId,
            title = data.QuestName,
            kind = kind,
            description = objective,
            accepted = progress != null,
            completed = progress != null && progress.IsClaimed,
            isNew = progress == null,
            objectives = new[]
            {
                new QuestListView.Objective
                {
                    description = objective,
                    current = progress != null ? progress.CurrentCount : 0,
                    target = required
                }
            },
            rewards = BuildQuestRewards(data, controller.DataMap.GoldRewardIcon, canEarnExperience)
        });
    }

    static QuestListView.Reward[] BuildQuestRewards(QuestData data, Sprite goldIcon, bool canEarnExperience)
    {
        var rewards = new List<QuestListView.Reward>(4);
        if (data.RewardGold > 0)
        {
            rewards.Add(new QuestListView.Reward
            {
                type = QuestRewardType.Gold,
                amount = data.RewardGold,
                icon = goldIcon
            });
        }

        AddQuestItemReward(rewards, data.RewardItem, data.RewardItemCount);
        AddQuestItemReward(rewards, data.RewardQuestItem, 1);

        if (canEarnExperience && data.RewardExperience > 0)
        {
            rewards.Add(new QuestListView.Reward
            {
                type = QuestRewardType.Exp,
                amount = data.RewardExperience
            });
        }

        return rewards.ToArray();
    }

    static void AddQuestItemReward(List<QuestListView.Reward> rewards, ItemData item, int count)
    {
        if (item == null || count <= 0) return;
        rewards.Add(new QuestListView.Reward
        {
            type = QuestRewardType.Item,
            name = item.ItemName,
            amount = count,
            icon = item.ItemIcon
        });
    }

    static string ObjectiveText(QuestData data)
    {
        switch (data)
        {
            case FishCaptureQuestData fish:
                return fish.TargetFish != null ? fish.TargetFish.FishName + " 포획" : "물고기 포획";
            case KillQuestData kill: return kill.QuestName;
            case ItemCollectQuestData item:
                return item.TargetItem != null ? item.TargetItem.ItemName + " 수집" : "아이템 수집";
            case ExploreQuestData area: return area.QuestName;
            case ObjectInteractQuestData obj: return obj.QuestName;
            default: return data.QuestName;
        }
    }

    void OnQuestAccepted(QuestListView.QuestData data)
    {
        QuestController controller = _manager.QuestController;
        if (data == null || controller == null || controller.DataMap == null) return;
        QuestData quest = controller.DataMap.GetQuestData(data.id);
        QuestGroupData group = controller.DataMap.GetGroupContainingQuest(data.id);
        // 메인은 선상 입장 시 자동 수락합니다. 할 일에서는 서브·해금 퀘스트만 수락합니다.
        if (group == null || group.Type != QuestGroupData.QuestType.Main)
            if (controller.AcceptQuest(data.id)) PlayAcceptDialogue(quest);
        RefreshQuests();
    }

    void OnQuestCompleted(QuestListView.QuestData data)
    {
        _manager.QuestController?.TryClaimQuest(data.id);
        RefreshQuests();
    }

    void OnQuestChanged(QuestController.QuestProgress _) => RefreshQuests();

    void OnQuestProgressChanged(QuestController.QuestProgress progress)
    {
        if (_questView != null)
            _questView.SetObjectiveProgress(progress.Data.QuestId, 0, progress.CurrentCount);
    }

    void RefreshEquipment()
    {
        EquipController controller = _manager.EquipController;
        if (_inventoryView == null || controller == null) return;
        foreach (EquipmentData data in controller.EquipmentDatas)
        {
            if (data == null) continue;
            int index = (int)data.Type;
            int level = index < controller.EquipmentLevels.Count ? controller.EquipmentLevels[index] : 0;
            SetEquipment(data.Type, data.EquipName, data.EquipDescription, level);
        }
    }

    void OnEquipmentInfoChanged(int type, string name, string description, int level, int maxLevel, bool isMaxLevel)
        => SetEquipment((EquipmentData.EquipType)type, name, description, level);

    void SetEquipment(EquipmentData.EquipType type, string name, string description, int level)
    {
        if (_inventoryView == null) return;
        int slot;
        string stats;
        EquipController controller = _manager.EquipController;
        switch (type)
        {
            case EquipmentData.EquipType.Knife:
                slot = 0; stats = $"공격력 {controller.GetKnifeDamage():0.##} / 사거리 {controller.GetKnifeRange():0.##}"; break;
            case EquipmentData.EquipType.Gig:
                slot = 1; stats = $"공격력 {controller.GetGigDamage():0.##} / 사거리 {controller.GetGigRange():0.##}"; break;
            case EquipmentData.EquipType.Gun:
                slot = 2; stats = $"공격력 {controller.GetGunDamage():0.##} / 탄약 {controller.GetGunBulletCount()}"; break;
            case EquipmentData.EquipType.OxygenCylinder:
                slot = 3; stats = $"최대 산소 {controller.GetMaxOxygen():0.##}"; break;
            case EquipmentData.EquipType.SwimSuit:
                slot = 4; stats = $"이동속도 {controller.GetSwimSuitMoveSpeed():0.##} / 방어력 {controller.GetSwimSuitDefense():0.##}"; break;
            case EquipmentData.EquipType.FishBag:
                slot = 5; stats = $"적재량 {controller.GetFishBagCapacity():0.##}"; break;
            default: return;
        }
        int index = (int)type;
        EquipmentData data = index < controller.EquipmentDatas.Count ? controller.EquipmentDatas[index] : null;
        Sprite icon = data != null && data.Type == type ? data.EquipIcon : null;
        _inventoryView.SetEquipment(slot, icon, name, description, stats, level);
    }

    void RefreshPlayerStatus()
    {
        PlayerProgress progress = _manager.PlayerProgress;
        if (_statusView == null) return;
        _statusView.SetPlayerName(_manager.PlayerName);
        if (progress == null) return;
        OnLevelChanged(progress.Level);
        OnExperienceChanged(progress.Experience, progress.RequiredExperience);
    }

    void OnLevelChanged(int level)
    {
        if (_statusView != null) _statusView.SetLevel(level);
        RefreshQuests();
    }

    void OnExperienceChanged(float current, float required)
    {
        if (_statusView != null)
            _statusView.SetProgress(required > 0f ? current / required : 1f);
    }
}



