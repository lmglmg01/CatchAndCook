#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택한 이미지 에셋의 임포트 설정을 단축키로 일괄 변경하는 에디터 도구입니다.
/// </summary>
public static class SpriteBatchSettings
{
    // 메뉴 경로 뒤의 %#F9는 Windows에서 Ctrl + Shift + F9 단축키를 의미합니다.
    private const string MenuPath = "Tools/Sprites/Apply Single Point Settings %#F9";

    /// <summary>
    /// 메뉴 또는 단축키 실행 시 선택한 이미지에 Sprite / Single / Point 설정을 적용합니다.
    /// 이미 설정이 일치하는 이미지는 건너뛰고, 변경한 이미지는 저장 후 재임포트합니다.
    /// 개별 이미지 처리에 실패해도 나머지 작업을 계속하며, 완료 시 결과를 콘솔에 출력합니다.
    /// </summary>
    [MenuItem(MenuPath)]
    private static void ApplyToSelection()
    {
        // 재임포트하면 선택한 Sprite 객체가 교체될 수 있으므로 에셋 경로를 먼저 확보합니다.
        var paths = GetSelectedTexturePaths();
        int changed = 0;
        int failed = 0;

        foreach (string path in paths)
        {
            try
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                // 불필요한 재임포트를 피하기 위해 세 설정이 모두 일치하면 건너뜁니다.
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
                changed++;
            }
            catch (Exception exception)
            {
                failed++;
                Debug.LogError($"[SpriteBatchSettings] Failed: {path}\n{exception}");
            }
        }

        Debug.Log($"[SpriteBatchSettings] 적용 완료 — 선택: {paths.Count}개, 변경: {changed}개, 실패: {failed}개");
    }

    /// <summary>
    /// 메뉴와 단축키를 실행할 수 있는 상태인지 검사합니다.
    /// 컴파일이나 에셋 갱신 중이 아니고, 처리 가능한 이미지가 하나 이상 선택되어야 합니다.
    /// </summary>
    /// <returns>실행 가능하면 true, 그렇지 않으면 false를 반환합니다.</returns>
    [MenuItem(MenuPath, true)]
    private static bool ValidateSelection()
    {
        return !EditorApplication.isCompiling &&
               !EditorApplication.isUpdating &&
               GetSelectedTexturePaths().Count > 0;
    }

    /// <summary>
    /// 현재 선택 항목에서 Texture2D 또는 Sprite의 원본 이미지 에셋 경로를 수집합니다.
    /// Assets 폴더 안의 TextureImporter가 있는 에셋만 포함하며, 폴더 내부를 검색하지 않습니다.
    /// 같은 이미지의 하위 스프라이트를 여러 개 선택해도 해당 이미지는 한 번만 포함합니다.
    /// </summary>
    /// <returns>중복을 제거한 이미지 에셋 경로 집합입니다.</returns>
    private static HashSet<string> GetSelectedTexturePaths()
    {
        var paths = new HashSet<string>();
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (!(selected is Texture2D) && !(selected is Sprite))
                continue;

            string path = AssetDatabase.GetAssetPath(selected);
            if (path.StartsWith("Assets/", StringComparison.Ordinal) &&
                AssetImporter.GetAtPath(path) is TextureImporter)
                paths.Add(path);
        }

        return paths;
    }
}
#endif
