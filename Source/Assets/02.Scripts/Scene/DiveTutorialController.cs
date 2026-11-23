using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DiveTutorialController : MonoBehaviour
{
    public enum Step { Movement, Melee, CollectFish, Harpoon, Resistance, Gun, ReturnBoat, Complete }

    [Header("순서별 대화: 이동, 근접, 포획, 작살, 저항, 총, 복귀")]
    [SerializeField] TutorialDialogueData[] _dialogues;
    [Header("안내 뷰")]
    [SerializeField] TutorialView _tutorialView;
    [Header("이동 목표: 위, 아래, 왼쪽, 오른쪽")]
    [SerializeField] Transform[] _movementTargets;
    [SerializeField, Min(0.1f)] float _arrivalRadius;
    [SerializeField] GameObject _movementMarker;
    [Header("복귀 안내")]
    [SerializeField] GameObject _returnMarker;

    [Header("단계별 연습 물고기")]
    [SerializeField] FishSpawner _fishSpawner;
    [SerializeField] Fish _meleeFishPrefab;
    [SerializeField] Fish _harpoonFishPrefab;
    [SerializeField] Transform _meleeFishSpawn;
    [SerializeField] Transform _harpoonFishSpawn;
    [SerializeField, Min(0f)] float _retryDelay;

    Fish _practiceFish;
    FishModel _practiceModel;
    Vector2 _practiceDirection;

    DivePlayScene _scene;
    GameManager _manager;
    UnderSeaHero _hero;
    TalkView _talk;
    Coroutine _routine;
    bool _objectiveDone;
    bool _resisted;
    bool _ownsDialogue;
    float _oxygenDrain;
    bool _oxygenPaused;

    public bool IsActive { get; private set; }
    public bool IsDialogue { get; private set; }
    public Step CurrentStep { get; private set; }
    public bool AllowsAttack => !IsActive || !IsDialogue && CurrentStep >= Step.Melee;
    public bool AllowsHarpoon => !IsActive || !IsDialogue && CurrentStep >= Step.Harpoon;
    public bool AllowsGun => !IsActive || !IsDialogue && CurrentStep >= Step.Gun;
    public bool AllowsInteract => !IsActive || !IsDialogue && CurrentStep >= Step.CollectFish;
    public bool AllowsReturn => !IsActive || !IsDialogue && CurrentStep == Step.ReturnBoat;

    public void Begin(DivePlayScene scene, GameManager manager, TalkView talk)
    {
        if (IsActive || !isActiveAndEnabled || scene == null || manager == null) return;
        var settings = scene.GetComponent<TutorialSceneSettings>();
        if (settings == null || !settings.isActiveAndEnabled || settings.CurrentChapter != TutorialSceneSettings.Chapter.Dive
            || manager.PlayerProgress == null || manager.PlayerProgress.DiveTutorialStep >= 7) return;
        if (talk == null || !(scene.SceneHero is UnderSeaHero hero) || _tutorialView == null
            || _fishSpawner == null || _meleeFishPrefab == null || _harpoonFishPrefab == null
            || _meleeFishSpawn == null || _harpoonFishSpawn == null
            || _dialogues == null || _dialogues.Length != 7 || _movementTargets == null || _movementTargets.Length != 4)
        {
            Debug.LogWarning("잠수 튜토리얼의 뷰·대화 7개·이동 목표 4개 연결을 확인해주세요.", this);
            return;
        }
        foreach (var dialogue in _dialogues)
            if (dialogue == null || dialogue.Dialogue == null || !dialogue.Dialogue.HasLines) return;
        foreach (var target in _movementTargets) if (target == null) return;
        _scene = scene; _manager = manager; _hero = hero; _talk = talk;
        CurrentStep = (Step)Mathf.Clamp(manager.PlayerProgress.DiveTutorialStep, 0, 7);
        IsActive = true;
        IsDialogue = true;
        PauseOxygen();
        _hero.FishCollected += OnFishCollected;
        _hero.GunFired += OnGun;
        if (_hero.HarpoonShooter != null)
        {
            _hero.HarpoonShooter.ResistanceChanged += OnResistance;
            _hero.HarpoonShooter.FlightChanged += OnFlightChanged;
        }
        _routine = StartCoroutine(RunTutorial());
    }

    IEnumerator RunTutorial()
    {
        // 입수 연출과 씬 Start가 끝나야 이동 안내를 시작한다.
        yield return null;
        while (_hero != null && _hero.IsEnteringWater) yield return null;
        while (IsActive && CurrentStep < Step.Complete)
        {
            _objectiveDone = false;
            _resisted = false;
            yield return PlayDialogue(_dialogues[(int)CurrentStep]);
            if (!IsActive) yield break;
            // 작살 사용과 저항 설명을 같은 한 마리의 연습으로 묶는다.
            if (CurrentStep == Step.Harpoon)
            {
                SetStep(Step.Resistance);
                yield return PlayDialogue(_dialogues[(int)Step.Resistance]);
                if (!IsActive) yield break;
            }
            if (CurrentStep == Step.Melee || CurrentStep == Step.CollectFish || CurrentStep == Step.Resistance)
            {
                if (!EnsurePracticeFish()) { StopTutorial(); yield break; }
            }
            else ClearPracticeFish();
            if (CurrentStep <= Step.CollectFish) _hero.SelectMeleeMode();
            else if (CurrentStep <= Step.Resistance)
            {
                _hero.SelectRangeMode();
                if (_hero.CurrentRangeMode != UnderSeaHero.RangeMode.Gig) _hero.ToggleRangeMode();
            }
            else if (CurrentStep == Step.Gun)
            {
                _hero.SelectRangeMode();
                if (_hero.CurrentRangeMode != UnderSeaHero.RangeMode.Pistol) _hero.ToggleRangeMode();
                _hero.RecoverBullet(1);
            }
            if (CurrentStep == Step.Movement)
            {
                string[] directions = { "위", "아래", "왼쪽", "오른쪽" };
                for (int i = 0; i < _movementTargets.Length; i++)
                {
                    _movementMarker.transform.position = _movementTargets[i].position;
                    _tutorialView.ShowInstruction($"빛나는 목표까지 {directions[i]}로 헤엄쳐 보세요. ({i} / 4)", _movementMarker);
                    while (IsActive && Vector2.Distance(_hero.transform.position, _movementTargets[i].position) > _arrivalRadius)
                        yield return null;
                }
                _objectiveDone = true;
            }
            else
            {
                string message = CurrentStep switch
                {
                    Step.Melee => "앞에 나타난 물고기 한 마리를 근접 공격으로 쓰러뜨리세요.",
                    Step.CollectFish => "물고기를 쓰러뜨린 뒤 가까이 가서 {Interact}키를 눌러 챙기세요.",
                    Step.Harpoon => "작살로 물고기를 잡아 회수하세요.",
                    Step.Resistance => "저항하는 물고기에 작살을 맞힌 뒤 {Interact}키를 연타해서 포획하세요.",
                    Step.Gun => "총으로 빈 공간을 조준한 뒤 한 발 발사해보세요.",
                    _ => "수면으로 올라가 {Interact}키를 길게 눌러 보트로 복귀하세요."
                };
                _tutorialView.ShowInstruction(message, CurrentStep == Step.ReturnBoat ? _returnMarker : null);
            }
            while (IsActive && !_objectiveDone)
            {
                // 작살 대상을 죽인 경우 재연습하되 동시에 한 마리만 유지한다.
                if (CurrentStep == Step.Resistance && (_practiceFish == null || !_practiceFish.gameObject.activeSelf
                    || _practiceModel == null || _practiceModel.CurrentHp <= 0f))
                {
                    while (IsActive && _hero.HarpoonShooter != null && _hero.HarpoonShooter.IsFlying) yield return null;
                    yield return new WaitForSeconds(_retryDelay);
                    if (!IsActive) yield break;
                    ClearPracticeFish();
                    _resisted = false;
                    if (!EnsurePracticeFish()) { StopTutorial(); yield break; }
                }
                yield return null;
            }
            if (!IsActive) yield break;
            SetStep(CurrentStep + 1);
        }
        _routine = null;
        Release();
    }

    IEnumerator PlayDialogue(TutorialDialogueData data)
    {
        IsDialogue = true;
        PauseOxygen();
        _tutorialView.HideInstruction();
        _hero.ResetInput();
        while (_talk.IsTalking) yield return null;
        var dialogue = data.Dialogue;
        var lines = new TalkView.TalkLine[dialogue.Lines.Length];
        for (int i = 0; i < lines.Length; i++)
            lines[i] = new TalkView.TalkLine(dialogue.Lines[i].PlayerSpeaks ? 0 : 1, dialogue.Lines[i].Content);
        _ownsDialogue = true;
        _talk.BeginTalk(_manager.PlayerName, dialogue.NpcName, lines, dialogue.PlayerIllustration, dialogue.NpcIllustration, keepBackgroundVisible: true);
        if (!_talk.IsTalking)
        {
            Debug.LogWarning("잠수 튜토리얼 대화를 열지 못했습니다.", this);
            Release();
            yield break;
        }
        while (_talk.IsTalking) yield return null;
        _ownsDialogue = false;
        IsDialogue = false;
    }

    void FixedUpdate()
    {
        if (!IsActive || _practiceFish == null || _practiceModel == null || _fishSpawner == null
            || !_practiceFish.CanFollowGroup) return;
        if (IsDialogue)
        {
            _practiceFish.Stop();
            return;
        }
        if (_practiceFish.TryConsumeObstacleNormal(out Vector2 normal)
            && Vector2.Dot(_practiceDirection, normal) < -0.01f)
            _practiceDirection = -_practiceDirection;
        _fishSpawner.Follow(_practiceFish, _practiceDirection, _practiceModel.MoveSpeed, Time.fixedDeltaTime);
    }

    bool EnsurePracticeFish()
    {
        if (_practiceFish != null && _practiceFish.gameObject.activeSelf) return true;
        ClearPracticeFish();
        bool harpoon = CurrentStep >= Step.Harpoon;
        Fish prefab = harpoon ? _harpoonFishPrefab : _meleeFishPrefab;
        Vector3 point = (harpoon ? _harpoonFishSpawn : _meleeFishSpawn).position;
        point.z = _hero.transform.position.z;
        var range = new FishVerticalRange(_fishSpawner.MovementMin.y, _fishSpawner.MovementMax.y);
        _practiceDirection = Vector2.left;
        // 연습 대상은 한 마리만 생성한다. 활성화한 Animator에서 사망 파라미터를 검사해야 한다.
        var fish = Instantiate(prefab, point, prefab.transform.rotation);
        fish.gameObject.SetActive(true);
        foreach (var child in fish.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = prefab.gameObject.layer;
        if (!fish.Initialize(_practiceDirection, _hero, _fishSpawner))
        {
            Destroy(fish.gameObject);
            return false;
        }
        fish.SetVerticalRange(range);
        fish.SetBrightness(1f);
        _practiceFish = fish;
        _practiceModel = _practiceFish.GetComponent<FishModel>();
        if (_practiceModel != null) _practiceModel.StateChanged += OnPracticeStateChanged;
        return true;
    }

    void OnPracticeStateChanged(FishState state)
    {
        if (IsActive && CurrentStep == Step.Melee && state == FishState.Dead) _objectiveDone = true;
    }

    void ClearPracticeFish()
    {
        if (_practiceModel != null) _practiceModel.StateChanged -= OnPracticeStateChanged;
        if (_practiceFish != null && _practiceFish.gameObject.activeSelf && _fishSpawner != null)
            _fishSpawner.DespawnFish(_practiceFish);
        _practiceFish = null;
        _practiceModel = null;
    }
    void OnGun() { if (IsActive && !IsDialogue && CurrentStep == Step.Gun) _objectiveDone = true; }
    void OnResistance(bool active) { if (IsActive && !IsDialogue && CurrentStep == Step.Resistance && active) _resisted = true; }
    void OnFlightChanged(bool flying) { if (!flying) _resisted = false; }
    void OnFishCollected(bool byHarpoon)
    {
        if (!IsActive || IsDialogue) return;
        if (CurrentStep == Step.CollectFish && !byHarpoon
            || CurrentStep == Step.Harpoon && byHarpoon
            || CurrentStep == Step.Resistance && byHarpoon && _resisted) _objectiveDone = true;
    }

    public void CompleteReturn()
    {
        if (!IsActive || !AllowsReturn) return;
        SetStep(Step.Complete);
        StopTutorial();
    }

    void SetStep(Step step)
    {
        CurrentStep = step;
        _manager.PlayerProgress.SetDiveTutorialStep((int)step);
    }

    void PauseOxygen()
    {
        if (_scene.RunModel == null || _oxygenPaused) return;
        _oxygenDrain = _scene.RunModel.OxygenDrainPerSecond;
        _scene.RunModel.SetOxygenDrainPerSecond(0f);
        _oxygenPaused = true;
    }
    void RestoreOxygen()
    {
        if (_oxygenPaused && _scene != null && _scene.RunModel != null)
            _scene.RunModel.SetOxygenDrainPerSecond(_oxygenDrain);
        _oxygenPaused = false;
    }

    public void StopTutorial()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        Release();
    }
    void Release()
    {
        if (!IsActive) return;
        IsActive = false;
        ClearPracticeFish();
        if (_hero != null)
        {
            _hero.FishCollected -= OnFishCollected;
            _hero.GunFired -= OnGun;
            if (_hero.HarpoonShooter != null)
            {
                _hero.HarpoonShooter.ResistanceChanged -= OnResistance;
                _hero.HarpoonShooter.FlightChanged -= OnFlightChanged;
            }
        }
        if (_ownsDialogue && _talk != null) _talk.EndTalk();
        _ownsDialogue = false;
        IsDialogue = false;
        RestoreOxygen();
        if (_tutorialView != null) _tutorialView.HideInstruction();
    }
    void OnDisable() => StopTutorial();

    void OnDrawGizmosSelected()
    {
        if (_movementTargets == null) return;
        Gizmos.color = Color.cyan;
        foreach (var target in _movementTargets)
            if (target != null) Gizmos.DrawWireSphere(target.position, _arrivalRadius);
    }
}
