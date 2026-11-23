using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using U = UnityEngine.UI;

public sealed partial class CanvasEditor
{
    readonly struct ReferenceKey : IEquatable<ReferenceKey>
    {
        public readonly Component Component;
        public readonly string Property;
        public ReferenceKey(Component component, string property)
        {
            Component = component;
            Property = property;
        }

        /// <summary>참조 또는 설정의 구성 값을 비교합니다.</summary>
        public bool Equals(ReferenceKey other) => ReferenceEquals(Component, other.Component) && Property == other.Property;
        /// <summary>참조 또는 설정의 구성 값을 비교합니다.</summary>
        public override bool Equals(object obj) => obj is ReferenceKey other && Equals(other);
        /// <summary>사전의 키에 사용할 참조 해시를 계산합니다.</summary>
        public override int GetHashCode() => unchecked((ReferenceEquals(Component, null) ? 0 : Component.GetInstanceID()) * 397 ^ (Property?.GetHashCode() ?? 0));
    }

    sealed class SpriteReference
    {
        public ReferenceKey Key;
        public Sprite Source;
        public string Location;
    }

    sealed class SpriteChange
    {
        public ReferenceKey Key;
        public Sprite Before, After;
    }

    readonly struct DraftStep
    {
        public readonly ReferenceKey Key;
        public readonly Sprite Before, After;
        public readonly Component TextComponent;
        public readonly TextState BeforeText, AfterText;
        public DraftStep(ReferenceKey key, Sprite before, Sprite after)
        {
            Key = key;
            Before = before;
            After = after;
            TextComponent = null;
            BeforeText = AfterText = default;
        }

        public DraftStep(Component component, TextState before, TextState after)
        {
            Key = default;
            Before = After = null;
            TextComponent = component;
            BeforeText = before;
            AfterText = after;
        }
    }

    readonly List<SpriteReference> _references = new();
    readonly Dictionary<ReferenceKey, SpriteChange> _changes = new();
    readonly Stack<DraftStep> _history = new(), _redo = new();
    SpriteReference _selectedReference;
    Sprite _replacementSprite;
    bool _refreshingDetails;
    Toggle _includeChildren;
    ScrollView _spriteScroll;
    Label _resourceCount, _resourceName, _resourcePath, _usageHint, _assetDimensions;
    UnityEngine.UIElements.Image _assetPreview, _replacementPreview;
    PopupField<string> _usagePopup;
    ObjectField _replacementField;
    Button _previewChangeButton, _locateButton, _browseSpritesButton;
#region 리소스 선택 화면
    /// <summary>사용 중인 Sprite 종류를 보여주는 목록을 구성합니다.</summary>
    void BuildResourcePalette(VisualElement parent)
    {
        _resourceCount = new Label();
        _resourceCount.AddToClassList("resource-count");
        parent.Add(_resourceCount);
        _spriteScroll = new ScrollView(ScrollViewMode.Horizontal)
        {horizontalScrollerVisibility = ScrollerVisibility.AlwaysVisible, verticalScrollerVisibility = ScrollerVisibility.Hidden};
        _spriteScroll.AddToClassList("sprite-palette");
        parent.Add(_spriteScroll);
        var hint = new Label("Sprite 카드 → 사용 위치 선택 → 교체 미리보기");
        hint.AddToClassList("palette-hint");
        parent.Add(hint);
    }

    /// <summary>사용 위치와 교체 후보를 확인하는 리소스 설정 영역을 구성합니다.</summary>
    void BuildResourceDetails(VisualElement parent)
    {
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("resource-details");
        parent.Add(scroll);
        var summary = Row(scroll, "asset-summary");
        _assetPreview = new UnityEngine.UIElements.Image{scaleMode = ScaleMode.ScaleToFit};
        _assetPreview.AddToClassList("asset-thumbnail");
        summary.Add(_assetPreview);
        _resourceName = new Label("Sprite를 선택하세요");
        _resourceName.AddToClassList("asset-name");
        summary.Add(_resourceName);
        _resourcePath = new Label();
        _resourcePath.AddToClassList("asset-path");
        scroll.Add(_resourcePath);
        _assetDimensions = new Label();
        _assetDimensions.AddToClassList("secondary-label");
        scroll.Add(_assetDimensions);
        _usagePopup = new PopupField<string>("사용 위치", new List<string>{"사용 위치 없음"}, 0);
        _usagePopup.AddToClassList("stacked-field");
        scroll.Add(_usagePopup);
        _usagePopup.RegisterValueChangedCallback(e =>
        {
            if (_refreshingDetails || _selectedReference == null)
                return;
            var group = _references.Where(r => EffectiveSprite(r) == EffectiveSprite(_selectedReference)).ToList();
            int index = _usagePopup.index;
            if (index >= 0 && index < group.Count)
            {
                _selectedReference = group[index];
                RefreshResourceDetails();
                HighlightReference();
            }
        });
        _replacementField = new ObjectField("교체할 Sprite")
        {objectType = typeof(Sprite), allowSceneObjects = false};
        _replacementField.AddToClassList("stacked-field");
        scroll.Add(_replacementField);
        _replacementField.RegisterValueChangedCallback(e =>
        {
            if (_refreshingDetails)
                return;
            _replacementSprite = e.newValue as Sprite;
            RefreshReplacement();
        });
        _replacementField.RegisterCallback<DragUpdatedEvent>(e =>
        {
            if (TryDraggedSprite(out _))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.StopPropagation();
            }
        });
        _replacementField.RegisterCallback<DragPerformEvent>(e =>
        {
            if (!TryDraggedSprite(out var sprite))
                return;
            DragAndDrop.AcceptDrag();
            _replacementField.value = sprite;
            e.StopPropagation();
        });
        _browseSpritesButton = AddButton(scroll, "폴더에서 Sprite 찾기", OpenResourceSpriteBrowser);
        _browseSpritesButton.name = "browse-replacement-sprites";
        _browseSpritesButton.tooltip = "폴더 계층과 썸네일을 보며 교체 이미지를 찾습니다. 선택 후 교체 미리보기로 확인하세요.";
        _replacementPreview = new UnityEngine.UIElements.Image{scaleMode = ScaleMode.ScaleToFit};
        _replacementPreview.AddToClassList("replacement-thumbnail");
        scroll.Add(_replacementPreview);
        _usageHint = new Label();
        _usageHint.AddToClassList("usage-hint");
        scroll.Add(_usageHint);
        var buttons = Row(parent, "detail-actions");
        _locateButton = AddButton(buttons, "프로젝트에서 찾기", () =>
        {
            var sprite = _selectedReference != null ? EffectiveSprite(_selectedReference) : null;
            if (sprite != null)
            {
                EditorUtility.FocusProjectWindow();
                EditorGUIUtility.PingObject(sprite);
            }
        });
        _previewChangeButton = AddButton(buttons, "교체 미리보기", PreviewReplacement);
        _previewChangeButton.AddToClassList("primary-button");
    }

    /// <summary>현재 Sprite 폴더에서 안전한 교체 후보 탐색을 시작합니다.</summary>
    void OpenResourceSpriteBrowser()
    {
        if (_selectedReference == null || !CanEditSource())
            return;
        var key = _selectedReference.Key;
        UIEditorSpriteBrowser.Open(_replacementSprite, sprite =>
        {
            if (this == null || _selectedReference == null || !_selectedReference.Key.Equals(key) || !CanEditSource())
                return false;
            _replacementField.value = sprite;
            return true;
        });
    }

    /// <summary>드래그한 Sprite 또는 단일 Sprite 텍스처를 판별합니다.</summary>
    static bool TryDraggedSprite(out Sprite sprite)
    {
        sprite = DragAndDrop.objectReferences.OfType<Sprite>().FirstOrDefault();
        if (sprite != null)
            return true;
        foreach (var texture in DragAndDrop.objectReferences.OfType<Texture2D>())
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture)).OfType<Sprite>().ToArray();
            if (sprites.Length == 1)
            {
                sprite = sprites[0];
                return true;
            }
        }

        return false;
    }

#endregion
#region Sprite 참조 조회
    /// <summary>선택 계층의 컴포넌트에서 Sprite 참조 위치를 조회합니다.</summary>
    List<SpriteReference> ReadSpriteReferences(Transform original, bool children)
    {
        var result = new List<SpriteReference>();
        if (original == null)
            return result;
        var components = children ? original.GetComponentsInChildren<Component>(true) : original.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == null)
                continue;
            using var data = new SerializedObject(component);
            var property = data.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;
                var sprite = property.objectReferenceValue as Sprite;
                bool emptyImage = property.objectReferenceValue == null && ((component is U.Image && property.propertyPath == "m_Sprite") || (component is SpriteRenderer && property.propertyPath == "m_Sprite"));
                if (sprite == null && !emptyImage)
                    continue;
                var sameType = component.GetComponents(component.GetType()).Where(c => c != null && c.GetType() == component.GetType()).ToArray();
                int index = Array.IndexOf(sameType, component);
                string owner = AnimationUtility.CalculateTransformPath(component.transform, _target.transform);
                string location = (string.IsNullOrEmpty(owner) ? _target.name : owner) + " · " + component.GetType().Name + (sameType.Length > 1 ? $"[{index}]" : "") + "." + property.propertyPath;
                result.Add(new SpriteReference{Key = new ReferenceKey(component, property.propertyPath), Source = sprite, Location = location});
            }
        }

        return result;
    }

    /// <summary>원본과 미적용 변경을 고려한 현재 Sprite를 반환합니다.</summary>
    Sprite EffectiveSprite(SpriteReference reference) => _changes.TryGetValue(reference.Key, out var change) ? change.After : reference.Source;
    /// <summary>지정한 컴포넌트 필드의 Sprite 참조를 읽습니다.</summary>
    static bool TryReadSprite(ReferenceKey key, out Sprite sprite)
    {
        sprite = null;
        if (key.Component == null)
            return false;
        using var data = new SerializedObject(key.Component);
        var property = data.FindProperty(key.Property);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            return false;
        if (property.objectReferenceValue != null && !(property.objectReferenceValue is Sprite))
            return false;
        sprite = property.objectReferenceValue as Sprite;
        return true;
    }

    /// <summary>같은 Sprite의 사용 위치를 묶어 목록과 상세 정보를 갱신합니다.</summary>
    void RefreshSpriteList()
    {
        if (_spriteScroll == null)
            return;
        var oldKey = _selectedReference?.Key;
        _references.Clear();
        if (_picked != null && _nodes.TryGetValue(_picked, out var node) && node.Original != null)
            _references.AddRange(ReadSpriteReferences(node.Original, _includeChildren?.value ?? true));
        _selectedReference = oldKey.HasValue ? _references.FirstOrDefault(r => r.Key.Equals(oldKey.Value)) : null;
        if (_selectedReference == null)
            _selectedReference = _references.FirstOrDefault(r => EffectiveSprite(r) != null) ?? _references.FirstOrDefault();
        Vector2 offset = _spriteScroll.scrollOffset;
        _spriteScroll.Clear();
        var groups = _references.GroupBy(EffectiveSprite).OrderBy(g => g.Key == null ? "\uffff" : g.Key.name, StringComparer.OrdinalIgnoreCase).ToArray();
        _resourceCount.text = $"{groups.Length}종 · 참조 {_references.Count}곳 · 같은 Sprite는 묶어서 표시";
        if (groups.Length == 0)
        {
            var empty = new Label(_picked == null ? "오브젝트를 선택하세요." : "이 범위에 연결된 Sprite가 없습니다.");
            empty.AddToClassList("palette-empty");
            _spriteScroll.Add(empty);
        }

        Button selectedCard = null;
        foreach (var group in groups)
        {
            var reference = group.First();
            var sprite = group.Key;
            var card = new Button(() =>
            {
                _selectedReference = reference;
                RefreshSpriteList();
                HighlightReference();
            });
            card.AddToClassList("resource-card");
            card.EnableInClassList("selected-item", _selectedReference != null && EffectiveSprite(_selectedReference) == sprite);
            if (_selectedReference != null && EffectiveSprite(_selectedReference) == sprite)
                selectedCard = card;
            var image = new UnityEngine.UIElements.Image{sprite = sprite, scaleMode = ScaleMode.ScaleToFit};
            image.AddToClassList("resource-card-image");
            card.Add(image);
            var name = new Label(sprite != null ? sprite.name : "Sprite 미지정");
            name.AddToClassList("resource-card-name");
            card.Add(name);
            var count = new Label($"사용 {group.Count()}곳");
            count.AddToClassList("secondary-label");
            card.Add(count);
            card.tooltip = (sprite != null ? sprite.name : "아직 Sprite가 없는 Image") + $"\n사용 {group.Count()}곳 · 우측에서 사용 위치를 선택하세요.";
            _spriteScroll.Add(card);
        }

        _spriteScroll.scrollOffset = offset;
        if (selectedCard != null)
        {
            var card = selectedCard;
            _spriteScroll.schedule.Execute(() =>
            {
                if (card.parent != null)
                    _spriteScroll.ScrollTo(card);
            }).StartingIn(1);
        }

        RefreshResourceDetails();
    }

    /// <summary>선택한 사용 위치와 교체 후보 및 안내를 갱신합니다.</summary>
    void RefreshResourceDetails()
    {
        if (_resourceName == null)
            return;
        _refreshingDetails = true;
        try
        {
            var reference = _selectedReference;
            var sprite = reference != null ? EffectiveSprite(reference) : null;
            _assetPreview.sprite = sprite;
            _resourceName.text = reference == null ? "Sprite를 선택하세요" : sprite != null ? sprite.name : "Sprite 미지정";
            _resourcePath.text = sprite != null ? AssetDatabase.GetAssetPath(sprite) : "";
            _resourcePath.tooltip = _resourcePath.text;
            if (sprite != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId))
                _assetDimensions.text = $"{sprite.rect.width:0} × {sprite.rect.height:0} px · ID {localId}";
            else
                _assetDimensions.text = "";
            var group = reference == null ? new List<SpriteReference>() : _references.Where(r => EffectiveSprite(r) == sprite).ToList();
            var labels = group.Count == 0 ? new List<string>{"사용 위치 없음"} : group.Select(r => r.Location).ToList();
            _usagePopup.choices = labels;
            _usagePopup.SetValueWithoutNotify(labels[Mathf.Max(0, group.IndexOf(reference))]);
            _usagePopup.SetEnabled(group.Count > 0);
            _replacementSprite = sprite;
            _replacementField.SetValueWithoutNotify(sprite);
            _replacementField.SetEnabled(reference != null && CanEditSource());
            _locateButton.SetEnabled(sprite != null);
            _browseSpritesButton.SetEnabled(reference != null && CanEditSource());
            if (reference == null)
                _usageHint.text = "";
            else if (IsTestCanvas)
                _usageHint.text = "테스트 UI · 교체 미리보기만 지원합니다. 적용과 저장은 비활성화됩니다.";
            else if (EditorUtility.IsPersistent(_target))
                _usageHint.text = "프리팹 에셋은 읽기 전용입니다. 원본 프리팹을 열어 편집하세요.";
            else if (reference.Key.Component is U.Image && reference.Key.Property == "m_Sprite")
                _usageHint.text = "선택한 Image 참조 하나만 교체합니다. 크기·색상·투명도는 유지합니다.";
            else if (reference.Key.Component is U.Selectable)
                _usageHint.text = "버튼 상태용 Sprite입니다. 실제 표시는 해당 상태에서 확인하세요.";
            else
                _usageHint.text = "스크립트/컴포넌트의 Sprite 참조입니다. 화면 반영은 기존 사용 코드에 따릅니다.";
        }
        finally
        {
            _refreshingDetails = false;
            RefreshReplacement();
        }
    }

    /// <summary>교체 후보의 이미지와 미리보기 버튼 상태를 갱신합니다.</summary>
    void RefreshReplacement()
    {
        if (_replacementPreview == null)
            return;
        _replacementPreview.sprite = _replacementSprite;
        bool changed = _selectedReference != null && EffectiveSprite(_selectedReference) != _replacementSprite;
        Display(_replacementPreview, changed && _replacementSprite != null);
        _previewChangeButton.SetEnabled(changed && CanEditSource());
    }

    /// <summary>선택한 Sprite 사용 위치를 미리보기 테두리로 표시합니다.</summary>
    void HighlightReference()
    {
        if (_selectedReference == null || _selectedReference.Key.Component == null)
            return;
        var node = _nodes.Values.FirstOrDefault(n => n.Original == _selectedReference.Key.Component.transform);
        if (node?.Copy is RectTransform rect && rect.gameObject.activeInHierarchy)
        {
            Canvas.ForceUpdateCanvases();
            var bounds = OutlineBounds(rect);
            Display(_outline, true);
            _outline.style.left = bounds.x;
            _outline.style.top = bounds.y;
            _outline.style.width = bounds.width;
            _outline.style.height = bounds.height;
        }
    }

#endregion
#region 복제본 미리보기 변경
    /// <summary>Sprite 교체를 이력에 기록하고 복제본에만 미리 반영합니다.</summary>
    void PreviewReplacement()
    {
        if (_selectedReference == null || !CanEditSource())
            return;
        var key = _selectedReference.Key;
        var before = EffectiveSprite(_selectedReference);
        if (before == _replacementSprite)
            return;
        _history.Push(new DraftStep(key, before, _replacementSprite));
        _redo.Clear();
        SetDraftValue(key, _replacementSprite);
        SetMessage("선택한 Sprite 참조를 미리보기로 변경했습니다. 원본에는 아직 반영하지 않았습니다.");
        RefreshSpriteList();
        RefreshStatus();
    }

    /// <summary>Sprite 참조의 미적용 변경을 추가하거나 원래 값으로 되돌립니다.</summary>
    void SetDraftValue(ReferenceKey key, Sprite value)
    {
        if (!TryReadSprite(key, out var original))
            return;
        if (_changes.TryGetValue(key, out var change))
        {
            change.After = value;
            if (change.Before == value)
                _changes.Remove(key);
        }
        else if (original != value)
            _changes.Add(key, new SpriteChange{Key = key, Before = original, After = value});
        ApplyPreviewValue(key, value);
        _renderNeeded = true;
    }

    /// <summary>원본에 대응하는 복제본 컴포넌트에 Sprite를 반영합니다.</summary>
    void ApplyPreviewValue(ReferenceKey key, Sprite value)
    {
        if (key.Component == null)
            return;
        var node = _nodes.Values.FirstOrDefault(n => n.Original == key.Component.transform);
        if (node?.Copy == null)
            return;
        var type = key.Component.GetType();
        var originals = key.Component.GetComponents(type).Where(c => c != null && c.GetType() == type).ToArray();
        int index = Array.IndexOf(originals, key.Component);
        var copies = node.Copy.GetComponents(type).Where(c => c != null && c.GetType() == type).ToArray();
        if (index < 0 || index >= copies.Length)
            return;
        using var data = new SerializedObject(copies[index]);
        var property = data.FindProperty(key.Property);
        if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            return;
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        if (copies[index] is U.Graphic graphic)
            graphic.SetAllDirty();
    }

#endregion
#region 변경 이력과 원본 적용
    /// <summary>가장 최근 Sprite 또는 텍스트 미리보기 변경을 취소합니다.</summary>
    void UndoDraft()
    {
        if (_history.Count == 0)
            return;
        var step = _history.Pop();
        if (!ReferenceEquals(step.TextComponent, null))
            SetTextDraftValue(step.TextComponent, step.BeforeText);
        else
            SetDraftValue(step.Key, step.Before);
        _redo.Push(step);
        SetMessage("미리보기 변경을 취소했습니다.");
        RefreshSpriteList();
        RefreshStatus();
    }

    /// <summary>취소한 Sprite 또는 텍스트 변경을 다시 미리 반영합니다.</summary>
    void RedoDraft()
    {
        if (_redo.Count == 0)
            return;
        var step = _redo.Pop();
        if (!ReferenceEquals(step.TextComponent, null))
            SetTextDraftValue(step.TextComponent, step.AfterText);
        else
            SetDraftValue(step.Key, step.After);
        _history.Push(step);
        SetMessage("미리보기 변경을 다시 적용했습니다.");
        RefreshSpriteList();
        RefreshStatus();
    }

    /// <summary>리소스와 텍스트 미적용 변경을 버리고 원본을 다시 읽습니다.</summary>
    void DiscardDraft()
    {
        if (_target != null)
            Rebuild(_target);
    }

    /// <summary>참조 충돌을 확인한 뒤 Sprite와 텍스트 변경을 원본에 함께 적용합니다.</summary>
    void ApplyDraft()
    {
        if (!CanSaveSource() || ResourceChangeCount == 0)
            return;
        foreach (var change in _changes.Values)
            if (change.Key.Component == null || !change.Key.Component.transform.IsChildOf(_target.transform) || !TryReadSprite(change.Key, out var current) || current != change.Before)
            {
                SetMessage("원본 Sprite 참조가 변경되었습니다. 변경을 버리고 다시 읽은 뒤 적용해 주세요.");
                return;
            }

        foreach (var change in _textChanges.Values)
            if (change.Component == null || !change.Component.transform.IsChildOf(_target.transform) || !ReadText(change.Component).Equals(change.Before))
            {
                SetMessage("원본 텍스트가 변경되었습니다. 변경을 버리고 다시 읽은 뒤 적용해 주세요.");
                return;
            }

        _applying = true;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Change UI Appearance");
        int count = ResourceChangeCount;
        try
        {
            foreach (var change in _changes.Values)
            {
                var component = change.Key.Component;
                Undo.RecordObject(component, "Change UI Sprite");
                using var data = new SerializedObject(component);
                var property = data.FindProperty(change.Key.Property);
                if (property == null)
                    throw new InvalidOperationException("Sprite 참조 필드를 찾지 못했습니다.");
                property.objectReferenceValue = change.After;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(component);
                if (component.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }

            foreach (var change in _textChanges.Values)
            {
                var component = change.Component;
                Undo.RecordObject(component, "Change UI Text");
                WriteText(component, change.After);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(component);
                if (component.gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            }

            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            _changes.Clear();
            _textChanges.Clear();
            _history.Clear();
            _redo.Clear();
            Rebuild(_target);
            SetMessage($"Sprite·텍스트 {count}건 적용 완료 · 씬/프리팹 저장은 Unity 저장 기능을 사용하세요.");
        }
        catch (Exception ex)
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            SetMessage("적용 중 오류가 발생해 이번 변경을 되돌렸습니다. " + ex.Message);
            Debug.LogException(ex);
        }
        finally
        {
            _applying = false;
            RefreshStatus();
        }
    }
#endregion
}
