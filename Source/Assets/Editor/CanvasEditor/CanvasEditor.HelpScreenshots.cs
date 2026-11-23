using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class CanvasEditor
{
#region 실제 화면 자료
    const string HelpScreenshotFolder = "Assets/Editor/CanvasEditor/HelpImages/";

    /// <summary>현재 카테고리에 해당하는 실제 촬영 이미지와 확인할 부분을 연결합니다.</summary>
    static void BuildHelpScreenshots(VisualElement parent, string mode, string kind)
    {
        string[][] photos;
        switch (kind)
        {
            case "selection": photos = new[] { new[] { "Hierarchy", "미리보기와 계층 선택", "왼쪽의 파란 선택 테두리와 오른쪽 AnimatedPanel 선택 행이 같은 대상을 가리킵니다. 계층 오른쪽 체크로 미리보기 표시를 조절합니다." } }; break;
            case "sprite": photos = new[] { new[] { "SpriteBrowser", "폴더에서 Sprite 찾기", "왼쪽에서 폴더를 고르고 오른쪽 썸네일에서 Sprite를 선택합니다. 선택 후 기존 편집창의 교체 미리보기를 사용합니다." }, new[] { "Resource", "교체할 사용 위치 확인", "하단 왼쪽의 사용 중 Sprite를 고른 뒤 하단 오른쪽의 사용 위치와 교체할 Sprite를 확인합니다." } }; break;
            case "text": photos = new[] { new[] { "Text", "텍스트 편집창", "위에서 편집할 텍스트를 고릅니다. 폰트, 폰트 크기, 자동 크기, 정렬, 텍스트 색상을 지정한 뒤 미리보기를 누릅니다." } }; break;
            case "hooks": photos = new[] { new[] { "Hooks", "기존 함수 연결 영역 확대", "연출 대상을 확인하고 제어 View를 선택합니다. 아래에서 등장·퇴장·상시 반복·상호작용 반응 함수를 지정합니다. 사진의 선택 안 함은 아직 함수를 지정하지 않은 예시입니다." }, new[] { "Director", "함수 연결 영역의 위치", "우측 상단 02 기존 함수 연결 영역을 확인합니다. 대상의 Sprite 썸네일은 확인용입니다." } }; break;
            case "timeline": photos = new[] { new[] { "Settings", "효과 목록과 시간 설정 확대", "왼쪽의 효과를 선택하면 오른쪽에서 지속시간, 지연, 실행 방식, 시작값과 끝값을 설정할 수 있습니다." }, new[] { "Ease", "Ease 선택창", "곡선 카드를 선택하고 오른쪽의 움직임을 비교합니다. 곡선 선택 확정으로 현재 효과에 반영합니다." } }; break;
            case "loop": photos = new[] { new[] { "Loop", "상시 반복 설정", "가운데 상시 반복 유형을 선택합니다. 오른쪽에서 왕복 반복 여부와 효과 시간을 지정합니다." }, new[] { "Reaction", "상호작용 반응 설정", "상호작용 반응 유형에서 반응할 버튼과 효과를 지정합니다. 기존 버튼 이벤트는 유지됩니다." } }; break;
            case "preview": photos = new[] { new[] { "Code", "미리보기와 연출 코드", "상단 미리보기 아래의 재생·정지 버튼으로 확인합니다. 하단 오른쪽 연출 코드 탭에서 현재 효과의 코드를 읽습니다." } }; break;
            case "save": photos = new[] { new[] { "Save", "하단 적용·저장 영역", "이 사진은 기본 테스트 UI여서 적용·저장이 비활성화된 상태입니다. 실제 Canvas에서는 변경 적용 후 씬·프리팹을 저장합니다." } }; break;
            default: photos = new[] { new[] { mode == "ResourceChanger" ? "Resource" : "Director", mode + " 전체 화면", "01은 Canvas 미리보기, 02는 선택과 연결, 03~04는 리소스 또는 효과 설정 영역입니다. 기본 테스트 UI에서 직접 촬영한 실제 편집창입니다." } }; break;
        }
        var group = new VisualElement();
        group.AddToClassList("help-photos");
        parent.Add(group);
        Help(group, "실제 화면으로 확인하기", "사진을 클릭하거나 크게 보기를 누르면 별도 창에서 확대해 볼 수 있습니다. 사진 속 값은 테스트 예시입니다.");
        for (int i = 0; i < photos.Length; i++)
            AddHelpScreenshot(group, photos[i][0], photos[i][1], photos[i][2], i == 0);
    }

    /// <summary>스크린샷을 비율에 맞춰 표시하고 원본 확대 보기와 설명을 제공합니다.</summary>
    static void AddHelpScreenshot(VisualElement parent, string file, string title, string caption, bool expanded)
    {
        string path = HelpScreenshotFolder + file + ".jpg";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var foldout = new Foldout { text = title, value = expanded };
        foldout.AddToClassList("help-photo");
        parent.Add(foldout);
        if (texture == null)
        {
            foldout.Add(new Label("스크린샷을 찾을 수 없습니다: " + path));
            return;
        }
        var button = new Button(() => UIEditorHelpImageWindow.Open(path, title, caption));
        button.AddToClassList("help-photo-button");
        button.tooltip = "클릭하여 실제 스크린샷 확대";
        var picture = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit };
        picture.pickingMode = PickingMode.Ignore;
        picture.style.height = 320;
        picture.RegisterCallback<GeometryChangedEvent>(e =>
        {
            if (e.newRect.width > 0)
                picture.style.height = Mathf.Clamp(e.newRect.width * texture.height / texture.width, 64, 620);
        });
        button.Add(picture);
        foldout.Add(button);
        var text = new Label(caption);
        text.AddToClassList("help-photo-caption");
        foldout.Add(text);
        foldout.Add(new Button(() => UIEditorHelpImageWindow.Open(path, title, caption)) { text = "크게 보기 · 맞춤 / 100%" });
    }
#endregion
}
