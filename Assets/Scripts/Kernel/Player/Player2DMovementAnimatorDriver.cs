using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player2DMovementController))]
[RequireComponent(typeof(Character2DAnimatorDriver))]
public sealed class Player2DMovementAnimatorDriver : MonoBehaviour
{
    [SerializeField] private Player2DMovementController movementController;
    [SerializeField] private Character2DAnimatorDriver animatorDriver;

    private void Awake()
    {
        if (ResolveReferences())
        {
            animatorDriver.RefreshAnimatorParameters();
        }
    }

    private void Update()
    {
        if (!ResolveReferences())
        {
            return;
        }

        bool hasMotion = movementController.TryGetMotion(out Vector2 velocity, out bool isDashing);
        Vector2 facing = ResolveFacingDirection(hasMotion ? velocity : Vector2.zero);
        animatorDriver.SetDashing(isDashing);

        if (hasMotion)
        {
            animatorDriver.SetMovement(velocity, facing);
            return;
        }

        animatorDriver.SetFacing(facing);
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private bool ResolveReferences()
    {
        if (movementController == null)
        {
            movementController = GetComponent<Player2DMovementController>();
        }

        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<Character2DAnimatorDriver>();
        }

        return movementController != null && animatorDriver != null;
    }

    private Vector2 ResolveFacingDirection(Vector2 fallback)
    {
        if (movementController != null && movementController.TryGetMouseFacingDirection(out Vector2 mouseFacing))
        {
            return mouseFacing;
        }

        if (fallback.sqrMagnitude > 0.000001f)
        {
            return fallback;
        }

        return animatorDriver != null ? animatorDriver.FacingDirection : Vector2.down;
    }
}
