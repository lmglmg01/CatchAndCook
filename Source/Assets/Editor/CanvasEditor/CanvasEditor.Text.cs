using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using U = UnityEngine.UI;

public sealed partial class CanvasEditor
{
    public struct TextState : IEquatable<TextState>
    {
        public UnityEngine.Object Font;
        public float Size;
        public int Alignment;
        public bool AutoSize;
        public Color Color;
        /// <summary>참조 또는 설정의 구성 값을 비교합니다.</summary>
        public bool Equals(TextState other) => Font == other.Font && Mathf.Approximately(Size, other.Size) && Alignment == other.Alignment && AutoSize == other.AutoSize && Color.Equals(other.Color);
    }

    sealed class TextChange
    {
        public Component Component;
        public TextState Before, After;
    }

    readonly Dictionary<Component, TextChange> _textChanges = new();
    Button _editTextButton;
    int ResourceChangeCount => _changes.Count + _textChanges.Count;
    /// <summary>선택 계층에서 편집 가능한 TMP 및 일반 텍스트를 찾습니다.</summary>
    List<Component> SelectedTexts()
    {
        if (_picked == null || !_nodes.TryGetValue(_picked, out var node) || node.Original == null)
            return new List<Component>();
        var components = (_includeChildren?.value ?? true) ? node.Original.GetComponentsInChildren<Component>(true) : node.Original.GetComponents<Component>();
        return components.Where(c => c is TMP_Text || c is U.Text).ToList();
    }

    /// <summary>텍스트 개수와 편집 가능 여부를 버튼에 반영합니다.</summary>
    void RefreshTextControl()
    {
        if (_editTextButton == null)
            return;
        int count = SelectedTexts().Count;
        _editTextButton.text = count > 0 ? $"텍스트 편집 ({count})" : "텍스트 없음";
        _editTextButton.SetEnabled(count > 0 && CanEditSource());
    }

    /// <summary>선택 계층의 텍스트 설정을 편집하는 보조창을 엽니다.</summary>
    void OpenTextEditor()
    {
        if (!CanEditSource() || _picked == null || !_nodes.TryGetValue(_picked, out var node))
            return;
        var texts = SelectedTexts();
        if (texts.Count == 0)
            return;
        var original = node.Original;
        UIEditorTextWindow.Open(texts, EffectiveText, (component, state) =>
        {
            if (this == null || !CanEditSource() || _picked == null || !_nodes.TryGetValue(_picked, out var current) || current.Original != original)
                return false;
            return PreviewTextChange(component, state);
        }, IsTestCanvas);
    }

    /// <summary>텍스트의 폰트와 크기 및 정렬과 색상 상태를 읽습니다.</summary>
    static TextState ReadText(Component component)
    {
        if (component is TMP_Text tmp)
            return new TextState{Font = tmp.font, Size = tmp.fontSize, Alignment = (int)tmp.alignment, AutoSize = tmp.enableAutoSizing, Color = tmp.color};
        if (component is U.Text text)
            return new TextState{Font = text.font, Size = text.fontSize, Alignment = (int)text.alignment, AutoSize = text.resizeTextForBestFit, Color = text.color};
        throw new InvalidOperationException("지원하지 않는 텍스트입니다.");
    }

    /// <summary>미적용 변경까지 포함한 현재 텍스트 설정을 반환합니다.</summary>
    TextState EffectiveText(Component component) => _textChanges.TryGetValue(component, out var change) ? change.After : ReadText(component);
    /// <summary>TMP 또는 일반 텍스트에 지정한 표시 설정을 반영합니다.</summary>
    static void WriteText(Component component, TextState state)
    {
        if (component is TMP_Text tmp)
        {
            tmp.font = state.Font as TMP_FontAsset;
            tmp.enableAutoSizing = state.AutoSize;
            tmp.fontSize = state.Size;
            tmp.alignment = (TextAlignmentOptions)state.Alignment;
            tmp.color = state.Color;
            tmp.SetAllDirty();
        }
        else if (component is U.Text text)
        {
            text.font = state.Font as Font;
            text.resizeTextForBestFit = state.AutoSize;
            text.fontSize = Mathf.RoundToInt(state.Size);
            text.alignment = (TextAnchor)state.Alignment;
            text.color = state.Color;
            text.SetAllDirty();
        }
    }

    /// <summary>텍스트 변경의 유효성을 확인하고 미리보기 이력에 기록합니다.</summary>
    bool PreviewTextChange(Component component, TextState state)
    {
        if (component == null || !CanEditSource() || !component.transform.IsChildOf(_target.transform))
            return false;
        if (state.Font == null || float.IsNaN(state.Size) || float.IsInfinity(state.Size) || state.Size < 1)
            return false;
        if (component is TMP_Text && state.Font is not TMP_FontAsset || component is U.Text && state.Font is not Font)
            return false;
        var before = EffectiveText(component);
        if (before.Equals(state))
            return true;
        _history.Push(new DraftStep(component, before, state));
        _redo.Clear();
        SetTextDraftValue(component, state);
        RefreshStatus();
        return true;
    }

    /// <summary>텍스트 변경을 관리하고 대응하는 복제본 텍스트를 갱신합니다.</summary>
    void SetTextDraftValue(Component component, TextState state)
    {
        if (component == null)
            return;
        if (_textChanges.TryGetValue(component, out var change))
        {
            change.After = state;
            if (change.Before.Equals(state))
                _textChanges.Remove(component);
        }
        else
        {
            var before = ReadText(component);
            if (!before.Equals(state))
                _textChanges.Add(component, new TextChange{Component = component, Before = before, After = state});
        }

        var node = _nodes.Values.FirstOrDefault(n => n.Original == component.transform);
        if (node?.Copy == null)
            return;
        var type = component.GetType();
        int index = Array.IndexOf(component.GetComponents(type), component);
        var copies = node.Copy.GetComponents(type);
        if (index >= 0 && index < copies.Length)
            WriteText(copies[index], state);
        _renderNeeded = true;
    }
}
