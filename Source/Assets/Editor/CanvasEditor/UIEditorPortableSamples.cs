using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>게임 프로젝트에 의존하지 않는 테스트 씬과 Canvas를 생성합니다.</summary>
public static class UIEditorPortableSamples
{
    const string Sample = "Assets/UIEditorPortable/Samples/Canvas_UIDirectorTest.prefab";

    /// <summary>내부 검증용 테스트 씬을 구성합니다. Tools 메뉴에는 노출하지 않습니다.</summary>
    public static void OpenTestScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("편집 모드에서 테스트 씬을 열어 주세요."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera)); camera.tag = "MainCamera";
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = new Color(.04f,.07f,.11f);
        AddSample();
        Debug.Log("CanvasEditor 전용 테스트 씬을 만들었습니다. 플레이하거나 AnimatedPanel을 선택해 Ctrl + Shift + F12를 누르세요.");
    }

    /// <summary>내부 검증에서 기본 테스트 프리팹을 씬에 추가합니다.</summary>
    public static void AddSample()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Sample);
        if (prefab == null) { Debug.LogError("테스트 UI 프리팹이 없습니다. Samples 폴더를 함께 가져오세요."); return; }
        int group = Undo.GetCurrentGroup();
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(root,"Create CanvasEditor Sample");
        if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Length == 0)
        {
            var events = new GameObject("EventSystem",typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(events,"Create UI EventSystem");
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
        }
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root.transform.Find("AnimatedPanel").gameObject;
        EditorGUIUtility.PingObject(Selection.activeGameObject);
    }
}

