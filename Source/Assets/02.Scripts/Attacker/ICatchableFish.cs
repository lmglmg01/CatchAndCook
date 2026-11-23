using UnityEngine;

/// <summary>Fish 담당자가 Fish 또는 물고기 루트의 컴포넌트에 구현할 획득 규칙.</summary>
public interface ICatchableFish
{
    ItemData FilletData { get; }
    int FilletAmount { get; }

    /// <summary>사망했고 아직 획득되지 않았으며 상호작용할 수 있는 상태.</summary>
    bool CanInteract { get; }

    /// <summary>살아 있고 작살에 포획된 상태. 사망하거나 포획을 해제하면 false.</summary>
    bool IsCaptured { get; }

    /// <summary>
    /// 데미지 적용 후 0 &lt; HP &lt;= 포획 기준인 경우에만 성공.
    /// 중복 포획을 거부하고 자체 이동과 공격을 중단한다.
    /// </summary>
    bool TryBeginCapture();

    /// <summary>포획 중 물고기 루트를 지정한 월드 위치로 이동한다.</summary>
    void MoveCaptured(Vector3 position);

    /// <summary>획득 없이 포획을 해제한다. 해제된 물고기는 현재 체력에 맞는 행동으로 복귀한다.</summary>
    void CancelCapture();

    /// <summary>
    /// 사망 또는 살아 있는 포획 상태에서만 한 번 성공한다.
    /// 획득 완료를 먼저 표시하고 물고기를 제거한다. 가방 지급은 Hero가 담당한다.
    /// 사망 시에도 이 호출 전까지 상호작용용 Collider를 유지해야 한다.
    /// </summary>
    bool TryCollect();
}
