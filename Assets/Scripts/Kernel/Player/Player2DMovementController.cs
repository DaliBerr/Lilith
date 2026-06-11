using Kernel.GameState;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the debug 2D player on the generated isometric Tilemap room.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class Player2DMovementController : MonoBehaviour
{
    private const float MinimumDashDuration = 0.01f;
    private const float MinimumFacingSqrMagnitude = 0.0001f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform facingPivot;
    [SerializeField] private float facingAngleOffsetDegrees = -90f;
    [SerializeField] private bool rotateTowardsMouse;

    [Header("Dash")]
    [SerializeField, Min(0f)] private float dashDistance = 2.2f;
    [SerializeField, Min(MinimumDashDuration)] private float dashDuration = 0.16f;
    [SerializeField, Min(0f)] private float dashStaminaCost = 25f;
    [SerializeField, Min(0f)] private float staminaMax = 100f;
    [SerializeField, Min(0f)] private float staminaRegenPerSecond = 20f;
    [SerializeField, Min(0f)] private float staminaRegenDelay = 0.35f;

    private Vector2 lastMoveDirection = Vector2.up;
    private Vector2 dashDirection = Vector2.up;
    private Vector2 lastAppliedVelocity;
    private float currentStamina;
    private float staminaRegenResumeTime;
    private float dashRemainingTime;
    private bool wasDashingLastStep;

    public Rigidbody2D Body => body;
    public Collider2D BodyCollider => bodyCollider;

    public bool TryGetMotion(out Vector2 velocity, out bool isDashing)
    {
        velocity = lastAppliedVelocity;
        isDashing = wasDashingLastStep || dashRemainingTime > 0f;
        return isDashing || velocity.sqrMagnitude > MinimumFacingSqrMagnitude;
    }

    public bool TryGetMouseFacingDirection(out Vector2 direction)
    {
        direction = Vector2.zero;
        if (Mouse.current == null || !TryGetTargetCamera(out Camera camera))
        {
            return false;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector2 playerScreenPosition = camera.WorldToScreenPoint(GetCurrentPosition());
        Vector2 offset = mousePosition - playerScreenPosition;
        if (offset.sqrMagnitude <= MinimumFacingSqrMagnitude)
        {
            return false;
        }

        direction = offset.normalized;
        return true;
    }

    public void WarpTo(Vector3 worldPosition)
    {
        Vector2 position = worldPosition;
        transform.position = new Vector3(position.x, position.y, worldPosition.z);
        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        lastAppliedVelocity = Vector2.zero;
        dashRemainingTime = 0f;
        wasDashingLastStep = false;
    }

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureBody();
        SanitizeConfiguration();
        InitializeRuntimeState();
    }

    private void Update()
    {
        if (IsGameplayInputBlockedByUI())
        {
            StopMotion();
            return;
        }

        UpdateStamina(Time.deltaTime);
        HandleDashInput();
        if (rotateTowardsMouse)
        {
            RotateTowardsMouse();
        }
    }

    private void FixedUpdate()
    {
        if (IsGameplayInputBlockedByUI())
        {
            StopMotion();
            return;
        }

        Vector2 velocity = ResolveVelocity(Time.fixedDeltaTime, out bool isDashing);
        ApplyVelocity(velocity, isDashing);
    }

    private void OnValidate()
    {
        ResolveReferences();
        ConfigureBody();
        SanitizeConfiguration();
    }

    private Vector2 ResolveVelocity(float deltaTime, out bool isDashing)
    {
        if (TryConsumeDashVelocity(deltaTime, out Vector2 dashVelocity))
        {
            isDashing = true;
            return dashVelocity;
        }

        isDashing = false;
        if (!TryGetCurrentMoveDirection(out Vector2 direction))
        {
            return Vector2.zero;
        }

        return direction * moveSpeed;
    }

    private void ApplyVelocity(Vector2 velocity, bool isDashing)
    {
        lastAppliedVelocity = velocity;
        wasDashingLastStep = isDashing;
        if (body == null)
        {
            transform.position += new Vector3(velocity.x, velocity.y, 0f) * Time.fixedDeltaTime;
            return;
        }

        body.linearVelocity = velocity;
    }

    private void HandleDashInput()
    {
        if (IsDashTriggered())
        {
            TryStartDash();
        }
    }

    private bool TryStartDash()
    {
        if (dashRemainingTime > 0f || dashDistance <= 0f || dashDuration <= 0f || currentStamina < dashStaminaCost)
        {
            return false;
        }

        if (!TryGetDashDirection(out Vector2 direction))
        {
            return false;
        }

        currentStamina = Mathf.Max(0f, currentStamina - dashStaminaCost);
        staminaRegenResumeTime = float.PositiveInfinity;
        dashDirection = direction;
        dashRemainingTime = dashDuration;
        return true;
    }

    private bool TryConsumeDashVelocity(float deltaTime, out Vector2 velocity)
    {
        velocity = Vector2.zero;
        if (dashRemainingTime <= 0f || deltaTime <= 0f)
        {
            return false;
        }

        float consumedTime = Mathf.Min(deltaTime, dashRemainingTime);
        float dashSpeed = dashDuration > 0f ? dashDistance / dashDuration : 0f;
        velocity = dashDirection * dashSpeed * (consumedTime / deltaTime);
        dashRemainingTime -= consumedTime;
        if (dashRemainingTime <= 0f)
        {
            EndDash();
        }

        return velocity.sqrMagnitude > MinimumFacingSqrMagnitude;
    }

    private void UpdateStamina(float deltaTime)
    {
        if (deltaTime <= 0f || currentStamina >= staminaMax || Time.time < staminaRegenResumeTime)
        {
            return;
        }

        currentStamina = Mathf.MoveTowards(currentStamina, staminaMax, staminaRegenPerSecond * deltaTime);
    }

    private bool TryGetDashDirection(out Vector2 direction)
    {
        if (TryGetCurrentMoveDirection(out direction))
        {
            return true;
        }

        direction = lastMoveDirection;
        if (direction.sqrMagnitude <= MinimumFacingSqrMagnitude)
        {
            return false;
        }

        direction.Normalize();
        return true;
    }

    private bool TryGetCurrentMoveDirection(out Vector2 direction)
    {
        direction = Vector2.ClampMagnitude(ReadMoveInput(), 1f);
        if (direction.sqrMagnitude <= MinimumFacingSqrMagnitude)
        {
            direction = Vector2.zero;
            return false;
        }

        direction.Normalize();
        lastMoveDirection = direction;
        return true;
    }

    private void EndDash()
    {
        dashRemainingTime = 0f;
        staminaRegenResumeTime = Time.time + staminaRegenDelay;
    }

    private void RotateTowardsMouse()
    {
        if (!TryGetMouseWorldPoint(out Vector3 mouseWorldPoint))
        {
            return;
        }

        Vector2 offset = (Vector2)mouseWorldPoint - GetCurrentPosition();
        if (offset.sqrMagnitude <= MinimumFacingSqrMagnitude)
        {
            return;
        }

        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg + facingAngleOffsetDegrees;
        GetFacingTransform().rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (Mouse.current == null || !TryGetTargetCamera(out Camera camera))
        {
            return false;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 screenPoint = new(mousePosition.x, mousePosition.y, camera.WorldToScreenPoint(transform.position).z);
        worldPoint = camera.ScreenToWorldPoint(screenPoint);
        worldPoint.z = transform.position.z;
        return true;
    }

    private bool TryGetTargetCamera(out Camera camera)
    {
        camera = targetCamera != null ? targetCamera : Camera.main;
        if (camera == null)
        {
            return false;
        }

        targetCamera = camera;
        return true;
    }

    private Transform GetFacingTransform()
    {
        return facingPivot != null ? facingPivot : transform;
    }

    private Vector2 GetCurrentPosition()
    {
        return body != null ? body.position : (Vector2)transform.position;
    }

    private void StopMotion()
    {
        EndDash();
        wasDashingLastStep = false;
        lastAppliedVelocity = Vector2.zero;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void ResolveReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        if (facingPivot == null)
        {
            facingPivot = transform;
        }
    }

    private void ConfigureBody()
    {
        if (body == null)
        {
            return;
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void SanitizeConfiguration()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(MinimumDashDuration, dashDuration);
        dashStaminaCost = Mathf.Max(0f, dashStaminaCost);
        staminaMax = Mathf.Max(0f, staminaMax);
        staminaRegenPerSecond = Mathf.Max(0f, staminaRegenPerSecond);
        staminaRegenDelay = Mathf.Max(0f, staminaRegenDelay);
        currentStamina = Mathf.Clamp(currentStamina, 0f, staminaMax);
    }

    private void InitializeRuntimeState()
    {
        currentStamina = staminaMax;
        staminaRegenResumeTime = 0f;
        dashRemainingTime = 0f;
        lastMoveDirection = Vector2.up;
        dashDirection = lastMoveDirection;
        lastAppliedVelocity = Vector2.zero;
        wasDashingLastStep = false;
    }

    private static Vector2 ReadMoveInput()
    {
        InputActionManager inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return Vector2.zero;
        }

        return inputManager.Player.Movement.Move.ReadValue<Vector2>();
    }

    private static bool IsDashTriggered()
    {
        InputActionManager inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return false;
        }

        return inputManager.Player.Movement.Accelerate.WasPressedThisFrame();
    }

    private static bool IsGameplayInputBlockedByUI()
    {
        return StatusController.HasStatus(StatusList.InBackPackStatus)
            || StatusController.HasStatus(StatusList.InHintStatus)
            || StatusController.HasStatus(StatusList.InUpgradeScreenStatus)
            || StatusController.HasStatus(StatusList.InNarrativeScreenStatus)
            || StatusController.HasStatus(StatusList.InPauseMenuStatus)
            || StatusController.HasStatus(StatusList.InSettlementScreenStatus)
            || StatusController.HasStatus(StatusList.InDialogStatus)
            || StatusController.HasStatus(StatusList.PausedStatus);
    }
}
