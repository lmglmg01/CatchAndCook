using System;
using UnityEngine;

public class PlayerProgress : MonoBehaviour
{
    [Header("성장 데이터")]
    [SerializeField] PlayerProgressData _progressData;

    [Header("현재 성장 상태")]
    [SerializeField, Min(1)] int _level;
    [SerializeField, Min(0f)] float _experience;

    [Header("보트 튜토리얼 진행")]
    [SerializeField] int _boatTutorialStep;
    public int BoatTutorialStep => _boatTutorialStep;
    public void SetBoatTutorialStep(int step) => _boatTutorialStep = Mathf.Clamp(step, 0, 5);

    [Header("잠수 튜토리얼 진행")]
    [SerializeField] int _diveTutorialStep;
    public int DiveTutorialStep => _diveTutorialStep;
    public void SetDiveTutorialStep(int step) => _diveTutorialStep = Mathf.Clamp(step, 0, 7);

    [Header("상점·강화 튜토리얼 진행") ]
    [SerializeField] int _boatCommerceTutorialStep;
    public int BoatCommerceTutorialStep => _boatCommerceTutorialStep;
    public void SetBoatCommerceTutorialStep(int step) => _boatCommerceTutorialStep = Mathf.Clamp(step, 0, 8);

    [Header("초밥집 튜토리얼 진행")]
    [SerializeField] bool _sushiTutorialIngredientsGranted;
    [SerializeField] bool _sushiTutorialUpgradeSuppliesGranted;
    [SerializeField] bool _sushiTutorialStaffUpgradeFundsGranted;
    [SerializeField] bool _sushiTutorialCompleted;
    public bool SushiTutorialIngredientsGranted => _sushiTutorialIngredientsGranted;
    public bool SushiTutorialUpgradeSuppliesGranted => _sushiTutorialUpgradeSuppliesGranted;
    public bool SushiTutorialStaffUpgradeFundsGranted => _sushiTutorialStaffUpgradeFundsGranted;
    public bool SushiTutorialCompleted => _sushiTutorialCompleted;
    internal void MarkSushiTutorialIngredientsGranted() => _sushiTutorialIngredientsGranted = true;
    internal void MarkSushiTutorialUpgradeSuppliesGranted() => _sushiTutorialUpgradeSuppliesGranted = true;
    internal void MarkSushiTutorialStaffUpgradeFundsGranted() => _sushiTutorialStaffUpgradeFundsGranted = true;
    internal void CompleteSushiTutorial() => _sushiTutorialCompleted = true;

    [Header("보스 석문")]
    [SerializeField] bool _bossGateOpened;
    public bool BossGateOpened => _bossGateOpened;

    internal bool TryOpenBossGate(Inventory inventory, System.Collections.Generic.IReadOnlyList<QuestItemData> tablets)
    {
        if (_bossGateOpened || inventory == null) return false;
        _bossGateOpened = true;
        if (inventory.TryRemoveQuestItems(tablets)) return true;
        _bossGateOpened = false;
        return false;
    }

    public int Level => _level;
    public float Experience => _experience;
    public int MaxLevel => _progressData != null ? _progressData.MaxLevel : 0;
    public bool IsMaxLevel => MaxLevel > 0 && _level >= MaxLevel;
    public float RequiredExperience => _progressData != null
        && _progressData.TryGetRequiredExperience(_level, out float required) ? required : 0f;

    public event Action<int> LevelChanged;
    public event Action<float, float> ExperienceChanged;

    void Awake() => ValidateState();

    void OnValidate() => ValidateState();

    void ValidateState()
    {
        _level = Mathf.Max(1, _level);
        if (MaxLevel > 0) _level = Mathf.Min(_level, MaxLevel);
        if (_experience < 0f || float.IsNaN(_experience) || float.IsInfinity(_experience) || IsMaxLevel)
            _experience = 0f;
    }

    internal PlayerProgressSaveData CaptureSave()
        => new PlayerProgressSaveData
        {
            Level = _level,
            Experience = _experience,
            BoatTutorialStep = _boatTutorialStep,
            DiveTutorialStep = _diveTutorialStep,
            BoatCommerceTutorialStep = _boatCommerceTutorialStep,
            SushiTutorialIngredientsGranted = _sushiTutorialIngredientsGranted,
            SushiTutorialUpgradeSuppliesGranted = _sushiTutorialUpgradeSuppliesGranted,
            SushiTutorialStaffUpgradeFundsGranted = _sushiTutorialStaffUpgradeFundsGranted,
            SushiTutorialCompleted = _sushiTutorialCompleted,
            BossGateOpened = _bossGateOpened
        };

    internal bool CanRestoreSave(PlayerProgressSaveData save)
    {
        if (save == null || _progressData == null || save.Level < 1 || save.Level > MaxLevel
            || save.Experience < 0f || float.IsNaN(save.Experience)
            || float.IsInfinity(save.Experience)) return false;
        if (save.Level == MaxLevel) return save.Experience == 0f;
        return _progressData.TryGetRequiredExperience(save.Level, out float required)
            && save.Experience < required;
    }

    internal void RestoreSave(PlayerProgressSaveData save)
    {
        _level = save.Level;
        _experience = save.Experience;
        SetBoatTutorialStep(save.BoatTutorialStep);
        SetDiveTutorialStep(save.DiveTutorialStep);
        SetBoatCommerceTutorialStep(save.BoatCommerceTutorialStep);
        _sushiTutorialIngredientsGranted = save.SushiTutorialIngredientsGranted;
        _sushiTutorialUpgradeSuppliesGranted = save.SushiTutorialUpgradeSuppliesGranted;
        _sushiTutorialStaffUpgradeFundsGranted = save.SushiTutorialStaffUpgradeFundsGranted;
        _sushiTutorialCompleted = save.SushiTutorialCompleted;
        _bossGateOpened = save.BossGateOpened;
        PublishProgress();
    }

    // 필요 경험치를 차감하고 남은 경험치를 유지하며 여러 레벨을 올릴 수 있다.
    public bool AddExperience(float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount) || _progressData == null
            || _level < 1 || MaxLevel < 1 || IsMaxLevel) return false;

        int nextLevel = _level;
        double experience = (double)_experience + amount;
        while (nextLevel < MaxLevel)
        {
            if (!_progressData.TryGetRequiredExperience(nextLevel, out float required)) return false;
            if (experience < required) break;
            experience -= required;
            nextLevel++;
        }
        if (nextLevel >= MaxLevel) experience = 0d;
        if (experience > float.MaxValue) return false;

        bool levelChanged = nextLevel != _level;
        _level = nextLevel;
        _experience = (float)experience;
        if (levelChanged) LevelChanged?.Invoke(_level);
        ExperienceChanged?.Invoke(_experience, RequiredExperience);
        return true;
    }

    // 기존 직접 레벨업 기능을 플레이어 성장 관리로 옮긴다.
    public void LevelUp()
    {
        if (_progressData == null || _level < 1 || MaxLevel < 1 || IsMaxLevel) return;
        _level++;
        _experience = 0f;
        PublishProgress();
    }

    public void PublishProgress()
    {
        LevelChanged?.Invoke(_level);
        ExperienceChanged?.Invoke(_experience, RequiredExperience);
    }

    [ContextMenu("테스트/레벨업")]
    public void TestLevelUp()
    {
        if (Application.isPlaying) LevelUp();
    }
}
