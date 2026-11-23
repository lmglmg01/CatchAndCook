using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class StartSceneLoader : MonoBehaviour
{
    [Serializable]
    public struct SceneLoadView
    {
        public string SceneName;
        public DiveLoadView LoadViewPrefab;
    }

    [Header("목적지별 로딩 화면")]
    [SerializeField] SceneLoadView[] _loadViews;
    [SerializeField, Min(0f)] float _minimumLoadDuration;

    DiveLoadView _activeLoadView;
    AsyncOperation _loadOperation;
    Coroutine _loadRoutine;
    bool _loading;

    void OnEnable()
    {
        GameSaveService.SceneLoadRequested += LoadScene;
    }

    void OnDisable()
    {
        GameSaveService.SceneLoadRequested -= LoadScene;
        ReleaseLoading();
    }

    void LoadScene(string sceneName)
    {
        if (_loading) return;
        _loading = true;
        DiveLoadView prefab = FindLoadView(sceneName);
        if (prefab != null)
        {
            _activeLoadView = Instantiate(prefab);
            var canvas = _activeLoadView.GetComponent<Canvas>();
            if (canvas != null)
            {
                int highestOrder = canvas.sortingOrder;
                foreach (Canvas other in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (other != canvas) highestOrder = Mathf.Max(highestOrder, other.sortingOrder);
                canvas.sortingOrder = Mathf.Min(short.MaxValue, highestOrder + 1);
            }
            _activeLoadView.Show();
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        _loadRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    DiveLoadView FindLoadView(string sceneName)
    {
        if (_loadViews == null) return null;
        string target = TutorialSceneSettings.GameplaySceneName(Path.GetFileNameWithoutExtension(sceneName));
        foreach (SceneLoadView entry in _loadViews)
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(entry.SceneName), target,
                    StringComparison.OrdinalIgnoreCase))
                return entry.LoadViewPrefab;
        }
        return null;
    }

    IEnumerator LoadSceneRoutine(string sceneName)
    {
        float shownAt = Time.realtimeSinceStartup;
        yield return null;
        while (_activeLoadView != null && _activeLoadView.IsFadingIn) yield return null;

        try
        {
            _loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            Debug.LogError($"시작 화면의 씬 로딩 실패 ({sceneName}): {exception.Message}", this);
        }
        if (_loadOperation == null)
        {
            _loadRoutine = null;
            ReleaseLoading();
            yield break;
        }

        _loadOperation.allowSceneActivation = false;
        float minimumDuration = _activeLoadView != null ? Mathf.Max(0f, _minimumLoadDuration) : 0f;
        while (_loadOperation.progress < 0.9f || Time.realtimeSinceStartup - shownAt < minimumDuration)
            yield return null;

        if (_activeLoadView != null)
        {
            DiveLoadView view = _activeLoadView;
            view.KeepForSceneTransition();
            // StartScene이 제거된 후에도 로드뷰가 새 씬 위에서 페이드 아웃합니다.
            _loadOperation.completed += operation =>
            {
                if (view != null) view.FadeOutAfterSceneLoad();
            };
        }
        _loadOperation.allowSceneActivation = true;
        while (!_loadOperation.isDone) yield return null;
        _loadRoutine = null;
        ReleaseLoading();
    }

    void ReleaseLoading()
    {
        if (_loadRoutine != null) StopCoroutine(_loadRoutine);
        _loadRoutine = null;
        // 비동기 씬 로드는 취소할 수 없으므로 활성화 대기로 큐를 막지 않습니다.
        if (_loadOperation != null && !_loadOperation.isDone) _loadOperation.allowSceneActivation = true;
        _loadOperation = null;
        if (_activeLoadView != null && !_activeLoadView.IsKeptForSceneTransition)
        {
            _activeLoadView.Hide();
            Destroy(_activeLoadView.gameObject);
        }
        _activeLoadView = null;
        _loading = false;
    }
}
