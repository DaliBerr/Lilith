using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Character2DAnimatorDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveYParameter = "MoveY";
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField, Min(0f)] private float movementThreshold = 0.0001f;
    [SerializeField] private Vector2 defaultFacing = Vector2.down;

    private readonly HashSet<int> availableParameters = new();
    private Vector2 facingDirection = Vector2.down;

    public Vector2 FacingDirection => facingDirection;

    public Vector2 Movement { get; private set; }

    public bool IsMoving { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        facingDirection = NormalizeOrDefault(defaultFacing, Vector2.down);
        CacheParameters();
        ApplyAnimatorParameters(facingDirection, false, 0f);
    }

    private void OnValidate()
    {
        movementThreshold = Mathf.Max(0f, movementThreshold);
        defaultFacing = NormalizeOrDefault(defaultFacing, Vector2.down);
        ResolveReferences();
    }

    public void SetMovement(Vector2 movement)
    {
        Movement = movement;
        float speed = movement.magnitude;
        bool moving = movement.sqrMagnitude > movementThreshold * movementThreshold;
        if (moving)
        {
            facingDirection = movement / speed;
        }

        IsMoving = moving;
        ApplyAnimatorParameters(facingDirection, moving, moving ? speed : 0f);
    }

    public void SetFacing(Vector2 facing)
    {
        facingDirection = NormalizeOrDefault(facing, facingDirection);
        Movement = Vector2.zero;
        IsMoving = false;
        ApplyAnimatorParameters(facingDirection, false, 0f);
    }

    public void SetIdle()
    {
        Movement = Vector2.zero;
        IsMoving = false;
        ApplyAnimatorParameters(facingDirection, false, 0f);
    }

    public void RefreshAnimatorParameters()
    {
        CacheParameters();
        ApplyAnimatorParameters(facingDirection, IsMoving, IsMoving ? Movement.magnitude : 0f);
    }

    private void ResolveReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void CacheParameters()
    {
        availableParameters.Clear();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            availableParameters.Add(parameters[i].nameHash);
        }
    }

    private void ApplyAnimatorParameters(Vector2 facing, bool isMoving, float speed)
    {
        if (animator == null)
        {
            return;
        }

        SetFloat(moveXParameter, facing.x);
        SetFloat(moveYParameter, facing.y);
        SetFloat(speedParameter, speed);
        SetBool(isMovingParameter, isMoving);
    }

    private void SetFloat(string parameterName, float value)
    {
        int hash = Animator.StringToHash(parameterName);
        if (availableParameters.Contains(hash))
        {
            animator.SetFloat(hash, value);
        }
    }

    private void SetBool(string parameterName, bool value)
    {
        int hash = Animator.StringToHash(parameterName);
        if (availableParameters.Contains(hash))
        {
            animator.SetBool(hash, value);
        }
    }

    private static Vector2 NormalizeOrDefault(Vector2 value, Vector2 fallback)
    {
        return value.sqrMagnitude > 0.000001f ? value.normalized : fallback.normalized;
    }
}
