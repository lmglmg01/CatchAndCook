using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct SaveSlotInfo
{
    public readonly int Slot;
    public readonly bool Exists;
    public readonly bool CanLoad;
    public readonly string SceneName;
    public readonly string PlayerName;
    public readonly DayPhase Phase;
    public readonly int DayNumber;
    public readonly int Level;
    public readonly long LastPlayedUtcTicks;
    public readonly string Error;

    public SaveSlotInfo(int slot, bool exists, bool canLoad, string sceneName,
        int dayNumber, int level, long lastPlayedUtcTicks, string error, string playerName = null, DayPhase phase = DayPhase.Morning)
    {
        Slot = slot;
        Exists = exists;
        CanLoad = canLoad;
        SceneName = sceneName;
        PlayerName = string.IsNullOrWhiteSpace(playerName) ? GameManager.DefaultPlayerName : playerName;
        DayNumber = dayNumber;
        Phase = phase;
        Level = level;
        LastPlayedUtcTicks = lastPlayedUtcTicks;
        Error = error;
    }
}

// One JSON file per slot. Dive progress is saved at scene entry and when the boss gate opens.
public static class GameSaveService
{
    public const int MaxSlots = 5;
    public const int MaxPlayerNameLength = 6;
    const int LegacyMaxPlayerNameLength = 12;
    const string DefaultNewPlayerName = "다이버";
    const string LegacyFileName = "save.json";
    const string LegacyMigrationMarker = "legacy-migration.done";
    static GameSaveData _pending;
    static string _newPlayerName;
    static ShopSaveData _pendingShop;
    static string _pendingScene;
    static bool _restoreFailed;
    static bool _migrationChecked;
    static int _activeSlot;
    static int _lastSavedSceneHandle = -1;

    public static int ActiveSlot => _activeSlot;

    // 저장 데이터 준비가 끝나면 현재 씬의 로더가 전환 연출을 담당합니다.
    public static event Action<string> SceneLoadRequested;

    public static string GetSlotPath(int slot)
    {
        if (!IsValidSlot(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        return Path.Combine(Application.persistentDataPath, $"slot_{slot}.json");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntimeState()
    {
        ClearActiveSlot();
        _migrationChecked = false;
        SceneLoadRequested = null;
    }

    static void ClearActiveSlot()
    {
        _pending = null;
        _newPlayerName = null;
        _pendingShop = null;
        _pendingScene = null;
        _restoreFailed = false;
        _activeSlot = 0;
        _lastSavedSceneHandle = -1;
    }

    public static SaveSlotInfo GetSlotInfo(int slot)
    {
        if (!IsValidSlot(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
        EnsureLegacyMigrated();
        string path = GetSlotPath(slot);
        bool exists = File.Exists(path) || File.Exists(path + ".bak");
        if (!exists) return new SaveSlotInfo(slot, false, false, null, 0, 0, 0, null);
        if (!TryReadSlotFile(slot, out GameSaveData save, out string error, out string source))
            return new SaveSlotInfo(slot, true, false, null, 0, 0, 0, error);
        bool available = Application.CanStreamedLevelBeLoaded(save.SceneName);
        long played = save.LastPlayedUtcTicks > 0 ? save.LastPlayedUtcTicks
            : File.GetLastWriteTimeUtc(source).Ticks;
        return new SaveSlotInfo(slot, true, available, save.SceneName, save.DayNumber,
            save.Player.Level, played, available ? null : "저장된 씬이 빌드 목록에 없습니다.", save.PlayerName, save.DayPhase);
    }

    public static bool HasAnySave
    {
        get
        {
            for (int slot = 1; slot <= MaxSlots; slot++)
                if (GetSlotInfo(slot).CanLoad) return true;
            return false;
        }
    }

    public static bool TryStartNewGame(out string error)
        => TryStartNewGame(DefaultNewPlayerName, out error);

    public static bool TryValidatePlayerName(string value, out string playerName, out string error)
        => TryValidatePlayerName(value, MaxPlayerNameLength, out playerName, out error);

    static bool TryValidatePlayerName(string value, int maximumLength, out string playerName, out string error)
    {
        playerName = value?.Trim();
        error = null;
        if (string.IsNullOrWhiteSpace(playerName)) error = "플레이어 이름을 입력해주세요.";
        else if (playerName.Length > maximumLength) error = $"이름은 최대 {maximumLength}자까지 입력할 수 있습니다.";
        else if (System.Linq.Enumerable.Any(playerName, char.IsControl)) error = "이름에 줄바꿈이나 제어 문자를 사용할 수 없습니다.";
        return error == null;
    }

    public static bool TryStartNewGame(string playerName, out string error)
    {
        if (!TryValidatePlayerName(playerName, out string normalizedName, out error)) return false;
        EnsureLegacyMigrated();
        for (int slot = 1; slot <= MaxSlots; slot++)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path) && !File.Exists(path + ".bak"))
                return TryStartEmptySlot(slot, normalizedName, out error);
        }
        error = "빈 저장 슬롯이 없습니다.";
        return false;
    }

    public static bool TryStartEmptySlot(int slot, out string error)
        => TryStartEmptySlot(slot, DefaultNewPlayerName, out error);

    public static bool TryStartEmptySlot(int slot, string playerName, out string error)
    {
        if (!TryValidatePlayerName(playerName, out string normalizedName, out error)) return false;
        if (!IsValidSlot(slot)) { error = "슬롯 번호가 올바르지 않습니다."; return false; }
        EnsureLegacyMigrated();
        string path = GetSlotPath(slot);
        if (File.Exists(path) || File.Exists(path + ".bak"))
        {
            error = "이미 저장 데이터가 있는 슬롯입니다.";
            return false;
        }
        if (!Application.CanStreamedLevelBeLoaded("TutorialMorningBoat"))
        {
            error = "TutorialMorningBoat 씬이 빌드 목록에 없습니다.";
            return false;
        }
        _activeSlot = slot;
        _pending = null;
        _newPlayerName = null;
        _pendingShop = null;
        _pendingScene = null;
        _restoreFailed = false;
        _lastSavedSceneHandle = -1;
        _newPlayerName = normalizedName;
        if (GameManager.Instance != null) GameManager.Instance.EndSession();
        RequestSceneLoad("TutorialMorningBoat");
        return true;
    }

    public static bool TryLoadSlot(int slot, out string error)
    {
        error = null;
        if (!IsValidSlot(slot)) { error = "슬롯 번호가 올바르지 않습니다."; return false; }
        EnsureLegacyMigrated();
        if (!TryReadSlotFile(slot, out GameSaveData save, out error, out _)) return false;
        if (!Application.CanStreamedLevelBeLoaded(save.SceneName))
        {
            error = "저장된 씬이 빌드 목록에 없습니다: " + save.SceneName;
            return false;
        }
        _activeSlot = slot;
        _pending = save;
        _newPlayerName = null;
        _lastSavedSceneHandle = -1;
        _pendingShop = null;
        _pendingScene = save.SceneName;
        _restoreFailed = false;
        if (GameManager.Instance != null) GameManager.Instance.EndSession();
        RequestSceneLoad(save.SceneName);
        return true;
    }

    internal static bool TryReturnToTitle()
    {
        const string titleScene = "StartScene";
        if (!Application.CanStreamedLevelBeLoaded(titleScene))
        {
            Debug.LogError("시작 화면을 열 수 없습니다. 빌드 목록의 StartScene을 확인해주세요.");
            return false;
        }

        ClearActiveSlot();
        if (GameManager.Instance != null) GameManager.Instance.EndSession();
        RequestSceneLoad(titleScene);
        return true;
    }

    static void RequestSceneLoad(string sceneName)
    {
        if (SceneLoadRequested != null) SceneLoadRequested.Invoke(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    public static SaveSlotInfo GetLatestSlotInfo()
    {
        SaveSlotInfo latest = default;
        long latestTime = long.MinValue;
        for (int slot = 1; slot <= MaxSlots; slot++)
        {
            SaveSlotInfo info = GetSlotInfo(slot);
            if (!info.CanLoad || info.LastPlayedUtcTicks <= latestTime) continue;
            latest = info;
            latestTime = info.LastPlayedUtcTicks;
        }
        return latest;
    }

    public static bool TryContinue(out string error)
    {
        SaveSlotInfo latest = GetLatestSlotInfo();
        if (latest.CanLoad) return TryLoadSlot(latest.Slot, out error);
        error = "이어할 수 있는 저장 데이터가 없습니다.";
        return false;
    }

    public static bool TryDeleteSlot(int slot, out string error)
    {
        error = null;
        if (!IsValidSlot(slot)) { error = "슬롯 번호가 올바르지 않습니다."; return false; }
        string path = GetSlotPath(slot);
        if (!File.Exists(path) && !File.Exists(path + ".bak"))
        {
            error = "삭제할 저장 데이터가 없습니다.";
            return false;
        }
        try
        {
            string legacy = Path.Combine(Application.persistentDataPath, LegacyFileName);
            if (File.Exists(legacy) || File.Exists(legacy + ".bak"))
                File.WriteAllText(Path.Combine(Application.persistentDataPath, LegacyMigrationMarker), "1");
            File.Delete(path + ".tmp");
            File.Delete(path + ".bak");
            File.Delete(path);
            if (_activeSlot == slot)
            {
                _activeSlot = 0;
                _pending = null;
                _pendingShop = null;
                _pendingScene = null;
                _newPlayerName = null;
                _lastSavedSceneHandle = -1;
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }

    public static void RestorePending(GameManager game)
    {
        if (_pending == null)
        {
            if (_newPlayerName != null) game.SetPlayerName(_newPlayerName);
            _newPlayerName = null;
            return;
        }
        GameSaveData save = _pending;
        _pending = null;
        Inventory inventory = game.Inventory;
        EquipController equipment = game.EquipController;
        PlayerProgress player = game.PlayerProgress;
        QuestController quests = game.QuestController;
        SushiShopProgress sushi = game.GetComponent<SushiShopProgress>();
        bool valid = inventory != null && equipment != null && player != null && quests != null
            && inventory.CanRestoreSave(save.Inventory)
            && equipment.CanRestoreSave(save.Equipment)
            && player.CanRestoreSave(save.Player)
            && quests.CanRestoreSave(save.Quests, save.Player.Level)
            && ValidateSushiJson(save.SushiShopJson, sushi);
        if (!valid)
        {
            _restoreFailed = true;
            Debug.LogError("저장 데이터를 복원할 수 없습니다. 기존 슬롯 파일은 유지합니다.");
            return;
        }

        inventory.RestoreSave(save.Inventory);
        equipment.RestoreSave(save.Equipment);
        player.RestoreSave(save.Player);
        quests.RestoreSave(save.Quests, inventory);
        if (sushi != null && !string.IsNullOrEmpty(save.SushiShopJson))
            sushi.TryImportJson(save.SushiShopJson);
        SettingPreferencesView.RestoreKeyBindings(save.KeyBindingsJson);
        game.SetPlayerName(save.PlayerName);
        game.RestoreDayState(save.DayNumber, save.DayPhase);
        // JsonUtility restores a missing inline Shop object as empty lists.
        _pendingShop = save.Shop?.SaleItemIds?.Count > 0 || save.Shop?.SoldOut?.Count > 0
            ? save.Shop
            : null;
        _pendingScene = save.SceneName;
    }

    public static void RestoreShopAtSceneEntry(PlayScene scene)
    {
        if (_pendingShop == null || scene.SceneShop == null) return;
        if (scene.gameObject.scene.name != _pendingScene
            || !scene.SceneShop.TryRestoreSave(_pendingShop, scene.Store.PurchaseSlotCount))
        {
            _restoreFailed = true;
            Debug.LogError("저장된 상점 목록을 복원할 수 없습니다. 기존 슬롯 파일은 유지합니다.");
        }
        _pendingShop = null;
    }

    public static void SaveAtSceneEntry(GameManager game, Scene scene)
    {
        if (scene.handle != _lastSavedSceneHandle) SaveScene(game, scene);
    }

    internal static void SaveBossGateOpened(GameManager game, Scene scene)
    {
        if (game.CurrentPlayScene != null && game.CurrentPlayScene.gameObject.scene == scene)
            SaveScene(game, scene);
    }

    internal static void SaveSushiTutorialProgress(GameManager game)
    {
        if (game.CurrentPlayScene is SushiPlayScene scene) SaveScene(game, scene.gameObject.scene);
    }

    static void SaveScene(GameManager game, Scene scene)
    {
        if (!IsValidSlot(_activeSlot) || _restoreFailed || scene.name == "StartScene") return;
        if (_pendingShop != null)
        {
            _restoreFailed = true;
            Debug.LogError("저장된 상점을 현재 씬에서 찾지 못했습니다. 기존 슬롯 파일은 유지합니다.");
            return;
        }
        if (game.Inventory == null || game.EquipController == null
            || game.PlayerProgress == null || game.QuestController == null) return;

        PlayScene playScene = game.CurrentPlayScene;
        var save = new GameSaveData
        {
            SceneName = scene.name,
            PlayerName = game.PlayerName,
            DayNumber = game.DayNumber,
            DayPhase = game.CurrentDayPhase,
            LastPlayedUtcTicks = DateTime.UtcNow.Ticks,
            Inventory = game.Inventory.CaptureSave(),
            Equipment = game.EquipController.CaptureSave(),
            Player = game.PlayerProgress.CaptureSave(),
            Quests = game.QuestController.CaptureSave(),
            Shop = playScene != null && playScene.SceneShop != null
                ? playScene.SceneShop.CaptureSave() : null,
            SushiShopJson = game.GetComponent<SushiShopProgress>()?.ExportJson(),
            KeyBindingsJson = SettingPreferencesView.CaptureKeyBindings()
        };
        if (TryWriteSlot(_activeSlot, save, out string error))
        {
            _lastSavedSceneHandle = scene.handle;
            _pendingScene = null;
        }
        else Debug.LogError("자동 저장 실패: " + error);
    }

    public static void UpdateActiveKeyBindings(string json)
    {
        if (!IsValidSlot(_activeSlot) || _restoreFailed) return;
        if (!TryReadSlotFile(_activeSlot, out GameSaveData save, out _, out _)) return;
        save.KeyBindingsJson = json;
        if (!TryWriteSlot(_activeSlot, save, out string error))
            Debug.LogWarning("키 설정 저장 실패: " + error);
    }

    static bool TryReadSlotFile(int slot, out GameSaveData save, out string error, out string source)
    {
        source = GetSlotPath(slot);
        if (TryReadFile(source, out save, out error)) return true;
        string primaryError = error;
        source += ".bak";
        if (TryReadFile(source, out save, out error)) return true;
        error = primaryError;
        return false;
    }

    static bool TryReadFile(string path, out GameSaveData save, out string error)
    {
        save = null;
        error = "저장 파일이 없습니다.";
        if (!File.Exists(path)) return false;
        try
        {
            save = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
            if (save != null && save.Version == 1 && save.DayNumber >= 1
                && Enum.IsDefined(typeof(DayPhase), save.DayPhase)
                && !string.IsNullOrEmpty(save.SceneName)
                && (string.IsNullOrEmpty(save.PlayerName) || TryValidatePlayerName(save.PlayerName, LegacyMaxPlayerNameLength, out _, out _))
                && save.Inventory != null && save.Equipment != null
                && save.Player != null && save.Quests != null)
            {
                if (save.SceneName is "Sushi" or "Izakaya")
                    save.SceneName = GameManager.NightScene;
                error = null;
                return true;
            }
            error = "저장 파일 형식이 올바르지 않습니다.";
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException)
        {
            error = exception.Message;
        }
        save = null;
        return false;
    }

    static bool TryWriteSlot(int slot, GameSaveData save, out string error)
    {
        error = null;
        string path = GetSlotPath(slot);
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            error = exception.Message;
            return false;
        }
    }

    static void EnsureLegacyMigrated()
    {
        if (_migrationChecked) return;
        _migrationChecked = true;
        if (File.Exists(Path.Combine(Application.persistentDataPath, LegacyMigrationMarker))) return;
        for (int slot = 1; slot <= MaxSlots; slot++)
            if (File.Exists(GetSlotPath(slot)) || File.Exists(GetSlotPath(slot) + ".bak")) return;

        string legacy = Path.Combine(Application.persistentDataPath, LegacyFileName);
        if (!TryReadFile(legacy, out GameSaveData save, out _))
        {
            legacy += ".bak";
            if (!TryReadFile(legacy, out save, out _)) return;
        }
        if (save.LastPlayedUtcTicks <= 0)
            save.LastPlayedUtcTicks = File.GetLastWriteTimeUtc(legacy).Ticks;
        if (!TryWriteSlot(1, save, out string error))
            Debug.LogWarning("기존 저장 데이터의 슬롯 이전 실패: " + error);
    }

    static bool ValidateSushiJson(string json, SushiShopProgress progress)
    {
        if (string.IsNullOrEmpty(json)) return true;
        if (progress == null) return false;
        try
        {
            var data = JsonUtility.FromJson<SushiShopProgress.SaveData>(json);
            if (data == null || data.Version != 1 || data.ServingLevel < 1
                || data.CookingLevel < 1 || data.MenuLevels == null) return false;
            var ids = new HashSet<int>();
            foreach (SushiShopProgress.MenuLevel entry in data.MenuLevels)
                if (entry == null || entry.MenuId < 0 || !ids.Add(entry.MenuId)
                    || entry.Level < 1 || entry.Level > SushiUpgradeData.MaxLevel) return false;
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    static bool IsValidSlot(int slot) => slot >= 1 && slot <= MaxSlots;
}
