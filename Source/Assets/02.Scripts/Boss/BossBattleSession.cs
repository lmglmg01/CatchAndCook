using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Unity.Cinemachine;

// 기존 입력/무기/패턴/뷰를 전투 시작부터 결과까지 연결하는 씬 전용 구성 요소.
public sealed class BossBattleSession : MonoBehaviour
{
    public enum BattleState { Intro, Fighting, Victory, Defeat, Exploring, Revealing }
    enum EntranceMotion { Background, Approach, Retreat }

    [Header("기존 전투 클래스")]
    [SerializeField] UnderSeaHero _hero;
    [SerializeField] Boss _boss;
    [SerializeField] BossPatternController _patterns;
    [SerializeField] InputHandler _input;
    [SerializeField] BossView _bossView;
    [SerializeField] DiveCharacterView _heroView;
    [Header("전투 표시")]
    [SerializeField] UnityEngine.UI.Image _hitFlash;
    [Header("패턴 시작 예고")]
    [SerializeField] CanvasGroup _patternNotice;
    [SerializeField] TMP_Text _patternTitle;
    [SerializeField] TMP_Text _phaseText;
    [SerializeField, Min(0f)] float _patternNoticeDuration;
    Sequence _patternNoticeTween;

    [Header("보스전 시작")]
    [SerializeField, Min(.1f)] float _introDuration = 6f;
    [SerializeField] Animator _entranceBoss;
    [SerializeField] EntranceBossAnimatorHandler _entranceAnimatorHandler;
    [SerializeField] BossEyeEffect _entranceEyes;
    [SerializeField] BoxCollider _encounterArea;
    [SerializeField] Transform _backgroundStart, _backgroundEnd, _revealStart, _revealEnd;
    [SerializeField, Min(.1f)] float _revealDuration = 3.5f;
    [SerializeField] CameraController _cameraController;
    [SerializeField] CinemachineFollow _cameraFollow;
    [SerializeField] Vector3 _revealCameraOffset = new Vector3(5f, 0f, 0f);
    [SerializeField, Range(20f, 80f)] float _revealFieldOfView = 44f;
    [SerializeField] CanvasGroup _cinematicBars;
    [SerializeField] Canvas _gameplayHud;
    [Header("처치 후 다이브 복귀")]
    [SerializeField] ShipReturn _diveReturn;
    bool _returnAvailable;

    [Header("보스 사망 연출")]
    [SerializeField] Transform _deathPose;
    [SerializeField] AnimationClip _deathClip;
    [SerializeField] CinemachineBrain _deathBrain;
    [SerializeField] CinemachineCamera _deathCamera;
    [SerializeField] CanvasGroup _deathFlash;
    [SerializeField] Transform _deathDustPoint;
    GameObject _deathDust;
    PlayableGraph _deathGraph;
    bool _deathCutscenePlaying;
    int _deathCameraOverride = -1;
    readonly System.Collections.Generic.List<Renderer> _hiddenHeroRenderers = new();
    bool _hudEnabled;
    [SerializeField] Shader _entranceShader;
    [Header("등장 이동 이펙트")]
    [SerializeField] GameObject _entranceMoveEffect;
    ParticleSystem[] _entranceDust;
    public bool IsRevealing => State == BattleState.Revealing;
    Vector3 _lastHeroPosition, _originalCameraOffset;
    Renderer[] _entranceRenderers;
    Material[][] _originalMaterials;
    readonly System.Collections.Generic.List<Material> _entranceMaterials = new();
    Mover _mover;
    Rigidbody _heroBody;
    bool _entranceReady, _triggered, _controlsLocked, _inputEnabled, _moverEnabled, _modelEnabled, _shooterEnabled, _wasKinematic, _heroViewActive;
    float _originalFieldOfView;
    [SerializeField] string _bossName = "돌연변이 전기 뱀장어";
    public BattleState State { get; private set; }
    public Boss Boss => _boss;
    public UnderSeaHero Hero => _hero;
    HeroRunModel _heroModel;
    float _flash;

    IEnumerator Start()
    {
        if (_diveReturn != null)
        {
            _diveReturn.gameObject.SetActive(false);
            _diveReturn.ReturnRequested += ReturnToDive;
        }
        // Hero equipment and controls are initialized by PlayScene, exactly as in Dive.
        yield return null;
        if (_hero == null || _boss == null || _patterns == null || _input == null)
        { Debug.LogError("BossBattleSession의 전투 참조를 연결해 주세요.", this); yield break; }
        _heroModel = _hero.GetComponent<HeroRunModel>();
        _patterns.StopBattle();
        _patterns.Initialize(_hero);
        _boss.Initialize(_hero);
        _heroModel.Damaged += OnHeroDamaged;
        _boss.RunModel.Damaged += OnBossDamaged;
        _boss.RunModel.Died += Victory;
        _bossView?.HideBoss();
        _boss.SetChasing(false);
        foreach (var pattern in _boss.GetComponentsInChildren<BossPattern>(true))
        {
            pattern.CancelPattern();
            pattern.gameObject.SetActive(false);
        }
        _lastHeroPosition = _hero.transform.position;
        _entranceReady = true;
        if (_entranceAnimatorHandler == null && _entranceBoss != null)
            _entranceAnimatorHandler = _entranceBoss.GetComponent<EntranceBossAnimatorHandler>();
        if (_entranceBoss != null && _entranceAnimatorHandler != null && _encounterArea != null && _backgroundStart != null
            && _backgroundEnd != null && _revealStart != null && _revealEnd != null)
            yield return PlayEntrance();
        else
        {
            Debug.LogWarning("보스 입장 연출 참조가 없어 기존 조우 표시로 시작합니다.", this);
            _bossView?.ShowBoss(_bossName, _boss.CurrentHealth, _boss.RunModel.MaxHealth);
            while (_bossView != null && _bossView.IsEncounterPlaying) yield return null;
        }
        if (State == BattleState.Victory || State == BattleState.Defeat) yield break;
        State = BattleState.Fighting;
        _patterns.StartBattle();
    }

    void Update()
    {
        if (_entranceReady && (State == BattleState.Intro || State == BattleState.Exploring) && _hero != null && _encounterArea != null)
        {
            // 영역을 한 프레임에 통과해도 놓치지 않는다. 별도의 센서 컴포넌트는 필요 없다.
            var area = new Bounds(_encounterArea.center, _encounterArea.size);
            Vector3 current = _encounterArea.transform.InverseTransformPoint(_hero.transform.position);
            Vector3 previous = _encounterArea.transform.InverseTransformPoint(_lastHeroPosition);
            Vector3 delta = current - previous;
            _triggered |= area.Contains(current) || (delta.sqrMagnitude > .0001f
                && area.IntersectRay(new Ray(previous, delta.normalized), out float distance) && distance <= delta.magnitude);
            _lastHeroPosition = _hero.transform.position;
        }
        if (_hitFlash != null)
        {
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime * 1.5f);
            _hitFlash.color = new Color(.8f, .04f, .02f, _flash);
        }
    }

    void OnEnable()
    {
        HidePatternNotice();
        if (_patterns != null) _patterns.PatternStarted += ShowPatternNotice;
    }

    void ShowPatternNotice(BossPattern pattern)
    {
        HidePatternNotice();
        if (State != BattleState.Fighting || _patternNotice == null || _patternTitle == null
            || _phaseText == null || _patternNoticeDuration <= 0f) return;

        switch (pattern)
        {
            case SuctionBossPattern _:
                _patternTitle.text = "흡입패턴";
                _phaseText.text = "보스에게서 벗어나세요!";
                break;
            case RushBiteBossPattern _:
                _patternTitle.text = "돌진 패턴";
                _phaseText.text = "돌진하는 보스를 피하세요!";
                break;
            case SweepBossPattern _:
                _patternTitle.text = "휩쓸기 패턴";
                _phaseText.text = "보스를 피해 안전한곳으로 이동하세요!";
                break;
            default:
                return;
        }

        _patternNotice.gameObject.SetActive(true);
        float fade = Mathf.Min(.2f, _patternNoticeDuration * .25f);
        _patternNoticeTween = DOTween.Sequence()
            .Append(_patternNotice.DOFade(1f, fade))
            .AppendInterval(_patternNoticeDuration - 2f * fade)
            .Append(_patternNotice.DOFade(0f, fade))
            .OnComplete(() =>
            {
                _patternNoticeTween = null;
                _patternNotice.gameObject.SetActive(false);
            });
    }

    void HidePatternNotice()
    {
        _patternNoticeTween?.Kill();
        _patternNoticeTween = null;
        if (_patternNotice == null) return;
        _patternNotice.alpha = 0f;
        _patternNotice.gameObject.SetActive(false);
    }

    IEnumerator PlayEntrance()
    {
        PrepareEntranceMaterials();
        if (_entranceMoveEffect != null)
            _entranceDust = _entranceMoveEffect.GetComponentsInChildren<ParticleSystem>(true);
        SetEntranceAppearance(false);
        State = BattleState.Intro;
        _entranceBoss.transform.SetPositionAndRotation(_backgroundStart.position, _backgroundStart.rotation);
        _entranceBoss.gameObject.SetActive(true);
        _entranceAnimatorHandler.PlaySwim(1f);
        yield return MoveEntrance(_backgroundStart, _backgroundEnd, _introDuration, EntranceMotion.Background);
        _entranceBoss.gameObject.SetActive(false);
        State = BattleState.Exploring;
        while (!_triggered) yield return null;

        State = BattleState.Revealing;
        LockControls();
        _entranceBoss.transform.SetPositionAndRotation(_revealStart.position, _revealStart.rotation);
        SetEntranceAppearance(true);
        _entranceBoss.gameObject.SetActive(true);
        _entranceAnimatorHandler.PlaySwim(1f);
        SetEntranceMoveEffect(true);
        yield return MoveEntrance(_revealStart, _revealEnd, _revealDuration, EntranceMotion.Approach);
        SetEntranceMoveEffect(false);
        _entranceAnimatorHandler.PlayRoar();
        _cameraController?.ShakeCamera(.55f, .35f);
        _bossView?.ShowBoss(_bossName, _boss.CurrentHealth, _boss.RunModel.MaxHealth);
        float elapsed = 0f;
        do
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        } while ((_bossView != null && _bossView.IsEncounterPlaying)
            || elapsed < _entranceAnimatorHandler.RoarDuration || _entranceAnimatorHandler.IsRoaring);
        _entranceAnimatorHandler.PlaySwim(1f);
        SetEntranceMoveEffect(true);
        yield return MoveEntrance(_revealEnd, _revealStart, _revealDuration, EntranceMotion.Retreat);
        RestoreEntrance();
    }

    IEnumerator MoveEntrance(Transform from, Transform to, float duration, EntranceMotion motion)
    {
        bool background = motion == EntranceMotion.Background;
        bool retreating = motion == EntranceMotion.Retreat;
        Quaternion startRotation = from.rotation;
        Quaternion endRotation = retreating ? startRotation : to.rotation;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            _entranceBoss.transform.SetPositionAndRotation(
                Vector3.Lerp(from.position, to.position, background ? smooth : t),
                Quaternion.Slerp(startRotation, endRotation, smooth));
            if (!background)
            {
                float framing = retreating ? 1f - smooth : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2f));
                SetCinematicFrame(framing);
            }
            yield return null;
        }
        _entranceBoss.transform.SetPositionAndRotation(to.position, endRotation);
    }

    void PrepareEntranceMaterials()
    {
        if (_entranceShader == null) return;
        _entranceRenderers = _entranceBoss.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        _originalMaterials = new Material[_entranceRenderers.Length][];
        for (int i = 0; i < _entranceRenderers.Length; i++)
        {
            var renderer = _entranceRenderers[i];
            _originalMaterials[i] = renderer.sharedMaterials;
            var materials = new Material[_originalMaterials[i].Length];
            for (int j = 0; j < materials.Length; j++)
            {
                var source = _originalMaterials[i][j];
                var material = new Material(_entranceShader);
                if (source != null && source.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                materials[j] = material;
                _entranceMaterials.Add(material);
            }
            renderer.sharedMaterials = materials;
        }
    }

    void SetEntranceAppearance(bool revealed)
    {
        foreach (var material in _entranceMaterials)
            material.SetFloat("_Reveal", revealed ? 1f : 0f);
        if (_entranceEyes != null) _entranceEyes.SetVisible(revealed);
    }

    void LockControls()
    {
        _controlsLocked = true;
        if (_gameplayHud != null)
        {
            _hudEnabled = _gameplayHud.enabled;
            _gameplayHud.enabled = false;
        }
        _inputEnabled = _input.enabled;
        _modelEnabled = _heroModel.enabled;
        _shooterEnabled = _hero.HarpoonShooter != null && _hero.HarpoonShooter.enabled;
        _mover = _hero.GetComponent<Mover>();
        _moverEnabled = _mover != null && _mover.enabled;
        _heroBody = _hero.GetComponent<Rigidbody>();
        _input.enabled = false;
        _hero.ResetInput();
        if (_hero.HarpoonShooter != null) _hero.HarpoonShooter.enabled = false;
        _heroModel.enabled = false; // 조작할 수 없는 연출 중에는 산소를 소모하지 않는다.
        if (_mover != null) _mover.enabled = false;
        if (_heroBody != null)
        {
            _wasKinematic = _heroBody.isKinematic;
            if (!_wasKinematic) _heroBody.linearVelocity = Vector3.zero;
            _heroBody.isKinematic = true;
        }
        if (_heroView != null)
        {
            _heroViewActive = _heroView.gameObject.activeSelf;
            _heroView.gameObject.SetActive(false);
        }
        if (_cameraFollow != null)
        {
            _originalCameraOffset = _cameraFollow.FollowOffset;
            _originalFieldOfView = _cameraFollow.GetComponent<CinemachineCamera>().Lens.FieldOfView;
        }
    }

    void SetCinematicFrame(float weight)
    {
        if (_cinematicBars != null) _cinematicBars.alpha = weight;
        if (_cameraFollow != null && _hero != null && _revealEnd != null)
        {
            _cameraFollow.FollowOffset = _originalCameraOffset + new Vector3(_revealCameraOffset.x, _revealEnd.position.y - _hero.transform.position.y + _revealCameraOffset.y, _revealCameraOffset.z) * weight;
            float fov = Mathf.Lerp(_originalFieldOfView, _revealFieldOfView, weight);
            if (_cameraController != null) _cameraController.SetBattleFraming(fov, fov);
        }
    }


    void SetEntranceMoveEffect(bool playing)
    {
        if (_entranceDust == null) return;
        foreach (var particles in _entranceDust)
        {
            if (particles == null) continue;
            if (playing) particles.Play(false);
            else particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void RestoreEntrance()
    {
        if (_entranceBoss != null) _entranceBoss.gameObject.SetActive(false);
        if (_controlsLocked) SetEntranceMoveEffect(false);
        if (_cinematicBars != null) _cinematicBars.alpha = 0f;
        if (!_controlsLocked) return;
        SetCinematicFrame(0f);
        if (_cameraController != null) _cameraController.ClearBattleFraming();
        _controlsLocked = false;
        if (_gameplayHud != null) _gameplayHud.enabled = _hudEnabled;
        if (_heroBody != null) _heroBody.isKinematic = _wasKinematic;
        if (_heroView != null) _heroView.gameObject.SetActive(_heroViewActive);
        if (_mover != null) _mover.enabled = _moverEnabled;
        if (_heroModel != null) _heroModel.enabled = _modelEnabled;
        if (_hero != null && _hero.HarpoonShooter != null) _hero.HarpoonShooter.enabled = _shooterEnabled;
        if (_input != null) _input.enabled = _inputEnabled;
    }

    void OnBossDamaged(float damage) => _bossView?.UpdateHp(_boss.CurrentHealth);
    void OnHeroDamaged(float damage) => _flash = .23f;
    void Victory()
    {
        if (!EndBattle(BattleState.Victory)) return;
        ItemData reward = _boss.RunModel.DropItemData;
        FishBag bag = _hero.FishBag;
        if (reward != null && (bag == null || !bag.TryAdd(reward, 1)))
            Debug.LogError("보스 처치 보상을 잠수 가방에 지급하지 못했습니다.", this);
        _bossView?.HideBoss();
        StartCoroutine(PlayDeathCutscene());
    }

    public void Defeat()
    {
        if (!EndBattle(BattleState.Defeat)) return;
        _hero.ResetInput();
        _input.enabled = false;
        if (_hero.HarpoonShooter != null) _hero.HarpoonShooter.enabled = false;
        _hero.enabled = false;
        if (_heroModel != null) _heroModel.enabled = false;
        var mover = _hero.GetComponent<Mover>();
        if (mover != null) mover.enabled = false;
        var body = _hero.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic) body.linearVelocity = Vector3.zero;
        _bossView?.HideBoss();
    }

    bool EndBattle(BattleState state)
    {
        if (State == BattleState.Victory || State == BattleState.Defeat) return false;
        StopAllCoroutines();
        RestoreEntrance();
        State = state;
        _patterns.StopBattle();
        HidePatternNotice();
        return true;
    }

    IEnumerator PlayDeathCutscene()
    {
        if (_deathPose == null || _deathClip == null || _entranceBoss == null)
        {
            RemoveDefeatedBoss();
            EnableDiveReturn();
            yield break;
        }

        _deathCutscenePlaying = true;
        LockControls();
        foreach (var renderer in _hero.GetComponentsInChildren<Renderer>())
        {
            if (renderer.forceRenderingOff) continue;
            renderer.forceRenderingOff = true;
            _hiddenHeroRenderers.Add(renderer);
        }

        if (_deathFlash != null) _deathFlash.alpha = 1f;
        if (_deathBrain != null && _deathCamera != null)
            _deathCameraOverride = _deathBrain.SetCameraOverride(-1, 100, null, _deathCamera, 1f, 0f);

        _entranceBoss.transform.SetPositionAndRotation(_deathPose.position, _deathPose.rotation);
        SetEntranceAppearance(true);
        _entranceBoss.gameObject.SetActive(true);
        var clip = AnimationPlayableUtilities.PlayClip(_entranceBoss, _deathClip, out _deathGraph);
        _deathGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
        clip.SetApplyFootIK(false);
        clip.SetSpeed(0);
        yield return new WaitForSecondsRealtime(.12f);

        if (_deathDustPoint != null && _entranceMoveEffect != null)
        {
            _deathDust = Instantiate(_entranceMoveEffect, _deathDustPoint.position, Quaternion.identity, _boss.transform);
            _deathDust.name = "BossDeathDust";
            foreach (var particle in _deathDust.GetComponentsInChildren<ParticleSystem>())
            {
                if (particle.GetComponent<ParticleSystemRenderer>().renderMode == ParticleSystemRenderMode.Mesh) continue;

                particle.transform.rotation = Quaternion.identity;
                var main = particle.main;
                main.startSizeMultiplier *= .72f;
                main.startLifetimeMultiplier *= .8f;
                main.startColor = new Color(.65f, .75f, .74f, .38f);

                var shape = particle.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(24f, .3f, 4f);

                var drift = particle.velocityOverLifetime;
                drift.space = ParticleSystemSimulationSpace.World;
                drift.x = new ParticleSystem.MinMaxCurve(-1.7f, 1.7f);
                drift.y = new ParticleSystem.MinMaxCurve(.15f, .65f);
                drift.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
                var emission = particle.emission;
                emission.rateOverTimeMultiplier *= .45f;
                particle.Play(false);
            }
        }
        clip.SetSpeed(1);
        yield return FadeDeathFlash(1f, 0f, .55f);
        yield return new WaitUntil(() => clip.GetTime() >= _deathClip.length);
        clip.SetSpeed(0);
        _entranceEyes?.SetDead();
        if (_deathDust != null)
            foreach (var particle in _deathDust.GetComponentsInChildren<ParticleSystem>())
                particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        yield return new WaitForSecondsRealtime(.3f);
        yield return FadeDeathFlash(0f, 1f, .26f);

        FinishDeathCutscene();
        yield return FadeDeathFlash(1f, 0f, .6f);
        EnableDiveReturn();
    }

    void EnableDiveReturn()
    {
        _returnAvailable = true;
        if (_diveReturn != null) _diveReturn.gameObject.SetActive(true);
    }

    void ReturnToDive()
    {
        if (!_returnAvailable || State != BattleState.Victory || _deathCutscenePlaying) return;
        DivePlayScene scene = GetComponent<DivePlayScene>();
        if (scene != null && scene.TryReturnFromBoss()) _returnAvailable = false;
    }

    IEnumerator FadeDeathFlash(float from, float to, float duration)
    {
        if (_deathFlash == null) yield break;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            _deathFlash.alpha = Mathf.SmoothStep(from, to, elapsed / duration);
            yield return null;
        }
        _deathFlash.alpha = to;
    }

    void FinishDeathCutscene()
    {
        if (!_deathCutscenePlaying) return;
        _deathCutscenePlaying = false;
        if (_deathCameraOverride >= 0 && _deathBrain != null)
            _deathBrain.ReleaseCameraOverride(_deathCameraOverride);
        _deathCameraOverride = -1;
        if (_deathGraph.IsValid()) _deathGraph.Destroy();
        foreach (var renderer in _hiddenHeroRenderers)
            if (renderer != null) renderer.forceRenderingOff = false;
        _hiddenHeroRenderers.Clear();
        RestoreEntrance();
        RemoveDefeatedBoss();
    }

    void RemoveDefeatedBoss()
    {
        if (_patterns != null) _patterns.RemoveBattleActors();
        if (_boss != null) Destroy(_boss.gameObject);
    }
    void OnDestroy()
    {
        if (_entranceRenderers != null)
            for (int i = 0; i < _entranceRenderers.Length; i++)
                if (_entranceRenderers[i] != null)
                    _entranceRenderers[i].sharedMaterials = _originalMaterials[i];
        foreach (var material in _entranceMaterials) if (material != null) Destroy(material);
    }

    void OnDisable()
    {
        if (_patterns != null) _patterns.PatternStarted -= ShowPatternNotice;
        HidePatternNotice();
        _returnAvailable = false;
        if (_diveReturn != null) _diveReturn.ReturnRequested -= ReturnToDive;
        StopAllCoroutines();
        FinishDeathCutscene();
        if (_deathFlash != null) _deathFlash.alpha = 0f;
        RestoreEntrance();
        if (_patterns != null) _patterns.StopBattle();
        if (_heroModel != null) _heroModel.Damaged -= OnHeroDamaged;
        if (_boss != null && _boss.RunModel != null) { _boss.RunModel.Damaged -= OnBossDamaged; _boss.RunModel.Died -= Victory; }
    }
}
