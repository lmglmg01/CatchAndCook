using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>ESC로 앞 창을 닫고, 닫을 창이 없으면 스마트폰을 열거나 닫습니다.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class UIEscapeView : MonoBehaviour
{
    sealed class Entry
    {
        public MonoBehaviour Owner;
        public UnityEngine.UI.Button Button;
    }

    static readonly List<Entry> Entries = new List<Entry>();
    static UIEscapeView _instance;
    static int _handledFrame = -1;
    static bool _consumed;
    InputAction _escapeAction;

    /// <summary>도메인 재로드를 생략한 플레이에서도 이전 실행의 참조를 남기지 않습니다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry()
    {
        Entries.Clear(); _instance = null; _handledFrame = -1; _consumed = false;
    }

    /// <summary>씬이나 프리팹에 별도 연결 없이 공통 입력 View를 한 번만 준비합니다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (!Application.isPlaying || _instance != null) return;
        var root = new GameObject("UIEscapeView");
        _instance = root.AddComponent<UIEscapeView>();
        DontDestroyOnLoad(root);
    }

    /// <summary>각 View가 이미 사용하는 취소 버튼을 등록합니다. 클릭 이벤트는 변경하지 않습니다.</summary>
    public static void Register(MonoBehaviour owner, params UnityEngine.UI.Button[] buttons)
    {
        if (!Application.isPlaying || owner == null || buttons == null) return;
        EnsureInstance();
        foreach (var button in buttons)
        {
            if (button == null) continue;
            bool exists = Entries.Exists(e => e.Owner == owner && e.Button == button);
            if (!exists) Entries.Add(new Entry { Owner = owner, Button = button });
        }
    }

    /// <summary>게임 시간이나 씬의 행동 액션맵과 독립적으로 ESC 입력을 받습니다.</summary>
    void OnEnable()
    {
        _escapeAction = new InputAction("UIBack", InputActionType.Button, "<Keyboard>/escape");
        _escapeAction.performed += OnEscape;
        _escapeAction.Enable();
    }

    /// <summary>View가 제거되면 자체 입력 연결만 정리합니다.</summary>
    void OnDisable()
    {
        if (_escapeAction == null) return;
        _escapeAction.performed -= OnEscape;
        _escapeAction.Dispose();
        _escapeAction = null;
    }

    /// <summary>입력 처리 시점에 취소를 전달해 한 프레임 안의 복수 입력 갱신도 놓치지 않습니다.</summary>
    void OnEscape(InputAction.CallbackContext context) => TryCloseTopmost();

    /// <summary>시간 정지 중에도 키를 한 번 누를 때 한 창만 닫습니다.</summary>
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TryCloseTopmost();
    }

    /// <summary>같은 ESC가 기존 스마트폰 액션에도 전달될 때 중복 실행을 막습니다.</summary>
    public static bool ConsumeEscapeShortcut()
    {
        if (_handledFrame == Time.frameCount && _consumed) return true;
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && TryCloseTopmost();
    }

    /// <summary>키 변경 취소, 앞 창 닫기, 스마트폰 토글 순서로 처리하며 같은 프레임에는 한 번만 실행합니다.</summary>
    public static bool TryCloseTopmost()
    {
        if (_handledFrame == Time.frameCount) return _consumed;
        _handledFrame = Time.frameCount;
        _consumed = false;
        // 미니게임은 결과가 확정될 때까지 유지하고 뒤쪽 창으로 ESC를 전달하지 않습니다.
        foreach (var minigame in FindObjectsByType<SushiMinigameView>(FindObjectsSortMode.None))
            if (minigame.isActiveAndEnabled && minigame.IsOpen) return _consumed = true;
        foreach (var keyboard in FindObjectsByType<KeyboardSettingView>(FindObjectsSortMode.None))
        {
            if (!keyboard.isActiveAndEnabled || !keyboard.IsListening) continue;
            keyboard.CancelRebind();
            return _consumed = true;
        }
        Entry top = null;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            var entry = Entries[i];
            if (entry.Owner == null || entry.Button == null) { Entries.RemoveAt(i); continue; }
            if (!entry.Owner.isActiveAndEnabled || !Visible(entry.Button)) continue;
            if (top == null || Compare(entry.Button.transform, top.Button.transform) > 0) top = entry;
        }
        if (top == null)
        {
            // NPC 선택창에는 닫기 버튼이 없으므로 스마트폰 토글 전에 기존 BoardView의 취소를 우선합니다.
            foreach (var board in FindObjectsByType<BoardView>(FindObjectsSortMode.None))
                if (board.TryCancelNPCInteraction()) return _consumed = true;
            return TryToggleSmartPhone();
        }
        // 닫히는 중이거나 잠겨 있는 앞 창의 입력이 뒤 창으로 새지 않게 합니다.
        _consumed = true;
        if (top.Button.IsInteractable()) top.Button.onClick.Invoke();
        return true;
    }

    /// <summary>닫을 창이 없으면 맨 앞에 표시된 스마트폰을 기존 연출로 열거나 닫습니다.</summary>
    static bool TryToggleSmartPhone()
    {
        BaseView top = null;
        foreach (var view in FindObjectsByType<BaseView>(FindObjectsSortMode.None))
        {
            var button = view.SmartPhoneEscapeButton;
            if (!view.isActiveAndEnabled || button == null || !Visible(button) || !button.IsInteractable()) continue;
            if (top == null || Compare(button.transform, top.SmartPhoneEscapeButton.transform) > 0) top = view;
        }
        if (top == null) return false;
        // 기존 스마트폰 액션이 같은 ESC를 받아도 다시 토글하지 않도록 먼저 소비합니다.
        _consumed = true;
        top.ToggleSmartPhoneFromEscape();
        return true;
    }

    /// <summary>숨겨 둔 버튼과 투명한 페이지는 취소 대상으로 선택하지 않습니다.</summary>
    static bool Visible(UnityEngine.UI.Button button)
    {
        if (!button.isActiveAndEnabled) return false;
        // 배경 Image가 투명해도 글씨·화살표가 보이는 뒤로가기 버튼은 유효합니다.
        bool hasVisibleGraphic = false;
        foreach (var graphic in button.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            if (graphic.enabled && graphic.color.a > .001f) { hasVisibleGraphic = true; break; }
        if (!hasVisibleGraphic) return false;
        for (var t = button.transform; t != null; t = t.parent)
        {
            foreach (var group in t.GetComponents<CanvasGroup>())
                if (group.alpha <= .001f) return false;
        }
        var canvas = button.GetComponentInParent<Canvas>();
        return canvas != null && canvas.isActiveAndEnabled;
    }

    /// <summary>실제 Canvas 정렬과 형제 순서를 비교하여 가장 앞에 그려지는 창을 선택합니다.</summary>
    static int Compare(Transform a, Transform b)
    {
        Canvas SortingCanvas(Transform t)
        {
            var canvases = t.GetComponentsInParent<Canvas>();
            foreach (var canvas in canvases) if (canvas.overrideSorting || canvas.isRootCanvas) return canvas;
            return canvases.Length > 0 ? canvases[canvases.Length - 1] : null;
        }
        var ca = SortingCanvas(a); var cb = SortingCanvas(b);
        if (ca != null && cb != null)
        {
            int result = (ca.renderMode == RenderMode.ScreenSpaceOverlay ? 1 : 0).CompareTo(cb.renderMode == RenderMode.ScreenSpaceOverlay ? 1 : 0);
            if (result != 0) return result;
            result = SortingLayer.GetLayerValueFromID(ca.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(cb.sortingLayerID));
            if (result != 0) return result;
            result = ca.sortingOrder.CompareTo(cb.sortingOrder);
            if (result != 0) return result;
        }
        var pa = new List<Transform>(); var pb = new List<Transform>();
        for (var t = a; t != null; t = t.parent) pa.Add(t);
        for (var t = b; t != null; t = t.parent) pb.Add(t);
        pa.Reverse(); pb.Reverse();
        int count = Mathf.Min(pa.Count, pb.Count);
        for (int i = 0; i < count; i++)
        {
            if (pa[i] == pb[i]) continue;
            return pa[i].GetSiblingIndex().CompareTo(pb[i].GetSiblingIndex());
        }
        return pa.Count.CompareTo(pb.Count);
    }
}
