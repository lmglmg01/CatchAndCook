using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class CanvasEditor
{
#region 사용방법 분류와 화면 구성
    sealed class HelpTopic
    {
        public readonly string Title, Visual;
        public readonly string[] Steps, Sections;
        public HelpTopic(string title, string visual, string[] steps, string[] sections)
        {
            Title = title;
            Visual = visual;
            Steps = steps;
            Sections = sections;
        }
    }

    /// <summary>두 모드의 사용방법을 카테고리 목록과 설명 영역으로 나눕니다.</summary>
    void BuildHelp(VisualElement parent)
    {
        _helpPage = new VisualElement();
        _helpPage.AddToClassList("help-browser");
        parent.Add(_helpPage);
        _resourceHelpPage = new VisualElement { name = "resource-help" };
        _directorHelpPage = new VisualElement { name = "director-help" };
        _helpPage.Add(_resourceHelpPage);
        _helpPage.Add(_directorHelpPage);
        BuildModeHelp(_resourceHelpPage, "ResourceChanger", new[]
        {
            new HelpTopic("빠른 시작", "overview", new[] { "Canvas 선택", "오브젝트 선택", "교체 미리보기", "변경 적용" }, new[] { "열기 / 대상 선택", "Tools > Canvas Editor 또는 Ctrl + Shift + F12로 엽니다. Canvas를 먼저 선택하거나 창을 먼저 열어도 됩니다. 기본 모드는 ResourceChanger입니다.", "기본 테스트 화면 / 저장 제한", "Canvas를 선택하지 않고 Tools > Canvas Editor를 열면 기본 테스트 UI가 표시됩니다. 테스트 화면은 현재 씬에 추가되지 않으며, Sprite 교체와 연출은 복제본에서만 시험합니다. 변경 적용·저장·프리팹 열기는 비활성화되고 변경을 저장하지 않습니다. Canvas를 선택한 채 열거나 창을 연 뒤 Hierarchy에서 Canvas 또는 자식 UI를 선택하면 해당 대상으로 전환됩니다. 실제 UI는 기존 적용·저장 기능을 사용합니다." }),
            new HelpTopic("화면 이동 / 계층", "selection", new[] { "미리보기 클릭", "계층 자동 선택", "표시 켜기 / 끄기" }, new[] { "오브젝트와 Sprite 찾기", "미리보기에서 클릭한 오브젝트는 우측 계층에서도 선택되며, 부모 계층을 펼치고 해당 행으로 자동 스크롤합니다. 검색에 가려진 대상은 검색을 해제해 표시합니다. 하위 오브젝트 포함을 켜면 비활성 자식까지 조회합니다. 하단 카드는 같은 Sprite를 묶어 표시하며 가로로 스크롤할 수 있습니다.", "미리보기에서 켜고 끄기", "계층 행 오른쪽 끝의 체크 또는 선택 대상의 미리보기에서 표시를 사용합니다. 검색창 아래의 펼침 화살표는 모두 펼치기/모두 닫기, 체크박스는 모두 켜기/모두 끄기로 전환되며 설명은 옆에 표시됩니다. 펼침 버튼은 검색을 해제하며, 표시 버튼은 미리보기만 변경합니다. 꺼진 오브젝트도 계층에 남으므로 다시 켤 수 있으며, 켜면 필요한 상위 계층도 함께 표시합니다. 표시 초기화는 원본 상태와 숨김 UI 미리보기 옵션을 기준으로 되돌립니다. 이 상태는 미리보기 전용이며 원본 Canvas·프리팹에는 적용하거나 저장하지 않습니다. 다른 Canvas를 읽으면 초기화됩니다.", "계층 일괄 조작", "계층 행의 표시 체크는 오른쪽 끝에 있습니다. 검색창 아래 화살표는 모두 펼치기/모두 닫기, 체크박스는 모두 켜기/모두 끄기로 바뀌고 설명은 각각 옆에 표시됩니다. 펼침 버튼은 검색을 해제하며, 표시 버튼은 현재 Canvas의 미리보기 계층 전체에 적용합니다. 원본 활성 상태는 저장하지 않습니다.", "미리보기 시점", "미리보기 위에서 마우스 휠로 확대·축소합니다. Space를 누른 채 왼쪽 드래그하거나 휠 버튼을 누른 채 드래그해 화면을 옮깁니다. Space 드래그는 UI 선택을 바꾸지 않고 원본 위치·크기도 변경하지 않습니다. 맞춤 또는 100% 버튼을 누르면 이동을 초기화합니다." }),
            new HelpTopic("Sprite 교체", "sprite", new[] { "사용 중 Sprite", "사용 위치 선택", "새 Sprite 선택", "교체 미리보기" }, new[] { "사용 위치 확인과 교체", "카드를 선택하고 우측 사용 위치에서 정확한 컴포넌트와 필드를 고릅니다. 교체할 Sprite를 선택하거나 드래그한 뒤 교체 미리보기를 누릅니다. 선택한 참조 하나만 바뀝니다.", "폴더에서 Sprite 찾기", "교체할 Sprite 아래 폴더에서 Sprite 찾기를 누릅니다. 현재 이미지의 폴더에서 시작하며 왼쪽은 Assets 폴더 계층, 오른쪽은 폴더와 Sprite 썸네일입니다. 폴더 더블클릭으로 이동하고 현재 폴더에서 이름을 검색합니다. Sprite 선택 또는 더블클릭은 교체 후보만 지정합니다. 교체 미리보기와 변경 적용은 기존 순서대로 진행하며, 사용 위치가 바뀌면 탐색창을 다시 열어 주세요.", "미리보기와 실제 표시", "Image.sprite는 Canvas에 즉시 미리보기됩니다. 버튼 상태 이미지와 스크립트 Sprite 필드는 사용 위치와 교체 이미지를 확인할 수 있으며 실제 표시는 해당 상태나 기존 코드에 따라 결정됩니다. 미리보기에서는 게임 코드나 버튼 이벤트를 실행하지 않습니다." }),
            new HelpTopic("텍스트 편집", "text", new[] { "텍스트 선택", "폰트 / 크기 / 정렬 / 색", "미리보기", "변경 적용" }, new[] { "텍스트 편집", "선택 계층에 TMP 또는 일반 UI 텍스트가 있으면 텍스트 편집 버튼이 활성화됩니다. 편집할 텍스트를 고르고 폰트·크기·정렬·색상과 알파값을 변경한 뒤 미리보기를 누릅니다. 직접 크기를 지정할 때는 자동 크기 또는 최적 맞춤을 끕니다. 원래 창의 실행 취소·재실행·변경 버리기를 함께 사용할 수 있고 실제 UI는 변경 적용 뒤 씬/프리팹을 저장합니다. 테스트 UI는 미리보기만 지원합니다." }),
            new HelpTopic("적용 / 저장", "save", new[] { "미리보기 변경", "변경 적용", "씬 / 프리팹 저장" }, new[] { "적용 / 취소 / 프리팹", "미리보기 변경은 실행 취소·다시 실행·변경 버리기로 조정합니다. 변경 적용은 선택한 Sprite 참조와 텍스트 표시 설정을 원본에 기록하며 Unity Undo도 지원합니다. 씬 인스턴스는 해당 인스턴스에, 열린 프리팹은 프리팹 편집 내용에 반영합니다. 저장은 기존 Unity 저장 방식을 사용합니다.", "유지되는 설정", "위치, 크기, 앵커, 색상, 투명도, 이미지 표시 방식과 기존 버튼·스크립트 연결은 유지합니다. Sprite 교체는 원본 이미지 파일이나 모든 사용처를 일괄 수정하지 않습니다. 텍스트 설정은 텍스트 편집에서 별도로 지정한 값만 변경합니다." }),
        });
        BuildModeHelp(_directorHelpPage, "UIDirector", new[]
        {
            new HelpTopic("빠른 시작", "overview", new[] { "대상 선택", "함수 연결", "효과 / Ease 설정", "재생 후 적용" }, new[] { "열기 / 대상 선택", "Tools > Canvas Editor 또는 Ctrl + Shift + F12로 엽니다. Canvas를 먼저 선택하거나 창을 먼저 열어도 됩니다. 기본 모드는 ResourceChanger입니다.", "기본 테스트 화면 / 저장 제한", "Canvas를 선택하지 않고 Tools > Canvas Editor를 열면 기본 테스트 UI가 표시됩니다. 테스트 화면은 현재 씬에 추가되지 않으며, Sprite 교체와 연출은 복제본에서만 시험합니다. 변경 적용·저장·프리팹 열기는 비활성화되고 변경을 저장하지 않습니다. Canvas를 선택한 채 열거나 창을 연 뒤 Hierarchy에서 Canvas 또는 자식 UI를 선택하면 해당 대상으로 전환됩니다. 실제 UI는 기존 적용·저장 기능을 사용합니다." }),
            new HelpTopic("화면 이동 / 계층", "selection", new[] { "미리보기 클릭", "계층 자동 선택", "자식과 함께 연출" }, new[] { "오브젝트와 Sprite 찾기", "미리보기에서 클릭한 오브젝트는 우측 계층에서도 선택되며, 부모 계층을 펼치고 해당 행으로 자동 스크롤합니다. 검색에 가려진 대상은 검색을 해제해 표시합니다. 하위 오브젝트 포함을 켜면 비활성 자식까지 조회합니다. 하단 카드는 같은 Sprite를 묶어 표시하며 가로로 스크롤할 수 있습니다.", "미리보기에서 켜고 끄기", "계층 행 오른쪽 끝의 체크 또는 선택 대상의 미리보기에서 표시를 사용합니다. 검색창 아래의 펼침 화살표는 모두 펼치기/모두 닫기, 체크박스는 모두 켜기/모두 끄기로 전환되며 설명은 옆에 표시됩니다. 펼침 버튼은 검색을 해제하며, 표시 버튼은 미리보기만 변경합니다. 꺼진 오브젝트도 계층에 남으므로 다시 켤 수 있으며, 켜면 필요한 상위 계층도 함께 표시합니다. 표시 초기화는 원본 상태와 숨김 UI 미리보기 옵션을 기준으로 되돌립니다. 이 상태는 미리보기 전용이며 원본 Canvas·프리팹에는 적용하거나 저장하지 않습니다. 다른 Canvas를 읽으면 초기화됩니다.", "계층 일괄 조작", "계층 행의 표시 체크는 오른쪽 끝에 있습니다. 검색창 아래 화살표는 모두 펼치기/모두 닫기, 체크박스는 모두 켜기/모두 끄기로 바뀌고 설명은 각각 옆에 표시됩니다. 펼침 버튼은 검색을 해제하며, 표시 버튼은 현재 Canvas의 미리보기 계층 전체에 적용합니다. 원본 활성 상태는 저장하지 않습니다.", "미리보기 시점", "미리보기 위에서 마우스 휠로 확대·축소합니다. Space를 누른 채 왼쪽 드래그하거나 휠 버튼을 누른 채 드래그해 화면을 옮깁니다. Space 드래그는 UI 선택을 바꾸지 않고 원본 위치·크기도 변경하지 않습니다. 맞춤 또는 100% 버튼을 누르면 이동을 초기화합니다." }),
            new HelpTopic("기존 함수 연결", "hooks", new[] { "제어 View", "등장 / 퇴장 함수", "반복 / 반응 함수" }, new[] { "UIDirector · 함수 연결", "미리보기 또는 Hierarchy에서 연출 대상을 선택하고 UIDirector를 엽니다. 제어 View와 기존 등장·퇴장 함수를 드롭다운으로 지정합니다. bool 인자가 하나인 같은 함수는 true=등장, false=퇴장으로 연결됩니다. 블록 형태의 동기 void 함수만 자동 연결하며, 오버로드·ref/out·async·조건부 선언은 이유를 표시하고 막습니다.", "상시 반복 / 상호작용 반응 함수", "우측 상단 ‘기존 함수 연결 · DOTween’에서 등장·퇴장 외에 ‘상시 반복 시작 함수’와 ‘상호작용 반응 함수’를 지정합니다. 반복 함수 미지정 시 등장 완료 후 자동 반복하며, 함수 지정 시 그 호출부터 시작합니다. 반응 함수 미지정 시 효과 설정의 반응할 버튼을 사용하고, 함수 지정 시 버튼 자동 연결 대신 지정 함수 호출마다 반응합니다. 기존 본문은 한 번 실행되고 반복·반응 함수는 다른 유형과 구분해야 합니다." }),
            new HelpTopic("효과 조합 / Ease", "timeline", new[] { "유형 선택", "프리셋 추가", "시간 / 값 / Ease", "현재 효과 재생" }, new[] { "효과 조합 / Ease", "등장·퇴장 각각에 투명도, 크기, 이동, 회전을 중복 추가합니다. 이전 효과와 함께 또는 다음 실행을 고르고 지속시간·지연·시작값과 끝값을 정합니다. 투명도는 0~1, 크기는 0~5배 슬라이더로 조절하며 직접 입력도 같은 범위로 제한합니다. 0은 보이지 않음, 크기 1은 원본 크기입니다. 투명도는 대상의 원본 CanvasGroup 값을 읽으므로 원본이 0.8이면 그대로 표시하며, 이미 저장한 연출은 저장값을 유지합니다. 같은 속성의 시간이 겹치면 적용이 차단됩니다. 곡선 선택창은 DOTween 곡선을 카드 호버 및 상세 움직임으로 비교합니다.", "게임 UI 프리셋", "등장 8종·퇴장 8종·상시 반복 5종·상호작용 반응 6종을 제공합니다. 유형을 누르면 하단의 프리셋 목록이 바뀝니다. 연출 추가는 기존 효과 뒤에 추가하며, 슬라이드+페이드 등 조합 연출은 함께 실행하도록 구성합니다. 추가 후 지속시간·이동량·크기·곡선을 자유롭게 바꿀 수 있습니다. 반복 프리셋은 왕복 반복을 기본으로 사용합니다." }),
            new HelpTopic("내 저장 연출", "timeline", new[] { "조합 선택", "이름 / 범위 저장", "저장본 선택", "교체 / 추가 / 미리보기" }, new[] { "조합 저장", "4번 효과 설정 영역 아래의 연출 저장하기 버튼으로 저장 창을 열고 이름을 입력합니다. 현재 유형만 또는 전체 4유형을 선택한 후 새 연출로 저장합니다. 대상·View·함수·버튼 참조 없이 효과와 시간·Ease만 프로젝트 에셋으로 저장합니다. 테스트 UI에서는 저장할 수 없습니다.", "다시 사용하기", "4번 영역 아래의 연출 불러오기 버튼으로 불러오기 창을 엽니다. 저장한 연출 목록에서 고르고 교체하여 불러오기 또는 기존 효과 뒤에 추가를 누릅니다. 단일 유형 저장본은 현재 유형에, 전체 저장본은 각 4유형에 불러옵니다. 기존 함수·버튼 연결은 유지하며 실행 취소할 수 있습니다. 미리보기 후 변경 적용을 눌러 실제 대상에 반영합니다.", "보관 위치", "Assets/Editor/CanvasEditor/Presets에 저장하므로 창이나 Unity를 다시 열어도 사용할 수 있습니다. 에셋 위치로 Project에서 찾고 목록 새로고침으로 갱신합니다. 덮어쓰기는 확인 후 선택한 저장본만 갱신합니다." }),
            new HelpTopic("상시 반복 / 반응", "loop", new[] { "반복 또는 반응 선택", "효과 설정", "함수 또는 버튼 연결", "재생 확인" }, new[] { "상시 반복 / 상호작용 반응 함수", "우측 상단 ‘기존 함수 연결 · DOTween’에서 등장·퇴장 외에 ‘상시 반복 시작 함수’와 ‘상호작용 반응 함수’를 지정합니다. 반복 함수 미지정 시 등장 완료 후 자동 반복하며, 함수 지정 시 그 호출부터 시작합니다. 반응 함수 미지정 시 효과 설정의 반응할 버튼을 사용하고, 함수 지정 시 버튼 자동 연결 대신 지정 함수 호출마다 반응합니다. 기존 본문은 한 번 실행되고 반복·반응 함수는 다른 유형과 구분해야 합니다.", "반복과 반응 시험하기", "상시 반복에서 깜빡임·둥실거림·좌우 흔들림·호흡·기울이기를 고르고 현재 효과 재생을 누릅니다. 반응에서는 클릭 팝·눌림·오류 흔들림·까딱임·점프·반짝임을 시험합니다. 반응 종료 후 원래 상태와 반복을 복구하고 퇴장·비활성 시 정리합니다. 미리보기는 실제 함수나 게임 버튼 이벤트를 실행하지 않습니다. 상단 코드보기는 이 편집기 스크립트를 외부 코드 편집기로 엽니다." }),
            new HelpTopic("미리보기 / 코드", "preview", new[] { "재생", "정지 / 복원", "연출 코드 확인" }, new[] { "미리보기 / 코드 보기", "재생·반복·재생 위치로 복제본에서 확인합니다. 실제 View 함수나 버튼 이벤트는 실행하지 않습니다. 정지·대상 변경·창 닫기 시 복원합니다. 연출 코드는 기존 함수 연결과 선택한 효과의 DOTween 코드를 읽기 전용으로 보여줍니다." }),
            new HelpTopic("적용 / 초기화", "save", new[] { "변경 적용", "컴파일 확인", "씬 / 프리팹 저장" }, new[] { "적용 / 초기화 / 저장", "변경 적용은 기존 함수에 관리 구간 한 곳을 연결하고 같은 연출 설정을 갱신합니다. 버튼 이벤트와 함수 인자는 보존됩니다. 퇴장 연출 완료 후 기존 숨김 처리가 실행됩니다. 초기화 후 변경 적용을 누르면 저장된 효과가 제거됩니다. 다른 씬의 연결을 위해 무해한 호출 구간은 남습니다. 설정은 Unity Undo로 복원하며 소스 원본은 Library/UIDirector/Backups에 보관합니다. 소스 최초 연결은 컴파일 후 씬/프리팹을 저장하세요. 기존 함수가 자체 DOTween을 실행 중인 동일 속성을 제어하는 경우 해당 연출과 겹치지 않게 선택하세요." }),
        });

    }

    /// <summary>모드별 카테고리를 선택하면 해당 설명과 시각자료만 보여줍니다.</summary>
    void BuildModeHelp(VisualElement parent, string mode, HelpTopic[] topics)
    {
        parent.AddToClassList("help-mode");
        var navigation = new ScrollView(ScrollViewMode.Vertical);
        navigation.AddToClassList("help-categories");
        parent.Add(navigation);
        var modeLabel = new Label(mode);
        modeLabel.AddToClassList("help-heading");
        navigation.Add(modeLabel);
        var content = new ScrollView(ScrollViewMode.Vertical);
        content.AddToClassList("help-detail");
        parent.Add(content);
        var pages = new VisualElement[topics.Length];
        var buttons = new Button[topics.Length];
        for (int i = 0; i < topics.Length; i++)
        {
            int index = i;
            pages[i] = new VisualElement { name = "help-topic-" + i };
            content.Add(pages[i]);
            Help(pages[i], mode + " / " + topics[i].Title, "사용 순서와 구성 예시를 확인한 뒤 아래의 상세 설명을 읽어주세요.");
            BuildHelpSteps(pages[i], topics[i].Steps);
            BuildHelpScreenshots(pages[i], mode, topics[i].Visual);
            BuildHelpVisual(pages[i], topics[i].Visual);
            for (int section = 0; section < topics[i].Sections.Length; section += 2)
                Help(pages[i], topics[i].Sections[section], topics[i].Sections[section + 1]);
            buttons[i] = AddButton(navigation, topics[i].Title, () =>
            {
                for (int j = 0; j < pages.Length; j++)
                {
                    Display(pages[j], j == index);
                    buttons[j].EnableInClassList("selected-item", j == index);
                }
                content.scrollOffset = Vector2.zero;
            });
            buttons[i].AddToClassList("help-category");
            buttons[i].name = "help-category-" + i;
            Display(pages[i], i == 0);
            buttons[i].EnableInClassList("selected-item", i == 0);
        }
        Help(navigation, "안내 닫기", "상단 사용 방법 버튼을 다시 누릅니다. 모드 탭은 안내를 유지한 채 설명만 전환합니다.");
    }
#endregion
#region 순서도와 화면 구성 예시
    /// <summary>설명의 작업 순서를 번호가 있는 카드로 표현합니다.</summary>
    static void BuildHelpSteps(VisualElement parent, string[] steps)
    {
        var flow = Row(parent, "help-flow");
        for (int i = 0; i < steps.Length; i++)
            HelpCard(flow, (i + 1) + ". " + steps[i]);
    }

    /// <summary>읽기 전용 도식에 사용할 강조 카드를 추가합니다.</summary>
    static VisualElement HelpCard(VisualElement parent, string text)
    {
        var card = new VisualElement();
        card.AddToClassList("help-diagram-card");
        parent.Add(card);
        var label = new Label(text);
        label.AddToClassList("help-diagram-label");
        card.Add(label);
        return card;
    }

    /// <summary>기능별 화면 배치와 동작을 실제 편집 상태와 구분된 예시로 보여줍니다.</summary>
    static void BuildHelpVisual(VisualElement parent, string kind)
    {
        var diagram = new VisualElement();
        diagram.AddToClassList("help-diagram");
        parent.Add(diagram);
        var caption = new Label("구성 예시 · 설명용 도식");
        caption.AddToClassList("secondary-label");
        diagram.Add(caption);
        var row = Row(diagram, "help-flow");
        switch (kind)
        {
            case "selection":
                HelpCard(row, "미리보기\n[확인 버튼 클릭]");
                HelpCard(row, "우측 계층\nCanvas\n  Popup\n    [확인 버튼 선택]");
                HelpCard(row, "자동 처리\n부모 펼치기\n선택 행으로 스크롤");
                break;
            case "sprite":
                var source = HelpCard(row, "사용 중 Sprite");
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/99.StoreAssets/UI/0.Common/Shared/Gold/Coin_Icon.png");
                if (sprite != null)
                {
                    var thumbnail = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit };
                    thumbnail.style.width = 56;
                    thumbnail.style.height = 56;
                    source.Add(thumbnail);
                }
                HelpCard(row, "사용 위치\nImage > m_Sprite");
                HelpCard(row, "폴더에서 후보 선택\n교체 미리보기\n변경 적용");
                break;
            case "text":
                var sample = HelpCard(row, "텍스트 미리보기");
                var text = new Label("테스트");
                text.style.fontSize = 28;
                text.style.color = (Color)new Color32(253, 247, 228, 255);
                text.style.unityTextAlign = TextAnchor.MiddleCenter;
                sample.Add(text);
                HelpCard(row, "폰트: CookieRun Bold\n크기: 32 (예시)\n정렬: 가운데\n색상: #FDF7E4");
                break;
            case "timeline":
                HelpCard(row, "함께 실행\n투명도  0 → 1   [0~0.3초]\n크기  0.9 → 1   [0~0.3초]");
                HelpCard(row, "이전 효과 다음\n등장  [0~0.3초]\n회전  [0.3~0.6초]");
                break;
            case "hooks":
                HelpCard(row, "기존 View\nShow() / Hide()");
                HelpCard(row, "연결할 트랙\n등장 / 퇴장\n상시 반복 / 상호작용 반응");
                HelpCard(row, "기존 함수 호출\n연결된 DOTween 재생\n기존 버튼 연결 유지");
                break;
            case "loop":
                HelpCard(row, "상시 반복\n위 → 아래 → 위\n표시 중 왕복 재생");
                HelpCard(row, "상호작용 반응\n클릭 → 팝 효과 → 복원\n다시 클릭하면 재생");
                break;
            case "save":
                HelpCard(row, "미리보기\n복제본에서 확인");
                HelpCard(row, "변경 적용\n실제 대상에 반영");
                HelpCard(row, "Unity 저장\n씬 / 프리팹 저장\n테스트 UI는 저장 불가");
                break;
            case "preview":
                HelpCard(row, "재생 / 반복 / 정지\n실제 게임 함수 호출 없음");
                HelpCard(row, "연출 코드\n현재 효과의 코드 확인\n정지 후 원래 상태 복원");
                break;
            default:
                HelpCard(row, "01 미리보기\n휠: 확대 / 축소\nSpace 또는 휠 버튼: 드래그");
                HelpCard(row, "02 우측 계층\n대상 선택 / 표시 조절");
                HelpCard(row, "03~04 하단 영역\n리소스 교체 또는 연출 설정");
                break;
        }
    }
#endregion
}



