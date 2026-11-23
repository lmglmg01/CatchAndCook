using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public sealed class StoneDoorInteractable : InteractObject
{
    [SerializeField] DivePlayScene _diveScene;
    [SerializeField] string _bossScene;
    [SerializeField] Transform _door;
    [SerializeField] QuestItemData[] _requiredTablets;
    [SerializeField] Transform[] _tabletModels;
    [SerializeField] ParticleSystem _silt;
    [SerializeField] TMP_Text _prompt;
    [SerializeField] InputActionReference _interactAction;
    [SerializeField, Min(0.01f)] float _insertDuration = 0.36f;
    [SerializeField, Min(0.01f)] float _lowerDuration = 1.65f;
    [SerializeField, Min(0.01f)] float _lowerDistance = 5.4f;

    const int TabletCount = 4;

    Vector3[] _tabletSockets;
    Vector3 _closedPosition;
    Inventory _inventory;
    bool _opening;

    public bool IsOpen => GameManager.Instance != null
        && GameManager.Instance.PlayerProgress != null && GameManager.Instance.PlayerProgress.BossGateOpened;

    protected override bool IsSingleUse => false;

    protected override void Awake()
    {
        base.Awake();
        if (_door == null || _requiredTablets == null || _requiredTablets.Length != TabletCount
            || _tabletModels == null || _tabletModels.Length != TabletCount)
        {
            Debug.LogError("석문의 문짝과 석판 조각 네 개를 연결해주세요.", this);
            enabled = false;
            return;
        }

        _closedPosition = _door.localPosition;
        _tabletSockets = new Vector3[TabletCount];
        for (int i = 0; i < TabletCount; i++)
        {
            if (_requiredTablets[i] == null || _tabletModels[i] == null || !_tabletModels[i].IsChildOf(_door))
            {
                Debug.LogError("석판 아이템과 문짝 아래 석판 모델을 연결해주세요.", this);
                enabled = false;
                return;
            }
            _tabletSockets[i] = _tabletModels[i].localPosition;
        }
    }

    void OnEnable() => RefreshFromSave();
    void Start() => RefreshFromSave();

    protected override void OnDisable()
    {
        StopAllCoroutines();
        _opening = false;
        if (_silt != null) _silt.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_inventory != null) _inventory.Changed -= RefreshPrompt;
        _inventory = null;
        base.OnDisable();
    }

    void RefreshFromSave()
    {
        if (GameManager.Instance != null && _inventory != GameManager.Instance.Inventory)
        {
            if (_inventory != null) _inventory.Changed -= RefreshPrompt;
            _inventory = GameManager.Instance.Inventory;
            if (_inventory != null) _inventory.Changed += RefreshPrompt;
        }

        SetDoorPose(IsOpen);
        RefreshPrompt();
    }

#if UNITY_EDITOR
    [ContextMenu("테스트/보스 입장용 석판 4종 지급")]
    void GiveMissingTabletsForTest()
    {
        if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || _opening || IsOpen) return;
        RefreshFromSave();
        if (_inventory == null) return;

        foreach (QuestItemData tablet in _requiredTablets)
        {
            if (_inventory.GetCount(tablet) == 0)
                _inventory.TryAdd(tablet, 1);
        }
    }
#endif

    int CountOwnedTablets()
    {
        if (_inventory == null) return 0;
        int count = 0;
        foreach (QuestItemData item in _requiredTablets)
            if (_inventory.GetCount(item) > 0) count++;
        return count;
    }

    void RefreshPrompt()
    {
        if (_prompt == null) return;
        string key = _interactAction != null ? _interactAction.action?.GetBindingDisplayString(0) : null;
        if (string.IsNullOrEmpty(key)) key = "상호작용";
        int owned = CountOwnedTablets();
        _prompt.text = IsOpen ? $"{key}  보스전 입장"
            : owned == TabletCount ? $"{key}  석판 끼우기" : $"석판 필요  {owned}/{TabletCount}";
    }

    public override bool CanInteract(Hero interactor)
    {
        return interactor is UnderSeaHero && !_opening && _diveScene != null && _diveScene.CanUseBossGate
            && GameManager.Instance != null && GameManager.Instance.PlayerProgress != null && base.CanInteract(interactor);
    }

    protected override bool ApplyInteraction(Hero interactor)
    {
        if (IsOpen) return _diveScene.TryEnterBossScene(_bossScene);
        if (CountOwnedTablets() != TabletCount) return false;

        _opening = true;
        SetDetected(false);
        StartCoroutine(PlayOpening(interactor));
        return true;
    }

    IEnumerator PlayOpening(Hero interactor)
    {
        var hand = _door.InverseTransformPoint(interactor.transform.position + Vector3.up * 0.6f);
        for (int i = 0; i < TabletCount && _diveScene.CanUseBossGate; i++)
        {
            Transform tablet = _tabletModels[i];
            Vector3 socket = _tabletSockets[i];
            Vector3 from = new Vector3(Mathf.Clamp(hand.x, -0.48f, 0.48f),
                Mathf.Clamp(hand.y, 0.2f, 1.05f), Mathf.Max(hand.z, socket.z + 0.34f));
            tablet.localPosition = from;
            tablet.gameObject.SetActive(true);
            yield return Move(tablet, from, socket, _insertDuration, 0.09f);
        }

        if (_diveScene.CanUseBossGate)
        {
            _silt?.Play(true);
            _diveScene._cameraController?.ShakeCamera(0.56f, 0.85f);
            yield return Move(_door, _closedPosition, LoweredPosition, _lowerDuration, 0f);
        }

        GameManager game = GameManager.Instance;
        bool opened = _diveScene.CanUseBossGate && game != null && game.PlayerProgress != null
            && game.PlayerProgress.TryOpenBossGate(_inventory, _requiredTablets);
        _opening = false;
        SetDoorPose(opened || IsOpen);
        RefreshPrompt();
        if (opened) GameSaveService.SaveBossGateOpened(game, gameObject.scene);
    }

    IEnumerator Move(Transform target, Vector3 from, Vector3 to, float duration, float arcHeight)
    {
        for (float elapsed = 0f; elapsed < duration && _diveScene.CanUseBossGate; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            target.localPosition = Vector3.LerpUnclamped(from, to, t) + Vector3.up * (arcHeight * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        if (_diveScene.CanUseBossGate) target.localPosition = to;
    }

    Vector3 LoweredPosition => _closedPosition + _door.parent.InverseTransformVector(Vector3.down * _lowerDistance);

    void SetDoorPose(bool opened)
    {
        if (_tabletSockets == null) return;
        _door.localPosition = opened ? LoweredPosition : _closedPosition;
        for (int i = 0; i < TabletCount; i++)
        {
            _tabletModels[i].localPosition = _tabletSockets[i];
            _tabletModels[i].gameObject.SetActive(opened);
        }
        _door.gameObject.SetActive(!opened);
    }
}
