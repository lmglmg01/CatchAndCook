using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossPatternController : MonoBehaviour
{
    [Header("공통 대상")]
    [SerializeField] Hero _hero;
    [SerializeField] Boss _boss;
    BossRunModel _model => _boss != null ? _boss.RunModel : null;
    [Header("보스 패턴 (씬 오브젝트 또는 프리팹)")]
    [SerializeField] BossPattern[] _bossPatterns;
    [SerializeField] bool _preventRepeat;
    [Tooltip("등록된 순서로 패턴을 반복합니다. 끄면 기존 무작위 선택을 사용합니다.")]
    [SerializeField] bool _playInOrder;
    [Header("패턴 진행")]
    [SerializeField] bool _autoStart;
    [SerializeField, Min(0f)] float _idleDuration;

    public event System.Action<BossPattern> PatternStarted;
    public bool IsRunning { get; private set; }
    public BossPattern CurrentPattern { get; private set; }

    readonly List<BossPattern> _patterns = new List<BossPattern>();
    readonly List<BossPattern> _spawnedPatterns = new List<BossPattern>();
    HeroRunModel _heroModel;
    Coroutine _routine;
    int _lastPatternIndex = -1;
    bool _waitingForPatternEnd;

    void Start()
    {
        if (_autoStart) StartBattle();
    }

    public void Initialize(Hero hero)
    {
        StopBattle();
        _hero = hero;
        foreach (var pattern in _patterns)
            if (pattern != null) pattern.Initialize(hero, _boss);
    }

    public void StartBattle()
    {
        if (IsRunning || !isActiveAndEnabled || _hero == null) return;
        if (_boss == null) _boss = GetComponent<Boss>();
        if (_boss == null) { Debug.LogError("보스 컨트롤러에 Boss를 연결해 주세요.", this); return; }
        _boss.Initialize(_hero);
        _boss.SetChasing(false);
        if (!_boss.CanReceiveDamage) return;
        if (!PreparePatterns()) return;
        _heroModel = _hero.GetComponent<HeroRunModel>();
        if (_heroModel != null)
        {
            if (_heroModel.CurrentHealth <= 0f) return;
            _heroModel.OxygenDepleted += StopBattle;
        }
        if (_model != null) _model.Died += StopBattle;
        IsRunning = true;
        _routine = StartCoroutine(RunBattle());
    }

    bool PreparePatterns()
    {
        if (_patterns.Count > 0) return true;
        if (_bossPatterns == null) return false;
        var added = new HashSet<BossPattern>();
        foreach (var source in _bossPatterns)
        {
            if (source == null || !added.Add(source)) continue;
            bool spawn = !source.gameObject.scene.IsValid();
            var pattern = spawn ? Instantiate(source, source.transform.position, source.transform.rotation, _boss.transform) : source;
            if (pattern.gameObject == gameObject || transform.IsChildOf(pattern.transform))
            {
                Debug.LogWarning("패턴은 보스 컨트롤러와 분리된 오브젝트로 연결해 주세요.", this);
                if (spawn) Destroy(pattern.gameObject);
                continue;
            }
            if (spawn) _spawnedPatterns.Add(pattern);
            pattern.Initialize(_hero, _boss);
            pattern.PatternCompleted += OnPatternCompleted;
            pattern.gameObject.SetActive(false);
            _patterns.Add(pattern);
        }
        return _patterns.Count > 0;
    }

    IEnumerator RunBattle()
    {
        while (IsRunning)
        {
            if (_idleDuration > 0f) yield return new WaitForSeconds(_idleDuration);
            int index = _playInOrder ? (_lastPatternIndex + 1) % _patterns.Count : Random.Range(0, _patterns.Count);
            if (!_playInOrder && _preventRepeat && _patterns.Count > 1 && index == _lastPatternIndex)
                index = (index + Random.Range(1, _patterns.Count)) % _patterns.Count;
            _lastPatternIndex = index;
            var selected = _patterns[index];
            if (selected == null) break;
            _waitingForPatternEnd = true;
            CurrentPattern = selected;
            selected.gameObject.SetActive(true);
            selected.Initialize(_hero, _boss);
            selected.ActivatePattern();
            if (CurrentPattern != null && !selected.IsActive)
            {
                Debug.LogWarning("보스 패턴을 시작하지 못했습니다. 패턴의 Inspector 설정을 확인해 주세요.", selected);
                selected.gameObject.SetActive(false);
                CurrentPattern = null;
                break;
            }
            if (CurrentPattern == selected && selected.IsActive) PatternStarted?.Invoke(selected);
            // 완료 이벤트에서 다음 패턴을 즉시 시작하지 않아 중첩 실행을 막는다.
            yield return null;
            while (IsRunning && _waitingForPatternEnd) yield return null;
        }
        IsRunning = false;
        _waitingForPatternEnd = false;
        _routine = null;
        Unsubscribe();
    }

    void OnPatternCompleted(BossPattern pattern)
    {
        if (!IsRunning || !_waitingForPatternEnd || pattern != CurrentPattern) return;
        _waitingForPatternEnd = false;
        CurrentPattern = null;
        pattern.gameObject.SetActive(false);
    }

    public void StopBattle()
    {
        IsRunning = false;
        _waitingForPatternEnd = false;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (CurrentPattern != null)
        {
            var pattern = CurrentPattern;
            CurrentPattern = null;
            pattern.CancelPattern();
            pattern.gameObject.SetActive(false);
        }
        Unsubscribe();
    }

    internal void RemoveBattleActors()
    {
        foreach (var pattern in _patterns)
            if (pattern != null && !pattern.transform.IsChildOf(transform))
                Destroy(pattern.gameObject);
    }

    void Unsubscribe()
    {
        if (_heroModel != null) _heroModel.OxygenDepleted -= StopBattle;
        _heroModel = null;
        if (_model != null) _model.Died -= StopBattle;
    }

    void OnDisable() => StopBattle();

    void OnDestroy()
    {
        StopBattle();
        foreach (var pattern in _patterns)
            if (pattern != null) pattern.PatternCompleted -= OnPatternCompleted;
        foreach (var pattern in _spawnedPatterns)
            if (pattern != null) Destroy(pattern.gameObject);
    }
}
