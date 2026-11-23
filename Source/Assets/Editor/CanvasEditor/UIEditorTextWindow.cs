using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using U = UnityEngine.UI;

public sealed class UIEditorTextWindow : EditorWindow
{
    List<Component> _texts;
    Func<Component, CanvasEditor.TextState> _read;
    Func<Component, CanvasEditor.TextState, bool> _preview;
    CanvasEditor.TextState _state;
    VisualElement _fields;
    int _index;
    bool _test;
    /// <summary>편집창을 열고 초기 대상과 선택 동작을 연결합니다.</summary>
    public static void Open(List<Component> texts, Func<Component, CanvasEditor.TextState> read, Func<Component, CanvasEditor.TextState, bool> preview, bool test)
    {
        var window = CreateInstance<UIEditorTextWindow>();
        window.titleContent = new GUIContent("텍스트 편집");
        window._texts = texts;
        window._read = read;
        window._preview = preview;
        window._test = test;
        window.minSize = new Vector2(420, 430);
        window.position = new Rect(130, 110, 500, 500);
        window.ShowUtility();
    }

    /// <summary>편집창의 화면과 조작부를 구성하고 현재 상태를 반영합니다.</summary>
    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.Clear();
        root.AddToClassList("canvas-editor");
        var style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/CanvasEditor/CanvasEditor.uss");
        if (style != null)
            root.styleSheets.Add(style);
        if (_texts == null || _texts.Count == 0 || _read == null)
        {
            root.Add(new Label("원래 편집창에서 텍스트 편집을 다시 열어 주세요."));
            return;
        }

        var choices = _texts.Select((t, i) => $"{i + 1}. {AnimationUtility.CalculateTransformPath(t.transform, null)} · {t.GetType().Name}").ToList();
        var picker = new PopupField<string>("편집할 텍스트", choices, _index);
        picker.AddToClassList("stacked-field");
        root.Add(picker);
        picker.RegisterValueChangedCallback(_ =>
        {
            _index = picker.index;
            ReadFields();
        });
        _fields = new VisualElement();
        root.Add(_fields);
        var note = new Label(_test ? "테스트 UI · 미리보기만 지원하며 저장되지 않습니다." : "미리보기로 확인 후 원래 창의 변경 적용을 눌러 반영하세요.");
        note.AddToClassList("usage-hint");
        root.Add(note);
        var hint = new Label("기존 자동 크기 사용 시 크기는 자동 계산됩니다. 직접 지정하려면 자동 크기를 꺼 주세요. 색상은 알파값도 조절합니다.");
        hint.AddToClassList("usage-hint");
        root.Add(hint);
        var actions = new VisualElement();
        actions.AddToClassList("row");
        actions.AddToClassList("detail-actions");
        root.Add(actions);
        var preview = new Button(() =>
        {
            if (_texts[_index] == null || _state.Font == null || float.IsNaN(_state.Size) || float.IsInfinity(_state.Size) || _state.Size < 1)
            {
                ShowNotification(new GUIContent("폰트와 1 이상의 유효한 크기를 지정하세요."));
                return;
            }

            if (_preview == null || !_preview(_texts[_index], _state))
            {
                ShowNotification(new GUIContent("편집 대상이 바뀌었습니다. 원래 창에서 다시 열어 주세요."));
                return;
            }

            ShowNotification(new GUIContent("미리보기에 반영했습니다. 원본은 아직 변경되지 않았습니다."));
        })
        {text = "미리보기"};
        preview.AddToClassList("primary-button");
        actions.Add(preview);
        actions.Add(new Button(Close)
        {text = "닫기"});
        ReadFields();
        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                Close();
                e.StopPropagation();
            }
        });
    }

    /// <summary>선택한 텍스트의 현재 설정을 편집 필드에 불러옵니다.</summary>
    void ReadFields()
    {
        _fields.Clear();
        var component = _texts[_index];
        if (component == null)
        {
            _fields.Add(new Label("텍스트가 삭제되었습니다. 창을 다시 열어 주세요."));
            return;
        }

        _state = _read(component);
        bool tmp = component is TMP_Text;
        var font = new ObjectField("폰트")
        {objectType = tmp ? typeof(TMP_FontAsset) : typeof(Font), allowSceneObjects = false, value = _state.Font};
        font.AddToClassList("stacked-field");
        _fields.Add(font);
        font.tooltip = tmp ? "TMP 폰트 에셋(SDF)을 선택합니다." : "Unity Font 에셋을 선택합니다.";
        font.RegisterValueChangedCallback(e => _state.Font = e.newValue);
        var size = new FloatField("폰트 크기")
        {value = _state.Size, isDelayed = true};
        size.AddToClassList("stacked-field");
        _fields.Add(size);
        size.RegisterValueChangedCallback(e => _state.Size = tmp ? e.newValue : Mathf.Round(e.newValue));
        var auto = new Toggle(tmp ? "자동 크기 사용" : "최적 맞춤 사용")
        {value = _state.AutoSize};
        _fields.Add(auto);
        size.SetEnabled(!_state.AutoSize);
        auto.RegisterValueChangedCallback(e =>
        {
            _state.AutoSize = e.newValue;
            size.SetEnabled(!e.newValue);
        });
        Enum alignment = tmp ? (Enum)(TextAlignmentOptions)_state.Alignment : (TextAnchor)_state.Alignment;
        var align = new EnumField("정렬", alignment);
        align.AddToClassList("stacked-field");
        _fields.Add(align);
        align.RegisterValueChangedCallback(e => _state.Alignment = Convert.ToInt32(e.newValue));
        var color = new ColorField("텍스트 색상")
        {value = _state.Color, showAlpha = true, hdr = false};
        color.AddToClassList("stacked-field");
        _fields.Add(color);
        color.RegisterValueChangedCallback(e => _state.Color = e.newValue);
    }
}
