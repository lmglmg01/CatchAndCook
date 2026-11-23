using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

/// <summary>음식과 음료에 필요한 설정만 표시하는 메뉴 인스펙터입니다.</summary>
[CustomEditor(typeof(Sushi), true)]
[CanEditMultipleObjects]
public class SushiEditor : Editor
{
    /// <summary>실제 메뉴 클래스에 맞는 직렬화 필드를 구성합니다.</summary>
    public override VisualElement CreateInspectorGUI()
    {
        // 음식과 음료를 동시에 선택한 경우 전용 필드를 숨깁니다.
        bool allFood = true;
        bool allDrink = true;
        foreach (var selected in targets)
        {
            // 선택한 모든 메뉴의 실제 종류를 확인합니다.
            var menu = (Sushi)selected;
            allFood &= menu.Type == MenuType.Food;
            allDrink &= menu.Type == MenuType.Drink;
        }

        // 공통 항목과 해당 메뉴의 전용 항목을 기본 순서대로 표시합니다.
        var root = new VisualElement();
        var property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            // 배열 내부는 PropertyField가 그리므로 최상위 항목만 순회합니다.
            enterChildren = false;
            if ((property.name == "_ingredients" || property.name == "_cookTime") && !allFood)
                continue;
            if (property.name == "_drinkInteractionTime" && !allDrink)
                continue;

            // 직렬화 바인딩을 사용해 Undo와 다중 편집을 지원합니다.
            var field = new PropertyField(property.Copy());
            if (property.name == "m_Script")
                field.SetEnabled(false);
            root.Add(field);
        }

        // Unity가 반환된 트리를 현재 SerializedObject에 자동 바인딩합니다.
        return root;
    }
}
