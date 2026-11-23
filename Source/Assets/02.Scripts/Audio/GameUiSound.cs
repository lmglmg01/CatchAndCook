using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

// Translates existing UI interactions into SFX without changing shared views or event handlers.
[DefaultExecutionOrder(-100)]
internal sealed class GameUiSound : MonoBehaviour
{
    sealed class Dialogue
    {
        internal TalkView View;
        internal TMP_Text Content;
        internal bool WasOpen;
        internal int Line = -1;
        internal int Visible;
    }

    struct PointerPress
    {
        internal GameObject Target;
        internal GameObject DragTarget;
        internal Vector2 Position;
    }

    readonly List<RaycastResult> _hits = new();
    readonly List<Dialogue> _dialogues = new();
    readonly List<(ViewActivationRelay Relay, System.Action Open, System.Action Close)> _windowSounds = new();
    readonly HashSet<Button> _buttons = new();
    readonly Dictionary<int, SushiSceneSound> _sushiSounds = new();
    static GameUiSound _instance;
    PointerEventData _pointer;
    EventSystem _events;
    PointerPress _mousePress;
#if ENABLE_INPUT_SYSTEM
    PointerPress _touchPress;
#endif
    bool _clickPending;
    bool _anyDialogueOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var observer = new GameObject("Game UI Sound");
        DontDestroyOnLoad(observer);
        _instance = observer.AddComponent<GameUiSound>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded) BindViews(scene);
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        foreach (var entry in _windowSounds)
        {
            if (entry.Relay == null) continue;
            entry.Relay.Opened -= entry.Open;
            entry.Relay.Closed -= entry.Close;
        }
        _windowSounds.Clear();
        foreach (var sounds in _sushiSounds.Values) sounds.Dispose();
        _sushiSounds.Clear();
        foreach (var button in _buttons)
            if (button != null) button.onClick.RemoveListener(PlayClick);
        _buttons.Clear();
        _dialogues.Clear();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindViews(scene);

    void OnSceneUnloaded(Scene scene)
    {
        if (_sushiSounds.Remove(scene.handle, out var sounds)) sounds.Dispose();
        for (int index = _windowSounds.Count - 1; index >= 0; index--)
        {
            var entry = _windowSounds[index];
            if (entry.Relay != null && entry.Relay.gameObject.scene != scene) continue;
            if (entry.Relay != null)
            {
                entry.Relay.Opened -= entry.Open;
                entry.Relay.Closed -= entry.Close;
            }
            _windowSounds.RemoveAt(index);
        }
        _buttons.RemoveWhere(button =>
        {
            if (button == null) return true;
            if (button.gameObject.scene != scene) return false;
            button.onClick.RemoveListener(PlayClick);
            return true;
        });
        _dialogues.RemoveAll(dialogue => dialogue.View == null || dialogue.View.gameObject.scene == scene);
        _mousePress = default;
#if ENABLE_INPUT_SYSTEM
        _touchPress = default;
#endif
    }

    void BindViews(Scene scene)
    {
        bool sushi = scene.name is "Izakaya" or "TutorialSushi" or "Sushi 2";
        if (sushi && !_sushiSounds.ContainsKey(scene.handle))
            _sushiSounds.Add(scene.handle, new SushiSceneSound(scene, BindPopup));
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (_buttons.Add(button)) button.onClick.AddListener(PlayClick);

            foreach (var view in root.GetComponentsInChildren<BaseView>(true))
            {
                var phone = view.transform.Find("SmartPhone/AppGroup");
                if (phone != null) BindPopup(phone.gameObject);
            }

            foreach (var panel in root.GetComponentsInChildren<RectTransform>(true))
                if (IsPopup(panel)) BindPopup(panel.gameObject);

            foreach (var view in root.GetComponentsInChildren<TalkView>(true))
            {
                if (_dialogues.Exists(entry => entry.View == view)) continue;
                TMP_Text content = null;
                foreach (var label in view.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.name != "TalkerContentText") continue;
                    content = label;
                    break;
                }
                _dialogues.Add(new Dialogue { View = view, Content = content });
            }
        }
    }

    static bool IsPopup(RectTransform panel)
    {
        return panel.name.EndsWith("Popup", StringComparison.Ordinal)
            || panel.name is "Interact_NPC" or "Store_Detail" or "Inventory_Details"
                or "UpgradeConfirmationOverlay" or "UpgradeFailureOverlay"
            || panel.TryGetComponent<CommonWindowEntranceView>(out _)
            || panel.TryGetComponent<StoreView>(out _)
            || panel.TryGetComponent<UpgradeEquipView>(out _)
            || panel.TryGetComponent<InventoryView>(out _)
            || panel.TryGetComponent<QuestListView>(out _)
            || panel.TryGetComponent<QuestDetailView>(out _)
            || panel.TryGetComponent<SettingView>(out _)
            || panel.TryGetComponent<KeyboardSettingView>(out _)
            || panel.TryGetComponent<DiveResultView>(out _)
            || panel.TryGetComponent<SushiMenuQuantityView>(out _)
            || panel.TryGetComponent<SushiRecipeDetailView>(out _)
            || panel.TryGetComponent<SushiStaffView>(out _)
            || panel.TryGetComponent<SushiStaffUpgradeView>(out _);
    }

    internal static void WatchPopup(GameObject panel)
    {
        if (_instance != null && panel != null) _instance.BindPopup(panel);
    }

    void BindPopup(GameObject panel, bool playsOpening = true)
    {
        var relay = panel.GetComponent<ViewActivationRelay>();
        if (relay == null) relay = panel.AddComponent<ViewActivationRelay>();
        if (_windowSounds.Exists(entry => entry.Relay == relay)) return;
        Action open = playsOpening ? PlayOpen : null;
        _windowSounds.Add((relay, open, PlayClose));
        relay.Opened += open;
        relay.Closed += PlayClose;
    }

    static void PlayClick() => GameSfx.Play(GameSoundId.MenuClick);
    static void PlayOpen() => GameSfx.Play(GameSoundId.OpenUi);
    static void PlayClose() => GameSfx.Play(GameSoundId.CloseUi);

    void Update()
    {
        var events = EventSystem.current;
        if (_events != events)
        {
            _events = events;
            _pointer = events == null ? null : new PointerEventData(events);
            _mousePress = default;
#if ENABLE_INPUT_SYSTEM
            _touchPress = default;
#endif
        }
        if (events == null || events.currentInputModule == null || !events.currentInputModule.isActiveAndEnabled) return;

#if ENABLE_INPUT_SYSTEM
        if (events.currentInputModule is InputSystemUIInputModule module)
        {
            if (module.leftClick?.action?.enabled == true && module.point?.action?.enabled == true)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                    TrackPointer(ref _mousePress, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);
                var screen = Touchscreen.current;
                if (screen != null)
                {
                    var touch = screen.primaryTouch;
                    TrackPointer(ref _touchPress, touch.position.ReadValue(), touch.press.wasPressedThisFrame, touch.press.wasReleasedThisFrame);
                }
            }

            if (events.sendNavigationEvents && module.submit?.action?.WasPerformedThisFrame() == true)
            {
                var selected = events.currentSelectedGameObject;
                if (selected != null && selected.TryGetComponent<Selectable>(out var selectable)
                    && (selectable is Button or Toggle) && selectable.isActiveAndEnabled && selectable.IsInteractable())
                    _clickPending = true;
            }
            return;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (events.currentInputModule is StandaloneInputModule)
            TrackPointer(ref _mousePress, Input.mousePosition, Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0));
#endif
    }

    void TrackPointer(ref PointerPress press, Vector2 position, bool pressed, bool released)
    {
        if (pressed)
        {
            var target = FindClickTarget(position, out var dragTarget);
            press = new PointerPress { Target = target, DragTarget = dragTarget, Position = position };
        }
        if (press.Target != null && press.DragTarget != null && press.Target != press.DragTarget
            && (position - press.Position).sqrMagnitude >= _events.pixelDragThreshold * _events.pixelDragThreshold)
            press = default;

        if (!released) return;
        if (press.Target != null && FindClickTarget(position, out _) == press.Target)
            _clickPending = true;
        press = default;
    }

    GameObject FindClickTarget(Vector2 position, out GameObject dragTarget)
    {
        dragTarget = null;
        _pointer.Reset();
        _pointer.position = position;
        _events.RaycastAll(_pointer, _hits);
        if (_hits.Count == 0 || _hits[0].module is not GraphicRaycaster) return null;
        GameObject top = _hits[0].gameObject;
        var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(top);
        if (handler == null) return null;
        if (handler.TryGetComponent<Selectable>(out var selectable) && !selectable.IsInteractable()) return null;
        dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(top);
        return handler;
    }

    void LateUpdate()
    {
        foreach (var sounds in _sushiSounds.Values) sounds.Update();
        bool anyOpen = false;
        foreach (var dialogue in _dialogues) anyOpen |= UpdateDialogue(dialogue);
        if (_anyDialogueOpen && !anyOpen) GameSfx.StopDialogue();
        _anyDialogueOpen = anyOpen;

        if (!_clickPending) return;
        _clickPending = false;
        PlayClick();
    }

    static bool UpdateDialogue(Dialogue dialogue)
    {
        if (dialogue.View == null || !dialogue.View.IsTalking)
        {
            if (dialogue.WasOpen && dialogue.View != null) PlayClose();
            dialogue.WasOpen = false;
            dialogue.Line = -1;
            dialogue.Visible = 0;
            return false;
        }

        int line = dialogue.View.CurrentLineIndex;
        if (!dialogue.WasOpen)
        {
            PlayOpen();
            dialogue.WasOpen = true;
        }
        if (dialogue.Line != line)
        {
            dialogue.Line = line;
            dialogue.Visible = 0;
        }
        var text = dialogue.Content;
        if (text == null) return true;
        int total = text.textInfo.characterCount;
        int maxVisible = text.maxVisibleCharacters;
        int visible = Mathf.Min(maxVisible, total);

        // A manual skip shows the whole line at once. Do not dump all its typing sounds.
        if (maxVisible != int.MaxValue || dialogue.Visible == total - 1)
        {
            for (int index = dialogue.Visible; index < visible; index++)
            {
                char character = text.textInfo.characterInfo[index].character;
                if (char.IsWhiteSpace(character) || char.IsControl(character)) continue;
                GameSfx.Play(GameSoundId.Dialogue);
                break;
            }
        }
        dialogue.Visible = visible;
        return true;
    }
}
