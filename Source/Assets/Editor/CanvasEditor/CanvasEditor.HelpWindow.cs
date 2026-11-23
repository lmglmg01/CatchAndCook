using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>각 영역의 물음표에서 바로 여는 읽기 전용 사용 안내입니다.</summary>
public sealed class UIEditorHelpWindow : EditorWindow
{
    [SerializeField]
    string _heading, _content;
    static readonly Dictionary<string, string> Guides = new()
    {["01  Canvas 미리보기"] = "1. 대상을 고릅니다\nHierarchy의 Canvas 또는 UI를 선택합니다. 창의 ‘선택 따라가기’를 끄거나 ‘대상 고정’을 누르면 다른 항목을 선택해도 Canvas는 유지됩니다.\n\n2. 화면에서 UI를 선택합니다\n미리보기에서 보이는 UI를 클릭합니다. 숨김 UI 미리보기는 선택한 UI와 부모를 복제본에서만 표시합니다. 비활성 자식을 전부 켜지는 않습니다.\n\n3. 화면 비율과 크기를 확인합니다\n16:9 등의 비율을 고르고 ‘맞춤’ 또는 ‘100%’로 봅니다. 미리보기는 원본의 위치·크기를 편집하지 않습니다.\n\n4. UIDirector에서 재생합니다\n등장 재생 / 퇴장 재생, 반복, 재생 위치 슬라이더를 사용합니다. 선택한 부모와 모든 자식이 함께 연출되며, 정지·원상 복구로 원래 복제본 상태로 돌아갑니다.\n\n안전 범위\n실제 View 함수나 버튼 클릭 이벤트는 미리보기에서 실행하지 않습니다. 게임 데이터와 원본 활성 상태를 바꾸지 않습니다.", ["02  오브젝트 계층"] = "1. 오브젝트를 찾습니다\n접기 화살표로 자식을 펼치거나 검색창에 오브젝트 이름을 입력합니다. [비활성] 항목도 선택할 수 있습니다.\n\n2. Sprite 조회 범위를 정합니다\n‘하위 오브젝트 포함’을 켜면 선택 계층 아래의 Sprite 참조를 함께 조회합니다. 끄면 선택 오브젝트만 조회합니다.\n\n3. 선택 결과를 확인합니다\n선택 테두리와 하단의 사용 중인 Sprite 카드가 갱신됩니다.\n\n이 영역은 오브젝트 선택용입니다. 부모 변경·이동·크기 조절이나 오브젝트 생성/삭제는 수행하지 않습니다.", ["03  사용 중인 Sprite"] = "1. 이미지를 확인합니다\n같은 Sprite는 하나의 카드로 묶어 표시합니다. 가로로 스크롤하여 나머지 카드를 볼 수 있습니다.\n\n2. 사용 위치를 고릅니다\n카드를 클릭하면 우측에 정확한 오브젝트·컴포넌트·필드가 표시됩니다. 같은 이미지가 여러 위치에 쓰이면 사용 위치 드롭다운에서 하나를 지정합니다.\n\n3. 조회 범위\nImage, 버튼 상태 이미지, 스크립트에 저장된 Sprite 필드와 비활성 하위 UI를 함께 조회합니다. 실제 화면에 표시되는 이미지는 해당 UI 상태와 기존 코드에 따라 달라집니다.", ["04  리소스 확인 / 교체"] = "1. 교체할 위치를 확인합니다\n카드 선택 후 사용 위치에서 교체할 참조 하나를 지정합니다. 오브젝트·컴포넌트·필드 이름을 먼저 확인하세요.\n\n2. 교체 이미지를 지정합니다\nSprite를 선택하거나 필드에 드래그한 뒤 ‘교체 미리보기’를 누릅니다. Image.sprite는 Canvas에 바로 미리보기됩니다. 버튼 상태·스크립트 필드는 선택된 참조와 이미지를 확인합니다.\n\n3. 원본에 적용합니다\n하단 ‘변경 적용’으로 기록합니다. 실행 취소, 다시 실행, 변경 버리기는 적용 전 변경에 사용합니다.\n\n4. 저장 범위\n씬 인스턴스는 해당 인스턴스에, 프리팹 편집 중이면 열린 프리팹 내용에 반영합니다. 이후 Unity의 저장 기능을 사용합니다. 위치·크기·색·투명도·기존 버튼 연결과 이미지 원본 파일은 유지합니다.", ["02  기존 함수 연결 · DOTween"] = "1. 연출 대상을 확인합니다\n대상 필드에 지정된 부모와 모든 자식 UI가 함께 연출됩니다. 바로 아래 Sprite 목록은 선택 계층의 실제 Image를 확인하는 보기 전용입니다. 가로 스크롤과 카드의 경로 툴팁을 사용할 수 있습니다.\n\n2. 제어 View를 고릅니다\n현재 Canvas 내부의 View 중 대상을 열고 닫는 View를 선택합니다.\n\n3. 등장·퇴장 함수를 직접 고릅니다\n기존 함수의 이름·인자·버튼 이벤트는 유지합니다. 하나의 bool 함수로 양쪽을 지정하면 true=등장, false=퇴장입니다. 오버로드, async, ref/out, 구문 오류, 조건부 본문 등 안전하게 연결하기 어려운 함수는 이유를 표시합니다.\n\n4. 실행 순서\n등장은 기존 처리가 먼저 실행된 뒤 연출됩니다. 퇴장은 연출이 완료된 뒤 기존 숨김 처리가 실행됩니다. 재생 중 입력 차단과 중복 요청 무시를 선택할 수 있습니다.\n\n5. 적용\n처음 적용 시 기존 함수 안에 관리 구간 한 곳을 연결합니다. 이후 효과 수정은 같은 설정을 갱신합니다. 코드 원본은 Library/UIDirector/Backups에 보관합니다. 같은 속성을 제어하는 기존 View 자체 연출과 겹치지 않도록 고르세요.", ["03  효과 목록 · 같은 효과 중복 사용 가능"] = "1. 방향을 고릅니다\n등장과 퇴장은 각각 별도의 효과 목록을 가집니다.\n\n2. 효과를 추가합니다\n아래 목록에서 투명도, 크기, 이동, 회전을 고르고 ‘연출 추가’를 누릅니다. 동일한 효과를 여러 번 추가할 수 있습니다.\n\n3. 효과를 편집합니다\n효과 카드를 선택하면 우측 설정과 강조되는 코드가 바뀝니다. 체크박스로 사용 여부를 바꾸고 복제·삭제·↑·↓로 구성을 정합니다.\n\n4. 순서를 확인합니다\n처음부터 / 이전 효과와 함께 / 이전 효과 다음을 설정합니다. 같은 속성의 시간이 겹치면 경고를 표시하고 적용을 막습니다.\n\n5. 초기화\n‘삭제 / 초기화’에서 선택 효과, 현재 방향, 등장·퇴장 전체를 지울 수 있습니다. ‘변경 적용’을 눌러 저장된 효과에도 반영합니다. 함수 선택과 기존 버튼 이벤트는 유지됩니다.", ["04  효과 설정 / 연출 코드"] = "1. 시간과 값을 입력합니다\n지속시간은 0.01초 이상, 지연은 0초 이상입니다. 투명도는 0~1, 크기는 원래 크기 대비 배율, 이동은 anchoredPosition 대비 차이, 회전은 원래 Z 대비 각도입니다. 자식의 개별 배치·개별 투명도는 유지됩니다.\n\n2. Ease를 선택합니다\n‘곡선 선택’으로 별도 창을 엽니다. 카드에 마우스를 올려 짧게 재생하거나 클릭하여 상세 그래프와 위치·크기·투명도·회전을 비교합니다. 확정하면 현재 효과에 적용됩니다.\n\n3. 코드를 확인합니다\n‘연출 코드’는 선택한 기존 함수의 연결 코드와 현재 설정에 대응하는 DOTween 코드를 읽기 전용으로 보여줍니다. 선택한 효과는 강조됩니다.\n\n4. 적용·저장\n‘변경 적용’은 기존 함수 연결과 연출 설정을 기록합니다. 같은 대상을 반복 적용해도 함수·컴포넌트·버튼 이벤트가 중복 생성되지 않습니다. 컴파일 후 Unity에서 씬/프리팹을 저장하세요.\n\n5. 실행 취소·초기화\n편집기 실행 취소는 적용 전 설정을, Unity Undo는 적용된 컴포넌트 설정을 복원합니다. 초기화 후에도 다른 씬의 연결을 위해 무해한 호출 구간은 남으며, 효과가 없으면 기존 함수가 즉시 실행됩니다."};
    /// <summary>선택한 영역의 설명을 읽기 전용 도움말 창으로 엽니다.</summary>
    public static void Open(string heading)
    {
        var window = CreateInstance<UIEditorHelpWindow>();
        window.titleContent = new GUIContent("사용 안내");
        window._heading = heading;
        window._content = Guides.TryGetValue(heading, out var text) ? text : "사용 방법 탭에서 전체 안내를 확인할 수 있습니다.";
        if (heading.StartsWith("03  효과 목록") || heading.StartsWith("04  효과 설정"))
            window._content += "\n\n상시 반복\nUI가 표시되는 동안 계속 재생합니다. 왕복 반복 / 처음부터 반복을 선택하고 현재 효과 재생으로 확인합니다. 퇴장·비활성 시 중지합니다.\n\n상호작용 반응\n반응할 버튼을 지정하면 클릭할 때마다 재생합니다. 기존 버튼 이벤트는 유지합니다. 상시 반복을 잠시 멈췄다가 반응 완료 후 다시 시작합니다.\n\n모든 연출 초기화는 네 가지 트랙의 효과를 모두 비웁니다.";
        window._content += "\n\n기본 테스트 화면\nCanvas 미선택으로 열면 테스트 UI가 표시됩니다. 현재 씬에 생성하지 않으며, 테스트 UI는 미리보기 전용으로 변경 적용·저장·프리팹 열기를 지원하지 않습니다. 실제 Canvas를 선택하면 그 대상으로 전환됩니다.\n\n함수 연결 4종\n기존 함수 연결 영역의 상시 반복 시작 함수와 상호작용 반응 함수도 지정할 수 있습니다. 미지정 시 자동 반복/반응할 버튼을 사용합니다. 지정 시 해당 함수가 호출될 때 시작하며 원래 본문은 유지됩니다. 반복·반응 함수는 서로, 그리고 등장·퇴장과 구분하세요.\n\n게임 UI 프리셋\n등장 8종 / 퇴장 8종 / 반복 5종 / 반응 6종입니다. 유형별 목록에서 연출 추가 후 시간·이동량·크기·곡선을 수정하고 현재 효과 재생으로 확인하세요. 반복 프리셋은 왕복 반복을 기본 적용합니다.";
        if (heading.Contains("리소스 확인"))
            window._content += "\n\n폴더에서 Sprite 찾기\n교체할 Sprite 아래 버튼을 누르면 왼쪽 폴더 계층과 오른쪽 썸네일로 찾아볼 수 있습니다. 현재 폴더 내 이름 검색, 폴더 더블클릭, Sprite 선택/더블클릭을 지원합니다. 선택은 교체 후보만 지정합니다. 교체 미리보기 후 변경 적용하세요. 사용 위치가 바뀌면 탐색창을 다시 여세요.";
        if (heading.Contains("계층") || heading.Contains("미리보기") || heading.Contains("기존 함수"))
            window._content += "\n\n미리보기 표시 조절\n계층 행의 체크 또는 선택 대상의 미리보기에서 표시로 켜고 끕니다. 켜면 필요한 부모도 표시합니다. 표시 초기화는 원본 상태와 숨김 UI 미리보기 옵션을 따릅니다. 원본 활성 상태는 변경·저장하지 않으며 다른 Canvas를 읽으면 초기화됩니다.";
        window._content += "\n\n추가 조작\n계층 체크는 오른쪽 끝에 있습니다. 상단 두 버튼은 전체 펼침/접힘과 전체 표시/숨김을 전환합니다. 텍스트 편집에서는 폰트·크기·정렬·색상을 미리보고 기존 변경 적용으로 반영합니다. 휠은 미리보기 확대/축소, Space+드래그는 시점 이동이며 맞춤/100%로 초기화합니다. 테스트 UI에서는 원본 저장을 지원하지 않습니다.";
        if (heading.Contains("내 저장 연출")) window._content = "1. 4번 영역 아래의 연출 저장하기 버튼으로 저장 창을 엽니다. 이름을 입력하고 현재 유형 / 전체 4유형을 선택해 새 연출로 저장합니다. 테스트 UI 저장은 지원하지 않습니다.\n\n2. 4번 영역 아래의 연출 불러오기 버튼으로 불러오기 창을 엽니다. 저장한 연출 목록에서 선택한 뒤 교체하여 불러오기 또는 기존 효과 뒤에 추가를 누릅니다. 기존 대상·함수·버튼 연결은 유지합니다.\n\n3. 미리보기 후 변경 적용을 누릅니다. 불러오기는 실행 취소할 수 있으며 저장본을 바꾸지 않습니다.\n\n4. 선택한 저장본 덮어쓰기로 조합을 갱신합니다. 에셋은 Assets/Editor/CanvasEditor/Presets에 보관하며 Unity를 다시 열어도 남습니다.";
        window.minSize = new Vector2(450, 420);
        window.position = new Rect(160, 130, 570, 650);
        window.ShowUtility();
    }

    /// <summary>제목과 설명을 스크롤로 확인하는 도움말 화면을 구성합니다.</summary>
    public void CreateGUI()
    {
        var root = rootVisualElement;
        root.AddToClassList("canvas-editor");
        var style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/CanvasEditor/CanvasEditor.uss");
        if (style != null)
            root.styleSheets.Add(style);
        var title = new Label(_heading);
        title.AddToClassList("help-heading");
        root.Add(title);
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("help-page");
        root.Add(scroll);
        var body = new Label(_content);
        body.AddToClassList("help-text");
        body.AddToClassList("area-help-text");
        scroll.Add(body);
        root.Add(new Button(Close)
        {text = "닫기"});
        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Escape)
            {
                Close();
                e.StopPropagation();
            }
        });
    }
}
