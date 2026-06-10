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

        if (movementController.TryGetMotion(out Vector2 velocity, out _))
        {
            animatorDriver.SetMovement(velocity);
            return;
        }

        animatorDriver.SetIdle();
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
}
