using System.Collections;
using UnityEngine;

public class SushiInteractableObject : InteractObject
{
    [Header("점프할 캐릭터")]
    [SerializeField] GroundHero _groundHero;
    [SerializeField] BoatNPC _boatNPC;
    [SerializeField] AnimatorHandler _heroAnimatorHandler;
    [SerializeField] AnimatorHandler _npcAnimatorHandler;

    [Header("착지 위치")]
    [SerializeField] Transform _heroMovePoint;
    [SerializeField] Transform _npcMovePoint;

    [Header("탑승 후 보트 이동")]
    [SerializeField] BoatMover _boatMover;

    [Header("점프 연출")]
    [SerializeField, Min(0f)] float _takeoffDelay;
    [SerializeField, Min(0.01f)] float _jumpDuration;
    [SerializeField, Min(0f)] float _jumpHeight;
    [SerializeField, Min(0f)] float _landingDuration;

    Coroutine _jumpRoutine;
    JumpActor _heroJump;
    JumpActor _npcJump;

    public BoatMover BoatMover => _boatMover;

    public bool IsJumping => _jumpRoutine != null;

    public override bool CanInteract(Hero interactor)
    {
        return base.CanInteract(interactor) && !IsJumping && interactor == _groundHero
            && _boatNPC != null && _boatNPC.isActiveAndEnabled
            && _heroAnimatorHandler != null && _npcAnimatorHandler != null
            && _heroMovePoint != null && _npcMovePoint != null
            && _groundHero.TryGetComponent<Rigidbody>(out _)
            && _boatNPC.TryGetComponent<Rigidbody>(out _);
    }

    protected override bool ApplyInteraction(Hero interactor)
    {
        _heroJump = new JumpActor(_groundHero, _heroAnimatorHandler, _heroMovePoint);
        _npcJump = new JumpActor(_boatNPC, _npcAnimatorHandler, _npcMovePoint);
        _jumpRoutine = StartCoroutine(JumpTogether());
        return true;
    }

    IEnumerator JumpTogether()
    {
        // 일반 이동과 Root Motion이 점프 경로를 덮어쓰지 않도록 잠시 멈춘다.
        _groundHero.ResetInput();
        _heroJump.Begin();
        _npcJump.Begin();
        _heroAnimatorHandler.SetBoatJump();
        _npcAnimatorHandler.SetBoatJump();

        if (_takeoffDelay > 0f) yield return new WaitForSeconds(_takeoffDelay);
        var step = new WaitForFixedUpdate();
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _jumpDuration);
        while (elapsed < duration)
        {
            yield return step;
            elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float height = 4f * Mathf.Max(0f, _jumpHeight) * progress * (1f - progress);
            _heroJump.Move(progress, height);
            _npcJump.Move(progress, height);
        }

        _heroJump.Land();
        _npcJump.Land();
        elapsed = 0f;
        while (elapsed < _landingDuration)
        {
            yield return step;
            elapsed += Time.fixedDeltaTime;
            _heroJump.FollowDeck();
            _npcJump.FollowDeck();
        }
        if (_boatMover != null && _boatMover.MoveToPoint())
        {
            while (_boatMover != null && _boatMover.IsMoving)
            {
                yield return step;
                _heroJump.FollowDeck();
                _npcJump.FollowDeck();
            }
            // 마지막 Rigidbody 이동이 물리에 반영된 뒤 탑승 상태를 복원한다.
            yield return step;
        }
        RestoreActors(false);
        _jumpRoutine = null;
    }

    protected override void OnDisable()
    {
        if (_jumpRoutine != null && _boatMover != null) _boatMover.StopMovement();
        if (_jumpRoutine != null) StopCoroutine(_jumpRoutine);
        _jumpRoutine = null;
        RestoreActors(true);
        base.OnDisable();
    }

    void RestoreActors(bool cancelled)
    {
        _heroJump?.Restore(cancelled);
        _npcJump?.Restore(cancelled);
        _heroJump = null;
        _npcJump = null;
    }

    // 두 캐릭터에 같은 이동/복원 규칙을 적용하며 게임 데이터는 변경하지 않는다.
    sealed class JumpActor
    {
        readonly Behaviour _controller;
        readonly Transform _root;
        readonly Rigidbody _body;
        readonly Mover _mover;
        readonly Animator _animator;
        readonly Vector3 _start;
        readonly Vector3 _destination;
        readonly Transform _landingPoint;
        readonly Quaternion _modelRotation;
        readonly Quaternion _landingRotation;
        readonly bool _controllerEnabled;
        readonly bool _moverEnabled;
        readonly bool _rootMotion;
        readonly bool _kinematic;
        readonly bool _detectCollisions;
        readonly RigidbodyConstraints _constraints;
        bool _landed;

        public JumpActor(Behaviour controller, AnimatorHandler handler, Transform destination)
        {
            _controller = controller;
            _root = controller.transform;
            _body = controller.GetComponent<Rigidbody>();
            _mover = controller.GetComponent<Mover>();
            _animator = handler.GetComponent<Animator>();
            _start = _root.position;
            _destination = destination.position;
            _landingPoint = destination;
            _modelRotation = _animator != null ? _animator.transform.rotation : Quaternion.identity;
            _landingRotation = destination.rotation;
            _controllerEnabled = controller.enabled;
            _moverEnabled = _mover != null && _mover.enabled;
            _rootMotion = _animator != null && _animator.applyRootMotion;
            if (_body == null) return;
            _kinematic = _body.isKinematic;
            _detectCollisions = _body.detectCollisions;
            _constraints = _body.constraints;
        }

        public void Begin()
        {
            _controller.enabled = false;
            if (_mover != null) _mover.enabled = false;
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                Vector3 direction = Vector3.ProjectOnPlane(_destination - _start, Vector3.up);
                if (direction.sqrMagnitude > 0.001f)
                    _animator.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            if (_body == null) return;
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.isKinematic = true;
            _body.detectCollisions = false;
            _body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        public void Move(float progress, float height)
        {
            if (_root == null) return;
            Vector3 position = Vector3.Lerp(_start, _destination, progress) + Vector3.up * height;
            if (_body != null) _body.MovePosition(position);
        }

        public void Land()
        {
            if (_root == null) return;
            if (_body != null) _body.position = _landingPoint != null ? _landingPoint.position : _destination;
            _landed = true;
        }

        public void FollowDeck()
        {
            if (_root == null || _landingPoint == null) return;
            if (_body != null) _body.MovePosition(_landingPoint.position);
            if (_animator != null) _animator.transform.rotation = _landingPoint.rotation;
        }

        public void Restore(bool cancelled)
        {
            if (_root == null) return;
            // 중간에 취소되면 공중에 남기지 않고 출발 지점으로 되돌린다.
            Vector3 position = cancelled && !_landed ? _start
                : _landingPoint != null ? _landingPoint.position : _destination;
            if (_body != null)
            {
                _body.position = position;
                _body.constraints = _constraints;
                _body.isKinematic = _kinematic;
                _body.detectCollisions = _detectCollisions;
                if (!_kinematic)
                {
                    _body.linearVelocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                }
            }
            if (_animator != null)
            {
                _animator.transform.rotation = cancelled && !_landed ? _modelRotation
                    : _landingPoint != null ? _landingPoint.rotation : _landingRotation;
                _animator.applyRootMotion = _rootMotion;
            }
            if (_mover != null)
            {
                _mover.SetMovementPlaneZ(position.z);
                _mover.enabled = _moverEnabled;
            }
            if (_controller != null) _controller.enabled = _controllerEnabled;
        }
    }
}
