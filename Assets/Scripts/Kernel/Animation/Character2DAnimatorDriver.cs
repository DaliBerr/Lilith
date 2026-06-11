using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Character2DAnimatorDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string moveXParameter = "MoveX";
    [SerializeField] private string moveYParameter = "MoveY";
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string isFacingBackParameter = "IsFacingBack";
    [SerializeField] private string dodgeParameter = "Dodge";
    [SerializeField, Min(0f)] private float movementThreshold = 0.0001f;
    [SerializeField] private Vector2 defaultFacing = Vector2.down;
    [SerializeField] private bool flipHorizontalWhenFacingLeft = true;
    [SerializeField, Min(0f)] private float horizontalFlipThreshold = 0.0001f;

    private readonly HashSet<int> availableParameters = new();
    private Vector2 facingDirection = Vector2.down;
    private bool wasDashing;

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
        horizontalFlipThreshold = Mathf.Max(0f, horizontalFlipThreshold);
        defaultFacing = NormalizeOrDefault(defaultFacing, Vector2.down);
        ResolveReferences();
    }

    public void SetMovement(Vector2 movement)
    {
        SetMovement(movement, movement);
    }

    public void SetMovement(Vector2 movement, Vector2 facing)
    {
        Movement = movement;
        float speed = movement.magnitude;
        bool moving = movement.sqrMagnitude > movementThreshold * movementThreshold;
        facingDirection = NormalizeOrDefault(facing, moving ? movement / speed : facingDirection);

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

    public void SetDashing(bool isDashing)
    {
        if (isDashing && !wasDashing)
        {
            SetTrigger(dodgeParameter);
        }

        wasDashing = isDashing;
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

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
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
        SetBool(isFacingBackParameter, ShouldFaceBack(facing));
        ApplySpriteFlip(facing);
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

    private void SetTrigger(string parameterName)
    {
        int hash = Animator.StringToHash(parameterName);
        if (availableParameters.Contains(hash))
        {
            animator.SetTrigger(hash);
        }
    }

    private void ApplySpriteFlip(Vector2 facing)
    {
        if (spriteRenderer == null || Mathf.Abs(facing.x) <= horizontalFlipThreshold)
        {
            return;
        }

        spriteRenderer.flipX = ShouldFlipHorizontally(facing);
    }

    private bool ShouldFlipHorizontally(Vector2 facing)
    {
        return flipHorizontalWhenFacingLeft ? facing.x < 0f : facing.x > 0f;
    }

    private static bool ShouldFaceBack(Vector2 facing)
    {
        return facing.y > 0f;
    }

    private static Vector2 NormalizeOrDefault(Vector2 value, Vector2 fallback)
    {
        return value.sqrMagnitude > 0.000001f ? value.normalized : fallback.normalized;
    }
}
