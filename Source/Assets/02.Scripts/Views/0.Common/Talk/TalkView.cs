using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 두 캐릭터의 대화 진행과 화자 강조 연출을 담당하는 공통 대화 뷰입니다.
/// </summary>
public class TalkView : MonoBehaviour
{

    #region 멤버

    #region UI 연결 및 표시 설정

    [SerializeField] GameObject _talkPanel; // 기존 Interact_TalkPanel 루트를 연결합니다.
    [SerializeField] Image[] _characterImages = new Image[2]; // 기존 일러스트 배열 연결과 순서를 그대로 유지합니다. 주인공 인덱스로 좌우 역할을 지정합니다.
    [SerializeField, Range(0, 1)] int _playerImageIndex = 1; // 기존 CharacterImages 배열에서 PlayerImage가 연결된 인덱스입니다. 나머지 한 칸은 NPC입니다.
    [SerializeField] GameObject _talkWindow; // 이름·대사·Skip 버튼을 포함한 TalkWindow를 연결합니다.
    [SerializeField] TMP_Text _talkerName; // TalkWindow/TalkerNameText를 연결합니다.
    [SerializeField] TMP_Text _talkerContent; // TalkWindow/TalkerContentText를 연결합니다.
    [SerializeField] Button _skipButton; // TalkWindow/TalkSkipButton을 연결합니다.
    [SerializeField] BaseView _baseView; // 같은 Canvas의 BasePanel에 있는 BaseView를 연결합니다.
    [SerializeField] CanvasGroup _panelGroup; // 대화 루트의 CanvasGroup을 연결합니다. 등장 연출과 입력 차단에 사용합니다.
    [SerializeField] TMP_Text _skipText; // 기존 TalkSkipText를 연결합니다. 표시 중에는 Skip, 완료 후에는 다음/종료로 표시합니다.

    [Header("대화 연출 · DOTween")]
    [SerializeField, Min(0f)] float _transitionDuration = 0.22f; // 화자 전환과 패널 등장 연출 시간입니다. UI 연결은 필요 없습니다.
    [SerializeField, Min(0f)] float _charactersPerSecond = 32f; // 초당 표시할 글자 수입니다. 0이면 즉시 표시합니다.
    [SerializeField, Range(0f, 1f)] float _listenerBrightness = 0.35f; // 듣는 일러스트의 밝기 비율입니다.
    [SerializeField, Min(1f)] float _speakerScale = 1.06f; // 말하는 일러스트의 원래 크기 대비 확대 배율입니다.

    #endregion

    #region 런타임 상태

    readonly List<Tween> _portraitTweens = new List<Tween>(); // 이 뷰에서 생성한 일러스트 트윈만 정리합니다.
    readonly Vector3[] _portraitScales = new Vector3[2]; // 종료 후 복원할 원래 일러스트 크기입니다.
    readonly Color[] _portraitColors = new Color[2]; // 종료 후 복원할 원래 일러스트 색입니다.
    Coroutine _portraitSetup;
    readonly TalkPortraitPreview[] _portraitPreviews = new TalkPortraitPreview[2];
    readonly Sprite[] _portraitSprites = new Sprite[2]; // 종료 후 복원할 원래 일러스트입니다.
    readonly int[] _portraitOrders = new int[2]; // 종료 후 복원할 일러스트 형제 순서입니다.
    TalkLine[] _lines = Array.Empty<TalkLine>(); // 현재 실행 중인 대사 목록의 복사본입니다.
    string _playerName, _npcName; // 현재 대화에 참가한 주인공과 NPC 이름입니다.
    int _lineIndex, _panelOrder, _windowOrder; // 현재 문장과 복원할 UI 형제 순서입니다.
    bool _initialized, _isRevealing; // 초기화와 글자 순차 표시 진행 여부입니다.
    Tween _textTween, _panelTween; // 글자와 패널의 DOTween 연출입니다.

    #endregion

    #region 임시 데이터 및 테스트 멤버

    [Header("임시 데이터 · 실제 퀘스트/대화 데이터와 무관")]
    [SerializeField] string _testPlayerName = "주인공"; // 테스트에서 왼쪽 캐릭터에 사용할 이름입니다.
    [SerializeField] string _testNpcName = "선장 민수"; // 테스트에서 오른쪽 캐릭터에 사용할 이름입니다.
    [SerializeField] TalkLine[] _testLines =
    {
        new TalkLine(1, "좋은 아침이야. 잠수하기 전에 장비는 모두 확인했니?"),
        new TalkLine(0, "네! 산소통과 잠수복을 확인했어요. 오늘은 어디를 조사하면 될까요?"),
        new TalkLine(1, "배 아래 산호초부터 살펴보자. 산소가 부족하면 무리하지 말고 돌아오렴."),
        new TalkLine(0, "알겠어요. 안전하게 다녀올게요!")
    }; // ContextMenu로 실행할 임시 대사입니다. 이 배열만 바꿔도 테스트할 수 있습니다.

    #endregion

    #region 프로퍼티

    internal bool KeepsBackgroundVisible { get; private set; }
    public bool IsTalking { get; private set; } // 외부에서 확인할 대화 실행 여부입니다.
    public int CurrentLineIndex => _lineIndex; // 현재 표시 중인 대사 순번입니다.

    #endregion

    #region 표시 데이터 형식

    /// <summary>
    /// 한 문장의 화자와 내용을 담습니다. 화자는 0: 주인공, 1: NPC입니다.
    /// </summary>
    [Serializable]
    public struct TalkLine
    {

        #region 멤버

        #region 런타임 상태

        [Range(0, 1)]
        public int SpeakerIndex; // CharacterImages 배열에 대응하는 화자입니다.
        [TextArea(2, 5)]
        public string Content; // 해당 화자가 말할 대화 내용입니다.

        /// <summary>
        /// 외부 대화 데이터나 임시 테스트에서 한 문장을 구성합니다.
        /// </summary>
        /// <param name="speakerIndex">화자 인덱스 값입니다.</param>
        /// <param name="content">내용 값입니다.</param>
        public TalkLine(int speakerIndex, string content)
        {
            SpeakerIndex = speakerIndex;
            Content = content;
        }

        #endregion

        #endregion

    }

    #endregion

    #endregion

    #region 이벤트 함수 및 초기화

    /// <summary>
    /// 기존 UI를 기억하고 Skip 클릭을 한 번만 연결합니다.
    /// </summary>

    void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// 테스트나 외부 대화 시작 전에는 대화 패널을 숨깁니다.
    /// </summary>
    void Start()
    {
        if (!IsTalking && _talkPanel != null)
        {
            _talkPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 대화창 바깥의 입력을 차단하고 캐릭터 프리뷰의 자세를 갱신합니다.
    /// </summary>
    void LateUpdate()
    {
        if (IsTalking && _baseView != null)
        {
            _baseView.RefreshTalkMode(this);
            foreach (TalkPortraitPreview preview in _portraitPreviews)
            {
                preview?.UpdatePose();
                preview?.AlignBottom(_talkWindow.transform as RectTransform);
            }
        }
        else if (_initialized && _talkPanel != null && _talkPanel.activeSelf)
        {
            _talkPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 외부에서 패널을 꺼도 연출과 UI 숨김이 남지 않도록 종료합니다.
    /// </summary>
    void OnDisable()
    {
        if (IsTalking)
        {
            EndTalk();
        }
    }

    /// <summary>
    /// 대화 뷰가 제거될 때 자신의 트윈과 클릭 이벤트를 정리합니다.
    /// </summary>
    void OnDestroy()
    {
        if (IsTalking)
        {
            EndTalk();
        }

        KillPresentation();
        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(NextTalk);
        }
    }

    #endregion

    #region 초기화 및 연결

    /// <summary>
    /// 직렬화된 연결을 우선 사용하고 복원에 필요한 UI 상태를 한 번 기억합니다.
    /// </summary>
    void Initialize()
    {
        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        if (_talkPanel == null)
        {
            _talkPanel = gameObject;
        }

        if (_baseView == null)
        {
            var canvas = GetComponentInParent<Canvas>(); // 캔버스 값입니다.
            if (canvas != null)
            {
                _baseView = canvas.GetComponentInChildren<BaseView>(true);
            }
        }

        if (_panelGroup == null)
        {
            _panelGroup = _talkPanel.GetComponent<CanvasGroup>();
        }

        if (_panelGroup == null)
        {
            _panelGroup = _talkPanel.AddComponent<CanvasGroup>();
        }

        if (_skipButton != null)
        {
            _skipButton.onClick.AddListener(NextTalk);
        }

        _panelOrder = _talkPanel.transform.GetSiblingIndex();
        _windowOrder = _talkWindow != null ? _talkWindow.transform.GetSiblingIndex() : 0;
        // i: 반복 값입니다.
        for (int i = 0; i < 2; i++)
        {
            var image = GetPortrait(i); // 이미지 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (image == null)
            {
                continue;
            }

            _portraitScales[i] = image.transform.localScale;
            _portraitColors[i] = image.color;
            _portraitSprites[i] = image.sprite;
            _portraitOrders[i] = image.transform.GetSiblingIndex();
        }
    }

    #endregion

    #region UI 표시 및 갱신

    /// <summary>
    /// 두 참가자의 이름과 대사 복사본으로 대화를 시작합니다. 일러스트는 생략 시 기존 연결을 사용합니다.
    /// </summary>
    /// <param name="playerName">플레이어 이름 값입니다.</param>
    /// <param name="npcName">NPC 이름 값입니다.</param>
    /// <param name="lines">줄 목록입니다.</param>
    /// <param name="playerIllustration">플레이어 전신 이미지입니다.</param>
    /// <param name="npcIllustration">NPC 전신 이미지입니다.</param>
    public void BeginTalk(string playerName, string npcName, TalkLine[] lines, Sprite playerIllustration = null, Sprite npcIllustration = null)
    {
        BeginTalk(playerName, npcName, lines, playerIllustration, npcIllustration, true);
    }

    internal void BeginTalk(string playerName, string npcName, TalkLine[] lines, Sprite playerIllustration, Sprite npcIllustration, bool keepBackgroundVisible)
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (lines == null || lines.Length == 0)
        {
            EndTalk();
            return;
        }

        // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
        if (_baseView == null || _talkerName == null || _talkerContent == null || _talkWindow == null)
        {
            Debug.LogWarning("TalkView의 BaseView·대화창·이름·대사 연결을 확인해주세요.", this);
            return;
        }

        if (IsTalking)
        {
            EndTalk();
        }

        _playerName = string.IsNullOrWhiteSpace(playerName) ? "주인공" : playerName;
        _npcName = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
        _lines = (TalkLine[])lines.Clone();
        _lineIndex = 0;
        if (playerIllustration != null && GetPortrait(0) != null)
        {
            GetPortrait(0).sprite = playerIllustration;
        }

        if (npcIllustration != null && GetPortrait(1) != null)
        {
            GetPortrait(1).sprite = npcIllustration;
        }

        KeepsBackgroundVisible = keepBackgroundVisible;
        // 대화창 바깥의 UI는 계속 조작하고, 대화창 영역만 클릭을 받는다.
        if (_talkPanel.TryGetComponent<Graphic>(out var backdrop))
            backdrop.raycastTarget = !keepBackgroundVisible;
        if (_talkWindow.TryGetComponent<Graphic>(out var window))
            window.raycastTarget = true;
        IsTalking = true;
        _talkPanel.SetActive(true);
        _talkWindow.SetActive(true);
        _talkPanel.transform.SetAsLastSibling();
        _baseView.BeginTalkMode(this);
        _panelGroup.interactable = true;
        _panelGroup.blocksRaycasts = true;
        // 연출: DOTween으로 대화 전체를 짧게 등장시킵니다. 텍스트는 최종적으로 완전 불투명합니다.
        _panelGroup.alpha = _transitionDuration > 0f ? 0f : 1f;
        if (_transitionDuration > 0f)
        {
            _panelTween = _panelGroup.DOFade(1f, _transitionDuration).SetUpdate(true);
        }

        _portraitSetup = StartCoroutine(ShowPortraitPreviews());
        ShowCurrentLine();
    }

    /// <summary>
    /// 대화를 즉시 종료하고 다른 UI와 일러스트의 원래 상태를 복원합니다.
    /// </summary>
    public void EndTalk()
    {
        IsTalking = false;
        KillPresentation();
        ClearPortraitPreviews();
        if (_baseView != null)
        {
            _baseView.EndTalkMode(this);
        }

        // 이미 연결한 입력이나 초기화가 중복 실행되지 않도록 합니다.
        if (!_initialized)
        {
            return;
        }

        // 외부에서 부모를 끄는 OnDisable 도중에는 Unity가 형제 순서 변경을 허용하지 않습니다.
        bool canRestoreOrder = _talkPanel != null && _talkPanel.activeInHierarchy;
        // i: 반복 값입니다.
        for (int i = 0; i < 2; i++)
        {
            var image = GetPortrait(i); // 이미지 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (image == null)
            {
                continue;
            }

            image.color = _portraitColors[i];
            image.sprite = _portraitSprites[i];
            image.transform.localScale = _portraitScales[i];
            if (canRestoreOrder)
            {
                image.transform.SetSiblingIndex(_portraitOrders[i]);
            }
        }

        if (canRestoreOrder && _talkWindow != null)
        {
            _talkWindow.transform.SetSiblingIndex(_windowOrder);
        }

        if (_talkerContent != null)
        {
            _talkerContent.maxVisibleCharacters = int.MaxValue;
        }

        if (_panelGroup != null)
        {
            _panelGroup.alpha = canRestoreOrder ? 1f : 0f;
            _panelGroup.interactable = false;
            _panelGroup.blocksRaycasts = false;
        }

        if (canRestoreOrder)
        {
            _talkPanel.transform.SetSiblingIndex(_panelOrder);
            _talkPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 표시 중인 문장은 즉시 완성하고, 완성된 문장에서는 다음 대사나 종료로 진행합니다.
    /// </summary>
    public void NextTalk()
    {
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!IsTalking)
        {
            return;
        }

        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (_isRevealing)
        {
            _textTween?.Kill();
            CompleteLine();
            return;
        }

        _lineIndex++;
        if (_lineIndex >= _lines.Length)
        {
            EndTalk();
        }
        else
        {
            ShowCurrentLine();
        }
    }

    /// <summary>
    /// 대사를 모두 표시하고 다음 또는 종료 버튼 문구를 갱신합니다.
    /// </summary>
    void CompleteLine()
    {
        _isRevealing = false;
        if (_talkerContent != null)
        {
            _talkerContent.maxVisibleCharacters = int.MaxValue;
        }

        if (_skipText != null)
        {
            _skipText.text = _lineIndex == _lines.Length - 1 ? "종료" : "다음";
        }
    }

    /// <summary>
    /// 현재 말하는 캐릭터 이름을 표시합니다.
    /// </summary>
    /// <param name="name">이름 값입니다.</param>
    void UpdateTalkerName(string name)
    {
        _talkerName.text = name;
    }

    /// <summary>
    /// 이름 텍스트와 분리된 대화 내용 텍스트를 갱신합니다.
    /// </summary>
    /// <param name="content">내용 값입니다.</param>
    void UpdateTalkerContent(string content)
    {
        _talkerContent.text = content ?? string.Empty;
    }

    /// <summary>
    /// 화자는 대화창 앞으로 확대하고, 청자는 대화창 뒤에서 어두워지도록 DOTween으로 전환합니다.
    /// </summary>
    /// <param name="speaker">화자 값입니다.</param>
    void PresentSpeaker(int speaker)
    {
        // tween: 트윈 값입니다.
        foreach (var tween in _portraitTweens)
        {
            tween?.Kill();
        }

        _portraitTweens.Clear();
        var listener = GetPortrait(1 - speaker); // 리스너 값입니다.
        var talker = GetPortrait(speaker); // 화자 값입니다.
        if (listener != null)
        {
            listener.transform.SetAsFirstSibling();
        }

        // 뒤에서부터 청자 → 대화창 → 화자 순으로 그립니다.
        // 계층 순서는 즉시 바꾸고 아래 DOTween으로 밝기와 크기를 부드럽게 전환합니다.
        if (_talkWindow != null)
        {
            _talkWindow.transform.SetAsLastSibling();
        }

        if (talker != null)
        {
            talker.transform.SetAsLastSibling();
        }

        // i: 반복 값입니다.
        for (int i = 0; i < 2; i++)
        {
            var image = GetPortrait(i); // 이미지 값입니다.
            // 참조나 대상의 유효 조건을 확인해 잘못된 처리를 막습니다.
            if (image == null)
            {
                continue;
            }

            bool speaking = i == speaker; // 발화 중 여부입니다.
            TalkPortraitPreview preview = _portraitPreviews[i];
            UnityEngine.UI.Graphic portrait = preview != null ? preview.Output : image;
            Color color = GetPortraitColor(i, speaking);

            Vector3 scale = _portraitScales[i] * (speaking ? _speakerScale : 0.6f); // 배율 값입니다.
            // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
            if (_transitionDuration <= 0f)
            {
                portrait.color = color;
                image.transform.localScale = scale;
                continue;
            }

            // 연출: DOTween의 색상과 크기 전환입니다. 좌우 위치는 바꾸지 않습니다.
            _portraitTweens.Add(portrait.DOColor(color, _transitionDuration).SetUpdate(true));
            _portraitTweens.Add(image.transform.DOScale(scale, _transitionDuration).SetEase(Ease.OutQuad).SetUpdate(true));
        }
    }

    #endregion

    #region 화면 및 탭 전환

    /// <summary>
    /// 대화 패널을 닫거나, 준비되어 있는 대사로 다시 엽니다. 임시 데이터는 자동 실행하지 않습니다.
    /// </summary>
    /// <param name="isOn">화면이나 항목을 표시할지 여부입니다.</param>
    public void SetTalkPanel(bool isOn)
    {
        if (!isOn)
        {
            EndTalk();
        }
        else if (!IsTalking)
        {
            BeginTalk(_playerName, _npcName, _lines);
        }
    }

    /// <summary>
    /// 현재 화자의 이름·대사를 갱신하고 글자 및 일러스트 연출을 시작합니다.
    /// </summary>
    void ShowCurrentLine()
    {
        var line = _lines[_lineIndex]; // 줄 값입니다.
        int speaker = Mathf.Clamp(line.SpeakerIndex, 0, 1); // 화자 값입니다.
        UpdateTalkerName(speaker == 0 ? _playerName : _npcName);
        UpdateTalkerContent(line.Content);
        PresentSpeaker(speaker);
        _talkerContent.maxVisibleCharacters = int.MaxValue;
        _talkerContent.ForceMeshUpdate();
        int count = _talkerContent.textInfo.characterCount; // 개수 값입니다.
        _textTween?.Kill();
        _isRevealing = _charactersPerSecond > 0f && count > 0;
        // 처리 조건이 맞지 않으면 이후 동작을 진행하지 않습니다.
        if (!_isRevealing)
        {
            CompleteLine();
            return;
        }

        if (_skipText != null)
        {
            _skipText.text = "Skip";
        }

        _talkerContent.maxVisibleCharacters = 0;
        // 연출: DOTween으로 TMP의 보이는 글자 수만 늘려 한글·리치 텍스트를 순차 표시합니다.
        _textTween = DOTween.To(() => _talkerContent.maxVisibleCharacters, value => _talkerContent.maxVisibleCharacters = value, count, count / _charactersPerSecond).SetEase(Ease.Linear).SetUpdate(true).OnComplete(CompleteLine);
    }

    #endregion

    #region 표시 데이터 및 보조 기능

    /// <summary>
    /// 왼쪽 주인공 또는 오른쪽 NPC의 기존 Image 연결을 안전하게 반환합니다.
    /// </summary>
    /// <param name="index">대상 항목의 인덱스입니다. 첫 항목은 0입니다.</param>
    Image GetPortrait(int index)
    {
        int slot = index == 0 ? _playerImageIndex : 1 - _playerImageIndex; // 슬롯 값입니다.
        return _characterImages != null && slot >= 0 && slot < _characterImages.Length ? _characterImages[slot] : null;
    }

    IEnumerator ShowPortraitPreviews()
    {
        // 씬 시작과 동시에 열린 대화도 Animator의 첫 프레임을 반영합니다.
        yield return null;
        _portraitSetup = null;

        PlayScene scene = GameManager.Instance != null ? GameManager.Instance.CurrentPlayScene : null;
        if (scene == null || scene.gameObject.scene != gameObject.scene)
            scene = FindInTalkScene<PlayScene>();

        Hero hero = scene != null ? scene.SceneHero : null;
        if (hero == null)
            hero = FindInTalkScene<Hero>();
        MonoBehaviour npc = scene != null ? scene.SceneBoatNPC : null;
        if (npc == null && scene is SushiPlayScene)
        {
            SushiManager sushi = FindInTalkScene<SushiManager>();
            if (sushi != null)
                npc = sushi.Cooker;
        }

        CreatePortraitPreview(0, hero);
        CreatePortraitPreview(1, npc);
    }

    Color GetPortraitColor(int slot, bool speaking)
    {
        Color color = _portraitColors[slot];
        if (_portraitPreviews[slot] != null)
            color = new Color(1f, 1f, 1f, color.a);

        if (!speaking)
        {
            color.r *= _listenerBrightness;
            color.g *= _listenerBrightness;
            color.b *= _listenerBrightness;
        }

        return color;
    }

    T FindInTalkScene<T>() where T : Component
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>();
            if (component != null)
                return component;
        }

        return null;
    }

    void CreatePortraitPreview(int slot, Component character)
    {
        try
        {
            TalkPortraitPreview preview = TalkPortraitPreview.Create(GetPortrait(slot), character);
            if (preview == null)
                return;

            _portraitPreviews[slot] = preview;
            int speaker = Mathf.Clamp(_lines[_lineIndex].SpeakerIndex, 0, 1);
            preview.Output.color = GetPortraitColor(slot, slot == speaker);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"대화 캐릭터 프리뷰를 그리지 못했습니다. {exception.Message}", this);
        }
    }

    void ClearPortraitPreviews()
    {
        if (_portraitSetup != null)
        {
            StopCoroutine(_portraitSetup);
            _portraitSetup = null;
        }

        for (int i = 0; i < _portraitPreviews.Length; i++)
        {
            _portraitPreviews[i]?.Dispose();
            _portraitPreviews[i] = null;
        }
    }

    #endregion

    #region 표시 연출 및 정리

    /// <summary>
    /// 이 대화에서 만든 트윈만 종료해 다른 UI의 DOTween 연출을 보존합니다.
    /// </summary>
    void KillPresentation()
    {
        _textTween?.Kill();
        _panelTween?.Kill();
        // tween: 트윈 값입니다.
        foreach (var tween in _portraitTweens)
        {
            tween?.Kill();
        }

        _portraitTweens.Clear();
        _textTween = null;
        _panelTween = null;
        _isRevealing = false;
    }

    #endregion

    #region 임시 데이터 및 테스트

    /// <summary>
    /// 플레이 모드의 컴포넌트 ContextMenu에서 임시 대화를 처음부터 시작합니다.
    /// </summary>
    [ContextMenu("대화 테스트/임시 대화 시작")]
    public void TestTalk()
    {
        // 실행 모드 조건이 맞지 않으면 테스트나 연출을 진행하지 않습니다.
        if (!Application.isPlaying)
        {
            Debug.Log("대화 테스트는 플레이 모드에서 실행해주세요.", this);
            return;
        }

        BeginTalk(_testPlayerName, _testNpcName, _testLines);
    }

    /// <summary>
    /// 플레이 모드의 ContextMenu에서 대화를 중단하고 다른 UI를 복원합니다.
    /// </summary>
    [ContextMenu("대화 테스트/대화 종료")]
    public void TestEndTalk()
    {
        if (Application.isPlaying)
        {
            EndTalk();
        }
    }

    #endregion

}
