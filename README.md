# 팀프로젝트 · DaveTheDiver

Unity 기반 잠수 탐험 게임의 개인 담당 파트 기술 문서입니다. 선상에서 장비를 준비하고, 수중에서 탐색과 전투를 진행한 뒤, 귀환해 획득물을 정산합니다.

## 플레이 시연 영상

https://github.com/user-attachments/assets/e01d7e89-9c93-46f6-91e6-f97b23a0b7b2

한글 자막과 함께 튜토리얼, 두 번의 잠수, 초밥집 영업을 차례로 볼 수 있습니다. 9분 48초부터 돌연변이 전기 뱀장어 보스전이 시작됩니다.

[720p 원본 화질로 재생 · 12분 57초](https://lmglmg01.github.io/TeamProject/)

## 1. 담당 범위

| 번호 | 담당 영역 | 구현 및 연동 범위 |
| --- | --- | --- |
| 1 | [GameManager 기반 씬 전환과 공용 상태 연동](#21-gamemanager-기반-씬-전환과-공용-상태-연동) | 기존 싱글톤에 선상·다이브·야간 씬 전환과 저장 복원 연결 |
| 2 | [플레이어 캐릭터 조작과 다이브 HUD](#22-플레이어-캐릭터-조작과-다이브-hud) | 수중 이동, 무기 전환과 조준, 잠수 상태 표시 |
| 3 | [피격 판정과 DamageableSensor 개선](#23-피격-판정과-damageablesensor-개선) | 기존 피해 인터페이스를 활용한 보스 콜라이더별 피격 필터 |
| 4 | [상점 거래와 공용 인벤토리](#24-상점-거래와-공용-인벤토리) | 구매·판매 검증, 거래 실패 시 골드·수량 복구 |
| 5 | [레벨별 퀘스트와 진행 이벤트](#25-레벨별-퀘스트와-진행-이벤트) | 레벨 1~10 데이터, 선행 조건과 목표 이벤트 연동 |
| 6 | [IInteractable 기반 상호작용](#26-iinteractable-기반-상호작용) | 공통 인터페이스, 거리 기반 대상 선택, NPC 메뉴와 수중 오브젝트 연동 |
| 7 | [전기 뱀장어 보스전](#27-전기-뱀장어-보스전) | 돌진 물기·흡입·꼬리 휩쓸기와 전투 씬 구성 |
| 8 | [JSON 5슬롯 저장·불러오기](#28-json-5슬롯-저장불러오기) | 시작 메뉴 연동, 파일 백업과 이전 저장 호환 |
| 9 | [정상·강제 귀환 정산](#29-정상강제-귀환-정산) | 귀환 유형별 획득 규칙 및 인벤토리·퀘스트 반영 |
| 10 | [Unity 에디터 아이콘 제작 도구 개선](#210-unity-에디터-아이콘-제작-도구-개선) | 캡처 조명·광택·실루엣 옵션과 투명 PNG 경계 처리 |

## 2. 담당 시스템

각 구조도 아래의 **구현 코드**를 펼치면 해당 동작을 맡는 C# 메서드를 볼 수 있습니다. 코드는 원본 프로젝트 커밋 `b1b5ebe6`에서 발췌했습니다.

### 2.1 GameManager 기반 씬 전환과 공용 상태 연동

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

<details>
<summary><strong>구현 코드</strong> · 잠수 후 시간대 전환과 씬 연결</summary>

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
~~~

~~~csharp
// GameManager.cs · 새로 로드한 씬에서 활성 PlayScene을 찾아 공용 매니저에 연결
void ConnectLoadedScene(Scene scene)
{
    if (!scene.IsValid() || !scene.isLoaded) return;
    BeginSceneDay(scene);
    foreach (GameObject root in scene.GetRootGameObjects())
    {
        foreach (PlayScene playScene in root.GetComponentsInChildren<PlayScene>(true))
        {
            if (!playScene.isActiveAndEnabled) continue;
            ConnectScene(playScene);
            return;
        }
    }
    GameSaveService.SaveAtSceneEntry(this, scene);
}
~~~

</details>

주요 컴포넌트는 `GameManager`, `PlayScene`, `GameSaveService`입니다. 본인 기여는 기존 매니저에 씬 이동과 저장을 연동한 범위입니다. 원본 커밋 `e38e4e76`, `95116bf3`, `ce46e343`, `4d1fae86`.

### 2.2 플레이어 캐릭터 조작과 다이브 HUD

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

<details>
<summary><strong>구현 코드</strong> · 수중 이동 입력과 산소 HUD</summary>

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
~~~

~~~csharp
// DiveCharacterView.cs · Refresh 내부에서 현재 산소를 게이지에 표시하고 25% 미만이면 경고 이미지로 교체
float max = _runModel != null ? Mathf.Max(0f, _runModel.MaxOxygen) : 0f; // 최대 값입니다.
float current = _runModel != null ? Mathf.Clamp(_runModel.CurrentOxygen, 0f, max) : 0f; // 현재 값입니다.
UpdateLowOxygenNotice(current, max);
float fraction = max > 0f ? current / max : 0f; // 비율 값입니다.
if (_oxygenText != null)
{
    _oxygenText.text = current.ToString("0");
}

if (_oxygenFillImage != null)
{
    _oxygenFillImage.fillAmount = fraction;
    // 25% 미만일 때만 경고 리소스로 바꾸고 회복하면 원본으로 되돌립니다.
    _oxygenFillImage.sprite = fraction < 0.25f && _oxygenWarningSprite != null ? _oxygenWarningSprite : _oxygenNormalSprite;
    _oxygenFillImage.color = Color.white;
}
~~~

</details>

주요 컴포넌트는 `UnderSeaHero`, `DiveCharacterView`, `GameplayViewConnector`입니다. 원본 커밋 `d613d0bc`, `e38e4e76`.

### 2.3 피격 판정과 DamageableSensor 개선

| 구분 | 내용 |
| --- | --- |
| 문제 | 보스 프리팹에 피격용 콜라이더와 감지·지형 접촉용 콜라이더가 같이 있으며, 패턴 단계마다 피해를 받을 수 있는 부위가 달라집니다. |
| 해결 | 기존 `IDamageable`과 `DamageableSensor`에 보스 패턴의 콜라이더별 피격 조건을 연결했습니다. 콜라이더 활성 여부와 `BossPattern.CanReceiveHit`를 검사한 뒤 피해를 전달합니다. |
| 결과 | 감지·접촉용 콜라이더를 피격 대상에서 제외하고, 현재 단계에서 활성화된 피격 부위에 공격이 들어왔을 때 피해를 반영합니다. |

~~~mermaid
flowchart LR
    A[DamageableSensor가 보스 콜라이더 감지] --> B{활성 피해 대상인가?}
    B -->|아니오| C[피격 제외]
    B -->|예| D{현재 패턴에서 맞을 수 있는 부위인가?}
    D -->|아니오| C
    D -->|예| E[보스 공용 체력에 피해 반영]
~~~

<details>
<summary><strong>구현 코드</strong> · 보스의 피격 부위 판정과 피해 전달</summary>

~~~csharp
// DamageableSensor.cs · 충돌한 부위가 현재 패턴에서 피해를 받을 수 있는지 검사
static IDamageable GetDamageable(Collider target)
{
    if (target == null) return null;
    var damageable = target.GetComponentInParent(typeof(IDamageable)) as IDamageable;
    // 감지 센서나 지형 접촉용 콜라이더가 공격 대상으로 처리되지 않게 한다.
    if (damageable is BossPattern pattern && !pattern.CanReceiveHit(target)) return null;
    return damageable;
}
~~~

~~~csharp
// BossPattern.cs · 등록된 피격 콜라이더를 확인하고 보스의 공용 체력에 피해 전달
public bool CanReceiveDamage => _damageable && IsActive && isActiveAndEnabled && _boss != null && _boss.CanReceiveDamage;
public bool CanReceiveHit(Collider hit) => CanReceiveDamage && hit != null && hit.enabled
    && IsRegisteredHurtCollider(hit) && IsHurtColliderInRange(hit);
protected virtual bool IsRegisteredHurtCollider(Collider hit) => _hurtColliders != null
    && Array.IndexOf(_hurtColliders, hit) >= 0;
protected virtual bool IsHurtColliderInRange(Collider hit) => true;
public void TakeDamage(float damage) { if (CanReceiveDamage) _boss.TakeDamage(damage); }
~~~

</details>

주요 컴포넌트는 `DamageableSensor`, `IDamageable`, `BossPattern`입니다. 본인 기여는 팀이 만든 인터페이스와 센서에 보스 피격 조건을 추가한 범위입니다. 원본 커밋 `02b3a0b6`.

### 2.4 상점 거래와 공용 인벤토리

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

<details>
<summary><strong>구현 코드</strong> · 상점 구매·판매와 거래 실패 복구</summary>

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

~~~csharp
// Shop.cs · 판매 대금 지급에 실패하면 회수한 아이템을 인벤토리에 복구
public bool TrySell(ItemData item, int amount, Inventory inventory)
{
    if (item == null || amount <= 0 || inventory == null
        || inventory.GetCount(item) < amount
        || !TryGetSellTotal(item, amount, out float total)
        || !inventory.CanAddGold(total)
        || !inventory.TryRemove(item, amount)) return false;

    if (inventory.TryAddGold(total)) return true;
    inventory.TryAdd(item, amount);
    return false;
}
~~~

</details>

| 주요 컴포넌트 | 역할 |
| --- | --- |
| `Shop` | 판매 상품과 품절 상태 관리, 거래 검증 및 실패 복구 |
| `Inventory` | 공용 골드·아이템 수량·유틸리티 장착 상태 관리 |
| `BoatNPCInteractionController`, `PlayScene` | NPC 메뉴 선택, 대화·상점·강화 화면 연결과 조작 제어 |
| `GameplayViewConnector` | 공용 UI에 상점 거래와 장비 강화 결과 반영 |

근거는 원본 저장소 커밋 `d613d0bc`, `e38e4e76`입니다. 장비 강화는 선상 UI와 공용 장비 시스템의 연동이 담당 범위입니다. 다이브 능력치 적용은 팀원 커밋 `83e0fe95`에서 확인됩니다.

### 2.5 레벨별 퀘스트와 진행 이벤트

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

<details>
<summary><strong>구현 코드</strong> · 메인 퀘스트 해금과 수집 목표 갱신</summary>

~~~csharp
// QuestController.cs · 요구 레벨과 이전 메인 퀘스트 그룹의 완료 여부 확인
public bool IsGroupUnlocked(QuestGroupData data)
{
    if (_questDataMap == null || data == null || CurrentLevel < data.RequiredLevel)
        return false;

    if (data.Type != QuestGroupData.QuestType.Main) return true;

    // Earlier main groups must be completed even if the player has already reached a higher level.
    foreach (QuestGroupData previous in _questDataMap.Groups)
    {
        if (previous == null || previous.Type != QuestGroupData.QuestType.Main
            || previous.RequiredLevel >= data.RequiredLevel) continue;
        GroupProgress progress = GetGroupProgress(previous.Type, previous.GroupId);
        if (progress == null || !progress.IsCompleted) return false;
    }
    return true;
}
~~~

~~~csharp
// QuestController.cs · 획득한 아이템의 ID가 수집 목표와 일치할 때만 진행 요청
public void ReportItemCollected(ItemData item, int amount)
{
    if (item == null || string.IsNullOrEmpty(item.ItemId)) return;
    AddProgress(data => data is ItemCollectQuestData collect
        && collect.TargetItem != null
        && collect.TargetItem.ItemId == item.ItemId, amount);
}
~~~

~~~csharp
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

</details>

주요 컴포넌트는 `QuestController`, `QuestAreaTrigger`, `QuestInteractableObject`, `QuestFishEventBridge`입니다. 원본 커밋 `af7b006b`, `e38e4e76`.

### 2.6 IInteractable 기반 상호작용

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

<details>
<summary><strong>구현 코드</strong> · 상호작용 인터페이스와 대상 선택</summary>

~~~csharp
// IInteractable.cs · 상호작용 인터페이스 전체 정의
public interface IInteractable
{
    /// <summary>Whether this hero can interact with the object right now.</summary>
    bool CanInteract(Hero interactor);

    /// <summary>Perform the object's interaction for this hero.</summary>
    void Interact(Hero interactor);
}
~~~

~~~csharp
// InteractableSensor.cs · FindNearestInteractable 내부에서 사용 가능한 대상 중 가장 가까운 콜라이더 선택
Component component = hit.GetComponentInParent(typeof(IInteractable));
if (!(component is IInteractable target) || component == null) continue;
if (component is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
if (!target.CanInteract(interactor)) continue;

float distance = (hit.ClosestPoint(position) - position).sqrMagnitude;
if (distance >= nearestDistance) continue;
nearestDistance = distance;
nearest = target;
~~~

</details>

<details>
<summary><strong>구현 코드</strong> · 수중 보급 상호작용 · 키 입력, 산소통, 탄약 상자</summary>

~~~csharp
// DiveSupplyHold.cs · Tick 내부에서 키를 떼면 진행도를 초기화하고 끝까지 누르면 보급품 사용
if (!held)
{
    _awaitRelease = false;
    if (_elapsed > 0f) ResetProgress();
    return;
}
if (_awaitRelease) return;

_elapsed += Time.deltaTime;
_fill.fillAmount = Mathf.Clamp01(_elapsed / _holdSeconds);
if (_elapsed < _holdSeconds) return;

_supply.Interact(hero);
Cancel();
~~~

~~~csharp
// InteractObject.cs · 보급 효과가 적용되면 단일 사용 오브젝트의 재사용 차단
public void Interact(Hero interactor)
{
    if (!CanInteract(interactor) || !ApplyInteraction(interactor)) return;
    if (interactor is UnderSeaHero) GameSfx.Play(GameSoundId.UseInteractable);
    if (!IsSingleUse) return;

    _isInteracted = true;
    if (_interactionCollider != null) _interactionCollider.enabled = false;
    SetDetected(false);
    FinishUse(_effect);
}
~~~

~~~csharp
// RecoveryHpInteractObject.cs · 산소 보급 상호작용 구현
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

~~~csharp
// RecoveryBulletInteractObject.cs · 탄약 보급 상호작용 구현
public class RecoveryBulletInteractObject : DiveSupplyInteractObject
{
    [Header("데이터")]
    [SerializeField] RecoveryBulletInteractObjectData _data;

    public override bool CanInteract(Hero interactor)
    {
        return base.CanInteract(interactor)
            && _data != null
            && _data.RecoveryBulletValue > 0
            && interactor is UnderSeaHero;
    }

    protected override bool ApplyInteraction(Hero interactor)
    {
        if (interactor is not UnderSeaHero hero) return false;

        hero.RecoverBullet(_data.RecoveryBulletValue);
        return true;
    }
}
~~~

~~~csharp
// DiveSupplyInteractObject.cs · 사용한 보급품에 소멸 셰이더를 적용하고 사라지는 연출 시작
protected override void FinishUse(GameObject effect)
{
    if (_supplyRenderer == null || _dissolveShader == null || _fadeSeconds <= 0f)
    {
        base.FinishUse(effect);
        Destroy(gameObject);
        return;
    }

    _dissolvingMaterial = new Material(_supplyRenderer.sharedMaterial);
    _dissolvingMaterial.shader = _dissolveShader;
    _dissolvingMaterial.renderQueue = -1;
    _supplyRenderer.sharedMaterial = _dissolvingMaterial;
    StartCoroutine(FadeOut(effect));
}
~~~

</details>

주요 컴포넌트는 `IInteractable`, `InteractableSensor`, `BoatNPCInteractionController`입니다. 원본 커밋 `d613d0bc`, `e38e4e76`.

### 2.7 전기 뱀장어 보스전

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

<details>
<summary><strong>구현 코드</strong> · 보스 패턴 선택과 완료 이벤트</summary>

~~~csharp
// BossPatternController.cs · 패턴을 선택해 활성화하고 현재 패턴의 완료 신호를 기다리는 전투 루프
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
~~~

~~~csharp
// BossPatternController.cs · 현재 패턴의 완료 이벤트가 도착하면 대기를 해제
void OnPatternCompleted(BossPattern pattern)
{
    if (!IsRunning || !_waitingForPatternEnd || pattern != CurrentPattern) return;
    _waitingForPatternEnd = false;
    CurrentPattern = null;
    pattern.gameObject.SetActive(false);
}
~~~

</details>

| 주요 컴포넌트 | 역할 |
| --- | --- |
| `BossPattern`, `BossPatternController` | 패턴 공통 동작, 피격 조건과 완료 이벤트, 전투 순서 |
| `RushBiteBossPattern`, `SuctionBossPattern`, `SweepBossPattern` | 개별 공격과 단계별 동작 |
| `BossBattleSession`, `BossTest.unity` | 전투 씬 구성, 전투 단계와 화면 연동 |

보스 모델·애니메이션 FBX를 Unity에 적용하고 전투 연출을 후속 수정했습니다. 근거는 원본 저장소 커밋 `faa3b53e`, `02b3a0b6`, `40209d19`입니다.

### 2.8 JSON 5슬롯 저장·불러오기

| 구분 | 내용 |
| --- | --- |
| 문제 | 여러 플레이 기록을 구분해야 하며, 파일 읽기 실패와 씬 이름 변경은 기존 기록의 불러오기에 영향을 줍니다. |
| 해결 | 슬롯별로 진행 상태를 JSON에 저장합니다. 임시 파일에 먼저 쓰고 이전 파일을 백업하며 교체합니다. 읽기 실패 시 백업을 시도하고, 당시 변경된 야간 씬 이름 `Sushi`는 불러올 때 `Sushi 2`로 매핑했습니다. |
| 결과 | 시작 메뉴에서 새 게임·이어하기·불러오기·슬롯 삭제를 처리할 수 있고, 당시 변경 전 야간 씬 이름이 담긴 저장도 새 씬으로 연결됩니다. |

~~~mermaid
flowchart LR
    A[임시 JSON 작성] -->|기존 파일 백업 후 교체| B[슬롯별 저장 파일]
    B --> C{슬롯 파일 읽기 성공?}
    D[시작 메뉴에서 슬롯 선택] --> C
    C -->|예| E[씬 이름 호환 처리 후 진행 복원]
    C -->|아니오| F{백업 읽기 성공?}
    F -->|예| E
    F -->|아니오| G[불러오기 실패 안내]
~~~

<details>
<summary><strong>구현 코드</strong> · 슬롯 JSON 저장과 백업 복구</summary>

~~~csharp
// GameSaveService.cs · 임시 파일을 작성한 뒤 기존 슬롯 파일을 백업하며 교체
static bool TryWriteSlot(int slot, GameSaveData save, out string error)
{
    error = null;
    string path = GetSlotPath(slot);
    string temporary = path + ".tmp";
    try
    {
        Directory.CreateDirectory(Application.persistentDataPath);
        File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
        return true;
    }
    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
    {
        error = exception.Message;
        return false;
    }
}
~~~

~~~csharp
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

</details>

| 주요 컴포넌트 | 역할 |
| --- | --- |
| `GameSaveData`, `GameSaveService` | 저장용 구조, 슬롯별 파일 입출력, 유효성 검증, 백업과 씬 이름 호환 |
| `StartSceneView` | 슬롯 선택과 새 게임·이어하기·삭제 UI |
| `GameManager`, `EditorBuildSettings.asset` | 시간대별 씬 진입, 빌드 시작 씬 등록 |

오후 보트 이름 변경에 맞춰 귀환 경로를 조정했고 병합 후 빌드의 시작 씬 순서를 복구했습니다. 야간 초밥집에서는 진입 경로와 기존 진행 데이터의 공용 저장 서비스 연동을 담당했습니다. 근거는 원본 저장소 커밋 `95116bf3`, `ce46e343`, `f36cb0af`, `4d1fae86`입니다.

### 2.9 정상·강제 귀환 정산

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

<details>
<summary><strong>구현 코드</strong> · 귀환 유형별 획득물과 정산</summary>

~~~csharp
// DiveResultView.cs · 팀 공용 결과 화면의 Confirm 내부에서 정상 귀환은 전체, 실패는 선택한 한 종류를 확정
Inventory.BagItem[] confirmedItems; // 확인 완료 아이템 목록입니다.
if (_isClear)
{
    confirmedItems = _items.ToArray();
}
else if (_selectedSlot != null)
{
    confirmedItems = new[]
    {
        new Inventory.BagItem(_selectedSlot.Item, _selectedSlot.Amount)
    };
}
else
{
    confirmedItems = Array.Empty<Inventory.BagItem>();
}

_confirmed = true;
UpdateGuideAndButton();
SetWindow(false);
OnItemsConfirmed?.Invoke(Array.AsReadOnly(confirmedItems));
~~~

~~~csharp
// DivePlayScene.cs · OnDiveItemsConfirmed 내부에서 확보한 수량을 인벤토리와 수집 퀘스트에 반영
foreach (var item in keptItems)
{
    if (!_inventory.TryAdd(item.ItemData, item.Count))
    {
        Debug.LogError($"다이브 획득물 정산 실패: {item.ItemData}", this);
        return;
    }
    _gameManager.QuestController?.ReportItemCollected(item.ItemData, item.Count);
    // Only retained fish fillets grant their configured experience per item.
    if (item.ItemData is FishFilletItemData fillet)
        _gameManager.PlayerProgress?.AddExperience(fillet.ExperienceOnAcquire * item.Count);
}
~~~

</details>

| 주요 컴포넌트 | 역할 |
| --- | --- |
| `PlayScene` | 귀환 시작, 결과 확정 이벤트 구독, 공용 인벤토리와 다음 씬 연결 |
| `DiveResultView` | 팀 공용 결과 화면. 귀환 유형에 따라 확정할 아이템 전달 |
| `QuestController` | 수집 진행 수신과 요구 레벨·선행 퀘스트에 따른 해금 |

결과 화면의 확정 이벤트에 귀환 정산을 연결했습니다. 원본 커밋 `f74d678a`, `e38e4e76`.

### 2.10 Unity 에디터 아이콘 제작 도구 개선

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

<details>
<summary><strong>구현 코드</strong> · 아이콘 실루엣과 반투명 경계 처리</summary>

~~~csharp
// IconCaptureService.cs · Capture 내부에서 실루엣은 투명도를 유지하고, 일반 아이콘은 반투명 경계의 RGB를 보정
if (settings.blackSilhouette)
{
    pixels[index] = new Color32(0, 0, 0, pixel.a);
}
else if (pixel.a > 0 && pixel.a < 255)
{
    // Render targets store coverage-premultiplied edges. PNG needs straight alpha.
    float factor = 255f / pixel.a;
    pixel.r = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.r * factor));
    pixel.g = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.g * factor));
    pixel.b = (byte)Mathf.Min(255, Mathf.RoundToInt(pixel.b * factor)); pixels[index] = pixel;
}
~~~

</details>

주요 컴포넌트는 `IconCaptureService`, `ItemIconGeneratorWindow`입니다. 기존 에디터 도구의 캡처 옵션과 이미지 처리 개선이 본인 기여 범위입니다. 원본 커밋 `7f18eadc`.

## 3. 보유 기술과 개발 도구

### 3.1 Unity · C#

**Unity 6 (6000.0.68f1) 게임플레이 개발** — 컴포넌트별로 플레이어 조작·대상 감지·씬 진행·UI 갱신의 역할을 나눴습니다. `OnEnable`과 `OnDisable`에 이벤트 구독과 해제를 연결하고, 씬 전환 중 공용 진행 상태를 유지하도록 구성했습니다. Collider 트리거로 상호작용 대상을 감지하고, 프리팹으로 보스 패턴과 귀환 결과 UI를 씬에 연결했습니다.

**C# 중급 · Unity 게임플레이 구현 기준** — 인터페이스로 상호작용 대상의 공통 동작을 정의하고, 추상 클래스와 다형성으로 서로 다른 보스 패턴을 공통 흐름에서 제어했습니다. 이벤트와 코루틴으로 패턴 진행과 결과 확정 흐름을 구성했습니다. `List<T>`와 `HashSet<T>`로 대상 목록과 중복 등록을 관리했으며, 저장 데이터와 읽기 전용 슬롯 정보를 별도 타입으로 구성했습니다. JSON 직렬화, .NET 파일 입출력과 예외 처리로 슬롯 저장·백업·기존 데이터 호환을 처리했습니다.

### 3.2 에셋과 협업 도구

#### 게임에 사용한 에셋

| 사용 장면 | 에셋 | 게임에서 쓰인 곳 |
| --- | --- | --- |
| 선상 | **FISHING BOAT LOWPOLY** | 선상 장면의 어선 선체와 갑판을 구성하는 3D 모델입니다. |
| 캐릭터 | **Funny Characters** (ithappy) | 선상·수중 캐릭터의 동작과 초밥집 요리사의 의상에 쓰였습니다. |
| 다이브 | **3D Characters-Fish** (LayerLab) | 블루탱·가오리·해마의 모델과 수영 애니메이션, 산호 등 수중 배경에 쓰였습니다. |
| 수중 보급 | **O2Canister**, **BulletBox** | 잠수 중 상호작용하는 산소통과 탄약 상자의 3D 모델입니다. 사용을 마치면 `DiveSupplyDissolve` 셰이더로 사라지는 모습을 표현합니다. |
| 전투 효과 | **Hits Effects FREE** (Matthew Guz), **Particle Pack** (Unity Technologies) | 다이브 피격에는 `Basic Hit` 효과를, 보스전에는 먼지와 돌 파편 파티클을 사용했습니다. |
| 전기 뱀장어 보스 | **EelBoss_Animations** | 물기(`Bite`), 수영(`Swim`), 사망(`Death`) FBX 클립을 보스 애니메이터에 연결했습니다. |
| 초밥집 | **Izakaya Asset Pack 1.1** | 젓가락, 사케 병과 잔, 계란말이 모델로 식당의 소품과 음식을 구성했습니다. |
| 인벤토리·상점 | **FishIcons_LowPoly**, **RestaurantIcons_LowPoly** | 어획물과 쌀·소금·간장 같은 식재료를 구분하는 아이콘입니다. |
| 다이브 HUD | **DOTween** | 원거리 무기 카드가 앞뒤로 바뀌는 전환과 산소 부족 경고의 점멸·페이드에 사용했습니다. |

#### 개발·협업 도구

- **Unity 에셋** — 기존 전기 뱀장어 모델과 FBX 클립을 가져와 Generic Rig와 반복 재생을 설정하고 머티리얼을 연결했습니다. 보트 머티리얼과 외곽선 셰이더를 수정했습니다.
- **AI 도구** — Codex를 활용해 Unity C# 구현과 오류 분석을 보조했습니다. 보스 에셋 작업에서는 Blender 리깅·애니메이션 수정과 FBX 내보내기·재임포트 검증에 활용했습니다.
- **Git · GitHub · GitHub Desktop** — 기능 브랜치와 Pull Request로 팀 변경 사항을 통합했습니다. 병합 충돌을 해결하고 시작 씬·빌드 진입 경로, TMP 폰트 아틀라스를 복구했습니다.

## 4. 이력서용 문구

> **Unity · C# | 게임플레이 시스템 구현 및 팀 시스템 연동**
>
> Collider 트리거와 인터페이스로 NPC·수중 오브젝트 상호작용을 구현하고, 선상 거래와 잠수 귀환 정산을 공용 인벤토리·퀘스트에 연결했습니다. 구매 후 아이템 지급이 실패하면 차감한 골드를 복구하도록 처리했습니다. 전기 뱀장어 보스전에서는 추상 클래스·이벤트·코루틴을 활용해 패턴 전환과 단계별 피격 판정을 구성했습니다. JSON 기반 5슬롯 저장·불러오기를 구현하면서 파일 백업과 야간 씬 이름 변경에 대한 기존 저장 호환을 처리했습니다.

## 5. 확인 범위

원본은 `CTeamProject`이며 Unity 제품 설정 이름은 `DaveTheDiver`입니다. 스크립트는 원본의 `Assets/02.Scripts/`에서 컴포넌트명으로 확인할 수 있습니다. 위 결과는 문서에 인용한 커밋의 코드와 씬 변경을 기준으로 설명합니다. 발표 시점에 후속으로 남은 범위는 유틸리티 사용 효과, 아이템별 무게 데이터, 일부 퀘스트 대상 배치와 퀘스트 보상 지급입니다.
