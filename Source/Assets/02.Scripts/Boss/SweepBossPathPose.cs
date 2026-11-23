using System.Collections.Generic;
using UnityEngine;

// Poses the existing skin bones along the head's travelled world-space path.
// Imported bind poses and weights are preserved. Sweep bones are placed on the
// travelling S curve instead of approximating it with small joint rotations.
[DefaultExecutionOrder(200)]
public class SweepBossPathPose : MonoBehaviour
{
    [SerializeField] Transform[] _bones;
    [SerializeField] Transform _visualRoot;
    [Header("지면에 고정된 꼬리 좌우 흔들기") ]
    [SerializeField, Min(0f)] float _tailSwingAmplitude = 3f;
    [SerializeField, Min(0f)] float _tailSwingFrequency = .8f;
    readonly List<Vector3> _trail = new List<Vector3>(512);
    Quaternion[] _referenceRotations, _animatedLocal;
    Vector3[] _referenceDirections;
    float[] _distances;
    Vector3[] _poseDirections;
    Vector3[] _animatedPositions, _tailPoints;
    Vector3[] _restLocalPositions, _wavePositions;
    float _wavePlacement;
    [SerializeField, Range(8f, 30f)] float _maxJointBend = 20f;
    [SerializeField, Range(.01f, .2f)] float _poseResponseTime = .06f;
    Vector3 _visualLocal, _headTarget;
    Vector3 _previousHead, _renderHead;
    float _headSampleTime, _renderTrailOffset;
    float _length, _combatZ, _swing, _swingTarget, _swingClock, _floorY;
    bool _poseInitialized;
    Vector3 _headReferenceDirection;
    float _tailLift, _tailLiftTarget, _tailBendSign;
    [SerializeField, Range(0f, 75f)] float _tailPresentationAngle = 65f;
    bool _running, _applied;
    bool _spatialEntry;
    float[] _yaw;
    float _headYaw;
    public Vector3 HeadTarget => _headTarget;
    public float BodyLength => _length;

    public bool Begin(Vector3 forward, float combatZ, float trailWaveAmplitude = 0f, float trailWaveLength = 30f)
    {
        if (_visualRoot == null || _bones == null || _bones.Length < 3) return false;
        foreach (var b in _bones) if (b == null) return false;
        StopFollowing();
        int n = _bones.Length;
        _spatialEntry = false; _yaw = new float[n]; _headYaw = 0f;
        _referenceRotations = new Quaternion[n]; _animatedLocal = new Quaternion[n];
        _animatedPositions = new Vector3[n]; _tailPoints = new Vector3[n];
        _restLocalPositions = new Vector3[n]; _wavePositions = new Vector3[n]; _wavePlacement = 1f;
        _referenceDirections = new Vector3[n]; _distances = new float[n];
        var bindFrames = new Dictionary<Transform, Matrix4x4>();
        foreach (var skin in _visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh == null) continue;
            var binds = skin.sharedMesh.bindposes; var skinBones = skin.bones;
            for (int j = 0; j < Mathf.Min(binds.Length, skinBones.Length); j++)
                if (skinBones[j] != null) bindFrames[skinBones[j]] = skin.transform.localToWorldMatrix * binds[j].inverse;
        }
        _length = 0f;
        for (int i = 0; i < n; i++)
        {
            Matrix4x4 bind;
            _referenceRotations[i] = bindFrames.TryGetValue(_bones[i], out bind) ? bind.rotation : _bones[i].rotation;
            Matrix4x4 parentBind;
            _restLocalPositions[i] = bindFrames.ContainsKey(_bones[i]) && bindFrames.TryGetValue(_bones[i].parent, out parentBind)
                ? parentBind.inverse.MultiplyPoint3x4(bind.GetColumn(3)) : _bones[i].localPosition;
            Matrix4x4 nextBind;
            _referenceDirections[i] = i + 1 < n && bindFrames.TryGetValue(_bones[i+1], out nextBind)
                ? ((Vector3)nextBind.GetColumn(3) - (Vector3)bind.GetColumn(3)).normalized
                : (i + 1 < n ? (_bones[i+1].position-_bones[i].position).normalized : _referenceRotations[i] * Vector3.up);
            if (i > 0) _length += Vector3.Distance(_bones[i - 1].position, _bones[i].position);
            _distances[i] = _length;
        }
        _headReferenceDirection = Vector3.zero;
        for (int i=0;i<Mathf.Min(3,n-1);i++) _headReferenceDirection += _referenceDirections[i] * (_distances[i+1]-_distances[i]);
        _headReferenceDirection.Normalize();
        _poseDirections = new Vector3[n]; _poseInitialized = false;
        _headTarget = _bones[0].position;
        _previousHead = _renderHead = _headTarget;
        _headSampleTime = Time.fixedTime;
        _renderTrailOffset = 0f;
        _visualLocal = _visualRoot.localPosition; _combatZ = combatZ;
        _swing = _swingTarget = _swingClock = 0f;
        _tailLift = _tailLiftTarget = 0f; _tailBendSign = -Mathf.Sign(forward.x);
        _trail.Clear();
        for (float d = _length + 15f; d > 0f; d -= .5f)
        {
            Vector3 point = _headTarget - forward.normalized * d;
            point.y += trailWaveAmplitude * Mathf.Sin(-d * Mathf.PI * 2f / Mathf.Max(1f, trailWaveLength));
            _trail.Add(point);
        }
        _trail.Add(_headTarget); _running = true;
        return true;
    }

    public void SetHead(Vector3 position)
    {
        if (!_running) return;
        Vector3 previous = _trail[_trail.Count - 1];
        float distance = Vector3.Distance(previous, position);
        if (distance > .00001f)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .15f));
            for (int i = 1; i <= steps; i++) _trail.Add(Vector3.Lerp(previous, position, i / (float)steps));
            // Keep at least twice the body's travelled length, independent of frame rate.
            float retained = 0f;
            for (int i = _trail.Count - 1; i > 0; i--)
            {
                retained += Vector3.Distance(_trail[i], _trail[i - 1]);
                if (retained > _length * 2f + 20f) { if (i > 1) _trail.RemoveRange(0, i - 1); break; }
            }
        }
        _previousHead = _headTarget;
        _headTarget = position;
        _headSampleTime = Time.fixedTime;
    }

    public void SetBurialSurface(float floorY) { _floorY = floorY; }
    public void BeginHorizontalEntry() { _spatialEntry = true; }

    public float BuriedFraction(float floorY)
    {
        if (!_running || _length <= 0f) return 0f;
        float buried = 0f;
        for (int i = 1; i < _bones.Length; i++)
        {
            float a = _bones[i-1].position.y, b = _bones[i].position.y;
            float length = _distances[i] - _distances[i-1];
            if (a <= floorY && b <= floorY) buried += length;
            else if ((a < floorY) != (b < floorY))
            {
                float crossing = Mathf.Clamp01((floorY-a)/(b-a));
                buried += length * (a < floorY ? crossing : 1-crossing);
            }
        }
        return Mathf.Clamp01(buried / _length);
    }


    // 최종 포즈의 몸통 중심선이 매몰 경계를 통과하는 실제 월드 위치.
    public bool TryGetBurialContact(float floorY, out Vector3 point)
    {
        point = Vector3.zero;
        if (!_running || _bones == null) return false;
        for (int i = 1; i < _bones.Length; i++)
        {
            if (_bones[i - 1] == null || _bones[i] == null) continue;
            Vector3 a = _bones[i - 1].position, b = _bones[i].position;
            if ((a.y < floorY && b.y < floorY) || (a.y > floorY && b.y > floorY)) continue;
            float height = b.y - a.y;
            if (Mathf.Abs(height) < .0001f) continue;
            point = Vector3.Lerp(a, b, Mathf.Clamp01((floorY - a.y) / height));
            point.y = floorY;
            return true;
        }
        return false;
    }

    public void SetTailPresentation(float amount) { _tailLiftTarget = Mathf.Clamp01(amount); }

    public void SetTailSwing(bool active)
    {
        if (active && _swingTarget == 0f) _swingClock = 0f;
        _swingTarget = active ? 1f : 0f;
    }

    Vector3 ReadTrail(float distance)
    {
        distance += _renderTrailOffset;
        Vector3 newer = _headTarget;
        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            Vector3 older = _trail[i]; float segment = Vector3.Distance(newer, older);
            if (segment > .0001f && distance <= segment) return Vector3.Lerp(newer, older, distance / segment);
            distance -= segment; newer = older;
        }
        return newer;
    }

    void RestorePose()
    {
        if (!_applied) return;
        for (int i = 0; i < _bones.Length; i++) if (_bones[i])
        {
            _bones[i].localRotation = _animatedLocal[i];
            _bones[i].localPosition = _animatedPositions[i];
        }
        if (_visualRoot) _visualRoot.localPosition = _visualLocal;
        _applied = false;
    }
    void Update() { RestorePose(); }
    static Quaternion PlanarRotation(Vector3 from, Vector3 to)
    {
        from.z = to.z = 0f;
        return Quaternion.AngleAxis(Vector3.SignedAngle(from, to, Vector3.forward), Vector3.forward);
    }
    Quaternion PathRotation(Vector3 from, Vector3 to, ref float yaw)
    {
        if (!_spatialEntry) return PlanarRotation(from, to);
        Vector3 a = Vector3.ProjectOnPlane(from, Vector3.up);
        Vector3 b = Vector3.ProjectOnPlane(to, Vector3.up);
        // Yaw is intentional only during entry. Keep its last value near vertical
        // to avoid an undefined heading and a sudden 180-degree roll underground.
        if (_swingTarget == 0f && _swing < .001f && a.sqrMagnitude > .0001f && b.sqrMagnitude > .0025f)
            yaw = Vector3.SignedAngle(a, b, Vector3.up);
        Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up);
        return Quaternion.FromToRotation(turn * from, to) * turn;
    }
    void LateUpdate()
    {
        if (!_running) return;
        // Physics supplies fixed-step targets; skinning uses the same delayed pose at
        // every render frame, so high-refresh displays never hold then jump at 50 Hz.
        float alpha = Mathf.Clamp01((Time.time - _headSampleTime) / Time.fixedDeltaTime);
        _renderHead = Vector3.Lerp(_previousHead, _headTarget, alpha);
        _renderTrailOffset = Vector3.Distance(_renderHead, _headTarget);
        // Ease the tail stroke in and out without dragging the endpoint to a fixed IK target.
        _swing = Mathf.MoveTowards(_swing, _swingTarget, Time.deltaTime / .8f);
        _swingClock += Time.deltaTime;
        _tailLift = Mathf.MoveTowards(_tailLift, _tailLiftTarget, Time.deltaTime / .65f);
        float swingWeight = Mathf.SmoothStep(0f, 1f, _swing);
        _visualLocal = _visualRoot.localPosition;
        for (int i = 0; i < _bones.Length; i++)
        {
            _animatedLocal[i] = _bones[i].localRotation;
            _animatedPositions[i] = _bones[i].localPosition;
        }
        // Glide clips now contain a complete positional wave for standalone preview.
        // The gameplay path supplies that same motion here; do not add it twice.
        for (int i = 0; i < _bones.Length; i++) _bones[i].localPosition = _restLocalPositions[i];
        _visualRoot.position += _renderHead - _bones[0].position;
        float tailStart = _length * .72f;
        float tailAngle = Mathf.Atan2(_tailSwingAmplitude, (_length-tailStart)*.65f) * Mathf.Rad2Deg;
        Quaternion previousDeformation = Quaternion.identity;
        for (int i = 0; i < _bones.Length; i++)
        {
            Vector3 direction;
            if (i + 1 < _bones.Length)
            {
                float middle = (_distances[i] + _distances[i+1]) * .5f;
                float halfSpan = (_distances[i+1] - _distances[i]) * .75f;
                // A tangent follows the curve without trying to correct chord-length error
                // at every joint, which previously produced alternating sharp kinks.
                direction = ReadTrail(middle + halfSpan) - ReadTrail(Mathf.Max(0f, middle - halfSpan));
                if (!_spatialEntry) direction.z = 0f;
                if (direction.sqrMagnitude < .00001f) direction = i > 0 ? _poseDirections[i-1] : _referenceDirections[i];
                direction.Normalize();
                if (i == 0)
                {
                    Vector3 headChord = ReadTrail(_distances[Mathf.Min(3,_bones.Length-1)]) - _renderHead;
                    if (!_spatialEntry) headChord.z = 0f;
                    direction = PathRotation(_headReferenceDirection, headChord.normalized, ref _headYaw) * _referenceDirections[0];
                }
                float tail = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tailStart, _length, middle));
                float wave = Mathf.Sin(_swingClock * Mathf.PI * 2f * _tailSwingFrequency - tail * .65f);
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_length*.65f, _length, middle));
                // Only the exposed chain swings. The buried chain stays on its entry path.
                // A vertical tail rotated around camera-forward produces a left/right stroke.
                float exposed = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_floorY, _floorY + 3f, ReadTrail(middle).y));
                float bend = Mathf.SmoothStep(0f, 1f, _tailLift) * lift * _tailPresentationAngle
                    + swingWeight * exposed * tail * tailAngle * wave;
                direction = Quaternion.AngleAxis(_tailBendSign * bend, Vector3.forward) * direction;
                if (_poseInitialized)
                    direction = Vector3.Slerp(_poseDirections[i], direction,
                        1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.01f, _poseResponseTime))).normalized;
                if (i > 0)
                    direction = Vector3.RotateTowards(_poseDirections[i-1], direction, _maxJointBend*Mathf.Deg2Rad, 0f).normalized;
            }
            else direction = _poseDirections[i-1];
            // Keep the jaw/neck region moving as one rigid head. Bending across its mixed
            // jaw/body weights stretched the collar and mouth even with modest spine angles.
            if (i == 1 || i == 2)
                direction = PathRotation(_referenceDirections[0], _poseDirections[0], ref _yaw[0]) * _referenceDirections[i];
            _poseDirections[i] = direction;
            Quaternion deformation = PathRotation(_referenceDirections[i], direction, ref _yaw[i]);
            if (_spatialEntry && i >= 3)
            {
                // Carry the preceding bone's cross-section along the curve with
                // only the bend needed to align the next tangent. Independent yaw
                // solutions twisted adjacent skin matrices and pinched the tail.
                // Bind rotations retain the model's authored roll and bone lengths.
                deformation = Quaternion.FromToRotation(previousDeformation * _referenceDirections[i], direction)
                    * previousDeformation;
            }
            _bones[i].rotation = deformation * _referenceRotations[i];
            previousDeformation = deformation;
        }
        PlaceSweepOnWave();
        BendTailToCombatPlane();
        _poseInitialized = true;
        _applied = true;
    }

    void PlaceSweepOnWave()
    {
        _wavePlacement = Mathf.MoveTowards(_wavePlacement, _spatialEntry ? 0f : 1f, Time.deltaTime / .35f);
        if (_wavePlacement <= 0f) return;
        // Keep the jaw/head as one piece and ease its neck into the same S curve.
        int neck = Mathf.Min(2, _bones.Length - 1);
        Vector3 neckOffset = _bones[neck].position - ReadTrail(_distances[neck]);
        for (int i = 0; i < _bones.Length; i++)
        {
            _wavePositions[i] = _bones[i].position;
            if (i <= neck) continue;
            float blend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_distances[neck], _distances[Mathf.Min(6, _bones.Length - 1)], _distances[i]));
            _wavePositions[i] = Vector3.Lerp(_wavePositions[i], ReadTrail(_distances[i]) + neckOffset * blend, _wavePlacement);
        }
        for (int i = neck + 1; i < _bones.Length; i++)
        {
            Vector3 direction = i + 1 < _bones.Length ? _wavePositions[i + 1] - _wavePositions[i] : _wavePositions[i] - _wavePositions[i - 1];
            Quaternion rotation = Quaternion.FromToRotation(_referenceDirections[i], direction.normalized) * _referenceRotations[i];
            _bones[i].SetPositionAndRotation(_wavePositions[i], Quaternion.Slerp(_bones[i].rotation, rotation, _wavePlacement));
        }
    }

    void BendTailToCombatPlane()
    {
        if (!_spatialEntry || _tailLift <= 0f) return;
        int last = _bones.Length - 1;
        int start = Mathf.Max(3, last - 5);
        float offset = (_combatZ - _bones[last].position.z) * _tailLift;
        // Only the final five tail links reach forward. Head, body, burial point,
        // visual root and the tail attachment position remain on the original path.
        for (int i = start; i <= last; i++)
        {
            float u = (i - start) / (float)(last - start);
            float weight = u*u*u*(u*(6f*u-15f)+10f);
            _tailPoints[i] = _bones[i].position + Vector3.forward * (offset * weight);
        }
        Quaternion previous = _bones[start-1].rotation * Quaternion.Inverse(_referenceRotations[start-1]);
        for (int i = start; i <= last; i++)
        {
            _bones[i].position = _tailPoints[i];
            Vector3 tangent = i < last ? (_tailPoints[i+1] - _tailPoints[i]).normalized
                : (_tailPoints[i] - _tailPoints[i-1]).normalized;
            Quaternion deformation = Quaternion.FromToRotation(previous * _referenceDirections[i], tangent) * previous;
            _bones[i].rotation = deformation * _referenceRotations[i];
            previous = deformation;
        }
    }

    public void StopFollowing() { RestorePose(); _running = false; _trail.Clear(); }
    void OnDisable() { StopFollowing(); }
}
