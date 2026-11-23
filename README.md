# 팀프로젝트 캐치앤쿡

## 프로젝트 개요

Unity 기반 잠수 탐험 게임의 개인 담당 파트 기술 문서입니다. 선상에서 장비를 준비하고, 수중에서 탐색과 전투를 진행한 뒤, 귀환해 획득물을 정산합니다.

## 플레이 시연 영상

https://github.com/user-attachments/assets/9c62803c-12c2-4330-9af4-322495df6297

[720p 원본 자동재생 · 30fps · 12분 57초](https://lmglmg01.github.io/CatchAndCook/)

## 담당 범위

| 번호 | 담당 영역 | 구현 및 연동 범위 |
| --- | --- | --- |
| 1 | [GameManager 기반 씬 전환과 공용 상태 연동](#1-gamemanager-기반-씬-전환과-공용-상태-연동) | 기존 싱글톤에 선상·다이브·야간 씬 전환과 저장 복원 연결 |
| 2 | [플레이어 캐릭터 조작과 다이브 HUD](#2-플레이어-캐릭터-조작과-다이브-hud) | 수중 이동, 무기 전환과 조준, 잠수 상태 표시 |
| 3 | [히어로 피격과 산소 감소](#3-히어로-피격과-산소-감소) | 공격형 물고기의 피해 전달, 방어력 적용과 연속 피격 방지 |
| 4 | [상점 거래와 공용 인벤토리](#4-상점-거래와-공용-인벤토리) | 구매·판매 검증, 거래 실패 시 골드·수량 복구 |
| 5 | [레벨별 퀘스트와 진행 이벤트](#5-레벨별-퀘스트와-진행-이벤트) | 레벨 1~10 데이터, 선행 조건과 목표 이벤트 연동 |
| 6 | [IInteractable 기반 상호작용](#6-iinteractable-기반-상호작용) | 공통 인터페이스, 거리 기반 대상 선택, NPC 메뉴와 수중 오브젝트 연동 |
| 7 | [전기 뱀장어 보스전](#7-전기-뱀장어-보스전) | 보스 모델·리깅·애니메이션 제작, 돌진 물기·흡입·꼬리 휩쓸기 구현 |
| 8 | [JSON 5슬롯 저장·불러오기](#8-json-5슬롯-저장불러오기) | 시작 메뉴 연동, 파일 백업과 이전 저장 호환 |
| 9 | [정상·강제 귀환 정산](#9-정상강제-귀환-정산) | 귀환 유형별 획득 규칙 및 인벤토리·퀘스트 반영 |
| 10 | [Unity 에디터 아이콘 제작 도구 개선](#10-unity-에디터-아이콘-제작-도구-개선) | 캡처 조명·광택·실루엣 옵션과 투명 PNG 경계 처리 |

## 담당 시스템

### 1. GameManager 기반 씬 전환과 공용 상태 연동

| 구분 | 내용 |
| --- | --- |
| 문제 | 선상·다이브·야간 씬을 오가면 씬 오브젝트는 교체되지만 인벤토리와 진행 데이터는 이어져야 합니다. |
| 해결 | 기존 `GameManager` 싱글톤을 활용해 씬별 진행을 연결했습니다. 저장 로드 시 `GameSaveService`의 복원 경로를 호출하고, 오후 보트와 야간 씬 이름 변경에 맞춰 이동 목적지를 수정했습니다. |
| 결과 | 잠수 여부와 귀환 결과에 따라 다음 시간대 씬으로 진입하며, 슬롯을 불러오면 공용 진행 상태를 복원합니다. |

~~~mermaid
flowchart LR
    A[선상] --> B{다음 행동}
    B -->|잠수| C[다이브]
    C -->|귀환 정산| D[다음 시간대 씬]
    B -->|시간 보내기| D
    D --> E[GameManager가 새 PlayScene 연결]
    F[저장 슬롯 불러오기] --> G[저장된 씬과 공용 상태 복원]
    G --> E
~~~

~~~csharp
// GameManager.cs · 잠수 귀환 시 오전은 오후로, 오후는 밤으로 전환
public string AdvanceAfterDive()
{
    if (_diveTransitionCompleted) return null;
    if (_dayPhase == DayPhase.Morning)
    {
        _diveTransitionCompleted = true;
        SetDayPhase(DayPhase.Afternoon);
        return TutorialDestination(TutorialSceneSettings.Chapter.Dive) ?? AfternoonBoatScene;
    }
    if (_dayPhase == DayPhase.Afternoon)
    {
        _diveTransitionCompleted = true;
        SetDayPhase(DayPhase.Night);
        return NightBoatScene;
    }
    return null;
}

// DivePlayScene.cs · 귀환 정산을 마치면 GameManager가 돌려준 씬으로 이동한다.
string nextScene = _gameManager.AdvanceAfterDive();
if (string.IsNullOrEmpty(nextScene))
{
    Debug.LogError("다이브 완료 후 이동할 시간대 씬을 결정하지 못했습니다.", this);
    return;
}
ChangeScene(nextScene);
~~~

### 2. 플레이어 캐릭터 조작과 다이브 HUD

| 구분 | 내용 |
| --- | --- |
| 문제 | 무기 모드와 조준 상태에 따라 이동·공격 입력을 다르게 처리해야 합니다. |
| 해결 | 수중 이동과 방향 전환을 연결하고 나이프, 작살, 총을 전환할 수 있게 했습니다. 원거리 조준 중에는 이동을 제한하고 현재 무기 모드에 따라 공격 입력을 전달했습니다. |
| 결과 | 플레이어는 이동과 조준 상태를 구분해 조작할 수 있습니다. 다이브 HUD에 산소 비율과 경고색, 수심, 무기·탄약과 유틸리티 장착 정보를 연결했습니다. |

~~~mermaid
flowchart LR
    A[수중 조작 입력] --> B[UnderSeaHero]
    B -->|이동| C{원거리 조준 중인가?}
    C -->|예| D[이동 제한]
    C -->|아니오| E[수영과 방향 전환]
    B -->|무기 전환| F[나이프·작살·총 선택]
    F -->|공격 입력| G[장착 무기로 공격]
    F -->|현재 무기| H[다이브 HUD]
    I[산소·수심·탄약·장착 정보] --> H
~~~

~~~csharp
// UnderSeaHero.cs · 잠수 진입과 근접 공격 중에는 이동을 멈추고, 원거리 조준 중에는 이동 입력을 제한
public override void SetMoveInput(Vector2 direction)
{
    if (this == null || !isActiveAndEnabled) return;
    if (_diveEntryActive || IsMeleeAttacking)
    {
        // 공격 방향은 유지하고, 이동 입력과 달리기만 멈춘다.
        if (_runModel != null) _runModel.SetSprinting(false);
        MoveHero(Vector2.zero, 0f);
        return;
    }
    bool movementLocked = _isAiming || (_shooter != null && _shooter.IsFlying);
    direction = movementLocked ? Vector2.zero : Vector2.ClampMagnitude(direction, 1f);
    _facingDirection = direction;
    if (direction.x != 0f)
    {
        _facingLeft = direction.x < 0f;
    }

    if (_runModel == null) return;
    _runModel.SetSprinting(_sprintHeld && direction.sqrMagnitude > 0f && _runModel.MoveSpeed > 0f);
    MoveHero(direction, _runModel.EffectiveMoveSpeed);
}

// DiveCharacterView.Refresh · 남은 산소 비율로 게이지를 채우고, 25% 미만이면 경고 이미지를 쓴다.
float fraction = max > 0f ? current / max : 0f;
if (_oxygenFillImage != null)
{
    _oxygenFillImage.fillAmount = fraction;
    _oxygenFillImage.sprite = fraction < 0.25f && _oxygenWarningSprite != null ? _oxygenWarningSprite : _oxygenNormalSprite;
    _oxygenFillImage.color = Color.white;
}
~~~

### 3. 히어로 피격과 산소 감소

| 구분 | 내용 |
| --- | --- |
| 문제 | 잠수 중 물고기에게 공격받으면 방어력에 따라 산소가 줄어야 하며, 피격 직후에는 잠시 보호가 필요합니다. |
| 해결 | 물고기의 `DamageableSensor`가 `IDamageable`을 통해 히어로에게 피해를 전달합니다. `HeroRunModel.TakeDamage`는 보호 시간이 지났는지 확인하고 공격 피해에서 방어력을 뺍니다. |
| 결과 | 방어력을 초과한 피해만큼 산소가 줄어듭니다. 남은 산소가 0이 되면 잠수 실패로 이어집니다. |

~~~mermaid
flowchart LR
    A[물고기의 공격이 히어로에게 적중] --> B{피격 보호 중인가?}
    B -->|예| C[산소 유지]
    B -->|아니오| D{피해가 방어력을 넘는가?}
    D -->|아니오| C
    D -->|예| E[보호 시간 시작 · 초과 피해만큼 산소 차감]
    E -->|산소 소진| F[잠수 실패]
~~~

~~~csharp
// IDamageable.cs · 공격 센서가 피격 대상에 피해량을 전달할 때 쓰는 팀 공용 인터페이스다.
public interface IDamageable
{
    void TakeDamage(float damage);
}

// HeroRunModel.cs · 피격 처리 발췌. 보호 시간에는 피해를 무시하고, 방어력을 넘는 피해만 산소에서 뺀다.
public class HeroRunModel : MonoBehaviour, IDamageable
{
    public void TakeDamage(float damage)
    {
        if (!(damage > 0f) || float.IsInfinity(damage) || currentHealth <= 0f || Time.time < _nextDamageTime) return;

        float reducedDamage = Mathf.Max(0f, damage - _defense);
        if (reducedDamage <= 0f) return;
        float appliedDamage = Mathf.Min(currentHealth, reducedDamage);
        _nextDamageTime = Time.time + _damageInvulnerabilityDuration;
        ConsumeOxygen(appliedDamage);
        Damaged?.Invoke(appliedDamage);
    }
}
~~~

### 4. 상점 거래와 공용 인벤토리

| 구분 | 내용 |
| --- | --- |
| 문제 | 구매와 판매는 골드와 아이템 수량을 각각 변경합니다. 한쪽만 반영된 상태에서 다음 작업이 실패할 수 있습니다. |
| 해결 | 구매 전에 상품·품절 여부·수용량·골드를 검사합니다. 차감 후 아이템 추가에 실패하면 골드를 반환합니다. 판매는 아이템 회수 후 골드 지급에 실패하면 아이템 수량을 복구합니다. |
| 결과 | 거래의 두 번째 처리에 실패할 때 먼저 변경한 값을 되돌리는 경로가 있습니다. 거래 결과는 팀 공용 상점·골드·인벤토리 UI에 반영합니다. |

~~~mermaid
flowchart LR
    A{상점 거래 유형} -->|구매| B[구매 조건 확인 후 골드 차감]
    A -->|판매| C[판매 조건 확인 후 아이템 회수]
    B --> D{아이템 지급 성공?}
    C --> E{골드 지급 성공?}
    D -->|예| F[거래 완료]
    E -->|예| F
    D -->|아니오| G[차감한 골드 반환]
    E -->|아니오| H[회수한 아이템 복구]
~~~

~~~csharp
// Shop.cs · 구매 시 소지금과 수용량을 확인하고 아이템 지급에 실패하면 골드를 반환
public bool TryBuy(UtilityItemData item, int amount, Inventory inventory)
{
    int index = Array.IndexOf(_saleItems, item);
    if (item == null || amount <= 0 || inventory == null || index < 0 || IsSoldOut(index)
        || !inventory.CanAdd(item, amount)
        || !TryGetTotal(item.PurchasePrice, amount, out float total)
        || !inventory.TrySpendGold(total)) return false;

    if (inventory.TryAdd(item, amount))
    {
        _soldOut[index] = true;
        SoldOutChanged?.Invoke(index, true);
        return true;
    }
    inventory.TryAddGold(total);
    return false;
}
~~~

### 5. 레벨별 퀘스트와 진행 이벤트

| 구분 | 내용 |
| --- | --- |
| 문제 | 퀘스트를 레벨과 선행 조건에 따라 해금하고 서로 다른 플레이 행동을 목표 진행에 반영해야 합니다. |
| 해결 | 레벨 1~10 메인·서브 퀘스트 데이터를 구성했습니다. 요구 레벨과 선행 퀘스트 완료 여부를 확인하고, 수집·지역 진입·조사·처치 이벤트를 진행 상태에 연결했습니다. |
| 결과 | 조건을 충족한 퀘스트를 활성화하고, 목표 유형에 해당하는 행동이 발생하면 진행도를 갱신합니다. 귀환으로 확보한 손질 생선은 아이템 데이터에 설정된 경험치를 반영합니다. |

~~~mermaid
flowchart LR
    A[레벨·선행 퀘스트 확인] -->|조건 충족 후 수락| B[퀘스트 활성화]
    B --> C{이벤트가 목표와 일치하는가?}
    D[수집·지역 진입·조사·처치] --> C
    C -->|아니오| E[진행도 유지]
    C -->|예| F[진행도 갱신]
    F -->|목표량 도달| G[퀘스트 완료]
~~~

~~~csharp
// QuestController.cs · 수집 목표와 획득 아이템의 ID를 비교하는 조건을 AddProgress에 넘긴다.
public void ReportItemCollected(ItemData item, int amount)
{
    if (item == null || string.IsNullOrEmpty(item.ItemId)) return;
    AddProgress(data => data is ItemCollectQuestData collect
        && collect.TargetItem != null
        && collect.TargetItem.ItemId == item.ItemId, amount);
}

// QuestController.cs · 남은 목표량만큼 진행도를 더하고 완료 이벤트 발행
void AddProgress(Predicate<QuestData> matches, int amount)
{
    if (amount <= 0) return;

    // 이벤트 구독자가 새 퀘스트를 수락해도 이번 알림에는 포함하지 않는다.
    foreach (QuestProgress progress in _quests.ToArray())
    {
        if (progress.IsCompleted || !matches(progress.Data)) continue;

        int remaining = progress.RequiredCount - progress.CurrentCount;
        progress.CurrentCount += Math.Min(amount, remaining);
        bool completed = progress.IsCompleted;
        QuestProgressChanged?.Invoke(progress);
        if (completed)
        {
            QuestCompleted?.Invoke(progress);
            AdvanceGroup(progress.Data);
        }
    }
}
~~~

### 6. IInteractable 기반 상호작용

| 구분 | 내용 |
| --- | --- |
| 문제 | 한 입력 범위에 여러 오브젝트가 들어올 수 있고, 대상마다 실행해야 할 행동이 다릅니다. |
| 해결 | `IInteractable`을 정의하고 `InteractableSensor`가 Collider 트리거로 후보를 관리하도록 했습니다. 상호작용 가능한 후보 중 콜라이더까지의 거리가 가장 가까운 대상을 선택해 동작을 실행합니다. |
| 결과 | 공통 입력으로 선상 NPC 메뉴, 수중 산소·탄약 보충, 귀환 등 대상별 동작을 호출합니다. NPC 메뉴를 여는 동안 이동을 제한하고 닫으면 조작을 복원합니다. |

~~~mermaid
flowchart LR
    A[트리거 진입·이탈] --> B[상호작용 후보 목록 갱신]
    B --> C{사용 가능한 후보가 있는가?}
    C -->|없음| D[대상 선택 해제]
    C -->|있음| E[가장 가까운 콜라이더의 대상 선택]
    E -->|상호작용 입력| F[IInteractable 실행]
    F -->|선상 NPC| G[NPC 메뉴 열기]
    F -->|수중 오브젝트| H[산소·탄약 보충 또는 귀환]
~~~

~~~csharp
// IInteractable.cs · 같은 입력으로 상호작용 가능 여부를 확인하고 대상의 동작을 실행한다.
public interface IInteractable
{
    bool CanInteract(Hero interactor);
    void Interact(Hero interactor);
}

// RecoveryHpInteractObject.cs · 산소통은 InteractObject의 Interact를 상속한다.
// Interact가 CanInteract로 사용 조건을 확인한 뒤, 아래 ApplyInteraction에서 산소를 보충한다.
public class RecoveryHpInteractObject : DiveSupplyInteractObject
{
    [Header("데이터")]
    [SerializeField] RecoveryHpInteractObjectData _data;

    public override bool CanInteract(Hero interactor)
    {
        return base.CanInteract(interactor)
            && _data != null
            && _data.RecoveryHpValue > 0f
            && interactor is UnderSeaHero;
    }

    protected override bool ApplyInteraction(Hero interactor)
    {
        if (interactor is not UnderSeaHero hero) return false;

        hero.RecoverOxygen(_data.RecoveryHpValue);
        return true;
    }
}
~~~

### 7. 전기 뱀장어 보스전

#### 1. 보스 모델과 애니메이션 제작

1. **콘셉트와 모델링** — ChatGPT로 전기 뱀장어의 콘셉트 이미지를 생성하고, 이 이미지를 Meshy AI에 입력해 3D 모델을 만들었습니다.
2. **메시 최적화** — Meshy AI의 리메시 기능으로 정점 수를 줄였습니다. Blender로 불러온 리메시 결과는 정점 **5,117개**였습니다.
3. **리깅과 애니메이션** — Codex에 Blender 리깅을 맡겨 몸통에서 꼬리까지 이어지는 뼈대와 입을 독립적으로 여닫는 턱 본을 구성했습니다. 루트를 포함한 본 17개로 수영, 물기, 좌우 꼬리 휩쓸기, 사망 등 **13개 애니메이션 클립**을 제작했습니다.
4. **Unity 적용** — 모델과 클립을 FBX로 내보낸 뒤 Unity의 Generic Rig와 Animator에 연결했습니다. 수영은 반복 재생하고, 물기와 사망 클립은 각각 공격과 처치 연출에 사용했습니다.

#### 2. 보스 전투 패턴

| 구분 | 내용 |
| --- | --- |
| 문제 | 공격 패턴마다 움직임과 피격 가능 구간이 다르지만 전투 진행과 보스 체력은 공유합니다. |
| 해결 | 추상 클래스 `BossPattern`으로 공통 동작을 정의하고 개별 프리팹에 패턴을 연결했습니다. 컨트롤러가 완료 이벤트를 받아 다음 패턴을 실행하며, 단계별로 피격 가능한 콜라이더를 제어합니다. |
| 결과 | 돌진 물기·흡입·꼬리 휩쓸기가 각각 다른 전투 흐름으로 진행됩니다. 여러 부위의 피격은 공통 체력에 반영되며, 꼬리 노출 단계에는 플레이어가 반격할 수 있습니다. |

~~~mermaid
flowchart LR
    A[전투 시작] --> B{컨트롤러의 패턴 선택}
    B -->|돌진 물기| C[돌진 후 물기]
    B -->|흡입| D[플레이어 끌어당기기]
    B -->|꼬리 휩쓸기| E[꼬리 공격과 노출]
    C --> F[패턴 완료 이벤트]
    D --> F
    E --> F
    F -->|전투 진행 중| B
~~~

~~~csharp
// BossPattern.cs · 시작과 종료에 관련된 멤버 발췌. 각 패턴은 시작 동작을 구현하고 완료 시 자신을 알린다.
public abstract class BossPattern : MonoBehaviour, IDamageable
{
    public event Action<BossPattern> PatternCompleted;
    public abstract void ActivatePattern();

    protected void EndPattern()
    {
        if (!IsActive) return;
        CancelPattern();
        PatternCompleted?.Invoke(this);
    }
}

// BossPatternController.PreparePatterns · 패턴을 준비할 때 완료 이벤트에 처리 메서드를 연결한다.
pattern.PatternCompleted += OnPatternCompleted;

// BossPatternController.cs · 현재 패턴에서 온 완료 신호만 받아 대기를 푼다.
void OnPatternCompleted(BossPattern pattern)
{
    if (!IsRunning || !_waitingForPatternEnd || pattern != CurrentPattern) return;
    _waitingForPatternEnd = false;
    CurrentPattern = null;
    pattern.gameObject.SetActive(false);
}
~~~

### 8. JSON 5슬롯 저장·불러오기

| 구분 | 내용 |
| --- | --- |
| 문제 | 여러 플레이 기록을 구분하고, 저장 파일을 읽지 못하더라도 진행 상태를 복구할 수 있어야 합니다. |
| 해결 | 슬롯마다 진행 상태를 JSON으로 저장합니다. 먼저 임시 파일에 쓴 뒤 기존 파일을 백업하고 새 파일로 교체합니다. 슬롯 파일을 읽지 못하면 같은 슬롯의 백업 파일에서 진행 상태를 복구합니다. |
| 결과 | 시작 메뉴에서 새 게임을 시작하거나 기존 기록을 이어서 플레이할 수 있습니다. 원하는 슬롯을 골라 불러오거나 삭제할 수도 있습니다. |

~~~mermaid
flowchart LR
    A[임시 JSON 작성] -->|기존 파일 백업 후 교체| B[슬롯별 저장 파일]
    B --> C{슬롯 파일 읽기 성공?}
    D[시작 메뉴에서 슬롯 선택] --> C
    C -->|예| E[저장된 진행 상태 복원]
    C -->|아니오| F{백업 읽기 성공?}
    F -->|예| E
    F -->|아니오| G[불러오기 실패 안내]
~~~

~~~csharp
// GameSaveService.TryWriteSlot · JSON을 임시 파일에 쓴다. 기존 슬롯을 교체할 때 이전 파일이 .bak이 된다.
File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
else File.Move(temporary, path);

// GameSaveService.cs · 슬롯 읽기에 실패하면 같은 슬롯의 백업 파일로 재시도
static bool TryReadSlotFile(int slot, out GameSaveData save, out string error, out string source)
{
    source = GetSlotPath(slot);
    if (TryReadFile(source, out save, out error)) return true;
    string primaryError = error;
    source += ".bak";
    if (TryReadFile(source, out save, out error)) return true;
    error = primaryError;
    return false;
}
~~~

### 9. 정상·강제 귀환 정산

| 구분 | 내용 |
| --- | --- |
| 문제 | 정상 귀환과 산소 소진 귀환의 획득물 규칙이 다르고, 퀘스트에는 실제로 가져온 아이템만 반영해야 합니다. |
| 해결 | 결과 화면에서 입력과 산소 소모를 멈춥니다. 팀 공용 UI가 확정한 목록을 이벤트로 받아 공용 인벤토리에 추가하고, 성공한 아이템을 수집 퀘스트에 보고합니다. |
| 결과 | 정상 귀환에서는 전부 가져오고 산소 소진 귀환에서는 선택한 한 종류의 보유 수량을 가져옵니다. 정산을 마치면 다음 시간대로 이동합니다. |

~~~mermaid
flowchart LR
    A[다이브 종료] --> B{귀환 사유}
    B -->|정상| C[전체 획득물 확정]
    B -->|산소 소진| D[한 종류 선택 후 확정]
    C --> E[공용 인벤토리에 추가]
    D --> E
    E -->|추가한 항목| F[수집 퀘스트에 보고]
    F --> G[정산 완료 후 시간대 전환]
~~~

~~~csharp
// DiveResultView.cs · 팀 공용 결과 화면이 확정한 아이템과 수량을 정산 쪽에 전달하는 이벤트다.
public event Action<IReadOnlyList<Inventory.BagItem>> OnItemsConfirmed;

// DiveResultView.Confirm · 정상 귀환은 전체 목록, 산소 소진 귀환은 선택한 한 종류의 수량을 전달한다.
OnItemsConfirmed?.Invoke(Array.AsReadOnly(confirmedItems));

// DivePlayScene.cs · 결과 화면을 열 때 아래 정산 메서드를 이벤트에 연결한다.
_diveResultView.OnItemsConfirmed += OnDiveItemsConfirmed;

// DivePlayScene.cs · OnDiveItemsConfirmed 내부에서 확보한 수량을 인벤토리와 수집 퀘스트에 반영
foreach (var item in keptItems)
{
    if (!_inventory.TryAdd(item.ItemData, item.Count))
    {
        Debug.LogError($"다이브 획득물 정산 실패: {item.ItemData}", this);
        return;
    }
    _gameManager.QuestController?.ReportItemCollected(item.ItemData, item.Count);
    // 실제로 가져온 손질 생선만 획득 수량에 비례해 경험치를 지급한다.
    if (item.ItemData is FishFilletItemData fillet)
        _gameManager.PlayerProgress?.AddExperience(fillet.ExperienceOnAcquire * item.Count);
}
~~~

### 10. Unity 에디터 아이콘 제작 도구 개선

| 구분 | 내용 |
| --- | --- |
| 문제 | 기존 모델을 아이콘으로 캡처할 때 강한 광택과 그림자가 남거나, 투명 PNG 가장자리가 어둡게 보일 수 있습니다. |
| 해결 | 기존 아이콘 제작 창에 그림자 없는 조명, 광택 제거, 검정 실루엣 옵션을 추가했습니다. 무광 설정은 캡처용 머티리얼 복제본에 적용하고, 렌더 결과의 알파를 PNG 저장 방식에 맞게 변환합니다. |
| 결과 | 에디터에서 용도에 따라 조명·광택·실루엣을 선택할 수 있습니다. 투명 경계의 색을 보정해 PNG로 내보내도록 캡처 흐름을 개선했습니다. |

~~~mermaid
flowchart LR
    A[모델과 조명 옵션 선택] --> B[프리뷰 모델 준비]
    B -->|광택 제거 사용 시| C[복제 머티리얼에 무광 적용]
    C --> D[모델 렌더링]
    B -->|기존 광택 유지| D
    D --> E{검정 실루엣 사용?}
    E -->|예| F[픽셀을 검정으로 변환]
    E -->|아니오| G[반투명 경계 색 보정]
    F --> H[투명 PNG 저장]
    G --> H
~~~

~~~csharp
// IconCaptureService.cs · Capture 내부에서 실루엣은 투명도를 유지하고, 일반 아이콘은 반투명 경계의 RGB를 보정
if (settings.blackSilhouette)
{
    pixels[index] = new Color32(0, 0, 0, pixel.a);
}
else if (pixel.a > 0 && pixel.a < 255)
{
    // PNG의 반투명 가장자리가 어두워지지 않도록 알파가 곱해진 RGB를 원래 값으로 되돌린다.
    float factor = 255f / pixel.a;
    pixel.r = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.r * factor));
    pixel.g = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.g * factor));
    pixel.b = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.b * factor)); pixels[index] = pixel;
}
~~~

## 트러블슈팅

### 1. 물고기 스포너의 반복 틱

**발견** — 잠수 씬을 Unity Profiler로 살펴보다가 `FishSpawner.FixedUpdate()`에서 물고기 무리의 패턴 판단이 반복되는 것을 발견했습니다. 스포너는 물리 틱마다 고정 갱신 대상인 모든 패턴의 `Tick()`을 호출했고, 무리가 늘수록 이동 판단과 재생성할 자리 확인도 함께 늘었습니다.

**수정 요청** — 물고기의 움직임은 부드럽게 유지하되, 이동 방향을 다시 판단하는 주기와 실제 이동을 갱신하는 주기를 분리해 달라고 요청했습니다. 재생성은 물고기가 풀로 반환될 때 해당 자리만 예약하도록 제안했습니다.

**반영 결과** — `FishPatternScheduler`는 무리별 판단 시각이 됐거나 장애물 회피처럼 즉시 판단할 요청이 있을 때만 `Tick()`을 호출합니다. 파동·점프 경로는 기존 물리 주기로 갱신하고, `FishRespawnScheduler`는 예약 시간이 된 자리만 재생성합니다.

~~~csharp
// 수정 전 · FishSpawner.FixedUpdate · 물리 틱마다 모든 대상 패턴의 판단을 실행한다.
foreach (FishSpawnPattern pattern in _fixedPatterns)
{
    pattern.Tick(Time.fixedDeltaTime);
}

// 수정 후 · FishPatternScheduler.Advance · 판단 시각이나 즉시 요청을 확인하고, 연속 이동은 따로 갱신한다.
if (_clock >= entry.NextDecision || pattern.DecisionRequested)
{
    // 조기 갱신에서도 마지막 판단 이후 실제 경과 시간만 전달합니다.
    float elapsed = (float)(_clock - entry.LastDecision);
    pattern.ConsumeDecisionRequest();
    pattern.Tick(elapsed);
    entry.LastDecision = _clock;

    // 최초 판단 이후 첫 간격을 분산하고 이후에는 설정된 주기를 유지합니다.
    float interval = pattern.DecisionInterval;
    entry.NextDecision = _clock + (entry.FirstDecision
        ? Random.Range(Mathf.Min(deltaTime, interval), interval)
        : interval);
    entry.FirstDecision = false;
}

// 파동과 점프의 진행만 매 물리 단계에 실행하며 판단 Tick은 재호출하지 않습니다.
if (pattern.RequiresPhysicsPath)
    pattern.TickPhysicsPath(deltaTime);
~~~

### 2. 초밥집 UI의 반복 상태 조회

**발견** — 초밥집에서 주문과 손님 상태가 그대로인데도 Unity Profiler에 프레젠터의 UI 갱신이 반복해서 나타났습니다. `IjakayaPresenter`는 0.25초마다 손님·의자·운반자를 다시 찾아 UI 연결을 확인했고, 조리 중인 주문과 완료된 주문 목록은 매 프레임 뷰에 전달했습니다.

**수정 요청** — 손님이 주문하거나 직원이 서빙을 마친 시점에 모델이 이벤트를 보내 해당 UI만 갱신하도록 요청했습니다. 이미 화면에 있는 대상은 프레젠터가 처음 켜지거나 다시 활성화될 때 한 번 연결하도록 했습니다.

**반영 결과** — 캐릭터 UI는 활성화·비활성화 이벤트로 등록하고 해제해 0.25초 주기의 전체 검색을 없앴습니다. 주문 시작(`OrderStarted`)과 서빙 완료(`FoodServed`)는 해당 표시를 갱신하고, 조리 목록은 변경되거나 슬롯을 다시 맞춰야 할 때 뷰에 전달합니다. 영업 상태와 의자 청소 등 조회가 남은 항목은 후속 이벤트 연결 대상으로 기록했습니다.

~~~csharp
// 수정 전 · IjakayaPresenter.LateUpdate · 0.25초마다 UI 대상을 다시 찾고 매 프레임 조리 목록을 전달한다.
if (_initialized && Time.unscaledTime >= _nextWorldRefresh)
{
    _nextWorldRefresh = Time.unscaledTime + .25f;
    ConnectWorldDisplays();
}
if (_initialized && _cookingDisplay != null && _shop.Cooker != null)
    _cookingDisplay.DisplayOrders(_shop.Cooker.CookingOrders, _shop.Cooker.CompletedOrders);

// 수정 후 · IjakayaPresenter · 이벤트로 UI를 연결하고 동기화가 필요할 때만 조리 목록을 전달한다.
void SubscribeWorldCanvases()
{
    if (_worldEventsSubscribed) return;
    SushiWorldCanvasView.Available += RegisterWorldCanvas;
    SushiWorldCanvasView.Unavailable += UnregisterWorldCanvas;
    _worldEventsSubscribed = true;
}

// RegisterWorldCanvas의 손님 등록 분기
guest.OrderStarted -= NotifyGuestDisplayChanged;
guest.OrderStarted += NotifyGuestDisplayChanged;

// RegisterWorldCanvas의 운반자 등록 분기
carrier.FoodServed -= HandleFoodServed;
carrier.FoodServed += HandleFoodServed;

// RefreshCookingDisplays의 조리 목록 갱신 조건
if (_cookingOrdersDirty || _cookingDisplay.NeedsOrderSync)
{
    _cookingDisplay.DisplayOrders(_cookingSource.CookingOrders, _cookingSource.CompletedOrders);
    _cookingOrdersDirty = false;
}
~~~

## 개발 환경

### 1. Unity · C#

**Unity 6 (6000.0.68f1) 게임플레이 개발** — 컴포넌트별로 플레이어 조작·대상 감지·씬 진행·UI 갱신의 역할을 나눴습니다. `OnEnable`과 `OnDisable`에 이벤트 구독과 해제를 연결하고, 씬 전환 중 공용 진행 상태를 유지하도록 구성했습니다. Collider 트리거로 상호작용 대상을 감지하고, 프리팹으로 보스 패턴과 귀환 결과 UI를 씬에 연결했습니다.

### 2. 에셋과 협업 도구

#### 1. 게임에 사용한 에셋

| 사용 장면 | 에셋 | 게임에서 쓰인 곳 |
| --- | --- | --- |
| 선상 | **FISHING BOAT LOWPOLY** | 선상 장면의 어선 선체와 갑판을 구성하는 3D 모델입니다. |
| 캐릭터 | **Funny Characters** (ithappy) | 선상·수중 캐릭터의 동작과 초밥집 요리사의 의상에 쓰였습니다. |
| 다이브 | **3D Characters-Fish** (LayerLab) | 블루탱·가오리·해마의 모델과 수영 애니메이션, 산호 등 수중 배경에 쓰였습니다. |
| 수중 보급 | **O2Canister**, **BulletBox** | 잠수 중 상호작용하는 산소통과 탄약 상자의 3D 모델입니다. 사용을 마치면 `DiveSupplyDissolve` 셰이더로 사라지는 모습을 표현합니다. |
| 전투 효과 | **Hits Effects FREE** (Matthew Guz), **Particle Pack** (Unity Technologies) | 다이브 피격에는 `Basic Hit` 효과를, 보스전에는 먼지와 돌 파편 파티클을 사용했습니다. |
| 전기 뱀장어 보스 | **자체 제작 모델·애니메이션** | 콘셉트 이미지에서 제작한 보스 모델과 전용 FBX 클립을 사용했습니다. [모델과 애니메이션 제작 과정](#1-보스-모델과-애니메이션-제작) |
| 초밥집 | **Izakaya Asset Pack 1.1** | 젓가락, 사케 병과 잔, 계란말이 모델로 식당의 소품과 음식을 구성했습니다. |
| 인벤토리·상점 | **FishIcons_LowPoly**, **RestaurantIcons_LowPoly** | 어획물과 쌀·소금·간장 같은 식재료를 구분하는 아이콘입니다. |
| 다이브 HUD | **DOTween** | 원거리 무기 카드가 앞뒤로 바뀌는 전환과 산소 부족 경고의 점멸·페이드에 사용했습니다. |

#### 2. 개발·협업 도구

- **ChatGPT · Meshy AI · Blender** — 보스 콘셉트 이미지 생성, 3D 모델링과 리메시, 리깅과 애니메이션 제작에 각각 사용했습니다.
- **Codex** — Blender의 리깅·애니메이션 작업을 지시하고 FBX를 다시 불러와 결과를 검증했습니다. Unity C# 구현과 오류 분석에도 활용했습니다.
- **Unity** — 직접 제작한 보스의 머티리얼과 Animator를 설정하고, 보트 머티리얼과 외곽선 셰이더를 수정했습니다.
- **Git · GitHub · GitHub Desktop** — 기능 브랜치와 Pull Request로 팀 변경 사항을 통합했습니다. 병합 충돌을 해결하고 시작 씬·빌드 진입 경로, TMP 폰트 아틀라스를 복구했습니다.
