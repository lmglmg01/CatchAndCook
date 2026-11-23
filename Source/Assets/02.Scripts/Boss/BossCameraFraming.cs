using Unity.Cinemachine;
using UnityEngine;

// Keeps the player and the current attack readable while retaining the existing follow/aim camera.
[DefaultExecutionOrder(-100)]
public sealed class BossCameraFraming : MonoBehaviour
{
    [SerializeField] Hero _hero;
    [SerializeField] BossPatternController _patterns;
    [SerializeField] CameraController _zoom;
    [SerializeField] CinemachineFollow _follow;
    [SerializeField] Camera _camera;
    [SerializeField] SuctionBossPattern _suction;
    [SerializeField] RushBiteBossPattern _rush;
    [SerializeField] SweepBossPattern _sweep;
    [SerializeField] Transform _suctionMouth, _rushMouth;
    [SerializeField] Transform[] _tail;
    [Header("패턴별 기본 화각")]
    [SerializeField] float _suctionFov = 34f;
    [SerializeField] float _rushFov = 36f;
    [SerializeField] float _sweepFov = 39f;
    [SerializeField] float _tailFov = 34f;
    [SerializeField] float _maximumFov = 52f;
    Vector3 _initialOffset, _offsetVelocity;
    bool _initialized;

    void OnEnable()
    {
        if (_follow != null) { _initialOffset = _follow.FollowOffset; _initialized = true; }
        _offsetVelocity = Vector3.zero;
    }

    void Update()
    {
        if (!_hero || !_zoom || !_follow || !_camera || !_initialized) return;
        BossPattern active = _patterns != null ? _patterns.CurrentPattern : null;
        if (active == null || !active.IsActive)
            active = _suction != null && _suction.IsActive ? (BossPattern)_suction
                : _rush != null && _rush.IsActive ? _rush
                : _sweep != null && _sweep.IsActive ? _sweep : null;
        Vector3 desiredOffset = _initialOffset;
        if (active == null || !active.IsActive)
            _zoom.ClearBattleFraming();
        else
        {
            Vector3 hero = _hero.transform.position + Vector3.up * .7f;
            Vector3 focus; Vector2 radius; float baseFov;
            if (active == _suction && _suctionMouth != null)
            {
                focus = _suctionMouth.position;
                // Entry/exit remain offscreen; do not chase the hidden full-length body.
                focus.x = Mathf.Clamp(focus.x, -23f, 23f);
                focus.y = Mathf.Clamp(focus.y, -5f, 7f);
                radius = new Vector2(8f, 6f); baseFov = _suctionFov;
            }
            else if (active == _rush && _rushMouth != null)
            {
                // Reserve its impact footprint while the head is still in the background.
                focus = _rushMouth.position; radius = new Vector2(8f, 7f); baseFov = _rushFov;
            }
            else if (active == _sweep && _sweep.Phase == SweepBossPattern.PatternPhase.TailFlail && _tail != null && _tail.Length > 0)
            {
                focus = Vector3.zero; int count = 0;
                foreach (var bone in _tail) if (bone != null) { focus += bone.position; count++; }
                if (count > 0) focus /= count; else focus = hero;
                radius = new Vector2(7f, 7f); baseFov = _tailFov;
            }
            else
            {
                // Open the shot during the wind-up, before the S-shaped pass enters the arena.
                focus = new Vector3(0, 1, hero.z); radius = new Vector2(25f, 9f); baseFov = _sweepFov;
            }
            focus.z = hero.z;
            Vector3 min = Vector3.Min(hero - new Vector3(2, 2, 0), focus - new Vector3(radius.x, radius.y, 0));
            Vector3 max = Vector3.Max(hero + new Vector3(2, 2, 0), focus + new Vector3(radius.x, radius.y, 0));
            Vector3 center = (min + max) * .5f;
            desiredOffset.x = center.x - _hero.transform.position.x;
            desiredOffset.y = center.y - _hero.transform.position.y;
            float minimum = 0;
            Quaternion inverse = Quaternion.Inverse(_camera.transform.rotation);
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
            {
                Vector3 p = inverse * (new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, hero.z) - _camera.transform.position);
                float tangent = Mathf.Max(Mathf.Abs(p.y) / .72f, Mathf.Abs(p.x) / (Mathf.Max(.5f, _camera.aspect) * .82f)) / Mathf.Max(1, p.z);
                minimum = Mathf.Max(minimum, 2 * Mathf.Atan(tangent) * Mathf.Rad2Deg);
            }
            minimum = Mathf.Clamp(minimum, 31f, _maximumFov);
            _zoom.SetBattleFraming(Mathf.Min(_maximumFov, Mathf.Max(baseFov, minimum + 1.5f)), minimum);
        }
        _follow.FollowOffset = Vector3.SmoothDamp(_follow.FollowOffset, desiredOffset, ref _offsetVelocity, .3f, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    void OnDisable()
    {
        if (_zoom != null) _zoom.ClearBattleFraming();
        if (_follow != null && _initialized) _follow.FollowOffset = _initialOffset;
        _initialized = false;
    }
}
