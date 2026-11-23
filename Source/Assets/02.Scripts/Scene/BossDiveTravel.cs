using UnityEngine;
using Unity.Cinemachine;

// 보스 왕복은 같은 잠수의 연속이다. 정산·시간대 변경 없이 런타임 상태만 전달한다.
internal sealed class BossDiveTravel
{
    string _returnScene;
    string _pendingScene;
    Vector3 _doorPosition;
    Quaternion _doorRotation;
    bool _hasDoorPosition;
    bool _returning;
    InventorySaveData _bag;
    float _oxygen;
    int _bullets;

    internal string ReturnScene => string.IsNullOrEmpty(_returnScene) ? "Dive" : _returnScene;

    internal void EnterBoss(DivePlayScene scene, UnderSeaHero hero, string destination)
    {
        _returnScene = scene.gameObject.scene.path;
        _doorPosition = hero.transform.position;
        _doorRotation = hero.transform.rotation;
        _hasDoorPosition = true;
        _returning = false;
        Capture(hero);
        _pendingScene = destination;
    }

    internal void LeaveBoss(UnderSeaHero hero)
    {
        Capture(hero);
        _returning = true;
        _pendingScene = ReturnScene;
    }

    void Capture(UnderSeaHero hero)
    {
        _bag = hero.FishBag != null ? hero.FishBag.CaptureSave() : null;
        _oxygen = hero.GetComponent<HeroRunModel>().CurrentOxygen;
        BulletSpawner gun = hero.GetComponentInChildren<BulletSpawner>(true);
        _bullets = gun != null ? gun.CurrentSpawnCount : 0;
    }

    internal bool TryRestore(DivePlayScene scene, UnderSeaHero hero)
    {
        if (string.IsNullOrEmpty(_pendingScene)
            || (_pendingScene != scene.gameObject.scene.path && _pendingScene != scene.gameObject.scene.name))
            return false;
        _pendingScene = null;

        if (_bag != null && hero.FishBag != null)
        {
            if (hero.FishBag.CanRestoreSave(_bag)) hero.FishBag.RestoreSave(_bag);
            else Debug.LogError("보스 왕복 중 잠수 가방의 아이템 맵을 복원할 수 없습니다.", scene);
        }
        HeroRunModel model = hero.GetComponent<HeroRunModel>();
        model.ConsumeOxygen(Mathf.Max(0f, model.CurrentOxygen - _oxygen));
        BulletSpawner gun = hero.GetComponentInChildren<BulletSpawner>(true);
        if (gun != null) gun.RestoreBulletCount(_bullets);

        if (_returning)
        {
            Vector3 position = _doorPosition;
            Quaternion rotation = _doorRotation;
            if (!_hasDoorPosition)
            {
                // 보스 씬을 단독 실행했을 때도 수면 대신 석문 입구로 복귀한다.
                StoneDoorInteractable gate = Object.FindFirstObjectByType<StoneDoorInteractable>();
                if (gate != null)
                {
                    position = gate.GetComponent<BoxCollider>().bounds.center;
                    position.z = hero.transform.position.z;
                }
                else position = hero.transform.position;
                rotation = hero.transform.rotation;
            }
            Vector3 delta = position - hero.transform.position;
            hero.transform.SetPositionAndRotation(position, rotation);
            Rigidbody body = hero.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            }
            foreach (CinemachineCamera camera in Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                if (camera.gameObject.scene == scene.gameObject.scene)
                {
                    camera.OnTargetObjectWarped(hero.transform, delta);
                    camera.PreviousStateIsValid = false;
                }
            Physics.SyncTransforms();
            _returning = false;
        }
        return true;
    }

    internal void Clear()
    {
        _returnScene = _pendingScene = null;
        _hasDoorPosition = _returning = false;
        _bag = null;
    }
}
