using System;
using UnityEngine;

public abstract class Hero : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] Mover _mover;

    [Header("애니메이션")]
    [SerializeField] AnimatorHandler _animatorHandler;

    public event Action<float> MoveSpeedChanged;

    protected AnimatorHandler HeroAnimator => _animatorHandler;
    protected virtual bool HorizontalOnly => false;

    protected virtual void Awake()
    {
        if (_mover == null) _mover = GetComponent<Mover>();
        if (_animatorHandler == null)
            _animatorHandler = GetComponentInChildren<AnimatorHandler>(true);
    }

    protected virtual void OnEnable()
    {
        if (_animatorHandler != null)
            MoveSpeedChanged += _animatorHandler.SetMoveSpeed;
    }

    protected virtual void OnDisable()
    {
        MoveHero(Vector2.zero, 0f);
        if (_animatorHandler != null)
            MoveSpeedChanged -= _animatorHandler.SetMoveSpeed;
    }

    protected void MoveHero(Vector2 direction, float speed)
    {
        if (_mover != null)
        {
            if (HorizontalOnly) _mover.MoveHorizontal(direction.x, speed);
            else _mover.Move(direction, speed);
        }
        MoveSpeedChanged?.Invoke(direction.magnitude * Mathf.Max(0f, speed));
    }

    public abstract void SetMoveInput(Vector2 direction);

    public virtual void ResetInput()
    {
        SetMoveInput(Vector2.zero);
    }
}
