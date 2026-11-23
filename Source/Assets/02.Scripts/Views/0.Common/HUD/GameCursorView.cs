using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Game 뷰와 빌드에서 동일한 좌우 반전 커서를 표시합니다.</summary>
public sealed class GameCursorView : MonoBehaviour
{
    [SerializeField] RawImage _image;
    [SerializeField] Vector2 _hotspot = new Vector2(3, 5);
    bool _previousVisible;
    static GameCursorView _instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance() { _instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (_instance != null) return;
        var prefab = Resources.Load<GameObject>("GameCursor");
        if (prefab != null) Instantiate(prefab);
    }
    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    void OnEnable() { _previousVisible = Cursor.visible; }
    void LateUpdate()
    {
        if (_image == null) return;
        var mouse = Mouse.current;
        Vector2 p = mouse == null ? Vector2.zero : mouse.position.ReadValue();
        bool show = Application.isFocused && mouse != null && Cursor.lockState != CursorLockMode.Locked &&
            p.x >= 0 && p.y >= 0 && p.x < Screen.width && p.y < Screen.height;
        _image.enabled = show;
        Cursor.visible = show ? false : _previousVisible;
        if (show) _image.rectTransform.anchoredPosition = p + new Vector2(-_hotspot.x, _hotspot.y);
    }
    void OnApplicationFocus(bool focus) { if (!focus) { if (_image != null) _image.enabled = false; Cursor.visible = _previousVisible; } }
    void OnDisable() { Cursor.visible = _previousVisible; }
}
