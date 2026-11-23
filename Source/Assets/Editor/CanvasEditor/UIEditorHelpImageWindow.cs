using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UIEditorHelpImageWindow : EditorWindow
{
    [SerializeField] string _path, _caption;
    Texture2D _texture;
    ScrollView _viewport;
    Image _picture;
    bool _fit = true;

    /// <summary>촬영된 도움말 이미지를 읽기 전용 확대 창으로 엽니다.</summary>
    public static void Open(string path, string title, string caption)
    {
        var window = CreateInstance<UIEditorHelpImageWindow>();
        window._path = path;
        window._caption = caption;
        window.titleContent = new GUIContent(title);
        window.minSize = new Vector2(500, 360);
        window.position = new Rect(100, 80, 940, 760);
        window.ShowUtility();
    }

    /// <summary>맞춤과 100% 표시 및 스크롤로 실제 촬영 이미지를 확인하도록 구성합니다.</summary>
    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.style.paddingLeft = root.style.paddingRight = 10;
        root.style.paddingTop = root.style.paddingBottom = 8;
        var bar = new VisualElement();
        bar.style.flexDirection = FlexDirection.Row;
        root.Add(bar);
        bar.Add(new Button(() => { _fit = true; ResizePicture(); }) { text = "화면에 맞춤" });
        bar.Add(new Button(() => { _fit = false; ResizePicture(); }) { text = "100% · 원본 크기" });
        bar.Add(new Button(Close) { text = "닫기" });
        var caption = new Label(_caption) { style = { whiteSpace = WhiteSpace.Normal, fontSize = 13, marginTop = 8, marginBottom = 8 } };
        root.Add(caption);
        _texture = AssetDatabase.LoadAssetAtPath<Texture2D>(_path);
        if (_texture == null)
        {
            root.Add(new Label("이미지를 불러올 수 없습니다."));
            return;
        }
        _viewport = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        _viewport.style.flexGrow = 1;
        _viewport.style.minHeight = 0;
        root.Add(_viewport);
        _picture = new Image { image = _texture, scaleMode = ScaleMode.ScaleToFit };
        _picture.style.flexShrink = 0;
        _viewport.Add(_picture);
        _viewport.RegisterCallback<GeometryChangedEvent>(_ => ResizePicture());
        root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) Close(); });
    }

    /// <summary>스크린샷의 원래 비율을 유지하며 맞춤 또는 원본 크기로 표시합니다.</summary>
    void ResizePicture()
    {
        if (_texture == null || _picture == null || _viewport == null)
            return;
        float width = _viewport.layout.width - 18;
        float height = _viewport.layout.height - 18;
        if (width <= 0 || height <= 0)
            return;
        float scale = _fit ? Mathf.Min(1, Mathf.Min(width / _texture.width, height / _texture.height)) : 1;
        _picture.style.width = _texture.width * scale;
        _picture.style.height = _texture.height * scale;
        _viewport.scrollOffset = Vector2.zero;
    }
}
