#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 편집 중과 플레이 중에 PrintScreen으로 현재 Game 뷰를 PNG 파일로 저장합니다.
/// 오브젝트에 붙일 필요 없이 에디터가 스크립트를 불러오면 자동으로 동작합니다.
/// </summary>
[InitializeOnLoad]
public static class GameViewScreenshot
{
    private const string SaveDirectory = "Assets/Screenshots";
    private const string CaptureMenu = "Tools/ScreenShot/Game 화면 저장";
    private static readonly Type GameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    // Game 뷰의 완성된 렌더 결과를 읽어 Overlay 방식 Canvas도 함께 저장합니다.
    // Unity 내부 항목이므로 에디터 버전을 올렸을 때 필드 이름을 확인해야 합니다.
    private static readonly FieldInfo RenderTextureField = GameViewType?.GetField(
        "m_RenderTexture", BindingFlags.Instance | BindingFlags.NonPublic);

    private static EditorWindow _gameView;
    private static EditorWindow _previousWindow;
    private static bool _pending;
    private static int _waitingUpdates;
    private static double _requestedAt;
    private static double _nextRequestAt;
#if UNITY_EDITOR_WIN
    private static bool _printHeld;
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
#endif

    /// <summary>에디터 갱신과 모드 전환 이벤트를 한 번 연결합니다.</summary>
    static GameViewScreenshot()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += CancelCapture;
    }

    /// <summary>Unity 단축키 설정에 PrintScreen 키를 등록합니다.</summary>
    [Shortcut("Project/Game 화면 스크린샷", KeyCode.Print)]
    private static void CaptureShortcut()
    {
        CaptureGameView();
    }

    /// <summary>
    /// 메뉴 또는 PrintScreen 요청을 받아 Game 뷰를 갱신하고 캡처를 예약합니다.
    /// 중복 입력과 모드 전환 중의 캡처는 건너뜁니다.
    /// </summary>
    [MenuItem(CaptureMenu)]
    public static void CaptureGameView()
    {
        double now = EditorApplication.timeSinceStartup;
        if (_pending || now < _nextRequestAt || EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (GameViewType == null || RenderTextureField == null)
        {
            Debug.LogError("[스크린샷] 이 Unity 버전의 Game 뷰 캡처 기능을 찾을 수 없습니다.");
            return;
        }

        _previousWindow = EditorWindow.focusedWindow;
        _gameView = EditorWindow.GetWindow(GameViewType);
        _gameView.Show();
        _gameView.Focus();
        _gameView.Repaint();
        EditorApplication.QueuePlayerLoopUpdate();
        _requestedAt = now;
        _nextRequestAt = now + 0.4;
        _waitingUpdates = 0;
        _pending = true;
    }

    /// <summary>
    /// Windows에서는 Game 뷰에 입력이 전달되는 동안에도 PrintScreen을 감지합니다.
    /// 캡처 요청이 있으면 Game 뷰가 다시 그려진 다음 결과를 저장합니다.
    /// </summary>
    private static void Update()
    {
#if UNITY_EDITOR_WIN
        int state = (ushort)GetAsyncKeyState(0x2C); // Windows의 PrintScreen 키입니다.
        bool held = (state & 0x8000) != 0;
        bool pressed = (held && !_printHeld) || (state & 1) != 0;
        _printHeld = held;
        if (pressed && UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            CaptureGameView();
#endif
        if (!_pending)
            return;

        try
        {
            if (_gameView == null)
                throw new InvalidOperationException("Game 뷰가 닫혀 캡처를 취소했습니다.");

            _gameView.Repaint();
            _waitingUpdates++;
            double elapsed = EditorApplication.timeSinceStartup - _requestedAt;
            // 편집 모드에서도 새로 그려진 화면을 읽도록 최소 갱신 시간을 확보합니다.
            if (_waitingUpdates < 3 || elapsed < 0.25)
                return;

            var rendered = RenderTextureField.GetValue(_gameView) as RenderTexture;
            if (rendered == null || !rendered.IsCreated() || rendered.width < 1 || rendered.height < 1)
            {
                if (elapsed < 4)
                    return;
                throw new InvalidOperationException("Game 화면이 준비되지 않았습니다. Game 뷰와 카메라를 확인해 주세요.");
            }

            SavePng(rendered);
            _gameView.ShowNotification(new GUIContent("스크린샷 저장 완료"));
            CancelCapture();
        }
        catch (Exception exception)
        {
            Debug.LogError("[스크린샷] 저장 실패: " + exception.Message);
            CancelCapture();
        }
    }

    /// <summary>
    /// Game 뷰의 렌더 텍스처를 읽어 씬 이름·모드·촬영 시간이 포함된 PNG로 저장합니다.
    /// Game 뷰가 소유한 렌더 텍스처는 변경하거나 제거하지 않습니다.
    /// </summary>
    private static void SavePng(RenderTexture rendered)
    {
        DateTime capturedAt = DateTime.Now;
        string dateFolder = capturedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string directory = $"{SaveDirectory}/{dateFolder}";
        EnsureDirectory(directory);
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName))
            sceneName = "Untitled";
        foreach (char invalid in Path.GetInvalidFileNameChars())
            sceneName = sceneName.Replace(invalid, '_');

        string mode = EditorApplication.isPlaying ? "Play" : "Edit";
        string timestamp = capturedAt.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string filename = $"{sceneName}_{mode}_{timestamp}.png";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{filename}");
        string fullPath = GetFullPath(assetPath);
        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = rendered;
            image = new Texture2D(rendered.width, rendered.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0, false);
            // Direct3D 등의 Game 뷰 렌더 결과는 상하 방향을 맞춰 저장합니다.
            if (SystemInfo.graphicsUVStartsAtTop)
                FlipVertically(image);
            image.Apply(false, false);
            File.WriteAllBytes(fullPath, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null)
                UnityEngine.Object.DestroyImmediate(image);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[스크린샷] {rendered.width}×{rendered.height} 저장 완료: {assetPath}",
            AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
    }

    /// <summary>렌더 결과의 픽셀 행 순서를 뒤집어 Game 뷰와 같은 상하 방향으로 만듭니다.</summary>
    private static void FlipVertically(Texture2D image)
    {
        Color32[] pixels = image.GetPixels32();
        var row = new Color32[image.width];
        for (int y = 0; y < image.height / 2; y++)
        {
            int top = y * image.width;
            int bottom = (image.height - 1 - y) * image.width;
            Array.Copy(pixels, top, row, 0, row.Length);
            Array.Copy(pixels, bottom, pixels, top, row.Length);
            Array.Copy(row, 0, pixels, bottom, row.Length);
        }
        image.SetPixels32(pixels);
    }
    /// <summary>프로젝트의 Assets 경로를 기준으로 절대 파일 경로를 만듭니다.</summary>
    private static string GetFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    /// <summary>스크린샷 저장 폴더가 없으면 생성하고 Unity에서 인식하도록 갱신합니다.</summary>
    private static void EnsureDirectory(string assetPath)
    {
        Directory.CreateDirectory(GetFullPath(assetPath));
        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.Refresh();
    }

    /// <summary>메뉴에서 스크린샷 저장 폴더를 탐색기로 엽니다.</summary>
    [MenuItem("Tools/ScreenShot/저장 폴더 열기")]
    private static void OpenDirectory()
    {
        EnsureDirectory(SaveDirectory);
        EditorUtility.RevealInFinder(GetFullPath(SaveDirectory));
    }

    /// <summary>편집·플레이 전환 도중에는 이전 화면의 예약 캡처를 취소합니다.</summary>
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
            CancelCapture();
    }

    /// <summary>예약 상태를 정리하고 캡처 전에 작업하던 에디터 창으로 돌아갑니다.</summary>
    private static void CancelCapture()
    {
        _pending = false;
        if (_previousWindow != null && _previousWindow != _gameView)
            _previousWindow.Focus();
        _previousWindow = null;
        _gameView = null;
    }
}
#endif